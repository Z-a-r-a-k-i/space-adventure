using System.Globalization;
using System.Text.Json;
using Godot;
using SpaceAdventure.Core;
using SpaceAdventure.SimCli;

namespace SpaceAdventure.Game;

/// <summary>Bounded headless smoke, graphical capture/input/resize, performance and station-handoff profiles.</summary>
public partial class ShipBattleHost
{
    private readonly List<(string Name, bool Passed)> _checks = [];
    private readonly List<double> _processMilliseconds = [];
    private readonly List<double> _frameMilliseconds = [];
    private bool _measuring;
    private double _autoQuitSeconds;

    /// <summary>Station crew IDs the handoff review expects to see in the battle.</summary>
    public IReadOnlyList<string>? ExpectedCrewIds { get; set; }

    private void StartAutomationIfRequested()
    {
        foreach (var argument in _arguments)
        {
            if (argument.StartsWith("--auto-quit-seconds=", StringComparison.Ordinal)
                && double.TryParse(argument["--auto-quit-seconds=".Length..], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            { _autoQuitSeconds = seconds; }
        }
        if (HandoffReviewMode is not null) { ReviewDrivesClock = true; _ = RunGuarded(RunHandoffReviewAsync); }
        else if (_arguments.Contains("--ship-battle-smoke")) { ReviewDrivesClock = true; _ = RunGuarded(RunSmokeAsync); }
        else if (_arguments.Contains("--ship-review=capture")) { ReviewDrivesClock = true; _ = RunGuarded(RunCaptureAsync); }
        else if (_arguments.Contains("--ship-review=input")) { ReviewDrivesClock = true; _ = RunGuarded(RunInputReviewAsync); }
        else if (_arguments.Contains("--ship-review=performance")) { _ = RunGuarded(RunPerformanceAsync); }
        else if (_autoQuitSeconds > 0) { GetTree().CreateTimer(_autoQuitSeconds).Timeout += () => GetTree().Quit(); }
    }

    private async Task RunGuarded(Func<Task> review)
    {
        try { await review(); }
        catch (Exception exception)
        {
            GD.Print($"[ship-review] FAIL exception {exception}");
            GetTree().Quit(1);
        }
    }

    private void Check(string name, bool passed)
    {
        _checks.Add((name, passed));
        GD.Print($"[ship-review] {(passed ? "PASS" : "FAIL")} {name}");
    }

    private void Finish(string kind, object extra)
    {
        var passed = _checks.All(item => item.Passed);
        GD.Print(JsonSerializer.Serialize(new { kind, passed, checks = _checks.Count, failed = _checks.Where(item => !item.Passed).Select(item => item.Name), extra }));
        GetTree().Quit(passed ? 0 : 1);
    }

    private async Task WaitFrames(int count)
    {
        for (var index = 0; index < count; index++) { await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame); }
    }

    private void StepFrames(int frames, ShipBattlePilot? pilot = null)
    {
        for (var frame = 0; frame < frames && _session.Phase == ShipBattlePhase.Active; frame++)
        {
            var before = _session.Tick;
            _session.Advance(TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60));
            if (pilot is not null && _session.Tick / ShipCombatSession.TicksPerSecond != before / ShipCombatSession.TicksPerSecond) { pilot.React(); }
        }
    }

    private void CheckAssets()
    {
        GD.Print($"[ship-review] player_bounds {_playerView.Bounds} enemy_bounds {_enemyView.Bounds}");
        Check("player publication bounds within contract (<=7x12.5 m)", _playerView.BoundsWithinContract);
        Check("enemy publication bounds within contract (<=6x12 m)", _enemyView.BoundsWithinContract);
        Check("player publication exposes every door leaf", _definition.Doors.All(door => _playerView.HasDoorLeaf(door.Id)));
        Check("player publication exposes work/system/effect anchors", ShipBattleDefinition.PlayerSystemIds.All(id =>
            _playerView.Anchors.ContainsKey($"work_{id}") && _playerView.Anchors.ContainsKey($"system_{id}") && _playerView.Anchors.ContainsKey($"effect_{id}")));
        Check("enemy publication exposes system and room anchors", ShipBattleDefinition.EnemySystemIds.All(id =>
            _enemyView.Anchors.ContainsKey($"system_{id}") && _enemyView.Anchors.ContainsKey($"room_{id}")));
        Check("enemy system anchors sit inside their targetable rooms", ShipBattleDefinition.EnemySystemIds.All(id =>
            EnemyRoomRect(id).HasPoint(new Vector2(EnemySystemPosition(id).X, EnemySystemPosition(id).Z))));
        GD.Print($"[ship-review] missing_publications {string.Join(",", _missingPublications)}");
    }

