---
format: 3
status: ready
owner: codex:01a10efe-cc85-7522-81c7-22387758b422
created: 2026-10-05T21:57:36Z
origin: system-detected
tags: ["quarrel", "network", "tests"]
value: 6
sessions:
  - codex:01a10d8d-8dee-7fb1-8831-37de3ce8c719
  - codex:01a10efe-cc85-7522-81c7-22387758b422
execution: unattended
parent: 75
depends-on: []
supersedes: []
split-from: []
---

# Isolate parallel UDP fixtures from released-port reuse

During ticket 77 verification, the unchanged parallel network suite's protocol-11 server rejected a protocol-12 packet.
The nearby join-timeout fixture drops its reserved socket before LiveClient repeatedly sends protocol-12 Hello packets for five seconds.
Windows can reuse that destination for another test server; this is a concrete plausible route, but no packet trace established which sender caused the observed failure.
A separate run failed join_timeout_and_bad_state_are_named_failures and left the old-session fixture waiting indefinitely with only its peer socket remaining.

## Outcome

Parallel network fixtures retain exclusive ownership of their intended endpoints and finish with a named failure when their authority exits.
Keep the production protocol behavior and the existing checks for missing authority, invalid snapshots and stale-session input.

## Decisions

Fix the test isolation or the demonstrated owning boundary; do not lengthen timeouts, suppress protocol errors or skip checks to make the suite pass.
Do not redesign production networking without evidence that the public runtime has the same defect.
Edits are confined to `#[cfg(test)]` fixtures in crates/quarrel-network. Ticket 81 may touch the same files: rebase onto it if it lands first.

## Evidence required

- Show on current main whether any fixture still releases a reserved endpoint while a live sender targets it. If one does, reproduce the collision deterministically and keep the resulting unsupported-protocol error. If none does, record that #78 closed the route.
- Run the complete default-parallel locked workspace suite with all checks enabled.
- Show that the stale-session fixture exits when its authority fails instead of looping forever.

## Work log

- 2026-10-05T21:57:36Z Discovered in ticket 77's final verification. Retained logs: out/ticket-077-delivery/ticket-077-entry-tests.log and ticket-077-final-tests.log. Sources: crates/quarrel-network/src/live.rs join_timeout_keeps_the_absent_socket_bound (fixed by #78 to hold its socket; base bc6184b) and old_session_input_from_a_current_peer_is_ignored; lib.rs two_udp_clients_stream_monotonic_inputs_and_progressive_snapshots.
- 2026-10-06T02:15:16Z Admitted for run #75 after an independent contract check updated it to main at bc6184b.
