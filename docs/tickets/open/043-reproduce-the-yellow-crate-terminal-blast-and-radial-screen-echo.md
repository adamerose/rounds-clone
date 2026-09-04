---
format: 3
status: idea
created: 2026-09-04T17:00:46Z
origin: system-detected
tags: ["bevy", "physics", "fidelity", "multiplayer", "presentation", "projectiles", "vertical-slice"]
value: 10
risk: 4
sessions:
  - codex:01a06920-7449-74d0-9b09-57855a012572
execution: unattended
depends-on: [42]
supersedes: []
split-from: [18, 20]
---

# Reproduce the yellow-crate terminal blast and radial screen echo

Reconstruct the continuous yellow-crate arena interval from PTS 4220149786, or 07:02.014979, through the established `ROUND ORANGE` result at PTS 4245983016, or 07:04.598302, in `reference/MedalTVRounds20260903165304088.mp4`.
This slice turns the user's strongest supplied screen-effect example into a playable authority-owned event: a terminal close-range impact dislodges real arena pieces, eliminates blue, and drives the source's bright local burst, screen-wide radial echo, chromatic separation, trails, and result handoff on both local and received-state render paths.

## Outcome

- A named `yellow-crate-terminal-blast-replay` advances continuously for the source interval's 155 decoded frames: calm top-right combat, terminal impact, physical crate response, peak shock, trailing particles, and `ROUND ORANGE`.
- The arena reproduces the source's staggered field of bright yellow trapezoidal platforms, long shadows, brown stacked pieces, dark teal paper background, top-right convergence, and full 1280x720 framing.
- Brown arena pieces are stable-ID Rapier rigid bodies with real colliders, contact, mass, friction, restitution, sleep/wake state, and ordered snapshots. The terminal impact applies authority-owned impulses; at least the source-visible nearby piece becomes airborne, rotates, and continues under physics rather than a pose script or render-only debris animation.
- The terminal hit arises from the existing authoritative input, projectile/contact, damage, explosion, and round-result path. It eliminates blue and awards orange without claiming the unseen original card formula, damage constant, explosion radius, or crate material values.
- One stable impact/explosion event carries the authoritative tick, owner, target, position, damage outcome, and gameplay impulse needed by clients. Render-only bloom, particles, screen sampling, chromatic offsets, shake, and audio cues never enter simulation hashes or feed back into physics.
- One authority and two separately launched UDP clients submit inputs throughout the replay and receive the same ordered fighter, projectile, impact, explosion, dynamic-crate, winner, score, and phase state. At least one visible/offscreen render consumes received snapshots instead of advancing combat, crate physics, or result state locally.
- The shared Bevy presentation reproduces the observed white-hot core, yellow/orange lobes, sparks, directional trails, screen-wide outward zoom/warp, discrete green/yellow/red scene echoes, brief shake, abrupt result dimming, score circles, and `ROUND ORANGE` text on both visible and offscreen paths.
- Bevy's existing bloom, chromatic-aberration, lens-distortion, camera, sprites/meshes, and particles remain the base effect stack. Add exactly one bounded project-owned fullscreen multi-tap pass for the source-proved repeated scene echoes that those single-sample built-ins cannot reproduce; do not fork, replace, or expose a general custom renderer.
- Programmatic capture emits source-bound frames for calm state, first response, local burst, peak radial echo, trailing streaks, last combat, adjacent result onset, and established result. Every anchor records source PTS/hash, replay tick, authoritative state/dynamic-body/round digests, executable and renderer identity, and clone-frame hash.
- `docs/fidelity/yellow-crate-terminal-blast-observations.md` remains the source contract and `docs/fidelity/footage-coverage.md` identifies the corrected 07:00–07:10 content without marking this proposed child implemented.
- This child advances arena umbrella `018` and overall-presentation umbrella `020`; it closes neither and does not claim complete-match replay progress for ticket `022`. It neither implements nor advances ticket `017`'s unmodified base-projectile presentation. Any source observation that may involve a card-modified projectile remains evidence for ticket `019` to verify, not an implementation claim for that card-mechanics ticket.

## Decisions

