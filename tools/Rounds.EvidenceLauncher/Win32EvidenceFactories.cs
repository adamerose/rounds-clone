using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Rounds.Game;

namespace Rounds.EvidenceLauncher;

internal sealed record Win32MonitorSnapshot(
    string DeviceName,
    EvidencePixelBounds PhysicalBounds);

internal interface IWin32TopologyApi
{
    bool SetPerMonitorV2DpiAwareness();

    bool IsPerMonitorV2DpiAware();

    IReadOnlyList<Win32MonitorSnapshot> EnumerateMonitors();
}

internal sealed class Win32TopologyFactory(IWin32TopologyApi api)
{
    internal EvidenceMonitorFacts ReadRequiredMonitor()
    {
        // SetProcessDpiAwarenessContext reports false once awareness was established
        // earlier in the process. The effective context is therefore the authority,
        // but the set attempt and exact PMv2 query must both precede enumeration.
        _ = api.SetPerMonitorV2DpiAwareness();
        if (!api.IsPerMonitorV2DpiAware())
        {
            throw new InvalidOperationException("PER_MONITOR_AWARE_V2 could not be established.");
        }

        var monitors = api.EnumerateMonitors();
        var matches = monitors
            .Select((monitor, ordinal) => (Monitor: monitor, Ordinal: ordinal))
            .Where(candidate => string.Equals(
                candidate.Monitor.DeviceName,
                BaseProjectileEvidenceLaunchPlanner.DisplayDevice,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one {BaseProjectileEvidenceLaunchPlanner.DisplayDevice} monitor; found {matches.Length}.");
        }

        var match = matches[0];
        return new EvidenceMonitorFacts(
            match.Monitor.DeviceName,
            match.Ordinal,
            match.Monitor.PhysicalBounds,
            PerMonitorV2DpiAware: true);
    }
}

internal interface IWin32DedicatedWorkerContext
{
    bool IsCurrentDedicatedWorker { get; }
}

internal sealed class Win32DedicatedWorker : IWin32DedicatedWorkerContext
{
    private int _workerManagedThreadId;
    private int _running;

    public bool IsCurrentDedicatedWorker =>
        Environment.CurrentManagedThreadId == Volatile.Read(ref _workerManagedThreadId);

    internal T Run<T>(Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (Interlocked.Exchange(ref _running, 1) != 0)
        {
            throw new InvalidOperationException("The native-boundary worker is already running.");
        }

        T? result = default;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            Volatile.Write(ref _workerManagedThreadId, Environment.CurrentManagedThreadId);
            try
            {
                result = operation();
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            finally
            {
                Volatile.Write(ref _workerManagedThreadId, 0);
            }
        })
        {
            IsBackground = true,
            Name = "Rounds evidence native boundary",
        };

        try
        {
            thread.Start();
            thread.Join();
        }
        finally
        {
            Volatile.Write(ref _workerManagedThreadId, 0);
            Volatile.Write(ref _running, 0);
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        return result!;
    }
}

internal interface IWin32DesktopApi : IWin32DesktopCloser
{
    nint OpenInputDesktop();

    nint CreateDesktop(string name, uint desiredAccess);

