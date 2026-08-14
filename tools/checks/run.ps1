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
    & 'tools/checks/check-replay-cli.ps1' -Repository $repository
    if ($LASTEXITCODE -ne 0) { throw 'Replay CLI process checks failed.' }
    & $dotnet run --project 'src/Rounds.Harness/Rounds.Harness.csproj' --configuration Release --no-build --no-restore -- verify-replays --directory 'replays/golden'
    if ($LASTEXITCODE -ne 0) { throw 'Golden replay verification failed.' }
    $goldenBase = $env:ROUNDS_GOLDEN_BASE
    $goldenHead = if ($env:ROUNDS_GOLDEN_HEAD) { $env:ROUNDS_GOLDEN_HEAD } else { 'HEAD' }
    if (-not $goldenBase) {
        $parentLines = @(& git rev-list --parents -n 1 $goldenHead)
        $parents = $parentLines[0].Split(' ', [StringSplitOptions]::RemoveEmptyEntries)
        if ($parents.Count -eq 1) { $goldenBase = 'ROOT' }
        elseif ($parents.Count -eq 2) { $goldenBase = $parents[1] }
        else { throw 'Merge commits require explicit ROUNDS_GOLDEN_BASE and ROUNDS_GOLDEN_HEAD for the local gate.' }
    }
    & 'tools/checks/check-golden-history.ps1' -Base $goldenBase -Head $goldenHead
    if ($LASTEXITCODE -ne 0) { throw 'Golden replay history check failed.' }
    $first = & $dotnet run --project 'src/Rounds.Harness/Rounds.Harness.csproj' --configuration Release --no-build --no-restore -- smoke --seed 20260814 --ticks 600
    $second = & $dotnet run --project 'src/Rounds.Harness/Rounds.Harness.csproj' --configuration Release --no-build --no-restore -- smoke --seed 20260814 --ticks 600
    if ($LASTEXITCODE -ne 0 -or $first -ne $second) { throw 'Repeated smoke runs did not match.' }
    Write-Output "deterministic smoke passed: $first"
    $matchFirst = & $dotnet run --project 'src/Rounds.Harness/Rounds.Harness.csproj' --configuration Release --no-build --no-restore -- match-smoke --seed 20260814
    $matchSecond = & $dotnet run --project 'src/Rounds.Harness/Rounds.Harness.csproj' --configuration Release --no-build --no-restore -- match-smoke --seed 20260814
    if ($LASTEXITCODE -ne 0 -or $matchFirst -ne $matchSecond -or $matchFirst -notmatch ' winner=0 score=5-0 ') {
        throw 'Repeated match smoke runs did not terminate identically at 5-0.'
    }
    Write-Output "deterministic match smoke passed: $matchFirst"
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
    $replayPath = Join-Path $repository 'replays/golden/base-combat-006-seed-1.rounds-replay.json'
    $earlyOutput = @(& $godot --headless --path 'game' --fixed-fps 60 --quit-after 10 -- --replay $replayPath 2>&1)
    $earlyExit = $LASTEXITCODE
    if ($earlyExit -eq 0 -or $earlyOutput | Where-Object { $_ -match '^REPLAY_COMPLETE ' }) {
        throw 'Godot accepted replay termination before consuming every input.'
    }
    $completeOutput = @(& $godot --headless --path 'game' --fixed-fps 60 --quit-after 600 -- --replay $replayPath 2>&1)
    $completeExit = $LASTEXITCODE
    $completion = 'REPLAY_COMPLETE id=base-combat-006-seed-1 ticks=600 hash=b91f86b6f1dc6b10 frames=600'
    if ($completeExit -ne 0 -or @($completeOutput | Where-Object { $_ -ceq $completion }).Count -ne 1) {
        throw 'Godot did not complete the full golden replay with its exact marker.'
    }
    Write-Output 'Godot editor, runtime, interrupted-replay, and complete-replay checks passed.'
} finally {
    Pop-Location
}
