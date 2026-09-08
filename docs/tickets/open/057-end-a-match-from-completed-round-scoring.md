---
format: 3
status: ready
created: 2026-09-08T14:09:05Z
origin: agent-proposed
tags: ["rounds", "bevy", "match-flow", "networking", "presentation", "fidelity"]
value: 8
risk: 4
sessions:
  - codex:01a07f87-97d0-7751-825a-29e87e8fe64a
execution: unattended
depends-on: [54]
supersedes: []
split-from: []
---

# End a match from completed-round scoring

The authority awards completed rounds but never lets those awards end a match. Its only concluded match is hard-coded into `FlowAuthority::new`, so the visible 4–5 victory and rematch can be replayed but never reached. Add the first real score-driven match boundary: a fifth completed round finishes the ordinary result envelope, enters an authoritative `WAITING` state, and remains there without opening another loser draft.

## Outcome

- A normal second-half award that raises either fighter's completed-round score to five follows the existing elimination, half-result, and `ROUND ORANGE` or `ROUND BLUE` envelope, then enters a distinct `Waiting` phase instead of `PostRoundDraft`. A score below five keeps the current loser-only post-round draft behavior.
- `Waiting` retains the final completed-round score, winner, badge stacks, loadouts, capabilities, and the live arena. On entry, clear projectiles and respawn both fighters at that profile's existing arena spawn anchors with live, full-health ECS state, so projectile and ring-out conclusions both produce the source-visible pair. Combat, draft, and rematch commands are rejected, further eliminations cannot score, and ordinary advancement leaves the state stable apart from its saturating phase age.
- The shared renderer displays literal `WAITING`, five filled winner pips and the loser's retained pips over the live scene. It reads the same typed snapshot for authority-local and received network state; it does not infer match completion from a replay profile or tick.
- A bounded `match-end-waiting-replay` starts from an explicitly constructed 3–4 completed-round match point with one half each, reaches blue's fifth point through a real public-input projectile elimination, preserves the normal result envelope, and ends in `Waiting` at 3–5. Its prehistory is reported as constructed, and it claims fidelity only for the match-score, state ordering, HUD, retained badges, visible fighters, and `WAITING` overlay.
- `FlowAuthority::new` represents a fresh 0–0 match with no winner, eliminated fighter, prior badges, or inherited build. The existing connected rematch profile uses an explicitly named historical 4–5 constructor so all of its delivered phases, offers, actions, loadouts, and timing stay unchanged.
- The authoritative snapshot and UDP protocol versions advance together for the new serialized flow phase. Old peers reject the new protocol. Existing five replay profiles retain their authoritative and visual behavior; protocol-bearing hashes may move only by the documented version and enum change.

## Decisions

- `docs/fidelity/match-end-waiting-observations.md` is the source contract. At PTS 2000158666 the second recording shows `WAITING`, both fighters, retained badges, and a 3–5 completed-round HUD. The first recording independently ends 4–5. Five is therefore the supported terminal score; win-by-two is neither required nor claimed.
- The branch belongs after the existing full-round result envelope, where `RoundBlue` and `RoundOrange` currently open `PostRoundDraft`. Entering `Waiting` directly from `record_elimination` would erase an already delivered visible result sequence.
- `Waiting` is a terminal gameplay state, not the existing `TerminalMatch`: `TerminalMatch` means a player rejected a rematch, while the source `WAITING` frame retains the live arena and both fighters. Reusing that name would merge two observably different states.
- Respawn and revive both ECS fighters on entry while leaving the arena itself loaded. Reviving state alone is insufficient after a ring-out because the eliminated physics body remains beyond the kill boundary and would still be offscreen. Clear projectiles and transient draft focus/selection so no stale combat or card interaction remains visible; retain score and builds exactly.
- The frame at PTS 2039991840 proves that a new 0–0 match follows, but not what triggers it. Automatic delay, host action, peer consensus, lobby readiness, card offers, and exact new-match cadence are outside this ticket. `Waiting` therefore has no exit here.
- The new replay is evidence machinery for a missing product rule, not a second implementation of it. It must initialize through a typed flow constructor, drive the same fixed-tick authority and public input path as online play, and render through the shared scene. No profile-only score award, forced winner, scripted overlay, or snapshot mutation is allowed.
- No weapon, damage, health, projectile, knockback, movement, arena-physics, card-formula, or network-prediction value changes. Ticket 056's blocked reload implementation is neither a dependency nor an input to this work.

## Evidence

