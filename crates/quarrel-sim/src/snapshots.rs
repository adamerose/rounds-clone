use super::*;
pub fn projectile_launch_speed(capabilities: FighterCapabilities) -> f32 {
    BULLET_SPEED * f32::from(capabilities.projectile_speed_factor.milli) / 1_000.0
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
            if let Some(snapshot) = observation
                && let Some(actor) = snapshot.players.iter().find(|fighter| fighter.id == player)
            {
                let target = snapshot
                    .players
                    .iter()
                    .filter(|fighter| fighter.id != player && fighter.alive)
                    .min_by_key(|fighter| {
                        let dx = i64::from(fighter.x_milli) - i64::from(actor.x_milli);
                        let dy = i64::from(fighter.y_milli) - i64::from(actor.y_milli);
                        dx.pow(2) + dy.pow(2)
                    });
                if let Some(target) = target {
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
pub enum DynamicBodyShape {
    Timber,
    Weight,
    Crate,
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
    pub tick: u32,
    pub arena: Vec<ArenaSurfaceSnapshot>,
    pub impacts: Vec<ImpactSnapshot>,
    pub players: Vec<PlayerSnapshot>,
    pub projectiles: Vec<ProjectileSnapshot>,
    pub metrics: CombatMetrics,
    pub winner: Option<u8>,
    pub flow: Option<FlowSnapshot>,
}

pub(crate) fn quantize(value: f32) -> i32 {
    (value * 1_000.0).round() as i32
}
