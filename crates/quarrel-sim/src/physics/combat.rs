use super::*;

pub struct AuthoritativeMatch {
    arena: ArenaDefinition,
    tuning: CombatTuning,
    tuning_path: Option<std::path::PathBuf>,
    tuning_source: String,
    tuning_reload_error: Option<String>,
    arena_bag: Vec<ArenaDefinition>,
    arena_index: usize,
    arena_path: Option<std::path::PathBuf>,
    arena_files: BTreeMap<String, std::path::PathBuf>,
    arena_sources: BTreeMap<String, String>,
    arena_source: String,
    arena_reload_error: Option<String>,
    world: World,
    physics: PhysicsBoundary,
    player_entities: Vec<Entity>,
    projectile_entities: BTreeMap<u32, Entity>,
    impacts: Vec<ImpactSnapshot>,
    tick: u32,
    next_projectile_id: u32,
    metrics: CombatMetrics,
    flow: FlowAuthority,
    seed: u64,
    phase: FlowPhase,
    observed_fight: u32,
    observed_match: u32,
}

impl AuthoritativeMatch {
    pub fn new(seed: u64) -> Self {
        let config = MatchConfig {
            seed,
            ..Default::default()
        };
        Self::with_config(config).expect("bundled cards and arenas must be valid")
    }
    pub fn with_config(config: MatchConfig) -> Result<Self, String> {
        let catalog = load_card_directory(&default_card_directory())?;
        let (arenas, paths, sources) = arena_file_map()?;
        let mut game = Self::create(
            config,
            catalog,
            arenas,
            paths,
            sources,
            CombatTuning::default(),
        )?;
        game.watch_tuning_file(&default_tuning_path())?;
        Ok(game)
    }
    pub fn with_content(config: MatchConfig, content: MatchContent) -> Result<Self, String> {
        Self::create(
            config,
            content.cards,
            content.arenas,
            BTreeMap::new(),
            BTreeMap::new(),
            content.tuning,
        )
    }
    pub fn from_arena(seed: u64, definition: ArenaDefinition) -> Result<Self, String> {
        let config = MatchConfig {
            seed,
            ..Default::default()
        };
        Self::with_content(
            config,
            MatchContent {
                tuning: CombatTuning::default(),
                cards: load_card_directory(&default_card_directory())?,
                arenas: vec![definition],
            },
        )
    }
    pub fn from_arena_file(seed: u64, path: &std::path::Path) -> Result<Self, String> {
        let source = std::fs::read_to_string(path)
            .map_err(|error| format!("{}: {error}", path.display()))?;
        let mut game = Self::from_arena(seed, ArenaDefinition::parse(&source)?)?;
        game.arena_path = Some(path.to_owned());
        game.arena_files
            .insert(game.arena.name.clone(), path.to_owned());
        game.arena_sources
            .insert(game.arena.name.clone(), source.clone());
        game.arena_source = source;
        Ok(game)
    }
    fn create(
        config: MatchConfig,
        catalog: Vec<ItemDefinition>,
        mut arenas: Vec<ArenaDefinition>,
        arena_files: BTreeMap<String, std::path::PathBuf>,
        arena_sources: BTreeMap<String, String>,
        tuning: CombatTuning,
    ) -> Result<Self, String> {
        config.validate()?;
        tuning.validate()?;
        arenas = arenas
            .into_iter()
            .map(Self::prepare_definition)
            .collect::<Result<_, _>>()?;
        if arenas.is_empty() {
            return Err("arena pool is empty".into());
        }
        SeededRandom(config.seed).shuffle(&mut arenas);
        let arena_count = arenas.len();
        let arena = arenas[arena_count - 1].clone();
        let arena_path = arena_files.get(&arena.name).cloned();
        let arena_source = arena_sources.get(&arena.name).cloned().unwrap_or_default();
        let mut world = World::new();
        let player_entities = (0..config.fighter_count)
            .map(|index| {
                world
                    .spawn(PlayerState {
                        id: index as u8,
                        aim: Vector::new(if index % 2 == 0 { 1.0 } else { -1.0 }, 0.0),
                        health: 100,
                        fire_cooldown: 0,
                        block_ticks: 0,
                        hit_flash_ticks: 0,
                        grounded: true,
                        jump_held: false,
                        jump_available: true,
                        block_held: false,
                        block_cooldown: 0,
                        ammunition: tuning.magazine_size,
                        reload_ticks: 0,
                        alive: true,
                        stun_ticks: 0,
                        stun_pulses_remaining: 0,
                    })
                    .id()
            })
            .collect();
        let flow = FlowAuthority::with_config(config.clone(), catalog)?;
        let mut physics = PhysicsBoundary::new(config.fighter_count, &arena, tuning.clone());
        physics.replace_arena(&arena);
        Ok(Self {
            physics,
            tuning,
            tuning_path: None,
            tuning_source: String::new(),
            tuning_reload_error: None,
            arena,
            arena_bag: arenas,
            // The first combat advances this cursor to the first shuffled arena.
            arena_index: arena_count - 1,
            arena_path,
            arena_files,
            arena_sources,
            arena_source,
            arena_reload_error: None,
            world,
            player_entities,
            projectile_entities: BTreeMap::new(),
            impacts: Vec::new(),
            tick: 0,
            next_projectile_id: 1,
            metrics: CombatMetrics::default(),
            seed: config.seed,
            phase: FlowPhase::Draft,
            observed_fight: 0,
            observed_match: 0,
            flow,
        })
    }
    pub fn step(&mut self, inputs: &[PlayerInput]) {
        self.tick = self.tick.saturating_add(1);
        self.reload_arena_if_changed();
        self.reload_tuning_if_changed();
        let observation = self.snapshot();
        let inputs = inputs
            .iter()
            .enumerate()
            .map(|(player, input)| {
                input.with_progressive_observation(player as u8, Some(&observation))
            })
            .collect::<Vec<_>>();
        let commands = (0..self.player_entities.len())
            .map(|i| inputs.get(i).and_then(|input| input.flow))
            .collect::<Vec<_>>();
        self.flow.advance(&commands);
        let snapshot = self.flow.snapshot();
        let entering_combat =
            snapshot.phase == FlowPhase::Combat && self.phase != FlowPhase::Combat;
        self.phase = snapshot.phase;
        if entering_combat {
            self.observed_fight = snapshot.fight_number;
            self.observed_match = snapshot.match_number;
            self.reset_fight();
        }
        if !self.flow.accepts_combat() {
            return;
        }
        for index in 0..self.player_entities.len() {
            let input = inputs.get(index).copied().unwrap_or_default().validated();
            let entity = self.player_entities[index];
            let capabilities = self.flow.capabilities(index as u8);
            let mut fire = None;
            {
                let mut actor = self.world.entity_mut(entity);
                let mut state = actor.get_mut::<PlayerState>().expect("player state");
                state.fire_cooldown = state.fire_cooldown.saturating_sub(1);
                state.block_ticks = state.block_ticks.saturating_sub(1);
                state.block_cooldown = state.block_cooldown.saturating_sub(1);
                if state.reload_ticks > 0 {
                    state.reload_ticks -= 1;
                    if state.reload_ticks == 0 {
                        state.ammunition = self.tuning.magazine_size;
                    }
                }
                state.hit_flash_ticks = state.hit_flash_ticks.saturating_sub(1);
                state.stun_ticks = state.stun_ticks.saturating_sub(1);
                if state.stun_pulses_remaining > 0 {
                    state.stun_ticks = state.stun_ticks.max(6);
                    state.hit_flash_ticks = 4;
                    state.stun_pulses_remaining -= 1;
                    self.metrics.dazzle_stun_pulses += 1;
                }
                let jump_pressed = input.jump && !state.jump_held;
                let block_pressed = input.block && !state.block_held;
                state.block_held = input.block;
                let released = state.jump_held && !input.jump;
                state.jump_held = input.jump;
                let acts = state.alive && state.health > 0;
                if acts && (input.aim_x != 0 || input.aim_y != 0) {
                    state.aim = Vector::new(f32::from(input.aim_x), f32::from(input.aim_y))
                        .normalize_or_zero();
                }
                if acts && block_pressed && state.block_cooldown == 0 {
                    state.block_ticks = state.block_ticks.max(self.tuning.block_duration_ticks);
                    state.block_cooldown = self.tuning.block_cooldown_ticks;
                    self.metrics.block_activations += 1;
                }
                if acts
                    && state.stun_ticks == 0
                    && self.physics.set_player_control(
                        state.id,
                        input,
                        state.jump_available,
                        jump_pressed,
                        released,
                        capabilities.movement_bonus,
                    )
                {
                    self.metrics.jumps += 1;
                    state.grounded = false;
                    state.jump_available = false;
                }
                if acts
                    && state.stun_ticks == 0
                    && input.fire
                    && state.fire_cooldown == 0
                    && state.reload_ticks == 0
                    && state.ammunition > 0
                {
                    state.fire_cooldown = self
                        .tuning
                        .fire_cooldown_ticks
                        .saturating_add(capabilities.fire_cooldown_extra_ticks);
                    state.ammunition -= 1;
                    if state.ammunition == 0 {
                        state.reload_ticks = self.tuning.reload_ticks;
                    }
                    fire = Some((state.id, state.aim));
                }
            }
            if let Some((owner, aim)) = fire {
                let id = self.next_projectile_id;
                self.next_projectile_id += 1;
                self.physics.spawn_bullet(
                    id,
                    owner,
                    aim,
                    self.tuning.bullet_speed
                        * f32::from(capabilities.projectile_speed_factor.milli)
                        / 1000.0,
                );
                self.physics.recoil(owner, aim);
                let entity = self
                    .world
                    .spawn(ProjectileState {
                        id,
                        owner,
                        dazzle_pulses: capabilities.dazzle_stun_pulses,
                        dazzle_stun_ticks: capabilities.dazzle_stun_ticks,
                        explosive_radius_milli: capabilities.explosion_radius_milli,
                    })
                    .id();
                self.projectile_entities.insert(id, entity);
                self.metrics.shots_fired += 1;
                if self.tuning.recoil_enabled {
                    self.metrics.recoil_impulses += 1;
                }
            }
        }
        self.physics.update_arena_motion(&self.arena, self.tick);
        self.physics.step();
        for (index, entity) in self.player_entities.iter().copied().enumerate() {
            let (grounded, wall) = self.physics.player_support(index as u8);
            if grounded {
                self.metrics.platform_contact_ticks += 1;
            }
            let mut actor = self.world.entity_mut(entity);
            let mut state = actor.get_mut::<PlayerState>().expect("player state");
            state.grounded = grounded;
            if grounded || wall != 0.0 {
                state.jump_available = true;
            }
        }
        self.resolve_projectiles();
        self.apply_object_damage();
        for index in 0..self.player_entities.len() {
            let state = *self
                .world
                .entity(self.player_entities[index])
                .get::<PlayerState>()
                .expect("player state");
            if state.alive
                && self.physics.return_from_edge(
                    index as u8,
                    self.arena.frame,
                    state.block_ticks > 0,
                )
            {
                if state.block_ticks == 0 {
                    self.damage_fighter(index as u8, self.tuning.edge_damage)
                        .expect("configured fighter");
                }
                self.metrics.ring_outs += 1;
            }
        }
        let alive = self
            .player_entities
            .iter()
            .map(|entity| {
                self.world
                    .entity(*entity)
                    .get::<PlayerState>()
                    .is_some_and(|state| state.alive)
            })
            .collect::<Vec<_>>();
        self.flow.record_survivors(&alive);
    }
    /// Applies ordinary damage, including periodic effect ticks. Blocking only
    /// intercepts shots and edge impacts at their respective contact boundaries.
    /// Returns true when this application eliminates a living fighter.
    pub fn damage_fighter(&mut self, fighter: u8, damage: u16) -> Result<bool, String> {
        let entity = *self
            .player_entities
            .get(usize::from(fighter))
            .ok_or("fighter is outside the match")?;
        let mut actor = self.world.entity_mut(entity);
        let mut state = actor.get_mut::<PlayerState>().expect("player state");
        if !state.alive {
            return Ok(false);
        }
        state.health = state.health.saturating_sub(damage);
        state.hit_flash_ticks = self.tuning.hit_flash_ticks;
        state.alive = state.health > 0;
        Ok(!state.alive)
    }
    fn apply_object_damage(&mut self) {
        for (_, fighter, damage) in self.physics.object_contacts() {
            self.damage_fighter(fighter, damage)
                .expect("physics fighter belongs to this match");
        }
    }
    fn resolve_projectiles(&mut self) {
        let mut removals = Vec::new();
        for (id, entity) in self
            .projectile_entities
            .iter()
            .map(|(&id, &entity)| (id, entity))
            .collect::<Vec<_>>()
        {
            let projectile = *self
                .world
                .entity(entity)
                .get::<ProjectileState>()
                .expect("projectile state");
            let target = self
                .player_entities
                .iter()
                .enumerate()
                .filter(|(target, _)| *target != usize::from(projectile.owner))
                .find_map(|(target, entity)| {
                    self.world
                        .entity(*entity)
                        .get::<PlayerState>()
                        .filter(|state| state.alive)
                        .and_then(|_| {
                            self.physics
                                .bullet_contact(id, target as u8)
                                .map(|point| (target as u8, point))
                        })
                });
            if let Some((target, position)) = target {
                let target_entity = self.player_entities[usize::from(target)];
                let blocking = self
                    .world
                    .entity(target_entity)
                    .get::<PlayerState>()
                    .expect("player state")
                    .block_ticks
                    > 0;
                if blocking {
                    self.physics.reflect_bullet(id, target);
                    self.world
                        .entity_mut(target_entity)
                        .get_mut::<PlayerState>()
                        .unwrap()
                        .block_ticks = self
                        .world
                        .entity(target_entity)
                        .get::<PlayerState>()
                        .unwrap()
                        .block_ticks
                        .saturating_add(self.tuning.block_extension_ticks);
                    let mut projectile_entity = self.world.entity_mut(entity);
                    projectile_entity
                        .get_mut::<ProjectileState>()
                        .expect("projectile state")
                        .owner = target;
                    self.metrics.reflections += 1;
                    continue;
                }
                let velocity = self
                    .physics
                    .bullet_pose(id)
                    .map(|(_, _, velocity, _)| velocity.normalize_or_zero())
                    .unwrap_or(Vector::X);
                let damage = self
                    .tuning
                    .damage_per_hit
                    .saturating_add(self.flow.capabilities(projectile.owner).damage_bonus);
                let eliminated = self
                    .damage_fighter(target, damage)
                    .expect("contact fighter");
                let knockback_scale;
                {
                    let mut target_actor = self.world.entity_mut(target_entity);
                    let mut state = target_actor.get_mut::<PlayerState>().expect("player state");
                    state.stun_ticks = state.stun_ticks.max(projectile.dazzle_stun_ticks);
                    state.stun_pulses_remaining = projectile.dazzle_pulses;
                    knockback_scale = 1.0
                        + f32::from(100u16.saturating_sub(state.health))
                            / self.tuning.knockback_health_scale;
                }
                self.physics
                    .apply_impulse(target, velocity * self.tuning.hit_impulse * knockback_scale);
                self.metrics.hits += 1;
                self.metrics.health_scaled_knockbacks += 1;
                self.impacts.push(ImpactSnapshot {
                    id,
                    tick: self.tick,
                    owner: projectile.owner,
                    target: Some(target),
                    x_milli: quantize(position.x),
                    y_milli: quantize(position.y),
                    damage,
                    eliminated,
                    impulse_x_milli: quantize(
                        velocity.x * self.tuning.hit_impulse * knockback_scale,
                    ),
                    impulse_y_milli: quantize(
                        velocity.y * self.tuning.hit_impulse * knockback_scale,
                    ),
                });
                removals.push(id);
            } else if let Some((piece, impulse)) = self.physics.bullet_object_contact(id) {
                self.damage_arena_piece(piece, self.tuning.shot_object_damage);
                if self.physics.object_pose(piece).is_some() {
                    // Consume the projectile while preserving its absorbed momentum.
                    self.physics.apply_object_impulse(piece, impulse);
                }
                removals.push(id);
            } else if self.physics.bullet_platform_contact(id)
                || self
                    .physics
                    .bullet_pose(id)
                    .is_none_or(|(_, _, _, life)| life == 0)
            {
                removals.push(id);
            }
        }
        for id in removals {
            self.physics.remove_bullet(id);
            if let Some(entity) = self.projectile_entities.remove(&id) {
                self.world.despawn(entity);
            }
        }
    }
    fn reset_fight(&mut self) {
        if self.arena_index + 1 == self.arena_bag.len() {
            let previous = self.arena.name.clone();
            SeededRandom(self.seed ^ u64::from(self.observed_fight)).shuffle(&mut self.arena_bag);
            if self.arena_bag.len() > 1 && self.arena_bag[0].name == previous {
                self.arena_bag.rotate_left(1);
            }
            self.arena_index = 0;
        } else {
            self.arena_index += 1;
        }
        self.arena = self.arena_bag[self.arena_index].clone();
        self.arena_path = self.arena_files.get(&self.arena.name).cloned();
        self.arena_source = self
            .arena_sources
            .get(&self.arena.name)
            .cloned()
            .unwrap_or_default();
        self.physics.replace_arena(&self.arena);
        self.clear_projectiles();
        self.impacts.clear();
        self.reset_players();
    }
    fn reset_players(&mut self) {
        for entity in &self.player_entities {
            let cap = self
                .flow
                .capabilities(self.world.entity(*entity).get::<PlayerState>().unwrap().id);
            let mut actor = self.world.entity_mut(*entity);
            let mut state = actor.get_mut::<PlayerState>().unwrap();
            state.health = 100u16.saturating_add(cap.health_bonus);
            state.alive = true;
            state.fire_cooldown = 0;
            state.block_ticks = 0;
            state.hit_flash_ticks = 0;
            state.stun_ticks = 0;
            state.stun_pulses_remaining = 0;
            state.jump_held = false;
            state.jump_available = true;
            state.grounded = false;
            state.block_held = false;
            state.block_cooldown = 0;
            state.ammunition = self.tuning.magazine_size;
            state.reload_ticks = 0;
        }
    }
    fn prepare_definition(mut definition: ArenaDefinition) -> Result<ArenaDefinition, String> {
        for body in &definition.legacy_bodies {
            let id = body
                .id
                .checked_add(1000)
                .ok_or("legacy body ID exceeds preview namespace")?;
            definition.objects.push(ArenaObject {
                id,
                position: body.position,
                rotation: body.rotation,
                shape: if body.shape == DynamicBodyShape::Weight {
                    ArenaShape::Circle {
                        radius: body.radius,
                    }
                } else {
                    ArenaShape::Rectangle {
                        size: [body.width, body.height],
                    }
                },
                kind: ArenaKind::Loose,
                color: body.face_rgb,
                mass: body.mass,
                health: None,
                motion: None,
            });
            if body.shape == DynamicBodyShape::Weight {
                definition.chains.push(ArenaChain {
                    id,
                    body_a: None,
                    body_b: id,
                    anchor: [body.position[0], 330.0],
                    length: 330.0 - body.position[1],
                });
            }
        }
        for saw in &definition.legacy_saws {
            let id = saw
                .id
                .checked_add(1000)
                .ok_or("legacy saw ID exceeds preview namespace")?;
            definition.objects.push(ArenaObject {
                id,
                position: saw.position,
                rotation: saw.initial_angle,
                shape: ArenaShape::Circle { radius: saw.radius },
                kind: ArenaKind::Saw {
                    loose: false,
                    teeth: saw.teeth,
                    angular_velocity: saw.angular_velocity,
                },
                color: [230, 230, 218],
                mass: 1.0,
                health: None,
                motion: None,
            });
        }
        definition.legacy_bodies.clear();
        definition.legacy_saws.clear();
        definition.validate()?;
        Ok(definition)
    }
    pub fn damage_arena_piece(&mut self, id: u16, damage: u16) -> bool {
        let Some(object) = self.arena.objects.iter_mut().find(|object| object.id == id) else {
            return false;
        };
        if !matches!(object.kind, ArenaKind::Breakable { .. }) {
            return false;
        }
        let health = object.health.as_mut().expect("validated breakable health");
        *health = health.saturating_sub(damage);
        if *health > 0 {
            return false;
        }
        self.physics.remove_object(id, &self.arena.chains);
        true
    }
    fn reload_arena_if_changed(&mut self) {
        let Some(path) = self.arena_path.clone() else {
            return;
        };
        if !self.tick.is_multiple_of(15) {
            return;
        }
        match std::fs::read_to_string(&path) {
            Ok(source) if source == self.arena_source => self.arena_reload_error = None,
            Ok(source) => {
                match ArenaDefinition::parse(&source).and_then(Self::prepare_definition) {
                    Ok(arena) => {
                        if self
                            .arena_files
                            .get(&arena.name)
                            .is_some_and(|other| *other != path)
                        {
                            self.arena_reload_error =
                                Some(format!("duplicate arena name {}", arena.name));
                            return;
                        }
                        // Name is editable metadata; the watched file keeps its identity.
                        self.arena_files.remove(&self.arena.name);
                        self.arena_sources.remove(&self.arena.name);
                        self.arena_files.insert(arena.name.clone(), path.clone());
                        self.arena = arena;
                        self.arena_bag[self.arena_index] = self.arena.clone();
                        self.arena_source = source;
                        self.arena_sources
                            .insert(self.arena.name.clone(), self.arena_source.clone());
                        self.clear_projectiles();
                        self.physics.replace_arena(&self.arena);
                        // Editing geometry preserves fighter health and elimination,
                        // as well as the match authority and its clocks.
                        self.arena_reload_error = None;
                    }
                    Err(error) => self.arena_reload_error = Some(error),
                }
            }
            Err(error) => self.arena_reload_error = Some(error.to_string()),
        }
    }
    /// Watch a tuning file without resetting health, timers, scores or drafted cards.
    pub fn watch_tuning_file(&mut self, path: &std::path::Path) -> Result<(), String> {
        let source = std::fs::read_to_string(path).map_err(|error| error.to_string())?;
        self.apply_tuning(CombatTuning::parse(&source)?);
        self.tuning_source = source;
        self.tuning_path = Some(path.to_owned());
        Ok(())
    }
    fn apply_tuning(&mut self, tuning: CombatTuning) {
        for entity in &self.player_entities {
            let mut actor = self.world.entity_mut(*entity);
            let mut state = actor.get_mut::<PlayerState>().unwrap();
            state.ammunition = state.ammunition.min(tuning.magazine_size);
        }
        self.physics.update_tuning(tuning.clone());
        self.tuning = tuning;
    }
    fn reload_tuning_if_changed(&mut self) {
        let Some(path) = &self.tuning_path else {
            return;
        };
        if !self.tick.is_multiple_of(15) {
            return;
        }
        let previous_error = self.tuning_reload_error.clone();
        match std::fs::read_to_string(path) {
            Ok(source) if source == self.tuning_source => self.tuning_reload_error = None,
            Ok(source) => match CombatTuning::parse(&source) {
                Ok(tuning) => {
                    self.apply_tuning(tuning);
                    self.tuning_source = source;
                    self.tuning_reload_error = None;
                }
                Err(error) => self.tuning_reload_error = Some(error),
            },
            Err(error) => self.tuning_reload_error = Some(error.to_string()),
        }
        if self.tuning_reload_error != previous_error
            && let Some(error) = &self.tuning_reload_error
        {
            eprintln!("tuning reload rejected: {error}");
        }
    }
    pub fn tuning(&self) -> &CombatTuning {
        &self.tuning
    }
    pub fn tuning_reload_error(&self) -> Option<&str> {
        self.tuning_reload_error.as_deref()
    }
    fn clear_projectiles(&mut self) {
        self.physics.clear_bullets();
        for entity in std::mem::take(&mut self.projectile_entities).into_values() {
            self.world.despawn(entity);
        }
    }
    pub fn arena_reload_error(&self) -> Option<&str> {
        self.arena_reload_error.as_deref()
    }
    pub fn arena_definition(&self) -> Option<&ArenaDefinition> {
        Some(&self.arena)
    }
    pub fn snapshot(&mut self) -> MatchSnapshot {
        let players = self
            .player_entities
            .iter()
            .enumerate()
            .map(|(index, entity)| {
                let state = *self.world.entity(*entity).get::<PlayerState>().unwrap();
                let (position, velocity) = self.physics.player_pose(index as u8);
                PlayerSnapshot {
                    id: state.id,
                    x_milli: quantize(position.x),
                    y_milli: quantize(position.y),
                    velocity_x_milli_per_second: quantize(velocity.x),
                    velocity_y_milli_per_second: quantize(velocity.y),
                    aim_x: quantize(state.aim.x) as i16,
                    aim_y: quantize(state.aim.y) as i16,
                    health: state.health,
                    fire_cooldown_ticks: state.fire_cooldown,
                    block_ticks: state.block_ticks,
                    hit_flash_ticks: state.hit_flash_ticks,
                    grounded: state.grounded,
                    alive: state.alive,
                    stun_ticks: state.stun_ticks,
                    ammunition: state.ammunition,
                    reload_ticks: state.reload_ticks,
                    block_cooldown_ticks: state.block_cooldown,
                    jump_available: state.jump_available,
                    radius_milli: quantize(self.tuning.player_radius),
                    height_milli: quantize(self.physics.player_half_height(index as u8) * 2.0),
                }
            })
            .collect();
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
        projectiles.sort_by_key(|p| p.id);
        projectiles.truncate(MAX_INSPECTED_PROJECTILES);
        MatchSnapshot {
            arena_objects: Some(ArenaRenderSnapshot {
                frame: self.arena.frame,
                objects: self
                    .arena
                    .objects
                    .iter()
                    .filter_map(|object| {
                        if matches!(object.kind, ArenaKind::Background) {
                            return Some(object.clone());
                        }
                        let (position, _, rotation, _) = self.physics.object_pose(object.id)?;
                        let mut rendered = object.clone();
                        rendered.position = position.to_array();
                        rendered.rotation = rotation;
                        Some(rendered)
                    })
                    .collect(),
                chains: self
                    .arena
                    .chains
                    .iter()
                    .filter(|chain| self.physics.chain_active(chain.id))
                    .cloned()
                    .collect(),
                spawns: self.arena.spawns.clone(),
            }),
            protocol: 12,
            seed: self.seed,
            tick: self.tick,
            arena: self.arena.surfaces.clone(),
            impacts: self.impacts.clone(),
            players,
            projectiles,
            metrics: self.metrics.clone(),
            winner: self.flow.snapshot().winner,
            flow: Some(self.flow.snapshot()),
        }
    }
    pub fn state_hash(&mut self) -> String {
        hash_snapshot(&self.snapshot())
    }
}

