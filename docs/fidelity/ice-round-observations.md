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

The source reaches the arena with one half per player and zero completed rounds. Blue's next elimination win fills its second half and awards the first blue round. Orange's existing half remains on screen throughout the bound result. The footage here does not establish what happens to that half after the following draft, so a future-round reset rule must not be inferred from this endpoint. A faithful implementation needs distinct completed-round and current-half state; the existing flow's single `scores` value currently represents prior-match rounds before rematch and half wins afterward.

The terminal burst appears during a close encounter beside the column, after ordinary traversal and projectile exchanges. This is a source-shaped action target, not authority to eliminate orange at a fixed tick. Either player must be able to win from legal live inputs. Likewise, two wins by one player in the first two arenas already complete a round; that path must not display a second HALF or force an unnecessary deciding ice duel.

Existing Dazzle and Explosive Bullet behavior, Rapier contacts, shared event-driven effects, received-state rendering and semantic input already cover the required kinds of behavior. Layout, source-shaped actions, surface painting and result timing extend those paths. No independent ice replay profile, second renderer, new evidence launcher, unobserved card formula or production-network claim is needed.

## Review focus

Compare the incoming result/arena overlap, opposite spawns, traversable contours, mid-duel positions, local projectile feedback, close terminal burst, adjacent result onset, preserved half circles, completed blue round pip and disappearing arena at native resolution. The source table binds observable states and ordering; hidden original movement, damage and friction constants remain unknown. Existing ticket 045 anchors remain the evidence for the earlier continuous route.
