use super::*;

/// Local two-player controls, expressed entirely as authority inputs.
/// Orange: A/D, Space/W jump, S crouch, Shift block, F fire, I/J/K/L aim.
/// Blue: arrows (down crouches), right Shift block, Enter fire, numpad aim.
pub fn keyboard_combat_input(keys: &ButtonInput<KeyCode>, player: u8) -> PlayerInput {
    let [
        left,
        right,
        jump,
        block,
        fire,
        aim_up,
        aim_left,
        aim_down,
        aim_right,
    ] = if player == 0 {
        [
            KeyCode::KeyA,
            KeyCode::KeyD,
            KeyCode::KeyW,
            KeyCode::ShiftLeft,
            KeyCode::KeyF,
            KeyCode::KeyI,
            KeyCode::KeyJ,
            KeyCode::KeyK,
            KeyCode::KeyL,
        ]
    } else {
        [
            KeyCode::ArrowLeft,
            KeyCode::ArrowRight,
            KeyCode::ArrowUp,
            KeyCode::ShiftRight,
            KeyCode::Enter,
            KeyCode::Numpad8,
            KeyCode::Numpad4,
            KeyCode::Numpad5,
            KeyCode::Numpad6,
        ]
    };
    let axis =
        |positive, negative| i16::from(keys.pressed(positive)) - i16::from(keys.pressed(negative));
    let aim_x = axis(aim_right, aim_left) * 1_000;
    let aim_y = axis(aim_up, aim_down) * 1_000;
    PlayerInput {
        move_axis: axis(right, left) as i8,
        aim_x,
        aim_y,
        aim_at_opponent: aim_x == 0 && aim_y == 0,
        jump: keys.pressed(jump) || (player == 0 && keys.pressed(KeyCode::Space)),
        crouch: keys.pressed(if player == 0 {
            KeyCode::KeyS
        } else {
            KeyCode::ArrowDown
        }),
        block: keys.pressed(block),
        fire: keys.pressed(fire),
        ..PlayerInput::default()
    }
}

pub fn keyboard_mouse_combat_input(
    keys: &ButtonInput<KeyCode>,
    mouse_buttons: &ButtonInput<MouseButton>,
    mouse_aim: Option<Vec2>,
) -> PlayerInput {
    mouse_combat_input(keyboard_combat_input(keys, 0), mouse_buttons, mouse_aim)
}

/// Applies cursor aim computed by the rendering camera, preserving keyboard aim.
pub(super) fn mouse_combat_input(
    mut input: PlayerInput,
    mouse_buttons: &ButtonInput<MouseButton>,
    mouse_aim: Option<Vec2>,
) -> PlayerInput {
    if let Some(aim) =
        mouse_aim.filter(|aim| input.aim_at_opponent && aim.length_squared() > f32::EPSILON)
    {
        let scale = aim.x.abs().max(aim.y.abs());
        input.aim_x = (aim.x * 1_000.0 / scale) as i16;
        input.aim_y = (aim.y * 1_000.0 / scale) as i16;
        input.aim_at_opponent = false;
    }
    input.fire |= mouse_buttons.pressed(MouseButton::Left);
    input.block |= mouse_buttons.pressed(MouseButton::Right);
    input
}
/// A network peer owns one fighter: primary controls work for either assigned ID,
/// while the second local keyboard bindings remain available as aliases.
pub(super) fn single_player_keyboard_input(keys: &ButtonInput<KeyCode>) -> PlayerInput {
    let mut input = keyboard_combat_input(keys, 0);
    let aliases = keyboard_combat_input(keys, 1);
    if input.move_axis == 0 {
        input.move_axis = aliases.move_axis;
    }
    if input.aim_at_opponent {
        input.aim_x = aliases.aim_x;
        input.aim_y = aliases.aim_y;
        input.aim_at_opponent = aliases.aim_at_opponent;
    }
    input.jump |= aliases.jump;
    input.crouch |= aliases.crouch;
    input.fire |= aliases.fire;
    input.block |= aliases.block;
    input
}
pub(super) fn single_player_flow_command(
    key: KeyCode,
    player: u8,
    flow: &FlowSnapshot,
) -> Option<FlowCommand> {
    let key = if player == 0 {
        match key {
            KeyCode::ArrowLeft => KeyCode::KeyA,
            KeyCode::ArrowRight => KeyCode::KeyD,
            KeyCode::Enter => KeyCode::Space,
            KeyCode::KeyK => KeyCode::KeyY,
            KeyCode::KeyL => KeyCode::KeyN,
            _ => key,
        }
    } else {
        match key {
            KeyCode::KeyA => KeyCode::ArrowLeft,
            KeyCode::KeyD => KeyCode::ArrowRight,
            KeyCode::Space => KeyCode::Enter,
            KeyCode::KeyY => KeyCode::KeyK,
            KeyCode::KeyN => KeyCode::KeyL,
            _ => key,
        }
    };
    keyboard_flow_command(key, player, flow)
}

