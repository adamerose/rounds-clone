---
format: 3
status: ready
created: 2026-09-05T00:15:08Z
origin: human-request
tags: ["bevy", "match-flow", "physics", "multiplayer", "fidelity", "vertical-slice"]
value: 10
risk: 4
sessions:
  - codex:01a06920-7449-74d0-9b09-57855a012572
execution: unattended
depends-on: [40, 41]
supersedes: []
split-from: [18, 19, 20, 22]
---

# Connect rematch, drafts, timber combat, and result in one playable session

Turn the already implemented rematch/draft and timber-collapse slices into one continuous authoritative match segment matching recording `453954a7…a18c` from the pre-victory frame at PTS 1595160286 through the final result-only frame at PTS 2351823926. Today those scenes are separate scripted replay profiles. The delivered path must let players or automation accept the rematch, make both card picks, fight through blue's first-half win, load the timber arena with those loadouts intact, cause the physical collapse through ordinary projectile contact, and reach orange's answering half win without rebuilding state or switching showcase profiles.

## Outcome

- One authority advances continuously through blue's prior victory, both rematch votes, the orange and blue drafts, resumed combat, blue's elimination win and `HALF BLUE` result, the next timber-arena load, the timber explosion and collapse, orange's answering elimination win and `HALF ORANGE` result.
- Score, phase, selected cards, fighter state, arena bodies, constraints, projectiles, impacts, and result remain in one authoritative match instance. No step reconstructs a later replay state, copies state between profiles, or advances a presentation-only model.
- `DAZZLE` and `EXPLOSIVE BULLET` persist from their actual draft confirmations and affect the ensuing projectiles. The timber release and terminal result originate from submitted player actions and the normal projectile/contact/damage/explosion/physics path, not unconditional tick triggers.
- Human keyboard/controller input, a small programmatic semantic-input route, and UDP clients use the same `PlayerInput` and `FlowCommand` boundaries. Automation may replay a source-shaped action trace, but it receives progressive observations and is not a separate simulation or test-only authority.
- Two separately launched UDP clients submit actions throughout the multi-phase session and converge on every progressive authority snapshot. This proves the existing development transport only; production prediction, rollback, matchmaking, relay/NAT traversal, and Steam integration remain unresolved.
- The shared visible/offscreen Bevy renderer composes the existing draft, arena, physics, effects, HUD, and result presentation from received match state. No new evidence launcher, renderer, capture stack, or profile-specific visual branch is added.
- A source audit binds canonical native-frame PTS and hashes across the previously omitted `HALF BLUE` result and timber-arena handoff and the later `HALF ORANGE` result, while reusing the already checked ticket 040 and 041 anchors for their intervals. It stops before the result begins crossfading into the next ice arena.
- Architecture and fidelity records describe the continuous route and leave the rest of both recordings, production multiplayer, audio fidelity, and unimplemented cards explicit.

## Decisions

