---
format: 3
status: closed
created: 2026-10-06T03:34:14Z
origin: system-detected
tags: ["quarrel", "mvp", "network"]
value: 9
sessions:
  - codex:01a10efb-452f-7e52-a19e-e83483327e9c
execution: unattended
parent: 75
depends-on: [78]
supersedes: []
split-from: []
---

# Let menu-hosted online sessions wait for players and finish their matches

The live transport's short automation limits also apply to menu-launched matches.
Players need enough time to enter a host address and complete matches and consensus run-backs without a timed cutoff.

## Outcome

- An interactive host can wait for the partner while remaining cancellable, including when joining takes longer than five seconds.
- An interactive online match and its run-backs continue until players leave, without the automation tick cap ending play.
- Existing bounded CLI automation and its configuration, timeout, disconnect and cleanup checks remain available.
- Connection/waiting and intentional departure states give players an accurate message.

## Decisions

- Destination is main for run 75.
- Preserve authority, packet/session identity and consensus match rules; this is lifecycle work, not a protocol redesign.
- Ticket 81's current worker was expressly scoped to client, presentation and match-flow UI. Network implementation needs its own admitted scope or explicit extension of that worker's scope.

## Evidence required

- A bounded headless test starts the interactive host, delays the partner beyond the existing five-second automation timeout, then proves both reach the first fight and close cleanly.
- A deterministic or controlled-clock regression proves the interactive route does not terminate at MAX_LIVE_TICKS while bounded automation still does.
- Existing live network regressions pass; cancellation and unavailable-authority cases release their sockets.

## Chat excerpts

Adam — [worker scope](http://ivy.localhost/sessions/codex/01a10efb-452f-7e52-a19e-e83483327e9c), 2026-10-05:

> keep to the client, presentation and match-flow UI.

## Work log

- 2026-10-06T03:34:14Z Found by ticket 81's fresh [Claude reviewer](http://ivy.localhost/sessions/claude/21d3df2e-8993-472b-aea0-ca439e61fa9a).
- 2026-10-06T03:34:14Z Snapshot menu host without a partner exited after 5189 ms with join_timeout; join alone exited after 5390 ms. Interactive menu args pass --ticks 36060 (601 seconds); code closes completed sessions during any current phase. Evidence: out/ticket081proof/review.json and its recorded scratch paths.
- 2026-10-06T03:36:54Z Abandoned: folded into #81 by the run #75 orchestrator (scope option a).
