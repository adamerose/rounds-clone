use rounds_network::{ClientScript, NETWORK_PROTOCOL, ServerReport, send_script};
use rounds_presentation::{FRAME_HEIGHT, FRAME_WIDTH, frame_sha256, render_png};
use rounds_sim::{run_scripted_match, scripted_inputs};
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
    script: &'static str,
    script_sha256: String,
    state_sha256: String,
    frame_sha256: String,
    frame_path: String,
    width: usize,
    height: usize,
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
    let ticks = argument(&arguments, "--ticks", 180_u32)?;
    let seed = argument(&arguments, "--seed", 38_u64)?;
    let report = match mode {
        "capture" => return capture(&arguments, seed, ticks),
        "local" => {
            let (state, state_hash) = run_scripted_match(seed, ticks);
            ServerReport {
                protocol: NETWORK_PROTOCOL,
                state_hash,
                state,
            }
        }
        "remote" => {
            let address = string_argument(&arguments, "--address")?;
            let client_id = argument(&arguments, "--client", 0_u8)?;
            let scripts = scripted_inputs(seed, ticks);
            let inputs = scripts
                .get(usize::from(client_id))
                .ok_or_else(|| "client must be 0 or 1".to_owned())?
                .clone();
            send_script(
                address,
                &ClientScript {
                    protocol: NETWORK_PROTOCOL,
                    client_id,
                    seed,
                    inputs,
                },
            )?
        }
        _ => return Err("usage: rounds-client [local|remote|capture] [options]".to_owned()),
    };
    println!(
        "{}",
        serde_json::to_string(&report).map_err(|error| error.to_string())?
    );
    Ok(())
}

fn capture(arguments: &[String], seed: u64, ticks: u32) -> Result<(), String> {
    let output = path_argument(arguments, "--output")?;
    let metadata = path_argument(arguments, "--metadata")?;
    let resolved_output = resolved_path(&output)?;
    let resolved_metadata = resolved_path(&metadata)?;
    if paths_equal(&resolved_output, &resolved_metadata) {
        return Err(format!(
            "--output and --metadata must resolve to different paths: {}",
            resolved_output.display()
        ));
    }
    let scripts = scripted_inputs(seed, ticks);
    let script_bytes = serde_json::to_vec(&scripts).map_err(|error| error.to_string())?;
    let (state, state_hash) = run_scripted_match(seed, ticks);
    let frame = render_png(&state);
    create_parent(&output)?;
    create_parent(&metadata)?;
    fs::write(&output, &frame).map_err(|error| format!("write {}: {error}", output.display()))?;
    let executable = env::current_exe().map_err(|error| error.to_string())?;
    let evidence = CaptureEvidence {
        format: 1,
        package: format!("rounds-client@{}", env!("CARGO_PKG_VERSION")),
        executable_sha256: sha256(
            &fs::read(&executable)
                .map_err(|error| format!("read {}: {error}", executable.display()))?,
        ),
        seed,
        tick: state.tick,
        script: "two-player-script-v1",
        script_sha256: sha256(&script_bytes),
        state_sha256: state_hash,
        frame_sha256: frame_sha256(&frame),
        frame_path: output.to_string_lossy().replace('\\', "/"),
        width: FRAME_WIDTH,
        height: FRAME_HEIGHT,
    };
    fs::write(
        &metadata,
        serde_json::to_vec_pretty(&evidence).map_err(|error| error.to_string())?,
    )
    .map_err(|error| format!("write {}: {error}", metadata.display()))?;
    println!(
        "{}",
        serde_json::to_string(&evidence).map_err(|error| error.to_string())?
    );
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

fn path_argument(arguments: &[String], name: &str) -> Result<PathBuf, String> {
    let index = arguments
        .iter()
        .position(|argument| argument == name)
        .ok_or_else(|| format!("missing {name}"))?;
    arguments
        .get(index + 1)
        .map(PathBuf::from)
        .ok_or_else(|| format!("missing value for {name}"))
}
