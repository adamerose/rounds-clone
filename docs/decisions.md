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

## 2026-08-14 — Do not add coyote time to a stored-air-jump system

The binding movement rule gives each player one stored jump that remains usable after leaving the ground and refills on landing.
A conventional coyote window therefore changes nothing while the jump remains stored, or incorrectly creates a second jump if recent ground contact grants eligibility after that stored jump was consumed.
Movement keeps the sourced provisional four-tick landing buffer and tests the actual state boundaries instead of adding redundant coyote machinery.

## 2026-08-14 — Admit the static movement slice

Reviewer session `codex:019ffff9-3be5-7da2-8811-5df376ffc9a4` admitted ticket 005 at risk 4 after tickets 002 and 004 closed.
The contract binds one static arena, one deterministic circle-versus-oriented-box collision path, researched movement constants, an explicit stored-jump state machine, live shell rendering, and negative boundaries without mixing in combat or lifecycle work.
No human choice remains because unmeasured feel constants are named provisional values with direct behavioral tests rather than hidden claims about the original game.

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

## 2026-08-14 — Integrate deterministic movement and static collision

Fresh reviewer `codex:019ffff6-6034-76b1-96a2-b080ac183346` approved exact candidate `1fa05e72962f771c5d5ff7fbe0e3266233f3c963` with no actionable findings after two earlier reviewers exposed three corrected public-boundary defects.
The approved slice loads immutable embedded arena data through a stream-testable API, moves two players against source-ordered static oriented boxes, preserves one stored air jump, buffers landing input, hashes complete movement state, and renders the same live world in Godot.
The complete gate passed 42 simulation and 37 checker tests, repeated deterministic hash `28bca5e37a7a3255`, a zero-warning release build, byte-identical spec trees, Godot editor/runtime smoke, and live GPU capture.
The candidate was fast-forwarded to `main` unchanged before this integration record was appended.

## 2026-08-14 — Admit the base combat duel slice

Reviewer `codex:019ffff6-6034-76b1-96a2-b080ac183346` admitted ticket 006 at risk 4 after tickets 002 and 005 closed and two earlier admission reviews corrected seven combat/lifecycle ambiguities plus one provenance error.
The contract binds a single complete base duel with fixed tick phases, measured gun/block/ring-out facts, explicit provisional behavior, deterministic swept contacts and impulses, bilateral spawn/result locks, exact reset preservation, and native evidence for both local control paths.
No human choice remains because every unmeasured value and fallback is named, while scoring, drafts, cards, arena cadence, bots, controller defaults, audio, camera, and production assets remain separate outcomes.

## 2026-08-14 — Build base combat as one ordered deterministic tick

The simplest new design would validate all inputs, advance timers, apply aim/block/fire and recoil, move players, sweep bullets, then resolve deaths through one explicit phase machine.
That matches the existing pure `Rounds.Sim` boundary, so combat extends the current world rather than adding event buses, engine bodies, component frameworks, or asynchronous effects before cards need them.
Stable player and bullet order, geometry/block/body tie priority, a four-contact bullet bound, and a visible overflow counter make every future-affecting choice replayable.

## 2026-08-14 — Model block launch as impulses from players and static contacts

The confirmed block push and wall-assisted launch do not require a simulated expanding rigid body.
Activation applies equal-and-opposite constant impulses between nearby living players, then queries the existing circle-versus-oriented-box overlap path and applies one outward impulse per source-ordered static contact.
This produces separation, floor jumps, and wall launches with one collision vocabulary while leaving unmeasured radius and magnitude explicit in combat tuning.

## 2026-08-14 — Separate death, observable result, display, and spawn lock

Health depletion or bottom-bound crossing marks death immediately and freezes active combat.
A six-tick resolving phase matches the measured ring-out-to-result delay, a 90-tick result phase presents the outcome, and a 60-tick bilateral spawn phase resets both players before simultaneous unlock.
World tick, RNG, next bullet ID, duel count, and overflow metrics continue across resets; per-duel players, bullets, health, ammo, block, aim, and timers reset exactly.

## 2026-08-14 — Keep gameplay data embedded behind stream-loadable simulation APIs

The simplest new design would give the pure simulation immutable arena and tuning values directly, with loading owned at an outer application boundary.
The existing repository already treats committed JSON as the binding cross-tool artifact, so `Rounds.Sim` embeds those exact files for its supported default while exposing stream-based catalog loading for tests and future hosts.
This avoids repository-relative runtime paths, keeps Godot types out of the simulation, and prevents the shell from duplicating map geometry or movement constants.

## 2026-08-14 — Use rounded-box sweep as the single static collision vocabulary

Circle movement against an oriented box is equivalent to sweeping a point against the box expanded by the player radius, with quarter-circle corners.
The simulation tests face and corner candidates in box-local coordinates, resolves initial overlap explicitly, selects exact-time ties by source order, and removes inward velocity for four deterministic slide iterations.
This one path handles floors, walls, slopes, corners, thin geometry, and later behavior-owned oriented boxes without parallel AABB and OBB solvers.

## 2026-08-14 — Name provisional movement feel without claiming source fidelity

The research binds run speed, acceleration, air control, gravity, jump speed, jump capacity, friction retention, and a four-tick jump buffer, but not jump release, contact threshold, ground probe, or collision skin.
The first playable slice uses a `0.5` jump-release multiplier, `0.65` ground-normal threshold, `0.04`-diameter ground probe, `0.000001`-diameter collision skin, and four slide iterations.
Boundary tests make their observable effect explicit so later controlled comparison can retune them without mistaking provisional feel for measured vanilla behavior.

## 2026-08-14 — Put replay encoding outside the simulation

The simplest replay is the seed, arena ID, exact input stream, and periodic hashes needed to reconstruct a world through `Sim.Step`.
A small `Rounds.Replay` library owns canonical JSON, validation, recording, and playback so `Rounds.Sim` stays unaware of files while the harness and Godot share one implementation.
Aim uses raw IEEE-754 bits rather than decimal text because the existing checksum distinguishes signed zero and must reproduce every accepted finite input exactly.

## 2026-08-14 — Protect behavior with checkpoints, not state snapshots

Snapshots would turn private world layout into a file format and could hide a broken transition by restoring its result.
Version 1 stores a hash after every 60 ticks and at the final tick, then stops at the first mismatch with exact diagnostics.
This keeps the replay small, localizes drift to at most one second, and still proves the complete input stream regenerated the state.

## 2026-08-14 — Render the replay through the playable Godot shell

Godot 4.7.1's pinned movie writer accepts an AVI path, fixed FPS, and a frame limit, so the nightly reel can use the same shell people play instead of a second visual implementation.
Replay mode replaces only live input acquisition, checks the same hashes as the harness, and leaves simulation and drawing paths unchanged.
AVI output remains ignored and hardware-dependent; the replay hash, successful frame count, RIFF container, and inspected representative frames are the durable evidence.

## 2026-08-14 — Anchor replay history and compare exposed endpoints

