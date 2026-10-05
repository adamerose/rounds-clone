use super::*;
pub fn hash_snapshot(snapshot: &MatchSnapshot) -> String {
    let bytes = serde_json::to_vec(snapshot).expect("snapshot serialization cannot fail");
    format!("{:x}", Sha256::digest(bytes))
}

pub fn scripted_inputs(seed: u64, ticks: u32) -> [Vec<PlayerInput>; 2] {
    scripted_inputs_for(ReplayProfile::default(), seed, ticks)
}

// Public controls for the connected ice observation; ranges name input ticks.
// Rows resolve last-match-wins over `PlayerInput::default()`, so each jump press
// is held through its airborne ticks by splitting or amending the rows it spans
// and filling the uncovered gaps with rows that are default in every field but
// `jump`, never by overlaying a wider row. The six holds ticket 050 extended to
// the end of their rise are written the same way, which is why `[4965, 4970)` is
// a filler row and why the late-listed `(1, 4958, 4959, ...)` — the row that wins
// over `[4955, 4960)` wherever they overlap — also carries the hold.
fn connected_ice_input(player: usize, tick: u32) -> PlayerInput {
    let actions = [
        (0, 4710, 4740, 1, 1, 0, 1000, 0, 0),
        (0, 4740, 4770, 1, 0, 0, 1000, 0, 0),
        (0, 4820, 4850, 1, 1, 0, 1000, 0, 0),
        (0, 4850, 4880, 1, 0, 0, 1000, 0, 0),
        (0, 4920, 4929, 0, 0, 0, 1000, 0, 0),
        (0, 4929, 4930, 1, 1, 0, 1000, 0, 0),
        (0, 4930, 4931, 1, 0, 0, 1000, 0, 0),
        (0, 4931, 4945, 1, 1, 0, 1000, 0, 0),
        (0, 4945, 4950, -1, 1, 0, -1000, 0, 0),
        (0, 4950, 4980, 0, 1, 0, 0, 0, 0),
        (0, 4980, 4988, 1, 1, 0, 1000, 0, 0),
        (0, 4988, 5020, 1, 0, 0, 1000, 0, 0),
        (0, 5055, 5063, 1, 0, 0, 1000, 0, 0),
        (0, 5175, 5190, 1, 0, 0, 1000, 0, 0),
        (0, 5190, 5202, -1, 0, 0, -1000, 0, 0),
        (0, 5202, 5211, 0, 0, 0, 1000, 0, 0),
        (0, 5211, 5212, 0, 1, 0, 1000, 0, 0),
        (0, 5212, 5213, 1, 0, 0, 1000, 0, 0),
        (0, 5213, 5218, 1, 1, 0, 1000, 0, 0),
        (0, 5218, 5225, 1, 0, 0, 1000, 0, 0),
        (0, 5225, 5230, -1, 0, 0, -1000, 0, 0),
        (0, 5230, 5265, -1, 1, 0, -1000, 0, 0),
        (0, 5265, 5282, -1, 1, 0, -1000, 0, 0),
        (0, 5282, 5299, -1, 0, 0, -1000, 0, 0),
        (0, 5299, 5305, 1, 0, 0, 1000, 0, 0),
        (0, 5305, 5312, 0, 0, 0, 1000, 0, 0),
        (1, 4601, 4666, -1, 1, 0, -1000, 0, 0),
        (1, 4666, 4801, -1, 0, 0, -1000, 0, 0),
        (1, 4801, 4813, -1, 1, 0, -1000, 0, 0),
        (1, 4813, 4831, -1, 0, 0, -1000, 0, 0),
        (1, 4831, 4905, 0, 0, 0, -1000, 0, 0),
        (1, 4905, 4943, -1, 0, 0, -1000, 0, 0),
        (1, 4943, 4955, 0, 1, 0, -1000, 0, 0),
        (1, 4955, 4960, 1, 1, 0, 1000, 0, 0),
        (1, 4960, 4965, 0, 1, 0, -1000, 0, 0),
        (1, 4965, 4970, 0, 1, 0, 0, 0, 0),
        (1, 4970, 4971, -1, 1, 0, -1000, 0, 0),
        (1, 4971, 5000, -1, 0, 0, -1000, 0, 0),
        (1, 5000, 5001, 0, 1, 0, 1000, 0, 0),
        (1, 5002, 5004, 0, 1, 0, 0, 0, 0),
        (1, 5004, 5005, 0, 1, 1, 0, 0, 1),
        (1, 5005, 5043, 0, 1, 0, 0, 0, 0),
        (1, 5043, 5044, 0, 1, 1, 0, 0, 1),
        (1, 5044, 5055, 0, 1, 0, 0, 0, 0),
        (1, 5055, 5056, 0, 1, 0, 1000, 0, 0),
        (1, 5056, 5065, 0, 1, 0, 0, 0, 0),
        (1, 5065, 5066, 0, 1, 0, 1000, 0, 0),
        (1, 5066, 5070, 0, 1, 0, 0, 0, 0),
        (1, 5072, 5073, 0, 0, 0, 1000, 0, 0),
        (1, 5082, 5083, 0, 0, 1, 0, 0, 1),
        (1, 5084, 5135, -1, 0, 0, -1000, 0, 0),
        (1, 5135, 5175, 1, 1, 0, 1000, 0, 0),
        (1, 5175, 5180, -1, 1, 0, -1000, 0, 0),
        (1, 5180, 5184, 0, 1, 0, -1000, 0, 0),
        (1, 5184, 5192, 0, 0, 0, -1000, 0, 0),
        (1, 5192, 5213, -1, 0, 0, -1000, 0, 0),
        (1, 5213, 5235, 1, 1, 0, 1000, 0, 0),
        (1, 5235, 5240, -1, 1, 0, -1000, 0, 0),
        (1, 5240, 5244, 0, 1, 0, -1000, 0, 0),
        (1, 5244, 5290, 0, 0, 0, -1000, 0, 0),
        (1, 5290, 5304, -1, 0, 0, -1000, 0, 0),
        (1, 5304, 5310, 1, 0, 0, 1000, 0, 0),
        (1, 5310, 5311, 0, 0, 0, 1000, 0, 0),
        (1, 5311, 5312, 0, 0, 1, 0, 0, 1),
        (1, 4958, 4959, 1, 1, 1, -1000, 350, 0),
    ];
    let mut input = PlayerInput::default();
    for (owner, start, end, movement, jump, fire, aim_x, aim_y, track) in actions {
        if owner == player && (start..end).contains(&tick) {
            input = PlayerInput {
                move_axis: movement,
                jump: jump != 0,
                fire: fire != 0,
                aim_x,
                aim_y,
                aim_at_opponent: track != 0,
                ..PlayerInput::default()
            };
        }
    }
    input
}

