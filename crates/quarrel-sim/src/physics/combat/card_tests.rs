use super::*;

fn arena(wall: bool) -> ArenaDefinition {
    let mut arena = ArenaDefinition::parse(r#"(
        name: "Card lab", frame: (-640.0, -360.0, 640.0, 360.0),
        spawns: [(-300.0, 0.0), (300.0, 0.0)],
        objects: [(id: 0, position: (0.0, -60.0), shape: Rectangle(size: (1200.0, 40.0)), kind: Solid, color: (90, 90, 90))],
    )"#).unwrap();
    if wall {
        arena.objects.push(ArenaObject {
            id: 1,
            position: [0.0, 20.0],
            rotation: 0.0,
            shape: ArenaShape::Rectangle {
                size: [30.0, 150.0],
            },
            kind: ArenaKind::Solid,
            color: [80, 90, 100],
            mass: 1.0,
            health: None,
            motion: None,
        });
    }
    arena
}
fn pool() -> Vec<ItemDefinition> {
    load_card_directory(&default_card_directory()).unwrap()
}
fn game(names: &[&str], wall: bool) -> AuthoritativeMatch {
    game_with_content(names, pool(), arena(wall))
}
fn game_with_content(
    names: &[&str],
    cards: Vec<ItemDefinition>,
    arena: ArenaDefinition,
) -> AuthoritativeMatch {
    let mut game = AuthoritativeMatch::with_content(
        MatchConfig {
            offer_size: cards.len(),
            ..Default::default()
        },
        MatchContent {
            tuning: CombatTuning::default(),
            cards,
            arenas: vec![arena],
        },
    )
    .unwrap();
    for name in names {
        let snap = game.snapshot().flow.unwrap();
        let id = snap.catalog.iter().find(|c| c.title == *name).unwrap().id;
        let other = snap
            .catalog
            .iter()
            .find(|c| c.title == "Rubber Round")
            .unwrap()
            .id;
        if snap.phase == FlowPhase::Combat {
            game.flow.record_survivors(&[false, true]);
            for _ in 0..30 {
                game.step(&[]);
            }
        }
        let snap = game.snapshot().flow.unwrap();
        game.step(&[
            PlayerInput {
                flow: Some(FlowCommand {
                    phase_revision: snap.phase_revision,
                    action: FlowAction::Confirm(id),
                }),
                ..Default::default()
            },
            PlayerInput {
                flow: snap.offers[1].contains(&other).then_some(FlowCommand {
                    phase_revision: snap.phase_revision,
                    action: FlowAction::Confirm(other),
                }),
                ..Default::default()
            },
        ]);
    }
    assert_eq!(game.snapshot().flow.unwrap().phase, FlowPhase::Combat);
    // Extra health allows individual effects to be observed without ending the lab fight.
    game.world
        .entity_mut(game.player_entities[1])
        .get_mut::<PlayerState>()
        .unwrap()
        .health = 10000;
    game
}
fn step(game: &mut AuthoritativeMatch, fire: bool, block: bool, aim_y: i16) {
    game.step(&[
        PlayerInput {
            fire,
            block,
            aim_x: 1000,
            aim_y,
            ..Default::default()
        },
        PlayerInput::default(),
    ]);
}
fn hit(game: &mut AuthoritativeMatch) {
    step(game, true, false, 0);
    for _ in 0..20 {
        if game.snapshot().metrics.hits > 0 {
            return;
        }
        step(game, false, false, 0);
    }
    assert!(game.snapshot().metrics.hits > 0);
}
#[test]
fn needlework_first_shot_is_faster_harder_and_slower_to_repeat() {
    let mut game = game(&["Needlework"], false);
    step(&mut game, true, false, 0);
    let snap = game.snapshot();
    assert!(snap.projectiles[0].velocity_x_milli_per_second > 8_000_000);
    assert!(snap.projectiles[0].damage >= 102);
    assert!(snap.players[0].fire_cooldown_ticks > game.tuning.fire_cooldown_ticks);
}
#[test]
fn hailstorm_first_shots_are_rapid_and_weak() {
    let mut game = game(&["Hailstorm"], false);
    for _ in 0..10 {
        step(&mut game, true, false, 300);
    }
    let snap = game.snapshot();
    assert!(snap.metrics.shots_fired >= 3);
    assert!(snap.projectiles.iter().all(|p| p.damage < 25));
}
#[test]
fn rubber_round_first_wall_contact_rebounds_and_survives() {
    let mut game = game(&["Rubber Round"], true);
    step(&mut game, true, false, 0);
    for _ in 0..6 {
        step(&mut game, false, false, 0);
    }
    let snap = game.snapshot();
    assert!(!snap.projectiles.is_empty());
    assert!(snap.projectiles[0].velocity_x_milli_per_second < 0);
}
#[test]
fn long_haul_first_shot_visibly_grows_and_gains_damage() {
    let mut game = game(&["Long Haul"], false);
    step(&mut game, true, false, 600);
    let before = game.snapshot().projectiles[0].clone();
    for _ in 0..5 {
        step(&mut game, false, false, 600);
    }
    let after = game.snapshot().projectiles[0].clone();
    assert!(after.radius_milli > before.radius_milli);
    assert!(after.damage > before.damage);
}
#[test]
fn shepherd_first_shot_turns_with_current_aim() {
    let mut game = game(&["Shepherd"], false);
    step(&mut game, true, false, 0);
    for _ in 0..4 {
        step(&mut game, false, false, 1000);
    }
    let shot = game.snapshot().projectiles[0].clone();
    assert!(shot.velocity_y_milli_per_second > 200000);
    assert!(shot.velocity_x_milli_per_second < 1_620_000);
}
#[test]
fn borer_first_shot_crosses_thin_terrain() {
    let mut game = game(&["Borer"], true);
    hit(&mut game);
    assert!(game.snapshot().players[1].health < 10000);
}
#[test]
fn firecracker_first_impact_explodes() {
    let mut game = game(&["Firecracker"], false);
    hit(&mut game);
    let snap = game.snapshot();
    assert_eq!(snap.metrics.explosive_projectile_impacts, 1);
    assert!(snap.impacts.len() >= 2);
}
#[test]
fn slow_burn_first_hit_keeps_dealing_damage() {
    let mut game = game(&["Slow Burn"], false);
    hit(&mut game);
    let health = game.snapshot().players[1].health;
    for _ in 0..60 {
        step(&mut game, false, false, 0);
    }
    assert!(game.snapshot().players[1].health < health);
}
#[test]
fn second_wind_first_hit_reloads() {
    let mut game = game(&["Second Wind"], false);
    hit(&mut game);
    let player = game.snapshot().players[0].clone();
    assert_eq!(player.fire_cooldown_ticks, 0);
    assert_eq!(player.ammunition, game.tuning.magazine_size);
    assert_eq!(player.reload_ticks, 0);
}
#[test]
fn blink_step_first_block_teleports() {
    let mut game = game(&["Blink Step"], false);
    let x = game.snapshot().players[0].x_milli;
    step(&mut game, false, true, 0);
    assert!(game.snapshot().players[0].x_milli - x > 85000);
}
#[test]
fn afterbeat_first_block_repeats_once() {
    let mut game = game(&["Afterbeat"], false);
    step(&mut game, false, true, 0);
    for _ in 0..14 {
        step(&mut game, false, false, 0);
    }
    assert_eq!(game.snapshot().metrics.block_activations, 2);
}
#[test]
fn watchfire_first_block_uses_current_shot_stats() {
    let mut game = game(&["Watchfire", "Needlework"], false);
    step(&mut game, false, true, 0);
    let snap = game.snapshot();
    assert_eq!(snap.metrics.shots_fired, 1);
    assert!(snap.projectiles[0].damage >= 102);
    assert!(snap.projectiles[0].velocity_x_milli_per_second > 8_000_000);
}
#[test]
fn echo_teleport_radar_combo_blips_twice_and_fires_twice() {
    let mut control = game(&["Afterbeat", "Watchfire"], false);
    let mut game = game(&["Afterbeat", "Blink Step", "Watchfire"], false);
    step(&mut game, false, true, 0);
    step(&mut control, false, true, 0);
    for _ in 0..13 {
        step(&mut game, false, false, 0);
        step(&mut control, false, false, 0);
    }
    let snap = game.snapshot();
    assert_eq!(snap.metrics.block_activations, 2);
    assert_eq!(snap.metrics.shots_fired, 2);
    assert!(snap.players[0].x_milli - control.snapshot().players[0].x_milli > 170000);
}
#[test]
fn grow_and_fast_shot_hits_harder_at_long_range() {
    let mut game = game(&["Long Haul", "Needlework"], false);
    hit(&mut game);
    assert!(game.snapshot().impacts[0].damage > 102);
}
#[test]
fn poison_spray_reload_combo_sustains_fire_from_one_hit() {
    let mut game = game(&["Slow Burn", "Hailstorm", "Second Wind"], false);
    let mut cards = pool();
    cards
        .iter_mut()
        .find(|c| c.title == "Second Wind")
        .unwrap()
        .event_rules
        .clear();
    let mut no_reload = game_with_content(
        &["Slow Burn", "Hailstorm", "Second Wind"],
        cards,
        arena(false),
    );
    hit(&mut game);
    hit(&mut no_reload);
    let shots = game.snapshot().metrics.shots_fired;
    // Fire upward after the one landed shot; subsequent reloads must come from poison.
    let mut poison_hits = 0;
    for _ in 0..180 {
        let health = game.snapshot().players[1].health;
        let input = [
            PlayerInput {
                fire: true,
                aim_y: 1000,
                ..Default::default()
            },
            PlayerInput::default(),
        ];
        game.step(&input);
        no_reload.step(&input);
        let after = game.snapshot();
        if after.players[1].health < health {
            poison_hits += 1;
            assert_eq!(after.players[0].fire_cooldown_ticks, 0);
            assert_eq!(after.players[0].ammunition, 30);
            assert_eq!(after.players[0].reload_ticks, 0);
        }
    }
    assert!(poison_hits >= 6);
    let snap = game.snapshot();
    assert!(snap.metrics.shots_fired > shots + 40);
    assert!(snap.metrics.hits >= 6);
    assert!(snap.metrics.shots_fired > no_reload.snapshot().metrics.shots_fired);
}
#[test]
fn held_copies_stack_stats_and_rules() {
    let mut game = game(
        &["Needlework", "Needlework", "Blink Step", "Blink Step"],
        false,
    );
    let x = game.snapshot().players[0].x_milli;
    step(&mut game, true, true, 0);
    let snap = game.snapshot();
    assert_eq!(snap.projectiles[0].damage, 125);
    assert!(snap.players[0].x_milli - x > 170000);
}
#[test]
fn self_triggering_chain_stops_and_replays_identically() {
    let mut cards = pool();
    let card = cards.iter_mut().find(|c| c.title == "Afterbeat").unwrap();
    card.event_rules[0].max_depth = 8;
    card.event_rules[0].effects = vec![CardEffect::RepeatBlock { delay_ticks: 1 }];
    let mut game = game(&["Afterbeat"], false);
    let mut replay = self::game(&["Afterbeat"], false);
    replay.flow.replace_catalog(cards.clone()).unwrap();
    game.flow.replace_catalog(cards).unwrap();
    step(&mut replay, false, true, 0);
    step(&mut game, false, true, 0);
    for _ in 0..100 {
        step(&mut game, false, false, 0);
    }
    assert!(game.snapshot().metrics.block_activations <= 9);
    assert!(game.reactions.is_empty());
    for _ in 0..100 {
        step(&mut replay, false, false, 0);
    }
    assert_eq!(game.snapshot(), replay.snapshot());
}
#[test]
fn card_file_edits_change_a_running_fighters_next_shot_and_reject_invalid_data() {
    let path = std::env::temp_dir().join(format!("quarrel-80-{}", std::process::id()));
    std::fs::create_dir_all(&path).unwrap();
    for entry in std::fs::read_dir(default_card_directory()).unwrap() {
        let entry = entry.unwrap();
        std::fs::copy(entry.path(), path.join(entry.file_name())).unwrap();
    }
    let mut game = game(&["Needlework"], false);
    game.card_path = Some(path.clone());
    let file = path.join("needle.ron");
    let source = std::fs::read_to_string(&file).unwrap();
    std::fs::write(&file, source.replace("2400", "3100")).unwrap();
    for _ in 0..15 {
        step(&mut game, false, false, 0);
    }
    step(&mut game, true, false, 1000);
    let shot = game.snapshot().projectiles[0].clone();
    assert!(shot.velocity_x_milli_per_second > 7_500_000);
    assert!(game.card_reload_error().is_none());
    std::fs::write(&file, "broken").unwrap();
    for _ in 0..15 {
        step(&mut game, false, false, 0);
    }
    assert!(game.card_reload_error().is_some());
    assert_eq!(
        game.snapshot().flow.unwrap().capabilities[0]
            .projectile_speed_factor
            .milli,
        3100
    );
    std::fs::remove_dir_all(path).unwrap();
}

