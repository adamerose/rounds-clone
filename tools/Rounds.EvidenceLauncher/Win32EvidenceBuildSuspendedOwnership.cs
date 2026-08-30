using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.ExceptionServices;

namespace Rounds.EvidenceLauncher;

internal sealed record EvidenceBuildExecutableAncestorIdentity(
    string Path,
    string HandleIdentity,
    bool IsDirectory,
    bool ReparseFree,
    bool RenameDeleteExcluded);

internal sealed record EvidenceBuildExecutableContinuityProof(
    string ExecutablePath,
    ImmutableArray<EvidenceBuildExecutableAncestorIdentity> Ancestors,
    bool ExactPath,
    bool ReparseFree,
    bool RenameDeleteExcluded);

internal sealed class EvidenceBuildExecutableBorrow
{
    private bool _active = true;
    private readonly nint _handle;
    private readonly EvidenceOpenedExecutableIdentity _identity;
    private readonly EvidenceBuildExecutableContinuityProof _continuity;

    internal EvidenceBuildExecutableBorrow(
        nint handle,
        EvidenceOpenedExecutableIdentity identity,
        EvidenceBuildExecutableContinuityProof continuity)
    {
        _handle = handle;
        _identity = identity;
        _continuity = continuity;
    }

    internal nint Handle => _active
        ? _handle
        : throw new ObjectDisposedException(nameof(EvidenceBuildExecutableBorrow));
    internal EvidenceOpenedExecutableIdentity Identity => _active
        ? _identity
        : throw new ObjectDisposedException(nameof(EvidenceBuildExecutableBorrow));
    internal EvidenceBuildExecutableContinuityProof Continuity => _active
        ? _continuity
        : throw new ObjectDisposedException(nameof(EvidenceBuildExecutableBorrow));

    internal void EndBorrow() => _active = false;
}

internal sealed class EvidenceBuildRetainedExecutableLease : IDisposable
{
    private readonly object _gate = new();
    private readonly IEvidenceBuildKernelHandleApi _api;
    private readonly IEvidenceBuildKernelHandleCleanupOwner _cleanupOwner;
    private readonly nint[] _ownedHandles;
    private readonly EvidenceOpenedExecutableIdentity _identity;
    private readonly EvidenceBuildExecutableContinuityProof _continuity;
    private bool _borrowActive;
    private bool _disposed;

    internal EvidenceBuildRetainedExecutableLease(
        IEvidenceBuildKernelHandleApi api,
        IEvidenceBuildKernelHandleCleanupOwner cleanupOwner,
        nint executableHandle,
        EvidenceOpenedExecutableIdentity identity,
        IReadOnlyList<nint> ancestorHandles,
        IReadOnlyList<EvidenceBuildExecutableAncestorIdentity> ancestors)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _cleanupOwner = cleanupOwner ?? throw new ArgumentNullException(nameof(cleanupOwner));
        var handleCount = ancestorHandles is null ? 1 : checked(ancestorHandles.Count + 1);
        _ownedHandles = new nint[handleCount];
        var seen = new HashSet<nint>();
        if (executableHandle is not 0 and not -1 && seen.Add(executableHandle))
        {
            _ownedHandles[0] = executableHandle;
        }
        if (ancestorHandles is not null)
        {
            for (var index = 0; index < ancestorHandles.Count; index++)
            {
                var handle = ancestorHandles[index];
                if (handle is not 0 and not -1 && seen.Add(handle))
                {
                    _ownedHandles[index + 1] = handle;
                }
            }
        }

        EvidenceOpenedExecutableIdentity frozenIdentity;
        EvidenceBuildExecutableContinuityProof frozenContinuity;
        try
        {
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentNullException.ThrowIfNull(ancestorHandles);
            ArgumentNullException.ThrowIfNull(ancestors);
            if (executableHandle is 0 or -1 || _ownedHandles[0] == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(executableHandle));
            }
            if (ancestorHandles.Count == 0 || ancestorHandles.Count != ancestors.Count ||
                _ownedHandles.Skip(1).Any(handle => handle == 0))
            {
                throw new InvalidDataException(
                    "Build executable requires one distinct retained handle per exact ancestor.");
            }
            if (!identity.Exists || !identity.IdentityBound || identity.IsReparsePoint ||
                !Path.IsPathFullyQualified(identity.Path) || string.IsNullOrWhiteSpace(identity.OpenedHandleIdentity))
            {
                throw new InvalidDataException("Build executable expected identity was incomplete.");
            }

