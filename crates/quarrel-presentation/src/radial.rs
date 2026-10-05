use super::*;
use super::{draft::spawn_left_half_disc, hud::spawn_triangle};

pub(super) fn clip_ice_paint(paint: &[Vec2; 3], contour: &[Vec2; 3]) -> Vec<Vec2> {
    let mut polygon = paint.to_vec();
    for edge in 0..3 {
        let start = contour[edge];
        let direction = contour[(edge + 1) % 3] - start;
        let input = std::mem::take(&mut polygon);
        for (&a, &b) in input
            .iter()
            .zip(input.iter().cycle().skip(1))
            .take(input.len())
        {
            let side_a = direction.perp_dot(a - start);
            let side_b = direction.perp_dot(b - start);
            if side_a >= 0.0 {
                polygon.push(a);
            }
            if (side_a >= 0.0) != (side_b >= 0.0) {
                polygon.push(a.lerp(b, side_a / (side_a - side_b)));
            }
        }
    }
    polygon
}

pub(super) fn spawn_radial_backdrop(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    tick: u32,
    arena: &[quarrel_sim::ArenaSurfaceSnapshot],
) {
    let phase = tick as f32 / 60.0;
    for stroke in 0..82_u32 {
        let lane = stroke % 13;
        let row = stroke / 13;
        let motion = (phase * (0.11 + row as f32 * 0.017) + stroke as f32 * 1.731).sin();
        let center = Vec2::new(
            -700.0 + lane as f32 * 116.0 + motion * (18.0 + (stroke % 5) as f32 * 3.0),
            -390.0 + row as f32 * 137.0 + (phase * 0.09 + stroke as f32 * 0.913).cos() * 24.0,
        );
        let rotation =
            -0.72 + (stroke % 11) as f32 * 0.137 + (phase * 0.07 + stroke as f32).sin() * 0.035;
        let tangent = Vec2::new(rotation.cos(), rotation.sin());
        let normal = Vec2::new(-tangent.y, tangent.x);
        let length = 210.0 + (stroke.wrapping_mul(83) % 390) as f32;
        let thickness = 12.0 + (stroke.wrapping_mul(47) % 61) as f32;
        let color = if stroke % 4 == 0 {
            Color::srgba_u8(4, 172, 210, 155)
        } else if stroke % 7 == 0 {
            Color::srgba_u8(35, 208, 226, 185)
        } else {
            Color::srgba_u8(250, 255, 249, 205 + (stroke % 3) as u8 * 18)
        };
        for segment in 0..5_u32 {
            let along_a = -0.5 + segment as f32 / 5.0;
            let along_b = -0.5 + (segment + 1) as f32 / 5.0;
            let taper_a = (1.0 - (along_a.abs() * 1.42).powi(3)).clamp(0.15, 1.0);
            let taper_b = (1.0 - (along_b.abs() * 1.42).powi(3)).clamp(0.15, 1.0);
            let jitter_a = radial_brush_noise(stroke, segment, 3) * thickness * 0.34;
            let jitter_b = radial_brush_noise(stroke, segment + 1, 3) * thickness * 0.34;
            let half_a =
                thickness * taper_a * (0.32 + radial_brush_noise(stroke, segment, 7).abs() * 0.45);
            let half_b = thickness
                * taper_b
                * (0.32 + radial_brush_noise(stroke, segment + 1, 7).abs() * 0.45);
            let spine_a = center + tangent * length * along_a + normal * jitter_a;
            let spine_b = center + tangent * length * along_b + normal * jitter_b;
            let top_a = spine_a + normal * half_a;
            let bottom_a = spine_a - normal * half_a * 0.78;
            let top_b = spine_b + normal * half_b;
            let bottom_b = spine_b - normal * half_b * 0.78;
            spawn_triangle(
                commands,
                meshes,
                materials,
                [top_a, bottom_a, top_b],
                color,
                -82.0,
            );
            spawn_triangle(
                commands,
                meshes,
                materials,
                [bottom_a, bottom_b, top_b],
                color,
                -82.0,
            );
        }
        if stroke % 3 == 0 {
            for bristle in 0..3_u32 {
                let offset = -0.42 + bristle as f32 * 0.14;
                let start = center + tangent * length * offset + normal * thickness * 0.7;
                let end = start
                    + tangent * length * (0.24 + bristle as f32 * 0.035)
                    + normal * radial_brush_noise(stroke, bristle, 19) * 9.0;
                spawn_triangle(
                    commands,
                    meshes,
                    materials,
                    [start, end, end - normal * (2.0 + bristle as f32)],
                    color,
                    -81.0,
                );
            }
        }
    }

    let dark = Color::srgb_u8(3, 23, 51);
    let left_top = Vec2::new(-112.0, 350.0);
    let left_notch = Vec2::new(-18.0, 304.0);
    let left = Vec2::new(-448.0, 0.0);
    let left_bottom = Vec2::new(-104.0, -350.0);
    let right_top = Vec2::new(112.0, 350.0);
    let right_notch = Vec2::new(18.0, 304.0);
    let right = Vec2::new(448.0, 0.0);
    let right_bottom = Vec2::new(104.0, -350.0);
    for triangle in [
        [Vec2::ZERO, left_top, left_notch],
        [Vec2::ZERO, left, left_top],
        [Vec2::ZERO, left_bottom, left],
        [Vec2::ZERO, right_notch, right_top],
        [Vec2::ZERO, right_top, right],
        [Vec2::ZERO, right, right_bottom],
        [Vec2::ZERO, right_bottom, left_bottom],
    ] {
        spawn_triangle(commands, meshes, materials, triangle, dark, -55.0);
    }
    for surface in arena.iter().filter(|surface| surface.id >= 6) {
        commands.spawn((
            SceneVisual,
            Sprite::from_color(
                dark,
                Vec2::new(
                    surface.width_milli as f32 / 1_000.0,
                    surface.height_milli as f32 / 1_000.0,
                ),
            ),
            Transform::from_xyz(
                surface.center_x_milli as f32 / 1_000.0,
                surface.center_y_milli as f32 / 1_000.0,
                -54.0,
            )
            .with_rotation(Quat::from_rotation_z(
                surface.rotation_milliradians as f32 / 1_000.0,
            )),
        ));
    }
}

