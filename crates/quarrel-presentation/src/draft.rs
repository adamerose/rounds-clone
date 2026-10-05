use super::*;
use super::{
    card_art::spawn_card_art,
    hud::{spawn_triangle, wrapped_rules},
};

pub(super) fn spawn_left_half_disc(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    center: Vec2,
    radius: f32,
    color: Color,
    z: f32,
) {
    for segment in 0..12 {
        let start = std::f32::consts::FRAC_PI_2 + segment as f32 * std::f32::consts::PI / 12.0;
        let end = std::f32::consts::FRAC_PI_2 + (segment + 1) as f32 * std::f32::consts::PI / 12.0;
        spawn_triangle(
            commands,
            meshes,
            materials,
            [
                center,
                center + Vec2::from_angle(start) * radius,
                center + Vec2::from_angle(end) * radius,
            ],
            color,
            z,
        );
    }
}

pub(super) fn spawn_draft_scene(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    let flow = snapshot
        .flow
        .as_ref()
        .expect("draft profile has flow state");
    let fade_alpha = match flow.phase {
        FlowPhase::ArenaFade => (flow.phase_tick as f32 / 150.0).clamp(0.0, 1.0),
        FlowPhase::ArenaTransition => (1.0 - flow.phase_tick as f32 / 60.0).clamp(0.0, 1.0),
        _ => 1.0,
    };
    commands.spawn((
        SceneVisual,
        Sprite::from_color(
            if flow.phase == FlowPhase::PostRoundBridge {
                Color::srgba(0.025, 0.13, 0.28, 0.96)
            } else {
                Color::srgba(0.005, 0.025, 0.05, 0.90 * fade_alpha)
            },
            Vec2::new(1_280.0, 720.0),
        ),
        Transform::from_xyz(0.0, 0.0, 25.0),
    ));
    let bridge_entry = flow.phase == FlowPhase::PostRoundBridge && flow.phase_tick == 0;
    if matches!(
        flow.phase,
        FlowPhase::ArenaFade | FlowPhase::ArenaTransition
    ) || (flow.phase == FlowPhase::PostRoundBridge && !bridge_entry)
    {
        return;
    }
    if flow.phase == FlowPhase::Handoff {
        return;
    }
    let player = flow
        .active_player
        .or_else(|| {
            flow.offers
                .iter()
                .position(|offers| !offers.is_empty())
                .map(|player| player as u8)
        })
        .unwrap_or(0);
    let base = if player == 0 {
        Color::srgb_u8(242, 76, 42)
    } else {
        Color::srgb_u8(43, 137, 244)
    };
    let accent = if player == 0 {
        Color::srgb_u8(255, 153, 50)
    } else {
        Color::srgb_u8(78, 204, 255)
    };
    let offers = &flow.offers[usize::from(player)];
    let hovered = flow.hovered[usize::from(player)];
    let focused_index = hovered
        .and_then(|item| offers.iter().position(|offer| *offer == item))
        .unwrap_or(2);
    let focus = (focused_index as f32 - 2.0) / 2.0;
    let selected_item = flow.revealed.or(flow.selected[usize::from(player)]);
    let reveal_pose = if selected_item.is_some() { 1.0 } else { 0.0 };
    let confirmation_progress = if flow.phase == FlowPhase::Reveal {
        (flow.phase_tick as f32 / 18.0).clamp(0.0, 1.0)
    } else {
        0.0
    };
    let confirmation_settled = flow.phase == FlowPhase::Reveal && flow.phase_tick >= 12;
    let breathe = (snapshot.tick as f32 * 0.045).sin() * 5.0;
    commands.spawn((
        SceneVisual,
        CaptureElement::Character,
        Mesh2d(meshes.add(Circle::new(240.0))),
        MeshMaterial2d(materials.add(Color::srgba_u8(
            if player == 0 { 222 } else { 25 },
            if player == 0 { 57 } else { 103 },
            if player == 0 { 34 } else { 211 },
            230,
        ))),
        Transform::from_xyz(0.0, -265.0 + breathe, 30.0).with_scale(Vec3::new(1.55, 1.0, 1.0)),
    ));
    for side in [-1.0_f32, 1.0] {
        let focus_affinity = (1.0 - (focus - side).abs() * 0.5).clamp(0.0, 1.0);
        let hand_x = side * (490.0 - reveal_pose * 42.0) + focus * 18.0;
        let hand_y = -25.0 + breathe + focus_affinity * 32.0 + reveal_pose * 48.0;
        commands.spawn((
            SceneVisual,
            CaptureElement::Hand,
            Sprite::from_color(base, Vec2::new(62.0, 340.0)),
            Transform::from_xyz(
                side * (425.0 - reveal_pose * 25.0),
                -190.0 + hand_y * 0.18,
                38.0,
            )
            .with_rotation(Quat::from_rotation_z(
                side * (-0.24 - focus_affinity * 0.06 - reveal_pose * 0.08),
            )),
        ));
        commands.spawn((
            SceneVisual,
            CaptureElement::Hand,
            Mesh2d(meshes.add(Circle::new(43.0))),
            MeshMaterial2d(materials.add(accent)),
            Transform::from_xyz(hand_x, hand_y, 42.0),
        ));
    }
    commands.spawn((
        SceneVisual,
        Sprite::from_color(Color::srgb_u8(229, 224, 207), Vec2::new(182.0, 92.0)),
        Transform::from_xyz(0.0, -125.0 + breathe, 37.0),
    ));
    commands.spawn((
        SceneVisual,
        Sprite::from_color(Color::srgb_u8(32, 32, 39), Vec2::new(230.0, 38.0)),
        Transform::from_xyz(0.0, -64.0 + breathe, 41.0),
    ));
    for side in [-1.0_f32, 1.0] {
        commands.spawn((
            SceneVisual,
            Mesh2d(meshes.add(Circle::new(14.0))),
            MeshMaterial2d(materials.add(Color::WHITE)),
            Transform::from_xyz(side * 48.0, -135.0 + breathe, 42.0),
        ));
        commands.spawn((
            SceneVisual,
            Mesh2d(meshes.add(Circle::new(6.0))),
            MeshMaterial2d(materials.add(Color::srgb_u8(25, 31, 37))),
            Transform::from_xyz(
                side * 46.0 + focus * 6.0,
                -137.0 + breathe + focus.abs() * 2.0 - reveal_pose * 4.0,
                43.0,
            ),
        ));
    }
    commands.spawn((
        SceneVisual,
        if reveal_pose > 0.0 {
            Mesh2d(meshes.add(Circle::new(13.0)))
        } else {
            Mesh2d(meshes.add(RegularPolygon::new(11.0, 4)))
        },
        MeshMaterial2d(materials.add(Color::srgb_u8(45, 28, 31))),
        Transform::from_xyz(focus * 3.0, -170.0 + breathe, 43.0).with_scale(Vec3::new(
            1.5,
            if reveal_pose > 0.0 { 1.0 } else { 0.25 },
            1.0,
        )),
    ));

    for (index, item_id) in offers.iter().enumerate() {
        if bridge_entry && selected_item == Some(*item_id) {
            continue;
        }
        let item = flow
            .catalog
            .iter()
            .find(|item| item.id == *item_id)
            .expect("offered item registered");
        let centered = index as f32 - 2.0;
        let selected_offscreen = confirmation_settled && selected_item == Some(*item_id);
        let highlighted = if matches!(flow.phase, FlowPhase::Reveal | FlowPhase::PostRoundReveal) {
            !confirmation_settled && selected_item == Some(*item_id)
        } else {
            hovered == Some(*item_id)
        };
        let angle = centered * -0.10;
        let x = centered * 190.0;
        let y = 73.0 - centered.abs().powf(1.35) * 22.0 + if highlighted { 72.0 } else { 0.0 }
            - confirmation_progress * 135.0
            - if selected_offscreen { 520.0 } else { 0.0 };
        spawn_card(
            commands,
            meshes,
            materials,
            item,
            Vec2::new(x, y),
            angle,
            highlighted,
            selected_item == Some(*item_id),
            50.0 + index as f32,
            CardPresentation {
                item: *item_id,
                highlighted,
                selected_offscreen,
            },
        );
        if flow.phase == FlowPhase::PostRoundDraft && flow.phase_tick < 60 && index >= 3 {
            commands.spawn((
                SceneVisual,
                Sprite::from_color(Color::srgba_u8(10, 13, 22, 245), Vec2::new(157.0, 250.0)),
                Transform::from_xyz(x, y, 70.0 + index as f32)
                    .with_rotation(Quat::from_rotation_z(angle))
                    .with_scale(Vec3::splat(0.92)),
            ));
        }
    }
}

