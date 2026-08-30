using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Godot;
using Microsoft.Win32.SafeHandles;

namespace Rounds.Game;

internal sealed class GodotEvidencePngDecoder : IAgentPlaytestRgba8Decoder
{
    public DecodedAgentPlaytestFrame Decode(ReadOnlySpan<byte> encodedPng)
    {
        using var image = new Image();
        var error = image.LoadPngFromBuffer(encodedPng.ToArray());
        if (error != Error.Ok)
        {
            throw new InvalidDataException($"Godot refused the evidence PNG with error {(int)error}.");
        }
        if (image.GetFormat() != Image.Format.Rgba8)
        {
            image.Convert(Image.Format.Rgba8);
        }
        return new DecodedAgentPlaytestFrame(image.GetWidth(), image.GetHeight(), image.GetData());
    }
}

internal readonly record struct EvidenceAssemblyIdentity(string Sha256, string Mvid)
{
    public static EvidenceAssemblyIdentity Current()
    {
        var assembly = typeof(Main).Assembly;
        var path = assembly.Location;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !File.Exists(path))
        {
            throw new IOException("The running evidence assembly has no attributable file.");
        }
        using var stream = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.Read);
        var sha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        return new EvidenceAssemblyIdentity(sha256, assembly.ManifestModule.ModuleVersionId.ToString("N"));
    }
}

internal static class EvidenceDesktopIdentityReader
{
    private const int UoiName = 2;

    public static string CurrentThreadDesktopName()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Desktop evidence requires Windows.");
        }
        var desktop = GetThreadDesktop(GetCurrentThreadId());
        if (desktop == IntPtr.Zero)
        {
            throw new IOException("The evidence thread desktop could not be opened.");
        }
        var name = new StringBuilder(256);
        if (!GetUserObjectInformation(desktop, UoiName, name, checked(name.Capacity * sizeof(char)), out _))
        {
            throw new IOException(
                "The evidence thread desktop name could not be read.",
                Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
        }
        return name.ToString();
    }

#pragma warning disable SYSLIB1054
    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr GetThreadDesktop(uint threadId);

    [DllImport("user32.dll", EntryPoint = "GetUserObjectInformationW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetUserObjectInformation(
        IntPtr objectHandle,
        int index,
        StringBuilder information,
        int length,
        out int needed);
#pragma warning restore SYSLIB1054
}

internal static class EvidenceAcknowledgementReader
{
    private const int BrokenPipe = 109;
    private const int PipeNotConnected = 233;

    public static async Task<bool> WaitAsync(string? encodedHandle, TimeSpan timeout)
    {
        if (!OperatingSystem.IsWindows() || timeout <= TimeSpan.Zero ||
            !ulong.TryParse(encodedHandle, NumberStyles.None, CultureInfo.InvariantCulture, out var rawHandle) ||
            rawHandle == 0 || rawHandle > (ulong)nuint.MaxValue)
        {
            return false;
        }

        var handle = new SafeFileHandle((nint)(nuint)rawHandle, ownsHandle: true);
        try
        {
            var deadline = DateTime.UtcNow + timeout;
            var oneByte = new byte[1];
            while (DateTime.UtcNow < deadline)
            {
                if (!PeekNamedPipe(handle, IntPtr.Zero, 0, IntPtr.Zero, out var available, IntPtr.Zero))
                {
                    _ = Marshal.GetLastWin32Error() is BrokenPipe or PipeNotConnected;
                    return false;
                }
                if (available > 0)
                {
                    return ReadFile(handle, oneByte, 1, out var read, IntPtr.Zero) &&
                        read == 1 && oneByte[0] == DebugEvidenceCaptureProtocol.EvidenceAcknowledgement;
                }
                await Task.Delay(10).ConfigureAwait(true);
            }
            return false;
        }
        finally
        {
            handle.Dispose();
        }
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
