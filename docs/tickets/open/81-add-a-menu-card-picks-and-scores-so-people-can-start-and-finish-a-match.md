---
format: 3
status: ready
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
