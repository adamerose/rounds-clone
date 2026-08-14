using Rounds.Sim.Maps;
using Rounds.Sim.Math;
using Rounds.Sim.Physics;

namespace Rounds.Sim.Tests;

public sealed class CombatTests
{
    [Fact]
    public void VanillaCombatTuningMatchesBindingAndNamedProvisionalValues()
    {
        var tuning = CombatTuning.Vanilla;

        Assert.Equal(1.0, tuning.BaseHealth);
        Assert.Equal(3, tuning.BaseAmmo);
        Assert.Equal(0.55, tuning.BaseDamage);
        Assert.Equal(18, tuning.FireIntervalTicks);
        Assert.Equal(120, tuning.ReloadTicks);
        Assert.Equal(2.4, tuning.ProjectileSpeed);
        Assert.Equal(0.08, tuning.ProjectileRadius);
        Assert.Equal(0, tuning.BaseBounces);
        Assert.Equal(0.10, tuning.RecoilSpeed);
        Assert.Equal(12, tuning.BlockActiveTicks);
        Assert.Equal(240, tuning.BlockCooldownTicks);
        Assert.Equal(240, tuning.BulletLifetimeSweeps);
        Assert.Equal(4, tuning.BulletContactIterations);
        Assert.Equal(60, tuning.SpawnLockTicks);
        Assert.Equal(6, tuning.ResolveTicks);
        Assert.Equal(90, tuning.ResultTicks);
        Assert.Equal(2048, tuning.LiveBulletCap);
    }

