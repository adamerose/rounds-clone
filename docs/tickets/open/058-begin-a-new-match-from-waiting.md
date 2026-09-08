---
format: 3
status: ready
created: 2026-09-08T15:58:37Z
origin: agent-proposed
tags: ["rounds", "bevy", "match-flow", "draft", "fidelity"]
value: 8
risk: 4
sessions:
  - codex:01a07f87-97d0-7751-825a-29e87e8fe64a
execution: unattended
depends-on: [57]
supersedes: []
split-from: []
---

# Begin a new match from Waiting

Ticket 057 can now reach the source's terminal `WAITING` state, but the authority has no way to begin the fresh match visibly shown four seconds later. Add one non-player lifecycle boundary that clears the concluded match and enters the ordinary first-player draft without guessing whether time, host input, or peer readiness supplied the unseen trigger.

## Outcome

- A typed lifecycle request accepted only in `Waiting` clears completed-round scores, halves, winner, eliminated fighter, badge history, loadouts, capabilities, rematch votes, draft focus/selection/reveal, and any combat projectiles. Both fighters remain live at the current profile's visible spawn anchors while the existing arena-fade-to-first-draft cadence begins.
- The first draft belongs to orange and exposes the five source-observed offers in order: `DAZZLE`, `STEADY SHOT`, `TANK`, `TIMED DETONATION`, `HOMING`. The prepared blue offer row is `HUGE`, `STEADY SHOT`, `EXPLOSIVE BULLET`, `HEALING FIELD`, `PARASITE`.
- The seven newly transcribed card identities are catalog-only. Their source-visible titles and rule text, source-sampled card colors, and independently drawn art keys may render and hover, but confirm returns `UnimplementedItem` without changing phase or builds. This ticket implements no new card mechanic, does not infer rarity from color, and does not claim the recorded players' eventual selections.
- The shared renderer shows a cleared two-row score HUD, no retained badges, orange's five-card fan, and the ordinary orange player reveal from the same typed snapshot used locally and after serialization. No `WAITING`, result, round, rematch, or combat overlay survives the transition.
- Requests before `Waiting`, repeated requests after the first accepted transition, and player flow commands masquerading as lifecycle control are rejected without mutation. The external source of the accepted request remains deliberately unmapped.

## Decisions

- `docs/fidelity/new-match-draft-observations.md` is the source contract. PTS 2039991840 proves the cleared 0–0 orange draft; exact later frames expose both five-card offer rows. They do not show the event that ended `WAITING`.
- Keep reset semantics separate from trigger policy. A public authority operation can be called later by a verified timer, host action, lobby, or readiness protocol; choosing one of those now would convert missing evidence into a game rule.
- Reuse the existing `ArenaFade` to orange `Draft` cadence and ordinary draft renderer. The source sequence does not justify a second card-state machine or a profile-only snapshot mutation.
- Catalog-only means unreachable gameplay, not absent presentation. Hovering a visible inert offer is already supported; confirmation remains the existing hard gate until a card's complete mechanic is admitted elsewhere.
- Do not extend the delivered `match-end-waiting-replay` past its stable terminal endpoint. Focused tests construct its final public snapshot, invoke the new lifecycle boundary explicitly, then advance through the ordinary authority path.
- No score threshold, combat, damage, weapon, movement, arena, rematch-vote, card-mechanic, network-trigger, or production-transport value changes.

## Evidence required

- Re-decode and hash PTS 2039991840, 2045158486, and 2080158346 at native 1280×720 RGBA with the retained FFmpeg 7.1 two-thread method. Inspect the frames at native resolution and retain commands, byte counts, hashes, offer order, score/badge reset, player ownership, and the explicit absence of trigger evidence.
- Focused flow tests reach `Waiting` through a fifth completed-round award, accept one lifecycle request, prove every reset field and `ArenaFade` ordering, advance to orange `Draft`, verify both offer rows, and prove wrong-phase/repeated requests are immutable rejections.
- A public `AuthoritativeMatch` regression reaches the 3–5 projectile ending, invokes the lifecycle operation, proves projectile cleanup and live fighters at visible spawns, advances to the first draft, and serializes the same typed state without a private score or phase mutation.
- Catalog and draft regressions cover all seven new identities, exact transcribed visible text, source order, hover acceptance, and confirmation rejection with no loadout/capability/phase change.
- Shared-renderer tests and a headless capture prove the cleared HUD, no badges or stale terminal overlay, orange reveal, and all five source titles. A guarded exact-candidate visible run appears only on monitor 4 after its center is verified.
- Re-run all six delivered replay profiles and their retained 100 frame anchors. Existing flow, score, loadout, arena, event, network, and rendered observations remain unchanged; protocol-bearing identities move only if the serialized item enum requires the documented version advance.
- Run sequentially on `out/cargo-target` under the repository two-job cap: format, strict all-target Clippy, locked build, complete locked workspace tests, release-matched ticket checker, and `git diff --check`.

## Work log

- 2026-09-08T15:50:45Z stage research start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — Reconsidering the next independent fidelity slice after ticket 057 delivered score-driven Waiting and separating the source-proved fresh-draft state from its unobserved external trigger.
- 2026-09-08T15:58:37Z stage research end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — Native and adjacent source frames prove a cleared 0-0 orange-first draft and identify both five-card rows; no frame exposes what ended Waiting, so trigger policy remains outside the slice and all seven newly identified mechanics remain catalog-only.
- 2026-09-08T15:58:37Z stage design start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — Shaping one typed lifecycle operation, complete reset semantics, existing fade/draft reuse, source-order catalog-only offers, shared rendering, public authority and preservation evidence.
- 2026-09-08T15:58:37Z stage design end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — The contract advances the proved post-Waiting state without selecting a timer, player input, host action or readiness rule, and without making any newly transcribed card playable.
- 2026-09-08T16:07:02Z stage review start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a081c0-96a1-7cf2-a4cb-45b52f79db74 — Fresh-context admission review of exact range `6f90f253dffc4cd978cd366db0d090008f14b380..84df0c8d5246bc83e03d84a481a87ba6c11085eb` against the source, current authority seams and the risk-4 admission bar.
- 2026-09-08T16:07:02Z stage review end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a081c0-96a1-7cf2-a4cb-45b52f79db74 — ADMIT at risk 4 with no findings. The contract separates reset semantics from its unseen trigger, keeps seven source-visible cards catalog-only, reuses the existing state machine, and has finite public, rendered, transport and preservation evidence.
- 2026-09-08T16:07:02Z — Admitted: status set to ready by top-level session codex:01a07f87-97d0-7751-825a-29e87e8fe64a. Implementation remains unreviewed.
