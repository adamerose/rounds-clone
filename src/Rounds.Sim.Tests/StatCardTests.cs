using System.Text;
using Rounds.Sim.Cards;
using Rounds.Sim.Maps;
using Rounds.Sim.Math;
using Rounds.Sim.Physics;

namespace Rounds.Sim.Tests;

public sealed class StatCardTests
{
    [Fact]
    public void EmbeddedCatalogHasExactOrdinalPoolAndSourcedRoundsNames()
    {
        var catalog = StatCardCatalog.LoadEmbedded();

        Assert.Equal(
            new[]
            {
                "bouncy", "careful-planning", "combine", "defender", "fast-forward", "fastball",
                "glass-cannon", "huge", "leech", "mayhem", "quick-reload", "quick-shot", "spray",
                "steady-shot", "tank", "wind-up",
            },
            catalog.Cards.Select(card => card.Id));
        Assert.Equal(
            new[]
            {
                "Bouncy", "Careful Planning", "Combine", "Defender", "Fast Forward", "Fastball",
                "Glass Cannon", "Huge", "Leech", "Mayhem", "Quick Reload", "Quick Shot", "Spray",
                "Steady Shot", "Tank", "Wind Up",
            },
            catalog.Cards.Select(card => card.DisplayName));
        Assert.Equal(
            new[] { "Id", "DisplayName", "Effects" },
            typeof(StatCardDefinition).GetProperties().Select(static property => property.Name));
    }

