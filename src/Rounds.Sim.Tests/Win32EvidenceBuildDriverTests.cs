using System.Collections.ObjectModel;
using Rounds.EvidenceLauncher;
using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class Win32EvidenceBuildDriverTests
{
    private const string Root = @"C:\repo";
    private const string RuntimePath = @"C:\repo\game\.godot\mono\temp\bin\Debug\Rounds.Game.dll";

    [Fact]
    public void Exact_rebuild_retains_runtime_closure_until_attestation_lease_disposal()
    {
        var rig = new Rig();
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        var lease = rig.Driver.RebuildAndAttest(rig.Invocation, msbuild);

        Assert.True(lease.Attestation.ZeroWarnings);
        Assert.True(lease.Attestation.DeletedPriorOutput);
        Assert.Equal(3, lease.Attestation.RuntimeClosure.Count);
        Assert.DoesNotContain("runtime-dispose", rig.Events);
        Assert.Equal(
        [
            "msbuild-open", "candidate", "prerequisites", "environment",
            "output-read:0", "output-delete", "output-read:1", "candidate",
            "process", "candidate", "output-read:2", "runtime-open",
        ], rig.Events);
        var request = Assert.IsType<EvidenceBuildProcessRequest>(rig.Process.Request);
        Assert.False(request.InheritAmbientEnvironment);
        Assert.True(request.StartSuspended);
        Assert.Equal(0x3U, request.JobLimits.AffinityMask);
        Assert.Equal(1, request.JobLimits.ActiveProcessLimit);
        Assert.True(request.JobLimits.KillOnJobClose);
        Assert.False(request.UseShellExecute);
        Assert.True(request.CreateNoWindow);
        Assert.True(request.HiddenWindow);
        Assert.True(request.BelowNormalPriority);
        Assert.Equal(TimeSpan.FromMinutes(5), request.Deadline);
        Assert.Equal(4 * 1024 * 1024, request.StandardOutputCapBytes);
        Assert.Equal(4 * 1024 * 1024, request.StandardErrorCapBytes);
        Assert.Contains("/noAutoResponse", request.Invocation.Arguments);
        Assert.Equal(13, request.EffectiveEnvironment.Count);

        lease.Dispose();
        lease.Dispose();

        Assert.Equal(1, rig.Events.Count(value => value == "runtime-dispose"));
    }

    [Fact]
    public void Open_refuses_non_pinned_msbuild_and_closes_lease_once()
    {
        var rig = new Rig();
        rig.MsBuild.Identity = ValidMsBuild() with { Sha256 = new string('0', 64) };

        Assert.Throws<InvalidOperationException>(() => rig.Driver.OpenMsBuildExecutable(rig.Invocation));

        Assert.Equal(["msbuild-open", "msbuild-dispose"], rig.Events);
    }

    [Fact]
    public void Invocation_requires_no_auto_response_and_exact_literal_arguments()
    {
        var rig = new Rig();
        var changed = rig.Invocation with
        {
            Arguments = rig.Invocation.Arguments.Where(value => value != "/noAutoResponse").ToArray(),
        };

        Assert.Throws<InvalidOperationException>(() => rig.Driver.OpenMsBuildExecutable(changed));

        Assert.Empty(rig.Events);
    }

    [Theory]
    [InlineData("sdk-version")]
    [InlineData("ref-version")]
    [InlineData("input-hash")]
    [InlineData("input-ancestor")]
    [InlineData("package-version")]
    [InlineData("package-hash")]
    public void Locked_prerequisite_drift_refuses_before_output_deletion(string mutation)
    {
        var rig = new Rig();
        rig.Prerequisites.Value = MutatePrerequisites(ValidPrerequisites(), mutation);
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.DoesNotContain("output-delete", rig.Events);
        Assert.DoesNotContain("process", rig.Events);
    }

    [Theory]
    [InlineData("ambient-key")]
    [InlineData("nuget")]
    [InlineData("language")]
    [InlineData("system-root")]
    public void Sanitized_environment_refuses_ambient_or_pinned_value_drift(string mutation)
    {
        var rig = new Rig();
        var environment = new Dictionary<string, string>(ValidEnvironment(), StringComparer.Ordinal);
        if (mutation == "ambient-key") environment["DirectoryBuildPropsPath"] = @"C:\host\inject.props";
        if (mutation == "nuget") environment["NUGET_PACKAGES"] = @"C:\Users\Adam\.nuget\packages";
        if (mutation == "language") environment["VSLANG"] = "1041";
        if (mutation == "system-root") environment["WINDIR"] = @"C:\Other";
        rig.Environment.Value = new ReadOnlyDictionary<string, string>(environment);
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.DoesNotContain("output-delete", rig.Events);
    }

    [Theory]
    [InlineData("image")]
    [InlineData("exit")]
    [InlineData("timeout")]
    [InlineData("stdout-eof")]
    [InlineData("stderr-eof")]
    [InlineData("stdout-cap")]
    [InlineData("stderr-cap")]
    [InlineData("image-proof")]
    [InlineData("job-before-resume")]
    [InlineData("concurrent-pipes")]
    [InlineData("job-empty")]
    [InlineData("effective-environment")]
    [InlineData("warning-unparsed")]
    [InlineData("warning")]
    public void Process_attribution_bounds_and_warning_failures_refuse(string mutation)
    {
        var rig = new Rig();
        rig.Process.Result = MutateProcess(ValidProcessResult(), mutation);
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.Contains("process", rig.Events);
        Assert.DoesNotContain("runtime-open", rig.Events);
    }

    [Theory]
    [InlineData("prior-missing")]
    [InlineData("delete-still-present")]
    [InlineData("recreated-same-id")]
    [InlineData("recreated-reparse")]
    public void Runtime_output_must_be_absent_before_spawn_then_new_identity_after_exit(string mutation)
    {
        var rig = new Rig();
        if (mutation == "prior-missing") rig.Output.States[0] = EvidenceBuildOutputState.Missing(RuntimePath);
        if (mutation == "delete-still-present") rig.Output.States[1] = PriorOutput();
        if (mutation == "recreated-same-id") rig.Output.States[2] = RecreatedOutput() with { OpenedHandleIdentity = "old-id" };
        if (mutation == "recreated-reparse") rig.Output.States[2] = RecreatedOutput() with { IsReparsePoint = true };
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void Candidate_drift_before_spawn_or_after_build_refuses(int readIndex, bool processRan)
    {
        var rig = new Rig();
        rig.Repository.Values[readIndex] = ValidCandidate() with { Commit = new string('b', 40) };
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.Equal(processRan, rig.Events.Contains("process"));
        Assert.DoesNotContain("runtime-open", rig.Events);
    }

    [Fact]
    public void Runtime_closure_mismatch_disposes_every_retained_handle_before_refusal()
    {
        var rig = new Rig();
        rig.Runtime.Closure[2] = rig.Runtime.Closure[2] with { Path = @"C:\repo\wrong.dll" };
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.Equal(1, rig.Events.Count(value => value == "runtime-dispose"));
    }

    [Fact]
    public void Runtime_factory_hashes_reads_mvid_and_retains_exact_three_handles_and_ancestors()
    {
        var files = new FakeRetainedFiles();
        var ancestors = new FakeAncestors(files.Events);
        var paths = RuntimeFixturePaths();
        files.Add(paths[0], File.ReadAllBytes(typeof(Win32EvidenceBuildDriverTests).Assembly.Location), 101, '1');
        files.Add(paths[1], File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Rounds.Replay.dll")), 102, '2');
        files.Add(paths[2], File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Rounds.Sim.dll")), 103, '3');

        var lease = new Win32RuntimeAssemblyFactory(files, ancestors)
            .OpenRecreatedClosure(paths, "old-id");

        Assert.Equal(paths, lease.RuntimeClosure.Select(value => value.Path));
        Assert.All(lease.RuntimeClosure, value =>
        {
            Assert.Equal(64, value.Sha256.Length);
            Assert.Equal(32, value.Mvid.Length);
        });
        Assert.True(lease.ReparseFreeAncestorChains);
        Assert.DoesNotContain(files.Events, value => value.StartsWith("close:", StringComparison.Ordinal));

        lease.Dispose();

        Assert.Equal(["close:103", "close:102", "close:101", "ancestors-dispose"], files.Events.TakeLast(4));
    }

    [Fact]
    public void Runtime_factory_wrong_assembly_name_closes_all_acquired_ownership()
    {
        var files = new FakeRetainedFiles();
        var ancestors = new FakeAncestors(files.Events);
        var paths = RuntimeFixturePaths();
        var tests = File.ReadAllBytes(typeof(Win32EvidenceBuildDriverTests).Assembly.Location);
        files.Add(paths[0], tests, 101, '1');
        files.Add(paths[1], tests, 102, '2');

        Assert.Throws<InvalidOperationException>(() =>
            new Win32RuntimeAssemblyFactory(files, ancestors).OpenRecreatedClosure(paths, "old-id"));

        Assert.Equal(["close:102", "close:101", "ancestors-dispose"], files.Events.TakeLast(3));
    }

    [Fact]
    public void Runtime_factory_snapshot_drift_closes_refused_handle_and_ancestor_lease()
    {
        var files = new FakeRetainedFiles { DriftAfterRead = true };
        var ancestors = new FakeAncestors(files.Events);
        var paths = RuntimeFixturePaths();
        files.Add(paths[0], File.ReadAllBytes(typeof(Win32EvidenceBuildDriverTests).Assembly.Location), 101, '1');

        Assert.Throws<InvalidOperationException>(() =>
            new Win32RuntimeAssemblyFactory(files, ancestors).OpenRecreatedClosure(paths, "old-id"));

        Assert.Equal(["close:101", "ancestors-dispose"], files.Events.TakeLast(2));
    }

    [Fact]
    public void Runtime_factory_refuses_unproven_ancestor_chains_before_file_open()
    {
        var files = new FakeRetainedFiles();
        var ancestors = new FakeAncestors(files.Events) { Exact = false };

        Assert.Throws<InvalidOperationException>(() =>
            new Win32RuntimeAssemblyFactory(files, ancestors)
                .OpenRecreatedClosure(RuntimeFixturePaths(), "old-id"));

        Assert.DoesNotContain(files.Events, value => value.StartsWith("open:", StringComparison.Ordinal));
        Assert.Contains("ancestors-dispose", files.Events);
    }

    private sealed class Rig
    {
        internal List<string> Events { get; } = [];
        internal EvidenceBuildInvocation Invocation { get; } = ValidInvocation();
        internal FakeMsBuild MsBuild { get; }
        internal FakeRepository Repository { get; }
        internal FakePrerequisites Prerequisites { get; }
        internal FakeEnvironment Environment { get; }
        internal FakeOutput Output { get; }
        internal FakeProcess Process { get; }
        internal FakeRuntime Runtime { get; }
        internal Win32EvidenceBuildDriver Driver { get; }

        internal Rig()
        {
            MsBuild = new FakeMsBuild(Events);
            Repository = new FakeRepository(Events);
            Prerequisites = new FakePrerequisites(Events);
            Environment = new FakeEnvironment(Events);
            Output = new FakeOutput(Events);
            Process = new FakeProcess(Events);
            Runtime = new FakeRuntime(Events);
            Driver = new Win32EvidenceBuildDriver(
                MsBuild, Repository, Prerequisites, Environment, Output, Process, Runtime);
        }
    }

    private sealed class FakeMsBuild(List<string> events) : IEvidenceMsBuildExecutableFactory
    {
        internal EvidenceOpenedExecutableIdentity Identity { get; set; } = ValidMsBuild();
        public IEvidenceExecutableLease OpenPinnedMsBuild()
        {
            events.Add("msbuild-open");
            return new FakeExecutableLease(events, Identity);
        }
    }

    private sealed class FakeExecutableLease(List<string> events, EvidenceOpenedExecutableIdentity identity) :
        IEvidenceExecutableLease
    {
        private bool _disposed;
        public EvidenceOpenedExecutableIdentity Identity { get; } = identity;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            events.Add("msbuild-dispose");
        }
    }

    private sealed class FakeRepository(List<string> events) : IEvidenceBuildRepositoryInspector
    {
        internal EvidenceCandidateIdentity[] Values { get; } =
            [ValidCandidate(), ValidCandidate(), ValidCandidate()];
        private int _read;
        public EvidenceCandidateIdentity ReadCleanCandidate(string exactRepositoryRoot)
        {
            events.Add("candidate");
            Assert.Equal(Root, exactRepositoryRoot);
            return Values[_read++];
        }
    }

    private sealed class FakePrerequisites(List<string> events) : IEvidenceBuildPrerequisiteInspector
    {
        internal EvidenceBuildPrerequisiteAttestation Value { get; set; } = ValidPrerequisites();
        public EvidenceBuildPrerequisiteAttestation Read(string exactRepositoryRoot)
        {
            events.Add("prerequisites");
            return Value;
        }
    }

    private sealed class FakeEnvironment(List<string> events) : IEvidenceBuildEnvironmentFactory
    {
        internal IReadOnlyDictionary<string, string> Value { get; set; } = ValidEnvironment();
        public IReadOnlyDictionary<string, string> CreateSanitized(EvidenceBuildInvocation required)
        {
            events.Add("environment");
            return Value;
        }
    }

    private sealed class FakeOutput(List<string> events) : IEvidenceBuildOutputApi
    {
        internal EvidenceBuildOutputState[] States { get; } =
            [PriorOutput(), EvidenceBuildOutputState.Missing(RuntimePath), RecreatedOutput()];
        private int _read;
        public EvidenceBuildOutputState Read(string exactRuntimeAssemblyPath)
        {
            events.Add($"output-read:{_read}");
            return States[_read++];
        }
        public void Delete(string exactRuntimeAssemblyPath) => events.Add("output-delete");
    }

    private sealed class FakeProcess(List<string> events) : IEvidenceBuildProcessRunner
    {
        internal EvidenceBuildProcessRequest? Request { get; private set; }
        internal EvidenceBuildProcessResult Result { get; set; } = ValidProcessResult();
        public EvidenceBuildProcessResult Run(
            EvidenceBuildProcessRequest request,
            IEvidenceExecutableLease retainedExecutable)
        {
            events.Add("process");
            Request = request;
            Assert.Equal(ValidMsBuild(), retainedExecutable.Identity);
            return Result;
        }
    }

    private sealed class FakeRuntime(List<string> events) : IEvidenceRuntimeAssemblyFactory
    {
        internal EvidenceRuntimeAssemblyIdentity[] Closure { get; } = ValidRuntimeClosure();
        public IEvidenceRuntimeAssemblyLease OpenRecreatedClosure(
            IReadOnlyList<string> exactRuntimeAssemblyPaths,
            string priorOpenedHandleIdentity)
        {
            events.Add("runtime-open");
            Assert.Equal("old-id", priorOpenedHandleIdentity);
            Assert.Equal(
                [RuntimePath, Path.Combine(Path.GetDirectoryName(RuntimePath)!, "Rounds.Replay.dll"), Path.Combine(Path.GetDirectoryName(RuntimePath)!, "Rounds.Sim.dll")],
                exactRuntimeAssemblyPaths);
            return new FakeRuntimeLease(events, Closure);
        }
    }

    private sealed class FakeRuntimeLease(
        List<string> events,
        IReadOnlyList<EvidenceRuntimeAssemblyIdentity> closure) : IEvidenceRuntimeAssemblyLease
    {
        private bool _disposed;
        public EvidenceRuntimeAssemblyIdentity Identity => RuntimeClosure[0];
        public IReadOnlyList<EvidenceRuntimeAssemblyIdentity> RuntimeClosure { get; } = closure;
        public bool ReparseFreeAncestorChains => true;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            events.Add("runtime-dispose");
        }
    }

    private sealed class FakeAncestors(List<string> events) :
        IEvidenceRuntimeAncestorFactory,
        IEvidenceRuntimeAncestorLease
    {
        internal bool Exact { get; set; } = true;
        public bool ExactReparseFreeChains => Exact;
        public IEvidenceRuntimeAncestorLease OpenRetainedChains(IReadOnlyList<string> exactAssemblyPaths)
        {
            events.Add("ancestors-open");
            return this;
        }
        public void Dispose() => events.Add("ancestors-dispose");
    }

    private sealed class FakeRetainedFiles : IWin32RetainedFileApi
    {
        private sealed record Entry(string Path, byte[] Bytes, nint Handle, string FileId);
        private readonly Dictionary<string, Entry> _byPath = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<nint, Entry> _byHandle = [];
        private readonly Dictionary<nint, int> _snapshotReads = [];
        internal List<string> Events { get; } = [];
        internal bool DriftAfterRead { get; set; }

        internal void Add(string path, byte[] bytes, nint handle, char fileId)
        {
            var entry = new Entry(path, bytes, handle, new string(fileId, 32));
            _byPath[path] = entry;
            _byHandle[handle] = entry;
        }

        public nint OpenReadNoReplace(
            string normalizedAbsolutePath,
            uint desiredAccess,
            uint shareMode,
            uint creationDisposition,
            uint flagsAndAttributes)
        {
            Events.Add($"open:{Path.GetFileName(normalizedAbsolutePath)}");
            Assert.Equal(Win32EvidenceConstants.GenericRead, desiredAccess);
            Assert.Equal(Win32EvidenceConstants.FileShareRead, shareMode);
            return _byPath.TryGetValue(normalizedAbsolutePath, out var entry) ? entry.Handle : 0;
        }

        public Win32RetainedFileSnapshot ReadSnapshot(nint handle)
        {
            var entry = _byHandle[handle];
            var read = _snapshotReads.GetValueOrDefault(handle);
            _snapshotReads[handle] = read + 1;
            return new Win32RetainedFileSnapshot(
                entry.Path, 7, entry.FileId, entry.Bytes.Length,
                DriftAfterRead && read > 0 ? 12 : 11,
                0x80, 0, 1, false, false);
        }

        public Stream OpenReadStream(nint handle) => new MemoryStream(_byHandle[handle].Bytes, writable: false);
        public Win32RetainedFileVersion ReadVersion(nint retainedHandle, string normalizedFinalPath) =>
            throw new NotSupportedException();
        public bool CloseKernelHandle(nint handle)
        {
            Events.Add($"close:{handle}");
            return true;
        }
    }

    private static EvidenceBuildInvocation ValidInvocation() => new(
        BaseProjectileEvidenceLaunchPlanner.MsBuildPath,
        Root,
        [
            @"game\Rounds.Game.csproj", "/noAutoResponse", "/t:Rebuild", "/p:Configuration=Debug",
            "/p:Restore=false", "/p:UseSharedCompilation=false", "/p:BuildProjectReferences=true",
            "/m:1", "/nr:false", "/v:minimal", "/warnaserror",
        ],
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DOTNET_PROCESSOR_COUNT"] = "2",
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["MSBuildEnableWorkloadResolver"] = "false",
            ["MSBuildSDKsPath"] = @"C:\repo\.tools\dotnet\sdk\8.0.423\Sdks",
        }));

    private static IReadOnlyDictionary<string, string> ValidEnvironment() =>
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SystemRoot"] = @"C:\Windows", ["WINDIR"] = @"C:\Windows",
            ["TEMP"] = @"C:\Temp", ["TMP"] = @"C:\Temp",
            ["DOTNET_PROCESSOR_COUNT"] = "2", ["MSBUILDDISABLENODEREUSE"] = "1",
            ["MSBuildEnableWorkloadResolver"] = "false",
            ["MSBuildSDKsPath"] = @"C:\repo\.tools\dotnet\sdk\8.0.423\Sdks",
            ["DOTNET_CLI_UI_LANGUAGE"] = "en-US", ["VSLANG"] = "1033",
            ["NUGET_PACKAGES"] = @"C:\repo\.tools\nuget-packages",
            ["DOTNET_CLI_HOME"] = @"C:\repo\.tools\dotnet-home",
            ["MSBuildUserExtensionsPath"] = @"C:\repo\.tools\empty\msbuild-user",
        });

    private static EvidenceBuildPrerequisiteAttestation ValidPrerequisites()
    {
        string[] relatives =
        [
            "global.json", @"game\Rounds.Game.csproj", @"game\packages.lock.json",
            @"game\obj\project.assets.json", @"src\Rounds.Replay\Rounds.Replay.csproj",
            @"src\Rounds.Replay\packages.lock.json", @"src\Rounds.Replay\obj\project.assets.json",
            @"src\Rounds.Sim\Rounds.Sim.csproj", @"src\Rounds.Sim\packages.lock.json",
            @"src\Rounds.Sim\obj\project.assets.json",
        ];
        var inputs = relatives.Select(relative => new EvidenceBuildInputIdentity(
            Path.Combine(Root, relative), true, true, true, new string('a', 64))).ToArray();
        var packages = new[] { "Godot.NET.Sdk", "Godot.SourceGenerators", "GodotSharp", "GodotSharpEditor" }
            .Select((id, index) => new EvidenceBuildPackageIdentity(
                id, "4.7.1", $@"C:\repo\.tools\nuget-packages\{id.ToLowerInvariant()}\4.7.1",
                true, true, true, new string((char)('a' + index), 64))).ToArray();
        return new EvidenceBuildPrerequisiteAttestation(
            "8.0.423", new string('a', 64), true, true, true,
            @"C:\repo\.tools\dotnet\sdk\8.0.423\Sdks", true, true,
            @"C:\repo\.tools\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.29\ref\net8.0",
            "8.0.29", true, true, inputs, packages);
    }

    private static EvidenceBuildPrerequisiteAttestation MutatePrerequisites(
        EvidenceBuildPrerequisiteAttestation value,
        string mutation) => mutation switch
        {
            "sdk-version" => value with { GlobalJsonSdkVersion = "8.0.422" },
            "ref-version" => value with { ReferencePackVersion = "8.0.28" },
            "input-hash" => value with { RequiredInputs = value.RequiredInputs.Select((item, index) => index == 0 ? item with { Sha256 = "bad" } : item).ToArray() },
            "input-ancestor" => value with { RequiredInputs = value.RequiredInputs.Select((item, index) => index == 0 ? item with { ReparseFreeAncestors = false } : item).ToArray() },
            "package-version" => value with { RequiredPackages = value.RequiredPackages.Select((item, index) => index == 0 ? item with { Version = "4.7.0" } : item).ToArray() },
            "package-hash" => value with { RequiredPackages = value.RequiredPackages.Select((item, index) => index == 0 ? item with { Sha256 = "bad" } : item).ToArray() },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };

    private static EvidenceCandidateIdentity ValidCandidate() =>
        new(Root, new string('a', 40), true, true, "repo-id");

    private static EvidenceOpenedExecutableIdentity ValidMsBuild() => new(
        BaseProjectileEvidenceLaunchPlanner.MsBuildPath, true, true, false, "msbuild-id",
        BaseProjectileEvidenceLaunchPlanner.MsBuildSha256,
        BaseProjectileEvidenceLaunchPlanner.MsBuildFileVersion,
        BaseProjectileEvidenceLaunchPlanner.MsBuildProductVersion);

    private static EvidenceBuildOutputState PriorOutput() =>
        new(RuntimePath, true, true, false, true, "old-id", 100, 10);

    private static EvidenceBuildOutputState RecreatedOutput() =>
        new(RuntimePath, true, true, false, true, "game-id", 200, 20);

    private static EvidenceRuntimeAssemblyIdentity[] ValidRuntimeClosure()
    {
        var directory = Path.GetDirectoryName(RuntimePath)!;
        return
        [
            RuntimeIdentity(RuntimePath, "game-id", 'a', 'b'),
            RuntimeIdentity(Path.Combine(directory, "Rounds.Replay.dll"), "replay-id", 'c', 'd'),
            RuntimeIdentity(Path.Combine(directory, "Rounds.Sim.dll"), "sim-id", 'e', 'f'),
        ];
    }

    private static string[] RuntimeFixturePaths() =>
    [
        @"C:\output\Rounds.Sim.Tests.dll",
        @"C:\output\Rounds.Replay.dll",
        @"C:\output\Rounds.Sim.dll",
    ];

    private static EvidenceRuntimeAssemblyIdentity RuntimeIdentity(
        string path, string identity, char hash, char mvid) =>
        new(path, true, true, false, true, identity, new string(hash, 64), new string(mvid, 32));

    private static EvidenceBuildProcessResult ValidProcessResult() => new(
        ValidMsBuild(), ValidEnvironment(), 0, false, true, true, false, false,
        true, true, true, true, true, 0, [], []);

    private static EvidenceBuildProcessResult MutateProcess(EvidenceBuildProcessResult value, string mutation) =>
        mutation switch
        {
            "image" => value with { ProcessImage = value.ProcessImage with { OpenedHandleIdentity = "other" } },
            "exit" => value with { ExitCode = 1 },
            "timeout" => value with { TimedOut = true },
            "stdout-eof" => value with { StandardOutputReachedEof = false },
            "stderr-eof" => value with { StandardErrorReachedEof = false },
            "stdout-cap" => value with { StandardOutputExceededCap = true },
            "stderr-cap" => value with { StandardErrorExceededCap = true },
            "image-proof" => value with { ProcessImageMatchedBeforeResume = false },
            "job-before-resume" => value with { AssignedToJobBeforeResume = false },
            "concurrent-pipes" => value with { PipesDrainedConcurrently = false },
            "job-empty" => value with { JobEmpty = false },
            "effective-environment" => value with
            {
                EffectiveEnvironment = new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>(value.EffectiveEnvironment, StringComparer.Ordinal)
                    {
                        ["DirectoryBuildPropsPath"] = @"C:\host\inject.props",
                    }),
            },
            "warning-unparsed" => value with { WarningCountParsed = false },
            "warning" => value with { WarningCount = 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };
}
