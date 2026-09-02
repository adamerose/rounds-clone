---
format: 3
status: idea
created: 2026-09-02T02:09:33Z
origin: system-detected
tags: ["project-maintenance", "observability", "workflow"]
value: 6
risk: 2
sessions:
  - codex:01a05fda-d5ec-7322-896d-b22b6c7499a0
execution: unattended
depends-on: []
supersedes: []
split-from: []
---

# Keep project failure postmortems current

The binding project goal requires every failure, stall, broken tool, or surprising friction to reach `docs/design-docs/postmortems.md`, but that ledger has no record after the early ticket-034 work while later ticket and run records contain many review rejections, flaky-check investigations, and blocked delivery slices.
Restore a concise project-level record of those causes and make future omissions visible so a new maintainer can learn what repeatedly costs time without reconstructing a large provider run ledger or a single ticket's implementation history.

## Outcome

- A committed `docs/recovery/postmortem-gap-audit-2026-08-30.md` enumerates every candidate event in the frozen catch-up source universe and maps each one to either an appended postmortem entry or an explicit evidence-backed disposition explaining why it is not a qualifying failure, stall, broken tool, or surprising friction.
- The postmortem ledger contains a same-session entry for every qualifying event created after this ticket is delivered. Recurring events may point to one concise shared cause summary, but each event remains individually traceable and no qualifying one-off is silently excluded.
- Ticket work logs and Ivy run records remain the detailed provenance, while each postmortem entry states the durable lesson, affected workflow, and verified resolution or continuing exposure.
- A bounded integration-time audit catches a missing, late, or undisposed candidate before delivery without replacing the creator's same-session recording obligation.

## Decisions

- Preserve the postmortem ledger's append-only history and do not rewrite its existing entries.
- Freeze the historical catch-up cutoff at current `main` commit `bd0c8a2d15648e6970de5404e9a4b9fb2a9915aa`. Its exact source universe is the 80 first-parent commits after postmortem commit `ced986cad76ebf9dd95220b6f5900e302a1f8c78`; ticket-034 blob `c0e073cfe36994622e6330447e870a81e3feb100`; ticket-035 blob `d2f82457171afca81eae899340036e8c062032a4`; and `.ivy/runs/2026-08-28-autonomous-rounds-clone/run.jsonl` at 244,566 bytes with SHA-256 `458790ad866ba744a49f2881e4e5fb7a34a2447052d8430973ad8dcbcdfd596c`, ending at `2026-08-30T17:09:42.5908925Z`.
- Activity after that frozen cutoff follows the restored same-session rule and does not expand or evade the catch-up audit.
- Treat every rejected, failed, flaky, broken, stalled, or surprising event as a candidate. A normal partial-delivery `blocked` receipt may receive an explicit nonqualifying disposition, but it cannot disappear through a category-wide exclusion.
- A same-session disposition records the exact candidate, reason, source, timestamp, and creating session in the owning ticket work log or another committed auditable record. Integration-time discovery of an undisposed candidate is a failure of the same-session process, not permission to defer ordinary recording until integration.
- Recurring candidates may share one postmortem cause section only when the gap audit or same-session disposition maps every individual event to that section.
- Keep ticket 024 as the owner of nightly reels and daily progress summaries. This ticket owns only the missing failure-learning path.

## Evidence required

- Regeneration of `docs/recovery/postmortem-gap-audit-2026-08-30.md` from the frozen source universe produces the same complete candidate list and proves every candidate has exactly one qualifying-entry or explicit-nonqualifying disposition.
- Negative fixtures prove the future gate rejects an unrecorded qualifying one-off, a category-wide exclusion, a disposition without exact source/session/time, a recurring cause that omits one occurrence, and a postmortem entry first created outside the owning session.
- Positive fixtures prove multiple recurring candidates may map to one concise cause summary and that an exact same-session nonqualifying disposition passes while remaining visible to integration audit.
- Repository checks, the ticket checker, and `git diff --check` pass with no product, simulation, replay, specification, dependency, or GUI change.

## Work log

- 2026-09-02T02:09:33Z stage design start session codex:01a05fda-d5ec-7322-896d-b22b6c7499a0 — Filed the missing project-level failure-learning path after current-main evidence showed the append-only postmortem ledger stopped while later durable run records accumulated repeated review and verification failures.
- 2026-09-02T02:23:52Z stage correction start session codex:01a05fda-d5ec-7322-896d-b22b6c7499a0 — Correcting the contract to preserve GOAL.md's same-session rule, freeze the catch-up universe, enumerate every candidate, and make integration audit an additional backstop rather than delayed recording authority.
- 2026-09-02T02:25:13Z stage correction end session codex:01a05fda-d5ec-7322-896d-b22b6c7499a0 — Froze the exact historical commits, ticket blobs, and hashed run ledger; required an auditable disposition for every candidate; restored same-session entries for all qualifying future events; and limited integration audit to detecting violations.
