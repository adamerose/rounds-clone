use super::scene::{flash_envelope, shock_envelope};
use super::*;

pub(super) fn wrapped_rules(item: &ItemDefinition) -> String {
    let mut output = Vec::new();
    for rule in &item.rules {
        let mut line = String::new();
        for word in rule.split_whitespace() {
            if !line.is_empty() && line.len() + word.len() + 1 > 23 {
                output.push(std::mem::take(&mut line));
            }
            if !line.is_empty() {
                line.push(' ');
            }
            line.push_str(word);
        }
        if !line.is_empty() {
            output.push(line);
        }
    }
    output.join("\n")
}

pub(super) fn spawn_flow_hud(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    let flow = snapshot
        .flow
        .as_ref()
        .expect("draft profile has flow state");
    let ice = snapshot.arena.iter().any(|surface| surface.id >= 40);
    let full_round = snapshot.round.as_ref().is_some_and(|round| {
        round
            .winner
            .is_some_and(|winner| round.scores[usize::from(winner)] >= 2)
    });
    let compact = ice || snapshot.hanging_entry.is_some() || full_round;
    if flow.phase == FlowPhase::Waiting {
        commands.spawn((
            SceneVisual,
            Text2d::new("WAITING"),
            TextFont {
                font_size: FontSize::Px(72.0),
                ..default()
            },
            TextColor(Color::WHITE),
            Transform::from_xyz(0.0, 22.0, 31.0),
        ));
    }
    if matches!(
        flow.phase,
        FlowPhase::CombatConclusion | FlowPhase::RematchPrompt
    ) {
        commands.spawn((
            SceneVisual,
            Sprite::from_color(Color::srgba_u8(0, 8, 28, 105), Vec2::new(1_280.0, 720.0)),
            Transform::from_xyz(0.0, 0.0, 28.0),
        ));
        let (title, size) = if flow.phase == FlowPhase::CombatConclusion {
            ("VICTORY!", 86.0)
        } else {
            ("REMATCH?", 80.0)
        };
        commands.spawn((
            SceneVisual,
            Text2d::new(title),
            TextFont {
                font_size: FontSize::Px(size),
                ..default()
            },
            TextColor(Color::srgb_u8(100, 238, 237)),
            Transform::from_xyz(0.0, 70.0, 31.0),
        ));
        if flow.phase == FlowPhase::CombatConclusion
            && let Some(winner) = flow.winner
        {
            commands.spawn((
                SceneVisual,
                Text2d::new(if winner == 0 { "ORANGE" } else { "BLUE" }),
                TextFont {
                    font_size: FontSize::Px(28.0),
                    ..default()
                },
                TextColor(if winner == 0 {
                    Color::srgb_u8(255, 153, 50)
                } else {
                    Color::srgb_u8(78, 204, 255)
                }),
                Transform::from_xyz(0.0, 15.0, 31.0),
            ));
        }
        if flow.phase == FlowPhase::RematchPrompt {
            commands.spawn((
                SceneVisual,
                Text2d::new("YES      NO"),
                TextFont {
                    font_size: FontSize::Px(45.0),
                    ..default()
                },
                TextColor(Color::WHITE),
                Transform::from_xyz(0.0, -30.0, 31.0),
            ));
            for (index, vote) in flow.rematch_votes.iter().enumerate() {
                if *vote == quarrel_sim::RematchVote::Yes {
                    commands.spawn((
                        SceneVisual,
                        Mesh2d(meshes.add(Circle::new(8.0))),
                        MeshMaterial2d(materials.add(if index == 0 {
                            Color::srgb_u8(255, 103, 40)
                        } else {
                            Color::srgb_u8(61, 178, 255)
                        })),
                        Transform::from_xyz(-82.0 + index as f32 * 25.0, -72.0, 32.0),
                    ));
                }
            }
        }
    }
    for player in 0..2 {
        for index in 0..5_u8 {
            let award_in_flight = full_round
                && snapshot.round.as_ref().is_some_and(|round| {
                    round.winner == Some(player as u8)
                        && (round.phase == quarrel_sim::RoundPhase::ResultTransition
                            || round.phase_tick < 105)
                })
                && flow.phase != FlowPhase::Waiting;
            let filled = index < flow.scores[player].saturating_sub(u8::from(award_in_flight));
            let color = if player == 0 {
                Color::srgb_u8(255, 116, 45)
            } else {
                Color::srgb_u8(76, 184, 255)
            };
            let mesh = if compact {
                Mesh::from(Circle::new(if filled { 7.0 } else { 1.3 }))
            } else if filled {
                Mesh::from(Circle::new(8.0))
            } else {
                Mesh::from(Annulus::new(5.5, 8.0))
            };
            commands.spawn((
                SceneVisual,
                HudScorePip {
                    player: player as u8,
                    index,
                    filled,
                },
                Mesh2d(meshes.add(mesh)),
                MeshMaterial2d(materials.add(if filled {
                    color
                } else {
                    color.with_alpha(0.38)
                })),
                Transform::from_xyz(
                    if compact {
                        -612.0 + index as f32 * 19.5
                    } else {
                        -610.0 + index as f32 * 22.0
                    },
                    if compact {
                        332.0 - player as f32 * 42.0
                    } else {
                        330.0 - player as f32 * 25.0
                    },
                    35.0,
                ),
            ));
        }
        let badges = flow.prior_badges[player]
            .iter()
            .map(|badge| badge.label())
            .chain(flow.loadouts[player].iter().map(|item| item.short_badge()))
            .collect::<Vec<_>>();
        let badge_count = badges.len();
        let badge_x = |index: usize| {
            if matches!(
                flow.phase,
                FlowPhase::PostRoundDraft
                    | FlowPhase::PostRoundReveal
                    | FlowPhase::PostRoundBridge
                    | FlowPhase::HangingEntry
            ) {
                612.0 - (badge_count - 1 - index) as f32 * 37.0
            } else {
                612.0 - index as f32 * 37.0
            }
        };
        for (index, badge) in badges.into_iter().enumerate() {
            if compact {
                let center = Vec2::new(badge_x(index), 332.0 - player as f32 * 40.0);
                for (offset, size) in [
                    (Vec2::new(-16.0, 0.0), Vec2::new(1.0, 32.0)),
                    (Vec2::new(16.0, 0.0), Vec2::new(1.0, 32.0)),
                    (Vec2::new(0.0, -16.0), Vec2::new(32.0, 1.0)),
                    (Vec2::new(0.0, 16.0), Vec2::new(32.0, 1.0)),
                ] {
                    commands.spawn((
                        SceneVisual,
                        Sprite::from_color(
                            if player == 0 {
                                Color::srgb_u8(220, 155, 76)
                            } else {
                                Color::srgb_u8(74, 172, 194)
                            },
                            size,
                        ),
                        Transform::from_xyz(center.x + offset.x, center.y + offset.y, 35.0),
                    ));
                }
            }
            commands.spawn((
                SceneVisual,
                HudBadge {
                    player: player as u8,
                    label: badge.to_owned(),
                },
                Text2d::new(badge),
                TextFont {
                    font_size: FontSize::Px(19.0),
                    ..default()
                },
                TextColor(if player == 0 {
                    Color::srgb_u8(255, 156, 82)
                } else {
                    Color::srgb_u8(103, 206, 255)
                }),
                Transform::from_xyz(
                    if compact {
                        badge_x(index)
                    } else {
                        552.0 + (index % 3) as f32 * 34.0
                    },
                    if compact {
                        332.0 - player as f32 * 40.0
                    } else {
                        326.0 - player as f32 * 88.0 - (index / 3) as f32 * 30.0
                    },
                    35.0,
                ),
            ));
        }
    }
}

