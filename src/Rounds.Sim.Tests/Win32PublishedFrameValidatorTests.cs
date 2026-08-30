using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Rounds.EvidenceLauncher;
using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class Win32PublishedFrameValidatorTests
{
    [Fact]
    public void Exact_frame_is_validated_through_retained_handles_and_lease_closes_in_reverse_once()
    {
        var png = BuildPng(1920, 1080, new byte[1920 * 1080 * 4]);
        var rig = new Rig(png, new ManagedStrictPngDecoder());

        var lease = rig.Validate();

        Assert.Equal(ValidHash(png), lease.Validation.PngSha256);
        Assert.True(lease.Validation.RootIdentityBound);
        Assert.True(lease.Validation.FrameIdentityBound);
        Assert.True(lease.Validation.RootLeaseObserved);
        Assert.True(lease.Validation.FrameLeaseObserved);
        Assert.True(lease.Validation.ContainsOnlyExpectedFrame);
        Assert.DoesNotContain("close:202", rig.Api.Events);
        Assert.Equal(
            (Win32PublishedFrameValidator.RootDesiredAccess,
             Win32PublishedFrameValidator.RetainedShareMode,
             Win32PublishedFrameValidator.RootFlags),
            rig.Api.RootOpenContract);
        Assert.Equal(
            (Win32EvidenceConstants.GenericRead,
             Win32PublishedFrameValidator.RetainedShareMode,
             Win32PublishedFrameValidator.FrameFlags),
            rig.Api.FrameOpenContract);

        lease.Dispose();
        lease.Dispose();

        AssertOrdered(rig.Api.Events, "stream-dispose", "close:202", "close:101");
        Assert.Equal(1, rig.Api.CloseCount(202));
        Assert.Equal(1, rig.Api.CloseCount(101));
    }

    [Theory]
    [InlineData("root-open")]
    [InlineData("frame-open")]
    [InlineData("marker-unlocked")]
    [InlineData("enumerate")]
    [InlineData("stream")]
    [InlineData("streams")]
    [InlineData("decoder")]
    public void Boundary_failure_refuses_and_closes_every_acquired_handle_once(string failure)
    {
        var png = BuildPng(1, 1, new byte[4]);
        var rig = new Rig(png, new FakeDecoder());
        rig.Api.Failure = failure;
        if (failure == "decoder") rig.Decoder.Throw = true;

        Assert.ThrowsAny<Exception>(() => rig.Validate());

        Assert.Equal(failure == "root-open" ? 0 : 1, rig.Api.CloseCount(101));
        Assert.Equal(failure is "root-open" or "frame-open" ? 0 : 1, rig.Api.CloseCount(202));
    }

    [Theory]
    [InlineData("root-case")]
    [InlineData("root-kind")]
    [InlineData("root-reparse")]
    [InlineData("root-delete")]
    [InlineData("root-zero-id")]
    [InlineData("frame-case")]
    [InlineData("frame-kind")]
    [InlineData("frame-reparse")]
    [InlineData("frame-delete")]
    [InlineData("frame-link")]
    [InlineData("frame-zero-id")]
    [InlineData("frame-volume")]
    [InlineData("frame-empty")]
    [InlineData("frame-oversize")]
    public void Snapshot_shape_or_identity_refuses_before_ack(string mutation)
    {
        var png = BuildPng(1, 1, new byte[4]);
        var rig = new Rig(png, new FakeDecoder());
        rig.Api.MutateInitialSnapshot(mutation);

        Assert.ThrowsAny<Exception>(() => rig.Validate());
        Assert.Equal(1, rig.Api.CloseCount(101));
    }

    [Theory]
    [InlineData("frame-length")]
    [InlineData("frame-change")]
    [InlineData("frame-link")]
    [InlineData("frame-delete")]
    [InlineData("frame-id")]
    [InlineData("root-change")]
    [InlineData("root-id")]
    public void Post_validation_snapshot_drift_refuses(string mutation)
    {
        var png = BuildPng(1, 1, new byte[4]);
        var rig = new Rig(png, new FakeDecoder());
        rig.Api.MutatePostSnapshot(mutation);

        Assert.ThrowsAny<Exception>(() => rig.Validate());
        AssertOrdered(rig.Api.Events, "stream-dispose", "close:202", "close:101");
    }

    [Theory]
    [InlineData("missing-marker")]
    [InlineData("missing-frame")]
    [InlineData("extra")]
    [InlineData("duplicate")]
    [InlineData("case")]
    [InlineData("directory")]
    [InlineData("reparse")]
    [InlineData("partial")]
    [InlineData("changed")]
    public void Root_contents_must_be_exact_stable_frame_plus_locked_owner_marker(string mutation)
    {
        var png = BuildPng(1, 1, new byte[4]);
        var rig = new Rig(png, new FakeDecoder());
        rig.Api.MutateEntries(mutation);

        Assert.ThrowsAny<Exception>(() => rig.Validate());
    }

    [Theory]
    [InlineData("length-mismatch")]
    [InlineData("short")]
    [InlineData("over")]
    [InlineData("read-fault")]
    [InlineData("dispose-fault")]
    public void Retained_stream_requires_exact_snapshot_length_and_close(string failure)
    {
        var png = BuildPng(1, 1, new byte[4]);
        var rig = new Rig(png, new FakeDecoder());
        rig.Api.StreamFailure = failure;

        Assert.ThrowsAny<Exception>(() => rig.Validate());

        Assert.Equal(1, rig.Api.StreamDisposeCount);
        AssertOrdered(rig.Api.Events, "stream-dispose", "close:202", "close:101");
    }

    [Fact]
    public void Hash_mismatch_refuses_before_decode()
    {
        var png = BuildPng(1, 1, new byte[4]);
        var decoder = new FakeDecoder();
        var rig = new Rig(png, decoder) { MarkerHash = new string('0', 64) };

        Assert.Throws<InvalidOperationException>(() => rig.Validate());

        Assert.Equal(0, decoder.Calls);
    }

    [Fact]
    public void Alternate_data_stream_is_rejected_before_read_or_decode()
    {
        var png = BuildPng(1, 1, new byte[4]);
        var decoder = new FakeDecoder();
        var rig = new Rig(png, decoder);
        rig.Api.AddAlternateStream();

        Assert.Throws<InvalidOperationException>(() => rig.Validate());

        Assert.DoesNotContain("stream-open", rig.Api.Events);
        Assert.Equal(0, decoder.Calls);
    }

    [Fact]
    public void Primary_validation_and_both_handle_close_failures_are_aggregated_in_reverse_order()
    {
        var png = BuildPng(1, 1, new byte[4]);
        var rig = new Rig(png, new FakeDecoder { Throw = true });
        rig.Api.CloseFailures.UnionWith([101, 202]);

        var failure = Assert.Throws<AggregateException>(() => rig.Validate());

        Assert.Equal(3, failure.Flatten().InnerExceptions.Count);
        AssertOrdered(rig.Api.Events, "stream-dispose", "close:202", "close:101");
    }

    [Fact]
    public void Successful_validation_lease_aggregates_both_close_failures_without_double_close()
    {
        var png = BuildPng(1, 1, new byte[4]);
        var rig = new Rig(png, new FakeDecoder());
        var lease = rig.Validate();
        rig.Api.CloseFailures.UnionWith([101, 202]);

        var failure = Assert.Throws<AggregateException>(lease.Dispose);
        lease.Dispose();

        Assert.Equal(2, failure.Flatten().InnerExceptions.Count);
        Assert.Equal(1, rig.Api.CloseCount(202));
        Assert.Equal(1, rig.Api.CloseCount(101));
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("crc")]
    [InlineData("ihdr-not-first")]
    [InlineData("ihdr-duplicate")]
    [InlineData("ihdr-width")]
    [InlineData("ihdr-depth")]
    [InlineData("unknown-critical")]
    [InlineData("idat-separated")]
    [InlineData("iend-trailing")]
    [InlineData("iend-missing")]
    [InlineData("chunk-truncated")]
    [InlineData("zlib-header")]
    [InlineData("adler")]
    [InlineData("filter")]
    [InlineData("decompressed-short")]
    [InlineData("decompressed-long")]
    [InlineData("deflate-trailing")]
    public void Strict_png_decoder_rejects_structural_compression_and_filter_failures(string mutation)
    {
        var png = MutatedTinyPng(mutation);

        Assert.ThrowsAny<Exception>(() => new ManagedStrictPngDecoder().Decode(png, 2, 2));
    }

    [Fact]
    public void Strict_png_decoder_accepts_all_filters_and_unfilters_exact_rgba_order()
    {
        const int width = 3;
        const int height = 5;
        var rgba = Enumerable.Range(0, width * height * 4)
            .Select(index => unchecked((byte)(index * 17 + 3)))
            .ToArray();
        var png = BuildPng(width, height, rgba, [0, 1, 2, 3, 4]);

        var decoded = new ManagedStrictPngDecoder().Decode(png, width, height);

        Assert.True(decoded.Rgba8);
        Assert.Equal(rgba.Length, decoded.DecodedBytes);
        Assert.Equal(ValidHash(rgba), decoded.RgbaSha256);
    }

    [Fact]
    public void Strict_png_decoder_accepts_crc_valid_legal_pre_idat_ancillary_chunk()
    {
        var chunks = BuildChunks(1, 1, new byte[4]);
        chunks.Insert(1, new Chunk("gAMA", [0, 0, 177, 143]));
        var png = EncodeChunks(chunks);

        var decoded = new ManagedStrictPngDecoder().Decode(png, 1, 1);

        Assert.Equal(4, decoded.DecodedBytes);
    }

    [Fact]
    public void Strict_png_decoder_accepts_supported_color_chunks_in_exact_rgba8_order()
    {
        var chunks = BuildChunks(1, 1, new byte[4]);
        chunks.Insert(1, new Chunk("cHRM", StandardSrgbChromaticities()));
        chunks.Insert(2, new Chunk("gAMA", [0, 0, 177, 143]));
        chunks.Insert(3, new Chunk("sBIT", [8, 8, 8, 8]));
        chunks.Insert(4, new Chunk("sRGB", [0]));
        chunks.Insert(5, new Chunk("PLTE", [0, 0, 0]));
        chunks.Insert(6, new Chunk("bKGD", [0, 1, 0, 2, 0, 3]));

        var decoded = new ManagedStrictPngDecoder().Decode(EncodeChunks(chunks), 1, 1);

        Assert.Equal(4, decoded.DecodedBytes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Strict_png_decoder_accepts_canonical_gamma_with_srgb_in_either_order(bool gammaFirst)
    {
        var chunks = BuildChunks(1, 1, new byte[4]);
        var gamma = new Chunk("gAMA", [0, 0, 177, 143]);
        var srgb = new Chunk("sRGB", [0]);
        chunks.Insert(1, gammaFirst ? gamma : srgb);
        chunks.Insert(2, gammaFirst ? srgb : gamma);

        var decoded = new ManagedStrictPngDecoder().Decode(EncodeChunks(chunks), 1, 1);

        Assert.Equal(4, decoded.DecodedBytes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Strict_png_decoder_rejects_noncanonical_gamma_with_srgb_in_either_order(bool gammaFirst)
    {
        var chunks = BuildChunks(1, 1, new byte[4]);
        var gamma = new Chunk("gAMA", [0, 1, 134, 160]);
        var srgb = new Chunk("sRGB", [0]);
        chunks.Insert(1, gammaFirst ? gamma : srgb);
        chunks.Insert(2, gammaFirst ? srgb : gamma);

        Assert.Throws<InvalidDataException>(() =>
            new ManagedStrictPngDecoder().Decode(EncodeChunks(chunks), 1, 1));
    }

    [Theory]
    [InlineData("cHRM")]
    [InlineData("gAMA")]
    [InlineData("sBIT")]
    [InlineData("sRGB")]
    public void Strict_png_decoder_rejects_color_definition_chunks_after_palette(string type)
    {
        var chunks = BuildChunks(1, 1, new byte[4]);
        chunks.Insert(1, new Chunk("PLTE", [0, 0, 0]));
        chunks.Insert(2, new Chunk(type, SupportedAncillaryData(type)));

        Assert.Throws<InvalidDataException>(() =>
            new ManagedStrictPngDecoder().Decode(EncodeChunks(chunks), 1, 1));
    }

    [Fact]
    public void Strict_png_decoder_rejects_background_before_a_later_palette()
    {
        var chunks = BuildChunks(1, 1, new byte[4]);
        chunks.Insert(1, new Chunk("bKGD", [0, 1, 0, 2, 0, 3]));
        chunks.Insert(2, new Chunk("PLTE", [0, 0, 0]));

        Assert.Throws<InvalidDataException>(() =>
            new ManagedStrictPngDecoder().Decode(EncodeChunks(chunks), 1, 1));
    }

    [Fact]
    public void Strict_png_decoder_rejects_background_samples_wider_than_rgba8()
    {
        var chunks = BuildChunks(1, 1, new byte[4]);
        chunks.Insert(1, new Chunk("bKGD", [1, 0, 0, 2, 0, 3]));

        Assert.Throws<InvalidDataException>(() =>
            new ManagedStrictPngDecoder().Decode(EncodeChunks(chunks), 1, 1));
    }

    [Fact]
    public void Handle_bound_directory_page_parser_uses_aligned_offsets_and_ignores_only_dot_entries()
    {
        var page = DirectoryPage(
            (".", 0x10U),
            (Win32PublishedFrameValidator.FrameName, 0x80U),
            (Win32PublishedFrameValidator.OwnerMarkerName, 0x80U));

        var entries = Win32PublishedDirectoryPageParser.Parse(page);

        Assert.Equal(
            [Win32PublishedFrameValidator.FrameName, Win32PublishedFrameValidator.OwnerMarkerName],
            entries.Select(entry => entry.Name));
        Assert.All(entries, entry =>
        {
            Assert.False(entry.Directory);
            Assert.False(entry.ReparsePoint);
        });
    }

    [Fact]
    public void Handle_bound_directory_enumerator_bounds_successful_dot_only_pages()
    {
        var calls = new List<int>();

        Assert.Throws<InvalidDataException>(() => Win32PublishedDirectoryEnumerator.Collect(index =>
        {
            calls.Add(index);
            return new Win32PublishedDirectoryReadResult(true, 0, DirectoryPage((".", 0x10U)));
        }));

        Assert.Equal(Enumerable.Range(0, Win32PublishedDirectoryEnumerator.MaximumSuccessfulPages), calls);
    }

    [Theory]
    [InlineData("unaligned-next")]
    [InlineData("odd-name")]
    [InlineData("oversized-name")]
    [InlineData("overlapping-next")]
    [InlineData("name-overflow")]
    [InlineData("next-overflow")]
    public void Handle_bound_directory_page_parser_rejects_malformed_native_framing(string mutation)
    {
        var page = DirectoryPage((Win32PublishedFrameValidator.FrameName, 0x80U), ("next", 0x80U));
        if (mutation == "unaligned-next") BinaryPrimitives.WriteUInt32LittleEndian(page, 105);
        if (mutation == "odd-name") BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(60), 3);
        if (mutation == "overlapping-next") BinaryPrimitives.WriteUInt32LittleEndian(page, 104);
        if (mutation == "name-overflow") BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(60), uint.MaxValue);
        if (mutation == "next-overflow") BinaryPrimitives.WriteUInt32LittleEndian(page, 0x80000000);
        if (mutation == "oversized-name")
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                page.AsSpan(60),
                Win32PublishedDirectoryPageParser.MaximumNameBytes + 2);
        }

        Assert.Throws<InvalidDataException>(() => Win32PublishedDirectoryPageParser.Parse(page));
    }

    [Fact]
    public void Handle_bound_stream_page_parser_exposes_default_and_named_streams()
    {
        var page = StreamPage(("::$DATA", 12L), (":hidden:$DATA", 4L));

        var streams = Win32PublishedStreamPageParser.Parse(page);

        Assert.Equal(["::$DATA", ":hidden:$DATA"], streams.Select(stream => stream.Name));
        Assert.Equal([12L, 4L], streams.Select(stream => stream.Length));
    }

    [Theory]
    [InlineData("overlapping-next")]
    [InlineData("name-overflow")]
    [InlineData("next-overflow")]
    public void Handle_bound_stream_page_parser_rejects_overlap_and_overflow(string mutation)
    {
        var page = StreamPage(("::$DATA", 12L), (":next:$DATA", 4L));
        if (mutation == "overlapping-next") BinaryPrimitives.WriteUInt32LittleEndian(page, 24);
        if (mutation == "name-overflow") BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(4), uint.MaxValue);
        if (mutation == "next-overflow") BinaryPrimitives.WriteUInt32LittleEndian(page, 0x80000000);

        Assert.Throws<InvalidDataException>(() => Win32PublishedStreamPageParser.Parse(page));
    }

    private sealed class Rig
    {
        internal const string Root = @"C:\RoundsEvidence\published";
        internal FakeApi Api { get; }
        internal FakeDecoder Decoder { get; }
        internal string MarkerHash { get; set; }
        private readonly IWin32PngDecoder _decoder;

        internal Rig(byte[] png, IWin32PngDecoder decoder)
        {
            Api = new FakeApi(Root, png);
            _decoder = decoder;
            Decoder = decoder as FakeDecoder ?? new FakeDecoder();
            MarkerHash = ValidHash(png);
        }

        internal IEvidencePublishedFrameValidationLease Validate() =>
            new Win32PublishedFrameValidator(Api, _decoder).ValidatePublishedFrame(
                Plan(Root),
                new DebugBaseProjectileEvidenceAttestation(
                    0x6a25f798f6582a29,
                    0,
                    0,
                    "RoundsEvidence-0123456789abcdef0123456789abcdef",
                    new DebugEvidenceCaptureAttestation(3, 684, -900, 1280, 720, 1920, 1080),
                    new string('a', 64),
                    new string('b', 32),
                    MarkerHash,
                    Win32PublishedFrameValidator.FrameName));
    }

    private sealed class FakeApi(string root, byte[] bytes) : IWin32PublishedFrameApi
    {
        private const nint RootHandle = 101;
        private const nint FrameHandle = 202;
        private Win32RetainedFileSnapshot _rootBefore = RootSnapshot(root);
        private Win32RetainedFileSnapshot _rootAfter = RootSnapshot(root);
        private Win32RetainedFileSnapshot _frameBefore = FrameSnapshot(root, bytes.Length);
        private Win32RetainedFileSnapshot _frameAfter = FrameSnapshot(root, bytes.Length);
        private IReadOnlyList<Win32PublishedArtifactEntry> _entriesBefore = ValidEntries();
        private IReadOnlyList<Win32PublishedArtifactEntry> _entriesAfter = ValidEntries();
        private int _rootReads;
        private int _frameReads;
        private int _enumerations;
        private IReadOnlyList<Win32PublishedStreamEntry> _streams =
            [new("::$DATA", bytes.Length)];
        private readonly Dictionary<nint, int> _closeCounts = [];

        internal List<string> Events { get; } = [];
        internal HashSet<nint> CloseFailures { get; } = [];
        internal string? Failure { get; set; }
        internal string? StreamFailure { get; set; }
        internal int StreamDisposeCount { get; private set; }
        internal (uint Access, uint Share, uint Flags) RootOpenContract { get; private set; }
        internal (uint Access, uint Share, uint Flags) FrameOpenContract { get; private set; }

        public nint OpenRoot(string normalizedRoot, uint desiredAccess, uint shareMode, uint flagsAndAttributes)
        {
            Events.Add("open-root");
            RootOpenContract = (desiredAccess, shareMode, flagsAndAttributes);
            Assert.Equal(root, normalizedRoot);
            return Failure == "root-open" ? 0 : RootHandle;
        }

        public nint OpenFrame(string normalizedFrame, uint desiredAccess, uint shareMode, uint flagsAndAttributes)
        {
            Events.Add("open-frame");
            FrameOpenContract = (desiredAccess, shareMode, flagsAndAttributes);
            Assert.Equal(Path.Combine(root, Win32PublishedFrameValidator.FrameName), normalizedFrame);
            return Failure == "frame-open" ? 0 : FrameHandle;
        }

        public Win32RetainedFileSnapshot ReadSnapshot(nint handle)
        {
            Events.Add($"snapshot:{handle}");
            if (handle == RootHandle) return _rootReads++ == 0 ? _rootBefore : _rootAfter;
            if (handle == FrameHandle) return _frameReads++ == 0 ? _frameBefore : _frameAfter;
            throw new InvalidOperationException("unexpected handle");
        }

        public IReadOnlyList<Win32PublishedArtifactEntry> EnumerateRoot(nint retainedRootHandle)
        {
            Events.Add("enumerate");
            Assert.Equal(RootHandle, retainedRootHandle);
            if (Failure == "enumerate") throw new IOException("injected enumeration failure");
            return _enumerations++ == 0 ? _entriesBefore : _entriesAfter;
        }

        public bool ProbeOwnershipMarkerLocked(nint retainedRootHandle, string exactMarkerName)
        {
            Events.Add("probe-marker-lock");
            Assert.Equal(RootHandle, retainedRootHandle);
            Assert.Equal(Win32PublishedFrameValidator.OwnerMarkerName, exactMarkerName);
            return Failure != "marker-unlocked";
        }

        public IReadOnlyList<Win32PublishedStreamEntry> EnumerateFrameStreams(nint retainedFrameHandle)
        {
            Events.Add("enumerate-streams");
            Assert.Equal(FrameHandle, retainedFrameHandle);
            if (Failure == "streams") throw new IOException("injected stream enumeration failure");
            return _streams;
        }

        public Stream OpenFrameStream(nint retainedFrameHandle)
        {
            Events.Add("stream-open");
            Assert.Equal(FrameHandle, retainedFrameHandle);
            if (Failure == "stream") throw new IOException("injected stream open failure");
            var reportedLength = StreamFailure == "length-mismatch" ? bytes.Length + 1 : bytes.Length;
            var payload = StreamFailure == "short" ? bytes[..^1]
                : StreamFailure == "over" ? [.. bytes, 42]
                : bytes;
            return new FakeStream(
                payload,
                reportedLength,
                StreamFailure == "read-fault",
                StreamFailure == "dispose-fault",
                () =>
                {
                    StreamDisposeCount++;
                    Events.Add("stream-dispose");
                });
        }

        public bool CloseKernelHandle(nint handle)
        {
            Events.Add($"close:{handle}");
            _closeCounts[handle] = _closeCounts.GetValueOrDefault(handle) + 1;
            return !CloseFailures.Contains(handle);
        }

        internal int CloseCount(nint handle) => _closeCounts.GetValueOrDefault(handle);

        internal void AddAlternateStream() =>
            _streams = [new("::$DATA", bytes.Length), new(":hidden:$DATA", 1)];

        internal void MutateInitialSnapshot(string mutation)
        {
            if (mutation.StartsWith("root", StringComparison.Ordinal))
            {
                _rootBefore = mutation switch
                {
                    "root-case" => _rootBefore with { FinalPath = root.ToUpperInvariant() },
                    "root-kind" => _rootBefore with { Directory = false },
                    "root-reparse" => _rootBefore with { ReparseTag = 1, Attributes = 0x410 },
                    "root-delete" => _rootBefore with { DeletePending = true },
                    "root-zero-id" => _rootBefore with { FileId = new string('0', 32) },
                    _ => _rootBefore,
                };
            }
            else
            {
                _frameBefore = mutation switch
                {
                    "frame-case" => _frameBefore with { FinalPath = _frameBefore.FinalPath.ToUpperInvariant() },
                    "frame-kind" => _frameBefore with { Directory = true, Attributes = 0x10 },
                    "frame-reparse" => _frameBefore with { ReparseTag = 1, Attributes = 0x400 },
                    "frame-delete" => _frameBefore with { DeletePending = true },
                    "frame-link" => _frameBefore with { LinkCount = 2 },
                    "frame-zero-id" => _frameBefore with { FileId = new string('0', 32) },
                    "frame-volume" => _frameBefore with { VolumeSerialNumber = 8 },
                    "frame-empty" => _frameBefore with { Length = 0 },
                    "frame-oversize" => _frameBefore with { Length = Win32PublishedFrameValidator.MaximumPngBytes + 1 },
                    _ => _frameBefore,
                };
            }
        }

        internal void MutatePostSnapshot(string mutation)
        {
            if (mutation.StartsWith("root", StringComparison.Ordinal))
            {
                _rootAfter = mutation == "root-id"
                    ? _rootAfter with { FileId = new string('9', 32) }
                    : _rootAfter with { ChangeTime = 99 };
            }
            else
            {
                _frameAfter = mutation switch
                {
                    "frame-length" => _frameAfter with { Length = _frameAfter.Length + 1 },
                    "frame-change" => _frameAfter with { ChangeTime = 99 },
                    "frame-link" => _frameAfter with { LinkCount = 2 },
                    "frame-delete" => _frameAfter with { DeletePending = true },
                    "frame-id" => _frameAfter with { FileId = new string('8', 32) },
                    _ => _frameAfter,
                };
            }
        }

        internal void MutateEntries(string mutation)
        {
            var frame = new Win32PublishedArtifactEntry(Win32PublishedFrameValidator.FrameName, false, false);
            var marker = new Win32PublishedArtifactEntry(Win32PublishedFrameValidator.OwnerMarkerName, false, false);
            _entriesBefore = mutation switch
            {
                "missing-marker" => [frame],
                "missing-frame" => [marker],
                "extra" => [frame, marker, new("extra.tmp", false, false)],
                "duplicate" => [frame, frame],
                "case" => [new("Frame-0000.png", false, false), marker],
                "directory" => [frame with { Directory = true }, marker],
                "reparse" => [frame with { ReparsePoint = true }, marker],
                "partial" => [new("frame-0000.png.partial", false, false), marker],
                "changed" => [frame, marker],
                _ => [frame, marker],
            };
            _entriesAfter = mutation == "changed" ? [frame, new("extra.tmp", false, false)] : _entriesBefore;
        }

        private static IReadOnlyList<Win32PublishedArtifactEntry> ValidEntries() =>
            [new(Win32PublishedFrameValidator.FrameName, false, false),
             new(Win32PublishedFrameValidator.OwnerMarkerName, false, false)];
    }

    private sealed class FakeDecoder : IWin32PngDecoder
    {
        internal bool Throw { get; set; }
        internal int Calls { get; private set; }

        public Win32PngDecodeResult Decode(ReadOnlyMemory<byte> png, int requiredWidth, int requiredHeight)
        {
            Calls++;
            if (Throw) throw new InvalidDataException("injected decoder failure");
            return new Win32PngDecodeResult(
                requiredWidth,
                requiredHeight,
                true,
                checked(requiredWidth * requiredHeight * 4),
                new string('d', 64));
        }
    }

    private sealed class FakeStream(
        byte[] bytes,
        long reportedLength,
        bool throwOnRead,
        bool throwOnDispose,
        Action disposed) : Stream
    {
        private int _position;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => reportedLength;
        public override long Position { get => _position; set => _position = checked((int)value); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (throwOnRead) throw new IOException("injected read failure");
            var available = System.Math.Min(count, bytes.Length - _position);
            if (available <= 0) return 0;
            Array.Copy(bytes, _position, buffer, offset, available);
            _position += available;
            return available;
        }
        public override int ReadByte() => _position < bytes.Length ? bytes[_position++] : -1;
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                disposed();
                if (throwOnDispose) throw new IOException("injected stream dispose failure");
            }
            base.Dispose(disposing);
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static Win32RetainedFileSnapshot RootSnapshot(string root) => new(
        root, 7, new string('1', 32), 0, 11, 0x10, 0, 1, false, true);

    private static Win32RetainedFileSnapshot FrameSnapshot(string root, long length) => new(
        Path.Combine(root, Win32PublishedFrameValidator.FrameName),
        7, new string('2', 32), length, 12, 0x80, 0, 1, false, false);

    private static BaseProjectileEvidenceLaunchPlan Plan(string outputRoot) => new(
        new string('a', 40), @"C:\repo", @"C:\godot.exe",
        "RoundsEvidence-0123456789abcdef0123456789abcdef", 3,
        new EvidencePixelBounds(364, -1080, 1920, 1080),
        new EvidencePixelBounds(684, -900, 1280, 720),
        outputRoot, [], new Dictionary<string, string>(),
        new BaseProjectileEvidenceJobLimits(3, 1, 768L * 1024 * 1024, 1024L * 1024 * 1024, true, true),
        TimeSpan.FromSeconds(30), 8192, 65536,
        @"C:\repo\game\Rounds.Game.dll", new string('a', 64), new string('b', 32),
        "WinSta0\\Default", []);

    private static byte[] MutatedTinyPng(string mutation)
    {
        var rgba = Enumerable.Range(0, 16).Select(value => (byte)value).ToArray();
        var chunks = BuildChunks(2, 2, rgba);
        switch (mutation)
        {
            case "signature":
            {
                var result = EncodeChunks(chunks);
                result[0] = 0;
                return result;
            }
            case "crc":
            {
                var result = EncodeChunks(chunks);
                result[^1] ^= 1;
                return result;
            }
            case "ihdr-not-first": chunks.Insert(0, new Chunk("gAMA", [0, 0, 177, 143])); break;
            case "ihdr-duplicate": chunks.Insert(1, chunks[0]); break;
            case "ihdr-width": chunks[0].Data[3] = 3; break;
            case "ihdr-depth": chunks[0].Data[8] = 16; break;
            case "unknown-critical": chunks.Insert(1, new Chunk("ABCD", [])); break;
            case "idat-separated": chunks.Insert(2, new Chunk("gAMA", [0, 0, 177, 143])); chunks.Insert(3, chunks[1]); break;
            case "iend-trailing": return [.. EncodeChunks(chunks), 1];
            case "iend-missing": chunks.RemoveAt(chunks.Count - 1); break;
            case "chunk-truncated": return EncodeChunks(chunks)[..^3];
            case "zlib-header": chunks[1].Data[0] = 0; break;
            case "adler": chunks[1].Data[^1] ^= 1; break;
            case "filter":
                chunks = BuildChunks(2, 2, rgba, [5, 0]);
                break;
            case "decompressed-short": chunks[1] = BuildChunks(2, 1, rgba[..8])[1]; break;
            case "decompressed-long": chunks[1] = BuildChunks(2, 3, [.. rgba, .. new byte[8]])[1]; break;
            case "deflate-trailing":
            {
                var zlib = chunks[1].Data;
                chunks[1] = new Chunk("IDAT", [.. zlib[..^4], 0, .. zlib[^4..]]);
                break;
            }
        }
        return EncodeChunks(chunks);
    }

    private static byte[] BuildPng(int width, int height, byte[] rgba, int[]? filters = null) =>
        EncodeChunks(BuildChunks(width, height, rgba, filters));

    private static List<Chunk> BuildChunks(int width, int height, byte[] rgba, int[]? filters = null)
    {
        filters ??= Enumerable.Repeat(0, height).ToArray();
        var filtered = Filter(rgba, width, height, filters);
        byte[] zlib;
        using (var output = new MemoryStream())
        {
            using (var compressor = new ZLibStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                compressor.Write(filtered);
            }
            zlib = output.ToArray();
        }
        var ihdr = new byte[13];
        BinaryPrimitives.WriteUInt32BigEndian(ihdr, checked((uint)width));
        BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4), checked((uint)height));
        ihdr[8] = 8;
        ihdr[9] = 6;
        return [new("IHDR", ihdr), new("IDAT", zlib), new("IEND", [])];
    }

    private static byte[] Filter(byte[] rgba, int width, int height, int[] filters)
    {
        var stride = checked(width * 4);
        Assert.Equal(stride * height, rgba.Length);
        Assert.Equal(height, filters.Length);
        var result = new byte[checked(height * (stride + 1))];
        var target = 0;
        for (var row = 0; row < height; row++)
        {
            var filter = filters[row];
            result[target++] = checked((byte)filter);
            for (var column = 0; column < stride; column++)
            {
                var offset = row * stride + column;
                var left = column >= 4 ? rgba[offset - 4] : 0;
                var up = row > 0 ? rgba[offset - stride] : 0;
                var upLeft = row > 0 && column >= 4 ? rgba[offset - stride - 4] : 0;
                var predictor = filter switch
                {
                    0 or 5 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => 0,
                };
                result[target++] = unchecked((byte)(rgba[offset] - predictor));
            }
        }
        return result;
    }

    private static int Paeth(int left, int up, int upLeft)
    {
        var estimate = left + up - upLeft;
        var pa = System.Math.Abs(estimate - left);
        var pb = System.Math.Abs(estimate - up);
        var pc = System.Math.Abs(estimate - upLeft);
        return pa <= pb && pa <= pc ? left : pb <= pc ? up : upLeft;
    }

    private static byte[] EncodeChunks(IEnumerable<Chunk> chunks)
    {
        using var output = new MemoryStream();
        output.Write([137, 80, 78, 71, 13, 10, 26, 10]);
        var length = new byte[4];
        var crc = new byte[4];
        foreach (var chunk in chunks)
        {
            BinaryPrimitives.WriteUInt32BigEndian(length, checked((uint)chunk.Data.Length));
            output.Write(length);
            var type = Encoding.ASCII.GetBytes(chunk.Type);
            output.Write(type);
            output.Write(chunk.Data);
            BinaryPrimitives.WriteUInt32BigEndian(crc, Crc(type, chunk.Data));
            output.Write(crc);
        }
        return output.ToArray();
    }

    private static byte[] SupportedAncillaryData(string type) => type switch
    {
        "cHRM" => StandardSrgbChromaticities(),
        "gAMA" => [0, 0, 177, 143],
        "sBIT" => [8, 8, 8, 8],
        "sRGB" => [0],
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    private static byte[] StandardSrgbChromaticities()
    {
        uint[] values = [31270, 32900, 64000, 33000, 30000, 60000, 15000, 6000];
        var result = new byte[values.Length * sizeof(uint)];
        for (var index = 0; index < values.Length; index++)
        {
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(index * sizeof(uint)), values[index]);
        }
        return result;
    }

    private static byte[] DirectoryPage(params (string Name, uint Attributes)[] entries)
    {
        var encoded = entries.Select(entry => Encoding.Unicode.GetBytes(entry.Name)).ToArray();
        var sizes = encoded.Select(name => Align8(Win32PublishedDirectoryPageParser.FileNameOffset + name.Length)).ToArray();
        var page = new byte[sizes.Sum()];
        var offset = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            var next = index == entries.Length - 1 ? 0 : sizes[index];
            BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(offset), checked((uint)next));
            BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(offset + 56), entries[index].Attributes);
            BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(offset + 60), checked((uint)encoded[index].Length));
            encoded[index].CopyTo(page.AsSpan(offset + Win32PublishedDirectoryPageParser.FileNameOffset));
            offset += sizes[index];
        }
        return page;
    }

    private static int Align8(int value) => checked((value + 7) & ~7);

    private static byte[] StreamPage(params (string Name, long Length)[] entries)
    {
        var encoded = entries.Select(entry => Encoding.Unicode.GetBytes(entry.Name)).ToArray();
        var sizes = encoded.Select(name => Align8(Win32PublishedStreamPageParser.StreamNameOffset + name.Length)).ToArray();
        var page = new byte[sizes.Sum()];
        var offset = 0;
        for (var index = 0; index < entries.Length; index++)
        {
            BinaryPrimitives.WriteUInt32LittleEndian(
                page.AsSpan(offset),
                checked((uint)(index == entries.Length - 1 ? 0 : sizes[index])));
            BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(offset + 4), checked((uint)encoded[index].Length));
            BinaryPrimitives.WriteInt64LittleEndian(page.AsSpan(offset + 8), entries[index].Length);
            encoded[index].CopyTo(page.AsSpan(offset + Win32PublishedStreamPageParser.StreamNameOffset));
            offset += sizes[index];
        }
        return page;
    }

    private static uint Crc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = 0xffffffffU;
        foreach (var value in type) crc = CrcByte(crc, value);
        foreach (var value in data) crc = CrcByte(crc, value);
        return crc ^ 0xffffffffU;
    }

    private static uint CrcByte(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? 0xedb88320U ^ (crc >> 1) : crc >> 1;
        return crc;
    }

    private static string ValidHash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void AssertOrdered(IEnumerable<string> events, params string[] expected)
    {
        var all = events.ToArray();
        var previous = -1;
        foreach (var value in expected)
        {
            var next = Array.FindIndex(all, previous + 1, item => item == value);
            Assert.True(next > previous, $"Missing ordered event {value}: {string.Join(", ", all)}");
            previous = next;
        }
    }

    private sealed record Chunk(string Type, byte[] Data);
}
