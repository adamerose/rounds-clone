using System.Text.Json;
using System.Text;

namespace Rounds.Game;

internal sealed class AgentPlaytestTraceRecorder
{
    private readonly ulong _seed;
    private readonly List<AgentPlaytestAcceptedInterval> _intervals = [];

    public AgentPlaytestTraceRecorder(ulong seed)
    {
        _seed = seed;
    }

    public void Record(AgentPlaytestAcceptedInterval interval)
    {
        ArgumentNullException.ThrowIfNull(interval);
        if (interval.Sequence != _intervals.Count + 1)
        {
            throw new AgentPlaytestFailure(interval.Sequence, "sequence", "invalid-sequence", "Trace intervals must be recorded once and in order.");
        }
        _intervals.Add(interval);
    }

    public byte[] ToCanonicalBytes(bool requireTerminal)
    {
        if (requireTerminal && (_intervals.Count == 0 || !_intervals[^1].Terminal))
        {
            throw new AgentPlaytestFailure(null, "terminal", "invalid-terminal", "A completed trace must end at the supported boundary.");
        }

        using var output = new MemoryStream();
        foreach (var interval in _intervals)
        {
            using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartObject();
                writer.WriteString("protocol", AgentPlaytestLimits.Protocol);
                writer.WriteString("traceLabel", NonHumanStructuralObservation.RequiredTraceLabel);
                writer.WriteString("seed", _seed.ToString(System.Globalization.CultureInfo.InvariantCulture));
                writer.WriteNumber("sequence", interval.Sequence);
                writer.WriteNumber("requestedIntervalTicks", interval.RequestedIntervalTicks);
                writer.WriteNumber("acceptedIntervalTicks", interval.AcceptedIntervalTicks);
                writer.WritePropertyName("players");
                writer.WriteStartArray();
                foreach (var player in interval.Players)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("move", player.Move);
                    writer.WriteBoolean("jump", player.Jump);
                    writer.WriteBoolean("fire", player.Fire);
                    writer.WriteBoolean("block", player.Block);
                    writer.WriteNumber("aimX", player.AimX);
                    writer.WriteNumber("aimY", player.AimY);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WritePropertyName("tickHashes");
                writer.WriteStartArray();
                foreach (var hash in interval.TickHashes)
                {
                    writer.WriteStringValue(hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture));
                }
                writer.WriteEndArray();
                writer.WriteBoolean("terminal", interval.Terminal);
                writer.WriteEndObject();
            }
            output.WriteByte((byte)'\n');
        }
        return output.ToArray();
    }
}

