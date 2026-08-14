using Rounds.Sim.Math;
using Rounds.Sim.Maps;

namespace Rounds.Sim;

public sealed class World
{
    public const int TickRate = 60;

    public World(ulong seed, ArenaDefinition arena, PlayerTuning tuning)
        : this(seed, arena, tuning, CombatTuning.Vanilla)
    {
    }

    public World(ulong seed, ArenaDefinition arena, PlayerTuning tuning, CombatTuning combatTuning)
    {
        ArgumentNullException.ThrowIfNull(arena);
        ArgumentNullException.ThrowIfNull(tuning);
        ArgumentNullException.ThrowIfNull(combatTuning);
        combatTuning.Validate();
        Seed = seed;
        Rng = new Pcg32(seed);
        Arena = arena;
        Tuning = tuning;
        Combat = combatTuning;
    }

    public ulong Seed { get; }

    public long Tick { get; set; }

    public Pcg32 Rng { get; }

    public ArenaDefinition Arena { get; }

    public PlayerTuning Tuning { get; }

    public CombatTuning Combat { get; }

    public List<Player> Players { get; } = [];

    public List<Bullet> Bullets { get; } = [];

    public long NextBulletId { get; internal set; }

    public long DroppedBulletCount { get; internal set; }

    public DuelPhase Phase { get; internal set; }

    public int PhaseTicksRemaining { get; internal set; }

    public int DuelNumber { get; internal set; }

    public int DuelResultCount { get; internal set; }

    public int? WinnerId { get; internal set; }

    public bool IsDraw { get; internal set; }

    internal int? PendingWinnerId { get; set; }

    internal bool PendingDraw { get; set; }

    public static World CreateSmoke(ulong seed)
    {
        return CreateMatch(
            seed,
            ArenaCatalog.LoadEmbedded().GetRequired("arena-006"),
            PlayerTuning.Vanilla,
            CombatTuning.Vanilla);
    }

    public static World CreateMatch(
        ulong seed,
        ArenaDefinition arena,
        PlayerTuning? tuning = null,
        CombatTuning? combatTuning = null)
    {
        var resolvedTuning = tuning ?? PlayerTuning.Vanilla;
        var resolvedCombat = combatTuning ?? CombatTuning.Vanilla;
        var world = new World(seed, arena, resolvedTuning, resolvedCombat);
        world.Players.Add(new Player
        {
            Id = 0,
            TeamId = 0,
        });
        world.Players.Add(new Player
        {
            Id = 1,
            TeamId = 1,
        });
        world.ResetDuel(incrementDuel: false);
        return world;
    }

    internal void ResetDuel(bool incrementDuel)
    {
        if (incrementDuel)
        {
            DuelNumber++;
        }

        Bullets.Clear();
        WinnerId = null;
        IsDraw = false;
        PendingWinnerId = null;
        PendingDraw = false;
        Phase = DuelPhase.Spawning;
        PhaseTicksRemaining = Combat.SpawnLockTicks;
        for (var index = 0; index < Players.Count; index++)
        {
            var player = Players[index];
            player.Position = Arena.Spawns[index].Center;
            player.Velocity = Vec2.Zero;
            player.IsGrounded = false;
            player.JumpsRemaining = Tuning.JumpCapacity;
            player.JumpBufferTicksRemaining = 0;
            player.JumpCutAvailable = false;
            player.WasJumpHeld = false;
            player.AimDirection = index == 0 ? new Vec2(1.0, 0.0) : new Vec2(-1.0, 0.0);
            player.Health = Combat.BaseHealth;
            player.Ammo = Combat.BaseAmmo;
            player.FireCooldownTicksRemaining = 0;
            player.ReloadTicksRemaining = 0;
            player.BlockPhase = BlockPhase.Ready;
            player.BlockTicksRemaining = 0;
            player.WasBlockHeld = false;
            player.IsAlive = true;
            player.LastInputBits = 0;
        }
    }
}