type ArenaFiles = (
    Vec<ArenaDefinition>,
    BTreeMap<String, std::path::PathBuf>,
    BTreeMap<String, String>,
);
fn arena_file_map() -> Result<ArenaFiles, String> {
    let directory = default_arena_directory();
    let mut paths = std::fs::read_dir(&directory)
        .map_err(|error| format!("{}: {error}", directory.display()))?
        .map(|entry| {
            entry
                .map(|entry| entry.path())
                .map_err(|error| error.to_string())
        })
        .collect::<Result<Vec<_>, _>>()?;
    paths.sort();
    let mut files = BTreeMap::new();
    let mut sources = BTreeMap::new();
    let mut arenas = Vec::new();
    for path in paths
        .into_iter()
        .filter(|path| path.extension().is_some_and(|extension| extension == "ron"))
    {
        let source = std::fs::read_to_string(&path)
            .map_err(|error| format!("{}: {error}", path.display()))?;
        let definition = ArenaDefinition::parse(&source)?;
        let name = definition.name.clone();
        if files.insert(name.clone(), path).is_some() {
            return Err(format!("duplicate arena name {name}"));
        }
        sources.insert(name, source);
        arenas.push(definition);
    }
    Ok((arenas, files, sources))
}

#[cfg(test)]
mod arena_runtime_tests {
    use super::*;
    use std::collections::BTreeSet;

