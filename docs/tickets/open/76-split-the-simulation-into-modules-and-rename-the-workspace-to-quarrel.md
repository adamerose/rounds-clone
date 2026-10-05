---
format: 3
status: ready
owner: codex:01a10d50-dccf-7641-937d-21bf6a455456
created: 2026-10-05T14:13:11Z
origin: agent-proposed
tags: ["quarrel", "mvp", "refactor"]
value: 6
sessions:
  - claude:bcbe88ae-0a32-432f-8fd1-3a061e17847f
  - codex:01a10d50-dccf-7641-937d-21bf6a455456
execution: unattended
parent: 75
depends-on: []
supersedes: []
split-from: []
---

# Split the simulation into modules and rename the workspace to quarrel

The simulation lives almost entirely in one 6,600-line `crates/rounds-sim/src/lib.rs`, and the presentation in one 5,700-line file, so every M1 ticket would edit the same file and none could run in parallel.
The crates are also still named after ROUNDS, which the game is no longer cloning.

## Outcome

- The workspace crates and binaries are named `quarrel-sim`, `quarrel-presentation`, `quarrel-network`, `quarrel-client`, `quarrel-server` and `quarrel-automation`, and every command in `README.md`, CI, scripts and docs uses the new names.
- `quarrel-sim` and `quarrel-presentation` are split into modules by concern (for example fighter movement, shooting and projectiles, blocking, arena and physics, match flow, cards, network-facing state), with no source file over about 1,500 lines.
- Game behaviour is unchanged: this is a move and rename only.

## Decisions

- No behaviour change; anything that looks wrong is noted in the work log for a later ticket, not fixed here.
- `ReplayProfile` and the footage code move with everything else; #78 deletes them.

## Evidence required

- `cargo fmt --all -- --check`, strict all-target Clippy, and a locked build and test of the workspace pass through the repository's `.cargo/config.toml` target.
- The existing headless capture and two-client smoke commands still pass under the new names.
- `git grep -nE "rounds[-_](sim|presentation|network|client|server|automation)"` finds nothing outside `docs/decisions.md`, `docs/tickets/closed/` and `docs/recovery/`.

## Chat excerpts

Adam — this session, 2026-10-05:

> Ok let's finish grilling and rethinking and get ready for you to orchestrate work on the MVP

## Work log

- 2026-10-05T14:13:11Z Drafted as the first M1 ticket under run #75.
- 2026-10-05T15:02:32Z Admitted for run #75 after an independent contract check; its fixes were applied first.
- 2026-10-05T18:29:09Z stage implement start session codex:01a10d50-dccf-7641-937d-21bf6a455456 — Mechanical crate rename and module split; preserve public API and behavior; reuse root out/cargo-target through a worktree junction with jobs=2.
