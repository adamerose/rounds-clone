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
    fs::write(&output, b"old image").unwrap();
    fs::write(&metadata_path, b"old metadata").unwrap();
    let executable = test_client();
    let result = Command::new(&executable)
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
        format!("{:x}", Sha256::digest(fs::read(&executable).unwrap()))
    );
}

#[test]
fn detailed_capture_supports_draft_and_practice_off_on() {
    let dir = fresh_directory();
    let draft = dir.join("draft-detailed.png");
    let practice_off = dir.join("practice-off.png");
    let practice_on = dir.join("practice-on.png");
    for args in [
        vec!["capture", "--seed", "38", "--ticks", "0", "--detailed"],
        vec!["capture", "--practice", "--seed", "38", "--ticks", "0"],
        vec![
            "capture",
            "--practice",
            "--seed",
            "38",
            "--ticks",
            "0",
            "--detailed",
        ],
    ] {
        let output = match args.as_slice() {
            ["capture", "--seed", ..] => &draft,
            ["capture", "--practice", .., "--detailed"] => &practice_on,
            _ => &practice_off,
        };
        let result = Command::new(test_client())
            .args(args)
            .arg("--output")
            .arg(output)
            .output()
            .unwrap();
        assert!(
            result.status.success(),
            "{}",
            String::from_utf8_lossy(&result.stderr)
        );
    }
    assert_ne!(
        fs::read(&practice_off).unwrap(),
        fs::read(&practice_on).unwrap()
    );
    fs::remove_dir_all(dir).unwrap();
}

#[test]
fn leading_flags_default_to_headless_target_practice() {
    let result = Command::new(test_client())
        .args(["--headless", "--ticks", "1"])
        .output()
        .unwrap();
    assert!(
        result.status.success(),
        "{}",
        String::from_utf8_lossy(&result.stderr)
    );
    let state: serde_json::Value = serde_json::from_slice(&result.stdout).unwrap();
    assert_eq!(state["flow"]["phase"], "Combat");
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
        let result = Command::new(test_client())
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

#[test]
fn capture_and_remote_reject_directory_destinations_before_work() {
    let dir = fresh_directory();
    let directory = dir.join("destination");
    fs::create_dir(&directory).unwrap();
    let sentinel = directory.join("sentinel");
    fs::write(&sentinel, b"preserve directory contents").unwrap();
    let paired = dir.join("paired");
    for (mode, output_flag, metadata_flag) in [
        ("capture", "--output", "--metadata"),
        ("remote", "--render-output", "--render-metadata"),
    ] {
        for directory_flag in [metadata_flag, output_flag] {
            for existing_pair in [false, true] {
                if existing_pair {
                    fs::write(&paired, b"preserve paired file").unwrap();
                }
                let (output, metadata) = if directory_flag == output_flag {
                    (&directory, &paired)
                } else {
                    (&paired, &directory)
                };
                // No remote address: even a regression cannot start network work.
                let result = Command::new(test_client())
                    .args([mode, "--seed", "38", "--ticks", "0", output_flag])
                    .arg(output)
                    .arg(metadata_flag)
                    .arg(metadata)
                    .output()
                    .unwrap();
                assert!(!result.status.success());
                let error = String::from_utf8_lossy(&result.stderr);
                assert!(
                    error.contains(&format!("{directory_flag} destination is a directory:")),
                    "{mode}: {error}"
                );
                assert!(directory.is_dir());
                assert_eq!(fs::read(&sentinel).unwrap(), b"preserve directory contents");
                assert_eq!(fs::read_dir(&directory).unwrap().count(), 1);
                if existing_pair {
                    assert_eq!(fs::read(&paired).unwrap(), b"preserve paired file");
                    fs::remove_file(&paired).unwrap();
                } else {
                    assert!(!paired.exists());
                }
            }
        }
    }
    fs::remove_dir_all(dir).unwrap();
}

fn test_client() -> std::path::PathBuf {
    std::env::var_os("QUARREL_TEST_CLIENT")
        .map(std::path::PathBuf::from)
        .unwrap_or_else(|| env!("CARGO_BIN_EXE_quarrel-client").into())
}