A per-commit parent diff is necessary to require a golden change and its explanation together, but it is not sufficient when branches diverge: a file added on an old fork can replace a same-named golden that appeared later on the target branch.
CI therefore trusts the repository's unique inception commit `b9073b6a9c110b5fbca5e242d49bd03a8cecef12`, rejects candidates outside that provenance, and checks every commit in the selected range.
For a diverged pull request it separately compares the established target with a conflict-free prospective three-way merge tree, not the raw feature head, so target-only additions survive while actual corpus replacements remain explicit.
Non-fast-forward branch updates and in-place tag updates are rejected because they discard the prior exposed endpoint; a delete-and-recreate tag is treated as a new fully verified tag because stateless event data cannot distinguish it from first creation.
Deleted golden names remain permanently reserved because version 1 has no unambiguous same-commit restore transition.

## 2026-08-14 — Admit deterministic replay and reel ticket

Fresh reviewer `codex:01a00096-742f-71f1-b5fc-80f5772e2046` admitted exact candidate `a1621ab87c9e9653ef8f875854e662e815dd7cb7` at risk 4 after dependency 006 closed and eight earlier exact reviews exposed and corrected the format, history, event, endpoint, merge, and rendered-frame edge cases.
The final contract has no human choice: it binds canonical bytes, stream playback, a protected golden corpus, explicit Git provenance, replay-only Godot input, exact AVI frame evidence, and pinned seven-day nightly output while leaving match scoring, cards, bots, and presentation outside the slice.

The empty-ledger byte grammar was re-admitted by reviewer `codex:01a000ac-dd36-7902-81e2-5b2c75826c5d` at exact candidate `228e55a5dfb32ea10be0568ca7d672ba311cfda5` after implementation proved a terminal blank line conflicts with the repository whitespace gate.
An empty ledger now ends after the heading LF; the blank separator arrives as part of the first append-only entry.

## 2026-08-14 — Integrate deterministic replays and the validated reel

Fresh reviewer `codex:01a00122-91f3-7250-b63c-55c236365989` approved exact candidate `11dc0a55d2994c1206c168fdbbe7e44e26947656` with no findings after five earlier implementation reviews exposed clean-runner, validation-order, general-renderer, legacy-history, and intermediate-replay bypasses.
The integrated boundary records canonical two-player input streams with periodic hashes, replays them through both the harness and playable Godot shell, protects every historical golden revision through public playback, and publishes a pinned nightly 600-frame reel.
The approved gate passed 167 simulation/replay/history tests, 37 repository-checker tests, the real pre-ticket integration range, deterministic smoke, interrupted and complete Godot playback, a one-frame generic render, and the six-state canonical render while leaving match scoring, drafts, cards, bots, controller defaults, and production presentation for later slices.

## 2026-08-14 — Admit the deterministic match and stat-card slice

Fresh reviewer `codex:01a0013c-933e-7563-ab82-361e2fc6cb2b` admitted exact ticket candidate `8b8952a45315c18e6c054b4bc85fad97eaa517c7` at risk 4 after an earlier review removed an unreachable capped-loser branch and made profile hashing, the opening reset, and arena RNG exact.
The admitted slice puts five-point scoring, sequential opening and loser drafts, 12 provisional stat-only card folds, and 62 static arena choices in one deterministic `Match` above the existing duel `World`.
Vanilla duel hashes and replay files remain exact; behavior cards, rarity weighting, non-static map behavior, match replays, bots, controllers, audio, camera work, and production presentation remain separate work.

## 2026-08-14 — Keep project windows on the small fourth monitor

Every visible window launched for this project belongs on monitor 4, the 1920x1080 display at zero-based screen index 3, so development does not interrupt work on the three main monitors.
Godot selects that screen before showing command-line or exported runs and repeats the selection when the scene starts to cover editor launches.
Agent-launched GUI tools must choose that monitor before showing; tools without startup placement support must launch hidden or minimized, move there, and only then become visible.

## 2026-08-14 — Integrate deterministic matches and stat cards

Fresh reviewer `codex:01a00174-06cd-7250-9346-7b3c17b490c0` approved exact candidate `16f41c8e94e143d4e30a8a8dd4a2ace68b30b2c0` after the first review exposed and the correction fixed a cross-tier card-identity bypass and missing deterministic evidence.
The integrated game now plays sequential opening and comeback drafts, folds 12 stat-only cards into player-specific combat, scores first to five, changes among 62 static arenas, preserves the approved duel replay contract, and shows the complete local match through Godot.
Every project window is also bound to the small fourth monitor through project guidance, Godot pre-show settings, and a runtime fallback.

## 2026-08-28 — Admit orphaned project progress recovery inventory

Fresh reviewer `codex:01a0494c-7878-7232-9169-27500bc90c45` admitted ticket 013 at risk 3 after three earlier fresh reviews exposed and the contract corrected omissions in dirty registered bytes, digest framing, ticket-version identity, surviving Git provenance, phase-scoped mutation, self-reference, project ledgers, and media inclusion.
The admitted inventory freezes the eight pre-existing orphan artifact paths, treats lifecycle labels as claims, correlates commits, objects, operational indexes, and bytes without touching credentials, and leaves every recovery or deletion action to a later independently reviewed ticket.
Authoritative `main` remains the only project state until that later recovery work closes through the normal pipeline.

## 2026-08-28 — Recover clean registered history before orphan snapshots

Ticket 009 is the safest next slice because it is the only complete frozen artifact with a clean working tree and an exact 13-commit chain directly above `c24ed0a88c2bff843e788e1957502d9b86bc3d25`, so a fresh ticket can review a named range without reconstructing provenance.
Later orphan outcomes remain evidence-only because matching operational indexes and review text preserve useful evidence but cannot replace missing commit, tree, parent, or review-range identity; the bounded evidence and remaining uncertainty are in the [recovery inventory](recovery/orphaned-progress-2026-08-28.md).
Tickets 010 and 012 are superseded by 026 and 025, ticket 024 remains `blocked-external-action` because credential reset requires authenticated provider access, and no frozen artifact is discardable because none is proven to lack unique recoverable bytes or records.

## 2026-08-28 — Recover core ricochet cards without orphan native-safety machinery

The clean orphan ticket-009 chain preserves a deterministic four-card ricochet implementation, but its final head was never approved and combines that core result with later native focus, cursor, evidence-driver, and release-wording work whose preflight remained blocked.
Ticket 014 therefore reconstructs the nine selected simulation/test snapshots at `95f15a5a9e22cf217d097c78147e827b349d5ff0`, only the two card-owned `game/Main.cs` hunks present at `3072bface31bfd5457c2014537fa387e773ffac4`, and narrowly reconciled current documentation onto authoritative `main`.
Fresh headless verification and independent review decide whether that reconstructed result is acceptable; orphan lifecycle text, native-safety machinery, recovery evidence, and every frozen artifact remain evidence rather than authority.
This decision supersedes only the 2026-08-14 identity decision's blanket prohibition on exact UI text: exact sourced gameplay identifiers and short names are allowed when fidelity and unambiguous validation require them.
The clean-room boundary still forbids copied source code, the original logo, card art or other extracted art, audio, and longer expressive or flavor text; ticket 014 updates the README and architecture policy sentences to state that narrower rule.
Fresh reviewer `codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a049d9-8546-74f0-8f4d-4cd29e32dc1f` admitted the corrected ticket at risk 4 with no findings after independently verifying the selected snapshots, exact two-hunk presentation boundary, native-work separation, recorded hashes, clean-room policy, immutable artifacts, and single-ticket delivery workaround.

