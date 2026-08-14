namespace Rounds.Sim;

public static class Sim
{
    private const ulong InputHashPrime = 1099511628211UL;

    public static void Step(World world, ReadOnlySpan<PlayerInput> inputs)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (inputs.Length != world.Players.Count)
        {
            throw new ArgumentException("One input is required for every player.", nameof(inputs));
        }

        for (var index = 0; index < inputs.Length; index++)
        {
            if (!double.IsFinite(inputs[index].AimDirection.X) ||
                !double.IsFinite(inputs[index].AimDirection.Y))
            {
                throw new ArgumentException("Aim input must be finite.", nameof(inputs));
            }
        }

        if (world.Phase != DuelPhase.Active)
        {
            AdvanceInactivePhase(world);
            FinishTick(world);
            return;
        }

        for (var index = 0; index < world.Players.Count; index++)
        {
            var player = world.Players[index];
            var inputBits = inputs[index].ToBits();
            player.LastInputBits = inputBits;
            var checksum = MixInput(player.InputChecksum, inputBits);
            checksum = MixInput(checksum, BitConverter.DoubleToUInt64Bits(inputs[index].AimDirection.X));
            checksum = MixInput(checksum, BitConverter.DoubleToUInt64Bits(inputs[index].AimDirection.Y));
            player.InputChecksum = MixInput(checksum, unchecked((ulong)player.Id));
        }

        CombatController.StepActive(world, inputs);
        FinishTick(world);
    }

    private static void AdvanceInactivePhase(World world)
    {
        world.PhaseTicksRemaining--;
        if (world.PhaseTicksRemaining > 0)
        {
            return;
        }

        if (world.Phase == DuelPhase.Spawning)
        {
            world.Phase = DuelPhase.Active;
            return;
        }

        if (world.Phase == DuelPhase.Resolving)
        {
            world.WinnerId = world.PendingWinnerId;
            world.IsDraw = world.PendingDraw;
            world.DuelResultCount++;
            world.Phase = DuelPhase.Result;
            world.PhaseTicksRemaining = world.Combat.ResultTicks;
            return;
        }

        world.ResetDuel(incrementDuel: true);
    }

    private static void FinishTick(World world)
    {
        world.Rng.NextUInt();
        world.Tick++;
    }

    private static ulong MixInput(ulong current, ulong value)
    {
        for (var shift = 0; shift < 64; shift += 8)
        {
            current ^= (byte)(value >> shift);
            current = unchecked(current * InputHashPrime);
        }
        return current;
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
        hash.Add((byte)world.Phase);
        hash.Add(world.PhaseTicksRemaining);
        hash.Add(world.DuelNumber);
        hash.Add(world.DuelResultCount);
        hash.Add(world.NextBulletId);
        hash.Add(world.DroppedBulletCount);
        hash.Add(world.WinnerId ?? -1);
        hash.Add(world.IsDraw ? (byte)1 : (byte)0);
        hash.Add(world.PendingWinnerId ?? -1);
        hash.Add(world.PendingDraw ? (byte)1 : (byte)0);
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
            hash.Add(player.AimDirection.X);
            hash.Add(player.AimDirection.Y);
            hash.Add(player.Health);
            hash.Add(player.Ammo);
            hash.Add(player.FireCooldownTicksRemaining);
            hash.Add(player.ReloadTicksRemaining);
            hash.Add((byte)player.BlockPhase);
            hash.Add(player.BlockTicksRemaining);
            hash.Add(player.WasBlockHeld ? (byte)1 : (byte)0);
            hash.Add(player.IsAlive ? (byte)1 : (byte)0);
            hash.Add(player.LastInputBits);
            hash.Add(player.InputChecksum);
        }

        hash.Add(world.Bullets.Count);
        foreach (var bullet in world.Bullets)
        {
            hash.Add(bullet.Id);
            hash.Add(bullet.OwnerId);
            hash.Add(bullet.Position.X);
            hash.Add(bullet.Position.Y);
            hash.Add(bullet.Velocity.X);
            hash.Add(bullet.Velocity.Y);
            hash.Add(bullet.Radius);
            hash.Add(bullet.Damage);
            hash.Add(bullet.BouncesRemaining);
            hash.Add(bullet.SweepsCompleted);
        }

        if (world.Players.Any(static player => player.CombatProfile != PlayerCombatProfile.Vanilla))
        {
            hash.Add("custom-player-combat-v1");
            foreach (var player in world.Players)
            {
                var profile = player.CombatProfile;
                hash.Add(profile.MaximumHealth);
                hash.Add(profile.MaximumAmmunition);
                hash.Add(profile.BulletDamage);
                hash.Add(profile.FireIntervalTicks);
                hash.Add(profile.ReloadTicks);
                hash.Add(profile.ProjectileSpeed);
                hash.Add(profile.BlockCooldownTicks);
                hash.Add(profile.Lifesteal);
            }
        }

        return hash.Value;
    }
}
