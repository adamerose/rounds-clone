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
    [InlineData("uppercase-aim")]
    [InlineData("short-aim")]
    [InlineData("binary-aim")]
    public void NonCanonicalOrMalformedBytesFail(string mutation)
    {
        var bytes = CreateReplayBytes(1);
        var text = Encoding.UTF8.GetString(bytes);
        var mutated = mutation switch
        {
            "leading-space" => Encoding.UTF8.GetBytes(" " + text),
            "bom" => [0xef, 0xbb, 0xbf, .. bytes],
            "unknown-property" => Encoding.UTF8.GetBytes(text.Replace("{\"format\":1", "{\"extra\":0,\"format\":1", StringComparison.Ordinal)),
            "uppercase-aim" => Encoding.UTF8.GetBytes(text.Replace("3ff0000000000000", "3FF0000000000000", StringComparison.Ordinal)),
            "short-aim" => Encoding.UTF8.GetBytes(text.Replace("3ff0000000000000", "3ff000000000000", StringComparison.Ordinal)),
            "binary-aim" => Encoding.UTF8.GetBytes(text.Replace("3ff0000000000000", new string('0', 64), StringComparison.Ordinal)),
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

    private static ReplayDocument CreateReplay(int ticks)
    {
        var recorder = new ReplayRecorder("test-replay", 3, "arena-006", ticks);
        for (var tick = 0; tick < ticks; tick++)
        {
            recorder.Step(Inputs(new Vec2(1, 0), fire: tick % 23 < 3));
        }
        return recorder.Finish();
    }

    private static byte[] CreateReplayBytes(int ticks) => ReplayCodec.ToCanonicalBytes(CreateReplay(ticks));

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
