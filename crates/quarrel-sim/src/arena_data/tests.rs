use super::*;
use bevy_rapier2d::parry::query::intersection_test;

const ORIGINALS: [&str; 10] = [
    "switchyard",
    "terraces",
    "keyhole",
    "kiln",
    "trestle",
    "skybridge",
    "lanterns",
    "gatefall",
    "pinch",
    "shuttle",
];

fn original(slug: &str) -> ArenaDefinition {
    ArenaDefinition::load(
        &Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("../../assets/arenas")
            .join(format!("{slug}.ron")),
    )
    .unwrap()
}

fn game(arena: ArenaDefinition, fighters: usize) -> AuthoritativeMatch {
    AuthoritativeMatch::with_content(
        MatchConfig {
            fighter_count: fighters,
            ..Default::default()
        },
        MatchContent {
            tuning: CombatTuning::default(),
            cards: load_card_directory(
                &Path::new(env!("CARGO_MANIFEST_DIR")).join("../../assets/cards"),
            )
            .unwrap(),
            arenas: vec![arena],
        },
    )
    .unwrap()
}

fn enter_combat(game: &mut AuthoritativeMatch) {
    let flow = game.snapshot().flow.unwrap();
    game.step(
        &flow
            .offers
            .iter()
            .map(|offer| PlayerInput {
                flow: Some(FlowCommand {
                    phase_revision: flow.phase_revision,
                    action: FlowAction::Confirm(offer[0]),
                }),
                ..Default::default()
            })
            .collect::<Vec<_>>(),
    );
    assert_eq!(game.snapshot().flow.unwrap().phase, FlowPhase::Combat);
}

fn aim_at(state: &MatchSnapshot, point: [f32; 2]) -> PlayerInput {
    let player = &state.players[0];
    let direction = (Vector::from(point)
        - Vector::new(
            player.x_milli as f32 / 1000.0,
            player.y_milli as f32 / 1000.0,
        ))
    .normalize();
    PlayerInput {
        aim_x: (direction.x * 1000.0) as i16,
        aim_y: (direction.y * 1000.0) as i16,
        fire: true,
        ..Default::default()
    }
}

fn assert_in_frame(arena: &ArenaDefinition, state: &MatchSnapshot) {
    for object in &state.arena_objects.as_ref().unwrap().objects {
        // Rapier permits a small contact penetration at the enclosing walls.
        for [x, y] in object
            .shape
            .world_vertices(object.position, object.rotation)
        {
            assert!(
                x >= arena.frame[0] - 2.0
                    && x <= arena.frame[2] + 2.0
                    && y >= arena.frame[1] - 2.0
                    && y <= arena.frame[3] + 2.0,
                "{} piece {} left the frame at tick {}: ({x}, {y})",
                arena.name,
                object.id,
                state.tick
            );
        }
    }
}

#[test]
fn original_arenas_have_safe_two_and_four_fighter_spawns_and_survive_twenty_seconds() {
    let mut names = BTreeSet::new();
    for slug in ORIGINALS {
        let arena = original(slug);
        assert!(names.insert(arena.name.clone()));
        assert_eq!(arena.frame, [-640.0, -360.0, 640.0, 360.0]);
        assert_eq!(arena.spawns.len(), 4);
        assert!(arena.surfaces.is_empty());
        assert!(arena.legacy_bodies.is_empty() && arena.legacy_saws.is_empty());
        for fighters in [2, 4] {
            let mut game = game(arena.clone(), fighters);
            let initial = game.snapshot();
            for (index, player) in initial.players.iter().enumerate() {
                let position = Vector::new(
                    player.x_milli as f32 / 1000.0,
                    player.y_milli as f32 / 1000.0,
                );
                assert_eq!(position.to_array(), arena.spawns[index]);
                assert!(position.x - CombatTuning::default().player_radius >= arena.frame[0]);
                assert!(position.x + CombatTuning::default().player_radius <= arena.frame[2]);
                assert!(position.y - CombatTuning::default().player_radius >= arena.frame[1]);
                assert!(position.y + CombatTuning::default().player_radius <= arena.frame[3]);
                let spawn = ColliderBuilder::ball(CombatTuning::default().player_radius)
                    .translation(position)
                    .build();
                for object in &arena.objects {
                    if matches!(object.kind, ArenaKind::Background) {
                        continue;
                    }
                    let geometry = object
                        .shape
                        .collider()
                        .translation(Vector::from(object.position))
                        .rotation(object.rotation)
                        .build();
                    assert!(
                        !intersection_test(
                            spawn.position(),
                            spawn.shape(),
                            geometry.position(),
                            geometry.shape(),
                        )
                        .unwrap(),
                        "{} fighter {index} starts in piece {}",
                        arena.name,
                        object.id
                    );
                }
                for other in initial.players.iter().skip(index + 1) {
                    assert!(
                        position.distance(Vector::new(
                            other.x_milli as f32 / 1000.0,
                            other.y_milli as f32 / 1000.0
                        )) > CombatTuning::default().player_radius * 2.0
                    );
                }
            }
            enter_combat(&mut game);
            for _ in 0..20 * TICKS_PER_SECOND {
                game.step(&vec![PlayerInput::default(); fighters]);
                let state = game.snapshot();
                // A draft or match end would stop physics and invalidate the duration.
                assert_eq!(state.flow.as_ref().unwrap().phase, FlowPhase::Combat);
                assert!(state.players.iter().all(|p| p.alive));
                assert_in_frame(&arena, &state);
            }
        }
    }
}

