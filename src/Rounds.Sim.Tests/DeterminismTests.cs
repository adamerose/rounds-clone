namespace Rounds.Sim.Tests;

public sealed class DeterminismTests
{
    [Fact]
    public void SameSeedAndInputsProduceSameHash()
    {
        var first = Run(seed: 42UL, mutateLastInput: false);
        var second = Run(seed: 42UL, mutateLastInput: false);

        Assert.Equal(first, second);
    }

    [Fact]
    public void InputChangeProducesDifferentHash()
    {
        var baseline = Run(seed: 42UL, mutateLastInput: false);
        var changed = Run(seed: 42UL, mutateLastInput: true);

        Assert.NotEqual(baseline, changed);
    }

    [Fact]
    public void OneMovementInputChangesCompleteArenaStateHash()
    {
        var idle = World.CreateSmoke(42UL);
        var moving = World.CreateSmoke(42UL);

        Sim.Step(idle, [default, default]);
        Sim.Step(moving, [new PlayerInput(1, false, false, false), default]);

        Assert.NotEqual(Sim.Hash(idle), Sim.Hash(moving));
        Assert.NotEqual(idle.Players[0].Velocity.X, moving.Players[0].Velocity.X);
    }

    [Fact]
    public void SeedChangeProducesDifferentHash()
    {
        Assert.NotEqual(
            Run(seed: 42UL, mutateLastInput: false),
            Run(seed: 43UL, mutateLastInput: false));
    }

    [Fact]
    public void StepRequiresExactlyOneInputPerPlayer()
    {
        var world = World.CreateSmoke(1UL);

        Assert.Throws<ArgumentException>(() => Sim.Step(world, [default]));
    }

    private static ulong Run(ulong seed, bool mutateLastInput)
    {
        var world = World.CreateSmoke(seed);
        var inputs = new PlayerInput[world.Players.Count];
        for (var tick = 0; tick < 240; tick++)
        {
            inputs[0] = new PlayerInput((sbyte)(tick % 3 - 1), tick % 31 == 0, tick % 17 == 0, false);
            inputs[1] = new PlayerInput((sbyte)(1 - tick % 3), false, tick % 19 == 0, tick % 47 == 0);
            if (mutateLastInput && tick == 239)
            {
                inputs[1] = inputs[1] with { FirePressed = !inputs[1].FirePressed };
            }

            Sim.Step(world, inputs);
        }

        return Sim.Hash(world);
    }
}
