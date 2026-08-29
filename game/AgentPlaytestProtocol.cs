using System.Text;
using System.Text.Json;
using Rounds.Sim;
using Rounds.Sim.Math;

namespace Rounds.Game;

internal static class AgentPlaytestLimits
{
    public const string Protocol = "rounds-agent-playtest-v1";
    public const int PlayerCount = 2;
    public const int MinimumIntervalTicks = 1;
    public const int MaximumIntervalTicks = 30;
    public const int MaximumRequests = 120;
    public const int MaximumSimulationTicks = 3_600;
    public const int MaximumFrames = 121;
    public const int MaximumWidth = 1_280;
    public const int MaximumHeight = 720;
    public const int MaximumFramesPerSecond = 10;
    public const int RouteTimeoutSeconds = 90;
    public const int OwnerTimeoutSeconds = 95;
    public const long MaximumOutputBytes = 256L * 1024L * 1024L;
    public const long MaximumPrivateMemoryBytes = 2L * 1024L * 1024L * 1024L;
    public const long MaximumDedicatedGpuMemoryBytes = 2L * 1024L * 1024L * 1024L;
    public const double MaximumGpuUtilization = 0.70;
    public const int MaximumLogicalProcessors = 2;
    public const int MaximumProcessCount = 2;
    public const int MaximumHeartbeatP95Milliseconds = 15;
    public const int MaximumHeartbeatIncreaseMilliseconds = 5;
    public const int MaximumHeartbeatDelayMilliseconds = 50;
    public const int LiveBulletCap = 2_048;
}

internal readonly record struct AgentPlaytestPlayerAction(
    sbyte Move,
    bool Jump,
    bool Fire,
    bool Block,
    double AimX,
    double AimY)
{
    public bool IsLegal =>
        Move is >= -1 and <= 1 &&
        double.IsFinite(AimX) && double.IsFinite(AimY) &&
        AimX is >= -1.0 and <= 1.0 && AimY is >= -1.0 and <= 1.0;

    public PlayerInput ToPlayerInput() =>
        new(Move, Jump, Fire, Block, new Vec2(AimX, AimY));
}

internal sealed record AgentPlaytestRequest(
    string Protocol,
    int Sequence,
    int IntervalTicks,
    IReadOnlyList<AgentPlaytestPlayerAction> Players);

internal abstract record AgentPlaytestResponse(string Protocol, int? Sequence, string Status);

internal sealed record AgentPlaytestFrameResponse(
    int FrameSequence,
    string FramePath,
    string FrameSha256,
    int Width,
    int Height,
    bool Terminal)
    : AgentPlaytestResponse(AgentPlaytestLimits.Protocol, FrameSequence, "frame");

internal sealed record AgentPlaytestErrorResponse(
    int? ErrorSequence,
    string Stage,
    string Code)
    : AgentPlaytestResponse(AgentPlaytestLimits.Protocol, ErrorSequence, "error");

internal static class AgentPlaytestErrors
{
    public static readonly IReadOnlySet<(string Stage, string Code)> Allowed =
        new HashSet<(string, string)>
        {
            ("request-parse", "malformed-json"),
            ("request-validate", "invalid-schema"),
            ("sequence", "invalid-sequence"),
            ("simulation", "simulation-failed"),
            ("renderer", "renderer-unavailable"),
            ("frame", "frame-publish-failed"),
            ("resource", "resource-limit-exceeded"),
            ("lifecycle", "timeout"),
            ("lifecycle", "cleanup-failed"),
            ("terminal", "invalid-terminal"),
            ("replay", "replay-mismatch"),
        };

    public static AgentPlaytestErrorResponse Create(int? sequence, string stage, string code)
    {
        if (!Allowed.Contains((stage, code)))
        {
            throw new ArgumentException("The agent-playtest error pair is not part of protocol v1.");
        }
        return new AgentPlaytestErrorResponse(sequence, stage, code);
    }
}

internal readonly record struct AgentPlaytestParseResult(
    AgentPlaytestRequest? Request,
    AgentPlaytestErrorResponse? Error)
{
    public bool IsSuccess => Request is not null;
}

