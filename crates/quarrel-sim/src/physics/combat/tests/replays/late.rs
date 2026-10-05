use super::super::super::*;
use super::super::lifecycle::connected_match_at_resumed_combat;

#[test]
fn either_color_can_sweep_or_win_the_deciding_ice_duel_with_ordinary_damage() {
    for pattern in [&[0, 0][..], &[1, 1], &[0, 1, 0], &[1, 0, 1]] {
        let scripts =
            scripted_inputs_for(ReplayProfile::RematchDraftReplay, SOURCE_DRAFT_SEED, 2_200);
        let mut game = AuthoritativeMatch::new_with_profile(
            SOURCE_DRAFT_SEED,
            ReplayProfile::RematchDraftReplay,
        );
        let mut previous = game.snapshot();
        let mut eliminated = Vec::new();
        let mut visited_ice = false;
        for tick in 0_u32..6_000 {
            let flow = previous.flow.as_ref().unwrap();
            let mut inputs = [PlayerInput::default(); 2];
            if tick < 2_200 {
                inputs = [scripts[0][tick as usize], scripts[1][tick as usize]];
            } else if matches!(
                flow.phase,
                FlowPhase::ResumedCombat | FlowPhase::TimberCombat | FlowPhase::IceCombat
            ) {
                let desired = pattern[usize::from(flow.halves.iter().sum::<u8>())];
                let first = flow.phase == FlowPhase::ResumedCombat;
                for (player, input) in inputs.iter_mut().enumerate() {
                    let dx =
                        previous.players[1 - player].x_milli - previous.players[player].x_milli;
                    *input = PlayerInput {
                        move_axis: if !first && dx.abs() > 45_000 {
                            dx.signum() as i8
                        } else {
                            0
                        },
                        jump: !first,
                        fire: player == usize::from(desired) && (first || dx.abs() < 400_000),
                        aim_at_opponent: true,
                        ..PlayerInput::default()
                    };
                }
            }
            game.step([
                inputs[0].with_progressive_observation(0, Some(&previous)),
                inputs[1].with_progressive_observation(1, Some(&previous)),
            ]);
            let state = game.snapshot();
            let flow = state.flow.as_ref().unwrap();
            visited_ice |= flow.phase == FlowPhase::IceCombat;
            if flow.phase == FlowPhase::EliminationConclusion
                && previous.flow.as_ref().unwrap().phase != flow.phase
            {
                let winner = flow.winner.unwrap();
                assert_eq!(state.players[usize::from(1 - winner)].health, 0);
                assert!(
                    state
                        .impacts
                        .iter()
                        .any(|hit| hit.tick == state.tick && hit.owner == winner && hit.eliminated)
                );
                eliminated.push(winner);
            }
            previous = state;
            if matches!(
                previous.flow.as_ref().unwrap().phase,
                FlowPhase::RoundBlue | FlowPhase::RoundOrange
            ) {
                break;
            }
        }
        assert_eq!(eliminated, pattern);
        assert_eq!(visited_ice, pattern.len() == 3);
        let winner = usize::from(*pattern.last().unwrap());
        let mut rounds = [0, 0];
        rounds[winner] = 1;
        assert_eq!(previous.flow.as_ref().unwrap().scores, rounds);
        assert_eq!(previous.flow.as_ref().unwrap().halves[winner], 2);
        assert_eq!(
            previous.flow.as_ref().unwrap().halves[1 - winner],
            u8::from(pattern.len() == 3)
        );
        for _ in 0..180 {
            game.step([PlayerInput::default(); 2]);
        }
        let draft = game.snapshot().flow.unwrap();
        let loser = 1 - winner;
        assert_eq!(draft.phase, FlowPhase::PostRoundDraft);
        assert_eq!(draft.active_player, Some(loser as u8));
        assert_eq!(draft.halves, [0, 0]);
        assert_eq!(draft.scores, rounds);
        assert_eq!(draft.offers[loser], first_loser_draft_offers());
        assert!(draft.offers[winner].is_empty());
    }
}

