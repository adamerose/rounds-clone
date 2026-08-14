# Vanilla arena catalog

## Binding target

The catalog targets the current public Windows build `21020021`, identified in-game as `v1.1.2.a75ee335a`.
The official Steam listing advertises “70+ maps,” which is a lower bound rather than an exact active-pool count.
A public community sheet contains exactly 70 row-ordered vanilla preview images, so the project assigns stable IDs `arena-001` through `arena-070` to sheet rows 2 through 71.
The current build exposes arenas through random matches and has no clean-room catalog browser, so complete direct runtime enumeration remains unresolved instead of being inferred from extracted data.

## Clean-room boundary

The committed catalog retains project IDs, source-row references, preview hashes, coarse vector measurements, classifications, and written uncertainty.
It does not retain the source images, proposed community names, internal identifiers, decompiled data, original art, code, or presentation assets.
The original previews were downloaded only to a temporary browser asset directory for visual measurement and will be deleted after the reviewed result is integrated.

## Scale and vectorization

The sheet previews are 230 by 128 pixels.
Ticket 002 measured a player body diameter of about 18 pixels in a 640 by 360 gameplay frame, which scales to about 6.4 preview pixels at the sheet width.
This cross-source calibration is provisional because preview framing may not scale exactly like the measured gameplay camera, so global scale carries a ±20 percent tolerance.
Each preview was reduced to occupied silhouette cells on a five-pixel grid, adjacent cells were merged into original axis-aligned rectangle primitives, and coordinates were converted to player diameters around the preview center.
The five-pixel grid corresponds to about 0.8 player diameters and defines the catalog's silhouette-coordinate tolerance.
The resulting rectangles are collision hypotheses, not traced render assets or claims about hidden colliders.

## Bounds and spawns

Every entry uses an 18-diameter horizontal camera half-width and a 10-diameter vertical half-height derived from the normalized preview frame.
Collision bounds enclose the cataloged primitives, while the provisional kill boundary sits two player diameters below the camera bottom at `-12`.
Two spawn regions are selected from visible supported platform tops with at least eight diameters of horizontal separation, more than one diameter of kill-bound clearance, and one diameter of conservative saw clearance.
The current catalog's smallest spawn-center separation is 14.266 diameters after hazard-clearance correction.
Spawn regions remain low-confidence until controlled current-build observations bind exact positions, facing, and randomized alternatives.

## Geometry vocabulary

The simplest implementation contract is one list of coarse collision rectangles plus optional regional behavior modules, with no map-specific class hierarchy.
`rect` describes static or behavior-owned collision silhouettes.
`radial-saw` records a visible lethal circular region while leaving contact response, rotation, and translation unknown.
`breakable-field` marks silhouettes whose health sharing, thresholds, fragments, and reset timing require runtime observation.
`moving-assembly` marks grouped parts whose paths and cycle timing are not available from a still preview.
`physics-assembly` marks structures whose joints, mass, damping, and material response are unresolved.
This vocabulary is intentionally smaller than the visual catalog because implementation should compose repeated behaviors instead of creating 70 special-purpose map types.

## Families and representative coverage

The 70 previews classify as 21 platform fields, 12 breakable fields, 10 hazard courses, 1 visibly moving assembly, 23 physics structures, and 3 ring-out island layouts.
Classification describes the dominant visible play pattern and does not claim that a still image reveals every dynamic component.
The representative implementation set is `arena-002` for static collision, `arena-026` for a moving assembly, `arena-007` for breakables, `arena-016` for saw hazards, `arena-040` for asymmetry, and `arena-006` for ring-out focus.
The repository checker verifies that each representative actually exhibits its named category.

## Implementation order

1. Implement `arena-002` with static rectangles, camera framing, spawns, collision bounds, and the kill boundary.
2. Add `arena-006` and `arena-040` to cover disconnected ring-out islands and asymmetric sightlines without new behavior modules.
3. Add `arena-016` with radial saw contact and keep rotation or translation disabled until measured.
4. Add `arena-007` with a reusable breakable-field boundary and separately research health, fragments, and reset behavior before enabling destruction.
5. Add `arena-026` with a reusable moving-assembly boundary and separately research its path and timing before enabling motion.
6. Add `arena-040`'s physics assembly only after reusable rigid-body constraints are measured, then expand by family across the remaining catalog.

## Unresolved work

Still previews cannot establish hidden or one-way colliders, friction, restitution, layer ownership, spawn facing, exact camera margins, or decorative versus collidable edges.
They also cannot establish break thresholds, movement paths, saw timing, rigid-body constraints, masses, damping, or reset sequencing.
Implementation must preserve these unknowns and retune the provisional scale from controlled current-build captures instead of treating the coarse vectors as final measurements.
