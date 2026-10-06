use super::*;

/// Drives only public player inputs. Used for headless transport smoke and
/// automated local play; it never writes scores, health, offers or phases.
pub fn automated_input(player: u8, state: &MatchSnapshot) -> PlayerInput {
    let mut input = PlayerInput::default();
    let Some(flow) = state.flow.as_ref() else {
        return input;
    };
    let index = usize::from(player);
    if index >= state.players.len() {
        return input;
    }
    if flow.phase == FlowPhase::Draft {
        if flow.selected[index].is_none()
            && let Some(&item) = flow.offers[index].first()
        {
            input.flow = Some(FlowCommand {
                phase_revision: flow.phase_revision,
                action: FlowAction::Confirm(item),
            });
        }
    } else if flow.phase == FlowPhase::Combat {
        input.aim_at_opponent = true;
        input.fire = index == 0;
        let actor = &state.players[index];
        if let Some(target) = state.players.iter().find(|p| p.id != player && p.alive) {
            let distance = target.x_milli - actor.x_milli;
            input.move_axis = if distance.abs() > 120_000 {
                distance.signum() as i8
            } else {
                0
            };
            input.jump = actor.grounded && state.tick % 120 < 8;
        }
    }
    input
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct MatchContent {
    pub cards: Vec<ItemDefinition>,
    pub arenas: Vec<ArenaDefinition>,
}
impl MatchContent {
    pub fn load_default() -> Result<Self, String> {
        Ok(Self {
            cards: load_card_directory(&default_card_directory())?,
            arenas: load_arena_directory(&default_arena_directory())?,
        })
    }
}

#[derive(Clone, Debug, Deserialize, Serialize)]
pub struct InputRecording {
    pub config: MatchConfig,
    /// Logical starting content makes a recording independent of later asset additions.
    pub content: MatchContent,
    /// One vector of inputs per tick, in fighter order.
    pub frames: Vec<Vec<PlayerInput>>,
}
pub fn play_recording(recording: &InputRecording) -> Result<Vec<MatchSnapshot>, String> {
    let mut game =
        AuthoritativeMatch::with_content(recording.config.clone(), recording.content.clone())?;
    let mut snapshots = vec![game.snapshot()];
    for frame in &recording.frames {
        if frame.len() != recording.config.fighter_count {
            return Err("recording input count differs from fighter count".into());
        }
        let observation = game.snapshot();
        let inputs: Vec<_> = frame
            .iter()
            .enumerate()
            .map(|(i, input)| input.with_progressive_observation(i as u8, Some(&observation)))
            .collect();
        game.step(&inputs);
        snapshots.push(game.snapshot());
    }
    Ok(snapshots)
}
pub fn hash_snapshot(state: &MatchSnapshot) -> String {
    digest_json(state)
}
fn digest_json(value: &impl Serialize) -> String {
    format!("{:x}", Sha256::digest(serde_json::to_vec(value).unwrap()))
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn three_fighters_play_to_match_end_through_public_inputs() {
        let config = MatchConfig {
            fighter_count: 3,
            target_score: 2,
            ..Default::default()
        };
        let mut game = AuthoritativeMatch::with_config(config).unwrap();
        for _ in 0..2_400 {
            let state = game.snapshot();
            assert_eq!(state.players.len(), 3);
            if state.flow.as_ref().unwrap().phase == FlowPhase::MatchEnd {
                assert!(state.flow.unwrap().scores.contains(&2));
                return;
            }
            game.step(
                &(0..3)
                    .map(|id| automated_input(id, &state))
                    .collect::<Vec<_>>(),
            );
        }
        panic!("three-fighter match did not finish");
    }
    #[test]
    fn recorded_ordinary_inputs_reach_draft_and_match_end_identically() {
        let recording: InputRecording =
            serde_json::from_str(include_str!("../../../assets/replays/ordinary-match.json"))
                .unwrap();
        let a = play_recording(&recording).unwrap();
        let b = play_recording(&recording).unwrap();
        assert_eq!(a, b);
        assert!(a.iter().any(|s| {
            s.flow
                .as_ref()
                .is_some_and(|f| f.phase == FlowPhase::Draft && f.fight_number > 0)
        }));
        assert_eq!(
            a.last().unwrap().flow.as_ref().unwrap().phase,
            FlowPhase::MatchEnd
        );
    }
}
