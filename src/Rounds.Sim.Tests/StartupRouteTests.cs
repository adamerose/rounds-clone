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
}
