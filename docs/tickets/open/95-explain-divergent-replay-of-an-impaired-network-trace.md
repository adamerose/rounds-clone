---
status: idea
created: 2026-10-12T18:49:42Z
origin: system-detected
tags: [quarrel, replay, network]
value: 5
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
- This is a supplemental finding, outside the required MVP evidence. It remains an idea for a separate replay investigation; live scores agree and no user-visible impact or cause has been established.
- Preserve the existing host-authoritative online model. docs/decisions.md pins starting tuning, cards and arenas for recordings, and rejects peer rollback for this MVP; this observation does not reopen that design.

## Evidence required

- Public replay of out/autonomy75-goal-check-20261012/replay-prefix-99.json returns SHA256 eee5a4e50c333a879eed48b0372991b3c10d147cafc960a9fb5835ff38d7f6bb.
- At the checked main game source, replay-prefix-100.json returns ce6dbbf935327c88784744e6d7160fcc2ceaf01d069c5136307793498d99b4f4, while the retained authority trace expects effbd25b9e37a476436421820d6bc40a0a7f80ac4df6530aa95ce15d9cfe635d. Hash compact raw stdout after stripping only CR/LF; do not reserialize numeric values before hashing.
- Use an attributable current-source quarrel-client.exe through replay --input <fixture>. Preserved MSVC baseline and full attribution are named in out/autonomy75-goal-check-20261012/goal-check.json. Repeated impaired replays produced the same divergent terminal hash; ordinary smoke replay matched live authority.

## Scratch

- Before admission, confirm that the reconstructed recording carries every authoritative input/content event and that comparing these hashes is a supported expectation. Prefix agreement is evidence, not proof of conversion completeness.
- Cold source snapshot: 3304cb8aba0515df64d660e4a108d44b61ca36fd; checked tip d30a304ce8d0fbce8a5ef3d2b4c6e734ee42e20b. All 85 game/data/settings files matched the approved ticket86 source; later changes here were ticket records.
- Fresh non-author finding: codex:01a13154-9da7-7062-9f55-7dc9d98a6df8. Reproduction, authority trace, recording, snapshots and goal report are retained in the ignored proof directory above.

## Work log

- 2026-10-12T18:49:42Z Recorded supplemental reproducible hash mismatch from the fresh MVP check. No cause, live score disagreement or user-visible defect claimed; not admitted or launched in this run.