    [Fact]
    public void PublicStreamLoaderAcceptsTheExactEmbeddedCatalog()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(ReadEmbeddedCatalog()));

        var catalog = StatCardCatalog.Load(stream);

        Assert.Equal(16, catalog.Cards.Count);
        Assert.Equal("bouncy", catalog.Cards[0].Id);
        Assert.Equal("wind-up", catalog.Cards[^1].Id);
    }

    [Theory]
    [InlineData("bouncy", 1.0, 3, 0.6875, 18, 135, 1.0, 240, 0.0, 2)]
    [InlineData("careful-planning", 1.0, 3, 1.10, 45, 150, 1.0, 240, 0.0, 0)]
    [InlineData("combine", 1.0, 1, 1.10, 18, 150, 1.0, 240, 0.0, 0)]
    [InlineData("defender", 1.3, 3, 0.55, 18, 120, 1.0, 168, 0.0, 0)]
    [InlineData("fast-forward", 1.0, 3, 0.55, 18, 92, 2.0, 240, 0.0, 0)]
    [InlineData("fastball", 1.0, 3, 0.55, 27, 135, 3.5, 240, 0.0, 0)]
    [InlineData("glass-cannon", 0.5, 3, 1.10, 18, 135, 1.0, 240, 0.0, 0)]
    [InlineData("huge", 1.8, 3, 0.55, 18, 120, 1.0, 240, 0.0, 0)]
    [InlineData("leech", 1.3, 3, 0.55, 18, 120, 1.0, 240, 0.75, 0)]
    [InlineData("mayhem", 1.0, 3, 0.4675, 18, 150, 1.0, 240, 0.0, 5)]
    [InlineData("quick-reload", 1.0, 3, 0.55, 18, 36, 1.0, 240, 0.0, 0)]
    [InlineData("quick-shot", 1.0, 3, 0.55, 18, 135, 2.5, 240, 0.0, 0)]
    [InlineData("spray", 1.0, 15, 0.1375, 2, 135, 1.0, 240, 0.0, 0)]
    [InlineData("steady-shot", 1.4, 3, 0.55, 18, 135, 2.0, 240, 0.0, 0)]
    [InlineData("tank", 2.0, 3, 0.55, 23, 150, 1.0, 240, 0.0, 0)]
    [InlineData("wind-up", 1.0, 3, 0.88, 36, 150, 2.0, 240, 0.0, 0)]
    public void EverySingleCardHasExactProfile(
        string id,
        double health,
        int ammo,
        double damage,
        int fireTicks,
        int reloadTicks,
        double projectileSpeedMultiplier,
        int blockTicks,
        double lifesteal,
        int bounces)
    {
        var profile = PlayerCombatProfile.Fold(new[] { id });

        Assert.Equal(health, profile.MaximumHealth, 10);
        Assert.Equal(ammo, profile.MaximumAmmunition);
        Assert.Equal(damage, profile.BulletDamage, 10);
        Assert.Equal(fireTicks, profile.FireIntervalTicks);
        Assert.Equal(reloadTicks, profile.ReloadTicks);
        Assert.Equal(
            CombatTuning.Vanilla.ProjectileSpeed * projectileSpeedMultiplier,
            profile.ProjectileSpeed,
            10);
        Assert.Equal(blockTicks, profile.BlockCooldownTicks);
        Assert.Equal(lifesteal, profile.Lifesteal, 10);
        Assert.Equal(bounces, profile.ProjectileBounces);
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
    public void ProjectileDuplicatesComposeDamageSpeedReloadAndBouncesExactly()
    {
        for (var copies = 2; copies <= 5; copies++)
        {
            var spray = PlayerCombatProfile.Fold(Enumerable.Repeat("spray", copies));
            Assert.Equal(0.55 * System.Math.Pow(0.25, copies), spray.BulletDamage, 12);
            Assert.Equal(3 + (12 * copies), spray.MaximumAmmunition);
            Assert.True(spray.FireIntervalTicks >= 1);
        }

        var first = PlayerCombatProfile.Fold(new[] { "bouncy", "mayhem", "fast-forward", "quick-reload", "spray" });
        var second = PlayerCombatProfile.Fold(new[] { "spray", "quick-reload", "fast-forward", "mayhem", "bouncy" });
        Assert.Equal(first, second);
        Assert.Equal(7, first.ProjectileBounces);
        Assert.Equal(0.55 * 1.25 * 0.85 * 0.25, first.BulletDamage, 12);
        Assert.Equal(42, first.ReloadTicks);
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
    public void StreamCatalogRejectsAnIdDuplicatedAcrossImplementationTiers()
    {
        var json = ReadEmbeddedCatalog();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            json.Replace("\"id\": \"abyssal-countdown\"", "\"id\": \"careful-planning\"", StringComparison.Ordinal)));

        Assert.Throws<InvalidDataException>(() => StatCardCatalog.Load(stream));
    }

    [Theory]
    [InlineData("bouncy", "\"value\": 2", "\"value\": 3")]
    [InlineData("bouncy", "\"value\": 2", "\"value\": 2.5")]
    [InlineData("spray", "\"value\": -75", "\"value\": -100")]
    [InlineData("quick-reload", "\"value\": 0.3", "\"value\": 0")]
    public void StreamCatalogRejectsUnsupportedSelectedEffectValues(
        string cardId,
        string oldText,
        string newText)
    {
        var json = MutateCard(ReadEmbeddedCatalog(), cardId, oldText, newText);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        Assert.Throws<InvalidDataException>(() => StatCardCatalog.Load(stream));
    }

    [Fact]
    public void StreamCatalogRejectsAMissingSupportedCard()
    {
        var json = MutateCard(ReadEmbeddedCatalog(), "bouncy", "\"id\": \"bouncy\"", "\"id\": \"unsupported-bouncy\"");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        Assert.Throws<InvalidDataException>(() => StatCardCatalog.Load(stream));
    }

    [Fact]
    public void StreamCatalogRejectsRenamedSupportedCard()
    {
        var json = MutateCard(
            ReadEmbeddedCatalog(),
            "bouncy",
            "\"originalName\": \"Bouncy\"",
            "\"originalName\": \"Rebound\"");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        Assert.Throws<InvalidDataException>(() => StatCardCatalog.Load(stream));
    }

    [Fact]
    public void FoldRoundsMidpointsAwayFromZeroAndClampsFireAndReloadToOneTick()
    {
        var midpointFire = PlayerCombatProfile.Fold(
            new[] { "careful-planning" },
            CatalogWithEffect("careful-planning", "attack-speed", -25.0));
        var midpointReload = PlayerCombatProfile.Fold(
            new[] { "careful-planning" },
            CatalogWithEffect("careful-planning", "reload-time", 1.0 / 120.0));
        var clampedFire = PlayerCombatProfile.Fold(
            new[] { "careful-planning" },
            CatalogWithEffect("careful-planning", "attack-speed", 10_000.0));
        var clampedReload = PlayerCombatProfile.Fold(
            new[] { "careful-planning" },
            CatalogWithEffect("careful-planning", "reload-time", -10.0));

        Assert.Equal(23, midpointFire.FireIntervalTicks);
        Assert.Equal(121, midpointReload.ReloadTicks);
        Assert.Equal(1, clampedFire.FireIntervalTicks);
        Assert.Equal(1, clampedReload.ReloadTicks);
    }

    [Fact]
    public void DerivedProfilesRejectEveryNonFiniteOrNonPositiveBoundary()
    {
        var vanilla = PlayerCombatProfile.Vanilla;
        var invalid = new[]
        {
            vanilla with { MaximumHealth = double.NaN },
            vanilla with { MaximumAmmunition = 0 },
            vanilla with { BulletDamage = double.PositiveInfinity },
            vanilla with { FireIntervalTicks = 0 },
            vanilla with { ReloadTicks = 0 },
            vanilla with { ProjectileSpeed = 0.0 },
            vanilla with { BlockCooldownTicks = 0 },
            vanilla with { Lifesteal = double.NaN },
            vanilla with { Lifesteal = -0.01 },
            vanilla with { ProjectileBounces = -1 },
        };

        foreach (var profile in invalid)
        {
            Assert.Throws<ArgumentOutOfRangeException>(profile.Validate);
        }

        var overflowing = CatalogWithEffect("glass-cannon", "damage", double.MaxValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerCombatProfile.Fold(
            Enumerable.Repeat("glass-cannon", 200),
            overflowing));
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
            Lifesteal: 0.0,
            ProjectileBounces: 0);
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

    private static string MutateCard(string json, string cardId, string oldText, string newText)
    {
        var marker = $"\"id\": \"{cardId}\"";
        var start = json.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing card `{cardId}`.");
        var next = json.IndexOf("\n    {\n      \"id\": ", start + marker.Length, StringComparison.Ordinal);
        var end = next < 0 ? json.Length : next;
        var card = json[start..end];
        Assert.Contains(oldText, card, StringComparison.Ordinal);
        card = card.Replace(oldText, newText, StringComparison.Ordinal);
        return string.Concat(json.AsSpan(0, start), card.AsSpan(), json.AsSpan(end));
    }

    private static StatCardCatalog CatalogWithEffect(string cardId, string effectId, double value)
    {
        var card = StatCardCatalog.LoadEmbedded().GetRequired(cardId);
        var effects = card.Effects
            .Select(effect => effect.Id == effectId ? effect with { Value = value } : effect)
            .ToArray();
        Assert.Contains(effects, effect => effect.Id == effectId && effect.Value == value);
        return StatCardCatalog.CreateForTesting(new StatCardDefinition(
            card.Id,
            card.DisplayName,
            effects));
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
