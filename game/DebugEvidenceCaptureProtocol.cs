using System.Globalization;

namespace Rounds.Game;

internal readonly record struct DebugEvidenceCaptureAttestation(
    int Screen,
    int WindowX,
    int WindowY,
    int WindowWidth,
    int WindowHeight,
    int ViewportWidth,
    int ViewportHeight);

internal static class DebugEvidenceCaptureProtocol
{
    internal const string CompletePrefix = "DEBUG_INCOMPLETE_FIDELITY_EVIDENCE_COMPLETE";
    internal const string ErrorPrefix = "DEBUG_INCOMPLETE_FIDELITY_EVIDENCE_ERROR";

    public static bool IsValidOutputPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }
        try
        {
            return Path.IsPathFullyQualified(path) &&
                string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static string CompleteMarker(DebugEvidenceCaptureAttestation attestation) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{CompletePrefix} screen={attestation.Screen} windowX={attestation.WindowX} windowY={attestation.WindowY} windowWidth={attestation.WindowWidth} windowHeight={attestation.WindowHeight} viewportWidth={attestation.ViewportWidth} viewportHeight={attestation.ViewportHeight}");

    public static string ErrorMarker(string stage, int code) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{ErrorPrefix} stage={stage} code={code}");

    public static string WrongScreenMarker(int screen, int expectedScreen) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{ErrorPrefix} stage=wrong-screen screen={screen} expectedScreen={expectedScreen}");
}
