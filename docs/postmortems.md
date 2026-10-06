# Postmortems

## 2026-10-05 — Arena conversion and rope scale in ticket 77

A bounded converter used PowerShell text replacement and temporarily wrote an exception into `physics.rs`.
The delegate restored that file from its exact base before handing off, but left two contour files empty; no candidate was committed or reviewed in that state.
The worker replaced the partial files with a temporary Rust exporter using the original definitions and RON serialization, then removed the exporter and original geometry code.
This retained exact contours and float values and all existing simulation tests passed unchanged.

The ten-second arena test exposed accumulating stretch in a body-to-body rope.
Rapier's correction speeds were scaled for metres while arena coordinates and gravity use pixels.
Setting ordinary arena integration to 100 world units per metre kept the chain within its declared length; the replay adapters retain their original scale.
The regression tests keep both chain attachment and loop-position checks at ten seconds.

The first default parallel network run missed two live timing checks while the bulk headless simulation ran alongside them.
Both tests passed individually and the unmodified base passed the default parallel suite.
The arena watcher initially read complete files on every physics tick; it now reads every fifteen ticks and retains the resolved path.
The original timing limits remain unchanged; verification includes the same default parallel network suite after that correction.

The added shot-push test caught a gameplay contact being consumed before projectile momentum reached the loose body.
Ordinary arena hits now apply the absorbed incoming projectile momentum before removing the shot; the same regression passes through public fighter input.
That regression was added after the broad run began, so its failure required another expensive native relink.
The final pass ran all new arena boundary tests first, then the workspace checks; adding checks while a broad build runs wastes that build's evidence.

The worktree cleanup helper refused the baseline's ignored Cargo junction and then its empty `out/` directory.
After checking the junction's exact target, the worker unlinked only the junction and removed the empty directory before retrying cleanup.
The shared prepared Cargo target remains intact. Delivery uses the same explicit unlink after retaining this ticket's evidence.

The first all-kinds preview reached ready GPU pipelines but timed out waiting for the scene.
The data renderer omitted the background capture marker consumed by the existing readiness gate.
It now marks the background and fighters, and a renderer regression checks capture readiness for every arena file.

The first independent review rejected the candidate's default lens distortion and stretched camera frame.
It also found that replay reload restarted network ticks and invalid later-stage edits could panic at transition.
Corrections disable data-camera distortion, preserve frame aspect, replace only arena state, and retain validated stage definitions.
Regressions cover edits during both timber and ice combat and malformed future-stage files through public replay input.
The correction audit also found snapshots still falling back to direct stage-file reads before the watcher caught up.
Snapshots now use the same validated cache as stage physics. Finding that read after the broad build started cost another native relink.

The second review found that preserving replay clocks while installing generic arena objects still discarded the replay HUD, draft screens and timber joints.
Replay edits now replace only their existing actors and physics, keeping the presentation adapter and following the next active arena file.
It also found the stage cache selected only the rematch profile, although match-end sessions can reach hanging or begin another match.
The cache now follows the presence of match flow. Public-input regressions cover an orange win into hanging and a fresh match's first half into timber.
The delegate's first flow fixtures attempted unimplemented or unhovered card confirmations; corrected fixtures hover an implemented offer before confirming it.

The last two-job workspace build failed in MSVC link.exe with exit 1 and no linker diagnostic.
C: had 48 GB free; post-failure physical memory had about 8 GB free and commit headroom about 21 GB.
Windows recorded no linker crash or resource-exhaustion event; those snapshots do not establish peak usage.
A fresh other-family Fable consult (claude:92ba952a-0df9-4271-8cfa-1b0fb1dcfe44) recommended one diagnostic retry and retaining an unknown-cause record.
The same locked workspace build passed with one job and verbose logging, using the prepared target without changing source, configuration or toolchain.
The cause remains unconfirmed. This was a verification scheduling adjustment, not a fix for a diagnosed application defect.
Repeated correction reviews and native relinks cost substantially more than the original verification; focused lifecycle regressions now cover the missed replay boundaries.

The final workspace test run overlapped headless GPU captures and failed join_timeout_and_bad_state_are_named_failures.
That existing fixture uses a 100 ms fake-authority receive, but the interrupted harness did not print the failed assertion; its cause is not established.
The old-session fixture then waited for Welcome with no authority socket left: only one test UDP socket remained, and its loop never resends Hello or ends.
The worker stopped that exact stuck test process after establishing it could not progress, retained the partial log, and reran the complete suite without captures or other worker jobs.
No test timeout, assertion, coverage or runtime setting was changed. The initial short smoke also omitted its explicit teal profile and hit the default timber event gate before the explosion; the corrected teal command passed.

The third review found that an earlier valid edit also rebuilt actors after the ice stage loader, clearing its 60-tick entry slide.
A public replay regression reproduced the missing entry pose on the second transition tick.
Edit-driven file switches now rebuild only the initial profile stage; timber, ice and hanging loaders already install validated data.
The prior-match/draft handoff still follows its edited files. The repeated review identified lifecycle boundaries that the original within-stage tests did not cover.

