using System.ComponentModel;
using System.Runtime.InteropServices;
using Rounds.EvidenceLauncher;

namespace Rounds.Sim.Tests;

public sealed class Win32EvidenceBuildHandlesCompletionTests
{
    [Fact]
    public void SecurityAbiAndExactFiveHandleBundleFactsArePinned()
    {
        Assert.Equal(24, Marshal.SizeOf<EvidenceBuildSecurityAttributes>());
        Assert.Equal(0, Marshal.OffsetOf<EvidenceBuildSecurityAttributes>(nameof(EvidenceBuildSecurityAttributes.Length)).ToInt32());
        Assert.Equal(8, Marshal.OffsetOf<EvidenceBuildSecurityAttributes>(nameof(EvidenceBuildSecurityAttributes.SecurityDescriptor)).ToInt32());
        Assert.Equal(16, Marshal.OffsetOf<EvidenceBuildSecurityAttributes>(nameof(EvidenceBuildSecurityAttributes.InheritHandle)).ToInt32());
        var api = new FakePipeApi();
        using var bundle = new EvidenceBuildPipeHandleFactory(api, new FakeCleanupOwner()).Create();

        Assert.Equal(new nint[] { 10, 21, 31 }, bundle.ChildHandleAllowlist.ToArray());
        Assert.Equal(
            [
                "open:NUL:80000000:3:3:80:24:1",
                "pipe:0:24:1", "set:20:1:0",
                "pipe:0:24:1", "set:30:1:0",
            ],
            api.Calls.Take(5));
        Assert.Equal(5, api.GetInfoCalls);
        Assert.Equal(5, api.GetTypeCalls);
    }

    [Fact]
    public void ChildEndCopiesCloseOnlyAtExplicitMilestoneThenReadersCloseInReverse()
    {
        var api = new FakePipeApi();
        var bundle = new EvidenceBuildPipeHandleFactory(api, new FakeCleanupOwner()).Create();

        Assert.Empty(api.CloseCalls);
        _ = bundle.CreateReadApi();
        bundle.CloseParentChildEndsAfterSuccessfulProcessCreation();
        Assert.Equal(new nint[] { 10, 21, 31 }, api.CloseCalls);
        Assert.Throws<InvalidOperationException>(() => _ = bundle.ChildHandleAllowlist);
        Assert.Throws<InvalidOperationException>(bundle.CloseParentChildEndsAfterSuccessfulProcessCreation);

        bundle.Dispose();
        bundle.Dispose();
        Assert.Equal(new nint[] { 10, 21, 31, 30, 20 }, api.CloseCalls);
    }

    [Theory]
    [InlineData("open", 0)]
    [InlineData("pipe1", 1)]
    [InlineData("clear20", 3)]
    [InlineData("pipe2", 3)]
    [InlineData("clear30", 5)]
    [InlineData("info", 5)]
    [InlineData("filetype", 5)]
    public void AcquisitionFailuresCloseEveryRecordedHandleInReverse(string failure, int expectedCloseCount)
    {
        var api = new FakePipeApi { Failure = failure };

        Assert.ThrowsAny<Exception>(() =>
            new EvidenceBuildPipeHandleFactory(api, new FakeCleanupOwner()).Create());

        Assert.Equal(expectedCloseCount, api.CloseCalls.Count);
        Assert.Equal(api.CloseCalls.Distinct().Count(), api.CloseCalls.Count);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("duplicate")]
    [InlineData("inherit")]
    [InlineData("type")]
    public void InvalidDuplicateInheritanceAndTypeAnomaliesFailClosed(string anomaly)
    {
        var api = new FakePipeApi { Anomaly = anomaly };

        Assert.ThrowsAny<Exception>(() =>
            new EvidenceBuildPipeHandleFactory(api, new FakeCleanupOwner()).Create());

        Assert.Equal(api.CloseCalls.Distinct().Count(), api.CloseCalls.Count);
    }

