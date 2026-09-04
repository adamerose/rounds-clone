---
format: 3
status: ready
created: 2026-09-04T22:56:47Z
origin: human-request
tags: ["build", "project-maintenance", "rust", "verification"]
value: 9
risk: 2
sessions:
  - codex:01a06920-7449-74d0-9b09-57855a012572
execution: unattended
depends-on: []
supersedes: []
split-from: []
---

# Bound Cargo resource use without reducing verification

A valid cold Bevy workspace test launched about seven MSVC linkers, created roughly 16.9 GiB of artifacts, drove memory to 99% and disk to 100%, and froze the development host and Codex. Keep complete native verification while making its resource cost bounded, visible before launch, and reusable across compatible commands.

## Outcome

- Repository Cargo configuration caps builds at two concurrent jobs and uses one ignored worktree-local `out/cargo-target` so compatible test, lint, build, capture, and review commands reuse prepared artifacts.
- Project agent guidance forbids upward job overrides and concurrent Cargo commands, defines when a clean target is justified, and requires a user-visible impact report before every clean native build.
- The impact report names the exact target, why reuse is invalid, the two-job cap, the measured approximately 17 GiB cold-build precedent, and the cleanup plan.
- Full required test, lint, locked build, capture, and review coverage remains unchanged. Resource safety cannot be implemented by skipping evidence or substituting a narrower check.
- The confirmed ticket-043 incident, cause, measured impact, and mitigation are appended to the durable postmortem ledger.

## Decisions

- Cap Cargo jobs in repository configuration rather than relying on each agent to remember a command-line flag. Two concurrent compile/link jobs bound the measured seven-linker fan-out while retaining parallel compilation.
- Keep the reusable target under ignored `out/` and local to each worktree. Never share one writable Cargo target across concurrent worktrees or machines.
- Reuse is valid only when the toolchain, lock file, feature set, target triple, and artifact trust are compatible. A contract that explicitly requires an absent target still permits a cold build, but only after the required impact report and with the repository job cap.
- Run sequential Cargo commands against the prepared target. The two-job cap does not make simultaneous Cargo invocations safe.

## Evidence required

- TOML parsing proves `.cargo/config.toml` sets `build.jobs = 2` and `build.target-dir = "out/cargo-target"`.
- A clean native workspace verification announces its expected impact before launch, runs with no more than two Cargo/rustc/link jobs attributable to the build at once, and creates artifacts only beneath the configured ignored target.
- The same prepared target then runs the complete locked workspace tests, format check, strict all-target lint, locked build, required GPU captures, and network smoke without cleaning or starting another Cargo target.
- Process sampling and final residue inspection record peak attributable compiler/linker concurrency, target size, command outcomes, and cleanup. No concurrent Cargo command or worktree `target/` directory appears.
- Ticket validation, configuration parsing, `git diff --check`, and review confirm that no verification requirement was removed or weakened.

## Work log

- 2026-09-04T22:56:47Z stage design start session codex:01a06920-7449-74d0-9b09-57855a012572 — Converted the confirmed ticket-043 host freeze into a bounded build-resource contract that preserves the complete native verification surface.
- 2026-09-04T22:56:47Z stage design end session codex:01a06920-7449-74d0-9b09-57855a012572 — Human direction admitted the risk-2 contract with a repository two-job cap, compatible target reuse, pre-clean impact reporting, measured process evidence, and no test reduction.
- 2026-09-04T22:56:47Z stage implement start session codex:01a06920-7449-74d0-9b09-57855a012572 — Added repository Cargo limits, durable agent guidance, and the measured resource-incident record before the next native build.
- 2026-09-04T23:00:20Z stage implement end session codex:01a06920-7449-74d0-9b09-57855a012572 — Committed the repository two-job cap, reusable ignored target, pre-clean impact-report rule, human-admitted ticket, decision, and durable incident record before launching another native build.
- 2026-09-04T23:06:55Z stage verify start session codex:01a06920-7449-74d0-9b09-57855a012572 — Announced the exact absent `out/cargo-target`, two-job cap, approximately 17 GiB cold-build precedent, reuse plan, and scoped final cleanup before launching the unchanged full locked workspace test.
- 2026-09-04T23:31:42Z stage verify end session codex:01a06920-7449-74d0-9b09-57855a012572 — The capped cold test compiled in 6m16s, followed by the complete 37-test rerun, format, strict all-target Clippy, locked build, eleven-anchor GPU capture, exact-source audit, two-client smoke, and guarded playback against the same retained target. Sampling observed one user Cargo invocation, at most two rustc workers and two linker children occupying the same two Cargo job slots, a brief 14.5 GiB attributable working-set peak, and 11.4 of 31.7 GiB physical memory still free afterward; Clippy's nested `cargo check` was proven by process ancestry to be internal rather than a concurrent invocation. The sole target reached 17,089,721,948 bytes, no worktree `target/` appeared, and no build or game process remained.
