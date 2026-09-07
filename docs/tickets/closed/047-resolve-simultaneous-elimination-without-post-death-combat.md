---
format: 3
status: closed
created: 2026-09-06T00:06:31Z
origin: system-detected
tags: ["simulation", "combat", "match-flow"]
value: 7
risk: 4
sessions:
  - codex:01a073e9-17ec-7170-933a-0e18a071972d
  - claude:96849848-e6ec-488e-b4ac-b113acc49f8a
execution: unattended
depends-on: [46]
supersedes: []
split-from: []
---

# Resolve simultaneous elimination without post-death combat

When both fighters ring out on the same simulation tick, the authority marks them dead but leaves combat running, so their remaining health still permits movement and firing. Stop eliminated fighters from taking combat actions and resolve the simultaneous outcome once, so later shots cannot invent a winner after both fighters have already lost.

## Outcome

- Authoritative elimination prevents subsequent movement, aiming, firing and block activation, including ring-outs that leave positive health. Holding or changing either player's inputs after elimination cannot create projectiles, recoil, jumps or block activations.
- Simultaneous elimination has one authoritative outcome shared by simulation, match flow and received snapshots. Later collision processing and held inputs cannot replace that outcome or award a winner who was already eliminated.
- The match can continue after the simultaneous result through behavior established before admission. Existing single-survivor elimination, score awards and connected-match continuity remain correct.

## Decisions

- This ticket owns the independently observed simultaneous-elimination defect, separated from ticket 046's ice-arena and first-round delivery. Implement on the integrated 046 base because its result and round transitions change the same authoritative flow boundary.
- Establish the failing case through `AuthoritativeMatch::step` with ordinary `PlayerInput` values and inspect `snapshot`; private body teleportation or a winner setter cannot substitute for the regression's player-input route.
- Keep death state, outcome and scoring authoritative. Presentation and clients consume their result; they cannot decide which dead fighter won or repair stalled flow locally.
- The archived prototype's provisional draw policy is historical evidence, not proof of ROUNDS behavior. Resolve the source-evidence question in Scratch before admission; this idea does not authorize inventing result visuals, timing or restart semantics.
- Delivery decision (2026-09-07): the input lockout and once-per-tick outcome resolution are unconditional defect corrections. For the both-dead outcome the delivery reuses the archived provisional no-award rule with the existing `EliminationConclusion` pause and no new phase, visual or draft: winner and eliminated stay `None`, halves, completed rounds and loadouts persist, and the same arena repeats from its spawns. No simultaneous elimination is visible in either recording, so this rule stays provisional and remains an open human product decision; replacing it later touches only `FlowAuthority::record_simultaneous_elimination`, the `None` arm of the conclusion advance and `AuthoritativeMatch::repeat_fight_in_place`.

## Evidence required

- Preserve a deterministic ordinary-input trace that makes both fighters cross the kill boundary in one tick. Record the exact candidate, seed, profile, tick, health, alive flags, winner, flow phase and score; then hold fire and change movement/aim/block to prove dead fighters cannot act or change the outcome.
- Add a small regression at the authoritative step/snapshot boundary. Demonstrate its failure before the correction and success afterward, with a later projectile-contact case preventing a dead owner or target from manufacturing a new winner.
- Verify the admitted simultaneous-result policy, preserved earned score/loadouts, return to playable combat, and one-time outcome observation. Cover both player identities and retain ordinary single-survivor score coverage from ticket 046.
- Verify the authoritative outcome and score on both real clients through the existing network smoke path. Existing supported capture is sufficient for any changed result presentation; do not add a separate renderer or test-only flow route.

## Scratch

