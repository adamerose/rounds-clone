use super::*;

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
pub enum CardEvent {
    Impact,
    Fire,
    Hit,
    Block,
    Bounce,
    Land,
    TakeDamage,
    Kill,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(deny_unknown_fields)]
pub struct EventRule {
    pub on: CardEvent,
    #[serde(default = "reaction_limit")]
    pub max_depth: u8,
    pub effects: Vec<CardEffect>,
}
fn reaction_limit() -> u8 {
    8
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(deny_unknown_fields)]
pub enum CardEffect {
    ExtraShots {
        count: u8,
        spread_milliradians: u16,
    },
    Reload,
    Teleport {
        distance: u16,
    },
    RepeatBlock {
        delay_ticks: u16,
    },
    FireAtOpponent,
    Explode {
        radius: u16,
        damage_milli: u16,
    },
    Poison {
        ticks: u8,
        interval_ticks: u16,
        damage_milli: u16,
    },
    Bounce(u8),
    Grow(u16),
    Steer(u16),
    Drill(u16),
}

impl CardEffect {
    pub(crate) fn is_flight_change(&self) -> bool {
        matches!(
            self,
            Self::Bounce(_) | Self::Grow(_) | Self::Steer(_) | Self::Drill(_)
        )
    }
}

pub(crate) fn validate_rules(rules: &[EventRule]) -> Result<(), String> {
    for rule in rules {
        if rule.max_depth > 8 || rule.effects.is_empty() {
            return Err("rules need effects and max_depth at most 8".into());
        }
        for effect in &rule.effects {
            let valid = match effect {
                CardEffect::ExtraShots {
                    count,
                    spread_milliradians,
                } => (1..=16).contains(count) && *spread_milliradians <= 3142,
                CardEffect::Teleport { distance } => (1..=600).contains(distance),
                CardEffect::RepeatBlock { delay_ticks } => (1..=600).contains(delay_ticks),
                CardEffect::Explode {
                    radius,
                    damage_milli,
                } => (1..=600).contains(radius) && (1..=5000).contains(damage_milli),
                CardEffect::Poison {
                    ticks,
                    interval_ticks,
                    damage_milli,
                } => {
                    (1..=60).contains(ticks)
                        && (1..=600).contains(interval_ticks)
                        && (1..=5000).contains(damage_milli)
                }
                CardEffect::Bounce(n) => (1..=32).contains(n),
                CardEffect::Grow(n) => (1..=2000).contains(n),
                CardEffect::Steer(n) => (1..=1000).contains(n),
                CardEffect::Drill(n) => (1..=600).contains(n),
                CardEffect::Reload | CardEffect::FireAtOpponent => true,
            };
            if !valid {
                return Err(format!("effect out of range: {effect:?}"));
            }
        }
    }
    Ok(())
}
