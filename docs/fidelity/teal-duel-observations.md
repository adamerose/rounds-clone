# Teal static duel observation

This record binds the first `S2-static-duel` slice to `reference/MedalTVRounds20260903170709695.mp4`, SHA-256 `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9`, from 00:22.50 through the last in-arena frame at 00:35.60.
The supplied video was read without modification; source crops and every clone render remain ignored under `out/ticket-039/`.

## Source observations

The arena fills a 1280×720 frame with ten narrow bright faces in two five-step groups and nine smaller brown-gray faces below them.
The upper faces are approximately 45–55 pixels wide and 22–26 pixels tall in the reviewed source samples.
Their centers alternate between roughly 20 and 130 pixels above the frame midpoint instead of forming one smooth arch.
The lower faces are approximately 45–55 by 25–35 pixels.
Every upper face casts a long navy column toward or beyond the bottom edge; the columns lean away from the center and widen or overlap irregularly.

Compressed pixel samples put the backdrop near RGB `(3,55,61)`, with neighboring mottled areas between `(2,48,56)` and `(16,81,76)`.
Bright platform samples range from cyan/green `(14,212,181)` through `(35,221,149)` to `(110,226,110)`.
Lower faces cluster around `(69,48,38)` to `(77,49,38)`, while the deepest shadow field is approximately `(1,17,54)`.
Those measurements guided the clone's dark teal base, alternating cyan/lime faces, muted lower blocks, tick-varying teal veil layers, and long navy shadow columns; they are observations from this compressed recording, not original game constants.

| Source anchor | Observed action | Clone anchor |
|---|---|---|
| 00:22.83 | The arena is established with orange and blue separated near the outer platforms. | `spawn`, tick 20 |
| 00:24.50 | Orange remains on the outer-left platform while blue has moved to the upper-right tier. | `asymmetric-traversal`, tick 120 |
| 00:29.75 | Gunfire crosses the middle tiers as orange begins its delayed inward and upward route. | `shot`, tick 435 |
| 00:34.16 | The fighters remain separated on the right half as their routes converge. The clone anchor exposes its modeled block and reflection, neither of which is visible in this exact source frame. | `block-reflection`, tick 700 |
| 00:35.60 | Both fighters converge at the upper right in the terminal white impact burst. | `terminal-impact`, tick 786 |

These observations come from frames decoded directly from the source video with FFmpeg, including an additional 00:35.70 frame that confirms the result transition starts outside the selected interval.
The clone maps its 60 Hz replay to the selected interval and does not claim frame-exact recovery of the card-modified source inputs.

## Derived frame identity

All five frames are 1280×720 PNGs rendered by `bevy-0.19.1-2d-offscreen` with seed 38 and input-trace SHA-256 `447242f70b01d86f2b30606cdaaebec8f9f49f8638baccf514a4c4e4ada40b62`.
The checked command records the executable and full metadata bundle under ignored `out/ticket-039/corrected-anchors.json`.

| Anchor | Source timestamp and SHA-256 | Tick | State SHA-256 | Frame SHA-256 |
|---|---|---:|---|---|
| spawn | 00:22.83 · `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9` | 20 | `062d3968f6639a2b0416f65b250c873deac314d991a5ae32a784b5f3d2511f6b` | `481132e47a59208973e35278b1b85c60d52aef36099ac9c517de8ce19497559e` |
| asymmetric-traversal | 00:24.50 · `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9` | 120 | `74b83cb16d1b3e2a35bcacbeb4e5e0b645d58c09faad4664eb72dad000c3f0d6` | `689f9ff359552ccf03b42ed6e451ada70c954ecffe1eafa6994937538c2d7992` |
| shot | 00:29.75 · `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9` | 435 | `abfbe5f47521b0adfeef9e86eadbcbf973561a4101cc293082a8666555ecabd6` | `7f5c8f47dc436085b1cf8a9e8f4a621e4f46a88731fd35654241444c4b042430` |
| block-reflection | 00:34.16 · `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9` | 700 | `2867579215680a7f73f5e430115f2cee6637001e69699a9f75f9795e05984410` | `e7e787648cdb204b326a8daaf73fd0b73cb7a56de7317fa5053ef43b478e5925` |
| terminal-impact | 00:35.60 · `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9` | 786 | `ccc3a488dfe11b8e718677cad19044332b8b386063320988461274584a95b7a3` | `f323d45f195596038abd55e980444c9fcdb549b1da8885103fa9231203f705b1` |

