---
format: 3
status: ready
created: 2026-09-06T01:26:15Z
origin: system-detected
tags: ["product-fidelity", "movement", "bevy"]
value: 7
risk: 4
sessions:
  - codex:01a073e9-17ec-7170-933a-0e18a071972d
  - claude:96849848-e6ec-488e-b4ac-b113acc49f8a
execution: unattended
depends-on: [46]
supersedes: []
split-from: []
---

# Reproduce the short hop at the ice platform edge

In the source, blue crosses the ice arena in the air: it jumps off the right outer platform, hops again at the upper-right platform's right wall, clears that platform entirely and lands on the stepped base 80 ticks later. The connected clone gives blue no jump at all across that window, so blue lands on the platform top, walks it under friction at half the source's speed, drops off the left edge and takes an extra stop on the lower-right inner column — 92 native pixels adrift at the bound 4786 anchor. Measurement (see **Measured basis**) separates the difference into two independent causes; this ticket corrects only the first, the source-timed input, so the route is judged on its own before any movement constant is considered.

## Outcome

- Blue leaves each surface on the source's observed ticks — 4673 from the right outer small platform, 4689 at the upper-right platform's right wall, 4769 from the stepped base's right shoulder, each within one tick — through ordinary authoritative input in the existing `rematch-draft-replay` script. No new mechanic is needed for the 4689 take-off: `player_grounded` already reports a side-wall contact as grounded.
- Blue clears the upper-right platform in the air instead of landing on its top, and has no grounded interval on the lower-right inner column (contour 49, native x777–828/y371–533) between ticks 4689 and 4790. Today blue stands on the platform top through tick 4725 and on that column from 4748 to 4771.
- Blue first becomes grounded on the stepped base's right shoulder (contour 56, native top edge y471 from x683 to x699, sloping to y480 at x710) within 15 ticks of the source's 4768, and still within 30 native pixels horizontally of the source's landing pose there (bar x 709.0, body about (708,454)); today blue already lands within about 2 pixels of that x, so this clause guards against regression rather than asking for improvement. Today blue reaches that surface at 4795, 27 ticks late.
- At the bound 4786 anchor blue is within 40 native pixels horizontally of source body (640.0,368.5), against today's 95.3, and no worse vertically than today's 35.8 pixels lower; the vertical clause is a guard, not a target, because the arc residual owned by idea 050 accounts for most of it. These figures are measured against the re-tracked source body; the 92/37 in `docs/fidelity/ice-round-observations.md` was measured against that record's earlier source estimate (643,367) and is superseded here. Orange keeps its current agreement of about 5 pixels.
- The later ice exchange, the four damage hits at 5005/5044/5084/5312, the terminal shot at 5311/5312, the adjacent result boundary at 5338/5339 and the first blue round award are still produced by real input, with the same outcome at the same ticks; removing only the 5311 fire input still leaves orange at 25 health and the authority in ice combat. Later blue input rows may be re-timed to absorb blue's earlier arrival; orange's rows stay as they are. Every affected anchor from 4603 onward, including the bound `ice-established` anchor at 4603 that the corrected 4601–4656 hold will change, is recaptured and re-paired with its bound source PTS and hash.
- The twelve ice anchor ticks and their source pairings stay exactly as they are, and earlier arenas are untouched: every anchor before tick 4601 and the other four replay profiles keep the digests retained by tickets 046 and 047.

## Decisions

