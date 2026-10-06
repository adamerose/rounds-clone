# Roadmap

Each milestone ends with something the group can play, followed by a play session whose notes go in `docs/playtests/`.
Those notes decide what comes next; this list is a plan, not a contract.

## Where things stand (2026-10-05)

The Rust/Bevy/Rapier foundation runs ordinary matches: a 60 Hz authority, general fighter vectors, opening and loser drafts, one point per fight, seeded tie resolution and unanimous end choices.
Data arenas rotate without repeats and retain their object physics and live editing. Five data-driven placeholder stat cards support the general offer and loadout path.
The shared renderer, local 1v1 controls, UDP client/host/dedicated split, headless captures and CI remain in use. A short input recording pins its starting content and checks replay determinism.
Footage profiles, fixed source offers and the ROUNDS card catalog have been removed. Base-fight tuning, event-rule cards, original arenas and production online play remain ahead.

## M1 — The MVP: a real match, online with friends

The goal is an ordinary match that anyone can start and finish, locally or between two homes, with no script behind it, and a base fight that is fun before any card.
Online play was a separate milestone until 2026-10-05; Adam moved it into the MVP because the group plays from their own homes.

- Replace `ReplayProfile` with general data: an arena definition (surfaces, spawns, dynamic bodies, hazards) and a match config (fighters, target score, run-it-back limit).
- Rules, draft and scoring for any number of fighters; ship 1v1.
- An arena format covering every ROUNDS object kind in `docs/rounds-maps.md` (solid, loose, breakable, background, chained, saws, moving), and a first set of our own arenas that use each kind; the existing scene geometry can seed them.
- Play the base fight with no cards and tune movement, shot, block and knockback until it is fun on its own.
- A card system built from event rules (on fire, hit, block, bounce, land, damage, kill) and effects, loaded from data that reloads while the game runs.
- The first twelve cards from `docs/card-ideas.md`, chosen to combine: Fast shot, Spray, Bounce, Grow, Steer, Drill, Explode, Poison, Reload on hit, Teleport, Echo and Radar (renamed before release).
- Everyone picks one card before the first fight; every fight is a point; the loser of each fight picks from a seeded offer; first to five; a fight never ends in a tie.
- Running it back raises the target to 10, then 15.
- A minimal menu: local match, host, join, invite a Steam friend.
- Keep a short recorded-input replay as a regression check; it plays through the general rules, not a profile.
- Delete the footage-slice profiles, the historical-rematch setup, the fixed offer lists, the ROUNDS card catalog and their capture-anchor tests once nothing uses them.

Online, inside M1:

- Spike first: run the current host/client at simulated 80 ms round trip with 2 % loss, and compare client-side prediction with interpolation against rollback for this physics.
  The current model shows the remote authority's snapshot with no prediction, so a player's own movement lags by a full round trip.
- Replace JSON snapshots with a compact binary encoding.
- Add Steam networking (relay, friend invites, no port forwarding) beside the UDP transport.
  Development can use Valve's public test app until the game has its own App ID.
- The MVP's first play session is online.

## M3 — Builds that keep surprising us

The goal is the "first time we found that combo" feeling, session after session.

- Play-test the experiments in `docs/game-design.md`: target score, run-it-back, opening pick, curse and modifier frequency.
- Trait cards, duo cards, curses and random fight modifiers.
- Fighter-changing cards: grapple hook, portal gun, melee, dash.
- Destructible terrain and screen distortion that scale with build power.
- Grow toward forty cards and ten arenas, adding what play sessions ask for.

## M4 — Our own identity

Style and juice are as important as mechanics; they wait only until the game is playable.

- Logo, palette, fighter and card art, original sound, screen shake, hit pause and particles.
- Controller support for every player, settings that persist.
- 2v2 and free-for-all.

## M5 — Steam release

- A check by someone qualified that the game does not infringe Landfall's trademarks or trade dress.
- App ID (Steam Direct fee), store page, achievements if wanted, crash reporting, a release build pipeline, and an outside playtest.

## Settled

- Working title `QUARREL` (2026-10-04); a trademark check comes before release.
