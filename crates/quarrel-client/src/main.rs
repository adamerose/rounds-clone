use quarrel_network::{
    Joined, LiveClient, LiveServer, MemoryNetwork, NetworkConditions, ServerReport, Transport,
    send_inputs,
};
use quarrel_presentation::{
    OnlineService, render_menu_png, render_png, render_waiting_png, run_interactive_visible,
    run_live_visible, run_menu, run_visible,
};
use quarrel_sim::{
    AuthoritativeMatch, FlowPhase, InputRecording, MatchConfig, MatchContent, PlayerInput,
    automated_input, hash_snapshot, play_recording,
};
use quarrel_steam::Steam;
use serde::Serialize;
use sha2::{Digest, Sha256};
use std::{
    env, fs,
    path::{Path, PathBuf},
};

fn main() {
    if let Err(error) = run() {
        eprintln!("{error}");
        std::process::exit(1);
    }
}
fn run() -> Result<(), String> {
    let mut args = env::args().collect::<Vec<_>>();
    // Exercise the same menu selections without opening a window for network evidence.
    if args.get(1).is_some_and(|mode| mode == "menu-start") {
        let selected = argument(&args, "--choice", 0_usize)?;
        let address = optional(&args, "--address").unwrap_or_else(|| "127.0.0.1:7777".into());
        let menu_args =
            quarrel_presentation::menu_match_args(selected, &address, &mut SteamMenu::new(None))?;
        let mut translated = vec![args[0].clone(), menu_args[0].clone()];
        translated.extend(args.into_iter().skip(2));
        translated.extend(menu_args.into_iter().skip(1));
        args = translated;
    }
    let mode = args.get(1).map(String::as_str).unwrap_or("menu");
    let config = config(&args)?;
    let ticks = argument(&args, "--ticks", 2_400_u32)?;
    match mode {
        "menu" => run_menu(SteamMenu::new(None)),
        // Steam starts the game this way when a player joins a friend from outside it.
        quarrel_steam::CONNECT_LOBBY => run_menu(SteamMenu::new(Some(
            quarrel_steam::connect_lobby(args.iter().map(String::as_str))
                .ok_or("+connect_lobby needs a lobby id")?,
        ))),
        "steam-host" => steam_host(&args, config, ticks),
        "steam-join" => steam_join(&args, config, ticks),
        "waiting-capture" => render_waiting_png(Path::new(&required(&args, "--output")?)).map(|_| ()),
        "menu-capture" => render_menu_png(Path::new(&required(&args, "--output")?)).map(|_| ()),
        "local" => print(run_local(config, ticks)?),
        "replay" => { let path = required(&args, "--input")?; let recording: InputRecording = serde_json::from_slice(&fs::read(&path).map_err(|e| format!("read {path}: {e}"))?).map_err(|e| format!("decode {path}: {e}"))?; let states = play_recording(&recording)?; print(states.last().ok_or("empty replay")?) },
        "remote" => remote(&args, config, ticks),
        "capture" => capture(&args, config, ticks),
        "record" => { let output = PathBuf::from(required(&args, "--output")?); let recording = scripted_until_match_end(config, ticks)?; let recorded_ticks = recording.frames.len(); fs::write(&output, serde_json::to_vec_pretty(&recording).map_err(|e| e.to_string())?).map_err(|e| format!("write {}: {e}", output.display()))?; print(serde_json::json!({"output": output, "ticks": recorded_ticks})) },
        "visible" => { let state = state_after(config.clone(), ticks)?; run_visible(vec![state.clone(); 120])?; print(serde_json::json!({"stateSha256": hash_snapshot(&state)})) },
        "visible-flow" => { let state = run_interactive_visible(config, ticks, args.iter().any(|argument| argument == "--automated"))?; print(serde_json::json!({"stateSha256": hash_snapshot(&state), "flow": state.flow, "tick": state.tick})) },
        "arena-preview" => { let arena = PathBuf::from(required(&args, "--arena")?); let output = PathBuf::from(required(&args, "--output")?); let mut state = AuthoritativeMatch::from_arena_file(config.seed, &arena)?.snapshot(); state.flow = None; render_png(&state, &output)?; print(serde_json::json!({"arena": arena, "output": output, "stateSha256": hash_snapshot(&state)})) },
        "join" => join(&args, config, ticks),
        "host" => host(&args, config, ticks),
        _ => Err("usage: quarrel-client [local|replay|remote|join|host|steam-host|steam-join|capture|record|visible|visible-flow|arena-preview] [options]".into()),
    }
}
fn capture_destinations(
    args: &[String],
    output_flag: &str,
    metadata_flag: &str,
) -> Result<Option<(PathBuf, Option<PathBuf>)>, String> {
    let output = optional(args, output_flag).map(PathBuf::from);
    let metadata = optional(args, metadata_flag).map(PathBuf::from);
    match (&output, &metadata) {
        (None, Some(_)) => return Err(format!("{metadata_flag} requires {output_flag}")),
        (Some(output), Some(metadata))
            if paths_equal(&resolved_path(output)?, &resolved_path(metadata)?) =>
        {
            return Err(format!(
                "{output_flag} and {metadata_flag} must resolve to different paths"
            ));
        }
        _ => {}
    }
    Ok(output.map(|output| (output, metadata)))
}
fn resolved_path(path: &Path) -> Result<PathBuf, String> {
    let absolute = std::path::absolute(path).map_err(|error| error.to_string())?;
    let mut ancestor = absolute.as_path();
    let mut missing = Vec::new();
    while !ancestor.exists() {
        missing.push(
            ancestor
                .file_name()
                .ok_or("destination has no existing ancestor")?
                .to_owned(),
        );
        ancestor = ancestor
            .parent()
            .ok_or("destination has no existing ancestor")?;
    }
    let mut resolved = fs::canonicalize(ancestor).map_err(|error| error.to_string())?;
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
fn capture(args: &[String], config: MatchConfig, ticks: u32) -> Result<(), String> {
    let (output, metadata) =
        capture_destinations(args, "--output", "--metadata")?.ok_or("missing --output")?;
    let state = match optional(args, "--state") {
        Some(path) => {
            serde_json::from_slice(&fs::read(&path).map_err(|e| format!("read {path}: {e}"))?)
                .map_err(|e| format!("decode {path}: {e}"))?
        }
        None => state_after(config, ticks)?,
    };
    print(render_capture(&state, &output, metadata.as_deref())?)
}
fn render_capture(
    state: &quarrel_sim::MatchSnapshot,
    output: &Path,
    metadata_path: Option<&Path>,
) -> Result<serde_json::Value, String> {
    let executable =
        fs::read(env::current_exe().map_err(|e| e.to_string())?).map_err(|e| e.to_string())?;
    let frame = render_png(state, output)?;
    let metadata = serde_json::json!({"format": 1, "package": env!("CARGO_PKG_VERSION"), "output": output, "executableSha256": format!("{:x}", Sha256::digest(executable)), "frameSha256": format!("{:x}", Sha256::digest(frame)), "stateSha256": hash_snapshot(state), "tick": state.tick, "phase": state.flow.as_ref().map(|flow| flow.phase)});
    if let Some(path) = metadata_path {
        if let Some(parent) = path
            .parent()
            .filter(|parent| !parent.as_os_str().is_empty())
        {
            fs::create_dir_all(parent).map_err(|e| e.to_string())?;
        }
        fs::write(
            path,
            serde_json::to_vec_pretty(&metadata).map_err(|e| e.to_string())?,
        )
        .map_err(|e| format!("write {}: {e}", path.display()))?;
    }
    Ok(metadata)
}
fn remote(args: &[String], config: MatchConfig, ticks: u32) -> Result<(), String> {
    let capture = capture_destinations(args, "--render-output", "--render-metadata")?;
    let address = required(args, "--address")?;
    let id = argument(args, "--client", 0_u8)?;
    let frames = scripted(config.clone(), ticks)?;
    let inputs = frames
        .into_iter()
        .map(|frame| {
            frame
                .get(usize::from(id))
                .copied()
                .ok_or("client is outside fighter count")
        })
        .collect::<Result<Vec<_>, _>>()?;
    let report = send_inputs(address, id, config, &inputs)?;
    if let Some((output, metadata)) = capture {
        render_capture(&report.final_report.state, &output, metadata.as_deref())?;
    }
    print(report)
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
fn scripted_until_match_end(config: MatchConfig, ticks: u32) -> Result<InputRecording, String> {
    let content = MatchContent::load_default()?;
    let mut game = AuthoritativeMatch::with_content(config.clone(), content.clone())?;
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
    Ok(InputRecording {
        config,
        content,
        frames,
    })
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
    run_live_client(
        address,
        id,
        config,
        ticks,
        args.iter().any(|arg| arg == "--headless"),
        args.iter().any(|arg| arg == "--interactive"),
    )
}
fn run_live_client(
    address: impl std::net::ToSocketAddrs,
    id: u8,
    config: MatchConfig,
    ticks: u32,
    headless: bool,
    interactive: bool,
) -> Result<(), String> {
    let client = if interactive {
        LiveClient::connect_interactive(address, id, config)?
    } else {
        LiveClient::connect(address, id, config)?
    };
    present_live_client(client, id, ticks, headless, interactive)
}

fn present_live_client<T: Transport + 'static>(
    client: LiveClient<T>,
    id: u8,
    ticks: u32,
    headless: bool,
    interactive: bool,
) -> Result<(), String> {
    let handle = client.handle();
    let close = handle.clone();
    let network = std::thread::spawn(move || {
        if interactive {
            client.run_interactive()
        } else {
            client.run(ticks)
        }
    });
    let presentation = if headless {
        let mut sent = None;
        let mut first_fight = false;
        let mut result = Ok(());
        while handle.result().is_none() {
            if let Some((state, _)) = handle.latest() {
                // Headless menu evidence is cancelled explicitly after its requested observation window.
                if interactive && state.tick >= ticks {
                    handle.close();
                }
                if !first_fight
                    && state
                        .flow
                        .as_ref()
                        .is_some_and(|flow| flow.phase == FlowPhase::Combat)
                {
                    first_fight = true;
                    println!(
                        "{{\"event\":\"menuFirstFight\",\"client\":{id},\"tick\":{}}}",
                        state.tick
                    );
                }
                let input = automated_input(id, &state);
                handle.set_held(input);
                if let Some(command) = input.flow
                    && sent != Some(command)
                {
                    if let Err(error) = handle.push_flow(command) {
                        result = Err(error);
                        break;
                    }
                    sent = Some(command);
                }
            }
            std::thread::sleep(std::time::Duration::from_millis(5));
        }
        result.and_then(|()| {
            first_fight
                .then_some(())
                .ok_or_else(|| "headless menu session never reached first fight".into())
        })
    } else {
        run_live_visible(handle, id)
    };
    close.close();
    let report = network
        .join()
        .map_err(|_| "live client thread panicked".to_owned())??;
    print(&report)?;
    presentation?;
    if report.result == "completed" || (interactive && report.result == "local_close") {
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
    let peer = if address.ip().is_unspecified() {
        std::net::SocketAddr::from(([127, 0, 0, 1], address.port()))
    } else {
        address
    };
    let headless = args.iter().any(|arg| arg == "--headless");
    if args.iter().any(|arg| arg == "--interactive") {
        let client = LiveClient::connect_interactive(peer, id, config.clone())?;
        return host_interactive(server, client, id, config, ticks, headless);
    }
    let server_config = config.clone();
    let trace = optional(args, "--trace").map(PathBuf::from);
    let authority = std::thread::spawn(move || server.run(server_config, ticks, trace.as_deref()));
    let client = run_live_client(peer, id, config, ticks, headless, false);
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
/// Plays the host's own fighter beside its authority until either side ends the session.
fn host_interactive<A: Transport + 'static, C: Transport + 'static>(
    server: LiveServer<A>,
    client: LiveClient<C>,
    id: u8,
    config: MatchConfig,
    ticks: u32,
    headless: bool,
) -> Result<(), String> {
    let client_close = client.handle();
    let server_close = server.handle();
    let authority = std::thread::spawn(move || {
        let result = server.run_interactive(config);
        let interrupted = result.is_err() && !client_close.is_closed();
        if interrupted {
            client_close.close();
        }
        (result, interrupted)
    });
    let client = present_live_client(client, id, ticks, headless, true);
    server_close.close();
    let (report, interrupted) = authority
        .join()
        .map_err(|_| "live authority thread panicked".to_owned())?;
    match &report {
        Ok(report) => print(report)?,
        Err(error) => print(serde_json::json!({"event":"authorityEnded", "result":error}))?,
    }
    if interrupted {
        return Err(report.unwrap_err());
    }
    client?;
    match report {
        Ok(_) => Ok(()),
        Err(error) if error == "local_close" || error.starts_with("peer_left: client ") => Ok(()),
        Err(error) => Err(error),
    }
}

/// Hosts an online match in a friends-only Steam lobby and opens Steam's invite overlay. The
/// host's own fighter reaches its authority in memory; the friend arrives over Steam's relay.
fn steam_host(args: &[String], config: MatchConfig, ticks: u32) -> Result<(), String> {
    let steam = std::sync::Arc::new(Steam::start()?);
    let lobby = steam.host_lobby()?;
    print(serde_json::json!({"event":"steamLobby", "lobby":lobby.to_string()}))?;
    let overlay = steam.clone();
    std::thread::spawn(move || {
        let opened = overlay.open_invite_overlay(lobby);
        let _ = print(serde_json::json!({"event":"steamInviteOverlay", "opened":opened}));
    });
    let conditions = NetworkConditions::from_env()?;
    let network = MemoryNetwork::default();
    let own = network.endpoint();
    let authority = Joined::new(
        network.endpoint(),
        steam.transport(steam.friend_or_member()),
    );
    let client = LiveClient::over(own, authority.first.peer(), 0, config.clone(), conditions)?;
    let server = LiveServer::over(authority, conditions)?;
    let headless = args.iter().any(|arg| arg == "--headless");
    host_interactive(server, client, 0, config, ticks, headless)
}

/// Joins a friend's Steam lobby and plays its match over Steam's relay.
fn steam_join(args: &[String], config: MatchConfig, ticks: u32) -> Result<(), String> {
    let lobby = required(args, "--lobby")?
        .parse()
        .map_err(|error| format!("invalid --lobby: {error}"))?;
    let steam = Steam::start()?;
    let host = steam.join_lobby(lobby)?;
    print(serde_json::json!({"event":"steamJoined", "lobby":lobby.to_string()}))?;
    let client = LiveClient::over(
        steam.transport(move |user| user == host),
        host,
        1,
        config,
        NetworkConditions::from_env()?,
    )?;
    present_live_client(
        client,
        1,
        ticks,
        args.iter().any(|arg| arg == "--headless"),
        true,
    )
}

/// The menu's Steam session. It hears join requests while the menu shows, and hands Steam to the
/// match process the menu starts.
struct SteamMenu {
    steam: Option<(Steam, std::sync::Mutex<std::sync::mpsc::Receiver<u64>>)>,
    pending: Option<u64>,
    last_attempt: Option<std::time::Instant>,
}

impl SteamMenu {
    fn new(pending: Option<u64>) -> Self {
        Self {
            steam: None,
            pending,
            last_attempt: None,
        }
    }
}

impl OnlineService for SteamMenu {
    fn ready(&mut self) -> Result<(), String> {
        if self.steam.is_none() {
            self.last_attempt = Some(std::time::Instant::now());
            let steam = Steam::start()?;
            let requests = steam.join_requests();
            self.steam = Some((steam, requests.into()));
        }
        Ok(())
    }
    fn join_request(&mut self) -> Option<u64> {
        if let Some(lobby) = self.pending.take() {
            return Some(lobby);
        }
        // Without Steam the menu retries quietly, so starting Steam later still delivers invites.
        if self.steam.is_none()
            && self
                .last_attempt
                .is_none_or(|attempt| attempt.elapsed() > std::time::Duration::from_secs(5))
        {
            let _ = self.ready();
        }
        self.steam
            .as_ref()
            .and_then(|(_, requests)| requests.lock().unwrap().try_recv().ok())
    }
    fn release(&mut self) {
        self.steam = None;
        self.last_attempt = None;
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
