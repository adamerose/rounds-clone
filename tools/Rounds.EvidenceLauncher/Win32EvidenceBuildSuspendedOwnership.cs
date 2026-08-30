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
    private readonly ImmutableArray<nint> _protectedHandles;

    internal EvidenceBuildExecutableBorrow(
        nint handle,
        EvidenceOpenedExecutableIdentity identity,
        EvidenceBuildExecutableContinuityProof continuity,
        ImmutableArray<nint> protectedHandles)
    {
        _handle = handle;
        _identity = identity;
        _continuity = continuity;
        _protectedHandles = protectedHandles;
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
    internal ImmutableArray<nint> ProtectedHandles => _active
        ? _protectedHandles
        : throw new ObjectDisposedException(nameof(EvidenceBuildExecutableBorrow));

    internal void EndBorrow() => _active = false;
}

internal interface IEvidenceBuildExecutableSnapshotAllocator
{
    nint[] AllocateHandleBuffer(int length);
}

internal sealed class EvidenceBuildExecutableSnapshotAllocator : IEvidenceBuildExecutableSnapshotAllocator
{
    internal static EvidenceBuildExecutableSnapshotAllocator Instance { get; } = new();
    private EvidenceBuildExecutableSnapshotAllocator() { }
    public nint[] AllocateHandleBuffer(int length) => new nint[length];
}

internal sealed class EvidenceBuildRetainedExecutableLease : IDisposable
{
    internal const int MaximumAncestorCount = 64;
    private readonly object _gate = new();
    private readonly IEvidenceBuildKernelHandleApi _api;
    private readonly IEvidenceBuildKernelHandleCleanupOwner _cleanupOwner;
    private readonly nint[] _ownedHandles;
    private readonly EvidenceOpenedExecutableIdentity _identity;
    private readonly EvidenceBuildExecutableContinuityProof _continuity;
    private readonly ImmutableArray<nint> _protectedHandles;
    private readonly int _ownedHandleCount;
    private bool _borrowActive;
    private bool _disposed;

    internal EvidenceBuildRetainedExecutableLease(
        IEvidenceBuildKernelHandleApi api,
        IEvidenceBuildKernelHandleCleanupOwner cleanupOwner,
        nint executableHandle,
        EvidenceOpenedExecutableIdentity identity,
        IEnumerable<nint> ancestorHandles,
        IEnumerable<EvidenceBuildExecutableAncestorIdentity> ancestors,
        IEvidenceBuildExecutableSnapshotAllocator? snapshotAllocator = null)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _cleanupOwner = cleanupOwner ?? throw new ArgumentNullException(nameof(cleanupOwner));
        snapshotAllocator ??= EvidenceBuildExecutableSnapshotAllocator.Instance;
        var executableOwner = new nint[1];
        if (executableHandle is not 0 and not -1) executableOwner[0] = executableHandle;
        nint[]? ownedHandles = null;
        var ownedHandleCount = 0;
        EvidenceOpenedExecutableIdentity frozenIdentity;
        EvidenceBuildExecutableContinuityProof frozenContinuity;
        ImmutableArray<nint> protectedHandles;
        try
        {
            ArgumentNullException.ThrowIfNull(identity);
            ArgumentNullException.ThrowIfNull(ancestorHandles);
            ArgumentNullException.ThrowIfNull(ancestors);
            if (executableOwner[0] == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(executableHandle));
            }
            if (!identity.Exists || !identity.IdentityBound || identity.IsReparsePoint ||
                !Path.IsPathFullyQualified(identity.Path) || string.IsNullOrWhiteSpace(identity.OpenedHandleIdentity))
            {
                throw new InvalidDataException("Build executable expected identity was incomplete.");
            }

            var expectedPaths = ExactAncestorPaths(identity.Path);
            if (expectedPaths.Length is 0 or > MaximumAncestorCount)
            {
                throw new InvalidDataException("Build executable exact ancestor count was outside the admitted bound.");
            }
            ownedHandles = snapshotAllocator.AllocateHandleBuffer(MaximumAncestorCount + 1);
            if (ownedHandles is null || ownedHandles.Length != MaximumAncestorCount + 1)
            {
                throw new InvalidDataException("Build executable handle snapshot allocation was not exact.");
            }
            ownedHandles[ownedHandleCount++] = executableOwner[0];
            executableOwner[0] = 0;

