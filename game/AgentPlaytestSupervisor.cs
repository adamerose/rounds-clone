using System.Security.Cryptography;

namespace Rounds.Game;

internal interface IAgentPlaytestOwnerChannel
{
    AgentPlaytestResponse ReadResponse();
    void WriteRequest(AgentPlaytestRequest request);
}

internal interface IAgentPlaytestFinalizedFrameVerifier
{
    HumanPlaytestObservation VerifyResponse(
        AgentPlaytestFrameResponse response,
        IAgentPlaytestRgba8Decoder decoder);
}

internal readonly record struct AgentPlaytestCausalityReceiptView(
    int PriorFrameSequence,
    string PriorFrameSha256,
    int RequestSequence,
    string ActionIdentity);

internal sealed class AgentPlaytestOwnerSupervisor
{
    private static readonly object ProofIssuer = new();

    internal sealed class CausalCompletionProof
    {
        private readonly AgentPlaytestCausalityReceiptView[] _receipts;
        private readonly int _terminalFrameSequence;
        private readonly string _terminalFrameSha256;
        private readonly bool _finalizedByArtifactOwner;
        private int _manifestConsumed;

        internal CausalCompletionProof(
            IReadOnlyList<AgentPlaytestCausalityReceiptView> receipts,
            int terminalFrameSequence,
            string terminalFrameSha256,
            bool finalizedByArtifactOwner,
            object issuer)
        {
            if (!ReferenceEquals(issuer, ProofIssuer))
            {
                throw new InvalidOperationException("Only a completed causal supervisor run can issue proof.");
            }
            _receipts = receipts.ToArray();
            _terminalFrameSequence = terminalFrameSequence;
            _terminalFrameSha256 = terminalFrameSha256;
            _finalizedByArtifactOwner = finalizedByArtifactOwner;
        }

        internal IReadOnlyList<AgentPlaytestCausalityReceiptView> SnapshotReceipts() =>
            Array.AsReadOnly(_receipts.ToArray());

        internal bool ConsumeForManifest(
            IReadOnlyList<AgentPlaytestFrameResponse> frames,
            IReadOnlyList<AgentPlaytestAcceptedInterval> intervals)
        {
            if (!_finalizedByArtifactOwner || !ReceiptsMatch(frames, intervals, _receipts) ||
                _terminalFrameSequence != frames.Count - 1 ||
                !string.Equals(_terminalFrameSha256, frames[^1].FrameSha256, StringComparison.Ordinal))
            {
                return false;
            }
            return Interlocked.CompareExchange(ref _manifestConsumed, 1, 0) == 0;
        }

        internal bool Matches(
            IReadOnlyList<AgentPlaytestCausalityReceiptView> receipts,
            IReadOnlyList<string?> frameHashes) =>
            Volatile.Read(ref _manifestConsumed) == 1 &&
            _receipts.SequenceEqual(receipts) &&
            _terminalFrameSequence == frameHashes.Count - 1 &&
            string.Equals(_terminalFrameSha256, frameHashes[^1], StringComparison.Ordinal);
    }

    private readonly IAgentPlaytestOwnerChannel _channel;
    private readonly IAgentPlaytestFinalizedFrameVerifier _verifier;
    private readonly IAgentPlaytestRgba8Decoder _decoder;
    private readonly IHumanPlaytestDriver _driver;
    private readonly List<AgentPlaytestCausalityReceiptView> _receipts = [];
    private bool _completed;

    public AgentPlaytestOwnerSupervisor(
        IAgentPlaytestOwnerChannel channel,
        IAgentPlaytestFinalizedFrameVerifier verifier,
        IAgentPlaytestRgba8Decoder decoder,
        IHumanPlaytestDriver driver)
    {
        _channel = channel ?? throw new ArgumentNullException(nameof(channel));
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _driver = driver ?? throw new ArgumentNullException(nameof(driver));
    }

    public CausalCompletionProof RunToTerminal()
    {
        if (_completed)
        {
            throw new InvalidOperationException("The causal supervisor can issue at most one proof.");
        }
        var response = RequireFrame(_channel.ReadResponse(), expectedSequence: 0);
        while (true)
        {
            var observation = _verifier.VerifyResponse(response, _decoder);
            if (observation.Sequence != response.FrameSequence)
            {
                throw new InvalidDataException("The verified human observation sequence does not match its finalized frame.");
            }
            _driver.Observe(observation);
            if (response.Terminal)
            {
                _completed = true;
                return new CausalCompletionProof(
                    _receipts,
                    response.FrameSequence,
                    response.FrameSha256,
                    _verifier is AgentPlaytestArtifactOwner,
                    ProofIssuer);
            }

            var request = _driver.Choose(observation);
            ValidateNextRequest(request, response.FrameSequence + 1);
            _receipts.Add(new AgentPlaytestCausalityReceiptView(
                response.FrameSequence,
                response.FrameSha256,
                request.Sequence,
                ActionIdentity(request)));
            _channel.WriteRequest(request);
            response = RequireFrame(_channel.ReadResponse(), request.Sequence);
        }
    }

    private static string ActionIdentity(AgentPlaytestRequest request) =>
        Convert.ToHexString(SHA256.HashData(AgentPlaytestNdjson.SerializeRequest(request))).ToLowerInvariant();

    private static bool ReceiptsMatch(
        IReadOnlyList<AgentPlaytestFrameResponse> frames,
        IReadOnlyList<AgentPlaytestAcceptedInterval> intervals,
        IReadOnlyList<AgentPlaytestCausalityReceiptView> receipts)
    {
        if (receipts.Count != intervals.Count || frames.Count != intervals.Count + 1)
        {
            return false;
        }
        for (var index = 0; index < receipts.Count; index++)
        {
            var interval = intervals[index];
            var request = new AgentPlaytestRequest(
                AgentPlaytestLimits.Protocol,
                interval.Sequence,
                interval.RequestedIntervalTicks,
                interval.Players);
            var receipt = receipts[index];
            if (receipt.PriorFrameSequence != index || receipt.RequestSequence != index + 1 ||
                !string.Equals(receipt.PriorFrameSha256, frames[index].FrameSha256, StringComparison.Ordinal) ||
                !string.Equals(receipt.ActionIdentity, ActionIdentity(request), StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static AgentPlaytestFrameResponse RequireFrame(AgentPlaytestResponse response, int expectedSequence)
    {
        ArgumentNullException.ThrowIfNull(response);
        if (response is AgentPlaytestErrorResponse error)
        {
            throw new AgentPlaytestFailure(error.ErrorSequence, error.Stage, error.Code, "The owner received an error response.");
        }
        if (response is not AgentPlaytestFrameResponse frame || frame.FrameSequence != expectedSequence)
        {
            throw new InvalidDataException("The owner received an out-of-order or non-frame response.");
        }
        return frame;
    }

    private static void ValidateNextRequest(AgentPlaytestRequest request, int expectedSequence)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Protocol != AgentPlaytestLimits.Protocol || request.Sequence != expectedSequence ||
            request.IntervalTicks is < AgentPlaytestLimits.MinimumIntervalTicks or > AgentPlaytestLimits.MaximumIntervalTicks ||
            request.Players.Count != AgentPlaytestLimits.PlayerCount ||
            request.Players.Any(static player => !player.IsLegal))
        {
            throw new InvalidDataException("The human driver returned an illegal or out-of-order request.");
        }
    }
}
