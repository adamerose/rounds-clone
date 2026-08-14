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

| Metric | Target | Tolerance | Confidence | Evidence |
|---|---:|---:|---|---|
| Base body diameter | 1.0 diameter | ±0.08 | high as a normalization choice | 18×18 px and 18×17 px torso samples |
| Sustained horizontal speed | 0.11 diameters/tick | ±0.02 | medium | 0.1193 and 0.1082 independent trajectories |
| Ground acceleration | 0.014 diameters/tick² | ±0.006 | low | short direction-change ramps at 30 fps |
| Air-control ratio | 0.80 | 0.65–1.00 | medium | long lateral corrections while airborne |
| Jump speed | 0.21 diameters/tick | ±0.04 | low | ballistic fit to ordinary-looking arcs |
| Gravity | 0.009 diameters/tick² | ±0.0025 | low | derived from jump height and apex time |
| Jump apex | 2.5 diameters | ±0.6 | low | ordinary arcs plus a 3.87-diameter contaminated upper bound |
| Base bullet damage | 0.55 health | ±0.05 | medium | community estimate and repeated two-hit kills |
| Fire interval | 18 ticks | ±5 | low | consecutive unmodified-looking muzzle events |
| Reload time | 120 ticks | ±24 | low | empty-magazine to restored-ammo intervals |
| Projectile speed | 2.4 diameters/tick | ±0.7 | low | 2.44 and 2.33 two-tick samples |
| Shot recoil | 0.10 diameters/tick | ±0.04 | low | isolated shooter displacement after firing |
| Active block window | 15 ticks | ±5 | medium | 16.016 and 14.0 tick shield samples |
| Block cooldown | 240 ticks | ±30 | medium | intervals between unmodified-looking blocks |
| Camera horizontal span | 35 diameters | ±5 | medium | 35.56 and 36.57 diameter frame spans |
| Out-of-bounds result delay | 6 ticks | ±4 | low | last visible body to first result fade |

`spec/measurements.json` preserves each source timestamp, observed frame interval, raw pixel values, normalization, tolerance, confidence, and method.
The broad projectile tolerance reflects motion streak ambiguity and two-tick temporal sampling rather than expected gameplay variability.
The measured 3.87-diameter jump arc is an upper bound because recoil, block impulse, or map contact could not be excluded.

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
Exact base reload, fire interval, projectile radius, recoil, gravity, and friction remain low-confidence implementation starting points.
These values must be retuned against clone-generated measurement captures instead of being promoted to exact facts.

The 2021 and 2022 footage predates the current build, but official later updates describe cross-play, rendering, options, bullet scaling, and platform fixes rather than a wholesale base-movement retune.
The current build is still the binding target, and any later direct runtime observation overrides the older recordings.

## Gate behavior

The repository checker validates every required `spec/*.json` document against the committed JSON Schema vocabulary subset.
It rejects unsupported schema keywords so future schemas cannot appear enforced while relying on silently ignored features.
It also rejects duplicate fact identifiers, duplicate source identifiers, unknown provenance references, measurements that target unknown facts, and a mechanics filename whose `kind` does not match.
Regression tests prove that a fact without `sources`, an unknown source, and an unknown measurement target all fail.
