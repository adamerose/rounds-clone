# Roadmap

Each milestone ends with something the group can play, followed by a play session whose notes go in `docs/playtests/`.
Those notes decide what comes next; this list is a plan, not a contract.

## Where things stand (2026-10-04)

The Rust/Bevy/Rapier foundation is solid: a 60 Hz authority, Rapier physics behind a project boundary, a shared 2D renderer, a UDP client/host/dedicated split, headless capture, and CI.
The game on top of it is not yet general.
Rules branch on seven `ReplayProfile` footage slices, draft offers are fixed lists transcribed from the recordings, three of 21 catalogued cards work, and a live session loads whichever footage scene is the default.
Good reusable pieces already exist: arena geometry for teal, timber, ice, lime, saw and crate scenes; jump, recoil, block, knockback, ring-out and explosion rules; halves, rounds, the loser draft, rematch and match end.

## M1 — A real match on one machine or LAN

The goal is a normal match that anyone can start and finish, with no script behind it.

- Replace `ReplayProfile` with general data: an arena definition (surfaces, spawns, dynamic bodies, hazards, palette) and a match config (players, rounds to win).
- Rotate the existing arena geometry as ordinary arenas.
- The round loser drafts from a seeded random offer drawn from every implemented card.
- Turn cards into data plus typed effects, so most ROUNDS-style cards are stat modifiers and only behaviours like bounce, explode or homing need code.
- Grow the implemented pool to about fifteen core cards.
- A minimal menu: local match, host, join.
- Keep a short recorded-input replay as a regression check; it plays through the general rules, not a profile.
- Delete the footage-slice profiles, the historical-rematch setup, the fixed offer lists and their capture-anchor tests once nothing uses them.

## M2 — Online with friends

The goal is a full match between two homes that feels fair.

- Spike first: run the current host/client at simulated 80 ms round trip with 2 % loss, and compare client-side prediction with interpolation against rollback for this physics.
  The current model shows the remote authority's snapshot with no prediction, so a player's own movement lags by a full round trip.
- Replace JSON snapshots with a compact binary encoding.
- Add Steam networking (relay, friend invites, no port forwarding) beside the UDP transport.
  Development can use Valve's public test app; a real App ID costs the Steam Direct fee and is Adam's decision.
- First online play session.

## M3 — Feel and content

The goal is that it feels like ROUNDS, with enough variety to last an evening.

- Tune movement, shooting, block and knockback side by side with ROUNDS and against the measurements in `docs/fidelity/`.
- About forty cards and ten arenas.
- Original sound effects, screen shake, hit pause and particles.
- Controller support for every player, and settings that persist.

## M4 — Make it ours

The goal is that improvements the group wants become the reason to play this instead of ROUNDS.

- Ideas come from playtest notes: new cards, arenas, balance changes, maybe more than two players or new modes.
- A content format friends can edit to design cards and arenas without touching Rust.

## M5 — Steam release (only if Adam decides to)

- An original name, logo, card names and art, and a visual identity distinct from ROUNDS.
- A check by someone qualified that the game does not infringe Landfall's trademarks or trade dress.
- Store page, App ID, achievements if wanted, crash reporting, a build pipeline, and an outside playtest.

## Open questions for Adam

- How many friends play at once? Two-player duels shape the code differently from three or four players.
- Is a Steam release likely enough to choose the original identity now rather than at M5?
