# Vanilla arena catalog

## Binding target

The catalog targets public Windows build `21020021`, identified in-game as `v1.1.2.a75ee335a`.
The official Steam listing advertises “70+ maps,” which remains a lower bound rather than an exact active-pool count.
The public community workbook contains exactly one preview anchored in each worksheet row 2 through 71, so project IDs `arena-001` through `arena-070` bind to those rows.
An independent Thunderstore index lists six arenas removed after the 7 April release-era build, and none of those six entries appears among the workbook's 70 internal-name rows.
The current build randomizes arenas and exposes no clean-room catalog browser, so exhaustive runtime reconciliation remains unresolved.

## Clean-room boundary

The ignored workbook is research input and is never committed.
The committed catalog retains project IDs, source rows, preview and mask hashes, derived oriented boxes, classifications, and uncertainty.
It does not retain source images, community names, internal identifiers, extracted game data, original art, or presentation assets.
`tools/maps/build-map-catalog.py` reproduces `spec/maps.json` from a public XLSX export stored under ignored `research/raw/`.

## Row identity and masks

Workbook media filenames are not ordered by worksheet row and must never be used as arena identity.
The generator resolves each `xdr:oneCellAnchor` row through `drawing1.xml.rels` to the embedded 640 by 360 image.
It marks a source pixel as visible foreground when the maximum red, green, or blue channel is at least 24.
That uniform threshold separates the previews' 19–23-value background from visible layout pixels, including the inverted light boundary in `arena-003`.
Each entry records the embedded preview hash and the row-major binary mask hash.

## Scale and geometry

Ticket 002 measured a player body diameter of about 18 pixels in a 640 by 360 gameplay frame, so the catalog uses 18 source pixels per player diameter.
The global scale remains provisional with a ±20 percent tolerance because the public preview framing has not been matched to a controlled current-build capture.
The generator finds eight-connected visible components and decomposes them into oriented boxes using axis, diagonal, and principal-component candidates.
It starts with one fitted box for every component, then splits the worst-fitting boxes until total fitted area is no more than source foreground area divided by 0.75 or the arena reaches its 96-box cap.
The separate acceptance oracle requires the resulting 8-pixel occupancy grid to reach 0.75 intersection over union.
The catalog contains 1,790 oriented boxes, no arena exceeds 96, and the 70 accepted coarse scores range from 0.787459 to 1.0.
This topology-scale limit preserves disconnected islands and broad play spaces without optimizing full-resolution pixel overlap or tracing a source silhouette.
These boxes abstract visible structures; they remain collision hypotheses because a still cannot reveal hidden, one-way, decorative, or selectively collidable regions.

## Executable evidence

Every map stores its source-component count, coarse source and rendered occupancy, intersection and union cell counts, coarse intersection-over-union result, and the SHA-256 digest of the full positioned render mask.
The repository checker rasterizes the committed oriented boxes at 640 by 360, recomputes 8-by-8-pixel occupancy, verifies the arithmetic, requires at least one box per source component, enforces the 96-box cap, and requires the positioned-render digest to match.
The ignored workbook and generator are still required to reproduce the source-mask hash and source-side coarse occupancy because original images cannot enter Git history.
The source-mask hash anchors the measurement input; it is not an acceptance target for a shipped full-resolution reconstruction.
Regressions reject a sub-0.75 coarse score, inconsistent overlap arithmetic, unsupported geometry, omitted components, excess primitives, and any geometry change that alters the recorded positioned render.

## Bounds and spawns

Every preview begins with camera bounds of 18 player diameters horizontally and 10.1 vertically, expanded only when a rounded oriented box extends beyond that envelope, while the provisional kill boundary sits at `-12`.
Collision bounds enclose the rounded oriented boxes.
Two provisional spawn regions are placed above named static support boxes and maximize Euclidean separation, which supports horizontal and vertical arena layouts.
Each region's width is derived from the support's usable oriented top surface rather than from a fixed rectangle.
The checker requires at least eight player diameters between centers, validates all four region corners in each box's local coordinates, keeps regions inside the camera, and applies a one-diameter visible-saw clearance.
Exact positions, facing, and randomized alternatives remain unmeasured.

## Behavior vocabulary

`oriented-box` is the only visible geometry primitive, with `static` and `hazard-visual` roles preventing a saw silhouette from silently becoming an ordinary collider.
`radial-saw` records a visible lethal region while leaving contact response, rotation, translation, and timing unknown.
`breakable-field`, `moving-assembly`, and `physics-assembly` are visual candidates rather than confirmed runtime behavior.
The official patch history confirms that vanilla Rounds contains moving platforms and a wrecking ball, but neither that note nor a still preview binds those behaviors to a catalog row.
Controlled current-build footage must confirm candidate rows and measure their behavior before implementation enables them.

## Representative implementation order

1. Implement `arena-006` to bind static oriented collision, disconnected islands, camera framing, supported spawns, and ring-out bounds.
2. Add asymmetric `arena-024` without introducing a behavior module.
3. Add `arena-015` with visible radial-saw contact while leaving unmeasured motion disabled.
4. Observe `arena-016`, `arena-026`, and `arena-030` in the current build before deciding whether their visual assemblies are breakable, moving, or physics-driven.
5. Implement each confirmed behavior once as a reusable module, then expand across the remaining catalog by evidence status rather than appearance alone.

## Unresolved work

Still previews cannot establish exact colliders, friction, restitution, layer ownership, spawn facing, camera margins, or decorative edges.
They also cannot establish break thresholds, movement paths, saw timing, rigid-body constraints, masses, damping, or reset sequencing.
Implementation must preserve those unknowns and retune scale from controlled current-build captures instead of treating the structural oracle as gameplay proof.
