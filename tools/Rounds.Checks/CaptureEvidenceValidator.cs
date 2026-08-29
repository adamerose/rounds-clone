using System.Text.Json;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Rounds.Checks.Tests")]

namespace Rounds.Checks;

public static class CaptureEvidenceValidator
{
    private const long TwoGiB = 2L * 1024 * 1024 * 1024;
    private const string RequiredVersion = "v1.1.2.a75ee335a";
    private const string ProcessNativeBoundary = "process-native-recording";
    private const string ProcessNativeInputRoute = "target-owned-deterministic-command-channel";
    private const string SeparateSessionBoundary = "existing-separate-gui-session";
    private const string SeparateSessionInputRoute = "session-scoped-hardware-input";
    private static readonly string[] IsolationFalseFields =
    [
        "foregroundActivated",
        "physicalPointerMoved",
        "globalKeyboardInput",
        "globalMouseInput",
        "globalControllerInput",
        "unrelatedPixelsInspected",
        "systemConfigurationChanged",
    ];

    public static string RepositoryRoot => FindRepositoryRoot(AppContext.BaseDirectory);

    public static IReadOnlyList<string> Validate(string json)
    {
        try
        {
            return ValidateCore(json, RepositoryRoot, ResolvePhysicalPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            return [$"validator could not attest its project-owned repository root: {exception.Message}"];
        }
    }

    internal static IReadOnlyList<string> ValidateForTests(
        string json,
        string repositoryRoot,
        Func<string, string> physicalPathResolver)
        => ValidateCore(json, repositoryRoot, physicalPathResolver);

    private static IReadOnlyList<string> ValidateCore(
        string json,
        string repositoryRoot,
        Func<string, string> physicalPathResolver)
    {
        var failures = new List<string>();
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return ["capture evidence root must be an object"];
            }

            RejectDuplicateProperties(root, "$", failures);
            RequireInt(root, "format", 1, failures);
            var target = RequireObject(root, "target", failures);
            var isolation = RequireObject(root, "isolation", failures);
            var limits = RequireObject(root, "limits", failures);
            var capture = RequireObject(root, "capture", failures);
            var targetProcessIds = ValidateTarget(target, failures);
            var boundary = ValidateIsolation(isolation, failures);
            var writerProcessIds = ValidateCapture(capture, repositoryRoot, physicalPathResolver, failures);
            ValidateDisplay(RequireObject(root, "display", failures), failures);
            var declaredLimits = ValidateLimits(limits, boundary, targetProcessIds, writerProcessIds, failures);
            ValidateHeartbeat(RequireObject(root, "heartbeat", failures), failures);
            ValidateResources(
                RequireObject(root, "resources", failures),
                RequireObject(root, "heartbeat", failures),
                targetProcessIds,
                writerProcessIds,
                declaredLimits,
                failures);
            ValidateDurationAgreement(root, failures);
            ValidateCleanup(RequireObject(root, "cleanup", failures), targetProcessIds, writerProcessIds, failures);
        }
        catch (JsonException exception)
        {
            failures.Add($"capture evidence is not strict JSON: {exception.Message}");
        }
        catch (InvalidOperationException exception)
        {
            failures.Add($"capture evidence has an invalid value kind: {exception.Message}");
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            failures.Add($"capture evidence contains an invalid path: {exception.Message}");
        }

