using System.Collections.Immutable;
using System.Runtime.ExceptionServices;

namespace Rounds.EvidenceLauncher;

internal sealed class EvidenceBuildRunDeadline
{
    internal static readonly TimeSpan ExactTimeout = TimeSpan.FromMinutes(5);
    private readonly object _gate = new();
    private readonly IWin32MonotonicClock _clock;
    private readonly long _origin;
    private TimeSpan _lastElapsed;

    private EvidenceBuildRunDeadline(IWin32MonotonicClock clock, long origin)
    {
        _clock = clock;
        _origin = origin;
    }

    internal long Origin => _origin;

    internal static EvidenceBuildRunDeadline Arm(
        IWin32MonotonicClock clock,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (timeout != ExactTimeout)
        {
            throw new InvalidOperationException("Build deadline must be the exact admitted five minutes.");
        }
        var origin = clock.GetTimestamp();
        if (origin < 0)
        {
            throw new InvalidOperationException("Monotonic build origin was negative or wrapped.");
        }
        return new EvidenceBuildRunDeadline(clock, origin);
    }

    internal EvidenceBuildDeadlineObservation Observe()
    {
        lock (_gate)
        {
            TimeSpan elapsed;
            try
            {
                elapsed = _clock.GetElapsedTime(_origin);
            }
            catch (OverflowException exception)
            {
                throw new InvalidOperationException("Monotonic build deadline arithmetic overflowed.", exception);
            }
            if (elapsed < TimeSpan.Zero || elapsed < _lastElapsed)
            {
                throw new InvalidOperationException("Monotonic build clock rolled backward or wrapped.");
            }
            _lastElapsed = elapsed;
            if (elapsed >= ExactTimeout)
            {
                return new EvidenceBuildDeadlineObservation(elapsed, TimeSpan.Zero, Expired: true);
            }
            return new EvidenceBuildDeadlineObservation(
                elapsed,
                ExactTimeout - elapsed,
                Expired: false);
        }
    }

    internal void DelayNoProgress(TimeSpan maximumDelay)
    {
        if (maximumDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDelay));
        }
        var observation = Observe();
        if (observation.Expired)
        {
            return;
        }
        _clock.Delay(observation.Remaining < maximumDelay ? observation.Remaining : maximumDelay);
    }
}

internal readonly record struct EvidenceBuildDeadlineObservation(
    TimeSpan Elapsed,
    TimeSpan Remaining,
    bool Expired);

internal enum EvidenceBuildRawReadKind
{
    NoProgress,
    Data,
    EndOfFile,
}

internal readonly record struct EvidenceBuildRawRead(EvidenceBuildRawReadKind Kind, byte[] Data)
{
    internal static EvidenceBuildRawRead NoProgress() =>
        new(EvidenceBuildRawReadKind.NoProgress, Array.Empty<byte>());

    internal static EvidenceBuildRawRead EndOfFile() =>
        new(EvidenceBuildRawReadKind.EndOfFile, Array.Empty<byte>());

    internal static EvidenceBuildRawRead Bytes(params byte[] bytes) =>
        new(EvidenceBuildRawReadKind.Data, bytes);
}

internal readonly record struct EvidenceBuildRawSource(string Identity)
{
    internal static EvidenceBuildRawSource Create(string identity)
    {
        if (string.IsNullOrWhiteSpace(identity) || identity.Contains('\0'))
        {
            throw new ArgumentException("Build raw source identity was empty or contained NUL.", nameof(identity));
        }
        return new EvidenceBuildRawSource(identity);
    }
}

internal interface IEvidenceBuildRawReadApi
{
    EvidenceBuildRawRead Poll(EvidenceBuildRawSource source, int maximumBytes);
}

internal interface IEvidenceBuildRawByteCopier
{
    byte[] CloneBounded(byte[] source, int exactLength);
}

internal sealed class EvidenceBuildRawByteCopier : IEvidenceBuildRawByteCopier
{
    public byte[] CloneBounded(byte[] source, int exactLength)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (exactLength <= 0 || exactLength != source.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(exactLength));
        }
        return source.AsSpan(0, exactLength).ToArray();
    }
}

