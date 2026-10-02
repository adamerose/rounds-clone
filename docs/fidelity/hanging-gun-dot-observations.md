# Gun-dot visibility and fading in retained hanging combat

The early orange count change is supported as a **visibility effect**: the head-side part of the gun passes behind the hat/head and reappears. It does not require a refill. The later muzzle-side dot fades at an exposed position and remains dim in later readable views, including the first-burst witness. That is a **persistent presentation change over the sampled interval**, not proof of persistent ammunition depletion. Hidden intervals remain unknown.

This refines [the first-combat observations](hanging-first-combat-observations.md), without changing product mechanics, block/reflection, or gameplay coverage. Source68 is not used. Admission `codex:01a0fd6c-3364-7f72-92e7-fa7a645e8def` inspected comparison sheets only; the measurements here recheck the retained native originals.

## Identity and reproduction

All evidence resides at integration-root `C:/_MyFiles/Programming/Projects/rounds-clone/out/ticket-071`; originals remain in `out/ticket-067`. The supplied recording `reference/MedalTVRounds20260903165304088.mp4` was hashed again: 782,876,414 bytes, SHA-256 `453954a7230401ed805be4e53dec41779a1913dfd69903671fc131fca2c8a18c`, matching `reference/manifest.json`. Every one of the 164 retained PNGs matches its indexed SHA-256 over native 1280×720 packed RGBA (3,686,400 bytes). The index's ordered native PTS also match retained `decode-stderr.txt`; numerically ordered PNG filenames match the index. No additional footage was decoded. This verifies retained identity and provenance consistency; it does not independently redo the earlier decode.

Run with the existing Python/Pillow installation:

```text
python C:/_MyFiles/Programming/Projects/rounds-clone/out/ticket-071/measure.py --repo C:/_MyFiles/Programming/Projects/rounds-clone
```

The run takes seconds locally, hashes the recording and all retained frames, and produces `verification.json`, `measurements.json`, `measurements.csv`, `table.md`, and four native-scale comparison sheets: `early-native.png`, `fade-native.png`, `later-native.png`, `blue-native.png`. Each panel pairs an untouched native crop with its annotated copy (cyan barrel reference, green exposed apertures, magenta excluded apertures). Original PNGs can be found by exact PTS in `out/ticket-067/frames.json`. Enlarged nearest-neighbor inspection sheets and their helpers are also retained; they are viewing aids, not new source samples. These ignored files must be retained with the root evidence, not moved into product assets.

Reproduction inputs are bound by SHA-256:

- `out/ticket-067/frames.json`: `bdb4982a3f06803898bb6904cfc785a5cd39305b863ba1967c95580514464954`.
- `out/ticket-071/measure.py`: `f285b6354aa6a8b94a9a570964dacbc7b71f4b0367dc1efbf9580843bab10b8d`.
- `out/ticket-071/annotations.json`: `003af20c2fe223b81943dfd6048588804796727e9896797e63e17712fa1697ef`.

## Measurement and uncertainty

Coordinates are native image pixels, x right and y down. Manual annotations mark a short visible barrel reference segment, directed from head side to muzzle side. Slots 1/2/3 follow that direction. They are projected presentation positions, not persistent engine identifiers. Registering the image center against the segment gives `u` along the barrel and `v` perpendicular to it; no scale normalization or 3D pose recovery is claimed. For the nearly horizontal orange views, slot spacing is about 4 px and dots lie roughly 4 px above the barrel. Strongly foreshortened views cannot be compared as a rigid two-dimensional translation.

Each exposed slot uses a fixed 3×3 aperture at the annotated center. `meanG` is mean encoded green (0–255), a reproducible brightness proxy rather than linear-light luminance. The full JSON/CSV also gives peak green, yellow-weighted centroid, and the min/max means when the aperture shifts one pixel up/down/left/right. The centroid uses `max(0,min(R,G)-B)` weights; a residual centroid in a dark aperture is not a detected dot. Coordinates and barrel endpoints carry roughly ±1 and ±2 px uncertainty respectively; blue oblique correspondence carries about ±2 px. Tiny dots and video compression make one-pixel photometry changes large. The table reports nominal aperture centers, with decimals retained only for reproducibility, not subpixel accuracy.

Head overlap is manually assessed against the hat/head silhouette. `headEdgeX` in the annotations is the approximate rightmost silhouette at orange's dot row, uncertain by about 2 px; `headGap` in JSON is aperture-center x minus that edge. It is a local horizontal clearance check, not a segmented 3D occlusion model. At 2598156274 slot 1 is behind that edge (331 versus about 333); at 2598322940 it clears it only narrowly (334 versus about 332). That boundary is uncertain, while the wider exposed views confirm the return. During the later fade the muzzle-side slot is about 16–20 px beyond the head edge, far beyond that uncertainty.

