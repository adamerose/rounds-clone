use rounds_network::{NETWORK_PROTOCOL, ServerReport, send_inputs};
use rounds_presentation::{
    FRAME_HEIGHT, FRAME_WIDTH, RENDERER_IDENTITY, frame_sha256, render_png, run_visible,
};
use rounds_sim::{
    MatchSnapshot, REPLAY_PROFILE, REPLAY_TICKS, SOURCE_INTERVAL, SOURCE_SHA256,
    run_scripted_match, run_scripted_snapshots, scripted_inputs,
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
    source_sha256: &'static str,
    input_trace: &'static str,
    input_trace_sha256: String,
    state_sha256: String,
    renderer: &'static str,
    frame_sha256: String,
    frame_path: String,
    width: u32,
    height: u32,
    live_client_id: Option<u8>,
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
    match mode {
        "capture" => capture(&arguments, seed, ticks),
        "capture-replay" => capture_replay(&arguments, seed, ticks),
        "local" => print_json(&local_report(seed, ticks)),
        "visible" => {
            let frames = argument(&arguments, "--frames", 180_u32)?;
            if frames < 5 {
                return Err("--frames must be at least 5".to_owned());
            }
            let replay = run_scripted_snapshots(seed, ticks);
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
            let report = local_report(seed, ticks);
            run_visible(sampled)?;
            print_json(&report)
        }
        "remote" => remote(&arguments, seed, ticks),
        _ => Err(
            "usage: rounds-client [local|remote|capture|capture-replay|visible] [options]"
                .to_owned(),
        ),
    }
}

fn remote(arguments: &[String], seed: u64, ticks: u32) -> Result<(), String> {
    let address = string_argument(arguments, "--address")?;
    let client_id = argument(arguments, "--client", 0_u8)?;
    let scripts = scripted_inputs(seed, ticks);
    let inputs = scripts
        .get(usize::from(client_id))
        .ok_or_else(|| "client must be 0 or 1".to_owned())?;
    let report = send_inputs(address, client_id, seed, inputs)?;
    match (
        optional_path_argument(arguments, "--render-output"),
        optional_path_argument(arguments, "--render-metadata"),
    ) {
        (Some(output), Some(metadata)) => {
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
        (None, None) => {}
        _ => {
            return Err(
                "--render-output and --render-metadata must be provided together".to_owned(),
            );
        }
    }
    print_json(&report)
}

fn capture(arguments: &[String], seed: u64, ticks: u32) -> Result<(), String> {
    let output = path_argument(arguments, "--output")?;
    let metadata = path_argument(arguments, "--metadata")?;
    reject_same_path(&output, &metadata)?;
    let (state, state_hash) = run_scripted_match(seed, ticks);
    let evidence = capture_state(&state, &state_hash, &output, seed, ticks, "single", None)?;
    write_metadata(&metadata, &evidence)?;
    print_json(&evidence)
}

fn capture_replay(arguments: &[String], seed: u64, ticks: u32) -> Result<(), String> {
    if ticks != REPLAY_TICKS {
        return Err(format!(
            "capture-replay requires the admitted {REPLAY_TICKS}-tick profile"
        ));
    }
    let output_dir = path_argument(arguments, "--output-dir")?;
    let metadata = path_argument(arguments, "--metadata")?;
    let anchors = [
        ("spawn", 20),
        ("traversal", 120),
        ("shot-block", 310),
        ("hit-knockback", 690),
        ("round-end", 779),
    ];
    let mut evidence = Vec::with_capacity(anchors.len());
    for (anchor, tick) in anchors {
        let (state, state_hash) = run_scripted_match(seed, tick);
        let output = output_dir.join(format!("{tick:04}-{anchor}.png"));
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
    let scripts = scripted_inputs(seed, trace_ticks);
    let script_bytes = serde_json::to_vec(&scripts).map_err(|error| error.to_string())?;
    let resolved_output = resolved_path(output)?;
    let frame = render_png(state, output)?;
    let executable = env::current_exe().map_err(|error| error.to_string())?;
    Ok(CaptureEvidence {
        format: 2,
        package: format!("rounds-client@{}", env!("CARGO_PKG_VERSION")),
        executable_sha256: sha256(
            &fs::read(&executable)
                .map_err(|error| format!("read {}: {error}", executable.display()))?,
        ),
        seed,
        tick: state.tick,
        anchor: anchor.to_owned(),
        source_interval: SOURCE_INTERVAL,
        source_timestamp: source_timestamp(state.tick),
        source_sha256: SOURCE_SHA256,
        input_trace: REPLAY_PROFILE,
        input_trace_sha256: sha256(&script_bytes),
        state_sha256: state_hash.to_owned(),
        renderer: RENDERER_IDENTITY,
        frame_sha256: frame_sha256(&frame),
        frame_path: resolved_output.to_string_lossy().replace('\\', "/"),
        width: FRAME_WIDTH,
        height: FRAME_HEIGHT,
        live_client_id,
    })
}

fn source_timestamp(tick: u32) -> String {
    let hundredths = 2_250 + (u64::from(tick) * 100 / 60);
    format!("00:{:02}.{:02}", hundredths / 100, hundredths % 100)
}

fn local_report(seed: u64, ticks: u32) -> ServerReport {
    let (state, state_hash) = run_scripted_match(seed, ticks);
    ServerReport {
        protocol: NETWORK_PROTOCOL,
        clients_handshaken: 2,
        inputs_received: ticks * 2,
        progressive_snapshots: ticks,
        first_snapshot_tick: 1,
        last_snapshot_tick: ticks,
        state_hash,
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

fn reject_same_path(left: &Path, right: &Path) -> Result<(), String> {
    let left = resolved_path(left)?;
    let right = resolved_path(right)?;
    if paths_equal(&left, &right) {
        return Err(format!(
            "--output and --metadata must resolve to different paths: {}",
            left.display()
        ));
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
