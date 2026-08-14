using Rounds.Sim.Math;

namespace Rounds.Sim;

public sealed class Player
{
    public required int Id { get; init; }

    public required int TeamId { get; init; }

    public Vec2 Position { get; set; }

    public Vec2 Velocity { get; set; }

    public byte LastInputBits { get; set; }

    public ulong InputChecksum { get; set; }
}