Head-, arena-, glare-masked and unresolved slots have **no brightness measurement**. No intensity from their covering pixels is treated as a dot. The glow adds broad illumination around orange, but the three small gun apertures remain separately exposed; compare their simultaneous brightness rather than interpreting the glow as a resource event. Blue's flash and tight turn are excluded. At blue 2603322920 and 2603656252 three yellow regions are separable, but the oblique barrel makes correspondence less secure than the orange broadside views.

## Separate conclusions

At 2597156278/2597322944 three orange dots are exposed. During the shot and recoil/turn (2597489610–2597822942), the gun lies behind the hat. At 2597989608 the projected dots merge and overlap the head; the older phrase “two discernible” is too precise for independent slot photometry. At 2598156274 the two outward dots are exposed while the inward slot remains behind the hat. Adjacent 2598322940 exposes three positions again as the gun clears it. This supports occlusion as a sufficient explanation of the early apparent restoration; it cannot exclude an additional hidden resource change. No constant-count requirement can be imposed on screen pixels during the turn.

The later fade affects the **opposite, muzzle-side slot**. At 2599156270 mean G for slots 1/2/3 is 178/160/158; at 2599322936 it is 155/152/96; at 2599489602 it is 156/143/46; at 2599656268 it is 151/147/14. The third-to-average-of-first-two ratio falls about 0.94 → 0.62 → 0.31 → 0.09. Even with the one-pixel aperture shifts, the third slot's 2599656268 range (7–25) is disjoint from the other two (102–151 and 103–147). Its exposed registered location excludes the head and shows a fade, not a dot merely leaving the image. Broad illumination changing uniformly cannot explain the selective loss.

Later readable orange samples at 2600322932, 2601322928, 2602656256 and 2608322900 retain two bright regions and a dim muzzle-side slot. At the burst mean G is 141/153/15. This rules out a brief glow-only masking explanation of the later loss. It does **not** prove uninterrupted persistence: at 2604322916 the arena compromises the outer slot, and at 2605489578/2607156238 it hides the gun. No restoration was established in these sampled views; unsampled or hidden changes remain possible.

Blue is a useful contrast, not a matching resource experiment. Three regions are exposed before its shot (2602322924/2602656256). The tight turn and flash prevent a count at 2602822922–2603156254. Three yellow regions are separable after the projectile has left at 2603322920 and 2603656252. At 2604322916 two are bright and the projected outward aperture is dim (129/145/49). The oblique registration and changed pose weaken a specific same-slot fading claim; this is a later two-bright-region appearance with the mechanism unresolved. The later name/bar/arc overlap is excluded. Blue's held ball stays white across the shot, contrasting with orange's dark ball after the separate glow. Neither case provides a refill, same-gun cooldown, or controls.

## Joint timeline through the first burst

The table below combines inspected gun crops and their native full-frame context. “Defensive-looking” describes the glow's appearance, not its input or causal role. Event bounds are those of the retained first-combat record; no new timed resource sequence is inferred.

| Native PTS | Orange muzzle / glow / held ball | Gun visibility and blue contrast |
|---:|---|---|
| 2597156278–2597322944 | Before orange muzzle; no large glow; white ball | Three orange dots exposed |
| 2597489610 / 2597656276 | Orange muzzle glint / separated projectile; white ball | Gun overlaps hat; no accepted count |
| 2597989608 / 2598156274 / 2598322940 | Projectile away; no large glow; white ball | Merged/overlapped, then two outward slots, then three exposed slots |
| 2598822938 / 2598989604 | Last no-large-glow / first large white glow; ball's transition becomes obscured | Three dots remain; restoration preceded glow by about 66.7 ms |
| 2599156270 / 2599322936 / 2599489602 / 2599656268 | Glow persists then subsides to rim/sparks; no second orange muzzle seen during fade; ball partly obscured | Selective exposed outer-dot fade |
| 2600322932 / 2601322928 | Rim; ball dark-looking then clearly dark | Two bright orange dots; no accepted continuous count through every intervening frame |
| 2602322924 / 2602656256 | Orange thin rim at latter witness; ball dark | Blue has three regions and white ball before shot |
| 2602822922 / 2602989588 / 2603156254 | No new orange muzzle established | Blue turns, flashes, then projectile at muzzle; white ball; count excluded |
| 2603322920 / 2603656252 | Orange glow's precise end uncertain; first-combat record has rim gone by latter PTS | Three blue yellow regions after separation; pale gun-side mark; white ball |
| 2604322916 | Orange ball dark; gun partly behind arena | Two blue bright regions; projected outer slot dim |
| 2605489578 / 2607156238 | Orange gun hidden; dark held ball; later contact splashes | Orange count unknown; blue gun/name overlap excluded |
| 2608322900 | First large upper-left burst; orange held ball dark | Two exposed orange dots and dim outer slot; blue arc/name overlap prevents count |

