---
format: 3
status: ready
created: 2026-10-05T14:13:11Z
origin: human-request
tags: ["quarrel", "mvp", "arenas"]
value: 8
sessions:
  - claude:bcbe88ae-0a32-432f-8fd1-3a061e17847f
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
