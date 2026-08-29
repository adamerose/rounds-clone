---
format: 3
status: ready
created: 2026-08-29T02:31:59Z
origin: human-request
tags: ["product-fidelity", "simulation", "combat"]
value: 10
risk: 4
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: [15, 30, 31]
supersedes: []
split-from: []
---

# Match base ROUNDS movement and combat feel

The current simulation promotes several provisional footage estimates into gameplay beyond the projectile-speed derivation corrected by ticket 030. Recalibrate the remaining base movement, shooting, blocking, damage, and timing against the installed target ROUNDS build so the clone is a measured faithful subset rather than deterministic scaffolding.

## Outcome

- A reproducible direct-comparison capture covers base-isolating run acceleration/speed, jump height/time, gravity/fall, projectile collision radius, fire interval, reload, damage, recoil, block active/recovery/cooldown timing, knockback, and out-of-bounds timing in both ROUNDS and the clone.
- The clone matches each captured quantity within an evidence-derived tolerance and confirms that ticket 030's corrected projectile speed remains consistent with the installed build.
- Research facts, architecture, simulation tuning, tests, and replay contracts agree on the corrected values. Unmeasured behavior remains explicitly unresolved rather than receiving an invented replacement.

## Decisions

- Use the installed public ROUNDS build `21020021` as the primary oracle. Public captured behavior is authoritative over the old single-sample low-confidence estimates.
- Public ROUNDS requires an opening card, so isolate each quantity across multiple loadouts whose visible sourced effects omit that quantity. Accept a base value only when those samples agree within the declared tolerance; complete card-mechanics verification remains with ticket 019 and is not a prerequisite here.
- Ticket 030 owns the known six-frame projectile-speed derivation error and its immediate correction. This ticket confirms that corrected speed against the installed build but does not reopen the already evidenced arithmetic fix.
- Ticket 031 owns the bounded, low-priority, non-activating and input-isolated installed-build capture route. Do not substitute global input injection, foreground activation, unbounded recording, or uncapped capture load when that prerequisite is unavailable.
- Normalize spatial measurements by visible player diameter and time by captured frames/ticks, but preserve raw frame-addressable observations so unit-conversion mistakes can be audited.
- Keep raw ROUNDS captures external and gitignored. Commit only a manifest of source/build, controlled state, timestamps/frame coordinates, hashes, derived measurements, tolerances, and independently generated clone evidence.
- Change golden replay hashes only through the existing intentional-break record, naming each corrected base behavior. Do not preserve a wrong hash at the cost of fidelity.
- Exclude a sample whenever any visible opening card lists or is directly observed changing the measured quantity. Neutral-for-that-metric loadouts are required calibration controls, while card-modified comparisons belong to ticket 019.
- Preserve the repository's no-`spec/`-plus-`src/` commit boundary while treating embedded binding specs as runtime data. When a binding spec changes behavior, first land green measurement/schema/checker evidence without changing the binding value, then land any behavior-neutral source/test preparation separately, and only then land the behavior-changing binding-spec update with its replay/golden/intentional-break consequences and no `src/` paths.

## Evidence required

- Deterministic comparison fixtures fail on any remaining out-of-tolerance base values, then pass after correction against the committed evidence manifest and externally retained raw observations.
- At least two controlled samples cover each timing or spatial quantity where the target build permits repeatable setup; single-sample evidence is labeled and receives a wider explicit tolerance.
- Focused and full simulation/replay suites, zero-warning build, repository checks, ticket checker, and `git diff --check` pass with every intentional replay change recorded.
- Native evidence for both applications is collected only after each exact window center is verified on monitor 4; no project window is shown on monitors 1 through 3.

## Work log

- 2026-08-29T02:31:59Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Filing direct installed-build calibration for the user-reported projectile-speed mismatch and the other provisional base-feel values.
- 2026-08-29T02:33:55Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Bound the base-feel correction to raw frame-addressable installed-build evidence, explicit tolerances, intentional replay-break records, and a red-before-green projectile-speed comparison.
- 2026-08-29T05:51:48Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Split the already-proven public-footage projectile frame-span correction into the ticket later numbered 030, corrected the unreachable no-card premise, and retained installed-build calibration of every remaining base-feel quantity here.
- 2026-08-29T06:02:16Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Renumbered the speed correction to 030, assigned non-disruptive installed capture to prerequisite 031, removed circular card verification, and made metric-neutral multi-loadout agreement plus the spec/runtime commit boundary explicit.
- 2026-08-29T06:07:01Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Correcting the impossible spec-before-runtime sequence after review confirmed binding combat specs are embedded runtime data.
- 2026-08-29T06:07:48Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Defined an embedded-runtime-safe green sequence that preserves the no-spec-plus-src commit boundary and carries replay consequences with each binding-spec behavior change.
- 2026-08-29T06:09:17Z stage admission start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04bf9-57f9-7fa3-b2a8-63710fa3769e — Cold-reading the split base-feel contract, multi-loadout isolation, safe-capture dependency, embedded-runtime commit sequence, and risk-4 evidence boundary.
- 2026-08-29T06:12:09Z stage admission end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04bf9-57f9-7fa3-b2a8-63710fa3769e — Admitted at risk 4 with no findings after the projectile correction, safe-capture infrastructure, remaining base-feel ownership, and green commit boundaries became dependency-safe and judgeable.
