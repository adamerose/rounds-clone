---
format: 3
status: ready
created: 2026-10-05T14:13:11Z
origin: human-request
tags: ["quarrel", "mvp", "arenas"]
value: 8
sessions:
  - claude:bcbe88ae-0a32-432f-8fd1-3a061e17847f
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
