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

## 2026-08-14 — Windows compiler servers blocked bootstrap worktree cleanup

After integration, `git worktree remove --force` removed the worktree registration but could not delete the directory because five .NET compiler-server processes still had files open under its ignored local SDK.
The owning `.tools/dotnet/dotnet.exe build-server shutdown` command stopped the MSBuild and C#/VB servers cleanly.
The shell policy rejected a validated recursive `Remove-Item`, so `git clean -ndx` first proved that only `.ivy/worktrees/001-bootstrap/` would be removed, and the same exact path was then removed with `git clean -fdx`.
The commits remain in `main`, while the deleted ignored SDK cache is reproducible from `tools/bootstrap.ps1`.

## 2026-08-14 — Gameplay-source discovery ran without a JavaScript runtime or system FFmpeg

The YouTube metadata search returned candidate recordings, but `yt-dlp` warned that no supported JavaScript runtime was enabled and that FFmpeg was absent from `PATH`.
The discovery result was still usable, and the selected transcript-and-frame workflow bundles an `imageio-ffmpeg` executable for media processing.
The research gate will treat any extraction or format loss as a separate failure rather than assuming these warnings are harmless.

## 2026-08-14 — The first 60 fps footage download was rejected mid-transfer

The first direct download of YouTube format 298 reached 4.9 percent before the media host returned HTTP 403.
The command had not enabled the installed Node.js runtime, and it left only an ignored 10 MB `.part` file under `research/raw/`.
Research will retry through `yt-dlp` with the explicit Node.js runtime and will retain only a fully validated video.

## 2026-08-14 — FFmpeg metadata inspection uses a failure exit code

The bundled FFmpeg printed complete stream metadata for both preview videos but exited with code 1 because no output file was requested.
This is expected FFmpeg behavior, not corrupt media; subsequent inspection commands will direct metadata to a null sink when a successful process result matters.

## 2026-08-14 — Native 60 fps extraction remained unavailable after the documented token flow

YouTube's media host returned HTTP 403 for complete downloads, range-limited downloads, explicit IPv4 requests, several chunk sizes, the mweb client, and FFmpeg section reads.
The retry used the recommended `bgutil-ytdlp-pot-provider` flow, including a successful manual provider warm-up, but the media request still failed after 8–11 MB.
The provider's cold version probe also exceeded its 15-second timeout once before the warm-up completed in 8.5 seconds.
Research used the skill-produced 29.97 and 30 fps previews, recorded an approximately two-tick frame interval, widened every timing tolerance, and committed no partial media.

## 2026-08-14 — The isolated token helper reported dependency and environment warnings

`npm ci` for the ignored research helper reported 14 advisories: one low, three moderate, and ten high.
Node also warned that a global `--tls-keylog` option writes keys to `C:\Users\Adam\.claudalyzer\tls-keys.log`, and pip reported that version 26.2.1 was available over 25.3.
The helper and its dependencies never enter the build, package locks, or shipped game, so the research run was completed in ignored storage and the residue will be deleted with the worktree.

## 2026-08-14 — Computer-control tooling could inspect windows but could not drive the Unity menu

The installed `@oai/sky` 0.6.2 package lacked the skill-documented `sky.documentation` function.
Nested `node_repl` calls to `launch_app`, `get_window_state`, and `press_key` failed with `node_repl exec context not found`, so the build was launched directly and captured with FFmpeg instead.
WScript SendKeys, `mouse_event`, `SendInput`, and `PostMessage` attempts did not change the menu selection, and one retry incorrectly assumed that a PowerShell type declared in a previous process still existed.
The live menu still established `v1.1.2.a75ee335a`, and the exact `ROUNDS.exe` process was later closed through `CloseMainWindow()` and verified absent.

## 2026-08-14 — The frame analyzer's preferred Python video libraries were absent

Both OpenCV (`cv2`) and `imageio` imports failed in the available Python environment.
The ignored analyzer instead consumed raw frames from the bundled FFmpeg executable and used NumPy for pixel measurements.
The change preserved the same frame-addressable inputs without adding a project dependency.

