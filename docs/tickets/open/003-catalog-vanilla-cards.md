---
format: 3
status: idea
created: 2026-08-14T06:12:02Z
origin: agent-proposed
tags: [research, cards, specification, fidelity]
value: 10
risk: 4
depends-on: [2]
sessions:
  - codex:019ffea8-55c5-79b3-96b2-da3210d67d84
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
