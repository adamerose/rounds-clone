use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};

pub const REMATCH_DRAFT_TICKS: u32 = 4_540;
pub const CONNECTED_FIRST_ROUND_TICKS: u32 = 5_466;
pub const FIRST_LOSER_DRAFT_TICKS: u32 = 5_893;
pub const CONNECTED_ICE_LOAD_TICK: u32 = 4_541;
pub const CONNECTED_ICE_COMBAT_TICK: u32 = 4_601;
pub const CONNECTED_ICE_RESULT_ONSET_TICK: u32 = 5_339;
pub const LEGACY_REMATCH_DRAFT_TICKS: u32 = 2_400;
pub const REMATCH_DRAFT_PROFILE: &str = "rematch-draft-replay";
pub const REMATCH_DRAFT_SOURCE_INTERVAL: &str = "02:39.516029-03:55.182393";
pub const REMATCH_DRAFT_SOURCE_START_HUNDREDTHS: u64 = 15_952;
pub const SOURCE_DRAFT_SEED: u64 = 41;
pub const CONNECTED_BLUE_RESULT_ONSET_TICK: u32 = 2_586;
pub const CONNECTED_HALF_BLUE_TICK: u32 = 2_609;
pub const CONNECTED_HALF_BLUE_TAIL_TICK: u32 = 2_700;
pub const CONNECTED_TIMBER_COMBAT_TICK: u32 = 2_730;
pub const CONNECTED_TIMBER_IMPACT_TARGET_TICK: u32 = 3_653;
pub const CONNECTED_ORANGE_RESULT_ONSET_TICK: u32 = 4_450;
pub const CONNECTED_HALF_ORANGE_TICK: u32 = 4_470;

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "kebab-case")]
pub enum ItemId {
    FrostSlam,
    Combine,
    TasteOfBlood,
    Burst,
    Dazzle,
    ExplosiveBullet,
    Echo,
    Lifestealer,
    Emp,
    QuickShot,
    ColdBullets,
    CarefulPlanning,
    Overpower,
    BigBullet,
}

impl ItemId {
    pub fn short_badge(self) -> &'static str {
        match self {
            Self::Dazzle => "Da",
            Self::ExplosiveBullet => "Ex",
            Self::FrostSlam => "Fr",
            Self::Combine => "Co",
            Self::TasteOfBlood => "Ta",
            Self::Burst => "Bu",
            Self::Echo => "Ec",
            Self::Lifestealer => "Li",
            Self::Emp => "Em",
            Self::QuickShot => "Qu",
            Self::ColdBullets => "Co",
            Self::CarefulPlanning => "Ca",
            Self::Overpower => "Ov",
            Self::BigBullet => "Bi",
        }
    }
}

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum ItemRarity {
    Common,
    Uncommon,
    Rare,
}

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum ImplementationState {
    Implemented,
    CatalogOnly,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct GameplayModifiers {
    pub dazzle_stun_pulses: u8,
    pub dazzle_stun_ticks: u16,
    pub explosion_radius_milli: i32,
    pub explosion_impulse_milli: i32,
    pub fire_cooldown_extra_ticks: u16,
    pub projectile_speed_factor: ProjectileSpeedFactor,
}

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ProjectileSpeedFactor {
    pub milli: u16,
}

impl Default for ProjectileSpeedFactor {
    fn default() -> Self {
        Self { milli: 1_000 }
    }
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ItemDefinition {
    pub id: ItemId,
    pub title: String,
    pub rules: Vec<String>,
    pub rarity: ItemRarity,
    pub palette_rgb: [u8; 3],
    pub art_key: String,
    pub implementation: ImplementationState,
    pub modifiers: Option<GameplayModifiers>,
}

pub fn item_catalog() -> Vec<ItemDefinition> {
    use ImplementationState::{CatalogOnly, Implemented};
    use ItemId::*;
    use ItemRarity::{Common, Rare, Uncommon};
    vec![
        item(
            FrostSlam,
            "FROST SLAM",
            &[
                "Slows enemies around you when you block",
                "More HP",
                "+0.25s Block cooldown",
            ],
            Uncommon,
            [80, 209, 239],
            "frost-ring",
            CatalogOnly,
            None,
        ),
        item(
            Combine,
            "COMBINE",
            &["A bunch more DMG", "-2 Ammo", "+0.5s Reload time"],
            Common,
            [236, 81, 74],
            "merged-rounds",
            CatalogOnly,
            None,
        ),
        item(
            TasteOfBlood,
            "TASTE OF BLOOD",
            &[
                "+50% movement speed 3s after dealing DMG",
                "Slightly more Life steal",
            ],
            Uncommon,
            [181, 62, 105],
            "fang-drop",
            CatalogOnly,
            None,
        ),
        item(
            Burst,
            "BURST",
            &[
                "Multiple bullets are fired in a sequence",
                "+2 Bullets",
                "+3 Ammo",
                "Lower DMG",
                "+0.25s Reload time",
            ],
            Rare,
            [233, 178, 45],
            "burst-rays",
            CatalogOnly,
            None,
        ),
        item(
            Dazzle,
            "DAZZLE",
            &[
                "Bullets stun the opponent multiple times",
                "+0.25s Reload time",
            ],
            Rare,
            [222, 128, 50],
            "stun-stars",
            Implemented,
            Some(GameplayModifiers {
                dazzle_stun_pulses: 3,
                dazzle_stun_ticks: 6,
                explosion_radius_milli: 0,
                explosion_impulse_milli: 0,
                fire_cooldown_extra_ticks: 15,
                projectile_speed_factor: ProjectileSpeedFactor::default(),
            }),
        ),
        item(
            ExplosiveBullet,
            "EXPLOSIVE BULLET",
            &[
                "Bullet explodes on impact",
                "Lower ATKSPD",
                "+0.25s Reload time",
            ],
            Rare,
            [238, 116, 35],
            "impact-burst",
            Implemented,
            Some(GameplayModifiers {
                dazzle_stun_pulses: 0,
                dazzle_stun_ticks: 0,
                explosion_radius_milli: 150_000,
                explosion_impulse_milli: 540_000,
                fire_cooldown_extra_ticks: 15,
                projectile_speed_factor: ProjectileSpeedFactor::default(),
            }),
        ),
        item(
            Echo,
            "ECHO",
            &[
                "Blocking triggers another, delayed block",
                "More HP",
                "+0.25s Block cooldown",
            ],
            Uncommon,
            [97, 175, 213],
            "echo-rings",
            CatalogOnly,
            None,
        ),
        item(
            Lifestealer,
            "LIFESTEALER",
            &["Steal HP from your opponent when near", "Slightly more HP"],
            Rare,
            [177, 71, 183],
            "vampire-orbit",
            CatalogOnly,
            None,
        ),
        item(
            Emp,
            "EMP",
            &[
                "Blocking spawns a ring of slowing projectiles",
                "More HP",
                "+0.25s Block cooldown",
            ],
            Rare,
            [68, 169, 204],
            "electric-ring",
            CatalogOnly,
            None,
        ),
        item(
            QuickShot,
            "QUICK SHOT",
            &["A bunch more Bullet speed", "+0.25s Reload time"],
            Common,
            [222, 244, 102],
            "speed-streak-blob",
            Implemented,
            Some(GameplayModifiers {
                projectile_speed_factor: ProjectileSpeedFactor { milli: 1_250 },
                ..neutral_modifiers()
            }),
        ),
        item(
            ColdBullets,
            "COLD BULLETS",
            &["A bunch more Bullet slow", "+0.25s Reload time"],
            Uncommon,
            [92, 188, 224],
            "snowflake-bullet",
            CatalogOnly,
            None,
        ),
        item(
            CarefulPlanning,
            "CAREFUL PLANNING",
            &["A bunch more DMG", "Lower ATKSPD", "+0.5s Reload time"],
            Uncommon,
            [22, 165, 190],
            "paper-fan-blob",
            CatalogOnly,
            None,
        ),
        item(
            Overpower,
            "OVERPOWER",
            &[
                "Deal 15% of your max HP to enemies around you when you block",
                "More HP",
                "+0.25s Block cooldown",
            ],
            Rare,
            [244, 78, 73],
            "dark-glasses-blob",
            CatalogOnly,
            None,
        ),
        item(
            BigBullet,
            "BIG BULLET",
            &["Bigger bullets", "+0.25s Reload time"],
            Common,
            [226, 83, 74],
            "oversized-red-round",
            CatalogOnly,
            None,
        ),
    ]
}

fn neutral_modifiers() -> GameplayModifiers {
    GameplayModifiers {
        dazzle_stun_pulses: 0,
        dazzle_stun_ticks: 0,
        explosion_radius_milli: 0,
        explosion_impulse_milli: 0,
        fire_cooldown_extra_ticks: 0,
        projectile_speed_factor: ProjectileSpeedFactor::default(),
    }
}

#[expect(
    clippy::too_many_arguments,
    reason = "each argument is one visible or typed column of the compact source-card table"
)]
fn item(
    id: ItemId,
    title: &str,
    rules: &[&str],
    rarity: ItemRarity,
    palette_rgb: [u8; 3],
    art_key: &str,
    implementation: ImplementationState,
    modifiers: Option<GameplayModifiers>,
) -> ItemDefinition {
    ItemDefinition {
        id,
        title: title.to_owned(),
        rules: rules.iter().map(|rule| (*rule).to_owned()).collect(),
        rarity,
        palette_rgb,
        art_key: art_key.to_owned(),
        implementation,
        modifiers,
    }
}

