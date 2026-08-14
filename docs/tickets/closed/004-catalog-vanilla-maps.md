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
  - codex:019fff73-e68d-79d0-bca4-5da80964846a
  - codex:019fffa0-7ad0-7a30-8b5e-bf4bfa70ab8d
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
- 2026-08-14T09:01:17Z stage review start session codex:019fff73-e68d-79d0-bca4-5da80964846a — Reviewing exact candidate `79e2007096735f0be082580a055464ceaa804e50` against ticket 004, the public row images, binding map design, and full combined gate.
- 2026-08-14T09:01:17Z stage review end session codex:019fff73-e68d-79d0-bca4-5da80964846a — Rejected misaligned row geometry and saw classifications, absent IoU acceptance evidence, and an axis-aligned schema that contradicts the oriented-box design.
- 2026-08-14T09:01:17Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Rebuilding row anchoring, oriented geometry, behavior classification, spawn derivation, and the binding mask-render IoU oracle from the public workbook.
- 2026-08-14T09:34:09Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Replaced the rejected catalog with 70 row-anchored previews, 7,557 oriented silhouette boxes, explicit visual behavior candidates, source-supported two-dimensional spawns, and exact mask IoU scores from 0.956757 to 0.997735.
- 2026-08-14T09:34:09Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Re-running the generator reproducibility, schema, semantic evidence, ticket, build, test, deterministic simulation, Godot, formatting, and residue gates on the complete correction.
- 2026-08-14T09:34:48Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Byte-identical regeneration hash `83f4f7224b2be8ee2a94115b6586096db3c6698d0d7552467e2e714a573bbc34`, zero-warning build, 40 tests, repository and ticket checks, deterministic hash `f250d549cfb52a8b`, Godot editor/runtime smoke, and diff formatting all passed.
- 2026-08-14T09:48:13Z stage review start session codex:019fffa0-7ad0-7a30-8b5e-bf4bfa70ab8d — Reviewing exact candidate `a6dc1a2ae85ee9b4bf377f956837c7d1003606d0` against the admitted contract, public workbook, generator, mask oracle, spawn regions, independent indexes, clean-room boundary, and full gate.
- 2026-08-14T09:48:13Z stage review end session codex:019fffa0-7ad0-7a30-8b5e-bf4bfa70ab8d — Rejected a count-only rerender gate that allowed position drift, 40 spawn regions that extended beyond their named supports, and reconciliation against only one complete arena index.
- 2026-08-14T09:48:13Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Adding a positional rendered-mask digest, deriving full support-contained spawn regions, and reconciling the 70-row catalog with an independent public removed-map index.
- 2026-08-14T09:54:45Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Position-locked all 70 renders, made every spawn-region corner support-valid, added an independent six-map removal index, and added regressions for position-preserving drift, partial support, and missing index independence.
- 2026-08-14T09:54:45Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Re-running byte-identical generation, source and rendered-mask evidence, 70-map spawn sampling, schema, semantic checks, tickets, build, tests, deterministic simulation, Godot, formatting, and residue checks.
- 2026-08-14T09:55:20Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Byte-identical catalog hash `162f694f0f821c597fdcffd4664d6d22143c47c386df0818cbf9c61983cd312f`, 6,300 supported spawn samples, zero-warning build, 42 tests, repository and ticket checks, deterministic hash `f250d549cfb52a8b`, Godot smoke, and diff formatting all passed.
