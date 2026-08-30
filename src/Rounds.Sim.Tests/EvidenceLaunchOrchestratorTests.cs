using System.Collections.ObjectModel;
using Rounds.EvidenceLauncher;
using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class EvidenceLaunchOrchestratorTests
{
    [Fact]
    public void Success_enforces_build_native_order_parent_validation_and_empty_job()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var build = new FakeBuild(events, plan);
        var native = new FakeNative(events, plan);

        var result = new EvidenceLaunchOrchestrator(build, native).Execute(plan);

        Assert.True(result.Success);
        Assert.Equal("success", result.Code);
        Assert.Null(result.PreservedUnprovenResidueRoot);
        Assert.Equal(
            new[]
            {
                "build", "worker-enter", "input-desktop", "preflight", "desktop-create",
                "handles-create", "process-create-suspended", "job-create", "job-configure",
                "job-assign", "resume-and-deadline", "capture-protocol", "frame-validate",
                "ack-close:06", "process-wait", "job-wait-empty", "foreground-check",
                "input-desktop", "job-dispose", "process-dispose", "handles-dispose",
                "desktop-dispose", "worker-exit",
            },
            events);
        var contract = Assert.IsType<EvidenceCreateProcessContract>(native.ProcessContract);
        Assert.False(contract.UseShell);
        Assert.Equal(plan.Desktop, contract.Desktop);
        Assert.Equal(plan.CommandLine, contract.CommandLine);
        Assert.Equal(
            "4242",
            contract.UnicodeEnvironment[DebugEvidenceCaptureProtocol.EvidenceAckHandleEnvironmentVariable]);
        Assert.Equal(
            EvidenceCreateProcessFlags.CreateSuspended |
            EvidenceCreateProcessFlags.CreateNoWindow |
            EvidenceCreateProcessFlags.CreateNewProcessGroup |
            EvidenceCreateProcessFlags.BelowNormalPriorityClass |
            EvidenceCreateProcessFlags.ExtendedStartupInfoPresent |
            EvidenceCreateProcessFlags.CreateUnicodeEnvironment,
            contract.Flags);
        Assert.Equal(
            new[]
            {
                EvidenceChildHandle.StandardInputRead,
                EvidenceChildHandle.StandardOutputWrite,
                EvidenceChildHandle.StandardErrorWrite,
                EvidenceChildHandle.AcknowledgementRead,
            },
            contract.InheritedHandles.Select(handle => handle.Kind));
        Assert.All(contract.InheritedHandles, handle => Assert.True(handle.Inheritable));
        Assert.Equal(plan.Deadline, native.StartedDeadline);
        Assert.Equal(plan.StandardOutputCapBytes, native.ObservedStandardOutputCap);
        Assert.Equal(plan.StandardErrorCapBytes, native.ObservedStandardErrorCap);
        Assert.Equal(plan.JobLimits, native.ObservedJobLimits);
    }

    [Fact]
    public void Build_attribution_mismatch_refuses_before_native_worker()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var build = new FakeBuild(events, plan)
        {
            Override = ValidBuild(plan) with { ZeroWarnings = false },
        };

        var result = new EvidenceLaunchOrchestrator(build, new FakeNative(events, plan)).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("build-attribution", result.Code);
        Assert.Equal(new[] { "build" }, events);
    }

    [Fact]
    public void Invalid_handle_allowlist_refuses_before_process_and_disposes_owned_leases()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var native = new FakeNative(events, plan)
        {
            Handles = new[]
            {
                new EvidenceChildHandleDescriptor(EvidenceChildHandle.StandardInputRead, true),
                new EvidenceChildHandleDescriptor(EvidenceChildHandle.StandardOutputWrite, true),
                new EvidenceChildHandleDescriptor(EvidenceChildHandle.StandardErrorWrite, true),
                new EvidenceChildHandleDescriptor(EvidenceChildHandle.AcknowledgementRead, true),
                new EvidenceChildHandleDescriptor(EvidenceChildHandle.AcknowledgementRead, true),
            },
        };

        var result = new EvidenceLaunchOrchestrator(new FakeBuild(events, plan), native).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("handle-allowlist", result.Code);
        Assert.DoesNotContain("process-create-suspended", events);
        Assert.Equal(
            new[] { "handles-dispose", "desktop-dispose", "worker-exit" },
            events.TakeLast(3));
    }

    [Theory]
    [InlineData(true, false, false, "deadline")]
    [InlineData(false, true, false, "pipe-cap")]
    [InlineData(false, false, true, "pipe-cap")]
    public void Deadline_or_pipe_cap_failure_closes_job_first_and_preserves_exact_unproven_residue(
        bool timedOut,
        bool standardOutputCapExceeded,
        bool standardErrorCapExceeded,
        string expectedCode)
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var native = new FakeNative(events, plan)
        {
            Protocol = ValidProtocol(plan) with
            {
                TimedOut = timedOut,
                StandardOutputCapExceeded = standardOutputCapExceeded,
                StandardErrorCapExceeded = standardErrorCapExceeded,
            },
        };

        var result = new EvidenceLaunchOrchestrator(new FakeBuild(events, plan), native).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal(expectedCode, result.Code);
        Assert.Equal(plan.OutputRoot, result.PreservedUnprovenResidueRoot);
        Assert.DoesNotContain("ack-close:06", events);
        Assert.True(events.IndexOf("job-dispose") < events.IndexOf("process-dispose"));
        Assert.DoesNotContain(events, value => value.Contains("delete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parent_validation_failure_never_acknowledges_and_preserves_residue()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var native = new FakeNative(events, plan)
        {
            Frame = ValidFrame(plan) with { FrameIdentityBound = false },
        };

        var result = new EvidenceLaunchOrchestrator(new FakeBuild(events, plan), native).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("parent-frame-validation", result.Code);
        Assert.Equal(plan.OutputRoot, result.PreservedUnprovenResidueRoot);
        Assert.Contains("frame-validate", events);
        Assert.DoesNotContain("ack-close:06", events);
        Assert.DoesNotContain("process-wait", events);
    }

    [Fact]
    public void Native_exception_after_resume_still_closes_job_first_and_reports_residue()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var native = new FakeNative(events, plan) { ThrowDuringFrameValidation = true };

        var result = new EvidenceLaunchOrchestrator(new FakeBuild(events, plan), native).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("native-boundary", result.Code);
        Assert.Equal(plan.OutputRoot, result.PreservedUnprovenResidueRoot);
        Assert.True(events.IndexOf("job-dispose") < events.IndexOf("process-dispose"));
        Assert.Equal("worker-exit", events[^1]);
    }

    [Fact]
    public void Cooperative_nonzero_exit_after_ack_does_not_claim_forced_residue()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var native = new FakeNative(events, plan)
        {
            Termination = new EvidenceProcessTermination(Exited: true, ExitCode: 9, Forced: false),
        };

        var result = new EvidenceLaunchOrchestrator(new FakeBuild(events, plan), native).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("process-exit", result.Code);
        Assert.Null(result.PreservedUnprovenResidueRoot);
        Assert.Contains("ack-close:06", events);
    }

    [Fact]
    public void Preflight_identity_or_topology_drift_refuses_before_private_desktop()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var native = new FakeNative(events, plan)
        {
            Preflight = ValidPreflight(plan) with { OutputRootAbsent = false },
        };

        var result = new EvidenceLaunchOrchestrator(new FakeBuild(events, plan), native).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("preflight", result.Code);
        Assert.DoesNotContain("desktop-create", events);
        Assert.Equal("worker-exit", events[^1]);
    }

    [Theory]
    [InlineData("plan", "Plan")]
    [InlineData("execute", "Execute")]
    public void Command_parser_keeps_plan_and_execute_explicitly_separate(
        string argument,
        string expected)
    {
        var parsed = EvidenceLauncherCommand.Parse(new[] { argument });

        Assert.True(parsed.Accepted);
        Assert.Equal(expected, parsed.Mode?.ToString());
        Assert.False(EvidenceLauncherCommand.Parse(Array.Empty<string>()).Accepted);
        Assert.False(EvidenceLauncherCommand.Parse(new[] { argument, "extra" }).Accepted);
        Assert.False(EvidenceLauncherCommand.Parse(new[] { "EXECUTE" }).Accepted);
    }

    private static BaseProjectileEvidenceLaunchPlan ValidPlan()
    {
        var repository = @"C:\candidate\rounds-clone";
        var output = @"D:\RoundsEvidence\capture-0001";
        var ancestors = Array.AsReadOnly(new[]
        {
            new EvidenceAncestorIdentityFacts(@"D:\", @"D:\", true, false, true),
            new EvidenceAncestorIdentityFacts(@"D:\RoundsEvidence", @"D:\RoundsEvidence", true, false, true),
        });
        var arguments = Array.AsReadOnly(new[]
        {
            "--quiet", "--path", repository + @"\game", "--screen", "3",
            "--position", "684,-900", "--resolution", "1280x720", "--windowed",
            "--audio-driver", "Dummy", "--rendering-method", "gl_compatibility", "--",
            DebugEvidenceCaptureProtocol.BaseProjectileArgument, output,
        });
        var environment = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DOTNET_PROCESSOR_COUNT"] = "2",
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["MSBuildEnableWorkloadResolver"] = "false",
            ["MSBuildSDKsPath"] = repository + @"\.tools\dotnet\sdk\8.0.423\Sdks",
            [DebugEvidenceCaptureProtocol.EvidenceDesktopEnvironmentVariable] =
                "RoundsEvidence-0123456789abcdef0123456789abcdef",
        });
        return new BaseProjectileEvidenceLaunchPlan(
            "0123456789abcdef0123456789abcdef01234567",
            repository,
            repository + @"\.tools\godot-4.7.1\Godot.exe",
            "RoundsEvidence-0123456789abcdef0123456789abcdef",
            3,
            BaseProjectileEvidenceLaunchPlanner.RequiredMonitorBounds,
            BaseProjectileEvidenceLaunchPlanner.RequiredWindowBounds,
            output,
            arguments,
            environment,
            new BaseProjectileEvidenceJobLimits(
                0x3, 1, 768L * 1024 * 1024, 1024L * 1024 * 1024, true, true),
            TimeSpan.FromSeconds(30),
            8 * 1024,
            64 * 1024,
            new string('a', 64),
            new string('b', 32),
            "WinSta0\\Default",
            ancestors);
    }

    private static EvidenceBuildAttestation ValidBuild(BaseProjectileEvidenceLaunchPlan plan) =>
        new(
            EvidenceBuildContract.Create(plan),
            plan.CandidateCommit,
            DeletedPriorOutput: true,
            RecreatedRuntimeAssembly: true,
            ZeroWarnings: true,
            plan.RuntimeAssemblySha256,
            plan.RuntimeAssemblyMvid);

    private static EvidenceNativePreflight ValidPreflight(BaseProjectileEvidenceLaunchPlan plan) =>
        new(
            new EvidenceMonitorFacts(
                BaseProjectileEvidenceLaunchPlanner.DisplayDevice,
                plan.Screen,
                plan.MonitorBounds,
                PerMonitorV2DpiAware: true),
            plan.InputDesktopIdentity,
            OutputRootAbsent: true,
            plan.OutputAncestors);

    private static EvidenceProtocolCapture ValidProtocol(BaseProjectileEvidenceLaunchPlan plan)
    {
        var marker = DebugEvidenceCaptureProtocol.BaseProjectileCompleteMarker(
            new DebugBaseProjectileEvidenceAttestation(
                0x6a25f798f6582a29UL,
                0,
                0,
                plan.Desktop,
                new DebugEvidenceCaptureAttestation(3, 684, -900, 1280, 720, 1920, 1080),
                plan.RuntimeAssemblySha256,
                plan.RuntimeAssemblyMvid,
                new string('c', 64),
                "frame-0000.png"));
        return new EvidenceProtocolCapture(marker + "\n", string.Empty, false, false, false);
    }

    private static EvidencePublishedFrameValidation ValidFrame(BaseProjectileEvidenceLaunchPlan plan) =>
        new(
            plan.OutputRoot,
            "frame-0000.png",
            new string('c', 64),
            1920,
            1080,
            Rgba8: true,
            RootIdentityBound: true,
            FrameIdentityBound: true,
            RootLeaseObserved: true,
            FrameLeaseObserved: true,
            ContainsOnlyExpectedFrame: true);

    private sealed class FakeBuild(List<string> events, BaseProjectileEvidenceLaunchPlan plan) : IEvidenceBuildDriver
    {
        public EvidenceBuildAttestation? Override { get; init; }

        public EvidenceBuildAttestation RebuildAndAttest(EvidenceBuildInvocation required)
        {
            events.Add("build");
            var attestation = Override ?? ValidBuild(plan);
            return attestation with { Invocation = required };
        }
    }

    private sealed class FakeNative : IEvidenceNativeBoundary
    {
        private readonly List<string> _events;
        private readonly BaseProjectileEvidenceLaunchPlan _plan;

        public FakeNative(List<string> events, BaseProjectileEvidenceLaunchPlan plan)
        {
            _events = events;
            _plan = plan;
            Preflight = ValidPreflight(plan);
            Protocol = ValidProtocol(plan);
            Frame = ValidFrame(plan);
        }

        public EvidenceNativePreflight Preflight { get; init; }

        public EvidenceProtocolCapture Protocol { get; init; }

        public EvidencePublishedFrameValidation Frame { get; init; }

        public EvidenceProcessTermination Termination { get; init; } = new(true, 0, false);

        public bool ThrowDuringFrameValidation { get; init; }

        public IReadOnlyList<EvidenceChildHandleDescriptor> Handles { get; init; } =
            Array.AsReadOnly(new[]
            {
                new EvidenceChildHandleDescriptor(EvidenceChildHandle.StandardInputRead, true),
                new EvidenceChildHandleDescriptor(EvidenceChildHandle.StandardOutputWrite, true),
                new EvidenceChildHandleDescriptor(EvidenceChildHandle.StandardErrorWrite, true),
                new EvidenceChildHandleDescriptor(EvidenceChildHandle.AcknowledgementRead, true),
            });

        public EvidenceCreateProcessContract? ProcessContract { get; private set; }

        public TimeSpan StartedDeadline { get; private set; }

        public int ObservedStandardOutputCap { get; private set; }

        public int ObservedStandardErrorCap { get; private set; }

        public BaseProjectileEvidenceJobLimits? ObservedJobLimits { get; private set; }

        public T RunOnDedicatedWorker<T>(Func<T> operation)
        {
            _events.Add("worker-enter");
            try
            {
                return operation();
            }
            finally
            {
                _events.Add("worker-exit");
            }
        }

        public string ReadInputDesktopIdentity()
        {
            _events.Add("input-desktop");
            return _plan.InputDesktopIdentity;
        }

        public EvidenceNativePreflight RevalidatePreflight(BaseProjectileEvidenceLaunchPlan plan)
        {
            _events.Add("preflight");
            return Preflight;
        }

        public IEvidenceDesktopLease CreatePrivateDesktop(string name)
        {
            _events.Add("desktop-create");
            return new FakeDesktop(_events, name);
        }

        public IEvidenceLaunchHandleLease CreateHandleAllowlist()
        {
            _events.Add("handles-create");
            return new FakeHandles(_events, Handles);
        }

        public IEvidenceProcessLease CreateSuspendedProcess(
            BaseProjectileEvidenceLaunchPlan plan,
            IEvidenceDesktopLease desktop,
            IEvidenceLaunchHandleLease handles,
            EvidenceCreateProcessContract contract)
        {
            _events.Add("process-create-suspended");
            ProcessContract = contract;
            return new FakeProcess(_events);
        }

        public IEvidenceJobLease CreateJob()
        {
            _events.Add("job-create");
            return new FakeJob(_events);
        }

        public void ConfigureJob(IEvidenceJobLease job, BaseProjectileEvidenceJobLimits limits)
        {
            _events.Add("job-configure");
            ObservedJobLimits = limits;
        }

        public void AssignProcess(IEvidenceJobLease job, IEvidenceProcessLease process) =>
            _events.Add("job-assign");

        public EvidenceDeadlineToken ResumePrimaryThreadAndStartDeadline(
            IEvidenceProcessLease process,
            TimeSpan deadline)
        {
            _events.Add("resume-and-deadline");
            StartedDeadline = deadline;
            return new EvidenceDeadlineToken(1234);
        }

        public EvidenceProtocolCapture CaptureProtocol(
            IEvidenceProcessLease process,
            EvidenceDeadlineToken deadline,
            int standardOutputCapBytes,
            int standardErrorCapBytes)
        {
            _events.Add("capture-protocol");
            Assert.Equal(1234, deadline.Value);
            ObservedStandardOutputCap = standardOutputCapBytes;
            ObservedStandardErrorCap = standardErrorCapBytes;
            return Protocol;
        }

        public EvidencePublishedFrameValidation ValidatePublishedFrame(
            BaseProjectileEvidenceLaunchPlan plan,
            DebugBaseProjectileEvidenceAttestation attestation)
        {
            _events.Add("frame-validate");
            if (ThrowDuringFrameValidation)
            {
                throw new InvalidOperationException("fake frame boundary failure");
            }
            return Frame;
        }

        public EvidenceProcessTermination WaitForProcessExit(
            IEvidenceProcessLease process,
            EvidenceDeadlineToken deadline)
        {
            _events.Add("process-wait");
            return Termination;
        }

        public bool WaitForEmptyJob(IEvidenceJobLease job, EvidenceDeadlineToken deadline)
        {
            _events.Add("job-wait-empty");
            return true;
        }

        public bool ForegroundObserverSawJobWindow(IEvidenceJobLease job)
        {
            _events.Add("foreground-check");
            return false;
        }

        private sealed class FakeDesktop(List<string> events, string name) : IEvidenceDesktopLease
        {
            public string Name { get; } = name;

            public void Dispose() => events.Add("desktop-dispose");
        }

        private sealed class FakeHandles(
            List<string> events,
            IReadOnlyList<EvidenceChildHandleDescriptor> handles) : IEvidenceLaunchHandleLease
        {
            public IReadOnlyList<EvidenceChildHandleDescriptor> ChildHandles { get; } = handles;

            public bool ParentEndpointsAreNonInheritable => true;

            public string AcknowledgementReadHandleValue => "4242";

            public void WriteAcknowledgementAndClose(byte value) =>
                events.Add($"ack-close:{value:x2}");

            public void Dispose() => events.Add("handles-dispose");
        }

        private sealed class FakeProcess(List<string> events) : IEvidenceProcessLease
        {
            public void Dispose() => events.Add("process-dispose");
        }

        private sealed class FakeJob(List<string> events) : IEvidenceJobLease
        {
            public void Dispose() => events.Add("job-dispose");
        }
    }
}
