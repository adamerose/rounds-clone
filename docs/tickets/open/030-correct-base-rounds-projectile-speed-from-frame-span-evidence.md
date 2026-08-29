---
format: 3
status: ready
created: 2026-08-29T05:51:48Z
origin: human-request
tags: ["product-fidelity", "simulation", "combat", "evidence"]
value: 10
risk: 4
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: [15]
supersedes: []
split-from: [16]
---

# Correct base ROUNDS projectile speed from frame-span evidence

The clone's base projectile crosses the arena roughly six times faster than the retained ROUNDS source footage, matching the user's direct complaint. The old `2.4`-diameters-per-tick estimate recorded an approximately 84-pixel trajectory as one video frame even though that displacement spans six frames, so correct the derivation and gameplay before refining projectile presentation.

## Outcome

- The accepted base-speed measurement records both unobstructed projectile-core trajectories from the retained `footage-ssag` Tank-and-Leech opening, with the exact source hash and cadence, first and last frame numbers/timestamps, per-frame or endpoint center coordinates, stable-scene reference, player-diameter normalization, elapsed frame spans, Euclidean displacement arithmetic, uncertainty, and coverage limits.
- A deterministic comparison fixture demonstrates that the current `2.4`-diameters-per-tick tuning is outside both corrected evidence intervals before the change and that one corrected vanilla projectile speed satisfies both intervals afterward.
- `spec/measurements.json`, the binding combat fact, research notes, embedded runtime tuning, focused tests, replay contracts, and the intentional-break record agree on the corrected source-backed speed.
- The shipped clone no longer uses the known six-frame arithmetic error. Projectile color, silhouette, glow, and trail remain owned by ticket 017, and all other base-feel calibration remains owned by ticket 016.

## Decisions

- Correct the arithmetic against the exact retained external video already used to create the old value, rather than waiting to remeasure the same source through foreground input. This removes a demonstrated derivation error without claiming that the 2022 recording independently proves every current-build tuning value.
- Bind the source audit to external video SHA-256 `D6383E0C7A10EC4CE89C4E551FA7D1817A5D0D120BF17B025FA553442973C36C`, 640-by-360 constant 30-fps video, trajectory A frames `838..844` with elapsed span `6` and expected result near `0.39` player diameters per tick, and trajectory B frames `972..981` with elapsed span `9` and expected result near `0.37`.
- Compute each trajectory from Euclidean projectile-core displacement divided by same-scene player diameter, elapsed source-frame span, and two simulation ticks per 30-fps frame. Prove the camera static through a stable scene reference or subtract its measured motion; do not use glow or trail length as core displacement.
- Derive a per-track interval from explicit core-center, player-diameter, frame-timing, and stable-reference uncertainty. The selected tuning must satisfy both fixed-track intervals while `2.4` fails both; extra samples may corroborate but cannot replace these two tracks without reopening the contract.
- Define the two fixed tracks as a named projectile-frame-span measurement subtype. For that subtype only, make `elapsedFrames` a required recorded operand equal to `lastSourceFrame - firstSourceFrame`; unrelated measurement kinds keep their existing fields. Repository checks must reject an omitted denominator, a forced value of one, or a span that disagrees with the frame endpoints on either fixed track.
- Use only opening cards whose sourced visible effects omit projectile speed. `Tank` and `Leech` satisfy that isolation for this source; the Thruster-and-Fast-Forward comparison remains excluded.
- Keep the raw ROUNDS video and extracted frames outside Git. Commit a reproducible manifest containing the raw SHA-256, stream cadence, bounded frame coordinates and calculations, then make independently generated clone evidence disposable after review.
- Preserve one source of gameplay truth: `CombatTuning.Vanilla` continues to load the binding `combat-projectile-speed` fact rather than gaining a separate hard-coded correction.
- Use a green sequence that recognizes `spec/combat.json` as embedded runtime data and never mixes `spec/` with `src/` in one commit: first land the measurement subtype, schema, checker, checker tests, fixed-track evidence, and research notes without changing the binding combat value; next land any source-only behavior-neutral test preparation while `2.4` remains active; finally change the binding combat spec together with replay/golden/intentional-break consequences and no `src/` paths. The final binding-spec commit is explicitly the behavior change.
- Ticket 016 must confirm the result against installed public build `21020021` through ticket 031's input-isolated capture route. Until then, describe this as correction of the retained frame evidence, not a complete installed-build calibration.

## Evidence required

- A source audit reproduces the exact video hash and constant cadence, proves every source frame in `838..844` and `972..981` is distinct, binds core centers to a stable scene reference, and recomputes both fixed-track intervals from the recorded Euclidean coordinates and frame spans.
- Checker mutation tests prove that omitting `elapsedFrames`, forcing it to `1`, or making it disagree with `lastSourceFrame - firstSourceFrame` on either projectile-frame-span record fails at the public repository-check boundary, while unrelated measurement kinds remain valid without those fields.
- A red-before-green comparison record shows the current `2.4` tuning failing both non-malleable corrected intervals before any gameplay edit and the final vanilla displacement passing the same fixture afterward.
- Focused combat/profile tests, the full simulation and repository gates, replay/history validation, a zero-warning build, the ticket checker, and `git diff --check` pass. Every changed golden replay or smoke hash has a same-change intentional-break entry naming the projectile-speed correction.
- The no-input clone capture sandbox, if used for visual corroboration, runs below normal priority, writes renderer-owned external evidence, opens only on monitor 4, never activates itself or injects input, and exits automatically.

