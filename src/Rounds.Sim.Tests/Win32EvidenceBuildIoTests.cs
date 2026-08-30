using System.Buffers.Binary;
using System.Collections;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using Rounds.EvidenceLauncher;
using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class Win32EvidenceBuildIoTests
{
    private const string Root = @"C:\repo";
    private const string Game = @"C:\repo\game\.godot\mono\temp\bin\Debug\Rounds.Game.dll";

    [Fact]
    public void Environment_factory_returns_fresh_immutable_exact_lease_and_revalidates_empty_directories()
    {
        var native = new FakeNative();
        var factory = new Win32EvidenceBuildEnvironmentFactory(native);

        using var first = factory.CreateSanitized(Invocation(), Trusted(@"C:\Windows", "windows"), Trusted(@"C:\Temp", "temp"));
        using var second = factory.CreateSanitized(Invocation(), Trusted(@"C:\Windows", "windows"), Trusted(@"C:\Temp", "temp"));

        Assert.NotSame(first.Environment, second.Environment);
        Assert.Equal(13, first.Environment.Count);
        Assert.Equal(@"C:\Windows", first.Environment["systemroot"]);
        Assert.Equal(@"C:\Temp", first.Environment["tmp"]);
        Assert.Equal(@"C:\repo\.tools\nuget-packages", first.Environment["NUGET_PACKAGES"]);
        Assert.Equal(@"C:\repo\.tools\dotnet-home", first.Environment["DOTNET_CLI_HOME"]);
        Assert.Equal(@"C:\repo\.tools\empty\msbuild-user", first.Environment["MSBuildUserExtensionsPath"]);
        Assert.Throws<NotSupportedException>(() =>
            ((IDictionary<string, string>)first.Environment).Add("hostile", "value"));
        var proof = first.Revalidate();
        Assert.True(proof.BothDirectoriesEmpty);
        Assert.True(proof.AllAncestorIdentitiesStable);
        Assert.True(proof.DotNetCliHomeWriteExclusionActive);
        Assert.True(proof.MsBuildUserExtensionsWriteExclusionActive);
        Assert.True(proof.NoWriteBreakObserved);
        Assert.NotEqual(proof.DotNetCliHomeDirectoryIdentity, proof.MsBuildUserExtensionsDirectoryIdentity);
        Assert.Equal(4, native.DirectoryOpens.Count(open =>
            open.Flags == Win32EvidenceBuildOutputApi.EnvironmentLeafDirectoryFlags));
        Assert.All(native.DirectoryOpens.Where(open =>
            open.Flags == Win32EvidenceBuildOutputApi.EnvironmentLeafDirectoryFlags), open =>
            Assert.True(open.Path.EndsWith("dotnet-home", StringComparison.Ordinal) ||
                        open.Path.EndsWith("msbuild-user", StringComparison.Ordinal)));
        Assert.DoesNotContain(native.Events, value => value.StartsWith("create", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("identity")]
    [InlineData("reparse")]
    [InlineData("handle")]
    [InlineData("alias")]
    [InlineData("relative")]
    [InlineData("nul")]
    public void Environment_factory_rejects_untrusted_system_or_temp_fact(string mutation)
    {
        var native = new FakeNative();
        var system = Trusted(@"C:\Windows", "windows");
        system = mutation switch
        {
            "missing" => system with { Exists = false },
            "identity" => system with { IdentityBound = false },
            "reparse" => system with { ReparseFreeAncestors = false },
            "handle" => system with { OpenedHandleIdentity = string.Empty },
            "alias" => system with { RequestedPath = @"C:\Windows\." },
            "relative" => system with { RequestedPath = "Windows", CanonicalPath = "Windows" },
            "nul" => system with { RequestedPath = "C:\\Win\0dows" },
            _ => system,
        };

        Assert.Throws<InvalidOperationException>(() =>
            new Win32EvidenceBuildEnvironmentFactory(native)
                .CreateSanitized(Invocation(), system, Trusted(@"C:\Temp", "temp")));

        Assert.Empty(native.Events);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("extra")]
    [InlineData("value")]
    [InlineData("nul")]
    [InlineData("equals")]
    [InlineData("case-duplicate")]
    [InlineData("sdk-alias")]
    public void Environment_factory_rejects_mutable_or_malformed_required_entries(string mutation)
    {
        IReadOnlyDictionary<string, string> environment = mutation == "case-duplicate"
            ? new HostileDictionary(Invocation().Environment.Concat(
                [new KeyValuePair<string, string>("dotnet_processor_count", "2")]))
            : MutateEnvironment(Invocation().Environment, mutation);
        var invocation = Invocation() with { Environment = environment };

        Assert.Throws<InvalidOperationException>(() =>
            new Win32EvidenceBuildEnvironmentFactory(new FakeNative()).CreateSanitized(
                invocation, Trusted(@"C:\Windows", "windows"), Trusted(@"C:\Temp", "temp")));
    }

    [Fact]
    public void Environment_lease_rejects_nonempty_or_drifting_dedicated_directory_and_retries_failed_close()
    {
        var native = new FakeNative { NonEmptyEnvironmentLeaf = true };
        Assert.Throws<InvalidOperationException>(() =>
            new Win32EvidenceBuildEnvironmentFactory(native).CreateSanitized(
                Invocation(), Trusted(@"C:\Windows", "windows"), Trusted(@"C:\Temp", "temp")));

        native = new FakeNative();
        var lease = new Win32EvidenceBuildEnvironmentFactory(native).CreateSanitized(
            Invocation(), Trusted(@"C:\Windows", "windows"), Trusted(@"C:\Temp", "temp"));
        native.DriftPath = @"C:\repo\.tools\dotnet-home";
        Assert.Throws<InvalidOperationException>(() => lease.Revalidate());
        native.DriftPath = null;
        native.FailNextClose = true;
        Assert.Throws<InvalidOperationException>(() => lease.Dispose());
        lease.Dispose();
        Assert.Contains(2, native.CloseCalls.Values);
    }

    [Fact]
    public void Environment_write_exclusion_is_sticky_across_transient_injection_and_retained_until_attestation_owner_disposes()
    {
        var native = new FakeNative();
        var lease = new Win32EvidenceBuildEnvironmentFactory(native).CreateSanitized(
            Invocation(), Trusted(@"C:\Windows", "windows"), Trusted(@"C:\Temp", "temp"));

        Assert.Equal(2, native.WriteExclusions.Count);
        Assert.All(native.WriteExclusions, exclusion => Assert.False(exclusion.Disposed));
        Assert.NotEmpty(native.LiveHandles);

        native.WriteExclusions[0].BreakObserved = true;
        native.NonEmptyEnvironmentLeaf = true;
        native.NonEmptyEnvironmentLeaf = false;
        var proof = lease.Revalidate();
        Assert.False(proof.DotNetCliHomeWriteExclusionActive);
        Assert.False(proof.NoWriteBreakObserved);
        Assert.All(native.WriteExclusions, exclusion => Assert.False(exclusion.Disposed));

        lease.Dispose();
        Assert.All(native.WriteExclusions, exclusion => Assert.True(exclusion.Disposed));
        Assert.Empty(native.LiveHandles);
    }

    [Fact]
    public void Environment_write_exclusion_cleanup_failure_retains_directory_handles_for_bounded_retry()
    {
        var native = new FakeNative();
        var lease = new Win32EvidenceBuildEnvironmentFactory(native).CreateSanitized(
            Invocation(), Trusted(@"C:\Windows", "windows"), Trusted(@"C:\Temp", "temp"));
        native.WriteExclusions[1].FailDisposeOnce = true;

        Assert.Throws<InvalidOperationException>(() => lease.Dispose());
        Assert.NotEmpty(native.LiveHandles);
        Assert.Empty(native.ClosedDirectoryHandles);

        lease.Dispose();
        Assert.Empty(native.LiveHandles);
        Assert.All(native.WriteExclusions, exclusion => Assert.Equal(1, exclusion.SuccessfulDisposals));
    }

    [Fact]
    public void Acquisition_cleanup_failure_transfers_strong_ownership_and_reaper_releases_in_order()
    {
        var scheduler = new FakeReaperScheduler();
        var owner = new Win32EvidenceBuildEnvironmentCleanupOwner(scheduler);
        var native = new FakeNative
        {
            NonEmptyEnvironmentLeaf = true,
            NewExclusionDisposeFailures = 1,
        };

        var failure = Assert.Throws<AggregateException>(() =>
            new Win32EvidenceBuildEnvironmentFactory(native, owner).CreateSanitized(
                Invocation(), Trusted(@"C:\Windows", "windows"), Trusted(@"C:\Temp", "temp")));

        Assert.Contains(failure.Flatten().InnerExceptions, value =>
            value.Message.Contains("not empty", StringComparison.Ordinal));
        Assert.Equal(1, owner.RetainedCount);
        Assert.NotEmpty(native.LiveHandles);
        Assert.Empty(native.ClosedDirectoryHandles);

        scheduler.RunAll();

        Assert.Equal(0, owner.RetainedCount);
        Assert.Empty(native.LiveHandles);
        Assert.Equal(native.DirectoryOpenHandles.AsEnumerable().Reverse(), native.ClosedDirectoryHandles);
    }

    [Fact]
    public void Scheduler_failure_retains_strong_ownership_until_explicit_retry_succeeds()
    {
        var scheduler = new FakeReaperScheduler { FailSchedule = true };
        var owner = new Win32EvidenceBuildEnvironmentCleanupOwner(scheduler);
        var native = new FakeNative
        {
            NonEmptyEnvironmentLeaf = true,
            NewExclusionDisposeFailures = 1,
        };

        var failure = Assert.Throws<AggregateException>(() =>
            new Win32EvidenceBuildEnvironmentFactory(native, owner).CreateSanitized(
                Invocation(), Trusted(@"C:\Windows", "windows"), Trusted(@"C:\Temp", "temp")));
        Assert.Contains(failure.Flatten().InnerExceptions, value => value.Message == "scheduler failure");
        Assert.Equal(1, owner.RetainedCount);
        Assert.NotEmpty(native.LiveHandles);

        scheduler.FailSchedule = false;
        owner.RetryRetained();
        scheduler.RunAll();
        Assert.Equal(0, owner.RetainedCount);
        Assert.Empty(native.LiveHandles);
    }

    [Fact]
    public void Terminal_ambiguous_exclusion_cleanup_is_never_retried_at_handle_level_or_released()
    {
        var scheduler = new FakeReaperScheduler();
        var owner = new Win32EvidenceBuildEnvironmentCleanupOwner(scheduler);
        var native = new FakeNative
        {
            NonEmptyEnvironmentLeaf = true,
            NewExclusionTerminalAmbiguity = true,
        };

        Assert.Throws<AggregateException>(() =>
            new Win32EvidenceBuildEnvironmentFactory(native, owner).CreateSanitized(
                Invocation(), Trusted(@"C:\Windows", "windows"), Trusted(@"C:\Temp", "temp")));
        scheduler.RunAll();

        var exclusion = Assert.Single(native.WriteExclusions);
        Assert.Equal(1, exclusion.EventCloseAttempts);
        Assert.Equal(1, owner.RetainedCount);
        Assert.NotEmpty(native.LiveHandles);
        Assert.Empty(native.ClosedDirectoryHandles);

        owner.RetryRetained();
        scheduler.RunAll();
        Assert.Equal(1, exclusion.EventCloseAttempts);
        Assert.Equal(1, owner.RetainedCount);
        Assert.Empty(native.ClosedDirectoryHandles);
    }

    [Fact]
    public void Prior_output_open_delete_and_absence_are_same_handle_identity_bound_and_ancestors_remain_owned()
    {
        var native = new FakeNative();
        native.AddFile(Game, "1", 200);
        var api = new Win32EvidenceBuildOutputApi(native, Root);

        var lease = api.OpenPrior(Game);

        var open = Assert.Single(native.FileOpens);
        Assert.Equal(Win32EvidenceBuildOutputApi.PriorFileDesiredAccess, open.Access);
        Assert.Equal(Win32EvidenceBuildOutputApi.FileShareMode, open.Share);
        Assert.Equal(Win32EvidenceBuildOutputApi.FileFlags, open.Flags);
        Assert.All(native.DirectoryOpens, directory =>
        {
            Assert.Equal(Win32EvidenceBuildOutputApi.DirectoryDesiredAccess, directory.Access);
            Assert.Equal(Win32EvidenceBuildOutputApi.DirectoryShareMode, directory.Share);
            Assert.Equal(Win32EvidenceBuildOutputApi.DirectoryFlags, directory.Flags);
        });
        Assert.True(lease.RetainsExactFileAndAncestorIdentity);
        Assert.Equal("volume:0000000000000007:file:" + new string('1', 32), lease.State.OpenedHandleIdentity);

        var proof = lease.DeleteRetainedIdentityAndProveAbsent();

        Assert.True(proof.ExactRetainedIdentityDisposition);
        Assert.True(proof.ExactPathAbsent);
        Assert.True(proof.AncestorIdentityStillRetained);
        Assert.Single(native.Dispositions);
        Assert.Equal(Win32EvidenceBuildOutputApi.DeleteDispositionFlags, native.Dispositions[0].Flags);
        Assert.DoesNotContain(native.Events, value => value.StartsWith("delete-path", StringComparison.Ordinal));
        Assert.True(native.LiveHandles.Count > 0);
        Assert.Throws<InvalidOperationException>(() => lease.DeleteRetainedIdentityAndProveAbsent());

        lease.Dispose();
        Assert.Empty(native.LiveHandles);
        Assert.Equal(native.DirectoryOpenHandles.AsEnumerable().Reverse(), native.ClosedDirectoryHandles);
    }

    [Theory]
    [InlineData("reparse")]
    [InlineData("hardlink")]
    [InlineData("ads")]
    [InlineData("delete-pending")]
    [InlineData("zero-id")]
    [InlineData("directory")]
    [InlineData("drift")]
    public void Prior_output_refuses_invalid_file_shape_or_snapshot_drift_and_closes_every_handle(string mutation)
    {
        var native = new FakeNative();
        native.AddFile(Game, mutation == "zero-id" ? "0" : "1", 200);
        native.FileMutation = mutation;

        Assert.Throws<InvalidOperationException>(() => new Win32EvidenceBuildOutputApi(native, Root).OpenPrior(Game));

        Assert.Empty(native.LiveHandles);
    }

    [Theory]
    [InlineData(@"C:\repo\game\.godot\mono\temp\bin\Debug\Other.dll")]
    [InlineData(@"C:\repo\game\.godot\mono\temp\bin\Debug\Rounds.Game.dll:ads")]
    [InlineData(@"C:\repo\game\.godot\mono\temp\bin\Debug\.\Rounds.Game.dll")]
    [InlineData(@"\\?\C:\repo\game\.godot\mono\temp\bin\Debug\Rounds.Game.dll")]
    [InlineData(@"C:\repo\game\.godot\mono\temp\bin\Debug\rounds.game.dll")]
    public void Output_adapter_refuses_nonexact_unadmitted_alias_or_ads_path(string path)
    {
        Assert.Throws<InvalidOperationException>(() =>
            new Win32EvidenceBuildOutputApi(new FakeNative(), Root).OpenPrior(path));
    }

    [Fact]
    public void Delete_failure_is_one_shot_and_ambiguous_file_close_is_not_retried()
    {
        var native = new FakeNative();
        native.AddFile(Game, "1", 200);
        native.FailDisposition = true;
        var lease = new Win32EvidenceBuildOutputApi(native, Root).OpenPrior(Game);

        Assert.Throws<InvalidOperationException>(() => lease.DeleteRetainedIdentityAndProveAbsent());
        Assert.Throws<InvalidOperationException>(() => lease.DeleteRetainedIdentityAndProveAbsent());
        Assert.Single(native.Dispositions);
        lease.Dispose();

        native = new FakeNative();
        native.AddFile(Game, "1", 200);
        lease = new Win32EvidenceBuildOutputApi(native, Root).OpenPrior(Game);
        native.FailFileClose = true;
        Assert.Throws<InvalidOperationException>(() => lease.DeleteRetainedIdentityAndProveAbsent());
        Assert.Throws<InvalidOperationException>(() => lease.Dispose());
        Assert.Equal(1, native.FileCloseAttempts);
    }

    [Fact]
    public void Delete_refuses_replacement_or_ancestor_drift_after_disposition_and_aggregates_reverse_close_failures()
    {
        var native = new FakeNative();
        native.AddFile(Game, "1", 200);
        native.KeepReplacementAfterClose = true;
        var lease = new Win32EvidenceBuildOutputApi(native, Root).OpenPrior(Game);
        Assert.Throws<InvalidOperationException>(() => lease.DeleteRetainedIdentityAndProveAbsent());
        lease.Dispose();

        native = new FakeNative();
        native.AddFile(Game, "1", 200);
        lease = new Win32EvidenceBuildOutputApi(native, Root).OpenPrior(Game);
        native.DriftAncestorsAfterDisposition = true;
        Assert.Throws<InvalidOperationException>(() => lease.DeleteRetainedIdentityAndProveAbsent());
        native.FailAllDirectoryCloses = true;
        var cleanup = Assert.Throws<AggregateException>(() => lease.Dispose());
        Assert.True(cleanup.Flatten().InnerExceptions.Count > 4);
    }

    [Fact]
    public void Recreated_read_is_nonmutating_stable_default_stream_only_and_aggregates_cleanup()
    {
        var native = new FakeNative();
        native.AddFile(Game, "2", 300);
        var state = new Win32EvidenceBuildOutputApi(native, Root).ReadRecreated(Game);
        Assert.Equal("volume:0000000000000007:file:" + new string('2', 32), state.OpenedHandleIdentity);
        Assert.Empty(native.Dispositions);
        Assert.Empty(native.LiveHandles);

        native = new FakeNative { FileMutation = "ads", FailAllDirectoryCloses = true };
        native.AddFile(Game, "2", 300);
        Assert.Throws<AggregateException>(() => new Win32EvidenceBuildOutputApi(native, Root).ReadRecreated(Game));
    }

    [Fact]
    public void Runtime_directory_enumerator_accepts_more_than_four_entries_and_bounds_pages_and_framing()
    {
        var page = DirectoryPage(Enumerable.Range(0, 12).Select(index => $"file-{index}.dll").ToArray());
        var entries = Win32BuildDirectoryEnumerator.Collect(index => index == 0
            ? new Win32PublishedDirectoryReadResult(true, 0, page)
            : new Win32PublishedDirectoryReadResult(false, Win32PublishedFrameApi.ErrorNoMoreFiles, []));
        Assert.Equal(12, entries.Count);

        Assert.Throws<InvalidDataException>(() => Win32BuildDirectoryEnumerator.Collect(_ =>
            new Win32PublishedDirectoryReadResult(true, 0, DirectoryPage(["."]))));
        var overlap = DirectoryPage(["one", "two"]);
        BinaryPrimitives.WriteUInt32LittleEndian(overlap.AsSpan(0, 4), 104);
        Assert.Throws<InvalidDataException>(() => Win32BuildDirectoryPageParser.Parse(overlap));
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad\0name")]
    [InlineData(@"sub\file.dll")]
    [InlineData("sub/file.dll")]
    [InlineData("file.dll:evil")]
    [InlineData("C:leaf.dll")]
    [InlineData("trailing.")]
    [InlineData("trailing ")]
    [InlineData("CON.txt")]
    [InlineData("bad?.dll")]
    public void Runtime_directory_parser_refuses_names_that_cannot_prove_a_leaf_absent(string name)
    {
        Assert.Throws<InvalidDataException>(() => Win32BuildDirectoryPageParser.Parse(DirectoryPage([name])));
    }

    [Fact]
    public void Runtime_directory_parser_refuses_malformed_surrogate_and_false_absence_end_to_end()
    {
        var malformed = DirectoryPage(["aa"]);
        BinaryPrimitives.WriteUInt32LittleEndian(malformed.AsSpan(60, 4), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(malformed.AsSpan(104, 2), 0xd800);
        Assert.Throws<InvalidDataException>(() => Win32BuildDirectoryPageParser.Parse(malformed));

        var native = new FakeNative { InjectUnsafeAbsenceName = true };
        native.AddFile(Game, "1", 200);
        var lease = new Win32EvidenceBuildOutputApi(native, Root).OpenPrior(Game);
        Assert.Throws<InvalidDataException>(() => lease.DeleteRetainedIdentityAndProveAbsent());
        lease.Dispose();
    }

    [Fact]
    public void Ancestor_snapshot_failure_is_owned_before_validation_and_aggregates_one_shot_close_failure()
    {
        var native = new FakeNative
        {
            ThrowDirectorySnapshotPath = @"C:\repo\game",
            FailClosePath = @"C:\repo\game",
        };
        native.AddFile(Game, "1", 200);

        var failure = Assert.Throws<AggregateException>(() =>
            new Win32EvidenceBuildOutputApi(native, Root).OpenPrior(Game));

        Assert.Contains(failure.Flatten().InnerExceptions, exception => exception.Message == "snapshot failure");
        Assert.Contains(failure.Flatten().InnerExceptions, exception => exception.Message.Contains("Closing runtime output ancestor failed", StringComparison.Ordinal));
        var failedClose = Assert.Single(native.CloseAttempts.Where(value => value.Path == @"C:\repo\game"));
        Assert.Equal(1, native.CloseCalls[failedClose.Handle]);
    }

    [Fact]
    public void Disposition_abi_constants_and_layout_are_exact_x64_independent()
    {
        Assert.Equal(21, Win32BuildOutputNative.FileDispositionInfoEx);
        Assert.Equal(4, Marshal.SizeOf<Win32BuildOutputNative.FileDispositionInfoExBuffer>());
        Assert.Equal(4, Win32BuildOutputNative.FileDispositionInfoExSize);
        Assert.Equal((IntPtr)0, Marshal.OffsetOf<Win32BuildOutputNative.FileDispositionInfoExBuffer>("Flags"));
        Assert.Equal(0x13U, Win32EvidenceBuildOutputApi.DeleteDispositionFlags);
        Assert.Equal(
            Win32EvidenceConstants.GenericRead | 0x00010000U,
            Win32EvidenceBuildOutputApi.PriorFileDesiredAccess);
        Assert.Equal(0x00090240U, Win32DirectoryOplockLease.FsctlRequestOplock);
        Assert.Equal(0x5U, Win32DirectoryOplockLease.RequestedLevel);
        Assert.Equal(12, Marshal.SizeOf<Win32DirectoryOplockLease.RequestOplockInputBuffer>());
        Assert.Equal(24, Marshal.SizeOf<Win32DirectoryOplockLease.RequestOplockOutputBuffer>());
        Assert.Equal(32, Marshal.SizeOf<Win32DirectoryOplockLease.NativeOverlappedBuffer>());
        Assert.Equal((IntPtr)24, Marshal.OffsetOf<Win32DirectoryOplockLease.NativeOverlappedBuffer>("EventHandle"));
        Assert.Equal(
            Win32EvidenceBuildOutputApi.DirectoryFlags | 0x40000000U,
            Win32EvidenceBuildOutputApi.EnvironmentLeafDirectoryFlags);
    }

    private sealed class FakeNative : IWin32BuildOutputNative
    {
        private sealed record Owned(string Path, bool Directory, string FileId, long Length);
        private nint _next = 100;
        private readonly Dictionary<nint, Owned> _owned = [];
        private readonly Dictionary<string, (string FileId, long Length)> _files = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<nint> _disposedFiles = [];
        internal List<string> Events { get; } = [];
        internal List<(string Path, uint Access, uint Share, uint Flags)> FileOpens { get; } = [];
        internal List<(string Path, uint Access, uint Share, uint Flags)> DirectoryOpens { get; } = [];
        internal List<nint> DirectoryOpenHandles { get; } = [];
        internal List<nint> ClosedDirectoryHandles { get; } = [];
        internal List<(nint Handle, uint Flags)> Dispositions { get; } = [];
        internal HashSet<nint> LiveHandles => _owned.Keys.ToHashSet();
        internal Dictionary<nint, int> CloseCalls { get; } = [];
        internal List<(nint Handle, string Path)> CloseAttempts { get; } = [];
        internal string? FileMutation { get; set; }
        internal string? DriftPath { get; set; }
        internal bool NonEmptyEnvironmentLeaf { get; set; }
        internal bool FailNextClose { get; set; }
        internal bool FailDisposition { get; set; }
        internal bool FailFileClose { get; set; }
        internal int FileCloseAttempts { get; private set; }
        internal bool KeepReplacementAfterClose { get; set; }
        internal bool DriftAncestorsAfterDisposition { get; set; }
        internal bool FailAllDirectoryCloses { get; set; }
        internal int NewExclusionDisposeFailures { get; set; }
        internal bool NewExclusionTerminalAmbiguity { get; set; }
        internal bool InjectUnsafeAbsenceName { get; set; }
        internal string? ThrowDirectorySnapshotPath { get; set; }
        internal string? FailClosePath { get; set; }
        internal List<FakeWriteExclusion> WriteExclusions { get; } = [];

        internal void AddFile(string path, string fileIdDigit, long length) =>
            _files[path] = (new string(fileIdDigit[0], 32), length);

        public Win32BuildOpenResult OpenDirectory(string path, uint access, uint share, uint flags)
        {
            Events.Add($"open-dir:{path}:{access:x}:{share:x}:{flags:x}");
            DirectoryOpens.Add((path, access, share, flags));
            var handle = _next++;
            var id = handle.ToString("x").PadLeft(32, '0');
            _owned[handle] = new Owned(path, true, id, 0);
            DirectoryOpenHandles.Add(handle);
            return new Win32BuildOpenResult(handle, 0);
        }

        public Win32BuildOpenResult OpenFile(string path, uint access, uint share, uint flags)
        {
            Events.Add($"open-file:{path}:{access:x}:{share:x}:{flags:x}");
            FileOpens.Add((path, access, share, flags));
            if (!_files.TryGetValue(path, out var file)) return new Win32BuildOpenResult(0, 2);
            var handle = _next++;
            _owned[handle] = new Owned(path, false, file.FileId, file.Length);
            return new Win32BuildOpenResult(handle, 0);
        }

        public Win32RetainedFileSnapshot ReadSnapshot(nint handle)
        {
            var owned = _owned[handle];
            if (owned.Directory && string.Equals(owned.Path, ThrowDirectorySnapshotPath, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("snapshot failure");
            }
            var change = 10L;
            if (DriftPath is not null && string.Equals(owned.Path, DriftPath, StringComparison.OrdinalIgnoreCase)) change++;
            if (FileMutation == "drift" && !owned.Directory && Events.Count(value => value == $"snapshot:{handle}") > 0) change++;
            if (DriftAncestorsAfterDisposition && owned.Directory && _disposedFiles.Count > 0) owned = owned with { FileId = new string('e', 32) };
            Events.Add($"snapshot:{handle}");
            return new Win32RetainedFileSnapshot(
                owned.Path,
                7,
                FileMutation == "zero-id" && !owned.Directory ? new string('0', 32) : owned.FileId,
                owned.Length,
                change,
                owned.Directory ? Win32EvidenceConstants.FileAttributeDirectory : Win32EvidenceConstants.FileAttributeNormal,
                FileMutation == "reparse" && !owned.Directory ? 1U : 0U,
                FileMutation == "hardlink" && !owned.Directory ? 2U : 1U,
                _disposedFiles.Contains(handle) || (FileMutation == "delete-pending" && !owned.Directory),
                FileMutation == "directory" && !owned.Directory || owned.Directory);
        }

        public IReadOnlyList<Win32PublishedArtifactEntry> EnumerateDirectory(nint handle)
        {
            var path = _owned[handle].Path;
            if (NonEmptyEnvironmentLeaf && (path.EndsWith("dotnet-home", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith("msbuild-user", StringComparison.OrdinalIgnoreCase)))
            {
                return [new Win32PublishedArtifactEntry("hostile.props", false, false)];
            }
            if (path.EndsWith(@"bin\Debug", StringComparison.OrdinalIgnoreCase))
            {
                if (InjectUnsafeAbsenceName)
                {
                    return Win32BuildDirectoryPageParser.Parse(DirectoryPage(["bad:name"]));
                }
                var entries = Enumerable.Range(0, 12)
                    .Select(index => new Win32PublishedArtifactEntry($"other-{index}.dll", false, false)).ToList();
                if (KeepReplacementAfterClose || !_disposedFiles.Any())
                {
                    entries.Add(new Win32PublishedArtifactEntry("Rounds.Game.dll", false, false));
                }
                return entries;
            }
            return [];
        }

        public IReadOnlyList<Win32PublishedStreamEntry> EnumerateStreams(nint handle)
        {
            var owned = _owned[handle];
            return FileMutation == "ads"
                ? [new("::$DATA", owned.Length), new(":evil:$DATA", 1)]
                : [new("::$DATA", owned.Length)];
        }

        public IEvidenceDirectoryWriteExclusionLease AcquireDirectoryWriteExclusion(
            nint retainedDirectoryHandle,
            string exactDirectoryIdentity)
        {
            Events.Add($"exclude:{retainedDirectoryHandle}:{exactDirectoryIdentity}");
            var exclusion = new FakeWriteExclusion(exactDirectoryIdentity)
            {
                RemainingDisposeFailures = NewExclusionDisposeFailures,
                TerminalAmbiguity = NewExclusionTerminalAmbiguity,
            };
            WriteExclusions.Add(exclusion);
            return exclusion;
        }

        public Win32BuildCallResult SetDeleteDisposition(nint handle, uint flags)
        {
            Dispositions.Add((handle, flags));
            if (FailDisposition) return new Win32BuildCallResult(false, 50);
            _disposedFiles.Add(handle);
            return new Win32BuildCallResult(true, 0);
        }

        public Win32BuildCallResult CloseKernelHandle(nint handle)
        {
            CloseCalls[handle] = CloseCalls.GetValueOrDefault(handle) + 1;
            var owned = _owned[handle];
            CloseAttempts.Add((handle, owned.Path));
            if (!owned.Directory) FileCloseAttempts++;
            if (FailNextClose)
            {
                FailNextClose = false;
                return new Win32BuildCallResult(false, 6);
            }
            if (FailFileClose && !owned.Directory) return new Win32BuildCallResult(false, 6);
            if (string.Equals(owned.Path, FailClosePath, StringComparison.Ordinal)) return new Win32BuildCallResult(false, 6);
            if (FailAllDirectoryCloses && owned.Directory) return new Win32BuildCallResult(false, 6);
            _owned.Remove(handle);
            if (owned.Directory) ClosedDirectoryHandles.Add(handle);
            return new Win32BuildCallResult(true, 0);
        }

        internal sealed class FakeWriteExclusion(string directoryIdentity) : IEvidenceDirectoryWriteExclusionLease
        {
            internal bool BreakObserved { get; set; }
            internal bool FailDisposeOnce { get; set; }
            internal int RemainingDisposeFailures { get; set; }
            internal bool TerminalAmbiguity { get; set; }
            internal bool Disposed { get; private set; }
            internal int SuccessfulDisposals { get; private set; }
            internal int EventCloseAttempts { get; private set; }
            private bool TerminalFailureObserved { get; set; }

            public EvidenceDirectoryWriteExclusionStatus Observe()
            {
                if (Disposed) throw new ObjectDisposedException(nameof(FakeWriteExclusion));
                return new EvidenceDirectoryWriteExclusionStatus(directoryIdentity, !BreakObserved, BreakObserved);
            }

            public void Dispose()
            {
                if (Disposed) return;
                if (TerminalFailureObserved)
                {
                    throw new InvalidOperationException("event handle ownership remains ambiguous");
                }
                if (TerminalAmbiguity)
                {
                    EventCloseAttempts++;
                    TerminalFailureObserved = true;
                    throw new InvalidOperationException("event CloseHandle was ambiguous");
                }
                if (RemainingDisposeFailures > 0)
                {
                    RemainingDisposeFailures--;
                    throw new InvalidOperationException("write-exclusion cleanup failed");
                }
                if (FailDisposeOnce)
                {
                    FailDisposeOnce = false;
                    throw new InvalidOperationException("write-exclusion cleanup failed");
                }
                Disposed = true;
                SuccessfulDisposals++;
            }
        }
    }

    private sealed class FakeReaperScheduler : IEvidenceBuildCleanupReaperScheduler
    {
        private readonly Queue<Action> _actions = [];
        internal bool FailSchedule { get; set; }
        internal int Backoffs { get; private set; }

        public void Schedule(Action action)
        {
            if (FailSchedule) throw new InvalidOperationException("scheduler failure");
            _actions.Enqueue(action);
        }

        public void Backoff(TimeSpan delay)
        {
            Assert.Equal(TimeSpan.FromSeconds(1), delay);
            Backoffs++;
        }

        internal void RunAll()
        {
            while (_actions.TryDequeue(out var action)) action();
        }
    }

    private sealed class HostileDictionary(IEnumerable<KeyValuePair<string, string>> values) :
        IReadOnlyDictionary<string, string>
    {
        private readonly KeyValuePair<string, string>[] _values = values.ToArray();
        public int Count => _values.Length;
        public IEnumerable<string> Keys => _values.Select(value => value.Key);
        public IEnumerable<string> Values => _values.Select(value => value.Value);
        public string this[string key] => _values.First(value => value.Key == key).Value;
        public bool ContainsKey(string key) => _values.Any(value => value.Key == key);
        public bool TryGetValue(string key, out string value)
        {
            var match = _values.FirstOrDefault(candidate => candidate.Key == key);
            value = match.Value;
            return match.Key is not null;
        }
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => ((IEnumerable<KeyValuePair<string, string>>)_values).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static EvidenceBuildInvocation Invocation() => new(
        BaseProjectileEvidenceLaunchPlanner.MsBuildPath,
        Root,
        [],
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DOTNET_PROCESSOR_COUNT"] = "2",
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["MSBuildEnableWorkloadResolver"] = "false",
            ["MSBuildSDKsPath"] = @"C:\repo\.tools\dotnet\sdk\8.0.423\Sdks",
        }));

    private static EvidenceTrustedDirectoryIdentity Trusted(string path, string identity) =>
        new(path, path, true, true, true, identity);

    private static IReadOnlyDictionary<string, string> MutateEnvironment(
        IReadOnlyDictionary<string, string> source,
        string mutation)
    {
        var values = new Dictionary<string, string>(source, StringComparer.Ordinal);
        if (mutation == "missing") values.Remove("DOTNET_PROCESSOR_COUNT");
        if (mutation == "extra") values["DirectoryBuildPropsPath"] = @"C:\hostile.props";
        if (mutation == "value") values["DOTNET_PROCESSOR_COUNT"] = "3";
        if (mutation == "nul") values["MSBuildSDKsPath"] += "\0evil";
        if (mutation == "equals") values["BAD=KEY"] = "value";
        if (mutation == "sdk-alias") values["MSBuildSDKsPath"] = @"C:\repo\.tools\dotnet\sdk\8.0.423\.\Sdks";
        return new ReadOnlyDictionary<string, string>(values);
    }

    private static byte[] DirectoryPage(string[] names)
    {
        if (names.Length == 0) return new byte[104];
        var records = names.Select(name =>
        {
            var nameBytes = Encoding.Unicode.GetBytes(name);
            var size = (104 + nameBytes.Length + 7) & ~7;
            return (nameBytes, size);
        }).ToArray();
        var page = new byte[records.Sum(value => value.size)];
        var offset = 0;
        for (var index = 0; index < records.Length; index++)
        {
            var next = index == records.Length - 1 ? 0 : records[index].size;
            BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(offset, 4), (uint)next);
            BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(offset + 56, 4), Win32EvidenceConstants.FileAttributeNormal);
            BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(offset + 60, 4), (uint)records[index].nameBytes.Length);
            records[index].nameBytes.CopyTo(page.AsSpan(offset + 104));
            offset += records[index].size;
        }
        return page;
    }
}
