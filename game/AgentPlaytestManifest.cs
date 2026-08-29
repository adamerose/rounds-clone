using System.Security.Cryptography;
using System.Text.Json;

namespace Rounds.Game;

internal sealed record AgentPlaytestManifestContext(
    string Status,
    string BuildIdentity,
    IReadOnlyList<AgentPlaytestResourceSample> ResourceSamples,
    IReadOnlyList<AgentPlaytestCausalityReceipt> CausalityReceipts,
    bool CleanupEvidenceAvailable,
    bool TelemetryEvidenceAvailable,
    bool MonitorAttestationAvailable)
{
    public static AgentPlaytestManifestContext TestOnlySynthetic { get; } = new(
        "test-only-non-evidence",
        "synthetic-unit-test-build",
        Array.Empty<AgentPlaytestResourceSample>(),
        Array.Empty<AgentPlaytestCausalityReceipt>(),
        false,
        false,
        false);

    public static AgentPlaytestManifestContext ProductionRendererUnavailable { get; } = new(
        "renderer-unavailable",
        "debug-build-without-renderer-evidence",
        Array.Empty<AgentPlaytestResourceSample>(),
        Array.Empty<AgentPlaytestCausalityReceipt>(),
        false,
        false,
        false);
}

internal sealed record AgentPlaytestManifest(
    AgentPlaytestManifestContext Context,
    bool Complete,
    int Width,
    int Height,
    IReadOnlyList<AgentPlaytestFrameResponse> Frames,
    IReadOnlyList<AgentPlaytestAcceptedInterval> Intervals,
    string TraceSha256)
{
    public static AgentPlaytestManifest Create(
        AgentPlaytestManifestContext context,
        IReadOnlyList<AgentPlaytestFrameResponse> frames,
        IReadOnlyList<AgentPlaytestAcceptedInterval> intervals,
        ReadOnlySpan<byte> traceBytes)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentNullException.ThrowIfNull(intervals);
        if (frames.Count != intervals.Count + 1 || frames.Count == 0 ||
            frames.Select(static frame => frame.FrameSequence).Where((sequence, index) => sequence != index).Any() ||
            frames.Any(frame => frame.Width != frames[0].Width || frame.Height != frames[0].Height) ||
            intervals.Count == 0 || !intervals[^1].Terminal || !frames[^1].Terminal)
        {
            throw new AgentPlaytestFailure(null, "replay", "replay-mismatch", "Manifest frames and trace intervals do not describe one ordered terminal run.");
        }
        var receiptsMatch = AgentPlaytestCausality.ReceiptsMatch(frames, intervals, context.CausalityReceipts);
        if ((context.Status == "test-only-non-evidence" && context.CausalityReceipts.Count != 0) ||
            (context.CausalityReceipts.Count != 0 && !receiptsMatch))
        {
            throw new AgentPlaytestFailure(null, "replay", "replay-mismatch", "Manifest causality receipts are unavailable or do not match the ordered actions.");
        }
        var complete = context.Status == "renderer-evidence" &&
            context.CleanupEvidenceAvailable && context.TelemetryEvidenceAvailable && context.MonitorAttestationAvailable &&
            frames.Count >= 5 && frames.Select(static frame => frame.FrameSha256).Distinct(StringComparer.Ordinal).Count() >= 5 &&
            receiptsMatch &&
            AgentPlaytestResourceGate.AcceptsRendererEvidence(AgentPlaytestOwnerConfiguration.Required, context.ResourceSamples);
        if (context.Status == "test-only-non-evidence" && complete)
        {
            throw new AgentPlaytestFailure(null, "resource", "resource-limit-exceeded", "Synthetic tests cannot publish evidence completion.");
        }
        var frozenContext = context with
        {
            ResourceSamples = Array.AsReadOnly(context.ResourceSamples.ToArray()),
            CausalityReceipts = Array.AsReadOnly(context.CausalityReceipts.ToArray()),
        };
        return new AgentPlaytestManifest(
            frozenContext,
            complete,
            frames[0].Width,
            frames[0].Height,
            Array.AsReadOnly(frames.ToArray()),
            Array.AsReadOnly(intervals.ToArray()),
            Convert.ToHexString(SHA256.HashData(traceBytes)).ToLowerInvariant());
    }
}

