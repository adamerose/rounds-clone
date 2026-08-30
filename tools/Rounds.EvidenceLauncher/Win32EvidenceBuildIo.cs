using System.Collections.ObjectModel;
using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Rounds.EvidenceLauncher;

internal sealed class Win32EvidenceBuildEnvironmentFactory(
    IWin32BuildOutputNative native,
    IEvidenceBuildEnvironmentCleanupOwner? cleanupOwner = null) : IEvidenceBuildEnvironmentFactory
{
    private readonly IEvidenceBuildEnvironmentCleanupOwner _cleanupOwner =
        cleanupOwner ?? Win32EvidenceBuildEnvironmentCleanupOwner.Instance;
    private static readonly string[] RequiredKeys =
    [
        "DOTNET_PROCESSOR_COUNT",
        "MSBUILDDISABLENODEREUSE",
        "MSBuildEnableWorkloadResolver",
        "MSBuildSDKsPath",
    ];

    public IEvidenceBuildEnvironmentLease CreateSanitized(
        EvidenceBuildInvocation required,
        EvidenceTrustedDirectoryIdentity systemRoot,
        EvidenceTrustedDirectoryIdentity temporaryDirectory)
    {
        ArgumentNullException.ThrowIfNull(required);
        var repositoryRoot = NormalizeExactDirectory(required.WorkingDirectory, "repository");
        var trustedSystemRoot = ValidateTrusted(systemRoot, "SystemRoot");
        var trustedTemporaryDirectory = ValidateTrusted(temporaryDirectory, "TEMP");
        var requiredEnvironment = FreezeRequired(required.Environment);
        if (requiredEnvironment.Count != RequiredKeys.Length ||
            RequiredKeys.Any(key => !requiredEnvironment.ContainsKey(key)))
        {
            throw new InvalidOperationException("The required build environment was not the exact four-key contract.");
        }

        var expectedSdk = ExactDescendant(repositoryRoot, @".tools\dotnet\sdk\8.0.423\Sdks");
        if (requiredEnvironment["DOTNET_PROCESSOR_COUNT"] != "2" ||
            requiredEnvironment["MSBUILDDISABLENODEREUSE"] != "1" ||
            requiredEnvironment["MSBuildEnableWorkloadResolver"] != "false" ||
            !ExactPath(requiredEnvironment["MSBuildSDKsPath"], expectedSdk))
        {
            throw new InvalidOperationException("Pinned build environment values did not match the admitted toolchain.");
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["SystemRoot"] = trustedSystemRoot,
            ["WINDIR"] = trustedSystemRoot,
            ["TEMP"] = trustedTemporaryDirectory,
            ["TMP"] = trustedTemporaryDirectory,
            ["DOTNET_PROCESSOR_COUNT"] = "2",
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["MSBuildEnableWorkloadResolver"] = "false",
            ["MSBuildSDKsPath"] = expectedSdk,
            ["DOTNET_CLI_UI_LANGUAGE"] = "en-US",
            ["VSLANG"] = "1033",
            ["NUGET_PACKAGES"] = ExactDescendant(repositoryRoot, @".tools\nuget-packages"),
            ["DOTNET_CLI_HOME"] = ExactDescendant(repositoryRoot, @".tools\dotnet-home"),
            ["MSBuildUserExtensionsPath"] = ExactDescendant(repositoryRoot, @".tools\empty\msbuild-user"),
        };
        var frozen = new ReadOnlyDictionary<string, string>(result);
        return Win32BuildEnvironmentLease.Open(
            native,
            _cleanupOwner,
            frozen,
            result["DOTNET_CLI_HOME"],
            result["MSBuildUserExtensionsPath"]);
    }

    private static Dictionary<string, string> FreezeRequired(IReadOnlyDictionary<string, string> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var frozen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source)
        {
            if (InvalidText(pair.Key) || InvalidText(pair.Value) || !frozen.TryAdd(pair.Key, pair.Value))
            {
                throw new InvalidOperationException("Required environment contained a duplicate, empty, or NUL-bearing entry.");
            }
        }
        return frozen;
    }

    private static string ValidateTrusted(EvidenceTrustedDirectoryIdentity value, string label)
    {
        ArgumentNullException.ThrowIfNull(value);
        var requested = NormalizeExactDirectory(value.RequestedPath, label);
        var canonical = NormalizeExactDirectory(value.CanonicalPath, label);
        if (!value.Exists || !value.IdentityBound || !value.ReparseFreeAncestors ||
            InvalidText(value.OpenedHandleIdentity) || !ExactPath(requested, canonical))
        {
            throw new InvalidOperationException($"Trusted {label} identity was missing, aliased, or not retained.");
        }
        return canonical;
    }

    private static string ExactDescendant(string root, string relative)
    {
        var combined = Path.GetFullPath(Path.Combine(root, relative));
        var fromRoot = Path.GetRelativePath(root, combined);
        if (Path.IsPathFullyQualified(fromRoot) || fromRoot == ".." ||
            fromRoot.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Repository-controlled environment path escaped its exact root.");
        }
        return combined;
    }

    private static string NormalizeExactDirectory(string value, string label)
    {
        if (InvalidText(value) || !Path.IsPathFullyQualified(value))
        {
            throw new InvalidOperationException($"{label} path was not an exact absolute path.");
        }
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{label} path used a noncanonical alias or trailing separator.");
        }
        return normalized;
    }

    private static bool ExactPath(string left, string right) =>
        string.Equals(left, right, StringComparison.Ordinal);

    private static bool InvalidText(string? value) =>
        string.IsNullOrWhiteSpace(value) || value.Contains('\0') || value.Contains('=');
}

internal sealed class Win32BuildEnvironmentLease : IEvidenceBuildEnvironmentLease
{
    private readonly IWin32BuildOutputNative _native;
    private readonly object _sync = new();
    private nint[] _handles;
    private readonly Win32RetainedFileSnapshot[] _snapshots;
    private readonly int[] _leafIndices;
    private IEvidenceDirectoryWriteExclusionLease?[] _writeExclusions;

    private Win32BuildEnvironmentLease(
        IWin32BuildOutputNative native,
        IReadOnlyDictionary<string, string> environment,
        nint[] handles,
        Win32RetainedFileSnapshot[] snapshots,
        int[] leafIndices,
        IEvidenceDirectoryWriteExclusionLease?[] writeExclusions)
    {
        _native = native;
        Environment = environment;
        _handles = handles;
        _snapshots = snapshots;
        _leafIndices = leafIndices;
        _writeExclusions = writeExclusions;
    }

    public IReadOnlyDictionary<string, string> Environment { get; }

