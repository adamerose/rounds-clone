---
format: 3
status: closed
created: 2026-08-14T05:06:10Z
origin: human-request
tags: [bootstrap, simulation, ci]
value: 10
risk: 4
depends-on: []
sessions:
  - codex:019ffea8-55c5-79b3-96b2-da3210d67d84
---

# Bootstrap the deterministic game skeleton

The repository can build and test the simulation independently from the presentation engine, and every commit runs the mechanical rules that protect deterministic replay work.

## Outcome

- Create the pinned .NET solution and projects described in `docs/architecture.md`.
- Keep `Rounds.Sim` free of Godot and expose a minimal deterministic `World`, `Input`, `Sim.Step`, and `Sim.Hash` boundary.
- Add a console harness that can run a repeatable smoke simulation.
- Add automated tests for the initial simulation and hash behavior.
- Add repository checks for every locked determinism rule that can be enforced mechanically at this stage.
- Add a Godot 4 C# shell that references `Rounds.Sim` without putting game rules in the engine layer.
- Run the checks in continuous integration.

## Why

Every later feature depends on a fast deterministic core, a playable rendering shell, and checks that catch architectural drift before it spreads through the card catalog.

## Essential constraints

- Target .NET 8 even when a newer SDK is installed locally.
- Pin dependency and tool versions exactly.
- Keep the initial simulation deliberately small; movement, collision, combat, and match rules belong to later tickets backed by research.
- CI must use the same entry point as local verification.
- Do not copy original Rounds art, audio, text, or code.

## Evidence required

- `dotnet build` succeeds for the full solution.
- `dotnet test` passes.
- The repository check entry point passes and catches representative forbidden constructs in fixture files or unit tests.
- Two smoke runs with the same seed and input stream print the same final state hash.
- The Godot shell loads successfully when the pinned editor/runtime is available; if the runtime cannot be provisioned during this ticket, preserve the exact validation command and prove the C# shell builds.

## Work log

- 2026-08-14T05:06:10Z stage design start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Admitted from Adam's explicit build directive and the binding bootstrap-first dependency in `GOAL.md`; no product decision remains open.
- 2026-08-14T05:12:00Z stage design end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Pinned the supported toolchain, defined the bootstrap boundary, and selected an original presentation identity that preserves gameplay readability without copying source assets.
- 2026-08-14T05:12:00Z stage implement start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Building the reproducible solution, deterministic seed boundary, automated rules, engine shell, and CI entry point.
- 2026-08-14T05:21:40Z stage implement end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Added the pinned local tool bootstrap, four-project solution, pure simulation seed boundary, PCG, stable hash, harness, tests, seven-rule checker, Godot shell, visual specification, usage guide, and CI workflow.
- 2026-08-14T05:21:40Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Ran the exact repository gate plus formatting, ticket-format, and diff checks.
- 2026-08-14T05:21:40Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Six xUnit tests and two Node checker tests passed; build completed with zero warnings; two 600-tick smoke runs matched hash `f250d549cfb52a8b`; Godot 4.7.1 headless editor import and runtime smoke both exited zero; formatting, ticket format, and diff checks passed.
