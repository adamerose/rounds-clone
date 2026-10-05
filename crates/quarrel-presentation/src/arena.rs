use super::*;
use super::{
    draft::{spawn_card, spawn_draft_scene},
    hud::{spawn_flow_hud, spawn_timber_floor, spawn_triangle},
    radial::{
        clip_ice_paint, radial_brush_noise, spawn_radial_backdrop, spawn_radial_impacts,
        spawn_radial_result, spawn_radial_saw, spawn_radial_surface_finish,
    },
    scene::{
        flash_envelope, spawn_yellow_hud, spawn_yellow_paper, spawn_yellow_result,
        yellow_flash_envelope,
    },
};

pub(super) fn spawn_snapshot_scene(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    if snapshot.arena_objects.is_some() {
        crate::data_arena::spawn_data_arena_scene(commands, meshes, materials, snapshot);
        return;
    }
    let profile = snapshot
        .profile
        .parse::<ReplayProfile>()
        .unwrap_or(ReplayProfile::TealDuelReplay);
    let timber_scene = snapshot
        .dynamic_bodies
        .iter()
        .any(|body| body.shape == DynamicBodyShape::Timber);
    let draft_replay = profile == ReplayProfile::RematchDraftReplay;
    let radial_replay = profile == ReplayProfile::RadialSawHalfBlueReplay;
    let yellow_replay = profile == ReplayProfile::YellowCrateTerminalBlastReplay;
    let lime_replay = profile == ReplayProfile::LimeModularArenaReplay;
    let ice_scene = snapshot.arena.iter().any(|surface| surface.id >= 40);
    let hanging_scene = snapshot.hanging_entry.is_some();
    let fighter_scale = if ice_scene || hanging_scene {
        0.56
    } else if yellow_replay {
        0.62
    } else {
        1.0
    };
    let circle = meshes.add(Circle::new(22.0 * fighter_scale));
    let block_ring = meshes.add(Annulus::new(29.0 * fighter_scale, 33.0 * fighter_scale));
    let bullet = meshes.add(Circle::new(5.0));

    commands.spawn((
        SceneVisual,
        CaptureElement::Background,
        Sprite::from_color(
            if radial_replay {
                Color::srgb_u8(188, 238, 229)
            } else if lime_replay {
                Color::srgb_u8(2, 43, 49)
            } else if yellow_replay {
                Color::srgb_u8(2, 43, 54)
            } else if ice_scene || hanging_scene {
                Color::srgb_u8(3, 42, 61)
            } else if timber_scene {
                Color::srgb_u8(2, 32, 49)
            } else if draft_replay {
                Color::srgb_u8(3, 39, 49)
            } else {
                Color::srgb_u8(2, 48, 54)
            },
            Vec2::new(
                if yellow_replay { 1_500.0 } else { 1_280.0 },
                if yellow_replay { 900.0 } else { 720.0 },
            ),
        ),
        Transform::from_xyz(0.0, 0.0, -100.0),
    ));
    if yellow_replay || ice_scene || lime_replay {
        spawn_yellow_paper(commands, meshes, materials, snapshot.tick, ice_scene);
    } else if !radial_replay {
        for (index, x) in [-520.0_f32, -260.0, 0.0, 260.0, 520.0]
            .into_iter()
            .enumerate()
        {
            let offset = backdrop_panel_offset(snapshot.tick, index, hanging_scene);
            commands.spawn((
                SceneVisual,
                Sprite::from_color(
                    if yellow_replay && index % 2 == 0 {
                        Color::srgba_u8(10, 74, 78, 62)
                    } else if yellow_replay {
                        Color::srgba_u8(0, 22, 46, 78)
                    } else if timber_scene && index % 2 == 0 {
                        Color::srgba_u8(5, 59, 78, 65)
                    } else if timber_scene {
                        Color::srgba_u8(0, 20, 42, 74)
                    } else if index % 2 == 0 {
                        Color::srgba_u8(9, 77, 76, 78)
                    } else {
                        Color::srgba_u8(0, 30, 57, 68)
                    },
                    Vec2::new(270.0, 840.0),
                ),
                Transform::from_xyz(x + offset, 20.0, -90.0)
                    .with_rotation(Quat::from_rotation_z(0.08 * (index as f32 - 2.0))),
            ));
        }
    }

    if radial_replay {
        spawn_radial_backdrop(commands, meshes, materials, snapshot.tick, &snapshot.arena);
    }
    if let Some(hanging) = &snapshot.hanging_entry {
        spawn_hanging_entry(commands, materials, hanging);
    }

    if let Some(flow) = &snapshot.flow
        && matches!(
            flow.phase,
            FlowPhase::ArenaFade
                | FlowPhase::Draft
                | FlowPhase::Reveal
                | FlowPhase::Handoff
                | FlowPhase::ArenaTransition
                | FlowPhase::PostRoundDraft
                | FlowPhase::PostRoundReveal
                | FlowPhase::PostRoundBridge
        )
    {
        spawn_draft_scene(commands, meshes, materials, snapshot);
        spawn_flow_hud(commands, meshes, materials, snapshot);
        return;
    }

    let arena_alpha = snapshot.round.as_ref().map_or(1.0, |round| {
        if round.phase == quarrel_sim::RoundPhase::HalfOrange {
            (1.0 - (round.phase_tick as f32 - 40.0) / 30.0).clamp(0.0, 1.0)
        } else {
            1.0
        }
    });
    for surface in &snapshot.arena {
        if arena_alpha == 0.0 {
            continue;
        }
        if radial_replay && surface.id >= 6 {
            continue;
        }
        let x = surface.center_x_milli as f32 / 1_000.0;
        let y = surface.center_y_milli as f32 / 1_000.0;
        let width = surface.width_milli as f32 / 1_000.0;
        let height = surface.height_milli as f32 / 1_000.0;
        let rotation = surface.rotation_milliradians as f32 / 1_000.0;
        if lime_replay {
            spawn_lime_surface(commands, meshes, materials, surface, snapshot.tick);
            continue;
        }
        if ice_scene {
            spawn_ice_surface(commands, meshes, materials, surface, snapshot.tick);
            continue;
        }
        if timber_scene {
            spawn_timber_floor(
                commands,
                meshes,
                materials,
                snapshot,
                Vec2::new(x, y),
                Vec2::new(width, height),
                arena_alpha,
            );
            continue;
        }
        if (surface.id < 10 || yellow_replay) && !radial_replay {
            let direction = if x < 0.0 { -1.0 } else { 1.0 };
            let shadow_length = 560.0;
            commands.spawn((
                SceneVisual,
                Sprite::from_color(
                    Color::srgb_u8(0, 14, 55),
                    Vec2::new(width * 1.05, shadow_length),
                ),
                Transform::from_xyz(x + direction * 48.0, y - shadow_length / 2.0, -40.0)
                    .with_rotation(Quat::from_rotation_z(direction * -0.16)),
            ));
        }
        if draft_replay {
            commands.spawn((
                SceneVisual,
                Sprite::from_color(Color::srgb_u8(137, 83, 25), Vec2::new(34.0, 58.0)),
                Transform::from_xyz(x - width * 0.16, y + height * 0.5 + 29.0, -1.0),
            ));
            commands.spawn((
                SceneVisual,
                Sprite::from_color(Color::srgb_u8(113, 66, 24), Vec2::new(30.0, 42.0)),
                Transform::from_xyz(x + width * 0.13, y + height * 0.5 + 21.0, -1.0),
            ));
        }
        if yellow_replay {
            let top_left = Vec2::new(x - width * 0.5, y + height * 0.5);
            let top_right = Vec2::new(x + width * 0.5, y + height * 0.5);
            let bottom_right = Vec2::new(x + width * 0.36, y - height * 0.5);
            let bottom_left = Vec2::new(x - width * 0.36, y - height * 0.5);
            for triangle in [
                [top_left, top_right, bottom_right],
                [top_left, bottom_right, bottom_left],
            ] {
                spawn_triangle(
                    commands,
                    meshes,
                    materials,
                    triangle,
                    Color::linear_rgba(1.8, 1.45, 0.02, 1.0),
                    0.0,
                );
            }
        } else {
            commands.spawn((
                SceneVisual,
                Sprite::from_color(
                    Color::srgb_u8(
                        surface.face_rgb[0],
                        surface.face_rgb[1],
                        surface.face_rgb[2],
                    ),
                    Vec2::new(width, height),
                ),
                Transform::from_xyz(x, y, 0.0).with_rotation(Quat::from_rotation_z(rotation)),
            ));
        }
        if radial_replay {
            spawn_radial_surface_finish(
                commands,
                meshes,
                materials,
                Vec2::new(x, y),
                Vec2::new(width, height),
                rotation,
                surface.id,
            );
        }
        if draft_replay {
            spawn_triangle(
                commands,
                meshes,
                materials,
                [
                    Vec2::new(x - width * 0.5, y + height * 0.5),
                    Vec2::new(x + width * 0.15, y + height * 0.5),
                    Vec2::new(x - width * 0.18, y - height * 0.5),
                ],
                Color::srgba_u8(255, 242, 37, 120),
                1.0,
            );
        }
    }

    if radial_replay {
        for saw in &snapshot.saws {
            spawn_radial_saw(commands, meshes, materials, saw);
        }
    }

    for constraint in snapshot.constraints.iter().filter(|constraint| {
        arena_alpha > 0.0
            && constraint.active
            && constraint.kind == quarrel_sim::ConstraintKind::Rope
    }) {
        let Some(body) = snapshot
            .dynamic_bodies
            .iter()
            .find(|body| body.id == constraint.body_b)
        else {
            continue;
        };
        let anchor = Vec2::new(
            constraint.anchor_x_milli as f32 / 1_000.0,
            constraint.anchor_y_milli as f32 / 1_000.0,
        );
        let position = Vec2::new(body.x_milli as f32 / 1_000.0, body.y_milli as f32 / 1_000.0);
        let segment = position - anchor;
        commands.spawn((
            SceneVisual,
            Sprite::from_color(
                Color::srgba_u8(130, 79, 54, (170.0 * arena_alpha) as u8),
                Vec2::new(segment.length(), 2.0),
            ),
            Transform::from_xyz(anchor.x + segment.x * 0.5, anchor.y + segment.y * 0.5, -4.0)
                .with_rotation(Quat::from_rotation_z(segment.y.atan2(segment.x))),
        ));
    }

    for body in &snapshot.dynamic_bodies {
        if arena_alpha == 0.0 {
            continue;
        }
        let x = body.x_milli as f32 / 1_000.0;
        let y = body.y_milli as f32 / 1_000.0;
        let rotation = body.rotation_milliradians as f32 / 1_000.0;
        let color = Color::srgb_u8(body.face_rgb[0], body.face_rgb[1], body.face_rgb[2])
            .with_alpha(arena_alpha);
        let (width, height) = match body.shape {
            DynamicBodyShape::Timber | DynamicBodyShape::Crate => (
                body.width_milli as f32 / 1_000.0,
                body.height_milli as f32 / 1_000.0,
            ),
            DynamicBodyShape::Weight => {
                let diameter = body.radius_milli as f32 / 500.0;
                (diameter, diameter)
            }
        };
        commands.spawn((
            SceneVisual,
            Sprite::from_color(
                Color::srgba_u8(0, 8, 25, (125.0 * arena_alpha) as u8),
                Vec2::new(width * 1.04, height * 1.04),
            ),
            Transform::from_xyz(x + 13.0, y - 15.0, -3.0)
                .with_rotation(Quat::from_rotation_z(rotation)),
        ));
        if body.shape == DynamicBodyShape::Weight {
            commands.spawn((
                SceneVisual,
                Mesh2d(meshes.add(Circle::new(body.radius_milli as f32 / 1_000.0))),
                MeshMaterial2d(materials.add(color)),
                Transform::from_xyz(x, y, 1.0),
            ));
        } else {
            commands.spawn((
                SceneVisual,
                Sprite::from_color(color, Vec2::new(width, height)),
                Transform::from_xyz(x, y, 1.0).with_rotation(Quat::from_rotation_z(rotation)),
            ));
        }
    }

    if let Some(explosion) = snapshot.explosions.last().filter(|_| {
        ice_scene
            || snapshot
                .round
                .as_ref()
                .map(|round| round.phase == quarrel_sim::RoundPhase::Combat)
                .unwrap_or(true)
    }) {
        let age = snapshot.tick.saturating_sub(explosion.tick);
        if age <= 48 {
            let mut center = Vec2::new(
                explosion.x_milli as f32 / 1_000.0,
                explosion.y_milli as f32 / 1_000.0,
            );
            if yellow_replay {
                center.y -= 40.0;
            }
            let flash = if ice_scene {
                yellow_flash_envelope(age + 3)
            } else if yellow_replay {
                yellow_flash_envelope(age)
            } else {
                flash_envelope(age)
            };
            if explosion.id >= 10_000 && flash > 0.0 && !yellow_replay && !timber_scene {
                for wedge in 0..18 {
                    let angle = wedge as f32 * std::f32::consts::TAU / 18.0;
                    let radius = if ice_scene {
                        (40.0 + age as f32 * 3.0) * flash.sqrt()
                    } else {
                        (90.0 + age as f32 * 13.0) * flash.sqrt()
                    };
                    let spread = 0.11 + (wedge % 3) as f32 * 0.025;
                    spawn_triangle(
                        commands,
                        meshes,
                        materials,
                        [
                            center + Vec2::new(angle.cos(), angle.sin()) * 12.0,
                            center
                                + Vec2::new((angle - spread).cos(), (angle - spread).sin())
                                    * radius,
                            center
                                + Vec2::new((angle + spread).cos(), (angle + spread).sin())
                                    * (radius * 0.78),
                        ],
                        if wedge % 2 == 0 {
                            Color::linear_rgba(4.8 * flash, 3.1 * flash, 0.08, 0.88)
                        } else {
                            Color::linear_rgba(3.5 * flash, 1.2 * flash, 0.02, 0.82)
                        },
                        19.0,
                    );
                }
            }
            if flash > 0.0 {
                commands.spawn((
                    SceneVisual,
                    Mesh2d(meshes.add(Circle::new(28.0 + age as f32 * 0.22))),
                    MeshMaterial2d(materials.add(Color::srgba_u8(
                        255,
                        72,
                        12,
                        (38.0 * flash) as u8,
                    ))),
                    Transform::from_xyz(center.x, center.y, 18.0),
                ));
                commands.spawn((
                    SceneVisual,
                    Mesh2d(meshes.add(Circle::new(if yellow_replay {
                        12.0 + flash * 17.0
                    } else {
                        7.0 + flash * 8.0
                    }))),
                    MeshMaterial2d(materials.add(if yellow_replay {
                        Color::linear_rgba(8.5 * flash, 7.2 * flash, 2.4 * flash, 0.98)
                    } else {
                        Color::linear_rgba(4.5 * flash, 3.2 * flash, 0.8 * flash, 0.95)
                    })),
                    Transform::from_xyz(center.x, center.y, 22.0),
                ));
                let lobe_count = if yellow_replay { 33 } else { 19 };
                for lobe in 0..lobe_count {
                    let angle = lobe as f32 * 2.399 + age as f32 * 0.018;
                    let distance = if yellow_replay {
                        5.0 + (lobe % 7) as f32 * 5.2
                            + age as f32 * (0.38 + (lobe % 3) as f32 * 0.08)
                    } else {
                        7.0 + (lobe % 5) as f32 * 4.4
                            + age as f32 * (0.24 + (lobe % 3) as f32 * 0.07)
                    };
                    let size = if yellow_replay {
                        (6.0 + (lobe * 7 % 17) as f32) * flash.powf(0.55)
                    } else {
                        (8.0 + (lobe * 7 % 13) as f32) * flash.powf(0.65)
                    };
                    let green = if yellow_replay {
                        if lobe % 4 == 0 { 4.6 } else { 2.5 }
                    } else if lobe % 3 == 0 {
                        2.4
                    } else {
                        1.1
                    };
                    commands.spawn((
                        SceneVisual,
                        Mesh2d(meshes.add(Circle::new(size.max(1.5)))),
                        MeshMaterial2d(materials.add(Color::linear_rgba(
                            if yellow_replay {
                                6.3 * flash
                            } else {
                                3.2 * flash
                            },
                            green * flash,
                            if yellow_replay { 0.35 * flash } else { 0.05 },
                            if yellow_replay { 0.9 } else { 0.78 },
                        ))),
                        Transform::from_xyz(
                            center.x + angle.cos() * distance,
                            center.y + angle.sin() * distance,
                            20.0 + (lobe % 2) as f32,
                        ),
                    ));
                }
            }
            for spark in 0..if ice_scene && age > 12 {
                0
            } else if ice_scene {
                24
            } else {
                72
            } {
                let angle = spark as f32 * 2.399 + (spark % 5) as f32 * 0.11;
                let speed = 1.5 + (spark * 11 % 17) as f32 * 0.18;
                let distance = 10.0 + age as f32 * speed;
                let gravity = age as f32 * age as f32 * 0.010;
                let end = center
                    + Vec2::new(angle.cos(), angle.sin()) * distance
                    + Vec2::new(0.0, -gravity);
                let spark_envelope = (1.0 - age as f32 / 49.0).max(0.0);
                let length = (3.0 + age as f32 * speed * 0.12).min(24.0);
                commands.spawn((
                    SceneVisual,
                    Sprite::from_color(
                        if spark % 4 == 0 {
                            Color::linear_rgba(
                                3.8 * spark_envelope,
                                2.4 * spark_envelope,
                                0.3 * spark_envelope,
                                0.95,
                            )
                        } else {
                            Color::linear_rgba(
                                3.0 * spark_envelope,
                                1.1 * spark_envelope,
                                0.08,
                                0.85,
                            )
                        },
                        Vec2::new(length, 1.4 + (spark % 3) as f32 * 0.45),
                    ),
                    Transform::from_xyz(end.x, end.y, 23.0)
                        .with_rotation(Quat::from_rotation_z(angle)),
                ));
            }
            if yellow_replay && (5..=29).contains(&age) {
                let trail_fade = ((30 - age) as f32 / 25.0).clamp(0.0, 1.0);
                for trail in 0..36 {
                    let forward = trail < 27;
                    let fan_index = if forward { trail } else { trail - 27 };
                    let base_angle = 0.14 + (fan_index % 9) as f32 * 0.064;
                    let angle = if forward {
                        base_angle
                    } else {
                        base_angle + std::f32::consts::PI
                    };
                    let speed = 3.1 + (trail * 13 % 17) as f32 * 0.23;
                    let distance = 18.0 + age as f32 * speed + (trail % 5) as f32 * 4.0;
                    let length = (24.0 + age as f32 * (1.7 + (trail % 4) as f32 * 0.45)).min(105.0);
                    let position = center + Vec2::new(angle.cos(), angle.sin()) * distance;
                    let color = match trail % 5 {
                        0 => Color::linear_rgba(
                            4.8 * trail_fade,
                            4.8 * trail_fade,
                            4.2 * trail_fade,
                            0.96,
                        ),
                        1 => Color::linear_rgba(
                            0.5 * trail_fade,
                            3.2 * trail_fade,
                            4.5 * trail_fade,
                            0.88,
                        ),
                        2 => Color::linear_rgba(4.6 * trail_fade, 2.8 * trail_fade, 0.12, 0.92),
                        3 => Color::linear_rgba(3.8 * trail_fade, 0.22, 0.06, 0.86),
                        _ => Color::linear_rgba(0.18, 2.7 * trail_fade, 0.14, 0.82),
                    };
                    commands.spawn((
                        SceneVisual,
                        Sprite::from_color(
                            color,
                            Vec2::new(length, 0.8 + (trail % 3) as f32 * 0.42),
                        ),
                        Transform::from_xyz(position.x, position.y, 24.0)
                            .with_rotation(Quat::from_rotation_z(angle)),
                    ));
                }
            }
            for fragment in 0..if ice_scene { 32 } else { 18 } {
                let angle = fragment as f32 * 1.71 + 0.23;
                let speed = 0.8 + (fragment % 6) as f32 * 0.28;
                let distance = 12.0 + age as f32 * speed;
                commands.spawn((
                    SceneVisual,
                    Sprite::from_color(
                        if ice_scene && fragment % 2 == 0 {
                            Color::linear_rgba(2.4, 2.6, 2.2, 0.85)
                        } else if fragment % 3 == 0 {
                            Color::srgb_u8(92, 29, 38)
                        } else {
                            Color::linear_rgba(2.8, 0.32, 0.04, 0.9)
                        },
                        Vec2::new(3.0 + (fragment % 4) as f32 * 1.5, 2.0),
                    ),
                    Transform::from_xyz(
                        center.x + angle.cos() * distance,
                        center.y + angle.sin() * distance
                            - age as f32 * age as f32 * if ice_scene { 0.055 } else { 0.013 },
                        19.0,
                    )
                    .with_rotation(Quat::from_rotation_z(angle + age as f32 * 0.08)),
                ));
            }
        }
    }

    for player in &snapshot.players {
        if (ice_scene || hanging_scene) && !player.alive {
            continue;
        }
        let mut position = Vec2::new(player.x_milli as f32, player.y_milli as f32) / 1_000.0;
        if let (Some(origins), Some(flow)) = (snapshot.arena_entry_from_milli, &snapshot.flow) {
            let origin = origins[usize::from(player.id)];
            let origin = Vec2::new(origin[0] as f32, origin[1] as f32) / 1_000.0;
            let progress = (flow.phase_tick as f32 / 60.0).clamp(0.0, 1.0);
            position = origin.lerp(position, 1.0 - (1.0 - progress).powi(3))
                - arena_presentation_offset(snapshot);
        }
        let x = position.x;
        let y = position.y - if yellow_replay { 32.0 } else { 0.0 };
        let body_color = if player.hit_flash_ticks > 0 && !yellow_replay {
            Color::WHITE
        } else if !player.alive {
            Color::srgba_u8(96, 42, 54, 150)
        } else if player.id == 0 && (ice_scene || hanging_scene) {
            Color::srgb_u8(248, 91, 35)
        } else if player.id == 0 {
            Color::srgb_u8(244, 63, 86)
        } else {
            Color::srgb_u8(39, 166, 255)
        };
        let stride = (snapshot.tick as f32 * 0.31 + player.id as f32 * 1.7).sin() * 0.16;
        for (offset, angle) in [(-9.0, -0.38 - stride), (9.0, 0.38 + stride)] {
            commands.spawn((
                SceneVisual,
                Sprite::from_color(
                    body_color,
                    Vec2::new(7.0 * fighter_scale, 25.0 * fighter_scale),
                ),
                Transform::from_xyz(x + offset * fighter_scale, y - 28.0 * fighter_scale, 4.0)
                    .with_rotation(Quat::from_rotation_z(angle)),
            ));
        }
        commands.spawn((
            SceneVisual,
            Mesh2d(circle.clone()),
            MeshMaterial2d(materials.add(body_color)),
            Transform::from_xyz(x, y, 5.0),
        ));
        if ice_scene || hanging_scene {
            for eye_x in [-4.0, 4.0] {
                commands.spawn((
                    SceneVisual,
                    Mesh2d(meshes.add(Circle::new(2.4))),
                    MeshMaterial2d(materials.add(Color::srgb_u8(235, 239, 225))),
                    Transform::from_xyz(x + eye_x, y + 1.5, 6.0),
                ));
                commands.spawn((
                    SceneVisual,
                    Mesh2d(meshes.add(Circle::new(1.0))),
                    MeshMaterial2d(materials.add(Color::srgb_u8(7, 32, 58))),
                    Transform::from_xyz(x + eye_x, y + 1.2, 6.1),
                ));
            }
            spawn_triangle(
                commands,
                meshes,
                materials,
                [
                    Vec2::new(x - 5.0, y - 3.0),
                    Vec2::new(x, y - 7.0),
                    Vec2::new(x + 5.0, y - 3.0),
                ],
                Color::srgb_u8(224, 93, 145),
                6.0,
            );
            if player.id == 0 {
                for (offset, size, color) in [
                    (
                        Vec2::new(0.0, 17.0),
                        Vec2::new(24.0, 12.0),
                        Color::srgb_u8(168, 43, 45),
                    ),
                    (
                        Vec2::new(-3.0, 17.0),
                        Vec2::new(6.0, 12.0),
                        Color::srgb_u8(224, 223, 200),
                    ),
                    (
                        Vec2::new(0.0, 10.0),
                        Vec2::new(30.0, 4.0),
                        Color::srgb_u8(32, 35, 36),
                    ),
                ] {
                    commands.spawn((
                        SceneVisual,
                        Sprite::from_color(color, size),
                        Transform::from_xyz(x + offset.x, y + offset.y, 7.0),
                    ));
                }
            } else {
                for point in 0..3 {
                    let crown_x = x - 6.0 + point as f32 * 6.0;
                    spawn_triangle(
                        commands,
                        meshes,
                        materials,
                        [
                            Vec2::new(crown_x - 3.5, y + 32.0),
                            Vec2::new(crown_x + 3.5, y + 32.0),
                            Vec2::new(crown_x, y + 40.0),
                        ],
                        Color::srgb_u8(255, 200, 54),
                        7.0,
                    );
                }
            }
        }
        let aim = Vec2::new(f32::from(player.aim_x), f32::from(player.aim_y)).normalize_or(Vec2::X);
        if ice_scene {
            commands.spawn((
                SceneVisual,
                Mesh2d(meshes.add(Circle::new(4.5))),
                MeshMaterial2d(materials.add(Color::srgb_u8(238, 233, 213))),
                Transform::from_xyz(x + aim.x * 24.0, y + aim.y * 24.0, 8.0),
            ));
        }
        commands.spawn((
            SceneVisual,
            Sprite::from_color(
                Color::srgb_u8(35, 39, 44),
                Vec2::new(42.0 * fighter_scale, 9.0 * fighter_scale),
            ),
            Transform::from_xyz(
                x + aim.x * 25.0 * fighter_scale,
                y + aim.y * 25.0 * fighter_scale,
                7.0,
            )
            .with_rotation(Quat::from_rotation_z(aim.y.atan2(aim.x))),
        ));
        commands.spawn((
            SceneVisual,
            Sprite::from_color(
                Color::srgb_u8(22, 27, 31),
                Vec2::new(70.0 * fighter_scale, 7.0 * fighter_scale),
            ),
            Transform::from_xyz(x, y + 35.0 * fighter_scale, 8.0),
        ));
        commands.spawn((
            SceneVisual,
            Sprite::from_color(
                if ice_scene {
                    Color::srgb_u8(164, 221, 80)
                } else if player.id == 0 {
                    Color::srgb_u8(244, 90, 104)
                } else {
                    Color::srgb_u8(70, 188, 255)
                },
                Vec2::new(
                    70.0 * fighter_scale * f32::from(player.health) / 100.0,
                    5.0 * fighter_scale,
                ),
            ),
            Transform::from_xyz(
                x - (70.0 * fighter_scale
                    - 70.0 * fighter_scale * f32::from(player.health) / 100.0)
                    / 2.0,
                y + 35.0 * fighter_scale,
                9.0,
            ),
        ));
        commands.spawn((
            SceneVisual,
            Text2d::new(if ice_scene {
                if player.id == 0 {
                    "Jewish Gorgonite"
                } else {
                    "Binklederry"
                }
            } else if !player.alive {
                if player.id == 0 {
                    "ORANGE • OUT"
                } else {
                    "BLUE • OUT"
                }
            } else if player.id == 0 {
                "ORANGE"
            } else {
                "BLUE"
            }),
            TextFont {
                font_size: FontSize::Px(if ice_scene {
                    11.0
                } else {
                    12.0 * fighter_scale
                }),
                ..default()
            },
            TextColor(Color::WHITE),
            Transform::from_xyz(x, y + 49.0 * fighter_scale, 9.0),
        ));
        if player.block_ticks > 0 {
            commands.spawn((
                SceneVisual,
                Mesh2d(block_ring.clone()),
                MeshMaterial2d(materials.add(Color::srgba_u8(225, 255, 244, 210))),
                Transform::from_xyz(x, y, 10.0),
            ));
        }
    }

    for projectile in &snapshot.projectiles {
        let mut start = Vec2::new(
            projectile.previous_x_milli as f32 / 1_000.0,
            projectile.previous_y_milli as f32 / 1_000.0,
        );
        let mut end = Vec2::new(
            projectile.x_milli as f32 / 1_000.0,
            projectile.y_milli as f32 / 1_000.0,
        );
        if yellow_replay {
            start.y -= 32.0;
            end.y -= 32.0;
        }
        let segment = end - start;
        let length = segment.length().clamp(2.0, 92.0);
        commands.spawn((
            SceneVisual,
            Sprite::from_color(
                if projectile.dazzle_pulses > 0 {
                    Color::srgba_u8(255, 241, 74, 210)
                } else if projectile.explosive_radius_milli > 0 {
                    Color::srgba_u8(255, 116, 27, 220)
                } else {
                    Color::srgba_u8(255, 206, 73, 148)
                },
                Vec2::new(
                    if projectile.explosive_radius_milli > 0 {
                        length * 1.45
                    } else {
                        length
                    },
                    if projectile.dazzle_pulses > 0 {
                        5.0
                    } else {
                        3.0
                    },
                ),
            ),
            Transform::from_xyz(end.x - segment.x * 0.5, end.y - segment.y * 0.5, 12.0)
                .with_rotation(Quat::from_rotation_z(segment.y.atan2(segment.x))),
        ));
        commands.spawn((
            SceneVisual,
            Mesh2d(bullet.clone()),
            MeshMaterial2d(materials.add(Color::srgb_u8(255, 240, 143))),
            Transform::from_xyz(end.x, end.y, 13.0),
        ));
        if projectile.dazzle_pulses > 0 {
            for sparkle in 0..3 {
                let offset = Vec2::new(
                    -10.0 + sparkle as f32 * 10.0,
                    (sparkle as f32 * 2.1).sin() * 7.0,
                );
                commands.spawn((
                    SceneVisual,
                    Mesh2d(meshes.add(RegularPolygon::new(3.5, 4))),
                    MeshMaterial2d(materials.add(Color::linear_rgba(3.5, 2.6, 0.4, 0.9))),
                    Transform::from_xyz(end.x + offset.x, end.y + offset.y, 14.0)
                        .with_rotation(Quat::from_rotation_z(snapshot.tick as f32 * 0.08)),
                ));
            }
        }
    }

    if radial_replay || draft_replay {
        spawn_radial_impacts(commands, meshes, materials, snapshot);
    }

    if yellow_replay {
        spawn_yellow_hud(commands, meshes, materials, snapshot);
        spawn_yellow_result(commands, meshes, materials, snapshot);
    }

    if draft_replay || profile == ReplayProfile::MatchEndWaitingReplay {
        spawn_post_round_leadin(commands, meshes, materials, snapshot);
        spawn_flow_hud(commands, meshes, materials, snapshot);
    }
    if !yellow_replay {
        spawn_radial_result(commands, meshes, materials, snapshot);
    }
    let motion = arena_presentation_offset(snapshot);
    if motion != Vec2::ZERO {
        commands.queue(move |world: &mut World| {
            let mut transforms = world.query_filtered::<&mut Transform, With<SceneVisual>>();
            for mut transform in transforms.iter_mut(world) {
                if (-60.0..30.0).contains(&transform.translation.z) {
                    transform.translation += motion.extend(0.0);
                }
            }
        });
    }
}

