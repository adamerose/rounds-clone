using System.Collections.ObjectModel;
using System.Text.Json;

namespace Rounds.Sim.Cards;

public sealed class StatCardCatalog
{
    private const string EmbeddedResourceName = "Rounds.Sim.Data.cards.json";
    private static readonly string[] SupportedIds =
    [
        "bouncy", "careful-planning", "combine", "defender", "fast-forward", "fastball",
        "glass-cannon", "huge", "leech", "mayhem", "quick-reload", "quick-shot", "spray",
        "steady-shot", "tank", "wind-up",
    ];
    private readonly StatCardDefinition[] _cards;
    private readonly ReadOnlyCollection<StatCardDefinition> _readOnlyCards;

    private StatCardCatalog(StatCardDefinition[] cards)
    {
        _cards = cards;
        _readOnlyCards = Array.AsReadOnly(cards);
    }

    public IReadOnlyList<StatCardDefinition> Cards => _readOnlyCards;

    internal static StatCardCatalog CreateForTesting(params StatCardDefinition[] cards) =>
        new(cards);

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

            if (Array.BinarySearch(SupportedIds, id, StringComparer.Ordinal) < 0)
            {
                continue;
            }

            ValidateCardMode(
                id,
                card.GetProperty("implementationTier").GetString(),
                card.GetProperty("behavior").GetProperty("hook").GetString());
            var originalName = card.GetProperty("originalName").GetString()
                ?? throw new InvalidDataException($"Supported card `{id}` has no original name.");
            ValidateOriginalName(id, originalName);

