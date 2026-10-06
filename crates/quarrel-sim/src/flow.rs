use super::*;
use std::path::{Path, PathBuf};

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(deny_unknown_fields)]
pub struct MatchConfig {
    pub fighter_count: usize,
    pub target_score: u16,
    pub offer_size: usize,
    pub run_it_back_limit: u8,
    pub seed: u64,
}
impl Default for MatchConfig {
    fn default() -> Self {
        Self {
            fighter_count: 2,
            target_score: 5,
            offer_size: 5,
            run_it_back_limit: 2,
            seed: 38,
        }
    }
}
impl MatchConfig {
    pub fn validate(&self) -> Result<(), String> {
        if !(1..=255).contains(&self.fighter_count) {
            return Err("fighter count must be 1..=255".into());
        }
        if self.target_score == 0 || self.offer_size == 0 || self.offer_size > 255 {
            return Err(
                "target score and offer size must be positive; offer size at most 255".into(),
            );
        }
        if self
            .target_score
            .checked_mul(u16::from(self.run_it_back_limit) + 1)
            .is_none()
        {
            return Err("run-back target exceeds score range".into());
        }
        Ok(())
    }
}

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, PartialOrd, Ord, Serialize)]
pub struct ItemId(pub u64);
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
pub struct ProjectileSpeedFactor {
    pub milli: u16,
}
impl Default for ProjectileSpeedFactor {
    fn default() -> Self {
        Self { milli: 1000 }
    }
}
#[derive(Clone, Copy, Debug, Default, Deserialize, PartialEq, Eq, Serialize)]
pub struct GameplayModifiers {
    pub dazzle_stun_pulses: u8,
    pub dazzle_stun_ticks: u16,
    pub explosion_radius_milli: i32,
    pub explosion_impulse_milli: i32,
    pub fire_cooldown_extra_ticks: u16,
    pub projectile_speed_factor: ProjectileSpeedFactor,
    pub health_bonus: u16,
    pub damage_bonus: u16,
    pub movement_bonus: u16,
    #[serde(default)]
    pub damage_factor_milli: u16,
    #[serde(default)]
    pub fire_interval_factor_milli: u16,
    #[serde(default)]
    pub magazine_bonus: u16,
}
pub type FighterCapabilities = GameplayModifiers;
pub(crate) fn factor(value: u16) -> u32 {
    if value == 0 { 1000 } else { u32::from(value) }
}
fn stack_factor(a: u16, b: u16) -> u16 {
    (factor(a) * factor(b) / 1000).clamp(1, u32::from(u16::MAX)) as u16
}

