use super::super::super::*;

#[test]
fn yellow_terminal_blast_is_one_continuous_authoritative_physics_replay() {
    let replay = run_profile_snapshots(
        ReplayProfile::YellowCrateTerminalBlastReplay,
        43,
        YELLOW_REPLAY_TICKS,
    );
    assert_eq!(replay.len(), 155);
    let calm_start = &replay[0];
    let calm_midpoint = &replay[39];
    let calm = &replay[(YELLOW_LAST_CALM_TICK - 1) as usize];
    let impact = &replay[(YELLOW_IMPACT_TICK - 1) as usize];
    let last_combat = &replay[(YELLOW_LAST_COMBAT_TICK - 1) as usize];
    let result_onset = &replay[(YELLOW_RESULT_ONSET_TICK - 1) as usize];
    let following_result = &replay[(YELLOW_FOLLOWING_RESULT_TICK - 1) as usize];
    let orange = &replay[(YELLOW_ROUND_ORANGE_TICK - 1) as usize];
    let fighter_separation = |snapshot: &MatchSnapshot| {
        (snapshot.players[1].x_milli - snapshot.players[0].x_milli).abs()
    };
    assert!(
        fighter_separation(calm_start) > fighter_separation(calm_midpoint)
            && fighter_separation(calm_midpoint) > fighter_separation(calm),
        "fighters did not converge through calm snapshots: start={:?}, midpoint={:?}, end={:?}",
        calm_start.players,
        calm_midpoint.players,
        calm.players
    );
    assert!(calm.impacts.is_empty() && calm.explosions.is_empty());
    assert_eq!(calm.round.as_ref().unwrap().phase, RoundPhase::Combat);
    let event = impact.impacts.last().expect("terminal projectile contact");
    assert_eq!((event.tick, event.owner, event.target), (81, 0, Some(1)));
    assert!(event.damage > 0 && event.eliminated);
    let target = &impact.players[usize::from(event.target.unwrap())];
    let distance = Vector::new(
        (event.x_milli - target.x_milli) as f32 / 1_000.0,
        (event.y_milli - target.y_milli) as f32 / 1_000.0,
    )
    .length();
    assert!(
        distance <= PLAYER_RADIUS + 1.0,
        "hit effect must originate on the struck fighter, not the projectile's later endpoint: {distance}"
    );
    let explosion = impact.explosions.last().unwrap();
    assert_eq!(
        (event.x_milli, event.y_milli),
        (explosion.x_milli, explosion.y_milli)
    );
    assert_eq!(
        (event.impulse_x_milli, event.impulse_y_milli),
        (3_870_000, 0),
        "published impulse must equal the complete health-scaled hit plus explosion impulse applied by authority"
    );
    assert_eq!(impact.explosions.last().unwrap().tick, 81);
    assert_eq!(impact.winner, Some(0));
    assert!(!impact.players[1].alive);
    assert_eq!(impact.dynamic_bodies.len(), 20);
    assert!(
        impact
            .dynamic_bodies
            .windows(2)
            .all(|pair| pair[0].id < pair[1].id)
    );
    assert!(impact.dynamic_bodies.iter().all(|body| {
        body.shape == DynamicBodyShape::Crate
            && body.mass_milli > 0
            && body.friction_milli > 0
            && body.restitution_milli > 0
    }));
    let calm_crate = calm
        .dynamic_bodies
        .iter()
        .find(|body| body.id == 315)
        .unwrap();
    let moved_crate = last_combat
        .dynamic_bodies
        .iter()
        .find(|body| body.id == 315)
        .unwrap();
    assert!(
        (moved_crate.x_milli - calm_crate.x_milli).abs() > 15_000
            || (moved_crate.y_milli - calm_crate.y_milli).abs() > 15_000
            || (moved_crate.rotation_milliradians - calm_crate.rotation_milliradians).abs() > 120
    );
    assert_eq!(
        last_combat.round.as_ref().unwrap().phase,
        RoundPhase::Combat
    );
    assert_eq!(
        result_onset.round.as_ref().unwrap().phase,
        RoundPhase::ResultTransition
    );
    assert_eq!(result_onset.round.as_ref().unwrap().phase_tick, 0);
    assert_eq!(result_onset.round.as_ref().unwrap().scores, [3, 1]);
    assert_eq!(
        following_result.round.as_ref().unwrap().phase,
        RoundPhase::ResultTransition
    );
    assert_eq!(following_result.round.as_ref().unwrap().phase_tick, 1);
    assert_eq!(
        orange.round.as_ref().unwrap().phase,
        RoundPhase::RoundOrange
    );
    assert_eq!(
        hash_snapshot(replay.last().unwrap()),
        run_profile_match(
            ReplayProfile::YellowCrateTerminalBlastReplay,
            43,
            YELLOW_REPLAY_TICKS,
        )
        .1
    );
}

