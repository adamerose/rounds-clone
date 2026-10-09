use super::*;
use super::{capture::create_monitor_four_window, runtime::verify_monitor_show_and_exit};
use bevy::input::keyboard::{Key, KeyboardInput};
use std::process::{Child, Command};

const ITEMS: [&str; 5] = [
    "Local match",
    "Host on port 7777",
    "Join",
    "Address",
    "Online: invite a Steam friend",
];
const ADDRESS: usize = 3;
const STEAM: usize = 4;

fn item_y(index: usize) -> f32 {
    170.0 - index as f32 * 62.0
}

/// Steam services for the menu. The client supplies them, so presentation does not link Steam.
pub trait OnlineService: Send + Sync + 'static {
    /// Starts Steam for online play, or says why it is unavailable.
    fn ready(&mut self) -> Result<(), String>;
    /// A Steam lobby the player chose to join since the last call.
    fn join_request(&mut self) -> Option<u64>;
    /// Hands Steam to the match process the menu starts.
    fn release(&mut self);
}

#[derive(Resource)]
pub(super) struct Menu {
    selected: usize,
    address: String,
    status: String,
    child: Option<Child>,
    online: Box<dyn OnlineService>,
}
impl Menu {
    fn new(online: Box<dyn OnlineService>) -> Self {
        Self {
            selected: 0,
            address: "127.0.0.1:7777".into(),
            status: String::new(),
            child: None,
            online,
        }
    }
}
/// Menu captures show the menu without starting anything.
impl Default for Menu {
    fn default() -> Self {
        Self::new(Box::new(Offline))
    }
}
struct Offline;
impl OnlineService for Offline {
    fn ready(&mut self) -> Result<(), String> {
        Err("Online play is unavailable in a menu capture".into())
    }
    fn join_request(&mut self) -> Option<u64> {
        None
    }
    fn release(&mut self) {}
}
impl Drop for Menu {
    fn drop(&mut self) {
        if let Some(child) = &mut self.child {
            let _ = child.kill();
            let _ = child.wait();
        }
    }
}

