use super::*;
use quarrel_sim::{ArenaKind, ArenaShape};

/// Draws an arena supplied by data rather than one of the replay-specific scenes.
/// `frame` is `[left, bottom, right, top]`, in the same world units as objects.
pub(super) fn spawn_data_arena_scene(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    let Some(arena) = &snapshot.arena_objects else {
        return;
    };
    let [left, bottom, right, top] = arena.frame;
    let center = Vec2::new((left + right) * 0.5, (bottom + top) * 0.5);
    commands.spawn((
        SceneVisual,
        CaptureElement::Background,
        Sprite::from_color(
            Color::srgb_u8(7, 16, 28),
            Vec2::new(right - left, top - bottom),
        ),
        Transform::from_xyz(center.x, center.y, -100.0),
    ));

    for object in &arena.objects {
        spawn_object(commands, meshes, materials, object);
    }
    for surface in &snapshot.arena {
        spawn_legacy_surface(commands, meshes, materials, surface);
    }
    for chain in &arena.chains {
        let end = arena
            .objects
            .iter()
            .find(|object| object.id == chain.body_b)
            .map(|object| Vec2::from(object.position));
        let start = chain.body_a.and_then(|id| {
            arena
                .objects
                .iter()
                .find(|object| object.id == id)
                .map(|object| Vec2::from(object.position))
        });
        if let Some(end) = end {
            spawn_chain(
                commands,
                meshes,
                materials,
                start.unwrap_or(Vec2::from(chain.anchor)),
                end,
            );
        }
    }
    for spawn in &arena.spawns {
        let point = Vec2::from(*spawn);
        commands.spawn((
            SceneVisual,
            Mesh2d(meshes.add(Annulus::new(12.0, 14.0))),
            MeshMaterial2d(materials.add(Color::srgba_u8(212, 245, 233, 170))),
            Transform::from_xyz(point.x, point.y, 2.0),
        ));
    }
    spawn_fighters(commands, meshes, materials, snapshot);
    spawn_projectiles(commands, meshes, materials, snapshot);
}

fn spawn_legacy_surface(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    surface: &quarrel_sim::ArenaSurfaceSnapshot,
) {
    let position = Vec2::new(
        surface.center_x_milli as f32 / 1_000.0,
        surface.center_y_milli as f32 / 1_000.0,
    );
    let rotation = surface.rotation_milliradians as f32 / 1_000.0;
    let color = Color::srgb_u8(
        surface.face_rgb[0],
        surface.face_rgb[1],
        surface.face_rgb[2],
    );
    if surface.outline_milli.is_empty() {
        commands.spawn((
            SceneVisual,
            Sprite::from_color(
                color,
                Vec2::new(
                    surface.width_milli as f32 / 1_000.0,
                    surface.height_milli as f32 / 1_000.0,
                ),
            ),
            Transform::from_xyz(position.x, position.y, 0.0)
                .with_rotation(Quat::from_rotation_z(rotation)),
        ));
    } else {
        let vertices = surface
            .outline_milli
            .iter()
            .map(|point| {
                position
                    + rotate(
                        Vec2::new(point[0] as f32 / 1_000.0, point[1] as f32 / 1_000.0),
                        rotation,
                    )
            })
            .collect::<Vec<_>>();
        concave_polygon(commands, meshes, materials, &vertices, color, 0.0);
    }
}

