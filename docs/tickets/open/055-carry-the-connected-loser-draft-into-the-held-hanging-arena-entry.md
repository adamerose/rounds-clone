---
format: 3
status: ready
created: 2026-09-08T10:40:28Z
origin: agent-proposed
tags: ["rounds", "bevy", "fidelity", "match-flow", "arena"]
value: 7
risk: 4
sessions:
  - codex:01a07f87-97d0-7751-825a-29e87e8fe64a
execution: unattended
depends-on: [53]
supersedes: []
split-from: []
---

# Carry the connected loser draft into the held hanging-arena entry

The connected rematch route currently stops on the empty bridge after orange drafts QUICK SHOT. Carry that same authority and its two clients into the next recognizable source arena while both fighters remain held, so the playable match advances without choosing the unsupported suspension, movement, reload, or damage behavior that blocked the former ticket 048 continuation.

## Outcome

- The existing `rematch-draft-replay` route advances from tick 5893 through tick 5941. One authority and the same two UDP clients leave `PostRoundBridge`, enter a distinct held hanging-arena phase, replace the outgoing ice scene once, and remain in that phase at the endpoint.
- The transition preserves blue's first completed round and `Ex` loadout, orange's `Da Qu` loadout, and the already-cleared current-half progress. It initializes both living fighters and transient combat state exactly once at measured spawn poses: orange world `(-405, 53)` / native `(235, 307)`, blue world `(405, 87)` / native `(1045, 273)`, both with zero velocity throughout the bounded hold.
- A single typed canonical layout exposes the 21 observed hanging bodies, their nominal rectangular geometry, visible links, identities, colors, and nominal poses to the authority and shared renderer. It does not claim or encode a solver topology.
- The shared presentation reproduces the separate left, right, and central object entry motions. The first lower-left body appears as a sliver at tick 5894, all 21 squares reach nominal positions by tick 5918, and the held scene remains settled through tick 5941 with the source-shaped fighters, labels, score, and loadout badges.
- Public inspection, capture, visible playback, and network smoke support the 5,941-tick endpoint. The coverage ledger states plainly that this slice adds the held arena entry but no active hanging-arena combat.

## Decisions

- `GOAL.md` binds a source-first Bevy/Rapier clone. Reuse the current flow authority, ECS world, semantic input, transport, capture, and shared-renderer paths; add no replay profile, alternate authority, capture-only scene, or generic animation framework.
- The source is `reference/MedalTVRounds20260903165304088.mp4`, SHA-256 `453954a7230401ed805be4e53dec41779a1913dfd69903671fc131fca2c8a18c`. The interval overlaps ticket 053 at tick 5893 / PTS 2577323024 and ends at tick 5941 / PTS 2585322992, packed native RGBA SHA-256 `f90f451ed5bf928b6ea04a3a5fdf1623278063ff043dd67127e7cd9f43ce3067`.
- `docs/fidelity/held-hanging-entry-observations.md` is the durable source contract for all eleven boundary identities, the 21-body nominal layout, 42 visible link segments, source-shaped palette, spawn poses, curve knots, and measurement uncertainty. Implementation and review use those values rather than ignored ticket-048 outputs.
- Tick 5941 is the last retained held observation, not proof of an original hidden lock duration. Tick 5942 / PTS 2585489658 is an adjacent departure witness and is excluded from the delivered route. Source onset has approximately two frames of uncertainty.
- The retained object-entry anchors are ticks 5894, 5898, 5902, 5906, 5910, 5914, and 5918. Left/right square offsets are separately measured; at 5914 the complete central lower body and square have centers near 641.5 and 670.5, and at 5918 all square centers are within about one native pixel of nominal. Use small local monotone presentation curves and an approximately four-frame lead for the central body; offscreen starting positions and interpolation are authored choices, not recovered engine behavior.
- The canonical layout contains only facts needed by this held slice: identities, nominal body rectangles and poses, visible link endpoints, and spawn poses. Do not add joint kinds, fixed-anchor semantics, mass, inertia, restitution, friction, collider roles, dynamic-body snapshots, constraint snapshots, fake sleeping bodies, or a speculative wire schema. A later admitted physics slice may consume the geometry and add the state its selected model actually needs.
- The held phase rejects combat actions and performs no solver interaction. Release into combat, actor support, traversal, stored or wall jumps, controller calibration, suspension response, ammunition/reload, projectile calibration, damage changes, and the tick-5954 established-combat pose are outside this ticket.
- Reimplement the bounded behavior against current main. Evidence and measurements from the abandoned ticket 048 worktree may be reverified, but its product commits and executables are not candidate provenance: that route used a different base, missed the source support, and rang orange out at tick 6040.

