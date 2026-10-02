---
format: 3
status: idea
created: 2026-10-02T04:56:22Z
origin: system-detected
tags: ["rounds", "multiplayer", "online", "bevy"]
value: 9
risk: 6
sessions:
  - codex:01a0fa96-a468-7332-92f6-c36c3908e4b9
execution: unattended
parent: 59
depends-on: [63]
supersedes: []
split-from: []
---

# Play through live input and progressive network snapshots

The current remote client sends a precomputed input trace and renders only its final received snapshot, while the dedicated server binds only to localhost. Two players need to send inputs while they play and see the same authority progress in their shared Bevy scenes, using the same game rules as local client-host play.

## Outcome

- Pending shaping: live input can be supplied during a bounded online session, both client scenes render received progressive snapshots, and the dedicated authority supports an explicitly selected development address. A client-host runs the same authoritative simulation and transport-facing boundary rather than a separate game-rules implementation.
- Inputs supplied after an observed earlier snapshot demonstrably affect later authoritative state; remote presentation cannot predict or mutate game rules privately. Existing scripted smoke and capture capabilities remain supported.
- Bounded session termination and peer/process errors release sockets, threads and child processes and leave callers with a concrete result. Real human control and programmatic control use the existing PlayerInput/flow-command boundaries.

## Decisions

- Goal 59 delegates reversible choices; no Steam, prediction, interpolation, rollback or lag-compensation claim without implementation and behavior evidence. This task advances online play but does not close the full S8 gap or missing footage mechanics by itself.
- Reuse one authoritative fixed-tick simulation, the existing UDP adapter and one shared Bevy renderer. Do not add a generic networking framework or move movement, weapon, card or scoring rules into transport or presentation.
- No firewall changes, privileged networking, account/secrets handling, purchased service, deployment, public host or outside-recipient messages. Verification uses controlled development endpoints and processes; address configurability alone does not authorize exposing a running host.
- Risk 6 requires an other-provider consult on scheduling, input sequencing, shutdown and the smallest product seam before this contract is admitted. Stage this work after the movement worker releases the sole Cargo/render/visible slot; all verification reuses the prepared two-job Cargo target and monitor-4 placement.

## Evidence required

- Pending shaping: the supported dedicated and client-host interfaces drive two live clients with changed inputs after a prior snapshot. Every received progressive state is authoritative, inputs affect the intended player and both scenes advance before the final tick.
- Meaningful regressions exercise late input sampling, progressive rendering, process/session termination and a failure at the real network boundary. Existing source replay hashes and capture routes remain covered at the approved candidate.
- A fresh independent review examines the exact candidate and actual supported-interface evidence before publication to main.

## Chat excerpts

Adam — this goal's initiating request:

> Finish the game

## Scratch

Current evidence: rounds-server/src/main.rs binds127.0.0.1; rounds-client remote builds scripted input before send_inputs and renders received final state; rounds-network send_inputs consumes a fixed slice and synchronously exchanges one input/snapshot per sequence; presentation run_interactive_visible owns a local authority. These are concrete missing live-play seams, not evidence that a new transport is necessary. A read-only planning agent is checking duplicates and the smallest contract; consult and admission must resolve tick pacing, peer-readiness, thread ownership and bounded stop behavior without guessing source mechanics.

## Work log

- 2026-10-02T04:56:22Z stage research start session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Identify actual live online-play gaps in current supported commands while movement and source workers continue.
- 2026-10-02T05:00:37Z stage research end session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Current network authority already owns the right two-input fixed-step seam; missing live input/snapshot pumping and received-state scene are concrete. Other-provider consult69403 running. Contract must support at least36011 ticks for600.17-second recording (prefer explicit source-derived601-second bound36060), not inherit the6000-tick smoke cap. Scripted smoke cap/API can remain separate; default bind remainsloopback, nonlocal bindexplicit, no deployment or firewall change. Fresh admission awaits minimal lifecycle/shutdown design.
