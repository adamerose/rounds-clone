use super::*;
use std::{
    collections::BTreeSet,
    path::{Path, PathBuf},
};

struct PiecePhysics {
    body: RigidBodyHandle,
    collider: ColliderHandle,
    before_velocity: Vector,
}

pub(super) struct ArenaRuntime {
    pub(super) definition: ArenaDefinition,
    path: Option<PathBuf>,
    source: String,
    pieces: BTreeMap<u16, PiecePhysics>,
    anchors: Vec<RigidBodyHandle>,
    chains: BTreeMap<u16, ImpulseJointHandle>,
    contact_pairs: BTreeSet<(u16, u8)>,
    age: u32,
}
#[derive(Clone)]
pub(super) struct LegacyArenaWatch {
    pub(super) file: &'static str,
    pub(super) definition: ArenaDefinition,
    source: String,
    path: PathBuf,
}

impl AuthoritativeMatch {
    pub(super) fn active_legacy_arena_filename(&self) -> &'static str {
        match self.arena_stage {
            ArenaStage::Timber => "timber.ron",
            ArenaStage::Ice => "ice.ron",
            ArenaStage::HangingEntry => "lime.ron",
            ArenaStage::Profile => {
                if self.profile == ReplayProfile::RematchDraftReplay
                    && self.flow.as_ref().is_some_and(|f| {
                        matches!(
                            f.snapshot().phase,
                            FlowPhase::CombatConclusion | FlowPhase::RematchPrompt
                        )
                    })
                {
                    "prior-match.ron"
                } else {
                    profile_arena_filename(self.profile)
                }
            }
        }
    }
    pub(super) fn watch_legacy_arena(&mut self) {
        if self.arena_runtime.is_some() {
            return;
        }
        let file = self.active_legacy_arena_filename();
        self.read_legacy_arena(file);
        self.legacy_arena_watch = Some(self.legacy_arena_cache[file].clone());
    }
    pub(super) fn cache_legacy_arenas(&mut self) {
        let directory = default_arena_directory();
        let mut files = BTreeSet::from([profile_arena_filename(self.profile)]);
        if self.flow.is_some() {
            files.extend([
                "draft.ron",
                "prior-match.ron",
                "timber.ron",
                "ice.ron",
                "lime.ron",
            ]);
        }
        for file in files {
            let path = directory.join(file);
            let source = std::fs::read_to_string(&path).expect("initial profile arenas must read");
            let definition =
                ArenaDefinition::parse(&source).expect("initial profile arenas must parse");
            self.legacy_arena_cache.insert(
                file,
                LegacyArenaWatch {
                    file,
                    definition,
                    source,
                    path,
                },
            );
        }
    }
    pub(super) fn read_legacy_arena(&mut self, file: &'static str) -> ArenaDefinition {
        let cached = &self.legacy_arena_cache[file];
        let result = std::fs::read_to_string(&cached.path)
            .map_err(|e| e.to_string())
            .and_then(|source| {
                ArenaDefinition::parse(&source).map(|definition| (definition, source))
            });
        match result {
            Ok((definition, source)) => {
                let cached = self.legacy_arena_cache.get_mut(file).unwrap();
                cached.definition = definition;
                cached.source = source;
                self.arena_reload_error = None;
            }
            Err(error) => self.arena_reload_error = Some(error),
        }
        self.legacy_arena_cache[file].definition.clone()
    }
    pub fn from_arena_file(seed: u64, path: &Path) -> Result<Self, String> {
        let source =
            std::fs::read_to_string(path).map_err(|e| format!("{}: {e}", path.display()))?;
        let definition = ArenaDefinition::parse(&source)?;
        let mut game = Self::from_arena(seed, definition)?;
        let runtime = game.arena_runtime.as_mut().unwrap();
        runtime.path = Some(path.to_owned());
        runtime.source = source;
        Ok(game)
    }
    pub fn from_arena(seed: u64, definition: ArenaDefinition) -> Result<Self, String> {
        definition.validate()?;
        let definition = Self::prepare_definition(definition)?;
        let mut game = Self::create(seed, ReplayProfile::TealDuelReplay, Some(&definition));
        game.install_arena(definition, None, String::new());
        Ok(game)
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
        definition.validate()?;
        Ok(definition)
    }
    /// The last failed reload remains visible; invalid edits leave the current arena intact.
    pub fn arena_reload_error(&self) -> Option<&str> {
        self.arena_reload_error.as_deref()
    }
    pub fn arena_definition(&self) -> Option<&ArenaDefinition> {
        self.arena_runtime.as_ref().map(|r| &r.definition)
    }

    fn install_arena(
        &mut self,
        definition: ArenaDefinition,
        path: Option<PathBuf>,
        source: String,
    ) {
        self.clear_projectiles();
        self.impacts.clear();
        self.explosions.clear();
        self.pending_radial_hit = None;
        self.arena_entry_from_milli = None;
        self.clear_arena_runtime();
        for collider in self.physics.platforms.drain(..) {
            if let Some(body) = self
                .physics
                .rapier
                .colliders
                .get(collider)
                .and_then(|c| c.parent())
            {
                self.physics.rapier.remove_body(body);
            }
        }
        self.install_arena_geometry(definition, path, source);
    }
    pub(super) fn clear_arena_runtime(&mut self) {
        if let Some(old) = self.arena_runtime.take() {
            for piece in old.pieces.into_values() {
                self.physics.rapier.remove_body(piece.body);
            }
            for anchor in old.anchors {
                self.physics.rapier.remove_body(anchor);
            }
            self.physics
                .platforms
                .retain(|handle| self.physics.rapier.colliders.contains(*handle));
            self.physics.rapier.integration_parameters.length_unit = 1.0;
        }
    }
    fn install_arena_geometry(
        &mut self,
        definition: ArenaDefinition,
        path: Option<PathBuf>,
        source: String,
    ) {
        let physics = &mut self.physics;
        // Arena coordinates are pixels. Rapier scales its contact tolerances and
        // joint correction speeds from metres; use 100 world units per metre.
        physics.rapier.integration_parameters.length_unit = 100.0;
        let mut runtime = ArenaRuntime {
            definition,
            path,
            source,
            pieces: BTreeMap::new(),
            anchors: vec![],
            chains: BTreeMap::new(),
            contact_pairs: BTreeSet::new(),
            age: 0,
        };
        for surface in &runtime.definition.surfaces {
            let (_, collider) = physics.rapier.insert(
                RigidBodyBuilder::fixed().translation(Vector::new(
                    surface.center_x_milli as f32 / 1000.0,
                    surface.center_y_milli as f32 / 1000.0,
                )),
                collider_for_surface(surface)
                    .rotation(surface.rotation_milliradians as f32 / 1000.0)
                    .friction(0.92)
                    .restitution(0.02)
                    .collision_groups(groups(Group::GROUP_3, Group::ALL)),
            );
            physics.platforms.push(collider);
        }
        for object in &runtime.definition.objects {
            if matches!(object.kind, ArenaKind::Background) {
                continue;
            }
            let loose = matches!(
                object.kind,
                ArenaKind::Loose | ArenaKind::Saw { loose: true, .. }
            ) || matches!(object.kind, ArenaKind::Breakable { loose: true });
            let kinematic = object.motion.is_some()
                || matches!(object.kind, ArenaKind::Saw { loose: false, .. });
            let builder = if kinematic {
                RigidBodyBuilder::kinematic_position_based()
            } else if loose {
                RigidBodyBuilder::dynamic()
            } else {
                RigidBodyBuilder::fixed()
            };
            let (body, collider) = physics.rapier.insert(
                builder
                    .translation(Vector::from(object.position))
                    .rotation(object.rotation)
                    .ccd_enabled(true),
                object
                    .shape
                    .collider()
                    .mass(object.mass)
                    .friction(0.8)
                    .restitution(0.08)
                    .collision_groups(groups(
                        if loose { DYNAMIC_GROUP } else { Group::GROUP_3 },
                        Group::ALL,
                    )),
            );
            runtime.pieces.insert(
                object.id,
                PiecePhysics {
                    body,
                    collider,
                    before_velocity: Vector::ZERO,
                },
            );
            // Existing bullet and grounded queries operate over this collider list.
            physics.platforms.push(collider);
        }
        for chain in &runtime.definition.chains {
            let a = if let Some(id) = chain.body_a {
                runtime.pieces[&id].body
            } else {
                let body = physics
                    .rapier
                    .insert_body(RigidBodyBuilder::fixed().translation(Vector::from(chain.anchor)));
                runtime.anchors.push(body);
                body
            };
            let handle = physics.rapier.impulse_joints.insert(
                a,
                runtime.pieces[&chain.body_b].body,
                RopeJointBuilder::new(chain.length),
                true,
            );
            runtime.chains.insert(chain.id, handle);
        }
        physics.spawns = std::array::from_fn(|i| {
            Vector::from(runtime.definition.spawns[i.min(runtime.definition.spawns.len() - 1)])
        });
        physics.respawn_players();
        self.revive_fighters();
        self.winner = None;
        self.arena_runtime = Some(runtime);
    }
    pub(super) fn reload_arena_if_changed(&mut self) {
        let Some(runtime) = &self.arena_runtime else {
            let file = self.active_legacy_arena_filename();
            if self
                .legacy_arena_watch
                .as_ref()
                .is_none_or(|w| w.file != file)
            {
                let previous_source = self.legacy_arena_cache[file].source.clone();
                self.watch_legacy_arena();
                // Later stages already install their cached definition in the
                // stage loader. Rebuilding again would cancel their entry state.
                if self.arena_stage == ArenaStage::Profile
                    && (self.legacy_arena_edited
                        || self.legacy_arena_cache[file].source != previous_source)
                {
                    self.reload_legacy_geometry(self.legacy_arena_cache[file].definition.clone());
                    self.legacy_arena_edited = true;
                }
                return;
            }
            // Live edits need prompt reloads, not filesystem reads on every physics tick.
            if !self.tick.is_multiple_of(15) {
                return;
            }
            match std::fs::read_to_string(&self.legacy_arena_watch.as_ref().unwrap().path) {
                Ok(source)
                    if self
                        .legacy_arena_watch
                        .as_ref()
                        .is_some_and(|w| w.source != source) =>
                {
                    match ArenaDefinition::parse(&source) {
                        Ok(definition) => {
                            let cached = self.legacy_arena_cache.get_mut(file).unwrap();
                            cached.definition = definition.clone();
                            cached.source = source;
                            self.legacy_arena_watch = Some(cached.clone());
                            self.reload_legacy_geometry(definition);
                            self.legacy_arena_edited = true;
                            self.arena_reload_error = None;
                        }
                        Err(error) => {
                            self.arena_reload_error = Some(error);
                        }
                    }
                }
                Ok(_) => {
                    self.arena_reload_error = None;
                }
                Err(error) => {
                    self.arena_reload_error = Some(error.to_string());
                }
            }
            return;
        };
        let Some(path) = runtime.path.clone() else {
            return;
        };
        if !self.tick.is_multiple_of(15) {
            return;
        }
        let result = std::fs::read_to_string(&path)
            .map_err(|e| e.to_string())
            .and_then(|source| {
                if source == runtime.source {
                    return Ok(None);
                }
                ArenaDefinition::parse(&source)
                    .and_then(Self::prepare_definition)
                    .map(|definition| Some((definition, source)))
            });
        match result {
            Ok(Some((definition, source))) => {
                self.install_arena(definition, Some(path), source);
                self.arena_reload_error = None;
            }
            Ok(None) => {
                self.arena_reload_error = None;
            }
            Err(error) => {
                self.arena_reload_error = Some(error);
            }
        }
    }
    fn reload_legacy_geometry(&mut self, definition: ArenaDefinition) {
        // Replace actors and physics only. Keep the replay's flow, clock,
        // presentation route and joint behavior through an arena edit.
        let profile = match self.arena_stage {
            ArenaStage::Timber => ReplayProfile::TimberCollapseReplay,
            ArenaStage::Ice => ReplayProfile::RematchDraftReplay,
            _ => self.profile,
        };
        let fresh = Self::create(self.seed, profile, Some(&definition));
        self.world = fresh.world;
        self.physics = fresh.physics;
        self.player_entities = fresh.player_entities;
        self.projectile_entities = fresh.projectile_entities;
        self.dynamic_body_entities = fresh.dynamic_body_entities;
        self.constraint_entities = fresh.constraint_entities;
        self.saw_entities = fresh.saw_entities;
        if self.arena_stage == ArenaStage::Ice {
            for player in self.physics.players {
                self.physics.rapier.colliders[player.collider].set_shape(SharedShape::ball(12.0));
            }
        } else if self.arena_stage == ArenaStage::HangingEntry {
            self.physics.load_held_hanging_entry();
        }
        self.impacts.clear();
        self.explosions.clear();
        self.pending_radial_hit = None;
        self.arena_entry_from_milli = None;
        self.revive_fighters();
        self.winner = None;
    }
    pub(super) fn prepare_arena_motion(&mut self) {
        let Some(runtime) = &mut self.arena_runtime else {
            return;
        };
        runtime.age += 1;
        let time = runtime.age as f32 / TICKS_PER_SECOND as f32;
        for object in &runtime.definition.objects {
            let Some(piece) = runtime.pieces.get_mut(&object.id) else {
                continue;
            };
            let body = &mut self.physics.rapier.bodies[piece.body];
            piece.before_velocity = body.linvel();
            let saw_speed = match object.kind {
                ArenaKind::Saw {
                    angular_velocity, ..
                } => angular_velocity,
                _ => 0.0,
            };
            if matches!(object.kind, ArenaKind::Saw { loose: true, .. }) {
                body.set_angvel(saw_speed, true);
            }
            if object.motion.is_some() || matches!(object.kind, ArenaKind::Saw { loose: false, .. })
            {
                let mut position = Vector::from(object.position);
                let mut angle = object.rotation + saw_speed * time;
                if let Some(motion) = &object.motion {
                    if !motion.path.is_empty() {
                        let phase =
                            (time / motion.period_seconds).fract() * motion.path.len() as f32;
                        let index = phase.floor() as usize;
                        let t = phase.fract();
                        position += Vector::from(motion.path[index]) * (1.0 - t)
                            + Vector::from(motion.path[(index + 1) % motion.path.len()]) * t;
                    }
                    angle += motion.angular_velocity * time;
                }
                body.set_next_kinematic_translation(position);
                body.set_next_kinematic_rotation(Rotation::new(angle));
            }
        }
    }
    pub(super) fn apply_arena_contacts(&mut self) {
        let Some(runtime) = &self.arena_runtime else {
            return;
        };
        let mut damage = Vec::new();
        let mut pairs = BTreeSet::new();
        for object in &runtime.definition.objects {
            let Some(piece) = runtime.pieces.get(&object.id) else {
                continue;
            };
            for id in 0..2u8 {
                let player = self.physics.players[usize::from(id)];
                if self
                    .physics
                    .rapier
                    .contact_pair(piece.collider, player.collider)
                    .is_some_and(|p| p.has_any_active_contact())
                {
                    pairs.insert((object.id, id));
                    if !runtime.contact_pairs.contains(&(object.id, id)) {
                        let speed = (piece.before_velocity
                            - self.physics.rapier.bodies[player.body].linvel())
                        .length();
                        let amount = if matches!(object.kind, ArenaKind::Saw { .. }) {
                            35
                        } else if self.physics.rapier.bodies[piece.body].is_dynamic()
                            && speed > 180.0
                        {
                            ((speed - 180.0) * object.mass * 0.15).clamp(1.0, 100.0) as u16
                        } else {
                            0
                        };
                        if amount > 0 {
                            damage.push((id, amount));
                        }
                    }
                }
            }
        }
        self.arena_runtime.as_mut().unwrap().contact_pairs = pairs;
        for (id, amount) in damage {
            let mut entity = self.world.entity_mut(self.player_entities[usize::from(id)]);
            let mut state = entity.get_mut::<PlayerState>().unwrap();
            if state.alive {
                state.health = state.health.saturating_sub(amount);
                state.hit_flash_ticks = 6;
                if state.health == 0 {
                    state.alive = false;
                }
            }
        }
        // Sorted projectile ids make simultaneous destruction deterministic.
        let mut hits = Vec::new();
        for (&id, bullet) in &self.physics.bullets {
            for (&piece_id, piece) in &self.arena_runtime.as_ref().unwrap().pieces {
                if self
                    .physics
                    .rapier
                    .contact_pair(bullet.collider, piece.collider)
                    .is_some_and(|p| p.has_any_active_contact())
                {
                    hits.push((
                        id,
                        piece_id,
                        bullet.previous_velocity
                            * self.physics.rapier.colliders[bullet.collider].mass(),
                    ));
                    break;
                }
            }
        }
        for (bullet, id, impulse) in hits {
            self.damage_arena_piece(id, 25);
            // Gameplay consumes CCD projectile contacts before a later solver
            // step can transfer their momentum. Apply that absorbed momentum here.
            if let Some(piece) = self.arena_runtime.as_ref().unwrap().pieces.get(&id) {
                self.physics.rapier.bodies[piece.body].apply_impulse(impulse, true);
            }
            self.physics.remove_bullet(bullet);
            if let Some(entity) = self.projectile_entities.remove(&bullet) {
                self.world.despawn(entity);
            }
        }
    }
    pub fn damage_arena_piece(&mut self, id: u16, damage: u16) -> bool {
        let Some(runtime) = &mut self.arena_runtime else {
            return false;
        };
        let Some(object) = runtime.definition.objects.iter_mut().find(|o| o.id == id) else {
            return false;
        };
        if !matches!(object.kind, ArenaKind::Breakable { .. }) {
            return false;
        }
        let health = object.health.as_mut().unwrap();
        *health = health.saturating_sub(damage);
        if *health > 0 {
            return false;
        }
        if let Some(piece) = runtime.pieces.remove(&id) {
            self.physics.platforms.retain(|c| *c != piece.collider);
            for chain in &runtime.definition.chains {
                if (chain.body_a == Some(id) || chain.body_b == id)
                    && let Some(handle) = runtime.chains.remove(&chain.id)
                {
                    self.physics.rapier.impulse_joints.remove(handle, true);
                }
            }
            self.physics.rapier.remove_body(piece.body);
        }
        true
    }
    pub(super) fn arena_render_snapshot(&self) -> Option<ArenaRenderSnapshot> {
        let runtime = self.arena_runtime.as_ref()?;
        let objects = runtime
            .definition
            .objects
            .iter()
            .filter_map(|object| {
                let mut object = object.clone();
                if matches!(object.kind, ArenaKind::Background) {
                    return Some(object);
                }
                let piece = runtime.pieces.get(&object.id)?;
                let body = &self.physics.rapier.bodies[piece.body];
                object.position = body.translation().to_array();
                object.rotation = body.rotation().angle();
                Some(object)
            })
            .collect();
        Some(ArenaRenderSnapshot {
            frame: runtime.definition.frame,
            objects,
            spawns: runtime.definition.spawns.clone(),
            chains: runtime
                .definition
                .chains
                .iter()
                .filter(|c| runtime.chains.contains_key(&c.id))
                .cloned()
                .collect(),
        })
    }
}
#[cfg(test)]
#[path = "data_arena/stage_flow_tests.rs"]
mod stage_flow_tests;
#[cfg(test)]
mod tests {
    use super::*;
    fn workshop() -> ArenaDefinition {
        ArenaDefinition::load(&default_arena_directory().join("all-kinds.ron")).unwrap()
    }
    fn simple(objects: Vec<ArenaObject>) -> ArenaDefinition {
        let mut arena = workshop();
        arena.objects = objects;
        arena.chains.clear();
        arena
    }
    fn object(id: u16, position: [f32; 2], kind: ArenaKind) -> ArenaObject {
        ArenaObject {
            id,
            position,
            rotation: 0.0,
            shape: ArenaShape::Rectangle { size: [30.0, 30.0] },
            kind,
            color: [180, 120, 60],
            mass: 4.0,
            health: None,
            motion: None,
        }
    }
    #[test]
    fn every_arena_loads_and_all_kinds_run_ten_seconds() {
        assert!(
            !load_arena_directory(&default_arena_directory())
                .unwrap()
                .is_empty()
        );
        let mut game = AuthoritativeMatch::from_arena(77, workshop()).unwrap();
        for _ in 0..600 {
            game.step([PlayerInput::default(); 2]);
        }
        let snapshot = game.snapshot();
        let arena = snapshot.arena_objects.unwrap();
        assert_eq!(arena.chains.len(), 2);
        let a = arena.objects.iter().find(|o| o.id == 5).unwrap();
        let b = arena.objects.iter().find(|o| o.id == 6).unwrap();
        assert!(Vector::from(a.position).distance(Vector::new(-100.0, 220.0)) <= 131.0);
        assert!(
            Vector::from(a.position).distance(Vector::from(b.position)) <= 81.0,
            "a={:?} b={:?}",
            a.position,
            b.position
        );
        let moving = arena.objects.iter().find(|o| o.id == 10).unwrap();
        assert!(moving.position[0].abs() < 0.01 && (moving.position[1] + 150.0).abs() < 0.01);
    }
    #[test]
    fn breaking_a_support_releases_its_chain_and_never_breaks_other_chains() {
        let mut game = AuthoritativeMatch::from_arena(77, workshop()).unwrap();
        assert!(!game.damage_arena_piece(5, 49));
        assert_eq!(game.snapshot().arena_objects.unwrap().chains.len(), 2);
        assert!(game.damage_arena_piece(5, 1));
        let snapshot = game.snapshot().arena_objects.unwrap();
        assert!(snapshot.chains.is_empty());
        assert!(snapshot.objects.iter().all(|o| o.id != 5));
        let before = snapshot
            .objects
            .iter()
            .find(|o| o.id == 6)
            .unwrap()
            .position[1];
        for _ in 0..10 {
            game.step([PlayerInput::default(); 2]);
        }
        assert!(
            game.snapshot()
                .arena_objects
                .unwrap()
                .objects
                .iter()
                .find(|o| o.id == 6)
                .unwrap()
                .position[1]
                < before
        );
    }
    #[test]
    fn falling_piece_hurts_a_fighter() {
        let mut floor = object(0, [0.0, -285.0], ArenaKind::Solid);
        floor.shape = ArenaShape::Rectangle {
            size: [1200.0, 30.0],
        };
        let falling = object(1, [-500.0, 100.0], ArenaKind::Loose);
        let mut arena = simple(vec![floor, falling]);
        arena.spawns[0] = [-500.0, -248.0];
        let mut game = AuthoritativeMatch::from_arena(77, arena).unwrap();
        for _ in 0..60 {
            game.step([PlayerInput::default(); 2]);
        }
        assert!(game.snapshot().players[0].health < 100);
    }
    #[test]
    fn resting_piece_contacts_do_not_damage_and_shots_push_loose_pieces() {
        let mut floor = object(0, [0.0, -285.0], ArenaKind::Solid);
        floor.shape = ArenaShape::Rectangle {
            size: [1200.0, 30.0],
        };
        let resting = object(1, [-500.0, -209.0], ArenaKind::Loose);
        let mut arena = simple(vec![floor, resting]);
        arena.spawns[0] = [-500.0, -248.0];
        let mut game = AuthoritativeMatch::from_arena(77, arena).unwrap();
        for _ in 0..120 {
            game.step([PlayerInput::default(); 2]);
        }
        assert_eq!(game.snapshot().players[0].health, 100);
        assert!(
            game.arena_runtime
                .as_ref()
                .unwrap()
                .contact_pairs
                .contains(&(1, 0))
        );
        let mut game = AuthoritativeMatch::from_arena(
            77,
            simple(vec![object(1, [-410.0, -240.0], ArenaKind::Loose)]),
        )
        .unwrap();
        for _ in 0..10 {
            game.step([
                PlayerInput {
                    fire: true,
                    aim_x: 1000,
                    ..Default::default()
                },
                PlayerInput::default(),
            ]);
        }
        assert!(game.snapshot().arena_objects.unwrap().objects[0].position[0] > -410.0);
    }
    #[test]
    fn saws_hurt_and_backgrounds_do_not_collide_with_fighters_or_shots() {
        let mut saw = object(
            1,
            [-500.0, -240.0],
            ArenaKind::Saw {
                loose: false,
                teeth: 8,
                angular_velocity: 3.0,
            },
        );
        saw.shape = ArenaShape::Circle { radius: 30.0 };
        let mut game = AuthoritativeMatch::from_arena(77, simple(vec![saw])).unwrap();
        game.step([PlayerInput::default(); 2]);
        assert!(game.snapshot().players[0].health < 100);
        let background = object(2, [-430.0, -240.0], ArenaKind::Background);
        let empty = AuthoritativeMatch::from_arena(77, simple(vec![])).unwrap();
        let mut backdrop = AuthoritativeMatch::from_arena(77, simple(vec![background])).unwrap();
        let mut empty = empty;
        for _ in 0..10 {
            let input = [
                PlayerInput {
                    fire: true,
                    aim_x: 1000,
                    ..Default::default()
                },
                PlayerInput::default(),
            ];
            empty.step(input);
            backdrop.step(input);
        }
        assert_eq!(empty.snapshot().players, backdrop.snapshot().players);
        assert_eq!(
            empty.snapshot().projectiles,
            backdrop.snapshot().projectiles
        );
    }
    #[test]
    fn shots_break_pieces_at_zero_health() {
        let mut piece = object(1, [-410.0, -240.0], ArenaKind::Breakable { loose: false });
        piece.health = Some(25);
        let mut game = AuthoritativeMatch::from_arena(77, simple(vec![piece])).unwrap();
        for _ in 0..5 {
            game.step([
                PlayerInput {
                    fire: true,
                    aim_x: 1000,
                    ..Default::default()
                },
                PlayerInput::default(),
            ]);
        }
        assert!(game.snapshot().arena_objects.unwrap().objects.is_empty());
    }
    #[test]
    fn a_running_session_reloads_a_changed_file_and_rejects_invalid_edits() {
        let path =
            std::env::temp_dir().join(format!("quarrel-arena-reload-{}.ron", std::process::id()));
        let mut arena = workshop();
        std::fs::write(&path, ron::to_string(&arena).unwrap()).unwrap();
        let mut game = AuthoritativeMatch::from_arena_file(77, &path).unwrap();
        arena.objects[1].position[0] = -350.0;
        std::fs::write(&path, ron::to_string(&arena).unwrap()).unwrap();
        game.step([PlayerInput::default(); 2]);
        assert_eq!(
            game.snapshot().arena_objects.unwrap().objects[1].position[0],
            -350.0
        );
        std::fs::write(&path, "(").unwrap();
        for _ in 0..15 {
            game.step([PlayerInput::default(); 2]);
        }
        assert!(game.arena_reload_error().is_some());
        assert_eq!(
            game.snapshot().arena_objects.unwrap().objects[1].position[0],
            -350.0
        );
        std::fs::write(&path, ron::to_string(&arena).unwrap()).unwrap();
        for _ in 0..15 {
            game.step([PlayerInput::default(); 2]);
        }
        assert!(game.arena_reload_error().is_none());
        std::fs::remove_file(path).unwrap();
    }
    #[test]
    fn replay_reload_keeps_network_ticks_and_match_progress_in_each_stage() {
        for (file, phase) in [
            ("timber.ron", FlowPhase::TimberCombat),
            ("ice.ron", FlowPhase::IceCombat),
        ] {
            let profile = ReplayProfile::RematchDraftReplay;
            let mut game = AuthoritativeMatch::new_with_profile(41, profile);
            let path = std::env::temp_dir().join(format!(
                "quarrel-stage-reload-{}-{file}",
                std::process::id()
            ));
            let entry = game.legacy_arena_cache.get_mut(file).unwrap();
            std::fs::write(&path, &entry.source).unwrap();
            entry.path = path.clone();
            let scripts = scripted_inputs_for(profile, 41, 6000);
            for (zero, one) in scripts[0].iter().zip(&scripts[1]) {
                let inputs = [*zero, *one];
                let before = game.snapshot();
                if before.flow.as_ref().is_some_and(|f| f.phase == phase)
                    && before.tick.is_multiple_of(15)
                {
                    break;
                }
                game.step(std::array::from_fn(|p| {
                    inputs[p].with_progressive_observation(p as u8, Some(&before))
                }));
            }
            let before = game.snapshot();
            assert_eq!(before.flow.as_ref().unwrap().phase, phase);
            let mut definition = game.legacy_arena_cache[file].definition.clone();
            definition.surfaces[0].center_x_milli += 1000;
            std::fs::write(&path, ron::to_string(&definition).unwrap()).unwrap();
            game.step([PlayerInput::default(); 2]);
            let after = game.snapshot();
            assert_eq!(after.tick, before.tick + 1);
            assert_eq!(after.flow.as_ref().unwrap().phase, phase);
            assert_eq!(
                after.flow.as_ref().unwrap().scores,
                before.flow.as_ref().unwrap().scores
            );
            assert_eq!(
                after.flow.as_ref().unwrap().loadouts,
                before.flow.as_ref().unwrap().loadouts
            );
            assert_eq!(
                after.arena[0].center_x_milli,
                definition.surfaces[0].center_x_milli
            );
            assert!(after.arena_objects.is_none());
            if file == "timber.ron" {
                assert_eq!(after.dynamic_bodies.len(), definition.legacy_bodies.len());
                assert!(
                    after
                        .constraints
                        .iter()
                        .any(|c| c.kind == ConstraintKind::Fixed && c.active)
                );
            }
            assert!(game.arena_reload_error().is_none());
            for _ in 0..30 {
                game.step([PlayerInput::default(); 2]);
            }
            assert!(game.snapshot().tick > after.tick);
            game.load_held_hanging_entry();
            let held = game.snapshot();
            assert!(held.arena_objects.is_none());
            assert!(held.arena.is_empty());
            assert!(held.hanging_entry.is_some());
            std::fs::remove_file(path).unwrap();
        }
    }
    #[test]
    fn replay_edits_keep_draft_projection_and_follow_the_next_active_file() {
        let profile = ReplayProfile::RematchDraftReplay;
        let mut game = AuthoritativeMatch::new_with_profile(41, profile);
        let directory =
            std::env::temp_dir().join(format!("quarrel-profile-edits-{}", std::process::id()));
        std::fs::create_dir_all(&directory).unwrap();
        for file in ["prior-match.ron", "draft.ron"] {
            let entry = game.legacy_arena_cache.get_mut(file).unwrap();
            entry.path = directory.join(file);
            std::fs::write(&entry.path, &entry.source).unwrap();
        }
        game.legacy_arena_watch = Some(game.legacy_arena_cache["prior-match.ron"].clone());
        let mut prior = game.legacy_arena_cache["prior-match.ron"]
            .definition
            .clone();
        prior.surfaces[0].center_x_milli += 1000;
        std::fs::write(
            directory.join("prior-match.ron"),
            ron::to_string(&prior).unwrap(),
        )
        .unwrap();
        game.step([PlayerInput::default(); 2]);
        assert_eq!(game.snapshot().arena, prior.surfaces);
        let scripts = scripted_inputs_for(profile, 41, 3000);
        for (zero, one) in scripts[0].iter().zip(&scripts[1]) {
            let before = game.snapshot();
            if before.flow.as_ref().unwrap().phase == FlowPhase::Draft {
                break;
            }
            game.step(std::array::from_fn(|p| {
                [*zero, *one][p].with_progressive_observation(p as u8, Some(&before))
            }));
        }
        let draft = game.snapshot();
        assert_eq!(draft.flow.as_ref().unwrap().phase, FlowPhase::Draft);
        assert_eq!(
            draft.arena,
            game.legacy_arena_cache["draft.ron"].definition.surfaces
        );
        assert!(draft.arena_objects.is_none());
        while !game.tick.is_multiple_of(15) {
            game.step([PlayerInput::default(); 2]);
        }
        let mut definition = game.legacy_arena_cache["draft.ron"].definition.clone();
        definition.surfaces[0].face_rgb = [200, 180, 0];
        std::fs::write(
            directory.join("draft.ron"),
            ron::to_string(&definition).unwrap(),
        )
        .unwrap();
        game.step([PlayerInput::default(); 2]);
        let edited = game.snapshot();
        assert_eq!(edited.flow.as_ref().unwrap().phase, FlowPhase::Draft);
        assert_eq!(
            edited.flow.as_ref().unwrap().offers,
            draft.flow.as_ref().unwrap().offers
        );
        assert_eq!(edited.arena, definition.surfaces);
        assert!(edited.arena_objects.is_none());
        for file in ["prior-match.ron", "draft.ron"] {
            std::fs::remove_file(directory.join(file)).unwrap();
        }
        std::fs::remove_dir(directory).unwrap();
    }
    #[test]
    fn a_draft_edit_before_activation_moves_the_drawn_and_collision_floor_together() {
        let profile = ReplayProfile::RematchDraftReplay;
        let mut game = AuthoritativeMatch::new_with_profile(41, profile);
        let path = std::env::temp_dir().join(format!(
            "quarrel-future-draft-edit-{}.ron",
            std::process::id()
        ));
        let entry = game.legacy_arena_cache.get_mut("draft.ron").unwrap();
        entry.path = path.clone();
        let mut definition = entry.definition.clone();
        definition.surfaces[0].center_y_milli -= 100_000;
        std::fs::write(&path, ron::to_string(&definition).unwrap()).unwrap();
        let scripts = scripted_inputs_for(profile, 41, 3000);
        for (zero, one) in scripts[0].iter().zip(&scripts[1]) {
            let before = game.snapshot();
            if before.flow.as_ref().unwrap().phase == FlowPhase::ResumedCombat {
                break;
            }
            game.step(std::array::from_fn(|p| {
                [*zero, *one][p].with_progressive_observation(p as u8, Some(&before))
            }));
        }
        assert_eq!(
            game.snapshot().flow.unwrap().phase,
            FlowPhase::ResumedCombat
        );
        for _ in 0..90 {
            game.step([PlayerInput::default(); 2]);
        }
        let snapshot = game.snapshot();
        std::fs::remove_file(path).unwrap();
        assert_eq!(snapshot.arena, definition.surfaces);
        let orange = &snapshot.players[0];
        assert!(orange.alive && orange.grounded);
        assert!(
            orange.y_milli > -288_000 && orange.y_milli < -240_000,
            "fighter stayed on the original floor at {}",
            orange.y_milli
        );
    }
    #[test]
    fn an_earlier_edit_keeps_the_ice_entry_slide_through_the_transition() {
        let profile = ReplayProfile::RematchDraftReplay;
        let mut game = AuthoritativeMatch::new_with_profile(41, profile);
        let path =
            std::env::temp_dir().join(format!("quarrel-ice-entry-edit-{}.ron", std::process::id()));
        let entry = game.legacy_arena_cache.get_mut("timber.ron").unwrap();
        entry.path = path.clone();
        std::fs::write(&path, &entry.source).unwrap();
        let scripts = scripted_inputs_for(profile, 41, 6000);
        let mut edited = false;
        let mut entry_from = None;
        let mut entry_ticks = 0;
        for (zero, one) in scripts[0].iter().zip(&scripts[1]) {
            let before = game.snapshot();
            let phase = before.flow.as_ref().unwrap().phase;
            if !edited && phase == FlowPhase::TimberCombat && before.tick.is_multiple_of(15) {
                let mut definition = game.legacy_arena_cache["timber.ron"].definition.clone();
                definition.surfaces[0].face_rgb = [200, 180, 0];
                std::fs::write(&path, ron::to_string(&definition).unwrap()).unwrap();
                edited = true;
            }
            game.step(std::array::from_fn(|p| {
                [*zero, *one][p].with_progressive_observation(p as u8, Some(&before))
            }));
            let after = game.snapshot();
            if after.flow.as_ref().unwrap().phase == FlowPhase::IceTransition {
                assert!(edited);
                assert!(after.arena_entry_from_milli.is_some());
                let expected = entry_from.get_or_insert(after.arena_entry_from_milli.unwrap());
                assert_eq!(after.arena_entry_from_milli, Some(*expected));
                entry_ticks += 1;
            } else if entry_ticks > 0 {
                break;
            }
        }
        std::fs::remove_file(path).unwrap();
        assert!(entry_ticks >= 60, "ice entry lasted {entry_ticks} ticks");
    }
    #[test]
    fn invalid_future_stage_edits_keep_the_last_valid_geometry() {
        for file in ["timber.ron", "ice.ron"] {
            let profile = ReplayProfile::RematchDraftReplay;
            let mut game = AuthoritativeMatch::new_with_profile(41, profile);
            let expected = game.legacy_arena_cache[file].definition.surfaces.clone();
            let path = std::env::temp_dir().join(format!(
                "quarrel-invalid-stage-{}-{file}",
                std::process::id()
            ));
            game.legacy_arena_cache.get_mut(file).unwrap().path = path.clone();
            std::fs::write(&path, "(").unwrap();
            let scripts = scripted_inputs_for(profile, 41, 6000);
            let mut reached = false;
            for (zero, one) in scripts[0].iter().zip(&scripts[1]) {
                let inputs = [*zero, *one];
                let before = game.snapshot();
                game.step(std::array::from_fn(|p| {
                    inputs[p].with_progressive_observation(p as u8, Some(&before))
                }));
                if game.active_legacy_arena_filename() == file {
                    assert!(game.arena_reload_error().is_some());
                    assert_eq!(game.snapshot().arena, expected);
                    reached = true;
                    break;
                }
            }
            assert!(reached, "{file}");
            for _ in 0..30 {
                game.step([PlayerInput::default(); 2]);
            }
            assert!(game.arena_reload_error().is_some());
            assert_eq!(game.snapshot().arena, expected);
            std::fs::remove_file(path).unwrap();
        }
    }
    #[test]
    fn invalid_geometry_spawns_references_and_motion_are_rejected() {
        let mut arena = workshop();
        arena.spawns[0] = [700.0, 0.0];
        assert!(arena.validate().is_err());
        let mut arena = workshop();
        arena.objects[0].position[0] = 600.0;
        assert!(arena.validate().is_err());
        let mut arena = workshop();
        arena.objects[0].shape = ArenaShape::Circle { radius: f32::NAN };
        assert!(arena.validate().is_err());
        let mut arena = workshop();
        arena.chains[0].body_b = 99;
        assert!(arena.validate().is_err());
        let mut arena = workshop();
        arena.objects[0].shape = ArenaShape::Polygon {
            vertices: vec![
                [0.0, 0.0],
                [30.0, 0.0],
                [10.0, 5.0],
                [30.0, 30.0],
                [0.0, 30.0],
            ],
        };
        assert!(arena.validate().is_err());
        let mut arena = workshop();
        arena.objects[10]
            .motion
            .as_mut()
            .unwrap()
            .path
            .push([1000.0, 0.0]);
        assert!(arena.validate().is_err());
    }
}
