---
format: 3
status: ready
created: 2026-08-28T16:29:14Z
origin: system-detected
tags: [project-maintenance, recovery, git, workflow]
value: 9
risk: 3
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: []
supersedes: []
split-from: []
---

# Make orphaned project progress safely recoverable

The authoritative `main` branch ends after ticket 008, while `.ivy/worktrees/` retains later project work as three registered detached worktrees and five unregistered directory snapshots containing ticket records through 029.
Create a durable, reviewable recovery inventory so completed work is not silently rebuilt, untrusted snapshots are not mistaken for integrated history, and unresolved operational records are not lost.

## Outcome

- Add the human-readable recovery inventory `docs/recovery/orphaned-progress-2026-08-28.md`, its per-artifact manifests under `docs/recovery/orphaned-progress-2026-08-28-manifests/`, and the bounded headless helper `tools/recovery/inventory-orphaned-progress.ps1`; together they account for the eight pre-existing artifact paths frozen by this contract, every registered head and working-tree state among them, and every ticket record numbered 009 through 029 found in those artifacts.
- Record for each artifact its exact path, whether Git currently registers it, its reachable commit when one exists, a deterministic content manifest and digest over the allowlisted project files, its ticket lifecycle claims, and its relationship to authoritative `main` at `c24ed0a88c2bff843e788e1957502d9b86bc3d25`.
- For every dirty registered worktree, preserve the staged, unstaged, and untracked status classification plus the content digest of every allowlisted regular file, so valuable bytes beyond its detached head cannot disappear from the inventory.
- Classify each distinct later outcome as `recoverable-reviewed-history`, `recoverable-evidence-only`, `duplicate`, `superseded`, `blocked-external-action`, or `discardable-residue`, with bounded evidence and the next owning ticket or recovery action.
- Preserve three separate identities for orphan ticket history: each artifact occurrence by artifact and relative path, each byte variant by SHA-256, and each logical ticket by number. Record all observed occurrences and variants so later lifecycle states and rejected or corrected reviews are not collapsed into one row.
- Preserve a collision map for ticket numbers 013 and later because the canonical next-ticket allocator reports 014 after creating this ticket while unregistered snapshots contain records using 013 through 029, including an orphaned moving-platform ticket numbered 013.
- Leave a cold-reader handoff that identifies the safest next independently reviewable recovery slice without integrating, deleting, renumbering, or trusting any artifact merely because it says `closed`.
- Correlate recorded commit IDs, surviving Git objects, operational `.git-index` files, and snapshot bytes read-only before deciding whether an unregistered snapshot retains recoverable provenance.

## Decisions

- `main` remains the only authoritative project state until a separate ticket owns and passes exact recovery review.
- Registered detached commits are addressable evidence but are not integrated results. Unregistered directory snapshots remain evidence until read-only correlation proves whether their recorded commits, surviving objects, operational indexes, and bytes establish stronger provenance.
- Lifecycle labels inside orphaned ticket files are claims to verify against commits, reviews, checks, and content, not authority to replay those changes.
- This ticket records and preserves; it does not admit, implement, integrate, clean, move, renumber, or delete orphaned work.
- Candidate-owned inventory paths are exactly `docs/recovery/orphaned-progress-2026-08-28.md`, `docs/recovery/orphaned-progress-2026-08-28-manifests/`, `tools/recovery/inventory-orphaned-progress.ps1`, and this ticket's work log.
- The frozen artifact baseline is exactly `.ivy/worktrees/009-projectile-cards`, `.ivy/worktrees/010-volley-projectiles`, `.ivy/worktrees/011-radial-saw-maps`, `.ivy/worktrees/013-dynamic-arena`, `.ivy/worktrees/014-content-roadmap`, `.ivy/worktrees/015-projectile-damage-scale`, `.ivy/worktrees/022-controller-support`, and `.ivy/worktrees/029-passive-auras`. The new `.ivy/worktrees/013-make-orphaned-project-progress-safely-recoverable` delivery worktree is workflow state, not an orphan artifact, and is excluded from inventory input.
- The inventory helper may read only regular files in these project-shaped roots: `.github/`, `docs/`, `game/`, `reels/`, `replays/`, `research/notes/`, `spec/`, `src/`, and `tools/`, plus the top-level files `.gitattributes`, `.gitignore`, `AGENTS.md`, `Directory.Build.props`, `global.json`, `GOAL.md`, `README.md`, and `Rounds.sln`. Every regular file beneath an allowlisted root is included regardless of extension, including the three design PNGs beneath `docs/design/concepts/`.
- The helper must exclude only files outside that allowlist, `.git`, `.git-index`, `.tools`, `.tmp`, `.godot`, every path segment named `bin` or `obj`, `research/raw`, and every reparse point from content manifests. Separate provenance inspection may read `.git-index` structure and named Git objects without including those operational bytes in a content digest.
- Content manifests use forward-slash relative paths sorted by ordinal UTF-8 byte order. Each LF-terminated UTF-8 line without a BOM is `<relative-path>\t<byte-length>\t<lowercase-SHA-256-of-raw-file-bytes>`; the artifact digest is the lowercase SHA-256 of those exact manifest bytes.
- The helper and its evidence commands must not enumerate, print, hash, encode, or test environment variables or credential values. No session recovery is needed for this inventory; if later correction proves otherwise, the contract must reopen before reading even a named variable.
- Candidate creation may add its new detached worktree and Git objects, and may change only this ticket's work log, the explicitly named inventory and helper paths, and narrowly scoped append-only entries in `docs/decisions.md` or `docs/design-docs/postmortems.md` for judgments or failures this inventory itself creates; it must not move any existing ref or mutate any pre-existing orphan-artifact byte. Admission and final integration may additionally change only this ticket's lifecycle path, its admission/decision bookkeeping, and the named delivery records while moving the intended ticket worktree and `main` ref through the normal reviewed workflow.
- Visible applications are unnecessary and forbidden for this ticket; all inspection and evidence stay headless under the project monitor-placement rule.

