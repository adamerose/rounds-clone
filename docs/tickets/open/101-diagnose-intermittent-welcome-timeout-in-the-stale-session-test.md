---
status: blocked
created: 2026-10-17T08:50:31Z
origin: system-detected
tags: [quarrel, network, testing]
value: 4
risk: 3
sessions:
  - codex:01a148b3-4215-78f1-b9a5-4ed795348e75
  - codex:01a14920-040a-73f3-88c1-9550be47146b
execution: unattended
parent: 75
depends-on: []
supersedes: []
split-from: []
---

# Diagnose intermittent welcome timeout in the stale-session test

The unchanged raw UDP fixture in stale_session_and_invalid_hash_are_rejected sometimes times out before it tests stale sessions or invalid hashes. Establish why its welcome is missed and correct the owning boundary, keeping the intended rejection checks.

## Blocked

No causal failure was reproduced. The retained ticket95 timeout has no packet trace, and the independently identified baseline and temporarily instrumented runs all passed. There is no evidence that the original Welcome was lost, consumed, mismatched or late, and no basis to classify this as environment-only.

Resume when a failed run can retain ordered Hello/Welcome send and receive evidence, including peer addresses, nonce, session and times. The temporary instrumentation patch and passing traces are retained at `C:/Users/Adam/AppData/Local/Temp/ivy-ticket101-01a14920-040a-73f3-88c1-9550be47146b`; they are diagnostic material, not a fix. Instrumentation records send completion after the socket call, so a receive can be logged before its corresponding send-completion line.

No source correction or unfinished implementation remains. No pre-fix regression, full-verifier result for a correction, exact-candidate review or delivered SHA exists. The full verifier was conditional on a source correction. No operator product decision is requested; the missing evidence is the blocker.

## Outcome

- Identify a deterministic fixture/client sequence that consumes or misses the expected Welcome before stale-session/invalid-hash validation, then correct the owning boundary so the existing test reaches both validation assertions. Do not change production transport behavior unless the same causal boundary is reproduced outside the fixture.

## Decisions

- Destination: main. This is separate from closed95's replay mass correction; that range has no network source change.
- Preserve the host-authoritative protocol, five-second client welcome window, existing Hello retry behavior, error strings and stale-session/invalid-hash rejection semantics. A test-only fake-authority synchronization/change is preferred if it explains the failure.
- No longer timeout, blind retry, dropped assertion or test serialization is a correction. Passing reruns do not establish a fix.
- Start after97 exits and releases the sole native Cargo slot; jobs<=2 and the reusable shared out/cargo-target remain required. No clean target, linker/profile/debuginfo change or concurrent Cargo. Preserve all retained proof/root human changes.
- Serialize ancillary record edits as well as code: no other writer of docs/decisions.md or docs/postmortems.md runs concurrently. The scheduler will hold its run-journal delivery until this worker ends.

## Evidence required

- Retain ordered send/receive evidence showing packet kind, the existing session/nonce identifiers where present, sender and ordering/times. It must demonstrate the pre-fix missed/consumed Welcome rather than guessing packet loss from a timeout.
- Keep a smallest owning-boundary regression that fails before and passes after. Assert the corrected fixture still delivers both the session6 valid-hash and session7 invalid-hash snapshots and yields validation_failure at the intended checks.
- Run the focused test and, only after a source correction, the full repository verifier from their supported directories. Do not represent the earlier six passing repetitions as proof of a fix; no soak or arbitrary repeated campaign is required.
- If evidence establishes an environment-only event that cannot be reproduced, record the exact evidence limit and block this ticket without changing source or claiming the cause fixed. Merely failing to reproduce is not proof of an environment-only cause.
- Fresh non-author exact-candidate review precedes direct main integration/close; preserve earlier records and clean only the newly owned worktree.

## Work log

- 2026-10-17T08:50:31Z stage research end session codex:01a148b3-4215-78f1-b9a5-4ed795348e75 — Detected during ticket 95 final retarget at 402e1068f214d4b4f32bd8064cceebb5975ff4c5. Full verifier:184 passed,1 failed; join_timeout at live.rs:1582 before validation. Same executable failed once from workspace root and passed six times from crates/quarrel-network. Two earlier full runs passed all185. Fixture sends Welcome only once; packet loss/scheduling cause remains unproved. Full failed output retained at C:/Users/Adam/AppData/Local/Temp/ivy-ticket095-01a148b3-4215-78f1-b9a5-4ed795348e75/verify-integration.log. No production network fix inferred or made.
- 2026-10-17T09:06:51Z Contract tightened and admitted after independent /root/shape_docs conditional approval risk3: ordered packet evidence, deterministic pre-fix failure, unchanged protocol/window/retry/rejection semantics and honest environment-only limit required. All requested amendments applied; depends-on empty, no human/product decision open. Launch waits for97 and no shared-record writer will overlap.
- 2026-10-17T09:11:44Z stage research start session codex:01a14920-040a-73f3-88c1-9550be47146b — Owned detached worktree 101-welcome-timeout at 3c3a82b. Sleep/restart protection both active after resolving installed skill junction; initial alias-relative helper paths absent. Baseline sources all match retained95 final record, network executable size/write time match; focused test passes once. Retained failure contains timeout only, no packet evidence. Investigate late fixture startup, mismatched welcome identifiers/sender, or unreceived datagram with ordered boundary instrumentation; no cause inferred.
- 2026-10-17T09:14:18Z stage research end session codex:01a14920-040a-73f3-88c1-9550be47146b — No causal reproduction: baseline focused1 and network43 pass; temporary packet instrumentation focused1 and normal-concurrency network43 pass. Trace shows matching protocol17/client0/nonce Welcome session7 received before session6 valid-hash and session7 invalid-hash snapshots, then validation_failure. This neither explains prior timeout nor establishes an environment-only cause. Proof and instrumentation patch: C:\Users\Adam\AppData\Local\Temp/ivy-ticket101-01a14920-040a-73f3-88c1-9550be47146b. Restored all source bytes; no correction, full verifier or delivery review claimed. Initial instrumentation compile syntax error corrected before traced checks. No GUI, clean target, retries, timeout/serialization/assertion/protocol changes.
- 2026-10-17T09:15:18Z Blocked without source correction; owner released and guarded helper removed only clean 101-welcome-timeout worktree. No unfinished code required a checkpoint/candidate ref. All earlier worktrees/proof and root human edits preserved. Windows power request released for this session. Diagnostic proof remains under the recorded Temp directory. No delivery review or code integration performed.
