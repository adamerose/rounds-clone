---
status: closed
created: 2026-10-17T11:05:06Z
origin: system-detected
tags: [quarrel, automation, window, bug]
value: 7
risk: 3
sessions:
  - codex:01a148af-57cb-7751-96dc-e93972e0b69f
  - codex:01a1498b-b1b7-7952-b1eb-ec8cbd93c1bf
execution: unattended
parent: 75
depends-on: [95]
supersedes: []
split-from: []
---

# Keep automated visible-flow launches from taking focus

The automated visible-flow command opened its game window on monitor4 but became the foreground application. This interrupts the operator and prevents safe native verification.

## Outcome

Correct the owning launch boundary so public visible-flow --automated can render and finish its existing flow on monitor4 without taking foreground focus. Preserve the ordinary manually launched game's expected input/focus behavior and all existing game flow.

## Decisions

- Destination: main. This is a bounded automation-launch defect, not a new product flow or a change to game rules, protocol, simulation, tuning or normal manual launch.
- Reproduction103 used current verified executable, exact PID83648/HWND3413098 on monitor4,189 retained samples all foreground=true, then exact owned process closed/exited0 at tick10230. Raw proof root out/ticket103-refresh/resume-visible and103 helper record; no native pass claimed.
- Find the exact creation/show/focus cause before editing. Correct it before the window becomes visible; no generic global keyboard, unrelated-window focus restoration, blind delay or post-hoc focus workaround.
- Every agent-launched visible window remains hidden/unfocused until monitor4 placement is configured/verified. Fresh display and desktop capture preflight required; center on monitor4 zero-based3 before and throughout visible evidence, show without activation. Close only exact owned window/process immediately if placement/focus violates contract.
- Use supported public automated visible-flow and exact-owned native observation, no native/global input injection or external Steam invite. Native practice Escape remains separate97.
- Sole native Cargo slot, max2 jobs, reuse canonical root out/cargo-target and matching prepared dependencies. No clean target, linker/profile/debuginfo change, concurrent Cargo or needless clean rebuild. Complete normal verifier after source correction.
- Preserve root human dirty/untracked onboarding files, old94 experiment/ref/proof/cache, unfinished103 worktree/checkpoints/proof. Do not edit103's record; its original worker will resume after this delivery. No other native/shared journal writer runs concurrently.
- Record the real foreground-focus departure and recovery in docs/postmortems.md. Add no new process rule where the existing unfocused-window instruction already applies.

## Evidence required

- Retain the public reproduction and identify the exact owning source/configuration line. Establish before/after at that boundary, keeping the smallest reliable regression; if a unit regression would merely mirror configuration, use the retained exact native reproduction and explain that choice.
- Run public visible-flow --automated against the corrected attributed executable. Retain exact owned PID/HWND, starting foreground identity, fresh monitor inventory, source/content/executable identity, all placement/foreground observations and rendered opening draft, match end, run-back10 and15. No owned visible window becomes foreground; every actual visible game center stays on monitor4. Assert immediate safe abort on a violating observation, not only retrospective reporting.
- Preserve automated flow scores/cards through run-backs and successful owned-process exit. No active unrelated window, input or process is manipulated to force the result. State unavailable evidence precisely if the desktop prerequisite changes.
- Complete supported normal tools/verify.ps1 coverage (format, strict all-target Clippy, locked build, tests) with attributed artifacts, plus ticket/docs/diff checks appropriate to changed records. Supported onboard checker known legacy Markdown error remains visible; no blanket full-doc pass.
- Fresh independent exact-candidate CLI review, correction/re-review as needed, main integration/close and cleanup of only owned104 worktree.103 remains separate until its evidence contract is complete.

## Work log

