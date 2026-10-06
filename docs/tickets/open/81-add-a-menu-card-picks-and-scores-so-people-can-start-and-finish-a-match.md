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
