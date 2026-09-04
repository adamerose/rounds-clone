---
format: 3
status: blocked
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

Reconstruct the complete duel visible from approximately 00:22.50 through the last in-arena frame at 00:35.60 in `reference/MedalTVRounds20260903170709695.mp4` as the first footage-derived vertical slice. The slice must establish the real physics, rendered client, authoritative multiplayer, and comparison workflow that later arenas, cards, and effects can extend; an isolated mechanics demo or synthetic state proof is not the outcome.

## Blocked

Fresh delivery review disproved the frozen contract's claimed 00:35.48 ring-out and found four implementation defects. The corrected source interval and behavior are now explicit; ticket 039 remains blocked only until those corrections pass an independent contract re-admission and exact delivery review, with no human decision outstanding.

## Outcome

- The authoritative 60 Hz simulation uses pinned Rapier 2D physics behind a project-owned boundary for player bodies, static arena colliders, bullets, contacts, impulses, and queries. Bevy entities and stable game identifiers remain the gameplay authority; snapshots and network messages never expose Rapier handles or serialized engine internals.
- The reconstructed teal arena matches the selected interval's stepped platform layout, spawn relationship, scale, dark teal background, colored platform faces, and long cast-shadow composition closely enough that source and clone anchor frames can be compared at the same 1280×720 viewport.
- Two circular fighters can stand and slide on platforms, run with air control, jump through the observed vertical routes, aim independently of movement, fire physical bullets, block or reflect a shot, take health-scaled knockback and damage, fall or be knocked outside the arena, and produce a single authoritative winner. The footage-derived replay ends with both fighters converging for the terminal upper-right impact visible before the result fade; it does not invent an on-screen ring-out.
- Gun recoil affects the shooter, bullets use continuous collision handling so a one-tick platform or player crossing is not missed, and collisions distinguish players, arena surfaces, bullets, and block state through named game concepts rather than third-party component types in public interfaces.
- A shared presentation model drives an actual Bevy 2D client scene and the command-line capture path. The client renders platforms and shadows, circular player bodies with directional gun and limbs, health/name treatment, bullets and trails, block feedback, hit flash, and restrained camera response visible in this interval; placeholder circles on a ground strip are removed.
- The client can run visibly from a command without editor interaction and can run an equivalent hidden or offscreen scripted replay that emits timestamped PNG anchor frames and bounded JSON evidence. Visible-window verification, when performed, obeys the repository's monitor-4 placement rule.
- The existing local-authority and headless-server modes run the same teal-duel rules. Two separately launched UDP clients connect to one concurrently running authority, send sequenced input packets while the match advances, and receive progressive authoritative snapshots plus the agreed final outcome. At least one actual rendered client consumes that live session rather than replaying a separately simulated result; the protocol transmits stable gameplay state and inputs, not physics-engine state.
- `docs/fidelity/footage-coverage.md` splits the 00:20–00:40 ledger entry into the observed draft fade, reconstructed duel, and result transition. It records this exact interval as the implemented first `S2-static-duel` sub-slice without claiming the unresolved card, match-result, advanced presentation, production-online, or other-arena work.
- `docs/architecture.md` describes the delivered Rapier boundary, real renderer and live-session network path, and `docs/decisions.md` records why those choices replace the foundation's integer simulation, CPU-only renderer, and batch-script transport.

## Decisions

