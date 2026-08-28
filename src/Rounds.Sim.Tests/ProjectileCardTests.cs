using Rounds.Sim.Cards;
using Rounds.Sim.Maps;
using Rounds.Sim.Math;
using Rounds.Sim.Physics;

namespace Rounds.Sim.Tests;

public sealed class ProjectileCardTests
{
    [Fact]
    public void ShooterProfileSuppliesTheBulletBounceBudget()
    {
        var profile = PlayerCombatProfile.Fold(new[] { "bouncy" });
        var world = CreateActiveWorld(OpenArena(), profile);

        Sim.Step(world, new[]
        {
            new PlayerInput(0, false, true, false, new Vec2(1.0, 0.0)),
            default,
        });

        var bullet = Assert.Single(world.Bullets);
        Assert.Equal(2, bullet.BouncesRemaining);
        Assert.Equal(profile.BulletDamage, bullet.Damage);
    }

    [Fact]
    public void MuzzleOverlapConsumesOnlyTheImpactBounceAndLeavesTheSurface()
    {
        var profile = PlayerCombatProfile.Fold(new[] { "mayhem" });
        var world = CreateActiveWorld(OpenArena(), profile);

        Sim.Step(world, new[]
        {
            new PlayerInput(0, false, true, false, new Vec2(0.0, -1.0)),
            default,
        });

        var bullet = Assert.Single(world.Bullets);
        Assert.Equal(4, bullet.BouncesRemaining);
        Assert.True(bullet.Velocity.Y > 0.0);
    }

    [Fact]
    public void BouncyReflectsTwiceWithExactRemainderThenDespawns()
    {
        var world = CreateActiveWorld(CorridorArena());
        world.Bullets.Add(CreateBullet(bounces: 2, velocity: new Vec2(2.0, 0.0)));

        Sim.Step(world, new PlayerInput[2]);
        var bullet = Assert.Single(world.Bullets);
        Assert.Equal(1, bullet.BouncesRemaining);
        Assert.Equal(new Vec2(-2.0, 0.0), bullet.Velocity);
        Assert.Equal(-0.360001, bullet.Position.X, 5);

        Sim.Step(world, new PlayerInput[2]);
        bullet = Assert.Single(world.Bullets);
        Assert.Equal(0, bullet.BouncesRemaining);
        Assert.Equal(new Vec2(2.0, 0.0), bullet.Velocity);
        Assert.Equal(0.72, bullet.Position.X, 5);

        Sim.Step(world, new PlayerInput[2]);
        Assert.Empty(world.Bullets);
    }

    [Fact]
    public void MayhemReflectsFiveTimesAndTheSixthGeometryContactDespawns()
    {
        var world = CreateActiveWorld(CorridorArena());
        world.Bullets.Add(CreateBullet(bounces: 5, velocity: new Vec2(2.0, 0.0)));

        var consumed = 0;
        var previous = 5;
        var guard = 0;
        while (world.Bullets.Count > 0 && guard++ < 10)
        {
            Sim.Step(world, new PlayerInput[2]);
            if (world.Bullets.Count == 0)
            {
                Assert.Equal(0, previous);
                break;
            }
            var remaining = Assert.Single(world.Bullets).BouncesRemaining;
            Assert.InRange(remaining, 0, previous - 1);
            consumed += previous - remaining;
            previous = remaining;
        }
        Assert.True(guard < 10);
        Assert.Equal(5, consumed);
        Assert.Empty(world.Bullets);
    }

