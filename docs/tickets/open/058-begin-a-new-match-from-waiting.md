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
- 2026-09-08T16:08:38Z stage implement start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/implement_058 — Implementing the admitted non-player Waiting lifecycle boundary, complete reset, source-ordered catalog-only offers, shared rendering, and public regressions without assigning an external trigger policy.
- 2026-09-08T16:46:49Z stage implement end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/implement_058 — The public lifecycle request now resets match-owned state once, performs the scene-side cleanup and grounded respawn, reuses the ordinary fade into the exact two offer rows, and renders seven unknown-rarity catalog-only identities with independent art keys.
- 2026-09-08T16:46:49Z stage verify start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/implement_058 — Verifying native source identities, focused and complete Rust gates, typed local/network/shared-renderer behavior, guarded monitor-4 presentation, and all 100 retained anchors on the prepared two-job target.
- 2026-09-08T16:48:24Z stage verify end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/implement_058 — All three native source hashes, focused regressions, guarded visible placement, 100-anchor preservation, format, strict Clippy, locked build, all 72 workspace tests, ticket validation and diff checks pass; snapshot/network protocols advance to 10/11 solely for the appended serialized catalog identities.
- 2026-09-08T16:53:20Z stage review start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a081a3-177f-7921-afb6-15e04f031afe — Fresh-context review of exact candidate `de0d8bc86f35f83b0763968c9e2696c75efc4a90..6fb51c9e6fa4242942eb4ae2123d1222a1cccaf7` against the admitted contract, native source frames, renderer capture, transport behavior and preservation evidence.
- 2026-09-08T17:01:55Z stage review end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a081a3-177f-7921-afb6-15e04f031afe — RETURN with two P2 findings: Parasite capitalization did not exactly match the native frame, and three catalog records retained the superseded 14-total/11-catalog-only counts.
- 2026-09-08T17:01:55Z stage correction start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — Correcting the exact visible Parasite rule and all stale owning catalog counts, then rerunning focused code, documentation and diff gates before re-review.
- 2026-09-08T17:03:28Z stage correction end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — Parasite now preserves the source-visible `More Life steal` capitalization, all owning records agree on 21 total/18 catalog-only definitions, and format, focused catalog testing, stale-wording scan, release-matched ticket validation and diff checks pass.
- 2026-09-08T17:03:51Z stage review start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a081fa-2125-7d62-a871-d7aaf1cab177 — Fresh independent review of corrected exact candidate `de0d8bc86f35f83b0763968c9e2696c75efc4a90..63af2940c8b126c90e05a61c30faa4b0c18f8d47`, including exact-tip source, capture, protocol and preservation evidence.
- 2026-09-08T17:16:02Z stage review end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a081fa-2125-7d62-a871-d7aaf1cab177 — RETURN with one P1 and two P2 findings: flow-bearing retained hashes predated the catalog-text correction, while architecture still described Waiting as exitless and named superseded snapshot/network protocols.
- 2026-09-08T17:16:02Z stage correction start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — Refreshing every flow-bearing capture and preservation record from the corrected executable and repairing the two stale architecture claims without cleaning the prepared Cargo target.
- 2026-09-09T07:43:09Z stage correction end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — Architecture now records the explicit Waiting lifecycle exit and protocols 10/11; the rematch terminal expectation names the source-exact catalog state; all 72 tests, format, strict Clippy, locked build, ticket validation and diff checks pass; and the final executable regenerated 101 metadata/PNG pairs with 100 anchor identities, 700/700 invariant digest matches, 100 expected state moves and 64 expected flow moves.
- 2026-09-09T07:43:30Z stage review start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a0851f-81c8-7e91-a9e3-c4d8e5ffe994 — Fresh independent review of exact full candidate `de0d8bc86f35f83b0763968c9e2696c75efc4a90..d955e39b0d3e7cef3d0a555a767e5850732fe03b`, including exact executable attribution, every retained artifact, public and wire contracts, source frames and guarded monitor-4 presentation.
- 2026-09-09T07:54:34Z stage review end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a0851f-81c8-7e91-a9e3-c4d8e5ffe994 — RETURN with one P2 documentation finding: architecture incorrectly said `LifecycleResult` was carried by snapshot protocol 10 even though it is a direct local authority return and absent from `MatchSnapshot` and network packets; all implementation, source, renderer, evidence and preservation checks otherwise passed.
- 2026-09-09T07:54:34Z stage correction start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — Correcting the protocol sentence to distinguish the direct lifecycle return from the serialized post-request flow state; no Rust source, executable or evidence bytes change.
- 2026-09-09T07:54:34Z stage correction end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — Architecture now states that snapshot protocol 10 carries Waiting, post-request flow state and the expanded catalog, while `LifecycleResult` remains local and unserialized; diff and ticket checks remain the required re-review gates.
- 2026-09-09T07:55:28Z stage review start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a0852a-62fe-7c62-8737-c1b5e50d2c2e — Fresh independent binding review of exact full candidate `de0d8bc86f35f83b0763968c9e2696c75efc4a90..90d479200a5eaebcf525c73e4001f0f7f7e0e573`, including implementation, corrected local-versus-wire lifecycle wording, source, executable attribution, all retained artifacts, preservation and focused public regressions.
- 2026-09-09T08:07:23Z stage review end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a0852a-62fe-7c62-8737-c1b5e50d2c2e — APPROVE with no findings: exact executable and 101 artifacts bind, 100 anchors and 700 invariant comparisons reproduce, source and rendered draft inspect correctly, focused lifecycle/presentation/network gates pass, and the lifecycle result is accurately documented as local and unserialized.
