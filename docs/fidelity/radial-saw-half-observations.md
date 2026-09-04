# Radial-saw half observations

This record bounds the next footage-derived slice in `reference/MedalTVRounds20260903170709695.mp4`, SHA-256 `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9`.
The selected source interval begins with the radial-saw arena at PTS 03:53.717 and ends with the last combat beat at approximately 04:07.700, immediately before the half-result presentation.
Source media stays ignored and unchanged, and decoded PNGs are disposable evidence under `out/ticket-042-source-exact/`.

## Timestamp defect

The existing second-recording rows in `docs/fidelity/footage-coverage.md` are not reliable as exact timestamp evidence from approximately 03:00 through at least 04:40.
The ledger currently places the radial-saw arena around 03:20–03:40 and labels another row `HOMING`, while exact source-PTS decoding places the radial-saw arena at 03:53.717 and the later suspended duel around 05:03.
Two independent exact decodes at source PTS 03:20.000 produce the same `WAITING` frame with PNG SHA-256 `1a36876bdc52204cf8a1a83094e3c26a014e9de1c6f78e8862ee4ac7c04c24fb`, not the documented radial-saw arena.
One cross-check decoded from the beginning and selected on output presentation time.
The other sought to an earlier keyframe, retained source timestamps with `-copyts`, and selected the global presentation timestamp.
Input-side `-ss` alone, hybrid double seeks, filename sequence numbers, and sparse-keyframe landings are not accepted as timestamp evidence.

Ticket 042 must audit and correct the affected contiguous ledger window before using it as coverage evidence.
The audit must bind every corrected row to exact source PTS and reproducible frame hashes rather than shifting the current shorthand by an assumed constant offset.
This observation does not claim that rows outside the audited window are correctly indexed.

## Direct visual anchors

| Source PTS | Decoded PNG SHA-256 | Direct observation |
|---|---|---|
| 03:53.717 | `f14b1d4b2a463cebe66e85b6cb54f8668c9eac972bb07cd595171d197e7498d7` | The radial-saw arena is established with fighters on opposing upper slopes, a dark diamond enclosure, bright white/cyan diagonal and diamond surfaces, and rotating saw geometry. |
| 03:56.000 | `b67aefd3506b3b0f51e072e599e640586925793e7102681c06d01adaaf976b34` | Both fighters traverse the upper slopes while a bright owner-colored projectile and trail cross the chamber, and the central saw has advanced its angle. |
| 04:00.000 | `65801cc09107073b95630e2950255b6c1fa0dcc4e2eaafdbdbc6f281a073b659` | Orange is struck or blocking near the upper-left boundary with a compact white/pink flash and particles while blue remains on the right slope. |
| 04:01.750 | `531eb1aceee554b586345ad5bbd6726a6922e927f01d37f3d36942720fd4d9af` | A strong icy blue-white burst fills part of the chamber and drives the most demanding impact-effects beat in the slice. |
| 04:04.000 | `e6394dfb6151fec667a6c47b301a10353910df71a82ee5ad297a66f03defb254` | Combat continues across the symmetric chamber with projectile trails, long player shadows, and a visibly different saw-tooth orientation. |
| 04:07.750 | `62db692dd79662cf85249b8e229a469a9a1aa572fa304d7f31f3f5015396134a` | The first checked frame just beyond the selected end still shows both fighters and the saw chamber immediately before the result overlay, which bounds the final transition more tightly during implementation. |

The bright surfaces and burst are direct visual facts.
Calling the treatment “ice” describes its appearance only; this interval does not establish a friction constant, freeze rule, or destructibility mechanic.
The saws visibly rotate, but the checked anchors do not show a fighter touching one, so saw-contact damage or lethality is not a fidelity claim for this ticket.

## Measurements required during implementation

- Decode short consecutive source sequences around the arena reveal, ordinary shots, the 04:01.750 burst, and the final combat beat to measure duration, motion, layering, and camera response rather than matching isolated stills.
- Record saw angles across consecutive frames and reproduce direction, period, phase relationship, collider silhouette, and authoritative reset.
- Separate fixed collision geometry from shader, texture, particles, light, trails, camera impulse, chromatic offset, and fullscreen distortion so presentation-only state cannot alter simulation results.
- Extract semantic source-audio event times and envelopes for shots, impacts, the strong burst, saw ambience, and arena cadence, then recreate the cues independently without copying source samples.
