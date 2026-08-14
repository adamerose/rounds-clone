---
format: 3
status: closed
created: 2026-08-14T11:56:30Z
origin: agent-proposed
tags: [implementation, simulation, combat, blocking, lifecycle]
value: 10
risk: 4
depends-on: [2, 5]
sessions:
  - codex:019ffea8-55c5-79b3-96b2-da3210d67d84
  - codex:019fffc8-8b3b-75a1-ae5d-f4b8ad895a73
  - codex:019ffff9-3be5-7da2-8811-5df376ffc9a4
  - codex:019ffff6-6034-76b1-96a2-b080ac183346
  - codex:01a00048-2aa0-7b33-87a8-edfba30eae67
---

# Implement deterministic base combat and a complete duel loop

Two local players can independently aim, fire a base gun, use recoil, take damage and knockback, block and reflect shots, die by damage or bottom ring-out, see a duel result, and reset into another deterministic duel in the live arena.

## Outcome

- Extend `PlayerInput` with a finite world-space aim vector and held fire/block semantics; zero aim retains the player's last valid direction, while all nonzero aim is normalized inside `Rounds.Sim`. Spawn and reset aim is provisionally `(1, 0)` for the left player and `(-1, 0)` for the right player.
- Add immutable tuning loaded from the embedded binding combat and player facts for exact `1.0` base health, three rounds, `0.55` health damage, 18-tick fire interval, 120-tick full-magazine reload, `2.4`-diameter-per-tick projectile speed, `0.08` projectile radius, zero base bounces, `0.10` recoil speed, 12 active block ticks, and 240 block cooldown ticks.
- Bind named provisional values where research is silent: 240 movement-sweep bullet lifetime, `0.14`-diameter-per-tick hit knockback, `0.02`-diameter muzzle clearance, `0.85`-diameter block radius, `2.0`-diameter block-push radius, `0.18`-diameter-per-tick block-push speed, four bullet-contact iterations per tick, 60 spawn-lock ticks, 90 result-display ticks, and 2,048 live bullets. Bind the measured ring-out-to-result delay to six ticks.
- Add stable monotonic bullet IDs and a fixed-cap flat bullet collection. Spawn permitted held-fire shots before bullet movement, sweep every bullet in ID order against static oriented boxes, active block circles, and living player circles, and resolve equal-time contact as geometry, then block, then body, with source/player ID inside each kind.
- Destroy a base bullet on its first geometry or damaging body contact. A block contact negates direct damage, reflects the projectile away, transfers ownership to the blocker, preserves its remaining properties, consumes the reflected bullet's remaining movement in the same tick, and may therefore hit the former owner immediately.
- Apply real velocity impulses for shooter recoil, directional bullet knockback, and the one-shot block push. Every other living player within two diameters receives a constant `0.18` impulse directly away from the blocker and the blocker receives its equal-and-opposite impulse; exact center overlap uses positive X when the other player ID is greater and negative X otherwise. Every source-ordered static box overlapped by the `0.85` block circle applies one additional `0.18` impulse to the blocker along its overlap normal, producing the confirmed wall/floor-assisted launch without a second collision system.
- Track health, ammunition, reload, fire cooldown, block state, aim, alive state, bullets, overflow count, duel phase, duel number, result timer, and winner/draw state in the deterministic hash.
- Mark death immediately when health reaches zero or a body center crosses the arena's explicit bottom `killBoundaryY`, freeze active combat, and publish exactly one result after a six-tick resolving phase. Display it for 90 ticks, reset both players into a 60-tick bilateral spawn-lock phase on the same arena, then unlock them together. A simultaneous death is a draw under the binding provisional no-award rule.
- Render live aim arms, projectiles and trails, active block shields, health/ammunition/block HUD, and the winner/draw state in Godot. Player one uses A/D, Space, mouse aim and mouse buttons; player two retains Arrow movement/Up jump and gains I/J/K/L aim plus O/P fire/block as the local keyboard fallback.

