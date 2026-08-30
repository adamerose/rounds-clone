using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class BaseProjectileEvidenceLaunchPlanTests
{
    private const string Nonce = "0123456789abcdef0123456789abcdef";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private const string AssemblySha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string AssemblyMvid = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void ExactFactsProduceImmutableCanonicalPlanWithoutNativeSideEffects()
    {
        var facts = ValidFacts();

        var decision = BaseProjectileEvidenceLaunchPlanner.Create(facts, Nonce);

        Assert.True(decision.Accepted);
        var plan = Assert.IsType<BaseProjectileEvidenceLaunchPlan>(decision.Plan);
        Assert.Equal("RoundsEvidence-" + Nonce, plan.Desktop);
        Assert.Equal(3, plan.Screen);
        Assert.Equal(new EvidencePixelBounds(364, -1080, 1920, 1080), plan.MonitorBounds);
        Assert.Equal(new EvidencePixelBounds(684, -900, 1280, 720), plan.WindowBounds);
        Assert.Equal(0x3U, plan.JobLimits.AffinityMask);
        Assert.Equal(1, plan.JobLimits.ActiveProcessLimit);
        Assert.Equal(768L * 1024 * 1024, plan.JobLimits.ProcessCommitBytes);
        Assert.Equal(1024L * 1024 * 1024, plan.JobLimits.JobCommitBytes);
        Assert.True(plan.JobLimits.BelowNormalPriority);
        Assert.True(plan.JobLimits.KillOnJobClose);
        Assert.Equal(TimeSpan.FromSeconds(30), plan.Deadline);
        Assert.Equal(8192, plan.StandardOutputCapBytes);
        Assert.Equal(65536, plan.StandardErrorCapBytes);
        Assert.Equal(new[]
        {
            "--quiet",
            "--path", Path.Combine(facts.RepositoryRoot, "game"),
            "--screen", "3",
            "--position", "684,-900",
            "--resolution", "1280x720",
            "--windowed",
            "--audio-driver", "Dummy",
            "--rendering-method", "gl_compatibility",
            "--",
            StartupRoute.DebugBaseProjectileEvidenceArgument,
            facts.Output.Root,
        }, plan.Arguments);
        Assert.Equal("2", plan.Environment["DOTNET_PROCESSOR_COUNT"]);
        Assert.Equal("1", plan.Environment["MSBUILDDISABLENODEREUSE"]);
        Assert.Equal("false", plan.Environment["MSBuildEnableWorkloadResolver"]);
        Assert.Equal(plan.Desktop, plan.Environment[DebugEvidenceCaptureProtocol.EvidenceDesktopEnvironmentVariable]);
        Assert.DoesNotContain(DebugEvidenceCaptureProtocol.EvidenceAckHandleEnvironmentVariable, plan.Environment.Keys);
        Assert.False(Directory.Exists(facts.Output.Root));
    }

    [Fact]
    public void PlannerRefusesEveryPinnedIdentityAndContainmentDrift()
    {
        var valid = ValidFacts();
        AssertRefusal(valid with { CandidateIsCleanHead = false }, "candidate");
        AssertRefusal(valid with { CandidateCommit = Commit.ToUpperInvariant() }, "candidate");
        AssertRefusal(valid with { Monitor = valid.Monitor with { DeviceName = @"\\.\DISPLAY3" } }, "topology");
        AssertRefusal(valid with { Monitor = valid.Monitor with { Ordinal = 2 } }, "topology");
        AssertRefusal(valid with { Monitor = valid.Monitor with { PerMonitorV2DpiAware = false } }, "topology");
        AssertRefusal(valid with { Monitor = valid.Monitor with { PhysicalBounds = new(0, 0, 1920, 1080) } }, "topology");
        AssertRefusal(valid with { Godot = valid.Godot with { IsReparsePoint = true } }, "godot");
        AssertRefusal(valid with { Godot = valid.Godot with { Sha256 = new string('0', 64) } }, "godot");
        AssertRefusal(valid with { Godot = valid.Godot with { ProductVersion = "4.7.2" } }, "godot");
        AssertRefusal(valid with { Toolchain = valid.Toolchain with { LockedAssetsExist = false } }, "toolchain");
        AssertRefusal(valid with { Toolchain = valid.Toolchain with { SdkVersion = "9.0.100" } }, "toolchain");
        AssertRefusal(valid with { Toolchain = valid.Toolchain with { MsBuild = valid.Toolchain.MsBuild with { Sha256 = new string('0', 64) } } }, "toolchain");
        AssertRefusal(valid with { RuntimeAssembly = valid.RuntimeAssembly with { RecreatedByImmediateRebuild = false } }, "runtime-assembly");
        AssertRefusal(valid with { RuntimeAssembly = valid.RuntimeAssembly with { BuildHadZeroWarnings = false } }, "runtime-assembly");
        AssertRefusal(valid with { Output = valid.Output with { RootAbsent = false } }, "output-root");
        AssertRefusal(valid with { Output = valid.Output with { ParentIsReparsePoint = true } }, "output-root");
        AssertRefusal(valid with { Output = valid.Output with { ParentIdentityBound = false } }, "output-root");
        AssertRefusal(valid with { Output = valid.Output with { Root = Path.Combine(valid.RepositoryRoot, "evidence") } }, "output-root");
        AssertRefusal(valid with { Output = valid.Output with { Root = Path.Combine(valid.OperatingSystemTemporaryDirectory, "evidence") } }, "output-root");
        AssertRefusal(valid with { InputDesktopIdentity = "" }, "input-desktop");
        AssertRefusal(valid, Nonce.ToUpperInvariant(), "nonce");
    }

    [Fact]
    public void CompletionGrammarIsLiteralAndPlanBound()
    {
        var plan = Assert.IsType<BaseProjectileEvidenceLaunchPlan>(
            BaseProjectileEvidenceLaunchPlanner.Create(ValidFacts(), Nonce).Plan);
        var marker = DebugEvidenceCaptureProtocol.BaseProjectileCompleteMarker(
            new DebugBaseProjectileEvidenceAttestation(
                0x6a25f798f6582a29UL,
                0,
                0,
                plan.Desktop,
                new DebugEvidenceCaptureAttestation(3, 684, -900, 1280, 720, 1920, 1080),
                AssemblySha,
                AssemblyMvid,
                new string('c', 64),
                "frame-0000.png")) + "\n";

        Assert.True(BaseProjectileEvidenceLaunchPlanner.MarkerMatchesPlan(marker, plan, out var parsed));
        Assert.Equal(new string('c', 64), parsed.PngSha256);
        Assert.False(BaseProjectileEvidenceLaunchPlanner.MarkerMatchesPlan(marker.TrimEnd('\n'), plan, out _));
        Assert.False(BaseProjectileEvidenceLaunchPlanner.MarkerMatchesPlan(marker + "extra\n", plan, out _));
        Assert.False(BaseProjectileEvidenceLaunchPlanner.MarkerMatchesPlan(marker.Replace("screen=3", "screen=03", StringComparison.Ordinal), plan, out _));
        Assert.False(BaseProjectileEvidenceLaunchPlanner.MarkerMatchesPlan(marker.Replace("assemblySha256=", "assemblySha256=ABC", StringComparison.Ordinal), plan, out _));
        Assert.False(BaseProjectileEvidenceLaunchPlanner.MarkerMatchesPlan(marker.Replace(" frame=", " extra=1 frame=", StringComparison.Ordinal), plan, out _));
        Assert.False(BaseProjectileEvidenceLaunchPlanner.MarkerMatchesPlan(marker.Replace(plan.Desktop, "RoundsEvidence-ffffffffffffffffffffffffffffffff", StringComparison.Ordinal), plan, out _));
    }

    [Theory]
    [InlineData("", "\"\"")]
    [InlineData("plain", "plain")]
    [InlineData("has space", "\"has space\"")]
    [InlineData("ends with slash \\", "\"ends with slash \\\\\"")]
    [InlineData("embedded\"quote", "\"embedded\\\"quote\"")]
    [InlineData("slash\\\"quote", "\"slash\\\\\\\"quote\"")]
    public void WindowsArgumentEncodingMatchesCreateProcessRules(string argument, string expected)
    {
        Assert.Equal(expected, WindowsArgumentEncoding.Encode(new[] { argument }));
    }

    private static void AssertRefusal(BaseProjectileEvidenceLaunchFacts facts, string code) =>
        AssertRefusal(facts, Nonce, code);

    private static void AssertRefusal(BaseProjectileEvidenceLaunchFacts facts, string nonce, string code)
    {
        var decision = BaseProjectileEvidenceLaunchPlanner.Create(facts, nonce);
        Assert.False(decision.Accepted);
        Assert.Equal(code, decision.Refusal);
        Assert.Null(decision.Plan);
    }

    private static BaseProjectileEvidenceLaunchFacts ValidFacts()
    {
        var repository = Path.GetFullPath(@"C:\RoundsCandidate");
        var output = Path.GetFullPath(@"C:\RoundsEvidence\capture-001");
        var temporary = Path.GetFullPath(@"C:\Users\Adam\AppData\Local\Temp");
        return new BaseProjectileEvidenceLaunchFacts(
            repository,
            Commit,
            CandidateIsCleanHead: true,
            new EvidenceMonitorFacts(
                BaseProjectileEvidenceLaunchPlanner.DisplayDevice,
                3,
                new EvidencePixelBounds(364, -1080, 1920, 1080),
                PerMonitorV2DpiAware: true),
            new EvidenceFileFacts(
                Path.Combine(repository, BaseProjectileEvidenceLaunchPlanner.GodotRelativePath),
                Exists: true,
                IsReparsePoint: false,
                BaseProjectileEvidenceLaunchPlanner.GodotSha256,
                FileVersion: string.Empty,
                BaseProjectileEvidenceLaunchPlanner.GodotVersion),
            new EvidenceToolchainFacts(
                new EvidenceFileFacts(
                    BaseProjectileEvidenceLaunchPlanner.MsBuildPath,
                    Exists: true,
                    IsReparsePoint: false,
                    BaseProjectileEvidenceLaunchPlanner.MsBuildSha256,
                    BaseProjectileEvidenceLaunchPlanner.MsBuildFileVersion,
                    BaseProjectileEvidenceLaunchPlanner.MsBuildProductVersion),
                BaseProjectileEvidenceLaunchPlanner.SdkVersion,
                RollForwardDisabled: true,
                SdkDirectoryExists: true,
                ReferencePackDirectoryExists: true,
                LockedAssetsExist: true),
            new EvidenceRuntimeAssemblyFacts(
                Path.Combine(repository, @"game\.godot\mono\temp\bin\Debug\Rounds.Game.dll"),
                Exists: true,
                RecreatedByImmediateRebuild: true,
                BuildHadZeroWarnings: true,
                AssemblySha,
                AssemblyMvid),
            new EvidenceOutputRootFacts(
                output,
                RootAbsent: true,
                ParentExists: true,
                ParentIsReparsePoint: false,
                ParentIdentityBound: true),
            temporary,
            "WinSta0\\Default:input-desktop-identity");
    }
}
