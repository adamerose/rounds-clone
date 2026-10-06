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
