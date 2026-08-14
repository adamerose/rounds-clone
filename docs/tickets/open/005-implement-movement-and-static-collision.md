---
format: 3
status: idea
created: 2026-08-14T11:08:00Z
origin: agent-proposed
tags: [implementation, simulation, movement, physics, maps]
value: 9
risk: 4
depends-on: [2, 4]
sessions:
  - codex:019ffea8-55c5-79b3-96b2-da3210d67d84
---

# Implement deterministic movement and static arena collision

Two local players can run, jump, fall, land, and slide against the real static oriented-box geometry of `arena-006` through the deterministic simulation and the Godot shell.

## Outcome

- Add immutable arena, oriented-box, spawn, and player-tuning types to `Rounds.Sim`, with a public loader that consumes the committed map catalog without a Godot dependency.
- Load only `static` primitives into ordinary collision and preserve source order as the deterministic tie-break order; `hazard-visual` and `dynamic-visual` primitives never silently become static colliders.
- Implement deterministic circle-versus-oriented-box overlap, sweep, earliest-hit selection, and four-iteration move-and-slide behavior without tunnelling through thin or rotated platforms.
- Implement base horizontal acceleration, sustained speed, air control, gravity, ground friction, jump storage, landing refill, grounded state, and edge-triggered jumping from the binding player facts.
- Add named provisional constants for coyote time, jump buffering, jump release, grounding threshold, probe distance, and collision skin where the research does not bind exact values.
- Spawn a two-player world from `arena-006`'s two catalog regions and include the arena identity plus all movement state in the deterministic hash.
- Replace the fixed Godot concept-stage fighters and platforms with a world-space rendering of the loaded arena and the live simulation positions, retaining an original presentation and two local keyboard control sets.

## Why

Movement against real level geometry is the foundation for shooting, recoil, ring-outs, bots, replays, camera behavior, cards, and meaningful self-play, while a static no-hazard arena isolates that foundation from unrelated lifecycle and behavior-module work.

## Essential constraints

- Keep `Rounds.Sim` free of Godot types, wall-clock access, unordered iteration, concurrency, `System.Random`, and unapproved trigonometric calls.
- Use one player diameter as one world unit, a circular player collider with radius `0.5`, and velocities expressed per 60 Hz simulation tick.
- Bind defaults to run speed `0.10`, ground acceleration `0.014`, air-control ratio `0.8`, gravity `0.007`, jump speed `0.25`, one stored jump, and ground velocity retention `0.72` from `spec/player.json` without changing `spec/`.
- Use an original deterministic sine-and-cosine implementation behind `Rounds.Sim.Math.Trig` for catalog rotations instead of calling platform trigonometry elsewhere in the simulation.
- Resolve initial overlap and zero-length motion deliberately, choose the earliest collision with primitive source order as the exact-time tie-break, and use named tolerances rather than scattered epsilon literals.
- Treat jump input as a held button whose rising edge consumes a buffered jump; releasing it while rising applies one provisional jump-cut factor.
- Keep player-player collision, moving platforms, saw contact, destructibles, kill handling, combat, scoring, camera zoom, cards, and audio out of this ticket.
- Preserve `World.CreateSmoke` or replace it with an equally simple supported deterministic harness entry point rather than making tests depend on Godot or repository-relative paths.

## Evidence required

- Unit tests cover axis-aligned and rotated hits, corner contact, initial overlap, zero motion, exact-time tie order, stable normals, sliding, and high-speed passage against a thin platform.
- Movement tests prove acceleration and sustained speed agree with the binding targets, an unmodified jump reaches its specified height and apex-time tolerance, landing restores the one stored jump, release shortens the jump, and coyote plus buffer windows are deterministic at their boundaries.
- Catalog tests prove `arena-006` loads 15 source-ordered static boxes and two supported spawn regions, rejects unknown or malformed arenas, and never loads hazard or dynamic visual boxes as static collision.
- A deterministic regression runs the same arena, seed, and input stream twice to the same complete state hash and changes that hash when one movement input changes.
- The Godot shell loads the same arena data, advances both keyboard-controlled players through `Rounds.Sim`, and passes editor plus runtime smoke without duplicating map geometry in a scene.
- The complete repository gate passes with zero warnings, and `spec/` remains byte-identical to its pre-implementation state.

## Work log

- 2026-08-14T11:08:00Z stage design start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Comparing the simplest immutable arena and kinematic-controller design with the existing deterministic smoke world and the binding movement, map, and architecture records.
- 2026-08-14T11:08:00Z stage design end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Bounded the first playable slice to static arena 006, one circle-versus-oriented-box sweep, researched movement constants, explicit provisional feel constants, live Godot rendering, and public-interface verification.
