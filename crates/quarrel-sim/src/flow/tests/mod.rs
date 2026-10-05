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
    assert_eq!(catalog.len(), 21, "two source rows share three identities");
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
fn new_match_catalog_rows_keep_source_text_colour_and_catalog_only_gate() {
    use ItemId::*;
    let expected = [
        (
            SteadyShot,
            "STEADY SHOT",
            vec!["More HP", "More Bullet speed", "+0.25s Reload time"],
            [151, 154, 67],
            "steady-target",
        ),
        (
            Tank,
            "TANK",
            vec![
                "A huge amount of HP",
                "Slightly lower ATKSPD",
                "+0.5s Reload time",
            ],
            [98, 161, 92],
            "tank-treads",
        ),
        (
            TimedDetonation,
            "TIMED DETONATION",
            vec![
                "Bullets spawn bombs that explode after half a second",
                "Slightly lower DMG",
                "+0.25s Reload time",
            ],
            [221, 79, 62],
            "timed-bomb",
        ),
        (
            Homing,
            "HOMING",
            vec![
                "Bullets home towards visible targets",
                "Slightly lower DMG",
                "Slightly lower ATKSPD",
                "+0.25s Reload time",
            ],
            [229, 190, 55],
            "homing-circuit",
        ),
        (
            Huge,
            "HUGE",
            vec!["A bunch more HP"],
            [83, 159, 89],
            "huge-weight",
        ),
        (
            HealingField,
            "HEALING FIELD",
            vec![
                "Blocking creates a healing field",
                "More HP",
                "+0.25s Block cooldown",
            ],
            [65, 190, 83],
            "healing-aura",
        ),
        (
            Parasite,
            "PARASITE",
            vec![
                "Bullets deal damage over 5 seconds",
                "More Life steal",
                "More HP",
                "More DMG",
                "+0.25s Reload time",
            ],
            [211, 45, 229],
            "parasite-host",
        ),
    ];
    for (id, title, rules, palette, art_key) in expected {
        let definition = item_definition(id);
        assert_eq!(definition.title, title);
        assert_eq!(definition.rules, rules);
        assert_eq!(definition.rarity, ItemRarity::Unknown);
        assert_eq!(definition.palette_rgb, palette);
        assert_eq!(definition.art_key, art_key);
        assert_eq!(definition.implementation, ImplementationState::CatalogOnly);
        assert_eq!(definition.modifiers, None);

        let mut flow = FlowAuthority::new(SOURCE_DRAFT_SEED);
        flow.snapshot.offers = [vec![id], vec![id]];
        flow.transition(FlowPhase::Draft, Some(0));
        let revision = flow.snapshot.phase_revision;
        flow.advance([
            Some(FlowCommand {
                phase_revision: revision,
                action: FlowAction::Hover(id),
            }),
            None,
        ]);
        assert_eq!(flow.snapshot.last_results[0], ActionResult::Accepted);
        let retained = (
            flow.snapshot.phase,
            flow.snapshot.loadouts.clone(),
            flow.snapshot.capabilities,
        );
        flow.advance([
            Some(FlowCommand {
                phase_revision: revision,
                action: FlowAction::Confirm(id),
            }),
            None,
        ]);
        assert_eq!(
            flow.snapshot.last_results[0],
            ActionResult::UnimplementedItem
        );
        assert_eq!(
            (
                flow.snapshot.phase,
                flow.snapshot.loadouts.clone(),
                flow.snapshot.capabilities,
            ),
            retained
        );
    }
}

