---
status: ready
owner: codex:01a14b62-e353-7311-9d23-da07bc81d288
created: 2026-10-17T19:00:25Z
origin: system-detected
tags: [quarrel, cli, trace, bug]
value: 2
risk: 1
sessions:
  - codex:01a148af-57cb-7751-96dc-e93972e0b69f
  - codex:01a14b62-e353-7311-9d23-da07bc81d288
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
- Admission source snapshot origin/main7959e9408f37a71e0dc16bb8ff60a03f644e2194 and flow-reader snapshot4954ba37904af2990139adffef8fa12db2792315 differ only in ticket records. Dedicated main.rs29 uses and_then(get(index+1)), turning a missing path into None; client host has the same parser after bind and its listening event. Move trace-path validation before binding in both routes.
- Treat a following option token as a missing path rather than writing a trace to that option name. Normal paths remain accepted. Add a small per-binary required-path parser for this option if needed; existing generic argument helpers accept option tokens and do not supply this validation. Leave other argument behavior unchanged; create no new shared crate.
- Serialize behind107 because both touch client main.rs and may append shared journals. No other native/shared writer runs concurrently. Native Cargo jobs<=2, canonical shared out/cargo-target reused, no clean target/linker/profile/debug change.
- Before/after reproduction must be bounded and local. Prefer an occupied ephemeral loopback UDP port: old program reaches bind and errors there, corrected program must report missing trace path first. This proves validation precedes networking without waiting for players or adding a timeout.
- Do not expose default LAN ports, start unbounded peer waits, inject native input or open GUI. Preserve root human files and prior proof/worktrees/cache.
- No product/operator decision or external publication is needed. Quota ceiling100 is explicitly authorized by105; no paid overflow or unreviewed integration at exhaustion.

## Evidence required

- Public process-level regressions for dedicated and client host: trailing --trace and --trace followed by another option report the missing-value diagnostic before socket-bind failure, produce no listening event and no trace file.
- Add the bounded public process regressions to each binary package integration-test surface. Omitted --trace and a valid trace path remain accepted by parsing. Existing trace-format preservation coverage is network-level LiveServer::run(Some(trace_path)); no current public CLI trace test exists. Retain that network coverage and add the stated CLI cases, using stable error precedence rather than elapsed-time assertions or longer waits.
- Normal tools/verify.ps1 required checks pass with source/artifact attribution; existing destination/capture coverage and all other checks remain intact. Applicable ticket/docs/diff checks pass, known legacy Markdown error remains truthfully reported.
- Fresh nonauthor exact-candidate CLI review, main integration/close, cleanup only owned worktree. Preserve a clean checkpoint and block if quota prevents required review.

## Work log

- 2026-10-17T19:00:25Z Current-origin reflection found this independent minor developer-CLI error. Scheduled after the higher-value capture directory fix rather than expanding107 or pretending a product redesign.
- 2026-10-17T19:03:01Z Applied independent drain_runtime_reflect conditional-admission corrections: source snapshots identified as historical checked metadata-only heads, actual client parsing occurs after bind, generic helpers do not reject option tokens, and existing trace coverage is network-level rather than a nonexistent CLI test. Fresh approval requested on corrected factual contract; source scope/risk1/dependency107 unchanged.
- 2026-10-17T19:05:08Z Admitted after independent drain_runtime_reflect re-read and approval of corrected contract risk1; no operator decision. Trace parse sites/coverage now factual;107 dependency serializes shared client source and journals. Remains queued until107 exits closed.
- 2026-10-17T19:43:09Z stage research start session codex:01a14b62-e353-7311-9d23-da07bc81d288 — Own succeeded; detached current origin/main94a6c95. Sleep and restart protection active after resolving installed skill junction. Sole Cargo slot, canonical target reuse, direct main delivery.
