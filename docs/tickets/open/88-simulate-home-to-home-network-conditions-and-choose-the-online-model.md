---
format: 3
status: ready
created: 2026-10-06T03:01:53Z
origin: agent-proposed
tags: ["quarrel", "mvp", "network"]
value: 8
sessions:
  - claude:b7dbdddc-0b4a-4606-a5de-5229ea29df0b
execution: unattended
parent: 75
depends-on: []
supersedes: []
split-from: [83]
---

# Simulate home-to-home network conditions and choose the online model

#83 needs online play to feel responsive at real latency, but it waits for #79's movement rules; the conditions it is measured under and the choice between prediction and rollback do not.
This ticket builds the network-conditions simulator and runs the comparison now, so #83 starts from a measured decision.

## Outcome

- The live UDP transport (`LiveServer`/`LiveClient`, crates/quarrel-network/src/live.rs) can add configurable delay, jitter and packet loss in both directions, set from a command-line option or config, for tests and manual play.
- At 80 ms round trip, 20 ms jitter and 2 % loss, the current netcode's input-to-visible delay for the local fighter (ms from `LiveClientHandle::set_held` to the first `latest()` snapshot showing it, median and p95), snapshot size and bandwidth are measured and recorded.
- A short spike compares client-side prediction of the local fighter with interpolation of everything else against rollback for this Rapier physics, on an arena with at least 30 loose pieces, records rollback's re-simulation cost per frame and whether two runs on this machine produce bit-identical state, and records the numbers, the choice and the rejected option in `docs/decisions.md`.

## Decisions

- The spike's code is throwaway unless it is the simulator; only the simulator and the measurements ship.
- Prediction on a host-authoritative simulation is the expected answer, because rollback needs bit-identical physics on every machine and re-simulates every loose piece; the spike may overturn it with evidence.
- #79 (movement and combat), #80 (cards), #81 (menu) and #85 (network test fixtures) run in parallel: keep production changes to the transport layer in crates/quarrel-network and rebase onto whichever lands first.

## Evidence required

- A test sends packets through the simulator and observes the configured delay, jitter and loss within tolerance.
- A headless two-client match runs under the simulated conditions, and the measurements above are in the work log.
- The comparison and decision are in `docs/decisions.md` under a heading naming this ticket and #75.

## Work log

- 2026-10-06T03:01:53Z Split from #83 under run #75 so the latency work can start before #79 lands.
