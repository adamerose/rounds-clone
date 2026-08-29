using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class AgentPlaytestTests
{
    [Fact]
    public void RequestSchemaParsesExactlyAndConvertsToSemanticInputs()
    {
        var result = Parse("""{"protocol":"rounds-agent-playtest-v1","sequence":1,"intervalTicks":30,"players":[{"move":-1,"jump":true,"fire":false,"block":true,"aimX":0.25,"aimY":-1},{"move":1,"jump":false,"fire":true,"block":false,"aimX":0,"aimY":1}]}""");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(1, result.Request!.Sequence);
        Assert.Equal(30, result.Request.IntervalTicks);
        Assert.Equal(2, result.Request.Players.Count);
        var first = result.Request.Players[0].ToPlayerInput();
        Assert.Equal(-1, first.MoveAxis);
        Assert.True(first.JumpHeld);
        Assert.True(first.BlockHeld);
        Assert.Equal(0.25, first.AimDirection.X);
        Assert.Equal(-1.0, first.AimDirection.Y);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"protocol\":\"rounds-agent-playtest-v1\",\"sequence\":1,\"intervalTicks\":0,\"players\":[]}")]
    [InlineData("{\"protocol\":\"rounds-agent-playtest-v1\",\"sequence\":1,\"intervalTicks\":31,\"players\":[]}")]
    [InlineData("{\"protocol\":\"wrong\",\"sequence\":1,\"intervalTicks\":1,\"players\":[]}")]
    [InlineData("{\"protocol\":\"rounds-agent-playtest-v1\",\"sequence\":-1,\"intervalTicks\":1,\"players\":[]}")]
    [InlineData("{\"protocol\":\"rounds-agent-playtest-v1\",\"sequence\":1,\"intervalTicks\":1,\"players\":[{\"move\":2,\"jump\":false,\"fire\":false,\"block\":false,\"aimX\":0,\"aimY\":0},{\"move\":0,\"jump\":false,\"fire\":false,\"block\":false,\"aimX\":0,\"aimY\":0}]}")]
    [InlineData("{\"protocol\":\"rounds-agent-playtest-v1\",\"sequence\":1,\"intervalTicks\":1,\"players\":[{\"move\":0,\"jump\":false,\"fire\":false,\"block\":false,\"aimX\":1.1,\"aimY\":0},{\"move\":0,\"jump\":false,\"fire\":false,\"block\":false,\"aimX\":0,\"aimY\":0}]}")]
    [InlineData("{\"protocol\":\"rounds-agent-playtest-v1\",\"sequence\":1,\"intervalTicks\":1,\"players\":[{\"move\":0,\"jump\":false,\"fire\":false,\"block\":false,\"aimX\":0,\"aimY\":0,\"extra\":0},{\"move\":0,\"jump\":false,\"fire\":false,\"block\":false,\"aimX\":0,\"aimY\":0}]}")]
    public void ValidJsonWithInvalidSchemaFailsClosedAndEchoesIntegerSequence(string json)
    {
        var result = Parse(json);

        Assert.False(result.IsSuccess);
        Assert.Equal("request-validate", result.Error!.Stage);
        Assert.Equal("invalid-schema", result.Error.Code);
        int? expectedSequence = json == "{}" ? null : json.Contains("\"sequence\":-1", StringComparison.Ordinal) ? -1 : 1;
        Assert.Equal(expectedSequence, result.Error.Sequence);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("")]
    [InlineData("{}\n{}")]
    public void MalformedOrImproperlyFramedInputHasNullSequence(string json)
    {
        var bytes = json == "" ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(json + "\n");
        var result = AgentPlaytestNdjson.ParseRequest(bytes);

        Assert.Equal("request-parse", result.Error!.Stage);
        Assert.Equal("malformed-json", result.Error.Code);
        Assert.Null(result.Error.Sequence);
    }

    [Fact]
    public void FrameAndEveryAllowedErrorHaveExactOneLineSchemas()
    {
        var frame = new AgentPlaytestFrameResponse(3, @"C:\owned\frame-0003.png", new string('a', 64), 1280, 720, true);
        Assert.Equal(
            "{\"protocol\":\"rounds-agent-playtest-v1\",\"sequence\":3,\"status\":\"frame\",\"framePath\":\"C:\\\\owned\\\\frame-0003.png\",\"frameSha256\":\"" + new string('a', 64) + "\",\"width\":1280,\"height\":720,\"terminal\":true}\n",
            Encoding.UTF8.GetString(AgentPlaytestNdjson.SerializeResponse(frame)));

        foreach (var pair in AgentPlaytestErrors.Allowed)
        {
            var response = AgentPlaytestErrors.Create(null, pair.Stage, pair.Code);
            var json = Encoding.UTF8.GetString(AgentPlaytestNdjson.SerializeResponse(response));
            Assert.EndsWith("\n", json, StringComparison.Ordinal);
            Assert.DoesNotContain("framePath", json, StringComparison.Ordinal);
            using var document = JsonDocument.Parse(json);
            Assert.Equal(5, document.RootElement.EnumerateObject().Count());
            Assert.Equal(pair.Stage, document.RootElement.GetProperty("stage").GetString());
            Assert.Equal(pair.Code, document.RootElement.GetProperty("code").GetString());
        }
        Assert.Throws<ArgumentException>(() => AgentPlaytestErrors.Create(null, "other", "other"));
    }

    [Fact]
    public void SessionRejectsDuplicateSkippedTimeoutAndResourceRequestsBeforeStepping()
    {
        var session = new AgentPlaytestSession();
        var initialHash = Match.Hash(session.Match);

        AssertFailure(() => session.Apply(Request(2, 1), TimeSpan.Zero), "sequence", "invalid-sequence");
        Assert.Equal(initialHash, Match.Hash(session.Match));
        Assert.Equal(0, session.TickCount);
        AssertFailure(() => session.Apply(Request(1, 1), TimeSpan.FromSeconds(91)), "lifecycle", "timeout");
        Assert.Equal(0, session.TickCount);

        session.Apply(Request(1, 30), TimeSpan.Zero);
        AssertFailure(() => session.Apply(Request(1, 1), TimeSpan.Zero), "sequence", "invalid-sequence");
        Assert.Equal(30, session.TickCount);

        var bounded = new AgentPlaytestSession();
        for (var sequence = 1; sequence <= AgentPlaytestLimits.MaximumRequests; sequence++)
        {
            bounded.Apply(Request(sequence, 30), TimeSpan.Zero);
        }
        Assert.Equal(3_600, bounded.TickCount);
        AssertFailure(
            () => bounded.Apply(Request(121, 1), TimeSpan.Zero),
            "resource",
            "resource-limit-exceeded");
    }

    [Fact]
    public void HeldActionsAdvanceEveryTickAndFreshShellReplayMatchesEveryHash()
    {
        var session = new AgentPlaytestSession();
        session.Apply(Request(1, 1), TimeSpan.Zero);
        session.Apply(Request(2, 1, jumpPlayer: 0), TimeSpan.Zero);
        session.Apply(Request(3, 1), TimeSpan.Zero);
        session.Apply(Request(4, 1, jumpPlayer: 1), TimeSpan.Zero);
        session.Apply(Request(5, 30), TimeSpan.Zero);
        session.Apply(Request(6, 30), TimeSpan.Zero);
        var interval = session.Apply(Request(7, 30, firePlayer: 0, blockPlayer: 1, aimX: 1), TimeSpan.Zero);

        Assert.Equal(30, interval.TickHashes.Count);
        Assert.Equal(94, session.TickCount);
        Assert.True(session.Match.World.Players[1].WasBlockHeld);
        Assert.All(session.Accepted, accepted =>
        {
            Assert.InRange(accepted.RequestedIntervalTicks, 1, 30);
            Assert.InRange(accepted.AcceptedIntervalTicks, 1, accepted.RequestedIntervalTicks);
            Assert.Equal(accepted.AcceptedIntervalTicks, accepted.TickHashes.Count);
        });
        AgentPlaytestSession.VerifyFreshReplay(session.Accepted);

        var corrupted = session.Accepted.ToArray();
        var last = corrupted[^1];
        var badHashes = last.TickHashes.ToArray();
        badHashes[^1] ^= 1;
        corrupted[^1] = last with { TickHashes = badHashes };
        AssertFailure(() => AgentPlaytestSession.VerifyFreshReplay(corrupted), "replay", "replay-mismatch");
    }

    [Fact]
    public void NonHumanStructuralSemanticActionsReachExactlyTheSupportedBoundaryWithoutMutation()
    {
        var session = CreateStructuralBoundarySession();

        Assert.True(session.IsTerminal);
        Assert.Equal(MatchPhase.LoserDraft, session.Match.Phase);
        Assert.NotEqual(MatchPhase.MatchResult, session.Match.Phase);
        Assert.InRange(session.Match.CurrentPickerId, 0, 1);
        Assert.Single(session.Match.AcquiredCardsFor(0));
        Assert.Single(session.Match.AcquiredCardsFor(1));
        Assert.InRange(session.TickCount, 1, AgentPlaytestLimits.MaximumSimulationTicks);
        Assert.Equal(496, session.TickCount);
        Assert.Equal(496, session.Accepted.Sum(static interval => interval.AcceptedIntervalTicks));
        Assert.Equal(496, session.Accepted.SelectMany(static interval => interval.TickHashes).Count());
        Assert.True(session.Accepted[^1].AcceptedIntervalTicks < session.Accepted[^1].RequestedIntervalTicks);
        Assert.Equal(session.Accepted[^1].AcceptedIntervalTicks, session.Accepted[^1].TickHashes.Distinct().Count());
        AgentPlaytestSession.VerifyFreshReplay(session.Accepted);
        var firstTrace = new AgentPlaytestTraceRecorder(1);
        var secondTrace = new AgentPlaytestTraceRecorder(1);
        foreach (var interval in session.Accepted)
        {
            firstTrace.Record(interval);
            secondTrace.Record(interval);
        }
        Assert.Equal(firstTrace.ToCanonicalBytes(requireTerminal: true), secondTrace.ToCanonicalBytes(requireTerminal: true));
        AssertFailure(
            () => session.Apply(Request(session.Sequence + 1, 1), TimeSpan.Zero),
            "terminal",
            "invalid-terminal");
    }

    [Fact]
    public void ProtocolLoopUsesOnlyNdjsonStreamsAndPublishesVerifiedTerminalArtifacts()
    {
        var source = CreateStructuralBoundarySession();
        var requests = source.Accepted.Select(interval => AgentPlaytestNdjson.SerializeRequest(new AgentPlaytestRequest(
                AgentPlaytestLimits.Protocol,
                interval.Sequence,
                interval.RequestedIntervalTicks,
                interval.Players))).ToArray();
        var transport = new TurnwiseTransport(requests);
        var root = Path.Combine(Path.GetTempPath(), "rounds-agent-playtest-loop-" + Guid.NewGuid().ToString("N"));
        var owner = AgentPlaytestArtifactOwner.Create(root);
        var loop = new AgentPlaytestProtocolLoop(
            owner,
            new FakeFrameSource(),
            new FixedDecoder(),
            static () => TimeSpan.Zero,
            AgentPlaytestManifestContext.TestOnlySynthetic);

        var exitCode = loop.Run(transport);

        Assert.Equal(0, exitCode);
        Assert.Empty(transport.Diagnostics);
        Assert.Equal(source.Accepted.Count + 1, transport.Responses.Count);
        Assert.All(transport.Responses, static response => Assert.IsType<AgentPlaytestFrameResponse>(response));
        Assert.True(Assert.IsType<AgentPlaytestFrameResponse>(transport.Responses[^1]).Terminal);
        Assert.Equal(requests.Length, transport.ReadCount);
        Assert.True(File.Exists(Path.Combine(root, "trace.jsonl")));
        Assert.True(File.Exists(Path.Combine(root, "manifest.json")));
        Assert.False(File.Exists(Path.Combine(root, "trace.jsonl.partial")));
        var storedTrace = File.ReadAllBytes(Path.Combine(root, "trace.jsonl"));
        var parsedTrace = AgentPlaytestTraceCodec.ParseCanonical(storedTrace, requireTerminal: true);
        AgentPlaytestSession.VerifyFreshReplay(parsedTrace);
        var manifestBytes = File.ReadAllBytes(Path.Combine(root, "manifest.json"));
        AgentPlaytestManifestCodec.ValidateCanonical(manifestBytes);
        using (var manifest = JsonDocument.Parse(manifestBytes))
        {
            Assert.Equal("test-only-non-evidence", manifest.RootElement.GetProperty("status").GetString());
            Assert.False(manifest.RootElement.GetProperty("complete").GetBoolean());
            Assert.Equal("synthetic-unit-test-build", manifest.RootElement.GetProperty("buildIdentity").GetString());
            Assert.Equal(source.Accepted.Count + 1, manifest.RootElement.GetProperty("frameHashes").GetArrayLength());
            Assert.Equal(source.Accepted.Count, manifest.RootElement.GetProperty("acceptedActions").GetArrayLength());
            Assert.Equal(496, manifest.RootElement.GetProperty("tickHashCoverage").EnumerateArray()
                .Sum(static item => item.GetProperty("hashes").GetArrayLength()));
            Assert.Empty(manifest.RootElement.GetProperty("resourceSamples").EnumerateArray());
        }
        owner.Dispose();
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void GenericStandardInputProcessesPreloadedRecordsWithoutClaimingAdaptiveEvidence()
    {
        var source = CreateStructuralBoundarySession();
        var requestBytes = source.Accepted.SelectMany(interval => AgentPlaytestNdjson.SerializeRequest(
            new AgentPlaytestRequest(
                AgentPlaytestLimits.Protocol,
                interval.Sequence,
                interval.RequestedIntervalTicks,
                interval.Players))).ToArray();
        using var input = new MemoryStream(requestBytes);
        using var output = new MemoryStream();
        using var diagnostics = new StringWriter();
        var root = Path.Combine(Path.GetTempPath(), "rounds-agent-playtest-preloaded-" + Guid.NewGuid().ToString("N"));
        var owner = AgentPlaytestArtifactOwner.Create(root);
        var loop = new AgentPlaytestProtocolLoop(
            owner,
            new FakeFrameSource(),
            new FixedDecoder(),
            static () => TimeSpan.Zero,
            AgentPlaytestManifestContext.TestOnlySynthetic);

        var exitCode = loop.Run(input, output, diagnostics);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, diagnostics.ToString());
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "manifest.json")));
        Assert.False(manifest.RootElement.GetProperty("complete").GetBoolean());
        Assert.Empty(manifest.RootElement.GetProperty("causalityReceipts").EnumerateArray());
        owner.Dispose();
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void IncompleteOpenStandardInputTimesOutWithoutWaitingForEofOrNinetyRealSeconds()
    {
        using var input = new NonSeekableReadStream(new MemoryStream());
        using var output = new MemoryStream();
        using var diagnostics = new StringWriter();
        var frames = new FakeFrameSource();
        var root = Path.Combine(Path.GetTempPath(), "rounds-agent-playtest-timeout-" + Guid.NewGuid().ToString("N"));
        using var owner = AgentPlaytestArtifactOwner.Create(root);
        var reader = new PartialThenTimeoutReader(Encoding.UTF8.GetBytes("{\"protocol\":"));
        var loop = new AgentPlaytestProtocolLoop(
            owner,
            frames,
            new FixedDecoder(),
            static () => TimeSpan.Zero,
            AgentPlaytestManifestContext.ProductionRendererUnavailable);

        var exitCode = loop.Run(new AgentPlaytestStandardStreamTransport(
            input,
            output,
            diagnostics,
            reader));

        Assert.Equal(1, exitCode);
        Assert.False(Directory.Exists(root));
        Assert.Equal(new[] { 0 }, frames.CapturedSequences);
        Assert.True(reader.TimeoutObserved);
        Assert.All(reader.ObservedTimeouts, timeout => Assert.InRange(timeout.TotalSeconds, 89, 90));
        var lines = Encoding.UTF8.GetString(output.ToArray()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        using var error = JsonDocument.Parse(lines[^1]);
        Assert.Equal("lifecycle", error.RootElement.GetProperty("stage").GetString());
        Assert.Equal("timeout", error.RootElement.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, error.RootElement.GetProperty("sequence").ValueKind);
        Assert.Contains("deadline", diagnostics.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void OwnerSupervisorPreservesFinalizedFrameChooseWriteReadCausality()
    {
        var events = new List<string>();
        var channel = new CausalOwnerChannel(events,
            Frame(0, terminal: false),
            Frame(1, terminal: false),
            Frame(2, terminal: true));
        var verifier = new RecordingVerifier(events);
        var driver = new RecordingDriver(events);
        var supervisor = new AgentPlaytestOwnerSupervisor(channel, verifier, new FixedDecoder(), driver);

        var proof = supervisor.RunToTerminal();
        var receipts = proof.SnapshotReceipts();

        Assert.Equal(2, driver.ChooseCount);
        Assert.Equal(3, driver.ObserveCount);
        Assert.Equal(2, channel.WrittenRequests.Count);
        Assert.Same(driver.ReturnedRequests[0], channel.WrittenRequests[0]);
        Assert.Same(driver.ReturnedRequests[1], channel.WrittenRequests[1]);
        Assert.Equal(new[]
        {
            "read:0", "verify:0", "observe:0", "choose:0", "write:1", "read:1",
            "verify:1", "observe:1", "choose:1", "write:2", "read:2", "verify:2", "observe:2",
        }, events);
        Assert.Equal(2, receipts.Count);
        Assert.Equal(0, receipts[0].PriorFrameSequence);
        Assert.Equal(1, receipts[0].RequestSequence);
        Assert.Equal(Frame(0, false).FrameSha256, receipts[0].PriorFrameSha256);
        Assert.Equal(64, receipts[0].ActionIdentity.Length);
        Assert.Empty(typeof(AgentPlaytestOwnerSupervisor.CausalCompletionProof).GetConstructors());
        Assert.Throws<InvalidOperationException>(() =>
            new AgentPlaytestOwnerSupervisor.CausalCompletionProof(
                receipts,
                2,
                Frame(2, true).FrameSha256,
                false,
                new object()));
    }

    [Fact]
    public void ProtocolLoopReportsRendererUnavailableOnStdoutAndDiagnosticsOnStderrThenCleans()
    {
        using var input = new MemoryStream();
        using var output = new MemoryStream();
        using var diagnostics = new StringWriter();
        var root = Path.Combine(Path.GetTempPath(), "rounds-agent-playtest-unavailable-" + Guid.NewGuid().ToString("N"));
        using var owner = AgentPlaytestArtifactOwner.Create(root);
        var loop = new AgentPlaytestProtocolLoop(
            owner,
            new AgentPlaytestRendererUnavailableFrameSource(),
            new FixedDecoder(),
            static () => TimeSpan.Zero,
            AgentPlaytestManifestContext.ProductionRendererUnavailable);

        var exitCode = loop.Run(input, output, diagnostics);

        Assert.Equal(1, exitCode);
        Assert.False(Directory.Exists(root));
        Assert.Contains("No renderer-backed frame source", diagnostics.ToString(), StringComparison.Ordinal);
        Assert.Equal(
            "{\"protocol\":\"rounds-agent-playtest-v1\",\"sequence\":0,\"status\":\"error\",\"stage\":\"renderer\",\"code\":\"renderer-unavailable\"}\n",
            Encoding.UTF8.GetString(output.ToArray()));
    }

    [Fact]
    public void DirectRequestsCannotBypassProtocolActionValidation()
    {
        var invalidPlayers = new[]
        {
            new AgentPlaytestPlayerAction(0, false, false, false, double.NaN, 0),
            new AgentPlaytestPlayerAction(0, false, false, false, 0, 0),
        };
        var session = new AgentPlaytestSession();

        AssertFailure(
            () => session.Apply(
                new AgentPlaytestRequest(AgentPlaytestLimits.Protocol, 1, 1, invalidPlayers),
                TimeSpan.Zero),
            "request-validate",
            "invalid-schema");
        Assert.Equal(0, session.TickCount);
    }

    [Fact]
    public void HumanObservationIsCopiedAndCannotExposeTraceMetadata()
    {
        var mutable = new byte[] { 255, 0, 0, 255, 0, 0, 255, 255 };
        var observation = new HumanPlaytestObservation(7, mutable, 2, 1);
        mutable[0] = 0;

        Assert.Equal(255, observation.PixelBytes[0]);
        Assert.Equal(8, observation.PixelBytes.Length);
        var properties = typeof(HumanPlaytestObservation).GetProperties().Select(static property => property.Name).ToArray();
        Assert.Equal(new[] { "Sequence", "Width", "Height", "PixelBytes" }, properties);
        Assert.DoesNotContain(properties, static property => property.Contains("Tick", StringComparison.Ordinal));
        Assert.DoesNotContain(properties, static property => property.Contains("Hash", StringComparison.Ordinal));
        Assert.DoesNotContain(properties, static property => property.Contains("Path", StringComparison.Ordinal));

        var structural = new NonHumanStructuralObservation(1, 7, 42, 30, new ulong[] { 9 }, false);
        Assert.Equal("non-human-structural", structural.TraceLabel);
    }

    [Fact]
    public void AdaptiveDriverChangesLegalActionForCounterfactualVisiblePixels()
    {
        var driver = new DeterministicAdaptivePlaytestDriver();
        var red = new HumanPlaytestObservation(4, new byte[] { 255, 0, 0, 255 }, 1, 1);
        var blue = new HumanPlaytestObservation(4, new byte[] { 0, 0, 255, 255 }, 1, 1);

        var first = driver.Choose(red);
        var counterfactual = driver.Choose(blue);

        Assert.Equal(5, first.Sequence);
        Assert.Equal(30, first.IntervalTicks);
        Assert.Equal(1.0, first.Players[0].AimX);
        Assert.Equal(-1.0, counterfactual.Players[0].AimX);
        Assert.All(first.Players, AssertLegal);
        Assert.All(counterfactual.Players, AssertLegal);
    }

    [Fact]
    public void ArtifactOwnerPublishesFinalFrameBeforeResponseAndCleansFailedRunExactly()
    {
        var root = Path.Combine(Path.GetTempPath(), "rounds-agent-playtest-" + Guid.NewGuid().ToString("N"));
        var sibling = root + "-sibling";
        Directory.CreateDirectory(sibling);
        var decoder = new FixedDecoder();
        var encoded = Encoding.ASCII.GetBytes("not-a-real-png-unit-boundary");
        var owner = AgentPlaytestArtifactOwner.Create(root);
        try
        {
            var published = owner.PublishFrame(0, encoded, decoder, terminal: false);
            Assert.True(File.Exists(published.Response.FramePath));
            Assert.False(File.Exists(published.Response.FramePath + ".partial"));
            Assert.Equal(Convert.ToHexString(SHA256.HashData(encoded)).ToLowerInvariant(), published.Response.FrameSha256);
            Assert.Equal(encoded, File.ReadAllBytes(published.Response.FramePath));
            Assert.Equal(8, published.Observation.PixelBytes.Length);
            Assert.Throws<AgentPlaytestFailure>(() => owner.PublishFrame(0, encoded, decoder, terminal: false));

            owner.CleanupFailedRun();
            Assert.False(Directory.Exists(root));
            Assert.True(Directory.Exists(sibling));
        }
        finally
        {
            owner.CleanupFailedRun();
            Directory.Delete(sibling);
        }
    }

    [Fact]
    public void ArtifactOwnerPreflightsOutputCapBeforeWritingFrameBytes()
    {
        var root = Path.Combine(Path.GetTempPath(), "rounds-agent-playtest-cap-" + Guid.NewGuid().ToString("N"));
        var owner = AgentPlaytestArtifactOwner.Create(root, maximumOutputBytes: 8);

        AssertFailure(
            () => owner.PublishFrame(0, new byte[9], new FixedDecoder(), terminal: false),
            "resource",
            "resource-limit-exceeded");

        Assert.False(File.Exists(Path.Combine(root, "frame-0000.png.partial")));
        Assert.False(File.Exists(Path.Combine(root, "frame-0000.png")));
        Assert.True(Directory.EnumerateFiles(root).Sum(static path => new FileInfo(path).Length) <= 8);
        owner.CleanupFailedRun();
        owner.Dispose();
    }

    [Fact]
    public void StrictTraceParserRejectsTamperingAndNoncanonicalBytes()
    {
        var session = CreateStructuralBoundarySession();
        var recorder = new AgentPlaytestTraceRecorder(1);
        foreach (var interval in session.Accepted)
        {
            recorder.Record(interval);
        }
        var canonical = recorder.ToCanonicalBytes(requireTerminal: true);
        var parsed = AgentPlaytestTraceCodec.ParseCanonical(canonical, requireTerminal: true);
        AgentPlaytestSession.VerifyFreshReplay(parsed);

        var text = Encoding.UTF8.GetString(canonical);
        var noncanonical = Encoding.UTF8.GetBytes(text.Replace(",\"sequence\"", ", \"sequence\"", StringComparison.Ordinal));
        AssertFailure(() => AgentPlaytestTraceCodec.ParseCanonical(noncanonical, true), "replay", "replay-mismatch");
        var firstHash = parsed[0].TickHashes[0].ToString("x16");
        var tampered = Encoding.UTF8.GetBytes(text.Replace(firstHash, new string('0', 16), StringComparison.Ordinal));
        AssertFailure(() => AgentPlaytestSession.VerifyFreshReplay(
            AgentPlaytestTraceCodec.ParseCanonical(tampered, true)), "replay", "replay-mismatch");
    }

    [Fact]
    public void AtomicRootAcquisitionNeverDeletesPreexistingOrRacedContent()
    {
        var preexisting = Path.Combine(Path.GetTempPath(), "rounds-agent-playtest-preexisting-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(preexisting);
        var sentinel = Path.Combine(preexisting, "sentinel.txt");
        File.WriteAllText(sentinel, "owned elsewhere");
        Assert.Throws<ArgumentException>(() => AgentPlaytestArtifactOwner.Create(preexisting));
        Assert.Equal("owned elsewhere", File.ReadAllText(sentinel));
        Directory.Delete(preexisting, recursive: true);

        var raced = Path.Combine(Path.GetTempPath(), "rounds-agent-playtest-raced-" + Guid.NewGuid().ToString("N"));
        Assert.ThrowsAny<IOException>(() => AgentPlaytestArtifactOwner.Create(raced, new AdversarialRootAcquirer()));
        Assert.Equal("adversarial", File.ReadAllText(Path.Combine(raced, "sentinel.txt")));
        Directory.Delete(raced, recursive: true);

        var locked = Path.Combine(Path.GetTempPath(), "rounds-agent-playtest-locked-" + Guid.NewGuid().ToString("N"));
        var lockPath = locked + ".rounds-agent-playtest-owner";
        File.WriteAllText(lockPath, "foreign lock");
        Assert.ThrowsAny<IOException>(() => AgentPlaytestArtifactOwner.Create(locked));
        Assert.False(Directory.Exists(locked));
        Assert.Equal("foreign lock", File.ReadAllText(lockPath));
        File.Delete(lockPath);
    }

    [Fact]
    public void AtomicRootAcquisitionNeverDeletesReplacementAfterParentIdentityChanges()
    {
        var root = Path.Combine(Path.GetTempPath(), "rounds-agent-playtest-parent-race-" + Guid.NewGuid().ToString("N"));
        var binder = new ReplacingParentBinder(root);
        var acquirer = new AtomicWindowsAgentPlaytestRootAcquirer(binder);

        Assert.ThrowsAny<IOException>(() => AgentPlaytestArtifactOwner.Create(root, acquirer));

        Assert.True(binder.Lease!.Disposed);
        Assert.True(binder.Lease.ReplacementInstalled);
        Assert.Equal("foreign replacement", File.ReadAllText(Path.Combine(root, "sentinel.txt")));
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void FailedRunKeepsSiblingOwnershipLockHeldThroughRecursiveDeletion()
    {
        var root = Path.Combine(Path.GetTempPath(), "rounds-agent-playtest-cleanup-race-" + Guid.NewGuid().ToString("N"));
        var acquirer = new CleanupRaceAcquirer();
        var owner = AgentPlaytestArtifactOwner.Create(root, acquirer);

        owner.CleanupFailedRun();

        Assert.False(Directory.Exists(root));
        Assert.True(acquirer.Lease!.DeleteCalledWhileLockHeld);
        Assert.False(acquirer.Lease.ForeignContentAdmitted);
        Assert.False(acquirer.Lease.SiblingLockHeld);
    }

    [Fact]
    public void CleanupRetriesWhileLeaseHeldThenEmitsOnlyOriginalFailure()
    {
        var root = Path.Combine(Path.GetTempPath(), "rounds-agent-playtest-cleanup-retry-" + Guid.NewGuid().ToString("N"));
        var acquirer = new RetryCleanupAcquirer(failuresBeforeSuccess: 1);
        var owner = AgentPlaytestArtifactOwner.Create(root, acquirer);
        var transport = new TurnwiseTransport(Array.Empty<byte[]>());
        var loop = new AgentPlaytestProtocolLoop(
            owner,
            new AgentPlaytestRendererUnavailableFrameSource(),
            new FixedDecoder(),
            static () => TimeSpan.Zero,
            AgentPlaytestManifestContext.ProductionRendererUnavailable);

        Assert.Equal(1, loop.Run(transport));

        var error = Assert.Single(transport.Responses);
        var renderer = Assert.IsType<AgentPlaytestErrorResponse>(error);
        Assert.Equal("renderer", renderer.Stage);
        Assert.Equal("renderer-unavailable", renderer.Code);
        Assert.Equal(2, acquirer.Lease!.Attempts);
        Assert.True(acquirer.Lease.HeldDuringEveryAttempt);
        Assert.False(acquirer.Lease.SiblingLockHeld);
        Assert.False(Directory.Exists(root));
        owner.Dispose();
    }

    [Fact]
    public void PermanentCleanupFailureSuppressesOriginalAndEmitsOneCleanupFailedResponse()
    {
        var root = Path.Combine(Path.GetTempPath(), "rounds-agent-playtest-cleanup-permanent-" + Guid.NewGuid().ToString("N"));
        var acquirer = new RetryCleanupAcquirer(failuresBeforeSuccess: int.MaxValue);
        var owner = AgentPlaytestArtifactOwner.Create(root, acquirer);
        var transport = new TurnwiseTransport(Array.Empty<byte[]>());
        var loop = new AgentPlaytestProtocolLoop(
            owner,
            new ThrowingFrameSource(),
            new FixedDecoder(),
            static () => TimeSpan.Zero,
            AgentPlaytestManifestContext.ProductionRendererUnavailable);

        Assert.Equal(1, loop.Run(transport));

        var error = Assert.IsType<AgentPlaytestErrorResponse>(Assert.Single(transport.Responses));
        Assert.Equal("lifecycle", error.Stage);
        Assert.Equal("cleanup-failed", error.Code);
        Assert.DoesNotContain(transport.Responses.OfType<AgentPlaytestErrorResponse>(),
            static response => response.Code == "simulation-failed");
        Assert.Equal(3, acquirer.Lease!.Attempts);
        Assert.True(acquirer.Lease.HeldDuringEveryAttempt);
        Assert.True(acquirer.Lease.SiblingLockHeld);
        Assert.True(Directory.Exists(root));
        Assert.Single(transport.Diagnostics);
        Assert.Contains("Cleanup failed", transport.Diagnostics[0], StringComparison.Ordinal);

        owner.Dispose();
        Assert.False(acquirer.Lease.SiblingLockHeld);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void ManifestValidationRejectsSyntheticCompletionAndDeclaredLimitDrift()
    {
        var session = CreateStructuralBoundarySession();
        var frames = Enumerable.Range(0, session.Accepted.Count + 1)
            .Select(sequence => new AgentPlaytestFrameResponse(
                sequence,
                Path.GetFullPath($"frame-{sequence:0000}.png"),
                Convert.ToHexString(SHA256.HashData(BitConverter.GetBytes(sequence))).ToLowerInvariant(),
                2,
                1,
                sequence == session.Accepted.Count))
            .ToArray();
        var recorder = new AgentPlaytestTraceRecorder(1);
        foreach (var interval in session.Accepted)
        {
            recorder.Record(interval);
        }
        var trace = recorder.ToCanonicalBytes(true);
        var manifest = AgentPlaytestManifest.Create(
            AgentPlaytestManifestContext.TestOnlySynthetic,
            frames,
            session.Accepted,
            trace);
        var canonical = AgentPlaytestManifestCodec.ToCanonicalBytes(manifest);
        AgentPlaytestManifestCodec.ValidateCanonical(canonical);
        var text = Encoding.UTF8.GetString(canonical);
        var falseCompletion = Encoding.UTF8.GetBytes(text.Replace("\"complete\":false", "\"complete\":true", StringComparison.Ordinal));
        AssertFailure(() => AgentPlaytestManifestCodec.ValidateCanonical(falseCompletion), "replay", "replay-mismatch");
        var limitDrift = Encoding.UTF8.GetBytes(text.Replace("\"maximumRequests\":120", "\"maximumRequests\":121", StringComparison.Ordinal));
        AssertFailure(() => AgentPlaytestManifestCodec.ValidateCanonical(limitDrift), "replay", "replay-mismatch");
    }

    [Fact]
    public void SyntheticAndCallerAuthoredEvidenceCannotCompleteWithoutOpaqueProductionProof()
    {
        var session = CreateStructuralBoundarySession();
        var frames = Enumerable.Range(0, session.Accepted.Count + 1)
            .Select(sequence => Frame(sequence, sequence == session.Accepted.Count))
            .ToArray();
        var events = new List<string>();
        var supervisor = new AgentPlaytestOwnerSupervisor(
            new CausalOwnerChannel(events, frames),
            new RecordingVerifier(events),
            new FixedDecoder(),
            new ExactActionDriver(session.Accepted));
        var proof = supervisor.RunToTerminal();
        var sample = new AgentPlaytestResourceSample(
            2, 1, 1, 0.1, 1, 2, 1, 1, 1, 1, 3, true);
        var eligible = new AgentPlaytestManifestContext(
            "renderer-evidence",
            "test-renderer-build",
            new[] { sample },
            true,
            true,
            true);
        var trace = TraceBytes(session);

        var withoutProof = AgentPlaytestManifest.Create(eligible, frames, session.Accepted, trace);
        Assert.False(withoutProof.Complete);
        Assert.Empty(withoutProof.CausalityReceipts);
        Assert.DoesNotContain(
            typeof(AgentPlaytestManifestContext).GetProperties(),
            static property => property.Name.Contains("Causal", StringComparison.Ordinal));

        AssertFailure(
            () => AgentPlaytestManifest.Create(eligible, frames, session.Accepted, trace, proof),
            "replay",
            "replay-mismatch");

        var forged = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(AgentPlaytestManifestCodec.ToCanonicalBytes(withoutProof))
            .Replace("\"complete\":false", "\"complete\":true", StringComparison.Ordinal));
        AssertFailure(() => AgentPlaytestManifestCodec.ValidateCanonical(forged), "replay", "replay-mismatch");
        AssertFailure(
            () => AgentPlaytestManifest.Create(
                AgentPlaytestManifestContext.TestOnlySynthetic,
                frames,
                session.Accepted,
                trace,
                proof),
            "replay",
            "replay-mismatch");
    }

    [Fact]
    public void RequiredOwnerConfigurationRejectsEveryMissingHostGate()
    {
        var required = AgentPlaytestOwnerConfiguration.Required;
        Assert.True(required.IsRendererEvidenceEligible());
        Assert.False((required with { BelowNormalPriority = false }).IsRendererEvidenceEligible());
        Assert.False((required with { LogicalProcessors = 3 }).IsRendererEvidenceEligible());
        Assert.False((required with { OwnerTimeoutSeconds = 96 }).IsRendererEvidenceEligible());
        Assert.False((required with { HeartbeatGateEnabled = false }).IsRendererEvidenceEligible());
        Assert.False((required with { ExactProcessTreeCleanupEnabled = false }).IsRendererEvidenceEligible());
        Assert.Equal(AgentPlaytestLimits.LiveBulletCap, CombatTuning.Vanilla.LiveBulletCap);
    }

    [Fact]
    public void RendererResourceSamplesEnforcePlacementCadenceMemoryGpuAndHeartbeatGates()
    {
        var configuration = AgentPlaytestOwnerConfiguration.Required;
        var valid = new AgentPlaytestResourceSample(
            2,
            AgentPlaytestLimits.MaximumPrivateMemoryBytes,
            AgentPlaytestLimits.MaximumDedicatedGpuMemoryBytes,
            AgentPlaytestLimits.MaximumGpuUtilization,
            10,
            1280,
            720,
            8,
            13,
            50,
            3,
            true);

        Assert.True(AgentPlaytestResourceGate.AcceptsRendererEvidence(configuration, new[] { valid }));
        Assert.False(AgentPlaytestResourceGate.AcceptsRendererEvidence(configuration, Array.Empty<AgentPlaytestResourceSample>()));
        Assert.False(AgentPlaytestResourceGate.AcceptsRendererEvidence(configuration, new[] { valid with { ProcessCount = 3 } }));
        Assert.False(AgentPlaytestResourceGate.AcceptsRendererEvidence(configuration, new[] { valid with { PrivateMemoryBytes = AgentPlaytestLimits.MaximumPrivateMemoryBytes + 1 } }));
        Assert.False(AgentPlaytestResourceGate.AcceptsRendererEvidence(configuration, new[] { valid with { DedicatedGpuMemoryBytes = AgentPlaytestLimits.MaximumDedicatedGpuMemoryBytes + 1 } }));
        Assert.False(AgentPlaytestResourceGate.AcceptsRendererEvidence(configuration, new[] { valid with { TotalGpuUtilization = 0.71 } }));
        Assert.False(AgentPlaytestResourceGate.AcceptsRendererEvidence(configuration, new[] { valid with { FramesInPreviousSecond = 11 } }));
        Assert.False(AgentPlaytestResourceGate.AcceptsRendererEvidence(configuration, new[] { valid with { Width = 1281 } }));
        Assert.False(AgentPlaytestResourceGate.AcceptsRendererEvidence(configuration, new[] { valid with { HeartbeatP95Milliseconds = 13.1 } }));
        Assert.False(AgentPlaytestResourceGate.AcceptsRendererEvidence(configuration, new[] { valid with { HeartbeatP95Milliseconds = -0.1 } }));
        Assert.False(AgentPlaytestResourceGate.AcceptsRendererEvidence(configuration, new[] { valid with { MaximumHeartbeatDelayMilliseconds = 50.1 } }));
        Assert.False(AgentPlaytestResourceGate.AcceptsRendererEvidence(configuration, new[] { valid with { MaximumHeartbeatDelayMilliseconds = -0.1 } }));
        Assert.False(AgentPlaytestResourceGate.AcceptsRendererEvidence(configuration, new[] { valid with { WindowScreen = 2 } }));
        Assert.False(AgentPlaytestResourceGate.AcceptsRendererEvidence(configuration, new[] { valid with { WindowNonActivating = false } }));
    }

    [Fact]
    public void AgentPlaytestSourceHasNoOperatingSystemInputNetworkOrPipeFallback()
    {
        var root = FindRepositoryRoot();
        var sources = Directory.GetFiles(Path.Combine(root, "game"), "AgentPlaytest*.cs");
        Assert.NotEmpty(sources);
        var forbidden = new[]
        {
            "Godot.Input",
            "GetGlobalMousePosition",
            "SendInput",
            "user32",
            "HttpClient",
            "System.Net",
            "PeekNamedPipe",
            "GetStdHandle",
            "AgentPlaytestPipelineProbe",
            "NamedPipeServerStream",
            "NamedPipeClientStream",
            "System.IO.Pipes",
        };
        foreach (var source in sources)
        {
            var text = File.ReadAllText(source);
            Assert.All(forbidden, token => Assert.DoesNotContain(token, text, StringComparison.Ordinal));
        }

        var main = File.ReadAllText(Path.Combine(root, "game", "Main.cs"));
        var guard = main.IndexOf("_startupMode == StartupMode.DebugAgentPlaytest", StringComparison.Ordinal);
        var firstInputRead = main.IndexOf("GetGlobalMousePosition", StringComparison.Ordinal);
        Assert.True(guard >= 0 && guard < firstInputRead);
        Assert.Contains("SetPhysicsProcess(false)", main, StringComparison.Ordinal);
    }

    private static AgentPlaytestParseResult Parse(string json) =>
        AgentPlaytestNdjson.ParseRequest(Encoding.UTF8.GetBytes(json + "\n"));

    private static AgentPlaytestRequest Request(
        int sequence,
        int ticks,
        int jumpPlayer = -1,
        int firePlayer = -1,
        int blockPlayer = -1,
        double aimX = 0,
        double aimY = 0)
    {
        var players = Enumerable.Range(0, 2)
            .Select(player => new AgentPlaytestPlayerAction(
                0,
                player == jumpPlayer,
                player == firePlayer,
                player == blockPlayer,
                player == firePlayer ? aimX : 0,
                player == firePlayer ? aimY : 0))
            .ToArray();
        return new AgentPlaytestRequest(AgentPlaytestLimits.Protocol, sequence, ticks, Array.AsReadOnly(players));
    }

    private static AgentPlaytestFrameResponse Frame(int sequence, bool terminal) => new(
        sequence,
        Path.GetFullPath($"frame-{sequence:0000}.png"),
        Convert.ToHexString(SHA256.HashData(BitConverter.GetBytes(sequence))).ToLowerInvariant(),
        2,
        1,
        terminal);

    private static AgentPlaytestSession CreateStructuralBoundarySession()
    {
        var session = new AgentPlaytestSession();
        session.Apply(Request(1, 1), TimeSpan.Zero);
        session.Apply(Request(2, 1, jumpPlayer: 0), TimeSpan.Zero);
        session.Apply(Request(3, 1), TimeSpan.Zero);
        session.Apply(Request(4, 1, jumpPlayer: 1), TimeSpan.Zero);
        while (!session.IsTerminal && session.Accepted.Count < AgentPlaytestLimits.MaximumRequests)
        {
            var world = session.Match.World;
            var offset = world.Players[1].Position - world.Players[0].Position;
            var length = System.Math.Sqrt(offset.LengthSquared);
            var aimX = length == 0 ? 1.0 : offset.X / length;
            var aimY = length == 0 ? 0.0 : offset.Y / length;
            session.Apply(Request(session.Sequence + 1, 30, firePlayer: 0, aimX: aimX, aimY: aimY), TimeSpan.Zero);
        }
        return session;
    }

    private static byte[] TraceBytes(AgentPlaytestSession session)
    {
        var recorder = new AgentPlaytestTraceRecorder(1);
        foreach (var interval in session.Accepted)
        {
            recorder.Record(interval);
        }
        return recorder.ToCanonicalBytes(requireTerminal: true);
    }

    private static string FindRepositoryRoot()
    {
        var candidate = new DirectoryInfo(AppContext.BaseDirectory);
        while (candidate is not null)
        {
            if (File.Exists(Path.Combine(candidate.FullName, "game", "Main.cs")))
            {
                return candidate.FullName;
            }
            candidate = candidate.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the repository root for source isolation checks.");
    }

    private static void AssertFailure(Action action, string stage, string code)
    {
        var failure = Assert.Throws<AgentPlaytestFailure>(action);
        Assert.Equal(stage, failure.Response.Stage);
        Assert.Equal(code, failure.Response.Code);
    }

    private static void AssertLegal(AgentPlaytestPlayerAction action)
    {
        Assert.InRange(action.Move, (sbyte)-1, (sbyte)1);
        Assert.InRange(action.AimX, -1.0, 1.0);
        Assert.InRange(action.AimY, -1.0, 1.0);
    }

    private sealed class FixedDecoder : IAgentPlaytestRgba8Decoder
    {
        public DecodedAgentPlaytestFrame Decode(ReadOnlySpan<byte> encodedPng) =>
            encodedPng.Length == 0
                ? throw new InvalidDataException()
                : new DecodedAgentPlaytestFrame(2, 1, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
    }

    private sealed class FakeFrameSource : IAgentPlaytestFrameSource
    {
        private readonly List<int> _capturedSequences = [];
        public IReadOnlyList<int> CapturedSequences => _capturedSequences;

        public byte[] CapturePng(int sequence, bool terminal)
        {
            _capturedSequences.Add(sequence);
            return Encoding.ASCII.GetBytes($"fake-png sequence={sequence} terminal={terminal}");
        }
    }

    private sealed class ThrowingFrameSource : IAgentPlaytestFrameSource
    {
        public byte[] CapturePng(int sequence, bool terminal)
        {
            _ = sequence;
            _ = terminal;
            throw new InvalidOperationException("synthetic simulation failure");
        }
    }

    private sealed class TurnwiseTransport(IReadOnlyList<byte[]> requests) : IAgentPlaytestTurnTransport
    {
        private byte[]? _available;
        private int _next;

        public List<AgentPlaytestResponse> Responses { get; } = [];
        public List<string> Diagnostics { get; } = [];
        public int ReadCount { get; private set; }

        public ValueTask<byte[]?> ReadRequestAsync(TimeSpan remaining, CancellationToken cancellationToken)
        {
            Assert.True(remaining > TimeSpan.Zero);
            Assert.False(cancellationToken.IsCancellationRequested);
            var request = _available;
            _available = null;
            if (request is not null)
            {
                ReadCount++;
            }
            return ValueTask.FromResult(request);
        }

        public void WriteResponse(AgentPlaytestResponse response)
        {
            Responses.Add(response);
            if (response is AgentPlaytestFrameResponse && _next < requests.Count)
            {
                Assert.Null(_available);
                _available = requests[_next++];
            }
        }

        public void WriteDiagnostic(string message) => Diagnostics.Add(message);
    }

    private sealed class AdversarialRootAcquirer : IAgentPlaytestRootAcquirer
    {
        public IAgentPlaytestRootLease Acquire(string absoluteRoot)
        {
            Directory.CreateDirectory(absoluteRoot);
            File.WriteAllText(Path.Combine(absoluteRoot, "sentinel.txt"), "adversarial");
            throw new IOException("Lost deterministic acquisition race.");
        }
    }

    private sealed class CleanupRaceAcquirer : IAgentPlaytestRootAcquirer
    {
        public CleanupRaceLease? Lease { get; private set; }

        public IAgentPlaytestRootLease Acquire(string absoluteRoot)
        {
            Directory.CreateDirectory(absoluteRoot);
            File.WriteAllText(Path.Combine(absoluteRoot, "owned.txt"), "owned");
            Lease = new CleanupRaceLease(absoluteRoot);
            return Lease;
        }
    }

    private sealed class CleanupRaceLease(string root) : IAgentPlaytestRootLease
    {
        public string Root { get; } = root;
        public bool SiblingLockHeld { get; private set; } = true;
        public bool DeleteCalledWhileLockHeld { get; private set; }
        public bool ForeignContentAdmitted { get; private set; }

        public bool TryDeleteOwnedRoot()
        {
            DeleteCalledWhileLockHeld = SiblingLockHeld;
            if (!SiblingLockHeld)
            {
                ForeignContentAdmitted = true;
                File.WriteAllText(Path.Combine(Root, "foreign.txt"), "foreign");
            }
            Directory.Delete(Root, recursive: true);
            return true;
        }

        public void Dispose()
        {
            SiblingLockHeld = false;
            if (Directory.Exists(Root))
            {
                ForeignContentAdmitted = true;
                File.WriteAllText(Path.Combine(Root, "foreign.txt"), "foreign");
            }
        }
    }

    private sealed class RetryCleanupAcquirer(int failuresBeforeSuccess) : IAgentPlaytestRootAcquirer
    {
        public RetryCleanupLease? Lease { get; private set; }

        public IAgentPlaytestRootLease Acquire(string absoluteRoot)
        {
            Directory.CreateDirectory(absoluteRoot);
            File.WriteAllText(Path.Combine(absoluteRoot, "owned.txt"), "owned");
            Lease = new RetryCleanupLease(absoluteRoot, failuresBeforeSuccess);
            return Lease;
        }
    }

    private sealed class RetryCleanupLease(string root, int failuresBeforeSuccess) : IAgentPlaytestRootLease
    {
        public string Root { get; } = root;
        public int Attempts { get; private set; }
        public bool SiblingLockHeld { get; private set; } = true;
        public bool HeldDuringEveryAttempt { get; private set; } = true;

        public bool TryDeleteOwnedRoot()
        {
            Attempts++;
            HeldDuringEveryAttempt &= SiblingLockHeld;
            if (Attempts <= failuresBeforeSuccess)
            {
                return false;
            }
            Directory.Delete(Root, recursive: true);
            return true;
        }

        public void Dispose() => SiblingLockHeld = false;
    }

    private sealed class ReplacingParentBinder(string root) : IAgentPlaytestParentIdentityBinder
    {
        public ReplacingParentLease? Lease { get; private set; }

        public IAgentPlaytestParentIdentityLease Bind(string normalizedParent)
        {
            Assert.Equal(Path.GetDirectoryName(root), normalizedParent);
            Lease = new ReplacingParentLease(root);
            return Lease;
        }
    }

    private sealed class ReplacingParentLease(string root) : IAgentPlaytestParentIdentityLease
    {
        private int _checks;
        public bool Disposed { get; private set; }
        public bool ReplacementInstalled { get; private set; }

        public bool MatchesCurrentPath()
        {
            _checks++;
            if (_checks < 3)
            {
                return true;
            }
            if (!ReplacementInstalled)
            {
                Directory.Delete(root);
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "sentinel.txt"), "foreign replacement");
                ReplacementInstalled = true;
            }
            return false;
        }

        public void Dispose() => Disposed = true;
    }

    private sealed class NonSeekableReadStream(Stream inner) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);
        public override int ReadByte() => inner.ReadByte();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class PartialThenTimeoutReader(byte[] partial) : IAgentPlaytestBoundedStreamReader
    {
        private int _index;
        public bool TimeoutObserved { get; private set; }
        public List<TimeSpan> ObservedTimeouts { get; } = [];

        public ValueTask<int> ReadAsync(
            Stream input,
            Memory<byte> buffer,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            _ = input;
            Assert.False(cancellationToken.IsCancellationRequested);
            ObservedTimeouts.Add(timeout);
            if (_index < partial.Length)
            {
                buffer.Span[0] = partial[_index++];
                return ValueTask.FromResult(1);
            }
            TimeoutObserved = true;
            throw new TimeoutException("The in-route playtest deadline expired while a partial line remained open.");
        }
    }

    private sealed class CausalOwnerChannel(
        List<string> events,
        params AgentPlaytestFrameResponse[] responses) : IAgentPlaytestOwnerChannel
    {
        private int _nextRead;
        public List<AgentPlaytestRequest> WrittenRequests { get; } = [];

        public AgentPlaytestResponse ReadResponse()
        {
            var response = responses[_nextRead++];
            events.Add($"read:{response.FrameSequence}");
            return response;
        }

        public void WriteRequest(AgentPlaytestRequest request)
        {
            WrittenRequests.Add(request);
            events.Add($"write:{request.Sequence}");
        }
    }

    private sealed class RecordingVerifier(List<string> events) : IAgentPlaytestFinalizedFrameVerifier
    {
        public HumanPlaytestObservation VerifyResponse(
            AgentPlaytestFrameResponse response,
            IAgentPlaytestRgba8Decoder decoder)
        {
            _ = decoder;
            events.Add($"verify:{response.FrameSequence}");
            return new HumanPlaytestObservation(response.FrameSequence, new byte[8], 2, 1);
        }
    }

    private sealed class RecordingDriver(List<string> events) : IHumanPlaytestDriver
    {
        public int ChooseCount { get; private set; }
        public int ObserveCount { get; private set; }
        public List<AgentPlaytestRequest> ReturnedRequests { get; } = [];

        public void Observe(HumanPlaytestObservation observation)
        {
            ObserveCount++;
            events.Add($"observe:{observation.Sequence}");
        }

        public AgentPlaytestRequest Choose(HumanPlaytestObservation observation)
        {
            ChooseCount++;
            events.Add($"choose:{observation.Sequence}");
            var request = Request(observation.Sequence + 1, 1);
            ReturnedRequests.Add(request);
            return request;
        }
    }

    private sealed class ExactActionDriver(IReadOnlyList<AgentPlaytestAcceptedInterval> intervals) : IHumanPlaytestDriver
    {
        private int _next;

        public void Observe(HumanPlaytestObservation observation) => Assert.Equal(_next, observation.Sequence);

        public AgentPlaytestRequest Choose(HumanPlaytestObservation observation)
        {
            Assert.Equal(_next, observation.Sequence);
            var interval = intervals[_next++];
            return new AgentPlaytestRequest(
                AgentPlaytestLimits.Protocol,
                interval.Sequence,
                interval.RequestedIntervalTicks,
                interval.Players);
        }
    }
}
