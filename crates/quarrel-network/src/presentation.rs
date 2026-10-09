use quarrel_sim::{CombatTuning, FlowPhase, MatchSnapshot, PlayerInput, ScenePrediction};
use std::collections::VecDeque;

#[derive(Clone)]
pub struct PresentationSample {
    pub state: MatchSnapshot,
    pub tuning: CombatTuning,
    pub input_ack: u64,
}

/// A current predicted world, corrected from the latest host observation.
pub struct ClientPresentation {
    player: u8,
    pending: VecDeque<(u64, PlayerInput, f64)>,
    previous: PlayerInput,
    revision: Option<u32>,
    observed_tick: Option<u32>,
    last_scene: Option<MatchSnapshot>,
    correction: Correction,
    prediction: Option<ScenePrediction>,
}

impl ClientPresentation {
    pub fn new(player: u8) -> Self {
        Self {
            player,
            pending: VecDeque::new(),
            previous: PlayerInput::default(),
            revision: None,
            observed_tick: None,
            last_scene: None,
            correction: Correction::default(),
            prediction: None,
        }
    }

    /// Sample devices first so this frame contains newly pressed controls.
    pub fn frame(
        &mut self,
        samples: &[PresentationSample],
        sequence: u64,
        input: PlayerInput,
        seconds: f64,
    ) -> Option<MatchSnapshot> {
        let latest = samples.last()?;
        let seconds = seconds.clamp(0., 0.25);
        let flow = latest.state.flow.as_ref();
        let revision = flow.map(|flow| flow.phase_revision);
        let changed = revision != self.revision;
        if changed {
            self.pending.clear();
            self.previous = PlayerInput::default();
            self.observed_tick = None;
            self.correction = Correction::default();
            self.revision = revision;
        }
        while self
            .pending
            .front()
            .is_some_and(|(id, _, _)| *id <= latest.input_ack)
        {
            self.previous = self.pending.pop_front().unwrap().1;
        }
        let combat = flow.is_some_and(|flow| flow.phase == FlowPhase::Combat);
        let mut display;
        if combat {
            if self.observed_tick != Some(latest.state.tick) {
                if !changed && let Some(previous) = &self.last_scene {
                    // Compare at the previous render time: including the new
                    // input here would cancel this frame's jump or movement.
                    let target = predict(
                        &mut self.prediction,
                        &latest.state,
                        self.player,
                        &latest.tuning,
                        self.previous,
                        &self.pending,
                    );
                    self.correction = Correction::between(previous, &target);
                }
                self.observed_tick = Some(latest.state.tick);
            }
            let mut base = latest.state.clone();
            // Apply position corrections before physics: collision poses and
            // the visible world are the same scene, including moving supports.
            self.correction.apply(&mut base);
            if self.pending.len() == 256 {
                self.pending.pop_front();
            }
            self.pending.push_back((sequence, input, seconds));
            display = predict(
                &mut self.prediction,
                &base,
                self.player,
                &latest.tuning,
                self.previous,
                &self.pending,
            );
        } else {
            if changed || self.prediction.is_none() {
                prepare_prediction(
                    &mut self.prediction,
                    &latest.state,
                    self.player,
                    &latest.tuning,
                    self.previous,
                );
            }
            if changed
                && flow.is_some_and(|flow| {
                    matches!(flow.phase, FlowPhase::Result | FlowPhase::MatchEnd)
                })
                && let Some(previous) = &self.last_scene
            {
                self.correction = Correction::between(previous, &latest.state);
            }
            display = latest.state.clone();
            self.correction.apply(&mut display);
        }
        self.correction.weight *= (-15. * seconds).exp();
        self.last_scene = Some(display.clone());
        Some(display)
    }
}

fn blend(a: i32, b: i32, t: f64) -> i32 {
    (a as f64 + (b as f64 - a as f64) * t).round() as i32
}

fn prepare_prediction<'a>(
    cached: &'a mut Option<ScenePrediction>,
    base: &MatchSnapshot,
    player: u8,
    tuning: &CombatTuning,
    previous: PlayerInput,
) -> &'a mut ScenePrediction {
    if let Some(prediction) = cached {
        prediction.reset(base, player, tuning.clone(), previous);
    } else {
        *cached = Some(ScenePrediction::new(base, player, tuning.clone(), previous));
    }
    cached.as_mut().unwrap()
}

