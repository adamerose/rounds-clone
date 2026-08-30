using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class StartupRouteTests
{
    [Fact]
    public void OrdinaryAndReplayArgumentsKeepTheirExistingRoutes()
    {
        var ordinary = StartupRoute.Parse(Array.Empty<string>(), allowDebugEvidence: false);
        var replay = StartupRoute.Parse(
            new[] { "--replay", "evidence.rounds-replay.json" },
            allowDebugEvidence: false);

        Assert.Equal(StartupMode.Match, ordinary.Mode);
        Assert.Null(ordinary.ReplayPath);
        Assert.Null(ordinary.DebugEvidenceOutputPath);
        Assert.Null(ordinary.DebugAgentPlaytestOutputRoot);
        Assert.True(ordinary.RunsContinuousPhysics);
        Assert.Equal(StartupMode.Replay, replay.Mode);
        Assert.Equal("evidence.rounds-replay.json", replay.ReplayPath);
        Assert.Null(replay.DebugEvidenceOutputPath);
        Assert.Null(replay.DebugAgentPlaytestOutputRoot);
        Assert.True(replay.RunsContinuousPhysics);
    }

    [Fact]
    public void AgentPlaytestArgumentIsDebugOnlyAbsoluteAbsentAndMutuallyExclusive()
    {
        var root = Path.Combine(Path.GetTempPath(), "rounds-agent-playtest-route-" + Guid.NewGuid().ToString("N"));
        var arguments = new[] { StartupRoute.DebugAgentPlaytestArgument, root };

        var route = StartupRoute.Parse(arguments, allowDebugEvidence: true);

        Assert.Equal(StartupMode.DebugAgentPlaytest, route.Mode);
        Assert.Equal(Path.GetFullPath(root), route.DebugAgentPlaytestOutputRoot);
        Assert.Null(route.ReplayPath);
        Assert.Null(route.DebugEvidenceOutputPath);
        Assert.False(route.RunsContinuousPhysics);
        Assert.Throws<ArgumentException>(() => StartupRoute.Parse(arguments, allowDebugEvidence: false));
        Assert.Throws<ArgumentException>(() => StartupRoute.Parse(
            new[] { StartupRoute.DebugAgentPlaytestArgument, "relative" },
            allowDebugEvidence: true));
        Assert.Throws<ArgumentException>(() => StartupRoute.Parse(
            new[] { "--replay", "x", StartupRoute.DebugAgentPlaytestArgument, root },
            allowDebugEvidence: true));

        Directory.CreateDirectory(root);
        try
        {
            Assert.Throws<ArgumentException>(() => StartupRoute.Parse(arguments, allowDebugEvidence: true));
        }
        finally
        {
            Directory.Delete(root);
        }
        Assert.False(Directory.Exists(root));
    }

    [Fact]
    public void AgentPlaytestRootNormalizationRequiresExistingNonReparseParentAndRejectsTrailingSeparators()
    {
        var parent = Path.Combine(Path.GetTempPath(), "rounds-agent-root-shapes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(parent);
        var normalized = Path.Combine(parent, "child");
        var equivalent = Path.Combine(parent, "unused", "..", "child");
        var missingParentChild = Path.Combine(parent, "missing", "nested-child");
        var parentFile = Path.Combine(parent, "not-a-directory");
        File.WriteAllText(parentFile, "file parent");
        var nonDirectoryParentChild = Path.Combine(parentFile, "child");
        var trailing = normalized + Path.DirectorySeparatorChar;
        var realParent = Path.Combine(parent, "real-parent");
        var reparseParent = Path.Combine(parent, "reparse-parent");
        Directory.CreateDirectory(realParent);
        Directory.CreateSymbolicLink(reparseParent, realParent);
        try
        {
            var route = StartupRoute.Parse(
                new[] { StartupRoute.DebugAgentPlaytestArgument, equivalent },
                allowDebugEvidence: true);
            Assert.Equal(Path.GetFullPath(normalized), route.DebugAgentPlaytestOutputRoot);

            Assert.Throws<ArgumentException>(() => StartupRoute.Parse(
                new[] { StartupRoute.DebugAgentPlaytestArgument, missingParentChild },
                allowDebugEvidence: true));
            Assert.Throws<ArgumentException>(() => AgentPlaytestArtifactOwner.Create(missingParentChild));
            Assert.False(Directory.Exists(Path.Combine(parent, "missing")));

            Assert.Throws<ArgumentException>(() => StartupRoute.Parse(
                new[] { StartupRoute.DebugAgentPlaytestArgument, nonDirectoryParentChild },
                allowDebugEvidence: true));
            Assert.Throws<ArgumentException>(() => AgentPlaytestArtifactOwner.Create(nonDirectoryParentChild));

            Assert.Throws<ArgumentException>(() => StartupRoute.Parse(
                new[] { StartupRoute.DebugAgentPlaytestArgument, trailing },
                allowDebugEvidence: true));
            Assert.Throws<ArgumentException>(() => AgentPlaytestArtifactOwner.Create(trailing));

            var reparseChild = Path.Combine(reparseParent, "child");
            Assert.Throws<ArgumentException>(() => StartupRoute.Parse(
                new[] { StartupRoute.DebugAgentPlaytestArgument, reparseChild },
                allowDebugEvidence: true));
            Assert.Throws<ArgumentException>(() => AgentPlaytestArtifactOwner.Create(reparseChild));

            var owner = AgentPlaytestArtifactOwner.Create(equivalent);
            Assert.Equal(Path.GetFullPath(normalized), owner.Root);
            owner.CleanupFailedRun();
            owner.Dispose();
        }
        finally
        {
            if (Directory.Exists(reparseParent))
            {
                Directory.Delete(reparseParent);
            }
            Directory.Delete(parent, recursive: true);
        }
    }

    [Fact]
    public void EvidenceArgumentIsAvailableOnlyToDebugRouting()
    {
        var outputPath = Path.Combine(Path.GetTempPath(), "rounds-boundary-evidence.png");
        var arguments = new[] { StartupRoute.DebugIncompleteFidelityEvidenceArgument, outputPath };

        var debug = StartupRoute.Parse(arguments, allowDebugEvidence: true);

        Assert.Equal(StartupMode.DebugIncompleteFidelityEvidence, debug.Mode);
        Assert.Null(debug.ReplayPath);
        Assert.Equal(outputPath, debug.DebugEvidenceOutputPath);
        Assert.False(debug.RunsContinuousPhysics);
        Assert.Throws<ArgumentException>(() => StartupRoute.Parse(arguments, allowDebugEvidence: false));
        Assert.Throws<ArgumentException>(() => StartupRoute.Parse(
            new[] { StartupRoute.DebugIncompleteFidelityEvidenceArgument },
            allowDebugEvidence: true));
        Assert.Throws<ArgumentException>(() => StartupRoute.Parse(
            new[] { StartupRoute.DebugIncompleteFidelityEvidenceArgument, "relative.png" },
            allowDebugEvidence: true));
        Assert.Throws<ArgumentException>(() => StartupRoute.Parse(
            new[] { StartupRoute.DebugIncompleteFidelityEvidenceArgument, Path.ChangeExtension(outputPath, ".jpg") },
            allowDebugEvidence: true));
    }

    [Fact]
    public void BaseProjectileEvidenceArgumentIsDebugOnlyAbsoluteFrozenAndMutuallyExclusive()
    {
        var outputRoot = Path.Combine(Path.GetTempPath(), "rounds-base-projectile-evidence-" + Guid.NewGuid().ToString("N"));
        var arguments = new[] { StartupRoute.DebugBaseProjectileEvidenceArgument, outputRoot };

        var debug = StartupRoute.Parse(arguments, allowDebugEvidence: true);

        Assert.Equal(StartupMode.DebugBaseProjectileEvidence, debug.Mode);
        Assert.Null(debug.ReplayPath);
        Assert.Equal(Path.GetFullPath(outputRoot), debug.DebugEvidenceOutputPath);
        Assert.Null(debug.DebugAgentPlaytestOutputRoot);
        Assert.False(debug.RunsContinuousPhysics);
        Assert.Throws<ArgumentException>(() => StartupRoute.Parse(arguments, allowDebugEvidence: false));
        Assert.Throws<ArgumentException>(() => StartupRoute.Parse(
            new[] { StartupRoute.DebugBaseProjectileEvidenceArgument },
            allowDebugEvidence: true));
        Assert.Throws<ArgumentException>(() => StartupRoute.Parse(
            new[] { StartupRoute.DebugBaseProjectileEvidenceArgument, "relative.png" },
            allowDebugEvidence: true));
        Assert.Throws<ArgumentException>(() => StartupRoute.Parse(
            new[] { "--replay", "x", StartupRoute.DebugBaseProjectileEvidenceArgument, outputRoot },
            allowDebugEvidence: true));

        Directory.CreateDirectory(outputRoot);
        try
        {
            Assert.Throws<ArgumentException>(() => StartupRoute.Parse(arguments, allowDebugEvidence: true));
        }
        finally
        {
            Directory.Delete(outputRoot);
        }

        var ordinary = StartupRoute.Parse(Array.Empty<string>(), allowDebugEvidence: false);
        var replay = StartupRoute.Parse(new[] { "--replay", "x" }, allowDebugEvidence: false);
        Assert.Equal(StartupMode.Match, ordinary.Mode);
        Assert.Equal(StartupMode.Replay, replay.Mode);
        Assert.True(ordinary.RunsContinuousPhysics);
        Assert.True(replay.RunsContinuousPhysics);
    }

    [Fact]
    public void BaseProjectileEvidenceReadyPathReusesFrozenCaptureBeforeAnyInputRead()
    {
        var main = File.ReadAllText(Path.Combine(FindRepository(), "game", "Main.cs"));
        var readyStart = main.IndexOf("public override void _Ready()", StringComparison.Ordinal);
        var readyEnd = main.IndexOf("private void RefuseUnavailableAgentPlaytestRenderer", StringComparison.Ordinal);
        Assert.True(readyStart >= 0 && readyEnd > readyStart);
        var ready = main[readyStart..readyEnd];

        Assert.Contains(
            "_world = DebugEvidenceMatchFactory.CreateBaseProjectileEvidence();",
            ready,
            StringComparison.Ordinal);
        Assert.Contains("SetPhysicsProcess(false);", ready, StringComparison.Ordinal);
        Assert.Equal(
            1,
            ready.Split(
                "CaptureBaseProjectileEvidenceAsync(route.DebugEvidenceOutputPath!)",
                StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("Godot.Input", ready, StringComparison.Ordinal);
        Assert.DoesNotContain("GetGlobalMousePosition", ready, StringComparison.Ordinal);
    }

    [Fact]
    public void RendererCaptureMarkersAreExactAndInvariant()
    {
        Assert.Equal(
            "DEBUG_INCOMPLETE_FIDELITY_EVIDENCE_COMPLETE screen=3 windowX=811 windowY=-878 windowWidth=821 windowHeight=486 viewportWidth=1920 viewportHeight=1080",
            DebugEvidenceCaptureProtocol.CompleteMarker(
                new DebugEvidenceCaptureAttestation(3, 811, -878, 821, 486, 1920, 1080)));
        Assert.Equal(
            "DEBUG_INCOMPLETE_FIDELITY_EVIDENCE_ERROR stage=save-png code=12",
            DebugEvidenceCaptureProtocol.ErrorMarker("save-png", 12));
        Assert.Equal(
            "DEBUG_INCOMPLETE_FIDELITY_EVIDENCE_ERROR stage=renderer-unavailable code=0",
            DebugEvidenceCaptureProtocol.ErrorMarker("renderer-unavailable", 0));
        Assert.Equal(
            "DEBUG_INCOMPLETE_FIDELITY_EVIDENCE_ERROR stage=wrong-screen screen=1 expectedScreen=3",
            DebugEvidenceCaptureProtocol.WrongScreenMarker(1, 3));
        Assert.Equal(
            "DEBUG_BASE_PROJECTILE_EVIDENCE_COMPLETE stateHash=0123456789abcdef bulletId=0 ownerId=0 desktop=RoundsEvidence-0123456789abcdef0123456789abcdef screen=3 windowX=684 windowY=-900 windowWidth=1280 windowHeight=720 viewportWidth=1920 viewportHeight=1080 assemblySha256=aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa assemblyMvid=bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb pngSha256=cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc frame=frame-0000.png",
            DebugEvidenceCaptureProtocol.BaseProjectileCompleteMarker(
                new DebugBaseProjectileEvidenceAttestation(
                    0x0123456789abcdefUL,
                    0,
                    0,
                    "RoundsEvidence-0123456789abcdef0123456789abcdef",
                    new DebugEvidenceCaptureAttestation(3, 684, -900, 1280, 720, 1920, 1080),
                    new string('a', 64),
                    new string('b', 32),
                    new string('c', 64),
                    "frame-0000.png")));
        Assert.Equal(
            "DEBUG_BASE_PROJECTILE_EVIDENCE_ERROR stage=save-png code=12",
            DebugEvidenceCaptureProtocol.BaseProjectileErrorMarker("save-png", 12));
        Assert.Equal(
            "DEBUG_BASE_PROJECTILE_EVIDENCE_ERROR stage=wrong-screen screen=1 expectedScreen=3",
            DebugEvidenceCaptureProtocol.BaseProjectileWrongScreenMarker(1, 3));
    }

    [Fact]
    public void EvidencePngPublicationCreatesANewDestinationAndNeverOverwrites()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "rounds-evidence-publish-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var temporary = Path.Combine(directory, "temporary.png");
            var output = Path.Combine(directory, "evidence.png");
            File.WriteAllText(temporary, "new-pixels");
            File.WriteAllText(output, "existing-pixels");

            Assert.Throws<IOException>(() =>
                DebugEvidenceCaptureProtocol.PublishPngCreateNew(temporary, output));
            Assert.Equal("existing-pixels", File.ReadAllText(output));
            Assert.Equal("new-pixels", File.ReadAllText(temporary));

            File.Delete(output);
            DebugEvidenceCaptureProtocol.PublishPngCreateNew(temporary, output);
            Assert.False(File.Exists(temporary));
            Assert.Equal("new-pixels", File.ReadAllText(output));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DebugEvidenceUsesRealDeterministicMatchTransitionAndStartsFrozen()
    {
        var first = DebugEvidenceMatchFactory.CreateIncompleteFidelityBoundary();
        var second = DebugEvidenceMatchFactory.CreateIncompleteFidelityBoundary();

        Assert.True(first.IsAtIncompleteFidelityBoundary);
        Assert.Equal(MatchPhase.LoserDraft, first.Match.Phase);
        Assert.Equal(1, first.Match.CurrentPickerId);
        Assert.Equal(new[] { 1, 0 }, first.Match.FullPoints);
        Assert.Equal(new[] { 0, 0 }, first.Match.HalfPoints);
        Assert.Single(first.Match.AcquiredCardsFor(0));
        Assert.Single(first.Match.AcquiredCardsFor(1));
        Assert.Equal(Match.Hash(first.Match), Match.Hash(second.Match));
        Assert.Equal(first.Match.World.Arena.Id, second.Match.World.Arena.Id);

        var frozenHash = Match.Hash(first.Match);
        var frozenTick = first.Match.World.Tick;
        first.Step(new[]
        {
            default,
            new PlayerInput(1, true, true, true),
        });
        Assert.Equal(frozenHash, Match.Hash(first.Match));
        Assert.Equal(frozenTick, first.Match.World.Tick);
        Assert.Single(first.Match.AcquiredCardsFor(1));
    }

    [Fact]
    public void BaseProjectileEvidenceUsesOneDeterministicVanillaSimulationProjectile()
    {
        var first = DebugEvidenceMatchFactory.CreateBaseProjectileEvidence();
        var second = DebugEvidenceMatchFactory.CreateBaseProjectileEvidence();
        var firstHash = Sim.Hash(first);

        Assert.Equal(firstHash, Sim.Hash(second));
        Assert.Equal(0x6a25f798f6582a29UL, firstHash);
        Assert.Equal("arena-006", first.Arena.Id);
        Assert.Equal(DuelPhase.Active, first.Phase);
        Assert.Equal(PlayerTuning.Vanilla, first.Tuning);
        Assert.Equal(CombatTuning.Vanilla, first.Combat);
        Assert.All(first.Players, player => Assert.Same(PlayerCombatProfile.Vanilla, player.CombatProfile));

        var bullet = Assert.Single(first.Bullets);
        Assert.Equal(0, bullet.Id);
        Assert.Equal(0, bullet.OwnerId);
        Assert.Equal(CombatTuning.Vanilla.ProjectileRadius, bullet.Radius);
        Assert.Equal(PlayerCombatProfile.Vanilla.BulletDamage, bullet.Damage);
        Assert.Equal(PlayerCombatProfile.Vanilla.ProjectileBounces, bullet.BouncesRemaining);
        Assert.Equal(PlayerCombatProfile.Vanilla.ProjectileSpeed, bullet.Velocity.Length, 12);
        Assert.True(bullet.Velocity.Y > 0.0);
        Assert.Equal(1, bullet.SweepsCompleted);
        Assert.Equal(1, first.NextBulletId);
        Assert.Equal(firstHash, Sim.Hash(first));
    }

    private static string FindRepository()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Rounds.sln")))
        {
            current = current.Parent;
        }
        return current?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }
}
