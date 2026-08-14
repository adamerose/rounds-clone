using Godot;
using Rounds.Sim;

namespace Rounds.Game;

public partial class Main : Node2D
{
    private static readonly Color Paper = Color.FromHtml("f2f0e8");
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
        DrawRect(new Rect2(0.0f, 790.0f, 620.0f, 54.0f), Paper);
        DrawRect(new Rect2(1300.0f, 790.0f, 620.0f, 54.0f), Paper);
        DrawSetTransform(new Vector2(960.0f, 760.0f), -0.35f);
        DrawRect(new Rect2(-34.0f, -150.0f, 68.0f, 300.0f), Paper);
        DrawSetTransform(Vector2.Zero, 0.0f);
        DrawFighter(new Vector2(360.0f, 720.0f), Red, facing: 1.0f);
        DrawFighter(new Vector2(1560.0f, 720.0f), Blue, facing: -1.0f);
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

    private void DrawFighter(Vector2 center, Color color, float facing)
    {
        DrawCircle(center, 58.0f, Colors.Black);
        DrawCircle(center, 51.0f, color);
        DrawCircle(center + new Vector2(15.0f * facing, -8.0f), 7.0f, Colors.Black);
        DrawLine(center + new Vector2(37.0f * facing, 5.0f), center + new Vector2(82.0f * facing, -8.0f), Colors.Black, 14.0f);
        DrawCircle(center + new Vector2(87.0f * facing, -10.0f), 13.0f, Colors.Black);
    }
}