            var effects = card.GetProperty("effects").EnumerateArray()
                .Select(effect => ParseEffect(id, effect))
                .ToArray();
            ValidateCardEffects(id, effects);
            cards.Add(new StatCardDefinition(
                id,
                originalName,
                effects));
        }

        cards.Sort(static (left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
        if (cards.Count != SupportedIds.Length)
        {
            throw new InvalidDataException("The supported card pool must contain exactly 16 cards.");
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
            "weapon.reload-speed" => StatTarget.ReloadSpeed,
            "weapon.ammo" => StatTarget.Ammunition,
            "weapon.projectile-speed" => StatTarget.ProjectileSpeed,
            "weapon.projectile-bounces" => StatTarget.ProjectileBounces,
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
        if (target == StatTarget.Damage && operation == StatOperation.AddPercent && value <= -100.0)
        {
            throw new InvalidDataException($"Card `{cardId}` has a non-positive damage factor.");
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
            (StatTarget.ReloadSpeed, StatOperation.AddPercent) => true,
            (StatTarget.Ammunition, StatOperation.AddCount) => true,
            (StatTarget.ProjectileSpeed, StatOperation.AddPercent) => true,
            (StatTarget.ProjectileBounces, StatOperation.AddCount) => true,
            _ => false,
        };

    private static void ValidateCardMode(string id, string? tier, string? hook)
    {
        var valid = id switch
        {
            "bouncy" or "mayhem" => tier == "projectile" && hook == "on-bounce",
            "fast-forward" or "spray" => tier == "projectile" && hook == "passive",
            _ => tier == "stat-only" && hook == "passive",
        };
        if (!valid)
        {
            throw new InvalidDataException($"Supported card `{id}` declares unsupported tier or hook behavior.");
        }
    }

    private static void ValidateCardEffects(string id, IReadOnlyList<StatEffect> effects)
    {
        StatEffect[] expected = id switch
        {
            "bouncy" =>
            [
                E("bounces", StatTarget.ProjectileBounces, StatOperation.AddCount, 2),
                E("damage", StatTarget.Damage, StatOperation.AddPercent, 25),
                E("reload-time", StatTarget.ReloadTime, StatOperation.AddFlat, 0.25),
            ],
            "careful-planning" =>
            [
                E("damage", StatTarget.Damage, StatOperation.AddPercent, 100),
                E("attack-speed", StatTarget.AttackSpeed, StatOperation.AddPercent, -150),
                E("reload-time", StatTarget.ReloadTime, StatOperation.AddFlat, 0.5),
            ],
            "combine" =>
            [
                E("damage", StatTarget.Damage, StatOperation.AddPercent, 100),
                E("ammo", StatTarget.Ammunition, StatOperation.AddCount, -2),
                E("reload-time", StatTarget.ReloadTime, StatOperation.AddFlat, 0.5),
            ],
            "defender" =>
            [
                E("block-cooldown", StatTarget.BlockCooldown, StatOperation.AddPercent, -30),
                E("health", StatTarget.MaximumHealth, StatOperation.AddPercent, 30),
            ],
            "fast-forward" =>
            [
                E("projectile-speed", StatTarget.ProjectileSpeed, StatOperation.AddPercent, 100),
                E("reload-speed", StatTarget.ReloadSpeed, StatOperation.AddPercent, 30),
            ],
            "fastball" =>
            [
                E("projectile-speed", StatTarget.ProjectileSpeed, StatOperation.AddPercent, 250),
                E("attack-speed", StatTarget.AttackSpeed, StatOperation.AddPercent, -50),
                E("reload-time", StatTarget.ReloadTime, StatOperation.AddFlat, 0.25),
            ],
            "glass-cannon" =>
            [
                E("damage", StatTarget.Damage, StatOperation.AddPercent, 100),
                E("health", StatTarget.MaximumHealth, StatOperation.AddPercent, -100),
                E("reload-time", StatTarget.ReloadTime, StatOperation.AddFlat, 0.25),
            ],
            "huge" => [E("health", StatTarget.MaximumHealth, StatOperation.AddPercent, 80)],
            "leech" =>
            [
                E("lifesteal", StatTarget.Lifesteal, StatOperation.AddPercent, 75),
                E("health", StatTarget.MaximumHealth, StatOperation.AddPercent, 30),
            ],
            "mayhem" =>
            [
                E("bounces", StatTarget.ProjectileBounces, StatOperation.AddCount, 5),
                E("damage", StatTarget.Damage, StatOperation.AddPercent, -15),
                E("reload-time", StatTarget.ReloadTime, StatOperation.AddFlat, 0.5),
            ],
            "quick-reload" => [E("reload-time", StatTarget.ReloadTime, StatOperation.Multiply, 0.3)],
            "quick-shot" =>
            [
                E("projectile-speed", StatTarget.ProjectileSpeed, StatOperation.AddPercent, 150),
                E("reload-time", StatTarget.ReloadTime, StatOperation.AddFlat, 0.25),
            ],
            "spray" =>
            [
                E("attack-speed", StatTarget.AttackSpeed, StatOperation.AddPercent, 1000),
                E("ammo", StatTarget.Ammunition, StatOperation.AddCount, 12),
                E("damage", StatTarget.Damage, StatOperation.AddPercent, -75),
                E("reload-time", StatTarget.ReloadTime, StatOperation.AddFlat, 0.25),
            ],
            "steady-shot" =>
            [
                E("health", StatTarget.MaximumHealth, StatOperation.AddPercent, 40),
                E("projectile-speed", StatTarget.ProjectileSpeed, StatOperation.AddPercent, 100),
                E("reload-time", StatTarget.ReloadTime, StatOperation.AddFlat, 0.25),
            ],
            "tank" =>
            [
                E("health", StatTarget.MaximumHealth, StatOperation.AddPercent, 100),
                E("attack-speed", StatTarget.AttackSpeed, StatOperation.AddPercent, -25),
                E("reload-time", StatTarget.ReloadTime, StatOperation.AddFlat, 0.5),
            ],
            "wind-up" =>
            [
                E("projectile-speed", StatTarget.ProjectileSpeed, StatOperation.AddPercent, 100),
                E("damage", StatTarget.Damage, StatOperation.AddPercent, 60),
                E("attack-speed", StatTarget.AttackSpeed, StatOperation.AddPercent, -100),
                E("reload-time", StatTarget.ReloadTime, StatOperation.AddFlat, 0.5),
            ],
            _ => throw new InvalidDataException($"Card `{id}` is not supported."),
        };

        if (effects.Count != expected.Length)
        {
            throw new InvalidDataException($"Supported card `{id}` has an unexpected effect count.");
        }
        for (var index = 0; index < effects.Count; index++)
        {
            var actual = effects[index];
            if (actual.Id != expected[index].Id ||
                actual.Target != expected[index].Target ||
                actual.Operation != expected[index].Operation ||
                actual.Value != expected[index].Value)
            {
                throw new InvalidDataException($"Supported card `{id}` has an unexpected effect at index {index}.");
            }
        }
    }

    private static StatEffect E(string id, StatTarget target, StatOperation operation, double value) =>
        new(id, target, operation, value);

    private static void ValidateOriginalName(string id, string originalName)
    {
        var expected = id switch
        {
            "bouncy" => "Bouncy",
            "careful-planning" => "Careful Planning",
            "combine" => "Combine",
            "defender" => "Defender",
            "fast-forward" => "Fast Forward",
            "fastball" => "Fastball",
            "glass-cannon" => "Glass Cannon",
            "huge" => "Huge",
            "leech" => "Leech",
            "mayhem" => "Mayhem",
            "quick-reload" => "Quick Reload",
            "quick-shot" => "Quick Shot",
            "spray" => "Spray",
            "steady-shot" => "Steady Shot",
            "tank" => "Tank",
            "wind-up" => "Wind Up",
            _ => throw new InvalidDataException($"Stat card `{id}` has no sourced original name."),
        };
        if (originalName != expected)
        {
            throw new InvalidDataException($"Supported card `{id}` must use original name `{expected}`.");
        }
    }

}
