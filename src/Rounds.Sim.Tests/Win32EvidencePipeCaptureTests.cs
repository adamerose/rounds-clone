using System.Collections.Concurrent;
using System.Text;
using Rounds.EvidenceLauncher;
using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class Win32EvidencePipeCaptureTests
{
    [Fact]
    public async Task Partial_concurrent_reads_preserve_raw_bytes_and_allow_one_EOF_before_other_continues()
    {
        var api = new FakePipeApi();
        var marker = MarkerBytes();
        using var stdoutReachedEof = new ManualResetEventSlim();
        api.Bytes(402, marker[..7], marker[7..31], marker[31..]);
        api.Callback(402, () =>
        {
            stdoutReachedEof.Set();
            return Win32PipePoll.EndOfFile();
        });
        api.Bytes(404, Encoding.UTF8.GetBytes("diagnostic "));
        api.Callback(404, () =>
        {
            Assert.True(stdoutReachedEof.Wait(TimeSpan.FromSeconds(2)));
            return Win32PipePoll.Bytes(Encoding.UTF8.GetBytes("payload\n"));
        });
        api.Eof(404);
        var handles = ReadyHandles(api);

        var capture = await Capture(api, handles);

        Assert.Equal(marker, capture.StandardOutputBytes.ToArray());
        Assert.Equal("diagnostic payload\n", capture.Protocol.StandardError);
        Assert.Equal(Encoding.UTF8.GetString(marker), capture.Protocol.StandardOutput);
        Assert.True(capture.StandardOutputEof);
        Assert.True(capture.StandardErrorEof);
        Assert.False(capture.Protocol.TimedOut);
        Assert.False(capture.Protocol.StandardOutputCapExceeded);
        Assert.False(capture.Protocol.StandardErrorCapExceeded);
        Assert.True(api.FirstEofOrdinal(402) < api.LastDataOrdinal(404));
        AssertParentReadsClosedExactlyOnce(api, handles);
    }

    [Theory]
    [InlineData("stdout-cap")]
    [InlineData("stderr-cap")]
    [InlineData("deadline")]
    public async Task Altered_caps_or_deadline_refuse_before_parent_handle_transfer(string field)
    {
        var api = new FakePipeApi();
        var handles = ReadyHandles(api);
        var stdoutCap = Win32BoundedPipeCapture.RequiredStandardOutputCapBytes;
        var stderrCap = Win32BoundedPipeCapture.RequiredStandardErrorCapBytes;
        var deadline = new Win32JobDeadline(100, TimeSpan.FromSeconds(30));
        switch (field)
        {
            case "stdout-cap": stdoutCap--; break;
            case "stderr-cap": stderrCap++; break;
            case "deadline": deadline = deadline with { Timeout = TimeSpan.FromSeconds(29) }; break;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new Win32BoundedPipeCapture(api, new FakeClock(TimeSpan.Zero)).CaptureAsync(
                handles,
                deadline,
                stdoutCap,
                stderrCap));

        Assert.Equal(0, api.CloseCount(402));
        Assert.Equal(0, api.CloseCount(404));
        handles.Dispose();
        Assert.Equal(1, api.CloseCount(402));
        Assert.Equal(1, api.CloseCount(404));
    }

    [Fact]
    public async Task Both_streams_begin_concurrently_and_drain_beyond_typical_pipe_buffer()
    {
        var api = new FakePipeApi { RequireConcurrentFirstPoll = true };
        api.Bytes(402, Enumerable.Repeat((byte)'x', 8_192).ToArray(), chunkBytes: 4_096);
        api.Eof(402);
        api.Bytes(404, Enumerable.Repeat((byte)'d', 5_000).ToArray(), chunkBytes: 2_500);
        api.Eof(404);
        var handles = ReadyHandles(api);

        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => Capture(api, handles));

        Assert.Contains("exactly one LF-terminated", failure.Message, StringComparison.Ordinal);
        Assert.Equal(8_192, api.BytesServed(402));
        Assert.Equal(5_000, api.BytesServed(404));
        Assert.Equal(2, api.ConcurrentFirstPollParticipants);
        AssertParentReadsClosedExactlyOnce(api, handles);
    }

    [Fact]
    public async Task Stdout_exact_cap_is_not_a_cap_failure_and_reaches_marker_validation()
    {
        var api = new FakePipeApi();
        api.Bytes(402, Enumerable.Repeat((byte)'x', 8_192).ToArray());
        api.Eof(402);
        api.Eof(404);
        var handles = ReadyHandles(api);

        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => Capture(api, handles));

        Assert.Contains("completion marker", failure.Message, StringComparison.Ordinal);
        Assert.Equal(8_192, api.BytesServed(402));
        AssertParentReadsClosedExactlyOnce(api, handles);
    }

    [Fact]
    public async Task Stderr_exact_cap_is_accepted_and_every_raw_byte_is_retained()
    {
        var api = new FakePipeApi();
        api.Bytes(402, MarkerBytes());
        api.Eof(402);
        var stderr = Enumerable.Repeat((byte)'d', 65_536).ToArray();
        api.Bytes(404, stderr);
        api.Eof(404);
        var handles = ReadyHandles(api);

        var capture = await Capture(api, handles);

        Assert.False(capture.Protocol.StandardErrorCapExceeded);
        Assert.Equal(65_536, capture.StandardErrorBytes.Length);
        Assert.Equal(65_536, capture.StandardErrorBytesObserved);
        Assert.Equal(stderr, capture.StandardErrorBytes.ToArray());
        AssertParentReadsClosedExactlyOnce(api, handles);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Cap_plus_one_is_rejected_without_retaining_beyond_the_exact_cap(bool stdout)
    {
        var api = new FakePipeApi();
        if (stdout)
        {
            api.Bytes(402, Enumerable.Repeat((byte)'x', 8_193).ToArray());
            api.Eof(404);
        }
        else
        {
            api.Bytes(402, MarkerBytes());
            api.Eof(402);
            api.Bytes(404, Enumerable.Repeat((byte)'d', 65_537).ToArray());
        }
        var handles = ReadyHandles(api);

        var capture = await Capture(api, handles);

        Assert.Equal(stdout, capture.Protocol.StandardOutputCapExceeded);
        Assert.Equal(!stdout, capture.Protocol.StandardErrorCapExceeded);
        Assert.Equal(stdout ? 8_192 : 65_536, stdout
            ? capture.StandardOutputBytes.Length
            : capture.StandardErrorBytes.Length);
        Assert.Equal(stdout ? 8_193 : 65_537, stdout
            ? capture.StandardOutputBytesObserved
            : capture.StandardErrorBytesObserved);
        AssertParentReadsClosedExactlyOnce(api, handles);
    }

    [Theory]
    [InlineData("stdout-bom")]
    [InlineData("stderr-bom")]
    [InlineData("stdout-invalid")]
    [InlineData("stderr-invalid")]
    public async Task BOM_or_lossy_UTF8_is_rejected_on_either_stream(string failure)
    {
        var api = new FakePipeApi();
        var stdout = MarkerBytes();
        var stderr = Encoding.UTF8.GetBytes("diagnostic");
        switch (failure)
        {
            case "stdout-bom": stdout = Encoding.UTF8.Preamble.ToArray().Concat(stdout).ToArray(); break;
            case "stderr-bom": stderr = Encoding.UTF8.Preamble.ToArray().Concat(stderr).ToArray(); break;
            case "stdout-invalid": stdout = new byte[] { 0xc3, 0x28 }; break;
            case "stderr-invalid": stderr = new byte[] { 0xc3, 0x28 }; break;
        }
        api.Bytes(402, stdout);
        api.Eof(402);
        api.Bytes(404, stderr);
        api.Eof(404);
        var handles = ReadyHandles(api);

        await Assert.ThrowsAsync<InvalidDataException>(() => Capture(api, handles));

        AssertParentReadsClosedExactlyOnce(api, handles);
    }

    [Theory]
    [InlineData("cr")]
    [InlineData("missing-lf")]
    [InlineData("extra-line")]
    [InlineData("leading-byte")]
    [InlineData("trailing-byte")]
    public async Task Stdout_accepts_only_one_exact_LF_terminated_protocol_marker_line(string failure)
    {
        var marker = Encoding.UTF8.GetString(MarkerBytes());
        var stdout = failure switch
        {
            "cr" => marker.Replace("\n", "\r\n", StringComparison.Ordinal),
            "missing-lf" => marker.TrimEnd('\n'),
            "extra-line" => marker + "extra\n",
            "leading-byte" => "x" + marker,
            "trailing-byte" => marker + "x",
            _ => throw new ArgumentOutOfRangeException(nameof(failure)),
        };
        var api = new FakePipeApi();
        api.Bytes(402, Encoding.UTF8.GetBytes(stdout));
        api.Eof(402);
        api.Eof(404);
        var handles = ReadyHandles(api);

        await Assert.ThrowsAsync<InvalidDataException>(() => Capture(api, handles));

        AssertParentReadsClosedExactlyOnce(api, handles);
    }

    [Fact]
    public async Task Completion_marker_on_stderr_never_substitutes_for_stdout_protocol()
    {
        var api = new FakePipeApi();
        api.Eof(402);
        api.Bytes(404, MarkerBytes());
        api.Eof(404);
        var handles = ReadyHandles(api);

        await Assert.ThrowsAsync<InvalidDataException>(() => Capture(api, handles));

        AssertParentReadsClosedExactlyOnce(api, handles);
    }

    [Fact]
    public async Task Shared_monotonic_deadline_requires_EOF_on_both_streams()
    {
        var api = new FakePipeApi();
        api.Bytes(402, MarkerBytes());
        api.Eof(402);
        var clock = new FakeClock(TimeSpan.FromSeconds(30) - TimeSpan.FromMilliseconds(5));
        var handles = ReadyHandles(api);

        var capture = await Capture(api, handles, clock: clock);

        Assert.True(capture.Protocol.TimedOut);
        Assert.True(capture.StandardOutputEof);
        Assert.False(capture.StandardErrorEof);
        Assert.True(clock.Elapsed >= TimeSpan.FromSeconds(30));
        AssertParentReadsClosedExactlyOnce(api, handles);
    }

    [Fact]
    public async Task Caller_cancellation_interrupts_no_data_polling_and_closes_both_handles()
    {
        using var cancellation = new CancellationTokenSource();
        var api = new FakePipeApi();
        var clock = new FakeClock(TimeSpan.Zero) { OnDelay = cancellation.Cancel };
        var handles = ReadyHandles(api);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Capture(api, handles, clock: clock, cancellationToken: cancellation.Token));

        AssertParentReadsClosedExactlyOnce(api, handles);
    }

    [Fact]
    public async Task Read_fault_propagates_and_cancels_the_sibling_without_blocking()
    {
        var api = new FakePipeApi();
        api.Fault(402, new IOException("injected stdout read fault"));
        var handles = ReadyHandles(api);

        var failure = await Assert.ThrowsAsync<IOException>(() => Capture(api, handles));

        Assert.Equal("injected stdout read fault", failure.Message);
        AssertParentReadsClosedExactlyOnce(api, handles);
    }

    [Fact]
    public async Task Zero_byte_data_is_EOF_and_never_spins()
    {
        var api = new FakePipeApi();
        api.ZeroByteData(402);
        api.ZeroByteData(404);
        var handles = ReadyHandles(api);

        await Assert.ThrowsAsync<InvalidDataException>(() => Capture(api, handles));

        Assert.Equal(1, api.PollCount(402));
        Assert.Equal(1, api.PollCount(404));
        AssertParentReadsClosedExactlyOnce(api, handles);
    }

    [Fact]
    public async Task Pipe_completion_never_writes_ACK_and_transferred_handles_close_once()
    {
        var api = new FakePipeApi();
        api.Bytes(402, MarkerBytes());
        api.Eof(402);
        api.Eof(404);
        var handles = ReadyHandles(api);

        _ = await Capture(api, handles);

        Assert.Empty(api.Writes);
        Assert.Equal(1, api.CloseCount(402));
        Assert.Equal(1, api.CloseCount(404));
        Assert.Throws<InvalidOperationException>(handles.TransferProtocolReadHandles);

        handles.WriteAcknowledgementAndClose(DebugEvidenceCaptureProtocol.EvidenceAcknowledgement);
        Assert.Single(api.Writes);
        handles.Dispose();
        Assert.Equal(1, api.CloseCount(402));
        Assert.Equal(1, api.CloseCount(404));
        Assert.Equal(1, api.CloseCount(407));
    }

    [Fact]
    public void Parent_read_ownership_cannot_transfer_before_successful_process_creation()
    {
        var api = new FakePipeApi();
        var handles = Handles(api);

        Assert.Throws<InvalidOperationException>(handles.TransferProtocolReadHandles);

        handles.Dispose();
        Assert.Equal(1, api.CloseCount(402));
        Assert.Equal(1, api.CloseCount(404));
    }

    private static Task<Win32BoundedProtocolCapture> Capture(
        FakePipeApi api,
        Win32LaunchHandleLease handles,
        FakeClock? clock = null,
        CancellationToken cancellationToken = default) =>
        new Win32BoundedPipeCapture(api, clock ?? new FakeClock(TimeSpan.Zero)).CaptureAsync(
            handles,
            new Win32JobDeadline(100, TimeSpan.FromSeconds(30)),
            Win32BoundedPipeCapture.RequiredStandardOutputCapBytes,
            Win32BoundedPipeCapture.RequiredStandardErrorCapBytes,
            cancellationToken);

    private static Win32LaunchHandleLease ReadyHandles(FakePipeApi api)
    {
        var handles = Handles(api);
        handles.CompleteSuccessfulProcessCreation();
        return handles;
    }

    private static Win32LaunchHandleLease Handles(FakePipeApi api) => new(
        api,
        standardInputRead: 401,
        standardOutputRead: 402,
        standardOutputWrite: 403,
        standardErrorRead: 404,
        standardErrorWrite: 405,
        acknowledgementRead: 406,
        acknowledgementWrite: 407);

    private static byte[] MarkerBytes()
    {
        var marker = DebugEvidenceCaptureProtocol.BaseProjectileCompleteMarker(
            new DebugBaseProjectileEvidenceAttestation(
                0x0123456789abcdef,
                1,
                0,
                "RoundsEvidence-0123456789abcdef0123456789abcdef",
                new DebugEvidenceCaptureAttestation(3, 684, -900, 1280, 720, 1920, 1080),
                new string('a', 64),
                new string('b', 32),
                new string('c', 64),
                "frame-0000.png"));
        return Encoding.UTF8.GetBytes(marker + "\n");
    }

    private static void AssertParentReadsClosedExactlyOnce(
        FakePipeApi api,
        Win32LaunchHandleLease handles)
    {
        Assert.Equal(1, api.CloseCount(402));
        Assert.Equal(1, api.CloseCount(404));
        handles.Dispose();
        Assert.Equal(1, api.CloseCount(402));
        Assert.Equal(1, api.CloseCount(404));
    }

    private sealed class FakeClock(TimeSpan initialElapsed) : IWin32MonotonicClock
    {
        private readonly object _gate = new();
        private TimeSpan _elapsed = initialElapsed;

        internal Action? OnDelay { get; init; }

        internal TimeSpan Elapsed
        {
            get
            {
                lock (_gate) return _elapsed;
            }
        }

        public long GetTimestamp() => 100;

        public TimeSpan GetElapsedTime(long startingTimestamp)
        {
            Assert.Equal(100, startingTimestamp);
            lock (_gate) return _elapsed;
        }

        public void Delay(TimeSpan duration)
        {
            lock (_gate) _elapsed += duration;
            OnDelay?.Invoke();
        }
    }

    private sealed class FakePipeApi : IWin32PipeReadApi, IWin32EvidenceApi
    {
        private readonly object _gate = new();
        private readonly Dictionary<nint, Queue<object>> _scripts = new();
        private readonly Dictionary<nint, int> _bytesServed = new();
        private readonly Dictionary<nint, int> _pollCounts = new();
        private readonly Dictionary<nint, int> _closeCounts = new();
        private readonly ConcurrentQueue<(int Ordinal, nint Handle, string Kind)> _readEvents = new();
        private readonly ConcurrentQueue<(nint Handle, byte[] Bytes)> _writes = new();
        private readonly CountdownEvent _concurrentFirstPoll = new(2);
        private readonly HashSet<nint> _firstPolls = new();
        private int _readOrdinal;

        internal bool RequireConcurrentFirstPoll { get; init; }
        internal int ConcurrentFirstPollParticipants => 2 - _concurrentFirstPoll.CurrentCount;
        internal IReadOnlyCollection<(nint Handle, byte[] Bytes)> Writes => _writes.ToArray();

        internal void Bytes(nint handle, params byte[][] chunks)
        {
            foreach (var chunk in chunks) Enqueue(handle, Win32PipePoll.Bytes(chunk));
        }

        internal void Bytes(nint handle, byte[] bytes, int chunkBytes)
        {
            for (var offset = 0; offset < bytes.Length; offset += chunkBytes)
            {
                Enqueue(handle, Win32PipePoll.Bytes(
                    bytes[offset..System.Math.Min(bytes.Length, offset + chunkBytes)]));
            }
        }

        internal void Eof(nint handle) => Enqueue(handle, Win32PipePoll.EndOfFile());

        internal void ZeroByteData(nint handle) => Enqueue(handle, Win32PipePoll.Bytes());

        internal void Fault(nint handle, Exception exception) => Enqueue(handle, exception);

        internal void Callback(nint handle, Func<Win32PipePoll> callback) => Enqueue(handle, callback);

        internal int BytesServed(nint handle)
        {
            lock (_gate) return _bytesServed.GetValueOrDefault(handle);
        }

        internal int PollCount(nint handle)
        {
            lock (_gate) return _pollCounts.GetValueOrDefault(handle);
        }

        internal int CloseCount(nint handle)
        {
            lock (_gate) return _closeCounts.GetValueOrDefault(handle);
        }

        internal int FirstEofOrdinal(nint handle) => _readEvents
            .Where(value => value.Handle == handle && value.Kind == "eof")
            .Select(value => value.Ordinal)
            .DefaultIfEmpty(int.MaxValue)
            .Min();

        internal int LastDataOrdinal(nint handle) => _readEvents
            .Where(value => value.Handle == handle && value.Kind == "data")
            .Select(value => value.Ordinal)
            .DefaultIfEmpty(int.MinValue)
            .Max();

        public Win32PipePoll Poll(nint handle, int maximumBytes)
        {
            var first = false;
            lock (_gate)
            {
                _pollCounts[handle] = _pollCounts.GetValueOrDefault(handle) + 1;
                first = _firstPolls.Add(handle);
            }
            if (first && RequireConcurrentFirstPoll)
            {
                _concurrentFirstPoll.Signal();
                if (!_concurrentFirstPoll.Wait(TimeSpan.FromSeconds(2)))
                {
                    throw new TimeoutException("Both drains did not start concurrently.");
                }
            }

            object next;
            lock (_gate)
            {
                if (!_scripts.TryGetValue(handle, out var script) || script.Count == 0)
                {
                    return Win32PipePoll.NoData();
                }
                next = script.Dequeue();
            }
            if (next is Exception exception) throw exception;
            var read = next is Func<Win32PipePoll> callback
                ? callback()
                : (Win32PipePoll)next;
            if (read.Kind == Win32PipePollKind.Data && read.Data.Length > maximumBytes)
            {
                var returned = read.Data[..maximumBytes];
                var remainder = read.Data[maximumBytes..];
                lock (_gate) _scripts[handle].EnqueueFront(Win32PipePoll.Bytes(remainder));
                read = Win32PipePoll.Bytes(returned);
            }

            var ordinal = Interlocked.Increment(ref _readOrdinal);
            if (read.Kind == Win32PipePollKind.Data)
            {
                lock (_gate) _bytesServed[handle] =
                    _bytesServed.GetValueOrDefault(handle) + read.Data.Length;
                _readEvents.Enqueue((ordinal, handle, "data"));
            }
            else if (read.Kind == Win32PipePollKind.EndOfFile)
            {
                _readEvents.Enqueue((ordinal, handle, "eof"));
            }
            return read;
        }

        public bool CloseKernelHandle(nint handle)
        {
            lock (_gate) _closeCounts[handle] = _closeCounts.GetValueOrDefault(handle) + 1;
            return true;
        }

        public bool CloseDesktop(nint desktop) => true;
        public bool TerminateProcess(nint process, uint exitCode) => true;
        public uint WaitForSingleObject(nint handle, uint milliseconds) =>
            Win32EvidenceConstants.WaitObject0;

        public bool WriteFile(nint handle, ReadOnlySpan<byte> data, out uint written)
        {
            var copy = data.ToArray();
            _writes.Enqueue((handle, copy));
            written = checked((uint)copy.Length);
            return true;
        }

        private void Enqueue(nint handle, object value)
        {
            lock (_gate)
            {
                if (!_scripts.TryGetValue(handle, out var script))
                {
                    script = new Queue<object>();
                    _scripts.Add(handle, script);
                }
                script.Enqueue(value);
            }
        }
    }
}

internal static class QueueExtensions
{
    internal static void EnqueueFront<T>(this Queue<T> queue, T value)
    {
        var existing = queue.ToArray();
        queue.Clear();
        queue.Enqueue(value);
        foreach (var item in existing) queue.Enqueue(item);
    }
}
