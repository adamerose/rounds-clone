---
format: 3
status: idea
created: 2026-10-02T03:44:36Z
origin: system-detected
tags: ["rounds", "ci", "rendering", "verification"]
value: 8
risk: 4
sessions:
  - codex:01a0fa96-a468-7332-92f6-c36c3908e4b9
execution: unattended
parent: 59
depends-on: [61]
supersedes: []
split-from: []
---

# Make hosted verification finish the real renderer checks

Hosted Windows CI fails three real renderer tests before it reaches the corrected smoke and capture commands. Diagnose the device-wait failure on the configuration CI actually runs, restore the full checks without skipping them, and stop duplicate ticket-log pushes from needlessly rebuilding unchanged product code.

## Outcome

- Required format, strict Clippy, locked build, complete locked workspace tests, two-client smoke and deterministic capture execute successfully on the existing Windows hosted configuration, with all renderer tests still enabled.
- Every rendered frame uses the shared Bevy GPU scene and still waits for complete scene/pipeline/screenshot readiness; invalid frames and actual device failures remain errors.
- Ticket-only pushes and superseded pushes do not start redundant complete product builds. Product Rust, Cargo configuration/lock/toolchain, assets, and CI changes still trigger the whole required verification; pull-request verification remains available without opening a PR for this run.

## Decisions

- GitHub Actions run36959586093 at82b641fb872723b5f8cb18151e45a269772811b7 reproduced three failures: repeated_draft_capture_waits_for_the_same_complete_scene, bevy_offscreen_renderer_captures_the_peak_builtin_shock, and yellow_sequence_uses_the_shared_gpu_scene_and_preserves_its_visual_signature. All fail at update_and_wait's device poll with 'The requested Wait timed out before the submission was completed.' The two-second device wait and fifteen-second capture budgets are independent; this names the failure boundary, not its root cause.
- First distinguish software-adapter throughput, simultaneous renderer instances, wrong device-wait semantics and actual device failure through supported public settings and bounded observations. Prefer the smallest scheduling/configuration correction that explains the reproduced failure; no swallowed error, blind retry, blanket timeout increase, disabled GPU check or invented adapter success.
- Keep the two-job Cargo cap and reuse prepared local target; serialize local Cargo/render use with other goal59 workers. Hosted ephemeral builds retain complete coverage; no local clean target is authorized merely for convenience.
- Use only this repository's current CI configuration and free existing tooling. Do not create a self-hosted runner/account, handle secrets, change host/system security or install drivers on Adam's machine. Preserve all unrelated integration-root dirt and other worktrees.
- If hosted success can only be measured after publishing the reviewed code, use the project-authorized partial-delivery path: explicitly record required hosted verification as undelivered, publish only independently approved code, retain the ticket and exact worktree until the hosted outcome passes, then close it through the guarded helper. A known failure is corrected and re-reviewed; it is not a completed delivery.
- The latest twelve inspected hosted runs were all in_progress, including repeated ticket-log-only commits. Use native workflow event filters and concurrency rather than a custom scheduler, and do not cancel unrelated runs through external control tools.

## Evidence required

- Retain exact failed-run logs, adapter/backend identity where available, failing capture boundary and timestamps; reproduce a focused real-renderer failure before changing product behavior or substantiate a configuration-only diagnosis from the hosted trace.
- A bounded supported configuration reproduces complete shared-scene captures without the prior device-wait failure. Tests protect whichever public scheduling/readiness boundary actually caused it; preserve all existing image/state signatures and process cleanup.
- Workflow syntax and trigger checks prove ticket/decision-log-only commits avoid redundant full builds, while relevant product/configuration/asset changes and PRs retain full verification. Native concurrency cancels only obsolete runs in the same workflow/ref.
- Exact-candidate sequential local format, strict Clippy, locked build/full tests, bounded smoke/capture, ticket validation and diff checks pass. The reviewed published destination must then have a completed successful hosted run for the full job; unavailable hosted success leaves this ticket open with the exact remaining failure.

## Work log

- 2026-10-02T03:44:36Z stage research start session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Read authoritative failed Actions logs and the shared renderer device-wait boundary after ticket61 published its caller correction.
- 2026-10-02T03:44:36Z stage research end session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Identified the exact two-second poll timeout failure and twelve simultaneous hosted runs; root cause remains to be established without weakening the fifteen-second readiness contract.
- 2026-10-02T03:50:01Z stage review start session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Requesting fresh independent admission of renderer failure diagnosis/full hosted verification and native duplicate-CI suppression, including explicit partial publication only while hosted evidence remains undelivered.
