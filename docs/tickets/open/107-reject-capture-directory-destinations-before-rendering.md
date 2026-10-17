---
status: ready
owner: codex:01a14b3a-4989-7243-8714-7e5004bba7f3
created: 2026-10-17T18:55:00Z
origin: system-detected
tags: [quarrel, capture, cli, bug]
value: 5
risk: 2
sessions:
  - codex:01a148af-57cb-7751-96dc-e93972e0b69f
  - codex:01a14b3a-4989-7243-8714-7e5004bba7f3
execution: unattended
parent: 105
depends-on: []
supersedes: []
split-from: []
---

# Reject capture directory destinations before rendering

A capture command can write its image and only then discover that the metadata destination is an existing directory. The failed command leaves incomplete evidence that looks usable and does needless renderer work.

## Outcome

Reject an existing directory supplied as either capture output or metadata destination before simulation, network or renderer work. Apply this through the existing capture-destination boundary for its current public CLI callers, preserving valid captures and path-alias protection.

## Decisions

- Destination: main. Bounded CLI boundary fix; no protocol, game rules, tuning, window or capture format changes.
- Revalidated exact origin/main7959e9408f37a71e0dc16bb8ff60a03f644e2194: quarrel-client main.rs capture_destinations only rejects aliases. Capture writes PNG before fs::write metadata; remote render resolves destinations before connect but also writes PNG before metadata.
- Public failure-before: create metadata-dir then run quarrel-client capture --seed 38 --ticks 0 --output frame.png --metadata metadata-dir. It fails after frame.png exists. Retain exact command/output and smallest regression before source edits; do not rely on the first stale rounds-* reflection report.
- Extend the existing resolver rather than creating a generic validator/transaction framework. The contract covers known existing-directory misuse; do not promise globally atomic files or broaden to races/permissions without separate evidence.
- Preserve existing destination alias checks, valid output content/provenance, intentional overwrite semantics and all unrelated CLI behavior. Reject directories without removing or altering them.
- Sole native Cargo slot, jobs<=2, canonical shared out/cargo-target reused; complete normal verification, no clean target or linker/profile/debug change. Headless proof needs no visible window; any unexpected GUI must obey unfocused monitor4 rules.
- Preserve stale root human dirty/untracked onboarding files, all old worktrees/proof and shared cache. No native/global input, external reports, Steam invite or outside repository changes.
- Record the real initial reflection-source detour briefly in docs/postmortems.md: both readers initially inspected stale main despite the requested origin/main; source-path mismatch caught before implementation, one old finding discarded and this one revalidated on current source. Existing explicit source instruction was sufficient; add no new process rule. Scheduler holds all other shared journal writers.

## Evidence required

- Public CLI regression fails before and passes after the correction. For capture and remote render, existing-directory metadata and output destinations produce the intended validation error before renderer/network and leave no new paired output. Preserve the pre-existing directories and sentinel contents.
- Existing aliased/equal-path rejections and a valid headless capture with correct metadata still pass. Reuse existing public capture tests rather than mirroring an internal check.
- Normal tools/verify.ps1 passes required format, strict all-target Clippy, locked build and tests with attributable source/artifacts. Supported documentation checker retains the known legacy Markdown error honestly; ticket/diff checks pass.
- Fresh nonauthor exact-candidate CLI review before direct main integration/close; preserve checkpoint and block if quota prevents review instead of integrating unreviewed work. Cleanup only newly owned worktree, retaining necessary proof.
- No actual GUI/network startup occurs for the invalid remote destinations; failures are not later connect timeouts or renderer errors.

## Work log

- 2026-10-17T18:55:00Z Bounded reflection yielded one current-source capture validation defect. Runtime reader discarded its stale rematch finding after rechecking exact origin/main. No code changed by scheduler; fresh admission pending.
- 2026-10-17T18:56:21Z Admitted after independent /root/drain_runtime_reflect current-origin source and contract check: risk2, no dependencies or operator decision. Resolver boundary precedes capture/remote work, public regressions preserve sentinels/valid captures/aliases, no atomic-framework expansion. Sole native slot free; no shared journal writer overlaps.
- 2026-10-17T18:59:13Z stage research start session codex:01a14b3a-4989-7243-8714-7e5004bba7f3 — Own detached worktree at fetched origin/main 4954ba37904af2990139adffef8fa12db2792315; current quarrel source confirmed. Sleep and restart protection active after resolving installed Playbook root; initial alias-relative helper path unavailable.
- 2026-10-17T19:01:46Z stage research end session codex:01a14b3a-4989-7243-8714-7e5004bba7f3 — Fresh current-source client reproduced capture --seed 38 --ticks 0 with metadata-dir: exit1 Access is denied after frame.png exists; proof out/ticket107-directory-destinations/before. capture_destinations checks aliases only; render_capture_metadata writes metadata after render_png saves image. Both callers resolve before simulation/network. Supported physical-path onboard/check-documents exits1 for existing root docs/design-docs/postmortems.md; alias invocation emitted nothing and is not counted.
- 2026-10-17T19:02:04Z stage implement start session codex:01a14b3a-4989-7243-8714-7e5004bba7f3 — CLI regression failed on baseline with late metadata write error (exit101); retained regression-before.txt. Add directory rejection to existing resolver after unchanged alias checks, covering both callers. Extend existing valid capture test to overwrite sentinel files.
- 2026-10-17T19:02:17Z stage implement end session codex:01a14b3a-4989-7243-8714-7e5004bba7f3 — Shared resolver directory check and public regressions complete; bounded reflection detour recorded in docs/postmortems.md using deslop. stage verify start: required normal tools/verify.ps1 per contract and AGENTS, alias/provenance/overwrite and invalid-destination CLI coverage; supported document checker known legacy failure retained. Sleep/restart protection remains active.
- 2026-10-17T19:12:04Z stage verify end session codex:01a14b3a-4989-7243-8714-7e5004bba7f3 — Normal tools/verify.ps1 passed format, strict all-target Clippy, locked workspace build, doctests and every executable test including eight directory cases, aliases and valid overwrite/provenance capture. Fresh public after command exits1 directory error, no image, sentinel intact; proof and attributable binary/record retained under out/ticket107-directory-destinations. Ticket and diff checks pass. Only known unrelated legacy Markdown document check remains failed. stage review start: fresh CLI exact candidate requested; current preset Sol6.1 high subscription, goal100 ceiling retained.
- 2026-10-17T19:20:09Z Fresh CLI review approved exact pre-trailer940c06ef1651d9c3fa8f042242c61f88a7df0a11, reviewer codex:01a14b4b-d683-7992-8f76-5867a99311cb; receipt out/ticket107-directory-destinations/review-result.json. All43 recorded source hashes and attributed binary copies checked. Reviewer ticket-helper refresh was network-blocked, retained current admitted107/goal105 exports used. Post-rebase full verifier passed all186 tests. Current direct delivery and Sol6.1 high preset refreshed; supported live budget95 eligible89percent, explicit goal100 ceiling remains. No files or GUI artifacts from reviewer. Preparing equivalent main integration.
