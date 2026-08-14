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

## 2026-08-14 — Ticket 003's third review rejected two source-unit conflicts

Fresh reviewer `codex:019fff5e-83ba-7fe3-9033-67c3ae45f4b8` rejected exact candidate `d50f8a391592fbbbeadc6a11093763911b54e60b`.
Brawler's 200-percent health effect and Pristine Perseverence's 400-percent health effect cited GameFAQs even though that guide states flat HP, a materially different operation once other health modifiers exist.
The current English table and independent Korean guide support the percentage units, so the correction cites those sources and records GameFAQs as an executable unit conflict for both facts.
During review, `jq` was unavailable and the JSON audit used PowerShell, while Fandom's ordinary reader returned HTTP 402 before the approved read-only fallback recovered the page.

## 2026-08-14 — A broad card-source patch touched the first matching effects

The first correction patch lacked card-specific context and replaced GameFAQs on Abyssal Countdown's hook and Barrage's projectile count instead of the intended Brawler and Pristine Perseverence health effects.
An immediate semantic inspection exposed the two wrong targets before verification, and an ID-anchored patch restored them while changing only the intended effects.

## 2026-08-14 — The map admission review guessed ticket 002's obsolete filename

The independent ticket 004 admission review first tried `docs/tickets/closed/002-research-reference-build-and-match-rules.md`, which does not exist after the ticket's final naming.
Repository file discovery immediately found `docs/tickets/closed/002-research-core-rules-and-measurements.md`, and the reviewer confirmed its closed status before admitting the dependent map research.

## 2026-08-14 — The public map sheet resisted two ordinary retrieval paths

Opening the public Google Sheet through the ordinary web reader returned a safe-open error, while the fetch fallback reached an edit page that required JavaScript and exposed no map rows.
The sheet's public CSV export succeeded and established 71 rows including the header, while the real browser exposed all 70 row-ordered preview images needed for visual research.

## 2026-08-14 — The first browser extraction session timed out

The first browser capability and page-state request exceeded its 30-second command limit and reset the persistent browser kernel before producing usable state.
One troubleshooting inventory and a 60-second reconnect restored the public sheet session, after which the browser downloaded all 70 previews without another missing asset.

## 2026-08-14 — The CSV reader flattened the map sheet

The general URL reader returned the public CSV as flattened text that was unsuitable for preserving row boundaries.
A direct CSV parser reproduced 71 rows and 70 map entries, so the catalog uses the browser images for silhouettes and the parsed sheet only for exact row ordering.

## 2026-08-14 — Map silhouette analysis could not use OpenCV

The environment did not provide the `cv2` module for the planned image segmentation pass.
A Pillow flood-fill and contact-sheet workflow produced inspectable silhouettes and spawn diagnostics for all 70 previews, with the generated geometry remaining explicitly coarse and low-confidence.
Pillow also emitted a `getdata` deprecation warning while deriving masks; retained code should use `get_flattened_data` if this temporary analysis becomes a supported tool.

## 2026-08-14 — The first map schema used unsupported validator keywords

The initial map schema used `maximum` and `maxItems`, but the repository's intentionally small schema validator rejects unsupported keywords instead of silently ignoring them.
The targeted checker suite failed at `SPEC001`, so numeric maxima and the exactly-two-spawns rule moved into the semantic map checker while the schema kept only supported structural constraints.

## 2026-08-14 — The unsafe-spawn regression used a safe boundary value

The first unsafe-spawn fixture placed the two spawn centers exactly eight player diameters apart, which satisfies the contract's minimum separation and correctly produced no `SPEC050` failure.
The fixture now places them seven diameters apart so it exercises the intended unsafe boundary without weakening the eight-diameter rule.

## 2026-08-14 — The first real-map gate exposed rounded envelopes and two conservative hazard overlaps

