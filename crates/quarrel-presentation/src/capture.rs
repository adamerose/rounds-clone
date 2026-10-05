use super::runtime::*;
use super::*;
use crate::post_process::SharedScenePlugin;

pub fn render_png(snapshot: &MatchSnapshot, output: &Path) -> Result<Vec<u8>, String> {
    render_png_with_readiness(snapshot, output).map(|(bytes, _)| bytes)
}

pub(super) fn render_png_with_readiness(
    snapshot: &MatchSnapshot,
    output: &Path,
) -> Result<(Vec<u8>, CaptureReadiness), String> {
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
    .add_plugins(SharedScenePlugin)
    .insert_resource(ClearColor(Color::srgb_u8(2, 48, 54)))
    .insert_resource(SceneSnapshot(snapshot.clone()))
    .init_resource::<CaptureReadiness>()
    .add_systems(Startup, setup_offscreen_scene)
    .add_systems(Update, update_capture_scene_readiness);
    app.sub_app_mut(RenderApp)
        .add_systems(ExtractSchedule, update_pipeline_readiness);
    app.finish();
    app.cleanup();
    let mut sub_apps = std::mem::take(app.sub_apps_mut());
    let target = new_render_target(&mut sub_apps, FRAME_WIDTH, FRAME_HEIGHT);
    sub_apps
        .main
        .world_mut()
        .insert_resource(CaptureTarget(target.clone()));

    let readiness = wait_for_capture_readiness(&mut sub_apps)?;
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
    Ok((bytes, readiness))
}

pub fn frame_sha256(frame: &[u8]) -> String {
    format!("{:x}", Sha256::digest(frame))
}

#[derive(Clone, Copy, Debug, serde::Serialize)]
#[serde(rename_all = "camelCase")]
pub struct YellowFrameSignature {
    pub hot_core_pixels: u32,
    pub hot_core_centroid_x: Option<u32>,
    pub hot_core_centroid_y: Option<u32>,
    pub white_core_pixels: u32,
    pub arena_hard_edge_pixels: u32,
    pub hud_hard_edge_pixels: u32,
    pub result_orange_left_pixels: u32,
    pub result_orange_right_pixels: u32,
}

/// Measures the source-bound yellow replay signature from the actual composited GPU PNG.
pub fn yellow_frame_signature(frame: &[u8]) -> Result<YellowFrameSignature, String> {
    let decoder = png::Decoder::new(std::io::Cursor::new(frame));
    let mut reader = decoder
        .read_info()
        .map_err(|error| format!("decode yellow signature PNG header: {error}"))?;
    if reader.info().width != FRAME_WIDTH
        || reader.info().height != FRAME_HEIGHT
        || reader.info().color_type != png::ColorType::Rgb
        || reader.info().bit_depth != png::BitDepth::Eight
    {
        return Err("yellow signature requires a 1280x720 RGB8 capture".to_owned());
    }
    let mut pixels = vec![
        0;
        reader
            .output_buffer_size()
            .ok_or_else(|| "yellow signature PNG has no output size".to_owned())?
    ];
    let info = reader
        .next_frame(&mut pixels)
        .map_err(|error| format!("decode yellow signature PNG pixels: {error}"))?;
    pixels.truncate(info.buffer_size());

    let mut signature = YellowFrameSignature {
        hot_core_pixels: 0,
        hot_core_centroid_x: None,
        hot_core_centroid_y: None,
        white_core_pixels: 0,
        arena_hard_edge_pixels: 0,
        hud_hard_edge_pixels: 0,
        result_orange_left_pixels: 0,
        result_orange_right_pixels: 0,
    };
    let (mut hot_x, mut hot_y) = (0_u64, 0_u64);
    for y in 0..FRAME_HEIGHT {
        for x in 0..FRAME_WIDTH {
            let index = ((y * FRAME_WIDTH + x) * 3) as usize;
            let rgb = [pixels[index], pixels[index + 1], pixels[index + 2]];
            let hot = x > 750 && y < 280 && rgb[0] > 245 && rgb[1] > 210 && rgb[2] > 120;
            if hot {
                signature.hot_core_pixels += 1;
                hot_x += u64::from(x);
                hot_y += u64::from(y);
            }
            if (760..1260).contains(&x)
                && (20..280).contains(&y)
                && rgb.iter().all(|channel| *channel > 220)
            {
                signature.white_core_pixels += 1;
            }
            let orange = rgb[0] > 180 && rgb[1] > 55 && rgb[1] < 210 && rgb[2] < 100;
            if orange && (480..596).contains(&x) && (300..440).contains(&y) {
                if x < 538 {
                    signature.result_orange_left_pixels += 1;
                } else {
                    signature.result_orange_right_pixels += 1;
                }
            }
            if x == 0 {
                continue;
            }
            let previous = index - 3;
            let edge = rgb
                .iter()
                .enumerate()
                .map(|(channel, value)| value.abs_diff(pixels[previous + channel]))
                .max()
                .unwrap_or(0);
            if x < 1_050 && (70..680).contains(&y) && edge > 80 {
                signature.arena_hard_edge_pixels += 1;
            }
            if x < 200 && y < 90 && edge > 50 {
                signature.hud_hard_edge_pixels += 1;
            }
        }
    }
    if signature.hot_core_pixels > 0 {
        signature.hot_core_centroid_x = Some((hot_x / u64::from(signature.hot_core_pixels)) as u32);
        signature.hot_core_centroid_y = Some((hot_y / u64::from(signature.hot_core_pixels)) as u32);
    }
    Ok(signature)
}

