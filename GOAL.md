# Goal

Build a faithful, playable clone of Rounds (Landfall, 2020) in this repository, and keep working until it is finished.
Nobody will answer questions, unblock you, or review your work.
Decide, record the decision, and keep going.

## Read first, and treat as binding

- `docs/architecture.md` — the decisions that don't bend, the module boundary, the determinism rules, and what CI enforces.
  Read it before every significant piece of work.
- `docs/design/physics-and-maps.md` — how movement, collision, bullets, blocking, and level geometry are meant to work, and the unit system everything is expressed in.
- `docs/decisions.md` — append-only log.
  Every judgment call you make under ambiguity goes here.
- `docs/design-docs/postmortems.md` — every failure, stall, broken tool, or surprising friction, recorded the same session it happens, even when it didn't block you.

If implementation proves something in `docs/architecture.md` is wrong, that is allowed and expected.
Stop, write the change and its reasoning into `docs/decisions.md`, update `docs/architecture.md`, then continue.
What is not allowed is quietly building something the document says you aren't building.

## How to work

Work in small commits that each leave the build green.
A commit either advances a specific item in `spec/`, fixes a failing check, or is explicitly recorded as cleanup.
Never leave the repository red at the end of a session — fix it or revert it.

Reorganize the workflow however serves the work.
Split tasks, run agents in parallel, change how tickets are shaped, reorder priorities.
The only things fixed are the module boundary, the seven determinism rules, and the CI invariants in `docs/architecture.md`.

Do not refactor for its own sake.
Do not invent features.
Do not add scope that isn't in `spec/`.

## The order things depend on each other

This is dependency, not ceremony.
Do the earliest thing that isn't done.

1. **Bootstrap.**
   `git init`.
   Solution and project skeleton per the repo layout.
   `Rounds.Sim` compiles with no Godot reference.
   The mechanical checks in `tools/checks/` exist and run.
   CI runs them on every commit.

2. **Research.**
   Produce `spec/` from the wiki, gameplay footage, screenshots, patch notes, and anything else you can reach: every card with its numbers and stacking rule, base player stats, gun behavior, the block window, map generation, round and match structure, scoring, UI flow.
   Every fact carries its source and a confidence rating; contradictions between sources are recorded rather than silently resolved.
   Also produce `spec/measurements.json` — dimensionless quantities read off footage frame by frame, such as jump apex in player-heights, time to apex in frames, bullet speed in player-widths per second, recoil displacement, block window length, screen height in player-heights.
   Raw media goes in gitignored `research/raw/`; only derived values are committed.
   This is the fidelity target for everything after it, so do it thoroughly and do not rush past it.

3. **Simulation core and the regression net.**
   Fixed 60 Hz `Step`, kinematic character controller, swept-circle bullets, level geometry, damage, round lifecycle, per `docs/design/physics-and-maps.md`.
   Then the harness: record a match as seed plus input stream, replay it, compare state hashes.
   Then bots and headless self-play at volume.
   Then replay-to-video, because it is the only visibility a person has into this project and it will never get built later if it isn't built now.

4. **Cards.**
   One card per file against the hook surface.
   Each card's acceptance is a forced self-play run showing it works and doesn't move the health statistics out of band.
   This is the long stretch and it parallelizes well.

5. **Feel.**
   Rounds' look is procedural, not animated: screen shake, hit-stop, particle bursts, bullet trails, squash and stretch, chunky simple shapes.
   Tune against footage, not intuition.

6. **The game around the match.**
   Menus, character select, the card draft screen, local multiplayer, bot opponents, settings, persistence.

## Rules that don't bend

- Never weaken, skip, or delete a test to make a build pass.
  If a test is genuinely wrong, fix it and write why in `docs/decisions.md`.
- Never edit `spec/` while implementing.
  Changing a researched value means re-deriving it from a source, in its own commit.
- Never change a golden replay hash without an entry in `replays/intentional-breaks.md` naming the behavior that changed and why.
- Never bump a dependency version as a side effect.
  It retunes the game; it gets its own commit.
- Never put a game rule in the Godot project, and never let `using Godot;` into `Rounds.Sim`.

## When you're stuck, and nobody is coming

- **Ambiguous requirement:** pick the reading most faithful to the research, record it in `docs/decisions.md`, continue.
  Do not stop to ask.
- **Sources disagree:** implement the higher-confidence source, record the conflict, move on.
  If self-play statistics later look wrong, revisit it first.
- **Same failure three times:** stop repeating it.
  Write what you tried in `docs/design-docs/postmortems.md`, take a different approach, and if the second approach also fails, park the item, record it, and work on something else.
- **A check is in your way:** the check is right and the code is wrong.
  That's the default assumption, and overriding it requires a `docs/decisions.md` entry justifying the change.
- **Backlog looks empty:** it isn't.
  Diff `spec/` against what's implemented and generate the remaining work from the gap.

## Done

The clone is finished when all of these hold at once:

- Every card in `spec/` is implemented with the stacking behavior the spec states.
- 10,000 headless self-play matches run with no crash, no assertion failure, and every match terminating inside the round cap.
- The sim's measurements match `spec/measurements.json` within the stated tolerance.
- A person can launch the game, play a full match against another local player and against bots, draft cards between rounds, and reach a win screen, using only the menus.
- The nightly reel renders and shows a real match.

When that holds, stop adding scope.
Spend a week hardening: more self-play volume, more golden replays, performance, and fixing whatever the statistics surface.
Write ideas for expansion to `docs/expansion.md` as you have them, and do not start any of them until the clone has been green for seven consecutive nightly runs.

## Every day

Leave behind a rendered replay reel and a one-paragraph summary of what changed, in `docs/progress/YYYY-MM-DD.md`.
Assume the only thing anyone looks at is the video.
