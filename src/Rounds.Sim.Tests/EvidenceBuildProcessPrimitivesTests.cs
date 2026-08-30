using System.Collections;
using System.Collections.ObjectModel;
using System.Text;
using Rounds.EvidenceLauncher;
using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class EvidenceBuildProcessPrimitivesTests
{
    private const string Root = @"C:\repo";

    [Fact]
    public void CompileFreezesExactDormantProcessContract()
    {
        var request = ValidRequest();

        var actual = EvidenceBuildProcessPrimitives.Compile(request);

        Assert.Equal(BaseProjectileEvidenceLaunchPlanner.MsBuildPath, actual.ApplicationName);
        Assert.Equal(Root, actual.WorkingDirectory);
        Assert.Equal(TimeSpan.FromMinutes(5), actual.Deadline);
        Assert.Equal(EvidenceBuildProcessPrimitives.StreamCapBytes, actual.StandardOutputCapBytes);
        Assert.Equal(EvidenceBuildProcessPrimitives.StreamCapBytes, actual.StandardErrorCapBytes);
        Assert.False(actual.InheritAmbientEnvironment);
        Assert.True(actual.StartSuspended);
        Assert.False(actual.UseShellExecute);
        Assert.True(actual.CreateNoWindow);
        Assert.True(actual.HiddenWindow);
        Assert.True(actual.BelowNormalPriority);
        Assert.Equal(0x3u, actual.JobLimits.AffinityMask);
        Assert.Equal(1, actual.JobLimits.ActiveProcessLimit);
        Assert.Equal(768L * 1024 * 1024, actual.JobLimits.ProcessCommitBytes);
        Assert.Equal(1024L * 1024 * 1024, actual.JobLimits.JobCommitBytes);
        Assert.True(actual.JobLimits.KillOnJobClose);
        Assert.Equal(actual.CompleteArgumentVector, WindowsArgumentEncoding.DecodeModel(actual.CommandLine));
        Assert.Equal(BaseProjectileEvidenceLaunchPlanner.MsBuildPath, actual.CompleteArgumentVector[0]);
        Assert.EndsWith("\0\0", actual.UnicodeEnvironmentBlock, StringComparison.Ordinal);
        var environmentKeys = actual.UnicodeEnvironmentBlock
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry[..entry.IndexOf('=')]);
        Assert.DoesNotContain(environmentKeys, key => string.Equals(key, "PATH", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CompileEnumeratesEachMutableCollectionExactlyOnceAndDeepFreezesIt()
    {
        var argumentsBacking = ValidArguments().ToList();
        var invocationBacking = ValidInvocationEnvironment().ToList();
        var effectiveBacking = ValidEffectiveEnvironment().ToList();
        var arguments = new SinglePassList(argumentsBacking);
        var invocationEnvironment = new SinglePassDictionary(invocationBacking);
        var effectiveEnvironment = new SinglePassDictionary(effectiveBacking);
        var request = ValidRequest(arguments, invocationEnvironment, effectiveEnvironment);

        var actual = EvidenceBuildProcessPrimitives.Compile(request);
        argumentsBacking[0] = "changed";
        invocationBacking[0] = new("changed", "changed");
        effectiveBacking[0] = new("changed", "changed");

        Assert.Equal(1, arguments.EnumerationCount);
        Assert.Equal(1, invocationEnvironment.EnumerationCount);
        Assert.Equal(1, effectiveEnvironment.EnumerationCount);
        Assert.Equal(@"game\Rounds.Game.csproj", actual.Invocation.Arguments[0]);
        Assert.Equal("2", actual.Invocation.Environment["DOTNET_PROCESSOR_COUNT"]);
        Assert.Equal(@"C:\Windows", actual.EffectiveEnvironment["SystemRoot"]);
    }

    public static TheoryData<int> InvalidRequestMutations => new()
    {
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15,
    };

    [Theory]
    [MemberData(nameof(InvalidRequestMutations))]
    public void CompileRefusesEveryResourceOrIsolationDrift(int mutation)
    {
        var request = ValidRequest();
        request = mutation switch
        {
            0 => request with { InheritAmbientEnvironment = true },
            1 => request with { StartSuspended = false },
            2 => request with { UseShellExecute = true },
            3 => request with { CreateNoWindow = false },
            4 => request with { HiddenWindow = false },
            5 => request with { BelowNormalPriority = false },
            6 => request with { Deadline = TimeSpan.FromMinutes(4) },
            7 => request with { Deadline = TimeSpan.FromMinutes(6) },
            8 => request with { StandardOutputCapBytes = EvidenceBuildProcessPrimitives.StreamCapBytes - 1 },
            9 => request with { StandardErrorCapBytes = EvidenceBuildProcessPrimitives.StreamCapBytes + 1 },
            10 => request with { JobLimits = request.JobLimits with { AffinityMask = 0x1 } },
            11 => request with { JobLimits = request.JobLimits with { AffinityMask = 0x7 } },
            12 => request with { JobLimits = request.JobLimits with { ActiveProcessLimit = 2 } },
            13 => request with { JobLimits = request.JobLimits with { ProcessCommitBytes = 767L * 1024 * 1024 } },
            14 => request with { JobLimits = request.JobLimits with { JobCommitBytes = 1025L * 1024 * 1024 } },
            15 => request with { JobLimits = request.JobLimits with { KillOnJobClose = false } },
            _ => throw new InvalidOperationException("Unknown test mutation."),
        };
        Assert.Throws<InvalidOperationException>(() => EvidenceBuildProcessPrimitives.Compile(request));
    }

    [Theory]
    [InlineData(@"C:\repo\.")]
    [InlineData(@"C:\repo\")]
    [InlineData(@"relative")]
    [InlineData(@"\\?\C:\repo")]
    [InlineData(@"C:\repo.")]
    [InlineData(@"C:\CON\repo")]
    [InlineData("C:\\repo\0escape")]
    public void CompileRefusesWorkingDirectoryNormalizationOrDeviceDrift(string workingDirectory)
    {
        var request = ValidRequest() with
        {
            Invocation = ValidRequest().Invocation with { WorkingDirectory = workingDirectory },
        };

        Assert.Throws<InvalidOperationException>(() => EvidenceBuildProcessPrimitives.Compile(request));
    }

    [Fact]
    public void CompileRefusesArgumentDriftAndNulBeforeEncoding()
    {
        var drifted = ValidArguments().ToArray();
        drifted[1] = "/t:Build";
        Assert.Throws<InvalidOperationException>(() => EvidenceBuildProcessPrimitives.Compile(
            ValidRequest(arguments: drifted)));

        drifted = ValidArguments().ToArray();
        drifted[2] += "\0tail";
        Assert.Throws<InvalidOperationException>(() => EvidenceBuildProcessPrimitives.Compile(
            ValidRequest(arguments: drifted)));
    }

    [Fact]
    public void CompileRefusesMissingExtraCaseDuplicateAndMalformedEnvironment()
    {
        var missing = ValidEffectiveEnvironment().Where(pair => pair.Key != "VSLANG").ToArray();
        Assert.Throws<InvalidOperationException>(() => EvidenceBuildProcessPrimitives.Compile(
            ValidRequest(effectiveEnvironment: new SinglePassDictionary(missing))));

        var extra = ValidEffectiveEnvironment().Append(new("PATH", @"C:\ambient")).ToArray();
        Assert.Throws<InvalidOperationException>(() => EvidenceBuildProcessPrimitives.Compile(
            ValidRequest(effectiveEnvironment: new SinglePassDictionary(extra))));

        var duplicate = ValidEffectiveEnvironment().Append(new("systemroot", @"C:\Windows")).ToArray();
        Assert.Throws<InvalidOperationException>(() => EvidenceBuildProcessPrimitives.Compile(
            ValidRequest(effectiveEnvironment: new SinglePassDictionary(duplicate))));

        var caseDrift = ValidEffectiveEnvironment().ToArray();
        caseDrift[0] = new("systemroot", @"C:\Windows");
        Assert.Throws<InvalidOperationException>(() => EvidenceBuildProcessPrimitives.Compile(
            ValidRequest(effectiveEnvironment: new SinglePassDictionary(caseDrift))));

        var malformed = ValidEffectiveEnvironment().ToArray();
        malformed[0] = new("Bad=Key", "value");
        Assert.Throws<InvalidOperationException>(() => EvidenceBuildProcessPrimitives.Compile(
            ValidRequest(effectiveEnvironment: new SinglePassDictionary(malformed))));

        malformed = ValidEffectiveEnvironment().ToArray();
        malformed[0] = new("Bad", "value\0tail");
        Assert.Throws<InvalidOperationException>(() => EvidenceBuildProcessPrimitives.Compile(
            ValidRequest(effectiveEnvironment: new SinglePassDictionary(malformed))));
    }

    [Fact]
    public void CompileRefusesOsAliasAndRepositoryControlledPathDrift()
    {
        var alias = ValidEffectiveEnvironment();
        alias["WINDIR"] = @"D:\Windows";
        Assert.Throws<InvalidOperationException>(() => EvidenceBuildProcessPrimitives.Compile(
            ValidRequest(effectiveEnvironment: alias)));

        var escaped = ValidEffectiveEnvironment();
        escaped["NUGET_PACKAGES"] = @"C:\packages";
        Assert.Throws<InvalidOperationException>(() => EvidenceBuildProcessPrimitives.Compile(
            ValidRequest(effectiveEnvironment: escaped)));
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("")]
    [InlineData("two words")]
    [InlineData("embedded\"quote")]
    [InlineData("trailing slash\\")]
    [InlineData("slashes\\\\\"quote\\")]
    public void CommandLineRoundTripsAdversarialWindowsArguments(string argument)
    {
        string[] vector = [@"C:\Program Files\Tool\tool.exe", argument, @"C:\tail\\"];

        var encoded = EvidenceBuildProcessPrimitives.BuildExecutableInclusiveCommandLine(vector);

        Assert.Equal(vector, WindowsArgumentEncoding.DecodeModel(encoded));
    }

    [Fact]
    public void CommandLineRefusesMissingArgvZeroNulAndWindowsLimit()
    {
        Assert.Throws<InvalidOperationException>(() =>
            EvidenceBuildProcessPrimitives.BuildExecutableInclusiveCommandLine(Array.Empty<string>()));
        Assert.Throws<InvalidOperationException>(() =>
            EvidenceBuildProcessPrimitives.BuildExecutableInclusiveCommandLine(["tool.exe", "bad\0arg"]));
        Assert.Throws<InvalidOperationException>(() =>
            EvidenceBuildProcessPrimitives.BuildExecutableInclusiveCommandLine(
                ["tool.exe", new string('x', EvidenceBuildProcessPrimitives.MaximumWindowsCommandLineCharactersIncludingTerminator)]));
    }

    [Fact]
    public void EnvironmentBlockIsDeterministicCaseInsensitiveAndDoubleNulTerminated()
    {
        var first = new SinglePassDictionary([new("z", "3"), new("A", "1"), new("m", "2")]);
        var second = new SinglePassDictionary([new("m", "2"), new("z", "3"), new("A", "1")]);

        var firstBlock = EvidenceBuildProcessPrimitives.EncodeUnicodeEnvironmentBlock(first);
        var secondBlock = EvidenceBuildProcessPrimitives.EncodeUnicodeEnvironmentBlock(second);

        Assert.Equal("A=1\0m=2\0z=3\0\0", firstBlock);
        Assert.Equal(firstBlock, secondBlock);
        Assert.Equal(1, first.EnumerationCount);
        Assert.Equal(1, second.EnumerationCount);
    }

    [Fact]
    public void EnvironmentBlockRefusesSyntaxDuplicatesAndWindowsLimit()
    {
        Assert.Throws<InvalidOperationException>(() => EvidenceBuildProcessPrimitives.EncodeUnicodeEnvironmentBlock(
            new SinglePassDictionary([new("A", "1"), new("a", "2")])));
        Assert.Throws<InvalidOperationException>(() => EvidenceBuildProcessPrimitives.EncodeUnicodeEnvironmentBlock(
            new SinglePassDictionary([new("A=B", "1")])));
        Assert.Throws<InvalidOperationException>(() => EvidenceBuildProcessPrimitives.EncodeUnicodeEnvironmentBlock(
            new SinglePassDictionary([new("A", new string('x', EvidenceBuildProcessPrimitives.MaximumWindowsEnvironmentCharactersIncludingTerminators))])));
    }

    [Theory]
    [InlineData("\r\n")]
    [InlineData("\n")]
    public void WarningParserAcceptsExactlyOneZeroEnUsSummary(string newline)
    {
        var stdout = Bytes($"Build succeeded.{newline}{newline}    0 Warning(s){newline}    0 Error(s){newline}{newline}Time Elapsed 00:00:01.23{newline}");

        var actual = EvidenceMsBuildWarningParser.Parse(stdout, ReadOnlySpan<byte>.Empty);

        Assert.Equal(0, actual.WarningCount);
        Assert.Equal(0, actual.ErrorCount);
        Assert.Equal(EvidenceMsBuildSummaryStream.StandardOutput, actual.SummaryStream);
    }

    [Fact]
    public void WarningParserAcceptsSummaryOnStderrWhenItIsUnique()
    {
        var stderr = Bytes("    0 Warning(s)\r\n    0 Error(s)\r\n");

        var actual = EvidenceMsBuildWarningParser.Parse(Bytes("log\r\n"), stderr);

        Assert.Equal(EvidenceMsBuildSummaryStream.StandardError, actual.SummaryStream);
    }

    [Theory]
    [InlineData("file.cs(1,1): warning CS0001: bad\r\n", "    0 Warning(s)\r\n    0 Error(s)\r\n")]
    [InlineData("    0 Warning(s)\r\n    0 Error(s)\r\n", "warning MSB0001: bad\r\n")]
    public void WarningParserRejectsWarningDiagnosticAcrossEitherStream(string stdout, string stderr) =>
        Assert.Throws<InvalidOperationException>(() => EvidenceMsBuildWarningParser.Parse(Bytes(stdout), Bytes(stderr)));

    [Theory]
    [InlineData("    1 Warning(s)\r\n    0 Error(s)\r\n")]
    [InlineData("    0 Warning(s)\r\n    1 Error(s)\r\n")]
    [InlineData("    0 Warning(s)\r\n")]
    [InlineData("    0 Error(s)\r\n")]
    [InlineData("  0 Warning(s)\r\n    0 Error(s)\r\n")]
    [InlineData("    0 Warnung(en)\r\n    0 Fehler\r\n")]
    [InlineData("    00 Warning(s)\r\n    0 Error(s)\r\n")]
    [InlineData("    0 Warning(s)\r\n    0 Error(s)\r\nextra\r\n")]
    public void WarningParserRejectsNonzeroMissingMalformedLocalizedOrNonfinalSummary(string stdout) =>
        Assert.Throws<InvalidOperationException>(() => EvidenceMsBuildWarningParser.Parse(Bytes(stdout), ReadOnlySpan<byte>.Empty));

    [Fact]
    public void WarningParserRejectsDuplicateSummary()
    {
        var summary = Bytes("    0 Warning(s)\r\n    0 Error(s)\r\n");
        Assert.Throws<InvalidOperationException>(() => EvidenceMsBuildWarningParser.Parse(summary, summary));
    }

    [Fact]
    public void WarningParserRejectsBomNonAsciiNulBareCrAndTruncation()
    {
        Assert.Throws<InvalidOperationException>(() => EvidenceMsBuildWarningParser.Parse(
            [0xef, 0xbb, 0xbf, .. Bytes("    0 Warning(s)\n    0 Error(s)\n")], []));
        Assert.Throws<InvalidOperationException>(() => EvidenceMsBuildWarningParser.Parse(
            [0x80, (byte)'\n'], []));
        Assert.Throws<InvalidOperationException>(() => EvidenceMsBuildWarningParser.Parse(
            [(byte)'x', 0, (byte)'\n'], []));
        Assert.Throws<InvalidOperationException>(() => EvidenceMsBuildWarningParser.Parse(
            Bytes("x\ry\n    0 Warning(s)\n    0 Error(s)\n"), []));
        Assert.Throws<InvalidOperationException>(() => EvidenceMsBuildWarningParser.Parse(
            Bytes("    0 Warning(s)\n    0 Error(s)"), []));
    }

    [Fact]
    public void WarningParserCountsEveryRawByteAgainstIndependentCaps()
    {
        var oversized = new byte[EvidenceBuildProcessPrimitives.StreamCapBytes + 1];
        Assert.Throws<InvalidOperationException>(() => EvidenceMsBuildWarningParser.Parse(oversized, []));
        Assert.Throws<InvalidOperationException>(() => EvidenceMsBuildWarningParser.Parse([], oversized));
    }

    [Fact]
    public void WarningParserRejectsLineLengthAndLineCountBombsWithinRawCap()
    {
        Assert.Throws<InvalidOperationException>(() => EvidenceMsBuildWarningParser.Parse(
            Bytes(new string('x', 16_385) + "\n    0 Warning(s)\n    0 Error(s)\n"), []));
        Assert.Throws<InvalidOperationException>(() => EvidenceMsBuildWarningParser.Parse(
            Enumerable.Repeat((byte)'\n', 65_537).ToArray(), []));
    }

    private static EvidenceBuildProcessRequest ValidRequest(
        IReadOnlyList<string>? arguments = null,
        IReadOnlyDictionary<string, string>? invocationEnvironment = null,
        IReadOnlyDictionary<string, string>? effectiveEnvironment = null) =>
        new(
            new EvidenceBuildInvocation(
                BaseProjectileEvidenceLaunchPlanner.MsBuildPath,
                Root,
                arguments ?? Array.AsReadOnly(ValidArguments()),
                invocationEnvironment ?? new ReadOnlyDictionary<string, string>(ValidInvocationEnvironment())),
            effectiveEnvironment ?? new ReadOnlyDictionary<string, string>(ValidEffectiveEnvironment()),
            false,
            true,
            new EvidenceBuildJobLimits(0x3, 1, 768L * 1024 * 1024, 1024L * 1024 * 1024, true),
            false,
            true,
            true,
            true,
            TimeSpan.FromMinutes(5),
            EvidenceBuildProcessPrimitives.StreamCapBytes,
            EvidenceBuildProcessPrimitives.StreamCapBytes);

    private static string[] ValidArguments() =>
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

    private static Dictionary<string, string> ValidInvocationEnvironment() => new(StringComparer.Ordinal)
    {
        ["DOTNET_PROCESSOR_COUNT"] = "2",
        ["MSBUILDDISABLENODEREUSE"] = "1",
        ["MSBuildEnableWorkloadResolver"] = "false",
        ["MSBuildSDKsPath"] = @"C:\repo\.tools\dotnet\sdk\8.0.423\Sdks",
    };

    private static Dictionary<string, string> ValidEffectiveEnvironment() => new(StringComparer.Ordinal)
    {
        ["SystemRoot"] = @"C:\Windows",
        ["WINDIR"] = @"C:\Windows",
        ["TEMP"] = @"C:\Temp",
        ["TMP"] = @"C:\Temp",
        ["DOTNET_PROCESSOR_COUNT"] = "2",
        ["MSBUILDDISABLENODEREUSE"] = "1",
        ["MSBuildEnableWorkloadResolver"] = "false",
        ["MSBuildSDKsPath"] = @"C:\repo\.tools\dotnet\sdk\8.0.423\Sdks",
        ["DOTNET_CLI_UI_LANGUAGE"] = "en-US",
        ["VSLANG"] = "1033",
        ["NUGET_PACKAGES"] = @"C:\repo\.tools\nuget-packages",
        ["DOTNET_CLI_HOME"] = @"C:\repo\.tools\dotnet-home",
        ["MSBuildUserExtensionsPath"] = @"C:\repo\.tools\empty\msbuild-user",
    };

    private static byte[] Bytes(string value) => Encoding.ASCII.GetBytes(value);

    private sealed class SinglePassList(IReadOnlyList<string> values) : IReadOnlyList<string>
    {
        internal int EnumerationCount { get; private set; }

        public int Count => throw new InvalidOperationException("Count must not be read.");

        public string this[int index] => throw new InvalidOperationException("Indexer must not be read.");

        public IEnumerator<string> GetEnumerator()
        {
            EnumerationCount++;
            if (EnumerationCount != 1)
            {
                throw new InvalidOperationException("Collection was enumerated more than once.");
            }
            return values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class SinglePassDictionary(IReadOnlyList<KeyValuePair<string, string>> values) :
        IReadOnlyDictionary<string, string>
    {
        internal int EnumerationCount { get; private set; }

        public int Count => throw new InvalidOperationException("Count must not be read.");

        public IEnumerable<string> Keys => throw new InvalidOperationException("Keys must not be read.");

        public IEnumerable<string> Values => throw new InvalidOperationException("Values must not be read.");

        public string this[string key] => throw new InvalidOperationException("Indexer must not be read.");

        public bool ContainsKey(string key) => throw new InvalidOperationException("ContainsKey must not be called.");

        public bool TryGetValue(string key, out string value) => throw new InvalidOperationException("TryGetValue must not be called.");

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
        {
            EnumerationCount++;
            if (EnumerationCount != 1)
            {
                throw new InvalidOperationException("Collection was enumerated more than once.");
            }
            return values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