## Work log

- 2026-08-29T05:51:48Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Split the user's immediately provable speed complaint from the broader installed-build calibration after recovering the retained 30-fps source and identifying the six-frame denominator error.
- 2026-08-29T06:02:16Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Renumbered away from recovery-owned identities, bound the two exact trajectories/hash/cadence, fixed non-malleable uncertainty and denominator checks, and separated research/spec from runtime/replay commits.
- 2026-08-29T06:07:01Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Scoping the denominator invariant to projectile frame-span records and replacing the impossible binding-spec order with an embedded-runtime-safe green sequence.
- 2026-08-29T06:07:48Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Limited elapsed-frame requirements to the two named tracks and sequenced green evidence, behavior-neutral source preparation, then the binding-spec/replay behavior change without any mixed spec-and-src commit.
- 2026-08-29T06:09:17Z stage admission start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04bf9-57f9-7fa3-b2a8-63710fa3769e — Independently checking the retained source hash/cadence, two fixed trajectories, denominator regression, dual intervals, dependency split, embedded binding, green sequence, and risk-4 bar.
- 2026-08-29T06:12:09Z stage admission end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04bf9-57f9-7fa3-b2a8-63710fa3769e — Admitted at risk 4 with no findings after independently confirming the six-frame error, source stream, non-malleable track evidence, scoped checker boundary, and feasible runtime/replay delivery order.
- 2026-08-29T06:21:50Z stage implementation start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04b82-114d-7493-92ec-85564ec1cedd — Started the first green evidence boundary only: fixed projectile-frame-span subtype, retained-source audit, scoped denominator and interval gates, mutation tests, and research correction while preserving the binding combat spec, runtime, replays, and goldens for later phases.
- 2026-08-29T06:59:17Z stage implementation end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Completed the three green boundaries at `ee7b548`: exact fixed-track evidence and denominator checks, source-only binding preparation, then the sole `0.38` binding/replay transition. Root reproduced the source/frame hashes, deterministic double record, zero-warning builds, 50 checker and 209 applicable simulation tests, repository/replay/smoke checks, exact headless Godot completion, and golden history/event gates across the admission range.
- 2026-08-29T06:59:34Z stage review start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04c50-ac78-7663-90ae-3202713755dd — Fresh independent review reserved for exact range `4da7d9002834e12789d634a03a2ee10e8cf58007..candidate`, including retained source reproduction, fixed-track arithmetic/uncertainty, denominator mutations, strict spec/source separation, deterministic replay transition/history, headless Godot marker, and the no-GUI/no-input execution boundary.
- 2026-08-29T07:10:26Z stage review end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04c50-ac78-7663-90ae-3202713755dd — Rejected `4da7d9..23082c7` with one P1 documentation finding: the final research notes and manifest still described `2.4` as the current binding even though the reviewed spec/runtime used `0.38`. All source reproduction, arithmetic, checker, commit-separation, replay/history, tests, smoke, and headless Godot evidence otherwise passed.
- 2026-08-29T07:11:20Z stage implementation start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Started the narrow review correction: time-scope the manifest's `2.4` fields as pre-binding evidence and make the final research status state the completed `0.38` runtime/replay outcome without changing mechanics, spec, source, or replay bytes.
- 2026-08-29T07:12:07Z stage implementation end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Corrected the only review finding: final notes now state the completed `0.38` embedded runtime binding and replay hash, while the manifest preserves `2.4` explicitly as the pre-binding red state and records the later applied outcome. JSON parsing, repository checks, ticket checks, stale-term search, path scope, and diff checks pass; no gameplay, spec, source, or replay bytes changed.
- 2026-08-29T07:12:46Z stage review start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04c50-ac78-7663-90ae-3202713755dd — Re-reviewing the exact corrected admission range through `256d1b7`, focused on the rejected final-state research/manifest contradiction and regression of the already-passed media, arithmetic, checker, commit-boundary, replay/history, and headless runtime evidence.
- 2026-08-29T07:15:07Z stage review verdict session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04c50-ac78-7663-90ae-3202713755dd — Approved exact corrected range `4da7d9002834e12789d634a03a2ee10e8cf58007..4154f8fa20ab927403240aa07be010ee1d86f76e` with no findings. The sole P1 is resolved, all previously reviewed product/evidence/replay bytes remain unchanged, and targeted JSON, repository, ticket, path, golden-history, event, and cross-field checks pass. The canonical review end remains reserved for Ivy's closing transaction.
