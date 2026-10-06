---
format: 3
status: ready
owner: codex:01a10efa-f6e1-7720-9f5d-cc0a32f67b29
created: 2026-10-05T14:13:11Z
origin: human-request
tags: ["quarrel", "mvp", "cards"]
value: 10
sessions:
  - claude:bcbe88ae-0a32-432f-8fd1-3a061e17847f
  - codex:01a10efa-f6e1-7720-9f5d-cc0a32f67b29
execution: unattended
parent: 75
depends-on: [78]
supersedes: []
split-from: []
---

# Build data-defined cards from stats and event rules, with the first twelve

The fun Adam's group remembers is discovering combos, such as poison plus spray plus reload-on-hit giving an endless stream after one hit.
Cards built from stat changes plus rules that react to events let such combos emerge without anyone designing each one (pillar 2 of `docs/game-design.md`).

## Outcome

- Cards are RON files under `assets/cards/`, reloaded while the game runs; each has an original name, a one-sentence description, stat changes and event rules.
- Rules react to: fire, hit, block, bounce, land, take damage and kill; effects include extra shots, reload, teleport, repeat a block, fire at an opponent, explode, poison and changes to a shot in flight.
- Rules scale with stats (a reaction shot uses the fighter's current shot stats), and reaction chains fade so each triggered reaction is less likely to trigger the next.
- Twelve cards with our own names and numbers, matching these ideas from `docs/card-ideas.md`: Fast shot (much faster, harder shots, slower fire; the sniper card), Spray, Bounce, Grow, Steer, Drill, Explode, Poison, Reload on hit, Teleport, Echo and Radar.
- Most cards have a visible upside and downside.
- Holding several copies of a card stacks it.

## Decisions

- Card names, text and numbers are our own; ROUNDS names stay only in design notes.
- Echo means "when I block, block again a moment later" (Adam, 2026-10-04).
- This ticket may run alongside #79, which edits movement and blocking; whichever lands second rebases.
- Extends the card format defined in #78 rather than replacing it, and removes its placeholder cards.

## Evidence required

- A sim test for each card showing its effect the first time it fires.
- Combo tests: Poison with Spray and Reload on hit keeps firing after one landed hit; Echo with Teleport and Radar teleports twice and fires two reaction shots; Grow with Fast shot hits harder at long range.
- A test that a self-triggering chain stops within a bounded number of reactions.
- A test changes a card file during a session and observes the change.

## Chat excerpts

Adam — this session, 2026-10-04:

> powerful combos, like high bullet speed high damage making me a sniper. or echo + teleport + radar shot. or poison + spray + scavenger giving infinite spray when i land one bullet, or abyssal countdown + supernova.... the first time we had each of those combos was amazing

> I like grow, I like the one where you guide the bullet with your mouse, I like bouncing, drilling, etc.

## Work log

- 2026-10-05T14:13:11Z Drafted under run #75.
- 2026-10-05T15:03:36Z Admitted for run #75 after an independent contract check; its fixes were applied first.
- 2026-10-06T02:17:22Z stage implement start session codex:01a10efa-f6e1-7720-9f5d-cc0a32f67b29 — extend card stats and event rules; reuse shared Cargo target with native build-directory locking
- 2026-10-06T02:26:57Z stage implement end session codex:01a10efa-f6e1-7720-9f5d-cc0a32f67b29 — twelve original RON cards, deterministic fading reactions, live reload, Grow/poison/explosion visuals and swept Drill accounting implemented
- 2026-10-06T02:27:03Z stage verify start session codex:01a10efa-f6e1-7720-9f5d-cc0a32f67b29 — card and combo regressions, workspace checks and headless visual evidence; reconcile magazine integration when ticket 79 lands
- 2026-10-06T03:24:51Z stage correction start — reproduced Shepherd ignoring opposite aim; rotate velocity through the shortest angle, preserving speed, with a boundary regression
- 2026-10-06T03:47:49Z stage correction end — Shepherd reverses toward opposite aim; Drill shots still expire; saw and loose-object damage emits TakeDamage in the same tick; original arena fixtures use neutral cards
- 2026-10-06T03:49:29Z stage verify end — format, strict all-target Clippy, locked all-target workspace build and doctest commands passed; 67 frozen candidate tests passed (47 sim, 14 network, 4 presentation, 2 CLI), no ignored tests. One intermittent existing FIFO network failure was retained and the complete network rerun passed. Two-client 2400-tick smoke reaches match end with identical state; Grow, explosion and poison headless PNGs have executable/state/frame hashes. Evidence out/ticket080proof; no GUI or human play-feel check.
- 2026-10-06T03:49:35Z stage review start — complete card candidate and retained evidence ready for fresh other-family CLI review; parallel base-fight ticket 79 has not yet landed and will be reconciled if it lands first
- 2026-10-06T04:09:52Z stage correction start — independent Claude review rejected unbounded impact history and explosions past fast-shot contacts; full findings and reproduction retained in out/ticket080proof/review-1.json and review-1-artifacts. Also correcting first-event priority, reliable Fire flight changes, combo strength and documentation examples.
- 2026-10-06T04:22:37Z stage correction end — reproduced and fixed review findings with red/green regressions: visual impacts expire after twelve ticks and cap at64; fast shots explode at first swept contact; primary events displace deep/distant queued reactions; launch flight changes survive fading. Reload combo now beats an identical no-Reload control; README uses a live card; journal spacing fixed. All51 sim tests and focused queue-priority check pass. Ticket85 fixture corrections incorporated.
- 2026-10-06T04:34:37Z stage verify end — corrected candidate passes format, strict all-target Clippy, locked all-target build, doctest commands and78 frozen tests (51 sim,21 network,4 presentation,2 CLI), none ignored. Refreshed PNGs inspected and hashes match frozen client36bb7ad9. Two-client2400-tick smoke reaches match end with matching state. Reviewer long reproduction now9218 compact bytes and1 recent impact with334 cumulative explosions, versus69739 bytes and334 retained impacts before correction.
- 2026-10-06T04:34:48Z stage review start — fresh other-family review of the complete corrected range, with prior findings, red/green regressions, bounded long reproduction, current captures and integrated tickets85/88
