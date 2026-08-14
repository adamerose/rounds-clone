---
format: 3
status: closed
created: 2026-08-14T06:12:02Z
origin: agent-proposed
tags: [research, cards, specification, fidelity]
value: 10
risk: 4
depends-on: [2]
sessions:
  - codex:019ffea8-55c5-79b3-96b2-da3210d67d84
  - codex:019fff30-7cf1-75a3-aa80-02e6bc681833
  - codex:019fff46-9914-7892-900d-0298b80df82b
  - codex:019fff51-5f0d-72f0-b036-b76b41d3e289
  - codex:019fff5e-83ba-7fe3-9033-67c3ae45f4b8
  - codex:019fff67-9eba-7042-8e81-fc16c3885b45
---

# Catalog every vanilla card

The project has a complete, sourced, machine-readable catalog of every upgrade available in unmodded public build `21020021` before card behavior is implemented.

## Outcome

- Identify every vanilla card by stable project-owned ID, original display name as research metadata, rarity, and draft availability.
- Record each base effect, stacking order, cap, interaction hook, and visible behavior with per-fact provenance, confidence, method, and tolerance.
- Preserve balance changes from official patch history and make the current-build value binding when versions differ.
- Record conflicts and unknown combinations without inventing exact rules.
- Add a card schema and repository checks for unique IDs, known stat names, valid stacking operators, complete provenance, and current build identity.
- Produce an implementation order that starts with stat-only cards and isolates hook-heavy interactions.

## Why

Cards create the game's combinatorial depth, but implementing from names or memory would bake untraceable balance and stacking mistakes into every later system.

## Essential constraints

- Target public Windows build `21020021`, identified as `v1.1.2.a75ee335a`.
- Treat the official store's “65+ unique powerups” as a lower bound until the catalog is reconciled against current public behavior.
- Use project-owned stable IDs in implementation-facing data and retain original names only in research metadata.
- Do not copy card art, descriptive text, code, audio, or extracted game data.
- Keep raw screenshots and recordings under ignored `research/raw/` and commit only derived facts.
- Do not implement card behavior in this ticket.

## Evidence required

- Every catalog entry passes the card schema and provenance gate.
- The catalog count is reconciled against at least two independent indexes and the current public build or an explicit unresolved gap.
- Each non-obvious numeric effect has an official note, direct observation, or two corroborating community sources.
- Representative additive, multiplicative, count-based, max-wins, and hook-driven cards have explicit stacking semantics.
- A regression proves that an unknown stat, missing source, duplicate ID, or unsupported stacking operator fails.
- The complete repository gate remains green.

## Work log

