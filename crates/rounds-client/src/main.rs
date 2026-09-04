use rounds_network::{NETWORK_PROTOCOL, ServerReport, send_inputs};
use rounds_presentation::{
    FRAME_HEIGHT, FRAME_WIDTH, RENDERER_IDENTITY, YellowFrameSignature, frame_sha256, render_png,
    run_interactive_visible, run_visible, yellow_frame_signature,
};
use rounds_sim::{
    MatchSnapshot, RADIAL_HALF_BLUE_TICK, RADIAL_LAST_COMBAT_TICK, RADIAL_RESULT_ONSET_TICK,
    REPLAY_TICKS, ReplayProfile, YELLOW_FOLLOWING_RESULT_TICK, YELLOW_IMPACT_TICK,
    YELLOW_LAST_CALM_TICK, YELLOW_LAST_COMBAT_TICK, YELLOW_LOCAL_BURST_TICK, YELLOW_PEAK_ECHO_TICK,
    YELLOW_REPLAY_TICKS, YELLOW_RESULT_ONSET_TICK, YELLOW_ROUND_ORANGE_TICK, YELLOW_TRAILS_TICK,
    arena_digest, combat_digest, dynamic_body_digest, flow_digest, loadout_digest, round_digest,
    run_profile_match, run_profile_snapshots, saw_digest, scripted_inputs_for,
};
use serde::Serialize;
use sha2::{Digest, Sha256};
use std::env;
use std::fs;
use std::path::{Path, PathBuf};

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct CaptureEvidence {
    format: u16,
    package: String,
    executable_sha256: String,
    seed: u64,
    tick: u32,
    anchor: String,
    source_interval: &'static str,
    source_timestamp: String,
    source_pts: Option<i64>,
    source_rgba_sha256: Option<&'static str>,
    source_sha256: &'static str,
    input_trace: &'static str,
    input_trace_sha256: String,
    state_sha256: String,
    dynamic_body_sha256: String,
    arena_sha256: String,
    saw_sha256: String,
    combat_sha256: String,
    round_sha256: Option<String>,
    flow_sha256: Option<String>,
    loadout_sha256: Option<String>,
    renderer: &'static str,
    frame_sha256: String,
    frame_path: String,
    width: u32,
    height: u32,
    live_client_id: Option<u8>,
    composited_echo_pass: Option<&'static str>,
    impact_audio_hook_tick: Option<u32>,
    visual_signature: Option<YellowFrameSignature>,
}

fn main() {
    if let Err(error) = run() {
        eprintln!("{error}");
        std::process::exit(1);
    }
}

fn run() -> Result<(), String> {
    let arguments = env::args().collect::<Vec<_>>();
    let mode = arguments.get(1).map(String::as_str).unwrap_or("local");
    let ticks = argument(&arguments, "--ticks", REPLAY_TICKS)?;
    let seed = argument(&arguments, "--seed", 38_u64)?;
    let profile = argument(&arguments, "--profile", ReplayProfile::default())?;
    match mode {
        "capture" => capture(&arguments, profile, seed, ticks),
        "capture-replay" => capture_replay(&arguments, profile, seed, ticks),
        "local" => print_json(&local_report(profile, seed, ticks)),
        "visible" => {
            let frames = argument(&arguments, "--frames", 180_u32)?;
            if frames < 5 {
                return Err("--frames must be at least 5".to_owned());
            }
            let replay = run_profile_snapshots(profile, seed, ticks);
            if replay.len() < 2 {
                return Err("visible replay needs at least 2 ticks".to_owned());
            }
            let count = usize::min(frames as usize, replay.len());
            let sampled = (0..count)
                .map(|index| {
                    let source = index * (replay.len() - 1) / (count - 1);
                    replay[source].clone()
                })
                .collect();
            let report = local_report(profile, seed, ticks);
            run_visible(sampled)?;
            print_json(&report)
        }
        "visible-flow" => {
            let automated = arguments.iter().any(|argument| argument == "--automated");
            run_interactive_visible(profile, seed, ticks, automated)?;
            print_json(&local_report(profile, seed, ticks))
        }
        "remote" => remote(&arguments, profile, seed, ticks),
        _ => Err(
            "usage: rounds-client [local|remote|capture|capture-replay|visible|visible-flow] [options]"
                .to_owned(),
        ),
    }
}