fn spawn_object(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    object: &quarrel_sim::ArenaObject,
) {
    let color = Color::srgb_u8(object.color[0], object.color[1], object.color[2]);
    let position = Vec2::from(object.position);
    let z = match &object.kind {
        ArenaKind::Background => -20.0,
        ArenaKind::Solid | ArenaKind::Breakable { .. } => 0.0,
        ArenaKind::Loose | ArenaKind::Saw { .. } => 1.0,
    };
    let loose = matches!(
        &object.kind,
        ArenaKind::Loose
            | ArenaKind::Breakable { loose: true }
            | ArenaKind::Saw { loose: true, .. }
    );
    let fill = if matches!(&object.kind, ArenaKind::Background) {
        Color::srgba(
            color.to_linear().red * 0.28,
            color.to_linear().green * 0.28,
            color.to_linear().blue * 0.28,
            1.0,
        )
    } else {
        color
    };
    match &object.shape {
        ArenaShape::Rectangle { size } => {
            commands.spawn((
                SceneVisual,
                Sprite::from_color(fill, Vec2::from(*size)),
                Transform::from_xyz(position.x, position.y, z)
                    .with_rotation(Quat::from_rotation_z(object.rotation)),
            ));
            if loose {
                rectangle_outline(
                    commands,
                    position,
                    Vec2::from(*size),
                    object.rotation,
                    z + 0.1,
                );
            }
        }
        ArenaShape::Circle { radius } => {
            commands.spawn((
                SceneVisual,
                Mesh2d(meshes.add(Circle::new(
                    if matches!(object.kind, ArenaKind::Saw { .. }) {
                        *radius * 0.7
                    } else {
                        *radius
                    },
                ))),
                MeshMaterial2d(materials.add(fill)),
                Transform::from_xyz(position.x, position.y, z),
            ));
            if loose {
                commands.spawn((
                    SceneVisual,
                    Mesh2d(meshes.add(Annulus::new(*radius, *radius + 2.0))),
                    MeshMaterial2d(materials.add(Color::srgba_u8(238, 248, 242, 190))),
                    Transform::from_xyz(position.x, position.y, z + 0.1),
                ));
            }
        }
        ArenaShape::Polygon { vertices } => {
            polygon(
                commands,
                meshes,
                materials,
                position.extend(z),
                object.rotation,
                vertices,
                fill,
            );
            if loose {
                polygon_outline(commands, position, object.rotation, vertices, z + 0.1);
            }
        }
    }
    if matches!(&object.kind, ArenaKind::Breakable { .. }) {
        let diameter = shape_radius(&object.shape) * 2.0;
        cracks(
            commands,
            position,
            Vec2::splat(diameter),
            object.rotation,
            z + 0.2,
        );
    }
    if let ArenaKind::Saw { teeth, .. } = &object.kind {
        let radius = shape_radius(&object.shape);
        spawn_saw(
            commands,
            meshes,
            materials,
            position.extend(z + 0.3),
            object.rotation,
            radius,
            *teeth,
        );
    }
}

fn concave_polygon(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    outline: &[Vec2],
    color: Color,
    z: f32,
) {
    if outline.len() < 3 {
        return;
    }
    let mut levels = outline.iter().map(|point| point.y).collect::<Vec<_>>();
    levels.sort_by(f32::total_cmp);
    levels.dedup();
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
            for triangle in [
                [bottom_left, bottom_right, top_right],
                [bottom_left, top_right, top_left],
            ] {
                commands.spawn((
                    SceneVisual,
                    Mesh2d(meshes.add(Triangle2d::new(triangle[0], triangle[1], triangle[2]))),
                    MeshMaterial2d(materials.add(color)),
                    Transform::from_xyz(0.0, 0.0, z),
                ));
            }
        }
    }
}

fn polygon(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    position: Vec3,
    rotation: f32,
    vertices: &[[f32; 2]],
    color: Color,
) {
    let z = position.z;
    let position = position.truncate();
    if vertices.len() < 3 {
        return;
    }
    let origin = rotate(Vec2::from(vertices[0]), rotation) + position;
    for pair in vertices[1..].windows(2) {
        let b = rotate(Vec2::from(pair[0]), rotation) + position;
        let c = rotate(Vec2::from(pair[1]), rotation) + position;
        commands.spawn((
            SceneVisual,
            Mesh2d(meshes.add(Triangle2d::new(origin, b, c))),
            MeshMaterial2d(materials.add(color)),
            Transform::from_xyz(0.0, 0.0, z),
        ));
    }
}

fn polygon_outline(
    commands: &mut Commands,
    position: Vec2,
    rotation: f32,
    vertices: &[[f32; 2]],
    z: f32,
) {
    for (from, to) in vertices
        .iter()
        .zip(vertices.iter().cycle().skip(1))
        .take(vertices.len())
    {
        segment(
            commands,
            position + rotate(Vec2::from(*from), rotation),
            position + rotate(Vec2::from(*to), rotation),
            2.0,
            Color::srgba_u8(236, 249, 243, 185),
            z,
        );
    }
}

fn rectangle_outline(commands: &mut Commands, center: Vec2, size: Vec2, rotation: f32, z: f32) {
    for (offset, length, thickness) in [
        (Vec2::new(0.0, size.y * 0.5), size.x, 2.0),
        (Vec2::new(0.0, -size.y * 0.5), size.x, 2.0),
        (Vec2::new(size.x * 0.5, 0.0), size.y, 2.0),
        (Vec2::new(-size.x * 0.5, 0.0), size.y, 2.0),
    ] {
        let dimensions = if offset.x.abs() > offset.y.abs() {
            Vec2::new(thickness, length)
        } else {
            Vec2::new(length, thickness)
        };
        let offset = rotate(offset, rotation);
        commands.spawn((
            SceneVisual,
            Sprite::from_color(Color::srgba_u8(236, 249, 243, 185), dimensions),
            Transform::from_xyz(center.x + offset.x, center.y + offset.y, z)
                .with_rotation(Quat::from_rotation_z(rotation)),
        ));
    }
}

