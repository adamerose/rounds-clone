# ROUNDS clean-room rewrite

This repository starts from the two supplied ten-minute ROUNDS recordings and builds the clone in Rust with Bevy.
The first footage-derived slice recreates the teal static duel at 00:22.50–00:35.60: two clients drive one 60 Hz Rapier authority through sequenced local UDP input, the same rules run as a client-host, and the shared Bevy 2D scene renders visibly or to offscreen evidence frames.

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
```

The smoke result proves that both client processes handshake, send monotonic input sequences, receive every progressive snapshot, agree with the authority and local host, and bind one real Bevy render to the received final state.
Replay capture emits five named 1280×720 anchors whose metadata identifies the source recording and timestamp, replay input, state, renderer, executable, and frame.

## What is and is not implemented

The teal slice has stable Bevy ECS identities, Rapier bodies and contacts behind a private boundary, static stepped geometry, movement and air control, jumping, aiming, recoil, CCD bullets, reflection, damage-scaled knockback, a terminal upper-right impact, one winner, a real Bevy renderer, and live authoritative snapshots.
Ring-out remains a separately tested simulation capability; the named replay ends before the result transition and records no ring-out.
Its named replay profile matches this card-modified interval without claiming base-game constants.
Production netcode and Steam transport, the full match loop, cards, other arenas, stronger effects, audio, and menus remain explicit gaps in `docs/fidelity/footage-coverage.md`.

## Recover the retired prototype

The final Godot and C# prototype is preserved by the annotated tag `archive/godot-csharp-prototype-2026-09-03`.
See `docs/legacy-prototype.md` for read-only lookup examples.
