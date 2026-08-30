using System.ComponentModel;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Rounds.Game;

namespace Rounds.EvidenceLauncher;

internal static class Win32EvidenceConstants
{
    internal const uint ProcThreadAttributeHandleList = 0x00020002;
    internal const uint HandleFlagInherit = 0x00000001;
    internal const uint StartfUseStdHandles = 0x00000100;
    internal const uint DesktopCreateWindow = 0x0002;
    internal const uint DesktopReadObjects = 0x0001;
    internal const uint DesktopWriteObjects = 0x0080;
    internal const uint DesktopSwitchDesktop = 0x0100;
    internal const uint RequiredDesktopAccess =
        DesktopCreateWindow | DesktopReadObjects | DesktopWriteObjects;
    internal static readonly nint DpiAwarenessContextPerMonitorAwareV2 = -4;
    internal const uint GenericRead = 0x80000000;
    internal const uint FileShareRead = 0x00000001;
    internal const uint FileShareWrite = 0x00000002;
    internal const uint FileShareDelete = 0x00000004;
    internal const uint OpenExisting = 3;
    internal const uint FileAttributeDirectory = 0x00000010;
    internal const uint FileAttributeNormal = 0x00000080;
    internal const uint FileAttributeReparsePoint = 0x00000400;
    internal const uint FileFlagOpenReparsePoint = 0x00200000;
    internal const uint FileFlagBackupSemantics = 0x02000000;
    internal const uint JobObjectExtendedLimitInformation = 9;
    internal const uint JobObjectBasicAccountingInformation = 1;
    internal const uint JobObjectBasicProcessIdList = 3;
    internal const uint JobObjectLimitWorkingSet = 0x00000001;
    internal const uint JobObjectLimitActiveProcess = 0x00000008;
    internal const uint JobObjectLimitAffinity = 0x00000010;
    internal const uint JobObjectLimitPriorityClass = 0x00000020;
    internal const uint JobObjectLimitProcessMemory = 0x00000100;
    internal const uint JobObjectLimitJobMemory = 0x00000200;
    internal const uint JobObjectLimitKillOnJobClose = 0x00002000;
    internal const uint BelowNormalPriorityClass = 0x00004000;
    internal const uint WaitObject0 = 0;
    internal const uint WaitTimeout = 258;
    internal const uint ResumeThreadFailure = 0xffffffff;
    internal const uint Infinite = 0xffffffff;
    internal const uint TerminationFallbackWaitMilliseconds = 5_000;
    internal const int UoiName = 2;

