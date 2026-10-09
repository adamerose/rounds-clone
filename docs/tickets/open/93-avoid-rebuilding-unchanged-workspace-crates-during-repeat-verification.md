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
- 2026-10-09T05:05:21Z Bounded fresh-context shaping by Codex /shape_cache_contract found no operator decision, but origin/main tools/verify.ps1 has no small public fixture entry point. The required foreign-writer/older-timestamp/embedded-input/corrupt-record sequence uses the whole workspace and may repeat Bevy links. Remains idea pending a meaningful minutes-scale attribution proof design; a performance benchmark cannot replace cache-safety evidence. No code, builds or cache changes performed.
## Scratch

Fresh admission context found no duplicate, no operator decision and no needed change to another ticket record, but did not admit this contract. Before admission:

- Make unchanged repeat success exact: no workspace crate compilation or linking in Clippy/build/test --no-run; doctest compilation is an explicit exception. Full frozen current-source tests still execute. If no trustworthy design exists, block rather than close.
- Cover content and checkout identity for all compile inputs, including dependency-info-listed files, manifests/lock/config/toolchain, assets/replays/ordinary-match.json and assets/tuning.ron. Verify artifact size/write-time still match attribution.
- Add foreign plain Cargo and supported-route builds between two worktree verifications, older-timestamp source and embedded-asset edits, and missing/corrupt attribution-record evidence.
- Prove dependency artifacts remain fresh. No RUSTFLAGS, CARGO_INCREMENTAL, profile, feature or unstable -Z changes; stable toolchain stays supported.
- Compare against the post-91 route with identical source, warm target and an exclusive project build window; update synopsis and AGENTS.md when blanket rebuild wording becomes false.
- Keep proof bounded and avoid multiplying full Bevy links merely for fixture evidence. Admission must judge that the chosen public-interface regression cases fit the minutes-scale evidence rule.

A per-artifact attribution record under the existing lock may serve this without a second build framework; this is a suggestion, not an approved implementation. The immediate build interruption fixes 92 and 91 stay ahead of this shaping work.
- 2026-10-09T03:52:15Z Fresh [admission context](http://ivy.localhost/sessions/claude/5e954959-5fd5-46e1-bbff-f03005da8bb4) returned not-admit with six contract findings; retained in Scratch for bounded follow-up. No implementation attempted.
