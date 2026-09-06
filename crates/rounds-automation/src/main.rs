use rounds_network::{ClientSessionReport, NETWORK_PROTOCOL, ServerReport};
use rounds_sim::{
    FlowPhase, ItemId, REPLAY_TICKS, ReplayProfile, arena_digest, combat_digest,
    dynamic_body_digest, round_digest, run_profile_match, saw_digest,
};
use serde::Serialize;
use std::env;
use std::fs;
use std::io::{BufRead, BufReader, Read};
use std::path::{Path, PathBuf};
use std::process::{Child, Command, ExitStatus, Stdio};

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct SmokeEvidence {
    protocol: u16,
    transport: &'static str,
    seed: u64,
    ticks: u32,
    clients: u8,
    handshakes_complete: bool,
    first_input_sequence: u32,
    last_input_sequence: u32,
    snapshots_per_client: u32,
    first_snapshot_tick: u32,
    last_snapshot_tick: u32,
    clients_agree: bool,
    local_host_agrees: bool,
    live_rendered_state_agrees: bool,
    progressive_explosion_transition_observed: bool,
    dynamic_body_digest: String,
    arena_digest: String,
    saw_digest: String,
    combat_digest: String,
    round_digest: Option<String>,
    flow_digest: Option<String>,
    loadout_digest: Option<String>,
    both_clients_observed_same_flow: bool,
    both_clients_observed_source_terminal_state: bool,
    both_clients_observed_rematch_reset: bool,
    both_clients_observed_blue_fan_by_tick_960: bool,
    observed_flow_phases: Vec<FlowPhase>,
    flow_completed_with_source_loadouts: bool,
    radial_saw_motion_observed: bool,
    radial_damage_observed: bool,
    radial_result_onset_observed: bool,
    radial_half_blue_observed: bool,
    yellow_calm_observed: bool,
    yellow_terminal_blast_observed: bool,
    yellow_crate_motion_observed: bool,
    yellow_result_onset_observed: bool,
    yellow_following_result_observed: bool,
    yellow_round_orange_observed: bool,
    live_frame_path: String,
    live_metadata_path: String,
    state_hash: String,
}

struct ChildGuard {
    child: Child,
    finished: bool,
}

impl ChildGuard {
    fn new(child: Child) -> Self {
        Self {
            child,
            finished: false,
        }
    }

    fn wait(&mut self) -> std::io::Result<ExitStatus> {
        let status = self.child.wait()?;
        self.finished = true;
        Ok(status)
    }
}

impl Drop for ChildGuard {
    fn drop(&mut self) {
        if !self.finished {
            let _ = self.child.kill();
            let _ = self.child.wait();
        }
    }
}

fn main() {
    if let Err(error) = run() {
        eprintln!("{error}");
        std::process::exit(1);
    }
}

fn run() -> Result<(), String> {
    let arguments = env::args().collect::<Vec<_>>();
    match arguments.get(1).map(String::as_str) {
        Some("smoke") => smoke(&arguments),
        Some("inspect") => inspect(&arguments),
        _ => Err("usage: rounds-automation <smoke|inspect> [--seed N] [--ticks N]".to_owned()),
    }
}

