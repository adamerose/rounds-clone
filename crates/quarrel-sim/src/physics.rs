use super::*;
#[derive(Component, Clone, Copy)]
struct PlayerState {
    id: u8,
    aim: Vector,
    health: u16,
    fire_cooldown: u16,
    block_ticks: u16,
    hit_flash_ticks: u8,
    grounded: bool,
    /// Last tick's jump input, so `step` can see the tick it is let go of. It
    /// follows the input on every tick the authority reads it, whether or not
    /// control runs for this fighter, and is cleared with the rest of the
    /// transient control state on a revive. Authority-internal: it reaches no
    /// serialised projection.
    jump_held: bool,
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

#[derive(Clone, Copy)]
struct PendingRadialHit {
    target: u8,
    direction: Vector,
}

#[derive(Component, Clone, Copy)]
struct DynamicBodyState {
    id: u16,
    shape: DynamicBodyShape,
    width: f32,
    height: f32,
    radius: f32,
    face_rgb: [u8; 3],
    mass: f32,
    friction: f32,
    restitution: f32,
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

#[derive(Component, Clone, Copy)]
struct SawState {
    id: u16,
    radius: f32,
    teeth: u8,
    initial_position: Vector,
    initial_angle: f32,
    angular_velocity: f32,
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
struct SawPhysics {
    body: RigidBodyHandle,
    collider: ColliderHandle,
}

#[derive(Clone, Copy)]
struct SawDefinition {
    id: u16,
    position: Vector,
    radius: f32,
    teeth: u8,
    initial_angle: f32,
    angular_velocity: f32,
}

const RADIAL_SAWS: [SawDefinition; 2] = [
    SawDefinition {
        id: 200,
        position: Vector::new(0.0, 0.0),
        radius: 82.0,
        teeth: 8,
        initial_angle: 0.18,
        angular_velocity: 7.43,
    },
    SawDefinition {
        id: 201,
        position: Vector::new(0.0, -348.0),
        radius: 76.0,
        teeth: 8,
        initial_angle: 0.51,
        angular_velocity: 7.43,
    },
];

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
    mass: f32,
    friction: f32,
    restitution: f32,
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
        mass: 1.0,
        friction: 0.82,
        restitution: 0.08,
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
            mass: 1.2,
            friction: 0.82,
            restitution: 0.08,
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
            mass: 1.2,
            friction: 0.82,
            restitution: 0.08,
        },
    ]
}

fn yellow_crate_definitions() -> Vec<DynamicBodyDefinition> {
    const BROWN: [u8; 3] = [151, 101, 23];
    let crate_body = |id, x, y, rotation, width, height| DynamicBodyDefinition {
        id,
        shape: DynamicBodyShape::Crate,
        position: Vector::new(x, y),
        rotation,
        width,
        height,
        radius: 0.0,
        face_rgb: BROWN,
        mass: 0.78,
        friction: 0.76,
        restitution: 0.12,
    };
    vec![
        crate_body(300, -485.0, 207.0, 0.0, 34.0, 48.0),
        crate_body(301, -450.0, 207.0, 0.0, 34.0, 48.0),
        crate_body(302, -468.0, 249.0, 0.0, 34.0, 36.0),
        crate_body(303, -165.0, 207.0, 0.0, 34.0, 48.0),
        crate_body(304, 140.0, 207.0, 0.0, 34.0, 48.0),
        crate_body(305, 178.0, 207.0, 0.0, 34.0, 48.0),
        crate_body(306, -610.0, 67.0, 0.0, 34.0, 48.0),
        crate_body(307, -324.0, 67.0, 0.0, 34.0, 48.0),
        crate_body(308, -286.0, 67.0, 0.0, 34.0, 48.0),
        crate_body(309, 0.0, 67.0, 0.0, 34.0, 48.0),
        crate_body(310, 38.0, 67.0, 0.0, 34.0, 48.0),
        crate_body(311, 322.0, 67.0, 0.0, 34.0, 48.0),
        crate_body(312, 360.0, 67.0, 0.0, 34.0, 48.0),
        crate_body(313, 405.0, 207.0, 0.0, 34.0, 48.0),
        crate_body(314, 442.0, 207.0, 0.0, 34.0, 48.0),
        crate_body(315, 424.0, 249.0, 0.0, 36.0, 36.0),
        crate_body(316, -485.0, -93.0, 0.0, 34.0, 48.0),
        crate_body(317, -165.0, -93.0, 0.0, 34.0, 48.0),
        crate_body(318, 155.0, -93.0, 0.0, 34.0, 48.0),
        crate_body(319, 475.0, -93.0, 0.0, 34.0, 48.0),
    ]
}