    internal static IEvidenceBuildEnvironmentLease Open(
        IWin32BuildOutputNative native,
        IEvidenceBuildEnvironmentCleanupOwner cleanupOwner,
        IReadOnlyDictionary<string, string> environment,
        string dotnetCliHome,
        string msBuildUserExtensions)
    {
        var handles = new List<nint>();
        var snapshots = new List<Win32RetainedFileSnapshot>();
        var leaves = new List<int>();
        var exclusions = new List<IEvidenceDirectoryWriteExclusionLease>();
        try
        {
            foreach (var leaf in new[] { dotnetCliHome, msBuildUserExtensions })
            {
                var paths = Win32EvidenceBuildOutputApi.AncestorPaths(leaf);
                for (var pathIndex = 0; pathIndex < paths.Count; pathIndex++)
                {
                    var path = paths[pathIndex];
                    var opened = native.OpenDirectory(
                        path,
                        Win32EvidenceBuildOutputApi.DirectoryDesiredAccess,
                        Win32EvidenceBuildOutputApi.DirectoryShareMode,
                        pathIndex == paths.Count - 1
                            ? Win32EvidenceBuildOutputApi.EnvironmentLeafDirectoryFlags
                            : Win32EvidenceBuildOutputApi.DirectoryFlags);
                    if (opened.Handle is 0 or -1)
                    {
                        throw new InvalidOperationException($"Opening retained build-environment directory failed ({opened.Error}).");
                    }
                    handles.Add(opened.Handle);
                    var snapshot = native.ReadSnapshot(opened.Handle);
                    ValidateDirectory(snapshot, path);
                    snapshots.Add(snapshot);
                }
                leaves.Add(handles.Count - 1);
                var expectedExclusionIdentity = Win32RetainedFileIdentity.Format(snapshots[^1]);
                var exclusion = native.AcquireDirectoryWriteExclusion(
                    handles[^1],
                    expectedExclusionIdentity);
                exclusions.Add(exclusion);
                var exclusionStatus = exclusion.Observe();
                if (exclusionStatus.DirectoryIdentity != expectedExclusionIdentity ||
                    !exclusionStatus.Active || exclusionStatus.BreakObserved)
                {
                    throw new InvalidOperationException("Dedicated build-environment directory write exclusion was not active.");
                }
                if (native.EnumerateDirectory(handles[^1]).Count != 0)
                {
                    throw new InvalidOperationException("Dedicated build-environment directory was not empty.");
                }
            }
            return new Win32BuildEnvironmentLease(
                native,
                environment,
                handles.ToArray(),
                snapshots.ToArray(),
                leaves.ToArray(),
                exclusions.Cast<IEvidenceDirectoryWriteExclusionLease?>().ToArray());
        }
        catch (Exception failure)
        {
            var partial = new Win32BuildEnvironmentLease(
                native,
                environment,
                handles.ToArray(),
                snapshots.ToArray(),
                leaves.ToArray(),
                exclusions.Cast<IEvidenceDirectoryWriteExclusionLease?>().ToArray());
            Exception? cleanup = null;
            try { partial.Dispose(); }
            catch (Exception error)
            {
                cleanup = error;
                try { cleanupOwner.Retain(partial, error); }
                catch (Exception transfer)
                {
                    cleanup = new AggregateException(cleanup, transfer);
                }
            }
            throw cleanup is null ? failure : new AggregateException(failure, cleanup);
        }
    }

    public EvidenceBuildEnvironmentRevalidation Revalidate()
    {
        lock (_sync)
        {
            if (_handles.Any(value => value is 0 or -1))
            {
                throw new ObjectDisposedException(nameof(Win32BuildEnvironmentLease));
            }
            for (var index = 0; index < _handles.Length; index++)
            {
                if (!SameSnapshot(_snapshots[index], _native.ReadSnapshot(_handles[index])))
                {
                    throw new InvalidOperationException("Retained build-environment directory or ancestor changed.");
                }
            }
            var exclusionStatuses = _writeExclusions.Select(exclusion =>
                exclusion?.Observe() ?? throw new ObjectDisposedException(nameof(Win32BuildEnvironmentLease))).ToArray();
            if (exclusionStatuses.Length != 2 ||
                exclusionStatuses[0].DirectoryIdentity != Win32RetainedFileIdentity.Format(_snapshots[_leafIndices[0]]) ||
                exclusionStatuses[1].DirectoryIdentity != Win32RetainedFileIdentity.Format(_snapshots[_leafIndices[1]]))
            {
                throw new InvalidOperationException("Directory write exclusion was not bound to the retained environment identity.");
            }
            var empty = _leafIndices.All(index => _native.EnumerateDirectory(_handles[index]).Count == 0);
            return new EvidenceBuildEnvironmentRevalidation(
                Win32RetainedFileIdentity.Format(_snapshots[_leafIndices[0]]),
                Win32RetainedFileIdentity.Format(_snapshots[_leafIndices[1]]),
                empty,
                AllAncestorIdentitiesStable: true,
                DotNetCliHomeWriteExclusionActive: exclusionStatuses[0].Active,
                MsBuildUserExtensionsWriteExclusionActive: exclusionStatuses[1].Active,
                NoWriteBreakObserved: exclusionStatuses.All(status => !status.BreakObserved));
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            var failures = new List<Exception>();
            for (var index = _writeExclusions.Length - 1; index >= 0; index--)
            {
                if (_writeExclusions[index] is null) continue;
                try
                {
                    _writeExclusions[index]!.Dispose();
                    _writeExclusions[index] = null;
                }
                catch (Exception exception)
                {
                    failures.Add(exception);
                }
            }
            if (_writeExclusions.Any(exclusion => exclusion is not null))
            {
                if (failures.Count == 1) throw failures[0];
                throw new AggregateException(failures);
            }
            for (var index = _handles.Length - 1; index >= 0; index--)
            {
                if (_handles[index] is 0 or -1) continue;
                var close = _native.CloseKernelHandle(_handles[index]);
                if (close.Success) _handles[index] = 0;
                else failures.Add(new InvalidOperationException($"Closing retained build-environment directory failed ({close.Error})."));
            }
            if (failures.Count == 1) throw failures[0];
            if (failures.Count > 1) throw new AggregateException(failures);
        }
    }

    private static void ValidateDirectory(Win32RetainedFileSnapshot snapshot, string expected)
    {
        if (!snapshot.Directory || snapshot.DeletePending || snapshot.ReparseTag != 0 ||
            !string.Equals(
                Win32EvidenceBuildOutputApi.NormalizeDirectoryFinalPath(snapshot.FinalPath),
                expected,
                StringComparison.Ordinal) || !ValidFileId(snapshot.FileId))
        {
            throw new InvalidOperationException("Build-environment directory identity was not exact and reparse-free.");
        }
    }

    private static bool ValidFileId(string value) =>
        value.Length == 32 && value.Any(character => character != '0') &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool SameSnapshot(Win32RetainedFileSnapshot left, Win32RetainedFileSnapshot right) =>
        left.VolumeSerialNumber == right.VolumeSerialNumber && left.FileId == right.FileId &&
        left.Length == right.Length && left.ChangeTime == right.ChangeTime &&
        left.Attributes == right.Attributes && left.ReparseTag == right.ReparseTag &&
        left.LinkCount == right.LinkCount && left.DeletePending == right.DeletePending &&
        left.Directory == right.Directory &&
        string.Equals(
            NormalizeSnapshotPath(left),
            NormalizeSnapshotPath(right),
            StringComparison.Ordinal);