## Visual review

The source-and-clone sheet at ignored `out/ticket-039/source-clone-contact-corrected.png` was inspected at original resolution.
Both sides share the ten-face stepped silhouette, nine lower faces, cyan/lime and brown-gray palette, separated outer spawn, orange's 00:24.50 outer-left position, delayed inward traversal, right-side convergence, projectile exchange, and white terminal hit flash.
The clone's 00:34.16 anchor shows its modeled block ring and reflected projectile, while the exact source frame shows only the two separated fighters converging on the right; the contact sheet does not present that modeled event as a frame-exact source match.
The clone ends at the 00:35.60 impact with both fighters in the upper-right composition, records zero ring-outs, and leaves the result transition to `S6-match`.

Remaining differences are visible and bounded.
The source faces are narrower and bloom softly, while the clone faces are wider and hard-edged.
Source shadows taper and merge organically; the clone uses straight columns.
The source backdrop has fine smoke/noise and slow organic motion, while the clone uses broad deterministic moving veils.
Source fighters have more articulated bodies, denser name/health detail, particles, camera movement, and source HUD elements.
The clone retains the observed circular silhouette, limbs, directional gun, health/name treatment, block, flash, trail, and restrained snapshot-driven camera nudge, but does not claim the unresolved advanced `S7-presentation` work.
At 00:29.75 clone orange is farther inward and higher than source orange, which still occupies the outer-left area.
At 00:34.16 the clone fighters overlap and show block/reflection feedback, while the source fighters remain separated; only their right-side convergence corresponds at that timestamp.
The source terminal burst has richer particles, and its character art and camera response remain more expressive.
The broad action route and event order correspond, while intermediate timing and exact poses remain tuned approximations because the recording follows card picks whose numeric effects are not recoverable from this interval alone.

## Verification and proportionality

The implementation keeps physics, rendering, transport, process orchestration, and evidence ownership in their existing crates rather than adding a parallel harness.
The simulation tests protect stable contact plus the asymmetric route, the complete reflected-shot-to-terminal-impact replay, separate ring-out capability, one-tick bullet CCD, and bounded public JSON.
The network test protects two live input sequences and every progressive snapshot.
The presentation test executes the actual Bevy offscreen renderer and verifies the PNG dimensions.
Three client process tests protect single-capture, replay-capture, and remote-render destination safety, including validation before the remote network request, and the automation child test protects cleanup after a partial launch failure.
The workspace has twelve focused executed test cases: two automation, three client, one network, one presentation, and five simulation tests; no test-only physics, rendering, or network implementation was added.

### Line and responsibility inventory

Counts below are LF source lines after formatting.

| File | Lines | Responsibility and proportional test ownership |
|---|---:|---|
| `crates/rounds-sim/src/lib.rs` | 878 | Authoritative ECS/Rapier rules, snapshots, arena, replay profile; lines 789–878 contain route/contact, terminal-impact replay, separate ring-out, real Rapier CCD, and bounded-JSON tests. |
| `crates/rounds-network/src/lib.rs` | 387 | Private packet grammar, handshake barrier, sequenced input and progressive snapshot transport; lines 355–387 run two real UDP clients against one authority. |
| `crates/rounds-presentation/src/lib.rs` | 565 | Shared visible/offscreen Bevy scene, event-driven bounded capture, common camera transform, and exact hidden monitor selection; lines 543–565 execute the GPU renderer and inspect the PNG dimensions. |
| `crates/rounds-client/src/main.rs` | 358 | Local, live remote, visible replay, single capture, named anchor capture, and pairwise destination validation; no private unit-test mirror. |
| `crates/rounds-client/tests/capture_cli.rs` | 99 | Three process-boundary regressions prove resolved-equivalent single capture, replay anchor/metadata, and remote image/metadata destinations are rejected before rendering, networking, or writing. |
| `crates/rounds-server/src/main.rs` | 41 | Thin headless authority process; protected through network and three-process smoke evidence rather than duplicate tests. |
| `crates/rounds-automation/src/main.rs` | 416 | Owns three-process lifecycle, agreement checks, and live-render binding; lines 345–416 prove a partial client-start failure releases the owned server, with one inert helper test entry point. |

