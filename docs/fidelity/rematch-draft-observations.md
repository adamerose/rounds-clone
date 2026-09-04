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
| 02:49.000 | Orange fills the lower screen with hat, moustache, face, hands, and five curved cards; `COMBINE` is raised/highlighted in yellow while neighboring faces are dim. | `e9867700616fb7dd91d59c61090c0a256a8776e9fce1791f1a35d54517683f0f` |
| 02:50.000 | Orange has moved the highlight to the gold `BURST` face; its art, outline, title, and rules brighten while the other four remain readable but subdued. This is hover evidence only. | `b0407242f0504813c1e7da673c239f8fdf7fa7d24a3171d203cf3f501c484d6e` |
| 02:54.000 | Orange's cards are lowering/dimming after confirmation; the top-right orange badge is `Da`, proving `DAZZLE` was confirmed despite the earlier `BURST` hover. | `98c8d1b27eb51939ae193ae4477a0821d3d5088d3a55d882e65140ccd3810dd0` |
| 02:56.000 | Blue already fills the lower screen with its complete five-card fan; the source does not contain a long blank `NEXT PLAYER` handoff at this anchor. | `af1a2f00bafcd1c66bc64cb7f8b8c153ac6f126ab160a09255edea9c5c0958a5` |
| 03:06.000 | Blue fills the lower screen; `LIFESTEALER` is raised and purple-highlighted among its five offers while the `Da` prior-loadout badge remains at top right. | `20c22057dfbee77c4cc360fee851c27c0bfec4e2e087f18a7b32eaab6f587ef8` |
| 03:14.000 | Blue continues navigating the same fan with `ECHO` raised; this proves a raised face is hover/focus, not necessarily the confirmed choice. | `03a3cdd4d4cdeef67caa388b34cb282795b7302121677c2aac2bf391abc5c0da` |
| 03:15.333 | Blue's selected `EXPLOSIVE BULLET` face is gone while the other four cards sit lowered/dimmed; orange `Da` and blue `Ex` persist at top right. | `42693ecc2e6ddd8383cf3c7791edf25a67fe3c658d25a558e4355256c37c44e5` |
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

- At 02:43 the orange row has four filled pips and one empty pip while the blue row has five filled pips: blue has won, with the displayed score ordered orange–blue as 4–5.
- The 4–5 pips and exact old stacks `Po De Th Qu Bu` for orange and `Bu Ca Co Co Fa` for blue remain visible through `VICTORY!` and `REMATCH?`; the arena continues behind both overlays.
- At 03:16 both five-pip rows are empty and the old multi-card stacks are gone. The new match therefore resets the visible score to 0–0 and clears prior-match cards before adding orange `Da` and blue `Ex`.
- Pixels do not establish whether one host, one local player, or both players must accept rematch. Requiring one vote from each client is the clone's explicit authority rule, not a claim about hidden ROUNDS networking.

## Source-to-clone inspection

The original-resolution comparison uses the thirteen named source frames under `out/ticket-041/correction-source/` and the thirteen final clone frames under `out/ticket-041/final-verified-v2-clone/`; all generated PNGs remain ignored. `final-verified` deliberately names an ordinary locked workspace build, not the isolated cold executable.
The corrected replay follows the source focus sequence: orange raises `COMBINE` at tick 540/02:49 and `BURST` at tick 600/02:50, then tick 840/02:54 shows its fan lowered and dimmed with the new `Da` badge. After only a half-second dark bridge, blue's complete fan is present at tick 960/02:56 with `DAZZLE` raised; it raises `LIFESTEALER` at tick 1560/03:06 and `ECHO` at tick 2040/03:14. At tick 2120/03:15.33 the selected `EXPLOSIVE BULLET` face is gone, the remaining fan is lowered, and `Da` plus `Ex` persist.
All nine stable `art_key` values now select distinct visible motifs: a frost ring, merged rounds, blood drop and fang, burst rays, stun stars, impact burst, echo rings, vampire orbit, and electric ring. The same authoritative hover and reveal state that moves the cards also moves the arms and hands, directs the pupils, and changes the mouth; original-resolution inspection confirmed the response on both orange and blue anchors.
The victory anchor renders blue as winner, orange as out, the 4–5 score, and both exact prior-card stacks. The accepted rematch clears that terminal result and both stacks before the new draft and restores both fighters and the score to 0–0. Ten hollow pip positions remain visible during every draft/reveal anchor; `Da` persists from orange confirmation onward and `Ex` joins it at blue confirmation through the shared HUD path.
Capture waits for the complete expected scene, an empty Bevy pipeline queue, and two consecutive complete extracted render frames before requesting a screenshot. The focused repeated-capture test produced byte-identical frames for the same immutable draft state and observed all expected scene roles without changing the flow or loadout digests.

