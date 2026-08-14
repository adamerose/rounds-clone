using Rounds.Sim.Math;

namespace Rounds.Sim.Physics;

public readonly record struct SweepHit(
    bool HasHit,
    double Time,
    Vec2 Normal,
    double Separation,
    int SourceOrder,
    string PrimitiveId)
{
    public static SweepHit None => new(false, 1.0, Vec2.Zero, 0.0, int.MaxValue, string.Empty);
}
