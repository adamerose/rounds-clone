use bevy::{
    app::SubApps,
    asset::{RenderAssetUsages, load_internal_asset, uuid_handle},
    camera::{Hdr, RenderTarget},
    core_pipeline::{Core2dSystems, FullscreenShader, schedule::Core2d},
    image::Image,
    post_process::{
        bloom::Bloom,
        effect_stack::{ChromaticAberration, LensDistortion},
    },
    prelude::*,
    render::{
        ExtractSchedule, MainWorld, RenderApp, RenderPlugin, RenderStartup,
        extract_component::{
            ComponentUniforms, DynamicUniformIndex, ExtractComponent, ExtractComponentPlugin,
            UniformComponentPlugin,
        },
        render_resource::{
            BindGroup, BindGroupEntries, BindGroupLayoutDescriptor, BindGroupLayoutEntries,
            CachedRenderPipelineId, ColorTargetState, ColorWrites, Extent3d, FragmentState,
            Operations, PipelineCache, PollType, RenderPassColorAttachment, RenderPassDescriptor,
            RenderPipelineDescriptor, Sampler, SamplerBindingType, SamplerDescriptor, ShaderStages,
            ShaderType, TextureDimension, TextureFormat, TextureSampleType, TextureUsages,
            TextureViewId,
            binding_types::{sampler, texture_2d, uniform_buffer},
        },
        renderer::{RenderContext, RenderDevice, ViewQuery},
        view::ViewTarget,
        view::screenshot::{Screenshot, ScreenshotCaptured},
    },
    shader::Shader,
    window::{ExitCondition, Monitor, OnMonitor, PrimaryWindow},
    winit::WinitPlugin,
};
use rounds_sim::{
    AuthoritativeMatch, DynamicBodyShape, FlowAction, FlowCommand, FlowPhase, FlowSnapshot,
    ItemDefinition, ItemId, MatchSnapshot, PlayerInput, ReplayProfile, scripted_inputs_for,
};
use sha2::{Digest, Sha256};
use std::{
    path::Path,
    sync::mpsc::{TryRecvError, sync_channel},
    time::{Duration, Instant},
};

pub const FRAME_WIDTH: u32 = 1_280;
pub const FRAME_HEIGHT: u32 = 720;
pub const RENDERER_IDENTITY: &str =
    "bevy-0.19.1-2d-hdr-shared-scene-single-final-composite-radial-echo";
const CAPTURE_TIMEOUT: Duration = Duration::from_secs(15);
const DEVICE_POLL_TIMEOUT: Duration = Duration::from_secs(2);
const PROJECT_DISPLAY_POSITION: IVec2 = IVec2::new(364, -1_080);
const PROJECT_DISPLAY_SIZE: UVec2 = UVec2::new(1_920, 1_080);
const MONITOR_DISCOVERY_FRAME_LIMIT: u16 = 120;
const REQUIRED_COMPLETE_RENDER_FRAMES: u8 = 2;
const RADIAL_ECHO_SHADER_HANDLE: Handle<Shader> =
    uuid_handle!("1b624fab-9a06-4a96-b054-d334760f910c");

struct RadialEchoPlugin;

impl Plugin for RadialEchoPlugin {
    fn build(&self, app: &mut App) {
        load_internal_asset!(
            app,
            RADIAL_ECHO_SHADER_HANDLE,
            "radial_echo.wgsl",
            Shader::from_wgsl
        );
        app.add_plugins((
            ExtractComponentPlugin::<RadialEchoSettings>::default(),
            UniformComponentPlugin::<RadialEchoSettings>::default(),
        ));
        let Some(render_app) = app.get_sub_app_mut(RenderApp) else {
            return;
        };
        render_app.add_systems(RenderStartup, init_radial_echo_pipeline);
        render_app.add_systems(
            Core2d,
            radial_echo_pass
                .after(Core2dSystems::EarlyPostProcess)
                .before(Core2dSystems::PostProcess),
        );
    }
}

#[derive(Component, Clone, Copy, Default, ExtractComponent, ShaderType)]
struct RadialEchoSettings {
    strength: f32,
    spacing: f32,
    red_offset: f32,
    _padding: f32,
}

#[derive(Resource)]
struct RadialEchoPipeline {
    layout: BindGroupLayoutDescriptor,
    sampler: Sampler,
    pipeline: CachedRenderPipelineId,
}

#[derive(Default)]
struct RadialEchoBindGroupCache(Option<(TextureViewId, BindGroup)>);

fn init_radial_echo_pipeline(
    mut commands: Commands,
    render_device: Res<RenderDevice>,
    fullscreen_shader: Res<FullscreenShader>,
    pipeline_cache: Res<PipelineCache>,
) {
    let layout = BindGroupLayoutDescriptor::new(
        "rounds_radial_echo_bind_group_layout",
        &BindGroupLayoutEntries::sequential(
            ShaderStages::FRAGMENT,
            (
                texture_2d(TextureSampleType::Float { filterable: true }),
                sampler(SamplerBindingType::Filtering),
                uniform_buffer::<RadialEchoSettings>(true),
            ),
        ),
    );
    let sampler = render_device.create_sampler(&SamplerDescriptor::default());
    let pipeline = pipeline_cache.queue_render_pipeline(RenderPipelineDescriptor {
        label: Some("rounds_radial_echo_pipeline".into()),
        layout: vec![layout.clone()],
        vertex: fullscreen_shader.to_vertex_state(),
        fragment: Some(FragmentState {
            shader: RADIAL_ECHO_SHADER_HANDLE,
            targets: vec![Some(ColorTargetState {
                format: TextureFormat::Rgba16Float,
                blend: None,
                write_mask: ColorWrites::ALL,
            })],
            ..default()
        }),
        ..default()
    });
    commands.insert_resource(RadialEchoPipeline {
        layout,
        sampler,
        pipeline,
    });
}