#[test]
fn extra_shot_self_triggering_rule_is_bounded() {
    let mut game = game(&["Afterbeat"], false);
    let mut cards = pool();
    let card = cards.iter_mut().find(|c| c.title == "Afterbeat").unwrap();
    card.event_rules = vec![EventRule {
        on: CardEvent::Fire,
        max_depth: 8,
        effects: vec![CardEffect::ExtraShots {
            count: 1,
            spread_milliradians: 0,
        }],
    }];
    game.flow.replace_catalog(cards).unwrap();
    step(&mut game, true, false, 1000);
    for _ in 0..40 {
        step(&mut game, false, false, 1000);
    }
    assert!((2..=9).contains(&game.snapshot().metrics.shots_fired));
    assert!(game.reactions.is_empty());
}
#[test]
fn event_rules_react_to_landing_damage_and_kill() {
    for event in [CardEvent::Land, CardEvent::TakeDamage, CardEvent::Kill] {
        let mut control = game(&["Afterbeat"], false);
        let mut game = game(&["Afterbeat"], false);
        let mut cards = pool();
        let card = cards.iter_mut().find(|c| c.title == "Afterbeat").unwrap();
        card.event_rules = vec![EventRule {
            on: event,
            max_depth: 8,
            effects: vec![CardEffect::Teleport { distance: 90 }],
        }];
        game.flow.replace_catalog(cards).unwrap();
        for game in [&mut game, &mut control] {
            match event {
                CardEvent::Land => {
                    for _ in 0..30 {
                        step(game, false, false, 0);
                    }
                }
                CardEvent::TakeDamage => {
                    game.step(&[
                        PlayerInput::default(),
                        PlayerInput {
                            fire: true,
                            aim_x: -1000,
                            ..Default::default()
                        },
                    ]);
                    for _ in 0..15 {
                        step(game, false, false, 0);
                    }
                }
                CardEvent::Kill => {
                    game.world
                        .entity_mut(game.player_entities[1])
                        .get_mut::<PlayerState>()
                        .unwrap()
                        .health = 1;
                    hit(game);
                }
                _ => unreachable!(),
            }
        }
        let after = game.snapshot();
        let reference = control.snapshot();
        assert!(
            after.players[0].x_milli - reference.players[0].x_milli > 80000,
            "{event:?}: actual={}, without_rule={}",
            after.players[0].x_milli,
            reference.players[0].x_milli
        );
    }
}
#[test]
fn firecracker_explodes_on_terrain_and_watchfire_requires_sight() {
    let mut game = game(&["Firecracker", "Watchfire"], true);
    step(&mut game, false, true, 0);
    assert_eq!(game.snapshot().metrics.shots_fired, 0);
    step(&mut game, true, false, 0);
    for _ in 0..10 {
        step(&mut game, false, false, 0);
    }
    assert_eq!(game.snapshot().metrics.explosive_projectile_impacts, 1);
}
#[test]
fn drill_spends_its_allowance_on_terrain_crossed_between_ticks() {
    let mut game = game(&["Borer"], true);
    let mut cards = pool();
    cards
        .iter_mut()
        .find(|c| c.title == "Borer")
        .unwrap()
        .event_rules[0]
        .effects = vec![CardEffect::Drill(10)];
    game.flow.replace_catalog(cards).unwrap();
    step(&mut game, true, false, 0);
    for _ in 0..15 {
        step(&mut game, false, false, 0);
    }
    assert_eq!(game.snapshot().players[1].health, 10000);
    assert!(game.snapshot().projectiles.is_empty());
}

