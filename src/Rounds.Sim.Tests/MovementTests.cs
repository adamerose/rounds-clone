using Rounds.Sim.Maps;
using Rounds.Sim.Math;
using Rounds.Sim.Physics;

namespace Rounds.Sim.Tests;

public sealed class MovementTests
{
    [Fact]
    public void VanillaTuningComesFromBindingFacts()
    {
        var tuning = PlayerTuning.Vanilla;

        Assert.Equal(0.5, tuning.Radius);
        Assert.Equal(0.10, tuning.RunSpeed);
        Assert.Equal(0.014, tuning.GroundAcceleration);
        Assert.Equal(0.8, tuning.AirControlRatio);
        Assert.Equal(0.007, tuning.Gravity);
        Assert.Equal(0.25, tuning.JumpSpeed);
        Assert.Equal(1, tuning.JumpCapacity);
        Assert.Equal(0.72, tuning.GroundVelocityRetention);
        Assert.Equal(4, tuning.JumpBufferTicks);
    }

    [Fact]
    public void GroundAccelerationReachesSustainedSpeed()
    {
        var world = CreateFlatWorld();
        Settle(world);

        for (var tick = 0; tick < 12; tick++)
        {
            Step(world, new PlayerInput(1, false, false, false));
        }

        Assert.Equal(PlayerTuning.Vanilla.RunSpeed, world.Players[0].Velocity.X, precision: 12);
        Assert.True(world.Players[0].IsGrounded);
    }

    [Fact]
    public void FullJumpMatchesMeasuredApexBand()
    {
        var world = CreateFlatWorld();
        Settle(world);
        var startY = world.Players[0].Position.Y;
        var maximumY = startY;
        var apexTick = 0;

        for (var tick = 1; tick <= 100; tick++)
        {
            Step(world, new PlayerInput(0, true, false, false));
            if (world.Players[0].Position.Y > maximumY)
            {
                maximumY = world.Players[0].Position.Y;
                apexTick = tick;
            }
        }

        Assert.InRange(maximumY - startY, 3.5, 5.5);
        Assert.InRange(apexTick, 28, 44);
    }

    [Fact]
    public void ReleasingJumpShortensTheArc()
    {
        var held = JumpApex(releaseAfterFirstTick: false);
        var released = JumpApex(releaseAfterFirstTick: true);

        Assert.True(released < held - 1.0, $"Expected released apex {released} to be materially below held apex {held}.");
    }

    [Fact]
    public void WalkingOffPreservesStoredJumpUntilUsed()
    {
        var world = CreateNarrowWorld();
        Settle(world);
        for (var tick = 0; tick < 80; tick++)
        {
            Step(world, new PlayerInput(1, false, false, false));
        }

        Assert.False(world.Players[0].IsGrounded);
        Assert.Equal(1, world.Players[0].JumpsRemaining);

        Step(world, new PlayerInput(0, true, false, false));

        Assert.Equal(0, world.Players[0].JumpsRemaining);
        Assert.True(world.Players[0].Velocity.Y > 0.0);
    }

    [Fact]
    public void GroundJumpCannotGainAnotherFromRecentContact()
    {
        var world = CreateFlatWorld();
        Settle(world);

        Step(world, new PlayerInput(0, true, false, false));
        for (var tick = 0; tick < 5; tick++)
        {
            Step(world, default);
        }
        Step(world, new PlayerInput(0, true, false, false));

        Assert.False(world.Players[0].IsGrounded);
        Assert.Equal(0, world.Players[0].JumpsRemaining);
        Assert.True(world.Players[0].Velocity.Y < PlayerTuning.Vanilla.JumpSpeed);
    }

    [Fact]
    public void BufferedJumpFiresOnlyAfterLandingRefillsStore()
    {
        var world = CreateFlatWorld();
        var player = world.Players[0];
        player.Position = new Vec2(0.0, 0.2);
        player.Velocity = new Vec2(0.0, -0.05);
        player.JumpsRemaining = 0;

        Step(world, new PlayerInput(0, true, false, false));
        Assert.Equal(0, player.JumpsRemaining);
        Assert.True(player.Velocity.Y < 0.0);

        for (var tick = 0; tick < 3 && player.Velocity.Y <= 0.0; tick++)
        {
            Step(world, default);
        }

        Assert.False(player.IsGrounded);
        Assert.Equal(0, player.JumpsRemaining);
        Assert.True(player.Velocity.Y > 0.0);
    }

