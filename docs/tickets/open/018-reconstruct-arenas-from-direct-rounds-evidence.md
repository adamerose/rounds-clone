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
- Child ticket `042` is the first radial-saw group split from this umbrella. It advances the catalog with one evidence-backed moving-hazard arena and result transition but does not close this ticket or inherit this umbrella's dependency on ticket `016`.
- Proposed child ticket `043` is the first yellow-crate group split from this umbrella. It covers one source-bound lattice of static platforms and physically reactive stacked pieces during a terminal impact without closing the full catalog.

## Decisions

- Recover arenas in small behavior-complete groups, beginning with the simplest static arena needed for base calibration and then one complete radial-saw group. Do not bulk-admit 62 coarse silhouettes.
- Create child tickets for each independently reviewable group and record their dependency/provenance here; closing those children advances but does not automatically close this full-catalog umbrella.
- Record `042` as the first radial-saw child. Its `split-from: [18]` provenance is informational rather than a delivery dependency, so it may advance after its own declared dependencies are met.
- Record proposed ticket `043` as the first yellow-crate/reactive-piece child. Its provenance is informational; admission and implementation remain separately reviewable.
- A preview image can establish a candidate layout but not scale, spawn, collision, or behavior. Those require direct runtime comparison.
- Keep raw ROUNDS screenshots/video external and gitignored. Commit a manifest of target build, arena/state, timestamps/frame coordinates, hashes, derived geometry/behavior measurements, and independently rendered clone evidence.
- Preserve deterministic map data and dynamic arena state in the shared `rounds-sim` Bevy 0.19.1 authority boundary; the shared presentation path renders the same authoritative geometry and snapshots for visible and offscreen output.

## Evidence required

- The pre-change playable pool audit identifies every current arena lacking direct evidence and prevents it from normal match selection until verified.
- Per-arena comparison manifests bind player-relative geometry, spawn/camera/kill bounds, contacts, and behavior timing to exact external target-build frames or recordings without committing those proprietary frame bytes.
- Automated simulation, catalog, presentation, and deterministic match tests cover every admitted arena and reject incomplete behavior classes.
- Full repository/replay checks, the shared Bevy 0.19.1 visible/offscreen presentation checks, ticket checker, `git diff --check`, and guarded monitor-4 native verification pass for each delivered arena group.
- A final catalog audit proves all 70 target rows have an evidence-backed delivered or source-excluded disposition before this umbrella closes.

## Work log

- 2026-08-29T02:31:59Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Replacing the coarse 62-map playable scaffold with a direct-evidence arena recovery order.
- 2026-08-29T02:33:55Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Required behavior-complete direct comparison before any arena enters ordinary play and limited orphan radial-saw bytes to non-authoritative implementation evidence.
- 2026-09-02T02:09:33Z — Reflection verdict: wait because the still-required full-catalog outcome's first evidence-backed group needs ticket 016 to establish calibrated base behavior through ticket 031's currently blocked installed-build capture route.
- 2026-09-04T12:40:06.869Z — Recorded ticket 042 as this umbrella's first radial-saw child while preserving the full-catalog closure boundary and ticket 042's independent dependency set.
- 2026-09-04T17:00:46.632Z — Recorded proposed ticket 043 as the first source-bound yellow-crate/reactive-piece child without changing this umbrella's full-catalog outcome.
