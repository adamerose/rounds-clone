---
format: 3
status: idea
created: 2026-10-02T03:14:55Z
origin: system-detected
tags: ["rounds", "ci", "verification"]
value: 7
risk: 2
sessions:
  - codex:01a0fa96-a468-7332-92f6-c36c3908e4b9
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