#[test]
fn yellow_crate_trajectory_changes_when_authority_impulse_changes() {
    let mut nominal =
        AuthoritativeMatch::new_with_profile(43, ReplayProfile::YellowCrateTerminalBlastReplay);
    let mut perturbed =
        AuthoritativeMatch::new_with_profile(43, ReplayProfile::YellowCrateTerminalBlastReplay);
    perturbed.explosion_strength *= 0.5;
    let scripts = scripted_inputs_for(
        ReplayProfile::YellowCrateTerminalBlastReplay,
        43,
        YELLOW_LAST_COMBAT_TICK,
    );
    for (&orange, &blue) in scripts[0].iter().zip(&scripts[1]) {
        nominal.step([orange, blue]);
        perturbed.step([orange, blue]);
    }
    assert_ne!(
        dynamic_body_digest(&nominal.snapshot()),
        dynamic_body_digest(&perturbed.snapshot())
    );
}

#[test]
fn platform_contact_and_asymmetric_route_match_the_source_order() {
    let replay = run_profile_snapshots(ReplayProfile::TealDuelReplay, 38, TEAL_REPLAY_TICKS);
    let source_24_50 = &replay[119];
    assert!(source_24_50.players[0].x_milli < -480_000);
    assert!(source_24_50.players[0].grounded);
    assert!(source_24_50.players[1].x_milli > 250_000);
    assert!(source_24_50.players[1].y_milli > -80_000);
    assert!(source_24_50.metrics.platform_contact_ticks > 120);
    let terminal = replay.last().unwrap();
    assert!(
        terminal
            .players
            .iter()
            .all(|player| player.x_milli > 300_000 && player.y_milli > -100_000)
    );
}

