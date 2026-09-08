# ROUNDS clone architecture

This document describes the active Bevy implementation.
The retired Godot and C# architecture remains available at the annotated tag `archive/godot-csharp-prototype-2026-09-03`.

## Fidelity source

The two videos identified by path, size, and SHA-256 in `reference/manifest.json` are the complete end-to-end target.
`docs/fidelity/footage-coverage.md` divides both recordings into contiguous reviewed intervals and assigns every visible behavior to implemented work or a named gap.
Older research under `research/` can help explain a behavior, but footage wins when the two disagree.

## Runtime shape

The authoritative match advances at 60 fixed ticks per second in `rounds-sim`.
Stable player and projectile identities and gameplay state live in Bevy ECS.
A project-owned `PhysicsBoundary` keeps Rapier rigid-body, collider, and joint handles private while it advances static arena contacts, dynamic circular players, dynamic arena bodies, constraints, CCD bullets, recoil, blocks, damage, knockback, explosions, and ring-outs.
Vertical control is variable-height and arena-independent: `set_player_control` sets the fixed jump impulse when jump is pressed on a grounded tick, and cuts the remaining upward velocity to `JUMP_RELEASE_CUT` once, on the tick the jump input goes from held to released, and only while that fighter is airborne and still rising. A release read on a grounded tick does nothing whatever the vertical velocity, and a held jump keeps the full arc. The memory of last tick's input is authority-internal and reaches no snapshot; it follows the jump input on every tick the authority reads it, whether or not control runs for that fighter, so a fighter that lets go while stunned or eliminated has let go rather than saving the release for the tick control returns, and a revive clears it with the rest of the transient control state. The one release the authority cannot read on its own tick is one inside a freeze, where no input is read at all; it is consumed on the first controlled tick after the freeze. `JUMP_SPEED`, gravity, damping and air control are unchanged by it, so every arena gets the same rule and none gets a special case. No shipped replay script releases jump while airborne and rising, so the mechanic exists without any current route exercising it; the ice route ticket 049 owns will be the first.
Only quantized project snapshots cross the simulation boundary; neither Bevy entity IDs nor Rapier handles appear on the wire.
Repeatability is required on the same locked build and platform, not across platforms.

Dynamic arena bodies and constraints use stable project IDs held in ordered registries.
The timber-collapse profile begins with 17 dynamic timber bodies held by fixed Rapier joints and two dynamic circular weights held by rope joints.
Its authoritative explosion releases the fixed joints, wakes and impulses nearby bodies, and leaves the rope joints active; contacts and Rapier integration determine the resulting pile.
Snapshots report ordered transforms, velocities, sleep state, constraint activity, and the stable explosion event.

The radial-saw profile uses the same boundary for two kinematic Rapier bodies with project IDs 200 and 201. The dark diamond support is represented by the same stable arena records that are rendered, so replay collision cannot rely on invisible rectangular walls. ECS owns each saw's initial pose, radius, eight-tooth silhouette, and measured 7.43 rad/s angular velocity; snapshots read the collider pose and body angular velocity, while reset immediately propagates the restored body pose to its collider before another snapshot can cross the wire. The source does not show saw contact, so their colliders carry no damage rule. Ordinary CCD projectiles remain the only damage path in this slice. A small authoritative round resource preserves orange's existing half, awards blue's half on the tick-909 ordinary hit, freezes combat, then exposes the established `HalfBlue` phase at tick 938.

The rematch/draft profile adds an authoritative match-flow resource above combat.
It owns blue's 4–5 terminal win, orange's elimination, both fighters' exact prior-card badge stacks, one rematch vote per player, and the both-yes reset that clears the result and old cards, revives both fighters, and starts the score at 0–0.
It also owns seeded ordered offers, active drafter, hover, confirmation, reveal, phase revision, and persistent new-match loadouts.
The source replay advances through only a brief dark bridge after orange's reveal: orange focuses `COMBINE` at tick 540, `BURST` at tick 600, and confirms Dazzle into a lowered fan at tick 840; blue's complete fan is authoritative by tick 960, then focuses Dazzle, Lifestealer, and Echo before confirming Explosive Bullet into a lowered fan at tick 2120.
The stable item registry holds the nine distinct definitions behind the ten visible offers; Dazzle appears in both fans.
Only Dazzle and Explosive Bullet are implemented and available to general offer generation, while the authority returns `UnimplementedItem` for the seven distinct catalog-only definitions no matter what a client renders.
Typed fighter capabilities enter ECS-backed projectile creation: Dazzle bullets schedule three stun pulses and Explosive Bullet impacts emit an authoritative radial event and impulse.