    fn workshop() -> ArenaDefinition {
        ArenaDefinition::load(&default_arena_directory().join("all-kinds.ron")).unwrap()
    }
    fn enter_combat(game: &mut AuthoritativeMatch) {
        let flow = game.snapshot().flow.unwrap();
        let inputs = flow
            .offers
            .iter()
            .map(|offers| PlayerInput {
                flow: Some(FlowCommand {
                    phase_revision: flow.phase_revision,
                    action: FlowAction::Confirm(offers[0]),
                }),
                ..Default::default()
            })
            .collect::<Vec<_>>();
        game.step(&inputs);
        assert_eq!(game.snapshot().flow.unwrap().phase, FlowPhase::Combat);
    }

    #[test]
    fn chains_constrain_objects_and_breaking_an_endpoint_releases_them() {
        let mut game = AuthoritativeMatch::from_arena(77, workshop()).unwrap();
        enter_combat(&mut game);
        for _ in 0..120 {
            game.step(&[PlayerInput::default(); 2]);
        }
        let before = game.snapshot().arena_objects.unwrap();
        assert_eq!(before.chains.len(), 2);
        let linked = before
            .objects
            .iter()
            .find(|object| object.id == 6)
            .unwrap()
            .position[1];
        assert!(game.damage_arena_piece(5, 50));
        assert!(game.snapshot().arena_objects.unwrap().chains.is_empty());
        for _ in 0..10 {
            game.step(&[PlayerInput::default(); 2]);
        }
        let after = game.snapshot().arena_objects.unwrap();
        assert!(after.objects.iter().all(|object| object.id != 5));
        assert!(
            after
                .objects
                .iter()
                .find(|object| object.id == 6)
                .unwrap()
                .position[1]
                < linked
        );
    }