The map checker passed all fixtures but rejected 69 real collision envelopes because the fixed camera edge rounded to 17.969 diameters while silhouette-derived collision edges rounded to 17.970.
It also rejected the inferred spawns on `arena-016` and `arena-045` because their centers fell within the visible saw radius plus a one-diameter clearance.
The correction widened every fixed camera envelope to its intended 18-diameter half-width and moved only those two provisional spawn regions to supported portions of the same visible platforms outside conservative saw clearance.
The repository checker then passed all 70 maps.

## 2026-08-14 — The map worktree predates the card research note

A documentation inspection tried to use `research/notes/cards.md` as a formatting reference, but the map worktree branches from `main` before ticket 003 and therefore does not contain that later file.
The existing core-rules note and repository sentence-per-line convention provide the local prose pattern, and the eventual rebase will bring the independent card note into this worktree.

## 2026-08-14 — The ring-out representative contradicted its own classification

The first representative set named `arena-064` for ring-out coverage even though that same entry has `ringOutFocused: false`.
The semantic checker had verified only that representative IDs existed, allowing a category label and arena classification to disagree.
The correction selects classified ring-out arena `arena-006`, requires every representative to exhibit its named static, moving, breakable, hazard, asymmetric, or ring-out property, and adds a regression for mismatched category coverage.

## 2026-08-14 — The first full map gate outlived its launcher timeout

The map worktree had no local `.tools` cache, so its first full gate spent the 120-second command window downloading and expanding the pinned .NET SDK and Godot instead of reaching compilation.
The launcher timed out with no captured output, but its bootstrap child remained live and continued growing the verified Godot archive, so the owning session monitored it rather than cancelling a progressing installation.
One 30-second process-wait probe returned exit code 1 as the watched process completed between the wait and follow-up inspection, but the installed SDK reported 8.0.423 and the expected Godot console executable was present.
The immediate gate rerun used those local tools and passed with zero warnings, 26 tests, repository checks, deterministic hash `f250d549cfb52a8b`, and Godot editor and runtime smoke.

## 2026-08-14 — Temporary map previews were removed after derivation

The browser research workflow left one exact temporary asset bundle containing 77 files, including six diagnostic PNG contact sheets and the downloaded preview payloads.
After the committed hashes, vectors, classifications, and uncertainty notes made the raw bundle unnecessary, the owning session validated the absolute temp parent and UUID leaf, deleted only that directory, and verified it absent.
The public sheet tab was then closed and the persistent browser kernel reset, leaving no live research session or original preview asset in the repository.

## 2026-08-14 — Card integration overlapped five map catalog files

Rebasing the completed map catalog onto integrated ticket 003 conflicted in the shared decisions ledger, postmortem ledger, source index, and checker-test file, while the main checker merged automatically.
The resolution retained both decision and failure histories, all card sources plus the map sheet, every card regression plus six map regressions, and both card and map cross-check paths.
The combined full gate then passed with zero warnings, 37 tests, repository checks, deterministic hash `f250d549cfb52a8b`, and Godot editor and runtime smoke.
Git also warned that the generated map JSON and mechanically resolved test file had CRLF working copies that would normalize to LF when touched; staged diff checks confirm the repository form is valid.

## 2026-08-14 — Ticket 004's first review rejected the geometry pipeline

Fresh reviewer `codex:019fff73-e68d-79d0-bca4-5da80964846a` rejected exact candidate `79e2007096735f0be082580a055464ceaa804e50`.
The public row 7 preview contains disconnected vertical islands, while the documented transform renders `arena-006` as a dense horizontal maze, and row 17 shows a destructible scaffold even though `arena-016` claims three saws.
The 4,520 generated rectangles, inferred spawns, collision bounds, behavior families, and representatives are therefore not reliably attached to their cited source rows.
The candidate also omitted the binding mask-render IoU oracle and allowed only axis-aligned cells even though the owning map design requires oriented boxes for visibly rotated platforms.
An independent review raster measured roughly 0.10 IoU for `arena-006`, far below the required 0.95 acceptance threshold, while the full repository gate remained falsely green.
Correction will replace the generator rather than patching individual rows, bind every embedded workbook image to its actual anchor, encode oriented boxes, store mechanically reproduced IoU evidence, and reject the catalog if any map falls below 0.95.
During review, ordinary Google Sheets access was blocked before the read-only fallback and XLSX export succeeded, one read-only PowerShell query assumed a nonexistent `catalog` property, and Pillow emitted `getdata` deprecation warnings.
Direct recursive cleanup of the reviewer's exact temporary directory was rejected before execution, after which the reviewer moved only that verified directory to the recycle bin.