#[test]
fn a_poison_hit_reaction_explodes_at_the_victim() {
    let mut game = game(&["Slow Burn"], false);
    let mut cards = pool();
    cards
        .iter_mut()
        .find(|c| c.title == "Slow Burn")
        .unwrap()
        .event_rules
        .push(EventRule {
            on: CardEvent::Hit,
            max_depth: 1,
            effects: vec![CardEffect::Explode {
                radius: 50,
                damage_milli: 10,
            }],
        });
    game.flow.replace_catalog(cards).unwrap();
    hit(&mut game);
    let mut blasts = game
        .snapshot()
        .impacts
        .into_iter()
        .filter(|i| i.radius_milli > 0)
        .collect::<Vec<_>>();
    for _ in 0..15 {
        step(&mut game, false, false, 0);
        let snap = game.snapshot();
        blasts.extend(
            snap.impacts
                .into_iter()
                .filter(|i| i.radius_milli > 0 && i.tick == snap.tick),
        );
    }
    assert!(blasts.len() >= 2);
    assert!(blasts.iter().all(|i| i.x_milli > 200000));
}

#[test]
fn drilling_shots_still_expire_in_open_space() {
    let mut game = game(&["Borer"], false);
    for tick in 0..200 {
        game.step(&[
            PlayerInput {
                fire: tick == 0,
                aim_x: 0,
                aim_y: 1000,
                ..Default::default()
            },
            PlayerInput::default(),
        ]);
    }
    assert_eq!(game.snapshot().metrics.shots_fired, 1);
    assert!(game.projectile_entities.is_empty());
}
#[test]
fn shepherd_can_turn_a_shot_back_toward_the_shooter() {
    let mut game = game(&["Shepherd"], false);
    game.physics.move_player(0, Vector::new(-300.0, 300.0));
    step(&mut game, true, false, 0);
    for _ in 0..12 {
        game.step(&[
            PlayerInput {
                aim_x: -1000,
                ..Default::default()
            },
            PlayerInput::default(),
        ]);
    }
    assert!(game.snapshot().projectiles[0].velocity_x_milli_per_second < 0);
}

