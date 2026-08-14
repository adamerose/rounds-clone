using Rounds.Sim.Math;
using Rounds.Sim.Physics;

namespace Rounds.Sim;

internal static class CombatController
{
    private enum ContactKind : byte
    {
        Geometry,
        Block,
        Body,
    }

    private readonly record struct BulletContact(
        bool HasHit,
        ContactKind Kind,
        SweepHit Hit,
        int PlayerId)
    {
        public static BulletContact None => new(false, ContactKind.Body, SweepHit.None, int.MaxValue);
    }

    public static void StepActive(World world, ReadOnlySpan<PlayerInput> inputs)
    {
        AdvanceTimers(world);
        ApplyInputs(world, inputs);
        for (var index = 0; index < world.Players.Count; index++)
        {
            var player = world.Players[index];
            if (player.IsAlive)
            {
                KinematicController.Step(player, inputs[index], world.Arena.StaticBoxes, world.Tuning);
            }
        }

        MoveBullets(world);
        DetectDeaths(world);
    }

    private static void AdvanceTimers(World world)
    {
        foreach (var player in world.Players)
        {
            if (player.FireCooldownTicksRemaining > 0)
            {
                player.FireCooldownTicksRemaining--;
            }

            if (player.ReloadTicksRemaining > 0)
            {
                player.ReloadTicksRemaining--;
                if (player.ReloadTicksRemaining == 0)
                {
                    player.Ammo = player.CombatProfile.MaximumAmmunition;
                }
            }

            if (player.BlockPhase == BlockPhase.Active)
            {
                player.BlockTicksRemaining--;
                if (player.BlockTicksRemaining == 0)
                {
                    player.BlockPhase = BlockPhase.Cooldown;
                    player.BlockTicksRemaining = player.CombatProfile.BlockCooldownTicks;
                }
            }
            else if (player.BlockPhase == BlockPhase.Cooldown)
            {
                player.BlockTicksRemaining--;
                if (player.BlockTicksRemaining == 0)
                {
                    player.BlockPhase = BlockPhase.Ready;
                }
            }
        }
    }

    private static void ApplyInputs(World world, ReadOnlySpan<PlayerInput> inputs)
    {
        for (var index = 0; index < world.Players.Count; index++)
        {
            var player = world.Players[index];
            var input = inputs[index];
            if (!player.IsAlive)
            {
                continue;
            }

            if (input.AimDirection.X != 0.0 || input.AimDirection.Y != 0.0)
            {
                player.AimDirection = input.AimDirection.Normalized();
            }

            var blockRising = input.BlockHeld && !player.WasBlockHeld;
            if (blockRising && player.BlockPhase == BlockPhase.Ready)
            {
                player.BlockPhase = BlockPhase.Active;
                player.BlockTicksRemaining = world.Combat.BlockActiveTicks;
                ApplyBlockPush(world, player);
            }
            player.WasBlockHeld = input.BlockHeld;

            if (input.FireHeld &&
                player.FireCooldownTicksRemaining == 0 &&
                player.ReloadTicksRemaining == 0 &&
                player.Ammo > 0)
            {
                Fire(world, player);
            }
        }
    }

    private static void Fire(World world, Player player)
    {
        var tuning = world.Combat;
        var muzzleDistance = world.Tuning.Radius + tuning.ProjectileRadius + tuning.MuzzleClearance;
        var bullet = new Bullet
        {
            Id = world.NextBulletId++,
            OwnerId = player.Id,
            Position = player.Position + (player.AimDirection * muzzleDistance),
            Velocity = player.AimDirection * player.CombatProfile.ProjectileSpeed,
            Radius = tuning.ProjectileRadius,
            Damage = player.CombatProfile.BulletDamage,
            BouncesRemaining = tuning.BaseBounces,
        };

        if (world.Bullets.Count >= tuning.LiveBulletCap)
        {
            world.Bullets.RemoveAt(0);
            world.DroppedBulletCount++;
        }
        world.Bullets.Add(bullet);

        player.Velocity -= player.AimDirection * tuning.RecoilSpeed;
        player.Ammo--;
        player.FireCooldownTicksRemaining = player.CombatProfile.FireIntervalTicks;
        if (player.Ammo == 0)
        {
            player.ReloadTicksRemaining = player.CombatProfile.ReloadTicks;
        }
    }

    private static void ApplyBlockPush(World world, Player blocker)
    {
        var tuning = world.Combat;
        foreach (var other in world.Players)
        {
            if (!other.IsAlive || other.Id == blocker.Id)
            {
                continue;
            }

            var offset = other.Position - blocker.Position;
            if (offset.LengthSquared > tuning.BlockPushRadius * tuning.BlockPushRadius)
            {
                continue;
            }

            var direction = offset.LengthSquared == 0.0
                ? new Vec2(other.Id > blocker.Id ? 1.0 : -1.0, 0.0)
                : offset.Normalized();
            var impulse = direction * tuning.BlockPushSpeed;
            other.Velocity += impulse;
            blocker.Velocity -= impulse;
        }

        foreach (var box in world.Arena.StaticBoxes.OrderBy(static box => box.SourceOrder))
        {
            var overlap = Collision.SweepCircle(
                blocker.Position,
                tuning.BlockRadius,
                Vec2.Zero,
                box);
            if (overlap.HasHit)
            {
                blocker.Velocity += overlap.Normal * tuning.BlockPushSpeed;
            }
        }
    }

