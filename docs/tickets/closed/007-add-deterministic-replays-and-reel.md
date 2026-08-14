---
format: 3
status: closed
created: 2026-08-14T13:00:41Z
origin: agent-proposed
tags: [implementation, replay, determinism, harness, rendering]
value: 10
risk: 4
depends-on: [6]
sessions:
  - codex:019ffea8-55c5-79b3-96b2-da3210d67d84
  - codex:01a0005d-9cf9-7730-964c-44052ae7659b
  - codex:01a00067-2a24-7c62-b0de-4a8d79fa34ef
  - codex:01a0006e-339c-73f0-99e9-d966cfcead82
  - codex:01a00072-a9bb-7de0-9941-688b793a486a
  - codex:01a0007a-0880-70a1-b793-7f37a393f662
  - codex:01a00080-987e-7bf3-b737-fc702b77fb97
  - codex:01a0008a-3d1e-74a0-8e6b-fb021ecf0291
  - codex:01a00090-916c-7161-81c5-276b71c5c437
  - codex:01a00096-742f-71f1-b5fc-80f5772e2046
  - codex:01a000ac-dd36-7902-81e2-5b2c75826c5d
  - codex:01a000c5-7def-76c1-94ad-1f2c895696c6
  - codex:01a000eb-212e-7640-82f7-a7b11c745b87
  - codex:01a000fd-0cf6-7142-aae8-c71a8445bd6a
  - codex:01a0010d-da47-7f20-adb3-831c90ff5aae
  - codex:01a00122-91f3-7250-b63c-55c236365989
---

# Add deterministic replays and a rendered reel

The repository can record an exact input stream, replay it through the public simulation boundary, fail on the first divergent checkpoint, protect a committed golden corpus, and render the same replay through Godot into an ignored AVI reel.

## Outcome

- Add a `Rounds.Replay` library between the pure simulation and its hosts; it may use serialization and file I/O, references `Rounds.Sim`, and has no Godot reference.
- Define version 1 as canonical UTF-8 JSON selected by target build `21020021` and ruleset `base-combat-v1`, which together name the embedded immutable arena and tuning data used by ticket 006.
- A canonical file is exactly one compact JSON object followed by one LF: UTF-8 without BOM, CR, indentation, or other insignificant whitespace.
  Its top-level properties occur exactly in this order: `format`, `replayId`, `targetBuild`, `ruleset`, `seed`, `arenaId`, `tickRate`, `playerCount`, `totalTicks`, `runs`, `checkpoints`, `finalHash`.
  Unknown or duplicate properties and noncanonical property order, integer spelling, escaping, whitespace, encoding, or final newline are rejected rather than normalized on load.
- `format` is integer `1`; `replayId` is 1 to 64 lowercase ASCII characters matching `[a-z0-9]+(?:-[a-z0-9]+)*`; `targetBuild` is integer `21020021`; `ruleset` is `base-combat-v1`; `seed` is a canonical unsigned-64 decimal string with no leading zero except `0`; `arenaId` is an embedded lowercase ASCII ID with the replay-ID grammar; `tickRate` is integer `60`; `playerCount` is integer `2`; and `totalTicks` is an integer from 1 through 216,000.
- `runs` is a nonempty array of objects with properties exactly `length`, `players` in that order.
  `length` is a positive integer and `players` is exactly two objects, each with properties exactly `move`, `aimXBits`, `aimYBits`, `jump`, `fire`, `block` in that order.
  Movement is integer `-1`, `0`, or `1`; each aim component is exactly `[0-9a-f]{16}`, produced most-significant nibble first from the unsigned numeric value returned by `DoubleToUInt64Bits`; and the three buttons are JSON booleans.
- `checkpoints` is a nonempty array of objects with properties exactly `tick`, `hash` in that order.
  It contains exactly one entry after ticks 60, 120, and so on below `totalTicks`, followed by exactly one entry at `totalTicks`; an already aligned final tick is not duplicated.
  Ticks strictly increase, hashes are 16 lowercase hexadecimal digits, and the last checkpoint hash equals `finalHash`.
