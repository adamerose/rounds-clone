---
format: 3
status: blocked
created: 2026-09-07T16:03:21Z
origin: system-detected
tags: ["product-fidelity", "movement", "bevy"]
value: 6
risk: 5
sessions:
  - claude:96849848-e6ec-488e-b4ac-b113acc49f8a
execution: unattended
depends-on: [50]
supersedes: []
split-from: [49]
---

# Match the source's airborne horizontal speed with a general control change

The source's blue crosses the ice arena faster in the air than the clone's fighter can move at all. Its 4753–4783 landing on the stepped base's right shoulder needs a **sustained 211–238 px/s** of airborne horizontal travel; the clone's airborne speed converges on **192.0 px/s** and, with the release cut ticket 050 landed, the best of eighty measured flight variants sustained **0.0–172.2 px/s**. That is the whole of what still separates ticket 049's route from the source. With the cut in place 049's bound 1 is reached — blue is grounded on the upper-right platform's right wall at world (331.25, 91.22) on tick 4688, 0.38 px from the source's own (331.5, 91.5) — while bound 2 misses by **44.4 px** of leftward reach (the furthest-left flight ends at world x 168.40 against the x 124 that clearing contour 49 needs, and all eighty variants ground on that column between ticks 4739 and 4765) and bound 3 leaves blue at 4786 at image (723.53, 425.57), **+80.53 px in x and +58.57 px in y** from the source's (643, 367). Both misses are horizontal; the arc shape is no longer the problem.

The clone's ceiling is arithmetic, not a tuning miss. `set_player_control` sets `velocity.x += (move_axis · RUN_SPEED − velocity.x) · control` with `control = AIR_CONTROL` (0.08) while airborne, and Rapier then divides by `1 + dt · linear_damping` (`dt` 1/60, damping 0.7). The fixed point of that pair is `RUN_SPEED · control / (control + damping / 60)` — 220 · 0.08 / 0.091667 = **192.0 px/s**, exactly the asymptote ticket 050 measured, so the recurrence is the shipped behaviour and not a model. As `control` approaches 1 that fixed point rises only to `RUN_SPEED / (1 + dt · damping)` = 217.5 px/s, and as damping approaches 0 it rises only to `RUN_SPEED` itself, 220 px/s. **The top of the source's band, 238 px/s, is above every value the current control law can produce at any air control and any damping**, so reaching it means moving `RUN_SPEED` or admitting a momentum source that does not exist today. This ticket is the decision about whether to do that at all, and if so with which mechanic.

## Blocked

Changing horizontal control perturbs every arena at once. Unlike ticket 050 there is no zero-change staging trick: 050 could extend eight jump holds so that no shipped release was airborne-and-rising and the cut arrived with all 73 anchors byte-identical, but a horizontal term is read on **every** airborne tick with horizontal input, and damping is read on every tick of every fall as well. There is no gate that makes the change inert on the shipped routes, so it lands as a wide recapture of evidence rather than as a preserved-digest edit. Adam decides:

1. **Do we pursue a general horizontal-control change now**, accepting that every affected anchor is recaptured through the shared renderer and re-paired with its bound source PTS and native RGBA SHA-256 — an estimated 58 of the 73 anchors, in all five replay profiles, by the reading of the shipped scripts in **The preservation problem** below?
2. **Or do we accept the ice traversal residual as it stands** — blue about 97 px from the source at the 4786 anchor, against today's shipped 92 px right and 37 px lower — leave tickets 049 and 052 blocked, and move to the next footage slice instead: ticket 048's first loser draft, or the remaining match lifecycle after the ice round?
3. **If we pursue it, is a per-axis drag acceptable as a clean-room mechanic?** Splitting the fighter's damping so that horizontal decay differs from vertical would leave every measured vertical arc and ticket 050's fitted release cut untouched, which is the cheapest option by far — but Rapier's `linear_damping` is one scalar, so the split has to be written into `set_player_control` as a rule of our own, and no source measurement recorded so far distinguishes a per-axis drag from any other horizontal model. Is that admissible without direct source evidence for it, or must the change stay inside terms the source's behaviour can be shown to imply? **Refined 2026-09-07 by measurement, and the refinement takes the weight off this question**: the horizontal pass now recorded in **Scratch** does distinguish the models. A per-axis drag is worth 2.5 px/s of airborne top speed and 27.3 px of the 36.6 px the ice route needs, so it cannot carry the change alone, while raising `AIR_CONTROL` to the 0.175–0.388 the source's own ramp fits reaches both the measured top speed and the measured displacement without inventing a mechanic. Question 3 is therefore now a question about an optional 1 % refinement rather than about the load-bearing choice it was when written.

Nothing is implemented until these are answered. The measurements listed in **Scratch** can be taken meanwhile without touching product code, and would sharpen answer 3 in particular.

## What the source shows about horizontal motion

The evidence base is thinner here than it was for the jump arc, and that is itself a finding. Ticket 050's twenty-three-arc table records rise, fall, fitted `v0`, ascent, descent and their residuals — **every column is vertical**. No horizontal quantity was measured for any of those arcs, in any arena. Everything below therefore comes from ticket 049's ice tracking, and the last two rows are arithmetic on numbers that ticket already recorded rather than fresh measurement:

| quantity | value | where it comes from |
|---|---:|---|
| source blue, mean airborne horizontal across the whole hop | **195 px/s** | 049 **Measured basis**: bar 969.5 → 709.0, 260.5 px left over 80 ticks, 4688 → 4768 |
| sustained horizontal the 4753–4783 shoulder landing needs | **211–238 px/s** | 049 bound 3, from the same track |
| source blue across the unexplained level stretch, ticks 4726–4736 | **≈192 px/s** | image x863 → x831 in 049's "two things the research did not resolve", 32 px over 10 intervals |
| source blue over the long fall, ticks 4736 → 4768 | **≈231 px/s** | body x ≈831 at 4736 to ≈708 at 4768, 123 px over 32 ticks |
| clone grounded walk on the ice platform (friction 0.92) | **110 u/s** | 049's public trace |
| clone airborne asymptote | **192.0 px/s** | derived above; measured by 050 as Rapier's −191.4 px/s instantaneous |

