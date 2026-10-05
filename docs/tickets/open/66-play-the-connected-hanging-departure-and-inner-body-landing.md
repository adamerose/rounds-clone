---
format: 3
status: ready
created: 2026-10-02T05:58:48Z
origin: agent-proposed
tags: ["rounds", "product-fidelity", "physics"]
value: 8
risk: 4
sessions:
  - codex:01a0fa96-a468-7332-92f6-c36c3908e4b9
execution: unattended
parent: 59
depends-on: [52, 55, 64, 65]
supersedes: []
split-from: []
---

# Make the connected hanging arena playable through the first inner-body landing

The connected replay currently stops at the held hanging entry. Continue ordinary authoritative play through the measured departure and first inner-body landing, ending before the source establishes moving suspended bodies or firing. This is one bounded step toward the two complete online matches; later hanging combat and suspension remain uncovered.

## Outcome

- Preserve every delivered connected anchor through held tick 5941 / native PTS 2585322992. Continue to tick 5987 / PTS 2592989628 through normal public move/jump input and the existing 60-Hz authority, flow, snapshot and shared renderer. Retain scores [0,1], halves [0,0], Da Qu / Ex badges and both living fighters.
- Provide actual authoritative support/separation at outer body 400, collision/separation at 409 and blue's inner body 408 landing. The source does not establish top-face support by 409; name the actual contact from which the reconstructed blue route jumps without claiming a recovered source collision callback. Orange reaches 401's top-left corner at the stop; that contact is not claimed as grounded support. Fighters move only from ordinary physics and public input, never from assigned poses or presentation animation.
- Match the delivered source observations at PTS 2585322992, 2585489658, 2585656324, 2586989652, 2591489634, 2591822966 and 2592989628. Reuse their full native RGBA identities from docs/fidelity/active-hanging-arena-observations.md. At the final frame fighters are within eight native pixels of orange (307,261), blue (955,275); tracked bodies within four native pixels and upper/lower rows within approximately three pixels of measured nominal stations. At every quantitatively measured anchor including the final frame, fighter body and health-bar centers must each be within eight native pixels and tracked body centers within four pixels of the delivered record. Report both body and bar error separately. PTS 2585656324 currently has only an identity and qualitative description: either verify its described pose change qualitatively without inventing a center, or measure and review a center row before adding a numeric claim.
- Reproduce blue's visible leg-first touchdown at PTS 2591656300..2591822966 and supported pose at the stop. The source does not reveal the first solver contact: its body center at PTS 2591822966 is y261, while a radius-12 ball rests at y273 on the measured top. Require real authoritative 408 support by PTS 2592989628 and retain the actual first contact tick as evidence; do not equate visible foot contact to an inferred solver tick. The shared renderer must reproduce the measured body, bar and visible touchdown without assigning a pose or hiding the body-center error. Retain rendered foot position relative to the measured 408 top at y285 for PTS 2591656300 and 2591822966; reverify those exact native PTS/hash identities. Use the source record's visual measurement uncertainty, state the measured foot-to-top distance explicitly, and leave touchdown unverified if the source/capture evidence cannot establish it. No fire input, projectile, impact, damage, ammunition or reload claim enters this route. Snapshot, network and received-state presentation agree through the extended prefix.
- Coverage records the precise delivered prefix and leaves later moving-body, projectile, explosion, result and draft behavior open.

## Decisions