- Store aim components as those exact lowercase hexadecimal IEEE-754 encodings so signed zero and every finite double round-trip into the existing input checksum; store movement only as `-1`, `0`, or `1` and the three buttons as booleans.
- Coalesce adjacent identical two-player frames into one run by comparing every stored field, including aim bit patterns so `+0.0` and `-0.0` remain distinct; reject zero-length or adjacent duplicate runs, non-finite aim, inconsistent totals, unsupported headers, unknown arenas, malformed hashes or bit strings, and every structural checkpoint error before constructing or stepping a world.
- Record checkpoints after ticks 60, 120, and so on, plus the final tick when it is not already a 60-tick boundary; playback stops at the first mismatch and reports replay ID, tick, expected hash, and actual hash.
- Extend `Rounds.Harness` with `record --profile base-combat --id <replay-id> --seed <ulong> --ticks <positive-int> --output <path>`, `replay --input <path>`, and `verify-replays --directory <path>` while keeping `smoke` supported.
- Make `record` use the existing deterministic base-combat input profile rather than hiding a second simulation driver; it writes through a public recorder that future live matches and bots can feed one input frame at a time.
- Commit `replays/golden/base-combat-006-seed-1.rounds-replay.json`, a 600-tick arena-006 golden that exercises movement, jump, aim, held fire, block, a result, and reset.
  Golden discovery is nonrecursive and matches only `replays/golden/*.rounds-replay.json`; verification uses ordinal basename order, requires the filename stem to equal `replayId`, and rejects an empty corpus or duplicate ID.
- Add `replays/intentional-breaks.md` with the exact heading `# Intentional replay breaks` followed by LF.
  An empty ledger ends there so it passes `git diff --check`; before the first entry, append one additional LF, then zero or more append-only LF-terminated lines matching `- replay: <filename>; old: <16-lower-hex>; new: <16-lower-hex|deleted>; reason: <reason>`.
  A reason is 1 to 200 ASCII characters from U+0020 through U+007E excluding semicolon, and its first and last characters are not spaces.
  `<filename>` is one golden basename, replacement entries use the prior and new `finalHash`, deletion uses the exact sentinel `deleted`, and the full tuple may occur only once in the ledger.
  Existing ledger lines may never be edited, reordered, or removed.
  Once any ledger entry deletes a basename, that basename is permanently reserved and a later file with the same basename fails; version 1 has no restore transition.
- Add `tools/checks/check-golden-history.ps1 -Base <sha|ROOT> -Head <sha>`.
  It rejects a shallow repository and missing revisions.
  A commit base must be an ancestor of `Head` and evaluates every commit reachable from `Head` but not `Base` in topological oldest-first order.
  The exact case-sensitive sentinel `ROOT` is local/test-only, requires `Head` itself to have no parent, and evaluates that one root commit; CI never supplies it.
  A non-merge commit is compared with its sole parent.
  A merge commit must have exactly two parents, both side histories are evaluated separately in the range, and `git merge-tree --write-tree <parent1> <parent2>` must produce a conflict-free tree byte-identical to the merge commit tree.
  A conflicting, octopus, or manually altered merge fails and must be rebased or recreated as a clean merge; a later commit cannot repair rejected history.
  Inherited replay transitions are not misclassified as changes introduced by the merge commit.
  A root commit treats its goldens as new files.
  Each replaced or deleted golden must add exactly one matching ledger line in that same commit; new goldens need no entry; an entry without its exact same-commit change fails.
- After commit-by-commit checks, event handling also compares the selected established endpoint tree with the effective candidate tree.
  For each basename whose endpoint value differs, it starts with the established hash or absence and applies transitions for that basename in topological oldest-first commit order.
  A validated new-file commit contributes an implicit `absent→newHash` transition; replacements and deletions contribute their already same-commit-validated ledger transitions.
  The transitions must form one continuous chain with no unused fork and must end at the effective hash or `deleted`; `absent→A`, `A→B`, `B→C` therefore proves a tagged root-to-`C` endpoint without inventing an orphan direct entry.
  A permanent deletion reservation still forbids any later implicit addition of that basename.
  This closes a branch-fork case where a file looked new relative to its own parent but replaces an already exposed golden at the target endpoint.
