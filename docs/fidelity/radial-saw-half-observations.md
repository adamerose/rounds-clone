# Radial-saw duel and half-transition observations

This record bounds the next footage-derived slice in `reference/MedalTVRounds20260903170709695.mp4`, SHA-256 `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9`.
The selected continuous interval begins with the fully revealed radial-saw arena at source PTS 2320490718, or 03:52.049072, and ends with established `HALF BLUE` presentation at source PTS 2476823426, or 04:07.682343.
The last undimmed combat frame is PTS 2471823446, or 04:07.182345, and the result transition begins on the immediately following frame at PTS 2471990112, or 04:07.199011.
Source media stays ignored and unchanged, and decoded working files remain disposable evidence under the candidate-specific `out/ticket-042/` directory.

## Canonical frame method

Canonical source frames use FFmpeg `7.1-essentials_build-www.gyan.dev`, libavcodec `61.19.100`, and its `libaom-av1` decoder.
Decode stream `0:v:0` at native 1280×720 and time base `1/10000000`, preserve source timestamps with `-copyts`, select on integer source PTS, convert the first selected frame to packed RGBA with `format=rgba`, and emit one raw-video frame with passthrough timing.
The resulting byte plane is exactly 3,686,400 bytes, and its SHA-256 is the frame identity recorded below.
For a requested wall-clock instant that is not a source-frame PTS, choose the first frame whose source PTS is greater than or equal to the request, then record its actual integer PTS and `pts_time`.
A full decode and an accurate seek to a known earlier keyframe are acceptable only when both preserve source PTS, apply that first-frame rule, and produce the same RGBA pixel hash.
PNG container hashes, output sequence numbers, input-side `-ss` without source-PTS selection, hybrid double seeks, and inferred constant offsets are not frame identities.

```text
ffmpeg -ss <earlier-keyframe-seconds> -copyts -i <source> -map 0:v:0 -an -vf "select='gte(pts,<integer-source-pts>)',showinfo,format=rgba" -frames:v 1 -fps_mode passthrough -f rawvideo <frame>.rgba
```

## Timestamp defect

The second-recording rows in `docs/fidelity/footage-coverage.md` are unreliable as exact timestamp evidence from approximately 03:00 through at least 04:40.
The ledger places the radial-saw arena around 03:20–03:40 and labels another row `HOMING`, while native exact-PTS inspection places this arena at 03:52.049072–04:07.682343 and a later suspended duel around 05:03.
For a request at 03:20.000, the canonical first source frame is PTS 2000158666, or 03:20.015867, and it shows `WAITING`, not the radial-saw arena.
Ticket 042 must audit and correct the affected contiguous ledger window before using it as coverage evidence.
Each corrected row must use native source PTS and canonical pixel hashes rather than an assumed constant offset.

## Canonical anchors

| Source PTS | RGBA pixel SHA-256 | Direct observation |
|---|---|---|
| 2000158666 / 03:20.015867 | `c4c9547151263157cf54afe9495c7f1b1103cc3c2da8d3b29314bb0c57a09a6d` | `WAITING` is visible, disproving the ledger's radial-saw placement around 03:20. |
| 2320490718 / 03:52.049072 | `6266d4e7fe24f1d0c7069279479c341dca5229d7b596e077368e4e73eb9c236f` | The arena is fully revealed with both fighters on opposing upper slopes, a dark diamond enclosure, bright white/cyan platforms, a central saw, and a second saw partly below frame. |
| 2471823446 / 04:07.182345 | `687acf71fb2fce461692bd7443b7703274dcb895a6b17b6a86e1bbfd136e92ce` | This is the last undimmed combat frame; an ordinary compact white impact cloud and small orange particles remain near the upper-left slope while blue stands at lower right. |
| 2471990112 / 04:07.199011 | `d9161d231c3e90438ea7c638abe2dfe1feba2f1bf28edc3a94a841d4b69b7cca` | The next frame dims the arena and introduces central result circles, establishing the exact combat-to-result boundary. |
| 2476823426 / 04:07.682343 | `0b9d2136b7e010a359d95e3d2310a3e55c1e851429537cefcbd7cece12d185ca` | `HALF BLUE` is fully readable above the result circles. |

