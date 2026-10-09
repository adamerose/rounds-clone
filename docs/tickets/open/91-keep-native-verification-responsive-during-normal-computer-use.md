---
status: idea
created: 2026-10-09T03:15:36Z
origin: human-request
tags: ["quarrel", "build", "performance"]
value: 9
sessions:
  - codex:01a11df8-2117-7ad1-93c1-4a57a586fedb
execution: unattended
parent: 75
depends-on: [87]
supersedes: []
split-from: []
---

# Keep native verification responsive during normal computer use

Adam reports that this project's builds make his computer lag while he uses it. Measure the heavy build phases, identify the resource competition and make the supported verification route yield to interactive applications while retaining every required check.

## Outcome

- The normal verification command runs native build processes at background priority and serializes native compilation across project worktrees.
- A bounded trace identifies CPU, memory and disk peaks through compilation, linking and tests; the resulting correction addresses the measured bottleneck.
- Full verification, shared dependency reuse and candidate-attributed executables still work.

## Decisions

- Keep the prepared out/cargo-target and at most two Cargo jobs. Do not invalidate dependency artifacts, start a clean target or change RUSTFLAGS/profiles/features for this work.
- Prefer native process scheduling and the verification route delivered by 87 over a separate build framework.
- No unrelated processes are stopped, reprioritized or constrained. Test and build coverage stays complete.
- The orchestrator already lowered the exact active 87 worker tree to BelowNormal as a reversible immediate measure. This is not evidence that the lag is fixed.

## Evidence required

- Record a bounded before/after build trace using the shared prepared target, with peak memory and CPU/disk activity at the heavy phases. Identify what explains the reported interference and what remains unmeasured.
- Confirm the native compiler/linker/test processes spawned through the supported route have the intended priority and no concurrent Cargo builds exceed the cap.
- Run the complete verification route successfully, with its source/artifact evidence retained and no skipped checks.

## Chat excerpts

Adam — [build report](http://ivy.localhost/sessions/codex/01a11df8-2117-7ad1-93c1-4a57a586fedb), 2026-10-08:

> is it possible to improve our build process?

> 1. it lags me

## Work log

- 2026-10-09T03:15:36Z Initial snapshot found about 4 GiB free physical RAM while the active worker was idle between tests; this does not establish the heavy-phase bottleneck. Exact registered worker and descendants were lowered to BelowNormal without changing compilation inputs or cache.