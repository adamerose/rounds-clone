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
$dotnetUri = 'https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.423/dotnet-sdk-8.0.423-win-x64.zip'
$dotnetHash = '063fcc35c136277e6fd767c66579f3b92db22a078a7f0c7177b6af1edb2c9afae1613f6cfdc01acf7421773d9ac77f0ef73a7fd8b37f469e7e3505e5c1361ba0'
$godotVersion = '4.7.1'
$godotUri = 'https://github.com/godotengine/godot-builds/releases/download/4.7.1-stable/Godot_v4.7.1-stable_mono_win64.zip'
$godotHash = '764a089809fb1a6f745686ce9f6d3ca83adce8fb60fb9a4e2324b63baaebaa45'
$godotDirectory = Join-Path $tools "godot-$godotVersion"

function Get-VerifiedArchive {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [Parameter(Mandatory)][string]$Destination,
        [Parameter(Mandatory)][string]$Algorithm,
        [Parameter(Mandatory)][string]$ExpectedHash
    )

    Invoke-WebRequest -Uri $Uri -OutFile $Destination
    $actualHash = (Get-FileHash -LiteralPath $Destination -Algorithm $Algorithm).Hash.ToLowerInvariant()
    if ($actualHash -ne $ExpectedHash) {
        Remove-Item -LiteralPath $Destination
        throw "Downloaded archive hash mismatch for $Uri."
    }
}

New-Item -ItemType Directory -Force $tools | Out-Null
if (-not (Test-Path -LiteralPath $dotnet)) {
    $archive = Join-Path $tools "dotnet-sdk-$dotnetVersion-win-x64.zip"
    Get-VerifiedArchive -Uri $dotnetUri -Destination $archive -Algorithm 'SHA512' -ExpectedHash $dotnetHash
    Expand-Archive -LiteralPath $archive -DestinationPath $dotnetDirectory -Force
    Remove-Item -LiteralPath $archive
}

$installedDotnetVersion = (& $dotnet --version).Trim()
if ($installedDotnetVersion -ne $dotnetVersion) {
    throw "Expected .NET SDK $dotnetVersion, found $installedDotnetVersion."
}

$godot = Get-ChildItem -LiteralPath $godotDirectory -Filter '*mono_win64_console.exe' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
if ($IncludeGodot -and -not $godot) {
    $archive = Join-Path $tools "godot-$godotVersion.zip"
    Get-VerifiedArchive -Uri $godotUri -Destination $archive -Algorithm 'SHA256' -ExpectedHash $godotHash
    Expand-Archive -LiteralPath $archive -DestinationPath $godotDirectory -Force
    Remove-Item -LiteralPath $archive
    $godot = Get-ChildItem -LiteralPath $godotDirectory -Filter '*mono_win64_console.exe' -Recurse | Select-Object -First 1 -ExpandProperty FullName
}

Write-Output "dotnet=$dotnet"
if ($IncludeGodot) {
    $installedGodotVersion = (& $godot --version).Trim()
    if (-not $installedGodotVersion.StartsWith("$godotVersion.stable.mono.", [System.StringComparison]::Ordinal)) {
        throw "Expected Godot $godotVersion .NET, found $installedGodotVersion."
    }
    Write-Output "godot=$godot"
}
