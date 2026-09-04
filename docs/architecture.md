# ROUNDS clone architecture

This document describes the active Bevy implementation.
The retired Godot and C# architecture remains available at the annotated tag `archive/godot-csharp-prototype-2026-09-03`.

## Fidelity source

The two videos identified by path, size, and SHA-256 in `reference/manifest.json` are the complete end-to-end target.
`docs/fidelity/footage-coverage.md` divides both recordings into contiguous reviewed intervals and assigns every visible behavior to implemented work or a named gap.
Older research under `research/` can help explain a behavior, but footage wins when the two disagree.

## Runtime shape

The authoritative match advances at 60 fixed ticks per second in `rounds-sim`.
Authoritative entities and resources live in Bevy ECS, and one ordered exclusive Bevy system advances input, movement, jumping, blocking, projectile creation, projectile travel, hits, and the match clock.
The first slice uses integer simulation units so its state is repeatable across the supported server and client-host paths.

Presentation reads an immutable authoritative snapshot through `rounds-presentation`.
Pixels, particles, audio, camera motion, interpolation, and other presentation-only state never enter the replicated snapshot or its hash.
The first renderer is deliberately small: it writes a deterministic PNG for automated evidence without creating a window or requiring editor-authored state.

`rounds-network` owns the wire records and the transport-facing API.
Its current adapter uses bounded IPv4 UDP datagrams on the local development machine.
The adapter accepts one complete scripted input stream from each of two clients and returns the same bounded authoritative snapshot to both.
It is not a production reliability protocol and does not claim prediction, interpolation, rollback, lag compensation, matchmaking, authentication, or Steam transport.
A future Steam adapter belongs behind this boundary and must preserve the same simulation inputs and snapshots.

`rounds-server` runs one headless authoritative session.
`rounds-client` runs the same simulation as a local client-host, submits one scripted stream to a remote development server, or captures a deterministic PNG with seed, tick, script digest, state digest, frame digest, and executable digest.
`rounds-automation` starts the headless server and two real client processes for smoke evidence and emits bounded JSON state.

## Workspace boundaries

| Crate | Owns | Does not own |
|---|---|---|
| `rounds-sim` | Bevy ECS authoritative state, fixed-tick rules, input validation, stable snapshots | rendering, sockets, files, wall clock |
| `rounds-presentation` | snapshot-to-frame presentation | authoritative or replicated state |
| `rounds-network` | bounded wire records and the current UDP adapter | game rules or presentation |
| `rounds-server` | headless server process and command-line configuration | duplicated simulation rules |
| `rounds-client` | local-host, remote scripted client and deterministic capture entry points | editor state or server-only rules |
| `rounds-automation` | smoke orchestration and JSON inspection | gameplay behavior |

No third-party physics library is selected in this slice.
If one is added, project-owned inputs and outputs must isolate it from match rules so an upgrade can be checked by behavior rather than compiler success.

## Public evidence commands

All dependencies, including Bevy `0.19.1`, are pinned in `Cargo.toml`, `rust-toolchain.toml`, and `Cargo.lock`.
The supported headless commands are:

```text
cargo fmt --all -- --check
cargo clippy --workspace --all-targets --locked -- -D warnings
cargo build --workspace --locked
cargo test --workspace --locked
target/debug/rounds-automation smoke --seed 38 --ticks 180
target/debug/rounds-automation inspect --seed 38 --ticks 180
target/debug/rounds-client capture --seed 38 --ticks 30 --output out/ticket-038-frame.png --metadata out/ticket-038-frame.json
```

The smoke command must report agreement among the headless server, both UDP clients, and the local client-host.
The capture command must reproduce the same frame and metadata digests when the executable, seed, tick, and script stay unchanged.

## Testing rule

Keep tests at the public and deep boundaries: deterministic simulation, bounded inspection, the UDP client/server agreement path, and deterministic rendering.
The smoke boundary deliberately forces a second-client launch failure and proves that its already-started server is gone; the capture boundary supplies two resolved-equivalent destinations and proves rejection happens before either file is written.
Do not retain tests for private layout or retired implementation details.
When test or support machinery outweighs the behavior it protects, rethink the slice instead of hardening the machinery by default.
