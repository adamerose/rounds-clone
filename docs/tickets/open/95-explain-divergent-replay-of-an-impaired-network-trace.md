---
status: ready
created: 2026-10-12T18:49:42Z
origin: system-detected
tags: [quarrel, replay, network]
value: 5
risk: 3
sessions:
  - codex:01a12f11-0715-7b71-b569-474dc68c330e
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

- Destination for any future delivery: main.
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