pub fn item_definition(id: ItemId) -> ItemDefinition {
    item_catalog()
        .into_iter()
        .find(|item| item.id == id)
        .expect("every item id is registered")
}

pub fn general_implemented_offer_pool() -> Vec<ItemId> {
    item_catalog()
        .into_iter()
        .filter(|item| item.implementation == ImplementationState::Implemented)
        .map(|item| item.id)
        .collect()
}

pub fn source_offers(seed: u64, player: u8) -> Vec<ItemId> {
    use ItemId::*;
    let mut offers = if player == 0 {
        vec![FrostSlam, Combine, TasteOfBlood, Burst, Dazzle]
    } else {
        vec![ExplosiveBullet, Echo, Lifestealer, Emp, Dazzle]
    };
    let rotation = seed.wrapping_sub(SOURCE_DRAFT_SEED) as usize % offers.len();
    offers.rotate_left(rotation);
    offers
}

pub fn first_loser_draft_offers() -> Vec<ItemId> {
    use ItemId::*;
    vec![
        QuickShot,
        ColdBullets,
        CarefulPlanning,
        Overpower,
        BigBullet,
    ]
}

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum FlowPhase {
    CombatConclusion,
    RematchPrompt,
    ArenaFade,
    Draft,
    Reveal,
    Handoff,
    ArenaTransition,
    ResumedCombat,
    EliminationConclusion,
    BlueResultTransition,
    HalfBlue,
    TimberTransition,
    TimberCombat,
    OrangeResultTransition,
    HalfOrange,
    IceTransition,
    IceCombat,
    RoundBlue,
    RoundOrange,
    PostRoundDraft,
    PostRoundReveal,
    PostRoundBridge,
    TerminalMatch,
}

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum RematchVote {
    Pending,
    Yes,
    No,
}

/// Source-visible badge abbreviations from the concluded match. Their full
/// card identities are not legible in the bounded recording, so the authority
/// preserves the observed values without inventing item definitions.
#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
pub enum PriorBadge {
    Po,
    De,
    Th,
    Qu,
    Bu,
    Ca,
    Co,
    Fa,
}

impl PriorBadge {
    pub fn label(self) -> &'static str {
        match self {
            Self::Po => "Po",
            Self::De => "De",
            Self::Th => "Th",
            Self::Qu => "Qu",
            Self::Bu => "Bu",
            Self::Ca => "Ca",
            Self::Co => "Co",
            Self::Fa => "Fa",
        }
    }
}

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum FlowAction {
    VoteYes,
    VoteNo,
    Hover(ItemId),
    Confirm(ItemId),
}

