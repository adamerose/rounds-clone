use super::super::*;
use std::collections::BTreeSet;
pub(super) fn connected_match_at_resumed_combat() -> (AuthoritativeMatch, MatchSnapshot) {
    let scripts = scripted_inputs_for(ReplayProfile::RematchDraftReplay, SOURCE_DRAFT_SEED, 2_200);
    let mut game =
        AuthoritativeMatch::new_with_profile(SOURCE_DRAFT_SEED, ReplayProfile::RematchDraftReplay);
    for (orange, blue) in scripts[0].iter().zip(&scripts[1]).take(2_200) {
        game.step([*orange, *blue]);
    }
    let mut state = game.snapshot();
    for _ in 0..600 {
        if state.flow.as_ref().unwrap().phase == FlowPhase::ResumedCombat {
            break;
        }
        game.step([PlayerInput::default(); 2]);
        state = game.snapshot();
    }
    assert_eq!(state.flow.as_ref().unwrap().phase, FlowPhase::ResumedCombat);
    (game, state)
}

/// Ordinary outward walking from the spawn until the fighter leaves the arena.
fn walk_outward(player: usize) -> PlayerInput {
    PlayerInput {
        move_axis: if player == 0 { -1 } else { 1 },
        ..PlayerInput::default()
    }
}

/// Ticks after combat resumes until one fighter, walking alone, rings out.
fn solo_ring_out_delay(player: usize) -> u32 {
    let (mut game, start) = connected_match_at_resumed_combat();
    for delay in 1..600 {
        let mut inputs = [PlayerInput::default(); 2];
        inputs[player] = walk_outward(player);
        game.step(inputs);
        if game.snapshot().metrics.ring_outs > start.metrics.ring_outs {
            return delay;
        }
    }
    panic!("player {player} never left the arena by walking");
}

#[test]
fn simultaneous_ring_out_freezes_both_fighters_and_repeats_the_fight_once() {
    let delays = [solo_ring_out_delay(0), solo_ring_out_delay(1)];
    let (mut game, start) = connected_match_at_resumed_combat();
    let start_flow = start.flow.clone().unwrap();
    let hold = [
        delays[1].saturating_sub(delays[0]),
        delays[0].saturating_sub(delays[1]),
    ];
    let mut state = start.clone();
    for elapsed in 0..delays[0].max(delays[1]) {
        let mut inputs = [PlayerInput::default(); 2];
        for (player, input) in inputs.iter_mut().enumerate() {
            if elapsed >= hold[player] {
                *input = walk_outward(player);
            }
        }
        game.step(inputs);
        state = game.snapshot();
        assert_eq!(state.winner, None, "nobody wins before both leave");
    }
    let flow = state.flow.clone().unwrap();
    assert_eq!(state.metrics.ring_outs, start.metrics.ring_outs + 2);
    assert!(
        state
            .players
            .iter()
            .all(|player| !player.alive && player.health == 100)
    );
    assert_eq!(state.winner, None);
    assert_eq!(flow.phase, FlowPhase::EliminationConclusion);
    assert_eq!((flow.winner, flow.eliminated), (None, None));
    assert_eq!(flow.fighter_alive, [false, false]);
    assert_eq!(
        (flow.halves, flow.scores),
        (start_flow.halves, start_flow.scores)
    );
    assert_eq!(state.metrics.simultaneous_eliminations, 1);
    let draw_tick = state.tick;
    let frozen_aim = state.players.iter().map(|p| (p.aim_x, p.aim_y));
    let frozen_aim = frozen_aim.collect::<Vec<_>>();

    // Held and changed inputs after elimination cannot act or pick a winner.
    let mut resumed_at = None;
    for _ in 0..40 {
        let hostile = PlayerInput {
            move_axis: 1,
            aim_x: 0,
            aim_y: -1_000,
            jump: true,
            fire: true,
            block: true,
            ..PlayerInput::default()
        };
        game.step([hostile; 2]);
        state = game.snapshot();
        let flow = state.flow.as_ref().unwrap();
        if flow.phase == FlowPhase::EliminationConclusion {
            assert_eq!(state.metrics.shots_fired, start.metrics.shots_fired);
            assert_eq!(state.metrics.jumps, start.metrics.jumps);
            assert_eq!(
                state.metrics.block_activations,
                start.metrics.block_activations
            );
            assert!(state.projectiles.is_empty());
            assert_eq!(state.winner, None);
            let aim = state.players.iter().map(|p| (p.aim_x, p.aim_y));
            assert_eq!(aim.collect::<Vec<_>>(), frozen_aim);
        } else if resumed_at.is_none() {
            resumed_at = Some(state.tick);
            assert_eq!(flow.phase, FlowPhase::ResumedCombat);
        }
    }
    let resumed_at = resumed_at.expect("the same fight repeats after the pause");
    assert!(resumed_at > draw_tick && resumed_at <= draw_tick + 20);
    let flow = state.flow.clone().unwrap();
    assert_eq!(flow.fighter_alive, [true, true]);
    assert_eq!(
        (flow.halves, flow.scores),
        (start_flow.halves, start_flow.scores)
    );
    assert_eq!(flow.loadouts, start_flow.loadouts);
    assert_eq!(state.metrics.simultaneous_eliminations, 1);
    assert_eq!(state.metrics.ring_outs, start.metrics.ring_outs + 2);
    assert!(
        state
            .players
            .iter()
            .all(|player| player.alive && player.health == 100)
    );
    for (player, spawn) in state.players.iter().zip([-500_000, 500_000]) {
        assert!((player.x_milli - spawn).abs() < 60_000, "{player:?}");
        assert!(player.x_milli.abs() < quantize(KILL_X));
    }
    // The repeated fight accepts ordinary combat again: the hostile inputs
    // above already fired and jumped once combat resumed.
    assert!(state.metrics.shots_fired > start.metrics.shots_fired);
    assert!(state.metrics.jumps > start.metrics.jumps);
}

