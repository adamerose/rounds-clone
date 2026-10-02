---
format: 3
status: ready
owner: codex:01a0fa9e-bca6-7c73-b44a-520349b116b2
created: 2026-10-02T03:14:55Z
origin: system-detected
tags: ["rounds", "ci", "verification"]
value: 7
risk: 2
sessions:
  - codex:01a0fa96-a468-7332-92f6-c36c3908e4b9
  - codex:01a0fa9e-bca6-7c73-b44a-520349b116b2
execution: unattended
parent: 59
depends-on: []
supersedes: []
split-from: []
---

# Run CI smoke and capture from the configured Cargo target

The Windows workflow builds with the repository's configured out/cargo-target but invokes binaries under target/debug, so its final smoke and capture steps cannot run those builds. Correct the invocation paths and ensure the existing bounded checks operate on the binaries the workflow builds.

## Outcome

- CI invokes rounds-automation and rounds-client from out/cargo-target/debug after the locked workspace build.
- The bounded two-client smoke and deterministic capture still run with their existing arguments and fail on any executable or runtime failure.

## Decisions

- Keep the repository Cargo configuration, two-job cap, pinned toolchain, existing verification coverage and bounds. Correct the caller rather than overriding the target configuration.
- Avoid new dependencies, generic CI machinery or unrelated Rust changes.

## Evidence required

- A before-change path check proves the configured output directory differs from both workflow executable paths; the corrected workflow contains no target/debug invocation outside out/cargo-target/debug.
- From the prepared target, run the exact corrected smoke and capture commands and verify successful two-client authority agreement and PNG/metadata output. Record remote CI as unavailable unless it actually runs.
- Validate workflow syntax, release-matched ticket checker and git diff --check.

## Work log

- 2026-10-02T03:14:55Z stage design start session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Read .cargo/config.toml and .github/workflows/ci.yml; both final executable paths omit the configured out/cargo-target prefix.
- 2026-10-02T03:14:55Z stage design end session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Shaped a caller-only correction that preserves all required verification and build resources.
- 2026-10-02T03:18:32Z stage review end session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9/claude-fdd0516c-4bec-42ce-915b-04f480c1cfa2 — Fresh other-family admission ADMIT at risk 2 with no blocking findings; build exact candidate before smoke/capture to avoid stale shared-target binaries.
- 2026-10-02T03:19:19Z stage implement start session codex:01a0fa9e-bca6-7c73-b44a-520349b116b2 — Isolated workflow correction; confirmed configured out/cargo-target differs from both target/debug callers.
- 2026-10-02T03:23:19Z stage implement end session codex:01a0fa9e-bca6-7c73-b44a-520349b116b2 — Corrected CI target paths and selected teal profile for the existing 180-tick bounded smoke; default timber profile requires a later explosion event.
- 2026-10-02T03:23:28Z stage verify start session codex:01a0fa9e-bca6-7c73-b44a-520349b116b2 — Verify exact workflow smoke and capture against a fresh workspace build in the prepared target.
- 2026-10-02T03:23:49Z stage verify end session codex:01a0fa9e-bca6-7c73-b44a-520349b116b2 — Fresh locked workspace build passed using prepared two-job target; 180-tick teal smoke passed two handshakes, 180 snapshots per client and all authority/render agreement flags; 30-tick capture emitted 1280x720 PNG and metadata; YAML parse, ticket checker and diff --check passed. Remote CI for the latest ticket-log tip was still in progress.
- 2026-10-02T03:24:29Z stage review start session codex:01a0fa9e-bca6-7c73-b44a-520349b116b2 — Sent exact candidate efe516aa061de53f38050b665e4b1dc451d69f35..fe21ed1f730e6aa17c54a45bf1c1dd23a2899290 for independent read-only other-family review.
- 2026-10-02T03:27:14Z stage review end session claude:4e1a9be8-a7a8-4272-97e7-2a5d67f4efd7 — APPROVE exact candidate efe516aa061de53f38050b665e4b1dc451d69f35..fe21ed1f730e6aa17c54a45bf1c1dd23a2899290. Remote CI candidate unavailable; existing GPU-test failures skip later steps on recent runs, a separate goal-59 gap.