    [Fact]
    public void BlockReflectionPreservesGeometryBouncesButBodyContactDespawns()
    {
        var blocked = CreateActiveWorld(OpenArena());
        blocked.Players[0].Position = new Vec2(-5.0, 0.0);
        blocked.Players[1].Position = Vec2.Zero;
        blocked.Players[1].BlockPhase = BlockPhase.Active;
        blocked.Players[1].BlockTicksRemaining = 10;
        blocked.Bullets.Add(CreateBullet(2, new Vec2(3.0, 0.0), new Vec2(-2.0, 0.0)));

        Sim.Step(blocked, new PlayerInput[2]);
        var reflected = Assert.Single(blocked.Bullets);
        Assert.Equal(1, reflected.OwnerId);
        Assert.Equal(2, reflected.BouncesRemaining);

        var body = CreateActiveWorld(OpenArena());
        body.Players[0].Position = new Vec2(-5.0, 0.0);
        body.Players[1].Position = Vec2.Zero;
        body.Bullets.Add(CreateBullet(2, new Vec2(3.0, 0.0), new Vec2(-2.0, 0.0)));
        Sim.Step(body, new PlayerInput[2]);
        Assert.Empty(body.Bullets);
        Assert.True(body.Players[1].Health < body.Players[1].CombatProfile.MaximumHealth);
    }

    [Fact]
    public void GeometryWinsAnEqualTimeBodyContactAndConsumesOneBounce()
    {
        var arena = ArenaWithBoxes(
            new[]
            {
                Obb.Create("wall", 0, Vec2.Zero, 0.2, 8.0, 0.0),
                Floor(),
            });
        var world = CreateActiveWorld(arena);
        world.Players[0].Position = new Vec2(-5.0, 0.0);
        world.Players[1].Position = new Vec2(0.4, 0.0);
        var health = world.Players[1].Health;
        world.Bullets.Add(CreateBullet(1, new Vec2(3.0, 0.0), new Vec2(-2.0, 0.0)));

        Sim.Step(world, new PlayerInput[2]);

        var bullet = Assert.Single(world.Bullets);
        Assert.Equal(0, bullet.BouncesRemaining);
        Assert.True(bullet.Velocity.X < 0.0);
        Assert.Equal(health, world.Players[1].Health);
    }

    [Fact]
    public void RoundedCornerBounceUsesTheRadialNormal()
    {
        var arena = ArenaWithBoxes(
            new[]
            {
                Obb.Create("corner", 0, new Vec2(1.0, 1.0), 0.2, 0.2, 0.0),
                Floor(),
            });
        var world = CreateActiveWorld(arena);
        world.Bullets.Add(CreateBullet(1, new Vec2(2.0, 2.0)));

        Sim.Step(world, new PlayerInput[2]);

        var bullet = Assert.Single(world.Bullets);
        Assert.Equal(0, bullet.BouncesRemaining);
        Assert.Equal(-2.0, bullet.Velocity.X, 8);
        Assert.Equal(-2.0, bullet.Velocity.Y, 8);
        Assert.Equal(-0.313139, bullet.Position.X, 5);
        Assert.Equal(-0.313139, bullet.Position.Y, 5);
    }

    [Fact]
    public void FourContactCapDeterministicallyRemovesATrappedRicochet()
    {
        var first = CreateActiveWorld(CorridorArena());
        var second = CreateActiveWorld(CorridorArena());
        first.Bullets.Add(CreateBullet(10, new Vec2(20.0, 0.0)));
        second.Bullets.Add(CreateBullet(10, new Vec2(20.0, 0.0)));

        Sim.Step(first, new PlayerInput[2]);
        Sim.Step(second, new PlayerInput[2]);

        Assert.Empty(first.Bullets);
        Assert.Empty(second.Bullets);
        Assert.Equal(Sim.Hash(first), Sim.Hash(second));
    }

    [Fact]
    public void ProfileAndRemainingBounceCountBothAffectTheHash()
    {
        var vanilla = CreateActiveWorld(OpenArena());
        var bouncy = CreateActiveWorld(OpenArena(), PlayerCombatProfile.Fold(new[] { "bouncy" }));
        Assert.NotEqual(Sim.Hash(vanilla), Sim.Hash(bouncy));

        vanilla.Bullets.Add(CreateBullet(1, Vec2.Zero));
        var changed = CreateActiveWorld(OpenArena());
        changed.Bullets.Add(CreateBullet(2, Vec2.Zero));
        Assert.NotEqual(Sim.Hash(vanilla), Sim.Hash(changed));
    }

