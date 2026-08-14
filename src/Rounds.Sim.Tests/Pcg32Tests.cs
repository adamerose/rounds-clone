using Rounds.Sim.Math;

namespace Rounds.Sim.Tests;

public sealed class Pcg32Tests
{
    [Fact]
    public void EqualSeedsProduceEqualSequences()
    {
        var first = new Pcg32(1234UL);
        var second = new Pcg32(1234UL);

        for (var index = 0; index < 1000; index++)
        {
            Assert.Equal(first.NextUInt(), second.NextUInt());
        }

        Assert.Equal(first.State, second.State);
    }

    [Fact]
    public void DifferentSeedsDiverge()
    {
        var first = new Pcg32(1234UL);
        var second = new Pcg32(1235UL);

        Assert.NotEqual(first.NextUInt(), second.NextUInt());
    }
}