            var expectedPaths = ExactAncestorPaths(identity.Path);
            if (expectedPaths.Length != ancestors.Count)
            {
                throw new InvalidDataException("Build executable proof did not retain the complete ancestor chain.");
            }
            var frozenAncestors = ImmutableArray.CreateBuilder<EvidenceBuildExecutableAncestorIdentity>(ancestors.Count);
            for (var index = 0; index < ancestors.Count; index++)
            {
                var ancestor = ancestors[index] ?? throw new InvalidDataException("Build ancestor proof was null.");
                if (!Path.IsPathFullyQualified(ancestor.Path) ||
                    !string.Equals(ancestor.Path, expectedPaths[index], StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(ancestor.HandleIdentity) ||
                    !ancestor.IsDirectory || !ancestor.ReparseFree || !ancestor.RenameDeleteExcluded)
                {
                    throw new InvalidDataException("Build executable ancestor proof was incomplete or out of order.");
                }
                frozenAncestors.Add(new EvidenceBuildExecutableAncestorIdentity(
                    string.Concat(ancestor.Path),
                    string.Concat(ancestor.HandleIdentity),
                    ancestor.IsDirectory,
                    ancestor.ReparseFree,
                    ancestor.RenameDeleteExcluded));
            }

            frozenIdentity = new EvidenceOpenedExecutableIdentity(
                string.Concat(identity.Path), identity.Exists, identity.IdentityBound, identity.IsReparsePoint,
                string.Concat(identity.OpenedHandleIdentity), string.Concat(identity.Sha256),
                string.Concat(identity.FileVersion), string.Concat(identity.ProductVersion));
            frozenContinuity = new EvidenceBuildExecutableContinuityProof(
                frozenIdentity.Path,
                frozenAncestors.MoveToImmutable(),
                ExactPath: true,
                ReparseFree: true,
                RenameDeleteExcluded: true);
        }
        catch (Exception validationFailure)
        {
            Exception? failure = validationFailure;
            for (var index = _ownedHandles.Length - 1; index >= 0; index--)
            {
                EvidenceBuildPipeHandleFactory.CloseOne(
                    _ownedHandles,
                    index,
                    index == 0 ? "rejected retained build executable" : $"rejected build executable ancestor {index - 1}",
                    _api,
                    _cleanupOwner,
                    ref failure);
            }
            ExceptionDispatchInfo.Capture(failure!).Throw();
            throw;
        }

        _identity = frozenIdentity;
        _continuity = frozenContinuity;
    }

    private static ImmutableArray<string> ExactAncestorPaths(string executablePath)
    {
        var paths = new Stack<string>();
        var current = Directory.GetParent(executablePath);
        while (current is not null)
        {
            paths.Push(current.FullName);
            current = current.Parent;
        }
        return [.. paths];
    }

    internal void Borrow(Action<EvidenceBuildExecutableBorrow> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_borrowActive)
            {
                throw new InvalidOperationException("Build executable lease does not permit a nested borrow.");
            }
            var borrow = new EvidenceBuildExecutableBorrow(_ownedHandles[0], _identity, _continuity);
            _borrowActive = true;
            try { operation(borrow); }
            finally
            {
                borrow.EndBorrow();
                _borrowActive = false;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (_borrowActive)
            {
                throw new InvalidOperationException("Build executable lease cannot be disposed from its borrow callback.");
            }
            _disposed = true;
            Exception? failure = null;
            for (var index = _ownedHandles.Length - 1; index >= 0; index--)
            {
                EvidenceBuildPipeHandleFactory.CloseOne(
                    _ownedHandles,
                    index,
                    index == 0 ? "retained build executable" : $"retained build executable ancestor {index - 1}",
                    _api,
                    _cleanupOwner,
                    ref failure);
            }
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}

internal sealed record EvidenceBuildRawSuspendedProcessResult(
    bool Succeeded,
    nint ProcessHandle,
    nint ThreadHandle,
    uint ProcessId,
    uint ThreadId,
    int Error);

internal interface IEvidenceBuildSuspendedProcessApi : IEvidenceBuildKernelHandleApi
{
    bool TerminateProcess(nint process, uint exitCode, out int error);
    uint WaitForSingleObject(nint process, uint milliseconds, out int error);
}

internal interface IEvidenceBuildSuspendedProcessCleanupOwner
{
    void Retain(EvidenceBuildSuspendedProcessOwner owner, Exception failure);
}

internal sealed class EvidenceBuildSuspendedProcessCleanupOwner : IEvidenceBuildSuspendedProcessCleanupOwner
{
    internal static EvidenceBuildSuspendedProcessCleanupOwner Instance { get; } = new();
    private EvidenceBuildSuspendedProcessCleanupOwner() { }
    public void Retain(EvidenceBuildSuspendedProcessOwner owner, Exception failure) { }
}

