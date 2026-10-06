---
format: 3
status: ready
owner: codex:01a10efb-452f-7e52-a19e-e83483327e9c
created: 2026-10-05T14:13:11Z
origin: agent-proposed
tags: ["quarrel", "mvp", "ui"]
value: 8
sessions:
  - claude:bcbe88ae-0a32-432f-8fd1-3a061e17847f
  - codex:01a10efb-452f-7e52-a19e-e83483327e9c
execution: unattended
parent: 75
depends-on: [78]
supersedes: []
split-from: []
---

# Add a menu, card picks and scores so people can start and finish a match

Players need to start a match, see their offers, read the score and choose to run it back, without command-line flags.
Style waits until after the MVP, so this is plain and readable rather than polished.

## Outcome

- A main menu offers local match, host and join (with an address field).
- A local match on one machine supports keyboard and mouse for one fighter and a controller for the other, or two controllers.
- Between fights, each picking fighter sees their offer with each card's name and one-sentence description and chooses one with their own input; others see who is picking.
- A score display shows points between fights, and the match end screen shows the winner and each fighter's current pick of run it back or new match, updating live until all agree.
- Everything uses plain shapes and text.

## Decisions

- Visible windows open on monitor 4 per `AGENTS.md`.
- #73 and #74 stay separate tickets; this one must not break them.
- #84 adds the Steam option to this menu.
- The scope includes the live-network lifecycle changes the menu needs: interactive host and join wait until cancelled instead of a five-second join window, and interactive play has no fixed tick cap, while bounded CLI automation keeps its limits (orchestrator decision for run #75, 2026-10-06, replacing the earlier launch note limiting this worker to client and presentation code).

## Evidence required

- Headless captures of the menu, a card pick, the score display and the match end screen.
- A visible local match on monitor 4, driven by scripted keyboard and simulated gamepad input, reaches a card pick, a match end and a run-back; the window centre is verified on monitor 4 and logged.
- A test drives one fighter from keyboard and mouse events and the other from simulated gamepad events.
- A host and a join on one machine reach the first fight from the menu, headless or with both windows verified on monitor 4.

## Chat excerpts

Adam — this session, 2026-10-04:

> also lets forget about aesthetic until we have an MVP, but its extremely important. the style and juice of rounds is great.

Adam — [this session](http://ivy.localhost/sessions/claude/b6830e28-b9c7-41d5-9510-1a09f7d6de88), 2026-10-05:

> just make it require consensus, and show the other players current pick

## Work log

- 2026-10-05T14:13:11Z Drafted under run #75.
- 2026-10-05T15:03:50Z Admitted for run #75 after an independent contract check; its fixes were applied first.
- 2026-10-06T02:13:42Z stage implement start session codex:01a10efb-452f-7e52-a19e-e83483327e9c — Reuse existing flow and network commands; add menu and device-aware readable presentation.
- 2026-10-06T02:24:51Z stage implement end session codex:01a10efb-452f-7e52-a19e-e83483327e9c — Menu, fixed UI framing, winner/vote labels, mixed device routing and event-based regression implemented.
- 2026-10-06T02:24:56Z stage verify start session codex:01a10efb-452f-7e52-a19e-e83483327e9c — Sequential fmt, strict all-target Clippy, locked workspace build/tests queued through shared Cargo lock; native and headless UI evidence next.
- 2026-10-06T02:52:14Z Verification correction: shared workspace artifacts were overwritten or reused across worktrees despite Cargo locking. CARGO_INCREMENTAL=0 gives this worker distinct workspace artifacts; 41 exact snapshotted tests passed. Captures exposed clipped score labels, now moved inward. Device scripts now hold choice screens for 30 rendered frames; UI evidence uses supported target-score=2 because the existing combat bot stalls in a later arena at target 5. Default menu target remains 5.
- 2026-10-06T03:24:58Z stage verify end session codex:01a10efb-452f-7e52-a19e-e83483327e9c — Combined arena delivery verified: 46 exact snapshotted tests, fmt, strict all-target Clippy, locked workspace build and doctests passed. Four headless views plus mixed votes inspected; native menu and scripted match verified on monitor 4 with two run-backs. Headless menu host/join reached first fight. Evidence: out/ticket081proof/report.md; optional GDI image unavailable.
- 2026-10-06T03:25:02Z stage review start session codex:01a10efb-452f-7e52-a19e-e83483327e9c — Fresh Claude Opus CLI reviews the complete candidate and rendered/native evidence read-only.
- 2026-10-06T03:35:45Z stage review end session claude:21d3df2e-8993-472b-aea0-ca439e61fa9a — REQUEST_CHANGES for e96974404f7ba7552dfffb802a424baecf7a603d..9a0005a6f3261c02a41aaff1d8529fb28b4827f5: reproduced five-second menu join window and found 601-second play cutoff; full findings and inspected evidence in out/ticket081proof/review.json. Network scope extension or separately admitted idea 89 required; candidate not integrated.
- 2026-10-06T03:36:49Z Orchestrator answered (a): scope extended to the live-network lifecycle fixes; #89 folds into this ticket. Resuming the same worker.
- 2026-10-06T03:39:11Z stage correction start session codex:01a10efb-452f-7e52-a19e-e83483327e9c — Authorized lifecycle fix: distinct cancellable interactive waiting/play, retain bounded automation and peer-loss checks; correct review notes where local changes are proportionate.
- 2026-10-06T04:23:02Z stage correction end session codex:01a10efb-452f-7e52-a19e-e83483327e9c — Interactive lifecycle and authority-failure findings fixed; native startup inspector corrected at hidden-window boundary.
- 2026-10-06T04:23:09Z stage verify end session codex:01a10efb-452f-7e52-a19e-e83483327e9c — 55 tests, strict Clippy, fmt, workspace build and doctests passed; six GPU views and monitor-4 native menu, wait, host and two runbacks verified.
- 2026-10-06T04:23:15Z stage review start session codex:01a10efb-452f-7e52-a19e-e83483327e9c — Fresh other-family review of complete corrected candidate and immutable executable evidence.
- 2026-10-06T04:27:25Z stage verify start session codex:01a10efb-452f-7e52-a19e-e83483327e9c — Rebased onto delivered ticket 88, preserving conditioned sockets and bounded constructors; full immutable-artifact verification queued through Cargo lock.
- 2026-10-06T04:33:07Z stage verify end session codex:01a10efb-452f-7e52-a19e-e83483327e9c — Combined ticket 88 candidate passed 58 exact snapshot tests, fmt, strict Clippy, locked build and doctests. Six refreshed GPU views inspected; native menu/wait/host verified on DISPLAY4, cancelled host port rebinds, scripted match finishes two runbacks. See out/ticket081proof/report.md.
- 2026-10-06T04:41:08Z stage review end session claude:f724753c-6c78-4477-be43-7f8a8c9a7326 — REQUEST_CHANGES for 6c9161350d55946cd5ebea593653b220fb2ee8ed..2c2c18f780be7a768c20d03a937b2c22e7ac3561: actual menu Join omitted interactive argument; headless join-before-host reproduced timeout. Full result out/ticket081proof/review2.json.
- 2026-10-06T04:41:19Z stage correction start session codex:01a10efb-452f-7e52-a19e-e83483327e9c — Correct actual menu Join route, add join-before-host regression and repeat native cancellation through menu-start choice 2.
- 2026-10-06T04:46:01Z stage correction end session codex:01a10efb-452f-7e52-a19e-e83483327e9c — Menu Join now selects interactive mode; join-before-host regression passes beyond old deadline; all-ready hint corrected and all capture hashes recorded.
- 2026-10-06T04:46:10Z stage verify start session codex:01a10efb-452f-7e52-a19e-e83483327e9c — Final evidence identity and format checks after 59 passing tests, strict Clippy, workspace build, doctests and native menu-route cancellation.
- 2026-10-06T04:46:18Z stage verify end session codex:01a10efb-452f-7e52-a19e-e83483327e9c — 59 passed, zero failed/ignored; seven refreshed views inspected, native actual menu Join client 1 cancels, host port rebinds, two runbacks verified on DISPLAY4; all checks passed.
- 2026-10-06T04:46:24Z stage review start session codex:01a10efb-452f-7e52-a19e-e83483327e9c — Fresh other-family review of whole range, including actual menu Join correction and reverse launch-order evidence.
- 2026-10-06T04:52:53Z Independent review approved whole range 4b949ef0c9242a79bd07ae61998814db7460a654..32b791a601f70c1284857e92a283506133ddead3, session claude:67bde4c2-028f-4c40-a066-45e88778b06d; reproduced actual menu Join alive beyond 8s. Nonblocking notes: silent pre-snapshot peer reservation may require cancel/rejoin, general disconnect copy, proof hashes added separately, native launch arguments retained in worker record, and ticket73 should use delivered mixed-device baseline. Full review out/ticket081proof/review3.json.
