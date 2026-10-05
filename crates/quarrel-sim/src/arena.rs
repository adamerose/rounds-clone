use super::*;
pub fn teal_arena() -> &'static [ArenaSurfaceSnapshot] {
    const CYAN: [u8; 3] = [44, 232, 207];
    const LIME: [u8; 3] = [104, 239, 133];
    const DARK: [u8; 3] = [91, 99, 76];
    static ARENA: [ArenaSurfaceSnapshot; 19] = [
        surface(0, -520, -170, 92, 24, CYAN),
        surface(1, -390, -58, 82, 24, LIME),
        surface(2, -260, 36, 76, 24, CYAN),
        surface(3, -130, -58, 72, 24, LIME),
        surface(4, -40, 36, 72, 24, CYAN),
        surface(5, 40, 36, 72, 24, LIME),
        surface(6, 130, -58, 72, 24, CYAN),
        surface(7, 260, 36, 76, 24, LIME),
        surface(8, 390, -58, 82, 24, CYAN),
        surface(9, 520, -170, 92, 24, LIME),
        surface(10, -520, -286, 58, 32, DARK),
        surface(11, -390, -230, 58, 32, DARK),
        surface(12, -260, -190, 58, 32, DARK),
        surface(13, -130, -230, 58, 32, DARK),
        surface(14, 0, -190, 58, 32, DARK),
        surface(15, 130, -230, 58, 32, DARK),
        surface(16, 260, -190, 58, 32, DARK),
        surface(17, 390, -230, 58, 32, DARK),
        surface(18, 520, -286, 58, 32, DARK),
    ];
    &ARENA
}

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

pub fn timber_arena() -> &'static [ArenaSurfaceSnapshot] {
    const FLOOR: [u8; 3] = [246, 0, 79];
    static ARENA: [ArenaSurfaceSnapshot; 1] = [surface(0, 0, -300, 1_600, 60, FLOOR)];
    &ARENA
}

pub fn draft_arena() -> &'static [ArenaSurfaceSnapshot] {
    const YELLOW: [u8; 3] = [252, 224, 0];
    static ARENA: [ArenaSurfaceSnapshot; 9] = [
        surface(0, -520, -210, 170, 44, YELLOW),
        surface(1, -260, -40, 140, 44, YELLOW),
        surface(2, 0, -210, 145, 44, YELLOW),
        surface(3, 260, -40, 140, 44, YELLOW),
        surface(4, 520, -210, 170, 44, YELLOW),
        surface(5, -390, 140, 130, 44, YELLOW),
        surface(6, 0, 190, 150, 44, YELLOW),
        surface(7, 390, 140, 130, 44, YELLOW),
        surface(8, 0, -20, 100, 40, YELLOW),
    ];
    &ARENA
}

pub fn prior_match_arena() -> &'static [ArenaSurfaceSnapshot] {
    const PINK: [u8; 3] = [248, 0, 78];
    static ARENA: [ArenaSurfaceSnapshot; 9] = [
        surface(0, -520, -260, 210, 48, PINK),
        surface(1, -310, -100, 130, 38, PINK),
        surface(2, -90, 55, 120, 38, PINK),
        surface(3, 170, -80, 120, 38, PINK),
        surface(4, 440, 90, 150, 42, PINK),
        surface(5, -420, 180, 120, 38, PINK),
        surface(6, -160, 255, 145, 40, PINK),
        surface(7, 110, 190, 130, 40, PINK),
        surface(8, 520, -230, 180, 45, PINK),
    ];
    &ARENA
}

pub fn radial_saw_arena() -> &'static [ArenaSurfaceSnapshot] {
    const WHITE: [u8; 3] = [244, 248, 239];
    const CYAN: [u8; 3] = [68, 221, 238];
    const DARK: [u8; 3] = [3, 23, 51];
    static ARENA: [ArenaSurfaceSnapshot; 9] = [
        rotated_surface(0, -220, 72, 286, 52, 785, WHITE),
        rotated_surface(1, 220, 72, 220, 52, -785, WHITE),
        rotated_surface(2, -190, -154, 214, 52, -785, CYAN),
        rotated_surface(3, 190, -154, 214, 52, 785, CYAN),
        rotated_surface(4, 0, 176, 72, 72, 785, WHITE),
        rotated_surface(5, 0, -178, 58, 58, 785, CYAN),
        rotated_surface(6, -273, 173, 494, 76, 776, DARK),
        rotated_surface(7, -276, -176, 650, 76, -794, DARK),
        rotated_surface(8, 276, -205, 650, 76, 794, DARK),
    ];
    &ARENA
}

