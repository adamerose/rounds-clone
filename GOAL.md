# Goal

Make a game that Adam and his friends want to play together on a regular night: fast 1v1 physics duels, a card draft for whoever lost the round, and the bright, chunky feel that made ROUNDS fun.
ROUNDS is no longer being developed, so this game is where the group keeps improving that idea: new cards, arenas, fixes, and modes they want.
If it turns out good enough, Adam may release it on Steam as his own game.

## Who it is for, in order

1. The friend group, playing online from their own homes.
2. Whoever builds and changes it, which includes agents: adding a card or an arena must be cheap and safe.
3. Possibly, later, Steam players.

## What "good" means

The test is whether the group plays it and asks to play again.
A build is better when a real play session says so, not when a capture matches a frame.
ROUNDS is the starting reference for feel: movement, jump, shot, block, knockback, round flow and the draft should feel like it before the game deliberately goes beyond it.
The two recordings in `reference/manifest.json` and the measurements in `docs/fidelity/` are reference material for that tuning, not acceptance tests.

Once the base game feels right, intentional improvements are the point.
Each one should be a choice the group made after playing, recorded in `docs/decisions.md`.

## Product boundary

The game is a clean-room implementation in Rust and Bevy.
Never copy ROUNDS source code or extract its art, logo, audio, or other asset bytes.
Game mechanics may be reimplemented.
A public release must not use the `ROUNDS` name, Landfall's card names or text, or a look that could pass as their game; until Adam picks a release identity, those names stay in data that can be replaced in one place.

Online play with friends is a core feature, not an extra.
It has to feel responsive at real internet latency and work without port forwarding.
Keep the game rules out of the transport so a Steam transport can sit beside the UDP development transport.

Programmatic play and capture stay product capabilities.
An agent must be able to drive bounded inputs, inspect authoritative state, render frames headlessly, and shut every process down cleanly.

## Engineering rules

Game rules are general: arenas, cards and match flow come from data and ordinary rules, never from branches that recreate one recorded moment.
Tests protect behavior people depend on, reproduced bugs, and release threats.
If support or test code grows larger or harder to understand than the game it protects, stop and rethink before adding more.

## Done

There is no final finish line; `docs/roadmap.md` lists the milestones.
The first one that matters is the group finishing a full online match and wanting another.
