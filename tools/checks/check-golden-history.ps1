param(
    [Parameter(Mandatory = $true)]
    [string]$Base,
    [Parameter(Mandatory = $true)]
    [string]$Head,
    [string]$Repository = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot))
)

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath($Repository)
$goldenPrefix = 'replays/golden/'
$goldenSuffix = '.rounds-replay.json'
$ledgerPath = 'replays/intentional-breaks.md'
$ledgerHeader = '# Intentional replay breaks'
$ledgerPattern = '^- replay: (?<file>[a-z0-9-]+\.rounds-replay\.json); old: (?<old>[0-9a-f]{16}); new: (?<new>[0-9a-f]{16}|deleted); reason: (?<reason>[^;]{1,200})$'

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

function Read-Ledger([string]$Revision) {
    $text = Get-FileText $Revision $ledgerPath $false
    if ($null -eq $text) { return [pscustomobject]@{ Text = ''; Lines = @(); Entries = @() } }
    if ($text -ceq "$ledgerHeader`n") { return [pscustomobject]@{ Text = $text; Lines = @(); Entries = @() } }
    $lines = @($text -split "`n")
    if ($lines.Count -lt 4 -or $lines[0] -cne $ledgerHeader -or $lines[1] -cne '' -or $lines[-1] -cne '') {
        throw "Replay break ledger at $Revision has a noncanonical heading or newline layout."
    }
    $entryLines = @($lines[2..($lines.Count - 2)] | Where-Object { $_.Length -gt 0 })
    $entries = @()
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($line in $entryLines) {
        if ($line -cnotmatch $ledgerPattern) { throw "Replay break ledger line is malformed at $Revision`: $line" }
        $reason = $Matches.reason
        $invalidCharacters = @($reason.ToCharArray() | Where-Object { [int]$_ -lt 0x20 -or [int]$_ -gt 0x7e })
        if ($reason[0] -eq ' ' -or $reason[-1] -eq ' ' -or $invalidCharacters.Count -gt 0) {
            throw "Replay break reason is noncanonical at $Revision."
        }
        $key = "$($Matches.file)|$($Matches.old)|$($Matches.new)"
        if (-not $seen.Add($key)) { throw "Replay break ledger duplicates transition `$key` at $Revision." }
        $entries += [pscustomobject]@{ Line = $line; File = $Matches.file; Old = $Matches.old; New = $Matches.new }
    }
    return [pscustomobject]@{ Text = $text; Lines = $entryLines; Entries = $entries }
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
            continue
        }

        $parent = if ($parents.Count -eq 1) { $parents[0] } else { '' }
        $oldLedger = if ($parent.Length -gt 0) { Read-Ledger $parent } else { [pscustomobject]@{ Text = ''; Lines = @(); Entries = @() } }
        $newLedger = Read-Ledger $commit
        if ($parent.Length -gt 0 -and -not $newLedger.Text.StartsWith($oldLedger.Text, [StringComparison]::Ordinal)) {
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
                Write-Output "TRANSITION $commit $file absent $newHash"
                continue
            }
            $oldHash = Get-ReplayHash $parent $change.Path
            $newHash = if ($change.Status -ceq 'D') { 'deleted' } else { Get-ReplayHash $commit $change.Path }
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
