use super::*;
use bevy_rapier2d::rapier::prelude::{ImpulseJointHandle, RopeJointBuilder};
use std::collections::BTreeSet;

#[derive(Component, Clone, Copy)]
pub(crate) struct PlayerState {
    pub(crate) id: u8,
    pub(crate) aim: Vector,
    pub(crate) health: u16,
    pub(crate) fire_cooldown: u16,
    pub(crate) block_ticks: u16,
    pub(crate) hit_flash_ticks: u8,
    pub(crate) grounded: bool,
    pub(crate) jump_held: bool,
    pub(crate) alive: bool,
    pub(crate) stun_ticks: u16,
    pub(crate) stun_pulses_remaining: u8,
}
#[derive(Component, Clone, Copy)]
pub(crate) struct ProjectileState {
    pub(crate) id: u32,
    pub(crate) owner: u8,
    pub(crate) dazzle_pulses: u8,
    pub(crate) dazzle_stun_ticks: u16,
    pub(crate) explosive_radius_milli: i32,
}
#[derive(Clone, Copy)]
struct PlayerPhysics {
    body: RigidBodyHandle,
    collider: ColliderHandle,
}
#[derive(Clone, Copy)]
struct BulletPhysics {
    body: RigidBodyHandle,
    collider: ColliderHandle,
    previous: Vector,
    lifetime: u16,
}

