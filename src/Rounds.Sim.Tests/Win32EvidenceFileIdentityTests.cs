using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Rounds.EvidenceLauncher;
using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class Win32EvidenceFileIdentityTests
{
    private const string RequestedPath = @"C:\candidate\Tool.exe";
    private const string FinalPath = @"\\?\C:\candidate\Tool.exe";
    private const string FileId = "0123456789abcdef0123456789abcdef";
    private static readonly byte[] Contents = Encoding.UTF8.GetBytes("retained executable bytes");
    private static readonly string Sha256 = Convert.ToHexString(
        SHA256.HashData(Contents)).ToLowerInvariant();

    [Fact]
    public void Retained_factory_opens_exact_no_replace_contract_and_keeps_handle_through_attestation()
    {
        var api = new FakeFileApi(Contents) { InitialStreamPosition = 3 };
        var factory = new Win32ExecutableIdentityFactory(api);

        var lease = factory.OpenExpected(Profile());

        Assert.Equal(
            new[]
            {
                "open", "snapshot:701", "stream:701", "version:701:C:\\candidate\\Tool.exe",
                "snapshot:701",
            },
            api.Events);
        Assert.Equal(RequestedPath, api.OpenedPath);
        Assert.Equal(Win32EvidenceConstants.GenericRead, api.DesiredAccess);
        Assert.Equal(Win32EvidenceConstants.FileShareRead, api.ShareMode);
        Assert.Equal(0U, api.ShareMode & Win32EvidenceConstants.FileShareWrite);
        Assert.Equal(0U, api.ShareMode & Win32EvidenceConstants.FileShareDelete);
        Assert.Equal(Win32EvidenceConstants.OpenExisting, api.CreationDisposition);
        Assert.Equal(
            Win32EvidenceConstants.FileAttributeNormal |
            Win32EvidenceConstants.FileFlagOpenReparsePoint,
            api.FlagsAndAttributes);
        Assert.Equal(3, api.Stream.Position);
        Assert.Equal(RequestedPath, lease.Identity.Path, ignoreCase: true);
        Assert.Equal(Sha256, lease.Identity.Sha256);
        Assert.Equal("volume:0000000000001234:file:" + FileId, lease.Identity.OpenedHandleIdentity);
        Assert.True(lease.Identity.IdentityBound);
        Assert.False(lease.Identity.IsReparsePoint);
        Assert.Equal((nint)701, lease.DangerousHandle);

        lease.Dispose();
        lease.Dispose();

        Assert.Equal("close:701", api.Events[^1]);
        Assert.Equal(1, api.Events.Count(value => value == "close:701"));
    }

    [Fact]
    public void Windows_final_path_prefix_and_case_normalize_to_the_expected_identity()
    {
        var api = new FakeFileApi(Contents)
        {
            Before = Snapshot() with { FinalPath = @"\\?\c:\CANDIDATE\TOOL.EXE" },
            After = Snapshot() with { FinalPath = @"\\?\C:\candidate\Tool.exe" },
        };

        using var lease = new Win32ExecutableIdentityFactory(api).OpenExpected(
            Profile() with { ExpectedPath = @"c:\Candidate\tool.exe" });

        Assert.Equal(RequestedPath, lease.Identity.Path, ignoreCase: true);
        Assert.Equal(
            @"\\server\share\Tool.exe",
            Win32ExecutableIdentityFactory.NormalizeFinalPath(
                @"\\?\UNC\server\share\Tool.exe"));
    }

    [Theory]
    [InlineData(@"relative\Tool.exe")]
    [InlineData(@"C:\candidate\Tool.exe:stream")]
    [InlineData(@"\\?\C:\candidate\Tool.exe")]
    [InlineData(@"\\.\C:\candidate\Tool.exe")]
    [InlineData(@"\??\C:\candidate\Tool.exe")]
    [InlineData(@"C:\candidate\")]
    public void Requested_path_refuses_relative_ads_extended_or_directory_shapes_before_open(string path)
    {
        var api = new FakeFileApi(Contents);

        Assert.Throws<ArgumentException>(() =>
            new Win32ExecutableIdentityFactory(api).OpenExpected(
                Profile() with { ExpectedPath = path }));

        Assert.Empty(api.Events);
    }

    [Theory]
    [InlineData("directory")]
    [InlineData("directory-attribute")]
    [InlineData("reparse-attribute")]
    [InlineData("reparse-tag")]
    [InlineData("hardlink")]
    [InlineData("delete-pending")]
    [InlineData("zero-file-id")]
    [InlineData("empty")]
    public void Non_regular_reparse_hardlink_or_unstable_shape_refuses_and_closes_once(string shape)
    {
        var valid = Snapshot();
        var snapshot = shape switch
        {
            "directory" => valid with { Directory = true },
            "directory-attribute" => valid with { Attributes = Win32EvidenceConstants.FileAttributeDirectory },
            "reparse-attribute" => valid with { Attributes = Win32EvidenceConstants.FileAttributeReparsePoint },
            "reparse-tag" => valid with { ReparseTag = 0xa000000c },
            "hardlink" => valid with { LinkCount = 2 },
            "delete-pending" => valid with { DeletePending = true },
            "zero-file-id" => valid with { FileId = new string('0', 32) },
            "empty" => valid with { Length = 0 },
            _ => throw new InvalidOperationException(),
        };
        var api = new FakeFileApi(Contents) { Before = snapshot };

        Assert.Throws<InvalidOperationException>(() =>
            new Win32ExecutableIdentityFactory(api).OpenExpected(Profile()));

        Assert.Equal(1, api.Events.Count(value => value == "close:701"));
        Assert.DoesNotContain("stream:701", api.Events);
    }

    [Fact]
    public void Final_path_alias_or_hardlink_name_refuses_before_hashing()
    {
        var api = new FakeFileApi(Contents)
        {
            Before = Snapshot() with { FinalPath = @"\\?\C:\alias\Tool.exe" },
        };

        Assert.Throws<InvalidOperationException>(() =>
            new Win32ExecutableIdentityFactory(api).OpenExpected(Profile()));

        Assert.Equal(new[] { "open", "snapshot:701", "close:701" }, api.Events);
    }

    [Fact]
    public void Size_cap_refuses_before_hash_stream_is_opened()
    {
        var api = new FakeFileApi(Contents)
        {
            Before = Snapshot() with { Length = 101 },
        };

        Assert.Throws<InvalidOperationException>(() =>
            new Win32ExecutableIdentityFactory(api).OpenExpected(
                Profile() with { MaximumBytes = 100 }));

        Assert.DoesNotContain("stream:701", api.Events);
        Assert.Equal(1, api.Events.Count(value => value == "close:701"));
    }

    [Fact]
    public void Identity_or_final_path_drift_after_version_read_refuses_and_closes_once()
    {
        var api = new FakeFileApi(Contents)
        {
            After = Snapshot() with { FileId = "1123456789abcdef0123456789abcdef" },
        };

        Assert.Throws<InvalidOperationException>(() =>
            new Win32ExecutableIdentityFactory(api).OpenExpected(Profile()));

        Assert.Equal(3, api.Stream.Position);
        Assert.Equal("snapshot:701", api.Events[^2]);
        Assert.Equal("close:701", api.Events[^1]);
    }

    [Fact]
    public void Canonical_final_path_drift_after_hash_and_version_refuses()
    {
        var api = new FakeFileApi(Contents)
        {
            After = Snapshot() with { FinalPath = @"\\?\C:\candidate\replacement.exe" },
        };

        Assert.Throws<InvalidOperationException>(() =>
            new Win32ExecutableIdentityFactory(api).OpenExpected(Profile()));

        Assert.Equal(1, api.Events.Count(value => value == "close:701"));
    }

    [Theory]
    [InlineData("hash")]
    [InlineData("file-version")]
    [InlineData("product-version")]
    public void Hash_or_version_mismatch_refuses_while_handle_is_retained(string mismatch)
    {
        var api = new FakeFileApi(Contents);
        var profile = mismatch switch
        {
            "hash" => Profile() with { ExpectedSha256 = new string('f', 64) },
            "file-version" => Profile() with { ExpectedFileVersion = "0" },
            "product-version" => Profile() with { ExpectedProductVersion = "0" },
            _ => throw new InvalidOperationException(),
        };

        Assert.Throws<InvalidOperationException>(() =>
            new Win32ExecutableIdentityFactory(api).OpenExpected(profile));

        Assert.True(api.Events.IndexOf("version:701:C:\\candidate\\Tool.exe") <
                    api.Events.IndexOf("close:701"));
        Assert.Equal(1, api.Events.Count(value => value == "close:701"));
    }

    [Fact]
    public void Version_exception_restores_hash_position_and_closes_once()
    {
        var api = new FakeFileApi(Contents) { ThrowOnVersion = true };

        Assert.Throws<IOException>(() =>
            new Win32ExecutableIdentityFactory(api).OpenExpected(Profile()));

        Assert.Equal(3, api.Stream.Position);
        Assert.Equal(1, api.Events.Count(value => value == "close:701"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Invalid_open_handle_refuses_without_attempting_close(int handle)
    {
        var api = new FakeFileApi(Contents) { OpenHandle = handle };

        Assert.Throws<Win32Exception>(() =>
            new Win32ExecutableIdentityFactory(api).OpenExpected(Profile()));

        Assert.Equal(new[] { "open" }, api.Events);
    }

    [Fact]
    public void Godot_and_msbuild_profiles_pin_distinct_expected_identities()
    {
        var msBuild = Win32ExecutableProfile.MsBuild();

        Assert.Equal(BaseProjectileEvidenceLaunchPlanner.MsBuildPath, msBuild.ExpectedPath);
        Assert.Equal(BaseProjectileEvidenceLaunchPlanner.MsBuildSha256, msBuild.ExpectedSha256);
        Assert.Equal(BaseProjectileEvidenceLaunchPlanner.MsBuildFileVersion, msBuild.ExpectedFileVersion);
        Assert.Equal(BaseProjectileEvidenceLaunchPlanner.MsBuildProductVersion, msBuild.ExpectedProductVersion);
        Assert.Equal(Win32ExecutableProfile.DefaultMaximumExecutableBytes, msBuild.MaximumBytes);
    }

    [Fact]
    public void File_identity_abi_layouts_match_x64_windows()
    {
        Assert.Equal(8, Marshal.SizeOf<Win32FileAttributeTagInfo>());
        Assert.Equal(24, Marshal.SizeOf<Win32FileStandardInfo>());
        Assert.Equal(40, Marshal.SizeOf<Win32FileBasicInfo>());
    }

    private static Win32ExecutableProfile Profile() =>
        new(RequestedPath, Sha256, "1.2.3.4", "1.2.3", 1024);

    private static Win32RetainedFileSnapshot Snapshot() =>
        new(
            FinalPath,
            0x1234,
            FileId,
            Contents.Length,
            987654321,
            Win32EvidenceConstants.FileAttributeNormal,
            0,
            1,
            DeletePending: false,
            Directory: false);

    private sealed class FakeFileApi : IWin32RetainedFileApi
    {
        private int _snapshotReads;

        internal FakeFileApi(byte[] contents)
        {
            Stream = new TrackingMemoryStream(contents);
            Stream.Position = 3;
            Before = Snapshot();
            After = Snapshot();
        }

        internal List<string> Events { get; } = new();
        internal nint OpenHandle { get; init; } = 701;
        internal string? OpenedPath { get; private set; }
        internal uint DesiredAccess { get; private set; }
        internal uint ShareMode { get; private set; }
        internal uint CreationDisposition { get; private set; }
        internal uint FlagsAndAttributes { get; private set; }
        internal Win32RetainedFileSnapshot Before { get; init; }
        internal Win32RetainedFileSnapshot After { get; init; }
        internal TrackingMemoryStream Stream { get; }
        internal long InitialStreamPosition
        {
            init => Stream.Position = value;
        }
        internal bool ThrowOnVersion { get; init; }

        public nint OpenReadNoReplace(
            string normalizedAbsolutePath,
            uint desiredAccess,
            uint shareMode,
            uint creationDisposition,
            uint flagsAndAttributes)
        {
            Events.Add("open");
            OpenedPath = normalizedAbsolutePath;
            DesiredAccess = desiredAccess;
            ShareMode = shareMode;
            CreationDisposition = creationDisposition;
            FlagsAndAttributes = flagsAndAttributes;
            return OpenHandle;
        }

        public Win32RetainedFileSnapshot ReadSnapshot(nint handle)
        {
            Events.Add($"snapshot:{handle}");
            return _snapshotReads++ == 0 ? Before : After;
        }

        public Stream OpenReadStream(nint handle)
        {
            Events.Add($"stream:{handle}");
            return Stream;
        }

        public Win32RetainedFileVersion ReadVersion(nint retainedHandle, string normalizedFinalPath)
        {
            Events.Add($"version:{retainedHandle}:{normalizedFinalPath}");
            if (ThrowOnVersion) throw new IOException("injected version failure");
            return new Win32RetainedFileVersion("1.2.3.4", "1.2.3");
        }

        public bool CloseKernelHandle(nint handle)
        {
            Events.Add($"close:{handle}");
            return true;
        }
    }

    private sealed class TrackingMemoryStream(byte[] contents) : MemoryStream(contents, writable: false)
    {
        protected override void Dispose(bool disposing)
        {
            // The injected stream models a non-owning FileStream over the retained handle.
        }
    }
}
