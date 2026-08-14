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

## 2026-08-14 — Admit ticket 003 as bounded card research

Reviewer session `codex:019fff30-7cf1-75a3-aa80-02e6bc681833` admitted ticket 003 at risk 4 after ticket 002 closed.
The work is limited to a sourced current-build catalog, stacking semantics, schema enforcement, and implementation ordering; it does not implement card behavior or copy original presentation assets.
The contract has no unresolved human choice because conflicts remain explicit rather than being silently resolved, and every non-obvious numeric value needs official evidence, direct observation, or two corroborating public sources.

## 2026-08-14 — Bind 67 cards while retaining the clean-room count gap

The catalog uses 67 project IDs because the English table, the 2024 GameFAQs list, and the 2021 Korean guide include Quick Shot, while the Japanese index's 66 linked pages omit it and the official store supplies only a 65+ lower bound.
The clean-room runtime cannot force a complete pool listing in one session, so that direct current-build enumeration remains an explicit gap instead of a fabricated confirmation.

## 2026-08-14 — Separate displayed modifiers from stacking formulas

Confirmed card numbers may enter the research catalog while unmeasured duplicate-card composition remains explicitly provisional or unresolved.
This prevents later simulation work from treating a UI percentage as proof of additive or multiplicative implementation order.

## 2026-08-14 — Bind official card fixes over older guides

Patch 1.05 supplies changed numeric card values, patch 1.1.1 binds damage-based projectile scaling, and the November 2024 update binds corrected growth behavior for build 21020021.

## 2026-08-14 — Treat stacking and caps as separate sourced facts

The first card candidate reused displayed-value provenance for 62 additive, 15 count, and 67 uncapped claims that its sources did not establish.
The corrected catalog gives every effect separate stacking-and-cap provenance, retains only five explicitly sourced representative formulas, and marks every other duplicate-copy formula or unobserved cap unresolved.
Project evaluation phases remain implementation vocabulary rather than claims about the original engine's hidden order.

## 2026-08-14 — Preserve all official patch 1.05 constraints

The official 11 April 2021 note binds not only numeric card changes but also Abyssal Countdown's relative timing changes, Cold Bullets accumulation beyond two copies, and Shield Charge range independence from health.
Exact values remain unknown where the note publishes only a direction or behavioral constraint.
GameFAQs' older Parasite and Poison values remain attached as historical conflicts rather than corroborating the current build.

## 2026-08-14 — Bind only directly reported duplicate-card behavior

The Japanese card pages explicitly report no change from repeated Refresh copies and one additional Echo activation per copy.
Quick Reload's one-copy factor does not prove multiplication across copies, and community statements about Remote contradict one another, so both duplicate behaviors remain unresolved.
The catalog keeps a named unresolved multiplicative test case without asserting its `0.3^n` hypothesis as behavior.

## 2026-08-14 — Make known source omissions executable

GameFAQs omits Bouncy and Homing entirely, omits Chase's health and Taste of Blood's lifesteal modifiers, and preserves pre-1.05 Parasite and Poison values.
The catalog records those exclusions explicitly, and the repository gate now rejects reuse of an excluded source as metadata, behavior, numeric, stacking, or cap evidence.

## 2026-08-14 — Integrate the reviewed vanilla card catalog

Fresh reviewer session `codex:019fff67-9eba-7042-8e81-fc16c3885b45` approved exact candidate `02c339a89dfdbb3ecf276f886d82f064e5a4eda5` with no findings.
The review independently confirmed the 67-card reconciliation, the corrected Brawler and Pristine Perseverence percentage sources, every prior stacking and patch correction, 31 passing tests, deterministic hash `f250d549cfb52a8b`, and Godot editor and runtime smoke.
The candidate was fast-forwarded to `main` unchanged before this integration record was appended.

## 2026-08-14 — Admit ticket 004 as bounded arena research

Reviewer session `codex:019fff46-e63b-7911-8124-12c0d8fe0b12` admitted ticket 004 at risk 4 after ticket 002 closed.
The contract binds current-build arena enumeration, player-diameter geometry, clean-room presentation, representative behavior coverage, schema failures, and an implementation order without implementing physics or rendering.
No human choice remains because unavailable geometry and source conflicts must stay explicit rather than being invented.

## 2026-08-14 — Use 70 row-stable project arena IDs

The official store's “70+ maps” remains a lower bound, while the public community sheet provides an exact row-ordered index of 70 vanilla previews.
The catalog therefore binds `arena-001` through `arena-070` to sheet rows 2 through 71 without retaining proposed names or internal identifiers.
Random current-build matches do not provide exhaustive enumeration, so the difference between the sheet index and the active runtime pool remains explicit rather than being filled from extracted game data.

## 2026-08-14 — Treat preview geometry as a coarse implementation contract

The arena catalog converts preview silhouettes to five-pixel grid rectangles in player-diameter units and records a ±0.8-diameter coordinate tolerance plus ±20 percent global scale uncertainty.
This is enough to preserve layout topology, sightlines, supported spawn regions, hazard placement, and implementation order without shipping or tracing original visual assets.
Controlled current-build captures must retune scale, camera margins, and exact collisions before a map graduates from provisional geometry.

## 2026-08-14 — Do not infer dynamic behavior from still previews

