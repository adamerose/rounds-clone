param([string]$Repository = (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)))

$ErrorActionPreference = 'Stop'
$repository = [IO.Path]::GetFullPath($Repository)
$dotnet = Join-Path $repository '.tools/dotnet/dotnet.exe'
$harness = Join-Path $repository 'src/Rounds.Harness/Rounds.Harness.csproj'
$temporary = Join-Path ([IO.Path]::GetTempPath()) ("rounds-replay-cli-" + [Guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($temporary) | Out-Null

function Invoke-Harness {
    param([string[]]$Arguments)
    $output = @(& $dotnet run --project $harness --configuration Release --no-build --no-restore -- @Arguments 2>&1)
    return [pscustomobject]@{ ExitCode = $LASTEXITCODE; Output = $output }
}

try {
    $recorded = Join-Path $temporary 'process-check.rounds-replay.json'
    $record = Invoke-Harness -Arguments @('record', '--profile', 'base-combat', '--id', 'process-check', '--seed', '7', '--ticks', '61', '--output', $recorded)
    if ($record.ExitCode -ne 0 -or -not [IO.File]::Exists($recorded) -or -not ($record.Output | Where-Object { $_ -match '^recorded id=process-check ticks=61 hash=[0-9a-f]{16} output=' })) {
        throw "Replay record command failed its public contract:`n$($record.Output -join [Environment]::NewLine)"
    }

    $replay = Invoke-Harness -Arguments @('replay', '--input', $recorded)
    if ($replay.ExitCode -ne 0 -or -not ($replay.Output | Where-Object { $_ -match '^replayed id=process-check ticks=61 hash=[0-9a-f]{16} ' })) {
        throw "Replay command failed its public contract:`n$($replay.Output -join [Environment]::NewLine)"
    }

    $corrupt = Join-Path $temporary 'corrupt.rounds-replay.json'
    $corruptBytes = [IO.File]::ReadAllBytes($recorded)
    [IO.File]::WriteAllBytes($corrupt, [byte[]]@($corruptBytes + [byte][char]' '))
    $corruptResult = Invoke-Harness -Arguments @('replay', '--input', $corrupt)
    if ($corruptResult.ExitCode -eq 0 -or -not ($corruptResult.Output | Where-Object { $_ -match 'canonical' })) {
        throw 'Corrupt replay did not return a useful nonzero diagnostic.'
    }

    $mismatch = Join-Path $temporary 'mismatch.rounds-replay.json'
    $text = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($recorded))
    if ($text -notmatch '"finalHash":"(?<hash>[0-9a-f]{16})"}\n$') { throw 'Recorded process fixture has no canonical final hash.' }
    $oldHash = $Matches.hash
    $badHash = if ($oldHash -cne '0000000000000000') { '0000000000000000' } else { '0000000000000001' }
    [IO.File]::WriteAllText($mismatch, $text.Replace($oldHash, $badHash, [StringComparison]::Ordinal), [Text.UTF8Encoding]::new($false))
    $mismatchResult = Invoke-Harness -Arguments @('replay', '--input', $mismatch)
    if ($mismatchResult.ExitCode -eq 0 -or -not ($mismatchResult.Output | Where-Object { $_ -match 'diverged at tick' })) {
        throw 'Hash-mismatching replay did not return a useful nonzero diagnostic.'
    }

    $corpus = Join-Path $temporary 'corpus'
    [IO.Directory]::CreateDirectory($corpus) | Out-Null
    foreach ($item in @(@{ Id = 'zeta'; Seed = '9' }, @{ Id = 'alpha'; Seed = '8' })) {
        $path = Join-Path $corpus ($item.Id + '.rounds-replay.json')
        $corpusRecord = Invoke-Harness -Arguments @('record', '--profile', 'base-combat', '--id', $item.Id, '--seed', $item.Seed, '--ticks', '1', '--output', $path)
        if ($corpusRecord.ExitCode -ne 0) { throw "Could not create ordinal corpus replay $($item.Id)." }
    }
    $corpusResult = Invoke-Harness -Arguments @('verify-replays', '--directory', $corpus)
    $verifiedIds = @($corpusResult.Output | ForEach-Object { if ($_ -match '^verified id=(?<id>[a-z0-9-]+) ') { $Matches.id } })
    if ($corpusResult.ExitCode -ne 0 -or ($verifiedIds -join ',') -cne 'alpha,zeta' -or
        -not ($corpusResult.Output | Where-Object { $_ -ceq 'verified replay corpus count=2' })) {
        throw "Multi-file process verification was not ordinal and complete:`n$($corpusResult.Output -join [Environment]::NewLine)"
    }

    Write-Output 'replay CLI record, replay, corruption, mismatch, and ordinal multi-file checks passed'
} finally {
    if ([IO.Directory]::Exists($temporary)) {
        Get-ChildItem -LiteralPath $temporary -Force -Recurse | ForEach-Object { $_.Attributes = [IO.FileAttributes]::Normal }
        [IO.Directory]::Delete($temporary, $true)
    }
}

# The final exercised command is intentionally a failing replay. Reset the native
# process status only after every assertion and cleanup has completed successfully.
$global:LASTEXITCODE = 0
