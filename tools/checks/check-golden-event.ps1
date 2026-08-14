param(
    [Parameter(Mandatory = $true)]
    [string]$HistoryBase,
    [Parameter(Mandatory = $true)]
    [string]$Established,
    [Parameter(Mandatory = $true)]
    [string]$Candidate,
    [switch]$ProspectiveMerge,
    [string]$Repository = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)),
    [string]$TrustedRoot = 'b9073b6a9c110b5fbca5e242d49bd03a8cecef12'
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath($Repository)
. (Join-Path $PSScriptRoot 'replay-ledger.ps1')
$historyScript = Join-Path $PSScriptRoot 'check-golden-history.ps1'
$trustedRoot = $TrustedRoot
$goldenPrefix = 'replays/golden/'
$goldenSuffix = '.rounds-replay.json'

function Invoke-Git {
    param([Parameter(ValueFromRemainingArguments = $true)][string[]]$Arguments)
    $result = & git @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($Arguments -join ' ') failed:`n$($result -join [Environment]::NewLine)" }
    return @($result)
}

function Resolve-Commit([string]$Revision) {
    return (@(Invoke-Git rev-parse --verify "$Revision`^{commit}"))[0].Trim()
}

function Assert-Trusted([string]$Commit) {
    & git merge-base --is-ancestor $trustedRoot $Commit
    if ($LASTEXITCODE -ne 0) { throw "Candidate revision $Commit does not descend from trusted repository inception $trustedRoot." }
}

function Get-TreeReplayMap([string]$Treeish) {
    $map = @{}
    $paths = @(Invoke-Git ls-tree -r --name-only $Treeish -- $goldenPrefix)
    foreach ($pathValue in $paths) {
        $path = $pathValue.Trim()
        if (-not $path.EndsWith($goldenSuffix, [StringComparison]::Ordinal)) { continue }
        $text = (Invoke-Git show "$Treeish`:$path") -join "`n"
        if ($text -notmatch '"finalHash":"(?<hash>[0-9a-f]{16})"}$') { throw "Effective replay `$path` has no canonical terminal finalHash." }
        $map[[IO.Path]::GetFileName($path)] = $Matches.hash
    }
    return $map
}

function Test-EffectiveCorpus([string]$Treeish) {
    $temporary = Join-Path ([IO.Path]::GetTempPath()) ("rounds-replay-corpus-" + [Guid]::NewGuid().ToString('N'))
    [IO.Directory]::CreateDirectory($temporary) | Out-Null
    try {
        $archive = Join-Path $temporary 'corpus.tar'
        & git archive --format=tar --output=$archive $Treeish replays
        if ($LASTEXITCODE -ne 0) { throw "Could not archive effective replay corpus $Treeish." }
        & tar -xf $archive -C $temporary
        if ($LASTEXITCODE -ne 0) { throw "Could not extract effective replay corpus $Treeish." }
        $effectiveLedger = Read-ReplayLedgerFromGit -Repository $repository -Revision $Treeish
        $reserved = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($entry in $effectiveLedger.Entries) {
            if ($entry.New -ceq 'deleted') { $null = $reserved.Add($entry.File) }
        }
        $goldenDirectory = Join-Path $temporary 'replays/golden'
        foreach ($file in [IO.Directory]::EnumerateFiles($goldenDirectory)) {
            if ($reserved.Contains([IO.Path]::GetFileName($file))) { throw "Effective replay corpus resurrects reserved basename `$([IO.Path]::GetFileName($file))`." }
        }
        $dotnet = if ($env:ROUNDS_DOTNET) { $env:ROUNDS_DOTNET } else { Join-Path $repository '.tools/dotnet/dotnet.exe' }
        $harness = if ($env:ROUNDS_HARNESS_PROJECT) { $env:ROUNDS_HARNESS_PROJECT } else { Join-Path $repository 'src/Rounds.Harness/Rounds.Harness.csproj' }
        $verification = & $dotnet run --project $harness --configuration Release --no-build --no-restore -- verify-replays --directory $goldenDirectory 2>&1
        if ($LASTEXITCODE -ne 0) { throw "Effective replay corpus failed canonical playback:`n$($verification -join [Environment]::NewLine)" }
    } finally {
        if ([IO.Directory]::Exists($temporary)) {
            Get-ChildItem -LiteralPath $temporary -Recurse -Force | ForEach-Object { $_.Attributes = [IO.FileAttributes]::Normal }
            [IO.Directory]::Delete($temporary, $true)
        }
    }
}

Push-Location $repository
try {
    $rootCommit = Resolve-Commit $trustedRoot
    if ($rootCommit -cne $trustedRoot) { throw 'Trusted repository inception does not resolve exactly.' }
    $historyCommit = Resolve-Commit $HistoryBase
    $establishedCommit = Resolve-Commit $Established
    $candidateCommit = Resolve-Commit $Candidate
    Assert-Trusted $historyCommit
    Assert-Trusted $establishedCommit
    Assert-Trusted $candidateCommit

    $historyOutput = & $historyScript -Base $historyCommit -Head $candidateCommit -Repository $repository
    if ($LASTEXITCODE -ne 0) { throw 'Per-commit golden history validation failed.' }
    if ($ProspectiveMerge -and $establishedCommit -cne $historyCommit) {
        $null = & $historyScript -Base $historyCommit -Head $establishedCommit -Repository $repository
        if ($LASTEXITCODE -ne 0) { throw 'Established golden history validation failed.' }
    }
    $transitions = @()
    foreach ($line in $historyOutput) {
        if ($line -match '^TRANSITION (?<commit>[0-9a-f]{40}) (?<file>[a-z0-9-]+\.rounds-replay\.json) (?<old>absent|[0-9a-f]{16}) (?<new>deleted|[0-9a-f]{16})$') {
            $transitions += [pscustomobject]@{ Commit = $Matches.commit; File = $Matches.file; Old = $Matches.old; New = $Matches.new }
        }
    }

    $effectiveTree = if ($ProspectiveMerge) {
        $mergeOutput = & git merge-tree --write-tree $establishedCommit $candidateCommit 2>&1
        if ($LASTEXITCODE -ne 0) { throw 'Prospective replay endpoint has a merge conflict.' }
        @($mergeOutput)[0].Trim()
    } else {
        (@(Invoke-Git rev-parse "$candidateCommit`^{tree}"))[0].Trim()
    }

    Test-EffectiveCorpus $effectiveTree
    $establishedMap = Get-TreeReplayMap $establishedCommit
    $effectiveMap = Get-TreeReplayMap $effectiveTree
    $names = @($establishedMap.Keys + $effectiveMap.Keys | Sort-Object -Unique)
    foreach ($name in $names) {
        $start = if ($establishedMap.ContainsKey($name)) { $establishedMap[$name] } else { 'absent' }
        $finish = if ($effectiveMap.ContainsKey($name)) { $effectiveMap[$name] } else { 'deleted' }
        if ($start -ceq $finish) { continue }
        $state = $start
        $applicable = @($transitions | Where-Object { $_.File -ceq $name })
        foreach ($transition in $applicable) {
            if ($transition.Old -cne $state) { throw "Endpoint transition chain for `$name` forks at $($transition.Commit): expected old $state, found $($transition.Old)." }
            $state = $transition.New
        }
        if ($state -cne $finish) { throw "Endpoint transition chain for `$name` ends at $state, expected $finish." }
    }
    $historyOutput | Write-Output
    Write-Output "golden event passed history=$historyCommit established=$establishedCommit candidate=$candidateCommit effective=$effectiveTree"
} finally {
    Pop-Location
}
