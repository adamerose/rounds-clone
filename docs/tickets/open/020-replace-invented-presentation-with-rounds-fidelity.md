---
format: 3
status: idea
created: 2026-08-29T02:31:59Z
origin: human-request
tags: ["product-fidelity", "presentation", "game-shell"]
value: 10
risk: 4
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: [15, 16, 17, 18, 19]
supersedes: []
split-from: []
---

# Replace invented presentation with ROUNDS fidelity

The active visual direction was intentionally designed as an original screen-printed `RICOCHET` game, so even mechanically correct slices are presented against the wrong target. Rebuild the menus, fighters, arenas, draft, HUD, camera, feedback, and audio cues from direct ROUNDS comparison while recreating all implementation and assets independently.

## Outcome

- The playable shell follows ROUNDS' observed screen flow, layout, typography character, fighter silhouette and motion, arena rendering, draft presentation, HUD, camera behavior, hit-stop, shake, particles, trails, block feedback, and sound-event timing.
- The three old concept PNGs remain preserved historical artifacts but have no acceptance authority and no invented card/title content reaches the live UI.
- Missing presentation remains visibly incomplete during development; it is not replaced by an unrelated style and called finished.
- Proposed child ticket `043` isolates the first source-proved terminal-blast screen treatment: local burst and particles, a discrete multi-tap radial scene echo, chromatic separation, and adjacent-frame result dimming. It advances but does not close this complete-presentation umbrella.

## Decisions

- Use direct installed-build captures at named states as acceptance references. Recreate the look with original code, open-license fonts where necessary, and independently made assets; do not extract or ship proprietary ROUNDS asset bytes.
- Keep raw ROUNDS captures external and gitignored. Version only a manifest of build/state/frame coordinates, raw hashes, derived visual measurements, and independently generated clone captures.
- Deliver vertical screen/state slices that can be compared end to end, beginning with base duel and draft after tickets 16–17. Do not polish an invented component system first.
- Preserve simulation ownership boundaries: presentation observes events/state and never becomes the source of game rules.

## Evidence required

- A versioned comparison manifest pairs external target-build coordinates/hashes with independently generated clone captures for every delivered screen/state at matched resolution, phase, players, cards, and arena; no raw ROUNDS frame is committed.
- Human-style native runs verify the complete local path and adjacent states on monitor 4, with no window shown on monitors 1 through 3.
- Visual regression checks cover stable layout/color/silhouette facts while allowing bounded raster differences from independent asset recreation.
- Godot editor/runtime, accessibility/control, repository, deterministic simulation/replay, ticket, and whitespace gates pass for every delivered slice.

## Work log

- 2026-08-29T02:31:59Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Filing replacement of the superseded RICOCHET concept direction with direct ROUNDS visual and interaction comparison.
- 2026-08-29T02:33:55Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Bound each delivered screen/state to paired target-build evidence and independent asset recreation without giving the old concept system residual authority.
- 2026-09-02T02:09:33Z — Reflection verdict: wait because the required user-facing presentation replacement's vertical slices need calibrated base behavior, projectile presentation, verified arenas, and verified cards from tickets 016 through 019.
- 2026-09-04T17:00:46.632Z — Recorded proposed ticket 043 as the first source-bound multi-tap radial screen-response child while leaving audio fidelity and the remaining presentation catalog here.
