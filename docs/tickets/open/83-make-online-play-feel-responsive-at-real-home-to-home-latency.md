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
- 2026-10-06T05:33:46Z stage verify end session codex:01a10fa3-aece-76f3-9658-406cb0d99123 — fmt, strict all-target Clippy, locked workspace build and all 82 tests passed. Frozen executable smoke reached opening draft, loser draft and match end with agreeing clients. Offscreen before/after predicted renders inspected. Impaired 32-piece two-client match completed with matching host terminal hash, score and card loadouts; 22,321.6 payload B/s per peer, max 2,306-byte datagram (93.64% below ticket 88 baseline 351,220 B/s). Movement/shot/block input-to-scene measured one presentation frame (16.67 ms at 60 Hz), predictor CPU 0.377/0.308 ms; baseline median 125.958/126.740 ms, nominal local-frame gain at least 109 ms. Display scanout and actual two-home routing unavailable.
- 2026-10-06T05:33:50Z stage review start session codex:01a10fa3-aece-76f3-9658-406cb0d99123 — Preparing exact code candidate for fresh read-only Claude Opus review; contract and evidence complete.
- 2026-10-06T05:45:07Z stage correction start session codex:01a10fa3-aece-76f3-9658-406cb0d99123 — Author found unconditional stance restoration replaces unchanged colliders and discards warmed floor contacts. Reproducing in a second detached checkout while the original exact range stays immutable for ongoing review.
- 2026-10-06T05:50:11Z stage review end session claude:e33188b8-f727-4d9f-a071-eb6b0724346d — Requested changes for exact c3f7fb7 range: local shots bounce from fixed fighter proxies, disappearing remote shots freeze, and remote aim/stance use newest rather than buffered state. Review result retained in task temporary review.json; new failing regressions reproduce both blocking paths. Ground-contact regression failed before its one-line stance-identity correction and now passes.
- 2026-10-06T05:51:29Z stage correction end session codex:01a10fa3-aece-76f3-9658-406cb0d99123 — Three failing boundary regressions reproduced discarded floor contacts, invented local shot bounces and frozen remote terminal segments. All pass after corrections; remote aim/stance now use buffered time. Stronger neutral-baseline impaired probe passed: one scene frame, 0.439/0.407 ms CPU, 23,971 payload B/s per peer (93.17% below baseline), matching terminal hashes, scores and picks.
- 2026-10-06T05:59:47Z stage verify end session codex:01a10fa3-aece-76f3-9658-406cb0d99123 — Corrected candidate: all 85 workspace tests, format, strict all-target Clippy, locked build and frozen executable smoke passed. Added control scene advances the same elapsed time with neutral input; probe alone causes movement, shot and block on next scene frame. Final impaired 32-piece match: 22,284.4 payload B/s per peer, 2,303-byte maximum, 93.66% below baseline, predictor CPU 0.349/0.288 ms, host/client terminal hashes, scores and card picks agree. Inspected corrected renderer capture; no visible windows launched.
- 2026-10-06T05:59:52Z stage review start session codex:01a10fa3-aece-76f3-9658-406cb0d99123 — Complete corrected range goes to a fresh read-only Claude context; previous findings, failing regressions, corrections and exact verification supplied.
- 2026-10-06T06:21:02Z stage review end session claude:a9af5c9a-ed42-49a6-b303-85f11b1b2c8a — Prior findings confirmed fixed; requested correction of raw snapshot overwrite before mouse aim. Also identified remote terminal extrapolation crossing walls. Public live-UDP/Bevy polling regression reproduced pose overwrite (-400000 to -500000 milli); collision regression reproduced wall penetration. Both now pass: bootstrap raw scene only once; reuse presentation-only CCD for terminal segments.
- 2026-10-06T06:23:46Z stage correction end session codex:01a10fa3-aece-76f3-9658-406cb0d99123 — Live sample arrival now preserves the drawn mouse-aim origin; unused raw hash cache removed. Terminal remote shots use existing CCD/lifetime/fixed-proxy prediction with fractional tick interpolation. Both new regressions failed before their fixes and pass now; rendering fixture added for wall contact.
- 2026-10-06T06:25:42Z stage verify start session codex:01a10fa3-aece-76f3-9658-406cb0d99123 — Verifying mouse-origin and collision-aware remote terminal correction, including four rendered/pixel-checked frame artifacts, full locked workspace gates and impaired 32-piece agreement/bandwidth.
- 2026-10-06T06:32:09Z stage verify end session codex:01a10fa3-aece-76f3-9658-406cb0d99123 — Format, strict locked all-target Clippy, locked build and all 88 serial tests passed; frozen smoke agrees through opening/loser draft and match end; deterministic capture and four pixel-checked frames passed. Impaired 32-piece probe: one scene frame, CPU 0.295/0.285 ms, 23,086.5 payload B/s/peer, max 2375 bytes, 93.43% baseline reduction. Display scanout/two-home routing unavailable; installed Ivy check-all inapplicable (requires absent playbook/skills). Evidence: out/netcode-evidence and TEMP/quarrel-83-final-tests.txt, final-impaired.txt.
