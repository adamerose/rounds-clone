using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Rounds.Game;

namespace Rounds.EvidenceLauncher;

internal sealed record EvidenceFrozenBuildProcessRequest(
    EvidenceBuildInvocation Invocation,
    IReadOnlyDictionary<string, string> EffectiveEnvironment,
    string ApplicationName,
    string WorkingDirectory,
    IReadOnlyList<string> CompleteArgumentVector,
    string CommandLine,
    string UnicodeEnvironmentBlock,
    bool InheritAmbientEnvironment,
    bool StartSuspended,
    EvidenceBuildJobLimits JobLimits,
    bool UseShellExecute,
    bool CreateNoWindow,
    bool HiddenWindow,
    bool BelowNormalPriority,
    TimeSpan Deadline,
    int StandardOutputCapBytes,
    int StandardErrorCapBytes);

internal enum EvidenceMsBuildSummaryStream
{
    StandardOutput,
    StandardError,
}

internal sealed record EvidenceMsBuildOutputProof(
    int WarningCount,
    int ErrorCount,
    EvidenceMsBuildSummaryStream SummaryStream,
    int StandardOutputLineCount,
    int StandardErrorLineCount);

/// <summary>
/// Pure compilation and validation for the dormant direct MSBuild process boundary.
/// This type never reads the ambient environment and never starts or owns a process.
/// </summary>
internal static class EvidenceBuildProcessPrimitives
{
    internal const int StreamCapBytes = 4 * 1024 * 1024;
    internal const int MaximumWindowsCommandLineCharactersIncludingTerminator = 32_767;
    internal const int MaximumWindowsEnvironmentCharactersIncludingTerminators = 32_767;

    private const uint RequiredAffinityMask = 0x3;
    private const int RequiredActiveProcessLimit = 1;
    private const long RequiredProcessCommitBytes = 768L * 1024 * 1024;
    private const long RequiredJobCommitBytes = 1024L * 1024 * 1024;

    private static readonly string[] RequiredArguments =
    [
        @"game\Rounds.Game.csproj",
        "/noAutoResponse",
        "/t:Rebuild",
        "/p:Configuration=Debug",
        "/p:Restore=false",
        "/p:UseSharedCompilation=false",
        "/p:BuildProjectReferences=true",
        "/m:1",
        "/nr:false",
        "/v:minimal",
        "/warnaserror",
    ];

    private static readonly string[] RequiredEnvironmentKeys =
    [
        "SystemRoot", "WINDIR", "TEMP", "TMP",
        "DOTNET_PROCESSOR_COUNT", "MSBUILDDISABLENODEREUSE",
        "MSBuildEnableWorkloadResolver", "MSBuildSDKsPath",
        "DOTNET_CLI_UI_LANGUAGE", "VSLANG", "NUGET_PACKAGES",
        "DOTNET_CLI_HOME", "MSBuildUserExtensionsPath",
    ];

