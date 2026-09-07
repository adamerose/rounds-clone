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

Research pass on 2026-09-07 (`claude:96849848-e6ec-488e-b4ac-b113acc49f8a/research-050-opus`) measured twenty-three further source jumps in four other arenas, counted the preservation cost of a change through the public `inspect` boundary, and modelled the candidate mechanics against the shipped constants. No product code, test, README, architecture note or decision was touched; no Cargo command was run and no window was opened. Every generated artifact is under ignored `out/ticket-050/`.

### Method, and why a camera measurement was needed first

The decoder is ticket 049's pipeline, generalised only in taking the source path so it can also read the second recording: imageio-ffmpeg's bundled FFmpeg 7.1, `-copyts`, `-fps_mode passthrough`, `format=rgba`, `showinfo` for integer PTS, SHA-256 over exactly 3,686,400 native 1280×720 RGBA bytes. `out/ticket-050/decode_frames.py` writes frames and hashes; `track_stream.py` consumes the same stream in memory so a 24-second interval can be tracked without caching gigabytes. The two commands actually run for the longest intervals were

```text
ffmpeg-win-x86_64-v7.1.exe -hide_banner -loglevel info -threads 2 -ss 20.500000 -t 16.100000 -copyts \
  -i reference/MedalTVRounds20260903170709695.mp4 -an \
  -vf select=gte(t\,22.492000)*lte(t\,35.608000),showinfo,format=rgba \
  -fps_mode passthrough -threads 2 -f rawvideo -
ffmpeg-win-x86_64-v7.1.exe -hide_banner -loglevel info -threads 2 -ss 204.000000 -t 27.000000 -copyts \
  -i reference/MedalTVRounds20260903165304088.mp4 -an \
  -vf select=gte(t\,205.992000)*lte(t\,230.008000),showinfo,format=rgba \
  -fps_mode passthrough -threads 2 -f rawvideo -
```

Each fighter is measured by the rigid green health bar (~`(158,213,42)`) that ticket 049 used, because the translucent motion trail moves a raw blob centroid by tens of pixels; the bar's owner is decided per frame by whether a blue or an orange body mask dominates the window below it, so neither fighter's screen side is assumed. Frame index is the true tick offset `(pts − pts0) / 166666`, not the position in the detected-row list, and a phase is cut wherever more than one consecutive frame is missing, so the handful of undetected frames do not compress the time axis.

Ticket 049 could take the ice camera as static. These four intervals cannot: two independent estimators were built and cross-checked instead.

* `camera_track.py` phase-correlates a 6×4 grid of 160 px patches on a gradient-magnitude image against the interval's first frame and fits a uniform zoom plus translation about the frame centre, discarding the worst 40 % of patches.
* `landmark_camera.py` measures one large rigid arena landmark per arena — the teal cyan/lime platform faces, the radial dark diamond enclosure, the timber hot-pink floor, the yellow platform faces — as robust percentile bounds, and derives the same zoom and translation from them.

The patch estimator reproduces ticket 049's static ice camera over its 119 frames (PTS 2373157174–2392823762) at scale 0.99841–1.00021, `ty` −1.33..+1.04 px, fit rms median 0.02 px and max 0.68 px, which is an independent confirmation of 049's landmark check. On the four new intervals:

| interval | recording | patch scale p1/p50/p99 | patch `ty` p1/p50/p99 | landmark scale p50 | landmark `ty` p50 | verdict |
|---|---|---|---|---|---|---|
| teal 00:22.5–00:35.4 | 1460e670 (second) | 0.9968 / 1.0154 / 1.0352 | −0.4 / +10.7 / +15.6 | 1.0000 | 0.0000 | camera static; the patch fit follows the animated backdrop, and the platform-face box is `[205, 1074, 306, 469]` in essentially every sampled frame across the whole interval |
| radial 03:52.0–04:07.7 | 1460e670 (second) | 0.9914 / 1.0013 / 1.0221 | −4.5 / −0.1 / +4.1 | 1.0000 | 0.0000 | both agree the camera is static; median disagreement 0.001 in scale, 0.02 px in `ty` |
| timber 03:26–03:50 | 453954a7 (first) | 0.9816 / 1.0001 / 1.0218 | −9.2 / −0.01 / +9.2 | 1.0058 | −1.61 | patch fit self-consistent to 0.41 px rms; real shake only around the explosion |
| yellow 07:02.0–07:04.6 | 453954a7 (first) | 0.9875 / 1.0003 / 1.0378 | −18.4 / +0.02 / +15.7 | 1.0000 | −1.03 | patch fit self-consistent to 0.37 px rms; real shake only after the tick-81 blast |

The teal and radial fits therefore use the landmark record and the timber and yellow fits use the patch record; every jump below reports the camera excursion inside its own window, and no measured jump has one worth more than a few pixels. Phase fits are ticket 049's `fit_phases.py` method unchanged — `y(n) = y0 + v n + a n²/2` over a whole phase, reported in world units per second — because single-frame deltas are aliased by a 60 Hz sampling of a game presenting faster.