#[test]
fn waiting_lifecycle_resets_once_then_reuses_the_orange_draft_cadence() {
    let mut flow = FlowAuthority::match_end_waiting_replay(SOURCE_DRAFT_SEED);
    assert!(flow.record_elimination(1));
    while flow.snapshot.phase != FlowPhase::Waiting {
        flow.advance([None, None]);
    }
    flow.snapshot.prior_badges = [vec![PriorBadge::Po], vec![PriorBadge::Fa]];
    flow.snapshot.rematch_votes = [RematchVote::Yes, RematchVote::No];
    flow.snapshot.hovered = [Some(ItemId::Dazzle), Some(ItemId::ExplosiveBullet)];
    flow.snapshot.selected = [Some(ItemId::Dazzle), Some(ItemId::ExplosiveBullet)];
    flow.snapshot.revealed = Some(ItemId::Dazzle);
    flow.snapshot.last_results = [ActionResult::Accepted, ActionResult::Duplicate];
    flow.snapshot.accepted_actions = 9;

    assert_eq!(
        flow.request_lifecycle(LifecycleRequest::BeginNewMatch),
        LifecycleResult::Accepted
    );
    let reset = flow.snapshot();
    assert_eq!(reset.phase, FlowPhase::ArenaFade);
    assert_eq!(reset.phase_tick, 0);
    assert_eq!(reset.active_player, None);
    assert_eq!(reset.scores, [0, 0]);
    assert_eq!(reset.halves, [0, 0]);
    assert_eq!((reset.winner, reset.eliminated), (None, None));
    assert_eq!(reset.fighter_alive, [true, true]);
    assert!(reset.prior_badges.iter().all(Vec::is_empty));
    assert_eq!(reset.rematch_votes, [RematchVote::Pending; 2]);
    assert_eq!(reset.offers, new_match_source_offers());
    assert_eq!(reset.hovered, [None, None]);
    assert_eq!(reset.selected, [None, None]);
    assert_eq!(reset.revealed, None);
    assert!(reset.loadouts.iter().all(Vec::is_empty));
    assert_eq!(reset.capabilities, [FighterCapabilities::default(); 2]);
    assert_eq!(reset.last_results, [ActionResult::None; 2]);
    assert_eq!(reset.accepted_actions, 0);

    assert_eq!(
        flow.request_lifecycle(LifecycleRequest::BeginNewMatch),
        LifecycleResult::WrongPhase
    );
    assert_eq!(flow.snapshot(), reset);
    for _ in 0..150 {
        flow.advance([None, None]);
    }
    let draft = flow.snapshot();
    assert_eq!(draft.phase, FlowPhase::Draft);
    assert_eq!(draft.active_player, Some(0));
    assert_eq!(draft.offers, new_match_source_offers());
    assert_eq!(draft.hovered[0], Some(ItemId::SteadyShot));
}

#[test]
fn lifecycle_rejects_before_waiting_without_mutating_authority() {
    let mut flow = FlowAuthority::new(SOURCE_DRAFT_SEED);
    let before = flow.snapshot();
    assert_eq!(
        flow.request_lifecycle(LifecycleRequest::BeginNewMatch),
        LifecycleResult::WrongPhase
    );
    assert_eq!(flow.snapshot(), before);
}

#[test]
fn fresh_and_historical_construction_do_not_conflate_match_state() {
    let fresh = FlowAuthority::new(SOURCE_DRAFT_SEED).snapshot();
    assert_eq!(fresh.phase, FlowPhase::ResumedCombat);
    assert_eq!(fresh.scores, [0, 0]);
    assert_eq!(fresh.halves, [0, 0]);
    assert_eq!((fresh.winner, fresh.eliminated), (None, None));
    assert_eq!(fresh.fighter_alive, [true, true]);
    assert!(fresh.prior_badges.iter().all(Vec::is_empty));
    assert!(fresh.loadouts.iter().all(Vec::is_empty));

    let historical = FlowAuthority::historical_rematch(SOURCE_DRAFT_SEED).snapshot();
    assert_eq!(historical.phase, FlowPhase::CombatConclusion);
    assert_eq!(historical.scores, [4, 5]);
    assert_eq!(
        (historical.winner, historical.eliminated),
        (Some(1), Some(0))
    );
}

