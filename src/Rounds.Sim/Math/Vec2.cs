namespace Rounds.Sim.Math;

public readonly record struct Vec2(double X, double Y)
{
    public static Vec2 Zero => new(0.0, 0.0);

    public static Vec2 operator +(Vec2 left, Vec2 right) =>
        new(left.X + right.X, left.Y + right.Y);

    public static Vec2 operator -(Vec2 left, Vec2 right) =>
        new(left.X - right.X, left.Y - right.Y);

    public static Vec2 operator *(Vec2 value, double scalar) =>
        new(value.X * scalar, value.Y * scalar);
}