- Re-decode and hash both exact source frames from the identified recording with the retained FFmpeg 7.1 method. Inspect them at native resolution and preserve the command arrays, byte counts, hashes, and observations; use the later frame only to demonstrate why reset remains excluded.
- Focused flow regressions drive both winner colours to a fifth completed round, prove the ordinary result envelope precedes `Waiting`, prove a 4–4 or 3–4 full-round result still opens the loser draft, and prove retained terminal state, rejected actions, no duplicate award, stable advancement, and simultaneous-elimination behavior.
- A public `AuthoritativeMatch::step` regression for `match-end-waiting-replay` proves a legal input projectile causes the deciding elimination, the score becomes 3–5 exactly once, both ECS fighters are alive at the profile's visible spawn anchors on entry, projectiles are gone, later movement/fire/flow inputs cannot change gameplay state, and no private winner or score mutation occurs after construction. A second public authority regression ends the fifth round by crossing a kill boundary and proves the offscreen loser is repositioned to its visible spawn anchor in `Waiting`.
- Serialization and two-client regressions prove the new phase round-trips, a mismatched protocol is rejected, both clients observe the same monotonic result-to-round-to-waiting sequence and final digests, and no client reconstructs the score or phase locally.
- Shared-renderer tests prove local and received snapshots produce `WAITING`, five blue pips, three orange pips, retained badges, and two fighters, with no draft fan or rematch prompt. A headless capture compares those regions with PTS 2000158666 while recording the intentionally unmatched arena and route.
- `inspect`, bounded replay capture, and two-client smoke expose the constructed-prehistory label, decisive event, final score, waiting phase, visible fighters, retained builds, agreement flags, and clean shutdown. One guarded visible run is placed and verified on monitor 4 before it is shown.
- Re-run all existing replay profiles and their retained anchors. Attribute protocol-bearing hash changes field by field and require every non-protocol source, flow, round, loadout, event, arena, body, constraint, saw, and frame observation to remain unchanged.
- Run sequentially on the prepared `out/cargo-target` under the repository two-job cap: format, strict all-target Clippy, locked build, and the complete locked workspace test suite with no failures or ignored tests. Run the ticket checker and `git diff --check`.

## Work log

- 2026-09-08T14:06:20Z stage research start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — Reconsidering an independent core-flow slice after ticket 056 returned blocked and checking the coverage ledger's named score-driven match-award gap against the shipped authority.
- 2026-09-08T14:09:05Z stage research end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — The two recordings establish terminal scores of 3–5 and 4–5, while the code establishes that completed rounds never branch to a match end and the only victory state is constructed. The later 0–0 draft proves a reset exists but not its trigger, so it stays outside this slice.
- 2026-09-08T14:09:05Z stage design start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — Shaping a finite fifth-round boundary, stable Waiting state, explicit historical/fresh constructors, shared rendering, public replay, transport evidence, and preservation contract.
- 2026-09-08T14:09:05Z stage design end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — The contract reuses the ordinary result envelope, distinguishes Waiting from rematch rejection, binds terminal score five, and defers every unsupported reset trigger, combat calibration, card, arena, and production-network decision.
- 2026-09-08T14:12:31Z — Re-decoded both exact source PTS with imageio-ffmpeg FFmpeg 7.1 at two threads. Each output is 3,686,400 packed RGBA bytes and reproduces the tracked hashes: `c4c954…9a6d` for the 3–5 WAITING frame and `7f8703…8da0` for the later empty-score orange draft. Native inspection confirms the contract's visible facts and its refusal to infer the reset trigger; commands and full identities are retained under `out/ticket-057/source/`.
- 2026-09-08T14:14:39Z stage review start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a0810e-331e-7d80-bddb-f7b7644813c7 — Fresh-context admission review began on exact range a8341173ea0661cebeec018a7710968e1483b734..1598b494db08503fb1d41c878c68747e9e630f78.
- 2026-09-08T14:16:53Z stage review end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a0810e-331e-7d80-bddb-f7b7644813c7 — RETURN on one finding: reviving ECS state without repositioning cannot make a fighter eliminated beyond a kill boundary visible in Waiting. Source identities, dependency, finite scope, risk 4, ticket validation and whitespace otherwise passed.
- 2026-09-08T14:16:53Z stage correction start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — Replacing the impossible no-respawn promise with one entry operation that clears projectiles, respawns both physics bodies at the profile's existing arena anchors and revives both ECS states; adding a public ring-out terminal regression.
- 2026-09-08T14:16:53Z stage correction end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a — Projectile and ring-out match conclusions now share one testable visible-pair outcome without changing the live arena, final score or retained builds.
- 2026-09-08T14:17:46Z stage review start session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a0810e-331e-7d80-bddb-f7b7644813c7 — Re-reviewing the exact amended range a8341173ea0661cebeec018a7710968e1483b734..3eb7a1f3c05877f4b36dca5e355621c5306f6ee7 against the returned ring-out finding and the full admission bar.
- 2026-09-08T14:19:02Z stage review end session codex:01a07f87-97d0-7751-825a-29e87e8fe64a/01a0810e-331e-7d80-bddb-f7b7644813c7 — ADMIT at risk 4 with no remaining findings. The explicit respawn-and-revive entry operation and public ring-out regression resolve the offscreen-loser path while retaining arena and terminal state; source identities, dependency, finite scope, ticket validation and whitespace pass.
- 2026-09-08T14:19:02Z — Admitted: status set to ready by the top-level session codex:01a07f87-97d0-7751-825a-29e87e8fe64a. Implementation remains unreviewed.