pub(super) fn spawn_timber_floor(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
    center: Vec2,
    size: Vec2,
    alpha: f32,
) {
    const SEGMENTS: usize = 14;
    const FACETS: [[u8; 3]; 4] = [[246, 0, 79], [222, 0, 75], [255, 17, 101], [195, 0, 70]];
    let age = snapshot
        .explosions
        .last()
        .map(|explosion| snapshot.tick.saturating_sub(explosion.tick));
    let flash = age.map(flash_envelope).unwrap_or(0.0);
    let shock = age.map(shock_envelope).unwrap_or(0.0);
    let explosion_x = snapshot
        .explosions
        .last()
        .map(|explosion| explosion.x_milli as f32 / 1_000.0)
        .unwrap_or(-245.0);
    let left_edge = center.x - size.x * 0.5;
    let bottom = center.y - size.y * 0.5;
    let segment_width = size.x / SEGMENTS as f32;
    let mut top = [0.0_f32; SEGMENTS + 1];
    for (index, value) in top.iter_mut().enumerate() {
        let x = left_edge + index as f32 * segment_width;
        let static_facet =
            ((index as f32 * 2.17).sin() * 5.5) + if index % 4 == 0 { 5.0 } else { 0.0 };
        let distance = (x - explosion_x).abs();
        let falloff = (1.0 - distance / 900.0).clamp(0.0, 1.0);
        let phase = (distance * 0.026 - age.unwrap_or(0) as f32 * 0.31).sin();
        *value =
            center.y + size.y * 0.5 + static_facet + phase * falloff * (flash * 7.0 + shock * 18.0);
    }

    let shadow_offset = Vec2::new(24.0 + shock * 34.0, 12.0 + shock * 8.0);
    for segment in 0..SEGMENTS {
        let left = left_edge + segment as f32 * segment_width;
        let right = left + segment_width + 0.5;
        let top_left = top[segment];
        let top_right = top[segment + 1];
        spawn_triangle(
            commands,
            meshes,
            materials,
            [
                Vec2::new(left, bottom) + shadow_offset,
                Vec2::new(right, bottom) + shadow_offset,
                Vec2::new(right, top_right) + shadow_offset,
            ],
            Color::srgba_u8(0, 8, 34, (205.0 * alpha) as u8),
            -2.0,
        );
        spawn_triangle(
            commands,
            meshes,
            materials,
            [
                Vec2::new(left, bottom) + shadow_offset,
                Vec2::new(right, top_right) + shadow_offset,
                Vec2::new(left, top_left) + shadow_offset,
            ],
            Color::srgba_u8(0, 8, 34, (205.0 * alpha) as u8),
            -2.0,
        );
        let first_color = FACETS[segment % FACETS.len()];
        let second_color = FACETS[(segment + 1) % FACETS.len()];
        spawn_triangle(
            commands,
            meshes,
            materials,
            [
                Vec2::new(left, bottom),
                Vec2::new(right, bottom),
                Vec2::new(right, top_right),
            ],
            Color::srgb_u8(first_color[0], first_color[1], first_color[2]).with_alpha(alpha),
            0.0,
        );
        spawn_triangle(
            commands,
            meshes,
            materials,
            [
                Vec2::new(left, bottom),
                Vec2::new(right, top_right),
                Vec2::new(left, top_left),
            ],
            Color::srgb_u8(second_color[0], second_color[1], second_color[2]).with_alpha(alpha),
            0.0,
        );
        if shock > 0.0 {
            let echo = Vec2::new((segment as f32 * 1.7).sin() * 8.0, 8.0 + shock * 10.0);
            spawn_triangle(
                commands,
                meshes,
                materials,
                [
                    Vec2::new(left, top_left - 7.0) + echo,
                    Vec2::new(right, top_right - 7.0) + echo,
                    Vec2::new(right, top_right) + echo,
                ],
                if segment % 2 == 0 {
                    Color::srgba_u8(0, 226, 255, (85.0 * shock) as u8)
                } else {
                    Color::srgba_u8(255, 0, 117, (105.0 * shock) as u8)
                },
                0.5,
            );
        }
    }
}

pub(super) fn spawn_triangle(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    vertices: [Vec2; 3],
    color: Color,
    z: f32,
) {
    commands.spawn((
        SceneVisual,
        Mesh2d(meshes.add(Triangle2d::new(vertices[0], vertices[1], vertices[2]))),
        MeshMaterial2d(materials.add(color)),
        Transform::from_xyz(0.0, 0.0, z),
    ));
}
