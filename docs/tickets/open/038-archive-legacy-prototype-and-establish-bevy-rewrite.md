---
format: 3
status: ready
created: 2026-09-04T00:43:56Z
origin: human-request
tags: ["architecture", "bevy", "multiplayer", "fidelity", "project-reset"]
value: 10
risk: 4
sessions:
  - codex:01a06920-7449-74d0-9b09-57855a012572
execution: unattended
depends-on: []
supersedes: [16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 31, 32, 34, 35]
split-from: []
---

# Archive the legacy prototype and establish the Bevy rewrite

The current Godot and C# implementation was optimized around internally verifiable mechanics before direct high-quality gameplay evidence and online multiplayer were treated as product requirements.
Preserve that work as recoverable history, then establish a Bevy codebase whose product target is the complete mechanics, physics, presentation, effects, match flow, and multiplayer experience visible across the two supplied ten-minute ROUNDS recordings.

## Outcome

- The exact final legacy `main` commit remains recoverable through an annotated archive tag, and a documented lookup proves that its source, tests, tickets, specifications, and history can be inspected without keeping them in the active product tree.
- The active product tree no longer builds or ships the legacy Godot shell, C# simulation, Windows evidence launcher, or implementation-coupled regression corpus.
- The project goal and architecture make the two supplied gameplay recordings the primary end-to-end fidelity target and require online multiplayer, eventual Steam transport, programmatic play, and programmatic frame or video capture.
- A committed reference manifest identifies every supplied video and screenshot by repository-relative local path, byte length, and SHA-256 while repository-visible ignore rules keep the media bytes out of Git.
- A complete timestamp-indexed coverage ledger for each identified video maps every distinct visible arena, mechanic, card interaction, match-flow state, and presentation effect to implemented follow-up work or an explicit unresolved fidelity gap.
- A pinned Rust workspace establishes separate simulation, client presentation, networking, headless server, and automation-tool boundaries without requiring GUI-authored project state.
- The first executable slice runs the same fixed-tick authoritative simulation as a local client-host and as a headless server, accepts scripted inputs from two clients, and exposes bounded state inspection suitable for future automated playtesting.
- Follow-up work is organized by visible gameplay slices from the recordings rather than by completing the superseded implementation's subsystem backlog.

## Decisions

- Adam selected a Bevy restart on 2026-09-03 after reviewing direct 60 FPS footage, physics and renderer requirements, multiplayer topology, and the current repository's implementation-to-verification imbalance.
- Preserve legacy commit `382ae14788646c199b42243652d5c0294c6994f4` in Git history and the annotated `archive/godot-csharp-prototype-2026-09-03` tag; do not copy obsolete product code into an `archive/` directory in the active tree.
- Keep the supplied videos and screenshots under ignored `reference/` storage unchanged; `reference/manifest.json` is the only tracked file in that tree.
- Tickets 036 and 037 remain open because the engine reset does not replace the postmortem-gap audit or the preservation-first inventory of unique ticket-035 worktree state.
- Use Bevy ECS for fixed-tick game state and systems, but keep rendering, audio, particles, camera effects, and other presentation state outside the authoritative replicated state.
- Begin with an authoritative client-host over ordinary development transport, predict only latency-sensitive local actions, interpolate remote and dynamic-world state, and keep the same simulation runnable by a headless dedicated server.
- Isolate third-party networking and physics behind narrow project-owned boundaries, pin every dependency and the lock file, and require behavioral upgrade evidence instead of treating compiler success as migration proof.
- Tests protect observed behavior and public boundaries; they are not permanent merely because they exist, and local automation is not hardened against an adversarial machine unless a real release threat model requires it.
- Do not impose a mechanical test-to-production line ratio, but require explicit justification whenever support or test code for a slice is larger or more complex than the product behavior it protects.

## Evidence required

- `git rev-parse archive/godot-csharp-prototype-2026-09-03^{}` equals the recorded legacy `main` commit, and representative legacy files are readable from that tag after their removal from the active tree.
- Repository inventory proves that no active build, package, or runtime path references the legacy Godot, .NET, or evidence-launcher implementation while ignored reference media remains byte-identical.
- Manifest verification recomputes every available reference byte length and SHA-256, and a clean-checkout test proves the repository-visible ignore rules exclude the media while retaining the manifest.
- The coverage ledger accounts for the full duration of both identified recordings with no unclassified interval that contains distinct gameplay or presentation behavior.
- Locked Rust formatting, lint, build, and test commands pass from a clean checkout with no editor interaction.
- A command-line smoke test starts an authoritative headless server plus two scripted clients, advances a bounded match interval, and verifies agreed authoritative state and clean shutdown.
- A client capture command renders a deterministic frame from the executable slice without opening an editor and records the exact inputs, seed, tick, and build identity used.
- Architecture review maps every retained test to a user-visible behavior, stable public boundary, reproduced defect, or explicit release threat; unsupported verification machinery is absent.

## Work log