- The binding source is recording SHA-256 `1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9`, interval 00:22.50–00:35.60. Store generated reference extracts and clone captures under ignored `out/`; do not alter the supplied recording.
- Use `bevy_rapier2d` 0.36 with default features disabled and only its 2D and headless features. Its documented `enhanced-determinism` feature currently conflicts with Bevy's reflection dependencies and is deliberately excluded; the authoritative server model does not depend on cross-platform lockstep. The project boundary limits future upgrade, patch, or engine-swap cost.
- Repeatability is required for the same locked build and platform used by automation, not asserted across platforms. The current online model is server/client-host authority with progressive snapshots, future client prediction for local actions, and interpolation for remote and dynamic-world state.
- Use ECS for durable gameplay identity, state, rules, and effect composition. Treat the physics world as a service synchronized at the fixed-step boundary and the renderer as a consumer of presentation snapshots; do not turn every short-lived visual particle or math helper into a networked ECS entity.
- Build the real Bevy renderer in this slice because subsequent fullscreen distortion, particles, lighting, and reactive-world work must extend the shipped path. A deterministic CPU image may remain only as a narrow diagnostic fallback and cannot be cited as visual-fidelity evidence.
- Tune against several frames and the complete motion interval, not a single screenshot. Because this recording occurs after visible card picks, implement a named `teal-duel-replay` tuning profile that reproduces this interval without claiming its values are base-game constants or complete implementations of the players' active cards. Match observable silhouettes, routes, impacts, timing, and composition; do not infer or claim inaccessible original numeric constants.
- Keep tests proportional. Protect physics/contact boundaries, one complete behavioral replay, network agreement, and capture metadata. If test or support code exceeds the product implementation for this slice, stop and document why each excess subsystem is necessary or simplify it before review.

## Non-goals

- Card choice UI, general card stacking, reverse-engineering base constants from this card-modified duel, the partial draft fade before 00:22.50, and the `HALF ORANGE` result presentation after 00:35.60.
- Moving, articulated, destructible, ice, explosive, or otherwise reactive arena geometry.
- Production matchmaking, relay/NAT traversal, rollback, Steamworks transport, anti-cheat, or adversarial local-machine hardening.
- Full audio fidelity or the extreme chromatic, radial, and explosive effects visible in later intervals.

## Evidence required

- A checked source-observation record identifies the selected interval, at least five source anchor timestamps, the arena geometry and palette measurements used, and the observed action sequence. Every derived frame records the source hash and timestamp.
- Locked formatting, strict lint, build, and all tests pass from an absent `target` directory without relying on command ordering, ignored tests, an editor, or a prebuilt sibling executable.
- Focused simulation evidence shows stable platform contact, the observed asymmetric traversal route, bullet CCD, block reflection, damage and recoil impulses, health-scaled knockback, the terminal upper-right impact, and one winner during the footage-derived replay. Ring-out remains a separately protected simulation capability rather than a false claim about this source interval.
- Packet-trace evidence shows two real client processes handshaking, sending monotonically sequenced inputs during the advancing match, and receiving multiple progressive snapshots from one server process. A capture from the rendered client binds one of those received snapshots. All three agree on the final bounded state and leave no child process behind on success or induced partial-start failure.
- The actual Bevy presentation path emits 1280×720 PNGs at no fewer than five named replay ticks spanning spawn, traversal, shot or block, hit or knockback, and round end. Capture metadata binds source interval, source hash, seed, input-trace hash, tick, state hash, executable hash, renderer identity, and frame hash.
- A reviewer inspects a source-and-clone contact sheet and records concrete similarities and remaining differences. Automated comparison may support layout and palette checks but cannot substitute for this visual review.
- A visible command-line launch is programmatically exercised for movement, aim, fire, and block without editor interaction. Before showing the window, the guard must identify the project display by its exact configured physical position and 1920×1080 extent, fail closed if that identity is absent or ambiguous, and report the observed identity rather than a hard-coded monitor index.
- A line and responsibility inventory maps every new test and support module to the behavior or public boundary it protects and confirms the proportionality decision.
- Documentation review confirms that the architecture, append-only decision record, fidelity ledger, dependency flags, executable modes, and delivered behavior agree.

## Work log

