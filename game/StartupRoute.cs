namespace Rounds.Game;

internal enum StartupMode
{
    Match,
    Replay,
    DebugIncompleteFidelityEvidence,
}

internal readonly record struct StartupRoute(
    StartupMode Mode,
    string? ReplayPath,
    string? DebugEvidenceOutputPath)
{
    internal const string Usage = "Usage: Rounds.Game -- --replay <path>";
    internal const string DebugIncompleteFidelityEvidenceArgument =
        "--debug-incomplete-fidelity-evidence";

    public bool RunsContinuousPhysics => Mode != StartupMode.DebugIncompleteFidelityEvidence;

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

        throw new ArgumentException(Usage, nameof(arguments));
    }
}
