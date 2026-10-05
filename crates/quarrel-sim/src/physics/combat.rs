use super::*;
fn spawn_dynamic_bodies(
    world: &mut World,
    dynamic_definitions: Vec<DynamicBodyDefinition>,
) -> BTreeMap<u16, Entity> {
    dynamic_definitions
        .into_iter()
        .map(|definition| {
            let id = definition.id;
            let entity = world
                .spawn(DynamicBodyState {
                    id,
                    shape: definition.shape,
                    width: definition.width,
                    height: definition.height,
                    radius: definition.radius,
                    face_rgb: definition.face_rgb,
                    mass: definition.mass,
                    friction: definition.friction,
                    restitution: definition.restitution,
                })
                .id();
            (id, entity)
        })
        .collect()
}

fn spawn_timber_constraints(world: &mut World) -> BTreeMap<u16, Entity> {
    timber_body_definitions()
        .into_iter()
        .map(|definition| {
            let (id, kind, anchor, active) = match definition.shape {
                DynamicBodyShape::Timber | DynamicBodyShape::Crate => (
                    definition.id,
                    ConstraintKind::Fixed,
                    definition.position,
                    true,
                ),
                DynamicBodyShape::Weight => (
                    1_000 + definition.id,
                    ConstraintKind::Rope,
                    Vector::new(definition.position.x, 330.0),
                    true,
                ),
            };
            let entity = world
                .spawn(ConstraintState {
                    id,
                    body_a: None,
                    body_b: definition.id,
                    kind,
                    anchor,
                    active,
                })
                .id();
            (id, entity)
        })
        .collect()
}

#[derive(Clone, Copy, PartialEq, Eq)]
enum ArenaStage {
    Profile,
    Timber,
    Ice,
    HangingEntry,
}

pub struct AuthoritativeMatch {
    world: World,
    physics: PhysicsBoundary,
    player_entities: [Entity; 2],
    projectile_entities: BTreeMap<u32, Entity>,
    dynamic_body_entities: BTreeMap<u16, Entity>,
    constraint_entities: BTreeMap<u16, Entity>,
    saw_entities: BTreeMap<u16, Entity>,
    explosions: Vec<ExplosionSnapshot>,
    impacts: Vec<ImpactSnapshot>,
    profile: ReplayProfile,
    seed: u64,
    tick: u32,
    next_projectile_id: u32,
    metrics: CombatMetrics,
    winner: Option<u8>,
    explosion_strength: f32,
    flow: Option<FlowAuthority>,
    round: Option<RoundStateSnapshot>,
    pending_radial_hit: Option<PendingRadialHit>,
    arena_stage: ArenaStage,
    arena_entry_from_milli: Option<[[i32; 2]; 2]>,
}

impl AuthoritativeMatch {
    pub fn new(seed: u64) -> Self {
        Self::new_with_profile(seed, ReplayProfile::default())
    }

    pub fn new_with_profile(seed: u64, profile: ReplayProfile) -> Self {
        let mut world = World::new();
        let player_entities = [0_u8, 1_u8].map(|id| {
            world
                .spawn(PlayerState {
                    id,
                    aim: Vector::new(if id == 0 { 1.0 } else { -1.0 }, 0.0),
                    health: 100,
                    fire_cooldown: 0,
                    block_ticks: 0,
                    hit_flash_ticks: 0,
                    grounded: true,
                    jump_held: false,
                    alive: true,
                    stun_ticks: 0,
                    stun_pulses_remaining: 0,
                    stun_pulse_cooldown: 0,
                })
                .id()
        });
        let dynamic_definitions = match profile {
            ReplayProfile::TimberCollapseReplay => timber_body_definitions(),
            ReplayProfile::YellowCrateTerminalBlastReplay => yellow_crate_definitions(),
            _ => Vec::new(),
        };
        let dynamic_body_entities = spawn_dynamic_bodies(&mut world, dynamic_definitions);
        let constraint_entities = if profile == ReplayProfile::TimberCollapseReplay {
            spawn_timber_constraints(&mut world)
        } else {
            BTreeMap::new()
        };
        let saw_entities = if profile == ReplayProfile::RadialSawHalfBlueReplay {
            RADIAL_SAWS
                .into_iter()
                .map(|definition| {
                    let id = definition.id;
                    let entity = world
                        .spawn(SawState {
                            id,
                            radius: definition.radius,
                            teeth: definition.teeth,
                            initial_position: definition.position,
                            initial_angle: definition.initial_angle,
                            angular_velocity: definition.angular_velocity,
                        })
                        .id();
                    (id, entity)
                })
                .collect()
        } else {
            BTreeMap::new()
        };
        let mut simulation = Self {
            world,
            physics: PhysicsBoundary::new(profile),
            player_entities,
            projectile_entities: BTreeMap::new(),
            dynamic_body_entities,
            constraint_entities,
            saw_entities,
            explosions: Vec::new(),
            impacts: Vec::new(),
            profile,
            seed,
            tick: 0,
            next_projectile_id: 1,
            metrics: CombatMetrics::default(),
            winner: None,
            explosion_strength: if profile == ReplayProfile::YellowCrateTerminalBlastReplay {
                YELLOW_EXPLOSION_IMPULSE
            } else {
                TIMBER_EXPLOSION_IMPULSE
            },
            flow: match profile {
                ReplayProfile::RematchDraftReplay => Some(FlowAuthority::historical_rematch(seed)),
                ReplayProfile::MatchEndWaitingReplay => {
                    Some(FlowAuthority::match_end_waiting_replay(seed))
                }
                _ => None,
            },
            round: (profile == ReplayProfile::RadialSawHalfBlueReplay)
                .then_some(RoundStateSnapshot {
                    completed_rounds: None,
                    phase: RoundPhase::Combat,
                    phase_tick: 0,
                    scores: [1, 0],
                    winner: None,
                    eliminated: None,
                })
                .or_else(|| {
                    (profile == ReplayProfile::YellowCrateTerminalBlastReplay).then_some(
                        RoundStateSnapshot {
                            completed_rounds: None,
                            phase: RoundPhase::Combat,
                            phase_tick: 0,
                            scores: [2, 1],
                            winner: None,
                            eliminated: None,
                        },
                    )
                }),
            pending_radial_hit: None,
            arena_entry_from_milli: None,
            arena_stage: if profile == ReplayProfile::TimberCollapseReplay {
                ArenaStage::Timber
            } else {
                ArenaStage::Profile
            },
        };
        if profile == ReplayProfile::RematchDraftReplay {
            let mut orange = simulation.world.entity_mut(simulation.player_entities[0]);
            let mut state = orange
                .get_mut::<PlayerState>()
                .expect("orange player state");
            state.health = 0;
            state.alive = false;
            simulation.winner = Some(1);
            simulation.sync_round_from_flow();
        }
        if profile == ReplayProfile::MatchEndWaitingReplay {
            simulation.sync_round_from_flow();
        }
        simulation
    }

