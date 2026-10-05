---
format: 3
status: idea
created: 2026-10-05T14:55:57Z
origin: human-request
tags: ["quarrel", "mvp", "network", "steam"]
value: 9
sessions:
  - claude:bcbe88ae-0a32-432f-8fd1-3a061e17847f
execution: unattended
parent: 75
depends-on: [81]
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
- With Steam running on this machine, the client initialises Steam, creates a lobby and shows the invite flow; recorded with a capture taken on monitor 4.
- With Steam not running, the client starts and the online-Steam option explains that Steam is needed.
- The two-home friend test is part of the MVP's first play session and is checked by Adam, not by the worker.

## Chat excerpts

Adam — [this session](http://ivy.localhost/sessions/claude/bcbe88ae-0a32-432f-8fd1-3a061e17847f), 2026-10-05:

> online play should be part of the MVP.

## Work log

- 2026-10-05T14:55:57Z Drafted under run #75.