internal interface IEvidenceBuildDrainWorkerStarter
{
    Task<T> Start<T>(Func<T> operation);

    bool WaitForCompletion(Task task, TimeSpan timeout);
}

internal sealed class EvidenceBuildDrainWorkerStarter : IEvidenceBuildDrainWorkerStarter
{
    public Task<T> Start<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return Task.Factory.StartNew(
            operation,
            CancellationToken.None,
            TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
    }

    public bool WaitForCompletion(Task task, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (timeout < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        try
        {
            return task.Wait(timeout);
        }
        catch (AggregateException)
        {
            return true;
        }
    }
}

internal sealed record EvidenceBuildRawDrainPolicy(
    int StandardOutputCapBytes,
    int StandardErrorCapBytes,
    TimeSpan Deadline)
{
    internal const int ExactStreamCapBytes = 4 * 1024 * 1024;

    internal static EvidenceBuildRawDrainPolicy Exact { get; } = new(
        ExactStreamCapBytes,
        ExactStreamCapBytes,
        EvidenceBuildRunDeadline.ExactTimeout);

    internal void Validate()
    {
        if (StandardOutputCapBytes != ExactStreamCapBytes ||
            StandardErrorCapBytes != ExactStreamCapBytes ||
            Deadline != EvidenceBuildRunDeadline.ExactTimeout)
        {
            throw new InvalidOperationException("Build raw-drain policy did not match the exact admitted caps and deadline.");
        }
    }
}

internal enum EvidenceBuildRawDrainTerminal
{
    EndOfFile,
    CapExceeded,
    TimedOut,
    Stopped,
}

internal sealed class EvidenceBuildRawStreamCapture
{
    internal EvidenceBuildRawStreamCapture(
        byte[] bytes,
        long bytesObserved,
        EvidenceBuildRawDrainTerminal terminal)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        Bytes = ImmutableArray.Create(bytes);
        BytesObserved = bytesObserved;
        Terminal = terminal;
    }

    internal ImmutableArray<byte> Bytes { get; }

    internal long BytesObserved { get; }

    internal EvidenceBuildRawDrainTerminal Terminal { get; }

    internal bool EndOfFile => Terminal == EvidenceBuildRawDrainTerminal.EndOfFile;

    internal bool CapExceeded => Terminal == EvidenceBuildRawDrainTerminal.CapExceeded;

    internal bool TimedOut => Terminal == EvidenceBuildRawDrainTerminal.TimedOut;

    internal bool Stopped => Terminal == EvidenceBuildRawDrainTerminal.Stopped;
}

internal sealed record EvidenceBuildRawDrainCapture(
    EvidenceBuildRawStreamCapture StandardOutput,
    EvidenceBuildRawStreamCapture StandardError)
{
    internal bool TimedOut => StandardOutput.TimedOut || StandardError.TimedOut;

    internal bool StandardOutputCapExceeded => StandardOutput.CapExceeded;

    internal bool StandardErrorCapExceeded => StandardError.CapExceeded;

    internal bool BothReachedEndOfFile => StandardOutput.EndOfFile && StandardError.EndOfFile;
}

internal sealed class EvidenceBuildRawDrainFactory(
    IEvidenceBuildRawReadApi readApi,
    IEvidenceBuildDrainWorkerStarter? workerStarter = null,
    IEvidenceBuildRawByteCopier? byteCopier = null)
{
    private static readonly TimeSpan StartupAndCleanupBound = TimeSpan.FromSeconds(5);
    private readonly IEvidenceBuildRawReadApi _readApi =
        readApi ?? throw new ArgumentNullException(nameof(readApi));
    private readonly IEvidenceBuildDrainWorkerStarter _workerStarter =
        workerStarter ?? new EvidenceBuildDrainWorkerStarter();
    private readonly IEvidenceBuildRawByteCopier _byteCopier =
        byteCopier ?? new EvidenceBuildRawByteCopier();

    internal EvidenceBuildRawDrainSession Prepare(
        EvidenceBuildRawSource standardOutput,
        EvidenceBuildRawSource standardError,
        EvidenceBuildRawDrainPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
        policy.Validate();
        if (string.IsNullOrWhiteSpace(standardOutput.Identity) || standardOutput.Identity.Contains('\0') ||
            string.IsNullOrWhiteSpace(standardError.Identity) || standardError.Identity.Contains('\0') ||
            standardOutput == standardError)
        {
            throw new InvalidOperationException("Build stdout and stderr require distinct retained sources.");
        }

        var session = new EvidenceBuildRawDrainSession(
            _readApi,
            _workerStarter,
            _byteCopier,
            standardOutput,
            standardError,
            policy,
            StartupAndCleanupBound);
        return session.StartAndProveReady();
    }
}