    #[test]
    fn snapshots_follow_dynamic_arena_geometry() {
        let mut game = AuthoritativeMatch::from_arena(77, workshop()).unwrap();
        enter_combat(&mut game);
        let initial = game
            .snapshot()
            .arena_objects
            .unwrap()
            .objects
            .into_iter()
            .find(|object| object.id == 10)
            .unwrap()
            .position;
        for _ in 0..30 {
            game.step(&[PlayerInput::default(); 2]);
        }
        let later = game
            .snapshot()
            .arena_objects
            .unwrap()
            .objects
            .into_iter()
            .find(|object| object.id == 10)
            .unwrap()
            .position;
        assert_ne!(initial, later);
    }

    #[test]
    fn ordinary_matches_keep_every_configured_fighter_in_the_public_state() {
        for fighter_count in [2, 3] {
            let config = MatchConfig {
                fighter_count,
                ..Default::default()
            };
            let mut game = AuthoritativeMatch::with_config(config).unwrap();
            game.step(&vec![PlayerInput::default(); fighter_count]);
            assert_eq!(game.snapshot().players.len(), fighter_count);
        }
    }

    #[test]
    fn arena_bag_uses_each_map_before_repeating() {
        let config = MatchConfig {
            seed: 91,
            ..Default::default()
        };
        let mut game = AuthoritativeMatch::with_config(config).unwrap();
        let count = game.arena_bag.len();
        let mut seen = BTreeSet::new();
        for _ in 0..count {
            game.reset_fight();
            assert!(seen.insert(game.arena.name.clone()));
        }
    }

