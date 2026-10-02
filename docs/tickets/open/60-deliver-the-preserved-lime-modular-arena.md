---
format: 3
status: ready
owner: codex:01a0faa2-64cc-7f81-b3f2-12929839eecb
created: 2026-10-02T03:14:51Z
origin: human-request
tags: ["rounds", "bevy", "arenas", "fidelity", "recovery"]
value: 8
risk: 4
sessions:
  - codex:01a0fa96-a468-7332-92f6-c36c3908e4b9
  - codex:01a0faa2-64cc-7f81-b3f2-12929839eecb
execution: unattended
parent: 59
depends-on: [58]
supersedes: []
split-from: []
---

# Deliver the preserved lime modular arena

The source continues the fresh-match draft delivered by ticket 058 into a distinctive lime arena built from repeated pedestal, stem, and cross-shaped modules. Reconstruct that arena as an authority-owned collision layout with shared presentation and a bounded standalone replay, so later Homing and Parasite work can connect the new match to a real next arena instead of inventing geometry while implementing two cards.

## Outcome

- A deterministic `lime-modular-arena-replay` loads the source-observed opposing spawn region and the complete visible field of repeated upper pedestals, intermediate stems, and lower cross modules. Stable arena records provide the same contours to Rapier collision and the shared Bevy renderer; no presentation-only collision copy exists.
- Both fighters can traverse and stand on the reconstructed geometry through ordinary public input. A bounded route visits the source-observed left and right spawn shelves and at least one interior upper module without teleporting, private pose mutation, or a profile-only collision rule.
- The shared renderer reproduces the arena's dark teal paper field, deep directional shadows, cyan-to-lime faceted surfaces, visible depth ordering, and the source-framed scale at 1280 by 720. Color motion may remain presentation-only, but surface identity and collision pose come from the authoritative snapshot.
- This slice is a reusable arena, not a claim that the recorded new-match fight is connected. It adds no Homing or Parasite confirmation, card effect, health, damage-over-time, lifesteal, reload, projectile, winner, or score behavior; the existing catalog-only confirmation gate remains intact.

## Decisions

- Use `reference/MedalTVRounds20260903170709695.mp4`, SHA-256 `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9`, as the source. The interval begins after the `Ho` and `Pa` badges are visible and the draft has cleared; raw decoded frames remain ignored.
- Treat the repeated silhouettes as one finite arena definition, not a generic modular-map generator. Reuse small contour constructors only where two or more measured modules are geometrically identical.
- Derive collision from stable bright surface edges across adjacent source frames. Dark shadows, background brush texture, highlight facets, and color cycling are presentation; do not make them colliders or dynamic bodies.
- Do not infer piston motion from the module motif. A surface is static unless adjacent-frame tracking shows authoritative translation or rotation beyond capture tolerance. Any proved movement requires typed snapshot pose and matching Rapier motion; otherwise all arena bodies stay fixed.
- The source frames carry `Ho` and `Pa` badges because the recorded players selected Homing and Parasite. This ticket may render those badges only in source-comparison notes or external crops; its standalone authority uses neutral capabilities and cannot represent either catalog-only card as selected.
- Add the arena at existing simulation scale and camera projection. Do not retune fighter radius, movement, jump, projectile speed, gravity, damage, knockback, or ring-out bounds to make a scripted route convenient.

## Evidence required

- Decode a compact adjacent-frame set spanning the first complete arena view, established spawn poses, early traversal, and one later stable overview with the retained FFmpeg 7.1 two-thread exact-PTS method. Record native PTS, packed-RGBA hashes, pixel-space contours, module equivalence, spawn centers, and the static-versus-moving determination in a tracked fidelity note.
- A geometry fixture binds every authoritative surface ID and contour to those measurements within an explicit pixel/world tolerance. It fails if a measured module is missing, duplicated, mirrored incorrectly, or represented by a renderer-only collision shape.
- Public-input simulation tests prove stable support and traversal on the left spawn shelf, right spawn shelf, and an interior upper module; suppressing a required jump or movement interval must change the protected endpoint. Existing arena and combat profiles keep their outcomes.
- Shared-renderer tests and exact-candidate offscreen captures cover arena reveal, both spawn sides, interior traversal, shadows, and color treatment. Inspect every anchor at native 1280 by 720 beside the bound source frame; retain hashes and a concise source/clone comparison without committing source pixels.
- Run one guarded exact-candidate visible playback only after its window center is verified on monitor 4. Run format, strict all-target Clippy, the complete locked workspace tests, locked build, all delivered replay profiles, release-matched ticket checker, and `git diff --check` sequentially on the prepared two-job Cargo target.


## Work log

- 2026-10-02T03:14:51Z stage design start session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Found preserved unintegrated lime candidate dc9cf40fc968c44dfe169790b13e4c5227c64499 at .ivy/worktrees/059-lime-modular-arena; reuse its measured finite contract under a newly allocated ticket, preserving its original worktree and source evidence.
- 2026-10-02T03:14:51Z stage design end session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Product contract matches the former candidate; worker must drop old ticket bytes, rebuild ownership/review metadata, and independently verify exact recovered code before delivery.
- 2026-10-02T03:18:18Z stage review end session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9/claude-fdd0516c-4bec-42ce-915b-04f480c1cfa2 — Fresh other-family admission ADMIT at risk 4 with no blocking findings; preserve old candidate and remap stale ticket/decision/session attribution; renew all exact-candidate evidence.
