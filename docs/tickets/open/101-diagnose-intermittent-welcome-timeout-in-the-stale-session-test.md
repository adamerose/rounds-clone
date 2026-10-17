---
status: ready
owner: codex:01a14920-040a-73f3-88c1-9550be47146b
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
