# Orphaned project progress inventory — 2026-08-28

This inventory preserves eight frozen artifact paths without making any of them authoritative.
Authoritative project content remains commit `c24ed0a88c2bff843e788e1957502d9b86bc3d25`, the end of integrated ticket 008.
The admitted recovery ticket and its decision record advance `main` to `44aa98d21441248d2cc2ef152c1b5ce82db28e95`, but they do not integrate any orphaned project result.
All artifact reads were headless and bounded to the ticket's allowlist, except the separately identified read-only Git-object and `.git-index` structure probes.
No artifact, pre-existing ref, registration, credential value, or external service was changed.
No command enumerated, printed, hashed, encoded, or tested an environment-variable or credential value.

## How to read the classifications

- `recoverable-reviewed-history` means an exact Git commit chain and its review chronology survive; it does not mean that the chain passed final review or may be integrated without a new ticket and fresh exact review.
- `recoverable-evidence-only` means useful bytes or records survive, but the exact reviewed commit is absent, the working tree is dirty, or provenance is otherwise incomplete.
- `duplicate` means an identical ticket byte variant occurs in more than one artifact; every occurrence remains listed because its artifact identity still matters.
- `superseded` means a later orphan ticket replaced the earlier plan; recover the later outcome and retain the earlier record only as rationale.
- `blocked-external-action` means repository work cannot perform the required authenticated provider action.
- `discardable-residue` requires proof that an artifact contains no unique recoverable content.
No frozen artifact qualifies as `discardable-residue` on the available evidence.

## Read-only baseline

The first content-independent probe ran before any artifact file was opened.
It recorded the detached admission base, the clean candidate boundary, every ref and worktree registration, and top-level metadata for the eight exact frozen roots.

| Baseline item | Value |
|---|---|
| Candidate detached `HEAD` | `44aa98d21441248d2cc2ef152c1b5ce82db28e95` |
| Admission base | `44aa98d21441248d2cc2ef152c1b5ce82db28e95` |
| Authoritative project-content base | `c24ed0a88c2bff843e788e1957502d9b86bc3d25` |
| Candidate tracked/untracked boundary | clean |
| Ref `HEAD` | `44aa98d21441248d2cc2ef152c1b5ce82db28e95` |
| Ref `refs/heads/main` | `44aa98d21441248d2cc2ef152c1b5ce82db28e95` |
| Ref `refs/codex/turn-diffs/captures/1787934243554/6b4e42d2-b73c-4a16-949a-f7d249634c05/base` | `62697d8a2d969a9aba86428ceb8747448c28e572` |

The root-repository Git commands needed a per-command `safe.directory` override because the sandbox and filesystem owner accounts differ.
No global or repository Git configuration changed.

| Frozen path | Created UTC | Last write UTC before content inspection | Root kind |
|---|---:|---:|---|
| `.ivy/worktrees/009-projectile-cards` | `2026-08-14T18:12:38.3822161Z` | `2026-08-14T18:37:11.2711572Z` | directory |
| `.ivy/worktrees/010-volley-projectiles` | `2026-08-14T20:34:30.4518476Z` | `2026-08-14T20:34:30.6640905Z` | directory |
| `.ivy/worktrees/011-radial-saw-maps` | `2026-08-14T20:40:04.0882756Z` | `2026-08-14T20:40:04.2734831Z` | directory |
| `.ivy/worktrees/013-dynamic-arena` | `2026-08-15T20:01:35.8037184Z` | `2026-08-15T22:48:24.4315331Z` | directory |
| `.ivy/worktrees/014-content-roadmap` | `2026-08-15T23:39:46.0013123Z` | `2026-08-16T05:03:28.4727055Z` | directory |
| `.ivy/worktrees/015-projectile-damage-scale` | `2026-08-16T17:57:02.7577514Z` | `2026-08-16T18:03:11.3876631Z` | directory |
| `.ivy/worktrees/022-controller-support` | `2026-08-16T18:03:50.3960028Z` | `2026-08-16T19:04:19.5334165Z` | directory |
| `.ivy/worktrees/029-passive-auras` | `2026-08-17T03:40:34.2107988Z` | `2026-08-17T03:57:02.7330440Z` | directory |

The baseline worktree registrations were the root `main` worktree, registered detached orphan worktrees 009, 010, and 011, plus this ticket's excluded delivery worktree.

| Registered path | Head | State |
|---|---|---|
| `C:/_MyFiles/Programming/Projects/rounds-clone` | `44aa98d21441248d2cc2ef152c1b5ce82db28e95` | `refs/heads/main` |
| `C:/_MyFiles/Programming/Projects/rounds-clone/.ivy/worktrees/009-projectile-cards` | `4ce6038d83cd5fbdc7c0b988e0a9ba8f57895047` | detached |
| `C:/_MyFiles/Programming/Projects/rounds-clone/.ivy/worktrees/010-volley-projectiles` | `0d1d75600c42d874c29f0bf20a15001ce684ac49` | detached |
| `C:/_MyFiles/Programming/Projects/rounds-clone/.ivy/worktrees/011-radial-saw-maps` | `c22bd4b08050d1b954ff66eb45ef86821c3b0975` | detached |
| `C:/_MyFiles/Programming/Projects/rounds-clone/.ivy/worktrees/013-make-orphaned-project-progress-safely-recoverable` | `44aa98d21441248d2cc2ef152c1b5ce82db28e95` | detached, excluded from artifact input |

## Deterministic content manifests

Run the bounded helper from any PowerShell working directory with explicit paths:

```powershell
& 'C:\_MyFiles\Programming\Projects\rounds-clone\.ivy\worktrees\013-make-orphaned-project-progress-safely-recoverable\tools\recovery\inventory-orphaned-progress.ps1' `
  -RepositoryRoot 'C:\_MyFiles\Programming\Projects\rounds-clone' `
  -OutputDirectory 'C:\_MyFiles\Programming\Projects\rounds-clone\.ivy\worktrees\013-make-orphaned-project-progress-safely-recoverable\docs\recovery\orphaned-progress-2026-08-28-manifests'
```

Each manifest is UTF-8 without a BOM, uses LF endings, ends with one LF, and is sorted by the ordinal byte order of each forward-slash UTF-8 path.
Each line is `<relative-path>\t<byte-length>\t<lowercase-SHA-256-of-raw-file-bytes>`.
The artifact digest is the SHA-256 of the complete manifest bytes.
The helper includes every regular file under the admitted roots and top-level names, skips every reparse point, and never enters `.git`, `.git-index`, `.tools`, `.tmp`, `.godot`, `bin`, `obj`, or `research/raw`.

| Artifact | Registered | Head and relation to `c24ed0a` | Working bytes | Manifest | Files | Artifact digest | Classification |
|---|---|---|---|---|---:|---|---|
| `.ivy/worktrees/009-projectile-cards` | yes | `4ce6038d83cd5fbdc7c0b988e0a9ba8f57895047`; merge base `c24ed0a`; 0 base-only, 13 artifact-only commits | clean | `009-projectile-cards.manifest.tsv` | 130 | `131ee1c4a1867f829cabb2c9d0085c43e814c16814b077080db6a8d2d5cecea8` | `recoverable-reviewed-history` |
| `.ivy/worktrees/010-volley-projectiles` | yes | `0d1d75600c42d874c29f0bf20a15001ce684ac49`; merge base `c24ed0a`; 0 base-only, 13 artifact-only commits | 0 staged, 2 unstaged, 2 untracked | `010-volley-projectiles.manifest.tsv` | 133 | `352a020408472a4ab8600fb98d81f5c67303fe4a0987d31951b3797346897317` | `recoverable-evidence-only` |
| `.ivy/worktrees/011-radial-saw-maps` | yes | `c22bd4b08050d1b954ff66eb45ef86821c3b0975`; merge base `c24ed0a`; 0 base-only, 3 artifact-only commits | 0 staged, 30 unstaged, 20 untracked | `011-radial-saw-maps.manifest.tsv` | 141 | `bf61a8a5499f3d7937ed0c1dca7f9a078879edce6b912e9f7890760a4c2add9f` | `recoverable-evidence-only` |
| `.ivy/worktrees/013-dynamic-arena` | no | no repository control file; recorded approved commit is missing | operational index: 146 matching, 7 different, 0 manifest-only | `013-dynamic-arena.manifest.tsv` | 153 | `6c1c40a461b992f6c087e2b763fea058ab29f1bd6012248fd79ec5facd6e9cdd` | `recoverable-evidence-only` |
| `.ivy/worktrees/014-content-roadmap` | no | no repository control file; recorded commits are missing | operational index: 130 matching, 16 different, 10 manifest-only | `014-content-roadmap.manifest.tsv` | 156 | `0eb6717d882120c880a901bd9ee72254be447c22d9710013256803923ffb96f4` | `recoverable-evidence-only` |
| `.ivy/worktrees/015-projectile-damage-scale` | no | no repository control file; recorded commits are missing | operational index: all 147 manifest files match | `015-projectile-damage-scale.manifest.tsv` | 147 | `857bc3c5288b325cd9ec87655d919d1440ecb215b092cd29d03ccf4641c4edf6` | `recoverable-evidence-only` |
| `.ivy/worktrees/022-controller-support` | no | no repository control file; recorded commits are missing | operational index: 161 matching, 5 different, 0 manifest-only | `022-controller-support.manifest.tsv` | 166 | `51166b26626a5ad27b57dd8a99d80f1a82ee9b3a1d8f89a27a692872146cb1c8` | `recoverable-evidence-only` |
| `.ivy/worktrees/029-passive-auras` | no | no repository control file; recorded commits are missing | operational index: 183 matching, 1 different, 1 manifest-only | `029-passive-auras.manifest.tsv` | 185 | `6c2de57125edd21a7290d5a7dd01578b603396da02de3cb19b4dd311f0a4bb2f` | `recoverable-evidence-only` |

The manifests are in [`orphaned-progress-2026-08-28-manifests/`](orphaned-progress-2026-08-28-manifests/).

## Registered working-tree state

The status probe used porcelain-v1 path quoting and `--untracked-files=all` under each exact registered worktree.
Every present allowlisted file below has a raw-byte digest in its artifact manifest.
The deleted open ticket path in worktree 011 has no current bytes; its untracked closed replacement does.