- 2026-10-17T11:05:06Z103 resumed after desktop became Default and reproduced foreground focus with on-monitor4 placement. Captured bounded launch correction; no source edit by scheduler.103 checkpointce603319c515c324d377c6167ae7281c23078b30/ref refs/ivy/candidates/103-resume and clean worktree retained.
- 2026-10-17T11:07:03Z Admitted as written after independent /root/shape_docs source/contract check risk3, no operator decision. Identified application boundary create_monitor_four_window in rounds-presentation lib.rs: hidden Window uses default focused=true, mapped by Bevy/Winit to active before visibility. Shared manual/live routine means fix must apply only to automated visible-flow; retained103 proof covers failure before. Depends95 closed; sole native slot free.
- 2026-10-17T11:09:05Z Research: retained103 proof establishes foreground failure (189 visible samples). Current origin/main moved creation to quarrel-presentation/src/capture.rs; tracing the current boundary and pinned Bevy/Winit. Power helper acquired sleep=true restart=true after resolving installed skill junction; first alias-based path was absent.
- 2026-10-17T11:10:14Z Implementation: automated authority alone sets Window.focused=false at hidden creation; manual/live/menu/practice retain focused=true. Bevy0.19.1 winit_windows.rs134 passes with_active; Winit0.30.13 window.rs1324 stores MARKER_ACTIVATE, window_state.rs328-335 selects SW_SHOWNOACTIVATE for first show. Native before/after is the regression: a field-assertion unit test would only mirror configuration, not Windows activation. Full normal verifier started with canonical prepared target and existing two-job cap/one-link scheduling.
- 2026-10-17T11:23:20Z Verification complete: normal tools/verify.ps1 exit0, format/strict all-target Clippy/locked build/doctests and185 executable tests passed. Public automated visible-flow seed38/ticks18000 exit0, PID71488/HWND58791604,786 samples on monitor4 with zero foreground/placement violations; inspected opening draft, match ends5/10 and run-backs10/15 with retained scores/cards. Root out/ticket104-focus retains watcher, images, summary, executable SHA256297ec294e07483343fb09a87fb1f0f2a921a854202e5b8aa0ed9d4ef6c7ed803 and current verifier/source attribution. Supported physical onboard docs checker exit1 only known shared-root legacy Markdown error; ticket check passed. No Steam/practice-Escape claim.
- 2026-10-17T11:29:39Z Fresh exact CLI review approved3c131fd4832ed5c378f7916a937b38df8f35302f..d1a986f4a4a72423df9ecca93f3986aac99fdc94 by codex:01a1499c-4387-74f2-8d29-474b1aa5789a, no findings; receipt out/ticket104-focus/review-result.json. Reviewer independently checked attribution/786 samples/five images and reused exact185/full verification; its network refresh unavailable, supplied current ticket/goal exports used. Post-review recursive inventory found only worker review-result.json added, no other output changes, root/worktree statuses unchanged. Direct delivery to main per admitted destination/run75; current high-drain settings agree.
- 2026-10-17T11:30:52Z stage integration start session codex:01a1498b-b1b7-7952-b1eb-ec8cbd93c1bf — reviewed 3c131fd4832ed5c378f7916a937b38df8f35302f..935ce812c8c015759e37ca6bacdb57a1323baa20 as 93e980ee6e24698bee16f9f3a756ad18fbf66eda..99129e3b5056f775844bb3e9998518598ebbdd3e
- 2026-10-17T11:32:12Z Integration preparation: equivalent first rebase passed close preflight (push code first), intent recorded. Full verifier repeats per delivery policy. Git rewrote unchanged capture.rs timestamp and triggered presentation-dependent relinks despite zero source-content changes. For final metadata-only rebase, preserve only that file LastWriteTimeUtc after checking SHA256 equality before/after; full normal verifier and its content/artifact guards still run. No source, Cargo flags, dependency cache or target trust changes; this avoids a needless identical relink.
- 2026-10-17T11:42:06Z stage review end session codex:01a1499c-4387-74f2-8d29-474b1aa5789a — approved candidate 3c131fd4832ed5c378f7916a937b38df8f35302f..935ce812c8c015759e37ca6bacdb57a1323baa20
- 2026-10-17T11:42:06Z stage integration end session codex:01a1498b-b1b7-7952-b1eb-ec8cbd93c1bf — integrated 935ce812c8c015759e37ca6bacdb57a1323baa20 as 639dc5baf9e5b52f1ab7f427d65dbf73e9c57ba1
