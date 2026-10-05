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
        GamepadButton::South if is_draft_phase(flow.phase) => flow.hovered[usize::from(player)]
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
    fn navigation_ignores_empty_and_already_selected_offers() {
        let mut state = flow();
        state.offers[0].clear();
        assert!(draft_navigation_command(0, &state, 1).is_none());
        let mut state = flow();
        state.selected[0] = state.offers[0].first().copied();
        assert!(draft_navigation_command(0, &state, 1).is_none());
    }
}
