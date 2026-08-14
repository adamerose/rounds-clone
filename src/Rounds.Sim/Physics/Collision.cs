using Rounds.Sim.Math;

namespace Rounds.Sim.Physics;

public static class Collision
{
    public const double TimeEpsilon = 1e-10;
    public const double ContactEpsilon = 1e-10;
    private const double ContactEpsilonSquared = ContactEpsilon * ContactEpsilon;

    public static SweepHit SweepCircle(Vec2 origin, double radius, Vec2 delta, Obb box)
    {
        if (!double.IsFinite(radius) || radius <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(radius));
        }

        var localOrigin = box.ToLocalPoint(origin);
        var localDelta = box.ToLocalVector(delta);
        var overlap = InitialOverlap(localOrigin, radius, box.HalfExtents, box);
        if (overlap.HasHit)
        {
            return overlap with { Normal = box.ToWorldVector(overlap.Normal).Normalized() };
        }

        if (localDelta.LengthSquared == 0.0)
        {
            return SweepHit.None;
        }

        var best = SweepHit.None;
        TestVerticalSide(-1.0, localOrigin, localDelta, radius, box, ref best);
        TestVerticalSide(1.0, localOrigin, localDelta, radius, box, ref best);
        TestHorizontalSide(-1.0, localOrigin, localDelta, radius, box, ref best);
        TestHorizontalSide(1.0, localOrigin, localDelta, radius, box, ref best);
        TestCorner(-1.0, -1.0, localOrigin, localDelta, radius, box, ref best);
        TestCorner(-1.0, 1.0, localOrigin, localDelta, radius, box, ref best);
        TestCorner(1.0, -1.0, localOrigin, localDelta, radius, box, ref best);
        TestCorner(1.0, 1.0, localOrigin, localDelta, radius, box, ref best);
        return best.HasHit
            ? best with { Normal = box.ToWorldVector(best.Normal).Normalized() }
            : best;
    }

    public static SweepHit SweepCircle(Vec2 origin, double radius, Vec2 delta, IReadOnlyList<Obb> boxes)
    {
        var best = SweepHit.None;
        foreach (var box in boxes)
        {
            var candidate = SweepCircle(origin, radius, delta, box);
            if (!candidate.HasHit)
            {
                continue;
            }

            if (!best.HasHit ||
                candidate.Time < best.Time - TimeEpsilon ||
                (System.Math.Abs(candidate.Time - best.Time) <= TimeEpsilon &&
                 candidate.SourceOrder < best.SourceOrder))
            {
                best = candidate;
            }
        }

        return best;
    }

    private static SweepHit InitialOverlap(Vec2 origin, double radius, Vec2 half, Obb box)
    {
        var closest = new Vec2(
            System.Math.Clamp(origin.X, -half.X, half.X),
            System.Math.Clamp(origin.Y, -half.Y, half.Y));
        var offset = origin - closest;
        var distanceSquared = offset.LengthSquared;
        if (distanceSquared > ContactEpsilonSquared)
        {
            var distance = System.Math.Sqrt(distanceSquared);
            if (distance >= radius - ContactEpsilon)
            {
                return SweepHit.None;
            }

            return new SweepHit(
                true,
                0.0,
                offset / distance,
                radius - distance,
                box.SourceOrder,
                box.Id);
        }

        var left = origin.X + half.X;
        var right = half.X - origin.X;
        var bottom = origin.Y + half.Y;
        var top = half.Y - origin.Y;
        var minimum = left;
        var normal = new Vec2(-1.0, 0.0);
        if (right < minimum)
        {
            minimum = right;
            normal = new Vec2(1.0, 0.0);
        }
        if (bottom < minimum)
        {
            minimum = bottom;
            normal = new Vec2(0.0, -1.0);
        }
        if (top < minimum)
        {
            minimum = top;
            normal = new Vec2(0.0, 1.0);
        }

        return new SweepHit(true, 0.0, normal, minimum + radius, box.SourceOrder, box.Id);
    }

    private static void TestVerticalSide(
        double sign,
        Vec2 origin,
        Vec2 delta,
        double radius,
        Obb box,
        ref SweepHit best)
    {
        if ((sign < 0.0 && delta.X <= 0.0) || (sign > 0.0 && delta.X >= 0.0))
        {
            return;
        }

        var plane = sign * (box.HalfExtents.X + radius);
        var time = (plane - origin.X) / delta.X;
        if (!ValidTime(time, best.Time))
        {
            return;
        }

        var y = origin.Y + (delta.Y * time);
        if (y < -box.HalfExtents.Y - ContactEpsilon || y > box.HalfExtents.Y + ContactEpsilon)
        {
            return;
        }

        best = new SweepHit(true, time, new Vec2(sign, 0.0), 0.0, box.SourceOrder, box.Id);
    }

    private static void TestHorizontalSide(
        double sign,
        Vec2 origin,
        Vec2 delta,
        double radius,
        Obb box,
        ref SweepHit best)
    {
        if ((sign < 0.0 && delta.Y <= 0.0) || (sign > 0.0 && delta.Y >= 0.0))
        {
            return;
        }

        var plane = sign * (box.HalfExtents.Y + radius);
        var time = (plane - origin.Y) / delta.Y;
        if (!ValidTime(time, best.Time))
        {
            return;
        }

        var x = origin.X + (delta.X * time);
        if (x < -box.HalfExtents.X - ContactEpsilon || x > box.HalfExtents.X + ContactEpsilon)
        {
            return;
        }

        best = new SweepHit(true, time, new Vec2(0.0, sign), 0.0, box.SourceOrder, box.Id);
    }

    private static void TestCorner(
        double signX,
        double signY,
        Vec2 origin,
        Vec2 delta,
        double radius,
        Obb box,
        ref SweepHit best)
    {
        var corner = new Vec2(signX * box.HalfExtents.X, signY * box.HalfExtents.Y);
        var fromCorner = origin - corner;
        var a = delta.LengthSquared;
        var b = 2.0 * Vec2.Dot(fromCorner, delta);
        var c = fromCorner.LengthSquared - (radius * radius);
        var discriminant = (b * b) - (4.0 * a * c);
        if (discriminant < 0.0)
        {
            return;
        }

        var root = System.Math.Sqrt(System.Math.Max(0.0, discriminant));
        var time = (-b - root) / (2.0 * a);
        if (!ValidTime(time, best.Time))
        {
            return;
        }

        var point = origin + (delta * time);
        if ((signX < 0.0 && point.X > -box.HalfExtents.X + ContactEpsilon) ||
            (signX > 0.0 && point.X < box.HalfExtents.X - ContactEpsilon) ||
            (signY < 0.0 && point.Y > -box.HalfExtents.Y + ContactEpsilon) ||
            (signY > 0.0 && point.Y < box.HalfExtents.Y - ContactEpsilon))
        {
            return;
        }

        best = new SweepHit(
            true,
            time,
            (point - corner).Normalized(),
            0.0,
            box.SourceOrder,
            box.Id);
    }

    private static bool ValidTime(double time, double bestTime) =>
        time >= -TimeEpsilon && time <= 1.0 + TimeEpsilon && time < bestTime - TimeEpsilon;
}
