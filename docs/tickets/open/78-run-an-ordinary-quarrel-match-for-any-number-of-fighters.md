---
format: 3
status: ready
created: 2026-10-05T14:13:11Z
origin: human-request
tags: ["quarrel", "mvp", "match-flow"]
value: 10
sessions:
  - claude:bcbe88ae-0a32-432f-8fd1-3a061e17847f
execution: unattended
parent: 75
depends-on: [77]
supersedes: []
split-from: []
---

# Run an ordinary QUARREL match for any number of fighters

The simulation branches on seven `ReplayProfile` footage slices, uses fixed card offers transcribed from recordings, and hard-codes two players, so nobody can play an ordinary match.
This ticket replaces all of that with one general match flow driven by a match config, following pillar 10 of `docs/game-design.md`.

## Outcome

- A match config (fighter count, target score, offer size, run-it-back limit, seed) replaces `ReplayProfile`; fighters are a list, not two fixed slots, and 1v1 is the shipped setting.
- A match goes: every fighter picks one card from their own offer; then fights, each on a random arena from `assets/arenas/` that does not repeat until all have been used; the last fighter standing scores a point; every fighter who did not win the fight picks one card from their own seeded offer; the first to the target score wins.
- A fight never ends in a tie: if the last fighters die on the same tick, a coin flip seeded from the match decides who scores, identically on every client.
- At match end, everyone can vote to run it back with their cards kept and the target raised from 5 to 10, then 15, at most twice; otherwise a new match starts from no cards.
- Offers are drawn from the card pool in data, so new cards appear in offers without code changes.
- A short recorded-input replay plays an ordinary match through these general rules as a regression check.
- `ReplayProfile`, the historical-rematch setup, the fixed offer lists, the ROUNDS card catalog and the capture-anchor tests that only served them are deleted.
- It defines the minimal card file format (id, name, one-sentence description, stat changes) under `assets/cards/`, with at least five placeholder stat cards; #80 extends this format and replaces the placeholders.

## Decisions

- Defaults: first to five points, five cards per offer, run-backs raise the target to 10 and then 15, at most two run-backs (Adam, 2026-10-04 and 2026-10-05; tunable in the config).
- Only fighters who did not win a fight pick; in 1v1 that is the loser (Adam, 2026-10-04).
- A new random arena every fight, as in ROUNDS (Adam, 2026-10-04).
- Offers are seeded from the match seed so a replay reproduces them.
- A run-back needs every fighter to vote yes; any no starts a new match from no cards, beginning with an opening pick (delegated choice for run #75, recorded in `docs/decisions.md`).
- "Every fighter who did not win picks" is the free-for-all reading of "the loser picks"; in 1v1 they are the same.

## Evidence required

- Sim tests cover: opening pick, a point per fight, loser pick, a same-tick double death resolved by the seeded coin flip, first to the target, run-it-back keeping cards and raising the target, the run-back limit, and arena rotation without repeats.
- A sim test runs a three-fighter match to its end.
- The recorded-input replay reaches a draft and a match end and produces the same result on two runs.
- A headless two-client smoke over UDP plays an ordinary match through at least one draft.
- `git grep -n ReplayProfile` finds nothing in code.

## Chat excerpts

Adam — this session, 2026-10-04:

> i want to go back to only the loser gets to pick an upgrade, but lets scrap the best of 3 round thing.

> keep it quick but allow a way to optionally continue, or cycle, without complete reset, a few times (not infinitely)

Adam — [this session](http://ivy.localhost/sessions/claude/bcbe88ae-0a32-432f-8fd1-3a061e17847f), 2026-10-05:

> that should never happen. or flip a coin. dont tie

> running it back should be 5,10,15

## Work log

- 2026-10-05T14:13:11Z Drafted under run #75.
- 2026-10-05T15:03:05Z Admitted for run #75 after an independent contract check; its fixes were applied first.
