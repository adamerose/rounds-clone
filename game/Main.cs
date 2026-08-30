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
    private FaithfulSubsetMatchShell? _matchShell;
    private AgentPlaytestSession? _agentPlaytest;
    private ReplayPlayback? _replay;
    private StartupMode _startupMode;
    private World _world = null!;

    public override void _Ready()
    {
        PlaceWindowOnPreferredScreen();
        Engine.PhysicsTicksPerSecond = World.TickRate;
        var arguments = OS.GetCmdlineUserArgs();
        StartupRoute route;
        try
        {
            route = StartupRoute.Parse(arguments, OS.IsDebugBuild());
        }
        catch (ArgumentException)
        {
            FailReplay(StartupRoute.Usage);
            return;
        }
        _startupMode = route.Mode;

        if (route.Mode == StartupMode.Replay)
        {
            try
            {
                using var stream = File.OpenRead(route.ReplayPath!);
                _replay = new ReplayPlayback(ReplayCodec.Load(stream));
                _world = _replay.World;
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
            {
                FailReplay(exception.Message);
                return;
            }
        }
        else if (route.Mode == StartupMode.DebugIncompleteFidelityEvidence)
        {
            _matchShell = DebugEvidenceMatchFactory.CreateIncompleteFidelityBoundary();
            _match = _matchShell.Match;
            _world = _match.World;
        }
        else if (route.Mode == StartupMode.DebugAgentPlaytest)
        {
            _agentPlaytest = new AgentPlaytestSession();
            _match = _agentPlaytest.Match;
            _world = _match.World;
        }
        else
        {
            _match = Match.Create(1UL);
            _matchShell = new FaithfulSubsetMatchShell(_match);
            _world = _match.World;
        }
        QueueRedraw();
        if (!route.RunsContinuousPhysics)
        {
            SetPhysicsProcess(false);
            if (route.Mode == StartupMode.DebugAgentPlaytest)
            {
                RefuseUnavailableAgentPlaytestRenderer(route.DebugAgentPlaytestOutputRoot!);
            }
            else
            {
                _ = CaptureDebugEvidenceAsync(route.DebugEvidenceOutputPath!);
            }
        }
    }

    private void RefuseUnavailableAgentPlaytestRenderer(string outputRoot)
    {
        AgentPlaytestArtifactOwner? owner = null;
        AgentPlaytestErrorResponse response;
        try
        {
            owner = AgentPlaytestArtifactOwner.Create(outputRoot);
            response = AgentPlaytestErrors.Create(0, "renderer", "renderer-unavailable");
        }
        catch (Exception)
        {
            response = AgentPlaytestErrors.Create(0, "resource", "resource-limit-exceeded");
        }
        if (owner is not null)
        {
            var cleanupFailed = false;
            try
            {
                owner.CleanupFailedRun();
            }
            catch (Exception)
            {
                cleanupFailed = true;
                response = AgentPlaytestErrors.Create(0, "lifecycle", "cleanup-failed");
            }
            if (!cleanupFailed)
            {
                owner.Dispose();
            }
        }

        var line = AgentPlaytestNdjson.SerializeResponse(response);
        using var output = Console.OpenStandardOutput();
        output.Write(line);
        output.Flush();
        GC.KeepAlive(owner);
        Console.Error.WriteLine("Agent playtest renderer-backed owner is unavailable; no perceptual evidence was produced.");
        GetTree().Quit(1);
    }

    private async Task CaptureDebugEvidenceAsync(string outputPath)
    {
        if (File.Exists(outputPath))
        {
            FinishDebugEvidenceWithError("output-exists", 0);
            return;
        }

        try
        {
            if (DisplayServer.GetName() == "headless")
            {
                FinishDebugEvidenceWithError("renderer-unavailable", 0);
                return;
            }

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw();
            var image = GetViewport().GetTexture().GetImage();
            if (image.GetWidth() <= 0 || image.GetHeight() <= 0)
            {
                FinishDebugEvidenceWithError("empty-viewport", 0);
                return;
            }

            var screen = DisplayServer.WindowGetCurrentScreen();
            var windowPosition = DisplayServer.WindowGetPosition();
            var windowSize = DisplayServer.WindowGetSize();
            if (screen != PreferredScreen)
            {
                GD.Print(DebugEvidenceCaptureProtocol.WrongScreenMarker(screen, PreferredScreen));
                GetTree().Quit(1);
                return;
            }

            var error = image.SavePng(outputPath);
            if (error != Error.Ok)
            {
                FinishDebugEvidenceWithError("save-png", (int)error);
                return;
            }

            var attestation = new DebugEvidenceCaptureAttestation(
                screen,
                windowPosition.X,
                windowPosition.Y,
                windowSize.X,
                windowSize.Y,
                image.GetWidth(),
                image.GetHeight());
            GD.Print(DebugEvidenceCaptureProtocol.CompleteMarker(attestation));
            GetTree().Quit();
        }
        catch (Exception)
        {
            FinishDebugEvidenceWithError("capture", 1);
        }
    }

    private void FinishDebugEvidenceWithError(string stage, int code)
    {
        GD.Print(DebugEvidenceCaptureProtocol.ErrorMarker(stage, code));
        GetTree().Quit(1);
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
        if (_startupMode == StartupMode.DebugAgentPlaytest)
        {
            throw new InvalidOperationException("The agent-playtest route must never enter ordinary physics/input processing.");
        }
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
        _matchShell!.Step(_inputs);
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
        if (_matchShell?.IsAtIncompleteFidelityBoundary == true)
        {
            DrawIncompleteFidelityBoundary();
        }
        else if (_match?.Phase is MatchPhase.OpeningDraft or MatchPhase.LoserDraft)
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
    }

    private void DrawMatchScore(Match match)
    {
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(710.0f, 62.0f),
            $"{match.FullPoints[0]}   ROUNDS   {match.FullPoints[1]}",
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
                _displayCards.GetRequired(card.Id).DisplayName,
                HorizontalAlignment.Center,
                bounds.Size.X - 40.0f,
                25,
                selected ? pickerColor : Paper);
        }
    }

    private void DrawIncompleteFidelityBoundary()
    {
        DrawRect(new Rect2(0.0f, 250.0f, 1920.0f, 560.0f), Ink with { A = 0.96f });
        DrawIncompleteFidelityLine(FaithfulSubsetMatchShell.IncompleteFidelityHeadlineLine1, 410.0f, 30, Paper);
        DrawIncompleteFidelityLine(FaithfulSubsetMatchShell.IncompleteFidelityHeadlineLine2, 455.0f, 30, Paper);
        DrawIncompleteFidelityLine(FaithfulSubsetMatchShell.IncompleteFidelityHeadlineLine3, 500.0f, 30, Paper);
        var subtitleColor = Paper.Darkened(0.1f);
        DrawIncompleteFidelityLine(FaithfulSubsetMatchShell.IncompleteFidelitySubtitleLine1, 610.0f, 24, subtitleColor);
        DrawIncompleteFidelityLine(FaithfulSubsetMatchShell.IncompleteFidelitySubtitleLine2, 645.0f, 24, subtitleColor);
        DrawIncompleteFidelityLine(FaithfulSubsetMatchShell.IncompleteFidelitySubtitleLine3, 680.0f, 24, subtitleColor);
    }

    private void DrawIncompleteFidelityLine(string text, float baselineY, int fontSize, Color color) =>
        DrawString(
            ThemeDB.FallbackFont,
            new Vector2(260.0f, baselineY),
            text,
            HorizontalAlignment.Center,
            1400.0f,
            fontSize,
            color);

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