Two things stand out. The source is not moving at one speed: it crosses the level stretch at about the clone's own asymptote and then covers the long fall at about 231 px/s, so the 195 px/s mean understates the fast part of the hop and the 211–238 px/s band is a real, sustained late-flight speed rather than a spike. And the source blue is *falling* through the fastest stretch, which is the opposite of what the clone does — with damping 0.7 a fighter that stops pressing decays toward zero at 1/1.011667 per tick.

## Candidate mechanisms

Each is a hypothesis, none is admitted, and the constants each touches are named so the cost is visible before anything is measured. The shipped values are `RUN_SPEED` 220, `AIR_CONTROL` 0.08, grounded control 0.18, idle grounded control 0.02, fighter `linear_damping` 0.7, `angular_damping` 8.0, `rapier.gravity` (0, −1500) and `dt` 1/60.

**(a) Lower the fighter's `linear_damping`, with a compensating change elsewhere.** Damping is the single knob behind the 192 px/s asymptote, and lowering it raises that asymptote toward 220 px/s. But damping is not a horizontal knob: it applies to both axes, so it shapes every measured descent as well. The clone's descent fits **1292 u/s²** against gravity's 1500 precisely because damping bleeds the fall; remove it and the descent goes to 1500, **away** from the source's measured 914–1145 u/s² across five ice phases and away from the 1013 u/s² median of ticket 050's nine paired arcs. It also moves the grounded asymptote (`RUN_SPEED · 0.18 / (0.18 + damping/60)` = 206.6 px/s today) and therefore the friction balance behind the measured 110 u/s platform walk, and it re-opens ticket 050's fit: the ×0.30 cut factor and its 82–90 px / 12–14 tick / `v0` 730–780 / ascent 2900–3300 acceptance band were all measured at damping 0.7. This option cannot stand alone; the compensating change would have to restore the descent, which means a second admitted mechanic.

