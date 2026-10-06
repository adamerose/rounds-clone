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
- 2026-10-06T04:49:23Z stage review end — fresh Claude reviewer 5749944d-f362-4c0e-8e5a-b48953246aa8 confirms all six earlier corrections but rejects 282ab1f: oldest64 projectile snapshot drops newest Spray shots. Secondary findings concern breakable damage, example wording, spacing and ray-cast construction. Complete review retained.
- 2026-10-06T04:49:28Z stage correction start — reproduce new-shot invisibility with bundled Hailstorm stacks and breakable interaction with Borer/Rubber Round; retain bounded snapshots while preserving new threats and piece damage.
- 2026-10-06T04:52:15Z stage correction end — reproduced and fixed newest-shot omission with bounded latest64 snapshots; Borer/Rubber Round now damage pieces once on contact. Both new regressions and all53 sim tests pass, including stack toppling after restricting swept detection to drilling sensors. Example, spacing and shape-only ray findings corrected.
- 2026-10-06T04:52:24Z stage verify start — complete checks for the second correction, immutable candidate executables, exact original Spray/breakable reproductions and refreshed headless captures before full fresh review.
- 2026-10-06T04:59:24Z stage verify end — exact 1cdede0 candidate passes format, strict all-target locked Clippy, guarded all-target build, all80 frozen tests (53sim/21net/4presentation/2CLI), documentation checks and agreeing2400tick two-client smoke. Client77282089 hash produces refreshed inspected PNGs and passes original Spray/breakable replays.
- 2026-10-06T04:59:33Z stage review start — fresh other-family review of complete second-corrected range and both prior finding sets; includes new-shot and piece-contact red/green regressions, exact reviewer reproductions and current captures.
- 2026-10-06T05:16:24Z stage review end — Claude0351a8a1-d1ef-4fc0-a609-d2cb8e7d75bd approves entire1cdede0 range, rechecks all11 earlier fixes, and measures worst-case compact snapshots43710bytes across19arenas/2and4fighters. Three notes retained; reaction-shot explosion and zero-damage hit inconsistencies will be corrected with required #81 reconciliation.
- 2026-10-06T05:16:32Z stage correction start — preserve landed #81 menu/network lifecycle and reuse its frozen-client test hook, retaining both record histories. Reproduce Firecracker/Watchfire and positive explosion damage, then update stale roadmap card status; changed complete range needs fresh review.
- 2026-10-06T05:18:20Z stage correction end — preserved landed menu code and its common frozen-client hook; all55sim tests pass after Firecracker reaction-shot depth and minimum damage regressions fail then pass. Roadmap current-card status corrected; both histories preserved; changed complete range needs fresh review.
- 2026-10-06T05:18:28Z stage verify start — complete combined menu/card checks including new menu-client integration tests, freshly bound binaries and refreshed captures before fresh full-range review.
- 2026-10-06T05:33:13Z stage correction end — reconciled landed79/81, preserves all15 base-mechanics cases, and all73 simulation tests pass. Reload refills real magazines; Watchfire uses watched tuning without spending primary ammo; Echo preserves extended block; periodic damage emits one reaction. Native recoil/ballistic fixture assumptions reproduced and corrected with public inputs.
- 2026-10-06T05:33:19Z stage verify start — complete final combined workspace including menu/network integration, compiler-bound frozen products, tuned pinned recordings and refreshed captures before fresh full-range review.
- 2026-10-06T05:48:14Z stage verify end session codex:01a10efa-f6e1-7720-9f5d-cc0a32f67b29 — combined79/81 candidate f455e2e passes format, strict all-target locked Clippy, guarded locked build, all117 frozen tests (74sim/24net/14presentation/2capture/3menu), docs and agreeing2400tick real-client smoke. Current cards and tuning are pinned in inspected Grow/Explosion/Poison recordings; frozen client22e6fd2c hash binds captures.
- 2026-10-06T05:48:20Z stage review start session codex:01a10efa-f6e1-7720-9f5d-cc0a32f67b29 — fresh other-family review of the entire combined three-commit card range on landed79/81, all prior findings, tuned recoil/magazine/shape reconciliation and117-test current evidence; no author changes while reviewer runs.
- 2026-10-06T06:14:37Z stage review end session claude:8e9b0b9c-c323-4536-a4fe-8635b1becb54 — Rejected: three-round Hailstorm lacks sustained stream; secondary damage rerolls fade after its effect already ran.
- 2026-10-06T06:14:46Z stage correction start session codex:01a10efa-f6e1-7720-9f5d-cc0a32f67b29 — Add stackable stream magazine stat; keep selected explosion/poison damage coherent with its effect.
- 2026-10-06T06:17:39Z stage correction end session codex:01a10efa-f6e1-7720-9f5d-cc0a32f67b29 — Added magazine stat (30/57 rounds), selected effect damage, numeric large-magazine HUD, preserved literal zero tuning. Both review regressions red before correction; 77 simulation tests pass.
- 2026-10-06T06:17:50Z stage verify start session codex:01a10efa-f6e1-7720-9f5d-cc0a32f67b29 — Rebased complete four-commit candidate onto current origin/main; rebuild compiler-bound frozen products and full supported-interface checks.
- 2026-10-06T06:22:53Z stage verify end session codex:01a10efa-f6e1-7720-9f5d-cc0a32f67b29 — All120 frozen checks pass (77sim/24net/14presentation/2capture/3menu), fmt/strictClippy/lockedbuild/docs pass; actual2UDP2400tick smoke agrees through match end; current4captures inspected; public20seed blasts84damage each and Hailstorm90shots/600ticks vs14control.
- 2026-10-06T06:23:00Z stage review start session codex:01a10efa-f6e1-7720-9f5d-cc0a32f67b29 — Dispatch fresh other-family read-only review of complete ab7667a..c305cb3 four-commit candidate; scratch and before inventories declared.
- 2026-10-06T06:38:14Z stage review end session claude:0787c7b2-6624-425b-a81e-c459c45f3551 — Rejected: Blink Step unchecked destination traps fighters under shipped floors and beyond side walls; current magazine/effect corrections pass. Preserve exact shipped public reproductions.
- 2026-10-06T06:38:19Z stage correction start session codex:01a10efa-f6e1-7720-9f5d-cc0a32f67b29 — Constrain blink by fighter shape, solid terrain and arena frame; keep open-space displacement and native movement intact.
- 2026-10-06T06:42:52Z stage correction end session codex:01a10efa-f6e1-7720-9f5d-cc0a32f67b29 — Gatefall downward regression red before unchecked teleport fix; both shipped-floor and wall-escape regressions green. All79 simulation tests pass. Blink shape sweep clips current terrain/frame; margin now live tuning and older recordings default2. Retain compounding Hailstorm downside as nonblocking balance note.
- 2026-10-06T06:42:59Z stage verify start session codex:01a10efa-f6e1-7720-9f5d-cc0a32f67b29 — Rebased complete five-commit candidate onto origin/main; rerun full frozen suite, current content captures, two-client smoke and shipped blink sweep.
- 2026-10-06T06:52:40Z stage verify end session codex:01a10efa-f6e1-7720-9f5d-cc0a32f67b29 — All122 immutable tests/native checks pass. Current4captures and real2UDP2400tick match agree. Shipped57case sweep fixes floor/wall traps;3 outward small-platform falls recover through public stored jump/movement at hp100 with zero edge returns. Preserve platforming risk; no support-only blink mode.