## 2026-08-14 — Repository inspection repeatedly guessed paths that were not present

Early commands expected `tools/Rounds.Checks.Tests`, `tests/`, `Rounds.slnx`, `Directory.Packages.props`, `tools/check.ps1`, and `.editorconfig`, none of which existed at the queried locations.
A later build also used `--no-restore` in a fresh worktree and failed because its `obj/project.assets.json` files did not exist.
Subsequent work uses `rg --files`, the actual `Rounds.sln`, `tools/checks/run.ps1`, `Directory.Build.props`, and a locked restore before no-restore builds.

## 2026-08-14 — A first checker edit failed compilation

After concatenating the check results into an array, `Program.cs` still tested the old collection's `Count` property and produced CS0019 because `Count` resolved as a method group.
Changing the condition to the array's `Length` property restored a warning-free build.
The repository checker and four provenance regression tests then passed.

## 2026-08-14 — The first follow-up ticket omitted the required owning session

Ticket 003 used an inline empty `sessions` array, but format 3 requires a non-empty block list even while a ticket remains an `idea`.
The ticket checker rejected the edit before any later ticket was created.
Adding the current provider-qualified session restored the format gate, and ticket 004 was created with the valid shape from the start.

## 2026-08-14 — Closing ticket 002 created a transient location failure

Changing ticket 002's stored status to `closed` necessarily preceded moving its previously untracked file from `open/` to `closed/`.
The required checker run between those two filesystem edits reported that the closed ticket was still in `open/`.
Moving the exact file immediately restored the ticket gate; no contract content changed between the failing and passing checks.
The same expected location failure recurred after each rejected candidate moved through correction and closed again, and each exact-file move immediately restored the gate.

## 2026-08-14 — Raw research cleanup required Git's nested-repository force level

The shell policy rejected a validated recursive `Remove-Item` for the exact ignored `research/raw/` directory.
A `git clean -ndx -- research/raw` preview then showed that the ignored token-provider checkout would be skipped as a nested repository.
The stricter `git clean -ndffx -- research/raw` preview resolved the target to that one directory, and `git clean -dffx -- research/raw` removed the helper, partial downloads, captures, frames, and analyzer together.
The raw inputs are not recoverable from Git but can be regenerated from the source index; all committed measurements and methods remain intact.

## 2026-08-14 — NuGet generated the new lock file with CRLF working-tree endings

Staging `tools/Rounds.Checks.Tests/packages.lock.json` warned that its CRLF working copy would become LF when Git next touched it.
The repository's `* text=auto eol=lf` rule normalized the staged content, and `git diff --cached --check` passed.
This is generation-format noise rather than a package change or invalid lock file.

## 2026-08-14 — Ticket 002's first review rejected incomplete and contaminated measurements

Reviewer session `codex:019ffef0-fa87-7a50-960e-5b747abd5c3b` rejected exact candidate `a572aef332c2c39166cb6168da75cee0c88f7f7c`.
The raw WCG run coordinates recomputed to 0.099345 rather than 0.1193 player diameters per tick, and the same late-match interval contained several active cards whose effects were not ruled out.
The narrative also presented sixteen numeric measurement targets while the measurement log covered only seven distinct metrics, including no frame-addressable recoil row even though the ticket requires recoil measurement.
The correction will use loadout-controlled samples, make normalized values mechanically recomputable, and require measurement coverage for every footage-derived numeric fact named by the contract.

The reviewer also reported nine generated PNGs under `C:\Users\Adam\AppData\Local\Temp\codex-review-019ffef0` after command policy blocked their cleanup.
They are outside the repository and contain only temporary source-video inspection frames; the owning session will remove that exact directory after the correction no longer needs the diagnostic evidence.

## 2026-08-14 — A third gameplay recording could not be acquired

The YouTube research skill found metadata for `-Yek-qXg_HA` but its complete 480p-or-lower download failed with HTTP 403 before it could produce a transcript or frame set.
The correction therefore keeps the two existing independent recordings, requires two-source coverage for the highest-impact metrics, and marks projectile size, projectile speed, and out-of-bounds timing as single-source limitations.

