use super::*;

fn arena() -> ArenaDefinition {
    let mut arena =
        ArenaDefinition::load(&default_arena_directory().join("all-kinds.ron")).unwrap();
    arena.objects.clear();
    arena.chains.clear();
    arena.surfaces.clear();
    arena.spawns = vec![[-100.0, 0.0], [100.0, 0.0]];
    arena.frame = [-640.0, -360.0, 640.0, 360.0];
    arena
}
fn game(arena: ArenaDefinition, tuning: CombatTuning) -> AuthoritativeMatch {
    let mut cards = load_card_directory(&default_card_directory()).unwrap();
    for card in &mut cards {
        card.modifiers = Default::default();
    }
    let mut game = AuthoritativeMatch::with_content(
        MatchConfig::default(),
        MatchContent {
            tuning,
            cards,
            arenas: vec![arena],
        },
    )
    .unwrap();
    let flow = game.snapshot().flow.unwrap();
    let inputs = flow
        .offers
        .iter()
        .map(|offer| PlayerInput {
            flow: Some(FlowCommand {
                phase_revision: flow.phase_revision,
                action: FlowAction::Confirm(offer[0]),
            }),
            ..Default::default()
        })
        .collect::<Vec<_>>();
    game.step(&inputs);
    game
}
fn idle(game: &mut AuthoritativeMatch, ticks: u16) {
    for _ in 0..ticks {
        game.step(&[PlayerInput::default(); 2]);
    }
}
fn save_evidence(game: &mut AuthoritativeMatch, name: &str) {
    if let Some(directory) = std::env::var_os("QUARREL_COMBAT_EVIDENCE_DIR") {
        let path = std::path::PathBuf::from(directory);
        std::fs::create_dir_all(&path).unwrap();
        std::fs::write(
            path.join(format!("{name}.json")),
            serde_json::to_vec_pretty(&game.snapshot()).unwrap(),
        )
        .unwrap();
    }
}

#[test]
fn each_edge_damages_and_pushes_inward_while_block_launches_without_damage() {
    for (spawn, axis, sign) in [
        ([-639.9, 0.0], 0, 1),
        ([639.9, 0.0], 0, -1),
        ([0.0, -359.0], 1, 1),
        ([0.0, 359.0], 1, -1),
    ] {
        for block in [false, true] {
            let mut a = arena();
            a.spawns[0] = spawn;
            let mut game = game(a, CombatTuning::default());
            for _ in 0..6 {
                game.step(&[
                    PlayerInput {
                        block,
                        move_axis: if axis == 0 { -sign as i8 } else { 0 },
                        jump: axis == 1 && sign == -1,
                        ..Default::default()
                    },
                    PlayerInput::default(),
                ]);
                if game.snapshot().metrics.ring_outs > 0 {
                    break;
                }
            }
            let state = game.snapshot();
            let actor = &state.players[0];
            assert_eq!(actor.health, if block { 100 } else { 40 });
            assert!(actor.alive);
            let velocity = if axis == 0 {
                actor.velocity_x_milli_per_second
            } else {
                actor.velocity_y_milli_per_second
            };
            assert_eq!(velocity, sign * if block { 1_100_000 } else { 680_000 });
            assert!(actor.x_milli.abs() <= 640_000 && actor.y_milli.abs() <= 360_000);
            if block && axis == 1 && sign == 1 {
                save_evidence(&mut game, "edge-contact");
                idle(&mut game, 12);
                save_evidence(&mut game, "edge-launch");
            }
        }
    }
}

#[test]
fn three_shots_then_reload_and_new_magazine() {
    let tuning = CombatTuning {
        gravity: 1.0,
        ..Default::default()
    };
    let mut game = game(arena(), tuning.clone());
    let input = PlayerInput {
        fire: true,
        aim_y: 1000,
        ..Default::default()
    };
    for _ in 0..=tuning.fire_cooldown_ticks * 2 {
        game.step(&[input, PlayerInput::default()]);
    }
    assert_eq!(game.snapshot().metrics.shots_fired, 3);
    assert_eq!(game.snapshot().players[0].ammunition, 0);
    for _ in 0..tuning.reload_ticks - 1 {
        game.step(&[input, PlayerInput::default()]);
    }
    assert_eq!(game.snapshot().metrics.shots_fired, 3);
    game.step(&[input, PlayerInput::default()]);
    assert_eq!(game.snapshot().metrics.shots_fired, 4);
}

