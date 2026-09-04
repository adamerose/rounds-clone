use bevy::{
    app::SubApps,
    asset::RenderAssetUsages,
    camera::RenderTarget,
    image::Image,
    prelude::*,
    render::{
        RenderPlugin,
        render_resource::{Extent3d, PollType, TextureDimension, TextureFormat, TextureUsages},
        renderer::RenderDevice,
        view::screenshot::{Screenshot, save_to_disk},
    },
    window::{ExitCondition, Monitor, OnMonitor, PrimaryWindow},
    winit::WinitPlugin,
};
use rounds_sim::MatchSnapshot;
use sha2::{Digest, Sha256};
use std::path::Path;

pub const FRAME_WIDTH: u32 = 1_280;
pub const FRAME_HEIGHT: u32 = 720;
pub const RENDERER_IDENTITY: &str = "bevy-0.19.1-2d-offscreen";

#[derive(Resource)]
struct SceneSnapshot(MatchSnapshot);

#[derive(Resource, Clone)]
struct CaptureTarget(Handle<Image>);

#[derive(Resource)]
struct VisibleLifetime {
    frames: u32,
    shown: bool,
}

#[derive(Resource)]
struct VisibleReplay {
    snapshots: Vec<MatchSnapshot>,
    next: usize,
}

#[derive(Resource, Default)]
struct VisibleWindowRequested(bool);

#[derive(Component)]
struct SceneVisual;

#[allow(clippy::unnecessary_to_owned)] // Bevy's observer owns a 'static capture path.
pub fn render_png(snapshot: &MatchSnapshot, output: &Path) -> Result<Vec<u8>, String> {
    if output.exists() {
        std::fs::remove_file(output)
            .map_err(|error| format!("remove stale {}: {error}", output.display()))?;
    }
    if let Some(parent) = output
        .parent()
        .filter(|parent| !parent.as_os_str().is_empty())
    {
        std::fs::create_dir_all(parent)
            .map_err(|error| format!("create {}: {error}", parent.display()))?;
    }

    let render_plugin = RenderPlugin {
        synchronous_pipeline_compilation: true,
        ..default()
    };
    let window_plugin = WindowPlugin {
        primary_window: None,
        exit_condition: ExitCondition::DontExit,
        ..default()
    };
    let mut app = App::new();
    app.add_plugins(
        DefaultPlugins
            .set(window_plugin)
            .set(render_plugin)
            .disable::<bevy::log::LogPlugin>()
            .disable::<WinitPlugin>(),
    )
    .insert_resource(ClearColor(Color::srgb_u8(2, 48, 54)))
    .insert_resource(SceneSnapshot(snapshot.clone()))
    .add_systems(Startup, setup_offscreen_scene);
    app.finish();
    app.cleanup();
    let mut sub_apps = std::mem::take(app.sub_apps_mut());
    let target = new_render_target(&mut sub_apps, FRAME_WIDTH, FRAME_HEIGHT);
    sub_apps
        .main
        .world_mut()
        .insert_resource(CaptureTarget(target.clone()));

    update_and_wait(&mut sub_apps);
    update_and_wait(&mut sub_apps);
    sub_apps
        .main
        .world_mut()
        .spawn(Screenshot::image(target))
        .observe(save_to_disk(output.to_path_buf()));
    for _ in 0..6 {
        update_and_wait(&mut sub_apps);
        if output.is_file() {
            break;
        }
    }
    let bytes = std::fs::read(output)
        .map_err(|error| format!("Bevy did not write {}: {error}", output.display()))?;
    if !bytes.starts_with(b"\x89PNG\r\n\x1a\n") {
        return Err("Bevy capture did not produce a PNG".to_owned());
    }
    Ok(bytes)
}

pub fn frame_sha256(frame: &[u8]) -> String {
    format!("{:x}", Sha256::digest(frame))
}

/// Runs the same scene model in a real Bevy window. The window starts hidden,
/// requests zero-based monitor index 3, and is only revealed after Bevy reports
/// that winit placed it on the project's required 1920x1080 display.
pub fn run_visible(snapshots: Vec<MatchSnapshot>) -> Result<(), String> {
    if snapshots.is_empty() {
        return Err("visible replay needs at least one snapshot".to_owned());
    }
    App::new()
        .add_plugins(DefaultPlugins.set(WindowPlugin {
            primary_window: None,
            exit_condition: ExitCondition::DontExit,
            ..default()
        }))
        .insert_resource(ClearColor(Color::srgb_u8(2, 48, 54)))
        .insert_resource(SceneSnapshot(snapshots[0].clone()))
        .insert_resource(VisibleReplay { snapshots, next: 0 })
        .init_resource::<VisibleWindowRequested>()
        .insert_resource(VisibleLifetime {
            frames: u32::MAX,
            shown: false,
        })
        .add_systems(Startup, setup_visible_scene)
        .add_systems(Update, create_monitor_four_window)
        .add_systems(
            Update,
            (verify_monitor_show_and_exit, advance_visible_scene).chain(),
        )
        .run();
    Ok(())
}