    pub fn reset_radial_arena(&mut self) -> Result<(), String> {
        if self.profile != ReplayProfile::RadialSawHalfBlueReplay {
            return Err("radial arena reset requires radial-saw-half-blue-replay".to_owned());
        }
        for entity in self.saw_entities.values() {
            let state = *self
                .world
                .entity(*entity)
                .get::<SawState>()
                .expect("saw state");
            self.physics.reset_saw(
                state.id,
                state.initial_position,
                state.initial_angle,
                state.angular_velocity,
            );
        }
        self.round = Some(RoundStateSnapshot {
            completed_rounds: None,
            phase: RoundPhase::Combat,
            phase_tick: 0,
            scores: [1, 0],
            winner: None,
            eliminated: None,
        });
        self.winner = None;
        self.pending_radial_hit = None;
        Ok(())
    }

    /// Applies a non-player match-lifecycle request. Player inputs cannot reach
    /// this boundary; the external policy that chooses when to call it belongs
    /// to a later host, lobby, or readiness implementation.
    pub fn request_lifecycle(&mut self, request: LifecycleRequest) -> LifecycleResult {
        let Some(flow) = &mut self.flow else {
            return LifecycleResult::WrongPhase;
        };
        let result = flow.request_lifecycle(request);
        if result == LifecycleResult::Accepted {
            self.clear_projectiles();
            self.explosions.clear();
            self.impacts.clear();
            self.physics.respawn_players();
            self.arena_entry_from_milli = None;
            self.revive_fighters();
            for entity in self.player_entities {
                self.world
                    .entity_mut(entity)
                    .get_mut::<PlayerState>()
                    .expect("player state")
                    .grounded = true;
            }
            self.winner = None;
            self.sync_round_from_flow();
        }
        result
    }

