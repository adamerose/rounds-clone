using Rounds.Sim.Math;

namespace Rounds.Sim;

public readonly record struct PlayerInput(
    sbyte MoveAxis,
    bool JumpHeld,
    bool FireHeld,
    bool BlockHeld,
    Vec2 AimDirection = default)
{
    public byte ToBits()
    {
        var axis = MoveAxis < 0 ? 1 : MoveAxis > 0 ? 2 : 0;
        return (byte)(axis
            | (JumpHeld ? 1 << 2 : 0)
            | (FireHeld ? 1 << 3 : 0)
            | (BlockHeld ? 1 << 4 : 0));
    }
}
