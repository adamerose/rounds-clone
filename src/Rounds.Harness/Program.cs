using System.Globalization;
using Rounds.Sim;
using Rounds.Sim.Math;

var parsed = Arguments.Parse(args);
if (!string.Equals(parsed.Command, "smoke", StringComparison.Ordinal))
{
    Console.Error.WriteLine("Usage: Rounds.Harness smoke [--seed <ulong>] [--ticks <positive-int>]");
    return 2;
}

var world = World.CreateSmoke(parsed.Seed);
var inputs = new PlayerInput[world.Players.Count];
for (var tick = 0; tick < parsed.Ticks; tick++)
{
    for (var player = 0; player < inputs.Length; player++)
    {
        inputs[player] = SmokeInput.At(tick, player);
    }

    Sim.Step(world, inputs);
}

Console.WriteLine(
    FormattableString.Invariant($"seed={parsed.Seed} ticks={parsed.Ticks} players={world.Players.Count} hash={Sim.Hash(world):x16}"));
return 0;

internal sealed record Arguments(string Command, ulong Seed, int Ticks)
{
    private const int DefaultTicks = 600;

    public static Arguments Parse(string[] values)
    {
        var command = values.Length > 0 ? values[0] : "smoke";
        var seed = 1UL;
        var ticks = DefaultTicks;
        for (var index = 1; index < values.Length; index++)
        {
            if (values[index] == "--seed" && index + 1 < values.Length)
            {
                seed = ulong.Parse(values[++index], CultureInfo.InvariantCulture);
            }
            else if (values[index] == "--ticks" && index + 1 < values.Length)
            {
                ticks = int.Parse(values[++index], CultureInfo.InvariantCulture);
                if (ticks <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(values), "Tick count must be positive.");
                }
            }
            else
            {
                throw new ArgumentException($"Unknown argument: {values[index]}", nameof(values));
            }
        }

        return new Arguments(command, seed, ticks);
    }
}

internal static class SmokeInput
{
    public static PlayerInput At(int tick, int player)
    {
        var phase = (tick + (player * 19)) % 120;
        var move = phase < 40 ? (sbyte)1 : phase < 80 ? (sbyte)-1 : (sbyte)0;
        return new PlayerInput(
            move,
            JumpHeld: phase == 8,
            FireHeld: phase % 23 < 3,
            BlockHeld: phase is >= 60 and < 63,
            AimDirection: player == 0 ? new Vec2(1.0, 0.0) : new Vec2(-1.0, 0.0));
    }
}
