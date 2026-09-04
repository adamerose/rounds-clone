# ROUNDS clean-room rewrite

This repository starts from the two supplied ten-minute ROUNDS recordings and builds the clone in Rust with Bevy.
Four footage-derived slices now cover a teal duel, an explosive timber collapse, the blue 4–5 victory through rematch and two-player card draft into upgraded combat, and the yellow-crate terminal blast through `ROUND ORANGE`.
Two clients drive one 60 Hz Rapier authority through sequenced local UDP input, the same rules run as a client-host, and the shared Bevy 2D scene renders visibly or to offscreen evidence frames.

## Build and verify

```powershell
cargo fmt --all -- --check
cargo clippy --workspace --all-targets --locked -- -D warnings
cargo build --workspace --locked
cargo test --workspace --locked
.\target\debug\rounds-automation.exe smoke --seed 38 --ticks 786 --output-dir out\ticket-039\smoke
.\target\debug\rounds-automation.exe inspect --seed 38 --ticks 786
.\target\debug\rounds-client.exe capture-replay --seed 38 --ticks 786 --output-dir out\ticket-039\anchors --metadata out\ticket-039\anchors.json
.\target\debug\rounds-client.exe visible --seed 38 --ticks 786 --frames 180
.\target\debug\rounds-automation.exe smoke --profile rematch-draft-replay --seed 41 --ticks 2400 --output-dir out\ticket-041\smoke
.\target\debug\rounds-client.exe capture-replay --profile rematch-draft-replay --seed 41 --ticks 2400 --output-dir out\ticket-041\anchors --metadata out\ticket-041\anchors.json
.\target\debug\rounds-client.exe visible-flow --profile rematch-draft-replay --seed 41 --ticks 2400 --automated
.\target\debug\rounds-automation.exe smoke --profile yellow-crate-terminal-blast-replay --seed 43 --ticks 155 --output-dir out\ticket-043\smoke
.\target\debug\rounds-client.exe capture-replay --profile yellow-crate-terminal-blast-replay --seed 43 --ticks 155 --output-dir out\ticket-043\anchors --metadata out\ticket-043\anchors.json
.\target\debug\rounds-client.exe visible --profile yellow-crate-terminal-blast-replay --seed 43 --ticks 155 --frames 155
```

The smoke result proves that both client processes handshake, send monotonic input sequences, receive every progressive snapshot, agree with the authority and local host, and bind one real Bevy render to the received final state.
The rematch replay capture emits thirteen named 1280×720 anchors whose metadata identifies the source recording and timestamp, replay input, state, renderer, executable, and frame.

## What is and is not implemented

The rematch slice adds an authoritative blue winner and orange elimination, the exact prior-card badges, explicit accepted-rematch reset, phase revisions, per-player votes, seeded five-card offers, active-player validation, typed persistent loadouts, Dazzle stun pulses, Explosive Bullet impacts, item-specific card art and pose response, and a source-timed return to combat.
Seven distinct unselected definitions are intentionally catalog-only and cannot be confirmed; they remain visible fidelity targets rather than inert fake upgrades.
The teal slice has stable Bevy ECS identities, Rapier bodies and contacts behind a private boundary, static stepped geometry, movement and air control, jumping, aiming, recoil, CCD bullets, reflection, damage-scaled knockback, a terminal upper-right impact, one winner, a real Bevy renderer, and live authoritative snapshots.
The yellow-crate slice adds stable-ID dynamic Rapier crates, an authority-owned terminal projectile contact and blast impulse, blue elimination and orange scoring, and one private final-composite fullscreen pass for the source-proved discrete radial RGB echoes. Visible and offscreen runs use that same GPU scene and effect pass.
Ring-out remains a separately tested simulation capability; the named replay ends before the result transition and records no ring-out.
Its named replay profile matches this card-modified interval without claiming base-game constants.
Production reliability and Steam transport, the remaining card mechanics, other arenas, complete match lifecycle, audio, and menus remain explicit gaps in `docs/fidelity/footage-coverage.md`.

## Recover the retired prototype

The final Godot and C# prototype is preserved by the annotated tag `archive/godot-csharp-prototype-2026-09-03`.
See `docs/legacy-prototype.md` for read-only lookup examples.
