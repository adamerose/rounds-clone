use bevy::{
    app::SubApps,
    asset::RenderAssetUsages,
    camera::{Hdr, RenderTarget},
    image::Image,
    post_process::{
        bloom::Bloom,
        effect_stack::{ChromaticAberration, LensDistortion},
    },
    prelude::*,
    render::{
        RenderPlugin,
        render_resource::{Extent3d, PollType, TextureDimension, TextureFormat, TextureUsages},
        renderer::RenderDevice,
        view::screenshot::{Screenshot, ScreenshotCaptured},
    },
    window::{ExitCondition, Monitor, OnMonitor, PrimaryWindow},
    winit::WinitPlugin,
};
use rounds_sim::{DynamicBodyShape, MatchSnapshot, ReplayProfile};
use sha2::{Digest, Sha256};
use std::{
    path::Path,
    sync::mpsc::{TryRecvError, sync_channel},
    time::{Duration, Instant},
};

pub const FRAME_WIDTH: u32 = 1_280;
pub const FRAME_HEIGHT: u32 = 720;
pub const RENDERER_IDENTITY: &str = "bevy-0.19.1-2d-hdr-bloom-chromatic-aberration-lens-distortion";
const CAPTURE_TIMEOUT: Duration = Duration::from_secs(15);
const DEVICE_POLL_TIMEOUT: Duration = Duration::from_secs(2);
const PROJECT_DISPLAY_POSITION: IVec2 = IVec2::new(364, -1_080);
const PROJECT_DISPLAY_SIZE: UVec2 = UVec2::new(1_920, 1_080);
const MONITOR_DISCOVERY_FRAME_LIMIT: u16 = 120;

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

#[derive(Resource, Default)]
struct MonitorDiscovery {
    frames: u16,
    failed: bool,
}

#[derive(Component)]
struct SceneVisual;

pub fn render_png(snapshot: &MatchSnapshot, output: &Path) -> Result<Vec<u8>, String> {
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

    update_and_wait(&mut sub_apps)?;
    update_and_wait(&mut sub_apps)?;
    let (sender, receiver) = sync_channel(1);
    sub_apps
        .main
        .world_mut()
        .spawn(Screenshot::image(target))
        .observe(move |captured: On<ScreenshotCaptured>| {
            let _ = sender.send(captured.image.clone());
        });
    let deadline = Instant::now() + CAPTURE_TIMEOUT;
    let captured = loop {
        update_and_wait(&mut sub_apps)?;
        match receiver.try_recv() {
            Ok(image) => break image,
            Err(TryRecvError::Disconnected) => {
                return Err("Bevy screenshot observer disconnected before completion".to_owned());
            }
            Err(TryRecvError::Empty) if Instant::now() >= deadline => {
                return Err(format!(
                    "Bevy screenshot did not complete within {} seconds",
                    CAPTURE_TIMEOUT.as_secs()
                ));
            }
            Err(TryRecvError::Empty) => {}
        }
    };
    let bytes = encode_png(captured)?;
    create_parent(output)?;
    std::fs::write(output, &bytes)
        .map_err(|error| format!("write {}: {error}", output.display()))?;
    Ok(bytes)
}

pub fn frame_sha256(frame: &[u8]) -> String {
    format!("{:x}", Sha256::digest(frame))
}

/// Runs the same scene model in a real Bevy window. The window starts hidden and
/// is revealed only after Bevy reports the exact configured project display.
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
        .init_resource::<MonitorDiscovery>()
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
        .run()
        .is_success()
        .then_some(())
        .ok_or_else(|| "visible replay exited before verifying the project display".to_owned())
}