    [Fact]
    public void AmbiguousMilestoneClosesTransferStronglyAndNeverRetriesRawHandle()
    {
        var api = new FakePipeApi();
        api.CloseFailures.Add(21);
        var owner = new FakeCleanupOwner();
        var bundle = new EvidenceBuildPipeHandleFactory(api, owner).Create();

        var failure = Assert.Throws<Win32Exception>(bundle.CloseParentChildEndsAfterSuccessfulProcessCreation);
        Assert.Contains("ambiguous", failure.Message, StringComparison.Ordinal);
        Assert.Single(owner.Retained);
        Assert.Equal((nint)21, owner.Retained[0].Handle);
        Assert.Equal(new nint[] { 10, 21, 31 }, api.CloseCalls);

        bundle.Dispose();
        bundle.Dispose();
        Assert.Equal(1, api.CloseCalls.Count(handle => handle == 21));
        Assert.Equal(new nint[] { 10, 21, 31, 30, 20 }, api.CloseCalls);
    }

    [Fact]
    public void ReverseDisposeAggregatesEveryCloseAmbiguityAndTransfersBeforeThrow()
    {
        var api = new FakePipeApi();
        api.CloseFailures.UnionWith([31, 20]);
        var owner = new FakeCleanupOwner();
        var bundle = new EvidenceBuildPipeHandleFactory(api, owner).Create();

        var failure = Assert.Throws<AggregateException>(bundle.Dispose).Flatten();

        Assert.Equal(2, failure.InnerExceptions.Count);
        Assert.Equal(new nint[] { 31, 20 }, owner.Retained.Select(item => item.Handle));
        Assert.Equal(new nint[] { 31, 30, 21, 20, 10 }, api.CloseCalls);
        bundle.Dispose();
        Assert.Equal(5, api.CloseCalls.Count);
    }

    [Theory]
    [InlineData(true, 109, 2)]
    [InlineData(true, 232, 0)]
    [InlineData(false, 109, 2)]
    [InlineData(false, 232, 0)]
    public void BrokenPipeIsOnlyExplicitEofAndNoDataIsNoProgress(
        bool failPeek,
        int error,
        int expectedRawKind)
    {
        var api = new FakePipeApi
        {
            PeekSuccess = !failPeek,
            PeekAvailable = failPeek ? 0u : 4u,
            PeekError = failPeek ? error : 0,
            ReadSuccess = failPeek,
            ReadError = failPeek ? 0 : error,
        };
        using var bundle = new EvidenceBuildPipeHandleFactory(api, new FakeCleanupOwner()).Create();

        var read = bundle.CreateReadApi().Poll(EvidenceBuildPipeHandleBundle.StandardOutputSource, 16);

        Assert.Equal((EvidenceBuildRawReadKind)expectedRawKind, read.Kind);
        Assert.Empty(read.Data);
    }

    [Fact]
    public void SuccessfulZeroByteReadIsNoProgressAndBoundedPartialReadReturnsExactBytes()
    {
        var api = new FakePipeApi { PeekAvailable = 10, ReadCount = 0 };
        using var bundle = new EvidenceBuildPipeHandleFactory(api, new FakeCleanupOwner()).Create();
        var reads = bundle.CreateReadApi();

        var zero = reads.Poll(EvidenceBuildPipeHandleBundle.StandardOutputSource, 4);
        Assert.Equal(EvidenceBuildRawReadKind.NoProgress, zero.Kind);
        Assert.Equal(4, api.LastReadBufferLength);

        api.ReadCount = 3;
        api.ReadFill = 0x5a;
        var data = reads.Poll(EvidenceBuildPipeHandleBundle.StandardErrorSource, 4);
        Assert.Equal(EvidenceBuildRawReadKind.Data, data.Kind);
        Assert.Equal(new byte[] { 0x5a, 0x5a, 0x5a }, data.Data);
        Assert.Equal(4, api.LastReadBufferLength);
    }

    [Fact]
    public void SuccessfulPeekWithNoAvailableBytesDoesNotAllocateOrRead()
    {
        var api = new FakePipeApi { PeekAvailable = 0, ReadCount = 99 };
        using var bundle = new EvidenceBuildPipeHandleFactory(api, new FakeCleanupOwner()).Create();

        var result = bundle.CreateReadApi().Poll(
            EvidenceBuildPipeHandleBundle.StandardOutputSource,
            EvidenceBuildHandlePolicy.MaximumReadBytes);

        Assert.Equal(EvidenceBuildRawReadKind.NoProgress, result.Kind);
        Assert.DoesNotContain(api.Calls, call => call.StartsWith("read:", StringComparison.Ordinal));
        Assert.Equal(0, api.LastReadBufferLength);
    }