    [Fact]
    public void InvalidCombatTuningIsRejectedBeforeWorldMutation()
    {
        var invalid = CombatTuning.Vanilla with { ProjectileSpeed = double.NaN };

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            World.CreateMatch(1, CreateArena(), combatTuning: invalid));
    }

    [Fact]
    public void AimIsNormalizedZeroRetainsAndNonFiniteRejectsWholeTick()
    {
        var world = CreateActiveWorld();
        Step(world, new PlayerInput(0, false, false, false, new Vec2(3.0, 4.0)));
        Assert.Equal(0.6, world.Players[0].AimDirection.X, precision: 12);
        Assert.Equal(0.8, world.Players[0].AimDirection.Y, precision: 12);

        Step(world, default);
        Assert.Equal(new Vec2(0.6, 0.8), world.Players[0].AimDirection);

        var before = Sim.Hash(world);
        Assert.Throws<ArgumentException>(() => Step(
            world,
            default,
            new PlayerInput(0, false, false, false, new Vec2(double.NaN, 0.0))));
        Assert.Equal(before, Sim.Hash(world));
    }

    [Fact]
    public void HeldFireUsesExactCadenceAmmoAndFullMagazineReload()
    {
        var world = CreateActiveWorld();
        var fire = new PlayerInput(0, false, true, false, new Vec2(0.0, 1.0));

        Step(world, fire);
        Assert.Equal(2, world.Players[0].Ammo);
        Assert.Equal(18, world.Players[0].FireCooldownTicksRemaining);
        Assert.Single(world.Bullets);
        Assert.Equal(2.4, world.Bullets[0].Velocity.Y, precision: 12);
        Assert.Equal(0.08, world.Bullets[0].Radius);
        Assert.True(double.IsFinite(world.Bullets[0].Position.Y));

        for (var tick = 0; tick < 17; tick++)
        {
            Step(world, fire);
        }
        Assert.Equal(2, world.Players[0].Ammo);
        Step(world, fire);
        Assert.Equal(1, world.Players[0].Ammo);

        for (var tick = 0; tick < 18; tick++)
        {
            Step(world, fire);
        }
        Assert.Equal(0, world.Players[0].Ammo);
        Assert.Equal(120, world.Players[0].ReloadTicksRemaining);

        for (var tick = 0; tick < 119; tick++)
        {
            Step(world);
        }
        Assert.Equal(0, world.Players[0].Ammo);
        Step(world);
        Assert.Equal(3, world.Players[0].Ammo);
        Assert.Equal(0, world.Players[0].ReloadTicksRemaining);
    }

    [Fact]
    public void ShotRecoilAddsOppositeVelocityWithoutErasingMomentum()
    {
        var world = CreateActiveWorld();
        world.Players[0].Velocity = new Vec2(0.4, 0.2);

        Step(world, new PlayerInput(0, false, true, false, new Vec2(1.0, 0.0)));

        Assert.InRange(world.Players[0].Velocity.X, 0.299999, 0.300001);
        Assert.True(world.Players[0].Velocity.Y > 0.0);
    }

    [Fact]
    public void BaseHitDamagesKnocksBackAndTwoHitsKill()
    {
        var world = CreateActiveWorld();
        world.Players[0].Position = new Vec2(-3.0, 0.0);
        world.Players[1].Position = new Vec2(3.0, 0.0);

        FireRightAndRun(world);
        Assert.Equal(0.45, world.Players[1].Health, precision: 12);
        Assert.True(world.Players[1].IsAlive);
        Assert.True(world.Players[1].Velocity.X > 0.0);

        world.Players[0].FireCooldownTicksRemaining = 0;
        world.Players[0].Position = new Vec2(-3.0, 0.0);
        world.Players[1].Position = new Vec2(3.0, 0.0);
        FireRightAndRun(world);

        Assert.False(world.Players[1].IsAlive);
        Assert.Equal(DuelPhase.Resolving, world.Phase);
        Assert.Null(world.WinnerId);
    }

    [Fact]
    public void BulletIgnoresCurrentOwnerAndDiesOnFirstGeometryHit()
    {
        var world = CreateActiveWorld();
        var owner = world.Players[0];
        owner.Position = Vec2.Zero;
        world.Bullets.Add(NewBullet(10, owner.Id, new Vec2(-1.0, 0.0), new Vec2(2.0, 0.0)));

        Step(world);
        Assert.Equal(1.0, owner.Health);
        Assert.Single(world.Bullets);

        world.Bullets.Clear();
        world.Bullets.Add(NewBullet(11, owner.Id, new Vec2(0.0, 2.0), new Vec2(0.0, -5.0)));
        Step(world);
        Assert.Empty(world.Bullets);
    }

    [Fact]
    public void BlockHasExactWindowCooldownAndNeedsReleaseToRetrigger()
    {
        var world = CreateActiveWorld(CombatTuning.Vanilla with { BlockPushSpeed = 0.0 });
        var held = new PlayerInput(0, false, false, true);

        Step(world, held);
        Assert.Equal(BlockPhase.Active, world.Players[0].BlockPhase);
        Assert.Equal(12, world.Players[0].BlockTicksRemaining);
        for (var tick = 0; tick < 11; tick++)
        {
            Step(world, held);
        }
        Assert.Equal(BlockPhase.Active, world.Players[0].BlockPhase);
        Step(world, held);
        Assert.Equal(BlockPhase.Cooldown, world.Players[0].BlockPhase);
        Assert.Equal(240, world.Players[0].BlockTicksRemaining);

        for (var tick = 0; tick < 240; tick++)
        {
            Step(world, held);
        }
        Assert.Equal(BlockPhase.Ready, world.Players[0].BlockPhase);
        Step(world, held);
        Assert.Equal(BlockPhase.Ready, world.Players[0].BlockPhase);
        Step(world);
        Step(world, held);
        Assert.Equal(BlockPhase.Active, world.Players[0].BlockPhase);
    }

    [Fact]
    public void BlockPushSeparatesPlayersAndLaunchesFromFloor()
    {
        var world = CreateActiveWorld();
        world.Players[0].Position = Vec2.Zero;
        world.Players[1].Position = new Vec2(1.0, 0.0);

        Step(world, new PlayerInput(0, false, false, true));

        Assert.True(world.Players[0].Velocity.X < 0.0);
        Assert.True(world.Players[1].Velocity.X > 0.0);
        Assert.True(world.Players[0].Velocity.Y > 0.0);
    }

    [Fact]
    public void BlockReflectsTransfersOwnershipAndNegatesDirectDamage()
    {
        var tuning = CombatTuning.Vanilla with { BlockPushSpeed = 0.0 };
        var world = CreateActiveWorld(tuning);
        world.Players[0].Position = new Vec2(-10.0, 0.0);
        world.Players[1].Position = Vec2.Zero;
        world.Bullets.Add(NewBullet(1, 0, new Vec2(-1.2, 0.0), new Vec2(2.4, 0.0)));

        Step(world, default, new PlayerInput(0, false, false, true));

        Assert.Equal(1.0, world.Players[1].Health);
        Assert.Single(world.Bullets);
        Assert.Equal(1, world.Bullets[0].OwnerId);
        Assert.True(world.Bullets[0].Velocity.X < 0.0);
    }

    [Fact]
    public void ReflectedRemainderCanDamageFormerOwnerSameTick()
    {
        var tuning = CombatTuning.Vanilla with { BlockPushSpeed = 0.0 };
        var world = CreateActiveWorld(tuning);
        world.Players[0].Position = new Vec2(-1.5, 0.0);
        world.Players[1].Position = Vec2.Zero;
        world.Bullets.Add(NewBullet(1, 0, new Vec2(-1.2, 0.0), new Vec2(2.4, 0.0)));

        Step(world, default, new PlayerInput(0, false, false, true));

        Assert.Equal(0.45, world.Players[0].Health, precision: 12);
        Assert.Equal(1.0, world.Players[1].Health);
        Assert.Empty(world.Bullets);
    }

    [Fact]
    public void ExactTimeGeometryContactWinsOverBodyAndBlock()
    {
        var tuning = CombatTuning.Vanilla with { BlockPushSpeed = 0.0 };
        var world = CreateActiveWorld(tuning, includeWall: true);
        world.Players[1].Position = new Vec2(0.8, 2.0);
        world.Players[1].BlockPhase = BlockPhase.Active;
        world.Players[1].BlockTicksRemaining = 12;
        world.Bullets.Add(NewBullet(1, 0, new Vec2(-2.0, 2.0), new Vec2(4.0, 0.0)));

        Step(world);

        Assert.Empty(world.Bullets);
        Assert.Equal(1.0, world.Players[1].Health);
    }

    [Fact]
    public void ExactTimeBlockContactWinsOverBody()
    {
        var tuning = CombatTuning.Vanilla with
        {
            BlockPushSpeed = 0.0,
            BulletContactIterations = 1,
        };
        var world = CreateActiveWorld(tuning);
        world.Players[0].Position = new Vec2(-0.08, 2.0);
        world.Players[1].Position = new Vec2(0.27, 2.0);
        world.Players[1].BlockPhase = BlockPhase.Active;
        world.Players[1].BlockTicksRemaining = 12;
        world.Bullets.Add(NewBullet(1, 99, new Vec2(-2.0, 2.0), new Vec2(4.0, 0.0)));

        Step(world);

        Assert.Equal(1.0, world.Players[0].Health);
        Assert.Empty(world.Bullets);
    }

    [Fact]
    public void ShieldPingPongExpiresAtFixedContactLimit()
    {
        var tuning = CombatTuning.Vanilla with { BlockPushSpeed = 0.0 };
        var world = CreateActiveWorld(tuning);
        world.Players[0].Position = new Vec2(-1.0, 2.0);
        world.Players[1].Position = new Vec2(1.0, 2.0);
        foreach (var player in world.Players)
        {
            player.BlockPhase = BlockPhase.Active;
            player.BlockTicksRemaining = 12;
        }
        world.Bullets.Add(NewBullet(1, 99, Vec2.Zero with { Y = 2.0 }, new Vec2(20.0, 0.0)));

        Step(world);

        Assert.Empty(world.Bullets);
        Assert.All(world.Players, player => Assert.Equal(1.0, player.Health));
    }

    [Fact]
    public void CapacityDropsOldestAndHashesOverflow()
    {
        var tuning = CombatTuning.Vanilla with
        {
            BaseAmmo = 10,
            FireIntervalTicks = 1,
            LiveBulletCap = 3,
        };
        var world = CreateActiveWorld(tuning);
        var fire = new PlayerInput(0, false, true, false, new Vec2(0.0, 1.0));
        for (var shot = 0; shot < 4; shot++)
        {
            Step(world, fire);
        }

        Assert.Equal(3, world.Bullets.Count);
        Assert.Equal(new long[] { 1, 2, 3 }, world.Bullets.Select(bullet => bullet.Id));
        Assert.Equal(1, world.DroppedBulletCount);
        Assert.Equal(4, world.NextBulletId);
        var hashWithOverflow = Sim.Hash(world);
        world.Bullets.RemoveAt(0);
        Assert.NotEqual(hashWithOverflow, Sim.Hash(world));
    }

    [Fact]
    public void BulletExpiresImmediatelyAfterConfiguredMovementSweep()
    {
        var tuning = CombatTuning.Vanilla with { BulletLifetimeSweeps = 3 };
        var world = CreateActiveWorld(tuning);
        world.Bullets.Add(NewBullet(1, 0, new Vec2(0.0, 2.0), new Vec2(0.0, 1.0)));

        Step(world);
        Step(world);
        Assert.Single(world.Bullets);
        Assert.Equal(2, world.Bullets[0].SweepsCompleted);
        Step(world);
        Assert.Empty(world.Bullets);
    }

    [Fact]
    public void HealthDeathPublishesOnceAfterSixTicksThenResetsThroughSpawnLock()
    {
        var world = CreateActiveWorld();
        Step(world, new PlayerInput(0, false, true, false, new Vec2(0.0, 1.0)));
        var preservedNextBulletId = world.NextBulletId;
        world.Players[0].Health = 0.0;

        Step(world);
        Assert.Equal(DuelPhase.Resolving, world.Phase);
        Assert.Equal(6, world.PhaseTicksRemaining);
        Assert.Equal(0, world.DuelResultCount);
        var frozenPosition = world.Players[1].Position;
        for (var tick = 0; tick < 5; tick++)
        {
            Step(world, default, new PlayerInput(1, true, true, true, new Vec2(0.0, 1.0)));
        }
        Assert.Equal(DuelPhase.Resolving, world.Phase);
        Assert.Equal(frozenPosition, world.Players[1].Position);
        Step(world);
        Assert.Equal(DuelPhase.Result, world.Phase);
        Assert.Equal(1, world.WinnerId);
        Assert.Equal(1, world.DuelResultCount);

        for (var tick = 0; tick < 90; tick++)
        {
            Step(world);
        }
        Assert.Equal(DuelPhase.Spawning, world.Phase);
        Assert.Equal(60, world.PhaseTicksRemaining);
        Assert.Equal(1, world.DuelNumber);
        Assert.Equal(new Vec2(1.0, 0.0), world.Players[0].AimDirection);
        Assert.Equal(new Vec2(-1.0, 0.0), world.Players[1].AimDirection);
        Assert.Equal(1.0, world.Players[0].Health);
        Assert.Equal(3, world.Players[0].Ammo);
        Assert.Empty(world.Bullets);
        Assert.Equal(preservedNextBulletId, world.NextBulletId);
    }

    [Fact]
    public void RingOutAndSimultaneousDeathUseBoundOutcomeRules()
    {
        var ringOut = CreateActiveWorld();
        ringOut.Players[0].Position = ringOut.Players[0].Position with
        {
            Y = ringOut.Arena.KillBoundaryY - 0.1,
        };
        Step(ringOut);
        Assert.Equal(DuelPhase.Resolving, ringOut.Phase);
        for (var tick = 0; tick < 6; tick++)
        {
            Step(ringOut);
        }
        Assert.Equal(1, ringOut.WinnerId);

        var draw = CreateActiveWorld();
        draw.Players[0].Health = 0.0;
        draw.Players[1].Health = 0.0;
        Step(draw);
        for (var tick = 0; tick < 6; tick++)
        {
            Step(draw);
        }
        Assert.True(draw.IsDraw);
        Assert.Null(draw.WinnerId);
    }

    [Fact]
    public void SideAndTopCoordinatesDoNotInferDeath()
    {
        var world = CreateActiveWorld();
        world.Players[0].Position = new Vec2(1000.0, 1000.0);

        Step(world);

        Assert.True(world.Players[0].IsAlive);
        Assert.Equal(DuelPhase.Active, world.Phase);
    }

    [Fact]
    public void SpawnLockIgnoresBothPlayersAndUnlocksTogether()
    {
        var world = World.CreateMatch(1, CreateArena());
        var initial = world.Players.Select(player => player.Position).ToArray();
        var input = new PlayerInput(1, true, true, true, new Vec2(0.0, 1.0));
        for (var tick = 0; tick < 59; tick++)
        {
            Step(world, input, input);
        }
        Assert.Equal(initial[0], world.Players[0].Position);
        Assert.Equal(initial[1], world.Players[1].Position);
        Assert.Empty(world.Bullets);
        Step(world, input, input);
        Assert.Equal(DuelPhase.Active, world.Phase);
        Assert.Equal(initial[0], world.Players[0].Position);
        Assert.Equal(initial[1], world.Players[1].Position);
        Step(world, input, input);
        Assert.NotEqual(initial[0], world.Players[0].Position);
        Assert.NotEqual(initial[1], world.Players[1].Position);
    }

    [Fact]
    public void CombatStreamIncludingResetIsDeterministic()
    {
        var first = RunResetStream();
        var second = RunResetStream();

        Assert.Equal(first, second);
    }

    private static void FireRightAndRun(World world)
    {
        Step(world, new PlayerInput(0, false, true, false, new Vec2(1.0, 0.0)));
        for (var tick = 0; tick < 10 && world.Bullets.Count > 0; tick++)
        {
            Step(world);
        }
    }

    private static Bullet NewBullet(long id, int owner, Vec2 position, Vec2 velocity) => new()
    {
        Id = id,
        OwnerId = owner,
        Position = position,
        Velocity = velocity,
        Radius = CombatTuning.Vanilla.ProjectileRadius,
        Damage = CombatTuning.Vanilla.BaseDamage,
    };

    private static ulong RunResetStream()
    {
        var world = CreateActiveWorld();
        world.Players[1].Health = 0.0;
        for (var tick = 0; tick < 240; tick++)
        {
            var first = new PlayerInput(
                (sbyte)(tick % 3 - 1),
                tick % 29 == 0,
                tick % 17 < 2,
                tick % 101 == 0,
                new Vec2(1.0, tick % 2));
            var second = new PlayerInput(
                (sbyte)(1 - tick % 3),
                tick % 31 == 0,
                tick % 19 < 2,
                tick % 103 == 0,
                new Vec2(-1.0, tick % 2));
            Step(world, first, second);
        }
        Assert.True(world.DuelNumber >= 1);
        return Sim.Hash(world);
    }

    private static World CreateActiveWorld(CombatTuning? tuning = null, bool includeWall = false)
    {
        var world = World.CreateMatch(1, CreateArena(includeWall), combatTuning: tuning);
        while (world.Phase == DuelPhase.Spawning)
        {
            Step(world);
        }
        return world;
    }

    private static ArenaDefinition CreateArena(bool includeWall = false)
    {
        var boxes = new List<Obb>
        {
            Obb.Create("floor", 0, new Vec2(0.0, -1.0), 40.0, 1.0, 0.0),
        };
        if (includeWall)
        {
            boxes.Add(Obb.Create("wall", 1, new Vec2(0.0, 2.0), 0.1, 4.0, 0.0));
        }

        return new ArenaDefinition(
            "combat-fixture",
            new ArenaBounds(-20.0, 20.0, -15.0, 15.0),
            new ArenaBounds(-20.0, 20.0, -5.0, 10.0),
            -12.0,
            boxes,
            new[]
            {
                new SpawnRegion("left", new ArenaBounds(-5.1, -4.9, -0.1, 0.1), "floor"),
                new SpawnRegion("right", new ArenaBounds(4.9, 5.1, -0.1, 0.1), "floor"),
            });
    }

    private static void Step(World world, PlayerInput first = default, PlayerInput second = default) =>
        Sim.Step(world, new[] { first, second });
}