#[test]
fn base_hit_takes_sixty_health_and_pushes_target_and_second_hit_kills() {
    let mut game = game(arena(), CombatTuning::default());
    let fire = PlayerInput {
        fire: true,
        aim_x: 1000,
        ..Default::default()
    };
    game.step(&[fire, PlayerInput::default()]);
    idle(&mut game, 3);
    let state = game.snapshot();
    assert_eq!(state.players[1].health, 40);
    assert!(state.players[1].alive && state.players[1].velocity_x_milli_per_second > 0);
    for _ in 0..150 {
        let state = game.snapshot();
        let shot = PlayerInput {
            fire: true,
            aim_at_opponent: true,
            ..Default::default()
        }
        .with_progressive_observation(0, Some(&state));
        game.step(&[shot, PlayerInput::default()]);
        if !game.snapshot().players[1].alive {
            break;
        }
    }
    assert!(!game.snapshot().players[1].alive);
}

#[test]
fn block_reflects_extends_and_has_a_press_and_cooldown_gate() {
    let mut game = game(
        arena(),
        CombatTuning {
            gravity: 1.0,
            ..Default::default()
        },
    );
    game.step(&[
        PlayerInput {
            fire: true,
            aim_x: 1000,
            ..Default::default()
        },
        PlayerInput {
            block: true,
            ..Default::default()
        },
    ]);
    let original = game.snapshot().players[1].block_ticks;
    for _ in 0..5 {
        game.step(&[
            PlayerInput::default(),
            PlayerInput {
                block: true,
                ..Default::default()
            },
        ]);
        if game.snapshot().metrics.reflections > 0 {
            break;
        }
    }
    let state = game.snapshot();
    assert_eq!(state.metrics.reflections, 1);
    assert_eq!(state.players[1].health, 100);
    assert!(state.players[1].block_ticks > original);
    assert_eq!(state.projectiles[0].owner, 1);
    assert!(state.projectiles[0].velocity_x_milli_per_second < 0);
    game.step(&[PlayerInput::default(); 2]);
    let returning = game.snapshot();
    assert!(
        returning.projectiles[0].velocity_x_milli_per_second < 0,
        "reflected shot must not rebound off its new owner next tick"
    );
    assert!(returning.projectiles[0].x_milli < state.projectiles[0].x_milli);
    save_evidence(&mut game, "reflection");
    for _ in 0..200 {
        game.step(&[
            PlayerInput::default(),
            PlayerInput {
                block: true,
                ..Default::default()
            },
        ]);
    }
    assert_eq!(
        game.snapshot().metrics.block_activations,
        1,
        "held block must not reactivate"
    );
    game.step(&[PlayerInput::default(); 2]);
    game.step(&[
        PlayerInput::default(),
        PlayerInput {
            block: true,
            ..Default::default()
        },
    ]);
    assert_eq!(game.snapshot().metrics.block_activations, 2);
}

#[test]
fn bullets_fall_and_downward_recoil_is_capped_and_switchable() {
    let mut velocities = Vec::new();
    for enabled in [false, true] {
        let mut game = game(
            arena(),
            CombatTuning {
                recoil_enabled: enabled,
                ..Default::default()
            },
        );
        let before = game.snapshot().players[0].velocity_y_milli_per_second;
        game.step(&[
            PlayerInput {
                fire: true,
                aim_y: -1000,
                ..Default::default()
            },
            PlayerInput::default(),
        ]);
        let after = game.snapshot();
        let gain = after.players[0].velocity_y_milli_per_second - before;
        velocities.push(after.players[0].velocity_y_milli_per_second);
        if enabled {
            assert!(
                (90_000..=120_000).contains(&gain),
                "default downward recoil gives a visible capped boost: {gain}"
            );
        } else {
            assert!(gain < 0);
        }
        assert!(after.projectiles[0].velocity_y_milli_per_second < -3_600_000);
    }
    let recoil_contribution = velocities[1] - velocities[0];
    assert!(
        (110_000..=120_000).contains(&recoil_contribution),
        "default impulse must exercise the cap: {recoil_contribution}"
    );
}