pub fn yellow_crate_arena() -> &'static [ArenaSurfaceSnapshot] {
    const YELLOW: [u8; 3] = [255, 231, 0];
    static ARENA: [ArenaSurfaceSnapshot; 13] = [
        surface(0, -480, 160, 118, 44, YELLOW),
        surface(1, -160, 160, 118, 44, YELLOW),
        surface(2, 160, 160, 118, 44, YELLOW),
        surface(3, 440, 160, 118, 44, YELLOW),
        surface(4, -610, 20, 112, 44, YELLOW),
        surface(5, -320, 20, 112, 44, YELLOW),
        surface(6, 0, 20, 112, 44, YELLOW),
        surface(7, 320, 20, 112, 44, YELLOW),
        surface(8, 610, 20, 112, 44, YELLOW),
        surface(9, -480, -140, 118, 44, YELLOW),
        surface(10, -160, -140, 118, 44, YELLOW),
        surface(11, 160, -140, 118, 44, YELLOW),
        surface(12, 480, -140, 118, 44, YELLOW),
    ];
    &ARENA
}

/// Static source-measured geometry from the first complete lime arena view.
/// Pixel coordinates use the native 1280x720 frame; one pixel is one world unit.
pub fn lime_modular_arena() -> &'static [ArenaSurfaceSnapshot] {
    static ARENA: std::sync::OnceLock<Vec<ArenaSurfaceSnapshot>> = std::sync::OnceLock::new();
    ARENA.get_or_init(|| {
        const LIME: [u8; 3] = [45, 232, 199];
        const OLIVE: [u8; 3] = [76, 81, 67];
        let mut arena = Vec::with_capacity(33);
        for (id, x) in [42, 312, 582, 852, 1122].into_iter().enumerate() {
            arena.push(outlined_surface_from_pixels(
                id as u8,
                &[
                    [x, 365],
                    [x + 116, 365],
                    [x + 116, 393],
                    [x + 90, 419],
                    [x + 76, 419],
                    [x + 76, 522],
                    [x + 40, 522],
                    [x + 40, 419],
                    [x + 26, 419],
                    [x, 393],
                ],
                LIME,
            ));
        }
        for (id, x) in [217, 495, 747, 1027].into_iter().enumerate() {
            arena.push(outlined_surface_from_pixels(
                5 + id as u8,
                &[[x, 441], [x + 36, 441], [x + 36, 579], [x, 579]],
                LIME,
            ));
        }
        for (id, x) in [45, 315, 585, 855, 1125].into_iter().enumerate() {
            arena.push(outlined_surface_from_pixels(
                9 + id as u8,
                &[
                    [x + 36, 611],
                    [x + 73, 611],
                    [x + 73, 647],
                    [x + 109, 647],
                    [x + 109, 686],
                    [x + 73, 686],
                    [x + 73, 720],
                    [x + 36, 720],
                    [x + 36, 686],
                    [x, 686],
                    [x, 647],
                    [x + 36, 647],
                ],
                LIME,
            ));
        }
        for (id, x) in [217, 495, 747, 1027].into_iter().enumerate() {
            arena.push(outlined_surface_from_pixels(
                14 + id as u8,
                &[[x, 693], [x + 36, 693], [x + 36, 720], [x, 720]],
                LIME,
            ));
        }
        for (column, center_x) in [100, 370, 640, 910, 1180].into_iter().enumerate() {
            let first = 18 + column as u8 * 3;
            arena.extend([
                surface(first, center_x - 640, 49, 37, 37, OLIVE),
                surface(first + 1, center_x - 661, 14, 38, 37, OLIVE),
                surface(first + 2, center_x - 619, 14, 38, 37, OLIVE),
            ]);
        }
        arena
    })
}

