# Footage coverage

This ledger accounts for the full duration of the two recordings identified in `reference/manifest.json`.
Intervals are half-open except for the final interval, which includes the final frame.
Each ten-second interval was reviewed from a headless decode; the observation names representative visible content, while the assigned slices cover every arena, mechanic, card interaction, flow state, and presentation event anywhere in that interval.
No interval is unclassified.

## Playable footage slices

| Slice | Outcome | Status |
|---|---|---|
| `S0-foundation` | Bevy ECS fixed ticks, two-player scripted input, movement, jump, fire, block, hits, UDP client-host and headless authority, bounded state, deterministic PNG capture | implemented scaffold; not fidelity evidence |
| `S1-flow-draft` | reproduce card offers, readable card faces, player reveal, pick input, inter-round handoff, waiting and rematch screens | unresolved fidelity gap |
| `S2-static-duel` | reproduce base locomotion, aim, gun, block, health, death and a footage-matched static arena end to end | first sub-slice implemented at recording `1460e670…15f9` 00:22.50–00:35.60; remaining static duels unresolved |
| `S3-arena-motion` | reproduce suspended, rotating, sliding and articulated arena pieces with authoritative networked physics | unresolved fidelity gap |
| `S4-reactive-world` | reproduce destructible platforms, debris, explosions, ice and other reactive arena materials | unresolved fidelity gap |
| `S5-card-combat` | reproduce every visible named card and its stacked combat interaction across rounds | unresolved fidelity gap |
| `S6-match` | reproduce round, half, score, color handoff, match completion and rematch cadence | unresolved fidelity gap |
| `S7-presentation` | reproduce characters, lighting, camera, shake, trails, hit-stop, chromatic/radial effects, particles, text and audio | unresolved fidelity gap |
| `S8-online` | replace the localhost scripted UDP scaffold with production online prediction, interpolation, reconciliation and eventual Steam transport | unresolved fidelity gap; Steam is not implemented |

The first `S2-static-duel` interval retains the `S0-foundation` process boundaries but replaces their placeholder physics, renderer, and batch transport.
Later slices add the remaining flow, cards, arena behavior, production online work, and presentation visible in their owned intervals.

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
| 02:40–02:50 | `REMATCH?` flow | S1, S6, S7 |
| 02:50–03:00 | `EXPLOSIVE BULLET` draft choice | S1, S5, S7 |
| 03:00–03:10 | `LIFESTEALER` draft choice | S1, S5, S7 |
| 03:10–03:20 | card reveal and selection | S1, S5, S7 |
| 03:20–03:30 | swinging structure over reactive floor | S2, S3, S4, S5, S7, S8 |
| 03:30–03:40 | structure damage and falling pieces | S2, S3, S4, S5, S7, S8 |
| 03:40–03:50 | collapse, debris and combat | S2, S3, S4, S5, S7, S8 |
| 03:50–04:00 | `HALF ORANGE` result overlay | S6, S7 |
| 04:00–04:10 | ice arena duel | S2, S4, S5, S7, S8 |
| 04:10–04:20 | `QUICK SHOT` draft choice | S1, S5, S7 |
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
| 07:00–07:10 | `ROUND ORANGE` transition | S6, S7 |
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
| 03:00–03:10 | `HOMING` draft choice | S1, S5, S7 |
| 03:10–03:20 | teal moving-platform arena | S2, S3, S5, S7, S8 |
| 03:20–03:30 | radial saw arena and ice burst | S2, S3, S4, S5, S7, S8 |
| 03:30–03:40 | radial arena continued duel | S2, S3, S4, S5, S7, S8 |
| 03:40–03:50 | articulated joint-platform arena | S2, S3, S5, S7, S8 |
| 03:50–04:00 | `ROUND BLUE` transition | S6, S7 |
| 04:00–04:10 | `FROST SLAM` draft choice | S1, S5, S7 |
| 04:10–04:20 | `FAST FORWARD` draft choice | S1, S5, S7 |
| 04:20–04:30 | pink suspended-structure duel | S2, S3, S4, S5, S7, S8 |
| 04:30–04:40 | pink structure combat and impact | S2, S3, S4, S5, S7, S8 |
| 04:40–04:50 | `HALF ORANGE` result overlay | S6, S7 |
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
