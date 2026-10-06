use serde::{Deserialize, Serialize};
use std::path::{Path, PathBuf};

/// Combat values pinned in recordings and watched by ordinary match authorities.
#[derive(Clone, Debug, PartialEq, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct CombatTuning {
    pub player_radius: f32,
    pub gravity: f32,
    pub player_density: f32,
    pub player_damping: f32,
    pub player_friction: f32,
    pub player_restitution: f32,
    pub run_speed: f32,
    pub ground_control: f32,
    pub ground_braking: f32,
    pub air_control: f32,
    pub jump_speed: f32,
    pub jump_release_cut: f32,
    pub wall_jump_speed: f32,
    pub wall_slide_speed: f32,
    pub support_normal_threshold: f32,
    pub support_velocity_tolerance: f32,
    pub crouch_height_factor: f32,
    pub crouch_gravity_factor: f32,
    pub bullet_radius: f32,
    #[serde(default = "default_hit_margin")]
    pub bullet_hit_margin: f32,
    pub bullet_speed: f32,
    pub bullet_gravity_factor: f32,
    pub bullet_density: f32,
    pub bullet_restitution: f32,
    pub muzzle_gap: f32,
    pub bullet_lifetime_ticks: u16,
    pub fire_cooldown_ticks: u16,
    pub magazine_size: u16,
    pub reload_ticks: u16,
    pub damage_per_hit: u16,
    pub shot_object_damage: u16,
    pub hit_impulse: f32,
    pub knockback_health_scale: f32,
    pub hit_flash_ticks: u8,
    pub block_duration_ticks: u16,
    pub block_extension_ticks: u16,
    pub block_cooldown_ticks: u16,
    pub edge_damage: u16,
    pub edge_push_speed: f32,
    pub edge_block_speed: f32,
    pub edge_inset: f32,
    pub recoil_enabled: bool,
    pub recoil_impulse: f32,
    pub recoil_speed_cap: f32,
}
fn default_hit_margin() -> f32 {
    2.0
}
impl Default for CombatTuning {
    fn default() -> Self {
        Self::parse(include_str!("../../../assets/tuning.ron")).expect("bundled tuning is valid")
    }
}
impl CombatTuning {
    pub fn parse(source: &str) -> Result<Self, String> {
        let tuning: Self = ron::from_str(source).map_err(|error| error.to_string())?;
        tuning.validate()?;
        Ok(tuning)
    }
    pub fn load(path: &Path) -> Result<Self, String> {
        Self::parse(&std::fs::read_to_string(path).map_err(|e| format!("{}: {e}", path.display()))?)
    }
    pub fn validate(&self) -> Result<(), String> {
        for (name, value) in [
            ("player_radius", self.player_radius),
            ("gravity", self.gravity),
            ("player_density", self.player_density),
            ("player_damping", self.player_damping),
            ("player_friction", self.player_friction),
            ("player_restitution", self.player_restitution),
            ("run_speed", self.run_speed),
            ("ground_control", self.ground_control),
            ("ground_braking", self.ground_braking),
            ("air_control", self.air_control),
            ("jump_speed", self.jump_speed),
            ("jump_release_cut", self.jump_release_cut),
            ("wall_jump_speed", self.wall_jump_speed),
            ("wall_slide_speed", self.wall_slide_speed),
            ("support_normal_threshold", self.support_normal_threshold),
            (
                "support_velocity_tolerance",
                self.support_velocity_tolerance,
            ),
            ("crouch_height_factor", self.crouch_height_factor),
            ("crouch_gravity_factor", self.crouch_gravity_factor),
            ("bullet_radius", self.bullet_radius),
            ("bullet_hit_margin", self.bullet_hit_margin),
            ("bullet_speed", self.bullet_speed),
            ("bullet_gravity_factor", self.bullet_gravity_factor),
            ("bullet_density", self.bullet_density),
            ("bullet_restitution", self.bullet_restitution),
            ("muzzle_gap", self.muzzle_gap),
            ("hit_impulse", self.hit_impulse),
            ("knockback_health_scale", self.knockback_health_scale),
            ("edge_push_speed", self.edge_push_speed),
            ("edge_block_speed", self.edge_block_speed),
            ("edge_inset", self.edge_inset),
            ("recoil_impulse", self.recoil_impulse),
            ("recoil_speed_cap", self.recoil_speed_cap),
        ] {
            if !value.is_finite() || value < 0.0 {
                return Err(format!("{name} must be finite and nonnegative"));
            }
        }
        if self.player_radius == 0.0
            || self.player_density == 0.0
            || self.gravity == 0.0
            || self.bullet_radius == 0.0
            || self.bullet_density == 0.0
            || self.knockback_health_scale == 0.0
            || self.magazine_size == 0
            || self.reload_ticks == 0
            || self.block_cooldown_ticks == 0
            || self.fire_cooldown_ticks == 0
            || self.bullet_lifetime_ticks == 0
        {
            return Err("sizes, masses, gravity, magazine and timers must be positive".into());
        }
        if [
            self.ground_control,
            self.ground_braking,
            self.air_control,
            self.jump_release_cut,
            self.player_restitution,
            self.bullet_restitution,
        ]
        .iter()
        .any(|value| *value > 1.0)
            || !(0.0..=1.0).contains(&self.support_normal_threshold)
            || self.support_normal_threshold == 0.0
            || !(0.0..=1.0).contains(&self.crouch_height_factor)
            || self.crouch_height_factor == 0.0
        {
            return Err(
                "control, restitution, support and height factors must be within their unit ranges"
                    .into(),
            );
        }
        Ok(())
    }
}
pub fn default_tuning_path() -> PathBuf {
    let current = std::env::current_dir().unwrap_or_default();
    for base in current.ancestors() {
        let path = base.join("assets/tuning.ron");
        if path.is_file() {
            return path;
        }
    }
    if let Ok(executable) = std::env::current_exe() {
        for base in executable.ancestors().skip(1) {
            let path = base.join("assets/tuning.ron");
            if path.is_file() {
                return path;
            }
        }
    }
    PathBuf::from("assets/tuning.ron")
}
