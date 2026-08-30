using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Rounds.EvidenceLauncher;

[StructLayout(LayoutKind.Sequential)]
internal struct EvidenceBuildSecurityAttributes
{
    internal uint Length;
    internal nint SecurityDescriptor;
    [MarshalAs(UnmanagedType.Bool)] internal bool InheritHandle;
}

internal static class EvidenceBuildHandlePolicy
{
    internal const uint GenericRead = 0x80000000;
    internal const uint FileShareRead = 0x00000001;
    internal const uint FileShareWrite = 0x00000002;
    internal const uint OpenExisting = 3;
    internal const uint FileAttributeNormal = 0x00000080;
    internal const uint HandleFlagInherit = 0x00000001;
    internal const uint FileTypeChar = 0x0002;
    internal const uint FileTypePipe = 0x0003;
    internal const int ErrorBrokenPipe = 109;
    internal const int ErrorNoData = 232;
    internal const int MaximumReadBytes = 64 * 1024;
    internal const string NullDevice = "NUL";
}

internal interface IEvidenceBuildKernelHandleApi
{
    bool CloseHandle(nint handle, out int error);
}

internal interface IEvidenceBuildPipeHandleApi : IEvidenceBuildKernelHandleApi
{
    nint OpenFile(
        string path,
        uint access,
        uint share,
        ref EvidenceBuildSecurityAttributes security,
        uint disposition,
        uint attributes,
        out int error);

    bool CreatePipe(
        out nint readHandle,
        out nint writeHandle,
        ref EvidenceBuildSecurityAttributes security,
        uint size,
        out int error);

    bool SetHandleInformation(nint handle, uint mask, uint flags, out int error);
    bool GetHandleInformation(nint handle, out uint flags, out int error);
    uint GetFileType(nint handle, out int error);
    bool PeekPipe(nint handle, out uint available, out int error);
    bool ReadFile(nint handle, byte[] buffer, out uint read, out int error);
}

internal sealed record EvidenceBuildAmbiguousKernelHandle(
    nint Handle,
    string Identity,
    int CloseError);

internal interface IEvidenceBuildKernelHandleCleanupOwner
{
    void Retain(EvidenceBuildAmbiguousKernelHandle handle, Exception failure);
}

internal sealed class EvidenceBuildKernelHandleCleanupOwner : IEvidenceBuildKernelHandleCleanupOwner
{
    internal static EvidenceBuildKernelHandleCleanupOwner Instance { get; } = new();
    private static readonly ConcurrentQueue<(EvidenceBuildAmbiguousKernelHandle Handle, Exception Failure)>
        RetainedUntilProcessExit = new();

    private EvidenceBuildKernelHandleCleanupOwner() { }

    public void Retain(EvidenceBuildAmbiguousKernelHandle handle, Exception failure)
    {
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(failure);
        RetainedUntilProcessExit.Enqueue((handle, failure));
    }
}

