using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;
using Rounds.Game;

namespace Rounds.EvidenceLauncher;

internal sealed record Win32ChildHandleValues(
    nint StandardInputRead,
    nint StandardOutputWrite,
    nint StandardErrorWrite,
    nint AcknowledgementRead)
{
    internal IReadOnlyList<nint> AsHandleList() => Array.AsReadOnly(new[]
    {
        StandardInputRead,
        StandardOutputWrite,
        StandardErrorWrite,
        AcknowledgementRead,
    });
}

internal interface IWin32InheritedEnvironment
{
    string? Read(string name);
}

internal sealed class Win32InheritedEnvironment : IWin32InheritedEnvironment
{
    public string? Read(string name) => Environment.GetEnvironmentVariable(name);
}

internal sealed record Win32UnicodeEnvironment(
    IReadOnlyDictionary<string, string> Entries,
    byte[] Block);

internal static class Win32UnicodeEnvironmentBuilder
{
    private static readonly string[] RequiredInheritedNames =
    {
        "SystemRoot",
        "TEMP",
        "TMP",
        "WINDIR",
    };

    internal static Win32UnicodeEnvironment Build(
        EvidenceCreateProcessContract contract,
        IWin32InheritedEnvironment inherited,
        string expectedAcknowledgementHandle)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(inherited);
        var entries = new List<KeyValuePair<string, string>>();
        foreach (var name in RequiredInheritedNames)
        {
            var value = inherited.Read(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"Required inherited environment value {name} was absent.");
            }
            entries.Add(new(name, value));
        }
        entries.AddRange(contract.UnicodeEnvironment);

        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in entries)
        {
            if (string.IsNullOrEmpty(key) || key.Contains('=') || key.Contains('\0') ||
                value is null || value.Contains('\0') || !unique.Add(key))
            {
                throw new InvalidOperationException("Unicode environment contained an invalid or duplicate key/value.");
            }
        }

        RequireExactEntry(
            entries,
            DebugEvidenceCaptureProtocol.EvidenceDesktopEnvironmentVariable,
            contract.Desktop);
        RequireExactEntry(
            entries,
            DebugEvidenceCaptureProtocol.EvidenceAckHandleEnvironmentVariable,
            expectedAcknowledgementHandle);

        var sorted = entries
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .ToArray();
        var text = string.Concat(sorted.Select(pair => $"{pair.Key}={pair.Value}\0")) + "\0";
        var block = Encoding.Unicode.GetBytes(text);
        var dictionary = new ReadOnlyDictionary<string, string>(
            sorted.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase));
        return new Win32UnicodeEnvironment(dictionary, block);
    }

    private static void RequireExactEntry(
        IEnumerable<KeyValuePair<string, string>> entries,
        string expectedKey,
        string expectedValue)
    {
        var matches = entries
            .Where(pair => string.Equals(pair.Key, expectedKey, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length != 1 ||
            !string.Equals(matches[0].Key, expectedKey, StringComparison.Ordinal) ||
            !string.Equals(matches[0].Value, expectedValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Required exact environment entry {expectedKey} was invalid.");
        }
    }
}

internal sealed record Win32CreateProcessRequest(
    string ApplicationName,
    char[] MutableCommandLine,
    byte[] UnicodeEnvironmentBlock,
    string CurrentDirectory,
    bool InheritHandles,
    EvidenceCreateProcessFlags CreationFlags,
    Win32StartupInfoEx StartupInfo,
    IReadOnlyList<nint> ExplicitInheritedHandles);

internal readonly record struct Win32CreateProcessResult(
    bool Created,
    nint Process,
    nint PrimaryThread,
    uint ProcessId,
    uint ThreadId);

internal interface IWin32ProcessCreationApi : IWin32EvidenceApi
{
    nuint QueryAttributeListSize(int attributeCount);

    nint Allocate(nuint bytes);

    void Free(nint memory);

    bool InitializeAttributeList(nint attributeList, int attributeCount, nuint bytes);

    void WriteHandles(nint memory, IReadOnlyList<nint> handles);

    bool UpdateHandleList(nint attributeList, nint handleListValue, nuint bytes);

    void DeleteAttributeList(nint attributeList);

    Win32CreateProcessResult CreateProcess(Win32CreateProcessRequest request);
}

internal interface IWin32PinnedBufferLease : IDisposable
{
    nint Address { get; }
}

internal interface IWin32PinnedBufferAllocator
{
    IWin32PinnedBufferLease Pin(Array buffer);
}

internal interface IWin32CreateProcessInvoker
{
    Win32CreateProcessResult CreateProcess(
        Win32CreateProcessRequest request,
        nint commandLine,
        nint environment);
}

internal interface IWin32ChildProcessImageReader
{
    EvidenceOpenedExecutableIdentity Read(
        Win32ProcessLease process,
        EvidenceOpenedExecutableIdentity expectedIdentity);
}

internal sealed class Win32ProcessAttributeListLease : IDisposable
{
    private readonly IWin32ProcessCreationApi _api;
    private nint _attributeList;
    private nint _handleListValue;
    private bool _initialized;
    private bool _disposed;

    private Win32ProcessAttributeListLease(IWin32ProcessCreationApi api)
    {
        _api = api;
    }

    internal nint DangerousAttributeList
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _attributeList;
        }
    }

    internal static Win32ProcessAttributeListLease Create(
        IWin32ProcessCreationApi api,
        IReadOnlyList<nint> handles)
    {
        var attributeBytes = api.QueryAttributeListSize(1);
        if (attributeBytes == 0)
        {
            throw new Win32Exception("Attribute-list size query failed.");
        }

        var lease = new Win32ProcessAttributeListLease(api);
        Exception? failure = null;
        try
        {
            lease._attributeList = api.Allocate(attributeBytes);
            if (lease._attributeList == 0)
            {
                throw new OutOfMemoryException("Attribute-list allocation failed.");
            }
            if (!api.InitializeAttributeList(lease._attributeList, 1, attributeBytes))
            {
                throw new Win32Exception("InitializeProcThreadAttributeList failed.");
            }
            lease._initialized = true;

            var valueBytes = checked((nuint)(handles.Count * IntPtr.Size));
            lease._handleListValue = api.Allocate(valueBytes);
            if (lease._handleListValue == 0)
            {
                throw new OutOfMemoryException("Handle-list value allocation failed.");
            }
            api.WriteHandles(lease._handleListValue, handles);
            if (!api.UpdateHandleList(lease._attributeList, lease._handleListValue, valueBytes))
            {
                throw new Win32Exception("PROC_THREAD_ATTRIBUTE_HANDLE_LIST update failed.");
            }
        }
        catch (Exception exception)
        {
            failure = exception;
        }

        if (failure is not null)
        {
            TryCleanup(lease.Dispose, ref failure);
            ExceptionDispatchInfo.Capture(failure!).Throw();
        }
        return lease;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Exception? failure = null;
        if (_initialized && _attributeList != 0)
        {
            TryCleanup(() => _api.DeleteAttributeList(_attributeList), ref failure);
        }
        if (_handleListValue != 0)
        {
            TryCleanup(() => _api.Free(_handleListValue), ref failure);
            _handleListValue = 0;
        }
        if (_attributeList != 0)
        {
            TryCleanup(() => _api.Free(_attributeList), ref failure);
            _attributeList = 0;
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
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
}

internal sealed class Win32SuspendedProcessFactory(
    IWin32ProcessCreationApi api,
    IWin32InheritedEnvironment inheritedEnvironment,
    IWin32ChildProcessImageReader imageReader)
{
    private static readonly IReadOnlyList<EvidenceChildHandleDescriptor> RequiredHandleDescriptors =
        Array.AsReadOnly(new[]
        {
            new EvidenceChildHandleDescriptor(EvidenceChildHandle.StandardInputRead, true),
            new EvidenceChildHandleDescriptor(EvidenceChildHandle.StandardOutputWrite, true),
            new EvidenceChildHandleDescriptor(EvidenceChildHandle.StandardErrorWrite, true),
            new EvidenceChildHandleDescriptor(EvidenceChildHandle.AcknowledgementRead, true),
        });

    internal Win32ProcessLease Create(
        BaseProjectileEvidenceLaunchPlan plan,
        Win32DesktopLease desktop,
        Win32LaunchHandleLease handles,
        Win32ExecutableLease executable,
        EvidenceCreateProcessContract contract)
    {
        ValidateContract(plan, desktop, handles, executable, contract);
        var environment = Win32UnicodeEnvironmentBuilder.Build(
            contract,
            inheritedEnvironment,
            handles.AcknowledgementReadHandleValue);
        Win32ProcessAttributeListLease? attributeList = null;
        Win32ProcessLease? process = null;
        Exception? failure = null;
        try
        {
            var childHandles = handles.ChildHandleValues.AsHandleList();
            attributeList = Win32ProcessAttributeListLease.Create(api, childHandles);

            var startup = new Win32StartupInfoEx
            {
                StartupInfo = new Win32StartupInfo
                {
                    Size = checked((uint)Marshal.SizeOf<Win32StartupInfoEx>()),
                    Desktop = contract.Desktop,
                    Flags = Win32EvidenceConstants.StartfUseStdHandles,
                    StandardInput = handles.ChildHandleValues.StandardInputRead,
                    StandardOutput = handles.ChildHandleValues.StandardOutputWrite,
                    StandardError = handles.ChildHandleValues.StandardErrorWrite,
                },
                AttributeList = attributeList.DangerousAttributeList,
            };
            var commandLine = (contract.CommandLine + "\0").ToCharArray();
            var request = new Win32CreateProcessRequest(
                executable.Identity.Path,
                commandLine,
                environment.Block,
                Path.GetFullPath(Path.Combine(plan.RepositoryRoot, "game")),
                InheritHandles: true,
                contract.Flags,
                startup,
                childHandles);
            var created = api.CreateProcess(request);
            if (created.Process != 0 && created.PrimaryThread != 0)
            {
                process = new Win32ProcessLease(api, created.Process, created.PrimaryThread);
            }
            if (!created.Created || process is null)
            {
                CleanupPartialCreate(created);
                throw new Win32Exception("CreateProcessW failed or returned incomplete handles.");
            }

            handles.CompleteSuccessfulProcessCreation();
            var childIdentity = imageReader.Read(process, executable.Identity);
            if (!SameExecutableIdentity(childIdentity, executable.Identity))
            {
                throw new InvalidOperationException("Suspended child image identity did not match retained Godot.");
            }
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            if (attributeList is not null) TryCleanup(attributeList.Dispose, ref failure);
            if (failure is not null && process is not null)
            {
                TryCleanup(process.Dispose, ref failure);
            }
        }

        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        return process!;
    }

    private static void ValidateContract(
        BaseProjectileEvidenceLaunchPlan plan,
        Win32DesktopLease desktop,
        Win32LaunchHandleLease handles,
        Win32ExecutableLease executable,
        EvidenceCreateProcessContract contract)
    {
        _ = desktop.DangerousHandle;
        _ = executable.DangerousHandle;
        if (contract.UseShell || contract.Flags != Win32EvidenceConstants.RequiredCreateProcessFlags ||
            !contract.InheritedHandles.SequenceEqual(RequiredHandleDescriptors) ||
            !handles.ChildHandles.SequenceEqual(RequiredHandleDescriptors) ||
            !handles.ReadyForProcessCreation ||
            !handles.ParentEndpointsAreNonInheritable ||
            !string.Equals(contract.Desktop, plan.Desktop, StringComparison.Ordinal) ||
            !string.Equals(desktop.Name, plan.Desktop, StringComparison.Ordinal) ||
            !string.Equals(contract.CommandLine, plan.CommandLine, StringComparison.Ordinal) ||
            !string.Equals(executable.Identity.Path, plan.Executable, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("CreateProcess contract did not match the admitted plan.");
        }
    }

    private void CleanupPartialCreate(Win32CreateProcessResult created)
    {
        if (created.Process != 0 && created.PrimaryThread != 0) return;
        Exception? failure = null;
        if (created.Process != 0)
        {
            try
            {
                var terminated = api.TerminateProcess(created.Process, 1);
                var wait = api.WaitForSingleObject(
                    created.Process,
                    Win32EvidenceConstants.TerminationFallbackWaitMilliseconds);
                if (!terminated || wait != Win32EvidenceConstants.WaitObject0)
                {
                    throw new Win32Exception("Partial CreateProcess result could not be terminated.");
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            TryCleanup(() => RequireClose(created.Process), ref failure);
        }
        if (created.PrimaryThread != 0)
        {
            TryCleanup(() => RequireClose(created.PrimaryThread), ref failure);
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private void RequireClose(nint handle)
    {
        if (!api.CloseKernelHandle(handle)) throw new Win32Exception("CloseHandle failed.");
    }

    private static bool SameExecutableIdentity(
        EvidenceOpenedExecutableIdentity actual,
        EvidenceOpenedExecutableIdentity expected) =>
        string.Equals(actual.Path, expected.Path, StringComparison.OrdinalIgnoreCase) &&
        actual.Exists == expected.Exists && actual.IdentityBound == expected.IdentityBound &&
        actual.IsReparsePoint == expected.IsReparsePoint &&
        string.Equals(actual.OpenedHandleIdentity, expected.OpenedHandleIdentity, StringComparison.Ordinal) &&
        string.Equals(actual.Sha256, expected.Sha256, StringComparison.Ordinal) &&
        string.Equals(actual.FileVersion, expected.FileVersion, StringComparison.Ordinal) &&
        string.Equals(actual.ProductVersion, expected.ProductVersion, StringComparison.Ordinal);

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
}

internal sealed class Win32ProcessCreationApi : IWin32ProcessCreationApi
{
    private readonly IWin32PinnedBufferAllocator _pinnedBuffers;
    private readonly IWin32CreateProcessInvoker _processInvoker;

    internal Win32ProcessCreationApi(
        IWin32PinnedBufferAllocator? pinnedBuffers = null,
        IWin32CreateProcessInvoker? processInvoker = null)
    {
        _pinnedBuffers = pinnedBuffers ?? new Win32PinnedBufferAllocator();
        _processInvoker = processInvoker ?? new Win32CreateProcessInvoker();
    }

    public nuint QueryAttributeListSize(int attributeCount)
    {
        nuint bytes = 0;
        _ = Win32EvidenceNativeMethods.InitializeProcThreadAttributeList(
            0,
            attributeCount,
            0,
            ref bytes);
        return bytes;
    }

    public nint Allocate(nuint bytes) => Marshal.AllocHGlobal(checked((nint)bytes));

    public void Free(nint memory) => Marshal.FreeHGlobal(memory);

    public bool InitializeAttributeList(nint attributeList, int attributeCount, nuint bytes)
    {
        var mutableBytes = bytes;
        return Win32EvidenceNativeMethods.InitializeProcThreadAttributeList(
            attributeList,
            attributeCount,
            0,
            ref mutableBytes) && mutableBytes <= bytes;
    }

    public void WriteHandles(nint memory, IReadOnlyList<nint> handles) =>
        Marshal.Copy(handles.ToArray(), 0, memory, handles.Count);

    public bool UpdateHandleList(nint attributeList, nint handleListValue, nuint bytes) =>
        Win32EvidenceNativeMethods.UpdateProcThreadAttribute(
            attributeList,
            0,
            Win32EvidenceConstants.ProcThreadAttributeHandleList,
            handleListValue,
            bytes,
            0,
            0);

    public void DeleteAttributeList(nint attributeList) =>
        Win32EvidenceNativeMethods.DeleteProcThreadAttributeList(attributeList);

    public Win32CreateProcessResult CreateProcess(Win32CreateProcessRequest request)
    {
        IWin32PinnedBufferLease? commandLine = null;
        IWin32PinnedBufferLease? environment = null;
        Win32CreateProcessResult result = default;
        Exception? failure = null;
        try
        {
            commandLine = _pinnedBuffers.Pin(request.MutableCommandLine);
            environment = _pinnedBuffers.Pin(request.UnicodeEnvironmentBlock);
            result = _processInvoker.CreateProcess(request, commandLine.Address, environment.Address);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            if (environment is not null) TryCleanup(environment.Dispose, ref failure);
            if (commandLine is not null) TryCleanup(commandLine.Dispose, ref failure);
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        return result;
    }

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
}

internal sealed class Win32PinnedBufferAllocator : IWin32PinnedBufferAllocator
{
    public IWin32PinnedBufferLease Pin(Array buffer) => new Win32PinnedBufferLease(buffer);
}

internal sealed class Win32PinnedBufferLease : IWin32PinnedBufferLease
{
    private GCHandle _handle;

    internal Win32PinnedBufferLease(Array buffer)
    {
        _handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
    }

    public nint Address
    {
        get
        {
            if (!_handle.IsAllocated) throw new ObjectDisposedException(nameof(Win32PinnedBufferLease));
            return _handle.AddrOfPinnedObject();
        }
    }

    public void Dispose()
    {
        if (!_handle.IsAllocated) return;
        _handle.Free();
        _handle = default;
    }
}

internal sealed class Win32CreateProcessInvoker : IWin32CreateProcessInvoker
{
    public Win32CreateProcessResult CreateProcess(
        Win32CreateProcessRequest request,
        nint commandLine,
        nint environment)
    {
        var startup = request.StartupInfo;
        var created = Win32EvidenceNativeMethods.CreateProcessW(
            request.ApplicationName,
            commandLine,
            0,
            0,
            request.InheritHandles,
            (uint)request.CreationFlags,
            environment,
            request.CurrentDirectory,
            ref startup,
            out var information);
        return new Win32CreateProcessResult(
            created,
            information.Process,
            information.Thread,
            information.ProcessId,
            information.ThreadId);
    }
}

internal sealed class Win32ChildProcessImageReader(Win32ExecutableIdentityFactory fileFactory) :
    IWin32ChildProcessImageReader
{
    public EvidenceOpenedExecutableIdentity Read(
        Win32ProcessLease process,
        EvidenceOpenedExecutableIdentity expectedIdentity)
    {
        var path = new StringBuilder(32_768);
        var length = checked((uint)path.Capacity);
        if (!Win32ProcessCreationNativeMethods.QueryFullProcessImageNameW(
                process.DangerousProcessHandle,
                0,
                path,
                ref length) || length == 0 || length >= path.Capacity)
        {
            throw new Win32Exception("QueryFullProcessImageNameW failed.");
        }
        using var lease = fileFactory.OpenExpected(new Win32ExecutableProfile(
            path.ToString(0, checked((int)length)),
            expectedIdentity.Sha256,
            expectedIdentity.FileVersion,
            expectedIdentity.ProductVersion,
            Win32ExecutableProfile.DefaultMaximumExecutableBytes));
        return lease.Identity;
    }
}

internal static class Win32ProcessCreationNativeMethods
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryFullProcessImageNameW(
        nint process,
        uint flags,
        StringBuilder executableName,
        ref uint size);
}