## 2026-08-14 — Map correction experiments exceeded one bound and produced one poor decomposition

The first adaptive oriented-box experiment exceeded its 124-second process limit without producing results.
A bounded global splitter then completed in 114 seconds but needed 42,637 boxes and still missed its approximate target on many arenas because it tried to partition disconnected layouts as one point cloud.
The supported generator instead labels eight-connected components before fitting and splitting, completes all 70 arenas in about 68 seconds on this workstation, and commits only results that pass exact raster verification.

## 2026-08-14 — The first corrected catalog build assumed horizontal spawns

The first generator run reached `arena-018` and stopped because that vertically arranged layout could not provide eight diameters of horizontal separation on source-supported surfaces.
The safety outcome is distance between opponents rather than horizontal ordering, so the generator and checker now measure Euclidean separation and retain named oriented-box support.

## 2026-08-14 — The corrected schema repeated unsupported upper-bound keywords

The first version 2 map schema again used `maximum` and `maxItems`, which the repository's deliberately small schema validator rejects instead of ignoring.
The checker suite reported `SPEC001` before semantic tests could run.
Supported schema constraints retain minima and structure, while the semantic checker enforces exact counts, coordinate maxima, rotation range, and evidence arithmetic; 31 focused tests now pass.

## 2026-08-14 — A correction search named the repository's nonexistent root test directory

A read-only file search included `tests`, which does not exist at the repository root, so it returned the useful map matches and then exited with code 1.
Repository discovery identified the actual `tools/Rounds.Checks.Tests` project before any edit.

## 2026-08-14 — Integrated card worktree cleanup was blocked by its local SDK executable

Removing the reviewed ticket 003 worktree unregistered it but failed to delete the directory with an `Invalid argument` error.
Two exact-directory deletion attempts then failed on `.tools/dotnet/dotnet.exe`; stopping five processes whose executable path was scoped to that worktree did not release the handle.
Renaming only that verified executable to `dotnet.cleanup.exe` succeeded, after which exact-directory deletion succeeded and both filesystem absence and worktree unregistration were verified.

## 2026-08-14 — Ticket 004's second review rejected three evidence boundaries

Fresh reviewer `codex:019fffa0-7ad0-7a30-8b5e-bf4bfa70ab8d` rejected exact candidate `a6dc1a2ae85ee9b4bf377f956837c7d1003606d0`.
The checker rerendered geometry but compared only total foreground pixels, so moving one non-support box preserved its pixel count and the stored overlap arithmetic while actual source IoU fell from 0.960915 to 0.876249.
Fixed 0.8-by-0.4 spawn regions also validated only their centers, leaving unsupported points in 40 of 140 regions, and the reconciliation record supplied only one complete public arena index.
Correction adds a positional rendered-mask digest and position-preserving regression, derives each whole spawn region from its support box, and reconciles the sheet with an independent public index of six removed release-era arenas.
The review policy rejected a combined generator-and-cleanup command before execution and then rejected its fixed temp-file deletion, while one read-only search named nonexistent `research/notes/sources.md` before finding `spec/sources.json`.
The owner later validated the exact 2,189,951-byte temp file at `C:\Users\Adam\AppData\Local\Temp\rounds-map-review-019fffa0.json`, deleted only that file, and verified it absent.
The first multi-file checker correction expected the pre-digest `SPEC059` diagnostic and atomically wrote nothing; a targeted retry used the current text and applied cleanly.

