---
status: ready
owner: codex:01a148b3-4215-78f1-b9a5-4ed795348e75
created: 2026-10-12T18:49:42Z
origin: system-detected
tags: [quarrel, replay, network]
value: 5
risk: 3
sessions:
  - codex:01a12f11-0715-7b71-b569-474dc68c330e
  - codex:01a148b3-4215-78f1-b9a5-4ed795348e75
execution: unattended
depends-on: []
supersedes: []
split-from: []
---

# Explain divergent replay of an impaired network trace

A fresh public host/join check completed with matching live client and authority state hashes, but replaying its reconstructed recording diverged from the authority at tick 100. Determine whether the trace-to-recording conversion omitted an input or whether a surviving replay boundary is wrong before changing gameplay.

## Outcome

- Identify the owning boundary and explain why the first 99 ticks agree while tick 100 differs.
- If the trace represents a supported complete recording, public replay reproduces its authority state hashes after a proportionate correction. If the conversion is invalid, correct the evidence conversion and record what the trace can reproduce.
- Keep a small stable regression at that boundary if a product defect is established; do not commit a failing check merely to record this lead.

## Decisions

- Destination: main.
- This is a supplemental investigation outside the required MVP evidence; live scores agree and no user-visible impact or cause has been established.
- Preserve the existing host-authoritative online model. docs/decisions.md pins starting tuning, cards and arenas for recordings, and rejects peer rollback for this MVP; this observation does not reopen that design.

## Evidence required

- Public replay of out/autonomy75-goal-check-20261012/replay-prefix-99.json returns SHA256 eee5a4e50c333a879eed48b0372991b3c10d147cafc960a9fb5835ff38d7f6bb.
- At the checked main game source, replay-prefix-100.json returns ce6dbbf935327c88784744e6d7160fcc2ceaf01d069c5136307793498d99b4f4, while the retained authority trace expects effbd25b9e37a476436421820d6bc40a0a7f80ac4df6530aa95ce15d9cfe635d. Hash compact raw stdout after stripping only CR/LF; do not reserialize numeric values before hashing.
- Use an attributable current-source quarrel-client.exe through replay --input <fixture>. Preserved MSVC baseline and full attribution are named in out/autonomy75-goal-check-20261012/goal-check.json. Repeated impaired replays produced the same divergent terminal hash; ordinary smoke replay matched live authority.

- Before claiming a product defect, compare every recorded applied input (including flow commands), the pinned starting config/content, and the replay command's raw-stdout hash contract with the authority trace. Prefix agreement alone does not establish conversion completeness. If that comparison fails, correct the conversion/evidence and retain the exact reproducible result.

## Work log

- 2026-10-12T18:49:42Z Recorded supplemental reproducible hash mismatch from the fresh MVP check. No cause, live score disagreement or user-visible defect claimed; not admitted or launched in this run.

