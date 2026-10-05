# ROUNDS maps

A catalogue of every map in ROUNDS, for reference while designing QUARREL's own arenas.
QUARREL's arenas are our own layouts built from the same kinds of objects; nothing here is copied into the game.

## Sources and limits

- The list comes from the public community spreadsheet ["ROUNDS maps community names"](https://docs.google.com/spreadsheets/d/1HFc2Svqcbpib71damoUTUfLsuxDcNt0c9WSFHgKYvpI/edit?usp=sharing): 70 maps, each with ROUNDS' internal name, a community nickname and one preview image.
  Steam advertises "70+ maps", which matches.
- Six more maps existed at launch and were later removed (listed at the end).
- Descriptions come from looking at each preview on 2026-10-04, plus the internal names, which often say what a map is about (`Destructible_…`, `Phys_Hang…`, `MovingParts…`).
  A still image cannot show motion, which pieces break, or ropes (previews do not draw them), so those details are inferred and marked "probably" where unsure.
- The preview images are ROUNDS screenshots and stay out of Git.
  Labelled contact sheets of all 70 are kept locally under the ignored `out/rounds-map-previews/` folder of the main checkout.

## How to read a preview

- **Bright, saturated shapes** are solid ground that never moves.
- **Muted, brownish or greyish, slightly see-through shapes** are physics objects: shots and fighters push them, they fall and topple, and many break.
- **Small hollow squares** are anchor points; physics pieces hang from them on ropes.
- **Dark vertical bars** are background pieces (poles, props) that fighters pass in front of.
- **Red or orange cogs** are saws.

## Kinds of map

| Kind | Count | What happens |
|---|---|---|
| Static | 19 | Solid layouts only; the fight is pure movement and shooting. |
| Saws | 7 | Static or physics layouts with saw hazards. |
| Moving parts | 4 | Platforms or pieces that move or pulse on their own. |
| Physics stacks | 22 | Towers, scaffolds and crate piles standing on the ground or on poles; shooting them topples them onto fighters. |
| Hanging | 15 | Pieces hung from anchors on ropes; little or no solid ground, so knocking pieces loose changes where you can stand. |
| Wrecking balls and heavy weights | 3 | Large hung balls or heavy blocks that swing or drop into the middle. |

Some maps combine kinds; each is counted once under its main idea.

## The maps

Numbers follow the spreadsheet order. "Nickname" is the community's proposed name, where there is one.

| # | Internal name | Nickname | Contents |
|---|---|---|---|
| 1 | Balance_M | Balance | Static. Mirrored bottle-shaped pillars and floating ledges around a central mound. |
| 2 | Bear_M | Bear | Static. Blocks, L-shaped ledges and two angled ramps over a wide floor. |
| 3 | BigJam_M | Cave | Saws. An enclosed diamond room with inner wall strips and two large saws on the centre line. |
| 4 | Bridge_M | Lion | Static. Scattered square blocks around a T-shaped centre piece. |
| 5 | Castle_M | Castle | Static. Rounded green ledges stepping up to a top block, with two diamonds. |
| 6 | City_M | City | Static. Columns of white "skyscraper" blocks of different heights. |
| 7 | Destructible_1_M | Panels | Hanging. Two rows of narrow panels hung from anchors, with no solid ground. |
| 8 | Destructible_2_M | Demolition | Wrecking balls. Two big balls hang at the top corners beside a grey framework hung from anchors, over a solid white floor. Shooting what holds a ball back swings it into the framework in the middle. |
| 9 | Destructible_3_M | Jenga | Physics stack. A tall central tower of stacked blocks, hanging T-shaped pieces either side, and planks. |
| 10 | Destructible_4_M | | Hanging. Three orange bench structures with hung pillars. |
| 11 | Destructible_5_M | | Hanging. Rows of planks and small blocks hung from anchors. |
| 12 | Destructible_6_M | | Hanging. A grid of large square blocks hung from anchors, with two planks. |
| 13 | Destructible_7_M | | Wrecking balls. Two big balls hung one above the other on the centre line, hung planks and blocks, and a small solid pillar below. |
| 14 | Destructible_8_M | Bridge | Hanging. A grey bridge hung from rows of anchors between solid blue pillars, with planks below. |
| 15 | Destructible_9_M | Saw Hill | Saws. A huge pink slope with three saws above it and loose posts and frames standing on it. |
| 16 | Destructible_10_M | | Physics stack. A tower of solid red bars with loose frame pieces between them, over a row of solid pillars. |
| 17 | Destructible_P1 | Suspension | Hanging. A huge solid triangle hangs from the top; planks hang from many anchors below it. |
| 18 | Destructible_P2 | | Hanging. Three solid diamonds and a base block on the centre line inside a dense grid of anchors. |
| 19 | Destructible_P6 | | Physics stack. An orange temple-like tower of blocks linked by short columns, with side ledges. |
| 20 | Factory1_M | Factory | Static. A large central plateau with floating ledges above. |
| 21 | Flat_M_V2 | Flat M | Static. A wide floor with a centre block and small floating wedges. |
| 22 | Grape_Ambience_M | Grapes | Physics stack. Rows of floating boat-shaped ledges, each carrying a small pile of crates. |
| 23 | Grim_Block | | Saws. A cross-shaped layout with a central saw, diamonds at the ends, and bars above and below that probably move. |
| 24 | Honed_Edge | Squares | Static. Three diamonds, each inside a broken cage of angled walls. |
| 25 | Jumbo_Jam_M | Jumpo Jam | Physics stack. Icy ground with tall loose totems standing on it and on two raised platforms. |
| 26 | MovingParts1_M | Sweep | Moving parts. Patterned blocks and walls with pieces that sweep across the map. |
| 27 | MovingParts2_M | Lifts | Moving parts. Blue ledges over a central floor, with lifts carrying fighters up and down. |
| 28 | NewMap_M | New Map | Static. Patterned arches and towers with angled ledges above. |
| 29 | Open_M | Crystals | Static. A red castle-like layout with floating crystal shards. |
| 30 | Phys_Bridge_M | Construction | Physics stack. Wooden scaffolds on background poles, standing on solid red bases. |
| 31 | Phys_Card_Castle | | Physics stack. A huge solid diamond above columns of stacked blocks, like card towers. |
| 32 | Phys_Crates_Yes | Crate Pyramid | Physics stack. A pyramid of stacked crate columns. |
| 33 | Phys_Hang_M | | Hanging. Solid blue side pillars and small ledges, with grey planks and blocks hung between them. |
| 34 | Phys_Hang2 | | Hanging. Solid white side pillars with a structure of tan blocks hung between them. |
| 35 | Phys_Hangaround | | Hanging. Only hung diamonds, squares and planks; no solid ground. |
| 36 | Phys_HangIm | | Hanging. Hung squares, planks and a diamond, with two small solid blocks at the bottom. |
| 37 | Phys_HangInThere | | Hanging. Hung diamonds, small blocks and planks only. |
| 38 | Phys_HangTown | Hang Town | Heavy weights. Heavy grey pillars hang from solid blue anchors above loose T-shaped benches, with solid cubes at the sides. |
| 39 | Phys_Ninja | Ninja | Physics stack. A loose pink scaffold of ledges on tall background poles over one long plank. |
| 40 | Phys_P1 | Ropes | Hanging. A chevron of small blocks each hung on a rope. |
| 41 | Phys_P2 | | Hanging. A chevron of small solid squares with loose squares hung beneath. |
| 42 | Phys_P3 | Crying | Hanging. Hung planks and blocks shaped like a face. |
| 43 | Phys_P4 | Pole | Physics stack. Solid white pedestals with loose blocks and a long pole balanced in the middle. |
| 44 | Phys_RoadBlock | Road Block | Physics stack. A solid blue mountain floor, heavy loose blocks on stilts, and a tall pillar hanging over the peak. |
| 45 | Phys_Saw1 | Sawmill | Saws. A central saw among loose frames on background poles. |
| 46 | Phys_Saw2 | Goblin | Saws. Two saws either side of a central block, with loose blocks and planks around. |
| 47 | Phys_Sledge_M | Sledge | Physics stack. Two heavy "sledgehammer" blocks on poles standing on solid pedestals, a loose cup on the centre ledge, and side ledges. |
| 48 | Phys_Structure_M | | Physics stack. A large grey framework of arches. |
| 49 | Phys_Temple | | Physics stack. Wooden scaffolds on background poles, with no solid ground. |
| 50 | Phys_TreeTown | Tree Town | Physics stack. Solid red blocks between heavy loose blocks stacked on background poles. |
| 51 | Physics_Canyon_M | Canyon | Physics stack. Solid red walls and ledges with columns of loose bricks between them. |
| 52 | Physics_FlyingCastle_M | | Physics stack. A floating castle built entirely of loose blocks. |
| 53 | Physics_Mix_M | | Saws. Two solid pink L-shaped towers lined with saws on their outer sides, and loose blocks above. |
| 54 | Physics_Mix2_M | Windmills | Saws. Saws on top of loose poles standing on solid orange bases, with loose crates between. |
| 55 | Physics_Mix3_M | Slums | Physics stack. Multi-storey building frames of loose beams on background poles. |
| 56 | Physics_Top_M | Top M | Physics stack. Green floor and ledges topped with loose blocks, and two tall loose poles. |
| 57 | Platform_Chaos | Platform Chaos | Physics stack. One floating row of loose plank segments with crates on and under it. |
| 58 | Polar_Serendipity_M | Polar | Physics stack. Icy T-shaped pillars, each topped with a small crate pile, over cross-shaped pillars. |
| 59 | Pyramid_M | | Static. A solid blue pyramid with floating shards around its top. |
| 60 | Rise_M | | Static. Pale blocks and L-shaped ledges over a trapezoid floor, with side walls. |
| 61 | Road_M | Dance | Static. A bowl-shaped enclosure with ledges and L shapes inside. |
| 62 | Rooms_M | | Static. A maze of white walls and ledges forming rooms. |
| 63 | Sails_M | Heart | Moving parts. Round red knobs and bars around a large circle; the community says the shapes pulse. |
| 64 | Space_M | Space | Physics stack. A gold basin filled with many small loose blocks, like a ball pit, under floating cups. |
| 65 | Staircase_M_V2 | | Static. Patterned floating blocks over two wide floors. |
| 66 | Steps_M | Pit | Moving parts, probably. A deep V-shaped pit with a dashed column of blocks on the centre line and two diamonds. |
| 67 | Temple_M_V3 | | Static. Patterned floating blocks of many sizes. |
| 68 | TreeTop_M | Church | Static. Pink crosses and pillars between tall side walls. |
| 69 | Tunnels_M | | Static. White ledges, Y-shaped pieces and a wide floor. |
| 70 | Welcome_M | | Static. Pale ledges and pillars around a central block. |

## Removed after launch

The [Vanilla Maps mod](https://thunderstore.io/c/rounds/p/Root/Vanilla_Maps/) restores six maps from the 7 April 2021 build that were later removed: Phys_Hangig, Diamond_City, Destructible_P3, Destructible_P4, Destructible_P5 and Hamburgers.
No previews or descriptions were found.

## Maps seen in detail

The two recordings in `reference/` (Adam's own matches) show some arenas in motion; `docs/fidelity/` records what was measured, including a timber collapse held by ropes, a radial-saw arena, an ice arena, a lime modular arena and a crate blast.
