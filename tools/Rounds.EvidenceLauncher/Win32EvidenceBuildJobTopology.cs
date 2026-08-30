using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.ExceptionServices;

namespace Rounds.EvidenceLauncher;

internal enum EvidenceBuildJobState
{
    Created,
    Configured,
    Assigned,
    Resumed,
    EmptyProven,
    Faulted,
    Terminated,
}

internal static class EvidenceBuildJobPolicy
{
    internal const uint LimitFlags = 0x2338;
    internal const uint BelowNormalPriorityClass = 0x4000;
    internal const uint AffinityMask = 0x3;
    internal const uint ActiveProcessLimit = 1;
    internal const ulong ProcessMemoryLimit = 768UL * 1024 * 1024;
    internal const ulong JobMemoryLimit = 1024UL * 1024 * 1024;
    internal const int ExtendedLimitBytes = 144;
    internal const int EmptyPidListBytes = 8;
    internal const int OnePidListBytes = 16;
    internal const int ErrorMoreData = 234;
    internal const uint ResumeFailed = 0xffffffff;
    internal const uint CleanupExitCode = 0xe0350002;

    internal static byte[] CompileExtendedLimits(EvidenceFrozenBuildProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var startSuspended = request.StartSuspended;
        var belowNormal = request.BelowNormalPriority;
        var deadline = request.Deadline;
        var limits = request.JobLimits ?? throw new InvalidOperationException("Build job limits were absent.");
        var affinity = limits.AffinityMask;
        var active = limits.ActiveProcessLimit;
        var processMemory = limits.ProcessCommitBytes;
        var jobMemory = limits.JobCommitBytes;
        var killOnClose = limits.KillOnJobClose;
        if (!startSuspended || !belowNormal || deadline != EvidenceBuildRunDeadline.ExactTimeout ||
            affinity != AffinityMask || active != ActiveProcessLimit ||
            processMemory != (long)ProcessMemoryLimit || jobMemory != (long)JobMemoryLimit || !killOnClose)
        {
            throw new InvalidOperationException("Build job policy did not match the exact admitted limits.");
        }

        var bytes = new byte[ExtendedLimitBytes];
        WriteUInt32(bytes, 16, LimitFlags);
        WriteUInt32(bytes, 40, ActiveProcessLimit);
        WriteUInt64(bytes, 48, AffinityMask);
        WriteUInt32(bytes, 56, BelowNormalPriorityClass);
        WriteUInt64(bytes, 112, ProcessMemoryLimit);
        WriteUInt64(bytes, 120, JobMemoryLimit);
        return bytes;
    }

    private static void WriteUInt32(byte[] bytes, int offset, uint value) =>
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)), value);

    private static void WriteUInt64(byte[] bytes, int offset, ulong value) =>
        BinaryPrimitives.WriteUInt64LittleEndian(bytes.AsSpan(offset, sizeof(ulong)), value);
}

internal sealed record EvidenceBuildRawJobHandle(bool Succeeded, nint Handle, int Error);

internal sealed record EvidenceBuildRawJobTopology(
    bool Succeeded,
    int Error,
    byte[] Bytes,
    int ReturnedBytes,
    ulong TotalProcesses,
    uint ActiveProcesses,
    ulong TotalTerminatedProcesses);

internal interface IEvidenceBuildJobApi
{
    EvidenceBuildRawJobHandle CreateUnnamedJob();
    bool SetExtendedLimits(nint job, byte[] exact144Bytes, out int error);
    bool QueryExtendedLimits(nint job, byte[] exact144Bytes, out int returnedBytes, out int error);
    bool IsProcessInAnyJob(nint process, out bool inJob, out int error);
    bool AssignProcess(nint job, nint process, out int error);
    uint ResumeThread(nint thread, out int error);
    EvidenceBuildRawJobTopology QueryPidTopology(nint job);
    bool TerminateJob(nint job, uint exitCode, out int error);
    bool CloseJob(nint job, out int error);
}

internal interface IEvidenceBuildJobCleanupOwner
{
    void Retain(EvidenceBuildJobLease lease, Exception failure);
}

internal sealed class EvidenceBuildJobCleanupOwner : IEvidenceBuildJobCleanupOwner
{
    internal static EvidenceBuildJobCleanupOwner Instance { get; } = new();
    private EvidenceBuildJobCleanupOwner() { }
    public void Retain(EvidenceBuildJobLease lease, Exception failure) { }
}