    private void CheckLayout(string label)
    {
        var screen = GetViewportRect();
        var panels = HudPanels();
        foreach (var view in new[] { _playerView, _enemyView })
        {
            var name = view == _playerView ? "player" : "enemy";
            Check($"{label}: {name} ship and shield bubble fit the HUD-safe area at maximum zoom-out",
                view.IsFramedAll && view.FitsAtMaximumZoomOut(view.Container.Size));
            var silhouette = new Rect2(view.Frame.GetGlobalRect().Position + view.ScreenSilhouette().Position, view.ScreenSilhouette().Size);
            var covered = panels.Where(panel => panel.Rect.Intersects(silhouette.Grow(-2))).Select(panel => panel.Name).ToArray();
            Check($"{label}: {name} hull is clear of HUD panels{(covered.Length > 0 ? " (" + string.Join(", ", covered) + ")" : "")}", covered.Length == 0);
        }
        var hidden = _controls.Where(control => control.IsVisibleInTree() && !screen.Encloses(control.GetGlobalRect())).Select(control => control.Name.ToString()).ToArray();
        Check($"{label}: all {_controls.Count} controls fully on screen{(hidden.Length > 0 ? " (" + string.Join(", ", hidden) + ")" : "")}", hidden.Length == 0);
        var overlaps = panels.SelectMany((a, i) => panels.Skip(i + 1).Where(b => a.Rect.Intersects(b.Rect)).Select(b => $"{a.Name}/{b.Name}")).ToArray();
        Check($"{label}: HUD panels do not overlap{(overlaps.Length > 0 ? " (" + string.Join(", ", overlaps) + ")" : "")}", overlaps.Length == 0);
    }

    private List<(string Name, Rect2 Rect)> HudPanels()
    {
        var hud = GetNode<Control>("Hud");
        return hud.GetChildren().OfType<Control>().Where(control => control.Visible && control != _pausedFrame && control is not Label)
            .Select(control => (control.Name.ToString(), control.GetGlobalRect())).ToList();
    }

