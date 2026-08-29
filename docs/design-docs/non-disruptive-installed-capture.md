# Non-disruptive installed-build capture boundary

Ticket 031 does not yet have an authorized route that can drive installed ROUNDS to a controlled state. This document defines the fail-closed evidence boundary that any future route must satisfy. The validator is headless: it reads one JSON manifest and never launches, activates, captures, or sends input to an application.

Run the validator after a candidate adapter has completed and removed its processes:

```powershell
dotnet run --project tools/Rounds.Checks/Rounds.Checks.csproj -- --capture-evidence <external-manifest.json>
```

The validator derives the candidate repository from its own executable layout, attests project identity, normalizes ordinary DOS, extended `\\?\` DOS, extended UNC, and safe DOS-device aliases into one filesystem namespace, resolves existing reparse points, and rejects raw media physically inside that repository. Local administrative-share aliases resolve back to their DOS drive; UNC shares that cannot be proven external and non-filesystem device namespaces fail closed instead of losing a prefix. The caller cannot supply or redirect the trusted repository root. The manifest and raw target media stay outside the repository. Duplicate JSON properties are rejected recursively before semantic validation. A passing result is necessary but is not, by itself, proof that the adapter's claims are true; review must bind every claim to independently collected logs and hashes.

## Required manifest sections

- `target` binds Steam app `1557740`, public build `21020021`, version `v1.1.2.a75ee335a`, executable hash, exact controlled state, loadout, and target process IDs.
- `isolation` accepts exactly `process-native-recording` with `target-owned-deterministic-command-channel`, or `existing-separate-gui-session` with `session-scoped-hardware-input`. Cross-paired, free-form, `SendInput`, `PostMessage`, WScript, `mouse_event`, global-input, and virtual-input routes fail. It also refuses foreground activation, physical-pointer movement, unrelated pixel inspection, and system-configuration changes.
- `display` binds screen index `3`, device `\\.\DISPLAY4`, a 1920x1080 monitor rectangle, the exact contained target-window rectangle, and proof that placement was verified before visibility.
- `limits` requires below-normal priority, one or two distinct logical processors, exactly one declared target process, zero or one declared capture writer, at most two GiB private and dedicated GPU memory, at most 70 percent GPU use, a bounded timeout, and automatic cleanup. Parsed limits are the resource validator's typed authority: each process affinity and their union stay inside the declared processor set; each process CPU stays within 100 percent per processor in that process's own valid affinity while aggregate CPU stays within 100 percent per globally declared processor; and each process and aggregate GPU/private-memory/dedicated-GPU-memory value stays within the corresponding declared cap as well as the hard ticket maximum. Invalid or empty process affinity creates no CPU capacity. Declared counts must equal the exact PID arrays, and the target and writer arrays must be disjoint.
- `capture` records the separate writer PID inventory, caps output at 1280x720, 60 fps, 20 seconds, refuses an existing output path, requires raw evidence outside the physically resolved repository, audits every frame timestamp with zero declared drops or duplicates, and binds labeled source-frame coordinates to the captured raster. Process-native recording requires zero separate writer PIDs.
- `heartbeat` retains all 20-millisecond observations for at least a 10-second baseline and the complete capture. Capture p95 may be no more than five milliseconds above baseline or 15 milliseconds absolute, and no capture delay may exceed 50 milliseconds.
- `resources` retains one-second baseline and capture samples for system CPU/GPU plus an exact per-PID inventory of role, priority, affinity, CPU, GPU-engine use, dedicated GPU memory, and private memory. Every capture sample must contain exactly the target/writer PID union, and its attributable totals must equal the per-process sums. Baseline samples require an empty attributable inventory. Limit violations fail validation.
- `cleanup` proves target exit, conditionally proves writer exit only when a writer was declared, records an exited-PID inventory exactly equal to the target/writer union, and requires no residual attributable process IDs.

## Current blocker

The installed Unity build has no proven target-owned command or automation channel for menu, card-draft, and duel input. Earlier WScript, `mouse_event`, `SendInput`, and `PostMessage` attempts did not drive its menu; those approaches also do not satisfy the global-input boundary. Renderer capture alone cannot reach two repeatable controlled states without an isolated input route.

Completing ticket 031 therefore needs one of the contract's currently unauthorized expansions: an already-available separate interactive GUI session that can be proven isolated, creation of a separate Windows account/session, enabling and configuring Windows Sandbox or another VM boundary, installing a virtual input boundary, or a scheduled foreground reference session. None is implied by this validator, and none may be enabled while the ticket remains under its present authority.