#[derive(Clone, Copy, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FlowCommand {
    pub phase_revision: u16,
    pub action: FlowAction,
}

#[derive(Clone, Copy, Debug, Default, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub enum ActionResult {
    #[default]
    None,
    Accepted,
    Stale,
    Duplicate,
    WrongPlayer,
    WrongPhase,
    NotOffered,
    NotHovered,
    UnimplementedItem,
}

#[derive(Clone, Copy, Debug, Default, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FighterCapabilities {
    pub dazzle_stun_pulses: u8,
    pub dazzle_stun_ticks: u16,
    pub explosion_radius_milli: i32,
    pub explosion_impulse_milli: i32,
    pub fire_cooldown_extra_ticks: u16,
    pub projectile_speed_factor: ProjectileSpeedFactor,
}

impl FighterCapabilities {
    fn accumulate(&mut self, modifiers: GameplayModifiers) {
        self.dazzle_stun_pulses = self
            .dazzle_stun_pulses
            .saturating_add(modifiers.dazzle_stun_pulses);
        self.dazzle_stun_ticks = self
            .dazzle_stun_ticks
            .saturating_add(modifiers.dazzle_stun_ticks);
        self.explosion_radius_milli = self
            .explosion_radius_milli
            .saturating_add(modifiers.explosion_radius_milli);
        self.explosion_impulse_milli = self
            .explosion_impulse_milli
            .saturating_add(modifiers.explosion_impulse_milli);
        self.fire_cooldown_extra_ticks = self
            .fire_cooldown_extra_ticks
            .saturating_add(modifiers.fire_cooldown_extra_ticks);
        let multiplied = u32::from(self.projectile_speed_factor.milli)
            * u32::from(modifiers.projectile_speed_factor.milli)
            / 1_000;
        self.projectile_speed_factor.milli = multiplied.min(u32::from(u16::MAX)) as u16;
    }
}

#[derive(Clone, Debug, Deserialize, PartialEq, Eq, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct FlowSnapshot {
    pub phase: FlowPhase,
    pub phase_revision: u16,
    pub phase_tick: u32,
    pub active_player: Option<u8>,
    pub scores: [u8; 2],
    pub halves: [u8; 2],
    pub winner: Option<u8>,
    pub eliminated: Option<u8>,
    pub fighter_alive: [bool; 2],
    pub prior_badges: [Vec<PriorBadge>; 2],
    pub rematch_votes: [RematchVote; 2],
    pub offers: [Vec<ItemId>; 2],
    pub hovered: [Option<ItemId>; 2],
    pub selected: [Option<ItemId>; 2],
    pub revealed: Option<ItemId>,
    pub loadouts: [Vec<ItemId>; 2],
    pub capabilities: [FighterCapabilities; 2],
    pub last_results: [ActionResult; 2],
    pub accepted_actions: u32,
    pub catalog: Vec<ItemDefinition>,
}

pub struct FlowAuthority {
    snapshot: FlowSnapshot,
    /// Combat phase to repeat after a no-award simultaneous elimination.
    repeat_phase: Option<FlowPhase>,
}

impl FlowAuthority {
    pub fn new(seed: u64) -> Self {
        Self {
            snapshot: FlowSnapshot {
                phase: FlowPhase::CombatConclusion,
                phase_revision: 0,
                phase_tick: 0,
                active_player: None,
                scores: [4, 5],
                halves: [0, 0],
                winner: Some(1),
                eliminated: Some(0),
                fighter_alive: [false, true],
                prior_badges: [
                    vec![
                        PriorBadge::Po,
                        PriorBadge::De,
                        PriorBadge::Th,
                        PriorBadge::Qu,
                        PriorBadge::Bu,
                    ],
                    vec![
                        PriorBadge::Bu,
                        PriorBadge::Ca,
                        PriorBadge::Co,
                        PriorBadge::Co,
                        PriorBadge::Fa,
                    ],
                ],
                rematch_votes: [RematchVote::Pending; 2],
                offers: [source_offers(seed, 0), source_offers(seed, 1)],
                hovered: [None, None],
                selected: [None, None],
                revealed: None,
                loadouts: [Vec::new(), Vec::new()],
                capabilities: [FighterCapabilities::default(); 2],
                last_results: [ActionResult::None; 2],
                accepted_actions: 0,
                catalog: item_catalog(),
            },
            repeat_phase: None,
        }
    }

    pub fn snapshot(&self) -> FlowSnapshot {
        self.snapshot.clone()
    }

    pub fn capabilities(&self, player: u8) -> FighterCapabilities {
        self.snapshot.capabilities[usize::from(player)]
    }

    pub fn accepts_combat(&self) -> bool {
        matches!(
            self.snapshot.phase,
            FlowPhase::ResumedCombat | FlowPhase::TimberCombat | FlowPhase::IceCombat
        )
    }

    pub fn record_elimination(&mut self, winner: u8) -> bool {
        if winner > 1 || !self.accepts_combat() {
            return false;
        }
        self.snapshot.halves[usize::from(winner)] += 1;
        if self.snapshot.halves[usize::from(winner)] == 2 {
            self.snapshot.scores[usize::from(winner)] += 1;
        }
        self.snapshot.winner = Some(winner);
        self.snapshot.eliminated = Some(1 - winner);
        self.snapshot.fighter_alive = [winner == 0, winner == 1];
        self.transition(FlowPhase::EliminationConclusion, None);
        true
    }

    /// Records that both fighters fell on the same tick. Provisional policy
    /// pending source evidence: nobody scores, halves and loadouts stay, and the
    /// same arena repeats without a draft. Returns false outside live combat.
    pub fn record_simultaneous_elimination(&mut self) -> bool {
        if !self.accepts_combat() {
            return false;
        }
        self.repeat_phase = Some(self.snapshot.phase);
        self.snapshot.winner = None;
        self.snapshot.eliminated = None;
        self.snapshot.fighter_alive = [false, false];
        self.transition(FlowPhase::EliminationConclusion, None);
        true
    }

