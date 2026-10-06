use super::*;
use super::{
    arena::spawn_snapshot_scene,
    scene::{camera_projection, camera_state, camera_viewport},
};
use bevy::camera::{Camera, Projection};

type CameraSettings<'a> = (
    &'a mut Transform,
    &'a mut Bloom,
    &'a mut ChromaticAberration,
    &'a mut LensDistortion,
    &'a mut Projection,
);

pub(super) fn setup_offscreen_scene(
    mut commands: Commands,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<ColorMaterial>>,
    snapshot: Res<SceneSnapshot>,
    target: Res<CaptureTarget>,
    menu: Option<Res<super::menu::Menu>>,
    view: Res<super::capture::OffscreenView>,
) {
    let (transform, bloom, chromatic, lens) = camera_state(&snapshot.0);
    commands.spawn((
        Camera2d,
        Camera {
            viewport: camera_viewport(&snapshot.0, UVec2::new(FRAME_WIDTH, FRAME_HEIGHT)),
            ..default()
        },
        camera_projection(&snapshot.0),
        Hdr,
        RenderTarget::Image(target.0.clone().into()),
        transform,
        bloom,
        chromatic,
        lens,
    ));
    if matches!(*view, super::capture::OffscreenView::Waiting) {
        spawn_waiting_scene(&mut commands);
        return;
    }
    if let Some(menu) = menu {
        super::menu::spawn_menu(&mut commands, &menu);
        return;
    }
    spawn_snapshot_scene(&mut commands, &mut meshes, &mut materials, &snapshot.0);
}

pub(super) fn setup_visible_scene(mut commands: Commands, snapshot: Res<SceneSnapshot>) {
    let (transform, bloom, chromatic, lens) = camera_state(&snapshot.0);
    commands.spawn((
        Camera2d,
        Camera::default(),
        camera_projection(&snapshot.0),
        Hdr,
        transform,
        bloom,
        chromatic,
        lens,
    ));
}