## 2026-08-29 — Implement faithful ROUNDS subsets instead of original substitutes

The user's binding direction is that partial progress may omit ROUNDS content but must never intentionally diverge from it.
This supersedes the 2026-08-14 decisions to ship the `RICOCHET` title and five wholly original draft choices, and retires the three generated concept screens as visual acceptance references while preserving their bytes as historical evidence.
The active product title and exact sourced short card names are `ROUNDS` fidelity requirements; the clean-room boundary still forbids copied source code and extracted proprietary logo, art, audio, or other asset bytes.

Deterministic tests remain required but cannot establish fidelity without a comparison signal against the installed public target or equally direct evidence.
The current simulation, maps, tuning, composition rules, and presentation remain explicitly provisional under tickets 016–025.
Because second-card composition is not verified, the shipped Godot shell now stops after the first full round before any loser-draft selection or later simulation step, while the pure `Match` scaffold remains available for internal deterministic testing.

As a narrow metadata-only exception to the ordinary `spec/` freeze, ticket 015 changes only the human-readable `title` field in the five existing schema files from the superseded product title to `ROUNDS`; their stable `$id` values and all validation and research bytes remain unchanged.

## 2026-08-29 — Admit the frame-span speed correction and non-disruptive evidence boundary

Fresh reviewer `codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04bf9-57f9-7fa3-b2a8-63710fa3769e` admitted tickets 016, 017, 030, and 031 together at risk 4 or lower after independently confirming the retained source hash, 30-fps stream, and two fixed projectile trajectories.
Ticket 030 corrects the demonstrated six-frame denominator error without claiming complete current-build calibration; ticket 016 retains all other base-feel work, ticket 017 owns presentation without changing simulation, and ticket 031 owns input-isolated installed-build capture under objective CPU, GPU, memory, cadence, latency, placement, and cleanup limits.
Binding combat specs are embedded runtime data, so the admitted green sequence lands measurement/checker evidence first, behavior-neutral source preparation separately when needed, and the behavior-changing binding spec together with its replay/golden/intentional-break consequences, never mixing `spec/` and `src/` in one commit.

## 2026-09-03 — Archive the Godot prototype and restart from the supplied recordings in Bevy

Adam decided that the finished product must reproduce the mechanics, physics, presentation, effects, match flow, and multiplayer experience visible throughout both supplied ten-minute ROUNDS recordings, not merely satisfy the existing internally derived specification.
The committed Godot and C# prototype remains recoverable at `archive/godot-csharp-prototype-2026-09-03`, while the active implementation restarts in Bevy with multiplayer, headless server execution, programmatic play, and programmatic capture in its first product slice.
The supplied media bytes stay ignored and unchanged, but a tracked manifest binds their paths, sizes, and SHA-256 values and the rewrite must account for every distinct gameplay and presentation interval in both recordings.
Tests protect observed behavior, stable public boundaries, reproduced defects, and actual release threats; their existence alone does not make them permanent, and support or test machinery larger than the product slice it protects triggers architectural reconsideration rather than automatic hardening.
Fresh reviewer `codex:01a06920-7449-74d0-9b09-57855a012572/01a069e1-2a76-7c51-b7f4-5750932d7c06` admitted ticket 038 at risk 4 with no findings after the contract retained maintenance tickets 036 and 037, durably identified all eleven supplied media files, and required complete timestamp-indexed coverage of both recordings.

## 2026-09-04 — Keep the first Bevy slice small and executable

The first Rust slice uses Bevy ECS as the authoritative fixed-tick state container while keeping presentation, UDP transport, process entry points, and automation in separate crates.
One bounded scripted-input datagram per client is enough to exercise a real headless server and two real client processes without inventing a production reliability protocol.
The deterministic renderer writes a PNG in software from the authoritative snapshot, so capture needs no editor, window, GPU, or second simulation.
The slice deliberately does not select a third-party physics library or claim prediction, interpolation, rollback, lag compensation, matchmaking, authentication, or Steam transport.
Those choices keep the executable product path larger than its test and support machinery while preserving the boundaries later footage slices need.

## 2026-09-04 — Admit the first footage-derived teal duel

Ticket 039 reconstructs the card-modified teal-arena duel at 00:22.50–00:35.50 of recording `1460e670…15f9` as one end-to-end Bevy slice: Rapier contacts and impulses behind owned identifiers, a shipped 2D renderer, live sequenced client inputs and progressive authority snapshots, headless capture, and source-versus-clone visual review.
The named `teal-duel-replay` profile reproduces the observed interval without pretending its parameters are card-neutral base constants, and the current server-authority model deliberately excludes Rapier's known-broken Bevy `enhanced-determinism` feature and makes no cross-platform lockstep claim.
Fresh reviewer `codex:01a06920-7449-74d0-9b09-57855a012572/01a06a55-572e-7bd2-be97-605bb16bb6bb` admitted the corrected contract at risk 4 after the first review exposed the dependency contradiction, card-confounded oracle, batch-network loophole, and stale-record omission.

## 2026-09-04 — Replace the Bevy foundation placeholders at their existing boundaries

The teal duel keeps the six-crate process shape but replaces the foundation's integer movement, software pixels, and whole-script UDP request with the production-facing choices the next footage slices need.
`rounds-sim` now owns stable Bevy ECS identities around a private Rapier 2D service; `rounds-presentation` renders the same snapshot scene through Bevy in visible and offscreen modes; and `rounds-network` advances one authority from per-tick sequenced inputs while returning each progressive snapshot.
Only project snapshots and inputs cross those boundaries, so Rapier handles and Bevy entity IDs remain local implementation details.
Rapier's `enhanced-determinism` feature stays disabled because it conflicts with the pinned Bevy dependency graph, and this server-authority design requires repeatability only for the same locked build and platform.

## 2026-09-04 — Correct the teal duel against direct source frames

Direct frame decoding corrected the first review's interpretation of recording `1460e670…15f9`: orange remains on the outer-left platform at 00:24.50, both fighters converge at the upper right for the terminal impact at 00:35.60, and the result presentation begins after that frame.
The `teal-duel-replay` therefore ends at tick 786 with a damage winner and no ring-out; ring-out remains a separately tested simulation capability instead of a claim about this footage.
Capture now waits for Bevy's screenshot-completion event with bounded failure, and metadata plus every generated PNG destination are resolved and checked pairwise before rendering, networking, or writing.
Visible execution requires the observed physical display identity `(364,-1080)`, 1920×1080, while visible and offscreen rendering share one snapshot-derived camera transform.
Fresh reviewer `codex:01a06920-7449-74d0-9b09-57855a012572/01a06a55-572e-7bd2-be97-605bb16bb6bb` approved re-admission of exact amended range `4b4f21cb822c2ffccdf397d3120bde4b2f8bb2bd..91f65999d708e435a293e82fdb478a6b6ce8ecb7` with no findings; the owner then restored the admitted ticket to `ready` in commit `21c1798ebb57a045022a8c74d7bb37dba363c921` before implementation correction resumed.

