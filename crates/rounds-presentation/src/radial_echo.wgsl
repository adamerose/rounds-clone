@group(0) @binding(0) var screen_texture: texture_2d<f32>;
@group(0) @binding(1) var screen_sampler: sampler;

struct RadialEchoSettings {
    strength: f32,
    spacing: f32,
    red_offset: f32,
    padding: f32,
};

@group(0) @binding(2) var<uniform> settings: RadialEchoSettings;

fn radial_uv(uv: vec2<f32>, distance: f32) -> vec2<f32> {
    let centered = uv - vec2<f32>(0.5, 0.5);
    return vec2<f32>(0.5, 0.5) + centered / (1.0 + distance);
}

@fragment
fn fragment(@builtin(position) position: vec4<f32>) -> @location(0) vec4<f32> {
    let dimensions = vec2<f32>(textureDimensions(screen_texture));
    let uv = position.xy / dimensions;
    let base = textureSample(screen_texture, screen_sampler, uv);
    let strength = clamp(settings.strength, 0.0, 1.0);
    if strength <= 0.0001 {
        return base;
    }

    let green_near = textureSample(screen_texture, screen_sampler, radial_uv(uv, settings.spacing));
    let yellow_near = textureSample(screen_texture, screen_sampler, radial_uv(uv, settings.spacing * 2.15));
    let red_mid = textureSample(screen_texture, screen_sampler, radial_uv(uv, settings.spacing * 3.45 + settings.red_offset));
    let red_far = textureSample(screen_texture, screen_sampler, radial_uv(uv, settings.spacing * 5.3 + settings.red_offset));
    let green_far = textureSample(screen_texture, screen_sampler, radial_uv(uv, settings.spacing * 7.1));

    // Keep the source scene readable, then lay down a small number of separated,
    // channel-isolated copies.  Averaging many weak full-colour taps here turns
    // hard platform and HUD edges into a continuous zoom blur.
    var echoed = base.rgb * (1.0 - strength * 0.22);
    echoed.g = max(echoed.g, max(green_near.g - 0.13, 0.0) * 0.94 * strength);
    echoed.r = max(echoed.r, max(yellow_near.r - 0.14, 0.0) * 0.62 * strength);
    echoed.g = max(echoed.g, max(yellow_near.g - 0.14, 0.0) * 0.52 * strength);
    echoed.r = max(echoed.r, max(red_mid.r - 0.13, 0.0) * 0.84 * strength);
    echoed.g = max(echoed.g, max(green_far.g - 0.16, 0.0) * 0.58 * strength);
    echoed.r = max(echoed.r, max(red_far.r - 0.15, 0.0) * 0.56 * strength);
    return vec4<f32>(echoed, base.a);
}
