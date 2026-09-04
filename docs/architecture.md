# ROUNDS clone architecture

This document describes the active Bevy implementation.
The retired Godot and C# architecture remains available at the annotated tag `archive/godot-csharp-prototype-2026-09-03`.

## Fidelity source

The two videos identified by path, size, and SHA-256 in `reference/manifest.json` are the complete end-to-end target.
`docs/fidelity/footage-coverage.md` divides both recordings into contiguous reviewed intervals and assigns every visible behavior to implemented work or a named gap.
Older research under `research/` can help explain a behavior, but footage wins when the two disagree.

## Runtime shape

The authoritative match advances at 60 fixed ticks per second in `rounds-sim`.
Stable player and projectile identities and gameplay state live in Bevy ECS.
A project-owned `PhysicsBoundary` keeps Rapier rigid-body and collider handles private while it advances static arena contacts, dynamic circular players, CCD bullets, recoil, blocks, damage, knockback, and ring-outs.
Only quantized project snapshots cross the simulation boundary; neither Bevy entity IDs nor Rapier handles appear on the wire.
Repeatability is required on the same locked build and platform, not across platforms.

Presentation reads an immutable authoritative snapshot through `rounds-presentation`.
Pixels, camera motion, and other presentation-only state never enter the replicated snapshot or its hash.
The shipped Bevy 2D scene draws the static platforms and long shadows, fighters, limbs, guns, health/name treatment, bullets, trails, block rings, hit flash, and winner treatment.
The same scene model has an offscreen 1280×720 GPU render path for PNG evidence and a visible command-line path that starts hidden on monitor index 3, verifies Bevy's reported 1920×1080 monitor, and only then reveals the window.

`rounds-network` owns the wire records and the transport-facing API.
Its current adapter uses bounded IPv4 UDP datagrams on the local development machine.
Two clients handshake, send one monotonically sequenced input per advancing tick, and receive that tick's progressive authoritative snapshot.
The authority waits for both handshakes before releasing either client so an early input cannot overtake the other handshake.
It is not a production reliability protocol and does not claim prediction, interpolation, rollback, lag compensation, matchmaking, authentication, or Steam transport.
A future Steam adapter belongs behind this boundary and must preserve the same simulation inputs and snapshots.

`rounds-server` runs one headless authoritative session.
`rounds-client` runs the same simulation as a local client-host, submits one input sequence to a remote development server, renders a received live snapshot, runs visibly, or emits named replay anchors with source, input, state, executable, renderer, and frame identity.
`rounds-automation` starts the headless server and two real client processes, binds one client's render to its received final snapshot, checks local-host agreement, and emits bounded JSON evidence.

## Workspace boundaries

| Crate | Owns | Does not own |
|---|---|---|
| `rounds-sim` | Bevy ECS authoritative state, private Rapier service, fixed-tick rules, input validation, stable snapshots | rendering, sockets, files, wall clock |
| `rounds-presentation` | shared Bevy 2D visible/offscreen snapshot scene | authoritative or replicated state |
| `rounds-network` | bounded wire records and the current UDP adapter | game rules or presentation |
| `rounds-server` | headless server process and command-line configuration | duplicated simulation rules |
| `rounds-client` | local-host, live remote, visible, and replay-capture entry points | editor state or server-only rules |
| `rounds-automation` | smoke orchestration and JSON inspection | gameplay behavior |

`bevy_rapier2d` 0.36 is pinned with default features disabled and only `dim2` and `headless` enabled.
The incompatible `enhanced-determinism` feature is deliberately absent; the server-authority model does not require cross-platform lockstep.

## Public evidence commands

All dependencies, including Bevy `0.19.1`, are pinned in `Cargo.toml`, `rust-toolchain.toml`, and `Cargo.lock`.
The supported headless commands are:

```text
cargo fmt --all -- --check
cargo clippy --workspace --all-targets --locked -- -D warnings
cargo build --workspace --locked
cargo test --workspace --locked
target/debug/rounds-automation smoke --seed 38 --ticks 780 --output-dir out/ticket-039/smoke
target/debug/rounds-automation inspect --seed 38 --ticks 780
target/debug/rounds-client capture-replay --seed 38 --ticks 780 --output-dir out/ticket-039/anchors --metadata out/ticket-039/anchors.json
target/debug/rounds-client visible --seed 38 --ticks 780 --frames 180
```

The smoke command must report every handshake, input sequence and progressive snapshot, agreement among the headless server, both UDP clients and local client-host, and a live client render bound to the agreed state hash.
Replay capture emits five named Bevy-rendered anchors spanning spawn through round end.

## Testing rule

Keep tests at the public and deep boundaries: stable contact and jump behavior, the complete duel, one-tick bullet CCD, bounded inspection, progressive UDP agreement, and the real Bevy offscreen renderer.
A deep process-lifecycle test starts a minimal test-owned UDP child, forces the next child launch to fail, and proves cleanup releases the server; the capture boundary supplies two resolved-equivalent destinations and proves rejection happens before either file is written.
Do not retain tests for private layout or retired implementation details.
When test or support machinery outweighs the behavior it protects, rethink the slice instead of hardening the machinery by default.