The next default-parallel suite rejected a protocol-12 packet at the protocol-11 server in lib.rs:811; its two clients then received Windows error 10054.
The unchanged timeout fixture in live.rs releases an ephemeral port before repeatedly sending protocol-12 Hello packets to it for five seconds.
That permits another parallel server to bind the destination. Port reuse is a concrete plausible explanation; no packet trace identified the sender.
Ticket 85 preserves this separate test-isolation problem, including the earlier receive-loop stall, without broadening the arena implementation.
Final full-suite verification uses test-threads=1 with every test, assertion and timeout intact. Both failed parallel logs are retained alongside the passing evidence.

The fourth review found that a draft edit made before activation refreshed its cached drawing but left the startup collision geometry unchanged.
The watcher had consumed the changed source before deciding whether a prior active edit required a rebuild.
A public replay regression lowered the future draft floor and reproduced the fighter standing at -166011 milli on the old floor.
The initial-stage file switch now compares its old cached source before reading, and rebuilds on that change or a previous active edit.
Later stages still use their loaders, so this correction retains the ice-entry fix and avoids resetting unchanged replay geometry.

## 2026-10-05 — Ticket 78 replacements removed wanted neighboring behavior

The first delegated simulation and transport replacements removed more than footage adapters: arena object physics and the live transport's session, flow-edge and terminal-delivery behavior disappeared.
The parent caught these gaps before committing and returned them for repair, then assigned fresh bounded contexts to preserve the existing contracts.
The original task already required preserving those interfaces; treating the smaller replacements as complete would have violated that scope.
Retained behavior is now explicit in the handoffs and verification: arena objects/reloads and live session/edge/terminal tests accompany the new match-rule tests.
One presentation child also attempted a Cargo check despite the parent-only build instruction; it stopped on a temporarily missing manifest target during another child's rewrite and did not start a clean target.
The parent remains the sole Cargo runner, using the prepared target and the repository's two-job cap.

The first complete test pass exposed live peers being welcomed before concave arena collision preparation finished.
The authority started its silence clock before that work, and clients stopped waiting before receiving a snapshot.
Packet round-trip tests ruled out serialization; preparing collision geometry before Welcome and reusing unchanged surface colliders restored the live tests with their existing time limits.
The final audit also found the watcher treating initially empty source text as an edit and resetting fighter health on reload.
Validated source text is retained across arena selection, and edits preserve fighter health and elimination as well as flow.
The installed Ivy check-all script expects an Ivy source repository containing playbook/skills and cannot run against this game's checkout.
The repository's format, strict Clippy, build, workspace tests, UDP smoke, rendered captures and installed ticket validator provide the applicable checks.
The rendered match-end evidence exposed missing separator glyphs in the default font; the shipped labels now use supported characters.
A projection audit also found fixed arena dimensions could stretch a visible window; automatic minimum dimensions now preserve aspect and have a camera-boundary regression.
The first separator substitution did not apply because Python used the Windows default encoding to read UTF-8 source.
The second render exposed that failed substitution; exact UTF-8 edits corrected it before review.

The first fresh review approved the match rules but found capture provenance and several general transport regressions had been removed with footage tests.
Passing the same path for PNG and metadata overwrote the image with JSON. A public CLI reproduction preserved this failure before destination validation and provenance were restored.
The recording also depended on the current live asset pool: adding cards changed its offers, and adding an arena changed its fight. Recordings now contain their starting logical content.
A separate dedicated-server reproduction used two real UDP peers to rename an active arena and edit it again in the next fight; the second edit was never observed.
The watcher updated its shuffled definition but left the file mapping under the previous name. Rename updates now move both path and cached-source keys together, with a regression across a public-input fight transition.
Architecture and roadmap descriptions still named retired profiles; they now describe the ordinary match and its remaining 1v1 presentation/live-transport scope.

## 2026-10-06 — Shared Cargo artifacts hid [ticket 82](http://ivy.localhost/tickets/82?repo=rounds-clone)'s arena tests

