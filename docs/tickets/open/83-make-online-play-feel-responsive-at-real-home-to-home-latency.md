---
format: 3
status: ready
owner: codex:01a10fa3-aece-76f3-9658-406cb0d99123
created: 2026-10-05T14:55:57Z
origin: human-request
tags: ["quarrel", "mvp", "network"]
value: 10
sessions:
  - claude:bcbe88ae-0a32-432f-8fd1-3a061e17847f
  - codex:01a10fa3-aece-76f3-9658-406cb0d99123
execution: unattended
parent: 75
depends-on: [78, 79, 88]
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

## Decisions

- Build the online model chosen and recorded by #88 in `docs/decisions.md`.
- The host stays authoritative for hits, deaths, points and draws of card offers.
- #74 is open and touches the network crate; this ticket must not break it.

## Evidence required

- An automated headless two-client match under the simulated conditions completes, with both clients reporting the same score and card picks as the host.
- A measurement of local input to visible response under those conditions, at most one frame, recorded in the work log.
- Snapshot bandwidth per client for a 1v1 on an arena with at least 30 loose pieces (an #82 arena or a test arena made for the measurement), recorded in the work log and below 30 KB/s.
- The measured gain over #88's baseline is in the work log.

## Chat excerpts

Adam — [this session](http://ivy.localhost/sessions/claude/bcbe88ae-0a32-432f-8fd1-3a061e17847f), 2026-10-05:

> online play should be part of the MVP.

## Work log

- 2026-10-05T14:55:57Z Drafted under run #75.
- 2026-10-05T15:04:18Z Admitted for run #75 after an independent contract check; its fixes were applied first.
- 2026-10-06T03:04:00Z Contract amended: the simulator and the prediction-vs-rollback spike moved to #88, which #83 now depends on; re-admitted for run #75 after an independent check.
- 2026-10-06T05:19:55Z stage implement start session codex:01a10fa3-aece-76f3-9658-406cb0d99123 — Traced hash-gated rendering; implementing local prediction, remote interpolation and compressed MessagePack snapshots.
- 2026-10-06T05:29:08Z stage implement end session codex:01a10fa3-aece-76f3-9658-406cb0d99123 — Binary independent snapshots at 10 Hz, input acknowledgements, bounded owned-fighter prediction and 200 ms remote interpolation implemented; host outcomes preserved.
- 2026-10-06T05:29:19Z stage verify start session codex:01a10fa3-aece-76f3-9658-406cb0d99123 — Shared prepared target with two jobs and incremental disabled; full workspace gates plus impaired two-client match and offscreen predicted scene.