fn radial_echo_pass(
    view: ViewQuery<(
        &ViewTarget,
        &RadialEchoSettings,
        &DynamicUniformIndex<RadialEchoSettings>,
    )>,
    pipeline: Option<Res<RadialEchoPipeline>>,
    pipeline_cache: Res<PipelineCache>,
    uniforms: Res<ComponentUniforms<RadialEchoSettings>>,
    mut cache: Local<RadialEchoBindGroupCache>,
    mut context: RenderContext,
) {
    let Some(pipeline) = pipeline else { return };
    let (target, _, settings_index) = view.into_inner();
    let Some(render_pipeline) = pipeline_cache.get_render_pipeline(pipeline.pipeline) else {
        return;
    };
    let Some(settings_binding) = uniforms.uniforms().binding() else {
        return;
    };
    let post_process = target.post_process_write();
    let bind_group = match &mut cache.0 {
        Some((source, bind_group)) if *source == post_process.source.id() => bind_group,
        cached => {
            let bind_group = context.render_device().create_bind_group(
                "rounds_radial_echo_bind_group",
                &pipeline_cache.get_bind_group_layout(&pipeline.layout),
                &BindGroupEntries::sequential((
                    post_process.source,
                    &pipeline.sampler,
                    settings_binding.clone(),
                )),
            );
            &cached.insert((post_process.source.id(), bind_group)).1
        }
    };
    let mut pass = context
        .command_encoder()
        .begin_render_pass(&RenderPassDescriptor {
            label: Some("rounds_radial_echo_final_composite_pass"),
            color_attachments: &[Some(RenderPassColorAttachment {
                view: post_process.destination,
                depth_slice: None,
                resolve_target: None,
                ops: Operations::default(),
            })],
            depth_stencil_attachment: None,
            timestamp_writes: None,
            occlusion_query_set: None,
            multiview_mask: None,
        });
    pass.set_pipeline(render_pipeline);
    pass.set_bind_group(0, bind_group, &[settings_index.index()]);
    pass.draw(0..3, 0..1);
}

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

#[derive(Component, Clone, Copy, Debug, PartialEq, Eq)]
struct HudScorePip {
    player: u8,
    index: u8,
    filled: bool,
}

#[derive(Component, Clone, Debug, PartialEq, Eq)]
struct HudBadge {
    player: u8,
    label: String,
}

#[derive(Component, Clone, Copy, Debug, PartialEq, Eq)]
struct CardPresentation {
    item: ItemId,
    highlighted: bool,
    selected_offscreen: bool,
}

#[derive(Component, Clone, Copy, Debug, PartialEq, Eq)]
enum CaptureElement {
    Background,
    Character,
    Hand,
    Card,
    CardArt,
    Saw,
}

#[derive(Resource, Clone, Debug, Default, PartialEq, Eq)]
struct CaptureReadiness {
    scene_complete: bool,
    pipelines_ready: bool,
    complete_render_frames: u8,
    visual_count: usize,
    background_count: usize,
    character_count: usize,
    hand_count: usize,
    card_count: usize,
    card_art_count: usize,
    saw_count: usize,
}

impl CaptureReadiness {
    fn ready(&self) -> bool {
        self.scene_complete
            && self.pipelines_ready
            && self.complete_render_frames >= REQUIRED_COMPLETE_RENDER_FRAMES
    }
}

#[derive(Resource)]
struct InteractiveAuthority {
    simulation: AuthoritativeMatch,
    scripts: [Vec<PlayerInput>; 2],
    tick: usize,
    limit: usize,
    automated: bool,
}

/// Maps concrete keyboard input into the same semantic command sent over the
/// network. Presentation never chooses or applies an item itself.
pub fn keyboard_flow_command(key: KeyCode, player: u8, flow: &FlowSnapshot) -> Option<FlowCommand> {
    match key {
        KeyCode::KeyY if flow.phase == FlowPhase::RematchPrompt => Some(FlowCommand {
            phase_revision: flow.phase_revision,
            action: FlowAction::VoteYes,
        }),
        KeyCode::KeyN if flow.phase == FlowPhase::RematchPrompt => Some(FlowCommand {
            phase_revision: flow.phase_revision,
            action: FlowAction::VoteNo,
        }),
        KeyCode::ArrowLeft if flow.phase == FlowPhase::Draft => {
            draft_navigation_command(player, flow, -1)
        }
        KeyCode::ArrowRight if flow.phase == FlowPhase::Draft => {
            draft_navigation_command(player, flow, 1)
        }
        KeyCode::Enter | KeyCode::Space if flow.phase == FlowPhase::Draft => {
            flow.hovered[usize::from(player)].map(|item| FlowCommand {
                phase_revision: flow.phase_revision,
                action: FlowAction::Confirm(item),
            })
        }
        _ => None,
    }
}

pub fn gamepad_flow_command(
    button: GamepadButton,
    player: u8,
    flow: &FlowSnapshot,
) -> Option<FlowCommand> {
    match button {
        GamepadButton::South if flow.phase == FlowPhase::RematchPrompt => Some(FlowCommand {
            phase_revision: flow.phase_revision,
            action: FlowAction::VoteYes,
        }),
        GamepadButton::East if flow.phase == FlowPhase::RematchPrompt => Some(FlowCommand {
            phase_revision: flow.phase_revision,
            action: FlowAction::VoteNo,
        }),
        GamepadButton::DPadLeft if flow.phase == FlowPhase::Draft => {
            draft_navigation_command(player, flow, -1)
        }
        GamepadButton::DPadRight if flow.phase == FlowPhase::Draft => {
            draft_navigation_command(player, flow, 1)
        }
        GamepadButton::South if flow.phase == FlowPhase::Draft => flow.hovered[usize::from(player)]
            .map(|item| FlowCommand {
                phase_revision: flow.phase_revision,
                action: FlowAction::Confirm(item),
            }),
        _ => None,
    }
}

fn draft_navigation_command(
    player: u8,
    flow: &FlowSnapshot,
    direction: isize,
) -> Option<FlowCommand> {
    if flow.active_player != Some(player) {
        return None;
    }
    let index = usize::from(player);
    let offers = &flow.offers[index];
    let current = flow.hovered[index]
        .and_then(|hovered| offers.iter().position(|item| *item == hovered))
        .unwrap_or(0);
    let next = (current as isize + direction).rem_euclid(offers.len() as isize) as usize;
    Some(FlowCommand {
        phase_revision: flow.phase_revision,
        action: FlowAction::Hover(offers[next]),
    })
}

