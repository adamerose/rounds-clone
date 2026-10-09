---
status: closed
created: 2026-10-09T03:41:28Z
origin: agent-proposed
tags: ["quarrel", "build", "performance"]
value: 7
sessions:
  - codex:01a11df8-2117-7ad1-93c1-4a57a586fedb
  - claude:dadaf59d-be68-4ec3-8324-29525f9fa839
execution: unattended
parent: 75
depends-on: [87, 91]
supersedes: []
split-from: []
---

# Avoid rebuilding unchanged workspace crates during repeat verification

The supported verification command rebuilds every workspace crate on every run to prevent stale artifacts from another checkout. Reuse unchanged compatible artifacts while preserving reliable source attribution and the complete verification route.

## Outcome

- Repeating verification in the same unchanged checkout performs no workspace-crate compilation or linking in Clippy, build or test --no-run. Doctest compilation is reported separately; all frozen current-source tests still execute.
- A run never accepts artifacts built from another checkout or outdated inputs as its own. Switching worktrees, older file timestamps and foreign builds still execute the intended source and asset content.
- Each run freezes its executables before a later build can overwrite the shared target. The complete format, strict Clippy, locked build, doctest and test route remains the supported public command.

## Decisions

- Preserve 87's source-attribution and frozen-executable invariants. Reuse must be backed by current compilation inputs, checkout identity and the actual artifact identity, rather than file timestamps alone.
- Inputs include dependency-info-listed files, workspace/package manifests, lock file, Cargo configuration, toolchain identity and embedded assets. In the real workspace that includes assets/replays/ordinary-match.json and assets/tuning.ron. Missing, corrupt or incompatible attribution forces trustworthy rebuilding.
- Retain the prepared shared target, dependency cache, two-job maximum and scheduling delivered by 91. Do not change targets, profiles, features, compiler flags, RUSTFLAGS, CARGO_INCREMENTAL or the stable toolchain.
- Use small detached fixture worktrees of this repository for the adversarial sequence. They retain the real Git common directory, existing .cargo/config.toml and exact supported verification script; do not add a generic build framework or a fixture-specific verification mode.
- Update the verification synopsis and agent guidance where they promise blanket workspace rebuilding. Do not claim improved foreground responsiveness or game build timing from fixture measurements.
- If reliable reuse cannot meet this contract, block with the demonstrated gap; an unverifiable cache shortcut is not a delivery.

## Evidence required

- Two detached fixture worktrees retain the real common directory, prepared target and unchanged supported verification invocation. Their small locked workspace includes a binary named quarrel-client, an executable test identifying its source marker, and a root include_bytes! asset identifying its content. The two fixtures use the same package graph and relative paths.
- Establish fixture A, then repeat it unchanged. Cargo reports no workspace compilation or linking in Clippy, build and test --no-run; frozen tests execute A's current source-and-asset marker. Report doctest behavior and avoided compilation work separately.
- At a deterministic gate after A has frozen its test executable, finish a plain Cargo build in B before releasing A's test. A still executes its own marker. A's next verification rejects the foreign artifact and rebuilds its current content. Cargo compilation phases remain serialized.
- Verify B through the supported command, then A; A rejects the intervening artifact. Also prove rebuilding after an older-timestamp source edit, an older-timestamp embedded-asset edit, a missing attribution record and a corrupt record. Each rebuilt test executes its current source-and-asset marker.
- Check the implementation's invalidation boundary against every input and identity listed in Decisions. Record what the fixture proves and its limitation: it reproduces the shared-target freshness and freezing boundary, but does not collide with the full Bevy graph's artifact filenames.
- Run one complete actual-workspace tools/verify.ps1 verification after the matrix, using the prepared target. All checks pass with current-source frozen executables, doctests and fresh compatible dependency artifacts. Then repeat that actual-workspace command unchanged: Cargo output proves no workspace compilation or linking in Clippy, build and test --no-run, while the complete checks and frozen current-source tests still pass. Report doctest behavior separately. This second pass checks real reuse and must not perform another Bevy link; do not multiply full Bevy links to repeat the fixture matrix.

## Work log

