using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Rounds.Game;

namespace Rounds.EvidenceLauncher;

internal static class EvidenceBuildCreatePolicy
{
    internal const uint StartfUseStdHandles = 0x00000100;
    internal const int ErrorInsufficientBuffer = 122;
    internal const nuint HandleListAttribute = 0x00020002;
    internal const int AttributeCount = 1;
    internal const int MaximumAttributeListBytes = 4096;
    internal const int HandleListBytesX64 = 24;
    internal const EvidenceCreateProcessFlags ExactFlags =
        EvidenceCreateProcessFlags.CreateSuspended |
        EvidenceCreateProcessFlags.CreateNoWindow |
        EvidenceCreateProcessFlags.BelowNormalPriorityClass |
        EvidenceCreateProcessFlags.ExtendedStartupInfoPresent |
        EvidenceCreateProcessFlags.CreateUnicodeEnvironment;
}

internal sealed record EvidenceBuildCreateSnapshot(
    string ApplicationName,
    string WorkingDirectory,
    string CommandLine,
    string UnicodeEnvironmentBlock,
    ImmutableArray<KeyValuePair<string, string>> Environment);

internal readonly record struct EvidenceBuildAttributeSizeQuery(
    bool Succeeded,
    nuint RequiredBytes,
    int Error);

internal interface IEvidenceBuildPinnedCharacterLease : IDisposable
{
    nint Address { get; }
    int CharacterCount { get; }
}

internal interface IEvidenceBuildChildImageLease : IDisposable
{
    EvidenceOpenedExecutableIdentity ReadSnapshot();
}

internal sealed record EvidenceBuildRawCreateCall(
    string ApplicationName,
    char[] MutableCommandLine,
    char[] UnicodeEnvironmentBlock,
    string CurrentDirectory,
    bool InheritHandles,
    EvidenceCreateProcessFlags CreationFlags,
    Win32StartupInfoEx StartupInfo,
    ImmutableArray<nint> ChildHandles,
    nint CommandLineAddress,
    nuint CommandLineBytes,
    nint EnvironmentAddress,
    nuint EnvironmentBytes,
    bool ProcessSecurityAttributesNull,
    bool ThreadSecurityAttributesNull);

internal interface IEvidenceBuildCreateApi : IEvidenceBuildSuspendedProcessApi
{
    EvidenceBuildAttributeSizeQuery QueryAttributeListSize(int attributeCount);
    nint Allocate(nuint bytes);
    void Free(nint memory);
    bool InitializeAttributeList(nint attributeList, int attributeCount, nuint bytes, out int error);
    void WriteHandleList(nint memory, ImmutableArray<nint> handles, nuint bytes);
    bool UpdateAttribute(
        nint attributeList,
        nuint attribute,
        nint value,
        nuint bytes,
        uint flags,
        out int error);
    void DeleteAttributeList(nint attributeList);
    IEvidenceBuildPinnedCharacterLease Pin(char[] characters);
    // A concrete adapter must call CreateProcessW directly and return its raw result without
    // performing any cleanup; the factory adopts every returned handle before later seams run.
    EvidenceBuildRawSuspendedProcessResult CreateProcessW(EvidenceBuildRawCreateCall request);
    IEvidenceBuildChildImageLease OpenChildImage(
        EvidenceBuildSuspendedProcessBorrow process,
        EvidenceOpenedExecutableIdentity expectedIdentity);
}

internal readonly record struct EvidenceBuildUnmanagedRegion(
    nint Address,
    nuint ByteLength,
    ulong EndExclusive,
    string Identity);

internal static class EvidenceBuildUnmanagedRegions
{
    internal static EvidenceBuildUnmanagedRegion Snapshot(
        nint address,
        nuint byteLength,
        string identity)
    {
        var signedAddress = address.ToInt64();
        if (IntPtr.Size != 8 || signedAddress <= 0 || byteLength == 0)
        {
            throw new InvalidDataException($"{identity} unmanaged region was invalid.");
        }
        var start = checked((ulong)signedAddress);
        var bytes = checked((ulong)byteLength);
        ulong end;
        try { end = checked(start + bytes); }
        catch (OverflowException exception)
        {
            throw new InvalidDataException($"{identity} unmanaged region end overflowed.", exception);
        }
        if (end > long.MaxValue)
        {
            throw new InvalidDataException($"{identity} unmanaged region exceeded the admitted user pointer range.");
        }
        return new EvidenceBuildUnmanagedRegion(address, byteLength, end, identity);
    }

