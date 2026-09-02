---
format: 3
status: idea
created: 2026-08-29T02:31:59Z
origin: human-request
tags: ["product-fidelity", "maps", "simulation"]
value: 10
risk: 4
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: [15, 16]
supersedes: []
split-from: []
---

# Reconstruct arenas from direct ROUNDS evidence

The current match rotates through 62 arenas whose geometry was abstracted from preview silhouettes at provisional scale while most hazards and dynamic behavior are ignored. Replace that broad placeholder pool incrementally with arenas whose layout, scale, spawns, collisions, camera, kill bounds, hazards, and motion are demonstrated against direct ROUNDS play.

## Outcome

- Only directly verified arenas are eligible for ordinary play. Unverified catalog rows remain research records and are not presented as faithful playable maps.
- Each admitted arena matches a controlled target-build capture in platform topology, player-relative dimensions, spawn support, camera framing, collision behavior, kill bounds, and every visible dynamic or hazard behavior.
- Recovery artifact 011 may supply implementation ideas and tests, but its dirty bytes and provisional contracts are evidence, never authority.
- This ticket is the full 70-row target-catalog umbrella and remains open until every current-build arena is either delivered through an independently reviewable child/group with complete direct evidence or explicitly removed from the target by stronger source evidence. The first static and radial-saw groups begin the sequence but do not satisfy the umbrella outcome alone.

## Decisions

- Recover arenas in small behavior-complete groups, beginning with the simplest static arena needed for base calibration and then one complete radial-saw group. Do not bulk-admit 62 coarse silhouettes.
- Create child tickets for each independently reviewable group and record their dependency/provenance here; closing those children advances but does not automatically close this full-catalog umbrella.
- A preview image can establish a candidate layout but not scale, spawn, collision, or behavior. Those require direct runtime comparison.
- Keep raw ROUNDS screenshots/video external and gitignored. Commit a manifest of target build, arena/state, timestamps/frame coordinates, hashes, derived geometry/behavior measurements, and independently rendered clone evidence.
- Preserve deterministic map data in `Rounds.Sim`; Godot renders the same authoritative geometry and behavior state.

## Evidence required

- The pre-change playable pool audit identifies every current arena lacking direct evidence and prevents it from normal match selection until verified.
- Per-arena comparison manifests bind player-relative geometry, spawn/camera/kill bounds, contacts, and behavior timing to exact external target-build frames or recordings without committing those proprietary frame bytes.
- Automated simulation, catalog, presentation, and deterministic match tests cover every admitted arena and reject incomplete behavior classes.
- Full repository/replay checks, ticket checker, `git diff --check`, and monitor-4 native verification pass for each delivered arena group.
- A final catalog audit proves all 70 target rows have an evidence-backed delivered or source-excluded disposition before this umbrella closes.

## Work log

- 2026-08-29T02:31:59Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Replacing the coarse 62-map playable scaffold with a direct-evidence arena recovery order.
- 2026-08-29T02:33:55Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Required behavior-complete direct comparison before any arena enters ordinary play and limited orphan radial-saw bytes to non-authoritative implementation evidence.
- 2026-09-02T02:09:33Z — Reflection verdict: wait. The full-catalog outcome is still required, but its first evidence-backed group waits for ticket 016 to establish calibrated base behavior through ticket 031's currently blocked installed-build capture route.