| Artifact | XY | Path | Manifest bytes present |
|---|---|---|---|
| 010 | ` M` | `docs/design-docs/postmortems.md` | yes |
| 010 | ` M` | `docs/tickets/open/010-implement-remaining-bounce-cards.md` | yes |
| 010 | `??` | `docs/tickets/closed/011-implement-radial-saw-maps.md` | yes |
| 010 | `??` | `docs/tickets/open/012-implement-volley-projectile-cards.md` | yes |
| 011 | ` M` | `AGENTS.md` | yes |
| 011 | ` M` | `GOAL.md` | yes |
| 011 | ` M` | `README.md` | yes |
| 011 | ` M` | `Rounds.sln` | yes |
| 011 | ` M` | `docs/architecture.md` | yes |
| 011 | ` M` | `docs/decisions.md` | yes |
| 011 | ` M` | `docs/design-docs/postmortems.md` | yes |
| 011 | ` M` | `docs/design/physics-and-maps.md` | yes |
| 011 | ` M` | `docs/progress/2026-08-14.md` | yes |
| 011 | ` D` | `docs/tickets/open/011-implement-radial-saw-maps.md` | no |
| 011 | ` M` | `game/Main.cs` | yes |
| 011 | ` M` | `game/project.godot` | yes |
| 011 | ` M` | `src/Rounds.Sim.Tests/ArenaCatalogTests.cs` | yes |
| 011 | ` M` | `src/Rounds.Sim.Tests/CollisionTests.cs` | yes |
| 011 | ` M` | `src/Rounds.Sim.Tests/CombatTests.cs` | yes |
| 011 | ` M` | `src/Rounds.Sim.Tests/MatchTests.cs` | yes |
| 011 | ` M` | `src/Rounds.Sim.Tests/MovementTests.cs` | yes |
| 011 | ` M` | `src/Rounds.Sim.Tests/Rounds.Sim.Tests.csproj` | yes |
| 011 | ` M` | `src/Rounds.Sim.Tests/StatCardTests.cs` | yes |
| 011 | ` M` | `src/Rounds.Sim/Cards/StatCardCatalog.cs` | yes |
| 011 | ` M` | `src/Rounds.Sim/Cards/StatCardDefinition.cs` | yes |
| 011 | ` M` | `src/Rounds.Sim/CombatController.cs` | yes |
| 011 | ` M` | `src/Rounds.Sim/Maps/ArenaCatalog.cs` | yes |
| 011 | ` M` | `src/Rounds.Sim/Maps/ArenaDefinition.cs` | yes |
| 011 | ` M` | `src/Rounds.Sim/Match.cs` | yes |
| 011 | ` M` | `src/Rounds.Sim/Physics/Collision.cs` | yes |
| 011 | ` M` | `src/Rounds.Sim/Physics/KinematicController.cs` | yes |
| 011 | ` M` | `src/Rounds.Sim/PlayerCombatProfile.cs` | yes |
| 011 | ` M` | `src/Rounds.Sim/Sim.cs` | yes |
| 011 | ` M` | `tools/checks/run.ps1` | yes |
| 011 | `??` | `docs/tickets/closed/011-implement-radial-saw-maps.md` | yes |
| 011 | `??` | `docs/tickets/open/009-implement-ricochet-projectile-cards.md` | yes |
| 011 | `??` | `game/NativeEvidenceDriver.cs` | yes |
| 011 | `??` | `game/NativeEvidenceDriver.cs.uid` | yes |
| 011 | `??` | `game/NativeLaunchPolicy.cs` | yes |
| 011 | `??` | `game/NativeLaunchPolicy.cs.uid` | yes |
| 011 | `??` | `src/Rounds.Sim.Tests/NativeEvidenceTests.cs` | yes |
| 011 | `??` | `src/Rounds.Sim.Tests/ProjectileCardTests.cs` | yes |
| 011 | `??` | `src/Rounds.Sim/Maps/RadialSaw.cs` | yes |
| 011 | `??` | `src/Rounds.Sim/Properties/AssemblyInfo.cs` | yes |
| 011 | `??` | `tools/Rounds.NativeProbe.Tests/CursorGuardStateTests.cs` | yes |
| 011 | `??` | `tools/Rounds.NativeProbe.Tests/Rounds.NativeProbe.Tests.csproj` | yes |
| 011 | `??` | `tools/Rounds.NativeProbe.Tests/packages.lock.json` | yes |
| 011 | `??` | `tools/Rounds.NativeProbe/CursorGuardState.cs` | yes |
| 011 | `??` | `tools/Rounds.NativeProbe/Program.cs` | yes |
| 011 | `??` | `tools/Rounds.NativeProbe/Properties/AssemblyInfo.cs` | yes |
| 011 | `??` | `tools/Rounds.NativeProbe/Rounds.NativeProbe.csproj` | yes |
| 011 | `??` | `tools/Rounds.NativeProbe/packages.lock.json` | yes |
| 011 | `??` | `tools/check-native-no-focus.ps1` | yes |
| 011 | `??` | `tools/checks/check-hazard-presentation.ps1` | yes |

Worktree 009 is clean.
No registered orphan worktree has a staged change.

## Operational index and surviving-object correlation

The five unregistered roots have no `.git` control file and each retains one `.git-index` file.
The structure probe read each index with the shared repository object database but did not refresh or rewrite it.
For every manifest path also present at index stage 0, the probe compared the index blob ID with a read-only Git blob hash of the current raw file bytes.
It did not read current bytes outside the allowlist.

| Artifact | `.git-index` bytes | Index SHA-256, separate from content digest | Total stage-0 entries | Unique indexed blobs surviving / missing |
|---|---:|---|---:|---:|
| 013 | 16,554 | `60cb07a88b99712a802c229e6f24f0026098ec45880a6ee5b1ee39b9f893585e` | 153 | 99 / 52 |
| 014 | 15,800 | `cc925f68dc9e04613ccf85fe27804e6cd466587b28a23cba3b246887690919a4` | 146 | 88 / 57 |
| 015 | 15,928 | `f8cbd831e6a9fea5ea3cba73c7e25d9085806209eace96d7c60967aa613ab7f0` | 147 | 88 / 58 |
| 022 | 18,088 | `9e9077a921985b96074c7954c59c44f87843561a43bc5140e85037497e149e96` | 166 | 85 / 79 |
| 029 | 20,144 | `40c8f0bae1b82cb77b40acf0b035ab5e91614045a85388a53427f98abef37ddf` | 184 | 80 / 102 |