## Evidence required

- A focused authority regression reaches tick 5893 through ordinary inputs, crosses into the held phase once, and proves ticks 5894–5941 keep the measured poses and zero velocities, accept no combat, perform no second current-half reset or revive, and preserve completed rounds, loadouts, capabilities, score, and player identity.
- Layout tests enumerate exactly 21 unique bodies and visible links, verify the measured nominal rows and dimensions, and prove the shared authority and renderer consume the same definition without dynamic-body or constraint state.
- Exact native/GPU comparisons cover 5893; the seven object-entry anchors 5894/5898/5902/5906/5910/5914/5918; and 5940/5941. They check the first sliver, unequal left/right spacing, the central body/square separation at 5914, nominal settled positions at 5918, held fighter/bar positions, readable `Da Qu` and `Ex` badges, the blue completed-round pip, and no stale ice scene. Tick 5942 is retained only as the excluded adjacent source witness.
- `rounds-automation inspect` and `smoke`, `rounds-client capture-replay`, and monitor-4 `visible-flow` complete through 5941. Both clients receive progressive snapshots and agree with the authority and received-state GPU; the existing 6,000-tick upper bound remains rejected.
- All earlier replay anchors retain their promised non-owned digests or have an explicitly proved protocol-only change. Workspace format, strict all-target Clippy, locked build, all tests, ticket checks, and `git diff --check` pass on the exact reviewed candidate. State any unavailable source decode, controller, audio, or manual evidence without substituting a weaker claim.

## Work log

- 2026-09-08T10:40:28Z stage design start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — Split the source-supported held arena entry from the blocked multiphysics continuation after current-code inspection and a read-only Astra challenge.
- 2026-09-08T10:42:37Z stage design end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — Wrote the bounded held-entry contract through tick 5941, passed the release-matched ticket checker and whitespace check, and left active combat and speculative physics explicitly excluded for fresh admission review.
- 2026-09-08T10:48:33Z — Fresh admission reviewer `codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a0809d-68de-7922-8718-07bcab8187c6` returned candidate bf73af4 solely because its exact layout and frame values lived only in ignored 048 evidence; scope, dependency, risk and no-physics boundary were otherwise sound.
- 2026-09-08T10:48:33Z stage correction start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — Promoting the exact source identities, geometry, presentation knots and uncertainty into a tracked fidelity record without changing the slice boundary or product scope.
- 2026-09-08T10:51:05Z stage correction end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — Added the durable eleven-frame source table, complete nominal layout, visible-link and palette choices, per-group curve knots and uncertainty; release-matched ticket and whitespace checks pass.
- 2026-09-08T10:51:39Z stage admission start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a080a5-1ad3-7a32-b389-afd732431f28 — Fresh context began admission review of exact range d8027c8..fb6947f after the source-contract correction.
- 2026-09-08T10:57:58Z stage admission end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a080a5-1ad3-7a32-b389-afd732431f28 — ADMIT risk 4 with no findings. The reviewer decoded every retained frame copy and matched all eleven packed RGBA hashes, checked the complete layout and current code seams, and passed the release-matched ticket and exact-range whitespace checks.
