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

- The installed ticket-tools.ps1 check . --ticket-ref passes. Run the installed designRecordErrors function on the candidate and its unchanged implementation base: exactly the five index.html outside-frame errors disappear, with no new errors. Its three existing errors (legacy postmortems.md and two game-iteration.html targets) remain outside this five-attribute contract. The check-all.mjs entry point is unavailable for onboarded projects because documentationErrors unconditionally loads playbook/skills.
- `git diff --check` passes.
- Review confirms the two internal HTML links lack `_top`, the five existing `../` project-document links have `_top`, and each resolves in the candidate.

## Work log

- 2026-10-02T03:17:15Z stage research start session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Inventoried documentation after the release-matched onboarding helper restored missing scaffold files.
- 2026-10-02T03:17:15Z stage research end session codex:01a0fa96-a468-7332-92f6-c36c3908e4b9 — Retained the complete path inventory as an idea ticket and deferred conversions while source-backed product delivery proceeds.
- 2026-10-17T07:12:19Z Current origin/main already contains the useful index; stale inventory/defer premise removed. Fresh context /root/shape_docs approved this exact narrow contract risk1, depends-on empty, no operator decision open. Admission authorizes only five target attributes, no implementation bytes.
- 2026-10-17T07:13:53Z stage implement start — Detached origin/main worktree; direct delivery to main per run75. Sleep and restart protection active after resolving installed skill junction to physical Playbook root.
- 2026-10-17T07:15:47Z stage verify end — Exact candidate7af8ad99fa9445626ed46a10c1c0f586954322a1 changes only five target attributes. Ticket checker, seven path/target assertions and diff check pass. Installed designRecordErrors removes exactly five index errors versus base af73200493863a504fc3ba5605b6da9eeb124ba1; three unrelated baseline errors remain. check-all fails at documentation.mjs:135 loading absent playbook/skills; corrected evidence awaits fresh admission. No GUI/Cargo or linked document changes.

- 2026-10-17T07:18:28Z Re-admitted corrected Node evidence at risk1 by independent reviewer codex:01a148b7-e962-7c82-a12c-3c2362753988; no human decision, depends-on empty. Same reviewer approved exact implementation af73200493863a504fc3ba5605b6da9eeb124ba1..7af8ad99fa9445626ed46a10c1c0f586954322a1 with no findings. Both admission and candidate independently checked; no source changes requested. Review receipt: C:\Users\Adam\AppData\Local\Temp\ticket62-01a148b4-9e0c-72d1-ab77-8787238ff7df/codex-review-af7320049386-7af8ad99fa94-949fdaf9d02e.
- 2026-10-17T07:19:07Z stage integration start session codex:01a148b4-9e0c-72d1-ab77-8787238ff7df — reviewed af73200493863a504fc3ba5605b6da9eeb124ba1..872d0d0d0ee2f3de4570f06505d3ff88f21f407e as 5dafe596178bedb08cb800c7ab47cace844f9399..b493acb3fe4b30e5405761d1f71098c5dd7ce71e
- 2026-10-17T07:20:16Z stage integration start session codex:01a148b4-9e0c-72d1-ab77-8787238ff7df — reviewed af73200493863a504fc3ba5605b6da9eeb124ba1..872d0d0d0ee2f3de4570f06505d3ff88f21f407e as 40a2493b98dfa6f85e3f74f9eba5d34275b8ac5b..fff8358db4065a8559d8b3d1ab256d1a5a8bd16a
