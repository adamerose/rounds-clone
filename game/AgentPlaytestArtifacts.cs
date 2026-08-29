using System.Security.Cryptography;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Rounds.Game;

internal interface IAgentPlaytestRgba8Decoder
{
    DecodedAgentPlaytestFrame Decode(ReadOnlySpan<byte> encodedPng);
}

internal sealed class DecodedAgentPlaytestFrame
{
    private readonly byte[] _pixels;

    public DecodedAgentPlaytestFrame(int width, int height, ReadOnlySpan<byte> topToBottomStraightSrgbRgba8)
    {
        if (width <= 0 || height <= 0 ||
            width > AgentPlaytestLimits.MaximumWidth || height > AgentPlaytestLimits.MaximumHeight ||
            topToBottomStraightSrgbRgba8.Length != checked(width * height * 4))
        {
            throw new InvalidDataException("Decoded playtest frames must be bounded, tightly packed top-to-bottom straight-alpha sRGB RGBA8.");
        }
        Width = width;
        Height = height;
        _pixels = topToBottomStraightSrgbRgba8.ToArray();
    }

    public int Width { get; }
    public int Height { get; }
    public ReadOnlySpan<byte> Pixels => _pixels;
}

internal interface IAgentPlaytestRootLease : IDisposable
{
    string Root { get; }
    bool TryDeleteOwnedRoot();
}

internal interface IAgentPlaytestTrackedRootLease
{
    void TrackOwnedFile(string fileName);
    void MoveOwnedFile(string sourceFileName, string destinationFileName);
}

internal interface IAgentPlaytestRootAcquirer
{
    IAgentPlaytestRootLease Acquire(string absoluteRoot);
}

internal interface IAgentPlaytestParentIdentityLease : IDisposable
{
    bool MatchesCurrentPath();
}

internal interface IAgentPlaytestParentIdentityBinder
{
    IAgentPlaytestParentIdentityLease Bind(string normalizedParent);
}

internal sealed class WindowsAgentPlaytestParentIdentityBinder : IAgentPlaytestParentIdentityBinder
{
    public static WindowsAgentPlaytestParentIdentityBinder Instance { get; } = new();

    public IAgentPlaytestParentIdentityLease Bind(string normalizedParent) =>
        WindowsParentIdentityLease.Open(normalizedParent);