pub(super) fn spawn_lime_surface(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    surface: &quarrel_sim::ArenaSurfaceSnapshot,
    tick: u32,
) {
    let center = Vec2::new(surface.center_x_milli as f32, surface.center_y_milli as f32) / 1_000.0;
    let width = surface.width_milli as f32 / 1_000.0;
    let height = surface.height_milli as f32 / 1_000.0;
    let rotation = Rot2::radians(surface.rotation_milliradians as f32 / 1_000.0);
    let outline = if surface.outline_milli.len() >= 3 {
        surface
            .outline_milli
            .iter()
            .map(|point| {
                center + rotation * (Vec2::new(point[0] as f32, point[1] as f32) / 1_000.0)
            })
            .collect::<Vec<_>>()
    } else {
        vec![
            center + Vec2::new(-width * 0.5, -height * 0.5),
            center + Vec2::new(width * 0.5, -height * 0.5),
            center + Vec2::new(width * 0.5, height * 0.5),
            center + Vec2::new(-width * 0.5, height * 0.5),
        ]
    };
    let mut levels = outline.iter().map(|point| point.y).collect::<Vec<_>>();
    levels.sort_by(f32::total_cmp);
    levels.dedup();
    let mut triangles = Vec::new();
    for band in levels.windows(2) {
        let middle = (band[0] + band[1]) * 0.5;
        let mut crossings = outline
            .iter()
            .zip(outline.iter().cycle().skip(1))
            .take(outline.len())
            .filter(|(a, b)| (a.y < middle && b.y > middle) || (b.y < middle && a.y > middle))
            .map(|(a, b)| {
                let x_at = |y| a.x + (b.x - a.x) * (y - a.y) / (b.y - a.y);
                (x_at(middle), x_at(band[0]), x_at(band[1]))
            })
            .collect::<Vec<_>>();
        crossings.sort_by(|a, b| a.0.total_cmp(&b.0));
        for pair in crossings.as_chunks::<2>().0 {
            let bottom_left = Vec2::new(pair[0].1, band[0]);
            let bottom_right = Vec2::new(pair[1].1, band[0]);
            let top_left = Vec2::new(pair[0].2, band[1]);
            let top_right = Vec2::new(pair[1].2, band[1]);
            triangles.extend([
                [bottom_left, bottom_right, top_right],
                [bottom_left, top_right, top_left],
            ]);
        }
    }

    let top = *levels.last().unwrap();
    let top_edge = outline.iter().filter(|point| (point.y - top).abs() < 0.001);
    let shadow_left = Vec2::new(
        top_edge
            .clone()
            .map(|point| point.x)
            .fold(f32::INFINITY, f32::min),
        top,
    );
    let shadow_right = Vec2::new(
        top_edge
            .map(|point| point.x)
            .fold(f32::NEG_INFINITY, f32::max),
        top,
    );
    let light = Vec2::new(0.0, 760.0);
    let shadow_end = |point: Vec2| {
        let direction = point - light;
        point + direction * ((-520.0 - point.y) / direction.y)
    };
    let far_left = shadow_end(shadow_left);
    let far_right = shadow_end(shadow_right);
    for triangle in [
        [shadow_left, far_left, far_right],
        [shadow_left, far_right, shadow_right],
    ] {
        spawn_triangle(
            commands,
            meshes,
            materials,
            triangle,
            Color::srgb_u8(0, 2, 29),
            -40.0,
        );
    }

    let phase = (tick as f32 / quarrel_sim::LIME_MODULAR_REPLAY_TICKS as f32)
        .clamp(0.0, 1.0)
        .powf(1.55);
    let (base, facets) = if surface.id < 18 {
        (
            Color::srgb_u8(
                (5.0 + phase * 160.0) as u8,
                (214.0 + phase * 40.0) as u8,
                (211.0 - phase * 193.0) as u8,
            ),
            [
                Color::srgba_u8(142, 255, 202, 65),
                Color::srgba_u8(0, 184, 219, 55),
                Color::srgba_u8(255, 255, 214, 42),
            ],
        )
    } else {
        (
            Color::srgb_u8(
                (68.0 + phase * 37.0) as u8,
                (72.0 + phase * 55.0) as u8,
                (65.0 - phase * 35.0) as u8,
            ),
            [
                Color::srgba_u8(139, 146, 83, 38),
                Color::srgba_u8(27, 60, 69, 34),
                Color::srgba_u8(196, 205, 121, 24),
            ],
        )
    };
    for &triangle in &triangles {
        commands.spawn((
            SceneVisual,
            LimeSurfaceVisual(surface.id),
            Mesh2d(meshes.add(Triangle2d::new(triangle[0], triangle[1], triangle[2]))),
            MeshMaterial2d(materials.add(base)),
            Transform::from_xyz(0.0, 0.0, 0.0),
        ));
    }
    let extent = width.hypot(height);
    for (facet, color) in facets.into_iter().enumerate() {
        let angle = 0.55 + facet as f32 * 1.13 + surface.id as f32 * 0.37 + tick as f32 * 0.002;
        let direction = Vec2::from_angle(angle);
        let normal = direction.perp();
        let offset = radial_brush_noise(u32::from(surface.id), facet as u32, 59);
        let origin = center + normal * offset * extent * 0.22;
        let paint = [
            origin - direction * extent * 0.72 - normal * extent * 0.16,
            origin + direction * extent * 0.65,
            origin - direction * extent * 0.18 + normal * extent * 0.24,
        ];
        for triangle in &triangles {
            let clipped = clip_ice_paint(&paint, triangle);
            for index in 1..clipped.len().saturating_sub(1) {
                spawn_triangle(
                    commands,
                    meshes,
                    materials,
                    [clipped[0], clipped[index], clipped[index + 1]],
                    color,
                    0.5 + facet as f32 * 0.01,
                );
            }
        }
    }
}

