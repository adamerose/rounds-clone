---
format: 3
status: ready
owner: codex:01a10f2b-96f4-7a13-b1e9-bc4e76af3969
created: 2026-10-06T03:01:53Z
origin: agent-proposed
tags: ["quarrel", "mvp", "network"]
value: 8
sessions:
  - claude:b7dbdddc-0b4a-4606-a5de-5229ea29df0b
  - codex:01a10f2b-96f4-7a13-b1e9-bc4e76af3969
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
- 2026-10-06T03:04:21Z Admitted for run #75 after an independent contract check.
- 2026-10-06T03:07:39Z stage implement start session codex:01a10f2b-96f4-7a13-b1e9-bc4e76af3969 — Outbound queues apply one delay per direction; live protocol and simulation stay unchanged.
- 2026-10-06T03:11:10Z Headless impaired two-client run: 720 ticks, 80 ms base RTT, uniform ±20 ms jitter each direction, 2% outbound loss, seed 88. set_held to latest visible aim: 40 probes/client, client 0 median 138.670 ms p95 172.708 ms; client 1 median 127.208 ms p95 172.305 ms. Snapshot maximum 6217 bytes; authority payload 8681682 sent / 213785 received bytes across both clients in 12.577 s (690299.3 B/s outbound total); 44 authority datagrams dropped. Both clients completed with matching terminal state hashes. Network crate: 17 tests passed, no skips.
- 2026-10-06T03:12:04Z stage verify start session codex:01a10f2b-96f4-7a13-b1e9-bc4e76af3969 — Strict all-target workspace Clippy passed; locked workspace build and tests are running sequentially with two Cargo jobs and the reusable root out/cargo-target.
- 2026-10-06T03:14:44Z Workspace verification passed: strict all-target Clippy, locked build, 43 tests including two headless capture CLI tests, and doc tests; no failures or skips. Refined the latency measurement to 41 probes for an unambiguous median; rerunning its focused check before review.
- 2026-10-06T03:17:35Z Updated focused measurement: 41 probes/client, client 0 median 125.958 ms p95 157.959 ms; client 1 median 126.740 ms p95 172.611 ms. Snapshot maximum 6221 bytes; authority payload 8680164 sent / 196061 received bytes across both clients in 12.357 s (702440.5 B/s outbound total); 44 authority datagrams dropped. Both completed with matching terminal hashes. Throwaway physics spike preserves all shared Cargo.lock package identities and adds serialization only outside the repository.