### 1. Is the source jump arc general?

Twenty-three ballistic arcs survive the filters (a fitted downward ascent, a following fall, a rise of at least 30 px and 5 ticks, no camera excursion, and a contiguous track). They cover both fighters, four arenas and both recordings, and are additional to ticket 049's three ice arcs. `v_early` is the mean upward velocity over the first four ticks after take-off and `v_late` over the last four before the apex; a fall shorter than 12 ticks cannot separate `v0` from `a`, so those accelerations are not reported.

| interval | rec | fighter | take-off PTS | apex PTS | rise px | rise t | v_early | v_late | fit v0 | fit ascent a | asc rms | fall px | fall t | descent a | desc rms | asc/desc |
|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| radial | 1460e670 | orange | 2364490542 | 2366823866 | 86.3 | 14 | 643 | 60 | 793 | 3428 | 2.28 | 106.2 | 22 | 1129 | 2.08 | 3.04 |
| teal | 1460e670 | orange | 309498762 | 312332084 | 90.0 | 17 | 518 | 45 | 770 | 2968 | 3.36 | 39.5 | 11 | n/a | n/a | n/a |
| teal | 1460e670 | orange | 314165410 | 316832066 | 99.5 | 16 | 608 | 15 | 943 | 4075 | 3.03 | 8.0 | 5 | n/a | n/a | n/a |
| teal | 1460e670 | orange | 333665332 | 335998656 | 96.0 | 14 | 526 | 120 | 807 | 2822 | 4.97 | 18.5 | 10 | n/a | n/a | n/a |
| timber | 453954a7 | orange | 2059991760 | 2061825086 | 48.0 | 11 | 450 | 52 | 562 | 3136 | 1.25 | 4.0 | 4 | n/a | n/a | n/a |
| timber | 453954a7 | orange | 2062658416 | 2067158398 | 151.5 | 27 | 570 | 173 | 540 | 914 | 3.05 | 45.4 | 14 | 1323 | 1.51 | 0.69 |
| timber | 453954a7 | blue | 2067491730 | 2069658388 | 84.0 | 13 | 345 | 211 | 615 | 1478 | 4.64 | 8.5 | 4 | n/a | n/a | n/a |
| timber | 453954a7 | orange | 2069825054 | 2072991708 | 104.0 | 19 | 360 | 143 | 526 | 974 | 4.08 | 72.0 | 17 | 925 | 1.36 | 1.05 |
| timber | 453954a7 | orange | 2076158362 | 2077658356 | 72.0 | 9 | 720 | 240 | 1021 | 6622 | 3.05 | 4.0 | 4 | n/a | n/a | n/a |
| timber | 453954a7 | blue | 2089991640 | 2092991628 | 93.9 | 18 | 565 | 8 | 732 | 2681 | 1.98 | 80.0 | 17 | 1065 | 1.16 | 2.52 |
| timber | 453954a7 | orange | 2104158250 | 2108158234 | 129.2 | 24 | 240 | 48 | 904 | 2765 | 8.40 | 126.6 | 23 | 1013 | 2.17 | 2.73 |
| timber | 453954a7 | blue | 2109491562 | 2111491554 | 56.0 | 12 | 517 | 30 | 658 | 3872 | 1.21 | 8.0 | 5 | n/a | n/a | n/a |
| timber | 453954a7 | orange | 2115991536 | 2118991524 | 110.5 | 18 | 540 | −7 | 1006 | 4043 | 4.94 | 62.5 | 18 | 360 | 2.61 | 11.24 |
| timber | 453954a7 | orange | 2122324844 | 2125658164 | 95.9 | 20 | 614 | 23 | 719 | 2690 | 2.75 | 20.0 | 9 | n/a | n/a | n/a |
| timber | 453954a7 | blue | 2124158170 | 2126324828 | 54.5 | 13 | 435 | 30 | 580 | 2874 | 1.57 | 7.5 | 5 | n/a | n/a | n/a |
| timber | 453954a7 | orange | 2143158094 | 2146158082 | 99.0 | 18 | 526 | 28 | 722 | 2467 | 2.12 | 454.6 | 46 | 1385 | 6.80 | 1.78 |
| timber | 453954a7 | blue | 2153158054 | 2156491374 | 87.4 | 20 | 520 | −8 | 657 | 2487 | 2.49 | 4.9 | 7 | n/a | n/a | n/a |
| timber | 453954a7 | orange | 2155991376 | 2157824702 | 74.5 | 11 | 610 | 179 | 815 | 3801 | 4.18 | 52.4 | 20 | 510 | 1.49 | 7.46 |
| timber | 453954a7 | blue | 2157824702 | 2160324692 | 67.6 | 15 | 173 | 61 | 476 | 1156 | 4.81 | 10.1 | 9 | n/a | n/a | n/a |
| timber | 453954a7 | orange | 2168657992 | 2170991316 | 79.5 | 14 | 541 | 21 | 820 | 3857 | 2.77 | 5.4 | 7 | n/a | n/a | n/a |
| timber | 453954a7 | orange | 2176991292 | 2179157950 | 88.5 | 13 | 481 | 157 | 728 | 2443 | 3.93 | 148.4 | 30 | 698 | 2.47 | 3.50 |
| timber | 453954a7 | orange | 2205157846 | 2207657836 | 74.4 | 15 | 516 | 0 | 724 | 3230 | 2.02 | 7.4 | 5 | n/a | n/a | n/a |
| yellow | 453954a7 | orange | 4227149758 | 4230483078 | 87.9 | 20 | 525 | 61 | 553 | 1737 | 2.63 | 11.7 | 7 | n/a | n/a | n/a |