    pub fn has_terminal_result(&self) -> bool {
        self.snapshot.winner.is_some()
    }

    pub fn advance(&mut self, commands: [Option<FlowCommand>; 2]) {
        self.snapshot.phase_tick += 1;
        self.snapshot.last_results = [ActionResult::None; 2];
        for (player, command) in commands.into_iter().enumerate() {
            if let Some(command) = command {
                self.apply(player as u8, command);
            }
        }
        match self.snapshot.phase {
            FlowPhase::CombatConclusion if self.snapshot.phase_tick >= 240 => {
                self.transition(FlowPhase::RematchPrompt, None);
            }
            FlowPhase::ArenaFade if self.snapshot.phase_tick >= 150 => {
                self.transition(FlowPhase::Draft, Some(0));
                self.snapshot.hovered[0] = self.snapshot.offers[0].get(1).copied();
            }
            FlowPhase::Reveal
                if self.snapshot.phase_tick
                    >= if self.snapshot.active_player == Some(0) {
                        120
                    } else {
                        60
                    } =>
            {
                if self.snapshot.active_player == Some(0) {
                    self.transition(FlowPhase::Handoff, None);
                } else {
                    self.transition(FlowPhase::ArenaTransition, None);
                }
            }
            FlowPhase::Handoff if self.snapshot.phase_tick >= 30 => {
                self.transition(FlowPhase::Draft, Some(1));
                self.snapshot.hovered[1] = self.snapshot.offers[1].get(4).copied();
            }
            FlowPhase::ArenaTransition if self.snapshot.phase_tick >= 59 => {
                self.transition(FlowPhase::ResumedCombat, None);
            }
            FlowPhase::RoundBlue | FlowPhase::RoundOrange if self.snapshot.phase_tick >= 139 => {
                let loser = self
                    .snapshot
                    .eliminated
                    .expect("a completed round records its losing fighter");
                self.snapshot.halves = [0, 0];
                self.snapshot.offers = [Vec::new(), Vec::new()];
                self.snapshot.offers[usize::from(loser)] = first_loser_draft_offers();
                self.snapshot.hovered = [None, None];
                self.snapshot.selected = [None, None];
                self.snapshot.revealed = None;
                self.transition(FlowPhase::PostRoundDraft, Some(loser));
            }
            FlowPhase::PostRoundReveal if self.snapshot.phase_tick >= 16 => {
                self.transition(FlowPhase::PostRoundBridge, None);
            }
            FlowPhase::EliminationConclusion
                if self.snapshot.phase_tick
                    >= if self.snapshot.halves.iter().sum::<u8>() == 1 {
                        13
                    } else if self.snapshot.halves.iter().sum::<u8>() == 3 {
                        27
                    } else {
                        16
                    } =>
            {
                let next = match self.snapshot.winner {
                    Some(1) => FlowPhase::BlueResultTransition,
                    Some(_) => FlowPhase::OrangeResultTransition,
                    None => {
                        self.snapshot.fighter_alive = [true, true];
                        self.repeat_phase.take().unwrap_or(FlowPhase::ResumedCombat)
                    }
                };
                self.transition(next, None);
            }
            FlowPhase::BlueResultTransition
                if self.snapshot.phase_tick
                    >= if self.snapshot.halves.iter().sum::<u8>() == 1 {
                        23
                    } else if self.snapshot.halves.iter().sum::<u8>() == 3 {
                        16
                    } else {
                        20
                    } =>
            {
                self.transition(
                    if self.snapshot.halves[1] == 2 {
                        FlowPhase::RoundBlue
                    } else {
                        FlowPhase::HalfBlue
                    },
                    None,
                );
            }
            FlowPhase::HalfBlue | FlowPhase::HalfOrange
                if self.snapshot.phase_tick >= 91
                    && self.snapshot.halves.iter().sum::<u8>() == 1 =>
            {
                self.snapshot.fighter_alive = [true, true];
                self.transition(FlowPhase::TimberTransition, None);
            }
            FlowPhase::TimberTransition if self.snapshot.phase_tick >= 30 => {
                self.snapshot.winner = None;
                self.snapshot.eliminated = None;
                self.snapshot.fighter_alive = [true, true];
                self.transition(FlowPhase::TimberCombat, None);
            }
            FlowPhase::HalfBlue | FlowPhase::HalfOrange
                if self.snapshot.phase_tick >= 71 && self.snapshot.halves == [1, 1] =>
            {
                self.snapshot.fighter_alive = [true, true];
                self.transition(FlowPhase::IceTransition, None);
            }
            FlowPhase::IceTransition if self.snapshot.phase_tick >= 60 => {
                self.snapshot.winner = None;
                self.snapshot.eliminated = None;
                self.snapshot.fighter_alive = [true, true];
                self.transition(FlowPhase::IceCombat, None);
            }
            FlowPhase::OrangeResultTransition
                if self.snapshot.phase_tick
                    >= if self.snapshot.halves.iter().sum::<u8>() == 1 {
                        23
                    } else if self.snapshot.halves.iter().sum::<u8>() == 3 {
                        16
                    } else {
                        20
                    } =>
            {
                self.transition(
                    if self.snapshot.halves[0] == 2 {
                        FlowPhase::RoundOrange
                    } else {
                        FlowPhase::HalfOrange
                    },
                    None,
                );
            }
            _ => {}
        }
    }

