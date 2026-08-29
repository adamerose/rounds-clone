using Rounds.Sim;

namespace Rounds.Game;

public sealed class FaithfulSubsetMatchShell
{
    public const string IncompleteFidelityMessage =
        "INCOMPLETE FIDELITY — SECOND-CARD COMBINATIONS AWAIT DIRECT ROUNDS VERIFICATION";

    public FaithfulSubsetMatchShell(Match match)
    {
        Match = match ?? throw new ArgumentNullException(nameof(match));
        UpdateBoundary();
    }

    public Match Match { get; }

    public bool IsAtIncompleteFidelityBoundary { get; private set; }

    public void Step(ReadOnlySpan<PlayerInput> inputs)
    {
        if (IsAtIncompleteFidelityBoundary)
        {
            return;
        }

        Match.Step(inputs);
        UpdateBoundary();
    }

    private void UpdateBoundary()
    {
        IsAtIncompleteFidelityBoundary =
            Match.Phase == MatchPhase.LoserDraft &&
            Match.AcquiredCardsFor(Match.CurrentPickerId).Count >= 1;
    }
}