- 2026-09-04T02:51:05.902Z stage design start session codex:01a06920-7449-74d0-9b09-57855a012572 — Began the first footage-derived end-to-end Bevy slice around the simplest complete static duel after Adam made full reproduction of both supplied videos the continuing goal and asked that the prior proof-heavy workflow not recur.
- 2026-09-04T02:52:32.253Z stage design end session codex:01a06920-7449-74d0-9b09-57855a012572 — Bound the exact source interval, Rapier boundary, real Bevy renderer, authoritative two-client replay, source-and-clone visual comparison, monitor-safe programmatic interaction, explicit non-goals, and support-code proportionality gate into one falsifiable vertical slice.
- 2026-09-04T02:53:07.868Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a06a55-572e-7bd2-be97-605bb16bb6bb — Began independent admission review of records candidate `8728eedfcf45ee22f8e3a57e899b1dbfcd835511`.
- 2026-09-04T02:58:08.768Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/01a06a55-572e-7bd2-be97-605bb16bb6bb — Rejected admission because the mandated determinism feature is known not to compile, the selected duel is card-modified, batch scripts could still masquerade as multiplayer, and required architecture records would become stale.
- 2026-09-04T02:59:02.000Z stage correction start session codex:01a06920-7449-74d0-9b09-57855a012572 — Correcting every admission finding before implementation.
- 2026-09-04T03:00:49.782Z stage correction end session codex:01a06920-7449-74d0-9b09-57855a012572 — Removed the known-broken determinism feature, bounded repeatability to the locked authority build, made the card-modified interval an explicit replay profile rather than a base-constant oracle, required live sequenced input and progressive snapshots consumed by the renderer, and added the owning architecture and decision records.
- 2026-09-04T03:01:17.504Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a06a55-572e-7bd2-be97-605bb16bb6bb — Began independent admission review of corrected exact range `fd99729e13bebf08980547a30f1420c8214e8642..d7bc2629e21dd5171d5a10731784f148c06bb021`.
- 2026-09-04T03:02:04.832Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/01a06a55-572e-7bd2-be97-605bb16bb6bb — Approved the corrected risk-4 contract with no findings after verifying every earlier blocker was resolved, no human decision remained, and ticket, whitespace, ancestry, scope, and clean-state checks passed.
- 2026-09-04T03:03:00.000Z stage design end session codex:01a06920-7449-74d0-9b09-57855a012572 — Admitted ticket 039 at risk 4 after independent review approved the corrected complete vertical-slice contract.
- 2026-09-04T03:04:01.390Z stage implement start session codex:01a06920-7449-74d0-9b09-57855a012572/01a06a5e-9890-7393-a1d4-ab46f06bfd78 — Began implementing the admitted teal-duel slice in its isolated worktree after reading the full contract, governing records, manifest, and existing Rust boundaries.
- 2026-09-04T04:15:48.189Z stage implement end session codex:01a06920-7449-74d0-9b09-57855a012572/01a06a5e-9890-7393-a1d4-ab46f06bfd78 — Delivered the Rapier authority, Bevy renderer, live sequenced UDP replay, footage-bound anchors, monitor-4 visible run, documentation, and proportional regressions; clean locked format, lint, build, tests, three-process smoke, captures, and residue checks passed.
- 2026-09-04T04:17:32.845Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a06aa2-a00b-7980-9be4-a5450273c014 — Began fresh delivery review of exact implementation range `e941a737de1b9e4772545dbe7dc913e6ff1384c8..4b4f21cb822c2ffccdf397d3120bde4b2f8bb2bd`.
- 2026-09-04T04:32:15.973Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/01a06aa2-a00b-7980-9be4-a5450273c014 — Rejected the candidate because a cold-first renderer test was timing-dependent, two source choreography claims were false, capture metadata could overwrite image output, monitor evidence printed an unproven constant, and visible/offscreen camera application diverged.
- 2026-09-04T04:33:30.000Z stage correction start session codex:01a06920-7449-74d0-9b09-57855a012572 — Reopened the frozen contract after direct source decode disproved the terminal ring-out assumption, extending the owned footage through the last in-arena frame and binding the real terminal impact, asymmetric route, and exact fail-closed display identity before correcting every delivery finding.