## 2026-09-04 — Admit the explosive timber-collapse stress slice

Ticket 040 binds recording `453954a7…a18c` from 03:26.00–03:50.00 to one vertical slice that combines authoritative Rapier bodies and joints, an explosion-driven persistent debris field, progressive two-client dynamic-world snapshots, received-state rendering, and the footage's bloom, chromatic/radial shock, particles, camera response, background, and shadows.
The existing private physics boundary remains: `bevy_rapier2d` 0.36.0 resolves `rapier2d` 0.35.0-glamx0.2, while project-owned stable body identities and quantized snapshots cross ECS and network boundaries instead of engine handles.
Presentation first uses Bevy 0.19's public bloom, chromatic-aberration, and lens-distortion components; custom fullscreen machinery is allowed only after a source comparison demonstrates a concrete missing effect.
The slice extends the shipped replay, capture, and smoke paths rather than creating another evidence launcher, and support plus test growth must remain smaller than the product behavior it protects.
Fresh reviewer `codex:01a06920-7449-74d0-9b09-57855a012572/01a06af4-05f6-7233-a557-3f5709224503` admitted the corrected contract at risk 4 with no findings after independently decoding the source sequence and confirming feasibility, ordering after closed ticket 039, and absence of an open human decision.

## 2026-09-04 — Model the timber collapse with released fixed joints and retained ropes

The first reactive-world profile uses 17 dynamic timber bodies held in their intact silhouette by project-owned fixed joints and two dynamic circular weights attached by rope joints.
At tick 864 one authoritative explosion releases the fixed joints, wakes and radially impulses the 16 bodies inside its 520-unit radius, and leaves both rope joints intact.
This is the smallest real Rapier configuration that reproduces the observed intact-to-collapse transition while keeping every post-impact pose contact-derived; it does not claim the original game's hidden joint graph or card constants.

Presentation uses Bevy 0.19's built-in HDR `Bloom`, `ChromaticAberration`, and `LensDistortion` components on both the offscreen and visible cameras.
Direct comparison found remaining differences in scale, duration, irregularity, and debris density, but did not establish a missing effect that required a custom fullscreen pass.
The project therefore keeps the render boundary at public Bevy components and snapshot-derived ordinary scene entities.

## 2026-09-04 — Separate timber flash, floor response, and delayed screen shock

Direct source-and-clone review rejected the first explosion treatment because one large HDR disk and a warm veil obscured the arena, the radial/chromatic response had already faded by the source's 03:41.20 shock frame, and the hot-pink floor stayed visually flat.
The shared visible/offscreen scene now uses a 36-tick compact multi-lobed flash, a delayed shock envelope peaking 48 ticks after impact, a faceted floor mesh whose contour and colored edge echo respond to that event, and Bevy's public bloom, chromatic-aberration, and lens-distortion components.
This produces the required visible response without a custom fullscreen pass; the collision floor remains one fixed authoritative Rapier body because the source interval proves surface deformation and structure collapse, not floor fracture.

## 2026-09-04 — Admit the rematch and two-player card-draft slice

Ticket 041 binds recording `453954a7…a18c` from 02:40.00–03:20.00 to the first authoritative match-shell slice: blue's visible 4–5 victory, orange's elimination, the exact `Po De Th Qu Bu` and `Bu Ca Co Co Fa` prior-card stacks, one rematch vote per client, a both-yes reset to 0–0 with the result and old cards cleared, alternating five-card presentations, orange's `DAZZLE` choice, blue's `EXPLOSIVE BULLET` choice, and the return to upgraded combat.
All ten visible faces keep stable IDs and transcribed display metadata, but only the two source-selected effects are implemented in this bounded ticket. The other eight are explicitly catalog-only: the fidelity replay may render and hover them, but the authority rejects confirmation with `UnimplementedItem` and general offer generation excludes them until their own combat tickets unlock real behavior.
The draft authority owns votes, phase, offers, active player, confirmation, scores, and loadouts; clients send semantic actions and animate received state without predicting confirmed choices. Persistent `Da`/`Ex` badges, not momentary raised cards, determine the selected source items.
Fresh reviewer `codex:01a06920-7449-74d0-9b09-57855a012572/01a06b87-1778-7283-b7e1-8090fc186167` admitted the corrected risk-4 contract after rejecting the initial `BURST` misread, undefined inert-card selection, unspecified rematch ownership/reset, and impossible work-log timestamps.
Delivery comparison later falsified four contract assumptions: the source already shows blue's complete fan at 02:56 rather than a long blank handoff; the concluded snapshot needs an authoritative blue winner, orange elimination, and both observed old badge stacks; each card needs item-key-specific art plus selection-responsive hand/face poses; and offscreen evidence must wait for complete scene and pipeline readiness and repeat a complete frame hash. The same fresh admission reviewer approved those amendments before implementation correction resumed.
A second delivery comparison corrected the remaining frame alignment: orange focuses `COMBINE` at 02:49 and `BURST` at 02:50 before the confirmed `DAZZLE` fan lowers; blue focuses `DAZZLE`, `LIFESTEALER`, and `ECHO` before its confirmed `EXPLOSIVE BULLET` lowers. Empty 0–0 pips and confirmed `Da`/`Ex` badges remain authoritative HUD state throughout the relevant draft frames, and evidence may use `cold` only when its executable comes from the named verified-absent isolated target.
The correction keeps only a half-second dark bridge, makes blue's complete fan authoritative at the 02:56 anchor, projects every catalog `art_key` into a recognizable motif, and derives hands, arms, eyes, and mouth from authoritative hover/reveal state. Capture now waits for exact scene-role counts, an empty Bevy pipeline queue, and two consecutive complete extracted render frames; a focused test requires two captures of the same immutable state to be byte-identical.

## 2026-09-04 — Keep draft choices semantic and card effects typed

The rematch profile carries phase revisions and semantic vote, hover, and confirm commands over the existing per-tick authority stream; it does not replicate card transforms or allow presentation to apply an upgrade.
One stable nine-definition registry projects the ten visible offers because `DAZZLE` appears in both fans, and the seven distinct unselected definitions remain catalog-only even though navigation may focus them.
The selected definitions fold into one `FighterCapabilities` value rather than one component per card: Dazzle marks bullets with an explicit three-pulse stun and Explosive Bullet marks them with an explicit radial impact, impulse, and reload tradeoff.
This is enough compositional ECS state for the observed return to combat without introducing a string effect interpreter or a framework for unimplemented cards.

## 2026-09-04 — Admit the radial-saw duel and HALF BLUE transition

