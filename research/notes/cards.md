# Vanilla card catalog

## Binding target

The catalog targets public Windows build `21020021`, whose live menu identifies `v1.1.2.a75ee335a`.
It contains research metadata and implementation guidance, not copied card art, original descriptive prose, game code, or extracted game data.

## Count reconciliation

The official store promises “65+” powerups, which is only a lower bound.
The English vanilla table enumerates 67 cards.
The independently maintained Japanese index exposes 66 linked card pages and omits Quick Shot.
A June 2024 GameFAQs list and an October 2021 Korean guide both include Quick Shot, so the project catalog binds 67 stable IDs while retaining the Japanese omission as a source gap.
The current build exposes random draft choices rather than a clean-room catalog browser, so the project has not directly forced every card to appear in one runtime session.

The catalog contains 17 common, 33 uncommon, and 17 rare cards.
All 67 are recorded as available in the vanilla draft pool with medium confidence because two current indexes agree on pool membership and no current public note removes a card.

## What the machine-readable record means

`spec/cards.json` separates display modifiers, visible behavior, runtime hooks, and cross-copy stacking.
The 199 effect records therefore do not pretend that a displayed percentage proves an internal formula.
Every numeric effect cites an official note or at least two independent public references.
Every effect also records a supported target, operation, project normalization phase, cap state, numeric provenance, and separate stacking-and-cap provenance.
The three declared phases distinguish direct modifiers, retention factors, and behavior-hook registration without claiming that this project vocabulary reproduces an unobserved internal evaluation order.

Most percentage composition remains `unresolved` until controlled multi-copy observation can distinguish additive percentage points from per-copy multiplication.
The catalog still gives explicit representative rules for flat addition, multiplication, integer counts, max-wins capabilities, and per-copy hooks so later simulation work has named hypotheses to test.
Quick Reload's multiplicative interpretation, Remote's max-wins behavior, and Echo's per-copy delayed block are intentionally provisional.
Bouncy's two-bounce count per copy has the strongest public corroboration of the five representative cases.

## Current-value conflicts

Patch 1.05 is the binding source for Careful Planning's `-150%` attack speed, Poison's `+70%` damage and `-1` ammunition, Parasite's `+25%` health, Toxic Cloud's removed damage penalty, Healing Field's 1.5-second activation, and Grow's 40-metre and 160% limits.
The same patch changes Lifestealer's drain from 8 to 4 and clarifies that Leech applies life steal generally rather than defining a separate Lifestealer behavior.
The 2021 Korean guide preserves several older values and is used as history, never as the sole current-value authority.
The June 2024 GameFAQs list also preserves pre-1.05 values for Parasite health and Poison damage while omitting Poison's ammunition penalty, so those entries remain explicit historical conflicts rather than current corroboration.

Patch 1.05 also confirms that Abyssal Countdown triggers sooner and lasts longer, Cold Bullets stacks beyond two copies, and Shield Charge range no longer varies with health.
Those relative constraints are binding, while their unpublished exact timings, duplicate-copy formula, cap, and charge distance remain unresolved.

The current indexes report Spray at `+1000%` attack speed while the older guide reports `+100%`.
The current indexes report Target Bounce at one added bounce while the older guide reports two.
The current indexes report Trickster at `-20%` base damage while the older guide reports `-25%`.
The current indexes report Remote with `+0.25` seconds reload time while the older guide reports `-0.25` seconds.
Those current-index values are binding, and every disagreement remains attached to the affected card's `unknowns` list.

Patch 1.1.1 makes projectile scaling consume damage supplied by cards.
The November 2024 update fixes bullets that failed to grow under some card effects.
Those fixes bind current visual and collision behavior even if older footage exhibits the defects.

## Implementation order

1. Implement the 12 stat-only cards behind a single ordered modifier fold and use controlled duplicate-card scenarios to replace unresolved percentage formulas.
2. Implement projectile-count, ammunition, speed, bounce, and reload modifiers before trajectory-changing hooks.
3. Add projectile hooks such as explosions, drilling, homing, steering, growth, delayed detonation, and damage over time through deterministic event registration.
4. Add conditional player hooks such as Chase, Brawler, Pristine Perseverence, and Taste of Blood with explicit start and expiry ticks.
5. Add block-trigger graphs with recursion guards, beginning with one-shot effects and ending with Echo, Empower, Refresh, Shields Up, and Tactical Reload interactions.
6. Add lifecycle behavior last, with Phoenix revival included in replay state, rollback snapshots, and stable hashes.

## Deliberate unknowns

Hook radii, tick rates, hidden cooldowns, target-selection ties, recursive block ordering, mixed-card caps, and most duplicate-card formulas are not safely inferable from display text.
The catalog records those boundaries instead of turning low-confidence community claims into implementation facts.
Later controlled observations may amend an `unknown` or `provisional` field, but changing a confirmed current value requires equally strong provenance and a ticketed contract update.
