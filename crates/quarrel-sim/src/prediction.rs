use super::*;

/// One presentation-only world for fighters, shots and pieces. It cannot award
/// damage, deaths, points or cards. A new authority sample replaces its base.
pub struct ScenePrediction {
    physics: physics::PhysicsBoundary,
    player: PlayerSnapshot,
    tuning: CombatTuning,
    capabilities: FighterCapabilities,
    frame: [f32; 4],
    jump_held: bool,
    block_held: bool,
    shots: Vec<ProjectileSnapshot>,
    next_shot: u32,
    targets: Vec<u8>,
    others: Vec<PlayerSnapshot>,
    arena: ArenaDefinition,
    tick: u32,
}

impl ScenePrediction {
    pub fn new(state: &MatchSnapshot, id: u8, tuning: CombatTuning, previous: PlayerInput) -> Self {
        Self::build(state, id, tuning, previous, BTreeMap::new())
    }

    /// Rebuild runtime bodies while retaining prepared polygon shapes.
    pub fn reset(
        &mut self,
        state: &MatchSnapshot,
        id: u8,
        tuning: CombatTuning,
        previous: PlayerInput,
    ) {
        let cache = self.physics.take_surface_colliders();
        *self = Self::build(state, id, tuning, previous, cache);
    }

    fn build(
        state: &MatchSnapshot,
        id: u8,
        tuning: CombatTuning,
        previous: PlayerInput,
        mut cache: BTreeMap<Vec<[i32; 2]>, ColliderBuilder>,
    ) -> Self {
        // Only this arena's outlines are retained; authored reloads cannot grow
        // the presentation cache indefinitely.
        cache.retain(|outline, _| state.arena.iter().any(|s| &s.outline_milli == outline));
        let rendered = state.arena_objects.as_ref();
        let frame = rendered.map_or([-1000., -1000., 1000., 1000.], |arena| arena.frame);
        let mut arena = ArenaDefinition {
            name: "prediction".into(),
            frame,
            spawns: rendered.map_or_else(Vec::new, |arena| arena.spawns.clone()),
            objects: rendered.map_or_else(Vec::new, |arena| arena.objects.clone()),
            chains: rendered.map_or_else(Vec::new, |arena| arena.chains.clone()),
            surfaces: state.arena.clone(),
            legacy_bodies: Vec::new(),
            legacy_saws: Vec::new(),
        };
        let mut physics =
            physics::PhysicsBoundary::new(state.players.len(), &arena, tuning.clone());
        physics.restore_surface_colliders(cache);
        physics.replace_arena(&arena);
        physics.prediction_poses(&state.players);
        if let Some(rendered) = rendered {
            physics.prediction_objects(rendered);
        }
        // Establish support normals, then restore the authoritative poses.
        physics.step();
        physics.prediction_poses(&state.players);
        if let Some(rendered) = rendered {
            physics.prediction_objects(rendered);
        }
        // Snapshots carry current poses. Motion paths use their authored base,
        // recovered with the same formula used by the host.
        for object in &mut arena.objects {
            if let Some(motion) = &object.motion {
                let (offset, rotation) = motion.offset(state.tick);
                for (position, offset) in object.position.iter_mut().zip(offset) {
                    *position -= offset;
                }
                object.rotation -= rotation;
            }
        }
        let shots = state.projectiles.clone();
        for shot in &shots {
            physics.spawn_bullet(shot.id, shot.owner, Vector::X, tuning.bullet_speed);
            physics.prediction_bullet(shot);
        }
        Self {
            physics,
            player: state.players[id as usize].clone(),
            tuning,
            frame,
            capabilities: state
                .flow
                .as_ref()
                .map_or_default(|flow| flow.capabilities[id as usize]),
            jump_held: previous.jump,
            block_held: previous.block,
            shots,
            next_shot: u32::MAX,
            targets: state
                .players
                .iter()
                .filter(|player| player.alive)
                .map(|player| player.id)
                .collect(),
            others: state
                .players
                .iter()
                .filter(|player| player.id != id)
                .cloned()
                .collect(),
            arena,
            tick: state.tick,
        }
    }