A twenty-fourth clean arc, radial orange PTS 2378990484 → 2380990476, rises 104.3 px in 12 ticks at fitted `v0` 940.8 and ascent 3621 u/s² with rms 3.91; it is quoted separately because its fall is interrupted by a landing before twelve ticks. The full record, including the fall hashes, is `out/ticket-050/jump-table.json` and the per-interval `jumps-*-corrected.json`.

Native RGBA SHA-256 for the six arcs the argument below leans on:

| arc | take-off PTS · hash | apex PTS · hash | landing PTS · hash |
|---|---|---|---|
| teal orange, short | 309498762 · `d6034934e7cd76051ce7b03788904aeae14061b5eeaa74c00d77a52d3c39ab02` | 312332084 · `760b6f869a02a4d470d608e2b82d89ab427e68302ff8944592b63ce04e90a704` | 314165410 · `cea0feaeb785dab5c0d03c26f10fcc1d5eb3e77dada4641b826c7ed649345ace` |
| radial orange, short | 2364490542 · `2d4b347799382fbbf32082364e94d4f80bd629973115b3532e7167dc3ae7c84c` | 2366823866 · `45f86f206a640ddda2f41b7aecad7bf7037acc8554719260e89d2b57ca8731c5` | 2370490518 · `8357789639bf7345dc6a0e5690f76d02e475f0a586aa48162f6cde3353408c48` |
| radial orange, second | 2378990484 · `95978d63a49fa4cddacf34e067e8865e05a835adb3ff5bcf06c767c7d321edc1` | 2380990476 · `2294997be8b677635f5357539a260b28819d767eab78f4fcb6f65aa73a322c2b` | — |
| timber orange, tallest | 2062658416 · `5510320eb7457ef49a7bf40ad4990731a9cf86f43c3c387cb1a28bc62711f468` | 2067158398 · `cc1f7b481f6b9a094155af4753f4ae8042bb98df9cc50a6f6951fd39980d07a5` | 2069491722 · `6ed3e2f531e107f62add4f899ca81cb45cea6dfa5322b0ed445ad94b36078ad0` |
| timber orange, second tall | 2069825054 · `f54cca1bf18fb48c5456aedfd80ebd916dc82e7dad52895fd261d2577fdb4f9b` | 2072991708 · `ecf54de6ba3e1d3d23d3fe908c1a60850c4b887b6b75663d970345a7b218d2cd` | 2075825030 · `034aa479729250b12060b0db932bfa09c8d2f3345526d189d4a738fdbf490146` |
| timber orange, shortest | 2076158362 · `1a05fe643a71e1d0a181a7b5f5ad58145d0e8167c18937f1b7b37d0ef36fc30e` | 2077658356 · `00b5af4f3ed2b30ac6287dd2acafebd88121899e54df0c29f263a24efda219aa` | 2078325020 · `957e9d4f4a23d44001b43f1a53a9016db6506d83fe65c3b89555d4d6df605b5c` |
| timber blue | 2089991640 · `f7f0f17fef63b317a8d8c5d5377b353369ed3c2cd7e2430df61b2766a9adf73f` | 2092991628 · `568b13cdf6e7665540a0dbfeca97627bf634f8ee1bbbf891911fea6e6ef454d9` | 2095824950 · `ba44889b32192c78d3917edab991004e6b5479697a1746fc2dc31958b6a5110a` |

The take-off, apex and landing frames of the teal, radial and timber arcs were also inspected at 1280×720 (`out/ticket-050/frames/`): each shows the fighter leaving a surface, airborne at the apex with the arena landmarks in identical screen positions, and the camera unmoved.

**What recurs.** A short, steep rise followed by a gentler fall recurs everywhere. Across the 23 arcs the median rise is 87.9 px in 15 ticks, the median fitted ascent is 2822 u/s² and the median fitted descent, over the nine arcs with a fall of at least twelve ticks, is 1013 u/s². The median ascent/descent ratio is 2.73 against the clone's 1.3, so the ice's "about 3.0" is a fair central value for the source in general.

