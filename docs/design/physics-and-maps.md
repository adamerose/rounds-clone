# Physics and maps

How the simulation moves things and how levels are represented.
This is design intent, not a transcript of a decision — update it when the implementation teaches us something better, and record the change in `docs/decisions.md`.

Everything here lives in `Rounds.Sim` and has no Godot dependency.

## Units

**One world unit is one player diameter.**
The player's collision circle has radius `0.5`.

This exists to make the research usable.
`spec/measurements.json` holds quantities read off gameplay footage as dimensionless ratios — jump apex in player-heights, bullet speed in player-widths per second — precisely because those survive resolution and zoom differences.
Defining the unit as the player's size means a measured value drops straight into a tuning constant with no conversion and no chance of a scale error creeping in.

Time is measured in ticks.
The tick rate is 60 Hz and is the only place a real duration appears.
A card that lasts "two seconds" is stored as `120` ticks.
Nothing in the simulation reads a clock.

So a gravity constant is in units per tick squared, a speed is units per tick, and a measured "bullet crosses the screen in 40 frames" becomes a tuning value directly.

## The one primitive everything is built on

```csharp
public static bool SweepCircle(
    Vec2 origin, double radius, Vec2 delta,   // motion this tick
    in Obb box,
    out double t,                             // time of impact in [0,1]
    out Vec2 normal);
```

A swept circle against an oriented box.
Players are circles.
Bullets are small circles.
Level geometry is oriented boxes.
Card-spawned walls are oriented boxes.
That single function, tested hard once, carries essentially all of the collision in the game.

It reduces to a ray against a rounded rectangle — the box expanded by the circle's radius, with rounded corners — which has a closed-form solution, no iteration, and no convergence loop that could behave differently across runs.

The second primitive is `SweepCircleCircle`, for bullets hitting players and for players hitting each other.
Same shape of answer: earliest time of impact and a normal.

Discrete overlap tests are not used for motion.
At Rounds' knockback velocities a player can cross a platform's thickness in a single tick, and bullets certainly can.
Everything moves by sweeping.

## Player movement

The player is a **circle with velocity, moved kinematically, that responds to impulses**.
Not a rigid body in a solver — solver-driven characters feel mushy and stick on seams — but not a momentum-free platformer controller either, because getting flung across the map by your own recoil is a large part of what Rounds is.

The circle never rotates for collision purposes.
Visual rotation, squash, and stretch belong to the rendering shell and never feed back into the simulation.

Each tick, per player, in index order:

1. **Intent.**
   Read input.
   Horizontal acceleration toward the target speed, with separate ground and air acceleration values.
   This accelerates rather than setting velocity, so momentum from knockback isn't erased by holding a direction — that distinction matters a lot to the feel.
2. **Forces.**
   Gravity times the player's gravity scale.
   Drag.
   Ground friction when grounded.
3. **Jump.**
   If jump was pressed and a jump is available, set upward velocity.
   Cards add jumps; the counter refills on landing.
   Include coyote time and input buffering, both in ticks, both tunable — they're invisible when right and awful when missing.
4. **Variable height.**
   Releasing jump while still rising cuts upward velocity by a factor.
5. **Move and slide.**
   The loop below.
6. **Ground state.**
   Grounded if the last resolution produced a contact whose normal points up past a threshold, or a short downward probe finds one.
   Grounded is sticky for a few ticks so that running over a seam doesn't cancel a jump.

Move and slide:

```
remaining = velocity * dt
for iteration in 0..3:                 # fixed count, never "until converged"
    hit = earliest sweep of remaining against broadphase candidates
    if no hit: position += remaining; break
    position += remaining * hit.t
    position += hit.normal * SkinWidth      # stay off the surface
    velocity  = slide(velocity, hit.normal) # remove the into-surface component
    remaining = slide(remaining * (1 - hit.t), hit.normal)
```

Four iterations, always.
A fixed iteration count is what keeps this deterministic and keeps a pathological corner from costing an unbounded amount of time in self-play.

Bounciness is a per-entity property.
Players have zero by default — high-speed impacts scrub velocity rather than reflecting it — but cards change that, so `slide` and `reflect` are both available and selected by the entity's restitution.

## Impulses

Knockback, recoil, and explosions are all the same operation:

```csharp
void ApplyImpulse(ref Player p, Vec2 impulse) => p.Velocity += impulse / p.Mass;
```

Gun recoil applies an impulse opposite the shot direction.
That's not a detail — recoil jumping is a real movement technique in Rounds and it falls out for free if recoil is a genuine impulse rather than a visual effect.

Explosions apply an impulse falling off with distance, to every entity within a radius, in stable id order.
Whether the falloff is linear or quadratic is a research question; whichever it is, it's one constant in `spec/`.

Mass exists so that cards can change it.
A heavier player is harder to knock around, which is a real card effect.

## Bullets

Bullets live in a flat array with a stable monotonic id, processed in id order.
A bullet carries everything a card might modify: position, velocity, radius, damage, gravity scale, drag, bounces remaining, pierce remaining, lifetime in ticks, homing strength, owner, and a small flags field.

Per tick, per bullet: apply gravity and drag, apply homing steering if any, sweep the motion against level geometry and players, and resolve the earliest hit.

- **Hit a player:** deal damage, apply knockback, decrement pierce.
  Fire `OnBulletHit` so cards can split, explode, heal the owner, or spawn more bullets.
- **Hit geometry:** reflect if bounces remain, otherwise expire.
- **Lifetime expires or leaves bounds:** expire.