- Current-main source inspection at `fdf5e8abf80fdb94700c6fc4cd2b1a640a19e8ec` confirms the cause in `crates/rounds-sim/src/lib.rs`: the ring-out loop at lines 1824–1841 clears `PlayerState.alive` without clearing health and assigns `winner` only when `dead.len() == 1`. `step` gates movement and firing on `state.health > 0` at lines 1536 and 1545, while aim and block activation at lines 1528–1535 have no alive check. Projectile damage can assign `Some(projectile.owner)` at lines 1762–1763 without validating a living owner or target. `begin_result_if_due` and `MatchFlow::record_elimination` require a winner; the latter also projects one fighter as alive. These facts explain why positive-health ring-out deaths do not close combat, independently of any draw policy.
- The existing `ring_out_remains_a_separate_authoritative_capability` test at lines 2818–2828 teleports one private Rapier body outside `KILL_X`; it covers a single ring-out, not an ordinary-input simultaneous outcome. The public constructor, `step` and `snapshot` APIs support the retained probe below. The ticket 046 investigator ran it against the uncommitted 046 implementation on base `fdf5e8abf80fdb94700c6fc4cd2b1a640a19e8ec`; this is runtime evidence for that working candidate, not a claim of an executed current-main regression. This ticket's author inspected the retained code/output without compiling or rerunning it.
- Probe artifacts are currently retained under `.ivy/worktrees/046-next-playable-slice/out/ticket-046/`: `simultaneous_ringout_probe.rs`, SHA-256 `7c7bc3dd96433f06c3a2d4b25270d092b3b7491c7c071f6223c81f10bf177b41`, and `simultaneous-ringout.txt`, SHA-256 `3a982022ced50aa1f9848d6a12cc3532fe765aa0fb276ba632885fe0c7ffb581`. At tick 2302 both fighters have health 100, `alive: false`, Y -450895 milli-units, `ring_outs: 2`, `shots_fired: 3`, no winner and `ResumedCombat`. By tick 2340 the shot count is four while both remain dead. Hits at ticks 2377, 2416, 2455 and 2494 lower blue's health to 75, 50, 25 and zero; tick 2494 names orange as winner, changes the flow to `EliminationConclusion`, awards halves `[1, 0]` with completed rounds `[0, 0]`, and projects flow alive flags `[true, false]` despite both player snapshots remaining dead. The exact ordinary-input recipe follows so it survives cleanup of those ignored files.
- Closed ticket 006 explicitly bound a provisional no-award draw; closed ticket 008 preserved both half counters, repeated the same arena and opened no draft. Their live rationale still fits this defect: neither eliminated fighter is a surviving winner, an unawarded duel should not erase earned progress, and there is no losing player to draft alone. This is a useful candidate policy, with timing and presentation still unverified.
- The 2026-08-29 fidelity decision in `docs/decisions.md` rejects intentional divergence and calls existing tuning provisional; the 2026-09-03 human decision restarts the active implementation from supplied recordings. No simultaneous-elimination observation was found in current `docs/fidelity` records, including the full-duration ten-second index in `docs/fidelity/footage-coverage.md`. This is absence of a recorded observation, not proof that neither video contains one. Before admission, inspect the indexed recordings or supported target-build evidence for the actual simultaneous result and determine whether the archived provisional no-award/repeat-arena policy remains justified. If evidence leaves a material product choice, ask for a human decision; do not silently promote the old prototype rule to source fidelity.

```rust
use rounds_sim::{AuthoritativeMatch, FlowPhase, PlayerInput, ReplayProfile, scripted_inputs_for};
fn main() {
    let scripts = scripted_inputs_for(ReplayProfile::RematchDraftReplay, 41, 2200);
    let mut game = AuthoritativeMatch::new_with_profile(41, ReplayProfile::RematchDraftReplay);
    let mut previous = game.snapshot();
    for tick in 0..2550usize {
        let mut inputs = [PlayerInput::default(); 2];
        if tick < 2200 {
            inputs = [scripts[0][tick], scripts[1][tick]];
        } else if previous.flow.as_ref().unwrap().phase == FlowPhase::ResumedCombat {
            for player in 0..2 {
                let actor = &previous.players[player];
                let target = &previous.players[1-player];
                let dx = target.x_milli - actor.x_milli;
                inputs[player] = PlayerInput {
                    move_axis: if dx.abs() > 70_000 { dx.signum() as i8 } else { 0 },
                    jump: actor.grounded && (tick % 60 < 35 || actor.y_milli < target.y_milli),
                    fire: player == 0,
                    aim_at_opponent: true,
                    ..PlayerInput::default()
                };
            }
        }
        game.step([inputs[0].with_progressive_observation(0, Some(&previous)), inputs[1].with_progressive_observation(1, Some(&previous))]);
        let state = game.snapshot();
        if state.metrics.ring_outs != previous.metrics.ring_outs || state.metrics.hits != previous.metrics.hits || state.winner != previous.winner || (state.players.iter().all(|p| !p.alive) && state.tick % 20 == 0) {
            println!("{state:#?}");
        }
        previous = state;
    }
}
```

## Work log