fn create_monitor_four_window(
    mut commands: Commands,
    monitors: Query<(Entity, &Monitor)>,
    mut requested: ResMut<VisibleWindowRequested>,
) {
    if requested.0 {
        return;
    }
    let Some((monitor_entity, monitor)) = monitors
        .iter()
        .find(|(_, monitor)| (monitor.physical_width, monitor.physical_height) == (1_920, 1_080))
    else {
        return;
    };
    commands.spawn((
        Window {
            title: "ROUNDS clone — teal duel".to_owned(),
            resolution: (FRAME_WIDTH, FRAME_HEIGHT).into(),
            position: WindowPosition::Centered(MonitorSelection::Entity(monitor_entity)),
            visible: false,
            ..default()
        },
        PrimaryWindow,
    ));
    requested.0 = true;
    println!(
        "{{\"event\":\"monitorFourSelected\",\"width\":{},\"height\":{},\"x\":{},\"y\":{}}}",
        monitor.physical_width,
        monitor.physical_height,
        monitor.physical_position.x,
        monitor.physical_position.y
    );
}

fn new_render_target(sub_apps: &mut SubApps, width: u32, height: u32) -> Handle<Image> {
    let mut target = Image::new_uninit(
        Extent3d {
            width,
            height,
            depth_or_array_layers: 1,
        },
        TextureDimension::D2,
        TextureFormat::Rgba8UnormSrgb,
        RenderAssetUsages::RENDER_WORLD,
    );
    target.texture_descriptor.usage |= TextureUsages::RENDER_ATTACHMENT;
    sub_apps
        .main
        .world_mut()
        .resource_mut::<Assets<Image>>()
        .add(target)
}

fn update_and_wait(sub_apps: &mut SubApps) {
    sub_apps.update();
    sub_apps
        .main
        .world()
        .resource::<RenderDevice>()
        .wgpu_device()
        .poll(PollType::Wait {
            submission_index: None,
            timeout: None,
        })
        .expect("poll Bevy render device");
}

fn setup_offscreen_scene(
    mut commands: Commands,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<ColorMaterial>>,
    snapshot: Res<SceneSnapshot>,
    target: Res<CaptureTarget>,
) {
    let camera_nudge = snapshot
        .0
        .players
        .iter()
        .map(|player| player.velocity_x_milli_per_second)
        .sum::<i32>() as f32
        / 600_000.0;
    commands.spawn((
        Camera2d,
        RenderTarget::Image(target.0.clone().into()),
        Transform::from_xyz(camera_nudge.clamp(-5.0, 5.0), 0.0, 0.0),
    ));
    spawn_snapshot_scene(&mut commands, &mut meshes, &mut materials, &snapshot.0);
}

fn setup_visible_scene(mut commands: Commands) {
    commands.spawn(Camera2d);
}

fn advance_visible_scene(
    mut commands: Commands,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<ColorMaterial>>,
    visuals: Query<Entity, With<SceneVisual>>,
    mut replay: ResMut<VisibleReplay>,
    mut lifetime: ResMut<VisibleLifetime>,
) {
    if replay.next >= replay.snapshots.len() {
        return;
    }
    for entity in &visuals {
        commands.entity(entity).despawn();
    }
    let snapshot = &replay.snapshots[replay.next];
    spawn_snapshot_scene(&mut commands, &mut meshes, &mut materials, snapshot);
    replay.next += 1;
    if replay.next == replay.snapshots.len() {
        lifetime.frames = 3;
    }
}

fn verify_monitor_show_and_exit(
    mut primary: Single<(&mut Window, &OnMonitor), With<PrimaryWindow>>,
    monitors: Query<&Monitor>,
    mut lifetime: ResMut<VisibleLifetime>,
    mut exit: MessageWriter<AppExit>,
) {
    lifetime.frames -= 1;
    if !lifetime.shown {
        let monitor = monitors
            .get(primary.1.0)
            .expect("primary window did not report its monitor");
        assert_eq!(
            (monitor.physical_width, monitor.physical_height),
            (1_920, 1_080),
            "monitor index 3 is not the required 1920x1080 project display; window remained hidden"
        );
        primary.0.visible = true;
        lifetime.shown = true;
        println!(
            "{{\"event\":\"windowPlacementVerified\",\"monitorIndex\":3,\"width\":{},\"height\":{},\"x\":{},\"y\":{}}}",
            monitor.physical_width,
            monitor.physical_height,
            monitor.physical_position.x,
            monitor.physical_position.y
        );
    }
    if lifetime.frames == 0 {
        exit.write(AppExit::Success);
    }
}

