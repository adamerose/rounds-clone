using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;

namespace Rounds.Game;

internal readonly record struct EvidencePixelBounds(int X, int Y, int Width, int Height)
{
    public bool Contains(EvidencePixelBounds child) =>
        Width > 0 && Height > 0 && child.Width > 0 && child.Height > 0 &&
        child.X >= X && child.Y >= Y &&
        (long)child.X + child.Width <= (long)X + Width &&
        (long)child.Y + child.Height <= (long)Y + Height;
}

internal sealed record EvidenceMonitorFacts(
    string DeviceName,
    int Ordinal,
    EvidencePixelBounds PhysicalBounds,
    bool PerMonitorV2DpiAware);

internal sealed record EvidenceFileFacts(
    string Path,
    bool Exists,
    bool IsReparsePoint,
    string Sha256,
    string FileVersion,
    string ProductVersion);

internal sealed record EvidenceToolchainFacts(
    EvidenceFileFacts MsBuild,
    string SdkVersion,
    bool RollForwardDisabled,
    bool SdkDirectoryExists,
    bool ReferencePackDirectoryExists,
    bool LockedAssetsExist);

internal sealed record EvidenceRuntimeAssemblyFacts(
    string Path,
    bool Exists,
    bool RecreatedByImmediateRebuild,
    bool BuildHadZeroWarnings,
    string Sha256,
    string Mvid);

internal sealed record EvidenceAncestorIdentityFacts(
    string RequestedPath,
    string IdentityResolvedCanonicalPath,
    bool Exists,
    bool IsReparsePoint,
    bool IdentityBound);

internal sealed record EvidenceOutputRootFacts(
    string Root,
    bool RootAbsent,
    IReadOnlyList<EvidenceAncestorIdentityFacts> Ancestors);

internal sealed record BaseProjectileEvidenceLaunchFacts(
    string RepositoryRoot,
    string CandidateCommit,
    bool CandidateIsCleanHead,
    EvidenceMonitorFacts Monitor,
    EvidenceFileFacts Godot,
    EvidenceToolchainFacts Toolchain,
    EvidenceRuntimeAssemblyFacts RuntimeAssembly,
    EvidenceOutputRootFacts Output,
    string OperatingSystemTemporaryDirectory,
    string InputDesktopIdentity);

internal sealed record BaseProjectileEvidenceJobLimits(
    uint AffinityMask,
    int ActiveProcessLimit,
    long ProcessCommitBytes,
    long JobCommitBytes,
    bool BelowNormalPriority,
    bool KillOnJobClose);

internal sealed record BaseProjectileEvidenceLaunchPlan(
    string CandidateCommit,
    string RepositoryRoot,
    string Executable,
    string Desktop,
    int Screen,
    EvidencePixelBounds MonitorBounds,
    EvidencePixelBounds WindowBounds,
    string OutputRoot,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment,
    BaseProjectileEvidenceJobLimits JobLimits,
    TimeSpan Deadline,
    int StandardOutputCapBytes,
    int StandardErrorCapBytes,
    string RuntimeAssemblySha256,
    string RuntimeAssemblyMvid,
    string InputDesktopIdentity,
    IReadOnlyList<EvidenceAncestorIdentityFacts> OutputAncestors)
{
    public string CommandLine => WindowsArgumentEncoding.Encode(
        new[] { Executable }.Concat(Arguments).ToArray());
}

internal readonly record struct BaseProjectileEvidencePlanDecision(
    BaseProjectileEvidenceLaunchPlan? Plan,
    string? Refusal)
{
    public bool Accepted => Plan is not null && Refusal is null;
}