#[test]
fn lone_ring_out_after_a_health_elimination_cannot_change_the_winner() {
    let (mut game, start) = connected_match_at_resumed_combat();
    let winner_before = start.winner;
    assert_eq!(winner_before, None);
    // Blue walks out alone; orange stays and wins that fight.
    let mut state = start;
    for _ in 0..600 {
        game.step([PlayerInput::default(), walk_outward(1)]);
        state = game.snapshot();
        if state.winner.is_some() {
            break;
        }
    }
    assert_eq!(state.winner, Some(0));
    assert_eq!(state.flow.as_ref().unwrap().halves, [1, 0]);
    // Orange now walking out during the held result cannot undo the award.
    for _ in 0..30 {
        game.step([walk_outward(0), walk_outward(1)]);
    }
    let held = game.snapshot();
    assert_eq!(held.flow.as_ref().unwrap().halves, [1, 0]);
    assert_eq!(held.flow.as_ref().unwrap().winner, Some(0));
    assert_eq!(held.metrics.simultaneous_eliminations, 0);
    assert_eq!(held.metrics.ring_outs, state.metrics.ring_outs);
}

/// Ticket 051's one-tick jump presses, rewritten as `{T} ∪ [A, R)`:
/// `(player, T, Some((A, R)))`, where `A` is the first tick after the press on
/// which that fighter's snapshot reports `grounded == false` and `R` the first
/// tick after `A` on which it reports grounded again. `None` means the fighter
/// never leaves the ground after the press, so it keeps no hold.
type ScriptedJumpPress = (usize, u32, Option<(u32, u32)>);

/// Ticket 050's extended holds: `(player, R, E)`, where `R` is the release tick
/// the shipped hold had before the extension and `E` its new end — the first
/// tick after `R` on which that fighter is no longer airborne-and-rising.
type ExtendedJumpHold = (usize, u32, u32);

struct ScriptedJumpContract {
    profile: ReplayProfile,
    seed: u64,
    ticks: u32,
    presses: &'static [ScriptedJumpPress],
    /// Jump holds already in the shipped scripts, as ticket 050 extends them.
    existing_holds: &'static [(usize, u32, u32)],
    /// The subset of `existing_holds` ticket 050 extended, with the release
    /// tick they used to have.
    extended_holds: &'static [ExtendedJumpHold],
    /// Releases that fall inside a freeze `step` returns from before it reads
    /// control, as `(player, release tick, first controlled tick)`. The
    /// release is consumed on the first controlled tick after the freeze, so
    /// every tick of the window has to be free of the cut's condition.
    frozen_releases: &'static [(usize, u32, u32)],
    jumps: u32,
    /// `quarrel-automation inspect --profile <name>` reports this as `stateHash`.
    state_sha256: &'static str,
}

const SCRIPTED_JUMP_CONTRACTS: &[ScriptedJumpContract] = &[
    ScriptedJumpContract {
        profile: ReplayProfile::TealDuelReplay,
        seed: 38,
        ticks: TEAL_REPLAY_TICKS,
        presses: &[
            (1, 40, Some((42, 74))),
            (1, 500, Some((502, 534))),
            (1, 650, Some((652, 681))),
        ],
        existing_holds: &[(0, 330, 670)],
        extended_holds: &[],
        frozen_releases: &[],
        jumps: 17,
        state_sha256: "893b333b6d6f5a593dbc92fbd1391ebe08d3e21d1ab6a5b531ab7ae67445fd15",
    },
    ScriptedJumpContract {
        profile: ReplayProfile::RadialSawHalfBlueReplay,
        seed: 42,
        ticks: RADIAL_REPLAY_TICKS,
        presses: &[
            (0, 160, Some((163, 164))),
            (0, 340, Some((343, 344))),
            (0, 520, Some((523, 525))),
            (0, 820, Some((824, 825))),
            (0, 888, Some((892, 893))),
            (1, 650, Some((652, 659))),
        ],
        existing_holds: &[],
        extended_holds: &[],
        frozen_releases: &[],
        jumps: 6,
        state_sha256: "12f4d0f85538f6e5f3b4a1a9d3bceb1d71cd8e6bd87c57ad9a3a3dcf4255c2cf",
    },
    ScriptedJumpContract {
        profile: ReplayProfile::YellowCrateTerminalBlastReplay,
        seed: 43,
        ticks: YELLOW_REPLAY_TICKS,
        presses: &[],
        existing_holds: &[],
        extended_holds: &[],
        frozen_releases: &[],
        jumps: 0,
        state_sha256: "b4b899db7ab193e21044890449e9b77ab5bc4257a6495a89cf520ac19ce3a661",
    },
    ScriptedJumpContract {
        profile: ReplayProfile::TimberCollapseReplay,
        seed: 40,
        ticks: REPLAY_TICKS,
        presses: &[
            (0, 120, Some((122, 170))),
            (0, 960, None),
            (0, 1_280, Some((1_299, 1_305))),
            (1, 260, Some((262, 310))),
            (1, 1_150, Some((1_159, 1_160))),
        ],
        existing_holds: &[],
        extended_holds: &[],
        frozen_releases: &[],
        jumps: 5,
        state_sha256: "0502a08fd2589cdd3ee19d9228ae9c961f95082fd1ffe6bdee2104bebe12193a",
    },
    ScriptedJumpContract {
        profile: ReplayProfile::RematchDraftReplay,
        seed: SOURCE_DRAFT_SEED,
        ticks: CONNECTED_FIRST_ROUND_TICKS,
        presses: &[
            (0, 4_929, Some((4_931, 4_988))),
            (0, 5_211, Some((5_213, 5_218))),
            (1, 5_000, Some((5_002, 5_070))),
        ],
        existing_holds: &[
            (1, 3_000, 3_365),
            (1, 3_700, 3_970),
            (0, 4_000, 4_541),
            (1, 4_000, 4_432),
            (1, 4_601, 4_666),
            (0, 4_710, 4_740),
            (1, 4_801, 4_813),
            (0, 4_820, 4_850),
            (1, 4_943, 4_971),
            (1, 5_135, 5_184),
            (1, 5_213, 5_244),
            (0, 5_230, 5_282),
        ],
        extended_holds: &[
            (0, 4_420, 4_541),
            (1, 4_420, 4_432),
            (1, 4_656, 4_666),
            (1, 4_811, 4_813),
            (1, 4_955, 4_971),
            (1, 5_175, 5_184),
            (1, 5_235, 5_244),
            (0, 5_265, 5_282),
        ],
        frozen_releases: &[(0, 4_541, 4_601)],
        jumps: 141,
        state_sha256: "374341276ebb0a1517b9c4e6747463199ed264beda387dfc3585567a601810e2",
    },
];