#[test]
fn connected_ice_keeps_identity_cleans_physics_and_awards_one_round_from_the_hit() {
    let scripts = scripted_inputs_for(
        ReplayProfile::RematchDraftReplay,
        SOURCE_DRAFT_SEED,
        CONNECTED_FIRST_ROUND_TICKS,
    );
    for omit_terminal_shot in [false, true] {
        let mut game = AuthoritativeMatch::new_with_profile(
            SOURCE_DRAFT_SEED,
            ReplayProfile::RematchDraftReplay,
        );
        let entities = game.player_entities;
        let fighter_bodies = game.physics.players.each_ref().map(|player| player.body);
        let mut previous = game.snapshot();
        assert_eq!(
            previous.round.as_ref().unwrap().completed_rounds,
            Some([4, 5])
        );
        assert_eq!(previous.round.as_ref().unwrap().scores, [0, 0]);
        let mut retired_entities = Vec::new();
        let mut retired_colliders = Vec::new();
        for tick in 0..CONNECTED_FIRST_ROUND_TICKS {
            if omit_terminal_shot && tick == 5_340 {
                break;
            }
            let mut inputs = [scripts[0][tick as usize], scripts[1][tick as usize]];
            if omit_terminal_shot && tick == 5_311 {
                inputs[1].fire = false;
            }
            game.step([
                inputs[0].with_progressive_observation(0, Some(&previous)),
                inputs[1].with_progressive_observation(1, Some(&previous)),
            ]);
            let state = game.snapshot();
            assert_eq!(game.player_entities, entities);
            assert_eq!(
                game.physics.players.each_ref().map(|player| player.body),
                fighter_bodies
            );
            if state.tick == CONNECTED_ICE_LOAD_TICK - 1 {
                retired_entities.extend(
                    game.dynamic_body_entities
                        .values()
                        .chain(game.constraint_entities.values())
                        .chain(game.projectile_entities.values())
                        .copied(),
                );
                retired_colliders.extend(
                    game.physics
                        .platforms
                        .iter()
                        .chain(&game.physics.retired_platforms)
                        .copied(),
                );
                assert!(!retired_entities.is_empty());
                assert!(!game.physics.retired_platforms.is_empty());
                for handle in &game.physics.retired_platforms {
                    assert_eq!(
                        game.physics.rapier.colliders[*handle]
                            .collision_groups()
                            .memberships,
                        Group::NONE
                    );
                }
            }
            if state.tick == CONNECTED_ICE_LOAD_TICK {
                assert_eq!(
                    state.arena_entry_from_milli,
                    Some([0, 1].map(|id| {
                        [previous.players[id].x_milli, previous.players[id].y_milli]
                    }))
                );
                assert!(
                    retired_entities
                        .iter()
                        .all(|entity| game.world.get_entity(*entity).is_err())
                );
                assert!(
                    retired_colliders.iter().all(|handle| !game
                        .physics
                        .rapier
                        .colliders
                        .contains(*handle))
                );
                assert!(game.physics.retired_platforms.is_empty());
                assert!(game.physics.dynamic_bodies.is_empty());
                assert!(game.physics.constraints.is_empty());
                assert!(game.physics.bullets.is_empty());
                assert_eq!(game.physics.rapier.bodies.len(), 19);
                assert_eq!(game.physics.rapier.colliders.len(), 19);
                assert_eq!(game.physics.rapier.impulse_joints.len(), 0);
                assert_eq!(state.arena.len(), 17);
                assert!(
                    state.dynamic_bodies.is_empty()
                        && state.constraints.is_empty()
                        && state.projectiles.is_empty()
                );
                assert_eq!(state.flow.as_ref().unwrap().halves, [1, 1]);
                assert_eq!(state.flow.as_ref().unwrap().scores, [0, 0]);
                assert!(
                    state
                        .players
                        .iter()
                        .all(|player| player.alive && player.health == 100)
                );
            }
            if state.tick >= CONNECTED_ICE_LOAD_TICK {
                assert_eq!(
                    state.flow.as_ref().unwrap().loadouts,
                    [vec![ItemId::Dazzle], vec![ItemId::ExplosiveBullet]]
                );
            }
            if state.tick == CONNECTED_ICE_COMBAT_TICK {
                assert!(state.arena_entry_from_milli.is_none());
            }
            if !omit_terminal_shot {
                if state.tick == 4_969 {
                    let orange = &state.players[0];
                    let blue = &state.players[1];
                    assert!((-230_000..-190_000).contains(&orange.x_milli));
                    assert!((150_000..210_000).contains(&orange.y_milli));
                    assert!((-125_000..-50_000).contains(&blue.x_milli));
                    assert!((0..65_000).contains(&blue.y_milli));
                    assert!(state.impacts.iter().any(|hit| {
                        hit.tick == 4_961
                            && hit.owner == 1
                            && hit.target.is_none()
                            && (-240_000..-200_000).contains(&hit.x_milli)
                            && (45_000..85_000).contains(&hit.y_milli)
                    }));
                }
                if state.tick == 5_213 {
                    let orange = &state.players[0];
                    let blue = &state.players[1];
                    assert!((-150_000..-110_000).contains(&orange.x_milli));
                    assert!((-230_000..-190_000).contains(&orange.y_milli));
                    assert!((-220_000..-180_000).contains(&blue.x_milli));
                    assert!((-20_000..25_000).contains(&blue.y_milli));
                }
                if state.tick == 5_312 {
                    assert!(
                        state
                            .impacts
                            .iter()
                            .any(|hit| hit.tick == 5_312 && hit.owner == 1 && hit.eliminated)
                    );
                    assert_eq!(state.players[0].health, 0);
                    assert_eq!(state.players[1].health, 100);
                    let hit = state.impacts.iter().find(|hit| hit.tick == 5_312).unwrap();
                    let fighter = &state.players[0];
                    assert!(
                        Vector::new(
                            (hit.x_milli - fighter.x_milli) as f32,
                            (hit.y_milli - fighter.y_milli) as f32,
                        )
                        .length()
                            <= 13_000.0
                    );
                }
                let expected = match state.tick {
                    5_338 => Some(FlowPhase::EliminationConclusion),
                    5_339 => Some(FlowPhase::BlueResultTransition),
                    5_355 => Some(FlowPhase::RoundBlue),
                    _ => None,
                };
                if let Some(phase) = expected {
                    assert_eq!(state.flow.as_ref().unwrap().phase, phase);
                }
            }
            previous = state;
        }
        let flow = previous.flow.as_ref().unwrap();
        if omit_terminal_shot {
            assert_eq!(flow.phase, FlowPhase::IceCombat);
            assert_eq!(flow.halves, [1, 1]);
            assert_eq!(flow.scores, [0, 0]);
            assert_eq!(previous.players[0].health, 25);
        } else {
            assert_eq!(flow.phase, FlowPhase::RoundBlue);
            assert_eq!(flow.halves, [1, 2]);
            assert_eq!(flow.scores, [0, 1]);
            assert_eq!(
                previous.round.as_ref().unwrap().completed_rounds,
                Some([0, 1])
            );
            for _ in 0..180 {
                game.step([PlayerInput::default(); 2]);
            }
            assert_eq!(game.snapshot().flow.unwrap().scores, [0, 1]);
        }
    }
}

