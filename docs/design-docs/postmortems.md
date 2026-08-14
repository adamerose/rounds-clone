# Postmortems

Append-only record of failures, stalls, broken tools, and surprising friction encountered while building the game.

## 2026-08-14 — The seed directory was not a Git repository

The initial workspace contained the founding design documents and `.gitignore`, but Git inspection failed because no `.git` directory existed.
This was expected work in `GOAL.md`, not data loss: the existing files were committed unchanged as baseline `b9073b6a9c110b5fbca5e242d49bd03a8cecef12`, then ticket work moved into a detached Ivy worktree.

## 2026-08-14 — Required game tooling is not yet installed

The machine has .NET SDK 10 and Node.js 22, but neither Godot nor FFmpeg was on `PATH` during the bootstrap audit.
The project targets .NET 8 and needs a pinned Godot .NET runtime plus a reproducible replay-video path, so tool provisioning and validation are explicit bootstrap work rather than ambient-machine assumptions.

## 2026-08-14 — The first ticket used an unsupported origin label

The Ivy ticket check rejected `origin: user-directed`; format 3 permits `human-request`, `agent-proposed`, or `system-detected`.
The ticket was corrected to `human-request`, which accurately describes Adam's explicit goal, and the check was rerun before implementation.

## 2026-08-14 — The first scaffold patch did not match generated project files

A single large `apply_patch` attempted to replace generated .NET templates and add the bootstrap implementation, but verification stopped at the first project file because the template's exact whitespace or byte prefix did not match the patch context.
The patch was atomic and changed nothing.
The retry deletes and recreates generated template files in smaller groups, leaving handwritten files on the required `apply_patch` path and making later failures easier to isolate.

## 2026-08-14 — The first solution build exposed two C# name-scope mistakes

The first full check built the simulation and tests but failed the harness because a nested type referenced a top-level local constant, and failed the Godot shell because `Sim` resolved to the enclosing `Rounds.Sim` namespace instead of the `Sim` type.
The harness default moved to a private constant on `Arguments`, and the engine call now names `Rounds.Sim.Sim.Step` explicitly.
These were compile-time failures with no runtime output or committed red state.

## 2026-08-14 — Godot initially ignored the project-local .NET SDK

The first headless editor launch used the GUI executable without a pinned `DOTNET_ROOT` and reported that the machine's .NET 10.0.11 runtime had no matching SDK, followed by a missing `Microsoft.Build` assembly.
Pointing `DOTNET_ROOT` and the front of `PATH` at `.tools/dotnet` made Godot discover SDK 8.0.423 and its MSBuild assemblies correctly.
The GUI executable also detached from PowerShell without useful output, so automation now selects the bundled `_console.exe` variant explicitly.
With those two changes, headless editor import and a three-frame runtime smoke both exit successfully.

## 2026-08-14 — `git mv` could not move an untracked ticket

Closing ticket 001 first tried `git mv`, but the newly created ticket had not yet been staged and Git correctly refused to move a path it did not track.
The following ticket check also failed because the closed-status file was still under `open/`.
PowerShell's `ErrorActionPreference` does not turn native nonzero exit codes into terminating errors, so later checks in that compound command still ran and passed; the ticket was moved with an explicit same-worktree `Move-Item`, then its check was rerun separately.

## 2026-08-14 — Generated files triggered line-ending normalization warnings

Staging the generated solution and NuGet lock files warned that their Windows CRLF working copies would be normalized to LF when Git next touched them.
The repository now declares LF for text and binary treatment for PNG concepts in `.gitattributes`, then renormalizes the candidate so the committed representation is explicit and stable across platforms.

## 2026-08-14 — Bootstrap candidate 0b91884 failed review

Fresh reviewer `codex:019ffeb9-c691-70c3-b458-78885d222233` requested changes to candidate `0b91884a9bbe5c0683519356dfa34136e6959bbb`.
The checker missed common forms of four prohibited APIs; Node, the CI runner/action, and the mutable .NET installer were not fully pinned; the architecture falsely said vanilla Rounds shipped 2v2; and the accepted draft concept reused the original `Burst` card name.
Correction replaces the checker and its tests with pinned-.NET projects, verifies exact download hashes, pins the CI runner generation and checkout commit, fixes the 1v1 rationale, and replaces the draft concept with five original choices.

## 2026-08-14 — Local `view_image` showed an incomplete copy despite identical bytes

The revised five-card concept rendered correctly from its generated-image source, but `view_image` showed only the empty top background when given the worktree copy.
SHA-256 hashes and byte lengths are identical (`13829567af2a9980a4f3667ef182ef126bf6322c01cfa57d7280118f06564d3f` and 2,229,360 bytes), so this is a local viewer/path anomaly rather than asset corruption.
The source image was visually inspected at original detail, and byte equality proves the committed project asset is that inspected image.

## 2026-08-14 — The abandoned installer remained in the ignored tool cache

Replacing the mutable .NET installer flow left `.tools/dotnet-install.ps1` from the first bootstrap attempt.
It was an ignored, reproducible download with no remaining caller and was removed permanently; the installed pinned SDK and Godot runtime remain intact.
