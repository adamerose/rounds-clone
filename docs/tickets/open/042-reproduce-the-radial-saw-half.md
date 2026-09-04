---
format: 3
status: idea
created: 2026-09-04T12:13:08Z
origin: system-detected
tags: ["bevy", "physics", "fidelity", "multiplayer", "presentation", "effects", "vertical-slice"]
value: 10
risk: 4
sessions:
  - codex:01a06920-7449-74d0-9b09-57855a012572
execution: unattended
depends-on: [41]
supersedes: []
split-from: []
---

# Reproduce the radial-saw half

Reconstruct source PTS 03:53.717 through approximately 04:07.700 of `reference/MedalTVRounds20260903170709695.mp4`: the complete ice-faced radial-saw combat interval, including ordinary projectile exchanges, the strong 04:01.750 ice burst, and the last combat beat before the half result.
This is the next highest-value footage-bound slice because it combines a reusable moving hazard, collision-rich arena traversal, the reference game's most demanding impact juice yet selected, independently recreated audio, and live received-state multiplayer in one short playable interval.

## Outcome

- A named source replay runs continuously from the radial-saw arena reveal at PTS 03:53.717 through the last combat beat at approximately 04:07.700 with the observed arena geometry, fighter spawn relationship, movement, firing, impacts, damage progression, camera behavior, and source-relative cadence.
- Stable project-owned saw identities carry authoritative angle, angular velocity, and collider pose through arena initialization, simulation snapshots, state digests, and the UDP wire.
- The saws rotate and reset from authority state rather than a local render clock, while this slice makes no unsupported claim that visible saw contact is lethal.
- Static white/cyan surfaces reproduce the observed bright paper-and-brush appearance, silhouette, collision support, and traversal without inventing special friction, freezing, fracture, or destruction behavior that the selected evidence does not show.
- The 04:01.750 hit recreates the observed layered ice-like burst through independently authored projectile and impact particles, emissive shapes, light/color response, camera impulse, chromatic separation, trails, and bounded screen distortion using the ordinary Bevy presentation path.
- Ordinary shots and the strong burst remain readable against the dark moving paper-brush background and long directional shadows, and presentation-only animation cannot alter authority state or state hashes.
- Independently created shot, impact, strong-burst, and saw-ambience cues play from semantic presentation events measured against the source interval.
- One authority and two separately launched UDP clients advance the replay from client inputs and agree on progressive fighter, projectile, damage, arena, saw, event, and final-state digests.
- At least one actual rendered client consumes received snapshots throughout the interval, including saw pose and the strong burst trigger, without advancing gameplay or hazard state locally.
- Programmatic replay capture emits bounded visual, motion, audio-event, and network evidence tied to exact source PTS, source hash, seed, input trace, executable identity, renderer identity, authority digest, and output hashes.
- `docs/fidelity/footage-coverage.md` replaces its materially incorrect second-recording rows from approximately 03:00 through at least 04:40 with an exact-PTS audit before marking this interval as implemented coverage.
- Architecture and decision records explain rotating-hazard authority, impact-event ownership, received-state rendering, and the chosen fullscreen-effects path without introducing a parallel simulation, renderer, network protocol, or evidence launcher.

## Decisions

- The binding source is recording SHA-256 `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9`, beginning at PTS 03:53.717 and ending at the last combat frame around 04:07.700.
- The verified arena-start frame is SHA-256 `f14b1d4b2a463cebe66e85b6cb54f8668c9eac972bb07cd595171d197e7498d7`, and the verified 04:01.750 burst frame is SHA-256 `531eb1aceee554b586345ad5bbd6726a6922e927f01d37f3d36942720fd4d9af`.
- `docs/fidelity/radial-saw-half-observations.md` records the checked anchors and the timestamp-ledger defect.
- Exact timestamp claims require a full decode with output-side PTS selection or a seek to an earlier keyframe followed by `-copyts` and selection on global PTS.
- Input-side `-ss` alone, hybrid double seeks, inferred constant offsets, filename sequence numbers, and sparse-keyframe landings cannot support source timing or coverage edits.
- Before tuning or ledger correction, reproduce the PTS 03:20.000 `WAITING` cross-check and both binding anchors through two accepted decode routes, then determine the exact end boundary and all affected ledger-row boundaries from source PTS.
- Extend the existing replay profile, shared Rapier boundary, stable snapshots and wire records, shared Bevy renderer, client capture, automation smoke, and effect components instead of creating slice-specific duplicates.
- Use ECS for durable fighters, projectiles, rotating hazards, and semantic gameplay events that compose through collisions or cards.
- Keep fixed arena definitions, stable-ID registries, replay choreography, background animation, particles, screen effects, and audio mixing as ordinary data or resources where entity composition adds no value.
- Stable saw IDs plus quantized pose and velocity cross snapshots and the wire.
- Rapier handles, Bevy entity IDs, particles, render targets, shader history, audio buffers, and device state remain outside deterministic gameplay state.
- Treat visible HUD card stacks as source context, not permission to implement unproven card mechanics.
- Recreate sound and visual assets independently.
- Keep evidence proportional to the feature by reusing the existing replay/capture paths and retaining only checks that protect visible fidelity, physics authority, network agreement, or process cleanup.

