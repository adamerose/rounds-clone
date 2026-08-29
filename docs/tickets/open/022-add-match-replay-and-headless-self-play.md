---
format: 3
status: idea
created: 2026-08-29T02:40:49Z
origin: human-request
tags: ["verification", "replay", "self-play"]
value: 10
risk: 4
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: [18, 19]
supersedes: []
split-from: []
---

# Add match replay and headless self-play

The goal requires long-running deterministic validation, but the current protected replay covers only a duel and the shipped UI must not invent a bot mode absent from ROUNDS. After ticket 019 verifies a playable current card subset, add complete-match replay plus internal headless agents that exercise it without becoming player-visible content.

## Outcome

- A full match serializes seed, all semantic inputs, drafts, arenas, card ownership, hashes, and terminal result and replays byte-for-byte deterministically.
- Internal headless agents complete 10,000 matches within the round cap with no crash, assertion, nontermination, or unbounded entity growth and report duration, card, arena, and failure statistics.
- No bot choice, bot branding, or AI-only mechanic appears in the shipped game flow.

## Decisions

- Self-play is verification infrastructure, not a ROUNDS gameplay feature. Agents use only legal player inputs and receive no hidden state unavailable to a real controller unless the test is explicitly labeled structural.
- Match replay extends the existing deterministic event/history rules and records intentional format or golden changes explicitly.

## Evidence required

- Repeated complete-match recording/replay produces identical per-tick hashes, draft history, arena sequence, and terminal result; malformed or interrupted data fails closed.
- A retained 10,000-match report proves the required termination and safety bounds on the current faithful subset.
- Full replay/history, simulation, repository, ticket, and whitespace gates pass without adding a player-visible bot mode.

## Work log

- 2026-08-29T02:40:49Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Assigning complete-match replay and bot-driven validation as internal infrastructure rather than an invented shipped ROUNDS mode.
- 2026-08-29T02:40:49Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Bound complete deterministic history and the 10,000-match safety gate to legal semantic inputs and no player-facing AI.