The larger simulation and renderer files contain the shipped product implementation, not test scaffolding.
Automation remains a single process runner around public executables, and its only support-only mechanism is the 22-line `ChildGuard` used by both the real smoke and cleanup regression.

## Completed evidence

Before the correction's first Cargo command, the exact resolved `CARGO_TARGET_DIR` `out/ticket-039/cold-first-target-20260904-correction` was deleted and checked as neither a directory nor a file.
`cargo test --workspace --locked -- --nocapture` was then the first Cargo command and passed from that verified absent target in 3 minutes 24 seconds: 12 passed, 0 failed, 0 ignored.
After the final correction, the following commands passed against the same isolated build graph:

- `cargo fmt --all -- --check` — exit 0.
- `cargo clippy --workspace --all-targets --locked -- -D warnings` — exit 0 in 1 minute 17 seconds.
- `cargo build --workspace --locked` — exit 0 in 22 seconds.
- `cargo test --workspace --locked -- --nocapture` — exit 0 in 40 seconds; 12 passed, 0 failed, 0 ignored across unit, integration, renderer, and documentation targets.
- `out\\ticket-039\\cold-first-target-20260904-correction\\debug\\rounds-automation.exe smoke --seed 38 --ticks 786 --output-dir out/ticket-039/corrected-smoke` — exit 0; two handshakes completed, both clients sent sequences 0–785 and received ticks 1–786, both clients and the local authority agreed on state SHA-256 `ccc3a488dfe11b8e718677cad19044332b8b386063320988461274584a95b7a3`, and the frame rendered from client 0's received snapshot agreed with that state.
- `out\\ticket-039\\cold-first-target-20260904-correction\\debug\\rounds-automation.exe inspect --seed 38 --ticks 786` — exit 0; the bounded final state reports 1,006 platform-contact ticks, 17 jumps, five shots and recoil impulses, seven block activations, one reflection, one CCD contact, one damage-scaled hit, zero ring-outs, both fighters at the upper right, and winner 0.
- `out\\ticket-039\\cold-first-target-20260904-correction\\debug\\rounds-client.exe capture-replay --seed 38 --ticks 786 --output-dir out/ticket-039/corrected-anchors --metadata out/ticket-039/corrected-anchors.json` — exit 0; the five renderer/state hashes above were reproduced exactly.
- `out\\ticket-039\\cold-first-target-20260904-correction\\debug\\rounds-client.exe visible --seed 38 --ticks 786 --frames 180` — exit 0; while hidden, the guard observed exactly one display at `(364,-1080)`, 1920×1080, re-verified that identity before showing, and completed all 786 ticks with the same final state hash as the smoke run.
- `cargo tree -e features -i bevy_rapier2d` — exit 0; only the workspace `dim2` and `headless` feature requests are active, with no `enhanced-determinism` feature.
- `node C:\\Users\\Adam\\.codex\\worktrees\\3d6a\\ivy\\playbook\\checks\\scripts\\check-tickets.mjs .` — exit 0; ticket format check passed. The ticket's documented repository-local checker path is absent in this checkout, so the installed Ivy checker was used.
- `git diff --check` — exit 0.

The visible replay and process smoke both exited on their own. A final process query found no `rounds-*` process, so neither the verified monitor-4 window nor a UDP authority/client remained running.
