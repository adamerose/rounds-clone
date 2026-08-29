# ROUNDS visual fidelity

## Authority

The installed public ROUNDS build and clean-room observations of its visible output are the visual acceptance authority.
Ticket 020 owns direct screen-by-screen comparison and the production visual system after tickets 016–019 establish the underlying feel, projectile, arena, and current-card behavior.
Until then, current colors, shapes, typography, layout, effects, and motion are development scaffolding, not accepted faithful presentation.

The three generated concept screens are preserved byte-for-byte only as superseded historical evidence:

- `docs/design/concepts/main-menu.png`
- `docs/design/concepts/gameplay-primary.png`
- `docs/design/concepts/card-draft-v2.png`

They do not define acceptance, and their invented product title, card names, screen-printed style, layout, tokens, or motion values must not guide new work.
Do not edit or ship the concept PNGs.

## Faithful-subset invariant

An incomplete screen or asset set may omit ROUNDS content, but implemented titles, card names, controls, geometry, silhouettes, colors, timing, effects, and screen flow must target direct ROUNDS evidence.
When evidence is missing, mark the element unresolved or leave it absent rather than inventing a substitute.
Deterministic rendering and internal consistency are regression signals only; visual acceptance requires comparison with the public target.

Clean-room work must recreate behavior and presentation without copying source code or extracting proprietary logo, art, audio, fonts, or other asset bytes.
The plain-text `ROUNDS` title and exact sourced short card names are allowed and required; the original logo artwork and card artwork are not.

## Current boundary and ownership

The shipped shell currently exposes only the two opening drafts and first full round.
At the first loser draft it shows an incomplete-fidelity notice and accepts no second-card selection until ticket 019 verifies current-card composition.
Any visual evidence from this shell proves only that the `ROUNDS` title and exact sourced card names render; it does not prove mechanics or presentation fidelity.

The remaining visual dependencies are explicit:

- Ticket 016 calibrates base movement and combat feel.
- Ticket 017 calibrates base projectile presentation.
- Ticket 018 reconstructs and audits the complete 70-arena catalog and behavior.
- Ticket 019 verifies and gates the current 16 cards.
- Ticket 020 replaces the scaffold with presentation derived from direct ROUNDS comparison.
- Ticket 021 matches controller and menu input.
- Ticket 022 adds match replay and internal headless self-play.
- Ticket 023 completes settings, persistence, and shipping behavior.
- Ticket 024 supplies nightly replay-reel evidence.
- Ticket 025 implements the remaining 51 cataloged cards after the verification infrastructure exists.

## Display safety

The game retains a 1920 × 1080 logical canvas and the project-wide monitor-4 placement rule while fidelity work continues.
Those development safeguards do not establish the target game's responsive behavior.
Every future native capture must verify the exact project window is centered on monitor 4 before it is shown or recorded.
