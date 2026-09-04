use std::process::Command;

#[test]
fn capture_rejects_resolved_equivalent_destinations_before_writing() {
    let directory =
        std::env::temp_dir().join(format!("rounds-capture-alias-{}", std::process::id()));
    let _ = std::fs::remove_dir_all(&directory);
    std::fs::create_dir_all(&directory).expect("create capture test directory");
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
