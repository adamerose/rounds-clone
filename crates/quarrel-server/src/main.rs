use quarrel_network::{BoundServer, LiveServer};
use quarrel_sim::MatchConfig;
use std::env;

fn main() {
    if let Err(error) = run() {
        eprintln!("{error}");
        std::process::exit(1);
    }
}
fn run() -> Result<(), String> {
    let args = env::args().collect::<Vec<_>>();
    let port = argument(&args, "--port", 0_u16)?;
    let ticks = argument(&args, "--ticks", 2_400_u32)?;
    let config = config(&args)?;
    let bind = argument(&args, "--bind", "127.0.0.1".to_owned())?;
    if args.get(1).is_some_and(|mode| mode == "dedicated") {
        let trace = trace_path(&args)?;
        let server = LiveServer::bind(format!("{bind}:{port}"))?;
        println!(
            "{{\"event\":\"listening\",\"address\":\"{}\"}}",
            server.local_addr()?
        );
        println!(
            "{}",
            serde_json::to_string(&server.run(config, ticks, trace.as_deref())?)
                .map_err(|e| e.to_string())?
        );
        return Ok(());
    }
    let server = BoundServer::bind(format!("{bind}:{port}")).map_err(|e| e.to_string())?;
    println!(
        "{{\"event\":\"listening\",\"address\":\"{}\"}}",
        server.local_addr().map_err(|e| e.to_string())?
    );
    println!(
        "{}",
        serde_json::to_string(&server.run(config, ticks)?).map_err(|e| e.to_string())?
    );
    Ok(())
}
fn trace_path(args: &[String]) -> Result<Option<std::path::PathBuf>, String> {
    args.iter()
        .position(|arg| arg == "--trace")
        .map(|index| {
            args.get(index + 1)
                .filter(|value| !value.starts_with('-'))
                .map(std::path::PathBuf::from)
                .ok_or_else(|| "missing value for --trace".to_owned())
        })
        .transpose()
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
