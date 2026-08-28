using Rounds.Sim.Cards;
using Rounds.Sim.Maps;
using Rounds.Sim.Math;
using System.Reflection;

namespace Rounds.Sim.Tests;

public sealed class MatchTests
{
    [Fact]
    public void OpeningDraftsAreSequentialArmedAndResetBothProfiles()
    {
        var match = Match.Create(8);
        var initialHash = Match.Hash(match);
        var firstOffer = match.CurrentOffer.Select(card => card.Id).ToArray();

        match.Step(new[]
        {
            new PlayerInput(0, true, false, false),
            new PlayerInput(1, true, false, false),
        });
        Assert.False(match.IsDraftArmed);
        Assert.NotEqual(initialHash, Match.Hash(match));
        Assert.Equal(0, match.World.Tick);

        ArmAndConfirm(match);
        Assert.Equal(MatchPhase.OpeningDraft, match.Phase);
        Assert.Equal(1, match.CurrentPickerId);
        Assert.Single(match.AcquiredCardsFor(0));
        Assert.Empty(match.AcquiredCardsFor(1));
        Assert.Equal(5, match.CurrentOffer.Select(card => card.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.NotEqual(firstOffer, match.CurrentOffer.Select(card => card.Id));

        var rngAfterSecondOffer = match.World.Rng.State;
        ArmAndConfirm(match);

        Assert.Equal(MatchPhase.Duel, match.Phase);
        Assert.Equal(0, match.World.Tick);
        Assert.Equal(0, match.World.DuelNumber);
        Assert.Equal(0, match.World.DuelResultCount);
        Assert.Equal(rngAfterSecondOffer, match.World.Rng.State);
        Assert.Equal(match.World.Players[0].CombatProfile.MaximumHealth, match.World.Players[0].Health);
        Assert.Equal(match.World.Players[1].CombatProfile.MaximumAmmunition, match.World.Players[1].Ammo);
    }

    [Fact]
    public void InactivePlayerCannotNavigateAndPickerWrapsOnRisingEdges()
    {
        var match = Match.Create(3);
        match.Step(new PlayerInput[2]);
        Assert.True(match.IsDraftArmed);

        match.Step(new[] { default(PlayerInput), new PlayerInput(1, true, false, false) });
        Assert.Equal(0, match.SelectedOfferIndex);
        Assert.Empty(match.AcquiredCardsFor(0));

        match.Step(new[] { new PlayerInput(-1, false, false, false), default(PlayerInput) });
        Assert.Equal(4, match.SelectedOfferIndex);
        match.Step(new[] { new PlayerInput(-1, false, false, false), default(PlayerInput) });
        Assert.Equal(4, match.SelectedOfferIndex);
        match.Step(new PlayerInput[2]);
        match.Step(new[] { new PlayerInput(1, false, false, false), default(PlayerInput) });
        Assert.Equal(0, match.SelectedOfferIndex);
    }

    [Fact]
    public void InvalidInputRejectsBeforeMutationInEveryPhase()
    {
        var match = Match.Create(4);
        var before = Match.Hash(match);

        Assert.Throws<ArgumentException>(() => match.Step(new[]
        {
            new PlayerInput(2, false, false, false),
            default,
        }));
        Assert.Equal(before, Match.Hash(match));
        Assert.Throws<ArgumentException>(() => match.Step(new[]
        {
            default,
            new PlayerInput(0, false, false, false, new Vec2(double.NaN, 0.0)),
        }));
        Assert.Equal(before, Match.Hash(match));
    }

    [Fact]
    public void StraightWinsAwardPointThenLoserDraftChangesArena()
    {
        var match = StartMatch(14);
        var openingArena = match.World.Arena.Id;

        CompleteDuel(match, winner: 0);
        Assert.Equal(new[] { 1, 0 }, match.HalfPoints);
        Assert.Equal(new[] { 0, 0 }, match.FullPoints);
        Assert.Equal(openingArena, match.World.Arena.Id);
        CompleteDuel(match, winner: 0);

        Assert.Equal(MatchPhase.LoserDraft, match.Phase);
        Assert.Equal(1, match.CurrentPickerId);
        Assert.Equal(new[] { 0, 0 }, match.HalfPoints);
        Assert.Equal(new[] { 1, 0 }, match.FullPoints);
        Assert.Equal(openingArena, match.World.Arena.Id);
        var counters = (match.World.Tick, match.World.DuelNumber, match.World.DuelResultCount, match.World.NextBulletId);

        ArmAndConfirm(match);

        Assert.Equal(MatchPhase.Duel, match.Phase);
        Assert.NotEqual(openingArena, match.World.Arena.Id);
        Assert.False(match.World.Arena.HasUnsupportedBehavior);
        Assert.Equal(counters.Tick, match.World.Tick);
        Assert.Equal(counters.DuelNumber, match.World.DuelNumber);
        Assert.Equal(counters.DuelResultCount, match.World.DuelResultCount);
        Assert.Equal(counters.NextBulletId, match.World.NextBulletId);
        Assert.Equal(2, match.AcquiredCardsFor(1).Count);
    }

    [Fact]
    public void SplitWinsNeedAThirdDuelAndDrawPreservesProgressAndArena()
    {
        var match = StartMatch(18);
        var arena = match.World.Arena.Id;
        CompleteDuel(match, winner: 0);
        CompleteDuel(match, winner: null);
        Assert.Equal(new[] { 1, 0 }, match.HalfPoints);
        Assert.Equal(arena, match.World.Arena.Id);
        CompleteDuel(match, winner: 1);
        Assert.Equal(new[] { 1, 1 }, match.HalfPoints);
        CompleteDuel(match, winner: 0);

        Assert.Equal(new[] { 1, 0 }, match.FullPoints);
        Assert.Equal(new[] { 0, 0 }, match.HalfPoints);
        Assert.Equal(MatchPhase.LoserDraft, match.Phase);
    }

    [Fact]
    public void FirstToFiveEndsWithoutFinalDraftAndFreezesState()
    {
        var match = StartMatch(29);
        for (var point = 0; point < 5; point++)
        {
            CompleteDuel(match, winner: 0);
            CompleteDuel(match, winner: 0);
            if (point < 4)
            {
                Assert.Equal(MatchPhase.LoserDraft, match.Phase);
                ArmAndConfirm(match);
            }
        }

        Assert.Equal(MatchPhase.MatchResult, match.Phase);
        Assert.Equal(0, match.WinnerId);
        Assert.Equal(new[] { 5, 0 }, match.FullPoints);
        Assert.Equal(5, match.AcquiredCardsFor(1).Count);
        var finalHash = Match.Hash(match);
        var finalTick = match.World.Tick;
        match.Step(new[]
        {
            new PlayerInput(1, true, true, true, new Vec2(1.0, 1.0)),
            new PlayerInput(-1, true, true, true, new Vec2(-1.0, 1.0)),
        });
        Assert.Equal(finalHash, Match.Hash(match));
        Assert.Equal(finalTick, match.World.Tick);
    }

    [Fact]
    public void SeededMatchesHaveStableOffersArenasAndHash()
    {
        var first = RunTwoPoints(51, chooseIndex: 0);
        var second = RunTwoPoints(51, chooseIndex: 0);
        var changed = RunTwoPoints(51, chooseIndex: 1);

        Assert.Equal(first.Hash, second.Hash);
        Assert.Equal(first.Arena, second.Arena);
        Assert.Equal(first.Cards, second.Cards);
        Assert.NotEqual(first.Hash, changed.Hash);
    }

    [Fact]
    public void CompleteScriptedMatchesHaveIdenticalPerDuelHistories()
    {
        var first = RunCompleteMatch(51, chooseIndex: 0);
        var second = RunCompleteMatch(51, chooseIndex: 0);
        var changed = RunCompleteMatch(51, chooseIndex: 1);

        Assert.Equal(first.DuelHashes, second.DuelHashes);
        Assert.Equal(first.Offers, second.Offers);
        Assert.Equal(first.Arenas, second.Arenas);
        Assert.Equal(first.Cards, second.Cards);
        Assert.Equal(first.Scores, second.Scores);
        Assert.Equal(first.FinalHash, second.FinalHash);
        Assert.NotEqual(first.FinalHash, changed.FinalHash);
    }

    [Fact]
    public void MatchHashCoversEveryOwnedFieldAndTheCompleteWorld()
    {
        AssertHashChanges("world", match => match.World.Players[0].Health -= 0.01);
        AssertHashChanges("future RNG", match => match.World.Rng.NextUInt());
        AssertHashChanges("phase", match => SetPrivate(match, "<Phase>k__BackingField", MatchPhase.Duel));
        AssertHashChanges("full score", match => ReadPrivate<int[]>(match, "_fullPoints")[0]++);
        AssertHashChanges("half score", match => ReadPrivate<int[]>(match, "_halfPoints")[1]++);
        AssertHashChanges("acquired IDs", match => ReadPrivate<List<string>[]>(match, "_acquiredCards")[0].Add("combine"));
        AssertHashChanges("picker", match => SetPrivate(match, "<CurrentPickerId>k__BackingField", 1));
        AssertHashChanges("selection", match => SetPrivate(match, "<SelectedOfferIndex>k__BackingField", 1));
        AssertHashChanges("armed latch", match => SetPrivate(match, "<IsDraftArmed>k__BackingField", true));
        AssertHashChanges("axis latch", match => SetPrivate(match, "_lastDraftAxis", (sbyte)1));
        AssertHashChanges("jump latch", match => SetPrivate(match, "_lastDraftJump", true));
        AssertHashChanges("offer length", match => SetPrivate(match, "_currentOffer", Array.Empty<StatCardDefinition>()));
        AssertHashChanges("offer IDs", match =>
        {
            var offer = ReadPrivate<StatCardDefinition[]>(match, "_currentOffer");
            (offer[0], offer[1]) = (offer[1], offer[0]);
        });
        AssertHashChanges("observed result", match => SetPrivate(match, "_observedDuelResultCount", 1));
        AssertHashChanges("pending loser", match => SetPrivate<int?>(match, "_pendingRoundLoser", 1));
        AssertHashChanges("pending winner", match => SetPrivate<int?>(match, "_pendingMatchWinner", 0));
        AssertHashChanges("winner", match => SetPrivate<int?>(match, "<WinnerId>k__BackingField", 0));
    }

    [Fact]
    public void OpeningOffersHaveExactSeedVectorsAndIndependentPcgConsumption()
    {
        var catalog = StatCardCatalog.LoadEmbedded();
        var reference = new Pcg32(23);
        var firstExpected = ReferenceOffer(catalog, reference);
        var match = Match.Create(23);

        Assert.Equal(
            new[] { "fastball", "spray", "fast-forward", "glass-cannon", "bouncy" },
            match.CurrentOffer.Select(card => card.Id));
        Assert.Equal(firstExpected, match.CurrentOffer.Select(card => card.Id));
        Assert.Equal(4_980_105_771_538_322_822UL, match.World.Rng.State);
        Assert.Equal(reference.State, match.World.Rng.State);

        ArmAndConfirm(match);
        var secondExpected = ReferenceOffer(catalog, reference);
        Assert.Equal(
            new[] { "careful-planning", "wind-up", "quick-shot", "glass-cannon", "mayhem" },
            match.CurrentOffer.Select(card => card.Id));
        Assert.Equal(secondExpected, match.CurrentOffer.Select(card => card.Id));
        Assert.Equal(2_330_107_498_961_357_555UL, match.World.Rng.State);
        Assert.Equal(reference.State, match.World.Rng.State);

        var changedSeed = Match.Create(24);
        Assert.Equal(
            new[] { "fast-forward", "quick-reload", "careful-planning", "wind-up", "tank" },
            changedSeed.CurrentOffer.Select(card => card.Id));
        Assert.NotEqual(
            match.CurrentOffer.Select(card => card.Id),
            changedSeed.CurrentOffer.Select(card => card.Id));
    }

    [Fact]
    public void EveryProjectileCardCanBeDraftedAndPersistsIntoTheDuelProfile()
    {
        foreach (var cardId in new[] { "bouncy", "fast-forward", "spray" })
        {
            var match = Match.Create(23);
            var index = Array.FindIndex(
                match.CurrentOffer.Select(card => card.Id).ToArray(),
                id => id == cardId);
            Assert.True(index >= 0);

            ArmAndConfirm(match, index);
            ArmAndConfirm(match);

            Assert.Equal(new[] { cardId }, match.AcquiredCardsFor(0));
            Assert.Equal(PlayerCombatProfile.Fold(new[] { cardId }), match.World.Players[0].CombatProfile);
        }

        var mayhem = Match.Create(23);
        ArmAndConfirm(mayhem);
        var mayhemIndex = Array.FindIndex(
            mayhem.CurrentOffer.Select(card => card.Id).ToArray(),
            id => id == "mayhem");
        Assert.True(mayhemIndex >= 0);
        ArmAndConfirm(mayhem, mayhemIndex);

        Assert.Equal(new[] { "mayhem" }, mayhem.AcquiredCardsFor(1));
        Assert.Equal(PlayerCombatProfile.Fold(new[] { "mayhem" }), mayhem.World.Players[1].CombatProfile);
    }

    [Fact]
    public void OwnedCardsRemainEligibleForLaterOffers()
    {
        var foundRecurrence = false;
        for (ulong seed = 0; seed < 100 && !foundRecurrence; seed++)
        {
            var match = Match.Create(seed);
            var owned = match.CurrentOffer[0].Id;
            ArmAndConfirm(match);
            foundRecurrence = match.CurrentOffer.Any(card => card.Id == owned);
        }

        Assert.True(foundRecurrence);
    }

    [Fact]
    public void ASecondSprayDraftTransitionsIntoTheNextDuelWithPositiveDamage()
    {
        ulong? exercisedSeed = null;
        for (ulong seed = 0; seed < 500 && exercisedSeed is null; seed++)
        {
            var match = Match.Create(seed);
            ArmAndConfirm(match);
            var openingSpray = Array.FindIndex(
                match.CurrentOffer.Select(card => card.Id).ToArray(),
                id => id == "spray");
            if (openingSpray < 0)
            {
                continue;
            }
            ArmAndConfirm(match, openingSpray);
            CompleteDuel(match, winner: 0);
            CompleteDuel(match, winner: 0);
            var comebackSpray = Array.FindIndex(
                match.CurrentOffer.Select(card => card.Id).ToArray(),
                id => id == "spray");
            if (comebackSpray < 0)
            {
                continue;
            }

            ArmAndConfirm(match, comebackSpray);
            exercisedSeed = seed;
            Assert.Equal(MatchPhase.Duel, match.Phase);
            Assert.Equal(new[] { "spray", "spray" }, match.AcquiredCardsFor(1));
            Assert.Equal(0.55 * 0.25 * 0.25, match.World.Players[1].CombatProfile.BulletDamage, 12);
            Assert.True(match.World.Players[1].CombatProfile.BulletDamage > 0.0);
        }

        Assert.NotNull(exercisedSeed);
    }

    [Fact]
    public void ArenaRotationConsumesTheBoundedOrdinalChoice()
    {
        const ulong seed = 77;
        var match = StartMatch(seed);
        CompleteDuel(match, 0);
        CompleteDuel(match, 0);
        var tickCount = match.World.Tick;

        var reference = new Pcg32(seed);
        ConsumeShuffle(reference);
        ConsumeShuffle(reference);
        for (var tick = 0L; tick < tickCount; tick++)
        {
            reference.NextUInt();
        }
        ConsumeShuffle(reference);
        var eligible = ArenaCatalog.LoadEmbedded().Arenas
            .Where(arena => !arena.HasUnsupportedBehavior && arena.Id != "arena-006")
            .OrderBy(arena => arena.Id, StringComparer.Ordinal)
            .ToArray();
        var expected = eligible[(int)reference.NextBounded(61)].Id;

        ArmAndConfirm(match);

        Assert.Equal(expected, match.World.Arena.Id);
        Assert.Equal(reference.State, match.World.Rng.State);
    }

    [Fact]
    public void EmbeddedArenaEligibilityIsExactAndEveryEligibleSpawnResets()
    {
        var catalog = ArenaCatalog.LoadEmbedded();
        var excluded = catalog.Arenas.Where(arena => arena.HasUnsupportedBehavior).Select(arena => arena.Id).ToArray();
        Assert.Equal(
            new[] { "arena-003", "arena-015", "arena-023", "arena-026", "arena-045", "arena-046", "arena-053", "arena-054" },
            excluded);
        var eligible = catalog.Arenas.Where(arena => !arena.HasUnsupportedBehavior).ToArray();
        Assert.Equal(62, eligible.Length);
        foreach (var arena in eligible)
        {
            var world = World.CreateMatch(1, arena);
            Assert.Equal(arena.Spawns[0].Center, world.Players[0].Position);
            Assert.Equal(arena.Spawns[1].Center, world.Players[1].Position);
        }
    }

    private static (ulong Hash, string Arena, string Cards) RunTwoPoints(ulong seed, int chooseIndex)
    {
        var match = Match.Create(seed);
        ArmAndConfirm(match, chooseIndex);
        ArmAndConfirm(match, chooseIndex);
        for (var point = 0; point < 2; point++)
        {
            CompleteDuel(match, 0);
            CompleteDuel(match, 0);
            ArmAndConfirm(match, chooseIndex);
        }
        return (
            Match.Hash(match),
            match.World.Arena.Id,
            string.Join(',', match.AcquiredCardsFor(0).Concat(match.AcquiredCardsFor(1))));
    }

    private static ScriptedHistory RunCompleteMatch(ulong seed, int chooseIndex)
    {
        var match = Match.Create(seed);
        var duelHashes = new List<ulong>();
        var offers = new List<string>();
        var arenas = new List<string>();
        var cards = new List<string>();
        var scores = new List<string>();

        RecordOffer(match, offers);
        ArmAndConfirm(match, chooseIndex);
        RecordOffer(match, offers);
        ArmAndConfirm(match, chooseIndex);
        RecordBuild(match, arenas, cards);

        while (match.Phase != MatchPhase.MatchResult)
        {
            CompleteDuel(match, winner: 0);
            duelHashes.Add(Match.Hash(match));
            scores.Add($"{match.FullPoints[0]}:{match.FullPoints[1]}:{match.HalfPoints[0]}:{match.HalfPoints[1]}");
            RecordBuild(match, arenas, cards);
            if (match.Phase == MatchPhase.LoserDraft)
            {
                RecordOffer(match, offers);
                ArmAndConfirm(match, chooseIndex);
                RecordBuild(match, arenas, cards);
            }
        }

        return new ScriptedHistory(
            duelHashes.ToArray(),
            offers.ToArray(),
            arenas.ToArray(),
            cards.ToArray(),
            scores.ToArray(),
            Match.Hash(match));
    }

    private static void RecordOffer(Match match, List<string> offers) =>
        offers.Add(string.Join(',', match.CurrentOffer.Select(card => card.Id)));

    private static void RecordBuild(Match match, List<string> arenas, List<string> cards)
    {
        arenas.Add(match.World.Arena.Id);
        cards.Add(string.Join('|',
            string.Join(',', match.AcquiredCardsFor(0)),
            string.Join(',', match.AcquiredCardsFor(1))));
    }

    private static string[] ReferenceOffer(StatCardCatalog catalog, Pcg32 rng)
    {
        var shuffled = catalog.Cards.ToArray();
        for (var index = shuffled.Length - 1; index > 0; index--)
        {
            var swap = (int)rng.NextBounded((uint)(index + 1));
            (shuffled[index], shuffled[swap]) = (shuffled[swap], shuffled[index]);
        }
        return shuffled[..5].Select(card => card.Id).ToArray();
    }

    private static void AssertHashChanges(string field, Action<Match> mutate)
    {
        var baseline = Match.Create(91);
        var changed = Match.Create(91);
        mutate(changed);
        Assert.True(Match.Hash(baseline) != Match.Hash(changed), $"Match.Hash omitted {field}.");
    }

    private static T ReadPrivate<T>(Match match, string fieldName) =>
        (T)(typeof(Match).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(match)
            ?? throw new InvalidOperationException($"Missing Match field `{fieldName}`."));

    private static void SetPrivate<T>(Match match, string fieldName, T value)
    {
        var field = typeof(Match).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Missing Match field `{fieldName}`.");
        field.SetValue(match, value);
    }

    private static Match StartMatch(ulong seed)
    {
        var match = Match.Create(seed);
        ArmAndConfirm(match);
        ArmAndConfirm(match);
        return match;
    }

    private static void ArmAndConfirm(Match match, int index = 0)
    {
        Assert.True(match.Phase is MatchPhase.OpeningDraft or MatchPhase.LoserDraft);
        match.Step(new PlayerInput[2]);
        for (var move = 0; move < index; move++)
        {
            var right = new PlayerInput[2];
            right[match.CurrentPickerId] = new PlayerInput(1, false, false, false);
            match.Step(right);
            match.Step(new PlayerInput[2]);
        }
        var confirm = new PlayerInput[2];
        confirm[match.CurrentPickerId] = new PlayerInput(0, true, false, false);
        match.Step(confirm);
    }

    private static void CompleteDuel(Match match, int? winner)
    {
        Assert.Equal(MatchPhase.Duel, match.Phase);
        while (match.World.Phase == DuelPhase.Spawning)
        {
            match.Step(new PlayerInput[2]);
        }
        Assert.Equal(DuelPhase.Active, match.World.Phase);
        if (winner is int winnerId)
        {
            match.World.Players[1 - winnerId].Health = 0.0;
        }
        else
        {
            match.World.Players[0].Health = 0.0;
            match.World.Players[1].Health = 0.0;
        }

        var duel = match.World.DuelNumber;
        var guard = 0;
        while (match.World.DuelNumber == duel && guard++ < 200)
        {
            match.Step(new PlayerInput[2]);
        }
        Assert.True(guard < 200);
    }

    private static void ConsumeShuffle(Pcg32 rng)
    {
        for (uint bound = 16; bound > 1; bound--)
        {
            rng.NextBounded(bound);
        }
    }

    private sealed record ScriptedHistory(
        ulong[] DuelHashes,
        string[] Offers,
        string[] Arenas,
        string[] Cards,
        string[] Scores,
        ulong FinalHash);
}
