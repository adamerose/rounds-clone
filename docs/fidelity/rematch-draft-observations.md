# Rematch and card-draft observations

## Source boundary

- Recording: `reference/MedalTVRounds20260903165304088.mp4`
- Recording SHA-256: `453954a7230401ed805be4e53dec41779a1913dfd69903671fc131fca2c8a18c`
- Compared interval: 02:40.00–03:20.00
- Decode method: direct frames from the original 1280×720 recording with the pinned local FFmpeg binary. Generated frames and contact sheets are ignored under `out/ticket-041/`.

The observations below describe pixels and timing visible in the supplied recording. Draft seed, hidden rarity weights, exact animation curves, and gameplay constants not printed on cards remain approximations.

## Anchors

| Source time | Direct observation | Decoded-frame SHA-256 |
|---|---|---|
| 02:43.000 | `VICTORY!` spans the still-visible pink arena; both fighters, score pips, card-abbreviation stacks, shadows, and debris remain visible behind it. | `82b5a87336cc48db01ef018d67d62895a940e77e34961358d395f9c6e49867a7` |
| 02:45.000 | Large thin cyan `REMATCH?` appears over the arena with bold `YES` and `NO`; fighters continue moving behind the overlay. | `232790722b52bdc7fbaa8bc85d27841ece3dc7e7717cffffd3b682b81d152aac` |
| 02:47.000 | The arena and overlay have faded to the animated dark teal background before the orange draft reveal. | `e3dde676a8b19a0e95d5e06ae87586852034771b35229069711719b236ef135e` |
| 02:49.000 | Orange fills the lower screen with hat, moustache, face, hands, and five curved cards; `TASTE OF BLOOD` is raised/highlighted while neighboring faces are dim. | `e9867700616fb7dd91d59c61090c0a256a8776e9fce1791f1a35d54517683f0f` |
| 02:50.000 | Orange has moved the highlight to the gold `BURST` face; its art, outline, title, and rules brighten while the other four remain readable but subdued. This is hover evidence only. | `b0407242f0504813c1e7da673c239f8fdf7fa7d24a3171d203cf3f501c484d6e` |
| 02:54.000 | Orange's cards are lowering/dimming after confirmation; the top-right orange badge is `Da`, proving `DAZZLE` was confirmed despite the earlier `BURST` hover. | `98c8d1b27eb51939ae193ae4477a0821d3d5088d3a55d882e65140ccd3810dd0` |
| 03:06.000 | Blue fills the lower screen; `LIFESTEALER` is raised and purple-highlighted among its five offers while the `Da` prior-loadout badge remains at top right. | `20c22057dfbee77c4cc360fee851c27c0bfec4e2e087f18a7b32eaab6f587ef8` |
| 03:14.000 | Blue continues navigating the same fan with `ECHO` raised; this proves a raised face is hover/focus, not necessarily the confirmed choice. | `03a3cdd4d4cdeef67caa388b34cb282795b7302121677c2aac2bf391abc5c0da` |
| 03:16.000 | A new timber-and-yellow-canopy arena is visible after the draft fade. Both five-pip score rows are empty and old loadouts are cleared; top-right stacks contain only orange `Da` and blue `Ex`, identifying the confirmed `DAZZLE` and `EXPLOSIVE BULLET` choices. | `20bca7620b188e9cac596cb42f9cb1b895e24a9c1595f3ecaf0c014716b01712` |
| 03:17.000–03:20.000 | Both fighters move and exchange bright projectiles beneath the canopy; the chosen badges persist through resumed combat. | direct one-second sequence in `flow-d.png`; comparison anchors must retain separate frames rather than this contact sheet |

## Card faces and printed rules

Orange's fan contains, from left to right:

| Card | Printed rules visible in the source |
|---|---|
| `FROST SLAM` | “Slows enemies around you when you block”; “More HP”; `+0.25s Block cooldown` |
| `COMBINE` | “A bunch more DMG”; `-2 Ammo`; `+0.5s Reload time` |
| `TASTE OF BLOOD` | `+50% movement speed 3s after dealing DMG`; “Slightly more Life steal” |
| `BURST` | “Multiple bullets are fired in a sequence”; `+2 Bullets`; `+3 Ammo`; “Lower DMG”; `+0.25s Reload time` |
| `DAZZLE` | “Bullets stun the opponent multiple times”; `+0.25s Reload time` |

Blue's fan contains, from left to right:

| Card | Printed rules visible in the source |
|---|---|
| `EXPLOSIVE BULLET` | “Bullet explodes on impact”; “Lower ATKSPD”; `+0.25s Reload time` |
| `ECHO` | “Blocking triggers another, delayed block”; “More HP”; `+0.25s Block cooldown` |
| `LIFESTEALER` | “Steal HP from your opponent when near”; “Slightly more HP” |
| `EMP` | “Blocking spawns a ring of slowing projectiles”; “More HP”; `+0.25s Block cooldown` |
| `DAZZLE` | “Bullets stun the opponent multiple times”; `+0.25s Reload time` |

Printed plus signs are transcribed as displayed even when the named property makes the tradeoff undesirable. The footage does not expose numeric magnitudes for “More,” “Lower,” “Slightly more,” damage, life steal, slow strength, stun duration, or explosion radius.

## Presentation envelope

- The draft is a character presentation, not a conventional flat menu: the large low-poly fighter body occupies most of the screen, arms/hands frame the fan, the face changes subtly, and the cards float in a curved depth-separated arrangement.
- Selection is readable through several simultaneous cues: the focused card rises and grows, its title/art/rules brighten, corner accents and rarity color intensify, neighboring cards dim, and the relevant hand follows it.
- The same five-card layout is recolored and character-matched for orange and blue while keeping readable type hierarchy. The animated gray/teal paper-like background survives behind both.
- The match result and rematch prompt are overlays on a still-live arena before the scene fades. The card screen then yields to the next arena without showing a separate editor/loading surface.
- The source crosshair remains visible during the sequence. Small loadout badges in the upper right persist and are the strongest evidence of which rapidly navigated card was actually confirmed.

## Match reset

- At 02:43 the orange row has four filled pips and one empty pip while the blue row has five filled pips: blue has won the match 5–4.
- The 5–4 pips and both old loadout stacks remain visible through `VICTORY!` and `REMATCH?`; the arena continues behind both overlays.
- At 03:16 both five-pip rows are empty and the old multi-card stacks are gone. The new match therefore resets the visible score to 0–0 and clears prior-match cards before adding orange `Da` and blue `Ex`.
- Pixels do not establish whether one host, one local player, or both players must accept rematch. Requiring one vote from each client is the clone's explicit authority rule, not a claim about hidden ROUNDS networking.
