use super::*;

#[expect(
    clippy::too_many_arguments,
    reason = "card art is projected from one definition into the existing card pose"
)]
pub(super) fn spawn_card_art(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    item: &ItemDefinition,
    position: Vec2,
    angle: f32,
    scale: f32,
    alpha: u8,
    z: f32,
) {
    let palette = item.palette_rgb;
    let color = Color::srgba_u8(palette[0], palette[1], palette[2], alpha);
    let dark = Color::srgba_u8(palette[0] / 7, palette[1] / 7, palette[2] / 7, alpha);
    match item.art_key.as_str() {
        "frost-ring" => {
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(0.0, 55.0),
                36.0,
                color,
                z,
                true,
            );
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(0.0, 55.0),
                24.0,
                dark,
                z + 0.1,
                false,
            );
            for spoke in 0..6 {
                spawn_art_bar(
                    commands,
                    position,
                    angle,
                    scale,
                    Vec2::new(0.0, 55.0),
                    Vec2::new(4.0, 48.0),
                    spoke as f32 * std::f32::consts::PI / 3.0,
                    color,
                    z + 0.2,
                    false,
                );
            }
        }
        "merged-rounds" => {
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(-17.0, 55.0),
                24.0,
                color,
                z,
                true,
            );
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(17.0, 55.0),
                24.0,
                color,
                z + 0.1,
                false,
            );
            spawn_art_bar(
                commands,
                position,
                angle,
                scale,
                Vec2::new(0.0, 55.0),
                Vec2::new(46.0, 9.0),
                0.0,
                Color::srgba_u8(255, 229, 211, alpha),
                z + 0.2,
                false,
            );
        }
        "fang-drop" => {
            spawn_art_polygon(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(0.0, 62.0),
                29.0,
                3,
                std::f32::consts::PI,
                color,
                z,
                true,
            );
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(0.0, 75.0),
                19.0,
                color,
                z + 0.1,
                false,
            );
            spawn_art_polygon(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(12.0, 42.0),
                12.0,
                3,
                -0.35,
                Color::srgba_u8(255, 205, 216, alpha),
                z + 0.2,
                false,
            );
        }
        "burst-rays" => {
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(0.0, 55.0),
                18.0,
                color,
                z + 0.2,
                true,
            );
            for ray in 0..8 {
                let ray_angle = ray as f32 * std::f32::consts::PI / 4.0;
                let offset =
                    Vec2::new(ray_angle.cos(), ray_angle.sin()) * 35.0 + Vec2::new(0.0, 55.0);
                spawn_art_bar(
                    commands,
                    position,
                    angle,
                    scale,
                    offset,
                    Vec2::new(7.0, 27.0),
                    ray_angle - std::f32::consts::FRAC_PI_2,
                    color,
                    z,
                    false,
                );
            }
        }
        "stun-stars" => {
            spawn_art_polygon(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(0.0, 55.0),
                25.0,
                4,
                0.25,
                color,
                z,
                true,
            );
            spawn_art_polygon(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(-31.0, 75.0),
                13.0,
                4,
                0.55,
                color,
                z + 0.1,
                false,
            );
            spawn_art_polygon(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(32.0, 70.0),
                10.0,
                4,
                0.1,
                Color::srgba_u8(255, 240, 108, alpha),
                z + 0.2,
                false,
            );
        }
        "impact-burst" => {
            spawn_art_polygon(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(0.0, 55.0),
                23.0,
                8,
                0.2,
                color,
                z + 0.2,
                true,
            );
            for ray in 0..6 {
                let ray_angle = ray as f32 * std::f32::consts::PI / 3.0;
                let offset =
                    Vec2::new(ray_angle.cos(), ray_angle.sin()) * 37.0 + Vec2::new(0.0, 55.0);
                spawn_art_polygon(
                    commands, meshes, materials, position, angle, scale, offset, 12.0, 3,
                    ray_angle, color, z, false,
                );
            }
        }
        "echo-rings" => {
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(-10.0, 55.0),
                34.0,
                color,
                z,
                true,
            );
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(-10.0, 55.0),
                25.0,
                dark,
                z + 0.1,
                false,
            );
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(20.0, 55.0),
                22.0,
                color,
                z + 0.2,
                false,
            );
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(20.0, 55.0),
                14.0,
                dark,
                z + 0.3,
                false,
            );
        }
        "vampire-orbit" => {
            spawn_art_polygon(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(0.0, 55.0),
                27.0,
                6,
                0.25,
                color,
                z,
                true,
            );
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(-39.0, 65.0),
                9.0,
                Color::srgba_u8(255, 203, 231, alpha),
                z + 0.2,
                false,
            );
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(38.0, 44.0),
                7.0,
                Color::srgba_u8(255, 203, 231, alpha),
                z + 0.2,
                false,
            );
            spawn_art_bar(
                commands,
                position,
                angle,
                scale,
                Vec2::new(0.0, 55.0),
                Vec2::new(84.0, 3.0),
                -0.25,
                color,
                z + 0.1,
                false,
            );
        }
        "electric-ring" => {
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(0.0, 55.0),
                34.0,
                color,
                z,
                true,
            );
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(0.0, 55.0),
                25.0,
                dark,
                z + 0.1,
                false,
            );
            for (offset, tilt) in [(-18.0, -0.45), (0.0, 0.45), (18.0, -0.45)] {
                spawn_art_bar(
                    commands,
                    position,
                    angle,
                    scale,
                    Vec2::new(offset, 55.0),
                    Vec2::new(7.0, 35.0),
                    tilt,
                    Color::srgba_u8(188, 248, 255, alpha),
                    z + 0.2,
                    false,
                );
            }
        }
        "speed-streak-blob" => {
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(12.0, 55.0),
                27.0,
                color,
                z + 0.2,
                true,
            );
            for y in [38.0_f32, 55.0, 72.0] {
                spawn_art_bar(
                    commands,
                    position,
                    angle,
                    scale,
                    Vec2::new(-30.0, y),
                    Vec2::new(42.0, 5.0),
                    0.0,
                    color,
                    z,
                    false,
                );
            }
        }
        "snowflake-bullet" => {
            spawn_art_bar(
                commands,
                position,
                angle,
                scale,
                Vec2::new(0.0, 55.0),
                Vec2::new(58.0, 12.0),
                0.0,
                color,
                z + 0.2,
                true,
            );
            for x in [-38.0_f32, 38.0] {
                for tilt in [
                    0.0_f32,
                    std::f32::consts::FRAC_PI_3,
                    -std::f32::consts::FRAC_PI_3,
                ] {
                    spawn_art_bar(
                        commands,
                        position,
                        angle,
                        scale,
                        Vec2::new(x, 55.0),
                        Vec2::new(28.0, 3.0),
                        tilt,
                        color,
                        z,
                        false,
                    );
                }
            }
        }
        "paper-fan-blob" => {
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(-18.0, 45.0),
                25.0,
                color,
                z,
                true,
            );
            for (x, tilt) in [(-22.0_f32, -0.25_f32), (0.0, 0.0), (22.0, 0.25)] {
                spawn_art_bar(
                    commands,
                    position,
                    angle,
                    scale,
                    Vec2::new(x, 69.0),
                    Vec2::new(30.0, 43.0),
                    tilt,
                    Color::srgba_u8(210, 246, 250, alpha),
                    z + 0.2,
                    false,
                );
            }
        }
        "dark-glasses-blob" => {
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(0.0, 55.0),
                31.0,
                color,
                z,
                true,
            );
            for x in [-15.0_f32, 15.0] {
                spawn_art_bar(
                    commands,
                    position,
                    angle,
                    scale,
                    Vec2::new(x, 62.0),
                    Vec2::new(24.0, 13.0),
                    0.0,
                    dark,
                    z + 0.2,
                    false,
                );
            }
        }
        "oversized-red-round" => {
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(0.0, 55.0),
                42.0,
                color,
                z,
                true,
            );
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(-12.0, 69.0),
                9.0,
                Color::srgba_u8(255, 184, 164, alpha),
                z + 0.2,
                false,
            );
        }
        "steady-target" => {
            for (radius, primary) in [(38.0_f32, true), (24.0, false), (10.0, false)] {
                spawn_art_circle(
                    commands,
                    meshes,
                    materials,
                    position,
                    angle,
                    scale,
                    Vec2::new(0.0, 55.0),
                    radius,
                    if radius == 24.0 { dark } else { color },
                    z + (38.0 - radius) * 0.01,
                    primary,
                );
            }
        }
        "tank-treads" => {
            spawn_art_bar(
                commands,
                position,
                angle,
                scale,
                Vec2::new(0.0, 47.0),
                Vec2::new(76.0, 30.0),
                0.0,
                color,
                z,
                true,
            );
            spawn_art_bar(
                commands,
                position,
                angle,
                scale,
                Vec2::new(9.0, 72.0),
                Vec2::new(45.0, 25.0),
                0.0,
                color,
                z + 0.1,
                false,
            );
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(-22.0, 47.0),
                10.0,
                dark,
                z + 0.2,
                false,
            );
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(22.0, 47.0),
                10.0,
                dark,
                z + 0.2,
                false,
            );
        }
        "timed-bomb" => {
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(-5.0, 48.0),
                34.0,
                color,
                z,
                true,
            );
            spawn_art_bar(
                commands,
                position,
                angle,
                scale,
                Vec2::new(18.0, 82.0),
                Vec2::new(29.0, 5.0),
                0.65,
                color,
                z + 0.1,
                false,
            );
            spawn_art_polygon(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(30.0, 95.0),
                12.0,
                8,
                0.0,
                Color::srgba_u8(255, 225, 105, alpha),
                z + 0.2,
                false,
            );
        }
        "homing-circuit" => {
            for (offset, radius, primary) in [
                (Vec2::new(-28.0, 69.0), 8.0_f32, true),
                (Vec2::new(4.0, 48.0), 7.0, false),
                (Vec2::new(31.0, 77.0), 8.0, false),
            ] {
                spawn_art_circle(
                    commands, meshes, materials, position, angle, scale, offset, radius, color, z,
                    primary,
                );
            }
            for (offset, tilt) in [
                (Vec2::new(-12.0, 59.0), -0.58_f32),
                (Vec2::new(18.0, 62.0), 0.82),
            ] {
                spawn_art_bar(
                    commands,
                    position,
                    angle,
                    scale,
                    offset,
                    Vec2::new(35.0, 4.0),
                    tilt,
                    color,
                    z + 0.1,
                    false,
                );
            }
        }
        "huge-weight" => {
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(0.0, 50.0),
                43.0,
                color,
                z,
                true,
            );
            spawn_art_bar(
                commands,
                position,
                angle,
                scale,
                Vec2::new(0.0, 91.0),
                Vec2::new(30.0, 15.0),
                0.0,
                color,
                z + 0.1,
                false,
            );
        }
        "healing-aura" => {
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(0.0, 55.0),
                42.0,
                color,
                z,
                true,
            );
            spawn_art_bar(
                commands,
                position,
                angle,
                scale,
                Vec2::new(0.0, 55.0),
                Vec2::new(46.0, 12.0),
                0.0,
                dark,
                z + 0.1,
                false,
            );
            spawn_art_bar(
                commands,
                position,
                angle,
                scale,
                Vec2::new(0.0, 55.0),
                Vec2::new(12.0, 46.0),
                0.0,
                dark,
                z + 0.1,
                false,
            );
        }
        "parasite-host" => {
            spawn_art_circle(
                commands,
                meshes,
                materials,
                position,
                angle,
                scale,
                Vec2::new(0.0, 48.0),
                36.0,
                color,
                z,
                true,
            );
            for (offset, tilt) in [
                (Vec2::new(-28.0, 78.0), -0.55_f32),
                (Vec2::new(0.0, 88.0), 0.0),
                (Vec2::new(28.0, 78.0), 0.55),
            ] {
                spawn_art_bar(
                    commands,
                    position,
                    angle,
                    scale,
                    offset,
                    Vec2::new(8.0, 29.0),
                    tilt,
                    color,
                    z + 0.1,
                    false,
                );
            }
        }
        unknown => panic!("unregistered card art key {unknown}"),
    }
}