internal sealed class EvidenceBuildRawDrainSession : IDisposable
{
    private const int MaximumPollBytes = 64 * 1024;
    private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(5);
    private readonly object _stateGate = new();
    private readonly IEvidenceBuildRawReadApi _readApi;
    private readonly IEvidenceBuildDrainWorkerStarter _workerStarter;
    private readonly IEvidenceBuildRawByteCopier _byteCopier;
    private readonly EvidenceBuildRawSource _standardOutput;
    private readonly EvidenceBuildRawSource _standardError;
    private readonly EvidenceBuildRawDrainPolicy _policy;
    private readonly TimeSpan _cleanupBound;
    private readonly ManualResetEventSlim _releaseGate = new(initialState: false);
    private readonly TaskCompletionSource _standardOutputStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _standardErrorStarted =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly EvidenceBuildDrainStopSignal _stop = new();
    private Task<EvidenceBuildRawStreamCapture>? _standardOutputTask;
    private Task<EvidenceBuildRawStreamCapture>? _standardErrorTask;
    private EvidenceBuildRunDeadline? _deadline;
    private EvidenceBuildRawDrainCapture? _capture;
    private bool _terminalProof;
    private bool _disposed;
    private bool _gateDisposed;
    private bool _standardOutputFaultDelivered;
    private bool _standardErrorFaultDelivered;
    private EvidenceBuildDrainReadyProof? _issuedReadyProof;
    private bool _readyProofConsumed;

    internal EvidenceBuildRawDrainSession(
        IEvidenceBuildRawReadApi readApi,
        IEvidenceBuildDrainWorkerStarter workerStarter,
        IEvidenceBuildRawByteCopier byteCopier,
        EvidenceBuildRawSource standardOutput,
        EvidenceBuildRawSource standardError,
        EvidenceBuildRawDrainPolicy policy,
        TimeSpan cleanupBound)
    {
        _readApi = readApi;
        _workerStarter = workerStarter;
        _byteCopier = byteCopier;
        _standardOutput = standardOutput;
        _standardError = standardError;
        _policy = policy;
        _cleanupBound = cleanupBound;
    }

    internal bool ReadyForDeadline =>
        _standardOutputStarted.Task.IsCompletedSuccessfully &&
        _standardErrorStarted.Task.IsCompletedSuccessfully &&
        !_releaseGate.IsSet;

