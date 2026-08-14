using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Rounds.Sim.Maps;

namespace Rounds.Replay;

public static class ReplayCodec
{
    private static readonly string[] RootProperties =
    [
        "format", "replayId", "targetBuild", "ruleset", "seed", "arenaId", "tickRate",
        "playerCount", "totalTicks", "runs", "checkpoints", "finalHash",
    ];
    private static readonly string[] RunProperties = ["length", "players"];
    private static readonly string[] PlayerProperties = ["move", "aimXBits", "aimYBits", "jump", "fire", "block"];
    private static readonly string[] CheckpointProperties = ["tick", "hash"];

    public static ReplayDocument Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        var source = bytes.ToArray();
        ReplayDocument replay;
        try
        {
            using var json = JsonDocument.Parse(
                source,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 16,
                });
            replay = Parse(json.RootElement);
        }
        catch (Exception exception) when (exception is JsonException or FormatException or OverflowException or InvalidOperationException)
        {
            throw new InvalidDataException("Replay is not valid canonical version 1 JSON.", exception);
        }

        ReplayValidator.Validate(replay);
        var canonical = ToCanonicalBytes(replay);
        if (!source.AsSpan().SequenceEqual(canonical))
        {
            throw new InvalidDataException("Replay bytes are not canonical version 1 JSON.");
        }

        return replay;
    }

    public static void Write(Stream stream, ReplayDocument replay)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var bytes = ToCanonicalBytes(replay);
        stream.Write(bytes);
    }

    public static byte[] ToCanonicalBytes(ReplayDocument replay)
    {
        ArgumentNullException.ThrowIfNull(replay);
        ReplayValidator.Validate(replay);
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("format", ReplayFormat.Version);
            writer.WriteString("replayId", replay.ReplayId);
            writer.WriteNumber("targetBuild", ReplayFormat.TargetBuild);
            writer.WriteString("ruleset", ReplayFormat.Ruleset);
            writer.WriteString("seed", replay.Seed.ToString(CultureInfo.InvariantCulture));
            writer.WriteString("arenaId", replay.ArenaId);
            writer.WriteNumber("tickRate", ReplayFormat.TickRate);
            writer.WriteNumber("playerCount", ReplayFormat.PlayerCount);
            writer.WriteNumber("totalTicks", replay.TotalTicks);
            writer.WriteStartArray("runs");
            foreach (var run in replay.Runs)
            {
                writer.WriteStartObject();
                writer.WriteNumber("length", run.Length);
                writer.WriteStartArray("players");
                WritePlayer(writer, run.Frame.Player0);
                WritePlayer(writer, run.Frame.Player1);
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("checkpoints");
            foreach (var checkpoint in replay.Checkpoints)
            {
                writer.WriteStartObject();
                writer.WriteNumber("tick", checkpoint.Tick);
                writer.WriteString("hash", checkpoint.Hash.ToString("x16", CultureInfo.InvariantCulture));
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteString("finalHash", replay.FinalHash.ToString("x16", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }

        var canonical = new byte[buffer.WrittenCount + 1];
        buffer.WrittenSpan.CopyTo(canonical);
        canonical[^1] = (byte)'\n';
        return canonical;
    }

    private static ReplayDocument Parse(JsonElement root)
    {
        RequireObject(root, RootProperties);
        RequireInteger(root.GetProperty("format"), ReplayFormat.Version, "format");
        var replayId = RequireString(root.GetProperty("replayId"), "replayId");
        RequireInteger(root.GetProperty("targetBuild"), ReplayFormat.TargetBuild, "targetBuild");
        if (RequireString(root.GetProperty("ruleset"), "ruleset") != ReplayFormat.Ruleset)
        {
            throw new InvalidDataException("Replay ruleset is unsupported.");
        }

        var seedText = RequireString(root.GetProperty("seed"), "seed");
        if (!ulong.TryParse(seedText, NumberStyles.None, CultureInfo.InvariantCulture, out var seed) ||
            (seedText.Length > 1 && seedText[0] == '0'))
        {
            throw new InvalidDataException("Replay seed is not a canonical unsigned decimal string.");
        }

        var arenaId = RequireString(root.GetProperty("arenaId"), "arenaId");
        RequireInteger(root.GetProperty("tickRate"), ReplayFormat.TickRate, "tickRate");
        RequireInteger(root.GetProperty("playerCount"), ReplayFormat.PlayerCount, "playerCount");
        var totalTicks = RequireInt32(root.GetProperty("totalTicks"), "totalTicks");

        var runsElement = root.GetProperty("runs");
        RequireKind(runsElement, JsonValueKind.Array, "runs");
        var runs = new List<ReplayRun>();
        foreach (var runElement in runsElement.EnumerateArray())
        {
            RequireObject(runElement, RunProperties);
            var length = RequireInt32(runElement.GetProperty("length"), "run length");
            var playersElement = runElement.GetProperty("players");
            RequireKind(playersElement, JsonValueKind.Array, "players");
            var players = playersElement.EnumerateArray().Select(ParsePlayer).ToArray();
            if (players.Length != ReplayFormat.PlayerCount)
            {
                throw new InvalidDataException("Every replay run requires exactly two players.");
            }

            runs.Add(new ReplayRun(length, new RecordedFrame(players[0], players[1])));
        }

        var checkpointsElement = root.GetProperty("checkpoints");
        RequireKind(checkpointsElement, JsonValueKind.Array, "checkpoints");
        var checkpoints = new List<ReplayCheckpoint>();
        foreach (var checkpointElement in checkpointsElement.EnumerateArray())
        {
            RequireObject(checkpointElement, CheckpointProperties);
            checkpoints.Add(new ReplayCheckpoint(
                RequireInt32(checkpointElement.GetProperty("tick"), "checkpoint tick"),
                ParseHex(RequireString(checkpointElement.GetProperty("hash"), "checkpoint hash"))));
        }

        return new ReplayDocument(
            replayId,
            seed,
            arenaId,
            totalTicks,
            runs,
            checkpoints,
            ParseHex(RequireString(root.GetProperty("finalHash"), "finalHash")));
    }

    private static RecordedPlayerInput ParsePlayer(JsonElement element)
    {
        RequireObject(element, PlayerProperties);
        var move = RequireInt32(element.GetProperty("move"), "move");
        if (move is < -1 or > 1)
        {
            throw new InvalidDataException("Replay movement must be -1, 0, or 1.");
        }

        return new RecordedPlayerInput(
            (sbyte)move,
            ParseHex(RequireString(element.GetProperty("aimXBits"), "aimXBits")),
            ParseHex(RequireString(element.GetProperty("aimYBits"), "aimYBits")),
            RequireBoolean(element.GetProperty("jump"), "jump"),
            RequireBoolean(element.GetProperty("fire"), "fire"),
            RequireBoolean(element.GetProperty("block"), "block"));
    }

    private static void WritePlayer(Utf8JsonWriter writer, RecordedPlayerInput input)
    {
        writer.WriteStartObject();
        writer.WriteNumber("move", input.MoveAxis);
        writer.WriteString("aimXBits", input.AimXBits.ToString("x16", CultureInfo.InvariantCulture));
        writer.WriteString("aimYBits", input.AimYBits.ToString("x16", CultureInfo.InvariantCulture));
        writer.WriteBoolean("jump", input.JumpHeld);
        writer.WriteBoolean("fire", input.FireHeld);
        writer.WriteBoolean("block", input.BlockHeld);
        writer.WriteEndObject();
    }

    private static void RequireObject(JsonElement element, IReadOnlyList<string> expected)
    {
        RequireKind(element, JsonValueKind.Object, "object");
        var actual = element.EnumerateObject().Select(property => property.Name).ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidDataException("Replay properties are missing, duplicated, unknown, or out of order.");
        }
    }

    private static void RequireKind(JsonElement element, JsonValueKind kind, string name)
    {
        if (element.ValueKind != kind)
        {
            throw new InvalidDataException($"Replay {name} has the wrong JSON type.");
        }
    }

    private static int RequireInt32(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out var value))
        {
            throw new InvalidDataException($"Replay {name} must be a 32-bit integer.");
        }

        return value;
    }

    private static void RequireInteger(JsonElement element, int expected, string name)
    {
        if (RequireInt32(element, name) != expected)
        {
            throw new InvalidDataException($"Replay {name} is unsupported.");
        }
    }

    private static string RequireString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"Replay {name} must be a string.");
        }

        return element.GetString()!;
    }

    private static bool RequireBoolean(JsonElement element, string name)
    {
        if (element.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException($"Replay {name} must be a boolean.");
        }

        return element.GetBoolean();
    }

    private static ulong ParseHex(string value)
    {
        if (value.Length != 16 || value.Any(character => !IsLowerHex(character)) ||
            !ulong.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var result))
        {
            throw new InvalidDataException("Replay bit and hash strings must be 16 lowercase hexadecimal digits.");
        }

        return result;
    }

    private static bool IsLowerHex(char character) =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f';
}

