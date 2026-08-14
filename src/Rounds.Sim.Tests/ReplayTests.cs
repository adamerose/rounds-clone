using System.Text;
using Rounds.Replay;
using Rounds.Sim;
using Rounds.Sim.Math;

namespace Rounds.Sim.Tests;

public sealed class ReplayTests
{
    [Fact]
    public void RoundTripPreservesEveryInputBitAndCanonicalBytes()
    {
        var recorder = new ReplayRecorder("edge-inputs", 7, "arena-006", 1);
        var inputs = new[]
        {
            new PlayerInput(-1, true, false, true, new Vec2(double.MaxValue, -0.0)),
            new PlayerInput(1, false, true, false, new Vec2(double.Epsilon, -double.Epsilon)),
        };

        recorder.Step(inputs);
        var original = recorder.Finish();
        var bytes = ReplayCodec.ToCanonicalBytes(original);
        using var stream = new MemoryStream(bytes);
        var loaded = ReplayCodec.Load(stream);

        Assert.Equal(BitConverter.DoubleToUInt64Bits(double.MaxValue), loaded.Runs[0].Frame.Player0.AimXBits);
        Assert.Equal(BitConverter.DoubleToUInt64Bits(-0.0), loaded.Runs[0].Frame.Player0.AimYBits);
        Assert.Equal(BitConverter.DoubleToUInt64Bits(double.Epsilon), loaded.Runs[0].Frame.Player1.AimXBits);
        Assert.Equal(BitConverter.DoubleToUInt64Bits(-double.Epsilon), loaded.Runs[0].Frame.Player1.AimYBits);
        Assert.Equal(bytes, ReplayCodec.ToCanonicalBytes(loaded));
        Assert.Equal((byte)'\n', bytes[^1]);
        Assert.DoesNotContain((byte)'\r', bytes);
    }

    [Theory]
    [InlineData("leading-space")]
    [InlineData("bom")]
    [InlineData("unknown-property")]
    [InlineData("duplicate-property")]
    [InlineData("reordered-property")]
    [InlineData("uppercase-aim")]
    [InlineData("short-aim")]
    [InlineData("binary-aim")]
    [InlineData("integer-decimal")]
    [InlineData("escaped-id")]
    [InlineData("crlf")]
    [InlineData("missing-newline")]
    [InlineData("extra-newline")]
    [InlineData("invalid-utf8")]
    public void NonCanonicalOrMalformedBytesFail(string mutation)
    {
        var bytes = CreateReplayBytes(1);
        var text = Encoding.UTF8.GetString(bytes);
        var mutated = mutation switch
        {
            "leading-space" => Encoding.UTF8.GetBytes(" " + text),
            "bom" => [0xef, 0xbb, 0xbf, .. bytes],
            "unknown-property" => Encoding.UTF8.GetBytes(text.Replace("{\"format\":1", "{\"extra\":0,\"format\":1", StringComparison.Ordinal)),
            "duplicate-property" => Encoding.UTF8.GetBytes(text.Replace("{\"format\":1", "{\"format\":1,\"format\":1", StringComparison.Ordinal)),
            "reordered-property" => Encoding.UTF8.GetBytes(text.Replace("{\"format\":1,\"replayId\":\"test-replay\"", "{\"replayId\":\"test-replay\",\"format\":1", StringComparison.Ordinal)),
            "uppercase-aim" => Encoding.UTF8.GetBytes(text.Replace("3ff0000000000000", "3FF0000000000000", StringComparison.Ordinal)),
            "short-aim" => Encoding.UTF8.GetBytes(text.Replace("3ff0000000000000", "3ff000000000000", StringComparison.Ordinal)),
            "binary-aim" => Encoding.UTF8.GetBytes(text.Replace("3ff0000000000000", new string('0', 64), StringComparison.Ordinal)),
            "integer-decimal" => Encoding.UTF8.GetBytes(text.Replace("\"totalTicks\":1", "\"totalTicks\":1.0", StringComparison.Ordinal)),
            "escaped-id" => Encoding.UTF8.GetBytes(text.Replace("test-replay", "test\\u002dreplay", StringComparison.Ordinal)),
            "crlf" => Encoding.UTF8.GetBytes(text.Replace("\n", "\r\n", StringComparison.Ordinal)),
            "missing-newline" => bytes[..^1],
            "extra-newline" => [.. bytes, (byte)'\n'],
            "invalid-utf8" => [0xff, .. bytes[1..]],
            _ => throw new InvalidOperationException(),
        };

        using var stream = new MemoryStream(mutated);
        Assert.Throws<InvalidDataException>(() => ReplayCodec.Load(stream));
    }

