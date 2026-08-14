---
format: 3
status: idea
created: 2026-08-14T16:54:24Z
origin: agent-proposed
tags: [implementation, match, cards, maps, ui]
value: 10
risk: 4
depends-on: [3, 4, 6, 7]
sessions:
  - codex:019ffea8-55c5-79b3-96b2-da3210d67d84
  - codex:01a00135-7e77-7f82-aa78-1831e5864da6
---

# Implement a complete match loop with stat cards

Two local players can play from sequential opening drafts through five full points, with half-point duel scoring, loser drafts, persistent stat-only cards, deterministic arena changes, and a visible final winner.

## Outcome

- Add a pure deterministic `Match` owner above the existing duel `World`.
  It owns match phase, full and half points, current draft picker and selection, five-card offers, acquired cards, arena cadence, and the one `World` used for every duel.
- Start each match on `arena-006` with player zero choosing one of five cards and then player one choosing one of five.
  After both picks, apply the acquired stats and begin the existing bilateral spawn-lock phase.
- Award a living duel winner one half point when the existing six-tick resolving phase publishes its result.
  When either player reaches two half points, award that player one full point, clear both half-point counters, and let the other player draft one card after the existing result display finishes.
- Treat a split pair of duel wins as `1–1`; the next non-draw duel decides the full point.
  A simultaneous death awards nothing, preserves both half-point counters, repeats the same arena, and does not open a draft.
- End the match when a player reaches five full points.
  Preserve the final score and acquired cards, freeze simulation input, and show the winner instead of opening a final loser draft.
- Give a player at most five acquired cards.
  One opening card plus at most four non-final loser drafts reaches that cap; the next loss ends the match, so there is no capped-loser transition to another duel.
- Draw each five-card offer without replacement from the 12 `stat-only` entries in embedded `spec/cards.json`, ordered by stable card ID before randomization.
  A card already owned remains eligible in later offers, so duplicate copies stack, while one offer never contains the same ID twice.
- Change arena only after a full point and never select the current arena.
  Select from the 62 source-ordered catalog arenas whose primitives are all `static`; exclude `arena-003`, `arena-015`, `arena-023`, `arena-026`, `arena-045`, `arena-046`, `arena-053`, and `arena-054` until their hazard or dynamic behavior exists.
- Render the score, half-point state, acquired-card stacks, active picker, five choices, selection highlight, concise original-neutral effect summaries, current arena, and match winner in Godot.
  Do not display original card art, original descriptive copy, or Landfall UI assets.
- Reuse movement and jump for draft input: the active picker's left/right rising edges wrap the five choices and a jump rising edge confirms.
  A new draft must first observe that picker's movement and jump neutral together so a held combat input cannot choose accidentally; the other player's input is ignored.

## Why

The base duel now plays and replays reliably, but it still resets forever without the comeback structure that defines Rounds.
Scoring, loser upgrades, persistent builds, and changing arenas create the smallest complete local match before behavior-hook cards, bots, controller support, sound, and production presentation expand the state space.

## Essential constraints

- Keep `Rounds.Sim` Godot-free, fixed at 60 Hz, deterministic, allocation-conscious in tick paths, and within every existing determinism rule.
  `Match.Step` is the only new gameplay step boundary; Godot reads inputs, calls it, and draws its public state.
- Use one `World` and the existing `Sim.Step`, combat controller, collision vocabulary, duel phases, timers, counters, and reset behavior.
  Do not duplicate combat in `Match`, freeze or replace the world during a duel, or create a second physics path.
- Validate the complete two-player input before any match, draft, RNG, or world mutation.
  Movement must be `-1`, `0`, or `1`, button values use the typed booleans, and aim components must be finite even while a draft or result phase ignores combat input.
- Keep `World.CreateSmoke`, `Sim.Step`, and the `base-combat-v1` replay behavior byte-for-byte compatible.
  Match play is a new caller; ticket 007's duel replay, golden hash, corpus policy, and generic renderer remain unchanged.
- Store one immutable derived combat profile on every `Player`, initialized to an exact `Vanilla` profile.
  Extend `Sim.Hash` only when at least one profile differs from `Vanilla`: after the entire existing hash sequence, append one fixed custom-profile marker followed by every player's eight profile values in player order.
  This makes custom `Sim.Step` futures complete while every all-vanilla world retains the exact pre-ticket hash bytes and golden result.
