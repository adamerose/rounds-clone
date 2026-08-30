using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using Rounds.Game;

namespace Rounds.EvidenceLauncher;

internal enum Win32PipePollKind
{
    NoData,
    Data,
    EndOfFile,
}

internal readonly record struct Win32PipePoll(Win32PipePollKind Kind, byte[] Data)
{
    internal static Win32PipePoll NoData() => new(Win32PipePollKind.NoData, Array.Empty<byte>());

    internal static Win32PipePoll EndOfFile() =>
        new(Win32PipePollKind.EndOfFile, Array.Empty<byte>());

    internal static Win32PipePoll Bytes(params byte[] data) =>
        new(Win32PipePollKind.Data, data);
}

internal interface IWin32PipeReadApi
{
    Win32PipePoll Poll(nint handle, int maximumBytes);
}

internal sealed class Win32ProtocolReadHandleLease : IDisposable
{
    private readonly IWin32KernelHandleCloser _closer;
    private nint _standardOutputRead;
    private nint _standardErrorRead;

    internal Win32ProtocolReadHandleLease(
        IWin32KernelHandleCloser closer,
        nint standardOutputRead,
        nint standardErrorRead)
    {
        _closer = closer;
        _standardOutputRead = standardOutputRead is not 0 and not -1
            ? standardOutputRead
            : throw new ArgumentOutOfRangeException(nameof(standardOutputRead));
        _standardErrorRead = standardErrorRead is not 0 and not -1 &&
            standardErrorRead != standardOutputRead
            ? standardErrorRead
            : throw new ArgumentOutOfRangeException(nameof(standardErrorRead));
    }

    internal nint StandardOutputRead => _standardOutputRead != 0
        ? _standardOutputRead
        : throw new ObjectDisposedException(nameof(Win32ProtocolReadHandleLease));

    internal nint StandardErrorRead => _standardErrorRead != 0
        ? _standardErrorRead
        : throw new ObjectDisposedException(nameof(Win32ProtocolReadHandleLease));

