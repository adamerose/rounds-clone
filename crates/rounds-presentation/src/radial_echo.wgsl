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
    let yellow_mid = textureSample(screen_texture, screen_sampler, radial_uv(uv, settings.spacing * 2.0));
    let red_mid = textureSample(screen_texture, screen_sampler, radial_uv(uv, settings.spacing * 3.1 + settings.red_offset));
    let green_far = textureSample(screen_texture, screen_sampler, radial_uv(uv, settings.spacing * 4.4));
    let red_far = textureSample(screen_texture, screen_sampler, radial_uv(uv, settings.spacing * 6.0 + settings.red_offset));

    var echoed = base.rgb * (1.0 - strength * 0.55);
    echoed += green_near.rgb * vec3<f32>(0.01, 0.20, 0.01) * strength;
    echoed += yellow_mid.rgb * vec3<f32>(0.14, 0.11, 0.005) * strength;
    echoed += red_mid.rgb * vec3<f32>(0.14, 0.01, 0.005) * strength;
    echoed += green_far.rgb * vec3<f32>(0.005, 0.07, 0.01) * strength;
    echoed += red_far.rgb * vec3<f32>(0.06, 0.003, 0.003) * strength;
    return vec4<f32>(echoed, base.a);
}