internal static class EvidenceBuildJobRetention
{
    private static readonly ConcurrentDictionary<EvidenceBuildJobLease, Exception> Retained = new();
    internal static void Retain(EvidenceBuildJobLease lease, Exception failure) => Retained.TryAdd(lease, failure);
    internal static void Release(EvidenceBuildJobLease lease) => Retained.TryRemove(lease, out _);
    internal static bool Contains(EvidenceBuildJobLease lease) => Retained.ContainsKey(lease);
}

internal sealed class EvidenceBuildDrainReadyProof
{
    private readonly EvidenceBuildRawDrainSession _session;
    private readonly EvidenceBuildJobLease _job;
    private readonly EvidenceBuildMatchedSuspendedProcessLease _process;
    private bool _consumed;

    internal EvidenceBuildDrainReadyProof(
        EvidenceBuildRawDrainSession session,
        EvidenceBuildJobLease job,
        EvidenceBuildMatchedSuspendedProcessLease process)
    {
        _session = session;
        _job = job;
        _process = process;
    }

    internal bool IsConsumed => _consumed;
    internal bool Matches(
        EvidenceBuildRawDrainSession session,
        EvidenceBuildJobLease job,
        EvidenceBuildMatchedSuspendedProcessLease process) =>
        ReferenceEquals(_session, session) && ReferenceEquals(_job, job) && ReferenceEquals(_process, process);
    internal void MarkConsumed()
    {
        if (_consumed) throw new InvalidOperationException("Build drain readiness proof was already consumed.");
        _consumed = true;
    }
}

internal sealed class EvidenceBuildActiveJobBorrow
{
    private bool _active = true;
    private readonly nint _job;
    private readonly nint _process;
    private readonly uint _processId;
    private readonly EvidenceBuildRunDeadline _deadline;

    internal EvidenceBuildActiveJobBorrow(nint job, nint process, uint processId, EvidenceBuildRunDeadline deadline)
    {
        _job = job;
        _process = process;
        _processId = processId;
        _deadline = deadline;
    }

    internal nint JobHandle => Active(_job);
    internal nint ProcessHandle => Active(_process);
    internal uint ProcessId => Active(_processId);
    internal EvidenceBuildRunDeadline Deadline => Active(_deadline);
    internal void EndBorrow() => _active = false;
    private T Active<T>(T value) => _active ? value : throw new ObjectDisposedException(nameof(EvidenceBuildActiveJobBorrow));
}

internal sealed class EvidenceBuildJobLease : IDisposable
{
    private readonly object _gate = new();
    private readonly IEvidenceBuildJobApi _api;
    private readonly IEvidenceBuildJobCleanupOwner _cleanupOwner;
    private readonly EvidenceBuildMatchedSuspendedProcessLease _process;
    private nint _job;
    private EvidenceBuildJobState _state = EvidenceBuildJobState.Created;
    private EvidenceBuildRunDeadline? _deadline;
    private bool _activeTopologyProven;
    private bool _borrowActive;
    private bool _disposeRequested;
    private bool _transferred;
    private bool _terminateIssued;
    private bool _closeAmbiguous;
    private Exception? _closeAmbiguousFailure;

    internal EvidenceBuildJobLease(
        IEvidenceBuildJobApi api,
        IEvidenceBuildJobCleanupOwner cleanupOwner,
        EvidenceBuildMatchedSuspendedProcessLease process,
        nint job)
    {
        _api = api;
        _cleanupOwner = cleanupOwner;
        _process = process;
        _job = job;
    }

    internal EvidenceBuildJobState State { get { lock (_gate) return _state; } }

    internal void ValidateNotProcessAlias()
    {
        lock (_gate)
        {
            RequireState(EvidenceBuildJobState.Created);
            _process.Borrow(process =>
            {
                if (_job != process.ProcessHandle && _job != process.ThreadHandle) return;
                // A CreateJob result cannot legitimately alias either already-owned child handle.
                // Disarm this slot before refusal so cleanup never closes foreign ownership.
                _job = 0;
                _state = EvidenceBuildJobState.Terminated;
                throw new InvalidDataException("Created build job handle aliased an already-owned child handle.");
            });
        }
    }

