---
format: 3
status: closed
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
- 2026-10-06T03:19:29Z Physics spike (release Rust 1.98.1, Intel Core Ultra 7 265F, 32 loose pieces, 2000 samples): prediction median/p95 0.0014/0.0016 ms; remote interpolation 0.0001/0.0001 ms; physics checkpoint capture 0.0147/0.0314 ms; restore + six-frame resimulation 0.1370/0.2047 ms; final checkpoint 54990 bytes. Independent same-machine physics states bit-identical at all 2000 frames; all 12000 restored continuation frames bit-identical. Rollback is affordable here; choose host-authoritative local prediction/interpolation because full-match atomic state and cross-machine identity remain unproved. Decision and limitations recorded under ticket 88/run 75 in docs/decisions.md; spike source/dependencies remain outside the repo.
- 2026-10-06T03:20:06Z stage implement end session codex:01a10f2b-96f4-7a13-b1e9-bc4e76af3969 — Simulator, metrics and prediction/rollback decision complete; throwaway physics code stays outside the repo.
- 2026-10-06T03:20:12Z stage verify end session codex:01a10f2b-96f4-7a13-b1e9-bc4e76af3969 — Formatting, strict all-target Clippy, locked workspace build/tests and refined 41-probe latency check passed; optimized physics spike passed same-machine state comparisons.
- 2026-10-06T03:20:18Z stage review start session codex:01a10f2b-96f4-7a13-b1e9-bc4e76af3969 — Preparing the complete candidate for fresh read-only Claude review after rebasing and repeating checks.
- 2026-10-06T03:29:59Z Final rebased candidate: fmt, strict all-target Clippy, locked build and 43 copied-executable tests passed. Documentation phase failed at rustdoc because shared-target libquarrel_sim-e3fdc65f5faf41ff.rlib was missing while other Cargo commands were active. Rerunning only docs after Cargo quiesces and refreshing candidate source timestamps; no new target or skipped check. Known shared-build isolation is owned by ticket 87.
- 2026-10-06T03:33:48Z Documentation recovery passed after waiting for Cargo activity to finish and rebuilding only candidate libraries in the reused target. All three crate doc-test phases completed with no failures or skips. Test executables remain frozen copies of this candidate; subsequent record-only edits do not change Rust sources or configuration.
- 2026-10-06T03:47:44Z Fresh read-only review by claude:cbca3670-e101-4942-ad83-efae2d30f24f approved 84659aeef579e5cd468d379d1cf12ad49bc291ec..10395ea6d7eb3278a290ae940eb74ae23cf1558b with no blockers. Reviewer independently reran the spike, reproducing bit identity and similar costs. Non-blocking notes: the determinism pair is two in-process worlds, not separate processes; live queue polling can add up to READ_INTERVAL plus scheduling; new traffic JSON keys are snake_case inside camelCase reports; restore-plus-six cost implies a per-frame upper bound of 0.022833 ms median / 0.034117 ms p95, not isolated re-simulation; extra blank lines are cosmetic. Exact review result is retained in the session and task temp review.json. Impaired peer-loss/terminal-timeout cases and cross-machine full-match determinism were not independently verified.
- 2026-10-06T03:59:35Z stage correction start session codex:01a10f2b-96f4-7a13-b1e9-bc4e76af3969 — Fresh resolution review approved the doc append, then ticket85 landed. Reproduced E0599 at 11 fixture try_clone calls after rebase. Changed those tests to clone the inner raw UDP socket and retained the impaired fixture authority endpoint through peer teardown; focused cargo check --tests now passes. Both decision/postmortem entries preserved.
- 2026-10-06T04:04:52Z stage correction end session codex:01a10f2b-96f4-7a13-b1e9-bc4e76af3969 — Combined ticket85/88 candidate passes fmt, strict all-target Clippy, locked build, 47 frozen-executable tests (21 network) and all doc phases, no failures or skips. Impaired run completes with matching terminal hashes; latest debug medians/p95 140.550/173.078 ms and 126.684/201.811 ms; max snapshot6222 bytes; authority payload698078.1 B/s total. New fixtures preserve raw endpoint lifetimes beneath the wrapper.
- 2026-10-06T04:05:00Z stage review start session codex:01a10f2b-96f4-7a13-b1e9-bc4e76af3969 — Fresh other-family review of the full candidate including ticket85 compatibility fixes and both preserved record entries.
- 2026-10-06T04:15:33Z Integration check note: installed Ivy check-all.mjs cannot run in this onboarded game repository: ENOENT scanning playbook/skills, which is absent here. Ticket validation and the game workspace format, Clippy, build, test, and documentation checks are the applicable verification; generic check-all is unavailable, not a game assertion failure.
- 2026-10-06T04:19:31Z stage review end session claude:bd861049-1072-414e-8f36-78756246754a — approved candidate 0289395079aa6f9051ecf33c2afee6c36d76061f..78f7cddd0ba6d11c046353ba91df8743c94f946f
- 2026-10-06T04:19:31Z stage integration end session codex:01a10f2b-96f4-7a13-b1e9-bc4e76af3969 — integrated 78f7cddd0ba6d11c046353ba91df8743c94f946f as 074e4500093b077ccce2f1f9d661af2f977fa130