fn remote(
    arguments: &[String],
    profile: ReplayProfile,
    seed: u64,
    ticks: u32,
) -> Result<(), String> {
    let address = string_argument(arguments, "--address")?;
    let client_id = argument(arguments, "--client", 0_u8)?;
    let render_paths = match (
        optional_path_argument(arguments, "--render-output"),
        optional_path_argument(arguments, "--render-metadata"),
    ) {
        (Some(output), Some(metadata)) => {
            reject_path_aliases(&[
                ("--render-output", output.as_path()),
                ("--render-metadata", metadata.as_path()),
            ])?;
            Some((output, metadata))
        }
        (None, None) => None,
        _ => {
            return Err(
                "--render-output and --render-metadata must be provided together".to_owned(),
            );
        }
    };
    let scripts = scripted_inputs_for(profile, seed, ticks);
    let inputs = scripts
        .get(usize::from(client_id))
        .ok_or_else(|| "client must be 0 or 1".to_owned())?;
    let report = send_inputs(address, client_id, seed, profile, inputs)?;
    if let Some((output, metadata)) = render_paths {
        let evidence = capture_state(
            &report.final_report.state,
            &report.final_report.state_hash,
            &output,
            seed,
            ticks,
            "live-network",
            Some(client_id),
        )?;
        write_metadata(&metadata, &evidence)?;
    }
    print_json(&report)
}

fn capture(
    arguments: &[String],
    profile: ReplayProfile,
    seed: u64,
    ticks: u32,
) -> Result<(), String> {
    let output = path_argument(arguments, "--output")?;
    let metadata = path_argument(arguments, "--metadata")?;
    reject_path_aliases(&[("--output", &output), ("--metadata", &metadata)])?;
    let (state, state_hash) = run_profile_match(profile, seed, ticks);
    let evidence = capture_state(&state, &state_hash, &output, seed, ticks, "single", None)?;
    write_metadata(&metadata, &evidence)?;
    print_json(&evidence)
}

fn capture_replay(
    arguments: &[String],
    profile: ReplayProfile,
    seed: u64,
    ticks: u32,
) -> Result<(), String> {
    if ticks != profile.replay_ticks() {
        return Err(format!(
            "capture-replay requires the admitted {}-tick {} profile",
            profile.replay_ticks(),
            profile.name()
        ));
    }
    let output_dir = path_argument(arguments, "--output-dir")?;
    let metadata = path_argument(arguments, "--metadata")?;
    let anchors: Vec<(&str, u32)> = match profile {
        ReplayProfile::TimberCollapseReplay => vec![
            ("intact", 0),
            ("pre-impact", 828),
            ("bright-impact", 864),
            ("impact-plus-100ms", 870),
            ("impact-plus-200ms", 876),
            ("impact-plus-300ms", 882),
            ("impact-plus-400ms", 888),
            ("first-release", 894),
            ("deformation", 912),
            ("debris", 1_050),
            ("settlement", 1_140),
            ("continued-combat", 1_410),
        ],
        ReplayProfile::TealDuelReplay => vec![
            ("spawn", 20),
            ("asymmetric-traversal", 120),
            ("shot", 435),
            ("block-reflection", 700),
            ("terminal-impact", profile.replay_ticks()),
        ],
        ReplayProfile::RadialSawHalfBlueReplay => vec![
            ("arena-reveal", 0),
            ("upper-slope-traversal", 180),
            ("projectile-exchange", 360),
            ("ordinary-impact", 540),
            ("late-traversal", 840),
            ("last-combat", RADIAL_LAST_COMBAT_TICK),
            ("result-onset", RADIAL_RESULT_ONSET_TICK),
            ("half-blue", RADIAL_HALF_BLUE_TICK),
        ],
        ReplayProfile::YellowCrateTerminalBlastReplay => vec![
            ("calm-start", 0),
            ("last-calm", YELLOW_LAST_CALM_TICK),
            ("first-response", YELLOW_IMPACT_TICK),
            ("local-burst", YELLOW_LOCAL_BURST_TICK),
            ("peak-radial-echo", YELLOW_PEAK_ECHO_TICK),
            ("directional-trails", YELLOW_TRAILS_TICK),
            ("last-combat", YELLOW_LAST_COMBAT_TICK),
            ("result-onset", YELLOW_RESULT_ONSET_TICK),
            ("following-result-transition", YELLOW_FOLLOWING_RESULT_TICK),
            ("round-orange", YELLOW_ROUND_ORANGE_TICK),
            ("result-tail", YELLOW_REPLAY_TICKS),
        ],
        ReplayProfile::RematchDraftReplay => vec![
            ("victory", 180),
            ("rematch-prompt", 300),
            ("arena-fade", 420),
            ("orange-initial-offer", 540),
            ("orange-burst-hover", 600),
            ("orange-dazzle-confirmed", 840),
            ("blue-initial-offer", 960),
            ("blue-lifestealer-hover", 1_560),
            ("blue-echo-hover", 2_040),
            ("blue-explosive-confirmed", 2_120),
            ("resumed-combat", 2_220),
            ("upgraded-projectiles", 2_280),
            ("continued-combat", 2_400),
        ],
    };
    let outputs = anchors
        .iter()
        .map(|(anchor, tick)| output_dir.join(format!("{tick:04}-{anchor}.png")))
        .collect::<Vec<_>>();
    let mut destinations = Vec::with_capacity(outputs.len() + 1);
    destinations.push(("--metadata", metadata.as_path()));
    destinations.extend(
        outputs
            .iter()
            .map(|output| ("generated replay PNG", output.as_path())),
    );
    reject_path_aliases(&destinations)?;
    let mut evidence = Vec::with_capacity(anchors.len());
    for ((anchor, tick), output) in anchors.into_iter().zip(outputs) {
        let (state, state_hash) = run_profile_match(profile, seed, tick);
        evidence.push(capture_state(
            &state,
            &state_hash,
            &output,
            seed,
            ticks,
            anchor,
            None,
        )?);
    }
    write_metadata(&metadata, &evidence)?;
    print_json(&evidence)
}

