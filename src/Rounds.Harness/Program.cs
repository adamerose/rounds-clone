using System.Globalization;
using Rounds.Replay;
using Rounds.Sim;
using Rounds.Sim.Math;

try
{
    return HarnessCommands.Run(args);
}
catch (Exception exception) when (exception is ArgumentException or IOException or InvalidDataException or ReplayMismatchException or InvalidOperationException)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}

internal static class HarnessCommands
{
    private const int DefaultTicks = 600;

    public static int Run(string[] args)
    {
        var command = args.Length == 0 ? "smoke" : args[0];
        var options = Options.Parse(args.Skip(1));
        return command switch
        {
            "smoke" => Smoke(options),
            "record" => Record(options),
            "replay" => Replay(options),
            "verify-replays" => VerifyReplays(options),
            _ => throw new ArgumentException(
                "Usage: Rounds.Harness smoke|record|replay|verify-replays [options]"),
        };
    }

    private static int Smoke(Options options)
    {
        options.RequireOnly("seed", "ticks");
        var seed = options.GetUlong("seed", 1UL);
        var ticks = options.GetPositiveInt("ticks", DefaultTicks);
        var world = World.CreateSmoke(seed);
        var inputs = new PlayerInput[world.Players.Count];
        for (var tick = 0; tick < ticks; tick++)
        {
            for (var player = 0; player < inputs.Length; player++)
            {
                inputs[player] = BaseCombatProfile.At(tick, player);
            }
            Rounds.Sim.Sim.Step(world, inputs);
        }

        Console.WriteLine(
            FormattableString.Invariant($"seed={seed} ticks={ticks} players={world.Players.Count} hash={Rounds.Sim.Sim.Hash(world):x16}"));
        return 0;
    }

    private static int Record(Options options)
    {
        options.RequireOnly("profile", "id", "seed", "ticks", "output");
        if (options.GetRequired("profile") != "base-combat")
        {
            throw new ArgumentException("Only record profile `base-combat` is supported.");
        }

        var id = options.GetRequired("id");
        var seed = options.GetUlong("seed");
        var ticks = options.GetPositiveInt("ticks");
        var output = options.GetRequired("output");
        var recorder = new ReplayRecorder(id, seed, "arena-006", ticks);
        var inputs = new PlayerInput[ReplayFormat.PlayerCount];
        for (var tick = 0; tick < ticks; tick++)
        {
            for (var player = 0; player < inputs.Length; player++)
            {
                inputs[player] = BaseCombatProfile.At(tick, player);
            }
            recorder.Step(inputs);
        }

        var replay = recorder.Finish();
        var parent = Path.GetDirectoryName(Path.GetFullPath(output));
        if (parent is not null)
        {
            Directory.CreateDirectory(parent);
        }
        using (var stream = File.Create(output))
        {
            ReplayCodec.Write(stream, replay);
        }

        Console.WriteLine(
            FormattableString.Invariant($"recorded id={replay.ReplayId} ticks={replay.TotalTicks} hash={replay.FinalHash:x16} output={Path.GetFullPath(output)}"));
        return 0;
    }

    private static int Replay(Options options)
    {
        options.RequireOnly("input");
        var input = options.GetRequired("input");
        using var stream = File.OpenRead(input);
        var replay = ReplayCodec.Load(stream);
        var playback = new ReplayPlayback(replay);
        playback.RunToEnd();
        Console.WriteLine(
            FormattableString.Invariant(
                $"replayed id={replay.ReplayId} ticks={playback.ConsumedTicks} hash={Rounds.Sim.Sim.Hash(playback.World):x16} duels={playback.World.DuelNumber} results={playback.World.DuelResultCount}"));
        return 0;
    }

    private static int VerifyReplays(Options options)
    {
        options.RequireOnly("directory");
        var directory = options.GetRequired("directory");
        var results = ReplayCorpus.VerifyDirectory(directory);
        foreach (var result in results)
        {
            Console.WriteLine(
                FormattableString.Invariant($"verified id={result.ReplayId} ticks={result.TotalTicks} hash={result.FinalHash:x16}"));
        }
        Console.WriteLine($"verified replay corpus count={results.Count}");
        return 0;
    }
}

internal sealed class Options
{
    private readonly Dictionary<string, string> _values;

    private Options(Dictionary<string, string> values)
    {
        _values = values;
    }

    public static Options Parse(IEnumerable<string> arguments)
    {
        var values = arguments.ToArray();
        var parsed = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < values.Length; index += 2)
        {
            var option = values[index];
            if (!option.StartsWith("--", StringComparison.Ordinal) || option.Length == 2 || index + 1 >= values.Length)
            {
                throw new ArgumentException($"Malformed command option near `{option}`.");
            }
            var name = option[2..];
            if (!parsed.TryAdd(name, values[index + 1]))
            {
                throw new ArgumentException($"Option `--{name}` was supplied more than once.");
            }
        }
        return new Options(parsed);
    }

    public void RequireOnly(params string[] allowed)
    {
        var set = new HashSet<string>(allowed, StringComparer.Ordinal);
        var unknown = _values.Keys.FirstOrDefault(name => !set.Contains(name));
        if (unknown is not null)
        {
            throw new ArgumentException($"Unknown option `--{unknown}`.");
        }
    }

    public string GetRequired(string name) =>
        _values.TryGetValue(name, out var value) && value.Length > 0
            ? value
            : throw new ArgumentException($"Required option `--{name}` is missing.");

    public ulong GetUlong(string name) =>
        ulong.TryParse(GetRequired(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new ArgumentException($"Option `--{name}` must be an unsigned integer.");

    public ulong GetUlong(string name, ulong fallback) =>
        _values.ContainsKey(name) ? GetUlong(name) : fallback;

    public int GetPositiveInt(string name) =>
        int.TryParse(GetRequired(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : throw new ArgumentException($"Option `--{name}` must be a positive integer.");

    public int GetPositiveInt(string name, int fallback) =>
        _values.ContainsKey(name) ? GetPositiveInt(name) : fallback;
}

internal static class BaseCombatProfile
{
    public static PlayerInput At(int tick, int player)
    {
        var phase = (tick + (player * 19)) % 120;
        var move = phase < 40 ? (sbyte)1 : phase < 80 ? (sbyte)-1 : (sbyte)0;
        return new PlayerInput(
            move,
            JumpHeld: phase == 8,
            FireHeld: phase % 23 < 3,
            BlockHeld: phase is >= 60 and < 63,
            AimDirection: player == 0 ? new Vec2(1.0, 0.0) : new Vec2(-1.0, 0.0));
    }
}
