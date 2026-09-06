# Footage coverage

This ledger accounts for the full duration of the two recordings identified in `reference/manifest.json`.
Intervals are half-open except for the final interval, which includes the final frame.
Each ten-second interval was reviewed from a headless decode; the observation names representative visible content, while the assigned slices cover every arena, mechanic, card interaction, flow state, and presentation event anywhere in that interval.
No interval is unclassified.

## Playable footage slices

| Slice | Outcome | Status |
|---|---|---|
| `S0-foundation` | Bevy ECS fixed ticks, two-player scripted input, movement, jump, fire, block, hits, UDP client-host and headless authority, bounded state, deterministic PNG capture | implemented scaffold; not fidelity evidence |
| `S1-flow-draft` | reproduce card offers, readable card faces, player reveal, pick input, inter-round handoff, waiting and rematch screens | first rematch/two-draft sub-slice implemented at recording `453954a7…a18c` 02:40.00–03:20.00; remaining flows unresolved |
| `S2-static-duel` | reproduce base locomotion, aim, gun, block, health, death and a footage-matched static arena end to end | first sub-slice implemented at recording `1460e670…15f9` 00:22.50–00:35.60; connected ice duel added at recording `453954a7…a18c` PTS 2351990592–2506156642; remaining static duels unresolved |
| `S3-arena-motion` | reproduce suspended, rotating, sliding and articulated arena pieces with authoritative networked physics | first suspended-weight and released-joint sub-slice implemented at recording `453954a7…a18c` 03:26.00–03:50.00; remaining arena motion unresolved |
| `S4-reactive-world` | reproduce destructible platforms, debris, explosions, ice and other reactive arena materials | first explosive timber-collapse sub-slice implemented at recording `453954a7…a18c` 03:26.00–03:50.00; connected ice contours and ordinary card impacts added, with no fracture or friction rule established by that interval; remaining reactive worlds unresolved |
| `S5-card-combat` | reproduce every visible named card and its stacked combat interaction across rounds | Dazzle and Explosive Bullet first implemented in the 02:40.00–03:20.00 sub-slice; remaining card behavior unresolved |
| `S6-match` | reproduce round, half, score, color handoff, match completion and rematch cadence | blue 4–5 victory/rematch/0–0 reset continues through the first full-round award at recording `453954a7…a18c` PTS 2506156642; either color may sweep the opening fights or win the deciding ice duel; next draft and remaining lifecycle unresolved |
| `S7-presentation` | reproduce characters, lighting, camera, shake, trails, hit-stop, chromatic/radial effects, particles, text and audio | first static-duel, explosive-collapse and connected ice/first-round visual sub-slices implemented in the shared scene; audio, hit-stop, and remaining presentation unresolved |
| `S8-online` | replace the localhost scripted UDP scaffold with production online prediction, interpolation, reconciliation and eventual Steam transport | unresolved fidelity gap; Steam is not implemented |

The first `S2-static-duel` interval retains the `S0-foundation` process boundaries but replaces their placeholder physics, renderer, and batch transport.
Later slices add the remaining flow, cards, arena behavior, production online work, and presentation visible in their owned intervals.

The connected rematch route joins the existing draft, timber and ice slices from native PTS 1595160286 through the selected first-round frame at PTS 2506156642 in recording `453954a7…a18c`. The source-shaped blue/orange/blue sequence retains Da and Ex, enters ice with halves 1–1 and rounds 0–0, then ends with halves 1–2 and rounds 0–1. Either color can instead win two opening fights and earn a round without entering ice; ordinary combat decides each outcome. The losing half remains visible through this result. The next draft, later half reset and remaining rounds are unresolved.
The ice extension starts at PTS 2351990592. Its seventeen static polygon contours supply both collision and rendering, while animated cyan/pale paint, shadows and incoming/outgoing motion stay in the shared presentation scene. The interval provides no evidence for ice friction, fracture or melting. See `connected-match-observations.md` for the earlier 25 anchors and `ice-round-observations.md` for the twelve added anchors, exact frame identities and visual differences. The existing `rematch-draft-replay` profile accepts the complete 5,466-tick route for local controls, bounded playback, replay capture and the two-client smoke path; this adds no separate ice profile or production-network claim.

