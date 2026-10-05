---
format: 3
status: ready
owner: codex:01a10d8d-8dee-7fb1-8831-37de3ce8c719
created: 2026-10-05T14:13:11Z
origin: human-request
tags: ["quarrel", "mvp", "arenas"]
value: 8
sessions:
  - claude:bcbe88ae-0a32-432f-8fd1-3a061e17847f
  - codex:01a10d8d-8dee-7fb1-8831-37de3ce8c719
execution: unattended
parent: 75
depends-on: [76]
supersedes: []
split-from: []
---

# Define arenas as data covering every ROUNDS object kind

Arenas are currently hard-coded scenes tied to footage profiles; QUARREL needs arenas as text files that agents write and check without a visual editor.
The format must cover every kind of object ROUNDS maps use (`docs/rounds-maps.md`, `docs/rounds-reference.md`), because a new random arena every fight is a large part of what makes ROUNDS fun.

## Outcome

- Arenas are RON files under `assets/arenas/`, loaded at start and reloaded while the game runs when a file changes.
- The format covers: solid ground as rectangles, circles and convex polygons; loose boxes and balls with mass that shots and fighters push; breakable pieces with health; background pieces fighters pass in front of; chains joining a fixed anchor to a piece or two pieces; fixed saws and loose saws; moving pieces that follow a path or rotate; and spawn points for up to four fighters.
- Each kind is drawn so its behaviour is clear from its look alone (solid, loose, breakable, background, chain, saw), using plain shapes and colours.
- Falling and thrown pieces damage fighters they hit hard enough.
- The existing teal, timber, ice, lime, saw and crate scenes are converted into arena files and the old scene code is removed.
- One headless command renders a PNG preview of any arena file.
- Converted arena files reproduce the old scene geometry and the existing profiles load them, so existing tests pass unchanged until #78 deletes the profiles.
- The format states the camera frame bounds, and the loader rejects geometry or spawns outside them; #79 uses these bounds as the screen edges.

## Decisions

- Chains never break; pieces come loose only by breaking the piece itself or a piece it hangs from (Adam, 2026-10-04).
- No drop-through platforms (Adam, 2026-10-04).
- Most terrain stays whole; nothing is chipped pixel by pixel.
- Arena content is our own; no ROUNDS map layout is copied.
- Every arena fits one fixed camera frame, as in ROUNDS.

## Evidence required

- A test loads every file in `assets/arenas/` without error.
- A test arena containing every object kind simulates 10 seconds headless with no panic, every chained piece still attached to its anchor, and a moving piece back on its path.
- The preview command writes a PNG for each converted arena; previews stay out of Git.
- A test changes an arena file while a session runs and observes the reloaded geometry.
- Tests show a hard-falling piece damages a fighter, a breakable piece breaks at zero health and releases what hangs from it, a saw damages a fighter, and fighters and shots pass through background pieces.

## Chat excerpts

Adam — this session, 2026-10-04:

> i prefer chains being unbreakable. i dont like dropthrough. we're vibecoding so any map editing system is catered to you. i like moving pieces. i like map material behavior being clear from texture. lets start with everything rounds has, and note what we want to add later

## Work log

