---
status: ready
owner: codex:01a13b9d-4094-7da3-9381-8a7815803a02
created: 2026-10-14T18:26:17Z
origin: human-request
tags: ["quarrel", "design", "playtest"]
value: 10
sessions:
  - codex:01a13b9d-4094-7da3-9381-8a7815803a02
execution: unattended
depends-on: []
supersedes: []
split-from: []
---

# Make practice drive combat tuning and design iteration

QUARREL completes matches but Adam reports that shots are too fast, movement is slow, and the game feels amateur. Measure those problems, deliver a reversible first tuning pass, and replace feature-count completion with an evidence-led roadmap toward a fun Steam release.

## Outcome

- Baseline and at least two data-only combat tuning candidates are compared through legal player inputs and authoritative snapshots; the chosen default makes movement faster and gives ordinary midrange shots more travel time.
- Launching with no arguments opens target practice immediately: one primary keyboard/mouse control set drives the player, and a second fighter receives no controls and respawns after elimination. No opening draft or score-driven interruption is needed to try base combat. Reset and access to online play remain available.
- A visible detailed-tooltip mode button and shortcut toggle exact combat numbers and exact selected-card effects. UI clicks never also fire; measured velocity and configured run speed are distinguished.
- A playtest record distinguishes native observations, simulation measurements, design hypotheses, and human enjoyment evidence. It states exactly what was unavailable.
- The roadmap orders base combat feel and audiovisual readability before expanding content, then verifies build variety, online play with friends, and shipping quality.
- A durable iteration process lets agents choose and test ordinary tuning changes without asking Adam for numbers; it uses research and a continuing Opus consultation, retains the best build, and requests occasional short experiential feedback at meaningful gates.

## Decisions

- Destination main, direct delivery. Adam explicitly requested agent playtesting, research, Opus consultation, roadmap/iteration changes, and minimal tuning prompts in this session.
- Preserve settled online match rules, opening/loser drafts, scoring, run-back choices, cards, arenas and Steam transport. Adam's later instruction makes target practice the default; local PvP is not a delivery goal. Keep the explicit menu/online routes accessible.
- Implement practice and detailed tooltips with existing simulation/input/rendering interfaces and no shared wire/schema change. Exact card numbers come from the authoritative catalog; a remote client's own base tuning must not be represented as the host's values.
- Fixes and architecture work must follow demonstrated friction. No general agent framework, RL training, game modes beyond Adam's target practice, or speculative refactor.
- Tune through existing validated content and public player-input interfaces. Keep a no-upgrade baseline trial so a speed card cannot masquerade as a default.
- GUI evidence must verify monitor 4 before capture. Reuse the attributed product build for live-data trials when code is unchanged; new compiled inputs require appropriate verification with the prepared Cargo target and serialized links.
- Bots and model judgments cannot certify human enjoyment or claim the Steam release complete. Spending, Steam account/legal actions and actual publication still need Adam's authority at the concrete final step.
- Risk 4: data changes are reversible, but practice/reset semantics and UI input consumption need boundary regressions, while movement/projectile timing interact with cards and arena traversal.

## Evidence required

- Reproducible baseline versus at least two candidates reports actual ground travel, reversal or stopping response, jump behavior, and projectile travel/contact; every trial pins content, inputs, source and executable identity.
- Run the same stable trials twice with identical results. The chosen variant increases measured running displacement and projectile travel time, and legal jump, fire, block and ordinary draft/match flow still work.
- Boundary checks prove default practice starts without a draft, ignores second-player controls, respawns the target, resets, and preserves explicit menu and online startup. Tooltip checks cover on/off rendering, exact numerical card effects and current stats, and consuming toggle clicks without firing.
- Native monitor-4 exploration records draft/combat observations while control is available. If Adam stops native control, stop UI input immediately, report its limits, and use attributed headless candidate renders for layout evidence.
- Research cites primary sources within their demonstrated limits, and at least one Opus follow-up challenges the initial advice using measured evidence.
- Roadmap, related stale design statements, and the playtest/process record agree about the gates and delegated decisions. A fresh independent context reviews the exact candidate and evidence.

## Chat excerpts