The same rematch authority continues after the original 2,400-tick draft slice through the first full round. Ordinary damage decides each fight. Two wins by either player in the opening arenas award a round immediately; a split loads the ice arena for a deciding fight. One `AuthoritativeMatch`, its ECS world and its Rapier boundary survive each handoff. Existing fighters retain identity and selected capabilities; loading the next arena replaces its collision surfaces, revives and repositions both fighters beneath the outgoing result overlay, and clears old projectiles, dynamic bodies and constraints before creating the incoming arena. The prior winner and half award remain visible until combat resumes.
Elimination ends a fighter's agency: a dead fighter's inputs cannot move, aim, block or fire, and projectiles that reach a dead body are discarded. Each tick resolves the fight outcome once from the fighters still alive after damage and ring-outs, so a lone survivor wins and a held or changed input can never manufacture a later winner. When both fighters die on the same tick the flow records a no-award result with no winner and no eliminated player, pauses through the same conclusion timing, then revives both fighters at the current arena's spawns with projectiles cleared while halves, completed rounds and loadouts persist. This repeat-in-place policy is provisional pending source evidence.
The source trace produces blue, orange, then blue. `FlowSnapshot.halves` records current fight wins and `FlowSnapshot.scores` records completed rounds. The full-round overlay retains [1, 2] halves and [0, 1] completed rounds; entry to the post-round draft resets only halves to [0, 0]. `RoundStateSnapshot.scores` retains its half-count meaning, while `completed_rounds` carries the connected round count. The completed round's loser is the sole active drafter. Dedicated post-round draft, reveal and bridge phases keep this cadence independent from the opening two-player draft, while revisioned `FlowCommand` remains the only input/application boundary.
The ice arena has seventeen stable polygon records, IDs 40–56. Each `ArenaSurfaceSnapshot.outline_milli` contains local centered vertices used by both Rapier's static collision contour and the shared renderer; an empty outline keeps the earlier rectangular surfaces. The entry snapshot briefly carries the actual outgoing fighter positions so both clients can interpolate their arrival while collision already uses the new arena. The contours stay intact during combat. This source interval establishes no ice friction, fracture, melting or extra damage rule.
In this connected route an Explosive Bullet must contact a dynamic timber body before the fixed timber joints release. Removing that shot or drafting Dazzle for blue instead leaves the structure constrained. The explosion event uses the actual Rapier surface contact point rather than the bullet center after its continuous-collision step. Subsequent pile poses come from Rapier. The standalone ticket-040 profile retains its previously admitted scripted impact and anchors; it is not the connected gameplay trigger.

`PlayerInput::with_progressive_observation` resolves explicitly requested opponent-relative aim using the latest snapshot before input submission. The public `AuthoritativeMatch::snapshot` and `step` methods let a program choose and submit later actions without reconstructing state. The local trace, visible client-host and UDP clients use that same resolver and revisioned `FlowCommand` boundary. Snapshot protocol 7 carries the post-round phases, five catalog entries, typed per-fighter projectile-speed factor, polygon contours and separate half/round state; network protocol 8 carries those snapshots and permits at most 6,000 exchanged ticks per smoke session. The bounded source route uses 5,893 ticks and stops before tick 5,894's hanging-arena pixel.
Human visible play maps two-player keyboard and controller combat controls to `PlayerInput` and advances at 60 Hz from elapsed time. The bounded automated visible route advances the source action trace faster for verification. Neither mode owns damage, card application, collapse or scoring.

