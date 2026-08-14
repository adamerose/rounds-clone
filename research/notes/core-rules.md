# Core rules and measurements

This note explains the machine-readable target in `spec/` and preserves the reasoning behind estimates that footage alone cannot settle.

## Target build

RICOCHET targets the current public Windows build `21020021`, identified by the live menu as `v1.1.2.a75ee335a`.
The local Steam app manifest identifies depot manifest `3274682947036329080`, and SteamDB independently reports the same public Build ID.
The official store lists the release date as 2021-04-01, so the founding goal's “2020” label is treated as a naming error rather than a separate historical ruleset.
Later cross-play, rendering, options, and platform fixes remain in scope when they affect the current public executable.
An older balance value is not used unless current footage or runtime behavior corroborates it.

The target was observed only through public interfaces, public metadata, and gameplay output.
No original code, art, audio, localization, or extracted game data enters this repository.

## Source method

`spec/sources.json` is the canonical source index.
Official store copy and announcements establish product scope and version history.
The running public build and its Steam manifest establish current build identity.
Two unedited public matches provide independent behavior samples.
Community references fill gaps only when footage corroborates them or when the resulting fact remains low-confidence and provisional.
The opening drafts visibly select `Thruster` and `Fast Forward` in WCG, then `Tank` and `Leech` in SSAG.
The 1.05 patch notes distinguish `Leech` from `Life stealer`, so the similar names are not treated as aliases.
Each measurement's `activeCards` field lists every card visible in that duel, while `modifierControl` explains whether the measured player's relevant behavior can be isolated from them.

The selected YouTube uploads expose native 60 fps formats, but the current media service rejected every complete and partial 60 fps download with HTTP 403.
Frame analysis therefore used 640×360 previews at 29.97 fps and 30 fps, which resolve time at approximately two simulation ticks per frame.
Every timing derived from those previews carries a tolerance wide enough to cover that quantization.
No downloaded video or source frame is committed.

## Exact vanilla match sequence

1. Exactly two opposing players join a vanilla match.
2. Each player receives five card choices and selects one persistent starting card.
3. A fixed arena appears, both players spawn with fresh health, ammunition, block cooldown, and no transient projectiles, and control unlocks together.
4. The players fight until one dies from depleted health or leaving the arena bounds.
5. The survivor receives half of one round point.
6. The same arena resets for another duel with cards and score retained.
7. A second duel win completes the round point.
8. The player who lost the full round receives five card choices and selects one persistent card.
9. The next round uses a new arena and repeats the two-duel scoring sequence.
10. The first player to five full round points wins the match.

The maximum vanilla loadout is five cards per player: one starting card and at most four loser drafts before the opponent reaches five points.
A clean simultaneous-death sample was unavailable, so replaying the duel without awarding a half point is an explicit provisional rule rather than a claimed observation.

## Measurement targets

Distances use one visible circular torso diameter as one player diameter.
Time uses 60 Hz simulation ticks.
Speeds use player diameters per tick.

| Metric | Target | Tolerance | Confidence | Accepted observations |
|---|---:|---:|---|---|
| Base body diameter | 1.0 diameter | ±0.08 | high as a normalization choice | 18×18 px and 18×17 px torso samples |
| Sustained horizontal speed | 0.10 diameters/tick | ±0.04 | low | 0.0965 collision-free trajectory; a shot-contaminated 0.0666 comparison is excluded |
| Jump apex height | 4.5 diameters | ±1.0 | low | 4.5078 collision-free grounded-to-apex rise; a shot-contaminated comparison is excluded |
| Jump apex time | 36 ticks | ±8 | low | 36-tick collision-free grounded-to-apex rise; a shot-contaminated comparison is excluded |
| Projectile speed | 2.4 diameters/tick | ±0.7 | low | 2.3333 from one controlled loadout; the second source is excluded |
| Projectile radius | 0.08 diameters | ±0.03 | low | 0.0833 bright-core radius from one controlled loadout |
| Shot recoil | 0.10 diameters/tick | ±0.06 | low | 0.0500 and 0.1422 isolated velocity changes |
| Active block window | 12 ticks | ±4 | medium | 10.01 and 14-tick shield samples |
| Camera horizontal span | 35 diameters | ±5 | medium | 35.5556 and 36.5714 diameter frame spans |
| Out-of-bounds result delay | 6 ticks | ±4 | low | 6.006 ticks from the one clean loss transition |