#[test]
fn stored_jump_works_in_air_once_and_floor_restores_it_without_fall_damage() {
    let mut a = arena();
    a.objects = vec![ArenaObject {
        id: 0,
        position: [0.0, -200.0],
        rotation: 0.0,
        shape: ArenaShape::Rectangle {
            size: [800.0, 40.0],
        },
        kind: ArenaKind::Solid,
        color: [100, 100, 100],
        mass: 1.0,
        health: None,
        motion: None,
    }];
    let mut game = game(a, CombatTuning::default());
    let jump = PlayerInput {
        jump: true,
        ..Default::default()
    };
    game.step(&[jump, PlayerInput::default()]);
    assert!(!game.snapshot().players[0].jump_available);
    idle(&mut game, 1);
    game.step(&[jump, PlayerInput::default()]);
    assert_eq!(game.snapshot().metrics.jumps, 1, "no double jump");
    idle(&mut game, 150);
    let actor = game.snapshot().players[0].clone();
    assert!(actor.grounded && actor.jump_available);
    assert_eq!(actor.health, 100, "landing causes no fall damage");
    game.step(&[jump, PlayerInput::default()]);
    assert_eq!(game.snapshot().metrics.jumps, 2);
    assert!(
        !game.snapshot().players[0].jump_available,
        "floor contact after launching must not refund the spent jump"
    );
    idle(&mut game, 1);
    game.step(&[jump, PlayerInput::default()]);
    assert_eq!(game.snapshot().metrics.jumps, 2);
}

#[test]
fn wall_restores_jump_clings_and_jump_climbs() {
    let mut a = arena();
    a.spawns[0] = [-123.0, 0.0];
    a.objects = vec![ArenaObject {
        id: 0,
        position: [-160.0, 0.0],
        rotation: 0.0,
        shape: ArenaShape::Rectangle {
            size: [30.0, 600.0],
        },
        kind: ArenaKind::Solid,
        color: [100, 100, 100],
        mass: 1.0,
        health: None,
        motion: None,
    }];
    let mut game = game(a, CombatTuning::default());
    let towards = PlayerInput {
        move_axis: -1,
        ..Default::default()
    };
    for _ in 0..20 {
        game.step(&[towards, PlayerInput::default()]);
    }
    let state = game.snapshot();
    assert!(!state.players[0].grounded, "wall is not floor");
    assert!(state.players[0].jump_available);
    assert!(state.players[0].velocity_y_milli_per_second > -110_000);
    let before = state.players[0].y_milli;
    game.step(&[
        PlayerInput {
            jump: true,
            ..towards
        },
        PlayerInput::default(),
    ]);
    for _ in 0..8 {
        game.step(&[towards, PlayerInput::default()]);
    }
    assert!(game.snapshot().players[0].y_milli > before);
}

#[test]
fn crouch_halves_collision_height_and_accelerates_air_fall() {
    let mut a = arena();
    a.spawns[0] = [-100.0, -158.0];
    a.objects = vec![ArenaObject {
        id: 0,
        position: [0.0, -200.0],
        rotation: 0.0,
        shape: ArenaShape::Rectangle {
            size: [800.0, 40.0],
        },
        kind: ArenaKind::Solid,
        color: [100, 100, 100],
        mass: 1.0,
        health: None,
        motion: None,
    }];
    let mut game = game(a, CombatTuning::default());
    idle(&mut game, 30);
    let standing = game.snapshot().players[0].height_milli;
    game.step(&[
        PlayerInput {
            crouch: true,
            ..Default::default()
        },
        PlayerInput::default(),
    ]);
    assert_eq!(game.snapshot().players[0].height_milli, standing / 2);
    let actor = &game.snapshot().players[0];
    assert!(
        (actor.y_milli - actor.height_milli / 2 + 180_000).abs() < 1_000,
        "crouching feet must stay at floor top; center={}, height={}",
        actor.y_milli,
        actor.height_milli
    );
    assert!(
        game.physics.rapier.colliders[game.physics.players[0].collider]
            .compute_aabb()
            .half_extents()
            .y
            < 12.0
    );
    for _ in 0..30 {
        game.step(&[
            PlayerInput {
                crouch: true,
                ..Default::default()
            },
            PlayerInput::default(),
        ]);
        assert_eq!(game.snapshot().players[0].height_milli, standing / 2);
        let actor = &game.snapshot().players[0];
        assert!(
            (actor.y_milli - actor.height_milli / 2 + 180_000).abs() < 1_000,
            "crouching feet must stay at floor top; center={}, height={}",
            actor.y_milli,
            actor.height_milli
        );
    }
    save_evidence(&mut game, "crouch");
    idle(&mut game, 1);
    let actor = &game.snapshot().players[0];
    assert_eq!(actor.height_milli, standing);
    assert!(
        (actor.y_milli - actor.height_milli / 2 + 180_000).abs() < 1_000,
        "standing back up must keep feet on floor"
    );
    let mut normal = game_for_air(false);
    let mut crouched = game_for_air(true);
    assert!(
        crouched.snapshot().players[0].velocity_y_milli_per_second
            < normal.snapshot().players[0].velocity_y_milli_per_second
    );
}
fn game_for_air(crouch: bool) -> AuthoritativeMatch {
    let mut game = game(arena(), CombatTuning::default());
    for _ in 0..10 {
        game.step(&[
            PlayerInput {
                crouch,
                ..Default::default()
            },
            PlayerInput::default(),
        ]);
    }
    game
}

