use super::*;
use bevy::text::TextBounds;
use quarrel_sim::{CardEffect, CardEvent, CombatTuning, ItemDefinition, PlayerSnapshot};

const TICKS_PER_SECOND: f32 = 60.0;

/// Local presentation preference. It never changes authority or flow input.
#[derive(Resource)]
pub(crate) struct DetailedTooltips {
    pub(super) enabled: bool,
    focus: Option<u8>,
    mouse_captured: bool,
}

impl Default for DetailedTooltips {
    fn default() -> Self {
        Self::new(false)
    }
}

impl DetailedTooltips {
    pub(crate) fn new(enabled: bool) -> Self {
        Self {
            enabled,
            focus: Some(0),
            mouse_captured: false,
        }
    }

    pub(crate) fn for_player(player: u8) -> Self {
        Self {
            enabled: false,
            focus: Some(player),
            mouse_captured: false,
        }
    }

    pub(super) fn enabled(&self) -> bool {
        self.enabled
    }

    pub(super) fn toggle(&mut self) {
        self.enabled = !self.enabled;
    }

    pub(super) fn update_mouse_capture(
        &mut self,
        pressed: bool,
        just_pressed: bool,
        over_button: bool,
    ) -> bool {
        if just_pressed && over_button {
            self.toggle();
            self.mouse_captured = true;
        }
        if !pressed {
            self.mouse_captured = false;
        }
        self.mouse_captured
    }
}

/// Adds the readable numerical panel after the ordinary snapshot scene.
/// `tuning` is supplied only when its source is known to be authoritative for
/// this view (local practice or an authoritative host sample).
pub(crate) fn spawn_details_overlay(
    commands: &mut Commands,
    snapshot: &MatchSnapshot,
    details: &DetailedTooltips,
    tuning: Option<&CombatTuning>,
) {
    if !details.enabled {
        return;
    }
    if snapshot
        .flow
        .as_ref()
        .is_some_and(|flow| flow.phase == FlowPhase::Draft)
    {
        spawn_draft_details(commands, snapshot, details);
        return;
    }
    let mut blocks = Vec::new();
    if let Some(tuning) = tuning {
        blocks.push(format_tuning(tuning));
    }
    if let Some(player) = details
        .focus
        .and_then(|id| snapshot.players.iter().find(|player| player.id == id))
    {
        blocks.push(format_observed_player(player));
    }
    blocks.extend(
        crate::draft::detail_cards(snapshot, details.focus)
            .into_iter()
            .take(1)
            .map(format_card_details),
    );
    if blocks.is_empty() {
        return;
    }
    let text = blocks.join("\n\n");
    let height = (text.lines().count() as f32 * 24.0 + 24.0).clamp(80.0, 400.0);
    commands.spawn((
        SceneVisual,
        Sprite::from_color(Color::srgba_u8(2, 8, 20, 230), Vec2::new(500.0, height)),
        Transform::from_xyz(365.0, 300.0 - height / 2.0, 80.0),
    ));
    commands.spawn((
        SceneVisual,
        Text2d::new(text),
        TextFont {
            font_size: FontSize::Px(15.0),
            ..default()
        },
        TextColor(Color::srgb_u8(235, 242, 239)),
        TextLayout::justify(Justify::Left),
        TextBounds::new(476.0, height - 20.0),
        Anchor::TOP_LEFT,
        Transform::from_xyz(127.0, 292.0, 81.0),
    ));
}

fn format_tuning(tuning: &CombatTuning) -> String {
    format!(
        "BASE COMBAT VALUES\nRun target: {run:.1} px/s (not current velocity)\nJump: {jump:.1} px/s | Shot: {shot:.1} px/s\nHit: {damage} | Magazine: {magazine}\nFire: {fire} ({fire_s:.2}s) | Reload: {reload} ({reload_s:.2}s)\nBlock: {block} ({block_s:.2}s) | Cooldown: {block_cd} ({block_cd_s:.2}s)",
        run = tuning.run_speed,
        jump = tuning.jump_speed,
        shot = tuning.bullet_speed,
        damage = tuning.damage_per_hit,
        magazine = tuning.magazine_size,
        fire = ticks(tuning.fire_cooldown_ticks),
        fire_s = seconds(tuning.fire_cooldown_ticks),
        reload = ticks(tuning.reload_ticks),
        reload_s = seconds(tuning.reload_ticks),
        block = ticks(tuning.block_duration_ticks),
        block_s = seconds(tuning.block_duration_ticks),
        block_cd = ticks(tuning.block_cooldown_ticks),
        block_cd_s = seconds(tuning.block_cooldown_ticks),
    )
}