#[test]
fn saw_damage_runs_take_damage_rules_in_the_same_tick() {
    let mut cards = pool();
    cards
        .iter_mut()
        .find(|c| c.title == "Afterbeat")
        .unwrap()
        .event_rules = vec![EventRule {
        on: CardEvent::TakeDamage,
        max_depth: 8,
        effects: vec![CardEffect::Teleport { distance: 90 }],
    }];
    let mut arena = arena(false);
    arena.objects.push(ArenaObject {
        id: 9,
        position: [-100.0, -18.0],
        rotation: 0.0,
        shape: ArenaShape::Circle { radius: 20.0 },
        kind: ArenaKind::Saw {
            loose: false,
            teeth: 12,
            angular_velocity: 2.0,
        },
        color: [180, 80, 80],
        mass: 1.0,
        health: None,
        motion: None,
    });
    let mut game = game_with_content(&["Afterbeat"], cards, arena);
    for _ in 0..120 {
        let before = game.snapshot().players[0].clone();
        game.step(&[
            PlayerInput {
                move_axis: 1,
                aim_x: -1000,
                ..Default::default()
            },
            PlayerInput::default(),
        ]);
        let after = game.snapshot().players[0].clone();
        if after.health < before.health {
            assert!(before.x_milli - after.x_milli > 80000);
            return;
        }
    }
    panic!("the fighter never reached the saw");
}

