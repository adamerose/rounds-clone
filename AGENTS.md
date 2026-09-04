# Project rules

## Window placement

- Every visible application window launched for this project must open on monitor 4, the small 1920x1080 display at zero-based screen index 3.
- This includes the game, Godot editor and runtime, browsers, render previews, capture helpers, and any other GUI tool.
- Configure placement before showing a window.
- If a tool cannot choose its startup monitor, launch it hidden or minimized, move the exact verified window to monitor 4, and only then restore or show it.
- Never maximize or surface a project window on monitors 1 through 3.
- Before collecting native UI evidence, verify that the window center is on monitor 4.
- Headless and background-only processes are exempt.
- If placement fails, move or close the window immediately and record the failure in `docs/design-docs/postmortems.md`.

## Native Rust build resources

- Cargo must use the repository configuration in `.cargo/config.toml`: at most two concurrent jobs and the reusable ignored target at `out/cargo-target`. Do not override the job cap upward or launch concurrent Cargo builds.
- Reuse that prepared target across compatible test, lint, build, capture, and review commands. Start a clean target only when the Rust toolchain, lock file, feature set, target triple, artifact trust, or a frozen verification contract makes reuse invalid.
- Before every clean native Rust build, tell the user the exact target path, why reuse is invalid, the two-job cap, and the expected impact. The measured cold Bevy workspace precedent created about 16.9 GiB of artifacts and saturated disk; allow for roughly 17 GiB of temporary disk even with the safer concurrency cap.
- A clean build still runs the complete required verification. Resource safety changes scheduling and reuse, never the test, lint, build, capture, or review coverage.
