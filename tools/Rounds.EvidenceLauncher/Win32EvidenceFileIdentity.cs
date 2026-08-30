using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
using Rounds.Game;

namespace Rounds.EvidenceLauncher;

internal sealed record Win32ExecutableProfile(
    string ExpectedPath,
    string ExpectedSha256,
    string ExpectedFileVersion,
    string ExpectedProductVersion,
    long MaximumBytes)
{
    internal const long DefaultMaximumExecutableBytes = 512L * 1024 * 1024;

    internal static Win32ExecutableProfile Godot(BaseProjectileEvidenceLaunchPlan plan) =>
        new(
            plan.Executable,
            BaseProjectileEvidenceLaunchPlanner.GodotSha256,
            BaseProjectileEvidenceLaunchPlanner.GodotFileVersion,
            BaseProjectileEvidenceLaunchPlanner.GodotVersion,
            DefaultMaximumExecutableBytes);

    internal static Win32ExecutableProfile MsBuild() =>
        new(
            BaseProjectileEvidenceLaunchPlanner.MsBuildPath,
            BaseProjectileEvidenceLaunchPlanner.MsBuildSha256,
            BaseProjectileEvidenceLaunchPlanner.MsBuildFileVersion,
            BaseProjectileEvidenceLaunchPlanner.MsBuildProductVersion,
            DefaultMaximumExecutableBytes);
}

internal sealed record Win32RetainedFileSnapshot(
    string FinalPath,
    ulong VolumeSerialNumber,
    string FileId,
    long Length,
    long ChangeTime,
    uint Attributes,
    uint ReparseTag,
    uint LinkCount,
    bool DeletePending,
    bool Directory);

internal sealed record Win32RetainedFileVersion(
    string FileVersion,
    string ProductVersion);

internal interface IWin32RetainedFileApi : IWin32KernelHandleCloser
{
    nint OpenReadNoReplace(
        string normalizedAbsolutePath,
        uint desiredAccess,
        uint shareMode,
        uint creationDisposition,
        uint flagsAndAttributes);

    Win32RetainedFileSnapshot ReadSnapshot(nint handle);

    Stream OpenReadStream(nint handle);

    Win32RetainedFileVersion ReadVersion(nint retainedHandle, string normalizedFinalPath);
}

internal sealed partial class Win32ExecutableIdentityFactory(IWin32RetainedFileApi api)
{
    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex LowerSha256();

