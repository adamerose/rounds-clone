---
format: 3
status: ready
created: 2026-09-04T12:13:08Z
origin: system-detected
tags: ["bevy", "physics", "fidelity", "multiplayer", "presentation", "match-flow", "vertical-slice"]
value: 9
risk: 4
sessions:
  - codex:01a06920-7449-74d0-9b09-57855a012572
execution: unattended
depends-on: [41]
supersedes: []
split-from: [18]
---

# Reproduce the radial-saw duel and HALF BLUE transition

Reconstruct the continuous source interval from the fully revealed radial-saw arena at PTS 2320490718, or 03:52.049072, through established `HALF BLUE` at PTS 2476823426, or 04:07.682343.
This adds one evidence-backed moving-hazard arena and its exact combat-to-result handoff while exercising authoritative physics, ordinary projectile feedback, match flow, and received-state multiplayer together.

## Outcome

- A named replay runs continuously from the fully revealed arena through combat and `HALF BLUE` with observed geometry, spawns, movement, firing, ordinary impacts, damage, score, framing, and cadence.
- Combat ends on PTS 2471823446, or 04:07.182345, and the result begins on the immediately following frame at PTS 2471990112, or 04:07.199011.
- Stable project-owned saw identities carry authoritative angle and angular velocity through initialization, snapshots, digests, and the UDP wire.
- Saws rotate and reset from authority state rather than a local render clock, but this child adds no unobserved saw-damage rule.
- Static white/cyan platforms reproduce observed silhouette, collision support, and traversal without inventing ice friction, freezing, fracture, or destruction.
- The ordinary Bevy presentation reproduces the moving paper-brush background, platform treatment, long shadows, saw silhouette and rotation, projectile trails, small impact clouds and particles, result dimming, score circles, and `HALF BLUE` text.
- One authority and two separately launched UDP clients advance the replay from client inputs and agree on fighter, projectile, damage, arena, saw, phase, score, and final-state digests.
- At least one rendered client consumes received snapshots through combat and the result without advancing gameplay, saw pose, winner, or score locally.
- Programmatic capture emits bounded visual, motion, simulation, and network evidence tied to exact source PTS and hash, seed, input trace, executable and renderer identity, authority digest, and output hashes.
- `docs/fidelity/footage-coverage.md` replaces materially incorrect second-recording rows from approximately 03:00 through at least 04:40 with a canonical exact-PTS audit before marking this interval implemented.
- Architecture and decisions record rotating-hazard ownership, received-state rendering, and the combat-to-result boundary without a parallel simulation, renderer, network protocol, or evidence launcher.
- This is the first radial-saw child split from arena umbrella ticket `018` and advances that catalog without closing its full 70-row outcome.
- This child contributes evidence toward but neither closes nor depends on umbrella outcomes `016` for base feel, `017` for projectile presentation, `020` for overall presentation and audio, and `022` for a full-match replay.

## Decisions

- The binding source is SHA-256 `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9`, source PTS 2320490718 through 2476823426 at time base `1/10000000`.
- `docs/fidelity/radial-saw-half-observations.md` records the extraction method, boundary, hashes, observations, and ledger defect.
- Canonical frame identity is the SHA-256 of exactly 3,686,400 native 1280×720 decoded RGBA bytes produced with FFmpeg `7.1-essentials_build-www.gyan.dev`, libavcodec `61.19.100`, and `libaom-av1`.
- Frame selection preserves source timestamps and takes the first decoded frame at or after an integer source PTS; wall-clock requests are replaced by the selected frame's actual PTS.
- PNG hashes, sequence numbers, input-side `-ss` without source-PTS selection, hybrid double seeks, and inferred constant offsets cannot support fidelity claims.
- Independently reproduce the four binding interval hashes and 03:20 `WAITING` cross-check before tuning or ledger correction.
- Extend the existing replay profile, Rapier boundary, snapshots, wire records, shared renderer, client capture, and automation smoke instead of slice-specific duplicates.
- Use ECS for durable fighters, projectiles, rotating hazards, and gameplay/result events that compose through collisions or match flow.
- Keep fixed arena definitions, stable-ID registries, replay choreography, background animation, particles, and UI interpolation as ordinary data or resources where entity composition adds no value.
- Stable saw IDs plus quantized pose and velocity cross snapshots and the wire, while engine handles and presentation state do not.
- The reviewed sequence disproves the proposed special ice-burst event, fullscreen distortion, and burst-audio work, so none is part of this child.
- Keep evidence proportional by reusing existing replay and capture paths and retaining checks only for visible fidelity, physics authority, match flow, network agreement, or cleanup.

