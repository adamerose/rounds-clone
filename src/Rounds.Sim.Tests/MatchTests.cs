using Rounds.Sim.Cards;
using Rounds.Sim.Maps;
using Rounds.Sim.Math;

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
        for (uint bound = 12; bound > 1; bound--)
        {
            rng.NextBounded(bound);
        }
    }
}