internal static class ReplayValidator
{
    public static void Validate(ReplayDocument replay)
    {
        if (!IsIdentifier(replay.ReplayId))
        {
            throw new InvalidDataException("Replay ID must be 1-64 lowercase ASCII hyphen-separated segments.");
        }

        if (!IsIdentifier(replay.ArenaId))
        {
            throw new InvalidDataException("Replay arena ID is malformed.");
        }

        try
        {
            _ = ArenaCatalog.LoadEmbedded().GetRequired(replay.ArenaId);
        }
        catch (KeyNotFoundException exception)
        {
            throw new InvalidDataException($"Replay arena `{replay.ArenaId}` is unsupported.", exception);
        }

        if (replay.TotalTicks is < 1 or > ReplayFormat.MaximumTicks)
        {
            throw new InvalidDataException($"Replay tick count must be between 1 and {ReplayFormat.MaximumTicks}.");
        }

        if (replay.Runs.Count == 0)
        {
            throw new InvalidDataException("Replay requires at least one input run.");
        }

        long total = 0;
        ReplayRun? previous = null;
        foreach (var run in replay.Runs)
        {
            if (run.Length <= 0)
            {
                throw new InvalidDataException("Replay run lengths must be positive.");
            }

            ValidatePlayer(run.Frame.Player0);
            ValidatePlayer(run.Frame.Player1);
            if (previous is not null && previous.Frame == run.Frame)
            {
                throw new InvalidDataException("Adjacent identical replay runs must be coalesced.");
            }

            total += run.Length;
            if (total > ReplayFormat.MaximumTicks)
            {
                throw new InvalidDataException("Replay run total exceeds the maximum before playback.");
            }

            previous = run;
        }

        if (total != replay.TotalTicks)
        {
            throw new InvalidDataException("Replay run lengths do not equal totalTicks.");
        }

        var expectedTicks = new List<int>();
        for (var tick = ReplayFormat.TickRate; tick < replay.TotalTicks; tick += ReplayFormat.TickRate)
        {
            expectedTicks.Add(tick);
        }
        expectedTicks.Add(replay.TotalTicks);
        if (replay.Checkpoints.Count != expectedTicks.Count)
        {
            throw new InvalidDataException("Replay checkpoints are incomplete.");
        }

        for (var index = 0; index < expectedTicks.Count; index++)
        {
            if (replay.Checkpoints[index].Tick != expectedTicks[index])
            {
                throw new InvalidDataException("Replay checkpoint ticks are not canonical.");
            }
        }

        if (replay.Checkpoints[^1].Hash != replay.FinalHash)
        {
            throw new InvalidDataException("Replay final checkpoint does not equal finalHash.");
        }
    }

    private static void ValidatePlayer(RecordedPlayerInput input)
    {
        if (input.MoveAxis is < -1 or > 1)
        {
            throw new InvalidDataException("Replay movement must be -1, 0, or 1.");
        }

        if (!double.IsFinite(BitConverter.UInt64BitsToDouble(input.AimXBits)) ||
            !double.IsFinite(BitConverter.UInt64BitsToDouble(input.AimYBits)))
        {
            throw new InvalidDataException("Replay aim must decode to finite doubles.");
        }
    }

    private static bool IsIdentifier(string value)
    {
        if (value.Length is < 1 or > 64 || value[0] == '-' || value[^1] == '-')
        {
            return false;
        }

        var previousHyphen = false;
        foreach (var character in value)
        {
            var hyphen = character == '-';
            if (!(character is >= 'a' and <= 'z' or >= '0' and <= '9') && !hyphen)
            {
                return false;
            }
            if (hyphen && previousHyphen)
            {
                return false;
            }
            previousHyphen = hyphen;
        }

        return true;
    }
}