    pub fn step(&mut self, inputs: [PlayerInput; 2]) {
        self.tick += 1;
        if self.flow.is_none()
            && let Some(round) = &mut self.round
            && round.phase != RoundPhase::Combat
        {
            round.phase_tick += 1;
            if round.phase == RoundPhase::ResultTransition {
                if self.profile == ReplayProfile::RadialSawHalfBlueReplay
                    && self.tick >= RADIAL_HALF_BLUE_TICK
                {
                    round.phase = RoundPhase::HalfBlue;
                } else if self.profile == ReplayProfile::YellowCrateTerminalBlastReplay
                    && self.tick >= YELLOW_ROUND_ORANGE_TICK
                {
                    round.phase = RoundPhase::RoundOrange;
                }
            }
            return;
        }
        if let Some(hit) = self.pending_radial_hit.take() {
            let target_entity = self.player_entities[usize::from(hit.target)];
            let (damage_scale, eliminated) = {
                let mut target_entity_mut = self.world.entity_mut(target_entity);
                let mut target_state = target_entity_mut
                    .get_mut::<PlayerState>()
                    .expect("player state");
                target_state.health = target_state.health.saturating_sub(50);
                target_state.hit_flash_ticks = 6;
                let damage_scale = 1.0 + (100 - target_state.health) as f32 / 70.0;
                let eliminated = target_state.health == 0;
                if eliminated {
                    target_state.alive = false;
                }
                (damage_scale, eliminated)
            };
            self.physics.apply_impulse(
                hit.target,
                hit.direction * RADIAL_HIT_IMPULSE * damage_scale,
            );
            self.metrics.hits += 1;
            self.metrics.health_scaled_knockbacks += 1;
            if eliminated {
                self.resolve_elimination_outcome();
            }
            if self.winner.is_some() {
                return;
            }
        }
        let mut rematch_reset = false;
        let mut timber_load = false;
        let mut ice_load = false;
        let mut hanging_entry_load = false;
        let mut timber_combat_started = false;
        let mut repeated_after_draw = false;
        let mut waiting_entered = false;
        let mut accepts_combat = true;
        if let Some(flow) = &mut self.flow {
            let had_terminal_result = flow.has_terminal_result();
            let previous_phase = flow.snapshot().phase;
            flow.advance(inputs.map(|input| input.flow));
            let phase = flow.snapshot().phase;
            rematch_reset = had_terminal_result && !flow.has_terminal_result();
            timber_load = previous_phase != FlowPhase::TimberTransition
                && phase == FlowPhase::TimberTransition;
            ice_load =
                previous_phase != FlowPhase::IceTransition && phase == FlowPhase::IceTransition;
            hanging_entry_load =
                previous_phase != FlowPhase::HangingEntry && phase == FlowPhase::HangingEntry;
            timber_combat_started = previous_phase != phase
                && matches!(phase, FlowPhase::TimberCombat | FlowPhase::IceCombat);
            repeated_after_draw =
                previous_phase == FlowPhase::EliminationConclusion && flow.accepts_combat();
            waiting_entered = previous_phase != FlowPhase::Waiting && phase == FlowPhase::Waiting;
            accepts_combat = flow.accepts_combat();
        }
        self.sync_round_from_flow();
        if timber_load {
            self.load_timber_arena();
            self.revive_fighters();
        }
        if ice_load {
            self.load_ice_arena();
            self.revive_fighters();
        }
        if hanging_entry_load {
            self.load_held_hanging_entry();
        }
        if repeated_after_draw {
            self.repeat_fight_in_place();
        }
        if waiting_entered {
            self.enter_match_waiting();
        }
        if timber_combat_started || rematch_reset {
            self.arena_entry_from_milli = None;
            self.revive_fighters();
            self.winner = None;
        }
        if !accepts_combat {
            return;
        }
        if self.flow.is_none()
            && self.profile != ReplayProfile::YellowCrateTerminalBlastReplay
            && (self.winner.is_some() || self.living_fighters().is_empty())
        {
            return;
        }
        let inputs = inputs.map(PlayerInput::validated);
        for (index, input) in inputs.into_iter().enumerate() {
            let entity = self.player_entities[index];
            let mut fire = None;
            {
                let mut player_entity = self.world.entity_mut(entity);
                let mut state = player_entity
                    .get_mut::<PlayerState>()
                    .expect("player state");
                state.fire_cooldown = state.fire_cooldown.saturating_sub(1);
                state.block_ticks = state.block_ticks.saturating_sub(1);
                state.hit_flash_ticks = state.hit_flash_ticks.saturating_sub(1);
                state.stun_ticks = state.stun_ticks.saturating_sub(1);
                if state.stun_pulses_remaining > 0 {
                    state.stun_pulse_cooldown = state.stun_pulse_cooldown.saturating_sub(1);
                    if state.stun_pulse_cooldown == 0 {
                        state.stun_ticks = state.stun_ticks.max(6);
                        state.hit_flash_ticks = 4;
                        state.stun_pulses_remaining -= 1;
                        state.stun_pulse_cooldown = 8;
                        self.metrics.dazzle_stun_pulses += 1;
                    }
                }
                // An eliminated fighter keeps its last pose but no input reaches it,
                // including after a ring-out that leaves positive health.
                let acts = state.alive && state.health > 0;
                // The release memory follows the jump input on every tick the
                // authority reads it, whether or not control runs for this
                // fighter. A fighter that lets go while stunned or eliminated has
                // let go; deferring that release to the tick control returns would
                // cut a jump the fighter is already halfway through.
                let released = state.jump_held && !input.jump;
                state.jump_held = input.jump;
                if acts && (input.aim_x != 0 || input.aim_y != 0) {
                    state.aim = Vector::new(f32::from(input.aim_x), f32::from(input.aim_y))
                        .normalize_or_zero();
                }
                if acts && input.block && state.block_ticks == 0 {
                    state.block_ticks = BLOCK_DURATION;
                    self.metrics.block_activations += 1;
                }
                if acts
                    && state.stun_ticks == 0
                    && self
                        .physics
                        .set_player_control(state.id, input, state.grounded, released)
                {
                    self.metrics.jumps += 1;
                    state.grounded = false;
                }
                if acts && state.stun_ticks == 0 && input.fire && state.fire_cooldown == 0 {
                    let extra = self
                        .flow
                        .as_ref()
                        .map(|flow| flow.capabilities(state.id).fire_cooldown_extra_ticks)
                        .unwrap_or(0);
                    state.fire_cooldown = FIRE_COOLDOWN + extra;
                    fire = Some((state.id, state.aim));
                }
            }
            if let Some((player_id, aim)) = fire {
                let capabilities = self
                    .flow
                    .as_ref()
                    .map(|flow| flow.capabilities(player_id))
                    .unwrap_or_default();
                let projectile_id = self.next_projectile_id;
                self.next_projectile_id += 1;
                self.physics.spawn_bullet(
                    projectile_id,
                    player_id,
                    aim,
                    projectile_launch_speed(capabilities),
                    self.profile != ReplayProfile::RadialSawHalfBlueReplay,
                );
                self.physics.apply_impulse(player_id, -aim * RECOIL_IMPULSE);
                let projectile_entity = self
                    .world
                    .spawn(ProjectileState {
                        id: projectile_id,
                        owner: player_id,
                        dazzle_pulses: capabilities.dazzle_stun_pulses,
                        dazzle_stun_ticks: capabilities.dazzle_stun_ticks,
                        explosive_radius_milli: capabilities.explosion_radius_milli,
                        explosive_impulse_milli: capabilities.explosion_impulse_milli,
                    })
                    .id();
                self.projectile_entities
                    .insert(projectile_id, projectile_entity);
                self.metrics.shots_fired += 1;
                self.metrics.recoil_impulses += 1;
            }
        }

        // The standalone ticket-040 replay retains its admitted source event.
        // The connected flow never enters this branch: its explosions require bullets.
        if self.profile == ReplayProfile::TimberCollapseReplay && self.tick == TIMBER_IMPACT_TICK {
            self.release_timber_constraints();
            self.metrics.explosion_impulsed_bodies += self.physics.apply_radial_explosion(
                TIMBER_EXPLOSION_CENTER,
                TIMBER_EXPLOSION_RADIUS,
                self.explosion_strength,
            );
            self.explosions.push(ExplosionSnapshot {
                id: 1,
                tick: self.tick,
                x_milli: quantize(TIMBER_EXPLOSION_CENTER.x),
                y_milli: quantize(TIMBER_EXPLOSION_CENTER.y),
                radius_milli: quantize(TIMBER_EXPLOSION_RADIUS),
                impulse_milli: quantize(self.explosion_strength),
            });
        }

        self.physics.step();
        let (dynamic_body_contacts, fighter_body_contacts) = self.physics.dynamic_contact_counts();
        self.metrics.dynamic_body_contacts += dynamic_body_contacts;
        self.metrics.fighter_body_contact_ticks += fighter_body_contacts;
        for id in 0..2 {
            let grounded = self.physics.player_grounded(id);
            if grounded {
                self.metrics.platform_contact_ticks += 1;
            }
            self.world
                .entity_mut(self.player_entities[usize::from(id)])
                .get_mut::<PlayerState>()
                .expect("player state")
                .grounded = grounded;
        }

        let projectile_ids = self.projectile_entities.keys().copied().collect::<Vec<_>>();
        let mut removals = Vec::new();
        for projectile_id in projectile_ids {
            let entity = self.projectile_entities[&projectile_id];
            let projectile = *self
                .world
                .entity(entity)
                .get::<ProjectileState>()
                .expect("projectile state");
            let target = 1 - projectile.owner;
            if let Some(impact_position) = self.physics.bullet_contact(projectile_id, target) {
                let target_entity = self.player_entities[usize::from(target)];
                let (target_alive, blocking) = {
                    let state = self
                        .world
                        .entity(target_entity)
                        .get::<PlayerState>()
                        .expect("player state");
                    (state.alive, state.block_ticks > 0)
                };
                if !target_alive {
                    // A settled elimination cannot be re-decided by a later contact.
                    removals.push(projectile_id);
                    continue;
                }
                if blocking {
                    self.physics.reflect_bullet(projectile_id, target);
                    self.world
                        .entity_mut(entity)
                        .get_mut::<ProjectileState>()
                        .expect("projectile state")
                        .owner = target;
                    self.metrics.reflections += 1;
                    continue;
                }
                let velocity = self
                    .physics
                    .bullet_pose(projectile_id)
                    .map(|(_, _, velocity, _)| velocity.normalize_or_zero())
                    .unwrap_or(Vector::new(if target == 0 { -1.0 } else { 1.0 }, 0.0));
                let damage = if self.profile == ReplayProfile::RadialSawHalfBlueReplay {
                    50
                } else if self.profile == ReplayProfile::RematchDraftReplay {
                    25
                } else {
                    DAMAGE_PER_HIT
                };
                let target_health = self
                    .world
                    .entity(target_entity)
                    .get::<PlayerState>()
                    .expect("player state")
                    .health;
                let remaining_health = target_health.saturating_sub(damage);
                let damage_scale = 1.0 + (100 - remaining_health) as f32 / 70.0;
                let event_impulse = if self.profile == ReplayProfile::YellowCrateTerminalBlastReplay
                {
                    velocity * (HIT_IMPULSE * damage_scale + self.explosion_strength)
                } else {
                    velocity * HIT_IMPULSE
                };
                self.impacts.push(ImpactSnapshot {
                    id: projectile_id,
                    tick: self.tick,
                    owner: projectile.owner,
                    target: Some(target),
                    x_milli: quantize(impact_position.x),
                    y_milli: quantize(impact_position.y),
                    damage,
                    eliminated: target_health <= damage,
                    impulse_x_milli: quantize(event_impulse.x),
                    impulse_y_milli: quantize(event_impulse.y),
                });
                if self.profile == ReplayProfile::RadialSawHalfBlueReplay {
                    self.pending_radial_hit = Some(PendingRadialHit {
                        target,
                        direction: velocity,
                    });
                    removals.push(projectile_id);
                    continue;
                }
                {
                    let mut target_entity_mut = self.world.entity_mut(target_entity);
                    let mut target_state = target_entity_mut
                        .get_mut::<PlayerState>()
                        .expect("player state");
                    target_state.health = target_state.health.saturating_sub(damage);
                    target_state.hit_flash_ticks = 6;
                    if projectile.dazzle_pulses > 0 {
                        target_state.stun_pulses_remaining = projectile.dazzle_pulses;
                        target_state.stun_pulse_cooldown = 1;
                        target_state.stun_ticks = projectile.dazzle_stun_ticks;
                    }
                    if self.profile == ReplayProfile::YellowCrateTerminalBlastReplay {
                        self.physics.apply_impulse(target, event_impulse);
                    } else {
                        self.physics
                            .apply_impulse(target, velocity * HIT_IMPULSE * damage_scale);
                    }
                    self.metrics.hits += 1;
                    self.metrics.health_scaled_knockbacks += 1;
                    if self.profile == ReplayProfile::YellowCrateTerminalBlastReplay {
                        self.metrics.explosion_impulsed_bodies +=
                            self.physics.apply_radial_explosion(
                                impact_position,
                                YELLOW_EXPLOSION_RADIUS,
                                self.explosion_strength,
                            );
                        self.explosions.push(ExplosionSnapshot {
                            id: 20_000 + projectile_id as u16,
                            tick: self.tick,
                            x_milli: quantize(impact_position.x),
                            y_milli: quantize(impact_position.y),
                            radius_milli: quantize(YELLOW_EXPLOSION_RADIUS),
                            impulse_milli: quantize(self.explosion_strength),
                        });
                        self.metrics.explosive_projectile_impacts += 1;
                    }
                    if target_state.health == 0 {
                        target_state.alive = false;
                    }
                }
                if projectile.explosive_radius_milli > 0 {
                    let impulse = projectile.explosive_impulse_milli as f32 / 1_000.0;
                    self.physics.apply_impulse(target, velocity * impulse);
                    self.trigger_projectile_explosion(projectile_id, projectile, impact_position);
                }
                removals.push(projectile_id);
            } else if self.physics.bullet_dynamic_contact(projectile_id).is_some()
                || self.physics.bullet_platform_contact(projectile_id)
            {
                self.metrics.bullet_ccd_contacts += 1;
                if self.profile == ReplayProfile::RadialSawHalfBlueReplay
                    && let Some((center, _, _, _)) = self.physics.bullet_pose(projectile_id)
                {
                    self.impacts.push(ImpactSnapshot {
                        id: projectile_id,
                        tick: self.tick,
                        owner: projectile.owner,
                        target: None,
                        x_milli: quantize(center.x),
                        y_milli: quantize(center.y),
                        damage: 0,
                        eliminated: false,
                        impulse_x_milli: 0,
                        impulse_y_milli: 0,
                    });
                }
                if projectile.explosive_radius_milli > 0
                    && let Some(center) = self
                        .physics
                        .bullet_dynamic_contact(projectile_id)
                        .map(|(_, point)| point)
                        .or_else(|| self.physics.bullet_pose(projectile_id).map(|pose| pose.0))
                {
                    self.impacts.push(ImpactSnapshot {
                        id: projectile_id,
                        tick: self.tick,
                        owner: projectile.owner,
                        target: None,
                        x_milli: quantize(center.x),
                        y_milli: quantize(center.y),
                        damage: 0,
                        eliminated: false,
                        impulse_x_milli: 0,
                        impulse_y_milli: 0,
                    });
                    self.trigger_projectile_explosion(projectile_id, projectile, center);
                }
                removals.push(projectile_id);
            } else if self.physics.bullet_pose(projectile_id).is_none_or(
                |(position, _, _, lifetime)| {
                    lifetime == 0
                        || position.x.abs() > KILL_X + 200.0
                        || position.y < KILL_Y - 100.0
                },
            ) {
                removals.push(projectile_id);
            }
        }
        for projectile_id in removals {
            self.physics.remove_bullet(projectile_id);
            if let Some(entity) = self.projectile_entities.remove(&projectile_id) {
                self.world.despawn(entity);
            }
        }

        for id in 0..2_u8 {
            let entity = self.player_entities[usize::from(id)];
            let (position, _) = self.physics.player_pose(id);
            if position.x.abs() > KILL_X || position.y < KILL_Y {
                let mut player_entity = self.world.entity_mut(entity);
                let mut state = player_entity
                    .get_mut::<PlayerState>()
                    .expect("player state");
                if state.alive {
                    state.alive = false;
                    self.metrics.ring_outs += 1;
                }
            }
        }
        self.resolve_elimination_outcome();
        // Standalone replays defer their result onset to a source tick.
        self.begin_result_if_due();
    }

