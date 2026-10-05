# Game design

`QUARREL` (working title) is a spiritual successor to ROUNDS: its pace, movement, physics, juice and build variety, with new mechanics of our own.
Everything below is a hypothesis until a play session supports it; `docs/playtests/` records which ones survived.

## What we keep from ROUNDS

- Short fights in small arenas, where one good block or shot decides things.
- Movement and physics that feel great on their own, before any card.
- Building something across a match, and the "first time we found that combo" moments that keep happening after hundreds of games.
- Power you can see: a big damage build visibly distorts the screen and breaks the map apart.
- A clean, bright, readable look.

## What we fix

- **Winning costs you upgrades.** In ROUNDS only the loser drafts, so playing hard means not picking.
- **Power creep.** Every card stacks forever on one gun, so late game turns into a contest of multiplied numbers and the screen becomes unreadable.
- **Shallow variety.** Most cards only change numbers, so many builds feel the same.

## Pillars

1. **Builds come from stats and rules together.**
   Stat cards push a fighter toward a role: high bullet speed plus high damage makes a sniper, fast fire plus spread makes a sprayer.
   A good stat card pays for itself in another stat (more damage, slower reload) or with a soft cap, so it shapes a role instead of adding raw power.
   Rule cards are small reactions to game events: when I fire, when I hit, when I block, when a bullet bounces, when I land, when I take damage, when I kill.
   Their effects include spawning a projectile, teleporting, poisoning, exploding, refilling ammo, healing, shoving terrain and changing gravity.
   ROUNDS combos fit this shape: Scavenger is "on hit, refill ammo" and Echo is "on fire, repeat this shot later".
   Rules scale with stats (a sniper's Echo is a second sniper shot), and new rules combine with existing ones, so nobody has to design the combos one by one.
2. **Builds are choices, not piles.**
   A fighter holds a limited number of cards, probably five or six so three-card combos still fit.
   A full hand means a new card replaces one you already have.
3. **Duplicates evolve.**
   Taking a card you already hold turns it into a stronger, different version, rather than doubling its numbers.
   Some pairs of different cards can fuse into hidden cards that players discover on their own; a shared combo book records what the group has found.
4. **Builds can change the fighter, not just the gun.**
   Dash, grapple, wall-cling, a timed parry that returns shots, sticky shots that detonate later, and cards that replace the gun entirely.
5. **The arena is part of the fight.**
   Destructible and moving terrain, ring-outs as a real way to win, and cards that act on the world.
   Power has to show: damage scales bullet size, screen distortion and terrain destruction.
6. **Winning sharpens a build; losing widens it.**
   Everyone picks one card before the first fight.
   After each draft, the winner upgrades or evolves a card they already hold, or swaps one out, while every other fighter takes a new card.
   Winning is rewarded with depth and losing with breadth, so nobody sits out a draft and the loser still gains the most new options.
7. **Quick matches that can keep going.**
   The default to test is first to five fight wins with a draft after every fight: five to nine fights, ending with about three to five cards each plus upgrades.
   The comparison is ROUNDS' rhythm shortened to first to three rounds of best-of-three fights, which keeps tense rounds but leaves only two to four cards each.
   At the end, everyone can vote to run it back with builds kept and the target raised (eight, then eleven), at most twice.
   The hand limit and evolution matter most in run-backs, keeping long builds deep rather than bloated.
8. **1v1 first, more players later.**
   The rules, draft and scoring work for any number of fighters, so 2v2 and free-for-all are configuration rather than rewrites.

## Experiments for play sessions

- Hand size: unlimited stacking against five or six slots.
- Draft: winner sharpens and loser widens, against ROUNDS' loser-only picks and a shared offer with the loser first.
- Draft after every fight to five wins, against first to three rounds of best-of-three, and the run-it-back targets.
- Whether the base fight with no cards is fun on its own; this comes first.

Cards are data that reloads while the game runs, so trying a variant takes minutes rather than a rebuild.
