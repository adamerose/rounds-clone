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