    fn living_fighters(&self) -> Vec<u8> {
        self.player_entities
            .iter()
            .filter_map(|entity| self.world.entity(*entity).get::<PlayerState>())
            .filter(|state| state.alive)
            .map(|state| state.id)
            .collect()
    }

    /// Decides one fight outcome from who is still alive after this tick's
    /// damage and ring-outs. A lone survivor wins; nobody wins when both fall
    /// together, and an outcome already recorded is never replaced.
    fn resolve_elimination_outcome(&mut self) {
        if self.winner.is_some() {
            return;
        }
        match self.living_fighters().as_slice() {
            [survivor] => {
                self.winner = Some(*survivor);
                self.begin_result_if_due();
            }
            [] => {
                if let Some(flow) = self.flow.as_mut()
                    && flow.record_simultaneous_elimination()
                {
                    self.metrics.simultaneous_eliminations += 1;
                    self.sync_round_from_flow();
                }
            }
            _ => {}
        }
    }

    /// Repeats the current fight in the same arena after a no-award result.
    fn repeat_fight_in_place(&mut self) {
        self.clear_projectiles();
        self.physics.respawn_players();
        self.arena_entry_from_milli = None;
        self.revive_fighters();
        self.winner = None;
    }

    /// Makes the concluded match source-visible without changing its arena,
    /// awarded score, winner, or retained build state.
    fn enter_match_waiting(&mut self) {
        self.clear_projectiles();
        self.physics.respawn_players();
        self.arena_entry_from_milli = None;
        self.revive_fighters();
        for entity in self.player_entities {
            self.world
                .entity_mut(entity)
                .get_mut::<PlayerState>()
                .expect("player state")
                .grounded = true;
        }
    }