- Goal 59 delegates reversible choices. Ticket 64 establishes a prefix with no observed geometry displacement above measurement uncertainty and no visible shot or gun-dot count change. Static authoritative support geometry is justified only within this prefix, not as the later suspension model. Existing visible links are not proof of joint topology.
- Read GOAL.md and the delivered source record before implementation. Initial activation follows the held frame and is witnessed no later than PTS 2585489658; its actual input onset is unknown. Select and report a deterministic public-input reconstruction, not recovered original controls. Preserve entry visuals and loadouts while enabling the active flow through the existing model.
- Use the movement law published on main when work begins. Ticket 52 is now an ordering dependency: fresh admission identified a predicted 0.08 shortfall for blue's first ten airborne ticks (about 13px versus the observed 30px). Its shared-control delivery is a prerequisite, but still prove reachability and all source comparisons rather than assuming 0.39 solves the route. If a source-required path cannot be reached, retain the exact public-input evidence and separately admit its cause correction. Do not tune physics per arena, alternate stick each tick to trace a pose, teleport, or import preserved 048/056 experiments.
- Dependency 65 orders integration with the live authority/received-state presentation seam. Use those public paths after it closes; introduce no second authority or rendering implementation. Keep ordinary play beyond the bounded replay possible, but claim no verified later behavior from this ticket. Collider bodies and visible placements come from the one existing HANGING_LAYOUT representation. Use solid fixed colliders for the measured bodies and square pieces; links are visual and have no collider, since fighters cross their lines. Square solidity is a provisional reversible model choice not established by this prefix. If source comparison proves that choice obstructs the measured route, the worker may omit square colliders generally for this active arena, record the exact evidence and alternative in docs/decisions.md, and rerun the full contract. No per-player or replay-only collision rule is allowed, and no engine-shape recovery is claimed.
- One worker owns Cargo, GPU, capture and visible verification at a time. Reuse the repository target and two-job cap. Configure every visible window hidden, move the exact window to monitor 4, verify its center, then show it. Headless source decoding uses one pinned FFmpeg process with at most two threads. Preserve all unique source and earlier experimental worktrees.

## Evidence required

- Reproduce the current held phase refusing ordinary active movement at the public simulation boundary before editing. Identify the flow/physics boundary causing the stop.
- Public regression covers retained flow/loadouts, input-driven fighter movement, real 408 grounding by the stop, separately recorded visual touchdown and solver contact, corner-only orange contact, nominal body layout and no combat event through 5987. Establish intermediate positions from actual snapshots; do not infer them from the final screenshot.
- Two-client authoritative smoke and live received-state presentation demonstrate the extended phase and canonical snapshot agreement. Distinguish automation, semantic input injection and actual physical input evidence honestly.
- Reverify recording SHA and all paired native PTS/RGBA identities, capture the exact candidate's source anchors through the shared renderer at 1280x720, and retain a measured fighter/bar/body comparison table with uncertainty. Identify unchanged anchors before the edit and verify the previous seven-profile gates and connected anchors through 5941 still pass.
- Sequential prepared-target format, strict all-target Clippy, locked build, full workspace tests, supported inspect/smoke/capture commands and a monitor-4-verified visible run pass. Report unavailable evidence and leave coverage open when a required source comparison fails.
- Complete ticket and decision records before fresh independent exact-range review. Delivery requires the entire bounded contract; retained evidence is verified before exact worktree cleanup.

## Work log

- 2026-10-02T05:58:48Z stage design start session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Framing the next playable prefix from delivered source ticket 64 and a bounded read-only assessment; no product code or builds.
- 2026-10-02T05:58:48Z stage design end session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Idea preserves all prior anchors and scopes real support through 5987 without claiming later dynamics or reload. Fresh admission is still required; worker 65 owns the build/render slot.

- 2026-10-02T06:05:05Z Fresh admission claude:d45c3da1-031c-43d4-920d-73aa1838407a RETURN: separated visible leg-first touchdown from unknown solver contact, added 52 ordering dependency from the predicted shortfall, stated intermediate body/bar limits, made unmeasured anchor qualitative, chose shared solid body/square geometry with noncolliding links and corrected the unsupported 409 support claim. Renewed admission pending; no implementation.

- 2026-10-02T06:08:27Z Renewed admission claude:d45c3da1-031c-43d4-920d-73aa1838407a ADMIT at risk 4 after six corrections; dispatch only once 52 and 65 close. Clarified provisional square colliders require concrete source failure before general omission, final bar centers use the same eight-pixel limit, and visible foot-to-platform evidence is retained separately from solver contact. Feasibility and implementation remain unproven.
- 2026-10-05T14:11:30Z Abandoned: superseded by the 2026-10-04 QUARREL direction (GOAL.md, docs/roadmap.md) and run ticket #75; Adam approved closing it. The footage-fidelity goal it served no longer exists.
