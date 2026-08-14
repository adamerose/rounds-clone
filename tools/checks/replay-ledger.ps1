$script:ReplayLedgerPath = 'replays/intentional-breaks.md'
$script:ReplayLedgerHeader = '# Intentional replay breaks'
$script:ReplayLedgerPattern = '^- replay: (?<file>[a-z0-9-]+\.rounds-replay\.json); old: (?<old>[0-9a-f]{16}); new: (?<new>[0-9a-f]{16}|deleted); reason: (?<reason>[^;]{1,200})$'

function Get-ReplayLedgerBlobBytes {
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][string]$Revision,
        [bool]$Required = $true
    )

    $object = @(& git -C $Repository rev-parse --verify "$Revision`:$script:ReplayLedgerPath" 2>$null)
    if ($LASTEXITCODE -ne 0) {
        if ($Required) { throw "Required file `$script:ReplayLedgerPath` is missing at $Revision." }
        return $null
    }
    $objectId = $object[0].Trim()
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = 'git'
    $start.UseShellExecute = $false
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $null = $start.ArgumentList.Add('-C')
    $null = $start.ArgumentList.Add($Repository)
    $null = $start.ArgumentList.Add('cat-file')
    $null = $start.ArgumentList.Add('blob')
    $null = $start.ArgumentList.Add($objectId)
    $process = [Diagnostics.Process]::Start($start)
    $errors = $process.StandardError.ReadToEndAsync()
    $bytes = [IO.MemoryStream]::new()
    try {
        $process.StandardOutput.BaseStream.CopyTo($bytes)
        $process.WaitForExit()
        if ($process.ExitCode -ne 0) { throw "Could not read replay ledger blob at $Revision`: $($errors.Result)" }
        return $bytes.ToArray()
    } finally {
        $bytes.Dispose()
        $process.Dispose()
    }
}

function ConvertFrom-ReplayLedgerBytes {
    param(
        [Parameter(Mandatory = $true)][byte[]]$Bytes,
        [Parameter(Mandatory = $true)][string]$Context
    )

    foreach ($byte in $Bytes) {
        if ($byte -ne 0x0a -and ($byte -lt 0x20 -or $byte -gt 0x7e)) {
            throw "Replay break ledger at $Context is not canonical printable ASCII with LF newlines."
        }
    }
    $text = [Text.Encoding]::ASCII.GetString($Bytes)
    if ($text -ceq "$script:ReplayLedgerHeader`n") {
        return [pscustomobject]@{ Bytes = $Bytes; Text = $text; Lines = @(); Entries = @() }
    }
    if (-not $text.StartsWith("$script:ReplayLedgerHeader`n`n", [StringComparison]::Ordinal) -or -not $text.EndsWith("`n", [StringComparison]::Ordinal)) {
        throw "Replay break ledger at $Context has a noncanonical heading or newline layout."
    }

    $lines = @($text.Split("`n", [StringSplitOptions]::None))
    if ($lines.Count -lt 4 -or $lines[0] -cne $script:ReplayLedgerHeader -or $lines[1] -cne '' -or $lines[-1] -cne '') {
        throw "Replay break ledger at $Context has a noncanonical heading or newline layout."
    }
    $entryLines = @($lines[2..($lines.Count - 2)])
    if ($entryLines | Where-Object { $_.Length -eq 0 }) {
        throw "Replay break ledger at $Context contains a blank entry line."
    }

    $entries = @()
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($line in $entryLines) {
        if ($line -cnotmatch $script:ReplayLedgerPattern) { throw "Replay break ledger line is malformed at $Context`: $line" }
        $reason = $Matches.reason
        if ($reason[0] -eq ' ' -or $reason[-1] -eq ' ') { throw "Replay break reason is noncanonical at $Context." }
        $key = "$($Matches.file)|$($Matches.old)|$($Matches.new)"
        if (-not $seen.Add($key)) { throw "Replay break ledger duplicates transition `$key` at $Context." }
        $entries += [pscustomobject]@{ Line = $line; File = $Matches.file; Old = $Matches.old; New = $Matches.new }
    }
    return [pscustomobject]@{ Bytes = $Bytes; Text = $text; Lines = $entryLines; Entries = $entries }
}

function Read-ReplayLedgerFromGit {
    param(
        [Parameter(Mandatory = $true)][string]$Repository,
        [Parameter(Mandatory = $true)][string]$Revision,
        [bool]$Required = $true
    )

    $bytes = Get-ReplayLedgerBlobBytes -Repository $Repository -Revision $Revision -Required $Required
    if ($null -eq $bytes) { return [pscustomobject]@{ Bytes = [byte[]]@(); Text = ''; Lines = @(); Entries = @() } }
    return ConvertFrom-ReplayLedgerBytes -Bytes $bytes -Context $Revision
}

function Test-ReplayLedgerBytePrefix {
    param([byte[]]$Candidate, [byte[]]$Prefix)
    if ($Candidate.Length -lt $Prefix.Length) { return $false }
    for ($index = 0; $index -lt $Prefix.Length; $index++) {
        if ($Candidate[$index] -ne $Prefix[$index]) { return $false }
    }
    return $true
}