    fn clear_projectiles(&mut self) {
        for entity in self
            .projectile_entities
            .values()
            .copied()
            .collect::<Vec<_>>()
        {
            self.world.despawn(entity);
        }
        for id in self.projectile_entities.keys().copied().collect::<Vec<_>>() {
            self.physics.remove_bullet(id);
        }
        self.projectile_entities.clear();
    }

    fn begin_result_if_due(&mut self) {
        if self.profile == ReplayProfile::YellowCrateTerminalBlastReplay
            && self.tick < YELLOW_RESULT_ONSET_TICK
        {
            return;
        }
        if let Some(winner) = self.winner {
            let flow_recorded = self
                .flow
                .as_mut()
                .is_some_and(|flow| flow.record_elimination(winner));
            if flow_recorded {
                self.sync_round_from_flow();
            }
            if self.flow.is_some() {
                return;
            }
        }
        if let Some(winner) = self.winner
            && let Some(round) = &mut self.round
            && round.phase == RoundPhase::Combat
        {
            round.phase = RoundPhase::ResultTransition;
            round.phase_tick = 0;
            round.scores[usize::from(winner)] += 1;
            round.winner = Some(winner);
            round.eliminated = Some(1 - winner);
        }
    }

    fn trigger_projectile_explosion(
        &mut self,
        projectile_id: u32,
        projectile: ProjectileState,
        center: Vector,
    ) {
        // Timber is one destructible assembly. Only an explosive projectile
        // contacting one of its bodies releases its supports; a distant floor
        // or fighter impact cannot collapse the tower.
        let contacted_timber = self
            .physics
            .bullet_dynamic_contact(projectile_id)
            .is_some_and(|(body_id, _)| {
                self.world
                    .entity(self.dynamic_body_entities[&body_id])
                    .get::<DynamicBodyState>()
                    .expect("dynamic body state")
                    .shape
                    == DynamicBodyShape::Timber
            });
        if contacted_timber {
            self.release_timber_constraints();
        }
        let radius = projectile.explosive_radius_milli as f32 / 1_000.0;
        let impulse = projectile.explosive_impulse_milli as f32 / 1_000.0;
        self.metrics.explosion_impulsed_bodies +=
            self.physics.apply_radial_explosion(center, radius, impulse);
        self.explosions.push(ExplosionSnapshot {
            id: 10_000 + projectile_id as u16,
            tick: self.tick,
            x_milli: quantize(center.x),
            y_milli: quantize(center.y),
            radius_milli: projectile.explosive_radius_milli,
            impulse_milli: projectile.explosive_impulse_milli,
        });
        self.metrics.explosive_projectile_impacts += 1;
    }

