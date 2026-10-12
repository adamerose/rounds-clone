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
depends-on: [91, 93]
supersedes: []
split-from: []
risk: 3
---

# Keep a single Bevy link from exhausting memory

Each debug link of a Bevy executable in this workspace peaks at 5 to 7.5 GB with MSVC link.exe. When less than about 8 GB is free, a single link still drives available memory below 100 MB and pages Adam's applications out. Switch the workspace's Windows linker to the toolchain's bundled rust-lld if a measurement shows it at least halves that peak, while keeping portable full PDBs, two Cargo jobs and the shared target.

## Outcome

- On x86_64-pc-windows-msvc the workspace links with the pinned toolchain's rust-lld, set by two lines in .cargo/config.toml ([target.x86_64-pc-windows-msvc] linker = "rust-lld.exe"). Every developer command, tools/verify.ps1 and CI use it with no extra installation.
- A tools/verify.ps1 run that relinks every workspace executable over warm dependencies keeps the combined peak working set of its Cargo, compiler and linker processes at or below 3.5 GB, down from the 7.4-7.6 GB that ticket 91 measured with one link.
- Executables still get ordinary full PDBs that resolve function names, source lines and types for workspace and dependency code without the object files in out/cargo-target.
- Full verification passes unchanged, with no check dropped. .cargo/config.toml keeps jobs = 2 and target-dir = out/cargo-target.

## Decisions

- Destination ref: main.
- The only allowed change to compilation is the target linker for x86_64-pc-windows-msvc, set in .cargo/config.toml. Do not change profiles, debuginfo, RUSTFLAGS, link-args, features, dependencies, the job cap or the target directory. Do not use /DEBUG:FASTLINK, which writes PDBs that depend on this machine's object files and which Microsoft has removed in VS2026.
- Measure before changing anything tracked. Gate: on identical warm inputs, rust-lld's peak working set and peak private commit are each at most 3.5 GB and at most 50% of link.exe's, in every alternating run. If the gate fails, change nothing tracked, record the measurements in the work log and docs/decisions.md, and return the ticket to blocked with Adam's question about reducing dependency debuginfo (line-tables-only for dependencies: loses variable and type inspection inside Bevy and needs a cold build of about 17 GB).
- Changing the linker changes every unit's Cargo fingerprint, so the first verify after the change rebuilds every dependency in place within out/cargo-target with two jobs. Before that run, give the AGENTS.md clean-build notice (target path, reason, two-job cap, expected impact, retention), and record the target size before and after.
- Run every measurement and the candidate's first verify in an exclusive window: hold out/cargo-target/quarrel-verify.lock for any raw Cargo command, and record a process inventory before and after showing no other project Cargo, rustc or test processes. Integrate only when no other native worker is mid-verification. In the delivery report and docs/decisions.md, note that worktrees based before the change must rebase onto main before their next verify, or they switch the linker back and rebuild every dependency.
- Update every record the change makes false: the tools/verify.ps1 header's link-memory paragraph, README's note on .cargo/config.toml, and a docs/decisions.md entry recording the choice and the rejected FASTLINK and debuginfo options.

## Evidence required

- Per-link A/B, with no dependency rebuilt: holding the verify lock, relink quarrel-client and the workspace Bevy executable with the highest baseline link peak three times each, alternating link.exe (default) and rust-lld (cargo rustc ... -- -C linker=rust-lld.exe). Report each linker process's PeakWorkingSet64, peak private commit and wall time, and show the gate result. Retain raw output under out/ticket094proof.
- Route before and after on the same source and warm dependencies, using the ticket 91 harness (out/ticket091proof/run.ps1 or a copy under out/ticket094proof). Delete the checkout's out/verify-record.json before each measured run so tools/verify.ps1 relinks every workspace crate. Baseline: link.exe on the pre-change commit. Candidate: rust-lld, measured on a run after the one-time dependency rebuild. Report the combined build-tree peak working set (acceptance: at most 3.5 GB), lowest available memory, seconds under 500 MB, hard-fault reads, number of concurrent linkers (at most 1) and total route time. Report the duration and peak of the one-time dependency rebuild separately; it is preparation, not acceptance.
- Symbol check through System32 dbghelp.dll, for quarrel-client built by link.exe (baseline) and by rust-lld (candidate). Copy the executable and PDB to a scratch directory under out/ticket094proof and set that directory as the only symbol path. Resolve a workspace function and a Bevy function to name plus source file and line, and resolve a workspace type and a Bevy type by name. Every lookup that succeeds on the baseline must succeed on the candidate.
- A complete passing tools/verify.ps1 run on the candidate, with no check skipped, including tests that start the client. This exercises the Steam steam_api64.dll import and the copying of linked DLLs.
- git diff of the candidate shows only .cargo/config.toml's linker table plus the record updates; jobs = 2 and target-dir unchanged.

## Work log

- 2026-10-09T05:10:37Z Filed by ticket 91's worker from its measurements: build peak 13 to 15 GB with two links, 7.4 to 7.6 GB with one; one-link runs that started with 3.9 and 6.6 GB available still fell below 100 MB.
- 2026-10-09T05:51:32Z Bounded independent assessment by Codex /assess_link_memory: installed Rust1.98.1/LLVM22.1.8, VS2022 BuildTools17.14 LINK14.44.35222 supports FASTLINK; official Microsoft /DEBUG docs say machine-dependent PDBs and VS2026 removal. FASTLINK remains unselected because portability loss requires an operator choice. Bundled rust-lld exists but Cargo/linker configuration, compatible reuse and full PDB/debug behavior are unproved; dependency debuginfo changes compilation inputs and needs the clean-build resource notice. No local symbol-resolution debugger was located. Before admission define numeric memory acceptance, exact candidate/environment restoration, current linker support, an available symbol-proof procedure and bounded warm baseline/candidate evidence. No builds, settings, artifacts or processes changed; remains idea, independent of current83 native worker.
- 2026-10-12T07:58:48Z Independent read-only assessment by claude:884eab9c-0fb4-42f9-8b51-f277b66ee8a1 found current contract incomplete; proposed reversible bundled rust-lld with a measured per-link gate, ordinary portable PDB proof, complete verification, explicit artifact rebuild notice, and dependence on closed93. No builds or tracked code changed; shaped at idea for fresh admission review.
