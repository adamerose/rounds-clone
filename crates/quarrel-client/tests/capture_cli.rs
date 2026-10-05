use std::process::Command;

#[test]
fn capture_renders_the_ordinary_opening_draft() {
    let output =
        std::env::temp_dir().join(format!("quarrel-opening-draft-{}.png", std::process::id()));
    let result = Command::new(env!("CARGO_BIN_EXE_quarrel-client"))
        .args(["capture", "--seed", "38", "--ticks", "0", "--output"])
        .arg(&output)
        .output()
        .expect("run capture");
    assert!(
        result.status.success(),
        "{}",
        String::from_utf8_lossy(&result.stderr)
    );
    let metadata: serde_json::Value =
        serde_json::from_slice(&result.stdout).expect("capture metadata");
    assert_eq!(metadata["phase"], "Draft");
    assert_eq!(metadata["tick"], 0);
    let bytes = std::fs::read(&output).expect("rendered PNG");
    assert_eq!(&bytes[..8], b"\x89PNG\r\n\x1a\n");
    let _ = std::fs::remove_file(output);
}
