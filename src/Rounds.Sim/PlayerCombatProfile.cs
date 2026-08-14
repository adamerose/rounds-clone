using Rounds.Sim.Cards;

namespace Rounds.Sim;

public sealed record PlayerCombatProfile(
    double MaximumHealth,
    int MaximumAmmunition,
    double BulletDamage,
    int FireIntervalTicks,
    int ReloadTicks,
    double ProjectileSpeed,
    int BlockCooldownTicks,
    double Lifesteal)
{
    public static PlayerCombatProfile Vanilla { get; } = FromVanilla();

    public static PlayerCombatProfile FromCombat(CombatTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        tuning.Validate();
        return new PlayerCombatProfile(
            tuning.BaseHealth,
            tuning.BaseAmmo,
            tuning.BaseDamage,
            tuning.FireIntervalTicks,
            tuning.ReloadTicks,
            tuning.ProjectileSpeed,
            tuning.BlockCooldownTicks,
            0.0);
    }

    public void Validate()
    {
        if (!double.IsFinite(MaximumHealth) || MaximumHealth <= 0.0 ||
            MaximumAmmunition <= 0 ||
            !double.IsFinite(BulletDamage) || BulletDamage <= 0.0 ||
            FireIntervalTicks <= 0 || ReloadTicks <= 0 ||
            !double.IsFinite(ProjectileSpeed) || ProjectileSpeed <= 0.0 ||
            BlockCooldownTicks <= 0 ||
            !double.IsFinite(Lifesteal) || Lifesteal < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(PlayerCombatProfile), "Player combat profile values must be finite and positive where required.");
        }
    }

    public static PlayerCombatProfile Fold(
        IEnumerable<string> acquiredCardIds,
        StatCardCatalog? catalog = null,
        CombatTuning? combat = null)
    {
        ArgumentNullException.ThrowIfNull(acquiredCardIds);
        var resolvedCatalog = catalog ?? StatCardCatalog.LoadEmbedded();
        var resolvedCombat = combat ?? CombatTuning.Vanilla;
        var positiveHealth = 0.0;
        var negativeHealth = 0.0;
        var damage = 0.0;
        var projectileSpeed = 0.0;
        var attackSpeed = 0.0;
        var blockCooldown = 0.0;
        var reloadSeconds = 0.0;
        var reloadMultiplierCopies = 0;
        var ammunition = 0;
        var lifesteal = 0.0;

        foreach (var cardId in acquiredCardIds.OrderBy(static id => id, StringComparer.Ordinal))
        {
            var card = resolvedCatalog.GetRequired(cardId);
            foreach (var effect in card.Effects)
            {
                switch (effect.Target)
                {
                    case StatTarget.MaximumHealth when effect.Value >= 0.0:
                        positiveHealth += effect.Value / 100.0;
                        break;
                    case StatTarget.MaximumHealth:
                        negativeHealth += -effect.Value / 100.0;
                        break;
                    case StatTarget.Damage:
                        damage += effect.Value / 100.0;
                        break;
                    case StatTarget.ProjectileSpeed:
                        projectileSpeed += effect.Value / 100.0;
                        break;
                    case StatTarget.AttackSpeed:
                        attackSpeed += -effect.Value / 100.0;
                        break;
                    case StatTarget.BlockCooldown:
                        blockCooldown += effect.Value / 100.0;
                        break;
                    case StatTarget.ReloadTime when effect.Operation == StatOperation.AddFlat:
                        reloadSeconds += effect.Value;
                        break;
                    case StatTarget.ReloadTime:
                        reloadMultiplierCopies++;
                        break;
                    case StatTarget.Ammunition:
                        ammunition += checked((int)effect.Value);
                        break;
                    case StatTarget.Lifesteal:
                        lifesteal += effect.Value / 100.0;
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported folded effect `{effect.Id}`.");
                }
            }
        }

        var reload = resolvedCombat.ReloadTicks + (reloadSeconds * World.TickRate);
        for (var copy = 0; copy < reloadMultiplierCopies; copy++)
        {
            reload *= 0.3;
        }

        var profile = new PlayerCombatProfile(
            MaximumHealth: resolvedCombat.BaseHealth * (1.0 + positiveHealth) / (1.0 + negativeHealth),
            MaximumAmmunition: System.Math.Max(1, resolvedCombat.BaseAmmo + ammunition),
            BulletDamage: resolvedCombat.BaseDamage * (1.0 + damage),
            FireIntervalTicks: RoundAtLeastOne(resolvedCombat.FireIntervalTicks * (1.0 + attackSpeed)),
            ReloadTicks: RoundAtLeastOne(reload),
            ProjectileSpeed: resolvedCombat.ProjectileSpeed * (1.0 + projectileSpeed),
            BlockCooldownTicks: RoundAtLeastOne(resolvedCombat.BlockCooldownTicks * System.Math.Max(0.05, 1.0 + blockCooldown)),
            Lifesteal: lifesteal);
        profile.Validate();
        return profile;
    }

    private static int RoundAtLeastOne(double value) =>
        System.Math.Max(1, checked((int)System.Math.Round(value, MidpointRounding.AwayFromZero)));

    private static PlayerCombatProfile FromVanilla()
    {
        return FromCombat(CombatTuning.Vanilla);
    }
}
