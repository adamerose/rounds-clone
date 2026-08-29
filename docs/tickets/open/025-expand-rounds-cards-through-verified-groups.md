---
format: 3
status: idea
created: 2026-08-29T02:50:41Z
origin: human-request
tags: ["product-fidelity", "cards", "simulation"]
value: 10
risk: 4
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: [19, 20, 22]
supersedes: []
split-from: [19]
---

# Expand ROUNDS cards through verified groups

Ticket 019 verifies and safely gates the current 16-card subset, while 51 cataloged ROUNDS cards remain absent. Expand the pool through small behavior-complete groups whose mechanics, stacking, presentation, and self-play evidence all match the installed target build.

## Outcome

- Every newly selectable card has its exact ROUNDS name and directly supported single-copy, duplicate, cross-card, timing, targeting, and presentation behavior.
- A new card or combination becomes reachable only after all of its reachable behavior is verified; unresolved content stays absent or gated.
- The runtime reaches all 67 cataloged cards through independently reviewable groups without invented stat-only substitutes for behavior cards.

## Decisions

- Order groups by the smallest shared deterministic hook surface and fidelity value, not ease of approximating effects.
- Use clean-room-safe manifests with raw ROUNDS captures external, exact catalog identity, controlled comparisons, and ticket 022's headless agents for every group.
- Extend shared typed hooks only when required by the next verified group; keep Godot presentation from owning game rules.

## Evidence required

- Each group has a red-before-green fidelity matrix covering every modifier, behavior, duplicate, reachable combination, visual state, and uncertainty gate.
- Focused deterministic tests, exact draft/play integration, modified-projectile evidence where applicable, and bounded self-play exercise every delivered card and combination boundary.
- The complete 67-card pool is not claimed until all catalog rows and reachable combinations meet the same evidence bar.
- Full build, simulation/replay/history, repository, Godot, ticket, whitespace, and monitor-4 native gates pass for every group.

## Work log

- 2026-08-29T02:50:41Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Splitting card expansion after current-subset verification and headless self-play so acceptance evidence has a legal owner.
- 2026-08-29T02:50:41Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Bound all 51 remaining cards to behavior-complete groups, reachable-combination gates, direct evidence, and pre-existing self-play infrastructure.
