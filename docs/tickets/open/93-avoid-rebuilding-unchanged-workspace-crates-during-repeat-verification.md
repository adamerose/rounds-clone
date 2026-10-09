---
status: idea
created: 2026-10-09T03:41:28Z
origin: agent-proposed
tags: ["quarrel", "build", "performance"]
value: 7
sessions:
  - codex:01a11df8-2117-7ad1-93c1-4a57a586fedb
execution: unattended
parent: 75
depends-on: [87, 91]
supersedes: []
split-from: []
---

# Avoid rebuilding unchanged workspace crates during repeat verification

The supported verification route currently touches every crate source and rebuilds all six workspace crates on every run to prevent stale artifacts from another checkout. Investigate a smaller invalidation boundary that retains trustworthy source attribution while avoiding needless compilation and Bevy links on an unchanged repeat run.

## Outcome

- An unchanged repeat verification reuses compatible workspace artifacts without accepting another checkout's source as its own.
- Switching between different worktrees, including older source timestamps, still executes the correct tests and freezes each run's executables before another build can overwrite them.
- The complete format, strict Clippy, locked build, doctest and test route remains the supported public command.

## Decisions

- Preserve 87's demonstrated source-attribution and concurrent-build safety invariants; investigate before selecting an invalidation design.
- Retain the prepared target and dependency cache, two-job maximum, scheduling from 91 and complete checks. No new target, compiler profile, feature or flag changes.
- This is an idea, separate from 91's frozen scheduling contract. 91 may finish its measured responsiveness correction before this optimization is admitted.

## Evidence required

- Compare bounded repeated warm-route timing and Cargo fresh/rebuilt messages on unchanged source before and after, identifying compilation/link work avoided.
- Re-run 87's two-source older-timestamp and build-during-frozen-tests evidence, plus the full supported route; maintain its exact current-source test names.
- Explain every source/input change that invalidates reuse and any remaining attribution limitations. An unverifiable cache shortcut does not meet the outcome.

## Work log

- 2026-10-09T03:41:28Z On origin/main after 87, tools/verify.ps1 lines110-111 assign current timestamps to every crate file. Its synopsis states every workspace crate rebuilds; independent admission context reported roughly minute-long warm compile phases. This is observed repeat cost, not proof of the complete user lag cause. No implementation or admission attempted here.