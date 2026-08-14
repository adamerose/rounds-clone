namespace Rounds.Sim.Math;

public sealed class Pcg32
{
    private const ulong Multiplier = 6364136223846793005UL;

    public Pcg32(ulong seed, ulong sequence = 1442695040888963407UL)
    {
        Increment = (sequence << 1) | 1UL;
        State = 0UL;
        NextUInt();
        State += seed;
        NextUInt();
    }

    public ulong State { get; private set; }

    public ulong Increment { get; }

    public uint NextUInt()
    {
        var oldState = State;
        State = unchecked((oldState * Multiplier) + Increment);
        var xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        var rotation = (int)(oldState >> 59);
        return (xorShifted >> rotation) | (xorShifted << ((-rotation) & 31));
    }
}
