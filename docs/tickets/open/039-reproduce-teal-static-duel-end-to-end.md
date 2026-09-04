---
format: 3
status: idea
created: 2026-09-04T02:51:05Z
origin: human-request
tags: ["bevy", "physics", "fidelity", "multiplayer", "vertical-slice"]
value: 10
risk: 4
sessions:
  - codex:01a06920-7449-74d0-9b09-57855a012572
execution: unattended
depends-on: [38]
supersedes: []
split-from: []
---

# Reproduce the teal static duel end to end

Reconstruct the complete duel visible from approximately 00:22.50 through 00:35.50 in `reference/MedalTVRounds20260903170709695.mp4` as the first footage-derived vertical slice. The slice must establish the real physics, rendered client, authoritative multiplayer, and comparison workflow that later arenas, cards, and effects can extend; an isolated mechanics demo or synthetic state proof is not the outcome.

## Outcome

- The authoritative 60 Hz simulation uses pinned Rapier 2D physics behind a project-owned boundary for player bodies, static arena colliders, bullets, contacts, impulses, and queries. Bevy entities and stable game identifiers remain the gameplay authority; snapshots and network messages never expose Rapier handles or serialized engine internals.
- The reconstructed teal arena matches the selected interval's stepped platform layout, spawn relationship, scale, dark teal background, colored platform faces, and long cast-shadow composition closely enough that source and clone anchor frames can be compared at the same 1280×720 viewport.
- Two circular fighters can stand and slide on platforms, run with air control, jump through the observed vertical routes, aim independently of movement, fire physical bullets, block or reflect a shot, take health-scaled knockback and damage, fall or be knocked outside the arena, and produce a single authoritative winner.
- Gun recoil affects the shooter, bullets use continuous collision handling so a one-tick platform or player crossing is not missed, and collisions distinguish players, arena surfaces, bullets, and block state through named game concepts rather than third-party component types in public interfaces.
- A shared presentation model drives an actual Bevy 2D client scene and the command-line capture path. The client renders platforms and shadows, circular player bodies with directional gun and limbs, health/name treatment, bullets and trails, block feedback, hit flash, and restrained camera response visible in this interval; placeholder circles on a ground strip are removed.
- The client can run visibly from a command without editor interaction and can run an equivalent hidden or offscreen scripted replay that emits timestamped PNG anchor frames and bounded JSON evidence. Visible-window verification, when performed, obeys the repository's monitor-4 placement rule.
- The existing local-authority and headless-server modes run the same teal-duel rules. Two separately launched UDP clients submit the footage-derived input trace and receive an agreed final authoritative snapshot; the protocol transmits stable gameplay state and inputs, not physics-engine state.
- `docs/fidelity/footage-coverage.md` splits the 00:20–00:40 ledger entry into the observed draft fade, reconstructed duel, and result transition. It records this exact interval as the implemented first `S2-static-duel` sub-slice without claiming the unresolved card, match-result, advanced presentation, production-online, or other-arena work.

## Decisions

- The binding source is recording SHA-256 `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9`, interval 00:22.50–00:35.50. Store generated reference extracts and clone captures under ignored `out/`; do not alter the supplied recording.
- Use `bevy_rapier2d` 0.36 with default features disabled and only the 2D, headless, and enhanced-determinism features needed by this slice. It is compatible with Bevy 0.19 and avoids inventing collision, contact, and CCD behavior, while a project boundary limits future upgrade or engine-swap cost.
- Enhanced determinism supports repeatable authoritative testing; it is not a promise of rollback lockstep. The current online model remains server/client-host authority with snapshots, future client prediction for local actions, and interpolation for remote and dynamic-world state.
- Use ECS for durable gameplay identity, state, rules, and effect composition. Treat the physics world as a service synchronized at the fixed-step boundary and the renderer as a consumer of presentation snapshots; do not turn every short-lived visual particle or math helper into a networked ECS entity.
- Build the real Bevy renderer in this slice because subsequent fullscreen distortion, particles, lighting, and reactive-world work must extend the shipped path. A deterministic CPU image may remain only as a narrow diagnostic fallback and cannot be cited as visual-fidelity evidence.
- Tune against several frames and the complete motion interval, not a single screenshot. Match observable silhouettes, routes, impacts, timing, and composition; do not infer or claim inaccessible original numeric constants.
- Keep tests proportional. Protect physics/contact boundaries, one complete behavioral replay, network agreement, and capture metadata. If test or support code exceeds the product implementation for this slice, stop and document why each excess subsystem is necessary or simplify it before review.

## Non-goals

- Card choice UI, card stacking, the partial draft fade before 00:22.50, and the `HALF ORANGE` result presentation after 00:35.50.
- Moving, articulated, destructible, ice, explosive, or otherwise reactive arena geometry.
- Production matchmaking, relay/NAT traversal, rollback, Steamworks transport, anti-cheat, or adversarial local-machine hardening.
- Full audio fidelity or the extreme chromatic, radial, and explosive effects visible in later intervals.

## Evidence required

- A checked source-observation record identifies the selected interval, at least five source anchor timestamps, the arena geometry and palette measurements used, and the observed action sequence. Every derived frame records the source hash and timestamp.
- Locked formatting, strict lint, build, and all tests pass from an absent `target` directory without relying on command ordering, ignored tests, an editor, or a prebuilt sibling executable.
- Focused simulation evidence shows stable platform contact, the intended jump route, bullet CCD, block reflection, damage and recoil impulses, health-scaled knockback, ring-out, and one winner during the footage-derived replay.
- Two real client processes and one server process complete the replay, agree on the bounded authoritative snapshot and outcome, and leave no child process behind on success or induced partial-start failure.
- The actual Bevy presentation path emits 1280×720 PNGs at no fewer than five named replay ticks spanning spawn, traversal, shot or block, hit or knockback, and round end. Capture metadata binds source interval, source hash, seed, input-trace hash, tick, state hash, executable hash, renderer identity, and frame hash.
- A reviewer inspects a source-and-clone contact sheet and records concrete similarities and remaining differences. Automated comparison may support layout and palette checks but cannot substitute for this visual review.
- A visible command-line launch is programmatically exercised for movement, aim, fire, and block without editor interaction; window-center evidence proves monitor 4 placement before any screenshot or observation.
- A line and responsibility inventory maps every new test and support module to the behavior or public boundary it protects and confirms the proportionality decision.

## Work log

- 2026-09-04T02:51:05.902Z stage design start session codex:01a06920-7449-74d0-9b09-57855a012572 — Began the first footage-derived end-to-end Bevy slice around the simplest complete static duel after Adam made full reproduction of both supplied videos the continuing goal and asked that the prior proof-heavy workflow not recur.
- 2026-09-04T02:52:32.253Z stage design end session codex:01a06920-7449-74d0-9b09-57855a012572 — Bound the exact source interval, Rapier boundary, real Bevy renderer, authoritative two-client replay, source-and-clone visual comparison, monitor-safe programmatic interaction, explicit non-goals, and support-code proportionality gate into one falsifiable vertical slice.
