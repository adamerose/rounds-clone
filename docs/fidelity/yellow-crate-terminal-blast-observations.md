# Yellow-crate terminal blast observations

## Source identity and decode rule

The selected interval is in `reference/MedalTVRounds20260903165304088.mp4`, SHA-256 `453954a7230401ed805be4e53dec41779a1913dfd69903671fc131fca2c8a18c`.
The video stream is AV1 (`libaom-av1`), 1280x720, `yuv420p`, with source time base `1/10000000` and an approximately 60 Hz cadence.
The audit used FFmpeg `7.1-essentials_build-www.gyan.dev`, libavcodec `61.19.100`, preserved source timestamps, selected exact integer PTS values, converted each selected frame to native RGBA, and hashed all 3,686,400 decoded bytes with SHA-256.
The raw source video and generated audit frames remain ignored and unmodified; PNG-container hashes and downscaled contact sheets are not frame identity.

The continuous slice begins at PTS 4220149786 (07:02.014979) and ends at PTS 4245983016 (07:04.598302). It contains 155 decoded source frames from the first through last anchor inclusive.

| Meaning | Source PTS | Time | Native RGBA SHA-256 |
|---|---:|---:|---|
| calm interval start | 4220149786 | 07:02.014979 | `0b43d0363e68046c92773d5cedd730c70c58d3d3b4604e1f8fa13bc4a7fc4d2f` |
| last calm frame | 4233483066 | 07:03.348307 | `d57dbd7ea73ba1d2e9c6c6fd37281629c0ee897aa52a29f6e81fb6cf532112bd` |
| first whole-frame response | 4233649732 | 07:03.364973 | `8fa8546c2b8a61b61b1d0ee58e217e4cf4b2ee99aedd437aa84606d50af84d14` |
| bright local burst | 4234149730 | 07:03.414973 | `d849c420675a75055a48f6fdf8a028a73192c1857d85fc84895c4732e32d1321` |
| peak radial echo | 4234983060 | 07:03.498306 | `0ca4ecadfc3d4ff6e2e7c12ad3ff5167b0f5d36429096f6f82af9a521b58b3b1` |
| long directional trails | 4237149718 | 07:03.714972 | `82feb79de9feb66b2fc6d600257b3107d9d833a3c86c71ff4fecacf5601cf8ca` |
| last combat frame | 4238483046 | 07:03.848305 | `74636191cb28cb44ac48d8f6106f5f9637b202b0097a35a966d2ac394a618c60` |
| adjacent result onset | 4238649712 | 07:03.864971 | `e315638c3fff8b0904470c38938dd9690349e26ff3c36be9c112a93d39e7f518` |
| `ROUND ORANGE` established | 4240983036 | 07:04.098304 | `aa4ad165071ac401b54fa2f6687a2ea08aef04ceb0c33dc32965fd5049e8ddce` |
| established result tail | 4245983016 | 07:04.598302 | `bd923e601703bc0fbd5fb9c1f45983abf5d5449db3ea0bb5c9be300be48bc032` |

## What consecutive frames show

- The calm interval shows a dark teal layered paper field, a staggered 4/5/4-like lattice of luminous yellow platform faces with long down-left shadows, and brown rectangular pieces stacked above many platforms. Both fighters converge on the upper-right stack.
- PTS 4233483066 is the last frame without the event response. The immediately following PTS 4233649732 brightens the fighter overlap and begins visible whole-frame chromatic displacement; there is no unobserved gap between those states.
- By PTS 4234149730, a white-hot core and asymmetric yellow/orange lobes occupy the upper-right contact. Fine sparks and short directional strokes radiate away from it.
- Peak PTS 4234983060 does more than bloom or move the camera. Platform faces, brown pieces, shadows, corner HUD, and the impact all appear in several radially separated copies. Left-side copies move farther left, right-side copies move farther right, and lower copies move downward, consistent with a viewport-centered zoom/warp. The separated copies carry a visible green/yellow/red ordering and extend from the impact across the entire frame.
- The peak frame has several discrete copies of the same hard platform edges rather than one continuous blur. A single lens displacement plus one RGB channel offset cannot produce those repeated same-channel silhouettes.
- At PTS 4237149718, the broad zoom has contracted, while long white, cyan, yellow, red, and green streaks continue from the upper-right impact toward and beyond the corner. Small separated dots and fragments persist elsewhere.
- A brown rectangular piece from the upper-right stack is visibly detached by PTS 4238483046 and has translated and rotated toward the arena center. That is direct evidence for dynamic arena response; the footage does not reveal its mass, friction, restitution, or exact impulse.
- PTS 4238483046 is the last combat frame. PTS 4238649712 immediately dims the arena and introduces the central score circles. `ROUND ORANGE` is clearly present by PTS 4240983036. The source therefore cuts the remaining effect decay into the result transition instead of showing a long natural tail.
- Orange is the result winner and blue is eliminated. The recording demonstrates a terminal high-energy hit, but the frames do not prove which active card or exact damage/explosion formula caused it.

