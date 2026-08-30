using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Rounds.EvidenceLauncher;

namespace Rounds.Sim.Tests;

public sealed class Win32ForegroundObserverTests
{
    [Fact]
    public void Abi_constants_and_inert_entry_are_exact()
    {
        Assert.Equal(48, Marshal.SizeOf<Win32Message>());
        Assert.Equal(0x0003U, Win32ForegroundObserverLease.EventSystemForeground);
        Assert.Equal(0U, Win32ForegroundObserverLease.WinEventOutOfContext);
        Assert.Equal(0, Win32ForegroundObserverLease.ObjectIdWindow);
        Assert.Equal(0, Win32ForegroundObserverLease.ChildIdSelf);
        Assert.Equal(0x1000U, Win32ForegroundObserverLease.ProcessQueryLimitedInformation);
        Assert.Equal(0x0012U, Win32ForegroundObserverLease.WindowMessageQuit);

        using var error = new StringWriter();
        var exit = EvidenceLauncherEntry.Run(new[] { "execute" }, 8, error);
        Assert.Equal(2, exit);
        Assert.Equal("native-boundary-not-installed\n", error.ToString());
    }

    [Fact]
    public void Start_publishes_only_after_queue_and_exact_hook_are_ready_then_unhooks_on_owner_thread()
    {
        using var rig = new Rig();

        var observer = rig.Start();
        rig.Api.MapWindow(40, 140, inJob: false);
        rig.Api.RaiseForeground(40);

        Assert.Equal(
            (0x0003U, 0x0003U, 0U),
            (rig.Api.EventMinimum, rig.Api.EventMaximum, rig.Api.HookFlags));
        Assert.Equal(Win32ForegroundObserverLease.RequiredReadinessTimeout, rig.Waiter.ObservedTimeout);
        Assert.NotEqual(Environment.CurrentManagedThreadId, rig.Api.InstallManagedThreadId);
        Assert.False(observer.StopAndReadSawJobWindow());
        Assert.Equal(rig.Api.InstallManagedThreadId, rig.Api.UnhookManagedThreadId);
        Assert.Equal(rig.Api.InstallManagedThreadId, rig.Api.TranslateManagedThreadId);
        Assert.Equal(rig.Api.InstallManagedThreadId, rig.Api.DispatchManagedThreadId);
        Assert.True(rig.Api.CallbackPresentAtUnhook);
        Assert.Equal(1, rig.Api.UnhookCount);
        Assert.Equal(1, rig.Thread.DisposeCount);
        AssertOrdered(rig.Api.Events, "thread-id", "queue", "install", "get-message", "translate", "dispatch", "unhook");

        observer.Dispose();
        Assert.Equal(1, rig.Api.UnhookCount);
        Assert.Equal(1, rig.Thread.DisposeCount);
    }

    [Fact]
    public void Unrelated_then_repeated_job_events_produce_sticky_true_and_close_every_process_handle()
    {
        using var rig = new Rig();
        rig.Api.MapWindow(41, 141, inJob: false);
        rig.Api.MapWindow(42, 142, inJob: true);
        var observer = rig.Start();

        rig.Api.RaiseForeground(41);
        rig.Api.RaiseForeground(42);
        rig.Api.RaiseForeground(42);

        Assert.True(observer.StopAndReadSawJobWindow());
        Assert.Equal(1, rig.Api.CloseCountForProcess(141));
        Assert.Equal(2, rig.Api.CloseCountForProcess(142));
        Assert.All(rig.Api.OpenCalls, call =>
        {
            Assert.Equal(Win32ForegroundObserverLease.ProcessQueryLimitedInformation, call.Access);
            Assert.False(call.Inherit);
        });
    }

    [Fact]
    public void Reentrant_callback_is_queued_and_drained_without_recursion_or_loss()
    {
        using var rig = new Rig();
        rig.Api.MapWindow(51, 151, inJob: false);
        rig.Api.MapWindow(52, 152, inJob: true);
        rig.Api.ReentrantWindow = 52;
        var observer = rig.Start();

        rig.Api.RaiseForeground(51);

        Assert.True(observer.StopAndReadSawJobWindow());
        Assert.Equal(1, rig.Api.CloseCountForProcess(151));
        Assert.Equal(1, rig.Api.CloseCountForProcess(152));
    }

