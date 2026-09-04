use rounds_network::BoundServer;
use std::env;

fn main() {
    if let Err(error) = run() {
        eprintln!("{error}");
        std::process::exit(1);
    }
}

fn run() -> Result<(), String> {
    let port = argument("--port", 0_u16)?;
    let ticks = argument("--ticks", 180_u32)?;
    let seed = argument("--seed", 38_u64)?;
    let server = BoundServer::bind(("127.0.0.1", port)).map_err(|error| error.to_string())?;
    let address = server.local_addr().map_err(|error| error.to_string())?;
    println!("{{\"event\":\"listening\",\"address\":\"{address}\"}}");
    let report = server.run(seed, ticks)?;
    println!(
        "{}",
        serde_json::to_string(&report).map_err(|error| error.to_string())?
    );
    Ok(())
}

fn argument<T>(name: &str, default: T) -> Result<T, String>
where
    T: std::str::FromStr,
    T::Err: std::fmt::Display,
{
    let arguments = env::args().collect::<Vec<_>>();
    let Some(index) = arguments.iter().position(|argument| argument == name) else {
        return Ok(default);
    };
    arguments
        .get(index + 1)
        .ok_or_else(|| format!("missing value for {name}"))?
        .parse()
        .map_err(|error| format!("invalid {name}: {error}"))
}