    #[test]
    fn saws_and_fast_loose_pieces_damage_on_contact() {
        let mut saw_arena = workshop();
        saw_arena.objects.retain(|object| object.id == 8);
        saw_arena.chains.clear();
        saw_arena.objects[0].position = [-500.0, -240.0];
        let mut saw_game = AuthoritativeMatch::from_arena(77, saw_arena).unwrap();
        enter_combat(&mut saw_game);
        saw_game.step(&[PlayerInput::default(); 2]);
        assert!(saw_game.snapshot().players[0].health < 100);

        let mut loose_arena = workshop();
        loose_arena
            .objects
            .retain(|object| object.id == 0 || object.id == 3);
        loose_arena.chains.clear();
        loose_arena.objects[1].position = [-500.0, 100.0];
        loose_arena.spawns[0] = [-500.0, -248.0];
        let mut loose_game = AuthoritativeMatch::from_arena(77, loose_arena).unwrap();
        enter_combat(&mut loose_game);
        for _ in 0..60 {
            loose_game.step(&[PlayerInput::default(); 2]);
        }
        assert!(loose_game.snapshot().players[0].health < 100);
    }

    #[test]
    fn shots_destroy_breakables_and_remove_their_chains() {
        let mut arena = workshop();
        arena.objects.retain(|object| object.id == 5);
        arena.objects[0].position = [-350.0, -240.0];
        arena.objects[0].health = Some(25);
        arena.chains = vec![ArenaChain {
            id: 0,
            body_a: None,
            body_b: 5,
            anchor: [-350.0, 100.0],
            length: 340.0,
        }];
        let mut game = AuthoritativeMatch::from_arena(77, arena).unwrap();
        enter_combat(&mut game);
        for _ in 0..12 {
            game.step(&[
                PlayerInput {
                    fire: true,
                    aim_x: 1000,
                    ..Default::default()
                },
                PlayerInput::default(),
            ]);
        }
        let arena = game.snapshot().arena_objects.unwrap();
        assert!(arena.objects.is_empty());
        assert!(arena.chains.is_empty());
    }

