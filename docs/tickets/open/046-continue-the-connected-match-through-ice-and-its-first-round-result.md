---
format: 3
status: ready
created: 2026-09-05T23:37:39Z
origin: system-detected
tags: ["product-fidelity", "match-flow", "arenas", "bevy"]
value: 9
risk: 4
sessions:
  - codex:01a073e9-17ec-7170-933a-0e18a071972d
execution: unattended
depends-on: [45]
supersedes: []
split-from: [18, 20]
---

# Continue the connected match through ice and its first round result

The connected Bevy match currently stops after the timber duel with one half per player, so neither player can finish even the first full round. Continue that same playable session into the footage's ice arena and deciding duel, then show the first full-round award with the correct half progress, round pip and retained cards.

## Outcome

- The existing rematch route continues from both drafts and the first two arena fights through the incoming ice arena, live third combat, ordinary elimination and the first full-round result. One authority, ECS world and private Rapier boundary persist; no later snapshot is reconstructed or copied from another replay profile.
- The source-shaped blue/orange/blue sequence reaches the ice arena with half progress 1–1 and completed rounds 0–0, then awards exactly one blue round. The result first preserves both previous half circles, fills blue's second half, shows ROUND BLUE and moves the award into the first blue HUD pip. Orange's half remains through the bound result, and Da/Ex remain attached to their actual draft loadouts.
- Legal live input can produce either winner. A player earning two wins in the first two arenas receives the first full-round award there; a split requires the deciding ice duel. No winner, elimination or award is chosen by the source trace's tick, arena number or player color, and no result is counted twice while the result remains displayed.
- The ice arena provides footage-matched spawns, static platform and spire contours, collision, ordinary traversal, aiming, jumping, shooting and blocking. Existing card capabilities affect its projectiles and hits. Its pale/cyan animated surfaces, long dark shadows, source-shaped combat feedback and incoming/outgoing arena motion appear in the same visible/offscreen Bevy scene used for received snapshots.
- The normal local keyboard/controller path and two separately launched UDP clients can drive the extended route using existing semantic input. The clients agree with every progressive authority observation, including distinct current-half progress and completed rounds, retained loadouts, arena, fighters, projectiles, elimination and result phase.
- README, architecture and fidelity coverage explain how to play and verify through this first full round. They keep the following card draft, later rounds, other footage, audio and production multiplayer unresolved.

## Decisions

- The source is `reference/MedalTVRounds20260903165304088.mp4`, SHA-256 `453954a7230401ed805be4e53dec41779a1913dfd69903671fc131fca2c8a18c`. The extension starts at PTS 2351990592/hash `ca82da2a0b4dead0fafd53be2ba3a3d6c8317b9c76649ebcbe7ce35655bb5405`, immediately after ticket 045's endpoint, and ends at the selected established-round PTS 2506156642/hash `ae4d4d943d9ec939064a0d9d4a3b08a8d6f31cb820639d297bf9caf1c5333a32`. This endpoint precedes the next draft; it is not labeled the final result-only frame.
- `docs/fidelity/ice-round-observations.md` binds twelve native RGBA source anchors, including adjacent PTS 2484823394/2484990060 for the undimmed/result boundary. Canonical identity uses exactly 3,686,400 decoded native RGBA bytes with preserved source timestamps, not PNG bytes or a wall-clock seek label.
- Preserve the existing five `ReplayProfile` variants and the current public input, authority, transport and shared renderer boundaries. Refactor the existing flow's score/result and arena-handoff seams only as needed for this connected round. Source actions and layout are configuration; gameplay remains ordinary authoritative combat.
- Model completed rounds separately from current-half progress. A second win by one player awards a full round symmetrically; retain the losing half in this result presentation. Resetting halves for a later round, the next loser draft and its card behavior are outside this interval and must not be claimed complete.
- The observed ice contours remain intact during combat. This ticket adds no ice friction, fracture, melting or new damage rule without direct source evidence. It may reuse existing camera/chromatic/burst effects; do not introduce a new fullscreen pass or effect framework.
- Extend the existing bounded smoke session enough to carry the complete approximately 5,466-tick source route, retaining finite validation and process cleanup. Do not truncate the earlier match, inject a later state or add a profile to evade the current 5,000-tick cap.
- This bounded child contributes to arena/presentation umbrellas 018 and 020 without closing or changing their contracts, and does not inherit their retired Godot prerequisites. Existing claims and tickets 031, 032, 034 and 035 remain untouched.

## Evidence required

