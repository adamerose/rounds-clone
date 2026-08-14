using System.Text;
using Rounds.Sim.Cards;
using Rounds.Sim.Maps;
using Rounds.Sim.Math;
using Rounds.Sim.Physics;

namespace Rounds.Sim.Tests;

public sealed class StatCardTests
{
    [Fact]
    public void EmbeddedCatalogHasExactOrdinalPoolAndOriginalNeutralNames()
    {
        var catalog = StatCardCatalog.LoadEmbedded();

        Assert.Equal(
            new[]
            {
                "careful-planning", "combine", "defender", "fastball", "glass-cannon", "huge",
                "leech", "quick-reload", "quick-shot", "steady-shot", "tank", "wind-up",
            },
            catalog.Cards.Select(card => card.Id));
        Assert.Equal(
            new[]
            {
                "Deliberate", "Chamber Trade", "Guarded", "Railshot", "Overcharge", "Heavy",
                "Siphon", "Snap Load", "Hair Trigger", "Stabilizer", "Juggernaut", "Windup",
            },
            catalog.Cards.Select(card => card.DisplayName));
    }

    [Theory]
    [InlineData("careful-planning", 1.0, 3, 1.10, 45, 150, 2.4, 240, 0.0)]
    [InlineData("combine", 1.0, 1, 1.10, 18, 150, 2.4, 240, 0.0)]
    [InlineData("defender", 1.3, 3, 0.55, 18, 120, 2.4, 168, 0.0)]
    [InlineData("fastball", 1.0, 3, 0.55, 27, 135, 8.4, 240, 0.0)]
    [InlineData("glass-cannon", 0.5, 3, 1.10, 18, 135, 2.4, 240, 0.0)]
    [InlineData("huge", 1.8, 3, 0.55, 18, 120, 2.4, 240, 0.0)]
    [InlineData("leech", 1.3, 3, 0.55, 18, 120, 2.4, 240, 0.75)]
    [InlineData("quick-reload", 1.0, 3, 0.55, 18, 36, 2.4, 240, 0.0)]
    [InlineData("quick-shot", 1.0, 3, 0.55, 18, 135, 6.0, 240, 0.0)]
    [InlineData("steady-shot", 1.4, 3, 0.55, 18, 135, 4.8, 240, 0.0)]
    [InlineData("tank", 2.0, 3, 0.55, 23, 150, 2.4, 240, 0.0)]
    [InlineData("wind-up", 1.0, 3, 0.88, 36, 150, 4.8, 240, 0.0)]
    public void EverySingleCardHasExactProfile(
        string id,
        double health,
        int ammo,
        double damage,
        int fireTicks,
        int reloadTicks,
        double speed,
        int blockTicks,
        double lifesteal)
    {
        var profile = PlayerCombatProfile.Fold(new[] { id });

        Assert.Equal(health, profile.MaximumHealth, 10);
        Assert.Equal(ammo, profile.MaximumAmmunition);
        Assert.Equal(damage, profile.BulletDamage, 10);
        Assert.Equal(fireTicks, profile.FireIntervalTicks);
        Assert.Equal(reloadTicks, profile.ReloadTicks);
        Assert.Equal(speed, profile.ProjectileSpeed, 10);
        Assert.Equal(blockTicks, profile.BlockCooldownTicks);
        Assert.Equal(lifesteal, profile.Lifesteal, 10);
    }

    [Fact]
    public void DuplicateFoldIsOrderIndependentAndAppliesClampsAndOrdering()
    {
        var first = PlayerCombatProfile.Fold(
            new[] { "combine", "quick-reload", "glass-cannon", "defender", "quick-reload", "combine" });
        var second = PlayerCombatProfile.Fold(
            new[] { "quick-reload", "defender", "combine", "glass-cannon", "combine", "quick-reload" });

        Assert.Equal(first, second);
        Assert.Equal(1, first.MaximumAmmunition);
        Assert.Equal(1.3 / 2.0, first.MaximumHealth, 10);
        Assert.Equal(18, first.ReloadTicks);
        Assert.Equal(168, first.BlockCooldownTicks);

        var fiveDefenders = PlayerCombatProfile.Fold(Enumerable.Repeat("defender", 5));
        Assert.Equal(12, fiveDefenders.BlockCooldownTicks);
        var twoGlassCannons = PlayerCombatProfile.Fold(new[] { "glass-cannon", "glass-cannon" });
        Assert.Equal(1.0 / 3.0, twoGlassCannons.MaximumHealth, 10);
    }

    [Fact]
    public void CustomProfilesChangeHashWhileVanillaProfilesRemainCompatible()
    {
        var arena = CreateArena();
        var implicitVanilla = World.CreateMatch(11, arena);
        var explicitVanilla = World.CreateMatch(
            11,
            arena,
            playerProfiles: new[] { PlayerCombatProfile.Vanilla, PlayerCombatProfile.Vanilla });
        Assert.Equal(Sim.Hash(implicitVanilla), Sim.Hash(explicitVanilla));

        var heavy = PlayerCombatProfile.Fold(new[] { "huge" });
        var custom = World.CreateMatch(
            11,
            arena,
            playerProfiles: new[] { heavy, PlayerCombatProfile.Vanilla });
        Assert.NotEqual(Sim.Hash(implicitVanilla), Sim.Hash(custom));
    }