#[test]
fn fifth_round_keeps_result_envelope_then_waits_stably_for_both_winner_colours() {
    for winner in [0_u8, 1_u8] {
        let mut flow = FlowAuthority::match_end_waiting_replay(SOURCE_DRAFT_SEED);
        flow.snapshot.scores = if winner == 0 { [4, 3] } else { [3, 4] };
        let retained_loadouts = flow.snapshot.loadouts.clone();
        let retained_capabilities = flow.snapshot.capabilities;
        assert!(flow.record_elimination(winner));
        assert_eq!(flow.snapshot.phase, FlowPhase::EliminationConclusion);
        assert_eq!(flow.snapshot.halves[usize::from(winner)], 2);
        assert_eq!(flow.snapshot.scores[usize::from(winner)], 5);

        let result_phase = if winner == 0 {
            FlowPhase::OrangeResultTransition
        } else {
            FlowPhase::BlueResultTransition
        };
        let round_phase = if winner == 0 {
            FlowPhase::RoundOrange
        } else {
            FlowPhase::RoundBlue
        };
        while flow.snapshot.phase == FlowPhase::EliminationConclusion {
            flow.advance([None, None]);
        }
        assert_eq!(flow.snapshot.phase, result_phase);
        while flow.snapshot.phase == result_phase {
            flow.advance([None, None]);
        }
        assert_eq!(flow.snapshot.phase, round_phase);
        while flow.snapshot.phase == round_phase {
            flow.advance([None, None]);
        }
        assert_eq!(flow.snapshot.phase, FlowPhase::Waiting);
        assert_eq!(flow.snapshot.fighter_alive, [true, true]);
        assert_eq!(flow.snapshot.loadouts, retained_loadouts);
        assert_eq!(flow.snapshot.capabilities, retained_capabilities);
        assert_eq!(
            (flow.snapshot.winner, flow.snapshot.eliminated),
            (Some(winner), Some(1 - winner))
        );
        assert!(!flow.record_elimination(1 - winner));

        let revision = flow.snapshot.phase_revision;
        flow.advance([
            Some(FlowCommand {
                phase_revision: revision,
                action: FlowAction::VoteYes,
            }),
            Some(FlowCommand {
                phase_revision: revision,
                action: FlowAction::Confirm(ItemId::Dazzle),
            }),
        ]);
        assert_eq!(flow.snapshot.last_results, [ActionResult::WrongPhase; 2]);
        assert_eq!(flow.snapshot.scores[usize::from(winner)], 5);
        assert_eq!(flow.snapshot.phase, FlowPhase::Waiting);
        flow.snapshot.phase_tick = u32::MAX;
        flow.advance([None, None]);
        assert_eq!(flow.snapshot.phase_tick, u32::MAX);
    }
}

#[test]
fn both_yes_resets_score_and_old_loadouts_but_either_no_terminates() {
    let mut accepted = FlowAuthority::historical_rematch(SOURCE_DRAFT_SEED);
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

    let mut rejected = FlowAuthority::historical_rematch(SOURCE_DRAFT_SEED);
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
    let mut flow = FlowAuthority::historical_rematch(SOURCE_DRAFT_SEED);
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
    let mut flow = FlowAuthority::historical_rematch(SOURCE_DRAFT_SEED);
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
    flow.snapshot.scores = [3, 4];
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
    flow.snapshot.scores = if winner == 0 { [4, 3] } else { [3, 4] };
    flow.snapshot.halves = if winner == 0 { [2, 1] } else { [1, 2] };
    flow.snapshot.winner = Some(winner);
    flow.snapshot.eliminated = Some(1 - winner);
    flow.snapshot.loadouts = [vec![ItemId::Dazzle], vec![ItemId::ExplosiveBullet]];
    flow.snapshot.capabilities = [FighterCapabilities::default(); 2];
    flow.snapshot.capabilities[0].accumulate(item_definition(ItemId::Dazzle).modifiers.unwrap());
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
            if winner == 0 { [4, 3] } else { [3, 4] }
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