    fn apply(&mut self, player: u8, command: FlowCommand) {
        let index = usize::from(player);
        if command.phase_revision != self.snapshot.phase_revision {
            self.snapshot.last_results[index] = ActionResult::Stale;
            return;
        }
        let result = match (self.snapshot.phase, command.action) {
            (FlowPhase::RematchPrompt, FlowAction::VoteYes) => self.vote(player, RematchVote::Yes),
            (FlowPhase::RematchPrompt, FlowAction::VoteNo) => self.vote(player, RematchVote::No),
            (FlowPhase::Draft, FlowAction::Hover(item)) => self.hover(player, item),
            (FlowPhase::Draft, FlowAction::Confirm(item)) => self.confirm(player, item),
            (FlowPhase::PostRoundDraft, FlowAction::Hover(item)) => self.hover(player, item),
            (FlowPhase::PostRoundDraft, FlowAction::Confirm(item)) => self.confirm(player, item),
            (FlowPhase::PostRoundReveal, FlowAction::Confirm(item)) => self.confirm(player, item),
            _ => ActionResult::WrongPhase,
        };
        self.snapshot.last_results[index] = result;
        if result == ActionResult::Accepted {
            self.snapshot.accepted_actions += 1;
        }
    }

    fn vote(&mut self, player: u8, vote: RematchVote) -> ActionResult {
        let slot = &mut self.snapshot.rematch_votes[usize::from(player)];
        if *slot != RematchVote::Pending {
            return ActionResult::Duplicate;
        }
        *slot = vote;
        if vote == RematchVote::No {
            self.transition(FlowPhase::TerminalMatch, None);
        } else if self.snapshot.rematch_votes == [RematchVote::Yes; 2] {
            self.snapshot.scores = [0, 0];
            self.snapshot.halves = [0, 0];
            self.snapshot.winner = None;
            self.snapshot.eliminated = None;
            self.snapshot.fighter_alive = [true, true];
            self.snapshot.prior_badges = [Vec::new(), Vec::new()];
            self.snapshot.loadouts = [Vec::new(), Vec::new()];
            self.snapshot.capabilities = [FighterCapabilities::default(); 2];
            self.transition(FlowPhase::ArenaFade, None);
        }
        ActionResult::Accepted
    }

    fn hover(&mut self, player: u8, item: ItemId) -> ActionResult {
        if self.snapshot.active_player != Some(player) {
            return ActionResult::WrongPlayer;
        }
        let index = usize::from(player);
        if !self.snapshot.offers[index].contains(&item) {
            return ActionResult::NotOffered;
        }
        if self.snapshot.hovered[index] == Some(item) {
            return ActionResult::Duplicate;
        }
        self.snapshot.hovered[index] = Some(item);
        ActionResult::Accepted
    }

    fn confirm(&mut self, player: u8, item: ItemId) -> ActionResult {
        if self.snapshot.active_player != Some(player) {
            return ActionResult::WrongPlayer;
        }
        let index = usize::from(player);
        if !self.snapshot.offers[index].contains(&item) {
            return ActionResult::NotOffered;
        }
        if self.snapshot.hovered[index] != Some(item) {
            return ActionResult::NotHovered;
        }
        let definition = item_definition(item);
        if definition.implementation != ImplementationState::Implemented {
            return ActionResult::UnimplementedItem;
        }
        if self.snapshot.selected[index].is_some() || self.snapshot.loadouts[index].contains(&item)
        {
            return ActionResult::Duplicate;
        }
        self.snapshot.selected[index] = Some(item);
        self.snapshot.revealed = Some(item);
        self.snapshot.loadouts[index].push(item);
        if let Some(modifiers) = definition.modifiers {
            self.snapshot.capabilities[index].accumulate(modifiers);
        }
        let reveal = if self.snapshot.phase == FlowPhase::PostRoundDraft {
            FlowPhase::PostRoundReveal
        } else {
            FlowPhase::Reveal
        };
        self.transition(reveal, Some(player));
        ActionResult::Accepted
    }

    fn transition(&mut self, phase: FlowPhase, active_player: Option<u8>) {
        self.snapshot.phase = phase;
        self.snapshot.phase_revision += 1;
        self.snapshot.phase_tick = 0;
        self.snapshot.active_player = active_player;
        if phase != FlowPhase::Reveal {
            self.snapshot.revealed = None;
        }
    }
}

pub fn flow_digest(flow: &FlowSnapshot) -> String {
    let bytes = serde_json::to_vec(flow).expect("flow serialization cannot fail");
    format!("{:x}", Sha256::digest(bytes))
}

pub fn loadout_digest(flow: &FlowSnapshot) -> String {
    let bytes = serde_json::to_vec(&(flow.loadouts.clone(), flow.capabilities))
        .expect("loadout serialization cannot fail");
    format!("{:x}", Sha256::digest(bytes))
}

#[cfg(test)]
mod tests {
    use super::*;

    fn advance_to_prompt(flow: &mut FlowAuthority) {
        for _ in 0..240 {
            flow.advance([None, None]);
        }
        assert_eq!(flow.snapshot.phase, FlowPhase::RematchPrompt);
    }

    #[test]
    fn catalog_transcribes_source_offers_without_unlocking_inert_cards() {
        let catalog = item_catalog();
        assert_eq!(catalog.len(), 14, "Dazzle is the repeated rematch offer");
        assert_eq!(source_offers(SOURCE_DRAFT_SEED, 0).len(), 5);
        assert_eq!(source_offers(SOURCE_DRAFT_SEED, 1).len(), 5);
        assert_eq!(
            general_implemented_offer_pool(),
            vec![ItemId::Dazzle, ItemId::ExplosiveBullet, ItemId::QuickShot]
        );
        assert_eq!(
            first_loser_draft_offers(),
            vec![
                ItemId::QuickShot,
                ItemId::ColdBullets,
                ItemId::CarefulPlanning,
                ItemId::Overpower,
                ItemId::BigBullet,
            ]
        );
        assert_eq!(
            item_definition(ItemId::QuickShot).rules,
            vec!["A bunch more Bullet speed", "+0.25s Reload time"]
        );
        assert_eq!(
            item_definition(ItemId::Dazzle).rules,
            vec![
                "Bullets stun the opponent multiple times",
                "+0.25s Reload time"
            ]
        );
    }