pub(super) fn backdrop_panel_offset(tick: u32, index: usize, held_hanging_entry: bool) -> f32 {
    if held_hanging_entry {
        return 0.0;
    }
    (tick as f32 * 0.035 + index as f32 * 1.7).sin() * 28.0
}

pub(super) fn spawn_post_round_leadin(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    let Some(flow) = &snapshot.flow else {
        return;
    };
    if !matches!(flow.phase, FlowPhase::RoundBlue | FlowPhase::RoundOrange) || flow.phase_tick < 124
    {
        return;
    }
    let rise = ((flow.phase_tick - 124) as f32 / 14.0).clamp(0.0, 1.0);
    let item = flow
        .catalog
        .iter()
        .find(|item| item.id == quarrel_sim::ItemId::QuickShot)
        .expect("quick shot is registered");
    spawn_card(
        commands,
        meshes,
        materials,
        item,
        Vec2::new(-455.0, -250.0 + rise * 270.0),
        -0.16,
        flow.phase_tick >= 138,
        false,
        34.0,
        CardPresentation {
            item: quarrel_sim::ItemId::QuickShot,
            highlighted: false,
            selected_offscreen: false,
        },
    );
    if flow.phase_tick >= 138 {
        for (index, x) in [-245.0_f32, -55.0].into_iter().enumerate() {
            commands.spawn((
                SceneVisual,
                CaptureElement::Card,
                Sprite::from_color(Color::srgb_u8(13, 16, 25), Vec2::new(157.0, 250.0)),
                Transform::from_xyz(x, -8.0 + index as f32 * 8.0, 35.0)
                    .with_rotation(Quat::from_rotation_z(-0.06 + index as f32 * 0.08)),
            ));
        }
    }
}

