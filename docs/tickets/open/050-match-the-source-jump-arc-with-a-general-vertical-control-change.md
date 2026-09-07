---
format: 3
status: idea
created: 2026-09-07T10:36:00Z
origin: system-detected
tags: ["product-fidelity", "movement", "bevy"]
value: 6
risk: 5
sessions:
  - codex:01a073e9-17ec-7170-933a-0e18a071972d
  - claude:96849848-e6ec-488e-b4ac-b113acc49f8a
execution: unattended
depends-on: [49]
supersedes: []
split-from: [49]
---

# Match the source's jump arc with a general vertical-control change

Every jump the source performs rises fast, stops early and then falls gently, while the clone's single fixed impulse rises half again as high over nearly twice as many ticks and falls faster than the source does. Ticket 049 corrects the ice script's jump timing and explicitly leaves this arc difference alone; it is the residual that remains after blue jumps on the right ticks, and fixing it means changing shared vertical control for all five arenas at once.

## Summary of what is measured

Ticket 049's research fitted three source jumps and modelled the clone's, in world units and seconds, with one world unit equal to one native pixel:

| | source | clone |
|---|---:|---:|
| take-off speed, three fitted jumps | 747, 811, 855 u/s | 645 u/s |
| ascent deceleration | 3100, 3150, 3222 u/s² | 1702 u/s² |
| descent acceleration | 914–1145 u/s² | 1292 u/s² |
| ascent / descent ratio | about 3.0 | about 1.3 |
| apex height, the ice edge hop | 82.5 px | 122.75 px |
| rise ticks | 12–13 | 24 |

The source's descent is close to, in fact slightly weaker than, the clone's; only its ascent is steep. That asymmetry is the signature of a variable-height jump released early, or equivalently of extra downward acceleration applied while rising. The clone has neither: `set_player_control` sets `velocity.y = JUMP_SPEED` when jump is pressed while grounded and has no branch that reads a jump release or reduces `velocity.y`, so releasing jump early does nothing and holding it re-applies the full impulse on the first tick after any contact restores `grounded`. The clone's 1.3 ratio comes entirely from Rapier's linear damping of 0.7 against gravity −1500 at `dt = 1/60`.

Supporting artifacts are ticket 049's: `out/ticket-049/source-blue-track.json` and `source-blue-approach.json` for the per-frame source trajectory, `clone-tick-trace.json` for the clone's public positions, velocities and grounded flags, `fit_phases.py` and `jump_model.py` for the fits, and `source-frames-hop.json` and `source-frames-approach.json` for the decoder commands and native RGBA frame hashes.

## The preservation problem

One world unit is one native pixel in every arena — teal, draft, radial, yellow, timber and ice are all laid out in the same −640..640 / −360..360 frame — so these constants are shared, not ice-specific. Any change to take-off speed, ascent deceleration or descent acceleration alters every jump in all five replay profiles, every scripted route that was tuned against today's arcs, and every source-paired anchor those routes produce, including the connected match's earlier fights and its first-round result. The 4601-tick connected route reaches ice only after two earlier arenas whose scripted inputs assume the current arc; three source jumps from one arena are thin evidence for a constant that broad. This is why 049 refused to touch it and why this idea carries risk 5.

The unresolved question is also product-shaped: the three arcs cannot distinguish a variable-height jump that the original player released early from an ordinary jump that is simply short and steep. Which mechanic exists changes what the clone should implement, not only what number it should carry.

## Scratch

- Decide the mechanic before the constants. Look for a source jump held to its full height — one with a taller apex or a longer rise than the 12–13 ticks measured here — anywhere in the indexed footage. If one exists, the original has a variable-height jump and the release rule belongs in `set_player_control`; if every jump in the recordings has the same cut, an ordinary short steep jump is the simpler explanation and no release rule should be invented.
- Ticket 049's unexplained level stretch is still open and may bear on this: source blue's vertical velocity sits near zero for eleven frames, ticks 4726–4736, fitting 33 u/s upward with 214 u/s², while nothing is beneath it. If that is a real vertical-control behaviour rather than a tracking artifact, a two-constant ascent/descent model will not reproduce it.
- Decide how a changed arc is delivered without invalidating what 046 and 047 established. Options to weigh: re-time the affected scripted inputs in the same change and recapture every anchor; or measure first against a throwaway probe and only then decide whether the fidelity gain is worth re-tuning five profiles. Neither is chosen here.
- Whether Rapier's linear damping should carry the ascent/descent split, or a term in `set_player_control` should, is open. Damping applies to horizontal motion too, so changing it is not a vertical-only change.
- Establish the failing case at the public step/snapshot boundary before touching a constant, the way 049 does, and keep the arc claim falsifiable against the fixed native frame hashes already retained.

## Work log

- 2026-09-07T10:36:00Z stage design start session claude:96849848-e6ec-488e-b4ac-b113acc49f8a/shape-049-opus — Splitting the arc-shape candidate out of ticket 049 so that ticket can stay an input-only correction.
- 2026-09-07T10:38:20Z stage design end session claude:96849848-e6ec-488e-b4ac-b113acc49f8a/shape-049-opus — Recorded the measured take-off, ascent and descent constants for both sides, the absence of any variable-height rule in the current simulation, and the preservation problem that one unit equals one pixel in all five arenas makes unavoidable. Left at risk 5 with an open mechanic question in Scratch; no product code, test or capture touched.
