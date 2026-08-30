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
    nint EnvironmentAddress,
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

internal sealed class EvidenceBuildProcessAttributeLease : IDisposable
{
    private readonly IEvidenceBuildCreateApi _api;
    private nint _list;
    private nint _value;
    private bool _initialized;
    private bool _disposed;

    private EvidenceBuildProcessAttributeLease(IEvidenceBuildCreateApi api) => _api = api;

    internal nint AttributeList => !_disposed && _initialized && _list != 0
        ? _list
        : throw new ObjectDisposedException(nameof(EvidenceBuildProcessAttributeLease));

    internal static EvidenceBuildProcessAttributeLease Create(
        IEvidenceBuildCreateApi api,
        ImmutableArray<nint> childHandles)
    {
        ArgumentNullException.ThrowIfNull(api);
        if (IntPtr.Size != 8 || childHandles.Length != 3 ||
            childHandles.Any(handle => handle is 0 or -1) || childHandles.Distinct().Count() != 3)
        {
            throw new InvalidOperationException("Build handle-list attribute requires three distinct x64 handles.");
        }
        var query = api.QueryAttributeListSize(EvidenceBuildCreatePolicy.AttributeCount);
        if (query.Succeeded || query.Error != EvidenceBuildCreatePolicy.ErrorInsufficientBuffer ||
            query.RequiredBytes == 0 || query.RequiredBytes > EvidenceBuildCreatePolicy.MaximumAttributeListBytes)
        {
            throw new Win32Exception(query.Error, "Initial attribute-list size query was not exact.");
        }

        var lease = new EvidenceBuildProcessAttributeLease(api);
        Exception? failure = null;
        try
        {
            lease._list = api.Allocate(query.RequiredBytes);
            if (lease._list == 0) throw new OutOfMemoryException("Build attribute-list allocation failed.");
            if (!api.InitializeAttributeList(
                    lease._list,
                    EvidenceBuildCreatePolicy.AttributeCount,
                    query.RequiredBytes,
                    out var initializeError))
            {
                throw new Win32Exception(initializeError, "Build attribute-list initialization failed.");
            }
            lease._initialized = true;
            lease._value = api.Allocate(EvidenceBuildCreatePolicy.HandleListBytesX64);
            if (lease._value == 0) throw new OutOfMemoryException("Build handle-list value allocation failed.");
            api.WriteHandleList(
                lease._value,
                childHandles,
                EvidenceBuildCreatePolicy.HandleListBytesX64);
            if (!api.UpdateAttribute(
                    lease._list,
                    EvidenceBuildCreatePolicy.HandleListAttribute,
                    lease._value,
                    EvidenceBuildCreatePolicy.HandleListBytesX64,
                    0,
                    out var updateError))
            {
                throw new Win32Exception(updateError, "Build handle-list attribute update failed.");
            }
        }
        catch (Exception exception) { failure = exception; }

        if (failure is not null)
        {
            DisposeAndCombine(lease, ref failure);
            ExceptionDispatchInfo.Capture(failure!).Throw();
        }
        return lease;
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
                        attributes = EvidenceBuildProcessAttributeLease.Create(_api, pipeBorrow.ChildHandles);
                        var command = (snapshot.CommandLine + '\0').ToCharArray();
                        var environment = snapshot.UnicodeEnvironmentBlock.ToCharArray();
                        commandPin = _api.Pin(command);
                        if (commandPin is null || commandPin.Address == 0 ||
                            commandPin.CharacterCount != command.Length)
                        {
                            throw new InvalidDataException("Build command-line pin was invalid.");
                        }
                        environmentPin = _api.Pin(environment);
                        if (environmentPin is null || environmentPin.Address == 0 ||
                            environmentPin.Address == commandPin.Address ||
                            environmentPin.CharacterCount != environment.Length)
                        {
                            throw new InvalidDataException("Build environment pin was invalid or aliased.");
                        }

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
                            commandPin.Address,
                            environmentPin.Address,
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