    internal static void RequireDisjoint(params EvidenceBuildUnmanagedRegion[] regions)
    {
        ArgumentNullException.ThrowIfNull(regions);
        for (var left = 0; left < regions.Length; left++)
        {
            var leftStart = checked((ulong)regions[left].Address.ToInt64());
            for (var right = left + 1; right < regions.Length; right++)
            {
                var rightStart = checked((ulong)regions[right].Address.ToInt64());
                if (leftStart < regions[right].EndExclusive && rightStart < regions[left].EndExclusive)
                {
                    throw new InvalidDataException(
                        $"{regions[left].Identity} overlapped {regions[right].Identity}.");
                }
            }
        }
    }
}

internal sealed class EvidenceBuildProcessAttributeLease : IDisposable
{
    private readonly IEvidenceBuildCreateApi _api;
    private nint _list;
    private nint _value;
    private EvidenceBuildUnmanagedRegion _listRegion;
    private EvidenceBuildUnmanagedRegion _valueRegion;
    private bool _initialized;
    private bool _configured;
    private bool _disposed;

    private EvidenceBuildProcessAttributeLease(IEvidenceBuildCreateApi api) => _api = api;

    internal nint AttributeList => !_disposed && _configured && _listRegion.Address != 0
        ? _listRegion.Address
        : throw new ObjectDisposedException(nameof(EvidenceBuildProcessAttributeLease));
    internal EvidenceBuildUnmanagedRegion ListRegion => !_disposed && _listRegion.Address != 0
        ? _listRegion
        : throw new ObjectDisposedException(nameof(EvidenceBuildProcessAttributeLease));
    internal EvidenceBuildUnmanagedRegion ValueRegion => !_disposed && _valueRegion.Address != 0
        ? _valueRegion
        : throw new ObjectDisposedException(nameof(EvidenceBuildProcessAttributeLease));

    internal static EvidenceBuildProcessAttributeLease Allocate(IEvidenceBuildCreateApi api)
    {
        ArgumentNullException.ThrowIfNull(api);
        if (IntPtr.Size != 8)
        {
            throw new InvalidOperationException("Build handle-list attribute requires x64.");
        }
        var query = api.QueryAttributeListSize(EvidenceBuildCreatePolicy.AttributeCount);
        var querySucceeded = query.Succeeded;
        var requiredBytes = query.RequiredBytes;
        var queryError = query.Error;
        if (querySucceeded || queryError != EvidenceBuildCreatePolicy.ErrorInsufficientBuffer ||
            requiredBytes == 0 || requiredBytes > EvidenceBuildCreatePolicy.MaximumAttributeListBytes)
        {
            throw new Win32Exception(queryError, "Initial attribute-list size query was not exact.");
        }

        var lease = new EvidenceBuildProcessAttributeLease(api);
        Exception? failure = null;
        try
        {
            var list = api.Allocate(requiredBytes);
            if (list.ToInt64() <= 0) throw new OutOfMemoryException("Build attribute-list allocation failed.");
            lease._list = list;
            lease._listRegion = EvidenceBuildUnmanagedRegions.Snapshot(
                list, requiredBytes, "build attribute list");

            var value = api.Allocate(EvidenceBuildCreatePolicy.HandleListBytesX64);
            if (value.ToInt64() <= 0) throw new OutOfMemoryException("Build handle-list value allocation failed.");
            lease._value = value;
            var valueRegion = EvidenceBuildUnmanagedRegions.Snapshot(
                value, EvidenceBuildCreatePolicy.HandleListBytesX64, "build handle-list backing");
            if (value == list || RangesOverlap(lease._listRegion, valueRegion))
            {
                // The current return aliases an already-owned allocation. It is not an
                // independently freeable pointer, so disarm it before unwinding.
                lease._value = 0;
                throw new InvalidDataException("Build attribute allocations aliased or overlapped.");
            }
            lease._valueRegion = valueRegion;
        }
        catch (Exception exception) { failure = exception; }

        if (failure is not null)
        {
            DisposeAndCombine(lease, ref failure);
            ExceptionDispatchInfo.Capture(failure!).Throw();
        }
        return lease;
    }

