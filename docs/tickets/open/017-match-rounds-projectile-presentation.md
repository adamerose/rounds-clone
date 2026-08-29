---
format: 3
status: ready
created: 2026-08-29T02:31:59Z
origin: human-request
tags: ["product-fidelity", "presentation", "projectiles"]
value: 9
risk: 3
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: [15, 30, 31]
supersedes: []
split-from: []
---

# Match ROUNDS projectile presentation

The clone draws a paper-colored core inside a thick dark ring with an invented colored line trail, which the user sees as black and unlike ROUNDS. Rebuild projectile color, silhouette, glow/trail, scale, and ownership cues from direct target-build frames without copying proprietary texture or shader bytes.

## Outcome

- The unmodified base projectile matches ROUNDS in visible core color, outline balance, apparent size, trail length/decay, direction cue, and owner readability at the corrected base speed. Card-modified projectile presentation remains owned by ticket 019 after those modifiers are verified.
- Rendering remains presentation-only and reads the corrected projectile speed from integrated ticket 030; it does not alter collisions or create a second projectile model. Apparent composited size is derived from visual evidence and does not treat ticket 016's still-provisional collision radius as an acceptance input.
- The old screen-printed bullet direction is not an acceptance target.

## Decisions

- Use frame-addressable screenshots from the installed target build as the visual oracle and recreate the observed effect with original code-native drawing/shaders.
- Use ticket 031's non-disruptive capture route for installed-build frames; never take foreground focus or global mouse/keyboard input as a shortcut.
- Keep raw ROUNDS frames external and gitignored. Commit source/build/state/frame coordinates, raw hashes, derived color/size/trail measurements, and independently generated clone captures.
- Compare the final composited projectile, not isolated source primitives. A technically light core that still reads as a black dot fails.
- Preserve accessibility/readability only where it does not intentionally change the ROUNDS cue; document any unavoidable platform-rendering tolerance.

## Evidence required

- A comparison report binds the same controlled base-projectile state in ROUNDS and the clone through the committed manifest and demonstrates the user-reported black/fast-dot mismatch is gone without committing raw ROUNDS frames.
- Automated presentation checks bind color ranges, size ratios, and trail lifetime to captured evidence without testing private implementation details.
- Godot editor/runtime checks and native human-style verification pass on monitor 4 for the corrected base projectile. Card-modified visual evidence is deferred to ticket 019.
- Simulation hashes, replay bytes, and dependency versions remain unchanged from ticket 017's integrated base; repository checks, ticket checker, and `git diff --check` pass.

## Work log

- 2026-08-29T02:31:59Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Filing direct projectile-render comparison for the user's black-projectile report instead of refining the invented visual style.
- 2026-08-29T02:33:55Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Bound projectile presentation to final-composite target-build comparisons across base and card-modified speeds while keeping collision and simulation ownership separate.
- 2026-08-29T05:51:48Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Pointed the presentation dependency at the bounded speed correction later numbered 030 so visual work need not wait for every unrelated base-feel calibration in ticket 016.
- 2026-08-29T06:02:16Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Bound presentation to ticket 030's corrected speed and ticket 031's safe target capture, decoupled apparent size from provisional collision radius, and froze simulation/replay bytes relative to this ticket's own base.
- 2026-08-29T06:09:17Z stage admission start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04bf9-57f9-7fa3-b2a8-63710fa3769e — Cold-reading the exact corrected-speed dependency, final-composite visual oracle, provisional collision-radius decoupling, safe capture, and unchanged simulation/replay boundary.
- 2026-08-29T06:12:09Z stage admission end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a04bf9-57f9-7fa3-b2a8-63710fa3769e — Admitted at risk 3 with no findings after projectile presentation became independently testable without changing simulation or waiting on unrelated base-feel work.