`spec/measurements.json` preserves each source timestamp, observed interval, named raw fields, normalized result, visible active cards, modifier control, tolerance, confidence, and method.
Every result carries a machine-readable arithmetic derivation that references those raw fields by name, and the repository gate rejects missing operands or a false recomputation.
The coverage contract requires two independent sources for body scale, recoil, blocking, and camera framing.
Movement, jumping, projectile speed, projectile radius, and out-of-bounds timing retain explicit single-source limitations because the independent WCG movement and jump comparisons contain shots, the WCG projectile has visible trajectory cards, and only one clean out-of-bounds transition was available.
Broad tolerances reflect motion-streak ambiguity, partial jump arcs, possible acceleration, and approximately two-tick temporal sampling rather than expected gameplay variability.

## Provisional tuning hypotheses

The following values are implementation starting points, not frame-addressable measurements.
They remain low-confidence until the clone can generate controlled captures for comparison.

| Metric | Starting point | Constraint |
|---|---:|---|
| Ground acceleration | 0.014 diameters/tick² | reaches the measured run-speed band in roughly eight ticks |
| Air-control ratio | 0.80 | preserves the strong visible airborne correction authority |
| Jump speed | 0.25 diameters/tick | algebraically fits the measured height and apex-time bands |
| Gravity | 0.007 diameters/tick² | algebraically fits the measured height and apex-time bands |
| Ground friction | 0.72 retained/tick | produces quick but non-instant settling |
| Base bullet damage | 0.55 health | matches the community estimate and repeated two-hit kills |
| Fire interval | 18 ticks | remains within visible muzzle-event cadence |
| Reload time | 120 ticks | remains within visible ammunition-HUD cycles |
| Block cooldown | 240 ticks | remains consistent with visible availability and card wording |

## Confirmed qualitative behavior

Base players have three shots, no regeneration, and a visible circular body collider.
Bullets travel in the aim direction, apply directional knockback, and disappear on their first environment impact without a bounce upgrade.
Block creates a short circular protection window, reflects direct projectile contacts, pushes nearby bodies, and can launch the blocker from a nearby arena boundary.
Block does not erase damage-over-time effects that were already applied.
The camera holds a stable full-stage 16:9 frame during ordinary duels, adds transient shake for impacts, and returns to the same framing.
Aim remains independent of body roll and horizontal movement.

## Open and conflicting items

The controller button labels remain open because the selected footage uses keyboard and mouse and synthetic input could not reach the live options menu.
The four-tick jump buffer is a provisional feel target because footage cannot distinguish buffering from precise player input.
The simultaneous-death rule remains open because neither recording contains a clean sample.
Ground acceleration, air control, jump speed, gravity, friction, bullet damage, fire interval, reload time, and block cooldown remain low-confidence implementation hypotheses rather than direct footage measurements.
They must be retuned against clone-generated controlled captures instead of being promoted to exact facts.

The 2021 and 2022 footage predates the current build, but official later updates describe cross-play, rendering, options, bullet scaling, and platform fixes rather than a wholesale base-movement retune.
The current build is still the binding target, and any later direct runtime observation overrides the older recordings.

## Gate behavior

The repository checker validates every required `spec/*.json` document against the committed JSON Schema vocabulary subset.
It rejects unsupported schema keywords so future schemas cannot appear enforced while relying on silently ignored features.
It also rejects duplicate identifiers, unknown provenance references, measurements that target unknown facts, raw spans that disagree with their endpoints, normalized results that do not reproduce, missing coverage, insufficient independent sources, and a mechanics filename whose `kind` does not match.
Regression tests prove that missing provenance, an unknown source, an unknown measurement target, unsupported schema vocabulary, a missing raw operand, an inconsistent endpoint span, and false measurement arithmetic all fail.