    private async Task RunSmokeAsync()
    {
        var defeat = _arguments.Contains("--review-sequence=defeat");
        await WaitFrames(2);
        var initial = _session.Observe();
        Check("battle starts paused at tick 0", initial.Paused && initial.Tick == 0 && initial.Attempt == 1);
        for (var frame = 0; frame < 60; frame++) { _session.Advance(TimeSpan.FromSeconds(1 / 60.0)); }
        Check("paused frames never advance simulation", _session.Tick == 0);
        CheckAssets();
        AdjustPower("engines", -1);
        AdjustPower("life_support", 1);
        Check("paused power edit is atomic and applied", _session.Observe().Player.Systems.First(item => item.Id == "life_support").AllocatedPower == 2 && _session.Tick == 0);
        AdjustPower("life_support", -1);
        AdjustPower("engines", 1);
        AdjustPower("weapons", 1);
        Check("power beyond capacity is rejected", _session.Observe().Player.Systems.First(item => item.Id == "weapons").AllocatedPower == 3);
        ClickEnemy(EnemySystemPosition("engines"));
        Check("enemy room click with no weapon chosen aims every weapon", _session.Observe().Player.Weapons.All(weapon => weapon.Target == "engines"));
        BeginAim(_definition.Player.Weapons[0].Id);
        ClickEnemy(EnemySystemPosition("shields"));
        Check("chosen weapon alone retargets", _session.Observe().Player.Weapons[0].Target == "shields" && _session.Observe().Player.Weapons[1].Target == "engines" && AimingWeapon is null);
        ToggleWeaponPower(_definition.Player.Weapons[0].Id);
        Check("right-click weapon power toggle unpowers it", !_session.Observe().Player.Weapons[0].Powered);
        ToggleWeaponPower(_definition.Player.Weapons[0].Id);
        Check("room picking resolves engines", RoomAt(RoomCentre("engines")) == "engines");
        Check("enemy room picking resolves every system", ShipBattleDefinition.EnemySystemIds.All(id => EnemyRoomAt(EnemySystemPosition(id)) == id));
        Check("orders while paused do not advance time", _session.Tick == 0);
        var pilot = new ShipBattlePilot(_session, defeat ? ShipBattleStrategy.Passive : ShipBattleStrategy.SuppressWeapons);
        pilot.Open();
        var frames = 0;
        while (_session.Phase == ShipBattlePhase.Active && frames < 5 * 60 * 60)
        {
            StepFrames(60, pilot);
            frames += 60;
            Synchronize();
        }
        var final = _session.Observe();
        Check($"battle reaches {(defeat ? "defeat" : "victory")} within five minutes", final.Phase == (defeat ? ShipBattlePhase.Defeat : ShipBattlePhase.Victory));
        var terminalTick = _session.Tick;
        // The destruction sequence runs on a bounded presentation-only clock; the simulation stays frozen.
        for (var frame = 0; frame < 5 * 60; frame++) { _session.Advance(TimeSpan.FromSeconds(1 / 60.0)); AdvanceTerminalClock(1 / 60.0); Synchronize(); }
        Check($"terminal outcome freezes simulation; destruction effects finish within the bounded clock (tick {_session.Tick - terminalTick:+0;-0;0}, "
            + $"clock {TerminalPresentationSeconds:0.00}s, projectiles {_projectiles.Count}, glows {_glows.Count}, texts {_texts.Count})",
            _session.Tick == terminalTick && final.Shots.Count == 0 && TerminalPresentationSeconds >= 3.99 && ActiveEffectCount == 0);
        Check("losing ship is destroyed and the outcome panel is shown", (defeat ? !_playerView.Ship.Visible : !_enemyView.Ship.Visible) && _terminalOverlay.Visible);
        Send(new ShipRestartCommand(NextCommandId("smoke")));
        Synchronize();
        var retried = _session.Observe();
        Check("battle-only retry resets to paused tick 0 with effects cleared", retried.Attempt == 2 && retried.Tick == 0 && retried.Paused
            && retried.Player.Hull == retried.Player.MaxHull && retried.Crew.All(crew => crew.Health == crew.MaxHealth) && ActiveEffectCount == 0
            && _playerView.Ship.Visible && _enemyView.Ship.Visible && !_terminalOverlay.Visible);
        Finish("ship_smoke", new { terminal_seconds = terminalTick / 30.0, phase = final.Phase.ToString(), player_hull = final.Player.Hull, enemy_hull = final.Enemy.Hull });
    }

    private async Task<string> Capture(string name)
    {
        await WaitFrames(3);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\'), "..", "artifacts", "ship-review"));
        System.IO.Directory.CreateDirectory(directory);
        var file = System.IO.Path.Combine(directory, $"{name}.png");
        var saved = GetViewport().GetTexture().GetImage().SavePng(file) == Error.Ok;
        Check($"capture {name} saved", saved);
        GD.Print($"[ship-review] capture {file}");
        return file;
    }

    private void WriteManifest(string file, object extra)
    {
        var observation = _session.Observe();
        System.IO.File.WriteAllText(System.IO.Path.ChangeExtension(file, ".json"), JsonSerializer.Serialize(new
        {
            capture = System.IO.Path.GetFileName(file), window = DisplayServer.WindowGetSize().ToString(), logical = GetViewportRect().Size.ToString(), tick = observation.Tick, paused = observation.Paused,
            player_bounds = _playerView.Bounds.ToString(), enemy_bounds = _enemyView.Bounds.ToString(), missing_publications = _missingPublications,
            hazards = observation.Rooms.Where(room => room.FireSeverity + room.BreachSeverity > 0).Select(room => new { room.Id, room.FireSeverity, room.BreachSeverity }),
            crew = observation.Crew.Select(crew => new
            {
                crew.Id, work = crew.CurrentWork.ToString(), posed = _crewViews[crew.Id].PosedWork.ToString(), weight = _crewViews[crew.Id].WorkWeight,
                pose = _crewViews[crew.Id].Presentation.GetDiagnostics().ToString(),
            }),
            checks = _checks.Select(item => new { name = item.Name, passed = item.Passed }),
            extra,
        }));
    }

