using Godot;
using Rounds.Replay;
using Rounds.Sim;
using Rounds.Sim.Cards;
using Rounds.Sim.Maps;
using SimVector = Rounds.Sim.Math.Vec2;

namespace Rounds.Game;

public partial class Main : Node2D
{
    private const int PreferredScreen = 3;
    private static readonly Color Paper = Color.FromHtml("f2f0e8");
    private static readonly Color Ink = Color.FromHtml("10131c");
    private static readonly Color Red = Color.FromHtml("ff625f");
    private static readonly Color Blue = Color.FromHtml("48a9ff");
    private readonly PlayerInput[] _inputs = new PlayerInput[2];
    private readonly StatCardCatalog _displayCards = StatCardCatalog.LoadEmbedded();
    private Match? _match;
    private ReplayPlayback? _replay;
    private World _world = null!;

    public override void _Ready()
    {
        PlaceWindowOnPreferredScreen();
        Engine.PhysicsTicksPerSecond = World.TickRate;
        var arguments = OS.GetCmdlineUserArgs();
        if (arguments.Length > 0)
        {
            if (arguments.Length != 2 || arguments[0] != "--replay")
            {
                FailReplay("Usage: Rounds.Game -- --replay <path>");
                return;
            }

            try
            {
                using var stream = File.OpenRead(arguments[1]);
                _replay = new ReplayPlayback(ReplayCodec.Load(stream));
                _world = _replay.World;
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
            {
                FailReplay(exception.Message);
                return;
            }
        }
        else
        {
            _match = Match.Create(1UL);
            _world = _match.World;
        }
        QueueRedraw();
    }

    private static void PlaceWindowOnPreferredScreen()
    {
        if (DisplayServer.GetScreenCount() > PreferredScreen)
        {
            DisplayServer.WindowSetCurrentScreen(PreferredScreen);
        }
    }

    public override void _ExitTree()
    {
        if (_replay is not null && !_replay.IsComplete)
        {
            GD.PushError($"Replay failed: process terminated after {_replay.ConsumedTicks} of {_replay.Replay.TotalTicks} ticks.");
            GetTree().Quit(1);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _ = delta;
        if (_replay is not null)
        {
            StepReplay();
            return;
        }

        var camera = CameraTransform.For(_world.Arena.CameraBounds);
        var mouseWorld = camera.ToWorld(GetGlobalMousePosition());
        var firstAim = mouseWorld - _world.Players[0].Position;
        _inputs[0] = ReadKeyboard(
            Key.A,
            Key.D,
            Key.Space,
            Godot.Input.IsMouseButtonPressed(MouseButton.Left),
            Godot.Input.IsMouseButtonPressed(MouseButton.Right),
            firstAim);
        _inputs[1] = ReadKeyboard(
            Key.Left,
            Key.Right,
            Key.Up,
            Godot.Input.IsKeyPressed(Key.O),
            Godot.Input.IsKeyPressed(Key.P),
            ReadKeyboardAim());
        _match!.Step(_inputs);
        QueueRedraw();
    }

    private void StepReplay()
    {
        if (_replay is null || _replay.IsComplete)
        {
            return;
        }

        try
        {
            _replay.StepNext();
        }
        catch (Exception exception) when (exception is ReplayMismatchException or InvalidOperationException)
        {
            FailReplay(exception.Message);
            return;
        }

        QueueRedraw();
        if (_replay.IsComplete)
        {
            var hash = Rounds.Sim.Sim.Hash(_replay.World);
            GD.Print(FormattableString.Invariant(
                $"REPLAY_COMPLETE id={_replay.Replay.ReplayId} ticks={_replay.ConsumedTicks} hash={hash:x16} frames={_replay.ConsumedTicks}"));
            SetPhysicsProcess(false);
        }
    }

    private void FailReplay(string message)
    {
        GD.PushError($"Replay failed: {message}");
        GetTree().Quit(1);
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
        foreach (var bullet in _world.Bullets)
        {
            DrawBullet(camera, bullet);
        }

        for (var index = 0; index < _world.Players.Count; index++)
        {
            var player = _world.Players[index];
            var color = index == 0 ? Red : Blue;
            if (player.BlockPhase == BlockPhase.Active)
            {
                DrawArc(
                    camera.ToScreen(player.Position),
                    (float)(_world.Combat.BlockRadius * camera.Scale),
                    0.0f,
                    Mathf.Tau,
                    64,
                    color,
                    8.0f,
                    antialiased: true);
            }
            DrawFighter(
                camera.ToScreen(player.Position),
                color,
                player.AimDirection,
                (float)(_world.Tuning.Radius * camera.Scale),
                player.IsAlive);
        }

        DrawHud();
        if (_match?.Phase is MatchPhase.OpeningDraft or MatchPhase.LoserDraft)
        {
            DrawDraft(_match);
        }
    }

    private static PlayerInput ReadKeyboard(
        Key left,
        Key right,
        Key jump,
        bool fireHeld,
        bool blockHeld,
        SimVector aimDirection)
    {
        var axis = Godot.Input.IsKeyPressed(left) ? (sbyte)-1 : Godot.Input.IsKeyPressed(right) ? (sbyte)1 : (sbyte)0;
        return new PlayerInput(
            axis,
            Godot.Input.IsKeyPressed(jump),
            fireHeld,
            blockHeld,
            aimDirection);
    }

    private static SimVector ReadKeyboardAim()
    {
        var x = Godot.Input.IsKeyPressed(Key.J) ? -1.0 : Godot.Input.IsKeyPressed(Key.L) ? 1.0 : 0.0;
        var y = Godot.Input.IsKeyPressed(Key.I) ? 1.0 : Godot.Input.IsKeyPressed(Key.K) ? -1.0 : 0.0;
        return new SimVector(x, y);
    }

    private void DrawFighter(Vector2 center, Color color, SimVector aim, float radius, bool alive)
    {
        var screenAim = new Vector2((float)aim.X, (float)-aim.Y).Normalized();
        var bodyColor = alive ? color : color.Darkened(0.65f);
        var outline = Mathf.Max(2.0f, radius * 0.12f);
        DrawCircle(center, radius, Ink);
        DrawCircle(center, radius - outline, bodyColor);
        DrawCircle(center + (screenAim * radius * 0.28f), radius * 0.12f, Ink);
        DrawLine(
            center + (screenAim * radius * 0.64f),
            center + (screenAim * radius * 1.40f),
            Ink,
            radius * 0.24f);
        DrawCircle(center + (screenAim * radius * 1.48f), radius * 0.22f, Ink);
    }

    private void DrawBullet(CameraTransform camera, Bullet bullet)
    {
        var center = camera.ToScreen(bullet.Position);
        var velocity = new Vector2((float)bullet.Velocity.X, (float)-bullet.Velocity.Y).Normalized();
        var radius = Mathf.Max(4.0f, (float)(bullet.Radius * camera.Scale));
        var color = bullet.OwnerId == 0 ? Red : Blue;
        DrawLine(center - (velocity * radius * 4.0f), center, color with { A = 0.45f }, radius * 0.9f);
        DrawCircle(center, radius + 2.0f, Ink);
        DrawCircle(center, radius, Paper);
    }

    private void DrawHud()
    {
        DrawPlayerHud(0, new Vector2(70.0f, 70.0f), Red, HorizontalAlignment.Left);
        DrawPlayerHud(1, new Vector2(1850.0f, 70.0f), Blue, HorizontalAlignment.Right);

        var phaseText = _match?.Phase == MatchPhase.MatchResult
            ? _match.WinnerId == 0 ? "RED WINS THE MATCH" : "BLUE WINS THE MATCH"
            : _world.Phase switch
        {
            DuelPhase.Spawning => $"GET READY  {_world.PhaseTicksRemaining}",
            DuelPhase.Resolving => "K.O.",
            DuelPhase.Result when _world.IsDraw => "DRAW",
            DuelPhase.Result => _world.WinnerId == 0 ? "RED WINS" : "BLUE WINS",
            _ => string.Empty,
        };
        if (phaseText.Length > 0)
        {
            DrawString(
                ThemeDB.FallbackFont,
                new Vector2(710.0f, 120.0f),
                phaseText,
                HorizontalAlignment.Center,
                500.0f,
                46,
                Paper);
        }

        if (_match is not null)
        {
            DrawMatchScore(_match);
        }

        var matchDebug = _match is null ? string.Empty : $"   match {_match.Phase}   arena {_world.Arena.Id}";
        var bounceDebug = _world.Bullets.Count == 0
            ? "-"
            : string.Join(',', _world.Bullets.Select(static bullet => bullet.BouncesRemaining));
        var debug = FormattableString.Invariant(
            $"P1 aim {_world.Players[0].AimDirection.X:0.00},{_world.Players[0].AimDirection.Y:0.00}   P2 aim {_world.Players[1].AimDirection.X:0.00},{_world.Players[1].AimDirection.Y:0.00}   bullets {_world.Bullets.Count}   bounces {bounceDebug}   duel {_world.DuelNumber}   phase {_world.Phase}{matchDebug}");
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(380.0f, 1035.0f),
            debug,
            HorizontalAlignment.Center,
            1160.0f,
            18,
            Paper.Darkened(0.2f));
    }

    private void DrawPlayerHud(int playerIndex, Vector2 anchor, Color color, HorizontalAlignment alignment)
    {
        var player = _world.Players[playerIndex];
        var direction = alignment == HorizontalAlignment.Left ? 1.0f : -1.0f;
        var healthWidth = 260.0f;
        var healthFraction = Mathf.Clamp((float)(player.Health / player.CombatProfile.MaximumHealth), 0.0f, 1.0f);
        var barX = direction > 0.0f ? anchor.X : anchor.X - healthWidth;
        DrawRect(new Rect2(barX, anchor.Y, healthWidth, 22.0f), Ink);
        var fillWidth = (healthWidth - 6.0f) * healthFraction;
        var fillX = direction > 0.0f ? barX + 3.0f : barX + healthWidth - 3.0f - fillWidth;
        DrawRect(new Rect2(fillX, anchor.Y + 3.0f, fillWidth, 16.0f), color);

        for (var round = 0; round < player.CombatProfile.MaximumAmmunition; round++)
        {
            var loaded = round < player.Ammo;
            DrawCircle(
                anchor + new Vector2(direction * (12.0f + (round * 28.0f)), 52.0f),
                8.0f,
                loaded ? Paper : Ink.Lightened(0.18f));
        }

        var blockText = player.BlockPhase == BlockPhase.Ready
            ? "BLOCK READY"
            : $"BLOCK {player.BlockPhase.ToString().ToUpperInvariant()} {player.BlockTicksRemaining}";
        var textX = alignment == HorizontalAlignment.Left ? anchor.X : anchor.X - 260.0f;
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(textX, anchor.Y + 90.0f),
            blockText,
            alignment,
            260.0f,
            18,
            color);
    }