fn smoke(arguments: &[String]) -> Result<(), String> {
    let ticks = argument(arguments, "--ticks", REPLAY_TICKS)?;
    let seed = argument(arguments, "--seed", 38_u64)?;
    let profile = argument(arguments, "--profile", ReplayProfile::default())?;
    let output_dir = optional_path_argument(arguments, "--output-dir")
        .unwrap_or_else(|| PathBuf::from("out/ticket-040/smoke"));
    fs::create_dir_all(&output_dir)
        .map_err(|error| format!("create {}: {error}", output_dir.display()))?;
    let live_frame = output_dir.join("live-client-0.png");
    let live_metadata = output_dir.join("live-client-0.json");
    let server_binary = sibling_binary("rounds-server")?;
    let client_binary = sibling_binary("rounds-client")?;
    let mut server = ChildGuard::new(
        Command::new(&server_binary)
            .args([
                "--port",
                "0",
                "--ticks",
                &ticks.to_string(),
                "--seed",
                &seed.to_string(),
                "--profile",
                profile.name(),
            ])
            .stdout(Stdio::piped())
            .stderr(Stdio::piped())
            .spawn()
            .map_err(|error| format!("start {}: {error}", server_binary.display()))?,
    );
    let server_stdout = server
        .child
        .stdout
        .take()
        .ok_or("server stdout was not captured")?;
    let mut server_reader = BufReader::new(server_stdout);
    let mut listening_line = String::new();
    server_reader
        .read_line(&mut listening_line)
        .map_err(|error| format!("read server address: {error}"))?;
    let listening: serde_json::Value = serde_json::from_str(listening_line.trim())
        .map_err(|error| format!("invalid server ready record: {error}"))?;
    let address = listening
        .get("address")
        .and_then(serde_json::Value::as_str)
        .ok_or("server ready record omitted address")?
        .to_owned();

    let mut clients = (0..2)
        .map(|client_id| {
            let mut command = Command::new(&client_binary);
            command.args([
                "remote",
                "--address",
                &address,
                "--client",
                &client_id.to_string(),
                "--ticks",
                &ticks.to_string(),
                "--seed",
                &seed.to_string(),
                "--profile",
                profile.name(),
            ]);
            if client_id == 0 {
                command
                    .arg("--render-output")
                    .arg(&live_frame)
                    .arg("--render-metadata")
                    .arg(&live_metadata);
            }
            command
                .stdout(Stdio::piped())
                .stderr(Stdio::piped())
                .spawn()
                .map(ChildGuard::new)
                .map_err(|error| format!("start client {client_id}: {error}"))
        })
        .collect::<Result<Vec<_>, _>>()?;

    let reports = clients
        .iter_mut()
        .enumerate()
        .map(|(client_id, client)| read_client_report(client_id, client))
        .collect::<Result<Vec<_>, _>>()?;

    let mut server_tail = String::new();
    server_reader
        .read_to_string(&mut server_tail)
        .map_err(|error| format!("read server report: {error}"))?;
    let server_status = server
        .wait()
        .map_err(|error| format!("wait for server: {error}"))?;
    let mut server_stderr = String::new();
    server
        .child
        .stderr
        .take()
        .ok_or("server stderr was not captured")?
        .read_to_string(&mut server_stderr)
        .map_err(|error| format!("read server stderr: {error}"))?;
    if !server_status.success() {
        return Err(format!("server failed: {}", server_stderr.trim()));
    }
    let server_report: ServerReport = serde_json::from_str(server_tail.trim())
        .map_err(|error| format!("decode server report: {error}"))?;
    let local_output = Command::new(&client_binary)
        .args([
            "local",
            "--ticks",
            &ticks.to_string(),
            "--seed",
            &seed.to_string(),
            "--profile",
            profile.name(),
        ])
        .output()
        .map_err(|error| format!("start local client-host: {error}"))?;
    if !local_output.status.success() {
        return Err(format!(
            "local client-host failed: {}",
            String::from_utf8_lossy(&local_output.stderr).trim()
        ));
    }
    let local_report: ServerReport = serde_json::from_slice(&local_output.stdout)
        .map_err(|error| format!("decode local client-host report: {error}"))?;
    let clients_agree = reports.len() == 2
        && reports[0].progressive_state_sha256 == reports[1].progressive_state_sha256
        && reports[0].final_report == reports[1].final_report
        && reports[0].final_report == server_report;
    let local_host_agrees = server_report == local_report;
    let render_metadata: serde_json::Value = serde_json::from_slice(
        &fs::read(&live_metadata)
            .map_err(|error| format!("read {}: {error}", live_metadata.display()))?,
    )
    .map_err(|error| format!("decode live render metadata: {error}"))?;
    let live_rendered_state_agrees = live_frame.is_file()
        && render_metadata.get("stateSha256").and_then(|v| v.as_str())
            == Some(server_report.state_hash.as_str())
        && render_metadata.get("liveClientId").and_then(|v| v.as_u64()) == Some(0);
    if !clients_agree || !local_host_agrees || !live_rendered_state_agrees {
        return Err(
            "authority, both streamed clients, local host, and live rendered snapshot did not agree"
                .to_owned(),
        );
    }
    let handshakes_complete = reports.iter().all(|report| report.handshake_complete);
    let progressive_explosion_transition_observed = reports.iter().all(|report| {
        report.observed_pre_explosion_constraints && report.observed_post_explosion_release
    });
    let both_clients_observed_same_flow =
        reports[0].observed_flow_phases == reports[1].observed_flow_phases;
    let both_clients_observed_source_terminal_state = reports
        .iter()
        .all(|report| report.observed_source_terminal_state);
    let both_clients_observed_rematch_reset =
        reports.iter().all(|report| report.observed_rematch_reset);
    let both_clients_observed_blue_fan_by_tick_960 = reports
        .iter()
        .all(|report| report.observed_blue_fan_by_tick_960);
    let flow_completed_with_source_loadouts =
        server_report.state.flow.as_ref().is_some_and(|flow| {
            (if ticks >= rounds_sim::CONNECTED_FIRST_ROUND_TICKS {
                flow.phase == FlowPhase::RoundBlue && flow.scores == [0, 1] && flow.halves == [1, 2]
            } else if ticks > rounds_sim::LEGACY_REMATCH_DRAFT_TICKS {
                flow.phase == FlowPhase::HalfOrange && flow.halves == [1, 1]
            } else {
                flow.phase == FlowPhase::ResumedCombat && flow.scores == [0, 0]
            }) && flow.loadouts == [vec![ItemId::Dazzle], vec![ItemId::ExplosiveBullet]]
        });
    let radial_saw_motion_observed = reports
        .iter()
        .all(|report| report.observed_radial_saw_motion);
    let radial_damage_observed = reports.iter().all(|report| report.observed_radial_damage);
    let radial_result_onset_observed = reports
        .iter()
        .all(|report| report.observed_radial_result_onset);
    let radial_half_blue_observed = reports
        .iter()
        .all(|report| report.observed_radial_half_blue);
    let yellow_calm_observed = reports.iter().all(|report| report.observed_yellow_calm);
    let yellow_terminal_blast_observed = reports
        .iter()
        .all(|report| report.observed_yellow_terminal_blast);
    let yellow_crate_motion_observed = reports
        .iter()
        .all(|report| report.observed_yellow_crate_motion);
    let yellow_result_onset_observed = reports
        .iter()
        .all(|report| report.observed_yellow_result_onset);
    let yellow_following_result_observed = reports
        .iter()
        .all(|report| report.observed_yellow_following_result);
    let yellow_round_orange_observed = reports
        .iter()
        .all(|report| report.observed_yellow_round_orange);
    if !handshakes_complete
        || (profile == ReplayProfile::TimberCollapseReplay
            && !progressive_explosion_transition_observed)
        || (profile == ReplayProfile::RematchDraftReplay
            && (!both_clients_observed_same_flow
                || !both_clients_observed_source_terminal_state
                || !both_clients_observed_rematch_reset
                || !both_clients_observed_blue_fan_by_tick_960
                || !flow_completed_with_source_loadouts
                || (ticks > rounds_sim::LEGACY_REMATCH_DRAFT_TICKS
                    && (!progressive_explosion_transition_observed
                        || !reports[0]
                            .observed_flow_phases
                            .contains(&FlowPhase::HalfBlue)
                        || !reports[0]
                            .observed_flow_phases
                            .contains(&FlowPhase::TimberCombat)))
                || (ticks >= rounds_sim::CONNECTED_FIRST_ROUND_TICKS
                    && !reports[0]
                        .observed_flow_phases
                        .contains(&FlowPhase::IceCombat))))
        || (profile == ReplayProfile::RadialSawHalfBlueReplay
            && (!radial_saw_motion_observed
                || !radial_damage_observed
                || !radial_result_onset_observed
                || !radial_half_blue_observed))
        || (profile == ReplayProfile::YellowCrateTerminalBlastReplay
            && (!yellow_calm_observed
                || !yellow_terminal_blast_observed
                || !yellow_crate_motion_observed
                || !yellow_result_onset_observed
                || !yellow_following_result_observed
                || !yellow_round_orange_observed))
        || reports
            .iter()
            .any(|report| report.snapshots_received != ticks)
    {
        return Err("stream did not complete every handshake and progressive snapshot".to_owned());
    }
    println!(
        "{}",
        serde_json::to_string(&SmokeEvidence {
            protocol: NETWORK_PROTOCOL,
            transport: "udp/ipv4-loopback",
            seed,
            ticks,
            clients: 2,
            handshakes_complete,
            first_input_sequence: reports[0].first_input_sequence,
            last_input_sequence: reports[0].last_input_sequence,
            snapshots_per_client: reports[0].snapshots_received,
            first_snapshot_tick: reports[0].first_snapshot_tick,
            last_snapshot_tick: reports[0].last_snapshot_tick,
            clients_agree,
            local_host_agrees,
            live_rendered_state_agrees,
            progressive_explosion_transition_observed,
            dynamic_body_digest: server_report.dynamic_body_digest,
            arena_digest: server_report.arena_digest,
            saw_digest: server_report.saw_digest,
            combat_digest: server_report.combat_digest,
            round_digest: server_report.round_digest,
            flow_digest: server_report.flow_digest,
            loadout_digest: server_report.loadout_digest,
            both_clients_observed_same_flow,
            both_clients_observed_source_terminal_state,
            both_clients_observed_rematch_reset,
            both_clients_observed_blue_fan_by_tick_960,
            observed_flow_phases: reports[0].observed_flow_phases.clone(),
            flow_completed_with_source_loadouts,
            radial_saw_motion_observed,
            radial_damage_observed,
            radial_result_onset_observed,
            radial_half_blue_observed,
            yellow_calm_observed,
            yellow_terminal_blast_observed,
            yellow_crate_motion_observed,
            yellow_result_onset_observed,
            yellow_following_result_observed,
            yellow_round_orange_observed,
            live_frame_path: slash_path(&live_frame),
            live_metadata_path: slash_path(&live_metadata),
            state_hash: server_report.state_hash,
        })
        .map_err(|error| error.to_string())?
    );
    Ok(())
}