Adam — [design and playtest session](http://ivy.localhost/sessions/codex/01a13b9d-4094-7da3-9381-8a7815803a02), 2026-10-14:

> can you playtest it? the game currently feels amateur and jank. the default bullet speed is way too high, movement is slow, etc.

> Do some research and Opus consultation on agent-driven game design and figure out what our roadmap and iteration process should look like. I want you to give me a steam-ready fun game with minimal tweaking-prompts by me.

Adam — same session, 2026-10-14:

> Also I dont care at all about local pvp. just make the default mode target practice where there's just 1 set of controls and 1 player and the 2nd player is essentially just AFK

> also, let's have a detailed tooltip mode button where it shows exact numbers

## Work log

- 2026-10-14T18:26:17Z stage research start session codex:01a13b9d-4094-7da3-9381-8a7815803a02 — compare delivered game feel, research limits and Opus advice.
- 2026-10-14T18:37:22Z guard session codex:01a13b9d-4094-7da3-9381-8a7815803a02 — put conflict: 1 conflicting hunk(s) against b85ef05e3387
- 2026-10-14T18:39:47Z Admitted risk 4 by fresh read-only /root/admit_design after the practice and tooltip amendment; no open product decision, depends-on empty. Destination main, direct delivery.
- 2026-10-14T19:03:00Z stage research end session codex:01a13b9d-4094-7da3-9381-8a7815803a02 — complete repeated motion/riding/shot trials and continuing Opus challenge; retain separate native-control limits.
- 2026-10-14T19:03:07Z stage verify start session codex:01a13b9d-4094-7da3-9381-8a7815803a02 — full supported verifier on practice/tooltips/data candidate with prepared target; serialized links.
- 2026-10-14T19:39:18Z stage verify end session codex:01a13b9d-4094-7da3-9381-8a7815803a02 — fmt, strict Clippy and build passed; full tests found 27 simulation failures tied to changed flight/control assumptions and the practice movement assertion; rendered details obscured gameplay.
- 2026-10-14T19:39:25Z stage correction start session codex:01a13b9d-4094-7da3-9381-8a7815803a02 — fix controlled lab fixtures, preserve terrain momentum and clear tooltip occlusion through rendered evidence.
- 2026-10-14T19:56:45Z stage correction end session codex:01a13b9d-4094-7da3-9381-8a7815803a02 — fixed lab assumptions, terrain momentum, tooltip occlusion/glyphs, primary live focus and reaction units; owned concurrent capture caused a library-copy lock, now ended and recorded in postmortems.
- 2026-10-14T19:56:58Z stage verify start session codex:01a13b9d-4094-7da3-9381-8a7815803a02 — serialized full verifier reuses prepared artifacts, with capture runs held until completion; repeated probes match 282 sampled states across old and rebuilt clients.
- 2026-10-14T20:26:26Z stage verify end session codex:01a13b9d-4094-7da3-9381-8a7815803a02 — fmt, strict Clippy, locked build, doctests and all 182 tests passed with no ignored checks; final captures and selected run/contact states passed; native input, human enjoyment and two-home Steam play unavailable.
- 2026-10-14T20:26:36Z stage review start session codex:01a13b9d-4094-7da3-9381-8a7815803a02 — prepare frozen range and exact executable/image/input provenance for independent configured Codex CLI review.
- 2026-10-14T20:49:28Z stage review end session codex:01a13c1d-a008-7db2-b1fe-e091fc2ddeef — changes requested for misplaced close handler leaving practice windowless; complete review receipt retained in quarrel-review-96-01a13b9d-20261014; evidence inventory unchanged.
- 2026-10-14T20:49:34Z stage correction start session codex:01a13b9d-4094-7da3-9381-8a7815803a02 — register practice close handler and regress close events; preserve authority tuning only for newly produced captures, omitting guesses for saved snapshots.
- 2026-10-14T21:08:42Z stage correction end session codex:01a13b9d-4094-7da3-9381-8a7815803a02 — practice close and saved-snapshot tuning provenance fixed with boundary regressions
- 2026-10-14T21:08:52Z stage verify end session codex:01a13b9d-4094-7da3-9381-8a7815803a02 — corrected full suite passed 184 tests, format, strict Clippy, locked build and doctests; seven captures read and two legal replay states identical
- 2026-10-14T21:09:04Z stage review start session codex:01a13b9d-4094-7da3-9381-8a7815803a02 — fresh correction review of the complete candidate, prior finding and final evidence
