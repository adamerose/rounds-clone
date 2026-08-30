using Rounds.Sim;
using Rounds.Sim.Maps;
using SimVector = Rounds.Sim.Math.Vec2;

namespace Rounds.Game;

internal static class DebugEvidenceMatchFactory
{
    private const ulong EvidenceSeed = 14;
    private const ulong BaseProjectileEvidenceSeed = 35;
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

    public static World CreateBaseProjectileEvidence()
    {
        var world = World.CreateMatch(
            BaseProjectileEvidenceSeed,
            ArenaCatalog.LoadEmbedded().GetRequired("arena-006"),
            PlayerTuning.Vanilla,
            CombatTuning.Vanilla,
            [PlayerCombatProfile.Vanilla, PlayerCombatProfile.Vanilla]);
        var inputs = new PlayerInput[world.Players.Count];
        for (var tick = 0; tick < world.Combat.SpawnLockTicks && world.Phase == DuelPhase.Spawning; tick++)
        {
            Rounds.Sim.Sim.Step(world, inputs);
        }

        inputs[0] = new PlayerInput(
            MoveAxis: 0,
            JumpHeld: false,
            FireHeld: true,
            BlockHeld: false,
            AimDirection: new SimVector(0.0, 1.0));
        Rounds.Sim.Sim.Step(world, inputs);

        if (world.Phase != DuelPhase.Active ||
            world.Arena.Id != "arena-006" ||
            world.Bullets.Count != 1 ||
            world.Bullets[0].OwnerId != 0 ||
            world.Players.Any(static player => player.CombatProfile != PlayerCombatProfile.Vanilla))
        {
            throw new InvalidOperationException("Debug base-projectile evidence did not reach its required vanilla state.");
        }
        return world;
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