pub(super) fn radial_brush_noise(stroke: u32, point: u32, salt: u32) -> f32 {
    ((stroke.wrapping_mul(73) + point.wrapping_mul(41) + salt.wrapping_mul(29)) as f32 * 0.137)
        .sin()
}

pub(super) fn spawn_radial_surface_finish(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    center: Vec2,
    size: Vec2,
    rotation: f32,
    id: u8,
) {
    let tangent = Vec2::new(rotation.cos(), rotation.sin());
    let normal = Vec2::new(-tangent.y, tangent.x);
    let left = center - tangent * size.x * 0.5 - normal * size.y * 0.5;
    let right = center + tangent * size.x * 0.5 - normal * size.y * 0.5;
    let shadow_offset = Vec2::new(88.0, -430.0);
    for triangle in [
        [left, right, right + shadow_offset],
        [left, right + shadow_offset, left + shadow_offset],
    ] {
        spawn_triangle(
            commands,
            meshes,
            materials,
            triangle,
            Color::srgba_u8(0, 11, 39, 205),
            -18.0,
        );
    }
    let tint = if id.is_multiple_of(2) {
        Color::srgba_u8(255, 255, 255, 92)
    } else {
        Color::srgba_u8(48, 203, 226, 105)
    };
    spawn_triangle(
        commands,
        meshes,
        materials,
        [
            left + normal * size.y * 0.44,
            right + normal * size.y * 0.44,
            center,
        ],
        tint,
        1.0,
    );
}

