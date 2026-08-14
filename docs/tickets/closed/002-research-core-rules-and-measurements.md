---
format: 3
status: closed
created: 2026-08-14T05:42:13Z
origin: human-request
tags: [research, specification, fidelity]
value: 10
risk: 6
depends-on: [1]
sessions:
  - codex:019ffea8-55c5-79b3-96b2-da3210d67d84
  - codex:019ffef0-fa87-7a50-960e-5b747abd5c3b
  - codex:019fff0e-f811-71d0-a4aa-27137690f147
  - codex:019fff1d-fe99-7af1-a895-91bf4b242796
  - codex:019fff26-f793-7293-87fe-8a816060e432
---

# Research core rules and footage measurements

The project has a sourced, machine-readable fidelity target for the vanilla match loop and base player, weapon, block, movement, and camera behavior before those rules are implemented.

## Outcome

- Name the exact public vanilla PC build or behavior window this clone targets and explain how later platform-only fixes are treated.
- Create a source index that records title, publisher, URL, access date, source kind, scope, and reliability.
- Create machine-readable specifications for match flow, player controls, base combat tuning, and measurement targets.
- Measure movement, jumping, bullets, recoil, blocking, camera framing, and out-of-bounds behavior from frame-addressable gameplay footage.
- Record conflicts and unknowns without silently converting estimates into facts.
- Add mechanical schema/provenance validation to the repository gate.
- File bounded follow-up tickets for the complete vanilla card catalog and map catalog.

## Why

The simulation can reproduce only what the research artifact makes explicit.
Locking sourced values and tolerances now prevents implementation intuition from becoming an untraceable substitute for the original game's behavior.

## Essential constraints

- Use official store copy and patch notes for product scope and version history.
- Use the current public Windows build as the default target unless evidence shows a gameplay regression or undocumented card retune; record any exception.
- Prefer direct 60 fps footage without edits, overlays, mods, or variable playback speed for measurements.
- Express distances in player diameters, time in 60 Hz ticks, speeds in player diameters per tick, and ratios as dimensionless values.
- Every non-obvious value carries at least one source, confidence, derivation method, and tolerance.
- Keep downloaded video and frames under gitignored `research/raw/`; commit only notes, source metadata, measurement procedures, and derived values.
- Do not change `src/` or implement researched mechanics in this ticket.
- Do not copy original art, audio, text, or code into the repository.

## Evidence required

- JSON schema validation passes for every new `spec/` artifact.
- A provenance check rejects a representative fact that lacks a source or confidence.
- At least two independent gameplay recordings support the highest-impact movement and combat measurements, or the conflict is explicit.
- `spec/measurements.json` includes the observed frame interval, pixel measurements, normalized result, tolerance, source timestamp, and confidence for each metric.
- The exact match sequence is supported from initial card choice through five-point victory, including two-kill points and five-choice loser drafts.
- The complete repository gate remains green without any implementation change.

## Work log

