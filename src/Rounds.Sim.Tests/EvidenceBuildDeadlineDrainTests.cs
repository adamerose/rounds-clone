using System.Collections.Concurrent;
using Rounds.EvidenceLauncher;

namespace Rounds.Sim.Tests;

public sealed class EvidenceBuildDeadlineDrainTests
{
    private static readonly EvidenceBuildRawSource Stdout = EvidenceBuildRawSource.Create("build-stdout");
    private static readonly EvidenceBuildRawSource Stderr = EvidenceBuildRawSource.Create("build-stderr");

    [Fact]
    public void ExactDeadlineUsesOneOriginAndSharedMonotonicObservations()
    {
        var clock = new FakeClock(TimeSpan.Zero);

        var deadline = EvidenceBuildRunDeadline.Arm(clock, TimeSpan.FromMinutes(5));
        var first = deadline.Observe();
        clock.Advance(TimeSpan.FromMinutes(4));
        var second = deadline.Observe();

        Assert.Equal(1, clock.TimestampReads);
        Assert.Equal(1234, deadline.Origin);
        Assert.False(first.Expired);
        Assert.Equal(TimeSpan.FromMinutes(5), first.Remaining);
        Assert.Equal(TimeSpan.FromMinutes(1), second.Remaining);
    }

    [Theory]
    [InlineData(299)]
    [InlineData(301)]
    public void AlteredBuildDeadlineRefusesBeforeOriginCapture(int seconds)
    {
        var clock = new FakeClock(TimeSpan.Zero);

        Assert.Throws<InvalidOperationException>(() =>
            EvidenceBuildRunDeadline.Arm(clock, TimeSpan.FromSeconds(seconds)));

        Assert.Equal(0, clock.TimestampReads);
    }

    [Fact]
    public void DeadlineRefusesNegativeOriginRollbackAndElapsedOverflow()
    {
        Assert.Throws<InvalidOperationException>(() =>
            EvidenceBuildRunDeadline.Arm(new FakeClock(TimeSpan.Zero) { Origin = -1 }, TimeSpan.FromMinutes(5)));

        var rollbackClock = new FakeClock(TimeSpan.Zero);
        rollbackClock.ScriptElapsed(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));
        var rollback = EvidenceBuildRunDeadline.Arm(rollbackClock, TimeSpan.FromMinutes(5));
        _ = rollback.Observe();
        Assert.Throws<InvalidOperationException>(() => rollback.Observe());

