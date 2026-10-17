---
status: idea
created: 2026-10-17T19:00:25Z
origin: system-detected
tags: [quarrel, cli, trace, bug]
value: 2
risk: 1
sessions:
  - codex:01a148af-57cb-7751-96dc-e93972e0b69f
execution: unattended
parent: 105
depends-on: [107]
supersedes: []
split-from: []
---

# Reject missing host trace paths before opening sockets

The dedicated server and client host silently treat a trailing --trace flag as if no trace was requested. This can start a session without the diagnostic recording the caller expected.

## Outcome

When --trace is present in either hosting CLI, require its path value before opening sockets or starting the session. Preserve optional tracing when the flag is absent, valid path handling and the trace format.

## Decisions

- Destination: main. Small argument-validation correction; no new CLI framework, protocol, session behavior, recording format or game flow.
- Current origin/main4954ba37904af2990139adffef8fa12db2792315: dedicated main.rs29 and client main.rs456 use and_then(get(index+1)), turning missing path into None. Adjacent required-value parsing rejects missing values for other hosting flags.
- Treat a following option token as a missing path rather than writing a trace to that option name. Normal paths remain accepted; use existing per-binary argument helpers where practical, not a new shared crate.
- Serialize behind107 because both touch client main.rs and may append shared journals. No other native/shared writer runs concurrently. Native Cargo jobs<=2, canonical shared out/cargo-target reused, no clean target/linker/profile/debug change.
- Before/after reproduction must be bounded and local. Prefer an occupied ephemeral loopback UDP port: old program reaches bind and errors there, corrected program must report missing trace path first. This proves validation precedes networking without waiting for players or adding a timeout.
- Do not expose default LAN ports, start unbounded peer waits, inject native input or open GUI. Preserve root human files and prior proof/worktrees/cache.
- No product/operator decision or external publication is needed. Quota ceiling100 is explicitly authorized by105; no paid overflow or unreviewed integration at exhaustion.

## Evidence required

- Public process-level regressions for dedicated and client host: trailing --trace and --trace followed by another option report the missing-value diagnostic before socket-bind failure, produce no listening event and no trace file.
- Omitted --trace and a valid trace path remain accepted by parsing; retain existing trace/host tests and verify no recording format change. Use stable error precedence rather than elapsed-time assertions or longer waits.
- Normal tools/verify.ps1 required checks pass with source/artifact attribution; existing destination/capture coverage and all other checks remain intact. Applicable ticket/docs/diff checks pass, known legacy Markdown error remains truthfully reported.
- Fresh nonauthor exact-candidate CLI review, main integration/close, cleanup only owned worktree. Preserve a clean checkpoint and block if quota prevents required review.

## Work log

- 2026-10-17T19:00:25Z Current-origin reflection found this independent minor developer-CLI error. Scheduled after the higher-value capture directory fix rather than expanding107 or pretending a product redesign.