use super::*;
fn authority(n: usize, target: u16) -> FlowAuthority {
    FlowAuthority::with_config(
        MatchConfig {
            fighter_count: n,
            target_score: target,
            ..Default::default()
        },
        load_card_directory(&default_card_directory()).unwrap(),
    )
    .unwrap()
}
fn pick(flow: &mut FlowAuthority) {
    let snapshot = flow.snapshot();
    let commands: Vec<_> = snapshot
        .offers
        .iter()
        .map(|offer| {
            offer.first().map(|&id| FlowCommand {
                phase_revision: snapshot.phase_revision,
                action: FlowAction::Confirm(id),
            })
        })
        .collect();
    flow.advance(&commands);
    assert_eq!(flow.snapshot().phase, FlowPhase::Combat);
}
fn win(flow: &mut FlowAuthority, player: usize) {
    let mut alive = vec![false; flow.snapshot().scores.len()];
    alive[player] = true;
    assert!(flow.record_survivors(&alive));
    assert!(!flow.record_survivors(&alive));
    for _ in 0..30 {
        flow.advance(&[]);
    }
}
fn votes(flow: &mut FlowAuthority, choices: &[RematchVote]) {
    let revision = flow.snapshot().phase_revision;
    flow.advance(
        &choices
            .iter()
            .map(|v| match v {
                RematchVote::Pending => None,
                RematchVote::Yes => Some(FlowCommand {
                    phase_revision: revision,
                    action: FlowAction::VoteYes,
                }),
                RematchVote::No => Some(FlowCommand {
                    phase_revision: revision,
                    action: FlowAction::VoteNo,
                }),
            })
            .collect::<Vec<_>>(),
    );
}
#[test]
fn opening_pick_then_every_nonwinner_drafts_in_three_fighter_match() {
    let mut flow = authority(3, 3);
    assert!(flow.snapshot().offers.iter().all(|o| o.len() == 5));
    pick(&mut flow);
    for score in 1..=3 {
        win(&mut flow, 2);
        assert_eq!(flow.snapshot().scores, [0, 0, score]);
        if score < 3 {
            assert_eq!(flow.snapshot().phase, FlowPhase::Draft);
            assert!(flow.snapshot().offers[2].is_empty());
            let revision = flow.snapshot().phase_revision;
            let item = flow.snapshot().catalog[0].id;
            flow.advance(&[
                None,
                None,
                Some(FlowCommand {
                    phase_revision: revision,
                    action: FlowAction::Confirm(item),
                }),
            ]);
            assert_eq!(flow.snapshot().last_results[2], ActionResult::WrongPlayer);
            pick(&mut flow);
        }
    }
    assert_eq!(flow.snapshot().phase, FlowPhase::MatchEnd);
    assert_eq!(
        flow.snapshot()
            .loadouts
            .iter()
            .map(Vec::len)
            .collect::<Vec<_>>(),
        [3, 3, 1]
    );
}
#[test]
fn double_death_is_seeded_and_awards_one_of_the_last_fighters() {
    let mut a = authority(3, 5);
    let mut b = authority(3, 5);
    pick(&mut a);
    pick(&mut b);
    for flow in [&mut a, &mut b] {
        assert!(!flow.record_survivors(&[false, true, true]));
        assert!(flow.record_survivors(&[false, false, false]));
        assert!(matches!(flow.snapshot().winner, Some(1 | 2)));
        assert_eq!(flow.snapshot().scores.iter().sum::<u16>(), 1);
    }
    assert_eq!(a.snapshot(), b.snapshot());
}
#[test]
fn consensus_can_change_and_run_backs_keep_cards_and_scores_to_ten_then_fifteen() {
    let mut flow = authority(2, 5);
    pick(&mut flow);
    for target in [5, 10, 15] {
        while flow.snapshot().scores[0] < target {
            win(&mut flow, 0);
            if flow.snapshot().phase == FlowPhase::Draft {
                pick(&mut flow);
            }
        }
        assert_eq!(flow.snapshot().phase, FlowPhase::MatchEnd);
        let loadouts = flow.snapshot().loadouts;
        if target < 15 {
            votes(&mut flow, &[RematchVote::Yes, RematchVote::Pending]);
            assert_eq!(flow.snapshot().phase, FlowPhase::MatchEnd);
            votes(&mut flow, &[RematchVote::Pending, RematchVote::No]);
            assert_eq!(
                flow.snapshot().rematch_votes,
                [RematchVote::Yes, RematchVote::No]
            );
            votes(&mut flow, &[RematchVote::Pending, RematchVote::Yes]);
            assert_eq!(flow.snapshot().target_score, target + 5);
            assert_eq!(flow.snapshot().loadouts, loadouts);
            assert_eq!(flow.snapshot().scores, [target, 0]);
            pick(&mut flow);
        } else {
            votes(&mut flow, &[RematchVote::Yes, RematchVote::Yes]);
            assert_eq!(flow.snapshot().phase, FlowPhase::MatchEnd);
            assert_eq!(
                flow.snapshot().last_results,
                [ActionResult::LimitReached; 2]
            );
            votes(&mut flow, &[RematchVote::No, RematchVote::No]);
            assert_eq!(flow.snapshot().phase, FlowPhase::Draft);
            assert_eq!(flow.snapshot().scores, [0, 0]);
            assert!(flow.snapshot().loadouts.iter().all(Vec::is_empty));
            assert_eq!(flow.snapshot().target_score, 5);
        }
    }
}
#[test]
fn stale_commands_are_rejected_and_repeated_cards_stack() {
    let pool = load_card_directory(&default_card_directory()).unwrap();
    let mut flow = FlowAuthority::with_config(
        MatchConfig {
            offer_size: pool.len(),
            ..Default::default()
        },
        pool,
    )
    .unwrap();
    pick(&mut flow);
    let first = flow.snapshot().loadouts[1][0];
    win(&mut flow, 0);
    let revision = flow.snapshot().phase_revision;
    flow.advance(&[
        None,
        Some(FlowCommand {
            phase_revision: revision - 1,
            action: FlowAction::Confirm(first),
        }),
    ]);
    assert_eq!(flow.snapshot().last_results[1], ActionResult::Stale);
    flow.advance(&[
        None,
        Some(FlowCommand {
            phase_revision: revision,
            action: FlowAction::Confirm(first),
        }),
    ]);
    assert_eq!(flow.snapshot().loadouts[1], [first, first]);
}
#[test]
fn card_files_are_pool_and_reject_duplicates_and_invalid_config() {
    let pool = load_card_directory(&default_card_directory()).unwrap();
    assert!(pool.len() >= 5);
    let mut extra = pool.clone();
    let mut card = pool[0].clone();
    card.id = ItemId(123);
    extra.push(card);
    let size = extra.len();
    let mut flow = FlowAuthority::with_config(
        MatchConfig {
            offer_size: size,
            ..Default::default()
        },
        extra,
    )
    .unwrap();
    assert!(flow.snapshot().offers[0].contains(&ItemId(123)));
    pick(&mut flow);
    assert!(
        MatchConfig {
            fighter_count: 0,
            ..Default::default()
        }
        .validate()
        .is_err()
    );
    assert!(
        FlowAuthority::with_config(
            MatchConfig {
                offer_size: size,
                ..Default::default()
            },
            pool
        )
        .is_err()
    );
}
#[test]
fn newly_added_card_file_is_offered_and_duplicate_ids_are_rejected() {
    let path = std::env::temp_dir().join(format!("quarrel-card-pool-{}", std::process::id()));
    std::fs::create_dir_all(&path).unwrap();
    let file = path.join("extra.ron");
    let source = "(id: \"new-card\", name: \"New card\", description: \"Gain seven health.\", stat_changes: (health: 7))";
    std::fs::write(&file, source).unwrap();
    let catalog = load_card_directory(&path).unwrap();
    let mut flow = FlowAuthority::with_config(
        MatchConfig {
            offer_size: 1,
            ..Default::default()
        },
        catalog.clone(),
    )
    .unwrap();
    assert_eq!(flow.snapshot().offers[0], [catalog[0].id]);
    pick(&mut flow);
    assert_eq!(flow.snapshot().capabilities[0].health_bonus, 7);
    let duplicate = path.join("duplicate.ron");
    std::fs::write(&duplicate, source).unwrap();
    assert!(
        load_card_directory(&path)
            .unwrap_err()
            .contains("duplicate card id")
    );
    std::fs::remove_file(&duplicate).unwrap();
    std::fs::remove_file(&file).unwrap();
    std::fs::remove_dir(&path).unwrap();
}

#[test]
fn logical_card_content_rejects_the_same_invalid_rules_as_files() {
    let mut cards = load_card_directory(&default_card_directory()).unwrap();
    cards[0].event_rules = vec![EventRule {
        on: CardEvent::Fire,
        max_depth: 8,
        effects: vec![CardEffect::ExtraShots {
            count: 0,
            spread_milliradians: 0,
        }],
    }];
    assert!(
        FlowAuthority::with_config(MatchConfig::default(), cards)
            .err()
            .unwrap()
            .contains("effect out of range")
    );
}
