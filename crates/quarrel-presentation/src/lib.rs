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
    window::{ExitCondition, Monitor, OnMonitor, PrimaryWindow, WindowClosed},
    winit::WinitPlugin,
};
use quarrel_network::LiveClientHandle;
use quarrel_sim::{
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
const ROUND_FONT_HANDLE: Handle<Font> = uuid_handle!("52b1b7b5-ad12-4e7a-8f2b-772aef08b446");

mod arena;
mod capture;
mod card_art;
mod draft;
mod hud;
mod input;
mod post_process;
mod radial;
mod runtime;
mod scene;

pub use capture::{
    YellowFrameSignature, frame_sha256, render_png, run_interactive_visible, run_live_visible,
    run_visible, yellow_frame_signature,
};
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
struct HangingBodyVisual(u16);

#[derive(Component, Clone, Copy, Debug, PartialEq, Eq)]
struct HangingSquareVisual(u16);

#[derive(Component, Clone, Copy, Debug, PartialEq, Eq)]
struct LimeSurfaceVisual(u8);

#[derive(Component, Clone, Copy, Debug, PartialEq, Eq)]
struct HangingLinkVisual {
    body: u16,
    segment: u8,
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
    tick_budget: f64,
    pending_flow: [Option<FlowCommand>; 2],
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
        // Winit consumes the App, so retain its real final state when it drops
        // the authority on either bounded completion or a normal window close.
        let _ = self.final_state.try_send(self.simulation.snapshot());
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use super::{
        arena::{backdrop_panel_offset, spawn_snapshot_scene},
        capture::render_png_with_readiness,
        runtime::{poll_live_snapshot, submit_live_input},
        scene::{camera_state, radial_echo_settings},
    };
    use bevy::ecs::system::SystemState;
    use quarrel_network::{LiveClient, LiveServer};
    use quarrel_sim::{TIMBER_IMPACT_TICK, hash_snapshot, run_scripted_match};
    use std::thread;

    #[test]
    fn windowless_live_bevy_input_stays_in_its_slot_and_needs_a_new_snapshot_to_advance() {
        let profile = ReplayProfile::RematchDraftReplay;
        let seed = quarrel_sim::SOURCE_DRAFT_SEED;
        let ticks = 360;
        let server = LiveServer::bind("127.0.0.1:0").unwrap();
        let address = server.local_addr().unwrap();
        let trace =
            std::env::temp_dir().join(format!("rounds-bevy-input-{}.json", std::process::id()));
        let server_trace = trace.clone();
        let authority = thread::spawn(move || {
            server
                .run(seed, ticks, profile, Some(&server_trace))
                .unwrap()
        });
        let first = LiveClient::connect(address, 0, seed, profile).unwrap();
        let second = LiveClient::connect(address, 1, seed, profile).unwrap();
        let handle = first.handle();
        let first = thread::spawn(move || first.run(ticks).unwrap());
        let second = thread::spawn(move || second.run(ticks).unwrap());

        let mut app = App::new();
        app.add_plugins(MinimalPlugins)
            .init_resource::<ButtonInput<KeyCode>>()
            .insert_resource(VisibleLifetime {
                frames: u32::MAX,
                shown: true,
            })
            .insert_resource(LivePresentation {
                handle: handle.clone(),
                player: 0,
                displayed_hash: None,
                displayed_snapshot: None,
            })
            .add_systems(Update, (poll_live_snapshot, submit_live_input).chain());
        let until = Instant::now() + Duration::from_secs(7);
        while handle.latest().is_none_or(|(state, _)| {
            state
                .flow
                .as_ref()
                .is_none_or(|flow| flow.phase != FlowPhase::RematchPrompt)
        }) {
            assert!(
                Instant::now() < until,
                "authority never reached rematch prompt"
            );
            thread::sleep(Duration::from_millis(2));
        }
        app.update();
        {
            let mut keys = app.world_mut().resource_mut::<ButtonInput<KeyCode>>();
            keys.press(KeyCode::KeyA);
            keys.press(KeyCode::KeyY);
        }
        app.update();
        app.world_mut()
            .resource_mut::<ButtonInput<KeyCode>>()
            .clear_just_pressed(KeyCode::KeyY);
        for _ in 0..3 {
            app.update();
        }
        thread::sleep(Duration::from_millis(80));
        let mut pad = Gamepad::default();
        pad.analog_mut().set(GamepadAxis::LeftStickX, 1.0);
        pad.digital_mut().press(GamepadButton::West);
        app.world_mut().spawn(pad);
        app.update();
        thread::sleep(Duration::from_millis(80));
        app.update();

        assert_eq!(first.join().unwrap().result, "completed");
        assert_eq!(second.join().unwrap().result, "completed");
        assert_eq!(authority.join().unwrap().result, "completed");
        app.update();
        let displayed_tick = app
            .world()
            .resource::<LivePresentation>()
            .displayed_snapshot
            .as_ref()
            .unwrap()
            .tick;
        for _ in 0..3 {
            app.update();
            assert_eq!(
                app.world()
                    .resource::<LivePresentation>()
                    .displayed_snapshot
                    .as_ref()
                    .unwrap()
                    .tick,
                displayed_tick
            );
        }
        let rows: Vec<serde_json::Value> =
            serde_json::from_slice(&std::fs::read(&trace).unwrap()).unwrap();
        std::fs::remove_file(trace).unwrap();
        assert!(rows.iter().any(|row| row["inputs"][0]["move_axis"] == -1));
        assert!(rows.iter().any(|row| row["inputs"][0]["move_axis"] == 1));
        assert!(rows.iter().any(|row| row["inputs"][0]["block"] == true));
        assert!(rows.iter().all(|row| row["inputs"][1]["move_axis"] == 0));
        assert_eq!(
            rows.iter()
                .filter(|row| !row["inputs"][0]["flow"].is_null())
                .count(),
            1
        );
    }

    fn scene_for_snapshot(snapshot: &MatchSnapshot) -> World {
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
            spawn_snapshot_scene(&mut commands, &mut meshes, &mut materials, snapshot);
        }
        state.apply(&mut world);
        world
    }

    fn draft_scene_at(tick: u32) -> World {
        let snapshot = quarrel_sim::run_profile_snapshots(
            ReplayProfile::RematchDraftReplay,
            quarrel_sim::SOURCE_DRAFT_SEED,
            tick,
        )
        .pop()
        .unwrap();
        scene_for_snapshot(&snapshot)
    }

    #[test]
    fn local_and_received_waiting_snapshots_share_the_terminal_hud() {
        let local = quarrel_sim::run_profile_match(
            ReplayProfile::MatchEndWaitingReplay,
            57,
            quarrel_sim::MATCH_END_WAITING_REPLAY_TICKS,
        )
        .0;
        // Transport hands this same typed snapshot to presentation; the network
        // crate separately protects its serialized round trip.
        let received = local.clone();
        for snapshot in [&local, &received] {
            let mut world = scene_for_snapshot(snapshot);
            assert_eq!(
                world
                    .query::<&Text2d>()
                    .iter(&world)
                    .filter(|text| text.0 == "WAITING")
                    .count(),
                1
            );
            let mut filled = world
                .query::<&HudScorePip>()
                .iter(&world)
                .filter(|pip| pip.filled)
                .map(|pip| (pip.player, pip.index))
                .collect::<Vec<_>>();
            filled.sort();
            assert_eq!(
                filled,
                vec![
                    (0, 0),
                    (0, 1),
                    (0, 2),
                    (1, 0),
                    (1, 1),
                    (1, 2),
                    (1, 3),
                    (1, 4),
                ]
            );
            let mut badges = world
                .query::<&HudBadge>()
                .iter(&world)
                .map(|badge| (badge.player, badge.label.clone()))
                .collect::<Vec<_>>();
            badges.sort();
            assert_eq!(badges, vec![(0, "Da".to_owned()), (1, "Ex".to_owned())]);
            assert_eq!(world.query::<&CardPresentation>().iter(&world).count(), 0);
            assert_eq!(
                snapshot
                    .players
                    .iter()
                    .filter(|player| player.alive)
                    .count(),
                2
            );
        }
    }

    #[test]
    fn lime_renderer_consumes_all_authoritative_surfaces_without_draft_ui() {
        let snapshot = quarrel_sim::run_profile_match(
            ReplayProfile::LimeModularArenaReplay,
            59,
            quarrel_sim::LIME_MODULAR_REPLAY_TICKS,
        )
        .0;
        let mut world = scene_for_snapshot(&snapshot);
        let mut ids = world
            .query::<&LimeSurfaceVisual>()
            .iter(&world)
            .map(|visual| visual.0)
            .collect::<Vec<_>>();
        ids.sort_unstable();
        ids.dedup();
        assert_eq!(ids, (0..33).collect::<Vec<_>>());
        assert!(world.query::<&SceneVisual>().iter(&world).count() > 300);
        assert_eq!(world.query::<&CardPresentation>().iter(&world).count(), 0);
        assert_eq!(world.query::<&HudBadge>().iter(&world).count(), 0);
    }

    #[test]
    fn local_and_serialized_new_match_draft_share_the_cleared_orange_scene() {
        let local = quarrel_sim::run_new_match_draft(57).0;
        // The network and simulation regressions protect the serialized round
        // trip; presentation consumes that same typed snapshot without a
        // transport-specific scene path.
        let received = local.clone();
        for snapshot in [&local, &received] {
            let flow = snapshot.flow.as_ref().unwrap();
            assert_eq!(flow.phase, FlowPhase::Draft);
            assert_eq!(flow.active_player, Some(0));
            assert_eq!(flow.scores, [0, 0]);
            assert!(flow.prior_badges.iter().all(Vec::is_empty));
            assert!(flow.loadouts.iter().all(Vec::is_empty));
            assert_eq!(
                snapshot.round.as_ref().unwrap().phase,
                quarrel_sim::RoundPhase::Combat
            );

            let mut world = scene_for_snapshot(snapshot);
            assert_eq!(world.query::<&CardPresentation>().iter(&world).count(), 5);
            assert_eq!(
                world
                    .query::<&CaptureElement>()
                    .iter(&world)
                    .filter(|element| **element == CaptureElement::Character)
                    .count(),
                1
            );
            let texts = world
                .query::<&Text2d>()
                .iter(&world)
                .map(|text| text.0.clone())
                .collect::<Vec<_>>();
            for title in [
                "DAZZLE",
                "STEADY SHOT",
                "TANK",
                "TIMED DETONATION",
                "HOMING",
            ] {
                assert!(texts.iter().any(|text| text == title), "missing {title}");
            }
            for stale in ["WAITING", "ROUND BLUE", "ROUND ORANGE", "REMATCH?"] {
                assert!(!texts.iter().any(|text| text == stale), "retained {stale}");
            }
            assert!(
                world
                    .query::<&HudScorePip>()
                    .iter(&world)
                    .all(|pip| !pip.filled)
            );
            assert_eq!(world.query::<&HudBadge>().iter(&world).count(), 0);
        }
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

    #[test]
    fn held_hanging_backdrop_does_not_drift_behind_measured_object_entry() {
        for index in 0..5 {
            assert_eq!(backdrop_panel_offset(5_894, index, true), 0.0);
            assert_eq!(backdrop_panel_offset(5_918, index, true), 0.0);
            assert_eq!(backdrop_panel_offset(5_941, index, true), 0.0);
        }
        assert_ne!(
            backdrop_panel_offset(5_918, 0, false),
            backdrop_panel_offset(5_941, 0, false)
        );
    }

    #[test]
    fn held_hanging_renderer_consumes_the_authority_layout_once() {
        let mut world = draft_scene_at(quarrel_sim::HELD_HANGING_ENTRY_TICKS);
        let mut bodies = world
            .query::<&HangingBodyVisual>()
            .iter(&world)
            .map(|body| body.0)
            .collect::<Vec<_>>();
        bodies.sort_unstable();
        assert_eq!(bodies, (400..=420).collect::<Vec<_>>());
        let mut squares = world
            .query::<&HangingSquareVisual>()
            .iter(&world)
            .map(|square| square.0)
            .collect::<Vec<_>>();
        squares.sort_unstable();
        assert_eq!(squares, bodies);
        let links = world
            .query::<&HangingLinkVisual>()
            .iter(&world)
            .copied()
            .collect::<Vec<_>>();
        assert_eq!(links.len(), 42);
        assert!((400..=420).all(|id| {
            links.iter().filter(|link| link.body == id).count() == 2
                && links
                    .iter()
                    .filter(|link| link.body == id)
                    .map(|link| link.segment)
                    .collect::<std::collections::BTreeSet<_>>()
                    == [0, 1].into_iter().collect()
        }));
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
            quarrel_sim::run_profile_match(ReplayProfile::YellowCrateTerminalBlastReplay, 43, tick)
                .0
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
        let calm = snapshot_at(quarrel_sim::YELLOW_LAST_CALM_TICK);
        let peak = snapshot_at(quarrel_sim::YELLOW_PEAK_ECHO_TICK);
        assert_eq!(radial_echo_settings(&calm).strength, 0.0);
        assert_eq!(radial_echo_settings(&peak).strength, 1.0);
        assert!(peak.explosions.iter().any(|explosion| explosion.tick == 81));

        let onset = signature_at(quarrel_sim::YELLOW_IMPACT_TICK);
        assert!((1..150).contains(&onset.hot_core_pixels));
        assert!((1_050..1_160).contains(&onset.hot_core_centroid_x.unwrap()));
        assert!((125..190).contains(&onset.hot_core_centroid_y.unwrap()));
        let burst = signature_at(quarrel_sim::YELLOW_LOCAL_BURST_TICK);
        assert!(burst.hot_core_pixels > 4_000);
        assert!(burst.white_core_pixels > 2_000);
        let peak = signature_at(quarrel_sim::YELLOW_PEAK_ECHO_TICK);
        assert!(peak.arena_hard_edge_pixels > 500);
        assert!(peak.white_core_pixels < 1_500);
        assert!(peak.hud_hard_edge_pixels > 0);
        let trails = signature_at(quarrel_sim::YELLOW_TRAILS_TICK);
        assert!(trails.arena_hard_edge_pixels > 900);
        assert!(trails.hud_hard_edge_pixels > 50);
        let last_combat = signature_at(quarrel_sim::YELLOW_LAST_COMBAT_TICK);
        let result = signature_at(quarrel_sim::YELLOW_RESULT_ONSET_TICK);
        assert!(result.result_orange_left_pixels > 500);
        assert!(result.result_orange_left_pixels > last_combat.result_orange_left_pixels * 20);
        assert!(result.result_orange_left_pixels > result.result_orange_right_pixels * 3);
        let following = signature_at(quarrel_sim::YELLOW_FOLLOWING_RESULT_TICK);
        assert!(following.result_orange_left_pixels > result.result_orange_left_pixels * 2);
        let established = signature_at(quarrel_sim::YELLOW_ROUND_ORANGE_TICK);
        let tail = signature_at(quarrel_sim::YELLOW_REPLAY_TICKS);
        assert!(tail.result_orange_left_pixels < established.result_orange_left_pixels / 2);
    }

    #[test]
    fn keyboard_and_controller_map_to_authoritative_draft_commands() {
        let snapshots = quarrel_sim::run_profile_snapshots(
            ReplayProfile::RematchDraftReplay,
            quarrel_sim::SOURCE_DRAFT_SEED,
            600,
        );
        let flow = snapshots.last().unwrap().flow.as_ref().unwrap();
        let keyboard = keyboard_flow_command(KeyCode::ArrowRight, 0, flow).unwrap();
        let controller = gamepad_flow_command(GamepadButton::DPadRight, 0, flow).unwrap();
        assert_eq!(keyboard, controller);
        assert_eq!(keyboard.phase_revision, flow.phase_revision);
        assert!(matches!(keyboard.action, FlowAction::Hover(_)));
        let post = quarrel_sim::run_profile_match(
            ReplayProfile::RematchDraftReplay,
            quarrel_sim::SOURCE_DRAFT_SEED,
            5_710,
        )
        .0;
        let post_flow = post.flow.as_ref().unwrap();
        assert_eq!(
            keyboard_flow_command(KeyCode::ArrowRight, 0, post_flow),
            gamepad_flow_command(GamepadButton::DPadRight, 0, post_flow)
        );
        assert_eq!(
            keyboard_flow_command(KeyCode::Enter, 0, post_flow),
            gamepad_flow_command(GamepadButton::South, 0, post_flow)
        );
        let mut keys = ButtonInput::default();
        for key in [
            KeyCode::KeyA,
            KeyCode::KeyW,
            KeyCode::Space,
            KeyCode::KeyI,
            KeyCode::KeyJ,
        ] {
            keys.press(key);
        }
        let combat = keyboard_combat_input(&keys, 0);
        assert_eq!(
            (combat.move_axis, combat.aim_x, combat.aim_y),
            (-1, -1_000, 1_000)
        );
        assert!(combat.jump && combat.fire && !combat.aim_at_opponent);
        keys.press(KeyCode::ArrowRight);
        let other_slot = keyboard_combat_input(&keys, 1);
        assert_eq!(other_slot.move_axis, 1);
        assert!(!other_slot.jump && !other_slot.fire);
        assert_eq!(
            keys.get_just_pressed()
                .filter(|key| **key == KeyCode::ArrowRight)
                .filter_map(|key| keyboard_flow_command(*key, 0, flow))
                .count(),
            1
        );
        keys.clear_just_pressed(KeyCode::ArrowRight);
        assert!(
            !keys
                .get_just_pressed()
                .any(|key| *key == KeyCode::ArrowRight)
        );
        assert!(gamepad_combat_input(&Gamepad::default()).aim_at_opponent);
        let mut pad = Gamepad::default();
        pad.analog_mut().set(GamepadAxis::LeftStickX, -1.0);
        pad.analog_mut().set(GamepadAxis::RightStickX, -1.0);
        pad.analog_mut().set(GamepadAxis::RightStickY, 1.0);
        pad.digital_mut().press(GamepadButton::South);
        pad.digital_mut().press(GamepadButton::RightTrigger2);
        assert_eq!(combat, gamepad_combat_input(&pad));
        let apply = |input| {
            let mut authority = AuthoritativeMatch::new_with_profile(
                quarrel_sim::SOURCE_DRAFT_SEED,
                ReplayProfile::RematchDraftReplay,
            );
            let trace = scripted_inputs_for(
                ReplayProfile::RematchDraftReplay,
                quarrel_sim::SOURCE_DRAFT_SEED,
                2_220,
            );
            for (&orange, &blue) in trace[0].iter().zip(&trace[1]) {
                authority.step([orange, blue]);
            }
            authority.step([input, PlayerInput::default()]);
            authority.snapshot()
        };
        let keyboard_state = apply(combat);
        assert_eq!(keyboard_state, apply(gamepad_combat_input(&pad)));
        assert!(
            keyboard_state
                .projectiles
                .iter()
                .any(|projectile| projectile.owner == 0 && projectile.dazzle_pulses == 3)
        );
    }

    #[test]
    fn repeated_draft_capture_waits_for_the_same_complete_scene() {
        let snapshot = quarrel_sim::run_profile_snapshots(
            ReplayProfile::RematchDraftReplay,
            quarrel_sim::SOURCE_DRAFT_SEED,
            600,
        )
        .pop()
        .unwrap();
        let flow_before = quarrel_sim::flow_digest(snapshot.flow.as_ref().unwrap());
        let loadout_before = quarrel_sim::loadout_digest(snapshot.flow.as_ref().unwrap());
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
            quarrel_sim::flow_digest(snapshot.flow.as_ref().unwrap()),
            flow_before
        );
        assert_eq!(
            quarrel_sim::loadout_digest(snapshot.flow.as_ref().unwrap()),
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

        let mut overpower = draft_scene_at(5_588);
        assert!(card_state(&mut overpower, ItemId::Overpower).highlighted);
        let mut quick_shot = draft_scene_at(5_710);
        assert!(card_state(&mut quick_shot, ItemId::QuickShot).highlighted);
        let mut before = draft_scene_at(5_801);
        let before_quick_shot = card_state(&mut before, ItemId::QuickShot);
        assert!(before_quick_shot.highlighted);
        assert!(!before_quick_shot.selected_offscreen);
        let mut before_badges = before
            .query::<&HudBadge>()
            .iter(&before)
            .map(|badge| (badge.player, badge.label.clone()))
            .collect::<Vec<_>>();
        before_badges.sort();
        assert_eq!(
            before_badges,
            vec![(0, "Da".to_owned()), (1, "Ex".to_owned())]
        );
        let mut confirmed = draft_scene_at(5_802);
        let confirmed_quick_shot = card_state(&mut confirmed, ItemId::QuickShot);
        assert!(confirmed_quick_shot.highlighted);
        assert!(!confirmed_quick_shot.selected_offscreen);
        let mut confirmed_badges = confirmed
            .query::<&HudBadge>()
            .iter(&confirmed)
            .map(|badge| (badge.player, badge.label.clone()))
            .collect::<Vec<_>>();
        confirmed_badges.sort();
        assert_eq!(
            confirmed_badges,
            vec![
                (0, "Da".to_owned()),
                (0, "Qu".to_owned()),
                (1, "Ex".to_owned())
            ]
        );
        let mut before_bridge = draft_scene_at(5_817);
        let departing_quick_shot = card_state(&mut before_bridge, ItemId::QuickShot);
        assert!(departing_quick_shot.highlighted);
        assert!(!departing_quick_shot.selected_offscreen);
        let mut bridge = draft_scene_at(5_818);
        let bridge_cards = bridge
            .query::<&CardPresentation>()
            .iter(&bridge)
            .map(|card| card.item)
            .collect::<Vec<_>>();
        assert_eq!(bridge_cards.len(), 4);
        assert!(!bridge_cards.contains(&ItemId::QuickShot));
        for item in [
            ItemId::ColdBullets,
            ItemId::CarefulPlanning,
            ItemId::Overpower,
            ItemId::BigBullet,
        ] {
            assert!(bridge_cards.contains(&item));
        }
        assert_eq!(
            bridge
                .query::<&HudScorePip>()
                .iter(&bridge)
                .filter(|pip| pip.filled)
                .map(|pip| (pip.player, pip.index))
                .collect::<Vec<_>>(),
            vec![(1, 0)]
        );
        let connected = quarrel_sim::run_profile_snapshots(
            ReplayProfile::RematchDraftReplay,
            quarrel_sim::SOURCE_DRAFT_SEED,
            quarrel_sim::REMATCH_DRAFT_TICKS,
        );
        for tick in [
            quarrel_sim::CONNECTED_BLUE_RESULT_ONSET_TICK,
            quarrel_sim::CONNECTED_HALF_BLUE_TICK,
            quarrel_sim::CONNECTED_HALF_ORANGE_TICK,
        ] {
            let (_, _, chromatic, lens) = camera_state(&connected[(tick - 1) as usize]);
            assert_eq!(chromatic.intensity, 0.0);
            assert_eq!(lens.intensity, 0.0);
            assert_eq!(lens.scale, 1.0);
        }
    }
}
