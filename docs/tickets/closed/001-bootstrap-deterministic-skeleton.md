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
  - codex:019ffeb9-c691-70c3-b458-78885d222233
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
- 2026-08-14T05:30:00Z stage review start session codex:019ffeb9-c691-70c3-b458-78885d222233 — Reviewed exact candidate `0b91884a9bbe5c0683519356dfa34136e6959bbb` against ticket 001.
- 2026-08-14T05:33:00Z stage review end session codex:019ffeb9-c691-70c3-b458-78885d222233 — Changes requested: strengthen four determinism rules, fully pin the verification toolchain, correct the 1v1 rationale, and remove an original card name from the concept; full findings are in the native review result for this session.
- 2026-08-14T05:33:00Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Correcting every review finding and the related five-choice draft assumption before producing a new candidate.
- 2026-08-14T05:34:40Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Replaced Node checks with a pinned-.NET checker and realistic bypass tests, hash-verified tool downloads, pinned CI references, corrected the 1v1 rationale, and replaced the three-card concept with five original choices.
- 2026-08-14T05:34:40Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Re-running the complete gate, formatting, ticket validation, and staged-diff checks on the correction candidate.
- 2026-08-14T05:35:25Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Nine xUnit tests passed, including the four review bypasses and math aliasing; the exact build had zero warnings; the 600-tick hash remained `f250d549cfb52a8b`; Godot editor/runtime smoke, formatting, ticket format, and diff checks passed.
- 2026-08-14T05:36:00Z stage review start session codex:019ffeb9-c691-70c3-b458-78885d222233 — Re-reviewed the complete corrected candidate `e572dd28328af7891ca93802d0b4d63f33911cd2` and each prior finding.
- 2026-08-14T05:39:00Z stage review end session codex:019ffeb9-c691-70c3-b458-78885d222233 — Approved exact candidate `e572dd28328af7891ca93802d0b4d63f33911cd2`; all four findings were resolved and no new defect was found.
- 2026-08-14T05:39:27Z stage integrate start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Fast-forwarding the approved candidate onto unchanged `main` at `b9073b6a9c110b5fbca5e242d49bd03a8cecef12`.
- 2026-08-14T05:39:27Z stage integrate end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — `main` now contains approved candidate `e572dd28328af7891ca93802d0b4d63f33911cd2`; post-integration bookkeeping is recorded by a later unreviewed documentation commit.
