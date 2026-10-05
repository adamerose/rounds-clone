---
format: 3
status: ready
owner: claude:b6830e28-b9c7-41d5-9510-1a09f7d6de88
created: 2026-10-05T01:09:00Z
origin: human-request
tags: ["quarrel", "autonomy", "autonomy-goal", "mvp"]
value: 10
sessions:
  - claude:66dca6be-08c4-4ba5-92a2-186fa5e8ebf5
  - claude:bcbe88ae-0a32-432f-8fd1-3a061e17847f
  - claude:b6830e28-b9c7-41d5-9510-1a09f7d6de88
execution: unattended
depends-on: []
supersedes: [59]
split-from: []
---

# Build a playable QUARREL MVP

Adam wants a first playable version of QUARREL, the ROUNDS spiritual successor described in `GOAL.md`, `docs/game-design.md` and `docs/roadmap.md` milestone M1. This run turns the footage-replay codebase into an ordinary match two people can start and finish, adds the card system and the loser-picks draft, and delegates implementation to Sol workers.

## Outcome

- Two players can start a QUARREL match from a menu on one machine, over LAN, or online from two homes through a Steam friend invite, pick an opening card, fight on a new random arena every fight to five points with no tied fights, with each fight's loser picking a card, and run it back to 10 and then 15 or start a new match, with no footage replay profile involved.
- Online play is responsive at simulated 80 ms round trip with 2 % loss: your own fighter answers on the next frame.
- The base fight plays like ROUNDS (docs/rounds-reference.md) plus capped recoil movement, with numbers in live-reloaded data.
- The first twelve original data-defined cards from docs/roadmap.md M1 are playable, and the combos named in #80 work.
- Arenas are data files covering every ROUNDS object kind, with at least ten original arenas.
- The superseded footage-fidelity queue is closed, and every ticket this run launches is closed or blocked.

## Decisions

- Destination: `main`. Workers use that ref; no pull request.
- Budget: Codex weekly window (about 1 % used at start), resets 2026-10-09T21:59Z; the run ends there or at 90 %. Claude windows are nearly unused and serve as fallback.
- Controls: up to five parallel workers, but tickets touching the same crates run one after another; workers launch on Sol (`gpt-6.1-sol`) at medium effort, as Adam asked.
- Adam approved closing #59, 016–037, 49, 52, 66, 69 and 70 as superseded by the 2026-10-04 direction; 62, 73 and 74 stay.
- The M1 work is tickets #76 to #84, created 2026-10-05; online play (#83, #84) moved into the MVP at Adam's request; #76 (module split and rename) goes first so later tickets can run in parallel.
- The 2026-10-04 winner-sharpens draft was reversed the same day: only the loser of each fight picks, and every fight is a point (docs/decisions.md).
- Human playtesting of feel is outside this run; the run delivers a build ready for the first play session.

## Evidence required

- On the `main` tip: `cargo fmt --all -- --check`, strict all-target Clippy, locked build and tests pass.
- A headless two-client smoke and a headless capture run an ordinary match through at least one draft.
- A visible local match on monitor 4 reaches a draft, a match end and a run-back.
- A headless two-client match under simulated 80 ms round trip and 2 % loss completes with matching scores (#83).
- The Steam friend invite between two homes is checked by Adam at the first play session; if everything else passes, the run ends blocked only on that check.
- #76 to #84 and every other ticket the run launches are closed or blocked.

## Chat excerpts

Adam — [this session](http://ivy.localhost/sessions/claude/bcbe88ae-0a32-432f-8fd1-3a061e17847f), 2026-10-05:

> Ok let's finish grilling and rethinking and get ready for you to orchestrate work on the MVP


Adam — this session, 2026-10-04:

> Go ahead autonomously and make me an MVP. delegate work to GPT Sol 6.1 where you can

Adam — [this session](http://ivy.localhost/sessions/claude/bcbe88ae-0a32-432f-8fd1-3a061e17847f), 2026-10-05:

> online play should be part of the MVP.

## Work log
- 2026-10-05T01:08:21Z Run ticket admitted on Adam's authority by invoking /autonomy; destination main.
- 2026-10-05T14:16:48Z Contract updated to the settled design (loser picks, new arena every fight, ROUNDS basics plus recoil) and split into #76-#82; superseded tickets closed. The run is not yet started.
- 2026-10-05T14:56:33Z Online play (#83 responsive netcode, #84 Steam invites) added to the MVP; no tied fights; run-backs go to 10 and 15.