Ticket 042 binds recording `1460e670…15f9` from native source PTS 2320490718 through 2476823426 to one continuous moving-hazard duel and result transition.
The project owns stable saw identities, angle, and angular velocity through Rapier, ECS snapshots, digests, and UDP state; the renderer consumes that state and cannot advance saw pose, winner, or score locally.
Canonical source identity uses the first decoded frame at or after an integer source PTS and SHA-256 over the native 1280×720 packed RGBA plane, avoiding route-dependent PNG bytes and coarse-frame ambiguity.
Native consecutive frames disproved the first contract's supposed ice burst, fullscreen distortion, and burst-audio premise, so the admitted slice contains only observed ordinary projectile feedback, rotating saws, background motion, and the exact adjacent-frame handoff into `HALF BLUE`.
Ticket 042 is the first radial-saw child split from arena umbrella ticket 018; it contributes evidence toward but does not close or depend on the older base-feel, projectile-presentation, overall-presentation/audio, or full-match-replay umbrellas.
Fresh reviewer `codex:01a06920-7449-74d0-9b09-57855a012572/01a06c5b-0eb9-7e10-98b8-7e2813eeda52` admitted the corrected risk-4 contract after independently reproducing all five native RGBA hashes, the last-combat/result-onset boundary, source scope, ownership, feasibility, and proportional evidence.

## 2026-09-04 — Keep radial hazards authoritative but non-damaging until contact is observed

Consecutive source frames measure the two visible eight-tooth saws rotating counter-clockwise at approximately 7.43 rad/s, but the bounded duel never shows a fighter contact one. The implementation therefore gives both saws stable ECS identities and real kinematic Rapier bodies, serializes their collider pose and angular velocity, and proves immediate authoritative reset, while their collision group has no damage consumer. The replay result comes from the observed ordinary projectile path; adding saw lethality, an ice burst, fullscreen distortion, or audio would assert behavior this interval does not establish.

The combat snapshot begins with orange's existing half at `[1, 0]`; blue's ordinary tick-909 win persists `[1, 1]` into tick-938 `HalfBlue`. Presentation projects those values as the two half-filled result circles and interpolates only their size and dimming. Arena, saw, combat, and round projections each have a wire-visible digest so the authority, both UDP clients, and received-state render can agree without replicating brush strokes, shadows, particles, or UI transforms.

## 2026-09-04 — Admit the yellow-crate terminal blast and radial screen echo

Ticket 043 binds recording `453954a7…a18c` from native source PTS 4220149786 through 4245983016 to one continuous terminal-hit slice: a real Rapier crate responds to the authoritative explosion, blue is eliminated, orange scores, and the source's local burst, whole-scene radial echoes, chromatic separation, trails, and adjacent-frame result handoff appear through the same received-state Bevy renderer used by two UDP clients.
The source proves discrete repeated copies of the final composited viewport, including HUD, which Bevy's single-sample bloom, chromatic-aberration, and lens-distortion components do not provide. The slice therefore permits exactly one bounded project-owned fullscreen multi-tap pass over Bevy's public final-view target; it does not permit a renderer fork or a reusable post-processing framework.
The contract makes no claim about hidden card formulas and defers final audio fidelity to ticket 020. It follows closed ticket 042, advances the editable arena and presentation umbrellas 018 and 020, and leaves modified-projectile verification to ticket 019. Fresh reviewer `codex:01a06920-7449-74d0-9b09-57855a012572/01a06d9b-afc7-7db1-8001-364cbaafb1db` approved the corrected contract at risk 4 with no remaining findings or open human decision.

## 2026-09-04 — Reject soft zoom smear as the yellow blast echo

Ticket 043's first delivery candidate proved the desired Bevy architecture, real crate physics, authority and UDP flow, and one final-composite GPU pass, but its own original-resolution pairs disproved visual completion. The peak lost the readable local explosion and produced broad soft smearing where the source shows crisp discrete green, yellow, and red copies of the whole scene, including HUD. Delivery therefore requires tuning the existing single pass and shared scene—not adding an evidence renderer, weakening the contract, or accepting effect presence as effect fidelity.

## 2026-09-04 — Keep yellow convergence and impact impulse authoritative

The corrected ticket 043 presentation now matches the bounded blast signature, but review found two simulation facts that its successful rendering concealed: both fighters remain stationary throughout the source's calm convergence, and the published impact event omits the health-scaled portion of the impulse authority applies. The replay must therefore drive the observed approach through ordinary authority inputs and publish the exact applied gameplay impulse in its stable event. Presentation motion and a merely nonzero event field are not substitutes for those authority-owned facts.

## 2026-09-04 — Reopen the yellow result boundary from native consecutive frames

Final review independently decoded the source frame before the ticket's supposed last-combat anchor and proved that PTS 4238483046 already contains the first dimmed result circles; PTS 4238649712 is the following, larger transition frame. Ticket 043 returns to blocked amendment rather than teaching code and evidence the wrong adjacent-frame boundary. The native preceding frame, contract labels, replay tick, captures, and README slice inventory must be corrected and freshly admitted before implementation resumes.

## 2026-09-04 — Re-admit the corrected yellow result boundary

Ticket 043 now binds PTS 4238316380 to tick 109 as final undimmed combat, adjacent PTS 4238483046 to tick 110 as first dimmed result onset, and PTS 4238649712 to tick 111 as the larger following transition. The eleven-anchor contract preserves all three frames and the source's single two-tick-duration frame explains why decoded ordinals 108/109/110 map to replay ticks 109/110/111. Fresh independent review reproduced every native hash and re-admitted the amended risk-4 contract; implementation may resume without changing the accepted Bevy, physics, authority, or multiplayer architecture.

## 2026-09-04 — Bound clean Cargo builds to two jobs and reuse compatible artifacts

A confirmed ticket-043 cold workspace test allowed about seven MSVC linkers to run together, produced roughly 16.9 GiB of artifacts, saturated memory and disk, and froze the host. Human direction admits ticket 044 at risk 2: repository Cargo configuration now enforces two jobs and a worktree-local ignored reusable target. Clean builds must announce their target, reason, cap, approximately 17 GiB precedent, and cleanup plan before launch; verification coverage remains complete and sequential compatible commands reuse the prepared artifacts.

## 2026-09-05 — Admit one continuous rematch-to-two-halves match slice

Ticket 045 composes closed tickets 040 and 041 into one authoritative playable session instead of adding a sixth disconnected replay showcase. Players or automation accept the rematch, draft `DAZZLE` and `EXPLOSIVE BULLET`, return to combat, produce `HALF BLUE`, load the timber arena with state intact, trigger the physical collapse through ordinary projectile contact, and produce the answering `HALF ORANGE` for a 1–1 half state.
The source contract begins at native PTS 1595160286 and ends at PTS 2351823926, the independently reproduced last clean result-only frame; adjacent PTS 2351990592 is the first visible ice geometry and remains outside the ticket. Durable gameplay remains ECS/authority state while scene actions and anchors are configuration, and implementation may not increase the current replay-profile branch count to connect the systems.
Fresh reviewer `codex:01a06920-7449-74d0-9b09-57855a012572/admit_045` approved the corrected risk-4 contract after rejecting two omitted source transitions and one imprecise endpoint. The admitted evidence stays limited to connected lifecycle and causality regressions, one two-client smoke, and shared-renderer source anchors.

## 2026-09-05 — Admit the connected ice duel and first full round

