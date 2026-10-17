---
status: idea
created: 2026-10-17T08:50:31Z
origin: system-detected
tags: [quarrel, network, testing]
value: 4
sessions:
  - codex:01a148b3-4215-78f1-b9a5-4ed795348e75
execution: unattended
depends-on: []
supersedes: []
split-from: []
---

# Diagnose intermittent welcome timeout in the stale-session test

The unchanged raw UDP fixture in stale_session_and_invalid_hash_are_rejected sometimes times out before it tests stale sessions or invalid hashes. Preserve this lead and establish why its welcome is missed before changing a timeout or transport behavior.

## Outcome

- Identify and reproduce the missed welcome at crates/quarrel-network/src/live.rs:1582, then make the fixture reliably reach its intended validation assertions.

## Decisions

- Destination: main.
- This is a separate investigation from ticket 95's replay mass rounding correction; no network source changed in that candidate.
- Do not treat passing reruns as a fix or increase timeouts without explaining the failure.

## Evidence required

- Reproduce the timeout, explain the packet/fixture sequence, and retain a regression that tests the owning boundary.
- Run the focused test and the full repository verifier from their supported directories.

## Work log

- 2026-10-17T08:50:31Z stage research end session codex:01a148b3-4215-78f1-b9a5-4ed795348e75 — Detected during ticket 95 final retarget at 402e1068f214d4b4f32bd8064cceebb5975ff4c5. Full verifier:184 passed,1 failed; join_timeout at live.rs:1582 before validation. Same executable failed once from workspace root and passed six times from crates/quarrel-network. Two earlier full runs passed all185. Fixture sends Welcome only once; packet loss/scheduling cause remains unproved. Full failed output retained at C:/Users/Adam/AppData/Local/Temp/ivy-ticket095-01a148b3-4215-78f1-b9a5-4ed795348e75/verify-integration.log. No production network fix inferred or made.