    internal Win32ExecutableLease OpenExpected(Win32ExecutableProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var expectedPath = NormalizeRequestedPath(profile.ExpectedPath);
        if (!LowerSha256().IsMatch(profile.ExpectedSha256) ||
            string.IsNullOrWhiteSpace(profile.ExpectedFileVersion) ||
            string.IsNullOrWhiteSpace(profile.ExpectedProductVersion) ||
            profile.MaximumBytes <= 0)
        {
            throw new ArgumentException("Executable identity profile is malformed.", nameof(profile));
        }

        var handle = api.OpenReadNoReplace(
            expectedPath,
            Win32EvidenceConstants.GenericRead,
            Win32EvidenceConstants.FileShareRead,
            Win32EvidenceConstants.OpenExisting,
            Win32EvidenceConstants.FileAttributeNormal |
            Win32EvidenceConstants.FileFlagOpenReparsePoint);
        if (handle == 0 || handle == -1)
        {
            throw new Win32Exception("CreateFileW failed for retained executable identity.");
        }

        try
        {
            var before = api.ReadSnapshot(handle);
            var canonicalFinalPath = ValidateSnapshot(before, expectedPath, profile.MaximumBytes);
            var sha256 = HashRetainedBytes(handle, before.Length, profile.MaximumBytes);
            var version = api.ReadVersion(handle, canonicalFinalPath);
            var after = api.ReadSnapshot(handle);
            if (!SameSnapshot(before, after) ||
                !string.Equals(
                    canonicalFinalPath,
                    NormalizeFinalPath(after.FinalPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Retained executable identity changed during attestation.");
            }
            if (!string.Equals(sha256, profile.ExpectedSha256, StringComparison.Ordinal) ||
                !string.Equals(version.FileVersion, profile.ExpectedFileVersion, StringComparison.Ordinal) ||
                !string.Equals(version.ProductVersion, profile.ExpectedProductVersion, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Retained executable hash or version did not match its profile.");
            }

            var identity = new EvidenceOpenedExecutableIdentity(
                canonicalFinalPath,
                Exists: true,
                IdentityBound: true,
                IsReparsePoint: false,
                OpenedHandleIdentity: FormatIdentity(before),
                sha256,
                version.FileVersion,
                version.ProductVersion);
            return new Win32ExecutableLease(api, handle, identity);
        }
        catch (Exception failure)
        {
            try
            {
                if (!api.CloseKernelHandle(handle))
                {
                    throw new Win32Exception("CloseHandle failed after executable identity refusal.");
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

    private string HashRetainedBytes(nint handle, long expectedLength, long maximumBytes)
    {
        using var stream = api.OpenReadStream(handle);
        if (!stream.CanRead || !stream.CanSeek || stream.Length != expectedLength ||
            expectedLength <= 0 || expectedLength > maximumBytes)
        {
            throw new InvalidOperationException("Retained executable stream shape was invalid.");
        }

        var originalPosition = stream.Position;
        Exception? failure = null;
        string? hash = null;
        try
        {
            stream.Position = 0;
            using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81_920];
            long consumed = 0;
            while (true)
            {
                var read = stream.Read(buffer, 0, buffer.Length);
                if (read == 0) break;
                consumed = checked(consumed + read);
                if (consumed > expectedLength || consumed > maximumBytes)
                {
                    throw new InvalidOperationException("Retained executable exceeded its attested size.");
                }
                hasher.AppendData(buffer, 0, read);
            }
            if (consumed != expectedLength)
            {
                throw new InvalidOperationException("Retained executable length changed while hashing.");
            }
            hash = Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant();
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            try
            {
                stream.Position = originalPosition;
            }
            catch (Exception positionException)
            {
                failure = failure is null
                    ? positionException
                    : new AggregateException(failure, positionException);
            }
        }
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        return hash!;
    }

    private static string ValidateSnapshot(
        Win32RetainedFileSnapshot snapshot,
        string expectedPath,
        long maximumBytes)
    {
        var finalPath = NormalizeFinalPath(snapshot.FinalPath);
        if (!string.Equals(finalPath, expectedPath, StringComparison.OrdinalIgnoreCase) ||
            snapshot.Directory || snapshot.DeletePending || snapshot.LinkCount != 1 ||
            snapshot.ReparseTag != 0 ||
            (snapshot.Attributes & Win32EvidenceConstants.FileAttributeDirectory) != 0 ||
            (snapshot.Attributes & Win32EvidenceConstants.FileAttributeReparsePoint) != 0 ||
            snapshot.Length <= 0 || snapshot.Length > maximumBytes ||
            !IsLowerHexFileId(snapshot.FileId))
        {
            throw new InvalidOperationException("Opened executable was not the exact regular identity-bound file.");
        }
        return finalPath;
    }

    private static bool SameSnapshot(
        Win32RetainedFileSnapshot before,
        Win32RetainedFileSnapshot after) =>
        before.VolumeSerialNumber == after.VolumeSerialNumber &&
        string.Equals(before.FileId, after.FileId, StringComparison.Ordinal) &&
        before.Length == after.Length && before.ChangeTime == after.ChangeTime &&
        before.Attributes == after.Attributes && before.ReparseTag == after.ReparseTag &&
        before.LinkCount == after.LinkCount && before.DeletePending == after.DeletePending &&
        before.Directory == after.Directory;

    private static string FormatIdentity(Win32RetainedFileSnapshot snapshot) =>
        $"volume:{snapshot.VolumeSerialNumber:x16}:file:{snapshot.FileId}";

    private static bool IsLowerHexFileId(string value) =>
        value.Length == 32 && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f') &&
        value.Any(character => character != '0');

    internal static string NormalizeRequestedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Contains('\0') ||
            path.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            path.StartsWith(@"\\.\", StringComparison.Ordinal) ||
            path.StartsWith(@"\??\", StringComparison.Ordinal) ||
            !Path.IsPathFullyQualified(path))
        {
            throw new ArgumentException("Executable path must be a normalized absolute DOS or UNC path.", nameof(path));
        }
        var normalized = Path.GetFullPath(path);
        var root = Path.GetPathRoot(normalized);
        if (string.IsNullOrEmpty(root) ||
            normalized[root.Length..].Contains(':') ||
            normalized.EndsWith(Path.DirectorySeparatorChar) ||
            normalized.EndsWith(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException("Executable path cannot name an ADS or directory.", nameof(path));
        }
        return normalized;
    }

    internal static string NormalizeFinalPath(string finalPath)
    {
        if (string.IsNullOrWhiteSpace(finalPath) || finalPath.Contains('\0'))
        {
            throw new InvalidOperationException("Final handle path was empty or malformed.");
        }
        var dosPath = finalPath.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)
            ? @"\\" + finalPath[8..]
            : finalPath.StartsWith(@"\\?\", StringComparison.Ordinal)
                ? finalPath[4..]
                : finalPath;
        if (!Path.IsPathFullyQualified(dosPath) || dosPath.StartsWith(@"\\.\", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Final handle path was not an absolute DOS or UNC path.");
        }
        return NormalizeRequestedPath(dosPath);
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct Win32FileAttributeTagInfo
{
    internal uint FileAttributes;
    internal uint ReparseTag;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Win32FileStandardInfo
{
    internal long AllocationSize;
    internal long EndOfFile;
    internal uint NumberOfLinks;
    internal byte DeletePending;
    internal byte Directory;
}

[StructLayout(LayoutKind.Sequential)]
internal struct Win32FileBasicInfo
{
    internal long CreationTime;
    internal long LastAccessTime;
    internal long LastWriteTime;
    internal long ChangeTime;
    internal uint FileAttributes;
}

internal sealed class Win32RetainedFileApi : IWin32RetainedFileApi
{
    public nint OpenReadNoReplace(
        string normalizedAbsolutePath,
        uint desiredAccess,
        uint shareMode,
        uint creationDisposition,
        uint flagsAndAttributes) =>
        Win32EvidenceNativeMethods.CreateFileW(
            normalizedAbsolutePath,
            desiredAccess,
            shareMode,
            0,
            creationDisposition,
            flagsAndAttributes,
            0);

    public Win32RetainedFileSnapshot ReadSnapshot(nint handle)
    {
        var standard = ReadInfo<Win32FileStandardInfo>(handle, 1);
        var basic = ReadInfo<Win32FileBasicInfo>(handle, 0);
        var tag = ReadInfo<Win32FileAttributeTagInfo>(handle, 9);
        var id = ReadInfo<Win32FileIdInfo>(handle, 18);
        if (basic.FileAttributes != tag.FileAttributes || id.FileId is null || id.FileId.Length != 16)
        {
            throw new Win32Exception("Retained file metadata was inconsistent.");
        }
        return new Win32RetainedFileSnapshot(
            ReadFinalPath(handle),
            id.VolumeSerialNumber,
            Convert.ToHexString(id.FileId).ToLowerInvariant(),
            standard.EndOfFile,
            basic.ChangeTime,
            tag.FileAttributes,
            tag.ReparseTag,
            standard.NumberOfLinks,
            standard.DeletePending != 0,
            standard.Directory != 0);
    }

    public Stream OpenReadStream(nint handle) =>
        new FileStream(new SafeFileHandle(handle, ownsHandle: false), FileAccess.Read);

    public Win32RetainedFileVersion ReadVersion(nint retainedHandle, string normalizedFinalPath)
    {
        if (retainedHandle == 0 || retainedHandle == -1)
        {
            throw new ArgumentOutOfRangeException(nameof(retainedHandle));
        }
        var version = FileVersionInfo.GetVersionInfo(normalizedFinalPath);
        return new Win32RetainedFileVersion(
            version.FileVersion ?? string.Empty,
            version.ProductVersion ?? string.Empty);
    }

    public bool CloseKernelHandle(nint handle) => Win32EvidenceNativeMethods.CloseHandle(handle);

    private static T ReadInfo<T>(nint handle, int informationClass) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var memory = Marshal.AllocHGlobal(size);
        try
        {
            if (!Win32FileIdentityNativeMethods.GetFileInformationByHandleEx(
                    handle,
                    informationClass,
                    memory,
                    checked((uint)size)))
            {
                throw new Win32Exception("GetFileInformationByHandleEx failed.");
            }
            return Marshal.PtrToStructure<T>(memory);
        }
        finally
        {
            Marshal.FreeHGlobal(memory);
        }
    }

    private static string ReadFinalPath(nint handle)
    {
        var required = Win32FileIdentityNativeMethods.GetFinalPathNameByHandleW(handle, null, 0, 0);
        if (required == 0) throw new Win32Exception("Final path size query failed.");
        var buffer = new StringBuilder(checked((int)required + 1));
        var written = Win32FileIdentityNativeMethods.GetFinalPathNameByHandleW(
            handle,
            buffer,
            checked((uint)buffer.Capacity),
            0);
        if (written == 0 || written >= buffer.Capacity)
        {
            throw new Win32Exception("Final path query failed or changed size.");
        }
        return buffer.ToString();
    }
}

internal static class Win32FileIdentityNativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetFileInformationByHandleEx(
        nint file,
        int informationClass,
        nint information,
        uint bufferSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern uint GetFinalPathNameByHandleW(
        nint file,
        StringBuilder? filePath,
        uint filePathLength,
        uint flags);
}
