# Legacy prototype lookup

The annotated tag `archive/godot-csharp-prototype-2026-09-03` resolves to legacy commit `382ae14788646c199b42243652d5c0294c6994f4`.
Git history is the archive; there is no copied `archive/` source tree.

Use read-only Git object lookup to inspect the retired implementation without restoring it into the active checkout:

```text
git rev-parse archive/godot-csharp-prototype-2026-09-03^{}
git show archive/godot-csharp-prototype-2026-09-03:game/project.godot
git show archive/godot-csharp-prototype-2026-09-03:src/Rounds.Sim/Sim.cs
git show archive/godot-csharp-prototype-2026-09-03:src/Rounds.Sim.Tests/CombatTests.cs
git show archive/godot-csharp-prototype-2026-09-03:docs/tickets/closed/006-implement-base-combat-duel.md
git show archive/godot-csharp-prototype-2026-09-03:spec/combat.json
git log archive/godot-csharp-prototype-2026-09-03 -- src/Rounds.Sim
```

These lookups preserve the old source, tests, ticket record, specification, and their history while the active build remains Rust-only.