#[test]
fn repeated_terrain_explosions_keep_only_recent_bounded_visual_impacts() {
    let mut game = game(&["Firecracker", "Hailstorm", "Rubber Round"], false);
    for _ in 0..3000 {
        game.step(&[
            PlayerInput {
                fire: true,
                aim_y: -1000,
                ..Default::default()
            },
            PlayerInput::default(),
        ]);
        let snap = game.snapshot();
        assert!(snap.impacts.len() <= 64);
        assert!(snap.impacts.iter().all(|i| snap.tick - i.tick <= 12));
        assert!(serde_json::to_vec(&snap).unwrap().len() < 60_000);
    }
    assert!(game.snapshot().metrics.explosive_projectile_impacts > 64);
}
#[test]
fn a_fast_shot_explodes_at_its_hit_and_damages_the_victim() {
    let mut game = game(
        &["Firecracker", "Needlework", "Needlework", "Needlework"],
        false,
    );
    step(&mut game, true, false, 0);
    let snap = game.snapshot();
    let hit = snap.impacts.iter().find(|i| i.target == Some(1)).unwrap();
    let blast = snap.impacts.iter().find(|i| i.radius_milli > 0).unwrap();
    assert_eq!((blast.x_milli, blast.y_milli), (hit.x_milli, hit.y_milli));
    assert!(snap.players[1].health < 10000 - hit.damage);
}
#[test]
fn reaction_shots_keep_fire_traits_even_when_the_next_reaction_fades() {
    let mut cards = pool();
    cards
        .iter_mut()
        .find(|c| c.title == "Afterbeat")
        .unwrap()
        .event_rules = vec![EventRule {
        on: CardEvent::Fire,
        max_depth: 8,
        effects: vec![
            CardEffect::Grow(180),
            CardEffect::ExtraShots {
                count: 1,
                spread_milliradians: 0,
            },
        ],
    }];
    let mut game = game_with_content(&["Afterbeat"], cards, arena(false));
    step(&mut game, true, false, 1000);
    step(&mut game, false, false, 1000);
    let snap = game.snapshot();
    assert!(snap.projectiles.len() >= 3);
    assert!(snap.projectiles.iter().all(|s| s.radius_milli > 5000));
}
#[test]
fn poison_backlog_preserves_both_fighters_primary_block_rules() {
    let mut cards = pool();
    cards
        .iter_mut()
        .find(|c| c.title == "Slow Burn")
        .unwrap()
        .event_rules[0]
        .effects = vec![CardEffect::Poison {
        ticks: 60,
        interval_ticks: 600,
        damage_milli: 180,
    }];
    cards
        .iter_mut()
        .find(|c| c.title == "Rubber Round")
        .unwrap()
        .event_rules
        .push(EventRule {
            on: CardEvent::Block,
            max_depth: 8,
            effects: vec![CardEffect::Teleport { distance: 90 }],
        });
    let mut game = game_with_content(
        &["Slow Burn", "Hailstorm", "Hailstorm", "Second Wind"],
        cards,
        arena(false),
    );
    for _ in 0..500 {
        step(&mut game, true, false, 40);
        if game.reactions.len() >= 254 {
            break;
        }
    }
    assert!(
        game.reactions.len() >= 254,
        "the real poison stream did not fill the queue: pending={}, hits={}, shots={}, player={:?}",
        game.reactions.len(),
        game.snapshot().metrics.hits,
        game.snapshot().metrics.shots_fired,
        game.snapshot().players[0]
    );
    let before = game.snapshot().players[1].x_milli;
    let shots = game.snapshot().metrics.shots_fired;
    game.step(&[
        PlayerInput {
            fire: true,
            block: true,
            aim_x: 1000,
            ..Default::default()
        },
        PlayerInput {
            block: true,
            aim_x: 1000,
            ..Default::default()
        },
    ]);
    assert_eq!(game.snapshot().metrics.shots_fired, shots + 1);
    assert!(game.snapshot().players[1].x_milli - before > 80000);
}