## Essential constraints

- The source interval is proved as one continuous playable run rather than a set of disconnected still-image setups.
- The rotating collider and its pose are authority-owned even though this slice does not assert saw damage.
- The strong burst is driven by a replicated semantic gameplay event, while distortion, particles, light, chromatic separation, camera impulse, and audio playback remain presentation-only.
- Received-state rendering uses authority snapshots and event identities instead of local client physics or locally inferred hits.
- Visible verification uses the existing hidden-first exact `(364,-1080)` 1920×1080 monitor identity and fails closed before showing a window when that display is missing or ambiguous.
- The current localhost UDP adapter remains a development transport and cannot be described as production networking.

## Non-goals

- The result overlay or the following arena, draft, and card selection.
- Unobserved ice friction, freezing, fracture, destruction, or saw-contact damage rules.
- Full visible card-stack behavior, general card combinations, or expansion of the random offer pool.
- Production online transport, prediction, rollback, reconnect, relay/NAT traversal, lobbies, matchmaking, authentication, Steamworks, anti-cheat, or cross-platform lockstep.
- Remaining arenas, full-match completion, final character art, complete music, localization, or identity with proprietary ROUNDS assets.

## Evidence required

- An exact-PTS source audit reproduces the PTS 03:20.000 `WAITING`, 03:53.717 arena-start, and 04:01.750 strong-burst hashes through two accepted decode routes, determines the exact last-combat boundary, and corrects every affected `docs/fidelity/footage-coverage.md` row from approximately 03:00 through at least 04:40.
- Source evidence includes short consecutive-frame sequences around arena reveal, ordinary traversal and firing, the strong burst, and the final combat beat so motion, effect duration, layering, and camera response are not accepted from isolated stills.
- Focused simulation checks prove stable saw identity, authoritative rotation and reset, collider participation without an unsupported damage rule, deterministic event ordering, damage progression, and replay final state.
- Perturbing saw speed, input timing, or the strong-burst gameplay event changes the protected motion or outcome rather than only a serialized tuning field.
- Live network evidence launches one authority and two client processes, advances one input from each client per tick for the full interval, and proves both clients plus a received-state render agree on ordered saw, fighter, projectile, damage, event, and final-state digests.
- Induced partial startup and mid-replay failure leave no child process.
- The shared Bevy offscreen path emits 1280×720 PNGs for no fewer than six named source-bound anchors spanning arena reveal, saw rotation, ordinary projectile exchange, upper-left compact impact, strong ice burst, and final combat.
- A short source/clone motion comparison records similarities and remaining differences in arena silhouette, scale, saw period and phase, surface/background motion, shadows, projectiles, hit feedback, camera response, chromatic separation, distortion, and cadence.
- A reviewer inspects original-resolution source and clone frames rather than only a reduced contact sheet.
- Audio evidence identifies exact source-relative semantic events, proves every cue is independently created, records event-to-output timing, and exercises the real client mix or a bounded capture of that same game-owned mix.
- A visible command-line replay passes the hidden-first monitor guard, audibly and visibly traverses the interval, exits boundedly, and leaves no process, window, or audio-device residue.
- `cargo test --workspace --locked -- --nocapture` passes as the first Cargo command from one verified-absent isolated target, followed by formatting, strict lint, locked build and tests, exact-PTS checks, replay inspection, live two-client smoke, visual and audio capture, visible playback, ticket checks, exact-range whitespace checks, artifact hashes, and residue checks.
- A line and responsibility inventory accounts separately for product, test, and automation-support growth.
- No test-only physics, local client simulation, duplicate renderer, duplicate audio engine, or parallel replay/evidence implementation is accepted.

## Work log

- 2026-09-04T12:13:08.096Z stage design start session codex:01a06920-7449-74d0-9b09-57855a012572/reflect_042 — Began shaping the highest-value next footage-bound slice after exact-PTS decoding disproved the ledger's radial-saw timestamps.
- 2026-09-04T12:16:08.709Z stage design end session codex:01a06920-7449-74d0-9b09-57855a012572/reflect_042 — Bounded ticket 042 to the exact radial-saw combat interval, its strong ice-like burst, multiplayer authority, and the required timing-ledger correction.
- 2026-09-04T12:19:01.827Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a06c5b-0eb9-7e10-98b8-7e2813eeda52 — Began fresh independent admission review of the exact radial-saw contract and source-observation range.
