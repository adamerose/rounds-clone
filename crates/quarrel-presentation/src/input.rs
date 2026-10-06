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