/// Ticket 051. A held jump re-applies the whole impulse on the first tick
/// contact restores `grounded`, so extending a press across ticks the fighter
/// is already airborne must change nothing: `set_player_control` reads
/// `input.jump` only as `input.jump && grounded`. This drives all five
/// pre-existing profiles at their published seeds and tick counts through the public
/// snapshot boundary and asserts, from `snapshot` alone, that every added hold
/// tick is airborne, that each press tick is still a grounded press, that the
/// scripted jump ticks are exactly the contract's set, and that the jump count
/// and the final `stateHash` `inspect` reports have not moved.
///
/// Ticket 050 extends the same argument to the eight pre-existing holds whose
/// release was airborne and rising, so that no shipped release can be reached
/// by a jump-release cut: every tick they gain is asserted airborne and rising,
/// and their new release tick is asserted to be neither.
#[test]
fn scripted_jump_presses_hold_through_their_airborne_ticks_without_moving_any_digest() {
    for contract in SCRIPTED_JUMP_CONTRACTS {
        let profile = contract.profile;
        let scripts = scripted_inputs_for(profile, contract.seed, contract.ticks);
        let snapshots = run_profile_snapshots(profile, contract.seed, contract.ticks);
        assert_eq!(snapshots.len(), contract.ticks as usize);
        // The input at script index `i` is applied against the `grounded` flag
        // published in the snapshot whose tick is `i`, because `step`
        // increments the tick before it reads control.
        let grounded_at =
            |tick: u32, player: usize| snapshots[tick as usize - 1].players[player].grounded;
        let velocity_y_at = |tick: u32, player: usize| {
            snapshots[tick as usize - 1].players[player].velocity_y_milli_per_second
        };

        // Ticket 050. Each of the eight extended holds runs from the release
        // tick `R` the shipped script had to `E`, the first tick after `R` on
        // which the fighter is no longer airborne-and-rising. That is the exact
        // negation of the release cut's own condition, so every added tick is a
        // tick `set_player_control` ignores and the new release at `E` is inert
        // whichever clause ends the rise.
        for (player, release, end) in contract.extended_holds {
            for tick in *release..*end {
                assert!(
                    !grounded_at(tick, *player),
                    "{profile:?} player {player}: added hold tick {tick} of the \
                     [{release}, {end}) extension is grounded, so the hold would jump again"
                );
                assert!(
                    velocity_y_at(tick, *player) > 0,
                    "{profile:?} player {player}: added hold tick {tick} of the \
                     [{release}, {end}) extension is not rising ({} milli u/s), so the \
                     release at {end} would not be the end of the rise",
                    velocity_y_at(tick, *player)
                );
            }
            assert!(
                grounded_at(*end, *player) || velocity_y_at(*end, *player) <= 0,
                "{profile:?} player {player}: the extended release at {end} is still \
                 airborne and rising ({} milli u/s), so a release cut could reach it",
                velocity_y_at(*end, *player)
            );
        }

        // Ticket 050's correction. Orange's extended hold ends at 4541, which
        // is inside the round-end freeze: from blue's elimination until the
        // ice round begins, `step` returns before it reads any input, so the
        // release is consumed on the first controlled tick after the freeze
        // rather than on 4541 itself. It is inert either way, and this asserts
        // the whole window is: physics is frozen at `velocity_y` 0 from 4541
        // to 4600 and the fighter is grounded from 4601, so no tick between
        // the release and the first controlled tick is airborne-and-rising.
        for (player, release, first_controlled) in contract.frozen_releases {
            for tick in *release..*first_controlled {
                assert_eq!(
                    velocity_y_at(tick, *player),
                    0,
                    "{profile:?} player {player}: tick {tick} of the frozen release \
                     window is not at rest, so the freeze is not what this claims"
                );
            }
            assert!(
                grounded_at(*first_controlled, *player),
                "{profile:?} player {player}: the first controlled tick \
                 {first_controlled} after the frozen release at {release} is airborne"
            );
            for tick in *release..=*first_controlled {
                assert!(
                    grounded_at(tick, *player) || velocity_y_at(tick, *player) <= 0,
                    "{profile:?} player {player}: tick {tick} between the frozen \
                     release at {release} and the first controlled tick is airborne \
                     and rising ({} milli u/s), so a release cut could reach it",
                    velocity_y_at(tick, *player)
                );
            }
        }

        for (player, script) in scripts.iter().enumerate() {
            let mut expected = BTreeSet::new();
            for (_, start, end) in contract
                .existing_holds
                .iter()
                .filter(|(owner, ..)| *owner == player)
            {
                expected.extend(*start..*end);
            }
            for (_, press, window) in contract
                .presses
                .iter()
                .filter(|(owner, ..)| *owner == player)
            {
                assert!(
                    grounded_at(*press, player),
                    "{profile:?} player {player}: press {press} must stay a grounded press"
                );
                expected.insert(*press);
                // Ticket 050's airborne gate rests on this: a rewritten press
                // `{T} u [A, R)` carries exactly two held-to-released
                // transitions, at `T + 1` and at `R`, and both fall on a
                // grounded tick, so a release cut can never reach one.
                assert!(
                    grounded_at(*press + 1, player),
                    "{profile:?} player {player}: press {press} is released on                          airborne tick {}, so a release cut could reach it",
                    *press + 1
                );
                let Some((first_airborne, regrounded)) = window else {
                    continue;
                };
                assert!(
                    grounded_at(*regrounded, player),
                    "{profile:?} player {player}: press {press} is released on                          airborne tick {regrounded}, so a release cut could reach it"
                );
                for tick in *first_airborne..*regrounded {
                    assert!(
                        !grounded_at(tick, player),
                        "{profile:?} player {player}: hold tick {tick} of press {press} \
                         is grounded, so the hold would jump again"
                    );
                    expected.insert(tick);
                }
            }
            let scripted = script
                .iter()
                .enumerate()
                .filter(|(_, input)| input.jump)
                .map(|(tick, _)| tick as u32)
                .collect::<BTreeSet<_>>();
            assert_eq!(
                scripted, expected,
                "{profile:?} player {player}: scripted jump ticks"
            );
        }

        let terminal = snapshots
            .last()
            .expect("every profile runs at least one tick");
        assert_eq!(terminal.metrics.jumps, contract.jumps, "{profile:?} jumps");
        assert_eq!(
            hash_snapshot(terminal),
            contract.state_sha256,
            "{profile:?} final stateHash"
        );
    }
}