        return failures;
    }

    private static HashSet<int> ValidateTarget(JsonElement target, List<string> failures)
    {
        if (target.ValueKind == JsonValueKind.Undefined) return [];
        RequireInt(target, "steamAppId", 1557740, failures);
        RequireString(target, "buildId", "21020021", failures);
        RequireString(target, "version", RequiredVersion, failures);
        RequireSha256(target, "executableSha256", failures);
        RequireNonEmptyString(target, "controlledState", failures);
        var loadout = RequireArray(target, "loadout", failures);
        if (loadout.ValueKind != JsonValueKind.Undefined && loadout.GetArrayLength() == 0)
        {
            failures.Add("target.loadout must name at least one controlled loadout item");
        }
        else if (loadout.ValueKind != JsonValueKind.Undefined && loadout.EnumerateArray().Any(static item =>
                     item.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(item.GetString())))
        {
            failures.Add("target.loadout entries must be non-empty strings");
        }
        return ReadProcessIds(target, "processIds", "target.processIds", 1, 1, failures);
    }

    private static string? ValidateIsolation(JsonElement isolation, List<string> failures)
    {
        if (isolation.ValueKind == JsonValueKind.Undefined) return null;
        var boundary = GetString(isolation, "boundary", failures);
        var inputRoute = GetString(isolation, "inputRoute", failures);
        var expectedRoute = boundary switch
        {
            ProcessNativeBoundary => ProcessNativeInputRoute,
            SeparateSessionBoundary => SeparateSessionInputRoute,
            _ => null,
        };
        if (expectedRoute is null)
        {
            failures.Add("isolation.boundary must be process-native-recording or existing-separate-gui-session");
        }
        else if (inputRoute != expectedRoute)
        {
            failures.Add($"isolation.inputRoute must be {expectedRoute} when isolation.boundary is {boundary}");
        }
        foreach (var field in IsolationFalseFields)
        {
            RequireBool(isolation, field, expected: false, failures);
        }
        return boundary;
    }

    private static void ValidateDisplay(JsonElement display, List<string> failures)
    {
        if (display.ValueKind == JsonValueKind.Undefined) return;
        RequireInt(display, "screenIndex", 3, failures);
        RequireString(display, "device", @"\\.\DISPLAY4", failures);
        RequireBool(display, "placementVerifiedBeforeVisible", expected: true, failures);
        var monitor = ReadRect(RequireObject(display, "monitor", failures), "display.monitor", failures);
        var window = ReadRect(RequireObject(display, "window", failures), "display.window", failures);
        if (monitor is null || window is null) return;
        if (monitor.Value.Width != 1920 || monitor.Value.Height != 1080)
        {
            failures.Add("display.monitor must be exactly 1920x1080");
        }
        if (window.Value.Left < monitor.Value.Left || window.Value.Top < monitor.Value.Top ||
            window.Value.Right > monitor.Value.Right || window.Value.Bottom > monitor.Value.Bottom)
        {
            failures.Add("display.window must be wholly contained by monitor 4");
        }
        var centerX = window.Value.Left + (window.Value.Width / 2.0);
        var centerY = window.Value.Top + (window.Value.Height / 2.0);
        if (centerX < monitor.Value.Left || centerX >= monitor.Value.Right ||
            centerY < monitor.Value.Top || centerY >= monitor.Value.Bottom)
        {
            failures.Add("display.window center must be on monitor 4");
        }
    }

    private static DeclaredLimits ValidateLimits(
        JsonElement limits,
        string? boundary,
        IReadOnlySet<int> targetProcessIds,
        IReadOnlySet<int> writerProcessIds,
        List<string> failures)
    {
        if (limits.ValueKind == JsonValueKind.Undefined) return DeclaredLimits.HardMaximums;
        RequireString(limits, "priority", "below-normal", failures);
        var logicalProcessorSelection = ReadLogicalProcessors(limits, "logicalProcessors", "limits.logicalProcessors", failures);
        var logicalProcessors = logicalProcessorSelection.Values;
        var privateMemoryBytes = RequireRange(limits, "privateMemoryBytes", 1, TwoGiB, failures) ?? TwoGiB;
        var dedicatedGpuMemoryBytes = RequireRange(limits, "dedicatedGpuMemoryBytes", 1, TwoGiB, failures) ?? TwoGiB;
        var maxGpuPercent = RequireNumberRange(limits, "maxGpuPercent", 0, 70, failures) ?? 70;
        RequireRange(limits, "timeoutSeconds", 1, 60, failures);
        var targetCount = RequireRange(limits, "targetProcessCount", 1, 1, failures);
        var writerCount = RequireRange(limits, "captureWriterCount", 0, 1, failures);
        if (targetCount is not null && targetCount != targetProcessIds.Count)
        {
            failures.Add("limits.targetProcessCount must equal target.processIds count");
        }
        if (writerCount is not null && writerCount != writerProcessIds.Count)
        {
            failures.Add("limits.captureWriterCount must equal capture.writerProcessIds count");
        }
        if (targetProcessIds.Overlaps(writerProcessIds))
        {
            failures.Add("target.processIds and capture.writerProcessIds must be disjoint");
        }
        if (boundary == ProcessNativeBoundary && writerProcessIds.Count != 0)
        {
            failures.Add("process-native-recording must have zero separate capture writer processes");
        }
        RequireBool(limits, "automaticCleanup", expected: true, failures);
        return new DeclaredLimits(
            logicalProcessors,
            privateMemoryBytes,
            dedicatedGpuMemoryBytes,
            maxGpuPercent,
            logicalProcessorSelection.IsValid ? logicalProcessors.Count * 100.0 : 0);
    }

    private static HashSet<int> ValidateCapture(
        JsonElement capture,
        string repositoryRoot,
        Func<string, string> physicalPathResolver,
        List<string> failures)
    {
        if (capture.ValueKind == JsonValueKind.Undefined) return [];
        var width = RequireRange(capture, "width", 1, 1280, failures);
        var height = RequireRange(capture, "height", 1, 720, failures);
        var framesPerSecond = RequireRange(capture, "framesPerSecond", 1, 60, failures);
        var duration = RequireNumberRange(capture, "durationSeconds", double.Epsilon, 20, failures);
        var tolerance = RequireNumberRange(capture, "cadenceToleranceMs", 0, 5, failures);
        RequireBool(capture, "outputExistedBeforeCapture", expected: false, failures);
        RequireBool(capture, "refuseOverwrite", expected: true, failures);
        RequireSha256(capture, "rawSha256", failures);

        var rawPath = GetString(capture, "rawPath", failures);
        if (rawPath is not null)
        {
            if (!Path.IsPathFullyQualified(rawPath)) failures.Add("capture.rawPath must be absolute");
            else
            {
                var repository = physicalPathResolver(repositoryRoot);
                var raw = physicalPathResolver(rawPath);
                if (IsSameOrDescendant(raw, repository))
                {
                    failures.Add("capture.rawPath must remain outside the repository");
                }
            }
        }

        var expectedFrames = RequireRange(capture, "expectedFrames", 1, 1200, failures);
        var capturedFrames = RequireRange(capture, "capturedFrames", 1, 1200, failures);
        RequireInt(capture, "droppedFrames", 0, failures);
        RequireInt(capture, "duplicatedFrames", 0, failures);
        if (framesPerSecond is not null && duration is not null && expectedFrames is not null)
        {
            var calculated = (int)Math.Round(framesPerSecond.Value * duration.Value, MidpointRounding.AwayFromZero);
            if (expectedFrames != calculated) failures.Add($"capture.expectedFrames must equal fps * duration ({calculated})");
        }
        if (expectedFrames is not null && capturedFrames is not null && expectedFrames != capturedFrames)
        {
            failures.Add("capture.capturedFrames must equal capture.expectedFrames");
        }

        var timestamps = RequireNumberArray(RequireArray(capture, "frameTimestampsMs", failures), "capture.frameTimestampsMs", failures);
        if (capturedFrames is not null && timestamps.Count != capturedFrames)
        {
            failures.Add("capture.frameTimestampsMs count must equal capture.capturedFrames");
        }
        if (framesPerSecond is not null && tolerance is not null)
        {
            var expectedInterval = 1000.0 / framesPerSecond.Value;
            for (var index = 1; index < timestamps.Count; index++)
            {
                var interval = timestamps[index] - timestamps[index - 1];
                if (interval <= 0 || Math.Abs(interval - expectedInterval) > tolerance.Value)
                {
                    failures.Add($"capture frame interval {index - 1}->{index} is {interval:F3} ms; expected {expectedInterval:F3} +/- {tolerance.Value:F3} ms");
                }
            }
        }
        ValidateSourceCoordinates(
            RequireArray(capture, "sourceCoordinates", failures),
            width,
            height,
            capturedFrames,
            failures);
        return ReadProcessIds(capture, "writerProcessIds", "capture.writerProcessIds", 0, 1, failures);
    }

    private static void ValidateHeartbeat(JsonElement heartbeat, List<string> failures)
    {
        if (heartbeat.ValueKind == JsonValueKind.Undefined) return;
        RequireInt(heartbeat, "periodMs", 20, failures);
        var baselineDuration = RequireNumberRange(heartbeat, "baselineDurationSeconds", 10, 60, failures);
        var captureDuration = RequireNumberRange(heartbeat, "captureDurationSeconds", double.Epsilon, 20, failures);
        var baseline = RequireNumberArray(RequireArray(heartbeat, "baselineDelaysMs", failures), "heartbeat.baselineDelaysMs", failures);
        var capture = RequireNumberArray(RequireArray(heartbeat, "captureDelaysMs", failures), "heartbeat.captureDelaysMs", failures);
        if (baselineDuration is not null && baseline.Count < Math.Floor(baselineDuration.Value * 50))
        {
            failures.Add("heartbeat.baselineDelaysMs must contain every 20 ms sample");
        }
        if (captureDuration is not null && capture.Count < Math.Floor(captureDuration.Value * 50))
        {
            failures.Add("heartbeat.captureDelaysMs must contain every 20 ms sample");
        }
        if (baseline.Count == 0 || capture.Count == 0) return;
        var baselineP95 = Percentile95(baseline);
        var captureP95 = Percentile95(capture);
        if (captureP95 > baselineP95 + 5) failures.Add($"heartbeat capture p95 {captureP95:F3} ms exceeds baseline p95 {baselineP95:F3} ms + 5 ms");
        if (captureP95 > 15) failures.Add($"heartbeat capture p95 {captureP95:F3} ms exceeds 15 ms");
        var maximum = capture.Max();
        if (maximum > 50) failures.Add($"heartbeat capture maximum {maximum:F3} ms exceeds 50 ms");
    }

    private static void ValidateResources(
        JsonElement resources,
        JsonElement heartbeat,
        IReadOnlySet<int> targetProcessIds,
        IReadOnlySet<int> writerProcessIds,
        DeclaredLimits declaredLimits,
        List<string> failures)
    {
        if (resources.ValueKind == JsonValueKind.Undefined) return;
        var baselineSeconds = heartbeat.ValueKind == JsonValueKind.Undefined
            ? null
            : ReadFiniteNumber(heartbeat, "baselineDurationSeconds");
        var captureSeconds = heartbeat.ValueKind == JsonValueKind.Undefined
            ? null
            : ReadFiniteNumber(heartbeat, "captureDurationSeconds");
        ValidateResourceSamples(
            RequireArray(resources, "baseline", failures),
            "resources.baseline",
            baselineSeconds,
            new Dictionary<int, string>(),
            declaredLimits,
            failures);
        var attributableProcesses = targetProcessIds.ToDictionary(static processId => processId, static _ => "target");
        foreach (var writerProcessId in writerProcessIds) attributableProcesses.Add(writerProcessId, "capture-writer");
        ValidateResourceSamples(
            RequireArray(resources, "capture", failures),
            "resources.capture",
            captureSeconds,
            attributableProcesses,
            declaredLimits,
            failures);
    }

    private static void ValidateResourceSamples(
        JsonElement samples,
        string path,
        double? durationSeconds,
        IReadOnlyDictionary<int, string> expectedProcesses,
        DeclaredLimits declaredLimits,
        List<string> failures)
    {
        if (samples.ValueKind == JsonValueKind.Undefined) return;
        if (samples.GetArrayLength() == 0) failures.Add($"{path} must contain one-second samples");
        if (durationSeconds is not null && samples.GetArrayLength() < Math.Ceiling(durationSeconds.Value))
        {
            failures.Add($"{path} must contain every one-second sample for the declared duration");
        }
        var priorTimestamp = -1000L;
        var index = 0;
        foreach (var sample in samples.EnumerateArray())
        {
            if (sample.ValueKind != JsonValueKind.Object)
            {
                failures.Add($"{path}[{index}] must be an object");
                index++;
                continue;
            }
            var timestamp = RequireRange(sample, "timestampMs", 0, int.MaxValue, failures, $"{path}[{index}]");
            if (timestamp is not null && index > 0 && timestamp - priorTimestamp is < 900 or > 1100)
            {
                failures.Add($"{path} samples must be spaced one second apart (+/- 100 ms)");
            }
            if (timestamp is not null) priorTimestamp = timestamp.Value;
            RequireNumberRange(sample, "systemCpuPercent", 0, 100, failures, $"{path}[{index}]");
            var attributableCpu = RequireNumberRange(sample, "attributableCpuPercent", 0, declaredLimits.CpuPercentCapacity, failures, $"{path}[{index}]");
            RequireNumberRange(sample, "systemGpuPercent", 0, 70, failures, $"{path}[{index}]");
            var attributableGpu = RequireNumberRange(sample, "attributableGpuPercent", 0, declaredLimits.MaxGpuPercent, failures, $"{path}[{index}]");
            var attributableGpuMemory = RequireRange(sample, "attributableDedicatedGpuMemoryBytes", 0, declaredLimits.DedicatedGpuMemoryBytes, failures, $"{path}[{index}]");
            var attributablePrivateMemory = RequireRange(sample, "attributablePrivateMemoryBytes", 0, declaredLimits.PrivateMemoryBytes, failures, $"{path}[{index}]");
            var recordedProcesses = ValidateProcessResourceSamples(
                RequireArray(sample, "processes", failures),
                $"{path}[{index}].processes",
                expectedProcesses,
                declaredLimits,
                failures);
            RequireAggregateAgreement(attributableCpu, recordedProcesses.Sum(static process => process.CpuPercent), $"{path}[{index}].attributableCpuPercent", failures);
            RequireAggregateAgreement(attributableGpu, recordedProcesses.Sum(static process => process.GpuPercent), $"{path}[{index}].attributableGpuPercent", failures);
            RequireAggregateAgreement(attributableGpuMemory, recordedProcesses.Sum(static process => process.DedicatedGpuMemoryBytes), $"{path}[{index}].attributableDedicatedGpuMemoryBytes", failures);
            RequireAggregateAgreement(attributablePrivateMemory, recordedProcesses.Sum(static process => process.PrivateMemoryBytes), $"{path}[{index}].attributablePrivateMemoryBytes", failures);
            index++;
        }
    }

    private static List<ProcessResourceSample> ValidateProcessResourceSamples(
        JsonElement processes,
        string path,
        IReadOnlyDictionary<int, string> expectedProcesses,
        DeclaredLimits declaredLimits,
        List<string> failures)
    {
        var result = new List<ProcessResourceSample>();
        if (processes.ValueKind == JsonValueKind.Undefined) return result;
        var observed = new HashSet<int>();
        var observedAffinity = new HashSet<int>();
        var index = 0;
        foreach (var process in processes.EnumerateArray())
        {
            var itemPath = $"{path}[{index}]";
            if (process.ValueKind != JsonValueKind.Object)
            {
                failures.Add($"{itemPath} must be an object");
                index++;
                continue;
            }
            var processId = RequireRange(process, "processId", 1, int.MaxValue, failures, itemPath);
            var role = GetString(process, "role", failures);
            RequireString(process, "priority", "below-normal", failures);
            var affinitySelection = ReadLogicalProcessors(process, "logicalProcessors", $"{itemPath}.logicalProcessors", failures);
            var affinity = affinitySelection.Values;
            var affinityIsDeclared = affinity.IsSubsetOf(declaredLimits.LogicalProcessors);
            if (!affinityIsDeclared)
            {
                failures.Add($"{itemPath}.logicalProcessors must be a subset of limits.logicalProcessors");
            }
            observedAffinity.UnionWith(affinity);
            var processCpuCapacity = affinitySelection.IsValid && affinityIsDeclared ? affinity.Count * 100.0 : 0;
            var cpu = RequireNumberRange(process, "cpuPercent", 0, processCpuCapacity, failures, itemPath);
            var gpu = RequireNumberRange(process, "gpuPercent", 0, declaredLimits.MaxGpuPercent, failures, itemPath);
            var gpuMemory = RequireRange(process, "dedicatedGpuMemoryBytes", 0, declaredLimits.DedicatedGpuMemoryBytes, failures, itemPath);
            var privateMemory = RequireRange(process, "privateMemoryBytes", 0, declaredLimits.PrivateMemoryBytes, failures, itemPath);
            if (processId is not null)
            {
                var id = checked((int)processId.Value);
                if (!observed.Add(id)) failures.Add($"{path} contains duplicate process ID {id}");
                if (!expectedProcesses.TryGetValue(id, out var expectedRole)) failures.Add($"{path} contains undeclared process ID {id}");
                else if (role != expectedRole) failures.Add($"{itemPath}.role must be {expectedRole} for process ID {id}");
            }
            if (cpu is not null && gpu is not null && gpuMemory is not null && privateMemory is not null)
            {
                result.Add(new ProcessResourceSample(cpu.Value, gpu.Value, gpuMemory.Value, privateMemory.Value));
            }
            index++;
        }
        foreach (var expectedProcessId in expectedProcesses.Keys)
        {
            if (!observed.Contains(expectedProcessId)) failures.Add($"{path} is missing declared process ID {expectedProcessId}");
        }
        if (!observedAffinity.IsSubsetOf(declaredLimits.LogicalProcessors))
        {
            failures.Add($"{path} affinity union must remain within limits.logicalProcessors");
        }
        return result;
    }

    private static void ValidateCleanup(
        JsonElement cleanup,
        IReadOnlySet<int> targetProcessIds,
        IReadOnlySet<int> writerProcessIds,
        List<string> failures)
    {
        if (cleanup.ValueKind == JsonValueKind.Undefined) return;
        RequireBool(cleanup, "targetExitObserved", expected: true, failures);
        RequireBool(cleanup, "captureWriterExitObserved", expected: writerProcessIds.Count == 1, failures);
        var expectedExited = targetProcessIds.Concat(writerProcessIds).ToHashSet();
        var exited = ReadProcessIds(cleanup, "exitedProcessIds", "cleanup.exitedProcessIds", expectedExited.Count, expectedExited.Count, failures);
        if (!exited.SetEquals(expectedExited)) failures.Add("cleanup.exitedProcessIds must exactly equal the declared target and capture-writer process union");
        var residual = RequireArray(cleanup, "residualProcessIds", failures);
        if (residual.ValueKind != JsonValueKind.Undefined && residual.GetArrayLength() != 0)
        {
            failures.Add("cleanup.residualProcessIds must be empty");
        }
    }

    private static void ValidateDurationAgreement(JsonElement root, List<string> failures)
    {
        if (!root.TryGetProperty("capture", out var capture) || capture.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("heartbeat", out var heartbeat) || heartbeat.ValueKind != JsonValueKind.Object)
        {
            return;
        }
        var captureSeconds = ReadFiniteNumber(capture, "durationSeconds");
        var heartbeatSeconds = ReadFiniteNumber(heartbeat, "captureDurationSeconds");
        if (captureSeconds is not null && heartbeatSeconds is not null && Math.Abs(captureSeconds.Value - heartbeatSeconds.Value) > 0.000001)
        {
            failures.Add("heartbeat.captureDurationSeconds must equal capture.durationSeconds");
        }
    }

    private static void ValidateSourceCoordinates(
        JsonElement coordinates,
        long? width,
        long? height,
        long? capturedFrames,
        List<string> failures)
    {
        if (coordinates.ValueKind == JsonValueKind.Undefined) return;
        if (coordinates.GetArrayLength() == 0) failures.Add("capture.sourceCoordinates must contain at least one frame-addressable observation");
        var index = 0;
        foreach (var coordinate in coordinates.EnumerateArray())
        {
            var path = $"capture.sourceCoordinates[{index}]";
            if (coordinate.ValueKind != JsonValueKind.Object)
            {
                failures.Add($"{path} must be an object");
                index++;
                continue;
            }
            RequireNonEmptyString(coordinate, "label", failures);
            RequireRange(coordinate, "frame", 0, capturedFrames is null ? 1199 : capturedFrames.Value - 1, failures, path);
            RequireNumberRange(coordinate, "x", 0, Math.BitDecrement(width is null ? 1280 : (double)width.Value), failures, path);
            RequireNumberRange(coordinate, "y", 0, Math.BitDecrement(height is null ? 720 : (double)height.Value), failures, path);
            index++;
        }
    }

    private static double? ReadFiniteNumber(JsonElement parent, string name)
    {
        return parent.TryGetProperty(name, out var value) && value.TryGetDouble(out var number) && double.IsFinite(number)
            ? number
            : null;
    }

    private static void RejectDuplicateProperties(JsonElement element, string path, List<string> failures)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) failures.Add($"duplicate JSON property at {path}.{property.Name}");
                RejectDuplicateProperties(property.Value, $"{path}.{property.Name}", failures);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in element.EnumerateArray())
            {
                RejectDuplicateProperties(item, $"{path}[{index}]", failures);
                index++;
            }
        }
    }

    private static HashSet<int> ReadProcessIds(
        JsonElement parent,
        string name,
        string path,
        int minimumCount,
        int maximumCount,
        List<string> failures)
    {
        var array = RequireArray(parent, name, failures);
        var result = new HashSet<int>();
        if (array.ValueKind == JsonValueKind.Undefined) return result;
        if (array.GetArrayLength() < minimumCount || array.GetArrayLength() > maximumCount)
        {
            failures.Add($"{path} must contain between {minimumCount} and {maximumCount} entries");
        }
        foreach (var value in array.EnumerateArray())
        {
            if (!value.TryGetInt32(out var processId) || processId <= 0)
            {
                failures.Add($"{path} must contain positive integer process IDs");
                continue;
            }
            if (!result.Add(processId)) failures.Add($"{path} contains duplicate process ID {processId}");
        }
        return result;
    }

    private static void RequireAggregateAgreement(double? declared, double calculated, string path, List<string> failures)
    {
        if (declared is not null && Math.Abs(declared.Value - calculated) > 0.001)
        {
            failures.Add($"{path} must equal the sum of its exact process inventory ({calculated:F3})");
        }
    }

    private static void RequireAggregateAgreement(long? declared, long calculated, string path, List<string> failures)
    {
        if (declared is not null && declared.Value != calculated)
        {
            failures.Add($"{path} must equal the sum of its exact process inventory ({calculated})");
        }
    }

    private static string FindRepositoryRoot(string startPath)
    {
        for (var directory = new DirectoryInfo(Path.GetFullPath(startPath)); directory is not null; directory = directory.Parent)
        {
            var root = directory.FullName;
            if (!File.Exists(Path.Combine(root, "Rounds.sln")) ||
                !File.Exists(Path.Combine(root, "GOAL.md")) ||
                !File.Exists(Path.Combine(root, "tools", "Rounds.Checks", "Rounds.Checks.csproj")) ||
                !File.Exists(Path.Combine(root, "spec", "sources.json")) ||
                (!File.Exists(Path.Combine(root, ".git")) && !Directory.Exists(Path.Combine(root, ".git"))))
            {
                continue;
            }

            var solution = File.ReadAllText(Path.Combine(root, "Rounds.sln"));
            var goal = File.ReadAllText(Path.Combine(root, "GOAL.md"));
            var sources = File.ReadAllText(Path.Combine(root, "spec", "sources.json"));
            if (!solution.Contains("Rounds.Checks", StringComparison.Ordinal) ||
                !goal.Contains("faithful", StringComparison.OrdinalIgnoreCase) ||
                !sources.Contains("1557740", StringComparison.Ordinal) ||
                !sources.Contains("21020021", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"candidate repository identity markers are invalid at {root}");
            }
            return ResolvePhysicalPath(root);
        }
        throw new InvalidOperationException("Rounds candidate repository root was not found from the validator executable layout");
    }

    private static string ResolvePhysicalPath(string path)
    {
        var fullPath = Path.GetFullPath(NormalizeFilesystemNamespace(path));
        var root = Path.GetPathRoot(fullPath) ?? throw new InvalidOperationException($"path has no root: {path}");
        var relative = fullPath[root.Length..];
        var current = root;
        foreach (var component in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(current, component);
            FileSystemInfo? information = Directory.Exists(candidate)
                ? new DirectoryInfo(candidate)
                : File.Exists(candidate)
                    ? new FileInfo(candidate)
                    : null;
            if (information is not null && (information.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                information = information.ResolveLinkTarget(returnFinalTarget: true)
                    ?? throw new InvalidOperationException($"could not resolve reparse point {candidate}");
                current = Path.GetFullPath(NormalizeFilesystemNamespace(information.FullName));
            }
            else
            {
                current = candidate;
            }
        }
        return Path.GetFullPath(NormalizeFilesystemNamespace(current)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string NormalizeFilesystemNamespace(string path)
    {
        if (!OperatingSystem.IsWindows()) return path;
        var normalized = path.Replace('/', '\\');
        if (normalized.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith(@"\\.\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            normalized = @"\\" + normalized[8..];
        }
        else if (normalized.StartsWith(@"\\?\", StringComparison.OrdinalIgnoreCase) ||
                 normalized.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase))
        {
            var tail = normalized[4..];
            if (!IsDosDrivePath(tail))
            {
                throw new NotSupportedException($"non-filesystem or unrecognized Windows device path is forbidden: {path}");
            }
            normalized = tail;
        }
        else if (normalized.StartsWith(@"\??\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            normalized = @"\\" + normalized[8..];
        }
        else if (normalized.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase))
        {
            var tail = normalized[4..];
            if (!IsDosDrivePath(tail))
            {
                throw new NotSupportedException($"non-filesystem or unrecognized NT device path is forbidden: {path}");
            }
            normalized = tail;
        }

        return NormalizeLocalAdministrativeShare(normalized);
    }

    private static bool IsDosDrivePath(string path) =>
        path.Length >= 3 && char.IsAsciiLetter(path[0]) && path[1] == ':' && path[2] == '\\';

    private static string NormalizeLocalAdministrativeShare(string path)
    {
        if (!path.StartsWith(@"\\", StringComparison.Ordinal)) return path;
        var serverEnd = path.IndexOf('\\', 2);
        if (serverEnd < 0) throw new NotSupportedException($"UNC path cannot be proven outside the local repository: {path}");
        var shareEnd = path.IndexOf('\\', serverEnd + 1);
        var server = path[2..serverEnd];
        var share = shareEnd < 0 ? path[(serverEnd + 1)..] : path[(serverEnd + 1)..shareEnd];
        if (!IsLocalServerAlias(server) || share.Length != 2 || !char.IsAsciiLetter(share[0]) || share[1] != '$')
        {
            throw new NotSupportedException($"UNC path cannot be proven outside the local repository: {path}");
        }
        var suffix = shareEnd < 0 ? string.Empty : path[(shareEnd + 1)..];
        return $"{char.ToUpperInvariant(share[0])}:\\{suffix}";
    }

    private static bool IsLocalServerAlias(string server) =>
        server.Equals(".", StringComparison.OrdinalIgnoreCase) ||
        server.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        server.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
        server.Equals("[::1]", StringComparison.OrdinalIgnoreCase) ||
        server.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);

    private static bool IsSameOrDescendant(string candidate, string directory)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var canonicalCandidate = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var canonicalDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return canonicalCandidate.Equals(canonicalDirectory, comparison) ||
            canonicalCandidate.StartsWith(canonicalDirectory + Path.DirectorySeparatorChar, comparison);
    }

    private static JsonElement RequireObject(JsonElement parent, string name, List<string> failures)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            failures.Add($"{name} must be an object");
            return default;
        }
        return value;
    }

    private static JsonElement RequireArray(JsonElement parent, string name, List<string> failures)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            failures.Add($"{name} must be an array");
            return default;
        }
        return value;
    }

    private static string? GetString(JsonElement parent, string name, List<string> failures)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            failures.Add($"{name} must be a string");
            return null;
        }
        return value.GetString();
    }

    private static void RequireString(JsonElement parent, string name, string expected, List<string> failures)
    {
        var value = GetString(parent, name, failures);
        if (value is not null && value != expected) failures.Add($"{name} must be {expected}");
    }

    private static void RequireNonEmptyString(JsonElement parent, string name, List<string> failures)
    {
        var value = GetString(parent, name, failures);
        if (value is not null && string.IsNullOrWhiteSpace(value)) failures.Add($"{name} must not be empty");
    }

    private static void RequireSha256(JsonElement parent, string name, List<string> failures)
    {
        var value = GetString(parent, name, failures);
        if (value is not null && (value.Length != 64 || value.Any(static character => !char.IsAsciiHexDigit(character) || char.IsUpper(character))))
        {
            failures.Add($"{name} must be a lowercase SHA-256 digest");
        }
    }

    private static void RequireInt(JsonElement parent, string name, int expected, List<string> failures)
    {
        if (!parent.TryGetProperty(name, out var value) || !value.TryGetInt32(out var actual)) failures.Add($"{name} must be integer {expected}");
        else if (actual != expected) failures.Add($"{name} must be {expected}");
    }

    private static void RequireBool(JsonElement parent, string name, bool expected, List<string> failures)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            failures.Add($"{name} must be boolean {expected.ToString().ToLowerInvariant()}");
        }
        else if (value.GetBoolean() != expected)
        {
            failures.Add($"{name} must be {expected.ToString().ToLowerInvariant()}");
        }
    }

    private static long? RequireRange(JsonElement parent, string name, long minimum, long maximum, List<string> failures, string? prefix = null)
    {
        var path = prefix is null ? name : $"{prefix}.{name}";
        if (!parent.TryGetProperty(name, out var value) || !value.TryGetInt64(out var actual))
        {
            failures.Add($"{path} must be an integer");
            return null;
        }
        if (actual < minimum || actual > maximum) failures.Add($"{path} must be between {minimum} and {maximum}");
        return actual;
    }

    private static double? RequireNumberRange(JsonElement parent, string name, double minimum, double maximum, List<string> failures, string? prefix = null)
    {
        var path = prefix is null ? name : $"{prefix}.{name}";
        if (!parent.TryGetProperty(name, out var value) || !value.TryGetDouble(out var actual) || !double.IsFinite(actual))
        {
            failures.Add($"{path} must be a finite number");
            return null;
        }
        if (actual < minimum || actual > maximum) failures.Add($"{path} must be between {minimum} and {maximum}");
        return actual;
    }

    private static LogicalProcessorSelection ReadLogicalProcessors(
        JsonElement parent,
        string name,
        string path,
        List<string> failures)
    {
        var array = RequireArray(parent, name, failures);
        var result = new HashSet<int>();
        if (array.ValueKind == JsonValueKind.Undefined) return new(result, false);
        var valid = true;
        if (array.GetArrayLength() < 1 || array.GetArrayLength() > 2)
        {
            failures.Add($"{path} must contain between 1 and 2 entries");
            valid = false;
        }
        foreach (var value in array.EnumerateArray())
        {
            if (!value.TryGetInt32(out var processor) || processor < 0 || !result.Add(processor))
            {
                failures.Add($"{path} must contain distinct non-negative integers");
                valid = false;
            }
        }
        return new(result, valid);
    }

    private static List<double> RequireNumberArray(JsonElement array, string path, List<string> failures)
    {
        var result = new List<double>();
        if (array.ValueKind == JsonValueKind.Undefined) return result;
        foreach (var value in array.EnumerateArray())
        {
            if (!value.TryGetDouble(out var number) || !double.IsFinite(number) || number < 0)
            {
                failures.Add($"{path} must contain finite non-negative numbers");
                return result;
            }
            result.Add(number);
        }
        return result;
    }

    private static Rect? ReadRect(JsonElement element, string path, List<string> failures)
    {
        if (element.ValueKind == JsonValueKind.Undefined) return null;
        var left = RequireRange(element, "left", int.MinValue, int.MaxValue, failures, path);
        var top = RequireRange(element, "top", int.MinValue, int.MaxValue, failures, path);
        var right = RequireRange(element, "right", int.MinValue, int.MaxValue, failures, path);
        var bottom = RequireRange(element, "bottom", int.MinValue, int.MaxValue, failures, path);
        if (left is null || top is null || right is null || bottom is null) return null;
        if (right <= left || bottom <= top)
        {
            failures.Add($"{path} must have positive width and height");
            return null;
        }
        return new Rect(left.Value, top.Value, right.Value, bottom.Value);
    }

    private static double Percentile95(List<double> values)
    {
        var ordered = values.Order().ToArray();
        return ordered[(int)Math.Ceiling(ordered.Length * 0.95) - 1];
    }

    private readonly record struct Rect(long Left, long Top, long Right, long Bottom)
    {
        public long Width => Right - Left;
        public long Height => Bottom - Top;
    }

    private readonly record struct ProcessResourceSample(
        double CpuPercent,
        double GpuPercent,
        long DedicatedGpuMemoryBytes,
        long PrivateMemoryBytes);

    private sealed record DeclaredLimits(
        HashSet<int> LogicalProcessors,
        long PrivateMemoryBytes,
        long DedicatedGpuMemoryBytes,
        double MaxGpuPercent,
        double CpuPercentCapacity)
    {
        public static DeclaredLimits HardMaximums { get; } = new([0, 1], TwoGiB, TwoGiB, 70, 200);
    }

    private sealed record LogicalProcessorSelection(HashSet<int> Values, bool IsValid);
}
