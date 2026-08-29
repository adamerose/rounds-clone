# Rounds clone — architecture

Binding decisions for this project.
Agents read this before writing code.
If implementation proves a decision here is wrong, stop, record the change and its reasoning in `docs/decisions.md`, update this file, and continue — do not quietly diverge from it.

Prose in this repository is written **one sentence per line**.
Markdown renderers ignore single newlines, so it looks the same, and it keeps diffs to the sentence that actually changed instead of reflowing whole paragraphs.

## What we're building

A faithful reimplementation of ROUNDS (Landfall, 2020): a 1v1 2D platform shooter played in short duels and comeback-driven rounds.
Both players open with one card; each full-round loser picks another persistent card; stat combinations build until one player reaches five points.

### Faithful subsets, never substitutes

Partial delivery may omit ROUNDS content, but implemented titles, names, mechanics, tuning, map geometry and behavior, controls, and presentation must target observed ROUNDS behavior.
Unverified content is absent or visibly marked as development scaffolding; it is never replaced with an original product name, ability, map, rule, or art direction.
Clean-room work excludes copied source code and extracted proprietary logo, art, audio, and other asset bytes, not observable behavior, sourced short names, or the `ROUNDS` title.
Determinism is necessary regression evidence, not fidelity evidence by itself: every feel-sensitive or user-visible slice needs a direct comparison signal from the installed public target or equally direct captured evidence.

Tickets 016–025 own the disclosed gaps: base feel, projectile presentation, the complete 70-arena catalog, current-card verification, presentation, controller/menu input, match replay and internal self-play, settings/persistence/shipping, nightly reel evidence, and the remaining 51 cards.
Until ticket 019 verifies card composition, the shipped Godot shell stops before the first loser can select a second card; the pure deterministic `Match` remains available to internal tests without claiming that its provisional combinations are faithful.
Mechanics are reimplemented from research.
Exact sourced gameplay identifiers and short names are allowed where fidelity and unambiguous validation require them.
Original source code, logo, card art or other extracted art, audio, and longer expressive or flavor text remain excluded.

The project is built by autonomous agents with no human reviewing the code.
That single constraint shapes nearly everything below.

## The idea everything else follows from

Nobody is checking this work by hand, so quality is held up entirely by what a machine can verify.
An unattended loop drifts toward whatever it can measure, so the measurements have to be the real thing rather than a proxy for it.
Three signals carry the project:

1. **Replay determinism.**
   The same seed and the same input stream must produce a byte-identical state hash.
   That is one assertion, it cannot be softened into meaninglessness, and it catches almost every behavioral regression in the simulation.

2. **Self-play statistics.**
   Thousands of headless bot matches per build.
   Crash rate, round length distribution, per-card win rate, bullet count ceilings.
   This catches balance problems, infinite-stalemate card combinations, and performance cliffs that no unit test would notice.

3. **Footage measurements.**
   Dimensionless quantities measured from real Rounds gameplay video — jump apex in player-heights, bullet speed in player-widths per second, block window in frames — compared against the same measurement taken from our own simulation.
   This is the only fidelity check that doesn't route through an agent's own description of the game.

Every structural decision below exists to keep those three cheap and reliable.

## Locked decisions

| Decision | Choice | Why | Reversible? |
|---|---|---|---|
| Engine | Godot 4 | Text `.tscn` project files that merge, headless CLI, real 2D tooling | Cheap, given the boundary below |
| Language | C# (.NET 8) | Static types act as a reviewer that never tires; ~10–50× faster than GDScript, which matters at self-play volume | No |
| Where the rules live | A pure C# library, `Rounds.Sim`, with zero Godot references | Headless testing, fast self-play, determinism we control | No |
| Physics | Written by us: kinematic character controller, swept-circle bullets, and static or behavior-owned oriented-box level geometry | Solver-driven player movement feels mushy; engine physics is neither deterministic nor version-stable, while one box vocabulary covers visible slopes and movers | No — all tuning is downstream of it |
| Numbers | `double`, our own `Vec2` | Godot's `Vector2` is `float` and would weld the core to the engine | No |
| Tick | Fixed 60 Hz, decoupled from render rate; no wall-clock in the sim | Every duration constant is expressed in ticks | No |
| Players | `List<Player>` with a `TeamId` from the first commit | Vanilla Rounds is 1v1, but explicit team ownership keeps targeting and friendly-fire rules from assuming that the opponent is always the other list index | No |
| Networking | None for now | Deterministic input-driven sim keeps rollback available later at no cost today | Deliberately deferred |
| Research | Agent-produced, with per-fact provenance; never reviewed by a human | Fidelity ceiling is the research artifact's fidelity, so provenance and footage measurement carry the weight | No |
| Raw research media | Gitignored, never committed | Committed screenshots or video frames are in git history permanently | Literally irreversible |

