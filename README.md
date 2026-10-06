# QUARREL (working title)

A spiritual successor to ROUNDS for Adam and his friends, built in Rust with Bevy and Rapier.
See `GOAL.md`, `docs/game-design.md` and `docs/roadmap.md` for the intended game.

An ordinary match starts with every fighter picking one card. Each fight awards one point to the last fighter standing, then everyone who did not win picks another card.
If the last fighters die together, the match seed decides the point. Arena files are shuffled and used without repeats until every arena has been used.
The default is two fighters, first to five points and five cards per offer.
At match end, every fighter chooses run it back or new match, sees everyone's choice, and can change theirs until everyone agrees.
Running it back keeps scores and cards and raises the target to ten, then fifteen; a new match clears scores and cards.

## Build and verify

```powershell
cargo fmt --all -- --check
cargo clippy --workspace --all-targets --locked -- -D warnings
cargo build --workspace --locked
cargo test --workspace --locked -- --test-threads=1
out/cargo-target/debug/quarrel-automation smoke --seed 38 --ticks 1200
out/cargo-target/debug/quarrel-client capture --seed 38 --ticks 30 --output out/frame.png
```

Cargo uses two jobs and the reusable `out/cargo-target` directory from `.cargo/config.toml`.
The smoke runs an authority and two UDP clients through ordinary drafts, checking progressive snapshots and final state agreement.
`assets/replays/ordinary-match.json` contains starting cards and arenas plus player inputs for a short ordinary match; the simulation tests replay it twice and compare every snapshot.
Adding or editing live assets does not change that recording.
Use `quarrel-client replay --input assets/replays/ordinary-match.json` to print its final authority snapshot.

## Play locally

The client opens a menu when launched without arguments. Select Local match, Host on port 7777, or Join; type the host's IPv4 address and port in Address.
Click a choice, or use arrows/D-pad and Enter/south button. The menu returns when the match window closes.
Connect controllers before starting; reconnecting or changing their count can change fighter assignment.
For local play, one controller controls fighter two and keyboard/mouse controls fighter one; two controllers control both fighters.
While a fighter's controller is idle, that fighter's keyboard controls still work in combat; any mapped stick or combat button input beyond the dead zones gives the controller precedence.
Keyboard/mouse uses A/D, Space/W jump, S crouch, Shift or right mouse block, F or left mouse fire, and mouse aim. Existing keyboard aim and two-keyboard controls remain available.
Interactive Host/Join waits until cancelled and has no timed match cutoff. Escape or close the waiting/game window to leave.
Online players each use their own primary keyboard/mouse layout or controller, and the keyboard keeps working in combat while the controller is idle. Card choices and match-end votes use the same device as combat.
Headless UI evidence: `quarrel-client menu-capture --output out/menu.png`; `menu-start --choice 1|2 --headless --ticks 90` exercises the same host/join menu selections.

```powershell
out/cargo-target/debug/quarrel-client visible-flow --seed 38 --ticks 18000
```

Capture metadata includes executable, PNG and state hashes. `capture --metadata` and scripted `remote --render-output --render-metadata` retain provenance; PNG and metadata paths must differ.

Project windows open hidden and appear only after placement on monitor four is verified.

| Input | Fighter one | Fighter two |
|---|---|---|
| Move or navigate draft | A / D | Left / Right |
| Jump | Space / W | Up |
| Crouch | S | Down |
| Block | Right mouse / Left Shift | Right Shift |
| Fire | Left mouse / F | Enter |
| Confirm card | Space | Enter |
| Aim up / left / down / right | I / J / K / L | Numpad 8 / 4 / 5 / 6 |
| Run it back / new match | Y / N | K / L |

Fighter one aims at the mouse cursor; keyboard aim keys take priority over the cursor.

Controllers use the left stick to move, right stick to aim, south button to jump, west button to block and right trigger to fire. Down on the left stick crouches.
D-pad and south button choose a card. At match end, south chooses run it back and east chooses a new match.

## Live development sessions

```powershell
out/cargo-target/debug/quarrel-server dedicated --port 41000 --ticks 18000
out/cargo-target/debug/quarrel-client join --address 127.0.0.1:41000 --client 0 --ticks 18000
out/cargo-target/debug/quarrel-client join --address 127.0.0.1:41000 --client 1 --ticks 18000
```

Local keyboard/controller presentation and paced live sessions support the shipped two fighters. The general simulation and scripted transport also accept other fighter counts.
Direct-IP UDP development sessions use one authority. Steam invitations and prediction are later MVP tickets.

## Card data and match configuration

Cards are RON files under `assets/cards/`. Add a file to put another card in the pool; no Rust enum or offer list needs changing.
Every file has a unique string `id`, a `name`, a one-sentence `description` and `stat_changes`:

```ron
(
    id: "grow",
    name: "Long Haul",
    description: "Shots grow and hit harder with distance, but start weaker.",
    stat_changes: (damage_factor_milli: 700),
    event_rules: [(on: Fire, effects: [Grow(180)])],
)
```

The format supports added health, damage, movement speed and magazine rounds (`magazine_bonus`), and projectile-speed, damage and fire-interval multipliers in thousandths.
`event_rules` contains `on`, optional `max_depth` (default 8), and `effects`, as the twelve bundled cards demonstrate.
Events are Fire, Hit, Impact, Block, Bounce, Land, TakeDamage and Kill; effects include shots, reloads, teleports, repeat blocks, explosions, poison and in-flight changes.
Omitted changes are neutral. Taking the same card again stacks its changes; an offer contains no repeated card.
Normal matches reload the pool every fifteen ticks; invalid edits retain the last valid cards and expose `card_reload_error()`.
Held and offered IDs cannot be removed during a match. New files enter later offers, and edits to held cards affect future shots.
Every shot inherits current Fire flight changes. First reactions are reliable; further triggered generations halve their chance and stop at depth eight. Echo repeats a primary block once.
Set `QUARREL_CARD_DIR` to select a different pool. Empty pools, duplicate IDs and offers larger than the pool are rejected.

`MatchConfig` controls fighter count, target score, offer size, run-back limit and seed.
The simulation accepts one to 255 fighters; two is the shipped setting. Arenas provide spawn points and additional fighters receive deterministic positions within the frame.
CLI configuration uses `--fighters`, `--target-score`, `--offer-size`, `--run-it-back-limit` and `--seed`.
Arena authoring and previews are documented in `docs/design-docs/arena-data.html`.

## Recover the retired prototype

The final Godot and C# prototype is preserved by the annotated tag `archive/godot-csharp-prototype-2026-09-03`.
See `docs/legacy-prototype.md` for read-only lookup examples.

Combat values live in `assets/tuning.ron` (pixels, pixels/second, and ticks at 60 Hz).
Ordinary matches reload valid edits every 15 ticks without resetting scores, health or cards;
invalid edits retain the last valid values, print a rejection once per changed error, and are available through `tuning_reload_error()`.
The base magazine holds three shots and automatically reloads when empty. Press block again after
its cooldown; holding it does not repeat it. Ground/wall contact restores one jump, crouching
halves the grounded collider and accelerates falling, and all four arena edges damage and
push inward. An active block at an edge cancels that damage and launches inward.
Set `recoil_enabled: false` to compare movement without firing recoil. Recordings pin their
starting tuning. Replays do not watch the live file.

Each network peer uses the fighter-one keyboard bindings for its own fighter.
Fighter-two keys also work as aliases in a network client.