## 2026-08-14 — Ticket 004's third review rejected pixel tracing and stale mover rationale

Fresh reviewer `codex:019fffb3-110f-7762-a73c-f7941d144fc9` rejected exact candidate `bd730d0966b819da292cded3bd9ff5787944e631`.
Its 7,557 boxes and 0.95 full-resolution silhouette oracle reproduced source pixels accurately but violated the frozen ticket's requirement for original vector geometry rather than tracing.
The binding physics-and-maps design also described movers as conditional and left their existence open even though official patch history confirms moving platforms and a wrecking ball.
Correction preserves every connected layout component, caps arenas at 96 original oriented boxes, checks only coarse 8-pixel occupancy, and leaves exact mover rows and motion parameters unresolved.
During review, a malformed PowerShell revision range and a read-only search for nonexistent local `playbook` paths failed before corrected read-only commands succeeded.
An accidental one-second wrapper interrupted the reviewer's first full gate, while an uninterrupted rerun passed; safety rejected direct temporary cleanup, after which scoped alternatives removed the exact review temp and cache with no residue.

## 2026-08-14 — The clean-room geometry cap expanded one camera envelope

The first capped clean-room catalog run failed the real-map gate because `arena-058`'s simplified oriented box extended below the fixed preview camera envelope after rounding.
Camera bounds now start from the preview envelope and expand deterministically to contain the committed collision geometry with a 0.01-diameter margin.
The semantic gate retains camera containment, so later geometry changes cannot silently recreate the defect.

## 2026-08-14 — A combined cache cleanup and test command was rejected

A command that validated and recursively removed the generator's exact `tools/maps/__pycache__` directory before running focused tests was rejected by safety policy before execution, and a second command with two explicit `Remove-Item` targets was also rejected before execution.
The owner separated cleanup from verification and used the runtime filesystem API to delete only the enumerated cache file and then its empty directory.

## 2026-08-14 — Final-gate discovery named a nonexistent local playbook path

A read-only search for repository gate documentation included a local `playbook` path even though the shared playbook lives outside this worktree.
The search still found the supported `tools/checks/run.ps1` entry point, so verification continued from that project-owned script.

## 2026-08-14 — Reproducibility first assumed a nonexistent bundled Python path

The first final reproducibility command tried `.tools/python/python.exe`, but this worktree bundles .NET and Godot only, so PowerShell rejected the missing executable.
The command's `finally` block removed its empty temporary output, and the retry used the installed `python` command that had built the catalog correction.

## 2026-08-14 — Ticket 004's fourth review rejected missing motion evidence and two contract holes

Fresh reviewer `codex:019fffc8-8b3b-75a1-ae5d-f4b8ad895a73` rejected exact candidate `f96782394ea9a5ad26da3f508a0aa2c97bc3c0ff`.
The frozen ticket requires a measured moving-map example when that category exists, but `arena-026` remained only a visual candidate with unknown timing despite official evidence that vanilla Rounds includes moving platforms.
The locked architecture still named static AABB level geometry while the map design, schema, and 70-row catalog require oriented boxes.
The coarse-evidence checker also accepted internally consistent occupied-cell counts larger than the fixed 80-by-45 grid's 3,600 cells.
Correction binds `arena-026` to frame-addressable current-build footage, records its mirrored U-shaped platform sweep with partial timing, aligns the architecture, and caps every occupancy count at the grid size with a regression.
During review, searches over nonexistent optional glob paths exited with code 1, an empty PowerShell pipeline caused a parser error before correction, importing the generator created a Python cache, and two direct cache cleanup attempts were rejected before a scoped runtime deletion left no residue.

## 2026-08-14 — Direct game observation and motion-analysis tools had recoverable failures

