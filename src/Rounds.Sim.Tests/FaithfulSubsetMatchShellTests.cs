using Rounds.Game;

namespace Rounds.Sim.Tests;

public sealed class FaithfulSubsetMatchShellTests
{
    [Fact]
    public void ShippedShellStopsBeforeSecondCardSelectionOrLaterSimulation()
    {
        var shell = new FaithfulSubsetMatchShell(Match.Create(14));

        ArmAndConfirm(shell);
        ArmAndConfirm(shell);

        Assert.Equal(MatchPhase.Duel, shell.Match.Phase);
        Assert.Single(shell.Match.AcquiredCardsFor(0));
        Assert.Single(shell.Match.AcquiredCardsFor(1));
        CompleteDuel(shell, winner: 0);
        Assert.False(shell.IsAtIncompleteFidelityBoundary);
        CompleteDuel(shell, winner: 0);

        Assert.True(shell.IsAtIncompleteFidelityBoundary);
        Assert.Equal(MatchPhase.LoserDraft, shell.Match.Phase);
        Assert.Equal(1, shell.Match.CurrentPickerId);
        Assert.Single(shell.Match.AcquiredCardsFor(1));
        Assert.Contains("SECOND-CARD COMBINATIONS", FaithfulSubsetMatchShell.IncompleteFidelityMessage, StringComparison.Ordinal);
        Assert.Contains("DIRECT ROUNDS VERIFICATION", FaithfulSubsetMatchShell.IncompleteFidelityMessage, StringComparison.Ordinal);

        var frozenHash = Match.Hash(shell.Match);
        var frozenTick = shell.Match.World.Tick;
        shell.Step(new PlayerInput[2]);
        shell.Step(new[]
        {
            default,
            new PlayerInput(1, true, true, true),
        });

        Assert.Equal(frozenHash, Match.Hash(shell.Match));
        Assert.Equal(frozenTick, shell.Match.World.Tick);
        Assert.Single(shell.Match.AcquiredCardsFor(1));
        Assert.Equal(MatchPhase.LoserDraft, shell.Match.Phase);
    }

    private static void ArmAndConfirm(FaithfulSubsetMatchShell shell)
    {
        shell.Step(new PlayerInput[2]);
        var confirm = new PlayerInput[2];
        confirm[shell.Match.CurrentPickerId] = new PlayerInput(0, true, false, false);
        shell.Step(confirm);
    }

    private static void CompleteDuel(FaithfulSubsetMatchShell shell, int winner)
    {
        while (shell.Match.World.Phase == DuelPhase.Spawning)
        {
            shell.Step(new PlayerInput[2]);
        }

        shell.Match.World.Players[1 - winner].Health = 0.0;
        var duel = shell.Match.World.DuelNumber;
        var guard = 0;
        while (shell.Match.World.DuelNumber == duel && !shell.IsAtIncompleteFidelityBoundary && guard++ < 200)
        {
            shell.Step(new PlayerInput[2]);
        }

        Assert.True(guard < 200);
    }
}
