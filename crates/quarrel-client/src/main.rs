use quarrel_network::{LiveClient, LiveServer, ServerReport, send_inputs};
use quarrel_presentation::{render_png, run_interactive_visible, run_live_visible, run_visible};
use quarrel_sim::{
    AuthoritativeMatch, FlowPhase, InputRecording, MatchConfig, PlayerInput, automated_input,
    hash_snapshot, play_recording,
};
use serde::Serialize;
use std::{env, fs, path::PathBuf};

fn main() {
    if let Err(error) = run() {
        eprintln!("{error}");
        std::process::exit(1);
    }
}
fn run() -> Result<(), String> {
    let args = env::args().collect::<Vec<_>>();
    let mode = args.get(1).map(String::as_str).unwrap_or("local");
    let config = config(&args)?;
    let ticks = argument(&args, "--ticks", 2_400_u32)?;
    match mode {
        "local" => print(run_local(config, ticks)?),
        "replay" => { let path = required(&args, "--input")?; let recording: InputRecording = serde_json::from_slice(&fs::read(&path).map_err(|e| format!("read {path}: {e}"))?).map_err(|e| format!("decode {path}: {e}"))?; let states = play_recording(&recording)?; print(states.last().ok_or("empty replay")?) },
        "remote" => { let address = required(&args, "--address")?; let id = argument(&args, "--client", 0_u8)?; let frames = scripted(config.clone(), ticks)?; let inputs = frames.into_iter().map(|frame| frame.get(usize::from(id)).copied().ok_or("client is outside fighter count")).collect::<Result<Vec<_>, _>>()?; print(send_inputs(address, id, config, &inputs)?) },
        "capture" => { let output = PathBuf::from(required(&args, "--output")?); let state = match optional(&args, "--state") { Some(path) => serde_json::from_slice(&fs::read(&path).map_err(|e| format!("read {path}: {e}"))?).map_err(|e| format!("decode {path}: {e}"))?, None => state_after(config, ticks)? }; render_png(&state, &output)?; let metadata = serde_json::json!({"output": output, "stateSha256": hash_snapshot(&state), "tick": state.tick, "phase": state.flow.as_ref().map(|flow| flow.phase)}); if let Some(path) = optional(&args, "--metadata") { fs::write(&path, serde_json::to_vec_pretty(&metadata).map_err(|e| e.to_string())?).map_err(|e| format!("write {path}: {e}"))?; } print(metadata) },
        "record" => { let output = PathBuf::from(required(&args, "--output")?); let frames = scripted_until_match_end(config.clone(), ticks)?; let recorded_ticks = frames.len(); fs::write(&output, serde_json::to_vec_pretty(&InputRecording { config, frames }).map_err(|e| e.to_string())?).map_err(|e| format!("write {}: {e}", output.display()))?; print(serde_json::json!({"output": output, "ticks": recorded_ticks})) },
        "visible" => { let state = state_after(config.clone(), ticks)?; run_visible(vec![state.clone(); 120])?; print(serde_json::json!({"stateSha256": hash_snapshot(&state)})) },
        "visible-flow" => { let state = run_interactive_visible(config, ticks, args.iter().any(|argument| argument == "--automated"))?; print(serde_json::json!({"stateSha256": hash_snapshot(&state)})) },
        "arena-preview" => { let arena = PathBuf::from(required(&args, "--arena")?); let output = PathBuf::from(required(&args, "--output")?); let mut state = AuthoritativeMatch::from_arena_file(config.seed, &arena)?.snapshot(); state.flow = None; render_png(&state, &output)?; print(serde_json::json!({"arena": arena, "output": output, "stateSha256": hash_snapshot(&state)})) },
        "join" => join(&args, config, ticks),
        "host" => host(&args, config, ticks),
        _ => Err("usage: quarrel-client [local|replay|remote|join|host|capture|record|visible|visible-flow|arena-preview] [options]".into()),
    }
}
fn scripted(config: MatchConfig, ticks: u32) -> Result<Vec<Vec<PlayerInput>>, String> {
    let mut game = AuthoritativeMatch::with_config(config.clone())?;
    let mut frames = Vec::with_capacity(ticks as usize);
    for _ in 0..ticks {
        let state = game.snapshot();
        let inputs = (0..config.fighter_count)
            .map(|id| {
                automated_input(id as u8, &state)
                    .with_progressive_observation(id as u8, Some(&state))
            })
            .collect::<Vec<_>>();
        game.step(&inputs);
        frames.push(inputs);
    }
    Ok(frames)
}
fn scripted_until_match_end(
    config: MatchConfig,
    ticks: u32,
) -> Result<Vec<Vec<PlayerInput>>, String> {
    let mut game = AuthoritativeMatch::with_config(config.clone())?;
    let mut frames = Vec::with_capacity(ticks as usize);
    for _ in 0..ticks {
        let state = game.snapshot();
        if state
            .flow
            .as_ref()
            .is_some_and(|flow| flow.phase == FlowPhase::MatchEnd)
        {
            break;
        }
        let inputs = (0..config.fighter_count)
            .map(|id| {
                automated_input(id as u8, &state)
                    .with_progressive_observation(id as u8, Some(&state))
            })
            .collect::<Vec<_>>();
        game.step(&inputs);
        frames.push(inputs);
    }
    Ok(frames)
}
fn state_after(config: MatchConfig, ticks: u32) -> Result<quarrel_sim::MatchSnapshot, String> {
    let mut game = AuthoritativeMatch::with_config(config.clone())?;
    for inputs in scripted(config, ticks)? {
        game.step(&inputs);
    }
    Ok(game.snapshot())
}
fn run_local(config: MatchConfig, ticks: u32) -> Result<ServerReport, String> {
    let state = state_after(config.clone(), ticks)?;
    Ok(ServerReport {
        protocol: quarrel_network::NETWORK_PROTOCOL,
        config: config.clone(),
        clients_handshaken: config.fighter_count as u8,
        inputs_received: ticks * config.fighter_count as u32,
        progressive_snapshots: ticks,
        first_snapshot_tick: 1,
        last_snapshot_tick: state.tick,
        state_hash: hash_snapshot(&state),
        state,
    })
}
fn join(args: &[String], config: MatchConfig, ticks: u32) -> Result<(), String> {
    let address = required(args, "--address")?;
    let id = argument(args, "--client", 0_u8)?;
    run_live_client(address, id, config, ticks)
}
fn run_live_client(
    address: impl std::net::ToSocketAddrs,
    id: u8,
    config: MatchConfig,
    ticks: u32,
) -> Result<(), String> {
    let client = LiveClient::connect(address, id, config)?;
    let handle = client.handle();
    let close = handle.clone();
    let network = std::thread::spawn(move || client.run(ticks));
    let presentation = run_live_visible(handle, id);
    close.close();
    let report = network
        .join()
        .map_err(|_| "live client thread panicked".to_owned())??;
    print(&report)?;
    presentation?;
    if report.result == "completed" {
        Ok(())
    } else {
        Err(format!("live client {}", report.result))
    }
}
fn host(args: &[String], config: MatchConfig, ticks: u32) -> Result<(), String> {
    let bind = argument(args, "--bind", "127.0.0.1".to_owned())?;
    let port = argument(args, "--port", 0_u16)?;
    let id = argument(args, "--client", 0_u8)?;
    let server = LiveServer::bind(format!("{bind}:{port}"))?;
    let address = server.local_addr()?;
    println!("{{\"event\":\"listening\",\"address\":\"{address}\"}}");
    let server_config = config.clone();
    let trace = optional(args, "--trace").map(PathBuf::from);
    let authority = std::thread::spawn(move || server.run(server_config, ticks, trace.as_deref()));
    let peer = if address.ip().is_unspecified() {
        std::net::SocketAddr::from(([127, 0, 0, 1], address.port()))
    } else {
        address
    };
    let client = run_live_client(peer, id, config, ticks);
    let report = authority
        .join()
        .map_err(|_| "live authority thread panicked".to_owned())??;
    print(&report)?;
    client?;
    if report.result == "completed" {
        Ok(())
    } else {
        Err(format!("live authority {}", report.result))
    }
}
fn config(args: &[String]) -> Result<MatchConfig, String> {
    Ok(MatchConfig {
        seed: argument(args, "--seed", 38)?,
        fighter_count: argument(args, "--fighters", 2)?,
        target_score: argument(args, "--target-score", 5)?,
        offer_size: argument(args, "--offer-size", 5)?,
        run_it_back_limit: argument(args, "--run-it-back-limit", 2)?,
    })
}
fn required(args: &[String], name: &str) -> Result<String, String> {
    args.iter()
        .position(|arg| arg == name)
        .and_then(|i| args.get(i + 1))
        .cloned()
        .ok_or_else(|| format!("missing value for {name}"))
}
fn optional(args: &[String], name: &str) -> Option<String> {
    args.iter()
        .position(|arg| arg == name)
        .and_then(|i| args.get(i + 1))
        .cloned()
}
fn argument<T: std::str::FromStr>(args: &[String], name: &str, default: T) -> Result<T, String>
where
    T::Err: std::fmt::Display,
{
    args.iter()
        .position(|arg| arg == name)
        .map_or(Ok(default), |i| {
            args.get(i + 1)
                .ok_or_else(|| format!("missing value for {name}"))?
                .parse()
                .map_err(|e| format!("invalid {name}: {e}"))
        })
}
fn print(value: impl Serialize) -> Result<(), String> {
    println!(
        "{}",
        serde_json::to_string(&value).map_err(|e| e.to_string())?
    );
    Ok(())
}