- 2026-08-14T07:34:08Z stage admission start session codex:019fff30-7cf1-75a3-aa80-02e6bc681833 — Cold-reading the complete-card research contract against the Ivy admission bar and closed dependency 002.
- 2026-08-14T07:34:08Z stage admission end session codex:019fff30-7cf1-75a3-aa80-02e6bc681833 — Admitted at risk 4: the research-only outcome, clean-room limits, ordering, conflict handling, provenance, schemas, and verification are explicit, with no unresolved human choice.
- 2026-08-14T07:34:08Z stage research start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Reconciling complete public card indexes, official balance history, visible current-build behavior, and stacking semantics before implementation.
- 2026-08-14T07:55:59Z stage research end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Bound 67 cards, 199 sourced effects, five explicit stacking families, three patch milestones, five retained value conflicts, and the current-build enumeration gap.
- 2026-08-14T07:55:59Z stage implement start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Adding the machine-readable catalog, schema, source records, semantic gate, regressions, research rationale, and implementation order without gameplay behavior.
- 2026-08-14T07:55:59Z stage implement end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Added the 67-card clean-room catalog and checker failures for unknown targets, missing provenance, duplicate IDs, unsupported stacking operators, numeric evidence, patch sources, and count drift.
- 2026-08-14T07:55:59Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Running the complete repository, schema, ticket, formatting, deterministic simulation, and Godot smoke gates before candidate review.
- 2026-08-14T07:56:38Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Full gate passed with zero warnings, 24 tests, repository and ticket checks, deterministic hash `f250d549cfb52a8b`, Godot editor/runtime smoke, and clean diff formatting.
- 2026-08-14T08:06:32Z stage review start session codex:019fff46-9914-7892-900d-0298b80df82b — Reviewing exact candidate `a8edf4f305358dbd720b7f69d54c493e09d0c411` against ticket 003 and its public sources.
- 2026-08-14T08:06:32Z stage review end session codex:019fff46-9914-7892-900d-0298b80df82b — Rejected unsupported per-effect stacking and cap claims, two concealed historical-source conflicts, Quick Reload's percent/factor mismatch, and patch 1.05's wrong date.
- 2026-08-14T08:06:32Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Replacing unsupported formulas with explicit unknowns, separating stacking provenance, correcting patch scope and historical conflicts, and adding semantic regressions.
- 2026-08-14T08:08:26Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Reduced 194 effects to unresolved stacking and 198 caps to unknown, retained five sourced representative formulas and Grow's official cap, corrected all review findings, and added four regressions.
- 2026-08-14T08:08:26Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Re-running the complete repository gate, semantic summaries, ticket checks, and diff checks on the corrected candidate.
- 2026-08-14T08:08:47Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Full gate passed with zero warnings, 28 tests, repository and ticket checks, deterministic hash `f250d549cfb52a8b`, Godot editor/runtime smoke, and clean diff formatting.
- 2026-08-14T08:20:34Z stage review start session codex:019fff51-5f0d-72f0-b036-b76b41d3e289 — Reviewing corrected exact candidate `58d5eca40be80170375228bdd8b86c275b4e884e` against ticket 003 and its cited public facts.
- 2026-08-14T08:20:34Z stage review end session codex:019fff51-5f0d-72f0-b036-b76b41d3e289 — Rejected three unsupported duplicate formulas, false GameFAQs corroboration for four cards, and incomplete preservation of Dazzle's relative duration change.
- 2026-08-14T08:20:34Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Auditing source coverage, binding supported Echo and Refresh duplicates, making Quick Reload and Remote unresolved, and preserving Dazzle's relative patch constraint.
- 2026-08-14T08:22:10Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Corrected every second-review finding, added 11 executable GameFAQs exclusions, and made resolved-versus-unresolved stacking cases mechanically consistent with their effects.
- 2026-08-14T08:22:10Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Running the complete repository, source-exclusion, schema, ticket, deterministic, Godot, and diff gates on the second correction.
- 2026-08-14T08:22:58Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — The new exclusion gate rejected three stale GameFAQs stacking citations before tests ran.
- 2026-08-14T08:22:58Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Aligning the three unresolved stacking records with their corrected current-value sources.
- 2026-08-14T08:22:58Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Removed the stale citations while retaining executable exclusions against recurrence.
- 2026-08-14T08:22:58Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Re-running the complete gate after the source-alignment correction.
- 2026-08-14T08:22:58Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Full gate passed with zero warnings, 30 tests, repository and ticket checks, deterministic hash `f250d549cfb52a8b`, Godot editor/runtime smoke, and clean diff formatting.
- 2026-08-14T08:32:00Z stage review start session codex:019fff5e-83ba-7fe3-9033-67c3ae45f4b8 — Reviewing exact candidate `d50f8a391592fbbbeadc6a11093763911b54e60b` against ticket 003, the corrected source exclusions, and public card references.
- 2026-08-14T08:32:00Z stage review end session codex:019fff5e-83ba-7fe3-9033-67c3ae45f4b8 — Rejected GameFAQs as false unit corroboration for Brawler and Pristine Perseverence percentage-health effects; every prior finding and the full gate otherwise passed.
- 2026-08-14T08:32:00Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Replacing the two flat-HP citations with independent percentage sources and adding executable exclusions plus a unit-conflict regression.
- 2026-08-14T08:32:55Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Replaced both incompatible citations, recorded two executable exclusions, and added a regression that rejects known source-unit conflicts as corroboration.
- 2026-08-14T08:32:55Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Re-running source consistency, schema, repository, test, deterministic simulation, Godot, ticket, and diff gates after the third correction.
- 2026-08-14T08:32:55Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Full gate passed with zero warnings, 31 tests, repository and ticket checks, deterministic hash `f250d549cfb52a8b`, Godot editor/runtime smoke, and clean diff formatting.
- 2026-08-14T08:43:28Z stage review start session codex:019fff67-9eba-7042-8e81-fc16c3885b45 — Reviewing exact candidate `02c339a89dfdbb3ecf276f886d82f064e5a4eda5` after the two health-unit source corrections.
- 2026-08-14T08:43:28Z stage review end session codex:019fff67-9eba-7042-8e81-fc16c3885b45 — Approved the exact candidate with no findings after source verification, full 67-card reconciliation, 31 passing tests, deterministic smoke, and Godot editor/runtime smoke.
