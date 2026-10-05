use super::*;
pub fn projectile_launch_speed(capabilities: FighterCapabilities) -> f32 {
    BULLET_SPEED * f32::from(capabilities.projectile_speed_factor.milli) / 1_000.0
}

#[derive(Clone, Copy, Debug, Default, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "kebab-case")]
pub enum ReplayProfile {
    TealDuelReplay,
    RematchDraftReplay,
    MatchEndWaitingReplay,
    LimeModularArenaReplay,
    RadialSawHalfBlueReplay,
    YellowCrateTerminalBlastReplay,
    #[default]
    TimberCollapseReplay,
}

impl ReplayProfile {
    pub fn name(self) -> &'static str {
        match self {
            Self::TealDuelReplay => TEAL_REPLAY_PROFILE,
            Self::RematchDraftReplay => REMATCH_DRAFT_PROFILE,
            Self::MatchEndWaitingReplay => MATCH_END_WAITING_REPLAY_PROFILE,
            Self::LimeModularArenaReplay => LIME_MODULAR_REPLAY_PROFILE,
            Self::RadialSawHalfBlueReplay => RADIAL_REPLAY_PROFILE,
            Self::YellowCrateTerminalBlastReplay => YELLOW_REPLAY_PROFILE,
            Self::TimberCollapseReplay => REPLAY_PROFILE,
        }
    }

    pub fn replay_ticks(self) -> u32 {
        match self {
            Self::TealDuelReplay => TEAL_REPLAY_TICKS,
            Self::RematchDraftReplay => REMATCH_DRAFT_TICKS,
            Self::MatchEndWaitingReplay => MATCH_END_WAITING_REPLAY_TICKS,
            Self::LimeModularArenaReplay => LIME_MODULAR_REPLAY_TICKS,
            Self::RadialSawHalfBlueReplay => RADIAL_REPLAY_TICKS,
            Self::YellowCrateTerminalBlastReplay => YELLOW_REPLAY_TICKS,
            Self::TimberCollapseReplay => REPLAY_TICKS,
        }
    }

    pub fn source_interval(self) -> &'static str {
        match self {
            Self::TealDuelReplay => TEAL_SOURCE_INTERVAL,
            Self::RematchDraftReplay => REMATCH_DRAFT_SOURCE_INTERVAL,
            Self::MatchEndWaitingReplay => MATCH_END_WAITING_SOURCE_INTERVAL,
            Self::LimeModularArenaReplay => LIME_MODULAR_SOURCE_INTERVAL,
            Self::RadialSawHalfBlueReplay => RADIAL_SOURCE_INTERVAL,
            Self::YellowCrateTerminalBlastReplay => YELLOW_SOURCE_INTERVAL,
            Self::TimberCollapseReplay => SOURCE_INTERVAL,
        }
    }

    pub fn source_sha256(self) -> &'static str {
        match self {
            Self::TealDuelReplay => TEAL_SOURCE_SHA256,
            Self::RematchDraftReplay => SOURCE_SHA256,
            Self::MatchEndWaitingReplay => TEAL_SOURCE_SHA256,
            Self::LimeModularArenaReplay => TEAL_SOURCE_SHA256,
            Self::RadialSawHalfBlueReplay => TEAL_SOURCE_SHA256,
            Self::YellowCrateTerminalBlastReplay => SOURCE_SHA256,
            Self::TimberCollapseReplay => SOURCE_SHA256,
        }
    }

    pub fn source_start_hundredths(self) -> u64 {
        match self {
            Self::TealDuelReplay => 2_250,
            Self::RematchDraftReplay => REMATCH_DRAFT_SOURCE_START_HUNDREDTHS,
            Self::MatchEndWaitingReplay => 19_602,
            Self::LimeModularArenaReplay => 21_402,
            Self::RadialSawHalfBlueReplay => 23_204,
            Self::YellowCrateTerminalBlastReplay => 42_201,
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
            MATCH_END_WAITING_REPLAY_PROFILE => Ok(Self::MatchEndWaitingReplay),
            LIME_MODULAR_REPLAY_PROFILE => Ok(Self::LimeModularArenaReplay),
            RADIAL_REPLAY_PROFILE => Ok(Self::RadialSawHalfBlueReplay),
            YELLOW_REPLAY_PROFILE => Ok(Self::YellowCrateTerminalBlastReplay),
            REPLAY_PROFILE => Ok(Self::TimberCollapseReplay),
            _ => Err(format!(
                "unsupported replay profile {value}; expected {TEAL_REPLAY_PROFILE}, {REMATCH_DRAFT_PROFILE}, {MATCH_END_WAITING_REPLAY_PROFILE}, {LIME_MODULAR_REPLAY_PROFILE}, {RADIAL_REPLAY_PROFILE}, {YELLOW_REPLAY_PROFILE}, or {REPLAY_PROFILE}"
            )),
        }
    }
}

impl ReplayProfile {
    pub fn constructed_prehistory(self) -> Option<&'static str> {
        (self == Self::MatchEndWaitingReplay).then_some(MATCH_END_WAITING_CONSTRUCTED_PREHISTORY)
    }
}

