$ErrorActionPreference = 'Stop'
$repository = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$dotnet = Join-Path $repository '.tools/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet)) {
    & (Join-Path $repository 'tools/bootstrap.ps1') -IncludeGodot
}

Push-Location $repository
try {
    & $dotnet restore 'Rounds.sln' --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & $dotnet build 'Rounds.sln' --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    & $dotnet run --project 'tools/Rounds.Checks/Rounds.Checks.csproj' --configuration Release --no-build --no-restore -- .
    if ($LASTEXITCODE -ne 0) { throw 'Repository checks failed.' }
    & $dotnet test 'Rounds.sln' --configuration Release --no-build --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
    $first = & $dotnet run --project 'src/Rounds.Harness/Rounds.Harness.csproj' --configuration Release --no-build --no-restore -- smoke --seed 20260814 --ticks 600
    $second = & $dotnet run --project 'src/Rounds.Harness/Rounds.Harness.csproj' --configuration Release --no-build --no-restore -- smoke --seed 20260814 --ticks 600
    if ($LASTEXITCODE -ne 0 -or $first -ne $second) { throw 'Repeated smoke runs did not match.' }
    Write-Output "deterministic smoke passed: $first"
    $sdkRoot = Join-Path $repository '.tools/dotnet'
    $env:DOTNET_ROOT = $sdkRoot
    $env:PATH = "$sdkRoot;$env:PATH"
    $godot = Get-ChildItem (Join-Path $repository '.tools/godot-4.7.1') -Filter '*mono_win64_console.exe' -Recurse | Select-Object -First 1 -ExpandProperty FullName
    if (-not $godot) {
        & (Join-Path $repository 'tools/bootstrap.ps1') -IncludeGodot
        $godot = Get-ChildItem (Join-Path $repository '.tools/godot-4.7.1') -Filter '*mono_win64_console.exe' -Recurse | Select-Object -First 1 -ExpandProperty FullName
    }
    & $godot --headless --path 'game' --editor --quit
    if ($LASTEXITCODE -ne 0) { throw 'Godot editor import failed.' }
    & $godot --headless --path 'game' --quit-after 3
    if ($LASTEXITCODE -ne 0) { throw 'Godot runtime smoke failed.' }
    Write-Output 'Godot editor import and runtime smoke passed.'
} finally {
    Pop-Location
}