    internal void Configure(ImmutableArray<nint> childHandles)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized || _configured || childHandles.Length != 3 ||
            childHandles.Any(handle => handle is 0 or -1) || childHandles.Distinct().Count() != 3)
        {
            throw new InvalidOperationException("Build handle-list attribute configuration was invalid.");
        }
        if (!_api.InitializeAttributeList(
                _listRegion.Address,
                EvidenceBuildCreatePolicy.AttributeCount,
                _listRegion.ByteLength,
                out var initializeError))
        {
            throw new Win32Exception(initializeError, "Build attribute-list initialization failed.");
        }
        _initialized = true;
        _api.WriteHandleList(_valueRegion.Address, childHandles, _valueRegion.ByteLength);
        if (!_api.UpdateAttribute(
                _listRegion.Address,
                EvidenceBuildCreatePolicy.HandleListAttribute,
                _valueRegion.Address,
                _valueRegion.ByteLength,
                0,
                out var updateError))
        {
            throw new Win32Exception(updateError, "Build handle-list attribute update failed.");
        }
        _configured = true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Exception? failure = null;
        if (_initialized && _list != 0)
        {
            Try(() => _api.DeleteAttributeList(_list), ref failure);
            _initialized = false;
        }
        if (_value != 0)
        {
            var value = _value;
            _value = 0;
            Try(() => _api.Free(value), ref failure);
        }
        if (_list != 0)
        {
            var list = _list;
            _list = 0;
            Try(() => _api.Free(list), ref failure);
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static bool RangesOverlap(
        EvidenceBuildUnmanagedRegion left,
        EvidenceBuildUnmanagedRegion right)
    {
        var leftStart = checked((ulong)left.Address.ToInt64());
        var rightStart = checked((ulong)right.Address.ToInt64());
        return leftStart < right.EndExclusive && rightStart < left.EndExclusive;
    }

    internal static void DisposeAndCombine(IDisposable? lease, ref Exception? failure)
    {
        if (lease is null) return;
        Try(lease.Dispose, ref failure);
    }

    internal static void Try(Action operation, ref Exception? failure)
    {
        try { operation(); }
        catch (Exception exception)
        {
            failure = failure is null ? exception : new AggregateException(failure, exception);
        }
    }
}

internal sealed class EvidenceBuildMatchedSuspendedProcessLease : IDisposable
{
    private readonly EvidenceBuildSuspendedProcessOwner _owner;
    private readonly EvidenceOpenedExecutableIdentity _matchedIdentity;

    internal EvidenceBuildMatchedSuspendedProcessLease(
        EvidenceBuildSuspendedProcessOwner owner,
        EvidenceOpenedExecutableIdentity matchedIdentity)
    {
        _owner = owner;
        _matchedIdentity = CopyIdentity(matchedIdentity);
    }

    internal EvidenceOpenedExecutableIdentity MatchedIdentity => CopyIdentity(_matchedIdentity);
    internal void Borrow(Action<EvidenceBuildSuspendedProcessBorrow> operation) => _owner.Borrow(operation);
    public void Dispose() => _owner.Dispose();

    internal static EvidenceOpenedExecutableIdentity CopyIdentity(EvidenceOpenedExecutableIdentity identity) => new(
        string.Concat(identity.Path),
        identity.Exists,
        identity.IdentityBound,
        identity.IsReparsePoint,
        string.Concat(identity.OpenedHandleIdentity),
        string.Concat(identity.Sha256),
        string.Concat(identity.FileVersion),
        string.Concat(identity.ProductVersion));
}

