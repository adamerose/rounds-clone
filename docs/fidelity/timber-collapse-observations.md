# Timber-collapse observations

This record compares recording `453954a7230401ed805be4e53dec41779a1913dfd69903671fc131fca2c8a18c` with the `timber-collapse-replay` profile at 1280×720.
The source file is `reference/MedalTVRounds20260903165304088.mp4`; the observed interval is 03:26.00–03:50.00.
Frames were decoded directly from that file with FFmpeg, without an intermediate transcode.
Generated source frames, clone frames, metadata, and contact sheets remain ignored under `out/ticket-040/`.
The reviewer contact sheet at `out/ticket-040/source-clone-contact.png` labels and juxtaposes all eight direct source anchors with their corresponding clone anchors; it excludes the four clone-only 100 ms sequence frames so they cannot be mistaken for direct source pairs.

## Direct anchors

| Source time | Clone tick | Direct source observation | Clone comparison |
|---|---:|---|---|
| 03:26.00 | 0 | A dense dark-red timber silhouette stands at center above a wide hot-pink floor. Circular weights hang on both sides. | The same main composition is present. The clone silhouette uses fewer, cleaner rectangular members and perfectly vertical ropes. |
| 03:39.80 | 828 | The structure remains intact while a bright projectile approaches the upper-left timber. | The clone remains fully constrained and shows a projectile arriving at the upper-left. Fighter routes and exact projectile position differ. |
| 03:40.40 | 864 | A compact orange-white explosion blooms against the upper-left structure. | The authoritative explosion appears at the same broad location and releases 17 fixed joints. The clone uses a compact irregular core, overlapping orange/yellow lobes, fragments, and dense sparks rather than an expanding opaque disk. |
| 03:40.90 | 894 | Nearby members have separated and the left hanging weight is displaced; most of the structure is still recognizable. | Snapshot transforms show local breakup and the left half beginning to deform. The clone flash has nearly ended, leaving moving sparks and fragments rather than a dominant light disk. |
| 03:41.20 | 912 | The left and upper members rotate and fall during a strong whole-screen radial/chromatic response with irregular light and particle lobes. | The clone has broad deformation, camera shake, a deformed faceted floor, and strong whole-screen radial stretch and RGB edge separation from built-in lens distortion and chromatic aberration. The clone response is cleaner and more symmetric than the compressed source echo. |
| 03:43.50 | 1050 | A broad debris pile occupies the left and center floor while some right-side members remain higher. | Real Rapier contacts produce a broad low pile with a few raised right-side members. Piece count and exact pile outline differ. |
| 03:45.00 | 1140 | The pile persists and continues settling during combat. | The clone pile persists, with substantially lower aggregate motion than shortly after impact. |
| 03:49.50 | 1410 | Combat continues above a persistent pile; the right suspended weight remains attached. | Both rope constraints remain active and the final rendered state retains the pile and suspended weights. Fighter choreography differs. |

The clone capture also includes ticks 870, 876, 882, and 888, at 100 ms intervals after the authoritative event, so the implementation can be inspected as motion rather than as unrelated endpoint stills.
Those four intermediate clone frames have no frame-exact source claim; they expose the simulated collapse envelope between the directly decoded 03:40.40 and 03:40.90 source anchors.

## What is authoritative and what is approximate

The 19 dynamic bodies, their project IDs, constraint activity, transforms, velocities, sleep state, and the explosion event come from the authoritative simulation snapshot.
Seventeen timber bodies use real dynamic Rapier bodies and fixed joints before impact; two weights use dynamic circular bodies and rope joints that remain attached.
At tick 864 the profile releases the fixed joints and applies a radial impulse to 16 bodies inside its radius.
Rapier contacts, gravity, damping, friction, restitution, and the retained ropes determine every later body pose; the replay contains no pose track.

The body count, member dimensions, density, friction, restitution, joint placement, 4,800-unit impulse, 520-unit radius, damping, exact fighters' input route, and event tick are tuning parameters chosen to reproduce the visible sequence.
They are not claims about inaccessible ROUNDS constants or the card stack visible earlier in the recording.
The explosion particles, light lobes, trails, camera shake, bloom, chromatic aberration, and lens distortion are presentation derived from the snapshot event and do not affect the state hash.

## Line and responsibility inventory

Against admitted base `370434817f97b604bc5df04e8ca9e9a8ea20557e`, the implementation adds 1,193 product lines, 171 test lines, and 21 automation-support lines across the Rust crates.
Product code owns the Rapier bodies, joints, contacts, impulse, ordered snapshot state, wire/profile propagation, client/server entry points, and the shared Bevy scene, reactive floor, particles, camera, and built-in post-processing.
Test code owns simulation ordering, release/contact/settlement/perturbation regressions, network agreement, capture alias safety, and the GPU offscreen assertion.
The small automation category owns the public inspect and two-client smoke orchestration; it does not implement separate physics, networking, replay, or rendering behavior.
Tests plus support remain smaller than product code (192 versus 1,193 added lines).

## Remaining differences

The source has a denser, more house-like initial silhouette, more varied member depth, granular background texture, and longer directional shadows.
The clone now uses a faceted hot-pink floor whose top contour, colored echo, and offset shadow respond to the snapshot explosion, but its facets are larger and more regular than the source floor and do not represent physical fracture.
The clone flash is compact, irregular, multi-lobed, short-lived, and spark-rich, but its exact lobe shape and green edge fringe differ from the source.
Its peak radial/chromatic shock is comparably unmistakable across the whole frame but is cleaner, more symmetric, and more strongly stretched at the outer edges than the source compression and motion echo.
The broad left-origin collapse and persistent pile match, but individual rotations, the left weight's swing, debris density, pile outline, fighter movement, camera response, and chromatic distortion do not match frame for frame.
These are known fidelity differences, not evidence that the dynamic-world and presentation boundaries are absent.