pub(super) fn advance_visible_scene(
    mut commands: Commands,
    mut meshes: ResMut<Assets<Mesh>>,
    mut materials: ResMut<Assets<ColorMaterial>>,
    visuals: Query<Entity, With<SceneVisual>>,
    mut camera: Single<CameraSettings<'_>, With<Camera2d>>,
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
    *camera.4 = camera_projection(snapshot);
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
    mouse_buttons: Res<ButtonInput<MouseButton>>,
    gamepads: Query<(Entity, &Gamepad)>,
    windows: Query<&Window, With<PrimaryWindow>>,
    cameras: Query<(&Camera, &GlobalTransform), With<Camera2d>>,
    time: Res<Time>,
    mut authority: ResMut<InteractiveAuthority>,
    mut scene: ResMut<SceneSnapshot>,
    mut lifetime: ResMut<VisibleLifetime>,
    mut camera: Single<CameraSettings<'_>, With<Camera2d>>,
    mut scripted_phase: Local<Option<FlowPhase>>,
) {
    if !lifetime.shown {
        return;
    }
    if authority.tick >= authority.limit {
        lifetime.frames = lifetime.frames.min(3);
        return;
    }
    let flow = scene.0.flow.as_ref();
    let fighters = flow.map_or(0, |flow| flow.scores.len());
    let mut controllers = gamepads.iter().collect::<Vec<_>>();
    controllers.sort_by_key(|(entity, _)| entity.to_bits());
    let keyboard_player = (controllers.len() < 2).then_some(0);
    let controller_start = usize::from(controllers.len() == 1);
    let mut direct = vec![None; fighters];
    if let Some(flow) = flow {
        for key in keys.get_just_pressed().copied() {
            if controllers.is_empty() {
                for (player, input) in direct.iter_mut().enumerate().take(2) {
                    if let Some(command) = keyboard_flow_command(key, player as u8, flow) {
                        *input = Some(command);
                    }
                }
            } else if let Some(player) = keyboard_player
                && let Some(input) = direct.get_mut(player)
                && let Some(command) = primary_keyboard_flow_command(key, player as u8, flow)
            {
                *input = Some(command);
            }
        }
        for (offset, (_, gamepad)) in controllers.iter().enumerate() {
            let player = controller_start + offset;
            if player >= fighters {
                break;
            }
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
    let mut live = if controllers.is_empty() {
        (0..fighters)
            .map(|player| keyboard_combat_input(&keys, player as u8))
            .collect()
    } else {
        vec![PlayerInput::default(); fighters]
    };
    if let Some(player) = keyboard_player
        && let Some(input) = live.get_mut(player)
    {
        *input = keyboard_mouse_combat_input(
            &keys,
            &mouse_buttons,
            mouse_aim(&scene.0, player as u8, &windows, &cameras),
        );
    }
    for (offset, (_, gamepad)) in controllers.iter().enumerate() {
        let player = controller_start + offset;
        if player >= fighters {
            break;
        }
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
        let observation = authority.simulation.snapshot();
        if authority.automated {
            let phase = observation.flow.as_ref().map(|flow| flow.phase);
            if *scripted_phase != phase {
                println!(
                    "{{\"event\":\"scriptedDeviceFlow\",\"tick\":{},\"phase\":\"{}\"}}",
                    observation.tick,
                    phase.map_or("none".to_owned(), |phase| format!("{phase:?}"))
                );
                *scripted_phase = phase;
            }
        }
        let mut inputs = if authority.automated {
            scripted_device_inputs(fighters, &observation)
        } else {
            live.clone()
        };
        if substep == 0 {
            for (player, input) in inputs.iter_mut().enumerate() {
                if let Some(command) = authority.pending_flow[player].take() {
                    input.flow = Some(command);
                }
            }
        }
        inputs = inputs
            .into_iter()
            .enumerate()
            .map(|(player, input)| {
                input.with_progressive_observation(player as u8, Some(&observation))
            })
            .collect();
        authority.simulation.step(&inputs);
        authority.tick += 1;
    }
    scene.0 = authority.simulation.snapshot();
    let (transform, bloom, chromatic, lens) = camera_state(&scene.0);
    *camera.0 = transform;
    *camera.1 = bloom;
    *camera.2 = chromatic;
    *camera.3 = lens;
    *camera.4 = camera_projection(&scene.0);
    for entity in &visuals {
        commands.entity(entity).despawn();
    }
    spawn_snapshot_scene(&mut commands, &mut meshes, &mut materials, &scene.0);
    if authority.tick >= authority.limit {
        lifetime.frames = 3;
    }
}

fn scripted_device_inputs(fighters: usize, observation: &MatchSnapshot) -> Vec<PlayerInput> {
    (0..fighters)
        .map(|player| {
            let mut source = quarrel_sim::automated_input(player as u8, observation);
            // Automated play advances ten ticks per frame. Keep choice screens
            // visible for thirty frames before the scripted device confirms.
            let waiting = observation.flow.as_ref().is_some_and(|flow| {
                matches!(flow.phase, FlowPhase::Draft | FlowPhase::MatchEnd)
                    && flow.phase_tick < 300
            });
            if waiting {
                source.flow = None;
            } else if observation
                .flow
                .as_ref()
                .is_some_and(|flow| flow.phase == FlowPhase::MatchEnd)
            {
                source.flow = Some(FlowCommand {
                    phase_revision: observation.flow.as_ref().unwrap().phase_revision,
                    action: FlowAction::VoteYes,
                });
            }
            match player {
                0 => scripted_keyboard_input(source, player as u8, observation.flow.as_ref()),
                1 => scripted_gamepad_input(source, player as u8, observation.flow.as_ref()),
                _ => source,
            }
        })
        .collect()
}

fn scripted_keyboard_input(
    source: PlayerInput,
    player: u8,
    flow: Option<&FlowSnapshot>,
) -> PlayerInput {
    let mut keys = ButtonInput::default();
    let mut mouse_buttons = ButtonInput::default();
    if source.move_axis < 0 {
        keys.press(KeyCode::KeyA);
    }
    if source.move_axis > 0 {
        keys.press(KeyCode::KeyD);
    }
    if source.jump {
        keys.press(KeyCode::KeyW);
    }
    if source.block {
        keys.press(KeyCode::KeyS);
    }
    if source.fire {
        mouse_buttons.press(MouseButton::Left);
    }
    if source.aim_x < 0 {
        keys.press(KeyCode::KeyJ);
    }
    if source.aim_x > 0 {
        keys.press(KeyCode::KeyL);
    }
    if source.aim_y < 0 {
        keys.press(KeyCode::KeyK);
    }
    if source.aim_y > 0 {
        keys.press(KeyCode::KeyI);
    }
    let mut input = keyboard_mouse_combat_input(&keys, &mouse_buttons, None);
    input.flow = source.flow.and_then(|wanted| {
        flow.and_then(|flow| {
            [
                KeyCode::KeyY,
                KeyCode::KeyN,
                KeyCode::KeyA,
                KeyCode::KeyD,
                KeyCode::Space,
            ]
            .into_iter()
            .find_map(|key| {
                primary_keyboard_flow_command(key, player, flow)
                    .filter(|command| command.action == wanted.action)
            })
        })
    });
    input
}

fn scripted_gamepad_input(
    source: PlayerInput,
    player: u8,
    flow: Option<&FlowSnapshot>,
) -> PlayerInput {
    let mut gamepad = Gamepad::default();
    gamepad
        .analog_mut()
        .set(GamepadAxis::LeftStickX, f32::from(source.move_axis));
    gamepad
        .analog_mut()
        .set(GamepadAxis::RightStickX, f32::from(source.aim_x) / 1_000.0);
    gamepad
        .analog_mut()
        .set(GamepadAxis::RightStickY, f32::from(source.aim_y) / 1_000.0);
    if source.jump {
        gamepad.digital_mut().press(GamepadButton::South);
    }
    if source.fire {
        gamepad.digital_mut().press(GamepadButton::RightTrigger2);
    }
    if source.block {
        gamepad.digital_mut().press(GamepadButton::West);
    }
    let mut input = gamepad_combat_input(&gamepad);
    input.flow = source.flow.and_then(|wanted| {
        flow.and_then(|flow| {
            [
                GamepadButton::South,
                GamepadButton::East,
                GamepadButton::DPadLeft,
                GamepadButton::DPadRight,
            ]
            .into_iter()
            .find_map(|button| {
                gamepad_flow_command(button, player, flow)
                    .filter(|command| command.action == wanted.action)
            })
        })
    });
    input
}

fn mouse_aim(
    snapshot: &MatchSnapshot,
    player: u8,
    windows: &Query<&Window, With<PrimaryWindow>>,
    cameras: &Query<(&Camera, &GlobalTransform), With<Camera2d>>,
) -> Option<Vec2> {
    let cursor = windows.single().ok()?.cursor_position()?;
    let (camera, transform) = cameras.single().ok()?;
    let target = camera.viewport_to_world_2d(transform, cursor).ok()?;
    let actor = snapshot
        .players
        .iter()
        .find(|fighter| fighter.id == player)?;
    Some(target - Vec2::new(actor.x_milli as f32, actor.y_milli as f32) / 1_000.0)
}

pub(super) fn setup_live_waiting_scene(mut commands: Commands) {
    commands.spawn((
        Camera2d,
        super::scene::ui_projection(),
        Hdr,
        Bloom::default(),
        ChromaticAberration::default(),
        LensDistortion::default(),
    ));
    spawn_waiting_scene(&mut commands);
}

fn spawn_waiting_scene(commands: &mut Commands) {
    commands.spawn((
        SceneVisual,
        CaptureElement::Background,
        Sprite::from_color(Color::srgb_u8(7, 16, 28), Vec2::new(1280.0, 720.0)),
        Transform::from_xyz(0.0, 0.0, -10.0),
    ));
    commands.spawn((
        SceneVisual,
        Text2d::new("Waiting for the other player...\nEscape or close the window to cancel"),
        TextFont {
            font_size: FontSize::Px(28.0),
            ..default()
        },
        TextColor(Color::WHITE),
        TextLayout::justify(Justify::Center),
        Transform::default(),
    ));
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
    mut cameras: Query<CameraSettings<'_>, With<Camera2d>>,
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
        mut projection,
    )) = cameras.single_mut()
    {
        *camera_transform = transform;
        *camera_bloom = bloom;
        *camera_chromatic = chromatic;
        *camera_lens = lens;
        *projection = camera_projection(snapshot);
    } else {
        commands.spawn((
            Camera2d,
            Camera::default(),
            camera_projection(snapshot),
            Hdr,
            transform,
            bloom,
            chromatic,
            lens,
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
    mut cameras: Query<&mut Camera, With<Camera2d>>,
    live: Res<LivePresentation>,
    mut lifetime: ResMut<VisibleLifetime>,
    mut exit: MessageWriter<AppExit>,
) {
    if let Some(snapshot) = live.displayed_snapshot.as_ref()
        && let Ok(mut camera) = cameras.single_mut()
    {
        camera.viewport = camera_viewport(snapshot, primary.0.physical_size());
    }
    if lifetime.shown {
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

#[expect(
    clippy::too_many_arguments,
    reason = "live input needs the concrete keyboard, mouse, controller, window, and camera resources"
)]
pub(super) fn submit_live_input(
    keys: Res<ButtonInput<KeyCode>>,
    mouse_buttons: Res<ButtonInput<MouseButton>>,
    gamepads: Query<(Entity, &Gamepad)>,
    windows: Query<&Window, With<PrimaryWindow>>,
    cameras: Query<(&Camera, &GlobalTransform), With<Camera2d>>,
    lifetime: Res<VisibleLifetime>,
    live: Res<LivePresentation>,
    mut exit: MessageWriter<AppExit>,
) {
    if keys.just_pressed(KeyCode::Escape) {
        live.handle.close();
        exit.write(AppExit::Success);
        return;
    }
    if let Some(result) = live.handle.result() {
        if (result != "completed" && result != "local_close") || lifetime.shown {
            exit.write(if result == "completed" || result == "local_close" {
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
    let mut controllers = gamepads.iter().collect::<Vec<_>>();
    controllers.sort_by_key(|(entity, _)| entity.to_bits());
    let gamepad = controllers.first().map(|(_, gamepad)| *gamepad);
    let input = gamepad.map_or_else(
        || {
            keyboard_mouse_combat_input(
                &keys,
                &mouse_buttons,
                live.displayed_snapshot
                    .as_ref()
                    .and_then(|snapshot| mouse_aim(snapshot, live.player, &windows, &cameras)),
            )
        },
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
        if let Some(command) = primary_keyboard_flow_command(key, live.player, flow)
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

pub(super) fn close_interactive_window(
    mut closed: MessageReader<WindowClosed>,
    mut exit: MessageWriter<AppExit>,
) {
    if closed.read().next().is_some() {
        exit.write(AppExit::Success);
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
    mut cameras: Query<&mut Camera, With<Camera2d>>,
    snapshot: Res<SceneSnapshot>,
    mut lifetime: ResMut<VisibleLifetime>,
    mut exit: MessageWriter<AppExit>,
) {
    if let Ok(mut camera) = cameras.single_mut() {
        camera.viewport = camera_viewport(&snapshot.0, primary.0.physical_size());
    }
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

#[cfg(test)]
mod scripted_tests {
    use super::*;

    #[test]
    fn device_script_displays_choices_before_confirming_and_reaches_run_back() {
        let mut game = AuthoritativeMatch::with_config(quarrel_sim::MatchConfig {
            target_score: 2,
            ..default()
        })
        .unwrap();
        let first = game.snapshot();
        assert!(
            scripted_device_inputs(2, &first)
                .iter()
                .all(|input| input.flow.is_none())
        );
        let mut saw_end = false;
        for _ in 0..2400 {
            let snapshot = game.snapshot();
            saw_end |= snapshot.flow.as_ref().unwrap().phase == FlowPhase::MatchEnd;
            let inputs = scripted_device_inputs(2, &snapshot)
                .into_iter()
                .enumerate()
                .map(|(player, input)| {
                    input.with_progressive_observation(player as u8, Some(&snapshot))
                })
                .collect::<Vec<_>>();
            game.step(&inputs);
            if game.snapshot().flow.as_ref().unwrap().run_backs > 0 {
                assert!(saw_end);
                return;
            }
        }
        panic!("device script did not reach a run-back");
    }
}