    #[test]
    fn renamed_arena_keeps_watching_its_file_after_the_next_fight() {
        let path =
            std::env::temp_dir().join(format!("quarrel-renamed-arena-{}.ron", std::process::id()));
        let mut arena = workshop();
        arena.objects.retain(|object| object.id == 0);
        arena.chains.clear();
        std::fs::write(&path, ron::to_string(&arena).unwrap()).unwrap();
        let mut game = AuthoritativeMatch::from_arena_file(77, &path).unwrap();
        enter_combat(&mut game);
        arena.name = "Renamed workshop".into();
        arena.objects[0].color = [63, 171, 178];
        std::fs::write(&path, ron::to_string(&arena).unwrap()).unwrap();
        for _ in 0..2_400 {
            let state = game.snapshot();
            let flow = state.flow.as_ref().unwrap();
            if flow.phase == FlowPhase::Combat && flow.fight_number > 0 {
                break;
            }
            game.step(
                &(0..2)
                    .map(|id| automated_input(id, &state))
                    .collect::<Vec<_>>(),
            );
        }
        let next = game.snapshot();
        let next_flow = next.flow.as_ref().unwrap();
        arena.objects[0].color = [200, 10, 90];
        std::fs::write(&path, ron::to_string(&arena).unwrap()).unwrap();
        for _ in 0..15 {
            game.step(&[PlayerInput::default(); 2]);
        }
        let edited = game.snapshot();
        std::fs::remove_file(path).unwrap();
        assert_eq!(next_flow.phase, FlowPhase::Combat);
        assert!(next_flow.fight_number > 0);
        assert_eq!(
            edited.arena_objects.unwrap().objects[0].color,
            [200, 10, 90]
        );
        assert!(game.arena_reload_error().is_none());
    }