#[test]
fn connected_first_loser_draft_follows_the_bound_public_input_cadence() {
    let replay = run_profile_snapshots(
        ReplayProfile::RematchDraftReplay,
        SOURCE_DRAFT_SEED,
        FIRST_LOSER_DRAFT_TICKS,
    );
    let at = |tick: u32| replay[(tick - 1) as usize].flow.as_ref().unwrap();

    let endpoint = at(5_466);
    assert_eq!(endpoint.phase, FlowPhase::RoundBlue);
    assert_eq!(endpoint.halves, [1, 2]);
    assert_eq!(endpoint.scores, [0, 1]);
    assert_eq!(
        endpoint.loadouts,
        [vec![ItemId::Dazzle], vec![ItemId::ExplosiveBullet]]
    );
    assert_eq!(at(5_493).phase, FlowPhase::RoundBlue);

    let entered = at(5_494);
    assert_eq!(entered.phase, FlowPhase::PostRoundDraft);
    assert_eq!(entered.active_player, Some(0));
    assert_eq!(entered.halves, [0, 0]);
    assert_eq!(entered.scores, [0, 1]);
    assert_eq!(entered.offers[0], first_loser_draft_offers());
    assert!(entered.offers[1].is_empty());
    assert_eq!(entered.hovered, [None, None]);
    assert_eq!(entered.selected, [None, None]);

    assert_eq!(at(5_588).hovered[0], Some(ItemId::Overpower));
    assert_eq!(at(5_710).hovered[0], Some(ItemId::QuickShot));
    let hovered = at(5_801);
    assert_eq!(hovered.phase, FlowPhase::PostRoundDraft);
    assert_eq!(hovered.hovered[0], Some(ItemId::QuickShot));
    assert_eq!(hovered.selected[0], None);
    assert_eq!(hovered.revealed, None);
    assert_eq!(
        hovered.loadouts[0],
        vec![ItemId::Dazzle],
        "hover does not apply the item"
    );
    let confirmed = at(5_802);
    assert_eq!(confirmed.phase, FlowPhase::PostRoundReveal);
    assert_eq!(confirmed.phase_tick, 0);
    assert_eq!(confirmed.hovered[0], Some(ItemId::QuickShot));
    assert_eq!(confirmed.selected[0], Some(ItemId::QuickShot));
    assert_eq!(confirmed.revealed, Some(ItemId::QuickShot));
    assert_eq!(
        confirmed.loadouts,
        [
            vec![ItemId::Dazzle, ItemId::QuickShot],
            vec![ItemId::ExplosiveBullet]
        ]
    );
    assert_eq!(confirmed.capabilities[0].dazzle_stun_pulses, 3);
    assert_eq!(confirmed.capabilities[0].dazzle_stun_ticks, 6);
    assert_eq!(confirmed.capabilities[0].fire_cooldown_extra_ticks, 15);
    assert!(confirmed.capabilities[0].projectile_speed_factor.milli > 1_000);
    assert_eq!(confirmed.capabilities[1], endpoint.capabilities[1]);
    let before_bridge = at(5_817);
    assert_eq!(before_bridge.phase, FlowPhase::PostRoundReveal);
    assert_eq!(before_bridge.revealed, Some(ItemId::QuickShot));
    let bridge = at(5_818);
    assert_eq!(bridge.phase, FlowPhase::PostRoundBridge);
    assert_eq!(bridge.phase_tick, 0);
    assert_eq!(bridge.revealed, None);
    assert_eq!(bridge.selected[0], Some(ItemId::QuickShot));
    let final_flow = at(FIRST_LOSER_DRAFT_TICKS);
    assert_eq!(final_flow.phase, FlowPhase::PostRoundBridge);
    assert_eq!(final_flow.scores, [0, 1]);
    assert_eq!(final_flow.halves, [0, 0]);
    assert_eq!(
        replay
            .last()
            .unwrap()
            .round
            .as_ref()
            .unwrap()
            .completed_rounds,
        Some([0, 1])
    );
}