internal static class BaseProjectileEvidenceLaunchPlanner
{
    internal const string DisplayDevice = @"\\.\DISPLAY4";
    internal const int Screen = 3;
    internal const string GodotRelativePath = @".tools\godot-4.7.1\Godot_v4.7.1-stable_mono_win64\Godot_v4.7.1-stable_mono_win64_console.exe";
    internal const string GodotVersion = "4.7.1.stable.mono.official";
    internal const string GodotSha256 = "b2c334ff6bf1e07ded41b80bd6f4785485650db6ddbb2740b802930f35237c26";
    internal const string MsBuildPath = @"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe";
    internal const string MsBuildFileVersion = "17.14.40.60911";
    internal const string MsBuildProductVersion = "17.14.40+3e744208875e56e4bf0bc22c40a1c431fb150987";
    internal const string MsBuildSha256 = "0f7daba445ba37b652ae4180270ab3eff27acbbe5d411d3b05dad51aab16404c";
    internal const string SdkVersion = "8.0.423";
    internal static readonly EvidencePixelBounds RequiredMonitorBounds = new(364, -1080, 1920, 1080);
    internal static readonly EvidencePixelBounds RequiredWindowBounds = new(684, -900, 1280, 720);
    private static readonly Regex LowerHex40 = new("^[0-9a-f]{40}$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex LowerHex64 = new("^[0-9a-f]{64}$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex LowerHex32 = new("^[0-9a-f]{32}$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static BaseProjectileEvidencePlanDecision Create(
        BaseProjectileEvidenceLaunchFacts facts,
        string nonce)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (!LowerHex32.IsMatch(nonce))
        {
            return Refuse("nonce");
        }
        if (!TryFullPath(facts.RepositoryRoot, out var repository) ||
            !DirectoryShape(repository) || !facts.CandidateIsCleanHead ||
            !LowerHex40.IsMatch(facts.CandidateCommit))
        {
            return Refuse("candidate");
        }
        if (!facts.Monitor.PerMonitorV2DpiAware ||
            !string.Equals(facts.Monitor.DeviceName, DisplayDevice, StringComparison.Ordinal) ||
            facts.Monitor.Ordinal != Screen ||
            facts.Monitor.PhysicalBounds != RequiredMonitorBounds ||
            !facts.Monitor.PhysicalBounds.Contains(RequiredWindowBounds))
        {
            return Refuse("topology");
        }

        var expectedGodot = Path.GetFullPath(Path.Combine(repository, GodotRelativePath));
        if (!ValidPinnedFile(facts.Godot, expectedGodot, GodotSha256) ||
            !string.Equals(facts.Godot.ProductVersion, GodotVersion, StringComparison.Ordinal))
        {
            return Refuse("godot");
        }
        if (!ValidPinnedFile(facts.Toolchain.MsBuild, Path.GetFullPath(MsBuildPath), MsBuildSha256) ||
            !string.Equals(facts.Toolchain.MsBuild.FileVersion, MsBuildFileVersion, StringComparison.Ordinal) ||
            !string.Equals(facts.Toolchain.MsBuild.ProductVersion, MsBuildProductVersion, StringComparison.Ordinal) ||
            !string.Equals(facts.Toolchain.SdkVersion, SdkVersion, StringComparison.Ordinal) ||
            !facts.Toolchain.RollForwardDisabled || !facts.Toolchain.SdkDirectoryExists ||
            !facts.Toolchain.ReferencePackDirectoryExists || !facts.Toolchain.LockedAssetsExist)
        {
            return Refuse("toolchain");
        }

        var expectedAssembly = Path.GetFullPath(Path.Combine(repository, @"game\.godot\mono\temp\bin\Debug\Rounds.Game.dll"));
        if (!TryFullPath(facts.RuntimeAssembly.Path, out var runtimePath) ||
            !string.Equals(runtimePath, expectedAssembly, StringComparison.OrdinalIgnoreCase) ||
            !facts.RuntimeAssembly.Exists || !facts.RuntimeAssembly.RecreatedByImmediateRebuild ||
            !facts.RuntimeAssembly.BuildHadZeroWarnings || !LowerHex64.IsMatch(facts.RuntimeAssembly.Sha256) ||
            !LowerHex32.IsMatch(facts.RuntimeAssembly.Mvid))
        {
            return Refuse("runtime-assembly");
        }

        if (!TryFullPath(facts.Output.Root, out var outputRoot) || !facts.Output.RootAbsent ||
            !TryFullPath(facts.OperatingSystemTemporaryDirectory, out var temporary) ||
            IsWithin(outputRoot, repository) || IsWithin(outputRoot, temporary) ||
            !HasSafeCompleteAncestorProof(outputRoot, facts.Output.Ancestors, repository, temporary))
        {
            return Refuse("output-root");
        }
        if (string.IsNullOrWhiteSpace(facts.InputDesktopIdentity))
        {
            return Refuse("input-desktop");
        }

        var gameDirectory = Path.GetFullPath(Path.Combine(repository, "game"));
        var arguments = Array.AsReadOnly(new[]
        {
            "--quiet",
            "--path", gameDirectory,
            "--screen", "3",
            "--position", "684,-900",
            "--resolution", "1280x720",
            "--windowed",
            "--audio-driver", "Dummy",
            "--rendering-method", "gl_compatibility",
            "--",
            DebugEvidenceCaptureProtocol.BaseProjectileArgument,
            outputRoot,
        });
        var desktop = "RoundsEvidence-" + nonce;
        var environment = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DOTNET_PROCESSOR_COUNT"] = "2",
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["MSBuildEnableWorkloadResolver"] = "false",
            ["MSBuildSDKsPath"] = Path.GetFullPath(Path.Combine(repository, @".tools\dotnet\sdk\8.0.423\Sdks")),
            [DebugEvidenceCaptureProtocol.EvidenceDesktopEnvironmentVariable] = desktop,
        });
        return new BaseProjectileEvidencePlanDecision(
            new BaseProjectileEvidenceLaunchPlan(
                facts.CandidateCommit,
                repository,
                expectedGodot,
                desktop,
                Screen,
                RequiredMonitorBounds,
                RequiredWindowBounds,
                outputRoot,
                arguments,
                environment,
                new BaseProjectileEvidenceJobLimits(
                    0x3,
                    1,
                    768L * 1024L * 1024L,
                    1024L * 1024L * 1024L,
                    BelowNormalPriority: true,
                    KillOnJobClose: true),
                TimeSpan.FromSeconds(30),
                8 * 1024,
                64 * 1024,
                facts.RuntimeAssembly.Sha256,
                facts.RuntimeAssembly.Mvid,
                facts.InputDesktopIdentity,
                Array.AsReadOnly(facts.Output.Ancestors.ToArray())),
            null);
    }

    public static bool MarkerMatchesPlan(
        string stdout,
        BaseProjectileEvidenceLaunchPlan plan,
        out DebugBaseProjectileEvidenceAttestation attestation)
    {
        if (!DebugEvidenceCaptureProtocol.TryParseBaseProjectileCompletion(stdout, out attestation))
        {
            return false;
        }
        return attestation.StateHash == 0x6a25f798f6582a29UL && attestation.BulletId == 0 &&
            attestation.OwnerId == 0 && string.Equals(attestation.Desktop, plan.Desktop, StringComparison.Ordinal) &&
            attestation.Capture.Screen == plan.Screen && attestation.Capture.WindowX == plan.WindowBounds.X &&
            attestation.Capture.WindowY == plan.WindowBounds.Y &&
            attestation.Capture.WindowWidth == plan.WindowBounds.Width &&
            attestation.Capture.WindowHeight == plan.WindowBounds.Height &&
            attestation.Capture.ViewportWidth == DebugEvidenceCaptureProtocol.EvidenceViewportWidth &&
            attestation.Capture.ViewportHeight == DebugEvidenceCaptureProtocol.EvidenceViewportHeight &&
            string.Equals(attestation.AssemblySha256, plan.RuntimeAssemblySha256, StringComparison.Ordinal) &&
            string.Equals(attestation.AssemblyMvid, plan.RuntimeAssemblyMvid, StringComparison.Ordinal) &&
            string.Equals(attestation.Frame, "frame-0000.png", StringComparison.Ordinal);
    }

    private static bool ValidPinnedFile(EvidenceFileFacts facts, string expectedPath, string expectedSha256) =>
        TryFullPath(facts.Path, out var actualPath) && facts.Exists && !facts.IsReparsePoint &&
        string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(facts.Sha256, expectedSha256, StringComparison.Ordinal);

    private static bool DirectoryShape(string path) =>
        !string.Equals(Path.GetPathRoot(path), path, StringComparison.OrdinalIgnoreCase);

    private static bool TryFullPath(string? path, out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return false;
        }
        try
        {
            fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsWithin(string candidate, string parent)
    {
        var relative = Path.GetRelativePath(parent, candidate);
        return relative == "." ||
            (!string.Equals(relative, "..", StringComparison.Ordinal) &&
             !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
             !Path.IsPathFullyQualified(relative));
    }

    private static bool HasSafeCompleteAncestorProof(
        string outputRoot,
        IReadOnlyList<EvidenceAncestorIdentityFacts>? ancestors,
        string repository,
        string temporary)
    {
        if (ancestors is null)
        {
            return false;
        }
        var expected = EnumerateAncestors(outputRoot);
        if (ancestors.Count != expected.Count)
        {
            return false;
        }
        string? previousResolved = null;
        for (var index = 0; index < expected.Count; index++)
        {
            var proof = ancestors[index];
            if (!TryFullPath(proof.RequestedPath, out var requested) ||
                !string.Equals(requested, expected[index], StringComparison.OrdinalIgnoreCase) ||
                !TryFullPath(proof.IdentityResolvedCanonicalPath, out var resolved) ||
                !proof.Exists || proof.IsReparsePoint || !proof.IdentityBound ||
                IsWithin(resolved, repository) || IsWithin(resolved, temporary))
            {
                return false;
            }
            var canonicalParent = Directory.GetParent(resolved)?.FullName;
            if (index == 0)
            {
                if (canonicalParent is not null)
                {
                    return false;
                }
            }
            else if (!string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(canonicalParent!)),
                previousResolved,
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            previousResolved = resolved;
        }

        var resolvedParent = Path.GetFullPath(ancestors[^1].IdentityResolvedCanonicalPath);
        var resolvedOutput = Path.GetFullPath(Path.Combine(resolvedParent, Path.GetFileName(outputRoot)));
        return !IsWithin(resolvedOutput, repository) && !IsWithin(resolvedOutput, temporary);
    }

    private static IReadOnlyList<string> EnumerateAncestors(string outputRoot)
    {
        var ancestors = new List<string>();
        var current = Directory.GetParent(outputRoot);
        while (current is not null)
        {
            ancestors.Add(Path.TrimEndingDirectorySeparator(Path.GetFullPath(current.FullName)));
            current = current.Parent;
        }
        ancestors.Reverse();
        return ancestors;
    }

    private static BaseProjectileEvidencePlanDecision Refuse(string code) => new(null, code);
}

