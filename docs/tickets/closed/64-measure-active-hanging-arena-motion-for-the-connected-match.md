---
format: 3
status: closed
created: 2026-10-02T04:44:32Z
origin: agent-proposed
tags: ["rounds", "product-fidelity", "physics", "research"]
value: 8
risk: 3
sessions:
  - codex:01a0fa96-a468-7332-92f6-c36c3908e4b9
  - codex:01a0faf2-6ae0-7df0-b427-a74271f729e3
execution: unattended
parent: 59
depends-on: [55]
supersedes: []
split-from: []
---

# Measure active hanging-arena motion for the connected match

The connected match stops at a held hanging arena because its active motion and contacts have not been measured. Establish what the following source frames actually show so the next playable continuation can choose a suspension and control model without importing the coupled experimental mechanics from old worktrees.

## Outcome

- Extend the first recording's hanging-entry evidence from native PTS 2585489658 through the next visible result or draft, stopping no later than 280 seconds. Identify the actual active interval and its end from decoded frames rather than treating the proposed endpoint as an observed transition.
- Record body and square centers, orientation, visible links, fighter body and health-bar centers, and observable contact/separation for representative samples across the active interval. Include dense adjacent frames around control departure, at least one meaningful hanging-body deflection, and the first visible fighter/body interaction. If an event does not occur in the interval, record that absence rather than inventing it.
- Distinguish source-supported behavior from uncertainty: independent body rotation/translation, square/link movement, possible support, onset uncertainty, and whether geometry behaves as rigid groups or independent bodies. Name observations that reject a static held scene and any alternative models the evidence cannot distinguish. Do not claim recovered mass, inertia, solver topology, weapon constants, or physical inputs from appearance alone.
- Deliver a durable source-bound record and a concrete proposed next playable contract: visible interval, required authoritative behavior, public interfaces it can reuse, measured tolerances and remaining uncertainties. State whether it can advance connected play independently of reload preserved Quick Shot reload/ammunition experiment 056; if not, identify the exact observed dependency and the evidence needed next.

## Decisions

- Goal 59 delegates reversible choices. This is a finite measurement prerequisite for full connected play, not closure of a gameplay coverage gap. Coverage remains explicitly unresolved until a later playable delivery proves it.
- Use recording reference/MedalTVRounds20260903165304088.mp4, SHA-256 453954a7230401ed805be4e53dec41779a1913dfd69903671fc131fca2c8a18c, native 1280x720 and native integer PTS. Reverify the supplied file hash and the existing 5940/5941/5942 packed-RGBA hashes before extending their evidence. Preserve source media unchanged.
- Existing held entry has 21 nominal bodies and 42 presentation links; those counts are observations to check, not an admitted Rapier constraint topology. Never infer physical joints simply because the renderer draws links.
- Change source records and evidence only. No simulation, card, weapon, input table, renderer, network or capture-command behavior changes in this ticket. Do not import 048/056 candidate mechanics or proprietary source/art/audio bytes into product assets.
- Work in an isolated detached worktree. Use headless decoding with at most two threads and one decode process at a time; do not launch Cargo, GPU rendering or visible GUI while 52/63 own those slots. Sparse full-interval samples and dense short event windows avoid retaining several gigabytes of raw video unnecessarily. Retain only the evidence needed to reproduce measurements under ignored out/ticket-064.
- A fresh review checks the exact candidate and its evidence before integration to main. Existing dirty root files and preserved experimental worktrees remain untouched.

## Evidence required

- Reproducible decode commands, decoder identity, source-file hash, per-frame native PTS and SHA-256 over exactly 3686400 RGBA bytes, with retained comparison images. Reverify prior hashes and report any disagreement before relying on them.
- A coordinate table with stable object labels, units, visibility/occlusion notes, uncertainty and enough adjacent samples to support each motion or contact claim. Compare measurements with original-resolution images; masks must avoid translucent fighter trails and background/link confusion.
- Independent review reproduces at least one bound source hash, checks sampled positions/orientations against original-resolution frames, and challenges the proposed model distinction and playable follow-up contract. Unsupported interpretation is corrected before approval.
- Update docs/fidelity/held-hanging-entry-observations.md or a linked finite active-arena record, and the relevant coverage rows without marking research as implemented behavior. Validate ticket records and the diff; product tests are unnecessary because product code is unchanged.

## Chat excerpts

Adam — this goal's initiating request:

> Finish the game

## Work log

