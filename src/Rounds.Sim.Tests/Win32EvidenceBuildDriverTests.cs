using System.Collections.ObjectModel;
using Rounds.EvidenceLauncher;
using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class Win32EvidenceBuildDriverTests
{
    private const string Root = @"C:\repo";
    private const string RuntimePath = @"C:\repo\game\.godot\mono\temp\bin\Debug\Rounds.Game.dll";
    private const string ReplayPath = @"C:\repo\game\.godot\mono\temp\bin\Debug\Rounds.Replay.dll";
    private const string SimPath = @"C:\repo\game\.godot\mono\temp\bin\Debug\Rounds.Sim.dll";

    [Fact]
    public void Exact_rebuild_retains_runtime_closure_until_attestation_lease_disposal()
    {
        var rig = new Rig();
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        var lease = rig.Driver.RebuildAndAttest(rig.Invocation, msbuild);

        Assert.True(lease.Attestation.ZeroWarnings);
        Assert.True(lease.Attestation.DeletedPriorOutput);
        Assert.Equal(3, lease.Attestation.RuntimeClosure.Count);
        Assert.DoesNotContain("runtime-dispose", rig.Events);
        Assert.DoesNotContain("environment-dispose", rig.Events);
        Assert.Equal(
        [
            "msbuild-open", "provenance-open", "candidate", "prerequisites", "candidate", "environment",
            "output-read:0", "output-read:1", "output-read:2", "candidate",
            "output-delete", "output-read:3", "output-delete", "output-read:4",
            "output-delete", "output-read:5", "candidate",
            "process", "candidate", "output-read:6", "output-read:7", "output-read:8",
            "candidate", "runtime-open", "candidate",
        ], rig.Events);
        var request = Assert.IsType<EvidenceBuildProcessRequest>(rig.Process.Request);
        Assert.False(request.InheritAmbientEnvironment);
        Assert.True(request.StartSuspended);
        Assert.Equal(0x3U, request.JobLimits.AffinityMask);
        Assert.Equal(1, request.JobLimits.ActiveProcessLimit);
        Assert.True(request.JobLimits.KillOnJobClose);
        Assert.False(request.UseShellExecute);
        Assert.True(request.CreateNoWindow);
        Assert.True(request.HiddenWindow);
        Assert.True(request.BelowNormalPriority);
        Assert.Equal(TimeSpan.FromMinutes(5), request.Deadline);
        Assert.Equal(4 * 1024 * 1024, request.StandardOutputCapBytes);
        Assert.Equal(4 * 1024 * 1024, request.StandardErrorCapBytes);
        Assert.Contains("/noAutoResponse", request.Invocation.Arguments);
        Assert.Equal(13, request.EffectiveEnvironment.Count);

        lease.Dispose();
        lease.Dispose();

        Assert.Equal(1, rig.Events.Count(value => value == "runtime-dispose"));
        Assert.Equal(1, rig.Events.Count(value => value == "provenance-dispose"));
        Assert.Equal(["prior-dispose:2", "prior-dispose:1", "prior-dispose:0", "environment-dispose", "provenance-dispose"], rig.Events.TakeLast(5));
    }

    [Theory]
    [InlineData(2, false)]
    [InlineData(4, true)]
    public void Environment_write_exclusion_break_before_or_during_build_refuses_and_cleans_up(
        int breakOnRevalidation,
        bool processExpected)
    {
        var rig = new Rig();
        rig.Environment.BreakOnRevalidation = breakOnRevalidation;
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.Equal(processExpected, rig.Events.Contains("process"));
        Assert.Equal(1, rig.Events.Count(value => value == "environment-dispose"));
        Assert.DoesNotContain("runtime-open", rig.Events);
    }

    [Fact]
    public void Transferred_environment_exclusion_cleanup_failure_remains_owned_for_retry()
    {
        var rig = new Rig();
        rig.Environment.FailDisposeOnce = true;
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);
        var lease = rig.Driver.RebuildAndAttest(rig.Invocation, msbuild);

        Assert.Throws<InvalidOperationException>(() => lease.Dispose());
        Assert.Equal(1, rig.Events.Count(value => value == "environment-dispose"));
        Assert.Equal(1, rig.Events.Count(value => value == "provenance-dispose"));

        lease.Dispose();
        Assert.Equal(2, rig.Events.Count(value => value == "environment-dispose"));
        Assert.Equal(1, rig.Events.Count(value => value == "provenance-dispose"));
    }

    [Fact]
    public void Build_failure_cleanup_transfers_environment_to_strong_reaper_owner()
    {
        var rig = new Rig();
        rig.Environment.BreakOnRevalidation = 2;
        rig.Environment.FailDisposeOnce = true;
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        var failure = Assert.Throws<AggregateException>(() =>
            rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.Contains(failure.Flatten().InnerExceptions, value =>
            value.Message == "environment exclusion cleanup failed");
        Assert.Equal(1, rig.CleanupOwner.RetainedCount);
        Assert.Equal(1, rig.Environment.DisposeCalls);

        rig.Reaper.RunAll();
        Assert.Equal(0, rig.CleanupOwner.RetainedCount);
        Assert.Equal(2, rig.Environment.DisposeCalls);
    }

    [Fact]
    public void Open_refuses_non_pinned_msbuild_and_closes_lease_once()
    {
        var rig = new Rig();
        rig.MsBuild.Identity = ValidMsBuild() with { Sha256 = new string('0', 64) };

        Assert.Throws<InvalidOperationException>(() => rig.Driver.OpenMsBuildExecutable(rig.Invocation));

        Assert.Equal(["msbuild-open", "msbuild-dispose"], rig.Events);
    }

    [Fact]
    public void Invocation_requires_no_auto_response_and_exact_literal_arguments()
    {
        var rig = new Rig();
        var changed = rig.Invocation with
        {
            Arguments = rig.Invocation.Arguments.Where(value => value != "/noAutoResponse").ToArray(),
        };

        Assert.Throws<InvalidOperationException>(() => rig.Driver.OpenMsBuildExecutable(changed));

        Assert.Empty(rig.Events);
    }

    [Theory]
    [InlineData("sdk-version")]
    [InlineData("ref-version")]
    [InlineData("input-hash")]
    [InlineData("input-ancestor")]
    [InlineData("package-version")]
    [InlineData("package-hash")]
    [InlineData("global-hash")]
    [InlineData("sdk-hash")]
    [InlineData("ref-hash")]
    [InlineData("ref-path")]
    [InlineData("input-path")]
    [InlineData("package-path")]
    [InlineData("package-tree-hash")]
    [InlineData("package-extracted-root")]
    [InlineData("package-entry-retention")]
    [InlineData("package-entry-count")]
    [InlineData("package-entry-identity")]
    [InlineData("input-hash-kind")]
    [InlineData("tree-hash-algorithm")]
    public void Locked_prerequisite_drift_refuses_before_output_deletion(string mutation)
    {
        var rig = new Rig();
        rig.Provenance.PrerequisitesValue = MutatePrerequisites(ValidPrerequisites(), mutation);
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.DoesNotContain("output-delete", rig.Events);
        Assert.DoesNotContain("process", rig.Events);
    }

    [Theory]
    [InlineData(0, "Sdk.targets")]
    [InlineData(1, "Godot.SourceGenerators.dll")]
    public void Extracted_package_target_or_dll_drift_refuses_when_archive_and_assets_are_unchanged(
        int packageIndex,
        string changedEntry)
    {
        var rig = new Rig();
        var valid = ValidPrerequisites();
        var originalPackage = valid.RequiredPackages[packageIndex];
        rig.Provenance.PrerequisitesValue = valid with
        {
            RequiredPackages = valid.RequiredPackages.Select((item, index) => index == packageIndex
                ? item with { ExtractedTreeSha256 = new string((char)('1' + index), 64) }
                : item).ToArray(),
        };
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.Equal(originalPackage.ArchiveSha256, valid.RequiredPackages[packageIndex].ArchiveSha256);
        Assert.Equal(
            EvidenceBuildManifest.Create(Root).RequiredInputs.Single(value => value.Path.EndsWith(@"game\.godot\mono\temp\obj\project.assets.json", StringComparison.Ordinal)).Sha256,
            valid.RequiredInputs.Single(value => value.Path.EndsWith(@"game\.godot\mono\temp\obj\project.assets.json", StringComparison.Ordinal)).Sha256);
        Assert.False(string.IsNullOrWhiteSpace(changedEntry));
        Assert.DoesNotContain("output-delete", rig.Events);
    }

    [Theory]
    [InlineData("ambient-key")]
    [InlineData("nuget")]
    [InlineData("language")]
    [InlineData("system-root")]
    public void Sanitized_environment_refuses_ambient_or_pinned_value_drift(string mutation)
    {
        var rig = new Rig();
        var environment = new Dictionary<string, string>(ValidEnvironment(), StringComparer.Ordinal);
        if (mutation == "ambient-key") environment["DirectoryBuildPropsPath"] = @"C:\host\inject.props";
        if (mutation == "nuget") environment["NUGET_PACKAGES"] = @"C:\Users\Adam\.nuget\packages";
        if (mutation == "language") environment["VSLANG"] = "1041";
        if (mutation == "system-root") environment["WINDIR"] = @"C:\Other";
        rig.Environment.Value = new ReadOnlyDictionary<string, string>(environment);
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.DoesNotContain("output-delete", rig.Events);
    }

    [Fact]
    public void Trusted_os_and_temp_paths_require_exact_canonical_retained_identities()
    {
        var rig = new Rig();
        rig.Provenance.SystemRoot = rig.Provenance.SystemRoot with { CanonicalPath = @"C:\OtherWindows" };
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.DoesNotContain("environment", rig.Events);
        Assert.Equal(1, rig.Events.Count(value => value == "provenance-dispose"));
    }

    [Fact]
    public void Mutable_environment_source_is_frozen_before_process_run()
    {
        var rig = new Rig();
        var mutable = new Dictionary<string, string>(ValidEnvironment(), StringComparer.OrdinalIgnoreCase);
        rig.Environment.Value = mutable;
        rig.Process.BeforeReturn = () => mutable["NUGET_PACKAGES"] = @"C:\hostile";
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        using var lease = rig.Driver.RebuildAndAttest(rig.Invocation, msbuild);

        Assert.Equal(@"C:\repo\.tools\nuget-packages", rig.Process.Request!.EffectiveEnvironment["NUGET_PACKAGES"]);
    }

    [Fact]
    public void Unretained_or_changed_and_restored_input_chain_refuses_before_spawn()
    {
        var rig = new Rig();
        rig.Provenance.InputIdentities[1] = "temporarily-changed";
        rig.Provenance.InputIdentities[2] = "inputs-id";
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.DoesNotContain("process", rig.Events);
        Assert.Equal(1, rig.Events.Count(value => value == "provenance-dispose"));
    }

    [Fact]
    public void Extracted_package_tree_change_and_restore_during_build_is_caught_by_retained_provenance()
    {
        var rig = new Rig();
        rig.Provenance.InputIdentities[3] = "package-tree-changed";
        rig.Provenance.InputIdentities[4] = "inputs-id";
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.Contains("process", rig.Events);
        Assert.DoesNotContain("runtime-open", rig.Events);
    }

    [Fact]
    public void Missing_continuous_repository_and_ancestor_ownership_refuses_before_delete()
    {
        var rig = new Rig();
        rig.Provenance.RetainsChains = false;
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.DoesNotContain("output-delete", rig.Events);
    }

    [Fact]
    public void Build_cancellation_aggregates_provenance_cleanup_failure()
    {
        var rig = new Rig();
        rig.Process.Failure = new OperationCanceledException("cancel build");
        rig.Provenance.ThrowOnDispose = true;
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        var failure = Assert.Throws<AggregateException>(() =>
            rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.Contains(failure.Flatten().InnerExceptions, value => value is OperationCanceledException);
        Assert.Contains(failure.Flatten().InnerExceptions, value => value.Message.Contains("provenance", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("image")]
    [InlineData("exit")]
    [InlineData("timeout")]
    [InlineData("stdout-eof")]
    [InlineData("stderr-eof")]
    [InlineData("stdout-cap")]
    [InlineData("stderr-cap")]
    [InlineData("image-proof")]
    [InlineData("job-before-resume")]
    [InlineData("concurrent-pipes")]
    [InlineData("job-empty")]
    [InlineData("effective-environment")]
    [InlineData("warning-unparsed")]
    [InlineData("warning")]
    public void Process_attribution_bounds_and_warning_failures_refuse(string mutation)
    {
        var rig = new Rig();
        rig.Process.Result = MutateProcess(ValidProcessResult(), mutation);
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.Contains("process", rig.Events);
        Assert.DoesNotContain("runtime-open", rig.Events);
    }

    [Theory]
    [InlineData("prior-missing")]
    [InlineData("delete-still-present")]
    [InlineData("recreated-same-id")]
    [InlineData("recreated-reparse")]
    public void Runtime_output_must_be_absent_before_spawn_then_new_identity_after_exit(string mutation)
    {
        var rig = new Rig();
        if (mutation == "prior-missing") rig.Output.States[0] = EvidenceBuildOutputState.Missing(RuntimePath);
        if (mutation == "delete-still-present") rig.Output.States[3] = PriorOutput(RuntimePath, "old-game-id");
        if (mutation == "recreated-same-id") rig.Output.States[6] = RecreatedOutput(RuntimePath, "old-game-id");
        if (mutation == "recreated-reparse") rig.Output.States[6] = RecreatedOutput(RuntimePath, "game-id") with { IsReparsePoint = true };
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Every_game_replay_and_sim_prior_output_must_exist_and_be_deleted(int closureIndex)
    {
        var rig = new Rig();
        rig.Output.States[closureIndex] = EvidenceBuildOutputState.Missing(
            new[] { RuntimePath, ReplayPath, SimPath }[closureIndex]);
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));
    }

    [Fact]
    public void Prior_output_alias_or_hardlink_identity_is_refused_before_deletion()
    {
        var rig = new Rig();
        rig.Output.States[1] = rig.Output.States[1] with { OpenedHandleIdentity = "old-game-id" };
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.DoesNotContain("output-delete", rig.Events);
    }

    [Fact]
    public void Same_parent_replacement_between_open_and_delete_is_refused_by_retained_identity_disposition()
    {
        var rig = new Rig();
        rig.Output.SwapBeforeDeleteIndex = 1;
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.DoesNotContain("process", rig.Events);
        Assert.Equal(["prior-dispose:2", "prior-dispose:1", "prior-dispose:0", "environment-dispose", "provenance-dispose"], rig.Events.TakeLast(5));
    }

    [Fact]
    public void Wrong_identity_delete_proof_is_refused_before_spawn()
    {
        var rig = new Rig();
        rig.Output.WrongIdentityProofIndex = 0;
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.DoesNotContain("process", rig.Events);
    }

    [Fact]
    public void Partial_delete_failure_aggregates_all_three_prior_lease_cleanup_failures_in_reverse_order()
    {
        var rig = new Rig();
        rig.Output.FailDeleteIndex = 1;
        rig.Output.ThrowDisposeIndices.UnionWith([0, 2]);
        rig.Provenance.ThrowOnDispose = true;
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        var failure = Assert.Throws<AggregateException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.Contains(failure.Flatten().InnerExceptions, value => value.Message == "delete failed:1");
        Assert.Contains(failure.Flatten().InnerExceptions, value => value.Message == "prior dispose failed:2");
        Assert.Contains(failure.Flatten().InnerExceptions, value => value.Message == "prior dispose failed:0");
        Assert.Contains(failure.Flatten().InnerExceptions, value => value.Message == "provenance cleanup failed");
        Assert.Equal(["prior-dispose:2", "prior-dispose:1", "prior-dispose:0", "environment-dispose", "provenance-dispose"], rig.Events.TakeLast(5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Every_game_replay_and_sim_recreation_must_have_a_new_identity(int closureIndex)
    {
        var rig = new Rig();
        var staleIds = new[] { "old-game-id", "old-replay-id", "old-sim-id" };
        rig.Output.States[6 + closureIndex] = rig.Output.States[6 + closureIndex] with
        {
            OpenedHandleIdentity = staleIds[closureIndex],
        };
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Recreated_output_identity_set_refuses_cross_slot_prior_permutation(bool threeCycle)
    {
        var rig = new Rig();
        rig.Output.States[6] = rig.Output.States[6] with { OpenedHandleIdentity = "old-replay-id" };
        rig.Output.States[7] = rig.Output.States[7] with
        {
            OpenedHandleIdentity = threeCycle ? "old-sim-id" : "old-game-id",
        };
        rig.Output.States[8] = rig.Output.States[8] with
        {
            OpenedHandleIdentity = threeCycle ? "old-game-id" : "sim-id",
        };
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.DoesNotContain("runtime-open", rig.Events);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(3, true)]
    public void Candidate_drift_before_spawn_or_after_build_refuses(int readIndex, bool processRan)
    {
        var rig = new Rig();
        rig.Provenance.Values[readIndex] = ValidCandidate() with { Commit = new string('b', 40) };
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.Equal(processRan, rig.Events.Contains("process"));
        Assert.DoesNotContain("runtime-open", rig.Events);
    }

    [Fact]
    public void Runtime_closure_mismatch_disposes_every_retained_handle_before_refusal()
    {
        var rig = new Rig();
        rig.Runtime.Closure[2] = rig.Runtime.Closure[2] with { Path = @"C:\repo\wrong.dll" };
        using var msbuild = rig.Driver.OpenMsBuildExecutable(rig.Invocation);

        Assert.Throws<InvalidOperationException>(() => rig.Driver.RebuildAndAttest(rig.Invocation, msbuild));

        Assert.Equal(1, rig.Events.Count(value => value == "runtime-dispose"));
    }

    [Fact]
    public void Runtime_factory_hashes_reads_mvid_and_retains_exact_three_handles_and_ancestors()
    {
        var files = new FakeRetainedFiles();
        var ancestors = new FakeAncestors(files.Events);
        var paths = RuntimeFixturePaths();
        files.Add(paths[0], File.ReadAllBytes(typeof(Win32EvidenceBuildDriverTests).Assembly.Location), 101, '1');
        files.Add(paths[1], File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Rounds.Replay.dll")), 102, '2');
        files.Add(paths[2], File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Rounds.Sim.dll")), 103, '3');

        var lease = new Win32RuntimeAssemblyFactory(files, ancestors, files)
            .OpenRecreatedClosure(paths, ["old-1", "old-2", "old-3"]);

        Assert.Equal(paths, lease.RuntimeClosure.Select(value => value.Path));
        Assert.All(lease.RuntimeClosure, value =>
        {
            Assert.Equal(64, value.Sha256.Length);
            Assert.Equal(32, value.Mvid.Length);
        });
        Assert.True(lease.ReparseFreeAncestorChains);
        Assert.DoesNotContain(files.Events, value => value.StartsWith("close:", StringComparison.Ordinal));

        lease.Dispose();

        Assert.Equal(["close:103", "close:102", "close:101", "ancestors-dispose"], files.Events.TakeLast(4));
    }

    [Fact]
    public void Runtime_factory_wrong_assembly_name_closes_all_acquired_ownership()
    {
        var files = new FakeRetainedFiles();
        var ancestors = new FakeAncestors(files.Events);
        var paths = RuntimeFixturePaths();
        var tests = File.ReadAllBytes(typeof(Win32EvidenceBuildDriverTests).Assembly.Location);
        files.Add(paths[0], tests, 101, '1');
        files.Add(paths[1], tests, 102, '2');

        Assert.Throws<InvalidOperationException>(() =>
            new Win32RuntimeAssemblyFactory(files, ancestors, files).OpenRecreatedClosure(paths, ["old-1", "old-2", "old-3"]));

        Assert.Equal(["close:102", "close:101", "ancestors-dispose"], files.Events.TakeLast(3));
    }

    [Fact]
    public void Runtime_factory_snapshot_drift_closes_refused_handle_and_ancestor_lease()
    {
        var files = new FakeRetainedFiles { DriftAfterRead = true };
        var ancestors = new FakeAncestors(files.Events);
        var paths = RuntimeFixturePaths();
        files.Add(paths[0], File.ReadAllBytes(typeof(Win32EvidenceBuildDriverTests).Assembly.Location), 101, '1');

        Assert.Throws<InvalidOperationException>(() =>
            new Win32RuntimeAssemblyFactory(files, ancestors, files).OpenRecreatedClosure(paths, ["old-1", "old-2", "old-3"]));

        Assert.Equal(["close:101", "ancestors-dispose"], files.Events.TakeLast(2));
    }

    [Fact]
    public void Runtime_factory_refuses_unproven_ancestor_chains_before_file_open()
    {
        var files = new FakeRetainedFiles();
        var ancestors = new FakeAncestors(files.Events) { Exact = false };

        Assert.Throws<InvalidOperationException>(() =>
            new Win32RuntimeAssemblyFactory(files, ancestors, files)
                .OpenRecreatedClosure(RuntimeFixturePaths(), ["old-1", "old-2", "old-3"]));

        Assert.DoesNotContain(files.Events, value => value.StartsWith("open:", StringComparison.Ordinal));
        Assert.Contains("ancestors-dispose", files.Events);
    }

    [Theory]
    [InlineData(101)]
    [InlineData(102)]
    [InlineData(103)]
    public void Runtime_factory_refuses_alternate_stream_on_every_closure_member(int adsHandle)
    {
        var files = new FakeRetainedFiles { AlternateStreamHandle = adsHandle };
        var ancestors = new FakeAncestors(files.Events);
        var paths = RuntimeFixturePaths();
        files.Add(paths[0], File.ReadAllBytes(typeof(Win32EvidenceBuildDriverTests).Assembly.Location), 101, '1');
        files.Add(paths[1], File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Rounds.Replay.dll")), 102, '2');
        files.Add(paths[2], File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Rounds.Sim.dll")), 103, '3');

        Assert.Throws<InvalidOperationException>(() =>
            new Win32RuntimeAssemblyFactory(files, ancestors, files)
                .OpenRecreatedClosure(paths, ["old-1", "old-2", "old-3"]));

        Assert.Contains($"close:{adsHandle}", files.Events);
        Assert.Contains("ancestors-dispose", files.Events);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Runtime_factory_refuses_any_cross_slot_prior_identity_permutation(bool threeCycle)
    {
        var files = new FakeRetainedFiles();
        var ancestors = new FakeAncestors(files.Events);
        var paths = RuntimeFixturePaths();
        files.Add(paths[0], File.ReadAllBytes(typeof(Win32EvidenceBuildDriverTests).Assembly.Location), 101, '1');
        files.Add(paths[1], File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Rounds.Replay.dll")), 102, '2');
        files.Add(paths[2], File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Rounds.Sim.dll")), 103, '3');
        var identities = new[] { RetainedIdentity('1'), RetainedIdentity('2'), RetainedIdentity('3') };
        var prior = threeCycle
            ? new[] { identities[1], identities[2], identities[0] }
            : new[] { identities[1], identities[0], "unrelated-prior" };

        Assert.Throws<InvalidOperationException>(() =>
            new Win32RuntimeAssemblyFactory(files, ancestors, files).OpenRecreatedClosure(paths, prior));

        Assert.Contains("close:101", files.Events);
        Assert.Contains("ancestors-dispose", files.Events);
    }

    private sealed class Rig
    {
        internal List<string> Events { get; } = [];
        internal EvidenceBuildInvocation Invocation { get; } = ValidInvocation();
        internal FakeMsBuild MsBuild { get; }
        internal FakeProvenance Provenance { get; }
        internal FakeEnvironment Environment { get; }
        internal FakeOutput Output { get; }
        internal FakeProcess Process { get; }
        internal FakeRuntime Runtime { get; }
        internal FakeReaperScheduler Reaper { get; }
        internal Win32EvidenceBuildEnvironmentCleanupOwner CleanupOwner { get; }
        internal Win32EvidenceBuildDriver Driver { get; }

        internal Rig()
        {
            MsBuild = new FakeMsBuild(Events);
            Provenance = new FakeProvenance(Events);
            Environment = new FakeEnvironment(Events);
            Output = new FakeOutput(Events);
            Process = new FakeProcess(Events);
            Runtime = new FakeRuntime(Events);
            Reaper = new FakeReaperScheduler();
            CleanupOwner = new Win32EvidenceBuildEnvironmentCleanupOwner(Reaper);
            Driver = new Win32EvidenceBuildDriver(
                MsBuild, Provenance, Environment, Output, Process, Runtime, CleanupOwner);
        }
    }

    private sealed class FakeMsBuild(List<string> events) : IEvidenceMsBuildExecutableFactory
    {
        internal EvidenceOpenedExecutableIdentity Identity { get; set; } = ValidMsBuild();
        public IEvidenceExecutableLease OpenPinnedMsBuild()
        {
            events.Add("msbuild-open");
            return new FakeExecutableLease(events, Identity);
        }
    }

    private sealed class FakeExecutableLease(List<string> events, EvidenceOpenedExecutableIdentity identity) :
        IEvidenceExecutableLease
    {
        private bool _disposed;
        public EvidenceOpenedExecutableIdentity Identity { get; } = identity;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            events.Add("msbuild-dispose");
        }
    }

    private sealed class FakeProvenance(List<string> events) :
        IEvidenceBuildProvenanceFactory,
        IEvidenceBuildProvenanceLease
    {
        internal EvidenceCandidateIdentity[] Values { get; } =
            Enumerable.Repeat(ValidCandidate(), 6).ToArray();
        internal EvidenceBuildPrerequisiteAttestation PrerequisitesValue { get; set; } = ValidPrerequisites();
        internal bool RetainsChains { get; set; } = true;
        internal bool ThrowOnDispose { get; set; }
        internal string[] InputIdentities { get; } = Enumerable.Repeat("inputs-id", 6).ToArray();
        private int _read;

        public EvidenceCandidateIdentity Candidate
        {
            get
            {
                events.Add("candidate");
                return Values[0];
            }
        }
        public EvidenceBuildPrerequisiteAttestation Prerequisites
        {
            get
            {
                events.Add("prerequisites");
                return PrerequisitesValue;
            }
        }
        public EvidenceTrustedDirectoryIdentity SystemRoot { get; set; } = Trusted(@"C:\Windows", "windows-id");
        public EvidenceTrustedDirectoryIdentity TemporaryDirectory { get; set; } = Trusted(@"C:\Temp", "temp-id");
        public bool RetainsExactRepositoryInputAndOutputAncestorChains => RetainsChains;
        public IEvidenceBuildProvenanceLease OpenRetained(string exactRepositoryRoot)
        {
            events.Add("provenance-open");
            Assert.Equal(Root, exactRepositoryRoot);
            return this;
        }
        public EvidenceBuildProvenanceSnapshot Revalidate()
        {
            events.Add("candidate");
            var index = System.Math.Min(_read, Values.Length - 1);
            _read++;
            return new EvidenceBuildProvenanceSnapshot(Values[index], InputIdentities[index]);
        }
        public void Dispose()
        {
            events.Add("provenance-dispose");
            if (ThrowOnDispose) throw new InvalidOperationException("provenance cleanup failed");
        }
    }

    private sealed class FakeEnvironment(List<string> events) :
        IEvidenceBuildEnvironmentFactory,
        IEvidenceBuildEnvironmentLease
    {
        internal IReadOnlyDictionary<string, string> Value { get; set; } = ValidEnvironment();
        internal int BreakOnRevalidation { get; set; } = int.MaxValue;
        internal bool FailDisposeOnce { get; set; }
        internal int DisposeCalls { get; private set; }
        private int RevalidationCount { get; set; }
        public IReadOnlyDictionary<string, string> Environment => Value;
        public IEvidenceBuildEnvironmentLease CreateSanitized(
            EvidenceBuildInvocation required,
            EvidenceTrustedDirectoryIdentity systemRoot,
            EvidenceTrustedDirectoryIdentity temporaryDirectory)
        {
            events.Add("environment");
            Assert.Equal(@"C:\Windows", systemRoot.CanonicalPath);
            Assert.Equal(@"C:\Temp", temporaryDirectory.CanonicalPath);
            return this;
        }
        public EvidenceBuildEnvironmentRevalidation Revalidate()
        {
            RevalidationCount++;
            var active = RevalidationCount < BreakOnRevalidation;
            return new("dotnet-home-id", "msbuild-user-id", true, true, active, active, active);
        }
        public void Dispose()
        {
            DisposeCalls++;
            events.Add("environment-dispose");
            if (FailDisposeOnce)
            {
                FailDisposeOnce = false;
                throw new InvalidOperationException("environment exclusion cleanup failed");
            }
        }
    }

    private sealed class FakeReaperScheduler : IEvidenceBuildCleanupReaperScheduler
    {
        private readonly Queue<Action> _actions = [];

        public void Schedule(Action action) => _actions.Enqueue(action);

        public void Backoff(TimeSpan delay) => Assert.Equal(TimeSpan.FromSeconds(1), delay);

        internal void RunAll()
        {
            while (_actions.TryDequeue(out var action)) action();
        }
    }

    private sealed class FakeOutput(List<string> events) : IEvidenceBuildOutputApi
    {
        private List<string> Events { get; } = events;
        internal EvidenceBuildOutputState[] States { get; } =
        [
            PriorOutput(RuntimePath, "old-game-id"),
            PriorOutput(ReplayPath, "old-replay-id"),
            PriorOutput(SimPath, "old-sim-id"),
            EvidenceBuildOutputState.Missing(RuntimePath),
            EvidenceBuildOutputState.Missing(ReplayPath),
            EvidenceBuildOutputState.Missing(SimPath),
            RecreatedOutput(RuntimePath, "game-id"),
            RecreatedOutput(ReplayPath, "replay-id"),
            RecreatedOutput(SimPath, "sim-id"),
        ];
        private int _open;
        private int _recreated;
        internal int FailDeleteIndex { get; set; } = -1;
        internal int WrongIdentityProofIndex { get; set; } = -1;
        internal int SwapBeforeDeleteIndex { get; set; } = -1;
        internal HashSet<int> ThrowDisposeIndices { get; } = [];

        public IEvidencePriorOutputLease OpenPrior(string exactRuntimeAssemblyPath)
        {
            var index = _open++;
            Events.Add($"output-read:{index}");
            Assert.Equal(States[index].Path, exactRuntimeAssemblyPath);
            return new FakePriorOutputLease(this, index, States[index]);
        }

        public EvidenceBuildOutputState ReadRecreated(string exactRuntimeAssemblyPath)
        {
            var stateIndex = 6 + _recreated++;
            Events.Add($"output-read:{stateIndex}");
            Assert.Equal(States[stateIndex].Path, exactRuntimeAssemblyPath);
            return States[stateIndex];
        }

        private sealed class FakePriorOutputLease(
            FakeOutput owner,
            int index,
            EvidenceBuildOutputState state) : IEvidencePriorOutputLease
        {
            private bool _disposed;
            public EvidenceBuildOutputState State { get; } = state;
            public bool RetainsExactFileAndAncestorIdentity => true;
            public EvidencePriorOutputDeletionProof DeleteRetainedIdentityAndProveAbsent()
            {
                owner.Events.Add("output-delete");
                owner.Events.Add($"output-read:{3 + index}");
                if (owner.FailDeleteIndex == index) throw new InvalidOperationException($"delete failed:{index}");
                var absent = owner.States[3 + index];
                return new EvidencePriorOutputDeletionProof(
                    State.Path,
                    owner.WrongIdentityProofIndex == index ? "replacement-id" : State.OpenedHandleIdentity,
                    ExactRetainedIdentityDisposition: owner.SwapBeforeDeleteIndex != index,
                    ExactPathAbsent: !absent.Exists,
                    AncestorIdentityStillRetained: true);
            }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                owner.Events.Add($"prior-dispose:{index}");
                if (owner.ThrowDisposeIndices.Contains(index))
                {
                    throw new InvalidOperationException($"prior dispose failed:{index}");
                }
            }
        }
    }

    private sealed class FakeProcess(List<string> events) : IEvidenceBuildProcessRunner
    {
        internal EvidenceBuildProcessRequest? Request { get; private set; }
        internal EvidenceBuildProcessResult Result { get; set; } = ValidProcessResult();
        internal Action? BeforeReturn { get; set; }
        internal Exception? Failure { get; set; }
        public EvidenceBuildProcessResult Run(
            EvidenceBuildProcessRequest request,
            IEvidenceExecutableLease retainedExecutable)
        {
            events.Add("process");
            Request = request;
            Assert.Equal(ValidMsBuild(), retainedExecutable.Identity);
            BeforeReturn?.Invoke();
            if (Failure is not null) throw Failure;
            return Result;
        }
    }

    private sealed class FakeRuntime(List<string> events) : IEvidenceRuntimeAssemblyFactory
    {
        internal EvidenceRuntimeAssemblyIdentity[] Closure { get; } = ValidRuntimeClosure();
        public IEvidenceRuntimeAssemblyLease OpenRecreatedClosure(
            IReadOnlyList<string> exactRuntimeAssemblyPaths,
            IReadOnlyList<string> priorOpenedHandleIdentities)
        {
            events.Add("runtime-open");
            Assert.Equal(["old-game-id", "old-replay-id", "old-sim-id"], priorOpenedHandleIdentities);
            Assert.Equal(
                [RuntimePath, Path.Combine(Path.GetDirectoryName(RuntimePath)!, "Rounds.Replay.dll"), Path.Combine(Path.GetDirectoryName(RuntimePath)!, "Rounds.Sim.dll")],
                exactRuntimeAssemblyPaths);
            return new FakeRuntimeLease(events, Closure);
        }
    }

    private sealed class FakeRuntimeLease(
        List<string> events,
        IReadOnlyList<EvidenceRuntimeAssemblyIdentity> closure) : IEvidenceRuntimeAssemblyLease
    {
        private bool _disposed;
        public EvidenceRuntimeAssemblyIdentity Identity => RuntimeClosure[0];
        public IReadOnlyList<EvidenceRuntimeAssemblyIdentity> RuntimeClosure { get; } = closure;
        public bool ReparseFreeAncestorChains => true;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            events.Add("runtime-dispose");
        }
    }

    private sealed class FakeAncestors(List<string> events) :
        IEvidenceRuntimeAncestorFactory,
        IEvidenceRuntimeAncestorLease
    {
        internal bool Exact { get; set; } = true;
        public bool ExactReparseFreeChains => Exact;
        public IEvidenceRuntimeAncestorLease OpenRetainedChains(IReadOnlyList<string> exactAssemblyPaths)
        {
            events.Add("ancestors-open");
            return this;
        }
        public void Dispose() => events.Add("ancestors-dispose");
    }

    private sealed class FakeRetainedFiles : IWin32RetainedFileApi, IWin32RuntimeStreamApi
    {
        private sealed record Entry(string Path, byte[] Bytes, nint Handle, string FileId);
        private readonly Dictionary<string, Entry> _byPath = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<nint, Entry> _byHandle = [];
        private readonly Dictionary<nint, int> _snapshotReads = [];
        internal List<string> Events { get; } = [];
        internal bool DriftAfterRead { get; set; }
        internal nint AlternateStreamHandle { get; set; }

        internal void Add(string path, byte[] bytes, nint handle, char fileId)
        {
            var entry = new Entry(path, bytes, handle, new string(fileId, 32));
            _byPath[path] = entry;
            _byHandle[handle] = entry;
        }

        public nint OpenReadNoReplace(
            string normalizedAbsolutePath,
            uint desiredAccess,
            uint shareMode,
            uint creationDisposition,
            uint flagsAndAttributes)
        {
            Events.Add($"open:{Path.GetFileName(normalizedAbsolutePath)}");
            Assert.Equal(Win32EvidenceConstants.GenericRead, desiredAccess);
            Assert.Equal(Win32EvidenceConstants.FileShareRead, shareMode);
            return _byPath.TryGetValue(normalizedAbsolutePath, out var entry) ? entry.Handle : 0;
        }

        public Win32RetainedFileSnapshot ReadSnapshot(nint handle)
        {
            var entry = _byHandle[handle];
            var read = _snapshotReads.GetValueOrDefault(handle);
            _snapshotReads[handle] = read + 1;
            return new Win32RetainedFileSnapshot(
                entry.Path, 7, entry.FileId, entry.Bytes.Length,
                DriftAfterRead && read > 0 ? 12 : 11,
                0x80, 0, 1, false, false);
        }

        public Stream OpenReadStream(nint handle) => new MemoryStream(_byHandle[handle].Bytes, writable: false);
        public IReadOnlyList<Win32PublishedStreamEntry> EnumerateStreams(nint retainedFileHandle) =>
            retainedFileHandle == AlternateStreamHandle
                ? [new Win32PublishedStreamEntry("::$DATA", _byHandle[retainedFileHandle].Bytes.Length),
                    new Win32PublishedStreamEntry(":hostile:$DATA", 1)]
                : [new Win32PublishedStreamEntry("::$DATA", _byHandle[retainedFileHandle].Bytes.Length)];
        public Win32RetainedFileVersion ReadVersion(nint retainedHandle, string normalizedFinalPath) =>
            throw new NotSupportedException();
        public bool CloseKernelHandle(nint handle)
        {
            Events.Add($"close:{handle}");
            return true;
        }
    }

    private static EvidenceBuildInvocation ValidInvocation() => new(
        BaseProjectileEvidenceLaunchPlanner.MsBuildPath,
        Root,
        [
            @"game\Rounds.Game.csproj", "/noAutoResponse", "/t:Rebuild", "/p:Configuration=Debug",
            "/p:Restore=false", "/p:UseSharedCompilation=false", "/p:BuildProjectReferences=true",
            "/m:1", "/nr:false", "/v:minimal", "/warnaserror",
        ],
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DOTNET_PROCESSOR_COUNT"] = "2",
            ["MSBUILDDISABLENODEREUSE"] = "1",
            ["MSBuildEnableWorkloadResolver"] = "false",
            ["MSBuildSDKsPath"] = @"C:\repo\.tools\dotnet\sdk\8.0.423\Sdks",
        }));

    private static IReadOnlyDictionary<string, string> ValidEnvironment() =>
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SystemRoot"] = @"C:\Windows", ["WINDIR"] = @"C:\Windows",
            ["TEMP"] = @"C:\Temp", ["TMP"] = @"C:\Temp",
            ["DOTNET_PROCESSOR_COUNT"] = "2", ["MSBUILDDISABLENODEREUSE"] = "1",
            ["MSBuildEnableWorkloadResolver"] = "false",
            ["MSBuildSDKsPath"] = @"C:\repo\.tools\dotnet\sdk\8.0.423\Sdks",
            ["DOTNET_CLI_UI_LANGUAGE"] = "en-US", ["VSLANG"] = "1033",
            ["NUGET_PACKAGES"] = @"C:\repo\.tools\nuget-packages",
            ["DOTNET_CLI_HOME"] = @"C:\repo\.tools\dotnet-home",
            ["MSBuildUserExtensionsPath"] = @"C:\repo\.tools\empty\msbuild-user",
        });

    private static EvidenceBuildPrerequisiteAttestation ValidPrerequisites() => EvidenceBuildManifest.Create(Root);

    private static EvidenceBuildPrerequisiteAttestation MutatePrerequisites(
        EvidenceBuildPrerequisiteAttestation value,
        string mutation) => mutation switch
        {
            "sdk-version" => value with { GlobalJsonSdkVersion = "8.0.422" },
            "ref-version" => value with { ReferencePackVersion = "8.0.28" },
            "input-hash" => value with { RequiredInputs = value.RequiredInputs.Select((item, index) => index == 0 ? item with { Sha256 = "bad" } : item).ToArray() },
            "input-ancestor" => value with { RequiredInputs = value.RequiredInputs.Select((item, index) => index == 0 ? item with { ReparseFreeAncestors = false } : item).ToArray() },
            "package-version" => value with { RequiredPackages = value.RequiredPackages.Select((item, index) => index == 0 ? item with { Version = "4.7.0" } : item).ToArray() },
            "package-hash" => value with { RequiredPackages = value.RequiredPackages.Select((item, index) => index == 0 ? item with { ArchiveSha256 = "bad" } : item).ToArray() },
            "global-hash" => value with { GlobalJsonSha256 = new string('0', 64) },
            "sdk-hash" => value with { SdkContentSha256 = new string('0', 64) },
            "ref-hash" => value with { ReferencePackContentSha256 = new string('0', 64) },
            "ref-path" => value with { ReferencePackDirectory = @"C:\repo\.tools\dotnet\packs\Microsoft.NETCore.App.Ref\8.0.28\ref\net8.0" },
            "input-path" => value with { RequiredInputs = value.RequiredInputs.Select((item, index) => index == 0 ? item with { Path = @"C:\repo\decoy\global.json" } : item).ToArray() },
            "package-path" => value with { RequiredPackages = value.RequiredPackages.Select((item, index) => index == 0 ? item with { ArchivePath = @"C:\host-cache\godot.net.sdk.4.7.1.nupkg" } : item).ToArray() },
            "package-tree-hash" => value with { RequiredPackages = value.RequiredPackages.Select((item, index) => index == 0 ? item with { ExtractedTreeSha256 = new string('0', 64) } : item).ToArray() },
            "package-extracted-root" => value with { RequiredPackages = value.RequiredPackages.Select((item, index) => index == 0 ? item with { ExtractedRoot = @"C:\host-cache\godot.net.sdk\4.7.1" } : item).ToArray() },
            "package-entry-retention" => value with { RequiredPackages = value.RequiredPackages.Select((item, index) => index == 0 ? item with { RetainedExtractedDirectoryAndEntries = false } : item).ToArray() },
            "package-entry-count" => value with { RequiredPackages = value.RequiredPackages.Select((item, index) => index == 0 ? item with { ExtractedEntryCount = item.ExtractedEntryCount + 1 } : item).ToArray() },
            "package-entry-identity" => value with { RequiredPackages = value.RequiredPackages.Select((item, index) => index == 0 ? item with { RetainedExtractedEntriesIdentity = string.Empty } : item).ToArray() },
            "input-hash-kind" => value with { RequiredInputs = value.RequiredInputs.Select((item, index) => index == 3 ? item with { HashKind = EvidenceBuildContentHashKind.RawBytesSha256 } : item).ToArray() },
            "tree-hash-algorithm" => value with { ReferencePackTreeHashAlgorithm = "different" },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };

    private static EvidenceCandidateIdentity ValidCandidate() =>
        new(Root, new string('a', 40), true, true, "repo-id");

    private static EvidenceOpenedExecutableIdentity ValidMsBuild() => new(
        BaseProjectileEvidenceLaunchPlanner.MsBuildPath, true, true, false, "msbuild-id",
        BaseProjectileEvidenceLaunchPlanner.MsBuildSha256,
        BaseProjectileEvidenceLaunchPlanner.MsBuildFileVersion,
        BaseProjectileEvidenceLaunchPlanner.MsBuildProductVersion);

    private static EvidenceBuildOutputState PriorOutput(string path, string identity) =>
        new(path, true, true, false, true, identity, 100, 10, false, 1, false);

    private static EvidenceBuildOutputState RecreatedOutput(string path, string identity) =>
        new(path, true, true, false, true, identity, 200, 20, false, 1, false);

    private static EvidenceTrustedDirectoryIdentity Trusted(string path, string identity) =>
        new(path, path, true, true, true, identity);

    private static EvidenceRuntimeAssemblyIdentity[] ValidRuntimeClosure()
    {
        var directory = Path.GetDirectoryName(RuntimePath)!;
        return
        [
            RuntimeIdentity(RuntimePath, "game-id", 'a', 'b'),
            RuntimeIdentity(Path.Combine(directory, "Rounds.Replay.dll"), "replay-id", 'c', 'd'),
            RuntimeIdentity(Path.Combine(directory, "Rounds.Sim.dll"), "sim-id", 'e', 'f'),
        ];
    }

    private static string[] RuntimeFixturePaths() =>
    [
        @"C:\output\Rounds.Sim.Tests.dll",
        @"C:\output\Rounds.Replay.dll",
        @"C:\output\Rounds.Sim.dll",
    ];

    private static EvidenceRuntimeAssemblyIdentity RuntimeIdentity(
        string path, string identity, char hash, char mvid) =>
        new(path, true, true, false, true, identity, new string(hash, 64), new string(mvid, 32));

    private static string RetainedIdentity(char fileId) =>
        $"volume:{7UL:x16}:file:{new string(fileId, 32)}";

    private static EvidenceBuildProcessResult ValidProcessResult() => new(
        ValidMsBuild(), ValidEnvironment(), 0, false, true, true, false, false,
        true, true, true, true, true, 0, [], []);

    private static EvidenceBuildProcessResult MutateProcess(EvidenceBuildProcessResult value, string mutation) =>
        mutation switch
        {
            "image" => value with { ProcessImage = value.ProcessImage with { OpenedHandleIdentity = "other" } },
            "exit" => value with { ExitCode = 1 },
            "timeout" => value with { TimedOut = true },
            "stdout-eof" => value with { StandardOutputReachedEof = false },
            "stderr-eof" => value with { StandardErrorReachedEof = false },
            "stdout-cap" => value with { StandardOutputExceededCap = true },
            "stderr-cap" => value with { StandardErrorExceededCap = true },
            "image-proof" => value with { ProcessImageMatchedBeforeResume = false },
            "job-before-resume" => value with { AssignedToJobBeforeResume = false },
            "concurrent-pipes" => value with { PipesDrainedConcurrently = false },
            "job-empty" => value with { JobEmpty = false },
            "effective-environment" => value with
            {
                EffectiveEnvironment = new ReadOnlyDictionary<string, string>(
                    new Dictionary<string, string>(value.EffectiveEnvironment, StringComparer.Ordinal)
                    {
                        ["DirectoryBuildPropsPath"] = @"C:\host\inject.props",
                    }),
            },
            "warning-unparsed" => value with { WarningCountParsed = false },
            "warning" => value with { WarningCount = 1 },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };
}
