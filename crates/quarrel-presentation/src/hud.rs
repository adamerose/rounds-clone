use super::*;

pub(super) fn wrapped_rules(item: &ItemDefinition) -> String {
    let mut output = Vec::new();
    for rule in &item.rules {
        let mut line = String::new();
        for word in rule.split_whitespace() {
            if !line.is_empty() && line.len() + word.len() + 1 > 23 {
                output.push(std::mem::take(&mut line));
            }
            if !line.is_empty() {
                line.push(' ');
            }
            line.push_str(word);
        }
        if !line.is_empty() {
            output.push(line);
        }
    }
    output.join("\n")
}

pub(super) fn fighter_color(index: usize) -> Color {
    const COLORS: [[u8; 3]; 6] = [
        [244, 84, 72],
        [54, 167, 252],
        [245, 190, 55],
        [159, 98, 239],
        [48, 205, 151],
        [246, 116, 190],
    ];
    let rgb = COLORS[index % COLORS.len()];
    Color::srgb_u8(rgb[0], rgb[1], rgb[2])
}

pub(super) fn spawn_flow_hud(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    let Some(flow) = &snapshot.flow else { return };
    for (fighter, score) in flow.scores.iter().copied().enumerate() {
        let y = 330.0 - fighter as f32 * 42.0;
        commands.spawn((
            SceneVisual,
            Text2d::new(format!("F{}  {score}/{}", fighter + 1, flow.target_score)),
            TextFont {
                font_size: FontSize::Px(22.0),
                ..default()
            },
            TextColor(fighter_color(fighter)),
            Transform::from_xyz(-570.0, y, 35.0),
        ));
        for pip in 0..flow.target_score.min(12) {
            let filled = pip < score;
            commands.spawn((
                SceneVisual,
                HudScorePip {
                    player: fighter as u8,
                    index: pip as u8,
                    filled,
                },
                Mesh2d(meshes.add(Circle::new(if filled { 5.0 } else { 2.0 }))),
                MeshMaterial2d(materials.add(if filled {
                    fighter_color(fighter)
                } else {
                    fighter_color(fighter).with_alpha(0.35)
                })),
                Transform::from_xyz(-470.0 + pip as f32 * 14.0, y + 2.0, 35.0),
            ));
        }
        let loadout = flow
            .loadouts
            .get(fighter)
            .into_iter()
            .flatten()
            .filter_map(|id| flow.catalog.iter().find(|item| item.id == *id))
            .map(|item| item.title.as_str())
            .collect::<Vec<_>>()
            .join(" / ");
        if !loadout.is_empty() {
            commands.spawn((
                SceneVisual,
                HudBadge {
                    player: fighter as u8,
                    label: loadout.clone(),
                },
                Text2d::new(loadout),
                TextFont {
                    font_size: FontSize::Px(14.0),
                    ..default()
                },
                TextColor(Color::srgba_u8(235, 242, 239, 210)),
                Transform::from_xyz(230.0, y, 35.0),
            ));
        }
    }
    match flow.phase {
        FlowPhase::Result => {
            commands.spawn((
                SceneVisual,
                Text2d::new("FIGHT OVER"),
                TextFont {
                    font_size: FontSize::Px(52.0),
                    ..default()
                },
                TextColor(Color::WHITE),
                Transform::from_xyz(0.0, 250.0, 35.0),
            ));
        }
        FlowPhase::MatchEnd => {
            commands.spawn((
                SceneVisual,
                Sprite::from_color(Color::srgba_u8(0, 8, 28, 190), Vec2::new(1_280.0, 720.0)),
                Transform::from_xyz(0.0, 0.0, 30.0),
            ));
            commands.spawn((
                SceneVisual,
                Text2d::new(if flow.run_backs < flow.run_it_back_limit {
                    "MATCH END - RUN IT BACK?"
                } else {
                    "MATCH END - RUN-BACK LIMIT REACHED"
                }),
                TextFont {
                    font_size: FontSize::Px(44.0),
                    ..default()
                },
                TextColor(Color::WHITE),
                Transform::from_xyz(0.0, 165.0, 31.0),
            ));
            for (fighter, vote) in flow.rematch_votes.iter().enumerate() {
                let choice = match vote {
                    quarrel_sim::RematchVote::Pending => "PENDING",
                    quarrel_sim::RematchVote::Yes => "RUN IT BACK",
                    quarrel_sim::RematchVote::No => "NEW MATCH",
                };
                commands.spawn((
                    SceneVisual,
                    Text2d::new(format!("F{}: {choice}", fighter + 1)),
                    TextFont {
                        font_size: FontSize::Px(27.0),
                        ..default()
                    },
                    TextColor(fighter_color(fighter)),
                    Transform::from_xyz(0.0, 80.0 - fighter as f32 * 44.0, 31.0),
                ));
            }
            commands.spawn((
                SceneVisual,
                Text2d::new(format!(
                    "F{} WINS",
                    flow.winner.map_or(1, |winner| winner + 1)
                )),
                TextFont {
                    font_size: FontSize::Px(38.0),
                    ..default()
                },
                TextColor(Color::WHITE),
                Transform::from_xyz(0.0, 240.0, 31.0),
            ));
            commands.spawn((
                SceneVisual,
                Text2d::new(if flow.run_backs < flow.run_it_back_limit {
                    "Keyboard: Y / N   Controller: A / B\nLocal second keyboard: K / L\nRun it back / New match - everyone must agree"
                } else {
                    "NEW MATCH: Keyboard N / Controller B\nLocal second keyboard: L\nEveryone must agree"
                }),
                TextFont {
                    font_size: FontSize::Px(20.0),
                    ..default()
                },
                TextColor(Color::srgb_u8(210, 225, 220)),
                Transform::from_xyz(0.0, -220.0, 31.0),
            ));
        }
        _ => {}
    };
}
