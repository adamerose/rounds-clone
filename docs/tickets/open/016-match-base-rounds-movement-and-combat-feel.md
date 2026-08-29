---
format: 3
status: idea
created: 2026-08-29T02:31:59Z
origin: human-request
tags: ["product-fidelity", "simulation", "combat"]
value: 10
risk: 4
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: [15]
supersedes: []
split-from: []
---

# Match base ROUNDS movement and combat feel

The current simulation promotes several provisional footage estimates into gameplay, and the user directly reports that bullets travel much too fast. Recalibrate the no-card duel against the installed target ROUNDS build so the clone's base movement, shooting, blocking, damage, and timing are a measured faithful subset rather than deterministic scaffolding.

## Outcome

- A reproducible direct-comparison capture covers no-card run acceleration/speed, jump height/time, gravity/fall, projectile travel, projectile radius, fire interval, reload, damage, recoil, block active/recovery/cooldown timing, knockback, and out-of-bounds timing in both ROUNDS and the clone.
- The clone matches each captured quantity within an evidence-derived tolerance, with the user's projectile-speed complaint represented by a failing comparison before the correction and a passing comparison afterward.
- Research facts, architecture, simulation tuning, tests, and replay contracts agree on the corrected values. Unmeasured behavior remains explicitly unresolved rather than receiving an invented replacement.

## Decisions

- Use the installed public ROUNDS build `21020021` as the primary oracle. Public captured behavior is authoritative over the old single-sample low-confidence estimates.
- Normalize spatial measurements by visible player diameter and time by captured frames/ticks, but preserve raw frame-addressable observations so unit-conversion mistakes can be audited.
- Keep raw ROUNDS captures external and gitignored. Commit only a manifest of source/build, controlled state, timestamps/frame coordinates, hashes, derived measurements, tolerances, and independently generated clone evidence.
- Change golden replay hashes only through the existing intentional-break record, naming each corrected base behavior. Do not preserve a wrong hash at the cost of fidelity.
- Keep cards out of the calibration scenes. Card-modified comparisons belong to ticket 019.

## Evidence required

- A deterministic comparison fixture fails on the current base projectile speed and any other out-of-tolerance base values, then passes after correction against the committed evidence manifest and externally retained raw observations.
- At least two controlled samples cover each timing or spatial quantity where the target build permits repeatable setup; single-sample evidence is labeled and receives a wider explicit tolerance.
- Focused and full simulation/replay suites, zero-warning build, repository checks, ticket checker, and `git diff --check` pass with every intentional replay change recorded.
- Native evidence for both applications is collected only after each exact window center is verified on monitor 4; no project window is shown on monitors 1 through 3.

## Work log

- 2026-08-29T02:31:59Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Filing direct installed-build calibration for the user-reported projectile-speed mismatch and the other provisional base-feel values.
- 2026-08-29T02:33:55Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Bound the base-feel correction to raw frame-addressable installed-build evidence, explicit tolerances, intentional replay-break records, and a red-before-green projectile-speed comparison.