pub fn gamepad_combat_input(gamepad: &Gamepad) -> PlayerInput {
    let movement = gamepad.left_stick().x;
    let aim = gamepad.right_stick();
    PlayerInput {
        move_axis: if movement.abs() > 0.2 {
            movement.signum() as i8
        } else {
            0
        },
        aim_x: (aim.x * 1_000.0) as i16,
        aim_y: (aim.y * 1_000.0) as i16,
        aim_at_opponent: aim.length_squared() < 0.04,
        jump: gamepad.pressed(GamepadButton::South),
        crouch: gamepad.left_stick().y < -0.5 || gamepad.pressed(GamepadButton::DPadDown),
        fire: gamepad.pressed(GamepadButton::RightTrigger2),
        block: gamepad.pressed(GamepadButton::West),
        ..PlayerInput::default()
    }
}

/// A controller drives its fighter only while it asks for something. Stick noise inside
/// the movement and aim dead zones leaves the slot's keyboard controls in charge.
pub(super) fn active_gamepad_combat_input(gamepad: &Gamepad) -> Option<PlayerInput> {
    let input = gamepad_combat_input(gamepad);
    (input.move_axis != 0
        || !input.aim_at_opponent
        || input.jump
        || input.crouch
        || input.fire
        || input.block)
        .then_some(input)
}

/// Chooses each local fighter's combat device. Controllers take fighters as before,
/// but a fighter whose controller is idle keeps its own keyboard layout (fighter one
/// also keeps the mouse).
pub(super) fn local_combat_inputs(
    keys: &ButtonInput<KeyCode>,
    mouse_buttons: &ButtonInput<MouseButton>,
    controllers: &[&Gamepad],
    fighters: usize,
    first_fighter_mouse_aim: Option<Vec2>,
) -> Vec<PlayerInput> {
    let keyboard_slots = if controllers.is_empty() {
        fighters
    } else {
        fighters.min(2)
    };
    let mut live = (0..fighters)
        .map(|player| {
            if player < keyboard_slots {
                keyboard_combat_input(keys, player as u8)
            } else {
                PlayerInput::default()
            }
        })
        .collect::<Vec<_>>();
    if let Some(input) = live.first_mut() {
        *input = mouse_combat_input(*input, mouse_buttons, first_fighter_mouse_aim);
    }
    let controller_start = usize::from(controllers.len() == 1);
    for (offset, gamepad) in controllers.iter().enumerate() {
        let player = controller_start + offset;
        if player >= fighters {
            break;
        }
        if let Some(input) = active_gamepad_combat_input(gamepad) {
            live[player] = input;
        }
    }
    live
}

/// An online peer's combat input: its first controller while active, otherwise its keyboard.
pub(super) fn online_combat_input(
    keys: &ButtonInput<KeyCode>,
    mouse_buttons: &ButtonInput<MouseButton>,
    gamepad: Option<&Gamepad>,
    mouse_aim: Option<Vec2>,
) -> PlayerInput {
    gamepad
        .and_then(active_gamepad_combat_input)
        .unwrap_or_else(|| {
            mouse_combat_input(single_player_keyboard_input(keys), mouse_buttons, mouse_aim)
        })
}

