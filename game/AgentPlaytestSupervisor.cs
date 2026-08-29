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

    public IReadOnlyList<AgentPlaytestCausalityReceiptView> RunToTerminal()
    {
        if (_completed)
        {
            throw new InvalidOperationException("The causal supervisor can run at most once.");
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
                return Array.AsReadOnly(_receipts.ToArray());
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