pub(super) fn spawn_radial_saw(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    saw: &quarrel_sim::SawSnapshot,
) {
    let center = Vec2::new(saw.x_milli as f32 / 1_000.0, saw.y_milli as f32 / 1_000.0);
    let angle = saw.angle_milliradians as f32 / 1_000.0;
    let radius = saw.radius_milli as f32 / 1_000.0;
    let teeth = usize::from(saw.teeth);
    for tooth in 0..teeth {
        let mid = angle + tooth as f32 * std::f32::consts::TAU / teeth as f32;
        let half = std::f32::consts::PI / teeth as f32;
        let root_a = center + Vec2::from_angle(mid - half) * radius * 0.72;
        let shoulder_a = center + Vec2::from_angle(mid - half * 0.38) * radius * 0.86;
        let tip = center + Vec2::from_angle(mid) * radius;
        let shoulder_b = center + Vec2::from_angle(mid + half * 0.38) * radius * 0.86;
        let root_b = center + Vec2::from_angle(mid + half) * radius * 0.72;
        for vertices in [
            [center, root_a, shoulder_a],
            [center, shoulder_a, tip],
            [center, tip, shoulder_b],
            [center, shoulder_b, root_b],
        ] {
            spawn_triangle(
                commands,
                meshes,
                materials,
                vertices,
                Color::srgb_u8(251, 47, 82),
                3.0,
            );
        }
    }
    commands.spawn((
        SceneVisual,
        CaptureElement::Saw,
        Mesh2d(meshes.add(Circle::new(radius * 0.36))),
        MeshMaterial2d(materials.add(Color::srgb_u8(2, 18, 43))),
        Transform::from_xyz(center.x, center.y, 4.0),
    ));
    commands.spawn((
        SceneVisual,
        Mesh2d(meshes.add(Circle::new(radius * 0.15))),
        MeshMaterial2d(materials.add(Color::srgb_u8(249, 46, 81))),
        Transform::from_xyz(center.x, center.y, 5.0),
    ));
}

pub(super) fn spawn_radial_impacts(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    for impact in &snapshot.impacts {
        let age = snapshot.tick.saturating_sub(impact.tick);
        if age > 34 {
            continue;
        }
        let center = Vec2::new(
            impact.x_milli as f32 / 1_000.0,
            impact.y_milli as f32 / 1_000.0,
        );
        let fade = 1.0 - age as f32 / 35.0;
        for cloud in 0..9 {
            let theta = cloud as f32 * 2.399;
            let distance = 5.0 + age as f32 * (0.35 + (cloud % 4) as f32 * 0.12);
            commands.spawn((
                SceneVisual,
                Mesh2d(meshes.add(Circle::new((8.0 + (cloud % 4) as f32 * 3.0) * fade))),
                MeshMaterial2d(materials.add(Color::srgba(0.92, 1.0, 1.0, 0.72 * fade))),
                Transform::from_xyz(
                    center.x + theta.cos() * distance,
                    center.y + theta.sin() * distance,
                    18.0,
                ),
            ));
        }
        for spark in 0..22 {
            let theta = spark as f32 * 2.399 + impact.owner as f32 * 0.4;
            let distance = 12.0 + age as f32 * (0.9 + (spark % 6) as f32 * 0.24);
            let color = Color::linear_rgba(3.8 * fade, 0.55 * fade, 0.08, 0.9 * fade);
            commands.spawn((
                SceneVisual,
                Sprite::from_color(color, Vec2::new(11.0 * fade.max(0.2), 2.0)),
                Transform::from_xyz(
                    center.x + theta.cos() * distance,
                    center.y + theta.sin() * distance - age as f32 * age as f32 * 0.01,
                    19.0,
                )
                .with_rotation(Quat::from_rotation_z(theta)),
            ));
        }
    }
}