#[test]
fn connected_route_enters_one_presentation_only_held_hanging_scene() {
    let scripts = scripted_inputs_for(
        ReplayProfile::RematchDraftReplay,
        SOURCE_DRAFT_SEED,
        HELD_HANGING_ENTRY_TICKS,
    );
    let mut game =
        AuthoritativeMatch::new_with_profile(SOURCE_DRAFT_SEED, ReplayProfile::RematchDraftReplay);
    let mut previous = game.snapshot();
    let mut accepted_before_entry = None;
    for tick in 0..HELD_HANGING_ENTRY_TICKS {
        let inputs = if tick >= FIRST_LOSER_DRAFT_TICKS {
            [
                PlayerInput {
                    move_axis: 1,
                    jump: true,
                    fire: true,
                    block: true,
                    ..PlayerInput::default()
                },
                PlayerInput {
                    move_axis: -1,
                    jump: true,
                    fire: true,
                    block: true,
                    ..PlayerInput::default()
                },
            ]
        } else {
            [scripts[0][tick as usize], scripts[1][tick as usize]]
        };
        game.step([
            inputs[0].with_progressive_observation(0, Some(&previous)),
            inputs[1].with_progressive_observation(1, Some(&previous)),
        ]);
        let state = game.snapshot();
        if state.tick == FIRST_LOSER_DRAFT_TICKS {
            accepted_before_entry = Some(state.flow.as_ref().unwrap().accepted_actions);
        }
        if state.tick == 5_894 {
            let flow = state.flow.as_ref().unwrap();
            assert_eq!(flow.phase, FlowPhase::HangingEntry);
            assert_eq!(flow.phase_tick, 0);
            assert_eq!(flow.scores, [0, 1]);
            assert_eq!(flow.halves, [0, 0]);
            assert_eq!(flow.winner, None);
            assert_eq!(flow.eliminated, None);
            assert_eq!(flow.fighter_alive, [true, true]);
            assert!(state.arena.is_empty());
            assert!(state.dynamic_bodies.is_empty());
            assert!(state.constraints.is_empty());
            assert!(state.projectiles.is_empty());
            assert!(game.physics.platforms.is_empty());
            assert!(game.physics.dynamic_bodies.is_empty());
            assert!(game.physics.constraints.is_empty());
            assert!(game.physics.bullets.is_empty());
            let hanging = state.hanging_entry.as_ref().unwrap();
            assert_eq!(hanging.bodies.len(), 21);
            let first = hanging.bodies.iter().find(|body| body.id == 410).unwrap();
            assert_eq!(first.body_x_milli - first.body_width_milli / 2, 634_000);
        }
        if state.tick == 5_914 {
            let hanging = state.hanging_entry.as_ref().unwrap();
            let central = hanging.bodies.iter().find(|body| body.id == 415).unwrap();
            assert_eq!(central.body_x_milli, 0);
            assert_eq!(central.square_x_milli, 30_500);
        }
        if state.tick >= 5_894 {
            assert_eq!(
                state
                    .players
                    .iter()
                    .map(|player| (player.x_milli, player.y_milli))
                    .collect::<Vec<_>>(),
                [(-405_000, 53_000), (405_000, 87_000)]
            );
            assert!(state.players.iter().all(|player| {
                player.alive
                    && player.health == 100
                    && player.velocity_x_milli_per_second == 0
                    && player.velocity_y_milli_per_second == 0
            }));
            assert_eq!(state.metrics, previous.metrics);
        }
        previous = state;
    }
    let flow = previous.flow.as_ref().unwrap();
    assert_eq!(flow.phase, FlowPhase::HangingEntry);
    assert_eq!(flow.phase_tick, 47);
    assert_eq!(flow.accepted_actions, accepted_before_entry.unwrap());
    assert_eq!(
        flow.loadouts,
        [
            vec![ItemId::Dazzle, ItemId::QuickShot],
            vec![ItemId::ExplosiveBullet]
        ]
    );
    assert_eq!(
        previous.round.as_ref().unwrap().completed_rounds,
        Some([0, 1])
    );
    let hanging = previous.hanging_entry.unwrap();
    assert_eq!(hanging.age_ticks, 47);
    assert_eq!(
        hanging
            .bodies
            .iter()
            .map(|body| body.id)
            .collect::<Vec<_>>(),
        (400..=420).collect::<Vec<_>>()
    );
    assert!(hanging.bodies.iter().all(|body| {
        body.body_x_milli == body.nominal_x_milli && body.square_x_milli == body.nominal_x_milli
    }));
}

