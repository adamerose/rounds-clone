# ROUNDS clean-room rewrite

This repository starts from the two supplied ten-minute ROUNDS recordings and builds the clone in Rust with Bevy.
Five footage-derived slices exist: a teal duel, an explosive timber collapse, the blue 4–5 victory through rematch and two-player card draft into upgraded combat, the radial-saw duel through `HALF BLUE`, and the yellow-crate terminal blast through `ROUND ORANGE`.
The rematch path now continues through both opening card drafts, the first two half results, the timber collapse, the deciding ice duel, the first full-round award, and the losing fighter's five-card `QUICK SHOT` draft in one match.
Two clients drive one 60 Hz Rapier authority through sequenced local UDP input, the same rules run as a client-host, and the shared Bevy 2D scene renders visibly or to offscreen evidence frames.

## Build and verify

```powershell
cargo fmt --all -- --check
cargo clippy --workspace --all-targets --locked -- -D warnings
cargo build --workspace --locked
cargo test --workspace --locked
.\out\cargo-target\debug\rounds-automation.exe smoke --seed 38 --ticks 786 --output-dir out\ticket-039\smoke
.\out\cargo-target\debug\rounds-automation.exe inspect --seed 38 --ticks 786
.\out\cargo-target\debug\rounds-client.exe capture-replay --seed 38 --ticks 786 --output-dir out\ticket-039\anchors --metadata out\ticket-039\anchors.json
.\out\cargo-target\debug\rounds-client.exe visible --seed 38 --ticks 786 --frames 180
.\out\cargo-target\debug\rounds-automation.exe smoke --profile rematch-draft-replay --seed 41 --ticks 2400 --output-dir out\ticket-041\smoke
.\out\cargo-target\debug\rounds-client.exe capture-replay --profile rematch-draft-replay --seed 41 --ticks 2400 --output-dir out\ticket-041\anchors --metadata out\ticket-041\anchors.json
.\out\cargo-target\debug\rounds-client.exe visible-flow --profile rematch-draft-replay --seed 41 --ticks 2400 --automated
.\out\cargo-target\debug\rounds-automation.exe smoke --profile yellow-crate-terminal-blast-replay --seed 43 --ticks 155 --output-dir out\ticket-043\smoke
.\out\cargo-target\debug\rounds-client.exe capture-replay --profile yellow-crate-terminal-blast-replay --seed 43 --ticks 155 --output-dir out\ticket-043\anchors --metadata out\ticket-043\anchors.json
.\out\cargo-target\debug\rounds-client.exe visible --profile yellow-crate-terminal-blast-replay --seed 43 --ticks 155 --frames 155
```

The smoke result proves that both client processes handshake, send monotonic input sequences, receive every progressive snapshot, agree with the authority and local host, and bind one real Bevy render to the received final state.
The 2,400-tick rematch replay capture retains thirteen named 1280×720 anchors. The complete 5,941-tick route emits 57 anchors: the previous 48 through the loser draft and bridge plus nine held hanging-arena entry anchors. Their metadata identifies the source recording and native PTS/RGBA identity, replay input, state, renderer, executable, and frame.


## Play the connected match

Run `out/cargo-target/debug/rounds-client.exe visible-flow --profile rematch-draft-replay --seed 41 --ticks 18000` for a bounded five-minute local session. The window starts hidden and appears only after verifying the project's monitor-4 placement.
At `REMATCH?`, orange accepts with Y and blue with Enter. Use left/right arrows and Enter to choose each player's card. The opening fans can confirm only Dazzle and Explosive Bullet; Quick Shot's projectile-speed capability is added later by the loser draft. The same match carries those cards through the fights needed to finish the first round.

| Input | Orange | Blue |
|---|---|---|
| Move | A / D | Left / Right |
| Jump | W | Up |
| Block | S | Down |
| Fire | Space | Enter |
| Aim up / left / down / right | I / J / K / L | Numpad 8 / 4 / 5 / 6 |