fn capture_state(
    state: &MatchSnapshot,
    state_hash: &str,
    output: &Path,
    seed: u64,
    trace_ticks: u32,
    anchor: &str,
    live_client_id: Option<u8>,
) -> Result<CaptureEvidence, String> {
    let profile = state.profile.parse::<ReplayProfile>()?;
    let scripts = scripted_inputs_for(profile, seed, trace_ticks);
    let script_bytes = serde_json::to_vec(&scripts).map_err(|error| error.to_string())?;
    let resolved_output = resolved_path(output)?;
    let frame = render_png(state, output)?;
    let executable = env::current_exe().map_err(|error| error.to_string())?;
    let visual_signature = (profile == ReplayProfile::YellowCrateTerminalBlastReplay)
        .then(|| yellow_frame_signature(&frame))
        .transpose()?;
    Ok(CaptureEvidence {
        format: 3,
        package: format!("rounds-client@{}", env!("CARGO_PKG_VERSION")),
        executable_sha256: sha256(
            &fs::read(&executable)
                .map_err(|error| format!("read {}: {error}", executable.display()))?,
        ),
        seed,
        tick: state.tick,
        anchor: anchor.to_owned(),
        source_interval: profile.source_interval(),
        source_timestamp: source_timestamp(profile, state.tick),
        source_pts: source_binding(profile, state.tick).map(|binding| binding.0),
        source_rgba_sha256: source_binding(profile, state.tick).map(|binding| binding.1),
        source_sha256: profile.source_sha256(),
        input_trace: profile.name(),
        input_trace_sha256: sha256(&script_bytes),
        state_sha256: state_hash.to_owned(),
        dynamic_body_sha256: dynamic_body_digest(state),
        arena_sha256: arena_digest(state),
        saw_sha256: saw_digest(state),
        combat_sha256: combat_digest(state),
        round_sha256: round_digest(state),
        flow_sha256: state.flow.as_ref().map(flow_digest),
        loadout_sha256: state.flow.as_ref().map(loadout_digest),
        renderer: RENDERER_IDENTITY,
        frame_sha256: frame_sha256(&frame),
        frame_path: resolved_output.to_string_lossy().replace('\\', "/"),
        width: FRAME_WIDTH,
        height: FRAME_HEIGHT,
        live_client_id,
        composited_echo_pass: (profile == ReplayProfile::YellowCrateTerminalBlastReplay)
            .then_some("rounds-radial-echo-final-composite-single-pass"),
        impact_audio_hook_tick: (profile == ReplayProfile::YellowCrateTerminalBlastReplay)
            .then_some(YELLOW_IMPACT_TICK),
        visual_signature,
    })
}

fn source_timestamp(profile: ReplayProfile, tick: u32) -> String {
    if let Some((pts, _)) = source_binding(profile, tick) {
        let micros = (pts + 5) / 10;
        return format!(
            "{:02}:{:02}.{:06}",
            micros / 60_000_000,
            (micros / 1_000_000) % 60,
            micros % 1_000_000
        );
    }
    let hundredths = profile.source_start_hundredths() + (u64::from(tick) * 100 / 60);
    format!(
        "{:02}:{:02}.{:02}",
        hundredths / 6_000,
        (hundredths / 100) % 60,
        hundredths % 100
    )
}

