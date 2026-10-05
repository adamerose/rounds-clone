# Game design

`QUARREL` (working title) is a spiritual successor to ROUNDS: its pace, movement, physics, juice and build variety, with new mechanics of our own.
Everything below is a hypothesis until a play session supports it; `docs/playtests/` records which ones survived.
`docs/card-ideas.md` is the pool of card, trait, curse and fight-modifier ideas.

## The basics

The controls and fight basics are ROUNDS' (`docs/rounds-reference.md`): move, one stored jump, crouch, wall cling and climb, arcing shots with three-shot magazines, a reflecting block, and screen edges that hurt and push you back unless you block into them for a launch.
They are the core of the pace and skill expression, so QUARREL keeps them and adds its variety through cards, arenas and modifiers.
The one change under test is recoil you can move with: firing pushes you back, so a downward shot mid-air gives a capped boost.

## What we keep from ROUNDS

- Short fights in small arenas, where one good block or shot decides things.
- Movement and physics that feel great on their own, before any card.
- The loser of each fight picks a card, which keeps matches close.
- Building something across a match, and the "first time we found that combo" moments that keep happening after hundreds of games.
- Power you can see: a big damage build visibly distorts the screen and breaks the map apart.
- Every good ROUNDS mechanic is fair game; our own mechanics come on top.

## What we fix

- **Power creep.** Cards that only add free numbers stack into a contest of multiplied stats and an unreadable screen.
- **Repetition.** After many games the same builds come up; new mechanics, traits, duos, curses and fight modifiers keep matches different.

## Pillars

1. **Easy to learn.**
   Every card explains itself in one sentence and shows its effect the first time it fires.
   Nothing combines through hidden recipes.
2. **Builds come from stats and rules together.**
   Stat cards push a fighter toward a role: high bullet speed plus high damage makes a sniper, fast fire plus spread makes a sprayer.
   Rule cards react to game events: when I fire, hit, block, bounce, land, take damage or kill.
   ROUNDS combos fit this shape: Reload-on-hit is "when I hit, reload" and Echo is "when I block, block again a moment later".
   Rules scale with stats (Radar on a sniper fires a sniper shot), and each new rule should combine with the existing ones, so nobody designs combos one by one.
3. **Most cards have an upside and a downside.**
   "Much harder hits, much slower fire" shapes a build and holds power creep down better than free bonuses.
   Pure upgrades exist but are rarer.
4. **Duo cards.**
   A duo card appears in your offer only once you hold both of its parent cards, and shows both parents so the reason is obvious.
   It makes a combo bigger than its parts, such as every bounce exploding.
5. **One trait at a time.**
   A trait card changes your whole fighter (Sniper, Juggernaut, Acrobat, Brawler) and gives a build its direction.
   You hold one; taking a new trait replaces the old one.
6. **Builds can change the fighter, not just the gun.**
   Grapple hook, portal gun, melee weapons, dash and wall cling sit beside gun upgrades.
7. **The arena is part of the fight.**
   Arenas start with every kind of object ROUNDS has (`docs/rounds-maps.md`): solid ground, loose boxes and balls, breakable pieces, background props, pieces hung on chains, fixed and loose saws, and moving parts.
   Chains never break; what they hold is knocked loose by breaking or shoving the piece itself.
   Most terrain stays solid and nothing is chipped pixel by pixel; there are no drop-through platforms.
   You can tell how a piece behaves from its look alone.
   Falling pieces hurt whoever they land on, and damage visibly scales shot size, screen distortion and how much breaks.
8. **Curses.**
   Now and then the loser's offer includes a curse that hurts the leader, such as slippery feet or a smaller magazine.
   Taking it spends the pick, so it is a real choice; curses never take away control.
9. **Fight modifiers, now and then.**
   Every few fights, at random, one rule applies to everyone for that fight (low gravity, ice floor, huge shots) and is announced before it starts.
10. **Quick matches that can keep going.**
    There are no best-of-three rounds: everyone picks one card before the first fight, every fight is a point, and the loser of each fight picks a card.
    The default is first to five points, five to nine fights.
    At the end, everyone can vote to run it back with builds kept and a higher target, at most twice.
11. **1v1 first, more players later.**
    The rules, draft and scoring work for any number of fighters, so 2v2 and free-for-all are configuration rather than rewrites.
12. **Style and juice matter as much as mechanics, after the MVP.**
    The MVP uses plain shapes; the look, sound and feel get full attention once the game is playable.

## Experiments for play sessions

- Recoil movement: how strong, and whether it is fun or mandatory.
- Whether the base fight with no cards is fun on its own; this comes first.
- The target score and the run-it-back targets.
- How often curses and fight modifiers appear.

## Arena ideas for later

Glass that shatters in one hit, metal that always ricochets shots, rubber that bounces fighters, ice floors, explosive barrels with chain reactions, liquids (water, lava, acid), force and wind zones, low-gravity bubbles, crushers, pendulums and rotating platforms.

## Engineering requirements

- Cards are data that reloads while the game runs, so trying a variant takes minutes rather than a rebuild.
- Arenas are text data that agents write and edit, with a headless preview render to check each change; nobody needs a visual map editor.
- Chains of reactions fade out (each reaction triggered by another is less likely to trigger the next), so absurd combos stay absurd instead of freezing the game or breaking online play.

## Tabled ideas

Kept for later, not planned: hand limits, evolving duplicates, card tags with set bonuses, a synergy pop and shared combo book, sudden death, a kill cam, secret cards, a card pool that grows as the group discovers duos, and handicaps for uneven players.