Ticket 046 extends the same rematch authority after the split timber result into the observed ice arena and its first full-round award. Completed rounds become distinct from current-half progress, either color can win through ordinary live combat, and a two-fight sweep earns a round without forcing a third arena. The source retains the losing half through this result; the following draft and later reset remain outside the interval.
Fresh reviewer codex:01a073e9-17ec-7170-933a-0e18a071972d/01a073f3-6a0d-76d0-8ca1-8d01b63c2c81 independently reproduced all twelve source anchors and both adjacent boundaries, inspected the frames, and admitted the risk-4 contract with ticket 045 as its completed prerequisite. The static ice contours add no unobserved friction or destruction rule; existing authority, Rapier, input, transport and shared rendering remain the delivery path.

## 2026-09-07 — Freeze eliminated fighters and settle simultaneous elimination once

Ticket 047 corrects a defect independent of any recording: a ring-out cleared `alive` but left health, so both fighters could keep moving, aiming, blocking and shooting after falling together, and a later hit could invent a winner after both had already lost. Elimination now removes all input agency, contacts with a dead body are discarded, and the outcome of a tick is resolved once from the surviving fighters rather than assigned per hit.
A same-tick double elimination records a no-award result through the existing flow authority: no winner, no eliminated player, halves, completed rounds and loadouts unchanged, and after the ordinary conclusion pause both fighters revive at the current arena's spawns with projectiles cleared. This reuses the archived prototype's provisional policy because no simultaneous elimination is visible in either recording; it is not a fidelity claim, adds no new result visual or phase, and remains the open product decision named in the ticket.

## 2026-09-07 — Admit the source-timed ice short-hop input correction

Ticket 049 becomes an input-only contract. Source blue jumps at ticks 4673, 4689 and 4769 while `connected_ice_input` gives blue no jump at all between 4656 and 4801, so blue lands on the upper-right platform, walks it at half the source's airborne speed and takes an extra stop on the lower-right inner column, arriving 95 native pixels right and 36 lower at the bound 4786 anchor. Only the blue rows of that input table may move: no physics constant, contour, friction, gravity value, jump-release rule, forced velocity, scripted pose or new replay profile is admitted, and the twelve ice anchor ticks with their source PTS/hash pairings stay fixed. The remaining arc-shape residual, the clone's 122.75-pixel, 24-tick jump against the source's 82.5-pixel, 13-tick one, moved to idea 050 and sets the tolerances this contract can honestly ask for.
Fresh reviewer claude:96849848-e6ec-488e-b4ac-b113acc49f8a/admit-049-opus admitted the risk-4 contract for shaping range 9bd8773..56dc2b4. It re-hashed the recording, re-decoded the 119 native frames from PTS 2373157174 to 2392823762 with a different seek offset and select form than the retained decoder, reproduced all eleven frame identities in the ticket's table and all 120 frames of both retained manifests, inspected the take-off, apex and landing frames, and re-measured blue's health bar and body from the pixels. It then reproduced every clone number from the prebuilt executables, including blue grounded on the platform top from 4680 to 4725, on the lower-right column from 4748 to 4771, on the stepped base at 4795, and at image (735.3,404.3) at 4786 against source (640.0,368.5), and confirmed the code seams, including that `player_grounded` already counts a side contact and that grounded is written after the physics step.
Its findings were folded into amendment 8548542 rather than left as notes: the recapture clause now covers the bound 4603 anchor that the corrected 4601–4656 hold will change; the 35-pixel vertical tolerance at 4786 and the 30-pixel horizontal landing clause are labelled as guards because today's route already meets them; later re-timing is limited to blue's rows; and the reviewer's model showing that a jump from the right outer platform's top surface passes the wall contact band by three to eleven pixels is recorded as a requirement to demonstrate the actual 4689 contact in the public trace. A second fresh context, claude:96849848-e6ec-488e-b4ac-b113acc49f8a/admit-049-confirm-opus, confirmed the amended cumulative range 9bd8773..8548542 at risk 4 with no open human decision.

## 2026-09-07 — Return the ice short-hop to blocked: input timing alone cannot reproduce it

Ticket 049 was admitted as an input-only correction and its implementer disproved that premise with public-route measurements rather than opinion. Under the shipped fixed jump impulse and 192 px/s air-speed ceiling, blue cannot reach the upper-right platform's wall at tick 4689 (19 px too high at best), cannot clear both right-hand columns on the way down (27 px short), and cannot reach the stepped base's shoulder by tick 4783 (54 to 70 px short). Every one of these is the jump-arc difference that idea 050 records: the source rises fast, stops early and falls gently, while the clone rises half again as high over nearly twice as many ticks.
The contract therefore returns to blocked with the failing reproduction retained outside the tree, and depends on 050. Changing shared vertical control touches all five replay profiles and every source-paired anchor, so 050 stays at risk 5 and needs a human decision before any implementation.

## 2026-09-07 — Admit the scripted-jump hold rewrite and return the release-cut contract

Ticket 051 splits the free half out of idea 050. Seventeen jumps in the five replay scripts are one-tick presses, and `set_player_control` reads the jump input only together with `grounded`, so extending each press across the ticks its fighter is already airborne is a provable no-op at the public boundary while making every jump that is meant to be full read as fully held. Zero movement in the nine per-anchor digests and in `metrics.jumps` is the acceptance criterion rather than an expectation; `inputTraceSha256`, which hashes the serialised scripts, and `executableSha256` are the only metadata fields allowed to move.
Fresh reviewer claude:96849848-e6ec-488e-b4ac-b113acc49f8a/admit-051-050-opus admitted 051 at risk 2 for range b986cf4..d166347. It re-derived all seventeen presses from the shipped scripts rather than the ticket, re-measured every airborne window through the prebuilt `inspect` boundary tick by tick, reproducing all seventeen rows, the seven full holds, the nine short ones and timber orange 960's complete absence of an airborne tick, and confirmed the jump counts 17, 6, 0, 5 and 141, the one-tick script-index offset that follows from `step` incrementing the tick before control, and that a hold covering a grounded tick re-fires the impulse.
The same review returned ticket 050 rather than admitting it. Its Outcome binds the release cut to the tick the input falls from held to released while rising, with no airborne condition, but ticket 051's rewrite necessarily leaves the press tick's successor outside the hold, and the reviewer measured all seven full-hold fighters grounded at that tick with velocity.y +647.447. Under the guard as written every one of the seventeen jumps is cut on its second tick, which contradicts the same ticket's promise that fully held jumps keep their arcs and removes the reason its risk fell from 5 to 4. Gating the cut on the fighter being airborne resolves it and also shrinks the movable-anchor set to the release ticks of the eleven pre-existing hold windows; the reviewer additionally found that a fit against airborne source arcs cannot choose between the two guards, that the promise to unblock ticket 049's second bound is unmeasured against a horizontal-reach limit a vertical cut does not touch, and that the contract sets no criterion for anchors that do move.

## 2026-09-07 — Admit the release-cut contract with its airborne gate