    internal void Configure(byte[] exactLimits)
    {
        lock (_gate)
        {
            RequireState(EvidenceBuildJobState.Created);
            try
            {
                if (exactLimits.Length != EvidenceBuildJobPolicy.ExtendedLimitBytes)
                    throw new InvalidDataException("Build job limit buffer was not exactly 144 bytes.");
                if (!_api.SetExtendedLimits(_job, (byte[])exactLimits.Clone(), out var setError))
                    throw new Win32Exception(setError, "Setting exact build job limits failed.");
                var queried = new byte[EvidenceBuildJobPolicy.ExtendedLimitBytes];
                if (!_api.QueryExtendedLimits(_job, queried, out var returned, out var queryError))
                    throw new Win32Exception(queryError, "Querying exact build job limits failed.");
                if (returned != queried.Length || !queried.AsSpan().SequenceEqual(exactLimits))
                    throw new InvalidDataException("Queried build job limits did not exactly match the configured 144-byte value.");
                RequireTopology(expectedPid: null, expectedTotal: 0, expectedActive: 0, expectedTerminated: 0);
                _state = EvidenceBuildJobState.Configured;
            }
            catch
            {
                _state = EvidenceBuildJobState.Faulted;
                throw;
            }
        }
    }

    internal void AssignSuspended()
    {
        lock (_gate)
        {
            RequireState(EvidenceBuildJobState.Configured);
            try
            {
                _process.Borrow(process =>
                {
                    if (!process.PreJobArmed || !process.PreResumeArmed)
                        throw new InvalidDataException("Build process was not retained in its pre-job, pre-resume state.");
                    if (!_api.IsProcessInAnyJob(process.ProcessHandle, out var inJob, out var queryError))
                        throw new Win32Exception(queryError, "Querying nested build-job membership failed.");
                    if (inJob) throw new InvalidOperationException("Build child already belonged to a job.");
                    if (!_api.AssignProcess(_job, process.ProcessHandle, out var assignError))
                        throw new Win32Exception(assignError, "Assigning the suspended build child to its exact job failed.");
                    // State is committed immediately after the only effectful assignment call.
                    _state = EvidenceBuildJobState.Assigned;
                    RequireTopology(process.ProcessId, expectedTotal: 1, expectedActive: 1, expectedTerminated: 0);
                    _activeTopologyProven = true;
                });
            }
            catch
            {
                _state = EvidenceBuildJobState.Faulted;
                throw;
            }
        }
    }

    internal EvidenceBuildDrainReadyProof AcquireDrainReadyProof(EvidenceBuildRawDrainSession drains)
    {
        ArgumentNullException.ThrowIfNull(drains);
        lock (_gate)
        {
            RequireState(EvidenceBuildJobState.Assigned);
            return drains.IssueReadyProof(this, _process);
        }
    }

    internal EvidenceBuildRunDeadline ResumeAfterDrainsReady(
        EvidenceBuildRawDrainSession drains,
        EvidenceBuildDrainReadyProof ready,
        IWin32MonotonicClock clock)
    {
        ArgumentNullException.ThrowIfNull(drains);
        ArgumentNullException.ThrowIfNull(ready);
        ArgumentNullException.ThrowIfNull(clock);
        lock (_gate)
        {
            RequireState(EvidenceBuildJobState.Assigned);
            drains.ConsumeReadyProof(ready, this, _process);
            _state = EvidenceBuildJobState.Faulted; // resume is a one-shot effect even if its result is malformed
            EvidenceBuildRunDeadline? armed = null;
            _process.Borrow(process =>
            {
                armed = EvidenceBuildRunDeadline.Arm(clock, EvidenceBuildRunDeadline.ExactTimeout);
                var previous = _api.ResumeThread(process.ThreadHandle, out var error);
                if (previous == EvidenceBuildJobPolicy.ResumeFailed)
                    throw new Win32Exception(error, "Resuming the assigned build child failed.");
                if (previous != 1)
                    throw new InvalidDataException($"Build primary thread had unexpected previous suspend count {previous}.");
            });
            _deadline = armed!;
            drains.ReleaseConsumedProof(ready, _deadline);
            _state = EvidenceBuildJobState.Resumed;
            return _deadline;
        }
    }

