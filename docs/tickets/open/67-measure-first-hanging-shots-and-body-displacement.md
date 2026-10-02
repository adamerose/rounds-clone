---
format: 3
status: ready
owner: codex:01a0fb39-66a8-7601-99ac-6a2c759a7ef9
created: 2026-10-02T06:02:47Z
origin: agent-proposed
tags: ["rounds", "product-fidelity", "research", "combat"]
value: 8
risk: 3
sessions:
  - codex:01a0fa96-a468-7332-92f6-c36c3908e4b9
  - codex:01a0fb39-66a8-7601-99ac-6a2c759a7ef9
execution: unattended
parent: 59
depends-on: [64]
supersedes: []
split-from: []
---

# Measure the first hanging-arena shots and body displacement

The measured hanging landing prefix stops before firing and suspended-body motion. Bound the first subsequent shot, projectile, hit/explosion and body displacement from adjacent native source frames so a playable combat continuation can choose its required rules without importing experimental ammunition or suspension assumptions.

## Outcome

- Extend the first recording's source evidence from PTS 2592989628 through 262 seconds. Locate the first visible muzzle/projectile event for each fighter, projectile paths, visible health changes, impact/burst onset and affected bodies within that bound. If an event is not observable, report the unresolved interval instead of inventing a precise tick or collision callback.
- Track body 401's corner-contact/side-slide interval and first measurable displacement/tilt, plus body/square/link 408 and nearby 402/405 through the first bright burst. State which distinctions between static geometry, one composite local assembly and separate moving bodies are supported or remain uncertain. No mass, inertia, joint topology or force constant is recovered from appearance alone.
- Count visible gun dots around the first shots where unobscured. Distinguish count change from occlusion, muzzle flash, lighting or a presentation trail. Identify any source evidence for shot spacing, ammunition depletion or refill, and say what cannot establish a cooldown, held trigger or reload rule. Record current delivered loadouts from the source and their relevance to the observed behavior without importing old 048/056 mechanics.
- Deliver a durable source-bound record with exact native PTS, packed-RGBA hashes, measurement uncertainty and a concrete next playable contract through the first observed combat/body event. Link it to the coverage ledger while leaving gameplay coverage open until later implementation and source comparison.

## Decisions

- Goal 59 delegates reversible research choices. This source-only ticket is independent of ticket 52's control fit and ticket 65's live transport work. It does not change product behavior or close a gameplay gap.
- Use reference/MedalTVRounds20260903165304088.mp4, SHA-256 453954a7230401ed805be4e53dec41779a1913dfd69903671fc131fca2c8a18c, native 1280x720 and integer PTS at 1/10000000 seconds. Reverify recording SHA and the delivered stop frame PTS 2592989628/hash 5edef7051bd1d9c0e5e5cdd3493b3d67b9ee6bf4fa648189a0c2f15a37a669b9 before measurement. Existing source labels 400..420 are spatial observation IDs, not recovered engine identities.
- Use delivered ticket 64 evidence and docs/fidelity/active-hanging-arena-observations.md. A frame at 2605489578 is already a displacement measurement, not its onset. Dense adjacent frames must bound onset, and concurrent flashes cannot establish a contact impulse without supporting sequence evidence.
- Work in an isolated detached worktree. No Cargo, executable product, GPU, GUI, physics, network, input-table or renderer edits. Decode headlessly with one pinned FFmpeg 7.1 process capped at two threads; preserve native PTS and RGBA identity. Retain only bounded ignored comparison evidence, not proprietary bytes in product assets. Unique source and experimental worktrees remain untouched.
- Never enumerate environment variables or inspect credentials. Native CLI metadata supplies session IDs. Ticket helpers always use --ref main. Reconcile disjoint documentation integration with active worker 65 without overwriting its or Adam's edits; exact review and guarded integration still apply.

## Evidence required

- Recalculate source SHA, re-decode the stop identity and every cited combat/motion anchor, and retain the exact decoder command, native PTS and packed-RGBA hashes under ignored out/ticket-067.
- Inspect adjacent frames around every proposed onset and provide last-absent/first-present bounds where exact onset is uncertain. Keep fighter/bar/body/square measurements and projectile/burst identification reproducible; label occluded or ambiguous objects rather than continuing guessed identities.
- The proposed next playable contract states the interval and stop event, necessary authoritative mechanics, public-input route, snapshot/render seams, source-comparison tolerances and remaining uncertainties. Explain whether its no-reload scope is supported or whether a measured shot/ammunition dependency requires separate admission.
- Update the coverage link without marking play implemented. Complete the ticket and decision records, release-matched ticket validation and git diff --check, then obtain fresh independent exact-range review. Preserve verified bound evidence at root before exact worktree removal.

## Work log

- 2026-10-02T06:02:47Z stage design start session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Framing the source prerequisite after the measured hanging landing, with no product or build work.
- 2026-10-02T06:02:47Z stage design end session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Idea bounds adjacent-frame shot and displacement evidence through 262 seconds; fresh admission remains required.

- 2026-10-02T06:06:24Z Fresh other-family admission claude:1f8503f4-b6e5-4f30-894b-f95e08951866 ADMIT at risk 3 under goal 59 delegation; about 160 native frames, exact source identities, source-only inference limits and disjoint no-Cargo/no-GPU research accepted. Worker uses out/ticket-067 and the delivered pinned decoder method; report absent one-sided firing without extending the bound.
- 2026-10-02T06:10:10Z stage research start session codex:01a0fb39-66a8-7601-99ac-6a2c759a7ef9 — Reverify source and stop identity, then decode adjacent native frames through 262 seconds.
- 2026-10-02T06:29:50Z stage research end session codex:01a0fb39-66a8-7601-99ac-6a2c759a7ef9 — One 164-frame FFmpeg decode matched source and prior stop; adjacent source frames bound first shots, body tilt and burst.
- 2026-10-02T06:29:54Z stage implement start session codex:01a0fb39-66a8-7601-99ac-6a2c759a7ef9 — Write finite source record, unresolved coverage link and goal 59 choice.
- 2026-10-02T06:30:42Z stage implement end session codex:01a0fb39-66a8-7601-99ac-6a2c759a7ef9 — Wrote source-bound combat record, unresolved coverage link and delegated reversible choice; ticket check and diff check pass.
- 2026-10-02T06:30:46Z stage review start session codex:01a0fb39-66a8-7601-99ac-6a2c759a7ef9 — Submit exact documentation candidate and ignored source evidence for fresh independent Claude review.
- 2026-10-02T06:37:37Z stage review end session claude:97854e71-1503-4b19-92da-d3ac33aed6bb — Changes required: projectile reversal, orange white glow, and observed 3–2–3 gun dots omitted; full report in out/ticket-067/review.json. Independent re-decode unavailable to reviewer.
- 2026-10-02T06:37:47Z stage correction start session codex:01a0fb39-66a8-7601-99ac-6a2c759a7ef9 — Inspect adjacent reversal, glow and gun-dot frames; revise source bounds and playable dependencies.
- 2026-10-02T07:00:53Z stage correction end session codex:01a0fb39-66a8-7601-99ac-6a2c759a7ef9 — Corrected reversal, glow, 3–2–3 dots, blue pre-shot count, source gap and next playable dependencies; 30 anchor hashes, ticket check and diff check pass.