struct PhysicsBoundary {
    rapier: RapierWorld,
    players: [PlayerPhysics; 2],
    /// Current arena spawn positions, reused when a fight repeats in place.
    spawns: [Vector; 2],
    platforms: Vec<ColliderHandle>,
    retired_platforms: Vec<ColliderHandle>,
    timber_anchor: Option<RigidBodyHandle>,
    bullets: BTreeMap<u32, BulletPhysics>,
    dynamic_bodies: BTreeMap<u16, DynamicBodyPhysics>,
    constraints: BTreeMap<u16, ConstraintPhysics>,
    saws: BTreeMap<u16, SawPhysics>,
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
                let shape = collider_for_surface(surface);
                let (_, collider) = rapier.insert(
                    RigidBodyBuilder::fixed().translation(Vector::new(
                        surface.center_x_milli as f32 / 1_000.0,
                        surface.center_y_milli as f32 / 1_000.0,
                    )),
                    shape
                        .rotation(surface.rotation_milliradians as f32 / 1_000.0)
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
            ReplayProfile::MatchEndWaitingReplay => [(-520.0, -134.0, 0_u8), (520.0, -134.0, 1_u8)],
            ReplayProfile::LimeModularArenaReplay => [(-405.0, -58.0, 0_u8), (405.0, -58.0, 1_u8)],
            ReplayProfile::RadialSawHalfBlueReplay => [(-285.0, 118.0, 0_u8), (285.0, 118.0, 1_u8)],
            ReplayProfile::YellowCrateTerminalBlastReplay => {
                [(220.0, 292.0, 0_u8), (570.0, 292.0, 1_u8)]
            }
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
                    .gravity_scale(
                        if profile == ReplayProfile::YellowCrateTerminalBlastReplay {
                            0.0
                        } else {
                            1.0
                        },
                    )
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
            spawns: player_spawns.map(|(x, y, _)| Vector::new(x, y)),
            platforms,
            retired_platforms: Vec::new(),
            timber_anchor: None,
            bullets: BTreeMap::new(),
            dynamic_bodies: BTreeMap::new(),
            constraints: BTreeMap::new(),
            saws: BTreeMap::new(),
        };
        if profile == ReplayProfile::TimberCollapseReplay {
            boundary.insert_timber_structure();
        }
        if profile == ReplayProfile::YellowCrateTerminalBlastReplay {
            boundary.insert_yellow_crates();
        }
        if profile == ReplayProfile::RadialSawHalfBlueReplay {
            boundary.insert_radial_saws();
        }
        boundary
    }

    fn insert_radial_saws(&mut self) {
        for definition in RADIAL_SAWS {
            let (body, collider) = self.rapier.insert(
                RigidBodyBuilder::kinematic_velocity_based()
                    .translation(definition.position)
                    .rotation(definition.initial_angle)
                    .angvel(definition.angular_velocity)
                    .can_sleep(false),
                ColliderBuilder::cuboid(definition.radius * 0.57, definition.radius * 0.57)
                    .friction(0.0)
                    .restitution(0.85)
                    .collision_groups(groups(SAW_GROUP, Group::NONE)),
            );
            self.saws
                .insert(definition.id, SawPhysics { body, collider });
        }
    }

    fn reset_saw(&mut self, id: u16, position: Vector, angle: f32, angular_velocity: f32) {
        let physics = self
            .saws
            .get(&id)
            .expect("radial saw registry remains complete");
        let body = &mut self.rapier.bodies[physics.body];
        body.set_translation(position, true);
        body.set_rotation(Rotation::new(angle), true);
        body.set_linvel(Vector::ZERO, true);
        body.set_angvel(angular_velocity, true);
        self.rapier
            .bodies
            .propagate_modified_body_positions_to_colliders(&mut self.rapier.colliders);
    }

    fn saw_pose(&self, id: u16) -> Option<(Vector, f32, f32)> {
        self.saws.get(&id).map(|physics| {
            let body = &self.rapier.bodies[physics.body];
            let collider = &self.rapier.colliders[physics.collider];
            (
                collider.position().translation,
                collider.position().rotation.angle(),
                body.angvel(),
            )
        })
    }

    fn insert_timber_structure(&mut self) {
        let anchor = self.rapier.insert_body(RigidBodyBuilder::fixed());
        self.timber_anchor = Some(anchor);
        for definition in timber_body_definitions() {
            let body_builder = RigidBodyBuilder::dynamic()
                .translation(definition.position)
                .rotation(definition.rotation)
                .linear_damping(1.1)
                .angular_damping(1.6)
                .ccd_enabled(true);
            let collider_builder = match definition.shape {
                DynamicBodyShape::Timber | DynamicBodyShape::Crate => {
                    ColliderBuilder::cuboid(definition.width * 0.5, definition.height * 0.5)
                }
                DynamicBodyShape::Weight => ColliderBuilder::ball(definition.radius),
            }
            .density(if definition.shape == DynamicBodyShape::Weight {
                0.008
            } else {
                0.0045
            })
            .friction(definition.friction)
            .restitution(definition.restitution)
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
                    DynamicBodyShape::Timber | DynamicBodyShape::Crate => (
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

    fn load_ice_arena(&mut self) {
        for constraint in std::mem::take(&mut self.constraints).into_values() {
            self.rapier.impulse_joints.remove(constraint.handle, true);
        }
        for body in std::mem::take(&mut self.dynamic_bodies).into_values() {
            self.rapier.remove_body(body.body);
        }
        if let Some(anchor) = self.timber_anchor.take() {
            self.rapier.remove_body(anchor);
        }
        for collider in self
            .platforms
            .drain(..)
            .chain(self.retired_platforms.drain(..))
        {
            if let Some(body) = self.rapier.colliders[collider].parent() {
                self.rapier.remove_body(body);
            }
        }
        for id in self.bullets.keys().copied().collect::<Vec<_>>() {
            self.remove_bullet(id);
        }
        self.platforms = ice_arena()
            .iter()
            .map(|surface| {
                let vertices = surface
                    .outline_milli
                    .iter()
                    .map(|p| Vector::new(p[0] as f32 / 1_000.0, p[1] as f32 / 1_000.0))
                    .collect::<Vec<_>>();
                let edges = (0..vertices.len())
                    .map(|i| [i as u32, ((i + 1) % vertices.len()) as u32])
                    .collect::<Vec<_>>();
                let shape = ColliderBuilder::convex_decomposition(&vertices, &edges);
                self.rapier
                    .insert(
                        RigidBodyBuilder::fixed().translation(Vector::new(
                            surface.center_x_milli as f32 / 1_000.0,
                            surface.center_y_milli as f32 / 1_000.0,
                        )),
                        shape
                            .rotation(surface.rotation_milliradians as f32 / 1_000.0)
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
                    )
                    .1
            })
            .collect();
        self.spawns = [Vector::new(-529.0, -102.0), Vector::new(519.0, -102.0)];
        self.respawn_players();
        for player in self.players {
            self.rapier.colliders[player.collider].set_shape(SharedShape::ball(12.0));
        }
    }

    fn load_held_hanging_entry(&mut self) {
        for constraint in std::mem::take(&mut self.constraints).into_values() {
            self.rapier.impulse_joints.remove(constraint.handle, true);
        }
        for body in std::mem::take(&mut self.dynamic_bodies).into_values() {
            self.rapier.remove_body(body.body);
        }
        if let Some(anchor) = self.timber_anchor.take() {
            self.rapier.remove_body(anchor);
        }
        for collider in self
            .platforms
            .drain(..)
            .chain(self.retired_platforms.drain(..))
        {
            if let Some(body) = self.rapier.colliders[collider].parent() {
                self.rapier.remove_body(body);
            }
        }
        for id in self.bullets.keys().copied().collect::<Vec<_>>() {
            self.remove_bullet(id);
        }
        self.spawns = [Vector::new(-405.0, 53.0), Vector::new(405.0, 87.0)];
        self.respawn_players();
    }

    fn load_timber_arena(&mut self) {
        // Keep the established Rapier insertion order for the connected timber
        // contacts. These colliders cannot collide; ice loading removes them.
        for collider in self.platforms.drain(..) {
            self.rapier.colliders[collider].set_collision_groups(groups(Group::NONE, Group::NONE));
            self.retired_platforms.push(collider);
        }
        let bullet_ids = self.bullets.keys().copied().collect::<Vec<_>>();
        for id in bullet_ids {
            self.remove_bullet(id);
        }
        self.platforms = timber_arena()
            .iter()
            .map(|surface| {
                let (_, collider) = self.rapier.insert(
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
            .collect();
        self.spawns = [Vector::new(-500.0, -210.0), Vector::new(500.0, -210.0)];
        self.respawn_players();
        self.insert_timber_structure();
        self.rapier
            .bodies
            .propagate_modified_body_positions_to_colliders(&mut self.rapier.colliders);
    }

    /// Returns both fighters to the current arena spawns at rest.
    fn respawn_players(&mut self) {
        for (id, position) in self.spawns.into_iter().enumerate() {
            let body = &mut self.rapier.bodies[self.players[id].body];
            body.set_translation(position, true);
            body.set_linvel(Vector::ZERO, true);
        }
        self.rapier
            .bodies
            .propagate_modified_body_positions_to_colliders(&mut self.rapier.colliders);
    }

    fn insert_yellow_crates(&mut self) {
        for definition in yellow_crate_definitions() {
            let (body, collider) = self.rapier.insert(
                RigidBodyBuilder::dynamic()
                    .translation(definition.position)
                    .rotation(definition.rotation)
                    .linear_damping(0.36)
                    .angular_damping(0.28)
                    .additional_mass(definition.mass)
                    .ccd_enabled(true),
                ColliderBuilder::cuboid(definition.width * 0.5, definition.height * 0.5)
                    .density(0.002)
                    .friction(definition.friction)
                    .restitution(definition.restitution)
                    .collision_groups(groups(
                        DYNAMIC_GROUP,
                        Group::GROUP_1
                            | Group::GROUP_2
                            | Group::GROUP_3
                            | Group::GROUP_4
                            | Group::GROUP_5
                            | DYNAMIC_GROUP,
                    )),
            );
            self.dynamic_bodies
                .insert(definition.id, DynamicBodyPhysics { body, collider });
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

    fn bullet_dynamic_contact(&self, id: u32) -> Option<(u16, Vector)> {
        let bullet = self.bullets.get(&id)?;
        self.dynamic_bodies.iter().find_map(|(body_id, body)| {
            let pair = self.rapier.contact_pair(bullet.collider, body.collider)?;
            if !pair.has_any_active_contact() {
                return None;
            }
            pair.manifolds.iter().find_map(|manifold| {
                let contact = manifold.data.solver_contacts.first()?;
                let (first, second) = manifold
                    .data
                    .solver_contact_world_points(contact, &self.rapier.bodies);
                Some((
                    *body_id,
                    if pair.collider1 == bullet.collider {
                        second
                    } else {
                        first
                    },
                ))
            })
        })
    }

    /// `released` is the fighter's own held-to-released transition on this tick,
    /// decided by the caller from the input it read, so a release the fighter
    /// could not act on is never carried forward to a later tick.
    fn set_player_control(
        &mut self,
        id: u8,
        input: PlayerInput,
        grounded: bool,
        released: bool,
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
        if grounded || input.move_axis != 0 {
            velocity.x += (f32::from(input.move_axis) * RUN_SPEED - velocity.x) * control;
        }
        // A jump the fighter lets go of stops rising. The cut fires once, on the
        // tick the input goes from held to released, and only while the fighter
        // is off the ground and still going up; a release read on a grounded tick
        // does nothing at all, whatever the vertical velocity.
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

    fn player_radius(&self, id: u8) -> f32 {
        self.rapier.colliders[self.players[usize::from(id)].collider]
            .shape()
            .as_ball()
            .expect("fighter circle")
            .radius
    }

    fn spawn_bullet(
        &mut self,
        id: u32,
        owner: u8,
        aim: Vector,
        launch_speed: f32,
        collide_with_arena: bool,
    ) {
        let shooter = &self.rapier.bodies[self.players[usize::from(owner)].body];
        let origin =
            shooter.translation() + aim * (self.player_radius(owner) + BULLET_RADIUS + 4.0);
        let (membership, filter) = bullet_groups(owner, collide_with_arena);
        let (body, collider) = self.rapier.insert(
            RigidBodyBuilder::dynamic()
                .translation(origin)
                .linvel(aim * launch_speed)
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

    fn bullet_contact(&self, id: u32, target: u8) -> Option<Vector> {
        let bullet = self.bullets.get(&id)?;
        if let Some(pair) = self
            .rapier
            .contact_pair(bullet.collider, self.players[usize::from(target)].collider)
            && let Some(point) = pair.solver_manifolds().iter().find_map(|manifold| {
                let contact = manifold.data.solver_contacts.first()?;
                let (first, second) = manifold
                    .data
                    .solver_contact_world_points(contact, &self.rapier.bodies);
                Some(if pair.collider1 == bullet.collider {
                    second
                } else {
                    first
                })
            })
        {
            return Some(point);
        }
        let bullet_position = self.rapier.bodies[bullet.body].translation();
        let player_position = self.player_pose(target).0;
        let radius = self.player_radius(target);
        let segment = bullet_position - bullet.previous;
        let length_squared = segment.length_squared();
        let fraction = if length_squared > 0.0 {
            ((player_position - bullet.previous).dot(segment) / length_squared).clamp(0.0, 1.0)
        } else {
            0.0
        };
        let closest = bullet.previous + segment * fraction;
        let contact_radius = radius + BULLET_RADIUS + 2.0;
        if closest.distance_squared(player_position) > contact_radius.powi(2) {
            return None;
        }
        // Preserve the existing swept-hit tolerance, but locate the first contact
        // on the fighter instead of publishing the projectile's later endpoint.
        let start = bullet.previous - player_position;
        let entry = if start.length_squared() > contact_radius.powi(2) && length_squared > 0.0 {
            let projection = start.dot(segment);
            let discriminant = projection.powi(2)
                - length_squared * (start.length_squared() - contact_radius.powi(2));
            let time =
                ((-projection - discriminant.max(0.0).sqrt()) / length_squared).clamp(0.0, 1.0);
            start + segment * time
        } else {
            start
        };
        Some(player_position + entry.normalize_or_zero() * radius)
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
            let (membership, filter) = bullet_groups(new_owner, true);
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

mod combat;
pub use combat::*;

fn groups(memberships: Group, filter: Group) -> InteractionGroups {
    InteractionGroups::new(memberships, filter, InteractionTestMode::And)
}

fn bullet_groups(owner: u8, collide_with_arena: bool) -> (Group, Group) {
    let arena = if collide_with_arena {
        Group::GROUP_3
    } else {
        Group::NONE
    };
    if owner == 0 {
        (Group::GROUP_4, Group::GROUP_2 | arena | DYNAMIC_GROUP)
    } else {
        (Group::GROUP_5, Group::GROUP_1 | arena | DYNAMIC_GROUP)
    }
}