- Build `Match.Hash` by adding the complete `Sim.Hash(Match.World)` value and then every match-owned field in its specified stable order.
  Acquired IDs remain hashed even if a combination happens to fold back to the vanilla profile.
- Pause `World.Tick` and ordinary per-tick RNG advancement during drafts and the final match result.
  Draft generation and arena selection consume only explicit values from the same persistent world PCG; no second RNG, wall clock, or Godot random source may affect gameplay.
- Add rejection-sampled bounded PCG selection: for unsigned bound `b`, compute `threshold = (-b mod 2^32) mod b`, discard draws below it, and return `draw mod b`.
  Use it for descending Fisher–Yates over the 12 ordinal card IDs and take the first five.
  Generate player zero's opening offer at match creation, player one's only after player zero confirms, and each loser offer only after the completed duel's result phase resets the world.
- When player one confirms the second opening pick, install both derived profiles and reset duel zero through the same world reset path with `incrementDuel:false`.
  Restore profile-based health, ammunition, block, movement, aim, and timers while preserving world tick, PCG state, next bullet ID, overflow count, duel number, and result count.
- After a loser confirms, choose the next arena from the ordinal eligible list with the current arena removed, apply all persistent player stats, and reset the already-incremented fresh duel without incrementing its number again.
  Select the arena with exactly one `NextBounded(61)` result after removal; only rejection retries may consume additional PCG draws.
- Process each published duel result exactly once by tracking the observed monotonic `DuelResultCount`.
  Scoring occurs when `World` enters `Result`; the result timer still completes before a draft or arena transition, and a transition tick does not also consume draft or active-duel input.
- Preserve `World.Tick`, PCG state, `NextBulletId`, `DroppedBulletCount`, and duel/result counters across every duel and arena change.
  Reset bullets, health, ammunition, block, movement, aim, and timers through the one world reset path.
- Load the embedded card catalog through a stream-loadable parser that rejects the wrong target build, duplicate IDs, unknown operations or targets, malformed values, a non-12 stat-only pool, and a stat-only entry whose behavior hook is not exactly `passive`.
  Do not use repository-relative runtime paths or change `spec/`.
- Represent per-player derived combat values explicitly: maximum health, maximum ammunition, bullet damage, fire interval, reload duration, projectile speed, block cooldown, and lifesteal.
  Combat must read the shooter's derived values, reset health/ammunition from them, and hash every derived value that can affect the future.
- Fold every acquired copy in acquisition order-independent groups over vanilla base values, then apply these provisional duplicate rules because the catalog deliberately leaves duplicate composition unresolved:
  - `player.max-health` is `vanilla × (1 + positive total) / (1 + abs(negative total))`, so positive percentages add against base and Glass Cannon stays playable;
  - positive `weapon.damage` and `weapon.projectile-speed` percentages add against the vanilla base;
  - negative `weapon.attack-speed` percentages multiply the vanilla fire interval by `1 + abs(total percentage)`;
  - negative `player.block-cooldown` percentages multiply the vanilla cooldown by `max(0.05, 1 + total percentage)`;
  - `weapon.ammo` counts add and clamp to at least one;
  - reload-time flat seconds add to the vanilla duration before all reload multipliers, and every Quick Reload copy contributes another `0.3` multiplier;
  - lifesteal percentages add without a cap and heal from actual health removed by that hit, capped at the attacker's derived maximum health.
- Convert seconds to ticks at 60 Hz before the reload fold.
  Round final tick and count values with midpoint-away-from-zero and clamp fire interval, reload duration, block cooldown, and ammunition to at least one; require every floating result to remain finite and positive where applicable.
- Keep rarity weighting, card exclusion rules beyond the 12-card slice, and exact vanilla duplicate formulas provisional and visible in documentation.
  Equal access to every stat-only ID is an implementation choice, not a claim that vanilla rarity weights are known.
- Give the 12 cards original-neutral shell labels rather than their catalog `originalName`: Deliberate, Chamber Trade, Guarded, Railshot, Overcharge, Heavy, Siphon, Snap Load, Hair Trigger, Stabilizer, Juggernaut, and Windup in ordinal card-ID order.
  The stable catalog IDs remain the simulation identity.
- Update `docs/architecture.md` to replace its prospective one-card-per-file stat machinery with the implemented ordered data fold while retaining ID-ordered hooks as the future behavior-card boundary.
  Remove the now-resolved frame-capture open item and describe the `Match`/`World` ownership boundary.
