using Rounds.Sim;

namespace Rounds.Game;

internal static class DebugEvidenceMatchFactory
{
    private const ulong EvidenceSeed = 14;
    private const int EvidenceWinner = 0;
    private const int TransitionStepLimit = 200;

    public static FaithfulSubsetMatchShell CreateIncompleteFidelityBoundary()
    {
        var shell = new FaithfulSubsetMatchShell(Match.Create(EvidenceSeed));
        ArmAndConfirmCurrentOffer(shell);
        ArmAndConfirmCurrentOffer(shell);
        CompleteDuel(shell, EvidenceWinner);
        CompleteDuel(shell, EvidenceWinner);

        if (!shell.IsAtIncompleteFidelityBoundary || shell.Match.Phase != MatchPhase.LoserDraft)
        {
            throw new InvalidOperationException("Debug evidence did not reach the incomplete-fidelity boundary.");
        }
        return shell;
    }

    private static void ArmAndConfirmCurrentOffer(FaithfulSubsetMatchShell shell)
    {
        shell.Step(new PlayerInput[2]);
        var confirm = new PlayerInput[2];
        confirm[shell.Match.CurrentPickerId] = new PlayerInput(0, true, false, false);
        shell.Step(confirm);
    }

    private static void CompleteDuel(FaithfulSubsetMatchShell shell, int winner)
    {
        var steps = 0;
        while (shell.Match.World.Phase == DuelPhase.Spawning)
        {
            StepWithLimit(shell, ref steps);
        }

        shell.Match.World.Players[1 - winner].Health = 0.0;
        var duelNumber = shell.Match.World.DuelNumber;
        while (shell.Match.World.DuelNumber == duelNumber && !shell.IsAtIncompleteFidelityBoundary)
        {
            StepWithLimit(shell, ref steps);
        }
    }

    private static void StepWithLimit(FaithfulSubsetMatchShell shell, ref int steps)
    {
        if (steps++ >= TransitionStepLimit)
        {
            throw new InvalidOperationException("Debug evidence match did not complete its expected transition.");
        }
        shell.Step(new PlayerInput[2]);
    }
}