- Scope is input only. The change lives in the blue rows of `connected_ice_input` in `crates/rounds-sim/src/lib.rs`, reached from `scripted_inputs_for` for every tick at or after `CONNECTED_ICE_COMBAT_TICK` (4601); blue is player 1. Input timing and holds are configuration. No physics constant, contour, friction or gravity value, jump-release rule, arena-specific special case, forced velocity at a source tick, scripted pose or new `ReplayProfile` may be introduced, and no test-only route may substitute for the script.
- The arc-shape residual is explicitly out of scope and belongs to idea 050. The source rises 82.5 px in 13 ticks at the hop, while the clone's fixed `JUMP_SPEED` impulse rises 122.75 px over 24 ticks with no variable-height rule anywhere in `set_player_control`. That residual is worth about 11 ticks of flight timing and about 40 px of apex height, which is why the tolerances above are what they are and not tighter: 11 ticks at the source's mean airborne 195 u/s is about 36 native pixels of horizontal phase error, and 17 ticks after a take-off from the same shoulder the clone stands about 30 px higher than the source's measured 85.5. Meeting these numbers is a real improvement on today's route; meeting the source's arc is not this ticket's job.
- The 4601–4656 held jump must be corrected too, not only the empty 4656–4801 window. At tick 4674 the clone's blue is already falling from that climb at native (974.5,238.8) while the source blue has only just left the right outer platform at about (1024,360); the divergence begins before the wall contact, so the hop cannot be judged in isolation from it.
- Holding jump re-applies the full impulse on the first tick after any contact restores `grounded` — the public trace shows orange jumping at both 4711 and 4712 for exactly this reason. Any hold used to make a take-off land on the right tick must be checked for a second unintended jump in the grounded/airborne intervals.
- The 4689 wall take-off is geometrically tight. Admission review modelled the shipped vertical recurrence and found that a jump from the right outer platform's top surface passes the upper-right platform's wall contact band by three to eleven pixels at every allowed take-off tick, which would then land blue on the platform top. The contract is still satisfiable by leaving the right outer platform from lower than its top surface, for example brushing its left wall while falling before jumping, but the implementation must demonstrate the actual contact in the public trace rather than assume it.
- Anchor ticks are not moved. The twelve ice anchors in `rounds-client` capture-replay (4541, 4603, 4786, 4969, 5213, 5310, 5312, 5314, 5338, 5339, 5355, 5466) and their source PTS/hash pairs in `docs/fidelity/ice-round-observations.md` stay fixed; the comparison must not be flattered by re-timing an anchor to a tick that happens to match.
- Public boundaries are preserved: `AuthoritativeMatch::step`/`snapshot`, `PlayerInput`, the five existing `ReplayProfile` variants, the transport and the one shared renderer. `docs/fidelity/ice-round-observations.md` records the current 92/37 gap and becomes false on delivery, so it is updated in the same change.
- Cargo must reuse the prepared target at `out/cargo-target` under the repository two-job cap; no clean build. Any visible window follows the monitor-4 rule, hidden-first with the verified centre inside monitor 4 before it is shown.
- Risk is 4: the change is a table of input rows that reverts exactly, every claim it makes is falsifiable against fixed source frame hashes already decoded and retained, and no shared movement, contact or scoring behaviour is touched.
- Ticket 046's frozen contract and its independent assessment are unaffected. Admitting this contract neither waives 046's early-traversal requirement nor reopens it.

## Measured basis

Everything below was measured in the research pass and retained under `out/ticket-049/`. No mechanic is admitted by it.

### Frame identity and tick alignment

Ticket 046's pipeline was reused unchanged (imageio-ffmpeg's bundled FFmpeg 7.1, `-copyts`, `-fps_mode passthrough`, `showinfo` for integer PTS, `format=rgba`, SHA-256 over exactly 3,686,400 native 1280×720 RGBA bytes; no ffmpeg or ffprobe is on PATH). The hop range decodes to exactly 99 frames, PTS 2376490494–2392823762, with a uniform 166666 PTS step, so the recording is constant 60 fps and one source frame is one simulation tick. The approach range adds 21 frames back to PTS 2373157174. Seven decoded hashes reproduce ticket 046 exactly. Exact commands and per-frame hash manifests are `out/ticket-049/source-frames-hop.json` and `source-frames-approach.json`; the decoder is `decode_range.py` beside them.

| PTS | tick | native RGBA SHA-256 | what it shows |
|---:|---:|---|---|
| 2373157174 | 4668 | `064660b445d8606b37d890d8f3b47f14de2a60e63bf3f5b305993dd360a8b30a` | blue standing on the right outer small platform |
| 2373990504 | 4673 | `b1fd8384c8db5a5181551f7459620bc2ae62d4506d9ed61215af5fcb2ba8a08d` | last frame before blue leaves that platform upward |
| 2376157162 | 4686 | `8fb1452c7a08442b40d9872826cec2f2063051bfe725621306a9038de06a0f0c` | apex of that first jump |
| 2376490494 | 4688 | `6c2f1b4e8d4f08b56d40005f29c419abf5be4cbfc8a447003711e05211f44cb3` | edge touch; last frame before the hop's rise |
| 2376657160 | 4689 | `e9ded57327626b1982b6bc96d57d6d9ebf16a085e6b20d7c846de296dfedf849` | first rising frame of the hop |
| 2378657152 | 4701 | `224098b55dcdd9982ad1716775806ef5cdf8f4b45f7346474c4fcb10a6c90618` | apex of the hop |
| 2382657136 | 4725 | `4aeef5b8855d870f84b060ba1316ef3539585f52e88b51a8289f42e73d553f75` | end of the clean descent |
| 2384490462 | 4736 | `404d1671077d24b8d81e8c06608344b90dc6334d9fdd573bbf2754f8a0a6da5f` | end of the unexplained level stretch |
| 2389823774 | 4768 | `65cfaa8acb14bd4ca2575be78e2d9084b7ae0ccf3057179d1da55d2e47176733` | blue lands on the stepped base's right shoulder |
| 2391990432 | 4781 | `e25538f7812f58d7c498cca49f85a46d974b7f6eff38689170ece42e5b7982a2` | apex of the next jump |
| 2392823762 | 4786 | `3d2ae2939f40a3a8d53191589ef5c42ab360a7242ce65d9590685069536b5ba4` | bound early-traversal anchor |