    private static string NormalizeSnapshotPath(Win32RetainedFileSnapshot snapshot) =>
        snapshot.Directory
            ? Win32EvidenceBuildOutputApi.NormalizeDirectoryFinalPath(snapshot.FinalPath)
            : Win32ExecutableIdentityFactory.NormalizeFinalPath(snapshot.FinalPath);
}

internal readonly record struct Win32BuildOpenResult(nint Handle, int Error);

internal readonly record struct Win32BuildCallResult(bool Success, int Error);

internal readonly record struct EvidenceDirectoryWriteExclusionStatus(
    string DirectoryIdentity,
    bool Active,
    bool BreakObserved);

internal interface IEvidenceDirectoryWriteExclusionLease : IDisposable
{
    EvidenceDirectoryWriteExclusionStatus Observe();
}

internal interface IEvidenceBuildCleanupReaperScheduler
{
    void Schedule(Action action);

    void Backoff(TimeSpan delay);
}

internal sealed class Win32EvidenceBuildEnvironmentCleanupOwner : IEvidenceBuildEnvironmentCleanupOwner
{
    internal static Win32EvidenceBuildEnvironmentCleanupOwner Instance { get; } =
        new(new Win32EvidenceBuildCleanupReaperScheduler());

    private readonly object _sync = new();
    private readonly IEvidenceBuildCleanupReaperScheduler _scheduler;
    private readonly Dictionary<long, RetainedCleanup> _retained = [];
    private readonly List<Exception> _failures = [];
    private long _nextId;

    internal Win32EvidenceBuildEnvironmentCleanupOwner(IEvidenceBuildCleanupReaperScheduler scheduler)
    {
        _scheduler = scheduler;
    }

    internal int RetainedCount
    {
        get { lock (_sync) return _retained.Count; }
    }

    internal IReadOnlyList<Exception> Failures
    {
        get { lock (_sync) return _failures.ToArray(); }
    }