fn predict(
    cached: &mut Option<ScenePrediction>,
    base: &MatchSnapshot,
    player: u8,
    tuning: &CombatTuning,
    previous: PlayerInput,
    pending: &VecDeque<(u64, PlayerInput, f64)>,
) -> MatchSnapshot {
    if pending.is_empty() {
        return base.clone();
    }
    let prediction = prepare_prediction(cached, base, player, tuning, previous);
    let mut budget = 0.;
    let mut partial = PlayerInput::default();
    for (_, held, dt) in pending {
        partial = PlayerInput {
            jump: partial.jump || held.jump,
            block: partial.block || held.block,
            fire: partial.fire || held.fire,
            ..*held
        };
        budget += dt * 60.;
        while budget >= 1. - 1e-9 {
            prediction.step(partial);
            budget = (budget - 1.).max(0.);
            partial = *held;
        }
    }
    let mut display = base.clone();
    prediction.apply(&mut display);
    if budget > 1e-9 {
        let floor = display.clone();
        prediction.step(partial);
        prediction.apply(&mut display);
        for (shown, before) in display.players.iter_mut().zip(&floor.players) {
            shown.x_milli = blend(before.x_milli, shown.x_milli, budget);
            shown.y_milli = blend(before.y_milli, shown.y_milli, budget);
        }
        if let (Some(shown), Some(before)) = (&mut display.arena_objects, &floor.arena_objects) {
            for object in &mut shown.objects {
                if let Some(old) = before.objects.iter().find(|old| old.id == object.id) {
                    for (position, old) in object.position.iter_mut().zip(old.position) {
                        *position = old + (*position - old) * budget as f32;
                    }
                    object.rotation =
                        old.rotation + angle_delta(old.rotation, object.rotation) * budget as f32;
                }
            }
        }
        for shot in &mut display.projectiles {
            let old = floor.projectiles.iter().find(|old| old.id == shot.id);
            shot.x_milli = blend(
                old.map_or(shot.previous_x_milli, |old| old.x_milli),
                shot.x_milli,
                budget,
            );
            shot.y_milli = blend(
                old.map_or(shot.previous_y_milli, |old| old.y_milli),
                shot.y_milli,
                budget,
            );
        }
    }
    display
}

fn angle_delta(a: f32, b: f32) -> f32 {
    (b - a + std::f32::consts::PI).rem_euclid(std::f32::consts::TAU) - std::f32::consts::PI
}

#[derive(Default)]
struct Correction {
    players: Vec<(u8, [i32; 2])>,
    pieces: Vec<(u16, [f32; 2], f32)>,
    shots: Vec<(u32, [i32; 2])>,
    weight: f64,
}

impl Correction {
    fn between(previous: &MatchSnapshot, target: &MatchSnapshot) -> Self {
        let players = target
            .players
            .iter()
            .filter_map(|p| {
                previous
                    .players
                    .iter()
                    .find(|old| old.id == p.id)
                    .map(|old| (p.id, [old.x_milli - p.x_milli, old.y_milli - p.y_milli]))
            })
            .collect();
        let shots = target
            .projectiles
            .iter()
            .filter_map(|p| {
                previous
                    .projectiles
                    .iter()
                    .find(|old| old.id == p.id)
                    .map(|old| (p.id, [old.x_milli - p.x_milli, old.y_milli - p.y_milli]))
            })
            .collect();
        let mut pieces = Vec::new();
        if let (Some(previous), Some(target)) = (&previous.arena_objects, &target.arena_objects) {
            for p in &target.objects {
                if let Some(old) = previous.objects.iter().find(|old| old.id == p.id) {
                    pieces.push((
                        p.id,
                        [
                            old.position[0] - p.position[0],
                            old.position[1] - p.position[1],
                        ],
                        angle_delta(p.rotation, old.rotation),
                    ));
                }
            }
        }
        Self {
            players,
            pieces,
            shots,
            weight: 1.,
        }
    }

