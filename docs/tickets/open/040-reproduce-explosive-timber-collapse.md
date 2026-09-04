---
format: 3
status: ready
created: 2026-09-04T05:42:59Z
origin: human-request
tags: ["bevy", "physics", "fidelity", "multiplayer", "presentation", "vertical-slice"]
value: 10
risk: 4
sessions:
  - codex:01a06920-7449-74d0-9b09-57855a012572
execution: unattended
depends-on: [39]
supersedes: []
split-from: []
---

# Reproduce the explosive timber collapse and shock presentation

Reconstruct the intact-to-collapsed timber-arena sequence visible from approximately 03:26.00 through 03:50.00 in `reference/MedalTVRounds20260903165304088.mp4`. This is the deliberate stress test for the chosen Bevy and Rapier architecture: the shipped game path must combine interacting rigid bodies, articulated hanging weights, an explosion-driven structural collapse, multiplayer authority, and the high-energy screen treatment visible in the supplied footage. A canned animation, decorative debris layer, or renderer-only mock is not the outcome.

## Outcome

- `rounds-sim` adds stable project-owned identities and bounded snapshot records for dynamic arena bodies and constraints while keeping Bevy entity IDs and Rapier handles private. The authoritative world contains the source interval's central stacked timber silhouette, two suspended circular side weights, the wide reactive floor, fighters, and projectiles.
- Timber pieces are real Rapier rigid bodies with colliders, mass, friction, restitution, contact, sleep/wake state, and stable ordering. The side weights use real project-owned constraint definitions backed by Rapier joints. Bodies support fighters, collide with bullets and one another, receive radial explosion impulses, break or release the source-profile connections that hold the initial silhouette, topple, pile, and settle without scripted pose playback.
- A named `timber-collapse-replay` profile reproduces the selected card-modified interval's broad sequence: intact combat, the upper-left explosive impact, immediate local breakup, whole-structure deformation, falling and rotating pieces, player/world interaction, and a persistent debris pile. It records observed results without claiming inaccessible ROUNDS constants or a general implementation of the cards active in the recording.
- The progressive authoritative snapshot includes every gameplay-relevant dynamic body, constraint state, explosion event, projectile, and fighter state needed by a remote client. Two separately launched UDP clients submit inputs throughout the replay and receive the same ordered dynamic-world outcome; a rendered client consumes received snapshots rather than resimulating or substituting local debris.
- The actual Bevy 2D presentation renders the timber faces, depth-separated dark members, suspended lines and weights, reactive floor, physically derived debris transforms, explosion core and lobes, sparks/fragments, projectile trails, bloom, camera impulse, and a short-lived screen-space radial/chromatic distortion comparable to the impact shown near 03:40. Prefer Bevy 0.19's public `Bloom`, `ChromaticAberration`, and `LensDistortion` components with ordinary cameras, materials, and particles. Add a project-owned fullscreen pass only if source-versus-clone comparison demonstrates a concrete effect those built-ins cannot produce, and record that gap before adding renderer machinery.
- The background retains the footage's dark teal layered motion while the structure and floor cast long directional shadows. Visible and offscreen paths render the same snapshot and presentation state, including the explosion envelope, at the same 1280×720 comparison viewport.
- Programmatic replay capture emits source-bound frames before impact, at the bright impact, during initial breakup, during full collapse, and after the debris pile settles. Each anchor records source timestamp/hash, tick, authoritative state hash, dynamic-body digest, renderer identity, and frame hash.
- Architecture, fidelity coverage, and decision records explain the new dynamic-world ownership, constraint lifecycle, network representation, interpolation boundary, and presentation path. The support/test inventory remains smaller than and directly mapped to the product behavior it protects.

## Decisions

- The binding source is recording SHA-256 `453954a7230401ed805be4e53dec41779a1913dfd69903671fc131fca2c8a18c`, interval 03:26.00–03:50.00. Direct decoding confirms the intact arena at 03:26, combat through 03:39.8, upper-left explosion near 03:40.4, first released pieces by 03:40.8–03:41.0, strong deformation and radial/chromatic shock by 03:41.2, a broad persistent pile by 03:43–03:45, and continued combat through 03:50; exact capture anchors still require frame-level recording before tuning claims. Generated extracts and clone captures remain ignored under `out/ticket-040/`; the supplied recording is never modified.
- Keep `bevy_rapier2d` 0.36.0, which resolves `rapier2d` 0.35.0-glamx0.2 in the pinned lock file, behind the existing `PhysicsBoundary`. Use its rigid bodies, colliders, impulse application, and joints rather than writing a second physics solver. If an observed behavior cannot be produced reliably, record the concrete Rapier limitation before considering Avian or custom physics; engine replacement is not part of this ticket.
- Dynamic arena state is authoritative gameplay state. Stable body IDs and quantized transforms/velocities cross the snapshot and wire boundaries; Bevy entities, Rapier handles, presentation particles, bloom state, and raw render data do not. The local client may interpolate received body poses for display, but it may not invent gameplay contacts or collapse results.
- Use ECS for durable fighters, projectiles, arena bodies, constraints, and effect-producing gameplay events. Keep the match phase, replay script, stable-ID registry, tuning profile, and render-only particle pool as ordinary resources/data when ECS composition offers no benefit.
- The explosion is both a gameplay event and a presentation event: the authority owns damage, impulses, wake/release decisions, and one stable event ID/tick; presentation derives camera, bloom, particles, and distortion from that event without feeding pixels or wall-clock state back into simulation.
- Visual comparison judges the full motion envelope, not only a pile-shaped final frame. Tune against several direct source frames and at least one short source/clone sequence covering impact through settlement.
- Keep evidence proportional. Extend the current capture and smoke commands rather than creating a parallel evidence launcher. Protect one complete collapse replay, one focused joint/contact/explosion boundary, live dynamic-world network agreement, and the actual GPU effect path. If support plus tests grow larger than the feature code added by this slice, stop and simplify before review.