Tick alignment is ticket 046's retained pairing 4786 ↔ 2392823762 extended at 166666 PTS per tick; it reproduces 046's other pairing, 4707 ↔ 2379657148.

### Scale, camera and tracking

`ice_arena()` builds every contour from native image pixels and emits `surface(40 + id, cx - 640, 360 - cy, …)`, and the offscreen `Camera2d` renders into a 1280×720 target behind a 1280×720 background sprite with no scaling override, so world = (image_x − 640, 360 − image_y) and one world unit is exactly one native pixel. That ratio is shared with the teal, draft, radial, yellow and timber arenas, which use the same −640..640 / −360..360 frame. The camera does not move across the 99 frames: the high centre platform's pale bounds stay within x597–598/681–682 and y182–184/230–231 (`out/ticket-049/source-camera-landmarks.json`), so measured image displacements are world displacements.

Blue drags a translucent motion trail in its own colour, so a raw blob centroid wanders by tens of pixels. Two markers were measured per frame instead: the rigid green health bar above the fighter (`~(158,213,42)`, constant 34–36 px wide because blue keeps full health here) as the primary trajectory measure, and a body disc from an azure mask restricted to rows and columns at least nine pixels thick. The bar sits a constant +23 to +27 px above the body in y. Per-frame tables, each row carrying index, PTS, native RGBA hash, bar centre and body centre, are `out/ticket-049/source-blue-track.json` (99 frames) and `source-blue-approach.json` (21 frames); `clone-tick-trace.json` holds clone ticks 4650–4810 for both fighters from 161 separate `rounds-automation inspect --profile rematch-draft-replay --seed 41 --ticks N` runs. The producing scripts are `blue_body_track.py`, `camera_check.py`, `clone_trace.py`, `fit_phases.py`, `jump_model.py` and `export_frames.py`; inspected PNGs and crops are under `frames/`.

### The source hop

Blue reaches the platform edge already airborne. It walks left along the right outer small platform (image x1030–1117, top y373) and leaves it upward at tick 4673, bar (1024.0,335.0), rising 93.5 px in 13 frames to an apex at tick 4686. The edge touch at tick 4688 is two frames later, still on that apex plateau (bar y 241.5, 242.5, 242.5 across 4686–4688): bar (969.5,242.5), body (971.5,268.5), body height 22 px. The body's left edge is at x960 and its bottom at y279 against the platform's right wall x963–964 and top y271–272, so blue overlaps the wall by about four pixels and hangs about eight pixels below the top surface, with vertical velocity near zero. From there blue rises again:

| quantity | measurement |
|---|---|
| take-off | tick 4688/4689, bar (969.5,242.5), body (971.5,268.5) |
| apex | tick 4701, bar (953.5,160.0), body (951.0,187.5) |
| apex height above take-off | 82.5 px by the bar, 81.0 by the body; 82 ± 2 world units |
| rise duration | 13 ticks = 0.2167 s |
| landing | tick 4768, bar (709.0,430.5), body about (708,454) |
| airborne duration | 80 ticks = 1.333 s; 260.5 px left, mean 195 world units per second |
| where blue lands | the stepped base (contour 56) right shoulder, top edge y471 from x683 to x699, sloping to y480 at x710 |

Blue never touches the upper-right platform's top surface; its descent crosses that top height (y272) at ticks 4725–4728, already left of the platform's left edge x878. It jumps again from the shoulder at tick 4769 and at the anchor, tick 4786, is beneath the centre platform at bar (643.0,345.0), body (640.0,368.5) — the source blue (643,367) the ticket already recorded.

Constant-acceleration least-squares fits over whole phases (per-frame differences are aliased because the recording samples a game presenting at a higher rate; cumulative fits are not), in world units and seconds:

