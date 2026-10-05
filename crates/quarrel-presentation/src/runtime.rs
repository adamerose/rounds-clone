use super::*;
use super::{
    arena::spawn_snapshot_scene,
    post_process::RadialEchoSettings,
    scene::{camera_state, radial_echo_settings},
};

pub(super) fn setup_offscreen_scene(
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

pub(super) fn setup_visible_scene(mut commands: Commands, snapshot: Res<SceneSnapshot>) {
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

pub(super) fn advance_visible_scene(
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
pub(super) fn advance_interactive_scene(
    mut commands: Commands,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<ColorMaterial>>,
    visuals: Query<Entity, With<SceneVisual>>,
    keys: Res<ButtonInput<KeyCode>>,
    gamepads: Query<&Gamepad>,
    time: Res<Time>,
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
    if !lifetime.shown {
        return;
    }
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
    for (player, command) in direct.into_iter().enumerate() {
        if command.is_some() {
            authority.pending_flow[player] = command;
        }
    }
    let mut live = std::array::from_fn(|player| keyboard_combat_input(&keys, player as u8));
    for (player, gamepad) in gamepads.iter().take(2).enumerate() {
        live[player] = gamepad_combat_input(gamepad);
    }
    let steps = if authority.automated {
        10
    } else {
        authority.tick_budget += time.delta_secs_f64() * f64::from(quarrel_sim::TICKS_PER_SECOND);
        let steps = authority.tick_budget.floor() as usize;
        authority.tick_budget -= steps as f64;
        steps
    };
    for substep in 0..steps {
        if authority.tick >= authority.limit {
            break;
        }
        let mut inputs = if authority.automated {
            [
                authority.scripts[0][authority.tick],
                authority.scripts[1][authority.tick],
            ]
        } else {
            live
        };
        if substep == 0 {
            for (player, input) in inputs.iter_mut().enumerate() {
                if let Some(command) = authority.pending_flow[player].take() {
                    input.flow = Some(command);
                }
            }
        }
        let observation = authority.simulation.snapshot();
        inputs = std::array::from_fn(|player| {
            inputs[player].with_progressive_observation(player as u8, Some(&observation))
        });
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

pub(super) fn poll_live_snapshot(mut live: ResMut<LivePresentation>) {
    if let Some((snapshot, hash)) = live.handle.latest()
        && live.displayed_hash.as_deref() != Some(hash.as_str())
    {
        live.displayed_hash = Some(hash);
        live.displayed_snapshot = Some(snapshot);
    }
}

pub(super) fn advance_live_scene(
    mut commands: Commands,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<ColorMaterial>>,
    visuals: Query<Entity, With<SceneVisual>>,
    mut cameras: Query<
        (
            &mut Transform,
            &mut Bloom,
            &mut ChromaticAberration,
            &mut LensDistortion,
            &mut RadialEchoSettings,
        ),
        With<Camera2d>,
    >,
    live: Res<LivePresentation>,
    mut rendered_hash: Local<Option<String>>,
) {
    let Some(snapshot) = live.displayed_snapshot.as_ref() else {
        return;
    };
    let Some(hash) = live.displayed_hash.as_ref() else {
        return;
    };
    if rendered_hash.as_ref() == Some(hash) {
        return;
    }
    let (transform, bloom, chromatic, lens) = camera_state(snapshot);
    if let Ok((
        mut camera_transform,
        mut camera_bloom,
        mut camera_chromatic,
        mut camera_lens,
        mut camera_echo,
    )) = cameras.single_mut()
    {
        *camera_transform = transform;
        *camera_bloom = bloom;
        *camera_chromatic = chromatic;
        *camera_lens = lens;
        *camera_echo = radial_echo_settings(snapshot);
    } else {
        commands.spawn((
            Camera2d,
            Hdr,
            transform,
            bloom,
            chromatic,
            lens,
            radial_echo_settings(snapshot),
        ));
    }
    for entity in &visuals {
        commands.entity(entity).despawn();
    }
    spawn_snapshot_scene(&mut commands, &mut meshes, &mut materials, snapshot);
    println!(
        "{{\"event\":\"presented\",\"tick\":{},\"hash\":\"{}\"}}",
        snapshot.tick, hash
    );
    *rendered_hash = Some(hash.clone());
}

pub(super) fn verify_live_monitor_show(
    mut primary: Single<(&mut Window, &OnMonitor), With<PrimaryWindow>>,
    monitors: Query<&Monitor>,
    live: Res<LivePresentation>,
    mut lifetime: ResMut<VisibleLifetime>,
    mut exit: MessageWriter<AppExit>,
) {
    if lifetime.shown || live.displayed_snapshot.is_none() {
        return;
    }
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
    let WindowPosition::At(position) = primary.0.position else {
        eprintln!("native window position was unavailable; window remained hidden");
        exit.write(AppExit::error());
        return;
    };
    let center = position + primary.0.physical_size().as_ivec2() / 2;
    let display_min = monitor.physical_position;
    let display_max = display_min + monitor.physical_size().as_ivec2();
    if !center.cmpge(display_min).all() || !center.cmplt(display_max).all() {
        eprintln!("native window center {center:?} was outside monitor 4; window remained hidden");
        exit.write(AppExit::error());
        return;
    }
    primary.0.visible = true;
    lifetime.shown = true;
    println!(
        "{{\"event\":\"windowPlacementVerified\",\"width\":{},\"height\":{},\"x\":{},\"y\":{},\"centerX\":{},\"centerY\":{}}}",
        monitor.physical_width,
        monitor.physical_height,
        monitor.physical_position.x,
        monitor.physical_position.y,
        center.x,
        center.y
    );
}

pub(super) fn submit_live_input(
    keys: Res<ButtonInput<KeyCode>>,
    gamepads: Query<&Gamepad>,
    lifetime: Res<VisibleLifetime>,
    live: Res<LivePresentation>,
    mut exit: MessageWriter<AppExit>,
) {
    if let Some(result) = live.handle.result() {
        if result != "completed" || lifetime.shown {
            exit.write(if result == "completed" {
                AppExit::Success
            } else {
                AppExit::error()
            });
        }
        return;
    }
    if !lifetime.shown {
        return;
    }
    let gamepad = gamepads.iter().next();
    let input = gamepad.map_or_else(
        || keyboard_combat_input(&keys, live.player),
        gamepad_combat_input,
    );
    live.handle.set_held(input);
    let Some(flow) = live
        .displayed_snapshot
        .as_ref()
        .and_then(|snapshot| snapshot.flow.as_ref())
    else {
        return;
    };
    for key in keys.get_just_pressed().copied() {
        if let Some(command) = keyboard_flow_command(key, live.player, flow)
            && let Err(error) = live.handle.push_flow(command)
        {
            eprintln!("submit keyboard flow command: {error}");
            exit.write(AppExit::error());
            return;
        }
    }
    if let Some(gamepad) = gamepad {
        for button in gamepad.get_just_pressed().copied() {
            if let Some(command) = gamepad_flow_command(button, live.player, flow)
                && let Err(error) = live.handle.push_flow(command)
            {
                eprintln!("submit controller flow command: {error}");
                exit.write(AppExit::error());
                return;
            }
        }
    }
}

pub(super) fn close_live_window(
    mut closed: MessageReader<WindowClosed>,
    live: Res<LivePresentation>,
    mut exit: MessageWriter<AppExit>,
) {
    if closed.read().next().is_some() {
        live.handle.close();
        exit.write(AppExit::Success);
    }
}

pub(super) fn verify_monitor_show_and_exit(
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
        let WindowPosition::At(position) = primary.0.position else {
            eprintln!("native window position was unavailable; window remained hidden");
            exit.write(AppExit::error());
            return;
        };
        let center = position + primary.0.physical_size().as_ivec2() / 2;
        let display_min = monitor.physical_position;
        let display_max = display_min + monitor.physical_size().as_ivec2();
        if !center.cmpge(display_min).all() || !center.cmplt(display_max).all() {
            eprintln!(
                "native window center {center:?} was outside monitor 4; window remained hidden"
            );
            exit.write(AppExit::error());
            return;
        }
        primary.0.visible = true;
        lifetime.shown = true;
        println!(
            "{{\"event\":\"windowPlacementVerified\",\"width\":{},\"height\":{},\"x\":{},\"y\":{},\"centerX\":{},\"centerY\":{}}}",
            monitor.physical_width,
            monitor.physical_height,
            monitor.physical_position.x,
            monitor.physical_position.y,
            center.x,
            center.y
        );
    }
    if lifetime.frames == 0 {
        exit.write(AppExit::Success);
    }
}

pub(super) fn is_project_display(monitor: &Monitor) -> bool {
    monitor.physical_position == PROJECT_DISPLAY_POSITION
        && monitor.physical_size() == PROJECT_DISPLAY_SIZE
}