- Add `tools/checks/check-golden-event.ps1 -HistoryBase <sha> -Established <sha> -Candidate <sha> [-ProspectiveMerge]` as the CI-facing wrapper.
  It first runs the commit-by-commit history guard from `HistoryBase` through `Candidate`.
  Without `-ProspectiveMerge`, endpoint comparison uses the candidate commit tree.
  With `-ProspectiveMerge`, it computes a three-way result using `git merge-tree --write-tree Established Candidate`, fails on any merge conflict, and compares the established tree with that prospective result tree; the generated tree is inspection state, not a commit or checkout mutation.
  Before success it loads the complete effective golden directory and ledger from the candidate or prospective tree and reapplies all corpus invariants: canonical files, nonempty unique IDs, filename agreement, valid unique append-only ledger grammar, and absence of every permanently reserved basename.
- CI trusts exact repository inception commit `b9073b6a9c110b5fbca5e242d49bd03a8cecef12`; every commit-bearing candidate must descend from it and `ROOT` is forbidden in CI.
  Every job fetches all current branch and tag refs plus the exact trusted root and every event revision it will use: `before`, PR base, PR head, default-branch head, and candidate head as applicable.
  Each exact fetch and commit peel is verified and the job fails closed if a required revision cannot be obtained.
- Pull requests check out the explicitly fetched PR head instead of a synthetic merge and require both PR base and head to descend from the trusted root.
  They compute a verified merge base and call the event wrapper with merge base as `HistoryBase`, current PR base as `Established`, PR head as `Candidate`, and `-ProspectiveMerge`.
  Thus feature commits remain the audited history while endpoint policy judges the result people would actually merge; an ordinary diverged PR with a base-only golden passes and a same-path merge conflict fails closed.
  Ordinary branch pushes require `before` to be an ancestor of `after` and call the wrapper with `before` as both `HistoryBase` and `Established` and `after` as `Candidate`.
  New-branch pushes compute a merge base with the fetched default head and call the wrapper with that merge base as `HistoryBase`, default head as `Established`, the new head as `Candidate`, and `-ProspectiveMerge`.
  Every non-fast-forward branch update, missing merge base, unrelated history, or candidate outside trusted-root ancestry fails closed.
- CI also subscribes to tag pushes.
  A newly created nondeleted tag is peeled to a commit, must descend from the trusted root, must be an ancestor of the fetched default head, and is checked from the trusted root through that commit.
  It calls the wrapper with the trusted root as both `HistoryBase` and `Established` and the peeled commit as `Candidate`.
  In-place tag updates are rejected; deletion followed by recreation of the same tag name is indistinguishable from a first creation and is treated as a new verified tag.
  A commit introduced only through a tag but absent from default-branch history therefore runs and fails the guard instead of bypassing it.
  Deleted branch or tag refs and tag targets that do not peel to a commit skip verification because they introduce no candidate commit; branch and tag filters cover no other push shape.
  Local use requires explicit `-Base` and `-Head`, except the supported gate may default to `HEAD^` only when `HEAD` has exactly one parent and to `ROOT` when `HEAD` is a root commit; any other missing parent or revision fails explicitly.
- Add Godot replay mode through `-- --replay <path>`: it reads only recorded inputs, never live controls, steps the same `World`, checks recorded checkpoints, prints the final hash, and contains no replay-only game rule.
- Replay rendering consumes exactly one input and calls `World.Step` once before each written frame, so AVI frame 1 shows post-step tick 1 and an N-tick replay writes exactly N frames with no pre-roll or terminal duplicate.
  Replay mismatch, malformed input, or termination before all inputs are consumed quits Godot nonzero.
  Success prints exactly one machine-readable `REPLAY_COMPLETE id=<id> ticks=<N> hash=<finalHash> frames=<N>` line after the last checkpoint passes.
- Add `tools/render-replay.ps1 -Replay <path> -Output <path>` that invokes pinned Godot with `--write-movie`, fixed 60 FPS, and `--quit-after` equal to `totalTicks`.
  It requires process success and the exact completion line, parses the AVI `avih` header as little-endian, and fails unless the file is nonempty RIFF AVI and `dwTotalFrames` equals `totalTicks`.