pub fn scripted_inputs_for(
    profile: ReplayProfile,
    _seed: u64,
    ticks: u32,
) -> [Vec<PlayerInput>; 2] {
    let mut scripts = [
        Vec::with_capacity(ticks as usize),
        Vec::with_capacity(ticks as usize),
    ];
    for tick in 0..ticks {
        if profile == ReplayProfile::LimeModularArenaReplay {
            let orange_movement = if (12..68).contains(&tick) {
                1
            } else if (68..74).contains(&tick) {
                -1
            } else {
                0
            };
            let blue_movement = -orange_movement;
            scripts[0].push(PlayerInput {
                move_axis: orange_movement,
                aim_x: 1_000,
                jump: (0..34).contains(&tick) || tick == 52 || (54..88).contains(&tick),
                ..PlayerInput::default()
            });
            scripts[1].push(PlayerInput {
                move_axis: blue_movement,
                aim_x: -1_000,
                jump: (0..34).contains(&tick) || tick == 52 || (54..88).contains(&tick),
                ..PlayerInput::default()
            });
            continue;
        } else if profile == ReplayProfile::MatchEndWaitingReplay {
            scripts[0].push(PlayerInput {
                aim_x: 1_000,
                ..PlayerInput::default()
            });
            scripts[1].push(PlayerInput {
                aim_x: -1_000,
                fire: tick == 0,
                ..PlayerInput::default()
            });
            continue;
        } else if profile == ReplayProfile::RematchDraftReplay {
            let mut orange = PlayerInput {
                aim_x: 1_000,
                ..PlayerInput::default()
            };
            let mut blue = PlayerInput {
                aim_x: -1_000,
                ..PlayerInput::default()
            };
            orange.flow = match tick {
                270 => Some(FlowCommand {
                    phase_revision: 1,
                    action: FlowAction::VoteYes,
                }),
                590 => Some(FlowCommand {
                    phase_revision: 3,
                    action: FlowAction::Hover(ItemId::Burst),
                }),
                660 => Some(FlowCommand {
                    phase_revision: 3,
                    action: FlowAction::Hover(ItemId::Dazzle),
                }),
                750 => Some(FlowCommand {
                    phase_revision: 3,
                    action: FlowAction::Confirm(ItemId::Dazzle),
                }),
                _ => None,
            };
            blue.flow = match tick {
                330 => Some(FlowCommand {
                    phase_revision: 1,
                    action: FlowAction::VoteYes,
                }),
                1_559 => Some(FlowCommand {
                    phase_revision: 6,
                    action: FlowAction::Hover(ItemId::Lifestealer),
                }),
                2_000 => Some(FlowCommand {
                    phase_revision: 6,
                    action: FlowAction::Hover(ItemId::Echo),
                }),
                2_060 => Some(FlowCommand {
                    phase_revision: 6,
                    action: FlowAction::Hover(ItemId::ExplosiveBullet),
                }),
                2_100 => Some(FlowCommand {
                    phase_revision: 6,
                    action: FlowAction::Confirm(ItemId::ExplosiveBullet),
                }),
                _ => None,
            };
            orange.move_axis = 0;
            blue.move_axis = 0;
            orange.fire = matches!(tick, 2_220 | 2_330)
                || (4_000..4_341).contains(&tick) && tick % 40 == 20
                || tick == 4_425;
            orange.aim_at_opponent = tick >= 4_000 && tick != 4_425;
            if tick == 4_425 {
                orange.aim_x = -1_000;
                orange.aim_y = 50;
            }
            blue.fire = matches!(tick, 2_260 | 2_350 | 2_440 | 2_518 | 2_557 | 3_650);
            if (3_500..3_680).contains(&tick) {
                blue.aim_x = 1_000;
                blue.aim_y = 100;
            }
            if (4_120..4_430).contains(&tick) {
                orange.move_axis = 1;
                blue.move_axis = -1;
            }
            // Both holds run to the end of their fighter's rise, so the tick they
            // are let go of is one the fighter is either grounded on or already
            // falling on and a jump-release cut can never reach. Orange's ends at
            // 4541, where the ice respawn zeroes the velocity it was frozen at
            // when blue was eliminated; blue's at 4432, where it starts falling.
            orange.jump = (4_000..4_541).contains(&tick);
            blue.jump = (3_000..3_365).contains(&tick)
                || (3_700..3_970).contains(&tick)
                || (4_000..4_432).contains(&tick);
            if (3_000..3_365).contains(&tick) {
                blue.move_axis = -1;
            }
            if (3_700..3_970).contains(&tick) {
                blue.move_axis = 1;
            }
            if tick >= CONNECTED_ICE_COMBAT_TICK {
                orange = connected_ice_input(0, tick);
                blue = connected_ice_input(1, tick);
                orange.flow = match tick {
                    5_587 => Some(FlowCommand {
                        phase_revision: 23,
                        action: FlowAction::Hover(ItemId::Overpower),
                    }),
                    5_709 => Some(FlowCommand {
                        phase_revision: 23,
                        action: FlowAction::Hover(ItemId::QuickShot),
                    }),
                    5_801 => Some(FlowCommand {
                        phase_revision: 23,
                        action: FlowAction::Confirm(ItemId::QuickShot),
                    }),
                    _ => None,
                };
            }
            scripts[0].push(orange);
            scripts[1].push(blue);
            continue;
        }
        if profile == ReplayProfile::RadialSawHalfBlueReplay {
            let mut orange = PlayerInput {
                aim_x: 1_000,
                aim_y: 0,
                ..PlayerInput::default()
            };
            let mut blue = PlayerInput {
                aim_x: -992,
                aim_y: 126,
                ..PlayerInput::default()
            };
            orange.move_axis = if (0..185).contains(&tick)
                || (210..365).contains(&tick)
                || (510..545).contains(&tick)
                || (810..842).contains(&tick)
                || (880..908).contains(&tick)
            {
                1
            } else {
                0
            };
            blue.move_axis = if (0..185).contains(&tick)
                || (210..365).contains(&tick)
                || (510..545).contains(&tick)
                || (760..900).contains(&tick)
            {
                -1
            } else if (545..760).contains(&tick) {
                1
            } else {
                0
            };
            // Each press is held through the ticks the fighter is already
            // airborne, so a jump that is still held keeps its arc. The holds
            // are inert today: `set_player_control` jumps only when grounded.
            orange.jump = matches!(tick, 160 | 340 | 520 | 820 | 888)
                || (163..164).contains(&tick)
                || (343..344).contains(&tick)
                || (523..525).contains(&tick)
                || (824..825).contains(&tick)
                || (892..893).contains(&tick);
            blue.jump = tick == 650 || (652..659).contains(&tick);
            orange.fire = tick == 359;
            if tick == 359 {
                orange.aim_x = 0;
                orange.aim_y = 1_000;
            }
            blue.fire = matches!(tick, 520 | 897);
            if tick == 520 {
                blue.aim_x = -992;
                blue.aim_y = 126;
            }
            if tick == 897 {
                blue.aim_x = -992;
                blue.aim_y = 126;
            }
            scripts[0].push(orange);
            scripts[1].push(blue);
            continue;
        }
        if profile == ReplayProfile::YellowCrateTerminalBlastReplay {
            let mut orange = PlayerInput {
                aim_x: 1_000,
                ..PlayerInput::default()
            };
            let mut blue = PlayerInput {
                aim_x: -1_000,
                ..PlayerInput::default()
            };
            orange.move_axis = if tick < 40 {
                1
            } else if tick < 44 {
                -1
            } else {
                0
            };
            blue.move_axis = if (15..40).contains(&tick) {
                -1
            } else if (40..44).contains(&tick) {
                1
            } else {
                0
            };
            orange.fire = tick + 1 == YELLOW_IMPACT_TICK;
            scripts[0].push(orange);
            scripts[1].push(blue);
            continue;
        }
        if profile == ReplayProfile::TimberCollapseReplay {
            let mut orange = PlayerInput {
                aim_x: 700,
                aim_y: 700,
                ..PlayerInput::default()
            };
            let mut blue = PlayerInput {
                aim_x: -700,
                aim_y: 700,
                ..PlayerInput::default()
            };
            orange.move_axis = if (120..300).contains(&tick) || (960..1_100).contains(&tick) {
                1
            } else {
                0
            };
            blue.move_axis = if (260..430).contains(&tick) {
                -1
            } else if (1_150..1_310).contains(&tick) {
                1
            } else {
                0
            };
            // Held through each press's airborne ticks. Orange's 960 press has
            // none: it reports grounded on every tick that follows it.
            orange.jump = matches!(tick, 120 | 960 | 1_280)
                || (122..170).contains(&tick)
                || (1_299..1_305).contains(&tick);
            blue.jump = matches!(tick, 260 | 1_150)
                || (262..310).contains(&tick)
                || (1_159..1_160).contains(&tick);
            orange.fire = matches!(tick, 820 | 1_100);
            blue.fire = matches!(tick, 620 | 1_320);
            scripts[0].push(orange);
            scripts[1].push(blue);
            continue;
        }
        let mut orange = PlayerInput {
            aim_x: 1_000,
            ..PlayerInput::default()
        };
        let mut blue = PlayerInput {
            aim_x: -1_000,
            ..PlayerInput::default()
        };
        if (40..100).contains(&tick) {
            blue.move_axis = -1;
        }
        if (115..155).contains(&tick) {
            blue.move_axis = 1;
        }
        if (330..670).contains(&tick) {
            orange.move_axis = 1;
        }
        if (500..560).contains(&tick) {
            blue.move_axis = -1;
        }
        orange.jump = (330..670).contains(&tick);
        // Each blue press is held through its airborne ticks; all three cover
        // the whole rise.
        blue.jump = matches!(tick, 40 | 500 | 650)
            || (42..74).contains(&tick)
            || (502..534).contains(&tick)
            || (652..681).contains(&tick);
        if tick == 292 {
            orange.aim_y = 180;
        }
        if tick == 416 {
            orange.aim_y = -300;
        }
        if tick == 620 {
            orange.aim_y = 500;
        }
        if tick == 785 {
            orange.aim_x = 120;
            orange.aim_y = -1_000;
        }
        if tick == 690 {
            blue.aim_x = 200;
            blue.aim_y = 1_000;
        }
        orange.fire = matches!(tick, 292 | 416 | 620 | 785);
        orange.block = (650..720).contains(&tick);
        blue.fire = tick == 690;
        blue.block = (410..450).contains(&tick);
        scripts[0].push(orange);
        scripts[1].push(blue);
    }
    scripts
}

