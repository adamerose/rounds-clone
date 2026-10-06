use quarrel_network::{ClientPresentation, PresentationSample};
use quarrel_sim::{
    AuthoritativeMatch, CombatTuning, FlowAction, FlowCommand, FlowPhase, PlayerInput,
};

#[test]
fn next_frame_predicted_shot_and_block_render_offscreen() {
    let mut game = AuthoritativeMatch::new(38);
    let flow = game.snapshot().flow.unwrap();
    let picks = (0..2)
        .map(|id| PlayerInput {
            flow: Some(FlowCommand {
                phase_revision: flow.phase_revision,
                action: FlowAction::Confirm(flow.offers[id][0]),
            }),
            ..Default::default()
        })
        .collect::<Vec<_>>();
    game.step(&picks);
    for _ in 0..120 {
        if game.snapshot().flow.unwrap().phase == FlowPhase::Combat {
            break;
        }
        game.step(&[PlayerInput::default(); 2]);
    }
    let state = game.snapshot();
    assert_eq!(state.flow.as_ref().unwrap().phase, FlowPhase::Combat);
    let sample = PresentationSample {
        state: state.clone(),
        tuning: CombatTuning::default(),
        input_ack: 0,
    };
    let mut presentation = ClientPresentation::new(0);
    let displayed = presentation
        .frame(
            &[sample],
            1,
            PlayerInput {
                move_axis: 1,
                jump: true,
                aim_x: 1000,
                fire: true,
                block: true,
                ..Default::default()
            },
            1. / 60.,
        )
        .unwrap();
    assert!(displayed.players[0].block_ticks > 0);
    assert!(displayed.projectiles.iter().any(|shot| shot.owner == 0));
    let output = std::path::Path::new("out/netcode-evidence");
    std::fs::create_dir_all(output).unwrap();
    let before = quarrel_presentation::render_png(&state, &output.join("before.png")).unwrap();
    let after = quarrel_presentation::render_png(&displayed, &output.join("after.png")).unwrap();
    assert_ne!(before, after);
}