**What does not recur, and this is the finding.** The ratio is not a constant: it runs from 0.69 to 11.24 across arcs measured with the same tracker on a static camera. Rise height varies by a factor of 3.16 (48.0 to 151.5 px) and rise duration by a factor of 3 (9 to 27 ticks). Fitted ascent acceleration varies by a factor of 7.25 (914 to 6622 u/s²) while fitted descent acceleration varies by 3.85 (360 to 1385 u/s²), seven of its nine values falling between 698 and 1385. No single vertical constant can produce both a 914 u/s² ascent and a 6622 u/s² ascent.

**The jumps held to full height exist, and they are symmetric.** Timber orange PTS 2062658416 rises 151.5 px over 27 ticks at a fitted ascent of 914 u/s², against its own fall's 1323 u/s²; timber orange PTS 2069825054 rises 104.0 px over 19 ticks at 974 u/s² against its own fall's 925 u/s². Those two arcs have essentially no ascent/descent asymmetry at all. The short arcs are the asymmetric ones: 72.0 px in 9 ticks gives 6622 against 535, and 74.5 px in 11 ticks gives 3801 against 510. `corr(rise ticks, ascent a) = −0.56`. That is the pattern a cut rise produces when a single parabola is fitted over it, and it is not the pattern a fixed steeper gravity produces.

**The take-off impulse itself does not vary with the rise.** `v_early` has median 525 u/s with most arcs between 450 and 650, and `corr(rise px, v_early) = +0.01`. Fighters leave the ground at the same speed and stop rising at different times. Under a fixed short steep jump the two would move together.

**A third, weaker line of evidence.** Over the nine paired arcs the ascent fits a single parabola worse than its own descent in eight cases (median ascent rms 3.03 px against descent rms 2.08 px), and ticket 049's three ice arcs show the same ordering (2.64/3.21/4.39 against 1.50/2.20). Tracking noise is identical on both phases, so the ascent is the phase that is not a single constant acceleration.

**Answer to the ticket's question.** The recordings do contain jumps the fighter clearly held longer and that rise much higher — the two timber arcs above, at 151.5 px and 104.0 px, against a 48.0 px minimum in the same arena and a 72.0 px arc by the same fighter fifteen seconds later. The source has a variable-height jump. The 12–13 tick rise measured at the ice edge is one point in a 9–27 tick distribution, not a constant.

### The clone's shipped arc is already the source's *full* jump

`clone_jump_model.py` re-derives the shipped recurrence (`v ← (v + g·dt)/(1 + dt·damping)`, `g = −1500`, `dt = 1/60`, damping 0.7, `JUMP_SPEED` 680), which ticket 049 verified against the public trace to five significant figures, and applies a release cut to it.

| case | rise px | rise t | first-tick v | v_early | fit v0 | fit ascent a | fit rms |
|---|---:|---:|---:|---:|---:|---:|---:|
| shipped fixed jump | 122.85 | 23 | 647.45 | 600 | 646 | 1712 | 0.24 |
| release after 6 ticks, cut ×0.25 | 60.69 | 10 | 647.45 | 600 | 821 | 5339 | 1.44 |
| release after 8 ticks, cut ×0.5 | 84.42 | 16 | 647.45 | 600 | 733 | 3138 | 1.13 |
| **release after 10 ticks, cut ×0.25** | **86.58** | **13** | 647.45 | 600 | **750** | **3046** | 1.62 |
| release after 12 ticks, cut ×0.5 | 101.97 | 18 | 647.45 | 600 | 721 | 2490 | 1.08 |
| fixed jump, rising gravity ×2.0 | 64.61 | 12 | 622.73 | 538 | 643 | 3206 | 0.07 |
| fixed jump, rising gravity ×2.2 | 58.80 | 11 | 617.79 | 526 | 641 | 3503 | 0.06 |
| fixed jump, `JUMP_SPEED` 800 | 166.10 | 27 | 766.06 | 716 | 758 | 1744 | 0.39 |

Three things fall out of that table.

1. **Today's constants already reproduce the ice hop once a release cut exists.** "Release after 10 ticks, cut ×0.25" gives 86.58 px in 13 ticks whose single-parabola fit is `v0` 750 u/s and ascent 3046 u/s², against the ice hop's measured 82.5 px in 13 ticks, `v0` 746.5 u/s and ascent 3100 u/s². No change to `JUMP_SPEED`, gravity or damping is needed to produce that arc.
2. **The clone's full jump is the source's full jump.** 122.85 px over 23 ticks with `v_early` 600 sits inside the source's tall-arc family (110.5 px/18 t, 129.2 px/24 t, 151.5 px/27 t) and its `v_early` sits inside the source's 450–650 band. The clone's descent, fitted the same way, is 1292 u/s² against the source's 1013 median and 360–1385 range. On this evidence the shipped constants are approximately right and only the release rule is missing.
3. **A fixed steeper gravity is falsifiable and falsified.** It produces a rise that fits a parabola to 0.06–0.07 px rms and it produces only short jumps. The source's ascents fit at 1.2–8.4 px rms and include 151.5 px rises.