/// Maps concrete keyboard input into the same semantic command sent over the
/// network. Presentation never chooses or applies an item itself.
pub fn keyboard_flow_command(key: KeyCode, player: u8, flow: &FlowSnapshot) -> Option<FlowCommand> {
    let yes = if player == 0 {
        KeyCode::KeyY
    } else {
        KeyCode::KeyK
    };
    let no = if player == 0 {
        KeyCode::KeyN
    } else {
        KeyCode::KeyL
    };
    let left = if player == 0 {
        KeyCode::KeyA
    } else {
        KeyCode::ArrowLeft
    };
    let right = if player == 0 {
        KeyCode::KeyD
    } else {
        KeyCode::ArrowRight
    };
    let confirm = if player == 0 {
        KeyCode::Space
    } else {
        KeyCode::Enter
    };
    match key {
        key if key == yes && flow.phase == FlowPhase::MatchEnd => Some(FlowCommand {
            phase_revision: flow.phase_revision,
            action: FlowAction::VoteYes,
        }),
        key if key == no && flow.phase == FlowPhase::MatchEnd => Some(FlowCommand {
            phase_revision: flow.phase_revision,
            action: FlowAction::VoteNo,
        }),
        key if key == left && is_draft_phase(flow.phase) => {
            draft_navigation_command(player, flow, -1)
        }
        key if key == right && is_draft_phase(flow.phase) => {
            draft_navigation_command(player, flow, 1)
        }
        key if key == confirm && is_draft_phase(flow.phase) => flow
            .hovered
            .get(usize::from(player))
            .copied()
            .flatten()
            .or_else(|| {
                flow.offers
                    .get(usize::from(player))
                    .and_then(|offers| offers.first().copied())
            })
            .map(|item| FlowCommand {
                phase_revision: flow.phase_revision,
                action: FlowAction::Confirm(item),
            }),
        _ => None,
    }
}

/// Maps the primary keyboard layout to the assigned player's flow offer.
pub fn primary_keyboard_flow_command(
    key: KeyCode,
    player: u8,
    flow: &FlowSnapshot,
) -> Option<FlowCommand> {
    let key = if player == 0 {
        key
    } else {
        match key {
            KeyCode::KeyY => KeyCode::KeyK,
            KeyCode::KeyN => KeyCode::KeyL,
            KeyCode::KeyA => KeyCode::ArrowLeft,
            KeyCode::KeyD => KeyCode::ArrowRight,
            KeyCode::Space => KeyCode::Enter,
            _ => return None,
        }
    };
    keyboard_flow_command(key, player, flow)
}

pub fn gamepad_flow_command(
    button: GamepadButton,
    player: u8,
    flow: &FlowSnapshot,
) -> Option<FlowCommand> {
    match button {
        GamepadButton::South if flow.phase == FlowPhase::MatchEnd => Some(FlowCommand {
            phase_revision: flow.phase_revision,
            action: FlowAction::VoteYes,
        }),
        GamepadButton::East if flow.phase == FlowPhase::MatchEnd => Some(FlowCommand {
            phase_revision: flow.phase_revision,
            action: FlowAction::VoteNo,
        }),
        GamepadButton::DPadLeft if is_draft_phase(flow.phase) => {
            draft_navigation_command(player, flow, -1)
        }
        GamepadButton::DPadRight if is_draft_phase(flow.phase) => {
            draft_navigation_command(player, flow, 1)
        }
        GamepadButton::South if is_draft_phase(flow.phase) => flow
            .hovered
            .get(usize::from(player))
            .copied()
            .flatten()
            .or_else(|| {
                flow.offers
                    .get(usize::from(player))
                    .and_then(|offers| offers.first().copied())
            })
            .map(|item| FlowCommand {
                phase_revision: flow.phase_revision,
                action: FlowAction::Confirm(item),
            }),
        _ => None,
    }
}

pub(super) fn is_draft_phase(phase: FlowPhase) -> bool {
    phase == FlowPhase::Draft
}

