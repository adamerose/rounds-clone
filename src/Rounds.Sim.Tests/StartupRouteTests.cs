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
        Assert.True(ordinary.RunsContinuousPhysics);
        Assert.Equal(StartupMode.Replay, replay.Mode);
        Assert.Equal("evidence.rounds-replay.json", replay.ReplayPath);
        Assert.Null(replay.DebugEvidenceOutputPath);
        Assert.True(replay.RunsContinuousPhysics);
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
            "DEBUG_INCOMPLETE_FIDELITY_EVIDENCE_COMPLETE width=1280 height=720",
            DebugEvidenceCaptureProtocol.CompleteMarker(1280, 720));
        Assert.Equal(
            "DEBUG_INCOMPLETE_FIDELITY_EVIDENCE_ERROR stage=save-png code=12",
            DebugEvidenceCaptureProtocol.ErrorMarker("save-png", 12));
        Assert.Equal(
            "DEBUG_INCOMPLETE_FIDELITY_EVIDENCE_ERROR stage=renderer-unavailable code=0",
            DebugEvidenceCaptureProtocol.ErrorMarker("renderer-unavailable", 0));
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
