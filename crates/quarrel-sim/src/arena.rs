use super::*;

pub(crate) fn collider_for_surface(surface: &ArenaSurfaceSnapshot) -> ColliderBuilder {
    if surface.outline_milli.len() < 3 {
        return ColliderBuilder::cuboid(
            surface.width_milli as f32 / 2_000.0,
            surface.height_milli as f32 / 2_000.0,
        );
    }
    let vertices = surface
        .outline_milli
        .iter()
        .map(|point| Vector::new(point[0] as f32 / 1_000.0, point[1] as f32 / 1_000.0))
        .collect::<Vec<_>>();
    let edges = (0..vertices.len())
        .map(|index| [index as u32, ((index + 1) % vertices.len()) as u32])
        .collect::<Vec<_>>();
    ColliderBuilder::convex_decomposition(&vertices, &edges)
}