#[test]
fn spray_snapshot_keeps_new_shots_after_an_upward_stream() {
    let mut game = game(&["Hailstorm", "Hailstorm"], false);
    let tuning = CombatTuning {
        magazine_size: 128,
        ..Default::default()
    };
    let tuning_path = watch_tuning(&mut game, &tuning);
    // Spend the loaded rounds and refill through the ordinary reload gate.
    for _ in 0..game.snapshot().players[0].ammunition {
        game.step(&[
            PlayerInput {
                fire: true,
                aim_y: 1000,
                ..Default::default()
            },
            PlayerInput::default(),
        ]);
    }
    for _ in 0..tuning.reload_ticks {
        game.step(&[PlayerInput::default(); 2]);
    }
    for _ in 0..90 {
        game.step(&[
            PlayerInput {
                fire: true,
                aim_x: 0,
                aim_y: 1000,
                ..Default::default()
            },
            PlayerInput::default(),
        ]);
    }
    let before = game.snapshot().metrics.shots_fired;
    step(&mut game, true, false, 0);
    let snap = game.snapshot();
    assert!(snap.metrics.shots_fired > before);
    let newest = *game.projectile_entities.keys().max().unwrap();
    assert!(
        snap.projectiles.iter().any(|p| p.id == newest),
        "the newest threat disappeared from the live snapshot"
    );
    assert_eq!(snap.projectiles.len(), MAX_INSPECTED_PROJECTILES);
    std::fs::remove_file(tuning_path).unwrap();
}

#[test]
fn flight_cards_still_damage_breakable_pieces_once_per_contact() {
    for name in ["Borer", "Rubber Round"] {
        let mut arena = arena(true);
        arena.objects[1].kind = ArenaKind::Breakable { loose: false };
        arena.objects[1].health = Some(75);
        let mut game = game_with_content(&[name], pool(), arena);
        step(&mut game, true, false, 0);
        for _ in 0..15 {
            step(&mut game, false, false, 0);
        }
        assert_eq!(
            game.arena
                .objects
                .iter()
                .find(|o| o.id == 1)
                .unwrap()
                .health,
            Some(50),
            "{name}"
        );
    }
}

#[test]
fn firecracker_reaction_shot_explodes_with_current_weapon_rules() {
    let mut game = game(&["Watchfire", "Firecracker"], false);
    step(&mut game, false, true, 0);
    for _ in 0..15 {
        step(&mut game, false, false, 0);
    }
    let snap = game.snapshot();
    assert!(snap.metrics.hits > 0);
    assert_eq!(snap.metrics.explosive_projectile_impacts, 1);
}

#[test]
fn firecracker_small_spray_explosion_deals_positive_damage() {
    let mut game = game(
        &["Firecracker", "Hailstorm", "Hailstorm", "Second Wind"],
        false,
    );
    hit(&mut game);
    let snap = game.snapshot();
    assert_eq!(snap.metrics.explosive_projectile_impacts, 1);
    assert!(
        snap.impacts
            .iter()
            .filter(|i| i.target.is_some())
            .all(|i| i.damage > 0)
    );
}

fn watch_tuning(game: &mut AuthoritativeMatch, tuning: &CombatTuning) -> std::path::PathBuf {
    let stamp = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap()
        .as_nanos();
    let path = std::env::temp_dir().join(format!(
        "quarrel-card-tuning-{}-{stamp}.ron",
        std::process::id()
    ));
    std::fs::write(&path, ron::ser::to_string(tuning).unwrap()).unwrap();
    game.watch_tuning_file(&path).unwrap();
    path
}

#[test]
fn watchfire_uses_live_tuning_without_spending_primary_ammunition() {
    let mut game = game(&["Watchfire"], false);
    let path = watch_tuning(&mut game, &CombatTuning::default());
    let changed = CombatTuning {
        damage_per_hit: 120,
        ..Default::default()
    };
    std::fs::write(&path, ron::ser::to_string(&changed).unwrap()).unwrap();
    for _ in 0..15 {
        step(&mut game, false, false, 0);
    }
    step(&mut game, false, true, 0);
    let snap = game.snapshot();
    assert_eq!(snap.projectiles[0].damage, 120);
    assert_eq!(snap.players[0].ammunition, changed.magazine_size);
    assert_eq!(snap.players[0].fire_cooldown_ticks, 0);
    std::fs::remove_file(path).unwrap();
}

#[test]
fn echo_preserves_a_reflection_extended_block() {
    let mut game = game(&["Afterbeat"], false);
    game.step(&[
        PlayerInput {
            block: true,
            aim_x: 1000,
            ..Default::default()
        },
        PlayerInput {
            fire: true,
            aim_x: -1000,
            ..Default::default()
        },
    ]);
    for _ in 0..13 {
        step(&mut game, false, false, 0);
    }
    let snap = game.snapshot();
    assert_eq!(snap.metrics.reflections, 1);
    assert_eq!(snap.metrics.block_activations, 2);
    assert!(snap.players[0].block_ticks > game.tuning.block_duration_ticks);
}

#[test]
fn public_periodic_damage_emits_one_take_damage_reaction() {
    let mut game = game(&["Afterbeat"], false);
    let mut cards = pool();
    cards
        .iter_mut()
        .find(|c| c.title == "Afterbeat")
        .unwrap()
        .event_rules = vec![EventRule {
        on: CardEvent::TakeDamage,
        max_depth: 8,
        effects: vec![CardEffect::Teleport { distance: 90 }],
    }];
    game.flow.replace_catalog(cards).unwrap();
    let before = game.snapshot().players[0].x_milli;
    assert!(!game.damage_fighter(0, 1).unwrap());
    step(&mut game, false, false, 0);
    let after = game.snapshot();
    assert_eq!(after.players[0].health, 99);
    assert_eq!(after.players[0].x_milli - before, 90000);
}

