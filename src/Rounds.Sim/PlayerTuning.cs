using System.Text.Json;

namespace Rounds.Sim;

public sealed record PlayerTuning(
    double Radius,
    double RunSpeed,
    double GroundAcceleration,
    double AirControlRatio,
    double Gravity,
    double JumpSpeed,
    int JumpCapacity,
    double GroundVelocityRetention,
    int JumpBufferTicks,
    double JumpReleaseFactor,
    double GroundNormalThreshold,
    double GroundProbeDistance,
    double CollisionSkin,
    int MoveIterations)
{
    public static PlayerTuning Vanilla { get; } = LoadVanilla();

    private static PlayerTuning LoadVanilla() => new(
        Radius: ReadNumber("Rounds.Sim.Data.player.json", "player-diameter") / 2.0,
        RunSpeed: ReadNumber("Rounds.Sim.Data.player.json", "player-run-speed"),
        GroundAcceleration: ReadNumber("Rounds.Sim.Data.player.json", "player-ground-acceleration"),
        AirControlRatio: ReadNumber("Rounds.Sim.Data.player.json", "player-air-control-ratio"),
        Gravity: ReadNumber("Rounds.Sim.Data.player.json", "player-gravity"),
        JumpSpeed: ReadNumber("Rounds.Sim.Data.player.json", "player-jump-speed"),
        JumpCapacity: ReadInteger("Rounds.Sim.Data.player.json", "player-jump-capacity"),
        GroundVelocityRetention: ReadNumber("Rounds.Sim.Data.player.json", "player-ground-friction"),
        JumpBufferTicks: ReadInteger("Rounds.Sim.Data.controls.json", "controls-jump-buffer"),
        JumpReleaseFactor: 0.5,
        GroundNormalThreshold: 0.65,
        GroundProbeDistance: 0.04,
        CollisionSkin: 0.000001,
        MoveIterations: 4);

    private static double ReadNumber(string resourceName, string factId) =>
        ReadValue(resourceName, factId).GetDouble();

    private static int ReadInteger(string resourceName, string factId) =>
        ReadValue(resourceName, factId).GetInt32();

    private static JsonElement ReadValue(string resourceName, string factId)
    {
        var assembly = typeof(PlayerTuning).Assembly;
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
