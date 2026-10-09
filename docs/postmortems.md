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

## 2026-10-06 — Ticket 81 interactive cancellation changed the headless proof's exit condition

The delayed menu join test reached play but failed its old successful-exit assertion after the host's scripted controller cancelled at its observation tick.
Interactive cancellation has no bounded terminal handshake; the remaining peer detects authority silence, so a final unacknowledged snapshot need not satisfy both observation windows.
The proof now requires each peer's first-fight event and permits only successful local cancellation or the documented authority-silence disconnect.
Other startup and transport errors still fail it. Bounded automation's terminal acknowledgement tests remain unchanged.

## 2026-10-06 — Ticket 81 uncapped waiting exposed a missing local-authority failure signal

A host with a malformed card file failed before welcoming peers, but its newly uncapped client kept waiting because the authority thread's error was read only after presentation returned.
The headless reproduction timed out with only a listening event. Interactive host preparation now gives the authority a client handle; an authority failure cancels waiting and returns its original error after both threads join.
A public CLI regression uses a malformed card fixture and requires prompt failure naming that file. This preserves indefinite waiting for an absent remote host while ending a locally failed host.

The first startup fixture used JSON, which the RON card loader ignores; the reproduced authority failure was an empty card pool rather than a parse failure.
Waiting cancellation already restored the intended error exit. The regression now uses malformed RON and checks its file-specific error; runtime code is unchanged by that fixture correction.

## 2026-10-06 — Ticket 81 window inspector sampled an unfinished hidden window

The native menu inspector once rejected its centre and immediately closed the exact process; the application log subsequently recorded monitor-four placement.
The original failure did not retain its rectangle. A fast startup trace then found hidden Join and Host windows outside the target before their placement marker, with no visible window outside monitor four.
The helper had treated native handle creation as completed placement. It now checks all visible startup samples, waits for the application's placement marker, then checks the exact physical rectangle before showing or focusing.
Per-monitor DPI coordinates are explicit and restored after inspection; paired legacy and physical measurements were equal in the controlled menu run, so DPI virtualization was not the demonstrated cause.
The repeated menu, Join, Host and scripted local checks passed, and every exact native process closed. Required views remain GPU headless captures.

## Ticket 81 correction review: menu Join omitted interactive mode

The network constructor and native waiting checks worked, but the menu Join argument list still selected bounded mode. Review reproduced its five-second timeout through `menu-start --choice 2`. Earlier evidence started Host first, hiding this boundary error, and checked the waiting window through explicit CLI flags. Join now passes `--interactive`; a public client regression starts Join first, waits beyond the old deadline, then starts Host and requires both first-fight events. Native waiting evidence now also uses the menu Join translation and verifies client 1. The actual missing argument was corrected; no timeout was enlarged.

## 2026-10-06 — Ticket [#79](http://ivy.localhost/tickets/79?repo=rounds-clone) combat regression findings

The first recorded-match regression still used inputs authored for straight bullets and one-hit kills; those inputs never completed a fight under the new baseline.
Regenerate this development fixture through the public record command and pin its tuning with its other starting content.
A ground-jump test caught a spent jump being restored on the launch tick. Rapier retains the contact manifold from the solve even after the fighter moves away.
Support now checks velocity relative to the contacted body along the contact normal, so separating contacts cannot restore a jump; the immediate repress regression remains.
Crouch initially changed height around the fighter center, temporarily removing floor contact and restoring standing height next tick.
Keep the feet anchored on shape changes and check held crouch across thirty physics ticks, including the actual collision bounds.

A shared-target workspace test command succeeded with sixteen simulation tests although this worktree contains twenty-six.
Another worker's newer package artifacts were considered fresh across checkouts. Cargo's build lock prevents concurrent compilation but does not make different worktrees' source contents identical.
Do not count that result as candidate evidence. Refreshing timestamps before entering the build queue was insufficient: a later compile received another branch's snapshot types.
Keep local source timestamps ahead of the build queue temporarily to force local rebuilds without discarding dependency artifacts.
Ask Cargo to build every workspace test executable, copy those executables into this ticket's artifact directory immediately, then run each copy serially and confirm the new regression names.
Restore ordinary timestamps after verification. This isolates test execution as well as compilation; a separate native target was initially impractical with less than 1 GiB free.
Session ownership initially refused because the launched Codex inherited the parent's Claude identity too. Pass `--session codex:<CODEX_THREAD_ID>` explicitly to the helper; environment enumeration is unnecessary.

