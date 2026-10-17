---
format: 3
status: ready
owner: codex:01a148b4-9e0c-72d1-ab77-8787238ff7df
created: 2026-10-02T03:17:15Z
origin: system-detected
tags: ["rounds", "documentation", "onboarding"]
value: 3
risk: 1
sessions:
  - codex:01a0fa96-a468-7332-92f6-c36c3908e4b9
  - codex:01a148b4-9e0c-72d1-ab77-8787238ff7df
execution: unattended
parent: 59
depends-on: []
supersedes: []
split-from: []
---

# Keep project-document links outside the design-record frame

The checked-in documentation index already links the current project records. Make its existing project-document links open outside the design-record frame so navigation follows the supported documentation layout.

## Outcome

- `docs/design-docs/index.html` remains the entry page for current project records. Its links to `game-iteration.html` and `arena-data.html` stay in the design-record frame; its links to `../roadmap.md`, `../game-design.md`, `../playtests/README.md`, `../decisions.md`, and `../postmortems.md` open in the top frame.

## Decisions

- Destination: main. Work only in `docs/design-docs/index.html`; add `target="_top"` to its existing five outside-design-doc links. Do not change the link set, title, theme, or linked documents.
- The checked-in origin/main index and theme are the baseline. The root checkout's untracked earlier onboarding scaffold (index.html, theme.css, docs/postmortems.md, ivy.yaml) is provenance only and must not be copied, deleted, or committed.
- Preserve the root checkout's human modifications to AGENTS.md and docs/design-docs/postmortems.md; they are outside the candidate. Do not inventory, convert, or add links for every ticket, fidelity item, research note, legacy, recovery, or architecture document.
- No Cargo, native test, build or GUI work is needed. Ticket95 holds the sole native Cargo slot.

## Evidence required

- On the candidate worktree, `node C:\Users\Adam\.ivy\source-checkouts\ivy-main-repair-20261014-ivy-open\playbook\checks\scripts\check-all.mjs .` passes. This is the release-matched Node-only ticket/documentation checker.
- `git diff --check` passes.
- Review confirms the two internal HTML links lack `_top`, the five existing `../` project-document links have `_top`, and each resolves in the candidate.

## Work log

- 2026-10-02T03:17:15Z stage research start session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Inventoried documentation after the release-matched onboarding helper restored missing scaffold files.
- 2026-10-02T03:17:15Z stage research end session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Retained the complete path inventory as an idea ticket and deferred conversions while source-backed product delivery proceeds.
- 2026-10-17T07:12:19Z Current origin/main already contains the useful index; stale inventory/defer premise removed. Fresh context /root/shape_docs approved this exact narrow contract risk1, depends-on empty, no operator decision open. Admission authorizes only five target attributes, no implementation bytes.