- The binding source is `reference/MedalTVRounds20260903165304088.mp4`, SHA-256 `453954a7230401ed805be4e53dec41779a1913dfd69903671fc131fca2c8a18c`. The connected interval begins at PTS 1595160286, native RGBA SHA-256 `8ed98510d1b11746b36f4bee6d577e2d42aef6f8012fa63c5eae07a7de8cf010`, and ends at the last clean result-only frame, PTS 2351823926, hash `9438b1bb537ad6b651729ed374d9f5ddf6d9badfa232df6cb34779ed03647ab2`. The immediately following frame, PTS 2351990592/hash `ca82da2a0b4dead0fafd53be2ba3a3d6c8317b9c76649ebcbe7ce35655bb5405`, is the first with visible ice geometry and is outside the interval. Canonical identity is SHA-256 over exactly 3,686,400 decoded 1280×720 RGBA bytes with timestamps preserved by FFmpeg 7.1/libaom-av1; wall-clock requests and output-frame ordinals do not bind acceptance.
- Direct native-frame audit fixes four intermediate boundaries: PTS 2029991880/hash `c5c3b9d31675d2317020c1f968ed0c4688656612f2e72e9550d9542e832e1a94` shows established `HALF BLUE`; PTS 2045158486/hash `b0307fab233516c2cea436aab85c5e29bfdd8c55d3fc238a6f11d6bef996e02b` shows its tail; PTS 2050158466/hash `9b1e056c1d00ed9890a4ae941f62ef4d9b6112a04eb04156a394d5fa3ae8631d` shows the timber arena established; and PTS 2340157306/hash `66d02502fd61186fff2da80ae1775a9b0546a02cf9310962b25e5a033f11f0e7` shows established `HALF ORANGE`. PTS 2360157226 already shows established ice combat and is explicitly outside this ticket.
- Reuse ticket 041's authoritative rematch, draft, registry, and typed card state and ticket 040's Rapier timber bodies, joints, impact, collapse, snapshots, and shared rendering. Refactor only the seams that prevent those systems composing in one match.
- Scenario-specific source actions, arena layout, source anchors, and presentation timing are ordinary configuration/data. Durable fighters, projectiles, dynamic bodies, constraints, and gameplay events remain ECS entities. Flow and match ownership stay authoritative resources. Do not grow the existing `ReplayProfile` branch count to join the scenes.
- The footage proves visible outcomes and order, not hidden original constants. Preserve directly observed cadence and causality without claiming unseen card formulas, physics values, or network architecture.
- The programmatic route emits bounded progressive semantic observations already owned by the match snapshot and optionally shared-renderer frames. It must be sufficient to choose a later legal action from current state, but it does not inherit ticket 032's private-desktop, Win32 lifecycle, elaborate artifact-identity, or pixel-only callback machinery.
- Keep evidence proportional: one connected lifecycle regression, one draft-to-projectile-to-collapse causality regression, one two-client smoke, and source-paired GPU anchors at the newly connected transitions. If test plus automation growth approaches the product code added, simplify before review.

## Essential constraints

- The ordinary runtime path, not an evidence helper, owns every phase transition and gameplay consequence.
- The connected path accepts live semantic input; the source-shaped trace is one client of that path, not hard-coded authority behavior.
- Gameplay state is server-authoritative. Clients may present received state but cannot infer hits, collapse, score, or draft results locally.
- Existing ticket 040 and 041 behavior remains available without duplicating their physics, card, flow, network, or renderer implementations.
- Visible verification follows the hidden-first monitor-4 guard; offscreen and headless routes remain the normal development path.

## Non-goals

- Complete production netcode, rollback/prediction, reconnect, lobbies, matchmaking, relay/NAT traversal, Steamworks, anti-cheat, or cross-platform deterministic lockstep.
- All cards, the ice arena that follows the final result, other arenas/rounds, audio, menus, settings, or remaining footage intervals.
- A general bot framework, 10,000-match self-play, a pixel-only model protocol, custom physics, renderer replacement, or another evidence launcher.
- Exact imitation of hidden ROUNDS constants or proprietary assets.

## Evidence required