## Evidence required

- `git rev-parse HEAD`, `git worktree list --porcelain`, and a bounded inventory of the eight frozen artifact paths reproduce the authoritative head, three pre-existing registered detached worktrees, five unregistered snapshots, and the exact artifact paths recorded in the recovery inventory while excluding the ticket's own delivery worktree.
- Every registered head is resolved to an exact commit and compared with `main`; every artifact receives the specified deterministic allowlisted manifest and digest without reading excluded caches, build-output directories, `research/raw` source media, reparse targets, environment variables, or credential values.
- `git status --porcelain=v1 -z --untracked-files=all` or an equivalently lossless read-only status boundary proves each registered worktree's staged, unstaged, and untracked allowlisted paths are represented without omitting bytes beyond its head.
- A ticket scan records every artifact occurrence, byte variant, and logical number from 009 through 029, including the observed multiple variants of 009, 011, 013, 015, and 019 and all idea, ready, blocked, and closed claims.
- Read-only Git-object and operational-index probes report which recorded orphan commits and index entries can still be resolved and correlate them with snapshot manifests; missing repository control files or objects remain explicit uncertainty rather than being converted into evidence-only or discardable verdicts.
- The inventory explicitly preserves the orphaned blocked external-action record without printing, searching for, hashing, encoding, or testing any credential value.
- Phase-specific before-and-after comparisons prove candidate creation moved no pre-existing ref or registration and changed no pre-existing orphan-artifact byte; candidate paths are limited to this ticket's work log, the named inventory and helper, and attributable append-only decision or postmortem entries; final integration changes only those reviewed paths, the admitted ticket/decision/delivery records, and intended refs or ticket worktree required by the reviewed workflow.
- `node C:/Users/Adam/.codex/plugins/cache/ivy/ivy/0.1.0/checks/scripts/check-tickets.mjs .` and `git diff --check` pass.

## Work log

