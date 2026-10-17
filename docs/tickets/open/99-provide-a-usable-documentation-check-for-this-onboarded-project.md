---
status: idea
created: 2026-10-17T07:50:27Z
origin: system-detected
tags: [quarrel, documentation, workflow]
value: 4
risk: 2
sessions:
  - codex:01a148af-57cb-7751-96dc-e93972e0b69f
execution: unattended
parent: 75
depends-on: []
supersedes: []
split-from: []
---

# Provide a usable documentation check for this onboarded project

The installed check-all entry point aborts before documentation checking because this project does not contain the Ivy playbook/skills source tree. Both documentation deliveries in this run needed narrower truthful verification, so determine the supported project checker instead of repeating that admission detour.

## Outcome

- Identify an installed documentation-check entry point that works in a normal onboarded repository without copying the Ivy source tree into it, or prepare a concrete upstream repair proposal if none exists.
- State precisely which checks are available and which remain unavailable. A failed checker must never be treated as a documentation pass, and unrelated baseline document errors must stay visible.

## Decisions

- Destination for any future project delivery: main. This is an unlaunched idea, not authorization to modify Ivy outside this repository or to publish an external report.
- check-all.mjs in the release-matched installed Playbook reaches documentation.mjs:135, which tries to load repo/playbook/skills and reports ENOENT here. Tickets62 and94 retain the concrete base/candidate outcomes and their independently admitted evidence substitutions.
- Do not add an in-repository Playbook clone, blanket suppression, success fallback or broad document conversion merely to make the checker green. Keep this separate from the known five-link repair and the three pre-existing design-record errors.

## Evidence required

- Reproduce with a normal onboarded-project fixture and the installed release-matched checker; identify the failing owning line before proposing a correction.
- An available replacement must perform its documented checks on a project fixture and reject a relevant deliberate documentation fault. If the fix belongs to Ivy, retain the concrete report locally and obtain separate authority before external publication or implementation.
- Record commands, exit outcomes and actual coverage without claiming a full pass from unavailable checks.

## Work log

- 2026-10-17T07:50:27Z Filed after tickets62 and94 independently observed the same absent-project-playbook failure and preserved truthful narrower evidence. No checker, suppression, project document or external repository was changed by this idea.