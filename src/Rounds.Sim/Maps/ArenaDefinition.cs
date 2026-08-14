using System.Collections.ObjectModel;
using Rounds.Sim.Physics;

namespace Rounds.Sim.Maps;

public sealed class ArenaDefinition
{
    private readonly ReadOnlyCollection<Obb> _staticBoxes;
    private readonly ReadOnlyCollection<SpawnRegion> _spawns;

    public ArenaDefinition(
        string id,
        ArenaBounds cameraBounds,
        ArenaBounds collisionBounds,
        double killBoundaryY,
        IEnumerable<Obb> staticBoxes,
        IEnumerable<SpawnRegion> spawns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(staticBoxes);
        ArgumentNullException.ThrowIfNull(spawns);
        if (!cameraBounds.IsValid || !collisionBounds.IsValid || !double.IsFinite(killBoundaryY))
        {
            throw new ArgumentException("Arena bounds and kill boundary must be finite and ordered.");
        }

        var boxes = staticBoxes.ToArray();
        var spawnArray = spawns.ToArray();
        if (boxes.Length == 0 || spawnArray.Length != 2)
        {
            throw new ArgumentException("An arena requires static collision and exactly two spawn regions.");
        }

        Id = id;
        CameraBounds = cameraBounds;
        CollisionBounds = collisionBounds;
        KillBoundaryY = killBoundaryY;
        _staticBoxes = Array.AsReadOnly(boxes);
        _spawns = Array.AsReadOnly(spawnArray);
    }

    public string Id { get; }

    public ArenaBounds CameraBounds { get; }

    public ArenaBounds CollisionBounds { get; }

    public double KillBoundaryY { get; }

    public IReadOnlyList<Obb> StaticBoxes => _staticBoxes;

    public IReadOnlyList<SpawnRegion> Spawns => _spawns;
}
