using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Rounds.EvidenceLauncher;

[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate void Win32WinEventCallback(
    nint hook,
    uint eventType,
    nint window,
    int objectId,
    int childId,
    uint eventThreadId,
    uint eventTimeMilliseconds);

internal enum Win32MessageLoopResult
{
    Message,
    Quit,
    Failure,
}

[StructLayout(LayoutKind.Sequential)]
internal struct Win32Message
{
    internal nint Window;
    internal uint Message;
    internal nuint WParam;
    internal nint LParam;
    internal uint Time;
    internal Win32Point Point;
    internal uint Private;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Win32Point
{
    internal int X;
    internal int Y;
}

internal interface IWin32ForegroundObserverApi : IWin32KernelHandleCloser
{
    uint GetCurrentThreadId();

    bool EnsureMessageQueue(out int error);

    nint InstallForegroundHook(
        uint eventMinimum,
        uint eventMaximum,
        Win32WinEventCallback callback,
        uint flags,
        out int error);

    Win32MessageLoopResult GetMessage(out Win32Message message, out int error);

    void TranslateMessage(in Win32Message message);

    void DispatchMessage(in Win32Message message);

    bool PostQuitMessage(uint threadId, out int error);

    bool UnhookWinEvent(nint hook, out int error);

    bool TryGetWindowProcessId(nint window, out uint processId, out int error);

    nint OpenProcess(uint desiredAccess, bool inheritHandle, uint processId, out int error);

    bool IsProcessInJob(nint process, nint job, out bool inJob, out int error);
}

internal interface IWin32ForegroundObserverThread : IDisposable
{
    bool Started { get; }

    void Start(ThreadStart operation);

    bool Join(TimeSpan timeout);
}

internal interface IWin32ForegroundObserverThreadFactory
{
    IWin32ForegroundObserverThread Create();
}

internal interface IWin32ForegroundObserverWaiter
{
    bool Wait(ManualResetEventSlim signal, TimeSpan timeout);
}

internal sealed class Win32ForegroundObserverWaiter : IWin32ForegroundObserverWaiter
{
    public bool Wait(ManualResetEventSlim signal, TimeSpan timeout) => signal.Wait(timeout);
}

internal sealed class Win32ForegroundObserverThreadFactory : IWin32ForegroundObserverThreadFactory
{
    public IWin32ForegroundObserverThread Create() => new Win32ForegroundObserverThread();
}

internal sealed class Win32ForegroundObserverThread : IWin32ForegroundObserverThread
{
    private Thread? _thread;

    public bool Started { get; private set; }

    public void Start(ThreadStart operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (Started || _thread is not null)
        {
            throw new InvalidOperationException("Foreground observer worker was already started.");
        }
        _thread = new Thread(operation)
        {
            IsBackground = true,
            Name = "Rounds evidence foreground observer",
        };
        _thread.Start();
        Started = true;
    }

    public bool Join(TimeSpan timeout) =>
        _thread?.Join(timeout) ?? throw new InvalidOperationException("Foreground observer worker was not started.");

    public void Dispose()
    {
        // System.Threading.Thread has no owned native handle to release. The bounded Join is the
        // ownership boundary; this lease only prevents a second start.
        _thread = null;
    }
}

internal sealed class Win32ForegroundObserverFactory(
    IWin32ForegroundObserverApi api,
    IWin32ForegroundObserverThreadFactory threadFactory,
    IWin32ForegroundObserverWaiter waiter)
{
    internal Win32ForegroundObserverLease Start(Win32JobLease job)
    {
        ArgumentNullException.ThrowIfNull(job);
        var lease = new Win32ForegroundObserverLease(
            api,
            threadFactory.Create(),
            waiter,
            job);
        lease.StartAndWaitUntilReady();
        return lease;
    }
}

internal sealed class Win32ForegroundObserverLease : IEvidenceForegroundObserverLease
{
    internal const uint EventSystemForeground = 0x0003;
    internal const uint WinEventOutOfContext = 0x0000;
    internal const int ObjectIdWindow = 0;
    internal const int ChildIdSelf = 0;
    internal const uint ProcessQueryLimitedInformation = 0x00001000;
    internal const uint WindowMessageQuit = 0x0012;
    internal static readonly TimeSpan RequiredReadinessTimeout = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan RequiredJoinTimeout = TimeSpan.FromSeconds(5);

    private readonly IWin32ForegroundObserverApi _api;
    private readonly IWin32ForegroundObserverThread _thread;
    private readonly IWin32ForegroundObserverWaiter _waiter;
    private readonly Win32JobLease _job;
    private readonly ManualResetEventSlim _ready = new(initialState: false);
    private readonly ManualResetEventSlim _workerCompleted = new(initialState: false);
    private readonly object _failureGate = new();
    private readonly object _stopGate = new();
    private readonly Queue<Win32ForegroundEvent> _callbackQueue = new();
    private readonly object _callbackGate = new();
    private readonly object _cleanupGate = new();
    private Exception? _failure;
    private bool _callbackDrainActive;
    private bool _sawJobWindow;
    private bool _readySucceeded;
    private bool _stopCompleted;
    private bool _threadDisposed;
    private bool _signalsDisposed;
    private bool _deferredCleanupRequested;
    private uint _workerThreadId;
    private nint _hook;
    private GCHandle _callbackRoot;

    internal Win32ForegroundObserverLease(
        IWin32ForegroundObserverApi api,
        IWin32ForegroundObserverThread thread,
        IWin32ForegroundObserverWaiter waiter,
        Win32JobLease job)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _thread = thread ?? throw new ArgumentNullException(nameof(thread));
        _waiter = waiter ?? throw new ArgumentNullException(nameof(waiter));
        _job = job ?? throw new ArgumentNullException(nameof(job));
    }

    internal void StartAndWaitUntilReady()
    {
        Exception? failure = null;
        try
        {
            _thread.Start(WorkerMain);
            if (!_waiter.Wait(_ready, RequiredReadinessTimeout))
            {
                throw new TimeoutException("Foreground observer hook and message loop did not become ready in five seconds.");
            }
            lock (_failureGate)
            {
                if (!_readySucceeded || _failure is not null)
                {
                    throw _failure ?? new InvalidOperationException("Foreground observer worker exited before readiness.");
                }
            }
            return;
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        StopWorkerBestEffort(ref failure);
        ReleaseOrDeferWorkerResources(ref failure);
        ExceptionDispatchInfo.Capture(failure!).Throw();
    }

    public bool StopAndReadSawJobWindow()
    {
        lock (_stopGate)
        {
            if (!_stopCompleted)
            {
                Exception? stopFailure = null;
                StopWorkerBestEffort(ref stopFailure);
                ReleaseOrDeferWorkerResources(ref stopFailure);
                lock (_failureGate)
                {
                    if (_failure is not null)
                    {
                        stopFailure = Combine(stopFailure, _failure);
                    }
                    _failure = stopFailure;
                }
                _stopCompleted = true;
            }

            Exception? failure;
            lock (_failureGate) failure = _failure;
            if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
            return Volatile.Read(ref _sawJobWindow);
        }
    }

    public void Dispose() => StopAndReadSawJobWindow();

    private void WorkerMain()
    {
        try
        {
            var workerThreadId = _api.GetCurrentThreadId();
            if (workerThreadId == 0)
            {
                throw new Win32Exception("GetCurrentThreadId returned zero for the foreground observer worker.");
            }
            Volatile.Write(ref _workerThreadId, workerThreadId);
            if (!_api.EnsureMessageQueue(out var queueError))
            {
                throw new Win32Exception(queueError, "PeekMessageW could not establish the foreground observer message queue.");
            }

            Win32WinEventCallback callback = ReceiveUnmanagedEvent;
            _callbackRoot = GCHandle.Alloc(callback, GCHandleType.Normal);
            var hook = _api.InstallForegroundHook(
                EventSystemForeground,
                EventSystemForeground,
                callback,
                WinEventOutOfContext,
                out var installError);
            if (hook is 0 or -1)
            {
                throw new Win32Exception(installError, "SetWinEventHook failed for the foreground observer.");
            }
            _hook = hook;
            lock (_failureGate) _readySucceeded = true;
            _ready.Set();

            while (true)
            {
                var result = _api.GetMessage(out var message, out var messageError);
                if (result == Win32MessageLoopResult.Message)
                {
                    _api.TranslateMessage(in message);
                    _api.DispatchMessage(in message);
                    continue;
                }
                if (result == Win32MessageLoopResult.Quit) break;
                throw new Win32Exception(messageError, "GetMessageW failed for the foreground observer worker.");
            }
        }
        catch (Exception exception)
        {
            RecordFailure(exception);
        }
        finally
        {
            DrainCallbackQueue();
            var hook = _hook;
            if (hook != 0)
            {
                try
                {
                    if (!_api.UnhookWinEvent(hook, out var unhookError))
                    {
                        throw new Win32Exception(unhookError, "UnhookWinEvent failed for the foreground observer.");
                    }
                }
                catch (Exception exception)
                {
                    RecordFailure(exception);
                }
                _hook = 0;
            }
            try
            {
                if (_callbackRoot.IsAllocated) _callbackRoot.Free();
            }
            catch (Exception exception)
            {
                RecordFailure(exception);
            }
            _ready.Set();
            _workerCompleted.Set();
            CompleteDeferredCleanupIfRequested();
        }
    }

    private void ReceiveUnmanagedEvent(
        nint hook,
        uint eventType,
        nint window,
        int objectId,
        int childId,
        uint eventThreadId,
        uint eventTimeMilliseconds)
    {
        try
        {
            if (hook != _hook)
            {
                throw new InvalidOperationException("Foreground callback hook identity did not match its retained hook.");
            }
            if (eventType != EventSystemForeground || window == 0 ||
                objectId != ObjectIdWindow || childId != ChildIdSelf)
            {
                throw new InvalidOperationException("Foreground callback parameters did not match the exact WinEvent contract.");
            }

            lock (_callbackGate)
            {
                _callbackQueue.Enqueue(new Win32ForegroundEvent(window));
                if (_callbackDrainActive) return;
                _callbackDrainActive = true;
            }
            DrainCallbackQueue();
        }
        catch (Exception exception)
        {
            RecordFailure(exception);
        }
    }

    private void DrainCallbackQueue()
    {
        while (true)
        {
            Win32ForegroundEvent next;
            lock (_callbackGate)
            {
                if (_callbackQueue.Count == 0)
                {
                    _callbackDrainActive = false;
                    return;
                }
                next = _callbackQueue.Dequeue();
            }

            try
            {
                Classify(next.Window);
            }
            catch (Exception exception)
            {
                RecordFailure(exception);
            }
        }
    }

    private void Classify(nint window)
    {
        if (!_api.TryGetWindowProcessId(window, out var processId, out var processIdError))
        {
            throw new Win32Exception(processIdError, "GetWindowThreadProcessId failed for a foreground window.");
        }
        if (processId == 0)
        {
            throw new InvalidOperationException("GetWindowThreadProcessId returned a zero process identity.");
        }

        var process = _api.OpenProcess(
            ProcessQueryLimitedInformation,
            inheritHandle: false,
            processId,
            out var openError);
        if (process is 0 or -1)
        {
            throw new Win32Exception(openError, "OpenProcess failed for a foreground window owner.");
        }

        Exception? failure = null;
        try
        {
            var inJob = _job.BorrowHandle(jobHandle =>
            {
                if (!_api.IsProcessInJob(process, jobHandle, out var result, out var membershipError))
                {
                    throw new Win32Exception(membershipError, "IsProcessInJob failed for a foreground window owner.");
                }
                return result;
            });
            if (inJob) Volatile.Write(ref _sawJobWindow, true);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            try
            {
                if (!_api.CloseKernelHandle(process))
                {
                    throw new Win32Exception("Closing a transient foreground process handle failed.");
                }
            }
            catch (Exception exception)
            {
                failure = Combine(failure, exception);
            }
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private void StopWorkerBestEffort(ref Exception? failure)
    {
        if (!_thread.Started) return;
        if (!_workerCompleted.IsSet)
        {
            var threadId = Volatile.Read(ref _workerThreadId);
            if (threadId == 0)
            {
                failure = Combine(
                    failure,
                    new InvalidOperationException("Foreground observer worker never published its native thread identity."));
            }
            else
            {
                try
                {
                    if (!_api.PostQuitMessage(threadId, out var postError))
                    {
                        throw new Win32Exception(postError, "PostThreadMessageW(WM_QUIT) failed for the foreground observer.");
                    }
                }
                catch (Exception exception)
                {
                    failure = Combine(failure, exception);
                }
            }
        }

        try
        {
            if (!_thread.Join(RequiredJoinTimeout))
            {
                throw new TimeoutException("Foreground observer worker did not terminate in five seconds.");
            }
        }
        catch (Exception exception)
        {
            failure = Combine(failure, exception);
        }

        if (!_workerCompleted.IsSet)
        {
            failure = Combine(
                failure,
                new InvalidOperationException("Foreground observer worker completion was not proven."));
        }
    }

    private void ReleaseOrDeferWorkerResources(ref Exception? failure)
    {
        lock (_cleanupGate)
        {
            if (!_thread.Started)
            {
                DisposeNeverStartedResources(ref failure);
            }
            else if (_workerCompleted.IsSet)
            {
                DisposeCompletedWorkerResources(ref failure);
            }
            else
            {
                _deferredCleanupRequested = true;
            }
        }
    }

    private void CompleteDeferredCleanupIfRequested()
    {
        Exception? failure = null;
        lock (_cleanupGate)
        {
            if (!_deferredCleanupRequested) return;
            DisposeCompletedWorkerResources(ref failure);
        }
        if (failure is not null) RecordFailure(failure);
    }

    private void DisposeCompletedWorkerResources(ref Exception? failure)
    {
        if (!_workerCompleted.IsSet)
        {
            throw new InvalidOperationException("Worker-owned resources cannot be released before completion.");
        }
        if (!_threadDisposed)
        {
            _threadDisposed = true;
            try
            {
                _thread.Dispose();
            }
            catch (Exception exception)
            {
                failure = Combine(failure, exception);
            }
        }
        if (!_signalsDisposed)
        {
            _signalsDisposed = true;
            _ready.Dispose();
            _workerCompleted.Dispose();
        }
    }

    private void DisposeNeverStartedResources(ref Exception? failure)
    {
        if (_thread.Started)
        {
            throw new InvalidOperationException("A started worker must prove completion before releasing resources.");
        }
        if (!_threadDisposed)
        {
            _threadDisposed = true;
            try
            {
                _thread.Dispose();
            }
            catch (Exception exception)
            {
                failure = Combine(failure, exception);
            }
        }
        if (!_signalsDisposed)
        {
            _signalsDisposed = true;
            _ready.Dispose();
            _workerCompleted.Dispose();
        }
    }

    private void RecordFailure(Exception exception)
    {
        lock (_failureGate) _failure = Combine(_failure, exception);
    }

    private static Exception Combine(Exception? first, Exception second) =>
        first is null ? second : new AggregateException(first, second);

    private readonly record struct Win32ForegroundEvent(nint Window);
}

internal sealed class Win32ForegroundObserverApi : IWin32ForegroundObserverApi
{
    public uint GetCurrentThreadId() => Win32ForegroundObserverNativeMethods.GetCurrentThreadId();

    public bool EnsureMessageQueue(out int error)
    {
        _ = Win32ForegroundObserverNativeMethods.PeekMessageW(out _, 0, 0, 0, 0);
        error = 0;
        return true;
    }

    public nint InstallForegroundHook(
        uint eventMinimum,
        uint eventMaximum,
        Win32WinEventCallback callback,
        uint flags,
        out int error)
    {
        var hook = Win32ForegroundObserverNativeMethods.SetWinEventHook(
            eventMinimum,
            eventMaximum,
            0,
            callback,
            0,
            0,
            flags);
        error = hook != 0 ? 0 : Marshal.GetLastPInvokeError();
        return hook;
    }

    public Win32MessageLoopResult GetMessage(out Win32Message message, out int error)
    {
        var result = Win32ForegroundObserverNativeMethods.GetMessageW(out message, 0, 0, 0);
        error = result >= 0 ? 0 : Marshal.GetLastPInvokeError();
        return result switch
        {
            > 0 => Win32MessageLoopResult.Message,
            0 => Win32MessageLoopResult.Quit,
            _ => Win32MessageLoopResult.Failure,
        };
    }

    public void TranslateMessage(in Win32Message message)
    {
        var mutable = message;
        _ = Win32ForegroundObserverNativeMethods.TranslateMessage(ref mutable);
    }

    public void DispatchMessage(in Win32Message message)
    {
        var mutable = message;
        _ = Win32ForegroundObserverNativeMethods.DispatchMessageW(ref mutable);
    }

    public bool PostQuitMessage(uint threadId, out int error)
    {
        var success = Win32ForegroundObserverNativeMethods.PostThreadMessageW(
            threadId,
            Win32ForegroundObserverLease.WindowMessageQuit,
            0,
            0);
        error = success ? 0 : Marshal.GetLastPInvokeError();
        return success;
    }

    public bool UnhookWinEvent(nint hook, out int error)
    {
        var success = Win32ForegroundObserverNativeMethods.UnhookWinEvent(hook);
        error = success ? 0 : Marshal.GetLastPInvokeError();
        return success;
    }

    public bool TryGetWindowProcessId(nint window, out uint processId, out int error)
    {
        Marshal.SetLastPInvokeError(0);
        var threadId = Win32ForegroundObserverNativeMethods.GetWindowThreadProcessId(window, out processId);
        error = threadId != 0 ? 0 : Marshal.GetLastPInvokeError();
        return threadId != 0;
    }

    public nint OpenProcess(uint desiredAccess, bool inheritHandle, uint processId, out int error)
    {
        var process = Win32ForegroundObserverNativeMethods.OpenProcess(
            desiredAccess,
            inheritHandle,
            processId);
        error = process != 0 ? 0 : Marshal.GetLastPInvokeError();
        return process;
    }

    public bool IsProcessInJob(nint process, nint job, out bool inJob, out int error)
    {
        var success = Win32ForegroundObserverNativeMethods.IsProcessInJob(process, job, out inJob);
        error = success ? 0 : Marshal.GetLastPInvokeError();
        return success;
    }

    public bool CloseKernelHandle(nint handle) =>
        Win32EvidenceNativeMethods.CloseHandle(handle);
}

internal static class Win32ForegroundObserverNativeMethods
{
    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint SetWinEventHook(
        uint eventMinimum,
        uint eventMaximum,
        nint hookModule,
        Win32WinEventCallback callback,
        uint processId,
        uint threadId,
        uint flags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnhookWinEvent(nint hook);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern int GetMessageW(
        out Win32Message message,
        nint window,
        uint messageFilterMinimum,
        uint messageFilterMaximum);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PeekMessageW(
        out Win32Message message,
        nint window,
        uint messageFilterMinimum,
        uint messageFilterMaximum,
        uint removeMessage);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TranslateMessage(ref Win32Message message);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern nint DispatchMessageW(ref Win32Message message);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PostThreadMessageW(
        uint threadId,
        uint message,
        nuint wParam,
        nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern uint GetWindowThreadProcessId(nint window, out uint processId);

    [DllImport("kernel32.dll")]
    internal static extern uint GetCurrentThreadId();

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern nint OpenProcess(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsProcessInJob(
        nint process,
        nint job,
        [MarshalAs(UnmanagedType.Bool)] out bool result);
}