#[test]
fn reaction_shots_use_the_same_capped_switchable_recoil() {
    let mut disabled = game(&["Watchfire"], false);
    let mut enabled = game(&["Watchfire"], false);
    let tuning = CombatTuning {
        recoil_enabled: false,
        ..Default::default()
    };
    let path = watch_tuning(&mut disabled, &tuning);
    step(&mut enabled, false, true, 0);
    step(&mut disabled, false, true, 0);
    let on = enabled.snapshot();
    let off = disabled.snapshot();
    assert!((-120001..-110000).contains(&on.players[0].velocity_x_milli_per_second));
    assert_eq!(off.players[0].velocity_x_milli_per_second, 0);
    assert_eq!(on.metrics.recoil_impulses, 1);
    assert_eq!(off.metrics.recoil_impulses, 0);
    assert_eq!(on.players[0].ammunition, tuning.magazine_size);
    std::fs::remove_file(path).unwrap();
}

#[test]
fn hailstorm_has_a_sustained_weak_stream_and_stackable_magazines() {
    let mut stream = game(&["Hailstorm"], false);
    let mut cards = pool();
    cards
        .iter_mut()
        .find(|c| c.title == "Hailstorm")
        .unwrap()
        .modifiers = GameplayModifiers::default();
    let mut neutral = game_with_content(&["Hailstorm"], cards, arena(false));
    assert_eq!(stream.snapshot().players[0].ammunition, 30);
    assert_eq!(neutral.snapshot().players[0].ammunition, 3);
    assert_eq!(
        game(&["Hailstorm", "Hailstorm"], false).snapshot().players[0].ammunition,
        57
    );
    let input = [
        PlayerInput {
            fire: true,
            aim_y: 1000,
            ..Default::default()
        },
        PlayerInput::default(),
    ];
    for _ in 0..600 {
        stream.step(&input);
        neutral.step(&input);
    }
    let stream = stream.snapshot();
    let neutral = neutral.snapshot();
    assert!(stream.metrics.shots_fired >= 85);
    assert!(stream.metrics.shots_fired > 5 * neutral.metrics.shots_fired);
    assert!(stream.projectiles.iter().all(|p| p.damage < 60));
}

#[test]
fn chosen_reaction_effects_deliver_their_damage_without_another_fade() {
    for (name, expected) in [("Firecracker", 84), ("Slow Burn", 75)] {
        let mut game = game(&["Watchfire", name], false);
        step(&mut game, false, true, 0);
        for _ in 0..200 {
            step(&mut game, false, false, 0);
        }
        assert_eq!(
            10000 - game.snapshot().players[1].health,
            expected,
            "{name}"
        );
    }
}

#[test]
fn zero_damage_tuning_stays_zero_through_card_shots_and_impacts() {
    let mut game = game(
        &["Watchfire", "Firecracker", "Slow Burn", "Long Haul"],
        false,
    );
    let path = watch_tuning(
        &mut game,
        &CombatTuning {
            damage_per_hit: 0,
            ..Default::default()
        },
    );
    step(&mut game, false, true, 0);
    assert!(game.snapshot().projectiles.iter().all(|p| p.damage == 0));
    for _ in 0..200 {
        step(&mut game, false, false, 0);
    }
    assert_eq!(game.snapshot().players[1].health, 10000);
    std::fs::remove_file(path).unwrap();
}

#[test]
fn blink_cannot_trap_a_fighter_under_a_shipped_floor_or_outside_a_wall() {
    let arena = load_arena_directory(&default_arena_directory())
        .unwrap()
        .into_iter()
        .find(|a| a.name == "Gatefall")
        .unwrap();
    for (ax, ay) in [(0, -1000), (-1000, 0), (707, -707)] {
        let mut game = game_with_content(&["Blink Step"], pool(), arena.clone());
        for _ in 0..40 {
            game.step(&[PlayerInput::default(); 2]);
        }
        game.step(&[
            PlayerInput {
                block: true,
                aim_x: ax,
                aim_y: ay,
                ..Default::default()
            },
            PlayerInput::default(),
        ]);
        for _ in 0..180 {
            game.step(&[PlayerInput::default(); 2]);
        }
        let snap = game.snapshot();
        assert!(snap.players[0].alive, "aim {ax},{ay}");
        assert_eq!(snap.players[0].health, 100, "aim {ax},{ay}");
        assert_eq!(snap.flow.unwrap().scores, vec![0, 0]);
    }
}