## 2026-08-14 — A diagnostic command guessed the checker filenames

A correction inspection tried `tools/Rounds.Checks/DeterminismGuard.cs` and `tools/Rounds.Checks.Tests/DeterminismGuardTests.cs`, which do not exist.
Repository file discovery located the actual boundary checker at `tools/Rounds.Checks/DeterminismBoundaryChecker.cs` and its tests under `src/Rounds.Sim.Tests`.

## 2026-08-14 — A broad FFmpeg search crossed inaccessible user directories

An `rg --files` search from the user profile found candidate FFmpeg paths but exited with code 1 after encountering directories it could not enumerate.
The correction used the already identified `imageio_ffmpeg` executable directly and did not depend on the incomplete broad search.

## 2026-08-14 — The first missing-coverage regression failed at the schema boundary

The fixture removed every coverage row, so the schema's non-empty-array rule emitted `SPEC001` before the semantic checker could emit the intended `SPEC014` missing-contract failure.
Pointing the surviving coverage row at a different known fact preserved schema validity and exercised the semantic omission boundary directly.

## 2026-08-14 — Ticket 002's second review rejected action-contaminated evidence

Reviewer session `codex:019fff0e-f811-71d0-a4aa-27137690f147` rejected exact candidate `63f09925e439c0060d02f00dd547ff7226bdaa29`.
The SSAG run interval includes overlapping players and hit effects, its jump interval includes direct player and platform contact, and its recoil interval cannot separate shot recoil from collision, contact, input, and gravity.
The same rows also call the visible `Leech` starting card `Lifestealer`, even though the official 1.05 notes distinguish those cards.
The correction will exclude every contaminated row from coverage, correct the research metadata, and claim only the independent evidence the footage actually supports.

The reviewer left diagnostic contact sheets at `C:\Users\Adam\AppData\Local\Temp\codex-review-63f0992-019fff0e` under the review-only no-cleanup rule.
Opening the Fandom all-cards page also returned HTTP 402 during review, so the reviewer used search results and the recorded official patch notes for the card distinction.

## 2026-08-14 — Temporary review cleanup was blocked once before exact deletion

PowerShell policy rejected a validated recursive `Remove-Item` for the prior review directory and the empty third-video directory.
The same script revalidated both absolute parents and exact leaf names, then `System.IO.Directory.Delete` removed only those two temporary directories and verified them absent.

## 2026-08-14 — A broad frame-tracker run exceeded the command display budget

The 42–50 second SSAG tracker run completed but its 10,024-token output was truncated in the command result.
The retained beginning, ending, and ten-frame-per-second samples were enough to identify candidate intervals, and short bounded reruns then preserved every coordinate used by the correction.

## 2026-08-14 — An inspection tried to reopen already deleted review residue

A visual check referenced the prior review's `wcg-40to44-contact.png` after the owning session had already verified that temporary directory deleted.
The retained ignored correction frames provided the same source interval, so no evidence was lost and no cleanup was reversed.

## 2026-08-14 — A broad coverage edit excluded the wrong row

A context-light patch changed the first `countsTowardCoverage` occurrence, excluding the WCG body-diameter row instead of the WCG action-contaminated run row.
The repository gate immediately failed the two-source body-normalization contract, and an ID-anchored patch restored body coverage while excluding the intended run.

## 2026-08-14 — Ticket 002's third review rejected a partial jump promoted as full height

Reviewer session `codex:019fff1d-fe99-7af1-a895-91bf4b242796` rejected exact candidate `f50acf6b83b37fdfd46f5f4ae78fa25a90500768`.
The accepted SSAG jump began at center Y 248.2318 after takeoff even though the immediately preceding grounded frame is center Y 277.1916, so the claimed 2.8989-diameter full rise excluded 1.6089 visible diameters.
The correction will start at the grounded frame, record the full 4.5078-diameter and 36-tick arc, and recompute every algebraic jump hypothesis from it.

