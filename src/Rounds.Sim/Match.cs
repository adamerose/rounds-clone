using System.Collections.ObjectModel;
using Rounds.Sim.Cards;
using Rounds.Sim.Maps;

namespace Rounds.Sim;

public sealed class Match
{
    private readonly StatCardCatalog _cardCatalog;
    private readonly ArenaDefinition[] _eligibleArenas;
    private readonly int[] _fullPoints = new int[2];
    private readonly int[] _halfPoints = new int[2];
    private readonly ReadOnlyCollection<int> _readOnlyFullPoints;
    private readonly ReadOnlyCollection<int> _readOnlyHalfPoints;
    private readonly List<string>[] _acquiredCards = [[], []];
    private readonly ReadOnlyCollection<string>[] _readOnlyAcquiredCards;
    private StatCardDefinition[] _currentOffer = [];
    private ReadOnlyCollection<StatCardDefinition> _readOnlyCurrentOffer = Array.AsReadOnly(Array.Empty<StatCardDefinition>());
    private int _observedDuelResultCount;
    private int? _pendingRoundLoser;
    private int? _pendingMatchWinner;
    private sbyte _lastDraftAxis;
    private bool _lastDraftJump;

    private Match(ulong seed, ArenaCatalog arenaCatalog, StatCardCatalog cardCatalog)
    {
        ArgumentNullException.ThrowIfNull(arenaCatalog);
        ArgumentNullException.ThrowIfNull(cardCatalog);
        _cardCatalog = cardCatalog;
        _eligibleArenas = arenaCatalog.Arenas
            .Where(static arena => !arena.HasUnsupportedBehavior)
            .OrderBy(static arena => arena.Id, StringComparer.Ordinal)
            .ToArray();
        if (_eligibleArenas.Length != 62)
        {
            throw new InvalidDataException("The playable match arena pool must contain exactly 62 static arenas.");
        }

        var openingArena = _eligibleArenas.Single(static arena => arena.Id == "arena-006");
        World = World.CreateMatch(seed, openingArena);
        _readOnlyFullPoints = Array.AsReadOnly(_fullPoints);
        _readOnlyHalfPoints = Array.AsReadOnly(_halfPoints);
        _readOnlyAcquiredCards =
        [
            _acquiredCards[0].AsReadOnly(),
            _acquiredCards[1].AsReadOnly(),
        ];
        Phase = MatchPhase.OpeningDraft;
        CurrentPickerId = 0;
        GenerateOffer();
        ResetDraftLatch();
    }

    public World World { get; }

    public MatchPhase Phase { get; private set; }

    public IReadOnlyList<int> FullPoints => _readOnlyFullPoints;

    public IReadOnlyList<int> HalfPoints => _readOnlyHalfPoints;

    public int CurrentPickerId { get; private set; } = -1;

    public int SelectedOfferIndex { get; private set; }

    public bool IsDraftArmed { get; private set; }

    public IReadOnlyList<StatCardDefinition> CurrentOffer => _readOnlyCurrentOffer;

    public int? WinnerId { get; private set; }

    public static Match Create(ulong seed) =>
        new(seed, ArenaCatalog.LoadEmbedded(), StatCardCatalog.LoadEmbedded());

    public static Match Create(
        ulong seed,
        ArenaCatalog arenaCatalog,
        StatCardCatalog cardCatalog) =>
        new(seed, arenaCatalog, cardCatalog);

