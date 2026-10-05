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
        ExtractSchedule, MainWorld, RenderApp, RenderPlugin,
        render_resource::{
            Extent3d, PipelineCache, PollType, TextureDimension, TextureFormat, TextureUsages,
        },
        renderer::RenderDevice,
        view::screenshot::{Screenshot, ScreenshotCaptured},
    },
    window::{ExitCondition, Monitor, OnMonitor, PrimaryWindow, WindowClosed},
    winit::WinitPlugin,
};
use quarrel_network::LiveClientHandle;
use quarrel_sim::{
    AuthoritativeMatch, FlowAction, FlowCommand, FlowPhase, FlowSnapshot, ItemDefinition, ItemId,
    MatchSnapshot, PlayerInput,
};
use std::{
    path::Path,
    sync::mpsc::{TryRecvError, sync_channel},
    time::{Duration, Instant},
};

pub const FRAME_WIDTH: u32 = 1_280;
pub const FRAME_HEIGHT: u32 = 720;
pub const RENDERER_IDENTITY: &str = "bevy-0.19.1-data-arena-ordinary-match";
const CAPTURE_TIMEOUT: Duration = Duration::from_secs(15);
const DEVICE_POLL_TIMEOUT: Duration = Duration::from_secs(2);
const PROJECT_DISPLAY_POSITION: IVec2 = IVec2::new(364, -1_080);
const PROJECT_DISPLAY_SIZE: UVec2 = UVec2::new(1_920, 1_080);
const MONITOR_DISCOVERY_FRAME_LIMIT: u16 = 120;
const REQUIRED_COMPLETE_RENDER_FRAMES: u8 = 2;

mod arena;
mod capture;
mod data_arena;
mod draft;
mod hud;
mod input;

mod runtime;
mod scene;

pub use capture::{render_png, run_interactive_visible, run_live_visible, run_visible};
pub use input::{
    gamepad_combat_input, gamepad_flow_command, keyboard_combat_input, keyboard_flow_command,
};

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
    Card,
}
#[derive(Resource, Clone, Debug, Default, PartialEq, Eq)]
struct CaptureReadiness {
    scene_complete: bool,
    pipelines_ready: bool,
    complete_render_frames: u8,
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
    tick: usize,
    limit: usize,
    automated: bool,
    tick_budget: f64,
    pending_flow: Vec<Option<FlowCommand>>,
    final_state: std::sync::mpsc::SyncSender<MatchSnapshot>,
}
#[derive(Resource)]
struct LivePresentation {
    handle: LiveClientHandle,
    player: u8,
    displayed_hash: Option<String>,
    displayed_snapshot: Option<MatchSnapshot>,
}
impl Drop for InteractiveAuthority {
    fn drop(&mut self) {
        let _ = self.final_state.try_send(self.simulation.snapshot());
    }
}