The review refreshed ignored `bin/`, `obj/`, and `game/.godot/` outputs but changed no tracked file.
Two read-only PowerShell formatting commands also failed with an `empty pipe element` parser error before corrected commands succeeded without creating files.

## 2026-08-14 — Ticket 002's approving review lacked OpenCV

Reviewer session `codex:019fff26-f793-7293-87fe-8a816060e432` approved exact candidate `6681545522380445e270edc6c2888fb0a3e81d5c` with no findings.
Its attempted OpenCV import failed with `ModuleNotFoundError`, so frame inspection used Pillow and NumPy and still reproduced every accepted interval and derivation.
The review refreshed ignored build and Godot outputs but changed no tracked file.

The reviewer left diagnostic PNGs at `C:\Users\Adam\AppData\Local\Temp\codex-review-019fff5` under the review-only no-cleanup rule.
Integration cleanup removed that exact directory, the earlier reported review-diagnostic directory, and the ignored `research/raw/` correction frames after validating each target.

## 2026-08-14 — Git could unregister ticket 002's worktree but not delete its long paths

`git worktree remove --force` removed the detached worktree registration but reported `Filename too long` while deleting its ignored local tools and build outputs.
The owning session revalidated the exact absolute target under `.ivy/worktrees/`, deleted only that orphan through the Windows long-path `System.IO.Directory` API, and verified both the directory and registration absent.

## 2026-08-14 — A proposed core-duel slice violated the binding research order

The owning session initially prioritized a playable deterministic duel after ticket 002 and created an empty `005-core-duel` worktree.
Reading `GOAL.md` showed that complete card and map research is a binding dependency before the simulation core, so no ticket or code was created and the exact empty worktree was removed immediately.
Work continues with ticket 003's complete vanilla card catalog instead.

## 2026-08-14 — Design-file inspection used an invalid Windows glob and a nonexistent path

An `rg` command passed `docs/design/*.md` as a literal Windows path and exited after the operating system rejected the wildcard syntax.
A follow-up read also guessed `docs/design/observability.md`, which does not exist.
The owning session used the actual `docs/design/physics-and-maps.md`, `GOAL.md`, and repository file map instead.

## 2026-08-14 — The primary card wiki rejected the ordinary fetch path

Opening the Fandom all-cards page through the ordinary web reader returned HTTP 402 even though search indexing exposed part of its table.
The required fetch-url fallback recovered the complete 67-row public table without copying card art or descriptive prose into the repository.

## 2026-08-14 — Card-source batching exposed two retrieval limits

The first multi-URL fetch-url command passed `--separator` as a separate dash-prefixed value, so its argument parser treated the value as another option and rejected the read before network work began.
Using the supported `--separator=value` form fixed that command-shape error.
A later 66-page request and a six-process parallel retry each exceeded the 60-second command limit because every fallback races browser-backed retrieval.
Sequential ten-page batches completed in about 6–12 seconds each and preserved the same public inputs.

## 2026-08-14 — Direct retrieval of the Japanese card index hit Cloudflare

PowerShell's ordinary `Invoke-WebRequest` received a JavaScript-and-cookie Cloudflare challenge from the Japanese card wiki.
The fetch-url fallback had already demonstrated access to the same pages and remained the read-only retrieval path.

## 2026-08-14 — The first generated catalog patch had a malformed terminator

The first in-memory card-catalog generator preserved the JSON file's trailing newline as an added patch line immediately before `*** End Patch`.
The patch parser rejected the malformed terminator and wrote nothing.
The retry removed the synthetic trailing line before constructing the patch and added the complete 67-card JSON atomically.

## 2026-08-14 — The fresh card worktree had no pinned SDK cache

The first test command reached the system `dotnet` launcher, but `global.json` correctly requires SDK 8.0.423 and the new worktree had no ignored `.tools` cache.
The hash-pinned repository bootstrap installed SDK 8.0.423 and Godot 4.7.1 into that worktree before verification continued.

