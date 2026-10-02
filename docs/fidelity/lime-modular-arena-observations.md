# Lime modular arena observations

This record binds ticket 060 to `reference/MedalTVRounds20260903170709695.mp4`, SHA-256 `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9`. The native video time base is 1/10,000,000 second. All measurements use exact 1280×720 packed-RGBA decodes made with FFmpeg 7.1 and two decoder threads; the raw frames remain ignored.

## Source identities

| Clone tick | Native PTS | Packed RGBA SHA-256 | Observation |
|---:|---:|---|---|
| 0 | 2140158106 | `45791458e4a1012ddd693922b4593b8d8e8662de2fc91e4a23a2cb7c55426d83` | First complete arena view; both fighters are on the narrow left and right shelves. |
| 120 | 2160158026 | `bc0e3ef3c9ba21242dfe5df456489de9f31ba47ee6a54b2e27366583869e7cc8` | Ordinary traversal reaches the upper modules; the surfaces have shifted from cyan toward green. |
| 240 | 2180157946 | `4669599d2e1e37c6cd7b7c4971613c720a847c965841ab02c2cd4c7a7c167c4e` | Later combat preserves every arena edge while surface facets continue changing color. |
| 360 | 2200157866 | `ae61a7f854309c7b126f3fcc4950ac437ac17d9e013b1813786e1ecc9f949193` | Stable overview with the surfaces at the lime end of their color cycle. |

Native coordinates are x rightward and y downward. Clone world coordinates are `(native_x - 640, 360 - native_y)`, so one native pixel is one world unit. Edge comparisons allow ±2 px for antialiasing, compression and animated facet paint; the authority contours below use the stable hard edge rather than the changing fill.

## Finite geometry fixture

The arena has 33 fixed surfaces. IDs are clone identities, not recovered source-engine IDs. A rectangle is listed by `[left, top, right, bottom]`; polygon points follow the visible native contour clockwise.

| IDs | Native placement | Count | Authority contour |
|---|---|---:|---|
| 0–4 | x origins 42, 312, 582, 852, 1122 | 5 | Prototype `[(x,365),(x+116,365),(x+116,393),(x+90,419),(x+76,419),(x+76,522),(x+40,522),(x+40,419),(x+26,419),(x,393)]`. |
| 5–8 | x origins 217, 495, 747, 1027 | 4 | Narrow stems `[x,441,x+36,579]`. IDs 5 and 8 are the observed spawn shelves. |
| 9–13 | x origins 45, 315, 585, 855, 1125 | 5 | Cross prototype: vertical x offsets 36–73 from y 611 to the frame edge, horizontal x offsets 0–109 from y 647–686. The complete 12-point contour is the union, clipped at native y 720. |
| 14–17 | x origins 217, 495, 747, 1027 | 4 | Lower stems `[x,693,x+36,720]`, clipped by the frame edge. |
| 18–32 | centers 100, 370, 640, 910, 1180 | 15 | Per column: top block 37×37 at world y 49, lower-left 38×37 at x−21/world y 14, and lower-right 38×37 at x+21/world y 14. |

Bright-mask connected components reproduce the five upper bounding boxes at native x 42–157/312–427/582–697/852–967/1122–1237 and y 364–521 in the first two frames. The four stems stay at x 217–252, 496–531, 748–783 and 1027–1062 with y 441–577; compression changes an outer edge by at most one pixel. The lower crosses keep their 270-pixel cadence and y 612–719. At PTS 2200157866 the chroma threshold no longer selects the now yellow-lime faces, but direct hard-edge registration remains unchanged.

The gray/olive three-block caps are collision surfaces: blue stands on the right interior cap at the later frames. The large navy wedges below every structure are light-cast shadows and have no collision body. Facet boundaries and background brush polygons change between frames and are presentation only.

## Static determination and traversal

The hard-edge locations remain registered across six seconds while fighters and projectiles change pose and the surface fill moves from cyan through green to yellow-lime. No module translates or rotates beyond the ±2 px capture tolerance. The repeated pedestal/stem motif therefore does not establish piston motion; all 33 bodies are fixed.

The observed spawn centers are approximately native `(235,419)` and `(1045,419)`, mapped to world `(-405,-59)` and `(405,-59)` on IDs 5 and 8. The standalone replay uses `(-405,-58)` and `(405,-58)` to retain stable Rapier support. Its symmetric public input first jumps from those shelves to the inner upper modules, then uses a second ordinary jump to clear the lower cap blocks and settle on the left/right interior cap assemblies. Removing either all movement or all jumps changes the protected endpoint by more than 50 world units; no pose mutation or profile-only collision rule is used.

## Presentation and evidence boundary

The shared renderer uses a dark teal paper field, opaque deep-navy projected shadows, the authoritative contours, and clipped translucent facets. A deterministic six-second presentation ramp follows the observed cyan-to-lime progression; it does not mutate `face_rgb`, collider pose or the arena digest. The cap blocks receive a darker olive version of the same progression.

The source HUD shows `Ho` and `Pa` because orange selected Homing and blue selected Parasite before this interval. The standalone replay has no flow authority or loadout state and renders neither badge. This record proves only arena geometry, traversal and presentation. It makes no claim about Homing, Parasite, reload, damage-over-time, lifesteal, projectiles, winner, score, audio or production networking.

Ticket 68's [adjacent selection and combat record](fresh-match-lime-observations.md) independently confirms orange/Homing and blue/Parasite. It also limits the static determination to this six-second fixture: cap blocks are seated at sparse PTS 2239157710, but the right top square is lifted/rotated and the center lower-right block tilted by 2241657700. At least two assemblies are disturbed; the loose right square continues through 2271824246. The large pedestal/stem/cross contours remain separate from these caps. Arena faces also progress from lime to yellow by 2241657700, orange in samples 2261657620–2281824206, and red at 2289324176 through the result. The current fixed-cap implementation and six-second color ramp do not cover these later interactions or presentation. This does not invalidate the earlier bounded fixture, recover an adjacent motion/hue onset, or establish a moving-cap topology or collision impulse.