pub(super) fn spawn_radial_result(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    if snapshot.hanging_entry.is_some() {
        return;
    }
    let Some(round) = &snapshot.round else {
        return;
    };
    if matches!(round.phase, quarrel_sim::RoundPhase::Combat) {
        return;
    }
    let ice = snapshot.arena.iter().any(|surface| surface.id >= 40);
    let full_round = snapshot.flow.is_some()
        && round
            .winner
            .is_some_and(|winner| round.scores[usize::from(winner)] >= 2);
    let established = matches!(
        round.phase,
        quarrel_sim::RoundPhase::HalfBlue
            | quarrel_sim::RoundPhase::HalfOrange
            | quarrel_sim::RoundPhase::ArenaTransition
            | quarrel_sim::RoundPhase::RoundBlue
            | quarrel_sim::RoundPhase::RoundOrange
    );
    let transition_alpha = if round.phase == quarrel_sim::RoundPhase::ArenaTransition {
        (1.0 - round.phase_tick as f32 / 30.0).clamp(0.0, 1.0)
    } else {
        1.0
    };
    let dim = if ice || full_round {
        0.72
    } else if established {
        0.82
    } else {
        (0.68 + round.phase_tick as f32 / 120.0).clamp(0.68, 0.82)
    };
    commands.spawn((
        SceneVisual,
        Sprite::from_color(
            Color::srgba(0.0, 0.025, 0.08, dim * transition_alpha),
            Vec2::new(1_280.0, 720.0),
        ),
        Transform::from_xyz(0.0, 0.0, 40.0),
    ));
    let scale = if established {
        1.0
    } else if full_round {
        0.75 + (round.phase_tick as f32 / 16.0).clamp(0.0, 1.0) * 0.25
    } else {
        0.44 + (round.phase_tick as f32 / 29.0).clamp(0.0, 1.0) * 0.56
    };
    for player in 0..2 {
        let color = if player == 0 {
            Color::srgb_u8(255, 169, 18)
        } else {
            Color::srgb_u8(35, 184, 255)
        }
        .with_alpha(transition_alpha);
        let mut center = if ice || full_round {
            Vec2::new(-100.0 + player as f32 * 200.0, 0.0)
        } else {
            Vec2::new(-96.0 + player as f32 * 192.0, -22.0)
        };
        let mut radius = 67.0 * scale;
        if full_round && established {
            let travel = (round.phase_tick.saturating_sub(40) as f32 / 65.0).clamp(0.0, 1.0);
            if round.winner == Some(player as u8) {
                center = center.lerp(Vec2::new(-612.0, 332.0 - player as f32 * 42.0), travel);
                radius = radius * (1.0 - travel) + 7.0 * travel;
            } else {
                center = center.lerp(Vec2::new(0.0, -323.0), travel);
            }
        }
        commands.spawn((
            SceneVisual,
            Mesh2d(meshes.add(Circle::new(radius - 2.0))),
            MeshMaterial2d(materials.add(Color::srgba_u8(
                0,
                12,
                35,
                (230.0 * transition_alpha) as u8,
            ))),
            Transform::from_xyz(center.x, center.y, 42.0),
        ));
        commands.spawn((
            SceneVisual,
            Mesh2d(meshes.add(Annulus::new(radius - 2.0, radius))),
            MeshMaterial2d(materials.add(color)),
            Transform::from_xyz(center.x, center.y, 43.0),
        ));
        if full_round && established && round.winner == Some(player as u8) {
            commands.spawn((
                SceneVisual,
                Mesh2d(meshes.add(Circle::new(radius - 1.0))),
                MeshMaterial2d(materials.add(color)),
                Transform::from_xyz(center.x, center.y, 43.0),
            ));
        } else if (round.scores[player] > 0
            && (snapshot.flow.is_none() || established || round.winner != Some(player as u8)))
            || (full_round && round.winner == Some(player as u8))
        {
            spawn_left_half_disc(
                commands,
                meshes,
                materials,
                center,
                radius - 3.0,
                color,
                43.0,
            );
        }
    }
    if established {
        let (label, color) = if round.winner == Some(0) {
            (
                if full_round {
                    "ROUND ORANGE"
                } else {
                    "HALF ORANGE"
                },
                if ice || full_round {
                    Color::srgb_u8(255, 203, 95)
                } else {
                    Color::srgb_u8(255, 183, 44)
                },
            )
        } else {
            (
                if full_round {
                    "ROUND BLUE"
                } else {
                    "HALF BLUE"
                },
                if ice || full_round {
                    Color::srgb_u8(160, 220, 245)
                } else {
                    Color::srgb_u8(101, 220, 255)
                },
            )
        };
        commands.spawn((
            SceneVisual,
            Text2d::new(label),
            TextFont {
                font_size: FontSize::Px(if ice || full_round { 66.0 } else { 70.0 }),
                font: if ice || full_round {
                    ROUND_FONT_HANDLE.into()
                } else {
                    default()
                },
                ..default()
            },
            TextColor(color.with_alpha(transition_alpha)),
            Transform::from_xyz(0.0, if ice || full_round { 134.0 } else { 88.0 }, 44.0),
        ));
    }
}