internal sealed class EvidenceBuildPipeHandleFactory(
    IEvidenceBuildPipeHandleApi api,
    IEvidenceBuildKernelHandleCleanupOwner? cleanupOwner = null)
{
    private readonly IEvidenceBuildPipeHandleApi _api = api ?? throw new ArgumentNullException(nameof(api));
    private readonly IEvidenceBuildKernelHandleCleanupOwner _cleanupOwner =
        cleanupOwner ?? EvidenceBuildKernelHandleCleanupOwner.Instance;

    internal EvidenceBuildPipeHandleBundle Create()
    {
        var handles = new nint[5];
        Exception? failure = null;
        try
        {
            var security = InheritableSecurity();
            handles[0] = RequireNewHandle(
                _api.OpenFile(
                    EvidenceBuildHandlePolicy.NullDevice,
                    EvidenceBuildHandlePolicy.GenericRead,
                    EvidenceBuildHandlePolicy.FileShareRead | EvidenceBuildHandlePolicy.FileShareWrite,
                    ref security,
                    EvidenceBuildHandlePolicy.OpenExisting,
                    EvidenceBuildHandlePolicy.FileAttributeNormal,
                    out var openError),
                handles,
                0,
                openError,
                "build stdin NUL");

            security = InheritableSecurity();
            if (!_api.CreatePipe(out var stdoutRead, out var stdoutWrite, ref security, 0, out var stdoutError))
            {
                throw new Win32Exception(stdoutError, "CreatePipe failed for build stdout.");
            }
            RecordPipePair(stdoutRead, stdoutWrite, handles, 1, 2, "build stdout");
            if (!_api.SetHandleInformation(
                    handles[1],
                    EvidenceBuildHandlePolicy.HandleFlagInherit,
                    0,
                    out var stdoutClearError))
            {
                throw new Win32Exception(stdoutClearError, "Clearing stdout-reader inheritance failed.");
            }

            security = InheritableSecurity();
            if (!_api.CreatePipe(out var stderrRead, out var stderrWrite, ref security, 0, out var stderrError))
            {
                throw new Win32Exception(stderrError, "CreatePipe failed for build stderr.");
            }
            RecordPipePair(stderrRead, stderrWrite, handles, 3, 4, "build stderr");
            if (!_api.SetHandleInformation(
                    handles[3],
                    EvidenceBuildHandlePolicy.HandleFlagInherit,
                    0,
                    out var stderrClearError))
            {
                throw new Win32Exception(stderrClearError, "Clearing stderr-reader inheritance failed.");
            }

            ValidateHandleFacts(handles);
            return new EvidenceBuildPipeHandleBundle(_api, _cleanupOwner, handles);
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        CloseReverse(handles, _api, _cleanupOwner, ref failure);
        ExceptionDispatchInfo.Capture(failure!).Throw();
        throw new InvalidOperationException("Unreachable after build-pipe acquisition failure.");
    }

    private void ValidateHandleFacts(nint[] handles)
    {
        var expectedInheritance = new[] { true, false, true, false, true };
        var expectedTypes = new[]
        {
            EvidenceBuildHandlePolicy.FileTypeChar,
            EvidenceBuildHandlePolicy.FileTypePipe,
            EvidenceBuildHandlePolicy.FileTypePipe,
            EvidenceBuildHandlePolicy.FileTypePipe,
            EvidenceBuildHandlePolicy.FileTypePipe,
        };
        for (var index = 0; index < handles.Length; index++)
        {
            if (!_api.GetHandleInformation(handles[index], out var flags, out var infoError))
            {
                throw new Win32Exception(infoError, $"GetHandleInformation failed for build handle {index}.");
            }
            var expectedFlags = expectedInheritance[index]
                ? EvidenceBuildHandlePolicy.HandleFlagInherit
                : 0u;
            if (flags != expectedFlags)
            {
                throw new InvalidDataException($"Build handle {index} inheritance flags drifted.");
            }
            var type = _api.GetFileType(handles[index], out var typeError);
            if (type != expectedTypes[index])
            {
                throw new Win32Exception(typeError, $"Build handle {index} file type drifted.");
            }
        }
    }

    private static EvidenceBuildSecurityAttributes InheritableSecurity() => new()
    {
        Length = checked((uint)Marshal.SizeOf<EvidenceBuildSecurityAttributes>()),
        SecurityDescriptor = 0,
        InheritHandle = true,
    };

    private static nint RequireNewHandle(
        nint handle,
        nint[] owned,
        int insertionIndex,
        int error,
        string identity)
    {
        if (handle is 0 or -1)
        {
            throw new Win32Exception(error, $"Acquiring {identity} returned an invalid handle.");
        }
        for (var index = 0; index < insertionIndex; index++)
        {
            if (owned[index] == handle)
            {
                throw new InvalidDataException($"Acquiring {identity} returned a duplicate handle identity.");
            }
        }
        return handle;
    }

    private static void RecordPipePair(
        nint readHandle,
        nint writeHandle,
        nint[] owned,
        int readIndex,
        int writeIndex,
        string identity)
    {
        if (readHandle is not 0 and not -1) owned[readIndex] = readHandle;
        if (writeHandle is not 0 and not -1 && writeHandle != readHandle) owned[writeIndex] = writeHandle;
        if (readHandle is 0 or -1 || writeHandle is 0 or -1)
        {
            throw new InvalidDataException($"{identity} returned an invalid handle.");
        }
        for (var index = 0; index < readIndex; index++)
        {
            if (owned[index] == readHandle || owned[index] == writeHandle)
            {
                if (owned[index] == readHandle) owned[readIndex] = 0;
                if (owned[index] == writeHandle) owned[writeIndex] = 0;
                throw new InvalidDataException($"{identity} returned a duplicate prior handle identity.");
            }
        }
        if (readHandle == writeHandle)
        {
            throw new InvalidDataException($"{identity} reader and writer identities were equal.");
        }
    }

    private static void CloseReverse(
        nint[] handles,
        IEvidenceBuildKernelHandleApi api,
        IEvidenceBuildKernelHandleCleanupOwner owner,
        ref Exception? failure)
    {
        for (var index = handles.Length - 1; index >= 0; index--)
        {
            CloseOne(handles, index, $"build pipe handle {index}", api, owner, ref failure);
        }
    }

    internal static void CloseOne(
        nint[] handles,
        int index,
        string identity,
        IEvidenceBuildKernelHandleApi api,
        IEvidenceBuildKernelHandleCleanupOwner owner,
        ref Exception? failure)
    {
        var handle = handles[index];
        if (handle == 0) return;
        handles[index] = 0;
        int closeError;
        try
        {
            if (api.CloseHandle(handle, out closeError)) return;
        }
        catch (Exception exception)
        {
            closeError = 0;
            var thrownClose = new InvalidOperationException(
                $"CloseHandle threw for {identity}; ownership is ambiguous.",
                exception);
            var thrownRetained = new EvidenceBuildAmbiguousKernelHandle(handle, identity, closeError);
            Exception? thrownRetainFailure = null;
            try { owner.Retain(thrownRetained, thrownClose); }
            catch (Exception retainException) { thrownRetainFailure = retainException; }
            failure = Combine(failure, thrownClose);
            if (thrownRetainFailure is not null) failure = Combine(failure, thrownRetainFailure);
            return;
        }

        var closeFailure = new Win32Exception(closeError, $"CloseHandle failed for {identity}; ownership is ambiguous.");
        var retained = new EvidenceBuildAmbiguousKernelHandle(handle, identity, closeError);
        Exception? retainFailure = null;
        try { owner.Retain(retained, closeFailure); }
        catch (Exception exception) { retainFailure = exception; }
        failure = Combine(failure, closeFailure);
        if (retainFailure is not null) failure = Combine(failure, retainFailure);
    }

    internal static Exception Combine(Exception? first, Exception second) =>
        first is null ? second : new AggregateException(first, second);
}

internal sealed class EvidenceBuildPipeHandleBundle : IDisposable
{
    internal static readonly EvidenceBuildRawSource StandardOutputSource =
        EvidenceBuildRawSource.Create("build-stdout-handle");
    internal static readonly EvidenceBuildRawSource StandardErrorSource =
        EvidenceBuildRawSource.Create("build-stderr-handle");

    private readonly object _gate = new();
    private readonly IEvidenceBuildPipeHandleApi _api;
    private readonly IEvidenceBuildKernelHandleCleanupOwner _cleanupOwner;
    private readonly nint[] _handles;
    private bool _childEndsTransitioned;
    private bool _disposed;

    internal EvidenceBuildPipeHandleBundle(
        IEvidenceBuildPipeHandleApi api,
        IEvidenceBuildKernelHandleCleanupOwner cleanupOwner,
        nint[] handles)
    {
        _api = api;
        _cleanupOwner = cleanupOwner;
        _handles = handles;
    }

    internal ImmutableArray<nint> ChildHandleAllowlist
    {
        get
        {
            lock (_gate)
            {
                ThrowIfDisposed();
                if (_childEndsTransitioned)
                {
                    throw new InvalidOperationException("Build child-handle allowlist is unavailable after transfer.");
                }
                return ImmutableArray.Create(_handles[0], _handles[2], _handles[4]);
            }
        }
    }

    internal IEvidenceBuildRawReadApi CreateReadApi() => new EvidenceBuildPipeRawReadApi(this);

    internal void CloseParentChildEndsAfterSuccessfulProcessCreation()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_childEndsTransitioned)
            {
                throw new InvalidOperationException("Build child-end transfer milestone already occurred.");
            }
            _childEndsTransitioned = true;
            Exception? failure = null;
            EvidenceBuildPipeHandleFactory.CloseOne(
                _handles, 0, "parent build stdin copy", _api, _cleanupOwner, ref failure);
            EvidenceBuildPipeHandleFactory.CloseOne(
                _handles, 2, "parent build stdout-writer copy", _api, _cleanupOwner, ref failure);
            EvidenceBuildPipeHandleFactory.CloseOne(
                _handles, 4, "parent build stderr-writer copy", _api, _cleanupOwner, ref failure);
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    internal EvidenceBuildRawRead Poll(EvidenceBuildRawSource source, int maximumBytes)
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            var handle = source == StandardOutputSource
                ? _handles[1]
                : source == StandardErrorSource
                    ? _handles[3]
                    : throw new InvalidOperationException("Build raw source was not owned by this pipe bundle.");
            if (handle == 0) throw new InvalidOperationException("Build reader handle ownership was unavailable.");
            return EvidenceBuildPipeRawReadApi.PollRetained(_api, handle, maximumBytes);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            Exception? failure = null;
            for (var index = _handles.Length - 1; index >= 0; index--)
            {
                EvidenceBuildPipeHandleFactory.CloseOne(
                    _handles, index, $"build pipe handle {index}", _api, _cleanupOwner, ref failure);
            }
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);
}