No index has an allowlisted stage-0 path absent from the current manifest.
The current bytes that differ from their index, plus current manifest-only paths, are:

- 013 different: `docs/design-docs/postmortems.md`, `docs/tickets/open/009-implement-ricochet-projectile-cards.md`, `tools/Rounds.NativeProbe.Helper/packages.lock.json`, `tools/Rounds.NativeProbe.Tests/NativeBoundaryTests.cs`, `tools/Rounds.NativeProbe.Tests/packages.lock.json`, `tools/Rounds.NativeProbe/NativeDesktop.cs`, and `tools/Rounds.NativeProbe/packages.lock.json`.
- 014 different: `Rounds.sln`, `docs/decisions.md`, `docs/design-docs/postmortems.md`, `docs/tickets/closed/019-correct-production-camera-span.md`, `game/Rounds.Game.csproj`, `game/packages.lock.json`, `src/Rounds.Harness/Program.cs`, `src/Rounds.Harness/Rounds.Harness.csproj`, `src/Rounds.Harness/packages.lock.json`, `src/Rounds.Replay/packages.lock.json`, `src/Rounds.Sim.Tests/Rounds.Sim.Tests.csproj`, `src/Rounds.Sim.Tests/packages.lock.json`, `src/Rounds.Sim/World.cs`, `src/Rounds.Sim/packages.lock.json`, `tools/Rounds.Checks.Tests/packages.lock.json`, and `tools/Rounds.Checks/packages.lock.json`.
- 014 manifest-only: `docs/tickets/open/020-add-deterministic-automated-opponent.md`, `src/Rounds.Bot/BotController.cs`, `src/Rounds.Bot/BotModel.cs`, `src/Rounds.Bot/BotObservationFactory.cs`, `src/Rounds.Bot/BotRunner.cs`, `src/Rounds.Bot/BotSession.cs`, `src/Rounds.Bot/BotStableHash64.cs`, `src/Rounds.Bot/Rounds.Bot.csproj`, `src/Rounds.Bot/packages.lock.json`, and `src/Rounds.Sim/Maps/SpawnResolver.cs`.
- 015 has no difference and no manifest-only path; all 147 current manifest files match its index entries exactly.
- 022 different: `game/packages.lock.json`, `src/Rounds.Input.Tests/packages.lock.json`, `src/Rounds.Input/packages.lock.json`, `src/Rounds.Replay/packages.lock.json`, and `src/Rounds.Sim/packages.lock.json`.
- 029 different: `docs/design-docs/postmortems.md`.
- 029 manifest-only: `docs/tickets/open/029-integrate-passive-proximity-auras.md`.

An exact index match proves that a snapshot agrees with its captured staging model, not that its missing commit was reviewed or integrated.
Partial survival of indexed blobs strengthens byte provenance but cannot recreate absent commit parentage, message, signatures, review range, or tree identity.

## Recorded commit claims

Every isolated 40-hex Git-object claim in the ticket variants was probed by exact object name.
Objects described as missing did not resolve in the shared repository object database on 2026-08-28.

| Logical ticket | Surviving commit objects | Missing recorded commit objects |
|---|---|---|
| 009 | `29b3027819450f095d71b8ef99e79497373d1734`, `3072bface31bfd5457c2014537fa387e773ffac4`, `56d94f468701840966b783230cbe577ed8fc1792`, `ade3d68fc7b3dbc34b52bce1013769efa9256746`, `f7874a6f3c831f5514e6b4ab9a2086556f2ee260` | `4b8ca8ae2df5e619d6f139cae4c87385925f579e`, `c54eff69edf1c5b2026960693b1436e80dc75eae` |
| 011 | `c22bd4b08050d1b954ff66eb45ef86821c3b0975`, `ee4b62104a1a6b94cf929454a3ab77ee0ef2b892` | `11d8911a24478288b77bd317f929f752245b981b`, `31a44a591a3a169af944f3a09684eb9e08795b71`, `3e665bd9c1614f69dd82516a9a4ade74ea79e16e`, `90bd327ac1824291d2af7fe9bbe6be7a97a6a1ba` |
| 013 | none | `77594f7e1026d66f22766dfaa185b82a6b353d35` |
| 014 | none | `39bd1fde714fa1c84f7e48a813c9b9e737a86887`, `40d94ea8c6dab2e47563bdb8089723cfedabdf7d`, `77594f7e1026d66f22766dfaa185b82a6b353d35` |
| 015 | none | `40d94ea8c6dab2e47563bdb8089723cfedabdf7d` |
| 017 | none | `40d94ea8c6dab2e47563bdb8089723cfedabdf7d`, `539ab78ade33c1fa617907266260118559b9ac4b` |
| 018 | none | `539ab78ade33c1fa617907266260118559b9ac4b`, `85f4478197ae1c92d7caeea6529025c544093d7b`, `c07451a303ba40cfbd795c71cc68689b22eacda0`, `deff7be46c212e19f29d492baea4bf31dc092646` |
| 019 | none | `0d36108aa6993764e1f68791cd93797af90303bf`, `2fe9544d8ef97b450de262397d35700a274112d4`, `731c8ba19aa5d6a7d6f9097c1d78070a8136ef82` |
| 022 | none | `09cfaa3a73dc255a5d3ecd042f4b7c6743dfd185`, `8656bd3f3c1f037aec2e70f127f98da18b63ab7a`, `9b1b3e5cf3976628c739513a4042a96082d949a9` |
| 023 | none | `a1633c90cc892c60a6d8f473631cbbca3d8eb5d5`, `cb1f983c2be04869df28d718611130ca81bc5bd0` |
| 024 | none | `a2d996e57dbd552275afab019e651e385ea42b12` |
| 025 | none | `2641f1139afdf42a0a7090f973f5742d13562d09`, `97e745d441868e0a57caea69c50de23cd2d2bb62`, `9e32b19f65fbea02f361384fcbc84499ee09e2cf`, `c7cd48443b73eb880abd007d766a209ef8671948` |
| 026 | none | `97a4bd519de3809a9715e90f1452d2a787bc4495`, `c2bd74b41c1b5e1660d8118219cedb4a8b963af2`, `c3d34e8e46a2a3fee48cd7ddd05bf3058c3d4cef` |
| 027 | none | `a935e96a0d96208898704868f1b939696845f256`, `acbb1d309697b1a1973601a9d95240909a9667da`, `f13db2e7e4c0d0f3a16fa58e4266571aebffbabb` |
| 028 | none | `0f95f0cba9a2928146c5ce94d12d84cb66de075d`, `3bf205e394dfc2876e4029d821929fb47118511f`, `a5177940efc18ebfd5d57b87906910712a2692b2`, `bfaf83ebb02f60e90204b31518c67fab6fa74958` |