#[derive(Clone, Copy, Debug, Default, Deserialize, PartialEq, Eq, Serialize)]
pub struct PlayerInput {
    pub move_axis: i8,
    pub aim_x: i16,
    pub aim_y: i16,
    pub aim_at_opponent: bool,
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

    /// Resolves optional opponent-relative aim from the latest received state.
    /// Revisioned flow commands remain unchanged and are validated by authority.
    pub fn with_progressive_observation(
        mut self,
        player: u8,
        observation: Option<&MatchSnapshot>,
    ) -> Self {
        if self.aim_at_opponent {
            if let Some(snapshot) = observation {
                let actor = snapshot.players.iter().find(|fighter| fighter.id == player);
                let target = snapshot
                    .players
                    .iter()
                    .find(|fighter| fighter.id == 1_u8.wrapping_sub(player));
                if let (Some(actor), Some(target)) = (actor, target) {
                    let dx = i64::from(target.x_milli) - i64::from(actor.x_milli);
                    let dy = i64::from(target.y_milli) - i64::from(actor.y_milli);
                    let scale = dx.abs().max(dy.abs()).max(1);
                    self.aim_x = (dx * 1_000 / scale) as i16;
                    self.aim_y = (dy * 1_000 / scale) as i16;
                }
            }
            self.aim_at_opponent = false;
        }
        self
    }
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ArenaSurfaceSnapshot {
    /// Local polygon contour shared by physics and presentation; empty for rectangles.
    pub outline_milli: Vec<[i32; 2]>,
    pub id: u8,
    pub center_x_milli: i32,
    pub center_y_milli: i32,
    pub width_milli: i32,
    pub height_milli: i32,
    pub rotation_milliradians: i32,
    pub face_rgb: [u8; 3],
}

/// One source-observed hanging body and its presentation-only square/link geometry.
/// These values deliberately carry no collider, mass, joint, or solver semantics.
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct HangingBodyPresentation {
    pub id: u16,
    pub nominal_x_milli: i32,
    pub body_x_milli: i32,
    pub body_y_milli: i32,
    pub body_width_milli: i32,
    pub body_height_milli: i32,
    pub square_x_milli: i32,
    pub square_y_milli: i32,
    pub square_size_milli: i32,
    pub square_opening_milli: i32,
    pub ceiling_y_milli: i32,
    pub body_top_y_milli: i32,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct HangingEntryPresentation {
    pub age_ticks: u8,
    pub body_rgb: [u8; 3],
    pub square_rim_rgb: [u8; 3],
    pub square_opening_rgb: [u8; 3],
    pub link_rgb: [u8; 3],
    pub bodies: Vec<HangingBodyPresentation>,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SawSnapshot {
    pub id: u16,
    pub x_milli: i32,
    pub y_milli: i32,
    pub angle_milliradians: i32,
    pub angular_velocity_milliradians_per_second: i32,
    pub radius_milli: i32,
    pub teeth: u8,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ImpactSnapshot {
    pub id: u32,
    pub tick: u32,
    pub owner: u8,
    pub target: Option<u8>,
    pub x_milli: i32,
    pub y_milli: i32,
    pub damage: u16,
    pub eliminated: bool,
    pub impulse_x_milli: i32,
    pub impulse_y_milli: i32,
}

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum RoundPhase {
    Combat,
    ResultTransition,
    HalfBlue,
    ArenaTransition,
    HalfOrange,
    RoundOrange,
    RoundBlue,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RoundStateSnapshot {
    /// The connected match owns round totals; isolated legacy slices do not.
    pub completed_rounds: Option<[u8; 2]>,
    pub phase: RoundPhase,
    pub phase_tick: u32,
    pub scores: [u8; 2],
    pub winner: Option<u8>,
    pub eliminated: Option<u8>,
}

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum DynamicBodyShape {
    Timber,
    Weight,
    Crate,
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
    pub mass_milli: i32,
    pub friction_milli: i32,
    pub restitution_milli: i32,
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
    pub simultaneous_eliminations: u32,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct MatchSnapshot {
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub arena_objects: Option<ArenaRenderSnapshot>,
    pub protocol: u16,
    pub seed: u64,
    pub profile: String,
    pub tick: u32,
    pub arena: Vec<ArenaSurfaceSnapshot>,
    pub hanging_entry: Option<HangingEntryPresentation>,
    pub saws: Vec<SawSnapshot>,
    pub dynamic_bodies: Vec<DynamicBodySnapshot>,
    pub constraints: Vec<ConstraintSnapshot>,
    pub explosions: Vec<ExplosionSnapshot>,
    pub impacts: Vec<ImpactSnapshot>,
    pub players: Vec<PlayerSnapshot>,
    pub arena_entry_from_milli: Option<[[i32; 2]; 2]>,
    pub projectiles: Vec<ProjectileSnapshot>,
    pub metrics: CombatMetrics,
    pub winner: Option<u8>,
    pub flow: Option<FlowSnapshot>,
    pub round: Option<RoundStateSnapshot>,
}

pub(crate) fn quantize(value: f32) -> i32 {
    (value * 1_000.0).round() as i32
}