    fn release_timber_constraints(&mut self) {
        let released = self.physics.release_explosion_constraints();
        for id in &released {
            if let Some(entity) = self.constraint_entities.get(id) {
                self.world
                    .entity_mut(*entity)
                    .get_mut::<ConstraintState>()
                    .expect("constraint state")
                    .active = false;
            }
        }
        self.metrics.released_constraints += released.len() as u32;
    }

    fn sync_round_from_flow(&mut self) {
        let Some(flow) = self.flow.as_ref().map(FlowAuthority::snapshot) else {
            return;
        };
        let phase = match flow.phase {
            FlowPhase::BlueResultTransition | FlowPhase::OrangeResultTransition => {
                RoundPhase::ResultTransition
            }
            FlowPhase::HalfBlue => RoundPhase::HalfBlue,
            FlowPhase::TimberTransition
            | FlowPhase::IceTransition
            | FlowPhase::PostRoundBridge
            | FlowPhase::HangingEntry => RoundPhase::ArenaTransition,
            FlowPhase::RoundBlue => RoundPhase::RoundBlue,
            FlowPhase::RoundOrange => RoundPhase::RoundOrange,
            FlowPhase::HalfOrange => RoundPhase::HalfOrange,
            _ => RoundPhase::Combat,
        };
        self.round = Some(RoundStateSnapshot {
            phase,
            phase_tick: flow.phase_tick,
            scores: flow.halves,
            completed_rounds: Some(flow.scores),
            winner: flow.winner,
            eliminated: flow.eliminated,
        });
    }

