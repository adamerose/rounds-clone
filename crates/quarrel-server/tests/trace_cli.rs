use std::{fs, net::UdpSocket, path::PathBuf, process::Command};

#[test]
fn trace_path_is_validated_before_binding() {
    // Keeping this socket alive makes every invocation bounded, even before the fix.
    let occupied = UdpSocket::bind("127.0.0.1:0").unwrap();
    let port = occupied.local_addr().unwrap().port().to_string();
    let dir = std::env::temp_dir().join(format!(
        "quarrel-server-trace-{}-{port}",
        std::process::id()
    ));
    fs::create_dir(&dir).unwrap();
    let executable = std::env::var_os("QUARREL_TEST_SERVER")
        .map(PathBuf::from)
        .unwrap_or_else(|| env!("CARGO_BIN_EXE_quarrel-server").into());
    let mut errors = Vec::new();
    for trace_args in [
        vec![],
        vec!["--trace", "session trace.jsonl"],
        vec!["--trace"],
        vec!["--trace", "--headless"],
    ] {
        let result = Command::new(&executable)
            .current_dir(&dir)
            .args(["dedicated", "--bind", "127.0.0.1", "--port", &port])
            .args(&trace_args)
            .output()
            .expect("run hosting CLI");
        assert!(!result.status.success());
        assert!(
            result.stdout.is_empty(),
            "no listening event before validation"
        );
        assert_eq!(fs::read_dir(&dir).unwrap().count(), 0, "no trace file");
        errors.push(String::from_utf8(result.stderr).unwrap());
    }
    fs::remove_dir(&dir).unwrap();
    assert!(
        errors[0].starts_with("bind live authority:"),
        "{}",
        errors[0]
    );
    assert_eq!(errors[1], errors[0], "valid trace path reaches bind");
    for error in &errors[2..] {
        assert_eq!(error.trim(), "missing value for --trace");
    }
}
