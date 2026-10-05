use super::*;

pub(super) struct SharedScenePlugin;

impl Plugin for SharedScenePlugin {
    fn build(&self, app: &mut App) {
        app.world_mut()
            .resource_mut::<Assets<Font>>()
            .insert(
                ROUND_FONT_HANDLE.id(),
                Font::from_bytes(include_bytes!("fonts/Arimo.ttf").to_vec()),
            )
            .expect("shared scene owns its embedded round font");
        load_internal_asset!(
            app,
            RADIAL_ECHO_SHADER_HANDLE,
            "radial_echo.wgsl",
            Shader::from_wgsl
        );
        app.add_plugins((
            ExtractComponentPlugin::<RadialEchoSettings>::default(),
            UniformComponentPlugin::<RadialEchoSettings>::default(),
        ));
        let Some(render_app) = app.get_sub_app_mut(RenderApp) else {
            return;
        };
        render_app.add_systems(RenderStartup, init_radial_echo_pipeline);
        render_app.add_systems(
            Core2d,
            radial_echo_pass
                .after(Core2dSystems::EarlyPostProcess)
                .before(Core2dSystems::PostProcess),
        );
    }
}

#[derive(Component, Clone, Copy, Default, ExtractComponent, ShaderType)]
pub(super) struct RadialEchoSettings {
    pub(super) strength: f32,
    pub(super) spacing: f32,
    pub(super) red_offset: f32,
    pub(super) _padding: f32,
}

#[derive(Resource)]
struct RadialEchoPipeline {
    layout: BindGroupLayoutDescriptor,
    sampler: Sampler,
    pipeline: CachedRenderPipelineId,
}

#[derive(Default)]
struct RadialEchoBindGroupCache(Option<(TextureViewId, BindGroup)>);

fn init_radial_echo_pipeline(
    mut commands: Commands,
    render_device: Res<RenderDevice>,
    fullscreen_shader: Res<FullscreenShader>,
    pipeline_cache: Res<PipelineCache>,
) {
    let layout = BindGroupLayoutDescriptor::new(
        "rounds_radial_echo_bind_group_layout",
        &BindGroupLayoutEntries::sequential(
            ShaderStages::FRAGMENT,
            (
                texture_2d(TextureSampleType::Float { filterable: true }),
                sampler(SamplerBindingType::Filtering),
                uniform_buffer::<RadialEchoSettings>(true),
            ),
        ),
    );
    let sampler = render_device.create_sampler(&SamplerDescriptor::default());
    let pipeline = pipeline_cache.queue_render_pipeline(RenderPipelineDescriptor {
        label: Some("rounds_radial_echo_pipeline".into()),
        layout: vec![layout.clone()],
        vertex: fullscreen_shader.to_vertex_state(),
        fragment: Some(FragmentState {
            shader: RADIAL_ECHO_SHADER_HANDLE,
            targets: vec![Some(ColorTargetState {
                format: TextureFormat::Rgba16Float,
                blend: None,
                write_mask: ColorWrites::ALL,
            })],
            ..default()
        }),
        ..default()
    });
    commands.insert_resource(RadialEchoPipeline {
        layout,
        sampler,
        pipeline,
    });
}

fn radial_echo_pass(
    view: ViewQuery<(
        &ViewTarget,
        &RadialEchoSettings,
        &DynamicUniformIndex<RadialEchoSettings>,
    )>,
    pipeline: Option<Res<RadialEchoPipeline>>,
    pipeline_cache: Res<PipelineCache>,
    uniforms: Res<ComponentUniforms<RadialEchoSettings>>,
    mut cache: Local<RadialEchoBindGroupCache>,
    mut context: RenderContext,
) {
    let Some(pipeline) = pipeline else { return };
    let (target, _, settings_index) = view.into_inner();
    let Some(render_pipeline) = pipeline_cache.get_render_pipeline(pipeline.pipeline) else {
        return;
    };
    let Some(settings_binding) = uniforms.uniforms().binding() else {
        return;
    };
    let post_process = target.post_process_write();
    let bind_group = match &mut cache.0 {
        Some((source, bind_group)) if *source == post_process.source.id() => bind_group,
        cached => {
            let bind_group = context.render_device().create_bind_group(
                "rounds_radial_echo_bind_group",
                &pipeline_cache.get_bind_group_layout(&pipeline.layout),
                &BindGroupEntries::sequential((
                    post_process.source,
                    &pipeline.sampler,
                    settings_binding.clone(),
                )),
            );
            &cached.insert((post_process.source.id(), bind_group)).1
        }
    };
    let mut pass = context
        .command_encoder()
        .begin_render_pass(&RenderPassDescriptor {
            label: Some("rounds_radial_echo_final_composite_pass"),
            color_attachments: &[Some(RenderPassColorAttachment {
                view: post_process.destination,
                depth_slice: None,
                resolve_target: None,
                ops: Operations::default(),
            })],
            depth_stencil_attachment: None,
            timestamp_writes: None,
            occlusion_query_set: None,
            multiview_mask: None,
        });
    pass.set_pipeline(render_pipeline);
    pass.set_bind_group(0, bind_group, &[settings_index.index()]);
    pass.draw(0..3, 0..1);
}
