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
        Assert.True(ordinary.RunsContinuousPhysics);
        Assert.Equal(StartupMode.Replay, replay.Mode);
        Assert.Equal("evidence.rounds-replay.json", replay.ReplayPath);
        Assert.True(replay.RunsContinuousPhysics);
    }

    [Fact]
    public void EvidenceArgumentIsAvailableOnlyToDebugRouting()
    {
        var arguments = new[] { StartupRoute.DebugIncompleteFidelityEvidenceArgument };

        var debug = StartupRoute.Parse(arguments, allowDebugEvidence: true);

        Assert.Equal(StartupMode.DebugIncompleteFidelityEvidence, debug.Mode);
        Assert.Null(debug.ReplayPath);
        Assert.False(debug.RunsContinuousPhysics);
        Assert.Throws<ArgumentException>(() => StartupRoute.Parse(arguments, allowDebugEvidence: false));
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
