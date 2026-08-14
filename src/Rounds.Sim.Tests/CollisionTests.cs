using Rounds.Sim.Math;
using Rounds.Sim.Physics;

namespace Rounds.Sim.Tests;

public sealed class CollisionTests
{
    [Fact]
    public void AxisAlignedSideHitReturnsExactTimeAndNormal()
    {
        var box = Obb.Create("box", 0, Vec2.Zero, 2.0, 2.0, 0.0);

        var hit = Collision.SweepCircle(new Vec2(-3.0, 0.0), 0.5, new Vec2(4.0, 0.0), box);

        Assert.True(hit.HasHit);
        Assert.Equal(0.375, hit.Time, precision: 12);
        AssertVector(new Vec2(-1.0, 0.0), hit.Normal);
    }

    [Fact]
    public void RotatedFaceReturnsWorldNormal()
    {
        var box = Obb.Create("slope", 0, Vec2.Zero, 4.0, 1.0, 45.0);
        var origin = box.AxisY * 3.0;

        var hit = Collision.SweepCircle(origin, 0.5, box.AxisY * -4.0, box);

        Assert.True(hit.HasHit);
        Assert.Equal(0.5, hit.Time, precision: 10);
        AssertVector(box.AxisY, hit.Normal, precision: 10);
    }

    [Fact]
    public void RoundedCornerReturnsRadialNormal()
    {
        var box = Obb.Create("box", 0, Vec2.Zero, 2.0, 2.0, 0.0);

        var hit = Collision.SweepCircle(new Vec2(-3.0, -3.0), 0.5, new Vec2(3.0, 3.0), box);

        Assert.True(hit.HasHit);
        Assert.InRange(hit.Time, 0.54881, 0.54882);
        var diagonal = -1.0 / System.Math.Sqrt(2.0);
        AssertVector(new Vec2(diagonal, diagonal), hit.Normal, precision: 10);
    }

    [Fact]
    public void InitialOverlapReportsSeparation()
    {
        var box = Obb.Create("box", 0, Vec2.Zero, 2.0, 2.0, 0.0);

        var hit = Collision.SweepCircle(new Vec2(1.25, 0.0), 0.5, Vec2.Zero, box);

        Assert.True(hit.HasHit);
        Assert.Equal(0.0, hit.Time);
        Assert.Equal(0.25, hit.Separation, precision: 12);
        AssertVector(new Vec2(1.0, 0.0), hit.Normal);
    }

    [Fact]
    public void ZeroMotionOutsideBoxDoesNotHit()
    {
        var box = Obb.Create("box", 0, Vec2.Zero, 2.0, 2.0, 0.0);

        var hit = Collision.SweepCircle(new Vec2(3.0, 0.0), 0.5, Vec2.Zero, box);

        Assert.False(hit.HasHit);
    }

    [Fact]
    public void ExactTimeTieKeepsSourceOrder()
    {
        var first = Obb.Create("first", 0, Vec2.Zero, 2.0, 2.0, 0.0);
        var second = Obb.Create("second", 1, Vec2.Zero, 2.0, 2.0, 0.0);

        var hit = Collision.SweepCircle(new Vec2(-3.0, 0.0), 0.5, new Vec2(4.0, 0.0), new[] { second, first });

        Assert.Equal("first", hit.PrimitiveId);
        Assert.Equal(0, hit.SourceOrder);
    }

    [Fact]
    public void HighSpeedCircleCannotTunnelThroughThinPlatform()
    {
        var platform = Obb.Create("thin", 0, Vec2.Zero, 10.0, 0.1, 0.0);

        var hit = Collision.SweepCircle(new Vec2(0.0, 5.0), 0.5, new Vec2(0.0, -20.0), platform);

        Assert.True(hit.HasHit);
        Assert.Equal(0.2225, hit.Time, precision: 12);
        AssertVector(new Vec2(0.0, 1.0), hit.Normal);
    }

    private static void AssertVector(Vec2 expected, Vec2 actual, int precision = 12)
    {
        Assert.Equal(expected.X, actual.X, precision);
        Assert.Equal(expected.Y, actual.Y, precision);
    }
}