Tickets 010, 012, 016, 020, 021, and 029 contain no isolated 40-hex Git-object claim.
Ticket 024's blocked record was preserved without reading, printing, searching for, hashing, encoding, or testing any credential or environment-variable value.

## Logical ticket outcomes and byte variants

Lifecycle labels below are claims from the frozen bytes, not authority.
Repeated artifacts with the same SHA-256 are `duplicate` occurrences of one byte variant, not independent reviews.

| Ticket | Observed byte variants and lifecycle claims | Outcome classification | Next owner or action |
|---:|---|---|---|
| 009 | `69e08d0bfe08d86f2d4ef019fffbf473e4f810d66125757cca6983a9a2be1888` ready; `c237efb42b05ddf6396fb7eacd4343ff4e5edac9bc7831a84df76cb5963a5947` ready; `f2b854a1cd8d9d28893523648dcecb1ec8e168f7053196ab3f5d91a239281053` ready | `recoverable-reviewed-history` | A new recovery ticket must review the exact registered chain from `c24ed0a` to a selected candidate; the latest ticket record includes rejected native-evidence reviews, so do not treat the clean head as approved. |
| 010 | `528aad2190dc613b42f40d0b8d1908245935264ad0dbc8646af41fe4be8f1e69` idea | `superseded` by ticket 026 | Retain its rebound rationale while recovering ticket 026; do not replay it as a separate implementation. |
| 011 | `1b6417c5f9d0e3ffc15ab14cb2be57ee22cff365a8559f862f521ecdd6ada034` closed; `592994ffb13e24b2042d42f0dd9320ef32127797add7694a471dea4034210721` closed | `recoverable-evidence-only` | Reconstruct from the registered design commits plus the 50 dirty paths, then compare with duplicate later snapshots and run fresh exact review. |
| 012 | `b286182e39c388ba1bab963a850740237a32dee46443a33beda51daeba92adf8` idea | `superseded` by ticket 025 | Retain as early planning only. |
| 013 | `0a297f14a49b0e4c977359c287ae09c6f58a3d4a39c333f96e26285d7141dbee` closed; `a2703f080e535a94b48d72f9f343e385451e6c81ece7b3ceb74a432aeca6a639` closed | `recoverable-evidence-only` | Recover moving-platform behavior in a new non-colliding ticket from snapshot/index evidence; the recorded approved commit is missing. |
| 014 | `a9461be183f31ade9499627bb5290a4410852db41414a75ca680e77d814f2ff6` closed | `recoverable-evidence-only` | Reconcile the roadmap/checker bytes only after earlier feature provenance is separated. |
| 015 | `7bf19d604095792b585a24dca3570078e24a9bfb1ef22111a2e0466fa4ddeec4` ready; `126f087790351e5f4f6fef776e3b4ae5287ccac9b365821f80edee5b78475927` blocked; `c3cf22f728368dcd94b1af93dfb8b38f6f5af4e0400e24297651ad139798f369` blocked | `recoverable-evidence-only`, still externally blocked | Preserve the research contract and resume only with its stated original-capture or safe-isolation prerequisite and fresh admission. |
| 016 | `646b79c496151288d3930d39908b72db7e0fce2db59010b3196fb7f903490438` idea | `recoverable-evidence-only` | Re-shape only when collision-scale evidence becomes available. |
| 017 | `69f7afacb26c8d9352a06338ab5eb52142932238fac8ece4773433ae54a174d4` closed | `recoverable-evidence-only` | Reconstruct exact-name presentation changes from snapshot bytes and independently review them. |
| 018 | `7244a7c2c0c11e94a7cf305333a695f6bec32d968190f65878479971e6321843` closed | `recoverable-evidence-only` | Recover the measurement harness as its own exact patch after prerequisite history is selected. |
| 019 | `0ef800d0fdc9c18601a33c3c0387f39a85bde2ab8e25055832e3ec3ab64b4cec` closed; `bcdf4a25dab73c4622406a3d77c0f0338df391afcfd756bd64dbbc527bfa30c6` closed | `recoverable-evidence-only` | Prefer the later variant, but prove its camera bytes against the index and run new review because every recorded commit is missing. |
| 020 | `4c95ccfc30f337130fd28290fceb26ee7de9598b9fb6a0d4c56f0497e748307f` blocked | `recoverable-evidence-only` | Preserve the 10 manifest-only bot/spawn files in artifact 014; static-map fidelity owns the stated prerequisite before bot work resumes. |
| 021 | `8fea39d1e9eabdab938abc5cad21e4463270daaf44a46def17066b1acac111b1` blocked | `recoverable-evidence-only` | Preserve the safety finding; resume only with a real filesystem-isolation boundary and never weaken it into after-the-fact monitoring. |
| 022 | `f5cd14ad24d8c76399bca33572544fba61d24a48bb4c79e505800ac5fbd47a72` closed | `recoverable-evidence-only` | Reconstruct controller input from artifact 022 and verify the five lock-file differences before fresh review. |
| 023 | `4619d41078dd6cc1ee56902f210810981866cebf6ace9192add4e133b9920d5e` closed | `recoverable-evidence-only` | Recover the projectile-card foundation before tickets 025–028, using artifact 029 only as cumulative evidence. |
| 024 | `2a31a8b93d39e8c7071de998fb8a71bb40ab8944fbf2c64e2bdf5f9730d9dc72` blocked | `blocked-external-action` | The authenticated owner must revoke and replace the integration credential through the provider; repository recovery must not inspect or replay the old value. |
| 025 | `d9a02412ad7f2b74e4be6ab67c30ec311c5f3fab7898f3d683d90dce2a588962` closed | `recoverable-evidence-only` | Recover after ticket 023 and independently verify volley mechanics. |
| 026 | `47eb57808a8cb3c146e5c6180c61fcfdb120bcfb256bd794e259bb2979173368` closed | `recoverable-evidence-only` | Recover after ticket 023; this is the successor to ticket 010. |
| 027 | `f3ef196ad2a1835f3686e3fa946da91a1784063763e57677c0d4b64ea71b53d1` closed | `recoverable-evidence-only` | Recover body-impact behavior only after its projectile-card prerequisites. |
| 028 | `c0040129428938f7def7b4585220d2e467eaed9483103815a0b21408d46402b2` closed | `recoverable-evidence-only` | Recover passive flight after ticket 023 and before considering ticket 029. |
| 029 | `5f1f0e3f6513355a5caa55d5d0b3f7f9bdd9b92ab07dcad5812464ccafd1248c` idea | `recoverable-evidence-only` | Treat as future planning; the file is manifest-only relative to artifact 029's index and contains no implementation-stage claim. |

