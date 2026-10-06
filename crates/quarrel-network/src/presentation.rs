use quarrel_sim::{CombatTuning, FlowPhase, LocalPrediction, MatchSnapshot, PlayerInput};
use std::collections::VecDeque;

#[derive(Clone)]
pub struct PresentationSample {
    pub state: MatchSnapshot,
    pub tuning: CombatTuning,
    pub input_ack: u64,
}

/// Client display state; authoritative observations and hashes remain separate.
pub struct ClientPresentation {
    player: u8,
    pending: VecDeque<(u64, PlayerInput, f64)>,
    previous: PlayerInput,
    remote_tick: Option<f64>,
    observed_tick: u32,
    revision: Option<u32>,
}

impl ClientPresentation {
    pub fn new(player: u8) -> Self {
        Self {
            player,
            pending: VecDeque::new(),
            previous: PlayerInput::default(),
            remote_tick: None,
            observed_tick: 0,
            revision: None,
        }
    }

    /// Called after sampling devices and before constructing this frame's scene.
    pub fn frame(
        &mut self,
        samples: &[PresentationSample],
        sequence: u64,
        input: PlayerInput,
        seconds: f64,
    ) -> Option<MatchSnapshot> {
        let latest = samples.last()?;
        let revision = latest.state.flow.as_ref().map(|flow| flow.phase_revision);
        if revision != self.revision {
            self.pending.clear();
            self.previous = PlayerInput::default();
            self.remote_tick = None;
            self.revision = revision;
        }
        while self
            .pending
            .front()
            .is_some_and(|(id, _, _)| *id <= latest.input_ack)
        {
            self.previous = self.pending.pop_front().unwrap().1;
        }
        // Four seconds is beyond the transport's peer-silence window. Bound the
        // input history even if a caller keeps drawing after disconnection.
        if self.pending.len() == 256 {
            self.pending.pop_front();
        }
        self.pending
            .push_back((sequence, input, seconds.clamp(0., 0.25)));
        let mut display = latest.state.clone();
        if !display
            .flow
            .as_ref()
            .is_some_and(|flow| flow.phase == FlowPhase::Combat)
        {
            return Some(display);
        }
        let clock = self
            .remote_tick
            .get_or_insert(latest.state.tick as f64 - 12.);
        *clock += seconds.clamp(0., 0.25) * 60.;
        if self.observed_tick != latest.state.tick {
            *clock += (latest.state.tick as f64 - 12. - *clock) * 0.1;
            self.observed_tick = latest.state.tick;
        }
        let compatible = samples
            .iter()
            .filter(|sample| sample.state.flow.as_ref().map(|flow| flow.phase_revision) == revision)
            .collect::<Vec<_>>();
        let before = compatible
            .iter()
            .rev()
            .find(|sample| sample.state.tick as f64 <= *clock)
            .copied()
            .unwrap_or(compatible[0]);
        let after = compatible
            .iter()
            .find(|sample| sample.state.tick as f64 >= *clock)
            .copied()
            .unwrap_or(latest);
        let span = after.state.tick.saturating_sub(before.state.tick);
        let fraction = if span == 0 {
            0.
        } else {
            ((*clock - before.state.tick as f64) / span as f64).clamp(0., 1.)
        };
        interpolate(
            &mut display,
            &before.state,
            &after.state,
            fraction,
            self.player,
        );
        let mut prediction = LocalPrediction::new(
            &latest.state,
            self.player,
            latest.tuning.clone(),
            self.previous,
        );
        let mut budget = 0.;
        let mut partial = PlayerInput::default();
        for (_, held, dt) in &self.pending {
            partial = PlayerInput {
                jump: partial.jump || held.jump,
                block: partial.block || held.block,
                fire: partial.fire || held.fire,
                ..*held
            };
            budget += dt * 60.;
            while budget >= 1. {
                prediction.step(partial);
                budget -= 1.;
                partial = *held;
            }
        }
        // A fractional render frame previews the next physics tick, so a press
        // also appears immediately on displays faster than the simulation.
        if budget > 0. || self.pending.len() == 1 {
            prediction.step(partial);
        }
        prediction.apply(&mut display);
        Some(display)
    }
}

fn blend(a: i32, b: i32, t: f64) -> i32 {
    (a as f64 + (b as f64 - a as f64) * t).round() as i32
}

fn interpolate(
    display: &mut MatchSnapshot,
    before: &MatchSnapshot,
    after: &MatchSnapshot,
    t: f64,
    owned: u8,
) {
    for player in &mut display.players {
        if player.id == owned {
            continue;
        }
        if let Some(a) = before.players.iter().find(|p| p.id == player.id)
            && let Some(b) = after.players.iter().find(|p| p.id == player.id)
        {
            player.x_milli = blend(a.x_milli, b.x_milli, t);
            player.y_milli = blend(a.y_milli, b.y_milli, t);
        }
    }
    // Entity membership is authoritative; interpolate only matching identities.
    display.projectiles.retain(|shot| shot.owner == owned);
    display.projectiles.extend(
        before
            .projectiles
            .iter()
            .filter(|shot| shot.owner != owned)
            .cloned(),
    );
    for shot in &mut display.projectiles {
        if shot.owner == owned {
            continue;
        }
        if let Some(a) = before.projectiles.iter().find(|p| p.id == shot.id)
            && let Some(b) = after.projectiles.iter().find(|p| p.id == shot.id)
        {
            shot.x_milli = blend(a.x_milli, b.x_milli, t);
            shot.y_milli = blend(a.y_milli, b.y_milli, t);
            shot.previous_x_milli = blend(a.previous_x_milli, b.previous_x_milli, t);
            shot.previous_y_milli = blend(a.previous_y_milli, b.previous_y_milli, t);
        }
    }
    if let Some(arena) = &mut display.arena_objects
        && let Some(a) = &before.arena_objects
        && let Some(b) = &after.arena_objects
    {
        for object in &mut arena.objects {
            if let Some(a) = a.objects.iter().find(|piece| piece.id == object.id)
                && let Some(b) = b.objects.iter().find(|piece| piece.id == object.id)
            {
                for axis in 0..2 {
                    object.position[axis] =
                        a.position[axis] + (b.position[axis] - a.position[axis]) * t as f32;
                }
                let angle = (b.rotation - a.rotation + std::f32::consts::PI)
                    .rem_euclid(std::f32::consts::TAU)
                    - std::f32::consts::PI;
                object.rotation = a.rotation + angle * t as f32;
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use quarrel_sim::{AuthoritativeMatch, FlowAction, FlowCommand, MatchConfig};

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

    #[test]
    fn remote_interpolation_blends_identity_and_shortest_rotation() {
        let mut before = combat();
        before.players[1].x_milli = 0;
        let mut after = before.clone();
        after.tick += 6;
        after.players[1].x_milli = 6000;
        let a = &mut before.arena_objects.as_mut().unwrap().objects[0];
        a.rotation = 3.1;
        a.position = [0., 0.];
        let b = &mut after.arena_objects.as_mut().unwrap().objects[0];
        b.rotation = -3.1;
        b.position = [6., 0.];
        let mut display = after.clone();
        interpolate(&mut display, &before, &after, 0.5, 0);
        assert_eq!(display.players[1].x_milli, 3000);
        let piece = &display.arena_objects.as_ref().unwrap().objects[0];
        assert!((piece.rotation - std::f32::consts::PI).abs() < 0.001);
        assert_eq!(piece.position, [3., 0.]);
        assert_eq!(display.flow, after.flow);
    }
}
