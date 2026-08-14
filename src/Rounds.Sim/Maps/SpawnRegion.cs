using Rounds.Sim.Math;

namespace Rounds.Sim.Maps;

public readonly record struct SpawnRegion(
    string Id,
    ArenaBounds Bounds,
    string SupportPrimitiveId)
{
    public Vec2 Center => new(
        (Bounds.XMin + Bounds.XMax) / 2.0,
        (Bounds.YMin + Bounds.YMax) / 2.0);
}