/// Runs the same scene model in a real Bevy window. The window starts hidden and
/// is revealed only after Bevy reports the exact configured project display.
pub fn run_visible(snapshots: Vec<MatchSnapshot>) -> Result<(), String> {
    if snapshots.is_empty() {
        return Err("visible replay needs at least one snapshot".to_owned());
    }
    App::new()
        .add_plugins((
            DefaultPlugins.set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            }),
            SharedScenePlugin,
        ))
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

/// Runs a local client-host whose keyboard/controller commands enter the
/// authoritative simulation before the received snapshot is projected.
pub fn run_interactive_visible(
    profile: ReplayProfile,
    seed: u64,
    ticks: u32,
    automated: bool,
) -> Result<MatchSnapshot, String> {
    if profile != ReplayProfile::RematchDraftReplay {
        return Err("interactive visible flow requires rematch-draft-replay".to_owned());
    }
    let mut simulation = AuthoritativeMatch::new_with_profile(seed, profile);
    let initial = simulation.snapshot();
    let (final_state, result) = sync_channel(1);
    let mut app = App::new();
    app.add_plugins((
        DefaultPlugins.set(WindowPlugin {
            primary_window: None,
            exit_condition: ExitCondition::DontExit,
            ..default()
        }),
        SharedScenePlugin,
    ))
    .insert_resource(ClearColor(Color::srgb_u8(2, 48, 54)))
    .insert_resource(SceneSnapshot(initial))
    .insert_resource(InteractiveAuthority {
        simulation,
        scripts: scripted_inputs_for(profile, seed, ticks),
        tick: 0,
        limit: ticks as usize,
        automated,
        tick_budget: 0.0,
        pending_flow: [None; 2],
        final_state,
    })
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
        (verify_monitor_show_and_exit, advance_interactive_scene).chain(),
    );
    if !app.run().is_success() {
        return Err("interactive replay exited before verifying the project display".to_owned());
    }
    result
        .try_recv()
        .map_err(|error| format!("receive final interactive state: {error}"))
}

/// Presents only the newest state accepted from an authority. Input is sent to
/// the assigned client slot; this function never owns a simulation.
pub fn run_live_visible(handle: LiveClientHandle, player: u8) -> Result<(), String> {
    if player > 1 {
        return Err(format!("player {player} is outside 0..=1"));
    }
    let closer = handle.clone();
    let result = App::new()
        .add_plugins((
            DefaultPlugins.set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            }),
            SharedScenePlugin,
        ))
        .insert_resource(ClearColor(Color::srgb_u8(2, 48, 54)))
        .insert_resource(LivePresentation {
            handle,
            player,
            displayed_hash: None,
            displayed_snapshot: None,
        })
        .init_resource::<VisibleWindowRequested>()
        .init_resource::<MonitorDiscovery>()
        .insert_resource(VisibleLifetime {
            frames: u32::MAX,
            shown: false,
        })
        .add_systems(Update, create_monitor_four_window)
        .add_systems(Update, close_live_window)
        .add_systems(
            Update,
            (
                poll_live_snapshot,
                advance_live_scene,
                verify_live_monitor_show,
                submit_live_input,
            )
                .chain(),
        )
        .run();
    closer.close();
    result
        .is_success()
        .then_some(())
        .ok_or_else(|| "live client exited before verifying the project display".to_owned())
}

