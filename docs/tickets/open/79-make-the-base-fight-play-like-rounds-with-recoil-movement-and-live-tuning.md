---
format: 3
status: ready
owner: codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d
created: 2026-10-05T14:13:11Z
origin: human-request
tags: ["quarrel", "mvp", "combat"]
value: 9
sessions:
  - claude:bcbe88ae-0a32-432f-8fd1-3a061e17847f
  - codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d
execution: unattended
parent: 75
depends-on: [78]
supersedes: []
split-from: []
---

# Make the base fight play like ROUNDS, with recoil movement and live tuning

QUARREL keeps ROUNDS' controls and fight basics because they carry its pace and skill expression, and the base fight must be fun with no cards before cards are worth tuning.
The current code differs from ROUNDS in places, most visibly by killing a fighter who leaves the arena.

## Outcome

- The basics in `docs/rounds-reference.md` hold: one stored jump restored by floor or wall; wall cling and climbing; crouch that halves height on the ground and falls faster in the air; three-shot magazine and reload; shots that drop under gravity and push what they hit; a base hit removing about 60 % of health; a block of about half a second that reflects shots and extends when it does, with a cooldown, that does not stop damage over time; no fall damage.
- Crossing any screen edge deals heavy damage and pushes the fighter back in instead of killing; blocking just before touching an edge cancels the damage and launches the fighter off it.
- Firing pushes the shooter back with a capped strength, so a downward shot mid-air gives a small boost; recoil can be switched off in tuning for comparison.
- Every movement, shot, block, edge and recoil number lives in `assets/tuning.ron` and reloads while the game runs.

## Decisions

- Keep ROUNDS' controls and basics; recoil movement is the only change under test (Adam, 2026-10-04).
- Numbers are our own starting points tuned by feel, not footage measurements.
- Perfect blocks and impact-scaled edge damage are not added (Adam, 2026-10-04).

## Evidence required

- Sim tests for each rule above, including an edge hit dealing damage and pushing back, an edge block launching without damage, block reflection and extension, crouch height, and capped recoil.
- A headless capture shows an edge launch and a reflected shot.
- A test changes `assets/tuning.ron` during a session and observes the new value.

## Chat excerpts

Adam — this session, 2026-10-04:

> yeah. also lets revisit the fundamental mechanics. are we just copying rounds so far? left click fires an arcing bullet, jumping, blocking, map edge damage and bounce up, blocking to bounce far as you hit the map edge and negate damage, hanging on to walls, wall jump, etc. do we want to change any of that?

> 1. nah 2. nah 3. yes lets try it

## Work log