    fn apply(&self, state: &mut MatchSnapshot) {
        for p in &mut state.players {
            if let Some((_, offset)) = self.players.iter().find(|(id, _)| *id == p.id) {
                p.x_milli += (offset[0] as f64 * self.weight).round() as i32;
                p.y_milli += (offset[1] as f64 * self.weight).round() as i32;
            }
        }
        for p in &mut state.projectiles {
            if let Some((_, offset)) = self.shots.iter().find(|(id, _)| *id == p.id) {
                let x = (offset[0] as f64 * self.weight).round() as i32;
                let y = (offset[1] as f64 * self.weight).round() as i32;
                p.x_milli += x;
                p.previous_x_milli += x;
                p.y_milli += y;
                p.previous_y_milli += y;
            }
        }
        if let Some(arena) = &mut state.arena_objects {
            for object in &mut arena.objects {
                if let Some((_, offset, angle)) =
                    self.pieces.iter().find(|(id, _, _)| *id == object.id)
                {
                    for (position, offset) in object.position.iter_mut().zip(offset) {
                        *position += *offset * self.weight as f32;
                    }
                    object.rotation += angle * self.weight as f32;
                }
            }
        }
    }
}
#[cfg(test)]
mod tests {
    use super::*;
    use quarrel_sim::{AuthoritativeMatch, FlowAction, FlowCommand, MatchConfig};
    fn sample(state: MatchSnapshot) -> PresentationSample {
        PresentationSample {
            state,
            tuning: CombatTuning::default(),
            input_ack: 0,
        }
    }

    #[test]
    fn current_world_advances_every_entity_without_changing_bullet_speed() {
        for rate in [60, 240] {
            let mut state = combat();
            state.arena.clear();
            let arena = state.arena_objects.as_mut().unwrap();
            arena.objects.clear();
            arena.chains.clear();
            arena.frame = [-2000., -2000., 2000., 2000.];
            arena.objects.push(quarrel_sim::ArenaObject {
                id: 42,
                position: [0., 400.],
                rotation: 0.,
                shape: quarrel_sim::ArenaShape::Rectangle { size: [20., 20.] },
                kind: quarrel_sim::ArenaKind::Loose,
                color: [100; 3],
                mass: 1.,
                health: None,
                motion: None,
            });
            arena.velocities = vec![quarrel_sim::ArenaObjectVelocity {
                id: 42,
                linear: [600000, 0],
                angular: 0,
            }];
            for (id, p) in state.players.iter_mut().enumerate() {
                p.x_milli = if id == 0 { -500000 } else { 0 };
                p.y_milli = 700000;
                p.velocity_x_milli_per_second = if id == 0 { 0 } else { 600000 };
                p.velocity_y_milli_per_second = 0;
            }
            state.projectiles = vec![quarrel_sim::ProjectileSnapshot {
                id: 77,
                owner: 1,
                x_milli: -500000,
                y_milli: 100000,
                previous_x_milli: -560000,
                previous_y_milli: 100000,
                velocity_x_milli_per_second: 3600000,
                velocity_y_milli_per_second: 0,
                lifetime_ticks: 60,
                dazzle_pulses: 0,
                explosive_radius_milli: 0,
                radius_milli: 5000,
                damage: 0,
            }];
            let shown = ClientPresentation::new(0)
                .frame(
                    &[sample(state.clone())],
                    1,
                    PlayerInput::default(),
                    1. / rate as f64,
                )
                .unwrap();
            let seconds = 1. / rate as f64;
            assert!((shown.players[1].x_milli as f64 - 600000. * seconds).abs() < 200.);
            let piece = &shown.arena_objects.as_ref().unwrap().objects[0];
            assert!((piece.position[0] as f64 - 600. * seconds).abs() < 0.2);
            assert!(
                (shown.projectiles[0].x_milli as f64 + 500000. - 3600000. * seconds).abs() < 100.
            );
            assert_eq!(shown.players[0].health, state.players[0].health);
            assert_eq!(shown.flow, state.flow);
        }
    }