## The module boundary

The most important structural rule in the project:

**`Rounds.Sim` does not know Godot exists.**

It's a plain .NET class library.
No `using Godot;`, no `Node` inheritance, no `Vector2`, no `_Process`, no scene tree.
It exposes roughly:

```csharp
public sealed class World { /* players, bullets, level, rng, tick, ... */ }

public sealed class Match { /* scores, drafts, cards, arena cadence, one World */ }

public static class Sim
{
    public static void Step(World world, ReadOnlySpan<Input> inputs);
    public static ulong Hash(World world);
}
```

The Godot project is a shell: read gamepads, call `Step` once per fixed tick, draw `World`, play sounds, run menus.
It never contains a game rule.
A card's behavior never lives in a scene.

This is what makes headless self-play a `for` loop instead of an engineering project, and it's what makes Godot replaceable if it disappoints.

`World` owns exactly one deterministic duel and remains the version-1 replay boundary.
`Match` owns the longer-lived score, half points, draft offers and input latches, acquired card IDs, arena rotation, and the same `World` across every duel.
Opening and loser draft phases pause ordinary world ticks; a confirmed pick recomputes immutable per-player combat profiles and enters the one existing world reset path.
The Godot live path steps `Match`, while replay mode continues to step `World`, so match growth cannot silently invalidate the protected base-combat corpus.

## Determinism rules

Each of these is cheap now and a rewrite later.
Each is a mechanical check in `tools/checks/`.

1. **No Godot in the sim.**
   `src/Rounds.Sim/` contains no `using Godot;` and its `.csproj` references no Godot assembly.
2. **`double` only.**
   No `float` declarations in the sim.
3. **Our own trig.**
   `Math.Sqrt` is correctly rounded and allowed.
   `Sin`, `Cos`, `Tan`, `Atan`, `Atan2`, `Pow`, `Exp`, and `Log` are not pinned across .NET versions — they may only be called from `src/Rounds.Sim/Math/Trig.cs`.
4. **Our own RNG.**
   No `System.Random` anywhere in the sim.
   A seeded PCG lives in `World` so replays carry their randomness with them.
5. **No unordered iteration.**
   No `Dictionary<>` or `HashSet<>` inside the sim; their enumeration order is unspecified and would silently break replays.
   Use lists and arrays, and sort by a stable id anywhere order could matter.
6. **No wall clock.**
   No `DateTime`, `Stopwatch`, or `Environment.TickCount` in the sim.
7. **No concurrency.**
   No `async`, `Task`, or threads inside `Step`.
   Parallelism belongs to the harness, one match per thread.

## What CI enforces, and what no agent may relax

These run on every commit.
An agent that cannot make one pass must record why in `docs/decisions.md` and fix the cause, never the check.

1. The seven determinism rules above.
2. **Golden replays reproduce.**
   Every replay in `replays/` re-simulates to its recorded state hash.
   A changed hash fails the build unless the same commit adds an entry to `replays/intentional-breaks.md` naming the behavior that changed and why.
3. **`spec/` is read-only to implementation work.**
   Changing a researched value requires re-deriving it from a source, not reasoning about it.
   Commits that touch both `spec/` and `src/` fail.
   Ticket 015's metadata-only correction is the sole recorded exception: it changes only the human-readable `title` token in the five existing `spec/schema/*.json` files from the superseded product title to `ROUNDS`, while every `$id`, validation rule, researched value, and other spec byte remains stable.
4. **Self-play is green.**
   N headless matches complete with no crash, no assertion failure, and every match terminates inside the round cap.
5. **Dependency versions are pinned exactly.**
   A version bump is its own commit, reviewed as a retune of the whole game, because that is what it is.
6. **The nightly reel renders.**
   If replay-to-video is broken, the build is red.
   This is the only window a human has into the project.