**There is a hard cap on live bullets, and it exists from the first commit.**
Splitting cards multiply, and stacked splitters multiply exponentially — this is a real phenomenon in Rounds, and in an unattended loop it shows up as self-play runs that mysteriously take twenty minutes.
When the cap is reached, the oldest bullet is dropped and a counter increments.
The counter is part of the self-play health statistics, so hitting the cap is visible rather than silent.

## Blocking

Block is a small state machine, all durations in ticks: `Ready → Active(n) → Recovery(m) → Cooldown(k) → Ready`.

While Active, an incoming bullet is reflected rather than absorbed: its velocity is mirrored about the player-to-bullet direction, its owner changes to the blocker, and `OnBlock` fires so cards can convert the block into a teleport, a shield, an explosion, or whatever else.
The reflected bullet keeps its modifiers, which is where a lot of the game's best moments come from.

Every duration here is a tunable in `spec/`, and the Active window in particular should come from frame-counted footage rather than from anyone's estimate.

## Maps

**A map is data, not a Godot scene.**
The simulation needs the geometry, and the simulation cannot see Godot.
The renderer reads exactly the same file the simulation does, so there is one source of truth and no possibility of the visual level drifting from the collidable one.

```json
{
  "id": "chasm",
  "bounds":   { "w": 40.0, "h": 22.5 },
  "boxes":    [ { "x": 0, "y": -8, "w": 30, "h": 2, "rot": 0 } ],
  "spawns":   [ { "x": -12, "y": 2 }, { "x": 12, "y": 2 } ],
  "movers":   [],
  "outOfBounds": "wall"
}
```

Oriented boxes are the only level-geometry primitive.
Rounds' levels are rectangles, some of them rotated, and adding a second primitive would double the collision surface area for very little gain.
Anything curved is approximated with boxes.
The research catalog's `hazard-visual` and `dynamic-visual` boxes participate in silhouette verification but do not become ordinary static collision; their owning behavior module decides contact semantics after runtime evidence exists.

`movers` covers moving platforms, including the mirrored pair measured for `arena-026`.
A mover is a box plus a motion defined as a pure function of the tick — a period, a phase, and an offset — never a simulated body.
That keeps a moving platform deterministic and rewindable for free.
The `arena-026` samples bind its broad U-shaped path and one approximately 840-tick endpoint-to-reversal interval, while exact interpolation, dwell, full period, other mover rows, phases, and wrecking-ball constraints remain unbound.

`outOfBounds` is a per-map policy, either a solid wall or a kill volume.
Which one Rounds uses is a research question and the format supports both.

### Card-spawned geometry

Some cards create barriers and walls.
Those go in `World.DynamicBoxes`, a small list checked alongside the static geometry, each entry carrying an owner and a lifetime in ticks.
Same collision code, different lifetime.

### Broadphase

Static level geometry never moves, so its spatial grid is built once at map load and never rebuilt.
A small stable-id mover list is evaluated from the tick and queried alongside static candidates without rebuilding the grid.
A uniform grid sized to roughly two units per cell is plenty.
Queries return candidate boxes in cell order, then by box index, so iteration order is stable.

This matters less for a live match than for self-play, where a few hundred bullets against a few dozen boxes across thousands of concurrent matches turns a linear scan into the thing that decides how long the nightly run takes.

### Where maps come from

Reconstructed as original vector geometry from the topology and broad proportions visible in public preview images, and then checked automatically.

Rounds' levels are light geometry on a dark background, so thresholding a preview produces a useful measurement mask.
The generator labels every eight-connected source component, fits at least one oriented box to each component, and bounds refinement by a 0.75 fitted-area ratio.
It then accepts only arenas whose coarse 80-by-45 occupancy grid reaches 0.75 intersection over union.
Each arena is capped at 96 boxes so the evidence preserves islands, sightlines, and large structures without becoming a pixel trace of the source image.

The public workbook's embedded media filenames are shuffled, so catalog identity comes from each drawing object's worksheet-row anchor and relationship target rather than from filename or ZIP order.
The committed evidence includes source-mask, preview, and positioned-render hashes, source-component coverage, coarse-cell arithmetic, and the score, while the repository gate rerenders the rounded oriented boxes and rejects count or position drift.
Source images stay under ignored `research/raw/`, and the supported generator reproduces their hashes and structural evidence from a fresh public XLSX export.

That gives map building a bounded structural oracle rather than an agent's judgment: preserve every connected island, render the JSON, compare coarse occupancy, and stop before refinement becomes tracing.
It is the same trick as `spec/measurements.json` — comparing against the actual game rather than against a description of it — applied to level geometry.

## Determinism, specifically here

The general rules are in `docs/architecture.md`.
The ones this design leans on:

- Every position, velocity, and constant is `double`.
- Fixed iteration counts everywhere.
  No loop runs until a tolerance is met.
- Entities are processed in stable id order: players by index, bullets by monotonic id, geometry by grid cell then box index.
- Named shared epsilons in one file.
  No scattered magic `1e-6` values that drift apart.
- Collision events are collected during the sweep and applied in a deterministic order after it, so a card that spawns bullets during `OnBulletHit` can't perturb the iteration that's still running.

## Open research questions

These change constants, not structure, so they don't block implementation — but they should be answered from footage before the tuning pass:

- Do players collide with each other, or pass through?
- Is out-of-bounds a wall or a kill volume, and is there a bottom pit?
- Explosion falloff: linear or quadratic, and over what radius?
- Exact block Active window, recovery, and cooldown in frames.
- Which additional catalog rows contain moving platforms or the wrecking ball, and what are their paths, periods, constraints, and break thresholds beyond the partial `arena-026` measurement?
- Does the camera's zoom-to-fit ever constrain movement, or is it purely presentation?
  It must be purely presentation here; if the original disagrees, that's a decision to record.