internal static class AgentPlaytestManifestCodec
{
    private static readonly string[] RootProperties =
    [
        "protocol", "status", "complete", "buildIdentity", "width", "height", "frameHashes",
        "acceptedActions", "causalityReceipts", "tickHashCoverage", "limits", "resourceSamples", "terminalBoundary",
        "cleanupEvidenceAvailable", "telemetryEvidenceAvailable", "monitorAttestationAvailable", "traceSha256",
    ];
    private static readonly string[] ActionProperties =
        ["sequence", "requestedIntervalTicks", "acceptedIntervalTicks", "players"];
    private static readonly string[] CoverageProperties = ["sequence", "hashes"];
    private static readonly string[] ReceiptProperties =
        ["priorFrameSequence", "priorFrameSha256", "requestSequence", "actionIdentity"];
    private static readonly string[] PlayerProperties = ["move", "jump", "fire", "block", "aimX", "aimY"];
    private static readonly string[] LimitProperties =
    [
        "maximumRequests", "maximumSimulationTicks", "maximumFrames", "maximumOutputBytes",
        "routeTimeoutSeconds", "ownerTimeoutSeconds", "liveBulletCap",
    ];
    private static readonly string[] ResourceSampleProperties =
    [
        "ProcessCount", "PrivateMemoryBytes", "DedicatedGpuMemoryBytes", "TotalGpuUtilization",
        "FramesInPreviousSecond", "Width", "Height", "HeartbeatBaselineP95Milliseconds",
        "HeartbeatP95Milliseconds", "MaximumHeartbeatDelayMilliseconds", "WindowScreen", "WindowNonActivating",
    ];

