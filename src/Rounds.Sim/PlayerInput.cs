namespace Rounds.Sim;

public readonly record struct PlayerInput(
    sbyte MoveAxis,
    bool JumpHeld,
    bool FirePressed,
    bool BlockPressed)
{
    public byte ToBits()
    {
        var axis = MoveAxis < 0 ? 1 : MoveAxis > 0 ? 2 : 0;
        return (byte)(axis
            | (JumpHeld ? 1 << 2 : 0)
            | (FirePressed ? 1 << 3 : 0)
            | (BlockPressed ? 1 << 4 : 0));
    }
}