- Reproduce the bound source hashes and inspect the twelve native source anchors. Retain exact decoder commands and bounded source scans, including the adjacent result onset; record the measured arena geometry and explicit fidelity differences with final clone anchors.
- One connected public-input regression starts at the existing prior victory, accepts the rematch, confirms both cards, plays the earlier two arenas and reaches the ice duel and first blue round. It proves identity/loadout continuity, outgoing-body/projectile cleanup, correct half/round state and one-time scoring. Removing or redirecting the actual terminal shot changes the protected elimination/award outcome.
- A compact outcome regression exercises both colors winning the deciding duel and either color winning the first two fights. Actual player actions and ordinary damage produce those results; no test-only winner setter or direct flow-state fabrication substitutes for combat. A two-win sweep awards a round without an unnecessary ice fight, and holding a result cannot re-award it.
- The existing smoke path runs one authority and two real UDP clients through the complete source route and proves progressive and final agreement with the local host. At least one later combat action uses a current observation. One final received-state GPU frame agrees with the same authority snapshot; finite tick rejection and attributable process cleanup remain covered.
- Capture source-paired shared-GPU anchors for incoming ice overlap, established spawns, early and later traversal, projectile exchange, terminal approach/burst/response, adjacent last-undimmed/first-result frames, full-round circles and the final blue HUD pip. Review them at 1280×720 alongside the bound source frames. Confirm earlier ticket 045 behavior remains intact with its existing regressions and retained anchors, recapturing affected states when shared changes alter them.
- Run format, strict all-target lint, locked workspace build, the full suite, the connected checks, complete two-client smoke, bounded offscreen/visible playback, source/hash checks, ticket validation and `git diff --check` on the repository's capped reusable Cargo target. Verify hidden-first monitor-4 placement before showing any native window, and record any unavailable physical-controller evidence accurately.
- A concise responsibility inventory confirms that the extension adds playable match behavior without duplicate simulation/render/transport paths, another evidence launcher, or support/test growth larger than the behavior it protects.

## Work log

- 2026-09-05T23:34:39Z stage research start session codex:01a073e9-17ec-7170-933a-0e18a071972d/01a073ea-e6fa-7bd2-9ff0-85f9201f2b93 — Began bounded source decoding after reading the current Bevy flow, footage coverage, decisions, postmortems and all ten existing idea contracts.
- 2026-09-05T23:37:10Z stage research end session codex:01a073e9-17ec-7170-933a-0e18a071972d/01a073ea-e6fa-7bd2-9ff0-85f9201f2b93 — Inspected the ice duel and first blue round, reproduced the incoming ice identity, and located the adjacent undimmed/result frames; no combat ice destruction or slippery behavior is established.
- 2026-09-05T23:37:39Z stage design start session codex:01a073e9-17ec-7170-933a-0e18a071972d/01a073ea-e6fa-7bd2-9ff0-85f9201f2b93 — Shaping one connected first-round delivery with symmetric live scoring, existing input/render paths and bounded source evidence; older contracts and claims remain untouched.
- 2026-09-05T23:40:54Z — Reflection triage: keep ideas 018 and 020 as the arena and presentation umbrellas, with this bounded footage child advancing both; keep 036's frozen historical failure audit because the Bevy restart explicitly retained it and recent postmortems do not dispose of that source universe. Wait on 019 and 025 until their obsolete 16-card/51-card baseline is reshaped around the active two-card Bevy implementation and direct mechanics evidence; wait on 021 until the complete menu/input flow has footage ownership; wait on 022 until complete-match behavior exists for its 10,000-match gate; wait on 023 until the local game/settings flow exists to package; wait on 024 until a complete legal match can produce a truthful reel; wait on 037 until ticket 035 closes and its evidence ownership is resolved. No idea was abandoned or older contract modified. Missing production transport and audio remain named footage gaps; adding infrastructure ahead of the next playable round was judged lower value.
- 2026-09-05T23:40:54Z stage design end session codex:01a073e9-17ec-7170-933a-0e18a071972d/01a073ea-e6fa-7bd2-9ff0-85f9201f2b93 — Drafted the idea and twelve-anchor source record, verified the full source recording hash, and passed the release-matched ticket checker and diff whitespace check; ready for independent admission, with no product changes or Cargo/GUI execution.

- 2026-09-05T23:46:04Z — Independent admission reviewer codex:01a073e9-17ec-7170-933a-0e18a071972d/01a073f3-6a0d-76d0-8ca1-8d01b63c2c81 approved exact shaping range 8caa16182f587522fd603bafe0d7e42441af7e80..ea9cd5f1c6774ee2ba95b537e5a4f52820a8e4ff after independently reproducing all twelve native source hashes and adjacent boundaries, inspecting source frames, and checking scope, risk 4, ordering and score semantics. Contract admitted with no open human decision; implementation remains unreviewed.