## Every ticket occurrence

This table preserves occurrence identity by artifact and relative path, variant identity by raw-byte SHA-256, and lifecycle claim from that exact occurrence.

| Artifact | Relative path | Byte variant SHA-256 | Claim |
|---|---|---|---|
| 009 | `docs/tickets/open/009-implement-ricochet-projectile-cards.md` | `69e08d0bfe08d86f2d4ef019fffbf473e4f810d66125757cca6983a9a2be1888` | ready |
| 010 | `docs/tickets/closed/011-implement-radial-saw-maps.md` | `1b6417c5f9d0e3ffc15ab14cb2be57ee22cff365a8559f862f521ecdd6ada034` | closed |
| 010 | `docs/tickets/open/009-implement-ricochet-projectile-cards.md` | `69e08d0bfe08d86f2d4ef019fffbf473e4f810d66125757cca6983a9a2be1888` | ready |
| 010 | `docs/tickets/open/010-implement-remaining-bounce-cards.md` | `528aad2190dc613b42f40d0b8d1908245935264ad0dbc8646af41fe4be8f1e69` | idea |
| 010 | `docs/tickets/open/012-implement-volley-projectile-cards.md` | `b286182e39c388ba1bab963a850740237a32dee46443a33beda51daeba92adf8` | idea |
| 011 | `docs/tickets/closed/011-implement-radial-saw-maps.md` | `592994ffb13e24b2042d42f0dd9320ef32127797add7694a471dea4034210721` | closed |
| 011 | `docs/tickets/open/009-implement-ricochet-projectile-cards.md` | `c237efb42b05ddf6396fb7eacd4343ff4e5edac9bc7831a84df76cb5963a5947` | ready |
| 013 | `docs/tickets/closed/011-implement-radial-saw-maps.md` | `592994ffb13e24b2042d42f0dd9320ef32127797add7694a471dea4034210721` | closed |
| 013 | `docs/tickets/closed/013-implement-arena-026-moving-platforms.md` | `0a297f14a49b0e4c977359c287ae09c6f58a3d4a39c333f96e26285d7141dbee` | closed |
| 013 | `docs/tickets/open/009-implement-ricochet-projectile-cards.md` | `f2b854a1cd8d9d28893523648dcecb1ec8e168f7053196ab3f5d91a239281053` | ready |
| 014 | `docs/tickets/closed/011-implement-radial-saw-maps.md` | `592994ffb13e24b2042d42f0dd9320ef32127797add7694a471dea4034210721` | closed |
| 014 | `docs/tickets/closed/013-implement-arena-026-moving-platforms.md` | `a2703f080e535a94b48d72f9f343e385451e6c81ece7b3ceb74a432aeca6a639` | closed |
| 014 | `docs/tickets/closed/014-bind-complete-content-roadmap.md` | `a9461be183f31ade9499627bb5290a4410852db41414a75ca680e77d814f2ff6` | closed |
| 014 | `docs/tickets/closed/017-show-exact-playable-card-names.md` | `69f7afacb26c8d9352a06338ab5eb52142932238fac8ece4773433ae54a174d4` | closed |
| 014 | `docs/tickets/closed/018-add-core-fidelity-measurement-harness.md` | `7244a7c2c0c11e94a7cf305333a695f6bec32d968190f65878479971e6321843` | closed |
| 014 | `docs/tickets/closed/019-correct-production-camera-span.md` | `0ef800d0fdc9c18601a33c3c0387f39a85bde2ab8e25055832e3ec3ab64b4cec` | closed |
| 014 | `docs/tickets/open/015-research-projectile-damage-scale.md` | `7bf19d604095792b585a24dca3570078e24a9bfb1ef22111a2e0466fa4ddeec4` | ready |
| 014 | `docs/tickets/open/016-research-projectile-collision-scale.md` | `646b79c496151288d3930d39908b72db7e0fce2db59010b3196fb7f903490438` | idea |
| 014 | `docs/tickets/open/020-add-deterministic-automated-opponent.md` | `4c95ccfc30f337130fd28290fceb26ee7de9598b9fb6a0d4c56f0497e748307f` | blocked |
| 015 | `docs/tickets/closed/011-implement-radial-saw-maps.md` | `592994ffb13e24b2042d42f0dd9320ef32127797add7694a471dea4034210721` | closed |
| 015 | `docs/tickets/closed/013-implement-arena-026-moving-platforms.md` | `a2703f080e535a94b48d72f9f343e385451e6c81ece7b3ceb74a432aeca6a639` | closed |
| 015 | `docs/tickets/closed/014-bind-complete-content-roadmap.md` | `a9461be183f31ade9499627bb5290a4410852db41414a75ca680e77d814f2ff6` | closed |
| 015 | `docs/tickets/closed/017-show-exact-playable-card-names.md` | `69f7afacb26c8d9352a06338ab5eb52142932238fac8ece4773433ae54a174d4` | closed |
| 015 | `docs/tickets/closed/018-add-core-fidelity-measurement-harness.md` | `7244a7c2c0c11e94a7cf305333a695f6bec32d968190f65878479971e6321843` | closed |
| 015 | `docs/tickets/closed/019-correct-production-camera-span.md` | `bcdf4a25dab73c4622406a3d77c0f0338df391afcfd756bd64dbbc527bfa30c6` | closed |
| 015 | `docs/tickets/open/015-research-projectile-damage-scale.md` | `126f087790351e5f4f6fef776e3b4ae5287ccac9b365821f80edee5b78475927` | blocked |
| 015 | `docs/tickets/open/016-research-projectile-collision-scale.md` | `646b79c496151288d3930d39908b72db7e0fce2db59010b3196fb7f903490438` | idea |
| 015 | `docs/tickets/open/021-build-safe-source-observation-rig.md` | `8fea39d1e9eabdab938abc5cad21e4463270daaf44a46def17066b1acac111b1` | blocked |
| 022 | `docs/tickets/closed/011-implement-radial-saw-maps.md` | `592994ffb13e24b2042d42f0dd9320ef32127797add7694a471dea4034210721` | closed |
| 022 | `docs/tickets/closed/013-implement-arena-026-moving-platforms.md` | `a2703f080e535a94b48d72f9f343e385451e6c81ece7b3ceb74a432aeca6a639` | closed |
| 022 | `docs/tickets/closed/014-bind-complete-content-roadmap.md` | `a9461be183f31ade9499627bb5290a4410852db41414a75ca680e77d814f2ff6` | closed |
| 022 | `docs/tickets/closed/017-show-exact-playable-card-names.md` | `69f7afacb26c8d9352a06338ab5eb52142932238fac8ece4773433ae54a174d4` | closed |
| 022 | `docs/tickets/closed/018-add-core-fidelity-measurement-harness.md` | `7244a7c2c0c11e94a7cf305333a695f6bec32d968190f65878479971e6321843` | closed |
| 022 | `docs/tickets/closed/019-correct-production-camera-span.md` | `bcdf4a25dab73c4622406a3d77c0f0338df391afcfd756bd64dbbc527bfa30c6` | closed |
| 022 | `docs/tickets/closed/022-build-deterministic-controller-input-foundation.md` | `f5cd14ad24d8c76399bca33572544fba61d24a48bb4c79e505800ac5fbd47a72` | closed |
| 022 | `docs/tickets/open/015-research-projectile-damage-scale.md` | `126f087790351e5f4f6fef776e3b4ae5287ccac9b365821f80edee5b78475927` | blocked |
| 022 | `docs/tickets/open/016-research-projectile-collision-scale.md` | `646b79c496151288d3930d39908b72db7e0fce2db59010b3196fb7f903490438` | idea |
| 022 | `docs/tickets/open/021-build-safe-source-observation-rig.md` | `8fea39d1e9eabdab938abc5cad21e4463270daaf44a46def17066b1acac111b1` | blocked |
| 029 | `docs/tickets/closed/011-implement-radial-saw-maps.md` | `592994ffb13e24b2042d42f0dd9320ef32127797add7694a471dea4034210721` | closed |
| 029 | `docs/tickets/closed/013-implement-arena-026-moving-platforms.md` | `a2703f080e535a94b48d72f9f343e385451e6c81ece7b3ceb74a432aeca6a639` | closed |
| 029 | `docs/tickets/closed/014-bind-complete-content-roadmap.md` | `a9461be183f31ade9499627bb5290a4410852db41414a75ca680e77d814f2ff6` | closed |
| 029 | `docs/tickets/closed/017-show-exact-playable-card-names.md` | `69f7afacb26c8d9352a06338ab5eb52142932238fac8ece4773433ae54a174d4` | closed |
| 029 | `docs/tickets/closed/018-add-core-fidelity-measurement-harness.md` | `7244a7c2c0c11e94a7cf305333a695f6bec32d968190f65878479971e6321843` | closed |
| 029 | `docs/tickets/closed/019-correct-production-camera-span.md` | `bcdf4a25dab73c4622406a3d77c0f0338df391afcfd756bd64dbbc527bfa30c6` | closed |
| 029 | `docs/tickets/closed/022-build-deterministic-controller-input-foundation.md` | `f5cd14ad24d8c76399bca33572544fba61d24a48bb4c79e505800ac5fbd47a72` | closed |
| 029 | `docs/tickets/closed/023-integrate-projectile-card-foundation.md` | `4619d41078dd6cc1ee56902f210810981866cebf6ace9192add4e133b9920d5e` | closed |
| 029 | `docs/tickets/closed/025-integrate-volley-card-mechanics.md` | `d9a02412ad7f2b74e4be6ab67c30ec311c5f3fab7898f3d683d90dce2a588962` | closed |
| 029 | `docs/tickets/closed/026-integrate-rebound-card-mechanics.md` | `47eb57808a8cb3c146e5c6180c61fcfdb120bcfb256bd794e259bb2979173368` | closed |
| 029 | `docs/tickets/closed/027-integrate-body-impact-cards.md` | `f3ef196ad2a1835f3686e3fa946da91a1784063763e57677c0d4b64ea71b53d1` | closed |
| 029 | `docs/tickets/closed/028-integrate-passive-projectile-flight.md` | `c0040129428938f7def7b4585220d2e467eaed9483103815a0b21408d46402b2` | closed |
| 029 | `docs/tickets/open/015-research-projectile-damage-scale.md` | `c3cf22f728368dcd94b1af93dfb8b38f6f5af4e0400e24297651ad139798f369` | blocked |
| 029 | `docs/tickets/open/016-research-projectile-collision-scale.md` | `646b79c496151288d3930d39908b72db7e0fce2db59010b3196fb7f903490438` | idea |
| 029 | `docs/tickets/open/021-build-safe-source-observation-rig.md` | `8fea39d1e9eabdab938abc5cad21e4463270daaf44a46def17066b1acac111b1` | blocked |
| 029 | `docs/tickets/open/024-rotate-exposed-ivy-discord-token.md` | `2a31a8b93d39e8c7071de998fb8a71bb40ab8944fbf2c64e2bdf5f9730d9dc72` | blocked |
| 029 | `docs/tickets/open/029-integrate-passive-proximity-auras.md` | `5f1f0e3f6513355a5caa55d5d0b3f7f9bdd9b92ab07dcad5812464ccafd1248c` | idea |

