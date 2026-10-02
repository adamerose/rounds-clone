---
format: 3
status: ready
owner: codex:01a0faf2-6ae0-7df0-b427-a74271f729e3
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
