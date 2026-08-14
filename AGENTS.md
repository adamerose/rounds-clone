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
