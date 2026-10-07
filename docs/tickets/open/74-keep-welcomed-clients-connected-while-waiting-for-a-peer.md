---
format: 3
status: ready
owner: claude:b7fc79ea-8bb3-4f55-9da8-6ce563ee13be
created: 2026-10-02T16:19:26Z
origin: agent-proposed
tags: ["rounds", "completion", "implementation"]
value: 8
risk: 3
sessions:
  - codex:01a0fd03-d222-7cf2-a225-29c243f2337e
  - claude:b7fc79ea-8bb3-4f55-9da8-6ce563ee13be
execution: unattended
parent: 59
depends-on: [65]
supersedes: []
split-from: []
---

# Keep welcomed clients connected while waiting for a peer

The live server allows five seconds for both players to join, but the first welcomed client starts a three-second silence timer before snapshots begin. A second player can arrive within the valid join window after the first player has already disconnected.

## Outcome

- A welcomed first client remains connected throughout the authority's valid peer-joining window, and a second client arriving after three seconds but before that window closes starts both clients.
- An absent second peer produces a bounded startup-specific failure. Once play begins, authority loss still terminates clients within the existing three-second running-session silence bound.

## Decisions

- Represent the distinction between waiting for the other peer and running gameplay at the current live protocol boundary. Gameplay stays stopped until both peers join.
- Fix the conflicting lifecycle assumptions; do not globally lengthen the running silence timeout, add blind retries or swallow failures. Keep existing join and running timeout behavior bounded and explicit.
- Reuse the existing dedicated and client-host authority path, nonce/session validation, ordered flow commands, progressive snapshots and terminal acknowledgement. No generic transport framework, Steam, prediction or authentication changes.
- If packet shape changes require a protocol version change, update both ends and mismatch handling together. Preserve current snapshot and simulation semantics.
- Verification uses real loopback UDP and attributable threads. Wait for the sole Cargo/GPU slot before native checks, reuse root out/cargo-target with two jobs, and configure monitor4 before any visible evidence.

## Evidence required

- Reproduce the original failure through the public live server/client interfaces with the first client welcomed and the second arriving after the running silence interval but before the authority join deadline. Preserve the smallest stable regression.
- Verify staggered successful arrival, absent-peer startup termination, authority disappearance during startup and after gameplay begins, and normal simultaneous startup. No test may pass by suppressing the existing silence check.
- Verify both peers agree on progressive authority state after startup, all threads terminate, and the same port can be rebound. Cover the client-host reuse of the same authority path.
- Run format, strict all-target Clippy, locked workspace build/tests and focused live UDP checks sequentially on the prepared target. Record timing uncertainty without adding arbitrary production timeout padding.
- Complete fresh other-family exact-range review and guarded ticket delivery; do not claim nonlocal or production-network reliability from loopback checks.

## Work log

- 2026-10-02T16:19:26Z stage design start session codex:01a0fd03-d222-7cf2-a225-29c243f2337e — Shaped from the completed independent Astra planning batch for goal59; no implementation claim.
- 2026-10-02T16:19:26Z stage design end session codex:01a0fd03-d222-7cf2-a225-29c243f2337e — Idea contract ready for separate fresh-context admission.
- 2026-10-02T16:29:58Z Fresh independent admission codex:01a0fd6c-3364-7ce1-9a79-6871e54c967f ADMIT unchanged at risk3, dependency65, with no operator decision. The five-second peer join window conflicts with the welcomed client three-second silence timer. Explicit startup/running states, real UDP timing regression, bounded loss behavior and port reuse bound the correction. The executing worker records this admission in docs/decisions.md with its reviewed candidate. Native verification waits for the assigned shared resource slot; no executed reproduction or game delivery is claimed by admission.
- 2026-10-07T06:34:00Z stage implement end session claude:b7fc79ea-8bb3-4f55-9da8-6ce563ee13be — candidate ed7bea311b43557ce48448c46e78d01c569c7771 (live protocol 14 Waiting/JoinTimeout); new live UDP and client-host CLI tests failed on unfixed code and pass; fmt, strict Clippy, locked build and workspace tests pass on out/cargo-target
- 2026-10-07T06:35:55Z Local Windows Firewall interruption fix landed on origin/main in 1f185490f88a2431a608001137e66e108070749d. Both live and replay clients bind loopback for loopback authorities; remote networking preserved. Fresh Claude review approved; 26 exact frozen network tests, strict Clippy, format, docs and 2400-tick automation smoke passed. Existing worker checkouts need this commit before further local UDP verification; older binaries can still trigger prompts.
- 2026-10-07T06:39:56Z Firewall fix delivery residue: clean integrated worktree .ivy/worktrees/firewall-loopback is retained. Automatic approval review rejected explicit build-target junction cleanup as blocked by policy; guarded Ivy remove then refused ignored out/cargo-target. The shared prepared Cargo target was preserved and code is already on origin/main.
- 2026-10-07T06:45:02Z stage review end session claude:b7fc79ea-8bb3-4f55-9da8-6ce563ee13be — fresh Codex reviewer codex:01a11512-4a1e-7033-a09b-027c5a4dad09 approved 20b9e28b0854e53d46b7e589172c2f0be07473d9..bf148c7da218b153dcc7a1fbd236deb315c56e6b with no blocking findings
- 2026-10-07T06:45:33Z stage integration start session claude:b7fc79ea-8bb3-4f55-9da8-6ce563ee13be — reviewed 20b9e28b0854e53d46b7e589172c2f0be07473d9..bf148c7da218b153dcc7a1fbd236deb315c56e6b, code-identical rebase of the single commit onto current main