fn spawn_draft_details(
    commands: &mut Commands,
    snapshot: &MatchSnapshot,
    details: &DetailedTooltips,
) {
    let Some(item) = crate::draft::detail_cards(snapshot, details.focus)
        .into_iter()
        .next()
    else {
        return;
    };
    commands.spawn((
        SceneVisual,
        Sprite::from_color(Color::srgba_u8(2, 8, 20, 230), Vec2::new(1_000.0, 46.0)),
        Transform::from_xyz(0.0, -332.0, 80.0),
    ));
    commands.spawn((
        SceneVisual,
        Text2d::new(format_card_strip(item)),
        TextFont {
            font_size: FontSize::Px(15.0),
            ..default()
        },
        TextColor(Color::srgb_u8(235, 242, 239)),
        TextLayout::justify(Justify::Left),
        TextBounds::new(970.0, 38.0),
        Anchor::TOP_LEFT,
        Transform::from_xyz(-485.0, -314.0, 81.0),
    ));
}

fn format_observed_player(player: &PlayerSnapshot) -> String {
    let mut text = String::from("CURRENT VALUES");
    text.push_str(&format!(
            "\nF{}: velocity {:+.3}, {:+.3} px/s\nHealth: {} | Ammo: {}\nFire cooldown: {} ({:.2}s) | Reload: {} ({:.2}s)\nBlock remaining: {} ({:.2}s) | Cooldown: {} ({:.2}s)",
            player.id + 1,
            milli(player.velocity_x_milli_per_second),
            milli(player.velocity_y_milli_per_second),
            player.health,
            player.ammunition,
            ticks(player.fire_cooldown_ticks),
            seconds(player.fire_cooldown_ticks),
            ticks(player.reload_ticks),
            seconds(player.reload_ticks),
            ticks(player.block_ticks),
            seconds(player.block_ticks),
            ticks(player.block_cooldown_ticks),
            seconds(player.block_cooldown_ticks),
        ));
    text
}

pub(super) fn format_card_details(item: &ItemDefinition) -> String {
    let mut lines = vec![format!("{} - EXACT CARD VALUES", item.title)];
    lines.extend(format_modifiers(item));
    for rule in &item.event_rules {
        let effects = rule
            .effects
            .iter()
            .map(format_effect)
            .collect::<Vec<_>>()
            .join(", ");
        lines.push(format!(
            "On {} (depth <= {}): {effects}",
            event_name(rule.on),
            rule.max_depth
        ));
    }
    if lines.len() == 1 {
        lines.push("No numerical modifier or event value.".into());
    }
    lines.join("\n")
}

fn format_card_strip(item: &ItemDefinition) -> String {
    let mut values = format_modifiers(item);
    for rule in &item.event_rules {
        values.push(format!(
            "on {}: {}",
            event_name(rule.on),
            rule.effects
                .iter()
                .map(format_effect)
                .collect::<Vec<_>>()
                .join(", ")
        ));
    }
    format!("{} exact: {}", item.title, values.join(" | "))
}

fn format_modifiers(item: &ItemDefinition) -> Vec<String> {
    let m = item.modifiers;
    let mut lines = Vec::new();
    for (name, value) in [
        ("Health", m.health_bonus),
        ("Damage", m.damage_bonus),
        ("Magazine", m.magazine_bonus),
    ] {
        if value != 0 {
            lines.push(format!("{name}: +{value}"));
        }
    }
    if m.movement_bonus != 0 {
        lines.push(format!("Movement: +{} px/s", m.movement_bonus));
    }
    if m.damage_factor_milli != 0 {
        lines.push(format!(
            "Damage multiplier: x{:.3} ({}%)",
            m.damage_factor_milli as f32 / 1000.0,
            percent(m.damage_factor_milli)
        ));
    }
    if m.fire_interval_factor_milli != 0 {
        lines.push(format!(
            "Fire interval multiplier: x{:.3} ({}%)",
            m.fire_interval_factor_milli as f32 / 1000.0,
            percent(m.fire_interval_factor_milli)
        ));
    }
    if m.projectile_speed_factor.milli != 1000 {
        lines.push(format!(
            "Projectile speed multiplier: x{:.3} ({}%)",
            m.projectile_speed_factor.milli as f32 / 1000.0,
            percent(m.projectile_speed_factor.milli)
        ));
    }
    if m.fire_cooldown_extra_ticks != 0 {
        lines.push(format!(
            "Fire cooldown: +{}",
            ticks(m.fire_cooldown_extra_ticks)
        ));
    }
    if m.dazzle_stun_pulses != 0 || m.dazzle_stun_ticks != 0 {
        lines.push(format!(
            "Dazzle: {} pulses x {}",
            m.dazzle_stun_pulses,
            ticks(m.dazzle_stun_ticks)
        ));
    }
    if m.explosion_radius_milli != 0 || m.explosion_impulse_milli != 0 {
        lines.push(format!(
            "Explosion: {:.3} px radius, {:.3} impulse",
            milli(m.explosion_radius_milli),
            milli(m.explosion_impulse_milli)
        ));
    }
    lines
}