    private static void MoveBullets(World world)
    {
        var index = 0;
        while (index < world.Bullets.Count)
        {
            if (MoveBullet(world, world.Bullets[index]))
            {
                index++;
            }
            else
            {
                world.Bullets.RemoveAt(index);
            }
        }
    }

    private static bool MoveBullet(World world, Bullet bullet)
    {
        var remaining = bullet.Velocity;
        var completedSweep = false;
        for (var iteration = 0; iteration < world.Combat.BulletContactIterations; iteration++)
        {
            if (remaining.LengthSquared == 0.0)
            {
                completedSweep = true;
                break;
            }

            var contact = FindContact(world, bullet, remaining);
            if (!contact.HasHit)
            {
                bullet.Position += remaining;
                completedSweep = true;
                break;
            }

            var time = System.Math.Clamp(contact.Hit.Time, 0.0, 1.0);
            bullet.Position += remaining * time;
            bullet.Position += contact.Hit.Normal * (contact.Hit.Separation + world.Tuning.CollisionSkin);
            if (contact.Kind == ContactKind.Geometry)
            {
                return false;
            }

            var target = world.Players[contact.PlayerId];
            if (contact.Kind == ContactKind.Body)
            {
                var healthBefore = target.Health;
                target.Health = System.Math.Max(0.0, target.Health - bullet.Damage);
                target.Velocity += bullet.Velocity.Normalized() * world.Combat.HitKnockbackSpeed;
                var actualDamage = healthBefore - target.Health;
                var owner = world.Players[bullet.OwnerId];
                if (owner.IsAlive && owner.CombatProfile.Lifesteal > 0.0)
                {
                    owner.Health = System.Math.Min(
                        owner.CombatProfile.MaximumHealth,
                        owner.Health + (actualDamage * owner.CombatProfile.Lifesteal));
                }
                return false;
            }

            bullet.OwnerId = target.Id;
            bullet.Velocity = Reflect(bullet.Velocity, contact.Hit.Normal);
            remaining = Reflect(remaining * (1.0 - time), contact.Hit.Normal);
        }

        if (!completedSweep)
        {
            return false;
        }

        bullet.SweepsCompleted++;
        return bullet.SweepsCompleted < world.Combat.BulletLifetimeSweeps;
    }

    private static BulletContact FindContact(World world, Bullet bullet, Vec2 remaining)
    {
        var geometry = Collision.SweepCircle(
            bullet.Position,
            bullet.Radius,
            remaining,
            world.Arena.StaticBoxes);
        var best = geometry.HasHit
            ? new BulletContact(true, ContactKind.Geometry, geometry, int.MaxValue)
            : BulletContact.None;

        foreach (var player in world.Players)
        {
            if (!player.IsAlive || player.Id == bullet.OwnerId)
            {
                continue;
            }

            if (player.BlockPhase == BlockPhase.Active)
            {
                var blockHit = Collision.SweepCircleCircle(
                    bullet.Position,
                    bullet.Radius,
                    remaining,
                    player.Position,
                    world.Combat.BlockRadius,
                    Vec2.Zero,
                    player.Id,
                    $"block-{player.Id}");
                best = Choose(best, new BulletContact(true, ContactKind.Block, blockHit, player.Id));
            }

            var bodyHit = Collision.SweepCircleCircle(
                bullet.Position,
                bullet.Radius,
                remaining,
                player.Position,
                world.Tuning.Radius,
                Vec2.Zero,
                player.Id,
                $"player-{player.Id}");
            best = Choose(best, new BulletContact(true, ContactKind.Body, bodyHit, player.Id));
        }

        return best;
    }

    private static BulletContact Choose(BulletContact current, BulletContact candidate)
    {
        if (!candidate.Hit.HasHit)
        {
            return current;
        }
        if (!current.HasHit || candidate.Hit.Time < current.Hit.Time - Collision.TimeEpsilon)
        {
            return candidate;
        }
        if (System.Math.Abs(candidate.Hit.Time - current.Hit.Time) > Collision.TimeEpsilon)
        {
            return current;
        }
        if (candidate.Kind < current.Kind ||
            (candidate.Kind == current.Kind && candidate.Hit.SourceOrder < current.Hit.SourceOrder))
        {
            return candidate;
        }
        return current;
    }

    private static Vec2 Reflect(Vec2 velocity, Vec2 normal) =>
        velocity - (normal * (2.0 * Vec2.Dot(velocity, normal)));

    private static void DetectDeaths(World world)
    {
        var anyDeath = false;
        foreach (var player in world.Players)
        {
            if (player.IsAlive &&
                (player.Health <= 0.0 || player.Position.Y < world.Arena.KillBoundaryY))
            {
                player.IsAlive = false;
                anyDeath = true;
            }
        }

        if (!anyDeath)
        {
            return;
        }

        var survivors = world.Players.Where(player => player.IsAlive).ToArray();
        world.PendingDraw = survivors.Length != 1;
        world.PendingWinnerId = survivors.Length == 1 ? survivors[0].Id : null;
        world.Phase = DuelPhase.Resolving;
        world.PhaseTicksRemaining = world.Combat.ResolveTicks;
    }
}