pub(super) fn card_art_transform(
    position: Vec2,
    card_angle: f32,
    scale: f32,
    offset: Vec2,
    local_angle: f32,
    z: f32,
) -> Transform {
    let (sine, cosine) = card_angle.sin_cos();
    let offset = offset * scale;
    let rotated = Vec2::new(
        offset.x * cosine - offset.y * sine,
        offset.x * sine + offset.y * cosine,
    );
    Transform::from_xyz(position.x + rotated.x, position.y + rotated.y, z)
        .with_rotation(Quat::from_rotation_z(card_angle + local_angle))
        .with_scale(Vec3::splat(scale))
}

#[expect(
    clippy::too_many_arguments,
    reason = "small card-art primitive keeps its complete local pose explicit"
)]
pub(super) fn spawn_art_circle(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    position: Vec2,
    angle: f32,
    scale: f32,
    offset: Vec2,
    radius: f32,
    color: Color,
    z: f32,
    primary: bool,
) {
    let mut entity = commands.spawn((
        SceneVisual,
        Mesh2d(meshes.add(Circle::new(radius))),
        MeshMaterial2d(materials.add(color)),
        card_art_transform(position, angle, scale, offset, 0.0, z),
    ));
    if primary {
        entity.insert(CaptureElement::CardArt);
    }
}