pub(super) fn create_monitor_four_window(
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

pub(super) fn new_render_target(sub_apps: &mut SubApps, width: u32, height: u32) -> Handle<Image> {
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

pub(super) fn update_and_wait(sub_apps: &mut SubApps) -> Result<(), String> {
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

pub(super) fn wait_for_capture_readiness(
    sub_apps: &mut SubApps,
) -> Result<CaptureReadiness, String> {
    let deadline = Instant::now() + CAPTURE_TIMEOUT;
    loop {
        update_and_wait(sub_apps)?;
        let readiness = sub_apps.main.world().resource::<CaptureReadiness>().clone();
        if readiness.ready() {
            return Ok(readiness);
        }
        if Instant::now() >= deadline {
            return Err(format!(
                "Bevy scene/pipeline readiness timed out: {readiness:?}"
            ));
        }
    }
}

pub(super) fn update_capture_scene_readiness(
    snapshot: Res<SceneSnapshot>,
    cameras: Query<(), With<Camera2d>>,
    visuals: Query<(), With<SceneVisual>>,
    elements: Query<&CaptureElement>,
    mut readiness: ResMut<CaptureReadiness>,
) {
    let mut counts = [0_usize; 6];
    for element in &elements {
        let index = match element {
            CaptureElement::Background => 0,
            CaptureElement::Character => 1,
            CaptureElement::Hand => 2,
            CaptureElement::Card => 3,
            CaptureElement::CardArt => 4,
            CaptureElement::Saw => 5,
        };
        counts[index] += 1;
    }
    readiness.visual_count = visuals.iter().count();
    readiness.background_count = counts[0];
    readiness.character_count = counts[1];
    readiness.hand_count = counts[2];
    readiness.card_count = counts[3];
    readiness.card_art_count = counts[4];
    readiness.saw_count = counts[5];
    let draft_face = snapshot.0.flow.as_ref().is_some_and(|flow| {
        matches!(
            flow.phase,
            FlowPhase::Draft
                | FlowPhase::Reveal
                | FlowPhase::PostRoundDraft
                | FlowPhase::PostRoundReveal
        )
    });
    let radial_face = snapshot.0.profile == quarrel_sim::RADIAL_REPLAY_PROFILE;
    readiness.scene_complete = cameras.iter().count() == 1
        && readiness.visual_count > 0
        && readiness.background_count == 1
        && (!draft_face
            || (readiness.character_count == 1
                && readiness.hand_count == 4
                && readiness.card_count == 5
                && readiness.card_art_count == 5))
        && (!radial_face || readiness.saw_count == 2);
}

pub(super) fn update_pipeline_readiness(
    mut main_world: ResMut<MainWorld>,
    pipelines: Res<PipelineCache>,
) {
    let pipelines_ready = pipelines.waiting_pipelines().next().is_none();
    if let Some(mut readiness) = main_world.get_resource_mut::<CaptureReadiness>() {
        readiness.pipelines_ready = pipelines_ready;
        readiness.complete_render_frames = if readiness.scene_complete && pipelines_ready {
            readiness.complete_render_frames.saturating_add(1)
        } else {
            0
        };
    }
}

pub(super) fn encode_png(image: Image) -> Result<Vec<u8>, String> {
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

pub(super) fn create_parent(path: &Path) -> Result<(), String> {
    if let Some(parent) = path
        .parent()
        .filter(|parent| !parent.as_os_str().is_empty())
    {
        std::fs::create_dir_all(parent)
            .map_err(|error| format!("create {}: {error}", parent.display()))?;
    }
    Ok(())
}
