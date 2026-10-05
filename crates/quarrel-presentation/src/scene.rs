use super::*;
use crate::{hud::spawn_triangle, post_process::RadialEchoSettings};
use bevy::camera::{OrthographicProjection, Projection, ScalingMode, Viewport};

pub(super) fn camera_viewport(snapshot: &MatchSnapshot, target: UVec2) -> Option<Viewport> {
    if target.x == 0 || target.y == 0 {
        return None;
    }
    snapshot
        .arena_objects
        .as_ref()
        .map(|arena| data_arena_viewport(arena.frame, target))
}

fn data_arena_viewport(frame: [f32; 4], target: UVec2) -> Viewport {
    let frame_size = Vec2::new(frame[2] - frame[0], frame[3] - frame[1]);
    let scale = (target.x as f32 / frame_size.x).min(target.y as f32 / frame_size.y);
    let size = (frame_size * scale).floor().as_uvec2().max(UVec2::ONE);
    Viewport {
        physical_position: (target - size) / 2,
        physical_size: size,
        ..default()
    }
}

pub(super) fn camera_projection(snapshot: &MatchSnapshot) -> Projection {
    let Some(arena) = &snapshot.arena_objects else {
        return Projection::Orthographic(OrthographicProjection::default_2d());
    };
    Projection::Orthographic(OrthographicProjection {
        scaling_mode: ScalingMode::Fixed {
            width: arena.frame[2] - arena.frame[0],
            height: arena.frame[3] - arena.frame[1],
        },
        ..OrthographicProjection::default_2d()
    })
}

pub(super) fn camera_state(
    snapshot: &MatchSnapshot,
) -> (Transform, Bloom, ChromaticAberration, LensDistortion) {
    if let Some(arena) = &snapshot.arena_objects {
        return (
            Transform::from_xyz(
                (arena.frame[0] + arena.frame[2]) * 0.5,
                (arena.frame[1] + arena.frame[3]) * 0.5,
                0.0,
            ),
            Bloom::NATURAL,
            ChromaticAberration {
                intensity: 0.0,
                ..default()
            },
            LensDistortion {
                intensity: 0.0,
                ..default()
            },
        );
    }
    let player_nudge = snapshot
        .players
        .iter()
        .map(|player| player.velocity_x_milli_per_second)
        .sum::<i32>() as f32
        / 600_000.0;
    let explosion_age = snapshot
        .explosions
        .last()
        .filter(|_| {
            snapshot
                .round
                .as_ref()
                .is_none_or(|round| round.phase == quarrel_sim::RoundPhase::Combat)
        })
        .map(|explosion| snapshot.tick.saturating_sub(explosion.tick));
    let yellow = snapshot.profile == quarrel_sim::YELLOW_REPLAY_PROFILE;
    let ice = snapshot.arena.iter().any(|surface| surface.id >= 40);
    let flash = explosion_age
        .map(|age| {
            if ice {
                yellow_flash_envelope(age + 3)
            } else if yellow {
                yellow_flash_envelope(age)
            } else {
                flash_envelope(age)
            }
        })
        .unwrap_or(0.0);
    let shock = if ice {
        0.0
    } else if yellow {
        radial_echo_settings(snapshot).strength
    } else {
        explosion_age.map(shock_envelope).unwrap_or(0.0)
    };
    let shake_x = (snapshot.tick as f32 * 2.31).sin() * (5.0 * flash + 8.0 * shock);
    let shake_y = if ice {
        explosion_age
            .map(|age| 11.0 * (1.0 - age as f32 / 3.0).clamp(0.0, 1.0))
            .unwrap_or(0.0)
    } else {
        (snapshot.tick as f32 * 1.73).cos() * (4.0 * flash + 6.0 * shock)
    };
    let transform = Transform::from_xyz(
        player_nudge.clamp(-5.0, 5.0) + shake_x,
        if yellow { 48.0 } else { 0.0 } + shake_y,
        0.0,
    );
    let bloom = Bloom {
        intensity: if yellow {
            0.10 + flash * 0.12 + shock * 0.03
        } else {
            0.12 + flash * 0.25 + shock * 0.12
        },
        ..Bloom::NATURAL
    };
    let chromatic = ChromaticAberration {
        intensity: if yellow {
            flash * 0.012 + shock * 0.018
        } else {
            flash * 0.025 + shock * 0.115
        },
        max_samples: if yellow { 8 } else { 20 },
        ..default()
    };
    let lens = LensDistortion {
        intensity: if ice {
            0.0
        } else if yellow {
            flash * -0.025 + shock * -0.045
        } else {
            flash * -0.04 + shock * -0.30
        },
        scale: if ice {
            1.0
        } else if yellow {
            1.0 + flash * 0.008 + shock * 0.018
        } else {
            1.0 + flash * 0.015 + shock * 0.10
        },
        ..default()
    };
    (transform, bloom, chromatic, lens)
}