- Add a scheduled Windows nightly workflow that provisions pinned tools, verifies all goldens, and renders exactly `replays/golden/base-combat-006-seed-1.rounds-replay.json`.
  Every workflow action is pinned by full commit SHA.
  The ignored AVI uploads with retention `7` days only after golden verification, replay completion, final-hash equality, RIFF validation, and exact frame-count validation pass.
- Leave the first local reel at `reels/2026-08-14-base-combat.avi`; AVI bytes are presentation output and are not a deterministic golden or committed file.

## Why

Cards, bots, match flow, and balance tuning will multiply the state space.
A small exact replay boundary now turns every later failure into a reproducible seed and input stream, while the reel gives the unattended project a visible result instead of trusting hashes alone.

## Essential constraints

- `Rounds.Sim` remains unaware of JSON, files, Godot, and replay policy; `Rounds.Replay` owns replay encoding and deterministic playback; Godot and the harness only adapt their inputs and outputs.
- Playback always constructs the world from embedded immutable arena and tuning data named by the replay; repository-relative `spec/` paths and captured state snapshots are not part of the format.
- No wall clock, locale-sensitive number, ambient random source, unordered iteration, or platform path enters recorded state or playback.
- Hash checkpoints diagnose drift but do not replace full input playback or `Sim.Hash`.
- Golden replay changes remain explicit historical decisions; the intentional-break ledger cannot bless a replay that still fails its newly recorded expected hash.
- The renderer consumes the replay through `Rounds.Replay`; it does not duplicate input generation, combat, interpolation, or lifecycle state in the scene.
- Do not add scoring, drafts, cards, bots, arena rotation, audio, camera effects, production codecs, or network replay compatibility in this ticket.

## Evidence required

- Round-trip tests prove every input field, maximum finite aim, subnormal aim, and signed zero survive encode/decode bit-exactly; canonical rewrites are byte-identical; and uppercase, 64-character binary, short, and byte-reversed aim spellings fail or decode to the demonstrably different value dictated by most-significant-nibble order.
- Negative tests cover every header/run/hash limit, adjacent-run coalescing, total mismatch, non-finite aim bits, unsupported arena, path-independent stream loading, and mutation-before-validation.
- Recorder/playback tests prove checkpoint placement at 60 and a non-multiple final tick, first-mismatch diagnostics, identical final worlds across repeated playback, and a one-input mutation changing the recorded hash.
- Golden-guard tests prove new files pass; changed or deleted goldens fail without one exact same-commit ledger entry; wrong hashes, duplicate or edited ledger entries, and orphan entries fail; and root, missing-parent, shallow, new-branch, force-push, merge, and synthetic pull-request histories have the specified result.
  At least one fixture places the golden change in an earlier commit of a multi-commit push, and one presents a synthetic merge while selecting the explicit PR head and base.
  Separate bypass fixtures prove an established orphan branch, unrelated pull request, and no-merge-base force push cannot invoke `ROOT` or recreate a golden without a ledger entry.
  Tag fixtures cover lightweight and annotated commit tags, a tag-only unledgered change, deletion, and a non-commit target; another fixture deletes every established ref before an orphan push and still fails the trusted-root requirement.
  Endpoint fixtures cover a base that advances after branch creation, base-only goldens preserved by prospective PR and new-branch merges, a true same-path merge conflict, a clean true merge carrying inherited transitions, a divergent branch forked before the first golden, a non-fast-forward branch update, in-place tag update, verified delete/recreate tag, `absent→A→B` from the trusted-root tag range, `A→B→C`, `A→B→deleted`, and deletion followed by a later same-name golden addition.
  A prospective-merge fixture forks before a basename is added and deleted on the base, then proves that adding the reserved name on the old branch fails complete effective-corpus validation.