The reviewed native sequence around the previously claimed 04:01.750 event shows ordinary traversal, projectiles, small particles, and saw motion.
It does not show a distinct ice burst, fullscreen distortion, or another special semantic event.
Those rejected claims create no implementation or audio obligation.
The white/cyan surfaces and large bright brush-stroke background are direct visual facts.
Calling the surfaces ice-faced describes appearance only and does not establish special friction, freezing, fracture, or destructibility.
The saws visibly rotate, but the interval does not show a fighter touching one, so saw-contact damage or lethality is not a fidelity claim.

## Measurements required during implementation

- Decode consecutive native frames around arena reveal, representative movement and shots, ordinary impacts, saw motion, and the combat-to-result boundary.
- Measure saw direction, period, phase relationship, silhouette, and authoritative reset across sequences rather than one still.
- Separate fixed collision geometry and authority-owned saw pose from background animation, trails, small impact particles, long shadows, result dimming, and UI interpolation.
- Do not infer sound behavior from this visual audit; ticket 042 carries no audio-specific outcome.

## Implemented motion measurement

A 120-frame native crop from PTS 2320490718 through 2340490638 was measured from the central saw rather than inferred from one still. The strongest eightfold silhouette harmonic advances counter-clockwise at approximately 7.43 rad/s, a 0.846-second period, with linear phase fit R² 0.99995. The lower saw begins approximately 0.33 rad ahead of the central saw in the reveal frame. The replay therefore uses stable saw IDs 200 and 201, eight teeth, +7.43 rad/s, central phase 0.18 rad, and lower phase 0.51 rad. Rapier owns the advancing and reset poses; renderer time is not involved.

The canonical audit also decoded the first frame at or after every ten-second request from 03:00 through 04:40. Those actual PTS values and RGBA hashes now replace the shifted descriptions in `footage-coverage.md`. This locates `WAITING` at 03:20.015867, the blue `PARASITE` draft at 03:30.015827, the lime modular arena at 03:40.015787, the radial arena beginning at 03:52.049072, the result at 04:07, the hanging articulated arena from 04:10, and `FAST FORWARD` at 04:40.015547.

## Source/clone motion comparison

| Signal | Native source | Implemented replay | Remaining difference |
|---|---|---|---|
| Arena silhouette and scale | broad dark notched diamond reaches near the frame edges | notched dark enclosure spans x ±448 with the same two upper bars, two lower bars, center diamonds, and partly cropped lower saw | clone edges and platform facets are cleaner and less brush-textured |
| Saw period and phase | eight-tooth, counter-clockwise, about 0.846 s; lower saw about 0.33 rad ahead | IDs 200/201, +7.43 rad/s, 0.18/0.51 rad initial phases, snapshot pose from Rapier | source teeth have hand-painted edge variation |
| Platform/background motion | fixed bright surfaces over continuously shifting white/cyan brush work | fixed authoritative rotated colliders; 82 deterministic, jagged, tapered multi-segment strokes plus edge bristles move from snapshot tick | clone marks are cleaner and less densely layered than the source's painted texture |
| Long shadows | deep navy surface projections down the enclosure | render-only quads extend from each rotated platform along one consistent light vector | source shadows contain more texture and local variation |
| Projectiles and ordinary hit | brief warm trail; compact white cloud and small orange particles near upper left at the final exchange | tick 360 visibly retains the ordinary projectile and trail; tick 540 retains its white/orange cloud; a second authority impact appears upper left on tick 908 | clone particle shape is more radial and regular than the source cloud |
| Result and cadence | last combat PTS 2471823446, strongly dimmed result onset on adjacent PTS 2471990112, established text by PTS 2476823426 | tick 908 combat, tick 909 begins at 68% dim with score circles, tick 938 `HalfBlue`; authoritative score persists from `[1,0]` to `[1,1]` | clone font weight, circle scale, and dim color remain approximate |

The remaining differences are presentation fidelity gaps, not evidence for the rejected ice burst, distortion, burst event, audio, or saw damage.
