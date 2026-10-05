use super::*;

#[derive(Clone, Copy)]
struct HangingBodyLayout {
    id: u16,
    x: i32,
    body_y: i32,
    body_height: i32,
    square_y: i32,
    body_top_y: i32,
}

const HANGING_LAYOUT: [HangingBodyLayout; 21] = [
    HangingBodyLayout {
        id: 400,
        x: -405,
        body_y: 23,
        body_height: 36,
        square_y: 152,
        body_top_y: 41,
    },
    HangingBodyLayout {
        id: 401,
        x: -315,
        body_y: 21,
        body_height: 108,
        square_y: 152,
        body_top_y: 75,
    },
    HangingBodyLayout {
        id: 402,
        x: -225,
        body_y: 21,
        body_height: 108,
        square_y: 152,
        body_top_y: 75,
    },
    HangingBodyLayout {
        id: 403,
        x: -135,
        body_y: 21,
        body_height: 108,
        square_y: 152,
        body_top_y: 75,
    },
    HangingBodyLayout {
        id: 404,
        x: -45,
        body_y: 21,
        body_height: 108,
        square_y: 152,
        body_top_y: 75,
    },
    HangingBodyLayout {
        id: 405,
        x: 45,
        body_y: 21,
        body_height: 108,
        square_y: 152,
        body_top_y: 75,
    },
    HangingBodyLayout {
        id: 406,
        x: 135,
        body_y: 21,
        body_height: 108,
        square_y: 152,
        body_top_y: 75,
    },
    HangingBodyLayout {
        id: 407,
        x: 225,
        body_y: 21,
        body_height: 108,
        square_y: 152,
        body_top_y: 75,
    },
    HangingBodyLayout {
        id: 408,
        x: 315,
        body_y: 21,
        body_height: 108,
        square_y: 152,
        body_top_y: 75,
    },
    HangingBodyLayout {
        id: 409,
        x: 405,
        body_y: 23,
        body_height: 36,
        square_y: 152,
        body_top_y: 41,
    },
    HangingBodyLayout {
        id: 410,
        x: -450,
        body_y: -235,
        body_height: 108,
        square_y: -104,
        body_top_y: -181,
    },
    HangingBodyLayout {
        id: 411,
        x: -360,
        body_y: -235,
        body_height: 108,
        square_y: -104,
        body_top_y: -181,
    },
    HangingBodyLayout {
        id: 412,
        x: -270,
        body_y: -235,
        body_height: 108,
        square_y: -104,
        body_top_y: -181,
    },
    HangingBodyLayout {
        id: 413,
        x: -180,
        body_y: -235,
        body_height: 108,
        square_y: -104,
        body_top_y: -181,
    },
    HangingBodyLayout {
        id: 414,
        x: -90,
        body_y: -235,
        body_height: 108,
        square_y: -104,
        body_top_y: -181,
    },
    HangingBodyLayout {
        id: 415,
        x: 0,
        body_y: -235,
        body_height: 108,
        square_y: -104,
        body_top_y: -181,
    },
    HangingBodyLayout {
        id: 416,
        x: 90,
        body_y: -235,
        body_height: 108,
        square_y: -104,
        body_top_y: -181,
    },
    HangingBodyLayout {
        id: 417,
        x: 180,
        body_y: -235,
        body_height: 108,
        square_y: -104,
        body_top_y: -181,
    },
    HangingBodyLayout {
        id: 418,
        x: 270,
        body_y: -235,
        body_height: 108,
        square_y: -104,
        body_top_y: -181,
    },
    HangingBodyLayout {
        id: 419,
        x: 360,
        body_y: -235,
        body_height: 108,
        square_y: -104,
        body_top_y: -181,
    },
    HangingBodyLayout {
        id: 420,
        x: 450,
        body_y: -235,
        body_height: 108,
        square_y: -104,
        body_top_y: -181,
    },
];

const LEFT_ENTRY_KNOTS: [(u8, f32); 7] = [
    (0, 1_102.0),
    (4, 755.5),
    (8, 440.5),
    (12, 224.5),
    (16, 90.5),
    (20, 21.5),
    (24, 0.0),
];
const RIGHT_ENTRY_KNOTS: [(u8, f32); 7] = [
    (0, 1_102.0),
    (4, 755.5),
    (8, 390.0),
    (12, 191.5),
    (16, 72.0),
    (20, 13.5),
    (24, 0.0),
];
const CENTRAL_ENTRY_KNOTS: [(u8, f32); 7] = [
    (0, 1_102.0),
    (4, 755.5),
    (8, 495.5),
    (12, 260.5),
    (16, 111.5),
    (20, 30.5),
    (24, 0.0),
];