    public void Retain(IEvidenceBuildEnvironmentLease lease, Exception cleanupFailure)
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(cleanupFailure);
        RetainedCleanup retained;
        lock (_sync)
        {
            var id = checked(++_nextId);
            retained = new RetainedCleanup(id, lease);
            _retained.Add(id, retained); // strong ownership exists before scheduling can fail
            _failures.Add(cleanupFailure);
        }
        Schedule(retained);
    }

    internal void RetryRetained()
    {
        RetainedCleanup[] retained;
        lock (_sync) retained = _retained.Values.Where(value => !value.Scheduled).ToArray();
        Exception? failure = null;
        foreach (var item in retained)
        {
            try { Schedule(item); }
            catch (Exception exception) { failure = failure is null ? exception : new AggregateException(failure, exception); }
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private void Schedule(RetainedCleanup retained)
    {
        lock (_sync)
        {
            if (!_retained.ContainsKey(retained.Id) || retained.Scheduled) return;
            retained.Scheduled = true;
        }
        try
        {
            _scheduler.Schedule(() => Reap(retained));
        }
        catch (Exception failure)
        {
            lock (_sync)
            {
                retained.Scheduled = false;
                _failures.Add(failure);
            }
            throw;
        }
    }

    private void Reap(RetainedCleanup retained)
    {
        const int maximumAttempts = 3;
        try
        {
            for (var attempt = 0; attempt < maximumAttempts; attempt++)
            {
                try
                {
                    retained.Lease.Dispose();
                    lock (_sync) _retained.Remove(retained.Id);
                    return;
                }
                catch (Exception failure)
                {
                    lock (_sync) _failures.Add(failure);
                    try
                    {
                        System.Diagnostics.Trace.TraceError(
                            "Retained build-environment cleanup attempt {0} failed: {1}", attempt + 1, failure);
                    }
                    catch { }
                    if (attempt + 1 < maximumAttempts)
                    {
                        try { _scheduler.Backoff(TimeSpan.FromSeconds(1)); }
                        catch (Exception backoffFailure)
                        {
                            lock (_sync) _failures.Add(backoffFailure);
                            break;
                        }
                    }
                }
            }
        }
        finally
        {
            lock (_sync)
            {
                if (_retained.ContainsKey(retained.Id))
                {
                    retained.Scheduled = false; // strongly owned for explicit retry/process exit
                }
            }
        }
    }

    private sealed class RetainedCleanup(long id, IEvidenceBuildEnvironmentLease lease)
    {
        internal long Id { get; } = id;
        internal IEvidenceBuildEnvironmentLease Lease { get; } = lease;
        internal bool Scheduled { get; set; }
    }
}

internal sealed class Win32EvidenceBuildCleanupReaperScheduler : IEvidenceBuildCleanupReaperScheduler
{
    public void Schedule(Action action)
    {
        var worker = new Thread(() =>
        {
            try { action(); }
            catch (Exception failure)
            {
                System.Diagnostics.Trace.TraceError("Build-environment cleanup reaper contained failure: {0}", failure);
            }
        })
        {
            IsBackground = true,
            Name = "Rounds evidence environment cleanup reaper",
            Priority = ThreadPriority.BelowNormal,
        };
        worker.Start();
    }

    public void Backoff(TimeSpan delay)
    {
        using var signal = new ManualResetEventSlim(false);
        signal.Wait(delay);
    }
}

internal interface IWin32BuildOutputNative
{
    Win32BuildOpenResult OpenDirectory(string normalizedAbsolutePath, uint desiredAccess, uint shareMode, uint flagsAndAttributes);

    Win32BuildOpenResult OpenFile(string normalizedAbsolutePath, uint desiredAccess, uint shareMode, uint flagsAndAttributes);

    Win32RetainedFileSnapshot ReadSnapshot(nint retainedHandle);

    IReadOnlyList<Win32PublishedArtifactEntry> EnumerateDirectory(nint retainedDirectoryHandle);

    IReadOnlyList<Win32PublishedStreamEntry> EnumerateStreams(nint retainedFileHandle);

    IEvidenceDirectoryWriteExclusionLease AcquireDirectoryWriteExclusion(
        nint retainedDirectoryHandle,
        string exactDirectoryIdentity);

    Win32BuildCallResult SetDeleteDisposition(nint retainedFileHandle, uint flags);

    Win32BuildCallResult CloseKernelHandle(nint handle);
}

internal sealed class Win32EvidenceBuildOutputApi : IEvidenceBuildOutputApi
{
    internal const uint PriorFileDesiredAccess = Win32EvidenceConstants.GenericRead | 0x00010000; // DELETE
    internal const uint ReadFileDesiredAccess = Win32EvidenceConstants.GenericRead;
    internal const uint FileShareMode = Win32EvidenceConstants.FileShareRead;
    internal const uint DirectoryDesiredAccess = 0x00000001; // FILE_LIST_DIRECTORY
    internal const uint DirectoryShareMode = Win32EvidenceConstants.FileShareRead | Win32EvidenceConstants.FileShareWrite;
    internal const uint FileFlags = Win32EvidenceConstants.FileAttributeNormal | Win32EvidenceConstants.FileFlagOpenReparsePoint;
    internal const uint DirectoryFlags = Win32EvidenceConstants.FileFlagBackupSemantics | Win32EvidenceConstants.FileFlagOpenReparsePoint;
    internal const uint EnvironmentLeafDirectoryFlags = DirectoryFlags | 0x40000000; // FILE_FLAG_OVERLAPPED
    internal const uint DeleteDispositionFlags = 0x00000001 | 0x00000002 | 0x00000010; // DELETE|POSIX|IGNORE_READONLY
    internal const long MaximumAssemblyBytes = 512L * 1024 * 1024;

    private readonly IWin32BuildOutputNative _native;
    private readonly string[] _exactPaths;

    internal Win32EvidenceBuildOutputApi(IWin32BuildOutputNative native, string exactRepositoryRoot)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
        var root = NormalizeRepositoryRoot(exactRepositoryRoot);
        var output = Path.GetFullPath(Path.Combine(root, @"game\.godot\mono\temp\bin\Debug"));
        _exactPaths =
        [
            Path.Combine(output, "Rounds.Game.dll"),
            Path.Combine(output, "Rounds.Replay.dll"),
            Path.Combine(output, "Rounds.Sim.dll"),
        ];
    }

    public IEvidencePriorOutputLease OpenPrior(string exactRuntimeAssemblyPath)
    {
        var expected = RequireExactOutput(exactRuntimeAssemblyPath);
        var ancestors = OpenAncestors(expected);
        nint file = 0;
        try
        {
            var opened = _native.OpenFile(expected, PriorFileDesiredAccess, FileShareMode, FileFlags);
            file = opened.Handle;
            EnsureHandle(file, opened.Error, "prior runtime output");
            var before = _native.ReadSnapshot(file);
            var state = ValidateFile(before, expected, file);
            var after = _native.ReadSnapshot(file);
            if (!SameSnapshot(before, after))
            {
                throw new InvalidOperationException("Prior runtime output changed during retained inspection.");
            }
            return new Win32PriorOutputLease(_native, ancestors, file, before, state);
        }
        catch (Exception failure)
        {
            throw CloseAcquired(failure, file, ancestors);
        }
    }

    public EvidenceBuildOutputState ReadRecreated(string exactRuntimeAssemblyPath)
    {
        var expected = RequireExactOutput(exactRuntimeAssemblyPath);
        var ancestors = OpenAncestors(expected);
        nint file = 0;
        Exception? failure = null;
        EvidenceBuildOutputState? state = null;
        try
        {
            var opened = _native.OpenFile(expected, ReadFileDesiredAccess, FileShareMode, FileFlags);
            file = opened.Handle;
            EnsureHandle(file, opened.Error, "recreated runtime output");
            var before = _native.ReadSnapshot(file);
            state = ValidateFile(before, expected, file);
            var after = _native.ReadSnapshot(file);
            if (!SameSnapshot(before, after))
            {
                throw new InvalidOperationException("Recreated runtime output changed during identity-bound inspection.");
            }
            ValidateAncestorsStable(ancestors);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        var cleanup = CloseAll(file, ancestors);
        if (failure is not null) throw cleanup is null ? failure : new AggregateException(failure, cleanup);
        if (cleanup is not null) ExceptionDispatchInfo.Capture(cleanup).Throw();
        return state!;
    }

    private List<Win32BuildOutputAncestor> OpenAncestors(string exactFile)
    {
        var result = new List<Win32BuildOutputAncestor>();
        try
        {
            foreach (var path in AncestorPaths(Path.GetDirectoryName(exactFile)!))
            {
                var opened = _native.OpenDirectory(path, DirectoryDesiredAccess, DirectoryShareMode, DirectoryFlags);
                var handle = opened.Handle;
                EnsureHandle(handle, opened.Error, "runtime output ancestor");
                var owned = new Win32BuildOutputAncestor(path, handle);
                result.Add(owned);
                var snapshot = _native.ReadSnapshot(handle);
                ValidateDirectory(snapshot, path);
                owned.Before = snapshot;
            }
            return result;
        }
        catch (Exception failure)
        {
            var cleanup = CloseAll(0, result);
            throw cleanup is null ? failure : new AggregateException(failure, cleanup);
        }
    }

    private EvidenceBuildOutputState ValidateFile(
        Win32RetainedFileSnapshot snapshot,
        string expected,
        nint handle)
    {
        var normalizedFinal = Win32ExecutableIdentityFactory.NormalizeFinalPath(snapshot.FinalPath);
        if (!string.Equals(normalizedFinal, expected, StringComparison.Ordinal) ||
            snapshot.Directory || snapshot.DeletePending || snapshot.LinkCount != 1 ||
            snapshot.ReparseTag != 0 || snapshot.Length <= 0 || snapshot.Length > MaximumAssemblyBytes ||
            snapshot.ChangeTime <= 0 || !ValidFileId(snapshot.FileId))
        {
            throw new InvalidOperationException("Runtime output was not an exact regular single-link retained file.");
        }
        var streams = _native.EnumerateStreams(handle);
        if (streams.Count != 1 || streams[0].Name != "::$DATA" || streams[0].Length != snapshot.Length)
        {
            throw new InvalidOperationException("Runtime output contained an alternate or inconsistent stream.");
        }
        return new EvidenceBuildOutputState(
            expected, true, true, false, true, Win32RetainedFileIdentity.Format(snapshot), snapshot.Length,
            snapshot.ChangeTime, false, snapshot.LinkCount, false);
    }

    private static void ValidateDirectory(Win32RetainedFileSnapshot snapshot, string expected)
    {
        if (!string.Equals(
                NormalizeDirectoryFinalPath(snapshot.FinalPath),
                expected,
                StringComparison.Ordinal) ||
            !snapshot.Directory || snapshot.DeletePending || snapshot.ReparseTag != 0 ||
            !ValidFileId(snapshot.FileId))
        {
            throw new InvalidOperationException("Runtime output ancestor was not an exact retained reparse-free directory.");
        }
    }

    internal static IReadOnlyList<string> AncestorPaths(string exactParent)
    {
        var parent = Path.GetFullPath(exactParent);
        var root = Path.GetPathRoot(parent) ?? throw new InvalidOperationException("Output path had no volume root.");
        var relative = Path.GetRelativePath(root, parent);
        var paths = new List<string> { root };
        var current = root;
        if (relative != ".")
        {
            foreach (var component in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
            {
                if (component is "." or "..") throw new InvalidOperationException("Output ancestor path was aliased.");
                current = Path.Combine(current, component);
                paths.Add(current);
            }
        }
        return Array.AsReadOnly(paths.ToArray());
    }

    internal static string NormalizeDirectoryFinalPath(string finalPath)
    {
        if (string.IsNullOrWhiteSpace(finalPath) || finalPath.Contains('\0'))
        {
            throw new InvalidOperationException("Final directory handle path was empty or malformed.");
        }
        var dosPath = finalPath.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)
            ? @"\\" + finalPath[8..]
            : finalPath.StartsWith(@"\\?\", StringComparison.Ordinal)
                ? finalPath[4..]
                : finalPath;
        if (!Path.IsPathFullyQualified(dosPath) || dosPath.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Final directory handle path was not an absolute DOS or UNC path.");
        }
        var normalized = Path.GetFullPath(dosPath);
        var root = Path.GetPathRoot(normalized) ?? throw new InvalidOperationException("Final directory path had no root.");
        if (normalized[root.Length..].Contains(':'))
        {
            throw new InvalidOperationException("Final directory handle path named an alternate data stream.");
        }
        return normalized.Length == root.Length ? root : Path.TrimEndingDirectorySeparator(normalized);
    }

    private string RequireExactOutput(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('\0') ||
            !Path.IsPathFullyQualified(value))
        {
            throw new InvalidOperationException("Runtime output path was not exact and absolute.");
        }
        var normalized = Path.GetFullPath(value);
        if (!string.Equals(value, normalized, StringComparison.Ordinal) ||
            !_exactPaths.Contains(normalized, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Only the exact Game, Replay, and Sim runtime outputs are admitted.");
        }
        return normalized;
    }

    private static string NormalizeRepositoryRoot(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('\0') ||
            !Path.IsPathFullyQualified(value))
        {
            throw new InvalidOperationException("Repository root was not exact and absolute.");
        }
        var normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Repository root was aliased.");
        }
        return normalized;
    }

    private static void EnsureHandle(nint handle, int error, string label)
    {
        if (handle is 0 or -1) throw new InvalidOperationException($"Opening {label} failed ({error}).");
    }

    private static bool ValidFileId(string value) =>
        value.Length == 32 && value.Any(character => character != '0') &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool SameSnapshot(Win32RetainedFileSnapshot left, Win32RetainedFileSnapshot right) =>
        left.VolumeSerialNumber == right.VolumeSerialNumber && left.FileId == right.FileId &&
        left.Length == right.Length && left.ChangeTime == right.ChangeTime &&
        left.Attributes == right.Attributes && left.ReparseTag == right.ReparseTag &&
        left.LinkCount == right.LinkCount && left.DeletePending == right.DeletePending &&
        left.Directory == right.Directory &&
        string.Equals(
            NormalizeSnapshotPath(left),
            NormalizeSnapshotPath(right),
            StringComparison.Ordinal);

    private static string NormalizeSnapshotPath(Win32RetainedFileSnapshot snapshot) =>
        snapshot.Directory
            ? NormalizeDirectoryFinalPath(snapshot.FinalPath)
            : Win32ExecutableIdentityFactory.NormalizeFinalPath(snapshot.FinalPath);

    private void ValidateAncestorsStable(IReadOnlyList<Win32BuildOutputAncestor> ancestors)
    {
        foreach (var ancestor in ancestors)
        {
            if (ancestor.Handle is 0 or -1 || ancestor.Before is null ||
                !SameSnapshot(ancestor.Before, _native.ReadSnapshot(ancestor.Handle)))
            {
                throw new InvalidOperationException("Retained output ancestor identity changed.");
            }
        }
    }

    private Exception CloseAcquired(Exception failure, nint file, IReadOnlyList<Win32BuildOutputAncestor> ancestors)
    {
        var cleanup = CloseAll(file, ancestors);
        return cleanup is null ? failure : new AggregateException(failure, cleanup);
    }

    private Exception? CloseAll(nint file, IReadOnlyList<Win32BuildOutputAncestor> ancestors)
    {
        Exception? failure = null;
        if (file is not 0 and not -1 && !_native.CloseKernelHandle(file).Success)
        {
            failure = new InvalidOperationException("Closing runtime output handle failed.");
        }
        for (var index = ancestors.Count - 1; index >= 0; index--)
        {
            if (!_native.CloseKernelHandle(ancestors[index].Handle).Success)
            {
                var close = new InvalidOperationException("Closing runtime output ancestor failed.");
                failure = failure is null ? close : new AggregateException(failure, close);
            }
        }
        return failure;
    }

    private sealed class Win32BuildOutputAncestor(string path, nint handle)
    {
        internal string Path { get; } = path;
        internal nint Handle { get; } = handle;
        internal Win32RetainedFileSnapshot? Before { get; set; }
    }

    private sealed class Win32PriorOutputLease(
        IWin32BuildOutputNative native,
        IReadOnlyList<Win32BuildOutputAncestor> ancestors,
        nint fileHandle,
        Win32RetainedFileSnapshot fileSnapshot,
        EvidenceBuildOutputState state) : IEvidencePriorOutputLease
    {
        private readonly object _sync = new();
        private nint _fileHandle = fileHandle;
        private nint[] _ancestorHandles = ancestors.Select(value => value.Handle).ToArray();
        private readonly Win32RetainedFileSnapshot[] _ancestorSnapshots = ancestors.Select(value =>
            value.Before ?? throw new InvalidOperationException("Retained output ancestor lacked its acquired snapshot.")).ToArray();
        private bool _dispositionApplied;
        private bool _fileCloseAttempted;
        private bool _deleteAttempted;
        private readonly Win32RetainedFileSnapshot _fileSnapshot = fileSnapshot;
        private EvidencePriorOutputDeletionProof? _proof;

        public EvidenceBuildOutputState State { get; } = state;

        public bool RetainsExactFileAndAncestorIdentity
        {
            get
            {
                lock (_sync)
                {
                    return _fileHandle is not 0 and not -1 && _ancestorHandles.All(value => value is not 0 and not -1);
                }
            }
        }

        public EvidencePriorOutputDeletionProof DeleteRetainedIdentityAndProveAbsent()
        {
            lock (_sync)
            {
                if (_deleteAttempted)
                {
                    throw new InvalidOperationException("Prior-output disposition is a one-shot operation.");
                }
                _deleteAttempted = true;
                EnsureLive();
                if (!_dispositionApplied)
                {
                    var immediatelyBefore = native.ReadSnapshot(_fileHandle);
                    if (!SameSnapshot(_fileSnapshot, immediatelyBefore))
                    {
                        throw new InvalidOperationException("Prior output changed before identity-bound disposition.");
                    }
                    var disposition = native.SetDeleteDisposition(_fileHandle, DeleteDispositionFlags);
                    if (!disposition.Success)
                    {
                        throw new InvalidOperationException($"Identity-bound prior-output disposition failed ({disposition.Error}).");
                    }
                    _dispositionApplied = true;
                }
                var after = native.ReadSnapshot(_fileHandle);
                if (!SameFileAfterDisposition(_fileSnapshot, after) ||
                    Win32RetainedFileIdentity.Format(after) != State.OpenedHandleIdentity || !after.DeletePending)
                {
                    throw new InvalidOperationException("Delete disposition did not remain bound to the retained prior identity.");
                }
                _fileCloseAttempted = true;
                var close = native.CloseKernelHandle(_fileHandle);
                if (!close.Success)
                {
                    throw new InvalidOperationException($"Closing disposed prior-output identity failed ({close.Error}).");
                }
                _fileHandle = 0;
                var parentIndex = _ancestorHandles.Length - 1;
                var fileName = Path.GetFileName(State.Path);
                if (native.EnumerateDirectory(_ancestorHandles[parentIndex]).Any(entry =>
                    string.Equals(entry.Name, fileName, StringComparison.OrdinalIgnoreCase)))
                {
                    throw new InvalidOperationException("Disposed prior-output path remained present in its retained parent.");
                }
                for (var index = 0; index < _ancestorHandles.Length; index++)
                {
                    var current = native.ReadSnapshot(_ancestorHandles[index]);
                    if (!SameDirectoryIdentity(_ancestorSnapshots[index], current))
                    {
                        throw new InvalidOperationException("Output ancestor identity changed during prior-output disposition.");
                    }
                }
                _proof = new EvidencePriorOutputDeletionProof(
                    State.Path,
                    State.OpenedHandleIdentity,
                    ExactRetainedIdentityDisposition: true,
                    ExactPathAbsent: true,
                    AncestorIdentityStillRetained: true);
                return _proof;
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                var failures = new List<Exception>();
                if (_fileHandle is not 0 and not -1)
                {
                    if (_fileCloseAttempted)
                    {
                        failures.Add(new InvalidOperationException("Prior-output close result was ambiguous and is not retried."));
                    }
                    else
                    {
                        _fileCloseAttempted = true;
                        var close = native.CloseKernelHandle(_fileHandle);
                        if (close.Success) _fileHandle = 0;
                        else failures.Add(new InvalidOperationException($"Closing retained prior-output handle failed ({close.Error})."));
                    }
                }
                for (var index = _ancestorHandles.Length - 1; index >= 0; index--)
                {
                    if (_ancestorHandles[index] is 0 or -1) continue;
                    var close = native.CloseKernelHandle(_ancestorHandles[index]);
                    if (close.Success) _ancestorHandles[index] = 0;
                    else failures.Add(new InvalidOperationException($"Closing retained prior-output ancestor failed ({close.Error})."));
                }
                if (failures.Count == 1) throw failures[0];
                if (failures.Count > 1) throw new AggregateException(failures);
            }
        }

        private void EnsureLive()
        {
            if (_fileHandle is 0 or -1 || _ancestorHandles.Any(value => value is 0 or -1))
            {
                throw new ObjectDisposedException(nameof(Win32PriorOutputLease));
            }
        }

        private static bool SameFileAfterDisposition(
            Win32RetainedFileSnapshot before,
            Win32RetainedFileSnapshot after) =>
            before.VolumeSerialNumber == after.VolumeSerialNumber && before.FileId == after.FileId &&
            before.Length == after.Length && before.Attributes == after.Attributes &&
            before.ReparseTag == after.ReparseTag && before.LinkCount == after.LinkCount &&
            before.Directory == after.Directory && !before.DeletePending && after.DeletePending &&
            string.Equals(
                Win32ExecutableIdentityFactory.NormalizeFinalPath(before.FinalPath),
                Win32ExecutableIdentityFactory.NormalizeFinalPath(after.FinalPath),
                StringComparison.Ordinal);

        private static bool SameDirectoryIdentity(
            Win32RetainedFileSnapshot before,
            Win32RetainedFileSnapshot after) =>
            before.VolumeSerialNumber == after.VolumeSerialNumber && before.FileId == after.FileId &&
            before.Attributes == after.Attributes && before.ReparseTag == after.ReparseTag &&
            before.Directory && after.Directory && !after.DeletePending &&
            string.Equals(
                NormalizeDirectoryFinalPath(before.FinalPath),
                NormalizeDirectoryFinalPath(after.FinalPath),
                StringComparison.Ordinal);
    }
}

