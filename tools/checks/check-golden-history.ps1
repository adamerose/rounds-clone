param(
    [Parameter(Mandatory = $true)]
    [string]$Base,
    [Parameter(Mandatory = $true)]
    [string]$Head,
    [string]$Repository = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath($Repository)
. (Join-Path $PSScriptRoot 'replay-ledger.ps1')
$goldenPrefix = 'replays/golden/'
$goldenSuffix = '.rounds-replay.json'
$script:ReplayVerifier = $null

function Invoke-Git {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    $result = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed:`n$($result -join [Environment]::NewLine)"
    }
    return @($result)
}

function Resolve-Commit([string]$Revision) {
    return (@(Invoke-Git rev-parse --verify "$Revision`^{commit}"))[0].Trim()
}

function Get-Parents([string]$Commit) {
    $line = (@(Invoke-Git rev-list --parents -n 1 $Commit))[0].Trim()
    if ($line.Length -eq 0) { return @() }
    return @($line.Split(' ', [StringSplitOptions]::RemoveEmptyEntries) | Select-Object -Skip 1)
}

function Get-FileText([string]$Revision, [string]$Path, [bool]$Required) {
    $exists = & git cat-file -e "$Revision`:$Path" 2>$null
    if ($LASTEXITCODE -ne 0) {
        if ($Required) { throw "Required file `$Path` is missing at $Revision." }
        return $null
    }
    $lines = Invoke-Git show "$Revision`:$Path"
    return ($lines -join "`n") + "`n"
}

function Get-ReplayHash([string]$Revision, [string]$Path) {
    $text = Get-FileText $Revision $Path $true
    if ($text -notmatch '"finalHash":"(?<hash>[0-9a-f]{16})"}\n$') {
        throw "Replay `$Path` at $Revision has no canonical terminal finalHash."
    }
    return $Matches.hash
}

function Test-TreeHasGolden([string]$Revision) {
    $paths = @(Invoke-Git ls-tree -r --name-only $Revision -- $goldenPrefix)
    return @($paths | Where-Object { $_.Trim().EndsWith($goldenSuffix, [StringComparison]::Ordinal) }).Count -gt 0
}

function Get-ReplayVerifier {
    if ($null -ne $script:ReplayVerifier) { return $script:ReplayVerifier }
    $dotnet = if ($env:ROUNDS_DOTNET) { $env:ROUNDS_DOTNET } else { Join-Path $repository '.tools/dotnet/dotnet.exe' }
    $harness = if ($env:ROUNDS_HARNESS_PROJECT) { $env:ROUNDS_HARNESS_PROJECT } else { Join-Path $repository 'src/Rounds.Harness/Rounds.Harness.csproj' }
    $assembly = Join-Path (Split-Path -Parent $harness) 'bin/Release/net8.0/Rounds.Harness.dll'
    if (-not [IO.File]::Exists($assembly)) {
        $restoreOutput = @(& $dotnet restore $harness --locked-mode 2>&1)
        if ($LASTEXITCODE -ne 0) { throw "Could not restore the replay verifier before historical playback:`n$($restoreOutput -join [Environment]::NewLine)" }
        $buildOutput = @(& $dotnet build $harness --configuration Release --no-restore 2>&1)
        if ($LASTEXITCODE -ne 0 -or -not [IO.File]::Exists($assembly)) {
            throw "Could not build the replay verifier before historical playback:`n$($buildOutput -join [Environment]::NewLine)"
        }
    }
    $script:ReplayVerifier = [pscustomobject]@{ Dotnet = $dotnet; Harness = $harness }
    return $script:ReplayVerifier
}

function Export-GitBlob([string]$Revision, [string]$Path, [string]$Destination) {
    $objectId = (@(Invoke-Git rev-parse --verify "$Revision`:$Path"))[0].Trim()
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = 'git'
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    foreach ($argument in @('-C', $repository, 'cat-file', 'blob', $objectId)) { $null = $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::Start($start)
    $errors = $process.StandardError.ReadToEndAsync()
    $stream = [IO.File]::Create($Destination)
    try {
        $process.StandardOutput.BaseStream.CopyTo($stream)
        $stream.Dispose()
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "Could not export replay `$Path` at $Revision`: $($errors.Result)" }
    } finally {
        $stream.Dispose()
        $process.Dispose()
    }
}

function Test-ReplayAtRevision([string]$Revision, [string]$Path, [string]$ExpectedHash) {
    $temporary = Join-Path ([IO.Path]::GetTempPath()) ("rounds-replay-history-" + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($temporary) | Out-Null
    try {
        $replayPath = Join-Path $temporary ([IO.Path]::GetFileName($Path))
        Export-GitBlob -Revision $Revision -Path $Path -Destination $replayPath
        $verifier = Get-ReplayVerifier
        $verification = @(& $verifier.Dotnet run --project $verifier.Harness --configuration Release --no-build --no-restore -- verify-replays --directory $temporary 2>&1)
        if ($LASTEXITCODE -ne 0 -or -not ($verification | Where-Object { $_ -match "^verified id=[a-z0-9-]+ ticks=[0-9]+ hash=$ExpectedHash$" })) {
            throw "Golden `$Path` failed canonical playback at $Revision`:`n$($verification -join [Environment]::NewLine)"
        }
    } finally {
        if ([IO.Directory]::Exists($temporary)) {
            Get-ChildItem -LiteralPath $temporary -Force -Recurse | ForEach-Object { $_.Attributes = [IO.FileAttributes]::Normal }
            [IO.Directory]::Delete($temporary, $true)
        }
    }
}

function Get-GoldenChanges([string]$Parent, [string]$Commit) {
    if ($Parent.Length -eq 0) {
        $paths = @(Invoke-Git ls-tree -r --name-only $Commit -- $goldenPrefix)
        return @($paths | Where-Object { $_.StartsWith($goldenPrefix, [StringComparison]::Ordinal) -and $_.EndsWith($goldenSuffix, [StringComparison]::Ordinal) } | ForEach-Object { [pscustomobject]@{ Status = 'A'; Path = $_ } })
    }
    $lines = @(Invoke-Git diff-tree --no-commit-id --name-status -r --no-renames $Parent $Commit -- $goldenPrefix)
    $changes = @()
    foreach ($line in $lines) {
        if ($line -notmatch '^(?<status>[AMD])\s+(?<path>.+)$') { throw "Unsupported golden diff record at $Commit`: $line" }
        $path = $Matches.path
        if (-not $path.EndsWith($goldenSuffix, [StringComparison]::Ordinal)) { continue }
        $changes += [pscustomobject]@{ Status = $Matches.status; Path = $path }
    }
    return $changes
}

Push-Location $repository
try {
    $shallow = (@(Invoke-Git rev-parse --is-shallow-repository))[0].Trim()
    if ($shallow -cne 'false') { throw 'Golden history policy refuses a shallow repository.' }
    $headCommit = Resolve-Commit $Head
    if ($Base -ceq 'ROOT') {
        if ((Get-Parents $headCommit).Count -ne 0) { throw 'ROOT is valid only when Head itself is a root commit.' }
        $commits = @($headCommit)
    } else {
        $baseCommit = Resolve-Commit $Base
        & git merge-base --is-ancestor $baseCommit $headCommit
        if ($LASTEXITCODE -ne 0) { throw "Golden history base $baseCommit is not an ancestor of $headCommit." }
        $commits = @(Invoke-Git rev-list --reverse --topo-order $headCommit "^$baseCommit")
    }

    foreach ($commitValue in $commits) {
        $commit = $commitValue.Trim()
        $parents = @(Get-Parents $commit)
        if ($parents.Count -gt 1) {
            if ($parents.Count -ne 2) { throw "Merge $commit has $($parents.Count) parents; only clean two-parent merges are supported." }
            $mergeOutput = & git merge-tree --write-tree $parents[0] $parents[1] 2>&1
            if ($LASTEXITCODE -ne 0) { throw "Merge $commit is not a conflict-free automatic merge." }
            $automaticTree = @($mergeOutput)[0].Trim()
            $actualTree = (@(Invoke-Git rev-parse "$commit`^{tree}"))[0].Trim()
            if ($automaticTree -cne $actualTree) { throw "Merge $commit changes the automatic merge tree and must be recreated cleanly." }
            $mergeLedger = Read-ReplayLedgerFromGit -Repository $repository -Revision $commit -Required $false
            if ($mergeLedger.Bytes.Length -eq 0 -and (Test-TreeHasGolden $commit)) {
                throw "Replay policy ledger is missing while golden replays exist at merge $commit."
            }
            foreach ($parentValue in $parents) {
                $parentLedger = Read-ReplayLedgerFromGit -Repository $repository -Revision $parentValue -Required $false
                if (-not (Test-ReplayLedgerBytePrefix -Candidate $mergeLedger.Bytes -Prefix $parentLedger.Bytes)) {
                    throw "Replay break ledger was removed or truncated by merge $commit."
                }
            }
            continue
        }

        $parent = if ($parents.Count -eq 1) { $parents[0] } else { '' }
        $oldLedger = if ($parent.Length -gt 0) { Read-ReplayLedgerFromGit -Repository $repository -Revision $parent -Required $false } else { [pscustomobject]@{ Bytes = [byte[]]@(); Text = ''; Lines = @(); Entries = @() } }
        $newLedger = Read-ReplayLedgerFromGit -Repository $repository -Revision $commit -Required $false
        if ($newLedger.Bytes.Length -eq 0 -and (Test-TreeHasGolden $commit)) {
            throw "Replay policy ledger is missing while golden replays exist at $commit."
        }
        if ($parent.Length -gt 0 -and -not (Test-ReplayLedgerBytePrefix -Candidate $newLedger.Bytes -Prefix $oldLedger.Bytes)) {
            throw "Replay break ledger was edited, reordered, or truncated at $commit."
        }
        $addedEntries = @($newLedger.Entries | Select-Object -Skip $oldLedger.Entries.Count)
        $used = [Collections.Generic.HashSet[int]]::new()
        foreach ($change in @(Get-GoldenChanges $parent $commit)) {
            $file = [IO.Path]::GetFileName($change.Path)
            if ($change.Status -ceq 'A') {
                if ($newLedger.Entries | Where-Object { $_.File -ceq $file -and $_.New -ceq 'deleted' }) {
                    throw "Golden `$file` reuses a permanently reserved deleted basename at $commit."
                }
                $newHash = Get-ReplayHash $commit $change.Path
                Test-ReplayAtRevision -Revision $commit -Path $change.Path -ExpectedHash $newHash
                Write-Output "TRANSITION $commit $file absent $newHash"
                continue
            }
            $oldHash = Get-ReplayHash $parent $change.Path
            $newHash = if ($change.Status -ceq 'D') { 'deleted' } else { Get-ReplayHash $commit $change.Path }
            if ($change.Status -cne 'D') { Test-ReplayAtRevision -Revision $commit -Path $change.Path -ExpectedHash $newHash }
            $matches = @()
            for ($index = 0; $index -lt $addedEntries.Count; $index++) {
                $entry = $addedEntries[$index]
                if ($entry.File -ceq $file -and $entry.Old -ceq $oldHash -and $entry.New -ceq $newHash) { $matches += $index }
            }
            if ($matches.Count -ne 1) { throw "Golden transition `$file` $oldHash->$newHash at $commit requires exactly one same-commit ledger entry." }
            $null = $used.Add($matches[0])
            Write-Output "TRANSITION $commit $file $oldHash $newHash"
        }
        if ($used.Count -ne $addedEntries.Count) { throw "Replay break ledger adds an orphan transition at $commit." }
    }
    Write-Output "golden history passed base=$Base head=$headCommit commits=$($commits.Count)"
} finally {
    Pop-Location
}