internal sealed class EvidenceBuildPipeRawReadApi(EvidenceBuildPipeHandleBundle bundle) : IEvidenceBuildRawReadApi
{
    private readonly EvidenceBuildPipeHandleBundle _bundle =
        bundle ?? throw new ArgumentNullException(nameof(bundle));

    public EvidenceBuildRawRead Poll(EvidenceBuildRawSource source, int maximumBytes) =>
        _bundle.Poll(source, maximumBytes);

    internal static EvidenceBuildRawRead PollRetained(
        IEvidenceBuildPipeHandleApi api,
        nint handle,
        int maximumBytes)
    {
        if (maximumBytes is <= 0 or > EvidenceBuildHandlePolicy.MaximumReadBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        }
        if (!api.PeekPipe(handle, out var available, out var peekError))
        {
            return FailureOrNoProgress(peekError, "PeekNamedPipe");
        }
        if (available == 0) return EvidenceBuildRawRead.NoProgress();

        var requested = checked((int)Math.Min(available, checked((uint)maximumBytes)));
        var bytes = new byte[requested];
        if (!api.ReadFile(handle, bytes, out var read, out var readError))
        {
            return FailureOrNoProgress(readError, "ReadFile");
        }
        if (read == 0) return EvidenceBuildRawRead.NoProgress();
        if (read > bytes.Length)
        {
            throw new InvalidDataException("ReadFile reported more build bytes than the bounded buffer.");
        }
        if (read != bytes.Length) Array.Resize(ref bytes, checked((int)read));
        return EvidenceBuildRawRead.Bytes(bytes);
    }

