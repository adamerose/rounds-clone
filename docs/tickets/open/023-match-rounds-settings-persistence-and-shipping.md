---
format: 3
status: idea
created: 2026-08-29T02:40:49Z
origin: human-request
tags: ["product-fidelity", "settings", "shipping"]
value: 8
risk: 4
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: [20, 21]
supersedes: []
split-from: []
---

# Match ROUNDS settings, persistence, and shipping

The clone has no owned completion path for ROUNDS' settings behavior, remembered choices, or distributable launch/update surface. Implement only observed target-build settings and persistence, then package the independent clone so a fresh Windows user can launch the same faithful local flow.

## Outcome

- Settings, defaults, ranges, labels, application timing, reset behavior, and persisted values match direct target-build observations.
- A clean packaged build starts without repository tools, preserves only the observed user choices, handles upgrades safely, and identifies itself as an unofficial ROUNDS clone where attribution is needed outside the in-game title.
- No new gameplay mode or convenience setting intentionally diverges from ROUNDS.

## Decisions

- Use public target behavior as the settings oracle; independently implement storage and packaging without copying source or proprietary asset bytes.
- Treat save migration, install/update, and failure recovery as shipping mechanics, not permission to change gameplay defaults.

## Evidence required

- A settings matrix and clean-room-safe evidence manifest bind every shipped control to target defaults/ranges and restart persistence.
- Fresh-install, upgrade, corrupt-save, reset, and portable launch tests pass in isolated directories.
- Native monitor-4 flow, packaged smoke, repository, deterministic simulation/replay, ticket, and whitespace gates pass.

## Work log

- 2026-08-29T02:40:49Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Assigning target-faithful settings, persistence, and packaging rather than leaving them as unowned completion prose.
- 2026-08-29T02:40:49Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Bound settings and shipping to direct public behavior, isolated persistence tests, and an independently packaged Windows build.
- 2026-09-02T02:09:33Z — Reflection verdict: wait because the distributable Windows build and observed persistence behavior required for completion need the faithful presentation and complete input flow owned by tickets 020 and 021.