pub fn ice_arena() -> &'static [ArenaSurfaceSnapshot] {
    static ARENA: std::sync::OnceLock<Vec<ArenaSurfaceSnapshot>> = std::sync::OnceLock::new();
    ARENA.get_or_init(|| {
        let contours: &[&[[i32; 2]]] = &[
            &[
                [452, 127],
                [503, 127],
                [503, 149],
                [499, 156],
                [499, 205],
                [503, 212],
                [503, 229],
                [452, 229],
                [452, 212],
                [456, 205],
                [456, 156],
                [452, 149],
            ],
            &[
                [777, 127],
                [828, 127],
                [828, 149],
                [824, 156],
                [824, 205],
                [828, 212],
                [828, 229],
                [777, 229],
                [777, 212],
                [781, 205],
                [781, 156],
                [777, 149],
            ],
            &[
                [596, 183],
                [683, 183],
                [683, 216],
                [670, 231],
                [608, 231],
                [596, 216],
            ],
            &[
                [316, 272],
                [402, 272],
                [402, 306],
                [388, 321],
                [330, 321],
                [316, 306],
            ],
            &[
                [878, 272],
                [964, 272],
                [964, 306],
                [950, 321],
                [892, 321],
                [878, 306],
            ],
            &[
                [570, 294],
                [710, 294],
                [710, 338],
                [699, 355],
                [581, 355],
                [570, 338],
            ],
            &[
                [164, 373],
                [251, 373],
                [251, 406],
                [237, 422],
                [178, 422],
                [164, 406],
            ],
            &[
                [1030, 373],
                [1117, 373],
                [1117, 406],
                [1103, 422],
                [1044, 422],
                [1030, 406],
            ],
            &[
                [452, 371],
                [503, 371],
                [503, 393],
                [499, 399],
                [499, 507],
                [503, 514],
                [503, 533],
                [452, 533],
                [452, 514],
                [456, 507],
                [456, 399],
                [452, 393],
            ],
            &[
                [777, 371],
                [828, 371],
                [828, 393],
                [824, 399],
                [824, 507],
                [828, 514],
                [828, 533],
                [777, 533],
                [777, 514],
                [781, 507],
                [781, 399],
                [777, 393],
            ],
            &[
                [68, 474],
                [130, 474],
                [130, 590],
                [149, 611],
                [149, 800],
                [45, 800],
                [45, 612],
                [68, 590],
            ],
            &[
                [1149, 474],
                [1213, 474],
                [1213, 590],
                [1232, 611],
                [1232, 800],
                [1130, 800],
                [1130, 612],
                [1149, 590],
            ],
            &[
                [284, 474],
                [347, 474],
                [347, 590],
                [365, 611],
                [365, 800],
                [264, 800],
                [264, 612],
                [284, 590],
            ],
            &[
                [933, 474],
                [996, 474],
                [996, 590],
                [1015, 611],
                [1015, 800],
                [914, 800],
                [914, 612],
                [933, 590],
            ],
            &[[181, 504], [233, 504], [233, 552], [207, 578], [181, 552]],
            &[
                [1047, 504],
                [1099, 504],
                [1099, 552],
                [1073, 578],
                [1047, 552],
            ],
            &[
                [425, 611],
                [531, 611],
                [570, 572],
                [570, 480],
                [581, 471],
                [596, 471],
                [596, 449],
                [609, 437],
                [670, 437],
                [683, 449],
                [683, 471],
                [699, 471],
                [710, 480],
                [710, 572],
                [750, 611],
                [855, 611],
                [855, 800],
                [425, 800],
            ],
        ];
        contours
            .iter()
            .enumerate()
            .map(|(id, contour)| {
                let min_x = contour.iter().map(|p| p[0]).min().unwrap();
                let max_x = contour.iter().map(|p| p[0]).max().unwrap();
                let min_y = contour.iter().map(|p| p[1]).min().unwrap();
                let max_y = contour.iter().map(|p| p[1]).max().unwrap();
                let cx = (min_x + max_x) / 2;
                let cy = (min_y + max_y) / 2;
                let mut result = surface(
                    40 + id as u8,
                    cx - 640,
                    360 - cy,
                    max_x - min_x,
                    max_y - min_y,
                    [223, 235, 229],
                );
                result.outline_milli = contour
                    .iter()
                    .rev()
                    .map(|p| [(p[0] - cx) * 1_000, (cy - p[1]) * 1_000])
                    .collect();
                result
            })
            .collect()
    })
}

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

fn outlined_surface_from_pixels(
    id: u8,
    contour: &[[i32; 2]],
    color: [u8; 3],
) -> ArenaSurfaceSnapshot {
    let min_x = contour.iter().map(|point| point[0]).min().unwrap();
    let max_x = contour.iter().map(|point| point[0]).max().unwrap();
    let min_y = contour.iter().map(|point| point[1]).min().unwrap();
    let max_y = contour.iter().map(|point| point[1]).max().unwrap();
    let center_x = (min_x + max_x) / 2;
    let center_y = (min_y + max_y) / 2;
    let mut result = surface(
        id,
        center_x - 640,
        360 - center_y,
        max_x - min_x,
        max_y - min_y,
        color,
    );
    result.outline_milli = contour
        .iter()
        .rev()
        .map(|point| [(point[0] - center_x) * 1_000, (center_y - point[1]) * 1_000])
        .collect();
    result
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

const fn surface(
    id: u8,
    x: i32,
    y: i32,
    width: i32,
    height: i32,
    color: [u8; 3],
) -> ArenaSurfaceSnapshot {
    ArenaSurfaceSnapshot {
        outline_milli: Vec::new(),
        id,
        center_x_milli: x * 1_000,
        center_y_milli: y * 1_000,
        width_milli: width * 1_000,
        height_milli: height * 1_000,
        rotation_milliradians: 0,
        face_rgb: color,
    }
}

const fn rotated_surface(
    id: u8,
    x: i32,
    y: i32,
    width: i32,
    height: i32,
    rotation_milliradians: i32,
    color: [u8; 3],
) -> ArenaSurfaceSnapshot {
    ArenaSurfaceSnapshot {
        outline_milli: Vec::new(),
        id,
        center_x_milli: x * 1_000,
        center_y_milli: y * 1_000,
        width_milli: width * 1_000,
        height_milli: height * 1_000,
        rotation_milliradians,
        face_rgb: color,
    }
}
