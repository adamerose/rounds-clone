# Goal

Make a spiritual successor to ROUNDS that Adam and his friends want to play together on a regular night, and that Adam can release on Steam as his own game.
It keeps what made ROUNDS great (fast physics duels, skill expression, build variety, destructible maps and juice) and adds mechanics of its own, so builds keep producing new combos without collapsing into power creep.
`docs/game-design.md` holds the design pillars.

## Who it is for, in order

1. The friend group, usually playing 1v1 online from their own homes, sometimes with more players.
2. Whoever builds and changes it, which includes agents: adding a card, rule or arena must be cheap and safe.
3. Steam players.

## What "good" means

The test is whether the group plays it and asks to play again.
A build is better when a real play session says so, not when a capture matches a frame.
ROUNDS is a reference for pace and feel, not a specification; the two recordings in `reference/manifest.json` and the measurements in `docs/fidelity/` are tuning material only.
Design changes come from play sessions and are recorded in `docs/decisions.md`.

## Product boundary

The game is a clean-room implementation in Rust and Bevy with its own name, card names, art and visual identity.
Never copy ROUNDS source code or extract its art, logo, audio, or other asset bytes, and do not use the `ROUNDS` name, Landfall's card names or text, or a look that could pass as their game.
General mechanics may be reimplemented.

Online play with friends is a core feature, not an extra.
It has to feel responsive at real internet latency and work without port forwarding.
Keep the game rules out of the transport so a Steam transport can sit beside the UDP development transport.

Programmatic play and capture stay product capabilities.
An agent must be able to drive bounded inputs, inspect authoritative state, render frames headlessly, and shut every process down cleanly.

## Engineering rules

Game rules are general: arenas, cards and match flow come from data and ordinary rules, never from branches that recreate one recorded moment.
Rules, draft and scoring work for any number of fighters even while 1v1 is the main mode.
Tests protect behavior people depend on, reproduced bugs, and release threats.
If support or test code grows larger or harder to understand than the game it protects, stop and rethink before adding more.

## Done

There is no final finish line; `docs/roadmap.md` lists the milestones.
The first one that matters is the group finishing a full online match and wanting another.
