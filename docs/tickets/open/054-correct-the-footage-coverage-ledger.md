---
format: 3
status: idea
created: 2026-09-07T17:39:20Z
origin: system-detected
tags: ["documentation", "fidelity", "coverage"]
value: 4
risk: 1
sessions:
  - claude:96849848-e6ec-488e-b4ac-b113acc49f8a
execution: unattended
depends-on: []
supersedes: []
split-from: []
---

# Correct the footage coverage ledger

`GOAL.md` gives `docs/fidelity/footage-coverage.md` one job: keep every visible arena, mechanic, card interaction, match-flow state and presentation effect linked either to implemented behaviour or to a named gap. Five rows currently fail that. The 07:00–07:10 yellow-crate row claims `S5` and `S8` are implemented in a slice that has neither a card formula nor production online play; the 03:50–04:00 ice row calls its route implemented without naming the traversal residual that keeps two tickets blocked; the `S7` row omits the two presentation sub-slices tickets 042 and 043 delivered; the `S6` row calls match completion reproduced when the 4–5 score it shows is constructed prior state; and two frames the survey decoded in recording `1460e670…15f9` — the observed match end and the following new-match reset — are not recorded anywhere.

A ledger that overstates coverage is worse than one with gaps in it, because the gaps are what the next ticket is chosen from. This is a documentation-only correction: no product code, test, profile or capture changes.

## Outcome

- The 07:00–07:10 row of recording `453954a7…a18c` no longer claims `S5` or `S8` as implemented. Ticket 043 delivered two development-transport UDP client processes on the localhost adapter and no card formula at all: the `yellow-crate-terminal-blast-replay` profile reports `flow: null` and `flowDigest: null` from `rounds-automation inspect`, so it carries no item, loadout or capability state whatever. The row's implemented set becomes `S2/S4/S6/S7`, with production online and card behaviour named as the unresolved remainder alongside the other yellow-arena combat.
- The 03:50–04:00 row of recording `453954a7…a18c` names the ice traversal gap rather than describing the route as implemented without qualification: at the bound 4786 anchor the clone's blue stands about 92 native pixels right and 37 pixels below the source's blue, because the clone's airborne horizontal speed converges on 192.0 px/s against the faster sustained travel the source's hop needs — recorded by ticket 049 as a 211–238 px/s band and under active refinement in ticket 052. Both 049 and 052 are `blocked`, and the row says so.
- The `S7-presentation` slice row lists the two presentation sub-slices already delivered but unnamed there: the radial-saw scene from ticket 042 (rotated platform and saw poses read from the snapshot, tick-derived paper-brush motion, long shadows, result dimming, half-score circles, `HALF BLUE`) and the yellow-crate scene from ticket 043, including its one private final-composite fullscreen pass for the source-proved discrete radial RGB echoes.
- The `S6-match` slice row states that match completion is constructed prior state, not reproduced behaviour: `FlowAuthority::new` hard-codes `scores: [4, 5]` with `winner: Some(1)` and `eliminated: Some(0)`, so the terminal 4–5 result the rematch route displays is where the authority starts rather than something any score drove it to. No score-driven match award exists anywhere in the flow: `record_elimination` awards halves and completed rounds only, and no phase or rule ends a match on a completed-round count.
- The 03:20–03:30 row of recording `1460e670…15f9` records both observed match-lifecycle frames with their native identities: the match end at PTS 2000158666, where `WAITING` stands over the still-live arena with both fighters visible and the HUD showing three filled orange pips against five filled blue pips; and the new-match reset at PTS 2039991840, where both five-pip rows are empty, no loadout badges remain at the upper right, and a fresh orange five-card draft fan is mid-reveal. This is the only place in either recording where a match ends on a score and a new one begins, so the row is where `S6`'s missing match award and `S1`'s new-match draft are anchored.
- Every other row of both recording tables, the slice table's other eight rows, the prose paragraphs and the review-discipline section are unchanged, and the diff shows exactly the rows named above.

## Decisions