fn cracks(commands: &mut Commands, center: Vec2, size: Vec2, rotation: f32, z: f32) {
    for (from, to) in [
        (Vec2::new(-0.27, 0.22), Vec2::new(-0.04, -0.05)),
        (Vec2::new(-0.04, -0.05), Vec2::new(0.22, -0.30)),
        (Vec2::new(-0.04, -0.05), Vec2::new(0.18, 0.20)),
    ] {
        let a = rotate(from * size, rotation) + center;
        let b = rotate(to * size, rotation) + center;
        segment(commands, a, b, 1.8, Color::srgba_u8(35, 27, 38, 210), z);
    }
}

fn spawn_saw(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    center: Vec3,
    rotation: f32,
    radius: f32,
    teeth: u8,
) {
    let z = center.z;
    let center = center.truncate();
    let teeth = teeth.max(3);
    for tooth in 0..teeth {
        let angle = rotation + tooth as f32 * std::f32::consts::TAU / f32::from(teeth);
        let a = center + Vec2::from_angle(angle - 0.22) * radius * 0.78;
        let b = center + Vec2::from_angle(angle + 0.22) * radius * 0.78;
        let tip = center + Vec2::from_angle(angle) * radius;
        commands.spawn((
            SceneVisual,
            Mesh2d(meshes.add(Triangle2d::new(a, b, tip))),
            MeshMaterial2d(materials.add(Color::srgb_u8(222, 234, 229))),
            Transform::from_xyz(0.0, 0.0, z),
        ));
    }
}

fn spawn_chain(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    start: Vec2,
    end: Vec2,
) {
    let delta = end - start;
    let steps = (delta.length() / 12.0).ceil().max(1.0) as u16;
    for step in 0..=steps {
        let point = start.lerp(end, f32::from(step) / f32::from(steps));
        commands.spawn((
            SceneVisual,
            Mesh2d(meshes.add(Circle::new(2.5))),
            MeshMaterial2d(materials.add(Color::srgb_u8(214, 190, 139))),
            Transform::from_xyz(point.x, point.y, -1.0),
        ));
    }
}

fn spawn_fighters(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    for player in &snapshot.players {
        let point = Vec2::new(player.x_milli as f32, player.y_milli as f32) / 1_000.0;
        let color = if player.id == 0 {
            Color::srgb_u8(244, 84, 72)
        } else {
            Color::srgb_u8(54, 167, 252)
        };
        commands.spawn((
            SceneVisual,
            CaptureElement::Character,
            Mesh2d(meshes.add(Circle::new(22.0))),
            MeshMaterial2d(materials.add(if player.alive {
                color
            } else {
                color.with_alpha(0.35)
            })),
            Transform::from_xyz(point.x, point.y, 8.0),
        ));
        let aim = Vec2::new(f32::from(player.aim_x), f32::from(player.aim_y)).normalize_or(Vec2::X);
        segment(
            commands,
            point + aim * 12.0,
            point + aim * 33.0,
            7.0,
            Color::srgb_u8(30, 36, 44),
            9.0,
        );
    }
}

fn spawn_projectiles(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    for projectile in &snapshot.projectiles {
        let start = Vec2::new(
            projectile.previous_x_milli as f32,
            projectile.previous_y_milli as f32,
        ) / 1_000.0;
        let end = Vec2::new(projectile.x_milli as f32, projectile.y_milli as f32) / 1_000.0;
        segment(
            commands,
            start,
            end,
            3.0,
            Color::srgba_u8(255, 218, 92, 180),
            11.0,
        );
        commands.spawn((
            SceneVisual,
            Mesh2d(meshes.add(Circle::new(5.0))),
            MeshMaterial2d(materials.add(Color::srgb_u8(255, 243, 158))),
            Transform::from_xyz(end.x, end.y, 12.0),
        ));
    }
}

fn segment(commands: &mut Commands, start: Vec2, end: Vec2, thickness: f32, color: Color, z: f32) {
    let delta = end - start;
    commands.spawn((
        SceneVisual,
        Sprite::from_color(color, Vec2::new(delta.length().max(thickness), thickness)),
        Transform::from_xyz((start.x + end.x) * 0.5, (start.y + end.y) * 0.5, z)
            .with_rotation(Quat::from_rotation_z(delta.y.atan2(delta.x))),
    ));
}

fn rotate(point: Vec2, angle: f32) -> Vec2 {
    Mat2::from_angle(angle) * point
}

fn shape_radius(shape: &ArenaShape) -> f32 {
    match shape {
        ArenaShape::Circle { radius } => *radius,
        ArenaShape::Rectangle { size } => Vec2::from(*size).length() * 0.5,
        ArenaShape::Polygon { vertices } => vertices
            .iter()
            .map(|vertex| Vec2::from(*vertex).length())
            .fold(0.0, f32::max),
    }
}
