using System.Buffers.Binary;
using System.ComponentModel;
using System.IO.Compression;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Rounds.Game;

namespace Rounds.EvidenceLauncher;

internal sealed record Win32PublishedArtifactEntry(
    string Name,
    bool Directory,
    bool ReparsePoint);

internal sealed record Win32PublishedStreamEntry(string Name, long Length);

internal interface IWin32PublishedFrameApi : IWin32KernelHandleCloser
{
    nint OpenRoot(string normalizedRoot, uint desiredAccess, uint shareMode, uint flagsAndAttributes);

    nint OpenFrame(string normalizedFrame, uint desiredAccess, uint shareMode, uint flagsAndAttributes);

    Win32RetainedFileSnapshot ReadSnapshot(nint handle);

    IReadOnlyList<Win32PublishedArtifactEntry> EnumerateRoot(nint retainedRootHandle);

    IReadOnlyList<Win32PublishedStreamEntry> EnumerateFrameStreams(nint retainedFrameHandle);

    bool ProbeOwnershipMarkerLocked(nint retainedRootHandle, string exactMarkerName);

    Stream OpenFrameStream(nint retainedFrameHandle);
}

internal sealed record Win32PngDecodeResult(
    int Width,
    int Height,
    bool Rgba8,
    int DecodedBytes,
    string RgbaSha256);

internal interface IWin32PngDecoder
{
    Win32PngDecodeResult Decode(ReadOnlyMemory<byte> png, int requiredWidth, int requiredHeight);
}