    private void DrawMatchScore(Match match)
    {
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(710.0f, 62.0f),
            $"{match.FullPoints[0]}   RICOCHET   {match.FullPoints[1]}",
            HorizontalAlignment.Center,
            500.0f,
            32,
            Paper);
        if (match.HalfPoints[0] > 0)
        {
            DrawCircle(new Vector2(835.0f, 92.0f), 7.0f, Red);
        }
        if (match.HalfPoints[1] > 0)
        {
            DrawCircle(new Vector2(1085.0f, 92.0f), 7.0f, Blue);
        }

        DrawCardStack(match, 0, new Vector2(70.0f, 190.0f), Red, HorizontalAlignment.Left);
        DrawCardStack(match, 1, new Vector2(1850.0f, 190.0f), Blue, HorizontalAlignment.Right);
    }

    private void DrawCardStack(
        Match match,
        int playerId,
        Vector2 anchor,
        Color color,
        HorizontalAlignment alignment)
    {
        var cards = match.AcquiredCardsFor(playerId);
        for (var index = 0; index < cards.Count; index++)
        {
            var definition = _displayCards.GetRequired(cards[index]);
            var x = alignment == HorizontalAlignment.Left ? anchor.X : anchor.X - 220.0f;
            DrawString(
                ThemeDB.FallbackFont,
                new Vector2(x, anchor.Y + (index * 24.0f)),
                definition.DisplayName,
                alignment,
                220.0f,
                17,
                color);
        }
    }

    private void DrawDraft(Match match)
    {
        DrawRect(new Rect2(0.0f, 250.0f, 1920.0f, 560.0f), Ink with { A = 0.96f });
        var pickerColor = match.CurrentPickerId == 0 ? Red : Blue;
        var title = match.Phase == MatchPhase.OpeningDraft
            ? $"PLAYER {match.CurrentPickerId + 1} — OPENING PICK"
            : $"PLAYER {match.CurrentPickerId + 1} — COMEBACK PICK";
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(610.0f, 320.0f),
            title,
            HorizontalAlignment.Center,
            700.0f,
            38,
            pickerColor);
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(610.0f, 765.0f),
            match.IsDraftArmed ? "LEFT / RIGHT TO CHOOSE     JUMP TO TAKE" : "RELEASE MOVE AND JUMP",
            HorizontalAlignment.Center,
            700.0f,
            22,
            Paper.Darkened(0.1f));

        for (var index = 0; index < match.CurrentOffer.Count; index++)
        {
            var card = match.CurrentOffer[index];
            var bounds = new Rect2(115.0f + (index * 345.0f), 370.0f, 310.0f, 300.0f);
            var selected = index == match.SelectedOfferIndex;
            DrawRect(bounds, selected ? pickerColor.Darkened(0.65f) : Ink.Lightened(0.08f));
            DrawRect(bounds, selected ? pickerColor : Paper.Darkened(0.45f), false, selected ? 8.0f : 3.0f);
            DrawString(
                ThemeDB.FallbackFont,
                new Vector2(bounds.Position.X + 20.0f, bounds.Position.Y + 68.0f),
                card.DisplayName,
                HorizontalAlignment.Center,
                bounds.Size.X - 40.0f,
                25,
                selected ? pickerColor : Paper);
            DrawString(
                ThemeDB.FallbackFont,
                new Vector2(bounds.Position.X + 22.0f, bounds.Position.Y + 150.0f),
                card.Summary,
                HorizontalAlignment.Center,
                bounds.Size.X - 44.0f,
                16,
                Paper.Darkened(0.05f));
            DrawString(
                ThemeDB.FallbackFont,
                new Vector2(bounds.Position.X + 22.0f, bounds.Position.Y + 252.0f),
                EffectLine(card),
                HorizontalAlignment.Center,
                bounds.Size.X - 44.0f,
                14,
                Paper.Darkened(0.22f));
        }
    }

    private static string EffectLine(StatCardDefinition card) =>
        string.Join("  ", card.Effects.Select(effect => (effect.Target, effect.Operation) switch
        {
            (_, StatOperation.Multiply) => $"×{effect.Value:0.##}",
            (StatTarget.ProjectileBounces, StatOperation.AddCount) => $"{effect.Value:+0;-0;0} bounces",
            (StatTarget.Ammunition, StatOperation.AddCount) => $"{effect.Value:+0;-0;0} ammo",
            (StatTarget.ReloadTime, StatOperation.AddFlat) => $"+{effect.Value:0.##}s reload",
            _ => $"{effect.Value:+0;-0;0}%",
        }));

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

        public SimVector ToWorld(Vector2 screen) =>
            new(
                ((screen.X - (ViewportWidth / 2.0)) / Scale) + CenterX,
                (((ViewportHeight / 2.0) - screen.Y) / Scale) + CenterY);
    }
}
