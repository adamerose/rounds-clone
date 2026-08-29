# ROUNDS

This repository is an unofficial, clean-room, in-progress clone of ROUNDS.
It contains no copied source code or extracted proprietary logo, art, audio, or other asset bytes.
Implemented behavior and short names target the public game; missing or unverified behavior stays absent or visibly unfinished instead of being replaced with original content.

The current Godot build is development scaffolding, not a completed faithful match.
It plays both opening drafts and one full round, then stops before the losing player can select a second card because duplicate and cross-card composition have not yet been verified directly against ROUNDS.
Its 16-card draft pool uses the exact sourced names Bouncy, Careful Planning, Combine, Defender, Fast Forward, Fastball, Glass Cannon, Huge, Leech, Mayhem, Quick Reload, Quick Shot, Spray, Steady Shot, Tank, and Wind Up.

Known gaps are explicitly owned by tickets 016–025: base movement and combat feel (016), projectile presentation (017), all 70 arenas and their behavior (018), verification and gating of the current 16 cards (019), presentation (020), controller and menu input (021), match replay and internal headless self-play (022), settings/persistence/shipping (023), nightly reel evidence (024), and the remaining 51 cataloged cards (025).

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

The live shell starts with player one's five-card opening choice, then player two's, and plays through the first full round.
Left/right movement wraps the active five-card selection and jump confirms after both controls have been released once.
At the first loser draft it displays an incomplete-fidelity boundary and accepts no further match input, so a second card or later simulation step is unreachable in the shipped shell.
The score, half points, sourced card names, effect summaries, opening card stacks, arena ID, and live bullet bounce budgets remain visible around the same pure-simulation combat.
Player one uses A/D, Space, mouse aim, Mouse1 fire, and Mouse2 block. Player two uses Left/Right, Up, I/J/K/L aim, O fire, and P block.
The protected `base-combat-v1` replay remains a standalone duel format.
Current movement, jumping, damage, fire, reload, recoil, block tuning, projectile speed/rendering, arena reconstruction, card stacking, menus, effects, audio, controller input, settings, persistence, match replay, self-play, packaging, and reel output are scaffolds or missing until their owning fidelity tickets close.

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
