---
format: 3
status: idea
created: 2026-08-29T23:20:16Z
origin: system-detected
tags: ["product-fidelity", "presentation", "terminology"]
value: 9
risk: 3
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: [15]
supersedes: []
split-from: [20]
---

# Remove unsupported live UI terminology

The shipped shell still presents internal simulation diagnostics and project-authored card explanations as ordinary game UI. Remove those unsupported labels now so the playable subset shows exact sourced ROUNDS card names without implying that internal IDs, telemetry, or unsourced explanatory copy belong to ROUNDS.

## Outcome

- Ordinary play, replay, and the incomplete-fidelity evidence route no longer render aim coordinates, bullet or bounce counters, internal duel and match phases, row-stable arena IDs, or block-state countdown diagnostics.
- Draft cards show their exact sourced ROUNDS display names, but no project-authored summary or derived effect line. Unsupported card explanation remains absent until ticket 019 verifies the current cards' mechanics and presentation summaries.
- Internal arena IDs, simulation phases, causal diagnostics, and clean-room research summaries remain available to tests, protocols, logs, and source records where they are not player-visible.
- The existing playable-subset boundary, exact card pool and names, match rules, controls, deterministic behavior, and replay bytes do not change. This removal does not claim that the remaining HUD or draft presentation matches ROUNDS; tickets 016–021 retain that work.

## Decisions

- Removing strings that are provably project-authored needs no new target capture. This ticket deletes unsupported live claims; it does not replace them with guessed ROUNDS wording or presentation.
- Remove the runtime `StatCardDefinition.Summary` surface and its hard-coded `SummaryFor` catalog so project-authored explanations cannot be accidentally rendered as ROUNDS card copy. Preserve the clean-room research `summary` fields in `spec/cards.json`; they are source notes, not original card text or shipped UI.
- Keep the exact sourced `DisplayName` mapping and player-visible acquired-card names. Ordinary uses of `arena-###` in deterministic formats and tests remain stable internal identifiers.
- Do not change functional prompts, winner feedback, health/ammunition display, drawing style, projectile presentation, card mechanics, arena geometry, controls, or the explicit incomplete-fidelity notice. Their fidelity belongs to the existing tickets and requires direct target evidence.
- Historical tickets, decisions, recovery records, progress reports, and superseded concept images keep their original wording.

## Evidence required

- Focused checks prove `game/Main.cs` cannot render `StatCardDefinition.Summary`, derived effect lines, arena IDs, aim coordinates, bullet/bounce counts, duel/match phase enum names, or block countdown diagnostics.
- Catalog tests still prove the 16 supported IDs map to the exact sourced ROUNDS names, and no runtime summary field or hard-coded summary catalog remains.
- The ordinary shell, replay shell, and incomplete-fidelity route retain the same state progression and exact boundary text in headless tests; simulation hashes, protected replay bytes, `spec/` bytes, dependency files, and arena/card mechanics remain unchanged.
- The zero-warning build, focused and applicable test suites, repository checker, ticket checker, and `git diff --check` pass without launching Godot, a browser, any visible window, or an input/capture helper.

## Work log

- 2026-08-29T23:20:16Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Auditing live terminology after the faithful-identity correction exposed internal diagnostics and project-authored card explanations that still appear as ordinary game UI.
- 2026-08-29T23:20:45Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Split a fail-closed removal from the broader presentation ticket: exact ROUNDS card names stay live, project-authored explanations and internal telemetry disappear, and every evidence-dependent visual or mechanical change remains with tickets 016–021.
