# Sparse lime cap motion

Ticket 72 measures two cap locations in the second recording over inclusive native PTS 2239157710–2271824246. The right upper square changes pose independently of the bright pedestal and its two lower squares; the center lower-right square is briefly tilted. These observations support separate movable cap geometry in a later reconstruction, not recovered source physics. No gameplay coverage is delivered, and goal 59 remains open.

## Source and reproduction

The recording is `reference/MedalTVRounds20260903170709695.mp4`, 791,095,141 bytes, SHA-256 `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9`, identified by `reference/manifest.json`. The timebase **1/10,000,000 second** comes from retained `out/ticket-068/sparse-stderr.txt` (`showinfo` input timebase and stream `10000k tbn`), corroborated by [the corrected published observations](fresh-match-lime-observations.md). It is not a manifest field. Corrected source candidate `e97b280c5ce2dd7d5488e7942ff3d29a4e7da6d3` was published with receipt `261a9b69c632605a98f273a47006bd9d5e1d9594`.

[The exact native-PTS/RGBA table](lime-cap-motion-measurements.tsv) contains all 14 samples, original paths relative to the repository root, raw and registered cap centers, visible/fitted corners, orientation, per-sample uncertainties, identity confidence, approximate fighter face centers and overlap/occlusion notes. Structured cells are JSON; `null` means unmeasured, not zero. Original PNGs are 1280×720. Hashes are SHA-256 of their 3,686,400 decoded packed RGBA bytes, not compressed PNG files. The source file SHA and every cited PNG hash were independently recomputed without decoding the video.

All derived evidence is retained outside the disposable worktree in root `out/ticket-072`. Run from any checkout:

```text
python C:/_MyFiles/Programming/Projects/rounds-clone/out/ticket-072/measure.py CHECKOUT_PATH
```

The bounded Pillow/NumPy helper runs in seconds, verifies source identity, PNG hashes, exact PTS order against retained decoder metadata, native timebase and the absence of additional retained samples in this interval. It then reproduces registration, cap fits, `measurements.json`, `measurements.tsv`, and all fourteen `annotated-PTS.png` files. Manual fighter points, partially visible landmarks and crop selections are explicit inputs, not automatically recovered hidden poses. Its stdout is retained in `verification.json`. No Cargo, decoder, GPU or visible GUI is used. The script and its hash are recorded in `out/ticket-072/artifact-manifest.json`; source originals remain under `out/ticket-068`. Derived media and scripts stay ignored; the tracked table and this rationale contain no proprietary media bytes.

Native-resolution comparisons include `annotated-2239157710.png` / `annotated-2241657700.png` (sparse first disturbance), `annotated-2244157690.png` / `annotated-2251657660.png` (leftward displacement), `annotated-2254157650.png` (orange overlap) and `annotated-2271824246.png` (late loose square). Each retains the full native frame with measurement outlines; the untouched original named by the table must also be inspected. Magenta marks the right square, cyan the center location, white circles approximate face centers, and white rectangles the independent pedestal registration regions. Outlines do not signify contact. All fourteen original frames and resulting measurement overlays were visually checked.

## Registration and uncertainty

Coordinates are native x right/y down. Register against three independently stationary bright pedestal/stem silhouettes in half-open rectangles `[35,372,165,535]`, `[575,372,705,535]`, `[1115,372,1245,535]`. These omit all cap geometry. The helper searches integer translations ±8 px, minimizing binary silhouette XOR against the first frame. Its bright mask is R>170, G>60, B<100. The JSON retains each rectangle's best shift, mismatch count and runner-up; the median of the three shifts is subtracted from cap centers. Fighters can contaminate a small part of the middle ROI late in the interval; outer pedestals independently constrain the same result. No moving cap or fighter is used as the registration reference.

All median translations are `(0,0)` except PTS 2244157690, `(0,−3)`. At that distorted frame the individual x shifts are −1, 0, +1: a single translation is only an approximation to the chromatic/distorted image. At 2264324276 the outer-right x shift is +1 while the other two are 0. Use **±2 px registration uncertainty**, not exact camera recovery. The full sample-wise residuals remain in JSON.

Within hand-selected cap crops, the script thresholds dark olive/brown pixels, excludes the right assembly's seated lower rectangles, keeps the largest four-connected component and fits its minimum-area oriented rectangle. The selected mask, crop bounds and threshold are explicit in the helper. Pixel-center edge lengths near 35–38 px are consistent with roughly 37-pixel square envelopes; this is not a subpixel collider recovery. Centers/corners carry ±2 px normally and ±3 px during distorted/overlap samples; angles carry ±4° normally and ±6° in those samples. The partially obscured seated right envelope is manual, ±3 px/±5°. Add registration uncertainty conservatively when comparing registered poses; endpoint-difference bounds add both endpoint uncertainties. Decimal output is reproducibility precision, not measurement accuracy. Fighter **visible face** centers are manual ±5 px and do not identify engine body origins, feet or support points.

Angles are image-space square-edge orientation modulo 90°, in `[−45°,45°)`. A jump across that boundary is not a recovered spin reversal or angular velocity. Fitted corners bound visible paint and can include a small continuation under an overlapping sprite; partially hidden physical corners are not directly observed.

## Measured poses and identity limits