    internal static EvidenceFrozenBuildProcessRequest Compile(EvidenceBuildProcessRequest source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Capture each top-level property before inspecting any nested mutable collection.
        var invocationSource = source.Invocation;
        var effectiveEnvironmentSource = source.EffectiveEnvironment;
        var inheritAmbientEnvironment = source.InheritAmbientEnvironment;
        var startSuspended = source.StartSuspended;
        var jobLimitsSource = source.JobLimits;
        var useShellExecute = source.UseShellExecute;
        var createNoWindow = source.CreateNoWindow;
        var hiddenWindow = source.HiddenWindow;
        var belowNormalPriority = source.BelowNormalPriority;
        var deadline = source.Deadline;
        var standardOutputCapBytes = source.StandardOutputCapBytes;
        var standardErrorCapBytes = source.StandardErrorCapBytes;

        ArgumentNullException.ThrowIfNull(invocationSource);
        ArgumentNullException.ThrowIfNull(effectiveEnvironmentSource);
        ArgumentNullException.ThrowIfNull(jobLimitsSource);

        var executable = invocationSource.Executable;
        var workingDirectory = invocationSource.WorkingDirectory;
        var argumentSource = invocationSource.Arguments;
        var invocationEnvironmentSource = invocationSource.Environment;
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(workingDirectory);
        ArgumentNullException.ThrowIfNull(argumentSource);
        ArgumentNullException.ThrowIfNull(invocationEnvironmentSource);

        var affinityMask = jobLimitsSource.AffinityMask;
        var activeProcessLimit = jobLimitsSource.ActiveProcessLimit;
        var processCommitBytes = jobLimitsSource.ProcessCommitBytes;
        var jobCommitBytes = jobLimitsSource.JobCommitBytes;
        var killOnJobClose = jobLimitsSource.KillOnJobClose;

        var arguments = SnapshotArguments(argumentSource);
        var invocationEnvironment = SnapshotEnvironment(invocationEnvironmentSource, "build invocation");
        var effectiveEnvironment = SnapshotEnvironment(effectiveEnvironmentSource, "effective build");

        var canonicalExecutable = RequireCanonicalFilePath(executable, "MSBuild application name");
        if (!string.Equals(canonicalExecutable, BaseProjectileEvidenceLaunchPlanner.MsBuildPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("MSBuild application name did not match the pinned executable.");
        }

        var canonicalWorkingDirectory = RequireCanonicalDirectoryPath(workingDirectory, "build working directory");
        ValidateArguments(arguments);
        ValidateInvocationEnvironment(invocationEnvironment, canonicalWorkingDirectory);
        ValidateEffectiveEnvironment(effectiveEnvironment, invocationEnvironment, canonicalWorkingDirectory);

        if (inheritAmbientEnvironment || !startSuspended || useShellExecute || !createNoWindow || !hiddenWindow ||
            !belowNormalPriority || deadline != TimeSpan.FromMinutes(5) ||
            standardOutputCapBytes != StreamCapBytes || standardErrorCapBytes != StreamCapBytes ||
            affinityMask != RequiredAffinityMask || activeProcessLimit != RequiredActiveProcessLimit ||
            processCommitBytes != RequiredProcessCommitBytes || jobCommitBytes != RequiredJobCommitBytes ||
            !killOnJobClose)
        {
            throw new InvalidOperationException("Build process request did not match the exact admitted resource and isolation contract.");
        }

        var frozenLimits = new EvidenceBuildJobLimits(
            affinityMask,
            activeProcessLimit,
            processCommitBytes,
            jobCommitBytes,
            killOnJobClose);
        var frozenInvocation = new EvidenceBuildInvocation(
            BaseProjectileEvidenceLaunchPlanner.MsBuildPath,
            canonicalWorkingDirectory,
            arguments,
            invocationEnvironment);
        var completeArgumentVector = Array.AsReadOnly(
            new[] { BaseProjectileEvidenceLaunchPlanner.MsBuildPath }.Concat(arguments).ToArray());
        var commandLine = BuildExecutableInclusiveCommandLine(completeArgumentVector);
        var environmentBlock = EncodeUnicodeEnvironmentBlock(effectiveEnvironment);

        return new EvidenceFrozenBuildProcessRequest(
            frozenInvocation,
            effectiveEnvironment,
            BaseProjectileEvidenceLaunchPlanner.MsBuildPath,
            canonicalWorkingDirectory,
            completeArgumentVector,
            commandLine,
            environmentBlock,
            false,
            true,
            frozenLimits,
            false,
            true,
            true,
            true,
            TimeSpan.FromMinutes(5),
            StreamCapBytes,
            StreamCapBytes);
    }

    internal static string BuildExecutableInclusiveCommandLine(IReadOnlyList<string> completeArgumentVector)
        => BuildExecutableInclusiveCommandLine(completeArgumentVector, WindowsArgumentEncoding.Encode);

    internal static string BuildExecutableInclusiveCommandLine(
        IReadOnlyList<string> completeArgumentVector,
        Func<IReadOnlyList<string>, string> encoder)
    {
        ArgumentNullException.ThrowIfNull(completeArgumentVector);
        ArgumentNullException.ThrowIfNull(encoder);
        var snapshot = SnapshotArguments(completeArgumentVector);
        if (snapshot.Count == 0 || string.IsNullOrEmpty(snapshot[0]))
        {
            throw new InvalidOperationException("Windows command line requires an executable argv[0].");
        }

        var expectedLengthIncludingTerminator = ComputeFrozenWindowsCommandLineCharactersIncludingTerminator(snapshot);
        var commandLine = encoder(snapshot);
        if (checked(commandLine.Length + 1) != expectedLengthIncludingTerminator)
        {
            throw new InvalidOperationException("Windows argument encoder length drifted from its allocation-free preflight.");
        }
        if (!WindowsArgumentEncoding.DecodeModel(commandLine).SequenceEqual(snapshot, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Windows command line did not round-trip the frozen argument vector.");
        }
        return commandLine;
    }

    internal static int ComputeWindowsCommandLineCharactersIncludingTerminator(IReadOnlyList<string> completeArgumentVector)
    {
        ArgumentNullException.ThrowIfNull(completeArgumentVector);
        var snapshot = SnapshotArguments(completeArgumentVector);
        return ComputeFrozenWindowsCommandLineCharactersIncludingTerminator(snapshot);
    }

    private static int ComputeFrozenWindowsCommandLineCharactersIncludingTerminator(IReadOnlyList<string> completeArgumentVector)
    {
        if (completeArgumentVector.Count == 0)
        {
            throw new InvalidOperationException("Windows command line requires an executable argv[0].");
        }

        var total = 1; // terminal UTF-16 NUL consumed by CreateProcessW
        for (var index = 0; index < completeArgumentVector.Count; index++)
        {
            var argument = completeArgumentVector[index] ??
                throw new InvalidOperationException("Windows command-line argument was null.");
            if (argument.Contains('\0'))
            {
                throw new InvalidOperationException("Windows command-line argument contained NUL.");
            }
            if (index > 0)
            {
                total = AddCommandLineLength(total, 1);
            }

            // Every admitted encoding is at least as long as its source. Refuse an obviously
            // oversized value without scanning it for quoting or allocating an encoded copy.
            if ((long)total + argument.Length > MaximumWindowsCommandLineCharactersIncludingTerminator)
            {
                throw new InvalidOperationException("Windows command line exceeded the admitted CreateProcessW bound.");
            }
            total = AddCommandLineLength(total, ComputeEncodedArgumentLength(argument));
        }
        return total;
    }

    private static int ComputeEncodedArgumentLength(string argument)
    {
        var needsQuotes = argument.Length == 0;
        if (!needsQuotes)
        {
            foreach (var character in argument)
            {
                if (char.IsWhiteSpace(character) || character == '"')
                {
                    needsQuotes = true;
                    break;
                }
            }
        }
        if (!needsQuotes)
        {
            return argument.Length;
        }

        var encodedLength = 1; // opening quote
        var slashCount = 0;
        foreach (var character in argument)
        {
            if (character == '\\')
            {
                slashCount = checked(slashCount + 1);
                continue;
            }
            if (character == '"')
            {
                encodedLength = checked(encodedLength + checked(slashCount * 2) + 2);
                slashCount = 0;
                continue;
            }
            encodedLength = checked(encodedLength + slashCount + 1);
            slashCount = 0;
        }
        return checked(encodedLength + checked(slashCount * 2) + 1); // trailing slashes + closing quote
    }

    private static int AddCommandLineLength(int current, int addition)
    {
        int result;
        try
        {
            result = checked(current + addition);
        }
        catch (OverflowException exception)
        {
            throw new InvalidOperationException("Windows command-line length arithmetic overflowed.", exception);
        }
        if (result > MaximumWindowsCommandLineCharactersIncludingTerminator)
        {
            throw new InvalidOperationException("Windows command line exceeded the admitted CreateProcessW bound.");
        }
        return result;
    }

    internal static string EncodeUnicodeEnvironmentBlock(IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var frozen = SnapshotEnvironment(environment, "Unicode environment block");
        var ordered = frozen
            .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static pair => pair.Key, StringComparer.Ordinal)
            .ToArray();
        var length = 1; // the additional NUL after the last key=value terminator
        foreach (var pair in ordered)
        {
            length = checked(length + pair.Key.Length + 1 + pair.Value.Length + 1);
            if (length > MaximumWindowsEnvironmentCharactersIncludingTerminators)
            {
                throw new InvalidOperationException("Unicode environment block exceeded the admitted CreateProcessW bound.");
            }
        }

        var block = new StringBuilder(length);
        foreach (var pair in ordered)
        {
            block.Append(pair.Key).Append('=').Append(pair.Value).Append('\0');
        }
        block.Append('\0');
        if (block.Length != length || block.Length < 2 || block[^1] != '\0' || block[^2] != '\0')
        {
            throw new InvalidOperationException("Unicode environment block framing was not exact.");
        }
        return block.ToString();
    }

    private static ReadOnlyCollection<string> SnapshotArguments(IEnumerable<string> source)
    {
        var frozen = new List<string>();
        foreach (var argument in source)
        {
            if (argument is null || argument.Contains('\0'))
            {
                throw new InvalidOperationException("Build argument was null or contained NUL.");
            }
            frozen.Add(argument);
            if (frozen.Count > 64)
            {
                throw new InvalidOperationException("Build argument count exceeded the admitted bound.");
            }
        }
        return frozen.AsReadOnly();
    }

    private static ReadOnlyDictionary<string, string> SnapshotEnvironment(
        IEnumerable<KeyValuePair<string, string>> source,
        string boundary)
    {
        var frozen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source)
        {
            if (string.IsNullOrEmpty(pair.Key) || pair.Key.Contains('=') || pair.Key.Contains('\0') ||
                pair.Value is null || pair.Value.Contains('\0') || !frozen.TryAdd(pair.Key, pair.Value))
            {
                throw new InvalidOperationException($"{boundary} environment was malformed or case-insensitively duplicated.");
            }
            if (frozen.Count > RequiredEnvironmentKeys.Length)
            {
                throw new InvalidOperationException($"{boundary} environment contained extra entries.");
            }
        }
        return new ReadOnlyDictionary<string, string>(frozen);
    }