### 2. Preservation cost per replay profile

Jump counts are `state.metrics.jumps` from the prebuilt `out/cargo-target/debug/rounds-automation.exe inspect`, one bounded run per anchor tick; the raw runs are `out/ticket-050/anchor-jumps.json` and `inspect-*.json`. Anchor ticks are the `capture-replay` lists in `crates/rounds-client/src/main.rs`.

| profile | seed | ticks | jump input windows in `scripted_inputs_for` | one-tick presses | jumps taken | anchors | first jump | anchors with no jump before them |
|---|---:|---:|---|---:|---:|---:|---:|---:|
| `teal-duel-replay` | 38 | 786 | blue `{40}`, `{500}`, `{650}`; orange `[330,670)` | 3 | 17 | 5 | 40 | 1 (`spawn`, 20) |
| `radial-saw-half-blue-replay` | 42 | 938 | orange `{160}`,`{340}`,`{520}`,`{820}`,`{888}`; blue `{650}` | 6 | 6 | 8 | 160 | 1 (`arena-reveal`, 0) |
| `yellow-crate-terminal-blast-replay` | 43 | 155 | none | 0 | 0 | 11 | — | 11 (all) |
| `timber-collapse-replay` | 40 | 1440 | orange `{120}`,`{960}`,`{1280}`; blue `{260}`,`{1150}` | 5 | 5 | 12 | 120 | 1 (`intact`, 0) |
| `rematch-draft-replay` | 41 | 5466 | blue `[3000,3365)`,`[3700,3970)`,`[4000,4420)`; orange `[4000,4420)`; eleven `connected_ice_input` windows | 3 | 141 | 3000 | 37 | 18 (through `timber-arena-load`, 2730) |

Cumulative jumps at every anchor, measured:

| profile | anchor tick → jumps taken so far |
|---|---|
| teal | 20 → 0 · 120 → 1 · 435 → 7 · 700 → 17 · 786 → 17 |
| radial | 1 → 0 · 180 → 1 · 360 → 2 · 540 → 3 · 840 → 5 · 908 → 6 · 909 → 6 · 938 → 6 |
| yellow | 1, 80, 81, 84, 89, 102, 109, 110, 111, 125, 155 → 0 at every anchor |
| timber | 1 → 0 · 828 → 2 · 864–912 → 2 · 1050 → 3 · 1140 → 3 · 1410 → 5 |
| rematch | 180–2730 → 0 at all eighteen · 3300 → 37 · 3653 → 38 · 3701 → 39 · 4449–4541 → 94 · 4603 → 96 · 4786 → 100 · 4969 → 107 · 5213 → 121 · 5310–5466 → 141 |

**Anchors that stay byte-identical if only jump physics changes: 32 of 73.** They are the eleven yellow anchors, teal `spawn`, radial `arena-reveal`, timber `intact` and the eighteen rematch anchors through `timber-arena-load` at 2730. **Forty-one need re-tuning and recapture.** Yellow is doubly safe: its script contains no jump input at all, and `PhysicsBoundary::new` gives its fighters `gravity_scale(0.0)`, so no vertical constant reaches them.

Two scoping facts change that arithmetic sharply.

* `rapier.gravity` is **global**. The seventeen timber members, the two hanging weights and the yellow crates are ordinary dynamic bodies at `gravity_scale` 1.0 (`insert_timber_structure` uses `linear_damping(1.1)`, `insert_yellow_crates` uses `linear_damping(0.36)`; neither sets a gravity scale). Changing `rapier.gravity` therefore moves the collapse pile and the crate blast as well as the fighters, which invalidates `dynamicBodyDigest` from the first tick and drops the preserved-anchor count from 32 to roughly 20 (teal `spawn`, radial `arena-reveal` and the eighteen pre-timber rematch anchors). A change confined to the player bodies — `gravity_scale` on the fighter, or a term inside `set_player_control` — leaves all of that alone.
* Rapier's `linear_damping(0.7)` on the fighter is not a vertical-only knob: ticket 049's measured air-speed asymptote of 192 px/s and the 110 u/s grounded walk both come out of it. Moving damping to carry the ascent/descent split would move the horizontal route of every profile as well.

### 3. Candidate mechanic shapes

