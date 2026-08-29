namespace Rounds.Game;

internal enum StartupMode
{
    Match,
    Replay,
    DebugIncompleteFidelityEvidence,
    DebugAgentPlaytest,
}

internal readonly record struct StartupRoute(
    StartupMode Mode,
    string? ReplayPath,
    string? DebugEvidenceOutputPath,
    string? DebugAgentPlaytestOutputRoot = null)
{
    internal const string Usage = "Usage: Rounds.Game -- --replay <path>";
    internal const string DebugIncompleteFidelityEvidenceArgument =
        "--debug-incomplete-fidelity-evidence";
    internal const string DebugAgentPlaytestArgument = "--debug-agent-playtest";

    public bool RunsContinuousPhysics =>
        Mode is not (StartupMode.DebugIncompleteFidelityEvidence or StartupMode.DebugAgentPlaytest);

    public static StartupRoute Parse(ReadOnlySpan<string> arguments, bool allowDebugEvidence)
    {
        if (arguments.Length == 0)
        {
            return new StartupRoute(StartupMode.Match, null, null);
        }
        if (arguments.Length == 2 && arguments[0] == "--replay")
        {
            return new StartupRoute(StartupMode.Replay, arguments[1], null);
        }
        if (allowDebugEvidence &&
            arguments.Length == 2 &&
            arguments[0] == DebugIncompleteFidelityEvidenceArgument &&
            DebugEvidenceCaptureProtocol.IsValidOutputPath(arguments[1]))
        {
            return new StartupRoute(StartupMode.DebugIncompleteFidelityEvidence, null, arguments[1]);
        }
        if (allowDebugEvidence &&
            arguments.Length == 2 &&
            arguments[0] == DebugAgentPlaytestArgument &&
            AgentPlaytestOutputRoot.TryNormalizeAbsentChild(arguments[1], out var normalizedAgentRoot))
        {
            return new StartupRoute(StartupMode.DebugAgentPlaytest, null, null, normalizedAgentRoot);
        }

        throw new ArgumentException(Usage, nameof(arguments));
    }

    internal static bool IsValidAbsentAbsoluteDirectory(string? path)
        => AgentPlaytestOutputRoot.TryNormalizeAbsentChild(path, out _);
}