fn monotone_entry_offset(age: u8, knots: &[(u8, f32)]) -> f32 {
    if age >= knots.last().expect("entry curve has knots").0 {
        return 0.0;
    }
    let pair = knots
        .windows(2)
        .find(|pair| age >= pair[0].0 && age <= pair[1].0)
        .expect("entry age lies within curve");
    let t = f32::from(age - pair[0].0) / f32::from(pair[1].0 - pair[0].0);
    let smooth = t * t * (3.0 - 2.0 * t);
    pair[0].1 + (pair[1].1 - pair[0].1) * smooth
}

pub fn hanging_entry_presentation(age: u8) -> HangingEntryPresentation {
    let age = age.min(47);
    let bodies = HANGING_LAYOUT
        .iter()
        .map(|body| {
            let square_offset = if body.x < 0 {
                monotone_entry_offset(age, &LEFT_ENTRY_KNOTS)
            } else if body.x > 0 {
                monotone_entry_offset(age, &RIGHT_ENTRY_KNOTS)
            } else {
                monotone_entry_offset(age, &CENTRAL_ENTRY_KNOTS)
            };
            let body_offset = if body.id == 415 {
                monotone_entry_offset(age.saturating_add(4), &CENTRAL_ENTRY_KNOTS)
            } else {
                square_offset
            };
            HangingBodyPresentation {
                id: body.id,
                nominal_x_milli: body.x * 1_000,
                body_x_milli: ((body.x as f32 + body_offset) * 1_000.0).round() as i32,
                body_y_milli: body.body_y * 1_000,
                body_width_milli: 36_000,
                body_height_milli: body.body_height * 1_000,
                square_x_milli: ((body.x as f32 + square_offset) * 1_000.0).round() as i32,
                square_y_milli: body.square_y * 1_000,
                square_size_milli: 23_000,
                square_opening_milli: 17_000,
                ceiling_y_milli: 400_000,
                body_top_y_milli: body.body_top_y * 1_000,
            }
        })
        .collect();
    HangingEntryPresentation {
        age_ticks: age,
        body_rgb: [157, 92, 72],
        square_rim_rgb: [102, 61, 62],
        square_opening_rgb: [3, 8, 30],
        link_rgb: [67, 61, 61],
        bodies,
    }
}

fn loaded_arena(file_name: &str) -> Vec<ArenaSurfaceSnapshot> {
    ArenaDefinition::load(&default_arena_directory().join(file_name))
        .expect("bundled arena data must parse")
        .surfaces
}

macro_rules! data_arena {
    ($function:ident, $file:literal) => {
        pub fn $function() -> &'static [ArenaSurfaceSnapshot] {
            static ARENA: std::sync::OnceLock<Vec<ArenaSurfaceSnapshot>> =
                std::sync::OnceLock::new();
            ARENA.get_or_init(|| loaded_arena($file))
        }
    };
}

data_arena!(teal_arena, "teal.ron");
data_arena!(timber_arena, "timber.ron");
data_arena!(draft_arena, "draft.ron");
data_arena!(prior_match_arena, "prior-match.ron");
data_arena!(radial_saw_arena, "radial-saw.ron");
data_arena!(yellow_crate_arena, "yellow-crate.ron");
data_arena!(lime_modular_arena, "lime.ron");
data_arena!(ice_arena, "ice.ron");

pub fn arena_for_profile(profile: ReplayProfile) -> &'static [ArenaSurfaceSnapshot] {
    match profile {
        ReplayProfile::TealDuelReplay => teal_arena(),
        ReplayProfile::RematchDraftReplay => draft_arena(),
        ReplayProfile::MatchEndWaitingReplay => teal_arena(),
        ReplayProfile::LimeModularArenaReplay => lime_modular_arena(),
        ReplayProfile::RadialSawHalfBlueReplay => radial_saw_arena(),
        ReplayProfile::YellowCrateTerminalBlastReplay => yellow_crate_arena(),
        ReplayProfile::TimberCollapseReplay => timber_arena(),
    }
}

pub fn profile_arena_filename(profile: ReplayProfile) -> &'static str {
    match profile {
        ReplayProfile::TealDuelReplay | ReplayProfile::MatchEndWaitingReplay => "teal.ron",
        ReplayProfile::RematchDraftReplay => "draft.ron",
        ReplayProfile::LimeModularArenaReplay => "lime.ron",
        ReplayProfile::RadialSawHalfBlueReplay => "radial-saw.ron",
        ReplayProfile::YellowCrateTerminalBlastReplay => "yellow-crate.ron",
        ReplayProfile::TimberCollapseReplay => "timber.ron",
    }
}

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