| source phase | ticks | v0 | acceleration | fit rms |
|---|---|---:|---:|---:|
| rise from the right outer platform | 4673–4686 | 810.6 up | 3150 down | 3.21 px |
| rise of the edge hop | 4689–4701 | 746.5 up | 3100 down | 2.64 px |
| descent from that apex | 4701–4725 | 71.2 down | 914 down | 1.50 px |
| long fall to the landing | 4737–4767 | 40.6 down | 1145 down | 2.20 px |
| rise of the following jump | 4769–4781 | 855.3 up | 3222 down | 4.39 px |

### The clone's jump, and what the comparison rules out

`set_player_control` sets `velocity.y = JUMP_SPEED` (680) when jump is pressed while grounded; `PhysicsBoundary::new` sets gravity −1500, `dt = 1/60` and linear damping 0.7, and Rapier integrates `v ← (v + g·dt)/(1 + dt·damping)`. That recurrence reproduces the public trace exactly — orange jumping from rest at tick 4711 reports vy 647.4, 615.3, 583.5, 552.0, 520.9 against the model's 647.45, 615.22, 583.5, 552.0, 520.9 — so it is the shipped behaviour, not an assumption. The clone's jump reaches 647.45 u/s on the first tick, rises for 24 ticks to 122.75 px, and has already passed 100.17 px by tick 13. Orange's measured arc from 4712 to its apex at 4735 rises 116.9 px in 23 ticks; the small difference is the held jump applying twice while orange was still reported grounded.

| | source | clone |
|---|---:|---:|
| take-off speed, three fitted jumps | 747, 811, 855 u/s | 645 u/s |
| ascent deceleration | 3100, 3150, 3222 u/s² | 1702 u/s² |
| descent acceleration | 914–1145 u/s² | 1292 u/s² |
| apex height, the edge hop | 82.5 px | 122.75 px |
| rise ticks | 12–13 | 24 |

Walking off the edge is ruled out: blue gains 82.5 px of height from a frame where its vertical velocity is near zero. A wall bounce is not supported: the take-off speed at the wall, 746.5 u/s, is the smallest of the three fitted jumps, below the same fighter's ordinary jump 16 ticks earlier and its jump 80 ticks later, so nothing proportional to an impact was added; contact matters only in that it makes a further jump legal, which the existing authority already permits. What remains is an ordinary jump whose ascent is cut — the asymmetry of about 3.0 between ascent and descent that the clone does not have (1.3, entirely from linear damping). Whether the original cuts a held jump on release or simply has a short, steep jump cannot be separated from these three arcs, which is why that question moves to 050 rather than being answered here.

### Today's inputs and grounded state

`connected_ice_input`'s blue rows covering this window are `move_axis = -1` with jump held for 4601–4656, jump false for 4656–4801, jump held again 4801–4811, then no jump. There is no jump input anywhere near the source's take-off ticks 4673, 4689 and 4769, and the one held window that exists matches none of them. Grounded state is public through `PlayerSnapshot.grounded`: blue is grounded for ticks 4680–4725, 4748–4771 and 4795–4802, airborne otherwise.

| clone tick | blue world | blue image | grounded | note |
|---:|---|---|---|---|
| 4674 | (334.46,121.17) | (974.5,238.8) | false | still falling from the 4601–4655 held-jump climb |
| 4680 | — | — | true | lands on the upper-right platform top |
| 4688 | (308.76,99.98) | (948.8,260.0) | true | source body (971.5,268.5) is at the right wall, 23 px right and 8 px lower |
| 4701 | (284.27,99.99) | (924.3,260.0) | true | source body (951.0,187.5) is at its hop apex, 73 px higher |
| 4726 | (234.61,99.73) | (874.6,260.3) | false | walks off the platform's left edge |
| 4748 | (173.69,0.26) | (813.7,359.7) | true | lands on the lower-right inner column — the extra stop |
| 4772 | — | — | false | leaves that column |
| 4786 | (95.28,−44.28) | (735.3,404.3) | false | source body (640.0,368.5) is 95 px left and 36 px higher |
| 4795 | (70.11,−103.69) | (710.1,463.7) | true | reaches the stepped base shoulder the source reached at 4768 |

Blue's horizontal speed while grounded on the platform settles at 110 u/s, half the source's airborne 195 u/s, because platform friction 0.92 balances the grounded control term against `RUN_SPEED` 220. That friction stretch and the extra column stop are most of today's 92 px horizontal gap, and both disappear if blue is in the air at the source's ticks.

