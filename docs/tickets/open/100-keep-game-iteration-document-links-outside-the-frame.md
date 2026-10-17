---
status: idea
created: 2026-10-17T08:27:06Z
origin: system-detected
tags: [quarrel, documentation, navigation]
value: 3
risk: 1
sessions:
  - codex:01a148af-57cb-7751-96dc-e93972e0b69f
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