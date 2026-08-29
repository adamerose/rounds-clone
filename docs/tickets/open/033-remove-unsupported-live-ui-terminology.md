---
format: 3
status: ready
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

- Every `Main` render route—including ordinary play, replay, incomplete-fidelity evidence, and agent playtesting—no longer renders aim coordinates, bullet or bounce counters, internal duel and match phases, row-stable arena IDs, or textual block-state diagnostics.
- Draft cards show their exact sourced ROUNDS display names, but no project-authored summary or derived effect line. Unsupported card explanation remains absent until ticket 019 verifies the current cards' mechanics and presentation summaries.
- Internal arena IDs, simulation phases, causal diagnostics, and clean-room research summaries remain available to tests, protocols, logs, and source records where they are not player-visible.
- The existing playable-subset boundary, exact card pool and names, match rules, controls, deterministic behavior, and replay bytes do not change. This removal does not claim that the remaining HUD or draft presentation matches ROUNDS; tickets 016–021 retain that work.
- `README.md` describes the reduced live shell accurately and no longer promises the removed effect summaries, arena ID, bounce budgets, or block-state text.

## Decisions

- Removing strings that are provably project-authored needs no new target capture. This ticket deletes unsupported live claims; it does not replace them with guessed ROUNDS wording or presentation.
- Remove the runtime `StatCardDefinition.Summary` surface and its hard-coded `SummaryFor` catalog so project-authored explanations cannot be accidentally rendered as ROUNDS card copy. Preserve the clean-room research `summary` fields in `spec/cards.json`; they are source notes, not original card text or shipped UI.
- Keep the exact sourced `DisplayName` mapping and player-visible acquired-card names. Ordinary uses of `arena-###` in deterministic formats and tests remain stable internal identifiers.
- Remove the complete `blockText` draw path, including both `BLOCK READY` and phase/tick text. Preserve block input, simulation state, active-block drawing, cooldown behavior, and every nontextual mechanic; sourcing any future block prompt belongs to tickets 020–021.
- Do not change functional prompts, winner feedback, health/ammunition display, drawing style, projectile presentation, card mechanics, arena geometry, controls, or the explicit incomplete-fidelity notice. Their fidelity belongs to the existing tickets and requires direct target evidence.
- Historical tickets, decisions, recovery records, progress reports, and superseded concept images keep their original wording.

## Evidence required

- Focused checks prove no `Main` route can render `StatCardDefinition.Summary`, derived effect lines, arena IDs, aim coordinates, bullet/bounce counts, duel/match phase enum names, `BLOCK READY`, or block phase/tick text.
- Catalog tests still prove the 16 supported IDs map to the exact sourced ROUNDS names, and no runtime summary field or hard-coded summary catalog remains.
- The ordinary shell, replay shell, incomplete-fidelity route, and agent-playtest route retain the same state progression and exact boundary/protocol behavior in headless tests; simulation hashes, protected replay bytes, `spec/` bytes, dependency files, and arena/card mechanics remain unchanged.
- Bounded inspection proves `README.md` names only the live information that remains visible and does not describe any removed diagnostic or project-authored card explanation as shipped behavior.
- The zero-warning build, focused and applicable test suites, repository checker, ticket checker, and `git diff --check` pass without launching Godot, a browser, any visible window, or an input/capture helper.

## Work log

- 2026-08-29T23:20:16Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Auditing live terminology after the faithful-identity correction exposed internal diagnostics and project-authored card explanations that still appear as ordinary game UI.
- 2026-08-29T23:20:45Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Split a fail-closed removal from the broader presentation ticket: exact ROUNDS card names stay live, project-authored explanations and internal telemetry disappear, and every evidence-dependent visual or mechanical change remains with tickets 016–021.
- 2026-08-29T23:21:42.7711275Z stage admission start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04f3a-8b6c-7471-91ea-749dd8a0df5b — Cold-reading the removal scope against active documentation, every `Main` startup route, block feedback, and the ticket 019/020 ownership graph.
- 2026-08-29T23:26:25.3728811Z stage admission end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04f3a-8b6c-7471-91ea-749dd8a0df5b — Rejected a missing ticket 019 dependency edge, ambiguous `BLOCK READY` treatment, a README promise that would become false, and omission of the agent-playtest render route.
- 2026-08-29T23:26:59.9104995Z stage correction start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Correcting all four findings while keeping the change removal-only and preserving block mechanics, functional controls, and broader presentation ownership.
- 2026-08-29T23:27:32.3911171Z stage correction end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Ordered ticket 019 after this removal, removed the entire textual block diagnostic while preserving mechanics, assigned the README correction, and covered every `Main` renderer route including agent playtesting.
- 2026-08-29T23:28:12.6199379Z stage admission start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04f3a-8b6c-7471-91ea-749dd8a0df5b — Freshly judging both corrected ticket records, the full `Main` route set, block feedback boundary, active README description, and the 033→019→020/021 dependency order.
- 2026-08-29T23:29:29.1482472Z stage admission end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04f3a-8b6c-7471-91ea-749dd8a0df5b — Admitted at risk 3 with no findings; the contract is removal-only, dependency-safe, mechanically judgeable without GUI evidence, and preserves exact names, functional feedback, controls, mechanics, maps, replays, and research summaries.
- 2026-08-29T23:33:16Z stage implementation start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Removing only unsupported player-visible diagnostics and project-authored card explanations, then adding a repository guard scoped to the live `Main` and runtime card surfaces.
- 2026-08-29T23:37:48.3588303Z stage implementation end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Removed the unconditional aim, projectile-count, bounce, duel, match-phase, arena-ID, block-text, summary, and derived-effect draw paths; deleted the runtime summary API and catalog; corrected the README; and added focused product-identity enforcement while preserving exact names and functional presentation. A locked restore populated the detached worktree after its initial no-restore build found no asset files; the resulting solution build had zero warnings, 87 focused simulation/game-route tests plus 4 focused checker tests passed, 251 applicable simulation tests plus 104 checker tests passed, repository and ticket checks passed, protected spec/replay/dependency/mechanics/map bytes did not change, and whitespace passed without launching any GUI, Godot process, browser, renderer, capture, input, or GPU work.