    [Fact]
    public void HugeReportedAvailabilityStillAllocatesOnlyTheExactRequestedMaximum()
    {
        var api = new FakePipeApi { PeekAvailable = uint.MaxValue, ReadCount = 0 };
        using var bundle = new EvidenceBuildPipeHandleFactory(api, new FakeCleanupOwner()).Create();

        var result = bundle.CreateReadApi().Poll(
            EvidenceBuildPipeHandleBundle.StandardOutputSource,
            EvidenceBuildHandlePolicy.MaximumReadBytes);

        Assert.Equal(EvidenceBuildRawReadKind.NoProgress, result.Kind);
        Assert.Equal(EvidenceBuildHandlePolicy.MaximumReadBytes, api.LastReadBufferLength);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65537)]
    public void InvalidReadMaximumRefusesBeforePeekOrAllocation(int maximum)
    {
        var api = new FakePipeApi();
        using var bundle = new EvidenceBuildPipeHandleFactory(api, new FakeCleanupOwner()).Create();
        var callsBefore = api.Calls.Count;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            bundle.CreateReadApi().Poll(EvidenceBuildPipeHandleBundle.StandardOutputSource, maximum));

        Assert.Equal(callsBefore, api.Calls.Count);
    }

    [Fact]
    public void UnexpectedReadFaultAndOversizedNativeCountFailClosed()
    {
        var api = new FakePipeApi { PeekAvailable = 4, ReadSuccess = false, ReadError = 5 };
        using var bundle = new EvidenceBuildPipeHandleFactory(api, new FakeCleanupOwner()).Create();
        var reads = bundle.CreateReadApi();
        Assert.Throws<Win32Exception>(() =>
            reads.Poll(EvidenceBuildPipeHandleBundle.StandardOutputSource, 4));

        api.ReadSuccess = true;
        api.ReadCount = 5;
        Assert.Throws<InvalidDataException>(() =>
            reads.Poll(EvidenceBuildPipeHandleBundle.StandardOutputSource, 4));
    }

    [Theory]
    [InlineData(0u, true)]
    [InlineData(1u, false)]
    [InlineData(259u, false)]
    public void SignaledProcessQueriesExitOnceAndTreats259AsAnExitValue(uint exitCode, bool success)
    {
        var events = new List<string>();
        var clock = new FakeClock(events, TimeSpan.Zero);
        var api = new FakeCompletionApi(events) { WaitResult = 0, ExitCode = exitCode };
        using var lease = Completion(api, clock);

        var result = lease.WaitForCompletion();
        var repeated = lease.WaitForCompletion();

        Assert.Same(result, repeated);
        Assert.True(result.Signaled);
        Assert.False(result.TimedOut);
        Assert.Equal(exitCode, result.ExitCode);
        Assert.Equal(success, result.Successful);
        Assert.Equal(["origin", "elapsed", "wait:300000", "exit"], events);
    }

    [Fact]
    public void ExpiredSharedDeadlineDoesNotWaitOrQueryExit()
    {
        var events = new List<string>();
        var clock = new FakeClock(events, TimeSpan.FromMinutes(5));
        var api = new FakeCompletionApi(events);
        using var lease = Completion(api, clock);

        var result = lease.WaitForCompletion();

        Assert.True(result.TimedOut);
        Assert.False(result.Signaled);
        Assert.Null(result.ExitCode);
        Assert.Equal(["origin", "elapsed"], events);
    }

    [Fact]
    public void WaitTimeoutUsesCeilingOfSharedRemainingTimeAndNeverQueriesExit()
    {
        var events = new List<string>();
        var elapsed = TimeSpan.FromMinutes(5) - TimeSpan.FromTicks(10_001);
        var clock = new FakeClock(events, elapsed);
        var api = new FakeCompletionApi(events) { WaitResult = 258 };
        using var lease = Completion(api, clock);

        var result = lease.WaitForCompletion();

        Assert.True(result.TimedOut);
        Assert.Equal(["origin", "elapsed", "wait:2"], events);
    }

    [Theory]
    [InlineData(0xffffffffu, true, false)]
    [InlineData(7u, false, false)]
    [InlineData(0u, false, true)]
    public void WaitFailureUnexpectedStateAndExitQueryFailureRefuse(
        uint waitResult,
        bool waitFails,
        bool exitFails)
    {
        var events = new List<string>();
        var api = new FakeCompletionApi(events)
        {
            WaitResult = waitResult,
            WaitError = waitFails ? 6 : 0,
            ExitSuccess = !exitFails,
            ExitError = exitFails ? 5 : 0,
        };
        using var lease = Completion(api, new FakeClock(events, TimeSpan.Zero));

        Assert.ThrowsAny<Exception>(lease.WaitForCompletion);
        Assert.Equal(waitResult == 0 ? 1 : 0, api.ExitCalls);
    }

    [Fact]
    public void SharedDeadlineRollbackRefusesBeforeASecondWait()
    {
        var events = new List<string>();
        var clock = new FakeClock(events, TimeSpan.Zero);
        clock.ElapsedScript.Enqueue(TimeSpan.FromSeconds(2));
        clock.ElapsedScript.Enqueue(TimeSpan.FromSeconds(1));
        var firstApi = new FakeCompletionApi(events) { WaitResult = 0xffffffff, WaitError = 5 };
        using var lease = Completion(firstApi, clock);
        Assert.Throws<Win32Exception>(lease.WaitForCompletion);

        Assert.Throws<InvalidOperationException>(lease.WaitForCompletion);
        Assert.Equal(1, firstApi.WaitCalls);
    }

    [Fact]
    public void ProcessCloseAmbiguityTransfersOnceAndDoubleDisposeNeverRetriesRawHandle()
    {
        var events = new List<string>();
        var api = new FakeCompletionApi(events) { CloseSuccess = false, CloseError = 6 };
        var owner = new FakeCleanupOwner();
        var deadline = EvidenceBuildRunDeadline.Arm(new FakeClock(events, TimeSpan.Zero), TimeSpan.FromMinutes(5));
        var lease = new EvidenceBuildProcessCompletionLease(api, owner, 90, deadline);

        Assert.Throws<Win32Exception>(lease.Dispose);
        lease.Dispose();

        Assert.Equal(1, api.CloseCalls);
        Assert.Single(owner.Retained);
        Assert.Equal((nint)90, owner.Retained[0].Handle);
    }

    private static EvidenceBuildProcessCompletionLease Completion(
        FakeCompletionApi api,
        FakeClock clock) =>
        new(
            api,
            new FakeCleanupOwner(),
            90,
            EvidenceBuildRunDeadline.Arm(clock, TimeSpan.FromMinutes(5)));

    private sealed class FakeCleanupOwner : IEvidenceBuildKernelHandleCleanupOwner
    {
        internal List<EvidenceBuildAmbiguousKernelHandle> Retained { get; } = [];

        public void Retain(EvidenceBuildAmbiguousKernelHandle handle, Exception failure)
        {
            Assert.NotNull(failure);
            Retained.Add(handle);
        }
    }

    private sealed class FakePipeApi : IEvidenceBuildPipeHandleApi
    {
        private int _pipeOrdinal;
        internal string? Failure { get; init; }
        internal string? Anomaly { get; init; }
        internal List<string> Calls { get; } = [];
        internal List<nint> CloseCalls { get; } = [];
        internal HashSet<nint> CloseFailures { get; } = [];
        internal int GetInfoCalls { get; private set; }
        internal int GetTypeCalls { get; private set; }
        internal bool PeekSuccess { get; set; } = true;
        internal uint PeekAvailable { get; set; }
        internal int PeekError { get; set; }
        internal bool ReadSuccess { get; set; } = true;
        internal uint ReadCount { get; set; }
        internal int ReadError { get; set; }
        internal byte ReadFill { get; set; }
        internal int LastReadBufferLength { get; private set; }
        private readonly Dictionary<nint, uint> _flags = [];

        public nint OpenFile(string path, uint access, uint share,
            ref EvidenceBuildSecurityAttributes security, uint disposition, uint attributes, out int error)
        {
            Calls.Add($"open:{path}:{access:x}:{share:x}:{disposition}:{attributes:x}:{security.Length}:{(security.InheritHandle ? 1 : 0)}");
            error = Failure == "open" ? 5 : 0;
            if (Failure == "open") return -1;
            _flags[10] = 1;
            return 10;
        }

        public bool CreatePipe(out nint readHandle, out nint writeHandle,
            ref EvidenceBuildSecurityAttributes security, uint size, out int error)
        {
            _pipeOrdinal++;
            Calls.Add($"pipe:{size}:{security.Length}:{(security.InheritHandle ? 1 : 0)}");
            var failureName = $"pipe{_pipeOrdinal}";
            if (Failure == failureName)
            {
                readHandle = 0;
                writeHandle = 0;
                error = 8;
                return false;
            }
            readHandle = _pipeOrdinal == 1 ? 20 : 30;
            writeHandle = _pipeOrdinal == 1 ? 21 : 31;
            if (Anomaly == "invalid" && _pipeOrdinal == 1) readHandle = 0;
            if (Anomaly == "duplicate" && _pipeOrdinal == 1) writeHandle = readHandle;
            if (readHandle != 0) _flags[readHandle] = 1;
            if (writeHandle != 0) _flags[writeHandle] = 1;
            error = 0;
            return true;
        }

        public bool SetHandleInformation(nint handle, uint mask, uint flags, out int error)
        {
            Calls.Add($"set:{handle}:{mask}:{flags}");
            if (Failure == $"clear{handle}")
            {
                error = 5;
                return false;
            }
            _flags[handle] = flags;
            error = 0;
            return true;
        }

        public bool GetHandleInformation(nint handle, out uint flags, out int error)
        {
            GetInfoCalls++;
            if (Failure == "info")
            {
                flags = 0;
                error = 6;
                return false;
            }
            flags = _flags[handle];
            if (Anomaly == "inherit" && handle == 20) flags = 1;
            error = 0;
            return true;
        }

        public uint GetFileType(nint handle, out int error)
        {
            GetTypeCalls++;
            if (Failure == "filetype")
            {
                error = 6;
                return 0;
            }
            error = 0;
            if (Anomaly == "type" && handle == 30) return EvidenceBuildHandlePolicy.FileTypeChar;
            return handle == 10 ? EvidenceBuildHandlePolicy.FileTypeChar : EvidenceBuildHandlePolicy.FileTypePipe;
        }

        public bool CloseHandle(nint handle, out int error)
        {
            CloseCalls.Add(handle);
            error = CloseFailures.Contains(handle) ? 6 : 0;
            return error == 0;
        }

        public bool PeekPipe(nint handle, out uint available, out int error)
        {
            Calls.Add($"peek:{handle}");
            available = PeekAvailable;
            error = PeekError;
            return PeekSuccess;
        }

        public bool ReadFile(nint handle, byte[] buffer, out uint read, out int error)
        {
            Calls.Add($"read:{handle}:{buffer.Length}");
            LastReadBufferLength = buffer.Length;
            buffer.AsSpan().Fill(ReadFill);
            read = ReadCount;
            error = ReadError;
            return ReadSuccess;
        }
    }

    private sealed class FakeClock(List<string> events, TimeSpan elapsed) : IWin32MonotonicClock
    {
        internal Queue<TimeSpan> ElapsedScript { get; } = new();

        public long GetTimestamp()
        {
            events.Add("origin");
            return 100;
        }

        public TimeSpan GetElapsedTime(long startingTimestamp)
        {
            Assert.Equal(100, startingTimestamp);
            events.Add("elapsed");
            return ElapsedScript.Count > 0 ? ElapsedScript.Dequeue() : elapsed;
        }

        public void Delay(TimeSpan duration) => throw new InvalidOperationException("completion must not delay");
    }

    private sealed class FakeCompletionApi(List<string> events) : IEvidenceBuildProcessCompletionApi
    {
        internal uint WaitResult { get; init; }
        internal int WaitError { get; init; }
        internal bool ExitSuccess { get; init; } = true;
        internal uint ExitCode { get; init; }
        internal int ExitError { get; init; }
        internal bool CloseSuccess { get; init; } = true;
        internal int CloseError { get; init; }
        internal int WaitCalls { get; private set; }
        internal int ExitCalls { get; private set; }
        internal int CloseCalls { get; private set; }

        public uint WaitForSingleObject(nint process, uint milliseconds, out int error)
        {
            Assert.Equal((nint)90, process);
            WaitCalls++;
            events.Add($"wait:{milliseconds}");
            error = WaitError;
            return WaitResult;
        }

        public bool GetExitCodeProcess(nint process, out uint exitCode, out int error)
        {
            Assert.Equal((nint)90, process);
            ExitCalls++;
            events.Add("exit");
            exitCode = ExitCode;
            error = ExitError;
            return ExitSuccess;
        }

        public bool CloseHandle(nint handle, out int error)
        {
            Assert.Equal((nint)90, handle);
            CloseCalls++;
            events.Add("close");
            error = CloseError;
            return CloseSuccess;
        }
    }
}