- 2026-10-09T03:41:28Z On origin/main after 87, tools/verify.ps1 lines110-111 assign current timestamps to every crate file. Its synopsis states every workspace crate rebuilds; independent admission context reported roughly minute-long warm compile phases. This is observed repeat cost, not proof of the complete user lag cause. No implementation or admission attempted here.
- 2026-10-09T05:05:21Z Bounded fresh-context shaping by Codex /shape_cache_contract found no operator decision, but origin/main tools/verify.ps1 has no small public fixture entry point. The required foreign-writer/older-timestamp/embedded-input/corrupt-record sequence uses the whole workspace and may repeat Bevy links. Remains idea pending a meaningful minutes-scale attribution proof design; a performance benchmark cannot replace cache-safety evidence. No code, builds or cache changes performed.
- 2026-10-09T03:52:15Z Fresh [admission context](http://ivy.localhost/sessions/claude/5e954959-5fd5-46e1-bbff-f03005da8bb4) returned not-admit with six contract findings; retained in Scratch for bounded follow-up. No implementation attempted.
- 2026-10-09T06:31:29Z stage admission start session codex:01a11f02-1a85-7e42-b5c7-6b3d99c6940c — folded six prior findings into contract; exact-script detached fixture matrix plus one actual-workspace route replaces repeated full Bevy links; fresh independent context to judge completeness, checkability and boundaries, no native work
- 2026-10-09T06:34:18Z Fresh contract round1 by [cache admission context](http://ivy.localhost/sessions/codex/01a11f5c-6b6d-7911-8378-218d60dddd09), isolated child of this orchestrator, NOT ADMIT risk3: tiny fixture matrix cannot alone prove the actual workspace avoids recompilation. Added an unchanged second complete real route requiring zero workspace compilation/linking, without a second Bevy link; other-family implementation review remains separate. API session metadata confirms child identity despite inherited parent shell session variable.
- 2026-10-09T06:36:08Z stage admission end session codex:01a11f02-1a85-7e42-b5c7-6b3d99c6940c — ADMIT risk4 after round2 by [fresh cache admission context](http://ivy.localhost/sessions/codex/01a11f5c-6b6d-7911-8378-218d60dddd09); unchanged second actual-workspace route resolves sole round1 gap, dependency87/91 closed, no operator decision. Frozen contract only, never implementation approval. Worker must publish both round1/round2 admission lines in docs/decisions.md with implementation range; no orchestration design record or native build performed.
- 2026-10-09T07:30:32Z stage implement start session claude:dadaf59d-be68-4ec3-8324-29525f9fa839 — attribution record in tools/verify.ps1; worktree .ivy/worktrees/093-avoid-rebuilding-unchanged-workspace-crates-during-repeat-verification
- 2026-10-09T07:50:47Z stage implement end session claude:dadaf59d-be68-4ec3-8324-29525f9fa839 — candidate 22ae9f3..8654808; 14-step fixture matrix passed (gate, foreign, B-then-A, old-timestamp source/asset/manifest, missing/truncated/other-checkout record); actual workspace on 4bbd810: establish 9m39s all 153 tests pass, unchanged repeat compiled 0 in clippy/build/test --no-run (15/6/13 reused), 153 pass, 2m53s; no doctests in workspace
- 2026-10-09T07:51:01Z stage review start session claude:dadaf59d-be68-4ec3-8324-29525f9fa839 — codex exec other-family review of 22ae9f3..8654808
- 2026-10-09T08:41:11Z stage correction end session claude:dadaf59d-be68-4ec3-8324-29525f9fa839 — review round2 codex:01a11fa7-2346-7170-b9d9-416557aadd80 REQUEST CHANGES (incomplete record trusted; in-compile edit recorded) corrected in d63697e..c308b60; round1 codex:01a11fa5-64ff-7df1-b505-719ee08a0069 could not run (read-only sandbox failed). Final script c308b60: 18-step matrix passed; actual workspace establish 7m59s 153 pass, unchanged repeat 0 compiled clippy/build/test --no-run (15/6/13), 153 pass, 2m57s. Range 22ae9f3..2316bbd to review
- 2026-10-09T09:16:38Z stage correction end session claude:dadaf59d-be68-4ec3-8324-29525f9fa839 — review round3 codex:01a11fd3-0016-7de1-b338-67ecf98e381d REQUEST CHANGES (restored in-compile edit; unvalidated dependency info) corrected in b988e59 (NTFS change time, recorded .d hashes); 22-step matrix passed; actual workspace establish 8m07s 153 pass, unchanged repeat 0 compiled clippy/build/test --no-run (15/6/13), 153 pass, 2m51s. Range 22ae9f3..5c03033 to review
- 2026-10-09T09:41:45Z stage correction end session claude:dadaf59d-be68-4ec3-8324-29525f9fa839 — review round4 codex:01a11ff3-865c-7883-8596-9506d2fb8cad REQUEST CHANGES (deleted dependency info shrank checked inputs) corrected in 7cd6621; 24-step matrix passed; actual workspace establish 7m55s 153 pass, unchanged repeat 0 compiled clippy/build/test --no-run (15/6/13), 153 pass, 2m52s. Range 22ae9f3..60cb675 to review
- 2026-10-09T10:05:31Z stage correction end session claude:dadaf59d-be68-4ec3-8324-29525f9fa839 — review round5 codex:01a1200a-6d16-76d2-8621-e6d79a80ceec REQUEST CHANGES (inputs outside the checkout skipped) corrected in d7942cc; 26-step matrix passed; actual workspace establish 7m55s 153 pass, unchanged repeat 0 compiled clippy/build/test --no-run (15/6/13), 153 pass, 2m56s. Range 22ae9f3..3c24fe1 to review
- 2026-10-09T10:12:06Z stage correction end session claude:dadaf59d-be68-4ec3-8324-29525f9fa839 — review round6 codex:01a12020-350b-7031-9187-efc807e22624 APPROVED 22ae9f3..3c24fe1 with one non-blocking decisions wording note; corrected in 2dc1870 (docs only); range 22ae9f3..2dc1870 to fresh review
- 2026-10-09T10:25:17Z stage integration start session claude:dadaf59d-be68-4ec3-8324-29525f9fa839 — delivery policy direct push to main (run 75 launch: destination main, no PR); reviewed 22ae9f3a7c31e688da0e2d4e5c50f7e954bf739c..3829d802c6e32b0b602036b25e36da03c788bdfe (round7 codex:01a12026-3644-7740-9e56-3951a304de09 APPROVE) as e35ba5b96f811b549b10ae543b0c63b216fd5c0d..cea25b2e16322077b5e1517aa837d4158287b3e8; rebased tip verify.ps1 passed 153 tests reusing all workspace units
- 2026-10-09T10:26:19Z stage review end session codex:01a12026-3644-7740-9e56-3951a304de09 — approved candidate 22ae9f3a7c31e688da0e2d4e5c50f7e954bf739c..3829d802c6e32b0b602036b25e36da03c788bdfe
- 2026-10-09T10:26:19Z stage integration end session claude:dadaf59d-be68-4ec3-8324-29525f9fa839 — integrated 3829d802c6e32b0b602036b25e36da03c788bdfe as cc715e4f50fe66675f4399bcc802373c4e8e28d6