internal sealed partial class Win32PublishedFrameValidator(
    IWin32PublishedFrameApi api,
    IWin32PngDecoder decoder)
{
    internal const string FrameName = "frame-0000.png";
    internal const string OwnerMarkerName = ".rounds-agent-playtest-owner";
    internal const int RequiredWidth = 1920;
    internal const int RequiredHeight = 1080;
    internal const long MaximumPngBytes = 9L * 1024 * 1024;
    internal const uint RootDesiredAccess = 0x00000001; // FILE_LIST_DIRECTORY
    internal const uint RetainedShareMode =
        Win32EvidenceConstants.FileShareRead |
        Win32EvidenceConstants.FileShareDelete;
    internal const uint RootFlags =
        Win32EvidenceConstants.FileFlagBackupSemantics |
        Win32EvidenceConstants.FileFlagOpenReparsePoint;
    internal const uint FrameFlags = Win32EvidenceConstants.FileFlagOpenReparsePoint;

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex LowerSha256();

    internal IEvidencePublishedFrameValidationLease ValidatePublishedFrame(
        BaseProjectileEvidenceLaunchPlan plan,
        DebugBaseProjectileEvidenceAttestation marker)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(marker);
        var rootPath = NormalizeExactRoot(plan.OutputRoot);
        if (!string.Equals(marker.Frame, FrameName, StringComparison.Ordinal) ||
            !LowerSha256().IsMatch(marker.PngSha256))
        {
            throw new InvalidOperationException("Published-frame marker identity was not literal.");
        }
        var framePath = Path.Combine(rootPath, FrameName);

        nint rootHandle = 0;
        nint frameHandle = 0;
        IEvidencePublishedFrameValidationLease? validationLease = null;
        Exception? failure = null;
        try
        {
            rootHandle = api.OpenRoot(rootPath, RootDesiredAccess, RetainedShareMode, RootFlags);
            RequireHandle(rootHandle, "output root");
            var rootBefore = api.ReadSnapshot(rootHandle);
            ValidateRootSnapshot(rootBefore, rootPath);

            frameHandle = api.OpenFrame(
                framePath,
                Win32EvidenceConstants.GenericRead,
                RetainedShareMode,
                FrameFlags);
            RequireHandle(frameHandle, "published frame");
            var frameBefore = api.ReadSnapshot(frameHandle);
            ValidateFrameSnapshot(frameBefore, framePath);
            if (rootBefore.VolumeSerialNumber != frameBefore.VolumeSerialNumber ||
                string.Equals(rootBefore.FileId, frameBefore.FileId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Published root and frame were not distinct identities on one volume.");
            }
            var streams = api.EnumerateFrameStreams(frameHandle);
            if (streams.Count != 1 ||
                !string.Equals(streams[0].Name, "::$DATA", StringComparison.Ordinal) ||
                streams[0].Length != frameBefore.Length)
            {
                throw new InvalidOperationException("Published frame contained an alternate or inconsistent data stream.");
            }

            var entriesBefore = ValidateEntries(api.EnumerateRoot(rootHandle));
            if (!api.ProbeOwnershipMarkerLocked(rootHandle, OwnerMarkerName))
            {
                throw new InvalidOperationException("The child ownership marker was not retained with an exact sharing lock.");
            }
            var bytes = ReadExactRetainedBytes(frameHandle, frameBefore.Length);
            var sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            if (!string.Equals(sha256, marker.PngSha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Published-frame SHA-256 did not match the completion marker.");
            }
            var decoded = decoder.Decode(bytes, RequiredWidth, RequiredHeight);
            if (decoded.Width != RequiredWidth || decoded.Height != RequiredHeight || !decoded.Rgba8 ||
                decoded.DecodedBytes != checked(RequiredWidth * RequiredHeight * 4))
            {
                throw new InvalidDataException("Published frame did not decode as the exact RGBA8 evidence frame.");
            }

            var frameAfter = api.ReadSnapshot(frameHandle);
            var rootAfter = api.ReadSnapshot(rootHandle);
            ValidateFrameSnapshot(frameAfter, framePath);
            ValidateRootSnapshot(rootAfter, rootPath);
            if (rootAfter.VolumeSerialNumber != frameAfter.VolumeSerialNumber ||
                string.Equals(rootAfter.FileId, frameAfter.FileId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Published root and frame post-snapshots lost same-volume identity binding.");
            }
            if (!SameSnapshot(frameBefore, frameAfter) || !SameSnapshot(rootBefore, rootAfter))
            {
                throw new InvalidOperationException("Published root or frame identity changed during validation.");
            }
            var entriesAfter = ValidateEntries(api.EnumerateRoot(rootHandle));
            if (!entriesBefore.SequenceEqual(entriesAfter, StringComparer.Ordinal))
            {
                throw new InvalidOperationException("Published root enumeration changed during validation.");
            }

            validationLease = new Win32PublishedFrameValidationLease(
                api,
                rootHandle,
                frameHandle,
                new EvidencePublishedFrameValidation(
                    rootPath,
                    FrameName,
                    sha256,
                    decoded.Width,
                    decoded.Height,
                    Rgba8: true,
                    RootIdentityBound: true,
                    FrameIdentityBound: true,
                    RootLeaseObserved: true,
                    FrameLeaseObserved: true,
                    ContainsOnlyExpectedFrame: true));
            rootHandle = 0;
            frameHandle = 0;
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            CloseHandle(frameHandle, "published frame", ref failure);
            CloseHandle(rootHandle, "output root", ref failure);
        }

        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        return validationLease!;
    }

    private byte[] ReadExactRetainedBytes(nint frameHandle, long expectedLength)
    {
        if (expectedLength <= 0 || expectedLength > MaximumPngBytes || expectedLength > int.MaxValue)
        {
            throw new InvalidDataException("Published PNG length was outside the exact bounded contract.");
        }

        Stream? stream = null;
        byte[]? bytes = null;
        Exception? failure = null;
        try
        {
            stream = api.OpenFrameStream(frameHandle);
            if (!stream.CanRead || !stream.CanSeek || stream.Length != expectedLength)
            {
                throw new InvalidDataException("Retained frame stream shape did not match its snapshot.");
            }
            stream.Position = 0;
            bytes = new byte[checked((int)expectedLength)];
            var consumed = 0;
            while (consumed < bytes.Length)
            {
                var read = stream.Read(bytes, consumed, bytes.Length - consumed);
                if (read <= 0)
                {
                    throw new EndOfStreamException("Retained frame stream ended before its attested length.");
                }
                consumed = checked(consumed + read);
            }
            if (stream.ReadByte() != -1)
            {
                throw new InvalidDataException("Retained frame stream exceeded its attested length.");
            }
        }
        catch (Exception exception)
        {
            failure = exception;
        }
        finally
        {
            if (stream is not null)
            {
                try
                {
                    stream.Dispose();
                }
                catch (Exception exception)
                {
                    failure = Combine(failure, exception);
                }
            }
        }
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
        return bytes!;
    }

    private static string[] ValidateEntries(IReadOnlyList<Win32PublishedArtifactEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count != 2)
        {
            throw new InvalidOperationException("Published root did not contain exactly the frame and retained owner marker.");
        }
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry is null || string.IsNullOrEmpty(entry.Name) ||
                entry.Name != Path.GetFileName(entry.Name) || entry.Directory || entry.ReparsePoint ||
                (entry.Name != FrameName && entry.Name != OwnerMarkerName) || !names.Add(entry.Name))
            {
                throw new InvalidOperationException("Published root contained an unexpected, duplicate, or linked entry.");
            }
        }
        if (!names.Contains(FrameName) || !names.Contains(OwnerMarkerName))
        {
            throw new InvalidOperationException("Published root was missing the exact frame or owner marker.");
        }
        return names.Order(StringComparer.Ordinal).ToArray();
    }

    private static string NormalizeExactRoot(string outputRoot)
    {
        if (string.IsNullOrWhiteSpace(outputRoot) || outputRoot.Contains('\0') ||
            !Path.IsPathFullyQualified(outputRoot) || Path.EndsInDirectorySeparator(outputRoot) ||
            outputRoot.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            outputRoot.StartsWith(@"\\.\", StringComparison.Ordinal) ||
            outputRoot.StartsWith(@"\??\", StringComparison.Ordinal))
        {
            throw new ArgumentException("Output root was not an exact normalized absolute path.", nameof(outputRoot));
        }
        var normalized = Path.GetFullPath(outputRoot);
        var root = Path.GetPathRoot(normalized);
        if (!string.Equals(normalized, outputRoot, StringComparison.Ordinal) || string.IsNullOrEmpty(root) ||
            normalized[root.Length..].Contains(':'))
        {
            throw new ArgumentException("Output root contained normalization, case, or ADS drift.", nameof(outputRoot));
        }
        return normalized;
    }

    private static void ValidateRootSnapshot(Win32RetainedFileSnapshot snapshot, string expectedPath)
    {
        if (!string.Equals(Win32ExecutableIdentityFactory.NormalizeFinalPath(snapshot.FinalPath), expectedPath, StringComparison.Ordinal) ||
            !snapshot.Directory || snapshot.DeletePending || snapshot.ReparseTag != 0 ||
            (snapshot.Attributes & Win32EvidenceConstants.FileAttributeDirectory) == 0 ||
            (snapshot.Attributes & Win32EvidenceConstants.FileAttributeReparsePoint) != 0 ||
            !HasIdentity(snapshot))
        {
            throw new InvalidOperationException("Retained output root was not the exact stable non-reparse directory.");
        }
    }

    private static void ValidateFrameSnapshot(Win32RetainedFileSnapshot snapshot, string expectedPath)
    {
        if (!string.Equals(Win32ExecutableIdentityFactory.NormalizeFinalPath(snapshot.FinalPath), expectedPath, StringComparison.Ordinal) ||
            snapshot.Directory || snapshot.DeletePending || snapshot.ReparseTag != 0 || snapshot.LinkCount != 1 ||
            (snapshot.Attributes & (Win32EvidenceConstants.FileAttributeDirectory |
                                    Win32EvidenceConstants.FileAttributeReparsePoint)) != 0 ||
            snapshot.Length <= 0 || snapshot.Length > MaximumPngBytes || !HasIdentity(snapshot))
        {
            throw new InvalidOperationException("Retained frame was not the exact stable single-link regular file.");
        }
    }

    private static bool HasIdentity(Win32RetainedFileSnapshot snapshot) =>
        snapshot.VolumeSerialNumber != 0 && snapshot.FileId.Length == 32 &&
        snapshot.FileId.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f') &&
        snapshot.FileId.Any(character => character != '0');

    private static bool SameSnapshot(Win32RetainedFileSnapshot left, Win32RetainedFileSnapshot right) =>
        string.Equals(left.FinalPath, right.FinalPath, StringComparison.Ordinal) &&
        left.VolumeSerialNumber == right.VolumeSerialNumber &&
        string.Equals(left.FileId, right.FileId, StringComparison.Ordinal) &&
        left.Length == right.Length && left.ChangeTime == right.ChangeTime &&
        left.Attributes == right.Attributes && left.ReparseTag == right.ReparseTag &&
        left.LinkCount == right.LinkCount && left.DeletePending == right.DeletePending &&
        left.Directory == right.Directory;

    private static void RequireHandle(nint handle, string target)
    {
        if (handle is 0 or -1) throw new Win32Exception($"Opening the retained {target} failed.");
    }

    private void CloseHandle(nint handle, string target, ref Exception? failure)
    {
        if (handle is 0 or -1) return;
        try
        {
            if (!api.CloseKernelHandle(handle))
            {
                throw new Win32Exception($"Closing the retained {target} handle failed.");
            }
        }
        catch (Exception exception)
        {
            failure = Combine(failure, exception);
        }
    }

    private static Exception Combine(Exception? first, Exception second) =>
        first is null ? second : new AggregateException(first, second);
}

internal sealed class Win32PublishedFrameValidationLease(
    IWin32KernelHandleCloser closer,
    nint rootHandle,
    nint frameHandle,
    EvidencePublishedFrameValidation validation) : IEvidencePublishedFrameValidationLease
{
    private nint _rootHandle = rootHandle;
    private nint _frameHandle = frameHandle;

    public EvidencePublishedFrameValidation Validation { get; } = validation;

    public void Dispose()
    {
        Exception? failure = null;
        Close(ref _frameHandle, "published frame", ref failure);
        Close(ref _rootHandle, "output root", ref failure);
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private void Close(ref nint handle, string target, ref Exception? failure)
    {
        var owned = Interlocked.Exchange(ref handle, 0);
        if (owned is 0 or -1) return;
        try
        {
            if (!closer.CloseKernelHandle(owned))
            {
                throw new Win32Exception($"Closing the retained {target} handle failed.");
            }
        }
        catch (Exception exception)
        {
            failure = failure is null ? exception : new AggregateException(failure, exception);
        }
    }
}

internal sealed class Win32PublishedFrameApi : IWin32PublishedFrameApi
{
    internal const int FileStreamInfo = 0x07;
    internal const int FileIdBothDirectoryInfo = 0x0a;
    internal const int FileIdBothDirectoryRestartInfo = 0x0b;
    internal const int ErrorNoMoreFiles = 18;
    private readonly Win32RetainedFileApi _files = new();

    public nint OpenRoot(string normalizedRoot, uint desiredAccess, uint shareMode, uint flagsAndAttributes) =>
        Win32EvidenceNativeMethods.CreateFileW(
            normalizedRoot, desiredAccess, shareMode, 0,
            Win32EvidenceConstants.OpenExisting, flagsAndAttributes, 0);

    public nint OpenFrame(string normalizedFrame, uint desiredAccess, uint shareMode, uint flagsAndAttributes) =>
        Win32EvidenceNativeMethods.CreateFileW(
            normalizedFrame, desiredAccess, shareMode, 0,
            Win32EvidenceConstants.OpenExisting, flagsAndAttributes, 0);

    public Win32RetainedFileSnapshot ReadSnapshot(nint handle) => _files.ReadSnapshot(handle);

    public IReadOnlyList<Win32PublishedArtifactEntry> EnumerateRoot(nint retainedRootHandle)
    {
        const int bufferSize = 64 * 1024;
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(bufferSize);
        try
        {
            return Win32PublishedDirectoryEnumerator.Collect(pageIndex =>
            {
                var informationClass = pageIndex == 0
                    ? FileIdBothDirectoryRestartInfo
                    : FileIdBothDirectoryInfo;
                if (Win32FileIdentityNativeMethods.GetFileInformationByHandleEx(
                        retainedRootHandle,
                        informationClass,
                        buffer,
                        bufferSize))
                {
                    var page = new byte[bufferSize];
                    System.Runtime.InteropServices.Marshal.Copy(buffer, page, 0, page.Length);
                    return new Win32PublishedDirectoryReadResult(true, 0, page);
                }
                return new Win32PublishedDirectoryReadResult(
                    false,
                    System.Runtime.InteropServices.Marshal.GetLastPInvokeError(),
                    []);
            });
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
        }
    }

    public IReadOnlyList<Win32PublishedStreamEntry> EnumerateFrameStreams(nint retainedFrameHandle)
    {
        const int bufferSize = 4096;
        var memory = System.Runtime.InteropServices.Marshal.AllocHGlobal(bufferSize);
        try
        {
            if (!Win32FileIdentityNativeMethods.GetFileInformationByHandleEx(
                    retainedFrameHandle,
                    FileStreamInfo,
                    memory,
                    bufferSize))
            {
                throw new Win32Exception(
                    System.Runtime.InteropServices.Marshal.GetLastPInvokeError(),
                    "Handle-bound frame stream enumeration failed.");
            }
            var page = new byte[bufferSize];
            System.Runtime.InteropServices.Marshal.Copy(memory, page, 0, page.Length);
            return Win32PublishedStreamPageParser.Parse(page);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(memory);
        }
    }

    public bool ProbeOwnershipMarkerLocked(nint retainedRootHandle, string exactMarkerName)
    {
        if (!string.Equals(exactMarkerName, Win32PublishedFrameValidator.OwnerMarkerName, StringComparison.Ordinal))
        {
            throw new ArgumentException("Ownership-marker probe name was not literal.", nameof(exactMarkerName));
        }
        var root = Win32ExecutableIdentityFactory.NormalizeFinalPath(_files.ReadSnapshot(retainedRootHandle).FinalPath);
        var markerPath = Path.Combine(root, exactMarkerName);
        var probe = Win32EvidenceNativeMethods.CreateFileW(
            markerPath,
            Win32EvidenceConstants.GenericRead,
            Win32EvidenceConstants.FileShareRead |
            Win32EvidenceConstants.FileShareWrite |
            Win32EvidenceConstants.FileShareDelete,
            0,
            Win32EvidenceConstants.OpenExisting,
            Win32EvidenceConstants.FileFlagOpenReparsePoint,
            0);
        if (probe is not 0 and not -1)
        {
            Exception? failure = null;
            try
            {
                if (!_files.CloseKernelHandle(probe))
                {
                    throw new Win32Exception("Closing an unexpectedly opened owner-marker probe failed.");
                }
            }
            catch (Exception exception)
            {
                failure = exception;
            }
            throw failure is null
                ? new InvalidOperationException("Ownership marker was not locked by the child lease.")
                : new AggregateException(
                    new InvalidOperationException("Ownership marker was not locked by the child lease."),
                    failure);
        }
        return System.Runtime.InteropServices.Marshal.GetLastPInvokeError() == 32; // ERROR_SHARING_VIOLATION
    }

    public Stream OpenFrameStream(nint retainedFrameHandle) => _files.OpenReadStream(retainedFrameHandle);

    public bool CloseKernelHandle(nint handle) => _files.CloseKernelHandle(handle);
}

internal readonly record struct Win32PublishedDirectoryReadResult(
    bool Success,
    int Error,
    byte[] Page);

internal static class Win32PublishedDirectoryEnumerator
{
    internal const int MaximumSuccessfulPages = 4;
    internal const int MaximumEntries = 4;
    internal const int MaximumNameBytes = 2048;

    internal static IReadOnlyList<Win32PublishedArtifactEntry> Collect(
        Func<int, Win32PublishedDirectoryReadResult> readPage)
    {
        ArgumentNullException.ThrowIfNull(readPage);
        var entries = new List<Win32PublishedArtifactEntry>();
        var totalNameBytes = 0;
        var successfulPages = 0;
        while (true)
        {
            if (successfulPages >= MaximumSuccessfulPages)
            {
                throw new InvalidDataException("Directory enumeration exceeded its successful-page bound.");
            }
            var result = readPage(successfulPages);
            if (!result.Success)
            {
                if (result.Error == Win32PublishedFrameApi.ErrorNoMoreFiles) return entries;
                throw new Win32Exception(result.Error, "Handle-bound root enumeration failed.");
            }
            successfulPages++;
            if (result.Page is null)
            {
                throw new InvalidDataException("Directory enumeration returned a null successful page.");
            }
            foreach (var entry in Win32PublishedDirectoryPageParser.Parse(result.Page))
            {
                totalNameBytes = checked(totalNameBytes + Encoding.Unicode.GetByteCount(entry.Name));
                if (totalNameBytes > MaximumNameBytes)
                {
                    throw new InvalidDataException("Directory enumeration names exceeded their bound.");
                }
                entries.Add(entry);
                if (entries.Count > MaximumEntries)
                {
                    throw new InvalidDataException("Directory enumeration exceeded its entry bound.");
                }
            }
        }
    }
}

internal static class Win32PublishedStreamPageParser
{
    internal const int StreamNameOffset = 24;
    internal const int MaximumNameBytes = 512;

    internal static IReadOnlyList<Win32PublishedStreamEntry> Parse(ReadOnlySpan<byte> page)
    {
        var entries = new List<Win32PublishedStreamEntry>();
        var offset = 0;
        while (true)
        {
            if (offset < 0 || offset > page.Length - StreamNameOffset || entries.Count >= 8)
            {
                throw new InvalidDataException("Frame stream entry offset was invalid.");
            }
            var next = BinaryPrimitives.ReadUInt32LittleEndian(page.Slice(offset, 4));
            var rawNameBytes = BinaryPrimitives.ReadUInt32LittleEndian(page.Slice(offset + 4, 4));
            if (rawNameBytes > int.MaxValue)
            {
                throw new InvalidDataException("Frame stream name framing overflowed.");
            }
            var nameBytes = (int)rawNameBytes;
            var length = BinaryPrimitives.ReadInt64LittleEndian(page.Slice(offset + 8, 8));
            var recordEnd = (long)offset + StreamNameOffset + nameBytes;
            if ((nameBytes & 1) != 0 || nameBytes <= 0 || nameBytes > MaximumNameBytes || length < 0 ||
                recordEnd > page.Length)
            {
                throw new InvalidDataException("Frame stream name or length framing was invalid.");
            }
            entries.Add(new Win32PublishedStreamEntry(
                Encoding.Unicode.GetString(page.Slice(offset + StreamNameOffset, nameBytes)),
                length));
            if (next == 0) break;
            if (next < StreamNameOffset || (next & 7) != 0 || next > int.MaxValue ||
                recordEnd > (long)offset + next || (long)offset + next >= page.Length)
            {
                throw new InvalidDataException("Frame stream continuation was invalid.");
            }
            offset = checked(offset + (int)next);
        }
        return entries;
    }
}

internal static class Win32PublishedDirectoryPageParser
{
    internal const int FileNameOffset = 104;
    internal const int MaximumNameBytes = 2048;

    internal static IReadOnlyList<Win32PublishedArtifactEntry> Parse(ReadOnlySpan<byte> page)
    {
        var entries = new List<Win32PublishedArtifactEntry>();
        var offset = 0;
        var visited = 0;
        while (true)
        {
            if (offset < 0 || offset > page.Length - FileNameOffset || ++visited > 8)
            {
                throw new InvalidDataException("Directory enumeration entry offset was invalid.");
            }
            var next = BinaryPrimitives.ReadUInt32LittleEndian(page.Slice(offset, 4));
            var attributes = BinaryPrimitives.ReadUInt32LittleEndian(page.Slice(offset + 56, 4));
            var rawNameBytes = BinaryPrimitives.ReadUInt32LittleEndian(page.Slice(offset + 60, 4));
            if (rawNameBytes > int.MaxValue)
            {
                throw new InvalidDataException("Directory enumeration name framing overflowed.");
            }
            var nameBytes = (int)rawNameBytes;
            var recordEnd = (long)offset + FileNameOffset + nameBytes;
            if ((nameBytes & 1) != 0 || nameBytes > MaximumNameBytes ||
                recordEnd > page.Length)
            {
                throw new InvalidDataException("Directory enumeration name framing was invalid.");
            }
            var name = Encoding.Unicode.GetString(page.Slice(offset + FileNameOffset, nameBytes));
            if (name is not "." and not "..")
            {
                entries.Add(new Win32PublishedArtifactEntry(
                    name,
                    (attributes & Win32EvidenceConstants.FileAttributeDirectory) != 0,
                    (attributes & Win32EvidenceConstants.FileAttributeReparsePoint) != 0));
            }
            if (next == 0) break;
            if (next < FileNameOffset || (next & 7) != 0 || next > int.MaxValue ||
                recordEnd > (long)offset + next || (long)offset + next >= page.Length)
            {
                throw new InvalidDataException("Directory enumeration continuation was invalid.");
            }
            offset = checked(offset + (int)next);
        }
        return entries;
    }
}

internal sealed class ManagedStrictPngDecoder : IWin32PngDecoder
{
    private static ReadOnlySpan<byte> Signature => [137, 80, 78, 71, 13, 10, 26, 10];
    private const int BytesPerPixel = 4;
    private const int MaximumAncillaryBytes = 1024 * 1024;

    public Win32PngDecodeResult Decode(ReadOnlyMemory<byte> png, int requiredWidth, int requiredHeight)
    {
        if (requiredWidth <= 0 || requiredHeight <= 0 || png.Length <= Signature.Length ||
            png.Length > Win32PublishedFrameValidator.MaximumPngBytes ||
            !png.Span[..Signature.Length].SequenceEqual(Signature))
        {
            throw new InvalidDataException("PNG signature or bounded size was invalid.");
        }

        var offset = Signature.Length;
        var chunkIndex = 0;
        var seenHeader = false;
        var seenPalette = false;
        var seenIdat = false;
        var idatClosed = false;
        var seenEnd = false;
        var ancillaryBytes = 0;
        uint? gamma = null;
        var seenSrgb = false;
        var seenAncillary = new HashSet<string>(StringComparer.Ordinal);
        using var idat = new MemoryStream();
        while (offset < png.Length)
        {
            if (png.Length - offset < 12) throw new InvalidDataException("PNG chunk framing was truncated.");
            var length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(png.Span.Slice(offset, 4)));
            offset += 4;
            if (length < 0 || length > Win32PublishedFrameValidator.MaximumPngBytes ||
                png.Length - offset < checked(8 + length))
            {
                throw new InvalidDataException("PNG chunk length exceeded its bounded framing.");
            }
            var typeBytes = png.Span.Slice(offset, 4);
            if (!ValidChunkType(typeBytes)) throw new InvalidDataException("PNG chunk type was invalid.");
            var type = Encoding.ASCII.GetString(typeBytes);
            offset += 4;
            var data = png.Span.Slice(offset, length);
            offset += length;
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(png.Span.Slice(offset, 4));
            offset += 4;
            if (ComputeCrc(typeBytes, data) != expectedCrc)
            {
                throw new InvalidDataException("PNG chunk CRC was invalid.");
            }
            if (chunkIndex == 0 && type != "IHDR") throw new InvalidDataException("IHDR was not the first PNG chunk.");
            chunkIndex++;

            switch (type)
            {
                case "IHDR":
                    if (seenHeader || length != 13 || seenIdat) throw new InvalidDataException("PNG IHDR was duplicated or malformed.");
                    var width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data[..4]));
                    var height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4)));
                    if (width != requiredWidth || height != requiredHeight || data[8] != 8 || data[9] != 6 ||
                        data[10] != 0 || data[11] != 0 || data[12] != 0)
                    {
                        throw new InvalidDataException("PNG IHDR did not declare exact noninterlaced RGBA8.");
                    }
                    seenHeader = true;
                    break;
                case "PLTE":
                    if (!seenHeader || seenPalette || seenIdat || seenAncillary.Contains("bKGD") ||
                        length is < 3 or > 768 || length % 3 != 0)
                    {
                        throw new InvalidDataException("PNG PLTE placement or length was invalid.");
                    }
                    seenPalette = true;
                    break;
                case "IDAT":
                    if (!seenHeader || idatClosed || seenEnd) throw new InvalidDataException("PNG IDAT chunks were not consecutive.");
                    seenIdat = true;
                    if (idat.Length + length > Win32PublishedFrameValidator.MaximumPngBytes)
                    {
                        throw new InvalidDataException("PNG IDAT payload exceeded its bound.");
                    }
                    idat.Write(data);
                    break;
                case "IEND":
                    if (!seenIdat || seenEnd || length != 0 || offset != png.Length)
                    {
                        throw new InvalidDataException("PNG IEND was missing, malformed, or not terminal.");
                    }
                    seenEnd = true;
                    idatClosed = true;
                    break;
                default:
                    if (!seenHeader || seenEnd || IsCritical(typeBytes))
                    {
                        throw new InvalidDataException("PNG contained an unknown critical or illegally placed chunk.");
                    }
                    if (seenIdat) idatClosed = true;
                    ancillaryBytes = checked(ancillaryBytes + length);
                    if (ancillaryBytes > MaximumAncillaryBytes ||
                        !ValidateAncillary(type, data, seenPalette, seenIdat) ||
                        !seenAncillary.Add(type))
                    {
                        throw new InvalidDataException("PNG ancillary chunk was unsupported, duplicated, or illegally placed.");
                    }
                    if (type == "gAMA") gamma = BinaryPrimitives.ReadUInt32BigEndian(data);
                    if (type == "sRGB") seenSrgb = true;
                    if (seenSrgb && gamma is not null and not 45455)
                    {
                        throw new InvalidDataException("PNG sRGB and gAMA chunks declared inconsistent transfer characteristics.");
                    }
                    break;
            }
            if (seenIdat && type != "IDAT" && type != "IEND") idatClosed = true;
        }
        if (!seenHeader || !seenIdat || !seenEnd || idat.Length == 0)
        {
            throw new InvalidDataException("PNG required chunk sequence was incomplete.");
        }

        var filteredLength = checked(requiredHeight * checked(1 + requiredWidth * BytesPerPixel));
        var filtered = InflateZlibExactly(idat.ToArray(), filteredLength);
        var decoded = Unfilter(filtered, requiredWidth, requiredHeight);
        return new Win32PngDecodeResult(
            requiredWidth,
            requiredHeight,
            true,
            decoded.Length,
            Convert.ToHexString(SHA256.HashData(decoded)).ToLowerInvariant());
    }

    private static byte[] InflateZlibExactly(byte[] zlib, int expectedLength)
    {
        if (zlib.Length < 7) throw new InvalidDataException("PNG zlib stream was truncated.");
        var cmf = zlib[0];
        var flg = zlib[1];
        if ((cmf & 0x0f) != 8 || (cmf >> 4) > 7 || ((cmf << 8) + flg) % 31 != 0 || (flg & 0x20) != 0)
        {
            throw new InvalidDataException("PNG zlib header was invalid or requested a dictionary.");
        }
        var deflateLength = zlib.Length - 6;
        var source = new OneByteReadStream(zlib.AsMemory(2, deflateLength));
        var output = new byte[expectedLength];
        using (var inflater = new DeflateStream(source, CompressionMode.Decompress, leaveOpen: true))
        {
            var consumed = 0;
            while (consumed < output.Length)
            {
                var read = inflater.Read(output, consumed, output.Length - consumed);
                if (read == 0) throw new InvalidDataException("PNG zlib output was truncated.");
                consumed = checked(consumed + read);
            }
            if (inflater.ReadByte() != -1) throw new InvalidDataException("PNG zlib output exceeded exact scanlines.");
        }
        if (source.BytesRead != deflateLength)
        {
            throw new InvalidDataException("PNG zlib payload contained bytes after the end of deflate.");
        }
        var expectedAdler = BinaryPrimitives.ReadUInt32BigEndian(zlib.AsSpan(zlib.Length - 4));
        if (Adler32(output) != expectedAdler) throw new InvalidDataException("PNG zlib Adler-32 was invalid.");
        return output;
    }

    private static byte[] Unfilter(byte[] filtered, int width, int height)
    {
        var stride = checked(width * BytesPerPixel);
        var decoded = new byte[checked(stride * height)];
        var inputOffset = 0;
        for (var row = 0; row < height; row++)
        {
            var filter = filtered[inputOffset++];
            if (filter > 4) throw new InvalidDataException("PNG scanline used an invalid filter.");
            var rowOffset = checked(row * stride);
            var previousOffset = rowOffset - stride;
            for (var column = 0; column < stride; column++)
            {
                var raw = filtered[inputOffset++];
                var left = column >= BytesPerPixel ? decoded[rowOffset + column - BytesPerPixel] : 0;
                var up = row > 0 ? decoded[previousOffset + column] : 0;
                var upLeft = row > 0 && column >= BytesPerPixel
                    ? decoded[previousOffset + column - BytesPerPixel]
                    : 0;
                var predictor = filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new InvalidDataException("PNG filter was outside the supported range."),
                };
                decoded[rowOffset + column] = unchecked((byte)(raw + predictor));
            }
        }
        if (inputOffset != filtered.Length) throw new InvalidDataException("PNG scanline payload had trailing data.");
        return decoded;
    }

    private static int Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var leftDistance = Math.Abs(estimate - left);
        var upDistance = Math.Abs(estimate - up);
        var upLeftDistance = Math.Abs(estimate - upLeft);
        return leftDistance <= upDistance && leftDistance <= upLeftDistance
            ? left
            : upDistance <= upLeftDistance ? up : upLeft;
    }

    private static bool ValidateAncillary(
        string type,
        ReadOnlySpan<byte> data,
        bool seenPalette,
        bool seenIdat) => type switch
    {
        // Narrow cHRM to the canonical sRGB chromaticities rather than accepting
        // unvalidated coordinate sets at this security boundary.
        "cHRM" => !seenPalette && !seenIdat && HasStandardSrgbChromaticities(data),
        "gAMA" => !seenPalette && !seenIdat && data.Length == 4 &&
                  BinaryPrimitives.ReadUInt32BigEndian(data) != 0,
        "sBIT" => !seenPalette && !seenIdat && data.Length == 4 &&
                  data.ToArray().All(value => value is >= 1 and <= 8),
        "sRGB" => !seenPalette && !seenIdat && data.Length == 1 && data[0] <= 3,
        "bKGD" => !seenIdat && HasValidRgba8Background(data),
        "pHYs" => !seenIdat && data.Length == 9 && data[8] <= 1,
        _ => false,
    };

    private static bool HasStandardSrgbChromaticities(ReadOnlySpan<byte> data)
    {
        ReadOnlySpan<uint> expected = [31270, 32900, 64000, 33000, 30000, 60000, 15000, 6000];
        if (data.Length != expected.Length * sizeof(uint)) return false;
        for (var index = 0; index < expected.Length; index++)
        {
            if (BinaryPrimitives.ReadUInt32BigEndian(data.Slice(index * sizeof(uint), sizeof(uint))) !=
                expected[index])
            {
                return false;
            }
        }
        return true;
    }

    private static bool HasValidRgba8Background(ReadOnlySpan<byte> data) =>
        data.Length == 6 &&
        BinaryPrimitives.ReadUInt16BigEndian(data[..2]) <= byte.MaxValue &&
        BinaryPrimitives.ReadUInt16BigEndian(data.Slice(2, 2)) <= byte.MaxValue &&
        BinaryPrimitives.ReadUInt16BigEndian(data.Slice(4, 2)) <= byte.MaxValue;

    private static bool ValidChunkType(ReadOnlySpan<byte> type) =>
        type.Length == 4 && type.ToArray().All(value => value is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z') &&
        (type[2] & 0x20) == 0;

    private static bool IsCritical(ReadOnlySpan<byte> type) => (type[0] & 0x20) == 0;

    private static uint ComputeCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xffffffffU;
        foreach (var value in type) crc = UpdateCrc(crc, value);
        foreach (var value in data) crc = UpdateCrc(crc, value);
        return crc ^ 0xffffffffU;
    }

    private static uint UpdateCrc(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++)
        {
            crc = (crc & 1) != 0 ? 0xedb88320U ^ (crc >> 1) : crc >> 1;
        }
        return crc;
    }

    private static uint Adler32(ReadOnlySpan<byte> data)
    {
        const uint modulus = 65521;
        uint a = 1;
        uint b = 0;
        foreach (var value in data)
        {
            a = (a + value) % modulus;
            b = (b + a) % modulus;
        }
        return (b << 16) | a;
    }

    private sealed class OneByteReadStream(ReadOnlyMemory<byte> bytes) : Stream
    {
        private int _position;
        internal int BytesRead => _position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => bytes.Length;
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            ArgumentNullException.ThrowIfNull(buffer);
            if (count == 0 || _position >= bytes.Length) return 0;
            buffer[offset] = bytes.Span[_position++];
            return 1;
        }
        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0 || _position >= bytes.Length) return 0;
            buffer[0] = bytes.Span[_position++];
            return 1;
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