        var overflowClock = new FakeClock(TimeSpan.Zero) { ThrowElapsedOverflow = true };
        var overflow = EvidenceBuildRunDeadline.Arm(overflowClock, TimeSpan.FromMinutes(5));
        Assert.Throws<InvalidOperationException>(() => overflow.Observe());
    }

    [Fact]
    public void BothWorkersProveGatedStartupBeforeSessionPublicationOrAnyRead()
    {
        var api = new FakeReadApi();
        api.Eof(Stdout);
        api.Eof(Stderr);
        var starter = new FakeWorkerStarter { ReverseExecutionOrder = true };

        using var session = Prepare(api, starter);

        Assert.True(session.ReadyForDeadline);
        Assert.Equal(2, starter.StartCount);
        Assert.Equal(0, api.TotalPolls);
        var clock = new FakeClock(TimeSpan.Zero);
        session.Release(EvidenceBuildRunDeadline.Arm(clock, TimeSpan.FromMinutes(5)));
        var capture = session.Complete();
        Assert.True(capture.BothReachedEndOfFile);
        Assert.Equal(2, api.TotalPolls);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void SynchronousWorkerStartFailureStopsAndObservesEveryStartedWorker(int failingStart)
    {
        var api = new FakeReadApi();
        var starter = new FakeWorkerStarter { FailStartNumber = failingStart };

        var failure = Assert.Throws<IOException>(() => Prepare(api, starter));

        Assert.Contains($"start {failingStart}", failure.Message, StringComparison.Ordinal);
        Assert.Equal(0, starter.ActiveWorkers);
        Assert.Equal(0, api.TotalPolls);
        Assert.True(starter.WaitCalls >= (failingStart == 1 ? 0 : 1));
    }

    [Theory]
    [InlineData("stdout-cap")]
    [InlineData("stderr-cap")]
    [InlineData("deadline")]
    public void AlteredDrainPolicyRefusesBeforeWorkerStart(string field)
    {
        var policy = EvidenceBuildRawDrainPolicy.Exact with
        {
            StandardOutputCapBytes = field == "stdout-cap"
                ? EvidenceBuildRawDrainPolicy.ExactStreamCapBytes - 1
                : EvidenceBuildRawDrainPolicy.ExactStreamCapBytes,
            StandardErrorCapBytes = field == "stderr-cap"
                ? EvidenceBuildRawDrainPolicy.ExactStreamCapBytes + 1
                : EvidenceBuildRawDrainPolicy.ExactStreamCapBytes,
            Deadline = field == "deadline" ? TimeSpan.FromSeconds(299) : TimeSpan.FromMinutes(5),
        };
        var starter = new FakeWorkerStarter();

        Assert.Throws<InvalidOperationException>(() =>
            new EvidenceBuildRawDrainFactory(new FakeReadApi(), starter).Prepare(Stdout, Stderr, policy));

        Assert.Equal(0, starter.StartCount);
    }

    [Fact]
    public void InterleavedPartialAndLargeReadsPreserveRawBytesWithAsymmetricEof()
    {
        var api = new FakeReadApi { RequireConcurrentFirstPoll = true };
        var stdout = Enumerable.Range(0, 100_000).Select(index => (byte)(index % 251)).ToArray();
        var stderr = Enumerable.Range(0, 90_000).Select(index => (byte)(255 - (index % 251))).ToArray();
        api.Bytes(Stdout, stdout);
        api.Eof(Stdout);
        api.Bytes(Stderr, stderr[..17], stderr[17..70_000], stderr[70_000..]);
        api.Eof(Stderr);

        using var session = Prepare(api);
        var capture = ReleaseAndComplete(session, new FakeClock(TimeSpan.Zero));

        Assert.Equal(stdout, capture.StandardOutput.Bytes.ToArray());
        Assert.Equal(stderr, capture.StandardError.Bytes.ToArray());
        Assert.True(capture.BothReachedEndOfFile);
        Assert.Equal(2, api.ConcurrentFirstPollParticipants);
        Assert.True(api.FirstEofOrdinal(Stdout) != api.FirstEofOrdinal(Stderr));
    }

    [Fact]
    public void NoProgressAndZeroByteDataRequireLaterExplicitBrokenPipeEof()
    {
        var api = new FakeReadApi();
        api.NoProgress(Stdout);
        api.ZeroByteData(Stdout);
        api.Bytes(Stdout, [1, 2, 3]);
        api.Eof(Stdout);
        api.ZeroByteData(Stderr);
        api.NoProgress(Stderr);
        api.Eof(Stderr);
        var clock = new FakeClock(TimeSpan.Zero);

        using var session = Prepare(api);
        var capture = ReleaseAndComplete(session, clock);

        Assert.True(capture.BothReachedEndOfFile);
        Assert.Equal(new byte[] { 1, 2, 3 }, capture.StandardOutput.Bytes.ToArray());
        Assert.True(clock.DelayCalls >= 4);
    }

    [Fact]
    public void ExactCapIsRetainedAndStillRequiresExplicitEof()
    {
        var api = new FakeReadApi();
        var bytes = Enumerable.Repeat((byte)0x5a, EvidenceBuildRawDrainPolicy.ExactStreamCapBytes).ToArray();
        api.Bytes(Stdout, bytes);
        api.Eof(Stdout);
        api.Eof(Stderr);

        using var session = Prepare(api);
        var capture = ReleaseAndComplete(session, new FakeClock(TimeSpan.Zero));

        Assert.False(capture.StandardOutputCapExceeded);
        Assert.True(capture.StandardOutput.EndOfFile);
        Assert.Equal(bytes.Length, capture.StandardOutput.Bytes.Length);
        Assert.Equal(bytes.Length, capture.StandardOutput.BytesObserved);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CapPlusOneRetainsOnlyExactCapAndStopsSibling(bool stdout)
    {
        var api = new FakeReadApi();
        var over = Enumerable.Repeat((byte)0x33, EvidenceBuildRawDrainPolicy.ExactStreamCapBytes + 1).ToArray();
        if (stdout)
        {
            api.Bytes(Stdout, over);
            api.BlockUntilBytesServed(Stderr, Stdout, over.Length);
        }
        else
        {
            api.Bytes(Stderr, over);
            api.BlockUntilBytesServed(Stdout, Stderr, over.Length);
        }

        using var session = Prepare(api);
        var capture = ReleaseAndComplete(session, new FakeClock(TimeSpan.Zero));
        var capped = stdout ? capture.StandardOutput : capture.StandardError;
        var sibling = stdout ? capture.StandardError : capture.StandardOutput;

        Assert.True(capped.CapExceeded);
        Assert.Equal(EvidenceBuildRawDrainPolicy.ExactStreamCapBytes, capped.Bytes.Length);
        Assert.Equal(EvidenceBuildRawDrainPolicy.ExactStreamCapBytes + 1L, capped.BytesObserved);
        Assert.True(sibling.Stopped);
    }

    [Fact]
    public void SharedDeadlineTimeoutStopsSiblingAndFlagsComeOnlyFromTerminalEvents()
    {
        var api = new FakeReadApi();
        var clock = new FakeClock(TimeSpan.FromMinutes(5));

        using var session = Prepare(api);
        var capture = ReleaseAndComplete(session, clock);

        Assert.True(capture.TimedOut);
        Assert.False(capture.BothReachedEndOfFile);
        Assert.True(capture.StandardOutput.TimedOut || capture.StandardError.TimedOut);
        Assert.Contains(
            capture.StandardOutput.Terminal,
            [EvidenceBuildRawDrainTerminal.TimedOut, EvidenceBuildRawDrainTerminal.Stopped]);
        Assert.Contains(
            capture.StandardError.Terminal,
            [EvidenceBuildRawDrainTerminal.TimedOut, EvidenceBuildRawDrainTerminal.Stopped]);
        Assert.False(capture.StandardOutputCapExceeded);
        Assert.False(capture.StandardErrorCapExceeded);
    }

    [Fact]
    public void RepeatedNoDataCannotManufactureEofAndReachesTheSharedDeadline()
    {
        var api = new FakeReadApi();
        api.NoProgress(Stdout);
        api.ZeroByteData(Stderr);
        var clock = new FakeClock(TimeSpan.FromMinutes(5) - TimeSpan.FromMilliseconds(5));

        using var session = Prepare(api);
        var capture = ReleaseAndComplete(session, clock);

        Assert.True(capture.TimedOut);
        Assert.False(capture.StandardOutput.EndOfFile);
        Assert.False(capture.StandardError.EndOfFile);
        Assert.True(clock.DelayCalls >= 1);
    }

    [Fact]
    public void SharedClockRollbackFailsClosedStopsSiblingAndObservesBothWorkers()
    {
        var api = new FakeReadApi();
        api.NoProgress(Stdout);
        api.NoProgress(Stderr);
        var starter = new FakeWorkerStarter();
        var clock = new FakeClock(TimeSpan.Zero);
        clock.ScriptElapsed(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1));

        using var session = Prepare(api, starter);
        session.Release(EvidenceBuildRunDeadline.Arm(clock, TimeSpan.FromMinutes(5)));
        var failure = Record.Exception(session.Complete);
        IEnumerable<Exception> failures = failure is AggregateException aggregate
            ? aggregate.Flatten().InnerExceptions
            : [Assert.IsAssignableFrom<Exception>(failure)];

        Assert.Contains(
            failures,
            exception => exception.Message.Contains("rolled backward", StringComparison.Ordinal));
        Assert.Equal(0, starter.ActiveWorkers);
    }

    [Fact]
    public void ReadFaultRequestsSiblingStopAndPropagatesAfterBothWorkersAreObserved()
    {
        var api = new FakeReadApi { RequireConcurrentFirstPoll = true };
        api.Fault(Stdout, new IOException("stdout read failed"));
        api.NoProgress(Stderr);

        using var session = Prepare(api);
        session.Release(EvidenceBuildRunDeadline.Arm(new FakeClock(TimeSpan.Zero), TimeSpan.FromMinutes(5)));
        var failure = Assert.Throws<IOException>(session.Complete);

        Assert.Equal("stdout read failed", failure.Message);
        Assert.True(api.PollCount(Stderr) >= 1);
    }

    [Fact]
    public void SimultaneousReadFaultsAreAggregatedInStreamOrder()
    {
        var api = new FakeReadApi { RequireConcurrentFirstPoll = true };
        api.Fault(Stdout, new IOException("stdout fault"));
        api.Fault(Stderr, new InvalidDataException("stderr fault"));

        using var session = Prepare(api);
        session.Release(EvidenceBuildRunDeadline.Arm(new FakeClock(TimeSpan.Zero), TimeSpan.FromMinutes(5)));
        var failure = Assert.Throws<AggregateException>(session.Complete);

        Assert.Collection(
            failure.InnerExceptions,
            exception => Assert.Equal("stdout fault", exception.Message),
            exception => Assert.Equal("stderr fault", exception.Message));
        Assert.Throws<InvalidOperationException>(session.Complete);
        session.Dispose();
        session.Dispose();
    }

    [Fact]
    public void OversizedReadShimFaultStopsAndObservesSibling()
    {
        var api = new FakeReadApi { IgnoreMaximumRead = true };
        var copier = new FakeByteCopier { ThrowIfInvoked = true };
        api.Bytes(Stdout, new byte[1_000_000]);
        api.NoProgress(Stderr);

        using var session = Prepare(api, copier: copier);
        session.Release(EvidenceBuildRunDeadline.Arm(new FakeClock(TimeSpan.Zero), TimeSpan.FromMinutes(5)));

        Assert.Throws<InvalidDataException>(session.Complete);
        Assert.Equal(0, copier.CloneCalls);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void ZeroStatePayloadsRefuseBeforeAnyClone(int rawKind)
    {
        var api = new FakeReadApi();
        var copier = new FakeByteCopier { ThrowIfInvoked = true };
        var kind = (EvidenceBuildRawReadKind)rawKind;
        api.Raw(Stdout, new EvidenceBuildRawRead(kind, new byte[1_000_000]));
        api.Eof(Stderr);

        using var session = Prepare(api, copier: copier);
        session.Release(EvidenceBuildRunDeadline.Arm(new FakeClock(TimeSpan.Zero), TimeSpan.FromMinutes(5)));

        Assert.Throws<InvalidDataException>(session.Complete);
        Assert.Equal(0, copier.CloneCalls);
    }

    [Fact]
    public void NullZeroLengthAndUnknownDataShapesRefuseBeforeAnyClone()
    {
        foreach (var read in new[]
                 {
                     EvidenceBuildRawRead.Bytes(),
                     new EvidenceBuildRawRead((EvidenceBuildRawReadKind)999, Array.Empty<byte>()),
                     new EvidenceBuildRawRead(EvidenceBuildRawReadKind.Data, null!),
                 })
        {
            var api = new FakeReadApi();
            var copier = new FakeByteCopier { ThrowIfInvoked = true };
            api.Raw(Stdout, read);
            api.Eof(Stderr);

            using var session = Prepare(api, copier: copier);
            session.Release(EvidenceBuildRunDeadline.Arm(new FakeClock(TimeSpan.Zero), TimeSpan.FromMinutes(5)));
            Assert.Throws<InvalidDataException>(session.Complete);
            Assert.Equal(0, copier.CloneCalls);
        }
    }

    [Fact]
    public void MutableReadBuffersAndReturnedCopiesCannotChangeRetainedOutput()
    {
        var api = new FakeReadApi();
        var mutable = new byte[] { 1, 2, 3 };
        api.Bytes(Stdout, mutable);
        api.Callback(Stdout, () =>
        {
            mutable.AsSpan().Fill(9);
            return EvidenceBuildRawRead.EndOfFile();
        });
        api.Eof(Stderr);

        using var session = Prepare(api);
        var capture = ReleaseAndComplete(session, new FakeClock(TimeSpan.Zero));
        var callerCopy = capture.StandardOutput.Bytes.ToArray();
        callerCopy.AsSpan().Fill(7);

        Assert.Equal(new byte[] { 1, 2, 3 }, capture.StandardOutput.Bytes.ToArray());
    }

    [Fact]
    public void WorkerIgnoringStopProducesBoundedFailureAndRemainsRetryableForCleanup()
    {
        using var blocker = new ManualResetEventSlim();
        var api = new FakeReadApi();
        var over = new byte[EvidenceBuildRawDrainPolicy.ExactStreamCapBytes + 1];
        api.Bytes(Stdout, over);
        api.Callback(Stderr, () =>
        {
            blocker.Wait();
            return EvidenceBuildRawRead.NoProgress();
        });
        var starter = new FakeWorkerStarter { MaximumRealWait = TimeSpan.FromMilliseconds(100) };
        var session = Prepare(api, starter);
        session.Release(EvidenceBuildRunDeadline.Arm(new FakeClock(TimeSpan.Zero), TimeSpan.FromMinutes(5)));

        try
        {
            var failure = Assert.Throws<TimeoutException>(session.Complete);
            Assert.Contains("bounded cleanup", failure.Message, StringComparison.Ordinal);
        }
        finally
        {
            blocker.Set();
            session.Dispose();
        }
        Assert.Equal(0, starter.ActiveWorkers);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WorkerFaultIsDeliveredOnceWhenSiblingCleanupInitiallyCannotComplete(bool stdoutFaults)
    {
        using var blocker = new ManualResetEventSlim();
        var api = new FakeReadApi { RequireConcurrentFirstPoll = true };
        var faulting = stdoutFaults ? Stdout : Stderr;
        var blocked = stdoutFaults ? Stderr : Stdout;
        api.Fault(faulting, new IOException($"{faulting.Identity} read fault"));
        api.Callback(blocked, () =>
        {
            blocker.Wait();
            return EvidenceBuildRawRead.NoProgress();
        });
        var starter = new FakeWorkerStarter { MaximumRealWait = TimeSpan.FromMilliseconds(100) };
        var session = Prepare(api, starter);
        session.Release(EvidenceBuildRunDeadline.Arm(new FakeClock(TimeSpan.Zero), TimeSpan.FromMinutes(5)));

        try
        {
            var first = Assert.Throws<AggregateException>(session.Complete).Flatten();
            Assert.Collection(
                first.InnerExceptions,
                exception => Assert.IsType<TimeoutException>(exception),
                exception => Assert.Equal($"{faulting.Identity} read fault", exception.Message));

            var retry = Assert.Throws<TimeoutException>(session.Complete);
            Assert.DoesNotContain("read fault", retry.ToString(), StringComparison.Ordinal);

            blocker.Set();
            session.Dispose();
            session.Dispose();
        }
        finally
        {
            blocker.Set();
            session.Dispose();
        }

        Assert.Equal(0, starter.ActiveWorkers);
    }

    [Fact]
    public void StopBeforeReleaseAndRepeatedStopDisposeAreIdempotentAndPerformNoReads()
    {
        var api = new FakeReadApi();
        var starter = new FakeWorkerStarter();
        var session = Prepare(api, starter);

        session.StopAndWait();
        session.StopAndWait();
        session.Dispose();
        session.Dispose();

        Assert.Equal(0, api.TotalPolls);
        Assert.Equal(0, starter.ActiveWorkers);
    }

    [Fact]
    public void CompleteIsRepeatableUntilDisposeAndReleaseIsOneShot()
    {
        var api = new FakeReadApi();
        api.Eof(Stdout);
        api.Eof(Stderr);
        var session = Prepare(api);
        session.Release(EvidenceBuildRunDeadline.Arm(new FakeClock(TimeSpan.Zero), TimeSpan.FromMinutes(5)));

        var first = session.Complete();
        var second = session.Complete();

        Assert.Same(first, second);
        Assert.Throws<InvalidOperationException>(() =>
            session.Release(EvidenceBuildRunDeadline.Arm(new FakeClock(TimeSpan.Zero), TimeSpan.FromMinutes(5))));
        session.Dispose();
        Assert.Throws<ObjectDisposedException>(session.Complete);
    }

    private static EvidenceBuildRawDrainSession Prepare(
        FakeReadApi api,
        FakeWorkerStarter? starter = null,
        FakeByteCopier? copier = null) =>
        new EvidenceBuildRawDrainFactory(
            api,
            starter ?? new FakeWorkerStarter(),
            copier ?? new FakeByteCopier()).Prepare(
            Stdout,
            Stderr,
            EvidenceBuildRawDrainPolicy.Exact);

    private static EvidenceBuildRawDrainCapture ReleaseAndComplete(
        EvidenceBuildRawDrainSession session,
        FakeClock clock)
    {
        session.Release(EvidenceBuildRunDeadline.Arm(clock, TimeSpan.FromMinutes(5)));
        return session.Complete();
    }

    private sealed class FakeClock(TimeSpan initialElapsed) : IWin32MonotonicClock
    {
        private readonly object _gate = new();
        private readonly Queue<TimeSpan> _scripted = new();
        private TimeSpan _elapsed = initialElapsed;

        internal long Origin { get; init; } = 1234;
        internal bool ThrowElapsedOverflow { get; init; }
        internal int TimestampReads { get; private set; }
        internal int DelayCalls { get; private set; }

        internal void ScriptElapsed(params TimeSpan[] values)
        {
            lock (_gate)
            {
                foreach (var value in values) _scripted.Enqueue(value);
            }
        }

        internal void Advance(TimeSpan duration)
        {
            lock (_gate) _elapsed += duration;
        }

        public long GetTimestamp()
        {
            TimestampReads++;
            return Origin;
        }

        public TimeSpan GetElapsedTime(long startingTimestamp)
        {
            Assert.Equal(Origin, startingTimestamp);
            if (ThrowElapsedOverflow) throw new OverflowException("injected elapsed overflow");
            lock (_gate) return _scripted.Count > 0 ? _scripted.Dequeue() : _elapsed;
        }

        public void Delay(TimeSpan duration)
        {
            lock (_gate)
            {
                DelayCalls++;
                _elapsed += duration;
            }
        }
    }

    private sealed class FakeWorkerStarter : IEvidenceBuildDrainWorkerStarter
    {
        private readonly ManualResetEventSlim _secondScheduled = new();
        private int _starts;
        private int _active;

        internal int? FailStartNumber { get; init; }
        internal bool ReverseExecutionOrder { get; init; }
        internal TimeSpan MaximumRealWait { get; init; } = TimeSpan.FromSeconds(2);
        internal int StartCount => Volatile.Read(ref _starts);
        internal int ActiveWorkers => Volatile.Read(ref _active);
        internal int WaitCalls { get; private set; }

        public Task<T> Start<T>(Func<T> operation)
        {
            var ordinal = Interlocked.Increment(ref _starts);
            if (FailStartNumber == ordinal)
            {
                throw new IOException($"injected start {ordinal} failure");
            }
            if (ordinal == 2) _secondScheduled.Set();
            return Task.Factory.StartNew(
                () =>
                {
                    Interlocked.Increment(ref _active);
                    try
                    {
                        if (ReverseExecutionOrder && ordinal == 1 &&
                            !_secondScheduled.Wait(TimeSpan.FromSeconds(2)))
                        {
                            throw new TimeoutException("second worker was not scheduled");
                        }
                        return operation();
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _active);
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);
        }

        public bool WaitForCompletion(Task task, TimeSpan timeout)
        {
            WaitCalls++;
            var bounded = timeout < MaximumRealWait ? timeout : MaximumRealWait;
            try
            {
                return task.Wait(bounded);
            }
            catch (AggregateException)
            {
                return true;
            }
        }
    }

    private sealed class FakeReadApi : IEvidenceBuildRawReadApi
    {
        private readonly object _gate = new();
        private readonly Dictionary<EvidenceBuildRawSource, LinkedList<object>> _scripts = new();
        private readonly Dictionary<EvidenceBuildRawSource, int> _polls = new();
        private readonly Dictionary<EvidenceBuildRawSource, long> _served = new();
        private readonly ConcurrentQueue<(int Ordinal, EvidenceBuildRawSource Source, string Kind)> _events = new();
        private readonly CountdownEvent _firstPollBarrier = new(2);
        private readonly HashSet<EvidenceBuildRawSource> _firstPolls = [];
        private int _ordinal;

        internal bool RequireConcurrentFirstPoll { get; init; }
        internal bool IgnoreMaximumRead { get; init; }
        internal int TotalPolls { get { lock (_gate) return _polls.Values.Sum(); } }
        internal int ConcurrentFirstPollParticipants => 2 - _firstPollBarrier.CurrentCount;

        internal void Bytes(EvidenceBuildRawSource source, params byte[][] chunks)
        {
            foreach (var chunk in chunks) Enqueue(source, chunk);
        }

        internal void NoProgress(EvidenceBuildRawSource source) =>
            Enqueue(source, EvidenceBuildRawRead.NoProgress());

        internal void ZeroByteData(EvidenceBuildRawSource source) =>
            Enqueue(source, EvidenceBuildRawRead.NoProgress());

        internal void Raw(EvidenceBuildRawSource source, EvidenceBuildRawRead read) =>
            Enqueue(source, new UnrecordedRead(read));

        internal void Eof(EvidenceBuildRawSource source) =>
            Enqueue(source, EvidenceBuildRawRead.EndOfFile());

        internal void Fault(EvidenceBuildRawSource source, Exception failure) => Enqueue(source, failure);

        internal void Callback(EvidenceBuildRawSource source, Func<EvidenceBuildRawRead> callback) =>
            Enqueue(source, callback);

        internal void BlockUntilBytesServed(
            EvidenceBuildRawSource blocked,
            EvidenceBuildRawSource observed,
            long expected) =>
            Callback(blocked, () =>
            {
                Assert.True(SpinWait.SpinUntil(() => BytesServed(observed) >= expected, TimeSpan.FromSeconds(2)));
                return EvidenceBuildRawRead.NoProgress();
            });

        internal int PollCount(EvidenceBuildRawSource source)
        {
            lock (_gate) return _polls.GetValueOrDefault(source);
        }

        internal long BytesServed(EvidenceBuildRawSource source)
        {
            lock (_gate) return _served.GetValueOrDefault(source);
        }

        internal int FirstEofOrdinal(EvidenceBuildRawSource source) => _events
            .Where(item => item.Source == source && item.Kind == "eof")
            .Select(item => item.Ordinal)
            .DefaultIfEmpty(int.MaxValue)
            .Min();

        public EvidenceBuildRawRead Poll(EvidenceBuildRawSource source, int maximumBytes)
        {
            var first = false;
            lock (_gate)
            {
                _polls[source] = _polls.GetValueOrDefault(source) + 1;
                first = _firstPolls.Add(source);
            }
            if (first && RequireConcurrentFirstPoll)
            {
                _firstPollBarrier.Signal();
                if (!_firstPollBarrier.Wait(TimeSpan.FromSeconds(2)))
                {
                    throw new TimeoutException("both build drains did not poll concurrently");
                }
            }

            object? next;
            lock (_gate)
            {
                next = _scripts.TryGetValue(source, out var script) && script.Count > 0
                    ? RemoveFirst(script)
                    : null;
            }
            if (next is null) return EvidenceBuildRawRead.NoProgress();
            if (next is Exception failure) throw failure;
            if (next is Func<EvidenceBuildRawRead> callback) return callback();
            if (next is UnrecordedRead unrecorded) return unrecorded.Read;
            if (next is EvidenceBuildRawRead direct) return Record(source, direct);

            var bytes = (byte[])next;
            if (!IgnoreMaximumRead && bytes.Length > maximumBytes)
            {
                var remainder = bytes[maximumBytes..];
                bytes = bytes[..maximumBytes];
                lock (_gate) _scripts[source].AddFirst(remainder);
            }
            return Record(source, EvidenceBuildRawRead.Bytes(bytes));
        }

        private EvidenceBuildRawRead Record(EvidenceBuildRawSource source, EvidenceBuildRawRead read)
        {
            var ordinal = Interlocked.Increment(ref _ordinal);
            if (read.Kind == EvidenceBuildRawReadKind.Data)
            {
                lock (_gate) _served[source] = _served.GetValueOrDefault(source) + read.Data.Length;
                _events.Enqueue((ordinal, source, "data"));
            }
            else if (read.Kind == EvidenceBuildRawReadKind.EndOfFile)
            {
                _events.Enqueue((ordinal, source, "eof"));
            }
            return read;
        }

        private void Enqueue(EvidenceBuildRawSource source, object value)
        {
            lock (_gate)
            {
                if (!_scripts.TryGetValue(source, out var script))
                {
                    script = new LinkedList<object>();
                    _scripts.Add(source, script);
                }
                script.AddLast(value);
            }
        }

        private static object RemoveFirst(LinkedList<object> values)
        {
            var value = values.First!.Value;
            values.RemoveFirst();
            return value;
        }

        private sealed record UnrecordedRead(EvidenceBuildRawRead Read);
    }

    private sealed class FakeByteCopier : IEvidenceBuildRawByteCopier
    {
        internal bool ThrowIfInvoked { get; init; }
        internal int CloneCalls { get; private set; }

        public byte[] CloneBounded(byte[] source, int exactLength)
        {
            CloneCalls++;
            if (ThrowIfInvoked) throw new Xunit.Sdk.XunitException("invalid read reached byte clone");
            Assert.Equal(source.Length, exactLength);
            return source.ToArray();
        }
    }
}
