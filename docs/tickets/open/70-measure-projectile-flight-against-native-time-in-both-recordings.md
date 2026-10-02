---
format: 3
status: idea
created: 2026-10-02T15:44:47Z
origin: agent-proposed
tags: ["rounds", "fidelity", "research", "projectiles"]
value: 8
risk: 4
sessions:
  - codex:01a0fd03-d222-7cf2-a225-29c243f2337e
execution: unattended
parent: 59
depends-on: [68]
supersedes: []
split-from: []
---

# Measure projectile flight against native time in both recordings

Establish what projectile motion the supplied recordings support before changing the game's general weapon physics. The existing lime source record fits curves in image space; choosing speed or gravity also needs elapsed-time measurements and a check for camera movement.

## Outcome

- Deliver frame-addressable tracks for the second recording's blue shot A and orange shot B already identified by ticket 68, plus one continuously identifiable ordinary projectile from the first recording. State each fighter's visible loadout and exclude collisions, reflected sections and ambiguous identity from free-flight fitting.
- Measure x and y against elapsed integer native PTS. Check camera translation, scale and rotation against static arena contours in the same frames, report uncertainty, and distinguish image motion from world motion using the existing measured arena transforms.
- Compare constant velocity, constant gravity and, only if justified by residuals, linear drag. Define each candidate equation and distinguish its functional form from its shared parameters. Fit each shot's initial position and velocity independently; test shared gravity and, where justified, shared drag against independent fits. Do not require equal launch speeds or infer base speed/card multipliers from differently loaded shots without identifying evidence. Ordinary means eligible non-Homing free flight, not a proven unmodified weapon. Report whether the eligible tracks identify, merely permit, or reject each shared-parameter hypothesis within combined measurement and camera uncertainty. A Homing-bearing track is a compatibility comparison only. If fewer than two eligible non-Homing tracks remain, report the cross-recording shared-model question as unidentifiable; this is an acceptable research result.
- Provide reproducible fitted values, residuals and uncertainty for supported models. If the data cannot distinguish models, camera correction is unreliable, or no shared model fits, report the exact limitations and conflicting samples. Completing this research establishes evidence; it neither implements a weapon law nor closes a gameplay coverage gap.
- Propose the smallest next implementation or measurement contract justified by the result. Keep source-observed events distinct from constructed replay input schedules. Include the current general projectile and damage behavior as context for that proposal, without changing product code or admitted contracts.

## Decisions

- Goal 59 delegates this reversible source investigation. Wait for ticket 68's corrected, independently reviewed source record before starting. Tickets 52, 66 and 69 remain the serial playable queue and do not depend on this research.
- Use only the two hash-identified recordings in reference/manifest.json. Verify file hashes, native resolution, timebase and cited packed-RGBA identities. Reuse retained evidence where suitable; any additional first-recording decode stays at native PTS <= 2620000000 and any second-recording decode at <= 2300000000. A missing usable first-recording track is an explicit evidence limitation, not permission to extend the range.
- Own the sole headless FFmpeg decoder slot, at most two threads, with the pinned release already used for source evidence. Use bounded dense windows around selected free-flight tracks rather than decoding whole recordings. Preserve all previous evidence and dirty worktrees. This ticket uses no Cargo, GPU, visible window or proprietary product asset.
- The consultant's numerical estimates are hypotheses, not constants. Do not treat a spatial parabola as proof of constant acceleration over native time, or changing image-space speed as proof of drag or a force. Measure camera drift first. Fitted values remain model estimates with residuals, never claimed recovery of hidden source constants.
- No profile-specific flight law, scripted poses, invented preceding damage/loadouts, inert card-selection mode or product changes. Reload, ammunition, gun-dot interpretation, damage and card-rule fidelity remain separate unresolved work.
- Keep retained frame identities, coordinates, fitting scripts and decoder commands under ignored root out/ticket-NNN. Publish source rationale in the repository's existing documentation structure and link the coverage ledger without marking gameplay implemented.

## Evidence required

- A bounded headless validator reruns coordinate/time fits and residual tables from retained inputs in minutes. It checks the source hashes, cited PTS/RGBA identities, track continuity, static contour samples and camera correction. Preserve commands and dependencies; a reviewer can inspect original-resolution cited PNGs independently.
- Tables identify every sample, native time, head-center uncertainty, occlusion and visible interaction. Compare raw and camera-corrected tracks, each model's residuals and shared versus independently fitted parameters. State how uncertainty affects the conclusion; a negative or non-identifying result is acceptable when supported by these tables.
- Identify source-observed muzzle, contact and effect bounds relevant to the proposed implementation. Clearly separate them from the clone's current constructed input rows and unverified assumptions.
- Ticket validation and git diff --check pass. A fresh other-family exact-range review checks the fit method and source facts before guarded integration. No product tests substitute for this source evidence, and neither full recording match is declared complete.

## Chat excerpts

Adam — [goal coordinator](http://ivy.localhost/sessions/codex/01a0fd03-d222-7cf2-a225-29c243f2337e), 2026-10-02:

> Finish the game

## Work log

- 2026-10-02T15:44:47Z stage design start session codex:01a0fd03-d222-7cf2-a225-29c243f2337e — Consultation exposed the missing elapsed-time and camera check; split source preflight from a future general weapon implementation.
- 2026-10-02T15:44:47Z stage design end session codex:01a0fd03-d222-7cf2-a225-29c243f2337e — Idea only, awaiting independent admission and corrected source ticket 68; no model value or product change admitted.
- 2026-10-02T16:17:03Z Fresh independent admission by codex:01a0fd5d-898c-7e92-a91f-ed4bed64148e returned one ambiguity: shared functional form versus shared parameters. Clarified per-shot initial conditions, eligible non-Homing versus unmodified weapons, shared gravity/drag tests and non-identifiability. Risk4 and dependency68 unchanged; renewed admission pending.