    internal EvidenceBuildDrainReadyProof IssueReadyProof(
        EvidenceBuildJobLease job,
        EvidenceBuildMatchedSuspendedProcessLease process)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(process);
        lock (_stateGate)
        {
            ThrowIfDisposed();
            if (!ReadyForDeadline || _deadline is not null || _terminalProof || _issuedReadyProof is not null)
            {
                throw new InvalidOperationException("Build drain readiness can be issued exactly once while both workers remain gated.");
            }
            _issuedReadyProof = new EvidenceBuildDrainReadyProof(this, job, process);
            return _issuedReadyProof;
        }
    }

    internal void ConsumeReadyProof(
        EvidenceBuildDrainReadyProof proof,
        EvidenceBuildJobLease job,
        EvidenceBuildMatchedSuspendedProcessLease process)
    {
        ArgumentNullException.ThrowIfNull(proof);
        lock (_stateGate)
        {
            ThrowIfDisposed();
            if (!ReferenceEquals(_issuedReadyProof, proof) || _readyProofConsumed ||
                !proof.Matches(this, job, process) || !ReadyForDeadline ||
                _deadline is not null || _terminalProof)
            {
                throw new InvalidOperationException("Build drain readiness proof was wrong, stale, released, or already consumed.");
            }
            _readyProofConsumed = true;
            proof.MarkConsumed();
        }
    }

    internal void ReleaseConsumedProof(EvidenceBuildDrainReadyProof proof, EvidenceBuildRunDeadline deadline)
    {
        ArgumentNullException.ThrowIfNull(proof);
        ArgumentNullException.ThrowIfNull(deadline);
        lock (_stateGate)
        {
            ThrowIfDisposed();
            if (!ReferenceEquals(_issuedReadyProof, proof) || !_readyProofConsumed || !proof.IsConsumed ||
                !ReadyForDeadline || _deadline is not null || _terminalProof)
            {
                throw new InvalidOperationException("Build drain readiness proof could not release this exact session.");
            }
            _deadline = deadline;
            _releaseGate.Set();
        }
    }

    internal EvidenceBuildRawDrainSession StartAndProveReady()
    {
        Exception? failure = null;
        try
        {
            _standardOutputTask = StartWorker(
                _standardOutput,
                _policy.StandardOutputCapBytes,
                _standardOutputStarted);
            _standardErrorTask = StartWorker(
                _standardError,
                _policy.StandardErrorCapBytes,
                _standardErrorStarted);
            var bothStarted = Task.WhenAll(_standardOutputStarted.Task, _standardErrorStarted.Task);
            if (!_workerStarter.WaitForCompletion(bothStarted, _cleanupBound))
            {
                throw new TimeoutException("Both build pipe drains did not prove gated startup in five seconds.");
            }
            bothStarted.GetAwaiter().GetResult();
            return this;
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        _stop.Request();
        _releaseGate.Set();
        failure = ObserveStartupCleanup(failure!);
        ExceptionDispatchInfo.Capture(failure).Throw();
        throw new InvalidOperationException("Unreachable after build drain startup failure.");
    }

    internal void Release(EvidenceBuildRunDeadline deadline)
    {
        ArgumentNullException.ThrowIfNull(deadline);
        lock (_stateGate)
        {
            ThrowIfDisposed();
            if (!ReadyForDeadline || _deadline is not null || _terminalProof || _issuedReadyProof is not null)
            {
                throw new InvalidOperationException("Build drains were not in the exact gated readiness state.");
            }
            _deadline = deadline;
            _releaseGate.Set();
        }
    }

    internal EvidenceBuildRawDrainCapture Complete()
    {
        lock (_stateGate)
        {
            ThrowIfDisposed();
            if (_capture is not null) return _capture;
            if (_terminalProof)
            {
                throw new InvalidOperationException("Build drains already terminated without a successful capture.");
            }
            if (_deadline is null || _standardOutputTask is null || _standardErrorTask is null)
            {
                throw new InvalidOperationException("Build drains require the shared armed deadline before completion.");
            }
        }

        var all = Task.WhenAll(_standardOutputTask, _standardErrorTask);
        ObserveEventually(all);
        Exception? failure = null;
        var completed = false;
        var deadlineExpired = false;
        try
        {
            var observation = _deadline.Observe();
            deadlineExpired = observation.Expired;
            completed = _workerStarter.WaitForCompletion(
                all,
                deadlineExpired ? _cleanupBound : observation.Remaining);
        }
        catch (Exception exception)
        {
            failure = exception;
            completed = all.IsCompleted;
        }
        if (!completed)
        {
            _stop.Request();
            if (!deadlineExpired)
            {
                try
                {
                    completed = _workerStarter.WaitForCompletion(all, _cleanupBound);
                }
                catch (Exception exception)
                {
                    failure = Combine(failure, exception);
                    completed = all.IsCompleted;
                }
            }
        }
        if (!completed)
        {
            failure = Combine(
                failure,
                new TimeoutException("Build pipe drains did not stop within the bounded cleanup interval."));
            ThrowWorkerFailures(failure);
        }

        failure = CollectUndeliveredWorkerFailures(failure);
        MarkTerminalProof();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        if (AnyWorkerFaultWasDelivered())
        {
            throw new InvalidOperationException("Build drain worker failure was already delivered to the caller.");
        }
        var capture = new EvidenceBuildRawDrainCapture(
            _standardOutputTask.GetAwaiter().GetResult(),
            _standardErrorTask.GetAwaiter().GetResult());
        lock (_stateGate)
        {
            _capture = capture;
            return capture;
        }
    }

    internal void StopAndWait()
    {
        lock (_stateGate)
        {
            if (_disposed || _terminalProof) return;
            _stop.Request();
            _releaseGate.Set();
        }
        var all = AllStartedWorkers();
        Exception? failure = null;
        var completed = all is null;
        if (all is not null)
        {
            try
            {
                completed = _workerStarter.WaitForCompletion(all, _cleanupBound);
            }
            catch (Exception exception)
            {
                failure = exception;
                completed = all.IsCompleted;
            }
        }
        if (!completed)
        {
            failure = Combine(
                failure,
                new TimeoutException("Build pipe drain stop did not prove worker completion in five seconds."));
            ThrowWorkerFailures(failure);
        }
        failure = CollectUndeliveredWorkerFailures(failure);
        MarkTerminalProof();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    public void Dispose()
    {
        lock (_stateGate)
        {
            if (_disposed) return;
        }
        StopAndWait();
        lock (_stateGate)
        {
            _disposed = true;
        }
    }

    private Task<EvidenceBuildRawStreamCapture> StartWorker(
        EvidenceBuildRawSource source,
        int capBytes,
        TaskCompletionSource started)
    {
        var task = _workerStarter.Start(() => Worker(source, capBytes, started));
        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
        return task;
    }

    private EvidenceBuildRawStreamCapture Worker(
        EvidenceBuildRawSource source,
        int capBytes,
        TaskCompletionSource started)
    {
        started.TrySetResult();
        _releaseGate.Wait();
        if (_stop.Requested)
        {
            return Capture([], 0, EvidenceBuildRawDrainTerminal.Stopped);
        }
        var deadline = _deadline ??
            throw new InvalidOperationException("Build drain release did not publish the shared deadline.");
        using var retained = new MemoryStream(capacity: MaximumPollBytes);
        long observed = 0;
        try
        {
            while (true)
            {
                if (_stop.Requested)
                {
                    return Capture(retained.ToArray(), observed, EvidenceBuildRawDrainTerminal.Stopped);
                }
                var time = deadline.Observe();
                if (time.Expired)
                {
                    _stop.Request();
                    return Capture(retained.ToArray(), observed, EvidenceBuildRawDrainTerminal.TimedOut);
                }

                var retainedCount = checked((int)retained.Length);
                var maximumRead = Math.Min(MaximumPollBytes, checked(capBytes - retainedCount + 1));
                var read = _readApi.Poll(source, maximumRead);
                var kind = read.Kind;
                var data = read.Data;
                if (!Enum.IsDefined(kind))
                {
                    throw new InvalidDataException("Build raw read returned an unknown state.");
                }
                if (data is null)
                {
                    throw new InvalidDataException("Build raw read returned a null byte buffer.");
                }
                switch (kind)
                {
                    case EvidenceBuildRawReadKind.NoProgress:
                        if (data.Length != 0)
                        {
                            throw new InvalidDataException("No-progress build read carried bytes.");
                        }
                        deadline.DelayNoProgress(PollDelay);
                        break;
                    case EvidenceBuildRawReadKind.EndOfFile:
                        if (data.Length != 0)
                        {
                            throw new InvalidDataException("Explicit build EOF carried bytes.");
                        }
                        return Capture(retained.ToArray(), observed, EvidenceBuildRawDrainTerminal.EndOfFile);
                    case EvidenceBuildRawReadKind.Data:
                        if (data.Length == 0)
                        {
                            throw new InvalidDataException("A zero-byte successful build read must be reported as no progress.");
                        }
                        if (data.Length > maximumRead)
                        {
                            throw new InvalidDataException("Build raw read exceeded the requested bounded byte count.");
                        }
                        observed = checked(observed + data.Length);
                        var accepted = Math.Min(data.Length, checked(capBytes - retainedCount));
                        var readBytes = _byteCopier.CloneBounded(data, data.Length);
                        if (ReferenceEquals(readBytes, data) || readBytes.Length != data.Length)
                        {
                            throw new InvalidDataException("Build raw byte copier did not return an independent exact clone.");
                        }
                        if (accepted > 0) retained.Write(readBytes, 0, accepted);
                        if (accepted != data.Length)
                        {
                            _stop.Request();
                            return Capture(retained.ToArray(), observed, EvidenceBuildRawDrainTerminal.CapExceeded);
                        }
                        break;
                }
            }
        }
        catch
        {
            _stop.Request();
            throw;
        }
    }

    private static EvidenceBuildRawStreamCapture Capture(
        byte[] bytes,
        long observed,
        EvidenceBuildRawDrainTerminal terminal) =>
        new(bytes, observed, terminal);

    private Exception ObserveStartupCleanup(Exception primary)
    {
        var all = AllStartedWorkers();
        var completed = all is null;
        if (all is not null)
        {
            try
            {
                completed = _workerStarter.WaitForCompletion(all, _cleanupBound);
            }
            catch (Exception exception)
            {
                primary = Combine(primary, exception);
                completed = all.IsCompleted;
            }
        }
        if (!completed)
        {
            primary = Combine(
                primary,
                new TimeoutException("Started build drain did not stop after startup failure."));
        }
        else
        {
            MarkTerminalProof();
        }
        return CollectUndeliveredWorkerFailures(primary)!;
    }

    private Task? AllStartedWorkers()
    {
        var tasks = new List<Task>(2);
        if (_standardOutputTask is not null) tasks.Add(_standardOutputTask);
        if (_standardErrorTask is not null) tasks.Add(_standardErrorTask);
        if (tasks.Count == 0) return null;
        var all = Task.WhenAll(tasks);
        ObserveEventually(all);
        return all;
    }

    private void ThrowWorkerFailures(Exception? primary)
    {
        var failure = CollectUndeliveredWorkerFailures(primary);
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private void MarkTerminalProof()
    {
        lock (_stateGate)
        {
            _terminalProof = true;
            DisposeGateAfterTerminalProof();
        }
    }

    private bool AnyWorkerFaultWasDelivered()
    {
        lock (_stateGate)
        {
            return _standardOutputFaultDelivered || _standardErrorFaultDelivered;
        }
    }

    private static Exception Combine(Exception? first, Exception second) =>
        first is null ? second : new AggregateException(first, second);

    private Exception? CollectUndeliveredWorkerFailures(Exception? primary)
    {
        var failures = new List<Exception>();
        if (primary is not null) failures.Add(primary);
        lock (_stateGate)
        {
            CollectCompletedFailure(
                _standardOutputTask,
                ref _standardOutputFaultDelivered,
                failures);
            CollectCompletedFailure(
                _standardErrorTask,
                ref _standardErrorFaultDelivered,
                failures);
        }
        return failures.Count switch
        {
            0 => null,
            1 => failures[0],
            _ => new AggregateException(failures),
        };
    }

    private static void CollectCompletedFailure(
        Task? task,
        ref bool delivered,
        List<Exception> failures)
    {
        if (task is null || !task.IsCompleted || delivered) return;
        try
        {
            task.GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            delivered = true;
            failures.Add(exception);
        }
    }

    private static void ObserveEventually(Task task) =>
        _ = task.ContinueWith(
            static completed => _ = completed.Exception,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);

    private void DisposeGateAfterTerminalProof()
    {
        if (_gateDisposed) return;
        _releaseGate.Dispose();
        _gateDisposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(EvidenceBuildRawDrainSession));
    }
}

internal sealed class EvidenceBuildDrainStopSignal
{
    private int _requested;

    internal bool Requested => Volatile.Read(ref _requested) != 0;

    internal void Request() => Interlocked.Exchange(ref _requested, 1);
}