            using (var enumerator = ancestorHandles.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (ownedHandleCount > MaximumAncestorCount)
                    {
                        throw new InvalidDataException("Build executable ancestor handle snapshot exceeded its bound.");
                    }
                    var handle = enumerator.Current;
                    if (handle is 0 or -1 ||
                        Array.IndexOf(ownedHandles, handle, 0, ownedHandleCount) >= 0)
                    {
                        throw new InvalidDataException("Build executable/ancestor handles were invalid or duplicated.");
                    }
                    ownedHandles[ownedHandleCount++] = handle;
                }
            }
            var ancestorCount = ownedHandleCount - 1;
            if (ancestorCount != expectedPaths.Length)
            {
                throw new InvalidDataException("Build executable proof did not retain the complete ancestor handle chain.");
            }

            var frozenAncestors = ImmutableArray.CreateBuilder<EvidenceBuildExecutableAncestorIdentity>(ancestorCount);
            using var ancestorEnumerator = ancestors.GetEnumerator();
            for (var index = 0; index < ancestorCount; index++)
            {
                if (!ancestorEnumerator.MoveNext())
                {
                    throw new InvalidDataException("Build executable ancestor proof ended early.");
                }
                var ancestor = ancestorEnumerator.Current ??
                    throw new InvalidDataException("Build ancestor proof was null.");
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
            if (ancestorEnumerator.MoveNext())
            {
                throw new InvalidDataException("Build executable ancestor proof contained an extra record.");
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
            protectedHandles = ImmutableArray.Create(ownedHandles, 0, ownedHandleCount);
        }
        catch (Exception validationFailure)
        {
            Exception? failure = validationFailure;
            if (ownedHandles is not null)
            {
                for (var index = ownedHandleCount - 1; index >= 0; index--)
                {
                    EvidenceBuildPipeHandleFactory.CloseOne(
                        ownedHandles,
                        index,
                        index == 0 ? "rejected retained build executable" : $"rejected build executable ancestor {index - 1}",
                        _api,
                        _cleanupOwner,
                        ref failure);
                }
            }
            EvidenceBuildPipeHandleFactory.CloseOne(
                executableOwner, 0, "rejected retained build executable", _api, _cleanupOwner, ref failure);
            ExceptionDispatchInfo.Capture(failure!).Throw();
            throw;
        }

        _ownedHandles = ownedHandles;
        _ownedHandleCount = ownedHandleCount;
        _identity = frozenIdentity;
        _continuity = frozenContinuity;
        _protectedHandles = protectedHandles;
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
            var borrow = new EvidenceBuildExecutableBorrow(
                _ownedHandles[0], _identity, _continuity, _protectedHandles);
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
            for (var index = _ownedHandleCount - 1; index >= 0; index--)
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
        var protectedHandles = new HashSet<nint>(pipes.AllHandles);
        protectedHandles.UnionWith(executable.ProtectedHandles);
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
        Exception? terminationDiagnostic = null;
        var process = _handles[0];
        if (process != 0)
        {
            try
            {
                if (!_api.TerminateProcess(process, CleanupExitCode, out var terminateError))
                {
                    terminationDiagnostic = new Win32Exception(
                        terminateError,
                        "TerminateProcess failed for uncontained build process, but exit proof is still required.");
                }
            }
            catch (Exception exception)
            {
                terminationDiagnostic = exception;
            }

            uint wait = WaitFailed;
            Exception? waitFailure = null;
            try
            {
                wait = _api.WaitForSingleObject(process, CleanupWaitMilliseconds, out var waitError);
                if (wait == WaitFailed)
                {
                    waitFailure = new Win32Exception(waitError, "Waiting for uncontained build process failed.");
                }
                else if (wait != WaitObject0)
                {
                    waitFailure = wait == WaitTimeout
                        ? new TimeoutException("Uncontained build process did not exit within five seconds.")
                        : new InvalidDataException($"Unexpected uncontained build wait state 0x{wait:x8}.");
                }
            }
            catch (Exception exception)
            {
                waitFailure = exception;
            }

            if (wait != WaitObject0)
            {
                var unprovenFailure = terminationDiagnostic is null
                    ? waitFailure!
                    : EvidenceBuildPipeHandleFactory.Combine(terminationDiagnostic, waitFailure!);
                if (transferOnFailure) TransferWholeLease(unprovenFailure);
                ExceptionDispatchInfo.Capture(unprovenFailure).Throw();
            }
        }

        Exception? closeFailure = terminationDiagnostic;
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
