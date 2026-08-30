using System.Collections.Immutable;
using Rounds.EvidenceLauncher;

namespace Rounds.Sim.Tests;

public sealed class Win32EvidenceBuildSuspendedOwnershipTests
{
    [Fact]
    public void ExecutableBorrowFreezesIdentityAndAncestorsAndSerializesCallbacks()
    {
        var api = new FakeApi();
        var ancestors = MutableAncestors();
        var ancestorHandles = new List<nint> { 101, 102, 103 };
        using var lease = ExecutableLease(api, ancestors, ancestorHandles);
        ancestors.Clear();
        ancestorHandles.Clear();
        var calls = new List<int>();
        EvidenceBuildExecutableBorrow? escapedBorrow = null;

        lease.Borrow(borrow =>
        {
            escapedBorrow = borrow;
            calls.Add(1);
            Assert.Equal((nint)100, borrow.Handle);
            Assert.Equal(ValidIdentity(), borrow.Identity);
            Assert.Equal(3, borrow.Continuity.Ancestors.Length);
            Assert.True(borrow.Continuity.ExactPath);
            Assert.True(borrow.Continuity.ReparseFree);
            Assert.True(borrow.Continuity.RenameDeleteExcluded);
        });
        Assert.Throws<IOException>(() => lease.Borrow(_ => throw new IOException("callback")));
        lease.Borrow(_ => calls.Add(2));

        Assert.Equal([1, 2], calls);
        Assert.Throws<ObjectDisposedException>(() => _ = escapedBorrow!.Handle);
    }

