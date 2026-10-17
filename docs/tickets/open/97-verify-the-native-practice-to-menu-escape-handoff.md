---
status: ready
owner: codex:01a14919-7a30-74e1-b46f-fc14f5f5682a
created: 2026-10-17T07:14:14Z
origin: system-detected
tags: [quarrel, practice, verification]
value: 6
risk: 2
sessions:
  - codex:01a148af-57cb-7751-96dc-e93972e0b69f
  - codex:01a14919-7a30-74e1-b46f-fc14f5f5682a
execution: unattended
parent: 75
depends-on: [96]
supersedes: []
split-from: []
---

# Verify the native practice-to-menu Escape handoff

Practice is the default entry point and Escape is the route from it to the match menu. The October14 playtest record explicitly leaves that desktop transition unverified, despite passing event-level lifecycle tests.

## Outcome

- Establish whether Escape from visible native practice starts exactly one working menu child window/process and releases the practice window and native event loop, with the original practice process waiting only for that child's exit.
- Correct only a reproduced defect in that existing handoff, preserving the current practice and menu flows. If the route works, deliver the bounded evidence record without a product change.

## Decisions

- Destination: main. This supplements goal75 and the delivered ticket96; it does not require a two-home Steam session or a human preference verdict.
- Start after the scheduler releases the sole native Cargo slot. Prefer an existing current-source executable only after independently proving its attribution and content identity. Otherwise use the normal complete verification with shared out/cargo-target and jobs<=2; do not create a clean target or alter linker/profiles/debuginfo.
- Every visible window must be created unfocused on monitor4, or hidden/minimized then moved and verified before showing. Verify its center before native evidence; never focus/cover monitors1-3. Close or move a misplaced exact verified project window immediately and record the departure.
- Use supported scoped input to the exact verified project window. Do not send global keyboard input or work around withheld desktop-control capabilities. If a supported scoped route is unavailable, record that evidence limitation and block this ticket; an event-only/headless check cannot claim native Escape proof.
- Do not change game design, add menu controls, or change the Winit process boundary without a demonstrated failure. Retain logs/window images under ignored out/ and a concise result in the existing October14 playtest record.

## Evidence required

- Retain source/executable/content identity, invocation, practice/menu PID identities, process lineage, window identities/placement before and after Escape, and a successful visible menu render.
- Show exactly one menu child/window and no retained practice window or native event loop after the transition. Record that the original practice process may remain only as the menu child's waiting parent; after closing the exact owned menu, verify both child and parent exit successfully. Do not touch other processes.
- Run the existing headless practice close/menu-route regressions. If code changes, preserve a smallest reliable boundary regression and complete the normal verification route; if only evidence changes, the release-matched Node documentation checker and git diff --check pass.
- A fresh non-author context reviews the exact evidence/code candidate. State unavailable evidence explicitly and never call native desktop proof passed from event-level tests alone.

## Work log

- 2026-10-17T07:14:14Z Bounded reflection found the explicitly unverified native Escape handoff in docs/playtests/2026-10-14.md; idea recorded without claiming a defect or changing product behavior.
- 2026-10-17T07:16:38Z Admitted after fresh context /root/shape_docs identified and corrected the mistaken parent-process exit expectation, then approved corrected wording risk2. Current Command::status intentionally retains the waiting parent; the evidence checks window/event-loop release and both exits after closing the child. depends-on96 closed; no operator decision open.