- 2026-10-17T07:10:46Z Admitted after fresh read-only non-author context /root/admit_replay approved the corrected contract at risk3: conversion completeness is a first investigation step, Scratch folded into evidence, dependencies empty and no operator decision open. Admission approves no implementation bytes.
- 2026-10-17T07:12:18Z stage research start — acquired sleep and restart protection; initial alias-relative power-helper paths unavailable, resolved installed playbook path succeeded. Conversion completeness and raw public replay reproduction first.
- 2026-10-17T07:16:27Z stage research end — all 1200 applied input vectors including flow commands equal conversion; starting content regenerated identically from unchanged retained assets. Current-source probe reproduces tick100 live effbd25... versus replay ce6dbbf...; first differing field loose-object rotation 0.01760505 versus 0.017605051. Testing density versus explicit mass at live tuning initialization.
- 2026-10-17T07:17:56Z stage implement start — red boundary regression confirms initial inverse fighter mass 0.16441624 versus unchanged live tuning 0.16441625. Preserve live startup mass arithmetic and use it for construction/stance; no conversion, transport, tuning or linker redesign.
- 2026-10-17T07:20:36Z stage implement end — corrected replay matches all 1200 authority hashes; initial mass boundary test red then green. Added stance parity assertions and admission/cause rationale to decisions. Temporary probe removed from source.
- 2026-10-17T07:20:44Z stage verify start — tools/verify.ps1 required by repository policy for the physics change: format, strict all-target Clippy, locked build and complete tests; existing canonical target, no clean build or linker changes. Ticket-specific raw public replay checks follow attributed client build. Windows sleep/restart protection active on repeat acquire.
- 2026-10-17T07:56:03Z stage verify end — tools/verify.ps1 passed format, strict all-target Clippy, locked build, docs and all 185 executable tests (0 failed/ignored), including mass/stance regression and 43 network tests. Fresh attributed public client matches trace ticks1/99/100/1200; all-ticks probe matches all1200. Raw outputs, source attribution and complete log in TEMP/ivy-ticket095-01a148b3-4215-78f1-b9a5-4ed795348e75. Scope requires no visible UI change; no GUI launched. Destination wording normalized to the already-authorized main for helper parsing.
- 2026-10-17T07:56:32Z stage review start — exact candidate 75b9468fe9ecd2ba1260d469cdbc966c4bdd220f..a85b82d14ce9aa573926c3be6a538446b19c1f77. Fresh Codex CLI review using current preset Sol6.1 high; no task-specific override. Direct main delivery remains authorized. Ivy quota endpoint unavailable; current owning-session rate-limit metadata supplies subscription budget evidence.
- 2026-10-17T08:06:50Z stage review end — fresh CLI reviewer codex:01a148df-cce8-7471-b1cd-7c1d6f1932dd approved exact a85b82d14ce9aa573926c3be6a538446b19c1f77 with no findings; trailer-only tip 4e78679f477e792e42f8e7bbf71e1430876bdcba. Independent public hashes and regression passed; all185-test attribution verified. Before/after review inventories identical (root242938 files, candidate17), seven root local hashes unchanged. Initial launcher refusal for absent artifact parent corrected before any review ran.
- 2026-10-17T08:09:01Z stage verify start — rebased onto 71892464e96485d4e5b76eb457b65cb081bed403; append conflict resolved by preserving ticket94 record and ticket95 record verbatim. New candidate13454ab11c800cc2d4824914abbe222f4ddb4036 awaits fresh review. Full verify rerun; Git converted physics.rs CRLF to LF, so attribution sees one changed source despite identical Git bytes and rebuilds affected workspace units. Canonical target retained, no clean/profile/linker change.
- 2026-10-17T08:27:33Z stage verify end session codex:01a148b3-4215-78f1-b9a5-4ed795348e75 — rebased13454ab full tools/verify.ps1 passed format, strict all-target Clippy, locked build, docs and all185 tests with0failed/ignored. Fresh rebuilt public client again matches1/99/100/1200 authority hashes; current record and log retained as verify-record-rebased.json and verify-rebased.log. Runtime Git bytes unchanged from original approval; both decision records preserved.
- 2026-10-17T08:27:40Z stage review start session codex:01a148b3-4215-78f1-b9a5-4ed795348e75 — fresh exact-range reconciliation review of71892464e96485d4e5b76eb457b65cb081bed403..13454ab11c800cc2d4824914abbe222f4ddb4036 after preserving concurrent ticket94 documentation. No supported findings in first review; new review required by append conflict.
- 2026-10-17T08:39:31Z stage review end session codex:01a148b3-4215-78f1-b9a5-4ed795348e75 — fresh reviewer codex:01a148fb-80c9-7212-a49b-b76afd685846 approved 71892464e96485d4e5b76eb457b65cb081bed403..13454ab11c800cc2d4824914abbe222f4ddb4036 with no findings. Trailer-only approved tip aced3ff113f29e652816681b92a8208df1045697. Candidate artifacts and seven root local hashes unchanged; separate run-usage artifact changed and was left untouched. Policy remains direct main.
- 2026-10-17T08:39:36Z stage verify start session codex:01a148b3-4215-78f1-b9a5-4ed795348e75 — final retarget onto current origin/main, preserving unrelated documentation. Compiler-input timestamps will be restored only after exact byte equality with the verified source is established; full required checks still rerun.