    private static void ValidateArguments(IReadOnlyList<string> actual)
    {
        if (!actual.SequenceEqual(RequiredArguments, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("MSBuild arguments did not match the pinned no-restore rebuild contract.");
        }
    }

    private static void ValidateInvocationEnvironment(
        IReadOnlyDictionary<string, string> actual,
        string root)
    {
        var expectedSdk = Path.GetFullPath(Path.Combine(root, @".tools\dotnet\sdk\8.0.423\Sdks"));
        var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DOTNET_PROCESSOR_COUNT"] = "2",
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["MSBuildEnableWorkloadResolver"] = "false",
            ["MSBuildSDKsPath"] = expectedSdk,
        };
        RequireCanonicalEnvironmentKeyCasing(actual, expected.Keys);
        RequireExactEnvironment(actual, expected, "build invocation");
    }

    private static void ValidateEffectiveEnvironment(
        IReadOnlyDictionary<string, string> actual,
        IReadOnlyDictionary<string, string> invocationEnvironment,
        string root)
    {
        if (actual.Count != RequiredEnvironmentKeys.Length ||
            RequiredEnvironmentKeys.Any(key => !actual.ContainsKey(key)))
        {
            throw new InvalidOperationException("Effective environment was not the exact thirteen-key allowlist.");
        }
        RequireCanonicalEnvironmentKeyCasing(actual, RequiredEnvironmentKeys);
        foreach (var pair in invocationEnvironment)
        {
            if (!actual.TryGetValue(pair.Key, out var value) || value != pair.Value)
            {
                throw new InvalidOperationException("Effective environment drifted from the pinned invocation environment.");
            }
        }

        var systemRoot = RequireCanonicalDirectoryPath(actual["SystemRoot"], "SystemRoot");
        var windowsDirectory = RequireCanonicalDirectoryPath(actual["WINDIR"], "WINDIR");
        var temporaryDirectory = RequireCanonicalDirectoryPath(actual["TEMP"], "TEMP");
        var temporaryDirectoryAlias = RequireCanonicalDirectoryPath(actual["TMP"], "TMP");
        if (!string.Equals(systemRoot, windowsDirectory, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(temporaryDirectory, temporaryDirectoryAlias, StringComparison.OrdinalIgnoreCase) ||
            actual["DOTNET_CLI_UI_LANGUAGE"] != "en-US" || actual["VSLANG"] != "1033")
        {
            throw new InvalidOperationException("Trusted OS/temp aliases or deterministic language settings were not exact.");
        }

        var expectedPaths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["NUGET_PACKAGES"] = Path.GetFullPath(Path.Combine(root, @".tools\nuget-packages")),
            ["DOTNET_CLI_HOME"] = Path.GetFullPath(Path.Combine(root, @".tools\dotnet-home")),
            ["MSBuildUserExtensionsPath"] = Path.GetFullPath(Path.Combine(root, @".tools\empty\msbuild-user")),
            ["MSBuildSDKsPath"] = Path.GetFullPath(Path.Combine(root, @".tools\dotnet\sdk\8.0.423\Sdks")),
        };
        foreach (var pair in expectedPaths)
        {
            var canonical = RequireCanonicalDirectoryPath(actual[pair.Key], pair.Key);
            if (!string.Equals(canonical, pair.Value, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"{pair.Key} escaped the exact repository-controlled root.");
            }
        }
    }

