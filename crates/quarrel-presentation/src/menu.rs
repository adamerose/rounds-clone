use super::*;
use super::{capture::create_monitor_four_window, runtime::verify_monitor_show_and_exit};
use bevy::input::keyboard::{Key, KeyboardInput};
use std::process::{Child, Command};

#[derive(Resource)]
pub(super) struct Menu {
    selected: usize,
    address: String,
    status: String,
    child: Option<Child>,
}
impl Default for Menu {
    fn default() -> Self {
        Self {
            selected: 0,
            address: "127.0.0.1:7777".into(),
            status: String::new(),
            child: None,
        }
    }
}
impl Drop for Menu {
    fn drop(&mut self) {
        if let Some(child) = &mut self.child {
            let _ = child.kill();
            let _ = child.wait();
        }
    }
}

pub fn run_menu() -> Result<(), String> {
    let snapshot = AuthoritativeMatch::new(38).snapshot();
    App::new()
        .add_plugins(DefaultPlugins.set(WindowPlugin {
            primary_window: None,
            exit_condition: ExitCondition::DontExit,
            ..default()
        }))
        .insert_resource(SceneSnapshot(snapshot))
        .init_resource::<Menu>()
        .init_resource::<VisibleWindowRequested>()
        .init_resource::<MonitorDiscovery>()
        .insert_resource(VisibleLifetime {
            frames: u32::MAX,
            shown: false,
        })
        .add_systems(Startup, setup_menu_camera)
        .add_systems(Update, create_monitor_four_window)
        .add_systems(Update, close_menu)
        .add_systems(Update, (verify_monitor_show_and_exit, update_menu).chain())
        .run()
        .is_success()
        .then_some(())
        .ok_or_else(|| "menu could not open on monitor 4".into())
}

fn close_menu(mut closed: MessageReader<WindowClosed>, mut exit: MessageWriter<AppExit>) {
    if closed.read().next().is_some() {
        exit.write(AppExit::Success);
    }
}

pub fn render_menu_png(output: &Path) -> Result<Vec<u8>, String> {
    super::capture::render_scene_png(&AuthoritativeMatch::new(38).snapshot(), output, true)
        .map(|(bytes, _)| bytes)
}

fn setup_menu_camera(mut commands: Commands) {
    commands.spawn((Camera2d, super::scene::ui_projection()));
}

pub(super) fn spawn_menu(commands: &mut Commands, menu: &Menu) {
    commands.spawn((
        SceneVisual,
        CaptureElement::Background,
        Sprite::from_color(Color::srgb_u8(7, 16, 28), Vec2::new(1280.0, 720.0)),
        Transform::from_xyz(0.0, 0.0, -10.0),
    ));
    text(commands, "QUARREL", 250.0, 52.0);
    for (index, label) in ["Local match", "Host on port 7777", "Join", "Address"]
        .iter()
        .enumerate()
    {
        let y = 130.0 - index as f32 * 70.0;
        commands.spawn((
            SceneVisual,
            Sprite::from_color(
                if menu.selected == index {
                    Color::srgb_u8(35, 95, 130)
                } else {
                    Color::srgb_u8(22, 35, 49)
                },
                Vec2::new(760.0, 58.0),
            ),
            Transform::from_xyz(0.0, y, 0.0),
        ));
        text(
            commands,
            &if index == 3 {
                format!("Address: {}", menu.address)
            } else {
                label.to_string()
            },
            y,
            26.0,
        );
    }
    text(
        commands,
        "Click or use arrows / D-pad, Enter / A to start.\nSelect Address; Ctrl+A clears it for the host's IP and port.\nLocal: keyboard + mouse and one controller, or two controllers.",
        -215.0,
        20.0,
    );
    text(commands, &menu.status, -305.0, 18.0);
}

fn text(commands: &mut Commands, value: &str, y: f32, size: f32) {
    commands.spawn((
        SceneVisual,
        Text2d::new(value),
        TextFont {
            font_size: FontSize::Px(size),
            ..default()
        },
        TextColor(Color::WHITE),
        TextLayout::justify(Justify::Center),
        Transform::from_xyz(0.0, y, 2.0),
    ));
}

pub fn menu_match_args(selected: usize, address: &str) -> Result<Vec<String>, String> {
    let args = match selected {
        0 => vec!["visible-flow", "--ticks", "4294967295"],
        1 => vec![
            "host", "--bind", "0.0.0.0", "--port", "7777", "--ticks", "36060",
        ],
        2 => {
            address
                .parse::<std::net::SocketAddr>()
                .map_err(|_| "Enter an IP address and port, for example 192.168.1.2:7777")?;
            vec![
                "join",
                "--address",
                address,
                "--client",
                "1",
                "--ticks",
                "36060",
            ]
        }
        _ => return Err("Select Local, Host or Join to start".into()),
    };
    Ok(args.into_iter().map(str::to_owned).collect())
}

