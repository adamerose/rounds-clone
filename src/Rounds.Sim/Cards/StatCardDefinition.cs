using System.Collections.ObjectModel;

namespace Rounds.Sim.Cards;

public enum StatTarget : byte
{
    MaximumHealth,
    Lifesteal,
    Damage,
    AttackSpeed,
    ReloadTime,
    ReloadSpeed,
    Ammunition,
    ProjectileSpeed,
    ProjectileBounces,
    BlockCooldown,
}

public enum StatOperation : byte
{
    AddPercent,
    AddFlat,
    AddCount,
    Multiply,
}

public sealed record StatEffect(
    string Id,
    StatTarget Target,
    StatOperation Operation,
    double Value);

public sealed class StatCardDefinition
{
    private readonly ReadOnlyCollection<StatEffect> _effects;

    public StatCardDefinition(
        string id,
        string displayName,
        string summary,
        IEnumerable<StatEffect> effects)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentNullException.ThrowIfNull(effects);
        var effectArray = effects.ToArray();
        if (effectArray.Length == 0)
        {
            throw new ArgumentException("A stat card requires at least one effect.", nameof(effects));
        }

        Id = id;
        DisplayName = displayName;
        Summary = summary;
        _effects = Array.AsReadOnly(effectArray);
    }

    public string Id { get; }

    public string DisplayName { get; }

    public string Summary { get; }

    public IReadOnlyList<StatEffect> Effects => _effects;
}