The first locked workspace test run reported success but ran only 16 simulation tests, missing all four new arena tests.
The shared target's `quarrel_sim-646de60163062f7a.d` named [ticket 85](http://ivy.localhost/tickets/85?repo=rounds-clone)'s worktree and omitted `arena_data/tests.rs`.
Its network binary also ran two tests absent from this candidate. Cargo's build-directory lock prevented concurrent compilation but did not make those cached artifacts belong to this checkout.
The other worktree had produced newer artifacts than this checkout's source timestamps, so Cargo reused them; the focused simulation command used a different feature-unified artifact and had run the correct 20 tests.
The first workspace result is invalid evidence. Refreshing this checkout's crate source timestamps forced its six crates to rebuild while retaining dependencies and the two-job cap, but the subsequent run still executed an overwritten simulation binary.
Cargo releases its compilation lock before running all test executables; another worker can replace a later executable while earlier tests run.
Verification uses a locked workspace `--no-run` build and copies each executable when Cargo emits its artifact record, then runs those copies and checks all four arena test names.
The artifact records identify this checkout and retain the same workspace test configuration. Logs, previews and hashes remain under the ignored `out/ticket-082/` parent in the integration root.
[Ticket 87](http://ivy.localhost/tickets/87?repo=rounds-clone) records the build-infrastructure follow-up; this arena delivery changes no Cargo configuration or locking infrastructure.

## 2026-10-06 — Ticket #85 shared Cargo artifacts obscured test provenance

The worker linked its target to the root's prepared Cargo target while other ticket worktrees also used it.
The final pre-review run passed 16 network tests, but the later rebased run executed a 14-test network binary missing both new regressions.
The reviewer caught the mismatch between source test count and executed test names; the worker's initial claim that this was exact-source verification was incorrect.
Cargo had reported a build-directory lock and finished successfully. That establishes process serialization, not that its cached workspace binary belongs to the current worktree.
The exact fingerprint mechanism was not traced, so treating this as a proven Cargo implementation defect would overstate the evidence.
Final verification uses a private target seeded only with compatible prepared artifacts, removes every copied workspace package output, and checks the executed regression names.
A lock on the shared target prevents overlapping Cargo jobs during this private verification. The private target is removed with this worktree; the root target is retained.
The same review found that endpoint clones removed the implicit Windows 10054 coverage. Explicit raw-socket reset and shutdown-error checks now retain that boundary coverage.
Automatic command safety review rejected replacing the target junction. A separate `out/isolated-cargo-target` keeps the junction intact and is covered by the artifact-trust exception.
The seed included 51.14 GiB of logical file data; package invalidation reported removing 21.9 GiB. Copying workspace artifacts only to remove them was avoidable I/O.
The shutdown regression initially assumed Rust would expose raw Winsock 10058. Rust returned an I/O error without a raw code, so the check now verifies propagation of the actual non-reset, non-timeout error.
The forced-rebind collision fixture introduced the same ephemeral-port gap this ticket removes. A later full parallel repeat failed at the replacement bind with Windows 10048.
The retained collision check sends the live Hello to an already bound synchronous authority; the authority-exit regression separately proves exclusive endpoint ownership, without adding another release-and-rebind gap.


## 2026-10-06 — Lock the throwaway physics spike for [ticket 88](http://ivy.localhost/tickets/88?repo=rounds-clone)

The first offline spike command could not resolve uncached `bincode`; resolving its standalone manifest then selected newer transitive versions than the game lockfile. That build was stopped before measuring. Copying the repository lockfile into the temporary crate preserved every shared package identity; only `bincode`, `enumn` and `serde_arrays` were added for physics serialization. The corrected spike uses the existing target and two Cargo jobs, with no spike dependency or source added to the game.


The final rebased verification copied each test executable as Cargo emitted it; all 43 tests passed from those copies. The later documentation phase failed because `rustdoc` could not find `libquarrel_sim-e3fdc65f5faf41ff.rlib` in the shared target while other Cargo commands were active. Test executable copies do not protect rustdoc's external libraries. Repeating documentation checks after those commands finished passed, with candidate source timestamps refreshed and the dependency target retained; [ticket 87](http://ivy.localhost/tickets/87?repo=rounds-clone) owns the shared-build isolation problem.

The verification helper initially waited for every Cargo process, including an already-built network test, and delayed the final checks unnecessarily. The corrected helper waits for active compilation or queued builds; running test executables do not consume Cargo compiler jobs. No unrelated process was stopped.
## 2026-10-06 — Shared Cargo target overwrote ticket 81 verification executables

Parallel run 75 workers serialized builds through Cargo's target lock, but Cargo releases that lock before a workspace's test executables finish.
Ticket 81's first workspace test command ran ticket 82's presentation and simulation binaries after its longer network tests; the test counts exposed the mismatch.
That result is not evidence for ticket 81. Snapshot each executable from Cargo's compiler-artifact output during locked compilation, then run the copies.
Client subprocess tests accept QUARREL_TEST_CLIENT to use the same snapshotted client; ordinary Cargo tests still default to CARGO_BIN_EXE_quarrel-client.
Keep one prepared target and two build jobs. The snapshots are verification evidence, not a second Cargo target or a clean build.
The shared fingerprint cache can also treat another worktree's workspace test binary as fresh. Confirm the expected test names, not only Cargo's fresh marker.
Refreshing workspace source timestamps did not prevent a later build from reusing another worktree's presentation library; the compiler then rejected the missing menu exports.
Verification now uses CARGO_INCREMENTAL=0 plus executable snapshots and checks the expected test names. External dependency artifacts remain reusable.
One snapshot compile batch failed, but the initial JSON collector did not forward Cargo's compiler-message records, so its exact diagnostic was lost.
The collector now forwards rendered diagnostics; the next foreground compile passed. No source change was made to hide that unexplained build failure.
Automatic approval review refused deletion of the duplicate ticket081proof/test-bin/quarrel-client/quarrel-client.exe artifact with 'blocked by policy'; it remains.
The print/exec session's GDI CopyFromScreen call returned 'The handle is invalid' after the menu window centre was verified on monitor four.
That optional desktop image is invalid evidence. Native verification uses the exact PID/window visibility and placement records plus the scripted match trace; rendered views come from GPU headless captures.
The disposable native windows were closed. No placement failure or window exposure on another monitor occurred.