    private sealed class WindowsParentIdentityLease(
        string path,
        SafeFileHandle handle,
        ParentIdentity identity) : IAgentPlaytestParentIdentityLease
    {
        public static WindowsParentIdentityLease Open(string path)
        {
            if (!OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Atomic playtest parent binding requires Windows.");
            }
            var handle = NativeParent.OpenDirectory(path);
            try
            {
                var identity = NativeParent.Identity(handle);
                return new WindowsParentIdentityLease(path, handle, identity);
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }

        public bool MatchesCurrentPath()
        {
            if (handle.IsInvalid || handle.IsClosed)
            {
                return false;
            }
            try
            {
                using var current = NativeParent.OpenDirectory(path);
                return NativeParent.Identity(current) == identity;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        public void Dispose() => handle.Dispose();
    }

    private readonly record struct ParentIdentity(uint VolumeSerial, ulong FileIndex);

    private static class NativeParent
    {
        private const uint FileShareRead = 1;
        private const uint FileShareWrite = 2;
        private const uint OpenExisting = 3;
        private const uint FileFlagBackupSemantics = 0x02000000;
        private const uint FileFlagOpenReparsePoint = 0x00200000;
        private const uint FileAttributeDirectory = 0x10;
        private const uint FileAttributeReparsePoint = 0x400;

        internal static SafeFileHandle OpenDirectory(string path)
        {
            var handle = CreateFile(
                path,
                0,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                FileFlagBackupSemantics | FileFlagOpenReparsePoint,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                throw new IOException("The playtest parent directory could not be bound by handle.", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
            }
            var information = Information(handle);
            if ((information.FileAttributes & FileAttributeDirectory) == 0 ||
                (information.FileAttributes & FileAttributeReparsePoint) != 0)
            {
                handle.Dispose();
                throw new IOException("The playtest parent handle is not a non-reparse directory.");
            }
            return handle;
        }

        internal static ParentIdentity Identity(SafeFileHandle handle)
        {
            var information = Information(handle);
            return new ParentIdentity(
                information.VolumeSerialNumber,
                ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow);
        }

        private static ByHandleFileInformation Information(SafeFileHandle handle)
        {
            if (!GetFileInformationByHandle(handle, out var information))
            {
                throw new IOException("The playtest parent identity could not be read.", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
            }
            return information;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {
            public uint LowDateTime;
            public uint HighDateTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public FileTime CreationTime;
            public FileTime LastAccessTime;
            public FileTime LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

#pragma warning disable SYSLIB1054
        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle file,
            out ByHandleFileInformation information);
#pragma warning restore SYSLIB1054
    }
}

internal readonly record struct AgentPlaytestDirectoryIdentity(uint VolumeSerial, ulong FileIndex);

internal sealed class WindowsAgentPlaytestDirectoryIdentityLease : IDisposable
{
    private readonly string _path;
    private SafeFileHandle? _handle;

    private WindowsAgentPlaytestDirectoryIdentityLease(
        string path,
        SafeFileHandle handle,
        AgentPlaytestDirectoryIdentity identity)
    {
        _path = path;
        _handle = handle;
        Identity = identity;
    }

    public AgentPlaytestDirectoryIdentity Identity { get; }

    public static WindowsAgentPlaytestDirectoryIdentityLease Open(
        string path,
        bool shareDelete,
        bool requestDeleteAccess)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Atomic playtest child binding requires Windows.");
        }
        var handle = NativeDirectoryIdentity.OpenDirectory(path, shareDelete, requestDeleteAccess);
        try
        {
            return new(path, handle, NativeDirectoryIdentity.Identity(handle));
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    public bool MatchesCurrentPath()
    {
        if (_handle is null || _handle.IsInvalid || _handle.IsClosed)
        {
            return false;
        }
        try
        {
            using var current = NativeDirectoryIdentity.OpenDirectory(_path, shareDelete: true, requestDeleteAccess: false);
            return NativeDirectoryIdentity.Identity(current) == Identity;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool TryDeleteExactOnClose()
    {
        if (_handle is null || _handle.IsInvalid || _handle.IsClosed)
        {
            return false;
        }
        if (!NativeDirectoryIdentity.MarkDeleteOnClose(_handle))
        {
            return false;
        }
        _handle.Dispose();
        _handle = null;
        return true;
    }

    public void Dispose()
    {
        _handle?.Dispose();
        _handle = null;
    }

    private static class NativeDirectoryIdentity
    {
        private const uint DeleteAccess = 0x00010000;
        private const uint FileShareRead = 1;
        private const uint FileShareWrite = 2;
        private const uint FileShareDelete = 4;
        private const uint OpenExisting = 3;
        private const uint FileFlagBackupSemantics = 0x02000000;
        private const uint FileFlagOpenReparsePoint = 0x00200000;
        private const uint FileAttributeDirectory = 0x10;
        private const uint FileAttributeReparsePoint = 0x400;
        private const int FileDispositionInfo = 4;

        internal static SafeFileHandle OpenDirectory(string path, bool shareDelete, bool requestDeleteAccess)
        {
            var share = FileShareRead | FileShareWrite | (shareDelete ? FileShareDelete : 0);
            var handle = CreateFile(
                path,
                requestDeleteAccess ? DeleteAccess : 0,
                share,
                IntPtr.Zero,
                OpenExisting,
                FileFlagBackupSemantics | FileFlagOpenReparsePoint,
                IntPtr.Zero);
            if (handle.IsInvalid)
            {
                throw new IOException("The playtest child directory could not be bound by handle.", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
            }
            var information = Information(handle);
            if ((information.FileAttributes & FileAttributeDirectory) == 0 ||
                (information.FileAttributes & FileAttributeReparsePoint) != 0)
            {
                handle.Dispose();
                throw new IOException("The playtest child handle is not a non-reparse directory.");
            }
            return handle;
        }

        internal static AgentPlaytestDirectoryIdentity Identity(SafeFileHandle handle)
        {
            var information = Information(handle);
            return new(
                information.VolumeSerialNumber,
                ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow);
        }

        internal static bool MarkDeleteOnClose(SafeFileHandle handle)
        {
            var disposition = new FileDispositionInformation { DeleteFile = true };
            return SetFileInformationByHandle(
                handle,
                FileDispositionInfo,
                ref disposition,
                Marshal.SizeOf<FileDispositionInformation>());
        }

        private static ByHandleFileInformation Information(SafeFileHandle handle)
        {
            if (!GetFileInformationByHandle(handle, out var information))
            {
                throw new IOException("The playtest child identity could not be read.", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
            }
            return information;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileDispositionInformation
        {
            [MarshalAs(UnmanagedType.Bool)]
            public bool DeleteFile;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileTime
        {
            public uint LowDateTime;
            public uint HighDateTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ByHandleFileInformation
        {
            public uint FileAttributes;
            public FileTime CreationTime;
            public FileTime LastAccessTime;
            public FileTime LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

#pragma warning disable SYSLIB1054
        [DllImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandle(
            SafeFileHandle file,
            out ByHandleFileInformation information);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetFileInformationByHandle(
            SafeFileHandle file,
            int fileInformationClass,
            ref FileDispositionInformation fileInformation,
            int bufferSize);
#pragma warning restore SYSLIB1054
    }
}

internal interface IAgentPlaytestAcquisitionRaceHook
{
    void AfterStagingMoved(string normalizedRoot);
}

internal sealed class NoOpAgentPlaytestAcquisitionRaceHook : IAgentPlaytestAcquisitionRaceHook
{
    public static NoOpAgentPlaytestAcquisitionRaceHook Instance { get; } = new();
    public void AfterStagingMoved(string normalizedRoot) { }
}

internal static class AgentPlaytestOutputRoot
{
    public static bool TryNormalizeAbsentChild(string? path, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || Path.EndsInDirectorySeparator(path))
        {
            return false;
        }
        try
        {
            var root = Path.GetFullPath(path);
            var parent = Path.GetDirectoryName(root);
            if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent) ||
                File.Exists(root) || Directory.Exists(root))
            {
                return false;
            }
            var attributes = File.GetAttributes(parent);
            if ((attributes & FileAttributes.Directory) == 0 || (attributes & FileAttributes.ReparsePoint) != 0)
            {
                return false;
            }
            normalized = root;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static string NormalizeAbsentChild(string path)
    {
        if (!TryNormalizeAbsentChild(path, out var normalized))
        {
            throw new ArgumentException(
                "The playtest output root must be a normalized absent child of an existing non-reparse directory.",
                nameof(path));
        }
        return normalized;
    }
}

internal sealed class AtomicWindowsAgentPlaytestRootAcquirer : IAgentPlaytestRootAcquirer
{
    private readonly IAgentPlaytestParentIdentityBinder _parentBinder;
    private readonly IAgentPlaytestAcquisitionRaceHook _raceHook;

    public static AtomicWindowsAgentPlaytestRootAcquirer Instance { get; } =
        new(WindowsAgentPlaytestParentIdentityBinder.Instance, NoOpAgentPlaytestAcquisitionRaceHook.Instance);

    internal AtomicWindowsAgentPlaytestRootAcquirer(
        IAgentPlaytestParentIdentityBinder parentBinder,
        IAgentPlaytestAcquisitionRaceHook? raceHook = null)
    {
        _parentBinder = parentBinder ?? throw new ArgumentNullException(nameof(parentBinder));
        _raceHook = raceHook ?? NoOpAgentPlaytestAcquisitionRaceHook.Instance;
    }

    public IAgentPlaytestRootLease Acquire(string absoluteRoot)
    {
        if (string.IsNullOrWhiteSpace(absoluteRoot) || !Path.IsPathFullyQualified(absoluteRoot) ||
            Path.EndsInDirectorySeparator(absoluteRoot) ||
            !string.Equals(Path.GetFullPath(absoluteRoot), absoluteRoot, StringComparison.Ordinal) ||
            File.Exists(absoluteRoot) || Directory.Exists(absoluteRoot))
        {
            throw new ArgumentException("The root acquirer requires one already-normalized absent child.", nameof(absoluteRoot));
        }
        var normalizedRoot = absoluteRoot;
        var parent = Path.GetDirectoryName(normalizedRoot)!;
        if (!Directory.Exists(parent))
        {
            throw new ArgumentException("The root acquirer requires an existing non-reparse parent.", nameof(absoluteRoot));
        }
        var parentAttributes = File.GetAttributes(parent);
        if ((parentAttributes & FileAttributes.Directory) == 0 ||
            (parentAttributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new ArgumentException("The root acquirer requires an existing non-reparse parent.", nameof(absoluteRoot));
        }
        var siblingLockPath = Path.Combine(
            parent,
            Path.GetFileName(normalizedRoot) + ".rounds-agent-playtest-owner");
        var stagingRoot = Path.Combine(
            parent,
            "." + Path.GetFileName(normalizedRoot) + ".rounds-agent-playtest-staging-" + Guid.NewGuid().ToString("N"));
        IAgentPlaytestParentIdentityLease? parentLease = null;
        FileStream? siblingLock = null;
        FileStream? marker = null;
        WindowsAgentPlaytestDirectoryIdentityLease? stagingLease = null;
        WindowsAgentPlaytestDirectoryIdentityLease? childLease = null;
        var stagingCreated = false;
        var movedToRoot = false;
        var childIdentityBound = false;
        var parentIdentityLost = false;
        try
        {
            parentLease = _parentBinder.Bind(parent);
            RequireStableParent(parentLease, ref parentIdentityLost);
            siblingLock = new FileStream(
                siblingLockPath,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                1,
                FileOptions.DeleteOnClose);
            RequireStableParent(parentLease, ref parentIdentityLost);
            if (!OperatingSystem.IsWindows() || NativeDirectory.CreateDirectory(stagingRoot, IntPtr.Zero) == 0)
            {
                throw new IOException("The playtest staging root could not be acquired exclusively.");
            }
            stagingCreated = true;
            stagingLease = WindowsAgentPlaytestDirectoryIdentityLease.Open(
                stagingRoot, shareDelete: true, requestDeleteAccess: false);
            RequireStableParent(parentLease, ref parentIdentityLost);
            Directory.Move(stagingRoot, normalizedRoot);
            movedToRoot = true;
            _raceHook.AfterStagingMoved(normalizedRoot);
            childLease = WindowsAgentPlaytestDirectoryIdentityLease.Open(
                normalizedRoot, shareDelete: false, requestDeleteAccess: true);
            if (childLease.Identity != stagingLease.Identity)
            {
                throw new IOException("The playtest output root identity changed before ownership was bound.");
            }
            childIdentityBound = true;
            stagingLease.Dispose();
            stagingLease = null;
            marker = new FileStream(
                Path.Combine(normalizedRoot, ".rounds-agent-playtest-owner"),
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                1,
                FileOptions.DeleteOnClose);
            RequireStableParent(parentLease, ref parentIdentityLost);
            return new AgentPlaytestRootLease(normalizedRoot, parentLease, siblingLock, marker, childLease);
        }
        catch
        {
            marker?.Dispose();
            try
            {
                if (childIdentityBound && childLease is not null && childLease.MatchesCurrentPath() &&
                    !Directory.EnumerateFileSystemEntries(normalizedRoot).Any())
                {
                    childLease.TryDeleteExactOnClose();
                }
                else if (!movedToRoot && stagingCreated && stagingLease is not null &&
                    stagingLease.MatchesCurrentPath() && !Directory.EnumerateFileSystemEntries(stagingRoot).Any())
                {
                    var stagingIdentity = stagingLease.Identity;
                    stagingLease.Dispose();
                    stagingLease = null;
                    using var cleanupLease = WindowsAgentPlaytestDirectoryIdentityLease.Open(
                        stagingRoot, shareDelete: false, requestDeleteAccess: true);
                    if (cleanupLease.Identity == stagingIdentity &&
                        !Directory.EnumerateFileSystemEntries(stagingRoot).Any())
                    {
                        cleanupLease.TryDeleteExactOnClose();
                    }
                }
            }
            finally
            {
                childLease?.Dispose();
                stagingLease?.Dispose();
                siblingLock?.Dispose();
                parentLease?.Dispose();
            }
            throw;
        }
    }

    private static void RequireStableParent(
        IAgentPlaytestParentIdentityLease parentLease,
        ref bool parentIdentityLost)
    {
        if (parentLease.MatchesCurrentPath())
        {
            return;
        }
        parentIdentityLost = true;
        throw new IOException("The playtest parent directory identity changed during acquisition.");
    }

    private sealed class AgentPlaytestRootLease(
        string root,
        IAgentPlaytestParentIdentityLease parentLease,
        FileStream siblingLock,
        FileStream marker,
        WindowsAgentPlaytestDirectoryIdentityLease childLease) : IAgentPlaytestRootLease, IAgentPlaytestTrackedRootLease
    {
        private FileStream? _marker = marker;
        private WindowsAgentPlaytestDirectoryIdentityLease? _childLease = childLease;
        private readonly HashSet<string> _ownedFiles = new(StringComparer.Ordinal);
        private bool _ownershipLost;
        private bool _ownedRootRemoved;

        public string Root { get; } = root;

        public void TrackOwnedFile(string fileName)
        {
            if (_ownershipLost || _ownedRootRemoved || Path.GetFileName(fileName) != fileName ||
                !_ownedFiles.Add(fileName))
            {
                throw new IOException("The playtest artifact ownership ledger rejected a file registration.");
            }
        }

        public void MoveOwnedFile(string sourceFileName, string destinationFileName)
        {
            if (_ownershipLost || _ownedRootRemoved || Path.GetFileName(sourceFileName) != sourceFileName ||
                Path.GetFileName(destinationFileName) != destinationFileName ||
                !_ownedFiles.Remove(sourceFileName) || !_ownedFiles.Add(destinationFileName))
            {
                throw new IOException("The playtest artifact ownership ledger rejected a file move.");
            }
        }

        public bool TryDeleteOwnedRoot()
        {
            if (_ownedRootRemoved)
            {
                return true;
            }
            if (_ownershipLost || _childLease is null || !parentLease.MatchesCurrentPath() ||
                !_childLease.MatchesCurrentPath())
            {
                _ownershipLost = true;
                return false;
            }
            _marker?.Dispose();
            _marker = null;
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(Root))
                {
                    var name = Path.GetFileName(entry);
                    if (Directory.Exists(entry) || !_ownedFiles.Contains(name))
                    {
                        return false;
                    }
                    File.Delete(entry);
                    _ownedFiles.Remove(name);
                }
                if (!_childLease.MatchesCurrentPath() || !_childLease.TryDeleteExactOnClose())
                {
                    _ownershipLost = true;
                    return false;
                }
                _childLease = null;
                _ownedRootRemoved = true;
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        public void Dispose()
        {
            _marker?.Dispose();
            _marker = null;
            _childLease?.Dispose();
            _childLease = null;
            siblingLock.Dispose();
            parentLease.Dispose();
        }
    }

    private static class NativeDirectory
    {
#pragma warning disable SYSLIB1054
        [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int CreateDirectory(string path, IntPtr securityAttributes);
#pragma warning restore SYSLIB1054
    }
}

internal sealed class AgentPlaytestArtifactOwner : IDisposable, IAgentPlaytestFinalizedFrameVerifier
{
    private const int MaximumCleanupAttempts = 3;
    private readonly string _rootWithSeparator;
    private readonly long _maximumOutputBytes;
    private IAgentPlaytestRootLease? _rootLease;
    private FileStream? _traceLease;
    private bool _disposed;
    private bool _completed;
    private bool _cleanupExhausted;

    private AgentPlaytestArtifactOwner(IAgentPlaytestRootLease rootLease, long maximumOutputBytes)
    {
        _rootLease = rootLease;
        Root = rootLease.Root;
        _rootWithSeparator = Root + Path.DirectorySeparatorChar;
        _maximumOutputBytes = maximumOutputBytes;
    }

    public string Root { get; }

    public static AgentPlaytestArtifactOwner Create(
        string absoluteAbsentRoot,
        IAgentPlaytestRootAcquirer? acquirer = null,
        long maximumOutputBytes = AgentPlaytestLimits.MaximumOutputBytes)
    {
        if (maximumOutputBytes is < 1 or > AgentPlaytestLimits.MaximumOutputBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumOutputBytes));
        }
        var root = AgentPlaytestOutputRoot.NormalizeAbsentChild(absoluteAbsentRoot);
        var lease = (acquirer ?? AtomicWindowsAgentPlaytestRootAcquirer.Instance).Acquire(root);
        if (!string.Equals(lease.Root, root, StringComparison.Ordinal))
        {
            lease.Dispose();
            throw new IOException("The root acquirer returned a lease for a different path.");
        }
        return new AgentPlaytestArtifactOwner(lease, maximumOutputBytes);
    }

    public (AgentPlaytestFrameResponse Response, HumanPlaytestObservation Observation) PublishFrame(
        int sequence,
        ReadOnlySpan<byte> encodedPng,
        IAgentPlaytestRgba8Decoder decoder,
        bool terminal)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (sequence is < 0 or >= AgentPlaytestLimits.MaximumFrames)
        {
            throw new AgentPlaytestFailure(sequence, "resource", "resource-limit-exceeded", "The frame budget was exceeded.");
        }
        ArgumentNullException.ThrowIfNull(decoder);
        var partial = ExpectedFramePath(sequence) + ".partial";
        var final = ExpectedFramePath(sequence);
        try
        {
            EnsureCanAdd(encodedPng.Length, sequence);
            using (var stream = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                TrackOwnedFile(partial);
                stream.Write(encodedPng);
                stream.Flush(flushToDisk: true);
            }
            var decoded = decoder.Decode(encodedPng);
            var hash = Convert.ToHexString(SHA256.HashData(encodedPng)).ToLowerInvariant();
            File.Move(partial, final, overwrite: false);
            MoveOwnedFile(partial, final);
            var response = new AgentPlaytestFrameResponse(sequence, final, hash, decoded.Width, decoded.Height, terminal);
            return (response, VerifyResponse(response, decoder));
        }
        catch (AgentPlaytestFailure)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new AgentPlaytestFailure(sequence, "frame", "frame-publish-failed", exception.Message);
        }
    }

    public HumanPlaytestObservation VerifyResponse(
        AgentPlaytestFrameResponse response,
        IAgentPlaytestRgba8Decoder decoder)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(decoder);
        var expected = ExpectedFramePath(response.FrameSequence);
        if (!string.Equals(Path.GetFullPath(response.FramePath), expected, StringComparison.Ordinal) ||
            !response.FramePath.StartsWith(_rootWithSeparator, StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(expected))
        {
            throw new AgentPlaytestFailure(response.FrameSequence, "frame", "frame-publish-failed", "The response frame path is not the exact finalized task-owned path.");
        }
        var bytes = File.ReadAllBytes(expected);
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var decoded = decoder.Decode(bytes);
        if (!string.Equals(hash, response.FrameSha256, StringComparison.Ordinal) ||
            decoded.Width != response.Width || decoded.Height != response.Height)
        {
            throw new AgentPlaytestFailure(response.FrameSequence, "frame", "frame-publish-failed", "The finalized frame hash or dimensions do not match the response.");
        }
        return new HumanPlaytestObservation(response.FrameSequence, decoded.Pixels, decoded.Width, decoded.Height);
    }

    public void PublishTrace(ReadOnlySpan<byte> traceBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var tracePartial = Path.Combine(Root, "trace.jsonl.partial");
        var traceFinal = Path.Combine(Root, "trace.jsonl");
        EnsureCanAdd(traceBytes.Length, null);
        WriteNew(tracePartial, traceBytes);
        File.Move(tracePartial, traceFinal, overwrite: false);
        MoveOwnedFile(tracePartial, traceFinal);
        _traceLease = new FileStream(traceFinal, FileMode.Open, FileAccess.Read, FileShare.Read);
    }

    public byte[] ReadFinalTrace()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_traceLease is null)
        {
            throw new AgentPlaytestFailure(null, "replay", "replay-mismatch", "The finalized trace is not locked for verification.");
        }
        _traceLease.Position = 0;
        using var copy = new MemoryStream();
        _traceLease.CopyTo(copy);
        return copy.ToArray();
    }

    public void PublishManifest(ReadOnlySpan<byte> manifestBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var manifestPartial = Path.Combine(Root, "manifest.json.partial");
        var manifestFinal = Path.Combine(Root, "manifest.json");
        EnsureCanAdd(manifestBytes.Length, null);
        WriteNew(manifestPartial, manifestBytes);
        File.Move(manifestPartial, manifestFinal, overwrite: false);
        MoveOwnedFile(manifestPartial, manifestFinal);
        AgentPlaytestManifestCodec.ValidateCanonical(File.ReadAllBytes(manifestFinal));
        _completed = true;
    }

    public void CleanupFailedRun()
    {
        if (_disposed || _rootLease is null)
        {
            return;
        }
        var lease = _rootLease;
        _traceLease?.Dispose();
        _traceLease = null;
        for (var attempt = 0; attempt < MaximumCleanupAttempts; attempt++)
        {
            if (!lease.TryDeleteOwnedRoot())
            {
                continue;
            }
            lease.Dispose();
            _rootLease = null;
            _disposed = true;
            return;
        }
        _cleanupExhausted = true;
        throw new IOException("The owned playtest output root could not be deleted after bounded retries.");
    }

    public string ExpectedFramePath(int sequence) =>
        Path.GetFullPath(Path.Combine(Root, $"frame-{sequence:0000}.png"));

    public void Dispose()
    {
        if (!_completed)
        {
            if (_cleanupExhausted)
            {
                _rootLease?.Dispose();
                _rootLease = null;
            }
            else
            {
                CleanupFailedRun();
            }
        }
        else
        {
            _traceLease?.Dispose();
            _traceLease = null;
            _rootLease?.Dispose();
            _rootLease = null;
        }
        _disposed = true;
    }

    private void WriteNew(string path, ReadOnlySpan<byte> bytes)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        TrackOwnedFile(path);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    private void TrackOwnedFile(string path) =>
        (_rootLease as IAgentPlaytestTrackedRootLease)?.TrackOwnedFile(Path.GetFileName(path));

    private void MoveOwnedFile(string sourcePath, string destinationPath) =>
        (_rootLease as IAgentPlaytestTrackedRootLease)?.MoveOwnedFile(
            Path.GetFileName(sourcePath),
            Path.GetFileName(destinationPath));

    private void EnsureCanAdd(long byteCount, int? sequence)
    {
        var current = Directory.EnumerateFiles(Root).Sum(static path => new FileInfo(path).Length);
        if (byteCount < 0 || current > _maximumOutputBytes || byteCount > _maximumOutputBytes - current)
        {
            throw new AgentPlaytestFailure(sequence, "resource", "resource-limit-exceeded", "The artifact output cap would be exceeded.");
        }
    }
}

internal sealed record AgentPlaytestOwnerConfiguration(
    bool BelowNormalPriority,
    int LogicalProcessors,
    int OwnerTimeoutSeconds,
    int MaximumProcessCount,
    long MaximumPrivateMemoryBytes,
    long MaximumDedicatedGpuMemoryBytes,
    double MaximumGpuUtilization,
    bool HeartbeatGateEnabled,
    bool ExactProcessTreeCleanupEnabled)
{
    public static AgentPlaytestOwnerConfiguration Required { get; } = new(
        true,
        AgentPlaytestLimits.MaximumLogicalProcessors,
        AgentPlaytestLimits.OwnerTimeoutSeconds,
        AgentPlaytestLimits.MaximumProcessCount,
        AgentPlaytestLimits.MaximumPrivateMemoryBytes,
        AgentPlaytestLimits.MaximumDedicatedGpuMemoryBytes,
        AgentPlaytestLimits.MaximumGpuUtilization,
        true,
        true);

    public bool IsRendererEvidenceEligible() => this == Required;
}

internal sealed record AgentPlaytestResourceSample(
    int ProcessCount,
    long PrivateMemoryBytes,
    long DedicatedGpuMemoryBytes,
    double TotalGpuUtilization,
    int FramesInPreviousSecond,
    int Width,
    int Height,
    double HeartbeatBaselineP95Milliseconds,
    double HeartbeatP95Milliseconds,
    double MaximumHeartbeatDelayMilliseconds,
    int WindowScreen,
    bool WindowNonActivating);

internal static class AgentPlaytestResourceGate
{
    public static bool AcceptsRendererEvidence(
        AgentPlaytestOwnerConfiguration configuration,
        IReadOnlyList<AgentPlaytestResourceSample> samples)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(samples);
        if (!configuration.IsRendererEvidenceEligible() || samples.Count == 0)
        {
            return false;
        }
        foreach (var sample in samples)
        {
            var heartbeatCeiling = System.Math.Min(
                AgentPlaytestLimits.MaximumHeartbeatP95Milliseconds,
                sample.HeartbeatBaselineP95Milliseconds + AgentPlaytestLimits.MaximumHeartbeatIncreaseMilliseconds);
            if (sample.ProcessCount is < 1 or > AgentPlaytestLimits.MaximumProcessCount ||
                sample.PrivateMemoryBytes is < 0 or > AgentPlaytestLimits.MaximumPrivateMemoryBytes ||
                sample.DedicatedGpuMemoryBytes is < 0 or > AgentPlaytestLimits.MaximumDedicatedGpuMemoryBytes ||
                !double.IsFinite(sample.TotalGpuUtilization) ||
                sample.TotalGpuUtilization is < 0.0 or > AgentPlaytestLimits.MaximumGpuUtilization ||
                sample.FramesInPreviousSecond is < 0 or > AgentPlaytestLimits.MaximumFramesPerSecond ||
                sample.Width is < 1 or > AgentPlaytestLimits.MaximumWidth ||
                sample.Height is < 1 or > AgentPlaytestLimits.MaximumHeight ||
                !double.IsFinite(sample.HeartbeatBaselineP95Milliseconds) || sample.HeartbeatBaselineP95Milliseconds < 0.0 ||
                !double.IsFinite(sample.HeartbeatP95Milliseconds) || sample.HeartbeatP95Milliseconds < 0.0 ||
                sample.HeartbeatP95Milliseconds > heartbeatCeiling ||
                !double.IsFinite(sample.MaximumHeartbeatDelayMilliseconds) ||
                sample.MaximumHeartbeatDelayMilliseconds < 0.0 ||
                sample.MaximumHeartbeatDelayMilliseconds > AgentPlaytestLimits.MaximumHeartbeatDelayMilliseconds ||
                sample.WindowScreen != 3 || !sample.WindowNonActivating)
            {
                return false;
            }
        }
        return true;
    }
}
