use super::*;

fn step_until(game: &mut AuthoritativeMatch, phase: FlowPhase) {
    for _ in 0..2_000 {
        if game.snapshot().flow.as_ref().unwrap().phase == phase {
            return;
        }
        game.step([PlayerInput::default(); 2]);
    }
    panic!(
        "flow did not reach {phase:?}; current {:?}",
        game.snapshot().flow.unwrap().phase
    );
}

fn confirm_active_offer(game: &mut AuthoritativeMatch) {
    let flow = game.snapshot().flow.unwrap();
    let player = flow.active_player.expect("draft has an active player") as usize;
    let item = flow.offers[player]
        .iter()
        .copied()
        .find(|item| {
            item_definition(*item).implementation == ImplementationState::Implemented
                && !flow.loadouts[player].contains(item)
        })
        .expect("draft has an implemented offer");
    let mut inputs = [PlayerInput::default(); 2];
    inputs[player].flow = Some(FlowCommand {
        phase_revision: flow.phase_revision,
        action: FlowAction::Hover(item),
    });
    game.step(inputs);
    let flow = game.snapshot().flow.unwrap();
    inputs[player].flow = Some(FlowCommand {
        phase_revision: flow.phase_revision,
        action: FlowAction::Confirm(item),
    });
    game.step(inputs);
    assert!(matches!(
        game.snapshot().flow.unwrap().phase,
        FlowPhase::Reveal | FlowPhase::PostRoundReveal
    ));
}

fn walk_blue_out(game: &mut AuthoritativeMatch) -> MatchSnapshot {
    let ring_outs = game.snapshot().metrics.ring_outs;
    for _ in 0..1_200 {
        game.step([
            PlayerInput::default(),
            PlayerInput {
                move_axis: 1,
                ..PlayerInput::default()
            },
        ]);
        let snapshot = game.snapshot();
        if snapshot.metrics.ring_outs > ring_outs {
            return snapshot;
        }
    }
    panic!("blue never reached the public ring-out boundary");
}

#[test]
fn orange_match_end_win_reaches_hanging_entry_without_missing_stage_data() {
    let mut game = AuthoritativeMatch::new_with_profile(57, ReplayProfile::MatchEndWaitingReplay);
    assert_eq!(
        game.snapshot().flow.unwrap().phase,
        FlowPhase::ResumedCombat
    );
    assert!(walk_blue_out(&mut game).metrics.ring_outs > 0);
    step_until(&mut game, FlowPhase::PostRoundDraft);
    confirm_active_offer(&mut game);
    step_until(&mut game, FlowPhase::HangingEntry);
    game.step([PlayerInput::default(); 2]);
    let hanging = game.snapshot();
    assert_eq!(hanging.flow.unwrap().phase, FlowPhase::HangingEntry);
    assert!(hanging.hanging_entry.is_some());
    assert!(hanging.arena.is_empty());
}

#[test]
fn a_new_match_after_waiting_reaches_timber_without_missing_stage_data() {
    let profile = ReplayProfile::MatchEndWaitingReplay;
    let mut game = AuthoritativeMatch::new_with_profile(57, profile);
    let scripts = scripted_inputs_for(profile, 57, MATCH_END_WAITING_TICK);
    for (zero, one) in scripts[0].iter().zip(&scripts[1]) {
        game.step([*zero, *one]);
    }
    assert_eq!(game.snapshot().flow.unwrap().phase, FlowPhase::Waiting);
    assert_eq!(
        game.request_lifecycle(LifecycleRequest::BeginNewMatch),
        LifecycleResult::Accepted
    );
    step_until(&mut game, FlowPhase::Draft);
    confirm_active_offer(&mut game);
    step_until(&mut game, FlowPhase::Draft);
    confirm_active_offer(&mut game);
    step_until(&mut game, FlowPhase::ResumedCombat);
    assert!(walk_blue_out(&mut game).metrics.ring_outs > 0);
    step_until(&mut game, FlowPhase::TimberTransition);
    game.step([PlayerInput::default(); 2]);
    let timber = game.snapshot();
    assert_eq!(timber.flow.unwrap().phase, FlowPhase::TimberTransition);
    assert_eq!(timber.arena.len(), 1);
    assert!(!timber.constraints.is_empty());
}
