use std::process::Command;

fn test_directory(name: &str) -> std::path::PathBuf {
    let directory = std::env::temp_dir().join(format!("rounds-{name}-{}", std::process::id()));
    let _ = std::fs::remove_dir_all(&directory);
    std::fs::create_dir_all(&directory).expect("create capture test directory");
    directory
}

#[test]
fn capture_rejects_resolved_equivalent_destinations_before_writing() {
    let directory = test_directory("capture-alias");
    let destination = directory.join("capture.png");

    let output = Command::new(env!("CARGO_BIN_EXE_rounds-client"))
        .current_dir(&directory)
        .args([
            "capture",
            "--seed",
            "38",
            "--ticks",
            "1",
            "--output",
            "capture.png",
            "--metadata",
        ])
        .arg(&destination)
        .output()
        .expect("run aliased capture");

    assert!(!output.status.success());
    assert!(
        String::from_utf8_lossy(&output.stderr)
            .contains("--output and --metadata must resolve to different paths")
    );
    assert!(
        !destination.exists(),
        "capture wrote before rejecting aliases"
    );
    std::fs::remove_dir_all(directory).expect("remove capture test directory");
}

#[test]
fn capture_replay_rejects_metadata_aliasing_an_anchor_before_writing() {
    let directory = test_directory("capture-replay-alias");
    let metadata = directory.join("0020-spawn.png");
    let output = Command::new(env!("CARGO_BIN_EXE_rounds-client"))
        .args([
            "capture-replay",
            "--seed",
            "38",
            "--ticks",
            "786",
            "--output-dir",
        ])
        .arg(&directory)
        .arg("--metadata")
        .arg(&metadata)
        .output()
        .expect("run replay with aliased metadata");

    assert!(!output.status.success());
    assert!(
        String::from_utf8_lossy(&output.stderr)
            .contains("--metadata and generated replay PNG must resolve to different paths")
    );
    assert_eq!(std::fs::read_dir(&directory).unwrap().count(), 0);
    std::fs::remove_dir_all(directory).expect("remove capture replay test directory");
}

#[test]
fn remote_render_rejects_aliased_destinations_before_network_or_writing() {
    let directory = test_directory("remote-render-alias");
    let destination = directory.join("live.png");
    let output = Command::new(env!("CARGO_BIN_EXE_rounds-client"))
        .args([
            "remote",
            "--address",
            "127.0.0.1:9",
            "--client",
            "0",
            "--ticks",
            "1",
            "--render-output",
        ])
        .arg(&destination)
        .arg("--render-metadata")
        .arg(&destination)
        .output()
        .expect("run remote render with aliased destinations");

    assert!(!output.status.success());
    assert!(
        String::from_utf8_lossy(&output.stderr)
            .contains("--render-output and --render-metadata must resolve to different paths")
    );
    assert!(!destination.exists());
    std::fs::remove_dir_all(directory).expect("remove remote render test directory");
}