#[test]
fn prior_terminal_hashes_are_recovered_by_removing_only_the_protocol_and_catalog_additions() {
    for (profile, seed, ticks, prior_hash) in [
        (
            ReplayProfile::TealDuelReplay,
            38,
            TEAL_REPLAY_TICKS,
            "90a93bd4fd2effcd78626b59a05a96473a83b92942f8766a4a630bfe32e50671",
        ),
        (
            ReplayProfile::RadialSawHalfBlueReplay,
            42,
            RADIAL_REPLAY_TICKS,
            "42700b3383c1d22fadd2d5e6c0bab32ebc6ab505c87f4ad49dc821c13c3600bb",
        ),
        (
            ReplayProfile::YellowCrateTerminalBlastReplay,
            43,
            YELLOW_REPLAY_TICKS,
            "f2c34be96331138bf07945db1c70b5ca9138d71ee0d48045c193c7908703c43e",
        ),
        (
            ReplayProfile::TimberCollapseReplay,
            40,
            REPLAY_TICKS,
            "dc547472a81528d07dd2fb0f1c07374ade31e0bae3537cabeac45a77f4c4e120",
        ),
        (
            ReplayProfile::RematchDraftReplay,
            SOURCE_DRAFT_SEED,
            CONNECTED_FIRST_ROUND_TICKS,
            "8dc4339d7271d1a1947e40db5437561fbfca263d70cbb62320102b28bb849686",
        ),
        (
            ReplayProfile::MatchEndWaitingReplay,
            57,
            MATCH_END_WAITING_REPLAY_TICKS,
            "eb1cdc060cacc736899de4ee8bbf856384edcc6942736911904587ee0ed3549b",
        ),
    ] {
        let mut snapshot = run_profile_match(profile, seed, ticks).0;
        snapshot.protocol = 9;
        if let Some(flow) = &mut snapshot.flow {
            flow.catalog.truncate(14);
        }
        assert_eq!(hash_snapshot(&snapshot), prior_hash, "{profile:?}");
    }
}

/// One measured jump arc, read from `snapshot` alone.
struct JumpArc {
    jumps: u32,
    takeoff_tick: u32,
    apex_tick: u32,
    rise_px: f64,
    fit_v0: f64,
    fit_ascent: f64,
    fit_rms: f64,
    release_grounded: bool,
    release_velocity_y_milli: i32,
}

/// Drives a fighter through `AuthoritativeMatch::step` with ordinary
/// `PlayerInput` values on the teal arena's flat ground: `settle` ticks at
/// rest, then jump held for `hold` input ticks and released on input tick
/// `hold`, then held released. Nothing is read but `snapshot`, and no body,
/// velocity or position is touched.
///
/// Take-off is the snapshot produced by the step that applied the *last*
/// impulse. A held jump applies the impulse twice, because the fighter still
/// reports `grounded` on the tick after the press and `set_player_control`
/// jumps again there — the behaviour ticket 049 recorded for orange at 4711
/// and 4712 and ticket 051 built its hold windows around.
fn scripted_jump_arc(hold: u32, ticks: u32) -> JumpArc {
    scripted_jump_arc_with_stun(hold, ticks, None)
}

/// Drives the teal ground press of `scripted_jump_arc`, optionally stunning
/// the fighter across the release tick. With `stun` set, the stun is written
/// immediately after the press step, so the release at input tick `hold` is
/// read on a tick `step` skips `set_player_control` for that fighter, and
/// control returns `stun - 1` ticks later while the fighter is airborne and
/// still rising. Everything measured is still read from `snapshot`: no body,
/// velocity or position is touched.
fn scripted_jump_arc_with_stun(hold: u32, ticks: u32, stun: Option<u16>) -> JumpArc {
    const SETTLE: u32 = 30;
    let mut simulation = AuthoritativeMatch::new_with_profile(38, ReplayProfile::TealDuelReplay);
    for _ in 0..SETTLE {
        simulation.step([PlayerInput::default(); 2]);
    }
    let mut release_grounded = false;
    let mut release_velocity_y_milli = 0;
    let mut trace: Vec<(u32, f64, u32)> = Vec::new();
    for index in 0..ticks {
        if index == hold {
            let fighter = &simulation.snapshot().players[0];
            release_grounded = fighter.grounded;
            release_velocity_y_milli = fighter.velocity_y_milli_per_second;
        }
        let input = PlayerInput {
            jump: index < hold,
            ..PlayerInput::default()
        };
        simulation.step([input, PlayerInput::default()]);
        if let Some(stun) = stun
            && index + 1 == hold
        {
            set_stun_ticks(&mut simulation, stun);
        }
        let snapshot = simulation.snapshot();
        trace.push((
            snapshot.tick,
            f64::from(snapshot.players[0].y_milli) / 1_000.0,
            snapshot.metrics.jumps,
        ));
    }
    // Take-off is the last tick of the *first* run of impulses: a held jump
    // applies the impulse again on every tick contact restores `grounded`, so
    // the arc under measurement ends where the fighter next jumps.
    let mut takeoff = trace
        .iter()
        .position(|row| row.2 > 0)
        .expect("the drive must actually jump");
    while takeoff + 1 < trace.len() && trace[takeoff + 1].2 > trace[takeoff].2 {
        takeoff += 1;
    }
    let jumps = trace[takeoff].2;
    let flight_end = (takeoff + 1..trace.len())
        .find(|index| trace[*index].2 > jumps)
        .unwrap_or(trace.len());
    let apex = (takeoff..flight_end)
        .max_by(|left, right| {
            trace[*left]
                .1
                .partial_cmp(&trace[*right].1)
                .expect("heights are finite")
        })
        .expect("a non-empty ascent");
    let heights = trace[takeoff..=apex]
        .iter()
        .map(|row| row.1 - trace[takeoff].1)
        .collect::<Vec<_>>();
    let (fit_v0, fit_ascent, fit_rms) = fit_constant_acceleration(&heights);
    JumpArc {
        jumps,
        takeoff_tick: trace[takeoff].0,
        apex_tick: trace[apex].0,
        rise_px: heights[heights.len() - 1],
        fit_v0,
        fit_ascent,
        fit_rms,
        release_grounded,
        release_velocity_y_milli,
    }
}