- 2026-10-05T14:13:11Z Drafted under run #75.
- 2026-10-05T15:03:20Z Admitted for run #75 after an independent contract check; its fixes were applied first.
- 2026-10-06T02:13:14Z stage implement start — traced boundary defects: zero bullet gravity, 100 damage, no magazine/cooldown, grounded-only jump and instant edge elimination; implementing data-driven baseline.
- 2026-10-06T02:26:10Z stage verify start — ten combat regressions pass; regenerated ordinary recording via public inputs; two-client 2400-tick smoke reaches loser draft and match end with matching states; edge and reflection PNGs rendered.
- 2026-10-06T02:35:47Z verification correction — rejected a shared-target run that contained only 16 sim tests and a later mixed-branch snapshot compile. Retaining dependencies, forcing local source freshness across the Cargo queue, and freezing Cargo-produced test executables before serial execution. No clean target: disk headroom is below 1 GiB.
- 2026-10-06T02:45:19Z stage implement end — live tuning, stored/wall jumps, physical crouch, ballistic shots, magazines/reload, block cooldown and reflection extension, four-edge damage/launch and capped switchable recoil implemented; recording regenerated through public inputs.
- 2026-10-06T02:45:35Z stage verify end — format, strict all-target Clippy and locked workspace build passed; all 46 candidate tests passed from frozen Cargo test executables (26 sim, 14 network, 4 presentation, 2 CLI); 2400-tick two-client smoke agrees through loser draft and match end. Edge launch tick 15 and reflection tick 5 PNGs with provenance retained in out/079-base-fight; no GUI launched. Source timestamps restored.
- 2026-10-06T03:28:27Z stage correction start session codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d — reflection capture exposed solver rebound after instant reversal; regression now checks the following tick. Completing shared damage boundary and single-peer control checks before fresh review.
- 2026-10-06T03:40:00Z stage correction end session codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d — reflection continues outward next tick; periodic damage bypasses block; primary network controls retain assigned identity. Reconciled #82 test inputs for reload and ballistic aim; pinned three-fighter progression fixture. Focused stack regression passes.
- 2026-10-06T03:40:05Z stage verify start session codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d — final complete workspace checks on the integrated arena baseline, frozen candidate executables, regenerated captures and live-pool authoritative smoke.
- 2026-10-06T03:48:39Z stage verify end session codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d — exact candidate 3a199e3bad06cccd2a52fdd0c064434ee4a88b83 passes fmt, strict all-target Clippy, locked workspace build, all 55 tests (31 sim,14 network,8 presentation,2 CLI) and doc tests. Frozen client SHA256 66f07b8b890ec8c376717a281a7e23e70669b649900d33deca4db754bea388e7 produced edge, reflection, crouch and ordinary PNGs with provenance. Live-pool smoke at 1200 and 2400 ticks reaches both drafts and match end with agreeing clients. Evidence in out/079-base-fight; no GUI or human feel assessment.
- 2026-10-06T03:48:49Z stage review start session codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d — fresh other-family Claude CLI read-only review of the full candidate range and rendered evidence; no author context shared.
- 2026-10-06T03:58:41Z stage review end session claude:37f96ba5-cce7-4ffe-a0ac-c00635e6b13b — requested changes to exact candidate 3a199e3: stationary crouch floats above the floor and default recoil is negligible. Full findings retained in out/079-base-fight/review-round-1.json; reviewer created no artifacts or processes.
- 2026-10-06T04:05:26Z stage correction start session codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d — reproduced stationary crouch feet at -177213 instead of floor -180000 before collider replacement; fresh collider contacts pass the same positional regression. Raising default recoil impulse to 800 with a shipped-default cap check; fixing verified smaller regression notes together before complete fresh review.
- 2026-10-06T04:09:31Z stage correction end session codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d — fixed crouch contact anchors and checked feet through held crouch and stand-up; default impulse800 cap120 now tested against disabled recoil. Preserved extended blocks, top-returning arcing bullets, legacy snapshot visibility and both network keysets; rejected tuning logs and cursor-aim documentation added. Public record regenerated275 ticks; strict Clippy and build pass.
- 2026-10-06T04:09:36Z stage verify start session codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d — complete corrected workspace tests and refreshed headless captures before fresh full-range review.
- 2026-10-06T04:16:14Z stage verify end session codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d — corrected candidate b7c95420cb2e99e9af545deea79726234a018f4e passes fmt, strict all-target Clippy, locked build, all63 tests (34 sim,18 network,9 presentation,2 CLI) and documentation tests after #85 integration. Default recoil contribution is bounded110-120 pixels/second; crouched feet are -180.010 with centre -169.010, also checked on stand-up. Frozen client4440180123f7ecc92faa31241848a033d8845761bf34dc90872e7ce90e19c9b3 renders updated PNGs with provenance; live-pool1200/2400 smoke reaches both drafts and match end with matching states.
- 2026-10-06T04:16:21Z stage review start session codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d — fresh other-family read-only review of the complete corrected candidate, first review findings, new boundary regressions and refreshed captures.
- 2026-10-06T04:31:37Z stage review end session claude:70da852b-5656-470d-accb-087984980fdd — approved b7c95420 after all previous findings were addressed; complete result in out/079-base-fight/review-round-2.json. A note about damage edits rebuilding unchanged colliders reproduces a one-tick crouch pop, so this approved tip will not be delivered unchanged.
- 2026-10-06T04:31:43Z stage correction start session codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d — public tuning edit regression fails with crouch height44000 rather than22000 on the edit tick; rebuild only actual shape changes and update mass/materials in place for other edits.
- 2026-10-06T04:42:00Z stage correction end session codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d — damage-edit crouch regression passes; unchanged collider shapes retain contacts while mass/material values update. Source rebased onto landed #88 without combat conflicts; correction committed separately from the previous approved tip.
- 2026-10-06T04:42:11Z stage verify start session codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d — final source cleanliness and evidence binding for the combined candidate; full native suite and captures completed.
- 2026-10-06T04:42:24Z stage verify end session codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d — exact two-commit candidate ending0a854f20fff15271d107ac37db4e5a0b46e25186 passes fmt, strict all-target Clippy, locked build, all67 tests (35 sim,21 network,9 presentation,2 CLI), doc tests and1200/2400-tick two-client smoke. New public-file regression checks every edit tick retains crouch. PNG provenance frozen client5f141e257409569363d3105d4aa5e5fd279ca00b2624ecfe0d3f799436c4bee6; source timestamps restored, clean status, no GUI.
- 2026-10-06T04:42:32Z stage review start session codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d — fresh other-family review of the full two-commit candidate after selective tuning-update correction and #88 rebase; prior complete review and current evidence supplied.
- 2026-10-06T04:51:28Z Review approved the complete range. Nonblocking notes accepted: live collider-geometry edits briefly reset support contacts; unchanged geometry edits retain crouch throughout. Human play-feel assessment was unavailable; deterministic simulation, rendered captures and two-client network matches passed.
- 2026-10-06T04:58:59Z stage correction end session codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d — Reconciled landed menu ticket81: retained controller assignment, cancellation and camera conversion; preserved crouch/block and network aliases; corrected scripted block binding. Full rebased range requires fresh review.
- 2026-10-06T05:05:29Z stage verify end session codex:01a10efa-ac4b-7192-8f54-91f1d6f6da0d — Rebased menu-compatible candidate e5034fcf3c645f3df88049049eabb63116e0e30f: 78 tests passed (35 sim,24 network,14 presentation,5 CLI), no failed/skipped tests; fmt, strict Clippy, locked build, docs, two-client1200-tick smoke and four refreshed headless renders passed. Compiler source provenance and frozen binary312efc5 recorded. No human feel assessment.
