namespace Rounds.Sim.Math;

public readonly record struct Vec2(double X, double Y)
{
    public static Vec2 Zero => new(0.0, 0.0);

    public double LengthSquared => Dot(this, this);

    public double Length => System.Math.Sqrt(LengthSquared);

    public static Vec2 operator +(Vec2 left, Vec2 right) =>
        new(left.X + right.X, left.Y + right.Y);

    public static Vec2 operator -(Vec2 left, Vec2 right) =>
        new(left.X - right.X, left.Y - right.Y);

    public static Vec2 operator -(Vec2 value) => new(-value.X, -value.Y);

    public static Vec2 operator *(Vec2 value, double scalar) =>
        new(value.X * scalar, value.Y * scalar);

    public static Vec2 operator /(Vec2 value, double scalar) =>
        new(value.X / scalar, value.Y / scalar);

    public static double Dot(Vec2 left, Vec2 right) =>
        (left.X * right.X) + (left.Y * right.Y);

    public Vec2 Normalized()
    {
        var scale = System.Math.Max(System.Math.Abs(X), System.Math.Abs(Y));
        if (scale == 0.0)
        {
            return Zero;
        }

        var scaled = this / scale;
        return scaled / System.Math.Sqrt(scaled.LengthSquared);
    }
}