pub(super) fn spawn_yellow_hud(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    let scores = snapshot
        .round
        .as_ref()
        .map(|round| round.scores)
        .unwrap_or([2, 1]);
    for player in 0..2_u8 {
        let y = 378.0 - player as f32 * 30.0;
        let color = if player == 0 {
            Color::srgb_u8(255, 185, 37)
        } else {
            Color::srgb_u8(83, 196, 255)
        };
        for index in 0..5_u8 {
            let filled = index < scores[usize::from(player)];
            commands.spawn((
                SceneVisual,
                HudScorePip {
                    player,
                    index,
                    filled,
                },
                Mesh2d(meshes.add(Circle::new(if filled { 5.0 } else { 2.1 }))),
                MeshMaterial2d(materials.add(if filled {
                    color
                } else {
                    Color::srgba_u8(210, 229, 220, 105)
                })),
                Transform::from_xyz(-608.0 + index as f32 * 21.0, y, 32.0),
            ));
        }
    }
}

pub(super) fn spawn_yellow_result(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    let Some(round) = &snapshot.round else { return };
    if round.phase == quarrel_sim::RoundPhase::Combat {
        return;
    }
    let established = round.phase == quarrel_sim::RoundPhase::RoundOrange;
    let transition_scale = (0.45 + round.phase_tick as f32 * 0.55).min(1.0);
    commands.spawn((
        SceneVisual,
        Sprite::from_color(
            Color::srgba(0.0, 0.02, 0.065, 0.84),
            Vec2::new(1_500.0, 900.0),
        ),
        Transform::from_xyz(0.0, 0.0, 40.0),
    ));
    for player in 0..2 {
        let center = Vec2::new(-102.0 + player as f32 * 200.0, 37.0);
        let scale = if established && player == 0 {
            (1.0 - round.phase_tick.saturating_sub(22) as f32 * 0.024).clamp(0.43, 1.0)
        } else if !established {
            transition_scale
        } else {
            1.0
        };
        let color = if player == 0 {
            Color::srgb_u8(255, 167, 24)
        } else {
            Color::srgb_u8(38, 184, 255)
        };
        commands.spawn((
            SceneVisual,
            Mesh2d(meshes.add(Annulus::new(54.0 * scale, 58.0 * scale))),
            MeshMaterial2d(materials.add(color)),
            Transform::from_xyz(center.x, center.y, 43.0),
        ));
        if round.scores[player] >= 2 && (established || player != 0) {
            commands.spawn((
                SceneVisual,
                Mesh2d(meshes.add(Circle::new(51.0 * scale))),
                MeshMaterial2d(materials.add(Color::srgba(
                    color.to_linear().red,
                    color.to_linear().green,
                    color.to_linear().blue,
                    if player == 0 { 0.9 } else { 0.38 },
                ))),
                Transform::from_xyz(center.x, center.y, 42.5),
            ));
        }
        if !established && player == 0 && round.scores[player] >= 2 {
            spawn_half_disc(
                commands,
                meshes,
                materials,
                center,
                color,
                51.0 * scale,
                42.5,
            );
        }
    }
    if established {
        commands.spawn((
            SceneVisual,
            Text2d::new("ROUND ORANGE"),
            TextFont {
                font_size: FontSize::Px(68.0),
                ..default()
            },
            TextColor(Color::srgb_u8(255, 183, 44)),
            Transform::from_xyz(-130.0, 216.0, 44.0),
        ));
    }
}

pub(super) fn spawn_half_disc(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    center: Vec2,
    color: Color,
    radius: f32,
    z: f32,
) {
    let fill = Color::srgba(
        color.to_linear().red,
        color.to_linear().green,
        color.to_linear().blue,
        0.9,
    );
    for segment in 0..12 {
        let first = std::f32::consts::FRAC_PI_2 + segment as f32 * std::f32::consts::PI / 12.0;
        let second =
            std::f32::consts::FRAC_PI_2 + (segment + 1) as f32 * std::f32::consts::PI / 12.0;
        spawn_triangle(
            commands,
            meshes,
            materials,
            [
                center,
                center + Vec2::new(first.cos(), first.sin()) * radius,
                center + Vec2::new(second.cos(), second.sin()) * radius,
            ],
            fill,
            z,
        );
    }
    commands.spawn((
        SceneVisual,
        Sprite::from_color(color, Vec2::new(2.0, radius * 2.0)),
        Transform::from_xyz(center.x, center.y, z + 0.1),
    ));
}

pub(super) fn flash_envelope(age: u32) -> f32 {
    (1.0 - age as f32 / 36.0).clamp(0.0, 1.0)
}

pub(super) fn yellow_flash_envelope(age: u32) -> f32 {
    match age {
        0 => 0.015,
        1 => 0.22,
        2 => 0.58,
        3 => 1.0,
        4..=8 => 1.0 - (age - 3) as f32 * 0.14,
        9..=18 => 0.30 - (age - 8) as f32 * 0.015,
        19..=29 => 0.15 - (age - 18) as f32 * 0.012,
        _ => 0.0,
    }
    .clamp(0.0, 1.0)
}