#[test]
fn blink_stops_at_a_wall_and_keeps_the_fighter_free_to_move() {
    let mut arena = arena(true);
    arena.spawns[0] = [-80.0, 0.0];
    let mut game = game_with_content(&["Blink Step"], pool(), arena);
    step(&mut game, false, true, 0);
    let x = game.snapshot().players[0].x_milli;
    assert!(x < -36000 && x > -81000);
    for _ in 0..20 {
        game.step(&[
            PlayerInput {
                move_axis: -1,
                aim_x: -1000,
                ..Default::default()
            },
            PlayerInput::default(),
        ]);
    }
    assert!(game.snapshot().players[0].x_milli < x - 20000);
}

#[test]
fn horizontal_blinks_cross_a_flat_floor_at_every_sampled_supported_position() {
    let mut arena = load_arena_directory(&default_arena_directory())
        .unwrap()
        .into_iter()
        .find(|a| a.name == "Gatefall")
        .unwrap();
    arena.objects.retain(|o| o.id == 0);
    arena.chains.clear();
    for walk in (0..60).step_by(2) {
        for direction in [-1, 1] {
            let mut game = game_with_content(&["Blink Step"], pool(), arena.clone());
            for _ in 0..40 {
                game.step(&[PlayerInput::default(); 2]);
            }
            for _ in 0..walk {
                game.step(&[
                    PlayerInput {
                        move_axis: 1,
                        ..Default::default()
                    },
                    PlayerInput::default(),
                ]);
            }
            for _ in 0..30 {
                game.step(&[PlayerInput::default(); 2]);
            }
            let before = game.snapshot().players[0].x_milli;
            game.step(&[
                PlayerInput {
                    block: true,
                    aim_x: direction * 1000,
                    ..Default::default()
                },
                PlayerInput::default(),
            ]);
            let distance = (game.snapshot().players[0].x_milli - before) * i32::from(direction);
            let expected = if direction < 0 {
                90000.min(before + 618000)
            } else {
                90000
            };
            assert!(
                distance >= expected - 10,
                "walk {walk}, direction {direction}, before {before}, distance {distance}"
            );
        }
    }
}

#[test]
fn blink_stops_at_the_column_in_a_concave_shipped_surface() {
    let arena = load_arena_directory(&default_arena_directory())
        .unwrap()
        .into_iter()
        .find(|a| a.name == "lime")
        .unwrap();
    let mut game = game_with_content(&["Blink Step"], pool(), arena);
    for _ in 0..20 {
        game.step(&[PlayerInput::default(); 2]);
    }
    for _ in 0..19 {
        game.step(&[
            PlayerInput {
                move_axis: -1,
                ..Default::default()
            },
            PlayerInput::default(),
        ]);
    }
    for _ in 0..120 {
        game.step(&[PlayerInput::default(); 2]);
    }
    let before = game.snapshot().players[0].x_milli;
    assert!((-496000..-495000).contains(&before), "before {before}");
    game.step(&[
        PlayerInput {
            block: true,
            aim_x: -1000,
            ..Default::default()
        },
        PlayerInput::default(),
    ]);
    let after = game.snapshot().players[0].x_milli;
    let allowance = (game
        .physics
        .rapier
        .integration_parameters
        .allowed_linear_error()
        * 1000.0) as i32;
    assert!(
        after >= -500000 - allowance - 10 && after < before,
        "blink crossed the column: {before} -> {after}"
    );
    for _ in 0..30 {
        game.step(&[
            PlayerInput {
                move_axis: 1,
                ..Default::default()
            },
            PlayerInput::default(),
        ]);
    }
    assert!(game.snapshot().players[0].x_milli > after + 20000);
    assert_eq!(game.snapshot().players[0].health, 100);
}

#[test]
fn blink_crosses_the_gap_between_same_height_platforms() {
    let mut arena = arena(false);
    arena.spawns = vec![[-50.0, 90.0], [240.0, 90.0]];
    arena.objects[0].position = [-142.0, 28.0];
    arena.objects[0].shape = ArenaShape::Rectangle {
        size: [276.0, 40.0],
    };
    let mut right = arena.objects[0].clone();
    right.id = 1;
    right.position[0] = 142.0;
    arena.objects.push(right);
    for crouch in [false, true] {
        let mut game = game_with_content(&["Blink Step"], pool(), arena.clone());
        for _ in 0..60 {
            game.step(&[
                PlayerInput {
                    crouch,
                    ..Default::default()
                },
                PlayerInput::default(),
            ]);
        }
        let before = game.snapshot().players[0].x_milli;
        game.step(&[
            PlayerInput {
                crouch,
                block: true,
                aim_x: 1000,
                ..Default::default()
            },
            PlayerInput::default(),
        ]);
        let distance = game.snapshot().players[0].x_milli - before;
        assert!(
            distance >= 89990,
            "crouch {crouch}: gap blink moved {distance}"
        );
        assert_eq!(game.snapshot().players[0].health, 100);
        for _ in 0..30 {
            game.step(&[
                PlayerInput {
                    crouch,
                    ..Default::default()
                },
                PlayerInput::default(),
            ]);
        }
        let after = &game.snapshot().players[0];
        assert!(after.grounded);
        assert!((after.x_milli - before - 90000).abs() <= 10);
        assert_eq!(after.health, 100);
    }
}
