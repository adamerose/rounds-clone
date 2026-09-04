---
format: 3
status: idea
created: 2026-09-04T08:25:00Z
origin: human-request
tags: ["bevy", "cards", "flow", "multiplayer", "fidelity", "vertical-slice"]
value: 10
risk: 4
sessions:
  - codex:01a06920-7449-74d0-9b09-57855a012572
execution: unattended
depends-on: [40]
supersedes: []
split-from: []
---

# Reproduce the rematch and card-draft handoff

Reconstruct the complete transition visible from 02:40.00 through 03:20.00 in `reference/MedalTVRounds20260903165304088.mp4`: the prior duel ends, the rematch prompt resolves, each player receives and chooses from a readable card fan in turn, the picked card is revealed on the expressive player presentation, and both upgraded fighters return to combat. This is the first source-bound implementation of the match shell around the existing combat slices. The outcome is an authoritative multiplayer flow that a person or automation client can drive without an editor, not a disconnected menu mock or a scripted video.

## Outcome

- `rounds-sim` gains an explicit authoritative flow state covering combat conclusion, rematch choice, ordered per-player drafts, card reveal/handoff, arena transition, and resumed combat. Match flow, draft RNG, offers, selection, scores, and player loadouts are stable project data/resources rather than renderer state or implicit timers.
- A stable item registry represents all ten offers in this source interval, including title, rules text, rarity/palette, art key, and typed gameplay modifiers. Orange selects `BURST` and blue selects `EXPLOSIVE BULLET`; both are applied once to the correct fighters, persist into resumed combat, and visibly affect real projectiles. Cards that are only offered remain honest descriptive data, not fake implemented mechanics.
- The authority alone creates deterministic offers from a recorded seed, decides whose turn it is, validates hover/selection/confirm/rematch inputs, advances phase timers, and applies the choice. Both real clients receive the same ordered phase, offer, selection, reveal, score, and loadout state. Invalid, stale, duplicate, and non-active-player inputs cannot change the outcome.
- The actual Bevy client reproduces the interval's darkened arena-to-prompt transition, large outlined `REMATCH?` choice, alternating full-screen orange/blue player reveal, five-card curved fan with readable highlighted face, dimmed neighboring faces, rarity-specific color and line art, hand/arm selection gesture, selection pop and reveal, and fade back into the arena. Presentation follows received authority state and never owns the draft result.
- Keyboard/controller input and a bounded automation command drive the same public flow actions. A named `rematch-draft-replay` profile reproduces the observed phase order and selected-card handoff without embedding renderer-only shortcuts in simulation.
- Programmatic capture emits source-bound original-resolution comparison anchors for the rematch prompt, each player's initial offer, hover, confirmation/reveal, the handoff between players, and resumed combat. Metadata binds source timestamp/hash, authoritative flow digest, loadout digest, renderer identity, and frame hash.
- The implementation extends the existing server, client, automation, capture, and inspection seams. Product behavior remains larger than the tests and support added for the slice, and every check maps to a user-visible or multiplayer-authority risk.

## Decisions

- The binding source is recording SHA-256 `453954a7230401ed805be4e53dec41779a1913dfd69903671fc131fca2c8a18c`, interval 02:40.00–03:20.00. Direct frame decoding recorded in `docs/fidelity/rematch-draft-observations.md` confirms `VICTORY!`, `REMATCH? YES NO`, the exact orange and blue offers and readable rules, orange's `BURST` pick, blue's `EXPLOSIVE BULLET` pick, the blank/fade handoffs, the return to a new arena, the persistent `Bu`/`Ex` loadout badges, and the first upgraded projectile exchange. Generated extracts remain ignored under `out/ticket-041/`; the recording is never modified.
- Use ECS for fighters and durable item-derived capabilities that participate in gameplay composition. Keep phase, active drafter, scores, deterministic draft state, item definitions, and stable registries as ordinary resources/data. A card face is a projection of one item definition, never the source of its rules.
- Network the authoritative semantic state and player actions, not UI transforms or animation frames. Local presentation may interpolate and animate received phase progress, but it may not predict a confirmed pick or mutate a loadout.
- Typed modifiers must name the gameplay property they change. Do not introduce stringly typed effects, a general scripting language, reflection-based patching, or one component type per card merely to make the first draft screen work.
- Faithfulness includes character expression, card readability, depth, motion, timing, and handoff cadence. If readable rules text cannot fit at the 1280×720 comparison viewport, fix layout/type scale rather than omitting it from evidence.
- Keep the complete-card promise honest. This ticket establishes the registry, offers, picks, persistence, and the two selected source effects that are visible on return to combat. The other eight offers need accurate displayed metadata but not unobserved combat implementations. Remaining cards and stacked interactions stay assigned to `S5-card-combat` and require their own footage-bound tickets.
- Preserve the command-line and hidden-first monitor-4 boundary. Visible evidence uses the shared renderer and exact physical-display guard; offscreen evidence remains the default for iteration.

