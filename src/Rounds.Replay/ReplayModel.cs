using Rounds.Sim;
using Rounds.Sim.Math;

namespace Rounds.Replay;

public static class ReplayFormat
{
    public const int Version = 1;
    public const int TargetBuild = 21020021;
    public const string Ruleset = "base-combat-v1";
    public const int TickRate = 60;
    public const int PlayerCount = 2;
    public const int MaximumTicks = 216_000;
    public const string FileSuffix = ".rounds-replay.json";
}

public sealed record RecordedPlayerInput(
    sbyte MoveAxis,
    ulong AimXBits,
    ulong AimYBits,
    bool JumpHeld,
    bool FireHeld,
    bool BlockHeld)
{
    public static RecordedPlayerInput FromPlayerInput(PlayerInput input) =>
        new(
            input.MoveAxis,
            BitConverter.DoubleToUInt64Bits(input.AimDirection.X),
            BitConverter.DoubleToUInt64Bits(input.AimDirection.Y),
            input.JumpHeld,
            input.FireHeld,
            input.BlockHeld);

    public PlayerInput ToPlayerInput() =>
        new(
            MoveAxis,
            JumpHeld,
            FireHeld,
            BlockHeld,
            new Vec2(
                BitConverter.UInt64BitsToDouble(AimXBits),
                BitConverter.UInt64BitsToDouble(AimYBits)));
}

public sealed record RecordedFrame(RecordedPlayerInput Player0, RecordedPlayerInput Player1)
{
    public static RecordedFrame FromInputs(ReadOnlySpan<PlayerInput> inputs)
    {
        if (inputs.Length != ReplayFormat.PlayerCount)
        {
            throw new ArgumentException("A replay frame requires exactly two player inputs.", nameof(inputs));
        }

        return new RecordedFrame(
            RecordedPlayerInput.FromPlayerInput(inputs[0]),
            RecordedPlayerInput.FromPlayerInput(inputs[1]));
    }

    public PlayerInput[] ToInputs() => [Player0.ToPlayerInput(), Player1.ToPlayerInput()];
}

public sealed record ReplayRun(int Length, RecordedFrame Frame);

public sealed record ReplayCheckpoint(int Tick, ulong Hash);

public sealed class ReplayDocument
{
    public ReplayDocument(
        string replayId,
        ulong seed,
        string arenaId,
        int totalTicks,
        IEnumerable<ReplayRun> runs,
        IEnumerable<ReplayCheckpoint> checkpoints,
        ulong finalHash)
    {
        ReplayId = replayId;
        Seed = seed;
        ArenaId = arenaId;
        TotalTicks = totalTicks;
        Runs = Array.AsReadOnly(runs.ToArray());
        Checkpoints = Array.AsReadOnly(checkpoints.ToArray());
        FinalHash = finalHash;
    }

    public string ReplayId { get; }

    public ulong Seed { get; }

    public string ArenaId { get; }

    public int TotalTicks { get; }

    public IReadOnlyList<ReplayRun> Runs { get; }

    public IReadOnlyList<ReplayCheckpoint> Checkpoints { get; }

    public ulong FinalHash { get; }
}

public sealed class ReplayMismatchException : Exception
{
    public ReplayMismatchException(string replayId, int tick, ulong expectedHash, ulong actualHash)
        : base($"Replay `{replayId}` diverged at tick {tick}: expected {expectedHash:x16}, actual {actualHash:x16}.")
    {
        ReplayId = replayId;
        Tick = tick;
        ExpectedHash = expectedHash;
        ActualHash = actualHash;
    }

    public string ReplayId { get; }

    public int Tick { get; }

    public ulong ExpectedHash { get; }

    public ulong ActualHash { get; }
}