/// Stuns fighter 0 exactly as a Dazzle impact does. The shipped impact is
/// `target_state.stun_ticks = projectile.dazzle_stun_ticks`, and every Dazzle
/// card in `flow.rs` carries `dazzle_stun_ticks: 6`. Firing a real Dazzle
/// projectile needs the draft profile 2,220 ticks in with both fighters placed
/// by hand, so the impact's own write is used here and nothing else about the
/// state is touched.
fn set_stun_ticks(simulation: &mut AuthoritativeMatch, ticks: u16) {
    let entity = simulation.player_entities[0];
    let mut player = simulation.world.entity_mut(entity);
    player
        .get_mut::<PlayerState>()
        .expect("player state")
        .stun_ticks = ticks;
}

/// Puts fighter 0 in the state an eliminated fighter is in as far as `acts` is
/// concerned: `step` computes `acts = state.alive && state.health > 0` and
/// skips control for it when that is false.
fn eliminate_fighter(simulation: &mut AuthoritativeMatch) {
    let entity = simulation.player_entities[0];
    let mut player = simulation.world.entity_mut(entity);
    player.get_mut::<PlayerState>().expect("player state").alive = false;
}

/// Reads fighter 0's release memory. It is authority-internal and reaches no
/// snapshot, so there is nothing else to read it from.
fn jump_held(simulation: &AuthoritativeMatch) -> bool {
    simulation
        .world
        .entity(simulation.player_entities[0])
        .get::<PlayerState>()
        .expect("player state")
        .jump_held
}

/// Settles a teal fighter on flat ground and presses jump for one tick, the
/// grounded press ticket 051's rewrite leaves. The fighter is airborne and
/// rising afterwards, with `jump_held` true.
fn teal_match_after_a_one_tick_press() -> AuthoritativeMatch {
    let mut simulation = AuthoritativeMatch::new_with_profile(38, ReplayProfile::TealDuelReplay);
    for _ in 0..30 {
        simulation.step([PlayerInput::default(); 2]);
    }
    simulation.step([
        PlayerInput {
            jump: true,
            ..PlayerInput::default()
        },
        PlayerInput::default(),
    ]);
    simulation
}

/// Ticket 049's whole-phase fit, unchanged: least squares of
/// `y(n) = y0 + v n + a n^2 / 2` over the ascent, returned as `v` in world
/// units per second, the ascent as a positive deceleration in units per
/// second squared, and the residual rms in world units.
fn fit_constant_acceleration(heights: &[f64]) -> (f64, f64, f64) {
    let count = heights.len();
    assert!(count >= 4, "a constant-acceleration fit needs four samples");
    let mut moments = [0.0_f64; 5];
    let mut weighted = [0.0_f64; 3];
    for (index, height) in heights.iter().enumerate() {
        let n = index as f64;
        let mut power = 1.0;
        for moment in moments.iter_mut() {
            *moment += power;
            power *= n;
        }
        weighted[0] += *height;
        weighted[1] += n * *height;
        weighted[2] += 0.5 * n * n * *height;
    }
    let matrix = [
        [moments[0], moments[1], moments[2] / 2.0],
        [moments[1], moments[2], moments[3] / 2.0],
        [moments[2] / 2.0, moments[3] / 2.0, moments[4] / 4.0],
    ];
    let determinant = determinant3(matrix);
    let solve = |column: usize| {
        let mut replaced = matrix;
        for row in 0..3 {
            replaced[row][column] = weighted[row];
        }
        determinant3(replaced) / determinant
    };
    let (offset, velocity, acceleration) = (solve(0), solve(1), solve(2));
    let mut squared = 0.0;
    for (index, height) in heights.iter().enumerate() {
        let n = index as f64;
        let residual = height - (offset + velocity * n + 0.5 * acceleration * n * n);
        squared += residual * residual;
    }
    (
        velocity * 60.0,
        -acceleration * 3_600.0,
        (squared / count as f64).sqrt(),
    )
}

fn determinant3(matrix: [[f64; 3]; 3]) -> f64 {
    matrix[0][0] * (matrix[1][1] * matrix[2][2] - matrix[1][2] * matrix[2][1])
        - matrix[0][1] * (matrix[1][0] * matrix[2][2] - matrix[1][2] * matrix[2][0])
        + matrix[0][2] * (matrix[1][0] * matrix[2][1] - matrix[1][1] * matrix[2][0])
}