internal sealed class EvidenceBuildSuspendedCreateImageFactory(
    IEvidenceBuildCreateApi api,
    IEvidenceBuildKernelHandleCleanupOwner? kernelCleanupOwner = null,
    IEvidenceBuildSuspendedProcessCleanupOwner? processCleanupOwner = null)
{
    private static readonly string[] ExactEnvironmentKeys =
    [
        "SystemRoot", "WINDIR", "TEMP", "TMP",
        "DOTNET_PROCESSOR_COUNT", "MSBUILDDISABLENODEREUSE",
        "MSBuildEnableWorkloadResolver", "MSBuildSDKsPath",
        "DOTNET_CLI_UI_LANGUAGE", "VSLANG", "NUGET_PACKAGES",
        "DOTNET_CLI_HOME", "MSBuildUserExtensionsPath",
    ];
    private static readonly string[] ExactInvocationEnvironmentKeys =
    [
        "DOTNET_PROCESSOR_COUNT", "MSBUILDDISABLENODEREUSE",
        "MSBuildEnableWorkloadResolver", "MSBuildSDKsPath",
    ];

    private readonly IEvidenceBuildCreateApi _api = api ?? throw new ArgumentNullException(nameof(api));
    private readonly IEvidenceBuildKernelHandleCleanupOwner _kernelCleanupOwner =
        kernelCleanupOwner ?? EvidenceBuildKernelHandleCleanupOwner.Instance;
    private readonly IEvidenceBuildSuspendedProcessCleanupOwner _processCleanupOwner =
        processCleanupOwner ?? EvidenceBuildSuspendedProcessCleanupOwner.Instance;

    internal EvidenceBuildMatchedSuspendedProcessLease Create(
        EvidenceFrozenBuildProcessRequest request,
        EvidenceBuildRetainedExecutableLease executable,
        EvidenceBuildPipeHandleBundle pipes)
    {
        var snapshot = FreezeAndValidate(request);
        ArgumentNullException.ThrowIfNull(executable);
        ArgumentNullException.ThrowIfNull(pipes);
        EvidenceBuildSuspendedProcessOwner? owner = null;
        EvidenceOpenedExecutableIdentity? matched = null;
        Exception? failure = null;
        try
        {
            executable.Borrow(executableBorrow =>
            {
                if (!executableBorrow.Identity.Exists || !executableBorrow.Identity.IdentityBound ||
                    executableBorrow.Identity.IsReparsePoint ||
                    !string.Equals(snapshot.ApplicationName, executableBorrow.Identity.Path, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("Frozen build request did not match the retained executable.");
                }

                pipes.BorrowForCreate(pipeBorrow =>
                {
                    EvidenceBuildProcessAttributeLease? attributes = null;
                    IEvidenceBuildPinnedCharacterLease? commandPin = null;
                    IEvidenceBuildPinnedCharacterLease? environmentPin = null;
                    Exception? createFailure = null;
                    try
                    {
                        attributes = EvidenceBuildProcessAttributeLease.Allocate(_api);
                        var command = (snapshot.CommandLine + '\0').ToCharArray();
                        var environment = snapshot.UnicodeEnvironmentBlock.ToCharArray();
                        commandPin = _api.Pin(command);
                        if (commandPin is null)
                        {
                            throw new InvalidDataException("Build command-line pin was invalid.");
                        }
                        var commandAddress = commandPin.Address;
                        var commandCharacterCount = commandPin.CharacterCount;
                        var commandBytes = checked((nuint)command.Length * sizeof(char));
                        if (commandCharacterCount != command.Length)
                        {
                            throw new InvalidDataException("Build command-line pin length was invalid.");
                        }
                        var commandRegion = EvidenceBuildUnmanagedRegions.Snapshot(
                            commandAddress, commandBytes, "build command-line pin");

                        environmentPin = _api.Pin(environment);
                        if (environmentPin is null)
                        {
                            throw new InvalidDataException("Build environment pin was invalid or aliased.");
                        }
                        var environmentAddress = environmentPin.Address;
                        var environmentCharacterCount = environmentPin.CharacterCount;
                        var environmentBytes = checked((nuint)environment.Length * sizeof(char));
                        if (environmentCharacterCount != environment.Length)
                        {
                            throw new InvalidDataException("Build environment pin length was invalid.");
                        }
                        var environmentRegion = EvidenceBuildUnmanagedRegions.Snapshot(
                            environmentAddress, environmentBytes, "build environment pin");
                        EvidenceBuildUnmanagedRegions.RequireDisjoint(
                            attributes.ListRegion,
                            attributes.ValueRegion,
                            commandRegion,
                            environmentRegion);
                        attributes.Configure(pipeBorrow.ChildHandles);

                        var startup = new Win32StartupInfoEx
                        {
                            StartupInfo = new Win32StartupInfo
                            {
                                Size = 112,
                                Reserved = null,
                                Desktop = null,
                                Title = null,
                                Flags = EvidenceBuildCreatePolicy.StartfUseStdHandles,
                                StandardInput = pipeBorrow.ChildHandles[0],
                                StandardOutput = pipeBorrow.ChildHandles[1],
                                StandardError = pipeBorrow.ChildHandles[2],
                            },
                            AttributeList = attributes.AttributeList,
                        };
                        var call = new EvidenceBuildRawCreateCall(
                            snapshot.ApplicationName,
                            command,
                            environment,
                            snapshot.WorkingDirectory,
                            InheritHandles: true,
                            EvidenceBuildCreatePolicy.ExactFlags,
                            startup,
                            pipeBorrow.ChildHandles,
                            commandRegion.Address,
                            commandRegion.ByteLength,
                            environmentRegion.Address,
                            environmentRegion.ByteLength,
                            ProcessSecurityAttributesNull: true,
                            ThreadSecurityAttributesNull: true);
                        var raw = _api.CreateProcessW(call);
                        owner = EvidenceBuildSuspendedProcessOwner.Adopt(
                            _api,
                            _kernelCleanupOwner,
                            _processCleanupOwner,
                            raw,
                            executableBorrow,
                            pipeBorrow);
                        pipeBorrow.MarkSuccessfulCreate();
                    }
                    catch (Exception exception) { createFailure = exception; }
                    finally
                    {
                        EvidenceBuildProcessAttributeLease.DisposeAndCombine(environmentPin, ref createFailure);
                        EvidenceBuildProcessAttributeLease.DisposeAndCombine(commandPin, ref createFailure);
                        EvidenceBuildProcessAttributeLease.DisposeAndCombine(attributes, ref createFailure);
                    }
                    if (createFailure is not null) ExceptionDispatchInfo.Capture(createFailure).Throw();
                });

                if (owner is null) throw new InvalidDataException("Build process owner was absent after creation.");
                IEvidenceBuildChildImageLease? image = null;
                Exception? imageFailure = null;
                try
                {
                    owner.Borrow(processBorrow =>
                    {
                        image = _api.OpenChildImage(processBorrow, executableBorrow.Identity);
                        if (image is null) throw new InvalidDataException("Child-image identity lease was absent.");
                        var before = image.ReadSnapshot();
                        RequireExactImage(before, executableBorrow.Identity);
                        var after = image.ReadSnapshot();
                        RequireExactImage(after, executableBorrow.Identity);
                        if (before != after) throw new InvalidDataException("Child-image identity drifted during binding.");
                        matched = EvidenceBuildMatchedSuspendedProcessLease.CopyIdentity(after);
                    });
                }
                catch (Exception exception) { imageFailure = exception; }
                finally
                {
                    EvidenceBuildProcessAttributeLease.DisposeAndCombine(image, ref imageFailure);
                }
                if (imageFailure is not null) ExceptionDispatchInfo.Capture(imageFailure).Throw();
            });
        }
        catch (Exception exception) { failure = exception; }

        if (failure is not null)
        {
            EvidenceBuildProcessAttributeLease.DisposeAndCombine(owner, ref failure);
            ExceptionDispatchInfo.Capture(failure!).Throw();
        }
        if (owner is null || matched is null)
        {
            throw new InvalidDataException("Build process image binding did not produce a retained owner.");
        }
        return new EvidenceBuildMatchedSuspendedProcessLease(owner, matched);
    }

    internal static EvidenceBuildCreateSnapshot FreezeAndValidate(EvidenceFrozenBuildProcessRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (IntPtr.Size != 8 || Marshal.SizeOf<Win32StartupInfo>() != 104 ||
            Marshal.SizeOf<Win32StartupInfoEx>() != 112 ||
            Marshal.OffsetOf<Win32StartupInfoEx>(nameof(Win32StartupInfoEx.AttributeList)).ToInt32() != 104 ||
            Marshal.SizeOf<Win32ProcessInformation>() != 24)
        {
            throw new PlatformNotSupportedException("Build process creation requires the pinned x64 ABI.");
        }

        var applicationName = string.Concat(request.ApplicationName);
        var workingDirectory = string.Concat(request.WorkingDirectory);
        var commandLine = string.Concat(request.CommandLine);
        var environmentBlock = string.Concat(request.UnicodeEnvironmentBlock);
        var vector = request.CompleteArgumentVector?.Select(value => string.Concat(value)).ToArray() ??
            throw new InvalidDataException("Build argument vector was absent.");
        var environment = request.EffectiveEnvironment?.Select(pair =>
            new KeyValuePair<string, string>(string.Concat(pair.Key), string.Concat(pair.Value))).ToArray() ??
            throw new InvalidDataException("Build environment was absent.");
        var invocation = request.Invocation ?? throw new InvalidDataException("Build invocation was absent.");
        var job = request.JobLimits ?? throw new InvalidDataException("Build job limits were absent.");
        var invocationArguments = invocation.Arguments?.Select(value => string.Concat(value)).ToArray() ??
            throw new InvalidDataException("Build invocation arguments were absent.");
        var invocationEnvironment = invocation.Environment?.Select(pair =>
            new KeyValuePair<string, string>(string.Concat(pair.Key), string.Concat(pair.Value))).ToArray() ??
            throw new InvalidDataException("Build invocation environment was absent.");

        if (!Path.IsPathFullyQualified(applicationName) || !Path.IsPathFullyQualified(workingDirectory) ||
            !string.Equals(Path.GetFullPath(applicationName), applicationName, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetFullPath(workingDirectory), workingDirectory, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(applicationName, BaseProjectileEvidenceLaunchPlanner.MsBuildPath, StringComparison.Ordinal) ||
            !string.Equals(invocation.Executable, applicationName, StringComparison.Ordinal) ||
            !string.Equals(invocation.WorkingDirectory, workingDirectory, StringComparison.Ordinal) ||
            !invocationArguments.SequenceEqual(vector.Skip(1), StringComparer.Ordinal) ||
            vector.Length == 0 || !string.Equals(vector[0], applicationName, StringComparison.Ordinal) ||
            !string.Equals(EvidenceBuildProcessPrimitives.BuildExecutableInclusiveCommandLine(vector), commandLine,
                StringComparison.Ordinal) ||
            !string.Equals(
                EvidenceBuildProcessPrimitives.EncodeUnicodeEnvironmentBlock(
                    environment.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)),
                environmentBlock,
                StringComparison.Ordinal) ||
            environment.Length != ExactEnvironmentKeys.Length ||
            !ExactEnvironmentKeys.All(key => environment.Count(pair =>
                string.Equals(pair.Key, key, StringComparison.Ordinal)) == 1) ||
            invocationEnvironment.Length != ExactInvocationEnvironmentKeys.Length ||
            !ExactInvocationEnvironmentKeys.All(key => invocationEnvironment.Count(pair =>
                string.Equals(pair.Key, key, StringComparison.Ordinal)) == 1) ||
            invocationEnvironment.Any(pair => environment.Count(effective =>
                string.Equals(effective.Key, pair.Key, StringComparison.Ordinal) &&
                string.Equals(effective.Value, pair.Value, StringComparison.Ordinal)) != 1) ||
            request.InheritAmbientEnvironment || !request.StartSuspended || request.UseShellExecute ||
            !request.CreateNoWindow || !request.HiddenWindow || !request.BelowNormalPriority ||
            request.Deadline != TimeSpan.FromMinutes(5) ||
            request.StandardOutputCapBytes != EvidenceBuildProcessPrimitives.StreamCapBytes ||
            request.StandardErrorCapBytes != EvidenceBuildProcessPrimitives.StreamCapBytes ||
            job.AffinityMask != 0x3 || job.ActiveProcessLimit != 1 ||
            job.ProcessCommitBytes != 768L * 1024 * 1024 ||
            job.JobCommitBytes != 1024L * 1024 * 1024 || !job.KillOnJobClose ||
            commandLine.Contains('\0') || environmentBlock.Length < 2 ||
            environmentBlock[^1] != '\0' || environmentBlock[^2] != '\0')
        {
            throw new InvalidOperationException("Frozen build request drifted from the exact creation contract.");
        }

        var frozenEnvironment = environment
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .ToImmutableArray();
        return new EvidenceBuildCreateSnapshot(
            applicationName,
            workingDirectory,
            commandLine,
            environmentBlock,
            frozenEnvironment);
    }

    private static void RequireExactImage(
        EvidenceOpenedExecutableIdentity actual,
        EvidenceOpenedExecutableIdentity expected)
    {
        ArgumentNullException.ThrowIfNull(actual);
        if (!actual.Exists || !actual.IdentityBound || actual.IsReparsePoint || actual != expected)
        {
            throw new InvalidDataException("Suspended build child image did not match the retained executable identity.");
        }
    }
}
