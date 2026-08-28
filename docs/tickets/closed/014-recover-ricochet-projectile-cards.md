---
format: 3
status: closed
created: 2026-08-28T19:17:45Z
origin: system-detected
tags: [recovery, cards, projectiles, combat]
value: 9
risk: 4
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: [13]
---

# Recover ricochet projectile cards

The authoritative match gains the four deterministic ricochet-card behaviors preserved by orphan logical ticket 009, including sourced names and visible bounce state. This matters because ticket 013 proved that the only complete clean orphan chain contains a valuable core-combat slice, but its final head mixed independently testable card behavior with rejected and unfinished native-window safety work that was never approved for integration.

## Outcome

- Expand the supported deterministic draft pool from 12 to 16 stable IDs by adding `bouncy`, `fast-forward`, `mayhem`, and `spray`, with the exact sourced Rounds names Bouncy, Fast Forward, Mayhem, and Spray.
- Fold those cards into immutable combat profiles using the selected source behavior: additive non-negative geometry-bounce counts; sign-aware attack speed; flat-then-speed-then-Quick-Reload reload composition; additive positive damage and one multiplicative factor per negative damage effect; and the existing bounded folds for projectile speed and ammunition.
- Give every spawned bullet its shooter's derived geometry-bounce budget. A geometry contact with budget remaining moves to the exact contact, applies the existing skin, reflects velocity and the exact unused sweep remainder, consumes one bounce, retains ownership, and continues within the existing contact cap.
- Preserve zero-bounce geometry despawn, geometry/block/body equal-time priority, block reflection and ownership transfer without consuming geometry bounces, body-hit damage/despawn, the 240-sweep lifetime, and deterministic four-contact overflow.
- Hash custom profile bounce state and every live bullet's remaining bounce count while preserving the protected all-vanilla duel hash and replay bytes.
- Show sourced card names and implemented effect summaries in the existing draft UI, and show live remaining-bounce budgets in the existing debug presentation without adding new native-window control or safety machinery.
- Keep the existing first-to-five scoring, arena cadence, layout, controls, `spec/`, `replays/`, and all eight frozen recovery artifacts unchanged.

## Decisions

- Preserve orphan logical number 009 only as provenance. This ticket owns a new authoritative lifecycle and does not import, rename, close, or otherwise treat the orphan ticket record as authority.
- The frozen source artifact is the clean registered worktree `.ivy/worktrees/009-projectile-cards`; source base is `c24ed0a88c2bff843e788e1957502d9b86bc3d25`, evidence head is `4ce6038d83cd5fbdc7c0b988e0a9ba8f57895047`, and authoritative target base is `ce66a2af9a87b96fcc1de130af012bf7fb8418c4`.
- Reconstruct the selected product result on a detached worktree from the authoritative target base; do not merge or cherry-pick the 13-commit orphan chain wholesale.
- For the exact selected simulation boundary, transplant the snapshots at `95f15a5a9e22cf217d097c78147e827b349d5ff0` for:
  `src/Rounds.Sim/Cards/StatCardCatalog.cs`,
  `src/Rounds.Sim/Cards/StatCardDefinition.cs`,
  `src/Rounds.Sim/CombatController.cs`,
  `src/Rounds.Sim/PlayerCombatProfile.cs`,
  `src/Rounds.Sim/Properties/AssemblyInfo.cs`,
  `src/Rounds.Sim/Sim.cs`,
  `src/Rounds.Sim.Tests/MatchTests.cs`,
  `src/Rounds.Sim.Tests/ProjectileCardTests.cs`, and
  `src/Rounds.Sim.Tests/StatCardTests.cs`.
- For presentation, transplant only the two card-owned `game/Main.cs` hunks present at `3072bface31bfd5457c2014537fa387e773ffac4`: bounce-budget debug text and target-aware effect-line formatting. Reconcile card-pool/ricochet prose plus the clean-room policy sentence in `README.md`, and the Cards section plus clean-room policy sentence in `docs/architecture.md`, against current authoritative text.
- Exact sourced gameplay identifiers and short names are allowed where fidelity and unambiguous validation require them. Original source code, logo, card art, other extracted art, audio, and longer expressive or flavor text remain excluded; update the README and architecture policy sentences narrowly so they no longer claim that every source-game text token is forbidden.
- Preserve deterministic stable-ID ordering and complete-catalog duplicate validation before filtering. The runtime accepts exactly the 16 selected direct-attribute cards and rejects missing IDs, duplicate identities, original-name drift, non-finite values, unsupported targets/operations, and extra selected behavior.
- Positive attack-speed totals divide the base interval; negative totals multiply it. Positive reload speed provisionally divides after flat reload changes and before per-copy Quick Reload multipliers. Midpoint rounding remains away from zero with the existing one-tick clamps.
- Positive damage percentages add against the vanilla base; each supported negative damage effect contributes its own `1 + percentage` multiplier, so repeated Spray remains positive at exact `0.25^n`.
- A geometry contact consumes one bounce only when budget remains. Geometry, block, and body priority and source ordering do not change; bounce state must not introduce another collision path or an unbounded loop.
- Headless evidence is sufficient for this recovery. The selected slice changes deterministic simulation, catalog strings, effect formatting, and existing debug text; it does not change the bullet renderer or require a visible native run. Any remaining focus, cursor, monitor-presentation, or native-driver work belongs to a separate ticket and must still obey the monitor-4 project rule.
- The final candidate may change only this ticket's work log; the 12 selected code/test/presentation/document paths above, including the named README and architecture clean-room policy sentences; and a narrowly attributable append-only decision or postmortem entry if implementation itself creates a new judgment or failure. `docs/tickets/**/009-*`, ticket 013, committed recovery manifests, recovery inventory bytes, and every frozen artifact are read-only.
- Before inspection or reconstruction, record exact hashes/statuses for all eight frozen artifacts plus pre-existing refs and registrations. Candidate creation may add only ticket 014's excluded delivery worktree; final comparisons must prove all pre-existing artifacts, refs, and registrations remain unchanged.
- Ticket 013 exposed an Ivy zero-padding defect. Delivery of this single ready ticket must use selector-free close auto-detection rather than `--ticket 14`; after closing, verify and remove only the actual `.ivy/claims/14.json` if the helper incorrectly targets `014.json`.