    [Fact]
    public void MostSignificantNibbleOrderDoesNotTreatByteReversalAsSameValue()
    {
        var bytes = CreateReplayBytes(1);
        var text = Encoding.UTF8.GetString(bytes);
        var reversed = Encoding.UTF8.GetBytes(text.Replace("3ff0000000000000", "000000000000f03f", StringComparison.Ordinal));
        using var stream = new MemoryStream(reversed);
        var replay = ReplayCodec.Load(stream);

        Assert.NotEqual(1.0, BitConverter.UInt64BitsToDouble(replay.Runs[0].Frame.Player0.AimXBits));
    }

    [Fact]
    public void RecorderCoalescesBitIdenticalFramesButSeparatesSignedZero()
    {
        var recorder = new ReplayRecorder("signed-zero", 1, "arena-006", 3);
        var positive = Inputs(new Vec2(1, +0.0));
        var negative = Inputs(new Vec2(1, -0.0));
        recorder.Step(positive);
        recorder.Step(positive);
        recorder.Step(negative);

        var replay = recorder.Finish();

        Assert.Equal(2, replay.Runs.Count);
        Assert.Equal(2, replay.Runs[0].Length);
        Assert.Equal(1, replay.Runs[1].Length);
        Assert.NotEqual(replay.Runs[0].Frame.Player0.AimYBits, replay.Runs[1].Frame.Player0.AimYBits);
    }

    [Fact]
    public void CheckpointsOccurAtSixtyAndNonMultipleFinalTick()
    {
        var recorder = new ReplayRecorder("checkpoint-edge", 2, "arena-006", 61);
        for (var tick = 0; tick < 61; tick++)
        {
            recorder.Step(Inputs(new Vec2(1, 0)));
        }

        var replay = recorder.Finish();

        Assert.Equal([60, 61], replay.Checkpoints.Select(checkpoint => checkpoint.Tick));
        Assert.Equal(replay.FinalHash, replay.Checkpoints[^1].Hash);
    }

    [Fact]
    public void PlaybackStopsAtFirstCheckpointMismatchWithExactDiagnostics()
    {
        var valid = CreateReplay(61);
        var checkpoints = valid.Checkpoints.ToArray();
        checkpoints[0] = checkpoints[0] with { Hash = checkpoints[0].Hash ^ 1UL };
        var corrupt = new ReplayDocument(
            valid.ReplayId,
            valid.Seed,
            valid.ArenaId,
            valid.TotalTicks,
            valid.Runs,
            checkpoints,
            valid.FinalHash);
        var playback = new ReplayPlayback(corrupt);

        var error = Assert.Throws<ReplayMismatchException>(() => playback.RunToEnd());

        Assert.Equal(valid.ReplayId, error.ReplayId);
        Assert.Equal(60, error.Tick);
        Assert.Equal(checkpoints[0].Hash, error.ExpectedHash);
        Assert.Equal(60, playback.ConsumedTicks);
    }

    [Fact]
    public void RepeatedPlaybackProducesIdenticalFinalWorlds()
    {
        var replay = CreateReplay(121);
        var first = new ReplayPlayback(replay);
        var second = new ReplayPlayback(replay);

        first.RunToEnd();
        second.RunToEnd();

        Assert.Equal(replay.FinalHash, Rounds.Sim.Sim.Hash(first.World));
        Assert.Equal(Rounds.Sim.Sim.Hash(first.World), Rounds.Sim.Sim.Hash(second.World));
    }

