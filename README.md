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

The client opens a menu when launched without arguments. Select Local match, Host on port 7777, or Join; type the host's IP address and port in Address.
Click a choice, or use arrows/D-pad and Enter/south button. The menu returns when the match window closes.
For local play, one controller controls fighter two and keyboard/mouse controls fighter one; two controllers control both fighters.
Keyboard/mouse uses A/D, W jump, S or right mouse block, Space or left mouse fire, and mouse aim. Existing keyboard aim and two-keyboard controls remain available.
Online players each use their own primary keyboard/mouse layout or controller. Card choices and match-end votes use the same device as combat.
Headless UI evidence: `quarrel-client menu-capture --output out/menu.png`; `menu-start --choice 1|2 --headless --ticks 90` exercises the same host/join menu selections.

```powershell
out/cargo-target/debug/quarrel-client visible-flow --seed 38 --ticks 18000
```

Capture metadata includes executable, PNG and state hashes. `capture --metadata` and scripted `remote --render-output --render-metadata` retain provenance; PNG and metadata paths must differ.

Project windows open hidden and appear only after placement on monitor four is verified.

| Input | Fighter one | Fighter two |
|---|---|---|
| Move or navigate draft | A / D | Left / Right |
| Jump | W | Up |
| Block | S | Down |
| Fire or confirm card | Space | Enter |
| Aim up / left / down / right | I / J / K / L | Numpad 8 / 4 / 5 / 6 |
| Run it back / new match | Y / N | K / L |

Controllers use the left stick to move, right stick to aim, south button to jump, west button to block and right trigger to fire.
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
    id: "stamina",
    name: "Stamina",
    description: "Gain 20 health.",
    stat_changes: (health: 20),
)
```

The placeholder format supports added health, added shot damage, added movement speed in world units per second, and a projectile-speed multiplier in thousandths.
Omitted changes are neutral. Taking the same card again stacks its changes; an offer contains no repeated card.
The five original placeholder stat cards are replaced and the format extended by ticket 80.
Set `QUARREL_CARD_DIR` to select a different pool. Empty pools, duplicate IDs and offers larger than the pool are rejected.

`MatchConfig` controls fighter count, target score, offer size, run-back limit and seed.
The simulation accepts one to 255 fighters; two is the shipped setting. Arenas provide spawn points and additional fighters receive deterministic positions within the frame.
CLI configuration uses `--fighters`, `--target-score`, `--offer-size`, `--run-it-back-limit` and `--seed`.
Arena authoring and previews are documented in `docs/design-docs/arena-data.html`.

## Recover the retired prototype

The final Godot and C# prototype is preserved by the annotated tag `archive/godot-csharp-prototype-2026-09-03`.
See `docs/legacy-prototype.md` for read-only lookup examples.
