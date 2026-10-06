use quarrel_network::{ClientPresentation, PresentationSample};
use quarrel_sim::{
    AuthoritativeMatch, CombatTuning, FlowAction, FlowCommand, FlowPhase, PlayerInput,
};

fn combat_state() -> quarrel_sim::MatchSnapshot {
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
    state
}

#[test]
fn next_frame_predicted_shot_and_block_render_offscreen() {
    let state = combat_state();
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
    assert!(
        shot_pixels(&after) > shot_pixels(&before),
        "the predicted shot must reach rendered pixels"
    );
}

fn shot_pixels(bytes: &[u8]) -> usize {
    let mut reader = png::Decoder::new(std::io::Cursor::new(bytes))
        .read_info()
        .unwrap();
    let mut pixels = vec![0; reader.output_buffer_size().unwrap()];
    let frame = reader.next_frame(&mut pixels).unwrap();
    assert_eq!(frame.color_type, png::ColorType::Rgb);
    pixels[..frame.buffer_size()]
        .as_chunks::<3>()
        .0
        .iter()
        .filter(|rgb| rgb[0] > 150 && rgb[1] > 130 && rgb[2] < 130)
        .count()
}

#[test]
fn remote_shot_stops_at_the_wall_in_rendered_frames() {
    let mut before = combat_state();
    before.arena.clear();
    let arena = before.arena_objects.as_mut().unwrap();
    arena.chains.clear();
    arena.objects = vec![quarrel_sim::ArenaObject {
        id: 900,
        position: [40., 100.],
        rotation: 0.,
        shape: quarrel_sim::ArenaShape::Rectangle { size: [20., 300.] },
        kind: quarrel_sim::ArenaKind::Solid,
        color: [100, 100, 100],
        mass: 1.,
        health: None,
        motion: None,
    }];
    before.projectiles = vec![quarrel_sim::ProjectileSnapshot {
        id: 42,
        owner: 1,
        x_milli: 0,
        y_milli: 100000,
        previous_x_milli: -60000,
        previous_y_milli: 100000,
        velocity_x_milli_per_second: 3600000,
        velocity_y_milli_per_second: 0,
        lifetime_ticks: 60,
        dazzle_pulses: 0,
        explosive_radius_milli: 0,
    }];
    let mut after = before.clone();
    after.tick += 6;
    after.projectiles.clear();
    let samples = [
        PresentationSample {
            state: before.clone(),
            tuning: CombatTuning::default(),
            input_ack: 0,
        },
        PresentationSample {
            state: after,
            tuning: CombatTuning::default(),
            input_ack: 0,
        },
    ];
    let mut view = ClientPresentation::new(0);
    let mut displayed = before.clone();
    for sequence in 1..=12 {
        displayed = view
            .frame(&samples, sequence, PlayerInput::default(), 1. / 60.)
            .unwrap();
    }
    assert!(displayed.projectiles.is_empty());
    let output = std::path::Path::new("out/netcode-evidence");
    std::fs::create_dir_all(output).unwrap();
    let initial =
        quarrel_presentation::render_png(&before, &output.join("remote-before.png")).unwrap();
    let stopped =
        quarrel_presentation::render_png(&displayed, &output.join("remote-after.png")).unwrap();
    assert!(shot_pixels(&initial) > 0);
    assert_eq!(
        shot_pixels(&stopped),
        0,
        "the shot cannot be drawn beyond the wall"
    );
}