- 2026-08-14T05:42:13Z stage research start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Adam's build directive explicitly admits the research dependency; starting with official version history, vanilla 1v1 scope, and frame-addressable mechanics sources.
- 2026-08-14T06:20:15Z stage research end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Pinned public build 21020021, sourced the complete match sequence, recorded 12 frame measurements from two independent matches, and preserved every disputed value as an estimate or open rule.
- 2026-08-14T06:20:15Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Exercising schema and provenance failures, the full .NET solution, deterministic smoke hash, and Godot editor/runtime boundary.
- 2026-08-14T06:20:15Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Repository checks passed, 14 tests passed, smoke hash repeated as f250d549cfb52a8b, Godot editor import/runtime passed, formatting was unchanged, and ticket format passed.
- 2026-08-14T06:20:15Z stage review start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Preparing the completed exact candidate for a fresh non-author review against ticket 002.
- 2026-08-14T06:34:30Z stage review end session codex:019ffef0-fa87-7a50-960e-5b747abd5c3b — Rejected candidate a572aef332c2c39166cb6168da75cee0c88f7f7c: one run-speed result did not reproduce, late-match samples did not control for card modifiers, and required recoil plus other numeric measurements lacked frame-addressable rows.
- 2026-08-14T06:34:30Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Reopening the admitted contract to replace contaminated samples, make every claimed footage-derived numeric target reproducible, and make coverage omissions fail mechanically.
- 2026-08-14T06:53:25Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Replaced late-match samples with early loadout-controlled evidence, recorded 19 raw measurements across 10 coverage contracts, and separated direct observations from provisional tuning hypotheses.
- 2026-08-14T06:53:25Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Rechecking source coverage, derivation arithmetic, schema failures, the deterministic simulation boundary, and both headless Godot entry points.
- 2026-08-14T06:53:25Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Full repository gate passed with zero build warnings, 18 tests, repeated smoke hash f250d549cfb52a8b, Godot editor/runtime smoke, unchanged formatting, and passing ticket format before closure.
- 2026-08-14T06:53:25Z stage review start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Preparing the corrected completed candidate for a fresh non-author review against ticket 002 and the prior rejection findings.
- 2026-08-14T07:03:20Z stage review end session codex:019fff0e-f811-71d0-a4aa-27137690f147 — Rejected candidate 63f09925e439c0060d02f00dd547ff7226bdaa29: SSAG movement, jump, and recoil intervals contain collisions or impacts, and the visible Leech card was recorded as Lifestealer.
- 2026-08-14T07:03:20Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Reopening the admitted contract to exclude action-contaminated rows, correct card identity, and replace them only with isolated evidence or explicit single-source limitations.
- 2026-08-14T07:12:06Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Replaced the contaminated SSAG run, jump, and recoil rows with isolated second-duel intervals, corrected Leech identity, excluded every shot-contaminated movement or jump comparison, and linked derivations directly to named raw fields.
- 2026-08-14T07:12:06Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Rechecking frame arithmetic, action and card controls, explicit one-source limitations, coverage omissions, the full build, deterministic smoke, and both Godot entry points.
- 2026-08-14T07:12:06Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Full repository gate passed with zero build warnings, 19 tests, repeated smoke hash f250d549cfb52a8b, Godot editor/runtime smoke, unchanged formatting, passing ticket format, and a clean diff check.
- 2026-08-14T07:12:06Z stage review start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Preparing the second corrected candidate for a fresh non-author review against ticket 002 and both prior rejection reports.
- 2026-08-14T07:19:43Z stage review end session codex:019fff1d-fe99-7af1-a895-91bf4b242796 — Rejected candidate f50acf6b83b37fdfd46f5f4ae78fa25a90500768 because the accepted jump row started after takeoff but was incorrectly promoted to the full above-takeoff height.
- 2026-08-14T07:19:43Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Reopening the admitted contract to include the immediately preceding grounded frame and recompute jump height, apex time, jump speed, and gravity from the full visible arc.
- 2026-08-14T07:21:39Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Extended the SSAG jump to the last grounded frame, recorded the full 4.5078-diameter and 36-tick rise, recomputed jump speed and gravity, and added a regression that rejects raw spans inconsistent with their endpoints.
- 2026-08-14T07:21:39Z stage verify start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Rechecking the full grounded-to-apex arithmetic, endpoint-span failure, source coverage, build, deterministic smoke, and Godot boundaries.
- 2026-08-14T07:21:39Z stage verify end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Full repository gate passed with zero build warnings, 20 tests, repeated smoke hash f250d549cfb52a8b, Godot editor/runtime smoke, unchanged formatting, passing ticket format, and a clean diff check.
- 2026-08-14T07:21:39Z stage review start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Preparing the third corrected candidate for a fresh non-author review against ticket 002 and all prior rejection findings.
- 2026-08-14T07:29:56Z stage review end session codex:019fff26-f793-7293-87fe-8a816060e432 — Approved exact candidate 6681545522380445e270edc6c2888fb0a3e81d5c with no findings after independently inspecting source frames, recomputing all 19 derivations, and passing the full 20-test gate.
- 2026-08-14T07:29:56Z stage integration start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Fast-forwarding the approved exact candidate onto unchanged main.
- 2026-08-14T07:29:56Z stage integration end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Main advanced from 8fb9156d78be65d52365a683f2705441b21a5c81 to reviewed candidate 6681545522380445e270edc6c2888fb0a3e81d5c without reconciliation.
