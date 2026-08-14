# RICOCHET

RICOCHET is an original, deterministic reimplementation of the short-round platform-shooter structure popularized by Rounds.
It keeps simulation rules in a pure .NET library and uses Godot only for input, presentation, and menus.
No Landfall art, audio, text, or source code is included.

The repository is in active construction.
The bootstrap milestone provides a deterministic input/hash boundary, a headless harness, mechanical architecture checks, automated tests, and a loadable Godot shell.
The core research milestone pins public build `21020021` (`v1.1.2.a75ee335a`) and defines sourced match, controls, player, combat, camera, and footage-measurement specifications.

## Quick start on Windows

```powershell
./tools/bootstrap.ps1 -IncludeGodot
./tools/checks/run.ps1
```

The bootstrap script installs the pinned .NET SDK and Godot .NET editor under the ignored `.tools/` directory.
It does not change machine-wide SDK or editor installations.

Run the deterministic harness directly:

```powershell
./.tools/dotnet/dotnet.exe run --project src/Rounds.Harness -- smoke --seed 20260814 --ticks 600
```

Launch the current game shell:

```powershell
$sdkRoot = (Resolve-Path .tools/dotnet).Path
$env:DOTNET_ROOT = $sdkRoot
$env:PATH = "$sdkRoot;$env:PATH"
$godot = Get-ChildItem .tools/godot-4.7.1 -Filter '*mono_win64_console.exe' -Recurse | Select-Object -First 1 -ExpandProperty FullName
& $godot --path game
```

The current shell is an architectural smoke surface, not a playable match yet.
Gameplay implementation now has a mechanically validated core specification, while the complete card and arena catalogs remain separate research milestones.

## Project map

- `src/Rounds.Sim/` contains deterministic game state and the fixed-step boundary with no Godot references.
- `src/Rounds.Harness/` runs simulations, replays, measurements, self-play, and renders as those capabilities land.
- `src/Rounds.Sim.Tests/` protects simulation behavior.
- `game/` contains the Godot presentation shell.
- `spec/` holds the sourced fidelity target and becomes read-only after each research contract closes.
- `research/notes/` explains measurement methods, conflicts, and the clean-room boundary without committing source media.
- `tools/checks/` is the local and CI verification entry point.
- `docs/architecture.md` and `docs/design/` hold binding technical and product design.

See `GOAL.md` for the full completion bar.
See `research/notes/core-rules.md` for the current match sequence, measurement targets, and known uncertainty.
