# ROUNDS clean-room rewrite

This repository now starts from the two supplied ten-minute ROUNDS recordings and builds the clone in Rust with Bevy ECS.
The current executable slice is development scaffolding: two scripted clients drive one 60 Hz authoritative match through a local UDP server, the same simulation runs as a client-host, bounded state is emitted as JSON, and a deterministic PNG can be captured without opening an editor.

## Build and verify

```powershell
cargo fmt --all -- --check
cargo clippy --workspace --all-targets --locked -- -D warnings
cargo build --workspace --locked
cargo test --workspace --locked
.\target\debug\rounds-automation.exe smoke --seed 38 --ticks 180
.\target\debug\rounds-automation.exe inspect --seed 38 --ticks 180
.\target\debug\rounds-client.exe capture --seed 38 --ticks 30 --output out\ticket-038-frame.png --metadata out\ticket-038-frame.json
```

The smoke result names the development transport and proves that two client processes, the headless server, and the local host agree on one authoritative hash.
The client capture metadata records the exact seed, tick, scripted-input digest, state digest, frame digest, and executable digest.

## What is and is not implemented

The first slice has Bevy ECS state, fixed ticks, two players, movement, jumping, firing, blocking, projectile hits, an authoritative headless server, a local client-host, localhost UDP transport, bounded state inspection, and deterministic headless capture.
Its tuning and minimal rendering are scaffolding and are not claimed as ROUNDS fidelity.
Steam transport, production netcode, the full match loop, cards, faithful arenas, effects, audio, menus, and footage-matched tuning remain explicit gaps in `docs/fidelity/footage-coverage.md`.

## Recover the retired prototype

The final Godot and C# prototype is preserved by the annotated tag `archive/godot-csharp-prototype-2026-09-03`.
See `docs/legacy-prototype.md` for read-only lookup examples.