    public void Dispose()
    {
        var standardOutputRead = Interlocked.Exchange(ref _standardOutputRead, 0);
        var standardErrorRead = Interlocked.Exchange(ref _standardErrorRead, 0);
        Exception? failure = null;
        if (standardOutputRead != 0)
        {
            TryClose(standardOutputRead, "stdout", ref failure);
        }
        if (standardErrorRead != 0)
        {
            TryClose(standardErrorRead, "stderr", ref failure);
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private void TryClose(nint handle, string stream, ref Exception? failure)
    {
        try
        {
            if (!_closer.CloseKernelHandle(handle))
            {
                throw new Win32Exception($"Closing retained parent {stream} handle failed.");
            }
        }
        catch (Exception exception)
        {
            failure = failure is null ? exception : new AggregateException(failure, exception);
        }
    }
}

internal sealed record Win32BoundedProtocolCapture(
    ReadOnlyMemory<byte> StandardOutputBytes,
    ReadOnlyMemory<byte> StandardErrorBytes,
    long StandardOutputBytesObserved,
    long StandardErrorBytesObserved,
    bool StandardOutputEof,
    bool StandardErrorEof,
    EvidenceProtocolCapture Protocol);

internal sealed class Win32BoundedPipeCapture(
    IWin32PipeReadApi pipeApi,
    IWin32MonotonicClock clock)
{
    internal const int RequiredStandardOutputCapBytes = 8_192;
    internal const int RequiredStandardErrorCapBytes = 65_536;
    private const int MaximumReadBytes = 4_096;
    private static readonly TimeSpan RequiredRunDeadline = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollDelay = TimeSpan.FromMilliseconds(5);
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    internal async Task<Win32BoundedProtocolCapture> CaptureAsync(
        Win32LaunchHandleLease launchHandles,
        Win32JobDeadline deadline,
        int standardOutputCapBytes,
        int standardErrorCapBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(launchHandles);
        if (standardOutputCapBytes != RequiredStandardOutputCapBytes ||
            standardErrorCapBytes != RequiredStandardErrorCapBytes ||
            deadline.Timeout != RequiredRunDeadline)
        {
            throw new InvalidOperationException("Pipe capture did not receive the exact admitted caps and run deadline.");
        }

        Win32ProtocolReadHandleLease? readHandles = null;
        Win32PipeDrainResult standardOutput = default;
        Win32PipeDrainResult standardError = default;
        Exception? failure = null;
        try
        {
            readHandles = launchHandles.TransferProtocolReadHandles();
            using var stop = new CancellationTokenSource();
            var standardOutputTask = Task.Run(
                () => Drain(
                    readHandles.StandardOutputRead,
                    standardOutputCapBytes,
                    deadline,
                    stop,
                    cancellationToken),
                CancellationToken.None);
            var standardErrorTask = Task.Run(
                () => Drain(
                    readHandles.StandardErrorRead,
                    standardErrorCapBytes,
                    deadline,
                    stop,
                    cancellationToken),
                CancellationToken.None);
            await Task.WhenAll(standardOutputTask, standardErrorTask).ConfigureAwait(false);
            standardOutput = standardOutputTask.Result;
            standardError = standardErrorTask.Result;
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            if (readHandles is not null) TryCleanup(readHandles.Dispose, ref failure);
        }

        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        cancellationToken.ThrowIfCancellationRequested();

        var timedOut = standardOutput.TimedOut || standardError.TimedOut;
        var standardOutputCapExceeded = standardOutput.CapExceeded;
        var standardErrorCapExceeded = standardError.CapExceeded;
        string standardOutputText = string.Empty;
        string standardErrorText = string.Empty;
        if (!timedOut && !standardOutputCapExceeded && !standardErrorCapExceeded)
        {
            if (!standardOutput.EndOfFile || !standardError.EndOfFile)
            {
                throw new InvalidDataException("Both protocol pipes must reach EOF before validation.");
            }
            standardOutputText = DecodeStrict(standardOutput.Bytes, "stdout");
            standardErrorText = DecodeStrict(standardError.Bytes, "stderr");
            if (!DebugEvidenceCaptureProtocol.TryParseBaseProjectileCompletion(
                    standardOutputText,
                    out _))
            {
                throw new InvalidDataException(
                    "Stdout must contain exactly one LF-terminated base-projectile completion marker.");
            }
        }

        return new Win32BoundedProtocolCapture(
            standardOutput.Bytes,
            standardError.Bytes,
            standardOutput.BytesObserved,
            standardError.BytesObserved,
            standardOutput.EndOfFile,
            standardError.EndOfFile,
            new EvidenceProtocolCapture(
                standardOutputText,
                standardErrorText,
                timedOut,
                standardOutputCapExceeded,
                standardErrorCapExceeded));
    }

    private Win32PipeDrainResult Drain(
        nint handle,
        int capBytes,
        Win32JobDeadline deadline,
        CancellationTokenSource stop,
        CancellationToken callerCancellation)
    {
        using var bytes = new MemoryStream(capacity: Math.Min(capBytes, MaximumReadBytes));
        long observed = 0;
        try
        {
            while (true)
            {
                callerCancellation.ThrowIfCancellationRequested();
                if (stop.IsCancellationRequested)
                {
                    return Win32PipeDrainResult.Stopped(bytes.ToArray(), observed);
                }

                var elapsed = clock.GetElapsedTime(deadline.StartingTimestamp);
                if (elapsed >= deadline.Timeout)
                {
                    stop.Cancel();
                    return Win32PipeDrainResult.Deadline(bytes.ToArray(), observed);
                }

                // The cap counts every raw byte, including stdout's terminal LF. Exactly cap bytes
                // are accepted. Polling for one sentinel byte beyond remaining capacity detects
                // cap+1 while retaining only the exact capped prefix, never unbounded output.
                var retained = checked((int)bytes.Length);
                var maximumPollBytes = Math.Min(MaximumReadBytes, checked(capBytes - retained + 1));
                var read = pipeApi.Poll(handle, maximumPollBytes);
                switch (read.Kind)
                {
                    case Win32PipePollKind.NoData:
                        DelayWithinDeadline(deadline);
                        break;
                    case Win32PipePollKind.EndOfFile:
                        return Win32PipeDrainResult.Eof(bytes.ToArray(), observed);
                    case Win32PipePollKind.Data:
                        if (read.Data.Length == 0)
                        {
                            return Win32PipeDrainResult.Eof(bytes.ToArray(), observed);
                        }
                        if (read.Data.Length > maximumPollBytes)
                        {
                            throw new InvalidDataException("Pipe read shim exceeded the requested bounded read size.");
                        }
                        observed = checked(observed + read.Data.Length);
                        var accepted = Math.Min(read.Data.Length, capBytes - retained);
                        if (accepted > 0) bytes.Write(read.Data, 0, accepted);
                        if (accepted != read.Data.Length)
                        {
                            stop.Cancel();
                            return Win32PipeDrainResult.OverCap(bytes.ToArray(), observed);
                        }
                        break;
                    default:
                        throw new InvalidDataException("Pipe read shim returned an unknown state.");
                }
            }
        }
        catch
        {
            stop.Cancel();
            throw;
        }
    }

    private void DelayWithinDeadline(Win32JobDeadline deadline)
    {
        var elapsed = clock.GetElapsedTime(deadline.StartingTimestamp);
        if (elapsed >= deadline.Timeout) return;
        var remaining = deadline.Timeout - elapsed;
        clock.Delay(remaining < PollDelay ? remaining : PollDelay);
    }

    private static string DecodeStrict(byte[] bytes, string stream)
    {
        if (bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble))
        {
            throw new InvalidDataException($"{stream} must not contain a UTF-8 BOM.");
        }
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException($"{stream} was not strict UTF-8.", exception);
        }
    }

