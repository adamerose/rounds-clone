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

internal readonly record struct DebugBaseProjectileEvidenceAttestation(
    ulong StateHash,
    long BulletId,
    int OwnerId,
    DebugEvidenceCaptureAttestation Capture);

internal static class DebugEvidenceCaptureProtocol
{
    internal const string CompletePrefix = "DEBUG_INCOMPLETE_FIDELITY_EVIDENCE_COMPLETE";
    internal const string ErrorPrefix = "DEBUG_INCOMPLETE_FIDELITY_EVIDENCE_ERROR";
    internal const string BaseProjectileCompletePrefix = "DEBUG_BASE_PROJECTILE_EVIDENCE_COMPLETE";
    internal const string BaseProjectileErrorPrefix = "DEBUG_BASE_PROJECTILE_EVIDENCE_ERROR";

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

    public static string BaseProjectileCompleteMarker(DebugBaseProjectileEvidenceAttestation attestation) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{BaseProjectileCompletePrefix} stateHash={attestation.StateHash:x16} bulletId={attestation.BulletId} ownerId={attestation.OwnerId} screen={attestation.Capture.Screen} windowX={attestation.Capture.WindowX} windowY={attestation.Capture.WindowY} windowWidth={attestation.Capture.WindowWidth} windowHeight={attestation.Capture.WindowHeight} viewportWidth={attestation.Capture.ViewportWidth} viewportHeight={attestation.Capture.ViewportHeight}");

    public static string BaseProjectileErrorMarker(string stage, int code) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{BaseProjectileErrorPrefix} stage={stage} code={code}");

    public static string BaseProjectileWrongScreenMarker(int screen, int expectedScreen) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{BaseProjectileErrorPrefix} stage=wrong-screen screen={screen} expectedScreen={expectedScreen}");

    public static void PublishPngCreateNew(string temporaryPngPath, string outputPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(temporaryPngPath);
        if (!IsValidOutputPath(outputPath))
        {
            throw new ArgumentException("Evidence output must be an absolute PNG path.", nameof(outputPath));
        }
        File.Move(temporaryPngPath, outputPath, overwrite: false);
    }
}