**(a) Variable-height jump: cut vertical velocity on jump release.** Constants: a cut factor (the model above lands the ice hop near ×0.25) and a guard deciding whether the cut fires only while airborne or whenever `velocity.y > 0`. `JUMP_SPEED`, gravity and damping stay as they are. Supported by: `v_early` constant at 450–650 u/s across a 3.16× spread of rise heights, `corr(rise px, v_early) = +0.01`; ascent acceleration spread of 7.25× against a descent spread of 3.85×; the two tall arcs whose ascent equals their own descent; the ascent fitting a parabola worse than the descent in eight of nine paired arcs; and the shipped recurrence reproducing the ice hop's 82.5 px/13 t, `v0` 746.5, ascent 3100 as 86.58 px/13 t, `v0` 750, ascent 3046 with a release at ten ticks. Contradicted by: nothing measured. Effect on other routes: every jump that is held through its whole rise is unchanged, so teal's fourteen orange jumps inside `[330,670)` and the bulk of the rematch profile's 141 keep their arcs; the fourteen one-tick presses and the three one-tick ice rows become minimum-height hops, and each hold window's final jump is cut if the fighter is still rising when the hold ends.

**(b) A plain shorter, steeper jump: higher gravity while rising, lower while falling.** Constants: a rising-gravity multiplier near 2.0–2.2 to reach a 3000–3500 u/s² ascent, and either the current gravity or a slightly lower one while falling. Supported by: it reproduces the ice arcs and the median of the new sample. Contradicted by: it cannot produce a 151.5 px rise over 27 ticks or a 914 u/s² ascent, both measured on a static camera in this pass; it predicts a fixed ratio, whereas the measured ratio runs 0.69 to 11.24; it predicts a near-perfect parabola (0.06 px rms in the model) where the source shows 1.2–8.4 px; and it predicts `v_early` falling to 526–538 u/s where the source's tall and short jumps share the same 450–650 band. Effect on other routes: every one of the 169 jumps in the five profiles changes, and if it is implemented on `rapier.gravity` the timber pile and yellow crates change too.

**(c) Different gravity plus release.** Constants: both of the above. Supported by: nothing the current sample requires. The clone's shipped full jump is already inside the source's tall-arc family in height (122.85 px against 110.5–151.5), duration (23 t against 18–27) and early speed (600 against 450–650), so no constant change is needed to reach the source's ceiling, and the release rule alone reaches its floor. Contradicted by: it is the only option that changes both the tall arcs (which currently agree) and the short ones. Effect on other routes: strictly worse than either (a) or (b) — every existing arc changes, and the tall ones move away from the source rather than toward it.

### 4. Effort and risk for a human

* **Anchors to recapture and re-pair:** 41 of 73 in the worst case (option a or b, player-scoped), about 53 under a global-gravity change. The 32 preserved anchors are yellow's eleven in full, plus one each in teal, radial and timber, plus rematch's eighteen through 2730 — that is, the whole draft flow, both earlier eliminations, `half-blue`, `half-blue-tail` and the timber arena load survive untouched. Each recaptured anchor needs its source PTS and native RGBA hash re-paired in the matching observations document; those pairings already exist and do not move, so the work is re-rendering and re-inspecting, not re-decoding.
* **Input rows likely to need re-tuning:** seventeen one-tick jump presses (teal 3, radial 6, timber 5, rematch ice 3) plus the tail of each of the fifteen hold windows, so of the order of thirty jump-carrying rows, inside routes whose horizontal rows will also need nudging where a shorter jump changes a landing. The five scripts total roughly 130 input rows.
* **A measured phasing lever.** `grounded_windows.py` walked the public `grounded` flag for 48 ticks after every one-tick press (`out/ticket-050/grounded-windows.json`). `grounded` stays true for one to eighteen ticks *after* the press, so a press cannot simply be lengthened into a hold — the impulse re-applies, exactly as ticket 049 recorded for orange at 4711/4712. But `set_player_control` ignores `input.jump` entirely while airborne, so a press can be rewritten as `{T} ∪ [first airborne tick, …)` with a gap over the still-grounded ticks and **today's state hashes do not change at all**, while under a release rule that jump is then fully held and its arc does not change either. The room measured per press:

| profile | fighter | press | grounded ticks after press | airborne from | re-grounded | free hold room |
|---|---|---:|---:|---:|---:|---|
| teal | blue | 40 | 1 | 42 | 74 | 32 ticks — enough for the whole 23-tick rise |
| teal | blue | 500 | 1 | 502 | 534 | 32 ticks — enough |
| teal | blue | 650 | 1 | 652 | 681 | 29 ticks — enough |
| radial | orange | 160 / 340 / 520 / 820 / 888 | 2–3 | +3 to +4 | +4 to +5 | 1–2 ticks — not enough; these jumps barely leave the slope |
| radial | blue | 650 | 1 | 652 | 659 | 7 ticks — not enough |
| timber | orange | 120 | 1 | 122 | none within 48 | ≥46 ticks — enough |
| timber | orange | 960 | 0 | never airborne in 48 | — | the fighter never leaves contact; a cut may not fire at all |
| timber | orange | 1280 | 18 | 1299 | 1305 | 6 ticks — not enough |
| timber | blue | 260 | 1 | 262 | none within 48 | ≥46 ticks — enough |
| timber | blue | 1150 | 8 | 1159 | 1160 | 1 tick — not enough |
| rematch | orange | 4929 | 1 | 4931 | none within 48 | ≥46 ticks — enough |
| rematch | orange | 5211 | 1 | 5213 | 5218 | 5 ticks — not enough |
| rematch | blue | 5000 | 1 | 5002 | none within 48 | ≥46 ticks — enough |

  Six of the seventeen presses can be converted to a free full hold with a provably byte-identical result today. The other eleven belong to fighters who are in contact with geometry for all but one to six ticks after the press, so a release cut has almost nothing to cut there; how little is exactly what a throwaway probe would measure.
