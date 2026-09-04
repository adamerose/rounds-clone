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
