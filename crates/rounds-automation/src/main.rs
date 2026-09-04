use rounds_network::{NETWORK_PROTOCOL, ServerReport};
use rounds_sim::run_scripted_match;
use serde::Serialize;
use std::env;
use std::io::{BufRead, BufReader, Read};
use std::path::PathBuf;
use std::process::{Command, Stdio};

#[derive(Serialize)]
struct SmokeEvidence {
    protocol: u16,
    transport: &'static str,
    seed: u64,
    ticks: u32,
    clients: u8,
    clients_agree: bool,
    local_host_agrees: bool,
    state_hash: String,
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
    let ticks = argument(arguments, "--ticks", 180_u32)?;
    let seed = argument(arguments, "--seed", 38_u64)?;
    let server_binary = sibling_binary("rounds-server")?;
    let client_binary = sibling_binary("rounds-client")?;
    let mut server = Command::new(&server_binary)
        .args([
            "--port",
            "0",
            "--ticks",
            &ticks.to_string(),
            "--seed",
            &seed.to_string(),
        ])
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .map_err(|error| format!("start {}: {error}", server_binary.display()))?;
    let server_stdout = server
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

    let clients = (0..2)
        .map(|client_id| {
            Command::new(&client_binary)
                .args([
                    "remote",
                    "--address",
                    &address,
                    "--client",
                    &client_id.to_string(),
                    "--ticks",
                    &ticks.to_string(),
                    "--seed",
                    &seed.to_string(),
                ])
                .stdout(Stdio::piped())
                .stderr(Stdio::piped())
                .spawn()
                .map_err(|error| format!("start client {client_id}: {error}"))
        })
        .collect::<Result<Vec<_>, _>>()?;

    let reports = clients
        .into_iter()
        .enumerate()
        .map(|(client_id, client)| {
            let output = client
                .wait_with_output()
                .map_err(|error| format!("wait for client {client_id}: {error}"))?;
            if !output.status.success() {
                return Err(format!(
                    "client {client_id} failed: {}",
                    String::from_utf8_lossy(&output.stderr).trim()
                ));
            }
            serde_json::from_slice::<ServerReport>(&output.stdout)
                .map_err(|error| format!("decode client {client_id} report: {error}"))
        })
        .collect::<Result<Vec<_>, _>>()?;

    let mut server_tail = String::new();
    server_reader
        .read_to_string(&mut server_tail)
        .map_err(|error| format!("read server report: {error}"))?;
    let server_output = server
        .wait_with_output()
        .map_err(|error| format!("wait for server: {error}"))?;
    if !server_output.status.success() {
        return Err(format!(
            "server failed: {}",
            String::from_utf8_lossy(&server_output.stderr).trim()
        ));
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
    let clients_agree =
        reports.len() == 2 && reports[0] == reports[1] && reports[0] == server_report;
    let local_host_agrees = server_report == local_report;
    if !clients_agree || !local_host_agrees {
        return Err("authoritative server, clients, and local host did not agree".to_owned());
    }
    println!(
        "{}",
        serde_json::to_string(&SmokeEvidence {
            protocol: NETWORK_PROTOCOL,
            transport: "udp/ipv4-loopback",
            seed,
            ticks,
            clients: 2,
            clients_agree,
            local_host_agrees,
            state_hash: server_report.state_hash,
        })
        .map_err(|error| error.to_string())?
    );
    Ok(())
}

fn inspect(arguments: &[String]) -> Result<(), String> {
    let ticks = argument(arguments, "--ticks", 180_u32)?;
    let seed = argument(arguments, "--seed", 38_u64)?;
    let (state, state_hash) = run_scripted_match(seed, ticks);
    let report = ServerReport {
        protocol: NETWORK_PROTOCOL,
        state_hash,
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
