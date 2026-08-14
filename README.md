# RICOCHET

RICOCHET is an original, deterministic reimplementation of the short-round platform-shooter structure popularized by Rounds.
It keeps simulation rules in a pure .NET library and uses Godot only for input, presentation, and menus.
No Landfall art, audio, text, or source code is included.

The current build provides a complete deterministic local match: two opening picks, short duels, half-point scoring, loser drafts, persistent stat builds, arena rotation, and a first-to-five winner.
The first 12 passive cards and 62 static arenas come from the pinned public-build research catalog; behavior cards, animated hazards, bots, controllers, audio, and production presentation remain in progress.

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

Run the bounded full-match smoke:

```powershell
./.tools/dotnet/dotnet.exe run --project src/Rounds.Harness -- match-smoke --seed 20260814
```

Launch the current game shell:

```powershell
$sdkRoot = (Resolve-Path .tools/dotnet).Path
$env:DOTNET_ROOT = $sdkRoot
$env:PATH = "$sdkRoot;$env:PATH"
$godot = Get-ChildItem .tools/godot-4.7.1 -Filter '*mono_win64_console.exe' -Recurse | Select-Object -First 1 -ExpandProperty FullName
& $godot --path game
```

The live shell starts with player one's five-card opening choice, then player two's, and plays through the complete first-to-five match.
Left/right movement wraps the active five-card selection and jump confirms after both controls have been released once.
The full score, half points, card stacks, arena ID, loser drafts, and final winner remain visible around the same pure-simulation combat.
Player one uses A/D, Space, mouse aim, Mouse1 fire, and Mouse2 block. Player two uses Left/Right, Up, I/J/K/L aim, O fire, and P block.
The protected `base-combat-v1` replay remains a standalone duel format; match replay, bots, controller defaults, sound, dynamic map behavior, and production presentation remain later milestones.

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