    [Fact]
    public void PlayerSpecificResetAndActualDamageLifestealUseProfiles()
    {
        var siphon = PlayerCombatProfile.Fold(new[] { "leech" });
        var chamber = PlayerCombatProfile.Fold(new[] { "combine" });
        var world = World.CreateMatch(1, CreateArena(), playerProfiles: new[] { siphon, chamber });
        while (world.Phase == DuelPhase.Spawning)
        {
            Sim.Step(world, new PlayerInput[2]);
        }
        Assert.Equal(1.3, world.Players[0].Health, 10);
        Assert.Equal(1, world.Players[1].Ammo);

        world.Players[0].Health = 0.5;
        world.Players[1].Health = 0.2;
        world.Bullets.Add(new Bullet
        {
            Id = 0,
            OwnerId = 0,
            Position = world.Players[1].Position - new Vec2(0.3, 0.0),
            Velocity = new Vec2(0.6, 0.0),
            Radius = world.Combat.ProjectileRadius,
            Damage = siphon.BulletDamage,
        });

        Sim.Step(world, new PlayerInput[2]);

        Assert.Equal(0.65, world.Players[0].Health, 10);
        Assert.Equal(0.0, world.Players[1].Health);
    }

    [Theory]
    [InlineData("\"targetBuild\": \"21020021\"", "\"targetBuild\": \"wrong\"")]
    [InlineData("\"id\": \"combine\"", "\"id\": \"careful-planning\"")]
    [InlineData("\"target\": \"weapon.damage\"", "\"target\": \"weapon.unknown\"")]
    [InlineData("\"operation\": \"add-percent\"", "\"operation\": \"mystery\"")]
    [InlineData("\"hook\": \"passive\"", "\"hook\": \"active\"")]
    [InlineData("\"implementationTier\": \"stat-only\"", "\"implementationTier\": \"conditional\"")]
    public void StreamCatalogRejectsMalformedContract(string oldText, string newText)
    {
        var json = ReadEmbeddedCatalog();
        Assert.Contains(oldText, json, StringComparison.Ordinal);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json.Replace(oldText, newText, StringComparison.Ordinal)));

        Assert.Throws<InvalidDataException>(() => StatCardCatalog.Load(stream));
    }

    [Fact]
    public void StreamCatalogRejectsNonFiniteEffectValue()
    {
        var json = ReadEmbeddedCatalog();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            json.Replace("\"value\": 100", "\"value\": 1e999", StringComparison.Ordinal)));

        Assert.Throws<InvalidDataException>(() => StatCardCatalog.Load(stream));
    }

    [Fact]
    public void CombatReadsEveryPlayerSpecificWeaponTimer()
    {
        var profile = new PlayerCombatProfile(
            MaximumHealth: 2.0,
            MaximumAmmunition: 1,
            BulletDamage: 0.25,
            FireIntervalTicks: 1,
            ReloadTicks: 2,
            ProjectileSpeed: 1.25,
            BlockCooldownTicks: 7,
            Lifesteal: 0.0);
        var world = World.CreateMatch(
            1,
            CreateArena(),
            playerProfiles: new[] { profile, PlayerCombatProfile.Vanilla });
        while (world.Phase == DuelPhase.Spawning)
        {
            Sim.Step(world, new PlayerInput[2]);
        }

        Sim.Step(world, new[]
        {
            new PlayerInput(0, false, true, true, new Vec2(1.0, 0.0)),
            default,
        });

        var bullet = Assert.Single(world.Bullets);
        Assert.Equal(0.25, bullet.Damage);
        Assert.Equal(new Vec2(1.25, 0.0), bullet.Velocity);
        Assert.Equal(1, world.Players[0].FireCooldownTicksRemaining);
        Assert.Equal(2, world.Players[0].ReloadTicksRemaining);
        for (var tick = 0; tick < world.Combat.BlockActiveTicks; tick++)
        {
            Sim.Step(world, new PlayerInput[2]);
        }
        Assert.Equal(BlockPhase.Cooldown, world.Players[0].BlockPhase);
        Assert.Equal(7, world.Players[0].BlockTicksRemaining);
        Assert.Equal(1, world.Players[0].Ammo);
    }

    private static string ReadEmbeddedCatalog()
    {
        using var stream = typeof(StatCardCatalog).Assembly
            .GetManifestResourceStream("Rounds.Sim.Data.cards.json")!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static ArenaDefinition CreateArena() => new(
        "stat-fixture",
        new ArenaBounds(-20.0, 20.0, -15.0, 15.0),
        new ArenaBounds(-20.0, 20.0, -5.0, 10.0),
        -12.0,
        new[] { Obb.Create("floor", 0, new Vec2(0.0, -1.0), 40.0, 1.0, 0.0) },
        new[]
        {
            new SpawnRegion("left", new ArenaBounds(-5.1, -4.9, -0.1, 0.1), "floor"),
            new SpawnRegion("right", new ArenaBounds(4.9, 5.1, -0.1, 0.1), "floor"),
        });
}
