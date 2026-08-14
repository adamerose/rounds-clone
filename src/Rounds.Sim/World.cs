using Rounds.Sim.Math;
using Rounds.Sim.Maps;

namespace Rounds.Sim;

public sealed class World
{
    public const int TickRate = 60;

    public World(ulong seed, ArenaDefinition arena, PlayerTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(tuning);
        Seed = seed;
        Rng = new Pcg32(seed);
        Arena = arena;
        Tuning = tuning;
    }

    public ulong Seed { get; }

    public long Tick { get; set; }

    public Pcg32 Rng { get; }

    public ArenaDefinition Arena { get; }

    public PlayerTuning Tuning { get; }

    public List<Player> Players { get; } = [];

    public static World CreateSmoke(ulong seed)
    {
        return CreateMatch(seed, ArenaCatalog.LoadEmbedded().GetRequired("arena-006"), PlayerTuning.Vanilla);
    }

    public static World CreateMatch(ulong seed, ArenaDefinition arena, PlayerTuning? tuning = null)
    {
        var resolvedTuning = tuning ?? PlayerTuning.Vanilla;
        var world = new World(seed, arena, resolvedTuning);
        world.Players.Add(new Player
        {
            Id = 0,
            TeamId = 0,
            Position = arena.Spawns[0].Center,
            JumpsRemaining = resolvedTuning.JumpCapacity,
        });
        world.Players.Add(new Player
        {
            Id = 1,
            TeamId = 1,
            Position = arena.Spawns[1].Center,
            JumpsRemaining = resolvedTuning.JumpCapacity,
        });
        return world;
    }
}