#[test]
fn lime_modular_geometry_binds_every_measured_surface() {
    let arena = lime_modular_arena();
    assert_eq!(arena.len(), 33);
    assert_eq!(
        arena.iter().map(|surface| surface.id).collect::<Vec<_>>(),
        (0..33).collect::<Vec<_>>()
    );
    assert_eq!(
        arena
            .iter()
            .map(|surface| surface.outline_milli.len())
            .collect::<Vec<_>>(),
        [
            10, 10, 10, 10, 10, 4, 4, 4, 4, 12, 12, 12, 12, 12, 4, 4, 4, 4, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0
        ]
    );
    // Pixel bounds measured across the four exact source frames. Check
    // both the authoritative records and Rapier's fixed colliders.
    let physics = PhysicsBoundary::new(ReplayProfile::LimeModularArenaReplay);
    assert_eq!(physics.platforms.len(), arena.len());
    for (surface, handle) in arena.iter().zip(&physics.platforms) {
        let (left, top, right, bottom) = match surface.id {
            0..=4 => {
                let x = [42, 312, 582, 852, 1122][surface.id as usize];
                (x as f32, 365.0, (x + 116) as f32, 522.0)
            }
            5..=8 => {
                let x = [217, 495, 747, 1027][(surface.id - 5) as usize];
                (x as f32, 441.0, (x + 36) as f32, 579.0)
            }
            9..=13 => {
                let x = [45, 315, 585, 855, 1125][(surface.id - 9) as usize];
                (x as f32, 611.0, (x + 109) as f32, 720.0)
            }
            14..=17 => {
                let x = [217, 495, 747, 1027][(surface.id - 14) as usize];
                (x as f32, 693.0, (x + 36) as f32, 720.0)
            }
            18..=32 => {
                let center = [100, 370, 640, 910, 1180][((surface.id - 18) / 3) as usize];
                match (surface.id - 18) % 3 {
                    0 => (center as f32 - 18.5, 292.5, center as f32 + 18.5, 329.5),
                    1 => (center as f32 - 40.0, 327.5, center as f32 - 2.0, 364.5),
                    _ => (center as f32 + 2.0, 327.5, center as f32 + 40.0, 364.5),
                }
            }
            _ => unreachable!(),
        };
        let bounds = |world_left: f32, world_bottom: f32, world_right: f32, world_top: f32| {
            [
                640.0 + world_left,
                360.0 - world_top,
                640.0 + world_right,
                360.0 - world_bottom,
            ]
        };
        let half_width = surface.width_milli as f32 / 2_000.0;
        let half_height = surface.height_milli as f32 / 2_000.0;
        let center_x = surface.center_x_milli as f32 / 1_000.0;
        let center_y = surface.center_y_milli as f32 / 1_000.0;
        let snapshot_bounds = bounds(
            center_x - half_width,
            center_y - half_height,
            center_x + half_width,
            center_y + half_height,
        );
        let collider = &physics.rapier.colliders[*handle];
        let aabb = collider.compute_aabb();
        let collider_bounds = bounds(aabb.mins.x, aabb.mins.y, aabb.maxs.x, aabb.maxs.y);
        for (actual, measured) in [snapshot_bounds, collider_bounds]
            .into_iter()
            .flat_map(|values| values.into_iter().zip([left, top, right, bottom]))
        {
            assert!(
                (actual - measured).abs() <= 2.0,
                "surface {} edge {actual} differs from measured {measured}",
                surface.id
            );
        }
        let measured_vertices: Vec<[f32; 2]> = match surface.id {
            0..=4 => {
                let x = left;
                [
                    [0, 365],
                    [116, 365],
                    [116, 393],
                    [90, 419],
                    [76, 419],
                    [76, 522],
                    [40, 522],
                    [40, 419],
                    [26, 419],
                    [0, 393],
                ]
                .map(|[dx, y]| [x + dx as f32, y as f32])
                .to_vec()
            }
            5..=8 | 14..=17 => vec![[left, top], [right, top], [right, bottom], [left, bottom]],
            9..=13 => {
                let x = left;
                [
                    [36, 611],
                    [73, 611],
                    [73, 647],
                    [109, 647],
                    [109, 686],
                    [73, 686],
                    [73, 720],
                    [36, 720],
                    [36, 686],
                    [0, 686],
                    [0, 647],
                    [36, 647],
                ]
                .map(|[dx, y]| [x + dx as f32, y as f32])
                .to_vec()
            }
            _ => Vec::new(),
        };
        let mut actual_vertices = surface
            .outline_milli
            .iter()
            .map(|[x, y]| {
                [
                    640.0 + (surface.center_x_milli + x) as f32 / 1_000.0,
                    360.0 - (surface.center_y_milli + y) as f32 / 1_000.0,
                ]
            })
            .collect::<Vec<_>>();
        let sort = |a: &[f32; 2], b: &[f32; 2]| a[0].total_cmp(&b[0]).then(a[1].total_cmp(&b[1]));
        let mut measured_vertices = measured_vertices;
        actual_vertices.sort_by(sort);
        measured_vertices.sort_by(sort);
        assert_eq!(actual_vertices.len(), measured_vertices.len());
        for (actual, measured) in actual_vertices.iter().zip(measured_vertices.iter()) {
            assert!(
                (actual[0] - measured[0]).abs() <= 2.0 && (actual[1] - measured[1]).abs() <= 2.0,
                "surface {} contour {actual:?} differs from measured {measured:?}",
                surface.id
            );
        }
    }
    let snapshot =
        AuthoritativeMatch::new_with_profile(59, ReplayProfile::LimeModularArenaReplay).snapshot();
    assert_eq!(
        arena_digest(&snapshot),
        "2a1a0fd5dcba20f0759e7d908235cd76bc7547503dede2ce599eb3b546715a8b"
    );
    assert!(snapshot.dynamic_bodies.is_empty());
    assert!(snapshot.constraints.is_empty());
}