- 2026-08-28T16:29:14Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Reflected on the empty authoritative queue and found later project progress split across registered detached worktrees and unregistered snapshots.
- 2026-08-28T16:29:14Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Bounded a read-only recovery inventory before feature work so later reviewed outcomes, blocked records, and ticket identities can be reconciled without treating residue as authority.
- 2026-08-28T16:42:31Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Reopened shaping after fresh admission review found dirty registered bytes, digest framing, ticket-variant identity, surviving Git provenance, and phase-scoped immutability underspecified.
- 2026-08-28T16:42:31Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Bound an allowlisted byte manifest, lossless dirty-state inventory, three ticket identities, read-only provenance correlation, credential-safe inspection, and exact candidate/integration mutation boundaries.
- 2026-08-28T16:49:45Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Reopened shaping after a second admission review found the artifact set self-referential and the candidate path boundary too narrow for binding project ledgers.
- 2026-08-28T16:49:45Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Froze the eight pre-existing artifact paths, excluded the delivery worktree, and allowed only attributable append-only decision and postmortem entries beyond the inventory-owned paths.
- 2026-08-28T16:54:59Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Reopened shaping after a third admission review found generic media exclusions made the allowlisted byte set ambiguous.
- 2026-08-28T16:54:59Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Included every regular file under the named roots regardless of extension, named the design PNGs explicitly, and reduced exclusions to exact paths, path segments, and reparse points.
- 2026-08-28T16:55:43Z stage admission start session codex:01a0494c-7878-7232-9169-27500bc90c45/admit_013d — Freshly reviewed the exact corrected contract against the eight artifact paths, dirty registered bytes, ticket variants, surviving indexes and objects, content-set grammar, credential boundary, ordering, risk, and prior findings.
- 2026-08-28T17:01:35Z stage admission end session codex:01a0494c-7878-7232-9169-27500bc90c45/admit_013d — Admitted at risk 3 with no findings after four fresh reviews made the preservation, provenance, digest, identity, self-reference, mutation, and media boundaries cold-reader complete.
- 2026-08-28T17:06:09Z stage implement start session codex:01a04953-a0de-7cf2-9383-bb13865303dd — Captured the read-only ref, registration, candidate-boundary, and frozen-root baselines before opening any artifact file.
- 2026-08-28T17:21:26Z stage implement end session codex:01a04953-a0de-7cf2-9383-bb13865303dd — Added the bounded helper, eight deterministic manifests, and a cold-reader inventory covering artifact bytes, dirty state, object/index provenance, ticket identities, collisions, classifications, and the next review slice.
- 2026-08-28T17:21:26Z stage verify start session codex:01a04953-a0de-7cf2-9383-bb13865303dd — Verifying deterministic reruns, manifest framing and raw bytes, ticket occurrence/variant/logical identity, provenance claims, frozen baselines, ticket structure, whitespace, and the exact write boundary.
- 2026-08-28T17:26:20Z stage verify end session codex:01a04953-a0de-7cf2-9383-bb13865303dd — Passed two deterministic reruns, independent raw-byte manifest checks, 55-occurrence/28-variant/21-logical-ticket checks, object/index correlation, unchanged artifact/ref/registration baselines, the Ivy ticket checker, LF-only checks, and `git diff --check`.
- 2026-08-28T17:27:28Z stage correction start session codex:01a04953-a0de-7cf2-9383-bb13865303dd — Correcting the cold-reader guarantee so it distinguishes forbidden environment-value inspection from the child-only Git control setting used to select each operational index.
- 2026-08-28T17:27:49Z stage correction end session codex:01a04953-a0de-7cf2-9383-bb13865303dd — Replaced the overbroad claim with the exact guarantee that no command enumerated, printed, hashed, encoded, or tested an environment-variable or credential value.
- 2026-08-28T17:27:49Z stage verify start session codex:01a04953-a0de-7cf2-9383-bb13865303dd — Rechecking ticket structure, LF-only bytes, whitespace, write scope, frozen baselines, and the corrected inventory wording.
- 2026-08-28T17:28:17Z stage verify end session codex:01a04953-a0de-7cf2-9383-bb13865303dd — The corrected guarantee, ticket structure, LF-only bytes, whitespace, 12-path write scope, refs, registrations, and frozen baselines all remain exact.
- 2026-08-28T17:31:38Z stage correction start session codex:01a04953-a0de-7cf2-9383-bb13865303dd — Correcting implementation-session attribution and recording the recovery-order judgment required before review.
- 2026-08-28T17:32:37Z stage correction end session codex:01a04953-a0de-7cf2-9383-bb13865303dd — Reattributed only the eight implementation-phase markers to the actual child session and appended the recovery-order decision without changing the existing decisions prefix.
- 2026-08-28T17:32:37Z stage verify start session codex:01a04953-a0de-7cf2-9383-bb13865303dd — Rechecking ticket identity boundaries, append-only decision bytes, LF-only files, allowed paths, frozen artifacts, refs, registrations, ticket structure, whitespace, and the complete diff.
- 2026-08-28T17:35:34Z stage verify end session codex:01a04953-a0de-7cf2-9383-bb13865303dd — Passed child-identity, append-only decision, LF-only, 13-path scope, ticket, whitespace, frozen artifact, ref, registration, detached-HEAD, and complete-diff checks.
- 2026-08-28T17:42:06Z stage review start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/review_013 — Beginning fresh read-only review of the exact admitted range, deterministic manifests, provenance claims, classification rationale, preservation evidence, and prior implementation corrections.
- 2026-08-28T17:55:17Z stage review end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/review_013 — Rejected because the documented reproduction command wrote into the committed baseline manifest directory, did not explicitly compare exact manifest bytes, and hardcoded the disposable delivery-worktree path.
- 2026-08-28T17:59:10Z stage correction start session codex:01a04953-a0de-7cf2-9383-bb13865303dd — Replacing the unsafe reproduction instructions with a durable-root, fresh external-output, exact-byte comparison and bounded cleanup procedure.
- 2026-08-28T18:03:52Z stage correction end session codex:01a04953-a0de-7cf2-9383-bb13865303dd — Replaced only the inventory instructions and checklist wording; the documented procedure now derives Git paths, writes to a unique external directory, compares the exact file set and every byte, retains failures, and removes only successful output.
- 2026-08-28T18:03:52Z stage verify start session codex:01a04953-a0de-7cf2-9383-bb13865303dd — Rechecking the documented procedure, ticket structure, LF-only bytes, exact write scope, frozen artifacts, refs, registrations, detached HEAD, whitespace, and the complete diff.
- 2026-08-28T18:05:39Z stage verify end session codex:01a04953-a0de-7cf2-9383-bb13865303dd — The documented procedure reproduced and byte-compared all eight manifests outside the repository, cleaned only its fresh output, and passed ticket, LF-only, two-path scope, whitespace, frozen artifact, ref, registration, detached-HEAD, and complete-diff checks.