    #[test]
    fn both_yes_resets_score_and_old_loadouts_but_either_no_terminates() {
        let mut accepted = FlowAuthority::new(SOURCE_DRAFT_SEED);
        assert_eq!(accepted.snapshot.scores, [4, 5]);
        assert_eq!(accepted.snapshot.winner, Some(1));
        assert_eq!(accepted.snapshot.eliminated, Some(0));
        assert_eq!(accepted.snapshot.fighter_alive, [false, true]);
        assert_eq!(
            accepted.snapshot.prior_badges,
            [
                vec![
                    PriorBadge::Po,
                    PriorBadge::De,
                    PriorBadge::Th,
                    PriorBadge::Qu,
                    PriorBadge::Bu,
                ],
                vec![
                    PriorBadge::Bu,
                    PriorBadge::Ca,
                    PriorBadge::Co,
                    PriorBadge::Co,
                    PriorBadge::Fa,
                ],
            ]
        );
        advance_to_prompt(&mut accepted);
        accepted.advance([
            Some(FlowCommand {
                phase_revision: 1,
                action: FlowAction::VoteYes,
            }),
            Some(FlowCommand {
                phase_revision: 1,
                action: FlowAction::VoteYes,
            }),
        ]);
        assert_eq!(accepted.snapshot.phase, FlowPhase::ArenaFade);
        assert_eq!(accepted.snapshot.scores, [0, 0]);
        assert_eq!(accepted.snapshot.winner, None);
        assert_eq!(accepted.snapshot.eliminated, None);
        assert_eq!(accepted.snapshot.fighter_alive, [true, true]);
        assert_eq!(accepted.snapshot.prior_badges, [Vec::new(), Vec::new()]);
        assert_eq!(accepted.snapshot.loadouts, [Vec::new(), Vec::new()]);

        let mut rejected = FlowAuthority::new(SOURCE_DRAFT_SEED);
        advance_to_prompt(&mut rejected);
        rejected.advance([
            Some(FlowCommand {
                phase_revision: 1,
                action: FlowAction::VoteNo,
            }),
            None,
        ]);
        assert_eq!(rejected.snapshot.phase, FlowPhase::TerminalMatch);
        assert_eq!(rejected.snapshot.scores, [4, 5]);
        assert_eq!(rejected.snapshot.winner, Some(1));
        assert_eq!(rejected.snapshot.eliminated, Some(0));
        assert_eq!(rejected.snapshot.fighter_alive, [false, true]);
        assert!(!rejected.snapshot.prior_badges[0].is_empty());
    }

    #[test]
    fn action_validation_rejects_stale_duplicate_wrong_owner_and_catalog_only_confirm() {
        let mut flow = FlowAuthority::new(SOURCE_DRAFT_SEED);
        advance_to_prompt(&mut flow);
        flow.advance([
            Some(FlowCommand {
                phase_revision: 0,
                action: FlowAction::VoteYes,
            }),
            None,
        ]);
        assert_eq!(flow.snapshot.last_results[0], ActionResult::Stale);
        flow.advance([
            Some(FlowCommand {
                phase_revision: 1,
                action: FlowAction::VoteYes,
            }),
            None,
        ]);
        flow.advance([
            Some(FlowCommand {
                phase_revision: 1,
                action: FlowAction::VoteYes,
            }),
            None,
        ]);
        assert_eq!(flow.snapshot.last_results[0], ActionResult::Duplicate);
        flow.advance([
            None,
            Some(FlowCommand {
                phase_revision: 1,
                action: FlowAction::VoteYes,
            }),
        ]);
        for _ in 0..150 {
            flow.advance([None, None]);
        }
        assert_eq!(flow.snapshot.phase, FlowPhase::Draft);
        flow.advance([
            None,
            Some(FlowCommand {
                phase_revision: 3,
                action: FlowAction::Hover(ItemId::Dazzle),
            }),
        ]);
        assert_eq!(flow.snapshot.last_results[1], ActionResult::WrongPlayer);
        flow.advance([
            Some(FlowCommand {
                phase_revision: 3,
                action: FlowAction::Hover(ItemId::Burst),
            }),
            None,
        ]);
        flow.advance([
            Some(FlowCommand {
                phase_revision: 3,
                action: FlowAction::Confirm(ItemId::Burst),
            }),
            None,
        ]);
        assert_eq!(
            flow.snapshot.last_results[0],
            ActionResult::UnimplementedItem
        );
        assert!(flow.snapshot.loadouts[0].is_empty());
    }

    #[test]
    fn implemented_source_picks_apply_once_to_the_correct_fighters() {
        let replay = crate::run_profile_snapshots(
            crate::ReplayProfile::RematchDraftReplay,
            SOURCE_DRAFT_SEED,
            LEGACY_REMATCH_DRAFT_TICKS,
        );
        let flow = replay.last().unwrap().flow.as_ref().unwrap();
        assert_eq!(flow.phase, FlowPhase::ResumedCombat);
        assert_eq!(
            flow.selected,
            [Some(ItemId::Dazzle), Some(ItemId::ExplosiveBullet)]
        );
        assert_eq!(
            flow.loadouts,
            [vec![ItemId::Dazzle], vec![ItemId::ExplosiveBullet]]
        );
        assert_eq!(flow.capabilities[0].dazzle_stun_pulses, 3);
        assert_eq!(flow.capabilities[1].explosion_radius_milli, 150_000);
        assert_eq!(flow.scores, [0, 0]);
    }

    #[test]
    fn source_cadence_has_blues_complete_fan_by_two_fifty_six() {
        let replay = crate::run_profile_snapshots(
            crate::ReplayProfile::RematchDraftReplay,
            SOURCE_DRAFT_SEED,
            960,
        );
        let flow = replay.last().unwrap().flow.as_ref().unwrap();
        assert_eq!(flow.phase, FlowPhase::Draft);
        assert_eq!(flow.active_player, Some(1));
        assert_eq!(flow.offers[1].len(), 5);
        assert_eq!(flow.hovered[1], Some(ItemId::Dazzle));
        assert_eq!(flow.selected[0], Some(ItemId::Dazzle));
    }