- 2026-10-05T14:13:11Z Drafted under run #75.
- 2026-10-05T15:02:48Z Admitted for run #75 after an independent contract check; its fixes were applied first.
- 2026-10-05T19:35:27Z stage implement start session codex:01a10d8d-8dee-7fb1-8831-37de3ce8c719 — Preserve legacy geometry and solver order; add validated RON loading and ordinary arena physics.
- 2026-10-05T19:54:42Z stage implement end session codex:01a10d8d-8dee-7fb1-8831-37de3ce8c719 — Converted all eight legacy arena layouts and body definitions; implemented ordinary arena loading, hazards, chains, reloads, and previews.
- 2026-10-05T19:54:58Z stage verify start session codex:01a10d8d-8dee-7fb1-8831-37de3ce8c719 — Run strict workspace Clippy, locked build/tests, headless previews, and unchanged replay checks using the shared two-job target.
- 2026-10-05T20:36:54Z stage verify end session codex:01a10d8d-8dee-7fb1-8831-37de3ce8c719 — Format, strict workspace Clippy, locked build and all 97 tests passed with no failures or skips; nine headless arena PNGs inspected; CI two-client smoke and legacy capture passed. Evidence retained under out/ticket-077-proof and ticket-077-verified-tests.log.
- 2026-10-05T20:37:08Z stage review start session codex:01a10d8d-8dee-7fb1-8831-37de3ce8c719 — Prepare complete code-only candidate and dispatch fresh read-only Claude Opus review of exact commits, contract, tests, and rendered PNGs.
- 2026-10-05T20:50:44Z stage correction start session codex:01a10d8d-8dee-7fb1-8831-37de3ce8c719 — Fresh Claude review rejected four defects: distorted/stretching camera, replay tick reset on reload, and invalid future-stage panic. Full findings in ticket-077-review.json; correct all four and re-review complete range.
- 2026-10-05T21:02:02Z stage review end session claude:4c0a2c42-0678-4af8-add7-9fec87ad8c49 — Rejected candidate adf1745fb046098e6e261752f3ab9a66e928118f..0bff4ae1ba8976a22413356971c89dff044b8486; four blocking findings preserved in out/ticket-077-review.json. Reviewer read all nine previews and legacy capture; no reviewer artifacts or processes created.
- 2026-10-05T21:06:52Z stage correction end session codex:01a10d8d-8dee-7fb1-8831-37de3ce8c719 — Corrected all four review blockers; snapshots and physics share validated stage data, reload keeps tick/flow/scores/loadouts, and camera preserves aspect with zero lens effects. Format, strict workspace Clippy, locked build, all 101 tests, nine regenerated inspected PNGs, two-client smoke and legacy capture passed; evidence in ticket-077-delivery-tests.log and ticket-077-proof-corrected.
- 2026-10-05T21:07:07Z stage review start session codex:01a10d8d-8dee-7fb1-8831-37de3ce8c719 — Re-review the complete revised candidate and earlier findings through the independent read-only Claude context; no changed bytes omitted.
- 2026-10-05T21:16:45Z stage correction start session codex:01a10d8d-8dee-7fb1-8831-37de3ce8c719 — Re-review resolved B1-B4 but found replay edits bypassing profile HUD/draft/joints and missing cache entries on match-end lifecycle routes. Preserve replay adapters on reload and cover both complete stage routes; full findings retained in ticket-077-review2.json.
- 2026-10-05T21:19:34Z stage review end session claude:4c0a2c42-0678-4af8-add7-9fec87ad8c49 — Rejected 1affd8c4ca462cfd7b21c9f8f9da33b2de69477e..5be3f4c7bbae1914c35d99341ed539130695e47b; B1-B4 resolved, N1 replay-adapter bypass and N2 match-end stage cache block. Findings in ticket-077-review2.json; reviewer read corrected PNGs and created no artifacts.
- 2026-10-05T21:41:21Z stage correction end session codex:01a10d8d-8dee-7fb1-8831-37de3ce8c719 — Preserved replay adapters and active-file handoffs; all104 workspace tests passed, nine previews and network smoke passed. Evidence: out/ticket-077-final-isolated-tests.log and out/ticket-077-proof-final/.
- 2026-10-05T21:41:36Z stage review start session codex:01a10d8d-8dee-7fb1-8831-37de3ce8c719 — Independent readonly Claude reviewer rechecks the complete final candidate, including replay adapter and all-flow cache corrections.
- 2026-10-05T21:47:15Z stage review end session claude:4c0a2c42-0678-4af8-add7-9fec87ad8c49 — Rejected 889174e: earlier six findings resolved; transition reload cancels ice entry after an earlier edit. Full findings: out/ticket-077-review3.json.
- 2026-10-05T21:47:23Z stage correction start session codex:01a10d8d-8dee-7fb1-8831-37de3ce8c719 — Reproduce and remove redundant stage-transition rebuild while preserving initial prior-match/draft edits.
- 2026-10-05T22:03:26Z stage correction end session codex:01a10d8d-8dee-7fb1-8831-37de3ce8c719 — Ice entry reproduced red and fixed; all105 tests pass with test-threads=1, strict Clippy/build/fmt, nine fresh previews and smoke. Existing parallel UDP fixture failure retained in idea ticket85. Evidence: out/ticket-077-entry-serial-tests.log and out/ticket-077-proof-entry/.
- 2026-10-05T22:03:38Z stage review start session codex:01a10d8d-8dee-7fb1-8831-37de3ce8c719 — Independent Claude reviewer checks the whole corrected range and disclosed parallel-test limitation.
- 2026-10-05T22:07:31Z stage review end session claude:4c0a2c42-0678-4af8-add7-9fec87ad8c49 — Rejected304aa08: ice-entry fix verified; pre-active draft edit changes drawing without rebuilding collisions. Serial-test evidence accepted; full findings out/ticket-077-review4.json.
- 2026-10-05T22:07:44Z stage correction start session codex:01a10d8d-8dee-7fb1-8831-37de3ce8c719 — Reproduce pre-active draft edit and rebuild its physics only when changed data or earlier edits require it.
