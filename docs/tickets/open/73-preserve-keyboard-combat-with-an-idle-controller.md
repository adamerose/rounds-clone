---
format: 3
status: ready
owner: claude:42f9a4e1-c84a-4ea2-831b-1b038f91fce9
created: 2026-10-02T16:19:26Z
origin: agent-proposed
tags: ["rounds", "completion", "implementation"]
value: 8
risk: 2
sessions:
  - codex:01a0fd03-d222-7cf2-a225-29c243f2337e
  - claude:42f9a4e1-c84a-4ea2-831b-1b038f91fce9
execution: unattended
parent: 59
depends-on: [65]
supersedes: []
split-from: []
---

# Preserve keyboard combat with an idle controller

An attached neutral controller currently replaces keyboard combat input while keyboard draft controls still work. Keep keyboard movement and actions usable in both local and online play when the assigned controller is idle.

## Outcome

- In local visible-flow and online join/host, an idle attached controller no longer prevents the affected player from using documented keyboard movement, jump, block, fire and manual aim.
- Active mapped controller input retains precedence; when it becomes neutral the slot's keyboard input works again. Inputs remain isolated to the intended local or assigned online player.

## Decisions

- Fix device selection at the existing presentation input boundary. Determine controller inactivity using existing movement and manual-aim dead zones plus mapped combat buttons. Ignore stick noise inside those dead zones.
- Preserve current controller-only behavior, player assignment, active-controller precedence, draft/rematch commands, semantic PlayerInput and the wire protocol. Add no device assignment menu, persistent ownership scheme or input framework.
- This is a usability policy chosen under goal59 delegation, not a claim about original-game input devices. Physical controller operation must not be claimed from an injected Bevy event test.
- Preserve unrelated root edits and active worker worktrees. Native verification must wait until the sole Cargo/GPU/visible slot is available; reuse root out/cargo-target with two jobs and monitor4 for any visible evidence.

## Evidence required

- Before editing, reproduce neutral-controller suppression through the existing Bevy input boundary, covering both local player slots and both online assigned slots. Keep a focused regression at this boundary.
- Verify neutral pad plus keyboard actions/manual aim, release to neutral, stick noise within dead zones, active controller precedence, controller return to neutral and removal. Assert only the intended slot receives each input.
- Use the real online adapter in a focused applied-input trace check to show the selected keyboard input reaches the authority and changes validated snapshots. Keep ordinary gameplay and flow behavior unchanged.
- Run required format, strict all-target Clippy, locked workspace build/tests and relevant input/network checks using the shared prepared target. Verify supported visible keyboard play on monitor4 when collecting native evidence. Report physical-device testing separately and explicitly as unavailable if it cannot be performed; it is not required to establish the boundary correction.
- Complete fresh other-family exact-range review and guarded ticket delivery. This correction does not claim complete recording or controller-hardware fidelity.

## Work log

- 2026-10-02T16:19:26Z stage design start session codex:01a0fd03-d222-7cf2-a225-29c243f2337e — Shaped from the completed independent Astra planning batch for goal59; no implementation claim.
- 2026-10-02T16:19:26Z stage design end session codex:01a0fd03-d222-7cf2-a225-29c243f2337e — Idea contract ready for separate fresh-context admission.
- 2026-10-02T16:29:58Z Fresh independent admission codex:01a0fd6c-3361-7153-8d15-c905a8e1e923 ADMIT unchanged at risk2, dependency65, with no operator decision. The local and online device-selection branches establish the cause; existing dead zones, active-controller precedence and slot isolation bound the correction. Windowless Bevy regressions plus real-adapter trace provide behavioral evidence; physical hardware is reported separately. The executing worker records this admission in docs/decisions.md with its reviewed candidate. Native verification waits for the assigned shared resource slot; no executed reproduction or game delivery is claimed by admission.
- 2026-10-07T06:08:59Z stage implement end session claude:42f9a4e1-c84a-4ea2-831b-1b038f91fce9 — Reproduced idle-controller suppression in local slots 1-2 and online via pre-fix selection rule; fixed in quarrel-presentation input selection; fmt, strict clippy, locked build and workspace tests pass.
- 2026-10-07T06:17:38Z stage integration start session claude:42f9a4e1-c84a-4ea2-831b-1b038f91fce9 — reviewed 44189112a705b9fa76f420aea7ab0fb6cde26857..5dc0ad97ee91fc178b6d2dc4855d496b6dc36d42 as 28132b904d1467b854e05064a24244034d7fc113..4ece8085f5252b6d7ec9d4e1588d0511db36fecd; reviewer codex:01a114fe-2d18-7342-9fae-67f9b70a7d99 approved.