## Essential constraints

- Combat and `HALF BLUE` are proved in one continuous playable run rather than disconnected setups.
- Saw pose is authority-owned even though saw collision damage is outside this evidence.
- Received-state rendering uses authority snapshots and ordered result events instead of local physics or inferred winners.
- Presentation-only background, shadows, trails, impact particles, dimming, and UI interpolation cannot change authority digests.
- Visible verification uses the hidden-first exact `(364,-1080)` 1920×1080 monitor identity and fails closed when that display is missing or ambiguous.
- The localhost UDP adapter remains a development transport and cannot be described as production networking.

## Non-goals

- A distinct ice burst, fullscreen distortion, burst semantic event, or burst-specific audio.
- Source-audio recreation or progress against ticket `020`'s complete audio outcome.
- Saw-contact damage or lethality, ice friction, freezing, fracture, or destructibility.
- The arena after `HALF BLUE`, draft flow, visible card-stack behavior, or general card combinations.
- Production online transport, prediction, rollback, reconnect, relay/NAT traversal, lobbies, matchmaking, authentication, Steamworks, anti-cheat, or cross-platform lockstep.
- Closing umbrella tickets `018`, `016`, `017`, `020`, or `022`.

## Evidence required

- A canonical exact-PTS audit reproduces the PTS 2000158666 `WAITING`, 2320490718 arena, 2471823446 last-combat, 2471990112 result-onset, and 2476823426 `HALF BLUE` RGBA hashes recorded in the observation document.
- That audit corrects every affected footage-ledger row from approximately 03:00 through at least 04:40 with actual source PTS and hashes rather than an assumed offset.
- Native consecutive frames cover arena reveal, representative traversal and shots, saw rotation, ordinary impacts, the adjacent-frame combat/result boundary, and established `HALF BLUE`.
- Focused simulation checks prove stable saw identity, authoritative rotation and reset, deterministic event ordering, damage progression, phase order, score persistence, and replay final state without asserting saw damage.
- Perturbing saw speed or input timing changes protected motion or outcome rather than only a tuning field.
- Live network evidence launches one authority and two clients, advances one input from each per tick, and proves both clients plus received-state rendering agree on saw, fighter, projectile, damage, phase, score, and final digests.
- Induced partial startup and mid-transition failure leave no child process.
- The shared offscreen path emits 1280×720 PNGs for no fewer than six anchors spanning reveal, traversal, projectile exchange, ordinary impact, last combat, result onset, and `HALF BLUE`.
- A source/clone motion comparison records arena silhouette, scale, saw period and phase, platform/background motion, shadows, projectiles, ordinary hit feedback, result dimming, score layout, and transition cadence.
- A reviewer inspects original-resolution frames rather than only a reduced contact sheet.
- A visible command-line replay passes the monitor guard, traverses combat and result, exits boundedly, and leaves no process or window residue.
- `cargo test --workspace --locked -- --nocapture` passes first from one verified-absent isolated target, followed by formatting, strict lint, locked build and tests, exact-PTS checks, replay inspection, two-client smoke, offscreen capture, visible playback, ticket checks, whitespace checks, hashes, and residue checks.
- A line and responsibility inventory separates product, test, and automation-support growth and rejects test-only physics, local client simulation, duplicate rendering, or parallel evidence infrastructure.

## Work log

