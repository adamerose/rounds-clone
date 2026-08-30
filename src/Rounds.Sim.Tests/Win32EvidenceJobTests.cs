using System.ComponentModel;
using System.Runtime.InteropServices;
using Rounds.EvidenceLauncher;
using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class Win32EvidenceJobTests
{
    private const uint RequiredLimitFlags =
        Win32EvidenceConstants.JobObjectLimitKillOnJobClose |
        Win32EvidenceConstants.JobObjectLimitActiveProcess |
        Win32EvidenceConstants.JobObjectLimitAffinity |
        Win32EvidenceConstants.JobObjectLimitPriorityClass |
        Win32EvidenceConstants.JobObjectLimitProcessMemory |
        Win32EvidenceConstants.JobObjectLimitJobMemory;

    [Fact]
    public void Create_configures_unnamed_job_with_exact_extended_limits_and_struct_size()
    {
        var events = new List<string>();
        var api = new FakeJobApi(events);
        var controller = new Win32JobObjectController(api, new FakeClock(events));

        var job = controller.CreateConfigured(Limits());

        Assert.True(job.Configured);
        Assert.Equal(48, Marshal.SizeOf<Win32JobBasicAccountingInformation>());
        Assert.Equal(144, Marshal.SizeOf<Win32JobExtendedLimitInformation>());
        Assert.Equal(144U, api.ObservedExtendedSize);
        Assert.Equal(RequiredLimitFlags, api.ObservedLimits.BasicLimitInformation.LimitFlags);
        Assert.Equal(1U, api.ObservedLimits.BasicLimitInformation.ActiveProcessLimit);
        Assert.Equal((nuint)3, api.ObservedLimits.BasicLimitInformation.Affinity);
        Assert.Equal(
            Win32EvidenceConstants.BelowNormalPriorityClass,
            api.ObservedLimits.BasicLimitInformation.PriorityClass);
        Assert.Equal((nuint)(768L * 1024 * 1024), api.ObservedLimits.ProcessMemoryLimit);
        Assert.Equal((nuint)(1024L * 1024 * 1024), api.ObservedLimits.JobMemoryLimit);
        Assert.Equal((nuint)0, api.ObservedLimits.BasicLimitInformation.MinimumWorkingSetSize);
        Assert.Equal((nuint)0, api.ObservedLimits.BasicLimitInformation.MaximumWorkingSetSize);
        Assert.Equal(new[] { "job-create:unnamed", "job-set-limits:701:144" }, events);
        Assert.DoesNotContain(events, value => value.Contains("ui", StringComparison.OrdinalIgnoreCase));

        job.Dispose();
        Assert.Equal("close:701", events[^1]);
    }

    [Theory]
    [InlineData("affinity-zero")]
    [InlineData("affinity-one")]
    [InlineData("affinity-seven")]
    [InlineData("active-zero")]
    [InlineData("active-two")]
    [InlineData("process-memory-zero")]
    [InlineData("process-memory-smaller")]
    [InlineData("process-memory-larger")]
    [InlineData("job-memory-smaller")]
    [InlineData("job-memory-larger")]
    [InlineData("priority")]
    [InlineData("kill")]
    public void Invalid_or_weakened_limits_refuse_before_creating_job(string field)
    {
        var limits = Limits();
        limits = field switch
        {
            "affinity-zero" => limits with { AffinityMask = 0 },
            "affinity-one" => limits with { AffinityMask = 0x1 },
            "affinity-seven" => limits with { AffinityMask = 0x7 },
            "active-zero" => limits with { ActiveProcessLimit = 0 },
            "active-two" => limits with { ActiveProcessLimit = 2 },
            "process-memory-zero" => limits with { ProcessCommitBytes = 0 },
            "process-memory-smaller" => limits with { ProcessCommitBytes = limits.ProcessCommitBytes - 1 },
            "process-memory-larger" => limits with { ProcessCommitBytes = limits.ProcessCommitBytes + 1 },
            "job-memory-smaller" => limits with { JobCommitBytes = limits.JobCommitBytes - 1 },
            "job-memory-larger" => limits with { JobCommitBytes = limits.JobCommitBytes + 1 },
            "priority" => limits with { BelowNormalPriority = false },
            "kill" => limits with { KillOnJobClose = false },
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };
        var events = new List<string>();

        Assert.Throws<InvalidOperationException>(() =>
            new Win32JobObjectController(new FakeJobApi(events), new FakeClock(events))
                .CreateConfigured(limits));

        Assert.Empty(events);
    }

    [Theory]
    [InlineData("create-zero")]
    [InlineData("create-invalid")]
    [InlineData("configure")]
    public void Create_or_configuration_failure_closes_only_an_acquired_job(string failure)
    {
        var events = new List<string>();
        var api = new FakeJobApi(events) { Failure = failure };

        Assert.ThrowsAny<Exception>(() =>
            new Win32JobObjectController(api, new FakeClock(events)).CreateConfigured(Limits()));

        if (failure == "configure")
        {
            Assert.Equal(new[]
            {
                "job-create:unnamed", "job-set-limits:701:144", "close:701",
            }, events);
        }
        else
        {
            Assert.Equal(new[] { "job-create:unnamed" }, events);
        }
    }

    [Fact]
    public void Assignment_checks_parent_job_and_marks_ownership_only_after_suspended_assign_success()
    {
        var events = new List<string>();
        var api = new FakeJobApi(events);
        var controller = new Win32JobObjectController(api, new FakeClock(events));
        var job = controller.CreateConfigured(Limits());
        var process = Process(api);

        controller.AssignSuspended(job, process);

        Assert.True(job.Assigned);
        Assert.True(process.AssignedToKillOnCloseJob);
        Assert.False(process.PrimaryThreadWasResumed);
        Assert.Equal(new[]
        {
            "job-create:unnamed", "job-set-limits:701:144",
            "process-in-job:801", "job-assign:701:801",
        }, events);

        job.Dispose();
        process.Dispose();
        Assert.Contains("process-terminate:801:1", events);
    }

    [Theory]
    [InlineData("membership-query")]
    [InlineData("already-in-job")]
    [InlineData("assign")]
    public void Parent_job_or_assignment_incompatibility_fails_closed_with_process_fallback_armed(
        string failure)
    {
        var events = new List<string>();
        var api = new FakeJobApi(events) { Failure = failure };
        var controller = new Win32JobObjectController(api, new FakeClock(events));
        var job = controller.CreateConfigured(Limits());
        var process = Process(api);

        Assert.ThrowsAny<Exception>(() => controller.AssignSuspended(job, process));

        Assert.False(job.Assigned);
        Assert.False(process.AssignedToKillOnCloseJob);
        Assert.False(process.PrimaryThreadWasResumed);
        Assert.DoesNotContain(events, value => value.StartsWith("resume:", StringComparison.Ordinal));
        process.Dispose();
        job.Dispose();
        Assert.Contains("process-terminate:801:1", events);
    }

    [Fact]
    public void Resume_starts_monotonic_deadline_before_exact_one_count_transition()
    {
        var events = new List<string>();
        var api = new FakeJobApi(events);
        var clock = new FakeClock(events);
        var controller = new Win32JobObjectController(api, clock);
        var job = controller.CreateConfigured(Limits());
        var process = Process(api);
        controller.AssignSuspended(job, process);

        var deadline = controller.ResumeAndStartDeadline(job, process, TimeSpan.FromSeconds(30));

        Assert.Equal(100, deadline.StartingTimestamp);
        Assert.Equal(TimeSpan.FromSeconds(30), deadline.Timeout);
        Assert.True(process.PrimaryThreadWasResumed);
        Assert.True(events.IndexOf("clock-now") < events.IndexOf("resume:802"));

        job.Dispose();
        process.Dispose();
    }

    [Theory]
    [InlineData(0U)]
    [InlineData(2U)]
    [InlineData(uint.MaxValue)]
    public void Resume_refuses_failure_already_running_or_multiply_suspended_state(uint previousCount)
    {
        var events = new List<string>();
        var api = new FakeJobApi(events) { ResumeResult = previousCount };
        var controller = new Win32JobObjectController(api, new FakeClock(events));
        var job = controller.CreateConfigured(Limits());
        var process = Process(api);
        controller.AssignSuspended(job, process);

        Assert.ThrowsAny<Exception>(() =>
            controller.ResumeAndStartDeadline(job, process, TimeSpan.FromSeconds(30)));

        Assert.False(process.PrimaryThreadWasResumed);
        process.Dispose();
        job.Dispose();
        Assert.Contains("process-terminate:801:1", events);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(31)]
    public void Resume_refuses_every_non_admitted_positive_deadline_before_clock_or_thread(int seconds)
    {
        var events = new List<string>();
        var api = new FakeJobApi(events);
        var controller = new Win32JobObjectController(api, new FakeClock(events));
        var job = controller.CreateConfigured(Limits());
        var process = Process(api);
        controller.AssignSuspended(job, process);

        Assert.Throws<InvalidOperationException>(() =>
            controller.ResumeAndStartDeadline(job, process, TimeSpan.FromSeconds(seconds)));

        Assert.DoesNotContain("clock-now", events);
        Assert.DoesNotContain("resume:802", events);
        process.Dispose();
        job.Dispose();
    }

    [Fact]
    public void Empty_proof_polls_exact_accounting_until_zero_then_allows_fallback_disarm()
    {
        var events = new List<string>();
        var api = new FakeJobApi(events);
        api.Accounting.Enqueue(Accounting(total: 1, active: 1, terminated: 0));
        api.Accounting.Enqueue(Accounting(total: 1, active: 0, terminated: 1));
        var clock = new FakeClock(events);
        var controller = new Win32JobObjectController(api, clock);
        var job = controller.CreateConfigured(Limits());
        var process = Process(api);
        controller.AssignSuspended(job, process);
        var deadline = controller.ResumeAndStartDeadline(job, process, TimeSpan.FromSeconds(30));

        Assert.True(controller.WaitForEmpty(job, deadline));

        Assert.True(job.EmptyProven);
        Assert.Equal(2, events.Count(value => value == "job-query:701:48"));
        Assert.Contains("clock-delay:10", events);
        process.DisarmTerminationFallbackAfterVerifiedExitAndEmptyJob();
        job.Dispose();
        process.Dispose();
        Assert.DoesNotContain(events, value => value.StartsWith("process-terminate:", StringComparison.Ordinal));
        Assert.Equal(1, events.Count(value => value == "close:701"));
        Assert.Equal(1, events.Count(value => value == "close:801"));
        Assert.Equal(1, events.Count(value => value == "close:802"));
    }

    [Fact]
    public void Active_job_timeout_uses_only_bounded_monotonic_poll_delays()
    {
        var events = new List<string>();
        var api = new FakeJobApi(events);
        var clock = new FakeClock(events);
        var controller = new Win32JobObjectController(api, clock);
        var job = controller.CreateConfigured(Limits());
        var process = Process(api);
        controller.AssignSuspended(job, process);
        var deadline = controller.ResumeAndStartDeadline(job, process, TimeSpan.FromSeconds(30));
        clock.Advance(TimeSpan.FromSeconds(30) - TimeSpan.FromMilliseconds(25));

        Assert.False(controller.WaitForEmpty(job, deadline));

        Assert.False(job.EmptyProven);
        Assert.Equal(new double[] { 10, 10, 5 }, clock.Delays.Select(value => value.TotalMilliseconds));
        Assert.Equal(4, events.Count(value => value == "job-query:701:48"));
        process.Dispose();
        job.Dispose();
    }

    [Theory]
    [InlineData("query")]
    [InlineData("query-size")]
    [InlineData("query-counters")]
    public void Accounting_query_failure_size_drift_or_impossible_counters_refuses_proof(string failure)
    {
        var events = new List<string>();
        var api = new FakeJobApi(events) { Failure = failure };
        var controller = new Win32JobObjectController(api, new FakeClock(events));
        var job = controller.CreateConfigured(Limits());
        var process = Process(api);
        controller.AssignSuspended(job, process);
        var deadline = controller.ResumeAndStartDeadline(job, process, TimeSpan.FromSeconds(30));

        Assert.ThrowsAny<Exception>(() => controller.WaitForEmpty(job, deadline));

        Assert.False(job.EmptyProven);
        process.Dispose();
        job.Dispose();
    }

    [Fact]
    public void Terminate_and_job_close_failures_leave_explicit_process_termination_authority_armed()
    {
        var events = new List<string>();
        var api = new FakeJobApi(events) { Failure = "terminate-and-close" };
        var controller = new Win32JobObjectController(api, new FakeClock(events));
        var job = controller.CreateConfigured(Limits());
        var process = Process(api);
        controller.AssignSuspended(job, process);
        _ = controller.ResumeAndStartDeadline(job, process, TimeSpan.FromSeconds(30));

        Assert.Throws<Win32Exception>(() => controller.Terminate(job));
        Assert.Throws<Win32Exception>(job.Dispose);
        process.Dispose();

        Assert.Contains("job-terminate:701:1", events);
        Assert.Contains("close:701", events);
        Assert.Contains("process-terminate:801:1", events);
        Assert.Contains("wait:801:5000", events);
        Assert.True(events.IndexOf("close:701") < events.IndexOf("process-terminate:801:1"));
        Assert.Equal(1, events.Count(value => value == "close:802"));
        Assert.Equal(1, events.Count(value => value == "close:801"));
    }

    private static BaseProjectileEvidenceJobLimits Limits() => new(
        AffinityMask: 3,
        ActiveProcessLimit: 1,
        ProcessCommitBytes: 768L * 1024 * 1024,
        JobCommitBytes: 1024L * 1024 * 1024,
        BelowNormalPriority: true,
        KillOnJobClose: true);

    private static Win32ProcessLease Process(FakeJobApi api) => new(api, 801, 802);

    private static Win32JobBasicAccountingInformation Accounting(
        uint total,
        uint active,
        uint terminated) => new()
        {
            TotalProcesses = total,
            ActiveProcesses = active,
            TotalTerminatedProcesses = terminated,
        };

    private sealed class FakeClock(List<string> events) : IWin32MonotonicClock
    {
        private TimeSpan _elapsed;

        internal List<TimeSpan> Delays { get; } = new();

        internal void Advance(TimeSpan duration) => _elapsed += duration;

        public long GetTimestamp()
        {
            events.Add("clock-now");
            return 100;
        }

        public TimeSpan GetElapsedTime(long startingTimestamp)
        {
            Assert.Equal(100, startingTimestamp);
            events.Add($"clock-elapsed:{_elapsed.TotalMilliseconds}");
            return _elapsed;
        }

        public void Delay(TimeSpan duration)
        {
            Assert.True(duration > TimeSpan.Zero);
            Delays.Add(duration);
            events.Add($"clock-delay:{duration.TotalMilliseconds}");
            _elapsed += duration;
        }
    }

    private sealed class FakeJobApi(List<string> events) : IWin32JobApi
    {
        internal string? Failure { get; init; }
        internal uint ResumeResult { get; init; } = 1;
        internal Win32JobExtendedLimitInformation ObservedLimits { get; private set; }
        internal uint ObservedExtendedSize { get; private set; }
        internal Queue<Win32JobBasicAccountingInformation> Accounting { get; } = new();

        public nint CreateUnnamedJobObject()
        {
            events.Add("job-create:unnamed");
            return Failure switch
            {
                "create-zero" => 0,
                "create-invalid" => -1,
                _ => 701,
            };
        }

        public bool SetExtendedLimits(
            nint job,
            in Win32JobExtendedLimitInformation information,
            uint informationLength)
        {
            events.Add($"job-set-limits:{job}:{informationLength}");
            ObservedLimits = information;
            ObservedExtendedSize = informationLength;
            return Failure != "configure";
        }

        public bool IsProcessInAnyJob(nint process, out bool inJob)
        {
            events.Add($"process-in-job:{process}");
            inJob = Failure == "already-in-job";
            return Failure != "membership-query";
        }

        public bool AssignProcessToJob(nint job, nint process)
        {
            events.Add($"job-assign:{job}:{process}");
            return Failure != "assign";
        }

        public uint ResumeThread(nint thread)
        {
            events.Add($"resume:{thread}");
            return ResumeResult;
        }

        public bool QueryBasicAccounting(
            nint job,
            out Win32JobBasicAccountingInformation information,
            uint informationLength,
            out uint returnLength)
        {
            events.Add($"job-query:{job}:{informationLength}");
            information = Failure == "query-counters"
                ? Win32EvidenceJobTests.Accounting(total: 1, active: 2, terminated: 0)
                : Accounting.Count > 0
                    ? Accounting.Dequeue()
                    : Win32EvidenceJobTests.Accounting(total: 1, active: 1, terminated: 0);
            returnLength = Failure == "query-size" ? informationLength - 1 : informationLength;
            return Failure != "query";
        }

        public bool TerminateJob(nint job, uint exitCode)
        {
            events.Add($"job-terminate:{job}:{exitCode}");
            return Failure != "terminate-and-close";
        }

        public bool CloseKernelHandle(nint handle)
        {
            events.Add($"close:{handle}");
            return Failure != "terminate-and-close" || handle != 701;
        }

        public bool CloseDesktop(nint desktop) => true;

        public bool TerminateProcess(nint process, uint exitCode)
        {
            events.Add($"process-terminate:{process}:{exitCode}");
            return true;
        }

        public uint WaitForSingleObject(nint handle, uint milliseconds)
        {
            events.Add($"wait:{handle}:{milliseconds}");
            return Win32EvidenceConstants.WaitObject0;
        }

        public bool WriteFile(nint handle, ReadOnlySpan<byte> data, out uint written)
        {
            written = checked((uint)data.Length);
            return true;
        }
    }
}