## Non-goals

- Every card in both recordings, balance-equivalent hidden constants, rarity distribution across long sessions, card stacking beyond this interval, or a general modding API.
- Steam lobbies, relay/NAT traversal, rollback prediction, account identity, public matchmaking, or production reconnect behavior.
- Every round/half score transition, final match victory, complete rematch lifecycle beyond the observed prompt-to-new-draft path, or every arena transition.
- Final audio fidelity, localization, accessibility remapping, mouse-only card dragging, or pixel-identical character illustration.

## Evidence required

- A checked observation record fixes exact timestamps for no fewer than eight anchors and records every readable card title/rules line, which player acts, hover/selection order, phase timing, scores, and the first resumed-combat evidence of the selected modifiers. Facts visible in the recording remain separate from inferred seeds, timers, and hidden effect constants.
- Focused simulation evidence proves legal phase order, deterministic offers, active-player ownership, one-time selection, loadout persistence, typed modifier application, stale/duplicate/invalid-input rejection, and repeatable flow/loadout digests. An intentional offer seed or selection perturbation changes the protected outcome.
- Live network evidence launches one authority and two separately launched clients, advances phase actions through both drafts, observes progressive state on both clients, proves both receive the same offers, selected IDs, phase sequence, scores, and final loadouts, and leaves no child process when startup or mid-flow input fails.
- The actual Bevy offscreen path produces 1280×720 PNGs for at least eight named anchors. Evidence proves it renders received state and that presentation animation cannot change flow or loadout digests.
- Visual inspection compares source and clone at original resolution and records concrete similarities and remaining differences in prompt typography, card fan geometry, card readability, rarity treatment, player silhouettes/expressions, selection motion, background, fades, and cadence. Labels must not imply frame-exact equivalence.
- A visible command-line replay passes the existing hidden-first exact monitor-4 guard, accepts real pick input, returns to combat, exits boundedly, and leaves no process or window residue.
- `cargo test --workspace --locked -- --nocapture` passes as the first Cargo command from one verified-absent isolated target, followed by formatting, strict lint, locked build/tests, replay inspection, live UDP flow smoke, offscreen capture, visible playback, ticket checks, exact-range whitespace checks, hashes, and residue checks.
- A line/responsibility inventory accounts separately for product, test, and automation-support growth. If tests plus support approach the product code added for this slice, stop and simplify before delivery review.

## Work log

- 2026-09-04T08:25:00.000Z stage design start session codex:01a06920-7449-74d0-9b09-57855a012572 — Began the first footage-bound match-shell slice after publishing the dynamic collapse, choosing the rematch-to-two-player-draft interval because it exercises authoritative flow, persistent item data, readable high-character presentation, live clients, and return to combat together.
- 2026-09-04T08:33:00.000Z stage design end session codex:01a06920-7449-74d0-9b09-57855a012572 — Fixed the source interval, exact offers and choices, readable rules, phase anchors, return-to-combat badges, selected projectile behaviors, authoritative ownership, presentation contract, evidence boundaries, and proportionality gate from direct headless frame decoding.
