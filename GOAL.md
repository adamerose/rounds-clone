# Goal

Reproduce the complete mechanics, physics, presentation, effects, match flow, and online multiplayer experience visible in both hash-identified recordings in `reference/manifest.json`.
The two recordings are the end-to-end target.
Earlier specifications and deterministic fixtures are recoverable from the archive tag, but they do not overrule visible behavior in the recordings.

## Product boundary

The game is a clean-room implementation in Rust and Bevy.
Do not copy source code or extract proprietary art, logo, audio, or other asset bytes.
Observable behavior, the `ROUNDS` title, and short gameplay names may be reproduced when the recordings or another direct source establish them.

Online play is part of the product from the first slice.
The same fixed-tick authoritative simulation must run in a client-host and a headless dedicated server.
The development transport may be ordinary UDP, but the network boundary must allow a later Steam transport without moving game rules into the transport.
Do not claim Steam transport, prediction, interpolation, rollback, or lag compensation until each one exists and has behavioral evidence.

Programmatic play and capture are product capabilities.
An agent must be able to supply bounded inputs, inspect bounded authoritative state, render a frame or video without opening an editor, and shut every process down cleanly.

## Fidelity work

`docs/fidelity/footage-coverage.md` accounts for both recordings from start to finish.
Each visible arena, mechanic, card interaction, match-flow state, and presentation effect stays linked to implemented behavior or a named unresolved gap.
Future work should close playable footage slices, including the rules and presentation needed to compare them end to end, instead of rebuilding the retired subsystem backlog.

Deterministic tests prove repeatability, not fidelity.
Fidelity requires a comparison with the identified recordings or equally direct evidence.
Tests should protect observed behavior, stable public boundaries, reproduced defects, and real release threats.
If support or test code becomes larger or harder to understand than the product slice it protects, stop and rethink the design before adding more machinery.

## Done

The project is done when both recordings can be replayed as complete online matches with matching visible behavior, the remaining gaps in the coverage ledger are closed, and the supported server, client, inspection, and capture commands pass from a clean checkout.
