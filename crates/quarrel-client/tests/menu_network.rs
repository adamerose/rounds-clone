use std::{
    io::{BufRead, BufReader},
    process::{Command, Stdio},
};

#[test]
fn headless_host_and_join_menu_choices_reach_first_fight() {
    let executable = test_client();
    let mut host = Command::new(&executable)
        .args([
            "menu-start",
            "--choice",
            "1",
            "--headless",
            "--port",
            "0",
            "--ticks",
            "90",
        ])
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();
    let mut output = BufReader::new(host.stdout.take().unwrap());
    let mut listening = String::new();
    output.read_line(&mut listening).unwrap();
    let event: serde_json::Value = serde_json::from_str(&listening).expect("host listening event");
    let port = event["address"]
        .as_str()
        .unwrap()
        .rsplit(':')
        .next()
        .unwrap();
    let peer = format!("127.0.0.1:{port}");
    let join = Command::new(&executable)
        .args([
            "menu-start",
            "--choice",
            "2",
            "--address",
            &peer,
            "--headless",
            "--ticks",
            "90",
        ])
        .output()
        .unwrap();
    let lines = output.lines().collect::<Result<Vec<_>, _>>().unwrap();
    let host = host.wait_with_output().unwrap();
    assert!(
        host.status.success(),
        "{}",
        String::from_utf8_lossy(&host.stderr)
    );
    assert!(
        join.status.success(),
        "{}",
        String::from_utf8_lossy(&join.stderr)
    );
    assert!(lines.iter().any(|line| line.contains("menuFirstFight")));
    assert!(String::from_utf8_lossy(&join.stdout).contains("menuFirstFight"));
}

fn test_client() -> std::path::PathBuf {
    std::env::var_os("QUARREL_TEST_CLIENT")
        .map(std::path::PathBuf::from)
        .unwrap_or_else(|| env!("CARGO_BIN_EXE_quarrel-client").into())
}
