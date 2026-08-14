# Visual system

The accepted visual direction is defined by three generated concept screens:

- `docs/design/concepts/main-menu.png`
- `docs/design/concepts/gameplay-primary.png`
- `docs/design/concepts/card-draft-v2.png`

They are the visual acceptance references, not runtime UI assets.
The game recreates their system with original reusable assets, code-native text, and live simulation state.

## Direction

`RICOCHET` is a tactile screen-printed arena game: sparse charcoal space, warm paper platforms and cards, bright team colors, heavy condensed type, and explosive ink-like effects.
The arena stays visually quiet until movement, firing, blocking, or impact adds a short burst of energy.
The result should remain readable from a couch and in chaotic late-match card combinations.

## Tokens

| Role | Value |
|---|---|
| Arena background | `#10131c` |
| Arena depth | `#080c13` |
| Paper/platform | `#f2f0e8` |
| Ink | `#171923` |
| Muted ink | `#777b83` |
| Red team | `#ff625f` |
| Blue team | `#48a9ff` |
| Impact | `#ffd34e` |
| Focus | Current player color, otherwise paper |

Paper and background texture are subtle monochrome noise at low opacity.
No color wash or decorative gradient sits over gameplay.
Shadows are short, hard, dark offsets rather than soft floating-card shadows.

## Typography

Use a bundled open-license condensed display face for the title, headings, score, and primary actions.
Use a bundled open-license condensed sans-serif for labels, effects, prompts, and settings.
Title and headings are uppercase with tight line height; body copy uses sentence case and enough weight for television viewing.
Every control defines its size and weight explicitly; no engine-default typography ships.

## Containers and components

- Menus use open alignment and underlines, not rounded panels.
- Player setup is aligned directly on the background with team-colored rules and focus marks.
- Draft choices use five warm-paper cards, one illustration region, one title, and one short effect line.
- Selected elements gain a team-colored outline, a small scale increase, and a hard offset shadow.
- HUD scoring uses circles and short rules rather than boxes.
- Prompts use simple controller/keyboard glyphs and plain labels along the bottom edge.

## Gameplay assets

- Fighters are small round bodies with dark outlines, dot eyes, short limbs, and separate aiming arms.
- State is communicated by squash, stretch, lean, limb pose, eye direction, and team color.
- Platforms are warm-paper oriented rectangles with sparse ink speckles and dark contact edges.
- Bullets are bright paper circles with short textured trails; special bullets change silhouette before color.
- Blocks are angular translucent shields with a paper edge, team tint, and a brief impact fracture effect.
- Hits use small wedges, flecks, rings, smoke puffs, and screen shake; they do not leave persistent clutter.
- Background depth comes from two very subtle texture layers and edge vignetting, not scenery.

Collision shapes remain code-owned and are tuned to the visible silhouettes.
Production fighter states, platform texture, block shield, card illustrations, impact particles, and background layers receive their own asset pass before final visual implementation.

## Motion

Menu focus snaps in roughly 90 ms with a small overshoot.
Cards settle in roughly 160 ms.
Gameplay uses short hit-stop, camera impulse, trail decay, particle bursts, and fighter squash/stretch driven from simulation events.
Reduced-motion mode removes camera shake and overshoot while keeping state changes visible.

## Screen inventory

The initial product path is main menu, local match setup, arena gameplay, card draft for the trailing player, repeated rounds, and win screen.
Settings and how-to-play are secondary menu screens in the same open layout.
The win screen reuses the arena and HUD motifs instead of introducing a new component family.

## Allowed first-viewport copy

The menu may show only `RICOCHET`, `PLAY`, `HOW TO PLAY`, `SETTINGS`, `QUIT`, `LOCAL MATCH`, `PLAYER 1`, `PLAYER 2`, `HUMAN`, `BOT`, `START`, `SELECT`, and `BACK` above the fold.
The arena uses score marks, the first-to-five target, and the tiny `RICOCHET` watermark shown in the gameplay concept.
The draft uses the selected player's color/name, `PICK ONE`, five card titles and effect lines from live card data, `CHOOSE`, and `DETAILS`.

## Responsive and display behavior

The native design viewport is 1680 × 945, a 16:9 reference.
Gameplay preserves a 16:9 safe region with letterboxing on wider or taller displays.
Menus reflow at narrow widths by placing match setup below the navigation while keeping minimum couch-readable type sizes.
The desktop game supports 1280 × 720 through 3840 × 2160 and scales code-native UI from a 1920 × 1080 logical canvas.

## Intentional constraints

The generated concepts contain rasterized text and illustrative character poses, but the implementation must not ship the screenshots as UI.
The exact paper distress is a texture target, not a pixel-perfect source asset.
Original Rounds art, audio, logo, card names, and exact UI wording remain prohibited.
