# Connected rematch and two-half match observations

The connected interval in `reference/MedalTVRounds20260903165304088.mp4` starts at PTS 1595160286 and ends at PTS 2351823926. The recording SHA-256 is `453954a7230401ed805be4e53dec41779a1913dfd69903671fc131fca2c8a18c`.
The timestamp time base is 1/10,000,000 second. Each identity below hashes exactly 3,686,400 native 1280×720 RGBA bytes, decoded directly with FFmpeg 7.1 and its libaom AV1 decoder.
The audit preserves source timestamps with `-copyts`, selects `eq(pts,PTS)`, and outputs one `format=rgba` frame with `-fps_mode passthrough`. Hashes refer to decoded pixels, not PNG encoding.

The earlier draft and combat observations remain in [rematch-draft-observations.md](rematch-draft-observations.md) and [timber-collapse-observations.md](timber-collapse-observations.md). Their source anchors still apply; this audit supplies the previously missing result and arena boundaries. Earlier clone hashes in those records describe their original delivered builds.

## Exact source boundaries

| Frame | PTS | Native RGBA SHA-256 | Direct observation |
|---|---:|---|---|
| prior-victory | 1595160286 | `8ed98510d1b11746b36f4bee6d577e2d42aef6f8012fa63c5eae07a7de8cf010` | Pink arena and both prior card stacks remain visible before the victory overlay. |
| half-blue | 2029991880 | `c5c3b9d31675d2317020c1f968ed0c4688656612f2e72e9550d9542e832e1a94` | HALF BLUE overlays the outgoing arena; only the blue left semicircle is filled. Da and Ex persist. |
| half-blue-tail | 2045158486 | `b0307fab233516c2cea436aab85c5e29bfdd8c55d3fc238a6f11d6bef996e02b` | HALF BLUE persists while the next timber floor and faint intact structure appear behind it. |
| timber-load | 2050158466 | `9b1e056c1d00ed9890a4ae941f62ef4d9b6112a04eb04156a394d5fa3ae8631d` | The result overlay has cleared. Intact timber, suspended weights, pink floor and both fighters are visible; Da and Ex persist. |
| half-orange | 2340157306 | `66d02502fd61186fff2da80ae1775a9b0546a02cf9310962b25e5a033f11f0e7` | HALF ORANGE overlays the collapsed timber arena. Both orange and blue left semicircles are filled, and Da and Ex persist. |
| last-result-only | 2351823926 | `9438b1bb537ad6b651729ed374d9f5ddf6d9badfa232df6cb34779ed03647ab2` | The last result-only frame retains HALF ORANGE, both half awards and the card badges. The timber pile and floor have transitioned away. |
| first-ice-excluded | 2351990592 | `ca82da2a0b4dead0fafd53be2ba3a3d6c8317b9c76649ebcbe7ce35655bb5405` | The next frame introduces pale ice geometry at the lower-right edge; it is outside the connected slice. |

All seven hashes were reproduced on 2026-09-05. Original-resolution source PNGs and the commands used are retained under `out/ticket-045/source-audit/`; `exact-pts-audit.json` also binds the full recording hash. The result-only endpoint is followed immediately by the excluded ice frame, rather than a later representative timestamp.

## Result onset and impact timing

The interrupted implementation initially labeled blue tick 2579 and orange tick 4440 as result onset. Direct adjacent-frame inspection disproved both labels: those source frames still show undimmed combat and elimination particles. The corrected boundaries below map to blue ticks 2585/2586 and orange ticks 4449/4450 from the interval's native starting PTS.

| Color | PTS | Native RGBA SHA-256 | Direct observation |
|---|---:|---|---|
| blue | 2025991896 | `32339820f8a05a839e3d315497cf614306f74340356d8de2237d7c72036b9687` | Final undimmed combat and elimination particles before the blue result. |
| blue | 2026158562 | `5c263f881a9050501566444fdb0f84bc7b7664ed5ed8e4aabd3f58ade637b317` | First dimmed blue-result frame with small empty circles; the award fills later. |
| orange | 2336657320 | `cfb81cd5b5fcd99bda0441cecf9afe8d5f82d83203a86b11e1fabca681885738` | Final undimmed combat and elimination particles before the orange result. |
| orange | 2336823986 | `2c903dfcc768591523211289f6dc3357631e010a8102dbcfdd2c72932c235bf8` | First dimmed orange-result frame: blue retains its half while orange is still empty. |

The first connected timber trace also reused the old clip's impact offset from the newly connected arena load. Those starts differ by almost a second: native PTS 2194157890 still shows intact timber and combat, while PTS 2204157850 shows the compact upper-left explosion (RGBA `d3d536325778594946b91171a42eea289fcbac84ed3dd4cd825ca31600e680ce`). The latter maps to connected tick 3653. Source inputs must account for that gap; changing an anchor's label cannot correct early gameplay.