## Why

Movement becomes a game only when aiming, recoil, projectiles, blocking, damage, ring-outs, and a repeatable duel can interact. This slice creates the smallest complete versus loop that can be played and replayed before scoring, drafts, cards, arena cadence, bots, and production presentation multiply the state space.

## Essential constraints

- Keep `Rounds.Sim` Godot-free, fixed at 60 Hz, deterministic, allocation-conscious in its tick path, and free of wall-clock, unordered iteration, platform trigonometry outside `Math/Trig.cs`, concurrency, and `System.Random`.
- Keep the existing circle-versus-oriented-box solver as the only bullet/environment path and add one deterministic swept-circle-versus-circle primitive rather than introducing engine physics or a parallel collision vocabulary.
- Validate every player's input before mutating the world so one non-finite aim rejects the whole tick. Validate tuning at construction, normalize aim once per input application, and retain a player's last direction for zero-length aim so fire never invents a platform-dependent angle.
- Order each active tick as timer advancement, aim/block/fire input with recoil, player movement, bullet movement and contacts, then death detection. A shot and its recoil act on their creation tick. `Spawning(60)` ignores both players' controls, `Active` runs combat, `Resolving(6)` freezes combat before publishing the stored outcome, and `Result(90)` freezes combat before reset; phase durations count later whole ticks rather than their transition tick.
- Treat fire as held repeat gated by cooldown, ammunition, and reload. Treat block as rising-edge activation gated by readiness; holding it cannot retrigger when cooldown finishes without a release.
- Use the simplest measured block machine: `Ready → Active(12) → Cooldown(240) → Ready`. Do not add a separate recovery phase until a distinct recovery behavior is observed.
- Update `docs/design/physics-and-maps.md` so its living block design matches the measured three-state machine and the bound static-contact self-launch rule rather than retaining the unsourced recovery phase.
- The public map format exposes only an explicit bottom kill boundary, so this slice implements bottom ring-outs only. Side/top kill policy remains a map-research gap rather than being inferred from camera or collision envelopes.
- Hard-cap live bullets at a named provisional 2,048. Drop the oldest live bullet before adding beyond the cap and increment a hashed overflow counter so card-driven growth cannot become silent or unbounded later.
- Process players by index and bullets by monotonic ID. Apply bullet results immediately in that stable order; bullets created by fire this tick may move this tick, while no collision callback may mutate the collection being enumerated.
- A bullet moves on its creation tick and expires immediately after completing its 240th movement sweep if no contact removed it earlier. Reflection continues only the unconsumed fraction of the current sweep and cannot collide with its new owner during that remainder. Resolve at most four contacts per bullet per tick and expire a bullet that exhausts that bound so two active shields cannot create an unbounded same-tick ping-pong loop.
- A duel reset increments `DuelNumber`, clears bullets and every per-duel player timer/value, and restores spawn/health/ammo/block while the world tick, RNG state, monotonic next-bullet ID, and cumulative overflow count continue unchanged.
- Keep player-player body collision, geometry bounces beyond the already stored base count, pierce, explosions, status effects, card hooks, match score, loser draft, victory, arena rotation, controller defaults, sound, camera zoom, and production assets out of this ticket.
- Preserve `World.CreateSmoke` as a supported endless deterministic duel harness. It may exercise combat and reset, but tests and tooling must not depend on Godot or repository-relative data paths.

## Evidence required