    #[test]
    fn a_new_host_sample_does_not_cancel_this_frames_input() {
        let state = combat();
        let mut view = ClientPresentation::new(0);
        view.frame(
            &[sample(state.clone())],
            1,
            PlayerInput::default(),
            1. / 60.,
        )
        .unwrap();
        let mut latest = sample(state);
        latest.state.tick += 6;
        latest.input_ack = 1;
        let input = PlayerInput {
            move_axis: -1,
            jump: true,
            fire: true,
            block: true,
            aim_x: 1000,
            ..Default::default()
        };
        let shown = view.frame(&[latest.clone()], 2, input, 1. / 60.).unwrap();
        assert!(shown.players[0].block_ticks > 0);
        assert!(shown.projectiles.iter().any(|p| p.owner == 0));
        assert!(shown.players[0].velocity_x_milli_per_second < 0);
        assert_eq!(shown.flow, latest.state.flow);
    }

    #[test]
    fn shipped_outline_world_frame_cpu_is_measured() {
        let arena = quarrel_sim::MatchContent::load_default()
            .unwrap()
            .arenas
            .into_iter()
            .max_by_key(|a| {
                a.surfaces
                    .iter()
                    .filter(|p| !p.outline_milli.is_empty())
                    .count()
            })
            .unwrap();
        let mut game = AuthoritativeMatch::from_arena(38, arena).unwrap();
        let mut state = game.snapshot();
        assert!(
            state
                .arena
                .iter()
                .filter(|p| !p.outline_milli.is_empty())
                .count()
                >= 18
        );
        let mut view = ClientPresentation::new(0);
        // Exercise the real Draft -> Combat route, including first-frame cost.
        let preparation = std::time::Instant::now();
        view.frame(
            &[sample(state.clone())],
            0,
            PlayerInput::default(),
            1. / 60.,
        )
        .unwrap();
        let preparation_ms = preparation.elapsed().as_secs_f64() * 1000.;
        let flow = state.flow.as_ref().unwrap();
        game.step(
            &(0..2)
                .map(|id| PlayerInput {
                    flow: Some(FlowCommand {
                        phase_revision: flow.phase_revision,
                        action: FlowAction::Confirm(flow.offers[id][0]),
                    }),
                    ..Default::default()
                })
                .collect::<Vec<_>>(),
        );
        let old = state.arena.clone();
        state = game.snapshot();
        assert_eq!(state.flow.as_ref().unwrap().phase, FlowPhase::Combat);
        assert_eq!(old, state.arena);
        let mut timings = Vec::new();
        for sequence in 1..=120_u64 {
            if sequence % 6 == 0 {
                state.tick += 6;
            }
            let start = std::time::Instant::now();
            let mut observation = sample(state.clone());
            observation.input_ack = sequence.saturating_sub(6);
            view.frame(&[observation], sequence, PlayerInput::default(), 1. / 60.)
                .unwrap();
            timings.push(start.elapsed().as_secs_f64() * 1000.);
        }
        let first_ms = timings[0];
        timings.sort_by(f64::total_cmp);
        println!(
            "real Draft preparation CPU={preparation_ms:.3}ms; first Combat frame CPU={first_ms:.3}ms"
        );
        println!(
            "shipped world: {} outlined surfaces; predictor CPU p50={:.3}ms p95={:.3}ms max={:.3}ms",
            state
                .arena
                .iter()
                .filter(|p| !p.outline_milli.is_empty())
                .count(),
            timings[60],
            timings[114],
            timings[119]
        );
    }
    fn combat() -> MatchSnapshot {
        let mut game = AuthoritativeMatch::with_config(MatchConfig::default()).unwrap();
        let state = game.snapshot();
        let flow = state.flow.unwrap();
        game.step(
            &(0..2)
                .map(|id| PlayerInput {
                    flow: Some(FlowCommand {
                        phase_revision: flow.phase_revision,
                        action: FlowAction::Confirm(flow.offers[id][0]),
                    }),
                    ..Default::default()
                })
                .collect::<Vec<_>>(),
        );
        for _ in 0..120 {
            if game.snapshot().flow.unwrap().phase == FlowPhase::Combat {
                return game.snapshot();
            }
            game.step(&[PlayerInput::default(); 2]);
        }
        panic!("opening draft did not enter combat")
    }
    #[test]
    fn result_keeps_the_world_clock_until_the_frozen_pose_is_reached() {
        let mut first = combat();
        first.players[1].x_milli = 0;
        let mut second = first.clone();
        second.tick += 6;
        second.players[1].x_milli = 22000;
        let mut samples = vec![sample(first), sample(second.clone())];
        let mut view = ClientPresentation::new(0);
        let mut previous = second.clone();
        for sequence in 1..=6 {
            previous = view
                .frame(&samples, sequence, PlayerInput::default(), 1. / 60.)
                .unwrap();
        }
        let mut result = second;
        result.tick += 6;
        result.players[1].x_milli = 44000;
        let flow = result.flow.as_mut().unwrap();
        flow.phase = FlowPhase::Result;
        flow.phase_revision += 1;
        samples.push(sample(result.clone()));
        for sequence in 7..=60 {
            let displayed = view
                .frame(&samples, sequence, PlayerInput::default(), 1. / 60.)
                .unwrap();
            assert!(
                (displayed.players[1].x_milli - previous.players[1].x_milli).abs() < 5000,
                "Result must settle from the current predicted world"
            );
            assert_eq!(displayed.flow, result.flow);
            previous = displayed;
        }
        assert_eq!(previous.players[1].x_milli, 44000);
    }
    #[test]
    fn incoming_shots_and_damage_share_the_current_authority_timeline() {
        let mut older = combat();
        older.projectiles = vec![quarrel_sim::ProjectileSnapshot {
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
            radius_milli: 5000,
            damage: 0,
        }];
        let mut latest = older.clone();
        latest.tick += 12;
        latest.players[0].health -= 60;
        latest.players[0].hit_flash_ticks = 6;
        latest.projectiles.clear();
        let shown = ClientPresentation::new(0)
            .frame(
                &[sample(older), sample(latest.clone())],
                1,
                PlayerInput::default(),
                1. / 60.,
            )
            .unwrap();
        assert_eq!(shown.players[0].health, latest.players[0].health);
        assert!(
            shown.projectiles.is_empty(),
            "a confirmed hit cannot retain its incoming shot in the past"
        );
    }
    #[test]
    fn immediate_preview_reconciles_acknowledged_inputs_and_phase_changes() {
        let state = combat();
        let mut view = ClientPresentation::new(0);
        let input = PlayerInput {
            move_axis: -1,
            jump: true,
            aim_x: 1000,
            aim_y: 0,
            fire: true,
            block: true,
            ..Default::default()
        };
        let mut sample = PresentationSample {
            state: state.clone(),
            input_ack: 0,
            tuning: CombatTuning::default(),
        };
        let shown = view.frame(&[sample.clone()], 1, input, 1. / 240.).unwrap();
        assert!(shown.players[0].block_ticks > 0);
        assert!(shown.projectiles.iter().any(|shot| shot.owner == 0));
        assert_eq!(shown.flow, state.flow);
        assert_eq!(shown.players[0].health, state.players[0].health);
        let released = view
            .frame(&[sample.clone()], 2, PlayerInput::default(), 1. / 240.)
            .unwrap();
        assert!(
            released.players[0].block_ticks > 0,
            "a sub-tick tap remains active through release"
        );
        sample.input_ack = 1;
        sample.state.tick += 6;
        sample.state.players[0].alive = false;
        sample.state.players[0].health = 0;
        let corrected = view
            .frame(&[sample.clone()], 2, PlayerInput::default(), 1. / 60.)
            .unwrap();
        assert!(!corrected.players[0].alive);
        assert!(corrected.projectiles.iter().all(|shot| shot.owner != 0));
        sample.state.flow.as_mut().unwrap().phase = FlowPhase::Draft;
        sample.state.flow.as_mut().unwrap().phase_revision += 1;
        assert_eq!(
            view.frame(&[sample.clone()], 3, input, 1. / 60.).unwrap(),
            sample.state
        );
    }
}
