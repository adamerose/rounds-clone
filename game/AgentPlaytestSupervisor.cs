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

internal sealed record AgentPlaytestCausalityReceipt(
    int PriorFrameSequence,
    string PriorFrameSha256,
    int RequestSequence,
    string ActionIdentity);

internal static class AgentPlaytestCausality
{
    public static string ActionIdentity(AgentPlaytestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Convert.ToHexString(SHA256.HashData(AgentPlaytestNdjson.SerializeRequest(request))).ToLowerInvariant();
    }

    public static bool ReceiptsMatch(
        IReadOnlyList<AgentPlaytestFrameResponse> frames,
        IReadOnlyList<AgentPlaytestAcceptedInterval> intervals,
        IReadOnlyList<AgentPlaytestCausalityReceipt> receipts)
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
            if (receipt.PriorFrameSequence != index ||
                receipt.RequestSequence != index + 1 ||
                !string.Equals(receipt.PriorFrameSha256, frames[index].FrameSha256, StringComparison.Ordinal) ||
                !string.Equals(receipt.ActionIdentity, ActionIdentity(request), StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }
}

internal sealed class AgentPlaytestOwnerSupervisor
{
    private readonly IAgentPlaytestOwnerChannel _channel;
    private readonly IAgentPlaytestFinalizedFrameVerifier _verifier;
    private readonly IAgentPlaytestRgba8Decoder _decoder;
    private readonly IHumanPlaytestDriver _driver;
    private readonly List<AgentPlaytestCausalityReceipt> _receipts = [];

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

    public IReadOnlyList<AgentPlaytestCausalityReceipt> CausalityReceipts => _receipts.AsReadOnly();

    public IReadOnlyList<AgentPlaytestCausalityReceipt> RunToTerminal()
    {
        var response = RequireFrame(_channel.ReadResponse(), expectedSequence: 0);
        while (!response.Terminal)
        {
            var observation = _verifier.VerifyResponse(response, _decoder);
            if (observation.Sequence != response.FrameSequence)
            {
                throw new InvalidDataException("The verified human observation sequence does not match its finalized frame.");
            }

            var request = _driver.Choose(observation);
            ValidateNextRequest(request, response.FrameSequence + 1);
            _receipts.Add(new AgentPlaytestCausalityReceipt(
                response.FrameSequence,
                response.FrameSha256,
                request.Sequence,
                AgentPlaytestCausality.ActionIdentity(request)));
            _channel.WriteRequest(request);
            response = RequireFrame(_channel.ReadResponse(), request.Sequence);
        }
        return Array.AsReadOnly(_receipts.ToArray());
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