internal sealed class Win32BuildOutputNative : IWin32BuildOutputNative
{
    internal const int FileDispositionInfoEx = 21;
    internal const int FileDispositionInfoExSize = 4;
    private readonly Win32PublishedFrameApi _files = new();

    public Win32BuildOpenResult OpenDirectory(string path, uint access, uint share, uint flags)
    {
        var handle = _files.OpenRoot(path, access, share, flags);
        var error = handle is 0 or -1 ? Marshal.GetLastPInvokeError() : 0;
        return new Win32BuildOpenResult(handle, error);
    }

    public Win32BuildOpenResult OpenFile(string path, uint access, uint share, uint flags)
    {
        var handle = _files.OpenFrame(path, access, share, flags);
        var error = handle is 0 or -1 ? Marshal.GetLastPInvokeError() : 0;
        return new Win32BuildOpenResult(handle, error);
    }

    public Win32RetainedFileSnapshot ReadSnapshot(nint handle) => _files.ReadSnapshot(handle);

    public IReadOnlyList<Win32PublishedArtifactEntry> EnumerateDirectory(nint handle) =>
        Win32BuildDirectoryEnumerator.Collect(page => ReadDirectoryPage(handle, page));

    public IReadOnlyList<Win32PublishedStreamEntry> EnumerateStreams(nint handle) => _files.EnumerateFrameStreams(handle);

