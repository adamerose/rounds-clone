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
                "build-executable-open", "build", "build-executable-dispose", "worker-enter",
                "input-desktop", "desktop-create", "handles-create", "godot-executable-open",
                "preflight", "process-create-suspended", "child-handle-copies-close", "child-image-match", "job-create", "job-configure",
                "job-assign", "process-transfer-to-job", "foreground-start", "resume-and-deadline", "capture-protocol", "frame-validate",
                "ack-close:06", "process-wait", "job-wait-empty", "foreground-stop-read",
                "input-desktop", "job-dispose", "frame-validation-dispose", "foreground-dispose", "process-dispose", "handles-dispose",
                "godot-executable-dispose", "desktop-dispose", "worker-exit",
                "build-attestation-dispose",
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
        Assert.Equal(
            events.IndexOf("preflight") + 1,
            events.IndexOf("process-create-suspended"));
        Assert.Equal(
            events.IndexOf("process-create-suspended") + 1,
            events.IndexOf("child-handle-copies-close"));
        Assert.Equal(
            events.IndexOf("child-handle-copies-close") + 1,
            events.IndexOf("child-image-match"));
        Assert.True(events.IndexOf("foreground-start") < events.IndexOf("resume-and-deadline"));
        Assert.True(events.IndexOf("job-wait-empty") < events.IndexOf("foreground-stop-read"));
        Assert.True(events.IndexOf("frame-validate") < events.IndexOf("ack-close:06"));
        Assert.True(events.IndexOf("job-wait-empty") < events.IndexOf("frame-validation-dispose"));
        Assert.True(events.IndexOf("job-dispose") < events.IndexOf("frame-validation-dispose"));
        Assert.DoesNotContain("process-terminate-fallback", events);
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
        Assert.Equal(new[] { "build-executable-open", "build", "build-executable-dispose", "build-attestation-dispose" }, events);
    }

    [Theory]
    [InlineData("candidate")]
    [InlineData("msbuild-hash")]
    [InlineData("msbuild-version")]
    [InlineData("msbuild-handle")]
    [InlineData("runtime")]
    [InlineData("runtime-closure")]
    [InlineData("process-image")]
    [InlineData("effective-environment")]
    public void Build_identity_mismatch_refuses_before_native_worker(string field)
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var valid = ValidBuild(plan);
        var invalid = field switch
        {
            "candidate" => valid with { Candidate = valid.Candidate with { IdentityBound = false } },
            "msbuild-hash" => valid with { MsBuild = valid.MsBuild with { Sha256 = new string('0', 64) } },
            "msbuild-version" => valid with { MsBuild = valid.MsBuild with { FileVersion = "17.0" } },
            "msbuild-handle" => valid with { MsBuild = valid.MsBuild with { OpenedHandleIdentity = "" } },
            "runtime" => valid with { RuntimeAssembly = valid.RuntimeAssembly with { Mvid = new string('0', 32) } },
            "runtime-closure" => valid with
            {
                RuntimeClosure = valid.RuntimeClosure.Select((item, index) =>
                    index == 1 ? item with { OpenedHandleIdentity = "" } : item).ToArray(),
            },
            "process-image" => valid with { BuildProcessImage = valid.BuildProcessImage with { OpenedHandleIdentity = "replacement" } },
            "effective-environment" => valid with
            {
                EffectiveEnvironment = new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>(valid.EffectiveEnvironment, StringComparer.Ordinal)
                    {
                        ["NUGET_PACKAGES"] = @"C:\Users\Adam\.nuget\packages",
                    }),
            },
            _ => throw new InvalidOperationException("unknown test field"),
        };

        var result = new EvidenceLaunchOrchestrator(
            new FakeBuild(events, plan) { Override = invalid },
            new FakeNative(events, plan)).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("build-attribution", result.Code);
        Assert.Equal(new[] { "build-executable-open", "build", "build-executable-dispose", "build-attestation-dispose" }, events);
    }

    [Fact]
    public void Build_process_is_bound_to_the_retained_opened_msbuild_lease()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var build = new FakeBuild(events, plan)
        {
            LeaseIdentityOverride = ValidMsBuild() with { OpenedHandleIdentity = "replacement" },
        };

        var result = new EvidenceLaunchOrchestrator(build, new FakeNative(events, plan)).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("build-attribution", result.Code);
        Assert.DoesNotContain("worker-enter", events);
    }

    [Fact]
    public void Runtime_build_attestation_close_failure_is_fail_closed_after_native_job_cleanup()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var build = new FakeBuild(events, plan) { ThrowOnAttestationDispose = true };

        var result = new EvidenceLaunchOrchestrator(build, new FakeNative(events, plan)).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("cleanup", result.Code);
        Assert.True(events.IndexOf("job-dispose") < events.IndexOf("build-attestation-dispose"));
    }

    [Theory]
    [InlineData("success")]
    [InlineData("native-failure")]
    [InlineData("build-attribution")]
    public void Actual_attestation_one_shot_cleanup_transfers_environment_before_execute_returns(
        string mode)
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var scheduler = new EnvironmentReaperScheduler();
        var owner = new Win32EvidenceBuildEnvironmentCleanupOwner(scheduler);
        var environment = new RetryingEnvironmentLease(events, failures: 1);
        var attestation = mode == "build-attribution"
            ? ValidBuild(plan) with { ZeroWarnings = false }
            : ValidBuild(plan);
        var actual = ActualBuildLease(events, environment, owner, attestation);
        var build = new FakeBuild(events, plan) { AttestationLeaseOverride = actual };
        var native = new FakeNative(events, plan)
        {
            FailureStage = mode == "native-failure" ? "job-create" : null,
        };

        var result = new EvidenceLaunchOrchestrator(build, native).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("cleanup", result.Code);
        Assert.Equal(1, owner.RetainedCount);
        Assert.Equal(1, environment.DisposeCalls);
        Assert.True(events.IndexOf("actual-runtime-dispose") < events.IndexOf("actual-environment-dispose"));
        Assert.True(events.IndexOf("actual-environment-dispose") < events.IndexOf("actual-provenance-dispose"));

        scheduler.RunAll();
        Assert.Equal(0, owner.RetainedCount);
        Assert.Equal(2, environment.DisposeCalls);
    }

    [Fact]
    public void Actual_attestation_cancellation_transfers_environment_without_manual_second_dispose()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var scheduler = new EnvironmentReaperScheduler();
        var owner = new Win32EvidenceBuildEnvironmentCleanupOwner(scheduler);
        var environment = new RetryingEnvironmentLease(events, failures: 1);
        var actual = new CancellationReadAttestationLease(
            ActualBuildLease(events, environment, owner, ValidBuild(plan)));
        var build = new FakeBuild(events, plan) { AttestationLeaseOverride = actual };

        var failure = Assert.Throws<AggregateException>(() =>
            new EvidenceLaunchOrchestrator(build, new FakeNative(events, plan)).Execute(plan));

        Assert.Contains(failure.Flatten().InnerExceptions, value => value is OperationCanceledException);
        Assert.Equal(1, owner.RetainedCount);
        Assert.Equal(1, environment.DisposeCalls);
        scheduler.RunAll();
        Assert.Equal(0, owner.RetainedCount);
        Assert.Equal(2, environment.DisposeCalls);
    }

    [Fact]
    public void Actual_attestation_scheduler_failure_stays_strongly_owned_before_execute_returns()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var scheduler = new EnvironmentReaperScheduler { FailSchedule = true };
        var owner = new Win32EvidenceBuildEnvironmentCleanupOwner(scheduler);
        var environment = new RetryingEnvironmentLease(events, failures: 1);
        var actual = ActualBuildLease(events, environment, owner, ValidBuild(plan));
        var build = new FakeBuild(events, plan) { AttestationLeaseOverride = actual };

        var result = new EvidenceLaunchOrchestrator(build, new FakeNative(events, plan)).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("cleanup", result.Code);
        Assert.Equal(1, owner.RetainedCount);
        Assert.Equal(1, environment.DisposeCalls);
        scheduler.FailSchedule = false;
        owner.RetryRetained();
        scheduler.RunAll();
        Assert.Equal(0, owner.RetainedCount);
        Assert.Equal(2, environment.DisposeCalls);
    }

    [Theory]
    [InlineData("success")]
    [InlineData("native-failure")]
    [InlineData("build-attribution")]
    public void Actual_attestation_one_shot_cleanup_transfers_provenance_before_execute_returns(
        string mode)
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var scheduler = new EnvironmentReaperScheduler();
        var provenanceOwner = new Win32EvidenceBuildProvenanceCleanupOwner(scheduler);
        var provenance = new ActualProvenanceLease(events, failures: 1);
        var attestation = mode == "build-attribution"
            ? ValidBuild(plan) with { ZeroWarnings = false }
            : ValidBuild(plan);
        var actual = ActualBuildLease(
            events,
            new RetryingEnvironmentLease(events, failures: 0),
            new Win32EvidenceBuildEnvironmentCleanupOwner(scheduler),
            attestation,
            provenance,
            provenanceOwner);
        var build = new FakeBuild(events, plan) { AttestationLeaseOverride = actual };
        var native = new FakeNative(events, plan)
        {
            FailureStage = mode == "native-failure" ? "job-create" : null,
        };

        var result = new EvidenceLaunchOrchestrator(build, native).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("cleanup", result.Code);
        Assert.Equal(1, provenanceOwner.RetainedCount);
        Assert.Equal(1, provenance.DisposeCalls);
        Assert.True(events.IndexOf("actual-environment-dispose") < events.IndexOf("actual-provenance-dispose"));
        scheduler.RunAll();
        Assert.Equal(0, provenanceOwner.RetainedCount);
        Assert.Equal(2, provenance.DisposeCalls);
    }

    [Fact]
    public void Actual_attestation_cancellation_transfers_provenance_before_throw()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var scheduler = new EnvironmentReaperScheduler();
        var provenanceOwner = new Win32EvidenceBuildProvenanceCleanupOwner(scheduler);
        var provenance = new ActualProvenanceLease(events, failures: 1);
        var actual = new CancellationReadAttestationLease(ActualBuildLease(
            events,
            new RetryingEnvironmentLease(events, failures: 0),
            new Win32EvidenceBuildEnvironmentCleanupOwner(scheduler),
            ValidBuild(plan),
            provenance,
            provenanceOwner));
        var build = new FakeBuild(events, plan) { AttestationLeaseOverride = actual };

        var failure = Assert.Throws<AggregateException>(() =>
            new EvidenceLaunchOrchestrator(build, new FakeNative(events, plan)).Execute(plan));

        Assert.Contains(failure.Flatten().InnerExceptions, value => value is OperationCanceledException);
        Assert.Equal(1, provenanceOwner.RetainedCount);
        Assert.Equal(1, provenance.DisposeCalls);
        scheduler.RunAll();
        Assert.Equal(0, provenanceOwner.RetainedCount);
        Assert.Equal(2, provenance.DisposeCalls);
    }

    [Fact]
    public void Actual_provenance_scheduler_failure_remains_strongly_owned_before_execute_returns()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var scheduler = new EnvironmentReaperScheduler { FailSchedule = true };
        var provenanceOwner = new Win32EvidenceBuildProvenanceCleanupOwner(scheduler);
        var provenance = new ActualProvenanceLease(events, failures: 1);
        var actual = ActualBuildLease(
            events,
            new RetryingEnvironmentLease(events, failures: 0),
            new Win32EvidenceBuildEnvironmentCleanupOwner(scheduler),
            ValidBuild(plan),
            provenance,
            provenanceOwner);
        var build = new FakeBuild(events, plan) { AttestationLeaseOverride = actual };

        var result = new EvidenceLaunchOrchestrator(build, new FakeNative(events, plan)).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("cleanup", result.Code);
        Assert.Equal(1, provenanceOwner.RetainedCount);
        Assert.Equal(1, provenance.DisposeCalls);
        scheduler.FailSchedule = false;
        provenanceOwner.RetryRetained();
        scheduler.RunAll();
        Assert.Equal(0, provenanceOwner.RetainedCount);
        Assert.Equal(2, provenance.DisposeCalls);
    }

    [Fact]
    public void Msbuild_lease_close_failure_disposes_runtime_attestation_and_refuses_native()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var build = new FakeBuild(events, plan) { ThrowOnExecutableDispose = true };

        var result = new EvidenceLaunchOrchestrator(build, new FakeNative(events, plan)).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("cleanup", result.Code);
        Assert.DoesNotContain("worker-enter", events);
        Assert.Equal("build-attestation-dispose", events[^1]);
    }

    [Fact]
    public void Build_cancellation_aggregates_attestation_and_msbuild_cleanup_failures()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var build = new FakeBuild(events, plan)
        {
            ThrowCancellationOnAttestationRead = true,
            ThrowOnAttestationDispose = true,
            ThrowOnExecutableDispose = true,
        };

        var failure = Assert.Throws<AggregateException>(() =>
            new EvidenceLaunchOrchestrator(build, new FakeNative(events, plan)).Execute(plan));

        Assert.Equal(3, failure.Flatten().InnerExceptions.Count);
        Assert.Contains(failure.Flatten().InnerExceptions, value => value is OperationCanceledException);
        Assert.Equal(1, events.Count(value => value == "build-attestation-dispose"));
        Assert.Equal(1, events.Count(value => value == "build-executable-dispose"));
        Assert.DoesNotContain("worker-enter", events);
    }

    [Fact]
    public void Throwing_attestation_collection_disposes_lease_once_and_refuses_native()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var invalid = ValidBuild(plan) with
        {
            RuntimeClosure = new ThrowingReadOnlyList<EvidenceRuntimeAssemblyIdentity>(),
        };

        var result = new EvidenceLaunchOrchestrator(
            new FakeBuild(events, plan) { Override = invalid },
            new FakeNative(events, plan)).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("build-driver", result.Code);
        Assert.Equal(1, events.Count(value => value == "build-attestation-dispose"));
        Assert.DoesNotContain("worker-enter", events);
    }

    [Fact]
    public void Throwing_effective_environment_disposes_lease_once_and_refuses_native()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var invalid = ValidBuild(plan) with
        {
            EffectiveEnvironment = new ThrowingReadOnlyDictionary(),
        };

        var result = new EvidenceLaunchOrchestrator(
            new FakeBuild(events, plan) { Override = invalid },
            new FakeNative(events, plan)).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("build-driver", result.Code);
        Assert.Equal(1, events.Count(value => value == "build-attestation-dispose"));
        Assert.DoesNotContain("worker-enter", events);
    }

    [Fact]
    public void Mutable_attestation_collection_is_snapshotted_before_validation_and_native_use()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var mutable = new OneShotReadOnlyList<EvidenceRuntimeAssemblyIdentity>(ValidRuntimeClosure(plan));
        var attestation = ValidBuild(plan) with { RuntimeClosure = mutable };

        var result = new EvidenceLaunchOrchestrator(
            new FakeBuild(events, plan) { Override = attestation },
            new FakeNative(events, plan)).Execute(plan);

        Assert.True(result.Success);
        Assert.Empty(mutable);
        Assert.Equal(1, events.Count(value => value == "build-attestation-dispose"));
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
            new[] { "handles-dispose", "desktop-dispose", "worker-exit", "build-attestation-dispose" },
            events.TakeLast(4));
    }

    [Fact]
    public void Zero_acknowledgement_handle_is_rejected_before_process_creation()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var native = new FakeNative(events, plan) { AcknowledgementHandleValue = "0" };

        var result = new EvidenceLaunchOrchestrator(new FakeBuild(events, plan), native).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("handle-allowlist", result.Code);
        Assert.DoesNotContain("process-create-suspended", events);
    }

    [Theory]
    [InlineData("job-create")]
    [InlineData("job-configure")]
    [InlineData("job-assign")]
    public void Suspended_process_is_terminated_if_job_ownership_cannot_be_established(string failureStage)
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var native = new FakeNative(events, plan) { FailureStage = failureStage };

        var result = new EvidenceLaunchOrchestrator(new FakeBuild(events, plan), native).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("native-boundary", result.Code);
        Assert.Contains("process-terminate-fallback", events);
        Assert.DoesNotContain("resume-and-deadline", events);
        if (failureStage != "job-create")
        {
            Assert.True(events.IndexOf("job-dispose") < events.IndexOf("process-terminate-fallback"));
        }
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
        Assert.True(events.IndexOf("job-dispose") < events.IndexOf("frame-validation-dispose"));
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
        Assert.Equal("worker-exit", events[^2]);
        Assert.Equal("build-attestation-dispose", events[^1]);
    }

    [Fact]
    public void Cooperative_nonzero_exit_after_ack_preserves_published_residue()
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
        Assert.Equal(plan.OutputRoot, result.PreservedUnprovenResidueRoot);
        Assert.Contains("ack-close:06", events);
        Assert.True(events.IndexOf("process-wait") < events.IndexOf("frame-validation-dispose"));
        Assert.True(events.IndexOf("job-dispose") < events.IndexOf("frame-validation-dispose"));
    }

    [Fact]
    public void Foreground_event_after_ack_preserves_exact_output_residue()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var native = new FakeNative(events, plan) { SawForegroundWindow = true };

        var result = new EvidenceLaunchOrchestrator(new FakeBuild(events, plan), native).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("foreground-activation", result.Code);
        Assert.Equal(plan.OutputRoot, result.PreservedUnprovenResidueRoot);
    }

    [Theory]
    [InlineData("job-empty")]
    [InlineData("desktop-change")]
    [InlineData("postflight-exception")]
    public void Every_post_ack_postflight_failure_preserves_exact_output_residue(string failure)
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var native = new FakeNative(events, plan)
        {
            JobEmpty = failure != "job-empty",
            PostflightDesktopIdentity = failure == "desktop-change" ? "WinSta0\\Changed" : null,
            ThrowDuringForegroundStop = failure == "postflight-exception",
        };

        var result = new EvidenceLaunchOrchestrator(new FakeBuild(events, plan), native).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal(plan.OutputRoot, result.PreservedUnprovenResidueRoot);
        Assert.Contains("ack-close:06", events);
        if (failure == "job-empty")
        {
            Assert.Equal("job-not-empty", result.Code);
            Assert.DoesNotContain("foreground-stop-read", events);
        }
        else if (failure == "desktop-change")
        {
            Assert.Equal("input-desktop-changed", result.Code);
        }
        else
        {
            Assert.Equal("native-boundary", result.Code);
        }
    }

    [Fact]
    public void Dedicated_worker_failure_after_ack_still_reports_exact_output_residue()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var native = new FakeNative(events, plan) { ThrowAfterWorkerOperation = true };

        var result = new EvidenceLaunchOrchestrator(new FakeBuild(events, plan), native).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("native-boundary", result.Code);
        Assert.Equal(plan.OutputRoot, result.PreservedUnprovenResidueRoot);
        Assert.Contains("ack-close:06", events);
    }

    [Theory]
    [InlineData("job")]
    [InlineData("frame-validation")]
    [InlineData("foreground")]
    [InlineData("process")]
    [InlineData("handles")]
    [InlineData("desktop")]
    [InlineData("executable")]
    public void Disposal_failure_never_skips_later_owned_lease_cleanup(string lease)
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var native = new FakeNative(events, plan) { DisposeFailure = lease };

        var result = new EvidenceLaunchOrchestrator(new FakeBuild(events, plan), native).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("cleanup", result.Code);
        Assert.Equal(plan.OutputRoot, result.PreservedUnprovenResidueRoot);
        Assert.Contains("job-dispose", events);
        Assert.Contains("frame-validation-dispose", events);
        Assert.Contains("foreground-dispose", events);
        Assert.Contains("process-dispose", events);
        Assert.Contains("handles-dispose", events);
        Assert.Contains("godot-executable-dispose", events);
        Assert.Contains("desktop-dispose", events);
    }

    [Fact]
    public void Failed_assignment_still_terminates_process_when_job_disposal_throws()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var native = new FakeNative(events, plan)
        {
            FailureStage = "job-assign",
            DisposeFailure = "job",
        };

        var result = new EvidenceLaunchOrchestrator(new FakeBuild(events, plan), native).Execute(plan);

        Assert.False(result.Success);
        Assert.Contains("job-dispose", events);
        Assert.Contains("process-terminate-fallback", events);
        Assert.Contains("process-dispose", events);
        Assert.Contains("handles-dispose", events);
        Assert.Contains("godot-executable-dispose", events);
        Assert.Contains("desktop-dispose", events);
    }

    [Fact]
    public void Assigned_resumed_process_uses_explicit_fallback_when_job_close_throws_before_empty_proof()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var native = new FakeNative(events, plan)
        {
            Protocol = ValidProtocol(plan) with { TimedOut = true },
            DisposeFailure = "job",
        };

        var result = new EvidenceLaunchOrchestrator(new FakeBuild(events, plan), native).Execute(plan);

        Assert.False(result.Success);
        Assert.Contains("process-transfer-to-job", events);
        Assert.Contains("resume-and-deadline", events);
        Assert.Contains("job-dispose", events);
        Assert.Contains("process-terminate-fallback", events);
        Assert.True(events.IndexOf("job-dispose") < events.IndexOf("process-terminate-fallback"));
    }

    [Theory]
    [InlineData("output")]
    [InlineData("candidate")]
    [InlineData("godot-identity")]
    [InlineData("godot-hash")]
    [InlineData("godot-version")]
    [InlineData("godot-handle")]
    [InlineData("runtime")]
    public void Preflight_identity_or_topology_drift_refuses_immediately_before_process(string field)
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var valid = ValidPreflight(plan);
        var invalid = field switch
        {
            "output" => valid with { OutputRootAbsent = false },
            "candidate" => valid with { Candidate = valid.Candidate with { CleanHead = false } },
            "godot-identity" => valid with { Godot = valid.Godot with { IdentityBound = false } },
            "godot-hash" => valid with { Godot = valid.Godot with { Sha256 = new string('0', 64) } },
            "godot-version" => valid with { Godot = valid.Godot with { ProductVersion = "4.7.2" } },
            "godot-handle" => valid with { Godot = valid.Godot with { OpenedHandleIdentity = "" } },
            "runtime" => valid with { RuntimeAssembly = valid.RuntimeAssembly with { Sha256 = new string('0', 64) } },
            _ => throw new InvalidOperationException("unknown test field"),
        };
        var native = new FakeNative(events, plan) { Preflight = invalid };

        var result = new EvidenceLaunchOrchestrator(new FakeBuild(events, plan), native).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("preflight", result.Code);
        Assert.Contains("desktop-create", events);
        Assert.Contains("handles-create", events);
        Assert.True(events.IndexOf("godot-executable-open") < events.IndexOf("preflight"));
        Assert.DoesNotContain("process-create-suspended", events);
        Assert.Equal("worker-exit", events[^2]);
        Assert.Equal("build-attestation-dispose", events[^1]);
    }

    [Fact]
    public void Suspended_child_image_replacement_refuses_before_resume_and_terminates_process()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var native = new FakeNative(events, plan)
        {
            SuspendedProcessImage = ValidGodot(plan) with { OpenedHandleIdentity = "replacement" },
        };

        var result = new EvidenceLaunchOrchestrator(new FakeBuild(events, plan), native).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("child-image-identity", result.Code);
        Assert.Contains("child-image-match", events);
        Assert.Contains("process-terminate-fallback", events);
        Assert.DoesNotContain("resume-and-deadline", events);
    }

    [Fact]
    public void Child_handle_transition_failure_after_create_terminates_before_image_check_or_resume()
    {
        var events = new List<string>();
        var plan = ValidPlan();
        var native = new FakeNative(events, plan) { FailureStage = "child-handle-close" };

        var result = new EvidenceLaunchOrchestrator(new FakeBuild(events, plan), native).Execute(plan);

        Assert.False(result.Success);
        Assert.Equal("native-boundary", result.Code);
        Assert.True(events.IndexOf("process-create-suspended") < events.IndexOf("child-handle-copies-close"));
        Assert.Contains("process-terminate-fallback", events);
        Assert.DoesNotContain("child-image-match", events);
        Assert.DoesNotContain("resume-and-deadline", events);
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

    [Theory]
    [InlineData(4, EvidenceLauncherArchitecture.Refusal)]
    [InlineData(16, EvidenceLauncherArchitecture.Refusal)]
    [InlineData(EvidenceLauncherArchitecture.RequiredPointerSize, null)]
    public void Launcher_architecture_refuses_every_non_x64_pointer_size(
        int pointerSize,
        string? expectedRefusal)
    {
        Assert.Equal(
            expectedRefusal,
            EvidenceLauncherArchitecture.RefusalForPointerSize(pointerSize));
    }

    [Fact]
    public void Program_refuses_non_x64_before_command_parsing_and_keeps_x64_execute_inert()
    {
        using var nonX64Error = new StringWriter();
        using var x64Error = new StringWriter();

        var nonX64Exit = EvidenceLauncherEntry.Run(
            Array.Empty<string>(),
            4,
            nonX64Error);
        var x64Exit = EvidenceLauncherEntry.Run(
            new[] { "execute" },
            8,
            x64Error);

        Assert.Equal(2, nonX64Exit);
        Assert.Equal(EvidenceLauncherArchitecture.Refusal + "\n", nonX64Error.ToString());
        Assert.Equal(2, x64Exit);
        Assert.Equal("native-boundary-not-installed\n", x64Error.ToString());
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
            repository + @"\game\.godot\mono\temp\bin\Debug\Rounds.Game.dll",
            new string('a', 64),
            new string('b', 32),
            "WinSta0\\Default",
            ancestors);
    }

    private static EvidenceBuildAttestation ValidBuild(BaseProjectileEvidenceLaunchPlan plan) =>
        new(
            EvidenceBuildContract.Create(plan),
            ValidBuildEnvironment(plan),
            ValidCandidate(plan),
            ValidMsBuild(),
            ValidMsBuild(),
            ValidRuntimeAssembly(plan),
            ValidRuntimeClosure(plan),
            ZeroWarnings: true,
            DeletedPriorOutput: true);

    private static IReadOnlyDictionary<string, string> ValidBuildEnvironment(BaseProjectileEvidenceLaunchPlan plan) =>
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SystemRoot"] = @"C:\Windows",
            ["WINDIR"] = @"C:\Windows",
            ["TEMP"] = @"C:\Temp",
            ["TMP"] = @"C:\Temp",
            ["DOTNET_PROCESSOR_COUNT"] = "2",
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["MSBuildEnableWorkloadResolver"] = "false",
            ["MSBuildSDKsPath"] = plan.RepositoryRoot + @"\.tools\dotnet\sdk\8.0.423\Sdks",
            ["DOTNET_CLI_UI_LANGUAGE"] = "en-US",
            ["VSLANG"] = "1033",
            ["NUGET_PACKAGES"] = plan.RepositoryRoot + @"\.tools\nuget-packages",
            ["DOTNET_CLI_HOME"] = plan.RepositoryRoot + @"\.tools\dotnet-home",
            ["MSBuildUserExtensionsPath"] = plan.RepositoryRoot + @"\.tools\empty\msbuild-user",
        });

    private static EvidenceCandidateIdentity ValidCandidate(BaseProjectileEvidenceLaunchPlan plan) =>
        new(
            plan.RepositoryRoot,
            plan.CandidateCommit,
            CleanHead: true,
            IdentityBound: true,
            RepositoryHandleIdentity: "repo-volume:42:file:100");

    private static EvidenceOpenedExecutableIdentity ValidMsBuild() =>
        new(
            BaseProjectileEvidenceLaunchPlanner.MsBuildPath,
            Exists: true,
            IdentityBound: true,
            IsReparsePoint: false,
            OpenedHandleIdentity: "msbuild-volume:1:file:200",
            BaseProjectileEvidenceLaunchPlanner.MsBuildSha256,
            BaseProjectileEvidenceLaunchPlanner.MsBuildFileVersion,
            BaseProjectileEvidenceLaunchPlanner.MsBuildProductVersion);

    private static EvidenceOpenedExecutableIdentity ValidGodot(BaseProjectileEvidenceLaunchPlan plan) =>
        new(
            plan.Executable,
            Exists: true,
            IdentityBound: true,
            IsReparsePoint: false,
            OpenedHandleIdentity: "godot-volume:42:file:300",
            BaseProjectileEvidenceLaunchPlanner.GodotSha256,
            BaseProjectileEvidenceLaunchPlanner.GodotFileVersion,
            BaseProjectileEvidenceLaunchPlanner.GodotVersion);

    private static EvidenceRuntimeAssemblyIdentity ValidRuntimeAssembly(
        BaseProjectileEvidenceLaunchPlan plan) =>
        new(
            plan.RuntimeAssemblyPath,
            Exists: true,
            IdentityBound: true,
            IsReparsePoint: false,
            RecreatedByImmediateRebuild: true,
            OpenedHandleIdentity: "runtime-volume:42:file:400",
            plan.RuntimeAssemblySha256,
            plan.RuntimeAssemblyMvid);

    private static IReadOnlyList<EvidenceRuntimeAssemblyIdentity> ValidRuntimeClosure(
        BaseProjectileEvidenceLaunchPlan plan)
    {
        var directory = Path.GetDirectoryName(plan.RuntimeAssemblyPath)!;
        return
        [
            ValidRuntimeAssembly(plan),
            ValidRuntimeAssembly(plan) with
            {
                Path = Path.Combine(directory, "Rounds.Replay.dll"),
                OpenedHandleIdentity = "runtime-volume:42:file:401",
                Sha256 = new string('c', 64),
                Mvid = new string('d', 32),
            },
            ValidRuntimeAssembly(plan) with
            {
                Path = Path.Combine(directory, "Rounds.Sim.dll"),
                OpenedHandleIdentity = "runtime-volume:42:file:402",
                Sha256 = new string('e', 64),
                Mvid = new string('f', 32),
            },
        ];
    }

    private static EvidenceNativePreflight ValidPreflight(BaseProjectileEvidenceLaunchPlan plan) =>
        new(
            new EvidenceMonitorFacts(
                BaseProjectileEvidenceLaunchPlanner.DisplayDevice,
                plan.Screen,
                plan.MonitorBounds,
                PerMonitorV2DpiAware: true),
            plan.InputDesktopIdentity,
            OutputRootAbsent: true,
            plan.OutputAncestors,
            ValidCandidate(plan),
            ValidGodot(plan),
            ValidRuntimeAssembly(plan));

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

    private static IEvidenceBuildAttestationLease ActualBuildLease(
        List<string> events,
        RetryingEnvironmentLease environment,
        IEvidenceBuildEnvironmentCleanupOwner owner,
        EvidenceBuildAttestation attestation,
        IEvidenceBuildProvenanceLease? provenance = null,
        IEvidenceBuildProvenanceCleanupOwner? provenanceOwner = null) =>
        new Win32EvidenceBuildAttestationLease(
            new ActualRuntimeLease(events, attestation.RuntimeAssembly, attestation.RuntimeClosure),
            [],
            environment,
            provenance ?? new ActualProvenanceLease(events),
            owner,
            provenanceOwner ?? new Win32EvidenceBuildProvenanceCleanupOwner(new EnvironmentReaperScheduler()),
            attestation);

    private sealed class RetryingEnvironmentLease(List<string> events, int failures) :
        IEvidenceBuildEnvironmentLease
    {
        private int _failures = failures;
        internal int DisposeCalls { get; private set; }
        public IReadOnlyDictionary<string, string> Environment { get; } =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>());
        public EvidenceBuildEnvironmentRevalidation Revalidate() =>
            new("cli", "msbuild", true, true, true, true, true);
        public void Dispose()
        {
            DisposeCalls++;
            events.Add("actual-environment-dispose");
            if (_failures-- > 0) throw new InvalidOperationException("actual environment cleanup failed");
        }
    }

    private sealed class ActualRuntimeLease(
        List<string> events,
        EvidenceRuntimeAssemblyIdentity identity,
        IReadOnlyList<EvidenceRuntimeAssemblyIdentity> closure) : IEvidenceRuntimeAssemblyLease
    {
        public EvidenceRuntimeAssemblyIdentity Identity { get; } = identity;
        public IReadOnlyList<EvidenceRuntimeAssemblyIdentity> RuntimeClosure { get; } = closure;
        public bool ReparseFreeAncestorChains => true;
        public void Dispose() => events.Add("actual-runtime-dispose");
    }

    private sealed class ActualProvenanceLease(List<string> events, int failures = 0) : IEvidenceBuildProvenanceLease
    {
        private int _failures = failures;
        internal int DisposeCalls { get; private set; }
        public EvidenceCandidateIdentity Candidate => throw new NotSupportedException();
        public EvidenceBuildPrerequisiteAttestation Prerequisites => throw new NotSupportedException();
        public EvidenceTrustedDirectoryIdentity SystemRoot => throw new NotSupportedException();
        public EvidenceTrustedDirectoryIdentity TemporaryDirectory => throw new NotSupportedException();
        public bool RetainsExactRepositoryInputAndOutputAncestorChains => true;
        public EvidenceBuildProvenanceSnapshot Revalidate() => throw new NotSupportedException();
        public void Dispose()
        {
            DisposeCalls++;
            events.Add("actual-provenance-dispose");
            if (_failures-- > 0) throw new InvalidOperationException("actual provenance cleanup failed");
        }
    }

    private sealed class CancellationReadAttestationLease(IEvidenceBuildAttestationLease inner) :
        IEvidenceBuildAttestationLease
    {
        public EvidenceBuildAttestation Attestation =>
            throw new OperationCanceledException("actual attestation cancellation");
        public void Dispose() => inner.Dispose();
    }

    private sealed class EnvironmentReaperScheduler : IEvidenceBuildCleanupReaperScheduler
    {
        private readonly Queue<Action> _actions = [];
        internal bool FailSchedule { get; set; }
        public void Schedule(Action action)
        {
            if (FailSchedule) throw new InvalidOperationException("actual cleanup scheduler failure");
            _actions.Enqueue(action);
        }
        public void Backoff(TimeSpan delay) => Assert.Equal(TimeSpan.FromSeconds(1), delay);
        internal void RunAll()
        {
            while (_actions.TryDequeue(out var action)) action();
        }
    }

    private sealed class FakeBuild(List<string> events, BaseProjectileEvidenceLaunchPlan plan) : IEvidenceBuildDriver
    {
        public EvidenceBuildAttestation? Override { get; init; }

        public EvidenceOpenedExecutableIdentity? LeaseIdentityOverride { get; init; }

        public bool ThrowOnAttestationDispose { get; init; }

        public bool ThrowOnExecutableDispose { get; init; }

        public bool ThrowCancellationOnAttestationRead { get; init; }

        public IEvidenceBuildAttestationLease? AttestationLeaseOverride { get; init; }

        public IEvidenceExecutableLease OpenMsBuildExecutable(EvidenceBuildInvocation required)
        {
            events.Add("build-executable-open");
            return new FakeExecutableLease(
                events,
                LeaseIdentityOverride ?? ValidMsBuild(),
                "build-executable-dispose",
                ThrowOnExecutableDispose);
        }

        public IEvidenceBuildAttestationLease RebuildAndAttest(
            EvidenceBuildInvocation required,
            IEvidenceExecutableLease msBuildExecutable)
        {
            events.Add("build");
            if (AttestationLeaseOverride is not null) return AttestationLeaseOverride;
            var attestation = Override ?? ValidBuild(plan);
            return new FakeBuildAttestationLease(
                events,
                attestation with { Invocation = required },
                ThrowOnAttestationDispose,
                ThrowCancellationOnAttestationRead);
        }
    }

    private sealed class FakeBuildAttestationLease(
        List<string> events,
        EvidenceBuildAttestation attestation,
        bool throwOnDispose,
        bool throwCancellationOnRead) : IEvidenceBuildAttestationLease
    {
        private bool _disposed;
        public EvidenceBuildAttestation Attestation => throwCancellationOnRead
            ? throw new OperationCanceledException("fake attestation cancellation")
            : attestation;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            events.Add("build-attestation-dispose");
            if (throwOnDispose) throw new InvalidOperationException("fake build-attestation disposal failure");
        }
    }

    private sealed class ThrowingReadOnlyList<T> : IReadOnlyList<T>
    {
        public int Count => 3;
        public T this[int index] => throw new InvalidOperationException("hostile attestation indexer");
        public IEnumerator<T> GetEnumerator() => throw new InvalidOperationException("hostile attestation enumeration");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class ThrowingReadOnlyDictionary : IReadOnlyDictionary<string, string>
    {
        public int Count => 13;
        public IEnumerable<string> Keys => throw new InvalidOperationException("hostile keys");
        public IEnumerable<string> Values => throw new InvalidOperationException("hostile values");
        public string this[string key] => throw new InvalidOperationException("hostile indexer");
        public bool ContainsKey(string key) => throw new InvalidOperationException("hostile lookup");
        public bool TryGetValue(string key, out string value) => throw new InvalidOperationException("hostile lookup");
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() =>
            throw new InvalidOperationException("hostile attestation enumeration");
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class OneShotReadOnlyList<T>(IEnumerable<T> values) : IReadOnlyList<T>
    {
        private readonly List<T> _values = values.ToList();
        public int Count => _values.Count;
        public T this[int index] => _values[index];
        public IEnumerator<T> GetEnumerator()
        {
            var snapshot = _values.ToArray();
            _values.Clear();
            return ((IEnumerable<T>)snapshot).GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class FakeExecutableLease(
        List<string> events,
        EvidenceOpenedExecutableIdentity identity,
        string disposeEvent,
        bool throwOnDispose) : IEvidenceExecutableLease
    {
        public EvidenceOpenedExecutableIdentity Identity { get; } = identity;

        public void Dispose()
        {
            events.Add(disposeEvent);
            if (throwOnDispose)
            {
                throw new InvalidOperationException("fake executable disposal failure");
            }
        }
    }

    private sealed class FakeNative : IEvidenceNativeBoundary
    {
        private readonly List<string> _events;
        private readonly BaseProjectileEvidenceLaunchPlan _plan;
        private int _inputDesktopReads;

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

        public string? FailureStage { get; init; }

        public string AcknowledgementHandleValue { get; init; } = "4242";

        public bool SawForegroundWindow { get; init; }

        public bool JobEmpty { get; init; } = true;

        public string? PostflightDesktopIdentity { get; init; }

        public bool ThrowDuringForegroundStop { get; init; }

        public bool ThrowAfterWorkerOperation { get; init; }

        public string? DisposeFailure { get; init; }

        public EvidenceOpenedExecutableIdentity? SuspendedProcessImage { get; init; }

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
                var result = operation();
                if (ThrowAfterWorkerOperation)
                {
                    throw new InvalidOperationException("fake worker teardown failure");
                }
                return result;
            }
            finally
            {
                _events.Add("worker-exit");
            }
        }

        public string ReadInputDesktopIdentity()
        {
            _events.Add("input-desktop");
            _inputDesktopReads++;
            return _inputDesktopReads > 1 && PostflightDesktopIdentity is not null
                ? PostflightDesktopIdentity
                : _plan.InputDesktopIdentity;
        }

        public EvidenceNativePreflight RevalidatePreflight(BaseProjectileEvidenceLaunchPlan plan)
        {
            _events.Add("preflight");
            return Preflight;
        }

        public IEvidenceExecutableLease OpenGodotExecutable(BaseProjectileEvidenceLaunchPlan plan)
        {
            _events.Add("godot-executable-open");
            return new FakeExecutableLease(
                _events,
                ValidGodot(plan),
                "godot-executable-dispose",
                DisposeFailure == "executable");
        }

        public IEvidenceDesktopLease CreatePrivateDesktop(string name)
        {
            _events.Add("desktop-create");
            return new FakeDesktop(_events, name, DisposeFailure == "desktop");
        }

        public IEvidenceLaunchHandleLease CreateHandleAllowlist()
        {
            _events.Add("handles-create");
            return new FakeHandles(
                _events,
                Handles,
                AcknowledgementHandleValue,
                FailureStage == "child-handle-close",
                DisposeFailure == "handles");
        }

        public EvidenceProcessLease CreateSuspendedProcess(
            BaseProjectileEvidenceLaunchPlan plan,
            IEvidenceDesktopLease desktop,
            IEvidenceLaunchHandleLease handles,
            IEvidenceExecutableLease executable,
            EvidenceCreateProcessContract contract)
        {
            _events.Add("process-create-suspended");
            Assert.Equal(ValidGodot(plan), executable.Identity);
            ProcessContract = contract;
            return new FakeProcess(_events, DisposeFailure == "process");
        }

        public EvidenceOpenedExecutableIdentity ReadSuspendedProcessImageIdentity(
            EvidenceProcessLease process)
        {
            _events.Add("child-image-match");
            return SuspendedProcessImage ?? ValidGodot(_plan);
        }

        public IEvidenceJobLease CreateJob()
        {
            _events.Add("job-create");
            if (FailureStage == "job-create")
            {
                throw new InvalidOperationException("fake job create failure");
            }
            return new FakeJob(_events, DisposeFailure == "job");
        }

        public void ConfigureJob(IEvidenceJobLease job, BaseProjectileEvidenceJobLimits limits)
        {
            _events.Add("job-configure");
            if (FailureStage == "job-configure")
            {
                throw new InvalidOperationException("fake job configure failure");
            }
            ObservedJobLimits = limits;
        }

        public void AssignProcess(IEvidenceJobLease job, EvidenceProcessLease process)
        {
            _events.Add("job-assign");
            if (FailureStage == "job-assign")
            {
                throw new InvalidOperationException("fake job assign failure");
            }
        }

        public IEvidenceForegroundObserverLease StartForegroundObserver(IEvidenceJobLease job)
        {
            _events.Add("foreground-start");
            return new FakeForegroundObserver(
                _events,
                SawForegroundWindow,
                ThrowDuringForegroundStop,
                DisposeFailure == "foreground");
        }

        public EvidenceDeadlineToken ResumePrimaryThreadAndStartDeadline(
            EvidenceProcessLease process,
            TimeSpan deadline)
        {
            _events.Add("resume-and-deadline");
            StartedDeadline = deadline;
            return new EvidenceDeadlineToken(1234);
        }

        public EvidenceProtocolCapture CaptureProtocol(
            EvidenceProcessLease process,
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

        public IEvidencePublishedFrameValidationLease ValidatePublishedFrame(
            BaseProjectileEvidenceLaunchPlan plan,
            DebugBaseProjectileEvidenceAttestation attestation)
        {
            _events.Add("frame-validate");
            if (ThrowDuringFrameValidation)
            {
                throw new InvalidOperationException("fake frame boundary failure");
            }
            return new FakeFrameValidationLease(
                _events,
                Frame,
                DisposeFailure == "frame-validation");
        }

        public EvidenceProcessTermination WaitForProcessExit(
            EvidenceProcessLease process,
            EvidenceDeadlineToken deadline)
        {
            _events.Add("process-wait");
            return Termination;
        }

        public bool WaitForEmptyJob(IEvidenceJobLease job, EvidenceDeadlineToken deadline)
        {
            _events.Add("job-wait-empty");
            return JobEmpty;
        }

        private sealed class FakeDesktop(
            List<string> events,
            string name,
            bool throwOnDispose) : IEvidenceDesktopLease
        {
            public string Name { get; } = name;

            public void Dispose()
            {
                events.Add("desktop-dispose");
                if (throwOnDispose) throw new InvalidOperationException("fake desktop disposal failure");
            }
        }

        private sealed class FakeFrameValidationLease(
            List<string> events,
            EvidencePublishedFrameValidation validation,
            bool throwOnDispose) : IEvidencePublishedFrameValidationLease
        {
            public EvidencePublishedFrameValidation Validation { get; } = validation;

            public void Dispose()
            {
                events.Add("frame-validation-dispose");
                if (throwOnDispose)
                {
                    throw new InvalidOperationException("fake frame-validation disposal failure");
                }
            }
        }

        private sealed class FakeHandles(
            List<string> events,
            IReadOnlyList<EvidenceChildHandleDescriptor> handles,
            string acknowledgementHandleValue,
            bool throwOnProcessCreationComplete,
            bool throwOnDispose) : IEvidenceLaunchHandleLease
        {
            public IReadOnlyList<EvidenceChildHandleDescriptor> ChildHandles { get; } = handles;

            public bool ParentEndpointsAreNonInheritable => true;

            public string AcknowledgementReadHandleValue { get; } = acknowledgementHandleValue;

            public void CompleteSuccessfulProcessCreation()
            {
                events.Add("child-handle-copies-close");
                if (throwOnProcessCreationComplete)
                {
                    throw new InvalidOperationException("fake child handle close failure");
                }
            }

            public void WriteAcknowledgementAndClose(byte value) =>
                events.Add($"ack-close:{value:x2}");

            public void Dispose()
            {
                events.Add("handles-dispose");
                if (throwOnDispose) throw new InvalidOperationException("fake handle disposal failure");
            }
        }

        private sealed class FakeProcess(List<string> events, bool throwOnDispose) : EvidenceProcessLease
        {
            protected override void OnTerminationTransferredToJob() =>
                events.Add("process-transfer-to-job");

            protected override void TerminateAndWaitForExit() =>
                events.Add("process-terminate-fallback");

            protected override void ReleaseProcessAndThreadHandles() =>
                AddDisposeEvent();

            private void AddDisposeEvent()
            {
                events.Add("process-dispose");
                if (throwOnDispose) throw new InvalidOperationException("fake process disposal failure");
            }
        }

        private sealed class FakeJob(List<string> events, bool throwOnDispose) : IEvidenceJobLease
        {
            public void Dispose()
            {
                events.Add("job-dispose");
                if (throwOnDispose) throw new InvalidOperationException("fake job disposal failure");
            }
        }

        private sealed class FakeForegroundObserver(
            List<string> events,
            bool sawForegroundWindow,
            bool throwDuringStop,
            bool throwOnDispose) : IEvidenceForegroundObserverLease
        {
            public bool StopAndReadSawJobWindow()
            {
                events.Add("foreground-stop-read");
                if (throwDuringStop)
                {
                    throw new InvalidOperationException("fake foreground stop failure");
                }
                return sawForegroundWindow;
            }

            public void Dispose()
            {
                events.Add("foreground-dispose");
                if (throwOnDispose) throw new InvalidOperationException("fake foreground disposal failure");
            }
        }
    }
}
