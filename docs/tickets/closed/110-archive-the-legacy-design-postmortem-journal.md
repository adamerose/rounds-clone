---
status: closed
created: 2026-10-17T19:18:36Z
origin: system-detected
tags: [quarrel, documentation, archive]
value: 3
risk: 1
sessions:
  - codex:01a148af-57cb-7751-96dc-e93972e0b69f
  - codex:01a14b51-ef77-71c0-8c23-f14fc1d38437
execution: unattended
parent: 105
depends-on: []
supersedes: []
split-from: []
---

# Archive the legacy design postmortem journal

An older Markdown journal remains in the HTML design-record directory and causes the remaining design checker error. Preserve its unique history in an accessible archive while leaving the current postmortem journal intact.

## Outcome

Move docs/design-docs/postmortems.md unchanged to docs/legacy/postmortems.md and add an Archived postmortems link beside the current journal link in docs/design-docs/index.html. Preserve every original byte and all173 historical headings; the canonical docs/postmortems.md remains unchanged by this ticket.

## Decisions

- Destination: main. Exact allowed candidate paths are old journal removal, new archive file and one index link. No canonical docs/postmortems.md, docs/decisions.md, source, tool, config, theme or guidance edits.
- Source baseline inspected at origin/mainf6268a1c3ac70076b8297f1d5c6189099c700701: legacy138178bytes/173entries, canonical79756bytes/41entries. Legacy contains unique August history; it is not a redundant duplicate to delete.
- Add relative href ../legacy/postmortems.md with target=_top and label Archived postmortems beside the canonical ../postmortems.md link. Preserve canonical link behavior and current HTML formatting rules.
- Archive path has the same depth as the original, preserving parent-relative references. Inspect outgoing relative links before moving; if the move would newly break a formerly resolvable same-directory link, stop for a bounded contract amendment instead of rewriting historical prose or claiming preservation.
- Native107 currently owns canonical journals/Cargo. This exact-path records-only job can run independently because it edits neither those journals nor native source and runs no Cargo/GUI. Routine facts stay in its ticket log; a new consequential judgment requiring a shared journal is outside this narrow contract and must wait for serialization.
- The installed onboarding checker --path resolves the shared stale integration root, not candidate worktrees. Use the release-matched exported designRecordErrors(candidateWorktree) for direct candidate evidence; report the root-only command limitation honestly, never claim it validated candidate bytes.
- Root is stale and human dirty/untracked, including local legacy journal edits; do not edit, delete, sync or copy those root files. Use only current fetched origin/main Git blobs in an isolated worktree, preserving root hashes/status. All old worktrees/proof/cache remain intact.

## Evidence required

- New archive Git blob equals baseline legacy Git blob exactly, with all173 historical headings retained. Canonical journal blob unchanged by the candidate range. No source/code changes.
- Current canonical link remains correct, new archive link reaches exact retained file with _top, and no newly broken previously valid outgoing relative archive links.
- Direct candidate designRecordErrors no longer reports Markdown within docs/design-docs and passes remaining site checks (or reports a genuinely unrelated baseline error precisely). Supported root-only command result is distinguished.
- Ticket validation and git diff --check pass. Fresh nonauthor exact-candidate CLI review before main integration/close, then cleanup only owned worktree. No native build, UI preview or broad documentation rewrite is required.
- If shared journal edits, new source decisions or historical content changes become necessary, preserve checkpoint and request scheduler re-admission rather than widening this contract.

## Work log

- 2026-10-17T19:18:36Z Read-only admit_replay feasibility pass found unique historical content and safe same-depth archive path; no existing remote incoming links to the old path. Records-only exact scope avoids the active native worker's canonical journal.
- 2026-10-17T19:22:07Z Independent current_sim_reflect admission check verified exact current Git blobs; corrected initial byte-count readings to138178/79756, preserving legacy blob2ab2c344ae1f8251ebb55a10b1843a7a8583fe8f. No scope or historical artifact changed. All other contract points judged complete at9c6193c3cb266a8fc35ba7971a02da9db4ebe9f0.
- 2026-10-17T19:23:20Z Admitted risk1 after independent current_sim_reflect final approval of corrected0064da65 contract; complete/checkable, no operator decision, no dependencies because exact allowed paths avoid native107 source/canonical journals and no Cargo/GUI.
- 2026-10-17T19:24:56Z stage implement start: fetched origin/main 8ac1834a2c1b9b439b4b6c94edc0f98879b7f1ac; exact legacy/canonical blobs match contract, 173 headings, no Markdown outgoing links. Direct baseline checker reproduces sole Markdown error. Power sleep/restart active after resolving physical skill root; alias-relative first invocation unavailable.
- 2026-10-17T19:25:38Z stage implement end; stage verify end: exact archive blob 2ab2c344ae1f8251ebb55a10b1843a7a8583fe8f, 138178 bytes and 173 headings; canonical 0c6e523a8d169826f7a5722ff539252dd9947ba0 unchanged. One _top archive link added; no outgoing Markdown/HTML links to break. Direct release-matched designRecordErrors(candidate) returned []; ticket validation and staged diff check passed. Root-only onboarding command cannot validate candidate and is not claimed. No Cargo or GUI required by exact contract. Power sleep/restart still active.
- 2026-10-17T19:26:21Z stage review start: clean candidate 6685e4e submitted to fresh foreground Codex CLI reviewer with refreshed ticket/goal authority and current preset gpt-6.1-sol high. Direct delivery selected by goal/current settings. Official run reading 90 percent, ordinaryUsageAllowed true, reset 2026-10-22T06:19:26Z; supported helper95 eligible, helper100 limitation remains. Narrow contract excludes native/GUI; reviewer artifacts isolated in task-specific OS temporary parent.
- 2026-10-17T19:28:19Z Fresh CLI review approved exact 8ac1834a2c1b9b439b4b6c94edc0f98879b7f1ac..6685e4e2923698e8b49127fac2c14c4452324708; reviewer codex:01a14b54-0d4c-7f81-a260-7cb2042c6416, no findings. Independent exact blob/link/checker/ticket-format/diff checks passed. Reviewer live ticket fetch unavailable (GitHub unreachable); supplied admitted contract governed approval. Receipt retained in OS temporary ivy-110-review-e1a116bd-d1ab-4bd6-8908-60cd33f68145/codex-review-8ac1834a2c1b-6685e4e29236-9a71edca938c. Candidate clean; root status unchanged.
- 2026-10-17T19:28:49Z stage integration start session codex:01a14b51-ef77-71c0-8c23-f14fc1d38437 — reviewed 8ac1834a2c1b9b439b4b6c94edc0f98879b7f1ac..20c1a79140e1a92d517ec03f32fe40c73d2a50b4 as 37a1ca7fcd1c2afdd9973e719daf473383939698..6099433204f90e2d6f960d5fd8c5057436ad3341
- 2026-10-17T19:29:22Z stage review end session codex:01a14b54-0d4c-7f81-a260-7cb2042c6416 — approved candidate 8ac1834a2c1b9b439b4b6c94edc0f98879b7f1ac..20c1a79140e1a92d517ec03f32fe40c73d2a50b4
- 2026-10-17T19:29:22Z stage integration end session codex:01a14b51-ef77-71c0-8c23-f14fc1d38437 — integrated 20c1a79140e1a92d517ec03f32fe40c73d2a50b4 as 8901f9480c0507d144464777fd593dab20e21f9d
