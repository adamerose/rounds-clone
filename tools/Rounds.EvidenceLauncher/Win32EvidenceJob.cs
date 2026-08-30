using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Rounds.Game;

namespace Rounds.EvidenceLauncher;

internal interface IWin32JobApi : IWin32EvidenceApi
{
    nint CreateUnnamedJobObject();

    bool SetExtendedLimits(
        nint job,
        in Win32JobExtendedLimitInformation information,
        uint informationLength);

    bool IsProcessInAnyJob(nint process, out bool inJob);

    bool AssignProcessToJob(nint job, nint process);

    uint ResumeThread(nint thread);

    bool QueryBasicAccounting(
        nint job,
        out Win32JobBasicAccountingInformation information,
        uint informationLength,
        out uint returnLength);

    bool TerminateJob(nint job, uint exitCode);
}

internal interface IWin32MonotonicClock
{
    long GetTimestamp();

    TimeSpan GetElapsedTime(long startingTimestamp);

    void Delay(TimeSpan duration);
}

internal readonly record struct Win32JobDeadline(long StartingTimestamp, TimeSpan Timeout);

internal sealed class Win32JobObjectController(
    IWin32JobApi api,
    IWin32MonotonicClock clock)
{
    private const uint AdmittedAffinityMask = 0x3;
    private const int AdmittedActiveProcessLimit = 1;
    private const long AdmittedProcessCommitBytes = 768L * 1024 * 1024;
    private const long AdmittedJobCommitBytes = 1024L * 1024 * 1024;
    private static readonly TimeSpan AdmittedDeadline = TimeSpan.FromSeconds(30);
    private const uint RequiredLimitFlags =
        Win32EvidenceConstants.JobObjectLimitKillOnJobClose |
        Win32EvidenceConstants.JobObjectLimitActiveProcess |
        Win32EvidenceConstants.JobObjectLimitAffinity |
        Win32EvidenceConstants.JobObjectLimitPriorityClass |
        Win32EvidenceConstants.JobObjectLimitProcessMemory |
        Win32EvidenceConstants.JobObjectLimitJobMemory;

    private static readonly uint ExtendedLimitSize =
        checked((uint)Marshal.SizeOf<Win32JobExtendedLimitInformation>());
    private static readonly uint BasicAccountingSize =
        checked((uint)Marshal.SizeOf<Win32JobBasicAccountingInformation>());
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    internal Win32JobLease CreateConfigured(BaseProjectileEvidenceJobLimits limits)
    {
        var information = BuildExactLimits(limits);
        var handle = api.CreateUnnamedJobObject();
        if (handle is 0 or -1)
        {
            throw new Win32Exception("CreateJobObjectW failed for the unnamed evidence job.");
        }

        var lease = new Win32JobLease(api, handle);
        Exception? failure = null;
        try
        {
            if (!api.SetExtendedLimits(
                    lease.DangerousHandle,
                    in information,
                    ExtendedLimitSize))
            {
                throw new Win32Exception("SetInformationJobObject failed for exact evidence limits.");
            }
            lease.MarkConfigured();
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        if (failure is not null)
        {
            TryCleanup(lease.Dispose, ref failure);
            ExceptionDispatchInfo.Capture(failure!).Throw();
        }
        return lease;
    }

    internal void AssignSuspended(Win32JobLease job, Win32ProcessLease process)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(process);
        if (!job.Configured || job.Assigned || process.PrimaryThreadWasResumed ||
            process.AssignedToKillOnCloseJob)
        {
            throw new InvalidOperationException("Only one still-suspended process can be assigned to the configured job.");
        }

        if (!api.IsProcessInAnyJob(process.DangerousProcessHandle, out var alreadyInJob))
        {
            throw new Win32Exception("IsProcessInJob could not establish nested-job compatibility.");
        }
        if (alreadyInJob)
        {
            throw new InvalidOperationException(
                "The suspended child already belongs to a parent job; nested assignment is refused.");
        }
        if (!api.AssignProcessToJob(job.DangerousHandle, process.DangerousProcessHandle))
        {
            throw new Win32Exception("AssignProcessToJobObject failed for the suspended child.");
        }

        job.MarkAssigned();
        process.MarkAssignedToKillOnCloseJob();
    }

    internal Win32JobDeadline ResumeAndStartDeadline(
        Win32JobLease job,
        Win32ProcessLease process,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(process);
        if (timeout != AdmittedDeadline || !job.Assigned || !process.AssignedToKillOnCloseJob ||
            process.PrimaryThreadWasResumed)
        {
            throw new InvalidOperationException(
                "Resume requires one assigned suspended process and the exact admitted 30-second deadline.");
        }

        var startingTimestamp = clock.GetTimestamp();
        var previousSuspendCount = api.ResumeThread(process.DangerousPrimaryThreadHandle);
        if (previousSuspendCount == Win32EvidenceConstants.ResumeThreadFailure)
        {
            throw new Win32Exception("ResumeThread failed for the evidence child.");
        }
        if (previousSuspendCount != 1)
        {
            throw new InvalidOperationException(
                $"ResumeThread returned unexpected previous suspend count {previousSuspendCount}; expected 1.");
        }
        process.MarkPrimaryThreadResumed();
        job.MarkResumed();
        return new Win32JobDeadline(startingTimestamp, timeout);
    }

    internal bool WaitForEmpty(Win32JobLease job, Win32JobDeadline deadline)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (!job.Assigned || !job.Resumed || job.EmptyProven || deadline.Timeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Empty-job proof state or deadline was invalid.");
        }

        while (true)
        {
            if (!api.QueryBasicAccounting(
                    job.DangerousHandle,
                    out var accounting,
                    BasicAccountingSize,
                    out var returned) || returned != BasicAccountingSize)
            {
                throw new Win32Exception("QueryInformationJobObject failed for exact basic accounting data.");
            }
            if (accounting.ActiveProcesses > accounting.TotalProcesses ||
                accounting.TotalTerminatedProcesses > accounting.TotalProcesses)
            {
                throw new InvalidOperationException("Job accounting counters were internally inconsistent.");
            }
            if (accounting.ActiveProcesses == 0)
            {
                job.MarkEmptyProven();
                return true;
            }

            var elapsed = clock.GetElapsedTime(deadline.StartingTimestamp);
            if (elapsed >= deadline.Timeout)
            {
                return false;
            }
            var remaining = deadline.Timeout - elapsed;
            clock.Delay(remaining < PollInterval ? remaining : PollInterval);
        }
    }

    internal void Terminate(Win32JobLease job, uint exitCode = 1)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (!api.TerminateJob(job.DangerousHandle, exitCode))
        {
            throw new Win32Exception("TerminateJobObject failed; process termination fallback remains armed.");
        }
    }

    private static Win32JobExtendedLimitInformation BuildExactLimits(
        BaseProjectileEvidenceJobLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (limits.AffinityMask != AdmittedAffinityMask ||
            limits.ActiveProcessLimit != AdmittedActiveProcessLimit ||
            limits.ProcessCommitBytes != AdmittedProcessCommitBytes ||
            limits.JobCommitBytes != AdmittedJobCommitBytes ||
            !limits.BelowNormalPriority || !limits.KillOnJobClose)
        {
            throw new InvalidOperationException("Evidence job limits did not match the admitted bounded contract.");
        }

        return new Win32JobExtendedLimitInformation
        {
            BasicLimitInformation = new Win32JobBasicLimitInformation
            {
                LimitFlags = RequiredLimitFlags,
                ActiveProcessLimit = checked((uint)limits.ActiveProcessLimit),
                Affinity = limits.AffinityMask,
                PriorityClass = Win32EvidenceConstants.BelowNormalPriorityClass,
            },
            ProcessMemoryLimit = checked((nuint)limits.ProcessCommitBytes),
            JobMemoryLimit = checked((nuint)limits.JobCommitBytes),
        };
    }

    private static void TryCleanup(Action cleanup, ref Exception? failure)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            failure = failure is null ? exception : new AggregateException(failure, exception);
        }
    }
}

