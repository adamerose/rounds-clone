# Held hanging-arena entry observations

This record binds ticket 055's held arena entry to `reference/MedalTVRounds20260903165304088.mp4`, SHA-256 `453954a7230401ed805be4e53dec41779a1913dfd69903671fc131fca2c8a18c`. PTS uses the native 1/10,000,000-second time base. The measurements were made from retained native 1280×720 frames; brown/red spans generally carry 1–2 pixels of mask, edge, and compression uncertainty.

## Interval and identities

| Clone tick | Entry age | Native PTS | Packed RGBA SHA-256 | Role |
|---:|---:|---:|---|---|
| 5893 | overlap | 2577323024 | `ccc3b4919a7988b1e14e2566814a6c540b4f9f65e4b0f49c74d25b2ba6bfbb74` | Last background-only bridge frame; no incoming body is visible. |
| 5894 | 0 | 2577489690 | `a89e7c2e1931d4f941eaf91ccc5a3187a4e032471dbf5041945b1c19513a2199` | The lower-left body first appears as a sliver at native x 1274–1279, y 541–648. |
| 5898 | 4 | 2578156354 | `2677a84fcd410b3a6c26bb1bcabc08c7f9a58d551f4d15a474f25f6ff719674a` | Left bodies enter; their squares and the right group remain offscreen. |
| 5902 | 8 | 2578823018 | `efd5d36e1e1a4ef937af291aa8aa97c9acdd23e1d0b63593d032add8a61acf09` | Both side groups are visible with unequal spacing. |
| 5906 | 12 | 2579489682 | `ababdcc0ea199133dd1bb2dae65e3529d818acc22a538b41ad9da7169f19eeb4` | Side groups converge; the central body leads its square. |
| 5910 | 16 | 2580156346 | `193b2b063f278302aae23442608c992fc873ca9f1f8a123cc759e5b1533d04ba` | Left/right offsets remain distinct. |
| 5914 | 20 | 2580823010 | `d98144f0bbcb12e561ca6a3f3f3ee703b31ec885c404e5e77bf4788bf891a067` | Central lower body and square are complete at x 641.5 and 670.5. |
| 5918 | 24 | 2581489674 | `689ded32c34af48aba5a00c725bd8a08356c48ad66cf4c73c475fa2bb532ccd4` | All 21 square centers are within about one pixel of nominal. |
| 5940 | 46 | 2585156326 | `5c8995a6ca0f0af7a38a2e4973b7ce1ca208fb18395201a1194db1cb4788c1cb` | Both fighters remain at their held scene poses. |
| 5941 | 47 | 2585322992 | `f90f451ed5bf928b6ea04a3a5fdf1623278063ff043dd67127e7cd9f43ce3067` | Last retained held observation. |
| 5942 | excluded | 2585489658 | `2431300ec77cc8f0fb4783d42aef07cacc9719b08e4bbc8537e5b1a8bf1d84a3` | Adjacent departure witness; active motion begins with about two frames of onset uncertainty. |

The 5940 and 5942 packed hashes come from the same retained 37-frame native decode and must be reverified before they become delivery evidence. Their inclusion here fixes the intended source identity; it does not claim a new decode in ticket 055's creating session.

## Canonical nominal layout

Native coordinates use x rightward and y downward. World coordinates are `(native_x - 640, 360 - native_y)`. Identifiers are stable clone identities, not recovered source-engine IDs.

| Group | IDs | Native x centers | Square center y | Body center y | Body size | Count |
|---|---|---|---:|---:|---|---:|
| Upper outer | 400, 409 | 235, 1045 | 208 | 337 | 36×36 | 2 |
| Upper inner | 401–408 | 325 through 955 in 90-pixel steps | 208 | 339 | 36×108 | 8 |
| Lower | 410–420 | 190 through 1090 in 90-pixel steps | 464 | 595 | 36×108 | 11 |

Every body has one 23×23 hollow square whose dark opening is 17×17. Presentation draws one vertical link from native `(x,-40)` to the square center and another from the square center to the body's top center: y 319 for upper-outer bodies, 285 for upper-inner bodies, and 541 for lower bodies. These 42 visible segments describe the image, not joints, colliders, anchors, or solver constraints.

The selected source-shaped palette is body RGB `[157,92,72]`, square rim `[102,61,62]`, square opening `[3,8,30]`, and link `[67,61,61]`. These are empirical presentation choices checked against the native frames, not exact recovered material constants.

Fighter held poses are orange native `(235,307)` / world `(-405,53)` and blue native `(1045,273)` / world `(405,87)`. Both velocities remain zero for this interval. Blue's visible gap above the right outer body does not establish hidden support.

## Object entry curves

Offsets are native horizontal pixels added to nominal x. Use a small local monotone interpolation through these knots and clamp the settled -0.5-pixel raster estimate to zero. Unseen starting positions and between-sample easing are authored choices.

| Age | Left body/square offset | Right body/square offset | Central lower square offset |
|---:|---:|---:|---:|
| 0 | 1102, inferred from the clipped first-body edge | offscreen, unmeasured | offscreen, unmeasured |
| 4 | 755.5 | offscreen, unmeasured | offscreen, unmeasured |
| 8 | 440.5 | 390.0 | about 495.5 |
| 12 | 224.5 | 191.5 | about 260.5 |
| 16 | 90.5 | 72.0 | about 111.5 |
| 20 | 21.5 | 13.5 | about 30.5 |
| 24 | 0.0 | 0.0 | 0.0 |

The central lower body evaluates the central-square curve about four ticks ahead. Its observed centers at ages 4/12/16/20 are approximately 1135.5/751.5/670.5/641.5, closely matching the square four ticks later. At age 20 the complete body and square are separated by 29 pixels; this same-frame evidence rules out one global scene translation.

At age 8 the visible upper-square centers are approximately 675.5, 765.5, 855.5, 945.5, 1035.5, 1075.0, 1165.0, and 1254.5. The 40-pixel middle gap, compared with the final 90 pixels, independently rules out camera motion as the cause. Background registration is stable to about one pixel, while weak texture and postprocessing leave individual edges uncertain by 1–2 pixels.

## Boundary of the evidence

This record supports a held scene and its presentation only. It does not select when ordinary control truly begins, a suspension topology, mass, inertia, collision role, actor support, jump behavior, ammunition/reload, projectile calibration, damage, or the later tick-5954 combat pose. Those questions require their own admitted contracts.