    private static EvidenceBuildRawRead FailureOrNoProgress(int error, string operation) => error switch
    {
        EvidenceBuildHandlePolicy.ErrorBrokenPipe => EvidenceBuildRawRead.EndOfFile(),
        EvidenceBuildHandlePolicy.ErrorNoData => EvidenceBuildRawRead.NoProgress(),
        _ => throw new Win32Exception(error, $"{operation} failed for a build output pipe."),
    };
}

internal interface IEvidenceBuildProcessCompletionApi : IEvidenceBuildKernelHandleApi
{
    uint WaitForSingleObject(nint process, uint milliseconds, out int error);
    bool GetExitCodeProcess(nint process, out uint exitCode, out int error);
}

internal sealed record EvidenceBuildProcessCompletion(
    bool Signaled,
    bool TimedOut,
    uint? ExitCode)
{
    internal bool Successful => Signaled && ExitCode == 0;
}

internal sealed class EvidenceBuildProcessCompletionLease : IDisposable
{
    internal const uint WaitObject0 = 0;
    internal const uint WaitTimeout = 258;
    internal const uint WaitFailed = 0xffffffff;
    internal const uint StillActiveExitCode = 259;

    private readonly object _gate = new();
    private readonly IEvidenceBuildProcessCompletionApi _api;
    private readonly IEvidenceBuildKernelHandleCleanupOwner _cleanupOwner;
    private readonly EvidenceBuildRunDeadline _deadline;
    private nint _process;
    private EvidenceBuildProcessCompletion? _completion;
    private bool _disposed;

    internal EvidenceBuildProcessCompletionLease(
        IEvidenceBuildProcessCompletionApi api,
        IEvidenceBuildKernelHandleCleanupOwner cleanupOwner,
        nint retainedProcess,
        EvidenceBuildRunDeadline deadline)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _cleanupOwner = cleanupOwner ?? throw new ArgumentNullException(nameof(cleanupOwner));
        _deadline = deadline ?? throw new ArgumentNullException(nameof(deadline));
        if (retainedProcess is 0 or -1) throw new ArgumentOutOfRangeException(nameof(retainedProcess));
        _process = retainedProcess;
    }

    internal EvidenceBuildProcessCompletion WaitForCompletion()
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_completion is not null) return _completion;
            var observation = _deadline.Observe();
            if (observation.Expired)
            {
                return _completion = new EvidenceBuildProcessCompletion(false, true, null);
            }
            var milliseconds = CeilingMilliseconds(observation.Remaining);
            var wait = _api.WaitForSingleObject(_process, milliseconds, out var waitError);
            switch (wait)
            {
                case WaitObject0:
                    if (!_api.GetExitCodeProcess(_process, out var exitCode, out var exitError))
                    {
                        throw new Win32Exception(exitError, "GetExitCodeProcess failed for the build process.");
                    }
                    return _completion = new EvidenceBuildProcessCompletion(true, false, exitCode);
                case WaitTimeout:
                    return _completion = new EvidenceBuildProcessCompletion(false, true, null);
                case WaitFailed:
                    throw new Win32Exception(waitError, "WaitForSingleObject failed for the build process.");
                default:
                    throw new InvalidDataException($"Unexpected build-process wait state 0x{wait:x8}.");
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            var handles = new[] { _process };
            _process = 0;
            Exception? failure = null;
            EvidenceBuildPipeHandleFactory.CloseOne(
                handles, 0, "retained build process", _api, _cleanupOwner, ref failure);
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }

    private static uint CeilingMilliseconds(TimeSpan remaining)
    {
        var ticks = remaining.Ticks;
        if (ticks <= 0) return 0;
        var milliseconds = checked((ticks + TimeSpan.TicksPerMillisecond - 1) / TimeSpan.TicksPerMillisecond);
        return checked((uint)milliseconds);
    }
}