| Native PTS | Registered right square center | Edge angle | Center lower-right observation |
|---:|---|---:|---|
| 2239157710 | (910.0,310.5) | 0° | Seated envelope (661.5,345.5), 0°. |
| 2241657700 | (909.2,306.9) | −10.3° | Tilted envelope (658.0,340.5), −11.3°. |
| 2244157690 | (896.6,303.4) | −41.6° | (663.5,346.5), near axis-aligned after registration. |
| 2246657680 | (885.7,304.2) | +24.4° | (663.5,345.5), near axis-aligned. |
| 2249157670 | (866.7,310.0) | −21.8° | (663.5,345.5), near axis-aligned. |
| 2251657660 | (849.1,327.5) | +11.3° | (663.5,345.5), near axis-aligned. |
| 2254157650 | (831.3,347.8) | −24.8° | Blue obscures the right side; full pose and identity track stop. |
| 2256657640 | (805.6,404.3) | +3.2° | Partial silhouette only. |
| 2259157630 | (823.9,475.9) | +22.8° | Partial silhouette only. |
| 2261657620 | (855.9,567.9) | −14.9° | Partial silhouette only. |
| 2264324276 | (842.3,616.2) | +12.5° | Partial silhouette only; distorted image. |
| 2266824266 | (807.2,637.5) | +9.8° | Partial silhouette only. |
| 2269324256 | (780.7,670.8) | −10.3° | Exposed lower-right silhouette, but same-color left boundary merges with neighbor; no complete pose or identity rejoin. |
| 2271824246 | (773.4,668.8) | −32.9° | Same boundary ambiguity; visible landmarks only. |

The exact TSV/JSON values control if rounded display values differ. Right-cap fit orientation and apparent direction changes do not recover speed, acceleration, impulses or accumulated rotation. Its roughly 61 px leftward displacement by 2251657660 is well beyond registration/edge uncertainty; later samples first move right/down, then left again. The final square is near `(773,669)`, but the preceding `(781,671)` sample does **not** prove landing or a bounce: vertical difference is within uncertainty, and unseen intervening contacts are possible.

The right upper location corresponds visually to clone cap **27**, above lower squares 28 and 29 and pedestal 3 in [the earlier fixture](lime-modular-arena-observations.md). Call it **R**, a positional source label rather than an engine ID. The sparse continuation is probable because a matching square leaves that location, which remains empty, and a similarly sized square occupies successive nearby positions. Blue overlaps its upper edge at 2244157690 and orange's lower appendage meets/overlaps it at 2254157650. The major contour remains visible, but no uninterrupted adjacent-frame identity or contact is claimed. Hidden swaps, intervening contacts and trajectory changes cannot be excluded across the gaps.

The center lower-right starting location is visually compatible with clone cap **26**, call it **C**. This does not assign the already-offset upper center square to an earlier clone ID or assert that the whole assembly matches its six-second pose. C's tilt at 2241657700 is visible relative to stationary pedestal 2 and differs from R's pose. Its later near-axis-aligned location is a probable short local correspondence, not proof of rest. At 2254157650 blue begins obscuring it in these samples; its identity track stops. Through 2266824266 only listed silhouette landmarks are recorded. Even when more of the lower-right shape is exposed at the last two samples, its left boundary merges with another same-color cap; neither a complete square pose nor physical identity is recovered.

## Missing boundaries and later physics contract

Every interval between consecutive rows is sparse: twelve gaps are 2,499,990 PTS (0.249999 s) and the gap 2261657620→2264324276 is 2,666,656 PTS (0.2666656 s). There are no retained intervening PNGs in this entire interval, as checked across ticket 68's frame indexes. In particular **2239157710→2241657700 remains the sparse last-seated/first-disturbed bound**, not adjacent-frame evidence or an exact onset. The intervals before/after blue overlap, orange overlap, the apparent center return, center occlusion/reappearance and each later direction change all lack dense boundaries. No contact cause, onset instant, release instant, contact duration, hidden input or settled endpoint can be measured from these samples. Research is limited to these retained frames, not a new full-video inspection.

The visible separation and independent orientation justify representing the measured right square separately from its lower neighbors and bright pedestal. The center square's separate tilted silhouette justifies a second independent cap candidate. Source pixels do not reveal the original solver's body count, collision activation, joints or shape topology. Existing support observations in ticket 60 support cap collision geometry, but overlap in this later interval proves neither support/contact nor an impulse. Navy shadows remain presentation, not inferred bodies. Nothing here proves every cap moves, that every cap is dynamic from spawn, or that pedestal/stem/cross bodies move.

The smallest later authoritative-physics ticket should introduce ordinary translating/rotating square collision-body capability for the two source-supported cap locations, keep the measured pedestal/stem/cross contours independent, and preserve the existing six-second comparison. Parameters and initialization policy must be declared **reconstruction choices**, separately admitted and checked; none are source constants here. Public fighter/projectile interaction should drive general physics, with authoritative pose snapshots and shared rendering, then compare the identified samples within the stated uncertainties. Perturbed public inputs must distinguish physical response from a pose/impulse script; ambiguous center samples cannot become exact pose gates. No scripted onset at the first disturbed PTS is justified. Its admission must settle whether it can reproduce the observed transition from the earlier fixture without invented source events. This is a bounded proposal, not an admitted implementation or recovered mass, friction, restitution, suspension, velocity, contact callback or all-cap rule.

The [coverage ledger](footage-coverage.md) remains unresolved for later cap dynamics, connected lime combat and both complete matches. Ticket 60's earlier six-second static fixture is unchanged and remains valid for its own interval.