The final reflection render showed a shot still inside its blocker on the next tick. Its recorded horizontal velocity had changed back to +1497.09 pixels/second after the authority reversed it.
The swept hit was detected after its endpoint had passed the fighter, so reversing there sent it through its new owner's collider.
Return from outside the reflector, using the incoming velocity saved before solver response. This retains ordinary fighter/projectile collision behaviour.
The reflection regression follows the next tick and checks both continued return velocity and return displacement, rather than only the instant reversal.

Rebasing onto [#82](http://ivy.localhost/tickets/82?repo=rounds-clone) reproduced two assumptions tied to the former baseline: four scheduled shots without reload, and three-player smoke completion across whichever maps exist today.
The stack test retains four shots and its toppling check, allowing the new reload. Match progression uses the recording's pinned starting content, while ordinary two-client smoke still checks the live pool.
The arcing shot needed a higher strike on Trestle's pier to topple it. A focused reproduction passed after aiming at its upper portion; the test still checks four actual shots and a toppled stack.

The first independent review found that checking crouch height alone had missed an 11-pixel gap under a stationary fighter.
The positional regression failed immediately with feet at -177.213 instead of the floor's -180, then showed the gap persisting during held crouch.
Changing the collider's shape in place kept its old solver contacts. Replacing that collider on stance changes gives fresh anchors while retaining the body, velocity and mass; crouch and stand-up now both keep feet on the floor.
Default recoil was only about 12 pixels/second at the chosen fighter mass. Raise its impulse to 800 and compare enabled versus disabled motion to check the 120 pixels/second cap with the shipped settings.
The three-fighter progression fixture now uses a flat arena and current tuning/cards, so regenerating the ordinary recording cannot change its pathfinding needs.

The second review approved the combat corrections but identified a tuning side effect. A damage-only edit recreated a crouched collider, removing support contacts before controls ran and restoring standing height for one tick.
A public watch-file regression reproduced height44000 instead of22000 on that tick. Only actual shape changes now recreate colliders; the same regression passes with continuous crouch height and floor contact.

The final delivery rebase met the landed menu ticket after verification, requiring another full check and review of the input reconciliation.
Keep its device assignment and camera cursor conversion, and correct its scripted S-block binding to Shift because S now crouches.
An initial conflict script used Windows' default text encoding and stopped before resolving anything; a chained Git step still staged conflict markers.
Abort that unfinished rebase, redo it with explicit UTF-8 and checked exit status, and reject remaining markers before continuing. No invalid commit was published.

## 2026-10-06 — Buffered combat presentation blocks ticket [#83](http://ivy.localhost/tickets/83?repo=rounds-clone)

Three independent reviews exposed separate gaps in the online presentation. Earlier corrections fixed projectile bounce/freeze, buffered aim, floor support, mouse origin and terminal wall contact.
The last candidate passed 88 tests, strict Clippy, format, locked build, renders and agreeing two-client smoke; the impaired 32-piece probe measured one scene frame and 23,086.5 payload B/s per peer.
Those gates did not cover timeline coherence. The [third review](http://ivy.localhost/sessions/claude/7e224bc4-45be-4714-ada8-11cec4737c98) found the Result transition clearing a 200 ms world buffer and current damage paired with older incoming shots.
New boundary regressions fail for both and for a kinematic support kept fixed by the local predictor. They are saved as an uncommitted reproduction patch, not failing repository tests.
The [consultation](http://ivy.localhost/sessions/claude/1e60ef7f-1ce5-426a-925e-0e10136083ea) rejected simply drawing current shots over buffered fighters: it creates a launch gap and leaves owned-shot impacts at current proxies away from drawn targets.
Accepting that gap, time-warping bullets or replacing buffered fighters with a current predicted scene needs an operator presentation decision. The chosen model was explicit, so only this ticket is blocked.

The unapproved range is `5889452dd6713c7679e4a8135f8f80a9a4404325..c3f5d614dc8d4bcf0803c6691936d4908486babc`.
It stays in `.ivy/worktrees/083-responsive-online-correction` and `refs/ivy/candidates/83-review3`; the original review checkout stays intact. No game code from either is on `origin/main`.
Review/consult results, full test output, frame images and the exact failing-fixture patch are retained under ignored `out/netcode-evidence/`.
Repeated full checks and serial reviews added cost. One full test output was lost at a context boundary and repeated from cache to retain checkable evidence; keep tool session identifiers and output files before image-heavy calls.
Display scanout, actual two-home routing and predictor performance on shipped outlined arenas remain unmeasured. After the decision, verify timeline boundaries and those arenas rather than treating the 32-box CPU measurement as a full frame-budget result.

All four frame PNGs and the smoke terminal JSON are now retained. The three failure stdout files accompany the reproduction patch; Cargo's expected test-failure exit code is 101, not the initial wrapper's mistaken 1.

## 2026-10-07 — Unattended Codex launch could not load its configuration

Run #75 could not resume the existing card worker because Ivy's canary exited before reaching the guard command.
The CLI reported `Error loading config.toml: invalid transport in mcp_servers.code-review`.
No worker code ran and neither existing MVP worker's claim or worktree was replaced.
The Claude fallback passed the guard check and launched ticket #73.
Further read-only checks ruled out the proposed CLI-version mismatch: npm 0.160.1 and bundled 0.162.0-alpha.2 both list the unchanged MCP configuration successfully.
Adding Ivy's `mcp_servers.code-review.enabled=false` override makes the bundled CLI fail configuration loading with the same transport error.
The bundled binary's canary failed too when using Ivy's credential withholding. No worker resumed through either failed route, and no guard was bypassed.
The failure is isolated to the disable override; the underlying CLI merge mechanism remains unconfirmed. Ivy's launcher needs a compatible withholding operation before these sessions can resume.
A self-contained reproduction report is retained under `out/ivy-codex-launcher-report-01a114f0.md`; filing it in Ivy's private issue tracker awaits operator approval.
The first metadata search also unnecessarily scanned large Ivy session caches. Subsequent port discovery reads the user-level `~/.ivy/server.json` directly.
Ivy's local usage endpoint was also unavailable, so the run could not compare Claude and Codex headroom.

## 2026-10-07 — Local UDP verification and overlapping Cargo runs

Windows Firewall prompted for the network test executable during unattended verification.
Both live and replay clients bound all interfaces even when their authority was on loopback.
Both paths now share a socket binding rule: loopback authorities use loopback; remote authorities use the matching address family's wildcard.
Regressions inspect real socket addresses without sending traffic to the remote test addresses.

The first review caught the replay client path missing from the initial live-client fix; it was corrected before publication.
After all 26 network tests passed, rustdoc failed because a shared simulation rlib was missing.
An autonomy worker was compiling into the same prepared Cargo target when inspected; the rlib had reappeared by then.
The next runs executed the other checkout's test binary: its three new tests appeared, while this fix's two regressions were absent.
Cargo also reused that binary on a no-run build. Waiting for Cargo alone did not establish artifact attribution.
Verification now refreshes this crate's source timestamps before rebuilding, freezes the executable outside the shared target,
and checks that both regressions are present before running it. The prepared target and two-job cap are retained.
All 26 tests then passed from the frozen executable; format, strict network Clippy and documentation tests passed.
The frozen automation smoke completed 2,400 ticks through opening draft, loser draft and match end with agreeing clients.
The firewall dialog itself was not captured after the change; the automated evidence verifies the socket bindings and local UDP sessions.

## Guard judgments

The ownership refusal below was a routine handoff check, with no recorded loss of work.
Ticket #59 records the old owner being released and the new coordinator taking ownership about one minute later, following Adam's renewed instruction.

- **Guard evidence:** Ticket #59; occurrence 1; `- 2026-10-02T14:30:40Z guard session codex:01a0fd03-d222-7cf2-a225-29c243f2337e — own refused: #59 is owned by codex:01a0fb88-1e6f-7a80-88cc-62fc856e1443; Ivy has no live observation that the session ended; release the ticket to hand it over`; judged by codex:01a114f0-227b-7711-828e-c62f2606df5c.

## 2026-10-06 — Shared Cargo target replaced a CLI under ticket 80's tests

The card sim passed, but `cargo test --workspace --locked -- --test-threads=1` launched a shared `quarrel-client.exe` that rejected the new card fields.
The error named the old four-field StatChanges parser; ticket 79 was compiling against the same prepared target at the time.
Cargo's native build-directory lock serializes compilation, not later integration-test access to a shared executable.
The capture tests now accept QUARREL_TEST_CLIENT_EXE, and this worker freezes and hashes the just-built client before testing and rendering.
This preserves the shared dependency cache and the two-job cap while binding CLI evidence to the candidate binary.

A subsequent build also reused old simulation metadata, so freezing only the client was not enough.
Ticket 87 independently records the shared-package timestamp collision and owns the durable build correction.
For this candidate, source timestamps force local workspace compilation; Cargo artifact messages supply frozen test and product executables.
Copies stay under this real worktree, are hashed and checked against compiler dependency paths, and run directly so later builds cannot replace them.
No package clean or cold dependency build was executed. The consultant confirmed this route and noted that the PDB error's cause remains unconfirmed.

The artifact guard initially checked the top-level product dependency file as soon as Cargo emitted an executable.
Cargo had not yet replaced that aggregate file, so the guard rejected it and closing its output pipe stopped the build.
Product evidence now binds the fresh compiler artifact to this worktree through manifest_path and target.src_path; test dependency files remain checked.

A PowerShell verification command continued after a rebase stopped on a records conflict.
That run checked an intermediate tree, so it was stopped by terminating only its verified Python/Cargo process tree and its evidence was rejected.
Finish reconciliation in a separate successful command before launching dependent verification; native command failures do not stop PowerShell automatically.

The final frozen suite once reported authority_silent from the existing FIFO live-network fixture; the same executable passed the focused reproduction immediately.
The failure log is retained as network-rejected.log. Network production and fixture source bytes are unchanged by this card candidate.
The complete serial network suite is checked again after native builds and captures finish; this does not claim to fix the intermittent failure.
Ticket 85 owns UDP fixture isolation, and ticket 86 owns the broader networking regression coverage.

## 2026-10-06 — Repeated card impacts could end a live match

The independent review of ticket 80 reproduced 334 explosion records after twelve thousand ticks of firing into terrain in one fight.
The compact snapshot reached 69,739 bytes, beyond the live UDP packet limit, because visual impacts were kept until a fight reset.
The renderer only needed twelve ticks of history. The authority now expires those records and retains at most sixty-four, while metrics remain cumulative.
A three-thousand-tick regression fires the explosive bounce/spray combination and checks history age, count, packet headroom and continuing explosions.
A second reproduction showed a fast projectile placing its blast beyond the victim; hit rules now use the first swept contact instead of the final bullet pose.
Regressions also exposed fading flight modifiers and a full poison queue dropping primary abilities. The corrected suite covers both.

## 2026-10-06 — Rapid cards exposed an inspection-only snapshot limit

Fresh review reproduced a newest-shot visibility failure with two Hailstorms: sixty-four old upward shots filled the snapshot, hiding all eight shots later aimed at the opponent. The authority retained those new shots and could still deal damage, so rendering and gameplay disagreed. Snapshots now keep the latest sixty-four in stable ID order, with a public-input regression. Flight-card branches also bypassed breakable damage; piece contact is now handled before those branches, with one damage application per continuous contact.

The first correction used swept object rays for ordinary physical shots as well as drilling sensors. That consumed shots before their native collision impulse and broke the existing trestle-toppling regression. Swept detection is required for drilling sensors only; ordinary shots retain native contact timing. The failing complete simulation run is retained with the corrected evidence.


## 2026-10-06 — Ticket 80 magazine reconciliation exposed weak stream and split effect results

The fourth independent review used sustained public replays after the native magazine landed. Hailstorm fired only 21 shots in 600 ticks versus 14 ordinary shots, with much lower damage, because the card changed cadence but retained a three-round magazine. It now supplies a stackable larger magazine, with a ten-second firing regression and the original ordinary weapon as control. Another reproduction showed reaction explosions displaying their ring and shove while damage independently failed its deeper fade roll. Damage now follows the selected effect; generated triggers still fade. The effect regression failed with 60 damage instead of 84 before correction and also checks all eighteen reaction poison pulses.

The earlier frozen-client hook name QUARREL_TEST_CLIENT_EXE describes the pre-menu candidate. After the menu delivery, both suites use QUARREL_TEST_CLIENT; the final verification manifest names that current hook. Older reviews and their exact products remain archived as rejected or superseded evidence.


## 2026-10-06 — Ticket 80 open-floor blink evidence missed terrain traps

The fifth fresh review followed Blink Step after downward and outward blocks on shipped geometry. Unchecked move_player placed the fighter beneath the floor or behind a side wall; the floor prevented recovery and repeated edge returns killed the holder. The review reproduced 41 teleport-only deaths in 57 arena/direction cases with surviving no-teleport controls. A Gatefall downward regression failed before correction. Blink now sweeps the actual fighter shape against live terrain and clips travel to the frame. Open-space distance and movement remain covered, and the shipped sweep is repeated through the frozen public client.

The chained reviews and repeated native builds were costly because early evidence stayed on an open lab floor and did not follow movement consequences on shipped terrain. Retain both small boundary regressions and the public shipped-arena sweep; another lab-only capture would not have prevented this failure. No clean target was used, and builds reused the shared cache under its lock and two-job cap.

The full simulation check exposed a damage-event fixture aiming its teleport deeper into a contacting saw. The corrected safety constraint stops that path. The fixture now aims away from the hazard and still requires the same-tick TakeDamage teleport, rather than bypassing obstacle checks or weakening the timing assertion.

The first sweep check incorrectly required survival while idle after every direction, including blinking off a small platform. It reported three remaining falls after the collision fix. Public recovery replays for ice, teal and yellow-crate each return to their platform at full health with no edge returns; the trapped Gatefall case could not do that before correction. Verification now names these cases and checks recovery instead of requiring blink to prevent ordinary falling. The failed zero-fall assertion is retained, and no native rebuild was repeated for this evidence correction.


## 2026-10-06 — Ticket 80 shape cast disagreed with native resting contact

The sixth fresh review found 28 cancelled standing blinks across 186 grounded horizontal cases, including a required combo that teleported once instead of twice. The stopping cast treated a shallow floor contact as approach because its normal had a tiny horizontal component. The exact flat-floor boundary regression failed at x=-492.282 with zero travel; the diagnostic measured approach -0.0000008306038 at time zero. Blink now honors the native physics penetration allowance for near-start tangent contacts, while genuine approaches still stop. The temporary diagnostic is removed and retained in blink-contact-red.log; the small regression covers sixty positions and directions.

The later journal rebase script used a DOTALL expression with unbounded marker-line matches, so it could consume text beyond a conflict. Rebase stopped before checks or publication. Abort that attempt and rebuild both records from committed upstream text plus the exact owned suffix; verify every upstream nonblank line remains. Consolidate the six unpublished owned commits before resolving the append conflict once. The preserved candidate ref and old review artifacts retain the earlier range. No rejected reconciliation result was verified or published.

## 2026-10-09 — Ticket 80: a support contact hid another part of a concave collider

Fresh review 8 found that the numerical tangent-contact correction skipped the entire collider. A concave arena outline becomes a compound collider, so its floor and column can share a handle. On shipped Lime, public inputs moved the fighter from -495.731 to -585.731 through cross 9; splitting that cross into rectangles stopped the blink at -500. A lab reproduction also left the fighter embedded. The earlier tests used separate floor and wall colliders and did not cover this boundary.

Cast each convex part and apply the starting-contact tolerance to that part alone. The new shipped-arena regression produced the exact red displacement and then passed after correction, including ordinary movement away from the wall. Keep the raw verdict, exact recordings, source hashes and rejected client in out/ticket080proof. Repeated complete review rounds have been costly; this round found a supported boundary defect, so delivery still requires the correction and fresh approval. No clean build or private Cargo target was used.

The first correction proof also asserted full health after 90 ticks of continuous jump-left. Once the trapped fighter could move again, that input carried it into the arena's damaging edge. Keep compound-public-overstrict.log and the complete original recording. Verify ordinary walking recovery for 90 ticks and the first ten ticks of the same jumping escape; both stay healthy and move away from the wall. This corrects the proof's assumption without changing movement or edge damage.

The following review was interrupted when the launched worker and supervisor disappeared. Its JSON result was empty, no matching reviewer process remained, and its scratch directory ended with an incomplete probe file. No verdict or approval was recoverable. Preserve review-9-interrupted-artifacts and the exact candidate ref, then start a fresh review. The already verified source and copied binaries are unchanged; do not repeat a native build. Resolve the machine-selected skill junction to the canonical Playbook before invoking sibling helpers.

## 2026-10-09 — Ticket 80: Blink stopped at a later floor corner

Review 10 confirmed the per-part fix prevented compound wall crossings, then found a separate short Blink on unmodified Teal. The starting floor and the next platform share a height, but native resting contact sinks the body by about 0.011 px. A zero-time contact exception did not cover that later platform corner. The existing horizontal sweep only rejected near-zero displacement, so a partial 52 px Blink escaped its predicate.

Add a two-platform gap regression that requires the full 90 px in both stances and a healthy grounded landing after settling. Apply the existing contact allowance to the cast shape along the entire path and remove the initial-contact exception. Preserve the raw rejection, exact public Teal inputs and earlier column/trap controls. The separate-platform test failed before this correction and the six focused Blink regressions passed afterward. Refresh replay states and captures against the new product; ordinary geometry bumps larger than the allowance remain solid.

## 2026-10-09 — Firewall prompts returned from the regression tests

Adam reported recurring approval popups during verification even with UAC set to Never notify. His photo showed Windows Firewall asking about `quarrel_network-3d1ebb929b1d7149.exe`, and the firewall log showed rules being added for the frozen `quarrel-client.exe` copies in several ticket 87 worktrees.
The 2026-10-07 fix kept loopback play on loopback, but its regressions proved the remote choice by actually binding `0.0.0.0` and `::` for documentation-only addresses (`quarrel-network` `lib.rs` and `live.rs`).
The menu network tests also started menu Host, which binds `0.0.0.0` for LAN play.
Ticket 87 then gave every worktree its own frozen test and client copies, so each new path prompted again; allow rules on older paths had hidden the problem.
Tests now check the remote address choice without binding it, and pass `--bind 127.0.0.1` to menu Host. A real socket in a test must not prove that it can listen on every interface.

## 2026-10-09 — Ticket 91's contract asked tests to yield, which broke a timing check

Ticket 91 was admitted with test executables running at BelowNormal priority alongside the compiler and linker. On the third full verification at that priority, `udp_delivery_observes_delay_jitter_and_loss` delivered 879 of the required 950 datagrams in its 250 ms window during an unrelated process's paging burst. In isolated runs under an 18-thread Normal-priority CPU load, it failed 9 of 12 times at BelowNormal and 2 of 12 at Normal. The ticket went back to blocked, a fresh Codex context re-admitted an amendment that runs tests at the caller's priority, and the work continued.
The test still fails under heavy Normal-priority load at Normal priority, because its 250 ms wall-clock window has no margin for a stalled sender. That fragility predates ticket 91 and is not addressed here.

The first Codex review also returned no result: `codex exec -s read-only` could not start its elevated Windows sandbox (`helper_unknown_error: setup refresh had errors`), so every tool call failed. The rerun used the operator's configured `danger-full-access` default, with the prompt keeping the reviewer read-only; the worktree stayed clean.

## 2026-10-09 — A superseded orchestrator resumed an active worker

Adam invoked autonomy again while the previous Claude orchestrator was waiting for ticket 91. The new Codex session took ownership of goal 75 and kept that worker running. After 91 closed, the new owner resumed the existing online-play session at 05:34:44Z as `81a98009-abed-4273-a38e-ba9d7165b613`. The old orchestrator also resumed it at 05:35:13Z as `77877e5c-b761-4f86-8e85-34376e83d58f`.

The duplicate launch exited with code 1 at 05:35:15Z. Each JSON launch record names its launching session, and a subsequent goal check still named the Codex owner. The original worker remained active. The stale orchestrator exited on its own before any interruption was issued; no replacement worker was launched. These observations establish overlapping dispatch, not lost work, delivery, or the reason the duplicate CLI failed.

The autonomy skill already requires checking goal ownership before each step and forbids resuming an active worker. The superseded session's reasoning and the duplicate exit's cause were not inspected, so the reason it missed that precondition is unknown. A prepared Ivy report proposes considering an ownership check at the worker launcher, with legitimate reviewer launches and handoffs preserved. No public issue has been filed or new guidance imposed.

Evidence remains in the two unattended JSON records under Ivy's home and [goal 75's work log](http://ivy.localhost/tickets/75?repo=72104b08f3e558c1). The [current orchestrator](http://ivy.localhost/sessions/codex/01a11f02-1a85-7e42-b5c7-6b3d99c6940c) observed the [superseded session](http://ivy.localhost/sessions/claude/a0d86392-754e-470a-b30a-fec894c3f7c6) gone before trying any interruption. The incident investigation and this journal change ran no Cargo build, changed no compiler settings, and retained the prepared target.
## 2026-10-06 — Ticket #83 prediction and remote-shot correction

The first complete checks passed, but the [fresh Claude review](http://ivy.localhost/sessions/claude/e33188b8-f727-4d9f-a071-eb6b0724346d) rejected the candidate's shot presentation.
Fixed opponent proxies physically bounced local shot visuals, and remote shots absent from the next sample froze at their last position.
The retained regressions reproduced both failures. The predictor now suppresses shots at swept or physical fighter contact without deciding a hit, and the final remote interval follows observed velocity.
The author also reproduced discarded ground-support contacts: pose restoration replaced an unchanged collider after the warm-up step.
Preserving its identity restored support and authoritative ground control. A second detached checkout kept the ongoing review's original range immutable during reproduction.
Remote aim and stance now use the buffered moment. The impairment probe now compares a neutral scene without an owned shot or active block, removing ambiguity in its one-frame assertion.

The [second Claude review](http://ivy.localhost/sessions/claude/a9af5c9a-ed42-49a6-b303-85f11b1b2c8a) confirmed the first corrections and found an input-origin regression.
Snapshot polling overwrote the predicted pose just before mouse aim, sending a direction from an older host position ten times per second.
A real live-UDP/Bevy polling regression failed with -500000 instead of the drawn -400000 milli. Bootstrap-only sampling now preserves the drawn origin.
The same review identified velocity-only remote shots crossing walls. The retained wall fixture failed, then passed using existing presentation-only CCD and fractional tick interpolation.
The remote wall-contact render fixture adds pixel evidence; no additional damage, reflection or flow authority was introduced.

The final full test output was lost at a context boundary, requiring one cached serial repeat to preserve checkable results.
The installed Ivy check-all script expects a Playbook repository and cannot run here because playbook/skills is absent; the project's CI gates were run instead.


## 2026-10-09 — Ticket #83 resumed after an incomplete worker exit

The October 6 worker stopped with an unpublished four-commit candidate and three supported review findings. No game delivery was claimed. The resumed owning session kept its claim and candidate, reconciled delivered dependencies, and reproduced all three findings through tools/verify.ps1: an unprepared arena at Combat entry, friction stopping a grounded remote runner, and double-counted fixed-saw rotation. The complete run passed the other tests and reported exactly those three failures; no required check was skipped.

The earlier candidate record incorrectly claimed preparation during Draft or Countdown. No Countdown phase exists, and Draft previously contained the preceding arena. The old timing was steady-state only. The correction prepares the same seeded upcoming arena when Draft begins and tests the real transition. The earlier incorrect claim in docs/design-docs/postmortems.md (the common-scene geometry entry) and docs/decisions.md remains historical and is superseded by this entry and the corresponding decision.

The installed skill directory is a junction to the active Playbook's skills. Invoking ticket-tools.ps1 through the junction made PowerShell resolve its parent against .codex/skills and report the helper missing. Invoking the same file through its verified physical Playbook path restored the release-matched helper; neither a different helper version nor the root checkout was substituted. Current main's supported verifier remains unchanged.

## 2026-10-09 — Ticket #83 verification linker failure with low disk space

The first corrected verification stopped at quarrel-server link with LNK1318 LIMIT (12); its tests did not run. C: had 219,602,944 bytes free and the named PDB was 417,521,586 bytes. The source gates had passed. Reversible NTFS compression of the existing prepared target at BelowNormal priority retains its artifacts and restores linking headroom. The error and compression report remain under out/netcode-evidence; the PDB error's cause is not claimed proven solely by low free space. Complete verification must run again before delivery.

The complete rerun after compression linked successfully and passed 152 tests, including all three review regressions; the one remaining failure was the 32-piece bandwidth check at 33,642.6 B/s per peer. The larger reconciled card payload exceeded the old 10 Hz budget. The same consultant recommended 7.5 Hz independent samples, with its longer correction interval recorded. The limit and all required checks remain unchanged; another complete supported run will verify the correction. Both frozen seeded ordinary matches retained their exact before-correction Combat hashes. The real first-Combat fixture measured 335.010 ms of Draft preparation and 0.700 ms on Combat entry, superseding the earlier artificial timing claim.

The cadence correction passed the complete supported route: all 153 tests, format, strict Clippy, locked build and doctests. Its attributed frozen impaired fixture measured 24,190.8 B/s per peer, one scene frame of local response and agreeing host/client outcomes; the real first Combat frame took 0.818 ms. The retained failing runs are superseded by this evidence, not erased.