#[test]
fn connected_first_loser_draft_rejects_invalid_public_flow_commands_without_mutation() {
    type RetainedState = (
        [u8; 2],
        [u8; 2],
        Option<[u8; 2]>,
        [Vec<ItemId>; 2],
        [FighterCapabilities; 2],
    );

    fn retained(snapshot: &MatchSnapshot) -> RetainedState {
        (
            snapshot.flow.as_ref().unwrap().halves,
            snapshot.flow.as_ref().unwrap().scores,
            snapshot
                .round
                .as_ref()
                .and_then(|round| round.completed_rounds),
            snapshot.flow.as_ref().unwrap().loadouts.clone(),
            snapshot.flow.as_ref().unwrap().capabilities,
        )
    }

    fn step_flow(
        game: &mut AuthoritativeMatch,
        player: usize,
        command: FlowCommand,
    ) -> MatchSnapshot {
        let previous = game.snapshot();
        let mut inputs = [PlayerInput::default(); 2];
        inputs[player].flow = Some(command);
        game.step([
            inputs[0].with_progressive_observation(0, Some(&previous)),
            inputs[1].with_progressive_observation(1, Some(&previous)),
        ]);
        game.snapshot()
    }

    fn assert_flow_result(
        game: &mut AuthoritativeMatch,
        player: usize,
        phase_revision: u16,
        action: FlowAction,
        expected_result: ActionResult,
        expected_state: &RetainedState,
    ) -> MatchSnapshot {
        let snapshot = step_flow(
            game,
            player,
            FlowCommand {
                phase_revision,
                action,
            },
        );
        assert_eq!(
            snapshot.flow.as_ref().unwrap().last_results[player],
            expected_result
        );
        assert_eq!(&retained(&snapshot), expected_state);
        snapshot
    }

    let ticks = 5_494;
    let scripts = scripted_inputs_for(ReplayProfile::RematchDraftReplay, SOURCE_DRAFT_SEED, ticks);
    let mut game =
        AuthoritativeMatch::new_with_profile(SOURCE_DRAFT_SEED, ReplayProfile::RematchDraftReplay);
    let mut previous = game.snapshot();
    for (player_zero, player_one) in scripts[0].iter().zip(&scripts[1]) {
        game.step([
            player_zero.with_progressive_observation(0, Some(&previous)),
            player_one.with_progressive_observation(1, Some(&previous)),
        ]);
        previous = game.snapshot();
    }

    let entered = game.snapshot();
    let flow = entered.flow.as_ref().unwrap();
    assert_eq!(flow.phase, FlowPhase::PostRoundDraft);
    assert_eq!(flow.active_player, Some(0));
    let revision = flow.phase_revision;
    let baseline = retained(&entered);

    for action in [
        FlowAction::Hover(ItemId::QuickShot),
        FlowAction::Confirm(ItemId::QuickShot),
    ] {
        assert_flow_result(
            &mut game,
            1,
            revision,
            action,
            ActionResult::WrongPlayer,
            &baseline,
        );
    }

    assert_flow_result(
        &mut game,
        0,
        revision - 1,
        FlowAction::Hover(ItemId::QuickShot),
        ActionResult::Stale,
        &baseline,
    );

    assert_flow_result(
        &mut game,
        0,
        revision,
        FlowAction::Hover(ItemId::Dazzle),
        ActionResult::NotOffered,
        &baseline,
    );

    for item in [
        ItemId::ColdBullets,
        ItemId::CarefulPlanning,
        ItemId::Overpower,
        ItemId::BigBullet,
    ] {
        let hovered = assert_flow_result(
            &mut game,
            0,
            revision,
            FlowAction::Hover(item),
            ActionResult::Accepted,
            &baseline,
        );
        assert_eq!(hovered.flow.as_ref().unwrap().hovered[0], Some(item));

        let unimplemented = assert_flow_result(
            &mut game,
            0,
            revision,
            FlowAction::Confirm(item),
            ActionResult::UnimplementedItem,
            &baseline,
        );
        assert_eq!(unimplemented.flow.as_ref().unwrap().hovered[0], Some(item));
    }

    assert_flow_result(
        &mut game,
        0,
        revision,
        FlowAction::Confirm(ItemId::QuickShot),
        ActionResult::NotHovered,
        &baseline,
    );

    assert_flow_result(
        &mut game,
        0,
        revision,
        FlowAction::Hover(ItemId::QuickShot),
        ActionResult::Accepted,
        &baseline,
    );
    let confirmed = step_flow(
        &mut game,
        0,
        FlowCommand {
            phase_revision: revision,
            action: FlowAction::Confirm(ItemId::QuickShot),
        },
    );
    assert_eq!(
        confirmed.flow.as_ref().unwrap().last_results[0],
        ActionResult::Accepted
    );
    let confirmed_retained = retained(&confirmed);
    assert_eq!(
        confirmed_retained.3,
        [
            vec![ItemId::Dazzle, ItemId::QuickShot],
            vec![ItemId::ExplosiveBullet],
        ]
    );

    let reveal_revision = confirmed.flow.as_ref().unwrap().phase_revision;
    assert_flow_result(
        &mut game,
        0,
        reveal_revision,
        FlowAction::Confirm(ItemId::QuickShot),
        ActionResult::Duplicate,
        &confirmed_retained,
    );
}