    private static void RequireExactEnvironment(
        IReadOnlyDictionary<string, string> actual,
        IReadOnlyDictionary<string, string> expected,
        string boundary)
    {
        if (actual.Count != expected.Count || expected.Any(pair =>
            !actual.TryGetValue(pair.Key, out var value) || value != pair.Value))
        {
            throw new InvalidOperationException($"{boundary} environment was not exact.");
        }
    }

    private static void RequireCanonicalEnvironmentKeyCasing(
        IReadOnlyDictionary<string, string> actual,
        IEnumerable<string> canonicalKeys)
    {
        var exact = new HashSet<string>(canonicalKeys, StringComparer.Ordinal);
        if (actual.Keys.Any(key => !exact.Contains(key)))
        {
            throw new InvalidOperationException("Build environment key casing did not match the exact admitted literals.");
        }
    }

    private static string RequireCanonicalFilePath(string path, string field) =>
        RequireCanonicalPath(path, field, directory: false);

    private static string RequireCanonicalDirectoryPath(string path, string field) =>
        RequireCanonicalPath(path, field, directory: true);

    private static string RequireCanonicalPath(string path, string field, bool directory)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\0') ||
            path.Length >= MaximumWindowsCommandLineCharactersIncludingTerminator ||
            path.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            path.StartsWith(@"\\.\", StringComparison.Ordinal) ||
            !Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException($"{field} was not an ordinary absolute path.");
        }
        string canonical;
        try
        {
            canonical = directory
                ? Path.TrimEndingDirectorySeparator(Path.GetFullPath(path))
                : Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new InvalidOperationException($"{field} was not normalizable.", exception);
        }
        if (!string.Equals(path, canonical, StringComparison.OrdinalIgnoreCase) ||
            (!directory && Path.EndsInDirectorySeparator(path)) || ContainsAlternateDataStream(canonical) ||
            HasUnsafeWindowsPathComponent(canonical))
        {
            throw new InvalidOperationException($"{field} contained normalization, trailing, or stream-name drift.");
        }
        return canonical;
    }

    private static bool ContainsAlternateDataStream(string path)
    {
        var rootLength = Path.GetPathRoot(path)?.Length ?? 0;
        return path.AsSpan(rootLength).Contains(':');
    }

    private static bool HasUnsafeWindowsPathComponent(string path)
    {
        var rootLength = Path.GetPathRoot(path)?.Length ?? 0;
        foreach (var component in path[rootLength..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            if (component is "." or ".." || component.EndsWith(' ') || component.EndsWith('.') ||
                component.Any(static character => character < 0x20 || character is '"' or '<' or '>' or '|' or '?' or '*'))
            {
                return true;
            }
            var baseName = component.Split('.')[0];
            if (baseName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
                baseName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                baseName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
                baseName.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
                (baseName.Length == 4 &&
                    (baseName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                     baseName.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
                    baseName[3] is >= '1' and <= '9'))
            {
                return true;
            }
        }
        return false;
    }
}

/// <summary>
/// Conservative proof parser for raw, already-bounded MSBuild output. It admits only 7-bit ASCII,
/// LF or CRLF terminated lines, the en-US four-space count summaries, and no warning diagnostic.
/// ASCII is intentional: the dormant runner has not yet pinned a broader child-output code page.
/// </summary>
internal static class EvidenceMsBuildWarningParser
{
    private const int MaximumLineCountPerStream = 65_536;
    private const int MaximumLineCharacters = 16_384;

    private readonly record struct CanonicalSummary(
        EvidenceMsBuildSummaryStream Stream,
        int Index,
        int Warnings,
        int Errors,
        int ElapsedIndex);

    internal static EvidenceMsBuildOutputProof Parse(ReadOnlySpan<byte> standardOutput, ReadOnlySpan<byte> standardError)
    {
        if (standardOutput.Length > EvidenceBuildProcessPrimitives.StreamCapBytes ||
            standardError.Length > EvidenceBuildProcessPrimitives.StreamCapBytes)
        {
            throw new InvalidOperationException("MSBuild output exceeded the admitted raw-byte cap.");
        }

        var stdoutLines = ParseLines(standardOutput, "stdout");
        var stderrLines = ParseLines(standardError, "stderr");
        var summaries = new List<CanonicalSummary>();
        CollectSummaries(stdoutLines, EvidenceMsBuildSummaryStream.StandardOutput, summaries);
        CollectSummaries(stderrLines, EvidenceMsBuildSummaryStream.StandardError, summaries);

        if (summaries.Count != 1)
        {
            throw new InvalidOperationException("MSBuild output did not contain exactly one unambiguous en-US final summary.");
        }
        var summary = summaries[0];
        if (summary.Warnings != 0 || summary.Errors != 0)
        {
            throw new InvalidOperationException("MSBuild summary reported warnings or errors.");
        }

        RejectReservedSummaryMarkers(
            stdoutLines,
            summary.Stream == EvidenceMsBuildSummaryStream.StandardOutput ? summary : null);
        RejectReservedSummaryMarkers(
            stderrLines,
            summary.Stream == EvidenceMsBuildSummaryStream.StandardError ? summary : null);
        RejectDiagnostics(stdoutLines, summary.Stream == EvidenceMsBuildSummaryStream.StandardOutput ? summary.Index : -1);
        RejectDiagnostics(stderrLines, summary.Stream == EvidenceMsBuildSummaryStream.StandardError ? summary.Index : -1);
        return new EvidenceMsBuildOutputProof(
            summary.Warnings,
            summary.Errors,
            summary.Stream,
            stdoutLines.Count,
            stderrLines.Count);
    }

    private static ReadOnlyCollection<string> ParseLines(ReadOnlySpan<byte> bytes, string stream)
    {
        if (bytes.IsEmpty)
        {
            return Array.AsReadOnly(Array.Empty<string>());
        }
        if (bytes[^1] != (byte)'\n')
        {
            throw new InvalidOperationException($"MSBuild {stream} ended with a truncated line.");
        }
        var lines = new List<string>();
        var start = 0;
        for (var index = 0; index < bytes.Length; index++)
        {
            var value = bytes[index];
            if (value == 0 || value > 0x7f)
            {
                throw new InvalidOperationException($"MSBuild {stream} was not strict BOM-free 7-bit ASCII.");
            }
            if (value == '\r')
            {
                if (index + 1 >= bytes.Length || bytes[index + 1] != '\n')
                {
                    throw new InvalidOperationException($"MSBuild {stream} contained a bare carriage return.");
                }
                continue;
            }
            if (value != '\n')
            {
                continue;
            }
            var end = index > start && bytes[index - 1] == '\r' ? index - 1 : index;
            if (end - start > MaximumLineCharacters || lines.Count == MaximumLineCountPerStream)
            {
                throw new InvalidOperationException($"MSBuild {stream} line framing exceeded the admitted bound.");
            }
            lines.Add(Encoding.ASCII.GetString(bytes[start..end]));
            start = index + 1;
        }
        return lines.AsReadOnly();
    }

    private static void CollectSummaries(
        IReadOnlyList<string> lines,
        EvidenceMsBuildSummaryStream stream,
        List<CanonicalSummary> summaries)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (TryParseCountLine(lines[index], " Warning(s)", out var warnings))
            {
                if (index + 1 >= lines.Count || !TryParseCountLine(lines[index + 1], " Error(s)", out var errors))
                {
                    throw new InvalidOperationException("MSBuild warning summary was missing its exact adjacent error count.");
                }
                var elapsedIndex = ValidateFinalSummaryTail(lines, index + 2);
                summaries.Add(new CanonicalSummary(stream, index, warnings, errors, elapsedIndex));
                break;
            }
        }
    }

    private static int ValidateFinalSummaryTail(IReadOnlyList<string> lines, int start)
    {
        var sawElapsed = false;
        var elapsedIndex = -1;
        for (var index = start; index < lines.Count; index++)
        {
            if (lines[index].Length == 0)
            {
                continue;
            }
            const string prefix = "Time Elapsed ";
            var line = lines[index].AsSpan();
            if (sawElapsed || !line.StartsWith(prefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("MSBuild summary was not the final admitted summary block.");
            }
            if (!ValidElapsed(line[prefix.Length..]))
            {
                throw new InvalidOperationException("MSBuild elapsed-time trailer was malformed or exceeded the exact five-minute deadline.");
            }
            sawElapsed = true;
            elapsedIndex = index;
        }
        return elapsedIndex;
    }

    private static void RejectReservedSummaryMarkers(
        IReadOnlyList<string> lines,
        CanonicalSummary? allowed)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (IsExemptCanonicalSummaryIndex(allowed, index))
            {
                continue;
            }
            var folded = FoldAsciiLetters(lines[index]);
            if (folded.Contains("warnings", StringComparison.Ordinal) ||
                folded.Contains("errors", StringComparison.Ordinal) ||
                folded.Contains("timeelapsed", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("MSBuild output reused a reserved summary marker outside the exact canonical tail.");
            }
        }

        for (var index = 0; index + 1 < lines.Count; index++)
        {
            if (IsExemptCanonicalSummaryIndex(allowed, index) ||
                IsExemptCanonicalSummaryIndex(allowed, index + 1))
            {
                continue;
            }
            if (IsCountShaped(lines[index]) && IsCountShaped(lines[index + 1]))
            {
                throw new InvalidOperationException("MSBuild output contained an additional malformed or localized count-shaped summary block.");
            }
        }
    }

    private static bool IsExemptCanonicalSummaryIndex(CanonicalSummary? allowed, int index) =>
        allowed is { } summary &&
        (index == summary.Index || index == summary.Index + 1 || index == summary.ElapsedIndex);

    private static string FoldAsciiLetters(string line)
    {
        var folded = new StringBuilder(line.Length);
        foreach (var value in line)
        {
            if (value is >= 'A' and <= 'Z')
            {
                folded.Append((char)(value + ('a' - 'A')));
            }
            else if (value is >= 'a' and <= 'z')
            {
                folded.Append(value);
            }
        }
        return folded.ToString();
    }

    private static bool IsCountShaped(string line)
    {
        var value = line.AsSpan();
        var index = 0;
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }
        if (index < value.Length && value[index] is '+' or '-')
        {
            index++;
        }
        var digitStart = index;
        while (index < value.Length && value[index] is >= '0' and <= '9')
        {
            index++;
        }
        if (index == digitStart)
        {
            return false;
        }
        while (index < value.Length &&
            (char.IsWhiteSpace(value[index]) || char.IsPunctuation(value[index]) || char.IsSymbol(value[index])))
        {
            index++;
        }
        return index < value.Length;
    }

    private static bool ValidElapsed(ReadOnlySpan<char> value)
    {
        var firstColon = value.IndexOf(':');
        if (firstColon < 2)
        {
            return false;
        }
        var secondRelativeColon = value[(firstColon + 1)..].IndexOf(':');
        if (secondRelativeColon < 0)
        {
            return false;
        }
        var secondColon = firstColon + 1 + secondRelativeColon;
        var dotRelative = value[(secondColon + 1)..].IndexOf('.');
        if (dotRelative < 0)
        {
            return false;
        }
        var dot = secondColon + 1 + dotRelative;
        var hours = value[..firstColon];
        var minutes = value[(firstColon + 1)..secondColon];
        var seconds = value[(secondColon + 1)..dot];
        var hundredths = value[(dot + 1)..];
        if (hours.Length != 2 || !AsciiDigits(hours) || minutes.Length != 2 || seconds.Length != 2 || hundredths.Length != 2 ||
            !AsciiDigits(minutes) || !AsciiDigits(seconds) || !AsciiDigits(hundredths) ||
            !ulong.TryParse(hours, NumberStyles.None, CultureInfo.InvariantCulture, out var hourValue) ||
            !int.TryParse(minutes, NumberStyles.None, CultureInfo.InvariantCulture, out var minuteValue) ||
            !int.TryParse(seconds, NumberStyles.None, CultureInfo.InvariantCulture, out var secondValue) ||
            !int.TryParse(hundredths, NumberStyles.None, CultureInfo.InvariantCulture, out var hundredthValue) ||
            minuteValue > 59 || secondValue > 59)
        {
            return false;
        }
        return hourValue == 0 &&
            (minuteValue < 5 || (minuteValue == 5 && secondValue == 0 && hundredthValue == 0));
    }

    private static bool AsciiDigits(ReadOnlySpan<char> value) =>
        value.Length > 0 && value.IndexOfAnyExceptInRange('0', '9') < 0;

    private static bool TryParseCountLine(string line, string suffix, out int count)
    {
        count = 0;
        if (!line.StartsWith("    ", StringComparison.Ordinal) || !line.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }
        var digits = line.AsSpan(4, line.Length - 4 - suffix.Length);
        return digits.Length > 0 && digits.Length <= 10 &&
            (digits.Length == 1 || digits[0] != '0') && AsciiDigits(digits) &&
            int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out count);
    }

    private static void RejectDiagnostics(IReadOnlyList<string> lines, int summaryIndex)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            if (summaryIndex >= 0 && (index == summaryIndex || index == summaryIndex + 1))
            {
                continue;
            }
            var line = lines[index];
            if (ContainsWarningDiagnostic(line))
            {
                throw new InvalidOperationException("MSBuild output contained a warning diagnostic outside the summary.");
            }
        }
    }

    private static bool ContainsWarningDiagnostic(string line)
    {
        const string token = "warning";
        var start = 0;
        while (start <= line.Length - token.Length)
        {
            var index = line.IndexOf(token, start, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }
            var beforeIsBoundary = index == 0 || !IsIdentifierCharacter(line[index - 1]);
            var after = index + token.Length;
            var afterIsDiagnostic = after < line.Length && char.IsWhiteSpace(line[after]);
            if (beforeIsBoundary && afterIsDiagnostic)
            {
                return true;
            }
            start = index + token.Length;
        }
        return false;
    }

    private static bool IsIdentifierCharacter(char value) =>
        value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_';
}