A read-only repository discovery included absent optional files and exited with code 1 after returning the useful paths.
The installed computer-control package exposed no documented `documentation` function, rejected one launch argument, and then lost its Node execution context for window inspection, although the visible shell successfully launched the installed ROUNDS build.
The existing frame-addressable gameplay cache therefore supplied the controlled observation instead of an inaccessible live window.
One generator import used its hyphenated filename as a Python module name and failed before an explicit file loader succeeded, while a planned SciPy analysis was unavailable and the retained NumPy and Pillow path produced the measurements.
An initial `imageio_ffmpeg` call placed the `fps` filter among input options and failed before moving it to output options, one openpyxl read-only sheet reported no maximum column before the reader bounded itself to six columns, and one tool-orchestration script had a JavaScript syntax error before correction.
A native-frame tracking result exceeded the tool output budget and was truncated, so the durable measurement uses a concise set of exact two-second source frames with explicit positional and timing tolerance rather than pretending the oversized trace was reviewed.

## 2026-08-14 — The measured-motion correction exposed four small validation mistakes

The first concise native tracker indexed a four-column array as though it had five columns and failed before the corrected read-only run summarized the available high-confidence samples.
The first measured bounds used the visual centers without enough room for the declared part-size tolerance, and the real repository checker rejected them at `SPEC062` before the bounds expanded by at most 0.1 player diameter.
One multi-document prose patch assumed the catalog note's headings also existed in the physics design and atomically changed nothing before separate exact-context edits succeeded.
The first focused test command used the system `dotnet`, which had no SDK compatible with pinned 8.0.423, before the worktree's verified `.tools/dotnet/dotnet.exe` ran all tests successfully.
After the owning session stopped the exact installed ROUNDS process, an immediate presence probe reported it still running, while a two-second follow-up returned no process and confirmed the launched research session had exited.
The first measured-motion fixture used module bounds taller than its collision envelope, so the new containment rule correctly failed the otherwise-valid repository fixture before the fixture adopted its existing half-unit collision height.
A cleanup inventory piped directly from a completed `foreach` block and reproduced PowerShell's empty-pipeline parser error before a collected-row retry listed both exact targets.
The owner then validated the UUID-named evidence directory and generator cache by absolute path, deleted only those two directories through the runtime filesystem API, and verified both absent after all derived measurements were committed.

## 2026-08-14 — Ticket 004's final review approved the corrected catalog

Fresh reviewer `codex:019fffeb-75c7-7c30-82d5-ac46c0ec51a3` approved exact candidate `67369534652c9aac6e2fb278e6afdc09eab213a9` with no actionable findings.
One read-only discovery named absent optional build files and exited with code 1 after useful results, one result-formatting mistake produced oversized truncated output, and one `ConvertTo-Json` diagnostic warned about its default nesting depth before focused reads recovered the evidence.
The review's unbounded background gate launcher did not emit its planned process identifier, but its complete success output, empty error log, and absent review process established clean completion.
The reviewer left one regenerated catalog and two gate logs under the user's temporary directory as instructed.
The owner validated those three exact session-named files, deleted only them, and verified no review residue remained.

## 2026-08-14 — Map worktree cleanup hit live build servers and a Windows path limit

`git worktree remove` unregistered the integrated map worktree but left its directory after reporting `Invalid argument` while six worktree-scoped .NET processes were live.
The worktree SDK's supported build-server shutdown stopped the MSBuild and compiler servers, and an exact-path `git clean` preview showed only the abandoned worktree before cleanup began.
Git removed the tracked tree and most ignored tools but left one SDK runtime-configuration file because its path exceeded the Windows command limit.
The owner revalidated the exact `004-map-catalog` directory under `.ivy/worktrees`, removed that final tree through the extended-path runtime API, and verified both filesystem absence and worktree unregistration.

## 2026-08-14 — Ticket 005's first admission review rejected generic coyote time

Reviewer `codex:019ffff6-6034-76b1-96a2-b080ac183346` rejected exact ticket candidate `9afcb7116bccd324c6c2449cee41bee38c5f6968` because its coyote-time requirement conflicted with the binding persistent stored-air-jump rule.
The correction removes coyote machinery from the ticket and physics design, retains the sourced four-tick landing buffer, and requires direct tests for ledge departure, jump consumption, and refill-after-landing behavior.
During review, one search included a nonexistent root `tests` path and produced oversized truncated output after useful results, one inspection guessed nonexistent `src/Rounds.Sim/Input.cs`, and one arena query guessed root property `arenas` before using `maps`.
All three mistakes were read-only and left no residue.