#[test]
fn projectile_speed_factor_is_typed_and_threaded_through_the_spawn_boundary() {
    let default = FighterCapabilities::default();
    assert_eq!(projectile_launch_speed(default), BULLET_SPEED);

    let confirmed = run_profile_match(ReplayProfile::RematchDraftReplay, SOURCE_DRAFT_SEED, 5_802)
        .0
        .flow
        .unwrap();
    assert!(confirmed.loadouts[0].contains(&ItemId::QuickShot));
    let quick_shot = confirmed.capabilities[0];
    assert_eq!(quick_shot.projectile_speed_factor.milli, 1_250);
    assert_ne!(
        quick_shot.projectile_speed_factor,
        default.projectile_speed_factor
    );
    assert_eq!(quick_shot.dazzle_stun_pulses, 3);
    assert_eq!(quick_shot.dazzle_stun_ticks, 6);
    assert_eq!(quick_shot.fire_cooldown_extra_ticks, 15);

    let (mut game, previous) = connected_match_at_resumed_combat();
    game.flow
        .as_mut()
        .unwrap()
        .copy_player_build_for_test(0, &confirmed);
    let equipped = game.snapshot().flow.unwrap();
    assert!(equipped.loadouts[0].contains(&ItemId::QuickShot));
    assert_eq!(equipped.capabilities[0], quick_shot);
    game.step([
        PlayerInput {
            fire: true,
            aim_x: 1_000,
            ..PlayerInput::default()
        }
        .with_progressive_observation(0, Some(&previous)),
        PlayerInput::default().with_progressive_observation(1, Some(&previous)),
    ]);
    let projectile = game
        .snapshot()
        .projectiles
        .into_iter()
        .find(|projectile| projectile.owner == 0)
        .expect("the resumed-combat input spawns orange's projectile");
    let speed = Vector::new(
        projectile.velocity_x_milli_per_second as f32,
        projectile.velocity_y_milli_per_second as f32,
    )
    .length()
        / 1_000.0;
    let expected_speed = BULLET_SPEED * f32::from(quick_shot.projectile_speed_factor.milli)
        / f32::from(ProjectileSpeedFactor::default().milli);
    assert_eq!(expected_speed, 4_500.0);
    assert!((speed - expected_speed).abs() < 0.01, "{speed}");
}

