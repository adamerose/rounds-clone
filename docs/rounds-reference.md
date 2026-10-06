# ROUNDS reference

What ROUNDS actually does, checked on 2026-10-04 against Adam's experience, the ROUNDS fandom wiki's Moveset page, the English NamuWiki article, Steam discussions and the MapsExtended map-editor source.
It is the baseline QUARREL starts from; `docs/game-design.md` says where QUARREL differs.

## Controls

- Move with A/D or the left stick; aim with the mouse or right stick.
- Jump: Space. Shoot: left click. Block: right click.
- Crouch: S. On the ground it squashes the fighter to about half height, which hides you behind low cover and fits through tight gaps; in the air it makes you fall faster.
- ROUNDS has no key rebinding.

## Movement

- One stored jump. Touching the floor or a wall restores it.
  Jumping from the ground spends it, so there is no double jump, but walking off a ledge keeps it for a jump in mid-air.
- Fighters stick to walls and slopes, lean on them, and climb by holding toward the wall and jumping; rhythmic jumping climbs faster than mashing.
- There is no fall damage.

## Shooting

- Three shots per magazine, then a reload.
- Shots drop under gravity and have travel time, so long shots must be aimed above the target.
- A base hit removes about 60 % of an unmodified fighter's health, so two hits kill.
- Shots push what they hit; health does not regenerate.

## Blocking

- A block lasts about half a second, is immune to shots and reflects them back toward the shooter; reflecting a shot extends the block by about another half second.
- Blocking right before impact can fail, so players learn to block early.
- Blocks have a cooldown; they do not stop damage over time such as poison.

## The arena edge

- Crossing any of the four screen edges deals heavy damage and pushes the fighter back in; it does not kill outright.
- Blocking just before touching an edge cancels the damage and launches the fighter off it.
  At the bottom edge this gains a lot of height; at the sides, a lot of distance.
- Earlier QUARREL footage-replay code treated leaving the arena as instant death; the base fight now follows the damage-and-push rule above.

## Matches

- Officially two players; four or more players exist only through community mods such as RoundsWithFriends and Local Game Modes.
- Both players pick one of five cards before the first fight.
- A round is won by the first to two fight wins; the round loser picks one of five cards; the first to five rounds wins.
- Every fight is on a new random arena, including the fights within one round; Adam's recordings show a round going from one arena to the timber-collapse arena to the ice arena.
- Duplicate cards are allowed.

## Arena objects

The map editor used by modders exposes ROUNDS' object kinds:

- Solid ground, as rectangles and circles.
- Physics boxes and balls that shots and fighters push.
- Destructible boxes that shots break.
- Background boxes, which sit behind the play layer.
- Ropes that hang objects or tie them together.
- Saws, fixed or physics-driven.
- Spawn points.
- Animated (moving) objects, and objects that take damage.

Shooting supports can collapse a structure; falling pieces damage fighters, and losing the floor can drop a fighter onto the damaging bottom edge.
Community map mods add liquids (water, lava, acid, space) and force zones that push objects, which shows what players asked for beyond vanilla.

## Sources

- [ROUNDS wiki: Moveset](https://rounds.fandom.com/wiki/Moveset)
- [NamuWiki: ROUNDS](https://en.namu.wiki/w/ROUNDS)
- [Steam discussion: What are the controls?](https://steamcommunity.com/app/1557740/discussions/0/3100141190335594370/)
- [MapsExtended source](https://github.com/olavim/RoundsMapsExtended)
- [WillsWackyMapObjects](https://thunderstore.io/c/rounds/p/willuwontu/WillsWackyMapObjects/)
- [RoundsWithFriends](https://rounds.thunderstore.io/package/olavim/RoundsWithFriends/)
