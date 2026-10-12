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
risk: 4
---

# Keep a single Bevy link from exhausting memory

A single MSVC debug link of this workspace peaks at 5 to 7.5 GB and can page Adam's applications out when only about 4 GB is free. Test the pinned toolchain's bundled rust-lld before selecting it, keeping ordinary portable debug symbols and complete verification.

## Outcome

- Ordinary Windows developer commands, tools/verify.ps1 and CI use the pinned bundled rust-lld through .cargo/config.toml, with no extra installation or caller PATH change.
- A complete warm verification run that relinks workspace executables keeps the simultaneous build-process working set at or below 3,500,000,000 bytes. Each selected executable's rust-lld lifetime peak working set and private commit are each at most that cap and half the corresponding same-target link.exe baseline.
- Matching executable/PDB pairs preserve portable full symbols: one workspace and one Bevy function resolve with source file and line, and one workspace and one Bevy concrete type resolve with type information.
- Complete verification passes; jobs = 2, the canonical shared out/cargo-target, profiles, debuginfo, dependencies and feature sets remain unchanged.

## Decisions

- Destination: main. Only linker selection may change compilation, through [target.x86_64-pc-windows-msvc] linker = "rust-lld.exe". No FASTLINK, profile/debuginfo changes, RUSTFLAGS, extra link arguments or dependency changes. Update the verify script's linker-memory explanation, README's Cargo configuration note and docs/decisions.md when they become false.
- Begin only after the sole native worker slot is released. Trials use the Git-common-directory-derived shared target, its existing quarrel-verify.lock, the compiler priority used by tools/verify.ps1 and --jobs 1; preserve the repository's two-job cap. Check for overlapping project Cargo, rustc, clippy-driver and linker writers before and after.
- Keep the tracked configuration unchanged until the selected-unit memory gate and baseline symbol preflight pass. Preserve the baseline executable/PDB pair, checkout SHA, compilation settings, attribution-record state and shared target identity before trials. Changing linker configuration changes affected Cargo fingerprints and may rebuild dependencies in place. Before that transition give the AGENTS.md resource notice with the exact target, reason, two-job cap, expected impact and retention; measure target size, reused/rebuilt units and transition duration. Never start a separate clean target.
- On discovery, sampling, memory, symbols or full-verification failure, deliver no linker configuration. Restore pre-trial tracked configuration and scoped environment in finally, and invalidate this checkout's verification attribution record. Preserve reusable trial artifacts; do not delete the target or run a redundant rebuild to roll back. Record the demonstrated failure in the ticket and docs/decisions.md, then block on that reason. Reduced dependency debugging capability remains a separate operator decision; failure does not authorize it.
- Before old worktrees next verify, rebase them onto the delivered configuration so they do not switch the shared target's linker back and rebuild dependencies. Integrate only when no other native writer is verifying.

## Evidence required

- Measure one baseline/candidate pair for quarrel-client and the executable with the highest observed baseline peak only if it is different: at most four selected-unit compilations/links. Record package, target selector (binary/test/example), profile, features and complete commands. Use identical inputs, --locked, --jobs 1 and final-unit arguments -- -C linker=link.exe versus -- -C linker=rust-lld.exe. Retain verbose/JSON Cargo evidence that dependencies were fresh and both linkers ran. Unexpected dependency rebuilding is preparation, not the warm comparison. Prove rust-lld's observed image path belongs to the pinned toolchain; retain raw proof under out/ticket094proof. Repeated trials or a soak require a separate benchmark ticket.
- For every selected linker, retain process handles long enough to capture lifetime PeakWorkingSetSize and PeakPagefileUsage, plus PID, creation time, parent, image path, command and exit status. Both candidate metrics must be <=3,500,000,000 bytes and <=0.50 times their same-target baseline. For the supported warm route, sample at a fixed interval <=100 ms and compute the maximum simultaneous sum of current WorkingSetSize for that route's Cargo/compiler/linker descendants, including clippy-driver and rust-lld and excluding test/game processes. Report summed current private commit too. Do not sum individual lifetime peaks as the tree peak. Report system available-memory minimum, time under 500,000,000 bytes, page reads, linker concurrency (at most one) and route time as context. Missing acceptance metrics or incomplete process capture leave the gate unproved.
- Before the global configuration switch, preflight a headless 64-bit probe using installed System32 dbghelp.dll and record its version/API errors. Record four successful baseline queries: workspace and Bevy functions with source lines, and workspace and Bevy concrete types with type information; require all four on the candidate. Partial or zero baseline success is unavailable evidence. Find the matching PDB from the executable's CodeView record, copy each pair to its own scratch directory, ignore embedded original CodeView paths with SYMOPT_IGNORE_CVREC, suppress ambient symbol/server searches and explicitly load the copied module. Record LoadedPdbName and matching PDB identity to prove the copied PDB was used. Retain the actual full-PDB linker invocation (no FASTLINK), and function/line/type queries through SymEnumSymbols/SymGetLineFromAddr64 and SymEnumTypes/SymGetTypeInfo. If installed DbgHelp cannot establish this proof, stop without installing a debugger or changing debuginfo.
- Use one measured complete warm candidate tools/verify.ps1 run as both route memory proof and full functional verification, with its normal checked-in linker configuration, no per-unit overrides or PATH changes, and captured bundled linker image. Invalidate this checkout's attribution record first so workspace executables actually relink. If the first changed-config run rebuilds dependencies, record it as preparation and perform at most one subsequent warm candidate route. Reuse the selected-unit baseline instead of requiring another complete baseline route or another duplicate candidate verification. Check CI's pinned toolchain/configuration, successful Steam DLL imports and client-start tests; diff confirms only the linker table and necessary record updates.

## Work log

- 2026-10-09T05:10:37Z Filed by ticket 91's worker from its measurements: build peak 13 to 15 GB with two links, 7.4 to 7.6 GB with one; one-link runs that started with 3.9 and 6.6 GB available still fell below 100 MB.
- 2026-10-09T05:51:32Z Bounded independent assessment by Codex /assess_link_memory: installed Rust1.98.1/LLVM22.1.8, VS2022 BuildTools17.14 LINK14.44.35222 supports FASTLINK; official Microsoft /DEBUG docs say machine-dependent PDBs and VS2026 removal. FASTLINK remains unselected because portability loss requires an operator choice. Bundled rust-lld exists but Cargo/linker configuration, compatible reuse and full PDB/debug behavior are unproved; dependency debuginfo changes compilation inputs and needs the clean-build resource notice. No local symbol-resolution debugger was located. Before admission define numeric memory acceptance, exact candidate/environment restoration, current linker support, an available symbol-proof procedure and bounded warm baseline/candidate evidence. No builds, settings, artifacts or processes changed; remains idea, independent of current83 native worker.
- 2026-10-12T07:58:48Z Independent read-only assessment by claude:884eab9c-0fb4-42f9-8b51-f277b66ee8a1 found current contract incomplete; proposed reversible bundled rust-lld with a measured per-link gate, ordinary portable PDB proof, complete verification, explicit artifact rebuild notice, and dependence on closed93. No builds or tracked code changed; shaped at idea for fresh admission review.
- 2026-10-12T08:08:29Z Fresh Codex admission review codex:01a12f1f-8c24-7e33-8230-4f083dc53537 requested six corrections at risk4. Shaping removes twelve redundant large links and duplicate routes, specifies comparable selected-unit commands and attributable memory peaks, prevents false symbol portability passes, and defines failure restoration and actual configuration-driven proof. No operator decision open; remains idea pending review of corrected blob.
