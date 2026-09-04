use rounds_sim::MatchSnapshot;
use sha2::{Digest, Sha256};

pub const FRAME_WIDTH: usize = 320;
pub const FRAME_HEIGHT: usize = 180;

pub fn render_png(snapshot: &MatchSnapshot) -> Vec<u8> {
    let mut pixels = vec![[7_u8, 31, 42]; FRAME_WIDTH * FRAME_HEIGHT];
    fill_rect(&mut pixels, 0, 158, FRAME_WIDTH, 22, [35, 62, 68]);
    fill_rect(&mut pixels, 72, 118, 176, 8, [71, 106, 104]);
    for player in &snapshot.players {
        let x = world_x_to_pixel(player.x_milli);
        let y = world_y_to_pixel(player.y_milli);
        let color = if player.id == 0 {
            [246, 80, 123]
        } else {
            [69, 192, 255]
        };
        fill_circle(&mut pixels, x, y, 9, color);
        if player.block_ticks > 0 {
            draw_ring(&mut pixels, x, y, 13, [235, 245, 240]);
        }
    }
    for projectile in &snapshot.projectiles {
        fill_circle(
            &mut pixels,
            world_x_to_pixel(projectile.x_milli),
            world_y_to_pixel(projectile.y_milli),
            2,
            [255, 232, 115],
        );
    }
    let raw_pixels = pixels.into_iter().flatten().collect::<Vec<_>>();
    let mut output = Vec::new();
    {
        let mut encoder = png::Encoder::new(&mut output, FRAME_WIDTH as u32, FRAME_HEIGHT as u32);
        encoder.set_color(png::ColorType::Rgb);
        encoder.set_depth(png::BitDepth::Eight);
        let mut writer = encoder.write_header().expect("PNG header");
        writer.write_image_data(&raw_pixels).expect("PNG pixels");
    }
    output
}

pub fn frame_sha256(frame: &[u8]) -> String {
    format!("{:x}", Sha256::digest(frame))
}

fn world_x_to_pixel(x_milli: i32) -> i32 {
    160 + x_milli * 140 / 16_000
}

fn world_y_to_pixel(y_milli: i32) -> i32 {
    158 - y_milli * 120 / 18_000
}

fn fill_rect(
    pixels: &mut [[u8; 3]],
    x: usize,
    y: usize,
    width: usize,
    height: usize,
    color: [u8; 3],
) {
    for py in y..(y + height).min(FRAME_HEIGHT) {
        for px in x..(x + width).min(FRAME_WIDTH) {
            pixels[py * FRAME_WIDTH + px] = color;
        }
    }
}

fn fill_circle(pixels: &mut [[u8; 3]], center_x: i32, center_y: i32, radius: i32, color: [u8; 3]) {
    for y in -radius..=radius {
        for x in -radius..=radius {
            if x * x + y * y <= radius * radius {
                set_pixel(pixels, center_x + x, center_y + y, color);
            }
        }
    }
}

fn draw_ring(pixels: &mut [[u8; 3]], center_x: i32, center_y: i32, radius: i32, color: [u8; 3]) {
    for y in -radius..=radius {
        for x in -radius..=radius {
            let distance = x * x + y * y;
            if distance >= (radius - 1) * (radius - 1) && distance <= radius * radius {
                set_pixel(pixels, center_x + x, center_y + y, color);
            }
        }
    }
}

fn set_pixel(pixels: &mut [[u8; 3]], x: i32, y: i32, color: [u8; 3]) {
    if x >= 0 && x < FRAME_WIDTH as i32 && y >= 0 && y < FRAME_HEIGHT as i32 {
        pixels[y as usize * FRAME_WIDTH + x as usize] = color;
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use rounds_sim::run_scripted_match;

    #[test]
    fn rendered_frame_is_deterministic_and_complete() {
        let (snapshot, _) = run_scripted_match(38, 180);
        let first = render_png(&snapshot);
        let second = render_png(&snapshot);
        assert_eq!(first, second);
        assert!(first.starts_with(b"\x89PNG\r\n\x1a\n"));
        assert_eq!(frame_sha256(&first).len(), 64);
    }
}
