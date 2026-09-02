---
format: 3
status: idea
created: 2026-08-29T02:31:59Z
origin: human-request
tags: ["product-fidelity", "cards", "simulation"]
value: 10
risk: 4
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: [15, 16, 17, 33]
supersedes: []
split-from: []
---

# Verify and gate current ROUNDS card mechanics

The runtime uses exact names for 16 of 67 cataloged ROUNDS cards, but several mechanics and composition rules are provisional. Verify this current subset against the installed target build, correct every mismatch, and permit only the single cards and combinations whose complete reachable behavior is directly supported.

## Outcome

- Every selectable card has the exact sourced ROUNDS name and observed single-copy mechanics, modifiers, timing, targeting, and presentation summary.
- Every duplicate and cross-card combination reachable through the draft is directly verified. Until a combination is verified, draft/runtime validation prevents that copy or combination from being selected or played; no provisional composition remains reachable behind a disclaimer.
- Ticket 015's temporary incomplete-fidelity gate is removed or advanced only enough to make the verified current subset playable. The other 51 cataloged cards remain absent until ticket 025.

## Decisions

- Audit the existing 16 before expanding the pool. Exact names do not prove exact mechanics.
- Prefer installed-build controlled comparisons and official patch behavior over community displayed-value guesses. Preserve conflicts and uncertainty explicitly.
- Correct the smallest shared deterministic behavior surface required by the current 16; never replace a hard ROUNDS mechanic with a stat-only approximation.
- Keep raw ROUNDS captures external and gitignored. Commit source/build/state/frame coordinates, hashes, derived modifier/behavior observations, and independently generated clone evidence.

## Evidence required

- A 16-card fidelity matrix names the direct evidence and verdict for every implemented modifier, behavior, duplicate, and reachable cross-card rule; each mismatch has a failing regression before correction, and every unresolved combination is proven unreachable.
- Controlled scripted matches exercise every permitted current card and combination boundary with exact inputs, deterministic hashes, draft/play integration, and user-visible verification including modified projectile presentation where applicable. Large-volume self-play is owned by ticket 022 and is not required to close this audit.
- Full build, simulation/replay/history, repository, ticket, whitespace, and monitor-4 native gates pass without copied proprietary assets or source code.

## Work log

- 2026-08-29T02:31:59Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Filing exact-mechanics audit and expansion for the 67-card target after preserving ticket 014's corrected names.
- 2026-08-29T02:33:55Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Required an evidence matrix and correction of the existing 16, with unsupported or unresolved cards and combinations omitted rather than approximated.
- 2026-08-29T02:50:41Z stage correction start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Removing the self-play dependency deadlock by limiting this ticket to current-card verification and gating before expansion.
- 2026-08-29T02:50:41Z stage correction end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Assigned scripted current-subset evidence here, large-volume self-play to ticket 022, and all 51-card expansion to ticket 025.
- 2026-08-29T23:26:59.9104995Z stage correction start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Ordering verified card presentation summaries after ticket 033 removes the currently unsupported runtime summary surface.
- 2026-08-29T23:27:32.3911171Z stage correction end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Added ticket 033 as a prerequisite so verified card presentation summaries can only be restored after the unsupported summary implementation is gone.
- 2026-09-02T02:09:33Z — Reflection verdict: wait because the necessary current 16-card audit's direct behavior and presentation acceptance needs tickets 016 and 017, which in turn require ticket 031's blocked installed-build evidence route.
