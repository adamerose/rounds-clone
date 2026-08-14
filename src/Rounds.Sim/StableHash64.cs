namespace Rounds.Sim;

internal sealed class StableHash64
{
    private const ulong Offset = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;
    private ulong _value = Offset;

    public ulong Value => _value;

    public void Add(byte value)
    {
        _value ^= value;
        _value = unchecked(_value * Prime);
    }

    public void Add(int value) => Add(unchecked((ulong)(long)value));

    public void Add(long value) => Add(unchecked((ulong)value));

    public void Add(ulong value)
    {
        for (var shift = 0; shift < 64; shift += 8)
        {
            Add((byte)(value >> shift));
        }
    }

    public void Add(double value) => Add(BitConverter.DoubleToUInt64Bits(value));

    public void Add(string value)
    {
        Add(value.Length);
        foreach (var character in value)
        {
            Add((int)character);
        }
    }
}
