using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text;
using Rounds.EvidenceLauncher;
using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class Win32EvidenceProcessCreationTests
{
    private const string DesktopName = "RoundsEvidence-0123456789abcdef0123456789abcdef";
    private const string AckHandle = "406";

    [Fact]
    public void Unicode_environment_is_case_insensitive_unique_sorted_and_double_null_terminated()
    {
        var inherited = ValidInherited();
        var contract = Contract(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["zed"] = "last",
                [DebugEvidenceCaptureProtocol.EvidenceDesktopEnvironmentVariable] = DesktopName,
                [DebugEvidenceCaptureProtocol.EvidenceAckHandleEnvironmentVariable] = AckHandle,
                ["alpha"] = "first",
            });

        var environment = Win32UnicodeEnvironmentBuilder.Build(contract, inherited, AckHandle);

        Assert.Equal(
            "alpha=first\0" +
            $"{DebugEvidenceCaptureProtocol.EvidenceAckHandleEnvironmentVariable}={AckHandle}\0" +
            $"{DebugEvidenceCaptureProtocol.EvidenceDesktopEnvironmentVariable}={DesktopName}\0" +
            "SystemRoot=C:\\Windows\0" +
            "TEMP=C:\\Temp\0" +
            "TMP=C:\\Tmp\0" +
            "WINDIR=C:\\Windows\0" +
            "zed=last\0\0",
            Encoding.Unicode.GetString(environment.Block));
        Assert.Equal(
            new[] { "SystemRoot", "TEMP", "TMP", "WINDIR" },
            inherited.ReadNames);
        Assert.DoesNotContain("PATH", environment.Entries.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(0, environment.Block[^1]);
        Assert.Equal(0, environment.Block[^2]);
        Assert.Equal(0, environment.Block[^3]);
        Assert.Equal(0, environment.Block[^4]);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("equals")]
    [InlineData("key-nul")]
    [InlineData("value-nul")]
    [InlineData("desktop-case")]
    [InlineData("desktop-value")]
    [InlineData("ack-value")]
    public void Unicode_environment_refuses_duplicates_injection_or_wrong_rounds_values(string failure)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DebugEvidenceCaptureProtocol.EvidenceDesktopEnvironmentVariable] = DesktopName,
            [DebugEvidenceCaptureProtocol.EvidenceAckHandleEnvironmentVariable] = AckHandle,
        };
        switch (failure)
        {
            case "duplicate": values["temp"] = "collision"; break;
            case "equals": values["bad=key"] = "value"; break;
            case "key-nul": values["bad\0key"] = "value"; break;
            case "value-nul": values["key"] = "bad\0value"; break;
            case "desktop-case":
                values.Remove(DebugEvidenceCaptureProtocol.EvidenceDesktopEnvironmentVariable);
                values[DebugEvidenceCaptureProtocol.EvidenceDesktopEnvironmentVariable.ToLowerInvariant()] = DesktopName;
                break;
            case "desktop-value": values[DebugEvidenceCaptureProtocol.EvidenceDesktopEnvironmentVariable] = "wrong"; break;
            case "ack-value": values[DebugEvidenceCaptureProtocol.EvidenceAckHandleEnvironmentVariable] = "407"; break;
        }

        Assert.Throws<InvalidOperationException>(() =>
            Win32UnicodeEnvironmentBuilder.Build(Contract(values), ValidInherited(), AckHandle));
    }

    [Fact]
    public void Unicode_environment_refuses_missing_allowlisted_os_value()
    {
        var inherited = ValidInherited();
        inherited.Values.Remove("TMP");

        Assert.Throws<InvalidOperationException>(() =>
            Win32UnicodeEnvironmentBuilder.Build(Contract(), inherited, AckHandle));

        Assert.DoesNotContain("PATH", inherited.ReadNames);
    }

    [Fact]
    public void Suspended_create_uses_exact_attribute_startup_environment_and_direct_process_contract()
    {
        var plan = Plan();
        var api = new FakeCreationApi();
        var image = new FakeImageReader(api.Events);
        var desktop = new Win32DesktopLease(api, 302, plan.Desktop);
        var handles = Handles(api);
        var executable = new Win32ExecutableLease(api, 301, ExecutableIdentity(plan));
        var contract = Contract(plan);

        var process = new Win32SuspendedProcessFactory(api, ValidInherited(), image).Create(
            plan,
            desktop,
            handles,
            executable,
            contract);

        var request = Assert.IsType<Win32CreateProcessRequest>(api.Request);
        Assert.Equal(executable.Identity.Path, request.ApplicationName);
        Assert.Equal(contract.CommandLine + "\0", new string(request.MutableCommandLine));
        Assert.Equal('\0', request.MutableCommandLine[^1]);
        Assert.Equal(Path.Combine(plan.RepositoryRoot, "game"), request.CurrentDirectory);
        Assert.True(request.InheritHandles);
        Assert.Equal(Win32EvidenceConstants.RequiredCreateProcessFlags, request.CreationFlags);
        Assert.Equal(112U, request.StartupInfo.StartupInfo.Size);
        Assert.Equal(plan.Desktop, request.StartupInfo.StartupInfo.Desktop);
        Assert.Equal(Win32EvidenceConstants.StartfUseStdHandles, request.StartupInfo.StartupInfo.Flags);
        Assert.Equal((nint)401, request.StartupInfo.StartupInfo.StandardInput);
        Assert.Equal((nint)403, request.StartupInfo.StartupInfo.StandardOutput);
        Assert.Equal((nint)405, request.StartupInfo.StartupInfo.StandardError);
        Assert.Equal((nint)900, request.StartupInfo.AttributeList);
        Assert.Equal(new nint[] { 401, 403, 405, 406 }, request.ExplicitInheritedHandles);
        Assert.Contains(
            $"{DebugEvidenceCaptureProtocol.EvidenceDesktopEnvironmentVariable}={plan.Desktop}\0",
            Encoding.Unicode.GetString(request.UnicodeEnvironmentBlock),
            StringComparison.Ordinal);
        Assert.Contains(
            $"{DebugEvidenceCaptureProtocol.EvidenceAckHandleEnvironmentVariable}=406\0",
            Encoding.Unicode.GetString(request.UnicodeEnvironmentBlock),
            StringComparison.Ordinal);
        Assert.Equal(
            new[]
            {
                "attribute-size:1", "allocate:128", "attribute-init:900:1:128",
                "attribute-update:900:401,403,405,406", "process-create",
                "close:401", "close:403", "close:405", "close:406",
                "image-read:901", "attribute-delete:900", "free:900",
            },
            api.Events);
        Assert.DoesNotContain("close:301", api.Events);
        Assert.Equal(executable.Identity, image.ExpectedIdentity);

        process.MarkAssignedToKillOnCloseJob();
        process.DisarmTerminationFallbackAfterVerifiedExitAndEmptyJob();
        process.Dispose();
        executable.Dispose();
        handles.Dispose();
        desktop.Dispose();

        Assert.Equal(1, api.Events.Count(value => value == "close:301"));
        Assert.Equal(1, api.Events.Count(value => value == "close:902"));
        Assert.Equal(1, api.Events.Count(value => value == "close:901"));
    }

    [Theory]
    [InlineData("size")]
    [InlineData("allocate")]
    [InlineData("initialize")]
    [InlineData("update")]
    [InlineData("create")]
    public void Attribute_or_process_failure_releases_only_resources_that_were_acquired(string stage)
    {
        var plan = Plan();
        var api = new FakeCreationApi { FailureStage = stage };
        var desktop = new Win32DesktopLease(api, 302, plan.Desktop);
        var handles = Handles(api);
        var executable = new Win32ExecutableLease(api, 301, ExecutableIdentity(plan));

        Assert.ThrowsAny<Exception>(() =>
            new Win32SuspendedProcessFactory(api, ValidInherited(), new FakeImageReader(api.Events)).Create(
                plan,
                desktop,
                handles,
                executable,
                Contract(plan)));

        if (stage is "initialize")
        {
            Assert.DoesNotContain("attribute-delete:900", api.Events);
            Assert.Contains("free:900", api.Events);
        }
        if (stage is "update" or "create")
        {
            Assert.Contains("attribute-delete:900", api.Events);
            Assert.Contains("free:900", api.Events);
        }
        if (stage is "size" or "allocate")
        {
            Assert.DoesNotContain("attribute-delete:900", api.Events);
        }
        Assert.DoesNotContain("image-read:901", api.Events);
        Assert.DoesNotContain("close:301", api.Events);

        executable.Dispose();
        handles.Dispose();
        desktop.Dispose();
    }

    [Fact]
    public void Create_failure_returning_process_handles_terminates_before_releasing_them()
    {
        var plan = Plan();
        var api = new FakeCreationApi
        {
            CreateResult = new Win32CreateProcessResult(false, 911, 912, 1, 2),
        };

        Assert.Throws<Win32Exception>(() =>
            new Win32SuspendedProcessFactory(api, ValidInherited(), new FakeImageReader(api.Events)).Create(
                plan,
                new Win32DesktopLease(api, 302, plan.Desktop),
                Handles(api),
                new Win32ExecutableLease(api, 301, ExecutableIdentity(plan)),
                Contract(plan)));

        Assert.True(api.Events.IndexOf("attribute-delete:900") < api.Events.IndexOf("terminate:911:1"));
        Assert.Equal(
            new[] { "terminate:911:1", "wait:911:5000", "close:912", "close:911" },
            api.Events.TakeLast(4));
    }

    [Theory]
    [InlineData("delete")]
    [InlineData("free")]
    public void Attribute_cleanup_exception_still_attempts_all_cleanup_and_terminates_child(string stage)
    {
        var plan = Plan();
        var api = new FakeCreationApi { FailureStage = stage };

        Assert.ThrowsAny<Exception>(() =>
            new Win32SuspendedProcessFactory(api, ValidInherited(), new FakeImageReader(api.Events)).Create(
                plan,
                new Win32DesktopLease(api, 302, plan.Desktop),
                Handles(api),
                new Win32ExecutableLease(api, 301, ExecutableIdentity(plan)),
                Contract(plan)));

        Assert.Contains("attribute-delete:900", api.Events);
        Assert.Contains("free:900", api.Events);
        Assert.Contains("terminate:901:1", api.Events);
        Assert.Contains("close:902", api.Events);
        Assert.Contains("close:901", api.Events);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Partial_process_result_is_terminated_or_closed_without_leaking(bool processOnly)
    {
        var plan = Plan();
        var api = new FakeCreationApi
        {
            CreateResult = processOnly
                ? new Win32CreateProcessResult(true, 921, 0, 1, 0)
                : new Win32CreateProcessResult(true, 0, 922, 0, 2),
        };

        Assert.Throws<Win32Exception>(() =>
            new Win32SuspendedProcessFactory(api, ValidInherited(), new FakeImageReader(api.Events)).Create(
                plan,
                new Win32DesktopLease(api, 302, plan.Desktop),
                Handles(api),
                new Win32ExecutableLease(api, 301, ExecutableIdentity(plan)),
                Contract(plan)));

        if (processOnly)
        {
            Assert.Contains("terminate:921:1", api.Events);
            Assert.Contains("wait:921:5000", api.Events);
            Assert.Equal(1, api.Events.Count(value => value == "close:921"));
        }
        else
        {
            Assert.Equal(1, api.Events.Count(value => value == "close:922"));
            Assert.DoesNotContain(api.Events, value => value.StartsWith("terminate:", StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("image-mismatch")]
    [InlineData("image-throw")]
    [InlineData("child-handle-close")]
    public void Post_create_attestation_or_transition_failure_terminates_suspended_process(string failure)
    {
        var plan = Plan();
        var api = new FakeCreationApi { FailChildHandleClose = failure == "child-handle-close" };
        var image = new FakeImageReader(api.Events)
        {
            Throw = failure == "image-throw",
            Override = failure == "image-mismatch"
                ? ExecutableIdentity(plan) with { OpenedHandleIdentity = "replacement" }
                : null,
        };

        Assert.ThrowsAny<Exception>(() =>
            new Win32SuspendedProcessFactory(api, ValidInherited(), image).Create(
                plan,
                new Win32DesktopLease(api, 302, plan.Desktop),
                Handles(api),
                new Win32ExecutableLease(api, 301, ExecutableIdentity(plan)),
                Contract(plan)));

        Assert.Contains("terminate:901:1", api.Events);
        Assert.Contains("wait:901:5000", api.Events);
        Assert.Contains("close:902", api.Events);
        Assert.Contains("close:901", api.Events);
        Assert.DoesNotContain("close:301", api.Events);
    }

    [Fact]
    public void Contract_mismatch_refuses_before_environment_or_attribute_work()
    {
        var plan = Plan();
        var api = new FakeCreationApi();
        var invalid = Contract(plan) with { UseShell = true };

        Assert.Throws<InvalidOperationException>(() =>
            new Win32SuspendedProcessFactory(api, ValidInherited(), new FakeImageReader(api.Events)).Create(
                plan,
                new Win32DesktopLease(api, 302, plan.Desktop),
                Handles(api),
                new Win32ExecutableLease(api, 301, ExecutableIdentity(plan)),
                invalid));

        Assert.Empty(api.Events);
    }

    [Fact]
    public void Disposed_retained_executable_refuses_before_environment_or_attribute_work()
    {
        var plan = Plan();
        var api = new FakeCreationApi();
        var executable = new Win32ExecutableLease(api, 301, ExecutableIdentity(plan));
        executable.Dispose();
        api.Events.Clear();

        Assert.Throws<ObjectDisposedException>(() =>
            new Win32SuspendedProcessFactory(api, ValidInherited(), new FakeImageReader(api.Events)).Create(
                plan,
                new Win32DesktopLease(api, 302, plan.Desktop),
                Handles(api),
                executable,
                Contract(plan)));

        Assert.Empty(api.Events);
    }

    private static BaseProjectileEvidenceLaunchPlan Plan()
    {
        var repository = @"C:\candidate folder\rounds-clone";
        var executable = @"C:\candidate folder\Godot.exe";
        return new BaseProjectileEvidenceLaunchPlan(
            new string('a', 40),
            repository,
            executable,
            DesktopName,
            3,
            new EvidencePixelBounds(364, -1080, 1920, 1080),
            new EvidencePixelBounds(684, -900, 1280, 720),
            @"D:\evidence\run",
            Array.AsReadOnly(new[] { "--quiet", "--path", Path.Combine(repository, "game") }),
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>()),
            new BaseProjectileEvidenceJobLimits(3, 1, 1, 1, true, true),
            TimeSpan.FromSeconds(30),
            8192,
            65536,
            @"C:\candidate folder\Rounds.Game.dll",
            new string('b', 64),
            new string('c', 32),
            "Default",
            Array.Empty<EvidenceAncestorIdentityFacts>());
    }

    private static EvidenceCreateProcessContract Contract(
        BaseProjectileEvidenceLaunchPlan plan) =>
        Contract(plan, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DebugEvidenceCaptureProtocol.EvidenceDesktopEnvironmentVariable] = plan.Desktop,
            [DebugEvidenceCaptureProtocol.EvidenceAckHandleEnvironmentVariable] = AckHandle,
            ["DOTNET_PROCESSOR_COUNT"] = "2",
        });

    private static EvidenceCreateProcessContract Contract(
        IReadOnlyDictionary<string, string>? environment = null) =>
        Contract(Plan(), environment ?? new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DebugEvidenceCaptureProtocol.EvidenceDesktopEnvironmentVariable] = DesktopName,
            [DebugEvidenceCaptureProtocol.EvidenceAckHandleEnvironmentVariable] = AckHandle,
        });

    private static EvidenceCreateProcessContract Contract(
        BaseProjectileEvidenceLaunchPlan plan,
        IReadOnlyDictionary<string, string> environment) =>
        new(
            Win32EvidenceConstants.RequiredCreateProcessFlags,
            plan.Desktop,
            plan.CommandLine,
            environment,
            UseShell: false,
            Array.AsReadOnly(new[]
            {
                new EvidenceChildHandleDescriptor(EvidenceChildHandle.StandardInputRead, true),
                new EvidenceChildHandleDescriptor(EvidenceChildHandle.StandardOutputWrite, true),
                new EvidenceChildHandleDescriptor(EvidenceChildHandle.StandardErrorWrite, true),
                new EvidenceChildHandleDescriptor(EvidenceChildHandle.AcknowledgementRead, true),
            }));

    private static EvidenceOpenedExecutableIdentity ExecutableIdentity(
        BaseProjectileEvidenceLaunchPlan plan) =>
        new(
            plan.Executable,
            true,
            true,
            false,
            "volume:0000000000000001:file:0123456789abcdef0123456789abcdef",
            new string('d', 64),
            "4.7.1",
            "4.7.1.stable.mono.official");

    private static Win32LaunchHandleLease Handles(FakeCreationApi api) =>
        new(
            api,
            standardInputRead: 401,
            standardOutputRead: 402,
            standardOutputWrite: 403,
            standardErrorRead: 404,
            standardErrorWrite: 405,
            acknowledgementRead: 406,
            acknowledgementWrite: 407);

    private static FakeInheritedEnvironment ValidInherited() => new(new Dictionary<string, string>(
        StringComparer.OrdinalIgnoreCase)
    {
        ["SystemRoot"] = @"C:\Windows",
        ["TEMP"] = @"C:\Temp",
        ["TMP"] = @"C:\Tmp",
        ["WINDIR"] = @"C:\Windows",
        ["PATH"] = @"C:\untrusted",
    });

    private sealed class FakeInheritedEnvironment(Dictionary<string, string> values) :
        IWin32InheritedEnvironment
    {
        internal Dictionary<string, string> Values { get; } = values;
        internal List<string> ReadNames { get; } = new();

        public string? Read(string name)
        {
            ReadNames.Add(name);
            return Values.GetValueOrDefault(name);
        }
    }

    private sealed class FakeImageReader : IWin32ChildProcessImageReader
    {
        private readonly List<string> _events;

        internal FakeImageReader(List<string> events)
        {
            _events = events;
        }

        internal bool Throw { get; init; }
        internal EvidenceOpenedExecutableIdentity? Override { get; init; }
        internal EvidenceOpenedExecutableIdentity? ExpectedIdentity { get; private set; }

        public EvidenceOpenedExecutableIdentity Read(
            Win32ProcessLease process,
            EvidenceOpenedExecutableIdentity expectedIdentity)
        {
            ExpectedIdentity = expectedIdentity;
            _events.Add($"image-read:{process.DangerousProcessHandle}");
            if (Throw) throw new IOException("injected image read failure");
            return Override ?? expectedIdentity;
        }
    }

    private sealed class FakeCreationApi : IWin32ProcessCreationApi
    {
        internal List<string> Events { get; } = new();
        internal string? FailureStage { get; init; }
        internal bool FailChildHandleClose { get; init; }
        internal Win32CreateProcessRequest? Request { get; private set; }
        internal Win32CreateProcessResult CreateResult { get; init; } =
            new(true, 901, 902, 11, 12);

        public nuint QueryAttributeListSize(int attributeCount)
        {
            Events.Add($"attribute-size:{attributeCount}");
            return FailureStage == "size" ? 0 : (nuint)128;
        }

        public nint Allocate(nuint bytes)
        {
            Events.Add($"allocate:{bytes}");
            return FailureStage == "allocate" ? 0 : 900;
        }

        public void Free(nint memory)
        {
            Events.Add($"free:{memory}");
            if (FailureStage == "free") throw new IOException("injected free failure");
        }

        public bool InitializeAttributeList(nint attributeList, int attributeCount, nuint bytes)
        {
            Events.Add($"attribute-init:{attributeList}:{attributeCount}:{bytes}");
            return FailureStage != "initialize";
        }

        public bool UpdateHandleList(nint attributeList, IReadOnlyList<nint> handles)
        {
            Events.Add($"attribute-update:{attributeList}:{string.Join(',', handles)}");
            return FailureStage != "update";
        }

        public void DeleteAttributeList(nint attributeList)
        {
            Events.Add($"attribute-delete:{attributeList}");
            if (FailureStage == "delete") throw new IOException("injected delete failure");
        }

        public Win32CreateProcessResult CreateProcess(Win32CreateProcessRequest request)
        {
            Events.Add("process-create");
            Request = request;
            return FailureStage == "create"
                ? new Win32CreateProcessResult(false, 0, 0, 0, 0)
                : CreateResult;
        }

        public bool CloseKernelHandle(nint handle)
        {
            Events.Add($"close:{handle}");
            return !FailChildHandleClose || handle is not 401;
        }

        public bool CloseDesktop(nint desktop)
        {
            Events.Add($"desktop-close:{desktop}");
            return true;
        }

        public bool TerminateProcess(nint process, uint exitCode)
        {
            Events.Add($"terminate:{process}:{exitCode}");
            return true;
        }

        public uint WaitForSingleObject(nint handle, uint milliseconds)
        {
            Events.Add($"wait:{handle}:{milliseconds}");
            return Win32EvidenceConstants.WaitObject0;
        }

        public bool WriteFile(nint handle, ReadOnlySpan<byte> data, out uint written)
        {
            written = checked((uint)data.Length);
            return true;
        }
    }
}