    internal void BorrowActive(Action<EvidenceBuildActiveJobBorrow> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_gate)
        {
            RequireState(EvidenceBuildJobState.Resumed);
            if (_borrowActive) throw new InvalidOperationException("Build job does not permit a nested active borrow.");
            _borrowActive = true;
            try
            {
                _process.Borrow(process =>
                {
                    var borrow = new EvidenceBuildActiveJobBorrow(_job, process.ProcessHandle, process.ProcessId, _deadline!);
                    try { operation(borrow); }
                    finally { borrow.EndBorrow(); }
                });
            }
            finally { _borrowActive = false; }
        }
    }

    internal void ProveEmptyAfterCompletion()
    {
        lock (_gate)
        {
            RequireState(EvidenceBuildJobState.Resumed);
            if (!_activeTopologyProven) throw new InvalidOperationException("Build job never proved its exact active PID topology.");
            try
            {
                RequireTopology(expectedPid: null, expectedTotal: 1, expectedActive: 0, expectedTerminated: 0);
                _state = EvidenceBuildJobState.EmptyProven;
            }
            catch
            {
                _state = EvidenceBuildJobState.Faulted;
                throw;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_job == 0) return;
            if (_borrowActive) throw new InvalidOperationException("Build job cannot be disposed inside its borrow callback.");
            _disposeRequested = true;
            AttemptCleanup(transferOnFailure: true);
        }
    }

    internal void RetryCleanupFromOwner()
    {
        lock (_gate)
        {
            if (_job == 0) return;
            AttemptCleanup(transferOnFailure: false);
        }
    }

    private void AttemptCleanup(bool transferOnFailure)
    {
        if (_closeAmbiguous)
        {
            ExceptionDispatchInfo.Capture(_closeAmbiguousFailure!).Throw();
        }
        Exception? failure = null;
        if (_state != EvidenceBuildJobState.EmptyProven && !_terminateIssued)
        {
            try
            {
                if (!_api.TerminateJob(_job, EvidenceBuildJobPolicy.CleanupExitCode, out var error))
                    failure = new Win32Exception(error, "Terminating the build job failed without exit proof.");
                else _terminateIssued = true;
            }
            catch (Exception exception) { failure = exception; }
        }
        if (failure is null && _state != EvidenceBuildJobState.EmptyProven)
        {
            try { RequireTopology(expectedPid: null, expectedTotal: null, expectedActive: 0, expectedTerminated: null); }
            catch (Exception exception) { failure = exception; }
        }
        if (failure is null)
        {
            try
            {
                if (!_api.CloseJob(_job, out var error))
                    failure = new Win32Exception(error, "Closing the retained build job was not proven.");
                else
                {
                    _job = 0;
                    _state = EvidenceBuildJobState.Terminated;
                    EvidenceBuildJobRetention.Release(this);
                }
            }
            catch (Exception exception)
            {
                _closeAmbiguous = true;
                _closeAmbiguousFailure = exception;
                failure = exception;
            }
        }
        if (failure is null) return;
        if (transferOnFailure && !_transferred)
        {
            _transferred = true;
            EvidenceBuildJobRetention.Retain(this, failure);
            try { _cleanupOwner.Retain(this, failure); }
            catch (Exception ownerFailure) { failure = new AggregateException(failure, ownerFailure); }
        }
        ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private void RequireTopology(
        uint? expectedPid,
        ulong? expectedTotal,
        uint expectedActive,
        ulong? expectedTerminated)
    {
        var raw = _api.QueryPidTopology(_job) ?? throw new InvalidDataException("Build job topology query returned null.");
        var succeeded = raw.Succeeded;
        var error = raw.Error;
        var bytes = raw.Bytes ?? throw new InvalidDataException("Build job topology bytes were absent.");
        var length = bytes.Length;
        var returnedBytes = raw.ReturnedBytes;
        var total = raw.TotalProcesses;
        var active = raw.ActiveProcesses;
        var terminated = raw.TotalTerminatedProcesses;
        if (!succeeded)
        {
            if (error == EvidenceBuildJobPolicy.ErrorMoreData)
                throw new InvalidDataException("Build job PID topology was truncated; larger/retry queries are forbidden.");
            throw new Win32Exception(error, "Querying exact build job PID topology failed.");
        }
        if (returnedBytes != length ||
            length is not EvidenceBuildJobPolicy.EmptyPidListBytes and not EvidenceBuildJobPolicy.OnePidListBytes)
            throw new InvalidDataException("Build job PID topology had a malformed or trailing byte length.");
        var snapshot = bytes.AsSpan(0, length).ToArray();
        var assigned = BinaryPrimitives.ReadUInt32LittleEndian(snapshot.AsSpan(0, sizeof(uint)));
        var listed = BinaryPrimitives.ReadUInt32LittleEndian(snapshot.AsSpan(4, sizeof(uint)));
        if (assigned > 1 || listed > 1 || listed > assigned)
            throw new InvalidDataException("Build job PID counts exceeded the exact one-process policy.");
        var expectedLength = checked(EvidenceBuildJobPolicy.EmptyPidListBytes + checked((int)listed * sizeof(ulong)));
        if (expectedLength != returnedBytes || assigned != listed)
            throw new InvalidDataException("Build job PID topology was truncated or count-inconsistent.");
        if ((expectedTotal.HasValue && total != expectedTotal.Value) ||
            (expectedTerminated.HasValue && terminated != expectedTerminated.Value) ||
            active > total || terminated > total || active != expectedActive || active != assigned)
            throw new InvalidDataException("Build job accounting did not match the exact PID topology.");
        if (expectedPid.HasValue)
        {
            if (listed != 1) throw new InvalidDataException("Build job did not contain exactly one active PID.");
            var pid64 = BinaryPrimitives.ReadUInt64LittleEndian(snapshot.AsSpan(8, sizeof(ulong)));
            if (pid64 == 0 || pid64 > uint.MaxValue || pid64 != expectedPid.Value)
                throw new InvalidDataException("Build job PID topology did not match the expected child.");
        }
        else if (listed != 0)
        {
            throw new InvalidDataException("Build job was not exactly empty.");
        }
    }

    private void RequireState(EvidenceBuildJobState expected)
    {
        if (_disposeRequested || _transferred || _job == 0)
            throw new ObjectDisposedException(nameof(EvidenceBuildJobLease));
        if (_state != expected)
            throw new InvalidOperationException($"Build job transition required {expected} but was {_state}.");
    }
}

