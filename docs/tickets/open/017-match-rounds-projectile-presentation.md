---
format: 3
status: idea
created: 2026-08-29T02:31:59Z
origin: human-request
tags: ["product-fidelity", "presentation", "projectiles"]
value: 9
risk: 3
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: [15, 16]
supersedes: []
split-from: []
---

# Match ROUNDS projectile presentation

The clone draws a paper-colored core inside a thick dark ring with an invented colored line trail, which the user sees as black and unlike ROUNDS. Rebuild projectile color, silhouette, glow/trail, scale, and ownership cues from direct target-build frames without copying proprietary texture or shader bytes.

## Outcome

- The unmodified base projectile matches ROUNDS in visible core color, outline balance, apparent size, trail length/decay, direction cue, and owner readability at the corrected base speed. Card-modified projectile presentation remains owned by ticket 019 after those modifiers are verified.
- Rendering remains presentation-only and reads the corrected simulation state from ticket 016; it does not alter collisions or create a second projectile model.
- The old screen-printed bullet direction is not an acceptance target.

## Decisions

- Use frame-addressable screenshots from the installed target build as the visual oracle and recreate the observed effect with original code-native drawing/shaders.
- Keep raw ROUNDS frames external and gitignored. Commit source/build/state/frame coordinates, raw hashes, derived color/size/trail measurements, and independently generated clone captures.
- Compare the final composited projectile, not isolated source primitives. A technically light core that still reads as a black dot fails.
- Preserve accessibility/readability only where it does not intentionally change the ROUNDS cue; document any unavoidable platform-rendering tolerance.

## Evidence required

- A comparison report binds the same controlled base-projectile state in ROUNDS and the clone through the committed manifest and demonstrates the user-reported black/fast-dot mismatch is gone without committing raw ROUNDS frames.
- Automated presentation checks bind color ranges, size ratios, and trail lifetime to captured evidence without testing private implementation details.
- Godot editor/runtime checks and native human-style verification pass on monitor 4 for the corrected base projectile. Card-modified visual evidence is deferred to ticket 019.
- Simulation hashes, replay bytes, dependency versions, repository checks, ticket checker, and `git diff --check` remain unchanged unless ticket 016's reviewed base already changed them.

## Work log

- 2026-08-29T02:31:59Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Filing direct projectile-render comparison for the user's black-projectile report instead of refining the invented visual style.
- 2026-08-29T02:33:55Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Bound projectile presentation to final-composite target-build comparisons across base and card-modified speeds while keeping collision and simulation ownership separate.