The remaining visible differences are concrete rather than hidden behind an equivalence claim.
The source uses thinner outlined prompt lettering, denser low-poly character faces, hats, moustache and hand poses, richer bespoke card illustrations, more overlap and perspective in the fan, and smoother eased selection motion.
The clone uses simpler geometric item motifs and face construction, bolder prompt type, shallower card depth, less curved arms, and deterministic linear phase envelopes.
The resumed source arena has taller timber columns and a much denser faceted yellow impact/canopy field; the clone approximates it with stable yellow platforms, paired timber caps, long shadows, typed Dazzle spark trails, and a large presentation-only faceted burst sourced from the authoritative Explosive Bullet impact.
Audio, exact animation curves, and frame-identical typography remain outside this slice.

## Delivery evidence

The authoritative evidence was regenerated from the ordinary locked workspace executable and is therefore named `final-verified-v2`, never `cold`. Every metadata record identifies that exact `rounds-client.exe` SHA-256 as `0947eca34dde8b8c3af8dcc9e72dd156ee8501b585f8c41ec7b73157797fa049`; a focused process-boundary test independently hashes the invoked executable and requires the emitted value to match.
The thirteen-anchor metadata is SHA-256 `d09cc2c935369b3b530d8b80df9ad7fa82df18ca0bbd7a14ccf87c750726ab72`; all 13 frame hashes and paths revalidated against 13 distinct 1280×720 PNGs. Representative final frame hashes are `594510960f556a0c7791fa6f5b367d3b35891b3b4598e7a773fe7eb6bfb0c172` for the authoritative victory, `c4521181fad9397dc9e7cd581794ec7e142e1d3cf86285a1dd10bba6b9f9d4ec` for the prompt, `29f3cbf79d6bd089cab9695ca630c8eea1d1cc2610d0a838b3d448ec62cc6ec3` for orange's `COMBINE` focus, `891d2d67b4f374ac3b1bb48aa61da33a531934f9e1de83b7e4ff53a1c37e9fda` for its lowered `DAZZLE` confirmation, `d2d2e4304b17f81b0e913f254d13284c88717ab89b7facc9d2f9a93d75593a26` for blue's initial fan, `9252af7cb52f8b206a37f3206ad31b3a75d01aa83b241f8335e32dddfb2325c7` for its lowered `EXPLOSIVE BULLET` confirmation, and `520b14b13b5cacca60a3c0f7c1b460feb5bf246abf302dd0c24f25b58b0cde2e` for the upgraded-projectile exchange.
The live UDP run completed 2,400 snapshots on each of two separately launched clients and reported matching state hash `f0d6a9f19e518521993035f376c0dee98e8b5d7811e0e66ceb18bf91375413f1`, flow digest `f46412500c26324502b01a3c793a45fa2527696bffd780c60a221ea52fe738c3`, and loadout digest `2489e426cddc787ffa978da316025d7d72a1f5adefa5a7ed016185e85c24cfeb` across both clients, the server, and the local host. Both clients also observed the exact terminal authority, accepted-rematch reset, and complete blue fan by tick 960.
The bounded visible run used the same final state, selected and re-verified the project display at `(364,-1080)`, 1920×1080 before showing, exited normally, and left no `rounds-*` process.

The line inventory counts inserted Rust lines only; documentation is excluded.

| Responsibility | Added lines | Breakdown |
|---|---:|---|
| Product behavior | 2,379 | flow model 600; simulation 257; networking 66; presentation 1,425; client 31 |
| Tests | 635 | flow 258; simulation 114; networking 64; presentation 164; client process boundary 35 |
| Automation support | 43 | existing smoke/inspection entry point only |

Tests plus automation support total 678 lines, well below the product behavior they protect.
