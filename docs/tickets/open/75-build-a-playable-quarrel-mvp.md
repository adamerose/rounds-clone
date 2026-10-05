---
format: 3
status: idea
created: 2026-10-05T01:09:00Z
origin: human-request
tags: ["quarrel", "autonomy", "autonomy-goal", "mvp"]
value: 10
sessions:
  - claude:66dca6be-08c4-4ba5-92a2-186fa5e8ebf5
execution: unattended
depends-on: []
supersedes: [59]
split-from: []
---

# Build a playable QUARREL MVP

Adam wants a first playable version of QUARREL, the ROUNDS spiritual successor described in `GOAL.md`, `docs/game-design.md` and `docs/roadmap.md` milestone M1. This run turns the footage-replay codebase into an ordinary match two people can start and finish, adds the card system and winner-sharpens draft, and delegates implementation to Sol workers.

## Outcome

- Two players can start a QUARREL match from a menu on one machine (or host/join over UDP), fight across rotating arenas to five fight wins, draft between fights under the winner-sharpens rule, and run it back or start a new match, with no footage replay profile involved.
- About ten original data-defined cards with stat and event-rule effects are playable, including at least one sniper-style stat pair and several intended combos.
- The superseded footage-fidelity queue is closed, and every ticket this run launches is closed or blocked.

## Decisions

- Destination: `main`. Workers use that ref; no pull request.
- Budget: Codex weekly window (about 1 % used at start), resets 2026-10-09T21:59Z; the run ends there or at 90 %. Claude windows are nearly unused and serve as fallback.
- Controls: up to five parallel workers, but tickets touching the same crates run one after another; workers launch on Sol (`gpt-6.1-sol`) at medium effort, as Adam asked.
- Adam approved closing #59, 016–037, 49, 52, 66, 69 and 70 as superseded by the 2026-10-04 direction; 62, 73 and 74 stay.
- Human playtesting of feel is outside this run; the run delivers a build ready for the first play session.

## Evidence required

- On the `main` tip: `cargo fmt --all -- --check`, strict all-target Clippy, locked build and tests pass.
- A headless two-client smoke and a headless capture run an ordinary match through at least one draft.
- A visible local match on monitor 4 reaches a draft, a match end and a run-back.
- Ticket list shows the superseded tickets closed and the run's tickets closed or blocked.

## Chat excerpts

Adam — this session, 2026-10-04:

> Go ahead autonomously and make me an MVP. delegate work to GPT Sol 6.1 where you can

## Work log