The first whole-frame response reaches the chosen peak eight frames later, about 0.133 seconds. The directional-trail anchor is about 0.350 seconds after onset. The last combat frame is about 0.483 seconds after onset, and the result begins on the next decoded frame.

The user's supplied `rounds-effect-06-calm-yellow-crates.png` through `rounds-effect-09-high-damage-trails.png` show this same scene and response. A downscaled image search placed `rounds-effect-08-heavy-radial-distortion.png` nearest PTS 4234983060 and `rounds-effect-09-high-damage-trails.png` nearest PTS 4237149718; these screenshot matches are scouting corroboration, not replacements for the native source hashes above.

## Bounded audio audit

All three audio streams were decoded from 07:01 through 07:05 as 48 kHz stereo float samples and measured in 10 ms windows. Streams 0 and 1 carry nearly identical envelopes; stream 2 is silent in this interval. The audit does not assign semantic names to those tracks.

The non-silent streams are quiet around the first visual response, rise sharply after approximately 423.45 seconds, and peak around 423.60–423.61 seconds at about 0.303 window RMS and 0.922 absolute sample peak. Energy remains elevated through 423.90 seconds and decays through the result transition. Input-side seeking and 10 ms windows make these timings approximate rather than frame-exact, and the compressed mix does not separate shot, impact, voice, music, or result components.

This is enough to preserve an impact-audio event hook and timing target. It is not enough to recreate timbre or to extract a reusable sound; original audio implementation remains owned by ticket `020`.

## Rendering conclusion

The footage does not require a renderer replacement.

- Ordinary Bevy meshes/sprites and HDR materials can draw the arena, local core, lobes, sparks, fragments, and directional streaks.
- Existing `Bloom`, `ChromaticAberration`, `LensDistortion`, and camera transform components cover glow, a base channel split, one warp, and shake.
- The source-proved gap is the set of discrete, radially scaled copies of the already composited world and HUD. One bounded fullscreen multi-tap pass over the final camera color can reproduce that behavior by sampling several scaled UV positions with per-tap color weighting and an event-driven envelope.
- That pass should remain a private presentation detail shared by visible and offscreen capture. It does not justify a custom renderer, gameplay-facing render graph, alternate capture renderer, or serialized shader state.

## Approximations and non-claims

- The source establishes visible geometry, event ordering, result ownership, and motion direction. Clone replay inputs, collision sizes, damage, mass, material coefficients, impulse strength, shader sample positions, and envelope values are tuning approximations.
- The detached brown piece is treated as a real dynamic crate because its translation and rotation are visible. The footage does not prove that every brown piece uses identical physics parameters or that all yellow faces are static in other rounds.
- No card badge in the corner is accepted as proof of the terminal event's hidden formula.
- No proprietary frame, texture, shader, or audio byte is committed or shipped.
- The other explosive intervals in both recordings remain future coverage under tickets `018`, `019`, and `020`; this contract does not silently claim them through one effect implementation.

## Implemented replay evidence

`yellow-crate-terminal-blast-replay`, seed 43, advances all 155 source-bound frames through authority-owned input, projectile contact, explosion, blue elimination, orange scoring, crate motion, result onset, and `ROUND ORANGE`. The final state SHA-256 is `a78af2b36e3bc17bfcf6ff561666af4fb0b3bc143b9740ff2fddbbf1b6e760cb`; the final ordered dynamic-body digest is `490ea828d15da52c18157672dc5bc0efa25b6441ef66fbe528e9ecf656f4de45`. Crate 315 is airborne and rotated after the blast, and halving the authority impulse changes the protected dynamic-body digest.

One authority and two separately launched UDP clients each sent 155 inputs and received ticks 1 through 155. Their final snapshots agreed with each other, the server, and the client-host, while client 0 rendered the received final snapshot through the real GPU path. This is localhost development evidence, not a production networking claim.

The final capture set contains the ten table anchors above at 1280x720. Its metadata binds exact source PTS/native RGBA hash, replay tick, state/dynamic/arena/combat/round digests, executable hash, clone-frame hash, renderer identity `bevy-0.19.1-2d-hdr-shared-scene-single-final-composite-radial-echo`, the single-pass identity, and the tick-81 audio hook. Original-resolution source-left/clone-right inspection found the same 4/5/4 arena silhouette, upper-right blast, whole-frame outward echo direction, green/yellow/red ordering, narrow multicolor trail direction, detached crate, adjacent result cut, score circles, and result cadence. The clone's paper texture and particle lobes remain simpler approximations, and its orange onset circle is fully rather than half filled.

The implementation added about 775 lines of product behavior, 177 lines of focused tests, and 98 lines of capture/network-smoke support before documentation. Product behavior therefore remains substantially larger than the evidence support: simulation owns the replay and Rapier state, networking owns projection observations, presentation owns the shared scene and one private 35-line WGSL pass, and the client/automation crates only bind captures and process evidence.