internal static class EvidenceBuildSuspendedProcessRetention
{
    private static readonly ConcurrentDictionary<EvidenceBuildSuspendedProcessOwner, byte> Retained = new();

    internal static void Retain(EvidenceBuildSuspendedProcessOwner owner) => Retained.TryAdd(owner, 0);
    internal static void Release(EvidenceBuildSuspendedProcessOwner owner) => Retained.TryRemove(owner, out _);
    internal static bool Contains(EvidenceBuildSuspendedProcessOwner owner) => Retained.ContainsKey(owner);
}

internal sealed class EvidenceBuildSuspendedProcessBorrow
{
    private bool _active = true;
    private readonly nint _process;
    private readonly nint _thread;
    private readonly uint _processId;
    private readonly uint _threadId;

    internal EvidenceBuildSuspendedProcessBorrow(nint process, nint thread, uint processId, uint threadId)
    {
        _process = process;
        _thread = thread;
        _processId = processId;
        _threadId = threadId;
    }

    internal nint ProcessHandle => Active(_process);
    internal nint ThreadHandle => Active(_thread);
    internal uint ProcessId => Active(_processId);
    internal uint ThreadId => Active(_threadId);
    internal bool PreJobArmed => Active(true);
    internal bool PreResumeArmed => Active(true);

    internal void EndBorrow() => _active = false;

    private T Active<T>(T value) => _active
        ? value
        : throw new ObjectDisposedException(nameof(EvidenceBuildSuspendedProcessBorrow));
}

internal sealed class EvidenceBuildSuspendedProcessOwner : IDisposable
{
    internal const uint CleanupExitCode = 0xe0350001;
    internal const uint CleanupWaitMilliseconds = 5_000;
    internal const uint WaitObject0 = 0;
    internal const uint WaitTimeout = 258;
    internal const uint WaitFailed = 0xffffffff;

    private readonly object _gate = new();
    private readonly IEvidenceBuildSuspendedProcessApi _api;
    private readonly IEvidenceBuildKernelHandleCleanupOwner _kernelCleanupOwner;
    private readonly IEvidenceBuildSuspendedProcessCleanupOwner _processCleanupOwner;
    private readonly nint[] _handles;
    private readonly uint _processId;
    private readonly uint _threadId;
    private bool _disposeRequested;
    private bool _cleanupComplete;
    private bool _transferred;
    private bool _borrowActive;

    private EvidenceBuildSuspendedProcessOwner(
        IEvidenceBuildSuspendedProcessApi api,
        IEvidenceBuildKernelHandleCleanupOwner kernelCleanupOwner,
        IEvidenceBuildSuspendedProcessCleanupOwner processCleanupOwner,
        nint process,
        nint thread,
        uint processId,
        uint threadId)
    {
        _api = api;
        _kernelCleanupOwner = kernelCleanupOwner;
        _processCleanupOwner = processCleanupOwner;
        _handles = [process, thread];
        _processId = processId;
        _threadId = threadId;
    }

    internal static EvidenceBuildSuspendedProcessOwner Adopt(
        IEvidenceBuildSuspendedProcessApi api,
        IEvidenceBuildKernelHandleCleanupOwner kernelCleanupOwner,
        IEvidenceBuildSuspendedProcessCleanupOwner processCleanupOwner,
        EvidenceBuildRawSuspendedProcessResult raw,
        EvidenceBuildExecutableBorrow executable,
        EvidenceBuildPipeCreateBorrow pipes,
        Action<EvidenceBuildSuspendedProcessOwner>? postAdoption = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(kernelCleanupOwner);
        ArgumentNullException.ThrowIfNull(processCleanupOwner);
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(pipes);
        var protectedHandles = new HashSet<nint>(pipes.AllHandles) { executable.Handle };
        var processValid = raw.ProcessHandle is not 0 and not -1;
        var threadValid = raw.ThreadHandle is not 0 and not -1;
        var processForeign = processValid && protectedHandles.Contains(raw.ProcessHandle);
        var threadForeign = threadValid && protectedHandles.Contains(raw.ThreadHandle);
        var pairAlias = processValid && threadValid && raw.ProcessHandle == raw.ThreadHandle;
        var process = processValid && !processForeign ? raw.ProcessHandle : 0;
        var thread = threadValid && !threadForeign && !pairAlias ? raw.ThreadHandle : 0;
        var owner = new EvidenceBuildSuspendedProcessOwner(
            api, kernelCleanupOwner, processCleanupOwner, process, thread, raw.ProcessId, raw.ThreadId);

        Exception? failure = null;
        if (!raw.Succeeded)
        {
            failure = new Win32Exception(raw.Error, "Suspended build process creation failed after handle adoption.");
        }
        if (!processValid || !threadValid || processForeign || threadForeign || pairAlias ||
            raw.ProcessId == 0 || raw.ThreadId == 0 || process == 0 || thread == 0)
        {
            failure = EvidenceBuildPipeHandleFactory.Combine(
                failure,
                new InvalidDataException("Suspended build process handles or identifiers were invalid or aliased."));
        }
        if (failure is null && postAdoption is not null)
        {
            try { postAdoption(owner); }
            catch (Exception exception) { failure = exception; }
        }
        if (failure is null) return owner;

        try { owner.Dispose(); }
        catch (Exception cleanupFailure)
        {
            failure = EvidenceBuildPipeHandleFactory.Combine(failure, cleanupFailure);
        }
        ExceptionDispatchInfo.Capture(failure).Throw();
        throw new InvalidOperationException("Unreachable after suspended build process adoption refusal.");
    }

