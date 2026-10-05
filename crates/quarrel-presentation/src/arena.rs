use super::*;
use crate::{draft::spawn_draft_scene, hud::spawn_flow_hud};

/// Projects the current authority snapshot. Arenas are data-driven; match UI
/// is only an overlay and never reconstructs historical replay scenes.
pub(super) fn spawn_snapshot_scene(
    commands: &mut Commands,
    meshes: &mut Assets<Mesh>,
    materials: &mut Assets<ColorMaterial>,
    snapshot: &MatchSnapshot,
) {
    if snapshot.arena_objects.is_some() {
        crate::data_arena::spawn_data_arena_scene(commands, meshes, materials, snapshot);
    } else {
        commands.spawn((
            SceneVisual,
            CaptureElement::Background,
            Sprite::from_color(Color::srgb_u8(7, 16, 28), Vec2::new(1_280.0, 720.0)),
            Transform::from_xyz(0.0, 0.0, -100.0),
        ));
    }
    if let Some(flow) = &snapshot.flow {
        if flow.phase == FlowPhase::Draft {
            spawn_draft_scene(commands, meshes, materials, snapshot);
        }
        spawn_flow_hud(commands, meshes, materials, snapshot);
    }
}