#[expect(
    clippy::too_many_arguments,
    reason = "menu reads concrete input devices and owns its child game window"
)]
fn update_menu(
    mut commands: Commands,
    mut menu: ResMut<Menu>,
    keys: Res<ButtonInput<KeyCode>>,
    mouse: Res<ButtonInput<MouseButton>>,
    gamepads: Query<&Gamepad>,
    mut keyboard: MessageReader<KeyboardInput>,
    mut window: Single<&mut Window, With<PrimaryWindow>>,
    visuals: Query<Entity, With<SceneVisual>>,
    mut exit: MessageWriter<AppExit>,
) {
    if let Some(child) = &mut menu.child {
        match child.try_wait() {
            Ok(Some(status)) => {
                menu.child = None;
                window.visible = true;
                if status.success() {
                    menu.status.clear();
                } else {
                    menu.status = "Match could not start or connection ended. Check the address and try again.".into();
                }
            }
            Ok(None) => {
                keyboard.clear();
                return;
            }
            Err(error) => {
                menu.status = error.to_string();
                return;
            }
        }
    }
    if keys.just_pressed(KeyCode::Escape) {
        exit.write(AppExit::Success);
    }
    let up = keys.just_pressed(KeyCode::ArrowUp)
        || gamepads
            .iter()
            .any(|g| g.just_pressed(GamepadButton::DPadUp));
    let down = keys.just_pressed(KeyCode::ArrowDown)
        || gamepads
            .iter()
            .any(|g| g.just_pressed(GamepadButton::DPadDown));
    if up {
        menu.selected = (menu.selected + 3) % 4;
    }
    if down {
        menu.selected = (menu.selected + 1) % 4;
    }
    let mut confirm = keys.just_pressed(KeyCode::Enter)
        || gamepads
            .iter()
            .any(|g| g.just_pressed(GamepadButton::South));
    if mouse.just_pressed(MouseButton::Left)
        && let Some(cursor) = window.cursor_position()
    {
        let point = Vec2::new(
            cursor.x / window.width() * 1280.0 - 640.0,
            360.0 - cursor.y / window.height() * 720.0,
        );
        for index in 0..4 {
            if point.x.abs() < 380.0 && (point.y - (130.0 - index as f32 * 70.0)).abs() < 29.0 {
                menu.selected = index;
                confirm = index < 3;
            }
        }
    }
    if menu.selected == 3
        && keys.just_pressed(KeyCode::KeyA)
        && (keys.pressed(KeyCode::ControlLeft) || keys.pressed(KeyCode::ControlRight))
    {
        menu.address.clear();
    }
    for event in keyboard.read() {
        if menu.selected != 3 || !event.state.is_pressed() {
            continue;
        }
        match &event.logical_key {
            Key::Backspace => {
                menu.address.pop();
            }
            Key::Character(value) => {
                if keys.pressed(KeyCode::ControlLeft) || keys.pressed(KeyCode::ControlRight) {
                    continue;
                }
                for character in value.chars().filter(|c| {
                    c.is_ascii_digit() || matches!(c, '.' | ':' | '[' | ']' | 'a'..='f' | 'A'..='F')
                }) {
                    if menu.address.len() < 64 {
                        menu.address.push(character);
                    }
                }
            }
            _ => {}
        }
    }
    if confirm && menu.selected < 3 {
        let result = menu_match_args(menu.selected, &menu.address).and_then(|args| {
            let executable = std::env::current_exe().map_err(|e| e.to_string())?;
            Command::new(executable)
                .args(args)
                .spawn()
                .map_err(|e| e.to_string())
        });
        match result {
            Ok(child) => {
                menu.child = Some(child);
                menu.status = if menu.selected == 1 {
                    "Hosting on port 7777. Your partner should select Join now.".into()
                } else {
                    "Starting match...".into()
                };
            }
            Err(error) => menu.status = error,
        }
    }
    for entity in &visuals {
        commands.entity(entity).despawn();
    }
    spawn_menu(&mut commands, &menu);
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn menu_launches_existing_local_and_network_routes() {
        assert_eq!(menu_match_args(0, "").unwrap()[0], "visible-flow");
        assert!(
            menu_match_args(1, "")
                .unwrap()
                .windows(2)
                .any(|pair| pair == ["--bind", "0.0.0.0"])
        );
        let join = menu_match_args(2, "127.0.0.1:7777").unwrap();
        assert!(join.windows(2).any(|pair| pair == ["--client", "1"]));
        assert!(menu_match_args(2, "bad address").is_err());
    }
}
