# Roadmap

Each milestone ends with something the group can play, followed by a play session whose notes go in `docs/playtests/`.
Those notes decide what comes next; this list is a plan, not a contract.

## Where things stand (2026-10-04)

The Rust/Bevy/Rapier foundation is solid: a 60 Hz authority, Rapier physics behind a project boundary, a shared 2D renderer, a UDP client/host/dedicated split, headless capture, and CI.
The game on top of it is not yet general.
Rules branch on seven `ReplayProfile` footage slices, draft offers are fixed lists transcribed from the recordings, three of 21 catalogued cards work, and a live session loads whichever footage scene is the default.
Good reusable pieces already exist: arena geometry for teal, timber, ice, lime, saw and crate scenes; jump, recoil, block, knockback, ring-out and explosion rules; halves, rounds, the loser draft, rematch and match end.

## M1 — A real match on one machine or LAN

The goal is an ordinary match that anyone can start and finish, with no script behind it, and a base fight that is fun before any card.

- Replace `ReplayProfile` with general data: an arena definition (surfaces, spawns, dynamic bodies, hazards, palette) and a match config (fighters, target score, run-it-back limit).
- Rules, draft and scoring for any number of fighters; ship 1v1.
- Rotate the existing arena geometry as ordinary arenas.
- Play the base fight with no cards and tune movement, shot, block and knockback until it is fun on its own.
- A card system built from event rules (on fire, hit, block, bounce, land, damage, kill) and effects, loaded from data that reloads while the game runs.
- About ten original cards chosen to combine with each other.
- Everyone drafts after each fight from a shared seeded offer, loser first.
- A minimal menu: local match, host, join.
- Keep a short recorded-input replay as a regression check; it plays through the general rules, not a profile.
- Delete the footage-slice profiles, the historical-rematch setup, the fixed offer lists, the ROUNDS card catalog and their capture-anchor tests once nothing uses them.

## M2 — Online with friends

The goal is a full match between two homes that feels fair.

- Spike first: run the current host/client at simulated 80 ms round trip with 2 % loss, and compare client-side prediction with interpolation against rollback for this physics.
  The current model shows the remote authority's snapshot with no prediction, so a player's own movement lags by a full round trip.
- Replace JSON snapshots with a compact binary encoding.
- Add Steam networking (relay, friend invites, no port forwarding) beside the UDP transport.
  Development can use Valve's public test app until the game has its own App ID.
- First online play session.

## M3 — Builds that keep surprising us

The goal is the "first time we found that combo" feeling, session after session.

- Play-test the experiments in `docs/game-design.md`: hand size, draft variants, curses, match length and run-it-back.
- Evolutions for duplicate cards, hidden fusions and a shared combo book.
- Fighter-changing cards: dash, grapple, wall-cling, parry, gun replacements.
- Destructible terrain and screen distortion that scale with build power.
- Grow toward forty cards and ten arenas, adding what play sessions ask for.

## M4 — Our own identity

- Name, logo, palette, fighter and card art, original sound, screen shake, hit pause and particles.
- Controller support for every player, settings that persist.
- 2v2 and free-for-all.

## M5 — Steam release

- A check by someone qualified that the game does not infringe Landfall's trademarks or trade dress.
- App ID (Steam Direct fee), store page, achievements if wanted, crash reporting, a release build pipeline, and an outside playtest.

## Open questions for Adam

- A working name, so card and UI text can use it from M1 onward.
