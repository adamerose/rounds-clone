use bevy_ecs::prelude::*;
use bevy_rapier2d::rapier::prelude::{
    ColliderBuilder, ColliderHandle, FixedJointBuilder, GenericJoint, Group, ImpulseJointHandle,
    InteractionGroups, InteractionTestMode, PhysicsWorld as RapierWorld, RigidBodyBuilder,
    RigidBodyHandle, RopeJointBuilder, Vector,
};
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use std::collections::BTreeMap;

mod flow;
pub use flow::*;

pub const TICKS_PER_SECOND: u32 = 60;
pub const REPLAY_TICKS: u32 = 1_440;
pub const MAX_INSPECTED_PROJECTILES: usize = 64;
pub const REPLAY_PROFILE: &str = "timber-collapse-replay";
pub const SOURCE_INTERVAL: &str = "03:26.00-03:50.00";
pub const SOURCE_SHA256: &str = "453954a7230401ed805be4e53dec41779a1913dfd69903671fc131fca2c8a18c";
pub const TIMBER_IMPACT_TICK: u32 = 864;
pub const TEAL_REPLAY_TICKS: u32 = 786;
pub const TEAL_REPLAY_PROFILE: &str = "teal-duel-replay";
pub const TEAL_SOURCE_INTERVAL: &str = "00:22.50-00:35.60";
pub const TEAL_SOURCE_SHA256: &str =
    "1460e67037f46e128972fa216894b24c4069ac9690d79e3861af6679486d15f9";

const PLAYER_RADIUS: f32 = 22.0;
const RUN_SPEED: f32 = 220.0;
const AIR_CONTROL: f32 = 0.08;
const JUMP_SPEED: f32 = 680.0;
const BULLET_RADIUS: f32 = 5.0;
const BULLET_SPEED: f32 = 3_600.0;
const BULLET_LIFETIME: u16 = 150;
const FIRE_COOLDOWN: u16 = 24;
const BLOCK_DURATION: u16 = 18;
const DAMAGE_PER_HIT: u16 = 100;
const RECOIL_IMPULSE: f32 = 72.0;
const HIT_IMPULSE: f32 = 420.0;
const KILL_X: f32 = 760.0;
const KILL_Y: f32 = -440.0;
const DYNAMIC_GROUP: Group = Group::GROUP_6;
const TIMBER_EXPLOSION_CENTER: Vector = Vector::new(-245.0, 135.0);
const TIMBER_EXPLOSION_RADIUS: f32 = 520.0;
const TIMBER_EXPLOSION_IMPULSE: f32 = 4_800.0;

#[derive(Clone, Copy, Debug, Default, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "kebab-case")]
pub enum ReplayProfile {
    TealDuelReplay,
    RematchDraftReplay,
    #[default]
    TimberCollapseReplay,
}

impl ReplayProfile {
    pub fn name(self) -> &'static str {
        match self {
            Self::TealDuelReplay => TEAL_REPLAY_PROFILE,
            Self::RematchDraftReplay => REMATCH_DRAFT_PROFILE,
            Self::TimberCollapseReplay => REPLAY_PROFILE,
        }
    }

    pub fn replay_ticks(self) -> u32 {
        match self {
            Self::TealDuelReplay => TEAL_REPLAY_TICKS,
            Self::RematchDraftReplay => REMATCH_DRAFT_TICKS,
            Self::TimberCollapseReplay => REPLAY_TICKS,
        }
    }

    pub fn source_interval(self) -> &'static str {
        match self {
            Self::TealDuelReplay => TEAL_SOURCE_INTERVAL,
            Self::RematchDraftReplay => REMATCH_DRAFT_SOURCE_INTERVAL,
            Self::TimberCollapseReplay => SOURCE_INTERVAL,
        }
    }

    pub fn source_sha256(self) -> &'static str {
        match self {
            Self::TealDuelReplay => TEAL_SOURCE_SHA256,
            Self::RematchDraftReplay => SOURCE_SHA256,
            Self::TimberCollapseReplay => SOURCE_SHA256,
        }
    }

    pub fn source_start_hundredths(self) -> u64 {
        match self {
            Self::TealDuelReplay => 2_250,
            Self::RematchDraftReplay => REMATCH_DRAFT_SOURCE_START_HUNDREDTHS,
            Self::TimberCollapseReplay => 20_600,
        }
    }
}

impl std::str::FromStr for ReplayProfile {
    type Err = String;

    fn from_str(value: &str) -> Result<Self, Self::Err> {
        match value {
            TEAL_REPLAY_PROFILE => Ok(Self::TealDuelReplay),
            REMATCH_DRAFT_PROFILE => Ok(Self::RematchDraftReplay),
            REPLAY_PROFILE => Ok(Self::TimberCollapseReplay),
            _ => Err(format!(
                "unsupported replay profile {value}; expected {TEAL_REPLAY_PROFILE}, {REMATCH_DRAFT_PROFILE}, or {REPLAY_PROFILE}"
            )),
        }
    }
}

#[derive(Clone, Copy, Debug, Default, Deserialize, PartialEq, Eq, Serialize)]
pub struct PlayerInput {
    pub move_axis: i8,
    pub aim_x: i16,
    pub aim_y: i16,
    pub jump: bool,
    pub fire: bool,
    pub block: bool,
    pub flow: Option<FlowCommand>,
}

