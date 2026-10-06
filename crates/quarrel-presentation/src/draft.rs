use super::*;
use crate::hud::wrapped_rules;

pub(super) fn spawn_draft_scene(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    let Some(flow) = &snapshot.flow else { return };
    commands.spawn((
        SceneVisual,
        Sprite::from_color(Color::srgba_u8(2, 8, 20, 220), Vec2::new(1_280.0, 720.0)),
        Transform::from_xyz(0.0, 0.0, 25.0),
    ));
    commands.spawn((
        SceneVisual,
        Text2d::new("PICK AN UPGRADE"),
        TextFont {
            font_size: FontSize::Px(42.0),
            ..default()
        },
        TextColor(Color::WHITE),
        Transform::from_xyz(0.0, 260.0, 26.0),
    ));
    let picking = flow
        .offers
        .iter()
        .enumerate()
        .filter(|(fighter, offers)| !offers.is_empty() && flow.selected[*fighter].is_none())
        .map(|(fighter, _)| format!("F{}", fighter + 1))
        .collect::<Vec<_>>()
        .join(" and ");
    for (fighter, offers) in flow.offers.iter().enumerate() {
        let selected = flow.selected.get(fighter).copied().flatten();
        let hovered = flow.hovered.get(fighter).copied().flatten();
        let row_y = 150.0 - fighter as f32 * 230.0;
        commands.spawn((
            SceneVisual,
            Text2d::new(if offers.is_empty() {
                format!("F{}\nWAITING", fighter + 1)
            } else if selected.is_some() {
                format!("F{}\nREADY", fighter + 1)
            } else {
                format!("F{}", fighter + 1)
            }),
            TextFont {
                font_size: FontSize::Px(22.0),
                ..default()
            },
            TextColor(crate::hud::fighter_color(fighter)),
            Transform::from_xyz(-590.0, row_y + 20.0, 27.0),
        ));
        for (index, id) in offers.iter().enumerate() {
            let Some(item) = flow.catalog.iter().find(|item| item.id == *id) else {
                continue;
            };
            let active = hovered == Some(*id) || selected == Some(*id);
            let x = -390.0 + index as f32 * 195.0;
            spawn_card(
                commands,
                meshes,
                materials,
                item,
                Vec2::new(x, row_y),
                active,
                28.0 + index as f32,
            );
        }
    }
    commands.spawn((
        SceneVisual,
        Text2d::new(format!(
            "{picking} picking\nKeyboard: A/D + Space    Controller: D-pad + A\nLocal only, no controllers: F2 keyboard: Arrows + Enter"
        )),
        TextFont {
            font_size: FontSize::Px(18.0),
            ..default()
        },
        TextColor(Color::WHITE),
        Transform::from_xyz(0.0, -285.0, 40.0),
    ));
}

fn spawn_card(
    commands: &mut Commands,
    _meshes: &mut Assets<Mesh>,
    _materials: &mut Assets<ColorMaterial>,
    item: &ItemDefinition,
    position: Vec2,
    active: bool,
    z: f32,
) {
    let palette = item.palette_rgb;
    commands.spawn((
        SceneVisual,
        CaptureElement::Card,
        CardPresentation {
            item: item.id,
            highlighted: active,
            selected_offscreen: false,
        },
        Sprite::from_color(
            if active {
                Color::srgb_u8(palette[0], palette[1], palette[2])
            } else {
                Color::srgba_u8(palette[0] / 3, palette[1] / 3, palette[2] / 3, 235)
            },
            Vec2::new(174.0, 178.0),
        ),
        Transform::from_xyz(position.x, position.y, z),
    ));
    commands.spawn((
        SceneVisual,
        Text2d::new(item.title.clone()),
        TextFont {
            font_size: FontSize::Px(19.0),
            ..default()
        },
        TextColor(Color::WHITE),
        Transform::from_xyz(position.x, position.y + 55.0, z + 1.0),
    ));
    commands.spawn((
        SceneVisual,
        Text2d::new(wrapped_rules(item)),
        TextFont {
            font_size: FontSize::Px(13.0),
            ..default()
        },
        TextColor(Color::srgb_u8(235, 240, 237)),
        TextLayout::justify(Justify::Center),
        Transform::from_xyz(position.x, position.y - 15.0, z + 1.0),
    ));
}