    fn load_timber_arena(&mut self) {
        if self.arena_stage == ArenaStage::Timber {
            return;
        }
        for entity in self
            .projectile_entities
            .values()
            .copied()
            .collect::<Vec<_>>()
        {
            self.world.despawn(entity);
        }
        self.projectile_entities.clear();
        self.physics.load_timber_arena();
        self.dynamic_body_entities =
            spawn_dynamic_bodies(&mut self.world, timber_body_definitions());
        self.constraint_entities = spawn_timber_constraints(&mut self.world);
        self.arena_stage = ArenaStage::Timber;
    }

    fn load_ice_arena(&mut self) {
        self.arena_entry_from_milli = Some([0, 1].map(|id| {
            let (position, _) = self.physics.player_pose(id);
            [quantize(position.x), quantize(position.y)]
        }));
        for entity in self
            .projectile_entities
            .values()
            .chain(self.dynamic_body_entities.values())
            .chain(self.constraint_entities.values())
            .copied()
            .collect::<Vec<_>>()
        {
            self.world.despawn(entity);
        }
        self.projectile_entities.clear();
        self.dynamic_body_entities.clear();
        self.constraint_entities.clear();
        self.physics.load_ice_arena();
        self.explosions.clear();
        self.impacts.clear();
        self.arena_stage = ArenaStage::Ice;
    }

    fn load_held_hanging_entry(&mut self) {
        if self.arena_stage == ArenaStage::HangingEntry {
            return;
        }
        for entity in self
            .projectile_entities
            .values()
            .chain(self.dynamic_body_entities.values())
            .chain(self.constraint_entities.values())
            .copied()
            .collect::<Vec<_>>()
        {
            self.world.despawn(entity);
        }
        self.projectile_entities.clear();
        self.dynamic_body_entities.clear();
        self.constraint_entities.clear();
        self.physics.load_held_hanging_entry();
        self.explosions.clear();
        self.impacts.clear();
        self.arena_entry_from_milli = None;
        self.revive_fighters();
        self.winner = None;
        self.arena_stage = ArenaStage::HangingEntry;
    }

    fn revive_fighters(&mut self) {
        for entity in self.player_entities {
            let mut player = self.world.entity_mut(entity);
            let mut state = player.get_mut::<PlayerState>().expect("player state");
            state.health = 100;
            state.alive = true;
            state.fire_cooldown = 0;
            state.block_ticks = 0;
            state.hit_flash_ticks = 0;
            state.stun_ticks = 0;
            state.stun_pulses_remaining = 0;
            state.stun_pulse_cooldown = 0;
            state.jump_held = false;
        }
    }