    [Fact]
    public void OneInputMutationChangesRecordedHash()
    {
        var first = new ReplayRecorder("first", 5, "arena-006", 61);
        var second = new ReplayRecorder("second", 5, "arena-006", 61);
        for (var tick = 0; tick < 61; tick++)
        {
            first.Step(Inputs(new Vec2(1, 0), fire: false));
            second.Step(Inputs(new Vec2(1, 0), fire: tick == 60));
        }

        Assert.NotEqual(first.Finish().FinalHash, second.Finish().FinalHash);
    }

    [Fact]
    public void StructuralInvalidityFailsBeforePlaybackWorldExists()
    {
        var valid = CreateReplay(1);
        var invalid = new ReplayDocument(
            valid.ReplayId,
            valid.Seed,
            valid.ArenaId,
            2,
            valid.Runs,
            valid.Checkpoints,
            valid.FinalHash);

        Assert.Throws<InvalidDataException>(() => new ReplayPlayback(invalid));
    }

    [Fact]
    public void RecorderRejectsScalarHeaderBeforeAllocatingReplayState()
    {
        Assert.Throws<InvalidDataException>(() => new ReplayRecorder("Bad-ID", 1, "arena-006", 1));
        Assert.Throws<InvalidDataException>(() => new ReplayRecorder("valid-id", 1, "arena-999", 1));
        Assert.Throws<InvalidDataException>(() => new ReplayRecorder("valid-id", 1, "arena-006", 0));
        Assert.Throws<InvalidDataException>(() => new ReplayRecorder("valid-id", 1, "arena-006", int.MaxValue));
        Assert.NotNull(new ReplayRecorder(new string('a', 64), 1, "arena-006", ReplayFormat.MaximumTicks).World);
    }

