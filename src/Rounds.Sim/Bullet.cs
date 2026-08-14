using Rounds.Sim.Math;

namespace Rounds.Sim;

public sealed class Bullet
{
    public required long Id { get; init; }

    public required int OwnerId { get; set; }

    public Vec2 Position { get; set; }

    public Vec2 Velocity { get; set; }

    public double Radius { get; init; }

    public double Damage { get; init; }

    public int BouncesRemaining { get; set; }

    public int SweepsCompleted { get; set; }
}
