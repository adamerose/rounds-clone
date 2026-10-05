# Game design

A spiritual successor to ROUNDS: its pace, movement, physics, juice and build variety, with new mechanics of our own.
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

1. **Combos come from rules that react to each other.**
   A card is mostly a small rule on a game event: when I fire, when I hit, when I block, when a bullet bounces, when I land, when I take damage, when I kill.
   Effects include spawning a projectile, teleporting, poisoning, exploding, refilling ammo, healing, shoving terrain and changing gravity.
   Cards that only add numbers are rare, and usually come attached to a rule.
   ROUNDS combos fit this shape: Scavenger is "on hit, refill ammo" and Echo is "on fire, repeat this shot later".
   New rules should combine with existing ones, so nobody has to design the combos one by one.
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
6. **Everyone progresses; the loser gets the edge.**
   Everyone drafts after every fight.
   The loser picks first from a shared offer, so the order itself creates denial picks; with more players, pick order follows standing.
   Options to try for a stronger comeback: the loser sees more cards, can reroll, can give the leader a curse instead of taking a card, or picks the next arena or an arena modifier.
7. **Quick matches that can keep going.**
   A match is short by default.
   At the end, everyone can vote to run it back with their builds kept and a higher target, up to a small fixed number of times.
   Hand limits and evolution keep those extended builds deep rather than bloated.
8. **1v1 first, more players later.**
   The rules, draft and scoring work for any number of fighters, so 2v2 and free-for-all are configuration rather than rewrites.

## Experiments for play sessions

- Hand size: unlimited stacking against five or six slots.
- Draft: loser-only (ROUNDS) against everyone-drafts with loser first, with and without curses.
- Match length and the run-it-back target.
- Whether the base fight with no cards is fun on its own; this comes first.

Cards are data that reloads while the game runs, so trying a variant takes minutes rather than a rebuild.
