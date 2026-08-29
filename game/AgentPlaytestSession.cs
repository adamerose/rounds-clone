using Rounds.Sim;

namespace Rounds.Game;

internal sealed record AgentPlaytestAcceptedInterval(
    int Sequence,
    int RequestedIntervalTicks,
    int AcceptedIntervalTicks,
    IReadOnlyList<AgentPlaytestPlayerAction> Players,
    IReadOnlyList<ulong> TickHashes,
    bool Terminal);

internal sealed class AgentPlaytestFailure : Exception
{
    public AgentPlaytestFailure(int? sequence, string stage, string code, string message)
        : base(message)
    {
        Response = AgentPlaytestErrors.Create(sequence, stage, code);
    }

    public AgentPlaytestErrorResponse Response { get; }
}

internal sealed class AgentPlaytestSession
{
    private readonly FaithfulSubsetMatchShell _shell;
    private readonly List<AgentPlaytestAcceptedInterval> _accepted = [];
    private int _tickCount;

    public AgentPlaytestSession() : this(new FaithfulSubsetMatchShell(Match.Create(1UL))) { }

    private AgentPlaytestSession(FaithfulSubsetMatchShell shell)
    {
        _shell = shell;
    }

    public int Sequence { get; private set; }
    public int TickCount => _tickCount;
    public bool IsTerminal => _shell.IsAtIncompleteFidelityBoundary;
    public Match Match => _shell.Match;
    public IReadOnlyList<AgentPlaytestAcceptedInterval> Accepted => _accepted.AsReadOnly();

    public AgentPlaytestAcceptedInterval Apply(AgentPlaytestRequest request, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (elapsed > TimeSpan.FromSeconds(AgentPlaytestLimits.RouteTimeoutSeconds))
        {
            throw new AgentPlaytestFailure(request.Sequence, "lifecycle", "timeout", "The in-route playtest deadline expired.");
        }
        if (request.Sequence != Sequence + 1)
        {
            throw new AgentPlaytestFailure(request.Sequence, "sequence", "invalid-sequence", "The request sequence was duplicated, skipped, or out of order.");
        }
        if (IsTerminal || Match.Phase == MatchPhase.MatchResult)
        {
            throw new AgentPlaytestFailure(request.Sequence, "terminal", "invalid-terminal", "The playtest received an action after a terminal state.");
        }
        if (request.Protocol != AgentPlaytestLimits.Protocol || request.Players.Count != 2 ||
            request.Players.Any(static player => !player.IsLegal) ||
            request.IntervalTicks is < 1 or > AgentPlaytestLimits.MaximumIntervalTicks)
        {
            throw new AgentPlaytestFailure(request.Sequence, "request-validate", "invalid-schema", "The request does not satisfy protocol v1.");
        }
        if (_accepted.Count >= AgentPlaytestLimits.MaximumRequests ||
            _tickCount + request.IntervalTicks > AgentPlaytestLimits.MaximumSimulationTicks)
        {
            throw new AgentPlaytestFailure(request.Sequence, "resource", "resource-limit-exceeded", "The bounded request or tick budget would be exceeded.");
        }

        var inputs = request.Players.Select(static action => action.ToPlayerInput()).ToArray();
        var hashes = new List<ulong>(request.IntervalTicks);
        try
        {
            for (var tick = 0; tick < request.IntervalTicks && !_shell.IsAtIncompleteFidelityBoundary; tick++)
            {
                _shell.Step(inputs);
                _tickCount++;
                hashes.Add(Match.Hash(Match));
                if (Match.World.Bullets.Count > AgentPlaytestLimits.LiveBulletCap)
                {
                    throw new AgentPlaytestFailure(request.Sequence, "resource", "resource-limit-exceeded", "The integrated live-bullet cap was exceeded.");
                }
                if (Match.Phase == MatchPhase.MatchResult)
                {
                    throw new AgentPlaytestFailure(request.Sequence, "terminal", "invalid-terminal", "The playtest reached MatchResult instead of the supported boundary.");
                }
                if (_shell.IsAtIncompleteFidelityBoundary)
                {
                    break;
                }
            }
        }
        catch (AgentPlaytestFailure)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new AgentPlaytestFailure(request.Sequence, "simulation", "simulation-failed", exception.Message);
        }

        Sequence = request.Sequence;
        var accepted = new AgentPlaytestAcceptedInterval(
            request.Sequence,
            request.IntervalTicks,
            hashes.Count,
            Array.AsReadOnly(request.Players.ToArray()),
            hashes.AsReadOnly(),
            IsTerminal);
        _accepted.Add(accepted);
        return accepted;
    }

    public static void VerifyFreshReplay(IReadOnlyList<AgentPlaytestAcceptedInterval> trace)
    {
        ArgumentNullException.ThrowIfNull(trace);
        var replay = new AgentPlaytestSession();
        foreach (var expected in trace)
        {
            AgentPlaytestAcceptedInterval actual;
            try
            {
                actual = replay.Apply(
                    new AgentPlaytestRequest(
                        AgentPlaytestLimits.Protocol,
                        expected.Sequence,
                        expected.RequestedIntervalTicks,
                        expected.Players),
                    TimeSpan.Zero);
            }
            catch (AgentPlaytestFailure failure)
            {
                throw new AgentPlaytestFailure(expected.Sequence, "replay", "replay-mismatch", failure.Message);
            }
            if (actual.AcceptedIntervalTicks != expected.AcceptedIntervalTicks ||
                !actual.TickHashes.SequenceEqual(expected.TickHashes) || actual.Terminal != expected.Terminal)
            {
                throw new AgentPlaytestFailure(expected.Sequence, "replay", "replay-mismatch", "Fresh-shell replay did not reproduce the trace.");
            }
        }
    }
}