    pub fn step(&mut self, input: PlayerInput) {
        let input = input.validated();
        let p = &mut self.player;
        p.fire_cooldown_ticks = p.fire_cooldown_ticks.saturating_sub(1);
        p.block_ticks = p.block_ticks.saturating_sub(1);
        p.block_cooldown_ticks = p.block_cooldown_ticks.saturating_sub(1);
        p.stun_ticks = p.stun_ticks.saturating_sub(1);
        if p.reload_ticks > 0 {
            p.reload_ticks -= 1;
            if p.reload_ticks == 0 {
                p.ammunition = self.tuning.magazine_size;
            }
        }
        if p.alive {
            let mut aim = Vector::new(p.aim_x as f32, p.aim_y as f32).normalize_or_zero();
            if input.aim_x != 0 || input.aim_y != 0 {
                aim = Vector::new(input.aim_x as f32, input.aim_y as f32).normalize_or_zero();
                p.aim_x = quantize(aim.x) as i16;
                p.aim_y = quantize(aim.y) as i16;
            }
            if input.block && !self.block_held && p.block_cooldown_ticks == 0 {
                p.block_ticks = p.block_ticks.max(self.tuning.block_duration_ticks);
                p.block_cooldown_ticks = self.tuning.block_cooldown_ticks;
            }
            if p.stun_ticks == 0
                && self.physics.set_player_control(
                    p.id,
                    input,
                    p.jump_available,
                    input.jump && !self.jump_held,
                    self.jump_held && !input.jump,
                    self.capabilities.movement_bonus,
                )
            {
                p.jump_available = false;
            }
            self.jump_held = input.jump;
            self.block_held = input.block;
            if p.stun_ticks == 0
                && input.fire
                && p.fire_cooldown_ticks == 0
                && p.reload_ticks == 0
                && p.ammunition > 0
            {
                p.fire_cooldown_ticks = self
                    .tuning
                    .fire_cooldown_ticks
                    .saturating_add(self.capabilities.fire_cooldown_extra_ticks);
                p.ammunition -= 1;
                if p.ammunition == 0 {
                    p.reload_ticks = self.tuning.reload_ticks;
                }
                let id = self.next_shot;
                self.next_shot -= 1;
                self.physics.spawn_bullet(
                    id,
                    p.id,
                    aim,
                    self.tuning.bullet_speed
                        * self.capabilities.projectile_speed_factor.milli as f32
                        / 1000.,
                );
                self.physics.recoil(p.id, aim);
                self.shots.push(ProjectileSnapshot {
                    id,
                    owner: p.id,
                    x_milli: p.x_milli,
                    y_milli: p.y_milli,
                    previous_x_milli: p.x_milli,
                    previous_y_milli: p.y_milli,
                    velocity_x_milli_per_second: 0,
                    velocity_y_milli_per_second: 0,
                    lifetime_ticks: self.tuning.bullet_lifetime_ticks,
                    dazzle_pulses: self.capabilities.dazzle_stun_pulses,
                    explosive_radius_milli: self.capabilities.explosion_radius_milli,
                });
            }
        }
        self.tick += 1;
        self.physics.update_arena_motion(&self.arena, self.tick);
        self.physics.step();
        self.physics
            .return_from_edge(p.id, self.frame, p.block_ticks > 0);
        let (position, velocity) = self.physics.player_pose(p.id);
        p.x_milli = quantize(position.x);
        p.y_milli = quantize(position.y);
        p.velocity_x_milli_per_second = quantize(velocity.x);
        p.velocity_y_milli_per_second = quantize(velocity.y);
        p.height_milli = quantize(self.physics.player_half_height(p.id) * 2.);
        let (grounded, wall) = self.physics.player_support(p.id);
        p.grounded = grounded;
        if grounded || wall != 0. {
            p.jump_available = true;
        }
        for player in &mut self.others {
            self.physics
                .return_from_edge(player.id, self.frame, player.block_ticks > 0);
            let (position, velocity) = self.physics.player_pose(player.id);
            player.x_milli = quantize(position.x);
            player.y_milli = quantize(position.y);
            player.velocity_x_milli_per_second = quantize(velocity.x);
            player.velocity_y_milli_per_second = quantize(velocity.y);
            player.grounded = self.physics.player_support(player.id).0;
            player.block_ticks = player.block_ticks.saturating_sub(1);
            player.stun_ticks = player.stun_ticks.saturating_sub(1);
            player.hit_flash_ticks = player.hit_flash_ticks.saturating_sub(1);
        }
        self.shots.retain_mut(|shot| {
            if self.physics.bullet_platform_contact(shot.id)
                || self
                    .targets
                    .iter()
                    .filter(|target| **target != shot.owner)
                    .any(|target| self.physics.prediction_fighter_contact(shot.id, *target))
            {
                self.physics.remove_bullet(shot.id);
                return false;
            }
            let Some((position, previous, velocity, lifetime)) = self.physics.bullet_pose(shot.id)
            else {
                return false;
            };
            if lifetime == 0 {
                self.physics.remove_bullet(shot.id);
                return false;
            }
            shot.x_milli = quantize(position.x);
            shot.y_milli = quantize(position.y);
            shot.previous_x_milli = quantize(previous.x);
            shot.previous_y_milli = quantize(previous.y);
            shot.velocity_x_milli_per_second = quantize(velocity.x);
            shot.velocity_y_milli_per_second = quantize(velocity.y);
            shot.lifetime_ticks = lifetime;
            true
        });
    }