## Evidence required

- A source-boundary check proves the nine exact simulation/test target snapshots match commit `95f15a5a9e22cf217d097c78147e827b349d5ff0`, the two selected `game/Main.cs` hunks match `3072bface31bfd5457c2014537fa387e773ffac4`, and no excluded orphan path or native-safety hunk entered the candidate.
- Catalog tests assert the exact 16 stable IDs and sourced names, deterministic order, stream loading, complete-catalog duplicate rejection, and rejection of every missing ID, duplicate identity, original-name drift, unsupported target/operation, non-finite value, extra behavior, and pool-size drift.
- Fold tests isolate every one-copy value for Bouncy, Fast Forward, Mayhem, and Spray; repeated copies; acquisition-order independence; mixed positive-additive and negative-multiplicative damage; exact two-through-five-copy Spray damage; sign-aware attack speed; reload order; midpoint rounding; and bounce, ammunition, fire, and reload clamps.
- Combat/contact tests prove shooter-derived bounce state, Bouncy's two reflections then despawn, Mayhem's five reflections, exact unused remainder through face and corner contacts, stable contact normals/source order, block preservation of geometry budget, body despawn, zero-bounce behavior, equal-time geometry priority, and deterministic four-contact termination.
- Hash tests prove each new profile value and live remaining-bounce value affects `Sim.Hash`, identical bounce scripts have identical per-tick hashes, a changed card diverges before firing, and all-vanilla worlds append no custom marker or value.
- Match tests prove deterministic 16-card opening vectors, five distinct choices, owned-card recurrence, drafting all four cards, a real second-Spray next-duel transition with exact `0.25^2` damage, complete history equality across repeated runs, and the selected source's bounded `match-smoke` hash `828a86f8f010349b`.
- The protected base duel smoke remains `d2687f48fe6dd085`; the protected golden replay remains `b91f86b6f1dc6b10`; `spec/` and `replays/` remain byte-identical to the target-base trees.
- The supported zero-warning build, repository checks, full simulation/replay/history suite, deterministic match and duel smokes, replay corpus/history verification, and headless Godot editor/runtime checks all pass.
- Candidate inspection proves only the admitted paths changed, all text files are LF-only with final LF, `node C:/Users/Adam/.codex/plugins/cache/ivy/ivy/0.1.0/checks/scripts/check-tickets.mjs .` passes, and `git diff --check` passes.
- Before/after comparisons prove the eight frozen artifact manifests/digests, dirty registered statuses, five operational indexes, pre-existing refs, and registrations are unchanged; only the reviewed candidate and normal ticket delivery metadata may advance authoritative `main`.

## Exclusions

- Do not import orphan ticket 009's lifecycle bytes, sessions, decisions, postmortems, ready/closed/reopened claims, or historical review markers.
- Do not import orphan `AGENTS.md`, `NativeEvidenceDriver`, `NativeLaunchPolicy`, their UID files/tests, `game/project.godot` no-focus change, native focus/cursor probe, repository-gate changes for that probe, or the unrelated release-year wording correction.
- Do not add generic hooks, event buses, per-card subclasses, scene-owned combat, parallel collision logic, multiple projectiles, spread, homing, growth, steering, poison, explosions, target-seeking bounces, bounce growth, delayed effects, block hooks, revive, rarity weighting, non-static map behavior, or any unsupported card.
- Do not launch a visible project window for implementation or verification. If a later separate ticket requires native evidence, configure monitor 4 before showing it and record any placement failure in the postmortem ledger.

## Work log