#[test]
fn explosive_weight_contact_keeps_timber_supports_attached() {
    let mut authority =
        AuthoritativeMatch::new_with_profile(SOURCE_DRAFT_SEED, ReplayProfile::RematchDraftReplay);
    let trace = scripted_inputs_for(
        ReplayProfile::RematchDraftReplay,
        SOURCE_DRAFT_SEED,
        CONNECTED_TIMBER_COMBAT_TICK,
    );
    for (&orange, &blue) in trace[0].iter().zip(&trace[1]) {
        authority.step([orange, blue]);
    }
    let before = authority.snapshot();
    for tick in 0..5 {
        authority.step([
            PlayerInput::default(),
            PlayerInput {
                aim_x: 71,
                aim_y: 1_000,
                fire: tick == 0,
                ..PlayerInput::default()
            },
        ]);
    }
    let after = authority.snapshot();
    assert_eq!(after.explosions.len(), before.explosions.len() + 1);
    assert_eq!(after.explosions.last().unwrap().tick, 2_735);
    assert_eq!(after.metrics.released_constraints, 0);
    assert!(after.constraints.iter().all(|constraint| constraint.active));
}

#[test]
fn connected_collapse_requires_the_drafted_explosive_card_and_actual_impact_input() {
    let run = |replace_card: bool, omit_impact: bool| {
        let mut simulation = AuthoritativeMatch::new_with_profile(
            SOURCE_DRAFT_SEED,
            ReplayProfile::RematchDraftReplay,
        );
        let mut inputs =
            scripted_inputs_for(ReplayProfile::RematchDraftReplay, SOURCE_DRAFT_SEED, 3_700);
        if replace_card {
            inputs[1][2_060].flow.as_mut().unwrap().action = FlowAction::Hover(ItemId::Dazzle);
            inputs[1][2_100].flow.as_mut().unwrap().action = FlowAction::Confirm(ItemId::Dazzle);
        }
        if omit_impact {
            inputs[1][3_650].fire = false;
        }
        for [orange, blue] in inputs[0]
            .iter()
            .copied()
            .zip(inputs[1].iter().copied())
            .map(|(a, b)| [a, b])
        {
            let observation = simulation.snapshot();
            simulation.step([
                orange.with_progressive_observation(0, Some(&observation)),
                blue.with_progressive_observation(1, Some(&observation)),
            ]);
        }
        simulation.snapshot()
    };
    let nominal = run(false, false);
    assert_eq!(nominal.metrics.released_constraints, 17);
    for changed in [run(true, false), run(false, true)] {
        assert_eq!(
            changed.flow.as_ref().unwrap().phase,
            FlowPhase::TimberCombat
        );
        assert_eq!(changed.metrics.released_constraints, 0);
        assert!(changed.constraints.iter().all(|joint| joint.active));
        assert_ne!(dynamic_body_digest(&nominal), dynamic_body_digest(&changed));
    }
}