## Smallest supported reconstruction and surviving alternatives

A future playable admission can start with ordinary gun pose and drawing order to reproduce the early loss and return, plus a selectively fading gun-dot presentation for the later exposed change. The latter must arise from a general admitted state/effect rule; this research does not supply its trigger. Keep the separate glow/held-ball presentation as a separate observation unless further evidence links it. Preserve existing block/reflection. Do not add ammo state, a refill, shared charges, a shot-delay timer, or PTS-driven dot overrides on the strength of these measurements.

| Variant / family | Status and missing discriminator |
|---|---|
| Every observed count change is persistent depletion | Excluded as a necessary reading: early loss is explainable by overlap and reverses with pose. Hidden depletion remains possible. |
| All later loss is head occlusion or temporary glare | Excluded for the exposed orange witnesses, especially after glare subsides and at burst. |
| Restoration happens at the visible glow onset | Excluded as an explanation of the observed ordering; restoration is earlier. A hidden earlier action remains unconstrained. |
| Static three always-bright dots with pose alone | Insufficient for the selective later exposed orange fade. |
| Shot-linked delayed presentation / ammunition animation | Survives, but no original delay or resource rule is established. Need repeated clear gun views spanning shots without glow, including fade onset. |
| Defensive action sharing a gun resource | Survives as a hypothesis. Need isolated glows without shots and shots without glows, with exposed slots and held ball throughout; visible original input or state would be needed to assign controls. |
| Independent gun animation and held-ball/defensive presentation | Survives and requires fewer inferred couplings. Need uncoupled repetitions to distinguish it from sharing. |
| Actual depletion, refill, then depletion | Not disproved as hidden state, but unnecessary to explain the early restoration. Need a clear refill transition at stable pose, and evidence of its trigger independent of the later glow. |

The next contract should compare **visibility-aware appearance**, not mandate four resource-state transitions from “3→2→3→2”. No gameplay gap or full goal is closed by this research.

## Exact accepted and excluded samples

`NA` means deliberately unmeasured, never zero brightness. The complete JSON/CSV contains the aperture sensitivity, yellow-weighted centroids, head clearance, and reasons for exclusions. For a dim exposed slot, the listed center is the projected aperture location, not proof of a remaining dot. No interpolation is used between rows.