impl FighterCapabilities {
    fn accumulate(&mut self, change: Self) {
        self.damage_factor_milli =
            stack_factor(self.damage_factor_milli, change.damage_factor_milli);
        self.fire_interval_factor_milli = stack_factor(
            self.fire_interval_factor_milli,
            change.fire_interval_factor_milli,
        );
        self.magazine_bonus = self.magazine_bonus.saturating_add(change.magazine_bonus);
        self.health_bonus = self.health_bonus.saturating_add(change.health_bonus);
        self.damage_bonus = self.damage_bonus.saturating_add(change.damage_bonus);
        self.movement_bonus = self.movement_bonus.saturating_add(change.movement_bonus);
        self.fire_cooldown_extra_ticks = self
            .fire_cooldown_extra_ticks
            .saturating_add(change.fire_cooldown_extra_ticks);
        self.projectile_speed_factor.milli = (u32::from(self.projectile_speed_factor.milli)
            * u32::from(change.projectile_speed_factor.milli)
            / 1000)
            .min(u32::from(u16::MAX)) as u16;
    }
}
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
pub struct ItemDefinition {
    pub id: ItemId,
    pub title: String,
    pub rules: Vec<String>,
    pub palette_rgb: [u8; 3],
    pub modifiers: GameplayModifiers,
    #[serde(default)]
    pub event_rules: Vec<EventRule>,
}
#[derive(Clone, Debug, Default, Deserialize, PartialEq, Eq, Serialize)]
#[serde(default, deny_unknown_fields)]
pub struct StatChanges {
    pub health: u16,
    pub damage: u16,
    pub movement_speed: u16,
    pub projectile_speed_milli: u16,
    pub damage_factor_milli: u16,
    pub fire_interval_factor_milli: u16,
    pub magazine_bonus: u16,
}
#[derive(Clone, Debug, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct CardFile {
    pub id: String,
    pub name: String,
    pub description: String,
    pub stat_changes: StatChanges,
    #[serde(default)]
    pub event_rules: Vec<EventRule>,
}
pub fn default_card_directory() -> PathBuf {
    if let Some(path) = std::env::var_os("QUARREL_CARD_DIR") {
        return PathBuf::from(path);
    }
    let current = std::env::current_dir().unwrap_or_default();
    for base in current.ancestors() {
        let path = base.join("assets/cards");
        if path.is_dir() {
            return path;
        }
    }
    if let Ok(exe) = std::env::current_exe() {
        for base in exe.ancestors().skip(1) {
            let path = base.join("assets/cards");
            if path.is_dir() {
                return path;
            }
        }
    }
    PathBuf::from("assets/cards")
}
pub fn load_card_directory(path: &Path) -> Result<Vec<ItemDefinition>, String> {
    let mut files = std::fs::read_dir(path)
        .map_err(|e| format!("{}: {e}", path.display()))?
        .map(|entry| entry.map(|e| e.path()).map_err(|e| e.to_string()))
        .collect::<Result<Vec<_>, _>>()?;
    files.sort();
    let mut catalog = Vec::new();
    for file in files
        .into_iter()
        .filter(|p| p.extension().is_some_and(|e| e == "ron"))
    {
        let source = std::fs::read_to_string(&file).map_err(|e| e.to_string())?;
        let card: CardFile =
            ron::from_str(&source).map_err(|e| format!("{}: {e}", file.display()))?;
        if card.id.trim().is_empty()
            || card.name.trim().is_empty()
            || card.description.trim().is_empty()
        {
            return Err(format!(
                "{}: card id, name and description cannot be empty",
                file.display()
            ));
        }
        let hash = Sha256::digest(card.id.as_bytes());
        let id = ItemId(u64::from_le_bytes(hash[..8].try_into().unwrap()));
        if catalog.iter().any(|c: &ItemDefinition| c.id == id) {
            return Err(format!("duplicate card id {}", card.id));
        }
        validate_rules(&card.event_rules).map_err(|e| format!("{}: {e}", file.display()))?;
        let changes = card.stat_changes;
        catalog.push(ItemDefinition {
            id,
            title: card.name,
            rules: vec![card.description],
            palette_rgb: [90, 190, 160],
            event_rules: card.event_rules,
            modifiers: GameplayModifiers {
                magazine_bonus: changes.magazine_bonus,
                damage_factor_milli: changes.damage_factor_milli,
                fire_interval_factor_milli: changes.fire_interval_factor_milli,
                health_bonus: changes.health,
                damage_bonus: changes.damage,
                movement_bonus: changes.movement_speed,
                projectile_speed_factor: ProjectileSpeedFactor {
                    milli: if changes.projectile_speed_milli == 0 {
                        1000
                    } else {
                        changes.projectile_speed_milli
                    },
                },
                ..Default::default()
            },
        });
    }
    if catalog.is_empty() {
        return Err("card pool is empty".into());
    }
    Ok(catalog)
}

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
pub enum FlowPhase {
    Draft,
    Combat,
    Result,
    MatchEnd,
}
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
pub enum RematchVote {
    Pending,
    Yes,
    No,
}
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
pub enum FlowAction {
    VoteYes,
    VoteNo,
    Hover(ItemId),
    Confirm(ItemId),
}
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
pub struct FlowCommand {
    pub phase_revision: u32,
    pub action: FlowAction,
}
#[derive(Clone, Copy, Debug, Default, Deserialize, PartialEq, Eq, Serialize)]
pub enum ActionResult {
    #[default]
    None,
    Accepted,
    Stale,
    Duplicate,
    WrongPlayer,
    WrongPhase,
    NotOffered,
    LimitReached,
}
#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
pub struct FlowSnapshot {
    pub phase: FlowPhase,
    pub phase_revision: u32,
    pub phase_tick: u32,
    pub scores: Vec<u16>,
    pub winner: Option<u8>,
    pub fighter_alive: Vec<bool>,
    pub rematch_votes: Vec<RematchVote>,
    pub offers: Vec<Vec<ItemId>>,
    pub hovered: Vec<Option<ItemId>>,
    pub selected: Vec<Option<ItemId>>,
    pub loadouts: Vec<Vec<ItemId>>,
    pub capabilities: Vec<FighterCapabilities>,
    pub last_results: Vec<ActionResult>,
    pub accepted_actions: u32,
    pub catalog: Vec<ItemDefinition>,
    pub target_score: u16,
    pub run_backs: u8,
    pub run_it_back_limit: u8,
    pub fight_number: u32,
    pub match_number: u32,
}
pub(crate) struct SeededRandom(pub u64);
impl SeededRandom {
    pub(crate) fn next(&mut self) -> u64 {
        self.0 = self.0.wrapping_add(0x9e3779b97f4a7c15);
        let mut z = self.0;
        z = (z ^ (z >> 30)).wrapping_mul(0xbf58476d1ce4e5b9);
        z = (z ^ (z >> 27)).wrapping_mul(0x94d049bb133111eb);
        z ^ (z >> 31)
    }
    pub(crate) fn shuffle<T>(&mut self, items: &mut [T]) {
        for i in (1..items.len()).rev() {
            let j = (self.next() % (i as u64 + 1)) as usize;
            items.swap(i, j);
        }
    }
}
pub struct FlowAuthority {
    config: MatchConfig,
    snapshot: FlowSnapshot,
    random: SeededRandom,
    eligible: Vec<bool>,
}
impl FlowAuthority {
    pub fn with_config(config: MatchConfig, catalog: Vec<ItemDefinition>) -> Result<Self, String> {
        config.validate()?;
        for card in &catalog {
            validate_rules(&card.event_rules)?;
        }
        let unique: std::collections::BTreeSet<_> = catalog.iter().map(|item| item.id).collect();
        if unique.len() != catalog.len() {
            return Err("duplicate card IDs".into());
        }
        if catalog.len() < config.offer_size {
            return Err("offer size exceeds card pool".into());
        }
        let n = config.fighter_count;
        let mut authority = Self {
            random: SeededRandom(config.seed),
            eligible: vec![true; n],
            snapshot: FlowSnapshot {
                phase: FlowPhase::Draft,
                phase_revision: 0,
                phase_tick: 0,
                scores: vec![0; n],
                winner: None,
                fighter_alive: vec![true; n],
                rematch_votes: vec![RematchVote::Pending; n],
                offers: vec![Vec::new(); n],
                hovered: vec![None; n],
                selected: vec![None; n],
                loadouts: vec![Vec::new(); n],
                capabilities: vec![Default::default(); n],
                last_results: vec![ActionResult::None; n],
                accepted_actions: 0,
                catalog,
                target_score: config.target_score,
                run_backs: 0,
                run_it_back_limit: config.run_it_back_limit,
                fight_number: 0,
                match_number: 0,
            },
            config,
        };
        authority.draw_offers();
        Ok(authority)
    }
    pub fn snapshot(&self) -> FlowSnapshot {
        self.snapshot.clone()
    }
    pub fn capabilities(&self, player: u8) -> FighterCapabilities {
        self.snapshot.capabilities[usize::from(player)]
    }
    pub(crate) fn rules_for(&self, player: u8) -> Vec<EventRule> {
        self.snapshot.loadouts[usize::from(player)]
            .iter()
            .flat_map(|id| {
                self.snapshot
                    .catalog
                    .iter()
                    .find(|c| c.id == *id)
                    .into_iter()
                    .flat_map(|c| c.event_rules.clone())
            })
            .collect()
    }
    pub(crate) fn replace_catalog(&mut self, catalog: Vec<ItemDefinition>) -> Result<(), String> {
        if catalog.len() < self.config.offer_size {
            return Err("offer size exceeds card pool".into());
        }
        if self
            .snapshot
            .loadouts
            .iter()
            .flatten()
            .chain(self.snapshot.offers.iter().flatten())
            .any(|id| !catalog.iter().any(|c| c.id == *id))
        {
            return Err("cannot remove cards held or currently offered during a match".into());
        }
        self.snapshot.catalog = catalog;
        for (index, held) in self.snapshot.loadouts.iter().enumerate() {
            let mut cap = FighterCapabilities::default();
            for id in held {
                cap.accumulate(
                    self.snapshot
                        .catalog
                        .iter()
                        .find(|c| c.id == *id)
                        .unwrap()
                        .modifiers,
                );
            }
            self.snapshot.capabilities[index] = cap;
        }
        Ok(())
    }
    pub fn accepts_combat(&self) -> bool {
        self.snapshot.phase == FlowPhase::Combat
    }
    pub fn record_survivors(&mut self, alive: &[bool]) -> bool {
        if !self.accepts_combat() || alive.len() != self.config.fighter_count {
            return false;
        }
        let remaining: Vec<_> = alive
            .iter()
            .enumerate()
            .filter(|(_, live)| **live)
            .map(|(i, _)| i)
            .collect();
        let candidates: Vec<_> = self
            .snapshot
            .fighter_alive
            .iter()
            .enumerate()
            .filter(|(_, live)| **live)
            .map(|(i, _)| i)
            .collect();
        self.snapshot.fighter_alive.clone_from_slice(alive);
        if remaining.len() > 1 {
            return false;
        }
        let winner = if let Some(&winner) = remaining.first() {
            winner
        } else {
            if candidates.is_empty() {
                return false;
            }
            candidates[(self.random.next() % candidates.len() as u64) as usize]
        };
        self.snapshot.winner = Some(winner as u8);
        self.snapshot.scores[winner] += 1;
        self.transition(FlowPhase::Result);
        true
    }
    pub fn advance(&mut self, commands: &[Option<FlowCommand>]) {
        self.snapshot.phase_tick = self.snapshot.phase_tick.saturating_add(1);
        self.snapshot.last_results.fill(ActionResult::None);
        // Apply a batch against one phase revision, then transition. This lets
        // simultaneous confirmations or votes arrive in either fighter order.
        for (player, command) in commands.iter().take(self.config.fighter_count).enumerate() {
            if let Some(command) = command {
                let result = self.apply(player, *command);
                self.snapshot.last_results[player] = result;
                if result == ActionResult::Accepted {
                    self.snapshot.accepted_actions += 1;
                }
            }
        }
        match self.snapshot.phase {
            FlowPhase::Draft
                if self
                    .eligible
                    .iter()
                    .enumerate()
                    .all(|(i, e)| !e || self.snapshot.selected[i].is_some()) =>
            {
                self.snapshot.fighter_alive.fill(true);
                self.snapshot.winner = None;
                self.transition(FlowPhase::Combat);
            }
            FlowPhase::Result if self.snapshot.phase_tick >= 30 => {
                let winner = usize::from(self.snapshot.winner.unwrap());
                if self.snapshot.scores[winner] >= self.snapshot.target_score {
                    self.snapshot.rematch_votes.fill(RematchVote::Pending);
                    self.snapshot.offers.iter_mut().for_each(Vec::clear);
                    self.transition(FlowPhase::MatchEnd);
                } else {
                    self.eligible = (0..self.config.fighter_count)
                        .map(|i| i != winner)
                        .collect();
                    self.snapshot.fight_number += 1;
                    self.draw_offers();
                    self.transition(FlowPhase::Draft);
                }
            }
            FlowPhase::MatchEnd
                if self
                    .snapshot
                    .rematch_votes
                    .iter()
                    .all(|v| *v == RematchVote::Yes) =>
            {
                self.snapshot.run_backs += 1;
                self.snapshot.target_score += self.config.target_score;
                self.snapshot.fight_number += 1;
                let winner = usize::from(self.snapshot.winner.unwrap());
                self.eligible = (0..self.config.fighter_count)
                    .map(|i| i != winner)
                    .collect();
                self.draw_offers();
                self.snapshot.rematch_votes.fill(RematchVote::Pending);
                self.transition(FlowPhase::Draft);
            }
            FlowPhase::MatchEnd
                if self
                    .snapshot
                    .rematch_votes
                    .iter()
                    .all(|v| *v == RematchVote::No) =>
            {
                self.snapshot.match_number += 1;
                self.snapshot.fight_number += 1;
                self.snapshot.scores.fill(0);
                self.snapshot.target_score = self.config.target_score;
                self.snapshot.run_backs = 0;
                self.snapshot.winner = None;
                self.snapshot.fighter_alive.fill(true);
                self.snapshot.loadouts.iter_mut().for_each(Vec::clear);
                self.snapshot.capabilities.fill(Default::default());
                self.snapshot.rematch_votes.fill(RematchVote::Pending);
                self.eligible.fill(true);
                self.draw_offers();
                self.transition(FlowPhase::Draft);
            }
            _ => {}
        }
    }
    fn apply(&mut self, player: usize, command: FlowCommand) -> ActionResult {
        if command.phase_revision != self.snapshot.phase_revision {
            return ActionResult::Stale;
        }
        match (self.snapshot.phase, command.action) {
            (FlowPhase::MatchEnd, FlowAction::VoteYes | FlowAction::VoteNo) => {
                let vote = if command.action == FlowAction::VoteYes {
                    RematchVote::Yes
                } else {
                    RematchVote::No
                };
                if vote == RematchVote::Yes
                    && self.snapshot.run_backs >= self.config.run_it_back_limit
                {
                    return ActionResult::LimitReached;
                }
                if self.snapshot.rematch_votes[player] == vote {
                    return ActionResult::Duplicate;
                }
                self.snapshot.rematch_votes[player] = vote;
                ActionResult::Accepted
            }
            (FlowPhase::Draft, FlowAction::Hover(item) | FlowAction::Confirm(item)) => {
                if !self.eligible[player] {
                    return ActionResult::WrongPlayer;
                }
                if self.snapshot.selected[player].is_some() {
                    return ActionResult::Duplicate;
                }
                if !self.snapshot.offers[player].contains(&item) {
                    return ActionResult::NotOffered;
                }
                self.snapshot.hovered[player] = Some(item);
                if matches!(command.action, FlowAction::Confirm(_)) {
                    self.snapshot.selected[player] = Some(item);
                    self.snapshot.loadouts[player].push(item);
                    let definition = self.snapshot.catalog.iter().find(|c| c.id == item).unwrap();
                    self.snapshot.capabilities[player].accumulate(definition.modifiers);
                }
                ActionResult::Accepted
            }
            _ => ActionResult::WrongPhase,
        }
    }
    fn draw_offers(&mut self) {
        self.snapshot.hovered.fill(None);
        self.snapshot.selected.fill(None);
        for (i, eligible) in self.eligible.iter().enumerate() {
            let mut pool: Vec<_> = self.snapshot.catalog.iter().map(|c| c.id).collect();
            self.random.shuffle(&mut pool);
            pool.truncate(if *eligible { self.config.offer_size } else { 0 });
            self.snapshot.hovered[i] = pool.first().copied();
            self.snapshot.offers[i] = pool;
        }
    }
    fn transition(&mut self, phase: FlowPhase) {
        self.snapshot.phase = phase;
        self.snapshot.phase_revision = self.snapshot.phase_revision.wrapping_add(1);
        self.snapshot.phase_tick = 0;
    }
}
#[cfg(test)]
mod tests;