pub(super) fn arena_presentation_offset(snapshot: &MatchSnapshot) -> Vec2 {
    let Some(flow) = &snapshot.flow else {
        return Vec2::ZERO;
    };
    if flow.phase == FlowPhase::IceTransition {
        let remaining = (1.0 - flow.phase_tick as f32 / 60.0).clamp(0.0, 1.0);
        return Vec2::new(1_206.0 * remaining.powi(3), 0.0);
    }
    if matches!(flow.phase, FlowPhase::RoundBlue | FlowPhase::RoundOrange) {
        let progress = (flow.phase_tick as f32 / 85.0).clamp(0.0, 1.0);
        return Vec2::new(-7.0 * progress, 28.0 + 1_000.0 * progress.powi(2));
    }
    Vec2::ZERO
}

pub(super) fn spawn_hanging_entry(
    commands: &mut Commands,
    _materials: &mut Assets<ColorMaterial>,
    hanging: &quarrel_sim::HangingEntryPresentation,
) {
    let body_color = Color::srgb_u8(
        hanging.body_rgb[0],
        hanging.body_rgb[1],
        hanging.body_rgb[2],
    );
    let rim_color = Color::srgb_u8(
        hanging.square_rim_rgb[0],
        hanging.square_rim_rgb[1],
        hanging.square_rim_rgb[2],
    );
    let opening_color = Color::srgb_u8(
        hanging.square_opening_rgb[0],
        hanging.square_opening_rgb[1],
        hanging.square_opening_rgb[2],
    );
    let link_color = Color::srgb_u8(
        hanging.link_rgb[0],
        hanging.link_rgb[1],
        hanging.link_rgb[2],
    );
    for body in &hanging.bodies {
        let body_center = Vec2::new(
            body.body_x_milli as f32 / 1_000.0,
            body.body_y_milli as f32 / 1_000.0,
        );
        let square_center = Vec2::new(
            body.square_x_milli as f32 / 1_000.0,
            body.square_y_milli as f32 / 1_000.0,
        );
        for (segment_index, (start, end)) in [
            (
                Vec2::new(square_center.x, body.ceiling_y_milli as f32 / 1_000.0),
                square_center,
            ),
            (
                square_center,
                Vec2::new(body_center.x, body.body_top_y_milli as f32 / 1_000.0),
            ),
        ]
        .into_iter()
        .enumerate()
        {
            let segment = end - start;
            commands.spawn((
                SceneVisual,
                HangingLinkVisual {
                    body: body.id,
                    segment: segment_index as u8,
                },
                Sprite::from_color(link_color, Vec2::new(segment.length(), 2.0)),
                Transform::from_xyz(start.x + segment.x * 0.5, start.y + segment.y * 0.5, -5.0)
                    .with_rotation(Quat::from_rotation_z(segment.y.atan2(segment.x))),
            ));
        }
        commands.spawn((
            SceneVisual,
            HangingBodyVisual(body.id),
            Sprite::from_color(
                body_color,
                Vec2::new(
                    body.body_width_milli as f32 / 1_000.0,
                    body.body_height_milli as f32 / 1_000.0,
                ),
            ),
            Transform::from_xyz(body_center.x, body_center.y, -2.0),
        ));
        let square_size = body.square_size_milli as f32 / 1_000.0;
        commands.spawn((
            SceneVisual,
            HangingSquareVisual(body.id),
            Sprite::from_color(rim_color, Vec2::splat(square_size)),
            Transform::from_xyz(square_center.x, square_center.y, -1.0),
        ));
        commands.spawn((
            SceneVisual,
            Sprite::from_color(
                opening_color,
                Vec2::splat(body.square_opening_milli as f32 / 1_000.0),
            ),
            Transform::from_xyz(square_center.x, square_center.y, 0.0),
        ));
    }
}