fn format_effect(effect: &CardEffect) -> String {
    match effect {
        CardEffect::ExtraShots {
            count,
            spread_milliradians,
        } => format!(
            "+{count} shots, {:.3} rad spread",
            milli(i32::from(*spread_milliradians))
        ),
        CardEffect::Reload => "reload".into(),
        CardEffect::Teleport { distance } => format!("teleport {distance} px"),
        CardEffect::RepeatBlock { delay_ticks } => format!(
            "repeat block after {} ({:.2}s)",
            ticks(*delay_ticks),
            seconds(*delay_ticks)
        ),
        CardEffect::FireAtOpponent => "fire at opponent".into(),
        CardEffect::Explode {
            radius,
            damage_milli,
        } => format!(
            "explode: {radius} px, {}% of triggering shot damage",
            percent(*damage_milli)
        ),
        CardEffect::Poison {
            ticks: count,
            interval_ticks,
            damage_milli,
        } => format!(
            "poison: {count} hits every {} ({:.2}s), {}% of triggering shot damage",
            ticks(*interval_ticks),
            seconds(*interval_ticks),
            percent(*damage_milli)
        ),
        CardEffect::Bounce(count) => format!("{count} bounces"),
        CardEffect::Grow(rate) => format!(
            "radius/damage +{}% of base per 100 px (radius max x6)",
            percent(*rate)
        ),
        CardEffect::Steer(strength) => {
            format!("turn {}% of aim angle per tick", percent(*strength))
        }
        CardEffect::Drill(depth) => format!("drill {depth} px"),
    }
}

fn event_name(event: CardEvent) -> &'static str {
    match event {
        CardEvent::Impact => "impact",
        CardEvent::Fire => "fire",
        CardEvent::Hit => "hit",
        CardEvent::Block => "block",
        CardEvent::Bounce => "bounce",
        CardEvent::Land => "land",
        CardEvent::TakeDamage => "take damage",
        CardEvent::Kill => "kill",
    }
}

fn ticks(value: u16) -> String {
    format!("{value}t")
}
fn seconds(value: u16) -> f32 {
    f32::from(value) / TICKS_PER_SECOND
}
fn percent(milli: u16) -> String {
    let value = milli as f32 / 10.0;
    if milli.is_multiple_of(10) {
        format!("{value:.0}")
    } else {
        format!("{value:.1}")
    }
}
fn milli(value: i32) -> f32 {
    value as f32 / 1000.0
}

#[cfg(test)]
mod tests {
    use super::*;
    use quarrel_sim::{GameplayModifiers, ItemId, ProjectileSpeedFactor};

    #[test]
    fn card_details_preserve_authoritative_units() {
        let item = ItemDefinition {
            id: ItemId(1),
            title: "Needlework".into(),
            rules: vec![],
            palette_rgb: [0; 3],
            modifiers: GameplayModifiers {
                damage_factor_milli: 1700,
                fire_interval_factor_milli: 1900,
                projectile_speed_factor: ProjectileSpeedFactor { milli: 2400 },
                ..default()
            },
            event_rules: vec![],
        };
        let text = format_card_details(&item);
        assert!(text.contains("x1.700 (170%)"));
        assert!(text.contains("x1.900 (190%)"));
        assert!(text.contains("x2.400 (240%)"));
    }

    #[test]
    fn modifier_percentage_converts_milli_to_percent() {
        assert_eq!(percent(1_600), "160");
        assert_eq!(percent(1_501), "150.1");
    }

    #[test]
    fn details_button_keeps_a_dragging_click_captured_until_release() {
        let mut details = DetailedTooltips::new(false);
        assert!(details.update_mouse_capture(true, true, true));
        assert!(details.enabled());
        assert!(details.update_mouse_capture(true, false, false));
        assert!(!details.update_mouse_capture(false, false, false));
    }

    #[test]
    fn initialized_live_details_show_the_primary_fighters_current_values() {
        let mut app = App::new();
        app.init_resource::<DetailedTooltips>();
        let mut details = app
            .world_mut()
            .remove_resource::<DetailedTooltips>()
            .unwrap();
        details.toggle();
        let snapshot = quarrel_sim::AuthoritativeMatch::practice(38)
            .unwrap()
            .snapshot();
        spawn_details_overlay(&mut app.world_mut().commands(), &snapshot, &details, None);
        app.world_mut().flush();
        let mut text = app.world_mut().query::<&Text2d>();
        assert!(
            text.iter(app.world())
                .any(|text| text.0.contains("F1: velocity"))
        );
    }

    #[test]
    fn reaction_damage_values_are_shot_percentages_and_flight_values_have_units() {
        assert!(
            format_effect(&CardEffect::Explode {
                radius: 120,
                damage_milli: 400
            })
            .contains("40% of triggering shot damage")
        );
        assert!(
            format_effect(&CardEffect::Poison {
                ticks: 18,
                interval_ticks: 10,
                damage_milli: 180
            })
            .contains("18% of triggering shot damage")
        );
        assert!(format_effect(&CardEffect::Grow(180)).contains("18% of base per 100 px"));
        assert!(format_effect(&CardEffect::Steer(80)).contains("8% of aim angle per tick"));
    }

    #[test]
    fn configured_run_target_is_not_presented_as_velocity() {
        let tuning = CombatTuning {
            run_speed: 321.5,
            ..Default::default()
        };
        let text = format_tuning(&tuning);
        assert!(text.contains("Run target: 321.5 px/s (not current velocity)"));
        assert!(text.contains("Fire: 24t (0.40s)"));
    }
}
