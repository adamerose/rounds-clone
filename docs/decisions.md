# Decisions

Append-only.
One entry per decision that a person would want to know about but doesn't need to approve first: a judgment call made under ambiguity, a deviation from `docs/architecture.md`, a check that had to be amended, a test that was found wrong, a research conflict resolved by choosing a side.

Routine progress belongs in commit messages, not here.

Format:

```
## YYYY-MM-DD — short title
What was decided, what the alternatives were, and why this one. Link the commit or file.
```

---

## 2026-08-13 — Founding architecture settled

Godot 4 + C#, with all game rules in a pure `Rounds.Sim` library that has no Godot references.
Own physics, own math, own RNG, fixed 60 Hz tick, teams from day one.

Alternatives considered and rejected: Unity (fights automation — editor coupling, GUID churn, poor headless story), Bevy (fights the agents — breaking API changes roughly every three months against stale training data), engine-native gameplay in Godot (its scheduler and physics are not deterministic, which would cost the replay regression net), GDScript (untyped, and too slow for self-play volume).

The decision that carries the most weight is the module boundary.
Headless self-play, nightly replay video, and deterministic regression testing are all cheap on one side of it and engineering projects on the other.

See `docs/architecture.md`.

## 2026-08-13 — No human review of the research artifact

The research in `spec/` will not be spot-checked by a person.
The consequence is accepted knowingly: the project's fidelity ceiling is the research artifact's fidelity, and a confidently wrong value will be implemented faithfully and never questioned.

Two things partly compensate.
Every fact carries its source and a confidence rating, so disagreement between sources becomes visible rather than resolved silently.
And `spec/measurements.json` holds dimensionless quantities measured from gameplay footage, which the harness reproduces from the simulation — a fidelity signal that comes from the game itself rather than from an agent's description of it.

## 2026-08-13 — Prose is one sentence per line

All Markdown in this repository breaks lines at sentence boundaries rather than wrapping at a column width.

Renderers ignore single newlines, so nothing looks different.
The reason is that these files are edited by agents for weeks: hard column wrapping means every edit has to reproduce the wrapping exactly, which drifts over hundreds of edits, and changing one word reflows a whole paragraph into a large diff.
One sentence per line makes a diff show the sentence that actually changed.

## 2026-08-14 — Preserve the design seed as the Git baseline

The workspace arrived without Git metadata even though `GOAL.md` makes `git init` the first bootstrap action.
The existing five files were committed unchanged on `main` as `b9073b6a9c110b5fbca5e242d49bd03a8cecef12` before any implementation work.
This root commit is the only unavoidable pre-worktree change: Git cannot create a detached task worktree until a repository and base commit exist.

## 2026-08-14 — The bootstrap ticket is human-admitted

Ticket 001 starts at `ready` because Adam explicitly directed the complete build, named the repository the sole autonomous workspace, and the binding `GOAL.md` says bootstrap is the earliest dependency.
The ticket narrows that admitted work without adding a product choice, so a separate admission round would add no information.

## 2026-08-14 — Pin the first supported desktop toolchain

Bootstrap pins Godot 4.7.1 .NET, the current stable engine release, and .NET SDK 8.0.423, the current .NET 8 servicing SDK.
The target framework remains `net8.0` because Godot supports it and the founding architecture chose it; newer SDKs installed on a contributor machine must not silently change compilation.
Project-local bootstrap scripts provision missing tools under `.tools/` so a clean machine does not depend on global editor state.
The release pins were checked against the official [Godot 4.7.1 archive](https://godotengine.org/download/archive/4.7.1-stable/) and [.NET 8 download page](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) on 2026-08-14.

## 2026-08-14 — Use an original minimalist identity for the clone

The implementation will match the original game's readable arena silhouette, procedural motion, bright player colors, and chunky physical feedback without copying its logo, card art, audio, or exact UI text.
The visible working title is `RICOCHET`, which communicates the core combat loop while keeping the shipped presentation distinct from Landfall's assets.

## 2026-08-14 — Use the playbook's canonical postmortem ledger path

The founding `GOAL.md` named `docs/postmortems.md`, while the active Ivy playbook requires `docs/design-docs/postmortems.md`.
The goal and architecture now point to the playbook path so failures have one append-only home instead of two diverging copies.

## 2026-08-14 — Keep team ownership but target vanilla 1v1

The official Steam listing describes vanilla Rounds as 1v1, contradicting the founding architecture's claim that it ships 2v2.
The first playable scope is therefore exactly two opposing players.
`TeamId` remains because it makes ownership and target filtering explicit without adding a four-player mode or changing the match scope.

## 2026-08-14 — Show five original draft choices

Early concept work used three cards and accidentally reused the original card name `Burst`.
Research shows vanilla Rounds presents five upgrades, so the accepted draft concept now shows five choices with wholly original working names and illustrations.
This changes the presentation spec before implementation rather than teaching the UI the wrong choice count.

## 2026-08-14 — Target public build 21020021 and preserve uncertainty

The fidelity target is the current public Windows build `21020021`, identified in-game as `v1.1.2.a75ee335a`.
The Steam app manifest, live menu, and SteamDB agree on that identity.
Public 2021 and 2022 match recordings remain usable for base-mechanics measurement because later official updates describe platform, rendering, options, cross-play, and isolated scaling fixes rather than a complete base-movement retune.
Where current runtime observation is unavailable, footage-derived values remain estimates with explicit confidence and tolerance rather than becoming exact constants.

The media service rejected complete 60 fps extraction, so measurements use 29.97 or 30 fps previews with approximately two-tick temporal resolution and deliberately broad timing tolerances.
No original media or extracted game data is committed; the clean-room boundary is public behavior, public metadata, and derived measurements only.

The specification gate uses a dependency-free JSON Schema subset validator covering every keyword present in the committed schemas.
It fails on unsupported future schema keywords so an unimplemented vocabulary cannot silently weaken provenance enforcement.
This avoids introducing a validator package with a commercial-use maintenance EULA into the eventual distributable while retaining a mechanically tested schema contract.

## 2026-08-14 — Accept explicit single-source limits instead of contaminated independence

Movement, jumping, projectile speed, projectile radius, and out-of-bounds timing each have only one action- and modifier-controlled observation in the selected recordings.
Shot-contaminated or card-contaminated comparisons remain visible in the measurement log but do not count toward coverage.
Body scale, recoil, block timing, and camera framing retain two independent accepted sources.
This is a more useful implementation contract than inflating source counts: every estimate remains tunable, and the harness must later compare clone-generated controlled captures against the recorded bands.

Fresh reviewer session `codex:019fff26-f793-7293-87fe-8a816060e432` approved exact candidate `6681545522380445e270edc6c2888fb0a3e81d5c` with no findings.
The candidate was fast-forwarded to `main` unchanged.