pub fn run_scripted_match(seed: u64, ticks: u32) -> (MatchSnapshot, String) {
    run_profile_match(ReplayProfile::default(), seed, ticks)
}

pub fn run_profile_match(profile: ReplayProfile, seed: u64, ticks: u32) -> (MatchSnapshot, String) {
    let snapshot = run_profile_snapshots(profile, seed, ticks)
        .pop()
        .unwrap_or_else(|| AuthoritativeMatch::new_with_profile(seed, profile).snapshot());
    let state_hash = hash_snapshot(&snapshot);
    (snapshot, state_hash)
}

/// Reaches the delivered stable match-end endpoint, applies the explicit
/// non-player lifecycle request, and advances through the ordinary fade into
/// orange's first draft. This is evidence for the boundary, not an extension
/// of `match-end-waiting-replay`'s terminal input trace.
pub fn run_new_match_draft(seed: u64) -> (MatchSnapshot, String) {
    let scripts = scripted_inputs_for(
        ReplayProfile::MatchEndWaitingReplay,
        seed,
        MATCH_END_WAITING_REPLAY_TICKS,
    );
    let mut simulation =
        AuthoritativeMatch::new_with_profile(seed, ReplayProfile::MatchEndWaitingReplay);
    for (&orange, &blue) in scripts[0].iter().zip(&scripts[1]) {
        simulation.step([orange, blue]);
    }
    assert_eq!(
        simulation.request_lifecycle(LifecycleRequest::BeginNewMatch),
        LifecycleResult::Accepted,
        "the delivered endpoint must be Waiting"
    );
    for _ in 0..NEW_MATCH_DRAFT_FADE_TICKS {
        simulation.step([PlayerInput::default(); 2]);
    }
    let snapshot = simulation.snapshot();
    let state_hash = hash_snapshot(&snapshot);
    (snapshot, state_hash)
}