fn read_client_report(
    client_id: usize,
    client: &mut ChildGuard,
) -> Result<ClientSessionReport, String> {
    let status = client
        .wait()
        .map_err(|error| format!("wait for client {client_id}: {error}"))?;
    let mut stdout = Vec::new();
    client
        .child
        .stdout
        .take()
        .ok_or_else(|| format!("client {client_id} stdout was not captured"))?
        .read_to_end(&mut stdout)
        .map_err(|error| format!("read client {client_id} stdout: {error}"))?;
    let mut stderr = Vec::new();
    client
        .child
        .stderr
        .take()
        .ok_or_else(|| format!("client {client_id} stderr was not captured"))?
        .read_to_end(&mut stderr)
        .map_err(|error| format!("read client {client_id} stderr: {error}"))?;
    if !status.success() {
        return Err(format!(
            "client {client_id} failed: {}",
            String::from_utf8_lossy(&stderr).trim()
        ));
    }
    serde_json::from_slice(&stdout)
        .map_err(|error| format!("decode client {client_id} report: {error}"))
}

fn inspect(arguments: &[String]) -> Result<(), String> {
    let ticks = argument(arguments, "--ticks", REPLAY_TICKS)?;
    let seed = argument(arguments, "--seed", 38_u64)?;
    let profile = argument(arguments, "--profile", ReplayProfile::default())?;
    let (state, state_hash) = run_profile_match(profile, seed, ticks);
    let report = ServerReport {
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
        flow_digest: state.flow.as_ref().map(rounds_sim::flow_digest),
        loadout_digest: state.flow.as_ref().map(rounds_sim::loadout_digest),
        state,
    };
    println!(
        "{}",
        serde_json::to_string(&report).map_err(|error| error.to_string())?
    );
    Ok(())
}

