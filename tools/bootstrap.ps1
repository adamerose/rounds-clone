[CmdletBinding()]
param(
    [switch]$IncludeGodot
)

$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent $PSScriptRoot
$tools = Join-Path $repository '.tools'
$dotnetDirectory = Join-Path $tools 'dotnet'
$dotnet = Join-Path $dotnetDirectory 'dotnet.exe'
$dotnetVersion = '8.0.423'
$godotVersion = '4.7.1'
$godotDirectory = Join-Path $tools "godot-$godotVersion"

New-Item -ItemType Directory -Force $tools | Out-Null
if (-not (Test-Path -LiteralPath $dotnet)) {
    $installer = Join-Path $tools 'dotnet-install.ps1'
    Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installer
    & powershell -NoProfile -ExecutionPolicy Bypass -File $installer -Version $dotnetVersion -InstallDir $dotnetDirectory -NoPath
}

if ($IncludeGodot -and -not (Test-Path -LiteralPath $godotDirectory)) {
    $archive = Join-Path $tools "godot-$godotVersion.zip"
    $uri = "https://github.com/godotengine/godot-builds/releases/download/$godotVersion-stable/Godot_v$godotVersion-stable_mono_win64.zip"
    Invoke-WebRequest -Uri $uri -OutFile $archive
    Expand-Archive -LiteralPath $archive -DestinationPath $godotDirectory
    Remove-Item -LiteralPath $archive
}

Write-Output "dotnet=$dotnet"
if ($IncludeGodot) {
    $godot = Get-ChildItem -LiteralPath $godotDirectory -Filter '*mono_win64_console.exe' -Recurse | Select-Object -First 1 -ExpandProperty FullName
    Write-Output "godot=$godot"
}
