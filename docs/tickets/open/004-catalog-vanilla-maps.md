---
format: 3
status: idea
created: 2026-08-14T06:12:02Z
origin: agent-proposed
tags: [research, maps, specification, fidelity]
value: 9
risk: 4
depends-on: [2]
sessions:
  - codex:019ffea8-55c5-79b3-96b2-da3210d67d84
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
