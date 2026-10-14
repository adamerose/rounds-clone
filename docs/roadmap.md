# Roadmap

The next milestone is combat people enjoy, not another list of implemented features.
Agents own tuning and implementation; Adam supplies occasional short reactions to a playable build.
The [iteration process](design-docs/game-iteration.html) defines how candidates are measured, reviewed and promoted.

## Current position — 2026-10-14

Ordinary matches, twelve cards, ten arenas, controllers, prediction and Steam friend invites exist.
Previous automated checks establish flow and transport correctness; they do not establish enjoyable combat.
Adam reports that bullets are too fast, movement is slow, and the game feels amateur.

Target practice becomes the default: one keyboard/mouse player and an idle, respawning target.
Local PvP is not a product milestone. Online 1v1 with friends remains the intended match experience.

## 1. Make the base fight responsive and readable

Use practice to compare actual running, stopping, reversing, jumping, recoil, shot travel and blocking.
Keep exact combat numbers and card effects behind the detailed-tooltip toggle.
Add original or properly licensed firing, hit, block, reflect, reload, landing, death and UI sounds.
Add restrained trails, muzzle/hit feedback, clear health/ammo/block readiness and a readable death/reset.
Check mouse/controller aim, moving-platform contact, slopes and wall movement through real inputs.

Exit when repeatable probes and rendered evidence explain the remaining tradeoffs and a short human checkpoint prefers the new base.
Models and metrics cannot certify that preference. Sound and feedback belong here because players need them to judge the fight.

## 2. Make complete online matches worth repeating

Recheck the twelve existing cards against the new base, including fast shots, spread, steering, blocks and reaction chains.
Use bounded aggressive and evasive test policies with delayed observation and imperfect aim when the design question needs them.
Review arenas for readable materials, reachable routes, useful cover, spawn safety and stalled fights.
Record actual human inputs so a reported bad moment can be replayed.
Test Steam invites, leaving/rejoining and sustained matches between homes beside the impaired-network checks.

Exit when the friend group starts and finishes matches comfortably and voluntarily chooses to play again.
Record complaints and rematch choices; silent bots and passing smokes are not enthusiasm.

## 3. Establish a coherent identity and ship a dependable playtest

Unify fighter, arena, card, audio and UI direction.
Add onboarding, remappable controls, persistent audio/video/accessibility settings and controller reconnect handling.
Package a release build that works from a clean installation without developer paths or test-app assumptions.
Check performance/input response on declared minimum hardware, crashes, recovery and disconnects.
Give small groups an attributable build and gather observed behavior plus brief feedback.
Use Steam Playtest once the real App ID and store prerequisites are available.

Exit when new players can learn and return without an agent explaining the game, and repeated sessions remain reliable.

## 4. Release on Steam

Finish the real App ID, ownership/licensing and name review, store assets, trailer, honest feature description,
depots/prerequisites, supported-platform checks, and Valve's store/build review.
Prepare support/crash reporting, Steam Input behavior and a rollbackable release process.
Adam retains spending, legal/account actions and the final public release decision.
Agents prepare the concrete build and materials first, then request only the authority actually needed.

## Content after evidence

Traits, duos, curses, fight modifiers, more cards, grapple/portal/melee/dash, destructible terrain and more players remain experiments.
Resume them when base combat and complete matches pass their gates and playtests identify a missing experience.
Keep existing content working while tuning; do not expand the pool to make the roadmap appear busy.

## Settled direction

QUARREL remains the working title and a clean-room spiritual successor to ROUNDS.
Online 1v1, loser drafts, first to five and unanimous run-backs to ten/fifteen remain the match rules.
Adam's 2026-10-14 instruction delegates ordinary design/tuning and makes AFK target practice the default.
Bounded programmatic play, authoritative inspection and headless rendering remain development capabilities.