    [Fact]
    public void LandingRefillsStoredJump()
    {
        var world = CreateFlatWorld();
        Settle(world);
        Step(world, new PlayerInput(0, true, false, false));
        Step(world, default);

        for (var tick = 0; tick < 120 && !world.Players[0].IsGrounded; tick++)
        {
            Step(world, default);
        }

        Assert.True(world.Players[0].IsGrounded);
        Assert.Equal(1, world.Players[0].JumpsRemaining);
    }

    [Fact]
    public void DiagonalMotionSlidesAlongWall()
    {
        var arena = CreateArena(
            Obb.Create("wall", 0, new Vec2(0.0, 1.0), 1.0, 6.0, 0.0),
            Obb.Create("floor", 1, new Vec2(0.0, -1.0), 20.0, 1.0, 0.0));
        var world = World.CreateMatch(1, arena);
        var player = world.Players[0];
        player.Position = new Vec2(-2.0, 1.0);
        player.Velocity = new Vec2(3.0, 1.0);

        Step(world, default);

        Assert.InRange(player.Position.X, -1.00001, -0.99999);
        Assert.True(player.Position.Y > 1.5);
        Assert.Equal(0.0, player.Velocity.X, precision: 10);
        Assert.True(player.Velocity.Y > 0.0);
    }

    [Fact]
    public void ArenaIdentityParticipatesInStateHash()
    {
        var first = World.CreateMatch(1, CreateFlatArena("first"));
        var second = World.CreateMatch(1, CreateFlatArena("second"));

        Assert.NotEqual(Sim.Hash(first), Sim.Hash(second));
    }

    private static double JumpApex(bool releaseAfterFirstTick)
    {
        var world = CreateFlatWorld();
        Settle(world);
        var startY = world.Players[0].Position.Y;
        var maximumY = startY;
        for (var tick = 0; tick < 100; tick++)
        {
            var held = !releaseAfterFirstTick || tick == 0;
            Step(world, new PlayerInput(0, held, false, false));
            maximumY = System.Math.Max(maximumY, world.Players[0].Position.Y);
        }
        return maximumY - startY;
    }

    private static World CreateFlatWorld() => World.CreateMatch(1, CreateFlatArena("flat"));

    private static ArenaDefinition CreateFlatArena(string id) => CreateArena(
        id,
        Obb.Create("floor", 0, new Vec2(0.0, -1.0), 40.0, 1.0, 0.0));

    private static World CreateNarrowWorld() => World.CreateMatch(1, CreateArena(
        Obb.Create("floor", 0, new Vec2(0.0, -1.0), 2.0, 1.0, 0.0)));

    private static ArenaDefinition CreateArena(params Obb[] boxes) => CreateArena("fixture", boxes);

    private static ArenaDefinition CreateArena(string id, params Obb[] boxes) => new(
        id,
        new ArenaBounds(-20.0, 20.0, -12.0, 12.0),
        new ArenaBounds(-20.0, 20.0, -4.0, 4.0),
        -12.0,
        boxes,
        new[]
        {
            new SpawnRegion("left", new ArenaBounds(-0.1, 0.1, -0.1, 0.1), boxes[0].Id),
            new SpawnRegion("right", new ArenaBounds(9.9, 10.1, -0.1, 0.1), boxes[0].Id),
        });

    private static void Settle(World world)
    {
        for (var tick = 0; tick < 5 && !world.Players[0].IsGrounded; tick++)
        {
            Step(world, default);
        }
        Assert.True(world.Players[0].IsGrounded);
    }

    private static void Step(World world, PlayerInput first)
    {
        Sim.Step(world, new[] { first, default(PlayerInput) });
    }
}