Ticket 050 returns from amendment with the guard the earlier review demanded. The cut fires only when a fighter's jump input falls from held to released on a tick where the `grounded` argument `set_player_control` already receives is false and vertical velocity is positive; a release read on a grounded tick does nothing whatever the velocity. That single condition turns ticket 051's seventeen rewritten presses from a measurement into a construction: `{T} ∪ [A, R)` carries exactly two transitions, at `T + 1` and at `R`, and both are grounded by 051's own definitions, so no cut factor can reach them. What remains reachable is the release tick of the thirteen multi-tick holds the shipped scripts already carry, not the eleven the returned review named, which were the eleven `connected_ice_input` rows with `jump = 1`, three of them one-tick presses 051 rewrites. All thirteen live in teal and rematch, so radial, yellow and timber are preserved entire, the worst-case identical set is 53 of the 73 anchors and the movable set at most 20, published before a single frame is recaptured; an anchor that moves may not put any affected fighter farther from its bound source position than today, measured as body centre and green health-bar centre in native 1280×720 pixels, and one that does blocks delivery.
Fresh reviewer claude:96849848-e6ec-488e-b4ac-b113acc49f8a/admit-050-opus admitted the contract at risk 4 for exact range b986cf4..be8e0cf. It re-implemented the jump field of both script functions from the shipped source rather than reading the ticket's tables, reproducing all thirteen holds with their release ticks and all seventeen presses; checked that the ice override at tick 4601 truncates no window and that no rewritten hold abuts a pre-existing one; verified every one of the seventeen uncuttability rows against the retained grounded rescan, including timber orange 960's complete absence of an airborne tick; recounted the 73 anchors from the client's own lists and reproduced the 53/20 split and the 32 with no jump before them; and confirmed in the code that `step` increments the tick before control while `grounded` is written after the physics step, so a cut at release `R` first reaches the snapshot at `R + 1`. It also found that the acceptance band of 82–90 px over 12–14 ticks at fitted v0 730–780 and ascent 2900–3300 uniquely selects ×0.25 at a ten-tick release on the retained model grid and rules out the continuous variant, so the one remaining binary is closed. Its findings were non-blocking; two wording corrections were folded in before the status change and the rest stay as notes, including that two source arcs at 129.2 and 151.5 px exceed the clone's uncut 122.85 px ceiling and are unreachable while the contract freezes every constant.

## 2026-09-07 — Re-admit the release-cut contract with its holds extended to the end of their rise

Ticket 050 returns for the third time, and the two returns between are part of the record. The first implementation attempt delivered the mechanic exactly as bound and was blocked by the contract's own no-worse criterion: eight of the thirteen pre-existing multi-tick holds release while their fighter is airborne and rising, all in `rematch-draft-replay`, and cutting them costs blue the height the shipped ice route was written against; fourteen of fifteen moved anchors got worse against the source, with blue 489 px from its bound position and off the bottom of the frame at `ice-early-traversal`, and no cut factor below 1.00 preserved the round. The amendment that answered it was itself returned, because ending each hold at the first airborne tick with non-positive velocity puts grounded ticks inside three of the eight extensions, where a held jump re-fires the full impulse, and because orange's `[4000, 4420)` hold has no apex in flight at all: blue's elimination at 4434 freezes orange airborne until the ice respawn zeroes its velocity at 4541. The current amendment defines the extension endpoint as the exact negation of the cut's own condition, the first tick that is no longer airborne-and-rising (`grounded` true or `velocity_y` at most 0), so every added tick is inert by construction and the new release is inert whichever clause ends it, and it binds the acceptance band to the single eleven-tick release the shipped simulation actually clears.
Fresh reviewer claude:96849848-e6ec-488e-b4ac-b113acc49f8a/admit-050c-opus admitted the contract at risk 3 for exact commit 0d58d1c. Rather than re-reading the returning review's series it re-derived all eight endpoints from its own per-tick runs at the public `inspect` boundary, reproducing 4541, 4432, 4666, 4813, 4971, 5184, 5244 and 5282 with every intermediate tick measured airborne and rising, and confirmed the three grounded contacts, the round-end freeze, the exact zero the respawn sets, the five falling releases left alone, the single site at which `input.jump` is read, the unchanged window count of 46, and the feasibility of the one extension that lands on overlapping last-match-wins rows. It also checked every fit number against the retained record: the acceptance band is cleared by exactly two factors and only at an eleven-tick release, which is now what the contract asserts. Its four findings are notes, the first being that neither earlier return had reached this file; that omission is corrected here.

## 2026-09-07 — Return, correct and approve the release cut

Ticket 050's first implementation candidate a5348f9 delivered the mechanic and the eight staged hold extensions exactly as bound, and a fresh review returned it on one blocking finding: the release memory was written inside `set_player_control`, which `step` skips while a fighter is stunned or eliminated and on every early-return tick, so a release read on a skipped tick was carried forward and judged against whatever state control next saw. Reproduced at the public boundary with a Dazzle-shaped six-tick stun written over the grounded release ticket 051's rewrite leaves at `T + 1`, the deferred release cut a jump the fighter was already halfway through, collapsing it from 116.862 px over 23 ticks to 53.993. That falsified two of the contract's own promises, that a release read on a grounded tick does nothing at all and that none of the seventeen rewritten presses can ever be cut. The correction moves the decision to the call site: `step` computes the held-to-released transition and updates the memory for both fighters immediately after `acts`, outside every gate, `set_player_control` takes the transition as a `bool` and owns no memory, and `revive_fighters` clears it with the rest of the transient control state. Three regressions were written first and shown failing (the stunned release read from `snapshot` alone, the eliminated release, and the revive), and the ticket 051 contract table gained a `frozen_releases` case pinning the one shipped release that falls inside a freeze.
Fresh reviewer claude:96849848-e6ec-488e-b4ac-b113acc49f8a/review-050b-opus approved exact commit 06dcbcb for range b884b1f..06dcbcb. Rather than trust the correction's account it reverted only the correction's runtime hunks in a copy outside the worktree, verified that copy diffs against a5348f9's runtime to nothing but a doc comment, and confirmed all three regressions fail there with the returning review's numbers to the milli and pass at 06dcbcb. It then enumerated every remaining tick on which a living fighter's input goes unread and established that all four early returns precede the physics step and the `grounded` rewrite, so state is frozen and a deferred release is judged against exactly the state it was made in, with every flow resumption into combat additionally passing through the revive that now clears the memory. It re-derived gate 1 from the table literals, reproduced the 46-window enumeration and ticket 049's bound 1 with its own probes, and re-ran the verification set: 50 tests passing, strict clippy and fmt clean, 50 of 50 recaptured anchors byte-identical on all nine digests with `inputTraceSha256` moved on the rematch profile alone, all five inspects byte-identical to ticket 051's baselines, and the smoke reporting the unchanged stateHash with every agreement flag true. Its findings are notes about doc wording for the single frozen release at orange 4541, the two tests that necessarily read the authority-internal memory, and a test-to-runtime ratio the contract's fitted acceptance band accounts for. With this delivery the release cut exists in every arena but no shipped route exercises it; ticket 049 remains blocked on horizontal reach and idea 052 holds that decision.

## 2026-09-07 — Re-admit the first loser draft after the protocol and weapon-evidence return

