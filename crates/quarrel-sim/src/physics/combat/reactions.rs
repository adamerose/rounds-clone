use super::*;

#[derive(Clone)]
pub(super) struct Reaction {
    due: u32,
    owner: u8,
    depth: u8,
    kind: ReactionKind,
}
#[derive(Clone)]
enum ReactionKind {
    Event {
        event: CardEvent,
        target: Option<u8>,
        shot: Option<u32>,
        position: Vector,
        damage: u16,
    },
    Block,
    Damage {
        target: u8,
        amount: u16,
        poison: bool,
    },
}
const MAX_DEPTH: u8 = 8;
const MAX_PENDING: usize = 256;
const MAX_PER_TICK: usize = 128;

impl AuthoritativeMatch {
    fn enqueue(&mut self, reaction: Reaction) {
        if reaction.depth > MAX_DEPTH {
            return;
        }
        if reaction.depth == 0
            && self.reactions.len() == MAX_PENDING
            && let Some(index) = self
                .reactions
                .iter()
                .enumerate()
                .filter(|(_, pending)| pending.depth > 0)
                .max_by_key(|(_, pending)| (pending.depth, pending.due))
                .map(|(index, _)| index)
        {
            self.reactions.remove(index);
        }
        if self.reactions.len() < MAX_PENDING {
            self.reactions.push_back(reaction);
        }
    }
    pub(super) fn queue_event(
        &mut self,
        owner: u8,
        event: CardEvent,
        target: Option<u8>,
        shot: Option<u32>,
        depth: u8,
    ) {
        let position = shot
            .and_then(|id| self.physics.bullet_pose(id).map(|p| p.0))
            .unwrap_or_else(|| {
                let at_target = matches!(event, CardEvent::Hit | CardEvent::Kill);
                self.physics
                    .player_pose(if at_target {
                        target.unwrap_or(owner)
                    } else {
                        owner
                    })
                    .0
            });
        self.queue_event_at(owner, event, target, shot, depth, position);
    }
    pub(super) fn queue_event_at(
        &mut self,
        owner: u8,
        event: CardEvent,
        target: Option<u8>,
        shot: Option<u32>,
        depth: u8,
        position: Vector,
    ) {
        let damage = shot
            .and_then(|id| self.projectile_entities.get(&id))
            .map(|entity| {
                self.shot_damage(*self.world.entity(*entity).get::<ProjectileState>().unwrap())
            })
            .unwrap_or_else(|| self.current_damage(owner));
        self.enqueue(Reaction {
            due: self.tick,
            owner,
            depth,
            kind: ReactionKind::Event {
                event,
                target,
                shot,
                position,
                damage,
            },
        });
    }
    fn current_damage(&self, owner: u8) -> u16 {
        let cap = self.flow.capabilities(owner);
        if self.tuning.damage_per_hit.saturating_add(cap.damage_bonus) == 0 {
            return 0;
        }
        (u32::from(self.tuning.damage_per_hit.saturating_add(cap.damage_bonus))
            * factor(cap.damage_factor_milli)
            / 1000)
            .clamp(1, u32::from(u16::MAX)) as u16
    }
    pub(super) fn shot_damage(&self, shot: ProjectileState) -> u16 {
        if shot.damage == 0 {
            return 0;
        }
        (f32::from(shot.damage) * (1.0 + shot.distance / 100.0 * f32::from(shot.growth) / 1000.0))
            .clamp(1.0, f32::from(u16::MAX)) as u16
    }
    pub(super) fn fire_shot(&mut self, owner: u8, aim: Vector, depth: u8) {
        if !self
            .world
            .entity(self.player_entities[usize::from(owner)])
            .get::<PlayerState>()
            .unwrap()
            .alive
        {
            return;
        }
        if depth > 0 && self.projectile_entities.len() >= 512 {
            return;
        }
        let cap = self.flow.capabilities(owner);
        let id = self.next_projectile_id;
        self.next_projectile_id = self.next_projectile_id.wrapping_add(1);
        self.physics.spawn_bullet(
            id,
            owner,
            aim,
            self.tuning.bullet_speed * f32::from(cap.projectile_speed_factor.milli) / 1000.0,
        );
        self.physics.recoil(owner, aim);
        if self.tuning.recoil_enabled {
            self.metrics.recoil_impulses += 1;
        }
        let mut state = ProjectileState {
            id,
            owner,
            damage: self.current_damage(owner),
            depth,
            dazzle_pulses: cap.dazzle_stun_pulses,
            dazzle_stun_ticks: cap.dazzle_stun_ticks,
            explosive_radius_milli: cap.explosion_radius_milli,
            bounces: 0,
            growth: 0,
            steering: 0,
            drill_remaining: 0.0,
            distance: 0.0,
            touching_terrain: false,
            touching_object: None,
        };
        // A triggered shot inherits the current weapon's flight changes even if its next chain fades.
        for rule in self.flow.rules_for(owner) {
            if rule.on == CardEvent::Fire && depth <= rule.max_depth {
                for effect in rule
                    .effects
                    .into_iter()
                    .filter(CardEffect::is_flight_change)
                {
                    apply_flight_change(&mut state, effect);
                }
            }
        }
        let entity = self.world.spawn(state).id();
        self.projectile_entities.insert(id, entity);
        self.metrics.shots_fired += 1;
        self.queue_event(owner, CardEvent::Fire, None, Some(id), depth);
    }
    pub(super) fn process_reactions(&mut self) {
        // Each invocation shares the same tick budget; both pending and generation are bounded.
        if self.reaction_budget_tick != self.tick {
            self.reaction_budget_tick = self.tick;
            self.reaction_budget = MAX_PER_TICK;
        }
        while self.reaction_budget > 0 {
            let next = self
                .reactions
                .iter()
                .position(|r| r.due <= self.tick && r.depth == 0)
                .or_else(|| self.reactions.iter().position(|r| r.due <= self.tick));
            let Some(index) = next else {
                break;
            };
            let reaction = self.reactions.remove(index).unwrap();
            self.reaction_budget -= 1;
            if !matches!(reaction.kind, ReactionKind::Damage { .. })
                && reaction.depth > 1
                && self.reaction_random.next() % 1000 >= (1000u64 >> (reaction.depth - 1))
            {
                continue;
            }
            let owner = reaction.owner;
            match reaction.kind {
                ReactionKind::Block => {
                    if !self
                        .world
                        .entity(self.player_entities[usize::from(owner)])
                        .get::<PlayerState>()
                        .unwrap()
                        .alive
                    {
                        continue;
                    }
                    let mut actor = self
                        .world
                        .entity_mut(self.player_entities[usize::from(owner)]);
                    let mut state = actor.get_mut::<PlayerState>().unwrap();
                    state.block_ticks = state.block_ticks.max(self.tuning.block_duration_ticks);
                    self.metrics.block_activations += 1;
                    self.queue_event(owner, CardEvent::Block, None, None, reaction.depth);
                }
                ReactionKind::Damage {
                    target,
                    amount,
                    poison,
                } => self.card_damage(owner, target, amount, reaction.depth, poison),
                ReactionKind::Event {
                    event,
                    target,
                    shot,
                    position,
                    damage,
                } => {
                    for rule in self.flow.rules_for(owner) {
                        if rule.on != event || reaction.depth > rule.max_depth {
                            continue;
                        }
                        for effect in rule.effects {
                            if event == CardEvent::Fire && effect.is_flight_change() {
                                continue;
                            }
                            self.apply_card_effect(
                                owner,
                                reaction.depth,
                                target,
                                shot,
                                position,
                                damage,
                                effect,
                            );
                        }
                    }
                }
            }
        }
    }
    fn card_damage(&mut self, owner: u8, target: u8, amount: u16, depth: u8, poison: bool) {
        let Some(eliminated) = self
            .apply_damage(target, amount)
            .expect("card target belongs to match")
        else {
            return;
        };
        self.metrics.hits += 1;
        let position = self.physics.player_pose(target).0;
        self.impacts.push(ImpactSnapshot {
            id: 0,
            tick: self.tick,
            owner,
            target: Some(target),
            x_milli: quantize(position.x),
            y_milli: quantize(position.y),
            damage: amount,
            eliminated,
            impulse_x_milli: 0,
            impulse_y_milli: 0,
            radius_milli: 0,
            poison,
        });
        self.queue_event(owner, CardEvent::Hit, Some(target), None, depth);
        self.queue_event(target, CardEvent::TakeDamage, Some(owner), None, depth);
        if eliminated {
            self.queue_event(owner, CardEvent::Kill, Some(target), None, depth);
        }
    }
    #[allow(clippy::too_many_arguments)]
    fn apply_card_effect(
        &mut self,
        owner: u8,
        depth: u8,
        target: Option<u8>,
        shot: Option<u32>,
        position: Vector,
        damage: u16,
        effect: CardEffect,
    ) {
        let aim = self
            .world
            .entity(self.player_entities[usize::from(owner)])
            .get::<PlayerState>()
            .unwrap()
            .aim;
        match effect {
            CardEffect::Reload => {
                let magazine = self
                    .tuning
                    .magazine_size
                    .saturating_add(self.flow.capabilities(owner).magazine_bonus);
                let mut actor = self
                    .world
                    .entity_mut(self.player_entities[usize::from(owner)]);
                let mut state = actor.get_mut::<PlayerState>().unwrap();
                state.ammunition = magazine;
                state.reload_ticks = 0;
                state.fire_cooldown = 0;
            }
            CardEffect::Teleport { distance } => {
                self.physics
                    .blink_player(owner, aim * f32::from(distance), self.arena.frame);
            }
            CardEffect::RepeatBlock { delay_ticks } => self.enqueue(Reaction {
                due: self.tick.saturating_add(u32::from(delay_ticks)),
                owner,
                depth: depth + 1,
                kind: ReactionKind::Block,
            }),
            CardEffect::ExtraShots {
                count,
                spread_milliradians,
            } => {
                if depth >= MAX_DEPTH {
                    return;
                }
                for index in 0..count {
                    let angle = (f32::from(index) - f32::from(count - 1) / 2.0)
                        * f32::from(spread_milliradians)
                        / 1000.0;
                    let direction = Vector::new(
                        aim.x * angle.cos() - aim.y * angle.sin(),
                        aim.x * angle.sin() + aim.y * angle.cos(),
                    );
                    self.fire_shot(owner, direction, depth + 1);
                }
            }
            CardEffect::FireAtOpponent => {
                if depth >= MAX_DEPTH {
                    return;
                }
                let origin = self.physics.player_pose(owner).0;
                if let Some((_, delta)) = self
                    .player_entities
                    .iter()
                    .enumerate()
                    .filter(|(id, entity)| {
                        *id != usize::from(owner)
                            && self
                                .world
                                .entity(**entity)
                                .get::<PlayerState>()
                                .unwrap()
                                .alive
                    })
                    .map(|(id, _)| (id, self.physics.player_pose(id as u8).0 - origin))
                    .filter(|(_, delta)| self.physics.line_of_sight(origin, origin + *delta))
                    .min_by(|a, b| a.1.length_squared().total_cmp(&b.1.length_squared()))
                {
                    self.fire_shot(owner, delta.normalize_or_zero(), depth + 1);
                }
            }
            CardEffect::Explode {
                radius,
                damage_milli,
            } => {
                self.metrics.explosive_projectile_impacts += 1;
                self.impacts.push(ImpactSnapshot {
                    id: shot.unwrap_or(0),
                    tick: self.tick,
                    owner,
                    target: None,
                    x_milli: quantize(position.x),
                    y_milli: quantize(position.y),
                    damage: 0,
                    eliminated: false,
                    impulse_x_milli: 0,
                    impulse_y_milli: 0,
                    radius_milli: i32::from(radius) * 1000,
                    poison: false,
                });
                for target in 0..self.player_entities.len() {
                    if target == usize::from(owner) {
                        continue;
                    }
                    let delta = self.physics.player_pose(target as u8).0 - position;
                    if delta.length() <= f32::from(radius) {
                        let amount = if damage == 0 {
                            0
                        } else {
                            (u32::from(damage) * u32::from(damage_milli) / 1000)
                                .clamp(1, u32::from(u16::MAX)) as u16
                        };
                        self.card_damage(owner, target as u8, amount, depth + 1, false);
                        self.physics.apply_impulse(
                            target as u8,
                            delta.normalize_or_zero() * self.tuning.hit_impulse,
                        );
                    }
                }
            }
            CardEffect::Poison {
                ticks,
                interval_ticks,
                damage_milli,
            } => {
                // Poison ticks count as hits but do not lay down a fresh poison stack.
                if shot.is_some()
                    && let Some(target) = target
                {
                    let amount = if damage == 0 {
                        0
                    } else {
                        (u32::from(damage) * u32::from(damage_milli) / 1000)
                            .clamp(1, u32::from(u16::MAX)) as u16
                    };
                    if amount == 0 {
                        return;
                    }
                    for tick in 1..=ticks {
                        self.enqueue(Reaction {
                            due: self
                                .tick
                                .saturating_add(u32::from(tick) * u32::from(interval_ticks)),
                            owner,
                            depth: depth + 1,
                            kind: ReactionKind::Damage {
                                target,
                                amount,
                                poison: true,
                            },
                        });
                    }
                }
            }
            effect => {
                if let Some(entity) = shot
                    .and_then(|id| self.projectile_entities.get(&id))
                    .copied()
                {
                    let mut actor = self.world.entity_mut(entity);
                    let mut state = actor.get_mut::<ProjectileState>().unwrap();
                    apply_flight_change(&mut state, effect);
                }
            }
        }
    }
    pub(super) fn update_card_projectiles(&mut self) {
        for (&id, &entity) in &self.projectile_entities {
            let Some((position, previous, _, _)) = self.physics.bullet_pose(id) else {
                continue;
            };
            let owner = self
                .world
                .entity(entity)
                .get::<ProjectileState>()
                .unwrap()
                .owner;
            let aim = self
                .world
                .entity(self.player_entities[usize::from(owner)])
                .get::<PlayerState>()
                .unwrap()
                .aim;
            let mut actor = self.world.entity_mut(entity);
            let mut shot = actor.get_mut::<ProjectileState>().unwrap();
            shot.distance += position.distance(previous);
            if !self.physics.bullet_platform_contact(id) {
                shot.touching_terrain = false;
            }
            let scale = (1.0 + shot.distance / 100.0 * f32::from(shot.growth) / 1000.0).min(6.0);
            self.physics.configure_bullet(
                id,
                self.tuning.bullet_radius * scale,
                shot.drill_remaining > 0.0,
            );
            if shot.steering > 0 {
                self.physics
                    .steer_bullet(id, aim, f32::from(shot.steering) / 1000.0);
            }
        }
    }
    pub fn card_reload_error(&self) -> Option<&str> {
        self.card_reload_error.as_deref()
    }
    pub(super) fn reload_cards_if_changed(&mut self) {
        if !self.tick.is_multiple_of(15) {
            return;
        }
        let Some(path) = &self.card_path else {
            return;
        };
        match load_card_directory(path).and_then(|catalog| self.flow.replace_catalog(catalog)) {
            Ok(()) => {
                self.card_reload_error = None;
                for entity in &self.player_entities {
                    let mut actor = self.world.entity_mut(*entity);
                    let mut state = actor.get_mut::<PlayerState>().unwrap();
                    state.ammunition = state.ammunition.min(
                        self.tuning
                            .magazine_size
                            .saturating_add(self.flow.capabilities(state.id).magazine_bonus),
                    );
                }
            }
            Err(error) => self.card_reload_error = Some(error),
        }
    }
}

fn apply_flight_change(state: &mut ProjectileState, effect: CardEffect) {
    match effect {
        CardEffect::Bounce(n) => state.bounces = state.bounces.saturating_add(n),
        CardEffect::Grow(n) => state.growth = state.growth.saturating_add(n),
        CardEffect::Steer(n) => state.steering = state.steering.saturating_add(n).min(1000),
        CardEffect::Drill(n) => state.drill_remaining += f32::from(n),
        _ => unreachable!("only flight effects reach the projectile modifier"),
    }
}
