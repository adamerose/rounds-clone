# Connected ice duel and first round observations

The next connected interval in `reference/MedalTVRounds20260903165304088.mp4` runs from PTS 2351990592 through the selected established-round frame at PTS 2506156642. The recording SHA-256 is `453954a7230401ed805be4e53dec41779a1913dfd69903671fc131fca2c8a18c`.
This adds about 15.42 seconds after ticket 045's result-only endpoint. It covers the incoming ice arena, the deciding duel and blue's first full round. It stops before the next card draft; the chosen endpoint is an established result frame, not a claim to be the last frame of the result animation.

## Source identity

PTS uses a 1/10,000,000-second time base. Each SHA-256 below hashes exactly 3,686,400 native 1280×720 packed RGBA bytes from FFmpeg 7.1's libaom AV1 decode, with `-copyts` and `-fps_mode passthrough`. Source images and audit commands remain ignored under `out/ticket-046/`.
The bounded overview selected 18 frames at approximately one-second intervals from 235.2 through 253.2 seconds. Shorter bounded scans then established the terminal encounter and adjacent result onset. These probes preserve native timestamps; they do not interpolate source frames or hash PNG encoding.

| Anchor | PTS | Native RGBA SHA-256 | Direct observation |
|---|---:|---|---|
| ice-crossfade | 2351990592 | `ca82da2a0b4dead0fafd53be2ba3a3d6c8317b9c76649ebcbe7ce35655bb5405` | First pale ice geometry at lower right beneath HALF ORANGE; the immediately previous frame belongs to ticket 045. |
| ice-established | 2362323884 | `7b345dad4e6b7fba4f23b6c4622273b8361ddecf1e8ca600cf8c19edb1492436` | Both fighters stand on opposite outer spires. Pale platforms, cyan streaks and long navy shadows fill the dark teal arena; Da and Ex persist. |
| early-traversal | 2392823762 | `3d2ae2939f40a3a8d53191589ef5c42ab360a7242ce65d9590685069536b5ba4` | Orange reaches the left middle platform while blue moves beneath the central platform. |
| projectile-exchange | 2423323640 | `28a0c8a0adb0a913c22d878c090c052cff570109c36d2326e36ee6cd09866800` | Orange rises beside the upper-left column; blue is below center; a local yellow impact and particles remain visible near the left platform. |
| later-traversal | 2463990144 | `5992ac0074765324a238d19bb7b1c02158dc768db551a0c7273bac558a47f428` | Blue is beside the lower-left central column and orange above the broad central base. Platform topology remains intact. |
| terminal-approach | 2480156746 | `d0334341908395c4e5dc6095529c3e3f0594a4d357669d5c035d0facdf7973a3` | Both fighters overlap just left of the lower-left central column, around source x430/y415. Orange has very little health. |
| terminal-burst | 2480490078 | `e584b9d9f4607ba3d0bfdb60a574fcb1d4664e8e6cf6c754c4b2a661f7c484ab` | A compact yellow-white flash and sharp yellow rays cover that encounter, with chromatic separation across the scene. |
| terminal-response | 2480823410 | `85d609cbf409c8b974ea3ebd228b667af579e01335d2ace07108ac17676ae82d` | Blue remains visible above orange elimination fragments; local burst, camera displacement and color separation continue. |
| last-undimmed | 2484823394 | `c9ccbd8e1c67498d9a7f4c5438da5c36e9914e8826b9e553793a8375aa3c2c61` | Last undimmed frame before the result; orange fragments descend beneath surviving blue. |
| first-result | 2484990060 | `e757a34cb37134b6458c96cbdd4434a025e3984011d5b9d33daede652742cc74` | Immediately adjacent frame dims the arena and introduces small circles, each retaining its prior left half. |
| round-blue | 2487656716 | `8e1fe4a7514a96823783ed995657c1dac71247527666d4f9f10875119891ec83` | ROUND BLUE is readable; blue's circle is full, orange's circle remains half full, and the outgoing arena begins moving away. |
| round-pip | 2506156642 | `ae4d4d943d9ec939064a0d9d4a3b08a8d6f31cb820639d297bf9caf1c5333a32` | The first blue HUD pip is full; ROUND BLUE, Da and Ex remain visible; orange's half circle is partly below the bottom edge. No draft card is visible. |