    [Theory]
    [InlineData("format")]
    [InlineData("empty-id")]
    [InlineData("long-id")]
    [InlineData("target-build")]
    [InlineData("ruleset")]
    [InlineData("leading-zero-seed")]
    [InlineData("overflow-seed")]
    [InlineData("malformed-arena")]
    [InlineData("unknown-arena")]
    [InlineData("tick-rate")]
    [InlineData("player-count")]
    [InlineData("zero-ticks")]
    [InlineData("too-many-ticks")]
    public void EveryReplayHeaderBoundaryRejectsUnsupportedValues(string mutation)
    {
        var text = Encoding.UTF8.GetString(CreateReplayBytes(1));
        var mutated = mutation switch
        {
            "format" => text.Replace("\"format\":1", "\"format\":2", StringComparison.Ordinal),
            "empty-id" => text.Replace("\"replayId\":\"test-replay\"", "\"replayId\":\"\"", StringComparison.Ordinal),
            "long-id" => text.Replace("test-replay", new string('a', 65), StringComparison.Ordinal),
            "target-build" => text.Replace("\"targetBuild\":21020021", "\"targetBuild\":21020022", StringComparison.Ordinal),
            "ruleset" => text.Replace("base-combat-v1", "base-combat-v2", StringComparison.Ordinal),
            "leading-zero-seed" => text.Replace("\"seed\":\"3\"", "\"seed\":\"03\"", StringComparison.Ordinal),
            "overflow-seed" => text.Replace("\"seed\":\"3\"", "\"seed\":\"18446744073709551616\"", StringComparison.Ordinal),
            "malformed-arena" => text.Replace("arena-006", "Arena-006", StringComparison.Ordinal),
            "unknown-arena" => text.Replace("arena-006", "arena-999", StringComparison.Ordinal),
            "tick-rate" => text.Replace("\"tickRate\":60", "\"tickRate\":61", StringComparison.Ordinal),
            "player-count" => text.Replace("\"playerCount\":2", "\"playerCount\":3", StringComparison.Ordinal),
            "zero-ticks" => text.Replace("\"totalTicks\":1", "\"totalTicks\":0", StringComparison.Ordinal),
            "too-many-ticks" => text.Replace("\"totalTicks\":1", $"\"totalTicks\":{ReplayFormat.MaximumTicks + 1}", StringComparison.Ordinal),
            _ => throw new InvalidOperationException(),
        };

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(mutated));
        Assert.Throws<InvalidDataException>(() => ReplayCodec.Load(stream));
    }

    [Fact]
    public void CanonicalSeedExtremesLoad()
    {
        var text = Encoding.UTF8.GetString(CreateReplayBytes(1));
        foreach (var seed in new[] { "0", ulong.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture) })
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text.Replace("\"seed\":\"3\"", $"\"seed\":\"{seed}\"", StringComparison.Ordinal)));
            Assert.Equal(ulong.Parse(seed, System.Globalization.CultureInfo.InvariantCulture), ReplayCodec.Load(stream).Seed);
        }
    }

    [Theory]
    [InlineData("empty-runs")]
    [InlineData("zero-run")]
    [InlineData("bad-move")]
    [InlineData("adjacent-duplicate")]
    [InlineData("wrong-total")]
    public void EveryRunShapeBoundaryFails(string mutation)
    {
        var valid = CreateReplay(1);
        var badPlayer = valid.Runs[0].Frame.Player0 with { MoveAxis = 2 };
        var invalid = mutation switch
        {
            "empty-runs" => Copy(valid, runs: []),
            "zero-run" => Copy(valid, runs: [valid.Runs[0] with { Length = 0 }]),
            "bad-move" => Copy(valid, runs: [valid.Runs[0] with { Frame = valid.Runs[0].Frame with { Player0 = badPlayer } }]),
            "adjacent-duplicate" => Copy(valid, totalTicks: 2, runs: [valid.Runs[0], valid.Runs[0]], checkpoints: [new ReplayCheckpoint(2, valid.FinalHash)]),
            "wrong-total" => Copy(valid, totalTicks: 2),
            _ => throw new InvalidOperationException(),
        };

        Assert.Throws<InvalidDataException>(() => ReplayCodec.ToCanonicalBytes(invalid));
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("missing-final")]
    [InlineData("wrong-tick")]
    [InlineData("duplicate-final")]
    [InlineData("final-mismatch")]
    public void EveryCheckpointBoundaryFails(string mutation)
    {
        var valid = CreateReplay(61);
        var checkpoints = valid.Checkpoints.ToArray();
        var invalid = mutation switch
        {
            "empty" => Copy(valid, checkpoints: []),
            "missing-final" => Copy(valid, checkpoints: checkpoints[..^1]),
            "wrong-tick" => Copy(valid, checkpoints: [checkpoints[0] with { Tick = 59 }, checkpoints[1]]),
            "duplicate-final" => Copy(valid, checkpoints: [.. checkpoints, checkpoints[^1]]),
            "final-mismatch" => Copy(valid, checkpoints: [checkpoints[0], checkpoints[1] with { Hash = checkpoints[1].Hash ^ 1UL }]),
            _ => throw new InvalidOperationException(),
        };

        Assert.Throws<InvalidDataException>(() => ReplayCodec.ToCanonicalBytes(invalid));
    }

    [Fact]
    public void NonFiniteAimAndUnknownArenaFailValidation()
    {
        var valid = CreateReplay(1);
        var nonFinitePlayer = valid.Runs[0].Frame.Player0 with
        {
            AimXBits = BitConverter.DoubleToUInt64Bits(double.PositiveInfinity),
        };
        var nonFinite = Copy(valid, runs: [new ReplayRun(1, valid.Runs[0].Frame with { Player0 = nonFinitePlayer })]);
        var unknownArena = new ReplayDocument(
            valid.ReplayId,
            valid.Seed,
            "arena-999",
            valid.TotalTicks,
            valid.Runs,
            valid.Checkpoints,
            valid.FinalHash);

        Assert.Throws<InvalidDataException>(() => ReplayCodec.ToCanonicalBytes(nonFinite));
        Assert.Throws<InvalidDataException>(() => ReplayCodec.ToCanonicalBytes(unknownArena));
    }

    [Fact]
    public void AdjacentDuplicateAndInconsistentTotalFailValidation()
    {
        var valid = CreateReplay(1);
        var duplicate = Copy(valid, totalTicks: 2, runs: [valid.Runs[0], valid.Runs[0]], checkpoints: [new ReplayCheckpoint(2, valid.FinalHash)]);
        var wrongTotal = Copy(valid, totalTicks: 2);

        Assert.Throws<InvalidDataException>(() => ReplayCodec.ToCanonicalBytes(duplicate));
        Assert.Throws<InvalidDataException>(() => ReplayCodec.ToCanonicalBytes(wrongTotal));
    }

    [Fact]
    public void StreamLoadingDoesNotDependOnPathOrPositionZero()
    {
        var replayBytes = CreateReplayBytes(1);
        using var first = new MemoryStream(replayBytes);
        using var second = new MemoryStream();
        second.Write(replayBytes);
        second.Position = 0;

        Assert.Equal(ReplayCodec.Load(first).FinalHash, ReplayCodec.Load(second).FinalHash);
    }

    [Fact]
    public void CorpusDiscoveryIsOrdinalNonrecursiveAndRejectsEmptyMismatchAndDuplicateId()
    {
        var root = Path.Combine(Path.GetTempPath(), "rounds-replay-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var empty = Path.Combine(root, "empty");
            Directory.CreateDirectory(empty);
            Assert.Throws<InvalidDataException>(() => ReplayCorpus.VerifyDirectory(empty));

            var valid = Path.Combine(root, "valid");
            Directory.CreateDirectory(valid);
            WriteReplay(valid, CreateReplay("zeta", 2));
            WriteReplay(valid, CreateReplay("alpha", 1));
            File.WriteAllText(Path.Combine(valid, "ignored.txt"), "ignored");
            var nested = Path.Combine(valid, "nested");
            Directory.CreateDirectory(nested);
            WriteReplay(nested, CreateReplay("nested", 1));
            Assert.Equal(["alpha", "zeta"], ReplayCorpus.VerifyDirectory(valid).Select(result => result.ReplayId));

            var mismatch = Path.Combine(root, "mismatch");
            Directory.CreateDirectory(mismatch);
            WriteReplay(mismatch, CreateReplay("actual", 1), "wrong" + ReplayFormat.FileSuffix);
            Assert.Contains("does not match ID", Assert.Throws<InvalidDataException>(() => ReplayCorpus.VerifyDirectory(mismatch)).Message, StringComparison.Ordinal);

            var duplicate = Path.Combine(root, "duplicate");
            Directory.CreateDirectory(duplicate);
            var repeated = CreateReplay("same", 1);
            WriteReplay(duplicate, repeated);
            WriteReplay(duplicate, repeated, "zzz" + ReplayFormat.FileSuffix);
            Assert.Contains("duplicates ID", Assert.Throws<InvalidDataException>(() => ReplayCorpus.VerifyDirectory(duplicate)).Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ReplayDocument CreateReplay(int ticks)
        => CreateReplay("test-replay", ticks);

    private static ReplayDocument CreateReplay(string id, int ticks)
    {
        var recorder = new ReplayRecorder(id, 3, "arena-006", ticks);
        for (var tick = 0; tick < ticks; tick++)
        {
            recorder.Step(Inputs(new Vec2(1, 0), fire: tick % 23 < 3));
        }
        return recorder.Finish();
    }

    private static byte[] CreateReplayBytes(int ticks) => ReplayCodec.ToCanonicalBytes(CreateReplay(ticks));

    private static void WriteReplay(string directory, ReplayDocument replay, string? filename = null)
    {
        var path = Path.Combine(directory, filename ?? replay.ReplayId + ReplayFormat.FileSuffix);
        using var stream = File.Create(path);
        ReplayCodec.Write(stream, replay);
    }

    private static PlayerInput[] Inputs(Vec2 aim, bool fire = false) =>
    [
        new PlayerInput(0, false, fire, false, aim),
        new PlayerInput(0, false, false, false, new Vec2(-1, 0)),
    ];

    private static ReplayDocument Copy(
        ReplayDocument source,
        int? totalTicks = null,
        IEnumerable<ReplayRun>? runs = null,
        IEnumerable<ReplayCheckpoint>? checkpoints = null) =>
        new(
            source.ReplayId,
            source.Seed,
            source.ArenaId,
            totalTicks ?? source.TotalTicks,
            runs ?? source.Runs,
            checkpoints ?? source.Checkpoints,
            source.FinalHash);
}
