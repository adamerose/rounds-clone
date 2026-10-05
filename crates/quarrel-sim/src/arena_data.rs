use super::*;
use std::{
    collections::BTreeSet,
    path::{Path, PathBuf},
};

/// Bounds are [left, bottom, right, top]; all geometry uses world units, Y up.
#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
#[serde(deny_unknown_fields)]
pub struct ArenaDefinition {
    pub name: String,
    pub frame: [f32; 4],
    pub spawns: Vec<[f32; 2]>,
    #[serde(default)]
    pub objects: Vec<ArenaObject>,
    #[serde(default)]
    pub chains: Vec<ArenaChain>,
    /// Exact geometry used by the remaining footage profiles.
    #[serde(default)]
    pub surfaces: Vec<ArenaSurfaceSnapshot>,
    #[serde(default)]
    pub legacy_bodies: Vec<LegacyBodyDefinition>,
    #[serde(default)]
    pub legacy_saws: Vec<LegacySawDefinition>,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
pub struct LegacyBodyDefinition {
    pub id: u16,
    pub shape: DynamicBodyShape,
    pub position: [f32; 2],
    pub rotation: f32,
    pub width: f32,
    pub height: f32,
    pub radius: f32,
    pub face_rgb: [u8; 3],
    pub mass: f32,
    pub friction: f32,
    pub restitution: f32,
}
#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
pub struct LegacySawDefinition {
    pub id: u16,
    pub position: [f32; 2],
    pub radius: f32,
    pub teeth: u8,
    pub initial_angle: f32,
    pub angular_velocity: f32,
}

#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
#[serde(deny_unknown_fields)]
pub struct ArenaObject {
    pub id: u16,
    pub position: [f32; 2],
    #[serde(default)]
    pub rotation: f32,
    pub shape: ArenaShape,
    pub kind: ArenaKind,
    pub color: [u8; 3],
    #[serde(default = "unit_mass")]
    pub mass: f32,
    #[serde(default)]
    pub health: Option<u16>,
    #[serde(default)]
    pub motion: Option<ArenaMotion>,
}
fn unit_mass() -> f32 {
    1.0
}

#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
pub enum ArenaShape {
    Rectangle { size: [f32; 2] },
    Circle { radius: f32 },
    Polygon { vertices: Vec<[f32; 2]> },
}
#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
pub enum ArenaKind {
    Solid,
    Loose,
    Breakable {
        loose: bool,
    },
    Background,
    Saw {
        loose: bool,
        teeth: u8,
        angular_velocity: f32,
    },
}
#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
#[serde(deny_unknown_fields)]
pub struct ArenaMotion {
    /// Offsets from the object's initial position; closes back to the first point.
    pub path: Vec<[f32; 2]>,
    pub period_seconds: f32,
    #[serde(default)]
    pub angular_velocity: f32,
}
#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
#[serde(deny_unknown_fields)]
pub struct ArenaChain {
    pub id: u16,
    pub body_a: Option<u16>,
    pub body_b: u16,
    /// World anchor when body_a is None. Body endpoints use their centers.
    pub anchor: [f32; 2],
    pub length: f32,
}
#[derive(Clone, Debug, Deserialize, PartialEq, Serialize)]
pub struct ArenaRenderSnapshot {
    pub frame: [f32; 4],
    pub objects: Vec<ArenaObject>,
    pub chains: Vec<ArenaChain>,
    pub spawns: Vec<[f32; 2]>,
}

pub fn default_arena_directory() -> PathBuf {
    if let Some(path) = std::env::var_os("QUARREL_ARENA_DIR") {
        return PathBuf::from(path);
    }
    let current = std::env::current_dir().unwrap_or_default();
    for base in current.ancestors() {
        let path = base.join("assets/arenas");
        if path.is_dir() {
            return path;
        }
    }
    if let Ok(exe) = std::env::current_exe() {
        for base in exe.ancestors().skip(1) {
            let path = base.join("assets/arenas");
            if path.is_dir() {
                return path;
            }
        }
    }
    PathBuf::from("assets/arenas")
}

pub fn load_arena_directory(path: &Path) -> Result<Vec<ArenaDefinition>, String> {
    let mut paths = std::fs::read_dir(path)
        .map_err(|e| format!("{}: {e}", path.display()))?
        .map(|entry| entry.map(|e| e.path()).map_err(|e| e.to_string()))
        .collect::<Result<Vec<_>, _>>()?;
    paths.sort();
    paths
        .into_iter()
        .filter(|p| p.extension().is_some_and(|e| e == "ron"))
        .map(|p| ArenaDefinition::load(&p))
        .collect()
}

impl ArenaDefinition {
    pub fn load(path: &Path) -> Result<Self, String> {
        let text = std::fs::read_to_string(path).map_err(|e| format!("{}: {e}", path.display()))?;
        Self::parse(&text).map_err(|e| format!("{}: {e}", path.display()))
    }
    pub fn parse(text: &str) -> Result<Self, String> {
        let arena: Self = ron::from_str(text).map_err(|e| e.to_string())?;
        arena.validate()?;
        Ok(arena)
    }
    pub fn validate(&self) -> Result<(), String> {
        let fail = |message: &str| Err(format!("arena {}: {message}", self.name));
        if self.name.trim().is_empty()
            || !self.frame.iter().all(|v| v.is_finite())
            || self.frame[0] >= self.frame[2]
            || self.frame[1] >= self.frame[3]
        {
            return fail("name and finite camera bounds are required");
        }
        if self.spawns.is_empty()
            || self.spawns.len() > 4
            || self.spawns.iter().any(|p| !self.contains(*p))
        {
            return fail("one to four spawns must lie inside the camera frame");
        }
        let mut ids = BTreeSet::new();
        for object in &self.objects {
            if !ids.insert(object.id) {
                return fail("duplicate object id");
            }
            if !object.rotation.is_finite() || !object.mass.is_finite() || object.mass <= 0.0 {
                return fail("rotation must be finite and mass must be positive");
            }
            object.shape.validate()?;
            match object.kind {
                ArenaKind::Breakable { .. } if object.health.is_none_or(|h| h == 0) => {
                    return fail("breakable pieces require positive health");
                }
                ArenaKind::Saw {
                    teeth,
                    angular_velocity,
                    ..
                } if teeth < 3
                    || !angular_velocity.is_finite()
                    || !matches!(object.shape, ArenaShape::Circle { .. }) =>
                {
                    return fail("saws require a circle, three or more teeth, and finite speed");
                }
                _ => {}
            }
            let mut offsets = vec![[0.0, 0.0]];
            let rotates = if let Some(motion) = &object.motion {
                if !motion.period_seconds.is_finite()
                    || motion.period_seconds <= 0.0
                    || !motion.angular_velocity.is_finite()
                    || motion.path.iter().flatten().any(|v| !v.is_finite())
                    || matches!(
                        object.kind,
                        ArenaKind::Loose
                            | ArenaKind::Breakable { loose: true }
                            | ArenaKind::Saw { loose: true, .. }
                            | ArenaKind::Background
                    )
                {
                    return fail(
                        "motion needs finite offsets, positive period, and a supported kinematic piece",
                    );
                }
                offsets.extend_from_slice(&motion.path);
                motion.angular_velocity != 0.0
            } else {
                matches!(object.kind, ArenaKind::Saw { .. })
            };
            for offset in offsets {
                let center = [
                    object.position[0] + offset[0],
                    object.position[1] + offset[1],
                ];
                let points = if rotates {
                    let radius = object.shape.radius();
                    vec![
                        [center[0] - radius, center[1] - radius],
                        [center[0] + radius, center[1] + radius],
                    ]
                } else {
                    object.shape.world_vertices(center, object.rotation)
                };
                if points.iter().any(|p| !self.contains(*p)) {
                    return fail("geometry or motion lies outside camera frame");
                }
            }
        }
        let mut legacy_ids = BTreeSet::new();
        for body in &self.legacy_bodies {
            let shape = if body.shape == DynamicBodyShape::Weight {
                ArenaShape::Circle {
                    radius: body.radius,
                }
            } else {
                ArenaShape::Rectangle {
                    size: [body.width, body.height],
                }
            };
            shape.validate()?;
            if !legacy_ids.insert(body.id)
                || !body.rotation.is_finite()
                || !body.mass.is_finite()
                || body.mass <= 0.0
                || !body.friction.is_finite()
                || body.friction < 0.0
                || !body.restitution.is_finite()
                || !(0.0..=1.0).contains(&body.restitution)
                || shape
                    .world_vertices(body.position, body.rotation)
                    .iter()
                    .any(|p| !self.contains(*p))
            {
                return fail("invalid legacy body properties or bounds");
            }
        }
        let mut saw_ids = BTreeSet::new();
        for saw in &self.legacy_saws {
            let shape = ArenaShape::Circle { radius: saw.radius };
            shape.validate()?;
            if !saw_ids.insert(saw.id)
                || saw.teeth < 3
                || !saw.initial_angle.is_finite()
                || !saw.angular_velocity.is_finite()
                || shape
                    .world_vertices(saw.position, 0.0)
                    .iter()
                    .any(|p| !self.contains(*p))
            {
                return fail("invalid legacy saw properties or bounds");
            }
        }
        let mut surface_ids = BTreeSet::new();
        for surface in &self.surfaces {
            if !surface_ids.insert(surface.id)
                || surface.width_milli <= 0
                || surface.height_milli <= 0
            {
                return fail("legacy surfaces require unique ids and positive sizes");
            }
            let shape = if surface.outline_milli.is_empty() {
                ArenaShape::Rectangle {
                    size: [
                        surface.width_milli as f32 / 1000.0,
                        surface.height_milli as f32 / 1000.0,
                    ],
                }
            } else {
                if surface.outline_milli.len() < 3 {
                    return fail("surface contour needs three points");
                }
                ArenaShape::Polygon {
                    vertices: surface
                        .outline_milli
                        .iter()
                        .map(|p| [p[0] as f32 / 1000.0, p[1] as f32 / 1000.0])
                        .collect(),
                }
            };
            if shape
                .world_vertices(
                    [
                        surface.center_x_milli as f32 / 1000.0,
                        surface.center_y_milli as f32 / 1000.0,
                    ],
                    surface.rotation_milliradians as f32 / 1000.0,
                )
                .iter()
                .any(|p| !self.contains(*p))
            {
                return fail("legacy geometry lies outside camera frame");
            }
        }
        let mut chain_ids = BTreeSet::new();
        for chain in &self.chains {
            let b = self.objects.iter().find(|o| o.id == chain.body_b);
            let a = chain
                .body_a
                .and_then(|id| self.objects.iter().find(|o| o.id == id));
            if !chain_ids.insert(chain.id)
                || chain.anchor.iter().any(|v| !v.is_finite())
                || !chain.length.is_finite()
                || chain.length <= 0.0
                || b.is_none()
                || (chain.body_a.is_some() && a.is_none())
                || chain.body_a == Some(chain.body_b)
                || b.is_some_and(|o| matches!(o.kind, ArenaKind::Background))
                || a.is_some_and(|o| matches!(o.kind, ArenaKind::Background))
                || (chain.body_a.is_none() && !self.contains(chain.anchor))
            {
                return fail(
                    "chain requires distinct existing collidable pieces or an in-frame fixed anchor",
                );
            }
            let start = a.map_or(chain.anchor, |o| o.position);
            if Vector::from(start).distance(Vector::from(b.unwrap().position)) > chain.length + 0.01
            {
                return fail("chain starts beyond its length");
            }
        }
        Ok(())
    }
    fn contains(&self, p: [f32; 2]) -> bool {
        p.iter().all(|v| v.is_finite())
            && p[0] >= self.frame[0]
            && p[0] <= self.frame[2]
            && p[1] >= self.frame[1]
            && p[1] <= self.frame[3]
    }
}

impl ArenaShape {
    fn validate(&self) -> Result<(), String> {
        let positive = |v: f32| v.is_finite() && v > 0.0;
        let valid = match self {
            Self::Rectangle { size } => size.iter().all(|v| positive(*v)),
            Self::Circle { radius } => positive(*radius),
            Self::Polygon { vertices } => {
                if vertices.len() < 3 || vertices.iter().flatten().any(|v| !v.is_finite()) {
                    false
                } else {
                    let cross = (0..vertices.len())
                        .map(|i| {
                            let a = Vector::from(vertices[i]);
                            let b = Vector::from(vertices[(i + 1) % vertices.len()]);
                            let c = Vector::from(vertices[(i + 2) % vertices.len()]);
                            (b - a).perp_dot(c - b)
                        })
                        .collect::<Vec<_>>();
                    let winding = if cross.iter().all(|v| *v > 0.0001) {
                        1.0
                    } else if cross.iter().all(|v| *v < -0.0001) {
                        -1.0
                    } else {
                        0.0
                    };
                    winding != 0.0
                        && (0..vertices.len()).all(|i| {
                            let a = Vector::from(vertices[i]);
                            let b = Vector::from(vertices[(i + 1) % vertices.len()]);
                            vertices.iter().all(|p| {
                                winding * (b - a).perp_dot(Vector::from(*p) - a) >= -0.0001
                            })
                        })
                }
            }
        };
        if valid {
            Ok(())
        } else {
            Err("shape needs positive finite dimensions or a strictly convex polygon".into())
        }
    }
    pub(crate) fn collider(&self) -> ColliderBuilder {
        match self {
            Self::Rectangle { size } => ColliderBuilder::cuboid(size[0] * 0.5, size[1] * 0.5),
            Self::Circle { radius } => ColliderBuilder::ball(*radius),
            Self::Polygon { vertices } => ColliderBuilder::convex_hull(
                &vertices
                    .iter()
                    .map(|p| Vector::from(*p))
                    .collect::<Vec<_>>(),
            )
            .expect("validated convex polygon"),
        }
    }
    pub(crate) fn radius(&self) -> f32 {
        match self {
            Self::Circle { radius } => *radius,
            Self::Rectangle { size } => Vector::from(*size).length() * 0.5,
            Self::Polygon { vertices } => vertices
                .iter()
                .map(|p| Vector::from(*p).length())
                .fold(0.0, f32::max),
        }
    }
    fn world_vertices(&self, center: [f32; 2], angle: f32) -> Vec<[f32; 2]> {
        let local = match self {
            Self::Circle { radius } => {
                return vec![
                    [center[0] - radius, center[1] - radius],
                    [center[0] + radius, center[1] + radius],
                ];
            }
            Self::Rectangle { size } => vec![
                [-size[0] * 0.5, -size[1] * 0.5],
                [size[0] * 0.5, -size[1] * 0.5],
                [size[0] * 0.5, size[1] * 0.5],
                [-size[0] * 0.5, size[1] * 0.5],
            ],
            Self::Polygon { vertices } => vertices.clone(),
        };
        let (s, c) = angle.sin_cos();
        local
            .into_iter()
            .map(|p| {
                [
                    center[0] + c * p[0] - s * p[1],
                    center[1] + s * p[0] + c * p[1],
                ]
            })
            .collect()
    }
}