The later probe at PTS 2510156626/hash `7c054315d2e42c582a69dc7985a0603e13abc78c333245b40d6aedc35ad17478` already shows QUICK SHOT entering behind ROUND BLUE and is outside this ticket. The following draft, its cards and the next combat need a separate delivery.

## What the footage establishes

The arena has bilateral outer spires, staggered small platforms, two upper columns, two lower central columns, a small high central platform, a wider middle platform and a broad stepped central base. The actual visible contours must also supply collision geometry; long shadows and painted cyan streaks are presentation. Between early and late combat anchors, the streaks change while the platform contours remain fixed. This interval does not establish slippery movement, ice fracture, melting, or a new material damage rule. The moving pieces during the result are part of the outgoing arena transition; they do not prove combat destruction.

The source reaches the arena with one half per player and zero completed rounds. Blue's next elimination win fills its second half and awards the first blue round. Orange's existing half remains on screen throughout the bound result. The footage here does not establish what happens to that half after the following draft, so a future-round reset rule must not be inferred from this endpoint. A faithful implementation needs distinct completed-round and current-half state; before this extension, the flow's single `scores` value represented prior-match rounds before rematch and half wins afterward. The connected implementation now keeps completed rounds and halves separately.

The terminal burst appears during a close encounter beside the column, after ordinary traversal and projectile exchanges. This is a source-shaped action target, not authority to eliminate orange at a fixed tick. Either player must be able to win from legal live inputs. Likewise, two wins by one player in the first two arenas already complete a round; that path must not display a second HALF or force an unnecessary deciding ice duel.

Existing Dazzle and Explosive Bullet behavior, Rapier contacts, shared event-driven effects, received-state rendering and semantic input already cover the required kinds of behavior. Layout, source-shaped actions, surface painting and result timing extend those paths. No independent ice replay profile, second renderer, new evidence launcher, unobserved card formula or production-network claim is needed.

## Review focus

Compare the incoming result/arena overlap, opposite spawns, traversable contours, mid-duel positions, local projectile feedback, close terminal burst, adjacent result onset, preserved half circles, completed blue round pip and disappearing arena at native resolution. The source table binds observable states and ordering; hidden original movement, damage and friction constants remain unknown. Existing ticket 045 anchors remain the evidence for the earlier continuous route.


## Implemented geometry and live behavior

Seventeen static polygons share their exact local vertices between Rapier collision and the Bevy scene. Representative native-image bounds are below; the offscreen bottom extension to y800 prevents an artificial floor edge at the viewport boundary.

| Surfaces | Native-image contour bounds |
|---|---|
| Upper columns, IDs 40–41 | x452–503 and 777–828, y127–229, including the narrow waist |
| High center platform, ID 42 | x596–683, y183–231, with clipped lower corners |
| Middle side platforms, IDs 43–44 | x316–402 and 878–964, y272–321 |
| Center platform, ID 45 | x570–710, y294–355 |
| Outer small platforms, IDs 46–47 | x164–251 and 1030–1117, y373–422 |
| Lower inner columns, IDs 48–49 | x452–503 and 777–828, y371–533 |
| Four lower spires, IDs 50–53 | narrow tops at y474, widening shoulders around y590–611 |
| Small lower points, IDs 54–55 | x181–233 and 1047–1099, y504–578 |
| Stepped base, ID 56 | top x609–670/y437; shoulders at y449/471/480/572; broad x425–855 base from y611 |

The source upper-right platform differs from its collision contour by approximately one pixel at its right/bottom boundaries. That silhouette difference does not explain the early traversal gap. Ice fighters use radius 12 collision circles and matching visible bodies; the earlier bounded arenas retain their established radius 22 collision configuration. These sizes fit the current arena projections, not a claim that the original fighters change size between arenas. General movement/camera calibration remains unresolved.

