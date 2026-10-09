---
status: idea
created: 2026-10-09T02:27:28Z
origin: system-detected
tags: ["quarrel", "network", "tests"]
value: 5
sessions:
  - codex:01a11df8-2117-7ad1-93c1-4a57a586fedb
execution: unattended
parent: 75
depends-on: [87]
supersedes: []
split-from: []
---

# Explain the intermittent late-peer menu join failure

The final card verification once failed the late-peer menu fixture at its five-second join window, then the focused test, three public reproductions and complete test matrix passed unchanged. Identify the failing boundary and remove the cause so successful verification is reliable rather than dependent on a rerun.

## Outcome

- The retained failure is explained by a specific code or fixture boundary, with a regression or exact bounded reproduction that distinguishes the cause from unrelated startup variation.
- The late-peer menu test reliably checks the intended peer-wait behavior using the current checkout's frozen executables.

## Decisions

- Preserve interactive host/join waiting until cancelled and the automation route's explicit finite limits.
- Do not lengthen timeouts, add blind retries or suppress errors to hide the symptom.
- Reuse the prepared Cargo target and two-job cap; depend on ticket 87's attributable verification route.
- Determine whether the defect is in the fixture or supported product behavior before choosing a correction. No cause is established by a successful rerun.

## Evidence required

- Inspect the rejected late-peer output and retained timed reproductions in out/ticket080proof; identify the owning test and exact observed failure.
- Establish a bounded reproduction or deterministic boundary regression before correction; record why a larger stress run is unnecessary or file it separately.
- The corrected targeted test and repository verification route pass with candidate-attributed executables and no skipped tests.

## Work log

- 2026-10-09T02:27:28Z Reported from ticket 80's final publication record. Its unchanged focused test, three timed public reproductions and full matrix passed after one failure; the cause remains unproven. Closed tickets 74, 81, 85 and 89 cover earlier lifecycle/port issues, not this intermittent final fixture failure.