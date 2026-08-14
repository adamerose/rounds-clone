using Rounds.Sim.Math;

namespace Rounds.Sim.Physics;

internal static class KinematicController
{
    private const double VelocityStopEpsilon = 1e-9;

    public static void Step(Player player, PlayerInput input, IReadOnlyList<Obb> boxes, PlayerTuning tuning)
    {
        var jumpHeld = input.JumpHeld;
        if (jumpHeld && !player.WasJumpHeld)
        {
            player.JumpBufferTicksRemaining = tuning.JumpBufferTicks;
        }

        ApplyHorizontalIntent(player, input.MoveAxis, tuning);
        if (!player.IsGrounded)
        {
            player.Velocity = player.Velocity with { Y = player.Velocity.Y - tuning.Gravity };
        }

        TryConsumeJump(player, tuning);
        ApplyJumpRelease(player, jumpHeld, tuning);
        MoveAndSlide(player, boxes, tuning);

        var grounded = player.IsGrounded || ProbeGround(player, boxes, tuning);
        player.IsGrounded = grounded;
        if (grounded)
        {
            player.JumpsRemaining = tuning.JumpCapacity;
            if (player.Velocity.Y < 0.0)
            {
                player.Velocity = player.Velocity with { Y = 0.0 };
            }
        }

        TryConsumeJump(player, tuning);
        ApplyJumpRelease(player, jumpHeld, tuning);
        if (player.JumpBufferTicksRemaining > 0)
        {
            player.JumpBufferTicksRemaining--;
        }

        if (player.Velocity.Y <= 0.0)
        {
            player.JumpCutAvailable = false;
        }
        player.WasJumpHeld = jumpHeld;
    }

    private static void ApplyHorizontalIntent(Player player, sbyte moveAxis, PlayerTuning tuning)
    {
        var axis = moveAxis < 0 ? -1.0 : moveAxis > 0 ? 1.0 : 0.0;
        if (axis == 0.0)
        {
            if (player.IsGrounded)
            {
                var retained = player.Velocity.X * tuning.GroundVelocityRetention;
                player.Velocity = player.Velocity with
                {
                    X = System.Math.Abs(retained) < VelocityStopEpsilon ? 0.0 : retained,
                };
            }
            return;
        }

        var acceleration = tuning.GroundAcceleration * (player.IsGrounded ? 1.0 : tuning.AirControlRatio);
        var target = axis * tuning.RunSpeed;
        player.Velocity = player.Velocity with
        {
            X = MoveTowards(player.Velocity.X, target, acceleration),
        };
    }

    private static void TryConsumeJump(Player player, PlayerTuning tuning)
    {
        if (player.JumpBufferTicksRemaining <= 0 || player.JumpsRemaining <= 0)
        {
            return;
        }

        player.JumpsRemaining--;
        player.JumpBufferTicksRemaining = 0;
        player.IsGrounded = false;
        player.JumpCutAvailable = true;
        player.Velocity = player.Velocity with { Y = tuning.JumpSpeed };
    }

    private static void ApplyJumpRelease(Player player, bool jumpHeld, PlayerTuning tuning)
    {
        if (!jumpHeld && player.JumpCutAvailable && player.Velocity.Y > 0.0)
        {
            player.Velocity = player.Velocity with { Y = player.Velocity.Y * tuning.JumpReleaseFactor };
            player.JumpCutAvailable = false;
        }
    }

    private static void MoveAndSlide(Player player, IReadOnlyList<Obb> boxes, PlayerTuning tuning)
    {
        var remaining = player.Velocity;
        player.IsGrounded = false;
        for (var iteration = 0; iteration < tuning.MoveIterations; iteration++)
        {
            if (remaining.LengthSquared == 0.0)
            {
                break;
            }

            var hit = Collision.SweepCircle(player.Position, tuning.Radius, remaining, boxes);
            if (!hit.HasHit)
            {
                player.Position += remaining;
                break;
            }

            var clampedTime = System.Math.Clamp(hit.Time, 0.0, 1.0);
            player.Position += remaining * clampedTime;
            player.Position += hit.Normal * (hit.Separation + tuning.CollisionSkin);
            var velocityIntoSurface = Vec2.Dot(player.Velocity, hit.Normal);
            if (velocityIntoSurface < 0.0)
            {
                player.Velocity -= hit.Normal * velocityIntoSurface;
            }

            var remainder = remaining * (1.0 - clampedTime);
            var remainderIntoSurface = Vec2.Dot(remainder, hit.Normal);
            remaining = remainderIntoSurface < 0.0
                ? remainder - (hit.Normal * remainderIntoSurface)
                : remainder;
            if (hit.Normal.Y >= tuning.GroundNormalThreshold)
            {
                player.IsGrounded = true;
            }
        }
    }

    private static bool ProbeGround(Player player, IReadOnlyList<Obb> boxes, PlayerTuning tuning)
    {
        var hit = Collision.SweepCircle(
            player.Position,
            tuning.Radius,
            new Vec2(0.0, -tuning.GroundProbeDistance),
            boxes);
        return hit.HasHit && hit.Normal.Y >= tuning.GroundNormalThreshold;
    }

    private static double MoveTowards(double current, double target, double maximumDelta)
    {
        if (current < target)
        {
            return System.Math.Min(current + maximumDelta, target);
        }
        if (current > target)
        {
            return System.Math.Max(current - maximumDelta, target);
        }
        return current;
    }
}
