---
format: 3
status: idea
created: 2026-09-06T01:26:15Z
origin: system-detected
tags: ["product-fidelity", "movement", "bevy"]
value: 7
risk: 5
sessions:
  - codex:01a073e9-17ec-7170-933a-0e18a071972d
execution: unattended
depends-on: [46]
supersedes: []
split-from: []
---

# Reproduce the short hop at the ice platform edge

The connected ice duel traverses the arena, but blue's early crossing misses the source position after the upper-right platform. Establish the short hop visible at that platform's right edge and reproduce the resulting crossing through ordinary authoritative input, so movement calibration follows observed contact behavior.

## Outcome

- Blue's passage beside the upper-right platform and underneath the center follows the source's visible route without an unexplained extra stop on the lower-right column.
- The correction comes from demonstrated input, contour or general movement/contact behavior. No arena-specific movement constant, scripted pose, forced velocity at a source tick or new replay profile substitutes for playable movement.
- The earlier connected fights, current either-color scoring, later ice projectile exchange and terminal result remain supported by real input and source evidence.

## Decisions

- Source recording is `reference/MedalTVRounds20260903165304088.mp4`, SHA-256 `453954a7230401ed805be4e53dec41779a1913dfd69903671fc131fca2c8a18c`. Identity is native 1280×720 RGBA, exactly 3,686,400 decoded bytes, with preserved integer PTS.
- Source movement starts near the existing ice combat boundary: blue remains camera-relative stationary through PTS 2361823886, then clearly moves by 2362157218/hash `7706066d9d3d219e4f92fdc7d00f58bf8571918145916509d89ed59261ab0342`. The observed first upward departure is PTS 2363323880/hash `0006cb0a337058096d5edfb2b3e82cd445fbd43607859eb84cbc73db10fbeef8`.
- At PTS 2376490494/hash `6c2f1b4e8d4f08b56d40005f29c419abf5be4cbfc8a447003711e05211f44cb3`, blue touches the right platform edge. PTS 2377323824/hash `348f3250d2ce369d981b8b26821ba536648a44aac1fe0bc5889e3658dd49d435` shows an upward departure there. PTS 2379657148/hash `2b130d3d9ef041ea149ce3e6c0f914a6d11cee80ee90840e26cf8c7e9066e3fb` shows the short airborne passage. These frames do not reveal the held controls or prove a particular wall-jump or variable-height-jump rule.
- The early traversal anchor remains PTS 2392823762/hash `3d2ae2939f40a3a8d53191589ef5c42ab360a7242ce65d9590685069536b5ba4`. Source blue is approximately image (643,367); the current shared-GPU candidate puts blue near (735,404), a measured 92-pixel horizontal and 37-pixel vertical difference. Orange is close: source (255,359), clone (250,362).
- The upper platform's source outline is approximately x878–963/y271–320; the current collision outline x878–964/y272–321 differs by about one pixel. That measurement does not establish a contour cause. The same drafted fighters continue across arenas, so a general movement change requires preservation analysis for earlier fights.
- Ticket 046's frozen contract and final independent assessment remain separate. Creating this idea neither waives its early-traversal requirement nor declares its candidate approved.

## Evidence required

- Reproduce the source identities and inspect the bounded contact sequence. Retained manifests are `out/ticket-046/blue-onset-adjacent.json`, `blue-jump-adjacent.json`, `blue-middle-route.json`, `blue-base-return.json` and `upper-contact.json`; the last includes the exact ten-frame decoder command. Preserve native pairs, using crops only as supporting inspection.
- Reproduce the current public route with `rounds-client capture --profile rematch-draft-replay --seed 41 --ticks 4786 --output <png> --metadata <json>` and the earlier mechanism at tick 4707. The original paired artifacts are `upper-contact-clone-4786.png`/`overview-2392823762.png` and `upper-contact-clone-4707.png`/`blue-middle-route-2379657148.png`.
- Diagnose the contact/input difference before admitting a mechanic. The current source-timed inputs produce an upper-platform landing, friction reduces horizontal speed to about 110 world units per second, and blue subsequently contacts the lower-right column. A full jump from the platform instead reaches the upper column. `contact-route-probe.rs`, `upper-platform-contact-trace.txt` and `source-*-trial.txt` retain these public-input observations; they do not prove a physics constant.
- A minimal regression must fail for the diagnosed cause and pass through the ordinary authority/input boundary after correction. Compare the corrected native trajectory, later 4969/5213 anchors, real 5312 terminal shot and adjacent 5338/5339 result boundary. Rerun all affected existing replay checks and source-paired states if general movement changes.

## Scratch

No wall-jump, tap-jump, gravity or friction formula is admitted. The observed hop is shorter than the current full jump at the top of the platform, but original control timing and the effect of side contact remain unresolved. Risk stays 5 until a bounded cause and preservation scope are established.

## Work log

- 2026-09-06T01:26:15Z stage design start session codex:01a073e9-17ec-7170-933a-0e18a071972d/01a073ea-e6fa-7bd2-9ff0-85f9201f2b93 — Searched the queue and decisions, confirmed number 49 across worktrees with the release-matched helper, and captured the observed short-hop gap without admitting a speculative movement rule.
- 2026-09-06T01:28:16Z stage design end session codex:01a073e9-17ec-7170-933a-0e18a071972d/01a073ea-e6fa-7bd2-9ff0-85f9201f2b93 — Idea records the native source identities, current public reproduction and unresolved contact/input cause; release-matched ticket validation passed, with no mechanic admitted.