fn sibling_binary(name: &str) -> Result<PathBuf, String> {
    let executable = env::current_exe().map_err(|error| error.to_string())?;
    let path = executable
        .parent()
        .ok_or("automation executable has no parent directory")?
        .join(format!("{name}{}", env::consts::EXE_SUFFIX));
    if !path.is_file() {
        return Err(format!(
            "{} is missing; run cargo build --workspace before the smoke test",
            path.display()
        ));
    }
    Ok(path)
}

fn slash_path(path: &Path) -> String {
    path.to_string_lossy().replace('\\', "/")
}

fn optional_path_argument(arguments: &[String], name: &str) -> Option<PathBuf> {
    arguments
        .iter()
        .position(|argument| argument == name)
        .and_then(|index| arguments.get(index + 1))
        .map(PathBuf::from)
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

#[cfg(test)]
mod tests {
    use super::ChildGuard;
    use std::fs;
    use std::io;
    use std::net::UdpSocket;
    use std::process::{Command, Stdio};
    use std::thread;
    use std::time::Duration;

    const ADDRESS_FILE_ENV: &str = "ROUNDS_TEST_SERVER_ADDRESS_FILE";

    #[test]
    fn partial_child_start_failure_releases_owned_server() {
        let address_file = std::env::temp_dir().join(format!(
            "rounds-test-server-address-{}.txt",
            std::process::id()
        ));
        let missing_client = std::env::temp_dir().join(format!(
            "rounds-missing-client-{}{}",
            std::process::id(),
            std::env::consts::EXE_SUFFIX
        ));
        let _ = fs::remove_file(&address_file);
        let _ = fs::remove_file(&missing_client);

        let mut server_address = None;
        let result = (|| -> io::Result<()> {
            let child = Command::new(std::env::current_exe()?)
                .args(["--exact", "tests::udp_server_helper"])
                .env(ADDRESS_FILE_ENV, &address_file)
                .stdout(Stdio::null())
                .stderr(Stdio::null())
                .spawn()?;
            let _server = ChildGuard::new(child);

            for _ in 0..200 {
                if let Ok(address) = fs::read_to_string(&address_file) {
                    server_address = Some(address);
                    break;
                }
                thread::sleep(Duration::from_millis(10));
            }
            if server_address.is_none() {
                return Err(io::Error::new(
                    io::ErrorKind::TimedOut,
                    "test-owned server did not publish its address",
                ));
            }

            Command::new(&missing_client).spawn()?;
            Ok(())
        })();

        assert_eq!(result.unwrap_err().kind(), io::ErrorKind::NotFound);
        let address = server_address.expect("test-owned server published its address");
        UdpSocket::bind(address.trim()).expect("partial startup releases the owned server");
        fs::remove_file(address_file).expect("remove test-owned server address file");
    }

    #[test]
    fn mid_transition_failure_releases_all_owned_children() {
        let address_files = (0..3)
            .map(|index| {
                std::env::temp_dir().join(format!(
                    "rounds-transition-child-{}-{index}.txt",
                    std::process::id()
                ))
            })
            .collect::<Vec<_>>();
        for path in &address_files {
            let _ = fs::remove_file(path);
        }
        let mut addresses = Vec::new();
        let result = (|| -> io::Result<()> {
            let mut children = Vec::new();
            for path in &address_files {
                children.push(ChildGuard::new(
                    Command::new(std::env::current_exe()?)
                        .args(["--exact", "tests::udp_server_helper"])
                        .env(ADDRESS_FILE_ENV, path)
                        .stdout(Stdio::null())
                        .stderr(Stdio::null())
                        .spawn()?,
                ));
            }
            for path in &address_files {
                for _ in 0..200 {
                    if let Ok(address) = fs::read_to_string(path) {
                        addresses.push(address);
                        break;
                    }
                    thread::sleep(Duration::from_millis(10));
                }
            }
            if addresses.len() != children.len() {
                return Err(io::Error::new(
                    io::ErrorKind::TimedOut,
                    "not every transition child published its address",
                ));
            }
            Err(io::Error::other("induced result-transition failure"))
        })();

        assert_eq!(result.unwrap_err().kind(), io::ErrorKind::Other);
        assert_eq!(addresses.len(), 3);
        for address in addresses {
            UdpSocket::bind(address.trim()).expect("transition cleanup releases every child");
        }
        for path in address_files {
            fs::remove_file(path).expect("remove transition child address file");
        }
    }

    #[test]
    fn udp_server_helper() {
        let Some(address_file) = std::env::var_os(ADDRESS_FILE_ENV) else {
            return;
        };
        let socket = UdpSocket::bind("127.0.0.1:0").expect("bind test-owned UDP server");
        let pending_address_file =
            std::path::PathBuf::from(&address_file).with_extension("pending");
        fs::write(
            &pending_address_file,
            socket.local_addr().unwrap().to_string(),
        )
        .expect("publish test-owned server address");
        fs::rename(pending_address_file, address_file)
            .expect("make test-owned server address visible atomically");
        loop {
            thread::park();
        }
    }
}