/// Ticket 050. `set_player_control` cuts the fighter's upward velocity once,
/// on the tick its jump input goes from held to released, and only while the
/// `grounded` argument it is passed is false and `velocity.y` is positive.
///
/// Everything below is driven through `AuthoritativeMatch::step` with ordinary
/// `PlayerInput` values and read from `snapshot`: no private body access, no
/// teleport, no forced velocity.
///
/// The uncut arc measured at this boundary is 116.862 px over 23 ticks, which
/// is ticket 049's retained public-trace measurement ("orange's measured arc
/// from 4712 to its apex at 4735 rises 116.9 px in 23 ticks"). The 122.85 px
/// in `clone-jump-model.md` is that model's own integration of the same
/// recurrence, about 4 % higher than Rapier's; the claim the contract makes —
/// that a held jump behaves exactly as it does today — is asserted here
/// against the shipped number.
///
/// The contract asserts its acceptance band at the eleven-tick release alone.
/// A ten- and a twelve-tick release are measured and printed, because no
/// factor clears all four numbers at either; nothing is asserted about them.
#[test]
fn a_released_jump_is_cut_once_while_airborne_and_rising() {
    // A jump released eleven ticks after take-off, while the fighter is
    // airborne and still rising. The contract's four acceptance bands for a
    // source-shaped short hop, against the source's measured ice hop of
    // 80.5 px in 12 ticks fitting v0 746.5 and ascent 3100.
    let hop = scripted_jump_arc(12, 60);
    println!(
        "release 11 ticks after take-off: {:.2} px over {} ticks, fit v0 {:.0}, \
         ascent {:.0}, rms {:.2}",
        hop.rise_px,
        hop.apex_tick - hop.takeoff_tick,
        hop.fit_v0,
        hop.fit_ascent,
        hop.fit_rms
    );
    assert!(
        !hop.release_grounded && hop.release_velocity_y_milli > 0,
        "the release must be read on an airborne rising tick: grounded={}, vy={}",
        hop.release_grounded,
        hop.release_velocity_y_milli
    );
    let hop_ticks = hop.apex_tick - hop.takeoff_tick;
    assert!(
        (82.0..=90.0).contains(&hop.rise_px),
        "cut hop rise {:.3} px is outside the source-shaped 82-90 px band \
         (take-off {}, apex {}, fit v0 {:.1}, ascent {:.0})",
        hop.rise_px,
        hop.takeoff_tick,
        hop.apex_tick,
        hop.fit_v0,
        hop.fit_ascent
    );
    assert!(
        (12..=14).contains(&hop_ticks),
        "cut hop lasted {hop_ticks} ticks, outside the 12-14 tick band"
    );
    assert!(
        (730.0..=780.0).contains(&hop.fit_v0),
        "cut hop fitted v0 {:.1} u/s is outside the 730-780 band (rms {:.2} px)",
        hop.fit_v0,
        hop.fit_rms
    );
    assert!(
        (2_900.0..=3_300.0).contains(&hop.fit_ascent),
        "cut hop fitted ascent {:.0} u/s^2 is outside the 2900-3300 band (rms {:.2} px)",
        hop.fit_ascent,
        hop.fit_rms
    );

    // Measured and reported, not asserted: the releases either side of the
    // asserted one. No factor clears all four bands at either.
    for hold in [11_u32, 13] {
        let arc = scripted_jump_arc(hold, 60);
        println!(
            "release {} ticks after take-off: {:.2} px over {} ticks, fit v0 {:.0}, \
             ascent {:.0}, rms {:.2}",
            hold - 1,
            arc.rise_px,
            arc.apex_tick - arc.takeoff_tick,
            arc.fit_v0,
            arc.fit_ascent,
            arc.fit_rms
        );
    }

    // A jump held through its whole rise is untouched.
    let held = scripted_jump_arc(200, 60);
    assert_eq!(held.jumps, 2, "a held jump re-applies the impulse once");
    assert_eq!(held.apex_tick - held.takeoff_tick, 23, "held rise ticks");
    assert!(
        (held.rise_px - 116.862).abs() < 0.001,
        "a held jump must keep the shipped arc, got {:.3} px",
        held.rise_px
    );

    // A release read after the apex, while the fighter is airborne but
    // falling, changes nothing.
    let after_apex = scripted_jump_arc(40, 60);
    assert!(
        !after_apex.release_grounded && after_apex.release_velocity_y_milli < 0,
        "the post-apex release must be read airborne and falling: grounded={}, vy={}",
        after_apex.release_grounded,
        after_apex.release_velocity_y_milli
    );
    assert_eq!(
        after_apex.apex_tick - after_apex.takeoff_tick,
        23,
        "post-apex release rise ticks"
    );
    assert!(
        (after_apex.rise_px - held.rise_px).abs() < 1e-9,
        "a release after the apex must leave the arc alone: {:.3} against {:.3}",
        after_apex.rise_px,
        held.rise_px
    );

    // The gate itself. A one-tick press releases at T + 1, where the fighter
    // still reports grounded with a strongly positive vertical velocity —
    // exactly the +647.447 state ticket 051's rewrite leaves at T + 1 for all
    // seventeen of its presses. Nothing may be cut there.
    let grounded_release = scripted_jump_arc(1, 60);
    assert!(
        grounded_release.release_grounded,
        "the one-tick press must be released on a grounded tick"
    );
    assert_eq!(
        grounded_release.release_velocity_y_milli, 647_447,
        "the grounded release must be read while strongly rising"
    );
    assert_eq!(grounded_release.jumps, 1, "a one-tick press jumps once");
    assert_eq!(
        grounded_release.apex_tick - grounded_release.takeoff_tick,
        23,
        "a grounded release must leave the whole rise alone"
    );
    assert!(
        (grounded_release.rise_px - 116.862).abs() < 0.001,
        "a grounded release must change nothing, got {:.3} px",
        grounded_release.rise_px
    );
}

/// Ticket 050's correction. The release memory follows the jump input on
/// every tick the authority reads it, whether or not control runs for that
/// fighter, so a release read while the fighter cannot act is spent where it
/// happened instead of being deferred to the next controlled tick and judged
/// against the state there.
///
/// The drive is the one-tick grounded press ticket 051's rewrite leaves at
/// `T + 1`: `grounded` true with `velocity_y_milli_per_second` +647,447, the
/// release the contract says "does nothing at all". Control is taken away
/// across it by the stun a Dazzle impact writes, and returns five ticks later
/// while the fighter is airborne and rising — where the deferred release used
/// to be cut, collapsing the arc from 116.862 px over 23 ticks to 53.993 px
/// over 11. The two tests after this one cover the eliminated fighter and the
/// revive.
#[test]
fn a_release_read_while_stunned_is_not_deferred_to_the_next_controlled_tick() {
    let stunned = scripted_jump_arc_with_stun(1, 60, Some(6));
    let rise_ticks = stunned.apex_tick - stunned.takeoff_tick;
    println!(
        "dazzle stun over the release: grounded {}, vy {} milli u/s; rise \
         {:.3} px over {rise_ticks} ticks, {} jump(s), fit v0 {:.1}, ascent {:.0}",
        stunned.release_grounded,
        stunned.release_velocity_y_milli,
        stunned.rise_px,
        stunned.jumps,
        stunned.fit_v0,
        stunned.fit_ascent
    );
    assert!(
        stunned.release_grounded,
        "the one-tick press must still be released on a grounded tick"
    );
    assert_eq!(
        stunned.release_velocity_y_milli, 647_447,
        "the stunned release must be read while strongly rising"
    );
    assert_eq!(stunned.jumps, 1, "a one-tick press jumps once");
    assert!(
        (stunned.rise_px - 116.862).abs() < 0.001,
        "a release read while stunned must change nothing, got {:.3} px over \
         {rise_ticks} ticks (fit v0 {:.1}, ascent {:.0})",
        stunned.rise_px,
        stunned.fit_v0,
        stunned.fit_ascent
    );
    assert_eq!(
        rise_ticks, 23,
        "a release read while stunned must leave the whole rise alone"
    );
}

