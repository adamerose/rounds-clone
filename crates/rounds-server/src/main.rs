use rounds_network::{BoundServer, LiveServer};
use rounds_sim::{REPLAY_TICKS, ReplayProfile};
use std::env;
use std::net::SocketAddr;
use std::path::PathBuf;

fn main() {
    if let Err(error) = run() {
        eprintln!("{error}");
        std::process::exit(1);
    }
}

fn run() -> Result<(), String> {
    let arguments = env::args().collect::<Vec<_>>();
    let mode = arguments
        .get(1)
        .filter(|argument| !argument.starts_with("--"))
        .map(String::as_str)
        .unwrap_or("scripted");
    match mode {
        "dedicated" => dedicated(&arguments),
        "scripted" => scripted(&arguments),
        _ => Err("usage: rounds-server [dedicated|scripted] [options]".to_owned()),
    }
}

fn dedicated(arguments: &[String]) -> Result<(), String> {
    let bind = argument(arguments, "--bind", "127.0.0.1".to_owned())?;
    let port = argument(arguments, "--port", 0_u16)?;
    let ticks = argument(arguments, "--ticks", REPLAY_TICKS)?;
    let seed = argument(arguments, "--seed", 38_u64)?;
    let profile = argument(arguments, "--profile", ReplayProfile::default())?;
    let trace = optional_path_argument(arguments, "--trace");
    let address = socket_address(&bind, port)?;
    let server = LiveServer::bind(address)?;
    let address = server.local_addr()?;
    println!("{{\"event\":\"listening\",\"address\":\"{address}\"}}");
    let report = server.run(seed, ticks, profile, trace.as_deref())?;
    println!(
        "{}",
        serde_json::to_string(&report).map_err(|error| error.to_string())?
    );
    if report.result == "completed" {
        Ok(())
    } else {
        Err(format!("live authority {}", report.result))
    }
}

fn scripted(arguments: &[String]) -> Result<(), String> {
    let port = argument(arguments, "--port", 0_u16)?;
    let ticks = argument(arguments, "--ticks", REPLAY_TICKS)?;
    let seed = argument(arguments, "--seed", 38_u64)?;
    let profile = argument(arguments, "--profile", ReplayProfile::default())?;
    let server = BoundServer::bind(("127.0.0.1", port)).map_err(|error| error.to_string())?;
    let address = server.local_addr().map_err(|error| error.to_string())?;
    println!("{{\"event\":\"listening\",\"address\":\"{address}\"}}");
    let report = server.run(seed, ticks, profile)?;
    println!(
        "{}",
        serde_json::to_string(&report).map_err(|error| error.to_string())?
    );
    Ok(())
}

fn socket_address(bind: &str, port: u16) -> Result<SocketAddr, String> {
    format!("{bind}:{port}")
        .parse()
        .map_err(|error| format!("invalid --bind {bind}: {error}"))
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

fn optional_path_argument(arguments: &[String], name: &str) -> Option<PathBuf> {
    arguments
        .iter()
        .position(|argument| argument == name)
        .and_then(|index| arguments.get(index + 1))
        .map(PathBuf::from)
}