internal sealed class Win32MonotonicClock : IWin32MonotonicClock
{
    public long GetTimestamp() => Stopwatch.GetTimestamp();

    public TimeSpan GetElapsedTime(long startingTimestamp) =>
        Stopwatch.GetElapsedTime(startingTimestamp);

    public void Delay(TimeSpan duration) => Thread.Sleep(duration);
}

internal sealed class Win32JobApi : IWin32JobApi
{
    public nint CreateUnnamedJobObject() => Win32JobNativeMethods.CreateJobObjectW(0, null);

    public bool SetExtendedLimits(
        nint job,
        in Win32JobExtendedLimitInformation information,
        uint informationLength)
    {
        var mutable = information;
        return Win32JobNativeMethods.SetInformationJobObject(
            job,
            Win32EvidenceConstants.JobObjectExtendedLimitInformation,
            ref mutable,
            informationLength);
    }

    public bool IsProcessInAnyJob(nint process, out bool inJob) =>
        Win32JobNativeMethods.IsProcessInJob(process, 0, out inJob);

    public bool AssignProcessToJob(nint job, nint process) =>
        Win32JobNativeMethods.AssignProcessToJobObject(job, process);

    public uint ResumeThread(nint thread) => Win32JobNativeMethods.ResumeThread(thread);

    public bool QueryBasicAccounting(
        nint job,
        out Win32JobBasicAccountingInformation information,
        uint informationLength,
        out uint returnLength) =>
        Win32JobNativeMethods.QueryInformationJobObject(
            job,
            Win32EvidenceConstants.JobObjectBasicAccountingInformation,
            out information,
            informationLength,
            out returnLength);

    public bool TerminateJob(nint job, uint exitCode) =>
        Win32JobNativeMethods.TerminateJobObject(job, exitCode);

    public bool CloseKernelHandle(nint handle) => Win32EvidenceNativeMethods.CloseHandle(handle);

    public bool CloseDesktop(nint desktop) => Win32EvidenceNativeMethods.CloseDesktop(desktop);

    public bool TerminateProcess(nint process, uint exitCode) =>
        Win32EvidenceNativeMethods.TerminateProcess(process, exitCode);

    public uint WaitForSingleObject(nint handle, uint milliseconds) =>
        Win32EvidenceNativeMethods.WaitForSingleObject(handle, milliseconds);

    public bool WriteFile(nint handle, ReadOnlySpan<byte> data, out uint written)
    {
        var bytes = data.ToArray();
        return Win32EvidenceNativeMethods.WriteFile(
            handle,
            bytes,
            checked((uint)bytes.Length),
            out written,
            0);
    }
}

internal static class Win32JobNativeMethods
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CreateJobObjectW(nint jobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetInformationJobObject(
        nint job,
        uint informationClass,
        ref Win32JobExtendedLimitInformation information,
        uint informationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsProcessInJob(
        nint process,
        nint job,
        [MarshalAs(UnmanagedType.Bool)] out bool result);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AssignProcessToJobObject(nint job, nint process);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint ResumeThread(nint thread);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryInformationJobObject(
        nint job,
        uint informationClass,
        out Win32JobBasicAccountingInformation information,
        uint informationLength,
        out uint returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TerminateJobObject(nint job, uint exitCode);
}
