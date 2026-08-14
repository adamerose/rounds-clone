using Godot;
using Rounds.Sim;
using Rounds.Sim.Maps;
using SimVector = Rounds.Sim.Math.Vec2;

namespace Rounds.Game;

public partial class Main : Node2D
{
    private static readonly Color Paper = Color.FromHtml("f2f0e8");
    private static readonly Color Ink = Color.FromHtml("10131c");
    private static readonly Color Red = Color.FromHtml("ff625f");
    private static readonly Color Blue = Color.FromHtml("48a9ff");
    private readonly PlayerInput[] _inputs = new PlayerInput[2];
    private World _world = null!;

    public override void _Ready()
    {
        Engine.PhysicsTicksPerSecond = World.TickRate;
        _world = World.CreateSmoke(1UL);
        QueueRedraw();
    }

    public override void _PhysicsProcess(double delta)
    {
        _ = delta;
        _inputs[0] = ReadKeyboard(Key.A, Key.D, Key.W, Key.F, Key.G);
        _inputs[1] = ReadKeyboard(Key.Left, Key.Right, Key.Up, Key.Kp1, Key.Kp2);
        Rounds.Sim.Sim.Step(_world, _inputs);
        QueueRedraw();
    }

    public override void _Draw()
    {
        var camera = CameraTransform.For(_world.Arena.CameraBounds);
        foreach (var box in _world.Arena.StaticBoxes)
        {
            var center = camera.ToScreen(box.Center);
            DrawSetTransform(center, Mathf.DegToRad((float)-box.RotationDegrees));
            DrawRect(
                new Rect2(
                    (float)(-box.HalfExtents.X * camera.Scale),
                    (float)(-box.HalfExtents.Y * camera.Scale),
                    (float)(box.Width * camera.Scale),
                    (float)(box.Height * camera.Scale)),
                Paper);
        }

        DrawSetTransform(Vector2.Zero, 0.0f);
        for (var index = 0; index < _world.Players.Count; index++)
        {
            var player = _world.Players[index];
            var color = index == 0 ? Red : Blue;
            var facing = player.Velocity.X < 0.0 ? -1.0f : 1.0f;
            DrawFighter(
                camera.ToScreen(player.Position),
                color,
                facing,
                (float)(_world.Tuning.Radius * camera.Scale));
        }
    }

    private static PlayerInput ReadKeyboard(Key left, Key right, Key jump, Key fire, Key block)
    {
        var axis = Godot.Input.IsKeyPressed(left) ? (sbyte)-1 : Godot.Input.IsKeyPressed(right) ? (sbyte)1 : (sbyte)0;
        return new PlayerInput(
            axis,
            Godot.Input.IsKeyPressed(jump),
            Godot.Input.IsKeyPressed(fire),
            Godot.Input.IsKeyPressed(block));
    }

    private void DrawFighter(Vector2 center, Color color, float facing, float radius)
    {
        var outline = Mathf.Max(2.0f, radius * 0.12f);
        DrawCircle(center, radius, Ink);
        DrawCircle(center, radius - outline, color);
        DrawCircle(center + new Vector2(radius * 0.25f * facing, -radius * 0.14f), radius * 0.12f, Ink);
        DrawLine(
            center + new Vector2(radius * 0.64f * facing, radius * 0.08f),
            center + new Vector2(radius * 1.40f * facing, -radius * 0.14f),
            Ink,
            radius * 0.24f);
        DrawCircle(center + new Vector2(radius * 1.48f * facing, -radius * 0.17f), radius * 0.22f, Ink);
    }

    private readonly record struct CameraTransform(double Scale, double CenterX, double CenterY)
    {
        private const double ViewportWidth = 1920.0;
        private const double ViewportHeight = 1080.0;
        private const double Margin = 80.0;

        public static CameraTransform For(ArenaBounds bounds)
        {
            var scale = System.Math.Min(
                (ViewportWidth - (2.0 * Margin)) / bounds.Width,
                (ViewportHeight - (2.0 * Margin)) / bounds.Height);
            return new CameraTransform(
                scale,
                (bounds.XMin + bounds.XMax) / 2.0,
                (bounds.YMin + bounds.YMax) / 2.0);
        }

        public Vector2 ToScreen(SimVector world) =>
            new(
                (float)(ViewportWidth / 2.0 + ((world.X - CenterX) * Scale)),
                (float)(ViewportHeight / 2.0 - ((world.Y - CenterY) * Scale)));
    }
}
