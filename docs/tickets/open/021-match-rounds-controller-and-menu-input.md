---
format: 3
status: idea
created: 2026-08-29T02:40:49Z
origin: human-request
tags: ["product-fidelity", "controls", "game-shell"]
value: 9
risk: 4
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: [15, 16, 20]
supersedes: []
split-from: []
---

# Match ROUNDS controller and menu input

ROUNDS is played through its complete controller and keyboard flow, while the clone currently exposes debug-oriented keyboard and mouse bindings. Match the target build's player join, menu navigation, movement, aim, jump, fire, block, draft, pause, reconnect, and input-device behavior without adding a different shipped control mode.

## Outcome

- Two local players can complete the same ROUNDS menu-to-win path using supported controllers and the observed keyboard fallback.
- Dead zones, aim normalization, held/released draft behavior, simultaneous-device ownership, pause, disconnect, and reconnect match direct target-build observations.
- Test bots may inject the same semantic inputs internally but never appear as a player-visible game mode.

## Decisions

- Use installed-build behavior and its visible control prompts as the oracle; keep raw target captures external and commit only clean-room-safe manifests and clone evidence.
- Preserve the deterministic `PlayerInput` boundary. Device mapping belongs to the game shell and must not add engine dependencies to `Rounds.Sim`.

## Evidence required

- A control matrix binds every delivered action/device/state to target-build observations and automated semantic-input tests.
- Native two-controller and keyboard fallback flows pass end to end on monitor 4, including draft, pause, disconnect, and reconnect.
- Simulation/replay, Godot, repository, ticket, and whitespace gates pass with no player-visible bot option.

## Work log

- 2026-08-29T02:40:49Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Assigning the controller and menu-input fidelity gap without converting internal bots into an invented shipped mode.
- 2026-08-29T02:40:49Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Bound local input behavior to direct target-build evidence and the existing deterministic semantic-input boundary.
