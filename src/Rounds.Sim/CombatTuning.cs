using System.Text.Json;

namespace Rounds.Sim;

public sealed record CombatTuning(
    double BaseHealth,
    int BaseAmmo,
    double BaseDamage,
    int FireIntervalTicks,
    int ReloadTicks,
    double ProjectileSpeed,
    double ProjectileRadius,
    int BaseBounces,
    double RecoilSpeed,
    int BlockActiveTicks,
    int BlockCooldownTicks,
    int BulletLifetimeSweeps,
    double HitKnockbackSpeed,
    double MuzzleClearance,
    double BlockRadius,
    double BlockPushRadius,
    double BlockPushSpeed,
    int BulletContactIterations,
    int SpawnLockTicks,
    int ResolveTicks,
    int ResultTicks,
    int LiveBulletCap)
{
    public static CombatTuning Vanilla { get; } = LoadVanilla();

    public void Validate()
    {
        if (!double.IsFinite(BaseHealth) || BaseHealth <= 0.0 ||
            BaseAmmo <= 0 ||
            !double.IsFinite(BaseDamage) || BaseDamage <= 0.0 ||
            FireIntervalTicks <= 0 || ReloadTicks <= 0 ||
            !double.IsFinite(ProjectileSpeed) || ProjectileSpeed <= 0.0 ||
            !double.IsFinite(ProjectileRadius) || ProjectileRadius <= 0.0 ||
            BaseBounces < 0 ||
            !double.IsFinite(RecoilSpeed) || RecoilSpeed < 0.0 ||
            BlockActiveTicks <= 0 || BlockCooldownTicks <= 0 ||
            BulletLifetimeSweeps <= 0 ||
            !double.IsFinite(HitKnockbackSpeed) || HitKnockbackSpeed < 0.0 ||
            !double.IsFinite(MuzzleClearance) || MuzzleClearance < 0.0 ||
            !double.IsFinite(BlockRadius) || BlockRadius <= 0.0 ||
            !double.IsFinite(BlockPushRadius) || BlockPushRadius <= 0.0 ||
            !double.IsFinite(BlockPushSpeed) || BlockPushSpeed < 0.0 ||
            BulletContactIterations <= 0 || SpawnLockTicks <= 0 ||
            ResolveTicks <= 0 || ResultTicks <= 0 || LiveBulletCap <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CombatTuning), "Combat tuning values must be finite and positive where required.");
        }
    }

    private static CombatTuning LoadVanilla() => new(
        BaseHealth: ReadNumber("Rounds.Sim.Data.player.json", "player-base-health"),
        BaseAmmo: ReadInteger("Rounds.Sim.Data.combat.json", "combat-base-ammo"),
        BaseDamage: ReadNumber("Rounds.Sim.Data.combat.json", "combat-base-damage"),
        FireIntervalTicks: ReadInteger("Rounds.Sim.Data.combat.json", "combat-fire-interval"),
        ReloadTicks: ReadInteger("Rounds.Sim.Data.combat.json", "combat-reload-time"),
        ProjectileSpeed: ReadNumber("Rounds.Sim.Data.combat.json", "combat-projectile-speed"),
        ProjectileRadius: ReadNumber("Rounds.Sim.Data.combat.json", "combat-projectile-radius"),
        BaseBounces: ReadInteger("Rounds.Sim.Data.combat.json", "combat-base-bounces"),
        RecoilSpeed: ReadNumber("Rounds.Sim.Data.combat.json", "combat-recoil-speed"),
        BlockActiveTicks: ReadInteger("Rounds.Sim.Data.combat.json", "combat-block-window"),
        BlockCooldownTicks: ReadInteger("Rounds.Sim.Data.combat.json", "combat-block-cooldown"),
        BulletLifetimeSweeps: 240,
        HitKnockbackSpeed: 0.14,
        MuzzleClearance: 0.02,
        BlockRadius: 0.85,
        BlockPushRadius: 2.0,
        BlockPushSpeed: 0.18,
        BulletContactIterations: 4,
        SpawnLockTicks: 60,
        ResolveTicks: 6,
        ResultTicks: 90,
        LiveBulletCap: 2048);

    private static double ReadNumber(string resourceName, string factId) =>
        ReadValue(resourceName, factId).GetDouble();

    private static int ReadInteger(string resourceName, string factId) =>
        ReadValue(resourceName, factId).GetInt32();

    private static JsonElement ReadValue(string resourceName, string factId)
    {
        var assembly = typeof(CombatTuning).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded tuning document `{resourceName}` is missing.");
        using var document = JsonDocument.Parse(stream);
        foreach (var fact in document.RootElement.GetProperty("facts").EnumerateArray())
        {
            if (fact.GetProperty("id").GetString() == factId)
            {
                return fact.GetProperty("value").Clone();
            }
        }

        throw new InvalidDataException($"Tuning fact `{factId}` is missing from `{resourceName}`.");
    }
}
