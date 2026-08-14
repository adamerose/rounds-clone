param(
    [Parameter(Mandatory = $true)]
    [string]$Replay,
    [Parameter(Mandatory = $true)]
    [string]$Output
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$replayPath = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Replay))
$outputPath = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Output))
if (-not [System.IO.File]::Exists($replayPath)) {
    throw "Replay file does not exist: $replayPath"
}
if ([System.IO.Path]::GetExtension($outputPath) -cne '.avi') {
    throw 'Replay movie output must use the lowercase .avi extension.'
}

$dotnet = Join-Path $repository '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    & (Join-Path $repository 'tools/bootstrap.ps1') -IncludeGodot
}

$verification = & $dotnet run --project (Join-Path $repository 'src/Rounds.Harness/Rounds.Harness.csproj') --configuration Release --no-restore -- replay --input $replayPath 2>&1
if ($LASTEXITCODE -ne 0) {
    throw "Headless replay verification failed:`n$($verification -join [Environment]::NewLine)"
}
$verificationLine = $verification | Where-Object { $_ -match '^replayed id=([a-z0-9-]+) ticks=([0-9]+) hash=([0-9a-f]{16}) ' } | Select-Object -Last 1
if (-not $verificationLine) {
    throw 'Headless replay verification did not report canonical ID, tick count, and hash.'
}
$null = $verificationLine -match '^replayed id=([a-z0-9-]+) ticks=([0-9]+) hash=([0-9a-f]{16}) '
$replayId = $Matches[1]
$totalTicks = [int]::Parse($Matches[2], [Globalization.CultureInfo]::InvariantCulture)
$expectedHash = $Matches[3]

$outputDirectory = Split-Path -Parent $outputPath
[System.IO.Directory]::CreateDirectory($outputDirectory) | Out-Null
if ([System.IO.File]::Exists($outputPath)) {
    [System.IO.File]::Delete($outputPath)
}

$godot = Get-ChildItem (Join-Path $repository '.tools/godot-4.7.1') -Filter '*mono_win64_console.exe' -Recurse | Select-Object -First 1 -ExpandProperty FullName
if (-not $godot) {
    & (Join-Path $repository 'tools/bootstrap.ps1') -IncludeGodot
    $godot = Get-ChildItem (Join-Path $repository '.tools/godot-4.7.1') -Filter '*mono_win64_console.exe' -Recurse | Select-Object -First 1 -ExpandProperty FullName
}
if (-not $godot) {
    throw 'Pinned Godot console executable is unavailable.'
}

$movieOutput = & $godot --path (Join-Path $repository 'game') --write-movie $outputPath --fixed-fps 60 --quit-after $totalTicks -- --replay $replayPath 2>&1
$movieExit = $LASTEXITCODE
$movieOutput | Write-Output
if ($movieExit -ne 0) {
    throw "Godot replay render exited with code $movieExit."
}
$expectedMarker = "REPLAY_COMPLETE id=$replayId ticks=$totalTicks hash=$expectedHash frames=$totalTicks"
$markers = @($movieOutput | Where-Object { $_ -ceq $expectedMarker })
if ($markers.Count -ne 1) {
    throw "Godot replay render did not emit exactly one expected completion marker: $expectedMarker"
}
if (-not [System.IO.File]::Exists($outputPath)) {
    throw "Godot replay render did not create: $outputPath"
}

$bytes = [System.IO.File]::ReadAllBytes($outputPath)
if ($bytes.Length -lt 32 -or
    [Text.Encoding]::ASCII.GetString($bytes, 0, 4) -cne 'RIFF' -or
    [Text.Encoding]::ASCII.GetString($bytes, 8, 4) -cne 'AVI ') {
    throw 'Replay movie is not a nonempty RIFF AVI.'
}
$avih = -1
for ($index = 12; $index -le $bytes.Length - 28; $index++) {
    if ($bytes[$index] -eq 0x61 -and $bytes[$index + 1] -eq 0x76 -and $bytes[$index + 2] -eq 0x69 -and $bytes[$index + 3] -eq 0x68) {
        $avih = $index
        break
    }
}
if ($avih -lt 0) {
    throw 'Replay movie has no AVI main header.'
}
$declaredFrames = [BitConverter]::ToUInt32($bytes, $avih + 24)
if ($declaredFrames -ne $totalTicks) {
    throw "Replay movie declares $declaredFrames frames; expected $totalTicks."
}

Write-Output "validated replay movie id=$replayId ticks=$totalTicks hash=$expectedHash frames=$declaredFrames output=$outputPath"