    /// <summary>Plays the ordinary battle with the suppress pilot until a crew member is working a hazard, then pauses.</summary>
    private void PlayUntilHazardWork()
    {
        // Silencing the interceptor early prevents every hazard in the first battle; the volley pilot lets one land.
        var pilot = new ShipBattlePilot(_session, ShipBattleStrategy.OverwhelmDefenses);
        pilot.Open();
        Select(DefaultCrew[2].Id, false);
        var frames = 0;
        while (_session.Phase == ShipBattlePhase.Active && frames++ < 200 * 60
            && !_session.Observe().Crew.Any(crew => crew.CurrentWork is ShipTaskKind.Extinguish or ShipTaskKind.Seal))
        { StepFrames(1, pilot); }
        StepFrames(24, pilot);
        Send(new ShipSetPauseCommand(NextCommandId("review"), true));
    }

    /// <summary>Plays the suppress pilot until projectiles from both ships are in flight, then pauses.</summary>
    private void PlayUntilProjectilesInFlight()
    {
        var pilot = new ShipBattlePilot(_session, ShipBattleStrategy.SuppressWeapons);
        pilot.Open();
        var frames = 0;
        while (_session.Phase == ShipBattlePhase.Active && frames++ < 60 * 60)
        {
            StepFrames(1, pilot);
            Synchronize();
            var shots = _session.Observe().Shots;
            if (shots.Any(shot => shot.Source == "player" && PresentationTick - shot.LaunchTick > 12)
                && shots.Any(shot => shot.Source == "enemy" && PresentationTick > shot.LaunchTick + 4)) { break; }
        }
        Send(new ShipSetPauseCommand(NextCommandId("review"), true));
    }

    private async Task RunCaptureAsync()
    {
        await WaitFrames(10);
        CheckAssets();
        CheckLayout("initial");
        var size = DisplayServer.WindowGetSize();
        await CheckMissFeedbackAsync();
        PlayUntilProjectilesInFlight();
        await WaitFrames(6);
        Check("projectiles are visible in flight", _projectiles.Values.Any(item => item.SourceHead.Visible || item.TargetHead.Visible));
        var combat = await Capture($"ship-combat-{size.X}x{size.Y}");
        _session.Execute(new ShipRestartCommand(NextCommandId("review")));
        Synchronize();
        PlayUntilHazardWork();
        await WaitFrames(20);
        var paused = PresentationTick;
        var tick = _session.Tick;
        await WaitFrames(20);
        Check("pause freezes battle tick and presentation clock", _session.Tick == tick && PresentationTick == paused);
        Check("some crew member shows a hazard work pose", _crewViews.Values.Any(view => view.PosedWork is ShipTaskKind.Extinguish or ShipTaskKind.Seal && view.WorkWeight > .99f));
        CheckLayout("hazard");
        var file = await Capture($"ship-battle-{size.X}x{size.Y}");
        WriteManifest(file, new { profile = "capture", combat });
        var worker = _session.Observe().Crew.FirstOrDefault(crew => crew.CurrentWork is ShipTaskKind.Extinguish or ShipTaskKind.Seal) ?? _session.Observe().Crew[0];
        _playerView.FocusOn(new Vector2((float)worker.X, (float)worker.Z), .32f);
        await WaitFrames(4);
        var closeup = await Capture($"ship-crew-closeup-{size.X}x{size.Y}");
        FrameBoth();
        Finish("ship_capture", new { file, combat, closeup });
    }

