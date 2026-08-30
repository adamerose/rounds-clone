using System.Globalization;
using System.Text.RegularExpressions;

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
    string Desktop,
    DebugEvidenceCaptureAttestation Capture,
    string AssemblySha256,
    string AssemblyMvid,
    string PngSha256,
    string Frame);

internal static class DebugEvidenceCaptureProtocol
{
    internal const string CompletePrefix = "DEBUG_INCOMPLETE_FIDELITY_EVIDENCE_COMPLETE";
    internal const string ErrorPrefix = "DEBUG_INCOMPLETE_FIDELITY_EVIDENCE_ERROR";
    internal const string BaseProjectileCompletePrefix = "DEBUG_BASE_PROJECTILE_EVIDENCE_COMPLETE";
    internal const string BaseProjectileErrorPrefix = "DEBUG_BASE_PROJECTILE_EVIDENCE_ERROR";
    internal const string EvidenceAckHandleEnvironmentVariable = "ROUNDS_EVIDENCE_ACK_HANDLE";
    internal const string EvidenceDesktopEnvironmentVariable = "ROUNDS_EVIDENCE_DESKTOP";
    internal const int EvidenceViewportWidth = 1920;
    internal const int EvidenceViewportHeight = 1080;
    internal const byte EvidenceAcknowledgement = 0x06;
    private static readonly Regex CompletionGrammar = new(
        "^DEBUG_BASE_PROJECTILE_EVIDENCE_COMPLETE stateHash=(?<state>[0-9a-f]{16}) bulletId=(?<bullet>[0-9]+) ownerId=(?<owner>[0-9]+) desktop=(?<desktop>RoundsEvidence-[0-9a-f]{32}) screen=(?<screen>[0-9]+) windowX=(?<x>-?[0-9]+) windowY=(?<y>-?[0-9]+) windowWidth=(?<width>[0-9]+) windowHeight=(?<height>[0-9]+) viewportWidth=(?<viewportWidth>[0-9]+) viewportHeight=(?<viewportHeight>[0-9]+) assemblySha256=(?<assembly>[0-9a-f]{64}) assemblyMvid=(?<mvid>[0-9a-f]{32}) pngSha256=(?<png>[0-9a-f]{64}) frame=(?<frame>frame-0000\\.png)$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

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
            $"{BaseProjectileCompletePrefix} stateHash={attestation.StateHash:x16} bulletId={attestation.BulletId} ownerId={attestation.OwnerId} desktop={attestation.Desktop} screen={attestation.Capture.Screen} windowX={attestation.Capture.WindowX} windowY={attestation.Capture.WindowY} windowWidth={attestation.Capture.WindowWidth} windowHeight={attestation.Capture.WindowHeight} viewportWidth={attestation.Capture.ViewportWidth} viewportHeight={attestation.Capture.ViewportHeight} assemblySha256={attestation.AssemblySha256} assemblyMvid={attestation.AssemblyMvid} pngSha256={attestation.PngSha256} frame={attestation.Frame}");

    public static bool TryParseBaseProjectileCompletion(
        string stdout,
        out DebugBaseProjectileEvidenceAttestation attestation)
    {
        attestation = default;
        if (stdout.Length == 0 || stdout[^1] != '\n' || stdout.Contains('\r') ||
            stdout.AsSpan(0, stdout.Length - 1).IndexOf('\n') >= 0)
        {
            return false;
        }
        var match = CompletionGrammar.Match(stdout[..^1]);
        if (!match.Success ||
            !ulong.TryParse(match.Groups["state"].Value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var state) ||
            !long.TryParse(match.Groups["bullet"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var bullet) ||
            !int.TryParse(match.Groups["owner"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var owner) ||
            !int.TryParse(match.Groups["screen"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var screen) ||
            !int.TryParse(match.Groups["x"].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var x) ||
            !int.TryParse(match.Groups["y"].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var y) ||
            !int.TryParse(match.Groups["width"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var width) ||
            !int.TryParse(match.Groups["height"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var height) ||
            !int.TryParse(match.Groups["viewportWidth"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var viewportWidth) ||
            !int.TryParse(match.Groups["viewportHeight"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var viewportHeight))
        {
            return false;
        }
        attestation = new DebugBaseProjectileEvidenceAttestation(
            state,
            bullet,
            owner,
            match.Groups["desktop"].Value,
            new DebugEvidenceCaptureAttestation(screen, x, y, width, height, viewportWidth, viewportHeight),
            match.Groups["assembly"].Value,
            match.Groups["mvid"].Value,
            match.Groups["png"].Value,
            match.Groups["frame"].Value);
        return string.Equals(BaseProjectileCompleteMarker(attestation), stdout[..^1], StringComparison.Ordinal);
    }

    public static bool IsValidEvidenceDesktop(string? desktop) =>
        desktop is not null && Regex.IsMatch(
            desktop,
            "^RoundsEvidence-[0-9a-f]{32}$",
            RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(100));

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