    internal const EvidenceCreateProcessFlags RequiredCreateProcessFlags =
        EvidenceCreateProcessFlags.CreateSuspended |
        EvidenceCreateProcessFlags.CreateNoWindow |
        EvidenceCreateProcessFlags.CreateNewProcessGroup |
        EvidenceCreateProcessFlags.BelowNormalPriorityClass |
        EvidenceCreateProcessFlags.ExtendedStartupInfoPresent |
        EvidenceCreateProcessFlags.CreateUnicodeEnvironment;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Win32SecurityAttributes
{
    internal uint Length;
    internal nint SecurityDescriptor;
    [MarshalAs(UnmanagedType.Bool)] internal bool InheritHandle;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct Win32StartupInfo
{
    internal uint Size;
    internal string? Reserved;
    internal string? Desktop;
    internal string? Title;
    internal uint X;
    internal uint Y;
    internal uint XSize;
    internal uint YSize;
    internal uint XCountChars;
    internal uint YCountChars;
    internal uint FillAttribute;
    internal uint Flags;
    internal ushort ShowWindow;
    internal ushort Reserved2Count;
    internal nint Reserved2;
    internal nint StandardInput;
    internal nint StandardOutput;
    internal nint StandardError;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct Win32StartupInfoEx
{
    internal Win32StartupInfo StartupInfo;
    internal nint AttributeList;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Win32ProcessInformation
{
    internal nint Process;
    internal nint Thread;
    internal uint ProcessId;
    internal uint ThreadId;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Win32Rect
{
    internal int Left;
    internal int Top;
    internal int Right;
    internal int Bottom;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct Win32MonitorInfoEx
{
    internal uint Size;
    internal Win32Rect Monitor;
    internal Win32Rect Work;
    internal uint Flags;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] internal string DeviceName;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Win32JobBasicLimitInformation
{
    internal long PerProcessUserTimeLimit;
    internal long PerJobUserTimeLimit;
    internal uint LimitFlags;
    internal nuint MinimumWorkingSetSize;
    internal nuint MaximumWorkingSetSize;
    internal uint ActiveProcessLimit;
    internal nuint Affinity;
    internal uint PriorityClass;
    internal uint SchedulingClass;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Win32IoCounters
{
    internal ulong ReadOperationCount;
    internal ulong WriteOperationCount;
    internal ulong OtherOperationCount;
    internal ulong ReadTransferCount;
    internal ulong WriteTransferCount;
    internal ulong OtherTransferCount;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Win32JobExtendedLimitInformation
{
    internal Win32JobBasicLimitInformation BasicLimitInformation;
    internal Win32IoCounters IoInfo;
    internal nuint ProcessMemoryLimit;
    internal nuint JobMemoryLimit;
    internal nuint PeakProcessMemoryUsed;
    internal nuint PeakJobMemoryUsed;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Win32JobBasicAccountingInformation
{
    internal long TotalUserTime;
    internal long TotalKernelTime;
    internal long ThisPeriodTotalUserTime;
    internal long ThisPeriodTotalKernelTime;
    internal uint TotalPageFaultCount;
    internal uint TotalProcesses;
    internal uint ActiveProcesses;
    internal uint TotalTerminatedProcesses;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Win32FileIdInfo
{
    internal ulong VolumeSerialNumber;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] internal byte[] FileId;
}

internal interface IWin32DesktopCloser
{
    bool CloseDesktop(nint desktop);
}

internal interface IWin32KernelHandleCloser
{
    bool CloseKernelHandle(nint handle);
}

internal interface IWin32EvidenceApi : IWin32DesktopCloser, IWin32KernelHandleCloser
{
    bool TerminateProcess(nint process, uint exitCode);
    uint WaitForSingleObject(nint handle, uint milliseconds);
    bool WriteFile(nint handle, ReadOnlySpan<byte> data, out uint written);
}

internal sealed class Win32EvidenceApi : IWin32EvidenceApi
{
    public bool CloseKernelHandle(nint handle) => Win32EvidenceNativeMethods.CloseHandle(handle);

    public bool CloseDesktop(nint desktop) => Win32EvidenceNativeMethods.CloseDesktop(desktop);

    public bool TerminateProcess(nint process, uint exitCode) =>
        Win32EvidenceNativeMethods.TerminateProcess(process, exitCode);

    public uint WaitForSingleObject(nint handle, uint milliseconds) =>
        Win32EvidenceNativeMethods.WaitForSingleObject(handle, milliseconds);

    public bool WriteFile(nint handle, ReadOnlySpan<byte> data, out uint written)
    {
        var bytes = data.ToArray();
        return Win32EvidenceNativeMethods.WriteFile(
            handle,
            bytes,
            checked((uint)bytes.Length),
            out written,
            0);
    }
}

internal sealed class Win32ExecutableLease(
    IWin32KernelHandleCloser api,
    nint handle,
    EvidenceOpenedExecutableIdentity identity) : IEvidenceExecutableLease
{
    private nint _handle = RequireHandle(handle, nameof(handle));

    public EvidenceOpenedExecutableIdentity Identity { get; } = identity;

    internal nint DangerousHandle => _handle != 0
        ? _handle
        : throw new ObjectDisposedException(nameof(Win32ExecutableLease));

    public void Dispose()
    {
        var owned = Interlocked.Exchange(ref _handle, 0);
        if (owned != 0 && !api.CloseKernelHandle(owned))
        {
            throw new Win32Exception("CloseHandle failed for retained executable lease.");
        }
    }

    private static nint RequireHandle(nint value, string name) =>
        value != 0 && value != -1 ? value : throw new ArgumentOutOfRangeException(name);
}

internal sealed class Win32DesktopLease(
    IWin32DesktopCloser api,
    nint handle,
    string name) : IEvidenceDesktopLease
{
    private nint _handle = handle != 0 ? handle : throw new ArgumentOutOfRangeException(nameof(handle));

    public string Name { get; } = !string.IsNullOrWhiteSpace(name)
        ? name
        : throw new ArgumentException("Desktop name is required.", nameof(name));

    internal nint DangerousHandle => _handle != 0
        ? _handle
        : throw new ObjectDisposedException(nameof(Win32DesktopLease));

    public void Dispose()
    {
        var owned = Interlocked.Exchange(ref _handle, 0);
        if (owned != 0 && !api.CloseDesktop(owned))
        {
            throw new Win32Exception("CloseDesktop failed for private evidence desktop.");
        }
    }
}

internal sealed class Win32ProcessLease(
    IWin32EvidenceApi api,
    nint process,
    nint primaryThread) : EvidenceProcessLease
{
    private nint _process = process != 0 ? process : throw new ArgumentOutOfRangeException(nameof(process));
    private nint _primaryThread = primaryThread != 0
        ? primaryThread
        : throw new ArgumentOutOfRangeException(nameof(primaryThread));

    internal nint DangerousProcessHandle => _process != 0
        ? _process
        : throw new ObjectDisposedException(nameof(Win32ProcessLease));

    internal nint DangerousPrimaryThreadHandle => _primaryThread != 0
        ? _primaryThread
        : throw new ObjectDisposedException(nameof(Win32ProcessLease));

    internal bool PrimaryThreadWasResumed { get; private set; }

    internal void MarkPrimaryThreadResumed()
    {
        if (PrimaryThreadWasResumed)
        {
            throw new InvalidOperationException("Primary thread was already resumed.");
        }
        _ = DangerousPrimaryThreadHandle;
        PrimaryThreadWasResumed = true;
    }

    protected override void TerminateAndWaitForExit()
    {
        var terminationRequested = api.TerminateProcess(DangerousProcessHandle, 1);
        var wait = api.WaitForSingleObject(
            DangerousProcessHandle,
            Win32EvidenceConstants.TerminationFallbackWaitMilliseconds);
        if (!terminationRequested || wait != Win32EvidenceConstants.WaitObject0)
        {
            throw new Win32Exception(
                $"Evidence child termination fallback failed (terminate={terminationRequested}, wait={wait}).");
        }
    }

    protected override void ReleaseProcessAndThreadHandles()
    {
        var thread = Interlocked.Exchange(ref _primaryThread, 0);
        var process = Interlocked.Exchange(ref _process, 0);
        var threadClosed = thread == 0 || api.CloseKernelHandle(thread);
        var processClosed = process == 0 || api.CloseKernelHandle(process);
        if (!threadClosed || !processClosed)
        {
            throw new Win32Exception("Closing evidence process/thread handles failed.");
        }
    }
}

internal sealed class Win32JobLease(IWin32EvidenceApi api, nint handle) : IEvidenceJobLease
{
    private nint _handle = handle != 0 ? handle : throw new ArgumentOutOfRangeException(nameof(handle));

    internal nint DangerousHandle => _handle != 0
        ? _handle
        : throw new ObjectDisposedException(nameof(Win32JobLease));

    internal bool Configured { get; private set; }

    internal bool Assigned { get; private set; }

    internal bool Resumed { get; private set; }

    internal bool EmptyProven { get; private set; }

    internal void MarkConfigured()
    {
        _ = DangerousHandle;
        if (Configured) throw new InvalidOperationException("Job was already configured.");
        Configured = true;
    }

    internal void MarkAssigned()
    {
        _ = DangerousHandle;
        if (!Configured || Assigned) throw new InvalidOperationException("Job assignment state was invalid.");
        Assigned = true;
    }

    internal void MarkResumed()
    {
        _ = DangerousHandle;
        if (!Assigned || Resumed) throw new InvalidOperationException("Job resume state was invalid.");
        Resumed = true;
    }

    internal void MarkEmptyProven()
    {
        _ = DangerousHandle;
        if (!Assigned || !Resumed) throw new InvalidOperationException("A non-running job cannot be proven empty.");
        EmptyProven = true;
    }

    public void Dispose()
    {
        var owned = Interlocked.Exchange(ref _handle, 0);
        if (owned != 0 && !api.CloseKernelHandle(owned))
        {
            throw new Win32Exception("CloseHandle failed for kill-on-close evidence job.");
        }
    }
}

internal sealed class Win32LaunchHandleLease : IEvidenceLaunchHandleLease
{
    private readonly IWin32EvidenceApi _api;
    private readonly nint[] _parentReadHandles;
    private readonly nint[] _childHandleCopies;
    private nint _acknowledgementWrite;
    private bool _processCreationCompleted;
    private bool _disposed;

    internal Win32LaunchHandleLease(
        IWin32EvidenceApi api,
        nint standardInputRead,
        nint standardOutputRead,
        nint standardOutputWrite,
        nint standardErrorRead,
        nint standardErrorWrite,
        nint acknowledgementRead,
        nint acknowledgementWrite)
    {
        _api = api;
        var allHandles = new[]
        {
            standardInputRead,
            standardOutputRead,
            standardOutputWrite,
            standardErrorRead,
            standardErrorWrite,
            acknowledgementRead,
            acknowledgementWrite,
        };
        if (allHandles.Distinct().Count() != allHandles.Length ||
            allHandles.Any(handle => handle == 0 || handle == -1))
        {
            throw new ArgumentOutOfRangeException(nameof(standardInputRead));
        }
        _parentReadHandles = new[] { standardOutputRead, standardErrorRead };
        _childHandleCopies = new[]
        {
            standardInputRead,
            standardOutputWrite,
            standardErrorWrite,
            acknowledgementRead,
        };
        ChildHandleValues = new Win32ChildHandleValues(
            standardInputRead,
            standardOutputWrite,
            standardErrorWrite,
            acknowledgementRead);
        AcknowledgementReadHandleValue = ((nuint)acknowledgementRead).ToString(CultureInfo.InvariantCulture);
        _acknowledgementWrite = acknowledgementWrite;
    }

    public IReadOnlyList<EvidenceChildHandleDescriptor> ChildHandles { get; } =
        Array.AsReadOnly(new[]
        {
            new EvidenceChildHandleDescriptor(EvidenceChildHandle.StandardInputRead, true),
            new EvidenceChildHandleDescriptor(EvidenceChildHandle.StandardOutputWrite, true),
            new EvidenceChildHandleDescriptor(EvidenceChildHandle.StandardErrorWrite, true),
            new EvidenceChildHandleDescriptor(EvidenceChildHandle.AcknowledgementRead, true),
        });

    public bool ParentEndpointsAreNonInheritable => true;

    public string AcknowledgementReadHandleValue { get; }

    internal Win32ChildHandleValues ChildHandleValues { get; }

    internal bool ReadyForProcessCreation => !_disposed && !_processCreationCompleted;

    public void CompleteSuccessfulProcessCreation()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_processCreationCompleted) return;
        _processCreationCompleted = true;

        var failures = 0;
        for (var index = 0; index < _childHandleCopies.Length; index++)
        {
            var handle = Interlocked.Exchange(ref _childHandleCopies[index], 0);
            if (handle != 0 && !_api.CloseKernelHandle(handle)) failures++;
        }
        if (failures != 0)
        {
            throw new Win32Exception(
                $"{failures} parent copy/copies of inherited child handles failed to close.");
        }
    }

    public void WriteAcknowledgementAndClose(byte value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var writer = Interlocked.Exchange(ref _acknowledgementWrite, 0);
        if (writer == 0)
        {
            throw new InvalidOperationException("Acknowledgement writer was already closed.");
        }

        Exception? failure = null;
        try
        {
            if (!_api.WriteFile(writer, new[] { value }, out var written) || written != 1)
            {
                failure = new Win32Exception("Exact acknowledgement write failed.");
            }
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            try
            {
                if (!_api.CloseKernelHandle(writer))
                {
                    throw new Win32Exception("Closing acknowledgement writer failed.");
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
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var failures = 0;
        var writer = Interlocked.Exchange(ref _acknowledgementWrite, 0);
        if (writer != 0 && !_api.CloseKernelHandle(writer)) failures++;
        foreach (ref var childHandle in _childHandleCopies.AsSpan())
        {
            var handle = Interlocked.Exchange(ref childHandle, 0);
            if (handle != 0 && !_api.CloseKernelHandle(handle)) failures++;
        }
        foreach (ref var parentHandle in _parentReadHandles.AsSpan())
        {
            var handle = Interlocked.Exchange(ref parentHandle, 0);
            if (handle != 0 && !_api.CloseKernelHandle(handle)) failures++;
        }
        if (failures != 0)
        {
            throw new Win32Exception($"{failures} evidence pipe handle(s) failed to close.");
        }
    }
}

internal static class Win32EvidenceNativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(nint handle);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseDesktop(nint desktop);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TerminateProcess(nint process, uint exitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint WaitForSingleObject(nint handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WriteFile(
        nint file,
        byte[] buffer,
        uint bytesToWrite,
        out uint bytesWritten,
        nint overlapped);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        nint securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CreateDesktopW(
        string desktop,
        nint device,
        nint deviceMode,
        uint flags,
        uint desiredAccess,
        nint securityAttributes);

    [DllImport("user32.dll", SetLastError = true)]
    internal static extern nint GetThreadDesktop(uint threadId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetUserObjectInformationW(
        nint handle,
        int index,
        char[]? information,
        uint length,
        out uint needed);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CreateProcessW(
        string applicationName,
        nint commandLine,
        nint processAttributes,
        nint threadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandles,
        uint creationFlags,
        nint environment,
        string currentDirectory,
        ref Win32StartupInfoEx startupInfo,
        out Win32ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool InitializeProcThreadAttributeList(
        nint attributeList,
        int attributeCount,
        uint flags,
        ref nuint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UpdateProcThreadAttribute(
        nint attributeList,
        uint flags,
        nuint attribute,
        nint value,
        nuint size,
        nint previousValue,
        nint returnSize);

    [DllImport("kernel32.dll")]
    internal static extern void DeleteProcThreadAttributeList(nint attributeList);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint CreateJobObjectW(nint jobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetInformationJobObject(
        nint job,
        uint informationClass,
        nint information,
        uint informationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AssignProcessToJobObject(nint job, nint process);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryInformationJobObject(
        nint job,
        uint informationClass,
        nint information,
        uint informationLength,
        out uint returnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CreatePipe(
        out nint readPipe,
        out nint writePipe,
        ref Win32SecurityAttributes pipeAttributes,
        uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetHandleInformation(nint handle, uint mask, uint flags);
}