pub fn render_png(snapshot: &MatchSnapshot, output: &Path) -> Result<Vec<u8>, String> {
    render_png_with_readiness(snapshot, output).map(|(bytes, _)| bytes)
}

fn render_png_with_readiness(
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
    .add_plugins(RadialEchoPlugin)
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
            RadialEchoPlugin,
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
) -> Result<(), String> {
    if profile != ReplayProfile::RematchDraftReplay {
        return Err("interactive visible flow requires rematch-draft-replay".to_owned());
    }
    let mut simulation = AuthoritativeMatch::new_with_profile(seed, profile);
    let initial = simulation.snapshot();
    App::new()
        .add_plugins((
            DefaultPlugins.set(WindowPlugin {
                primary_window: None,
                exit_condition: ExitCondition::DontExit,
                ..default()
            }),
            RadialEchoPlugin,
        ))
        .insert_resource(ClearColor(Color::srgb_u8(2, 48, 54)))
        .insert_resource(SceneSnapshot(initial))
        .insert_resource(InteractiveAuthority {
            simulation,
            scripts: scripted_inputs_for(profile, seed, ticks),
            tick: 0,
            limit: ticks as usize,
            automated,
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
        )
        .run()
        .is_success()
        .then_some(())
        .ok_or_else(|| "interactive replay exited before verifying the project display".to_owned())
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

fn wait_for_capture_readiness(sub_apps: &mut SubApps) -> Result<CaptureReadiness, String> {
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

fn update_capture_scene_readiness(
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
    let draft_face = snapshot
        .0
        .flow
        .as_ref()
        .is_some_and(|flow| matches!(flow.phase, FlowPhase::Draft | FlowPhase::Reveal));
    let radial_face = snapshot.0.profile == rounds_sim::RADIAL_REPLAY_PROFILE;
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

fn update_pipeline_readiness(mut main_world: ResMut<MainWorld>, pipelines: Res<PipelineCache>) {
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
        radial_echo_settings(&snapshot.0),
    ));
    spawn_snapshot_scene(&mut commands, &mut meshes, &mut materials, &snapshot.0);
}

fn setup_visible_scene(mut commands: Commands, snapshot: Res<SceneSnapshot>) {
    let (transform, bloom, chromatic, lens) = camera_state(&snapshot.0);
    commands.spawn((
        Camera2d,
        Hdr,
        transform,
        bloom,
        chromatic,
        lens,
        radial_echo_settings(&snapshot.0),
    ));
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
            &mut RadialEchoSettings,
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
    *camera.4 = radial_echo_settings(snapshot);
    spawn_snapshot_scene(&mut commands, &mut meshes, &mut materials, snapshot);
    replay.next += 1;
    if replay.next == replay.snapshots.len() {
        lifetime.frames = 3;
    }
}

#[expect(
    clippy::too_many_arguments,
    reason = "the visible client-host system explicitly exposes its authority, concrete devices, scene assets, and bounded lifetime"
)]
fn advance_interactive_scene(
    mut commands: Commands,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<ColorMaterial>>,
    visuals: Query<Entity, With<SceneVisual>>,
    keys: Res<ButtonInput<KeyCode>>,
    gamepads: Query<&Gamepad>,
    mut authority: ResMut<InteractiveAuthority>,
    mut scene: ResMut<SceneSnapshot>,
    mut lifetime: ResMut<VisibleLifetime>,
    mut camera: Single<
        (
            &mut Transform,
            &mut Bloom,
            &mut ChromaticAberration,
            &mut LensDistortion,
            &mut RadialEchoSettings,
        ),
        With<Camera2d>,
    >,
) {
    if authority.tick >= authority.limit {
        lifetime.frames = lifetime.frames.min(3);
        return;
    }
    let flow = scene.0.flow.as_ref();
    let mut direct = [None, None];
    if let Some(flow) = flow {
        for key in keys.get_just_pressed().copied() {
            let player = flow
                .active_player
                .unwrap_or(if key == KeyCode::Enter { 1 } else { 0 });
            if flow.phase == FlowPhase::RematchPrompt && key == KeyCode::Enter {
                direct[1] = Some(FlowCommand {
                    phase_revision: flow.phase_revision,
                    action: FlowAction::VoteYes,
                });
            } else if let Some(command) = keyboard_flow_command(key, player, flow) {
                direct[usize::from(player)] = Some(command);
            }
        }
        for (player, gamepad) in gamepads.iter().take(2).enumerate() {
            for button in gamepad.get_just_pressed().copied() {
                if let Some(command) = gamepad_flow_command(button, player as u8, flow) {
                    direct[player] = Some(command);
                }
            }
        }
    }
    for substep in 0..10 {
        if authority.tick >= authority.limit {
            break;
        }
        let mut inputs = if authority.automated {
            [
                authority.scripts[0][authority.tick],
                authority.scripts[1][authority.tick],
            ]
        } else {
            [PlayerInput::default(); 2]
        };
        if substep == 0 {
            for player in 0..2 {
                if direct[player].is_some() {
                    inputs[player].flow = direct[player];
                }
            }
        }
        authority.simulation.step(inputs);
        authority.tick += 1;
    }
    scene.0 = authority.simulation.snapshot();
    let (transform, bloom, chromatic, lens) = camera_state(&scene.0);
    *camera.0 = transform;
    *camera.1 = bloom;
    *camera.2 = chromatic;
    *camera.3 = lens;
    *camera.4 = radial_echo_settings(&scene.0);
    for entity in &visuals {
        commands.entity(entity).despawn();
    }
    spawn_snapshot_scene(&mut commands, &mut meshes, &mut materials, &scene.0);
    if authority.tick >= authority.limit {
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
    let yellow = snapshot.profile == rounds_sim::YELLOW_REPLAY_PROFILE;
    let flash = explosion_age
        .map(if yellow {
            yellow_flash_envelope
        } else {
            flash_envelope
        })
        .unwrap_or(0.0);
    let shock = if yellow {
        radial_echo_settings(snapshot).strength
    } else {
        explosion_age.map(shock_envelope).unwrap_or(0.0)
    };
    let shake_x = (snapshot.tick as f32 * 2.31).sin() * (5.0 * flash + 8.0 * shock);
    let shake_y = (snapshot.tick as f32 * 1.73).cos() * (4.0 * flash + 6.0 * shock);
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
        intensity: if yellow {
            flash * -0.025 + shock * -0.045
        } else {
            flash * -0.04 + shock * -0.30
        },
        scale: if yellow {
            1.0 + flash * 0.008 + shock * 0.018
        } else {
            1.0 + flash * 0.015 + shock * 0.10
        },
        ..default()
    };
    (transform, bloom, chromatic, lens)
}

