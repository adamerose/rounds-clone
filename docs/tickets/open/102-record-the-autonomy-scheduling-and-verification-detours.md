---
status: idea
created: 2026-10-17T09:10:57Z
origin: system-detected
tags: [quarrel, autonomy, evidence]
value: 3
risk: 1
sessions:
  - codex:01a148af-57cb-7751-96dc-e93972e0b69f
execution: unattended
parent: 75
depends-on: [94, 95, 100]
supersedes: []
split-from: []
---

# Record the autonomy scheduling and verification detours

The current autonomy run repeated expensive verification after two workers appended to the same decision journal. Record what happened and what restored trustworthy evidence so the next run can avoid the same cost.

## Outcome

Append concise factual entries to docs/postmortems.md for the significant departures in the October17 run, including the scheduler's shared-file scheduling mistake and the related verification detours. Preserve existing journal entries and distinguish known causes from unresolved evidence limitations.

## Decisions

- Destination: main. Journal-only work; no source, protocol, test, linker, process-rule or historical ticket edits.
- Start only after worker101 and all other native/shared-record writers exit. The scheduler holds this ticket until then, even though its source dependencies are closed.
- The scheduler knowingly overlapped94 and95, both of whose prompts required docs/decisions.md. The append conflict required95 to rebase, repeat full verification and receive fresh review. The existing autonomy rule already required serialization of shared files; this was failure to follow it, not a missing rule.
- Distinguish the recorded CRLF-to-LF source-byte attribution change from a product change. It forced affected rebuild/verification; its originating writer is not established. Do not invent a cause or new policy.
- Record the two refused goal-delivery associations94/100 and the installed documentation checker limitation briefly, linking their open followups98/99. These were reported without retry bypass or a false full-pass claim. Do not implement the upstream fixes here.
- Retain the separate101 welcome-timeout outcome only as accurately supported by that ticket's final record; the95 failure itself was one184-pass/1-fail attempt followed by185-pass complete verification. Do not call six passing repeats a repair.
- Preserve the dirty root onboarding files, old94 worktree/ref/proof, shared Cargo target, and retained95 evidence. No Cargo or GUI required.

## Evidence required

- Compare the new text against helper-read94/95/100/101 records and retained tool-check evidence; identify the true scheduling cause, consequence, recovery and unresolved limits.
- Existing entries remain unchanged; links use the verified repository id1302a5de74680c84 and session-native ids. No secrets, provider transcripts or invented failure explanation.
- Run the applicable release-matched documentation/ticket checks and git diff --check; state the known check-all limitation accurately rather than claiming it passed.
- Fresh non-author CLI review of exact journal-only commits, followed by integration, ticket close and cleanup of only this worker's worktree.

## Work log

- 2026-10-17T09:10:57Z Captured the scheduler's acknowledged shared-record overlap and evidence detours; the corrective scheduling is already active. Queue behind101 to avoid repeating it.