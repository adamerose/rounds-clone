# Teal static duel observation

This record binds the first `S2-static-duel` slice to `reference/MedalTVRounds20260903170709695.mp4`, SHA-256 `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9`, from 00:22.50 through 00:35.50.
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
| 00:24.50 | Both fighters leave their initial level and traverse upward/inward across the stepped faces. | `traversal`, tick 120 |
| 00:27.66 | Gunfire crosses the middle tiers while a circular block response is visible. | `shot-block`, tick 310 |
| 00:34.00 | The exchange converges on the right half with a visible impact and strong displacement. | `hit-knockback`, tick 690 |
| 00:35.48 | The losing fighter exits the playable composition at the end of the duel; the separate result treatment begins outside this slice. | `round-end`, tick 779 |

The source timestamps are the nearest reviewed contact-sheet samples to the named replay events.
The clone maps its 60 Hz replay to the selected interval and does not claim frame-exact recovery of the card-modified source inputs.

## Derived frame identity

All five frames are 1280×720 PNGs rendered by `bevy-0.19.1-2d-offscreen` with seed 38 and input-trace SHA-256 `1360ea1a9efc3c6e7196c1e05fe6f65251f78416bc410d232240c4585e9eac1f`.
The checked command records the executable and full metadata bundle under ignored `out/ticket-039/evidence-anchors.json`.

| Anchor | Source timestamp and SHA-256 | Tick | State SHA-256 | Frame SHA-256 |
|---|---|---:|---|---|
| spawn | 00:22.83 · `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9` | 20 | `062d3968f6639a2b0416f65b250c873deac314d991a5ae32a784b5f3d2511f6b` | `358f9c947ef8d01b55b8c0028e0787b9d9eb8e407a2631a4d24b33ae1693d31e` |
| traversal | 00:24.50 · `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9` | 120 | `13067d040fef2913f20b80363780d34fde64f8e4d2cd2234e3a54ca792a6989d` | `07e6595ae7ddbe06cb65c897b07ee6e1c864edf2a7452968b9a19dd551f24300` |
| shot-block | 00:27.66 · `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9` | 310 | `684ade6db838d5bdffeedb06cd5ca6de49d2f47f3d0855094cec8974e7f24c85` | `4a374433909ee460517197aa20984dd8fb2ee8a3ee949f09f33599d58933ca4c` |
| hit-knockback | 00:34.00 · `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9` | 690 | `e83a34407c7ce3562f8a6b24466be5c97be69731df901582bf70d557d329deac` | `8b5bbc304f01678a6977d672d09f652831f47f5764c0b14281ae436c2f53311f` |
| round-end | 00:35.48 · `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9` | 779 | `b9b796f5cf90656c15ae45ba1b43a5042b583f481ca67723156b30983f37bfe9` | `43ff6c2a225bd1050a8ac6df7ddcc11a61f28da3adaf5b8f91bad50a9ac2963a` |

## Visual review

The source-and-clone sheet at ignored `out/ticket-039/source-clone-contact-final.png` was inspected at original resolution.
Both sides now share the ten-face stepped silhouette, nine lower faces, cyan/lime and brown-gray palette, separated outer spawn, inward upper-tier traversal, long center-facing dark gaps, block ring, projectile trail, white hit flash, and right-side knockback/ring-out sequence.
The clone deliberately leaves the 00:35.50 result overlay to `S6-match` rather than inventing result text inside this duel.

Remaining differences are visible and bounded.
The source faces are narrower and bloom softly, while the clone faces are wider and hard-edged.
Source shadows taper and merge organically; the clone uses straight columns.
The source backdrop has fine smoke/noise and slow organic motion, while the clone uses broad deterministic moving veils.
Source fighters have more articulated bodies, denser name/health detail, particles, camera movement, and source HUD elements.
The clone retains the observed circular silhouette, limbs, directional gun, health/name treatment, block, flash, trail, and restrained snapshot-driven camera nudge, but does not claim the unresolved advanced `S7-presentation` work.
The action route and event order correspond, while timing and exact poses remain tuned approximations because the recording follows card picks whose numeric effects are not recoverable from this interval alone.

## Verification and proportionality

