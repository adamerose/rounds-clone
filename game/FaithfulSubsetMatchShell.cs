using Rounds.Sim;

namespace Rounds.Game;

public sealed class FaithfulSubsetMatchShell
{
    public const string IncompleteFidelityMessage =
        "INCOMPLETE FIDELITY — SECOND-CARD COMBINATIONS AWAIT DIRECT ROUNDS VERIFICATION";
    public const string IncompleteFidelitySubtitle =
        "THE OPENING CARDS AND FIRST FULL ROUND ARE THE CURRENT PLAYABLE SUBSET";
    internal const int IncompleteFidelityMaximumLineCharacters = 32;
    internal const string IncompleteFidelityHeadlineLine1 = "INCOMPLETE FIDELITY —";
    internal const string IncompleteFidelityHeadlineLine2 = "SECOND-CARD COMBINATIONS AWAIT";
    internal const string IncompleteFidelityHeadlineLine3 = "DIRECT ROUNDS VERIFICATION";
    internal const string IncompleteFidelitySubtitleLine1 = "THE OPENING CARDS";
    internal const string IncompleteFidelitySubtitleLine2 = "AND FIRST FULL ROUND";
    internal const string IncompleteFidelitySubtitleLine3 = "ARE THE CURRENT PLAYABLE SUBSET";

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