- The exact-PTS audit reproduces the bound native hashes and records direct observations for `HALF BLUE`, the timber-arena handoff, and `HALF ORANGE`, and references the retained ticket 040/041 anchors for the intervening scenes.
- A connected regression begins at the prior victory and reaches `HALF ORANGE` through accepted rematch votes, both legal drafts, resumed player input, blue's first elimination/half award and result cadence, timber-arena loading, real projectile contact, Rapier collapse, orange's answering elimination/half award, and the resulting 1–1 half state in one match instance. Perturbing the selected card or impact input changes a protected downstream outcome.
- A programmatic driver chooses at least one later legal combat action from a progressive observation instead of submitting only a precomputed full-session array. The same public input route can be driven by the visible client.
- One authority and two UDP client processes traverse every phase, exchange per-player actions, and agree progressively and finally on flow, loadouts, fighters, projectiles, dynamic bodies, impacts, score, winner, and phase. Failure cleanup leaves no attributable process.
- The actual shared Bevy GPU path captures source-paired anchors at the draft-to-arena handoff, resumed combat, `HALF BLUE`, timber-arena load, intact timber, impact/collapse, last combat, first orange-result onset, established `HALF ORANGE`, and its result-only tail. At least one received-state client frame is included.
- Original-resolution review checks phase continuity, card persistence, arena silhouette, collapse causality, effect timing, score/HUD continuity, and result cadence. Differences remain explicit rather than being hidden by contact sheets or labels.
- Format, strict lint, locked build, the full existing suite, the focused connected checks, source-hash audit, two-client smoke, bounded offscreen/visible playback, ticket checks, diff checks, and process/window/artifact cleanup pass using the repository's capped reusable Cargo target policy.
- A responsibility inventory rejects duplicate simulation/render/network paths and any support/test growth larger than the product behavior added.

## Work log

- 2026-09-05T00:15:08Z stage design start session codex:01a06920-7449-74d0-9b09-57855a012572 — Reflected on the next footage-derived product gap after integrating the first five Bevy slices and identified disconnected replay profiles, rather than missing isolated effects, as the highest-value constraint.
- 2026-09-05T00:16:05Z stage design end session codex:01a06920-7449-74d0-9b09-57855a012572 — Drafted one continuous playable rematch-to-half-result contract that composes the existing card, physics, networking, and renderer paths while forbidding another launcher or profile-specific branch layer.
- 2026-09-05T00:17:10Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/admit_045 — Began independent admission review against the source footage, current profile-owned architecture, closed prerequisites, live-input boundary, and proportional evidence requirement.
- 2026-09-05T00:21:55Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/admit_045 — Rejected admission because the contract omitted the visible `HALF BLUE` result and next-arena handoff, then mislabeled an endpoint that already included the following ice arena as orange-result footage.
- 2026-09-05T00:22:45Z stage correction start session codex:01a06920-7449-74d0-9b09-57855a012572 — Began correcting both source gaps with exact native PTS and RGBA hashes rather than relying on the coverage ledger's representative ten-second labels.
- 2026-09-05T00:25:52Z stage correction end session codex:01a06920-7449-74d0-9b09-57855a012572 — Bound the full connected lifecycle to both half results, the 1–1 half state, the intervening timber load, and an exact result-only endpoint before the next ice arena.
- 2026-09-05T00:26:35Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/admit_045 — Began focused re-review of both corrected lifecycle gaps, every bound native hash, live-input ownership, dependencies, and proportional scope.
- 2026-09-05T00:29:40Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/admit_045 — Rejected the otherwise admissible contract because its chosen clean result-tail anchor was not the actual final result-only frame.
- 2026-09-05T00:30:13Z stage correction start session codex:01a06920-7449-74d0-9b09-57855a012572 — Began replacing the selected tail anchor with the independently reproduced adjacent-frame boundary where ice geometry first appears.
- 2026-09-05T00:30:13Z stage correction end session codex:01a06920-7449-74d0-9b09-57855a012572 — Bound the endpoint to PTS 2351823926 and recorded the immediately following ice-crossfade frame as explicitly out of scope.
- 2026-09-05T00:30:35Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/admit_045 — Began final focused admission review of the corrected adjacent-frame endpoint and complete contract.
- 2026-09-05T00:31:05Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/admit_045 — Approved admission at risk 4 with no findings after independently reproducing the last result-only frame and first ice-crossfade frame and regressing feasibility, dependencies, live-input ownership, and proportional scope.
- 2026-09-05T00:31:18Z stage design end session codex:01a06920-7449-74d0-9b09-57855a012572 — Admitted ticket 045 as the continuous rematch-to-two-halves product slice.