    [Theory]
    [InlineData("event")]
    [InlineData("window")]
    [InlineData("object")]
    [InlineData("child")]
    [InlineData("hook")]
    public void Malformed_callback_is_captured_and_stop_fails_closed_without_callback_escape(string field)
    {
        using var rig = new Rig();
        var observer = rig.Start();

        rig.Api.RaiseMalformed(field);

        Assert.ThrowsAny<Exception>(() => observer.StopAndReadSawJobWindow());
        Assert.Empty(rig.Api.OpenCalls);
        Assert.Equal(1, rig.Api.UnhookCount);
    }

    [Theory]
    [InlineData("pid-error")]
    [InlineData("pid-disappeared")]
    [InlineData("pid-zero")]
    [InlineData("open-error")]
    [InlineData("process-disappeared")]
    [InlineData("membership-error")]
    [InlineData("pid-throw")]
    [InlineData("open-throw")]
    [InlineData("membership-throw")]
    public void Classification_failure_or_disappearance_is_retained_and_fails_closed(string failure)
    {
        using var rig = new Rig();
        rig.Api.ConfigureClassificationFailure(failure, window: 61, processId: 161);
        var observer = rig.Start();

        rig.Api.RaiseForeground(61);

        Assert.ThrowsAny<Exception>(() => observer.StopAndReadSawJobWindow());
        Assert.Equal(
            failure is "membership-error" or "membership-throw" ? 1 : 0,
            rig.Api.CloseCountForProcess(161));
    }

    [Fact]
    public void Transient_process_close_failure_is_retained_after_membership_result()
    {
        using var rig = new Rig();
        rig.Api.MapWindow(71, 171, inJob: true);
        rig.Api.FailProcessCloseFor = 171;
        var observer = rig.Start();

        rig.Api.RaiseForeground(71);

        Assert.ThrowsAny<Exception>(() => observer.StopAndReadSawJobWindow());
        Assert.Equal(1, rig.Api.CloseCountForProcess(171));
    }

    [Theory]
    [InlineData("queue")]
    [InlineData("install")]
    [InlineData("readiness")]
    [InlineData("thread-start")]
    public void Startup_failure_is_bounded_and_never_returns_a_lease(string failure)
    {
        using var rig = new Rig();
        switch (failure)
        {
            case "queue": rig.Api.FailQueue = true; break;
            case "install": rig.Api.FailInstall = true; break;
            case "readiness": rig.Waiter.ForceTimeout = true; rig.Thread.NoRun = true; break;
            case "thread-start": rig.Thread.ThrowOnStart = true; break;
        }

        Assert.ThrowsAny<Exception>(() => rig.Start());

        Assert.Equal(0, rig.Api.UnhookCount);
        if (failure == "readiness")
        {
            Assert.Equal(0, rig.Thread.DisposeCount);
        }
        else
        {
            Assert.Equal(1, rig.Thread.DisposeCount);
        }
    }

    [Theory]
    [InlineData("get-message")]
    [InlineData("translate")]
    [InlineData("dispatch")]
    [InlineData("thread-dispose")]
    public void Message_loop_and_stop_lifecycle_failures_are_retained(string failure)
    {
        using var rig = new Rig();
        if (failure == "thread-dispose") rig.Thread.ThrowOnDispose = true;
        if (failure == "translate") rig.Api.ThrowOnTranslate = true;
        if (failure == "dispatch") rig.Api.ThrowOnDispatch = true;
        var observer = rig.Start();
        if (failure == "get-message")
        {
            rig.Api.QueueMessageFailure();
        }
        else if (failure is "translate" or "dispatch")
        {
            rig.Api.MapWindow(80, 180, inJob: false);
            rig.Api.RaiseForeground(80);
        }

        Assert.ThrowsAny<Exception>(() => observer.StopAndReadSawJobWindow());

        Assert.Equal(1, rig.Api.UnhookCount);
        Assert.True(rig.Thread.LastJoinTimeout == Win32ForegroundObserverLease.RequiredJoinTimeout);
    }

