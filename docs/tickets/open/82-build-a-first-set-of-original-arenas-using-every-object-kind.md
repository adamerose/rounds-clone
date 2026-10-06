---
format: 3
status: ready
owner: codex:01a10efb-9238-7e12-aecc-c488e6f1fa44
created: 2026-10-05T14:13:11Z
origin: human-request
tags: ["quarrel", "mvp", "arenas"]
value: 8
sessions:
  - claude:bcbe88ae-0a32-432f-8fd1-3a061e17847f
  - codex:01a10efb-9238-7e12-aecc-c488e6f1fa44
execution: unattended
parent: 75
depends-on: [77, 78]
supersedes: []
split-from: []
---

# Build a first set of original arenas using every object kind

With a new random arena every fight, a match of five to nine fights needs enough arenas to stay fresh, and each ROUNDS object kind needs at least one arena built around it.

## Outcome

- At least ten original arenas in `assets/arenas/`, besides the converted scenes: three static, two physics stacks that topple when shot, two hanging arenas held by chains, one with a wrecking ball that swings into the middle when what holds it breaks, one with saws, and one with moving parts.
- Every arena has safe spawns for two and four fighters, and fits the fixed camera frame.
- Layouts are our own; `docs/rounds-maps.md` informs the kinds, not the shapes.

## Decisions

- Chains never break and there are no drop-through platforms (Adam, 2026-10-04).

## Evidence required

- A preview PNG of each arena, inspected for readability and recorded in the work log (previews stay out of Git).
- A test spawns two and four fighters in each arena and checks none starts inside geometry or a saw, and each simulates 20 seconds headless without a panic or a piece leaving the world.
- A test checks that every object kind from #77 appears in at least one original arena.

## Chat excerpts

Adam — this session, 2026-10-04:

> i remember one map has two big balls on strings that you can shoot a blocker and they crash into the map center

## Work log

- 2026-10-05T14:13:11Z Drafted under run #75.
- 2026-10-05T15:04:03Z Admitted for run #75 after an independent contract check; its fixes were applied first.
- 2026-10-06T02:14:17Z stage research start session codex:01a10efb-9238-7e12-aecc-c488e6f1fa44 — Claimed ticket; inspect data format, runtime and prepared-target locking on origin/main.
- 2026-10-06T02:14:21Z stage research end session codex:01a10efb-9238-7e12-aecc-c488e6f1fa44 — Existing RON objects and public arena-preview cover the outcome; code changes stay in arena tests. Cargo uses its target .cargo-lock; no custom repository build wrapper exists.
- 2026-10-06T02:16:09Z stage implement start session codex:01a10efb-9238-7e12-aecc-c488e6f1fa44 — Author ten original layouts and boundary tests for safe spawns, object-kind coverage, 1200 active physics ticks, shot-toppled stacks and released wrecking ball.
- 2026-10-06T02:23:11Z stage implement end session codex:01a10efb-9238-7e12-aecc-c488e6f1fa44 — Ten original RON arenas and four boundary tests pass; two/four safe spawns, full kind coverage, 20-second active simulation, shot-toppled stacks with stable unshot controls and shot-released ball with permanent chain.
- 2026-10-06T02:23:15Z stage verify start session codex:01a10efb-9238-7e12-aecc-c488e6f1fa44 — Run format, strict all-target workspace Clippy, locked build/tests, ten offscreen previews and ordinary CLI smoke/capture in prepared target with jobs=2 and Cargo locking.
- 2026-10-06T02:37:14Z stage verify end session codex:01a10efb-9238-7e12-aecc-c488e6f1fa44 — Fmt, strict workspace all-target Clippy, locked build, 40 copied workspace tests and doc-tests pass. Four arena tests run 20 active seconds for two/four fighters and shot scenarios. Inspected all ten PNGs: switchyard, terraces, keyhole, kiln, trestle, skybridge, lanterns, gatefall, pinch, shuttle; fresh build reproduces each PNG byte-for-byte. UDP 1200-tick smoke agrees through opening/loser drafts and match end; capture passes. Evidence root out/ticket-082; initial shared-target workspace logs invalid, corrected artifact-time snapshots and test lists retained. Build-cache follow-up ticket87 remains idea; human feel playtesting outside run75.
- 2026-10-06T02:47:07Z stage review start session codex:01a10efb-9238-7e12-aecc-c488e6f1fa44 — Candidate 3d59be78dae18c06ebab13529fa6114f6562477c..9cadb9822227fae7b7e55f3dde65c27ab8be63fb; rebased append-only decision conflict preserves both entries and all source/assets hashes. Repeated fmt/strict Clippy/locked build and all40 copied tests pass. Fresh other-family Opus-high CLI reviews complete range and all ten PNGs; artifacts root out/ticket-082 and unique OS temporary parent.
