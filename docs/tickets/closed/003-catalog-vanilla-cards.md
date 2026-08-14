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
- 2026-08-14T07:55:59Z stage research end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Bound 67 cards, 199 sourced effects, five explicit stacking families, three patch milestones, four retained value conflicts, and the current-build enumeration gap.
- 2026-08-14T07:55:59Z stage implement start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Adding the machine-readable catalog, schema, source records, semantic gate, regressions, research rationale, and implementation order without gameplay behavior.
- 2026-08-14T07:55:59Z stage implement end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Added the 67-card clean-room catalog and checker failures for unknown targets, missing provenance, duplicate IDs, unsupported stacking operators, numeric evidence, patch sources, and count drift.
- 2026-08-14T07:55:59Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Running the complete repository, schema, ticket, formatting, deterministic simulation, and Godot smoke gates before candidate review.
- 2026-08-14T07:56:38Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Full gate passed with zero warnings, 24 tests, repository and ticket checks, deterministic hash `f250d549cfb52a8b`, Godot editor/runtime smoke, and clean diff formatting.