The scan found all 21 logical numbers from 009 through 029.
It found three byte variants of 009, two of 011, two of 013, three of 015, and two of 019.
Every other logical number has one observed byte variant.

## Collision map

The canonical allocator reports 014 after considering the admitted recovery ticket 013 in this worktree.
The frozen snapshots already contain an unrelated orphan ticket 013 and records numbered 014 through 029.
No number is renumbered or reserved by this inventory.

| Number range | Authoritative/admitted identity | Orphan identity | Required handling |
|---|---|---|---|
| 013 | `Make orphaned project progress safely recoverable` | `Implement arena 026's moving platforms` | Keep the admitted recovery ticket as 013; assign any recovered moving-platform work a fresh allocator result and retain 013 as historical identity metadata. |
| 014–029 | allocator considers these available after current 013 | every number has at least one frozen orphan ticket occurrence | Before creating any new ticket, rerun the allocator and check this map; recovered work gets a fresh authoritative number while preserving its orphan logical number in provenance. |

## Safest next independently reviewable slice

Start with the registered, clean `.ivy/worktrees/009-projectile-cards` history.
It is the only complete orphan artifact whose exact 13-commit chain is still addressable directly above `c24ed0a` and whose current allowlisted bytes are clean against its detached head.
A new recovery ticket should inspect `c24ed0a88c2bff843e788e1957502d9b86bc3d25..4ce6038d83cd5fbdc7c0b988e0a9ba8f57895047`, select an exact candidate rather than assuming the head is acceptable, reproduce the supported headless checks, and obtain fresh independent review.
The ticket-009 lifecycle claim remains `ready`, and later recorded exact candidates are missing or rejected, so this recommendation is for review, not integration.