#[test]
fn rematch_clears_the_terminal_winner_and_elimination_in_match_state() {
    let mut simulation =
        AuthoritativeMatch::new_with_profile(SOURCE_DRAFT_SEED, ReplayProfile::RematchDraftReplay);
    let concluded = simulation.snapshot();
    assert_eq!(concluded.winner, Some(1));
    assert!(!concluded.players[0].alive);
    assert_eq!(concluded.players[0].health, 0);
    assert!(concluded.players[1].alive);

    let scripts = scripted_inputs_for(ReplayProfile::RematchDraftReplay, SOURCE_DRAFT_SEED, 331);
    for (&orange, &blue) in scripts[0].iter().zip(&scripts[1]) {
        simulation.step([orange, blue]);
    }
    let reset = simulation.snapshot();
    assert_eq!(reset.winner, None);
    assert!(reset.players.iter().all(|player| player.alive));
    assert!(reset.players.iter().all(|player| player.health == 100));
    let flow = reset.flow.unwrap();
    assert_eq!(flow.scores, [0, 0]);
    assert!(flow.prior_badges.iter().all(Vec::is_empty));
}

#[test]
fn selected_cards_mark_real_projectiles_and_apply_typed_impact_behaviors() {
    let mut dazzle =
        AuthoritativeMatch::new_with_profile(SOURCE_DRAFT_SEED, ReplayProfile::RematchDraftReplay);
    let scripts = scripted_inputs_for(ReplayProfile::RematchDraftReplay, SOURCE_DRAFT_SEED, 2_221);
    for (&orange, &blue) in scripts[0].iter().zip(&scripts[1]).take(2_220) {
        dazzle.step([orange, blue]);
    }
    dazzle.physics.rapier.bodies[dazzle.physics.players[0].body]
        .set_translation(Vector::new(-120.0, 80.0), true);
    dazzle.physics.rapier.bodies[dazzle.physics.players[1].body]
        .set_translation(Vector::new(120.0, 80.0), true);
    for entity in dazzle.player_entities {
        let mut state = dazzle.world.entity_mut(entity);
        let mut player = state.get_mut::<PlayerState>().unwrap();
        player.alive = true;
        player.health = 100;
        player.fire_cooldown = 0;
        player.stun_ticks = 0;
        player.stun_pulses_remaining = 0;
    }
    dazzle.step([scripts[0][2_220], PlayerInput::default()]);
    let fired = dazzle.snapshot();
    assert!(
        fired
            .projectiles
            .iter()
            .any(|bullet| bullet.owner == 0 && bullet.dazzle_pulses == 3)
    );
    for _ in 0..12 {
        dazzle.step([PlayerInput::default(); 2]);
    }
    assert!(dazzle.snapshot().metrics.dazzle_stun_pulses >= 1);

    let mut explosive =
        AuthoritativeMatch::new_with_profile(SOURCE_DRAFT_SEED, ReplayProfile::RematchDraftReplay);
    for (&orange, &blue) in scripts[0].iter().zip(&scripts[1]) {
        explosive.step([orange, blue]);
    }
    for _ in scripts[0].len()..2_240 {
        explosive.step([PlayerInput::default(); 2]);
    }
    explosive.physics.rapier.bodies[explosive.physics.players[0].body]
        .set_translation(Vector::new(-120.0, 80.0), true);
    explosive.physics.rapier.bodies[explosive.physics.players[1].body]
        .set_translation(Vector::new(120.0, 80.0), true);
    for entity in explosive.player_entities {
        let mut state = explosive.world.entity_mut(entity);
        let mut player = state.get_mut::<PlayerState>().unwrap();
        player.alive = true;
        player.health = 100;
        player.fire_cooldown = 0;
        player.stun_ticks = 0;
        player.stun_pulses_remaining = 0;
    }
    explosive.step([
        PlayerInput::default(),
        PlayerInput {
            fire: true,
            aim_x: -1_000,
            ..PlayerInput::default()
        },
    ]);
    let fired = explosive.snapshot();
    assert!(
        fired
            .projectiles
            .iter()
            .any(|bullet| bullet.owner == 1 && bullet.explosive_radius_milli == 150_000)
    );
    for _ in 0..5 {
        explosive.step([PlayerInput::default(); 2]);
    }
    let impact = explosive.snapshot();
    assert_eq!(impact.metrics.explosive_projectile_impacts, 1);
    assert!(
        impact
            .explosions
            .iter()
            .any(|explosion| explosion.id >= 10_000)
    );
}