    [Fact]
    public void IdenticalBounceScriptsHaveIdenticalPerTickHashesAndChangedCardsDivergeBeforeFire()
    {
        var bouncyProfile = PlayerCombatProfile.Fold(new[] { "bouncy" });
        var first = CreateActiveWorld(CorridorArena(), bouncyProfile);
        var second = CreateActiveWorld(CorridorArena(), bouncyProfile);
        first.Bullets.Add(CreateBullet(bouncyProfile.ProjectileBounces, new Vec2(2.0, 0.0)));
        second.Bullets.Add(CreateBullet(bouncyProfile.ProjectileBounces, new Vec2(2.0, 0.0)));
        var firstHashes = new List<ulong>();
        var secondHashes = new List<ulong>();

        for (var tick = 0; tick < 3; tick++)
        {
            Sim.Step(first, new PlayerInput[2]);
            Sim.Step(second, new PlayerInput[2]);
            firstHashes.Add(Sim.Hash(first));
            secondHashes.Add(Sim.Hash(second));
        }

        Assert.Equal(firstHashes, secondHashes);
        var bouncyBeforeFire = CreateActiveWorld(CorridorArena(), bouncyProfile);
        var mayhemBeforeFire = CreateActiveWorld(
            CorridorArena(),
            PlayerCombatProfile.Fold(new[] { "mayhem" }));
        Assert.Empty(first.Bullets);
        Assert.Empty(bouncyBeforeFire.Bullets);
        Assert.Empty(mayhemBeforeFire.Bullets);
        Assert.NotEqual(Sim.Hash(bouncyBeforeFire), Sim.Hash(mayhemBeforeFire));
    }

    private static World CreateActiveWorld(
        ArenaDefinition arena,
        PlayerCombatProfile? firstProfile = null)
    {
        var world = World.CreateMatch(
            1,
            arena,
            playerProfiles:
            [
                firstProfile ?? PlayerCombatProfile.Vanilla,
                PlayerCombatProfile.Vanilla,
            ]);
        while (world.Phase == DuelPhase.Spawning)
        {
            Sim.Step(world, new PlayerInput[2]);
        }
        return world;
    }

    private static Bullet CreateBullet(int bounces, Vec2 velocity, Vec2? position = null) => new()
    {
        Id = 100,
        OwnerId = 0,
        Position = position ?? Vec2.Zero,
        Velocity = velocity,
        Radius = CombatTuning.Vanilla.ProjectileRadius,
        Damage = 0.1,
        BouncesRemaining = bounces,
    };

    private static ArenaDefinition CorridorArena() => ArenaWithBoxes(
        new[]
        {
            Obb.Create("left", 0, new Vec2(-1.0, 0.0), 0.2, 8.0, 0.0),
            Obb.Create("right", 1, new Vec2(1.0, 0.0), 0.2, 8.0, 0.0),
            Floor(),
        });

    private static ArenaDefinition OpenArena() => ArenaWithBoxes(new[] { Floor() });

    private static Obb Floor() => Obb.Create("floor", 10, new Vec2(0.0, -5.0), 30.0, 1.0, 0.0);

    private static ArenaDefinition ArenaWithBoxes(IReadOnlyList<Obb> boxes) => new(
        "projectile-fixture",
        new ArenaBounds(-20.0, 20.0, -15.0, 15.0),
        new ArenaBounds(-20.0, 20.0, -8.0, 10.0),
        -12.0,
        boxes,
        new[]
        {
            new SpawnRegion("left-spawn", new ArenaBounds(-10.1, -9.9, -4.1, -3.9), "floor"),
            new SpawnRegion("right-spawn", new ArenaBounds(9.9, 10.1, -4.1, -3.9), "floor"),
        });
}
