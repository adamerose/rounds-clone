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
    public void BoundedDrawRejectsAKnownLowValueBeforeReturningTheNextDraw()
    {
        const uint bound = 2_147_483_649;
        var raw = new Pcg32(3);
        Assert.Equal(1_282_583_244U, raw.NextUInt());
        Assert.Equal(4_290_596_118U, raw.NextUInt());

        var bounded = new Pcg32(3);
        Assert.Equal(2_143_112_469U, bounded.NextBounded(bound));
        Assert.Equal(13_416_056_705_687_159_891UL, bounded.State);
        Assert.Equal(raw.State, bounded.State);
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