pub fn run_menu(online: impl OnlineService) -> Result<(), String> {
    let snapshot = AuthoritativeMatch::new(38).snapshot();
    App::new()
        .add_plugins(DefaultPlugins.set(WindowPlugin {
            primary_window: None,
            exit_condition: ExitCondition::DontExit,
            ..default()
        }))
        .insert_resource(SceneSnapshot(snapshot))
        .insert_resource(Menu::new(Box::new(online)))
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
    super::capture::render_scene_png(
        &AuthoritativeMatch::new(38).snapshot(),
        output,
        super::capture::OffscreenView::Menu,
    )
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
    for (index, label) in ITEMS.iter().enumerate() {
        let y = item_y(index);
        commands.spawn((
            SceneVisual,
            Sprite::from_color(
                if menu.selected == index {
                    Color::srgb_u8(35, 95, 130)
                } else {
                    Color::srgb_u8(22, 35, 49)
                },
                Vec2::new(760.0, 52.0),
            ),
            Transform::from_xyz(0.0, y, 0.0),
        ));
        text(
            commands,
            &if index == ADDRESS {
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
        "Click or use arrows / D-pad, Enter / A to start.\nSelect Address; Ctrl+A clears it for the host's IP and port.\nOnline: pick a friend in Steam's invite window; they accept the invite in Steam.\nLocal: keyboard + mouse and one controller, or two controllers.\nConnect your controllers before starting the match.",
        -190.0,
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

/// The client arguments for a menu choice. Online play first checks that Steam is ready.
pub fn menu_match_args(
    selected: usize,
    address: &str,
    online: &mut dyn OnlineService,
) -> Result<Vec<String>, String> {
    let args = match selected {
        0 => vec!["visible-flow", "--ticks", "4294967295"],
        1 => vec![
            "host",
            "--bind",
            "0.0.0.0",
            "--port",
            "7777",
            "--interactive",
        ],
        2 => {
            let endpoint = address
                .parse::<std::net::SocketAddr>()
                .map_err(|_| "Enter an IPv4 address and port, for example 192.168.1.2:7777")?;
            if !endpoint.is_ipv4() {
                return Err("Join requires an IPv4 address and port".into());
            }
            vec![
                "join",
                "--address",
                address,
                "--client",
                "1",
                "--interactive",
            ]
        }
        STEAM => {
            online.ready()?;
            vec!["steam-host"]
        }
        _ => return Err("Select Local, Host, Join or Online to start".into()),
    };
    Ok(args.into_iter().map(str::to_owned).collect())
}

/// The client arguments that join a friend's Steam lobby.
pub fn steam_join_args(lobby: u64) -> Vec<String> {
    vec!["steam-join".into(), "--lobby".into(), lobby.to_string()]
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
    window.resizable = false;
    if let Some(child) = &mut menu.child {
        match child.try_wait() {
            Ok(Some(status)) => {
                menu.child = None;
                window.visible = true;
                if status.success() {
                    menu.status.clear();
                } else {
                    menu.status =
                        "Session ended or could not connect. Check the host and try again.".into();
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
    let mut start = None;
    if let Some(lobby) = menu.online.join_request() {
        start = Some(Ok(steam_join_args(lobby)));
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
        menu.selected = (menu.selected + ITEMS.len() - 1) % ITEMS.len();
    }
    if down {
        menu.selected = (menu.selected + 1) % ITEMS.len();
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
        for index in 0..ITEMS.len() {
            if point.x.abs() < 380.0 && (point.y - item_y(index)).abs() < 26.0 {
                menu.selected = index;
                confirm = index != ADDRESS;
            }
        }
    }
    if menu.selected == ADDRESS
        && keys.just_pressed(KeyCode::KeyA)
        && (keys.pressed(KeyCode::ControlLeft) || keys.pressed(KeyCode::ControlRight))
    {
        menu.address.clear();
    }
    for event in keyboard.read() {
        if menu.selected != ADDRESS || !event.state.is_pressed() {
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
    if start.is_none() && confirm && menu.selected != ADDRESS {
        let Menu {
            selected,
            address,
            online,
            ..
        } = &mut *menu;
        start = Some(menu_match_args(*selected, address, online.as_mut()));
    }
    if let Some(args) = start {
        let result = args.and_then(|args| {
            // The match process owns Steam while it runs; the menu takes it back afterwards.
            menu.online.release();
            let joining = args[0] == "steam-join";
            let executable = std::env::current_exe().map_err(|e| e.to_string())?;
            Command::new(executable)
                .args(args)
                .spawn()
                .map(|child| (child, joining))
                .map_err(|e| e.to_string())
        });
        match result {
            Ok((child, joining)) => {
                menu.child = Some(child);
                menu.status = if joining {
                    "Joining your friend's Steam match...".into()
                } else if menu.selected == 1 {
                    "Hosting on port 7777. Your partner should select Join now.".into()
                } else if menu.selected == STEAM {
                    "Steam's invite window opens in the match. Pick a friend to invite.".into()
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
    struct Steam(Result<(), String>);
    impl OnlineService for Steam {
        fn ready(&mut self) -> Result<(), String> {
            self.0.clone()
        }
        fn join_request(&mut self) -> Option<u64> {
            None
        }
        fn release(&mut self) {}
    }

    #[test]
    fn menu_launches_existing_local_and_network_routes() {
        let online = &mut Steam(Ok(()));
        assert_eq!(menu_match_args(0, "", online).unwrap()[0], "visible-flow");
        assert!(
            menu_match_args(1, "", online)
                .unwrap()
                .windows(2)
                .any(|pair| pair == ["--bind", "0.0.0.0"])
        );
        let join = menu_match_args(2, "127.0.0.1:7777", online).unwrap();
        assert!(join.windows(2).any(|pair| pair == ["--client", "1"]));
        assert!(join.iter().any(|arg| arg == "--interactive"));
        assert!(menu_match_args(2, "bad address", online).is_err());
        assert_eq!(menu_match_args(STEAM, "", online).unwrap(), ["steam-host"]);
        assert_eq!(steam_join_args(7), ["steam-join", "--lobby", "7"]);
    }

    #[test]
    fn online_choice_explains_that_steam_is_needed() {
        let needed = "Steam is needed for online play".to_owned();
        assert_eq!(
            menu_match_args(STEAM, "", &mut Steam(Err(needed.clone()))),
            Err(needed)
        );
    }
}