#[test]
fn live_tuning_reload_changes_real_shot_damage_and_rejects_invalid_edits() {
    let directory = std::env::temp_dir().join(format!("quarrel-tuning-{}", std::process::id()));
    std::fs::create_dir_all(directory.join("assets")).unwrap();
    let path = directory.join("assets/tuning.ron");
    let mut tuning = CombatTuning::default();
    std::fs::write(&path, ron::to_string(&tuning).unwrap()).unwrap();
    let mut game = game(arena(), tuning.clone());
    game.watch_tuning_file(&path).unwrap();
    tuning.damage_per_hit = 17;
    tuning.recoil_enabled = false;
    std::fs::write(&path, ron::to_string(&tuning).unwrap()).unwrap();
    idle(&mut game, 15);
    assert_eq!(game.tuning().damage_per_hit, 17);
    for _ in 0..6 {
        game.step(&[
            PlayerInput {
                fire: true,
                aim_x: 1000,
                ..Default::default()
            },
            PlayerInput::default(),
        ]);
    }
    assert_eq!(game.snapshot().players[1].health, 83);
    assert_eq!(game.snapshot().metrics.recoil_impulses, 0);
    std::fs::write(&path, "(").unwrap();
    idle(&mut game, 15);
    assert!(game.tuning_reload_error().is_some());
    assert_eq!(game.tuning().damage_per_hit, 17);
    std::fs::write(&path, ron::to_string(&tuning).unwrap()).unwrap();
    idle(&mut game, 15);
    assert!(game.tuning_reload_error().is_none());
    std::fs::remove_file(path).unwrap();
    std::fs::remove_dir(directory.join("assets")).unwrap();
    std::fs::remove_dir(directory).unwrap();
}

#[test]
fn crouching_ducks_a_shot_above_the_reduced_collider() {
    for crouch in [false, true] {
        let mut a = arena();
        a.spawns = vec![[-100.0, 24.0], [100.0, 0.0]];
        a.objects = vec![ArenaObject {
            id: 0,
            position: [0.0, -42.0],
            rotation: 0.0,
            shape: ArenaShape::Rectangle {
                size: [800.0, 40.0],
            },
            kind: ArenaKind::Solid,
            color: [100, 100, 100],
            mass: 1.0,
            health: None,
            motion: None,
        }];
        let mut game = game(a, CombatTuning::default());
        for tick in 0..6 {
            game.step(&[
                PlayerInput {
                    fire: tick == 0,
                    aim_x: 1000,
                    ..Default::default()
                },
                PlayerInput {
                    crouch,
                    ..Default::default()
                },
            ]);
        }
        assert_eq!(
            game.snapshot().players[1].health,
            if crouch { 100 } else { 40 }
        );
    }
}

#[test]
fn block_does_not_stop_repeated_damage_ticks() {
    let mut game = game(arena(), CombatTuning::default());
    game.step(&[
        PlayerInput {
            block: true,
            ..Default::default()
        },
        PlayerInput::default(),
    ]);
    for _ in 0..5 {
        assert!(!game.damage_fighter(0, 3).unwrap());
        game.step(&[PlayerInput::default(); 2]);
        assert!(game.snapshot().players[0].block_ticks > 0);
    }
    assert_eq!(game.snapshot().players[0].health, 85);
    assert!(game.damage_fighter(0, 100).unwrap());
    assert!(!game.snapshot().players[0].alive);
    assert!(game.damage_fighter(200, 3).is_err());
}

