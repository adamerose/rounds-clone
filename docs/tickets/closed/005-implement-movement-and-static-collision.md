---
format: 3
status: closed
created: 2026-08-14T11:08:00Z
origin: agent-proposed
tags: [implementation, simulation, movement, physics, maps]
value: 9
risk: 4
depends-on: [2, 4]
sessions:
  - codex:019ffea8-55c5-79b3-96b2-da3210d67d84
  - codex:019ffff6-6034-76b1-96a2-b080ac183346
  - codex:019ffff9-3be5-7da2-8811-5df376ffc9a4
  - codex:019fffc8-8b3b-75a1-ae5d-f4b8ad895a73
---

# Implement deterministic movement and static arena collision

Two local players can run, jump, fall, land, and slide against the real static oriented-box geometry of `arena-006` through the deterministic simulation and the Godot shell.

## Outcome

- Add immutable arena, oriented-box, spawn, and player-tuning types to `Rounds.Sim`, with a public loader that consumes the committed map catalog without a Godot dependency.
- Load only `static` primitives into ordinary collision and preserve source order as the deterministic tie-break order; `hazard-visual` and `dynamic-visual` primitives never silently become static colliders.
- Implement deterministic circle-versus-oriented-box overlap, sweep, earliest-hit selection, and four-iteration move-and-slide behavior without tunnelling through thin or rotated platforms.
- Implement base horizontal acceleration, sustained speed, air control, gravity, ground friction, jump storage, landing refill, grounded state, and edge-triggered jumping from the binding player facts.
- Add named provisional constants for the sourced four-tick jump buffer, jump release, grounding threshold, probe distance, and collision skin where the research does not bind exact values.
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
- Movement tests prove acceleration and sustained speed agree with the binding targets, an unmodified jump reaches its specified height and apex-time tolerance, landing restores the one stored jump, and release shortens the jump.
- Jump-state tests prove walking off preserves the stored jump beyond any short grace window, a ground jump consumes it without recent ground contact granting another, and a buffered press while empty executes only after landing refills the jump.
- Catalog tests prove `arena-006` loads 15 source-ordered static boxes and two supported spawn regions, rejects unknown or malformed arenas, and never loads hazard or dynamic visual boxes as static collision.
- A deterministic regression runs the same arena, seed, and input stream twice to the same complete state hash and changes that hash when one movement input changes.
- The Godot shell loads the same arena data, advances both keyboard-controlled players through `Rounds.Sim`, and passes editor plus runtime smoke without duplicating map geometry in a scene.
- The complete repository gate passes with zero warnings, and `spec/` remains byte-identical to its pre-implementation state.

## Work log