impl PlayerInput {
    pub fn validated(self) -> Self {
        Self {
            move_axis: self.move_axis.clamp(-1, 1),
            aim_x: self.aim_x.clamp(-1_000, 1_000),
            aim_y: self.aim_y.clamp(-1_000, 1_000),
            ..self
        }
    }
}

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ArenaSurfaceSnapshot {
    pub id: u8,
    pub center_x_milli: i32,
    pub center_y_milli: i32,
    pub width_milli: i32,
    pub height_milli: i32,
    pub face_rgb: [u8; 3],
}

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum DynamicBodyShape {
    Timber,
    Weight,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct DynamicBodySnapshot {
    pub id: u16,
    pub shape: DynamicBodyShape,
    pub x_milli: i32,
    pub y_milli: i32,
    pub rotation_milliradians: i32,
    pub velocity_x_milli_per_second: i32,
    pub velocity_y_milli_per_second: i32,
    pub angular_velocity_milliradians_per_second: i32,
    pub width_milli: i32,
    pub height_milli: i32,
    pub radius_milli: i32,
    pub face_rgb: [u8; 3],
    pub sleeping: bool,
}

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum ConstraintKind {
    Fixed,
    Rope,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ConstraintSnapshot {
    pub id: u16,
    pub body_a: Option<u16>,
    pub body_b: u16,
    pub kind: ConstraintKind,
    pub anchor_x_milli: i32,
    pub anchor_y_milli: i32,
    pub active: bool,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ExplosionSnapshot {
    pub id: u16,
    pub tick: u32,
    pub x_milli: i32,
    pub y_milli: i32,
    pub radius_milli: i32,
    pub impulse_milli: i32,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PlayerSnapshot {
    pub id: u8,
    pub x_milli: i32,
    pub y_milli: i32,
    pub velocity_x_milli_per_second: i32,
    pub velocity_y_milli_per_second: i32,
    pub aim_x: i16,
    pub aim_y: i16,
    pub health: u16,
    pub fire_cooldown_ticks: u16,
    pub block_ticks: u16,
    pub hit_flash_ticks: u8,
    pub grounded: bool,
    pub alive: bool,
    pub stun_ticks: u16,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ProjectileSnapshot {
    pub id: u32,
    pub owner: u8,
    pub x_milli: i32,
    pub y_milli: i32,
    pub previous_x_milli: i32,
    pub previous_y_milli: i32,
    pub velocity_x_milli_per_second: i32,
    pub velocity_y_milli_per_second: i32,
    pub lifetime_ticks: u16,
    pub dazzle_pulses: u8,
    pub explosive_radius_milli: i32,
}

#[derive(Clone, Debug, Default, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct CombatMetrics {
    pub platform_contact_ticks: u32,
    pub jumps: u32,
    pub shots_fired: u32,
    pub recoil_impulses: u32,
    pub block_activations: u32,
    pub reflections: u32,
    pub hits: u32,
    pub health_scaled_knockbacks: u32,
    pub bullet_ccd_contacts: u32,
    pub ring_outs: u32,
    pub dynamic_body_contacts: u32,
    pub fighter_body_contact_ticks: u32,
    pub released_constraints: u32,
    pub explosion_impulsed_bodies: u32,
    pub dazzle_stun_pulses: u32,
    pub explosive_projectile_impacts: u32,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct MatchSnapshot {
    pub protocol: u16,
    pub seed: u64,
    pub profile: String,
    pub tick: u32,
    pub arena: Vec<ArenaSurfaceSnapshot>,
    pub dynamic_bodies: Vec<DynamicBodySnapshot>,
    pub constraints: Vec<ConstraintSnapshot>,
    pub explosions: Vec<ExplosionSnapshot>,
    pub players: Vec<PlayerSnapshot>,
    pub projectiles: Vec<ProjectileSnapshot>,
    pub metrics: CombatMetrics,
    pub winner: Option<u8>,
    pub flow: Option<FlowSnapshot>,
}

#[derive(Component, Clone, Copy)]
struct PlayerState {
    id: u8,
    aim: Vector,
    health: u16,
    fire_cooldown: u16,
    block_ticks: u16,
    hit_flash_ticks: u8,
    grounded: bool,
    alive: bool,
    stun_ticks: u16,
    stun_pulses_remaining: u8,
    stun_pulse_cooldown: u8,
}

#[derive(Component, Clone, Copy)]
struct ProjectileState {
    id: u32,
    owner: u8,
    dazzle_pulses: u8,
    dazzle_stun_ticks: u16,
    explosive_radius_milli: i32,
    explosive_impulse_milli: i32,
}

#[derive(Component, Clone, Copy)]
struct DynamicBodyState {
    id: u16,
    shape: DynamicBodyShape,
    width: f32,
    height: f32,
    radius: f32,
    face_rgb: [u8; 3],
}

#[derive(Component, Clone, Copy)]
struct ConstraintState {
    id: u16,
    body_a: Option<u16>,
    body_b: u16,
    kind: ConstraintKind,
    anchor: Vector,
    active: bool,
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

#[derive(Clone, Copy)]
struct DynamicBodyPhysics {
    body: RigidBodyHandle,
    collider: ColliderHandle,
}

#[derive(Clone, Copy)]
struct ConstraintPhysics {
    handle: ImpulseJointHandle,
    release_on_explosion: bool,
    active: bool,
}

#[derive(Clone, Copy)]
struct DynamicBodyDefinition {
    id: u16,
    shape: DynamicBodyShape,
    position: Vector,
    rotation: f32,
    width: f32,
    height: f32,
    radius: f32,
    face_rgb: [u8; 3],
}

fn timber_body_definitions() -> Vec<DynamicBodyDefinition> {
    const TIMBER: [u8; 3] = [124, 39, 48];
    const DARK_TIMBER: [u8; 3] = [83, 28, 39];
    const WEIGHT: [u8; 3] = [98, 42, 59];
    let timber = |id, x, y, rotation, width, height, color| DynamicBodyDefinition {
        id,
        shape: DynamicBodyShape::Timber,
        position: Vector::new(x, y),
        rotation,
        width,
        height,
        radius: 0.0,
        face_rgb: color,
    };
    vec![
        timber(0, -210.0, -236.0, 0.0, 260.0, 30.0, DARK_TIMBER),
        timber(1, 210.0, -236.0, 0.0, 260.0, 30.0, DARK_TIMBER),
        timber(2, -322.0, -151.0, 0.0, 30.0, 170.0, TIMBER),
        timber(3, -102.0, -151.0, 0.0, 30.0, 170.0, TIMBER),
        timber(4, 102.0, -151.0, 0.0, 30.0, 170.0, TIMBER),
        timber(5, 322.0, -151.0, 0.0, 30.0, 170.0, TIMBER),
        timber(6, -212.0, -58.0, 0.0, 250.0, 30.0, DARK_TIMBER),
        timber(7, 212.0, -58.0, 0.0, 250.0, 30.0, DARK_TIMBER),
        timber(8, -102.0, 20.0, 0.0, 30.0, 140.0, TIMBER),
        timber(9, 102.0, 20.0, 0.0, 30.0, 140.0, TIMBER),
        timber(10, -205.0, 85.0, 0.42, 230.0, 28.0, TIMBER),
        timber(11, 205.0, 85.0, -0.42, 230.0, 28.0, TIMBER),
        timber(12, -72.0, 154.0, 0.0, 160.0, 27.0, DARK_TIMBER),
        timber(13, 72.0, 154.0, 0.0, 160.0, 27.0, DARK_TIMBER),
        timber(14, 0.0, 213.0, 0.0, 30.0, 106.0, TIMBER),
        timber(15, -72.0, 262.0, 0.28, 150.0, 26.0, TIMBER),
        timber(16, 72.0, 262.0, -0.28, 150.0, 26.0, TIMBER),
        DynamicBodyDefinition {
            id: 100,
            shape: DynamicBodyShape::Weight,
            position: Vector::new(-520.0, 70.0),
            rotation: 0.0,
            width: 0.0,
            height: 0.0,
            radius: 42.0,
            face_rgb: WEIGHT,
        },
        DynamicBodyDefinition {
            id: 101,
            shape: DynamicBodyShape::Weight,
            position: Vector::new(520.0, 70.0),
            rotation: 0.0,
            width: 0.0,
            height: 0.0,
            radius: 42.0,
            face_rgb: WEIGHT,
        },
    ]
}

struct PhysicsBoundary {
    rapier: RapierWorld,
    players: [PlayerPhysics; 2],
    platforms: Vec<ColliderHandle>,
    bullets: BTreeMap<u32, BulletPhysics>,
    dynamic_bodies: BTreeMap<u16, DynamicBodyPhysics>,
    constraints: BTreeMap<u16, ConstraintPhysics>,
}

impl PhysicsBoundary {
    fn new(profile: ReplayProfile) -> Self {
        let mut rapier = RapierWorld::new();
        rapier.gravity = Vector::new(0.0, -1_500.0);
        rapier.integration_parameters.dt = 1.0 / TICKS_PER_SECOND as f32;
        rapier.integration_parameters.max_ccd_substeps = 4;
        rapier.integration_parameters.normalized_max_linear_velocity = 5_000.0;

        let platforms = arena_for_profile(profile)
            .iter()
            .map(|surface| {
                let (_, collider) = rapier.insert(
                    RigidBodyBuilder::fixed().translation(Vector::new(
                        surface.center_x_milli as f32 / 1_000.0,
                        surface.center_y_milli as f32 / 1_000.0,
                    )),
                    ColliderBuilder::cuboid(
                        surface.width_milli as f32 / 2_000.0,
                        surface.height_milli as f32 / 2_000.0,
                    )
                    .friction(0.92)
                    .restitution(0.02)
                    .collision_groups(groups(
                        Group::GROUP_3,
                        Group::GROUP_1
                            | Group::GROUP_2
                            | Group::GROUP_4
                            | Group::GROUP_5
                            | DYNAMIC_GROUP,
                    )),
                );
                collider
            })
            .collect::<Vec<_>>();

        let player_spawns = match profile {
            ReplayProfile::TealDuelReplay => [(-520.0, -134.0, 0_u8), (520.0, -134.0, 1_u8)],
            ReplayProfile::RematchDraftReplay => [(-500.0, -150.0, 0_u8), (500.0, -150.0, 1_u8)],
            ReplayProfile::TimberCollapseReplay => [(-500.0, -210.0, 0_u8), (500.0, -210.0, 1_u8)],
        };
        let players = player_spawns.map(|(x, y, id)| {
            let (membership, filter) = if id == 0 {
                (
                    Group::GROUP_1,
                    Group::GROUP_2 | Group::GROUP_3 | Group::GROUP_5 | DYNAMIC_GROUP,
                )
            } else {
                (
                    Group::GROUP_2,
                    Group::GROUP_1 | Group::GROUP_3 | Group::GROUP_4 | DYNAMIC_GROUP,
                )
            };
            let (body, collider) = rapier.insert(
                RigidBodyBuilder::dynamic()
                    .translation(Vector::new(x, y))
                    .linear_damping(0.7)
                    .angular_damping(8.0)
                    .lock_rotations()
                    .ccd_enabled(true)
                    .can_sleep(false),
                ColliderBuilder::ball(PLAYER_RADIUS)
                    .density(if profile != ReplayProfile::TealDuelReplay {
                        0.02
                    } else {
                        0.004
                    })
                    .friction(0.55)
                    .restitution(0.05)
                    .collision_groups(groups(membership, filter)),
            );
            PlayerPhysics { body, collider }
        });

        let mut boundary = Self {
            rapier,
            players,
            platforms,
            bullets: BTreeMap::new(),
            dynamic_bodies: BTreeMap::new(),
            constraints: BTreeMap::new(),
        };
        if profile == ReplayProfile::TimberCollapseReplay {
            boundary.insert_timber_structure();
        }
        boundary
    }

    fn insert_timber_structure(&mut self) {
        let anchor = self.rapier.insert_body(RigidBodyBuilder::fixed());
        for definition in timber_body_definitions() {
            let body_builder = RigidBodyBuilder::dynamic()
                .translation(definition.position)
                .rotation(definition.rotation)
                .linear_damping(1.1)
                .angular_damping(1.6)
                .ccd_enabled(true);
            let collider_builder = match definition.shape {
                DynamicBodyShape::Timber => {
                    ColliderBuilder::cuboid(definition.width * 0.5, definition.height * 0.5)
                }
                DynamicBodyShape::Weight => ColliderBuilder::ball(definition.radius),
            }
            .density(if definition.shape == DynamicBodyShape::Weight {
                0.008
            } else {
                0.0045
            })
            .friction(0.82)
            .restitution(0.08)
            .collision_groups(groups(
                DYNAMIC_GROUP,
                Group::GROUP_1
                    | Group::GROUP_2
                    | Group::GROUP_3
                    | Group::GROUP_4
                    | Group::GROUP_5
                    | DYNAMIC_GROUP,
            ));
            let (body, collider) = self.rapier.insert(body_builder, collider_builder);
            self.dynamic_bodies
                .insert(definition.id, DynamicBodyPhysics { body, collider });

            let (constraint_id, joint, release_on_explosion): (u16, GenericJoint, bool) =
                match definition.shape {
                    DynamicBodyShape::Timber => (
                        definition.id,
                        FixedJointBuilder::new()
                            .local_anchor1(definition.position)
                            .local_anchor2(Vector::ZERO)
                            .build()
                            .into(),
                        true,
                    ),
                    DynamicBodyShape::Weight => {
                        let anchor_position = Vector::new(definition.position.x, 330.0);
                        (
                            1_000 + definition.id,
                            RopeJointBuilder::new(anchor_position.distance(definition.position))
                                .local_anchor1(anchor_position)
                                .local_anchor2(Vector::ZERO)
                                .build()
                                .into(),
                            false,
                        )
                    }
                };
            let handle = self.rapier.impulse_joints.insert(anchor, body, joint, true);
            self.constraints.insert(
                constraint_id,
                ConstraintPhysics {
                    handle,
                    release_on_explosion,
                    active: true,
                },
            );
        }
    }

    fn release_explosion_constraints(&mut self) -> Vec<u16> {
        let mut released = Vec::new();
        for (id, constraint) in &mut self.constraints {
            if constraint.release_on_explosion && constraint.active {
                let _ = self.rapier.impulse_joints.remove(constraint.handle, true);
                constraint.active = false;
                released.push(*id);
            }
        }
        released
    }

    fn apply_radial_explosion(&mut self, center: Vector, radius: f32, strength: f32) -> u32 {
        let mut count = 0;
        for body in self.dynamic_bodies.values() {
            let rigid_body = &mut self.rapier.bodies[body.body];
            let offset = rigid_body.translation() - center;
            let distance = offset.length();
            if distance >= radius {
                continue;
            }
            let direction = (offset + Vector::new(0.0, 80.0)).normalize_or_zero();
            let impulse = direction * strength * (1.0 - distance / radius).max(0.12);
            rigid_body.apply_impulse(impulse, true);
            rigid_body.apply_torque_impulse(
                impulse.x.signum() * strength * 0.004 * (1.0 - distance / radius),
                true,
            );
            rigid_body.wake_up(true);
            count += 1;
        }
        count
    }

    fn dynamic_body_pose(&self, id: u16) -> Option<(Vector, f32, Vector, f32, bool)> {
        self.dynamic_bodies.get(&id).map(|physics| {
            let body = &self.rapier.bodies[physics.body];
            (
                body.translation(),
                body.rotation().angle(),
                body.linvel(),
                body.angvel(),
                body.is_sleeping(),
            )
        })
    }

    fn dynamic_contact_counts(&self) -> (u32, u32) {
        let bodies = self.dynamic_bodies.values().collect::<Vec<_>>();
        let mut body_contacts = 0;
        for left in 0..bodies.len() {
            for right in left + 1..bodies.len() {
                if self
                    .rapier
                    .contact_pair(bodies[left].collider, bodies[right].collider)
                    .is_some_and(|pair| pair.has_any_active_contact())
                {
                    body_contacts += 1;
                }
            }
        }
        let fighter_contacts = self
            .players
            .iter()
            .flat_map(|player| {
                bodies.iter().filter(move |body| {
                    self.rapier
                        .contact_pair(player.collider, body.collider)
                        .is_some_and(|pair| pair.has_any_active_contact())
                })
            })
            .count() as u32;
        (body_contacts, fighter_contacts)
    }

    fn bullet_dynamic_contact(&self, id: u32) -> Option<u16> {
        let bullet = self.bullets.get(&id)?;
        self.dynamic_bodies.iter().find_map(|(body_id, body)| {
            self.rapier
                .contact_pair(bullet.collider, body.collider)
                .is_some_and(|pair| pair.has_any_active_contact())
                .then_some(*body_id)
        })
    }

    fn set_player_control(&mut self, id: u8, input: PlayerInput, grounded: bool) -> bool {
        let body = &mut self.rapier.bodies[self.players[usize::from(id)].body];
        let mut velocity = body.linvel();
        let control = if input.move_axis == 0 {
            if grounded { 0.02 } else { 0.0 }
        } else if grounded {
            0.18
        } else {
            AIR_CONTROL
        };
        if grounded || input.move_axis != 0 {
            velocity.x += (f32::from(input.move_axis) * RUN_SPEED - velocity.x) * control;
        }
        let jumped = input.jump && grounded;
        if jumped {
            velocity.y = JUMP_SPEED;
        }
        body.set_linvel(velocity, true);
        jumped
    }

    fn spawn_bullet(&mut self, id: u32, owner: u8, aim: Vector) {
        let shooter = &self.rapier.bodies[self.players[usize::from(owner)].body];
        let origin = shooter.translation() + aim * (PLAYER_RADIUS + BULLET_RADIUS + 4.0);
        let (membership, filter) = bullet_groups(owner);
        let (body, collider) = self.rapier.insert(
            RigidBodyBuilder::dynamic()
                .translation(origin)
                .linvel(aim * BULLET_SPEED)
                .gravity_scale(0.0)
                .ccd_enabled(true)
                .can_sleep(false),
            ColliderBuilder::ball(BULLET_RADIUS)
                .density(0.0005)
                .friction(0.0)
                .restitution(0.8)
                .collision_groups(groups(membership, filter)),
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

    fn apply_impulse(&mut self, player: u8, impulse: Vector) {
        self.rapier.bodies[self.players[usize::from(player)].body].apply_impulse(impulse, true);
    }

    fn step(&mut self) {
        for bullet in self.bullets.values_mut() {
            bullet.previous = self.rapier.bodies[bullet.body].translation();
            bullet.lifetime = bullet.lifetime.saturating_sub(1);
        }
        self.rapier.step();
    }

    fn player_pose(&self, id: u8) -> (Vector, Vector) {
        let body = &self.rapier.bodies[self.players[usize::from(id)].body];
        (body.translation(), body.linvel())
    }

    fn player_grounded(&self, id: u8) -> bool {
        let player = self.players[usize::from(id)].collider;
        self.platforms.iter().any(|platform| {
            self.rapier
                .contact_pair(player, *platform)
                .is_some_and(|pair| pair.has_any_active_contact())
        }) || self.dynamic_bodies.values().any(|body| {
            self.rapier
                .contact_pair(player, body.collider)
                .is_some_and(|pair| pair.has_any_active_contact())
        })
    }

    fn bullet_contact(&self, id: u32, target: u8) -> bool {
        let Some(bullet) = self.bullets.get(&id) else {
            return false;
        };
        if self
            .rapier
            .contact_pair(bullet.collider, self.players[usize::from(target)].collider)
            .is_some_and(|pair| pair.has_any_active_contact())
        {
            return true;
        }
        let bullet_position = self.rapier.bodies[bullet.body].translation();
        let player_position =
            self.rapier.bodies[self.players[usize::from(target)].body].translation();
        segment_distance_squared(bullet.previous, bullet_position, player_position)
            <= (PLAYER_RADIUS + BULLET_RADIUS + 2.0).powi(2)
    }

    fn bullet_platform_contact(&self, id: u32) -> bool {
        let Some(bullet) = self.bullets.get(&id) else {
            return false;
        };
        self.platforms.iter().any(|platform| {
            self.rapier
                .contact_pair(bullet.collider, *platform)
                .is_some_and(|pair| pair.has_any_active_contact())
        })
    }

    fn reflect_bullet(&mut self, id: u32, new_owner: u8) {
        if let Some(bullet) = self.bullets.get(&id) {
            let body = &mut self.rapier.bodies[bullet.body];
            let incoming = body.linvel();
            let velocity = Vector::new(-incoming.x, incoming.x.abs() * 0.22 - incoming.y);
            body.set_linvel(velocity, true);
            let translation = body.translation() + velocity.normalize_or_zero() * 8.0;
            body.set_translation(translation, true);
            let (membership, filter) = bullet_groups(new_owner);
            self.rapier.colliders[bullet.collider].set_collision_groups(groups(membership, filter));
        }
    }

    fn bullet_pose(&self, id: u32) -> Option<(Vector, Vector, Vector, u16)> {
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

    fn remove_bullet(&mut self, id: u32) {
        if let Some(bullet) = self.bullets.remove(&id) {
            let _ = self.rapier.remove_body(bullet.body);
        }
    }
}

pub struct AuthoritativeMatch {
    world: World,
    physics: PhysicsBoundary,
    player_entities: [Entity; 2],
    projectile_entities: BTreeMap<u32, Entity>,
    dynamic_body_entities: BTreeMap<u16, Entity>,
    constraint_entities: BTreeMap<u16, Entity>,
    explosions: Vec<ExplosionSnapshot>,
    profile: ReplayProfile,
    seed: u64,
    tick: u32,
    next_projectile_id: u32,
    metrics: CombatMetrics,
    winner: Option<u8>,
    explosion_strength: f32,
    flow: Option<FlowAuthority>,
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
                    alive: true,
                    stun_ticks: 0,
                    stun_pulses_remaining: 0,
                    stun_pulse_cooldown: 0,
                })
                .id()
        });
        let dynamic_body_entities = if profile == ReplayProfile::TimberCollapseReplay {
            timber_body_definitions()
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
                        })
                        .id();
                    (id, entity)
                })
                .collect()
        } else {
            BTreeMap::new()
        };
        let constraint_entities = if profile == ReplayProfile::TimberCollapseReplay {
            timber_body_definitions()
                .into_iter()
                .map(|definition| {
                    let (id, kind, anchor, active) = match definition.shape {
                        DynamicBodyShape::Timber => (
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
            explosions: Vec::new(),
            profile,
            seed,
            tick: 0,
            next_projectile_id: 1,
            metrics: CombatMetrics::default(),
            winner: None,
            explosion_strength: TIMBER_EXPLOSION_IMPULSE,
            flow: (profile == ReplayProfile::RematchDraftReplay).then(|| FlowAuthority::new(seed)),
        };
        if profile == ReplayProfile::RematchDraftReplay {
            let mut orange = simulation.world.entity_mut(simulation.player_entities[0]);
            let mut state = orange
                .get_mut::<PlayerState>()
                .expect("orange player state");
            state.health = 0;
            state.alive = false;
            simulation.winner = Some(1);
        }
        simulation
    }

    pub fn step(&mut self, inputs: [PlayerInput; 2]) {
        self.tick += 1;
        let mut rematch_reset = false;
        if let Some(flow) = &mut self.flow {
            let had_terminal_result = flow.has_terminal_result();
            flow.advance(inputs.map(|input| input.flow));
            rematch_reset = had_terminal_result && !flow.has_terminal_result();
            if !flow.accepts_combat() {
                if rematch_reset {
                    self.reset_fighters_for_rematch();
                }
                return;
            }
        }
        if rematch_reset {
            self.reset_fighters_for_rematch();
        }
        if self.winner.is_some() && self.flow.is_none() {
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
                if input.aim_x != 0 || input.aim_y != 0 {
                    state.aim = Vector::new(f32::from(input.aim_x), f32::from(input.aim_y))
                        .normalize_or_zero();
                }
                if input.block && state.block_ticks == 0 {
                    state.block_ticks = BLOCK_DURATION;
                    self.metrics.block_activations += 1;
                }
                if state.health > 0
                    && state.stun_ticks == 0
                    && self
                        .physics
                        .set_player_control(state.id, input, state.grounded)
                {
                    self.metrics.jumps += 1;
                    state.grounded = false;
                }
                if state.health > 0
                    && state.stun_ticks == 0
                    && input.fire
                    && state.fire_cooldown == 0
                {
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
                self.physics.spawn_bullet(projectile_id, player_id, aim);
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

        if self.profile == ReplayProfile::TimberCollapseReplay && self.tick == TIMBER_IMPACT_TICK {
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
            if self.physics.bullet_contact(projectile_id, target) {
                let target_entity = self.player_entities[usize::from(target)];
                let blocking = self
                    .world
                    .entity(target_entity)
                    .get::<PlayerState>()
                    .expect("player state")
                    .block_ticks
                    > 0;
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
                let mut target_entity_mut = self.world.entity_mut(target_entity);
                let mut target_state = target_entity_mut
                    .get_mut::<PlayerState>()
                    .expect("player state");
                let damage = if self.profile == ReplayProfile::RematchDraftReplay {
                    25
                } else {
                    DAMAGE_PER_HIT
                };
                target_state.health = target_state.health.saturating_sub(damage);
                target_state.hit_flash_ticks = 6;
                if projectile.dazzle_pulses > 0 {
                    target_state.stun_pulses_remaining = projectile.dazzle_pulses;
                    target_state.stun_pulse_cooldown = 1;
                    target_state.stun_ticks = projectile.dazzle_stun_ticks;
                }
                let damage_scale = 1.0 + (100 - target_state.health) as f32 / 70.0;
                self.physics
                    .apply_impulse(target, velocity * HIT_IMPULSE * damage_scale);
                self.metrics.hits += 1;
                self.metrics.health_scaled_knockbacks += 1;
                if projectile.explosive_radius_milli > 0 {
                    let center = self
                        .physics
                        .bullet_pose(projectile_id)
                        .map(|pose| pose.0)
                        .unwrap_or_else(|| self.physics.player_pose(target).0);
                    let impulse = projectile.explosive_impulse_milli as f32 / 1_000.0;
                    self.physics.apply_impulse(target, velocity * impulse);
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
                removals.push(projectile_id);
                if target_state.health == 0 {
                    target_state.alive = false;
                    self.winner = Some(projectile.owner);
                }
            } else if self.physics.bullet_dynamic_contact(projectile_id).is_some()
                || self.physics.bullet_platform_contact(projectile_id)
            {
                self.metrics.bullet_ccd_contacts += 1;
                if projectile.explosive_radius_milli > 0
                    && let Some((center, _, _, _)) = self.physics.bullet_pose(projectile_id)
                {
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

        let mut dead = Vec::new();
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
                dead.push(id);
            }
        }
        if dead.len() == 1 {
            self.winner = Some(1 - dead[0]);
        }
    }

    fn reset_fighters_for_rematch(&mut self) {
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
        }
        self.winner = None;
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
        MatchSnapshot {
            protocol: 3,
            seed: self.seed,
            profile: self.profile.name().to_owned(),
            tick: self.tick,
            arena: if self.profile == ReplayProfile::RematchDraftReplay
                && self.flow.as_ref().is_some_and(|flow| {
                    matches!(
                        flow.snapshot().phase,
                        FlowPhase::CombatConclusion | FlowPhase::RematchPrompt
                    )
                }) {
                prior_match_arena().to_vec()
            } else {
                arena_for_profile(self.profile).to_vec()
            },
            dynamic_bodies,
            constraints,
            explosions: self.explosions.clone(),
            players,
            projectiles,
            metrics: self.metrics.clone(),
            winner: self.winner,
            flow: self.flow.as_ref().map(FlowAuthority::snapshot),
        }
    }

    pub fn state_hash(&mut self) -> String {
        hash_snapshot(&self.snapshot())
    }
}

pub fn teal_arena() -> &'static [ArenaSurfaceSnapshot] {
    const CYAN: [u8; 3] = [44, 232, 207];
    const LIME: [u8; 3] = [104, 239, 133];
    const DARK: [u8; 3] = [91, 99, 76];
    const ARENA: [ArenaSurfaceSnapshot; 19] = [
        surface(0, -520, -170, 92, 24, CYAN),
        surface(1, -390, -58, 82, 24, LIME),
        surface(2, -260, 36, 76, 24, CYAN),
        surface(3, -130, -58, 72, 24, LIME),
        surface(4, -40, 36, 72, 24, CYAN),
        surface(5, 40, 36, 72, 24, LIME),
        surface(6, 130, -58, 72, 24, CYAN),
        surface(7, 260, 36, 76, 24, LIME),
        surface(8, 390, -58, 82, 24, CYAN),
        surface(9, 520, -170, 92, 24, LIME),
        surface(10, -520, -286, 58, 32, DARK),
        surface(11, -390, -230, 58, 32, DARK),
        surface(12, -260, -190, 58, 32, DARK),
        surface(13, -130, -230, 58, 32, DARK),
        surface(14, 0, -190, 58, 32, DARK),
        surface(15, 130, -230, 58, 32, DARK),
        surface(16, 260, -190, 58, 32, DARK),
        surface(17, 390, -230, 58, 32, DARK),
        surface(18, 520, -286, 58, 32, DARK),
    ];
    &ARENA
}

pub fn timber_arena() -> &'static [ArenaSurfaceSnapshot] {
    const FLOOR: [u8; 3] = [246, 0, 79];
    const ARENA: [ArenaSurfaceSnapshot; 1] = [surface(0, 0, -300, 1_600, 60, FLOOR)];
    &ARENA
}

pub fn draft_arena() -> &'static [ArenaSurfaceSnapshot] {
    const YELLOW: [u8; 3] = [252, 224, 0];
    const ARENA: [ArenaSurfaceSnapshot; 9] = [
        surface(0, -520, -210, 170, 44, YELLOW),
        surface(1, -260, -40, 140, 44, YELLOW),
        surface(2, 0, -210, 145, 44, YELLOW),
        surface(3, 260, -40, 140, 44, YELLOW),
        surface(4, 520, -210, 170, 44, YELLOW),
        surface(5, -390, 140, 130, 44, YELLOW),
        surface(6, 0, 190, 150, 44, YELLOW),
        surface(7, 390, 140, 130, 44, YELLOW),
        surface(8, 0, -20, 100, 40, YELLOW),
    ];
    &ARENA
}

pub fn prior_match_arena() -> &'static [ArenaSurfaceSnapshot] {
    const PINK: [u8; 3] = [248, 0, 78];
    const ARENA: [ArenaSurfaceSnapshot; 9] = [
        surface(0, -520, -260, 210, 48, PINK),
        surface(1, -310, -100, 130, 38, PINK),
        surface(2, -90, 55, 120, 38, PINK),
        surface(3, 170, -80, 120, 38, PINK),
        surface(4, 440, 90, 150, 42, PINK),
        surface(5, -420, 180, 120, 38, PINK),
        surface(6, -160, 255, 145, 40, PINK),
        surface(7, 110, 190, 130, 40, PINK),
        surface(8, 520, -230, 180, 45, PINK),
    ];
    &ARENA
}

pub fn arena_for_profile(profile: ReplayProfile) -> &'static [ArenaSurfaceSnapshot] {
    match profile {
        ReplayProfile::TealDuelReplay => teal_arena(),
        ReplayProfile::RematchDraftReplay => draft_arena(),
        ReplayProfile::TimberCollapseReplay => timber_arena(),
    }
}

const fn surface(
    id: u8,
    x: i32,
    y: i32,
    width: i32,
    height: i32,
    color: [u8; 3],
) -> ArenaSurfaceSnapshot {
    ArenaSurfaceSnapshot {
        id,
        center_x_milli: x * 1_000,
        center_y_milli: y * 1_000,
        width_milli: width * 1_000,
        height_milli: height * 1_000,
        face_rgb: color,
    }
}

pub fn hash_snapshot(snapshot: &MatchSnapshot) -> String {
    let bytes = serde_json::to_vec(snapshot).expect("snapshot serialization cannot fail");
    format!("{:x}", Sha256::digest(bytes))
}

pub fn scripted_inputs(seed: u64, ticks: u32) -> [Vec<PlayerInput>; 2] {
    scripted_inputs_for(ReplayProfile::default(), seed, ticks)
}

pub fn scripted_inputs_for(
    profile: ReplayProfile,
    _seed: u64,
    ticks: u32,
) -> [Vec<PlayerInput>; 2] {
    let mut scripts = [
        Vec::with_capacity(ticks as usize),
        Vec::with_capacity(ticks as usize),
    ];
    for tick in 0..ticks {
        if profile == ReplayProfile::RematchDraftReplay {
            let mut orange = PlayerInput {
                aim_x: 1_000,
                ..PlayerInput::default()
            };
            let mut blue = PlayerInput {
                aim_x: -1_000,
                ..PlayerInput::default()
            };
            orange.flow = match tick {
                270 => Some(FlowCommand {
                    phase_revision: 1,
                    action: FlowAction::VoteYes,
                }),
                590 => Some(FlowCommand {
                    phase_revision: 3,
                    action: FlowAction::Hover(ItemId::Burst),
                }),
                660 => Some(FlowCommand {
                    phase_revision: 3,
                    action: FlowAction::Hover(ItemId::Dazzle),
                }),
                750 => Some(FlowCommand {
                    phase_revision: 3,
                    action: FlowAction::Confirm(ItemId::Dazzle),
                }),
                _ => None,
            };
            blue.flow = match tick {
                330 => Some(FlowCommand {
                    phase_revision: 1,
                    action: FlowAction::VoteYes,
                }),
                1_559 => Some(FlowCommand {
                    phase_revision: 6,
                    action: FlowAction::Hover(ItemId::Lifestealer),
                }),
                2_000 => Some(FlowCommand {
                    phase_revision: 6,
                    action: FlowAction::Hover(ItemId::Echo),
                }),
                2_060 => Some(FlowCommand {
                    phase_revision: 6,
                    action: FlowAction::Hover(ItemId::ExplosiveBullet),
                }),
                2_100 => Some(FlowCommand {
                    phase_revision: 6,
                    action: FlowAction::Confirm(ItemId::ExplosiveBullet),
                }),
                _ => None,
            };
            orange.move_axis = 0;
            blue.move_axis = 0;
            orange.fire = matches!(tick, 2_220 | 2_330);
            blue.fire = matches!(tick, 2_260 | 2_350);
            scripts[0].push(orange);
            scripts[1].push(blue);
            continue;
        }
        if profile == ReplayProfile::TimberCollapseReplay {
            let mut orange = PlayerInput {
                aim_x: 700,
                aim_y: 700,
                ..PlayerInput::default()
            };
            let mut blue = PlayerInput {
                aim_x: -700,
                aim_y: 700,
                ..PlayerInput::default()
            };
            orange.move_axis = if (120..300).contains(&tick) || (960..1_100).contains(&tick) {
                1
            } else {
                0
            };
            blue.move_axis = if (260..430).contains(&tick) {
                -1
            } else if (1_150..1_310).contains(&tick) {
                1
            } else {
                0
            };
            orange.jump = matches!(tick, 120 | 960 | 1_280);
            blue.jump = matches!(tick, 260 | 1_150);
            orange.fire = matches!(tick, 820 | 1_100);
            blue.fire = matches!(tick, 620 | 1_320);
            scripts[0].push(orange);
            scripts[1].push(blue);
            continue;
        }
        let mut orange = PlayerInput {
            aim_x: 1_000,
            ..PlayerInput::default()
        };
        let mut blue = PlayerInput {
            aim_x: -1_000,
            ..PlayerInput::default()
        };
        if (40..100).contains(&tick) {
            blue.move_axis = -1;
        }
        if (115..155).contains(&tick) {
            blue.move_axis = 1;
        }
        if (330..670).contains(&tick) {
            orange.move_axis = 1;
        }
        if (500..560).contains(&tick) {
            blue.move_axis = -1;
        }
        orange.jump = (330..670).contains(&tick);
        blue.jump = matches!(tick, 40 | 500 | 650);
        if tick == 292 {
            orange.aim_y = 180;
        }
        if tick == 416 {
            orange.aim_y = -300;
        }
        if tick == 620 {
            orange.aim_y = 500;
        }
        if tick == 785 {
            orange.aim_x = 120;
            orange.aim_y = -1_000;
        }
        if tick == 690 {
            blue.aim_x = 200;
            blue.aim_y = 1_000;
        }
        orange.fire = matches!(tick, 292 | 416 | 620 | 785);
        orange.block = (650..720).contains(&tick);
        blue.fire = tick == 690;
        blue.block = (410..450).contains(&tick);
        scripts[0].push(orange);
        scripts[1].push(blue);
    }
    scripts
}

pub fn run_scripted_match(seed: u64, ticks: u32) -> (MatchSnapshot, String) {
    run_profile_match(ReplayProfile::default(), seed, ticks)
}

pub fn run_profile_match(profile: ReplayProfile, seed: u64, ticks: u32) -> (MatchSnapshot, String) {
    let snapshot = run_profile_snapshots(profile, seed, ticks)
        .pop()
        .unwrap_or_else(|| AuthoritativeMatch::new_with_profile(seed, profile).snapshot());
    let state_hash = hash_snapshot(&snapshot);
    (snapshot, state_hash)
}

pub fn run_scripted_snapshots(seed: u64, ticks: u32) -> Vec<MatchSnapshot> {
    run_profile_snapshots(ReplayProfile::default(), seed, ticks)
}

pub fn run_profile_snapshots(profile: ReplayProfile, seed: u64, ticks: u32) -> Vec<MatchSnapshot> {
    let scripts = scripted_inputs_for(profile, seed, ticks);
    let mut simulation = AuthoritativeMatch::new_with_profile(seed, profile);
    scripts[0]
        .iter()
        .copied()
        .zip(scripts[1].iter().copied())
        .map(|(player_zero, player_one)| {
            simulation.step([player_zero, player_one]);
            simulation.snapshot()
        })
        .collect()
}

pub fn dynamic_body_digest(snapshot: &MatchSnapshot) -> String {
    let bytes = serde_json::to_vec(&snapshot.dynamic_bodies)
        .expect("dynamic body serialization cannot fail");
    format!("{:x}", Sha256::digest(bytes))
}

fn quantize(value: f32) -> i32 {
    (value * 1_000.0).round() as i32
}

fn groups(memberships: Group, filter: Group) -> InteractionGroups {
    InteractionGroups::new(memberships, filter, InteractionTestMode::And)
}

fn bullet_groups(owner: u8) -> (Group, Group) {
    if owner == 0 {
        (
            Group::GROUP_4,
            Group::GROUP_2 | Group::GROUP_3 | DYNAMIC_GROUP,
        )
    } else {
        (
            Group::GROUP_5,
            Group::GROUP_1 | Group::GROUP_3 | DYNAMIC_GROUP,
        )
    }
}

fn segment_distance_squared(start: Vector, end: Vector, point: Vector) -> f32 {
    let segment = end - start;
    let length_squared = segment.length_squared();
    if length_squared == 0.0 {
        return point.distance_squared(start);
    }
    let fraction = ((point - start).dot(segment) / length_squared).clamp(0.0, 1.0);
    point.distance_squared(start + segment * fraction)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn platform_contact_and_asymmetric_route_match_the_source_order() {
        let replay = run_profile_snapshots(ReplayProfile::TealDuelReplay, 38, TEAL_REPLAY_TICKS);
        let source_24_50 = &replay[119];
        assert!(source_24_50.players[0].x_milli < -480_000);
        assert!(source_24_50.players[0].grounded);
        assert!(source_24_50.players[1].x_milli > 250_000);
        assert!(source_24_50.players[1].y_milli > -80_000);
        assert!(source_24_50.metrics.platform_contact_ticks > 120);
        let terminal = replay.last().unwrap();
        assert!(
            terminal
                .players
                .iter()
                .all(|player| player.x_milli > 300_000 && player.y_milli > -100_000)
        );
    }

    #[test]
    fn complete_teal_duel_exercises_combat_and_selects_one_winner() {
        let first = run_profile_match(ReplayProfile::TealDuelReplay, 38, TEAL_REPLAY_TICKS);
        let second = run_profile_match(ReplayProfile::TealDuelReplay, 38, TEAL_REPLAY_TICKS);
        assert_eq!(first, second);
        let before_impact =
            run_profile_match(ReplayProfile::TealDuelReplay, 38, TEAL_REPLAY_TICKS - 1).0;
        let metrics = &first.0.metrics;
        assert_eq!(metrics.shots_fired, 5);
        assert_eq!(metrics.recoil_impulses, 5);
        assert!(metrics.block_activations >= 1);
        assert_eq!(metrics.reflections, 1);
        assert_eq!(metrics.hits, 1);
        assert_eq!(metrics.health_scaled_knockbacks, 1);
        assert_eq!(metrics.bullet_ccd_contacts, 1);
        assert_eq!(first.0.metrics.ring_outs, 0);
        assert!(
            first
                .0
                .players
                .iter()
                .all(|player| player.x_milli.abs() < 760_000)
        );
        assert_eq!(before_impact.winner, None);
        assert_eq!(first.0.winner, Some(0));
        assert!(first.0.players[1].hit_flash_ticks > 0);
    }

    #[test]
    fn ring_out_remains_a_separate_authoritative_capability() {
        let mut simulation =
            AuthoritativeMatch::new_with_profile(38, ReplayProfile::TealDuelReplay);
        let body = simulation.physics.players[1].body;
        simulation.physics.rapier.bodies[body]
            .set_translation(Vector::new(KILL_X + 10.0, 0.0), true);
        simulation.step([PlayerInput::default(); 2]);
        let snapshot = simulation.snapshot();
        assert_eq!(snapshot.metrics.ring_outs, 1);
        assert_eq!(snapshot.winner, Some(0));
    }

    #[test]
    fn rapier_ccd_stops_a_one_tick_thin_platform_crossing() {
        let mut physics = RapierWorld::new();
        physics.gravity = Vector::ZERO;
        physics.integration_parameters.dt = 1.0 / 60.0;
        physics.integration_parameters.max_ccd_substeps = 4;
        let _ = physics.insert(
            RigidBodyBuilder::fixed().translation(Vector::new(0.0, 0.0)),
            ColliderBuilder::cuboid(2.0, 100.0),
        );
        let (bullet, _) = physics.insert(
            RigidBodyBuilder::dynamic()
                .translation(Vector::new(-100.0, 0.0))
                .linvel(Vector::new(12_000.0, 0.0))
                .gravity_scale(0.0)
                .ccd_enabled(true),
            ColliderBuilder::ball(BULLET_RADIUS),
        );
        physics.step();
        assert!(physics.bodies[bullet].translation().x < 10.0);
    }

    #[test]
    fn snapshot_json_stays_bounded() {
        let (snapshot, _) = run_scripted_match(38, REPLAY_TICKS);
        let json = serde_json::to_vec(&snapshot).unwrap();
        assert!(json.len() < 32_000, "{} bytes", json.len());
    }

    #[test]
    fn timber_replay_is_repeatable_and_collapses_from_real_constraints() {
        let first = run_scripted_snapshots(40, REPLAY_TICKS);
        let second = run_scripted_snapshots(40, REPLAY_TICKS);
        assert_eq!(first, second);
        let before = &first[(TIMBER_IMPACT_TICK - 2) as usize];
        let impact = &first[(TIMBER_IMPACT_TICK - 1) as usize];
        let settled = first.last().unwrap();
        assert_eq!(
            before.dynamic_bodies.len(),
            19,
            "ids={:?}",
            before
                .dynamic_bodies
                .iter()
                .map(|body| body.id)
                .collect::<Vec<_>>()
        );
        assert!(
            before
                .constraints
                .iter()
                .all(|constraint| constraint.active)
        );
        assert_eq!(impact.explosions.len(), 1);
        assert_eq!(impact.metrics.released_constraints, 17);
        assert!(impact.metrics.explosion_impulsed_bodies >= 15);
        assert!(
            impact
                .constraints
                .iter()
                .filter(|constraint| constraint.kind == ConstraintKind::Fixed)
                .all(|constraint| !constraint.active)
        );
        assert!(
            impact
                .constraints
                .iter()
                .filter(|constraint| constraint.kind == ConstraintKind::Rope)
                .all(|constraint| constraint.active)
        );
        assert!(settled.metrics.dynamic_body_contacts > 100);
        assert!(settled.metrics.fighter_body_contact_ticks > 0);
        assert!(settled.metrics.bullet_ccd_contacts > 0);
        assert_eq!(
            settled.metrics.ring_outs, 0,
            "players={:?}",
            settled.players
        );
        assert_eq!(settled.winner, None);
        let impact_motion: i64 = first[(TIMBER_IMPACT_TICK + 59) as usize]
            .dynamic_bodies
            .iter()
            .map(|body| {
                i64::from(body.velocity_x_milli_per_second.abs())
                    + i64::from(body.velocity_y_milli_per_second.abs())
            })
            .sum();
        let settled_motion: i64 = settled
            .dynamic_bodies
            .iter()
            .map(|body| {
                i64::from(body.velocity_x_milli_per_second.abs())
                    + i64::from(body.velocity_y_milli_per_second.abs())
            })
            .sum();
        assert!(
            settled_motion < impact_motion,
            "{settled_motion} >= {impact_motion}"
        );
        assert!(
            settled
                .dynamic_bodies
                .iter()
                .all(|body| { body.x_milli.abs() < 900_000 && body.y_milli > -360_000 }),
            "bodies={:?}",
            settled
                .dynamic_bodies
                .iter()
                .map(|body| (body.id, body.x_milli, body.y_milli))
                .collect::<Vec<_>>()
        );
    }

    #[test]
    fn explosion_boundary_releases_fixed_joints_and_changes_rapier_motion() {
        let mut simulation =
            AuthoritativeMatch::new_with_profile(40, ReplayProfile::TimberCollapseReplay);
        assert_eq!(simulation.physics.dynamic_bodies.len(), 19);
        assert_eq!(simulation.physics.constraints.len(), 19);
        assert_eq!(simulation.physics.rapier.impulse_joints.len(), 19);
        for _ in 0..TIMBER_IMPACT_TICK {
            simulation.step([PlayerInput::default(); 2]);
        }
        assert_eq!(simulation.physics.rapier.impulse_joints.len(), 2);
        let snapshot = simulation.snapshot();
        assert!(snapshot.dynamic_bodies.iter().any(|body| {
            body.velocity_x_milli_per_second != 0 || body.velocity_y_milli_per_second != 0
        }));
    }

    #[test]
    fn explosion_impulse_perturbation_changes_dynamic_body_poses() {
        let mut nominal =
            AuthoritativeMatch::new_with_profile(40, ReplayProfile::TimberCollapseReplay);
        let mut perturbed =
            AuthoritativeMatch::new_with_profile(40, ReplayProfile::TimberCollapseReplay);
        perturbed.explosion_strength *= 0.92;
        let scripts = scripted_inputs_for(ReplayProfile::TimberCollapseReplay, 40, REPLAY_TICKS);
        for (nominal_input, perturbed_input) in
            scripts[0].iter().copied().zip(scripts[1].iter().copied())
        {
            nominal.step([nominal_input, perturbed_input]);
            perturbed.step([nominal_input, perturbed_input]);
        }
        let nominal = nominal.snapshot();
        let perturbed = perturbed.snapshot();
        assert_ne!(
            dynamic_body_digest(&nominal),
            dynamic_body_digest(&perturbed)
        );
        assert!(
            nominal
                .dynamic_bodies
                .iter()
                .zip(&perturbed.dynamic_bodies)
                .any(|(left, right)| {
                    left.x_milli != right.x_milli
                        || left.y_milli != right.y_milli
                        || left.rotation_milliradians != right.rotation_milliradians
                })
        );
    }

    #[test]
    fn rematch_clears_the_terminal_winner_and_elimination_in_match_state() {
        let mut simulation = AuthoritativeMatch::new_with_profile(
            SOURCE_DRAFT_SEED,
            ReplayProfile::RematchDraftReplay,
        );
        let concluded = simulation.snapshot();
        assert_eq!(concluded.winner, Some(1));
        assert!(!concluded.players[0].alive);
        assert_eq!(concluded.players[0].health, 0);
        assert!(concluded.players[1].alive);

        let scripts =
            scripted_inputs_for(ReplayProfile::RematchDraftReplay, SOURCE_DRAFT_SEED, 331);
        for (&orange, &blue) in scripts[0].iter().zip(&scripts[1]) {
            simulation.step([orange, blue]);
        }
        let reset = simulation.snapshot();
        assert_eq!(reset.winner, None);
        assert!(reset.players.iter().all(|player| player.alive));
        assert!(reset.players.iter().all(|player| player.health == 100));
        let flow = reset.flow.unwrap();
        assert_eq!(flow.scores, [0, 0]);
        assert!(flow.prior_badges.iter().all(Vec::is_empty));
    }

    #[test]
    fn selected_cards_mark_real_projectiles_and_apply_typed_impact_behaviors() {
        let mut dazzle = AuthoritativeMatch::new_with_profile(
            SOURCE_DRAFT_SEED,
            ReplayProfile::RematchDraftReplay,
        );
        let scripts =
            scripted_inputs_for(ReplayProfile::RematchDraftReplay, SOURCE_DRAFT_SEED, 2_221);
        for (&orange, &blue) in scripts[0].iter().zip(&scripts[1]).take(2_220) {
            dazzle.step([orange, blue]);
        }
        dazzle.physics.rapier.bodies[dazzle.physics.players[0].body]
            .set_translation(Vector::new(-120.0, 80.0), true);
        dazzle.physics.rapier.bodies[dazzle.physics.players[1].body]
            .set_translation(Vector::new(120.0, 80.0), true);
        for entity in dazzle.player_entities {
            let mut state = dazzle.world.entity_mut(entity);
            let mut player = state.get_mut::<PlayerState>().unwrap();
            player.alive = true;
            player.health = 100;
            player.fire_cooldown = 0;
            player.stun_ticks = 0;
            player.stun_pulses_remaining = 0;
        }
        dazzle.step([scripts[0][2_220], PlayerInput::default()]);
        let fired = dazzle.snapshot();
        assert!(
            fired
                .projectiles
                .iter()
                .any(|bullet| bullet.owner == 0 && bullet.dazzle_pulses == 3)
        );
        for _ in 0..12 {
            dazzle.step([PlayerInput::default(); 2]);
        }
        assert!(dazzle.snapshot().metrics.dazzle_stun_pulses >= 1);

        let mut explosive = AuthoritativeMatch::new_with_profile(
            SOURCE_DRAFT_SEED,
            ReplayProfile::RematchDraftReplay,
        );
        for (&orange, &blue) in scripts[0].iter().zip(&scripts[1]) {
            explosive.step([orange, blue]);
        }
        for _ in scripts[0].len()..2_240 {
            explosive.step([PlayerInput::default(); 2]);
        }
        explosive.physics.rapier.bodies[explosive.physics.players[0].body]
            .set_translation(Vector::new(-120.0, 80.0), true);
        explosive.physics.rapier.bodies[explosive.physics.players[1].body]
            .set_translation(Vector::new(120.0, 80.0), true);
        for entity in explosive.player_entities {
            let mut state = explosive.world.entity_mut(entity);
            let mut player = state.get_mut::<PlayerState>().unwrap();
            player.alive = true;
            player.health = 100;
            player.fire_cooldown = 0;
            player.stun_ticks = 0;
            player.stun_pulses_remaining = 0;
        }
        explosive.step([
            PlayerInput::default(),
            PlayerInput {
                fire: true,
                aim_x: -1_000,
                ..PlayerInput::default()
            },
        ]);
        let fired = explosive.snapshot();
        assert!(
            fired
                .projectiles
                .iter()
                .any(|bullet| bullet.owner == 1 && bullet.explosive_radius_milli == 150_000)
        );
        for _ in 0..5 {
            explosive.step([PlayerInput::default(); 2]);
        }
        let impact = explosive.snapshot();
        assert_eq!(impact.metrics.explosive_projectile_impacts, 1);
        assert!(
            impact
                .explosions
                .iter()
                .any(|explosion| explosion.id >= 10_000)
        );
    }
}