## Non-goals

- General destructible geometry, arbitrary user-authored fracture, every moving or reactive arena, ice behavior, saw hazards, or complete implementations of `EXPLOSIVE BULLET`, `LIFESTEALER`, or other card stacks visible earlier in the recording.
- Final character art, every HUD detail, audio fidelity, card selection, half/round transitions, matchmaking, NAT traversal, rollback, Steamworks transport, or production anti-cheat.
- Replacing Bevy's renderer, exposing renderer graph internals as gameplay APIs, serializing physics-engine state, or requiring cross-platform lockstep.
- Pixel-identical particles or inferring hidden original joint, mass, impulse, damage, and shader constants from compressed footage.

## Evidence required

- A checked observation record fixes the exact source interval and at least seven directly decoded anchors spanning intact structure, pre-impact combat, bright impact, first released bodies, large-scale deformation, debris settlement, and continued combat. It identifies which facts are directly visible and which replay parameters are approximations.
- Focused simulation evidence proves stable dynamic-body identity and ordering, a load-bearing joint/contact configuration before impact, one source-profile explosion that wakes/releases and impulses nearby bodies, real body-body and fighter-body collisions during collapse, bounded settlement, and repeatable same-build final hashes. An intentional physics perturbation changes the protected outcome.
- The network smoke launches one authority and two real clients, advances one input from each client per tick, observes progressive pre-impact and post-impact snapshots, and proves both clients plus the rendered received-state path agree on the full ordered dynamic-body digest and final outcome. Induced child-start failure still leaves no server or client process.
- The actual Bevy offscreen path produces 1280×720 PNGs for no fewer than seven named anchors and a short impact-to-settlement sequence. Evidence confirms that body transforms came from authoritative snapshots and that presentation-only particles/effect envelopes do not alter the state hash.
- Visual inspection compares source and clone at original resolution and records concrete similarities and remaining differences in structure silhouette, collapse direction/timing, debris density, floor/background/shadows, explosion light, bloom, camera response, particles, and screen distortion. A comparison artifact must make any non-matching anchor labels explicit rather than imply frame-exact equivalence.
- A visible command-line replay is exercised only through the existing hidden-first exact physical-display guard and exits without residue. The impact, camera motion, and screen effect must be observable there without editor interaction.
- `cargo test --workspace --locked -- --nocapture` passes as the first Cargo command from one verified-absent isolated target, followed by formatting, strict lint, locked build/tests, replay inspection, live UDP smoke, offscreen captures, visible playback, ticket checks, exact-range whitespace checks, and process/residue checks.
- A line/responsibility inventory accounts for feature, test, and support growth. No test-only physics, renderer, network, or alternate replay implementation is accepted.

## Work log

- 2026-09-04T05:42:59.228Z stage design start session codex:01a06920-7449-74d0-9b09-57855a012572 — Began the dense reactive-world stress slice after publishing the first static duel, using the supplied footage and the user-requested physics, effects, multiplayer, and programmatic-testing requirements as the product boundary.
- 2026-09-04T05:44:59.060Z stage design end session codex:01a06920-7449-74d0-9b09-57855a012572 — Bound the timber collapse to real Rapier bodies and joints, server-authoritative dynamic snapshots, received-state rendering, ordinary Bevy presentation plus a minimal fullscreen effect, direct motion-sequence comparison, and an explicit support-code proportionality gate.
- 2026-09-04T05:46:29.767Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a06af4-05f6-7233-a557-3f5709224503 — Began independent admission review of exact contract range `fea16ad4a1b98a6a7e78b300e154c1a3ccbfe140..2afeff1d4c0fd8e6cfa552002e6af0f748cdd5cc`.
- 2026-09-04T05:56:40.588Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/01a06af4-05f6-7233-a557-3f5709224503 — Rejected admission because the contract mandated a custom fullscreen pass before testing Bevy's built-in post-process effects and named the integration crate version as the underlying Rapier version; source, architecture, risk, ordering, and remaining evidence were otherwise sound.
- 2026-09-04T05:57:10.000Z stage correction start session codex:01a06920-7449-74d0-9b09-57855a012572 — Correcting the two admission findings and incorporating the reviewer's direct source decode without widening the product outcome.
- 2026-09-04T05:57:25.000Z stage correction end session codex:01a06920-7449-74d0-9b09-57855a012572 — Preferred Bevy's public bloom, chromatic-aberration, and lens-distortion components unless comparison proves a missing effect, named both pinned physics crate versions accurately, and fixed the directly observed source sequence and bounds.
- 2026-09-04T05:58:00.725Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a06af4-05f6-7233-a557-3f5709224503 — Began independent re-review of corrected admission range `2afeff1d4c0fd8e6cfa552002e6af0f748cdd5cc..8013c5b15c68a060ecb6099ffa352872147abd18`.
- 2026-09-04T05:58:49.391Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/01a06af4-05f6-7233-a557-3f5709224503 — Approved the corrected risk-4 contract with no findings after confirming the built-in-first post-process rule, exact physics versions, direct source sequence, feasible boundaries, ordering, scope, ancestry, and absence of a human decision.
- 2026-09-04T05:59:10.000Z stage design end session codex:01a06920-7449-74d0-9b09-57855a012572 — Admitted ticket 040 at risk 4 after independent review approved the corrected reactive-collapse contract.