    pub fn snapshot(&mut self) -> MatchSnapshot {
        let mut players = Vec::with_capacity(2);
        for id in 0..2_u8 {
            let state = *self
                .world
                .entity(self.player_entities[usize::from(id)])
                .get::<PlayerState>()
                .expect("player state");
            let (position, velocity) = self.physics.player_pose(id);
            players.push(PlayerSnapshot {
                id,
                x_milli: quantize(position.x),
                y_milli: quantize(position.y),
                velocity_x_milli_per_second: quantize(velocity.x),
                velocity_y_milli_per_second: quantize(velocity.y),
                aim_x: (state.aim.x * 1_000.0).round() as i16,
                aim_y: (state.aim.y * 1_000.0).round() as i16,
                health: state.health,
                fire_cooldown_ticks: state.fire_cooldown,
                block_ticks: state.block_ticks,
                hit_flash_ticks: state.hit_flash_ticks,
                grounded: state.grounded,
                alive: state.alive,
                stun_ticks: state.stun_ticks,
            });
        }
        let mut projectiles = self
            .projectile_entities
            .iter()
            .filter_map(|(id, entity)| {
                let state = self.world.entity(*entity).get::<ProjectileState>()?;
                let (position, previous, velocity, lifetime) = self.physics.bullet_pose(*id)?;
                Some(ProjectileSnapshot {
                    id: state.id,
                    owner: state.owner,
                    x_milli: quantize(position.x),
                    y_milli: quantize(position.y),
                    previous_x_milli: quantize(previous.x),
                    previous_y_milli: quantize(previous.y),
                    velocity_x_milli_per_second: quantize(velocity.x),
                    velocity_y_milli_per_second: quantize(velocity.y),
                    lifetime_ticks: lifetime,
                    dazzle_pulses: state.dazzle_pulses,
                    explosive_radius_milli: state.explosive_radius_milli,
                })
            })
            .collect::<Vec<_>>();
        projectiles.sort_by_key(|projectile| projectile.id);
        projectiles.truncate(MAX_INSPECTED_PROJECTILES);
        let dynamic_bodies = self
            .dynamic_body_entities
            .iter()
            .filter_map(|(id, entity)| {
                let state = self.world.entity(*entity).get::<DynamicBodyState>()?;
                let (position, rotation, velocity, angular_velocity, sleeping) =
                    self.physics.dynamic_body_pose(*id)?;
                Some(DynamicBodySnapshot {
                    id: state.id,
                    shape: state.shape,
                    x_milli: quantize(position.x),
                    y_milli: quantize(position.y),
                    rotation_milliradians: quantize(rotation),
                    velocity_x_milli_per_second: quantize(velocity.x),
                    velocity_y_milli_per_second: quantize(velocity.y),
                    angular_velocity_milliradians_per_second: quantize(angular_velocity),
                    width_milli: quantize(state.width),
                    height_milli: quantize(state.height),
                    radius_milli: quantize(state.radius),
                    face_rgb: state.face_rgb,
                    sleeping,
                    mass_milli: quantize(state.mass),
                    friction_milli: quantize(state.friction),
                    restitution_milli: quantize(state.restitution),
                })
            })
            .collect();
        let constraints = self
            .constraint_entities
            .values()
            .filter_map(|entity| {
                let state = self.world.entity(*entity).get::<ConstraintState>()?;
                Some(ConstraintSnapshot {
                    id: state.id,
                    body_a: state.body_a,
                    body_b: state.body_b,
                    kind: state.kind,
                    anchor_x_milli: quantize(state.anchor.x),
                    anchor_y_milli: quantize(state.anchor.y),
                    active: state.active,
                })
            })
            .collect();
        let saws = self
            .saw_entities
            .iter()
            .filter_map(|(id, entity)| {
                let state = self.world.entity(*entity).get::<SawState>()?;
                let (position, angle, angular_velocity) = self.physics.saw_pose(*id)?;
                Some(SawSnapshot {
                    id: state.id,
                    x_milli: quantize(position.x),
                    y_milli: quantize(position.y),
                    angle_milliradians: quantize(angle),
                    angular_velocity_milliradians_per_second: quantize(angular_velocity),
                    radius_milli: quantize(state.radius),
                    teeth: state.teeth,
                })
            })
            .collect();
        MatchSnapshot {
            protocol: 10,
            seed: self.seed,
            profile: self.profile.name().to_owned(),
            tick: self.tick,
            arena: if self.arena_stage == ArenaStage::Ice {
                ice_arena().to_vec()
            } else if self.arena_stage == ArenaStage::Timber {
                timber_arena().to_vec()
            } else if self.arena_stage == ArenaStage::HangingEntry {
                Vec::new()
            } else if self.profile == ReplayProfile::RematchDraftReplay
                && self.flow.as_ref().is_some_and(|flow| {
                    matches!(
                        flow.snapshot().phase,
                        FlowPhase::CombatConclusion | FlowPhase::RematchPrompt
                    )
                })
            {
                prior_match_arena().to_vec()
            } else {
                arena_for_profile(self.profile).to_vec()
            },
            hanging_entry: (self.arena_stage == ArenaStage::HangingEntry).then(|| {
                let age = self
                    .flow
                    .as_ref()
                    .map(|flow| flow.snapshot().phase_tick.min(47) as u8)
                    .unwrap_or(0);
                hanging_entry_presentation(age)
            }),
            saws,
            dynamic_bodies,
            constraints,
            explosions: self.explosions.clone(),
            impacts: self.impacts.clone(),
            players,
            arena_entry_from_milli: self.arena_entry_from_milli,
            projectiles,
            metrics: self.metrics.clone(),
            winner: self.winner,
            flow: self.flow.as_ref().map(FlowAuthority::snapshot),
            round: self.round.clone(),
        }
    }

    pub fn state_hash(&mut self) -> String {
        hash_snapshot(&self.snapshot())
    }
}

#[cfg(test)]
mod tests;
