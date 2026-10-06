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

mod arena;
mod arena_data;
mod cards;
pub use cards::*;
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

mod tuning;
pub use tuning::*;
