using System.Text.Json;
using System.Text.Json.Nodes;

namespace Rounds.Checks.Tests;

public sealed class CaptureEvidenceValidatorTests
{
    [Fact]
    public void AcceptsProcessNativeEvidenceWithNoSeparateWriter()
    {
        Assert.Empty(CaptureEvidenceValidator.Validate(ValidEvidence()));
    }

    [Fact]
    public void AcceptsExistingSeparateSessionWithOneWriter()
    {
        Assert.Empty(CaptureEvidenceValidator.Validate(ValidEvidence(withWriter: true)));
    }

    [Fact]
    public void AcceptsExistingSeparateSessionWithoutSeparateWriter()
    {
        var evidence = ParseValid();
        evidence["isolation"]!["boundary"] = "existing-separate-gui-session";
        evidence["isolation"]!["inputRoute"] = "session-scoped-hardware-input";

        Assert.Empty(CaptureEvidenceValidator.Validate(evidence.ToJsonString()));
    }

    [Theory]
    [InlineData("display.screenIndex", "0", "screenIndex must be 3")]
    [InlineData("isolation.foregroundActivated", "true", "foregroundActivated must be false")]
    [InlineData("isolation.globalKeyboardInput", "true", "globalKeyboardInput must be false")]
    [InlineData("limits.logicalProcessors", "[0,1,2]", "between 1 and 2 entries")]
    [InlineData("capture.width", "1281", "width must be between 1 and 1280")]
    [InlineData("capture.outputExistedBeforeCapture", "true", "outputExistedBeforeCapture must be false")]
    [InlineData("target.processIds", "[0]", "positive integer process IDs")]
    [InlineData("resources.capture.0.systemGpuPercent", "70.1", "systemGpuPercent must be between 0 and 70")]
    [InlineData("cleanup.residualProcessIds", "[123]", "residualProcessIds must be empty")]
    public void RefusesUnsafeOrUnboundedEvidence(string path, string replacementJson, string expectedFailure)
    {
        var evidence = ParseValid();
        Replace(evidence, path, JsonNode.Parse(replacementJson));

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains(failures, failure => failure.Contains(expectedFailure, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("SendInput")]
    [InlineData("PostMessage")]
    [InlineData("WScript SendKeys")]
    [InlineData("mouse_event")]
    [InlineData("global keyboard input")]
    [InlineData("virtual-input-driver")]
    public void RefusesKnownForbiddenInputRoutes(string forbiddenRoute)
    {
        var evidence = ParseValid();
        evidence["isolation"]!["inputRoute"] = forbiddenRoute;

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains(failures, failure => failure.Contains("inputRoute must be target-owned-deterministic-command-channel", StringComparison.Ordinal));
    }

    [Fact]
    public void RefusesCrossPairedBoundaryAndInputRoute()
    {
        var evidence = ParseValid();
        evidence["isolation"]!["boundary"] = "existing-separate-gui-session";

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains(failures, failure => failure.Contains("inputRoute must be session-scoped-hardware-input", StringComparison.Ordinal));
    }

    [Fact]
    public void RefusesProcessNativeEvidenceWithSeparateWriter()
    {
        var evidence = ParseValid(withWriter: true);
        evidence["isolation"]!["boundary"] = "process-native-recording";
        evidence["isolation"]!["inputRoute"] = "target-owned-deterministic-command-channel";

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains("process-native-recording must have zero separate capture writer processes", failures);
    }

    [Fact]
    public void RefusesDeclaredProcessCountThatContradictsInventory()
    {
        var evidence = ParseValid();
        evidence["limits"]!["captureWriterCount"] = 1;

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains("limits.captureWriterCount must equal capture.writerProcessIds count", failures);
    }

    [Fact]
    public void RefusesOverlappingTargetAndWriterInventories()
    {
        var evidence = ParseValid(withWriter: true);
        evidence["capture"]!["writerProcessIds"] = new JsonArray(1234);
        evidence["resources"]!["capture"]![0]!["processes"]![1]!["processId"] = 1234;
        evidence["cleanup"]!["exitedProcessIds"] = new JsonArray(1234);

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains("target.processIds and capture.writerProcessIds must be disjoint", failures);
    }

    [Fact]
    public void RefusesResourceInventoryMissingDeclaredProcess()
    {
        var evidence = ParseValid(withWriter: true);
        evidence["resources"]!["capture"]![0]!["processes"]!.AsArray().RemoveAt(1);

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains(failures, failure => failure.Contains("is missing declared process ID 5678", StringComparison.Ordinal));
    }

    [Fact]
    public void RefusesResourceInventoryWithUndeclaredProcess()
    {
        var evidence = ParseValid();
        evidence["resources"]!["capture"]![0]!["processes"]![0]!["processId"] = 9999;

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains(failures, failure => failure.Contains("contains undeclared process ID 9999", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("is missing declared process ID 1234", StringComparison.Ordinal));
    }

    [Fact]
    public void RefusesAttributableAggregateThatContradictsProcessInventory()
    {
        var evidence = ParseValid();
        evidence["resources"]!["capture"]![0]!["attributableCpuPercent"] = 24.0;

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains(failures, failure => failure.Contains("must equal the sum of its exact process inventory", StringComparison.Ordinal));
    }

    [Fact]
    public void RefusesCleanupInventoryThatDoesNotEqualAttributableUnion()
    {
        var evidence = ParseValid(withWriter: true);
        evidence["cleanup"]!["exitedProcessIds"] = new JsonArray(1234);

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains("cleanup.exitedProcessIds must exactly equal the declared target and capture-writer process union", failures);
    }

    [Fact]
    public void RefusesWriterExitClaimThatContradictsWriterInventory()
    {
        var evidence = ParseValid(withWriter: true);
        evidence["cleanup"]!["captureWriterExitObserved"] = false;

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains(failures, failure => failure.Contains("captureWriterExitObserved must be true", StringComparison.Ordinal));
    }

    [Fact]
    public void RefusesMissingResourceLimit()
    {
        var evidence = ParseValid();
        evidence["limits"]!.AsObject().Remove("priority");

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains(failures, failure => failure.Contains("priority must be a string", StringComparison.Ordinal));
    }

    [Fact]
    public void RefusesRawEvidenceInsideDerivedRepository()
    {
        var evidence = ParseValid();
        evidence["capture"]!["rawPath"] = Path.Combine(CaptureEvidenceValidator.RepositoryRoot, "research", "raw", "capture.mkv");

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains("capture.rawPath must remain outside the repository", failures);
    }

    [Fact]
    public void RefusesRawEvidenceUsingParentAliasInsideDerivedRepository()
    {
        var evidence = ParseValid();
        evidence["capture"]!["rawPath"] = Path.Combine(CaptureEvidenceValidator.RepositoryRoot, "tools", "..", "research", "raw", "capture.mkv");

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains("capture.rawPath must remain outside the repository", failures);
    }

    [Fact]
    public void RefusesExistingAndNonexistentRepositoryDescendantsAcrossWindowsFilesystemAliases()
    {
        var repository = CaptureEvidenceValidator.RepositoryRoot;
        var existing = Path.Combine(repository, "README.md");
        var nonexistent = Path.Combine(repository, "never-created-capture-directory", "capture.mkv");
        var aliases = new[]
        {
            existing,
            nonexistent,
            ToExtendedDosPath(existing),
            ToExtendedDosPath(nonexistent),
            ToDosDevicePath(existing),
            ToDosDevicePath(nonexistent),
            ToLocalAdminShare(existing, extended: false),
            ToLocalAdminShare(nonexistent, extended: false),
            ToLocalAdminShare(existing, extended: true),
            ToLocalAdminShare(nonexistent, extended: true),
            ToLocalDeviceUncShare(existing),
            ToLocalDeviceUncShare(nonexistent),
        };

        foreach (var alias in aliases)
        {
            var evidence = ParseValid();
            evidence["capture"]!["rawPath"] = alias;

            var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

            Assert.Contains("capture.rawPath must remain outside the repository", failures);
        }
    }

    [Theory]
    [InlineData(@"\\?\GLOBALROOT\Device\HarddiskVolumeShadowCopy1\capture.mkv", "non-filesystem or unrecognized")]
    [InlineData(@"\\.\PhysicalDrive0", "non-filesystem or unrecognized")]
    [InlineData(@"\\localhost\custom-share\capture.mkv", "UNC path cannot be proven")]
    public void RefusesNonFilesystemDeviceNamespacesWithoutStrippingPrefix(string rawPath, string expectedFailure)
    {
        var evidence = ParseValid();
        evidence["capture"]!["rawPath"] = rawPath;

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains(failures, failure => failure.Contains(expectedFailure, StringComparison.Ordinal));
    }

    [Fact]
    public void RefusesRawEvidenceWhoseExistingParentReparsePointResolvesInsideRepository()
    {
        var repository = CaptureEvidenceValidator.RepositoryRoot;
        var logicalAlias = Path.Combine(Path.GetPathRoot(repository)!, "external-rounds-alias");
        var evidence = ParseValid();
        evidence["capture"]!["rawPath"] = Path.Combine(logicalAlias, "research", "raw", "capture.mkv");
        string Resolver(string path)
        {
            var fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(logicalAlias, StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(repository, Path.GetRelativePath(logicalAlias, fullPath))
                : fullPath;
        }

        var failures = CaptureEvidenceValidator.ValidateForTests(evidence.ToJsonString(), repository, Resolver);

        Assert.Contains("capture.rawPath must remain outside the repository", failures);
    }

    [Fact]
    public void AcceptsResourceUsageExactlyAtDeclaredLimits()
    {
        var evidence = ParseValid();
        evidence["limits"]!["logicalProcessors"] = new JsonArray(0);
        evidence["limits"]!["maxGpuPercent"] = 20.0;
        evidence["limits"]!["privateMemoryBytes"] = 512L * 1024 * 1024;
        evidence["limits"]!["dedicatedGpuMemoryBytes"] = 512L * 1024 * 1024;
        evidence["resources"]!["capture"]![0]!["attributableCpuPercent"] = 100.0;
        evidence["resources"]!["capture"]![0]!["processes"]![0]!["logicalProcessors"] = new JsonArray(0);
        evidence["resources"]!["capture"]![0]!["processes"]![0]!["cpuPercent"] = 100.0;

        Assert.Empty(CaptureEvidenceValidator.Validate(evidence.ToJsonString()));
    }

    [Fact]
    public void RefusesProcessCpuBeyondItsOwnSingleProcessorAffinityEvenWhenAggregateMatches()
    {
        var evidence = ParseValid();
        evidence["resources"]!["capture"]![0]!["attributableCpuPercent"] = 150.0;
        evidence["resources"]!["capture"]![0]!["processes"]![0]!["logicalProcessors"] = new JsonArray(0);
        evidence["resources"]!["capture"]![0]!["processes"]![0]!["cpuPercent"] = 150.0;

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains(failures, failure => failure.Contains("cpuPercent must be between 0 and 100", StringComparison.Ordinal));
    }

    [Fact]
    public void AcceptsProcessCpuAtItsOwnSingleProcessorAffinityCapacity()
    {
        var evidence = ParseValid();
        evidence["resources"]!["capture"]![0]!["attributableCpuPercent"] = 100.0;
        evidence["resources"]!["capture"]![0]!["processes"]![0]!["logicalProcessors"] = new JsonArray(0);
        evidence["resources"]!["capture"]![0]!["processes"]![0]!["cpuPercent"] = 100.0;

        Assert.Empty(CaptureEvidenceValidator.Validate(evidence.ToJsonString()));
    }

    [Fact]
    public void AcceptsProcessCpuAtItsOwnTwoProcessorAffinityCapacity()
    {
        var evidence = ParseValid();
        evidence["resources"]!["capture"]![0]!["attributableCpuPercent"] = 200.0;
        evidence["resources"]!["capture"]![0]!["processes"]![0]!["cpuPercent"] = 200.0;

        Assert.Empty(CaptureEvidenceValidator.Validate(evidence.ToJsonString()));
    }

    [Fact]
    public void InvalidEmptyProcessAffinityDoesNotCreateCpuCapacity()
    {
        var evidence = ParseValid();
        evidence["resources"]!["capture"]![0]!["attributableCpuPercent"] = 1.0;
        evidence["resources"]!["capture"]![0]!["processes"]![0]!["logicalProcessors"] = new JsonArray();
        evidence["resources"]!["capture"]![0]!["processes"]![0]!["cpuPercent"] = 1.0;

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains(failures, failure => failure.Contains("logicalProcessors must contain between 1 and 2 entries", StringComparison.Ordinal));
        Assert.Contains(failures, failure => failure.Contains("cpuPercent must be between 0 and 0", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("logicalProcessors", "[2]", "subset of limits.logicalProcessors")]
    [InlineData("cpuPercent", "201.0", "cpuPercent must be between 0 and 200")]
    [InlineData("gpuPercent", "21.0", "gpuPercent must be between 0 and 20")]
    [InlineData("privateMemoryBytes", "536870913", "privateMemoryBytes must be between 0 and 536870912")]
    [InlineData("dedicatedGpuMemoryBytes", "536870913", "dedicatedGpuMemoryBytes must be between 0 and 536870912")]
    public void RefusesPerProcessUsageOutsideDeclaredLimits(string field, string replacementJson, string expectedFailure)
    {
        var evidence = ParseValid();
        evidence["limits"]!["maxGpuPercent"] = 20.0;
        evidence["limits"]!["privateMemoryBytes"] = 512L * 1024 * 1024;
        evidence["limits"]!["dedicatedGpuMemoryBytes"] = 512L * 1024 * 1024;
        evidence["resources"]!["capture"]![0]!["processes"]![0]![field] = JsonNode.Parse(replacementJson);

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains(failures, failure => failure.Contains(expectedFailure, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("attributableCpuPercent", "201.0", "attributableCpuPercent must be between 0 and 200")]
    [InlineData("attributableGpuPercent", "21.0", "attributableGpuPercent must be between 0 and 20")]
    [InlineData("attributablePrivateMemoryBytes", "536870913", "attributablePrivateMemoryBytes must be between 0 and 536870912")]
    [InlineData("attributableDedicatedGpuMemoryBytes", "536870913", "attributableDedicatedGpuMemoryBytes must be between 0 and 536870912")]
    public void RefusesAggregateUsageOutsideDeclaredLimits(string field, string replacementJson, string expectedFailure)
    {
        var evidence = ParseValid();
        evidence["limits"]!["maxGpuPercent"] = 20.0;
        evidence["limits"]!["privateMemoryBytes"] = 512L * 1024 * 1024;
        evidence["limits"]!["dedicatedGpuMemoryBytes"] = 512L * 1024 * 1024;
        evidence["resources"]!["capture"]![0]![field] = JsonNode.Parse(replacementJson);

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains(failures, failure => failure.Contains(expectedFailure, StringComparison.Ordinal));
    }

    [Fact]
    public void RefusesAggregateCpuBeyondOneDeclaredProcessorEvenWhenEachProcessFits()
    {
        var evidence = ParseValid(withWriter: true);
        evidence["limits"]!["logicalProcessors"] = new JsonArray(0);
        foreach (var process in evidence["resources"]!["capture"]![0]!["processes"]!.AsArray())
        {
            process!["logicalProcessors"] = new JsonArray(0);
            process["cpuPercent"] = 60.0;
        }
        evidence["resources"]!["capture"]![0]!["attributableCpuPercent"] = 120.0;

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains(failures, failure => failure.Contains("attributableCpuPercent must be between 0 and 100", StringComparison.Ordinal));
    }

    [Fact]
    public void RefusesRecursiveDuplicateJsonProperty()
    {
        var json = ValidEvidence().Replace(
            "\"target\":{",
            "\"target\":{\"buildId\":\"21020021\",",
            StringComparison.Ordinal);

        var failures = CaptureEvidenceValidator.Validate(json);

        Assert.Contains(failures, failure => failure.Contains("duplicate JSON property at $.target.buildId", StringComparison.Ordinal));
    }

    [Fact]
    public void RefusesHostHeartbeatRegressionEvenWhenCadenceIsValid()
    {
        var evidence = ParseValid();
        evidence["heartbeat"]!["captureDelaysMs"] = new JsonArray(
            Enumerable.Range(0, 50).Select(_ => (JsonNode?)JsonValue.Create(16.0)).ToArray());

        var failures = CaptureEvidenceValidator.Validate(evidence.ToJsonString());

        Assert.Contains(failures, failure => failure.Contains("capture p95 16.000 ms exceeds", StringComparison.Ordinal));
    }

    [Fact]
    public void RefusesDroppedOrDuplicatedFrames()
    {
        var dropped = ParseValid();
        dropped["capture"]!["droppedFrames"] = 1;
        var duplicated = ParseValid();
        duplicated["capture"]!["duplicatedFrames"] = 1;

        Assert.Contains(CaptureEvidenceValidator.Validate(dropped.ToJsonString()), failure => failure.Contains("droppedFrames must be 0", StringComparison.Ordinal));
        Assert.Contains(CaptureEvidenceValidator.Validate(duplicated.ToJsonString()), failure => failure.Contains("duplicatedFrames must be 0", StringComparison.Ordinal));
    }

    private static JsonNode ParseValid(bool withWriter = false) => JsonNode.Parse(ValidEvidence(withWriter))!;

    private static string ValidEvidence(bool withWriter = false)
    {
        var frameTimestamps = Enumerable.Range(0, 60).Select(index => index * (1000.0 / 60)).ToArray();
        var baselineDelays = Enumerable.Repeat(1.0, 500).ToArray();
        var captureDelays = Enumerable.Repeat(2.0, 50).ToArray();
        static object ProcessSample(int processId, string role) => new
        {
            processId,
            role,
            priority = "below-normal",
            logicalProcessors = new[] { 0, 1 },
            cpuPercent = 25.0,
            gpuPercent = 20.0,
            dedicatedGpuMemoryBytes = 512L * 1024 * 1024,
            privateMemoryBytes = 512L * 1024 * 1024,
        };
        static object ResourceSample(int timestampMs, object[] processes) => new
        {
            timestampMs,
            systemCpuPercent = 20.0,
            attributableCpuPercent = processes.Length * 25.0,
            systemGpuPercent = 30.0,
            attributableGpuPercent = processes.Length * 20.0,
            attributableDedicatedGpuMemoryBytes = processes.Length * 512L * 1024 * 1024,
            attributablePrivateMemoryBytes = processes.Length * 512L * 1024 * 1024,
            processes,
        };
        var captureProcesses = withWriter
            ? new[] { ProcessSample(1234, "target"), ProcessSample(5678, "capture-writer") }
            : new[] { ProcessSample(1234, "target") };
        return JsonSerializer.Serialize(new
        {
            format = 1,
            target = new
            {
                steamAppId = 1557740,
                buildId = "21020021",
                version = "v1.1.2.a75ee335a",
                executableSha256 = new string('a', 64),
                controlledState = "base-loadout-duel",
                loadout = new[] { "base-player-one", "base-player-two" },
                processIds = new[] { 1234 },
            },
            isolation = new
            {
                boundary = withWriter ? "existing-separate-gui-session" : "process-native-recording",
                inputRoute = withWriter ? "session-scoped-hardware-input" : "target-owned-deterministic-command-channel",
                foregroundActivated = false,
                physicalPointerMoved = false,
                globalKeyboardInput = false,
                globalMouseInput = false,
                globalControllerInput = false,
                unrelatedPixelsInspected = false,
                systemConfigurationChanged = false,
            },
            display = new
            {
                screenIndex = 3,
                device = @"\\.\DISPLAY4",
                placementVerifiedBeforeVisible = true,
                monitor = new { left = 364, top = -1080, right = 2284, bottom = 0 },
                window = new { left = 500, top = -900, right = 1780, bottom = -180 },
            },
            limits = new
            {
                priority = "below-normal",
                logicalProcessors = new[] { 0, 1 },
                privateMemoryBytes = 2L * 1024 * 1024 * 1024,
                dedicatedGpuMemoryBytes = 2L * 1024 * 1024 * 1024,
                maxGpuPercent = 70,
                timeoutSeconds = 30,
                targetProcessCount = 1,
                captureWriterCount = withWriter ? 1 : 0,
                automaticCleanup = true,
            },
            capture = new
            {
                writerProcessIds = withWriter ? new[] { 5678 } : Array.Empty<int>(),
                width = 1280,
                height = 720,
                framesPerSecond = 60,
                durationSeconds = 1.0,
                cadenceToleranceMs = 0.01,
                outputExistedBeforeCapture = false,
                refuseOverwrite = true,
                rawPath = Path.Combine(Path.GetTempPath(), "external-rounds-evidence", "capture.mkv"),
                rawSha256 = new string('b', 64),
                expectedFrames = 60,
                capturedFrames = 60,
                droppedFrames = 0,
                duplicatedFrames = 0,
                frameTimestampsMs = frameTimestamps,
                sourceCoordinates = new[] { new { label = "projectile-center", frame = 30, x = 640.0, y = 360.0 } },
            },
            heartbeat = new
            {
                periodMs = 20,
                baselineDurationSeconds = 10.0,
                captureDurationSeconds = 1.0,
                baselineDelaysMs = baselineDelays,
                captureDelaysMs = captureDelays,
            },
            resources = new
            {
                baseline = Enumerable.Range(0, 10).Select(index => ResourceSample(index * 1000, [])).ToArray(),
                capture = new[] { ResourceSample(0, captureProcesses) },
            },
            cleanup = new
            {
                targetExitObserved = true,
                captureWriterExitObserved = withWriter,
                exitedProcessIds = withWriter ? new[] { 1234, 5678 } : new[] { 1234 },
                residualProcessIds = Array.Empty<int>(),
            },
        });
    }

    private static void Replace(JsonNode root, string path, JsonNode? replacement)
    {
        var segments = path.Split('.');
        JsonNode current = root;
        for (var index = 0; index < segments.Length - 1; index++)
        {
            current = int.TryParse(segments[index], out var arrayIndex)
                ? current[arrayIndex]!
                : current[segments[index]]!;
        }
        if (int.TryParse(segments[^1], out var finalIndex)) current[finalIndex] = replacement;
        else current[segments[^1]] = replacement;
    }

    private static string ToExtendedDosPath(string path) => @"\\?\" + Path.GetFullPath(path);

    private static string ToDosDevicePath(string path) => @"\\.\" + Path.GetFullPath(path);

    private static string ToLocalAdminShare(string path, bool extended)
    {
        var fullPath = Path.GetFullPath(path);
        var drive = char.ToUpperInvariant(fullPath[0]);
        var suffix = fullPath[3..];
        var prefix = extended ? @"\\?\UNC\localhost\" : @"\\localhost\";
        return $"{prefix}{drive}$\\{suffix}";
    }

    private static string ToLocalDeviceUncShare(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var drive = char.ToUpperInvariant(fullPath[0]);
        return $@"\\.\UNC\localhost\{drive}$\{fullPath[3..]}";
    }
}
