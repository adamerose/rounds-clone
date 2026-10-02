---
format: 3
status: ready
owner: codex:01a0fa96-a468-7332-92f6-c36c3908e4b9
created: 2026-10-02T03:13:11Z
origin: human-request
tags: ["rounds", "autonomy", "autonomy-goal", "completion"]
value: 10
risk: 4
sessions:
  - codex:01a0fa96-a468-7332-92f6-c36c3908e4b9
execution: unattended
depends-on: []
supersedes: []
split-from: []
---

# Finish the game against both supplied recordings

Deliver the complete clean-room Rust and Bevy game described in GOAL.md, so both supplied recordings can be reproduced as complete online matches rather than isolated demonstrations. Adam delegated reversible implementation and scheduling decisions through ivy:autonomy; this goal remains open until its full evidence passes.

## Outcome

- Both hash-identified reference recordings replay as complete online matches with matching source-visible mechanics, physics, presentation, effects, cards, arenas and match flow.
- Every remaining product gap in docs/fidelity/footage-coverage.md has source-linked delivered evidence; a partial local loop or additional replay slice does not close this goal.
- Supported server, client, bounded public-input inspection and capture commands pass from a clean checkout and every attributable process exits cleanly.

## Decisions

- Destination: main. This is the established Ivy project integration destination; ticket-helper operations and reviewed deliveries use this ref.
- GOAL.md and reference/manifest.json define completion. Superseded Godot-era tickets are historical, not the active implementation queue. Extend footage slices in the existing Rust authority and shared renderer.
- Preserve clean-room boundaries: no proprietary asset/source/audio bytes enter product assets. Observable behavior and directly established short names may be reproduced.
- Choose reversible implementation decisions under Adam's delegation and record meaningful choices with alternatives in docs/decisions.md. Do not spend money, handle secrets, deploy, open pull requests, change other branches' history, perform destructive actions or physical actions. Ordinary reviewed delivery and pushing the run destination are authorized by the invoked workflow.
- Reuse the prepared ignored Cargo target with at most two jobs, serialize Cargo builds across workers, and place all visible project windows on monitor 4 before showing them.
- Do not claim Steam, prediction, interpolation or reconciliation unless implemented and behaviorally verified. Missing source evidence remains a named gap rather than an invented fidelity claim.

## Evidence required

- A fresh independent context checks every coverage-ledger product gap and both full-recording online runs at the final destination tip, with frame-addressable source identities, authoritative inputs/state and shared-renderer captures.
- Clean-checkout format, strict all-target Clippy, locked workspace build and tests, two-client/client-host agreement, bounded inspection and deterministic capture pass with recorded commands and no skipped required checks.
- Supported local keyboard/controller paths and online play traverse drafts, fights, half/round results, completed match and the source-observed reset/rematch paths. Monitor-4 native evidence verifies placement and rendering.
- Server/client/capture processes terminate without residue and ticket/whitespace checks pass. Remaining unavailable evidence is reported honestly and prevents closure when required for completion.

## Chat excerpts

Adam — [source session](http://ivy.localhost/sessions/01a0fa96-a468-7332-92f6-c36c3908e4b9), 2026-10-01:

> Finish the game

## Work log

- 2026-10-02T03:13:11Z stage design start session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Shaping the autonomy goal against GOAL.md and both supplied recordings after read-only runtime and backlog assessment.
- 2026-10-02T03:13:11Z stage design end session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Recorded complete online-match scope, main destination, delegated choices and clean-room/resource/window constraints; both CLI family probes succeeded.
- 2026-10-02T03:15:40Z stage implement start session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Started delegated completion run on main; first child contracts 60 recover the measured lime arena and 61 repair CI executable paths; original unintegrated worktrees remain preserved.
- 2026-10-02T03:19:59Z stage research start session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Inspecting preserved weapon-reload and hanging candidates for the next source-backed completion tranche; 60 is admitted and queued while 61 owns the sole Cargo verification slot.
- 2026-10-02T03:22:45Z stage research end session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Preserved reload is genuinely incompatible with current timber damage/ballistic/control assumptions, not merely stale hashes. Other-family consultation is checking whether source-track AIR_CONTROL recommendation meets both top-speed and displacement evidence before choosing the next tranche.
- 2026-10-02T03:46:42Z stage verify start session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Checking delivered CI receipt and goal gaps; ticket61 is closed at a00aae032f682026865bd30a94b2f6c2ec3a71fb, ticket60 is performing exact-candidate capture verification, ticket52 is independently re-admitted under delegation and waits for60, and hosted renderer timeout owns idea63.
- 2026-10-02T03:50:32Z stage verify end session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Confirmed61 closed/published with guarded receipt and preserved root dirt;60 passed build/lint/tests and two-client lime agreement, and is renewing render evidence;52 is admitted but waits for60;63 now has a fresh-admission request for the reproduced hosted device-poll failure. Full recording-defined completion remains open.
