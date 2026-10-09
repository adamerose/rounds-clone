---
status: ready
created: 2026-10-09T02:27:28Z
origin: system-detected
tags: ["quarrel", "network", "tests"]
value: 5
sessions:
  - codex:01a11df8-2117-7ad1-93c1-4a57a586fedb
execution: unattended
parent: 75
depends-on: [87, 91]
supersedes: []
split-from: []
---

# Explain the intermittent late-peer menu join failure

The final card verification once failed the late-peer menu fixture at its five-second join window, then the focused test, three public reproductions and complete test matrix passed unchanged. Identify the failing boundary and remove the cause so successful verification is reliable rather than dependent on a rerun.

## Outcome

- The retained failure is explained by a specific code or fixture boundary, with a regression or exact bounded reproduction that distinguishes the cause from unrelated startup variation.
- A deterministic boundary regression or bounded manual reproduction identifies the event that starts the host/peer deadline and distinguishes an incorrect fixture deadline from supported menu behavior. It fails on the retained base and passes after correction, using candidate-attributed executables.

## Decisions

- Preserve interactive host/join waiting until cancelled and the automation route's explicit finite limits.
- Do not lengthen timeouts, add blind retries or suppress errors to hide the symptom.
- Reuse the prepared Cargo target and two-job cap; depend on ticket 87's attributable verification route.
- Determine whether the defect is in the fixture or supported product behavior before choosing a correction. No cause is established by a successful rerun.

## Evidence required

- Inspect the rejected late-peer output and retained timed reproductions in out/ticket080proof; identify the owning test and exact observed failure.
- Establish a bounded reproduction or deterministic boundary regression before correction; record why a larger stress run is unnecessary or file it separately.
- Record failure-before/pass-after evidence for the exact boundary regression or manual reproduction, including its finite deadline. Keep the existing menu fixture targeted run: set QUARREL_TEST_CLIENT to out/verify/test/quarrel-client.exe and run the frozen out/verify/test/menu_network-*.exe with --exact bounded_host_client_waits_for_a_peer_joining_after_the_silence_interval --nocapture. Its host waits five seconds for the initial peer, and the fixture launches that peer 3500 ms after the listening event.
- Run powershell -NoProfile -ExecutionPolicy Bypass -File tools/verify.ps1 after dependency 91 lands. Format, strict all-target Clippy, locked build, doctests and every frozen test must pass with no skipped tests, prepared-target reuse and candidate attribution. This full delivery check may take several minutes; the small boundary reproduction is the checkable contract evidence. File any additional stress or soak benchmark separately.

## Work log

- 2026-10-09T02:27:28Z Reported from ticket 80's final publication record. Its unchanged focused test, three timed public reproductions and full matrix passed after one failure; the cause remains unproven. Closed tickets 74, 81, 85 and 89 cover earlier lifecycle/port issues, not this intermittent final fixture failure.
- 2026-10-09T04:58:54Z Admission round1 by fresh Codex context /admit_late_peer requested exact targeted commands, finite deadlines and measurable failure-before/pass-after evidence; retained rejected and timed logs available. Contract amended, ordering after91 explicit; no operator decision open.

- 2026-10-09T05:01:11Z Admission round2 by fresh Codex context /admit_late_peer approves amended contract at risk3: exact frozen target/deadline, failure-before/pass-after proof, full verification route and explicit87/91 dependencies resolved all findings. No operator decision open. Worker must record both rounds in docs/decisions.md before code review.
- 2026-10-09T06:06:24Z Both admission rounds now recorded in docs/decisions.md on origin/main at389137397560efd14b2e9455d85cc9d08566549f. Fresh child identity resolved to codex:01a11f02-1a85-7e42-b5c7-6b3d99c6940c/01a11f05-5ed0-7951-8091-dd95385c6a31; documentation independently exact-range approved by Claude9d7cc8cf. Future implementation need not duplicate the record; admission is not implementation approval.