| PTS / fighter | RGBA SHA-256 | Barrel endpoints | Slots 1/2/3: image centers; gun-relative (u,v) | Mean G 1/2/3 | Visibility 1/2/3 |
|---|---|---|---|---|---|
| 2597156278 orange | `d722928cd0eb3f6a3ce67ecf5fd9fd66fe9be06f5cce9ab9fe6ade8c32a14d82` | [[340, 262], [352, 262]] | [344, 258] (4.0,-4.0); [347, 258] (7.0,-4.0); [351, 258] (11.0,-4.0) | 144.2 / 157.4 / 141.3 | exposed / exposed / exposed |
| 2597322944 orange | `da1a1281c5b6cdb6f2b920c579138d1d668f87c50d58dced7c83c89ee13aabe3` | [[340, 261], [351, 261]] | [344, 257] (4.0,-4.0); [348, 257] (8.0,-4.0); [352, 256] (12.0,-5.0) | 146.6 / 142.3 / 99.9 | exposed / exposed / exposed |
| 2597489610 orange | `44ee8688864d11f7092abc697993a01dcb3feb4ca6c0dd155358a9d048902ee3` | None | None excluded; None excluded; None excluded | NA / NA / NA | head / head / head |
| 2597656276 orange | `8a37be7e76e686d0d8e9100285a20fef154fab9f2e0383d2a17c83346a86d806` | None | None excluded; None excluded; None excluded | NA / NA / NA | head / head / head |
| 2597822942 orange | `0befe2090eec2a4d1d2ae8038e2c47eccc8241ad62df2d8e0167f71536e25961` | None | None excluded; None excluded; None excluded | NA / NA / NA | head / head / head |
| 2597989608 orange | `917ddf3d9b2e61ce5ecfd23b6f250031d8d6614de8854ca418f056925796e508` | [[329, 262], [336, 262]] | [329, 257] excluded; [332, 257] excluded; [334, 257] excluded | NA / NA / NA | head / head / unresolved |
| 2598156274 orange | `26f2ce323dcdb00f244b4e31055b430fe179f07506f44abc067f7ea649800c54` | [[332, 262], [339, 262]] | [331, 258] excluded; [335, 258] (3.0,-4.0); [338, 258] (6.0,-4.0) | NA / 138.0 / 145.2 | head / exposed / exposed |
| 2598322940 orange | `b3dc62016bb9844b877f3594eca13d18f810e8a37c9907561251788760e9904f` | [[334, 262], [344, 262]] | [334, 258] (0.0,-4.0); [338, 258] (4.0,-4.0); [342, 258] (8.0,-4.0) | 104.2 / 139.8 / 175.4 | exposed / exposed / exposed |
| 2598489606 orange | `6d00e86c2892162c3feeb351b62bfcea022492c04a08caa4953d85b33820ef32` | [[334, 262], [346, 262]] | [337, 259] (3.0,-3.0); [341, 259] (7.0,-3.0); [345, 258] (11.0,-4.0) | 132.3 / 122.9 / 176.0 | exposed / exposed / exposed |
| 2598656272 orange | `de5409a07f589417839033d8f6c1b8ed0775360ee5734f6e95b911f63771cdec` | [[337, 262], [348, 262]] | [341, 259] (4.0,-3.0); [345, 258] (8.0,-4.0); [349, 258] (12.0,-4.0) | 116.3 / 127.0 / 104.0 | exposed / exposed / exposed |
| 2598822938 orange | `f225ff9998451f0491226091bb9fc62eb700e3fee75dcb13913ac14e39285911` | [[340, 263], [351, 263]] | [343, 259] (3.0,-4.0); [347, 259] (7.0,-4.0); [351, 259] (11.0,-4.0) | 143.0 / 138.9 / 161.1 | exposed / exposed / exposed |
| 2598989604 orange | `9d14b46710380c4934a8b4da03379c623eac330935cecaa37ec78f2bde33b3de` | [[343, 264], [354, 264]] | [344, 260] (1.0,-4.0); [348, 260] (5.0,-4.0); [352, 260] (9.0,-4.0) | 154.0 / 153.4 / 159.1 | exposed / exposed / exposed |
| 2599156270 orange | `2b064081a63e25ff237f714fe9b538421955ebf6e920004edae798a34f171a45` | [[345, 264], [357, 264]] | [344, 261] (-1.0,-3.0); [348, 261] (3.0,-3.0); [352, 261] (7.0,-3.0) | 178.1 / 160.3 / 158.2 | exposed / exposed / exposed |
| 2599322936 orange | `44d2909b91640f33468969576f34090d20d01a4d3cb44332da2520258076f04e` | [[347, 266], [359, 266]] | [347, 263] (0.0,-3.0); [351, 262] (4.0,-4.0); [355, 262] (8.0,-4.0) | 155.2 / 152.3 / 95.7 | exposed / exposed / exposed |
| 2599489602 orange | `66a5749c06bb611ec21944f80a4984e4359aee504cf61714fee3b42e23215f52` | [[349, 266], [361, 266]] | [350, 263] (1.0,-3.0); [354, 263] (5.0,-3.0); [358, 263] (9.0,-3.0) | 156.2 / 143.2 / 46.1 | exposed / exposed / exposed |
| 2599656268 orange | `d4a9641927b629f3298a1b952e41a483afeefeaae7fa4fc03080f01ff75dfb69` | [[351, 267], [363, 267]] | [352, 263] (1.0,-4.0); [356, 263] (5.0,-4.0); [360, 263] (9.0,-4.0) | 151.1 / 147.4 / 13.9 | exposed / exposed / exposed |
| 2600322932 orange | `15e24c154d05ca1678f7a219ea46dbf83940e9eb995cc84c2971507d094e472a` | [[360, 264], [372, 264]] | [363, 261] (3.0,-3.0); [367, 261] (7.0,-3.0); [371, 261] (11.0,-3.0) | 112.2 / 122.2 / 22.2 | exposed / exposed / exposed |
| 2601322928 orange | `340ea2c16a1eb5e8b0f238a09029326ad02f3672d81e623ed3931cc6240a167a` | [[383, 266], [395, 266]] | [384, 263] (1.0,-3.0); [388, 263] (5.0,-3.0); [392, 263] (9.0,-3.0) | 147.9 / 141.7 / 0.0 | exposed / exposed / exposed |
| 2602322924 blue | `2bded9cfcba7c70fffff44e0f57c0c32435dcbbef52a1364c3fe760f32f3c5f8` | [[742, 94], [731, 92]] | [740, 91] (2.5,2.59); [736, 90] (6.62,2.86); [733, 89] (9.75,3.31) | 108.8 / 117.7 / 126.6 | exposed / exposed / exposed |
| 2602656256 blue | `337570a25d7be1577a9712e7f6fd034becfb47e475e8a547a98be0fcbb48ab9c` | [[734, 98], [724, 97]] | [733, 95] (1.29,2.89); [729, 94] (5.37,3.48); [726, 94] (8.36,3.18) | 159.3 / 146.4 / 150.1 | exposed / exposed / exposed |
| 2602656256 orange | `337570a25d7be1577a9712e7f6fd034becfb47e475e8a547a98be0fcbb48ab9c` | [[398, 272], [410, 272]] | [399, 269] (1.0,-3.0); [403, 268] (5.0,-4.0); [407, 267] (9.0,-5.0) | 143.2 / 124.2 / 37.4 | exposed / exposed / exposed |
| 2602822922 blue | `778247770cf400ff36ec4b62b8b59ff2ca6bd815ca17d19058bd8ae3f573623c` | None | None excluded; None excluded; None excluded | NA / NA / NA | unresolved / unresolved / unresolved |
| 2602989588 blue | `86bd3e21e40beb3d3ec7dc2b679cd34b603a504060772fc6a141b1f10e5ba5bf` | None | None excluded; None excluded; None excluded | NA / NA / NA | glare / glare / glare |
| 2603156254 blue | `bb3102dea2ef71c8b9c3515af27b847281b136da6e3621821ad7955c7c16ac24` | None | None excluded; None excluded; None excluded | NA / NA / NA | glare / glare / glare |
| 2603322920 blue | `e9c0583affd6f6bb169286ad160f537a7e22a9d9482e701e8e9bc257bc0efc2e` | [[729, 116], [723, 107]] | [728, 116] (0.55,-0.83); [727, 112] (4.44,0.55); [725, 108] (8.88,1.11) | 174.3 / 127.1 / 127.7 | exposed / exposed / exposed |
| 2603656252 blue | `7c948af828557fe2668a617a0d5abba8f392834d99dfa1aaae09629cbb35cedd` | [[716, 122], [711, 111]] | [716, 120] (1.82,0.83); [714, 116] (6.29,0.66); [712, 113] (9.85,0.08) | 153.7 / 138.9 / 150.3 | exposed / exposed / exposed |
| 2604322916 blue | `683033b8e018981168a013164508c618bf505b5ca0914f8efca7e85bebc2c0ac` | [[693, 133], [683, 128]] | [692, 129] (2.68,3.13); [689, 127] (6.26,3.58); [686, 125] (9.84,4.02) | 129.4 / 144.6 / 48.6 | exposed / exposed / exposed |
| 2604322916 orange | `683033b8e018981168a013164508c618bf505b5ca0914f8efca7e85bebc2c0ac` | [[397, 285], [409, 282]] | [398, 284] (1.21,-0.73); [402, 283] (5.34,-0.73); [406, 282] excluded | 91.3 / 157.6 / NA | exposed / exposed / unresolved |
| 2605489578 blue | `67f8f30003d831383c8401d7055739f679aee05a49c980e76a596644ebb9baa7` | None | None excluded; None excluded; None excluded | NA / NA / NA | unresolved / unresolved / unresolved |
| 2605489578 orange | `67f8f30003d831383c8401d7055739f679aee05a49c980e76a596644ebb9baa7` | None | None excluded; None excluded; None excluded | NA / NA / NA | arena / arena / arena |
| 2607156238 orange | `e5a71cadbfc267c0e6c12121b2c6131c92c4b856d43701b4a6c9889ff7372382` | None | None excluded; None excluded; None excluded | NA / NA / NA | arena / arena / arena |
| 2608322900 blue | `2963427ae45215917516e1e353df1f0f24779779871a1097d052bcfc9e4ab211` | None | None excluded; None excluded; None excluded | NA / NA / NA | unresolved / unresolved / unresolved |
| 2608322900 orange | `2963427ae45215917516e1e353df1f0f24779779871a1097d052bcfc9e4ab211` | [[402, 266], [414, 262]] | [402, 262] (1.26,-3.79); [406, 260] (5.69,-4.43); [410, 258] (10.12,-5.06) | 140.7 / 152.6 / 14.6 | exposed / exposed / exposed |
