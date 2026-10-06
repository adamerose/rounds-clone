---
format: 3
status: idea
created: 2026-10-06T00:49:51Z
origin: system-detected
tags: ["quarrel", "tests", "regressions"]
value: 6
sessions:
  - codex:01a10e3b-a842-7822-a150-68dd15a3b423
execution: unattended
depends-on: [78]
supersedes: []
split-from: []
---

# Restore general regression coverage after footage-profile removal

Ordinary matches retain arena validation, object contacts, jump-release handling and scripted UDP boundaries, but their earlier focused regression tests were removed along with footage tests.
Restore checks for those surviving behaviors so later tuning and content work can distinguish intentional changes from regressions.
The independent [ticket 78 reviewer](http://ivy.localhost/sessions/claude/171c00a4-8fca-43e8-bc09-eaba886a6d94) found these coverage gaps and no broken behavior; they do not reopen the delivered match contract.

## Outcome

- Focused tests reject invalid arena geometry, spawns, references and motion through `ArenaDefinition::validate`.
- Public ordinary-match tests cover resting object contacts without damage, shots pushing loose pieces, background objects without collision, and CCD stopping a one-tick thin-platform crossing.
- Jump-release regressions cover release while stunned or eliminated and transient input reset when fighters revive, where those mechanics remain current.
- Scripted UDP tests cover handshake/configuration/protocol, input sequence and tick-bound errors through bound sockets, alongside the existing successful ordinary-match smoke.

## Decisions

- Proposed follow-up only: preserve the current public behavior and use ordinary arenas and MatchConfig, without restoring footage adapters or historical snapshot anchors.
- Destination: main.
- Existing [ticket 85](http://ivy.localhost/tickets/85?repo=72104b08f3e558c1) owns parallel UDP port isolation; this ticket owns missing general boundary coverage.
- Upcoming combat/card changes may intentionally alter mechanics. Admission must reconcile expectations with that current behavior, rather than freezing retired tuning values.

## Evidence required

- Each retained assertion names a surviving invariant and exercises its owning public boundary with a small fixture.
- The locked serial workspace test command passes in minutes with no ignored tests and no clean Cargo target.
- Review compares the removed general tests at base `bb00c8dc5b4c3959b8ccb04d892a9d343f0b1258` with the delivered ordinary-match implementation, explaining any test whose behavior no longer exists.

## Scratch

The same review noted, without a runtime reproduction, that editing an inactive arena is observed only at the first fifteen-tick poll after activation. Reproduce that authoring case before proposing a fix.
UI sizing and draft/fire input concerns belong to the existing menu work; Impact tuning and leftover modifier fields belong to combat/card work, rather than expanding this coverage ticket.
The full review and retained evidence are in `out/ticket078proof/review-round2.json`.

## Work log

- 2026-10-06T00:49:51Z Recorded nonblocking coverage findings after ticket 78 was approved and published; no work admitted or implemented here.