    #[test]
    fn live_reload_keeps_the_match_authority_and_rejects_bad_geometry() {
        let path = std::env::temp_dir().join(format!("quarrel-arena-{}.ron", std::process::id()));
        let mut arena = workshop();
        std::fs::write(&path, ron::to_string(&arena).unwrap()).unwrap();
        let mut game = AuthoritativeMatch::from_arena_file(77, &path).unwrap();
        enter_combat(&mut game);
        game.world
            .entity_mut(game.player_entities[0])
            .get_mut::<PlayerState>()
            .unwrap()
            .health = 77;
        let before = game.snapshot();
        arena.objects[1].position[0] = -350.0;
        std::fs::write(&path, ron::to_string(&arena).unwrap()).unwrap();
        while !game.tick.is_multiple_of(15) {
            game.step(&[PlayerInput::default(); 2]);
        }
        game.step(&[PlayerInput::default(); 2]);
        let reloaded = game.snapshot();
        assert!(
            reloaded.players[0].health <= 77,
            "geometry edits must not heal fighters"
        );
        assert!(reloaded.tick > before.tick);
        assert_eq!(
            reloaded.flow.as_ref().unwrap().phase,
            before.flow.as_ref().unwrap().phase
        );
        assert_eq!(
            reloaded.flow.as_ref().unwrap().scores,
            before.flow.as_ref().unwrap().scores
        );
        assert_eq!(
            reloaded.flow.as_ref().unwrap().loadouts,
            before.flow.as_ref().unwrap().loadouts
        );
        assert_eq!(
            reloaded
                .arena_objects
                .unwrap()
                .objects
                .iter()
                .find(|object| object.id == 1)
                .unwrap()
                .position[0],
            -350.0
        );
        std::fs::write(&path, "(").unwrap();
        for _ in 0..15 {
            game.step(&[PlayerInput::default(); 2]);
        }
        assert!(game.arena_reload_error().is_some());
        std::fs::remove_file(path).unwrap();
    }
}

#[cfg(test)]
mod mechanics_tests;
