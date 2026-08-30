using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class BaseProjectileEvidenceAcknowledgementTests
{
    [Fact]
    public async Task ExactlyOneAcknowledgementFollowedByPipeClosureSucceeds()
    {
        var source = new ScriptedAcknowledgementSource(
            Data(DebugEvidenceCaptureProtocol.EvidenceAcknowledgement),
            new EvidenceAcknowledgementRead(EvidenceAcknowledgementReadKind.Closed));

        Assert.True(await EvidenceAcknowledgementReader.WaitAsync(source, TimeSpan.FromSeconds(1)));
        Assert.True(source.Disposed);
    }

    [Theory]
    [InlineData(0x06, 0x06)]
    [InlineData(0x06, 0x07)]
    [InlineData(0x07, 0x06)]
    public async Task AnyWrongOrTrailingByteIsRejected(byte first, byte second)
    {
        var source = new ScriptedAcknowledgementSource(
            Data(first),
            Data(second),
            new EvidenceAcknowledgementRead(EvidenceAcknowledgementReadKind.Closed));

        Assert.False(await EvidenceAcknowledgementReader.WaitAsync(source, TimeSpan.FromSeconds(1)));
        Assert.True(source.Disposed);
    }

    [Fact]
    public async Task AcknowledgementWithoutClosureTimesOutOnMonotonicDeadline()
    {
        var source = new ScriptedAcknowledgementSource(
            Data(DebugEvidenceCaptureProtocol.EvidenceAcknowledgement));

        Assert.False(await EvidenceAcknowledgementReader.WaitAsync(source, TimeSpan.FromMilliseconds(25)));
        Assert.True(source.Disposed);
        Assert.True(source.PollCount >= 2);
    }

    [Fact]
    public async Task PipeClosureBeforeAcknowledgementIsRejected()
    {
        var source = new ScriptedAcknowledgementSource(
            new EvidenceAcknowledgementRead(EvidenceAcknowledgementReadKind.Closed));

        Assert.False(await EvidenceAcknowledgementReader.WaitAsync(source, TimeSpan.FromSeconds(1)));
        Assert.True(source.Disposed);
    }

    private static EvidenceAcknowledgementRead Data(byte value) =>
        new(EvidenceAcknowledgementReadKind.Data, value);

    private sealed class ScriptedAcknowledgementSource(
        params EvidenceAcknowledgementRead[] reads) : IEvidenceAcknowledgementSource
    {
        private readonly Queue<EvidenceAcknowledgementRead> _reads = new(reads);

        public bool Disposed { get; private set; }
        public int PollCount { get; private set; }

        public EvidenceAcknowledgementRead Poll()
        {
            PollCount++;
            return _reads.TryDequeue(out var read)
                ? read
                : new EvidenceAcknowledgementRead(EvidenceAcknowledgementReadKind.NoData);
        }

        public void Dispose() => Disposed = true;
    }
}