pub(super) fn shock_envelope(age: u32) -> f32 {
    if !(12..=84).contains(&age) {
        0.0
    } else if age <= 48 {
        (age - 12) as f32 / 36.0
    } else {
        (1.0 - (age - 48) as f32 / 36.0).max(0.0)
    }
}

pub(super) fn radial_echo_settings(snapshot: &MatchSnapshot) -> RadialEchoSettings {
    if snapshot.profile != quarrel_sim::YELLOW_REPLAY_PROFILE {
        return RadialEchoSettings::default();
    }
    let strength = snapshot
        .explosions
        .last()
        .map(|explosion| snapshot.tick.saturating_sub(explosion.tick))
        .map(|age| match age {
            0 => 0.025,
            1..=4 => 0.04 + age as f32 * 0.055,
            5..=8 => 0.34 + (age - 4) as f32 * 0.165,
            9..=21 => 1.0 - (age - 8) as f32 / 22.0,
            22..=29 => 0.41 - (age - 21) as f32 * 0.047,
            _ => 0.0,
        })
        .unwrap_or(0.0)
        .clamp(0.0, 1.0);
    RadialEchoSettings {
        strength,
        spacing: 0.011 + strength * 0.008,
        red_offset: 0.0025 * strength,
        _padding: 0.0,
    }
}

pub(super) fn spawn_yellow_paper(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    tick: u32,
    ice: bool,
) {
    let drift = tick as f32 * 0.0025;
    for facet in 0..if ice { 480_u32 } else { 54_u32 } {
        let column = facet % 9;
        let row = facet / 9;
        let seed = facet as f32 * 1.731;
        let center = if ice {
            Vec2::new(
                -680.0 + (facet.wrapping_mul(317) % 1_360) as f32 + (seed + drift).sin() * 24.0,
                -390.0
                    + (facet.wrapping_mul(191) % 780) as f32
                    + (seed * 0.73 - drift).cos() * 18.0,
            )
        } else {
            Vec2::new(
                -620.0 + column as f32 * 154.0 + (seed + drift).sin() * 34.0,
                -330.0 + row as f32 * 132.0 + (seed * 0.73 - drift).cos() * 31.0,
            )
        };
        let width = 82.0 + (facet.wrapping_mul(47) % 105) as f32;
        let height = if ice {
            7.0 + (facet.wrapping_mul(31) % 35) as f32
        } else {
            58.0 + (facet.wrapping_mul(31) % 78) as f32
        };
        let skew = (seed * 0.41).sin() * 30.0;
        let brush = Rot2::radians(if ice {
            (center.x * 0.003 + center.y * 0.002 + drift * 0.03).sin() * 0.8
        } else {
            0.0
        });
        spawn_triangle(
            commands,
            meshes,
            materials,
            [
                center + brush * Vec2::new(-width * 0.5, -height * 0.5),
                center + brush * Vec2::new(width * 0.5, -height * 0.42),
                center + brush * Vec2::new(skew, height * 0.5),
            ],
            if ice && facet % 3 != 0 {
                Color::srgba_u8(0, 12, 40, 16 + (facet % 7) as u8 * 3)
            } else if facet % 3 == 0 {
                Color::srgba_u8(9, 77, 78, 16)
            } else {
                Color::srgba_u8(0, 21, 48, 22)
            },
            -94.0 + (facet % 3) as f32,
        );
    }
}

#[cfg(test)]
mod data_camera_tests {
    use super::*;

    #[test]
    fn data_camera_has_no_lens_effects() {
        let arena = quarrel_sim::ArenaDefinition::load(
            &quarrel_sim::default_arena_directory().join("all-kinds.ron"),
        )
        .unwrap();
        let snapshot = quarrel_sim::AuthoritativeMatch::from_arena(77, arena)
            .unwrap()
            .snapshot();
        let (_, _, chromatic, lens) = camera_state(&snapshot);
        assert_eq!(chromatic.intensity, 0.0);
        assert_eq!(lens.intensity, 0.0);
    }

    #[test]
    fn letterboxing_keeps_square_and_fifteen_by_eight_scales_equal() {
        for (frame, target) in [
            ([0.0, 0.0, 8.0, 8.0], UVec2::new(1_280, 720)),
            ([0.0, 0.0, 15.0, 8.0], UVec2::new(1_500, 720)),
            ([0.0, 0.0, 15.0, 8.0], UVec2::new(1_280, 720)),
        ] {
            let viewport = data_arena_viewport(frame, target);
            let x_scale = viewport.physical_size.x as f32 / (frame[2] - frame[0]);
            let y_scale = viewport.physical_size.y as f32 / (frame[3] - frame[1]);
            assert!((x_scale - y_scale).abs() <= 1.0 / 8.0);
        }
    }
}