#[expect(
    clippy::too_many_arguments,
    reason = "small card-art primitive keeps its complete local pose explicit"
)]
pub(super) fn spawn_art_polygon(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    position: Vec2,
    angle: f32,
    scale: f32,
    offset: Vec2,
    radius: f32,
    sides: u32,
    local_angle: f32,
    color: Color,
    z: f32,
    primary: bool,
) {
    let mut entity = commands.spawn((
        SceneVisual,
        Mesh2d(meshes.add(RegularPolygon::new(radius, sides))),
        MeshMaterial2d(materials.add(color)),
        card_art_transform(position, angle, scale, offset, local_angle, z),
    ));
    if primary {
        entity.insert(CaptureElement::CardArt);
    }
}

#[expect(
    clippy::too_many_arguments,
    reason = "small card-art primitive keeps its complete local pose explicit"
)]
pub(super) fn spawn_art_bar(
    commands: &mut Commands,
    position: Vec2,
    angle: f32,
    scale: f32,
    offset: Vec2,
    size: Vec2,
    local_angle: f32,
    color: Color,
    z: f32,
    primary: bool,
) {
    let mut entity = commands.spawn((
        SceneVisual,
        Sprite::from_color(color, size),
        card_art_transform(position, angle, scale, offset, local_angle, z),
    ));
    if primary {
        entity.insert(CaptureElement::CardArt);
    }
}