internal static class WindowsArgumentEncoding
{
    public static string Encode(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return string.Join(' ', arguments.Select(EncodeOne));
    }

    private static string EncodeOne(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        if (argument.Length > 0 && !argument.Any(static character => char.IsWhiteSpace(character) || character == '"'))
        {
            return argument;
        }

        var encoded = new StringBuilder(argument.Length + 2).Append('"');
        var slashCount = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                slashCount++;
                continue;
            }
            if (character == '"')
            {
                encoded.Append('\\', checked((slashCount * 2) + 1)).Append('"');
                slashCount = 0;
                continue;
            }
            encoded.Append('\\', slashCount).Append(character);
            slashCount = 0;
        }
        encoded.Append('\\', checked(slashCount * 2)).Append('"');
        return encoded.ToString();
    }

    public static IReadOnlyList<string> DecodeModel(string commandLine)
    {
        ArgumentNullException.ThrowIfNull(commandLine);
        var arguments = new List<string>();
        var index = 0;
        while (index < commandLine.Length)
        {
            while (index < commandLine.Length && char.IsWhiteSpace(commandLine[index]))
            {
                index++;
            }
            if (index == commandLine.Length)
            {
                break;
            }

            var argument = new StringBuilder();
            var inQuotes = false;
            var started = false;
            while (index < commandLine.Length && (inQuotes || !char.IsWhiteSpace(commandLine[index])))
            {
                started = true;
                var slashCount = 0;
                while (index < commandLine.Length && commandLine[index] == '\\')
                {
                    slashCount++;
                    index++;
                }
                if (index < commandLine.Length && commandLine[index] == '"')
                {
                    argument.Append('\\', slashCount / 2);
                    if ((slashCount & 1) == 0)
                    {
                        inQuotes = !inQuotes;
                    }
                    else
                    {
                        argument.Append('"');
                    }
                    index++;
                    continue;
                }
                argument.Append('\\', slashCount);
                if (index < commandLine.Length && (inQuotes || !char.IsWhiteSpace(commandLine[index])))
                {
                    argument.Append(commandLine[index]);
                    index++;
                }
            }
            if (inQuotes)
            {
                throw new FormatException("The modeled Windows command line has an unmatched quote.");
            }
            if (started)
            {
                arguments.Add(argument.ToString());
            }
        }
        return arguments;
    }
}