    [Fact]
    public async Task ExecutableDisposeBlocksBehindBorrowThenClosesAncestorsInReverse()
    {
        var api = new FakeApi();
        var lease = ExecutableLease(api);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var borrow = Task.Run(() => lease.Borrow(_ => { entered.Set(); release.Wait(); }));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
        var dispose = Task.Run(lease.Dispose);

        await Task.Delay(TimeSpan.FromMilliseconds(50));
        Assert.False(dispose.IsCompleted);
        Assert.Empty(api.CloseCalls);
        release.Set();
        await Task.WhenAll(borrow, dispose);

        Assert.Equal(new nint[] { 103, 102, 101, 100 }, api.CloseCalls);
        Assert.Throws<ObjectDisposedException>(() => lease.Borrow(_ => { }));
        lease.Dispose();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExecutableAmbiguousCloseUsesStaticFallbackBeforeInjectedOwner(bool throws)
    {
        var api = new FakeApi();
        api.CloseFailures.Add(101);
        var owner = new FakeKernelOwner { ThrowAfterRetain = throws };
        var lease = ExecutableLease(api, cleanupOwner: owner);

        var failure = Record.Exception(lease.Dispose);

        Assert.NotNull(failure);
        Assert.True(owner.SawStaticFirst);
        Assert.Contains(owner.Retained, item => item.Handle == 101);
        lease.Dispose();
        Assert.Equal(1, api.CloseCalls.Count(handle => handle == 101));
    }

    [Fact]
    public void ExecutableValidationFailureOwnsAndClosesEveryDistinctInputHandle()
    {
        var api = new FakeApi();
        api.CloseFailures.Add(102);
        var ancestors = MutableAncestors();
        ancestors[1] = ancestors[1] with { Path = "C:\\replacement" };

        var failure = Assert.Throws<AggregateException>(() => ExecutableLease(api, ancestors));

        Assert.Contains(failure.Flatten().InnerExceptions, exception => exception is InvalidDataException);
        Assert.True(EvidenceBuildProcessExitHandleRetention.Contains(102));
        Assert.Equal(new nint[] { 103, 102, 101, 100 }, api.CloseCalls);
    }

    [Fact]
    public void PipeCreateBorrowRevalidatesFactsAndClosesChildCopiesAfterMarkedCallback()
    {
        var api = new FakeApi();
        using var pipes = PipeBundle(api);
        var events = api.Events;
        EvidenceBuildPipeCreateBorrow? escapedBorrow = null;

        pipes.BorrowForCreate(borrow =>
        {
            escapedBorrow = borrow;
            events.Add("callback");
            Assert.Equal(new nint[] { 10, 21, 31 }, borrow.ChildHandles);
            Assert.Equal(new nint[] { 10, 20, 21, 30, 31 }, borrow.AllHandles);
            Assert.DoesNotContain(events, item => item.StartsWith("close:", StringComparison.Ordinal));
            borrow.MarkSuccessfulCreate();
        });

        Assert.Equal(10, api.GetInfoCalls);
        Assert.Equal(10, api.GetTypeCalls);
        Assert.True(events.IndexOf("callback") < events.IndexOf("close:10"));
        Assert.Equal(new nint[] { 10, 21, 31 }, api.CloseCalls);
        Assert.Throws<ObjectDisposedException>(() => _ = escapedBorrow!.AllHandles);
        Assert.Throws<InvalidOperationException>(() => pipes.BorrowForCreate(_ => { }));
    }

    [Fact]
    public void PipeBorrowThrowDoesNotTransitionAndSecondBorrowCanSucceed()
    {
        var api = new FakeApi();
        using var pipes = PipeBundle(api);

        Assert.Throws<IOException>(() => pipes.BorrowForCreate(_ => throw new IOException("create callback")));
        Assert.Empty(api.CloseCalls);
        pipes.BorrowForCreate(borrow => borrow.MarkSuccessfulCreate());
        Assert.Equal(new nint[] { 10, 21, 31 }, api.CloseCalls);
    }

    [Fact]
    public void PipeBorrowRefusesFactDriftBeforeCallback()
    {
        var api = new FakeApi();
        using var pipes = PipeBundle(api);
        api.FlagOverrides[20] = 1;
        var callbackCalled = false;

        Assert.Throws<InvalidDataException>(() => pipes.BorrowForCreate(_ => callbackCalled = true));

        Assert.False(callbackCalled);
        Assert.Empty(api.CloseCalls);
    }

    [Fact]
    public async Task PipeDisposeBlocksBehindCreateBorrowAndDisposeFirstRefuses()
    {
        var api = new FakeApi();
        var pipes = PipeBundle(api);
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var borrowTask = Task.Run(() => pipes.BorrowForCreate(_ => { entered.Set(); release.Wait(); }));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
        var disposeTask = Task.Run(pipes.Dispose);
        await Task.Delay(TimeSpan.FromMilliseconds(50));
        Assert.False(disposeTask.IsCompleted);
        release.Set();
        await Task.WhenAll(borrowTask, disposeTask);

        Assert.Throws<ObjectDisposedException>(() => pipes.BorrowForCreate(_ => { }));
    }

    [Fact]
    public void SameThreadNestedBorrowAndDisposeAreRefusedWithoutBreakingLaterCleanup()
    {
        var executableApi = new FakeApi();
        var executable = ExecutableLease(executableApi);
        executable.Borrow(_ =>
        {
            Assert.Throws<InvalidOperationException>(() => executable.Borrow(_ => { }));
            Assert.Throws<InvalidOperationException>(executable.Dispose);
        });
        executable.Dispose();

        var pipeApi = new FakeApi();
        var pipes = PipeBundle(pipeApi);
        pipes.BorrowForCreate(_ =>
        {
            Assert.Throws<InvalidOperationException>(() => pipes.BorrowForCreate(_ => { }));
            Assert.Throws<InvalidOperationException>(pipes.Dispose);
        });
        pipes.Dispose();

        var processApi = new FakeApi();
        var process = Adopt(processApi, Raw(true, 200, 201, 300, 301));
        process.Borrow(_ =>
        {
            Assert.Throws<InvalidOperationException>(() => process.Borrow(_ => { }));
            Assert.Throws<InvalidOperationException>(process.Dispose);
        });
        process.Dispose();
    }

    [Fact]
    public void CompleteSuspendedOwnerBorrowsExactFactsAndDisposesInSafetyOrder()
    {
        var api = new FakeApi();
        var owner = Adopt(api, Raw(success: true, process: 200, thread: 201, pid: 300, tid: 301));
        EvidenceBuildSuspendedProcessBorrow? escapedBorrow = null;

        owner.Borrow(borrow =>
        {
            escapedBorrow = borrow;
            Assert.Equal((nint)200, borrow.ProcessHandle);
            Assert.Equal((nint)201, borrow.ThreadHandle);
            Assert.Equal((uint)300, borrow.ProcessId);
            Assert.Equal((uint)301, borrow.ThreadId);
            Assert.True(borrow.PreJobArmed);
            Assert.True(borrow.PreResumeArmed);
        });
        owner.Dispose();
        owner.Dispose();

        Assert.Equal(
            ["terminate:200:e0350001", "wait:200:5000", "close:201", "close:200"],
            api.Events.Where(item => item.StartsWith("terminate:", StringComparison.Ordinal) ||
                                     item.StartsWith("wait:200", StringComparison.Ordinal) ||
                                     item is "close:201" or "close:200"));
        Assert.Throws<ObjectDisposedException>(() => owner.Borrow(_ => { }));
        Assert.Throws<ObjectDisposedException>(() => _ = escapedBorrow!.ProcessHandle);
    }

    [Theory]
    [InlineData(false, 200, 0, 300, 0, 1, "200")]
    [InlineData(false, 0, 201, 0, 301, 0, "201")]
    [InlineData(false, 0, 0, 0, 0, 0, "")]
    [InlineData(true, 100, 201, 300, 301, 0, "201")]
    [InlineData(true, 200, 21, 300, 301, 1, "200")]
    [InlineData(true, 200, 200, 300, 301, 1, "200")]
    [InlineData(true, 200, 201, 0, 301, 1, "201,200")]
    public void PartialAliasAndIdentifierCartesianOwnsOnlyGenuinelyNewHandles(
        bool success,
        int process,
        int thread,
        uint pid,
        uint tid,
        int expectedTerminate,
        string expectedClosed)
    {
        var api = new FakeApi();

        Assert.ThrowsAny<Exception>(() => Adopt(
            api,
            Raw(success, process, thread, pid, tid)));

        Assert.Equal(expectedTerminate, api.TerminateCalls);
        var expected = string.IsNullOrEmpty(expectedClosed)
            ? Array.Empty<nint>()
            : expectedClosed.Split(',').Select(nint.Parse).ToArray();
        Assert.Equal(expected, api.CloseCalls.Where(handle => handle is 200 or 201));
        Assert.DoesNotContain(api.CloseCalls, handle => handle is 10 or 20 or 21 or 30 or 31 or 100);
        Assert.Equal(api.CloseCalls.Distinct().Count(), api.CloseCalls.Count);
    }

    [Fact]
    public void PostAdoptionSeamThrowStillTerminatesWaitsAndCloses()
    {
        var api = new FakeApi();

        var failure = Assert.Throws<IOException>(() => Adopt(
            api,
            Raw(true, 200, 201, 300, 301),
            _ => throw new IOException("post adoption")));

        Assert.Equal("post adoption", failure.Message);
        Assert.Equal(1, api.TerminateCalls);
        Assert.Equal(new nint[] { 201, 200 }, api.CloseCalls);
    }

    [Theory]
    [InlineData("terminate-false")]
    [InlineData("terminate-throw")]
    [InlineData("wait-timeout")]
    [InlineData("wait-failed")]
    [InlineData("wait-throw")]
    [InlineData("wait-unexpected")]
    public void UnterminatedCleanupFailuresTransferWholeLeaseWithoutClosing(string failureMode)
    {
        var api = new FakeApi { ProcessFailure = failureMode };
        var cleanup = new FakeProcessOwner();
        var owner = Adopt(api, Raw(true, 200, 201, 300, 301), processCleanup: cleanup);

        Assert.ThrowsAny<Exception>(owner.Dispose);

        Assert.True(EvidenceBuildSuspendedProcessRetention.Contains(owner));
        Assert.Single(cleanup.Retained);
        Assert.True(cleanup.SawStaticFirst);
        Assert.Empty(api.CloseCalls.Where(handle => handle is 200 or 201));
        owner.Dispose();
        Assert.Single(cleanup.Retained);
    }

    [Fact]
    public void CleanupOwnerSchedulingFailureAggregatesAfterStaticRetentionAndNeverDoubleTransfers()
    {
        var api = new FakeApi { ProcessFailure = "wait-timeout" };
        var cleanup = new FakeProcessOwner { ThrowAfterRetain = true };
        var owner = Adopt(api, Raw(true, 200, 201, 300, 301), processCleanup: cleanup);

        var failure = Assert.Throws<AggregateException>(owner.Dispose).Flatten();
        owner.Dispose();

        Assert.Equal(2, failure.InnerExceptions.Count);
        Assert.True(cleanup.SawStaticFirst);
        Assert.True(EvidenceBuildSuspendedProcessRetention.Contains(owner));
        Assert.Single(cleanup.Retained);
        Assert.Empty(api.CloseCalls.Where(handle => handle is 200 or 201));
    }

    [Fact]
    public void RetainedOwnerCanRetryAtLeaseLevelAndReleaseAfterProvenExit()
    {
        var api = new FakeApi { ProcessFailure = "wait-timeout" };
        var cleanup = new FakeProcessOwner();
        var owner = Adopt(api, Raw(true, 200, 201, 300, 301), processCleanup: cleanup);
        Assert.Throws<TimeoutException>(owner.Dispose);
        api.ProcessFailure = null;

        owner.RetryCleanupFromOwner();
        owner.RetryCleanupFromOwner();

        Assert.False(EvidenceBuildSuspendedProcessRetention.Contains(owner));
        Assert.Equal(2, api.TerminateCalls);
        Assert.Equal(2, api.WaitCalls);
        Assert.Equal(new nint[] { 201, 200 }, api.CloseCalls);
    }

    [Fact]
    public async Task ProcessDisposeBlocksBehindBorrow()
    {
        var api = new FakeApi();
        var owner = Adopt(api, Raw(true, 200, 201, 300, 301));
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var borrow = Task.Run(() => owner.Borrow(_ => { entered.Set(); release.Wait(); }));
        Assert.True(entered.Wait(TimeSpan.FromSeconds(2)));
        var dispose = Task.Run(owner.Dispose);

        await Task.Delay(TimeSpan.FromMilliseconds(50));
        Assert.False(dispose.IsCompleted);
        Assert.Equal(0, api.TerminateCalls);
        release.Set();
        await Task.WhenAll(borrow, dispose);
        Assert.Equal(1, api.TerminateCalls);
    }

    [Fact]
    public void ProvenExitCloseAmbiguityUsesC1StaticRetentionWithoutRawRetry()
    {
        var api = new FakeApi();
        api.CloseFailures.Add(201);
        var kernel = new FakeKernelOwner { ThrowAfterRetain = true };
        var owner = Adopt(api, Raw(true, 200, 201, 300, 301), kernelCleanup: kernel);

        Assert.Throws<AggregateException>(owner.Dispose);
        owner.Dispose();

        Assert.True(kernel.SawStaticFirst);
        Assert.True(EvidenceBuildProcessExitHandleRetention.Contains(201));
        Assert.Equal(1, api.CloseCalls.Count(handle => handle == 201));
        Assert.Equal(1, api.CloseCalls.Count(handle => handle == 200));
    }

    private static EvidenceBuildSuspendedProcessOwner Adopt(
        FakeApi api,
        EvidenceBuildRawSuspendedProcessResult raw,
        Action<EvidenceBuildSuspendedProcessOwner>? postAdoption = null,
        FakeKernelOwner? kernelCleanup = null,
        FakeProcessOwner? processCleanup = null) =>
        EvidenceBuildSuspendedProcessOwner.Adopt(
            api,
            kernelCleanup ?? new FakeKernelOwner(),
            processCleanup ?? new FakeProcessOwner(),
            raw,
            ExecutableBorrow(),
            PipeBorrow(),
            postAdoption);

    private static EvidenceBuildRawSuspendedProcessResult Raw(
        bool success,
        int process,
        int thread,
        uint pid,
        uint tid) => new(success, process, thread, pid, tid, success ? 0 : 5);

    private static EvidenceBuildExecutableBorrow ExecutableBorrow() => new(
        100,
        ValidIdentity(),
        new EvidenceBuildExecutableContinuityProof(
            ValidIdentity().Path,
            [new EvidenceBuildExecutableAncestorIdentity("C:\\repo", "ancestor", true, true, true)],
            true,
            true,
            true));

    private static EvidenceBuildPipeCreateBorrow PipeBorrow() => new(
        [10, 20, 21, 30, 31],
        [10, 21, 31]);

    private static EvidenceBuildRetainedExecutableLease ExecutableLease(
        FakeApi api,
        List<EvidenceBuildExecutableAncestorIdentity>? ancestors = null,
        List<nint>? handles = null,
        FakeKernelOwner? cleanupOwner = null) => new(
            api,
            cleanupOwner ?? new FakeKernelOwner(),
            100,
            ValidIdentity(),
            handles ?? [101, 102, 103],
            ancestors ?? MutableAncestors());

    private static List<EvidenceBuildExecutableAncestorIdentity> MutableAncestors() =>
    [
        new("C:\\", "volume:1:file:1", true, true, true),
        new("C:\\repo", "volume:1:file:2", true, true, true),
        new("C:\\repo\\tools", "volume:1:file:3", true, true, true),
    ];

    private static EvidenceOpenedExecutableIdentity ValidIdentity() => new(
        "C:\\repo\\tools\\MSBuild.exe",
        true,
        true,
        false,
        "volume:1:file:3",
        new string('a', 64),
        "17.14.0.0",
        "17.14.0");

    private static EvidenceBuildPipeHandleBundle PipeBundle(FakeApi api) =>
        new EvidenceBuildPipeHandleFactory(api, new FakeKernelOwner()).Create();

    private sealed class FakeKernelOwner : IEvidenceBuildKernelHandleCleanupOwner
    {
        internal List<EvidenceBuildAmbiguousKernelHandle> Retained { get; } = [];
        internal bool ThrowAfterRetain { get; init; }
        internal bool SawStaticFirst { get; private set; }

        public void Retain(EvidenceBuildAmbiguousKernelHandle handle, Exception failure)
        {
            SawStaticFirst = EvidenceBuildProcessExitHandleRetention.Contains(handle);
            Retained.Add(handle);
            if (ThrowAfterRetain) throw new IOException("kernel cleanup scheduler");
        }
    }

    private sealed class FakeProcessOwner : IEvidenceBuildSuspendedProcessCleanupOwner
    {
        internal List<EvidenceBuildSuspendedProcessOwner> Retained { get; } = [];
        internal bool ThrowAfterRetain { get; init; }
        internal bool SawStaticFirst { get; private set; }

        public void Retain(EvidenceBuildSuspendedProcessOwner owner, Exception failure)
        {
            SawStaticFirst = EvidenceBuildSuspendedProcessRetention.Contains(owner);
            Retained.Add(owner);
            if (ThrowAfterRetain) throw new IOException("process cleanup scheduler");
        }
    }

    private sealed class FakeApi : IEvidenceBuildPipeHandleApi, IEvidenceBuildSuspendedProcessApi
    {
        private int _pipe;
        private readonly Dictionary<nint, uint> _flags = [];
        internal List<string> Events { get; } = [];
        internal List<nint> CloseCalls { get; } = [];
        internal HashSet<nint> CloseFailures { get; } = [];
        internal Dictionary<nint, uint> FlagOverrides { get; } = [];
        internal string? ProcessFailure { get; set; }
        internal int GetInfoCalls { get; private set; }
        internal int GetTypeCalls { get; private set; }
        internal int TerminateCalls { get; private set; }
        internal int WaitCalls { get; private set; }

        public nint OpenFile(string path, uint access, uint share, ref EvidenceBuildSecurityAttributes security,
            uint disposition, uint attributes, out int error)
        {
            _flags[10] = 1;
            error = 0;
            return 10;
        }

        public bool CreatePipe(out nint readHandle, out nint writeHandle,
            ref EvidenceBuildSecurityAttributes security, uint size, out int error)
        {
            _pipe++;
            readHandle = _pipe == 1 ? 20 : 30;
            writeHandle = _pipe == 1 ? 21 : 31;
            _flags[readHandle] = 1;
            _flags[writeHandle] = 1;
            error = 0;
            return true;
        }

        public bool SetHandleInformation(nint handle, uint mask, uint flags, out int error)
        {
            _flags[handle] = flags;
            error = 0;
            return true;
        }

        public bool GetHandleInformation(nint handle, out uint flags, out int error)
        {
            GetInfoCalls++;
            flags = FlagOverrides.GetValueOrDefault(handle, _flags[handle]);
            error = 0;
            return true;
        }

        public uint GetFileType(nint handle, out int error)
        {
            GetTypeCalls++;
            error = 0;
            return handle == 10 ? 2u : 3u;
        }

        public bool CloseHandle(nint handle, out int error)
        {
            Events.Add($"close:{handle}");
            CloseCalls.Add(handle);
            error = CloseFailures.Contains(handle) ? 6 : 0;
            return error == 0;
        }

        public bool TerminateProcess(nint process, uint exitCode, out int error)
        {
            Events.Add($"terminate:{process}:{exitCode:x8}");
            TerminateCalls++;
            if (ProcessFailure == "terminate-throw") throw new IOException("terminate throw");
            error = ProcessFailure == "terminate-false" ? 5 : 0;
            return error == 0;
        }

        public uint WaitForSingleObject(nint process, uint milliseconds, out int error)
        {
            Events.Add($"wait:{process}:{milliseconds}");
            WaitCalls++;
            if (ProcessFailure == "wait-throw") throw new IOException("wait throw");
            error = ProcessFailure == "wait-failed" ? 6 : 0;
            return ProcessFailure switch
            {
                "wait-timeout" => 258,
                "wait-failed" => 0xffffffff,
                "wait-unexpected" => 7,
                _ => 0,
            };
        }

        public bool PeekPipe(nint handle, out uint available, out int error) =>
            throw new NotSupportedException();

        public bool ReadFile(nint handle, byte[] buffer, out uint read, out int error) =>
            throw new NotSupportedException();
    }
}
