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
| 03:40.40 | 864 | A compact orange-white explosion blooms against the upper-left structure. | The authoritative explosion appears at the same broad location and releases 17 fixed joints. The clone bloom is larger and more circular. |
| 03:40.90 | 894 | Nearby members have separated and the left hanging weight is displaced; most of the structure is still recognizable. | Snapshot transforms show local breakup and the left half beginning to deform. The clone core remains brighter and larger than the source at this time. |
| 03:41.20 | 912 | The left and upper members rotate and fall during a strong whole-screen radial/chromatic response with irregular light and particle lobes. | The clone has broad deformation, camera shake, built-in chromatic aberration and lens distortion, sparks, and HDR bloom. Its response is much weaker at the screen edges and reads mainly as one large flat yellow disk with a warm veil. |
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

## Remaining differences

The source has a denser, more house-like initial silhouette, more varied member depth, granular background texture, a faceted and visibly reactive floor, and longer directional shadows.
The clone uses a cleaner geometric structure, a flat floor strip, broad translucent background bands, and simpler shadows.
Its explosion is too large and remains visibly dominant longer than the source's compact multi-lobed, spark-rich flash; the clone core reads as a flat yellow disk, its sparks are more regular, and its whole-screen radial/chromatic shock is much weaker.
The broad left-origin collapse and persistent pile match, but individual rotations, the left weight's swing, debris density, pile outline, fighter movement, camera response, and chromatic distortion do not match frame for frame.
These are known fidelity differences, not evidence that the dynamic-world and presentation boundaries are absent.