- The binding source is recording SHA-256 `453954a7230401ed805be4e53dec41779a1913dfd69903671fc131fca2c8a18c`, source PTS 4220149786 through 4245983016 at time base `1/10000000`.
- Canonical source-frame identity is SHA-256 over exactly 3,686,400 native 1280x720 RGBA bytes decoded with FFmpeg `7.1-essentials_build-www.gyan.dev`, libavcodec `61.19.100`, and `libaom-av1`. Select by integer source PTS with timestamps preserved; PNG-container hashes, frame ordinals, and wall-clock seeking do not bind acceptance.
- Consecutive frames establish the event boundary: PTS 4233483066 is the last calm frame, PTS 4233649732 is the first whole-frame response, PTS 4234983060 is the peak radial echo, PTS 4238483046 is the last combat frame, and PTS 4238649712 begins the result on the immediately following frame.
- Treat the visible repeated platform silhouettes as a post-composite screen response. Bloom, one lens warp, and one chromatic offset cannot generate the several discrete radially separated copies visible across the whole viewport, including HUD elements; that concrete gap justifies one fullscreen multi-tap pass but not a custom renderer.
- Derive the effect envelope deterministically from the stable authority event and replay tick. The first response reaches peak in about eight frames (0.133 seconds), leaves directional streaks by about 0.350 seconds, and is cut into the result transition after about 0.483 seconds rather than being allowed an invented long decay.
- Reuse the existing replay profile, Rapier boundary, stable snapshots, wire format, shared scene, capture command, and automation smoke. Do not add a slice-specific renderer, physics loop, local-client simulation, or evidence launcher.
- Use ECS for durable fighters, projectiles, crates, and gameplay events that participate in collision or network projection. Keep the fixed platform layout, source anchors, effect envelope, shader parameters, particle pool, and capture schedule as ordinary data/resources unless composition requires an entity.
- The audio tracks provide timing evidence but not reusable source assets. Preserve an event hook aligned to the impact; original impact/result audio implementation and timbre comparison remain with umbrella ticket `020` rather than being improvised in this visual/physics slice.
- Keep evidence proportional: protect the authority event and real crate response, one exact two-client projection, one actual GPU post-process path, and one bounded source/clone sequence. If tests plus evidence support exceed the product behavior added by this slice, simplify before review.

## Essential constraints

- Impact, crate response, effect envelope, and result transition are one continuous playable run, not disconnected setup captures.
- Gameplay damage, elimination, score, crate contacts, and impulses are authority-owned; clients may interpolate received transforms but cannot resimulate or infer them.
- The fullscreen pass consumes the final composited scene and cannot mutate authority state, become a public renderer framework, or require an alternate offscreen implementation.
- Source and clone comparisons use the same 1280x720 viewport and include consecutive-frame motion, not only isolated attractive stills.
- Visible verification starts hidden, verifies the exact monitor-4 identity at `(364,-1080)` with physical size 1920x1080, then shows the game; absence or ambiguity fails closed.
- The localhost UDP adapter remains development multiplayer evidence and cannot be described as production networking.

## Non-goals

- General implementations or numeric claims for `EXPLOSIVE BULLET`, `COMBINE`, `CAREFUL PLANNING`, `DAZZLE`, or any other card badge visible in the recording.
- Base-projectile presentation owned by ticket `017`, or verification and presentation of any card-modified projectile owned by ticket `019`.
- Every yellow arena, every explosive hit, general destructible terrain, arbitrary fracture, or a full dynamic-arena catalog.
- Extracting, shipping, or matching proprietary shader/audio/texture bytes; pixel-identical particles; inferred hidden camera, physics, damage, or card constants.
- A renderer fork, replacement render pipeline, general post-processing framework, multiple effect passes for convenience, or shader parameters crossing the gameplay wire.
- Final audio fidelity, music, full-match replay, self-play, prediction, rollback, reconnect, relay/NAT traversal, lobbies, matchmaking, Steamworks, anti-cheat, or cross-platform lockstep.
- Closing tickets `018`, `019`, `020`, or `022`.

## Evidence required