    #[test]
    fn source_anchors_preserve_exact_focus_and_confirmation_sequence() {
        let at = |tick| {
            crate::run_profile_snapshots(
                crate::ReplayProfile::RematchDraftReplay,
                SOURCE_DRAFT_SEED,
                tick,
            )
            .pop()
            .unwrap()
            .flow
            .unwrap()
        };

        let orange_initial = at(540);
        assert_eq!(orange_initial.phase, FlowPhase::Draft);
        assert_eq!(orange_initial.active_player, Some(0));
        assert_eq!(orange_initial.hovered[0], Some(ItemId::Combine));
        assert_eq!(orange_initial.scores, [0, 0]);

        assert_eq!(at(600).hovered[0], Some(ItemId::Burst));

        let orange_confirmed = at(840);
        assert_eq!(orange_confirmed.phase, FlowPhase::Reveal);
        assert_eq!(orange_confirmed.selected[0], Some(ItemId::Dazzle));
        assert_eq!(orange_confirmed.loadouts[0], vec![ItemId::Dazzle]);

        assert_eq!(at(960).hovered[1], Some(ItemId::Dazzle));
        assert_eq!(at(1_560).hovered[1], Some(ItemId::Lifestealer));
        assert_eq!(at(2_040).hovered[1], Some(ItemId::Echo));

        let blue_confirmed = at(2_120);
        assert_eq!(blue_confirmed.phase, FlowPhase::Reveal);
        assert_eq!(
            blue_confirmed.selected,
            [Some(ItemId::Dazzle), Some(ItemId::ExplosiveBullet)]
        );
        assert_eq!(
            blue_confirmed.loadouts,
            [vec![ItemId::Dazzle], vec![ItemId::ExplosiveBullet]]
        );
        assert_eq!(blue_confirmed.scores, [0, 0]);
    }

    #[test]
    fn offer_seed_and_selected_item_perturbations_change_protected_digests() {
        let nominal = FlowAuthority::new(SOURCE_DRAFT_SEED).snapshot();
        let perturbed = FlowAuthority::new(SOURCE_DRAFT_SEED + 1).snapshot();
        assert_ne!(nominal.offers, perturbed.offers);
        assert_ne!(flow_digest(&nominal), flow_digest(&perturbed));

        let mut changed_loadout = nominal.clone();
        changed_loadout.loadouts[0] = vec![ItemId::Dazzle];
        changed_loadout.capabilities[0] = FighterCapabilities {
            dazzle_stun_pulses: 3,
            dazzle_stun_ticks: 6,
            fire_cooldown_extra_ticks: 15,
            ..Default::default()
        };
        assert_ne!(loadout_digest(&nominal), loadout_digest(&changed_loadout));
    }

    #[test]
    fn simultaneous_elimination_awards_nothing_and_repeats_the_same_combat_phase() {
        let mut flow = FlowAuthority::new(SOURCE_DRAFT_SEED);
        assert!(!flow.record_simultaneous_elimination(), "not in combat");
        advance_to_prompt(&mut flow);
        let revision = flow.snapshot.phase_revision;
        flow.advance([
            Some(FlowCommand {
                phase_revision: revision,
                action: FlowAction::VoteYes,
            }),
            Some(FlowCommand {
                phase_revision: revision,
                action: FlowAction::VoteNo,
            }),
        ]);
        assert_eq!(flow.snapshot.phase, FlowPhase::TerminalMatch);
        assert!(!flow.record_simultaneous_elimination());

        let mut flow = FlowAuthority::new(SOURCE_DRAFT_SEED);
        flow.snapshot.halves = [1, 0];
        flow.snapshot.loadouts = [vec![ItemId::Dazzle], vec![ItemId::ExplosiveBullet]];
        flow.snapshot.winner = None;
        flow.snapshot.eliminated = None;
        flow.snapshot.fighter_alive = [true, true];
        flow.transition(FlowPhase::TimberCombat, None);
        let before = flow.snapshot();
        assert!(flow.record_simultaneous_elimination());
        assert_eq!(flow.snapshot.phase, FlowPhase::EliminationConclusion);
        assert_eq!(flow.snapshot.fighter_alive, [false, false]);
        assert_eq!(
            (flow.snapshot.winner, flow.snapshot.eliminated),
            (None, None)
        );
        assert!(!flow.has_terminal_result());
        // A later elimination report cannot replace the recorded outcome.
        assert!(!flow.record_elimination(0));
        assert!(!flow.record_simultaneous_elimination());
        let mut ticks = 0;
        while flow.snapshot.phase == FlowPhase::EliminationConclusion {
            flow.advance([None, None]);
            ticks += 1;
            assert!(ticks <= 30, "the pause is bounded");
        }
        assert_eq!(flow.snapshot.phase, FlowPhase::TimberCombat);
        assert_eq!(flow.snapshot.fighter_alive, [true, true]);
        assert_eq!(flow.snapshot.halves, before.halves);
        assert_eq!(flow.snapshot.scores, before.scores);
        assert_eq!(flow.snapshot.loadouts, before.loadouts);
        assert_eq!(flow.snapshot.capabilities, before.capabilities);
        assert!(flow.accepts_combat());
        assert!(flow.record_elimination(1));
        assert_eq!(flow.snapshot.halves, [1, 1]);
    }

    fn completed_round(winner: u8) -> FlowAuthority {
        let mut flow = FlowAuthority::new(SOURCE_DRAFT_SEED);
        flow.snapshot.scores = if winner == 0 { [1, 0] } else { [0, 1] };
        flow.snapshot.halves = if winner == 0 { [2, 1] } else { [1, 2] };
        flow.snapshot.winner = Some(winner);
        flow.snapshot.eliminated = Some(1 - winner);
        flow.snapshot.loadouts = [vec![ItemId::Dazzle], vec![ItemId::ExplosiveBullet]];
        flow.snapshot.capabilities = [FighterCapabilities::default(); 2];
        flow.snapshot.capabilities[0]
            .accumulate(item_definition(ItemId::Dazzle).modifiers.unwrap());
        flow.snapshot.capabilities[1]
            .accumulate(item_definition(ItemId::ExplosiveBullet).modifiers.unwrap());
        flow.snapshot.hovered = [Some(ItemId::Dazzle), Some(ItemId::ExplosiveBullet)];
        flow.snapshot.selected = [Some(ItemId::Dazzle), Some(ItemId::ExplosiveBullet)];
        flow.transition(
            if winner == 0 {
                FlowPhase::RoundOrange
            } else {
                FlowPhase::RoundBlue
            },
            None,
        );
        for _ in 0..139 {
            flow.advance([None, None]);
        }
        flow
    }