#[test]
fn lime_public_route_leaves_both_fighters_supported_on_upper_modules() {
    let mut shelf = AuthoritativeMatch::new_with_profile(59, ReplayProfile::LimeModularArenaReplay);
    for _ in 0..30 {
        shelf.step([PlayerInput::default(); 2]);
    }
    let shelf = shelf.snapshot();
    assert!(shelf.players.iter().all(|player| {
        player.grounded
            && player.alive
            && (400_000..410_000).contains(&player.x_milli.abs())
            && (-65_000..-55_000).contains(&player.y_milli)
    }));

    let replay = run_profile_snapshots(
        ReplayProfile::LimeModularArenaReplay,
        59,
        LIME_MODULAR_REPLAY_TICKS,
    );
    let first = &replay[0];
    assert!(first.players[0].x_milli < -390_000);
    assert!(first.players[1].x_milli > 390_000);
    assert!(first.players.iter().all(|player| player.alive));
    let settled = &replay[179];
    assert!(settled.players.iter().all(|player| {
        player.grounded
            && player.alive
            && (300_000..325_000).contains(&player.x_milli.abs())
            && (45_000..65_000).contains(&player.y_milli)
    }));
    let terminal = replay.last().unwrap();
    assert_eq!(arena_digest(first), arena_digest(terminal));
    assert_eq!(terminal.metrics.jumps, 4);
    assert_eq!(terminal.metrics.ring_outs, 0);
    assert!(terminal.players.iter().all(|player| {
        player.grounded
            && player.alive
            && (300_000..325_000).contains(&player.x_milli.abs())
            && (45_000..65_000).contains(&player.y_milli)
    }));

    let nominal = terminal
        .players
        .iter()
        .map(|player| [player.x_milli, player.y_milli])
        .collect::<Vec<_>>();
    for suppress_jump in [false, true] {
        let mut simulation =
            AuthoritativeMatch::new_with_profile(59, ReplayProfile::LimeModularArenaReplay);
        let mut scripts = scripted_inputs_for(
            ReplayProfile::LimeModularArenaReplay,
            59,
            LIME_MODULAR_REPLAY_TICKS,
        );
        for input in scripts.iter_mut().flatten() {
            if suppress_jump {
                input.jump = false;
            } else {
                input.move_axis = 0;
            }
        }
        for (&orange, &blue) in scripts[0].iter().zip(&scripts[1]) {
            simulation.step([orange, blue]);
        }
        let perturbed = simulation.snapshot();
        assert!(perturbed.players.iter().enumerate().any(|(index, player)| {
            (player.x_milli - nominal[index][0]).abs() > 50_000
                || (player.y_milli - nominal[index][1]).abs() > 50_000
        }));
    }
}

#[test]
fn complete_teal_duel_exercises_combat_and_selects_one_winner() {
    let first = run_profile_match(ReplayProfile::TealDuelReplay, 38, TEAL_REPLAY_TICKS);
    let second = run_profile_match(ReplayProfile::TealDuelReplay, 38, TEAL_REPLAY_TICKS);
    assert_eq!(first, second);
    let before_impact =
        run_profile_match(ReplayProfile::TealDuelReplay, 38, TEAL_REPLAY_TICKS - 1).0;
    let metrics = &first.0.metrics;
    assert_eq!(metrics.shots_fired, 5);
    assert_eq!(metrics.recoil_impulses, 5);
    assert!(metrics.block_activations >= 1);
    assert_eq!(metrics.reflections, 1);
    assert_eq!(metrics.hits, 1);
    assert_eq!(metrics.health_scaled_knockbacks, 1);
    assert_eq!(metrics.bullet_ccd_contacts, 1);
    assert_eq!(first.0.metrics.ring_outs, 0);
    assert!(
        first
            .0
            .players
            .iter()
            .all(|player| player.x_milli.abs() < 760_000)
    );
    assert_eq!(before_impact.winner, None);
    assert_eq!(first.0.winner, Some(0));
    assert!(first.0.players[1].hit_flash_ticks > 0);
}