internal sealed class EvidenceBuildJobFactory(
    IEvidenceBuildJobApi api,
    IEvidenceBuildJobCleanupOwner? cleanupOwner = null)
{
    private readonly IEvidenceBuildJobApi _api = api ?? throw new ArgumentNullException(nameof(api));
    private readonly IEvidenceBuildJobCleanupOwner _cleanupOwner = cleanupOwner ?? EvidenceBuildJobCleanupOwner.Instance;

    internal EvidenceBuildJobLease CreateConfigured(
        EvidenceFrozenBuildProcessRequest request,
        EvidenceBuildMatchedSuspendedProcessLease process)
    {
        ArgumentNullException.ThrowIfNull(process);
        var exact = EvidenceBuildJobPolicy.CompileExtendedLimits(request);
        var raw = _api.CreateUnnamedJob() ?? throw new InvalidDataException("Build job creation returned null.");
        var valid = raw.Handle is not 0 and not -1;
        EvidenceBuildJobLease? lease = valid ? new EvidenceBuildJobLease(_api, _cleanupOwner, process, raw.Handle) : null;
        Exception? failure = null;
        if (lease is not null)
        {
            try { lease.ValidateNotProcessAlias(); }
            catch (Exception exception) { failure = exception; }
        }
        if (!raw.Succeeded) failure = EvidenceBuildPipeHandleFactory.Combine(
            failure,
            new Win32Exception(raw.Error, "Creating the unnamed build job failed after handle adoption."));
        if (!valid) failure = EvidenceBuildPipeHandleFactory.Combine(failure, new InvalidDataException("Build job handle was invalid."));
        if (failure is null)
        {
            try { lease!.Configure(exact); return lease; }
            catch (Exception exception) { failure = exception; }
        }
        if (lease is not null)
        {
            try { lease.Dispose(); }
            catch (Exception cleanupFailure) { failure = EvidenceBuildPipeHandleFactory.Combine(failure, cleanupFailure); }
        }
        ExceptionDispatchInfo.Capture(failure!).Throw();
        throw new InvalidOperationException("Unreachable after build job creation refusal.");
    }
}
