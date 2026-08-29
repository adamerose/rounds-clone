using System.Globalization;

namespace Rounds.Game;

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

    public static string CompleteMarker(int width, int height) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{CompletePrefix} width={width} height={height}");

    public static string ErrorMarker(string stage, int code) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{ErrorPrefix} stage={stage} code={code}");
}