fn spawn_snapshot_scene(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    let circle = meshes.add(Circle::new(22.0));
    let block_ring = meshes.add(Annulus::new(29.0, 33.0));
    let bullet = meshes.add(Circle::new(5.0));

    commands.spawn((
        SceneVisual,
        Sprite::from_color(Color::srgb_u8(2, 48, 54), Vec2::new(1_280.0, 720.0)),
        Transform::from_xyz(0.0, 0.0, -100.0),
    ));
    let drift = snapshot.tick as f32 * 0.035;
    for (index, x) in [-520.0_f32, -260.0, 0.0, 260.0, 520.0]
        .into_iter()
        .enumerate()
    {
        let offset = (drift + index as f32 * 1.7).sin() * 28.0;
        commands.spawn((
            SceneVisual,
            Sprite::from_color(
                if index % 2 == 0 {
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

    for surface in &snapshot.arena {
        let x = surface.center_x_milli as f32 / 1_000.0;
        let y = surface.center_y_milli as f32 / 1_000.0;
        let width = surface.width_milli as f32 / 1_000.0;
        let height = surface.height_milli as f32 / 1_000.0;
        if surface.id < 10 {
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
            Transform::from_xyz(x, y, 0.0),
        ));
    }

    for player in &snapshot.players {
        let x = player.x_milli as f32 / 1_000.0;
        let y = player.y_milli as f32 / 1_000.0;
        let body_color = if player.hit_flash_ticks > 0 {
            Color::WHITE
        } else if player.id == 0 {
            Color::srgb_u8(244, 63, 86)
        } else {
            Color::srgb_u8(39, 166, 255)
        };
        for (offset, angle) in [(-9.0, -0.38), (9.0, 0.38)] {
            commands.spawn((
                SceneVisual,
                Sprite::from_color(body_color, Vec2::new(7.0, 25.0)),
                Transform::from_xyz(x + offset, y - 28.0, 4.0)
                    .with_rotation(Quat::from_rotation_z(angle)),
            ));
        }
        commands.spawn((
            SceneVisual,
            Mesh2d(circle.clone()),
            MeshMaterial2d(materials.add(body_color)),
            Transform::from_xyz(x, y, 5.0),
        ));
        let aim = Vec2::new(f32::from(player.aim_x), f32::from(player.aim_y)).normalize_or(Vec2::X);
        commands.spawn((
            SceneVisual,
            Sprite::from_color(Color::srgb_u8(35, 39, 44), Vec2::new(42.0, 9.0)),
            Transform::from_xyz(x + aim.x * 25.0, y + aim.y * 25.0, 7.0)
                .with_rotation(Quat::from_rotation_z(aim.y.atan2(aim.x))),
        ));
        commands.spawn((
            SceneVisual,
            Sprite::from_color(Color::srgb_u8(22, 27, 31), Vec2::new(70.0, 7.0)),
            Transform::from_xyz(x, y + 48.0, 8.0),
        ));
        commands.spawn((
            SceneVisual,
            Sprite::from_color(
                if player.id == 0 {
                    Color::srgb_u8(244, 90, 104)
                } else {
                    Color::srgb_u8(70, 188, 255)
                },
                Vec2::new(70.0 * f32::from(player.health) / 100.0, 5.0),
            ),
            Transform::from_xyz(
                x - (70.0 - 70.0 * f32::from(player.health) / 100.0) / 2.0,
                y + 48.0,
                9.0,
            ),
        ));
        commands.spawn((
            SceneVisual,
            Text2d::new(if player.id == 0 { "ORANGE" } else { "BLUE" }),
            TextFont {
                font_size: FontSize::Px(12.0),
                ..default()
            },
            TextColor(Color::WHITE),
            Transform::from_xyz(x, y + 62.0, 9.0),
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
        let start = Vec2::new(
            projectile.previous_x_milli as f32 / 1_000.0,
            projectile.previous_y_milli as f32 / 1_000.0,
        );
        let end = Vec2::new(
            projectile.x_milli as f32 / 1_000.0,
            projectile.y_milli as f32 / 1_000.0,
        );
        let segment = end - start;
        let length = segment.length().clamp(2.0, 92.0);
        commands.spawn((
            SceneVisual,
            Sprite::from_color(Color::srgba_u8(255, 206, 73, 148), Vec2::new(length, 3.0)),
            Transform::from_xyz(end.x - segment.x * 0.5, end.y - segment.y * 0.5, 12.0)
                .with_rotation(Quat::from_rotation_z(segment.y.atan2(segment.x))),
        ));
        commands.spawn((
            SceneVisual,
            Mesh2d(bullet.clone()),
            MeshMaterial2d(materials.add(Color::srgb_u8(255, 240, 143))),
            Transform::from_xyz(end.x, end.y, 13.0),
        ));
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use rounds_sim::run_scripted_match;

    #[test]
    fn bevy_offscreen_renderer_writes_a_full_size_png() {
        let (snapshot, _) = run_scripted_match(38, 320);
        let path = std::env::temp_dir().join(format!(
            "rounds-bevy-render-{}-{}.png",
            std::process::id(),
            snapshot.tick
        ));
        let _ = std::fs::remove_file(&path);
        let first = render_png(&snapshot, &path).unwrap();
        let decoder = png::Decoder::new(std::io::Cursor::new(&first));
        let reader = decoder.read_info().unwrap();
        assert_eq!(reader.info().width, FRAME_WIDTH);
        assert_eq!(reader.info().height, FRAME_HEIGHT);
        assert_eq!(frame_sha256(&first).len(), 64);
        std::fs::remove_file(path).unwrap();
    }
}