* **Is a phased path available?** Not one arena at a time — the change lives in one shared `set_player_control` and lands for all five profiles simultaneously, and an arena-specific special case is forbidden by ticket 049's Decisions. But a *two-ticket* path is available and is measured, not assumed: ticket A rewrites the seventeen presses as press-plus-airborne-hold rows, which is an input-only change with no state-hash movement at all and therefore no recapture; ticket B lands the release rule, at which point every long-hold jump keeps its arc and only the eleven contact-bound jumps and the hold tails move. That converts one risk-5 change into a risk-2 input edit followed by a much smaller physics change.
* **Residual risk.** A release rule does not unblock ticket 049 on its own. It relieves 049's bounds 1 and 2 (the 4689 wall take-off is unreachable because the clone rises too far; no arc clears both columns because the apex is too high), both of which are height problems a cut fixes. It does not touch 049's bound 3, the 211–238 px/s sustained horizontal that the 192 px/s air-speed asymptote cannot produce. 049 stays blocked on that third bound until horizontal control is examined separately.

### 5. Decision options for Adam

1. **Close 050 as won't-fix and leave the arc as it is.** Cost nothing, change nothing, keep all 73 anchors. Evidence for: the clone's full-height jump already matches the source's full-height jumps in height, duration, early speed and descent. Evidence against: the source's short hops are unreachable, and ticket 049 stays blocked on its first two bounds as well as its third.
2. **Ticket A only — rewrite the seventeen one-tick presses as press-plus-airborne-hold rows now, and defer the mechanic.** Cost: an input-only change with a measured zero state-hash delta, no recapture, no anchor movement, risk 1–2. Evidence for: `set_player_control` ignores `input.jump` while airborne, and the airborne windows are measured above. Evidence against: it delivers no fidelity by itself; it is preparation.
3. **Recommended: ticket A then ticket B — add a jump-release cut to `set_player_control` and change no constant.** Cost after ticket A: eleven contact-bound jumps plus fifteen hold tails move; recapture is bounded by which anchors those actually disturb rather than by all 41. Evidence for: `v_early` constant across a 3.16× rise spread with `corr = +0.01`; ascent spread 7.25× against descent spread 3.85×; two tall arcs with ascent ≈ descent; ascent fitting worse than descent in eight of nine paired arcs; and the shipped recurrence reproducing the ice hop to 4 px and 4 u/s with a release at ten ticks and a ×0.25 cut. Evidence against: the cut factor and the airborne-versus-rising guard are chosen from a model, not read off the footage; both need a probe.
4. **Land the release cut in one ticket with all five routes re-tuned and every affected anchor recaptured.** Cost: 41 anchors, of the order of thirty input rows, one large change. Evidence for: it is the only single-ticket path and it keeps the five profiles consistent at every commit. Evidence against: it is the risk-5 shape the ticket already carries, and option 3 reaches the same end state with the risk split.
5. **Change the ascent constant instead (option b), scoped to the player body.** Cost: 41 anchors, every one of the 169 jumps changes. Evidence for: it is a two-line change and it reproduces the ice arcs. Evidence against: four independent measurements contradict it — the 914 u/s² ascent, the 151.5 px rise, the 0.69–11.24 ratio spread, and the model's 0.06 px parabola against the source's 1.2–8.4 px.
6. **Change `rapier.gravity`.** Cost: about 53 anchors, including the timber collapse and the yellow crate blast, whose `dynamicBodyDigest` values ticket 040 and ticket 043 established. Evidence for: none found in this pass. Evidence against: the fighters are the only bodies whose arcs are wrong, and this is the only option that also moves the seventeen timber members, the two weights and the crates. Listed so it can be ruled out explicitly.
7. **Probe first, decide after.** Spend one throwaway branch measuring the release rule against the ice hop and against the eleven contact-bound jumps, retain the trace, and revisit. Evidence for: the cut factor and the guard are the only two unmeasured quantities left in option 3. Evidence against: the model already brackets the answer, so the probe mostly buys confidence rather than information.

### Still open, carried forward