internal static class AgentPlaytestTraceCodec
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] IntervalProperties =
    [
        "protocol", "traceLabel", "seed", "sequence", "requestedIntervalTicks",
        "acceptedIntervalTicks", "players", "tickHashes", "terminal",
    ];
    private static readonly string[] PlayerProperties = ["move", "jump", "fire", "block", "aimX", "aimY"];

    public static IReadOnlyList<AgentPlaytestAcceptedInterval> ParseCanonical(
        ReadOnlySpan<byte> bytes,
        bool requireTerminal)
    {
        try
        {
            return ParseCanonicalCore(bytes, requireTerminal);
        }
        catch (AgentPlaytestFailure)
        {
            throw;
        }
        catch (Exception exception) when (exception is DecoderFallbackException or InvalidOperationException or
            FormatException or OverflowException or ArgumentException)
        {
            throw ReplayMismatch(exception.Message);
        }
    }

    private static IReadOnlyList<AgentPlaytestAcceptedInterval> ParseCanonicalCore(
        ReadOnlySpan<byte> bytes,
        bool requireTerminal)
    {
        if (bytes.Length == 0 || bytes[^1] != (byte)'\n')
        {
            throw ReplayMismatch("Trace must be nonempty LF-terminated canonical NDJSON.");
        }
        var text = StrictUtf8.GetString(bytes);
        var lines = text.Split('\n');
        var intervals = new List<AgentPlaytestAcceptedInterval>(lines.Length - 1);
        for (var index = 0; index < lines.Length - 1; index++)
        {
            if (lines[index].Length == 0)
            {
                throw ReplayMismatch("Trace cannot contain blank records.");
            }
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(lines[index]);
            }
            catch (JsonException exception)
            {
                throw ReplayMismatch(exception.Message);
            }
            using (document)
            {
                var root = document.RootElement;
                if (!HasExactlyInOrder(root, IntervalProperties) ||
                    root.GetProperty("protocol").GetString() != AgentPlaytestLimits.Protocol ||
                    root.GetProperty("traceLabel").GetString() != NonHumanStructuralObservation.RequiredTraceLabel ||
                    root.GetProperty("seed").GetString() != "1" ||
                    !root.GetProperty("sequence").TryGetInt32(out var sequence) || sequence != index + 1 ||
                    !root.GetProperty("requestedIntervalTicks").TryGetInt32(out var requested) || requested is < 1 or > 30 ||
                    !root.GetProperty("acceptedIntervalTicks").TryGetInt32(out var accepted) || accepted is < 1 || accepted > requested ||
                    root.GetProperty("players").ValueKind != JsonValueKind.Array ||
                    root.GetProperty("players").GetArrayLength() != 2 ||
                    root.GetProperty("tickHashes").ValueKind != JsonValueKind.Array ||
                    root.GetProperty("tickHashes").GetArrayLength() != accepted ||
                    root.GetProperty("terminal").ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    throw ReplayMismatch("Trace interval schema is invalid.", sequence: index + 1);
                }

                var players = root.GetProperty("players").EnumerateArray().Select(ParsePlayer).ToArray();
                var hashes = root.GetProperty("tickHashes").EnumerateArray().Select(ParseHash).ToArray();
                var terminal = root.GetProperty("terminal").GetBoolean();
                if (terminal && index != lines.Length - 2)
                {
                    throw ReplayMismatch("Only the final interval may be terminal.", sequence);
                }
                intervals.Add(new AgentPlaytestAcceptedInterval(
                    sequence,
                    requested,
                    accepted,
                    Array.AsReadOnly(players),
                    Array.AsReadOnly(hashes),
                    terminal));
            }
        }
        if (requireTerminal && (intervals.Count == 0 || !intervals[^1].Terminal))
        {
            throw ReplayMismatch("Trace does not end at the supported terminal boundary.");
        }

        var recorder = new AgentPlaytestTraceRecorder(1UL);
        foreach (var interval in intervals)
        {
            recorder.Record(interval);
        }
        if (!recorder.ToCanonicalBytes(requireTerminal).AsSpan().SequenceEqual(bytes))
        {
            throw ReplayMismatch("Trace bytes are not canonical.");
        }
        return intervals.AsReadOnly();
    }

    private static AgentPlaytestPlayerAction ParsePlayer(JsonElement element)
    {
        if (!HasExactlyInOrder(element, PlayerProperties) ||
            !element.GetProperty("move").TryGetSByte(out var move) ||
            element.GetProperty("jump").ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            element.GetProperty("fire").ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            element.GetProperty("block").ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
            !element.GetProperty("aimX").TryGetDouble(out var aimX) ||
            !element.GetProperty("aimY").TryGetDouble(out var aimY))
        {
            throw ReplayMismatch("Trace player schema is invalid.");
        }
        var action = new AgentPlaytestPlayerAction(
            move,
            element.GetProperty("jump").GetBoolean(),
            element.GetProperty("fire").GetBoolean(),
            element.GetProperty("block").GetBoolean(),
            aimX,
            aimY);
        return action.IsLegal ? action : throw ReplayMismatch("Trace player action is invalid.");
    }

    private static ulong ParseHash(JsonElement element)
    {
        var text = element.ValueKind == JsonValueKind.String ? element.GetString() : null;
        if (text is null || text.Length != 16 || text.Any(static character =>
            character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')) ||
            !ulong.TryParse(text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var hash))
        {
            throw ReplayMismatch("Trace hash must be 16 lowercase hexadecimal digits.");
        }
        return hash;
    }

    private static bool HasExactlyInOrder(JsonElement element, IReadOnlyList<string> expected)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        var index = 0;
        foreach (var property in element.EnumerateObject())
        {
            if (index >= expected.Count || property.Name != expected[index++])
            {
                return false;
            }
        }
        return index == expected.Count;
    }

    private static AgentPlaytestFailure ReplayMismatch(string message, int? sequence = null) =>
        new(sequence, "replay", "replay-mismatch", message);
}
