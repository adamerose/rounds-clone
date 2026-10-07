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
    // A person has time to type an address after Host; this exceeded the old join window.
    std::thread::sleep(std::time::Duration::from_millis(5_300));
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
    // Each automation controller cancels after its observation window. The host's
    // close can reach the peer before the peer's final observation, so authority
    // silence is the supported disconnect result, not a bounded terminal handshake.
    for output in [&host, &join] {
        assert!(
            output.status.success()
                || String::from_utf8_lossy(&output.stderr).contains("live client authority_silent")
                || String::from_utf8_lossy(&output.stderr).starts_with("peer_left: client "),
            "unexpected menu failure: {}",
            String::from_utf8_lossy(&output.stderr)
        );
    }
    assert!(lines.iter().any(|line| line.contains("menuFirstFight")));
    assert!(String::from_utf8_lossy(&join.stdout).contains("menuFirstFight"));
}

fn test_client() -> std::path::PathBuf {
    std::env::var_os("QUARREL_TEST_CLIENT")
        .map(std::path::PathBuf::from)
        .unwrap_or_else(|| env!("CARGO_BIN_EXE_quarrel-client").into())
}

#[test]
fn interactive_host_startup_failure_closes_the_waiting_client() {
    let fixture = std::env::temp_dir().join(format!(
        "quarrel-81-startup-{}-{}",
        std::process::id(),
        std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .unwrap()
            .as_nanos()
    ));
    std::fs::create_dir_all(fixture.join("assets/cards")).unwrap();
    std::fs::write(fixture.join("assets/cards/broken.ron"), "{ invalid ron").unwrap();
    let mut child = Command::new(test_client())
        .args(["host", "--interactive", "--headless", "--ticks", "90"])
        .current_dir(&fixture)
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();
    let deadline = std::time::Instant::now() + std::time::Duration::from_secs(5);
    while child.try_wait().unwrap().is_none() && std::time::Instant::now() < deadline {
        std::thread::sleep(std::time::Duration::from_millis(10));
    }
    let finished = child.try_wait().unwrap().is_some();
    if !finished {
        child.kill().unwrap();
    }
    let output = child.wait_with_output().unwrap();
    std::fs::remove_dir_all(fixture).unwrap();
    assert!(
        finished,
        "local authority failure left an uncancellable headless wait"
    );
    assert!(!output.status.success());
    assert!(
        String::from_utf8_lossy(&output.stderr).contains("broken.ron"),
        "{}",
        String::from_utf8_lossy(&output.stderr)
    );
}

#[test]
fn headless_menu_join_waits_for_a_host_started_after_old_deadline() {
    let reservation = std::net::UdpSocket::bind("127.0.0.1:0").unwrap();
    let address = reservation.local_addr().unwrap();
    let mut join = Command::new(test_client())
        .args([
            "menu-start",
            "--choice",
            "2",
            "--address",
            &address.to_string(),
            "--headless",
            "--ticks",
            "90",
        ])
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();
    std::thread::sleep(std::time::Duration::from_millis(5_300));
    if join.try_wait().unwrap().is_some() {
        let output = join.wait_with_output().unwrap();
        panic!(
            "menu Join exited before Host started: {}",
            String::from_utf8_lossy(&output.stderr)
        );
    }
    drop(reservation);
    let host = Command::new(test_client())
        .args([
            "menu-start",
            "--choice",
            "1",
            "--headless",
            "--port",
            &address.port().to_string(),
            "--ticks",
            "90",
        ])
        .output()
        .unwrap();
    let join = join.wait_with_output().unwrap();
    for output in [&host, &join] {
        assert!(
            String::from_utf8_lossy(&output.stdout).contains("menuFirstFight"),
            "{}",
            String::from_utf8_lossy(&output.stderr)
        );
        assert!(
            output.status.success()
                || String::from_utf8_lossy(&output.stderr).contains("live client authority_silent")
                || String::from_utf8_lossy(&output.stderr).starts_with("peer_left: client "),
            "unexpected menu failure: {}",
            String::from_utf8_lossy(&output.stderr)
        );
    }
}

#[test]
fn bounded_host_client_waits_for_a_peer_joining_after_the_silence_interval() {
    let mut host = Command::new(test_client())
        .args(["host", "--headless", "--port", "0", "--ticks", "90"])
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .unwrap();
    let mut output = BufReader::new(host.stdout.take().unwrap());
    let mut listening = String::new();
    output.read_line(&mut listening).unwrap();
    let event: serde_json::Value = serde_json::from_str(&listening).expect("host listening event");
    let address = event["address"].as_str().unwrap().to_owned();
    // After the three-second running silence bound, inside the five-second join window.
    std::thread::sleep(std::time::Duration::from_millis(3_500));
    let join = Command::new(test_client())
        .args([
            "join",
            "--address",
            &address,
            "--client",
            "1",
            "--headless",
            "--ticks",
            "90",
        ])
        .output()
        .unwrap();
    let lines = output.lines().collect::<Result<Vec<_>, _>>().unwrap();
    let host = host.wait_with_output().unwrap();
    for output in [&host, &join] {
        assert!(
            output.status.success(),
            "{}",
            String::from_utf8_lossy(&output.stderr)
        );
    }
    let joined = String::from_utf8_lossy(&join.stdout).into_owned();
    // The host prints its client and authority reports; the joining peer prints its own.
    let reports = lines
        .iter()
        .map(String::as_str)
        .chain(joined.lines())
        .filter_map(|line| serde_json::from_str::<serde_json::Value>(line).ok())
        .filter_map(|report| report["stateHash"].as_str().map(str::to_owned))
        .collect::<Vec<_>>();
    assert_eq!(reports.len(), 3, "{reports:?}");
    assert!(
        reports.windows(2).all(|pair| pair[0] == pair[1]),
        "{reports:?}"
    );
}