    private async Task CheckMissFeedbackAsync()
    {
        foreach (var fractional in new[] { false, true })
        {
            _session.Execute(new ShipRestartCommand(NextCommandId("review")));
            Synchronize();
            var pilot = new ShipBattlePilot(_session, ShipBattleStrategy.SuppressWeapons);
            pilot.Open();
            var misses = 0;
            for (var step = 0; step < 60 * ShipCombatSession.TicksPerSecond && misses == 0 && _session.Phase == ShipBattlePhase.Active; step++)
            {
                _session.AdvanceTicks(1);
                pilot.React();
                misses = _session.EventsSince(_eventCursor).Count(item => item.Kind == "shot_missed");
                if (misses > 0 && fractional) { _session.Advance(TimeSpan.FromMilliseconds(1)); }
                Synchronize();
            }
            Check($"miss feedback appears at {(fractional ? "fractional" : "integer")} tick", misses > 0 && _texts.Count(item => item.Text == "MISS") == misses);
            Send(new ShipSetPauseCommand(NextCommandId("review"), true));
            await WaitFrames(12);
            Check("paused miss feedback does not repeat", _texts.Count(item => item.Text == "MISS") == misses);
            Check("paused floating feedback remains drawable", FloatingTexts(_playerView, PresentationTick).Count()
                + FloatingTexts(_enemyView, PresentationTick).Count() == _texts.Count);
            if (fractional) { await Capture("ship-miss-feedback"); }
        }
        _session.Execute(new ShipRestartCommand(NextCommandId("review")));
        Synchronize();
    }

    private async Task RunInputReviewAsync()
    {
        await WaitFrames(20);
        PushKey(Key.Tab);
        await WaitFrames(2);
        Check("Tab selects first crew", Selected.Count == 1 && Selected[0] == DefaultCrew[0].Id);
        PushClick(_playerView, RoomCentre("engines"), MouseButton.Right);
        await WaitFrames(2);
        Check("right-click orders move to engines", _session.Observe().Crew[0].Destination == "engines");
        PushClickControl(_weaponCards[0]);
        await WaitFrames(2);
        Check("clicking a weapon card starts aiming it", AimingWeapon == _definition.Player.Weapons[0].Id);
        PushClick(_enemyView, EnemySystemPosition("shields"), MouseButton.Left);
        await WaitFrames(2);
        Check("left-click enemy shields room aims that weapon", _session.Observe().Player.Weapons[0].Target == "shields" && AimingWeapon is null);
        PushKey(Key.Key2);
        await WaitFrames(2);
        PushClick(_enemyView, EnemySystemPosition("weapons"), MouseButton.Left);
        await WaitFrames(2);
        Check("key 2 then enemy weapons room aims the second weapon", _session.Observe().Player.Weapons[1].Target == "weapons");
        PushClickControl(_power, _power.ColumnRect("engines").GetCenter());
        await WaitFrames(2);
        Check("clicking a full power column is rejected, never over-allocates", _session.Observe().Player.Systems.Single(item => item.Id == "engines").AllocatedPower == 2);
        PushClickControl(_power, _power.ColumnRect("engines").GetCenter(), MouseButton.Right);
        await WaitFrames(2);
        Check("right-click power column removes a bar", _session.Observe().Player.Systems.Single(item => item.Id == "engines").AllocatedPower == 1);
        PushKey(Key.H);
        await WaitFrames(2);
        Check("H holds the volley", _session.Observe().Player.HoldFire);
        PushKey(Key.H);
        await WaitFrames(2);
        var door = _definition.Doors.First(item => item.Id == "door_shields");
        PushClick(_playerView, new Vector3((float)door.X, ShipBattleView.FloorY, (float)door.Z), MouseButton.Left);
        await WaitFrames(2);
        Check("left-click door toggles it", _session.Observe().Doors.First(item => item.Id == "door_shields").CommandedOpen);
        PushClickControl(_pauseButton);
        await WaitFrames(2);
        Check("Pause button resumes through the essential bar", !_session.Paused);
        PushKey(Key.Space);
        await WaitFrames(2);
        Check("Space pauses", _session.Paused);
        var tick = _session.Tick;
        PushWheel(_playerView, MouseButton.WheelUp);
        await WaitFrames(2);
        Check("wheel zooms the player view only", !_playerView.IsFramedAll && _enemyView.IsFramedAll && _session.Tick == tick);
        PushKey(Key.F);
        await WaitFrames(2);
        Check("F frames both views", _playerView.IsFramedAll && _enemyView.IsFramedAll);
        var files = new List<string>();
        // canvas_items stretch keeps a 1280x720 logical layout; non-16:9 windows letterbox. Record the real window size.
        foreach (var size in new[] { new Vector2I(1920, 1080), new Vector2I(1440, 900), new Vector2I(1280, 720), MinimumWindowSize })
        {
            DisplayServer.WindowSetSize(size);
            await WaitFrames(12);
            var window = DisplayServer.WindowGetSize();
            var logical = GetViewportRect().Size;
            CheckLayout($"window {window.X}x{window.Y} (logical {(int)logical.X}x{(int)logical.Y})");
            files.Add(await Capture($"ship-resize-{window.X}x{window.Y}"));
        }
        Finish("ship_input_review", new { files });
    }

