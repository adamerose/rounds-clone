---
format: 3
status: ready
created: 2026-08-30T03:14:10Z
origin: agent-proposed
tags: ["verification", "projectiles", "playtesting"]
value: 7
risk: 4
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: [15, 30, 33]
supersedes: []
split-from: [32, 34]
---

# Add an unattended base-projectile evidence state

Ticket 034's source correction cannot be visually approved without rendering an exact-candidate projectile, but reaching that transient state through interactive play would require operating-system input and could disrupt the user.
Extend the existing monitor-attested debug evidence protocol with one deterministic, frozen base-projectile state so a later renderer run can capture the exact final composite without interactive play.

## Outcome

- A debug-only command-line route constructs an active `arena-006` world through the real simulation API and vanilla player/combat profiles.
- The frozen state contains exactly one ordinary owner-0 projectile in flight, with no cards or custom combat profile involved.
- The route draws one frame, writes one new absolute PNG without overwriting an existing destination, emits a projectile-specific attestation, and exits automatically.
- Ordinary play, replay, the incomplete-fidelity boundary capture, and agent playtesting retain their existing routing and behavior.
- The route never reads global input and never enters continuous physics.

## Decisions

- Reuse `DebugEvidenceCaptureProtocol` and the existing viewport/monitor attestation lifecycle rather than adding another capture implementation.
- Give projectile evidence its own exact completion/error marker namespace; preserve every byte of the existing incomplete-fidelity evidence markers.
- Reach the projectile through `World.CreateMatch` and `Sim.Step`, using embedded `arena-006`, `PlayerTuning.Vanilla`, `CombatTuning.Vanilla`, and two `PlayerCombatProfile.Vanilla` instances.
- Fire upward from player 0 on the first active tick so the projectile is visible, remains clear of the arena geometry, and is produced by ordinary fire mechanics.
- Keep this ticket infrastructure-only: it enables later visual verification but does not itself prove the projectile matches installed ROUNDS.
- Do all implementation and verification headlessly with at most two logical processors; do not launch Godot, a renderer, a browser, a window, or any input mechanism while the user is playing.

## Evidence required

- Route tests prove the new argument is accepted only for debug builds, requires an absolute PNG path, remains mutually exclusive, and has no exposure through ordinary/release or replay routing.
- Factory tests prove repeated construction has one stable hash, active `arena-006`, vanilla profiles, and exactly one owner-0 base projectile produced by the simulation.
- Lifecycle tests prove both debug evidence routes are frozen and use distinct exact markers while the original marker bytes remain unchanged.
- Source inspection proves the route reuses the existing one-frame capture method and never reaches global input or continuous physics.
- A zero-warning solution build, all applicable simulation and checker tests, repository checks, ticket format, and `git diff --check` pass under the bounded headless toolchain.
- No native visual result is claimed until a later exact-candidate monitor-4 renderer run produces the PNG and projectile-specific attestation.

## Work log

- 2026-08-30T03:14:10Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a050a7-6cc6-7081-bf1d-7003d8eb12d5 — Split the missing transient-projectile evidence state from ticket 034's correction and ticket 032's broader closed-loop playtesting route.
- 2026-08-30T03:14:10Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a050a7-6cc6-7081-bf1d-7003d8eb12d5 — Bound one debug-only frozen state to real vanilla simulation APIs, one create-new absolute PNG, route-specific attestation, existing monitor checks, zero input, and zero continuous physics.
- 2026-08-30T03:14:10Z stage implementation start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a050a7-6cc6-7081-bf1d-7003d8eb12d5 — Extending the shared debug evidence protocol and deterministic match factory without launching Godot or any visible process.
- 2026-08-30T03:16:49Z stage admission start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a050ab-1751-7bd3-82e0-739793113f2a — Cold-read the agent-proposed risk-4 contract for route isolation, real vanilla simulation state, create-new output, bounded lifecycle, and later visual evidence.
- 2026-08-30T03:18:01Z stage admission end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a050ab-1751-7bd3-82e0-739793113f2a — Admitted at risk 4 with no findings. Uncommitted implementation had begun prematurely at 03:14:10Z and was paused before this independent verdict; no commit or integration occurred before admission.
- 2026-08-30T03:18:01Z stage implementation start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a050a7-6cc6-7081-bf1d-7003d8eb12d5 — Resumed the isolated uncommitted implementation after independent admission.
- 2026-08-30T03:23:54Z stage implementation end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a050a7-6cc6-7081-bf1d-7003d8eb12d5 — Added the debug-only frozen projectile route, real vanilla `arena-006` simulation state with stable hash `6a25f798f6582a29`, distinct attestation, and atomic create-new publication; passed a zero-warning bounded solution rebuild, 134 checker tests, 255 applicable simulation tests, repository checks, the exact 73-file identity boundary at `7bee1ee96a4a02ca32cba66ff04ca0403ba382d78bbd6779fb2d7e345d9bd185`, six-worktree/two-branch ticket format, and whitespace checks. No Godot, renderer, GUI, browser, GPU workload, or input ran, and no build or test process remained.
