---
format: 3
status: idea
created: 2026-10-06T02:28:31Z
origin: system-detected
tags: ["quarrel", "build", "verification"]
value: 8
sessions:
  - codex:01a10efb-9238-7e12-aecc-c488e6f1fa44
execution: unattended
depends-on: []
supersedes: []
split-from: []
---

# Verify shared Cargo artifacts belong to the current worktree

A locked workspace test can reuse binaries produced from another checkout and report success without running new tests. Prevent that false verification while retaining the shared dependency target and two-job resource cap.

## Outcome

- Verification builds and tests the current checkout's source even when another worktree populated the shared target more recently.
- Dependency artifacts remain reusable, and concurrent compilations remain serialized.

## Decisions

- Keep the configured shared target and two-job cap; this report does not authorize a clean Bevy dependency build or a new target.

## Evidence required

- Two isolated worktrees with different simulation tests use the same prepared target in succession; each workspace test executes its own test names, including after the second checkout's source timestamp predates the first build.
- The ordinary format, strict Clippy, build and test route remains supported.

## Scratch

Ticket 82's first workspace run executed 16 sim tests rather than its expected 20, omitting every arena_data::tests test. The dependency file quarrel_sim-646de60163062f7a.d named worktree 085-isolate-parallel-udp-fixtures, while cargo reported no compilation. Its network binary ran two extra tests absent from ticket 82. Refreshing only ticket 82's crate-source timestamps caused all six workspace crates to rebuild, but the full run still executed an overwritten 16-test simulation binary. Cargo releases its compilation lock before running all test executables; another worker can replace later executables while earlier tests run. A locked --no-run workspace build with compiler-artifact-time executable copies retains the intended test binaries. Evidence: root out/ticket-082/tests.log (invalid), tests-fresh.log and docs/postmortems.md. A source-identity guard or supported per-package invalidation may be sufficient; investigate before selecting an implementation.

## Work log

- 2026-10-06T02:28:31Z Reported during ticket 82 verification; no build infrastructure implementation in this ticket.