#[test]
fn ring_out_remains_a_separate_authoritative_capability() {
    let mut simulation = AuthoritativeMatch::new_with_profile(38, ReplayProfile::TealDuelReplay);
    let body = simulation.physics.players[1].body;
    simulation.physics.rapier.bodies[body].set_translation(Vector::new(KILL_X + 10.0, 0.0), true);
    simulation.step([PlayerInput::default(); 2]);
    let snapshot = simulation.snapshot();
    assert_eq!(snapshot.metrics.ring_outs, 1);
    assert_eq!(snapshot.winner, Some(0));
}

#[test]
fn rapier_ccd_stops_a_one_tick_thin_platform_crossing() {
    let mut physics = RapierWorld::new();
    physics.gravity = Vector::ZERO;
    physics.integration_parameters.dt = 1.0 / 60.0;
    physics.integration_parameters.max_ccd_substeps = 4;
    let _ = physics.insert(
        RigidBodyBuilder::fixed().translation(Vector::new(0.0, 0.0)),
        ColliderBuilder::cuboid(2.0, 100.0),
    );
    let (bullet, _) = physics.insert(
        RigidBodyBuilder::dynamic()
            .translation(Vector::new(-100.0, 0.0))
            .linvel(Vector::new(12_000.0, 0.0))
            .gravity_scale(0.0)
            .ccd_enabled(true),
        ColliderBuilder::ball(BULLET_RADIUS),
    );
    physics.step();
    assert!(physics.bodies[bullet].translation().x < 10.0);
}

#[test]
fn snapshot_json_stays_bounded() {
    let (snapshot, _) = run_scripted_match(38, REPLAY_TICKS);
    let json = serde_json::to_vec(&snapshot).unwrap();
    assert!(json.len() < 32_000, "{} bytes", json.len());
}

#[test]
fn radial_replay_preserves_saw_motion_and_adjacent_result_boundary() {
    let replay = run_profile_snapshots(
        ReplayProfile::RadialSawHalfBlueReplay,
        42,
        RADIAL_REPLAY_TICKS,
    );
    assert_eq!(replay.len(), RADIAL_REPLAY_TICKS as usize);
    let reveal = &replay[0];
    let moving = &replay[179];
    let shot = &replay[359];
    let ordinary_impact = &replay[539];
    let late_traversal = &replay[839];
    let last_combat = &replay[(RADIAL_LAST_COMBAT_TICK - 1) as usize];
    let result = &replay[(RADIAL_RESULT_ONSET_TICK - 1) as usize];
    let half_blue = replay.last().unwrap();

    assert_eq!(
        reveal.saws.iter().map(|saw| saw.id).collect::<Vec<_>>(),
        [200, 201]
    );
    assert!(
        reveal
            .saws
            .iter()
            .all(|saw| { saw.teeth == 8 && saw.angular_velocity_milliradians_per_second == 7_430 })
    );
    assert_ne!(saw_digest(reveal), saw_digest(moving));
    assert!(moving.players.iter().all(|player| {
        player.x_milli.abs() < 420_000 && (-250_000..260_000).contains(&player.y_milli)
    }));
    assert_eq!(shot.projectiles.len(), 1);
    assert!(shot.projectiles[0].y_milli > shot.players[0].y_milli + 40_000);
    assert!(
        ordinary_impact
            .impacts
            .iter()
            .any(|impact| { impact.tick == 531 && impact.owner == 1 && impact.target == Some(0) })
    );
    let late_orange = late_traversal
        .players
        .iter()
        .find(|player| player.id == 0)
        .unwrap();
    let late_blue = late_traversal
        .players
        .iter()
        .find(|player| player.id == 1)
        .unwrap();
    assert!(late_orange.x_milli < -250_000 && late_orange.y_milli > -40_000);
    assert!(late_blue.x_milli > 200_000 && late_blue.y_milli < -60_000);
    assert!(last_combat.impacts.iter().any(|impact| {
        impact.tick == RADIAL_LAST_COMBAT_TICK
            && impact.owner == 1
            && impact.target == Some(0)
            && impact.x_milli < -300_000
    }));
    assert_eq!(
        last_combat.round.as_ref().unwrap().phase,
        RoundPhase::Combat
    );
    assert_eq!(last_combat.winner, None);
    assert_eq!(last_combat.metrics.hits, 1);
    assert_eq!(
        result.round.as_ref().unwrap().phase,
        RoundPhase::ResultTransition
    );
    assert_eq!(result.winner, Some(1));
    assert_eq!(result.round.as_ref().unwrap().scores, [1, 1]);
    assert_eq!(result.metrics.hits, 2);
    assert_eq!(
        half_blue.round.as_ref().unwrap().phase,
        RoundPhase::HalfBlue
    );
    assert_eq!(half_blue.round.as_ref().unwrap().winner, Some(1));
    assert_eq!(half_blue.metrics.explosive_projectile_impacts, 0);
    assert!(half_blue.explosions.is_empty());

    let repeat = run_profile_match(
        ReplayProfile::RadialSawHalfBlueReplay,
        42,
        RADIAL_REPLAY_TICKS,
    );
    assert_eq!(hash_snapshot(half_blue), repeat.1);
}