After ticket 009 is resolved, recover ticket 011 from its registered design head plus manifest-preserved dirty bytes before attempting cumulative unregistered snapshots.
Tickets 013–029 should then be split into independently reviewable outcomes in dependency order, using their index correlations as evidence but never as replacements for missing commit provenance.
The ticket-024 provider action remains separate from repository recovery and must not inspect or reproduce the old credential value.

## Reproduction and final comparison checklist

- `git rev-parse HEAD` in the delivery worktree records the admission base and later proves it stayed detached.
- `git -c safe.directory=C:/_MyFiles/Programming/Projects/rounds-clone -C C:/_MyFiles/Programming/Projects/rounds-clone show-ref --head` captures all refs without changing configuration.
- `git -c safe.directory=C:/_MyFiles/Programming/Projects/rounds-clone -C C:/_MyFiles/Programming/Projects/rounds-clone worktree list --porcelain` captures all registrations.
- The helper command above must produce byte-identical manifests and the eight artifact digests in this inventory on repeated runs.
- Registered status must remain 009 clean, 010 with 0 staged/2 unstaged/2 untracked, and 011 with 0 staged/30 unstaged/20 untracked.
- The five `.git-index` SHA-256 values, sizes, root metadata, refs, registrations, and all eight artifact manifest digests must match before and after implementation.
- Ticket verification must find 55 occurrences, 28 byte variants, and all 21 logical numbers 009–029.
- `node C:/Users/Adam/.codex/plugins/cache/ivy/ivy/0.1.0/checks/scripts/check-tickets.mjs .` and `git diff --check` must pass in the delivery worktree.