pub fn run_scripted_snapshots(seed: u64, ticks: u32) -> Vec<MatchSnapshot> {
    run_profile_snapshots(ReplayProfile::default(), seed, ticks)
}

pub fn run_profile_snapshots(profile: ReplayProfile, seed: u64, ticks: u32) -> Vec<MatchSnapshot> {
    let scripts = scripted_inputs_for(profile, seed, ticks);
    let mut simulation = AuthoritativeMatch::new_with_profile(seed, profile);
    scripts[0]
        .iter()
        .copied()
        .zip(scripts[1].iter().copied())
        .map(|(player_zero, player_one)| {
            let observation = simulation.snapshot();
            simulation.step([
                player_zero.with_progressive_observation(0, Some(&observation)),
                player_one.with_progressive_observation(1, Some(&observation)),
            ]);
            simulation.snapshot()
        })
        .collect()
}

pub fn dynamic_body_digest(snapshot: &MatchSnapshot) -> String {
    let bytes = serde_json::to_vec(&snapshot.dynamic_bodies)
        .expect("dynamic body serialization cannot fail");
    format!("{:x}", Sha256::digest(bytes))
}

pub fn arena_digest(snapshot: &MatchSnapshot) -> String {
    digest_json(&snapshot.arena)
}

pub fn saw_digest(snapshot: &MatchSnapshot) -> String {
    digest_json(&snapshot.saws)
}

pub fn combat_digest(snapshot: &MatchSnapshot) -> String {
    digest_json(&(
        &snapshot.players,
        &snapshot.projectiles,
        &snapshot.impacts,
        &snapshot.metrics,
        snapshot.winner,
    ))
}

pub fn round_digest(snapshot: &MatchSnapshot) -> Option<String> {
    snapshot.round.as_ref().map(digest_json)
}

fn digest_json(value: &impl Serialize) -> String {
    let bytes = serde_json::to_vec(value).expect("snapshot projection serialization cannot fail");
    format!("{:x}", Sha256::digest(bytes))
}