fn source_binding(profile: ReplayProfile, tick: u32) -> Option<(i64, &'static str)> {
    if profile == ReplayProfile::YellowCrateTerminalBlastReplay {
        return match tick {
            0 => Some((
                4_220_149_786,
                "0b43d0363e68046c92773d5cedd730c70c58d3d3b4604e1f8fa13bc4a7fc4d2f",
            )),
            YELLOW_LAST_CALM_TICK => Some((
                4_233_483_066,
                "d57dbd7ea73ba1d2e9c6c6fd37281629c0ee897aa52a29f6e81fb6cf532112bd",
            )),
            YELLOW_IMPACT_TICK => Some((
                4_233_649_732,
                "8fa8546c2b8a61b61b1d0ee58e217e4cf4b2ee99aedd437aa84606d50af84d14",
            )),
            YELLOW_LOCAL_BURST_TICK => Some((
                4_234_149_730,
                "d849c420675a75055a48f6fdf8a028a73192c1857d85fc84895c4732e32d1321",
            )),
            YELLOW_PEAK_ECHO_TICK => Some((
                4_234_983_060,
                "0ca4ecadfc3d4ff6e2e7c12ad3ff5167b0f5d36429096f6f82af9a521b58b3b1",
            )),
            YELLOW_TRAILS_TICK => Some((
                4_237_149_718,
                "82feb79de9feb66b2fc6d600257b3107d9d833a3c86c71ff4fecacf5601cf8ca",
            )),
            YELLOW_LAST_COMBAT_TICK => Some((
                4_238_316_380,
                "c2e0df8612a7ea782e40f94e1c8a941971368db55fd767cdbc742bc27e460124",
            )),
            YELLOW_RESULT_ONSET_TICK => Some((
                4_238_483_046,
                "74636191cb28cb44ac48d8f6106f5f9637b202b0097a35a966d2ac394a618c60",
            )),
            YELLOW_FOLLOWING_RESULT_TICK => Some((
                4_238_649_712,
                "e315638c3fff8b0904470c38938dd9690349e26ff3c36be9c112a93d39e7f518",
            )),
            YELLOW_ROUND_ORANGE_TICK => Some((
                4_240_983_036,
                "aa4ad165071ac401b54fa2f6687a2ea08aef04ceb0c33dc32965fd5049e8ddce",
            )),
            YELLOW_REPLAY_TICKS => Some((
                4_245_983_016,
                "bd923e601703bc0fbd5fb9c1f45983abf5d5449db3ea0bb5c9be300be48bc032",
            )),
            _ => None,
        };
    }
    if profile != ReplayProfile::RadialSawHalfBlueReplay {
        return None;
    }
    match tick {
        0 => Some((
            2_320_490_718,
            "6266d4e7fe24f1d0c7069279479c341dca5229d7b596e077368e4e73eb9c236f",
        )),
        180 => Some((
            2_350_657_264,
            "67b696e933729bc62463cffd3469a1a54ba15682e3dcc45bfce782626677e87e",
        )),
        360 => Some((
            2_380_657_144,
            "71c7b2c860cef7110992984f26f8e821c5f9b1caa9bb1d8d17f58fbf2ea62887",
        )),
        540 => Some((
            2_410_657_024,
            "6751167ed9f01b1a81c50abc64c650393d2e4d63e2f62a770c78e595e0073ec5",
        )),
        840 => Some((
            2_460_656_824,
            "faac3f7dbed16d238f44090b4d2fdf3db6fd025d438a867b97817eba2afab7a7",
        )),
        RADIAL_LAST_COMBAT_TICK => Some((
            2_471_823_446,
            "687acf71fb2fce461692bd7443b7703274dcb895a6b17b6a86e1bbfd136e92ce",
        )),
        RADIAL_RESULT_ONSET_TICK => Some((
            2_471_990_112,
            "d9161d231c3e90438ea7c638abe2dfe1feba2f1bf28edc3a94a841d4b69b7cca",
        )),
        RADIAL_HALF_BLUE_TICK => Some((
            2_476_823_426,
            "0b9d2136b7e010a359d95e3d2310a3e55c1e851429537cefcbd7cece12d185ca",
        )),
        _ => None,
    }
}

