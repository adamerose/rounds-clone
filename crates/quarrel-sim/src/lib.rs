use bevy_ecs::prelude::*;
use bevy_rapier2d::rapier::prelude::{
    ColliderBuilder, ColliderHandle, FixedJointBuilder, GenericJoint, Group, ImpulseJointHandle,
    InteractionGroups, InteractionTestMode, PhysicsWorld as RapierWorld, RigidBodyBuilder,
    RigidBodyHandle, RopeJointBuilder, Rotation, SharedShape, Vector,
};
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use std::collections::BTreeMap;

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
pub const RADIAL_REPLAY_TICKS: u32 = 938;
pub const RADIAL_REPLAY_PROFILE: &str = "radial-saw-half-blue-replay";
pub const RADIAL_SOURCE_INTERVAL: &str = "03:52.049072-04:07.682343";
pub const RADIAL_SOURCE_START_PTS: i64 = 2_320_490_718;
pub const RADIAL_LAST_COMBAT_TICK: u32 = 908;
pub const RADIAL_RESULT_ONSET_TICK: u32 = 909;
pub const RADIAL_HALF_BLUE_TICK: u32 = 938;
pub const YELLOW_REPLAY_TICKS: u32 = 155;
pub const YELLOW_REPLAY_PROFILE: &str = "yellow-crate-terminal-blast-replay";
pub const YELLOW_SOURCE_INTERVAL: &str = "07:02.014979-07:04.598302";
pub const YELLOW_SOURCE_START_PTS: i64 = 4_220_149_786;
pub const YELLOW_LAST_CALM_TICK: u32 = 80;
pub const YELLOW_IMPACT_TICK: u32 = 81;
pub const YELLOW_LOCAL_BURST_TICK: u32 = 84;
pub const YELLOW_PEAK_ECHO_TICK: u32 = 89;
pub const YELLOW_TRAILS_TICK: u32 = 102;
pub const YELLOW_LAST_COMBAT_TICK: u32 = 109;
pub const YELLOW_RESULT_ONSET_TICK: u32 = 110;
pub const YELLOW_FOLLOWING_RESULT_TICK: u32 = 111;
pub const YELLOW_ROUND_ORANGE_TICK: u32 = 125;
pub const MATCH_END_WAITING_REPLAY_TICKS: u32 = 240;
pub const MATCH_END_WAITING_REPLAY_PROFILE: &str = "match-end-waiting-replay";
pub const MATCH_END_WAITING_SOURCE_INTERVAL: &str = "03:20.015867";
pub const MATCH_END_WAITING_SOURCE_PTS: i64 = 2_000_158_666;
pub const MATCH_END_WAITING_SOURCE_RGBA_SHA256: &str =
    "c4c9547151263157cf54afe9495c7f1b1103cc3c2da8d3b29314bb0c57a09a6d";
pub const MATCH_END_WAITING_CONSTRUCTED_PREHISTORY: &str =
    "constructed 3-4 completed rounds with one half each";
pub const MATCH_END_DECISIVE_IMPACT_TICK: u32 = 17;
pub const MATCH_END_RESULT_TRANSITION_TICK: u32 = 44;
pub const MATCH_END_ROUND_BLUE_TICK: u32 = 60;
pub const MATCH_END_WAITING_TICK: u32 = 199;
pub const NEW_MATCH_DRAFT_FADE_TICKS: u32 = 150;
pub const NEW_MATCH_DRAFT_SOURCE_PTS: i64 = 2_039_991_840;
pub const NEW_MATCH_DRAFT_SOURCE_RGBA_SHA256: &str =
    "7f8703810079f5beef741953d896175d70ee5602fe997b37be3349308c218da0";
pub const LIME_MODULAR_REPLAY_TICKS: u32 = 360;
pub const LIME_MODULAR_REPLAY_PROFILE: &str = "lime-modular-arena-replay";
pub const LIME_MODULAR_SOURCE_INTERVAL: &str = "03:34.015811-03:40.015787";
pub const LIME_MODULAR_SOURCE_START_PTS: i64 = 2_140_158_106;

const PLAYER_RADIUS: f32 = 22.0;
const RUN_SPEED: f32 = 220.0;
const AIR_CONTROL: f32 = 0.08;
const JUMP_SPEED: f32 = 680.0;
/// Fraction of its upward velocity a fighter keeps when it lets go of the jump
/// input while airborne and still rising. Fitted in ticket 050 against the
/// twenty-six measured source arcs.
const JUMP_RELEASE_CUT: f32 = 0.30;
const BULLET_RADIUS: f32 = 5.0;
pub const BULLET_SPEED: f32 = 3_600.0;
const BULLET_LIFETIME: u16 = 150;
const FIRE_COOLDOWN: u16 = 24;
const BLOCK_DURATION: u16 = 18;
const DAMAGE_PER_HIT: u16 = 100;
const RECOIL_IMPULSE: f32 = 72.0;
const HIT_IMPULSE: f32 = 420.0;
const RADIAL_HIT_IMPULSE: f32 = 140.0;
const KILL_X: f32 = 760.0;
const KILL_Y: f32 = -440.0;
const DYNAMIC_GROUP: Group = Group::GROUP_6;
const SAW_GROUP: Group = Group::GROUP_7;
const TIMBER_EXPLOSION_CENTER: Vector = Vector::new(-245.0, 135.0);
const TIMBER_EXPLOSION_RADIUS: f32 = 520.0;
const TIMBER_EXPLOSION_IMPULSE: f32 = 4_800.0;
const YELLOW_EXPLOSION_RADIUS: f32 = 330.0;
const YELLOW_EXPLOSION_IMPULSE: f32 = 2_850.0;

mod arena;
mod arena_data;
mod flow;
mod physics;
mod replay;
mod snapshots;

pub use arena::*;
pub use arena_data::*;
pub use flow::*;
pub use physics::*;
pub use replay::*;
pub use snapshots::*;