    private static void TryCleanup(Action cleanup, ref Exception? failure)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            failure = failure is null ? exception : new AggregateException(failure, exception);
        }
    }

    private readonly record struct Win32PipeDrainResult(
        byte[] Bytes,
        long BytesObserved,
        bool EndOfFile,
        bool CapExceeded,
        bool TimedOut)
    {
        internal static Win32PipeDrainResult Eof(byte[] bytes, long observed) =>
            new(bytes, observed, EndOfFile: true, CapExceeded: false, TimedOut: false);

        internal static Win32PipeDrainResult OverCap(byte[] bytes, long observed) =>
            new(bytes, observed, EndOfFile: false, CapExceeded: true, TimedOut: false);

        internal static Win32PipeDrainResult Deadline(byte[] bytes, long observed) =>
            new(bytes, observed, EndOfFile: false, CapExceeded: false, TimedOut: true);

        internal static Win32PipeDrainResult Stopped(byte[] bytes, long observed) =>
            new(bytes, observed, EndOfFile: false, CapExceeded: false, TimedOut: false);
    }
}

internal sealed class Win32PipeReadApi : IWin32PipeReadApi
{
    private const int ErrorBrokenPipe = 109;
    private const int ErrorNoData = 232;

    public Win32PipePoll Poll(nint handle, int maximumBytes)
    {
        if (maximumBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        if (!Win32PipeNativeMethods.PeekNamedPipe(
                handle,
                0,
                0,
                out _,
                out var available,
                out _))
        {
            return EndOfFileOrThrow("PeekNamedPipe");
        }
        if (available == 0) return Win32PipePoll.NoData();

        var bytes = new byte[Math.Min(maximumBytes, checked((int)available))];
        if (!Win32PipeNativeMethods.ReadFile(
                handle,
                bytes,
                checked((uint)bytes.Length),
                out var read,
                0))
        {
            return EndOfFileOrThrow("ReadFile");
        }
        if (read == 0) return Win32PipePoll.EndOfFile();
        if (read != bytes.Length) Array.Resize(ref bytes, checked((int)read));
        return Win32PipePoll.Bytes(bytes);
    }

    private static Win32PipePoll EndOfFileOrThrow(string operation)
    {
        var error = Marshal.GetLastPInvokeError();
        if (error is ErrorBrokenPipe or ErrorNoData) return Win32PipePoll.EndOfFile();
        throw new Win32Exception(error, $"{operation} failed for an evidence protocol pipe.");
    }
}

internal static class Win32PipeNativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PeekNamedPipe(
        nint pipe,
        nint buffer,
        uint bufferSize,
        out uint bytesRead,
        out uint totalBytesAvailable,
        out uint bytesLeftThisMessage);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadFile(
        nint file,
        byte[] buffer,
        uint bytesToRead,
        out uint bytesRead,
        nint overlapped);
}
