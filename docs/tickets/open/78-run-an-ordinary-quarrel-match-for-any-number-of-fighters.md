---
format: 3
status: ready
owner: codex:01a10e3b-a842-7822-a150-68dd15a3b423
created: 2026-10-05T14:13:11Z
origin: human-request
tags: ["quarrel", "mvp", "match-flow"]
value: 10
sessions:
  - claude:bcbe88ae-0a32-432f-8fd1-3a061e17847f
  - codex:01a10e3b-a842-7822-a150-68dd15a3b423
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
- The end-of-match choice needs consensus: each fighter picks run it back or new match, can change their pick, and sees everyone's current pick; nothing happens until every pick agrees (Adam, 2026-10-05).
- "Every fighter who did not win picks" is the free-for-all reading of "the loser picks"; in 1v1 they are the same.

## Evidence required

- Sim tests cover: opening pick, a point per fight, loser pick, a same-tick double death resolved by the seeded coin flip, first to the target, run-it-back keeping cards and raising the target, the end-of-match choice waiting until every fighter's pick agrees, the run-back limit, and arena rotation without repeats.
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

Adam — [this session](http://ivy.localhost/sessions/claude/b6830e28-b9c7-41d5-9510-1a09f7d6de88), 2026-10-05:

> just make it require consensus, and show the other players current pick

## Work log

- 2026-10-05T14:13:11Z Drafted under run #75.
- 2026-10-05T15:03:05Z Admitted for run #75 after an independent contract check; its fixes were applied first.
- 2026-10-05T22:44:33Z stage research start session codex:01a10e3b-a842-7822-a150-68dd15a3b423 — Trace simulation, snapshots, clients and replay consumers for the general match replacement.
- 2026-10-05T22:50:06Z stage research end session codex:01a10e3b-a842-7822-a150-68dd15a3b423 — Footage adapters own the obsolete assumptions throughout all six crates; replace those adapters while preserving ordinary arena physics and UDP/live render interfaces.
- 2026-10-05T22:50:44Z stage implement start session codex:01a10e3b-a842-7822-a150-68dd15a3b423 — General flow and card pool implemented; bounded agents replace physics adapters, network consumers and rendering consumers in the isolated checkout.
- 2026-10-05T23:12:06Z stage verify start session codex:01a10e3b-a842-7822-a150-68dd15a3b423 — Run strict all-target Clippy, locked build/tests and public UDP/capture evidence on the general flow; reuse prepared two-job Cargo target.
- 2026-10-05T23:45:25Z stage implement end session codex:01a10e3b-a842-7822-a150-68dd15a3b423 — General match config, seeded card pool, consensus run-backs, random arena bag and recorded-input replay replace footage branches; retained arena and live UDP behaviors have active regressions.
- 2026-10-05T23:46:52Z stage correction start session codex:01a10e3b-a842-7822-a150-68dd15a3b423 — Rendered recheck exposed the failed Unicode separator replacement; apply exact edits and verify final presentation bytes.
- 2026-10-05T23:48:11Z stage correction end session codex:01a10e3b-a842-7822-a150-68dd15a3b423 — Final captures show supported separators, both current vote choices and consensus instructions; arena projection retains aspect in visible and offscreen views.
- 2026-10-05T23:49:31Z stage verify end session codex:01a10e3b-a842-7822-a150-68dd15a3b423 — Format, strict all-target Clippy, locked build and all 28 tests pass; 235-tick replay is deterministic, three fighters finish, 1200-tick UDP clients agree through drafts and match end, and final offscreen captures are inspected. Ivy check-all requires absent playbook/skills; ticket validation passes.
- 2026-10-05T23:49:45Z stage review start session codex:01a10e3b-a842-7822-a150-68dd15a3b423 — Freeze the complete code-only candidate and send its exact range, contract and rendered evidence to a fresh other-family Claude CLI reviewer.
- 2026-10-06T00:11:28Z stage review end session claude:38ce06fc-1361-4773-86ab-dd2211daa9a2 — Reviewed a84f6b68623bcd20a1afada4a5a21ca64ee749af..a66085dcea3ec56523ad5d2711f72aa72b804fd7: match rules and images approved, with notes on capture aliasing, replay content coupling and stale docs. Integration withheld for those defects and a parent-reproduced name-edit reload failure.
- 2026-10-06T00:11:41Z stage correction start session codex:01a10e3b-a842-7822-a150-68dd15a3b423 — Fix capture destination aliasing/provenance, pin recorded logical content, preserve arena file identity across name edits, refresh living docs and restore general transport regressions.
