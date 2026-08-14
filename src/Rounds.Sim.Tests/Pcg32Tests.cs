using Rounds.Sim.Math;

namespace Rounds.Sim.Tests;

public sealed class Pcg32Tests
{
    [Fact]
    public void BoundedDrawsRejectZeroAndStayDeterministicAndInRange()
    {
        var first = new Pcg32(1234);
        var second = new Pcg32(1234);
        Assert.Throws<ArgumentOutOfRangeException>(() => first.NextBounded(0));

        var firstValues = Enumerable.Range(0, 100).Select(_ => first.NextBounded(7)).ToArray();
        var secondValues = Enumerable.Range(0, 100).Select(_ => second.NextBounded(7)).ToArray();

        Assert.Equal(firstValues, secondValues);
        Assert.All(firstValues, value => Assert.InRange(value, 0U, 6U));
        Assert.Equal(first.State, second.State);
    }
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