- 2026-09-04T00:43:56Z stage design start session codex:01a06920-7449-74d0-9b09-57855a012572 — Began the recoverable Bevy reset after Adam made complete replication of both supplied gameplay videos, including multiplayer, the final product goal and asked to archive the old implementation.
- 2026-09-04T00:45:24Z stage design end session codex:01a06920-7449-74d0-9b09-57855a012572 — Bound the recoverable legacy tag, footage-first product target, multiplayer-first Bevy boundaries, proportional testing rule, and command-line server, client, inspection, and capture evidence; the repository-local `playbook` checker path named by newly injected guidance was absent, so the identical installed Ivy checker was resolved from the active plugin cache and passed across all registered worktrees.
- 2026-09-04T00:46:05.861Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a069e1-2a76-7c51-b7f4-5750932d7c06 — Began fresh review of the first records-only ticket candidate `f7fd66fc6063bf5ea605abe3360e38ae42d30853`.
- 2026-09-04T00:50:20.413Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/01a069e1-2a76-7c51-b7f4-5750932d7c06 — Rejected the first candidate because it incorrectly superseded two maintenance tickets and did not durably identify or coverage-gate the two supplied recordings.
- 2026-09-04T00:51:05Z stage correction start session codex:01a06920-7449-74d0-9b09-57855a012572 — Correcting fresh-review findings by retaining tickets 036 and 037, binding every supplied media file through a tracked hash manifest, and requiring timestamp-complete coverage of both recordings.
- 2026-09-04T00:52:55Z stage correction end session codex:01a06920-7449-74d0-9b09-57855a012572 — Retained the two maintenance tickets, added repository-visible raw-media ignore rules and a verified eleven-file hash manifest, and required a timestamp-indexed ledger to account for every distinct behavior across both recordings; the installed ticket checker and whitespace validation passed.
- 2026-09-04T00:53:59.013Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a069e1-2a76-7c51-b7f4-5750932d7c06 — Began fresh review of corrected records-only candidate `592bf066245cae9ee65d1c06376c542702333434`.
- 2026-09-04T00:55:06.971Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/01a069e1-2a76-7c51-b7f4-5750932d7c06 — Approved the corrected candidate with no findings after independently verifying all eleven media hashes and sizes, ignore behavior, archive-tag identity, retained maintenance tickets, full-duration footage coverage requirement, ticket format, and clean whitespace.
- 2026-09-04T00:55:23.510Z stage design end session codex:01a06920-7449-74d0-9b09-57855a012572 — Marked the risk-4 contract ready after independent admission review resolved every finding and confirmed that no open human decision remained.
- 2026-09-04T00:56:41.153Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a069ea-c48c-79b3-8a09-bb68f94e1159 — Began fresh final review of exact records-only range `382ae14788646c199b42243652d5c0294c6994f4..f68fd81d24f9cd427aa7905fec9d7abe5f1f1871`.
- 2026-09-04T01:01:58.313Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/01a069ea-c48c-79b3-8a09-bb68f94e1159 — Approved the exact range with no findings after verifying archive recovery, every media identity and ignore rule, retained maintenance tickets, ticket lifecycle and provenance, clean Git integrity, and absence of product changes.
- 2026-09-04T01:06:55.666Z stage implement start session codex:01a069f2-f797-76a2-9107-89a33b430359 — Began the complete Bevy restart in the existing ticket worktree after reading the integration guidance, admitted contract, reference manifest, retained maintenance tickets, architecture, decisions, and relevant failure records; the requested worktree `CLAUDE.md` is absent.
- 2026-09-04T01:06:58.228Z stage implement start session codex:01a06920-7449-74d0-9b09-57855a012572/implement_038 — Began replacing the active Godot and C# product with the footage-first Bevy foundation and its headless multiplayer, state-inspection, and deterministic-capture slice.
- 2026-09-04T01:30:27.154Z implementation incident — An earlier Ivy dispatcher had silently started duplicate implementer `codex:01a069f2-f797-76a2-9107-89a33b430359` in the same worktree; the orchestrator terminated exact duplicate root PID 9968, verified its dispatcher tree gone, and the intended implementer reconciled the compatible CI, client-capture, and complete footage-ledger work before resuming as sole writer.
- 2026-09-04T01:36:04.989Z stage implement end session codex:01a06920-7449-74d0-9b09-57855a012572/implement_038 — Replaced active legacy runtime and build paths with the pinned six-crate Bevy workspace, mapped all 1,200.30 seconds of supplied footage to implemented or explicit-gap slices, and passed clean locked format, lint, build, tests, two-client authority, deterministic capture, archive, manifest, inventory, ticket, whitespace, process, and residue checks.
- 2026-09-04T01:39:06.215Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a06a11-3267-7340-9035-208e7b7885cc — Began fresh review of exact implementation candidate range `007699e85c12bd268f6382a7eb8e69f516bae654..1715d6894c0d4a1bdbdb5012e9922cb0b49242b8`.
- 2026-09-04T01:47:10.277Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/01a06a11-3267-7340-9035-208e7b7885cc — Rejected the candidate because partial client-start failure orphaned the authoritative server until timeout, aliased capture and metadata paths destroyed the claimed PNG while exiting successfully, and the mandatory exact-range whitespace check failed in three TOML files.
- 2026-09-04T01:48:19.039Z stage correction start session codex:01a06920-7449-74d0-9b09-57855a012572/implement_038 — Correcting the rejected candidate at its public command boundaries by making every spawned smoke-test child cleanup-owned, rejecting resolved-equivalent capture paths before writing, adding focused process regressions, and removing the three EOF blank lines.
- 2026-09-04T01:53:08.665Z evidence correction — The 01:36 implementation-end claim that exact-range whitespace validation passed was unsupported: it checked only the working-tree diff, while fresh review found trailing blank lines relative to base `007699e85c12bd268f6382a7eb8e69f516bae654`; this correction removes them and will validate the required exact range directly.
- 2026-09-04T01:55:24.121Z stage correction end session codex:01a06920-7449-74d0-9b09-57855a012572/implement_038 — Made the smoke command cleanup-own every spawned server and client, proved forced second-client launch failure releases the server, rejected resolved-equivalent capture paths before either write, removed all three reported EOF blank lines, and passed fresh locked format, lint, build, seven tests, successful smoke and inspection, repeated capture, exact-range whitespace, ticket, process, and residue checks.
- 2026-09-04T01:58:26.988Z evidence correction — The 01:55 correction-end claim that the fresh workspace test passed concealed an ordering dependency: its clean target ran `cargo build` before `cargo test`; after the owner removed `target`, `cargo test --workspace --offline --locked` failed because the smoke regression required an unbuilt sibling `rounds-server` executable.
- 2026-09-04T01:58:27.001Z stage correction start session codex:01a06920-7449-74d0-9b09-57855a012572/implement_038 — Replacing the regression's hidden client-binary override and workspace-build precondition with a test-owned minimal UDP child that directly exercises cleanup ownership across partial process startup.
- 2026-09-04T02:01:14.502Z stage correction end session codex:01a06920-7449-74d0-9b09-57855a012572/implement_038 — Removed the production fault-injection environment seam and sibling-binary integration test, then proved the real cleanup owner with a minimal test-owned UDP child and a failing second spawn; `cargo test --workspace --offline --locked` passed alone from an absent target with no ignored tests, followed by locked format, lint, build, tests, smoke, inspection, repeated capture, exact-range whitespace, ticket, process, and residue checks.
- 2026-09-04T02:02:49.694Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a06a27-50ae-7d13-8d6d-ae9e84230d5c — Began fresh review of corrected exact implementation range `007699e85c12bd268f6382a7eb8e69f516bae654..55ee68793114044d23cfe9b1b727df573c7f1464`.
- 2026-09-04T02:08:37.240Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/01a06a27-50ae-7d13-8d6d-ae9e84230d5c — Approved the corrected range with no findings after absent-target tests, format, strict lint, build, repeated two-client smoke, bounded inspection, deterministic and aliased-path capture, archive/media/coverage integrity, exact-range whitespace, ticket, dependency, provenance, process, residue, and clean-status checks passed.
- 2026-09-04T02:16:48.698Z stage integration start session codex:01a06920-7449-74d0-9b09-57855a012572 — Preparing a bounded resolution candidate because the approved range appended ticket-038 operational notes to the same postmortem file that contains Adam's uncommitted monitor-placement record in the integration root.
- 2026-09-04T02:17:07.218Z stage integration end session codex:01a06920-7449-74d0-9b09-57855a012572 — Removed only the candidate's ticket-038 postmortem additions so the integration does not touch Adam's dirty file; the duplicate-dispatch incident remains durably recorded in this ticket, and the candidate postmortem now matches the integration base byte-for-byte.
- 2026-09-04T02:17:33.866Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a06a35-0162-7762-a4c6-54f1f19f3648 — Began fresh review of bounded integration resolution range `007699e85c12bd268f6382a7eb8e69f516bae654..f5eeb0b1eccff28300c25d9c62a6d2c48742137c` against approved tip `a819f111f1bf215967454889863d724e9178bfc2` and Adam's dirty integration-root state.
- 2026-09-04T02:21:13.150Z stage review end session codex:01a06920-7449-74d0-9b09-57855a012572/01a06a35-0162-7762-a4c6-54f1f19f3648 — Approved the resolved range with no findings after proving every runtime byte unchanged, the candidate postmortem identical to the integration base, Adam's uncommitted records untouched, the duplicate-dispatch ticket record retained, and exact-range whitespace, ticket, provenance, Git integrity, ancestry, and clean-status checks passed.
- 2026-09-04T02:27:14Z stage review start session codex:01a06920-7449-74d0-9b09-57855a012572/01a06a3d-8f2a-7a62-ba9a-78fb963923c1 — Began final fresh delivery review of the exact candidate after recording its independent reviewer identity before that context inspected any repository byte.