    public IEvidenceDirectoryWriteExclusionLease AcquireDirectoryWriteExclusion(
        nint retainedDirectoryHandle,
        string exactDirectoryIdentity) =>
        Win32DirectoryOplockLease.Acquire(retainedDirectoryHandle, exactDirectoryIdentity);

    public Win32BuildCallResult CloseKernelHandle(nint handle)
    {
        var success = _files.CloseKernelHandle(handle);
        var error = success ? 0 : Marshal.GetLastPInvokeError();
        return new Win32BuildCallResult(success, error);
    }

    public Win32BuildCallResult SetDeleteDisposition(nint retainedFileHandle, uint flags)
    {
        var information = new FileDispositionInfoExBuffer { Flags = flags };
        var success = SetFileInformationByHandle(
            retainedFileHandle,
            FileDispositionInfoEx,
            ref information,
            (uint)Marshal.SizeOf<FileDispositionInfoExBuffer>());
        var error = success ? 0 : Marshal.GetLastPInvokeError();
        return new Win32BuildCallResult(success, error);
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct FileDispositionInfoExBuffer
    {
        internal uint Flags;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetFileInformationByHandle(
        nint file,
        int fileInformationClass,
        ref FileDispositionInfoExBuffer fileInformation,
        uint bufferSize);

    private static Win32PublishedDirectoryReadResult ReadDirectoryPage(nint handle, int page)
    {
        const int bufferSize = 64 * 1024;
        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            var success = GetFileInformationByHandleEx(
                handle,
                page == 0 ? Win32PublishedFrameApi.FileIdBothDirectoryRestartInfo : Win32PublishedFrameApi.FileIdBothDirectoryInfo,
                buffer,
                (uint)bufferSize);
            var error = success ? 0 : Marshal.GetLastPInvokeError();
            if (!success) return new Win32PublishedDirectoryReadResult(false, error, []);
            var bytes = new byte[bufferSize];
            Marshal.Copy(buffer, bytes, 0, bytes.Length);
            return new Win32PublishedDirectoryReadResult(true, 0, bytes);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        nint file,
        int fileInformationClass,
        nint fileInformation,
        uint bufferSize);
}

// A pending RH directory oplock is the continuous exclusion boundary: any child namespace
// mutation requests a break and remains blocked until the owner acknowledges or closes.
// This lease never acknowledges a break. It records the event sticky and releases only in
// bounded cleanup, so an add/remove cannot influence MSBuild and disappear between samples.
internal sealed class Win32DirectoryOplockLease : IEvidenceDirectoryWriteExclusionLease
{
    internal const uint FsctlRequestOplock = 0x00090240;
    internal const uint RequestedLevel = 0x00000001 | 0x00000004; // CACHE_READ | CACHE_HANDLE
    internal const uint RequestFlag = 0x00000001;
    internal const int ErrorIoPending = 997;
    internal const int ErrorNotFound = 1168;
    internal const uint WaitObject0 = 0;
    internal const uint WaitTimeout = 258;
    internal const uint WaitFailed = 0xffffffff;
    internal const uint CleanupWaitMilliseconds = 5000;

    private readonly object _sync = new();
    private readonly nint _directoryHandle;
    private readonly string _directoryIdentity;
    private nint _eventHandle;
    private nint _input;
    private nint _output;
    private nint _overlapped;
    private bool _breakObserved;
    private bool _ioCompleted;
    private bool _eventCloseAttempted;
    private Exception? _terminalCleanupFailure;

    private Win32DirectoryOplockLease(
        nint directoryHandle,
        string directoryIdentity,
        nint eventHandle,
        nint input,
        nint output,
        nint overlapped)
    {
        _directoryHandle = directoryHandle;
        _directoryIdentity = directoryIdentity;
        _eventHandle = eventHandle;
        _input = input;
        _output = output;
        _overlapped = overlapped;
    }

    internal static IEvidenceDirectoryWriteExclusionLease Acquire(
        nint retainedDirectoryHandle,
        string exactDirectoryIdentity)
    {
        if (retainedDirectoryHandle is 0 or -1 || string.IsNullOrWhiteSpace(exactDirectoryIdentity))
        {
            throw new ArgumentException("Directory oplock acquisition requires an exact retained identity.");
        }
        nint eventHandle = 0;
        nint input = 0;
        nint output = 0;
        nint overlapped = 0;
        Exception? failure = null;
        try
        {
            eventHandle = CreateEventW(0, true, false, null);
            if (eventHandle is 0 or -1)
            {
                var error = Marshal.GetLastPInvokeError();
                throw new Win32Exception(error, "Creating directory-oplock event failed.");
            }
            input = Marshal.AllocHGlobal(Marshal.SizeOf<RequestOplockInputBuffer>());
            output = Marshal.AllocHGlobal(Marshal.SizeOf<RequestOplockOutputBuffer>());
            overlapped = Marshal.AllocHGlobal(Marshal.SizeOf<NativeOverlappedBuffer>());
            Marshal.StructureToPtr(new RequestOplockInputBuffer
            {
                StructureVersion = 1,
                StructureLength = checked((ushort)Marshal.SizeOf<RequestOplockInputBuffer>()),
                RequestedOplockLevel = RequestedLevel,
                Flags = RequestFlag,
            }, input, false);
            Marshal.StructureToPtr(new RequestOplockOutputBuffer
            {
                StructureVersion = 1,
                StructureLength = checked((ushort)Marshal.SizeOf<RequestOplockOutputBuffer>()),
            }, output, false);
            Marshal.StructureToPtr(new NativeOverlappedBuffer { EventHandle = eventHandle }, overlapped, false);
            var started = DeviceIoControl(
                retainedDirectoryHandle,
                FsctlRequestOplock,
                input,
                checked((uint)Marshal.SizeOf<RequestOplockInputBuffer>()),
                output,
                checked((uint)Marshal.SizeOf<RequestOplockOutputBuffer>()),
                out _,
                overlapped);
            var errorCode = started ? 0 : Marshal.GetLastPInvokeError();
            if (started || errorCode != ErrorIoPending)
            {
                throw new Win32Exception(errorCode, "Directory write-exclusion oplock was not granted pending break notification.");
            }
            return new Win32DirectoryOplockLease(
                retainedDirectoryHandle,
                exactDirectoryIdentity,
                eventHandle,
                input,
                output,
                overlapped);
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        Exception? cleanup = null;
        if (eventHandle is not 0 and not -1 && !CloseHandle(eventHandle))
        {
            var error = Marshal.GetLastPInvokeError();
            cleanup = new Win32Exception(error, "Closing refused directory-oplock event failed.");
            Win32AmbiguousEventHandleOwner.Retain(eventHandle, cleanup);
        }
        if (overlapped != 0) Marshal.FreeHGlobal(overlapped);
        if (output != 0) Marshal.FreeHGlobal(output);
        if (input != 0) Marshal.FreeHGlobal(input);
        throw cleanup is null ? failure! : new AggregateException(failure!, cleanup);
    }

    public EvidenceDirectoryWriteExclusionStatus Observe()
    {
        lock (_sync)
        {
            if (_terminalCleanupFailure is not null)
            {
                throw new InvalidOperationException(
                    "Directory write-exclusion event ownership is terminally ambiguous.",
                    _terminalCleanupFailure);
            }
            if (_eventHandle is 0 or -1 || _input == 0 || _output == 0 || _overlapped == 0)
            {
                throw new ObjectDisposedException(nameof(Win32DirectoryOplockLease));
            }
            var wait = WaitForSingleObject(_eventHandle, 0);
            if (wait == WaitObject0)
            {
                _breakObserved = true;
                _ioCompleted = true;
            }
            else if (wait != WaitTimeout)
            {
                var error = wait == WaitFailed ? Marshal.GetLastPInvokeError() : unchecked((int)wait);
                throw new Win32Exception(error, $"Observing directory write exclusion failed for {_directoryIdentity}.");
            }
            return new EvidenceDirectoryWriteExclusionStatus(_directoryIdentity, !_breakObserved, _breakObserved);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_terminalCleanupFailure is not null)
            {
                throw new InvalidOperationException(
                    "Directory write-exclusion event ownership is terminally ambiguous and retained until process exit.",
                    _terminalCleanupFailure);
            }
            if (_input == 0 && _output == 0 && _overlapped == 0 && _eventHandle == 0) return;
            if (!_ioCompleted)
            {
                var wait = WaitForSingleObject(_eventHandle, 0);
                if (wait == WaitObject0)
                {
                    _breakObserved = true;
                    _ioCompleted = true;
                }
                else if (wait == WaitTimeout)
                {
                    var canceled = CancelIoEx(_directoryHandle, _overlapped);
                    var cancelError = canceled ? 0 : Marshal.GetLastPInvokeError();
                    if (!canceled && cancelError != ErrorNotFound)
                    {
                        throw new Win32Exception(cancelError, "Canceling directory write exclusion failed; ownership retained for retry.");
                    }
                    var completion = WaitForSingleObject(_eventHandle, CleanupWaitMilliseconds);
                    if (completion != WaitObject0)
                    {
                        var error = completion == WaitFailed ? Marshal.GetLastPInvokeError() : unchecked((int)completion);
                        throw new Win32Exception(error, "Directory write-exclusion cancellation did not complete within five seconds; ownership retained.");
                    }
                    _ioCompleted = true;
                }
                else
                {
                    var error = wait == WaitFailed ? Marshal.GetLastPInvokeError() : unchecked((int)wait);
                    throw new Win32Exception(error, "Observing directory write-exclusion cleanup failed; ownership retained.");
                }
            }

            Marshal.FreeHGlobal(_overlapped);
            Marshal.FreeHGlobal(_output);
            Marshal.FreeHGlobal(_input);
            _overlapped = 0;
            _output = 0;
            _input = 0;
            if (_eventHandle is not 0 and not -1 && !_eventCloseAttempted)
            {
                _eventCloseAttempted = true;
                var eventHandle = _eventHandle;
                if (!CloseHandle(eventHandle))
                {
                    var error = Marshal.GetLastPInvokeError();
                    _terminalCleanupFailure = new Win32Exception(
                        error,
                        "Closing directory write-exclusion event failed; handle ownership is ambiguous.");
                    throw _terminalCleanupFailure;
                }
                _eventHandle = 0;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RequestOplockInputBuffer
    {
        internal ushort StructureVersion;
        internal ushort StructureLength;
        internal uint RequestedOplockLevel;
        internal uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RequestOplockOutputBuffer
    {
        internal ushort StructureVersion;
        internal ushort StructureLength;
        internal uint OriginalOplockLevel;
        internal uint NewOplockLevel;
        internal uint Flags;
        internal uint AccessMode;
        internal ushort ShareMode;
        internal ushort Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct NativeOverlappedBuffer
    {
        internal nint Internal;
        internal nint InternalHigh;
        internal uint Offset;
        internal uint OffsetHigh;
        internal nint EventHandle;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint CreateEventW(nint securityAttributes, [MarshalAs(UnmanagedType.Bool)] bool manualReset,
        [MarshalAs(UnmanagedType.Bool)] bool initialState, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(nint device, uint controlCode, nint input, uint inputSize,
        nint output, uint outputSize, out uint bytesReturned, nint overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CancelIoEx(nint handle, nint overlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(nint handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);
}

internal static class Win32AmbiguousEventHandleOwner
{
    private static readonly object Sync = new();
    private static readonly List<(nint Handle, Exception Failure)> Retained = [];

    internal static void Retain(nint handle, Exception failure)
    {
        lock (Sync)
        {
            Retained.Add((handle, failure)); // never retry an ambiguous CloseHandle; OS exit is the owner
        }
    }
}

internal static class Win32BuildDirectoryEnumerator
{
    internal const int MaximumSuccessfulPages = 64;
    internal const int MaximumEntries = 4096;
    internal const int MaximumNameBytes = 1024 * 1024;

    internal static IReadOnlyList<Win32PublishedArtifactEntry> Collect(
        Func<int, Win32PublishedDirectoryReadResult> readPage)
    {
        ArgumentNullException.ThrowIfNull(readPage);
        var entries = new List<Win32PublishedArtifactEntry>();
        var names = 0;
        for (var page = 0; page < MaximumSuccessfulPages; page++)
        {
            var result = readPage(page);
            if (!result.Success)
            {
                if (result.Error == Win32PublishedFrameApi.ErrorNoMoreFiles) return Array.AsReadOnly(entries.ToArray());
                throw new Win32Exception(result.Error, "Handle-bound runtime directory enumeration failed.");
            }
            foreach (var entry in Win32BuildDirectoryPageParser.Parse(result.Page))
            {
                names = checked(names + Encoding.Unicode.GetByteCount(entry.Name));
                if (entries.Count >= MaximumEntries || names > MaximumNameBytes)
                {
                    throw new InvalidDataException("Runtime directory enumeration exceeded its independent bounds.");
                }
                entries.Add(entry);
            }
        }
        throw new InvalidDataException("Runtime directory enumeration exceeded its successful-page bound.");
    }
}

internal static class Win32BuildDirectoryPageParser
{
    internal const int FileNameOffset = 104;
    internal const int MaximumEntriesPerPage = 1024;
    internal const int MaximumNameBytes = 32 * 1024;
    private static readonly UnicodeEncoding StrictUtf16 = new(false, false, true);

    internal static IReadOnlyList<Win32PublishedArtifactEntry> Parse(ReadOnlySpan<byte> page)
    {
        var entries = new List<Win32PublishedArtifactEntry>();
        var offset = 0;
        while (true)
        {
            if (offset < 0 || offset > page.Length - FileNameOffset || entries.Count >= MaximumEntriesPerPage)
            {
                throw new InvalidDataException("Runtime directory entry offset exceeded its bound.");
            }
            var next = BinaryPrimitives.ReadUInt32LittleEndian(page.Slice(offset, 4));
            var attributes = BinaryPrimitives.ReadUInt32LittleEndian(page.Slice(offset + 56, 4));
            var rawNameBytes = BinaryPrimitives.ReadUInt32LittleEndian(page.Slice(offset + 60, 4));
            if (rawNameBytes > int.MaxValue) throw new InvalidDataException("Runtime directory name overflowed.");
            var nameBytes = (int)rawNameBytes;
            var recordEnd = (long)offset + FileNameOffset + nameBytes;
            if (nameBytes == 0 || (nameBytes & 1) != 0 || nameBytes > MaximumNameBytes || recordEnd > page.Length)
            {
                throw new InvalidDataException("Runtime directory name framing was invalid.");
            }
            string name;
            try
            {
                name = StrictUtf16.GetString(page.Slice(offset + FileNameOffset, nameBytes));
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("Runtime directory name was not strict UTF-16.", exception);
            }
            if (name is not "." and not "..")
            {
                ValidateLeafName(name);
                entries.Add(new Win32PublishedArtifactEntry(
                    name,
                    (attributes & Win32EvidenceConstants.FileAttributeDirectory) != 0,
                    (attributes & Win32EvidenceConstants.FileAttributeReparsePoint) != 0));
            }
            if (next == 0) break;
            if (next < FileNameOffset || (next & 7) != 0 || next > int.MaxValue ||
                recordEnd > (long)offset + next || (long)offset + next >= page.Length)
            {
                throw new InvalidDataException("Runtime directory continuation was invalid.");
            }
            offset = checked(offset + (int)next);
        }
        return Array.AsReadOnly(entries.ToArray());
    }

    private static void ValidateLeafName(string name)
    {
        if (name.Contains('\0') || name.Contains('\\') || name.Contains('/') || name.Contains(':') ||
            name.Any(character => character < ' ' || character is '"' or '<' or '>' or '|' or '?' or '*') ||
            Path.IsPathFullyQualified(name) ||
            name.EndsWith(' ') || name.EndsWith('.') || !name.IsNormalized(NormalizationForm.FormC))
        {
            throw new InvalidDataException("Runtime directory entry was not one exact normalized leaf name.");
        }
        var stem = name.Split('.')[0];
        if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("CLOCK$", StringComparison.OrdinalIgnoreCase) ||
            stem.Length == 4 &&
            (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
             stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
            stem[3] is >= '1' and <= '9')
        {
            throw new InvalidDataException("Runtime directory entry used an impossible DOS leaf name.");
        }
    }
}
