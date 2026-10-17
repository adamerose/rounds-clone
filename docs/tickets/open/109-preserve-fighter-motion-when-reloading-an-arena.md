---
status: idea
created: 2026-10-17T19:10:53Z
origin: system-detected
tags: [quarrel, simulation, hot-reload, bug]
value: 6
risk: 3
sessions:
  - codex:01a148af-57cb-7751-96dc-e93972e0b69f
execution: unattended
parent: 105
depends-on: [107]
supersedes: []
split-from: []
---

# Preserve fighter motion when reloading an arena

Saving a valid arena edit teleports active fighters to spawn and clears their velocity, including when only an object's color changed. Keep the ongoing fight continuous while replacing the edited arena.

## Outcome

Arena hot reload does not itself reset fighter position or velocity. Preserve active fighter physics state and the established match-state preservation guarantees while applying valid arena edits; normal initialization and fight transitions still reset fighters at the correct spawns.

## Decisions

- Destination: main. Bounded lifecycle correction, no new game flow, physics tuning, protocol, asset format or generic state-preservation framework.
- Exact source snapshot305b25219c3bb92c7207131f839aa91f348f4e28: physics/combat.rs reload_arena_if_changed accepts the edit and calls physics.replace_arena; physics.rs replace_arena ends in reset(), whose player loop moves every fighter to distributed spawns and zeroes linear velocity. Inspect current origin/main and all callers before choosing the smallest correct seam.
- Color-only edit provides unambiguous failure evidence: it must change appearance without teleporting fighters. This is an implicit continuity defect; current architecture explicitly lists preserving ticks/phase/points/cards/health/elimination but does not explicitly list position. Do not misquote it as an explicit pose requirement.
- Preserve existing valid-edit geometry/object/transient-projectile replacement policy and malformed-edit fallback. Natural collision effects of edited geometry remain normal physics, not a forced spawn reset.
- Initialization, new fight, rematch and normal reset_fight semantics retain intended spawn placement and velocity reset. Avoid a mode/special case that merely skips reload for color edits; fix the reset ownership boundary.
- Use a minimal static arena and identical control authority for regression. Move fighters away from spawn and retain nonzero motion before the reload tick; edit only color in one authority. Compare post-step fighter state with the unchanged twin at the same tick, within existing public snapshot quantization. Do not assert unchanged coordinates across time or compare full hashes that include the intentionally changed color.
- Inspect any explicit operator decision before changing behavior; if one intentionally requires hot-reload teleportation, block for that decision. Earlier agent implementation wording alone is not such a decision.
- Queue behind107; one native Cargo/shared journal writer, jobs<=2, canonical shared out/cargo-target, no clean target/linker/profile/debug changes. No GUI needed for causal proof; preserve root human files, old worktrees/proof/cache.
- No speculative guards, retries, longer waits or arbitrary soak.105 explicitly authorizes current Codex usage through100percent with no paid overflow; no unreviewed integration if quota ends.

## Evidence required

- Keep the smallest public simulation/asset-boundary regression that fails before and passes after: accepted cosmetic edit visible in snapshot while active fighter pose/velocity continue identically to the unedited control. Use ordinary input/flow APIs, not a new testing hook or private-body mutation.
- Regression evidence preserves health/elimination, phase/clock/points/cards, invalid-edit behavior and existing projectile/arena replacement policy. Existing initialization/fight-transition tests remain passing and explicitly verify intended spawn/reset behavior at the changed callers.
- Complete normal tools/verify.ps1 checks with attributable source/artifacts; focused lifecycle regressions before the expensive broad build. Applicable ticket/docs/diff checks, known legacy Markdown error reported truthfully.
- Fresh independent exact-candidate CLI review, main integration/close and exact owned-worktree cleanup. If a real ambiguous product decision or exhausted review quota appears, checkpoint clean work and block precisely.

## Work log

- 2026-10-17T19:10:53Z Fresh current_sim_reflect fetched current origin/main first and traced the live reload reset chain. Pure read-only finding, no implementation or claimed test pass; independent admission required.