    [Fact]
    public void Failed_first_post_is_retryable_by_dispose_and_retains_failure_after_exact_cleanup()
    {
        using var rig = new Rig();
        rig.Thread.JoinWithoutWaiting = true;
        rig.Api.FailPostQuit = true;
        var observer = rig.Start();
        var started = Stopwatch.StartNew();

        Assert.ThrowsAny<Exception>(() => observer.StopAndReadSawJobWindow());

        Assert.True(started.Elapsed < TimeSpan.FromSeconds(1));
        Assert.Equal(0, rig.Api.UnhookCount);
        Assert.Equal(0, rig.Thread.DisposeCount);
        Assert.True(rig.Api.CallbackIsRetained);
        Assert.True(observer.CallbackRootAllocated);
        Assert.True(observer.HookIdentityRetained);
        Assert.False(rig.Thread.Exited.IsSet);
        Assert.Equal(1, rig.Api.PostQuitCount);

        rig.Api.FailPostQuit = false;
        rig.Thread.JoinWithoutWaiting = false;
        Assert.ThrowsAny<Exception>(observer.Dispose);

        Assert.True(rig.Thread.Exited.IsSet);
        Assert.Null(rig.Thread.UnhandledFailure);
        Assert.Equal(1, rig.Api.UnhookCount);
        Assert.Equal(rig.Api.InstallManagedThreadId, rig.Api.UnhookManagedThreadId);
        Assert.Equal(1, rig.Thread.DisposeCount);
        Assert.False(rig.Api.CallbackIsRetained);
        Assert.False(observer.CallbackRootAllocated);
        Assert.False(observer.HookIdentityRetained);
        Assert.Equal(2, rig.Api.PostQuitCount);
        Assert.ThrowsAny<Exception>(() => observer.StopAndReadSawJobWindow());
        Assert.Equal(1, rig.Thread.DisposeCount);
    }

    [Fact]
    public void Repeated_post_failure_remains_retryable_and_owned_until_a_later_bounded_cleanup()
    {
        using var rig = new Rig();
        rig.Thread.JoinWithoutWaiting = true;
        rig.Api.FailPostQuit = true;
        var observer = rig.Start();

        Assert.ThrowsAny<Exception>(() => observer.StopAndReadSawJobWindow());
        Assert.ThrowsAny<Exception>(observer.Dispose);

        Assert.Equal(2, rig.Api.PostQuitCount);
        Assert.Equal(0, rig.Api.UnhookCount);
        Assert.Equal(0, rig.Thread.DisposeCount);
        Assert.True(observer.CallbackRootAllocated);
        Assert.True(observer.HookIdentityRetained);
        Assert.True(rig.Api.CallbackIsRetained);
        Assert.False(rig.Thread.Exited.IsSet);

        // Test-only teardown supplies the first successful post so no background worker leaks.
        rig.Api.FailPostQuit = false;
        rig.Thread.JoinWithoutWaiting = false;
        Assert.ThrowsAny<Exception>(() => observer.StopAndReadSawJobWindow());
        Assert.True(rig.Thread.Exited.IsSet);
        Assert.Equal(1, rig.Api.UnhookCount);
        Assert.Equal(1, rig.Thread.DisposeCount);
    }

    [Fact]
    public void Failed_unhook_retains_hook_and_root_until_a_later_join_is_actually_proven()
    {
        using var rig = new Rig();
        rig.Api.FailUnhook = true;
        rig.Thread.ReportJoinFailure = true;
        var observer = rig.Start();

        Assert.ThrowsAny<Exception>(() => observer.StopAndReadSawJobWindow());

        Assert.True(rig.Thread.Exited.IsSet);
        Assert.Equal(1, rig.Api.UnhookCount);
        Assert.True(rig.Api.CallbackIsRetained);
        Assert.True(observer.CallbackRootAllocated);
        Assert.True(observer.HookIdentityRetained);
        Assert.Equal(0, rig.Thread.DisposeCount);

        rig.Thread.ReportJoinFailure = false;
        Assert.ThrowsAny<Exception>(observer.Dispose);

        Assert.False(rig.Api.CallbackIsRetained);
        Assert.False(observer.CallbackRootAllocated);
        Assert.False(observer.HookIdentityRetained);
        Assert.Equal(1, rig.Api.UnhookCount);
        Assert.Equal(1, rig.Thread.DisposeCount);
        Assert.Null(rig.Thread.UnhandledFailure);
    }