#[expect(
    clippy::too_many_arguments,
    reason = "the card projection keeps renderer assets, item data, and five independent pose cues explicit"
)]
pub(super) fn spawn_card(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    item: &ItemDefinition,
    position: Vec2,
    angle: f32,
    highlighted: bool,
    revealed: bool,
    z: f32,
    presentation: CardPresentation,
) {
    let scale = if revealed {
        1.20
    } else if highlighted {
        1.10
    } else {
        0.92
    };
    let alpha = if highlighted { 255 } else { 145 };
    let palette = item.palette_rgb;
    commands.spawn((
        SceneVisual,
        Sprite::from_color(Color::srgba_u8(0, 5, 15, 170), Vec2::new(170.0, 272.0)),
        Transform::from_xyz(position.x + 12.0, position.y - 15.0, z - 1.0)
            .with_rotation(Quat::from_rotation_z(angle))
            .with_scale(Vec3::splat(scale)),
    ));
    commands.spawn((
        SceneVisual,
        CaptureElement::Card,
        presentation,
        Sprite::from_color(
            Color::srgba_u8(palette[0] / 5, palette[1] / 5, palette[2] / 5, alpha),
            Vec2::new(166.0, 268.0),
        ),
        Transform::from_xyz(position.x, position.y, z)
            .with_rotation(Quat::from_rotation_z(angle))
            .with_scale(Vec3::splat(scale)),
    ));
    commands.spawn((
        SceneVisual,
        Sprite::from_color(
            Color::srgba_u8(palette[0], palette[1], palette[2], alpha),
            Vec2::new(154.0, 5.0),
        ),
        Transform::from_xyz(position.x, position.y + 124.0 * scale, z + 1.0)
            .with_rotation(Quat::from_rotation_z(angle))
            .with_scale(Vec3::splat(scale)),
    ));
    spawn_card_art(
        commands,
        meshes,
        materials,
        item,
        position,
        angle,
        scale,
        alpha,
        z + 2.0,
    );
    commands.spawn((
        SceneVisual,
        Text2d::new(item.title.clone()),
        TextFont {
            font_size: FontSize::Px(if item.title.len() > 12 { 14.0 } else { 18.0 }),
            ..default()
        },
        TextColor(Color::srgba_u8(255, 255, 246, alpha)),
        TextLayout::justify(Justify::Center),
        Transform::from_xyz(position.x, position.y + 104.0, z + 3.0)
            .with_rotation(Quat::from_rotation_z(angle))
            .with_scale(Vec3::splat(scale)),
    ));
    commands.spawn((
        SceneVisual,
        Text2d::new(wrapped_rules(item)),
        TextFont {
            font_size: FontSize::Px(10.0),
            ..default()
        },
        TextColor(Color::srgba_u8(240, 245, 238, alpha)),
        TextLayout::justify(Justify::Center),
        Transform::from_xyz(position.x, position.y - 43.0, z + 3.0)
            .with_rotation(Quat::from_rotation_z(angle))
            .with_scale(Vec3::splat(scale)),
    ));
}