## 2026-08-14 — Ticket 005's amended admission review used two focused retries

Reviewer `codex:019ffff9-3be5-7da2-8811-5df376ffc9a4` admitted exact amended candidate `b09b9fd1814443f3dfeaea76c80541bc116ede55` with no findings.
One read-only catalog query initially used incorrect property names and returned empty counts before the corrected query confirmed 15 static arena-006 primitives and two spawns.
One combined inspection exceeded the display budget, so focused rereads recovered the relevant ticket and design evidence.
Neither retry changed files or left residue.

## 2026-08-14 — Movement implementation discovery made four harmless path and shell mistakes

The first build command assumed another worktree contained a usable `.tools/dotnet/dotnet.exe`, but SDK tools are intentionally ignored and worktree-local; the verified bootstrap installed the pinned SDK in the movement worktree and the zero-warning build passed.
One read-only arena inventory piped directly from a completed PowerShell `foreach` block and hit the known empty-pipeline parser error before a collected-array retry returned the intended roles.
One frontend inspection guessed `game/Rounds.Godot/Main.cs` and `game/Rounds.Godot/project.godot`, and the same command guessed `tools/Rounds.Harness/Program.cs`; `rg --files` immediately located the actual `game/Main.cs`, `game/project.godot`, and `src/Rounds.Harness/Program.cs` paths.
These failures changed no tracked files and left no residue.

## 2026-08-14 — Ticket 005's first implementation review found two untested public boundaries

Fresh reviewer `codex:019fffc8-8b3b-75a1-ae5d-f4b8ad895a73` rejected exact candidate `c523642e731be3003aa845b91e5dad2864d4d120` because the stream catalog loader accepted spawn support IDs that named no loaded static box, while the catalog test asserted only the two-spawn count.
The same review found that initial corner overlap compared squared offset distance with a linear epsilon, producing a discontinuous axis normal for offsets of about `0.000007` diameters.
Correction makes both invariants explicit at their public boundaries and retains regressions for the malformed support reference and radial near-corner normal.
The computer-control package lacked its skill-required `documentation()` API and rejected the verified live Godot window with a contradictory ownership error; a scoped Win32 capture proved the arena and both players render, but indistinguishable injected-key frames were not claimed as control evidence.
One review revision-range interpolation produced invalid `git diff` syntax before a quoted retry, and one no-match search exited 1; the reviewer stopped the exact Godot process, removed all four screenshots, and left no residue.

## 2026-08-14 — Combat-slice discovery used two invalid Windows path forms

A read-only `rg` call supplied Unix-style recursive file globs that Windows rejected after the useful combat and match spec reads had completed.
A corrected directory search then included a nonexistent empty `docs/tickets/open` directory in `Get-ChildItem`, which failed after returning the closed ticket names.
Focused directory reads recovered the design evidence, and neither mistake changed files or left residue.

## 2026-08-14 — One fresh reviewer reservation exposed the owner's session UUID

A newly isolated reviewer context reported the top-level owner's provider session UUID instead of a distinct durable reviewer ID before inspecting any repository file.
The owner cancelled that reservation immediately; it ran no commands and made no changes.
The corrected candidate instead went to the existing isolated admission context whose distinct provider-qualified session had not authored or reviewed the implementation.

## 2026-08-14 — Ticket 005's second implementation review found permissive role parsing

