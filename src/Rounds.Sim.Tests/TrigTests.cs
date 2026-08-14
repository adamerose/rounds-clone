using Rounds.Sim.Math;

namespace Rounds.Sim.Tests;

public sealed class TrigTests
{
    [Theory]
    [InlineData(-45.0)]
    [InlineData(-30.0)]
    [InlineData(0.0)]
    [InlineData(30.0)]
    [InlineData(45.0)]
    [InlineData(135.0)]
    [InlineData(270.0)]
    [InlineData(-810.0)]
    public void DeterministicPolynomialMatchesReferenceAngles(double degrees)
    {
        var (sine, cosine) = Trig.SinCosDegrees(degrees);
        var radians = degrees * System.Math.PI / 180.0;

        Assert.Equal(System.Math.Sin(radians), sine, precision: 8);
        Assert.Equal(System.Math.Cos(radians), cosine, precision: 8);
    }
}