- Collision tests cover swept circle-circle face and glancing hits, initial overlap, zero relative motion, high-speed passage, stable normals, and exact-time environment/block/body ordering.
- Gun tests prove held-fire cadence, exactly three rounds, automatic full reload at 120 ticks, projectile radius/speed/damage, zero environment bounces, finite muzzle spawn, and recoil opposite aim without erasing prior velocity.
- Damage tests prove one base hit leaves the target alive near `0.45` health, two clean hits kill, knockback follows projectile travel, dead players cannot shoot or receive repeated results, and a bullet never damages its current owner.
- Block tests prove a 12-tick active window, no held-button retrigger, 240-tick cooldown, constant equal-and-opposite player push, source-ordered wall/floor self-launch, direct-damage negation, reflected ownership/velocity, and a reflected shot can damage its former owner during the remaining same-tick sweep.
- Capacity tests prove the live bullet count never exceeds 2,048, the oldest ID is dropped, and the overflow count enters the state hash.
- Duel tests prove one health kill, one bottom ring-out, the exact six-tick crossing-to-result delay, one simultaneous draw, bilateral input lock for 60 spawn ticks plus resolving/result phases, inward initial/reset aim, exact transient reset, preserved deterministic counters, and no side/top death inference.
- Determinism tests run the same arena, seed, combat input stream, kills, and resets twice to the same complete hash and change that hash when one aim sample changes.
- The Godot shell visibly renders the same arena, two independently aimed players, bullets, block state, HUD, spawn lock, and result/reset loop. In addition to editor/runtime smoke and controlled rendered frames, a precisely recorded native-window input exercise must drive player one's mouse aim/fire/block and player two's keyboard aim/fire/block through the documented shell bindings and show the resulting aim, bullet, shield, result, and reset state without scene-owned combat duplication.
- The complete repository gate passes with zero warnings, `spec/` remains byte-identical to its pre-implementation state, and the smoke harness terminates with a stable hash even across multiple duel resets.

## Work log

