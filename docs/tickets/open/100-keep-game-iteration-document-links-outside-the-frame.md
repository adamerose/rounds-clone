---
status: ready
owner: codex:01a148fb-3fe1-7eb2-b374-bf714f99b8fd
created: 2026-10-17T08:27:06Z
origin: system-detected
tags: [quarrel, documentation, navigation]
value: 3
risk: 1
sessions:
  - codex:01a148af-57cb-7751-96dc-e93972e0b69f
  - codex:01a148fb-3fe1-7eb2-b374-bf714f99b8fd
execution: unattended
parent: 75
depends-on: []
supersedes: []
split-from: []
---

# Keep game-iteration document links outside the frame

Two links in the Game iteration design record open project Markdown documents inside the design-record frame. Give those existing links the supported top-frame navigation without changing the linked content.

## Outcome

- The links to `../roadmap.md` (The roadmap) and `../playtests/2026-10-14.md` (first iteration record) open their existing project documents in the top frame. Destinations, labels, surrounding prose and every other link stay unchanged.

## Decisions

- Destination: main. Modify only `docs/design-docs/game-iteration.html`, adding `target="_top"` to those two exact anchors. Keep its `index.html` link in-frame.
- Leave the unrelated legacy `docs/design-docs/postmortems.md` Markdown-file checker error unchanged. Do not convert/remove it, alter hierarchy/theme, suppress checker errors or change Cargo/UI/build settings.
- No Cargo, GUI, native process or broad checker workaround. Use a distinct detached worktree from current origin/main; preserve all root human/untracked onboarding edits and existing worktrees/proof/cache. Worker95 retains the sole native slot.

## Evidence required

- Run the installed release-matched `designRecordErrors` against the unchanged implementation base and candidate: exactly the two named top-frame errors disappear with no new errors; the pre-existing Markdown error remains. State that residual explicitly, not as a full documentation pass.
- Assert those exact two hrefs retain their paths and labels and have `_top`, while `index.html` lacks `_top`; confirm both destination files exist.
- Run the release-matched ticket checker and `git diff --check`.
- A fresh non-author exact-candidate review confirms only two attributes changed. Integrate/push main, close through the ticket helper and clean only the new owned worktree.

## Work log

- 2026-10-17T08:27:06Z Fresh read-only context /root/shape_docs confirmed the exact two current origin/main defects and proposed this risk1 scope. New idea recorded for a separate admission check; no implementation bytes approved.
- 2026-10-17T08:29:11Z Admitted as written by independent context /root/admit_replay at risk1: complete, bounded, checkable, depends-on empty, no human decision. /root/shape_docs independently confirmed both source anchors. Approval covers contract only, no implementation bytes.
- 2026-10-17T08:31:07Z stage implement start — owned detached worktree; Windows sleep and restart protection active after resolving installed skill junction. Direct delivery to main per contract.
- 2026-10-17T08:32:03Z stage verify end — exact-byte/path/label/index assertions and diff check passed; installed designRecordErrors base 3 to candidate 1, exactly two top-frame errors removed, legacy postmortems.md error unchanged. Ticket checker passed. No broad checker, Cargo or GUI run; required scope is contract evidence.
- 2026-10-17T08:33:00Z stage review start — fresh Codex CLI exact-candidate review; current preset Sol6.1 high, normal budget endpoint eligible at 33percent weekly below90. Candidate f5c3350; only two navigation attributes; root status unchanged.