- CLI tests or supported process checks prove `record`, `replay`, and ordinal multi-file verification return useful output and nonzero status on corruption or mismatch.
- The complete gate replays the committed corpus and remains zero-warning with `spec/` byte-identical to ticket 006.
- A controlled Godot replay render shows the same arena, HUD, bullets, shields, result, and reset as headless playback; its completion marker matches the golden and consumed tick count, its AVI declares exactly 600 frames, and representative first/combat/result frames are visually inspected.
- The nightly workflow and local render script use only pinned repository tools, retain no unexpected process or temporary frame residue, and leave the requested local reel outside Git history.

## Work log

- 2026-08-14T13:00:41Z stage design start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Comparing the smallest exact replay boundary with the existing pure simulation, golden-hash policy, Godot movie writer, nightly visibility requirement, and future bot/live-input producers.
- 2026-08-14T13:00:41Z stage design end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Bound a separate replay library, bit-exact canonical RLE inputs, checkpointed playback, public harness commands, parent-diff golden protection, replay-only Godot input adaptation, deterministic-hash verification, and ignored AVI reel generation without adding match or card rules.
- 2026-08-14T13:10:14Z stage admission start session codex:01a0005d-9cf9-7730-964c-44052ae7659b — Challenged exact candidate f2916be681324139325cb21486d6a43d67fd633d for cold-reader completeness, executable history policy, canonical bytes, render proof, and nightly dependency invariants.
- 2026-08-14T13:10:14Z stage admission end session codex:01a0005d-9cf9-7730-964c-44052ae7659b — Rejected the candidate because golden history ranges and ledger deletion were ambiguous, the JSON schema was not independently canonical, movie frame count was not proven, and nightly pinning and retention were unspecified.
- 2026-08-14T13:10:14Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Defining exact bytes and shapes, commit-by-commit full-history enforcement, post-step frame semantics and completion proof, and pinned seven-day nightly upload policy.
- 2026-08-14T13:11:47Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Made the v1 byte grammar and shapes complete, the append-only deletion-capable ledger exact, every Git event range explicit, and every movie frame, completion, action pin, and retention boundary verifiable.
- 2026-08-14T13:19:18Z stage admission start session codex:01a00067-2a24-7c62-b0de-4a8d79fa34ef — Rechecked exact candidate 1b825a0f5237a05ffd8f6834c833adf1320d65fb with emphasis on root invocation, event coverage, and merge-side history semantics.
- 2026-08-14T13:19:18Z stage admission end session codex:01a00067-2a24-7c62-b0de-4a8d79fa34ef — Rejected the candidate because no declared base value could invoke root history and broad push subscription left tag and deleted-ref events without a candidate head.
- 2026-08-14T13:19:18Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Adding an exact ROOT range sentinel and limiting CI to executable pull-request and nondeleted branch-head cases.
- 2026-08-14T13:19:51Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Made root history callable, assigned initial and orphan pushes to it, excluded tag events, and explicitly skipped deleted branch refs that have no reviewable head.
- 2026-08-14T13:24:09Z stage admission start session codex:01a0006e-339c-73f0-99e9-d966cfcead82 — Challenged exact candidate 623f44122513fc6e4cd09a53fe8f5e81144c8329 for root-sentinel abuse across unrelated and orphan histories.
- 2026-08-14T13:24:09Z stage admission end session codex:01a0006e-339c-73f0-99e9-d966cfcead82 — Rejected the candidate because established orphan, unrelated pull-request, and no-merge-base force-push histories could recreate protected goldens under ROOT.
- 2026-08-14T13:24:09Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Reserving ROOT for a provably unique first repository history and rejecting every established-history case without a merge base.
- 2026-08-14T13:24:44Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Required the base to be ancestral, confined ROOT to a history containing every fetched ref, rejected unrelated histories, and added the three bypass fixtures to the evidence contract.
- 2026-08-14T13:32:13Z stage admission start session codex:01a00072-a9bb-7de0-9941-688b793a486a — Challenged exact candidate 39e677f27db2807f9c62a8ce4846178ab7abd80c against deleted-ref orphan pushes, tag-only commits, and event revisions no longer advertised by current refs.
- 2026-08-14T13:32:13Z stage admission end session codex:01a00072-a9bb-7de0-9941-688b793a486a — Rejected the candidate because ref visibility could still misclassify an orphan as first history, tag pushes were excluded, and stale event SHAs were not explicitly fetched.
- 2026-08-14T13:32:13Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Anchoring CI to the immutable repository inception, fetching every exact event revision, checking commit-bearing tags, and limiting ROOT to a single local root fixture.
- 2026-08-14T13:32:53Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Bound the trusted inception, explicit event fetches and ancestry, tag-only commit range, fail-closed unrelated histories, and deleted-ref bypass evidence without weakening local root testing.
- 2026-08-14T13:39:04Z stage admission start session codex:01a0007a-0880-70a1-b793-7f37a393f662 — Tested exact candidate 81da55b521798b88a41a58977058b344d5ed5dde against diverged pull requests, endpoint corpus substitution, delete-and-recreate laundering, and independent aim-byte interpretation.
- 2026-08-14T13:39:04Z stage admission end session codex:01a0007a-0880-70a1-b793-7f37a393f662 — Rejected the candidate because normal diverged pull requests failed, divergent endpoints and re-added names could launder changes, aim hexadecimal order was implicit, and trusted-root policy lacked a decision record.
- 2026-08-14T13:39:04Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Adding merge-base PR ranges, target endpoint comparison, reserved deletions, fail-closed ref rewrites, exact hexadecimal order, and durable provenance rationale.
- 2026-08-14T13:39:57Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Made diverged PRs executable through merge base, protected the exposed endpoint, rejected history replacement, reserved deleted names, fixed aim nibble order, and recorded the provenance decision.
- 2026-08-14T13:49:48Z stage admission start session codex:01a00080-987e-7bf3-b737-fc702b77fb97 — Reproduced exact candidate 85fe5b916b6710c8c72072c0f20931683034f828 against a harmless diverged pull request and the observable limits of stateless tag events.
- 2026-08-14T13:49:48Z stage admission end session codex:01a00080-987e-7bf3-b737-fc702b77fb97 — Rejected the candidate because raw base-to-head endpoint comparison falsely deleted a base-only golden and the tag-retarget claim included indistinguishable delete-and-recreate events.
- 2026-08-14T13:49:48Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Separating feature history from prospective merge-result endpoint validation and limiting tag-update claims to observable in-place events.
- 2026-08-14T13:50:27Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Bound every event to an explicit history, established endpoint, and candidate, used a conflict-free prospective tree only for PR endpoint state, and made tag update semantics observable and testable.
- 2026-08-14T13:56:48Z stage admission start session codex:01a0008a-3d1e-74a0-8e6b-fb021ecf0291 — Tested exact candidate f57735b45185b364963a805d130d03579635dc5c against multi-step golden transitions, true merges, and a pre-deletion fork that resurrects a reserved name in the prospective result.
- 2026-08-14T13:56:48Z stage admission end session codex:01a0008a-3d1e-74a0-8e6b-fb021ecf0291 — Rejected the candidate because endpoint policy demanded impossible direct entries for valid transition chains and did not revalidate permanent name reservations on the complete effective merge corpus.
- 2026-08-14T13:56:48Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Chaining validated transitions, making clean merge trees inheritance-only, and revalidating every effective corpus and reservation before event success.
- 2026-08-14T13:57:29Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Made endpoint proof a continuous validated transition chain, merge commits clean inheritance, and the final candidate or prospective tree a fully revalidated corpus with permanent reservations enforced.
- 2026-08-14T14:03:17Z stage admission start session codex:01a00090-916c-7161-81c5-276b71c5c437 — Tested exact candidate d1aca45e60f3da7bf251e36630113605d20c0e3b against root-to-replacement tag history, a default branch advancing before new-branch publication, and the stated recovery from a rejected merge.
- 2026-08-14T14:03:17Z stage admission end session codex:01a00090-916c-7161-81c5-276b71c5c437 — Rejected the candidate because validated additions were absent from endpoint chains, new branches used raw rather than prospective state, and a later commit could not repair a rejected merge already in history.
- 2026-08-14T14:03:17Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Adding implicit validated addition transitions, prospective new-branch state, and the only executable rejected-merge recovery: rebase or clean recreation.
- 2026-08-14T14:03:54Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Completed endpoint chains from absence, preserved default-only state in new-branch prospective merges, and removed the impossible promise that a descendant could repair rejected history.
- 2026-08-14T14:10:26Z stage admission start session codex:01a00096-742f-71f1-b5fc-80f5772e2046 — Audited exact candidate a1621ab87c9e9653ef8f875854e662e815dd7cb7 after eight correction rounds across replay bytes, history, endpoint, merge, rendering, and workflow boundaries.
- 2026-08-14T14:10:26Z stage admission end session codex:01a00096-742f-71f1-b5fc-80f5772e2046 — Admitted risk-4 ticket 007 with no findings because dependency 006 is closed, every Git and effective-corpus case is executable, and no human decision remains.
- 2026-08-14T14:10:26Z stage implement start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Implementing the canonical replay library, both host adapters, golden and history gates, exact movie validation, nightly artifact, tests, and local reel.
- 2026-08-14T14:27:38Z stage implement end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Stopped after the first implementation commit because the frozen empty-ledger grammar necessarily failed the repository's whitespace gate.
- 2026-08-14T14:27:38Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Returning the ticket to blocked and defining the empty ledger as heading plus LF, with the separating blank line introduced only alongside the first entry.
- 2026-08-14T14:27:38Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Removed the contradictory terminal blank line while retaining append-only grammar and identical nonempty-entry bytes.
- 2026-08-14T14:30:51Z stage admission start session codex:01a000ac-dd36-7902-81e2-5b2c75826c5d — Re-audited exact candidate 228e55a5dfb32ea10be0568ca7d672ba311cfda5 for empty-ledger bytes, append-only first entry, scope, risk, dependency, and unresolved choices.
- 2026-08-14T14:30:51Z stage admission end session codex:01a000ac-dd36-7902-81e2-5b2c75826c5d — Re-admitted ticket 007 with no findings after exact bytes, ticket format, full and amendment diffs, dependency 006, and risk 4 all passed.
- 2026-08-14T14:30:51Z stage implement start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Resuming implementation by aligning the ledger parser, exercising Git fixtures, completing the supported gate, and hardening host and movie evidence.
- 2026-08-14T14:53:13Z stage implement end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Completed canonical recording and playback, one protected 600-tick golden, 23 Git-history fixtures, public Godot replay mode, pinned CI and nightly workflows, and the ignored 600-frame reel; the zero-warning gate passed 111 simulation/replay tests, 37 checker tests, golden hash b91f86b6f1dc6b10, deterministic smoke, and Godot editor/runtime smoke with spec unchanged.
- 2026-08-14T15:11:08Z stage review start session codex:01a000c5-7def-76c1-94ad-1f2c895696c6 — Reviewing exact candidate 6fddf1786b827e778ad84d4be661e0d99642d213 against the frozen replay, Git-event, validation-order, rendered-frame, process-exit, and evidence requirements.
- 2026-08-14T15:11:08Z stage review end session codex:01a000c5-7def-76c1-94ad-1f2c895696c6 — Rejected incomplete AVI frames, successful early replay termination, non-byte-exact effective ledger validation, unreachable non-commit-tag skip, world construction before validation, and missing negative/process/corpus fixtures.
- 2026-08-14T15:11:08Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Reopening implementation to fix every rejected public boundary, regenerate independently decoded render evidence, and complete the frozen evidence matrix before a new exact review.
- 2026-08-14T15:34:15Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Validated replay headers before allocation or world creation; made interrupted Godot playback fail; shared one raw-byte ledger parser across history and effective trees; made blob-tag CI checkout-safe; expanded canonical, corpus, history, and process evidence to 147 simulation tests; and regenerated a 600-frame reel whose six representative frames independently decode with expected visual state.
- 2026-08-14T15:46:20Z stage review start session codex:01a000eb-212e-7640-82f7-a7b11c745b87 — Independently reviewed exact candidate dc4dd45a6e1dc623fa99fe44d673349301ee2d8d in a clean archive and at its public recorder, renderer, process, Godot, ledger, workflow, and movie boundaries.
- 2026-08-14T15:46:20Z stage review end session codex:01a000eb-212e-7640-82f7-a7b11c745b87 — Rejected clean CI verification before build, invalid recorder input mutating before rejection, corrupted absolute render paths, and remaining hash, run-shape, and ordinal multi-file process evidence gaps.
- 2026-08-14T15:46:20Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Reopening implementation to make the event guard self-sufficient on a clean runner, validate frames before stepping, preserve absolute paths, and complete the remaining public evidence rows.
- 2026-08-14T15:53:31Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Built a missing verifier before effective-corpus playback, rejected malformed movement and aim before changing tick or hash, supported absolute renderer paths in the nightly workflow, added every named hash and run-type mutation, and verified an ordinal two-file corpus through the public process; the zero-warning gate passed 163 simulation tests and 37 checker tests.
- 2026-08-14T16:06:00Z stage review start session codex:01a000fd-0cf6-7142-aae8-c71a8445bd6a — Reviewed exact candidate 6628ac2192fbb2f5ea76ff736cb5905c749b0db8 across its real pre-ticket integration range and with a valid one-tick replay in addition to every earlier regression.
- 2026-08-14T16:06:00Z stage review end session codex:01a000fd-0cf6-7142-aae8-c71a8445bd6a — Rejected the candidate because the history guard could not cross commits before ledger introduction and the general renderer applied six golden-specific frame assertions to shorter valid replays.
- 2026-08-14T16:06:00Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Reopening implementation to model pre-ledger history as empty until one-way policy introduction and separate generic replay movie validation from golden-only presentation evidence.
- 2026-08-14T16:11:39Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Made pre-policy commits traversable while requiring the ledger whenever goldens exist and forbidding later removal, proved the real four-commit integration range, and made generic first/middle/last decoding independent of the six canonical-golden state checks; a one-tick replay rendered and validated one frame, and the full gate passed 166 simulation tests plus 37 checker tests.
- 2026-08-14T16:27:26Z stage review start session codex:01a0010d-da47-7f20-adb3-831c90ff5aae — Reviewed exact candidate cecd2c22ba01ddb2b1d52ccd654851fbb0be7050 across the real five-commit range, altered merges, short and canonical rendering, every prior boundary, and a hostile invalid-intermediate replay chain.
- 2026-08-14T16:27:26Z stage review end session codex:01a0010d-da47-7f20-adb3-831c90ff5aae — Rejected the candidate because history checked intermediate replay hash text and ledger transitions but canonically replayed only the final effective tree, allowing a later valid commit to hide an earlier invalid golden.
- 2026-08-14T16:27:26Z stage correction start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Reopening implementation to export and publicly replay every added or replaced golden from the exact commit where it changes, with an invalid-intermediate/later-valid range regression.
- 2026-08-14T16:34:53Z stage correction end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Exported each changed golden's exact Git blob and verified its canonical filename, bytes, and deterministic playback before accepting the transition; the invalid-B/later-valid-C range now fails at B, all 30 history fixtures pass, the real integration range replays its first golden, and the full zero-warning gate passes 167 simulation tests plus 37 checker tests.
- 2026-08-14T16:45:59Z stage review start session codex:01a00122-91f3-7250-b63c-55c236365989 — Independently reviewed exact candidate 11dc0a55d2994c1206c168fdbbe7e44e26947656 across the complete ticket, explicit integration range, hostile intermediate replay, renderer, process, workflow, and residue boundaries.
- 2026-08-14T16:45:59Z stage review end session codex:01a00122-91f3-7250-b63c-55c236365989 — Approved the exact candidate with no findings after the full gate, clean verifier rebuild, A/B/C rejection, one-frame and canonical renders, Godot exits, spec comparison, pins, and cleanup all passed.
- 2026-08-14T16:45:59Z stage integrate start session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Fast-forwarding the approved six-commit replay history to main, preserving the ignored validated reel, and running the explicit pre-ticket integration range before removing every ticket workspace and transfer artifact.
- 2026-08-14T16:45:59Z stage integrate end session codex:019ffea8-55c5-79b3-96b2-da3210d67d84 — Fast-forwarded main to reviewed candidate 11dc0a55d2994c1206c168fdbbe7e44e26947656 and copied the validated 21,389,476-byte local reel; final range verification and residue cleanup follow this bookkeeping commit.