pub(crate) struct PhysicsBoundary {
    rapier: RapierWorld,
    players: Vec<PlayerPhysics>,
    spawns: Vec<Vector>,
    platforms: Vec<ColliderHandle>,
    surface_colliders: BTreeMap<Vec<[i32; 2]>, ColliderBuilder>,
    object_bodies: BTreeMap<u16, (RigidBodyHandle, ColliderHandle, ArenaKind)>,
    object_before_velocity: BTreeMap<u16, Vector>,
    object_contacts: BTreeSet<(u16, u8)>,
    arena_chains: BTreeMap<u16, ImpulseJointHandle>,
    arena_anchors: Vec<RigidBodyHandle>,
    bullets: BTreeMap<u32, BulletPhysics>,
}
impl PhysicsBoundary {
    pub(crate) fn new(fighter_count: usize, arena: &ArenaDefinition) -> Self {
        let mut rapier = RapierWorld::new();
        rapier.gravity = Vector::new(0.0, -1_500.0);
        rapier.integration_parameters.dt = 1.0 / TICKS_PER_SECOND as f32;
        rapier.integration_parameters.max_ccd_substeps = 4;
        rapier.integration_parameters.normalized_max_linear_velocity = 5_000.0;
        let platforms = Vec::new();
        let spawns = distributed_spawns(fighter_count, arena);
        let players = spawns
            .iter()
            .map(|spawn| {
                let (body, collider) = rapier.insert(
                    RigidBodyBuilder::dynamic()
                        .translation(*spawn)
                        .linear_damping(0.7)
                        .angular_damping(8.0)
                        .lock_rotations()
                        .ccd_enabled(true)
                        .can_sleep(false),
                    ColliderBuilder::ball(PLAYER_RADIUS)
                        .density(0.004)
                        .friction(0.55)
                        .restitution(0.05),
                );
                PlayerPhysics { body, collider }
            })
            .collect();
        Self {
            rapier,
            players,
            spawns,
            platforms,
            surface_colliders: BTreeMap::new(),
            object_bodies: BTreeMap::new(),
            object_before_velocity: BTreeMap::new(),
            object_contacts: BTreeSet::new(),
            arena_chains: BTreeMap::new(),
            arena_anchors: Vec::new(),
            bullets: BTreeMap::new(),
        }
    }
    pub(crate) fn replace_arena(&mut self, arena: &ArenaDefinition) {
        for handle in self.platforms.drain(..) {
            if let Some(body) = self.rapier.colliders[handle].parent() {
                self.rapier.remove_body(body);
            }
        }
        for (_, (body, _, _)) in std::mem::take(&mut self.object_bodies) {
            self.rapier.remove_body(body);
        }
        for anchor in self.arena_anchors.drain(..) {
            self.rapier.remove_body(anchor);
        }
        self.arena_chains.clear();
        self.object_before_velocity.clear();
        self.object_contacts.clear();
        // Arena assets are authored in pixels; Rapier's joint tolerances are metres.
        self.rapier.integration_parameters.length_unit = 100.0;
        self.platforms = arena
            .surfaces
            .iter()
            .map(|surface| {
                self.rapier
                    .insert(
                        RigidBodyBuilder::fixed().translation(Vector::new(
                            surface.center_x_milli as f32 / 1_000.0,
                            surface.center_y_milli as f32 / 1_000.0,
                        )),
                        if surface.outline_milli.len() < 3 {
                            collider_for_surface(surface)
                        } else {
                            self.surface_colliders
                                .entry(surface.outline_milli.clone())
                                .or_insert_with(|| collider_for_surface(surface))
                                .clone()
                        }
                        .rotation(surface.rotation_milliradians as f32 / 1_000.0)
                        .friction(0.92)
                        .restitution(0.02),
                    )
                    .1
            })
            .collect();
        for object in &arena.objects {
            if matches!(object.kind, ArenaKind::Background) {
                continue;
            }
            let loose = matches!(
                object.kind,
                ArenaKind::Loose
                    | ArenaKind::Breakable { loose: true }
                    | ArenaKind::Saw { loose: true, .. }
            );
            let builder = if object.motion.is_some()
                || matches!(object.kind, ArenaKind::Saw { loose: false, .. })
            {
                RigidBodyBuilder::kinematic_position_based()
            } else if loose {
                RigidBodyBuilder::dynamic()
            } else {
                RigidBodyBuilder::fixed()
            };
            let (body, collider) = self.rapier.insert(
                builder
                    .translation(Vector::from(object.position))
                    .rotation(object.rotation)
                    .ccd_enabled(true),
                object
                    .shape
                    .collider()
                    .mass(object.mass)
                    .friction(0.8)
                    .restitution(0.08),
            );
            self.object_bodies
                .insert(object.id, (body, collider, object.kind.clone()));
            self.object_before_velocity.insert(object.id, Vector::ZERO);
            self.platforms.push(collider);
        }
        for chain in &arena.chains {
            let a = if let Some(id) = chain.body_a {
                self.object_bodies[&id].0
            } else {
                let anchor = self
                    .rapier
                    .insert_body(RigidBodyBuilder::fixed().translation(Vector::from(chain.anchor)));
                self.arena_anchors.push(anchor);
                anchor
            };
            let handle = self.rapier.impulse_joints.insert(
                a,
                self.object_bodies[&chain.body_b].0,
                RopeJointBuilder::new(chain.length),
                true,
            );
            self.arena_chains.insert(chain.id, handle);
        }
        self.spawns = distributed_spawns(self.players.len(), arena);
        self.reset();
    }
    pub(crate) fn object_pose(&self, id: u16) -> Option<(Vector, Vector, f32, ArenaKind)> {
        self.object_bodies.get(&id).map(|(body, _, kind)| {
            let body = &self.rapier.bodies[*body];
            (
                body.translation(),
                body.linvel(),
                body.rotation().angle(),
                kind.clone(),
            )
        })
    }
    pub(crate) fn chain_active(&self, id: u16) -> bool {
        self.arena_chains.contains_key(&id)
    }
    pub(crate) fn object_contacts(&mut self) -> Vec<(u16, u8, u16)> {
        let mut next = BTreeSet::new();
        let mut damage = Vec::new();
        for (&object, (body, collider, kind)) in &self.object_bodies {
            for (fighter, player) in self.players.iter().enumerate() {
                if !self
                    .rapier
                    .contact_pair(*collider, player.collider)
                    .is_some_and(|pair| pair.has_any_active_contact())
                {
                    continue;
                }
                let key = (object, fighter as u8);
                next.insert(key);
                if self.object_contacts.contains(&key) {
                    continue;
                }
                let speed = (self.object_before_velocity[&object]
                    - self.rapier.bodies[player.body].linvel())
                .length();
                let amount = if matches!(kind, ArenaKind::Saw { .. }) {
                    35
                } else if self.rapier.bodies[*body].is_dynamic() && speed > 180.0 {
                    ((speed - 180.0) * self.rapier.colliders[*collider].mass() * 0.15)
                        .clamp(1.0, 100.0) as u16
                } else {
                    0
                };
                if amount > 0 {
                    damage.push((object, fighter as u8, amount));
                }
            }
        }
        self.object_contacts = next;
        damage
    }
    pub(crate) fn bullet_object_contact(&self, id: u32) -> Option<(u16, Vector)> {
        let bullet = self.bullets.get(&id)?;
        self.object_bodies
            .iter()
            .find_map(|(&object, (_, collider, _))| {
                self.rapier
                    .contact_pair(bullet.collider, *collider)
                    .is_some_and(|pair| pair.has_any_active_contact())
                    .then(|| {
                        (
                            object,
                            self.rapier.bodies[bullet.body].linvel()
                                * self.rapier.colliders[bullet.collider].mass(),
                        )
                    })
            })
    }
    pub(crate) fn remove_object(&mut self, id: u16, chains: &[ArenaChain]) {
        let Some((body, collider, _)) = self.object_bodies.remove(&id) else {
            return;
        };
        self.platforms.retain(|handle| *handle != collider);
        self.object_before_velocity.remove(&id);
        self.object_contacts.retain(|(object, _)| *object != id);
        for chain in chains
            .iter()
            .filter(|chain| chain.body_a == Some(id) || chain.body_b == id)
        {
            if let Some(handle) = self.arena_chains.remove(&chain.id) {
                self.rapier.impulse_joints.remove(handle, true);
            }
        }
        self.rapier.remove_body(body);
    }
    pub(crate) fn apply_object_impulse(&mut self, id: u16, impulse: Vector) {
        if let Some((body, _, _)) = self.object_bodies.get(&id) {
            self.rapier.bodies[*body].apply_impulse(impulse, true);
        }
    }
    pub(crate) fn update_arena_motion(&mut self, arena: &ArenaDefinition, tick: u32) {
        let time = tick as f32 / TICKS_PER_SECOND as f32;
        for object in &arena.objects {
            let Some((body, _, kind)) = self.object_bodies.get(&object.id) else {
                continue;
            };
            self.object_before_velocity
                .insert(object.id, self.rapier.bodies[*body].linvel());
            if object.motion.is_none() && !matches!(kind, ArenaKind::Saw { loose: false, .. }) {
                continue;
            }
            let mut position = Vector::from(object.position);
            let mut angle = object.rotation;
            if let ArenaKind::Saw {
                angular_velocity, ..
            } = kind
            {
                angle += angular_velocity * time;
            }
            if let Some(motion) = &object.motion {
                if !motion.path.is_empty() {
                    let phase = (time / motion.period_seconds).fract() * motion.path.len() as f32;
                    let i = phase.floor() as usize;
                    let t = phase.fract();
                    position += Vector::from(motion.path[i]) * (1.0 - t)
                        + Vector::from(motion.path[(i + 1) % motion.path.len()]) * t;
                }
                angle += motion.angular_velocity * time;
            }
            let body = &mut self.rapier.bodies[*body];
            body.set_next_kinematic_translation(position);
            body.set_next_kinematic_rotation(Rotation::new(angle));
        }
    }
    pub(crate) fn reset(&mut self) {
        for (player, spawn) in self.players.iter().zip(&self.spawns) {
            let body = &mut self.rapier.bodies[player.body];
            body.set_translation(*spawn, true);
            body.set_linvel(Vector::ZERO, true);
        }
        for (_, bullet) in std::mem::take(&mut self.bullets) {
            self.rapier.remove_body(bullet.body);
        }
        self.rapier
            .bodies
            .propagate_modified_body_positions_to_colliders(&mut self.rapier.colliders);
    }
    pub(crate) fn set_player_control(
        &mut self,
        id: u8,
        input: PlayerInput,
        grounded: bool,
        released: bool,
        movement_bonus: u16,
    ) -> bool {
        let body = &mut self.rapier.bodies[self.players[usize::from(id)].body];
        let mut velocity = body.linvel();
        let control = if input.move_axis == 0 {
            if grounded { 0.02 } else { 0.0 }
        } else if grounded {
            0.18
        } else {
            AIR_CONTROL
        };
        let speed = RUN_SPEED + f32::from(movement_bonus);
        if grounded || input.move_axis != 0 {
            velocity.x += (f32::from(input.move_axis) * speed - velocity.x) * control;
        }
        if released && !grounded && velocity.y > 0.0 {
            velocity.y *= JUMP_RELEASE_CUT;
        }
        let jumped = input.jump && grounded;
        if jumped {
            velocity.y = JUMP_SPEED;
        }
        body.set_linvel(velocity, true);
        jumped
    }
    pub(crate) fn spawn_bullet(&mut self, id: u32, owner: u8, aim: Vector, speed: f32) {
        let shooter = &self.rapier.bodies[self.players[usize::from(owner)].body];
        let origin = shooter.translation() + aim * (PLAYER_RADIUS + BULLET_RADIUS + 4.0);
        let (body, collider) = self.rapier.insert(
            RigidBodyBuilder::dynamic()
                .translation(origin)
                .linvel(aim * speed)
                .gravity_scale(0.0)
                .ccd_enabled(true)
                .can_sleep(false),
            ColliderBuilder::ball(BULLET_RADIUS)
                .density(0.0005)
                .friction(0.0)
                .restitution(0.8),
        );
        self.bullets.insert(
            id,
            BulletPhysics {
                body,
                collider,
                previous: origin,
                lifetime: BULLET_LIFETIME,
            },
        );
    }
    pub(crate) fn apply_impulse(&mut self, player: u8, impulse: Vector) {
        self.rapier.bodies[self.players[usize::from(player)].body].apply_impulse(impulse, true);
    }
    pub(crate) fn step(&mut self) {
        for bullet in self.bullets.values_mut() {
            bullet.previous = self.rapier.bodies[bullet.body].translation();
            bullet.lifetime = bullet.lifetime.saturating_sub(1);
        }
        self.rapier.step();
    }
    pub(crate) fn player_pose(&self, id: u8) -> (Vector, Vector) {
        let body = &self.rapier.bodies[self.players[usize::from(id)].body];
        (body.translation(), body.linvel())
    }
    pub(crate) fn player_grounded(&self, id: u8) -> bool {
        let player = self.players[usize::from(id)].collider;
        self.platforms.iter().any(|platform| {
            self.rapier
                .contact_pair(player, *platform)
                .is_some_and(|pair| pair.has_any_active_contact())
        })
    }
    pub(crate) fn bullet_contact(&self, id: u32, target: u8) -> Option<Vector> {
        let bullet = self.bullets.get(&id)?;
        let position = self.rapier.bodies[bullet.body].translation();
        let target_position = self.player_pose(target).0;
        let segment = position - bullet.previous;
        let length_squared = segment.length_squared();
        let fraction = if length_squared > 0.0 {
            ((target_position - bullet.previous).dot(segment) / length_squared).clamp(0.0, 1.0)
        } else {
            0.0
        };
        let closest = bullet.previous + segment * fraction;
        (closest.distance_squared(target_position) <= (PLAYER_RADIUS + BULLET_RADIUS + 2.0).powi(2))
            .then_some(
                target_position + (closest - target_position).normalize_or_zero() * PLAYER_RADIUS,
            )
    }
    pub(crate) fn bullet_platform_contact(&self, id: u32) -> bool {
        self.bullets.get(&id).is_some_and(|bullet| {
            self.platforms.iter().any(|platform| {
                self.rapier
                    .contact_pair(bullet.collider, *platform)
                    .is_some_and(|pair| pair.has_any_active_contact())
            })
        })
    }
    pub(crate) fn reflect_bullet(&mut self, id: u32) {
        if let Some(bullet) = self.bullets.get(&id) {
            let body = &mut self.rapier.bodies[bullet.body];
            let incoming = body.linvel();
            body.set_linvel(
                Vector::new(-incoming.x, incoming.x.abs() * 0.22 - incoming.y),
                true,
            );
        }
    }
    pub(crate) fn bullet_pose(&self, id: u32) -> Option<(Vector, Vector, Vector, u16)> {
        self.bullets.get(&id).map(|bullet| {
            let body = &self.rapier.bodies[bullet.body];
            (
                body.translation(),
                bullet.previous,
                body.linvel(),
                bullet.lifetime,
            )
        })
    }
    pub(crate) fn remove_bullet(&mut self, id: u32) {
        if let Some(bullet) = self.bullets.remove(&id) {
            self.rapier.remove_body(bullet.body);
        }
    }
    pub(crate) fn clear_bullets(&mut self) {
        for bullet in std::mem::take(&mut self.bullets).into_values() {
            self.rapier.remove_body(bullet.body);
        }
    }
}
fn distributed_spawns(count: usize, arena: &ArenaDefinition) -> Vec<Vector> {
    let base = arena
        .spawns
        .iter()
        .copied()
        .map(Vector::from)
        .collect::<Vec<_>>();
    (0..count)
        .map(|index| {
            if index < base.len() {
                base[index]
            } else {
                let left = arena.frame[0] + PLAYER_RADIUS * 2.0;
                let right = arena.frame[2] - PLAYER_RADIUS * 2.0;
                let y = base.first().map_or(0.0, |spawn| spawn.y);
                let fraction = (index + 1) as f32 / (count + 1) as f32;
                Vector::new(left + (right - left) * fraction, y)
            }
        })
        .collect()
}
mod combat;
pub use combat::*;
