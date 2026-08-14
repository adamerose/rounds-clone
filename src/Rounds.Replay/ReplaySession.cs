using Rounds.Sim;
using Rounds.Sim.Maps;

namespace Rounds.Replay;

public sealed class ReplayRecorder
{
    private readonly List<ReplayRun> _runs = [];
    private readonly List<ReplayCheckpoint> _checkpoints = [];
    private readonly int _totalTicks;
    private int _recordedTicks;

    public ReplayRecorder(string replayId, ulong seed, string arenaId, int totalTicks)
    {
        var arena = ReplayValidator.ValidateHeader(replayId, arenaId, totalTicks);
        ReplayId = replayId;
        ArenaId = arenaId;
        _totalTicks = totalTicks;
        World = World.CreateMatch(seed, arena);
    }

    public string ReplayId { get; }

    public string ArenaId { get; }

    public World World { get; }

    public int RecordedTicks => _recordedTicks;

    public void Step(ReadOnlySpan<PlayerInput> inputs)
    {
        if (_recordedTicks >= _totalTicks)
        {
            throw new InvalidOperationException("Replay recorder already consumed its declared tick count.");
        }

        var frame = RecordedFrame.FromInputs(inputs);
        ReplayValidator.ValidateFrame(frame);
        Rounds.Sim.Sim.Step(World, inputs);
        if (_runs.Count > 0 && _runs[^1].Frame == frame)
        {
            _runs[^1] = _runs[^1] with { Length = _runs[^1].Length + 1 };
        }
        else
        {
            _runs.Add(new ReplayRun(1, frame));
        }

        _recordedTicks++;
        if (_recordedTicks % ReplayFormat.TickRate == 0 || _recordedTicks == _totalTicks)
        {
            _checkpoints.Add(new ReplayCheckpoint(_recordedTicks, Rounds.Sim.Sim.Hash(World)));
        }
    }

    public ReplayDocument Finish()
    {
        if (_recordedTicks != _totalTicks)
        {
            throw new InvalidOperationException($"Replay recorder expected {_totalTicks} ticks but received {_recordedTicks}.");
        }

        var replay = new ReplayDocument(
            ReplayId,
            World.Seed,
            ArenaId,
            _totalTicks,
            _runs,
            _checkpoints,
            Rounds.Sim.Sim.Hash(World));
        ReplayValidator.Validate(replay);
        return replay;
    }

}

public sealed class ReplayPlayback
{
    private readonly ReplayDocument _replay;
    private int _runIndex;
    private int _ticksInRun;
    private int _checkpointIndex;

    public ReplayPlayback(ReplayDocument replay)
    {
        ArgumentNullException.ThrowIfNull(replay);
        ReplayValidator.Validate(replay);
        _replay = replay;
        World = World.CreateMatch(
            replay.Seed,
            ArenaCatalog.LoadEmbedded().GetRequired(replay.ArenaId));
    }

    public ReplayDocument Replay => _replay;

    public World World { get; }

    public int ConsumedTicks { get; private set; }

    public bool IsComplete => ConsumedTicks == _replay.TotalTicks;

    public void StepNext()
    {
        if (IsComplete)
        {
            throw new InvalidOperationException("Replay playback is already complete.");
        }

        var run = _replay.Runs[_runIndex];
        var inputs = run.Frame.ToInputs();
        Rounds.Sim.Sim.Step(World, inputs);
        ConsumedTicks++;
        _ticksInRun++;
        if (_ticksInRun == run.Length)
        {
            _runIndex++;
            _ticksInRun = 0;
        }

        if (_checkpointIndex < _replay.Checkpoints.Count &&
            _replay.Checkpoints[_checkpointIndex].Tick == ConsumedTicks)
        {
            var expected = _replay.Checkpoints[_checkpointIndex].Hash;
            var actual = Rounds.Sim.Sim.Hash(World);
            if (actual != expected)
            {
                throw new ReplayMismatchException(_replay.ReplayId, ConsumedTicks, expected, actual);
            }
            _checkpointIndex++;
        }
    }

    public void RunToEnd()
    {
        while (!IsComplete)
        {
            StepNext();
        }
    }
}

public sealed record ReplayVerification(string ReplayId, int TotalTicks, ulong FinalHash);

public static class ReplayCorpus
{
    public static IReadOnlyList<ReplayVerification> VerifyDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Replay directory `{directory}` does not exist.");
        }

        var files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => Path.GetFileName(path).EndsWith(ReplayFormat.FileSuffix, StringComparison.Ordinal))
            .OrderBy(Path.GetFileName, StringComparer.Ordinal)
            .ToArray();
        if (files.Length == 0)
        {
            throw new InvalidDataException("Replay corpus is empty.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<ReplayVerification>();
        foreach (var file in files)
        {
            using var stream = File.OpenRead(file);
            var replay = ReplayCodec.Load(stream);
            var basename = Path.GetFileName(file);
            var stem = basename[..^ReplayFormat.FileSuffix.Length];
            if (!ids.Add(replay.ReplayId))
            {
                throw new InvalidDataException($"Replay corpus duplicates ID `{replay.ReplayId}`.");
            }
            if (!string.Equals(stem, replay.ReplayId, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Replay filename `{basename}` does not match ID `{replay.ReplayId}`.");
            }

            var playback = new ReplayPlayback(replay);
            playback.RunToEnd();
            results.Add(new ReplayVerification(replay.ReplayId, replay.TotalTicks, replay.FinalHash));
        }

        return results.AsReadOnly();
    }
}