## Recording `453954a7…a18c`

Source: `reference/MedalTVRounds20260903165304088.mp4`, duration `600.13` seconds.

| Time | Representative visible content | Assigned slices |
|---|---|---|
| 00:00–00:10 | opening card presentation and player reveal | S1, S5, S7 |
| 00:10–00:20 | suspended bridge duel, movement and gunfire | S2, S3, S5, S7, S8 |
| 00:20–00:30 | bridge combat and impacts | S2, S3, S5, S7, S8 |
| 00:30–00:40 | `HALF ORANGE` result overlay | S6, S7 |
| 00:40–00:50 | `ROUND BLUE` transition | S6, S7 |
| 00:50–01:00 | `BUCKSHOT` draft choice | S1, S5, S7 |
| 01:00–01:10 | rotating-saw arena and explosive color field | S2, S3, S4, S5, S7, S8 |
| 01:10–01:20 | hanging-weight arena duel | S2, S3, S5, S7, S8 |
| 01:20–01:30 | `COMBINE` draft choice | S1, S5, S7 |
| 01:30–01:40 | card reveal and handoff | S1, S5, S7 |
| 01:40–01:50 | gem platforms, saw hazard and projectile combat | S2, S3, S4, S5, S7, S8 |
| 01:50–02:00 | `HALF BLUE` result overlay | S6, S7 |
| 02:00–02:10 | `FAST FORWARD` draft choice | S1, S5, S7 |
| 02:10–02:20 | neon stepped arena duel | S2, S4, S5, S7, S8 |
| 02:20–02:30 | neon combat, shots and trails | S2, S4, S5, S7, S8 |
| 02:30–02:40 | high-energy hit and explosion effects | S2, S4, S5, S7, S8 |
| 02:40–02:50 | blue 4–5 victory, `REMATCH?`, fade, and orange card fan | S1/S6/S7 implemented sub-slice; remaining match variants unresolved |
| 02:50–03:00 | orange navigation and `DAZZLE` confirmation inferred from its persistent badge | S1/S5/S7 implemented sub-slice; non-selected offer behavior remains catalog-only |
| 03:00–03:10 | handoff and blue card fan with `LIFESTEALER` hover | S1/S5/S7 implemented sub-slice; non-selected offer behavior remains catalog-only |
| 03:10–03:20 | blue navigation, `EXPLOSIVE BULLET` confirmation, 0–0 reset, and upgraded projectiles | S1/S5/S6/S7 implemented sub-slice; production S8 and remaining card behavior unresolved |
| 03:20–03:26 | first elimination, HALF BLUE and timber-arena handoff | connected S2/S3/S5/S6/S7 route implemented; production S8 unresolved |
| 03:26–03:30 | intact stacked timber, suspended weights, reactive floor and combat | S3/S4/S7 implemented sub-slice; S2, S5, S8 remain incomplete |
| 03:30–03:40 | intact timber combat and upper-left projectile approach | S3/S4/S7 implemented sub-slice; S2, S5, S8 remain incomplete |
| 03:40–03:50 | explosion, released structure, collapse, debris settlement and continued combat | S3/S4/S7 implemented sub-slice; S2, S5, S8 remain incomplete |
| 03:50–04:00 | elimination and HALF ORANGE through PTS 2351823926, then incoming ice and first traversal | connected S2/S5/S6/S7 route implemented; unobserved ice reactions and production S8 unresolved |
| 04:00–04:10 | ice duel, close terminal burst, adjacent undimmed/result frames at PTS 2484823394/2484990060, then ROUND BLUE | connected S2/S5/S6/S7 route implemented; remaining S4 and production S8 unresolved |
| 04:10–04:20 | first blue round pip through selected PTS 2506156642, then `QUICK SHOT` draft choice | connected S6/S7 first-round result implemented through the selected endpoint; following S1/S5/S7 draft unresolved |
| 04:20–04:30 | hanging-column arena duel | S2, S3, S5, S7, S8 |
| 04:30–04:40 | hanging-column movement and combat | S2, S3, S5, S7, S8 |
| 04:40–04:50 | `COMBINE` card presentation | S1, S5, S7 |
| 04:50–05:00 | `SCAVENGER` draft choice | S1, S5, S7 |
| 05:00–05:10 | `HALF BLUE` result overlay | S6, S7 |
| 05:10–05:20 | saw-and-pedestal arena duel | S2, S3, S5, S7, S8 |
| 05:20–05:30 | saw arena combat and color change | S2, S3, S5, S7, S8 |
| 05:30–05:40 | saw arena combat and blocks | S2, S3, S5, S7, S8 |
| 05:40–05:50 | saw arena continued duel | S2, S3, S5, S7, S8 |
| 05:50–06:00 | saw arena round conclusion | S2, S3, S6, S7, S8 |
| 06:00–06:10 | `CAREFUL PLANNING` draft choice | S1, S5, S7 |
| 06:10–06:20 | `SHIELD CHARGE` draft choice | S1, S5, S7 |
| 06:20–06:30 | shield/card reveal sequence | S1, S5, S7 |
| 06:30–06:40 | card selection and transition | S1, S5, S7 |
| 06:40–06:50 | bright stepped arena combat | S2, S4, S5, S7, S8 |
| 06:50–07:00 | hanging-platform arena duel | S2, S3, S5, S7, S8 |
| 07:00–07:10 | yellow-crate duel, terminal blast/radial screen echo through final combat PTS 4238316380, adjacent result onset at PTS 4238483046, then `ROUND ORANGE` | S2/S4/S5/S6/S7/S8 implemented sub-slice; other yellow-arena combat remains incomplete |
| 07:10–07:20 | chromatic hit effect and reactive geometry | S2, S4, S5, S7, S8 |
| 07:20–07:30 | layered wooden arena duel | S2, S3, S4, S5, S7, S8 |
| 07:30–07:40 | wooden structure collapse | S2, S3, S4, S5, S7, S8 |
| 07:40–07:50 | debris-field combat | S2, S3, S4, S5, S7, S8 |
| 07:50–08:00 | surviving structure and projectiles | S2, S3, S4, S5, S7, S8 |
| 08:00–08:10 | suspended timber arena combat | S2, S3, S4, S5, S7, S8 |
| 08:10–08:20 | impact echo and damaged structure | S2, S3, S4, S5, S7, S8 |
| 08:20–08:30 | orange stepped arena duel | S2, S4, S5, S7, S8 |
| 08:30–08:40 | explosive projectile and platform damage | S2, S4, S5, S7, S8 |
| 08:40–08:50 | orange arena round conclusion | S2, S4, S6, S7, S8 |
| 08:50–09:00 | ice platform arena duel | S2, S4, S5, S7, S8 |
| 09:00–09:10 | articulated hanging-tile arena | S2, S3, S4, S5, S7, S8 |
| 09:10–09:20 | hanging-tile deformation and collapse | S2, S3, S4, S5, S7, S8 |
| 09:20–09:30 | `ROUND ORANGE` transition | S6, S7 |
| 09:30–09:40 | bright green platform duel | S2, S4, S5, S7, S8 |
| 09:40–09:50 | green arena impact and projectile effects | S2, S4, S5, S7, S8 |
| 09:50–10:00.13 | red framework duel through final frame | S2, S3, S4, S5, S6, S7, S8 |

