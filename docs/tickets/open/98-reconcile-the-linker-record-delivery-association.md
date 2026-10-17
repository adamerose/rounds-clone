---
status: idea
created: 2026-10-17T07:42:48Z
origin: system-detected
tags: [quarrel, workflow, provenance]
value: 3
risk: 1
sessions:
  - codex:01a148af-57cb-7751-96dc-e93972e0b69f
execution: unattended
parent: 75
depends-on: [94, 99, 100, 102]
supersedes: []
split-from: []
---

The ticket helper refuses to associate three reviewed deliveries with the autonomy goal after their integration ranges were rebased around legacy metadata. Record the exact producer mismatch and a concrete upstream proposal without weakening proof or changing history.

## Outcome

Append a concise ticket98/goal75 decision record to docs/decisions.md identifying the delivery-association protocol mismatch for94/100/102, its evidence and a concrete proposed Ivy correction. Canonical goal association remains unavailable; this records the cause, not an implemented repair.

## Decisions

- Destination: main. Records-only project work, serialized after99 because both append docs/decisions.md. No source, helper, config, immutable source/receipt, historical ticket, or external Ivy change.
- integration-start records the then-intended integrated range. close permits a later equivalent rebase after legacy-main metadata advances and writes the final integrated range in Delivery-Integrated-As. associateDelivery then reads the close-receipt snapshot and requires integration-start to match that final range exactly.
- A later append cannot repair that immutable receipt snapshot. There is no supported repository-side correction. Preserve original ranges/refusals; do not retry association, fabricate proof, force-push or rewrite history.
- The proposed upstream remedy treats the final validated receipt range as canonical only when the earlier start has the same reviewed range and equivalent rebased delivery is independently proven. Retain both intended and final ranges. This is a proposal requiring separate Ivy ownership/authorization, not an accepted security/protocol change or permission to publish externally.
- Preserve old94 experiment/ref/proof/cache and unfinished103 worktree/checkpoint/evidence. No Cargo or GUI.
- Earlier resolver49/59 authority leads are separate historical questions, already retained in goal75's log and their own tickets. Do not assert they share this cause or change them here.

## Evidence required

- Inspect the release-matched checks/src/ticket-store.mjs integrationStart/closeTicket/associateDelivery and locate the exact rejection boundary. Helper-read94/100/102 and their validated receipts establish both original intent and final ranges.
- Independently verify reviewed/integrated range equivalence and origin/main ancestry for delivered tips94=75807e7cc8283cd17b6f4b335ec44816c01efd84,100=113dda4db4c9cbd2386b04baae4478a733038503,102=f267a9489cb44d4c689eb8136bd7b61c77504c6c. Preserve exact one-shot refusal and source/receipt identities in the record or linked helper records.
- Identify the upstream owner and concrete validation conditions for a future correction; state no delivery proof was lost and canonical association remains unavailable. No external publication or implementation claimed.
- Run the supported onboard/check-documents.mjs against the worktree, retaining the known legacy Markdown error instead of a full pass; ticket validation and git diff --check pass. Fresh nonauthor exact-candidate CLI review, main integration/close and only owned worktree cleanup.

## Work log

- 2026-10-17T07:42:48Z Original idea retained the one-shot94 association refusal after its reviewed journal delivery reached origin/main; no bypass or source/receipt mutation.
- 2026-10-17T09:38:45Z Read-only /root/admit_replay traced same mechanism through94/100/102 and immutable receipt validation. Folded same-mechanism Scratch facts into contract/evidence, separated unrelated49/59 leads without claiming their resolution. Records-only proposal now depends on99 to serialize the shared journal. Fresh admission pending.