- 2026-08-14T11:08:00Z stage design start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Comparing the simplest immutable arena and kinematic-controller design with the existing deterministic smoke world and the binding movement, map, and architecture records.
- 2026-08-14T11:08:00Z stage design end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Bounded the first playable slice to static arena 006, one circle-versus-oriented-box sweep, researched movement constants, explicit provisional feel constants, live Godot rendering, and public-interface verification.
- 2026-08-14T11:11:44Z stage admission start session codex:019ffff6-6034-76b1-96a2-b080ac183346 — Cold-reading exact ticket candidate `9afcb7116bccd324c6c2449cee41bee38c5f6968` against the self-admission bar, closed dependencies, binding jump rules, arena facts, risk, and evidence.
- 2026-08-14T11:11:44Z stage admission end session codex:019ffff6-6034-76b1-96a2-b080ac183346 — Rejected conventional coyote time because the indefinitely stored air jump makes it redundant or turns it into an incorrect second-jump grant; the rest of the risk-4 contract passed.
- 2026-08-14T11:11:44Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Removing coyote machinery from the ticket and owning design, then replacing it with observable stored-jump and landing-buffer boundary tests.
- 2026-08-14T11:11:44Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Bound one persistent stored jump after ledge departure, no recent-ground bonus after consumption, and a sourced four-tick buffer that can fire only after landing refills the jump.
- 2026-08-14T11:14:17Z stage admission start session codex:019ffff9-3be5-7da2-8811-5df376ffc9a4 — Cold-reading amended exact candidate `b09b9fd1814443f3dfeaea76c80541bc116ede55` against the self-admission bar, coyote correction, jump-state boundaries, closed dependencies, and risk.
- 2026-08-14T11:14:17Z stage admission end session codex:019ffff9-3be5-7da2-8811-5df376ffc9a4 — Admitted at risk 4 with no findings: the bounded static-arena slice, deterministic interfaces, exclusions, provisional constants, and public evidence leave no unresolved human decision.
- 2026-08-14T11:14:17Z stage implement start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Implementing immutable arena loading, deterministic oriented-box collision, researched player movement, stored-jump state, live arena rendering, and boundary regressions without changing spec.
- 2026-08-14T11:27:31Z stage implement end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Delivered the embedded stream-loadable catalog, rounded oriented-box sweep and slide, sourced movement and stored-jump state, complete hashing, and live arena rendering with two local control sets.
- 2026-08-14T11:27:31Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Running focused boundaries, the complete release gate, deterministic replay, spec immutability, Godot editor/runtime smoke, and GPU-rendered visual inspection before candidate review.
- 2026-08-14T11:28:29Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Passed 39 simulation and 37 checker tests, repository checks, zero-warning release build, repeated 600-tick hash `28bca5e37a7a3255`, unchanged spec, Godot editor/runtime smoke, and a clean GPU frame of the live arena.
- 2026-08-14T11:28:29Z stage review start session codex:019fffc8-8b3b-75a1-ae5d-f4b8ad895a73 — Sending the complete detached candidate to a fresh non-author review against ticket 005 and the supported public evidence.
- 2026-08-14T11:39:10Z stage review end session codex:019fffc8-8b3b-75a1-ae5d-f4b8ad895a73 — Rejected exact candidate `c523642e731be3003aa845b91e5dad2864d4d120`: unsupported spawn IDs survived public loading, and a linear epsilon compared to squared distance destabilized near-corner overlap normals.
- 2026-08-14T11:39:10Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Adding regressions for static spawn support and near-corner overlap normals, then enforcing arena invariants and dimensionally correct collision tolerances.
- 2026-08-14T11:41:18Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Reproduced both findings with failing public tests, then rejected missing support references, protected direct arena construction, and compared squared corner distance with a squared epsilon.
- 2026-08-14T11:41:18Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Re-running the exact two regressions, full simulation suite, repository policy, release build, deterministic smoke, spec immutability, and Godot smoke for the corrected candidate.
- 2026-08-14T11:41:18Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Passed both new boundaries, 41 simulation and 37 checker tests, repository checks, zero-warning release build, repeated hash `28bca5e37a7a3255`, unchanged spec, and Godot editor/runtime smoke.
- 2026-08-14T11:41:18Z stage review start session codex:019ffff9-3be5-7da2-8811-5df376ffc9a4 — Sending the corrected exact candidate to an isolated non-author context that admitted the frozen contract but has not reviewed or authored its implementation.
- 2026-08-14T11:47:24Z stage review end session codex:019ffff9-3be5-7da2-8811-5df376ffc9a4 — Rejected exact candidate `71410dcd2c7ecaf84745de01ae572f58a55b9d84`: the loader skipped arbitrary primitive-role misspellings as though they were the two supported non-static visual roles.
- 2026-08-14T11:47:24Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Reproducing silent geometry deletion through the public loader, then restricting role handling to static, hazard visual, and dynamic visual values.
- 2026-08-14T11:49:11Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Replaced permissive role filtering with an explicit supported-role switch and added a mutation-checked public regression for misspelled roles.
- 2026-08-14T11:49:11Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Re-running the malformed-role boundary, complete release gate, deterministic replay, spec immutability, ticket format, and Godot smoke.
- 2026-08-14T11:49:11Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Passed the corrected regression, 42 simulation and 37 checker tests, repository checks, zero-warning release build, repeated hash `28bca5e37a7a3255`, unchanged spec, and Godot editor/runtime smoke.
- 2026-08-14T11:49:11Z stage review start session codex:019ffff6-6034-76b1-96a2-b080ac183346 — Sending the second correction to another isolated non-author context that challenged the original contract but has not reviewed or authored the implementation.
