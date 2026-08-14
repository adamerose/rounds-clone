using Rounds.Sim.Math;

namespace Rounds.Sim.Physics;

public readonly record struct Obb
{
    private Obb(
        string id,
        int sourceOrder,
        Vec2 center,
        Vec2 halfExtents,
        Vec2 axisX,
        Vec2 axisY,
        double rotationDegrees)
    {
        Id = id;
        SourceOrder = sourceOrder;
        Center = center;
        HalfExtents = halfExtents;
        AxisX = axisX;
        AxisY = axisY;
        RotationDegrees = rotationDegrees;
    }

    public string Id { get; }

    public int SourceOrder { get; }

    public Vec2 Center { get; }

    public Vec2 HalfExtents { get; }

    public Vec2 AxisX { get; }

    public Vec2 AxisY { get; }

    public double RotationDegrees { get; }

    public double Width => HalfExtents.X * 2.0;

    public double Height => HalfExtents.Y * 2.0;

    public static Obb Create(
        string id,
        int sourceOrder,
        Vec2 center,
        double width,
        double height,
        double rotationDegrees)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (sourceOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceOrder));
        }

        if (!double.IsFinite(width) || width <= 0.0 ||
            !double.IsFinite(height) || height <= 0.0 ||
            !double.IsFinite(rotationDegrees))
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Box dimensions and rotation must be finite, with positive dimensions.");
        }

        var (sine, cosine) = Trig.SinCosDegrees(rotationDegrees);
        return new Obb(
            id,
            sourceOrder,
            center,
            new Vec2(width / 2.0, height / 2.0),
            new Vec2(cosine, sine),
            new Vec2(-sine, cosine),
            rotationDegrees);
    }

    public Vec2 ToLocalPoint(Vec2 point)
    {
        var delta = point - Center;
        return new Vec2(Vec2.Dot(delta, AxisX), Vec2.Dot(delta, AxisY));
    }

    public Vec2 ToLocalVector(Vec2 vector) =>
        new(Vec2.Dot(vector, AxisX), Vec2.Dot(vector, AxisY));

    public Vec2 ToWorldVector(Vec2 vector) =>
        (AxisX * vector.X) + (AxisY * vector.Y);
}