The same ECS fighters, Rapier fighter-body handles, selected Da/Ex loadouts and authority survive both arena handoffs. At ice load, all outgoing dynamic entities, colliders, constraints, projectiles and the timber anchor are gone; seventeen static bodies plus two fighter bodies remain. The earlier disabled first-arena bodies are retained only through timber to preserve its established Rapier insertion sequence, then removed at ice load. They have no collision membership or filter while retained.

The full public trace produces four deciding-fight damage hits at 5005/5044/5084/5312, with orange's health 100→75→50→25→0. The first dimmed result is 5339 and ROUND BLUE is 5355; elimination therefore precedes dimming by 27 ticks. Removing only the actual 5311 fire input keeps orange at 25 health and the authority in IceCombat through 5340. Separate ordinary-damage regressions exercise orange/blue sweeps and orange/blue deciding wins, preserve losing halves and prohibit re-awarding a held result.

## Native comparison and remaining differences

The source pairs are retained at 1280×720, with the twelve native hashes above. The following pose estimates are screen pixels from those images; they are distinct from quantized simulation-coordinate diagnostics.

- Incoming ice now keeps both fighters visible by interpolating from their actual outgoing timber positions. Those inherited poses differ substantially: clone blue is near (379,356) and orange (957,359), versus source blue (1084,416) and orange (462,503). The implementation preserves the connected authority instead of inserting the source poses into a new state.
- The outer spawns match the geometry, but blue has begun its trace jump by the 4603 anchor and appears about 18 pixels higher than the source. Source motion begins near the current 4601 combat boundary, with the first upward departure closer to 4609.
- At 4786, orange is close—clone (250,362), source (255,359)—while blue is clone (735,404), source (643,367): 92 pixels right and 37 pixels lower. Bounded source/contact checks found a short hop beside the upper-right platform. The current trace lands on its top, slows through friction and reaches the lower-right column before the source route. Geometry differs by about one pixel, and sampled frames establish no earlier movement window, substantial camera change or visible recoil/block impulse that explains the gap. Idea 049 owns further diagnosis; no wall-jump, variable jump, gravity or arena-specific movement rule was invented or admitted.
- The 4969 exchange and 5213 traversal are much closer: both fighters are near the same platform/column landmarks, with later positions within roughly 10 pixels. The ordinary left-platform shot now hits at 4961, leaving an eight-tick-old effect at 4969 rather than a fresh oversized flash. The final visible burst core is around (417,297), compared with source (416,264): about 33 native pixels lower. This older effect is smaller and less distorted than the previous fresh flash, but is still brighter and denser than the source halo and fading dots. Its contact point and particle pattern remain approximate.
- At 5310, blue is close to the source, but orange is about 37 pixels higher and the fighters' vertical order differs. The terminal shot still comes from the live close encounter, and its event/explosion use the actual struck surface. No fixed tick chooses the winner.
- The ice correction narrows the broad cyan brush strokes, starts stepped-base shadows at its actual narrow top, replaces the inappropriate lens zoom with a short downward camera pulse, and slows descending pale/orange fragments. These correct specific source mismatches; painted texture, facial detail, exact pose paths, particle density and camera response remain approximations. The central base still has broader saturated cyan bands than the largely pale source face. At 5312 the corrected camera pulse places the core around (423,419), versus source (430,425); at 5314 the yellow flash still hides the face visible in the source. Late fragments descend at the encounter height but spread farther radially than the source fragments and puffs.
- The adjacent undimmed/dimmed boundary, preserved prior halves, filled blue circle and final blue pip are structurally correct. At 5466 the arena and its shadows have departed, the first blue pip is at about(28,70), and orange's half circle is partly below the viewport near (640,683). The 66-pixel Arimo ROUND BLUE title closely matches source position and size; circle glow and color remain approximate.

This record does not claim pixel-identical traversal or independently approved fidelity. The retained pairs expose the remaining discrepancies for exact-candidate review. The early hop idea does not waive any frozen 046 requirement.