* Ticket 049's unexplained level stretch — source blue's vertical velocity near zero for eleven frames, ticks 4726–4736, fitting 33 u/s upward with 214 u/s² against nothing beneath it — is untouched by this pass and is not explained by a release cut. A two-constant ascent/descent model will not reproduce it either.
* Whether the release cut fires only while airborne or whenever `velocity.y > 0` decides whether radial's six near-surface hops and timber's two contact-bound jumps change at all. It is measurable and is the first thing a probe should settle.
* Whether Rapier's linear damping or a term in `set_player_control` should carry any residual asymmetry is still open, and damping remains a horizontal knob as well as a vertical one.
* Establish the failing case at the public step/snapshot boundary before touching a constant, the way 049 does, and keep the arc claim falsifiable against the native frame hashes retained here and in `out/ticket-049/`.

### Retained artifacts

Under ignored `out/ticket-050/`: `decode_frames.py`, `track_stream.py`, `camera_track.py`, `landmark_camera.py`, `camera_compare.py`, `fit_corrected.py`, `find_jumps.py`, `split_fit.py`, `jump_table.py`, `clone_jump_model.py`, `anchor_jumps.py`, `grounded_windows.py`, `summarize_inspect.py`, `probe_colors.py`; the tracks `track-{teal,radial,timber,yellow}.json`; the camera records `camera-{teal,radial,timber,yellow}.json`, `camera-{teal,radial,timber,yellow}-landmark.json` and the `camera-ice-check.json` cross-check against ticket 049's static ice range; the fits `jumps-*-corrected.json`, `fits-*.txt`, `jump-table.json`, `jump-table.md`; the model `clone-jump-model.json` and `clone-jump-model.md`; the public-boundary counts `inspect-*.json`, `anchor-jumps.json`, `anchor-jumps.txt`, `grounded-windows.json`; and the inspected native frames under `frames/` and `probe/`.

## Work log

- 2026-09-07T10:36:00Z stage design start session claude:96849848-e6ec-488e-b4ac-b113acc49f8a/shape-049-opus — Splitting the arc-shape candidate out of ticket 049 so that ticket can stay an input-only correction.
- 2026-09-07T10:38:20Z stage design end session claude:96849848-e6ec-488e-b4ac-b113acc49f8a/shape-049-opus — Recorded the measured take-off, ascent and descent constants for both sides, the absence of any variable-height rule in the current simulation, and the preservation problem that one unit equals one pixel in all five arenas makes unavoidable. Left at risk 5 with an open mechanic question in Scratch; no product code, test or capture touched.
- 2026-09-07T11:15:30Z stage research start session claude:96849848-e6ec-488e-b4ac-b113acc49f8a/research-050-opus — Answering the Scratch question of whether the source arc is general: measure further jumps in the teal, radial, timber and yellow slices of both recordings, count the preservation cost through the prebuilt inspect boundary, and write a decision brief. No Cargo, no window, no product change.
- 2026-09-07T11:52:00Z stage research end session claude:96849848-e6ec-488e-b4ac-b113acc49f8a/research-050-opus — Measured twenty-three further source jumps plus one partial across four arenas, both fighters and both recordings, after building and cross-checking two camera estimators (the patch estimator reproduces ticket 049's static ice camera to 0.02 px rms; the teal and radial cameras are static and the naive patch fit there follows the animated backdrop, so those two intervals use an arena-landmark estimate instead). The short steep rise recurs — median 87.9 px in 15 ticks, median ascent 2822 u/s², median descent 1013 u/s², median ascent/descent 2.73 against the clone's 1.3 — but it is not one fixed jump: rise height spans 48.0–151.5 px, ascent acceleration spans 914–6622 u/s² while descent spans 360–1385, the ascent/descent ratio spans 0.69–11.24, the two tallest arcs have ascent equal to their own descent, and the four-tick take-off speed is 450–650 u/s regardless of rise height (`corr = +0.01`). That is a variable-height jump, not a fixed steep one. Also found that the shipped constants reproduce the ice hop exactly once a release cut exists (release at ten ticks, cut ×0.25 gives 86.58 px in 13 ticks fitting v0 750 and ascent 3046, against the measured 82.5 px in 13 ticks, v0 746.5, ascent 3100), so no constant needs to move. Counted the preservation cost at the public boundary: 32 of 73 anchors have no jump before them and stay byte-identical (all eleven yellow anchors, whose fighters also carry `gravity_scale(0.0)`, plus eighteen rematch anchors through tick 2730), 41 need recapture, and a `rapier.gravity` change would additionally move the timber pile and the yellow crates because both are ordinary dynamic bodies. Measured the public `grounded` flag after all seventeen one-tick presses and found a two-ticket phased path whose first half is provably free. Rewrote Scratch with the tables, decoder commands, frame hashes and seven numbered decision options; corrected nothing in Summary, Outcome, Decisions or Evidence, since ticket 049's three ice arcs and the "about 3.0" ratio they give are reproduced by this larger sample as a central value — what this pass adds is that the ratio is not constant. No product code, test, capture, README, architecture note or decision touched; no Cargo command run and no window opened.