    [Fact]
    public void Event_queued_before_stop_is_classified_before_quit_and_unhook()
    {
        using var rig = new Rig();
        rig.Api.MapWindow(81, 181, inJob: true);
        var observer = rig.Start();

        rig.Api.RaiseForeground(81);

        Assert.True(observer.StopAndReadSawJobWindow());
        AssertOrdered(rig.Api.Events, "event:81", "membership:181", "unhook");
    }

    [Fact]
    public void Idle_observer_does_not_duplicate_or_delay_kill_on_close_job_handle()
    {
        using var rig = new Rig();
        var observer = rig.Start();

        rig.Job.Dispose();

        Assert.Equal(1, rig.Api.CloseCount(303));
        Assert.False(observer.StopAndReadSawJobWindow());
        Assert.Equal(1, rig.Api.CloseCount(303));
    }

    [Fact]
    public async Task Callback_borrow_prevents_reused_job_handle_and_dispose_closes_immediately_after_borrow()
    {
        using var rig = new Rig();
        rig.Api.MapWindow(91, 191, inJob: true);
        rig.Api.BlockMembership = true;
        var observer = rig.Start();
        rig.Api.RaiseForeground(91);
        Assert.True(rig.Api.MembershipEntered.Wait(TimeSpan.FromSeconds(2)));

        var disposerEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var dispose = Task.Factory.StartNew(
            () =>
            {
                disposerEntered.SetResult();
                rig.Job.Dispose();
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        try
        {
            await disposerEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            Assert.False(dispose.IsCompleted);
            Assert.Equal(0, rig.Api.CloseCount(303));
        }
        finally
        {
            rig.Api.MembershipRelease.Set();
        }
        await dispose.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(1, rig.Api.CloseCount(303));
        Assert.True(observer.StopAndReadSawJobWindow());
        Assert.Equal(1, rig.Api.CloseCountForProcess(191));
    }

    [Fact]
    public void Callback_after_forced_job_close_closes_transient_process_and_fails_without_dereference()
    {
        using var rig = new Rig();
        rig.Api.MapWindow(101, 201, inJob: true);
        var observer = rig.Start();
        rig.Job.Dispose();

        rig.Api.RaiseForeground(101);

        Assert.ThrowsAny<Exception>(() => observer.StopAndReadSawJobWindow());
        Assert.Equal(1, rig.Api.CloseCount(303));
        Assert.Equal(1, rig.Api.CloseCountForProcess(201));
        Assert.DoesNotContain("membership:201", rig.Api.Events);
    }

    private static void AssertOrdered(IEnumerable<string> events, params string[] expected)
    {
        var all = events.ToArray();
        var previous = -1;
        foreach (var item in expected)
        {
            var next = Array.FindIndex(all, previous + 1, value => value == item);
            Assert.True(next > previous, $"Missing ordered event '{item}' in: {string.Join(", ", all)}");
            previous = next;
        }
    }

    private sealed class Rig : IDisposable
    {
        internal FakeApi Api { get; } = new();
        internal FakeThread Thread { get; }
        internal FakeWaiter Waiter { get; } = new();
        internal Win32JobLease Job { get; }

        internal Rig()
        {
            Thread = new FakeThread(Api.Events);
            Thread.OnJoinProven = Api.NotifyInstallingThreadExitProven;
            Job = new Win32JobLease(Api, 303);
        }

        internal Win32ForegroundObserverLease Start() =>
            new Win32ForegroundObserverFactory(Api, new FakeThreadFactory(Thread), Waiter).Start(Job);

        public void Dispose()
        {
            Api.MembershipRelease.Set();
            Job.Dispose();
            Api.Dispose();
        }
    }

    private sealed class FakeWaiter : IWin32ForegroundObserverWaiter
    {
        internal bool ForceTimeout { get; set; }
        internal TimeSpan ObservedTimeout { get; private set; }

        public bool Wait(ManualResetEventSlim signal, TimeSpan timeout)
        {
            ObservedTimeout = timeout;
            return !ForceTimeout && signal.Wait(TimeSpan.FromSeconds(2));
        }
    }

    private sealed class FakeThreadFactory(FakeThread thread) : IWin32ForegroundObserverThreadFactory
    {
        public IWin32ForegroundObserverThread Create() => thread;
    }

    private sealed class FakeThread(ConcurrentQueue<string> events) : IWin32ForegroundObserverThread
    {
        private Thread? _thread;

        internal bool NoRun { get; set; }
        internal bool ThrowOnStart { get; set; }
        internal bool ReportJoinFailure { get; set; }
        internal bool JoinWithoutWaiting { get; set; }
        internal bool ThrowOnDispose { get; set; }
        internal int DisposeCount { get; private set; }
        internal TimeSpan? LastJoinTimeout { get; private set; }
        internal ManualResetEventSlim Exited { get; } = new(false);
        internal Exception? UnhandledFailure { get; private set; }
        internal Action? OnJoinProven { get; set; }
        public bool Started { get; private set; }

        public void Start(ThreadStart operation)
        {
            events.Enqueue("thread-start");
            if (ThrowOnStart) throw new InvalidOperationException("injected thread start failure");
            Started = true;
            if (NoRun) return;
            _thread = new Thread(() =>
            {
                try
                {
                    operation();
                }
                catch (Exception exception)
                {
                    UnhandledFailure = exception;
                }
                finally
                {
                    Exited.Set();
                }
            }) { IsBackground = true };
            _thread.Start();
        }

        public bool Join(TimeSpan timeout)
        {
            LastJoinTimeout = timeout;
            events.Enqueue("join");
            if (JoinWithoutWaiting) return false;
            var joined = _thread?.Join(TimeSpan.FromSeconds(2)) ?? false;
            var proven = joined && !ReportJoinFailure;
            if (proven) OnJoinProven?.Invoke();
            return proven;
        }

        public void Dispose()
        {
            DisposeCount++;
            events.Enqueue("thread-dispose");
            if (ThrowOnDispose) throw new IOException("injected thread dispose failure");
        }
    }

    private sealed class FakeApi : IWin32ForegroundObserverApi, IWin32EvidenceApi, IDisposable
    {
        private const nint Hook = 901;
        private readonly BlockingCollection<LoopItem> _messages = new();
        private readonly object _gate = new();
        private readonly Dictionary<nint, uint> _windowPids = new();
        private readonly HashSet<uint> _jobPids = new();
        private readonly Dictionary<nint, uint> _processHandles = new();
        private readonly Dictionary<nint, int> _closeCounts = new();
        private Win32WinEventCallback? _callback;
        private string? _classificationFailure;
        private nint _failureWindow;
        private uint _failureProcessId;

        internal ConcurrentQueue<string> Events { get; } = new();
        internal ManualResetEventSlim MembershipEntered { get; } = new(false);
        internal ManualResetEventSlim MembershipRelease { get; } = new(false);
        internal List<(uint Access, bool Inherit, uint ProcessId)> OpenCalls { get; } = new();
        internal uint EventMinimum { get; private set; }
        internal uint EventMaximum { get; private set; }
        internal uint HookFlags { get; private set; }
        internal int InstallManagedThreadId { get; private set; }
        internal int UnhookManagedThreadId { get; private set; }
        internal bool CallbackPresentAtUnhook { get; private set; }
        internal int UnhookCount { get; private set; }
        internal bool FailQueue { get; set; }
        internal bool FailInstall { get; set; }
        internal bool FailPostQuit { get; set; }
        internal bool FailUnhook { get; set; }
        internal bool BlockMembership { get; set; }
        internal uint? FailProcessCloseFor { get; set; }
        private nint _reentrantWindow;

        internal nint ReentrantWindow
        {
            get => Volatile.Read(ref _reentrantWindow);
            set => Volatile.Write(ref _reentrantWindow, value);
        }
        internal bool ThrowOnTranslate { get; set; }
        internal bool ThrowOnDispatch { get; set; }
        internal int TranslateManagedThreadId { get; private set; }
        internal int DispatchManagedThreadId { get; private set; }
        internal bool CallbackIsRetained => _callback is not null;
        internal int PostQuitCount { get; private set; }

        internal void MapWindow(nint window, uint processId, bool inJob)
        {
            lock (_gate)
            {
                _windowPids[window] = processId;
                if (inJob) _jobPids.Add(processId);
            }
        }

        internal void ConfigureClassificationFailure(string failure, nint window, uint processId)
        {
            _classificationFailure = failure;
            _failureWindow = window;
            _failureProcessId = processId;
            MapWindow(window, processId, inJob: false);
        }

        internal void RaiseForeground(nint window) =>
            QueueCallback(Hook, Win32ForegroundObserverLease.EventSystemForeground, window, 0, 0);

        internal void RaiseMalformed(string field) => QueueCallback(
            field == "hook" ? 999 : Hook,
            field == "event" ? 4U : Win32ForegroundObserverLease.EventSystemForeground,
            field == "window" ? 0 : 111,
            field == "object" ? 1 : 0,
            field == "child" ? 1 : 0);

        internal void QueueMessageFailure() =>
            _messages.Add(new LoopItem(Win32MessageLoopResult.Failure, 5, null));

        internal void NotifyInstallingThreadExitProven()
        {
            Events.Enqueue("thread-exit-proven");
            _callback = null;
        }

        internal int CloseCount(nint handle)
        {
            lock (_gate) return _closeCounts.GetValueOrDefault(handle);
        }

        internal int CloseCountForProcess(uint processId) => CloseCount(ProcessHandle(processId));

        public uint GetCurrentThreadId()
        {
            Events.Enqueue("thread-id");
            return 77;
        }

        public bool EnsureMessageQueue(out int error)
        {
            Events.Enqueue("queue");
            error = FailQueue ? 5 : 0;
            return !FailQueue;
        }

        public nint InstallForegroundHook(
            uint eventMinimum,
            uint eventMaximum,
            Win32WinEventCallback callback,
            uint flags,
            out int error)
        {
            Events.Enqueue("install");
            InstallManagedThreadId = Environment.CurrentManagedThreadId;
            EventMinimum = eventMinimum;
            EventMaximum = eventMaximum;
            HookFlags = flags;
            _callback = callback;
            error = FailInstall ? 5 : 0;
            return FailInstall ? 0 : Hook;
        }

        public Win32MessageLoopResult GetMessage(out Win32Message message, out int error)
        {
            Events.Enqueue("get-message");
            var item = _messages.Take();
            item.Callback?.Invoke();
            message = new Win32Message { Message = 0x0400 };
            error = item.Error;
            return item.Result;
        }

        public void TranslateMessage(in Win32Message message)
        {
            Events.Enqueue("translate");
            TranslateManagedThreadId = Environment.CurrentManagedThreadId;
            if (ThrowOnTranslate) throw new IOException("injected TranslateMessage failure");
        }

        public void DispatchMessage(in Win32Message message)
        {
            Events.Enqueue("dispatch");
            DispatchManagedThreadId = Environment.CurrentManagedThreadId;
            if (ThrowOnDispatch) throw new IOException("injected DispatchMessage failure");
        }

        public bool PostQuitMessage(uint threadId, out int error)
        {
            Events.Enqueue("post-quit");
            PostQuitCount++;
            Assert.Equal(77U, threadId);
            if (!FailPostQuit)
            {
                _messages.Add(new LoopItem(Win32MessageLoopResult.Quit, 0, null));
            }
            error = FailPostQuit ? 5 : 0;
            return !FailPostQuit;
        }

        public bool UnhookWinEvent(nint hook, out int error)
        {
            Events.Enqueue("unhook");
            Assert.Equal(Hook, hook);
            UnhookManagedThreadId = Environment.CurrentManagedThreadId;
            CallbackPresentAtUnhook = _callback is not null;
            UnhookCount++;
            if (!FailUnhook) _callback = null;
            error = FailUnhook ? 5 : 0;
            return !FailUnhook;
        }

        public bool TryGetWindowProcessId(nint window, out uint processId, out int error)
        {
            Events.Enqueue($"pid:{window}");
            if (_classificationFailure == "pid-throw" && window == _failureWindow)
            {
                throw new IOException("injected PID callback failure");
            }
            if (_classificationFailure is "pid-error" or "pid-disappeared" && window == _failureWindow)
            {
                processId = 0;
                error = _classificationFailure == "pid-disappeared" ? 1400 : 5;
                return false;
            }
            lock (_gate) processId = _windowPids.GetValueOrDefault(window);
            if (_classificationFailure == "pid-zero" && window == _failureWindow) processId = 0;
            error = 0;
            return true;
        }

        public nint OpenProcess(uint desiredAccess, bool inheritHandle, uint processId, out int error)
        {
            Events.Enqueue($"open:{processId}");
            lock (_gate) OpenCalls.Add((desiredAccess, inheritHandle, processId));
            if (_classificationFailure == "open-throw" && processId == _failureProcessId)
            {
                throw new IOException("injected open callback failure");
            }
            if (_classificationFailure is "open-error" or "process-disappeared" &&
                processId == _failureProcessId)
            {
                error = _classificationFailure == "process-disappeared" ? 87 : 5;
                return 0;
            }
            var handle = ProcessHandle(processId);
            lock (_gate) _processHandles[handle] = processId;
            error = 0;
            return handle;
        }

        public bool IsProcessInJob(nint process, nint job, out bool inJob, out int error)
        {
            Assert.Equal(303, job);
            uint processId;
            lock (_gate) processId = _processHandles[process];
            Events.Enqueue($"membership:{processId}");
            MembershipEntered.Set();
            if (BlockMembership)
            {
                MembershipRelease.Wait();
            }
            var reentrant = Interlocked.Exchange(ref _reentrantWindow, 0);
            if (reentrant != 0)
            {
                _callback!(Hook, Win32ForegroundObserverLease.EventSystemForeground, reentrant, 0, 0, 0, 0);
            }
            if (_classificationFailure == "membership-throw" && processId == _failureProcessId)
            {
                throw new IOException("injected membership callback failure");
            }
            if (_classificationFailure == "membership-error" && processId == _failureProcessId)
            {
                inJob = false;
                error = 5;
                return false;
            }
            lock (_gate) inJob = _jobPids.Contains(processId);
            error = 0;
            return true;
        }

        public bool CloseKernelHandle(nint handle)
        {
            lock (_gate) _closeCounts[handle] = _closeCounts.GetValueOrDefault(handle) + 1;
            Events.Enqueue($"close:{handle}");
            uint processId;
            lock (_gate) processId = _processHandles.GetValueOrDefault(handle);
            return !FailProcessCloseFor.HasValue || processId != FailProcessCloseFor.Value;
        }

        public bool CloseDesktop(nint desktop) => true;
        public bool TerminateProcess(nint process, uint exitCode) => true;
        public uint WaitForSingleObject(nint handle, uint milliseconds) => 0;
        public bool WriteFile(nint handle, ReadOnlySpan<byte> data, out uint written)
        {
            written = checked((uint)data.Length);
            return true;
        }

        public void Dispose()
        {
            _messages.Dispose();
            MembershipEntered.Dispose();
            MembershipRelease.Dispose();
        }

        private void QueueCallback(nint hook, uint eventType, nint window, int objectId, int childId)
        {
            _messages.Add(new LoopItem(
                Win32MessageLoopResult.Message,
                0,
                () =>
                {
                    Events.Enqueue($"event:{window}");
                    _callback!(hook, eventType, window, objectId, childId, 0, 0);
                }));
        }

        private static nint ProcessHandle(uint processId) => checked((nint)(5_000 + processId));

        private sealed record LoopItem(
            Win32MessageLoopResult Result,
            int Error,
            Action? Callback);
    }
}