internal static class AgentPlaytestNdjson
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly string[] RequestProperties = ["protocol", "sequence", "intervalTicks", "players"];
    private static readonly string[] PlayerProperties = ["move", "jump", "fire", "block", "aimX", "aimY"];

    public static AgentPlaytestParseResult ParseRequest(ReadOnlySpan<byte> line)
    {
        if (line.Length == 0 || line[^1] != (byte)'\n' || line[..^1].IndexOf((byte)'\n') >= 0)
        {
            return Malformed();
        }

        JsonDocument document;
        try
        {
            var json = StrictUtf8.GetString(line[..^1]);
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8,
            });
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException)
        {
            return Malformed();
        }

        using (document)
        {
            var root = document.RootElement;
            int? parsedSequence = TryReadSequence(root);
            if (root.ValueKind != JsonValueKind.Object ||
                !HasExactly(root, RequestProperties) ||
                !TryReadExactString(root, "protocol", out var protocol) ||
                protocol != AgentPlaytestLimits.Protocol ||
                !TryReadInt32(root, "sequence", out var sequence) ||
                sequence < 0 ||
                !TryReadInt32(root, "intervalTicks", out var intervalTicks) ||
                intervalTicks is < AgentPlaytestLimits.MinimumIntervalTicks or > AgentPlaytestLimits.MaximumIntervalTicks ||
                !root.TryGetProperty("players", out var playersElement) ||
                playersElement.ValueKind != JsonValueKind.Array ||
                playersElement.GetArrayLength() != AgentPlaytestLimits.PlayerCount)
            {
                return Invalid(parsedSequence);
            }

            var players = new AgentPlaytestPlayerAction[AgentPlaytestLimits.PlayerCount];
            var index = 0;
            foreach (var player in playersElement.EnumerateArray())
            {
                if (!TryReadPlayer(player, out players[index++]))
                {
                    return Invalid(parsedSequence);
                }
            }

            return new AgentPlaytestParseResult(
                new AgentPlaytestRequest(protocol, sequence, intervalTicks, Array.AsReadOnly(players)),
                null);
        }
    }

    public static byte[] SerializeResponse(AgentPlaytestResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("protocol", response.Protocol);
            if (response.Sequence is int sequence)
            {
                writer.WriteNumber("sequence", sequence);
            }
            else
            {
                writer.WriteNull("sequence");
            }
            writer.WriteString("status", response.Status);
            switch (response)
            {
                case AgentPlaytestFrameResponse frame:
                    writer.WriteString("framePath", frame.FramePath);
                    writer.WriteString("frameSha256", frame.FrameSha256);
                    writer.WriteNumber("width", frame.Width);
                    writer.WriteNumber("height", frame.Height);
                    writer.WriteBoolean("terminal", frame.Terminal);
                    break;
                case AgentPlaytestErrorResponse error:
                    writer.WriteString("stage", error.Stage);
                    writer.WriteString("code", error.Code);
                    break;
                default:
                    throw new ArgumentException("Unknown agent-playtest response type.", nameof(response));
            }
            writer.WriteEndObject();
        }
        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }

    public static byte[] SerializeRequest(AgentPlaytestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("protocol", request.Protocol);
            writer.WriteNumber("sequence", request.Sequence);
            writer.WriteNumber("intervalTicks", request.IntervalTicks);
            writer.WritePropertyName("players");
            writer.WriteStartArray();
            foreach (var player in request.Players)
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
            writer.WriteEndObject();
        }
        stream.WriteByte((byte)'\n');
        return stream.ToArray();
    }

    private static bool TryReadPlayer(JsonElement player, out AgentPlaytestPlayerAction action)
    {
        action = default;
        if (player.ValueKind != JsonValueKind.Object ||
            !HasExactly(player, PlayerProperties) ||
            !TryReadInt32(player, "move", out var move) || move is < -1 or > 1 ||
            !TryReadBoolean(player, "jump", out var jump) ||
            !TryReadBoolean(player, "fire", out var fire) ||
            !TryReadBoolean(player, "block", out var block) ||
            !TryReadDouble(player, "aimX", out var aimX) || aimX is < -1.0 or > 1.0 ||
            !TryReadDouble(player, "aimY", out var aimY) || aimY is < -1.0 or > 1.0)
        {
            return false;
        }
        action = new AgentPlaytestPlayerAction((sbyte)move, jump, fire, block, aimX, aimY);
        return action.IsLegal;
    }

    private static bool HasExactly(JsonElement element, IReadOnlyCollection<string> expected)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!expected.Contains(property.Name, StringComparer.Ordinal) || !seen.Add(property.Name))
            {
                return false;
            }
        }
        return seen.Count == expected.Count;
    }

    private static int? TryReadSequence(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object && TryReadInt32(root, "sequence", out var sequence)
            ? sequence
            : null;

    private static bool TryReadExactString(JsonElement element, string name, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        value = property.GetString()!;
        return true;
    }

    private static bool TryReadInt32(JsonElement element, string name, out int value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value);
    }

    private static bool TryReadBoolean(JsonElement element, string name, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(name, out var property) ||
            property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return false;
        }
        value = property.GetBoolean();
        return true;
    }

    private static bool TryReadDouble(JsonElement element, string name, out double value)
    {
        value = 0.0;
        return element.TryGetProperty(name, out var property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetDouble(out value) &&
            double.IsFinite(value);
    }

    private static AgentPlaytestParseResult Malformed() =>
        new(null, AgentPlaytestErrors.Create(null, "request-parse", "malformed-json"));

    private static AgentPlaytestParseResult Invalid(int? sequence) =>
        new(null, AgentPlaytestErrors.Create(sequence, "request-validate", "invalid-schema"));
}