fn spawn_yellow_hud(
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

fn spawn_yellow_result(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    let Some(round) = &snapshot.round else { return };
    if round.phase == rounds_sim::RoundPhase::Combat {
        return;
    }
    let established = round.phase == rounds_sim::RoundPhase::RoundOrange;
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
            spawn_half_disc(commands, meshes, materials, center, color, 51.0, 42.5);
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

fn spawn_half_disc(
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

fn flash_envelope(age: u32) -> f32 {
    (1.0 - age as f32 / 36.0).clamp(0.0, 1.0)
}

fn yellow_flash_envelope(age: u32) -> f32 {
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

fn shock_envelope(age: u32) -> f32 {
    if !(12..=84).contains(&age) {
        0.0
    } else if age <= 48 {
        (age - 12) as f32 / 36.0
    } else {
        (1.0 - (age - 48) as f32 / 36.0).max(0.0)
    }
}

fn radial_echo_settings(snapshot: &MatchSnapshot) -> RadialEchoSettings {
    if snapshot.profile != rounds_sim::YELLOW_REPLAY_PROFILE {
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

fn spawn_yellow_paper(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    tick: u32,
) {
    let drift = tick as f32 * 0.0025;
    for facet in 0..54_u32 {
        let column = facet % 9;
        let row = facet / 9;
        let seed = facet as f32 * 1.731;
        let center = Vec2::new(
            -620.0 + column as f32 * 154.0 + (seed + drift).sin() * 34.0,
            -330.0 + row as f32 * 132.0 + (seed * 0.73 - drift).cos() * 31.0,
        );
        let width = 82.0 + (facet.wrapping_mul(47) % 105) as f32;
        let height = 58.0 + (facet.wrapping_mul(31) % 78) as f32;
        let skew = (seed * 0.41).sin() * 30.0;
        spawn_triangle(
            commands,
            meshes,
            materials,
            [
                center + Vec2::new(-width * 0.5, -height * 0.5),
                center + Vec2::new(width * 0.5, -height * 0.42),
                center + Vec2::new(skew, height * 0.5),
            ],
            if facet % 3 == 0 {
                Color::srgba_u8(9, 77, 78, 16)
            } else {
                Color::srgba_u8(0, 21, 48, 22)
            },
            -94.0 + (facet % 3) as f32,
        );
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
    let draft_replay = profile == ReplayProfile::RematchDraftReplay;
    let radial_replay = profile == ReplayProfile::RadialSawHalfBlueReplay;
    let yellow_replay = profile == ReplayProfile::YellowCrateTerminalBlastReplay;
    let fighter_scale = if yellow_replay { 0.62 } else { 1.0 };
    let circle = meshes.add(Circle::new(22.0 * fighter_scale));
    let block_ring = meshes.add(Annulus::new(29.0 * fighter_scale, 33.0 * fighter_scale));
    let bullet = meshes.add(Circle::new(5.0));

    commands.spawn((
        SceneVisual,
        CaptureElement::Background,
        Sprite::from_color(
            if radial_replay {
                Color::srgb_u8(188, 238, 229)
            } else if yellow_replay {
                Color::srgb_u8(2, 43, 54)
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
    if yellow_replay {
        spawn_yellow_paper(commands, meshes, materials, snapshot.tick);
    } else if !radial_replay {
        let drift = snapshot.tick as f32 * 0.035;
        for (index, x) in [-520.0_f32, -260.0, 0.0, 260.0, 520.0]
            .into_iter()
            .enumerate()
        {
            let offset = (drift + index as f32 * 1.7).sin() * 28.0;
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

    if let Some(flow) = &snapshot.flow
        && matches!(
            flow.phase,
            FlowPhase::ArenaFade
                | FlowPhase::Draft
                | FlowPhase::Reveal
                | FlowPhase::Handoff
                | FlowPhase::ArenaTransition
        )
    {
        spawn_draft_scene(commands, meshes, materials, snapshot);
        spawn_flow_hud(commands, meshes, materials, snapshot);
        return;
    }

    for surface in &snapshot.arena {
        if radial_replay && surface.id >= 6 {
            continue;
        }
        let x = surface.center_x_milli as f32 / 1_000.0;
        let y = surface.center_y_milli as f32 / 1_000.0;
        let width = surface.width_milli as f32 / 1_000.0;
        let height = surface.height_milli as f32 / 1_000.0;
        let rotation = surface.rotation_milliradians as f32 / 1_000.0;
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

    if let Some(explosion) = snapshot.explosions.last().filter(|_| {
        snapshot
            .round
            .as_ref()
            .map(|round| round.phase == rounds_sim::RoundPhase::Combat)
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
            let flash = if yellow_replay {
                yellow_flash_envelope(age)
            } else {
                flash_envelope(age)
            };
            if explosion.id >= 10_000 && flash > 0.0 && !yellow_replay {
                for wedge in 0..18 {
                    let angle = wedge as f32 * std::f32::consts::TAU / 18.0;
                    let radius = (90.0 + age as f32 * 13.0) * flash.sqrt();
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
        let y = player.y_milli as f32 / 1_000.0 - if yellow_replay { 32.0 } else { 0.0 };
        let body_color = if player.hit_flash_ticks > 0 && !yellow_replay {
            Color::WHITE
        } else if !player.alive {
            Color::srgba_u8(96, 42, 54, 150)
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
        let aim = Vec2::new(f32::from(player.aim_x), f32::from(player.aim_y)).normalize_or(Vec2::X);
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
                if player.id == 0 {
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
            Text2d::new(if !player.alive {
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
                font_size: FontSize::Px(12.0 * fighter_scale),
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

    if radial_replay {
        spawn_radial_impacts(commands, meshes, materials, snapshot);
        spawn_radial_result(commands, meshes, materials, snapshot);
    }

    if yellow_replay {
        spawn_yellow_hud(commands, meshes, materials, snapshot);
        spawn_yellow_result(commands, meshes, materials, snapshot);
    }

    if draft_replay {
        spawn_flow_hud(commands, meshes, materials, snapshot);
    }
}

fn spawn_radial_backdrop(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    tick: u32,
    arena: &[rounds_sim::ArenaSurfaceSnapshot],
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

fn radial_brush_noise(stroke: u32, point: u32, salt: u32) -> f32 {
    ((stroke.wrapping_mul(73) + point.wrapping_mul(41) + salt.wrapping_mul(29)) as f32 * 0.137)
        .sin()
}

fn spawn_radial_surface_finish(
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

fn spawn_radial_saw(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    saw: &rounds_sim::SawSnapshot,
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

fn spawn_radial_impacts(
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

fn spawn_radial_result(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    let Some(round) = &snapshot.round else {
        return;
    };
    if round.phase == rounds_sim::RoundPhase::Combat {
        return;
    }
    let dim = if round.phase == rounds_sim::RoundPhase::HalfBlue {
        0.82
    } else {
        (0.68 + round.phase_tick as f32 / 120.0).clamp(0.68, 0.82)
    };
    commands.spawn((
        SceneVisual,
        Sprite::from_color(
            Color::srgba(0.0, 0.025, 0.08, dim),
            Vec2::new(1_280.0, 720.0),
        ),
        Transform::from_xyz(0.0, 0.0, 40.0),
    ));
    let scale = if round.phase == rounds_sim::RoundPhase::HalfBlue {
        1.0
    } else {
        0.44 + (round.phase_tick as f32 / 29.0).clamp(0.0, 1.0) * 0.56
    };
    let radius = 67.0 * scale;
    for player in 0..2 {
        let color = if player == 0 {
            Color::srgb_u8(255, 169, 18)
        } else {
            Color::srgb_u8(35, 184, 255)
        };
        let center = Vec2::new(-96.0 + player as f32 * 192.0, -22.0);
        commands.spawn((
            SceneVisual,
            Mesh2d(meshes.add(Circle::new(radius - 2.0))),
            MeshMaterial2d(materials.add(Color::srgba_u8(0, 12, 35, 230))),
            Transform::from_xyz(center.x, center.y, 42.0),
        ));
        commands.spawn((
            SceneVisual,
            Mesh2d(meshes.add(Annulus::new(radius - 2.0, radius))),
            MeshMaterial2d(materials.add(color)),
            Transform::from_xyz(center.x, center.y, 43.0),
        ));
        if round.scores[player] > 0 {
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
    if round.phase == rounds_sim::RoundPhase::HalfBlue {
        commands.spawn((
            SceneVisual,
            Text2d::new("HALF BLUE"),
            TextFont {
                font_size: FontSize::Px(70.0),
                ..default()
            },
            TextColor(Color::srgb_u8(101, 220, 255)),
            Transform::from_xyz(0.0, 88.0, 44.0),
        ));
    }
}

fn spawn_left_half_disc(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    center: Vec2,
    radius: f32,
    color: Color,
    z: f32,
) {
    for segment in 0..12 {
        let start = std::f32::consts::FRAC_PI_2 + segment as f32 * std::f32::consts::PI / 12.0;
        let end = std::f32::consts::FRAC_PI_2 + (segment + 1) as f32 * std::f32::consts::PI / 12.0;
        spawn_triangle(
            commands,
            meshes,
            materials,
            [
                center,
                center + Vec2::from_angle(start) * radius,
                center + Vec2::from_angle(end) * radius,
            ],
            color,
            z,
        );
    }
}

fn spawn_draft_scene(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    let flow = snapshot
        .flow
        .as_ref()
        .expect("draft profile has flow state");
    let fade_alpha = match flow.phase {
        FlowPhase::ArenaFade => (flow.phase_tick as f32 / 150.0).clamp(0.0, 1.0),
        FlowPhase::ArenaTransition => (1.0 - flow.phase_tick as f32 / 60.0).clamp(0.0, 1.0),
        _ => 1.0,
    };
    commands.spawn((
        SceneVisual,
        Sprite::from_color(
            Color::srgba(0.005, 0.025, 0.05, 0.90 * fade_alpha),
            Vec2::new(1_280.0, 720.0),
        ),
        Transform::from_xyz(0.0, 0.0, 25.0),
    ));
    if matches!(
        flow.phase,
        FlowPhase::ArenaFade | FlowPhase::ArenaTransition
    ) {
        return;
    }
    if flow.phase == FlowPhase::Handoff {
        return;
    }
    let player = flow.active_player.unwrap_or(0);
    let base = if player == 0 {
        Color::srgb_u8(242, 76, 42)
    } else {
        Color::srgb_u8(43, 137, 244)
    };
    let accent = if player == 0 {
        Color::srgb_u8(255, 153, 50)
    } else {
        Color::srgb_u8(78, 204, 255)
    };
    let offers = &flow.offers[usize::from(player)];
    let hovered = flow.hovered[usize::from(player)];
    let focused_index = hovered
        .and_then(|item| offers.iter().position(|offer| *offer == item))
        .unwrap_or(2);
    let focus = (focused_index as f32 - 2.0) / 2.0;
    let reveal_pose = if flow.revealed.is_some() { 1.0 } else { 0.0 };
    let confirmation_progress = if flow.phase == FlowPhase::Reveal {
        (flow.phase_tick as f32 / 18.0).clamp(0.0, 1.0)
    } else {
        0.0
    };
    let confirmation_settled = flow.phase == FlowPhase::Reveal && flow.phase_tick >= 12;
    let breathe = (snapshot.tick as f32 * 0.045).sin() * 5.0;
    commands.spawn((
        SceneVisual,
        CaptureElement::Character,
        Mesh2d(meshes.add(Circle::new(240.0))),
        MeshMaterial2d(materials.add(Color::srgba_u8(
            if player == 0 { 222 } else { 25 },
            if player == 0 { 57 } else { 103 },
            if player == 0 { 34 } else { 211 },
            230,
        ))),
        Transform::from_xyz(0.0, -265.0 + breathe, 30.0).with_scale(Vec3::new(1.55, 1.0, 1.0)),
    ));
    for side in [-1.0_f32, 1.0] {
        let focus_affinity = (1.0 - (focus - side).abs() * 0.5).clamp(0.0, 1.0);
        let hand_x = side * (490.0 - reveal_pose * 42.0) + focus * 18.0;
        let hand_y = -25.0 + breathe + focus_affinity * 32.0 + reveal_pose * 48.0;
        commands.spawn((
            SceneVisual,
            CaptureElement::Hand,
            Sprite::from_color(base, Vec2::new(62.0, 340.0)),
            Transform::from_xyz(
                side * (425.0 - reveal_pose * 25.0),
                -190.0 + hand_y * 0.18,
                38.0,
            )
            .with_rotation(Quat::from_rotation_z(
                side * (-0.24 - focus_affinity * 0.06 - reveal_pose * 0.08),
            )),
        ));
        commands.spawn((
            SceneVisual,
            CaptureElement::Hand,
            Mesh2d(meshes.add(Circle::new(43.0))),
            MeshMaterial2d(materials.add(accent)),
            Transform::from_xyz(hand_x, hand_y, 42.0),
        ));
    }
    commands.spawn((
        SceneVisual,
        Sprite::from_color(Color::srgb_u8(229, 224, 207), Vec2::new(182.0, 92.0)),
        Transform::from_xyz(0.0, -125.0 + breathe, 37.0),
    ));
    commands.spawn((
        SceneVisual,
        Sprite::from_color(Color::srgb_u8(32, 32, 39), Vec2::new(230.0, 38.0)),
        Transform::from_xyz(0.0, -64.0 + breathe, 41.0),
    ));
    for side in [-1.0_f32, 1.0] {
        commands.spawn((
            SceneVisual,
            Mesh2d(meshes.add(Circle::new(14.0))),
            MeshMaterial2d(materials.add(Color::WHITE)),
            Transform::from_xyz(side * 48.0, -135.0 + breathe, 42.0),
        ));
        commands.spawn((
            SceneVisual,
            Mesh2d(meshes.add(Circle::new(6.0))),
            MeshMaterial2d(materials.add(Color::srgb_u8(25, 31, 37))),
            Transform::from_xyz(
                side * 46.0 + focus * 6.0,
                -137.0 + breathe + focus.abs() * 2.0 - reveal_pose * 4.0,
                43.0,
            ),
        ));
    }
    commands.spawn((
        SceneVisual,
        if reveal_pose > 0.0 {
            Mesh2d(meshes.add(Circle::new(13.0)))
        } else {
            Mesh2d(meshes.add(RegularPolygon::new(11.0, 4)))
        },
        MeshMaterial2d(materials.add(Color::srgb_u8(45, 28, 31))),
        Transform::from_xyz(focus * 3.0, -170.0 + breathe, 43.0).with_scale(Vec3::new(
            1.5,
            if reveal_pose > 0.0 { 1.0 } else { 0.25 },
            1.0,
        )),
    ));

    for (index, item_id) in offers.iter().enumerate() {
        let item = flow
            .catalog
            .iter()
            .find(|item| item.id == *item_id)
            .expect("offered item registered");
        let centered = index as f32 - 2.0;
        let selected_offscreen = confirmation_settled && flow.revealed == Some(*item_id);
        let highlighted = if flow.phase == FlowPhase::Reveal {
            !confirmation_settled && flow.revealed == Some(*item_id)
        } else {
            hovered == Some(*item_id)
        };
        let angle = centered * -0.10;
        let x = centered * 190.0;
        let y = 73.0 - centered.abs().powf(1.35) * 22.0 + if highlighted { 72.0 } else { 0.0 }
            - confirmation_progress * 135.0
            - if selected_offscreen { 520.0 } else { 0.0 };
        spawn_card(
            commands,
            meshes,
            materials,
            item,
            Vec2::new(x, y),
            angle,
            highlighted,
            flow.revealed == Some(*item_id),
            50.0 + index as f32,
            CardPresentation {
                item: *item_id,
                highlighted,
                selected_offscreen,
            },
        );
    }
}

#[expect(
    clippy::too_many_arguments,
    reason = "the card projection keeps renderer assets, item data, and five independent pose cues explicit"
)]
fn spawn_card(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    item: &ItemDefinition,
    position: Vec2,
    angle: f32,
    highlighted: bool,
    revealed: bool,
    z: f32,
    presentation: CardPresentation,
) {
    let scale = if revealed {
        1.20
    } else if highlighted {
        1.10
    } else {
        0.92
    };
    let alpha = if highlighted { 255 } else { 145 };
    let palette = item.palette_rgb;
    commands.spawn((
        SceneVisual,
        Sprite::from_color(Color::srgba_u8(0, 5, 15, 170), Vec2::new(170.0, 272.0)),
        Transform::from_xyz(position.x + 12.0, position.y - 15.0, z - 1.0)
            .with_rotation(Quat::from_rotation_z(angle))
            .with_scale(Vec3::splat(scale)),
    ));
    commands.spawn((
        SceneVisual,
        CaptureElement::Card,
        presentation,
        Sprite::from_color(
            Color::srgba_u8(palette[0] / 5, palette[1] / 5, palette[2] / 5, alpha),
            Vec2::new(166.0, 268.0),
        ),
        Transform::from_xyz(position.x, position.y, z)
            .with_rotation(Quat::from_rotation_z(angle))
            .with_scale(Vec3::splat(scale)),
    ));
    commands.spawn((
        SceneVisual,
        Sprite::from_color(
            Color::srgba_u8(palette[0], palette[1], palette[2], alpha),
            Vec2::new(154.0, 5.0),
        ),
        Transform::from_xyz(position.x, position.y + 124.0 * scale, z + 1.0)
            .with_rotation(Quat::from_rotation_z(angle))
            .with_scale(Vec3::splat(scale)),
    ));
    spawn_card_art(
        commands,
        meshes,
        materials,
        item,
        position,
        angle,
        scale,
        alpha,
        z + 2.0,
    );
    commands.spawn((
        SceneVisual,
        Text2d::new(item.title.clone()),
        TextFont {
            font_size: FontSize::Px(if item.title.len() > 12 { 14.0 } else { 18.0 }),
            ..default()
        },
        TextColor(Color::srgba_u8(255, 255, 246, alpha)),
        TextLayout::justify(Justify::Center),
        Transform::from_xyz(position.x, position.y + 104.0, z + 3.0)
            .with_rotation(Quat::from_rotation_z(angle))
            .with_scale(Vec3::splat(scale)),
    ));
    commands.spawn((
        SceneVisual,
        Text2d::new(wrapped_rules(item)),
        TextFont {
            font_size: FontSize::Px(10.0),
            ..default()
        },
        TextColor(Color::srgba_u8(240, 245, 238, alpha)),
        TextLayout::justify(Justify::Center),
        Transform::from_xyz(position.x, position.y - 43.0, z + 3.0)
            .with_rotation(Quat::from_rotation_z(angle))
            .with_scale(Vec3::splat(scale)),
    ));
}

#[expect(
    clippy::too_many_arguments,
    reason = "card art is projected from one definition into the existing card pose"
)]
fn spawn_card_art(
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
        unknown => panic!("unregistered card art key {unknown}"),
    }
}

fn card_art_transform(
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
fn spawn_art_circle(
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
fn spawn_art_polygon(
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
fn spawn_art_bar(
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

fn wrapped_rules(item: &ItemDefinition) -> String {
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

fn spawn_flow_hud(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    let flow = snapshot
        .flow
        .as_ref()
        .expect("draft profile has flow state");
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
                if *vote == rounds_sim::RematchVote::Yes {
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
            let filled = index < flow.scores[player];
            let color = if player == 0 {
                Color::srgb_u8(255, 116, 45)
            } else {
                Color::srgb_u8(76, 184, 255)
            };
            let mesh = if filled {
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
                    -610.0 + index as f32 * 22.0,
                    330.0 - player as f32 * 25.0,
                    35.0,
                ),
            ));
        }
        let badges = flow.prior_badges[player]
            .iter()
            .map(|badge| badge.label())
            .chain(flow.loadouts[player].iter().map(|item| item.short_badge()));
        for (index, badge) in badges.enumerate() {
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
                    552.0 + (index % 3) as f32 * 34.0,
                    326.0 - player as f32 * 88.0 - (index / 3) as f32 * 30.0,
                    35.0,
                ),
            ));
        }
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
    use bevy::ecs::system::SystemState;
    use rounds_sim::{TIMBER_IMPACT_TICK, hash_snapshot, run_scripted_match};

    fn draft_scene_at(tick: u32) -> World {
        let snapshot = rounds_sim::run_profile_snapshots(
            ReplayProfile::RematchDraftReplay,
            rounds_sim::SOURCE_DRAFT_SEED,
            tick,
        )
        .pop()
        .unwrap();
        let mut world = World::new();
        world.init_resource::<Assets<Mesh>>();
        world.init_resource::<Assets<ColorMaterial>>();
        let mut state = SystemState::<(
            Commands,
            ResMut<Assets<Mesh>>,
            ResMut<Assets<ColorMaterial>>,
        )>::new(&mut world);
        {
            let (mut commands, mut meshes, mut materials) = state.get_mut(&mut world).unwrap();
            spawn_snapshot_scene(&mut commands, &mut meshes, &mut materials, &snapshot);
        }
        state.apply(&mut world);
        world
    }

    fn assert_empty_score_and_badges(world: &mut World, expected_badges: &[(u8, &str)]) {
        let mut pips = world
            .query::<&HudScorePip>()
            .iter(world)
            .copied()
            .collect::<Vec<_>>();
        pips.sort_by_key(|pip| (pip.player, pip.index));
        assert_eq!(pips.len(), 10);
        assert!(pips.iter().all(|pip| !pip.filled));
        assert_eq!(
            pips.iter()
                .map(|pip| (pip.player, pip.index))
                .collect::<Vec<_>>(),
            (0..2)
                .flat_map(|player| (0..5).map(move |index| (player, index)))
                .collect::<Vec<_>>()
        );

        let mut badges = world
            .query::<&HudBadge>()
            .iter(world)
            .map(|badge| (badge.player, badge.label.clone()))
            .collect::<Vec<_>>();
        badges.sort();
        let mut expected = expected_badges
            .iter()
            .map(|(player, label)| (*player, (*label).to_owned()))
            .collect::<Vec<_>>();
        expected.sort();
        assert_eq!(badges, expected);
    }

    fn card_state(world: &mut World, item: ItemId) -> CardPresentation {
        world
            .query::<&CardPresentation>()
            .iter(world)
            .copied()
            .find(|card| card.item == item)
            .unwrap()
    }

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

    #[test]
    fn yellow_sequence_uses_the_shared_gpu_scene_and_preserves_its_visual_signature() {
        let snapshot_at = |tick| {
            rounds_sim::run_profile_match(ReplayProfile::YellowCrateTerminalBlastReplay, 43, tick).0
        };
        let signature_at = |tick| {
            let snapshot = snapshot_at(tick);
            let path = std::env::temp_dir().join(format!(
                "rounds-yellow-signature-{}-{tick}.png",
                std::process::id()
            ));
            let _ = std::fs::remove_file(&path);
            let frame = render_png(&snapshot, &path).unwrap();
            let signature = yellow_frame_signature(&frame).unwrap();
            std::fs::remove_file(path).unwrap();
            signature
        };
        let calm = snapshot_at(rounds_sim::YELLOW_LAST_CALM_TICK);
        let peak = snapshot_at(rounds_sim::YELLOW_PEAK_ECHO_TICK);
        assert_eq!(radial_echo_settings(&calm).strength, 0.0);
        assert_eq!(radial_echo_settings(&peak).strength, 1.0);
        assert!(peak.explosions.iter().any(|explosion| explosion.tick == 81));

        let onset = signature_at(rounds_sim::YELLOW_IMPACT_TICK);
        assert!((1..150).contains(&onset.hot_core_pixels));
        assert!((1_050..1_160).contains(&onset.hot_core_centroid_x.unwrap()));
        assert!((125..190).contains(&onset.hot_core_centroid_y.unwrap()));
        let burst = signature_at(rounds_sim::YELLOW_LOCAL_BURST_TICK);
        assert!(burst.hot_core_pixels > 4_000);
        assert!(burst.white_core_pixels > 2_000);
        let peak = signature_at(rounds_sim::YELLOW_PEAK_ECHO_TICK);
        assert!(peak.arena_hard_edge_pixels > 500);
        assert!(peak.white_core_pixels < 1_500);
        assert!(peak.hud_hard_edge_pixels > 0);
        let trails = signature_at(rounds_sim::YELLOW_TRAILS_TICK);
        assert!(trails.arena_hard_edge_pixels > 900);
        assert!(trails.hud_hard_edge_pixels > 50);
        let result = signature_at(rounds_sim::YELLOW_RESULT_ONSET_TICK);
        assert!(result.result_orange_left_pixels > 3_000);
        assert!(result.result_orange_left_pixels > result.result_orange_right_pixels * 3);
        let established = signature_at(rounds_sim::YELLOW_ROUND_ORANGE_TICK);
        let tail = signature_at(rounds_sim::YELLOW_REPLAY_TICKS);
        assert!(tail.result_orange_left_pixels < established.result_orange_left_pixels / 2);
    }

    #[test]
    fn keyboard_and_controller_map_to_authoritative_draft_commands() {
        let snapshots = rounds_sim::run_profile_snapshots(
            ReplayProfile::RematchDraftReplay,
            rounds_sim::SOURCE_DRAFT_SEED,
            600,
        );
        let flow = snapshots.last().unwrap().flow.as_ref().unwrap();
        let keyboard = keyboard_flow_command(KeyCode::ArrowRight, 0, flow).unwrap();
        let controller = gamepad_flow_command(GamepadButton::DPadRight, 0, flow).unwrap();
        assert_eq!(keyboard, controller);
        assert_eq!(keyboard.phase_revision, flow.phase_revision);
        assert!(matches!(keyboard.action, FlowAction::Hover(_)));
    }

    #[test]
    fn repeated_draft_capture_waits_for_the_same_complete_scene() {
        let snapshot = rounds_sim::run_profile_snapshots(
            ReplayProfile::RematchDraftReplay,
            rounds_sim::SOURCE_DRAFT_SEED,
            600,
        )
        .pop()
        .unwrap();
        let flow_before = rounds_sim::flow_digest(snapshot.flow.as_ref().unwrap());
        let loadout_before = rounds_sim::loadout_digest(snapshot.flow.as_ref().unwrap());
        let directory = std::env::temp_dir();
        let first_path = directory.join(format!(
            "rounds-complete-draft-{}-first.png",
            std::process::id()
        ));
        let second_path = directory.join(format!(
            "rounds-complete-draft-{}-second.png",
            std::process::id()
        ));
        let _ = std::fs::remove_file(&first_path);
        let _ = std::fs::remove_file(&second_path);
        let (first, first_ready) = render_png_with_readiness(&snapshot, &first_path).unwrap();
        let (second, second_ready) = render_png_with_readiness(&snapshot, &second_path).unwrap();
        assert_eq!(first, second);
        assert_eq!(first_ready, second_ready);
        assert!(first_ready.ready());
        assert_eq!(first_ready.background_count, 1);
        assert_eq!(first_ready.character_count, 1);
        assert_eq!(first_ready.hand_count, 4);
        assert_eq!(first_ready.card_count, 5);
        assert_eq!(first_ready.card_art_count, 5);
        assert_eq!(
            rounds_sim::flow_digest(snapshot.flow.as_ref().unwrap()),
            flow_before
        );
        assert_eq!(
            rounds_sim::loadout_digest(snapshot.flow.as_ref().unwrap()),
            loadout_before
        );
        std::fs::remove_file(first_path).unwrap();
        std::fs::remove_file(second_path).unwrap();
    }

    #[test]
    fn draft_projection_preserves_focus_confirmation_pips_and_badges() {
        let mut orange_initial = draft_scene_at(540);
        assert!(card_state(&mut orange_initial, ItemId::Combine).highlighted);
        assert_empty_score_and_badges(&mut orange_initial, &[]);

        let mut orange_confirmed = draft_scene_at(840);
        assert!(card_state(&mut orange_confirmed, ItemId::Dazzle).selected_offscreen);
        assert!(
            orange_confirmed
                .query::<&CardPresentation>()
                .iter(&orange_confirmed)
                .all(|card| !card.highlighted)
        );
        assert_empty_score_and_badges(&mut orange_confirmed, &[(0, "Da")]);

        let mut blue_initial = draft_scene_at(960);
        assert!(card_state(&mut blue_initial, ItemId::Dazzle).highlighted);
        assert_empty_score_and_badges(&mut blue_initial, &[(0, "Da")]);

        let mut blue_lifestealer = draft_scene_at(1_560);
        assert!(card_state(&mut blue_lifestealer, ItemId::Lifestealer).highlighted);
        assert_empty_score_and_badges(&mut blue_lifestealer, &[(0, "Da")]);

        let mut blue_echo = draft_scene_at(2_040);
        assert!(card_state(&mut blue_echo, ItemId::Echo).highlighted);
        assert_empty_score_and_badges(&mut blue_echo, &[(0, "Da")]);

        let mut blue_confirmed = draft_scene_at(2_120);
        assert!(card_state(&mut blue_confirmed, ItemId::ExplosiveBullet).selected_offscreen);
        assert!(
            blue_confirmed
                .query::<&CardPresentation>()
                .iter(&blue_confirmed)
                .all(|card| !card.highlighted)
        );
        assert_empty_score_and_badges(&mut blue_confirmed, &[(0, "Da"), (1, "Ex")]);
    }
}