- 2026-09-06T00:06:31Z stage design start session codex:01a073e9-17ec-7170-933a-0e18a071972d/01a07407-eaeb-75b3-94cf-57c216e441e1 — Shaping a separate defect record from current-main source and archived result-policy evidence; ordinary-input probe transfer remains pending.
- 2026-09-06T00:11:30Z stage design end session codex:01a073e9-17ec-7170-933a-0e18a071972d/01a07407-eaeb-75b3-94cf-57c216e441e1 — Recorded exact current-main cause, the independently run 046 public-input probe and retained artifact hashes, and the unverified provisional draw proposal. Idea remains unadmitted pending direct-source result research; release ticket checker passed and no build or GUI ran in this worktree.
- 2026-09-07T05:13:07Z stage implement start session claude:96849848-e6ec-488e-b4ac-b113acc49f8a — Implementing on integrated 046 base 71a7d53 in worktree `project-continuation-88fd8b`, reusing the prepared two-job Cargo target through `CARGO_TARGET_DIR` rather than starting a clean build.
- 2026-09-07T05:13:07Z — Public-input reproduction retained as `out/ticket-047/probe-before.txt` from a detached HEAD checkout of 71a7d53: profile `rematch-draft-replay`, seed 41, `ResumedCombat` from tick 2220, both fighters walking outward from their spawns leave the arena 102 ticks later at tick 2322 with health `[100, 100]`, `alive: [false, false]`, y −443577 milli-units, `ring_outs: 2`, no winner, phase still `ResumedCombat`, halves `[0, 0]`. Holding move/aim-down/jump/fire/block for 40 ticks then produced 4 shots, 6 block activations and re-aimed both dead fighters while they fell to y −1149815. `out/ticket-047/probe-after.txt` shows the corrected tree at the same tick: `EliminationConclusion`, no shot, jump, block or aim change while dead, then both fighters alive at their spawns (y −150221) in `ResumedCombat` at tick 2338 with halves `[0, 0]`, rounds `[0, 0]` and `simultaneous_eliminations: 1`.
- 2026-09-07T05:13:07Z — Correction in `crates/rounds-sim`: `PlayerState.alive` now gates aim, block, movement and fire; projectile contacts with a dead target are discarded; `resolve_elimination_outcome` decides each tick once from the surviving fighters instead of per-hit and per-ring-out winner assignment; `FlowAuthority::record_simultaneous_elimination` records the no-award result and the conclusion's `None` arm repeats the stored combat phase; `repeat_fight_in_place` clears projectiles, respawns both fighters from the arena's recorded spawns and revives them. Standalone profiles without a flow freeze combat when nobody is alive. The unused `PendingRadialHit.owner` field was removed.
- 2026-09-07T05:13:07Z — Regressions: `simultaneous_ring_out_freezes_both_fighters_and_repeats_the_fight_once` (public step/snapshot, self-calibrated same-tick walk-off, dead-input lockout, one-time outcome, resume at spawns, retained halves/rounds/loadouts, combat playable again), `lone_ring_out_after_a_health_elimination_cannot_change_the_winner` and flow-level `simultaneous_elimination_awards_nothing_and_repeats_the_same_combat_phase` (a later `record_elimination` cannot replace the recorded outcome). Existing single-survivor, sweep/split and connected-route regressions remain.
- 2026-09-07T05:20:44Z stage verify end session claude:96849848-e6ec-488e-b4ac-b113acc49f8a — `cargo fmt --all -- --check`, `cargo clippy --workspace --all-targets --locked -- -D warnings`, `cargo build --workspace --locked` and `cargo test --workspace --locked` (45 passed, zero failed or ignored) all passed on the shared two-job target; output in `out/ticket-047/delivery-verification.txt`. Connected checks in `out/ticket-047/connected-checks.txt`: the 5,466-tick two-client smoke handshook, exchanged 5,466 progressive snapshots per client, and reported clients, local host and received-state GPU render in agreement; `capture-replay` retained all 37 anchors with executable 477720a6c3870bc1caf38c05f8f8b51c8b849b34a75a137bbbb06d10e06fe0e7; the yellow 155-tick capture succeeded; `visible-flow --automated` verified the hidden-first window center (1324,-540) inside monitor 4 (364,-1080,1920,1080) before showing and exited normally. Compared with the retained 046 delivery anchors, all 37 frame, round, flow, loadout, dynamic-body and arena digests are identical; only the state and combat digests differ on every anchor because `CombatMetrics` gained `simultaneous_eliminations`, which the combat digest serializes. No game, server, Cargo or capture processes remained. `git diff --check` passed. No physical controller or real double elimination was exercised on the source route, which contains none.
- 2026-09-07T05:20:44Z stage implement end session claude:96849848-e6ec-488e-b4ac-b113acc49f8a — Closed with the provisional no-award repeat-in-place policy recorded in `docs/decisions.md` as an open human product decision; README, architecture and footage coverage updated. Responsibility inventory: about 120 added runtime Rust lines in `rounds-sim` (input gating, once-per-tick outcome, flow no-award record and repeat, spawn respawn), about 210 test lines, no new profile, phase, renderer path or transport change.
- 2026-09-07T09:54:12Z stage review end session claude:96849848-e6ec-488e-b4ac-b113acc49f8a/review-047-opus — approved exact candidate 71a7d53..be27dd6. The fresh context reran the locked workspace suite (45 passed), strict all-target clippy, format check and the 5,466-tick two-client smoke (clients, local host and received-state GPU agree; state hash identical to the author's retained smoke), confirmed 37/37 source-route frame, round, flow, loadout, arena and dynamic-body digests identical to the retained 046 anchors, and ran a throwaway ice-arena double walk-off probe (draw at ice tick 3066, repeat in the ice arena at its spawns with halves 1–1, rounds 0–0, loadouts and seventeen contours intact, outcome counted once). Five notes, none blocking: the network smoke exercises only the unchanged outcomes because the source route contains no double elimination; standalone profiles without a flow freeze with no result when both die, unreachable on their scripted routes; a timber draw repeats in the already-collapsed arena with retained debris, matching the stated repeat-in-place policy; stun-pulse and timer decay still runs for a corpse in the one tick before the flow leaves combat, not input-driven; the architecture sentence "after damage and ring-outs" is one notch stronger than the standalone radial path, which resolves its pending hit at the top of the tick. The Ivy ticket checker script is not present in this checkout and was not run.
- 2026-09-07T09:54:12Z stage integration end session claude:96849848-e6ec-488e-b4ac-b113acc49f8a — integrated eb28cec as eb28cec by fast-forwarding local main; the worktree is retained because ticket 049 research is running in it.
