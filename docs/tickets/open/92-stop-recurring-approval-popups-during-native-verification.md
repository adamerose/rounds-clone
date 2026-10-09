---
status: idea
created: 2026-10-09T03:16:30Z
origin: human-request
tags: ["quarrel", "build", "windows"]
value: 8
sessions:
  - codex:01a11df8-2117-7ad1-93c1-4a57a586fedb
execution: unattended
parent: 75
depends-on: []
supersedes: []
split-from: []
---

# Stop recurring approval popups during native verification

Adam reports repeated approval popups during this project's build process even though Windows UAC is set to Never notify. Identify the actual prompt and remove the unnecessary operation that triggers it without changing the project's verification coverage.

## Outcome

- The reported popup is identified from its exact title/text and attributed to a specific build, test or launch operation.
- The supported build/verification route completes without that recurring interruption; required network behavior and checks remain supported.

## Decisions

- UAC, Windows Firewall application notifications and agent approvals are different mechanisms; do not assume which one Adam saw.
- Preserve the prepared Cargo target, two-job cap and candidate-attributed verification.
- Prefer correcting the triggering code or launch path over changing global Windows or agent security settings.
- An actual security-policy change or needed credential is outside unattended authority and requires a concrete separate decision after the cause is identified.

## Evidence required

- Preserve the exact title/text, executable path when available and triggering command. Reproduce the same notification before correction.
- Run the corrected supported route and verify the intended tests/network outcome and absence of that popup.

## Chat excerpts

Adam — [build report](http://ivy.localhost/sessions/codex/01a11df8-2117-7ad1-93c1-4a57a586fedb), 2026-10-08:

> 2. I keep getting popups for approval even though I have UAC set to Never notify

## Work log

- 2026-10-09T03:16:30Z No approval title appeared in the current main-window inventory. Computer Use initialization failed before any UI input with 'failed to write kernel assets: The system cannot find the path specified.' The exact popup is still unknown; obtain title/text or a screenshot before assuming a cause. Existing journals already document a prior network-test Windows Firewall prompt and a loopback binding correction, but do not establish this report's identity.