    public static byte[] ToCanonicalBytes(AgentPlaytestManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        using var output = new MemoryStream();
        using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = false }))
        {
            writer.WriteStartObject();
            writer.WriteString("protocol", AgentPlaytestLimits.Protocol);
            writer.WriteString("status", manifest.Context.Status);
            writer.WriteBoolean("complete", manifest.Complete);
            writer.WriteString("buildIdentity", manifest.Context.BuildIdentity);
            writer.WriteNumber("width", manifest.Width);
            writer.WriteNumber("height", manifest.Height);
            writer.WritePropertyName("frameHashes");
            writer.WriteStartArray();
            foreach (var frame in manifest.Frames)
            {
                writer.WriteStringValue(frame.FrameSha256);
            }
            writer.WriteEndArray();
            writer.WritePropertyName("acceptedActions");
            writer.WriteStartArray();
            foreach (var interval in manifest.Intervals)
            {
                writer.WriteStartObject();
                writer.WriteNumber("sequence", interval.Sequence);
                writer.WriteNumber("requestedIntervalTicks", interval.RequestedIntervalTicks);
                writer.WriteNumber("acceptedIntervalTicks", interval.AcceptedIntervalTicks);
                WritePlayers(writer, interval.Players);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WritePropertyName("causalityReceipts");
            writer.WriteStartArray();
            foreach (var receipt in manifest.Context.CausalityReceipts)
            {
                writer.WriteStartObject();
                writer.WriteNumber("priorFrameSequence", receipt.PriorFrameSequence);
                writer.WriteString("priorFrameSha256", receipt.PriorFrameSha256);
                writer.WriteNumber("requestSequence", receipt.RequestSequence);
                writer.WriteString("actionIdentity", receipt.ActionIdentity);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WritePropertyName("tickHashCoverage");
            writer.WriteStartArray();
            foreach (var interval in manifest.Intervals)
            {
                writer.WriteStartObject();
                writer.WriteNumber("sequence", interval.Sequence);
                writer.WritePropertyName("hashes");
                writer.WriteStartArray();
                foreach (var hash in interval.TickHashes)
                {
                    writer.WriteStringValue(hash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture));
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            WriteLimits(writer);
            writer.WritePropertyName("resourceSamples");
            writer.WriteStartArray();
            foreach (var sample in manifest.Context.ResourceSamples)
            {
                JsonSerializer.Serialize(writer, sample);
            }
            writer.WriteEndArray();
            writer.WriteString("terminalBoundary", "first-loser-draft");
            writer.WriteBoolean("cleanupEvidenceAvailable", manifest.Context.CleanupEvidenceAvailable);
            writer.WriteBoolean("telemetryEvidenceAvailable", manifest.Context.TelemetryEvidenceAvailable);
            writer.WriteBoolean("monitorAttestationAvailable", manifest.Context.MonitorAttestationAvailable);
            writer.WriteString("traceSha256", manifest.TraceSha256);
            writer.WriteEndObject();
        }
        output.WriteByte((byte)'\n');
        return output.ToArray();
    }

    public static void ValidateCanonical(ReadOnlySpan<byte> bytes)
    {
        try
        {
            ValidateCanonicalCore(bytes);
        }
        catch (AgentPlaytestFailure)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or
            OverflowException or ArgumentException or KeyNotFoundException)
        {
            throw Mismatch(exception.Message);
        }
    }

    private static void ValidateCanonicalCore(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0 || bytes[^1] != (byte)'\n')
        {
            throw Mismatch("Manifest must be LF-terminated.");
        }
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(bytes[..^1].ToArray());
        }
        catch (JsonException exception)
        {
            throw Mismatch(exception.Message);
        }
        using (document)
        {
            var root = document.RootElement;
            if (!HasExactlyInOrder(root, RootProperties) ||
                root.GetProperty("protocol").GetString() != AgentPlaytestLimits.Protocol ||
                string.IsNullOrWhiteSpace(root.GetProperty("buildIdentity").GetString()) ||
                root.GetProperty("width").GetInt32() is < 1 or > AgentPlaytestLimits.MaximumWidth ||
                root.GetProperty("height").GetInt32() is < 1 or > AgentPlaytestLimits.MaximumHeight ||
                root.GetProperty("terminalBoundary").GetString() != "first-loser-draft" ||
                !IsLowerHash(root.GetProperty("traceSha256").GetString()))
            {
                throw Mismatch("Manifest identity, dimensions, terminal boundary, or trace hash is invalid.");
            }
            var status = root.GetProperty("status").GetString();
            var complete = root.GetProperty("complete").GetBoolean();
            var cleanup = root.GetProperty("cleanupEvidenceAvailable").GetBoolean();
            var telemetry = root.GetProperty("telemetryEvidenceAvailable").GetBoolean();
            var monitor = root.GetProperty("monitorAttestationAvailable").GetBoolean();
            var resourceElements = root.GetProperty("resourceSamples").EnumerateArray().ToArray();
            if (status is not ("test-only-non-evidence" or "renderer-evidence") ||
                (status == "test-only-non-evidence" &&
                    (complete || cleanup || telemetry || monitor || resourceElements.Length != 0)))
            {
                throw Mismatch("Synthetic protocol output cannot claim renderer evidence.");
            }
            var frameHashes = root.GetProperty("frameHashes").EnumerateArray().Select(static item => item.GetString()).ToArray();
            if (frameHashes.Length == 0 || frameHashes.Any(static hash => !IsLowerHash(hash)))
            {
                throw Mismatch("Manifest frame hashes are invalid.");
            }
            var actions = root.GetProperty("acceptedActions").EnumerateArray().ToArray();
            var receipts = root.GetProperty("causalityReceipts").EnumerateArray().ToArray();
            var coverage = root.GetProperty("tickHashCoverage").EnumerateArray().ToArray();
            if (status == "test-only-non-evidence" && receipts.Length != 0)
            {
                throw Mismatch("Synthetic protocol output cannot publish causal evidence receipts.");
            }
            if (frameHashes.Length != actions.Length + 1 || actions.Length != coverage.Length || actions.Length == 0)
            {
                throw Mismatch("Manifest frame, action, and tick coverage counts disagree.");
            }
            for (var index = 0; index < actions.Length; index++)
            {
                var sequence = index + 1;
                var requested = actions[index].GetProperty("requestedIntervalTicks").GetInt32();
                var accepted = actions[index].GetProperty("acceptedIntervalTicks").GetInt32();
                var hashes = coverage[index].GetProperty("hashes").EnumerateArray().ToArray();
                if (!HasExactlyInOrder(actions[index], ActionProperties) ||
                    !HasExactlyInOrder(coverage[index], CoverageProperties) ||
                    actions[index].GetProperty("sequence").GetInt32() != sequence ||
                    coverage[index].GetProperty("sequence").GetInt32() != sequence ||
                    requested is < 1 or > 30 || accepted is < 1 || accepted > requested || hashes.Length != accepted ||
                    hashes.Any(static hash => !IsLowerTickHash(hash.GetString())) ||
                    !PlayersAreLegal(actions[index].GetProperty("players")))
                {
                    throw Mismatch("Manifest accepted actions or exact tick/hash coverage are invalid.");
                }
            }
            var parsedReceipts = new AgentPlaytestCausalityReceipt[receipts.Length];
            for (var index = 0; index < receipts.Length; index++)
            {
                if (!HasExactlyInOrder(receipts[index], ReceiptProperties))
                {
                    throw Mismatch("Manifest causality receipt schema is invalid.");
                }
                parsedReceipts[index] = new AgentPlaytestCausalityReceipt(
                    receipts[index].GetProperty("priorFrameSequence").GetInt32(),
                    receipts[index].GetProperty("priorFrameSha256").GetString() ?? string.Empty,
                    receipts[index].GetProperty("requestSequence").GetInt32(),
                    receipts[index].GetProperty("actionIdentity").GetString() ?? string.Empty);
                if (!IsLowerHash(parsedReceipts[index].PriorFrameSha256) ||
                    !IsLowerHash(parsedReceipts[index].ActionIdentity))
                {
                    throw Mismatch("Manifest causality receipt hashes are invalid.");
                }
            }
            ValidateLimits(root.GetProperty("limits"));
            var resourceSamples = resourceElements.Select(ParseResourceSample).ToArray();
            var receiptsMatch = ReceiptsMatchActions(frameHashes, actions, parsedReceipts);
            if (parsedReceipts.Length != 0 && !receiptsMatch)
            {
                throw Mismatch("Manifest causality receipts do not match the ordered frame/action transitions.");
            }
            var rendererEvidenceComplete = status == "renderer-evidence" && cleanup && telemetry && monitor &&
                frameHashes.Distinct(StringComparer.Ordinal).Count() >= 5 &&
                receiptsMatch &&
                AgentPlaytestResourceGate.AcceptsRendererEvidence(
                    AgentPlaytestOwnerConfiguration.Required,
                    resourceSamples);
            if (complete != rendererEvidenceComplete)
            {
                throw Mismatch("Manifest completion does not match renderer, resource, cleanup, and monitor evidence.");
            }

            using var normalized = new MemoryStream();
            using (var writer = new Utf8JsonWriter(normalized, new JsonWriterOptions { Indented = false }))
            {
                root.WriteTo(writer);
            }
            normalized.WriteByte((byte)'\n');
            if (!normalized.ToArray().AsSpan().SequenceEqual(bytes))
            {
                throw Mismatch("Manifest bytes are not canonical.");
            }
        }
    }

    private static void WritePlayers(Utf8JsonWriter writer, IReadOnlyList<AgentPlaytestPlayerAction> players)
    {
        writer.WritePropertyName("players");
        writer.WriteStartArray();
        foreach (var player in players)
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
    }

    private static void WriteLimits(Utf8JsonWriter writer)
    {
        writer.WritePropertyName("limits");
        writer.WriteStartObject();
        writer.WriteNumber("maximumRequests", AgentPlaytestLimits.MaximumRequests);
        writer.WriteNumber("maximumSimulationTicks", AgentPlaytestLimits.MaximumSimulationTicks);
        writer.WriteNumber("maximumFrames", AgentPlaytestLimits.MaximumFrames);
        writer.WriteNumber("maximumOutputBytes", AgentPlaytestLimits.MaximumOutputBytes);
        writer.WriteNumber("routeTimeoutSeconds", AgentPlaytestLimits.RouteTimeoutSeconds);
        writer.WriteNumber("ownerTimeoutSeconds", AgentPlaytestLimits.OwnerTimeoutSeconds);
        writer.WriteNumber("liveBulletCap", AgentPlaytestLimits.LiveBulletCap);
        writer.WriteEndObject();
    }

    private static void ValidateLimits(JsonElement limits)
    {
        if (!HasExactlyInOrder(limits, LimitProperties) ||
            limits.GetProperty("maximumRequests").GetInt32() != AgentPlaytestLimits.MaximumRequests ||
            limits.GetProperty("maximumSimulationTicks").GetInt32() != AgentPlaytestLimits.MaximumSimulationTicks ||
            limits.GetProperty("maximumFrames").GetInt32() != AgentPlaytestLimits.MaximumFrames ||
            limits.GetProperty("maximumOutputBytes").GetInt64() != AgentPlaytestLimits.MaximumOutputBytes ||
            limits.GetProperty("routeTimeoutSeconds").GetInt32() != AgentPlaytestLimits.RouteTimeoutSeconds ||
            limits.GetProperty("ownerTimeoutSeconds").GetInt32() != AgentPlaytestLimits.OwnerTimeoutSeconds ||
            limits.GetProperty("liveBulletCap").GetInt32() != AgentPlaytestLimits.LiveBulletCap)
        {
            throw Mismatch("Manifest declared limits do not match protocol v1.");
        }
    }

    private static bool PlayersAreLegal(JsonElement players)
    {
        if (players.ValueKind != JsonValueKind.Array || players.GetArrayLength() != 2)
        {
            return false;
        }
        foreach (var player in players.EnumerateArray())
        {
            if (!HasExactlyInOrder(player, PlayerProperties) ||
                !player.TryGetProperty("move", out var moveElement) || !moveElement.TryGetSByte(out var move) ||
                !player.TryGetProperty("jump", out var jump) || jump.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !player.TryGetProperty("fire", out var fire) || fire.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !player.TryGetProperty("block", out var block) || block.ValueKind is not (JsonValueKind.True or JsonValueKind.False) ||
                !player.TryGetProperty("aimX", out var aimXElement) || !aimXElement.TryGetDouble(out var aimX) ||
                !player.TryGetProperty("aimY", out var aimYElement) || !aimYElement.TryGetDouble(out var aimY) ||
                !new AgentPlaytestPlayerAction(move, jump.GetBoolean(), fire.GetBoolean(), block.GetBoolean(), aimX, aimY).IsLegal)
            {
                return false;
            }
        }
        return true;
    }

    private static bool ReceiptsMatchActions(
        IReadOnlyList<string?> frameHashes,
        IReadOnlyList<JsonElement> actions,
        IReadOnlyList<AgentPlaytestCausalityReceipt> receipts)
    {
        if (receipts.Count != actions.Count)
        {
            return false;
        }
        for (var index = 0; index < receipts.Count; index++)
        {
            var action = actions[index];
            var players = action.GetProperty("players").EnumerateArray().Select(static player =>
                new AgentPlaytestPlayerAction(
                    player.GetProperty("move").GetSByte(),
                    player.GetProperty("jump").GetBoolean(),
                    player.GetProperty("fire").GetBoolean(),
                    player.GetProperty("block").GetBoolean(),
                    player.GetProperty("aimX").GetDouble(),
                    player.GetProperty("aimY").GetDouble())).ToArray();
            var request = new AgentPlaytestRequest(
                AgentPlaytestLimits.Protocol,
                index + 1,
                action.GetProperty("requestedIntervalTicks").GetInt32(),
                Array.AsReadOnly(players));
            var receipt = receipts[index];
            if (receipt.PriorFrameSequence != index || receipt.RequestSequence != index + 1 ||
                !string.Equals(receipt.PriorFrameSha256, frameHashes[index], StringComparison.Ordinal) ||
                !string.Equals(receipt.ActionIdentity, AgentPlaytestCausality.ActionIdentity(request), StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static AgentPlaytestResourceSample ParseResourceSample(JsonElement element)
    {
        if (!HasExactlyInOrder(element, ResourceSampleProperties))
        {
            throw Mismatch("Manifest resource sample schema is invalid.");
        }
        try
        {
            return JsonSerializer.Deserialize<AgentPlaytestResourceSample>(element.GetRawText())
                ?? throw Mismatch("Manifest resource sample is null.");
        }
        catch (JsonException exception)
        {
            throw Mismatch(exception.Message);
        }
    }

    private static bool HasExactlyInOrder(JsonElement element, IReadOnlyList<string> names) =>
        element.ValueKind == JsonValueKind.Object &&
        element.EnumerateObject().Select(static property => property.Name).SequenceEqual(names);

    private static bool IsLowerHash(string? hash) =>
        hash is { Length: 64 } && hash.All(static character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static bool IsLowerTickHash(string? hash) =>
        hash is { Length: 16 } && hash.All(static character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static AgentPlaytestFailure Mismatch(string message) =>
        new(null, "replay", "replay-mismatch", message);
}
