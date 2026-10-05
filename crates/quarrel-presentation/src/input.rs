use super::*;

/// Local two-player controls, expressed entirely as authority inputs.
/// Orange: A/D, W jump, S block, Space fire, I/J/K/L aim.
/// Blue: arrows, Enter fire, numpad 8/4/5/6 aim. Resting aim tracks the opponent.
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
            KeyCode::KeyS,
            KeyCode::Space,
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
            KeyCode::ArrowDown,
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
        jump: keys.pressed(jump),
        block: keys.pressed(block),
        fire: keys.pressed(fire),
        ..PlayerInput::default()
    }
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
        fire: gamepad.pressed(GamepadButton::RightTrigger2),
        block: gamepad.pressed(GamepadButton::West),
        ..PlayerInput::default()
    }
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
        KeyCode::ArrowLeft if is_draft_phase(flow.phase) => {
            draft_navigation_command(player, flow, -1)
        }
        KeyCode::ArrowRight if is_draft_phase(flow.phase) => {
            draft_navigation_command(player, flow, 1)
        }
        KeyCode::Enter | KeyCode::Space if is_draft_phase(flow.phase) => {
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
        GamepadButton::DPadLeft if is_draft_phase(flow.phase) => {
            draft_navigation_command(player, flow, -1)
        }
        GamepadButton::DPadRight if is_draft_phase(flow.phase) => {
            draft_navigation_command(player, flow, 1)
        }
        GamepadButton::South if is_draft_phase(flow.phase) => flow.hovered[usize::from(player)]
            .map(|item| FlowCommand {
                phase_revision: flow.phase_revision,
                action: FlowAction::Confirm(item),
            }),
        _ => None,
    }
}

pub(super) fn is_draft_phase(phase: FlowPhase) -> bool {
    matches!(phase, FlowPhase::Draft | FlowPhase::PostRoundDraft)
}

pub(super) fn draft_navigation_command(
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
