---
format: 3
status: idea
created: 2026-08-29T02:40:49Z
origin: human-request
tags: ["verification", "rendering", "observability"]
value: 8
risk: 3
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: [20, 22]
supersedes: []
split-from: []
---

# Restore nightly replay reel evidence

The project goal says the rendered nightly reel is how unattended regressions become visible, but no current ticket owns a real-match reel after the recovery reset. Render a complete deterministic match through the faithful presentation and publish a bounded nightly artifact plus machine-readable diagnostics.

## Outcome

- A nightly job selects a completed self-play match, verifies its full replay, renders the same deterministic state through the game presentation, and retains a playable reel with seed, hashes, build, cards, arenas, and terminal result.
- Failure artifacts identify the exact stage and retain enough evidence to reproduce without treating a green render as proof of gameplay fidelity.
- The daily progress summary links the current reel and states which fidelity tickets remain incomplete.

## Decisions

- The reel is observability infrastructure. Its content must be a legal match from the faithful subset and may not add presentation or mechanics visible only in rendering.
- Keep raw ROUNDS reference media external; the reel contains only independently generated clone output.

## Evidence required

- The nightly path passes from match replay through frame render and encoded video twice with identical semantic frame/hash metadata.
- Interrupted replay, render failure, encoder failure, and artifact-retention tests fail visibly and preserve bounded diagnostics.
- Repository, replay/history, Godot render, ticket, and whitespace gates pass; the produced reel is inspected as a person-visible artifact.

## Work log

- 2026-08-29T02:40:49Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Assigning the missing nightly reel and diagnostics required by the founding goal.
- 2026-08-29T02:40:49Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Bound the reel to verified complete-match replay, faithful clone rendering, reproducible metadata, and visible failure retention.
