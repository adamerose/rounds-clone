---
format: 3
status: ready
owner: claude:6c76fdbe-dfe3-46c4-b8c7-77b3aed79a4d
created: 2026-10-05T14:55:57Z
origin: human-request
tags: ["quarrel", "mvp", "network", "steam"]
value: 9
sessions:
  - claude:bcbe88ae-0a32-432f-8fd1-3a061e17847f
  - claude:6c76fdbe-dfe3-46c4-b8c7-77b3aed79a4d
execution: unattended
parent: 75
depends-on: [81, 83]
supersedes: []
split-from: []
---

# Invite a Steam friend to a match with no port forwarding

Friends in different homes cannot join a raw UDP host without port forwarding, which most of the group will not set up.
Steam's lobbies and relay network solve that and match where the game is heading.

## Outcome

- A Steam transport sits beside the UDP one, behind the same interface the match uses.
- From the menu, a player hosts an online match and invites a Steam friend through the Steam overlay or a friends list; the friend joins by accepting the invite or choosing join game in Steam.
- Traffic goes through Steam's relay, so neither player forwards ports.
- The game still runs, with local and UDP play, when Steam is not running.

## Decisions

- Use the `steamworks` Rust crate, whose bundled redistributable needs no Steamworks partner login.
- Development uses Valve's public test app ID 480 from configuration; our own App ID comes with the Steam release (M5), which costs money and is a hard stop.
- No Steam account credentials are entered or stored by the game or by workers.

## Evidence required

- Tests run the match over the transport interface with an in-memory loopback, showing the Steam path uses the same match code as UDP.
- If Steam is already running and signed in when the work is done, the worker shows lobby creation and the invite overlay in a game window verified on monitor 4; otherwise it records that, and this check joins Adam's first play session with the two-home test in #75.
- With Steam unavailable (not running, or initialisation forced to fail by a test setting), the client starts and the online-Steam option explains that Steam is needed.
- The two-home friend test is part of the MVP's first play session and is checked by Adam, not by the worker.

## Chat excerpts

Adam — [this session](http://ivy.localhost/sessions/claude/bcbe88ae-0a32-432f-8fd1-3a061e17847f), 2026-10-05:

> online play should be part of the MVP.

## Work log

- 2026-10-05T14:55:57Z Drafted under run #75.
- 2026-10-05T15:04:43Z Admitted for run #75 after an independent contract check; its fixes were applied first.
- 2026-10-09T10:48:52Z stage implement end session claude:6c76fdbe-dfe3-46c4-b8c7-77b3aed79a4d — Transport-generic live session, quarrel-steam crate, menu Online choice; live Steam lobby and invite overlay shown on monitor 4
- 2026-10-09T10:49:08Z stage verify start session claude:6c76fdbe-dfe3-46c4-b8c7-77b3aed79a4d — tools/verify.ps1 on worktree .ivy/worktrees/084-invite-a-steam-friend-to-a-match-with-no-port-forwarding
- 2026-10-09T10:59:34Z stage verify end session claude:6c76fdbe-dfe3-46c4-b8c7-77b3aed79a4d — tools/verify.ps1 passed fmt, strict Clippy, locked build, doctests and 157 tests on candidate 1b5458c6c8a69848076e437f2ce44d0487193e0f
- 2026-10-09T10:59:50Z stage review start session claude:6c76fdbe-dfe3-46c4-b8c7-77b3aed79a4d — fresh Codex gpt-6.1-sol high review of a971ef49742c9ac0744d764156bfaa4b90a7d85b..1b5458c6c8a69848076e437f2ce44d0487193e0f
- 2026-10-09T11:01:02Z review fallback: codex exec -s read-only failed sandbox setup (helper_unknown_error: setup refresh had errors) and reviewed nothing; rerun with -s danger-full-access and read-only instructions, as tickets 87 and 93 did
- 2026-10-09T11:14:36Z stage review end session codex:01a12052-f3dd-7d61-99e4-b163146f01f8 — APPROVED a971ef49742c9ac0744d764156bfaa4b90a7d85b..1b5458c6c8a69848076e437f2ce44d0487193e0f; one non-blocking note: the Steam host's session-request callback holds its client, so Steam shuts down at process exit rather than on drop (each match is its own process)
