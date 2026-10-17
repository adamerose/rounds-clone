---
status: idea
created: 2026-10-17T18:55:00Z
origin: system-detected
tags: [quarrel, capture, cli, bug]
value: 5
risk: 2
sessions:
  - codex:01a148af-57cb-7751-96dc-e93972e0b69f
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
- Public failure-before: create metadata-dir then run quarrel-client capture --seed38 --ticks0 --output frame.png --metadata metadata-dir using actual CLI flag separation. It fails after frame.png exists. Retain exact command/output and smallest regression before source edits; do not rely on the first stale rounds-* reflection report.
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