- Update the README from an endless duel description to the playable match flow and exact draft controls.
- Keep the 55 behavior-card entries, card hooks, rarity weighting, dynamic/hazard/breakable map behavior, bots, self-play statistics, match replay format, rematch/menu flow, controller defaults, sound, camera zoom, and production assets out of this ticket.

## Evidence required

- Catalog tests load the embedded and stream forms, assert the exact 12 ordinal IDs and aliases, and reject wrong build, duplicate/missing IDs, unsupported target/operation, non-finite values, hooks, and an incorrect pool size.
- Modifier tests isolate every target and operation, exact one-copy values for all 12 cards, duplicate accumulation, acquisition-order independence, negative health and attack-speed normalization, minimum ammunition/timer clamps, reload add-then-multiply order, midpoint rounding, and finite validation.
- Combat tests prove player-specific health/ammunition reset, damage, fire interval, reload, projectile speed, block cooldown, and actual-damage lifesteal with maximum-health capping.
- Draft tests prove deterministic seed-to-offer behavior, five distinct choices, owned-card recurrence, neutral arming, rising-edge wrap and confirmation, inactive-player lockout, sequential opening pickers, exact opening reset, explicit PCG consumption, and changed offers for a changed seed.
- Score tests prove two straight wins, a split `1–1` followed by a decider, a draw with retained halves and arena, exactly-once result observation, result-display completion before transition, loser-only draft, half reset, first-to-five termination, no final draft, and frozen final input.
- Arena tests prove the same arena within a full point, a different eligible arena after it, deterministic seeded selection, exact exclusion of all eight unsupported visual-behavior maps, preserved world counters, and a valid spawn/reset on every one of the 62 eligible arenas.
- Hash tests cover the whole world plus match phase, score, half points, picker, selection, offer IDs, acquired cards and order-independent derived stats, draft latches, pending transition, and future RNG state.
  Identical scripted matches must finish with identical per-duel hashes, offers, arenas, cards, score, and final hash; one changed selection must change the hash.
  Focused compatibility tests must prove every all-vanilla `Sim.Hash` remains exact, any changed custom profile changes `Sim.Hash` before the next tick, and `Match.Hash` changes for match state while still incorporating the complete world hash.
- The harness exposes a bounded deterministic match smoke that programmatically selects cards and drives enough combat outcomes to terminate at five points without Godot, reporting final winner, score, card IDs, arena sequence, tick count, and hash.
- A precisely recorded native Godot exercise must show both sequential opening drafts, wrap and confirmation controls, changed health/ammunition or cadence from selected cards, one full-point loser draft, a changed arena, visible persistent score/card stacks, and the final winner path.
- The complete repository gate passes with zero warnings, every pre-existing replay and hash remains unchanged, `spec/` and `replays/` remain byte-identical to their pre-implementation trees, the generic one-frame and canonical 600-frame replay renders still pass, and the worktree is clean at the reviewed candidate.

## Work log

- 2026-08-14T16:54:24Z stage design start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Designing the smallest complete match owner above the approved duel world, with sourced scoring, deterministic drafts and arena cadence, an explicit provisional stat fold, and no behavior-card or replay-format expansion.
- 2026-08-14T16:57:24Z stage design end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Bound a single match owner, exact score and transition edges, unbiased draft and arena RNG, 12-card duplicate formulas, per-player stats, original-neutral UI, and exclusions that preserve the approved duel replay contract.
- 2026-08-14T17:04:41Z stage admission start session codex:01a00135-7e77-7f82-aa78-1831e5864da6 — Cold-reading exact candidate `7c03c8486ad7aa3af7b6befb6fb8290985b848d7` against the closed dependencies, match/card/map specs, architecture, implementation seams, evidence, and risk-4 admission bar.
- 2026-08-14T17:04:41Z stage admission end session codex:01a00135-7e77-7f82-aa78-1831e5864da6 — Rejected an unreachable capped-loser branch, unresolved custom-profile hash ownership, a missing opening reset, and underspecified arena RNG consumption.
- 2026-08-14T17:04:41Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Removing the impossible branch and binding conditional profile hashing, the profile-aware duel-zero reset, and exact bounded arena selection.
- 2026-08-14T17:04:41Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Capped drafts now terminate naturally, vanilla hashes remain byte-exact through a custom-profile marker, both opening profiles reset duel zero, and arena choice consumes one bounded selection plus rejection retries.