#[test]
fn radial_saw_reset_uses_ecs_initial_state_and_speed_changes_motion_digest() {
    let mut nominal =
        AuthoritativeMatch::new_with_profile(42, ReplayProfile::RadialSawHalfBlueReplay);
    let initial = nominal.snapshot();
    for _ in 0..90 {
        nominal.step([PlayerInput::default(); 2]);
    }
    assert_ne!(saw_digest(&initial), saw_digest(&nominal.snapshot()));
    nominal.reset_radial_arena().unwrap();
    assert_eq!(initial.saws, nominal.snapshot().saws);
    assert_eq!(nominal.snapshot().round.unwrap().phase, RoundPhase::Combat);

    let mut perturbed =
        AuthoritativeMatch::new_with_profile(42, ReplayProfile::RadialSawHalfBlueReplay);
    let upper = perturbed.physics.saws.get(&200).unwrap().body;
    perturbed.physics.rapier.bodies[upper].set_angvel(6.9, true);
    for _ in 0..90 {
        perturbed.step([PlayerInput::default(); 2]);
    }
    assert_ne!(
        saw_digest(&nominal.snapshot()),
        saw_digest(&perturbed.snapshot())
    );
}

#[test]
fn timber_replay_is_repeatable_and_collapses_from_real_constraints() {
    let first = run_scripted_snapshots(40, REPLAY_TICKS);
    let second = run_scripted_snapshots(40, REPLAY_TICKS);
    assert_eq!(first, second);
    let before = &first[(TIMBER_IMPACT_TICK - 2) as usize];
    let impact = &first[(TIMBER_IMPACT_TICK - 1) as usize];
    let settled = first.last().unwrap();
    assert_eq!(
        before.dynamic_bodies.len(),
        19,
        "ids={:?}",
        before
            .dynamic_bodies
            .iter()
            .map(|body| body.id)
            .collect::<Vec<_>>()
    );
    assert!(
        before
            .constraints
            .iter()
            .all(|constraint| constraint.active)
    );
    assert_eq!(impact.explosions.len(), 1);
    assert_eq!(impact.metrics.released_constraints, 17);
    assert!(impact.metrics.explosion_impulsed_bodies >= 15);
    assert!(
        impact
            .constraints
            .iter()
            .filter(|constraint| constraint.kind == ConstraintKind::Fixed)
            .all(|constraint| !constraint.active)
    );
    assert!(
        impact
            .constraints
            .iter()
            .filter(|constraint| constraint.kind == ConstraintKind::Rope)
            .all(|constraint| constraint.active)
    );
    assert!(settled.metrics.dynamic_body_contacts > 100);
    assert!(settled.metrics.fighter_body_contact_ticks > 0);
    assert!(settled.metrics.bullet_ccd_contacts > 0);
    assert_eq!(
        settled.metrics.ring_outs, 0,
        "players={:?}",
        settled.players
    );
    assert_eq!(settled.winner, None);
    let impact_motion: i64 = first[(TIMBER_IMPACT_TICK + 59) as usize]
        .dynamic_bodies
        .iter()
        .map(|body| {
            i64::from(body.velocity_x_milli_per_second.abs())
                + i64::from(body.velocity_y_milli_per_second.abs())
        })
        .sum();
    let settled_motion: i64 = settled
        .dynamic_bodies
        .iter()
        .map(|body| {
            i64::from(body.velocity_x_milli_per_second.abs())
                + i64::from(body.velocity_y_milli_per_second.abs())
        })
        .sum();
    assert!(
        settled_motion < impact_motion,
        "{settled_motion} >= {impact_motion}"
    );
    assert!(
        settled
            .dynamic_bodies
            .iter()
            .all(|body| { body.x_milli.abs() < 900_000 && body.y_milli > -360_000 }),
        "bodies={:?}",
        settled
            .dynamic_bodies
            .iter()
            .map(|body| (body.id, body.x_milli, body.y_milli))
            .collect::<Vec<_>>()
    );
}