pub(super) fn draft_navigation_command(
    player: u8,
    flow: &FlowSnapshot,
    direction: isize,
) -> Option<FlowCommand> {
    let index = usize::from(player);
    let offers = flow.offers.get(index)?;
    if offers.is_empty() || flow.selected.get(index).copied().flatten().is_some() {
        return None;
    }
    let current = flow.hovered[index]
        .and_then(|hovered| offers.iter().position(|item| *item == hovered))
        .unwrap_or(0);
    let next = (current as isize + direction).rem_euclid(offers.len() as isize) as usize;
    Some(FlowCommand {
        phase_revision: flow.phase_revision,
        action: FlowAction::Hover(offers[next]),
    })
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::input::{
        ButtonState, InputPlugin,
        gamepad::{RawGamepadAxisChangedEvent, RawGamepadButtonChangedEvent, RawGamepadEvent},
        keyboard::{Key, KeyboardInput},
        mouse::MouseButtonInput,
    };

    #[test]
    fn keyboard_aim_takes_priority_over_a_parked_mouse() {
        let mut keys = ButtonInput::default();
        keys.press(KeyCode::KeyI);
        let input = keyboard_mouse_combat_input(&keys, &ButtonInput::default(), Some(Vec2::X));
        assert_eq!((input.aim_x, input.aim_y), (0, 1_000));
    }

    #[test]
    fn keyboard_slots_preserve_jump_crouch_fire_and_block_controls() {
        let mut keys = ButtonInput::default();
        for key in [
            KeyCode::KeyA,
            KeyCode::Space,
            KeyCode::KeyS,
            KeyCode::KeyF,
            KeyCode::ShiftLeft,
        ] {
            keys.press(key);
        }
        let first = keyboard_combat_input(&keys, 0);
        assert_eq!(first.move_axis, -1);
        assert!(first.jump && first.crouch && first.fire && first.block);
        assert!(!keyboard_combat_input(&keys, 1).jump);
        keys.clear();
        for key in [
            KeyCode::ArrowRight,
            KeyCode::ArrowUp,
            KeyCode::ArrowDown,
            KeyCode::Enter,
            KeyCode::ShiftRight,
        ] {
            keys.press(key);
        }
        let second = keyboard_combat_input(&keys, 1);
        assert_eq!(second.move_axis, 1);
        assert!(second.jump && second.crouch && second.fire && second.block);
    }
    #[test]
    fn network_peers_get_primary_controls_and_keep_secondary_aliases() {
        let mut keys = ButtonInput::default();
        for key in [KeyCode::Space, KeyCode::KeyA, KeyCode::KeyS] {
            keys.press(key);
        }
        let input = single_player_keyboard_input(&keys);
        assert!(input.jump && input.crouch);
        assert_eq!(input.move_axis, -1);
        let mut aliases = ButtonInput::default();
        for key in [KeyCode::ArrowUp, KeyCode::ArrowRight, KeyCode::Enter] {
            aliases.press(key);
        }
        let input = single_player_keyboard_input(&aliases);
        assert!(input.jump && input.fire);
        assert_eq!(input.move_axis, 1);
    }
    #[test]
    fn network_primary_confirm_and_navigation_use_the_assigned_offer() {
        let flow = flow();
        for player in 0..2 {
            assert_eq!(
                single_player_flow_command(KeyCode::Space, player, &flow)
                    .unwrap()
                    .action,
                FlowAction::Confirm(flow.hovered[usize::from(player)].unwrap())
            );
            assert_eq!(
                single_player_flow_command(KeyCode::Enter, player, &flow)
                    .unwrap()
                    .action,
                FlowAction::Confirm(flow.hovered[usize::from(player)].unwrap())
            );
            let hover = single_player_flow_command(KeyCode::KeyD, player, &flow).unwrap();
            assert_eq!(
                hover.action,
                FlowAction::Hover(flow.offers[usize::from(player)][1])
            );
        }
    }

    #[test]
    fn network_voting_accepts_both_keyboard_sets_for_either_peer() {
        let mut state = flow();
        state.phase = FlowPhase::MatchEnd;
        for player in 0..2 {
            for key in [KeyCode::KeyY, KeyCode::KeyK] {
                assert_eq!(
                    single_player_flow_command(key, player, &state)
                        .unwrap()
                        .action,
                    FlowAction::VoteYes
                );
            }
            for key in [KeyCode::KeyN, KeyCode::KeyL] {
                assert_eq!(
                    single_player_flow_command(key, player, &state)
                        .unwrap()
                        .action,
                    FlowAction::VoteNo
                );
            }
        }
    }

    fn flow() -> FlowSnapshot {
        AuthoritativeMatch::new(38).snapshot().flow.unwrap()
    }

    #[test]
    fn each_keyboard_slot_confirms_its_own_offer_and_vote() {
        let mut draft = flow();
        let first = draft.offers[0][0];
        let second = draft.offers[1][0];
        assert_eq!(
            keyboard_flow_command(KeyCode::Space, 0, &draft)
                .unwrap()
                .action,
            FlowAction::Confirm(first)
        );
        assert_eq!(
            keyboard_flow_command(KeyCode::Enter, 1, &draft)
                .unwrap()
                .action,
            FlowAction::Confirm(second)
        );
        assert!(keyboard_flow_command(KeyCode::Space, 1, &draft).is_none());
        draft.phase = FlowPhase::MatchEnd;
        assert_eq!(
            keyboard_flow_command(KeyCode::KeyY, 0, &draft)
                .unwrap()
                .action,
            FlowAction::VoteYes
        );
        assert_eq!(
            keyboard_flow_command(KeyCode::KeyK, 1, &draft)
                .unwrap()
                .action,
            FlowAction::VoteYes
        );
        assert!(keyboard_flow_command(KeyCode::KeyY, 1, &draft).is_none());
        assert_eq!(
            keyboard_flow_command(KeyCode::KeyL, 1, &draft)
                .unwrap()
                .action,
            FlowAction::VoteNo
        );
    }

    #[test]
    fn primary_keyboard_can_choose_the_assigned_players_offer() {
        let draft = flow();
        assert_eq!(
            primary_keyboard_flow_command(KeyCode::Space, 1, &draft)
                .unwrap()
                .action,
            FlowAction::Confirm(draft.offers[1][0])
        );
    }

    #[test]
    fn navigation_ignores_empty_and_already_selected_offers() {
        let mut state = flow();
        state.offers[0].clear();
        assert!(draft_navigation_command(0, &state, 1).is_none());
        let mut state = flow();
        state.selected[0] = state.offers[0].first().copied();
        assert!(draft_navigation_command(0, &state, 1).is_none());
    }

    #[test]
    fn keyboard_mouse_and_simulated_gamepad_produce_combat_inputs() {
        let mut keys = ButtonInput::default();
        keys.press(KeyCode::KeyD);
        let mut mouse = ButtonInput::default();
        mouse.press(MouseButton::Left);
        mouse.press(MouseButton::Right);
        let keyboard = keyboard_mouse_combat_input(&keys, &mouse, Some(Vec2::new(-2.0, 1.0)));
        assert_eq!(keyboard.move_axis, 1);
        assert_eq!((keyboard.aim_x, keyboard.aim_y), (-1_000, 500));
        assert!(keyboard.fire && keyboard.block);

        let mut app = App::new();
        app.add_plugins((MinimalPlugins, InputPlugin));
        let gamepad = app.world_mut().spawn(Gamepad::default()).id();
        app.world_mut()
            .write_message(RawGamepadEvent::Button(RawGamepadButtonChangedEvent::new(
                gamepad,
                GamepadButton::RightTrigger2,
                1.0,
            )));
        app.world_mut()
            .write_message(RawGamepadEvent::Axis(RawGamepadAxisChangedEvent::new(
                gamepad,
                GamepadAxis::LeftStickX,
                -1.0,
            )));
        app.world_mut()
            .write_message(RawGamepadEvent::Axis(RawGamepadAxisChangedEvent::new(
                gamepad,
                GamepadAxis::RightStickY,
                1.0,
            )));
        app.update();
        let controller = gamepad_combat_input(app.world().get::<Gamepad>(gamepad).unwrap());
        assert_eq!(controller.move_axis, -1);
        assert_eq!((controller.aim_x, controller.aim_y), (0, 1_000));
        assert!(controller.fire);
    }

    struct Devices {
        app: App,
        window: Entity,
        pads: Vec<Entity>,
    }

    impl Devices {
        fn new(controllers: usize) -> Self {
            let mut app = App::new();
            app.add_plugins((MinimalPlugins, InputPlugin));
            let window = app.world_mut().spawn_empty().id();
            let pads = (0..controllers)
                .map(|_| app.world_mut().spawn(Gamepad::default()).id())
                .collect();
            Self { app, window, pads }
        }

        fn key(&mut self, key_code: KeyCode, pressed: bool) {
            self.app.world_mut().write_message(KeyboardInput {
                key_code,
                logical_key: Key::Unidentified(bevy::input::keyboard::NativeKey::Unidentified),
                state: if pressed {
                    ButtonState::Pressed
                } else {
                    ButtonState::Released
                },
                text: None,
                repeat: false,
                window: self.window,
            });
        }

        fn keys(&mut self, keys: &[KeyCode], pressed: bool) {
            for key in keys {
                self.key(*key, pressed);
            }
            self.app.update();
        }

        fn axis(&mut self, pad: usize, axis: GamepadAxis, value: f32) {
            self.app.world_mut().write_message(RawGamepadEvent::Axis(
                RawGamepadAxisChangedEvent::new(self.pads[pad], axis, value),
            ));
        }

        fn button(&mut self, pad: usize, button: GamepadButton, value: f32) {
            self.app.world_mut().write_message(RawGamepadEvent::Button(
                RawGamepadButtonChangedEvent::new(self.pads[pad], button, value),
            ));
        }

        /// Stick drift that stays inside the movement and aim dead zones.
        fn drift(&mut self, pad: usize) {
            self.axis(pad, GamepadAxis::LeftStickX, 0.15);
            self.axis(pad, GamepadAxis::LeftStickY, -0.3);
            self.axis(pad, GamepadAxis::RightStickX, 0.12);
            self.axis(pad, GamepadAxis::RightStickY, -0.12);
            self.app.update();
            assert!(
                self.gamepads()[pad].left_stick().x > 0.0,
                "drift reaches the controller"
            );
        }

        fn gamepads(&self) -> Vec<&Gamepad> {
            self.pads
                .iter()
                .map(|pad| self.app.world().get::<Gamepad>(*pad).unwrap())
                .collect()
        }

        fn local(&self) -> Vec<PlayerInput> {
            let world = self.app.world();
            local_combat_inputs(
                world.resource(),
                world.resource(),
                &self.gamepads(),
                2,
                None,
            )
        }

        fn online(&self) -> PlayerInput {
            let world = self.app.world();
            online_combat_input(
                world.resource(),
                world.resource(),
                self.gamepads().first().copied(),
                None,
            )
        }
    }

    const FIRST_KEYS: [KeyCode; 5] = [
        KeyCode::KeyD,
        KeyCode::Space,
        KeyCode::ShiftLeft,
        KeyCode::KeyF,
        KeyCode::KeyI,
    ];
    const SECOND_KEYS: [KeyCode; 5] = [
        KeyCode::ArrowLeft,
        KeyCode::ArrowUp,
        KeyCode::ShiftRight,
        KeyCode::Enter,
        KeyCode::Numpad4,
    ];

    fn keyboard_actions(move_axis: i8, aim_x: i16, aim_y: i16) -> PlayerInput {
        PlayerInput {
            move_axis,
            aim_x,
            aim_y,
            jump: true,
            block: true,
            fire: true,
            ..PlayerInput::default()
        }
    }

    fn neutral() -> PlayerInput {
        PlayerInput {
            aim_at_opponent: true,
            ..PlayerInput::default()
        }
    }

    #[test]
    fn idle_controllers_leave_each_local_slot_on_its_own_keyboard() {
        for controllers in 1..=2 {
            let mut devices = Devices::new(controllers);
            for pad in 0..controllers {
                devices.drift(pad);
                assert!(active_gamepad_combat_input(devices.gamepads()[pad]).is_none());
            }
            devices.keys(&FIRST_KEYS, true);
            assert_eq!(
                devices.local(),
                [keyboard_actions(1, 0, 1_000), neutral()],
                "{controllers} idle controllers, first keyboard"
            );
            devices.keys(&FIRST_KEYS, false);
            devices.keys(&SECOND_KEYS, true);
            assert_eq!(
                devices.local(),
                [neutral(), keyboard_actions(-1, -1_000, 0)],
                "{controllers} idle controllers, second keyboard"
            );
            devices.keys(&SECOND_KEYS, false);
            assert_eq!(devices.local(), [neutral(), neutral()]);
        }
    }

    #[test]
    fn active_controllers_take_precedence_until_they_return_to_neutral() {
        let mut devices = Devices::new(2);
        devices.keys(
            &[FIRST_KEYS.as_slice(), SECOND_KEYS.as_slice()].concat(),
            true,
        );
        devices.axis(1, GamepadAxis::LeftStickX, 1.0);
        devices.app.update();
        let inputs = devices.local();
        assert_eq!(inputs[0], keyboard_actions(1, 0, 1_000));
        assert_eq!(inputs[1].move_axis, 1);
        assert!(!inputs[1].jump && !inputs[1].fire && inputs[1].aim_at_opponent);

        for (axis, button) in [
            (Some((GamepadAxis::RightStickX, -1.0)), None),
            (Some((GamepadAxis::LeftStickY, -1.0)), None),
            (None, Some(GamepadButton::South)),
            (None, Some(GamepadButton::West)),
            (None, Some(GamepadButton::RightTrigger2)),
            (None, Some(GamepadButton::DPadDown)),
        ] {
            let mut devices = Devices::new(1);
            devices.keys(&SECOND_KEYS, true);
            if let Some((axis, value)) = axis {
                devices.axis(0, axis, value);
            }
            if let Some(button) = button {
                devices.button(0, button, 1.0);
            }
            devices.app.update();
            let controller = devices.local()[1];
            assert_ne!(controller, keyboard_actions(-1, -1_000, 0));
            assert_eq!(controller.move_axis, 0, "{axis:?} {button:?}");
            assert_eq!(devices.online(), controller);

            if let Some((axis, _)) = axis {
                devices.axis(0, axis, 0.0);
            }
            if let Some(button) = button {
                devices.button(0, button, 0.0);
            }
            devices.app.update();
            assert_eq!(devices.local()[1], keyboard_actions(-1, -1_000, 0));
        }

        devices.axis(1, GamepadAxis::LeftStickX, 0.0);
        devices.app.update();
        assert_eq!(devices.local()[1], keyboard_actions(-1, -1_000, 0));
        devices.axis(0, GamepadAxis::LeftStickX, -1.0);
        devices.app.update();
        let inputs = devices.local();
        assert_eq!(inputs[0].move_axis, -1);
        assert!(!inputs[0].fire);
        assert_eq!(inputs[1], keyboard_actions(-1, -1_000, 0));
    }

    #[test]
    fn removing_a_controller_leaves_its_slot_on_the_keyboard() {
        let mut devices = Devices::new(1);
        devices.axis(0, GamepadAxis::LeftStickX, 1.0);
        devices.keys(&SECOND_KEYS, true);
        assert_eq!(devices.local()[1].move_axis, 1);
        let pad = devices.pads.pop().unwrap();
        devices.app.world_mut().despawn(pad);
        assert_eq!(devices.local()[1], keyboard_actions(-1, -1_000, 0));
        assert_eq!(devices.online(), keyboard_actions(-1, -1_000, 0));
    }

    #[test]
    fn online_peers_use_their_keyboard_while_their_controller_is_idle() {
        let mut devices = Devices::new(1);
        devices.drift(0);
        devices.keys(&FIRST_KEYS, true);
        assert_eq!(devices.online(), keyboard_actions(1, 0, 1_000));
        devices.axis(0, GamepadAxis::LeftStickX, -1.0);
        devices.app.update();
        let controller = devices.online();
        assert_eq!(controller.move_axis, -1);
        assert!(!controller.fire && controller.aim_at_opponent);
        devices.drift(0);
        assert_eq!(devices.online(), keyboard_actions(1, 0, 1_000));
        devices.keys(&FIRST_KEYS, false);
        devices.keys(&SECOND_KEYS, true);
        assert_eq!(devices.online(), keyboard_actions(-1, -1_000, 0));
    }

    /// Each peer holds a different keyboard layout and aim so the trace shows which slot got which.
    const PEER_KEYS: [[KeyCode; 2]; 2] = [
        [KeyCode::KeyD, KeyCode::KeyI],
        [KeyCode::ArrowRight, KeyCode::Numpad4],
    ];
    const PEER_AIMS: [(i8, i16, i16); 2] = [(1, 0, 1_000), (1, -1_000, 0)];

    /// The selected keyboard input reaches the real live authority for either assigned
    /// fighter while that peer's controller sits idle, and reaches only that fighter.
    #[test]
    fn online_keyboard_input_reaches_the_authority_past_an_idle_controller() {
        use quarrel_network::{AppliedTick, LiveClient, LiveServer};
        use std::{thread, time::Instant};

        let config = quarrel_sim::MatchConfig::default();
        let ticks = 600;
        let trace_path = std::env::temp_dir().join(format!(
            "quarrel-idle-controller-trace-{}.json",
            std::process::id()
        ));
        let server = LiveServer::bind("127.0.0.1:0").unwrap();
        let address = server.local_addr().unwrap();
        let server_config = config.clone();
        let server_trace = trace_path.clone();
        let authority = thread::spawn(move || {
            server
                .run(server_config, ticks, Some(&server_trace))
                .unwrap()
        });
        let clients = (0..2_u8)
            .map(|id| {
                let config = config.clone();
                thread::spawn(move || {
                    let client = LiveClient::connect(address, id, config).unwrap();
                    let handle = client.handle();
                    let run = thread::spawn(move || client.run(ticks).unwrap());
                    let until = Instant::now() + Duration::from_secs(8);
                    let mut revision = None;
                    loop {
                        if let Some((state, _)) = handle.latest() {
                            let flow = state.flow.clone().unwrap();
                            if flow.phase == FlowPhase::Combat {
                                break;
                            }
                            if flow.phase == FlowPhase::Draft
                                && revision != Some(flow.phase_revision)
                            {
                                handle
                                    .push_flow(FlowCommand {
                                        phase_revision: flow.phase_revision,
                                        action: FlowAction::Confirm(
                                            flow.offers[usize::from(id)][0],
                                        ),
                                    })
                                    .unwrap();
                                revision = Some(flow.phase_revision);
                            }
                        }
                        assert!(Instant::now() < until, "combat did not start");
                        thread::sleep(Duration::from_millis(1));
                    }
                    let mut devices = Devices::new(1);
                    devices.drift(0);
                    devices.keys(&PEER_KEYS[usize::from(id)], true);
                    let input = devices.online();
                    let expected = PEER_AIMS[usize::from(id)];
                    assert_eq!((input.move_axis, input.aim_x, input.aim_y), expected);
                    handle.set_held(input);
                    let fighter = usize::from(id);
                    loop {
                        if let Some((state, _)) = handle.latest()
                            && (
                                1,
                                state.players[fighter].aim_x,
                                state.players[fighter].aim_y,
                            ) == expected
                        {
                            break;
                        }
                        assert!(Instant::now() < until, "keyboard input not observed");
                        thread::sleep(Duration::from_millis(1));
                    }
                    run.join().unwrap()
                })
            })
            .collect::<Vec<_>>();
        for client in clients {
            assert_eq!(client.join().unwrap().result, "completed");
        }
        assert_eq!(authority.join().unwrap().result, "completed");
        let trace: Vec<AppliedTick> =
            serde_json::from_slice(&std::fs::read(&trace_path).unwrap()).unwrap();
        std::fs::remove_file(trace_path).unwrap();
        for fighter in 0..2 {
            let applied = |aims: (i8, i16, i16)| {
                trace.iter().any(|row| {
                    let input = row.inputs[fighter];
                    (input.move_axis, input.aim_x, input.aim_y) == aims && !input.aim_at_opponent
                })
            };
            assert!(applied(PEER_AIMS[fighter]));
            assert!(!applied(PEER_AIMS[1 - fighter]));
        }
        let mut game = AuthoritativeMatch::with_config(config).unwrap();
        for row in &trace {
            game.step(&row.inputs);
            assert_eq!(quarrel_sim::hash_snapshot(&game.snapshot()), row.hash);
        }
    }

    #[test]
    fn bevy_device_events_drive_a_match_from_draft_to_combat() {
        let mut app = App::new();
        app.add_plugins((MinimalPlugins, InputPlugin));
        let window = app.world_mut().spawn_empty().id();
        let gamepad = app.world_mut().spawn(Gamepad::default()).id();
        let mut game = AuthoritativeMatch::new(38);
        let draft = game.snapshot().flow.unwrap();
        app.world_mut().write_message(KeyboardInput {
            key_code: KeyCode::Space,
            logical_key: Key::Character(" ".into()),
            state: ButtonState::Pressed,
            text: Some(" ".into()),
            repeat: false,
            window,
        });
        app.world_mut()
            .write_message(RawGamepadEvent::Button(RawGamepadButtonChangedEvent::new(
                gamepad,
                GamepadButton::South,
                1.0,
            )));
        app.update();
        let keyboard =
            keyboard_mouse_combat_input(app.world().resource(), app.world().resource(), None);
        let controller = gamepad_combat_input(app.world().get::<Gamepad>(gamepad).unwrap());
        game.step(&[
            PlayerInput {
                flow: primary_keyboard_flow_command(KeyCode::Space, 0, &draft),
                ..keyboard
            },
            PlayerInput {
                flow: gamepad_flow_command(GamepadButton::South, 1, &draft),
                ..controller
            },
        ]);
        assert_eq!(game.snapshot().flow.unwrap().phase, FlowPhase::Combat);

        app.world_mut().write_message(KeyboardInput {
            key_code: KeyCode::KeyD,
            logical_key: Key::Character("d".into()),
            state: ButtonState::Pressed,
            text: Some("d".into()),
            repeat: false,
            window,
        });
        app.world_mut().write_message(MouseButtonInput {
            button: MouseButton::Left,
            state: ButtonState::Pressed,
            window,
        });
        app.world_mut()
            .write_message(RawGamepadEvent::Axis(RawGamepadAxisChangedEvent::new(
                gamepad,
                GamepadAxis::LeftStickX,
                -1.0,
            )));
        app.world_mut()
            .write_message(RawGamepadEvent::Button(RawGamepadButtonChangedEvent::new(
                gamepad,
                GamepadButton::RightTrigger2,
                1.0,
            )));
        app.update();
        let inputs = [
            keyboard_mouse_combat_input(
                app.world().resource(),
                app.world().resource(),
                Some(Vec2::X),
            ),
            gamepad_combat_input(app.world().get::<Gamepad>(gamepad).unwrap()),
        ];
        assert_eq!(inputs[0].move_axis, 1);
        assert!(inputs[0].fire && inputs[1].fire);
        assert_eq!(inputs[1].move_axis, -1);
        game.step(&inputs);
        assert!(game.snapshot().metrics.shots_fired > 0);
    }
}