    private async Task RunPerformanceAsync()
    {
        await WaitFrames(30);
        var pilot = new ShipBattlePilot(_session, ShipBattleStrategy.SuppressWeapons);
        pilot.Open();
        _measuring = true;
        var started = Time.GetTicksMsec();
        var lastSecond = 0L;
        while (Time.GetTicksMsec() - started < 20_000 && _session.Phase == ShipBattlePhase.Active)
        {
            await WaitFrames(1);
            if (_session.Tick / ShipCombatSession.TicksPerSecond != lastSecond) { lastSecond = _session.Tick / ShipCombatSession.TicksPerSecond; pilot.React(); }
        }
        _measuring = false;
        var ordered = _processMilliseconds.Order().ToArray();
        var frames = _frameMilliseconds.Order().ToArray();
        double Percentile(double[] values, double p) => values.Length == 0 ? 0 : values[Math.Min(values.Length - 1, (int)(values.Length * p))];
        var result = new
        {
            frames = ordered.Length, wall_seconds = (Time.GetTicksMsec() - started) / 1000.0, ticks = _session.Tick,
            process_ms_avg = ordered.DefaultIfEmpty().Average(), process_ms_p95 = Percentile(ordered, .95), process_ms_max = ordered.DefaultIfEmpty().Max(),
            frame_ms_avg = frames.DefaultIfEmpty().Average(), frame_ms_p95 = Percentile(frames, .95), frame_ms_max = frames.DefaultIfEmpty().Max(),
            size = GetViewportRect().Size.ToString(),
        };
        Check("simulation kept pace with wall time (>= 90% of expected ticks)", _session.Tick >= result.wall_seconds * 30 * .9 || _session.Phase != ShipBattlePhase.Active);
        Check("ship host process p95 below 8 ms", result.process_ms_p95 < 8);
        var directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(ProjectSettings.GlobalizePath("res://").TrimEnd('/', '\\'), "..", "artifacts", "ship-review"));
        System.IO.Directory.CreateDirectory(directory);
        System.IO.File.WriteAllText(System.IO.Path.Combine(directory, "ship-performance.json"), JsonSerializer.Serialize(result));
        Finish("ship_performance", result);
    }

    private void MeasureProcess(long started, double delta)
    {
        if (_measuring) { _frameMilliseconds.Add(delta * 1000); _processMilliseconds.Add((System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000.0 / System.Diagnostics.Stopwatch.Frequency); }
    }

    private async Task RunHandoffReviewAsync()
    {
        await WaitFrames(2);
        var observation = _session.Observe();
        Check("handoff: exactly one battle host started", InstancesStarted == 1 && GetTree().Root.GetChildren().OfType<ShipBattleHost>().Count() == 1);
        Check("handoff: battle is the current scene and the station scene is gone", GetTree().CurrentScene == this && !GetTree().Root.GetChildren().OfType<GameHost>().Any());
        Check("handoff: battle entered paused at tick 0 attempt 1", observation.Paused && observation.Tick == 0 && observation.Attempt == 1);
        Check("handoff: crew identity carried from the station", ExpectedCrewIds is not null && observation.Crew.Select(crew => crew.Id).SequenceEqual(ExpectedCrewIds));
        Check("handoff: fresh full health on entry", observation.Crew.All(crew => crew.Health == crew.MaxHealth));
        Check("handoff: the station arrival intro plays", IntroPlaying);
        // Real time drives the intro; sample it after the interceptor warps in, then let it settle.
        for (var frame = 0; frame < 60 * 8 && IntroPlaying && _introSeconds < WarpInSeconds + .45f; frame++) { await WaitFrames(1); }
        if (HandoffReviewMode == "capture") { await Capture($"ship-intro-{(int)GetViewportRect().Size.X}x{(int)GetViewportRect().Size.Y}"); }
        for (var frame = 0; frame < 60 * 10 && IntroPlaying; frame++) { await WaitFrames(1); }
        Check("handoff: the intro ends settled, framed and with the HUD shown", !IntroPlaying && _playerView.IsFramedAll && _enemyView.IsFramedAll
            && _enemyView.Ship.Visible && GetNode<Control>("Hud").Modulate.A >= .999f);
        for (var frame = 0; frame < 30; frame++) { _session.Advance(TimeSpan.FromSeconds(1 / 60.0)); }
        await WaitFrames(5);
        Check("handoff: no hidden simulation while presented paused", _session.Tick == 0);
        CheckAssets();
        CheckLayout("handoff");
        string? file = null;
        if (HandoffReviewMode == "capture") { file = await Capture($"ship-handoff-{(int)GetViewportRect().Size.X}x{(int)GetViewportRect().Size.Y}"); }
        var pilot = new ShipBattlePilot(_session, ShipBattleStrategy.SuppressWeapons);
        pilot.Open();
        StepFrames(60 * 8, pilot);
        Check("handoff: battle runs after the player resumes", _session.Tick > 200);
        Send(new ShipRestartCommand(NextCommandId("handoff")));
        await WaitFrames(2);
        var retried = _session.Observe();
        Check("handoff: retry is battle-only (attempt 2, tick 0, station not re-entered)", retried.Attempt == 2 && retried.Tick == 0 && retried.Paused
            && InstancesStarted == 1 && !GetTree().Root.GetChildren().OfType<GameHost>().Any()
            && retried.Crew.Select(crew => crew.Id).SequenceEqual(ExpectedCrewIds ?? []));
        if (file is not null) { WriteManifest(file, new { profile = "handoff" }); }
        Finish("ship_handoff", new { file });
    }

    private static void PushKey(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true });
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
    }

    private static Vector2 GlobalPoint(ShipBattleView view, Vector3 world) => view.Container.GetGlobalRect().Position + view.ToScreen(world);

    private void PushClick(ShipBattleView view, Vector3 world, MouseButton button)
    {
        var global = GlobalPoint(view, world);
        foreach (var pressed in new[] { true, false })
        {
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = button, Pressed = pressed, Position = global, GlobalPosition = global,
                ButtonMask = pressed ? (button == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Right) : 0 });
        }
    }

    private void PushWheel(ShipBattleView view, MouseButton button)
    {
        var global = view.Container.GetGlobalRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = button, Pressed = true, Position = global, GlobalPosition = global });
        GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = button, Pressed = false, Position = global, GlobalPosition = global });
    }

    private void PushClickControl(Control control, Vector2? local = null, MouseButton button = MouseButton.Left)
    {
        var global = local is { } point ? control.GetGlobalRect().Position + point : control.GetGlobalRect().GetCenter();
        GetViewport().PushInput(new InputEventMouseMotion { Position = global, GlobalPosition = global });
        foreach (var pressed in new[] { true, false })
        {
            GetViewport().PushInput(new InputEventMouseButton { ButtonIndex = button, Pressed = pressed, Position = global, GlobalPosition = global,
                ButtonMask = pressed ? (button == MouseButton.Left ? MouseButtonMask.Left : MouseButtonMask.Right) : 0 });
        }
    }
}