#[test]
fn explosion_boundary_releases_fixed_joints_and_changes_rapier_motion() {
    let mut simulation =
        AuthoritativeMatch::new_with_profile(40, ReplayProfile::TimberCollapseReplay);
    assert_eq!(simulation.physics.dynamic_bodies.len(), 19);
    assert_eq!(simulation.physics.constraints.len(), 19);
    assert_eq!(simulation.physics.rapier.impulse_joints.len(), 19);
    for _ in 0..TIMBER_IMPACT_TICK {
        simulation.step([PlayerInput::default(); 2]);
    }
    assert_eq!(simulation.physics.rapier.impulse_joints.len(), 2);
    let snapshot = simulation.snapshot();
    assert!(snapshot.dynamic_bodies.iter().any(|body| {
        body.velocity_x_milli_per_second != 0 || body.velocity_y_milli_per_second != 0
    }));
}

#[test]
fn explosion_impulse_perturbation_changes_dynamic_body_poses() {
    let mut nominal = AuthoritativeMatch::new_with_profile(40, ReplayProfile::TimberCollapseReplay);
    let mut perturbed =
        AuthoritativeMatch::new_with_profile(40, ReplayProfile::TimberCollapseReplay);
    perturbed.explosion_strength *= 0.92;
    let scripts = scripted_inputs_for(ReplayProfile::TimberCollapseReplay, 40, REPLAY_TICKS);
    for (nominal_input, perturbed_input) in
        scripts[0].iter().copied().zip(scripts[1].iter().copied())
    {
        nominal.step([nominal_input, perturbed_input]);
        perturbed.step([nominal_input, perturbed_input]);
    }
    let nominal = nominal.snapshot();
    let perturbed = perturbed.snapshot();
    assert_ne!(
        dynamic_body_digest(&nominal),
        dynamic_body_digest(&perturbed)
    );
    assert!(
        nominal
            .dynamic_bodies
            .iter()
            .zip(&perturbed.dynamic_bodies)
            .any(|(left, right)| {
                left.x_milli != right.x_milli
                    || left.y_milli != right.y_milli
                    || left.rotation_milliradians != right.rotation_milliradians
            })
    );
}