fn local_report(profile: ReplayProfile, seed: u64, ticks: u32) -> ServerReport {
    let (state, state_hash) = run_profile_match(profile, seed, ticks);
    ServerReport {
        protocol: NETWORK_PROTOCOL,
        clients_handshaken: 2,
        inputs_received: ticks * 2,
        progressive_snapshots: ticks,
        first_snapshot_tick: 1,
        last_snapshot_tick: ticks,
        state_hash,
        dynamic_body_digest: dynamic_body_digest(&state),
        arena_digest: arena_digest(&state),
        saw_digest: saw_digest(&state),
        combat_digest: combat_digest(&state),
        round_digest: round_digest(&state),
        flow_digest: state.flow.as_ref().map(flow_digest),
        loadout_digest: state.flow.as_ref().map(loadout_digest),
        state,
    }
}

fn write_metadata(path: &Path, evidence: &impl Serialize) -> Result<(), String> {
    create_parent(path)?;
    fs::write(
        path,
        serde_json::to_vec_pretty(evidence).map_err(|error| error.to_string())?,
    )
    .map_err(|error| format!("write {}: {error}", path.display()))
}

fn reject_path_aliases(destinations: &[(&str, &Path)]) -> Result<(), String> {
    let destinations = destinations
        .iter()
        .map(|(name, path)| Ok((*name, resolved_path(path)?)))
        .collect::<Result<Vec<_>, String>>()?;
    for left in 0..destinations.len() {
        for right in left + 1..destinations.len() {
            if paths_equal(&destinations[left].1, &destinations[right].1) {
                return Err(format!(
                    "{} and {} must resolve to different paths: {}",
                    destinations[left].0,
                    destinations[right].0,
                    destinations[left].1.display()
                ));
            }
        }
    }
    Ok(())
}

fn resolved_path(path: &Path) -> Result<PathBuf, String> {
    let absolute = std::path::absolute(path)
        .map_err(|error| format!("resolve {}: {error}", path.display()))?;
    if absolute.exists() {
        return fs::canonicalize(&absolute)
            .map_err(|error| format!("resolve {}: {error}", path.display()));
    }
    let mut ancestor = absolute.as_path();
    let mut missing = Vec::new();
    while !ancestor.exists() {
        let name = ancestor
            .file_name()
            .ok_or_else(|| format!("resolve {}: no existing ancestor", path.display()))?;
        missing.push(name.to_owned());
        ancestor = ancestor
            .parent()
            .ok_or_else(|| format!("resolve {}: no existing ancestor", path.display()))?;
    }
    let mut resolved = fs::canonicalize(ancestor)
        .map_err(|error| format!("resolve {}: {error}", path.display()))?;
    for name in missing.into_iter().rev() {
        resolved.push(name);
    }
    Ok(resolved)
}

fn paths_equal(left: &Path, right: &Path) -> bool {
    if cfg!(windows) {
        left.to_string_lossy()
            .eq_ignore_ascii_case(&right.to_string_lossy())
    } else {
        left == right
    }
}

fn sha256(bytes: &[u8]) -> String {
    format!("{:x}", Sha256::digest(bytes))
}

fn create_parent(path: &Path) -> Result<(), String> {
    if let Some(parent) = path
        .parent()
        .filter(|parent| !parent.as_os_str().is_empty())
    {
        fs::create_dir_all(parent)
            .map_err(|error| format!("create {}: {error}", parent.display()))?;
    }
    Ok(())
}

fn print_json(value: &impl Serialize) -> Result<(), String> {
    println!(
        "{}",
        serde_json::to_string(value).map_err(|error| error.to_string())?
    );
    Ok(())
}

fn argument<T>(arguments: &[String], name: &str, default: T) -> Result<T, String>
where
    T: std::str::FromStr,
    T::Err: std::fmt::Display,
{
    let Some(index) = arguments.iter().position(|argument| argument == name) else {
        return Ok(default);
    };
    arguments
        .get(index + 1)
        .ok_or_else(|| format!("missing value for {name}"))?
        .parse()
        .map_err(|error| format!("invalid {name}: {error}"))
}

fn string_argument<'a>(arguments: &'a [String], name: &str) -> Result<&'a str, String> {
    let index = arguments
        .iter()
        .position(|argument| argument == name)
        .ok_or_else(|| format!("missing {name}"))?;
    arguments
        .get(index + 1)
        .map(String::as_str)
        .ok_or_else(|| format!("missing value for {name}"))
}

fn optional_path_argument(arguments: &[String], name: &str) -> Option<PathBuf> {
    arguments
        .iter()
        .position(|argument| argument == name)
        .and_then(|index| arguments.get(index + 1))
        .map(PathBuf::from)
}

fn path_argument(arguments: &[String], name: &str) -> Result<PathBuf, String> {
    optional_path_argument(arguments, name).ok_or_else(|| format!("missing {name}"))
}