#[test]
fn original_arenas_cover_every_object_kind() {
    let mut kinds = BTreeSet::new();
    for slug in ORIGINALS {
        let arena = original(slug);
        for object in arena.objects {
            kinds.insert(match object.shape {
                ArenaShape::Rectangle { .. } => "rectangle",
                ArenaShape::Circle { .. } => "circle",
                ArenaShape::Polygon { .. } => "polygon",
            });
            kinds.insert(match object.kind {
                ArenaKind::Solid => "solid",
                ArenaKind::Loose => "loose",
                ArenaKind::Breakable { loose: false } => "fixed breakable",
                ArenaKind::Breakable { loose: true } => "loose breakable",
                ArenaKind::Background => "background",
                ArenaKind::Saw { loose: false, .. } => "fixed saw",
                ArenaKind::Saw { loose: true, .. } => "loose saw",
            });
            if let Some(motion) = object.motion {
                if motion.path.len() > 1 {
                    kinds.insert("translation");
                }
                if motion.angular_velocity != 0.0 {
                    kinds.insert("rotation");
                }
            }
        }
        for chain in arena.chains {
            kinds.insert(if chain.body_a.is_none() {
                "fixed anchor chain"
            } else {
                "body chain"
            });
        }
    }
    assert_eq!(
        kinds,
        BTreeSet::from([
            "rectangle",
            "circle",
            "polygon",
            "solid",
            "loose",
            "fixed breakable",
            "loose breakable",
            "background",
            "fixed saw",
            "loose saw",
            "translation",
            "rotation",
            "fixed anchor chain",
            "body chain",
        ])
    );
    for slug in ["switchyard", "terraces", "keyhole"] {
        let arena = original(slug);
        assert!(arena.chains.is_empty());
        assert!(
            arena
                .objects
                .iter()
                .all(|o| o.kind == ArenaKind::Solid && o.motion.is_none())
        );
    }
    for slug in ["skybridge", "lanterns"] {
        let arena = original(slug);
        assert!(arena.chains.len() >= 3);
        assert!(arena.chains.iter().all(|chain| {
            arena.objects.iter().any(|o| {
                o.id == chain.body_b
                    && matches!(
                        o.kind,
                        ArenaKind::Loose | ArenaKind::Breakable { loose: true }
                    )
            })
        }));
    }
}

#[test]
fn shots_topple_both_original_physics_stacks() {
    for (slug, target) in [("kiln", [-130.0, -145.0]), ("trestle", [-180.0, -215.0])] {
        let arena = original(slug);
        let mut idle = game(arena.clone(), 2);
        let mut shot = game(arena.clone(), 2);
        enter_combat(&mut idle);
        enter_combat(&mut shot);
        let mut toppled = false;
        for tick in 0..20 * TICKS_PER_SECOND {
            let input = if [0, 30, 60, 150].contains(&tick) {
                aim_at(&shot.snapshot(), target)
            } else {
                PlayerInput::default()
            };
            idle.step(&[PlayerInput::default(); 2]);
            shot.step(&[input, PlayerInput::default()]);
            let control = idle.snapshot();
            let fired = shot.snapshot();
            assert_in_frame(&arena, &fired);
            for piece in &fired.arena_objects.as_ref().unwrap().objects {
                if piece.kind != ArenaKind::Loose {
                    continue;
                }
                let unshot = control
                    .arena_objects
                    .as_ref()
                    .unwrap()
                    .objects
                    .iter()
                    .find(|o| o.id == piece.id)
                    .unwrap();
                assert!(unshot.rotation.abs() < 0.1, "{slug} toppled without a shot");
                if (piece.rotation - unshot.rotation).abs() > 0.35 {
                    toppled = true;
                }
            }
        }
        assert_eq!(shot.snapshot().metrics.shots_fired, 4);
        assert!(toppled, "a shot did not topple {slug}");
    }
}

#[test]
fn shooting_gatefall_support_swings_the_ball_into_the_middle_without_breaking_its_chain() {
    let arena = original("gatefall");
    let mut idle = game(arena.clone(), 2);
    let mut shot = game(arena.clone(), 2);
    enter_combat(&mut idle);
    enter_combat(&mut shot);
    let mut released = false;
    let mut crossed_middle = false;
    for tick in 0..20 * TICKS_PER_SECOND {
        let input = if [0, 30, 60, 150].contains(&tick) {
            aim_at(&shot.snapshot(), [-315.0, 18.0])
        } else {
            PlayerInput::default()
        };
        shot.step(&[input, PlayerInput::default()]);
        idle.step(&[PlayerInput::default(); 2]);
        let state = shot.snapshot();
        assert_in_frame(&arena, &state);
        let rendered = state.arena_objects.unwrap();
        assert_eq!(rendered.chains, arena.chains);
        released |= rendered.objects.iter().all(|o| o.id != 10);
        let ball = rendered.objects.iter().find(|o| o.id == 11).unwrap();
        crossed_middle |= released && ball.position[0] > -80.0;
    }
    let control = idle.snapshot().arena_objects.unwrap();
    let held_ball = control.objects.iter().find(|o| o.id == 11).unwrap();
    assert!(
        held_ball.position[0] < -250.0,
        "unshot ball was not held back"
    );
    assert!(control.objects.iter().any(|o| o.id == 10));
    assert!(released, "shots did not break the support");
    assert!(
        crossed_middle,
        "released ball did not swing into the middle"
    );
}
