use quarrel_network::{BoundServer, send_inputs};
use quarrel_sim::{AuthoritativeMatch, MatchConfig, automated_input, hash_snapshot};
use serde::Serialize;
use std::env;
use std::thread;

#[derive(Serialize)]
struct Evidence {
    seed: u64,
    ticks: u32,
    opening_draft: bool,
    loser_draft: bool,
    match_end: bool,
    state_hash: String,
    clients_agree: bool,
}
fn main() {
    if let Err(error) = run() {
        eprintln!("{error}");
        std::process::exit(1);
    }
}
fn run() -> Result<(), String> {
    let args = env::args().collect::<Vec<_>>();
    let ticks = argument(&args, "--ticks", 2_400)?;
    let config = MatchConfig {
        seed: argument(&args, "--seed", 38)?,
        target_score: argument(&args, "--target-score", 2)?,
        ..Default::default()
    };
    let mut game = AuthoritativeMatch::with_config(config.clone())?;
    let mut opening = false;
    let mut loser = false;
    let mut end = false;
    for _ in 0..ticks {
        let state = game.snapshot();
        let flow = state.flow.as_ref().ok_or("match snapshot lacks flow")?;
        opening |= flow.phase == quarrel_sim::FlowPhase::Draft && flow.fight_number == 0;
        loser |= flow.phase == quarrel_sim::FlowPhase::Draft && flow.fight_number > 0;
        end |= flow.phase == quarrel_sim::FlowPhase::MatchEnd;
        let inputs = (0..config.fighter_count)
            .map(|id| {
                automated_input(id as u8, &state)
                    .with_progressive_observation(id as u8, Some(&state))
            })
            .collect::<Vec<_>>();
        game.step(&inputs);
    }
    let state = game.snapshot();
    let scripts = scripts(config.clone(), ticks)?;
    let server = BoundServer::bind("127.0.0.1:0").map_err(|error| error.to_string())?;
    let address = server.local_addr().map_err(|error| error.to_string())?;
    let authority = thread::spawn({
        let config = config.clone();
        move || server.run(config, ticks)
    });
    let clients = scripts
        .into_iter()
        .enumerate()
        .map(|(id, input)| {
            let config = config.clone();
            thread::spawn(move || send_inputs(address, id as u8, config, &input))
        })
        .collect::<Vec<_>>();
    let reports = clients
        .into_iter()
        .map(|client| {
            client
                .join()
                .map_err(|_| "smoke client panicked".to_owned())?
        })
        .collect::<Result<Vec<_>, _>>()?;
    let authority = authority
        .join()
        .map_err(|_| "smoke authority panicked".to_owned())??;
    let clients_agree = reports.iter().all(|report| {
        report.final_report.state_hash == authority.state_hash
            && report.final_report.state == authority.state
            && report.observed_opening_draft
            && report.observed_loser_draft
    }) && authority.state == state;
    println!(
        "{}",
        serde_json::to_string(&Evidence {
            seed: config.seed,
            ticks,
            opening_draft: opening,
            loser_draft: loser,
            match_end: end,
            state_hash: hash_snapshot(&state),
            clients_agree
        })
        .map_err(|e| e.to_string())?
    );
    if opening && loser && clients_agree {
        Ok(())
    } else {
        Err("ordinary smoke did not reach expected drafts or clients disagreed".into())
    }
}
fn scripts(config: MatchConfig, ticks: u32) -> Result<Vec<Vec<quarrel_sim::PlayerInput>>, String> {
    let mut game = AuthoritativeMatch::with_config(config.clone())?;
    let mut by_player = vec![Vec::with_capacity(ticks as usize); config.fighter_count];
    for _ in 0..ticks {
        let state = game.snapshot();
        let inputs = (0..config.fighter_count)
            .map(|id| {
                automated_input(id as u8, &state)
                    .with_progressive_observation(id as u8, Some(&state))
            })
            .collect::<Vec<_>>();
        for (id, input) in inputs.iter().copied().enumerate() {
            by_player[id].push(input);
        }
        game.step(&inputs);
    }
    Ok(by_player)
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