- 2026-08-28T19:17:45Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Selecting the smallest independently reviewable product slice from the clean 13-commit orphan chain without importing its rejected native-safety work or lifecycle claims.
- 2026-08-28T19:19:25Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Bound exact simulation/test snapshots, two presentation hunks, current-main reconciliation, headless evidence, frozen-artifact preservation, explicit native exclusions, and selector-free delivery for the recovered ricochet cards.
- 2026-08-28T19:20:10Z stage admission start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a049d0-60ce-71f3-b98a-11b72646dcd9 — Freshly challenging ticket 014's exact orphan source selection, current-main reconstruction, native exclusions, deterministic evidence, immutability boundaries, and risk-4 admission bar.
- 2026-08-28T19:28:02Z stage admission end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a049d0-60ce-71f3-b98a-11b72646dcd9 — Rejected the contradiction between exact sourced card names and authoritative guidance that still prohibited every original UI text token.
- 2026-08-28T19:28:58Z stage correction start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Narrowing the clean-room policy to permit sourced gameplay identifiers and short names while preserving the source-code, logo, art, audio, and expressive-text exclusions.
- 2026-08-28T19:29:32Z stage correction end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Superseded the blanket exact-UI-text prohibition in the decision record and bound narrow README and architecture reconciliation so the selected catalog and durable project guidance agree.
- 2026-08-28T19:30:05Z stage admission start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a049d9-8546-74f0-8f4d-4cd29e32dc1f — Freshly reviewing the corrected exact-name policy, selected orphan snapshots, reconstruction boundary, headless evidence, preservation rules, and all prior admission findings.
- 2026-08-28T19:34:41Z stage admission end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a049d9-8546-74f0-8f4d-4cd29e32dc1f — Admitted at risk 4 with no findings after the exact-name policy, source snapshots, native exclusions, hashes, reconstruction scope, frozen evidence, and selector-free delivery boundary all proved cold-reader complete.
- 2026-08-28T19:42:45Z stage implement start session codex:01a049e0-39ea-7c60-b57d-8094bf7af9b5 — Reconstructing only the admitted nine-file simulation/test snapshot, two-hunk presentation snapshot, and narrow current-base documentation policy after recording the complete frozen-state baseline.
- 2026-08-28T19:44:43Z stage implement end session codex:01a049e0-39ea-7c60-b57d-8094bf7af9b5 — Transplanted ten exact selected blobs, reconciled only the admitted README and architecture sentences, and proved the working tree is limited to the 13-path boundary with no orphan lifecycle or native-safety content.
- 2026-08-28T19:44:43Z stage verify start session codex:01a049e0-39ea-7c60-b57d-8094bf7af9b5 — Running focused catalog, fold, combat, contact, hash, and match evidence before the complete repository, replay/history, deterministic smoke, headless Godot, preservation, scope, LF, ticket, and whitespace gates.
- 2026-08-28T20:04:21Z stage verify end session codex:01a049e0-39ea-7c60-b57d-8094bf7af9b5 — Passed 64 focused and 270 complete tests, zero-warning build, repository and replay/history checks, exact duel/match/golden hashes, supported headless Godot checks, exact source blobs and two presentation hunks, protected trees, LF/scope/ticket/whitespace gates, and unchanged frozen manifests, indexes, dirty states, refs, and registrations.
- 2026-08-28T20:08:15Z stage correction start session codex:01a049e0-39ea-7c60-b57d-8094bf7af9b5 — Recording the two attributable implementation failures whose safe no-mutation boundaries and verified resolutions existed only in transient command output.
- 2026-08-28T20:08:50Z stage correction end session codex:01a049e0-39ea-7c60-b57d-8094bf7af9b5 — Appended one postmortem section covering the refused linked-index transplant and offline NuGet-audit failure without changing the existing Godot record or any product byte.
- 2026-08-28T20:08:50Z stage verify start session codex:01a049e0-39ea-7c60-b57d-8094bf7af9b5 — Rechecking ticket format, whitespace, LF bytes, exact path scope, selected source blobs and presentation hunks, complete incremental and candidate diffs, and all frozen manifests, indexes, dirty states, refs, and registrations.
- 2026-08-28T20:16:46Z stage verify end session codex:01a049e0-39ea-7c60-b57d-8094bf7af9b5 — Passed append-only bookkeeping, complete-diff inspection, ticket, whitespace, LF, 14-path scope, exact selected-blob and two-hunk presentation checks, and unchanged frozen manifests, indexes, dirty states, refs, registrations, detached HEAD, and product bytes.
- 2026-08-28T20:20:54Z stage review start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04a07-582c-77e3-af95-ea1b831e548b — Independently reviewing the complete exact recovery candidate from admission base through this unmatched marker, including source fidelity, clean-room boundaries, deterministic evidence, full verification, frozen-artifact preservation, and workflow identity.
- 2026-08-28T20:37:50Z stage review end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04a07-582c-77e3-af95-ea1b831e548b — approved candidate 801376d1bf815864e714dc15e30de9a9033a384c..ef3c71ea43c432a823c9496a41527879682ce4a2
- 2026-08-28T20:37:50Z stage integration end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — integrated ef3c71ea43c432a823c9496a41527879682ce4a2 as ef3c71ea43c432a823c9496a41527879682ce4a2
