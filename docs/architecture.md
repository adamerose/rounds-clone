# QUARREL architecture

The active game uses Rust, Bevy and Rapier. The retired Godot and C# prototype remains at `archive/godot-csharp-prototype-2026-09-03`.
`GOAL.md`, `docs/game-design.md` and `docs/roadmap.md` describe the intended game; this document describes the running implementation.

## Match authority

`quarrel-sim` owns one `AuthoritativeMatch`, advancing at 60 fixed ticks per second. `MatchConfig` supplies fighter count, target score, offer size, run-back limit and seed.
Fighters live in a vector of stable ECS entities. `FlowAuthority` owns opening and loser drafts, points, persistent card loadouts and end-of-match choices.
Every fighter picks one card before combat. The last survivor earns a point; if the last fighters die together, a seeded choice among fighters alive before that tick awards it.
After a brief result phase, every nonwinner picks another card. The first fighter to the target reaches `MatchEnd`.
Votes remain visible and changeable until everyone agrees. A run-back retains points and cards and increases the original target; a new match resets both.

Cards load from sorted `assets/cards/*.ron` files. Each has a string ID, name, description, stat changes and event rules, converted to a stable hashed `ItemId` and fighter modifiers.
Offers draw without replacement within a row and may contain cards already owned. The twelve original cards change shot stats and react to combat events; copies stack stats and rules.
Blink movement sweeps the current fighter shape against live terrain and clips travel to the arena frame; open-space blinks retain their authored distance.
Shots inherit current Fire flight changes at launch. The reaction queue carries shot identity and generation; further triggered events fade, stop at depth eight, and obey per-tick and pending limits. Damage from a selected effect is delivered without another fade roll. No footage profile, fixed offer list or source card enum drives the match.
Normal matches reload cards every fifteen ticks; malformed edits or removal of held/offered IDs retain the current catalog and expose `card_reload_error()`.
Reload updates held modifiers and future shots; existing shots retain launch stats, and health changes apply at the next fighter reset. Magazine bonuses stack on live tuning; reducing the magazine clamps loaded rounds, while increases take effect on the next refill.

`PlayerInput` is the public control boundary. Revisioned `FlowCommand` values reject actions from an earlier phase and apply an entire tick's commands before transitions.
`automated_input` uses the same boundary; automation cannot write scores, health, loadouts or phases.
`InputRecording` stores the configuration, logical starting card and arena content, and each tick's inputs. Playback uses that content without filesystem reloads.
The short ordinary recording reaches loser draft and match end; tests replay it twice and compare every snapshot. Repeatability is required on the same locked build and platform.

## Physics and arena data

Arenas load from sorted `assets/arenas/*.ron` files; the [arena data format](design-docs/arena-data.html) describes shapes, behaviors, camera bounds, chains and previews.
A seeded shuffle bag selects each arena once before repeats. Extra fighters receive deterministic positions when an arena has fewer declared spawns.
`PhysicsBoundary` keeps Rapier handles private and integrates players, CCD projectiles, fixed and loose objects, kinematic movement, saws, breakables and rope constraints.
Stable project IDs connect collision objects to ordered snapshots. Quantized snapshots expose neither Bevy entity IDs nor Rapier handles.
Movement, variable-height jumps, recoil, blocking, damage, knockback and ring-outs remain authority behavior. Elimination disables combat input until the next fight.

Normal matches watch their active arena file every fifteen ticks. Valid edits replace geometry and transient projectiles while preserving ticks, phase, points, cards, health and elimination.
Invalid edits retain the last valid arena and expose `arena_reload_error()`. Renaming an arena updates the name-to-file mapping, so later fights keep watching the same file.
`from_arena_file` follows this reload path; `with_content` uses supplied logical content without watching disk, as recorded playback requires.
Unchanged contour colliders are cached at the physics boundary to avoid repeating expensive concave preparation at each fight.

## Presentation and transport

`quarrel-presentation` reads immutable snapshots. Its shared scene draws data arenas, fighters, shots, cards, scores and consensus choices for visible play and offscreen PNGs.
Rendering never applies damage or cards. Impact records retain their twelve-tick visual window and at most sixty-four entries; metrics retain cumulative outcomes. Arena camera proportions are preserved; offscreen captures letterbox the declared frame.
Capture waits for rendering and screenshot completion before writing PNG bytes. Metadata hashes the executable, frame and authority state; output and metadata destinations must differ.
Visible windows start hidden, require the designated 1920 by 1080 display at physical position `(364,-1080)`, and appear only after placement is verified.

`quarrel-network` owns direct-IP UDP sessions. The scripted path retains JSON, configurable fighter counts, progressive snapshots and final state agreement.
The paced live path ships two peers: an authority runs at 60 Hz, applies newest held controls, consumes bounded FIFO flow edges, and publishes received state for rendering.
Session identities reject stale packets; consumed acknowledgements prevent repeated edge application; terminal snapshots are retransmitted until acknowledged or the bounded delivery window ends.
Host and dedicated modes use the same authority loop. Live protocol 14 uses compressed MessagePack and full independent snapshots at 10 Hz.
Clients replay unacknowledged local controls in one current physics scene containing every fighter, projectile and arena piece.
Remote bodies continue with observed velocities; moving paths use the host's motion formula. Snapshots include piece velocities and host outcomes always replace predicted observations.
Pose errors settle over subsequent frames inside the predicted physics scene; collision checks use the drawn poses. Result transitions settle toward the frozen host pose without resetting positions.
Fractional render frames interpolate adjacent current physics ticks for every entity. Remote entities are never buffered behind local time, and projectile speed is unchanged.
Prediction cannot apply damage, deaths, points or cards. Physics and tuning come from the host sample; no full-match rollback or local flow authority is created.
Protocol changes reject mismatched peers. Steam invitations remain MVP work.
Local keyboard/controller presentation and live sessions currently support the shipped 1v1 controls; the general simulation is also tested with three fighters.

## Verification

The repository configuration limits Cargo to two jobs and reuses `out/cargo-target`.
CI runs formatting, strict Clippy, build, serial workspace tests and a real two-client UDP smoke. Tests cover match rules, recorded inputs, arena physics and reloads, transport failures and capture safety.
Headless capture verifies draft and match-end presentation without opening a desktop window.