Visible saws, breakable groups, moving groups, and physics structures become reusable behavior-module regions rather than invented timing or material constants.
Paths, rotation, health, fragments, constraints, masses, damping, contact response, and reset sequencing remain unknown until direct behavior evidence binds them.
The first playable map work should implement static topology before enabling each behavior family from separately measured evidence.

## 2026-08-14 — Supersede the first coarse map grid with anchored oriented geometry

The first catalog incorrectly associated shuffled workbook media filenames with sequential sheet rows and had no source-render acceptance oracle.
Arena identity now follows each embedded drawing object's worksheet-row anchor and relationship target, while its 640 by 360 mask is decomposed into oriented boxes in player-diameter units.
The first correction required every rounded catalog render to reach at least 0.95 full-resolution intersection over union with the row-bound source mask, but independent review found that requirement incompatible with the clean-room prohibition on tracing.
This supersedes the earlier five-pixel axis-grid decision; visible geometry remains provisional collision evidence rather than an assertion about hidden colliders.

## 2026-08-14 — Treat unobserved arena dynamics as candidates

Visible saw silhouettes are direct evidence for hazard regions, but a still does not prove movement, breakability, physics constraints, or timing.
The catalog therefore labels `arena-016`, `arena-026`, and `arena-030` as visual candidates for separately observed behavior instead of claiming those behaviors as confirmed facts.
Hazard silhouette boxes use a distinct non-static role so later collision work cannot accidentally treat a lethal saw as an ordinary platform.

## 2026-08-14 — Measure spawn safety in two dimensions

The eight-diameter spawn rule measures Euclidean center separation rather than horizontal distance because vanilla layouts include vertical arenas such as `arena-018`.
Each provisional spawn names an oriented support box, and the gate checks support in that box's local coordinates, camera containment, kill-bound clearance, and visible-saw clearance.

## 2026-08-14 — Position-lock accepted arena renders

Total foreground pixels do not identify where geometry appears, so a count-only rerender gate can miss a moved box.
Each accepted arena now records a digest of the entire positioned 640 by 360 render mask, and the checker rejects count or digest drift before trusting the source-overlap evidence produced by the reproducible generator.

## 2026-08-14 — Reconcile removed release-era arenas separately

An independent public mod index lists six arenas from the 7 April release-era build that were removed later.
None of those entries appears in the community workbook's 70 internal-name rows, so the removed subset corroborates the workbook boundary without being counted as current-build geometry.
The exact active pool remains a runtime gap because randomized current matches cannot exhaustively enumerate it.

## 2026-08-14 — Make every declared spawn point source-supported

Spawn-region width now derives from the named oriented support surface, and the checker validates all four rectangle corners in support-local coordinates.
This replaces center-only validation, which allowed a plausible center while portions of 40 declared regions hung beyond narrow platforms.

## 2026-08-14 — Use topology-scale acceptance instead of pixel tracing

The arena generator now represents every eight-connected source component with at least one oriented box, caps each arena at 96 boxes, and requires at least 0.75 intersection over union on an 80-by-45 occupancy grid.
The source-mask digest anchors the ignored measurement input, while a full positioned-render digest protects committed geometry from unnoticed drift.
Full-resolution source overlap is deliberately not optimized or accepted because the prior 0.95 oracle produced 7,557 silhouette boxes and crossed the ticket's boundary from measuring play patterns into tracing source art.
The resulting 1,790-box catalog preserves topology and broad proportions with coarse scores from 0.787459 to 1.0 while leaving exact collision and scale provisional.

## 2026-08-14 — Bind arena 026 to measured mirrored motion

Workbook row 27 and the unobscured `00:00:34.000` frame in `footage-wcg` match at 0.897384 coarse occupancy intersection over union and 0.972384 source coverage, while the following 18 seconds directly show two mirrored square platforms traversing a U-shaped path and reversing.
The catalog therefore promotes `arena-026` from a visual candidate to measured motion, separates its two squares from static silhouette geometry, records ten paired position samples in player diameters and ticks, and contains 1,792 boxes after adding the two mover-owned primitives.
The observed endpoint-to-reversal interval is about 840 ticks with ±120-tick timing tolerance, while a full period, dwell behavior, and exact interpolation remain explicitly unobserved.

## 2026-08-14 — Use one oriented-box vocabulary for static and moving level geometry

The bootstrap architecture's static-AABB phrase contradicted the binding map design and could not represent visible slopes without a second collision vocabulary.
Static and behavior-owned oriented boxes preserve the deterministic custom-physics boundary while allowing the same local-coordinate sweep and contact code to support fixed, sloped, and moving surfaces.
Hazard and dynamic visual roles keep unimplemented behavior from silently entering the static collision set.

## 2026-08-14 — Integrate the reviewed vanilla arena catalog

Fresh reviewer session `codex:019fffeb-75c7-7c30-82d5-ac46c0ec51a3` approved exact candidate `67369534652c9aac6e2fb278e6afdc09eab213a9` with no actionable findings.
The review independently reproduced all ten arena-026 mover samples, identified workbook row 27 as the clear footage match, regenerated the 70-map catalog byte-for-byte, and passed the zero-warning 46-test gate plus Godot smoke.
The candidate was fast-forwarded to `main` unchanged before this integration record was appended.