Ticket 053 takes the physics-free prefix of blocked ticket 048: the post-round draft from the bound first blue round pip at native PTS 2506156642 through the last frame before the hanging arena appears at PTS 2577323024, clone ticks 5466 to 5893, in which orange drafts QUICK SHOT while blue keeps its round and its card. Its first admission review returned it on two points. The required snapshot-protocol bump moves `stateSha256` at every anchor of every profile, so the contract now claims the 36 non-rematch anchors identical on the eight other digests and requires each `stateSha256` move to be shown as the protocol field alone by re-hashing the retained pre-change state text with the literal flipped, 36 of 36; the bump stays because five of the seven snapshot-shape changes in this repository moved both numbers and 046 is the nearest precedent. And no public input route can fire while holding QUICK SHOT, because `accepts_combat` admits only the three combat phases, `FlowAuthority::new` is the only writer of offers and fills them from `source_offers`, and `general_implemented_offer_pool` has no product caller; the impossible before-and-after firing test is replaced by a three-part authority-level route through the public step and flow-command surface, with the factor's magnitude left as an implementation choice because the interval contains no projectile. Two further facts are bound because the code forces them: `confirm` assigns capabilities wholesale, so appending a second item would zero Dazzle, and `selected` is never cleared, so every post-round confirmation would be rejected as a duplicate without a per-draft reset.
Fresh reviewer claude:96849848-e6ec-488e-b4ac-b113acc49f8a/admit-053-054b-opus admitted the risk-4 contract for exact commit ae61a37. It re-ran the attribution probe itself on two profiles by slicing the state text out of the raw inspect bytes, confirmed the text begins with the protocol field, hashes to the reported state hash and moves when the literal is flipped, established that the retention step works at the three tick-0 anchors, reproduced the round-blue freeze at ticks 5466 and 5893 with the live `selected` value that proves the reset clause, recounted the 73 anchors from the client's own lists, placed all eleven anchors on the PTS lattice with its three dropped frames, and verified every protocol precedent by diff. Its three notes are recorded on the ticket as binding clarifications: a third capture seam, the equality gate on the ice anchors that would emit 25 rematch anchors instead of 37 at 5,893; the weapon regression's third part cannot fail for the wiring it targets, so threading the factor into `spawn_bullet` is a review obligation; and the accumulation rule must classify the two explosion magnitudes.

## 2026-09-07 — Re-admit the coverage-ledger correction with current ice figures

Ticket 054 was returned because the row meant to stop the ledger overstating coverage would itself have frozen two superseded numbers, the 92/37 displacement measured against an earlier source estimate and the 211–238 px/s band that ticket 052 showed was a chord over the noisiest ticks of one flight. The amended row carries 95.3 px right and 35.8 px lower at the bound 4786 anchor against the re-tracked source body, and 052's fitted 218.2–219.4 px/s sustained top speed against the clone's 192.0 px/s fixed point with the residual reattributed to the approach rate; correcting the older pair in `docs/fidelity/ice-round-observations.md` is left to whichever of 049 or 052 lands. Fresh reviewer claude:96849848-e6ec-488e-b4ac-b113acc49f8a/admit-053-054b-opus admitted the risk-1 contract for exact commit ae61a37 with no findings, re-establishing the other four corrections from the shipped artefacts: the 07:00–07:10 row claims card combat and online play the yellow profile cannot carry, `FlowAuthority::new` hard-codes the 4–5 prior match, the presentation row omits the 042 and 043 sub-slices, and the second recording's 03:20–03:30 row needs the new-match reset frame beside the match-end frame it already carries.

## 2026-09-07 — Correct the loser-draft contract's split-from and confirm its admission

The admission recorded above under "Re-admit the first loser draft after the protocol and weapon-evidence return" is corrected here rather than left standing: that reviewer, claude:96849848-e6ec-488e-b4ac-b113acc49f8a/admit-053-054b-opus, later revised its verdict on ticket 053 from admit to return, on one point and no other. The Ivy ticket checker had never been run against these tickets; it is not in this checkout at all and was found in a stray clone under `/tmp`. It resolves every `depends-on`, `supersedes` and `split-from` against the ticket files in the checkout it scans, so 053's `split-from: [48]` failed outright: ticket 048 lives only in the detached worktree `.ivy/worktrees/048-first-loser-draft/` and has never reached main, which makes the relation unresolvable by construction. Commit 5310df1 clears the field and changes exactly one line. Nothing is lost, because the prose names 048 in five places, from the physics-free prefix this ticket takes to the source-fitted launch-speed magnitude that stays 048's, and `depends-on: [46]` resolves as it always did. The checker now exits 0; its remaining output is a notice, not an error, that ticket number 50 is claimed by a different title in that same stale 048 worktree, which must renumber before that work ever lands. Fresh reviewer claude:96849848-e6ec-488e-b4ac-b113acc49f8a/admit-053c-opus confirmed the amended contract at risk 4 for exact commit 5310df1 with no findings: the one-line frontmatter change is the whole difference from the contract admitted at 34f1398, so the earlier substantive verification stands unchanged. The general lesson is worth more than the fix: a relation field is a claim the checker can test, and a parent that exists only in a worktree cannot be cited in one, however faithfully the prose describes it.

## 2026-09-08 — Admit the held hanging-arena entry without its unresolved physics

Ticket 055 continues the connected loser-draft route from its empty bridge at tick 5893 through the last retained held source observation at tick 5941. It loads the measured 21-body hanging scene, preserves blue's completed round and `Ex` plus orange's `Da Qu`, initializes the measured fighter poses once, and reproduces the separately measured left, right and central object-entry curves. It adds no active combat time: tick 5942 is an excluded departure witness, and suspension topology, collision roles, movement, reload, projectile and damage behavior remain separate decisions.

The first admission review returned the contract because the precise geometry and frame identities still lived only in ignored evidence under the abandoned ticket-048 worktree. The correction moved all eleven native identities, the complete nominal layout, 42 visible link segments, source-shaped palette, spawn poses, curve knots and uncertainty into `docs/fidelity/held-hanging-entry-observations.md`. Fresh reviewer codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a080a5-1ad3-7a32-b389-afd732431f28 independently decoded every retained frame copy and matched all eleven packed RGBA hashes, confirmed the current flow, simulation and presentation seams can support the held slice without fake dynamic or constraint snapshots, and admitted exact range d8027c8..fb6947f at risk 4 with no open human decision.

## 2026-09-08 — Admit score-driven match completion without inventing its reset trigger

Ticket 057 adds the missing fifth-completed-round boundary and stops at the state the source actually exposes. A normal second-half award to five keeps the existing result and `ROUND` envelope, then enters a stable authoritative `Waiting` phase instead of opening another loser draft. The second recording proves `WAITING` over a live arena with both fighters, retained badges and a 3–5 score; the first independently concludes 4–5. The later empty 0–0 draft proves that a reset follows but not whether a timer, host, peer consensus or network event triggers it, so that transition remains separate work.

Fresh reviewer codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a0810e-331e-7d80-bddb-f7b7644813c7 first returned exact range a8341173..1598b49 because reviving ECS state alone would leave a ring-out loser beyond the kill boundary and invisible. The amended contract makes Waiting entry clear projectiles, respawn both physics bodies at the existing arena anchors and revive both ECS states without replacing the arena, and requires a public ring-out terminal regression beside the projectile route. The reviewer then admitted exact range a8341173ea0661cebeec018a7710968e1483b734..3eb7a1f3c05877f4b36dca5e355621c5306f6ee7 at risk 4 with no remaining findings.