**(b) A separate horizontal drag, leaving vertical untouched.** Per-axis damping: keep the vertical behaviour exactly as shipped (and with it ticket 050's whole fit and every vertical measurement in 049 and 050), and give the horizontal axis its own, smaller coefficient. Nothing vertical moves, the release cut's band is unaffected, and the only routes that change are those with horizontal motion. The problems are two. Rapier exposes `linear_damping` as one scalar, so this is a rule written into `set_player_control` rather than a body property — a clean-room mechanic of our own invention, which is question 3 above. And it still cannot exceed `RUN_SPEED` 220: with zero horizontal drag the fixed point is exactly 220 px/s, short of 238.

**(c) Higher `AIR_CONTROL`, or a separate airborne maximum speed.** Raising `AIR_CONTROL` from 0.08 to 0.2735 puts the fixed point at 211 px/s, the bottom of the source's band, by the same formula; the ceiling at `AIR_CONTROL` 1.0 is 217.5 px/s, so the top of the band is out of reach this way. Air control also governs how fast a fighter reverses direction in the air, not just its top speed, so raising it changes the shape of every airborne correction in every profile, not only the ice hop. A separate air maximum speed — replacing `RUN_SPEED` in the airborne branch with a larger constant — is the only candidate here that can reach 238 px/s without touching the grounded route, and it is the one with the least evidence: nothing measured so far says the source's fighters are faster in the air than on the ground.

**(d) Carry ground speed into the air.** Today the airborne branch pulls `velocity.x` toward ±`RUN_SPEED` from wherever it is, so a fighter that leaves the ground at the grounded asymptote 206.6 px/s decays toward 192.0 px/s rather than keeping it. A rule that preserved take-off speed while a direction is held would keep at most 206.6 px/s, and on the ice route far less — blue leaves the platform under friction 0.92 at the measured 110 u/s. This option is the smallest change and it does not reach the band on its own; it is worth measuring because it is what a source fighter falling at 231 px/s *looks* like, and because combined with (b) it would explain both the level stretch and the fast fall without raising any speed constant.

**What each would do to existing routes.** (a) moves every fall, every walk and every jump arc in all five profiles, and invalidates ticket 050's fitted cut. (b) and (c) leave every vertical measurement alone but move every horizontal route in all five profiles. (d) moves only routes where a fighter leaves the ground with horizontal speed, which is most of them. None of the four is confined to the ice arena, and an arena-specific rule is ruled out by ticket 049's standing decision and by ticket 050's.

## The preservation problem

Ticket 050 could publish an expected-identical set of all 73 anchors because its gate — a jump release read while airborne and rising — could be made unreachable by the shipped scripts. No equivalent gate exists here. Every airborne tick on which a fighter holds a direction reads the term this ticket would change, and under option (a) every tick of every fall reads it too.

Read off the shipped scripts in `scripted_inputs_for` and `connected_ice_input` against the 73 anchors in `crates/rounds-client/src/main.rs`, **an estimated 15 anchors have no horizontal fighter motion of any kind before them and would survive by construction**, leaving about 58 to recapture:

| profile | anchors | first horizontal input | anchors with no horizontal motion before them |
|---|---:|---|---|
| `teal-duel-replay` | 5 | blue `move_axis = −1` at 40 | 1 (`spawn`, 20) |
| `radial-saw-half-blue-replay` | 8 | both fighters from tick 0 | 1 (`arena-reveal`, 0) |
| `yellow-crate-terminal-blast-replay` | 11 | orange from tick 0, blue from 15 | 1 (`calm-start`, 0) |
| `timber-collapse-replay` | 12 | orange at 120 | 1 (`intact`, 0) |
| `rematch-draft-replay` | 37 | blue `move_axis = −1` at 3000; first combat knockback at 2220 | 11 (through `resumed-combat`, 2220) |

Three cautions on that estimate, which is a reading rather than a measurement. It counts an anchor as affected once *any* horizontal motion precedes it, including knockback from a hit, which is why the rematch count stops at the first fire rather than at 3000. It assumes fighters spawn with zero horizontal velocity. And yellow is the awkward case: its fighters carry `gravity_scale(0.0)` and its script has no jump input, which is what made yellow immune to every vertical change, but they do carry `move_axis` on ticks 0–44 and whether they read as grounded or airborne on those ticks has never been measured — if airborne, an air-control change moves 10 of the 11 yellow anchors that a vertical change could never touch.

The comparison ticket 050 used as its backstop becomes the primary criterion here rather than a guard that never fires: for every recaptured anchor, each affected fighter's body centre and green health-bar centre in native 1280×720 pixels against the same two points in the bound source frame, before and after, with the change in distance stated. A horizontal change that makes the ice hop right and other arenas worse is not a fidelity improvement, and the whole point of measuring the 58 is to find out which way that goes before deciding.

## Scratch

Measured on 2026-09-07 by session `claude:96849848-e6ec-488e-b4ac-b113acc49f8a/research-052-opus`, entirely off tickets 049's and 050's retained per-frame tracks plus the prebuilt `rounds-automation inspect` at the public boundary. No frame was decoded, no product code, test, capture or document other than this ticket was touched, no Cargo command was run and no window was opened. Artifacts are under ignored `out/ticket-052/`: `horizontal.py`, `ground.py`, `recurrence.py`, `analyze.py`, `clone.py`, and the records `measurements.json`, `source-horizontal-arcs.json`, `source-ground-runs.json`, `clone-teal-trace.json`, `clone-rematch-trace.json`, `analyze.txt`, `clone-teal.txt`, `clone-rematch.txt`.

**The premise stated above is wrong in the direction that makes this cheaper, and that is the finding.** The 211–238 px/s band was a set of chord estimates over sub-windows of a single flight. Fitting the shipped control law's *own recurrence* to the whole of that flight puts the source's sustained airborne top speed at **218.2–219.4 px/s**, not 238 — and the shipped law reaches 217.5 px/s at `AIR_CONTROL` 1.0 and exactly 220.0 px/s with no horizontal drag. **No speed constant has to move.** What is wrong is not the top speed but the rate at which the source reaches it: the source's airborne velocity converges with a per-tick factor of 0.605–0.815 (time constant 2.0–4.9 ticks) against the clone's 0.9094 (10.53 ticks), and it is that slow ramp, not the asymptote, that loses the ice traversal its distance.

### Method

Positions are ticket 050's tracks unchanged (`out/ticket-050/track-{teal,radial,timber,yellow}.json`, the rigid green health bar) and ticket 049's ice tracks (`source-blue-track.json`, `source-blue-approach.json`). Camera correction is ticket 050's, extended to x by the same map — a point measured at `q'` under camera `(scale s, translation t)` about the frame centre `c` returns to first-frame coordinates as `q = c + (q' − c − t) / s` — using the landmark record for teal and radial and the patch record for timber and yellow, exactly as 050's vertical fits did. Correction matters little in practice: across every held segment and ground run reported below the corrected and raw chord speeds differ by at most 3.9 px/s and by a median of 0.1 px/s, and the corrected value is quoted.

Three instruments, in increasing strength:

1. **Chords and 5-tick slopes.** Bar centres are quantised to 0.5 px and the recording samples a game presenting faster than 60 Hz, so single-tick deltas alias by tens of px/s. Every velocity below is a least-squares slope over at least five ticks.
2. **Held segments.** The source fighters duel and reverse constantly, so a whole-flight mean is not a speed. A *held segment* is a maximal run of ticks whose 5-tick smoothed velocity keeps one sign and stays above 60 px/s (25 px/s on the ground) for at least eight ticks; the arc's longest held segment is the one reported.
3. **The recurrence fit, which is the new instrument.** On any tick with one horizontal direction held, `set_player_control` plus Rapier's damping is exactly a first-order linear recurrence in horizontal velocity, `v_{n+1} = a·v_n + b` with `a = (1 − control)/(1 + dt·damping)` and `b = RUN_SPEED · control/(1 + dt·damping)`, whose fixed point is `b/(1 − a)`. Fitting `(a, b)` to a measured position series therefore measures the two numbers the clone's law has — how fast the fighter approaches its top speed, and what that top speed is — *without first choosing which shipped constant is wrong*. Given `a` the modelled position is linear in `(x0, v0, b)`, so `a` is scanned on a grid and the rest solved exactly; positions are integrated `x_{n+1} = x_n + v_{n+1}/60` to match Rapier's semi-implicit step. Shipped values for comparison: airborne `a` 0.9094, `b` 17.397, fixed point 192.00, time constant 10.53 ticks; grounded `a` 0.8105, `b` 39.143, fixed point 206.61, 4.76 ticks. Synthesising the shipped airborne recurrence from rest and refitting it recovers `a` 0.910, `b` 17.29, fixed point 192.1, implied control 0.0794 and implied `RUN_SPEED` 220.4 at 0.012 px rms, so the instrument is calibrated against the thing it is used to measure.

Fits are reported only where the window actually contains a ramp (its 5-tick velocity rises by at least 60 px/s), the fit rms is at most 3 px and `a` lands inside the grid at 0.05–0.95; a window recorded at the fighter's terminal speed carries no information about `a` and its "fixed point" is meaningless, so those are left blank rather than quoted.

### Frame identity

The frames the ice argument leans on, native 1280×720 RGBA SHA-256 over exactly 3,686,400 bytes, from `out/ticket-049/source-frames-hop.json`:

| tick | PTS | native RGBA SHA-256 | what it fixes |
|---:|---:|---|---|
| 4688 | 2376490494 | `6c2f1b4e8d4f08b56d40005f29c419abf5be4cbfc8a447003711e05211f44cb3` | edge touch; horizontal velocity −60 px/s and falling to 0 |
| 4694 | 2377490490 | `5218c4e0680b30b046e6b2f0563327fa7c94e7d259016577599c7ae491a74ad8` | the horizontal ramp starts here |
| 4698 | 2378157154 | `6ef161ec6f4e77a9155ccd95ce73504b640c7cb1e2b79e12bb69855f0ffe20c5` | ramp complete; 207 px/s reached |
| 4706 | 2379490482 | `d4615e5653bbb486d29850b6b778b3d410f6af498f3a8acc58f371fe1d02a558` | start of the 62-tick cruise |
| 4726 | 2382823802 | `72afd6f5e47adf83918d90a38066720b639645a1e29f7b34cdbe0b00a36be243` | start of 049's level stretch |
| 4736 | 2384490462 | `404d1671077d24b8d81e8c06608344b90dc6334d9fdd573bbf2754f8a0a6da5f` | end of it |
| 4753 | 2387323784 | `dea26d91680b8b08ee0cf1b64cef8f37d9830d94d661a2d3d4c7e457eaef17f3` | start of 049's bound-3 stretch |
| 4768 | 2389823774 | `65cfaa8acb14bd4ca2575be78e2d9084b7ae0ccf3057179d1da55d2e47176733` | landing on the stepped base's right shoulder |

The twenty-three arcs keep the take-off, apex and landing PTS and hashes ticket 050 published in `jump-table.json`; every held segment's own endpoint PTS and hash is in `out/ticket-052/measurements.json`. The ground runs' endpoints are given with their hashes in their own table below.

### 1. Source airborne horizontal velocity, per arc

The airborne window is take-off to landing as ticket 050 fixed it. "Held" is the arc's longest held segment; "sustained" is its chord speed, "peak" the largest 5-tick slope inside it, `v_in`/`v_out` its first and last third. The verdict is the sign of an exponential fit `|v| ∝ e^{−k t}` over the held segment: *decays* at `k > 0.25 s⁻¹`, *grows* at `k < −0.25`, *holds* between. The clone's damping-only decay with no input is `k = 60·ln(1 + 0.7/60) = 0.696 s⁻¹`.

| arc | take-off PTS | airborne t | held t | sustained | peak | v_in | v_out | verdict | recurrence fit |
|---|---:|---:|---:|---:|---:|---:|---:|---|---|
| radial orange | 2364490542 | 36 | 12 | 144.4 | 195.0 | 105.2 | 119.6 | grows | `a` 0.335, top 164 |
| teal orange | 309498762 | 28 | 19 | 191.1 | 282.0 | 170.6 | 258.0 | grows | — |
| teal orange | 314165410 | 21 | 21 | 207.1 | 252.0 | 168.2 | 208.9 | grows | — |
| teal orange | 333665332 | 24 | 22 | 199.1 | 279.8 | 178.8 | 203.0 | grows | `a` 0.900, top 254 |
| timber orange | 2059991760 | 15 | — | — | — | — | — | no held segment | — |
| timber orange | 2062658416 | 41 | 22 | 212.5 | 272.5 | 208.5 | 244.7 | grows | — |
| timber blue | 2067491730 | 17 | 10 | 209.8 | 299.3 | 299.3 | 134.9 | decays | — |
| timber orange | 2069825054 | 36 | 21 | 178.6 | 343.7 | 101.8 | 254.0 | grows | — |
| timber orange | 2076158362 | 13 | — | — | — | — | — | no held segment | — |
| timber blue | 2089991640 | 35 | 18 | 206.9 | 306.6 | 224.6 | 247.9 | grows | — |
| timber orange | 2104158250 | 47 | 8 | 182.7 | 236.0 | 143.3 | 158.1 | grows | `a` 0.615, top 231 |
| timber blue | 2109491562 | 17 | 14 | 189.2 | 232.5 | 143.9 | 183.6 | grows | `a` 0.615, top 211 |
| timber orange | 2115991536 | 36 | 27 | 194.2 | 278.1 | 179.8 | 164.3 | decays | `a` 0.295, top 214 |
| timber orange | 2122324844 | 29 | 25 | 200.4 | 268.7 | 212.3 | 160.3 | decays | — |
| timber blue | 2124158170 | 18 | 16 | 173.7 | 242.9 | 97.2 | 156.1 | grows | `a` 0.460, top 199 |
| timber orange | 2143158094 | 64 | 54 | 219.0 | 309.1 | 245.6 | 222.9 | holds | — |
| timber blue | 2153158054 | 27 | 15 | 175.8 | 220.0 | 130.7 | 179.5 | grows | `a` 0.515, top 198 |
| timber orange | 2155991376 | 31 | 9 | 159.5 | 197.1 | 85.5 | 107.7 | grows | — |
| timber blue | 2157824702 | 24 | — | — | — | — | — | no held segment | — |
| timber orange | 2168657992 | 21 | 14 | 178.4 | 258.0 | 159.1 | 258.0 | grows | — |
| timber orange | 2176991292 | 43 | 24 | 174.7 | 221.4 | 205.6 | 176.0 | decays | `a` 0.745, top 170 |
| timber orange | 2205157846 | 20 | — | — | — | — | — | no held segment | — |
| yellow orange | 4227149758 | 27 | 19 | 198.8 | 288.0 | 140.5 | 172.8 | grows | `a` 0.460, top 228 |
| **ice blue (049's hop)** | 2376490494 | 80 | 72 | **215.8** | 270.0 | 217.1 | 225.7 | holds | **`a` 0.605, top 218.2, rms 1.59 px** |

**Population.** Twenty of the twenty-four arcs contain a held segment; four are fighters that never commit to a direction for eight ticks. Across the twenty, sustained speed runs min 144.4 / p25 178.4 / **median 194.2** / p75 206.9 / max 219.0 px/s, and the largest 5-tick slope inside a segment has median 270.0 and max 343.7 px/s. **The ice hop is not exceptional**: at 215.8 px/s sustained it is the second fastest of the twenty, behind timber orange PTS 2143158094 at 219.0 over a 54-tick held segment, and its 270 px/s peak is at the population median. Nine arcs plus the ice hop carry a usable recurrence fit; their fixed points run min 164.4 / p25 197.5 / **median 210.7** / p75 228.4 / max 254.3 px/s, and their per-tick `a` runs 0.295–0.900 with median 0.515 — against the clone's airborne 0.909 and *grounded* 0.811. **The source's airborne responsiveness is faster than the clone's grounded responsiveness, in every arc that carries a fit.**

**Decay verdict: the source does not decay in flight; it accelerates.** Fourteen of the twenty held segments *grow*, two hold and four decay. The four that decay do so at `k` 0.22–4.98 s⁻¹, which straddles rather than centres on the clone's 0.696. The fourteen that grow are fighters that left the ground below their top speed and are still ramping toward it, which is the same thing the ice hop does from a standing start at the wall.

**The ice hop in detail.** Blue is horizontally *stationary* at the wall — its 5-tick velocity is −60, −42, −6, +24, +36, +18, −21 px/s across ticks 4688–4694, so it has no take-off speed to carry — and then ramps: −84 at 4696, −135 at 4698, −198 at 4700, −207 at 4701. Ten-tick block chords for the rest of the flight are 207, 219, 213, 210, 222, 237, 219 px/s, and the chord over the whole post-ramp stretch 4698 → 4768 is **218.1 px/s over 70 ticks**. A single recurrence fitted over 4694–4768 gives `a` 0.605, `b` 86.2, **fixed point 218.2 px/s at 1.59 px rms**, with per-decade mean residuals inside ±2.3 px and no residual worse than 4.4 px over 74 ticks. Starting the fit anywhere in 4688–4694 moves the fixed point only between 218.2 and 219.4 px/s while `a` moves 0.605–0.815, so **the top speed is the well-determined quantity and the ramp rate the loosely determined one**. At the shipped damping 0.7 those fits imply an air-control coefficient of **0.175–0.388** and a `RUN_SPEED` of **225–234**; with no horizontal drag at all they imply a control of 0.395 and a `RUN_SPEED` of exactly 220.

**049's level stretch, horizontally.** The chord over 4726–4736 is 192.0 px/s against the flight's 218, a 12 % dip, and it shows in the recurrence fit as a 2.25 px mean lag over ticks 4734–4743 which is recovered by the landing. So the stretch is a genuine but small horizontal anomaly, nothing like the vertical one, where the fit goes to 214 u/s² against 914–1145 before and after. Whatever it is, it is not primarily horizontal, and a horizontal mechanic will not explain it.

**Is 218 px/s a control speed, or was blue pushed?** It is a control speed. Blue's green health bar is 32–36 px wide in every one of the 99 hop frames, so it takes no damage during the flight; the horizontal series has no step anywhere — the ten-tick block chords vary by ±14 px/s about 218 with no discontinuity — and a single first-order recurrence fits the whole 74-tick series to 1.59 px rms, which an added impulse would break. **The band's upper figure of 238 px/s does not survive: it was a chord over the noisiest 10–15 ticks of the flight.**

### 2. Source ground run speed against `RUN_SPEED` 220

A grounded stretch is a maximal window whose corrected bar height varies by no more than 8 px and which lasts at least 15 ticks. That excludes every ballistic phase by construction — at the arcs' own 914–1385 u/s² a fall covers 31–94 px in 15 ticks — and the remaining ambiguity, a very slow arc's apex, is separated by the plateau's sign: a fighter resting on a surface sits at a local *maximum* of screen y (it arrived from above and leaves upward), a jump apex at a local *minimum*. Held segments are then measured inside.

Five windows classify as ground:

| interval | fighter | PTS from · hash | PTS to · hash | t | sustained | peak | recurrence fit |
|---|---|---|---|---:|---:|---:|---|
| timber | blue | 2064158410 · `65f0e2de8466a0969591a72f0ba6350a63459c86aeb25ce3314e472771bafdf9` | 2067658396 · `5cd78858f367fa13187b2998765876b19124b18dcae193a5f034c55ea2e41a07` | 21 | 203.0 | 237.2 | `a` 0.425, top **214.4**, implied `RUN_SPEED` 218.8 |
| timber | blue | 2063991744 · `27f003b335281d5fff95d8bd329120f561654a0db92882609d357d559b62edfa` | 2067325064 · `d93d2aa28f6dfcdc404ad674c8c692966f64ffacf61c17546a6f0c213978f130` | 20 | 196.7 | 285.6 | `a` 0.545, top **214.9**, implied `RUN_SPEED` 220.5 |
| timber | blue | 2199491202 · `9d229ca5d7f8d54d5912013d096becc76d418ef603e35efef0a196af897f60fe` | 2202824522 · `768059b857f302e85d1d44877e837296cad7f1ad7a74b591fec6dc86762ba1cb` | 20 | 186.0 | 243.8 | decelerating, no ramp |
| timber | blue | 2126158162 · `1d5a88adbfa72374a363dc01fd2c57a27b97258cdf4831532847c2f760e2a9af` | 2128824818 · `5b0cb435b4bb334db4fa7ac05d24b3ef2a61a6ef0c7454cf997ccc7c9bdfb0a7` | 16 | 169.1 | 272.8 | at speed, no ramp |
| timber | orange | 2174657968 · `a2a1d03cfeb73d6f30a4d4feb5f3bdc6aba036f5ba65e7e4877c7c3752634ed4` | 2177157958 · `25d2fc4b0cf73270e9b3e0a49be7fffdb959ea592585a8d5a6b916e6ab269d88` | 15 | 205.8 | 346.3 | `a` 0.635, top 166.5 |

**Sustained source ground speed 169.1–205.8 px/s, median 196.7.** A thirteen-window set that keeps the apex-classified flat windows too gives min 111.6 / median 196.7 / max 205.8, so the classification does not move the central value. Under the strictest possible filter — timber orange PTS 2078325020, whose bar height varies by 0.98 px across 12 grounded ticks and which was checked frame by frame — the run is 162.2 px/s with a 248.7 px/s peak. **Two of the ground runs carry a clean ramp, and both put the implied `RUN_SPEED` at 218.8 and 220.5 against the shipped 220.** That is the strongest single confirmation in this pass that `RUN_SPEED` itself is right.

**Caveat, stated plainly.** These recordings are duels in which fighters mostly stand, shoot and jump. Across all six tracked fighter-intervals there is *no* flat-surface held run longer than 21 ticks, and a 2 px flatness tolerance over 15 ticks yields none at all: at 2 px the flat windows are 88–349 ticks long and the fighter inside them is stationary (net 0.1–0.7 px/s). The ground figures above therefore rest on five windows of 15–21 ticks in one arena, against twenty airborne segments across four.

### 3. Horizontal deceleration in falls without input, and during blocking

Fifty-one airborne, off-surface stretches were found where `|vx|` falls by at least 40 % without a sign change over at least eight ticks. Their exponential decay rates are min −4.00 / p25 1.38 / **median 4.34** / p75 8.00 s⁻¹, against the clone's damping-only 0.696 s⁻¹. **The source's fighters slow down in the air two to twelve times faster than the clone's linear damping alone could slow them.** That is not drag; it is counter-input — a fighter braking or reversing with the opposite direction held, which a large control coefficient produces and a drag term cannot. The lower tail is equally informative: the slowest decays measured are `k` −0.20 to 0.44 s⁻¹ over 23–47 ticks, *below* the clone's 0.696, so no observation requires a horizontal drag as strong as 0.7 and several are inconsistent with one.

**No interval in the retained tracks isolates a fall with the direction released**, because input state is not observable from the recordings; every stretch above is bounded above by drag-plus-counter-input and bounded below by drag-minus-forward-input. **Blocking cannot be measured at all**: nothing in ticket 049's or 050's tracks records a block, no block visual was ever measured, and no arena interval is annotated with one. That question stays unanswered and no attempt was made to guess at it.

### 4. Clone counterparts at the public boundary

`rounds-automation inspect` reports the authoritative state at its final tick only, so ticket 049's `clone_trace.py` was reused unchanged to run one process per tick and keep the two `PlayerSnapshot`s: teal seed 38 ticks 1–786 in full, and rematch seed 41 ticks 4650–4800 across the ice window. `velocityXMilliPerSecond` and `grounded` are both public, so the clone's own recurrence is fitted directly on velocity — `v_{n+1} = a·v_n + b` by ordinary least squares — and the grounded and airborne populations are separated without inference. The binaries are the prebuilt debug set at `out/cargo-target/debug/`, built from main at 10479ff, which includes ticket 050's release cut.

| population | runs | peak `|vx|` | fitted `a` | fitted fixed point | rms |
|---|---:|---:|---:|---:|---:|
| teal, airborne, both fighters | 11 | 104.5 / **188.7** / 191.5 (min/median/max) | 0.9094 exactly on seven runs | **192.00 exactly** | 0.000 px/s |
| teal, grounded, blue | 3 | 109.5 / 110.3 / 112.2 | 0.817 on the one clean run | **111.9** | 0.50 |
| rematch ice window, blue airborne | 3 | 183.4 / 184.2 / 188.0 | **0.9094 exactly on all three** | **192.00 exactly** | 0.000 px/s |
| rematch ice window, blue grounded | 2 | 110.73 / — / 110.82 | 0.8105 exactly, 0.8125 | **110.74**, 111.11 | 0.000, 0.18 |

Seven of the eleven teal airborne runs, and all four rematch airborne runs (three at exactly zero residual, one at 0.001 px/s), reproduce `a` 0.90939, `b` 17.397 and the fixed point 192.00 px/s at **zero residual**, which confirms at the public boundary that the recurrence in **The clone's ceiling** above is the shipped behaviour and not a model. The runs that do not are the ones interrupted by a wall or a landing.

**The grounded number is the surprise.** The clone's grounded *asymptote* is 206.6 px/s, but its measured grounded speed is 108–112 px/s, because Rapier's surface friction opposes the control term; the fitted `a` is the shipped 0.81 but the fitted `b` is halved. So the like-for-like comparison is:

| | source | clone (shipped) |
|---|---:|---:|
| sustained ground run | 169–206 px/s, median 197 | **110** px/s |
| implied `RUN_SPEED` from a ground ramp | 218.8, 220.5 | 220 by construction |
| sustained airborne speed | 144–219 px/s, median 194; ice hop 218 | **189–192** px/s |
| airborne per-tick approach factor `a` | 0.295–0.900, median 0.515 | **0.9094** |
| airborne time constant | 1.0–9.5 ticks, median 1.5 | **10.5 ticks** |
| horizontal slowing in the air | median `k` 4.34 s⁻¹ | 0.696 s⁻¹ with no input |

**The clone's airborne top speed is already within 12 % of the source's; its airborne *responsiveness* is seven times too slow, and its ground speed is short by a factor of 1.8.**

### 5. The four mechanisms against the data

The decisive arithmetic is displacement, because that is what 049's bounds are made of. Blue's airborne window on the ice is 80 ticks and it starts from rest at the wall; the source covers **260.5 px** in it. Integrating the shipped recurrence from rest over the same 80 ticks:

| variant | fixed point | 80-tick travel | against the source's 260.5 px |
|---|---:|---:|---|
| shipped, `AIR_CONTROL` 0.08, damping 0.7 | 192.0 | **223.9** | 36.6 px short |
| damping 0.4 | 203.1 | 234.9 | 25.6 short |
| damping 0.2 | 211.2 | 242.8 | 17.7 short |
| **damping 0.0**, control unchanged | 220.0 | **251.2** | **9.3 short — this is mechanism (a) or (b) at its absolute limit** |
| `AIR_CONTROL` 0.15 | 204.1 | 254.3 | 6.2 short |
| `AIR_CONTROL` 0.20 | 207.9 | 264.1 | 3.6 over |
| `AIR_CONTROL` 0.29 | 211.5 | 273.7 | 13.2 over |
| `AIR_CONTROL` 0.388 | 213.6 | 279.3 | 18.8 over |
| `AIR_CONTROL` 1.0 | 217.5 | 290.0 | 29.5 over |
| zero horizontal drag *and* `AIR_CONTROL` 0.388 | 220.0 | 287.5 | 27.0 over |

**(a) Lower `linear_damping`.** Ruled out on its own by arithmetic, not by taste: taking damping all the way to zero buys 27.3 px of the 36.6 px needed, still 9.3 px short, while moving every measured descent from the clone's fitted 1292 u/s² to gravity's full 1500 — away from the source's 914–1145 across five ice phases and away from the 1013 median of 050's nine paired arcs — and re-opening 050's ×0.30 cut, whose 82–90 px / 12–14 tick / `v0` 730–780 / ascent 2900–3300 band was fitted at damping 0.7. It also cannot explain the ramp: damping does not change `a` enough (0.9094 → 0.9200 at damping 0) against the source's measured 0.605.

**(b) A separate horizontal drag.** Same arithmetic, same ceiling: with `AIR_CONTROL` left at 0.08, removing horizontal drag entirely gives 251.2 px, 9.3 px short, and moves `a` from 0.9094 to 0.9200 against the measured 0.605. It leaves every vertical measurement and 050's fit untouched, which is its whole appeal, but **it cannot close the gap alone and the data no longer supports it as the primary term**. What it can do is worth 2.5 px/s at the top end: with it the airborne fixed point is exactly `RUN_SPEED` 220.0 rather than 217.5, and the source's measured 218.2–219.4 sits between the two. Question 3 in **Blocked** can therefore be answered without admitting the mechanic at all — it is now an optional 1 % refinement rather than the load-bearing change.

**(c) Higher `AIR_CONTROL`.** This is what the evidence points at, and it points at it three independent ways. The source's own ramp fits an air-control coefficient of **0.175–0.388** on the ice hop and a median of **0.479** across nine other arcs, against the shipped 0.08. The travel arithmetic wants **0.185–0.20** to reproduce the source's 260.5 px exactly, and 0.245 to close 049's 44.4 px bound-2 shortfall — all inside the measured range. And the top speed needs nothing: the source's 218.2–219.4 px/s is 0.3–0.9 % above the shipped law's `AIR_CONTROL` 1.0 ceiling of 217.5 and 0.3–0.8 % below `RUN_SPEED` 220, so **the separate airborne maximum speed the ticket floated is not needed and should be dropped from consideration**. The cost the ticket names is real and unchanged — air control governs reversal as well as top speed, so every airborne correction in every profile moves — but the source's own reversals are *faster* than the clone's, not slower: the 51 measured coast intervals slow at a median 4.34 s⁻¹ against the clone's 0.696, which is the signature of a large control coefficient and is direct evidence in the same direction.

**(d) Carry ground speed into the air.** Falsified directly for the case that matters. Blue's horizontal velocity at the wall across ticks 4688–4694 is −60, −42, −6, +24, +36, +18, −21 px/s: it has **no** take-off speed to carry, and it reaches 207 px/s seven ticks later while airborne. Across the population the same pattern holds — fourteen of twenty held segments *grow* through the flight, meaning the fighter left the ground below its airborne speed. Carrying take-off speed would change nothing on the ice route and would explain none of the twenty arcs.

**What the evidence favours.** Raise `AIR_CONTROL` from 0.08 to about **0.20–0.29**, and change nothing else. That single constant is horizontal-only — `set_player_control` reads it in one place, the `velocity.x` line, and no vertical quantity depends on it — so **every vertical measurement in 049 and 050 and the whole of 050's fitted release cut are untouched by construction**, which is a stronger preservation property than option (b) was reaching for. It reproduces the source's measured ramp, its measured top speed and its measured ice displacement at once, and it needs no new mechanic, no new constant and no clean-room invention. The preservation problem in the section above is unchanged: roughly 58 anchors still read the term and still need recapture, and yellow's `gravity_scale(0.0)` fighters on ticks 0–44 are still the open case. What has changed is that the change is now one number with a measured value and a measured uncertainty, rather than a choice between four mechanics none of which had evidence.

**Still not measured.** The source's ground speed rests on five windows of 15–21 ticks in one arena; blocking is unmeasurable from the retained evidence; and the ground gap — source 169–206 px/s against the clone's friction-limited 110 — is *larger* than the airborne gap and is untouched by every one of the four mechanisms, since it lives in the interaction between grounded control and Rapier friction rather than in any of the constants named above. If 049's route is ever run partly grounded, that gap becomes the binding one.

## Work log

- 2026-09-07T16:03:21Z stage design start session claude:96849848-e6ec-488e-b4ac-b113acc49f8a/shape-052-opus — Splitting the horizontal-reach residual out of ticket 049 after ticket 050's release cut landed and its re-measurement showed 049's bounds 2 and 3 failing on horizontal reach alone. Reading the tickets, the retained 050 evidence and `set_player_control` only; no Cargo command, no executable, no window.
- 2026-09-07T16:09:38Z stage design end session claude:96849848-e6ec-488e-b4ac-b113acc49f8a/shape-052-opus — Idea recorded at risk 5 with an open human decision, so status is blocked and `## Blocked` carries three numbered questions for Adam: pursue a general horizontal-control change now and recapture every affected anchor, accept the ice traversal residual and move to the next footage slice, or — if pursued — admit a per-axis drag without direct source evidence for it. The gap is stated with ticket 050's re-measured numbers (bound 1 reached at world (331.25, 91.22) 0.38 px from source; bound 2 short by 44.4 px of leftward reach; bound 3's best 4786 pose +80.53 x / +58.57 y) and with the derived ceiling that settles the scope: the shipped control law's airborne fixed point is `RUN_SPEED · control / (control + damping / 60)` = 192.0 px/s, reproducing the measured asymptote exactly, and it cannot exceed `RUN_SPEED` 220 px/s at any air control or damping, so the 238 px/s top of the source's band needs a speed constant or a momentum source rather than a re-tune. Four candidate mechanisms are written as hypotheses with the constants each touches and what each does to existing routes, not as admitted mechanics. The preservation problem is stated plainly: no zero-change staging trick exists because every airborne tick with horizontal input reads the term, and an estimated 15 of 73 anchors survive by construction against about 58 to recapture, with the estimate's three cautions named — knockback counts as horizontal motion, spawn velocity is assumed zero, and yellow's `gravity_scale(0.0)` fighters may read as airborne on their ticks 0–44. Scratch lists the five measurements still needed, all of which run off retained tracks and recordings. Recorded honestly that ticket 050's twenty-three-arc table contains no horizontal column at all, so only ticket 049's ice figures are measured source horizontal speeds and two of the four in the table are arithmetic on numbers 049 already recorded. No product code, test, capture, README, architecture note or decision record was touched, no Cargo command was run, no executable was launched and no window was opened; the Ivy ticket checker is not present in this checkout and was not run.
- 2026-09-07T17:17:13Z stage research start session claude:96849848-e6ec-488e-b4ac-b113acc49f8a/research-052-opus — Taking the five measurements Scratch lists off the retained tracks: per-arc source airborne horizontal velocity for ticket 050's twenty-three arcs plus ticket 049's ice hop, source ground run speed against `RUN_SPEED` 220, whether source falls decelerate horizontally, the level stretch horizontally, and the clone's counterparts at the public `inspect` boundary. Reading tickets 049, 050 and 052, the retained `out/ticket-049` and `out/ticket-050` records and `set_player_control`; the only executable run is the prebuilt `rounds-automation.exe inspect` offscreen at the public boundary. No frame decoded, no product code, test, capture or other document touched, no Cargo command, no window.
- 2026-09-07T17:39:26Z stage research end session claude:96849848-e6ec-488e-b4ac-b113acc49f8a/research-052-opus — Scratch rewritten as five measured sections with method, frame identities (PTS plus native RGBA SHA-256) and an evidence-based verdict on the four mechanisms. The headline is that this ticket's own premise does not survive measurement, in the direction that makes the change cheaper: fitting the shipped control law's own first-order recurrence `v_(n+1) = a v_n + b` to the whole ice flight puts the source's sustained airborne top speed at 218.2-219.4 px/s at 1.59 px rms over 74 ticks, not the 211-238 px/s band, and that band's upper figure was a chord over the flight's noisiest 10-15 ticks. Since the shipped law reaches 217.5 px/s at `AIR_CONTROL` 1.0 and exactly 220.0 with no horizontal drag, no speed constant has to move; what is wrong by a factor of six is the approach rate, the source's per-tick `a` being 0.605-0.815 (median 0.515 across nine further arcs) against the clone's 0.9094. Airborne held segments were measured for all twenty-three of ticket 050's arcs plus the ice hop: twenty carry a held direction, sustained min 144.4 / median 194.2 / max 219.0 px/s with 5-tick peaks median 270.0, so the ice hop at 215.8 is second-fastest rather than exceptional; fourteen of twenty grow through the flight, two hold and four decay, so the source accelerates in the air rather than decaying. Source ground runs were found by flat-height windows with a plateau-sign test separating ground from apex: five windows of 15-21 ticks, sustained 169.1-205.8 px/s median 196.7, and the two that carry a ramp imply `RUN_SPEED` 218.8 and 220.5 against the shipped 220, so `RUN_SPEED` is right. Fifty-one airborne decay intervals slow at median k 4.34 1/s against the clone's damping-only 0.696, which is counter-input rather than drag, and the slowest observed decays are below 0.696, so nothing requires a horizontal drag as strong as 0.7; no interval isolates a released fall and blocking is unmeasurable from the retained evidence, which is recorded as unanswered rather than guessed. Clone counterparts were taken at the public boundary with ticket 049's `clone_trace.py` unchanged, one prebuilt `rounds-automation inspect` process per tick, teal seed 38 ticks 1-786 and rematch seed 41 ticks 4650-4800: seven of eleven teal airborne runs and all four rematch airborne runs reproduce `a` 0.90939, `b` 17.397 and the fixed point 192.00 px/s at zero residual, and blue's grounded speed on both the teal and ice platforms is 110.7-112.2 px/s against the 206.6 asymptote, because friction halves `b`. The mechanism verdict is (c), raising `AIR_CONTROL` from 0.08 to about 0.20-0.29 and changing nothing else: the ice displacement arithmetic gives 223.9 px today against the source's measured 260.5 px over the same 80 airborne ticks, mechanisms (a) and (b) reach only 251.2 px even with damping taken to zero, and (d) is falsified outright because blue's horizontal velocity at the wall is -60 to +36 px/s so there is no take-off speed to carry. Question 3 in `## Blocked` was refined to say that the measurement now distinguishes the models and reduces a per-axis drag to an optional 1 % refinement; the numbered ask is otherwise unchanged and nothing was decided on Adam's behalf. Artifacts under ignored `out/ticket-052/`. No product code, test, capture, README, architecture note or decision record was touched, no Cargo command was run and no window was opened; `git diff --check` is clean and the only changed file is this ticket.
