namespace Rounds.Game;

internal enum StartupMode
{
    Match,
    Replay,
    DebugIncompleteFidelityEvidence,
}

internal readonly record struct StartupRoute(StartupMode Mode, string? ReplayPath)
{
    internal const string Usage = "Usage: Rounds.Game -- --replay <path>";
    internal const string DebugIncompleteFidelityEvidenceArgument =
        "--debug-incomplete-fidelity-evidence";

    public bool RunsContinuousPhysics => Mode != StartupMode.DebugIncompleteFidelityEvidence;

    public static StartupRoute Parse(ReadOnlySpan<string> arguments, bool allowDebugEvidence)
    {
        if (arguments.Length == 0)
        {
            return new StartupRoute(StartupMode.Match, null);
        }
        if (arguments.Length == 2 && arguments[0] == "--replay")
        {
            return new StartupRoute(StartupMode.Replay, arguments[1]);
        }
        if (allowDebugEvidence &&
            arguments.Length == 1 &&
            arguments[0] == DebugIncompleteFidelityEvidenceArgument)
        {
            return new StartupRoute(StartupMode.DebugIncompleteFidelityEvidence, null);
        }

        throw new ArgumentException(Usage, nameof(arguments));
    }
}
