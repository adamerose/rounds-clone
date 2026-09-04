use std::net::UdpSocket;
use std::process::Command;

#[test]
fn partial_client_start_failure_releases_the_server() {
    let missing_client = std::env::temp_dir().join(format!(
        "rounds-missing-client-{}{}",
        std::process::id(),
        std::env::consts::EXE_SUFFIX
    ));
    let _ = std::fs::remove_file(&missing_client);

    let output = Command::new(env!("CARGO_BIN_EXE_rounds-automation"))
        .args(["smoke", "--seed", "38", "--ticks", "1"])
        .env("ROUNDS_SMOKE_CLIENT_1_BINARY", &missing_client)
        .output()
        .expect("run smoke fault injection");

    assert!(!output.status.success());
    let stderr = String::from_utf8(output.stderr).expect("smoke error is UTF-8");
    assert!(stderr.contains("start client 1 for server"), "{stderr}");
    let address = stderr
        .split_once(" for server ")
        .and_then(|(_, tail)| tail.split_once(" pid "))
        .map(|(address, _)| address)
        .expect("smoke error identifies the server address");
    UdpSocket::bind(address).expect("failed smoke releases the authoritative server socket");
}
