---
format: 3
status: closed
created: 2026-08-14T06:12:02Z
origin: agent-proposed
tags: [research, maps, specification, fidelity]
value: 9
risk: 4
depends-on: [2]
sessions:
  - codex:019ffea8-55c5-79b3-96b2-da3210d67d84
  - codex:019fff46-e63b-7911-8124-12c0d8fe0b12
---

# Catalog and classify every vanilla arena

The project has a complete, sourced, machine-readable catalog and reusable geometry vocabulary for every arena available in unmodded public build `21020021` before map collision is implemented.

## Outcome

- Identify every vanilla arena with a stable project-owned ID and enough visual evidence to distinguish it without copying original assets.
- Normalize platform bounds, spawn regions, kill bounds, hazards, moving parts, breakable parts, scale, symmetry, and camera framing in player-diameter units.
- Group arenas into a small set of original geometry primitives and behavior modules that can reproduce their play patterns.
- Record per-map provenance, measurement method, confidence, tolerance, conflicts, and unavailable details.
- Add map and geometry schemas with checks for safe spawn separation, supported primitives, bounded coordinates, unique IDs, and complete provenance.
- Produce an implementation order that covers the smallest diverse arena set before the full catalog.

## Why

Arena geometry controls sightlines, recoil recovery, wall blocks, ring-outs, and card strength, so an unsourced handful of decorative layouts would undermine otherwise faithful combat tuning.

## Essential constraints

- Target public Windows build `21020021`, identified as `v1.1.2.a75ee335a`.
- Treat the official store's “70+ maps” as a lower bound until the catalog is reconciled against current public behavior.
- Recreate play patterns with original vector geometry and presentation rather than tracing or shipping screenshots.
- Express distance in player diameters and time in 60 Hz ticks.
- Keep raw screenshots and recordings under ignored `research/raw/` and commit only derived measurements and diagrams.
- Do not implement map physics or rendering in this ticket.

## Evidence required

- Every map entry passes the map schema and provenance gate.
- The catalog count is reconciled against at least two independent indexes and the current public build or an explicit unresolved gap.
- At least one measured example covers static, moving, breakable, hazard, asymmetric, and ring-out-focused layouts when those categories exist.
- Each map identifies valid spawn regions, collision bounds, kill bounds, and camera framing.
- A regression proves that an unsupported primitive, missing source, duplicate ID, unsafe spawn, or unbounded coordinate fails.
- The complete repository gate remains green.

## Work log

- 2026-08-14T07:58:56Z stage admission start session codex:019fff46-e63b-7911-8124-12c0d8fe0b12 — Cold-reading the complete-arena research contract against the Ivy admission bar and closed dependency 002.
- 2026-08-14T07:58:56Z stage admission end session codex:019fff46-e63b-7911-8124-12c0d8fe0b12 — Admitted at risk 4: the bounded research outcome, clean-room limits, geometry units, provenance, categories, schemas, and verification are explicit, with no unresolved human choice.
- 2026-08-14T07:58:56Z stage research start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Reconciling complete public arena indexes and identifying frame-addressable current-build examples for each geometry and behavior family.
- 2026-08-14T08:36:37Z stage research end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Reconciled 70 row-ordered public previews against the official lower bound and runtime enumeration gap, then inspected every silhouette, inferred spawn support, and classified six play-pattern families.
- 2026-08-14T08:36:37Z stage implement start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Encoding the clean-room arena catalog, geometry vocabulary, provisional bounds, behavior modules, schema, semantic checks, regressions, research rationale, and implementation order without map physics or rendering.
- 2026-08-14T08:36:37Z stage implement end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Added 70 sourced arenas with 4,520 coarse rectangles, 59 behavior modules, six verified representatives, 192 explicit unknowns, and failures for primitive, source, ID, spawn, coordinate, and representative-category defects.
- 2026-08-14T08:36:37Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Running the complete schema, source, ticket, test, deterministic simulation, Godot, formatting, and clean-room residue gates before exact-candidate review.
- 2026-08-14T08:42:46Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Full gate passed with zero warnings, 26 tests, repository and ticket checks, deterministic hash `f250d549cfb52a8b`, Godot editor/runtime smoke, clean diff formatting, and verified removal of temporary source previews.
- 2026-08-14T08:46:20Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Rebasing the completed map catalog onto the independently reviewed card catalog and reconciling their shared source, checker, test, decision, and failure records.
- 2026-08-14T08:46:20Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Preserved both research contracts and all 28 checker tests, with card and map documents required and cross-checked together.
- 2026-08-14T08:46:20Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Re-running the full combined repository gate after overlap reconciliation.
- 2026-08-14T08:46:20Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Combined gate passed with zero warnings, 37 tests, repository and ticket checks, deterministic hash `f250d549cfb52a8b`, Godot editor/runtime smoke, and clean diff formatting.
