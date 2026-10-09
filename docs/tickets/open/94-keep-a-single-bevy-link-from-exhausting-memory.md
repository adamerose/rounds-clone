---
status: idea
created: 2026-10-09T05:10:37Z
origin: agent-proposed
tags: ["quarrel", "build", "performance"]
value: 7
sessions:
  - claude:5d7961b5-7082-42f9-9e88-f4b260740f6f
execution: unattended
parent: 75
depends-on: [91]
supersedes: []
split-from: []
---

# Keep a single Bevy link from exhausting memory

Each debug link of a Bevy executable in this workspace peaks at 5 to 7.5 GB of memory. Ticket 91 stopped two links running at once, but when less than about 8 GB is free a single link still drives available memory below 100 MB and pages Adam's applications out, which is the lag he reported.

## Outcome

- A verification run on the warm target keeps the build's peak memory well below 7.5 GB, so a single link no longer exhausts memory on a machine with about 4 GB free, with no check dropped.

## Decisions

- Undecided. Candidates, cheapest to reverse first: append `/DEBUG:FASTLINK` through MSVC's `_LINK_` environment variable in `tools/verify.ps1` (no compiler artifact changes, so no cold rebuild, but PDBs then reference object files in the shared target); use `rust-lld`; reduce debuginfo for dependencies (changes the profile, so it needs the cold-build notice in AGENTS.md, about 17 GB). Each changes linker or profile settings, which ticket 91 forbade; admission must record which is allowed.

## Evidence required

- The ticket 91 harness (retained under `out/ticket091proof` in the main checkout) run on the same warm source before and after, reporting peak build working set, lowest available memory, seconds under 500 MB and hard-fault reads, plus a passing complete `tools/verify.ps1` run and a debugger or crash-dump check that symbols still resolve if PDB contents change.

## Work log

- 2026-10-09T05:10:37Z Filed by ticket 91's worker from its measurements: build peak 13 to 15 GB with two links, 7.4 to 7.6 GB with one; one-link runs that started with 3.9 and 6.6 GB available still fell below 100 MB.