using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Rounds.Game;

internal enum EvidenceAcknowledgementReadKind
{
    NoData,
    Data,
    Closed,
    Invalid,
}

internal readonly record struct EvidenceAcknowledgementRead(
    EvidenceAcknowledgementReadKind Kind,
    byte Value = 0);

internal interface IEvidenceAcknowledgementSource : IDisposable
{
    EvidenceAcknowledgementRead Poll();
}

internal static class EvidenceAcknowledgementReader
{
    public static Task<bool> WaitAsync(string? encodedHandle, TimeSpan timeout)
    {
        if (!OperatingSystem.IsWindows() || timeout <= TimeSpan.Zero ||
            !ulong.TryParse(encodedHandle, NumberStyles.None, CultureInfo.InvariantCulture, out var rawHandle) ||
            rawHandle == 0 || rawHandle > (ulong)nuint.MaxValue)
        {
            return Task.FromResult(false);
        }
        return WaitAsync(new NativeEvidenceAcknowledgementSource(rawHandle), timeout);
    }

    internal static async Task<bool> WaitAsync(
        IEvidenceAcknowledgementSource source,
        TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (timeout <= TimeSpan.Zero)
        {
            source.Dispose();
            return false;
        }

        using (source)
        {
            var elapsed = Stopwatch.StartNew();
            var acknowledgementSeen = false;
            while (elapsed.Elapsed < timeout)
            {
                var read = source.Poll();
                if (read.Kind == EvidenceAcknowledgementReadKind.Closed)
                {
                    return acknowledgementSeen;
                }
                if (read.Kind is EvidenceAcknowledgementReadKind.Invalid)
                {
                    return false;
                }
                if (read.Kind == EvidenceAcknowledgementReadKind.Data)
                {
                    if (acknowledgementSeen ||
                        read.Value != DebugEvidenceCaptureProtocol.EvidenceAcknowledgement)
                    {
                        return false;
                    }
                    acknowledgementSeen = true;
                    continue;
                }
                await Task.Delay(10).ConfigureAwait(true);
            }
            return false;
        }
    }
}

internal sealed class NativeEvidenceAcknowledgementSource : IEvidenceAcknowledgementSource
{
    private const int BrokenPipe = 109;
    private const int PipeNotConnected = 233;
    private SafeFileHandle? _handle;

    public NativeEvidenceAcknowledgementSource(ulong rawHandle)
    {
        _handle = new SafeFileHandle((nint)(nuint)rawHandle, ownsHandle: true);
    }

    public EvidenceAcknowledgementRead Poll()
    {
        if (_handle is null || _handle.IsClosed || _handle.IsInvalid)
        {
            return new(EvidenceAcknowledgementReadKind.Invalid);
        }
        if (!PeekNamedPipe(_handle, IntPtr.Zero, 0, IntPtr.Zero, out var available, IntPtr.Zero))
        {
            var error = Marshal.GetLastWin32Error();
            return new(error is BrokenPipe or PipeNotConnected
                ? EvidenceAcknowledgementReadKind.Closed
                : EvidenceAcknowledgementReadKind.Invalid);
        }
        if (available == 0)
        {
            return new(EvidenceAcknowledgementReadKind.NoData);
        }
        if (available != 1)
        {
            return new(EvidenceAcknowledgementReadKind.Invalid);
        }

        var oneByte = new byte[1];
        return ReadFile(_handle, oneByte, 1, out var read, IntPtr.Zero) && read == 1
            ? new(EvidenceAcknowledgementReadKind.Data, oneByte[0])
            : new(EvidenceAcknowledgementReadKind.Invalid);
    }

    public void Dispose()
    {
        _handle?.Dispose();
        _handle = null;
    }

#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PeekNamedPipe(
        SafeFileHandle namedPipe,
        IntPtr buffer,
        uint bufferSize,
        IntPtr bytesRead,
        out uint totalBytesAvailable,
        IntPtr bytesLeftThisMessage);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadFile(
        SafeFileHandle file,
        byte[] buffer,
        uint bytesToRead,
        out uint bytesRead,
        IntPtr overlapped);
#pragma warning restore SYSLIB1054
}
