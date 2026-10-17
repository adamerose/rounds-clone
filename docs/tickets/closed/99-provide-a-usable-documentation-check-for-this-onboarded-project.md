---
status: closed
created: 2026-10-17T07:50:27Z
origin: system-detected
tags: [quarrel, documentation, workflow]
value: 4
risk: 1
sessions:
  - codex:01a148af-57cb-7751-96dc-e93972e0b69f
  - codex:01a1493b-3018-7fd0-8478-9bc19bc5c639
execution: unattended
parent: 75
depends-on: []
supersedes: []
split-from: []
---

# Provide a usable documentation check for this onboarded project

The installed check-all entry point aborts before documentation checking because this project does not contain the Ivy playbook/skills source tree. Both documentation deliveries in this run needed narrower truthful verification, so determine the supported project checker instead of repeating that admission detour.

## Outcome

Record the supported project-facing documentation checker and its actual coverage in docs/decisions.md, under a ticket99/goal75 heading. Use the existing installed tool; no local wrapper, new checker or Ivy repair is needed.

## Decisions

- Destination: main. Records-only project delivery; do not change source, tools, configuration, AGENTS.md, existing design records or external Ivy files.
- The installed playbook/skills/onboard/check-documents.mjs --path <project-or-worktree> is the documented project-facing entrypoint. Resolve its installed physical path from the current skill root, rather than requiring a project-local Playbook checkout.
- The source-repository aggregate checks/scripts/check-all.mjs loads repo/playbook/skills and repo/playbook/context/AGENTS.md unconditionally. Its ENOENT here reflects the wrong entrypoint, not a project checker defect.
- Project checker coverage is designRecordErrors: shared-theme/link/frame targets, line length, reachability and forbidden Markdown under docs/design-docs. It is not a complete game/source/ticket checker.
- Current project checker truthfully rejects the existing legacy docs/design-docs/postmortems.md Markdown-file error. Keep that error visible; do not convert/delete/suppress it in this ticket. Earlier five/two frame-link repairs are already delivered62/100.
- Do not publish an external report or create a framework/policy file. The decision record prevents repeating the observed wrong-entrypoint verification cost.

## Evidence required

- Verify the installed onboard skill documents this command and inspect its implementation/coverage. Run it against current project-worktree inputs and retain the exact residual legacy error/exit1.
- Use a disposable documentation fixture to demonstrate the supported checker rejects one relevant deliberate link/frame fault, with a clean fixture passing. Keep it outside tracked source; no permanent test framework required.
- Record the command, source-repository aggregate limitation, available coverage and existing failure accurately; no full-documentation or full-repository pass claim.
- Release-matched ticket validation and git diff --check pass. Fresh independent exact-candidate CLI review precedes integration/close; clean only the new owned record worktree. No Cargo/GUI required.
## Work log

- 2026-10-17T07:50:27Z Filed after tickets62 and94 independently observed the same absent-project-playbook failure and preserved truthful narrower evidence. No checker, suppression, project document or external repository was changed by this idea.
- 2026-10-17T09:37:26Z Read-only /root/shape_docs found the existing documented onboard/check-documents.mjs public entrypoint and reproduced its truthful residual error. Shaped records-only delivery, risk1; no upstream repair or product decision needed. Fresh admission pending.
- 2026-10-17T09:38:39Z Admitted after fresh independent /root/goal_evidence read source/documented command and approved exact bounded record contract risk1, no dependencies or operator decision. Supported fixture verification covers both pass and intentional fault rejection; current legacy error remains visible.
- 2026-10-17T09:42:06Z stage research end — Installed onboard API confirmed; worktree --path resolves the shared integration root. Current JSON retains exactly the legacy postmortems.md error, exit 1. Initial alias-based power path was absent; physical Playbook path acquired sleep and restart protection successfully.
- 2026-10-17T09:43:12Z stage verify end — Required scope from ticket99: installed skill/API inspected; project checker exit1 retains exactly legacy Markdown error; disposable clean fixture exit0 and missing _top fault exit1; release-matched ticket validation and diff check passed. No Cargo or GUI required. Power acquire remains active for sleep and restart. Direct main delivery per goal75.
- 2026-10-17T09:45:05Z stage review start — Fresh independent Codex CLI review of c6ac0d9f3b48a16f4e8b17909d366668172ac4a8..faf173533b4e070a58dfff82c00d3bd9a58e9b6c. Current preset selects gpt-6.1-sol high; approved usage fallback shows weekly42percent below90. Launcher requires an existing artifact parent outside candidate, so its receipt uses task-specific TEMP/ivy-ticket099-review-01a1493b-3018-7fd0-8478-9bc19bc5c639; candidate and fixtures remain in owned worktree. No reviewer launched in the two setup refusals.
- 2026-10-17T09:47:46Z stage integration start session codex:01a1493b-3018-7fd0-8478-9bc19bc5c639 — reviewed c6ac0d9f3b48a16f4e8b17909d366668172ac4a8..8171b3e17aa542fc459c4a82c41d539a898913f0 as 8f194fa0175034ed602ee4111f7551e28fff113d..0d450ae22040417f61a521dcc2426308f0508289
- 2026-10-17T09:48:32Z stage review end session codex:01a1493f-c668-71b1-9e09-d54fc35f812a — approved candidate c6ac0d9f3b48a16f4e8b17909d366668172ac4a8..8171b3e17aa542fc459c4a82c41d539a898913f0
- 2026-10-17T09:48:32Z stage integration end session codex:01a1493b-3018-7fd0-8478-9bc19bc5c639 — integrated 8171b3e17aa542fc459c4a82c41d539a898913f0 as cd6890c1cba1bd14c1a0e1ebe76f235ace344a19