    public IReadOnlyList<string> AcquiredCardsFor(int playerId)
    {
        if ((uint)playerId >= (uint)_readOnlyAcquiredCards.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(playerId));
        }
        return _readOnlyAcquiredCards[playerId];
    }

    public void Step(ReadOnlySpan<PlayerInput> inputs)
    {
        ValidateInputs(inputs);
        switch (Phase)
        {
            case MatchPhase.OpeningDraft:
            case MatchPhase.LoserDraft:
                StepDraft(inputs);
                break;
            case MatchPhase.Duel:
                StepDuel(inputs);
                break;
            case MatchPhase.MatchResult:
                break;
            default:
                throw new InvalidOperationException("The match has an unknown phase.");
        }
    }

    public static ulong Hash(Match match)
    {
        ArgumentNullException.ThrowIfNull(match);
        var hash = new StableHash64();
        hash.Add("match-v1");
        hash.Add(Sim.Hash(match.World));
        hash.Add((byte)match.Phase);
        for (var player = 0; player < 2; player++)
        {
            hash.Add(match._fullPoints[player]);
            hash.Add(match._halfPoints[player]);
            hash.Add(match._acquiredCards[player].Count);
            foreach (var cardId in match._acquiredCards[player])
            {
                hash.Add(cardId);
            }
        }
        hash.Add(match.CurrentPickerId);
        hash.Add(match.SelectedOfferIndex);
        hash.Add(match.IsDraftArmed ? (byte)1 : (byte)0);
        hash.Add((int)match._lastDraftAxis);
        hash.Add(match._lastDraftJump ? (byte)1 : (byte)0);
        hash.Add(match._currentOffer.Length);
        foreach (var card in match._currentOffer)
        {
            hash.Add(card.Id);
        }
        hash.Add(match._observedDuelResultCount);
        hash.Add(match._pendingRoundLoser ?? -1);
        hash.Add(match._pendingMatchWinner ?? -1);
        hash.Add(match.WinnerId ?? -1);
        return hash.Value;
    }

    private static void ValidateInputs(ReadOnlySpan<PlayerInput> inputs)
    {
        if (inputs.Length != 2)
        {
            throw new ArgumentException("A match requires exactly two player inputs.", nameof(inputs));
        }
        for (var index = 0; index < inputs.Length; index++)
        {
            var input = inputs[index];
            if (input.MoveAxis is < -1 or > 1)
            {
                throw new ArgumentException("Match movement must be -1, 0, or 1.", nameof(inputs));
            }
            if (!double.IsFinite(input.AimDirection.X) || !double.IsFinite(input.AimDirection.Y))
            {
                throw new ArgumentException("Match aim input must be finite.", nameof(inputs));
            }
        }
    }

    private void StepDraft(ReadOnlySpan<PlayerInput> inputs)
    {
        var input = inputs[CurrentPickerId];
        if (!IsDraftArmed)
        {
            if (input.MoveAxis == 0 && !input.JumpHeld)
            {
                IsDraftArmed = true;
            }
            _lastDraftAxis = input.MoveAxis;
            _lastDraftJump = input.JumpHeld;
            return;
        }

        if (input.MoveAxis != 0 && input.MoveAxis != _lastDraftAxis)
        {
            SelectedOfferIndex = (SelectedOfferIndex + input.MoveAxis + _currentOffer.Length) % _currentOffer.Length;
        }
        var confirm = input.JumpHeld && !_lastDraftJump;
        _lastDraftAxis = input.MoveAxis;
        _lastDraftJump = input.JumpHeld;
        if (confirm)
        {
            ConfirmDraft();
        }
    }

    private void ConfirmDraft()
    {
        _acquiredCards[CurrentPickerId].Add(_currentOffer[SelectedOfferIndex].Id);
        if (_acquiredCards[CurrentPickerId].Count > 5)
        {
            throw new InvalidOperationException("A player cannot acquire more than five cards.");
        }

        if (Phase == MatchPhase.OpeningDraft && CurrentPickerId == 0)
        {
            CurrentPickerId = 1;
            GenerateOffer();
            ResetDraftLatch();
            return;
        }

        var profiles = FoldProfiles();
        if (Phase == MatchPhase.OpeningDraft)
        {
            World.ConfigureDuel(World.Arena, profiles, incrementDuel: false);
        }
        else
        {
            var nextArena = ChooseNextArena();
            World.ConfigureDuel(nextArena, profiles, incrementDuel: false);
            _pendingRoundLoser = null;
        }

        Phase = MatchPhase.Duel;
        CurrentPickerId = -1;
        ClearOffer();
    }

    private void StepDuel(ReadOnlySpan<PlayerInput> inputs)
    {
        var duelNumberBefore = World.DuelNumber;
        Sim.Step(World, inputs);
        if (World.DuelResultCount > _observedDuelResultCount)
        {
            ProcessPublishedResult();
        }

        if (World.DuelNumber == duelNumberBefore)
        {
            return;
        }

        if (_pendingMatchWinner is int matchWinner)
        {
            WinnerId = matchWinner;
            Phase = MatchPhase.MatchResult;
            CurrentPickerId = -1;
            ClearOffer();
            return;
        }
        if (_pendingRoundLoser is int loser)
        {
            Phase = MatchPhase.LoserDraft;
            CurrentPickerId = loser;
            GenerateOffer();
            ResetDraftLatch();
        }
    }

    private void ProcessPublishedResult()
    {
        if (World.DuelResultCount != _observedDuelResultCount + 1)
        {
            throw new InvalidOperationException("Duel results must be observed in sequence.");
        }
        _observedDuelResultCount = World.DuelResultCount;
        if (World.IsDraw || World.WinnerId is not int winner)
        {
            return;
        }

        _halfPoints[winner]++;
        if (_halfPoints[winner] < 2)
        {
            return;
        }

        _halfPoints[0] = 0;
        _halfPoints[1] = 0;
        _fullPoints[winner]++;
        if (_fullPoints[winner] >= 5)
        {
            _pendingMatchWinner = winner;
        }
        else
        {
            _pendingRoundLoser = 1 - winner;
        }
    }

    private PlayerCombatProfile[] FoldProfiles() =>
    [
        PlayerCombatProfile.Fold(_acquiredCards[0], _cardCatalog, World.Combat),
        PlayerCombatProfile.Fold(_acquiredCards[1], _cardCatalog, World.Combat),
    ];

    private void GenerateOffer()
    {
        var shuffled = _cardCatalog.Cards.ToArray();
        for (var index = shuffled.Length - 1; index > 0; index--)
        {
            var swap = (int)World.Rng.NextBounded((uint)(index + 1));
            (shuffled[index], shuffled[swap]) = (shuffled[swap], shuffled[index]);
        }
        _currentOffer = shuffled[..5];
        _readOnlyCurrentOffer = Array.AsReadOnly(_currentOffer);
        SelectedOfferIndex = 0;
    }

    private ArenaDefinition ChooseNextArena()
    {
        var choices = _eligibleArenas.Where(arena => arena.Id != World.Arena.Id).ToArray();
        if (choices.Length != 61)
        {
            throw new InvalidOperationException("Arena rotation requires 61 alternatives.");
        }
        return choices[(int)World.Rng.NextBounded(61)];
    }

    private void ResetDraftLatch()
    {
        IsDraftArmed = false;
        _lastDraftAxis = 0;
        _lastDraftJump = false;
    }

    private void ClearOffer()
    {
        _currentOffer = [];
        _readOnlyCurrentOffer = Array.AsReadOnly(_currentOffer);
        SelectedOfferIndex = 0;
        IsDraftArmed = false;
        _lastDraftAxis = 0;
        _lastDraftJump = false;
    }
}