- Ledger rows link visible content to implemented behaviour or to a named gap, per `GOAL.md`. A row may not describe a slice as implemented on the strength of a neighbouring slice's delivery, and a named gap must carry enough of a handle — a number, an anchor, a ticket — for the next ticket to be chosen from it.
- No product code, test, replay profile, anchor, capture path or fidelity claim changes. Nothing here re-opens or amends closed tickets 042, 043, 045 or 046; it corrects how their delivered scope is described.
- Both bound frames of recording `1460e670…15f9` fall inside the 03:20–03:30 row: PTS 2000158666 is 200.0158666 s and PTS 2039991840 is 203.9991840 s. The 03:10–03:20 row therefore needs no change and keeps its existing pink-fortress content and frame identity. Naming the containing row rather than the surrounding block keeps the ledger's half-open interval convention intact.
- The two frames were decoded and hashed for this contract through the retained imageio-ffmpeg FFmpeg 7.1 pipeline, with `-copyts`, `-fps_mode passthrough` and `select=eq(pts,N)`, over exactly 3,686,400 native 1280×720 packed RGBA bytes. PTS 2000158666 hashes to `c4c9547151263157cf54afe9495c7f1b1103cc3c2da8d3b29314bb0c57a09a6d`, which is the value already in the ledger's 03:20–03:30 row and needs no correction; PTS 2039991840 hashes to `7f8703810079f5beef741953d896175d70ee5602fe997b37be3349308c218da0` and is new to the ledger. The implementer re-decodes and re-hashes both rather than copying these values across.
- The 07:00–07:10 row keeps `S6` in its implemented set: ticket 043 does own an authority-driven blue elimination, orange score and `ROUND ORANGE` result in that interval. Only the `S5` and `S8` claims are wrong.
- The airborne-speed figure quoted in the 03:50–04:00 row is attributed to ticket 049 rather than asserted as settled, because ticket 052 is actively remeasuring the source band. The clone-side ceiling of 192.0 px/s is the fixed point of the shipped `set_player_control` and Rapier damping pair and is stable; the source-side band is not, and the ledger must not freeze a number two blocked tickets are still moving.
- Cargo must use `.cargo/config.toml`: at most two concurrent jobs, the reusable ignored target at `out/cargo-target`, no upward job-cap override and no concurrent builds. No build is expected for a documentation-only change; if one is run for the `inspect` evidence below, reuse that target. Every visible window must open on monitor 4, the 1920×1080 display at zero-based screen index 3, launched hidden or minimized and verified before being shown; this ticket is not expected to open one.

## Evidence required

- Re-decode PTS 2000158666 and PTS 2039991840 from `reference/MedalTVRounds20260903170709695.mp4`, SHA-256 `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9`, retain the exact decoder command arrays and both native RGBA SHA-256 values, and inspect both frames at 1280×720 to confirm the pip counts, `WAITING` over the live arena, the emptied pip rows, the absent badge stacks and the fresh orange fan before writing them into the ledger.
- Confirm the `S5`/`S8` correction against the shipped executable rather than against ticket 043's prose: `rounds-automation inspect --profile yellow-crate-terminal-blast-replay --seed 43 --ticks 155` must report `flow: null` and `flowDigest: null`. Retain the output.
- Confirm the `S6` correction against the source: quote the hard-coded `scores: [4, 5]`, `winner` and `eliminated` values in `FlowAuthority::new` in `crates/rounds-sim/src/flow.rs`, and confirm that no code path awards a match on a completed-round count.
- Run `git diff --check` and review `git diff` on `docs/fidelity/footage-coverage.md`, confirming that the only changed lines are the two 453954a7 rows, the two slice rows and the one 1460e670 row named in the Outcome, and that no other row, paragraph or table gained or lost a character.
- Run the repository's ticket validation over both new ticket files.

## Work log

- 2026-09-07T17:39:20Z stage design start session claude:96849848-e6ec-488e-b4ac-b113acc49f8a/shape-053-opus — Shaping the ledger correction alongside ticket 053, after decoding and hashing both recording `1460e670…15f9` frames and confirming the yellow profile carries no flow state.
- 2026-09-07T17:46:30Z stage design end session claude:96849848-e6ec-488e-b4ac-b113acc49f8a/shape-053-opus — Five ledger corrections bound to checked evidence: the yellow profile's null flow state, the hard-coded `[4, 5]` prior score, the 4786 traversal residual with 049/052 both blocked, the two 042/043 presentation sub-slices, and both re-decoded `1460e670…15f9` frame identities. Both bound PTS fall inside the 03:20–03:30 row, so the contract names that row rather than the 03:10–03:30 block. No product or existing documentation changed.