- 2026-10-02T04:44:32Z stage design start session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Shape a measured prerequisite for the active connected hanging arena while CI and movement workers run.
- 2026-10-02T04:48:55Z stage design end session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Fresh independent admission claude:92cd72a2-3849-4e51-b075-7fb8914dfe44 ADMIT risk3 with no blocking findings. Applied approved spacing corrections and review notes: reverify5940/5941/5942; retained evidence out/ticket-064;056 denotes preserved Quick Shot experiment, not a main ticket. Dependency55 closed; no human decision. Admit under goal59 delegation; worker records this admission in docs/decisions.md with exact candidate.
- 2026-10-02T04:50:51Z stage research start session codex:01a0faf2-6ae0-7df0-b427-a74271f729e3 — Reverify source identity and held-boundary hashes, then sample active interval.
- 2026-10-02T04:56:57Z stage research end session codex:01a0faf2-6ae0-7df0-b427-a74271f729e3 — Source and held hashes match; sampled through PTS2799988800, still active; adjacent frames show fighter departure, separate body motion and later displaced upper piece.
- 2026-10-02T04:59:29Z stage implement start session codex:01a0faf2-6ae0-7df0-b427-a74271f729e3 — Write source-bound active record, coverage rows, admission and reversible next-slice decision.
- 2026-10-02T05:00:12Z stage implement end session codex:01a0faf2-6ae0-7df0-b427-a74271f729e3 — Added bounded source record, unresolved coverage rows, and goal59 admission/choice; no product code changed.
- 2026-10-02T05:00:49Z stage review start session codex:01a0faf2-6ae0-7df0-b427-a74271f729e3 — Submit exact e6d0a541 candidate and bound ignored evidence to fresh read-only Claude CLI reviewer.
- 2026-10-02T05:03:19Z stage review end session claude:006446ca-b1f0-45e0-90bb-ba5f80c56785 — Changes required: evidence access denied, plus no-shot basis, movement dependency and onset wording; full report out/ticket-064/review.json.
- 2026-10-02T05:04:31Z stage correction start session codex:01a0faf2-6ae0-7df0-b427-a74271f729e3 — Address no-shot basis with all 37 adjacent prefix frames, source-fit movement prerequisite and onset wording; reopen full review with evidence access.
- 2026-10-02T05:04:48Z stage correction end session codex:01a0faf2-6ae0-7df0-b427-a74271f729e3 — Inspected every prefix frame; documented no visible shot/HUD change, speed prerequisite and bounded onset in exact tip 0476348.
- 2026-10-02T05:05:00Z stage review start session codex:01a0faf2-6ae0-7df0-b427-a74271f729e3 — Fresh CLI context reviews exact corrected range 377902c..0476348 with evidence access.
- 2026-10-02T05:14:27Z stage review end session claude:7d94a465-5e97-418e-bab9-fbe10a25703a — Reproduced seven hashes and checked geometry; changes required for inner-body landing, tilt sign, late rows and bar centers. Full report out/ticket-064/review-2.json.
- 2026-10-02T05:14:35Z stage correction start session codex:01a0faf2-6ae0-7df0-b427-a74271f729e3 — Extend prefix through blue/408 and orange/401 support; correct signed tilt and bars; measure late upper/lower geometry.
- 2026-10-02T05:15:02Z stage correction end session codex:01a0faf2-6ae0-7df0-b427-a74271f729e3 — Tip fe34660 records inner support, signed angle, corrected bars and late geometry; all 14 stated hashes match retained frame indexes.
- 2026-10-02T05:15:12Z stage review start session codex:01a0faf2-6ae0-7df0-b427-a74271f729e3 — Fresh reviewer checks exact full range 377902c..fe34660 and second-round corrections against original frames.
- 2026-10-02T05:23:25Z stage review end session claude:5e4051be-49e7-49b7-973f-8377fb2c8cde — Confirmed prefix and late source hashes; changes required for bar centers, orange/401 corner versus support and late tilt. Full report out/ticket-064/review-3.json.
- 2026-10-02T05:23:33Z stage correction start session codex:01a0faf2-6ae0-7df0-b427-a74271f729e3 — Re-measure every green bar, inspect 18 after-stop frames, and limit 401 to observed corner/side contact.
- 2026-10-02T05:24:50Z stage correction end session codex:01a0faf2-6ae0-7df0-b427-a74271f729e3 — Tip 1e09fc8 corrects all green bars by pixel row, limits 401 to corner/side contact, and records after-stop tilt; 15 hash rows and ticket check pass.
- 2026-10-02T05:25:01Z stage review start session codex:01a0faf2-6ae0-7df0-b427-a74271f729e3 — Fresh read-only review exact range 377902c..1e09fc8 with original source and corrected bar/contact record.
- 2026-10-02T05:29:59Z stage integration start session codex:01a0faf2-6ae0-7df0-b427-a74271f729e3 — Fresh review claude:398b81f5-4d8f-4fbb-8a21-fb283a49f418 APPROVE exact 377902c..1e09fc8; source hashes and bar/body measurements reproduced; nonblocking later401 support note in out/ticket-064/review-4.json.
- 2026-10-02T05:30:29Z stage review end session claude:398b81f5-4d8f-4fbb-8a21-fb283a49f418 — approved candidate 377902c0514a09a2b6d85f2e3f07612281c3458f..d08ba19abb2f5be06ff0d9f766e0e8b605173d43
- 2026-10-02T05:30:29Z stage integration end session codex:01a0faf2-6ae0-7df0-b427-a74271f729e3 — integrated d08ba19abb2f5be06ff0d9f766e0e8b605173d43 as 8aed6efc033d5a54dc4bd18b8234cce1b7df96c1