pub(super) fn spawn_ice_surface(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    surface: &quarrel_sim::ArenaSurfaceSnapshot,
    tick: u32,
) {
    let center = Vec2::new(surface.center_x_milli as f32, surface.center_y_milli as f32) / 1_000.0;
    let rotation = Rot2::radians(surface.rotation_milliradians as f32 / 1_000.0);
    let outline = surface
        .outline_milli
        .iter()
        .map(|point| center + rotation * (Vec2::new(point[0] as f32, point[1] as f32) / 1_000.0))
        .collect::<Vec<_>>();
    if outline.len() < 3 {
        return;
    }
    // Horizontal bands triangulate the stepped and notched contours without filling their recesses.
    let mut levels = outline.iter().map(|point| point.y).collect::<Vec<_>>();
    levels.sort_by(f32::total_cmp);
    levels.dedup();
    let mut triangles = Vec::new();
    for band in levels.windows(2) {
        let middle = (band[0] + band[1]) * 0.5;
        let mut crossings = outline
            .iter()
            .zip(outline.iter().cycle().skip(1))
            .take(outline.len())
            .filter(|(a, b)| (a.y < middle && b.y > middle) || (b.y < middle && a.y > middle))
            .map(|(a, b)| {
                let x_at = |y| a.x + (b.x - a.x) * (y - a.y) / (b.y - a.y);
                (x_at(middle), x_at(band[0]), x_at(band[1]))
            })
            .collect::<Vec<_>>();
        crossings.sort_by(|a, b| a.0.total_cmp(&b.0));
        for pair in crossings.as_chunks::<2>().0 {
            let bottom_left = Vec2::new(pair[0].1, band[0]);
            let bottom_right = Vec2::new(pair[1].1, band[0]);
            let top_left = Vec2::new(pair[0].2, band[1]);
            let top_right = Vec2::new(pair[1].2, band[1]);
            triangles.extend([
                [bottom_left, bottom_right, top_right],
                [bottom_left, top_right, top_left],
            ]);
        }
    }
    let top = *levels.last().unwrap();
    let width = surface.width_milli as f32 / 1_000.0;
    let top_edge = outline.iter().filter(|point| (point.y - top).abs() < 0.001);
    let shadow_left = Vec2::new(
        top_edge
            .clone()
            .map(|point| point.x)
            .fold(f32::INFINITY, f32::min),
        top,
    );
    let shadow_right = Vec2::new(
        top_edge
            .map(|point| point.x)
            .fold(f32::NEG_INFINITY, f32::max),
        top,
    );
    let light = Vec2::new(0.0, 760.0);
    let shadow_end = |point: Vec2| {
        let direction = point - light;
        point + direction * ((-520.0 - point.y) / direction.y)
    };
    let far_left = shadow_end(shadow_left);
    let far_right = shadow_end(shadow_right);
    for triangle in [
        [shadow_left, far_left, far_right],
        [shadow_left, far_right, shadow_right],
    ] {
        spawn_triangle(
            commands,
            meshes,
            materials,
            triangle,
            Color::srgb_u8(0, 0, 27),
            -40.0,
        );
    }
    for &triangle in &triangles {
        spawn_triangle(
            commands,
            meshes,
            materials,
            triangle,
            Color::srgb_u8(221, 233, 226),
            0.0,
        );
    }
    let height = surface.height_milli as f32 / 1_000.0;
    let phase = tick as f32 * 0.007 + surface.id as f32 * 1.71;
    let extent = width.hypot(height);
    for stroke in 0..36_u32 {
        let cluster = stroke / 9;
        let seed = u32::from(surface.id) * 31 + cluster * 97;
        let cyan = (0.5 + (phase * 0.63 + cluster as f32 * 1.7).sin() * 0.7).clamp(0.0, 1.0)
            * if center.y > 0.0 { 0.25 } else { 1.0 };
        let direction =
            Vec2::from_angle(phase.sin() * 1.2 + 0.7 + radial_brush_noise(seed, 0, 17) * 0.9);
        let normal = direction.perp();
        let origin = center
            + Vec2::new(
                radial_brush_noise(seed, 0, 3) * width * 0.52,
                radial_brush_noise(seed, 0, 7) * height * 0.52,
            )
            + normal * radial_brush_noise(seed, stroke, 11) * extent * 0.13;
        let broad = stroke % 9 == 0;
        let thickness = if broad {
            extent * (0.04 + cyan * 0.08)
        } else {
            (1.0 + (stroke * 13 % 19) as f32) * (0.3 + cyan * 0.7)
        };
        let length = extent * (0.14 + radial_brush_noise(seed, stroke, 23).abs() * 0.65);
        let paint = [
            origin - direction * length * 0.7,
            origin + direction * length * 0.6 + normal * thickness,
            origin + direction * length * 0.1 - normal * thickness * 0.45,
        ];
        let color = if broad || stroke % 4 == 0 {
            Color::srgba_u8(0, 188, 226, (20.0 + cyan * 230.0) as u8)
        } else if stroke % 3 == 0 || stroke % 5 == 0 {
            Color::srgba_u8(243, 249, 232, 225)
        } else {
            Color::srgba_u8(28, 203, 230, (12.0 + cyan * 185.0) as u8)
        };
        for triangle in &triangles {
            let clipped = clip_ice_paint(&paint, triangle);
            for index in 1..clipped.len().saturating_sub(1) {
                spawn_triangle(
                    commands,
                    meshes,
                    materials,
                    [clipped[0], clipped[index], clipped[index + 1]],
                    color,
                    0.5 + stroke as f32 * 0.001,
                );
            }
        }
    }
}