The implementation keeps physics, rendering, transport, process orchestration, and evidence ownership in their existing crates rather than adding a parallel harness.
The simulation tests protect stable contact plus the two-jump route, the complete reflected-shot-to-ring-out replay, one-tick bullet CCD, and bounded public JSON.
The network test protects two live input sequences and every progressive snapshot.
The presentation test executes the actual Bevy offscreen renderer and verifies the PNG dimensions.
The client alias test protects capture destination safety, and the automation child test protects cleanup after a partial launch failure.
This is seven focused product-boundary tests plus one bounded-state check and the automation test's inert UDP helper entry point; all nine executed test cases pass, and no test-only physics, rendering, or network implementation was added.

### Line and responsibility inventory

Counts below are LF source lines after formatting.

| File | Lines | Responsibility and proportional test ownership |
|---|---:|---|
| `crates/rounds-sim/src/lib.rs` | 840 | Authoritative ECS/Rapier rules, snapshots, arena, replay profile; lines 771–840 contain route/contact, complete replay, real Rapier CCD, and bounded-JSON tests. |
| `crates/rounds-network/src/lib.rs` | 387 | Private packet grammar, handshake barrier, sequenced input and progressive snapshot transport; lines 355–387 run two real UDP clients against one authority. |
| `crates/rounds-presentation/src/lib.rs` | 481 | Shared visible/offscreen Bevy scene, hidden monitor selection, renderer capture; lines 460–481 execute the GPU renderer and inspect the PNG dimensions. |
| `crates/rounds-client/src/main.rs` | 332 | Local, live remote, visible replay, single capture, named anchor capture, and metadata entry points; no private unit-test mirror. |
| `crates/rounds-client/tests/capture_cli.rs` | 37 | One process-boundary regression proving equivalent image/metadata destinations are rejected before writing. |
| `crates/rounds-server/src/main.rs` | 41 | Thin headless authority process; protected through network and three-process smoke evidence rather than duplicate tests. |
| `crates/rounds-automation/src/main.rs` | 416 | Owns three-process lifecycle, agreement checks, and live-render binding; lines 345–416 prove a partial client-start failure releases the owned server, with one inert helper test entry point. |

The larger simulation and renderer files contain the shipped product implementation, not test scaffolding.
Automation remains a single process runner around public executables, and its only support-only mechanism is the 22-line `ChildGuard` used by both the real smoke and cleanup regression.

## Completed evidence

Verification started after deleting the exact resolved worktree `target` directory and confirming it no longer existed. The following commands then passed against the candidate:

- `cargo fmt --all -- --check` — exit 0.
- `cargo clippy --workspace --all-targets --locked -- -D warnings` — exit 0 after rebuilding the absent target graph.
- `cargo build --workspace --locked` — exit 0; the clean development build completed in 2 minutes 36 seconds.
- `cargo test --workspace --locked -- --nocapture` — exit 0; 9 passed, 0 failed, 0 ignored across unit, integration, renderer, and documentation targets.
- `target\\debug\\rounds-automation.exe smoke --seed 38 --ticks 780 --output-dir out/ticket-039/final-smoke` — exit 0; two handshakes completed, both clients sent sequences 0–779 and received ticks 1–780, both clients and the local authority agreed on state SHA-256 `9ecf33a52f1e8b57937e066b18092606a7766533ae131e09bff5098968d050bb`, and the frame rendered from client 0's received snapshot agreed with that state.
- `target\\debug\\rounds-client.exe capture-replay --seed 38 --ticks 780 --output-dir out/ticket-039/evidence-anchors --metadata out/ticket-039/evidence-anchors.json` — exit 0; the five renderer/state hashes above were reproduced exactly.
- `target\\debug\\rounds-client.exe visible --seed 38 --ticks 780 --frames 180` — exit 0; the hidden-first guard selected and re-verified monitor index 3 at `(364,-1080)`, 1920×1080 before showing the replay, then completed all 780 ticks with the same final state hash as the smoke run.
- `cargo tree -e features -i bevy_rapier2d` — exit 0; only the workspace `dim2` and `headless` feature requests are active, with no `enhanced-determinism` feature.
- `node C:\\Users\\Adam\\.codex\\worktrees\\3d6a\\ivy\\playbook\\checks\\scripts\\check-tickets.mjs .` — exit 0; ticket format check passed. The ticket's documented repository-local checker path is absent in this checkout, so the installed Ivy checker was used.
- `git diff --check` — exit 0.

The visible replay and process smoke both exited on their own. A final process query found no `rounds-*` process, so neither the verified monitor-4 window nor a UDP authority/client remained running.
