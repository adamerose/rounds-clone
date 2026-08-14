namespace Rounds.Sim;

public static class Sim
{
    public static void Step(World world, ReadOnlySpan<PlayerInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (inputs.Length != world.Players.Count)
        {
            throw new ArgumentException("One input is required for every player.", nameof(inputs));
        }

        for (var index = 0; index < world.Players.Count; index++)
        {
            var player = world.Players[index];
            var inputBits = inputs[index].ToBits();
            player.LastInputBits = inputBits;
            player.InputChecksum = unchecked(
                (player.InputChecksum * 1099511628211UL)
                ^ inputBits
                ^ (ulong)player.Id);
            Physics.KinematicController.Step(player, inputs[index], world.Arena.StaticBoxes, world.Tuning);
        }

        world.Rng.NextUInt();
        world.Tick++;
    }

    public static ulong Hash(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        var hash = new StableHash64();
        hash.Add(world.Seed);
        hash.Add(world.Tick);
        hash.Add(world.Rng.State);
        hash.Add(world.Rng.Increment);
        hash.Add(world.Arena.Id);
        hash.Add(world.Players.Count);
        foreach (var player in world.Players)
        {
            hash.Add(player.Id);
            hash.Add(player.TeamId);
            hash.Add(player.Position.X);
            hash.Add(player.Position.Y);
            hash.Add(player.Velocity.X);
            hash.Add(player.Velocity.Y);
            hash.Add(player.IsGrounded ? (byte)1 : (byte)0);
            hash.Add(player.JumpsRemaining);
            hash.Add(player.JumpBufferTicksRemaining);
            hash.Add(player.JumpCutAvailable ? (byte)1 : (byte)0);
            hash.Add(player.WasJumpHeld ? (byte)1 : (byte)0);
            hash.Add(player.LastInputBits);
            hash.Add(player.InputChecksum);
        }

        return hash.Value;
    }
}