### Two things the research did not resolve

- Blue's vertical velocity is near zero for eleven frames, ticks 4726–4736, while it crosses image x863 to x831 with nothing beneath it: the fit over that stretch is 33 u/s upward with 214 u/s², against 914–1145 before and after. The cause is unknown, and it means the 80-tick airborne interval is not one clean ballistic arc.
- The 4601–4656 held jump leaves blue in a different state before the hop even begins, as the tick 4674 row above shows. Correcting it is part of this ticket, but the source's controls during 4601–4672 were not measured frame by frame; the take-off at 4673 is the first fixed point.

## Evidence required

- A regression at the public step/snapshot boundary that fails before the correction for the diagnosed reason and passes after. Drive `AuthoritativeMatch::step` with `scripted_inputs_for(ReplayProfile::RematchDraftReplay, 41, …)` and assert on `snapshot` alone: blue airborne across 4690–4750, no grounded tick on the lower-right inner column between 4689 and 4790, first grounded tick on the stepped base shoulder within 15 ticks of 4768, and blue's position at 4786 inside the stated tolerance. Expectations come from the source table above, not from whatever the corrected route happens to produce. No private body access, teleport or forced velocity.
- The source identities this rests on, reproduced and cited by PTS and native RGBA SHA-256: 2373990504/`b1fd8384…` (take-off 4673), 2376490494/`6c2f1b4e…` and 2376657160/`e9ded573…` (edge touch and first rising frame, 4688/4689), 2378657152/`224098b5…` (apex 4701), 2389823774/`65cfaa8a…` (landing 4768), 2391990432/`e25538f7…` (apex 4781) and 2392823762/`3d2ae293…` (anchor 4786). Reuse the retained decoder commands and per-frame tables in `out/ticket-049/source-frames-hop.json`, `source-frames-approach.json`, `source-blue-track.json`, `source-blue-approach.json` and `source-camera-landmarks.json` rather than re-deriving them; preserve native pairs and use crops only as supporting inspection.
- A recapture of the affected anchors through the shared renderer with `rounds-client capture-replay --profile rematch-draft-replay --seed 41 --ticks 5466`, and a native comparison table of blue's position against the source at 4786, 4969, 5213 and 5310, stated in native pixels beside today's 95/36, 4969 and 5213 within roughly 10 px, and 5310 close for blue. Confirm the anchors before tick 4601 keep the digests retained from tickets 046 and 047, and re-pair every changed ice anchor with its bound source PTS and hash.
- The full verification set on the prepared two-job target: `cargo fmt --all -- --check`; `cargo clippy --workspace --all-targets --locked -- -D warnings`; `cargo build --workspace --locked`; `cargo test --workspace --locked` with no failures or ignored tests; the 5,466-tick two-client smoke with both clients, the local host and received-state GPU in agreement; the offscreen capture-replay above; `visible-flow --profile rematch-draft-replay --seed 41 --ticks 5466 --automated` with the hidden-first monitor-4 guard reporting the verified window centre; `git diff --check`. Say plainly what was unavailable, as the earlier tickets did for physical controllers.
- A responsibility inventory stating that only input configuration and tests changed, plus the living records the new numbers make false — `docs/fidelity/ice-round-observations.md`'s 92/37 paragraph above all. No new profile, phase, renderer path, transport change or evidence launcher.

## Work log