7. **Tests may not be deleted or weakened to go green.**
   Removing or loosening an assertion requires a `docs/decisions.md` entry explaining what was wrong with it.

## Research artifact

`spec/` is the project's fidelity target and its root of trust.
It is produced once, up front, and read-only thereafter.

Every value carries where it came from:

```json
{
  "id": "bouncy",
  "displayName": "Bouncy",
  "rarity": "common",
  "effects": [{ "stat": "bullet.bounces", "op": "add", "value": 2 }],
  "stacking": "additive",
  "sources": [
    { "kind": "wiki", "url": "...", "confidence": "high" },
    { "kind": "video", "id": "...", "t": "4:12", "confidence": "medium" }
  ],
  "confidence": "high",
  "conflicts": []
}
```

Where sources disagree, the disagreement is recorded in `conflicts` rather than silently resolved by whichever agent wrote last.
Low-confidence values are the first place to look when self-play statistics come out strange.

`spec/measurements.json` holds the dimensionless quantities extracted from footage.
The harness produces the same measurements from the simulation, and the fidelity check compares them within a stated tolerance.

## Design documents

- `docs/design/physics-and-maps.md` — movement, collision, bullets, blocking, level format, and the unit system.
  Read it before touching anything in `Rounds.Sim/Physics/`.

## Repo layout

```
src/
  Rounds.Sim/           class library — zero Godot references
    World.cs, Sim.cs
    Math/               Vec2, Trig, Rng
    Physics/            kinematic controller, swept circles, level geometry
    Cards/              embedded catalogs and deterministic stat/hook folds
  Rounds.Harness/       console app: self-play, replay, stats, measurement, render
  Rounds.Sim.Tests/     unit and property tests
game/                   the Godot project — rendering, input, audio, menus, juice
spec/                   research output; read-only to implementation
  cards/, maps/, tuning.json, measurements.json
replays/                golden corpus + expected hashes + intentional-breaks.md
research/
  raw/                  GITIGNORED — screenshots, video frames, downloads
  notes/                committed prose with provenance
docs/                   architecture.md, decisions.md, design-docs/postmortems.md, design/
tools/checks/           the mechanical checks CI runs
```

## Cards

Cards are embedded catalog data first, not imperative objects with arbitrary world access.
The supported 16-card pool passes through one ordered fold into an immutable per-player combat profile containing health, ammunition, damage, cadence, reload, projectile speed, geometry bounces, block cooldown, and lifesteal.
The four ricochet additions use additive non-negative bounce counts, sign-aware attack speed, flat-then-speed-then-Quick-Reload composition, additive positive damage, and one multiplicative factor per negative damage effect while retaining the existing ammunition and projectile-speed bounds.
Combat reads that profile at the owning player boundary; a spawned bullet copies its shooter's geometry-bounce budget, and a duel reset restores profile maxima while acquired IDs and the profile persist in `Match`.
Geometry reflection consumes only an available geometry bounce, while block reflection retains the budget and transfers ownership through the existing bounded contact path.

The catalog records sourced single-copy values but leaves most duplicate formulas unresolved.
The internal match scaffold therefore names one provisional composition rule per supported target, tests acquisition-order independence and every one-copy result, and keeps acquired IDs in the hash even if a combination folds to vanilla values.
Those tests prove deterministic composition, not ROUNDS fidelity, so the shipped shell does not expose a second-card choice while ticket 019 verifies the rules directly.
All-vanilla profiles append nothing to `Sim.Hash`, preserving the version-1 replay hash exactly; any custom profile appends a fixed marker and every player's full profile.

Future hook-driven behavior cards will add a small typed surface only when the first owning card needs it; the recovered ricochet cards extend the shared profile and collision path instead.
Hooks run by declared priority and then stable card ID, never acquisition or collection insertion order.
A new behavior must extend that shared surface and its deterministic event order rather than reaching ad hoc into `World`.

## What the orchestrator may change freely

Workflow is not fixed here.
The orchestrator decides task ordering, how many agents run in parallel, how work is split, when to research versus build, and how tickets are shaped, and it is expected to change all of that as the project develops.

What it may not change without amending this document: the module boundary, the seven determinism rules, the CI invariants, and the read-only status of `spec/` and `replays/`.

## Open items

- **Web export.**
  Godot's C# support for browser builds has been unreliable.
  If a playable link ever matters, verify it before the codebase is large.
  Otherwise the game is desktop-only.
