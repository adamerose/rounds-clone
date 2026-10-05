use bevy_ecs::prelude::*;
use bevy_rapier2d::rapier::prelude::{
    ColliderBuilder, ColliderHandle, PhysicsWorld as RapierWorld, RigidBodyBuilder,
    RigidBodyHandle, Rotation, Vector,
};
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use std::collections::BTreeMap;

pub const TICKS_PER_SECOND: u32 = 60;
pub const MAX_INSPECTED_PROJECTILES: usize = 64;
const PLAYER_RADIUS: f32 = 22.0;
const RUN_SPEED: f32 = 220.0;
const AIR_CONTROL: f32 = 0.08;
const JUMP_SPEED: f32 = 680.0;
const JUMP_RELEASE_CUT: f32 = 0.30;
const BULLET_RADIUS: f32 = 5.0;
pub const BULLET_SPEED: f32 = 3_600.0;
const BULLET_LIFETIME: u16 = 150;
const FIRE_COOLDOWN: u16 = 24;
const BLOCK_DURATION: u16 = 18;
const DAMAGE_PER_HIT: u16 = 100;
const RECOIL_IMPULSE: f32 = 72.0;
const HIT_IMPULSE: f32 = 420.0;
const KILL_X: f32 = 760.0;
const KILL_Y: f32 = -440.0;

mod arena;
mod arena_data;
mod flow;
mod physics;
mod replay;
mod snapshots;

use arena::collider_for_surface;
pub use arena_data::*;
pub use flow::*;
pub use physics::*;
pub use replay::*;
pub use snapshots::*;