- 2026-09-04T12:13:08.096Z stage design start session codex:01a06920-7449-74d0-9b09-57855a012572/reflect_042 — Began shaping the next footage-bound slice after exact-PTS decoding disproved the ledger's radial-saw timestamps.
- 2026-09-04T12:16:08.709Z stage design end session codex:01a06920-7449-74d0-9b09-57855a012572/reflect_042 — Bounded the first candidate to a supposed interval and claimed ice-like burst.
- 2026-09-04T12:19:01.827Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a06c5b-0eb9-7e10-98b8-7e2813eeda52 — Began independent admission review of the contract and source range.
- 2026-09-04T12:40:06.868Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/01a06c5b-0eb9-7e10-98b8-7e2813eeda52 — Rejected admission after native frames disproved the timestamps and ice-burst premise, exposed unstable PNG hashes, and found missing umbrella provenance.
- 2026-09-04T12:40:06.869Z stage correction start session codex:01a06920-7449-74d0-9b09-57855a012572/reflect_042 — Began rebinding the contract to source PTS, RGBA pixel hashes, the adjacent-frame result boundary, and arena-child provenance.
- 2026-09-04T12:43:54.956Z stage correction end session codex:01a06920-7449-74d0-9b09-57855a012572/reflect_042 — Replaced the rejected premise with a native-frame contract for the radial-saw duel and `HALF BLUE`, recorded canonical hashing and provenance, and passed ticket and whitespace checks.
- 2026-09-04T12:45:14.529Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a06c5b-0eb9-7e10-98b8-7e2813eeda52 — Began independent re-review of the native-frame correction, exact combat/result boundary, and umbrella provenance.
- 2026-09-04T12:49:16.470Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/01a06c5b-0eb9-7e10-98b8-7e2813eeda52 — Approved the corrected risk-4 contract with no findings after reproducing all five canonical RGBA hashes, verifying the adjacent combat/result frames, and confirming ownership and feasibility.
- 2026-09-04T12:49:16.470Z stage design end session codex:01a06920-7449-74d0-9b09-57855a012572 — Admitted ticket 042 after independent review approved its native-frame radial-saw duel, `HALF BLUE` transition, multiplayer, presentation, and proportional evidence contract.
- 2026-09-04T12:57:12.019Z stage implement start session codex:01a06920-7449-74d0-9b09-57855a012572/implement_042 — Began the admitted radial-saw duel after reading the frozen contract, source audit, active architecture, and prior vertical-slice patterns; a dedicated cold target was proved absent and the first locked workspace test passed all 29 tests.
- 2026-09-04T13:42:59.319Z stage implement end session codex:01a06920-7449-74d0-9b09-57855a012572/implement_042 — Implemented one authoritative radial-saw replay, ordinary-damage result boundary, progressive UDP projections, shared Bevy presentation, source-ledger correction, and proportional lifecycle checks without the rejected effects.
- 2026-09-04T13:42:59.320Z stage verify start session codex:01a06920-7449-74d0-9b09-57855a012572/implement_042 — Began final locked checks, source-bound capture, live two-client evidence, monitor-4 playback, artifact inspection, and residue audit from the named cold candidate.
- 2026-09-04T13:55:43.034Z stage verify end session codex:01a06920-7449-74d0-9b09-57855a012572/implement_042 — Passed formatting, strict clippy, locked build, all 33 tests, exact-PTS audit, eight inspected anchors, progressive two-client/live-render agreement, hidden-first monitor-4 playback, ticket/diff checks, and bounded residue cleanup.
- 2026-09-04T13:59:02.426Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a06cb6-b503-7d30-8c1f-833acc660a9b — Began independent delivery review of the exact source-bound replay, authority and two-client path, native-resolution presentation evidence, lifecycle checks, and proportional implementation.
- 2026-09-04T14:12:13.793Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/01a06cb6-b503-7d30-8c1f-833acc660a9b — Rejected the candidate because traversal left the rendered arena, projectile and impact labels lacked visible events, adjacent-frame result dimming was too weak, the background was geometric rather than paper-brush, and the initial review marker omitted the parent session identity.
- 2026-09-04T14:15:09.838Z stage correction start session codex:01a06920-7449-74d0-9b09-57855a012572/implement_042 — Began correcting the rejected candidate against the original-resolution source pairs: visible collision support, event-aligned anchors, adjacent-frame dimming, and a dense irregular paper-brush field.
- 2026-09-04T15:26:08.569Z stage correction end session codex:01a06920-7449-74d0-9b09-57855a012572/implement_042 — Replaced invisible rectangular support with rendered authority surfaces, aligned real projectile and impact evidence through the exact tick-908/tick-909 boundary, strengthened onset dimming, and rebuilt the background as dense irregular brush work.
- 2026-09-04T15:26:08.570Z stage verify start session codex:01a06920-7449-74d0-9b09-57855a012572/implement_042 — Began final correction verification with locked gates, fresh source and clone evidence, live two-client agreement, guarded monitor-4 playback, and bounded residue cleanup.
- 2026-09-04T15:28:08.796Z stage verify end session codex:01a06920-7449-74d0-9b09-57855a012572/implement_042 — Passed focused and full locked gates, reproduced all five exact-PTS RGBA hashes, inspected all source/clone pairs, proved two-client received-state agreement and hidden-first monitor-4 playback, and reduced evidence to one bounded final set.
- 2026-09-04T15:31:39Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a06cb6-b503-7d30-8c1f-833acc660a9b — Began exact-tip re-review of the corrected collider topology, source-aligned traversal and events, result dimming, paper-brush field, and refreshed evidence.
