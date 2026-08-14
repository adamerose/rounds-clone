param(
    [Parameter(Mandatory = $true)]
    [string]$Replay,
    [Parameter(Mandatory = $true)]
    [string]$Output
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$replayPath = if ([System.IO.Path]::IsPathFullyQualified($Replay)) {
    [System.IO.Path]::GetFullPath($Replay)
} else {
    [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Replay))
}
$outputPath = if ([System.IO.Path]::IsPathFullyQualified($Output)) {
    [System.IO.Path]::GetFullPath($Output)
} else {
    [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Output))
}
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

function Read-FourCC([byte[]]$Data, [int]$Offset) {
    return [Text.Encoding]::ASCII.GetString($Data, $Offset, 4)
}

$moviStart = -1
$moviEnd = -1
$chunk = 12
while ($chunk -le $bytes.Length - 12) {
    $chunkId = Read-FourCC $bytes $chunk
    $chunkSize = [BitConverter]::ToUInt32($bytes, $chunk + 4)
    $next = [long]$chunk + 8 + $chunkSize + ($chunkSize -band 1)
    if ($next -gt $bytes.Length) { throw "AVI top-level chunk `$chunkId` exceeds the file." }
    if ($chunkId -ceq 'LIST' -and (Read-FourCC $bytes ($chunk + 8)) -ceq 'movi') {
        $moviStart = $chunk + 12
        $moviEnd = $chunk + 8 + $chunkSize
        break
    }
    $chunk = [int]$next
}
if ($moviStart -lt 0) { throw 'Replay movie has no AVI movi list.' }

$videoChunks = [Collections.Generic.List[object]]::new()
$chunk = $moviStart
while ($chunk -le $moviEnd - 8) {
    $chunkId = Read-FourCC $bytes $chunk
    $chunkSize = [BitConverter]::ToUInt32($bytes, $chunk + 4)
    $payload = $chunk + 8
    $next = [long]$payload + $chunkSize + ($chunkSize -band 1)
    if ($next -gt $moviEnd) { throw "AVI movi chunk `$chunkId` exceeds its list." }
    if ($chunkId -ceq '00db') {
        $videoChunks.Add([pscustomobject]@{ Offset = $payload; Size = [int]$chunkSize })
    }
    $chunk = [int]$next
}
if ($chunk -ne $moviEnd -or $videoChunks.Count -ne $totalTicks) {
    throw "Replay movie contains $($videoChunks.Count) decodable video chunks; expected $totalTicks."
}

Add-Type -AssemblyName System.Drawing.Common
function Measure-DecodedFrame([int]$FrameNumber) {
    $record = $videoChunks[$FrameNumber - 1]
    $stream = [IO.MemoryStream]::new($bytes, $record.Offset, $record.Size, $false, $true)
    $source = $null
    $bitmap = $null
    $graphics = $null
    $locked = $null
    try {
        $source = [Drawing.Image]::FromStream($stream, $true, $true)
        if ($source.Width -ne 1280 -or $source.Height -ne 720) {
            throw "Replay frame $FrameNumber decoded as $($source.Width)x$($source.Height); expected 1280x720."
        }
        $bitmap = [Drawing.Bitmap]::new(1280, 720, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $graphics = [Drawing.Graphics]::FromImage($bitmap)
        $graphics.DrawImageUnscaled($source, 0, 0)
        $rectangle = [Drawing.Rectangle]::new(0, 0, 1280, 720)
        $locked = $bitmap.LockBits($rectangle, [Drawing.Imaging.ImageLockMode]::ReadOnly, [Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $pixels = [byte[]]::new([Math]::Abs($locked.Stride) * $locked.Height)
        [Runtime.InteropServices.Marshal]::Copy($locked.Scan0, $pixels, 0, $pixels.Length)
        $paper = 0
        $red = 0
        $blue = 0
        for ($y = 0; $y -lt 720; $y++) {
            $row = $y * [Math]::Abs($locked.Stride)
            for ($x = 0; $x -lt 1280; $x++) {
                $pixel = $row + ($x * 3)
                $b = [int]$pixels[$pixel]
                $g = [int]$pixels[$pixel + 1]
                $r = [int]$pixels[$pixel + 2]
                if ($r -gt 210 -and $g -gt 210 -and $b -gt 200) { $paper++ }
                if ($r -gt 180 -and $r -gt ($g * 1.8) -and $r -gt ($b * 1.4)) { $red++ }
                if ($b -gt 150 -and $b -gt ($r * 1.5) -and $b -gt ($g * 1.05)) { $blue++ }
            }
        }
        if ($paper -lt 120000) {
            throw "Replay frame $FrameNumber is visually incomplete after independent decode: paper=$paper red=$red blue=$blue."
        }
        return [pscustomobject]@{ Frame = $FrameNumber; Paper = $paper; Red = $red; Blue = $blue }
    } finally {
        if ($locked -ne $null) { $bitmap.UnlockBits($locked) }
        if ($graphics -ne $null) { $graphics.Dispose() }
        if ($bitmap -ne $null) { $bitmap.Dispose() }
        if ($source -ne $null) { $source.Dispose() }
        $stream.Dispose()
    }
}

$decoded = @{}
$genericFrames = @(1, [Math]::Max(1, [int][Math]::Ceiling($totalTicks / 2.0)), $totalTicks) | Select-Object -Unique
foreach ($frameNumber in $genericFrames) {
    $decoded[$frameNumber] = Measure-DecodedFrame $frameNumber
}
if ($decoded[1].Red -lt 1000 -or $decoded[1].Blue -lt 1000) {
    throw "Replay first frame does not show both players after independent decode: red=$($decoded[1].Red) blue=$($decoded[1].Blue)."
}

$isCanonicalGolden = $replayId -ceq 'base-combat-006-seed-1' -and $totalTicks -eq 600 -and $expectedHash -ceq 'b91f86b6f1dc6b10'
if ($isCanonicalGolden) {
    foreach ($frameNumber in @(1, 62, 100, 181, 300, 600)) {
        if (-not $decoded.ContainsKey($frameNumber)) { $decoded[$frameNumber] = Measure-DecodedFrame $frameNumber }
    }
    if ($decoded[62].Red -lt $decoded[1].Red + 500) { throw 'Shield representative frame does not contain its expected team-color expansion.' }
    if (($decoded[181].Red + $decoded[181].Blue) -ge ($decoded[1].Red + $decoded[1].Blue - 500)) { throw 'Result representative frame does not show the expected defeated-player change.' }
    if (($decoded[300].Red + $decoded[300].Blue) -lt 4000) { throw 'Reset representative frame does not restore both visible players.' }
}
$decodedSummary = $decoded.Keys | Sort-Object | ForEach-Object { $value = $decoded[$_]; "$($value.Frame):$($value.Paper)/$($value.Red)/$($value.Blue)" }
Write-Output "independently decoded replay frames (frame:paper/red/blue) $($decodedSummary -join ', ')"

Write-Output "validated replay movie id=$replayId ticks=$totalTicks hash=$expectedHash frames=$declaredFrames output=$outputPath"