- 2026-08-14T11:56:30Z stage design start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Comparing the smallest complete deterministic duel with the current movement world, binding combat/controls/match facts, bullet architecture, and the future card-hook surface.
- 2026-08-14T11:59:40Z stage design end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Bounded the slice to one base-combat duel and deterministic reset, explicit sourced/provisional tuning, stable swept contacts, two independent local aim schemes, and exclusions that leave scoring, drafts, cards, maps, bots, and production presentation separately reviewable.
- 2026-08-14T12:06:12Z stage admission start session codex:019fffc8-8b3b-75a1-ae5d-f4b8ad895a73 — Cold-reading exact candidate `537d94d38ae2fe2da71cf69d3976828164000d8d` against the combat, camera, controls, match, movement, and living-design bindings plus the risk-4 self-admission bar.
- 2026-08-14T12:06:12Z stage admission end session codex:019fffc8-8b3b-75a1-ae5d-f4b8ad895a73 — Rejected missing ring-out delay, block self-launch mechanics, initial/reset aim, native control evidence, bilateral spawn lock, reflected-motion/lifetime boundaries, and the stale four-state block design.
- 2026-08-14T12:06:12Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Binding the measured six-tick result delay, deterministic block/player/static impulses, inward aim reset, spawn lock, reflection remainder, lifetime expiry, native input evidence, and required design update.
- 2026-08-14T12:07:39Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Bound four explicit duel phases, all phase durations, initial/reset aim, block self-launch and overlap fallback, same-tick reflection with a four-contact cap, exact lifetime expiry, native control proof, and the owning design correction.
- 2026-08-14T12:11:01Z stage admission start session codex:019ffff9-3be5-7da2-8811-5df376ffc9a4 — Cold-reading corrected exact candidate `bfbf7e7081ca47f87dc0945644081f0324ba6a8c` against all first-review findings, source provenance, dependencies, evidence, and risk.
- 2026-08-14T12:11:01Z stage admission end session codex:019ffff9-3be5-7da2-8811-5df376ffc9a4 — Rejected only the incorrect provisional classification of exact confirmed base health; all behavior, evidence, ordering, human-choice, and risk boundaries otherwise passed.
- 2026-08-14T12:11:01Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Moving `1.0` base health into the embedded sourced-tuning contract without changing its numeric behavior.
- 2026-08-14T12:11:01Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Bound exact base health to `spec/player.json` and left only genuinely unmeasured values in the provisional list.
- 2026-08-14T12:14:13Z stage admission start session codex:019ffff6-6034-76b1-96a2-b080ac183346 — Final cold read of exact corrected candidate `58fa739e64f9a2154fc34c535f5b413832f8ff94` against both rejection rounds, provenance, timer/contact edges, dependencies, evidence, and risk.
- 2026-08-14T12:14:13Z stage admission end session codex:019ffff6-6034-76b1-96a2-b080ac183346 — Admitted at risk 4 with no findings: one base duel, four fixed phases, one collision vocabulary, exact source ownership, named provisional values, and public/native evidence leave no human choice or material ambiguity.
- 2026-08-14T12:14:13Z stage implement start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Implementing validated aim, combat tuning, stable bullets and swept contacts, recoil/damage/block impulses, deterministic duel phases/reset/hash, live controls, HUD, and focused regressions without changing spec.
- 2026-08-14T12:35:42Z stage implement end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Added sourced/provisional combat tuning, validated independent aim, fire/reload/recoil, stable swept bullets, block reflection and impulses, four duel phases/reset, complete hashing, 26 new simulation facts—21 combat, four collision, and one aim-hash determinism case—and live HUD/rendering; the suite reached 68 passing tests.
- 2026-08-14T12:35:42Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — The supported gate passed 68 simulation plus 37 checker tests with zero warnings and deterministic hash `d2687f48fe6dd085`; a 75-frame Godot movie showed spawn-to-active rendering, then exact launched PID 98220 received P1 mouse motion/left/right buttons, P2 `I`/`O`/`P`, and P1 `A`, producing normalized aims `0.95,-0.32` and `0,1`, both eight-tick shields, one visible bullet, `BLUE WINS` in `Result`, and reset from duel 2 to duel 3.
- 2026-08-14T12:37:45Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Repeated the complete supported gate at the final tree: zero-warning build, repository checks, 68/68 simulation tests, 37/37 checker tests, deterministic 600-tick hash `d2687f48fe6dd085`, Godot editor/runtime smoke, ticket formatting, clean diff validation, unchanged `spec/`, and verified absence of the exact native process and 82 temporary capture artifacts.
- 2026-08-14T12:45:12Z stage review start session codex:01a00048-2aa0-7b33-87a8-edfba30eae67 — Reviewing exact implementation candidate `7d79215413ae022d8707d38491fdd181cc8dfa7b` against ticket 006, the immutable specs, full diff, exact-candidate gate, deterministic edge cases, native evidence, and living design.
- 2026-08-14T12:45:12Z stage review end session codex:01a00048-2aa0-7b33-87a8-edfba30eae67 — Rejected scale-unsafe normalization for extreme finite aim, missing isolation of exact block/static/overflow evidence, and an overstated test-count line; the full gate and native evidence otherwise passed.
- 2026-08-14T12:45:12Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Making aim normalization scale-safe across every finite nonzero vector, isolating constant player push, wall/floor launch, source ordering, and overflow hashing, and correcting the durable evidence count.
- 2026-08-14T12:50:20Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Replaced squared-norm aim detection with direct component nonzero checks and scaled normalization, made static block impulses explicitly source-ordered, added maximum/subnormal aim, exact bilateral push, combined floor/wall launch, reversed-storage contact-order, and overflow-only hash boundaries, and corrected the 26-test evidence label.
- 2026-08-14T12:50:20Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Re-running the focused 24-case combat suite, complete repository gate, spec/ticket/diff checks, and deterministic smoke after the first implementation-review correction.
- 2026-08-14T12:50:54Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Corrected gate passed with zero warnings, repository checks, 71/71 simulation tests, 37/37 checker tests, unchanged deterministic hash `d2687f48fe6dd085`, Godot editor/runtime smoke, ticket and diff checks, and no `spec/` change.