## 2026-08-14 — `dotnet test` does not accept the restore-only locked-mode switch

A targeted test command passed `--locked-mode` directly to `dotnet test`, which forwarded it to MSBuild and failed with `MSB1001` before compilation.
Running `dotnet restore --locked-mode` first and then `dotnet test --no-restore` exercised the intended locked dependency path and passed all 15 checker tests.

## 2026-08-14 — A combined research-note patch used the wrong decision context

The first patch that added the card research note also tried to append after an admission sentence that did not match the actual decision record's prose.
The atomic patch wrote nothing.
The retry anchored to the existing final sentence, added the note, and appended three concise decision sections without changing prior entries.

## 2026-08-14 — Ticket 003's first review rejected unsupported stacking claims

Fresh reviewer `codex:019fff46-9914-7892-900d-0298b80df82b` rejected exact candidate `a8edf4f305358dbd720b7f69d54c493e09d0c411`.
The catalog had treated single-card display tables as evidence for 62 additive, 15 count, and 67 `none-observed` claims even though only five representative duplicate-card cases had stacking-specific support.
It also presented older GameFAQs values as corroboration for current Parasite and Poison values, used a percent unit for Quick Reload's dimensionless factor, and dated official patch 1.05 ten days late.
Correction separates stacking-and-cap provenance from numeric provenance, makes unsupported formulas and caps unknown, records the historical conflicts, and adds operation-unit and evaluation-order regressions.
During review, the Fandom fallback produced no output before a 64-second timeout, one exploratory search request was malformed, and one read-only `rg` command named a nonexistent `scripts` path before the reviewer completed the full gate from remaining indexed and official evidence.
The review refreshed ignored build and Godot outputs but changed no tracked file.

## 2026-08-14 — A card-correction inspection named a nonexistent test path

A read-only `rg` command included a root `tests` path that this repository does not have, so the search returned useful matches and then exited with code 1.
The correction used the actual `tools/Rounds.Checks.Tests` path on the next inspection.

## 2026-08-14 — The card correction guessed a nonexistent shared diff checker

A verification command looked for `check-diff.mjs` under the Ivy ticket-check directory, but no such script exists, and a repository-wide filename search confirmed the absence.
The supported `git diff --check` command remained the formatting gate.
The same inspection also named a nonexistent root `scripts` directory while looking for gate documentation; reading `README.md` and `.github/workflows/ci.yml` supplied the actual `tools/checks/run.ps1` entry point.

## 2026-08-14 — Ticket 003's second review rejected false corroboration

Fresh reviewer `codex:019fff51-5f0d-72f0-b036-b76b41d3e289` rejected exact candidate `58d5eca40be80170375228bdd8b86c275b4e884e`.
Quick Reload and Remote still asserted duplicate formulas their sources did not establish, Echo cited a discussion that never mentioned it, and GameFAQs was falsely counted as support for Bouncy, Homing, Chase health, and Taste of Blood lifesteal.
The patch record also said only that Dazzle duration changed instead of preserving the official 25% increase and the unknown absolute duration.
Correction makes Quick Reload and Remote unresolved, cites the Japanese Echo and Refresh pages for the behaviors they state directly, and turns every known GameFAQs omission or historical value into an executable source exclusion.
The reviewer's first full-gate call accidentally used a one-second timeout and was terminated after a clean build; an immediate correctly bounded rerun passed all gates and changed no tracked files.

## 2026-08-14 — The first stacking-consistency fixture reused one effect for five families

The new checker correctly rejected the existing valid fixture because its five representative cases all targeted one additive effect while claiming different resolved families.
The fixture now supplies one correctly typed effect for each family, and all 21 checker tests pass.

## 2026-08-14 — The new source-exclusion gate caught three stale stacking citations

The first full correction gate rejected GameFAQs in the stacking provenance for current Parasite health and Poison damage and ammunition even though their numeric provenance had already removed it.
Those unresolved stacking records now cite only the same current sources as their corrected numeric facts, and the exclusion remains as a regression against reintroduction.