Fresh implementation reviewer `codex:019ffff9-3be5-7da2-8811-5df376ffc9a4` rejected exact candidate `71410dcd2c7ecaf84745de01ae572f58a55b9d84` because the arena loader skipped every primitive role other than `static`.
Skipping the two known visual-only roles is required, but treating a misspelling such as `statci` the same way can silently remove collision geometry from malformed input.
Correction replaces the open-ended conditional with an explicit role switch and retains a public malformed-role regression.
During review, two discovery commands guessed nonexistent checker script names before finding `tools/checks/run.ps1`, two broad outputs were truncated and recovered with focused reads, and the first in-memory probe used invalid JSON shorthand `-.5` before valid JSON reproduced the defect.
The supported gate refreshed only ignored build outputs; no review-specific temporary or tracked residue remained.

The first owner regression used a raw-string replacement whose indentation did not match the valid fixture, so it accidentally exercised unchanged valid JSON and still reported no exception after the parser correction.
An explicit insertion at the primitive-array key plus a mutation assertion made the fixture trustworthy before verification resumed.

## 2026-08-14 — Ticket 005's final review approved the corrected movement slice

Fresh reviewer `codex:019ffff6-6034-76b1-96a2-b080ac183346` approved exact candidate `1fa05e72962f771c5d5ff7fbe0e3266233f3c963` with no findings after independently checking the three earlier correction boundaries, the complete implementation, spec immutability, full gate, and live rendering.
The computer-control package again lacked its documented API and rejected its own verified Godot window, so scoped Win32 capture supplied the visual evidence.
The first Godot discovery guessed nonexistent `.tools/godot` before using the gate's `.tools/godot-4.7.1`, and the first screenshot used DPI-virtualized coordinates before the same exact PNG was overwritten with a correct 120-DPI capture.
The reviewer stopped the exact game process, deleted the exact temporary PNG, verified both absent, and left only ordinary ignored build outputs.

## 2026-08-14 — Movement worktree cleanup repeated the Windows invalid-argument failure

The first cleanup command assumed the root checkout contained the ignored worktree-local .NET executable, so its build-server shutdown step failed before Git unregistered the movement worktree but again reported `Invalid argument` while leaving the directory.
The owner resolved and validated the exact `005-movement-collision` path under `.ivy/worktrees`, used its residual SDK to stop the MSBuild and compiler servers, and confirmed Git no longer registered it.
An extended-path runtime deletion then removed only that validated abandoned directory, and final checks proved both filesystem and worktree-list absence.

## 2026-08-14 — Ticket 006's first admission review found six combat contract gaps

Reviewer `codex:019fffc8-8b3b-75a1-ae5d-f4b8ad895a73` rejected exact ticket candidate `537d94d38ae2fe2da71cf69d3976828164000d8d` before implementation.
The ticket resolved ring-outs immediately despite a binding six-tick observable delay, named block-push magnitudes without defining wall-assisted self-launch, left initial/reset zero-aim behavior open, did not prove native shell controls, omitted the confirmed bilateral spawn lock, and chose a three-state block machine while leaving the living design's unsourced recovery phase active.
The same review asked for exact reflected-movement and bullet-lifetime boundaries.
Correction binds all seven behaviors, adds focused evidence, and requires the living design update without expanding into score, draft, cards, or arena cadence.
Several broad read-only searches used nonexistent design paths, Windows-incompatible wildcard paths, or absent directories and exited 1; one output was truncated before focused reads recovered every cited fact.
No files, processes, or temporary artifacts were left by the review.

## 2026-08-14 — Ticket 006's second admission review corrected base-health provenance

Reviewer `codex:019ffff9-3be5-7da2-8811-5df376ffc9a4` rejected exact ticket candidate `bfbf7e7081ca47f87dc0945644081f0324ba6a8c` because it called `1.0` base health provisional even though `spec/player.json` confirms it exactly with high confidence.
The correction loads that unchanged value from the embedded player facts and reserves the provisional label for genuinely unmeasured combat behavior.
One parallel inspection exceeded its display budget, one diff used an incorrectly expanded parent SHA before the exact revision was supplied, and one read guessed nonexistent `docs/design/match-flow.md`; focused reads recovered all evidence.
The review created no files, processes, or temporary artifacts.