/// Ticket 050's correction, the eliminated fighter. `acts` is false on the
/// release tick, so control is skipped there; the elimination also ends the
/// round at the end of that same tick, so there is no later controlled tick
/// and the arc cannot carry the measurement. The memory itself is read
/// instead: it reaches no snapshot, and a release still standing in it is a
/// release waiting to be spent on whatever state control next sees.
#[test]
fn a_release_read_while_the_fighter_is_eliminated_is_not_left_standing() {
    let mut eliminated = teal_match_after_a_one_tick_press();
    assert!(
        jump_held(&eliminated),
        "the press must leave the release memory held"
    );
    eliminate_fighter(&mut eliminated);
    eliminated.step([PlayerInput::default(); 2]);
    assert!(
        !jump_held(&eliminated),
        "a release read while the fighter is eliminated must be spent on that \
         tick, not left standing for the next controlled tick"
    );
}

/// Ticket 050's correction, the revive. Every arena load, rematch reset and
/// repeated fight runs `revive_fighters`, which clears the fighter's transient
/// control state; the release memory belongs with the rest of it, so a fighter
/// that was still holding jump when its round ended starts the next one with
/// nothing to release.
#[test]
fn revive_fighters_clears_the_release_memory() {
    let mut revived = teal_match_after_a_one_tick_press();
    assert!(
        jump_held(&revived),
        "the press must leave the release memory held"
    );
    revived.revive_fighters();
    assert!(
        !jump_held(&revived),
        "revive_fighters must clear the release memory with the rest of the \
         fighter's transient control state"
    );
}

#[test]
fn public_match_end_replay_scores_once_then_respawns_and_freezes_gameplay() {
    let replay = run_profile_snapshots(
        ReplayProfile::MatchEndWaitingReplay,
        57,
        MATCH_END_WAITING_REPLAY_TICKS,
    );
    let at = |tick: u32| &replay[(tick - 1) as usize];
    assert_eq!(
        at(MATCH_END_DECISIVE_IMPACT_TICK)
            .impacts
            .last()
            .map(|impact| (
                impact.owner,
                impact.target,
                impact.damage,
                impact.eliminated,
            )),
        Some((1, Some(0), 100, true))
    );
    assert_eq!(
        at(MATCH_END_DECISIVE_IMPACT_TICK)
            .flow
            .as_ref()
            .unwrap()
            .phase,
        FlowPhase::EliminationConclusion
    );
    assert_eq!(
        at(MATCH_END_RESULT_TRANSITION_TICK)
            .flow
            .as_ref()
            .unwrap()
            .phase,
        FlowPhase::BlueResultTransition
    );
    assert_eq!(
        at(MATCH_END_ROUND_BLUE_TICK).flow.as_ref().unwrap().phase,
        FlowPhase::RoundBlue
    );
    let waiting = at(MATCH_END_WAITING_TICK);
    let flow = waiting.flow.as_ref().unwrap();
    assert_eq!(flow.phase, FlowPhase::Waiting);
    assert_eq!(flow.scores, [3, 5]);
    assert_eq!(flow.halves, [1, 2]);
    assert_eq!(
        flow.loadouts,
        [vec![ItemId::Dazzle], vec![ItemId::ExplosiveBullet]]
    );
    assert_eq!(flow.fighter_alive, [true, true]);
    assert!(waiting.projectiles.is_empty());
    assert_eq!(
        waiting
            .players
            .iter()
            .map(|player| (
                player.id,
                player.x_milli,
                player.y_milli,
                player.health,
                player.alive,
                player.grounded
            ))
            .collect::<Vec<_>>(),
        vec![
            (0, -520_000, -134_000, 100, true, true),
            (1, 520_000, -134_000, 100, true, true)
        ]
    );

    let mut authority =
        AuthoritativeMatch::new_with_profile(57, ReplayProfile::MatchEndWaitingReplay);
    let input = scripted_inputs_for(
        ReplayProfile::MatchEndWaitingReplay,
        57,
        MATCH_END_WAITING_TICK,
    );
    for tick in 0..MATCH_END_WAITING_TICK {
        authority.step([input[0][tick as usize], input[1][tick as usize]]);
    }
    let before = authority.snapshot();
    for _ in 0..20 {
        authority.step([
            PlayerInput {
                move_axis: 1,
                fire: true,
                flow: Some(FlowCommand {
                    phase_revision: before.flow.as_ref().unwrap().phase_revision,
                    action: FlowAction::VoteYes,
                }),
                ..PlayerInput::default()
            },
            PlayerInput {
                move_axis: -1,
                fire: true,
                ..PlayerInput::default()
            },
        ]);
    }
    let after = authority.snapshot();
    assert_eq!(after.players, before.players);
    assert_eq!(after.projectiles, before.projectiles);
    assert_eq!(after.flow.as_ref().unwrap().scores, [3, 5]);
    assert_eq!(after.flow.as_ref().unwrap().phase, FlowPhase::Waiting);
    assert_eq!(
        after.flow.as_ref().unwrap().last_results[0],
        ActionResult::WrongPhase
    );
}

