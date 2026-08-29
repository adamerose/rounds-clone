---
format: 3
status: ready
created: 2026-08-29T06:01:22Z
origin: human-request
tags: ["product-fidelity", "research", "infrastructure", "evidence"]
value: 9
risk: 4
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: [15]
supersedes: []
split-from: [16]
---

# Establish non-disruptive installed ROUNDS evidence capture

Direct calibration against the installed public ROUNDS build currently requires foreground input, which can steal focus or interfere with the user's mouse and work. Establish and prove a bounded capture route for controlled target states that preserves frame timing without taking over the active desktop or creating sustained host load.

## Outcome

- A documented, reproducible route reaches controlled card-draft and duel states in installed public build `21020021`, supplies repeatable player input, and captures frame-addressable output without activating the user's current desktop, moving the physical pointer, injecting global keyboard or mouse input, or inspecting unrelated application pixels.
- Every attributable target, capture, and analysis process runs below normal priority or under an equivalent explicit resource cap, has a bounded timeout and automatic cleanup, and leaves the user's foreground application responsive.
- Captured evidence records build identity, controlled state and loadout, exact frame cadence, dropped or duplicated frames, window/display placement, raw external hashes, and source frame coordinates so later fidelity tickets can distinguish target behavior from capture distortion.
- If no route can meet those constraints without enabling a Windows feature, creating a VM or account, or changing security/system configuration, ticket 031 becomes or remains blocked and presents that exact expansion for user approval instead of mutating the host. That result cannot close 031 or satisfy tickets 016 and 017; after approval, this contract is amended and re-admitted with the authorized expansion or the dependents name a separately admitted successor.

## Decisions

- Prefer process-native recording, a separate existing GUI session, or another already-available isolation boundary over Windows Sandbox or a new virtual machine. Do not enable Windows Sandbox, Hyper-V features, remote access, virtual input drivers, or new local accounts without explicit user approval.
- A project window may become visible only after its exact center is verified on monitor 4, zero-based screen index 3. The route must not switch the user's active desktop or maximize a project window on monitors 1 through 3.
- Background `PostMessage`, `SendInput`, WScript, and mouse-event attempts already failed to drive the Unity menu; do not count repetition of those methods as a capture route unless new evidence shows a materially different boundary.
- Same-PC capture is invalid when its cadence audit shows dropped, duplicated, or delayed target frames outside the declared tolerance. Lower resolution or a shorter bounded sample is preferable to unmeasured lag.
- Each evidence sample is capped at 1280 by 720, 60 captured frames per second, and 20 seconds of recording. Run at most the target plus one capture writer during that interval; perform frame extraction and analysis only after both measurements finish.
- Before target launch, collect a 10-second idle baseline from an independent 20-millisecond scheduling heartbeat. During capture, the heartbeat passes only when its 95th-percentile delay is at most 5 milliseconds above baseline and at most 15 milliseconds absolute, with no delay above 50 milliseconds. A cadence-valid capture still fails when this host-impact gate fails.
- Record one-second process and system CPU, GPU-engine, dedicated GPU-memory, and private-memory samples for the baseline and capture. Constrain attributable CPU work to at most two logical processors and two GiB private memory; reject any run that exceeds 70 percent total GPU-engine utilization or two GiB attributable dedicated GPU memory rather than hiding the load behind below-normal CPU priority.
- Keep raw proprietary target frames external and gitignored. Commit only clean-room measurements, hashes, coordinates, manifests, scripts that operate on public behavior, and independently generated clone evidence.

## Evidence required

- A monitor-4 proof records the exact target window geometry, non-activation/input boundary, process priorities and limits, source cadence, dropped/duplicate-frame audit, automatic exit or exact cleanup, and absence of residual attributable processes.
- The proof includes the idle and capture heartbeat distributions plus the one-second CPU, GPU, GPU-memory, and private-memory samples, and fails on any declared duration, resolution, frame-rate, concurrency, affinity, memory, GPU, or responsiveness limit.
- Two repeated controlled target runs produce compatible frame-addressable measurements within their declared tolerance while the user's foreground input and pointer remain untouched.
- A negative test proves the route refuses wrong-screen placement, foreground/global-input fallback, missing resource limits, and evidence overwrite rather than silently weakening isolation.
- Repository checks, the ticket checker, and `git diff --check` pass; no raw ROUNDS frame, video, executable, or proprietary asset is committed.

## Work log

- 2026-08-29T06:01:22Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Recorded the user's no-mouse-takeover/no-lag requirement as the owner for installed-build evidence infrastructure after background Unity input methods proved insufficient.
- 2026-08-29T06:02:16Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Bound the route to already-available isolation first, explicit approval before host changes, monitor-4 placement, resource/cadence audits, refusal tests, external raw evidence, and an honest stop when no non-disruptive route exists.
- 2026-08-29T06:07:01Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Making an unavailable route a blocking result rather than a successful close and adding objective host-impact limits for the user's no-lag requirement.
- 2026-08-29T06:07:48Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Required blocked status when isolation needs new authority and bound capture resolution, duration, concurrency, CPU, GPU, memory, cadence, and independent heartbeat latency with explicit refusal thresholds.
- 2026-08-29T06:09:17Z stage admission start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04bf9-57f9-7fa3-b2a8-63710fa3769e — Cold-reading the non-activating input boundary, monitor placement, no-host-mutation rule, resource and latency gates, refusal paths, cleanup, blocking behavior, and risk-4 bar.
- 2026-08-29T06:12:09Z stage admission end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04bf9-57f9-7fa3-b2a8-63710fa3769e — Admitted at risk 4 with no findings after the route gained objective no-lag thresholds, exact negative refusals, and a dependency-safe blocked result when new user authority is required.