## Scope and interpretation

The source establishes the visible order, selected card badges, two half awards, arena handoff and collapse. It does not expose hidden damage formulas, exact physics constants, original networking, or which physical inputs caused each action.
The clone composes its existing Dazzle, Explosive Bullet, Rapier timber, flow, UDP and shared Bevy scene implementations. The source-shaped action trace remains an approximation of player choices. Other card mechanics, the following ice arena, the rest of both recordings, production multiplayer and audio fidelity remain outside this slice.

## Anchor timing

The thirteen inherited draft anchors retain their checked source timestamps on the earlier 02:40.00 clock, while the newly audited connected boundaries use the exact 02:39.5160286 interval start. Their named poses therefore preserve an approximately 0.484-second timing approximation inherited from ticket 041; metadata binds each actual source frame rather than claiming one uniform frame-for-frame clock across the whole sequence. The later result and timber anchors are bound independently to native PTS, including the corrected adjacent onsets.

## Verified connected build

The final client executable SHA-256 is `9755f3822572ce43981aa63419ede7c3d0279a4024efaaf5724856e0b4042fae`. Its seed-41 session ends at tick 4540 with state SHA-256 `1ababf3565b7a0e139fced028bb2e2809f341806e97be43c6f26101c963c808d`, both drafted cards retained and a 1–1 half score. The ordinary blue projectile contacts the upper-left timber surface at tick 3653 and releases 17 fixed joints. Both elimination results follow ordinary damage events; the arena load preserves the authority and revives fighters beneath the preceding result overlay.

`out/ticket-045/delivery-anchors.json` binds 25 shared-GPU PNGs to the executable, source PTS/native hashes, input trace and authority state. Every PNG and source identity was checked. All original candidate frames were inspected at 1280×720; all changed corrected frames were inspected again, and the unchanged frames were verified byte-identical. The earlier `final-*` artifacts belong to the rejected visual candidate, and `verified-*` belong to the candidate rejected for releasing timber supports on a separate weight hit. Those artifacts remain diagnostic history; use `delivery-*` for this delivery.

`out/ticket-045/checks/delivery-smoke.log` records two separately launched UDP clients agreeing on every one of 4,540 progressive snapshots, all 18 phase entries, both loadouts, physics and final state. `delivery-smoke/live-client-0.png` is the received-state GPU frame; its PNG hash `5d421fc6c19f098d051e0698efad6f841defef05d85264b31ebb9deea5bb146a` also matches the local final anchor. `checks/delivery-visible.log` records hidden-first placement verification on the 1920×1080 monitor at (364, −1080) and the actual visible authority returning the same final state. The bounded processes exited successfully.

Format, strict workspace/all-target lint, locked workspace build and all 40 tests passed sequentially against the reusable two-job Cargo target. Tests comprise 24 simulation, four network, five presentation, four capture CLI and three automation checks, with no failures or ignored tests. The connected regressions exercise both halves, progressive observation-driven aim, draft-card perturbation and removal of the actual timber impact input. Keyboard/controller mapping is covered through the Bevy input boundary; a physical controller was not operated during this verification. Audio and production transport behavior remain unverified and out of scope.

Original-resolution comparison confirms the result order, clear result text, previous half preservation, persistent Da/Ex badges, revived fighters at the arena handoff, compact upper-left explosion, physical timber collapse and a result-only endpoint without ice. Remaining differences are visible: blue climbs and fires from the left roof while the source shooter is on the right, and the burst center is approximately 25 pixels above the source. Fighter poses, typography, arena proportions, pile arrangement and camera/effect details retain the approximations documented for tickets 040 and 041. This is a continuous playable slice, not a claim of pixel-identical footage.

## Responsibility inventory

The change reuses one `AuthoritativeMatch`, the existing ECS arena constructors and Rapier world, one received-snapshot renderer, the current UDP adapter, and the existing capture/playback commands. None of the five `ReplayProfile` variants, CLI modes, launchers, renderer paths or network adapters were added. Approximate added-line counts, including refactoring, are 480 for authority/physics/flow/semantic input, 231 for shared rendering and human controls, and 23 for the UDP adapter: 734 product lines. Tests add 279 lines; conservatively counting every client and automation addition as support adds 174, for 453 combined test/support lines. Support growth remains below the product behavior added.

The independent review's weight-contact reproduction is now a public-input regression. It failed before the correction with 17 released supports and passes with zero, while retaining the weight explosion. The complete two-client source reports before and after the correction match, including all 4,540 snapshots and progressive state hash `46de8943c2979fd2b443bae4ef585d7181ec6824aee445e51f64f7683c330194`; `checks/weight-fix-source-identity.json` records the executable identities and comparison.