- 2026-09-06T01:26:15Z stage design start session codex:01a073e9-17ec-7170-933a-0e18a071972d/01a073ea-e6fa-7bd2-9ff0-85f9201f2b93 — Searched the queue and decisions, confirmed number 49 across worktrees with the release-matched helper, and captured the observed short-hop gap without admitting a speculative movement rule.
- 2026-09-06T01:28:16Z stage design end session codex:01a073e9-17ec-7170-933a-0e18a071972d/01a073ea-e6fa-7bd2-9ff0-85f9201f2b93 — Idea records the native source identities, current public reproduction and unresolved contact/input cause; release-matched ticket validation passed, with no mechanic admitted.
- 2026-09-07T10:03:05Z — Measured the hop instead of describing it. Decoded all 99 native frames from PTS 2376490494 to 2392823762 and 21 more back to 2373157174 with ticket 046's exact FFmpeg 7.1 pipeline; seven hashes reproduce 046 and the uniform 166666 PTS step confirms one source frame per tick. Confirmed the camera is fixed across the range and that 1 world unit is 1 native pixel in every arena, not just ice. Tracked blue by its rigid health bar and a thickness-filtered body mask: the hop takes off at tick 4688 against the platform's right wall, rises 82.5 px in 13 ticks to tick 4701, clears the platform entirely, travels 260.5 px left and lands on the stepped base's right shoulder at tick 4768. The current clone jump from a standing start rises 122.75 px in 24 ticks, confirmed against orange's public 4711 jump. Three fitted source jumps give 747-855 u/s take-off with 3100-3222 u/s^2 ascent and only 914-1145 u/s^2 descent, against the clone's 645, 1702 and 1292. Walk-off and the current full jump are ruled out numerically; a wall bounce adds no energy; the arcs fit a cut ascent. The script gives blue no jump at all between ticks 4656 and 4800, and the public grounded flag shows blue standing on the upper platform through 4725 and taking the extra lower-right column stop at 4748-4771. Recorded per-frame tables, decoder manifests and scripts under out/ticket-049/. No mechanic admitted, no product code, test, README, architecture or decision changed.
- 2026-09-07T10:12:01Z stage design start session claude:96849848-e6ec-488e-b4ac-b113acc49f8a/shape-049-opus — Reshaping the idea into a contract for the first correction alone, the source-timed input, with the arc-shape residual split out; reading only, no Cargo, capture or window.
- 2026-09-07T10:34:40Z stage design end session claude:96849848-e6ec-488e-b4ac-b113acc49f8a/shape-049-opus — Scratch folded into the body and deleted; outcome now names the source take-off ticks 4673/4689/4769, the removed lower-right column stop, a 15-tick landing tolerance against the source's 4768 and 40/35 native pixels at the 4786 anchor, each tolerance justified by the measured arc residual it cannot fix. Decisions bind the change to `connected_ice_input`'s blue rows, keep the twelve ice anchor ticks and their source pairings fixed, and rule out every physics, contour and profile change; risk lowered to 4 because the rows revert exactly and every claim is falsifiable against retained frame hashes. The arc-shape candidate moved to idea 050 with the measured constants. Only these two ticket files changed, `git diff --check` passes, and no product code, test, capture or window was touched.
- 2026-09-07T10:34:04Z — Amended after admission review by session claude:96849848-e6ec-488e-b4ac-b113acc49f8a: the recapture clause now covers the bound 4603 anchor, the two tolerances that today's route already meets are labelled as guards, later re-timing is limited to blue's rows, the reviewer's wall take-off geometry is recorded as a binding demonstration requirement, and the shaping session is listed. No product or evidence change.
- 2026-09-07T10:38:36Z — Independent admission reviewer claude:96849848-e6ec-488e-b4ac-b113acc49f8a/admit-049-opus approved shaping range 9bd8773..56dc2b4 after re-hashing the recording, re-decoding the 119 native frames from PTS 2373157174 to 2392823762 with an independent seek offset, reproducing all eleven frame identities in the ticket's table and all 120 frames of both retained manifests, inspecting the take-off/apex/landing frames, and reproducing every clone position, grounded interval and code seam from the prebuilt executables. Its five findings were folded into amendment 8548542; evidence is under `out/ticket-049/admission-review/`.
- 2026-09-07T10:38:36Z stage review end session claude:96849848-e6ec-488e-b4ac-b113acc49f8a/admit-049-confirm-opus — Confirmed the amended contract at risk 4 across 9bd8773..8548542, building on the earlier reviewer's independent frame decode and clone reproduction: the recapture clause now covers the bound 4603 anchor, which is in `rounds-client` capture-replay's twelve ice anchors; the 30 px landing and 35.8 px vertical clauses read as regression guards and match the retained trace (4786 image 735.3,404.3 against source body 640.0,368.5 is 95.3 right and 35.8 lower; 4795 lands within about 2 px of source x 709.0); later re-timing is limited to blue's rows; the 4689 wall geometry is bound as a demonstration requirement, not an admitted mechanic; the shaping session is listed. No new mechanic, physics or product decision, depends-on [46] and risk 4 unchanged, no Scratch or Blocked section. The Ivy ticket checker is not present in this checkout and was not run.
- 2026-09-07T10:38:36Z — Admitted: status set to ready by the top-level session claude:96849848-e6ec-488e-b4ac-b113acc49f8a after both independent contexts approved; the amendment record line was reworded to drop its unpaired stage marker and the wall take-off bullet now names the right outer platform explicitly. Implementation remains unreviewed.