#[test]
fn connected_session_keeps_drafted_cards_through_both_contact_driven_halves() {
    let snapshots = run_profile_snapshots(
        ReplayProfile::RematchDraftReplay,
        SOURCE_DRAFT_SEED,
        REMATCH_DRAFT_TICKS,
    );
    let mut phases = Vec::new();
    for snapshot in &snapshots {
        let flow = snapshot.flow.as_ref().unwrap();
        if phases.last().is_none_or(|(_, phase)| *phase != flow.phase) {
            phases.push((snapshot.tick, flow.phase));
        }
        if snapshot.tick >= 2_101 {
            assert_eq!(
                flow.loadouts,
                [vec![ItemId::Dazzle], vec![ItemId::ExplosiveBullet]]
            );
        }
    }
    assert_eq!(
        phases
            .iter()
            .filter(|(_, phase)| matches!(phase, FlowPhase::EliminationConclusion))
            .count(),
        2
    );
    assert!(phases.contains(&(
        CONNECTED_BLUE_RESULT_ONSET_TICK,
        FlowPhase::BlueResultTransition
    )));
    assert!(phases.contains(&(
        CONNECTED_ORANGE_RESULT_ONSET_TICK,
        FlowPhase::OrangeResultTransition
    )));
    let at = |tick: u32| &snapshots[(tick - 1) as usize];
    assert_eq!(
        at(CONNECTED_HALF_BLUE_TICK).flow.as_ref().unwrap().phase,
        FlowPhase::HalfBlue
    );
    assert_eq!(
        at(CONNECTED_TIMBER_COMBAT_TICK)
            .flow
            .as_ref()
            .unwrap()
            .phase,
        FlowPhase::TimberCombat
    );
    assert_eq!(
        at(CONNECTED_HALF_ORANGE_TICK).flow.as_ref().unwrap().phase,
        FlowPhase::HalfOrange
    );
    let handoff = at(CONNECTED_HALF_BLUE_TAIL_TICK);
    assert_eq!(handoff.round.as_ref().unwrap().winner, Some(1));
    assert_eq!(handoff.flow.as_ref().unwrap().fighter_alive, [true, true]);
    assert!(
        handoff
            .players
            .iter()
            .all(|player| player.alive && player.health == 100)
    );
    let loaded = at(CONNECTED_TIMBER_COMBAT_TICK);
    assert_eq!(loaded.dynamic_bodies.len(), 19);
    assert!(loaded.constraints.iter().all(|joint| joint.active));
    assert!(
        loaded
            .players
            .iter()
            .all(|player| player.alive && player.health == 100)
    );
    let impact = snapshots
        .iter()
        .find(|state| state.metrics.released_constraints > 0)
        .unwrap();
    assert_eq!(impact.tick, CONNECTED_TIMBER_IMPACT_TARGET_TICK);
    let blast = impact.explosions.last().unwrap();
    assert!((-200_000..-100_000).contains(&blast.x_milli));
    assert!((100_000..200_000).contains(&blast.y_milli));
    assert!(at(2_280).explosions.iter().any(|blast| blast.tick == 2_276));
    assert_eq!(impact.metrics.released_constraints, 17);
    assert!(
        impact
            .impacts
            .iter()
            .any(|hit| hit.tick == impact.tick && hit.owner == 1)
    );
    assert!(
        impact
            .explosions
            .iter()
            .any(|blast| blast.tick == impact.tick)
    );
    assert!(
        snapshots
            .iter()
            .any(|state| state.metrics.dazzle_stun_pulses > 0)
    );
    let last = snapshots.last().unwrap();
    assert_eq!(last.flow.as_ref().unwrap().halves, [1, 1]);
    assert_eq!(last.round.as_ref().unwrap().scores, [1, 1]);
    assert_eq!(last.winner, Some(0));
    assert!(
        last.impacts
            .iter()
            .any(|hit| hit.owner == 1 && hit.eliminated)
    );
    assert!(
        last.impacts
            .iter()
            .any(|hit| hit.owner == 0 && hit.eliminated)
    );
    assert_eq!(
        last.players
            .iter()
            .map(|player| player.alive)
            .collect::<Vec<_>>(),
        vec![true, false]
    );
}