#[test]
fn a_new_press_does_not_shorten_a_reflection_extended_block() {
    let mut game = game(
        arena(),
        CombatTuning {
            gravity: 1.0,
            block_extension_ticks: 240,
            ..Default::default()
        },
    );
    game.step(&[
        PlayerInput {
            fire: true,
            aim_x: 1000,
            ..Default::default()
        },
        PlayerInput {
            block: true,
            ..Default::default()
        },
    ]);
    while game.snapshot().players[1].block_cooldown_ticks > 0 {
        idle(&mut game, 1);
    }
    assert_eq!(game.snapshot().metrics.reflections, 1);
    let remaining = game.snapshot().players[1].block_ticks;
    assert!(remaining > game.tuning().block_duration_ticks);
    game.step(&[
        PlayerInput::default(),
        PlayerInput {
            block: true,
            ..Default::default()
        },
    ]);
    assert_eq!(game.snapshot().players[1].block_ticks, remaining - 1);
}

#[test]
fn arcing_shots_can_leave_the_top_of_the_frame_and_return() {
    let mut a = ArenaDefinition::load(&default_arena_directory().join("all-kinds.ron")).unwrap();
    a.objects.retain(|object| object.id == 0);
    a.chains.clear();
    a.surfaces.clear();
    a.spawns = vec![[-100.0, -258.0], [100.0, -258.0]];
    a.frame[3] = 100.0;
    let mut game = game(
        a,
        CombatTuning {
            bullet_speed: 1200.0,
            ..Default::default()
        },
    );
    game.step(&[
        PlayerInput {
            fire: true,
            aim_y: 1000,
            ..Default::default()
        },
        PlayerInput::default(),
    ]);
    idle(&mut game, 29);
    assert_eq!(game.snapshot().projectiles.len(), 1);
    assert!(game.snapshot().projectiles[0].y_milli > 100_000);
    idle(&mut game, 60);
    let shot = &game.snapshot().projectiles[0];
    assert!(shot.y_milli < 100_000 && shot.velocity_y_milli_per_second < 0);
}

#[test]
fn old_snapshots_without_size_fields_keep_visible_standing_fighters() {
    let original = game(arena(), CombatTuning::default()).snapshot();
    let mut legacy = serde_json::to_value(&original).unwrap();
    for actor in legacy["players"].as_array_mut().unwrap() {
        actor.as_object_mut().unwrap().remove("radiusMilli");
        actor.as_object_mut().unwrap().remove("heightMilli");
    }
    let decoded: MatchSnapshot = serde_json::from_value(legacy).unwrap();
    for (before, after) in original.players.iter().zip(decoded.players.iter()) {
        assert_eq!(after.radius_milli, before.radius_milli);
        assert_eq!(after.height_milli, before.height_milli);
    }
}

#[test]
fn editing_damage_preserves_a_held_ground_crouch() {
    let mut a = ArenaDefinition::load(&default_arena_directory().join("all-kinds.ron")).unwrap();
    a.objects.retain(|object| object.id == 0);
    a.chains.clear();
    a.surfaces.clear();
    a.spawns = vec![[-100.0, -258.0], [100.0, -258.0]];
    let mut tuning = CombatTuning::default();
    let mut game = game(a, tuning.clone());
    idle(&mut game, 30);
    let path =
        std::env::temp_dir().join(format!("quarrel-79-crouch-edit-{}.ron", std::process::id()));
    std::fs::write(&path, ron::to_string(&tuning).unwrap()).unwrap();
    game.watch_tuning_file(&path).unwrap();
    let input = PlayerInput {
        crouch: true,
        ..Default::default()
    };
    for _ in 0..15 {
        game.step(&[input, PlayerInput::default()]);
    }
    tuning.damage_per_hit = 17;
    std::fs::write(&path, ron::to_string(&tuning).unwrap()).unwrap();
    let mut heights = Vec::new();
    for _ in 0..15 {
        game.step(&[input, PlayerInput::default()]);
        let actor = &game.snapshot().players[0];
        heights.push((actor.height_milli, actor.y_milli - actor.height_milli / 2));
    }
    std::fs::remove_file(path).unwrap();
    assert_eq!(game.tuning().damage_per_hit, 17);
    for (height, feet) in heights {
        assert_eq!(
            height, 22_000,
            "damage edit must preserve a held crouch on every tick"
        );
        assert!((feet + 280_000).abs() < 1_000);
    }
}
