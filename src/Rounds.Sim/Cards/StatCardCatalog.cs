using System.Collections.ObjectModel;
using System.Text.Json;

namespace Rounds.Sim.Cards;

public sealed class StatCardCatalog
{
    private const string EmbeddedResourceName = "Rounds.Sim.Data.cards.json";
    private readonly StatCardDefinition[] _cards;
    private readonly ReadOnlyCollection<StatCardDefinition> _readOnlyCards;

    private StatCardCatalog(StatCardDefinition[] cards)
    {
        _cards = cards;
        _readOnlyCards = Array.AsReadOnly(cards);
    }

    public IReadOnlyList<StatCardDefinition> Cards => _readOnlyCards;

    public static StatCardCatalog LoadEmbedded()
    {
        var assembly = typeof(StatCardCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException($"Embedded card catalog `{EmbeddedResourceName}` is missing.");
        return Load(stream);
    }

    public static StatCardCatalog Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        if (root.GetProperty("targetBuild").GetString() != "21020021")
        {
            throw new InvalidDataException("Card catalog targets an unsupported build.");
        }

        var cards = new List<StatCardDefinition>();
        var seenIds = new List<string>();
        foreach (var card in root.GetProperty("cards").EnumerateArray())
        {
            var id = card.GetProperty("id").GetString()
                ?? throw new InvalidDataException("A card id is missing.");
            if (seenIds.Contains(id, StringComparer.Ordinal))
            {
                throw new InvalidDataException($"Card catalog duplicates `{id}`.");
            }
            seenIds.Add(id);

            if (card.GetProperty("implementationTier").GetString() != "stat-only")
            {
                continue;
            }

            if (card.GetProperty("behavior").GetProperty("hook").GetString() != "passive")
            {
                throw new InvalidDataException("A stat-only card declares a behavior hook.");
            }

            var effects = card.GetProperty("effects").EnumerateArray()
                .Select(effect => ParseEffect(id, effect))
                .ToArray();
            cards.Add(new StatCardDefinition(
                id,
                DisplayNameFor(id),
                SummaryFor(id),
                effects));
        }

        cards.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        if (cards.Count != 12)
        {
            throw new InvalidDataException("The stat-only card pool must contain exactly 12 cards.");
        }
        return new StatCardCatalog(cards.ToArray());
    }

    public StatCardDefinition GetRequired(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        foreach (var card in _cards)
        {
            if (card.Id == id)
            {
                return card;
            }
        }
        throw new KeyNotFoundException($"Stat card `{id}` is not present in the catalog.");
    }

    private static StatEffect ParseEffect(string cardId, JsonElement effect)
    {
        var id = effect.GetProperty("id").GetString()
            ?? throw new InvalidDataException($"Card `{cardId}` has an effect without an id.");
        var targetText = effect.GetProperty("target").GetString();
        var operationText = effect.GetProperty("operation").GetString();
        var target = targetText switch
        {
            "player.max-health" => StatTarget.MaximumHealth,
            "player.lifesteal" => StatTarget.Lifesteal,
            "player.block-cooldown" => StatTarget.BlockCooldown,
            "weapon.damage" => StatTarget.Damage,
            "weapon.attack-speed" => StatTarget.AttackSpeed,
            "weapon.reload-time" => StatTarget.ReloadTime,
            "weapon.ammo" => StatTarget.Ammunition,
            "weapon.projectile-speed" => StatTarget.ProjectileSpeed,
            _ => throw new InvalidDataException($"Card `{cardId}` has unsupported target `{targetText}`."),
        };
        var operation = operationText switch
        {
            "add-percent" => StatOperation.AddPercent,
            "add-flat" => StatOperation.AddFlat,
            "add-count" => StatOperation.AddCount,
            "multiply" => StatOperation.Multiply,
            _ => throw new InvalidDataException($"Card `{cardId}` has unsupported operation `{operationText}`."),
        };
        if (!IsSupportedPair(target, operation))
        {
            throw new InvalidDataException($"Card `{cardId}` has unsupported `{targetText}`/`{operationText}` semantics.");
        }

        double value;
        try
        {
            value = effect.GetProperty("value").GetDouble();
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException($"Card `{cardId}` has a malformed effect value.", exception);
        }
        if (!double.IsFinite(value))
        {
            throw new InvalidDataException($"Card `{cardId}` has a non-finite effect.");
        }
        if (operation == StatOperation.AddCount && value != System.Math.Truncate(value))
        {
            throw new InvalidDataException($"Card `{cardId}` has a fractional count effect.");
        }
        if (operation == StatOperation.Multiply && value <= 0.0)
        {
            throw new InvalidDataException($"Card `{cardId}` has a non-positive multiplier.");
        }
        return new StatEffect(id, target, operation, value);
    }

    private static bool IsSupportedPair(StatTarget target, StatOperation operation) =>
        (target, operation) switch
        {
            (StatTarget.MaximumHealth, StatOperation.AddPercent) => true,
            (StatTarget.Lifesteal, StatOperation.AddPercent) => true,
            (StatTarget.BlockCooldown, StatOperation.AddPercent) => true,
            (StatTarget.Damage, StatOperation.AddPercent) => true,
            (StatTarget.AttackSpeed, StatOperation.AddPercent) => true,
            (StatTarget.ReloadTime, StatOperation.AddFlat) => true,
            (StatTarget.ReloadTime, StatOperation.Multiply) => true,
            (StatTarget.Ammunition, StatOperation.AddCount) => true,
            (StatTarget.ProjectileSpeed, StatOperation.AddPercent) => true,
            _ => false,
        };

    private static string DisplayNameFor(string id) => id switch
    {
        "careful-planning" => "Deliberate",
        "combine" => "Chamber Trade",
        "defender" => "Guarded",
        "fastball" => "Railshot",
        "glass-cannon" => "Overcharge",
        "huge" => "Heavy",
        "leech" => "Siphon",
        "quick-reload" => "Snap Load",
        "quick-shot" => "Hair Trigger",
        "steady-shot" => "Stabilizer",
        "tank" => "Juggernaut",
        "wind-up" => "Windup",
        _ => throw new InvalidDataException($"Stat card `{id}` has no original-neutral display name."),
    };

    private static string SummaryFor(string id) => id switch
    {
        "careful-planning" => "Damage up; fire and reload slow",
        "combine" => "Damage up; one-round magazine",
        "defender" => "Health up; block recovers faster",
        "fastball" => "Bullet speed up; handling slow",
        "glass-cannon" => "Double damage; half health",
        "huge" => "Eighty percent more health",
        "leech" => "Health and damage healing",
        "quick-reload" => "Reload at thirty percent",
        "quick-shot" => "Bullet speed up; reload slow",
        "steady-shot" => "Health and bullet speed up",
        "tank" => "Double health; slower fire",
        "wind-up" => "Damage and speed up; fire slow",
        _ => throw new InvalidDataException($"Stat card `{id}` has no original-neutral summary."),
    };
}