#[test]
fn public_ring_out_match_end_returns_the_loser_to_a_visible_waiting_spawn() {
    let mut authority =
        AuthoritativeMatch::new_with_profile(58, ReplayProfile::MatchEndWaitingReplay);
    let mut observed_ring_out = false;
    for _ in 0..1_200 {
        authority.step([
            PlayerInput {
                move_axis: -1,
                ..PlayerInput::default()
            },
            PlayerInput::default(),
        ]);
        let snapshot = authority.snapshot();
        observed_ring_out |= snapshot.metrics.ring_outs > 0;
        if snapshot.flow.as_ref().unwrap().phase == FlowPhase::Waiting {
            assert!(observed_ring_out);
            assert_eq!(snapshot.flow.as_ref().unwrap().scores, [3, 5]);
            assert_eq!(
                snapshot
                    .players
                    .iter()
                    .map(|player| {
                        (
                            player.x_milli,
                            player.y_milli,
                            player.health,
                            player.alive,
                            player.grounded,
                        )
                    })
                    .collect::<Vec<_>>(),
                vec![
                    (-520_000, -134_000, 100, true, true),
                    (520_000, -134_000, 100, true, true)
                ]
            );
            return;
        }
    }
    panic!("orange never reached the public ring-out match end");
}

#[test]
fn public_lifecycle_begins_a_serializable_fresh_orange_draft_from_waiting() {
    let mut authority =
        AuthoritativeMatch::new_with_profile(57, ReplayProfile::MatchEndWaitingReplay);
    let inputs = scripted_inputs_for(
        ReplayProfile::MatchEndWaitingReplay,
        57,
        MATCH_END_WAITING_TICK,
    );
    for tick in 0..MATCH_END_WAITING_TICK {
        authority.step([inputs[0][tick as usize], inputs[1][tick as usize]]);
    }
    let waiting = authority.snapshot();
    assert_eq!(waiting.flow.as_ref().unwrap().phase, FlowPhase::Waiting);
    assert_eq!(waiting.flow.as_ref().unwrap().scores, [3, 5]);
    assert!(waiting.projectiles.is_empty());
    let session_tick = waiting.tick;
    let session_metrics = waiting.metrics.clone();

    assert_eq!(
        authority.request_lifecycle(LifecycleRequest::BeginNewMatch),
        LifecycleResult::Accepted
    );
    let reset = authority.snapshot();
    let flow = reset.flow.as_ref().unwrap();
    assert_eq!(flow.phase, FlowPhase::ArenaFade);
    assert_eq!(flow.scores, [0, 0]);
    assert_eq!(flow.halves, [0, 0]);
    assert_eq!((flow.winner, flow.eliminated), (None, None));
    assert!(flow.prior_badges.iter().all(Vec::is_empty));
    assert!(flow.loadouts.iter().all(Vec::is_empty));
    assert_eq!(flow.capabilities, [FighterCapabilities::default(); 2]);
    assert_eq!(flow.offers, new_match_source_offers());
    assert!(reset.projectiles.is_empty());
    assert!(reset.impacts.is_empty());
    assert!(reset.explosions.is_empty());
    assert_eq!(reset.winner, None);
    assert_eq!(reset.tick, session_tick);
    assert_eq!(reset.metrics, session_metrics);
    assert_eq!(reset.round.as_ref().unwrap().completed_rounds, Some([0, 0]));
    assert_eq!(
        reset
            .players
            .iter()
            .map(|player| (
                player.x_milli,
                player.y_milli,
                player.health,
                player.alive,
                player.grounded,
            ))
            .collect::<Vec<_>>(),
        vec![
            (-520_000, -134_000, 100, true, true),
            (520_000, -134_000, 100, true, true)
        ]
    );
    let before_repeat = reset.clone();
    assert_eq!(
        authority.request_lifecycle(LifecycleRequest::BeginNewMatch),
        LifecycleResult::WrongPhase
    );
    assert_eq!(authority.snapshot(), before_repeat);

    for _ in 0..10 {
        authority.step([
            PlayerInput {
                move_axis: 1,
                fire: true,
                ..PlayerInput::default()
            },
            PlayerInput {
                move_axis: -1,
                fire: true,
                ..PlayerInput::default()
            },
        ]);
    }
    let inert_fade = authority.snapshot();
    assert!(inert_fade.projectiles.is_empty());
    assert_eq!(inert_fade.players, reset.players);
    for _ in 10..150 {
        authority.step([PlayerInput::default(); 2]);
    }
    let draft = authority.snapshot();
    let flow = draft.flow.as_ref().unwrap();
    assert_eq!(flow.phase, FlowPhase::Draft);
    assert_eq!(flow.active_player, Some(0));
    assert_eq!(flow.offers, new_match_source_offers());
    assert_eq!(flow.hovered[0], Some(ItemId::SteadyShot));
    assert_eq!(draft.protocol, 10);
    let encoded = serde_json::to_vec(&draft).unwrap();
    assert_eq!(
        serde_json::from_slice::<MatchSnapshot>(&encoded).unwrap(),
        draft
    );

    let revision = flow.phase_revision;
    authority.step([
        PlayerInput {
            flow: Some(FlowCommand {
                phase_revision: revision,
                action: FlowAction::Hover(ItemId::Homing),
            }),
            ..PlayerInput::default()
        },
        PlayerInput::default(),
    ]);
    let hovered = authority.snapshot();
    assert_eq!(
        hovered.flow.as_ref().unwrap().last_results[0],
        ActionResult::Accepted
    );
    authority.step([
        PlayerInput {
            flow: Some(FlowCommand {
                phase_revision: revision,
                action: FlowAction::Confirm(ItemId::Homing),
            }),
            ..PlayerInput::default()
        },
        PlayerInput::default(),
    ]);
    let rejected = authority.snapshot();
    let flow = rejected.flow.as_ref().unwrap();
    assert_eq!(flow.last_results[0], ActionResult::UnimplementedItem);
    assert_eq!(flow.phase, FlowPhase::Draft);
    assert!(flow.loadouts.iter().all(Vec::is_empty));
    assert_eq!(flow.capabilities, [FighterCapabilities::default(); 2]);
}