    internal void Borrow(Action<EvidenceBuildSuspendedProcessBorrow> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        lock (_gate)
        {
            if (_disposeRequested || _transferred || _cleanupComplete)
            {
                throw new ObjectDisposedException(nameof(EvidenceBuildSuspendedProcessOwner));
            }
            if (_borrowActive)
            {
                throw new InvalidOperationException("Suspended build process owner does not permit a nested borrow.");
            }
            var borrow = new EvidenceBuildSuspendedProcessBorrow(
                _handles[0], _handles[1], _processId, _threadId);
            _borrowActive = true;
            try { operation(borrow); }
            finally
            {
                borrow.EndBorrow();
                _borrowActive = false;
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposeRequested) return;
            if (_borrowActive)
            {
                throw new InvalidOperationException("Suspended build process owner cannot be disposed from its borrow callback.");
            }
            _disposeRequested = true;
            AttemptCleanup(transferOnFailure: true);
        }
    }

    internal void RetryCleanupFromOwner()
    {
        lock (_gate)
        {
            if (_cleanupComplete) return;
            AttemptCleanup(transferOnFailure: false);
        }
    }

    private void AttemptCleanup(bool transferOnFailure)
    {
        Exception? failure = null;
        var process = _handles[0];
        if (process != 0)
        {
            try
            {
                if (!_api.TerminateProcess(process, CleanupExitCode, out var terminateError))
                {
                    failure = new Win32Exception(terminateError, "TerminateProcess failed for uncontained build process.");
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }

            uint wait = WaitFailed;
            try
            {
                wait = _api.WaitForSingleObject(process, CleanupWaitMilliseconds, out var waitError);
                if (wait == WaitFailed)
                {
                    failure = EvidenceBuildPipeHandleFactory.Combine(
                        failure,
                        new Win32Exception(waitError, "Waiting for uncontained build process failed."));
                }
                else if (wait != WaitObject0)
                {
                    failure = EvidenceBuildPipeHandleFactory.Combine(
                        failure,
                        wait == WaitTimeout
                            ? new TimeoutException("Uncontained build process did not exit within five seconds.")
                            : new InvalidDataException($"Unexpected uncontained build wait state 0x{wait:x8}."));
                }
            }
            catch (Exception exception)
            {
                failure = EvidenceBuildPipeHandleFactory.Combine(failure, exception);
            }

            if (failure is not null || wait != WaitObject0)
            {
                if (transferOnFailure) TransferWholeLease(failure!);
                ExceptionDispatchInfo.Capture(failure!).Throw();
            }
        }

        Exception? closeFailure = null;
        EvidenceBuildPipeHandleFactory.CloseOne(
            _handles, 1, "suspended build primary thread", _api, _kernelCleanupOwner, ref closeFailure);
        EvidenceBuildPipeHandleFactory.CloseOne(
            _handles, 0, "suspended build process", _api, _kernelCleanupOwner, ref closeFailure);
        _cleanupComplete = true;
        EvidenceBuildSuspendedProcessRetention.Release(this);
        if (closeFailure is not null) ExceptionDispatchInfo.Capture(closeFailure).Throw();
    }

    private void TransferWholeLease(Exception failure)
    {
        if (_transferred) return;
        _transferred = true;
        EvidenceBuildSuspendedProcessRetention.Retain(this);
        try { _processCleanupOwner.Retain(this, failure); }
        catch (Exception ownerFailure)
        {
            throw new AggregateException(failure, ownerFailure);
        }
    }
}
