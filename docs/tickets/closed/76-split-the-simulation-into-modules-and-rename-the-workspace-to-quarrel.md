---
format: 3
status: closed
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
- 2026-10-05T18:39:26Z stage implement end session codex:01a10d50-dccf-7641-937d-21bf6a455456 — Six crate names/paths and current command references renamed; sim and presentation split with all 62 original tests retained, largest source 1324 lines; fmt and strict locked all-target Clippy pass.
- 2026-10-05T18:40:31Z stage verify start session codex:01a10d50-dccf-7641-937d-21bf6a455456 — Locked workspace build/test followed by renamed public smoke/capture commands and saved pre-rename state/frame comparisons; RUST_TEST_THREADS=1 for renderer tests.
- 2026-10-05T18:52:38Z stage verify end session codex:01a10d50-dccf-7641-937d-21bf6a455456 — fmt, strict locked all-target Clippy, locked workspace build and all 88 tests pass. Renamed CI smoke (180 ticks), connected two-client smoke (5941 ticks), and headless capture pass. Exact pre-change build matches all seven full inspection outputs and PNG bytes; existing cached executables were stale. Evidence: out/ticket-076. Name scan has only this ticket's pre-change summary until closure.
- 2026-10-05T18:52:48Z stage review start session codex:01a10d50-dccf-7641-937d-21bf6a455456 — Fresh read-only Claude Fable CLI review of the complete move/rename candidate after retargeting onto the current origin/main and verification.
- 2026-10-05T19:21:00Z stage review end session claude:d99e00db-544d-4b73-b800-d4508ce0e319 — approved candidate c0ca1853ddcc73c37fa106984d5a051884613b98..94bbb709a006baee059fc1142cedf92984085a18
- 2026-10-05T19:21:00Z stage integration end session codex:01a10d50-dccf-7641-937d-21bf6a455456 — integrated 94bbb709a006baee059fc1142cedf92984085a18 as 94bbb709a006baee059fc1142cedf92984085a18