- An exact-PTS audit independently reproduces every canonical RGBA hash in `docs/fidelity/yellow-crate-terminal-blast-observations.md` before tuning or comparison begins.
- Original-resolution consecutive-frame inspection proves the last-calm/first-response pair, eight-frame rise to peak, physical crate displacement, directional trail phase, last-combat/result-onset pair, and established result.
- Focused simulation evidence proves stable crate identity and ordering, real Rapier crate/platform/player contacts, an authority-owned terminal hit and explosion, blue elimination, orange score/result, and deterministic same-build replay output. Perturbing the impact impulse changes the protected crate trajectory or terminal state rather than only a configuration field.
- Wire round-trip and live network evidence launch one authority plus two client processes, advance one input from each client per tick, and prove progressive and final agreement for combat, impact/explosion, crates, winner, score, and phase. A frame rendered from a received snapshot carries the same authority digests.
- The actual Bevy GPU path emits no fewer than eight 1280x720 anchors spanning calm through established result. Captures prove bloom, local particles, multi-tap radial scene echoes, chromatic separation, and result dimming rather than substituting labels or a CPU image mock.
- A source/clone comparison records impact position, arena silhouette, which nearby crate moves, effect onset/peak/result timing, approximate radial direction and viewport reach, echo count/spacing, color ordering, particle/trail direction, HUD participation, camera response, and result cadence.
- A reviewer inspects the source and clone anchors at original resolution and at least one short side-by-side or alternating sequence; reduced contact sheets alone are insufficient.
- The impact audio audit is retained as timing evidence or explicitly cited as deferred to ticket `020`; no source audio byte is committed or shipped.
- The visible command-line replay passes the monitor guard, traverses impact and result, exits boundedly, and leaves no process or window residue. Induced partial startup and mid-replay failure also leave no child process.
- `cargo test --workspace --locked -- --nocapture` passes first from one verified-absent isolated target, followed by format, strict lint, locked build/test, exact-PTS audit, source/clone inspection, live two-client smoke, visible playback, ticket, whitespace, hash, and residue checks.
- A line/responsibility inventory separates product, tests, and automation support and rejects test-only physics/rendering, duplicate capture paths, or more test/support code than product behavior added by the slice.

## Work log

- 2026-09-04T17:00:46.632Z stage design start session codex:01a06920-7449-74d0-9b09-57855a012572/reflect_043 — Began turning the exact-PTS yellow-arena audit into one bounded physics, multiplayer, and screen-response contract.
- 2026-09-04T17:03:54.507Z stage design end session codex:01a06920-7449-74d0-9b09-57855a012572/reflect_043 — Drafted an idea contract around the adjacent-frame blast/result boundary, real crate response, one source-justified fullscreen pass, two-client received-state rendering, and proportional evidence.
- 2026-09-04T17:12:18Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a06d65-c551-78a3-8526-feecb1a4dd86 — Began independent admission review of the exact source interval, crate-physics and result boundary, one-pass rendering conclusion, multiplayer evidence, provenance, and proportional scope.
- 2026-09-04T18:07:53Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/01a06d65-c551-78a3-8526-feecb1a4dd86 — Admission review ended without a verdict when the isolated reviewer handle disappeared across the continuation boundary; no approval or finding was inferred.
- 2026-09-04T18:08:59Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a06d9b-afc7-7db1-8001-364cbaafb1db — Restarted independent admission review from a fresh root, candidate, ignored-artifact, and unique temporary-directory baseline after the prior reviewer handle disappeared.
- 2026-09-04T18:22:16Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/01a06d9b-afc7-7db1-8001-364cbaafb1db — Rejected admission because ticket 017 did not own this modified terminal effect, the editable umbrella tickets retained contradictory Godot guidance, and the observation record failed the whitespace gate; source and Bevy-pass evidence otherwise held.
- 2026-09-04T18:24:39.810Z stage correction start session codex:01a06920-7449-74d0-9b09-57855a012572/correct_043_contract — Began correcting the rejected projectile-ownership provenance, stale Godot-era parent guidance, and observation-record EOF whitespace without changing the approved source, rendering, or audio findings.
- 2026-09-04T18:25:37.334Z stage correction end session codex:01a06920-7449-74d0-9b09-57855a012572/correct_043_contract — Removed ticket 017 from provenance and implementation claims, routed modified-projectile verification to ticket 019, updated only the directly relevant parent runtime gates to the shared Bevy path, removed the observation EOF blank line, and passed ticket, whitespace, status, and frozen-ticket checks.
- 2026-09-04T18:26:50Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a06d9b-afc7-7db1-8001-364cbaafb1db — Began focused re-review of the corrected ownership, editable-parent Bevy guidance, whitespace, and complete ticket 043 contract.
