---
format: 3
status: idea
created: 2026-08-14T11:56:30Z
origin: agent-proposed
tags: [implementation, simulation, combat, blocking, lifecycle]
value: 10
risk: 4
depends-on: [2, 5]
sessions:
  - codex:019ffea8-55c5-79b3-96b2-da3210d67d84
---

# Implement deterministic base combat and a complete duel loop

Two local players can independently aim, fire a base gun, use recoil, take damage and knockback, block and reflect shots, die by damage or bottom ring-out, see a duel result, and reset into another deterministic duel in the live arena.

## Outcome

- Extend `PlayerInput` with a finite world-space aim vector and held fire/block semantics; zero aim retains the player's last valid direction, while all nonzero aim is normalized inside `Rounds.Sim`.
- Add immutable combat tuning loaded from the embedded binding combat facts for three rounds, `0.55` health damage, 18-tick fire interval, 120-tick full-magazine reload, `2.4`-diameter-per-tick projectile speed, `0.08` projectile radius, zero base bounces, `0.10` recoil speed, 12 active block ticks, and 240 block cooldown ticks.
- Bind named provisional values where research is silent: `1.0` base health, 240-tick bullet lifetime, `0.14`-diameter-per-tick hit knockback, `0.02`-diameter muzzle clearance, `0.85`-diameter block radius, `2.0`-diameter block-push radius, `0.18`-diameter-per-tick block-push speed, 90 result ticks, and 2,048 live bullets.
- Add stable monotonic bullet IDs and a fixed-cap flat bullet collection. Spawn permitted held-fire shots before bullet movement, sweep every bullet in ID order against static oriented boxes, active block circles, and living player circles, and resolve equal-time contact as geometry, then block, then body, with source/player ID inside each kind.
- Destroy a base bullet on its first geometry or damaging body contact. A block contact negates direct damage, reflects the projectile away, transfers ownership to the blocker, preserves its remaining properties, and lets it threaten the former owner.
- Apply real velocity impulses for shooter recoil, directional bullet knockback, and the one-shot radial block push. Player input may accelerate against an impulse but never replaces velocity wholesale.
- Track health, ammunition, reload, fire cooldown, block state, aim, alive state, bullets, overflow count, duel phase, duel number, result timer, and winner/draw state in the deterministic hash.
- Resolve exactly one duel result when health reaches zero or a body center crosses the arena's explicit bottom `killBoundaryY`. Ignore inputs while resolved, clear transient combat state, and respawn both players on the same arena after a provisional 90-tick result display. A simultaneous death is a draw under the binding provisional no-award rule.
- Render live aim arms, projectiles and trails, active block shields, health/ammunition/block HUD, and the winner/draw state in Godot. Player one uses A/D, Space, mouse aim and mouse buttons; player two retains Arrow movement/Up jump and gains I/J/K/L aim plus O/P fire/block as the local keyboard fallback.

## Why

Movement becomes a game only when aiming, recoil, projectiles, blocking, damage, ring-outs, and a repeatable duel can interact. This slice creates the smallest complete versus loop that can be played and replayed before scoring, drafts, cards, arena cadence, bots, and production presentation multiply the state space.

## Essential constraints

- Keep `Rounds.Sim` Godot-free, fixed at 60 Hz, deterministic, allocation-conscious in its tick path, and free of wall-clock, unordered iteration, platform trigonometry outside `Math/Trig.cs`, concurrency, and `System.Random`.
- Keep the existing circle-versus-oriented-box solver as the only bullet/environment path and add one deterministic swept-circle-versus-circle primitive rather than introducing engine physics or a parallel collision vocabulary.
- Validate every player's input before mutating the world so one non-finite aim rejects the whole tick. Validate tuning at construction, normalize aim once per input application, and retain a player's last direction for zero-length aim so fire never invents a platform-dependent angle.
- Order each active tick as timer advancement, aim/block/fire input with recoil, player movement, bullet movement and contacts, then death/result resolution. A shot and its recoil act on their creation tick; a result created this tick retains its full 90-tick display for later resolved ticks.
- Treat fire as held repeat gated by cooldown, ammunition, and reload. Treat block as rising-edge activation gated by readiness; holding it cannot retrigger when cooldown finishes without a release.
- Use the simplest measured block machine: `Ready → Active(12) → Cooldown(240) → Ready`. Do not add a separate recovery phase until a distinct recovery behavior is observed.
- The public map format exposes only an explicit bottom kill boundary, so this slice implements bottom ring-outs only. Side/top kill policy remains a map-research gap rather than being inferred from camera or collision envelopes.
- Hard-cap live bullets at a named provisional 2,048. Drop the oldest live bullet before adding beyond the cap and increment a hashed overflow counter so card-driven growth cannot become silent or unbounded later.
- Process players by index and bullets by monotonic ID. Apply bullet results immediately in that stable order; bullets created by fire this tick may move this tick, while no collision callback may mutate the collection being enumerated.
- A duel reset increments `DuelNumber`, clears bullets and every per-duel player timer/value, and restores spawn/health/ammo/block while the world tick, RNG state, monotonic next-bullet ID, and cumulative overflow count continue unchanged.
- Keep player-player body collision, geometry bounces beyond the already stored base count, pierce, explosions, status effects, card hooks, match score, loser draft, victory, arena rotation, controller defaults, sound, camera zoom, and production assets out of this ticket.
- Preserve `World.CreateSmoke` as a supported endless deterministic duel harness. It may exercise combat and reset, but tests and tooling must not depend on Godot or repository-relative data paths.

## Evidence required

- Collision tests cover swept circle-circle face and glancing hits, initial overlap, zero relative motion, high-speed passage, stable normals, and exact-time environment/block/body ordering.
- Gun tests prove held-fire cadence, exactly three rounds, automatic full reload at 120 ticks, projectile radius/speed/damage, zero environment bounces, finite muzzle spawn, and recoil opposite aim without erasing prior velocity.
- Damage tests prove one base hit leaves the target alive near `0.45` health, two clean hits kill, knockback follows projectile travel, dead players cannot shoot or receive repeated results, and a bullet never damages its current owner.
- Block tests prove a 12-tick active window, no held-button retrigger, 240-tick cooldown, one activation push, direct-damage negation, reflected ownership/velocity, and a reflected shot can damage its former owner.
- Capacity tests prove the live bullet count never exceeds 2,048, the oldest ID is dropped, and the overflow count enters the state hash.
- Duel tests prove one health kill, one bottom ring-out, one simultaneous draw, input lock during the 90-tick result, exact transient reset, preserved deterministic counters, and no side/top death inference.
- Determinism tests run the same arena, seed, combat input stream, kills, and resets twice to the same complete hash and change that hash when one aim sample changes.
- The Godot shell visibly renders the same arena, two independently aimed players, bullets, block state, HUD, and result/reset loop; editor import, runtime smoke, and a controlled rendered-frame sequence pass without duplicated combat state in the scene.
- The complete repository gate passes with zero warnings, `spec/` remains byte-identical to its pre-implementation state, and the smoke harness terminates with a stable hash even across multiple duel resets.

## Work log

- 2026-08-14T11:56:30Z stage design start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Comparing the smallest complete deterministic duel with the current movement world, binding combat/controls/match facts, bullet architecture, and the future card-hook surface.
- 2026-08-14T11:59:40Z stage design end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Bounded the slice to one base-combat duel and deterministic reset, explicit sourced/provisional tuning, stable swept contacts, two independent local aim schemes, and exclusions that leave scoring, drafts, cards, maps, bots, and production presentation separately reviewable.
