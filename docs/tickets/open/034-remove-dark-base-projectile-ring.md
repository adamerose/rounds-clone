---
format: 3
status: ready
created: 2026-08-30T02:35:54Z
origin: human-request
tags: ["product-fidelity", "projectiles", "presentation"]
value: 8
risk: 3
sessions:
  - codex:01a0492f-57bc-7f32-87fa-1fbe5d483893
execution: unattended
depends-on: [15, 30, 33]
supersedes: []
split-from: [17]
---

# Remove the dark base-projectile ring

The user reported that the clone's bullets read as black, while public ROUNDS gameplay presents a bright projectile core against its dark arenas.
The current renderer draws an `Ink` circle of radius `r + 2` behind a `Paper` circle of radius `r`; at the four-pixel minimum core radius, the dark ring occupies more visible area than the bright core.
Remove that known-wrong ring now without claiming ticket 017's still-unverified complete projectile presentation.

## Outcome

- An ordinary base projectile has a bright `Paper` circular core and no dark `Ink` ring.
- The existing owner-colored trail remains unchanged.
- Projectile speed, simulation radius, collisions, damage, ownership, and card behavior remain unchanged.
- Exact glow, trail geometry, size scaling, motion blur, impacts, and card-modified projectile presentation remain owned by ticket 017 and require its direct target-build evidence.

## Decisions

- Treat the user's direct observation that black bullets are wrong as binding negative evidence.
- Use the official Steam gameplay stills only as corroboration that active projectile effects read brightly against the dark arena; do not infer unmeasured glow, opacity, radius, or trail values from compressed promotional frames.
- Make the smallest removal-only correction: delete the dark outline draw and preserve the already-present bright core and colored trail.
- Keep all downloaded public stills and video outside the repository; record URLs and derived observations only.
- Exclude the ignored `.tools` cache from the shipped runtime identity walk; its SDK files are external verification machinery, not repository-controlled build inputs.
- No Godot editor/runtime, visible window, input injection, GPU capture, or renderer verification is permitted while the user is active.

## Evidence required

- A source-level regression proves `DrawBullet` retains the owner-colored trail and `Paper` core but contains no `Ink` projectile circle.
- The shipped runtime boundary remains exactly 73 repository-controlled files and ignores an adversarial build-control file under `.tools`.
- The zero-warning game build, focused regression, repository checks, ticket checker, and `git diff --check` pass with at most two logical processors.
- Candidate inspection proves only this ticket, the projectile drawing line, the deliberate runtime-boundary hash, the `.tools` exclusion, their direct regressions, and a narrowly attributable postmortem append changed.
- No claim of complete projectile fidelity or native visual verification is made.

## Work log

- 2026-08-30T02:35:54Z stage design start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Split the user's still-visible black-projectile complaint from ticket 017's larger renderer-backed comparison so the known-wrong dark ring can be removed without inventing the remaining presentation.
- 2026-08-30T02:35:54Z stage design end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Bound a removal-only correction to the dark outline, preserved the bright core and owner trail, and left every unmeasured projectile quantity with ticket 017.
- 2026-08-30T02:38:00Z stage implementation start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Removing only the dark base-projectile ring and adding a source-level regression while remaining fully headless and low-CPU.
- 2026-08-30T02:43:00Z stage correction start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Correcting the runtime identity walk after the root's ignored SDK cache changed its claimed repository-controlled boundary from 73 files to 322.
- 2026-08-30T02:43:00Z stage correction end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Excluded `.tools`, added an adversarial cache regression, and independently reproduced the prior 73-file hash before deriving the new projectile-only boundary hash `8e7a43b3c71f421f71ff5b14f3a618a0307ec2f0c1990a219710f5fa19298d84`.
- 2026-08-30T02:47:02Z stage implementation end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Removed the dark ring, retained the bright core and owner trail, compiled the changed checker and test sources through retained Roslyn, passed the real identity gate at exactly 73 files and the new hash, passed the source boundary, ticket checker, and whitespace check, and used no GUI, Godot, input, or GPU work; full SDK build and test-runner evidence remain pending because the ignored root SDK cache has lost its host and redownloading it was deferred during the user's active game.
- 2026-08-30T02:56:07Z stage verification start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Using installed Visual Studio Build Tools plus NuGet's verified 6.45 MiB .NET 8 reference pack to replace the missing SDK host without downloading or launching the full SDK.
- 2026-08-30T02:56:07Z stage verification end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Passed a zero-warning full solution build, all 134 checker tests, 251 applicable simulation tests excluding only the known CLI-dependent `GoldenHistoryScriptTests`, repository checks, the exact 73-file identity hash, ticket format, and whitespace; the temporary incomplete launcher pieces were removed, no Godot runtime or renderer ran, and no window or operating-system input was used.
- 2026-08-30T02:56:55Z stage review start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a05099-3e8e-7852-bc46-07d314927def — Reviewed exact range `621d1a175e57c700950622ecb7203b9a197af042..ced986cad76ebf9dd95220b6f5900e302a1f8c78` against ticket 034.
- 2026-08-30T03:02:39Z stage review end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a05099-3e8e-7852-bc46-07d314927def — Changes requested: make the no-`Ink` regression semantic and retain exact-candidate rendered evidence before appearance approval; full findings are in the native review result for this session.
- 2026-08-30T03:04:00Z stage correction start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — Tightening the projectile regression so spacing or literal variations cannot reintroduce any `Ink` use inside `DrawBullet`; keeping the visual-evidence gate open under the user's no-GUI constraint.
- 2026-08-30T03:05:47Z stage correction end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893 — The regression now rejects every `Ink` token within the extracted `DrawBullet` method, while positive assertions retain the owner trail and `Paper` core; a zero-warning bounded full solution build and the focused test pass. Exact-tip rendered evidence remains intentionally pending.
- 2026-08-30T03:06:12Z stage review start session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a05099-3e8e-7852-bc46-07d314927def — Re-reviewed corrected exact range `621d1a175e57c700950622ecb7203b9a197af042..53c0212c84f12515d319bb277e3b49273c1ed4ec` and the prior findings.
- 2026-08-30T03:07:11Z stage review end session codex:01a0492f-57bc-7f32-87fa-1fbe5d483893/01a05099-3e8e-7852-bc46-07d314927def — The semantic-regression finding is resolved and no new code, scope, test, or prose finding remains; approval is withheld solely until an exact-tip monitor-4 render proves the final projectile composite while the user is available.
