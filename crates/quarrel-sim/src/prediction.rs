use super::*;

/// Presentation-only physics. Other bodies are fixed proxies; it cannot award
/// damage, deaths, points or cards. A new authority sample replaces its base.
pub struct LocalPrediction {
    physics: physics::PhysicsBoundary,
    player: PlayerSnapshot,
    tuning: CombatTuning,
    capabilities: FighterCapabilities,
    frame: [f32; 4],
    jump_held: bool,
    block_held: bool,
    shots: Vec<ProjectileSnapshot>,
    next_shot: u32,
}

impl LocalPrediction {
    pub fn new(state: &MatchSnapshot, id: u8, tuning: CombatTuning, previous: PlayerInput) -> Self {
        let rendered = state.arena_objects.as_ref();
        let frame = rendered.map_or([-1000., -1000., 1000., 1000.], |arena| arena.frame);
        let mut arena = ArenaDefinition {
            name: "prediction".into(),
            frame,
            spawns: rendered.map_or_else(Vec::new, |arena| arena.spawns.clone()),
            objects: rendered.map_or_else(Vec::new, |arena| arena.objects.clone()),
            chains: Vec::new(),
            surfaces: state.arena.clone(),
            legacy_bodies: Vec::new(),
            legacy_saws: Vec::new(),
        };
        for object in &mut arena.objects {
            if !matches!(object.kind, ArenaKind::Background) {
                object.kind = ArenaKind::Solid;
            }
            object.motion = None;
        }
        let mut physics =
            physics::PhysicsBoundary::new(state.players.len(), &arena, tuning.clone());
        physics.replace_arena(&arena);
        physics.prediction_poses(&state.players, id);
        // Establish support normals, then restore the authoritative poses.
        physics.step();
        physics.prediction_poses(&state.players, id);
        let shots = state
            .projectiles
            .iter()
            .filter(|shot| shot.owner == id)
            .cloned()
            .collect::<Vec<_>>();
        for shot in &shots {
            physics.spawn_bullet(shot.id, id, Vector::X, tuning.bullet_speed);
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
        if !p.alive {
            return;
        }
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
                self.tuning.bullet_speed * self.capabilities.projectile_speed_factor.milli as f32
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
        self.shots.retain_mut(|shot| {
            if self.physics.bullet_platform_contact(shot.id) {
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
        state.players[self.player.id as usize] = self.player.clone();
        state
            .projectiles
            .retain(|shot| shot.owner != self.player.id);
        state.projectiles.extend(self.shots.iter().cloned());
    }
}
