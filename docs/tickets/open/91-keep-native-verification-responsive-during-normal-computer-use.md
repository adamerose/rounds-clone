---
status: ready
owner: claude:5d7961b5-7082-42f9-9e88-f4b260740f6f
created: 2026-10-09T03:15:36Z
origin: human-request
tags: ["quarrel", "build", "performance"]
value: 9
sessions:
  - codex:01a11df8-2117-7ad1-93c1-4a57a586fedb
  - claude:5d7961b5-7082-42f9-9e88-f4b260740f6f
execution: unattended
parent: 75
depends-on: [87, 92]
supersedes: []
split-from: []
---

# Keep native verification responsive during normal computer use

Adam reports that this project's builds make his computer lag while he uses it. Measure the heavy build phases, identify the resource competition and make the supported verification route yield to interactive applications while retaining every required check.

## Outcome

- The normal verification command makes compiler, linker and test processes yield to normal-priority interactive applications through inherited BelowNormal or Idle process priority, with measured improvement in foreground responsiveness.
- Retain compilation serialization delivered by 87 and bound test execution so overlapping project verification cannot recreate the measured interference.
- A bounded trace identifies CPU, memory and disk peaks through compilation, linking and tests; the resulting correction addresses the measured bottleneck.
- Full verification, shared dependency reuse and candidate-attributed executables still work.

## Decisions

- Keep the prepared out/cargo-target and at most two Cargo jobs. Do not invalidate dependency artifacts, start a clean target or change RUSTFLAGS/profiles/features for this work.
- Prefer native process scheduling and the verification route delivered by 87 over a separate build framework.
- Allowed corrections are native CPU process priority inherited by the complete compiler/linker/test/client tree, sequencing compilation and tests, test parallelism, and a Cargo job cap of one or two. System-wide I/O or memory policy changes are outside this ticket. If a fix needs changed compiler profiles, debuginfo, RUSTFLAGS, dependencies, features or dropped checks, return to blocked for re-admission.
- No unrelated processes are stopped, reprioritized or constrained. Test and build coverage stays complete.
- Measure in an exclusive project verification window after 92: record process inventory before and after and ensure no other project Cargo/tests run. Compare the same source and warm target; the baseline is 87 delivered route at Normal priority, not the temporary manual BelowNormal worker lowering. Cold dependency builds remain unmeasured.
- The orchestrator already lowered the exact active 87 worker tree to BelowNormal as a reversible immediate measure. This is not evidence that the lag is fixed.

## Evidence required

- Record bounded before/after traces across the complete route phases using the same prepared target and source. Fixed-interval samples include CPU, process peak working set, lowest available memory, hard page faults and disk active time or queue length; report unavailable counters explicitly.
- Run a small Normal-priority foreground timer probe during both runs; report p99 and maximum wake-up delay alongside resource pressure. Use those observations to show improvement in the reported lag and identify remaining limitations without claiming an unmeasured cold-build benefit. Explain anomalously long test phases if they recur.
- Confirm the native compiler/linker/test processes spawned through the supported route have the intended priority and no concurrent Cargo builds exceed the cap.
- Run the complete verification route successfully at the chosen priority while ordinary foreground use and the Normal-priority probe continue. Wall-clock-sensitive network/menu checks must pass unchanged, with source/artifact evidence retained and no skipped checks.

## Chat excerpts

Adam — [build report](http://ivy.localhost/sessions/codex/01a11df8-2117-7ad1-93c1-4a57a586fedb), 2026-10-08:

> is it possible to improve our build process?

> 1. it lags me

## Work log

- 2026-10-09T03:15:36Z Initial snapshot found about 4 GiB free physical RAM while the active worker was idle between tests; this does not establish the heavy-phase bottleneck. Exact registered worker and descendants were lowered to BelowNormal without changing compilation inputs or cache.
- 2026-10-09T03:33:40Z Fresh admission round requested measured foreground wake-up delay, resource counters, precise scheduling scope, exclusive warm-target comparison, retained timing-sensitive checks and ordering after 92; contract amended accordingly.

- 2026-10-09T03:37:35Z Admitted after two independent contract rounds by [Claude admission context](http://ivy.localhost/sessions/claude/f53414f4-74d2-4f1a-b26f-bf6a8a50f48d), separate from the creator. Every finding resolved; evidence checkable in minutes, dependencies explicit, no operator decision open. Worker must carry one line per admission round into docs/decisions.md before code review.
- 2026-10-09T04:09:03Z stage implement start session claude:5d7961b5-7082-42f9-9e88-f4b260740f6f — worktree .ivy/worktrees/091-keep-native-verification-responsive at 34d2aae; Normal-priority baseline measurement first
- 2026-10-09T04:32:53Z Measured warm route (same source and out/cargo-target, no other project Cargo/test processes before or after) — baseline Normal: all native procs Normal, 2 concurrent links peaking 7.5 GB, available memory min 12 MB, hard-fault reads max 10123/s, disk 100%/queue 14, probe p99>5ms in 5/312 s, max 17 ms, route ~312 s. Candidate (BelowNormal, --jobs 1 for build/test --no-run), two runs: every cargo/rustc/clippy-driver/link/test/client process BelowNormal, max 1 link and 1 rustc, available memory min 1084/1726 MB, hard faults max 6261/5677/s, probe p99>5ms 9/385 and 1/319 s, max 117 ms (menu tests, unattributed, not recurring) and 61 ms (during link with Normal-priority user apps busy); link phases +91/+27 s. All 11 test binaries passed in each run, nothing skipped. Traces and harness retained under main checkout out/ticket091proof.
- 2026-10-09T04:33:07Z stage implement end session claude:5d7961b5-7082-42f9-9e88-f4b260740f6f — candidate edfe79c1e24c50c818968c16bdcfdf143f587c02
