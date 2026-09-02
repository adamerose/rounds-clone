---
format: 3
status: idea
created: 2026-09-02T02:12:00Z
origin: system-detected
tags: ["project-maintenance", "recovery", "git", "workflow"]
value: 7
risk: 4
sessions:
  - codex:01a05fda-d5ec-7322-896d-b22b6c7499a0
execution: unattended
depends-on: [35]
supersedes: []
split-from: []
---

# Reconcile ticket 035 delivery residue

Ticket 035 has accumulated many registered delivery worktrees and local branches while landing reviewed slices incrementally, including numerous heads already contained by `main` and at least one later head that is not.
After ticket 035 closes, preserve any unique evidence and remove only proven-redundant residue so future worktree-wide checks, ticket allocation, and recovery decisions operate on a small and unambiguous project state.

## Outcome

- A deterministic inventory classifies every ticket-035 worktree and local branch by exact head, tracked and ignored dirt, relationship to final ticket-035 delivery, unique commits, review status, and recovery value.
- Every non-integrated or otherwise unique artifact receives an explicit preserve, recover, or abandon disposition before any registration, branch, or directory is removed.
- Exact clean ticket-035 worktrees and local branches proven redundant with the final integrated history are removed through the supported guarded cleanup path, leaving no unexplained ticket-035 residue.

## Decisions

- Wait for ticket 035 to close so an apparently redundant worktree cannot still be active delivery evidence or the intended continuation base.
- Reuse ticket 013's preservation-first inventory method, but do not mutate or reclassify its frozen ticket-009 through ticket-011 artifacts.
- Treat registered worktrees, branch refs, ignored generated output, active claims, run-ledger references, and unique commits as separate ownership facts.
Containment by `main` alone does not authorize directory or ref deletion.
- Do not modify product files or rewrite history while reconciling residue.

## Evidence required

- A pre-cleanup manifest accounts for every ticket-035 worktree, branch, claim, dirty path, unique commit, review marker, and run-ledger reference with stable hashes and an explicit disposition.
- Guarded dry runs prove each removal target is exact, clean or deliberately stripped only of enumerated reproducible output, redundant with integrated history, and unrelated to the integration root or ticket-013 recovery artifacts.
- A post-cleanup inventory proves every approved removal absent, every preserved artifact byte and ref unchanged, final `main` unchanged, the ticket checker green, and `git diff --check` clean.

## Work log

- 2026-09-02T02:12:00Z stage design start session codex:01a05fda-d5ec-7322-896d-b22b6c7499a0 — Filed preservation-first residue reconciliation after read-only inventory found 29 registered worktrees, 24 local branches, 25 worktree heads already contained by current main, and a non-integrated ticket-035 continuation head.
- 2026-09-02T02:23:52Z stage correction start session codex:01a05fda-d5ec-7322-896d-b22b6c7499a0 — Raising deletion and provenance risk to 4 while preserving the explicit wait for ticket 035 to close and every preservation-first ownership boundary.
- 2026-09-02T02:25:13Z stage correction end session codex:01a05fda-d5ec-7322-896d-b22b6c7499a0 — Set risk to 4 without changing idea status, dependency 35, cleanup scope, or preservation evidence requirements.
- 2026-09-02T02:40:59.115Z stage review start session codex:01a05fda-d5ec-7322-896d-b22b6c7499a0/01a05ffd-90e6-73d3-b53d-3c337ceb13db — Fresh exact review started for records-only reflection range `bd0c8a2..b00a97c` while tickets 036 and 037 remained ideas and ticket 037 waited on ticket 035.
- 2026-09-02T02:47:41.889Z stage review end session codex:01a05fda-d5ec-7322-896d-b22b6c7499a0/01a05ffd-90e6-73d3-b53d-3c337ceb13db — Fresh exact review approved records-only reflection range `bd0c8a2..b00a97c` with no findings while tickets 036 and 037 remained ideas and ticket 037 waited on ticket 035.