internal sealed class Win32EvidenceBuildHandleApi : IEvidenceBuildPipeHandleApi, IEvidenceBuildProcessCompletionApi
{
    public nint OpenFile(string path, uint access, uint share, ref EvidenceBuildSecurityAttributes security,
        uint disposition, uint attributes, out int error)
    {
        var handle = Native.CreateFileW(path, access, share, ref security, disposition, attributes, 0);
        error = handle == -1 ? Marshal.GetLastPInvokeError() : 0;
        return handle;
    }

    public bool CreatePipe(out nint readHandle, out nint writeHandle,
        ref EvidenceBuildSecurityAttributes security, uint size, out int error)
    {
        var success = Native.CreatePipe(out readHandle, out writeHandle, ref security, size);
        error = success ? 0 : Marshal.GetLastPInvokeError();
        return success;
    }

    public bool SetHandleInformation(nint handle, uint mask, uint flags, out int error)
    {
        var success = Native.SetHandleInformation(handle, mask, flags);
        error = success ? 0 : Marshal.GetLastPInvokeError();
        return success;
    }

    public bool GetHandleInformation(nint handle, out uint flags, out int error)
    {
        var success = Native.GetHandleInformation(handle, out flags);
        error = success ? 0 : Marshal.GetLastPInvokeError();
        return success;
    }

    public uint GetFileType(nint handle, out int error)
    {
        var type = Native.GetFileType(handle);
        error = type == 0 ? Marshal.GetLastPInvokeError() : 0;
        return type;
    }

    public bool PeekPipe(nint handle, out uint available, out int error)
    {
        var success = Native.PeekNamedPipe(handle, 0, 0, out _, out available, out _);
        error = success ? 0 : Marshal.GetLastPInvokeError();
        return success;
    }

    public bool ReadFile(nint handle, byte[] buffer, out uint read, out int error)
    {
        var success = Native.ReadFile(handle, buffer, checked((uint)buffer.Length), out read, 0);
        error = success ? 0 : Marshal.GetLastPInvokeError();
        return success;
    }

    public uint WaitForSingleObject(nint process, uint milliseconds, out int error)
    {
        var result = Native.WaitForSingleObject(process, milliseconds);
        error = result == EvidenceBuildProcessCompletionLease.WaitFailed ? Marshal.GetLastPInvokeError() : 0;
        return result;
    }

    public bool GetExitCodeProcess(nint process, out uint exitCode, out int error)
    {
        var success = Native.GetExitCodeProcess(process, out exitCode);
        error = success ? 0 : Marshal.GetLastPInvokeError();
        return success;
    }

    public bool CloseHandle(nint handle, out int error)
    {
        var success = Native.CloseHandle(handle);
        error = success ? 0 : Marshal.GetLastPInvokeError();
        return success;
    }

    private static class Native
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern nint CreateFileW(string path, uint access, uint share,
            ref EvidenceBuildSecurityAttributes security, uint disposition, uint attributes, nint template);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CreatePipe(out nint readHandle, out nint writeHandle,
            ref EvidenceBuildSecurityAttributes security, uint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetHandleInformation(nint handle, uint mask, uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetHandleInformation(nint handle, out uint flags);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint GetFileType(nint handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool PeekNamedPipe(nint pipe, nint buffer, uint bufferSize,
            out uint bytesRead, out uint totalBytesAvailable, out uint bytesLeftThisMessage);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ReadFile(nint file, byte[] buffer, uint bytesToRead,
            out uint bytesRead, nint overlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern uint WaitForSingleObject(nint handle, uint milliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetExitCodeProcess(nint process, out uint exitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool CloseHandle(nint handle);
    }
}
