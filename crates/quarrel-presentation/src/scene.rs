use super::*;
use bevy::camera::{OrthographicProjection, Projection, ScalingMode, Viewport};

pub(super) fn camera_viewport(snapshot: &MatchSnapshot, target: UVec2) -> Option<Viewport> {
    if snapshot
        .flow
        .as_ref()
        .is_some_and(|flow| flow.phase != FlowPhase::Combat)
    {
        return None;
    }
    let arena = snapshot.arena_objects.as_ref()?;
    let width = arena.frame[2] - arena.frame[0];
    let height = arena.frame[3] - arena.frame[1];
    if width <= 0.0 || height <= 0.0 {
        return None;
    }
    let scale = (target.x as f32 / width).min(target.y as f32 / height);
    let size = (Vec2::new(width, height) * scale)
        .floor()
        .as_uvec2()
        .max(UVec2::ONE);
    Some(Viewport {
        physical_position: (target - size) / 2,
        physical_size: size,
        ..default()
    })
}

pub(super) fn camera_projection(snapshot: &MatchSnapshot) -> Projection {
    if snapshot
        .flow
        .as_ref()
        .is_some_and(|flow| flow.phase != FlowPhase::Combat)
    {
        return ui_projection();
    }
    let Some(arena) = &snapshot.arena_objects else {
        return Projection::Orthographic(OrthographicProjection::default_2d());
    };
    Projection::Orthographic(OrthographicProjection {
        scaling_mode: ScalingMode::AutoMin {
            min_width: arena.frame[2] - arena.frame[0],
            min_height: arena.frame[3] - arena.frame[1],
        },
        ..OrthographicProjection::default_2d()
    })
}

pub(super) fn ui_projection() -> Projection {
    Projection::Orthographic(OrthographicProjection {
        scaling_mode: ScalingMode::AutoMin {
            min_width: 1280.0,
            min_height: 720.0,
        },
        ..OrthographicProjection::default_2d()
    })
}

pub(super) fn camera_state(
    snapshot: &MatchSnapshot,
) -> (Transform, Bloom, ChromaticAberration, LensDistortion) {
    let center = if snapshot
        .flow
        .as_ref()
        .is_some_and(|flow| flow.phase != FlowPhase::Combat)
    {
        Vec2::ZERO
    } else {
        snapshot
            .arena_objects
            .as_ref()
            .map(|arena| {
                Vec2::new(
                    (arena.frame[0] + arena.frame[2]) * 0.5,
                    (arena.frame[1] + arena.frame[3]) * 0.5,
                )
            })
            .unwrap_or(Vec2::ZERO)
    };
    (
        Transform::from_xyz(center.x, center.y, 0.0),
        Bloom::NATURAL,
        ChromaticAberration {
            intensity: 0.0,
            ..default()
        },
        LensDistortion {
            intensity: 0.0,
            ..default()
        },
    )
}

#[cfg(test)]
mod tests {
    use super::*;
    use bevy::camera::CameraProjection;

    #[test]
    fn visible_projection_fits_the_arena_without_stretching() {
        let mut snapshot = AuthoritativeMatch::new(38).snapshot();
        snapshot.flow = None;
        let Projection::Orthographic(mut projection) = camera_projection(&snapshot) else {
            panic!("expected orthographic camera")
        };
        projection.update(1_280.0, 720.0);
        assert!(
            (projection.area.width() / projection.area.height() - 1_280.0 / 720.0).abs() < 0.001
        );
        let frame = snapshot.arena_objects.unwrap().frame;
        assert!(projection.area.width() >= frame[2] - frame[0]);
        assert!(projection.area.height() >= frame[3] - frame[1]);
    }

    #[test]
    fn arena_camera_keeps_the_data_frame_aspect_ratio() {
        let mut snapshot = AuthoritativeMatch::new(38).snapshot();
        snapshot.flow = None;
        let viewport = camera_viewport(&snapshot, UVec2::new(1_280, 720)).unwrap();
        let frame = snapshot.arena_objects.as_ref().unwrap().frame;
        let viewport_aspect = viewport.physical_size.x as f32 / viewport.physical_size.y as f32;
        let frame_aspect = (frame[2] - frame[0]) / (frame[3] - frame[1]);
        assert!((viewport_aspect - frame_aspect).abs() < 0.02);
    }
}
