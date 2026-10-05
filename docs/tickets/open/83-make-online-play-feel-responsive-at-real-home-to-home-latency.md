---
format: 3
status: idea
created: 2026-10-05T14:55:57Z
origin: human-request
tags: ["quarrel", "mvp", "network"]
value: 10
sessions:
  - claude:bcbe88ae-0a32-432f-8fd1-3a061e17847f
execution: unattended
parent: 75
depends-on: [78]
supersedes: []
split-from: []
---

# Make online play feel responsive at real home-to-home latency

The group mostly plays 1v1 from their own homes, and a fast twitch game like this is only fun online if your own fighter answers your input immediately.
Today a client shows the host's JSON snapshot with no prediction, so a player's own movement lags by a full round trip.

## Outcome

- Under simulated 80 ms round trip, 20 ms jitter and 2 % packet loss, the local player's own movement, shots and blocks show on the next rendered frame.
- Remote fighters, shots and loose arena pieces move smoothly, without visible snapping in normal play.
- Hits, deaths, points and card picks agree on every machine.
- Snapshots use a compact binary encoding instead of JSON.
- The transport can add configurable delay, jitter and loss for tests and for manual checks.

## Decisions

- Start with a short spike that measures, at the conditions above, client-side prediction of the local fighter with interpolation of everything else against rollback for this Rapier physics; record the numbers, the choice and the rejected option in `docs/decisions.md`, then build the choice.
  Prediction on a host-authoritative simulation is the expected answer, because rollback needs bit-identical physics on every machine and re-simulates every loose piece; the spike may overturn it with evidence.
- The host stays authoritative for hits, deaths, points and draws of card offers.

## Evidence required

- An automated headless two-client match under the simulated conditions completes, with both clients reporting the same score and card picks as the host.
- A measurement of local input to visible response under those conditions, at most one frame, recorded in the work log.
- Snapshot bandwidth per client for a 1v1 on an arena with at least 30 loose pieces, recorded in the work log and below 30 KB/s.
- The spike's comparison is in `docs/decisions.md`.

## Chat excerpts

Adam — [this session](http://ivy.localhost/sessions/claude/bcbe88ae-0a32-432f-8fd1-3a061e17847f), 2026-10-05:

> online play should be part of the MVP.

## Work log

- 2026-10-05T14:55:57Z Drafted under run #75.
