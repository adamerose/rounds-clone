using Rounds.Sim.Math;

namespace Rounds.Sim;

public sealed class World
{
    public const int TickRate = 60;

    public World(ulong seed)
    {
        Seed = seed;
        Rng = new Pcg32(seed);
    }

    public ulong Seed { get; }

    public long Tick { get; set; }

    public Pcg32 Rng { get; }

    public List<Player> Players { get; } = [];

    public static World CreateSmoke(ulong seed)
    {
        var world = new World(seed);
        world.Players.Add(new Player
        {
            Id = 0,
            TeamId = 0,
            Position = new Vec2(-6.0, 0.0),
        });
        world.Players.Add(new Player
        {
            Id = 1,
            TeamId = 1,
            Position = new Vec2(6.0, 0.0),
        });
        return world;
    }
}
