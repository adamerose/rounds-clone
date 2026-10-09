---
status: ready
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

The supported verification command rebuilds every workspace crate on every run to prevent stale artifacts from another checkout. Reuse unchanged compatible artifacts while preserving reliable source attribution and the complete verification route.

## Outcome

- Repeating verification in the same unchanged checkout performs no workspace-crate compilation or linking in Clippy, build or test --no-run. Doctest compilation is reported separately; all frozen current-source tests still execute.
- A run never accepts artifacts built from another checkout or outdated inputs as its own. Switching worktrees, older file timestamps and foreign builds still execute the intended source and asset content.
- Each run freezes its executables before a later build can overwrite the shared target. The complete format, strict Clippy, locked build, doctest and test route remains the supported public command.

## Decisions

- Preserve 87's source-attribution and frozen-executable invariants. Reuse must be backed by current compilation inputs, checkout identity and the actual artifact identity, rather than file timestamps alone.
- Inputs include dependency-info-listed files, workspace/package manifests, lock file, Cargo configuration, toolchain identity and embedded assets. In the real workspace that includes assets/replays/ordinary-match.json and assets/tuning.ron. Missing, corrupt or incompatible attribution forces trustworthy rebuilding.
- Retain the prepared shared target, dependency cache, two-job maximum and scheduling delivered by 91. Do not change targets, profiles, features, compiler flags, RUSTFLAGS, CARGO_INCREMENTAL or the stable toolchain.
- Use small detached fixture worktrees of this repository for the adversarial sequence. They retain the real Git common directory, existing .cargo/config.toml and exact supported verification script; do not add a generic build framework or a fixture-specific verification mode.
- Update the verification synopsis and agent guidance where they promise blanket workspace rebuilding. Do not claim improved foreground responsiveness or game build timing from fixture measurements.
- If reliable reuse cannot meet this contract, block with the demonstrated gap; an unverifiable cache shortcut is not a delivery.

## Evidence required

- Two detached fixture worktrees retain the real common directory, prepared target and unchanged supported verification invocation. Their small locked workspace includes a binary named quarrel-client, an executable test identifying its source marker, and a root include_bytes! asset identifying its content. The two fixtures use the same package graph and relative paths.
- Establish fixture A, then repeat it unchanged. Cargo reports no workspace compilation or linking in Clippy, build and test --no-run; frozen tests execute A's current source-and-asset marker. Report doctest behavior and avoided compilation work separately.
- At a deterministic gate after A has frozen its test executable, finish a plain Cargo build in B before releasing A's test. A still executes its own marker. A's next verification rejects the foreign artifact and rebuilds its current content. Cargo compilation phases remain serialized.
- Verify B through the supported command, then A; A rejects the intervening artifact. Also prove rebuilding after an older-timestamp source edit, an older-timestamp embedded-asset edit, a missing attribution record and a corrupt record. Each rebuilt test executes its current source-and-asset marker.
- Check the implementation's invalidation boundary against every input and identity listed in Decisions. Record what the fixture proves and its limitation: it reproduces the shared-target freshness and freezing boundary, but does not collide with the full Bevy graph's artifact filenames.
- Run one complete actual-workspace tools/verify.ps1 verification after the matrix, using the prepared target. All checks pass with current-source frozen executables, doctests and fresh compatible dependency artifacts. Then repeat that actual-workspace command unchanged: Cargo output proves no workspace compilation or linking in Clippy, build and test --no-run, while the complete checks and frozen current-source tests still pass. Report doctest behavior separately. This second pass checks real reuse and must not perform another Bevy link; do not multiply full Bevy links to repeat the fixture matrix.

## Work log

- 2026-10-09T03:41:28Z On origin/main after 87, tools/verify.ps1 lines110-111 assign current timestamps to every crate file. Its synopsis states every workspace crate rebuilds; independent admission context reported roughly minute-long warm compile phases. This is observed repeat cost, not proof of the complete user lag cause. No implementation or admission attempted here.
- 2026-10-09T05:05:21Z Bounded fresh-context shaping by Codex /shape_cache_contract found no operator decision, but origin/main tools/verify.ps1 has no small public fixture entry point. The required foreign-writer/older-timestamp/embedded-input/corrupt-record sequence uses the whole workspace and may repeat Bevy links. Remains idea pending a meaningful minutes-scale attribution proof design; a performance benchmark cannot replace cache-safety evidence. No code, builds or cache changes performed.
- 2026-10-09T03:52:15Z Fresh [admission context](http://ivy.localhost/sessions/claude/5e954959-5fd5-46e1-bbff-f03005da8bb4) returned not-admit with six contract findings; retained in Scratch for bounded follow-up. No implementation attempted.
- 2026-10-09T06:31:29Z stage admission start session codex:01a11f02-1a85-7e42-b5c7-6b3d99c6940c — folded six prior findings into contract; exact-script detached fixture matrix plus one actual-workspace route replaces repeated full Bevy links; fresh independent context to judge completeness, checkability and boundaries, no native work
- 2026-10-09T06:34:18Z Fresh contract round1 by [cache admission context](http://ivy.localhost/sessions/codex/01a11f5c-6b6d-7911-8378-218d60dddd09), isolated child of this orchestrator, NOT ADMIT risk3: tiny fixture matrix cannot alone prove the actual workspace avoids recompilation. Added an unchanged second complete real route requiring zero workspace compilation/linking, without a second Bevy link; other-family implementation review remains separate. API session metadata confirms child identity despite inherited parent shell session variable.