## Recording `1460e670…15f9`

Source: `reference/MedalTVRounds20260903170709695.mp4`, duration `600.17` seconds.

| Time | Representative visible content | Assigned slices |
|---|---|---|
| 00:00–00:10 | hanging-column duel and vertical movement | S2, S3, S5, S7, S8 |
| 00:10–00:20 | `TANK` draft choice | S1, S5, S7 |
| 00:20–00:22.50 | tail of `TANK` draft and fade into the arena | S1, S5, S7 |
| 00:22.50–00:30 | teal stepped arena spawn, asymmetric traversal, gunfire and block | S2 (implemented first sub-slice), S5, S7, S8 |
| 00:30–00:35.60 | teal arena upper-tier convergence, shots and terminal impact | S2 (implemented first sub-slice), S5, S7, S8 |
| 00:35.60–00:40 | `HALF ORANGE` result transition | S6, S7 |
| 00:40–00:50 | tall-column arena traversal | S2, S3, S5, S7, S8 |
| 00:50–01:00 | card selection and reveal | S1, S5, S7 |
| 01:00–01:10 | pastel fortress duel | S2, S4, S5, S7, S8 |
| 01:10–01:20 | `HALF BLUE` result overlay | S6, S7 |
| 01:20–01:30 | `ROUND BLUE` transition | S6, S7 |
| 01:30–01:40 | ice arena traversal | S2, S4, S5, S7, S8 |
| 01:40–01:50 | ice arena combat and impact | S2, S4, S5, S7, S8 |
| 01:50–02:00 | orange hanging-platform arena | S2, S3, S5, S7, S8 |
| 02:00–02:10 | orange arena continued duel | S2, S3, S5, S7, S8 |
| 02:10–02:20 | card reveal and selection | S1, S5, S7 |
| 02:20–02:30 | mixed ice-block arena | S2, S4, S5, S7, S8 |
| 02:30–02:40 | orange platform arena combat | S2, S4, S5, S7, S8 |
| 02:40–02:50 | orange arena round conclusion | S2, S4, S6, S7, S8 |
| 02:50–03:00 | pink fortress duel and waiting transition | S1, S2, S6, S7, S8 |
| 03:00–03:10 | pink fortress duel; first frame at or after 03:00 is PTS 1800159466, RGBA `86b275fbc6b498d6dccb73ab7fe6df2def431b6a97cea1f35d5aac949e171831` | S2, S5, S7, S8 |
| 03:10–03:20 | pink fortress continued combat; PTS 1900159066, RGBA `3cecc328dc3d0c4a3392f4aa7e12efc30fb016610f288334c87083dac9529df6` | S2, S5, S7, S8 |
| 03:20–03:30 | `WAITING`; PTS 2000158666, RGBA `c4c9547151263157cf54afe9495c7f1b1103cc3c2da8d3b29314bb0c57a09a6d` | S1, S6, S7 |
| 03:30–03:40 | blue `PARASITE` draft; PTS 2100158266, RGBA `27997930942ef3ab9539add4a20a41d7ec5c3f99cae7707152499e98a1230a1f` | S1, S5, S7 |
| 03:40–03:50 | lime modular/piston arena duel; PTS 2200157866, RGBA `ae61a7f854309c7b126f3fcc4950ac437ac17d9e013b1813786e1ecc9f949193` | S2, S3, S5, S7, S8 |
| 03:50–04:00 | `HALF ORANGE`, then radial-saw reveal and duel from PTS 2320490718; first row frame PTS 2300157466, RGBA `392caceaa1e670bc9498fc68f99528de0f39c53f1e623f55e3d5e28530e2db38` | S2/S3/S6/S7 radial-saw sub-slice implemented; S5/S8 incomplete |
| 04:00–04:10 | radial-saw duel, ordinary impact, adjacent-frame result onset, and established `HALF BLUE`; PTS 2400157066, RGBA `2bb31569dee81a9eef00bab1cb903a34057392f1255df0eadf9bccae6d5176de` | S2/S3/S6/S7 radial-saw sub-slice implemented; S5/S8 incomplete |
| 04:10–04:20 | hanging articulated arena; PTS 2500156666, RGBA `cd45852969daa0f3ff61d560bb4454e47743ceb1ad88b9fcfaee8d1c80302067` | S2, S3, S5, S7, S8 |
| 04:20–04:30 | hanging articulated arena continued; PTS 2600156266, RGBA `52a6d71c22030492cf2f3869ddc65a6b8f58b4296f8ca0cf39005f9be03aa828` | S2, S3, S5, S7, S8 |
| 04:30–04:40 | hanging articulated arena continued; PTS 2700155866, RGBA `3234d4b0e6c0f62a9d4bf545715fb2d622f0e32b7715f11b5e503a4a21894b21` | S2, S3, S5, S7, S8 |
| 04:40–04:50 | `FAST FORWARD` draft, followed by `SPRAY`; PTS 2800155466, RGBA `2eaed7698b0f7f536e8aeaa7b8ae7d0c474bc6e6379c066d1570043def9e05d1` | S1, S5, S7 |
| 04:50–05:00 | red timber arena duel | S2, S3, S4, S5, S7, S8 |
| 05:00–05:10 | timber arena continued combat | S2, S3, S4, S5, S7, S8 |
| 05:10–05:20 | `TASTE OF BLOOD` draft choice | S1, S5, S7 |
| 05:20–05:30 | destructible ice-cell arena | S2, S4, S5, S7, S8 |
| 05:30–05:40 | gray fortress arena duel | S2, S4, S5, S7, S8 |
| 05:40–05:50 | `HALF BLUE` result overlay | S6, S7 |
| 05:50–06:00 | rainbow stepped arena duel | S2, S4, S5, S7, S8 |
| 06:00–06:10 | rainbow arena continued combat | S2, S4, S5, S7, S8 |
| 06:10–06:20 | floating-diamond arena | S2, S3, S4, S5, S7, S8 |
| 06:20–06:30 | close hit, block and particle effects | S2, S4, S5, S7, S8 |
| 06:30–06:40 | red bridge arena duel | S2, S3, S5, S7, S8 |
| 06:40–06:50 | ice spire arena impact | S2, S4, S5, S7, S8 |
| 06:50–07:00 | hanging-block arena combat | S2, S3, S5, S7, S8 |
| 07:00–07:10 | articulated swing arena | S2, S3, S5, S7, S8 |
| 07:10–07:20 | `ROUND BLUE` transition | S6, S7 |
| 07:20–07:30 | card-backed player transition | S1, S5, S7 |
| 07:30–07:40 | `HALF ORANGE` result overlay | S6, S7 |
| 07:40–07:50 | dense ice-block arena duel | S2, S4, S5, S7, S8 |
| 07:50–08:00 | `FASTBALL` draft choice | S1, S5, S7 |
| 08:00–08:10 | `REFRESH` draft choice | S1, S5, S7 |
| 08:10–08:20 | suspended mixed-material arena | S2, S3, S4, S5, S7, S8 |
| 08:20–08:30 | orange rotating/reactive arena | S2, S3, S4, S5, S7, S8 |
| 08:30–08:40 | `REFRESH` card reveal | S1, S5, S7 |
| 08:40–08:50 | ice arena combat | S2, S4, S5, S7, S8 |
| 08:50–09:00 | orange castle arena duel | S2, S3, S4, S5, S7, S8 |
| 09:00–09:10 | explosive hit, trails and debris | S2, S4, S5, S7, S8 |
| 09:10–09:20 | `REMATCH?` flow | S1, S6, S7 |
| 09:20–09:30 | rematch card transition | S1, S5, S6, S7 |
| 09:30–09:40 | new-match arena and combat | S2, S3, S4, S5, S7, S8 |
| 09:40–09:50 | new-match card/combat transition | S1, S2, S5, S6, S7, S8 |
| 09:50–10:00.17 | final duel and match-flow frames | S2, S3, S4, S5, S6, S7, S8 |

## Review discipline

Closing a slice requires a source interval, captured clone output from the same observable path, and a recorded comparison.
The broad slice assignment prevents an interval from falling out of scope; implementation tickets may split a slice only if the union still covers every assigned interval.
The supplied media remains ignored and unchanged, and generated contact sheets are disposable local evidence under `out/`.