    string ReadDesktopName(nint desktop);
}

internal sealed partial class Win32DesktopFactory(
    IWin32DesktopApi api,
    IWin32DedicatedWorkerContext workerContext)
{
    private const string DesktopPrefix = "RoundsEvidence-";

    [GeneratedRegex("^[0-9a-f]{32}$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex NoncePattern();

    internal string ReadInputDesktopIdentity()
    {
        var desktop = api.OpenInputDesktop();
        if (desktop == 0 || desktop == -1)
        {
            throw new Win32Exception("OpenInputDesktop failed.");
        }

        Exception? failure = null;
        string? name = null;
        try
        {
            name = RequireDesktopName(api.ReadDesktopName(desktop));
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            try
            {
                if (!api.CloseDesktop(desktop))
                {
                    throw new Win32Exception("CloseDesktop failed for input-desktop identity handle.");
                }
            }
            catch (Exception closeException)
            {
                failure = failure is null
                    ? closeException
                    : new AggregateException(failure, closeException);
            }
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        return name!;
    }

    internal IEvidenceDesktopLease CreatePrivateDesktopForNonce(string nonce)
    {
        if (!workerContext.IsCurrentDedicatedWorker)
        {
            throw new InvalidOperationException(
                "Private evidence desktop creation requires the dedicated native-boundary worker.");
        }
        if (!NoncePattern().IsMatch(nonce))
        {
            throw new ArgumentException("Desktop nonce must be 32 lowercase hexadecimal characters.", nameof(nonce));
        }

        var expectedName = DesktopPrefix + nonce;
        var desktop = api.CreateDesktop(expectedName, Win32EvidenceConstants.RequiredDesktopAccess);
        if (desktop == 0 || desktop == -1)
        {
            throw new Win32Exception("CreateDesktopW failed for private evidence desktop.");
        }

        try
        {
            var actualName = RequireDesktopName(api.ReadDesktopName(desktop));
            if (!string.Equals(actualName, expectedName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Created desktop name did not match the nonce-bound request.");
            }
            return new Win32DesktopLease(api, desktop, actualName);
        }
        catch (Exception failure)
        {
            try
            {
                if (!api.CloseDesktop(desktop))
                {
                    throw new Win32Exception("CloseDesktop failed after private-desktop validation failure.");
                }
            }
            catch (Exception closeException)
            {
                throw new AggregateException(failure, closeException);
            }
            ExceptionDispatchInfo.Capture(failure).Throw();
            throw;
        }
    }

    private static string RequireDesktopName(string name) =>
        !string.IsNullOrWhiteSpace(name) && !name.Contains('\0')
            ? name
            : throw new InvalidOperationException("Desktop identity was empty or malformed.");
}

internal sealed class Win32TopologyApi : IWin32TopologyApi
{
    public bool SetPerMonitorV2DpiAwareness() =>
        Win32FactoryNativeMethods.SetProcessDpiAwarenessContext(
            Win32EvidenceConstants.DpiAwarenessContextPerMonitorAwareV2);

    public bool IsPerMonitorV2DpiAware() =>
        Win32FactoryNativeMethods.AreDpiAwarenessContextsEqual(
            Win32FactoryNativeMethods.GetThreadDpiAwarenessContext(),
            Win32EvidenceConstants.DpiAwarenessContextPerMonitorAwareV2);

    public IReadOnlyList<Win32MonitorSnapshot> EnumerateMonitors()
    {
        var monitors = new List<Win32MonitorSnapshot>();
        Win32FactoryNativeMethods.MonitorEnumCallback callback =
            (monitor, _, _, _) =>
            {
                var info = new Win32MonitorInfoEx
                {
                    Size = checked((uint)Marshal.SizeOf<Win32MonitorInfoEx>()),
                    DeviceName = string.Empty,
                };
                if (!Win32FactoryNativeMethods.GetMonitorInfoW(monitor, ref info))
                {
                    return false;
                }
                monitors.Add(new Win32MonitorSnapshot(
                    info.DeviceName,
                    new EvidencePixelBounds(
                        info.Monitor.Left,
                        info.Monitor.Top,
                        checked(info.Monitor.Right - info.Monitor.Left),
                        checked(info.Monitor.Bottom - info.Monitor.Top))));
                return true;
            };
        if (!Win32FactoryNativeMethods.EnumDisplayMonitors(0, 0, callback, 0))
        {
            throw new Win32Exception("EnumDisplayMonitors/GetMonitorInfoW failed.");
        }
        return monitors.AsReadOnly();
    }
}

internal sealed class Win32DesktopApi : IWin32DesktopApi
{
    public nint OpenInputDesktop() =>
        Win32FactoryNativeMethods.OpenInputDesktop(
            0,
            inherit: false,
            Win32EvidenceConstants.DesktopReadObjects);

    public nint CreateDesktop(string name, uint desiredAccess) =>
        Win32EvidenceNativeMethods.CreateDesktopW(name, 0, 0, 0, desiredAccess, 0);

    public string ReadDesktopName(nint desktop)
    {
        _ = Win32EvidenceNativeMethods.GetUserObjectInformationW(
            desktop,
            Win32EvidenceConstants.UoiName,
            null,
            0,
            out var requiredBytes);
        if (requiredBytes < sizeof(char) || requiredBytes % sizeof(char) != 0)
        {
            throw new Win32Exception("Desktop name size query failed.");
        }

        var characters = new char[requiredBytes / sizeof(char)];
        if (!Win32EvidenceNativeMethods.GetUserObjectInformationW(
                desktop,
                Win32EvidenceConstants.UoiName,
                characters,
                requiredBytes,
                out _))
        {
            throw new Win32Exception("Desktop name query failed.");
        }
        return new string(characters).TrimEnd('\0');
    }

    public bool CloseDesktop(nint desktop) => Win32EvidenceNativeMethods.CloseDesktop(desktop);
}

internal static class Win32FactoryNativeMethods
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal delegate bool MonitorEnumCallback(
        nint monitor,
        nint deviceContext,
        nint monitorRectangle,
        nint data);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetProcessDpiAwarenessContext(nint dpiContext);

    [DllImport("user32.dll")]
    internal static extern nint GetThreadDpiAwarenessContext();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AreDpiAwarenessContextsEqual(nint first, nint second);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplayMonitors(
        nint deviceContext,
        nint clipRectangle,
        MonitorEnumCallback callback,
        nint data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfoW(nint monitor, ref Win32MonitorInfoEx monitorInfo);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint OpenInputDesktop(
        uint flags,
        [MarshalAs(UnmanagedType.Bool)] bool inherit,
        uint desiredAccess);
}
