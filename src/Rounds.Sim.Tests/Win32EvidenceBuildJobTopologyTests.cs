using System.Collections.ObjectModel;
using System.ComponentModel;
using Rounds.EvidenceLauncher;
using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class Win32EvidenceBuildJobTopologyTests
{
    private const string Root = @"C:\repo";

    [Fact]
    public void ExactPolicyWritesAndReadsBackOnlyThePinned144ByteValueBeforeConfigured()
    {
        var api = new FakeJobApi();
        using var process = Process();
        using var job = new EvidenceBuildJobFactory(api).CreateConfigured(Frozen(), process);

        Assert.Equal(EvidenceBuildJobState.Configured, job.State);
        Assert.Equal(144, api.SetBytes!.Length);
        Assert.Equal(0x2338u, BitConverter.ToUInt32(api.SetBytes, 16));
        Assert.Equal(1u, BitConverter.ToUInt32(api.SetBytes, 40));
        Assert.Equal(0x3ul, BitConverter.ToUInt64(api.SetBytes, 48));
        Assert.Equal(0x4000u, BitConverter.ToUInt32(api.SetBytes, 56));
        Assert.Equal(768ul * 1024 * 1024, BitConverter.ToUInt64(api.SetBytes, 112));
        Assert.Equal(1024ul * 1024 * 1024, BitConverter.ToUInt64(api.SetBytes, 120));
        Assert.All(api.SetBytes.Where((_, index) => index is not (16 or 17 or 40 or 48 or 56 or 57 or 114 or 115 or 116 or 117 or 122 or 123 or 124 or 125)), value => Assert.Equal(0, value));
        Assert.Equal(["create", "set:144", "query-limits:144", "topology"], api.Events.Take(4));
    }

    [Theory]
    [InlineData("affinity")]
    [InlineData("active")]
    [InlineData("process-memory")]
    [InlineData("job-memory")]
    [InlineData("kill")]
    [InlineData("priority")]
    [InlineData("suspended")]
    [InlineData("deadline")]
    public void AlteredPolicyRefusesBeforeCreatingAJob(string mutation)
    {
        var api = new FakeJobApi();
        using var process = Process();
        var request = Mutate(Frozen(), mutation);

        Assert.Throws<InvalidOperationException>(() => new EvidenceBuildJobFactory(api).CreateConfigured(request, process));
        Assert.Empty(api.Events);
    }

    [Fact]
    public void FalseCreateWithValidHandleAdoptsThenTerminatesAndClosesIt()
    {
        var api = new FakeJobApi { CreateSuccess = false, CreateError = 5 };
        using var process = Process();

        var failure = Assert.Throws<Win32Exception>(() => new EvidenceBuildJobFactory(api).CreateConfigured(Frozen(), process));

        Assert.Equal(5, failure.NativeErrorCode);
        Assert.Equal(["create", "terminate:700", "topology", "close:700"], api.Events);
    }

    [Fact]
    public void CreatedJobAliasWithForeignProcessHandleIsDisarmedAndNeverClosedAsAJob()
    {
        var api = new FakeJobApi { CreatedHandle = 200 };
        using var process = Process(out var processApi);

        Assert.Throws<InvalidDataException>(() => new EvidenceBuildJobFactory(api).CreateConfigured(Frozen(), process));
        Assert.DoesNotContain("close:200", api.Events);

        process.Dispose();
        Assert.Equal(new nint[] { 201, 200 }, processApi.Closed);
    }

    [Fact]
    public void LimitQueryBackDriftRefusesAndCleansUp()
    {
        var api = new FakeJobApi { DriftQueryLimits = true };
        using var process = Process();

        Assert.Throws<InvalidDataException>(() => new EvidenceBuildJobFactory(api).CreateConfigured(Frozen(), process));
        Assert.Contains("terminate:700", api.Events);
        Assert.Contains("close:700", api.Events);
    }

    [Fact]
    public void InitialTopologyMustBeExactZeroOverZero()
    {
        foreach (var initial in new[]
        {
            Topology(42, total: 1, active: 1),
            TopologyEmpty(total: 1, terminated: 1),
        })
        {
            var api = new FakeJobApi { InitialTopology = initial };
            using var process = Process();
            Assert.Throws<InvalidDataException>(() => new EvidenceBuildJobFactory(api).CreateConfigured(Frozen(), process));
            Assert.Contains("terminate:700", api.Events);
        }
    }

    [Fact]
    public void AssignmentRefusesNestedJobBeforeAssign()
    {
        var api = new FakeJobApi { AlreadyInJob = true };
        using var process = Process();
        using var job = new EvidenceBuildJobFactory(api).CreateConfigured(Frozen(), process);

        Assert.Throws<InvalidOperationException>(job.AssignSuspended);

        Assert.DoesNotContain("assign:700:200", api.Events);
        Assert.Equal(EvidenceBuildJobState.Faulted, job.State);
    }

    [Theory]
    [InlineData("set")]
    [InlineData("query-limits")]
    [InlineData("membership")]
    [InlineData("assign")]
    public void NativeSeamFailuresRefuseAtTheirExactTransitionAndCleanup(string failurePoint)
    {
        var api = new FakeJobApi { FailurePoint = failurePoint };
        using var process = Process();
        if (failurePoint is "set" or "query-limits")
        {
            Assert.Throws<Win32Exception>(() => new EvidenceBuildJobFactory(api).CreateConfigured(Frozen(), process));
            Assert.DoesNotContain(api.Events, value => value.StartsWith("membership:", StringComparison.Ordinal));
            return;
        }
        using var job = new EvidenceBuildJobFactory(api).CreateConfigured(Frozen(), process);
        Assert.Throws<Win32Exception>(job.AssignSuspended);
        if (failurePoint == "membership") Assert.DoesNotContain(api.Events, value => value.StartsWith("assign:", StringComparison.Ordinal));
        Assert.Equal(EvidenceBuildJobState.Faulted, job.State);
    }

    [Fact]
    public void AssignmentCommitsThenProvesExactlyTheExpectedActivePid()
    {
        var api = new FakeJobApi();
        api.Topologies.Enqueue(Topology(300, total: 1, active: 1));
        using var process = Process();
        using var job = new EvidenceBuildJobFactory(api).CreateConfigured(Frozen(), process);

        job.AssignSuspended();

        Assert.Equal(EvidenceBuildJobState.Assigned, job.State);
        Assert.Equal(["membership:200", "assign:700:200", "topology"], api.Events.Skip(4).Take(3));
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("wrong")]
    [InlineData("extra")]
    [InlineData("trailing")]
    [InlineData("truncated")]
    [InlineData("accounting")]
    [InlineData("returned-short")]
    [InlineData("returned-long")]
    [InlineData("terminated")]
    public void AssignmentRejectsMalformedOrInexactTopologyWithoutRetryingLarger(string anomaly)
    {
        var api = new FakeJobApi();
        api.Topologies.Enqueue(anomaly switch
        {
            "zero" => Topology(0, 1, 1),
            "wrong" => Topology(301, 1, 1),
            "extra" => TopologyRaw([2, 2, 300, 301], 2, 2),
            "trailing" => new EvidenceBuildRawJobTopology(true, 0, new byte[24], 24, 1, 1, 0),
            "truncated" => new EvidenceBuildRawJobTopology(false, EvidenceBuildJobPolicy.ErrorMoreData, [], 0, 1, 1, 0),
            "accounting" => Topology(300, 2, 1),
            "returned-short" => Topology(300, 1, 1) with { ReturnedBytes = 8 },
            "returned-long" => Topology(300, 1, 1) with { ReturnedBytes = 24 },
            "terminated" => Topology(300, 1, 1, terminated: 1),
            _ => throw new InvalidOperationException(),
        });
        using var process = Process();
        using var job = new EvidenceBuildJobFactory(api).CreateConfigured(Frozen(), process);

        Assert.ThrowsAny<Exception>(job.AssignSuspended);

        Assert.Equal(2, api.Events.Count(value => value == "topology"));
        Assert.Equal(EvidenceBuildJobState.Faulted, job.State);
    }

    [Fact]
    public void DrainProofArmsSingleDeadlineImmediatelyBeforeExactResume()
    {
        var api = AssignedApi(out var process, out var job);
        using (process)
        using (job)
        {
            var clock = new FakeClock(api.Events);
            var deadline = Resume(job, clock);

            Assert.Equal(EvidenceBuildJobState.Resumed, job.State);
            Assert.Same(deadline, BorrowDeadline(job));
            Assert.Equal(["clock-origin", "resume:201"], api.Events.TakeLast(2));
            Assert.Equal(1, clock.TimestampReads);
        }
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(2u)]
    [InlineData(uint.MaxValue)]
    public void ResumeIsOneShotAndRequiresPreviousSuspendCountExactlyOne(uint previous)
    {
        var api = AssignedApi(out var process, out var job);
        api.ResumePreviousCount = previous;
        using (process)
        using (job)
        {
            Assert.ThrowsAny<Exception>(() => Resume(job, new FakeClock(api.Events)));
            Assert.Throws<InvalidOperationException>(() => Resume(job, new FakeClock(api.Events)));
            Assert.Equal(1, api.Events.Count(value => value == "resume:201"));
            Assert.Equal(EvidenceBuildJobState.Faulted, job.State);
        }
    }

    [Fact]
    public void EmptyProofRequiresPriorActiveProofAndExactAccountingThenNormalCloseDoesNotTerminate()
    {
        var api = AssignedApi(out var process, out var job);
        using (process)
        {
            Resume(job, new FakeClock(api.Events));
            api.Topologies.Enqueue(TopologyEmpty(total: 1, terminated: 0));
            job.ProveEmptyAfterCompletion();
            job.Dispose();

            Assert.Equal(EvidenceBuildJobState.Terminated, job.State);
            Assert.DoesNotContain(api.Events, value => value.StartsWith("terminate:", StringComparison.Ordinal));
            Assert.Equal("close:700", api.Events[^1]);
        }
    }

    [Fact]
    public void EmptyProofRejectsWrongAccountingOrResidualPid()
    {
        foreach (var topology in new[]
        {
            TopologyEmpty(total: 0),
            Topology(300, 1, 1),
            TopologyEmpty(total: 1, terminated: 1),
        })
        {
            var api = AssignedApi(out var process, out var job);
            using (process)
            using (job)
            {
                Resume(job, new FakeClock(api.Events));
                api.Topologies.Enqueue(topology);
                Assert.Throws<InvalidDataException>(job.ProveEmptyAfterCompletion);
            }
        }
    }

    [Fact]
    public void ActiveBorrowExpiresAndCannotEscapeRawOwnership()
    {
        var api = AssignedApi(out var process, out var job);
        using (process)
        using (job)
        {
            Resume(job, new FakeClock(api.Events));
            EvidenceBuildActiveJobBorrow? escaped = null;
            job.BorrowActive(value =>
            {
                Assert.Equal((nint)700, value.JobHandle);
                Assert.Equal((nint)200, value.ProcessHandle);
                Assert.Equal(300u, value.ProcessId);
                escaped = value;
            });
            Assert.Throws<ObjectDisposedException>(() => _ = escaped!.JobHandle);
        }
    }

    [Fact]
    public void DisposeWaitsForActiveBorrowAndThenClosesWithoutOwningTheProcess()
    {
        var api = AssignedApi(out var process, out var job);
        using (process)
        {
            Resume(job, new FakeClock(api.Events));
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            using var disposerEntered = new ManualResetEventSlim();
            Exception? borrowFailure = null;
            Exception? disposeFailure = null;
            var borrower = new Thread(() =>
            {
                try { job.BorrowActive(_ => { entered.Set(); release.Wait(); }); }
                catch (Exception exception) { borrowFailure = exception; }
            }) { IsBackground = true };
            var disposer = new Thread(() =>
            {
                disposerEntered.Set();
                try { job.Dispose(); }
                catch (Exception exception) { disposeFailure = exception; }
            }) { IsBackground = true };
            borrower.Start();
            try
            {
                Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
                disposer.Start();
                Assert.True(disposerEntered.Wait(TimeSpan.FromSeconds(2)));
                Assert.False(disposer.Join(TimeSpan.FromMilliseconds(50)));
                Assert.DoesNotContain("close:700", api.Events);
            }
            finally
            {
                release.Set();
                borrower.Join(TimeSpan.FromSeconds(2));
                disposer.Join(TimeSpan.FromSeconds(2));
            }
            Assert.Null(borrowFailure);
            Assert.Null(disposeFailure);
            Assert.False(borrower.IsAlive);
            Assert.False(disposer.IsAlive);
            Assert.Equal("close:700", api.Events[^1]);
        }
    }

    [Fact]
    public void JobTransitionsNeverDisarmTheProcessOwnersDirectTerminateFallback()
    {
        var process = Process(out var processApi);
        var api = new FakeJobApi();
        api.Topologies.Enqueue(Topology(300, 1, 1));
        var job = new EvidenceBuildJobFactory(api).CreateConfigured(Frozen(), process);
        job.AssignSuspended();
        Resume(job, new FakeClock(api.Events));
        job.Dispose();

        process.Dispose();

        Assert.Equal(1, processApi.TerminateCalls);
        Assert.Equal(1, processApi.WaitCalls);
    }

    [Fact]
    public void RepeatedOrOutOfOrderTransitionsRefuseWithoutRepeatingEffects()
    {
        var api = new FakeJobApi();
        api.Topologies.Enqueue(Topology(300, 1, 1));
        using var process = Process();
        using var job = new EvidenceBuildJobFactory(api).CreateConfigured(Frozen(), process);

        Assert.Throws<InvalidOperationException>(() => job.ProveEmptyAfterCompletion());
        job.AssignSuspended();
        Assert.Throws<InvalidOperationException>(job.AssignSuspended);
        Assert.Equal(1, api.Events.Count(value => value.StartsWith("assign:", StringComparison.Ordinal)));
    }

    [Fact]
    public void TerminateFailureTransfersWholeJobBeforeCleanupOwnerRunsAndRetryCanFinish()
    {
        var api = new FakeJobApi { TerminateSuccess = false };
        var owner = new FakeCleanupOwner();
        using var process = Process();
        var job = new EvidenceBuildJobFactory(api, owner).CreateConfigured(Frozen(), process);

        Assert.Throws<Win32Exception>(job.Dispose);
        Assert.True(owner.SawStaticRetention);
        Assert.Same(job, Assert.Single(owner.Retained));
        Assert.True(EvidenceBuildJobRetention.Contains(job));
        api.TerminateSuccess = true;
        job.RetryCleanupFromOwner();

        Assert.False(EvidenceBuildJobRetention.Contains(job));
        Assert.Equal(2, api.Events.Count(value => value == "terminate:700"));
        Assert.Equal("close:700", api.Events[^1]);
    }

    [Fact]
    public void CloseFailureRetainsWholeLeaseAndOwnerFailureAggregates()
    {
        var api = new FakeJobApi { CloseSuccess = false };
        var owner = new FakeCleanupOwner { Throw = true };
        using var process = Process();
        var job = new EvidenceBuildJobFactory(api, owner).CreateConfigured(Frozen(), process);

        var failure = Assert.Throws<AggregateException>(job.Dispose).Flatten();

        Assert.Contains(failure.InnerExceptions, value => value is Win32Exception);
        Assert.Contains(failure.InnerExceptions, value => value.Message == "job scheduler failed");
        Assert.True(owner.SawStaticRetention);
        Assert.True(EvidenceBuildJobRetention.Contains(job));
    }

    [Fact]
    public void FalseCloseIsUnambiguouslyOpenAndRetryableAtLeaseLevel()
    {
        var api = new FakeJobApi { CloseSuccess = false };
        var owner = new FakeCleanupOwner();
        using var process = Process();
        var job = new EvidenceBuildJobFactory(api, owner).CreateConfigured(Frozen(), process);

        Assert.Throws<Win32Exception>(job.Dispose);
        api.CloseSuccess = true;
        job.RetryCleanupFromOwner();

        Assert.Equal(2, api.CloseCalls);
        Assert.False(EvidenceBuildJobRetention.Contains(job));
    }

    [Fact]
    public void ThrowAfterCloseIsAmbiguousAndNeverTouchesAReusedNumericHandle()
    {
        var api = new FakeJobApi { ThrowAfterClose = true };
        var owner = new FakeCleanupOwner();
        using var process = Process();
        var job = new EvidenceBuildJobFactory(api, owner).CreateConfigured(Frozen(), process);

        Assert.Throws<InvalidOperationException>(job.RetryCleanupFromOwner);
        Assert.False(EvidenceBuildJobRetention.Contains(job));
        var first = Assert.Throws<IOException>(job.Dispose);
        Assert.True(owner.SawStaticRetention);
        Assert.True(EvidenceBuildJobRetention.Contains(job));
        api.NumericHandleWasReused = true;
        var retry = Assert.Throws<IOException>(job.RetryCleanupFromOwner);
        Assert.Same(first, retry);

        Assert.Equal(1, api.CloseCalls);
        Assert.Equal(0, api.ForeignCloseCalls);
        Assert.Equal(2, api.Events.Count(value => value == "topology"));
    }

    [Fact]
    public void PrematureOwnerRetryRefusesWithoutAnyApiCallOrStateChangeAcrossEveryState()
    {
        AssertPrematureRetry(EvidenceBuildJobState.Created, (api, process) =>
            new EvidenceBuildJobLease(api, new FakeCleanupOwner(), process, 700));
        AssertPrematureRetry(EvidenceBuildJobState.Configured, (api, process) =>
            new EvidenceBuildJobFactory(api).CreateConfigured(Frozen(), process));
        AssertPrematureRetry(EvidenceBuildJobState.Assigned, (api, process) =>
        {
            api.Topologies.Enqueue(Topology(300, 1, 1));
            var job = new EvidenceBuildJobFactory(api).CreateConfigured(Frozen(), process);
            job.AssignSuspended();
            return job;
        });
        AssertPrematureRetry(EvidenceBuildJobState.Resumed, (api, process) =>
        {
            api.Topologies.Enqueue(Topology(300, 1, 1));
            var job = new EvidenceBuildJobFactory(api).CreateConfigured(Frozen(), process);
            job.AssignSuspended();
            Resume(job, new FakeClock(api.Events));
            return job;
        });
        AssertPrematureRetry(EvidenceBuildJobState.EmptyProven, (api, process) =>
        {
            api.Topologies.Enqueue(Topology(300, 1, 1));
            var job = new EvidenceBuildJobFactory(api).CreateConfigured(Frozen(), process);
            job.AssignSuspended();
            Resume(job, new FakeClock(api.Events));
            api.Topologies.Enqueue(TopologyEmpty(total: 1));
            job.ProveEmptyAfterCompletion();
            return job;
        });
    }

    [Fact]
    public void DrainReadinessIsBoundToExactSessionJobAndProcessAndConsumedOnce()
    {
        var apiA = AssignedApi(out var processA, out var jobA);
        var apiB = AssignedApi(out var processB, out var jobB);
        using (processA)
        using (processB)
        using (jobA)
        using (jobB)
        using (var drainsA = ReadyDrains(jobA))
        using (var drainsB = ReadyDrains(jobB))
        {
            Assert.Throws<InvalidOperationException>(() =>
                jobA.ResumeAfterDrainsReady(drainsB.Session, drainsA.Proof, new FakeClock(apiA.Events)));
            Assert.Throws<InvalidOperationException>(() => drainsA.Session.IssueReadyProof(jobA, processA));
            Assert.Throws<InvalidOperationException>(() => drainsA.Session.Release(
                EvidenceBuildRunDeadline.Arm(new FakeClock(apiA.Events), TimeSpan.FromMinutes(5))));
            _ = jobA.ResumeAfterDrainsReady(drainsA.Session, drainsA.Proof, new FakeClock(apiA.Events));
            Assert.Throws<InvalidOperationException>(() =>
                jobB.ResumeAfterDrainsReady(drainsA.Session, drainsA.Proof, new FakeClock(apiB.Events)));
            Assert.Equal(1, apiA.Events.Count(value => value == "resume:201"));
            Assert.Equal(0, apiB.Events.Count(value => value == "resume:201"));
        }
    }

    private static FakeJobApi AssignedApi(out EvidenceBuildMatchedSuspendedProcessLease process, out EvidenceBuildJobLease job)
    {
        var api = new FakeJobApi();
        api.Topologies.Enqueue(Topology(300, 1, 1));
        process = Process();
        job = new EvidenceBuildJobFactory(api).CreateConfigured(Frozen(), process);
        job.AssignSuspended();
        return api;
    }

    private static void AssertPrematureRetry(
        EvidenceBuildJobState expected,
        Func<FakeJobApi, EvidenceBuildMatchedSuspendedProcessLease, EvidenceBuildJobLease> arrange)
    {
        var api = new FakeJobApi();
        using var process = Process();
        using var job = arrange(api, process);
        var before = api.Events.ToArray();

        Assert.Throws<InvalidOperationException>(job.RetryCleanupFromOwner);

        Assert.Equal(expected, job.State);
        Assert.Equal(before, api.Events);
        Assert.False(EvidenceBuildJobRetention.Contains(job));
    }

    private static EvidenceBuildRunDeadline BorrowDeadline(EvidenceBuildJobLease job)
    {
        EvidenceBuildRunDeadline? result = null;
        job.BorrowActive(value => result = value.Deadline);
        return result!;
    }

    private static EvidenceBuildRunDeadline Resume(EvidenceBuildJobLease job, IWin32MonotonicClock clock)
    {
        using var drains = ReadyDrains(job);
        return job.ResumeAfterDrainsReady(drains.Session, drains.Proof, clock);
    }

    private static ReadyDrainFixture ReadyDrains(EvidenceBuildJobLease job) => new(job);

    private static EvidenceBuildRawJobTopology Topology(uint pid, ulong total, uint active, ulong terminated = 0)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(1u).CopyTo(bytes, 0);
        BitConverter.GetBytes(1u).CopyTo(bytes, 4);
        BitConverter.GetBytes((ulong)pid).CopyTo(bytes, 8);
        return new(true, 0, bytes, bytes.Length, total, active, terminated);
    }

    private static EvidenceBuildRawJobTopology TopologyEmpty(ulong total = 0, ulong terminated = 0) =>
        new(true, 0, new byte[8], 8, total, 0, terminated);

    private static EvidenceBuildRawJobTopology TopologyRaw(uint[] values, ulong total, uint active)
    {
        var bytes = new byte[8 + System.Math.Max(0, values.Length - 2) * 8];
        BitConverter.GetBytes(values[0]).CopyTo(bytes, 0);
        BitConverter.GetBytes(values[1]).CopyTo(bytes, 4);
        for (var index = 2; index < values.Length; index++)
            BitConverter.GetBytes((ulong)values[index]).CopyTo(bytes, 8 + (index - 2) * 8);
        return new(true, 0, bytes, bytes.Length, total, active, 0);
    }

    private static EvidenceBuildMatchedSuspendedProcessLease Process() => Process(out _);

    private static EvidenceBuildMatchedSuspendedProcessLease Process(out FakeProcessApi api)
    {
        api = new FakeProcessApi();
        var owner = EvidenceBuildSuspendedProcessOwner.Adopt(
            api,
            new FakeKernelOwner(),
            new FakeProcessCleanupOwner(),
            new EvidenceBuildRawSuspendedProcessResult(true, 200, 201, 300, 301, 0),
            new EvidenceBuildExecutableBorrow(
                100,
                Identity(),
                new EvidenceBuildExecutableContinuityProof(
                    Identity().Path,
                    [new EvidenceBuildExecutableAncestorIdentity(@"C:\repo", "ancestor", true, true, true)],
                    true, true, true),
                [100, 101]),
            new EvidenceBuildPipeCreateBorrow([10, 20, 21, 30, 31], [10, 21, 31]));
        return new EvidenceBuildMatchedSuspendedProcessLease(owner, Identity());
    }

    private static EvidenceOpenedExecutableIdentity Identity() => new(
        @"C:\repo\MSBuild.exe", true, true, false, "volume:1:file:1", new string('a', 64), "1.0", "1.0");

    private static EvidenceFrozenBuildProcessRequest Frozen() =>
        EvidenceBuildProcessPrimitives.Compile(new EvidenceBuildProcessRequest(
            new EvidenceBuildInvocation(
                BaseProjectileEvidenceLaunchPlanner.MsBuildPath,
                Root,
                Arguments(),
                new ReadOnlyDictionary<string, string>(InvocationEnvironment())),
            new ReadOnlyDictionary<string, string>(EffectiveEnvironment()),
            false,
            true,
            new EvidenceBuildJobLimits(0x3, 1, 768L * 1024 * 1024, 1024L * 1024 * 1024, true),
            false, true, true, true, TimeSpan.FromMinutes(5),
            EvidenceBuildProcessPrimitives.StreamCapBytes,
            EvidenceBuildProcessPrimitives.StreamCapBytes));

    private static EvidenceFrozenBuildProcessRequest Mutate(EvidenceFrozenBuildProcessRequest request, string mutation) => mutation switch
    {
        "affinity" => request with { JobLimits = request.JobLimits with { AffinityMask = 1 } },
        "active" => request with { JobLimits = request.JobLimits with { ActiveProcessLimit = 2 } },
        "process-memory" => request with { JobLimits = request.JobLimits with { ProcessCommitBytes = 1 } },
        "job-memory" => request with { JobLimits = request.JobLimits with { JobCommitBytes = 1 } },
        "kill" => request with { JobLimits = request.JobLimits with { KillOnJobClose = false } },
        "priority" => request with { BelowNormalPriority = false },
        "suspended" => request with { StartSuspended = false },
        "deadline" => request with { Deadline = TimeSpan.FromSeconds(299) },
        _ => throw new InvalidOperationException(),
    };

    private static string[] Arguments() =>
    [
        @"game\Rounds.Game.csproj", "/noAutoResponse", "/t:Rebuild", "/p:Configuration=Debug",
        "/p:Restore=false", "/p:UseSharedCompilation=false", "/p:BuildProjectReferences=true",
        "/m:1", "/nr:false", "/v:minimal", "/warnaserror",
    ];

    private static Dictionary<string, string> InvocationEnvironment() => new(StringComparer.Ordinal)
    {
        ["DOTNET_PROCESSOR_COUNT"] = "2",
        ["MSBUILDDISABLENODEREUSE"] = "1",
        ["MSBuildEnableWorkloadResolver"] = "false",
        ["MSBuildSDKsPath"] = @"C:\repo\.tools\dotnet\sdk\8.0.423\Sdks",
    };

    private static Dictionary<string, string> EffectiveEnvironment()
    {
        var values = InvocationEnvironment();
        values["SystemRoot"] = values["WINDIR"] = @"C:\Windows";
        values["TEMP"] = values["TMP"] = @"C:\Temp";
        values["DOTNET_CLI_UI_LANGUAGE"] = "en-US";
        values["VSLANG"] = "1033";
        values["NUGET_PACKAGES"] = @"C:\repo\.tools\nuget-packages";
        values["DOTNET_CLI_HOME"] = @"C:\repo\.tools\dotnet-home";
        values["MSBuildUserExtensionsPath"] = @"C:\repo\.tools\empty\msbuild-user";
        return values;
    }

    private sealed class FakeJobApi : IEvidenceBuildJobApi
    {
        internal List<string> Events { get; } = [];
        internal Queue<EvidenceBuildRawJobTopology> Topologies { get; } = new();
        internal bool CreateSuccess { get; set; } = true;
        internal int CreateError { get; set; }
        internal bool DriftQueryLimits { get; set; }
        internal bool AlreadyInJob { get; set; }
        internal uint ResumePreviousCount { get; set; } = 1;
        internal bool TerminateSuccess { get; set; } = true;
        internal bool CloseSuccess { get; set; } = true;
        internal bool ThrowAfterClose { get; set; }
        internal bool NumericHandleWasReused { get; set; }
        internal int CloseCalls { get; private set; }
        internal int ForeignCloseCalls { get; private set; }
        internal string? FailurePoint { get; init; }
        internal byte[]? SetBytes { get; private set; }
        internal EvidenceBuildRawJobTopology? InitialTopology { get; set; }
        internal nint CreatedHandle { get; init; } = 700;
        private int _topologyCalls;

        public EvidenceBuildRawJobHandle CreateUnnamedJob() { Events.Add("create"); return new(CreateSuccess, CreatedHandle, CreateError); }
        public bool SetExtendedLimits(nint job, byte[] exact144Bytes, out int error)
        { Events.Add($"set:{exact144Bytes.Length}"); SetBytes = (byte[])exact144Bytes.Clone(); error = FailurePoint == "set" ? 5 : 0; return FailurePoint != "set"; }
        public bool QueryExtendedLimits(nint job, byte[] exact144Bytes, out int returnedBytes, out int error)
        {
            Events.Add($"query-limits:{exact144Bytes.Length}");
            SetBytes!.CopyTo(exact144Bytes, 0);
            if (DriftQueryLimits) exact144Bytes[0] = 1;
            returnedBytes = exact144Bytes.Length; error = FailurePoint == "query-limits" ? 5 : 0; return FailurePoint != "query-limits";
        }
        public bool IsProcessInAnyJob(nint process, out bool inJob, out int error)
        { Events.Add($"membership:{process}"); inJob = AlreadyInJob; error = FailurePoint == "membership" ? 5 : 0; return FailurePoint != "membership"; }
        public bool AssignProcess(nint job, nint process, out int error)
        { Events.Add($"assign:{job}:{process}"); error = FailurePoint == "assign" ? 5 : 0; return FailurePoint != "assign"; }
        public uint ResumeThread(nint thread, out int error)
        { Events.Add($"resume:{thread}"); error = ResumePreviousCount == uint.MaxValue ? 5 : 0; return ResumePreviousCount; }
        public EvidenceBuildRawJobTopology QueryPidTopology(nint job)
        {
            Events.Add("topology");
            _topologyCalls++;
            if (_topologyCalls == 1) return InitialTopology ?? TopologyEmpty();
            return Topologies.Count == 0 ? TopologyEmpty() : Topologies.Dequeue();
        }
        public bool TerminateJob(nint job, uint exitCode, out int error)
        { Events.Add($"terminate:{job}"); error = TerminateSuccess ? 0 : 5; return TerminateSuccess; }
        public bool CloseJob(nint job, out int error)
        {
            Events.Add($"close:{job}");
            CloseCalls++;
            if (NumericHandleWasReused) ForeignCloseCalls++;
            if (ThrowAfterClose)
            {
                ThrowAfterClose = false;
                throw new IOException("close threw after side effect");
            }
            error = CloseSuccess ? 0 : 6;
            return CloseSuccess;
        }
    }

    private sealed class ReadyDrainFixture : IDisposable
    {
        internal ReadyDrainFixture(EvidenceBuildJobLease job)
        {
            Session = new EvidenceBuildRawDrainFactory(new EofReadApi()).Prepare(
                EvidenceBuildRawSource.Create("job-test-stdout"),
                EvidenceBuildRawSource.Create("job-test-stderr"),
                EvidenceBuildRawDrainPolicy.Exact);
            try { Proof = job.AcquireDrainReadyProof(Session); }
            catch
            {
                Session.Dispose();
                throw;
            }
        }

        internal EvidenceBuildRawDrainSession Session { get; }
        internal EvidenceBuildDrainReadyProof Proof { get; }
        public void Dispose() => Session.Dispose();
    }

    private sealed class EofReadApi : IEvidenceBuildRawReadApi
    {
        public EvidenceBuildRawRead Poll(EvidenceBuildRawSource source, int maximumBytes) =>
            EvidenceBuildRawRead.EndOfFile();
    }

    private sealed class FakeClock(List<string> events) : IWin32MonotonicClock
    {
        internal int TimestampReads { get; private set; }
        public long GetTimestamp() { TimestampReads++; events.Add("clock-origin"); return 1; }
        public TimeSpan GetElapsedTime(long origin) => TimeSpan.Zero;
        public void Delay(TimeSpan delay) { }
    }

    private sealed class FakeCleanupOwner : IEvidenceBuildJobCleanupOwner
    {
        internal List<EvidenceBuildJobLease> Retained { get; } = [];
        internal bool SawStaticRetention { get; private set; }
        internal bool Throw { get; init; }
        public void Retain(EvidenceBuildJobLease lease, Exception failure)
        {
            SawStaticRetention = EvidenceBuildJobRetention.Contains(lease);
            Retained.Add(lease);
            if (Throw) throw new IOException("job scheduler failed");
        }
    }

    private sealed class FakeProcessApi : IEvidenceBuildSuspendedProcessApi
    {
        internal int TerminateCalls { get; private set; }
        internal int WaitCalls { get; private set; }
        internal List<nint> Closed { get; } = [];
        public bool TerminateProcess(nint process, uint exitCode, out int error) { TerminateCalls++; error = 0; return true; }
        public uint WaitForSingleObject(nint process, uint milliseconds, out int error) { WaitCalls++; error = 0; return 0; }
        public bool CloseHandle(nint handle, out int error) { Closed.Add(handle); error = 0; return true; }
    }
    private sealed class FakeKernelOwner : IEvidenceBuildKernelHandleCleanupOwner
    { public void Retain(EvidenceBuildAmbiguousKernelHandle handle, Exception failure) { } }
    private sealed class FakeProcessCleanupOwner : IEvidenceBuildSuspendedProcessCleanupOwner
    { public void Retain(EvidenceBuildSuspendedProcessOwner owner, Exception failure) { } }
}