Without a manual aim direction, aim follows the opponent from the latest observation. Controllers use the left stick to move, right stick to aim, south button to jump, west button to block and right trigger to fire; D-pad and south button control the draft.
The first player to win two fights earns a full round. Either color can win both opening fights and finish there; a split sends both players into the ice arena. The result keeps the losing player's half visible, fills the winner's circle and moves the award into a completed-round HUD pip. When the result clears, current halves reset, completed rounds and loadouts remain, and only the completed round's loser drafts. The seed-41 route hovers `OVERPOWER`, selects `QUICK SHOT`, retains Dazzle, and accumulates a typed per-fighter projectile-speed multiplier; the other four new cards are visible but catalog-only.
For an automated demonstration of the full connected route, run `out/cargo-target/debug/rounds-client.exe visible-flow --profile rematch-draft-replay --seed 41 --ticks 5941 --automated`.
For the two-client development-transport check, run `out/cargo-target/debug/rounds-automation.exe smoke --profile rematch-draft-replay --seed 41 --ticks 5941 --output-dir out/ticket-055/smoke`.
Capture all 57 shared-renderer entries with `out/cargo-target/debug/rounds-client.exe capture-replay --profile rematch-draft-replay --seed 41 --ticks 5941 --output-dir out/ticket-055/anchors --metadata out/ticket-055/anchors.json`. These commands extend the existing profile; smoke sessions remain bounded to 6,000 exchanged ticks.

## What is and is not implemented

The rematch slice adds an authoritative blue winner and orange elimination, the exact prior-card badges, explicit accepted-rematch reset, phase revisions, per-player votes, seeded five-card offers, active-player validation, typed persistent loadouts, Dazzle stun pulses, Explosive Bullet impacts, item-specific card art and pose response, and a source-timed return to combat.
The catalog contains 14 definitions. Dazzle, Explosive Bullet, and Quick Shot are implemented; the other eleven definitions are catalog-only and cannot be confirmed. The separate general implemented-offer pool contains those three implemented entries, but it does not drive the fixed source-shaped offer lists.
The teal slice has stable Bevy ECS identities, Rapier bodies and contacts behind a private boundary, static stepped geometry, movement and air control, jumping, aiming, recoil, CCD bullets, reflection, damage-scaled knockback, a terminal upper-right impact, one winner, a real Bevy renderer, and live authoritative snapshots.
Jump height is variable in every arena: letting go of jump while airborne and still rising cuts the remaining upward speed to 30 %, so a held jump rises 116.86 px over 23 ticks and a jump let go of eleven ticks after take-off rises 85.12 px over 14 ticks. Holding jump changes nothing, and a release read on a grounded tick does nothing whatever the vertical speed. Letting go while stunned or eliminated counts as letting go on that tick, so being stunned mid-jump never cuts the jump short once the stun wears off. No shipped replay script exercises the cut yet — every scripted release falls on a grounded or already-falling tick, which is why all 73 capture anchors are byte-identical across the change — so the first deliberate use of it will be the ice route ticket 049 owns.
Ring-out remains a separately tested simulation capability; the named replay ends before the result transition and records no ring-out.
Its named replay profile matches this card-modified interval without claiming base-game constants.
The radial-saw slice adds stable authoritative moving hazards, ordinary projectile feedback, a moving painted background, and the adjacent `HALF BLUE` result handoff without claiming unobserved saw damage.
The yellow-crate slice adds stable-ID dynamic Rapier crates, an authority-owned terminal projectile contact and blast impulse, blue elimination and orange scoring, and one private final-composite fullscreen pass for the source-proved discrete radial RGB echoes. Visible and offscreen runs use that same GPU scene and effect pass; its eleven capture anchors preserve the adjacent tick-109 combat, tick-110 result onset, and tick-111 larger result transition.
The connected ice extension adds seventeen static polygon contours shared by collision and rendering, animated cyan/pale paint, long shadows, arena arrival/departure motion and a symmetric first-round award. Current half progress and completed rounds are separate: the source's blue/orange/blue sequence ends with halves 1–2 and rounds 0–1, with Da and Ex retained. Ordinary damage decides each fight; the ice interval adds no friction, fracture or melting rule. The source anchors and remaining visual differences belong in `docs/fidelity/ice-round-observations.md`.
An eliminated fighter, including one that leaves the arena with health remaining, accepts no further movement, aim, block or fire input, and each fight's outcome is decided once from who remains alive. When both fighters fall on the same tick, nobody scores: halves, completed rounds and cards stay as they were, and after the usual result pause the same arena repeats without a draft. That no-award rule is provisional because neither recording shows a simultaneous elimination.
Production reliability and Steam transport, the remaining card mechanics, other arenas, the rest of the match lifecycle, audio, and menus remain explicit gaps in `docs/fidelity/footage-coverage.md`.

## Recover the retired prototype

The final Godot and C# prototype is preserved by the annotated tag `archive/godot-csharp-prototype-2026-09-03`.
See `docs/legacy-prototype.md` for read-only lookup examples.