## Delivery verification and provenance

Final captures are `out/ticket-046/delivery-anchors/` and `delivery-anchors.json`: all 37 connected anchors, including the twelve ice source identities. The earlier `anchors/` and `final-anchors/` directories are retained correction evidence and are not the final capture set. Full recording/native-hash reproduction is in `source-recheck.txt`; exact decoder commands and adjacent boundaries are in `boundary-recheck.txt` and `admission-review/source-verification.json`. The final `delivery-verification.json` confirms all source identities, capture PNG hashes, executable binding and identical local/received final GPU pixels. Native image comparisons above are author QA, not independent approval.

All 42 workspace tests pass (26 simulation, 5 presentation, 4 network, 4 client and 3 automation), with no failures or skipped tests. Final strict all-target Clippy, locked build, format and ticket/whitespace checks are recorded in `delivery-*.txt`. The two-process client smoke delivered all 5,466 snapshots to each client; both clients, the local authority and received-state GPU agreed. It observed the entire existing route through ROUND BLUE. The 6,001-tick smoke rejects the unsupported bound rather than starting an unbounded run.

The bounded automated visible flow reached tick 5,466 and exited normally. Its hidden-first guard obtained the OS-updated physical window position and logged center (1324,-540) inside the selected 1920×1080 monitor at (364,-1080), before setting visibility. The normal keyboard/gamepad mapping regression covers combat actions through the same public authority. Physical keys and a physical controller were not operated: native manual-control tooling was unavailable. The automated visible flow and two separately launched UDP clients were exercised directly.

All twenty-five earlier connected anchors retain their source PTS/hash, dynamic-body projection and loadouts. Twelve PNGs are byte-identical to the delivered 045 images; thirteen change. Combat digests differ from tick 2280 onward because fighter impacts now record the actual contact, with later event history retaining those corrected coordinates. The half/round HUD interpretation also changes intentionally after the first half. This is not evidence of unchanged full combat digests; `delivery-verification.json` lists every changed anchor. Native inspection of the thirteen changed pairs finds the expected contact effects: the 2280/2585 burst moves about 11–14 pixels right, and the 4449/4450 hit puffs move about 40 pixels to the incoming-shot side of blue. Timber geometry, fighter poses, impact/collapse and result layout remain visibly as before; premature full-round pips disappear during half results. No additional visual regression was found in these pairs, while inherited source differences remain. The affected Yellow replay is documented separately, including its changed physical impulses. A final executable capture of Yellow tick 89 reproduces the already inspected corrected Yellow PNG and state exactly (`delivery-yellow-89.json`).

`final-product-manifest.json` binds all product-source files and three executable hashes. Captures were made from those dirty working-tree contents at HEAD `fdf5e8abf80fdb94700c6fc4cd2b1a640a19e8ec`, not from a clean implementation commit. The build-time product inventory hash is `86d315d817d90f480d23ee67e6a13ff98d5601844cb9e5c06c14e3e0cfa3d204`; the producing client executable hash is `11370c4b827e65294bfc299c4b2f06315db4ff3ca53c34f046f979412cefb980`. After that build, the staged whitespace check found and removed one trailing space on line 21 of the noncompiled OFL license. `post-build-license-manifest.json` records the resulting source inventory `be34b4863cae91fb919a6fdb28263755b4458942b390f51347529870f67bda6a` and the exact single-file change. Compiled inputs, font bytes and all three executables remain unchanged; the original build/capture manifest is preserved. Independent review will rebuild the exact committed candidate. This license-only normalization needs no GPU recapture.

The added-line responsibility inventory counts 1,112 runtime Rust lines (including layout/action configuration and blanks), 298 test lines and 85 capture/smoke-support lines, plus the 496,268-byte licensed font. This retains one authority, five existing replay profiles, one transport and one shared scene; no separate evidence launcher or generic search system ships. Ignored probes are bounded investigation artifacts. The inventory is `responsibility-inventory.json`.
