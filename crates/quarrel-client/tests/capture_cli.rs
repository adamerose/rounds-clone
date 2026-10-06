use sha2::{Digest, Sha256};
use std::{fs, path::PathBuf, process::Command};

fn fresh_directory() -> PathBuf {
    let nonce = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap()
        .as_nanos();
    let path = std::env::temp_dir().join(format!("quarrel-capture-{}-{nonce}", std::process::id()));
    fs::create_dir(&path).unwrap();
    path
}

#[test]
fn capture_renders_the_ordinary_opening_draft_with_provenance() {
    let dir = fresh_directory();
    let output = dir.join("draft.png");
    let metadata_path = dir.join("metadata.json");
    let executable = env!("CARGO_BIN_EXE_quarrel-client");
    let result = Command::new(executable)
        .args(["capture", "--seed", "38", "--ticks", "0", "--output"])
        .arg(&output)
        .arg("--metadata")
        .arg(&metadata_path)
        .output()
        .expect("run capture");
    assert!(
        result.status.success(),
        "{}",
        String::from_utf8_lossy(&result.stderr)
    );
    let metadata: serde_json::Value = serde_json::from_slice(&result.stdout).unwrap();
    let saved: serde_json::Value =
        serde_json::from_slice(&fs::read(&metadata_path).unwrap()).unwrap();
    let bytes = fs::read(&output).unwrap();
    fs::remove_dir_all(&dir).unwrap();
    assert_eq!(metadata, saved);
    assert_eq!(metadata["phase"], "Draft");
    assert_eq!(metadata["tick"], 0);
    assert_eq!(&bytes[..8], b"\x89PNG\r\n\x1a\n");
    assert_eq!(
        metadata["frameSha256"],
        format!("{:x}", Sha256::digest(&bytes))
    );
    assert_eq!(
        metadata["executableSha256"],
        format!("{:x}", Sha256::digest(fs::read(executable).unwrap()))
    );
}

#[test]
fn capture_and_remote_reject_output_aliases_before_rendering_or_connecting() {
    let dir = fresh_directory();
    let sentinel = dir.join("frame.png");
    fs::write(&sentinel, b"preserve this file").unwrap();
    for (mode, output_flag, metadata_flag) in [
        ("capture", "--output", "--metadata"),
        ("remote", "--render-output", "--render-metadata"),
    ] {
        let result = Command::new(env!("CARGO_BIN_EXE_quarrel-client"))
            .current_dir(&dir)
            .args([mode, output_flag, "frame.png", metadata_flag])
            .arg(&sentinel)
            .output()
            .unwrap();
        assert!(!result.status.success());
        assert!(
            String::from_utf8_lossy(&result.stderr).contains("must resolve to different paths")
        );
        assert_eq!(fs::read(&sentinel).unwrap(), b"preserve this file");
    }
    fs::remove_dir_all(&dir).unwrap();
}
