---
format: 3
status: ready
created: 2026-10-06T00:49:51Z
origin: system-detected
tags: ["quarrel", "tests", "regressions"]
value: 6
risk: 3
sessions:
  - codex:01a10e3b-a842-7822-a150-68dd15a3b423
execution: unattended
parent: 75
depends-on: [78, 79, 80]
supersedes: []
split-from: []
---

# Restore general regression coverage after footage-profile removal

Ordinary matches retain arena validation, object contacts, jump-release handling and scripted UDP boundaries, but their earlier focused regression tests were removed along with footage tests.
Restore checks for those surviving behaviors so later tuning and content work can distinguish intentional changes from regressions.
The independent [ticket 78 reviewer](http://ivy.localhost/sessions/claude/171c00a4-8fca-43e8-bc09-eaba886a6d94) found these coverage gaps and no broken behavior; they do not reopen the delivered match contract.

## Outcome

- Focused tests reject invalid arena geometry, spawns, references and motion through `ArenaDefinition::validate`.
- Public ordinary-match tests cover resting object contacts, shots pushing loose pieces, non-colliding background objects and CCD at a one-tick thin-platform crossing, using the current semantics after dependency delivery.
- Jump-release regressions cover release while stunned or eliminated and transient input reset when fighters are reset for the next fight, where those mechanics remain current.
- Scripted UDP tests cover handshake/configuration/protocol, input sequence and tick-bound errors through bound sockets, alongside the existing successful ordinary-match smoke.

## Decisions

- Preserve the current public behavior and use ordinary arenas and MatchConfig, without restoring footage adapters or historical snapshot anchors.
- Destination: main.
- Delivered [ticket 85](http://ivy.localhost/tickets/85?repo=72104b08f3e558c1) keeps fixture endpoints bound while peers can send to them. New UDP fixtures retain that ownership rule so they cannot send into another parallel test.
- Implementation starts from main after tickets 78, 79 and 80 have landed. The worker reconciles every Outcome with the public behavior there: assert changed surviving mechanics as currently defined, and explain omitted mechanics that no longer exist. Do not restore retired tuning or footage behavior.

## Evidence required

- Each retained assertion names a surviving invariant and exercises its owning public boundary with a small fixture.
- Both the default-parallel and serial locked workspace test commands pass in minutes with no ignored tests and no clean Cargo target. The parallel run checks that new UDP fixtures retain endpoint ownership.
- Review compares the removed general tests at base `bb00c8dc5b4c3959b8ccb04d892a9d343f0b1258` with the delivered ordinary-match implementation, explaining any test whose behavior no longer exists.

## Work log

- 2026-10-06T00:49:51Z Recorded nonblocking coverage findings after ticket 78 was approved and published; no work admitted or implemented here.

- 2026-10-07T06:29:28Z Admission round 1 by claude:0683a9e7-fb00-4d49-b97f-0361671bd371 requested changes: implementation-time reconciliation after dependencies, preservation and default-parallel verification of ticket 85 endpoint ownership, and retention of the unreproduced authoring lead before dropping Scratch. No operator decision is open.
- 2026-10-07T06:29:28Z Retained unreproduced ticket 78 review lead: editing an inactive arena may be observed only at the first fifteen-tick poll after activation. Reproduce before proposing a separate fix; it is outside this coverage contract. UI sizing and draft/fire concerns belong to menu work, and Impact tuning/modifier fields to combat/card work. Original evidence: out/ticket078proof/review-round2.json.

- 2026-10-07T06:32:44Z Admission round 2 by claude:dda50277-846a-494f-bf2e-b2cde9ee2ec5 ADMIT the complete corrected contract at risk 3, with explicit dependencies 78, 79 and 80 and no open operator decision. Every round-1 finding resolved; proposed admission decision record approved. Implementation waits for 80.
