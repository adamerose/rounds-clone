---
format: 3
status: ready
owner: codex:01a0fad0-777c-7a50-ab66-2bdbb95628fc
created: 2026-10-02T03:44:36Z
origin: system-detected
tags: ["rounds", "ci", "rendering", "verification"]
value: 8
risk: 4
sessions:
  - codex:01a0fa96-a468-7332-92f6-c36c3908e4b9
  - codex:01a0fad0-777c-7a50-ab66-2bdbb95628fc
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
- Ticket-only pushes do not start redundant complete product builds; native concurrency cancels superseded runs in the same workflow/ref. Product Rust, Cargo configuration/lock/toolchain, assets, and CI changes still trigger the whole required verification; pull-request verification remains available without opening a PR for this run.

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

- 2026-10-02T04:05:21Z stage review end session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9/claude-9ffe0981-58b2-428c-99b1-d354e4a4f75d — ADMIT at risk4 with no blockers. Folded in the clarification that concurrency cancels obsolete same-ref runs. Use a paths-ignore denylist for docs/tickets/** and docs/decisions.md only, preserve branch/tag/PR coverage, accept a successful full main run containing the approved range, and prove the concurrent-renderer hypothesis before changing scheduling or wait semantics. Hosted evidence may use the explicitly authorized partial-delivery path.
- 2026-10-02T04:13:22Z stage research start session codex:01a0fad0-777c-7a50-ab66-2bdbb95628fc — Inspecting hosted failure timing, renderer polling, and CI triggers before changing code.
- 2026-10-02T04:22:12Z stage research end session codex:01a0fad0-777c-7a50-ab66-2bdbb95628fc — Failed hosted run 36959586093 timed out three concurrent GPU tests at the two-second device poll; successful run 36961500565 passed the same tests, so software-runner load varies. Bevy's headless example waits without an inner deadline; CI scheduling is the reversible first diagnosis.
- 2026-10-02T04:22:17Z stage implement start session codex:01a0fad0-777c-7a50-ab66-2bdbb95628fc — Changed CI to one Rust test thread, ticket/decision-only push exclusion, and same-workflow/ref concurrency; recorded alternatives in goal59 decisions.
- 2026-10-02T04:22:21Z stage implement end session codex:01a0fad0-777c-7a50-ab66-2bdbb95628fc — Candidate changes only .github/workflows/ci.yml and docs/decisions.md; no renderer behavior or test disabled.
- 2026-10-02T04:22:24Z stage verify start session codex:01a0fad0-777c-7a50-ab66-2bdbb95628fc — Reused root ignored Cargo target after ticket60 closure, through a verified worktree junction; ran exact local suite and CLI paths.
- 2026-10-02T04:22:28Z stage verify end session codex:01a0fad0-777c-7a50-ab66-2bdbb95628fc — Local fmt, strict Clippy, locked build, full locked workspace tests with RUST_TEST_THREADS=1, 180-tick two-client smoke, 30-tick capture, YAML/trigger assertions, ticket validation and diff check passed. Hosted sequential result remains required.
- 2026-10-02T04:23:24Z stage review start session codex:01a0fad0-777c-7a50-ab66-2bdbb95628fc — Requesting independent read-only other-family review of exact candidate cf0a8139202f02038d9fe891d49705a328080efc..16ae1885a2b515200ff0e21840f62be83a69b504.
- 2026-10-02T04:27:40Z stage review end session codex:01a0fad0-777c-7a50-ab66-2bdbb95628fc/claude-e2dfe7f5-b8e3-4ef4-8b25-1cc2b207df68 — Independent Claude Opus APPROVE for cf0a8139202f02038d9fe891d49705a328080efc..16ae1885a2b515200ff0e21840f62be83a69b504 as genuine partial delivery; full hosted success remains undelivered. Reviewer noted the 20-minute job headroom and one green baseline among multiple failures; neither was treated as hosted proof.