    #[test]
    fn completed_round_opens_a_clean_loser_only_draft_in_both_colours() {
        for winner in [0, 1] {
            let mut flow = completed_round(winner);
            let loser = 1 - winner;
            assert_eq!(flow.snapshot.phase, FlowPhase::PostRoundDraft);
            assert_eq!(flow.snapshot.active_player, Some(loser));
            assert_eq!(flow.snapshot.halves, [0, 0]);
            assert_eq!(
                flow.snapshot.scores,
                if winner == 0 { [1, 0] } else { [0, 1] }
            );
            assert_eq!(
                flow.snapshot.offers[usize::from(loser)],
                first_loser_draft_offers()
            );
            assert!(flow.snapshot.offers[usize::from(winner)].is_empty());
            assert_eq!(flow.snapshot.hovered, [None, None]);
            assert_eq!(flow.snapshot.selected, [None, None]);
            assert_eq!(flow.snapshot.revealed, None);
            assert_eq!(
                flow.snapshot.loadouts,
                [vec![ItemId::Dazzle], vec![ItemId::ExplosiveBullet]]
            );
            assert!(!flow.record_elimination(winner));
        }
    }

    #[test]
    fn post_round_draft_rejections_are_state_safe_and_quick_shot_accumulates() {
        let mut flow = completed_round(1);
        let revision = flow.snapshot.phase_revision;
        fn retained(
            flow: &FlowAuthority,
        ) -> ([u8; 2], [u8; 2], [Vec<ItemId>; 2], [FighterCapabilities; 2]) {
            (
                flow.snapshot.halves,
                flow.snapshot.scores,
                flow.snapshot.loadouts.clone(),
                flow.snapshot.capabilities,
            )
        }
        let expected = retained(&flow);

        for action in [
            FlowAction::Hover(ItemId::QuickShot),
            FlowAction::Confirm(ItemId::QuickShot),
        ] {
            flow.advance([
                None,
                Some(FlowCommand {
                    phase_revision: revision,
                    action,
                }),
            ]);
            assert_eq!(flow.snapshot.last_results[1], ActionResult::WrongPlayer);
            assert_eq!(retained(&flow), expected);
        }
        flow.advance([
            Some(FlowCommand {
                phase_revision: revision - 1,
                action: FlowAction::Hover(ItemId::QuickShot),
            }),
            None,
        ]);
        assert_eq!(flow.snapshot.last_results[0], ActionResult::Stale);
        assert_eq!(retained(&flow), expected);
        flow.advance([
            Some(FlowCommand {
                phase_revision: revision,
                action: FlowAction::Hover(ItemId::Dazzle),
            }),
            None,
        ]);
        assert_eq!(flow.snapshot.last_results[0], ActionResult::NotOffered);
        assert_eq!(retained(&flow), expected);

        for item in [
            ItemId::ColdBullets,
            ItemId::CarefulPlanning,
            ItemId::Overpower,
            ItemId::BigBullet,
        ] {
            flow.advance([
                Some(FlowCommand {
                    phase_revision: revision,
                    action: FlowAction::Hover(item),
                }),
                None,
            ]);
            assert_eq!(flow.snapshot.last_results[0], ActionResult::Accepted);
            flow.advance([
                Some(FlowCommand {
                    phase_revision: revision,
                    action: FlowAction::Confirm(item),
                }),
                None,
            ]);
            assert_eq!(
                flow.snapshot.last_results[0],
                ActionResult::UnimplementedItem
            );
            assert_eq!(flow.snapshot.hovered[0], Some(item));
            assert_eq!(retained(&flow), expected);
        }

        flow.advance([
            Some(FlowCommand {
                phase_revision: revision,
                action: FlowAction::Confirm(ItemId::QuickShot),
            }),
            None,
        ]);
        assert_eq!(flow.snapshot.last_results[0], ActionResult::NotHovered);
        assert_eq!(retained(&flow), expected);
        flow.advance([
            Some(FlowCommand {
                phase_revision: revision,
                action: FlowAction::Hover(ItemId::QuickShot),
            }),
            None,
        ]);
        flow.advance([
            Some(FlowCommand {
                phase_revision: revision,
                action: FlowAction::Confirm(ItemId::QuickShot),
            }),
            None,
        ]);
        assert_eq!(flow.snapshot.phase, FlowPhase::PostRoundReveal);
        assert_eq!(
            flow.snapshot.loadouts,
            [
                vec![ItemId::Dazzle, ItemId::QuickShot],
                vec![ItemId::ExplosiveBullet]
            ]
        );
        let capability = flow.snapshot.capabilities[0];
        assert_eq!(capability.dazzle_stun_pulses, 3);
        assert_eq!(capability.dazzle_stun_ticks, 6);
        assert_eq!(capability.fire_cooldown_extra_ticks, 15);
        assert!(capability.projectile_speed_factor.milli > 1_000);
        assert_eq!(flow.snapshot.capabilities[1], expected.3[1]);
        let reveal_revision = flow.snapshot.phase_revision;
        flow.advance([
            Some(FlowCommand {
                phase_revision: reveal_revision,
                action: FlowAction::Confirm(ItemId::QuickShot),
            }),
            None,
        ]);
        assert_eq!(flow.snapshot.last_results[0], ActionResult::Duplicate);
        assert_eq!(
            flow.snapshot.loadouts[0],
            vec![ItemId::Dazzle, ItemId::QuickShot]
        );
    }
}