fn create_monitor_four_window(
    mut commands: Commands,
    monitors: Query<(Entity, &Monitor)>,
    mut requested: ResMut<VisibleWindowRequested>,
    mut discovery: ResMut<MonitorDiscovery>,
    mut exit: MessageWriter<AppExit>,
) {
    if requested.0 || discovery.failed {
        return;
    }
    let matches = monitors
        .iter()
        .filter(|(_, monitor)| is_project_display(monitor))
        .collect::<Vec<_>>();
    if matches.len() > 1 {
        discovery.failed = true;
        eprintln!(
            "multiple displays reported the configured project-display identity; window remained hidden"
        );
        exit.write(AppExit::error());
        return;
    }
    let Some((monitor_entity, monitor)) = matches.into_iter().next() else {
        discovery.frames += 1;
        if discovery.frames >= MONITOR_DISCOVERY_FRAME_LIMIT {
            discovery.failed = true;
            eprintln!(
                "configured project display at ({},{}) {}x{} was not reported; window remained hidden",
                PROJECT_DISPLAY_POSITION.x,
                PROJECT_DISPLAY_POSITION.y,
                PROJECT_DISPLAY_SIZE.x,
                PROJECT_DISPLAY_SIZE.y
            );
            exit.write(AppExit::error());
        }
        return;
    };
    commands.spawn((
        Window {
            title: "ROUNDS clone — authoritative replay".to_owned(),
            resolution: (FRAME_WIDTH, FRAME_HEIGHT).into(),
            position: WindowPosition::Centered(MonitorSelection::Entity(monitor_entity)),
            visible: false,
            ..default()
        },
        PrimaryWindow,
    ));
    requested.0 = true;
    println!(
        "{{\"event\":\"projectDisplaySelected\",\"width\":{},\"height\":{},\"x\":{},\"y\":{}}}",
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

fn update_and_wait(sub_apps: &mut SubApps) -> Result<(), String> {
    sub_apps.update();
    sub_apps
        .main
        .world()
        .resource::<RenderDevice>()
        .wgpu_device()
        .poll(PollType::Wait {
            submission_index: None,
            timeout: Some(DEVICE_POLL_TIMEOUT),
        })
        .map_err(|error| format!("poll Bevy render device: {error}"))?;
    Ok(())
}

fn encode_png(image: Image) -> Result<Vec<u8>, String> {
    let rgb = image
        .try_into_dynamic()
        .map_err(|error| format!("decode Bevy screenshot: {error}"))?
        .to_rgb8();
    let mut bytes = Vec::new();
    {
        let mut encoder = png::Encoder::new(&mut bytes, rgb.width(), rgb.height());
        encoder.set_color(png::ColorType::Rgb);
        encoder.set_depth(png::BitDepth::Eight);
        let mut writer = encoder
            .write_header()
            .map_err(|error| format!("encode Bevy screenshot header: {error}"))?;
        writer
            .write_image_data(rgb.as_raw())
            .map_err(|error| format!("encode Bevy screenshot pixels: {error}"))?;
    }
    Ok(bytes)
}

fn create_parent(path: &Path) -> Result<(), String> {
    if let Some(parent) = path
        .parent()
        .filter(|parent| !parent.as_os_str().is_empty())
    {
        std::fs::create_dir_all(parent)
            .map_err(|error| format!("create {}: {error}", parent.display()))?;
    }
    Ok(())
}

fn setup_offscreen_scene(
    mut commands: Commands,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<ColorMaterial>>,
    snapshot: Res<SceneSnapshot>,
    target: Res<CaptureTarget>,
) {
    let (transform, bloom, chromatic, lens) = camera_state(&snapshot.0);
    commands.spawn((
        Camera2d,
        Hdr,
        RenderTarget::Image(target.0.clone().into()),
        transform,
        bloom,
        chromatic,
        lens,
    ));
    spawn_snapshot_scene(&mut commands, &mut meshes, &mut materials, &snapshot.0);
}

fn setup_visible_scene(mut commands: Commands, snapshot: Res<SceneSnapshot>) {
    let (transform, bloom, chromatic, lens) = camera_state(&snapshot.0);
    commands.spawn((Camera2d, Hdr, transform, bloom, chromatic, lens));
}

fn advance_visible_scene(
    mut commands: Commands,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<ColorMaterial>>,
    visuals: Query<Entity, With<SceneVisual>>,
    mut camera: Single<
        (
            &mut Transform,
            &mut Bloom,
            &mut ChromaticAberration,
            &mut LensDistortion,
        ),
        With<Camera2d>,
    >,
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
    let (transform, bloom, chromatic, lens) = camera_state(snapshot);
    *camera.0 = transform;
    *camera.1 = bloom;
    *camera.2 = chromatic;
    *camera.3 = lens;
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
        if !is_project_display(monitor) {
            eprintln!(
                "primary window was not associated with the configured project display; window remained hidden"
            );
            exit.write(AppExit::error());
            return;
        }
        primary.0.visible = true;
        lifetime.shown = true;
        println!(
            "{{\"event\":\"windowPlacementVerified\",\"width\":{},\"height\":{},\"x\":{},\"y\":{}}}",
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

fn is_project_display(monitor: &Monitor) -> bool {
    monitor.physical_position == PROJECT_DISPLAY_POSITION
        && monitor.physical_size() == PROJECT_DISPLAY_SIZE
}

fn camera_state(
    snapshot: &MatchSnapshot,
) -> (Transform, Bloom, ChromaticAberration, LensDistortion) {
    let player_nudge = snapshot
        .players
        .iter()
        .map(|player| player.velocity_x_milli_per_second)
        .sum::<i32>() as f32
        / 600_000.0;
    let explosion_age = snapshot
        .explosions
        .last()
        .map(|explosion| snapshot.tick.saturating_sub(explosion.tick));
    let flash = explosion_age.map(flash_envelope).unwrap_or(0.0);
    let shock = explosion_age.map(shock_envelope).unwrap_or(0.0);
    let shake_x = (snapshot.tick as f32 * 2.31).sin() * (7.0 * flash + 24.0 * shock);
    let shake_y = (snapshot.tick as f32 * 1.73).cos() * (5.0 * flash + 16.0 * shock);
    let transform = Transform::from_xyz(player_nudge.clamp(-5.0, 5.0) + shake_x, shake_y, 0.0);
    let bloom = Bloom {
        intensity: 0.12 + flash * 0.25 + shock * 0.12,
        ..Bloom::NATURAL
    };
    let chromatic = ChromaticAberration {
        intensity: flash * 0.025 + shock * 0.115,
        max_samples: 20,
        ..default()
    };
    let lens = LensDistortion {
        intensity: flash * -0.04 + shock * -0.30,
        scale: 1.0 + flash * 0.015 + shock * 0.10,
        ..default()
    };
    (transform, bloom, chromatic, lens)
}

fn flash_envelope(age: u32) -> f32 {
    (1.0 - age as f32 / 36.0).clamp(0.0, 1.0)
}

fn shock_envelope(age: u32) -> f32 {
    if !(12..=84).contains(&age) {
        0.0
    } else if age <= 48 {
        (age - 12) as f32 / 36.0
    } else {
        (1.0 - (age - 48) as f32 / 36.0).max(0.0)
    }
}

fn spawn_snapshot_scene(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    let profile = snapshot
        .profile
        .parse::<ReplayProfile>()
        .unwrap_or(ReplayProfile::TealDuelReplay);
    let timber_scene = profile == ReplayProfile::TimberCollapseReplay;
    let circle = meshes.add(Circle::new(22.0));
    let block_ring = meshes.add(Annulus::new(29.0, 33.0));
    let bullet = meshes.add(Circle::new(5.0));

    commands.spawn((
        SceneVisual,
        Sprite::from_color(
            if timber_scene {
                Color::srgb_u8(2, 32, 49)
            } else {
                Color::srgb_u8(2, 48, 54)
            },
            Vec2::new(1_280.0, 720.0),
        ),
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
                if timber_scene && index % 2 == 0 {
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

    for surface in &snapshot.arena {
        let x = surface.center_x_milli as f32 / 1_000.0;
        let y = surface.center_y_milli as f32 / 1_000.0;
        let width = surface.width_milli as f32 / 1_000.0;
        let height = surface.height_milli as f32 / 1_000.0;
        if timber_scene {
            spawn_timber_floor(
                commands,
                meshes,
                materials,
                snapshot,
                Vec2::new(x, y),
                Vec2::new(width, height),
            );
            continue;
        }
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

    for constraint in snapshot.constraints.iter().filter(|constraint| {
        constraint.active && constraint.kind == rounds_sim::ConstraintKind::Rope
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
                Color::srgba_u8(130, 79, 54, 170),
                Vec2::new(segment.length(), 2.0),
            ),
            Transform::from_xyz(anchor.x + segment.x * 0.5, anchor.y + segment.y * 0.5, -4.0)
                .with_rotation(Quat::from_rotation_z(segment.y.atan2(segment.x))),
        ));
    }

    for body in &snapshot.dynamic_bodies {
        let x = body.x_milli as f32 / 1_000.0;
        let y = body.y_milli as f32 / 1_000.0;
        let rotation = body.rotation_milliradians as f32 / 1_000.0;
        let color = Color::srgb_u8(body.face_rgb[0], body.face_rgb[1], body.face_rgb[2]);
        let (width, height) = match body.shape {
            DynamicBodyShape::Timber => (
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
                Color::srgba_u8(0, 8, 25, 125),
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

    if let Some(explosion) = snapshot.explosions.last() {
        let age = snapshot.tick.saturating_sub(explosion.tick);
        if age <= 48 {
            let center = Vec2::new(
                explosion.x_milli as f32 / 1_000.0,
                explosion.y_milli as f32 / 1_000.0,
            );
            let flash = flash_envelope(age);
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
                    Mesh2d(meshes.add(Circle::new(7.0 + flash * 8.0))),
                    MeshMaterial2d(materials.add(Color::linear_rgba(
                        4.5 * flash,
                        3.2 * flash,
                        0.8 * flash,
                        0.95,
                    ))),
                    Transform::from_xyz(center.x, center.y, 22.0),
                ));
                for lobe in 0..19 {
                    let angle = lobe as f32 * 2.399 + age as f32 * 0.018;
                    let distance = 7.0
                        + (lobe % 5) as f32 * 4.4
                        + age as f32 * (0.24 + (lobe % 3) as f32 * 0.07);
                    let size = (8.0 + (lobe * 7 % 13) as f32) * flash.powf(0.65);
                    let green = if lobe % 3 == 0 { 2.4 } else { 1.1 };
                    commands.spawn((
                        SceneVisual,
                        Mesh2d(meshes.add(Circle::new(size.max(1.5)))),
                        MeshMaterial2d(materials.add(Color::linear_rgba(
                            3.2 * flash,
                            green * flash,
                            0.05,
                            0.78,
                        ))),
                        Transform::from_xyz(
                            center.x + angle.cos() * distance,
                            center.y + angle.sin() * distance,
                            20.0 + (lobe % 2) as f32,
                        ),
                    ));
                }
            }
            for spark in 0..72 {
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
            for fragment in 0..18 {
                let angle = fragment as f32 * 1.71 + 0.23;
                let speed = 0.8 + (fragment % 6) as f32 * 0.28;
                let distance = 12.0 + age as f32 * speed;
                commands.spawn((
                    SceneVisual,
                    Sprite::from_color(
                        if fragment % 3 == 0 {
                            Color::srgb_u8(92, 29, 38)
                        } else {
                            Color::linear_rgba(2.8, 0.32, 0.04, 0.9)
                        },
                        Vec2::new(3.0 + (fragment % 4) as f32 * 1.5, 2.0),
                    ),
                    Transform::from_xyz(
                        center.x + angle.cos() * distance,
                        center.y + angle.sin() * distance - age as f32 * age as f32 * 0.013,
                        19.0,
                    )
                    .with_rotation(Quat::from_rotation_z(angle + age as f32 * 0.08)),
                ));
            }
        }
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

fn spawn_timber_floor(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
    center: Vec2,
    size: Vec2,
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
            Color::srgba_u8(0, 8, 34, 205),
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
            Color::srgba_u8(0, 8, 34, 205),
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
            Color::srgb_u8(first_color[0], first_color[1], first_color[2]),
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
            Color::srgb_u8(second_color[0], second_color[1], second_color[2]),
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

fn spawn_triangle(
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

#[cfg(test)]
mod tests {
    use super::*;
    use rounds_sim::{TIMBER_IMPACT_TICK, hash_snapshot, run_scripted_match};

    #[test]
    fn bevy_offscreen_renderer_captures_the_peak_builtin_shock() {
        let (snapshot, state_hash) = run_scripted_match(40, TIMBER_IMPACT_TICK + 48);
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
        assert_eq!(hash_snapshot(&snapshot), state_hash);
        assert_eq!(snapshot.explosions.len(), 1);
        let (_, bloom, chromatic, lens) = camera_state(&snapshot);
        assert!(bloom.intensity >= 0.23);
        assert!(chromatic.intensity >= 0.11);
        assert!(lens.intensity <= -0.29);
        std::fs::remove_file(path).unwrap();
    }
}