    pub fn apply(&self, state: &mut MatchSnapshot) {
        state.tick = self.tick;
        state.players[self.player.id as usize] = self.player.clone();
        for player in &self.others {
            state.players[player.id as usize] = player.clone();
        }
        state.projectiles = self.shots.clone();
        if let Some(arena) = &mut state.arena_objects {
            for object in &mut arena.objects {
                if let Some((position, _, rotation, _)) = self.physics.object_pose(object.id) {
                    object.position = position.to_array();
                    object.rotation = rotation;
                }
            }
            arena.velocities = arena
                .objects
                .iter()
                .filter_map(|object| self.physics.object_velocity(object.id))
                .collect();
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    #[test]
    fn moving_support_uses_the_same_timeline_as_the_owned_fighter() {
        let arena = ArenaDefinition {
            name: "moving support".into(),
            frame: [-1000., -1000., 1000., 1000.],
            spawns: vec![[-300., 150.], [300., 150.]],
            chains: vec![],
            surfaces: vec![],
            legacy_bodies: vec![],
            legacy_saws: vec![],
            objects: vec![ArenaObject {
                id: 1,
                position: [0., 0.],
                rotation: 0.,
                shape: ArenaShape::Rectangle { size: [900., 20.] },
                kind: ArenaKind::Solid,
                color: [100, 100, 100],
                mass: 1.,
                health: None,
                motion: Some(ArenaMotion {
                    path: vec![[0., 0.], [0., 300.]],
                    period_seconds: 8.,
                    angular_velocity: 0.,
                }),
            }],
        };
        let mut game = AuthoritativeMatch::from_arena(38, arena).unwrap();
        let flow = game.snapshot().flow.unwrap();
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
        for _ in 0..60 {
            game.step(&[PlayerInput::default(); 2]);
        }
        let state = game.snapshot();
        assert!(state.players[0].grounded);
        let mut prediction =
            ScenePrediction::new(&state, 0, game.tuning().clone(), PlayerInput::default());
        for _ in 0..6 {
            prediction.step(PlayerInput::default());
            game.step(&[PlayerInput::default(); 2]);
        }
        let mut shown = state;
        prediction.apply(&mut shown);
        let actual = game.snapshot();
        let predicted_platform = &shown.arena_objects.as_ref().unwrap().objects[0];
        let authoritative_platform = &actual.arena_objects.as_ref().unwrap().objects[0];
        assert!(
            (predicted_platform.position[1] - authoritative_platform.position[1]).abs() < 0.01,
            "platform must advance with the predicted support"
        );
        assert!(
            (shown.players[0].y_milli - actual.players[0].y_milli).abs() < 2000,
            "fighter must ride the moving proxy like authority"
        );
    }
    #[test]
    fn predicted_shot_stops_at_opponent_without_inventing_a_reflection() {
        let mut state = AuthoritativeMatch::new(38).snapshot();
        state.arena.clear();
        let arena = state.arena_objects.as_mut().unwrap();
        arena.objects.clear();
        arena.chains.clear();
        arena.frame = [-1000., -1000., 1000., 1000.];
        for (id, player) in state.players.iter_mut().enumerate() {
            player.x_milli = if id == 0 { -300000 } else { 0 };
            player.y_milli = 100000;
            player.velocity_x_milli_per_second = 0;
            player.velocity_y_milli_per_second = 0;
        }
        state.projectiles = vec![ProjectileSnapshot {
            id: 42,
            owner: 0,
            x_milli: -100000,
            y_milli: 100000,
            previous_x_milli: -100000,
            previous_y_milli: 100000,
            velocity_x_milli_per_second: 3600000,
            velocity_y_milli_per_second: 0,
            lifetime_ticks: 90,
            dazzle_pulses: 0,
            explosive_radius_milli: 0,
        }];
        let mut prediction =
            ScenePrediction::new(&state, 0, CombatTuning::default(), PlayerInput::default());
        for _ in 0..8 {
            prediction.step(PlayerInput::default());
        }
        prediction.apply(&mut state);
        assert!(
            state.projectiles.is_empty(),
            "fighter contact cannot display an invented bounce"
        );
        assert_eq!(state.players[1].health, 100);
    }

    #[test]
    fn authoritative_ground_support_survives_prediction_pose_restore() {
        let mut game = AuthoritativeMatch::new(38);
        let flow = game.snapshot().flow.unwrap();
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
        for _ in 0..180 {
            game.step(&[PlayerInput::default(); 2]);
        }
        let state = game.snapshot();
        assert!(state.players[0].grounded);
        let mut prediction =
            ScenePrediction::new(&state, 0, game.tuning().clone(), PlayerInput::default());
        assert!(
            prediction.physics.player_support(0).0,
            "pose restoration must preserve warmed support contacts"
        );
        let input = PlayerInput {
            move_axis: 1,
            ..Default::default()
        };
        prediction.step(input);
        let mut shown = state.clone();
        prediction.apply(&mut shown);
        game.step(&[input, PlayerInput::default()]);
        let actual = game.snapshot();
        assert!(
            (shown.players[0].velocity_x_milli_per_second
                - actual.players[0].velocity_x_milli_per_second)
                .abs()
                < 2000,
            "predicted ground control {} differs from authority {}",
            shown.players[0].velocity_x_milli_per_second,
            actual.players[0].velocity_x_milli_per_second
        );
    }
}