Presentation reads an immutable authoritative snapshot through `rounds-presentation`.
Pixels, camera motion, and other presentation-only state never enter the replicated snapshot or its hash.
The shipped Bevy 2D scene draws the static platforms and long shadows, a snapshot-responsive faceted timber floor and directional shadow, dynamic timber and weights, suspended lines, fighters, limbs, guns, health/name treatment, bullets, trails, block rings, hit flash, and snapshot-derived explosion particles.
Visible and offscreen modes apply the same snapshot-derived camera transform, shake envelope, `Bloom`, `ChromaticAberration`, and `LensDistortion` settings.
The timber impact uses separate short flash and delayed shock envelopes so the compact multi-lobed light ends while the source-timed whole-screen displacement peaks.
These effects and the render-only particle arrangement do not enter the authoritative snapshot or state hash.
The offscreen 1280×720 GPU path waits for Bevy's screenshot-completion event, bounds both device polling and the total capture, encodes the returned image, and writes the PNG only after capture succeeds.
The visible path starts hidden, requires exactly one physical display at `(364,-1080)` with extent 1920×1080, verifies the window against that observed identity, and only then reveals it; missing or ambiguous displays fail closed.
For the draft profile the same renderer projects received phase state into the live-arena result and rematch overlays, brief dark transitions, large expressive orange/blue fighters, curved readable five-card fans, reveal cadence, focus lift/dimming, selected-card confirmation motion, and arena bridges. The post-round lead-in raises a card behind the still-authoritative result; draft and reveal preserve the completed-round pip and authoritative badges, changing orange's stack from `Da` to `Da Qu` only after confirmation. At bridge entry on tick 5818, orange and the four unselected cards remain while Quick Shot alone is absent; by the settled tick-5893 endpoint, only the patterned background and HUD remain. No second renderer or presentation-side item application exists.
Every stable item `art_key` selects a distinct card motif, while authoritative hover and reveal state drives the card lift, arms, hands, eyes, and mouth.
Offscreen capture does not guess how many updates rendering needs: it requires the expected camera, background, character, hands, cards, and item-art entities, observes an empty Bevy pipeline queue, and then waits for two consecutive complete extracted render frames before requesting the screenshot.
Keyboard arrows plus Enter/Space and controller D-pad plus south button map to the same revisioned `FlowCommand` values used by automation and the UDP clients; these mappings never apply an item in presentation.
For the radial profile, that same shared scene reads rotated platform and saw poses from the snapshot, then adds tick-derived paper-brush motion, long shadows, ordinary trails and impact particles, result dimming, half-score circles, and `HALF BLUE`. Those render entities are absent from authority hashes and cannot advance the frozen result locally.
For the connected ice arena, the same scene clips animated cyan and pale facets to the snapshot contours and adds long navy shadows. Phase-relative movement brings the arena in and takes it away with its fighters and effects. Existing burst and chromatic effects respond to authoritative events. The result starts with both prior halves, fills the winning circle, displays `ROUND BLUE` or `ROUND ORANGE`, then moves the full circle into the first completed-round HUD pip while retaining the other half and loadout badges. No new replay profile, renderer, fullscreen pass or transport path owns this extension.

`rounds-network` owns the wire records and the transport-facing API.
Its current adapter uses bounded IPv4 UDP datagrams on the local development machine.
Two clients handshake, send one monotonically sequenced input per advancing tick, and receive that tick's progressive authoritative snapshot.
The authority waits for both handshakes before releasing either client so an early input cannot overtake the other handshake.
It is not a production reliability protocol and does not claim prediction, interpolation, rollback, lag compensation, matchmaking, authentication, or Steam transport.
A future Steam adapter belongs behind this boundary and must preserve the same simulation inputs and snapshots.

`rounds-server` runs one headless authoritative session.
`rounds-client` runs the same simulation as a local client-host, submits one input sequence to a remote development server, renders a received live snapshot, runs visibly, or emits named replay anchors with source, input, state, arena, saw, combat, round, dynamic-body, flow, loadout, executable, renderer, and frame identity.
`rounds-automation` starts the headless server and two real client processes, proves each received the same progressive phase sequence, binds one client's render to its received final snapshot, checks the profile-specific authority projections and local-host agreement, and emits bounded JSON evidence.

## Workspace boundaries

| Crate | Owns | Does not own |
|---|---|---|
| `rounds-sim` | Bevy ECS authoritative state, private Rapier service, fixed-tick rules, input validation, stable snapshots | rendering, sockets, files, wall clock |
| `rounds-presentation` | shared Bevy 2D visible/offscreen snapshot scene | authoritative or replicated state |
| `rounds-network` | bounded wire records and the current UDP adapter | game rules or presentation |
| `rounds-server` | headless server process and command-line configuration | duplicated simulation rules |
| `rounds-client` | local-host, live remote, visible, and replay-capture entry points | editor state or server-only rules |
| `rounds-automation` | smoke orchestration and JSON inspection | gameplay behavior |

`bevy_rapier2d` 0.36 is pinned with default features disabled and only `dim2` and `headless` enabled.
The incompatible `enhanced-determinism` feature is deliberately absent; the server-authority model does not require cross-platform lockstep.

## Public evidence commands

All dependencies, including Bevy `0.19.1`, are pinned in `Cargo.toml`, `rust-toolchain.toml`, and `Cargo.lock`.
The supported headless commands are:

```text
cargo fmt --all -- --check
cargo clippy --workspace --all-targets --locked -- -D warnings
cargo build --workspace --locked
cargo test --workspace --locked
out/cargo-target/debug/rounds-automation smoke --profile timber-collapse-replay --seed 40 --ticks 1440 --output-dir out/ticket-040/smoke
out/cargo-target/debug/rounds-automation inspect --profile timber-collapse-replay --seed 40 --ticks 1440
out/cargo-target/debug/rounds-client capture-replay --profile timber-collapse-replay --seed 40 --ticks 1440 --output-dir out/ticket-040/clone-anchors --metadata out/ticket-040/clone-anchors.json
out/cargo-target/debug/rounds-client visible --profile timber-collapse-replay --seed 40 --ticks 1440 --frames 180
out/cargo-target/debug/rounds-automation smoke --profile rematch-draft-replay --seed 41 --ticks 2400 --output-dir out/ticket-041/smoke
out/cargo-target/debug/rounds-automation inspect --profile rematch-draft-replay --seed 41 --ticks 2400
out/cargo-target/debug/rounds-client capture-replay --profile rematch-draft-replay --seed 41 --ticks 2400 --output-dir out/ticket-041/anchors --metadata out/ticket-041/anchors.json
out/cargo-target/debug/rounds-client visible-flow --profile rematch-draft-replay --seed 41 --ticks 2400 --automated
out/cargo-target/debug/rounds-automation smoke --profile radial-saw-half-blue-replay --seed 42 --ticks 938 --output-dir out/ticket-042/smoke
out/cargo-target/debug/rounds-automation inspect --profile radial-saw-half-blue-replay --seed 42 --ticks 938
out/cargo-target/debug/rounds-client capture-replay --profile radial-saw-half-blue-replay --seed 42 --ticks 938 --output-dir out/ticket-042/anchors --metadata out/ticket-042/anchors.json
out/cargo-target/debug/rounds-client visible --profile radial-saw-half-blue-replay --seed 42 --ticks 938 --frames 180
```

The smoke command must report every handshake, input sequence and progressive snapshot, agreement among the headless server, both UDP clients and local client-host, and a live client render bound to the agreed state hash.
Replay capture emits twelve named Bevy-rendered timber anchors spanning the intact structure, pre-impact combat, bright impact, 100 ms impact progression, first release, deformation, debris, settlement, and continued combat.
The earlier teal-duel profile remains available by passing `--profile teal-duel-replay --ticks 786`.
The 2,400-tick rematch/draft capture retains thirteen anchors from `VICTORY!` through both five-card fans and the upgraded projectile exchange. Passing `--ticks 4540` extends it to 25 anchors, and `--ticks 5466` emits 37 through the final blue pip. Passing `--ticks 5893` retains those 37 and adds eleven entries at 5466, 5478/5479, 5493/5494, 5588, 5710, 5801/5802, 5818 and 5893. Native source PTS and RGBA hashes bind every connected anchor; the adjacent pairs preserve the silhouette, overlay/reset and badge boundaries.
Run `out/cargo-target/debug/rounds-automation smoke --profile rematch-draft-replay --seed 41 --ticks 5893 --output-dir out/ticket-053/smoke` for the extended two-client check. The corresponding capture command is `out/cargo-target/debug/rounds-client capture-replay --profile rematch-draft-replay --seed 41 --ticks 5893 --output-dir out/ticket-053/anchors --metadata out/ticket-053/anchors.json`; bounded playback uses `visible-flow --profile rematch-draft-replay --seed 41 --ticks 5893 --automated` on that same client executable.
The radial replay emits eight anchors from arena reveal through traversal, ordinary projectile exchange, adjacent tick-908/tick-909 combat and result frames, and established tick-938 `HALF BLUE`.

## Testing rule

Keep tests at the public and deep boundaries: stable contact and jump behavior, the jump-release cut's height band and its grounded-release gate, the complete duel and collapse, joint release and explosion response, outcome-changing physics perturbation, one-tick bullet CCD, bounded inspection, progressive UDP agreement, and the real Bevy offscreen renderer.
For match flow, one compact set covers the complete phase order, terminal authority and exact prior badges, accepted-rematch clearing, source-bound blue-fan cadence, vote outcomes, invalid actions, exact source offers, catalog-only rejection, one-time typed picks, seed/loadout perturbations, concrete keyboard/controller mapping, and real Dazzle/Explosive projectile behavior.
The connected first-round regressions drive public inputs through both drafts and arena handoffs, check body/projectile cleanup and retained loadouts, and distinguish current halves from completed rounds. Ordinary-damage outcomes cover both colors winning a deciding fight and both colors sweeping the opening fights. A changed terminal shot must change the protected elimination and award, and holding the result must leave its score unchanged.
A public-input regression walks both fighters off the first rematch arena on the same tick and proves that dead fighters cannot shoot, jump, block or re-aim, that no winner or half is awarded, and that the same fight resumes once with both fighters revived at their spawns. A flow-level test proves a later elimination report cannot replace the recorded no-award outcome.
A deep process-lifecycle test starts a minimal test-owned UDP child, forces the next child launch to fail, and proves cleanup releases the server.
The capture boundary resolves metadata and every PNG destination before rendering or writing, compares every pair, and rejects aliases; process tests cover single capture, replay capture, and remote rendering before any network request or file write. A focused renderer test captures the same immutable draft state twice and requires byte-identical complete frames and unchanged authoritative digests.
The radial checks protect stable saw IDs and measured motion, immediate collider-pose reset, an outcome-sensitive angular-speed perturbation, the exact ordinary-damage/result/score boundary, and two clients observing the same final authority projections.
Do not retain tests for private layout or retired implementation details.
When test or support machinery outweighs the behavior it protects, rethink the slice instead of hardening the machinery by default.
