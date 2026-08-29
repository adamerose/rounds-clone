namespace Rounds.Game;

internal sealed class HumanPlaytestObservation
{
    private readonly byte[] _pixelBytes;

    public HumanPlaytestObservation(int sequence, ReadOnlySpan<byte> pixelBytes, int width, int height)
    {
        if (sequence < 0 || width <= 0 || height <= 0 ||
            width > AgentPlaytestLimits.MaximumWidth || height > AgentPlaytestLimits.MaximumHeight ||
            pixelBytes.Length != checked(width * height * 4))
        {
            throw new ArgumentOutOfRangeException(nameof(pixelBytes), "A human observation requires bounded tightly packed RGBA8 pixels.");
        }
        Sequence = sequence;
        Width = width;
        Height = height;
        _pixelBytes = pixelBytes.ToArray();
    }

    public int Sequence { get; }
    public int Width { get; }
    public int Height { get; }
    public ReadOnlySpan<byte> PixelBytes => _pixelBytes;
}

internal sealed record NonHumanStructuralObservation(
    string TraceLabel,
    ulong Seed,
    int Sequence,
    int ExactTick,
    int AcceptedIntervalTicks,
    IReadOnlyList<ulong> TickHashes,
    bool Terminal)
{
    public const string RequiredTraceLabel = "non-human-structural";

    public NonHumanStructuralObservation(
        ulong seed,
        int sequence,
        int exactTick,
        int acceptedIntervalTicks,
        IReadOnlyList<ulong> tickHashes,
        bool terminal)
        : this(RequiredTraceLabel, seed, sequence, exactTick, acceptedIntervalTicks, tickHashes, terminal)
    {
    }
}

internal interface IHumanPlaytestDriver
{
    AgentPlaytestRequest Choose(HumanPlaytestObservation observation);
}

internal sealed class DeterministicAdaptivePlaytestDriver : IHumanPlaytestDriver
{
    public AgentPlaytestRequest Choose(HumanPlaytestObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ulong red = 0;
        ulong blue = 0;
        for (var index = 0; index < observation.PixelBytes.Length; index += 4)
        {
            red += observation.PixelBytes[index];
            blue += observation.PixelBytes[index + 2];
        }
        var direction = red >= blue ? 1.0 : -1.0;
        var sequence = checked(observation.Sequence + 1);
        var phaseStep = sequence <= 4;
        var jump = sequence is 2 or 4;
        var players = new[]
        {
            new AgentPlaytestPlayerAction(0, jump && sequence == 2, !phaseStep, false, direction, 0.0),
            new AgentPlaytestPlayerAction(0, jump && sequence == 4, false, false, -direction, 0.0),
        };
        return new AgentPlaytestRequest(
            AgentPlaytestLimits.Protocol,
            sequence,
            phaseStep ? 1 : AgentPlaytestLimits.MaximumIntervalTicks,
            Array.AsReadOnly(players));
    }
}
