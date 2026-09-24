using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

/// <summary>
/// Godot adapter for the pure ship battle. Human input, HUD widgets and automation all funnel through
/// <see cref="Send"/> with the same typed commands. Presentation reads observations and events only.
/// Layout follows the approved separated composition: two full-height overhead views with HUD panels in the
/// corners (FTL-style points and pips), never a bottom deck that shrinks the ships.
/// </summary>
public partial class ShipBattleHost : Control
{
    public const string PlayerModelPath = "res://Assets/Published/ship.escape_cutter.combat.v1.glb";
    public const string EnemyModelPath = "res://Assets/Published/ship.interceptor.v1.glb";
    public const string FireEffectPath = "res://Assets/Published/fx.ship_fire.v1.glb";
    public const string BreachEffectPath = "res://Assets/Published/fx.ship_breach.v1.glb";
    public static readonly Vector2I MinimumWindowSize = new(1152, 648);
    private const float SideColumn = 196;
    private static readonly Dictionary<string, string> CrewModels = new(StringComparer.Ordinal)
    {
        ["actor.protagonist"] = "res://Assets/Published/character.crew.vanguard.v1.glb",
        ["actor.companion.protector"] = "res://Assets/Published/character.crew.protector.v1.glb",
        ["actor.companion.medic"] = "res://Assets/Published/character.crew.operator.v1.glb",
    };
    private static readonly Dictionary<string, string> Portraits = new(StringComparer.Ordinal)
    {
        ["actor.protagonist"] = "res://ui/portraits/vanguard.png",
        ["actor.companion.protector"] = "res://ui/portraits/protector.png",
        ["actor.companion.medic"] = "res://ui/portraits/operator.png",
    };

    private ShipCombatSession _session = null!;
    private ShipBattleDefinition _definition = null!;
    private ShipBattleView _playerView = null!;
    private ShipBattleView _enemyView = null!;
    private readonly List<string> _selected = [];
    private readonly Dictionary<string, ShipCrewPresentation> _crewViews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Node3D> _fireViews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Node3D> _breachViews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MeshInstance3D> _doorViews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MeshInstance3D> _roomTints = new(StringComparer.Ordinal);
    private readonly List<string> _missingPublications = [];
    private readonly Dictionary<string, ShipCrewCard> _cards = new(StringComparer.Ordinal);
    private readonly List<WeaponCard> _weaponCards = [];
    private readonly List<EnemyWeaponRow> _enemyRows = [];
    private readonly List<Control> _controls = [];
    private HullBar _playerHull = null!, _enemyHull = null!;
    private ShieldPips _playerShields = null!, _enemyShields = null!;
    private Label _playerName = null!, _enemyName = null!, _playerStats = null!, _enemyStats = null!, _enemyRepair = null!;
    private Label _clock = null!, _alerts = null!, _feedback = null!, _terminalLabel = null!, _terminalDetail = null!;
    private PowerPanel _power = null!;
    private Button _pauseButton = null!, _holdButton = null!, _airlockButton = null!;
    private Control _pausedFrame = null!;
    private CenterContainer _terminalOverlay = null!;
    private string? _aimingWeapon;
    private long _eventCursor;
    private long _commandSequence;
    private bool _panning;
    private ShipBattleView? _panView;
    private string[] _arguments = [];

    /// <summary>Crew handed over by the station continuation; the direct dev scene uses the station trio.</summary>
    public IReadOnlyList<ShipCrewSeed>? CrewSeeds { get; set; }

    public bool EnteredFromStation { get; set; }

    /// <summary>Set by the station handoff review profile ("smoke" or "capture") to verify the entered battle.</summary>
    public string? HandoffReviewMode { get; set; }

    public static int InstancesStarted { get; private set; }
    public string? InitializationError { get; private set; }
    public ShipCombatSession Session => _session;
    public bool ReviewDrivesClock { get; set; }
    public IReadOnlyList<string> MissingPublications => _missingPublications;

    public static IReadOnlyList<ShipCrewSeed> DefaultCrew { get; } =
    [
        new("actor.protagonist", "Vanguard", false),
        new("actor.companion.protector", "Protector", false),
        new("actor.companion.medic", "Medic", true),
    ];

    public override void _Ready()
    {
        InstancesStarted++;
        _arguments = OS.GetCmdlineUserArgs();
        SetAnchorsPreset(LayoutPreset.FullRect);
        try
        {
            _definition = ShipBattleDefinition.ParseJson(Godot.FileAccess.GetFileAsString("res://content/ship-battle.json"));
            _session = new ShipCombatSession(_definition, CrewSeeds ?? DefaultCrew);
            BuildLayout();
            InitializationError = _playerView.LoadError ?? _enemyView.LoadError;
            if (InitializationError is null) { BuildPlayerShip(); BuildEnemyShip(); }
        }
        catch (Exception exception) when (exception is InvalidDataException or InvalidOperationException or ArgumentException or KeyNotFoundException or System.Text.Json.JsonException)
        {
            InitializationError = $"Ship battle initialization failed: {exception.Message}";
        }
        if (InitializationError is not null)
        {
            GD.Print($"[ship-review] initialization_error {InitializationError}");
            var error = new Label { Text = InitializationError, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            error.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(error);
            SetProcess(false);
            SetProcessInput(false);
            if (_arguments.Any(argument => argument.StartsWith("--ship-", StringComparison.Ordinal))) { GetTree().Quit(1); }
            return;
        }
        GameAudio.Ensure(this);
        if (_missingPublications.Count > 0) { SetFeedback($"Missing publication: {string.Join(", ", _missingPublications.Select(System.IO.Path.GetFileName))}", TacticalUi.Damaged); }
        else { SetFeedback("Paused. Space resumes. Click a weapon, then an enemy room.", TacticalUi.Muted); }
        if (DisplayServer.GetName() != "headless") { DisplayServer.WindowSetMinSize(MinimumWindowSize); }
        CallDeferred(MethodName.FrameBoth);
        StartAutomationIfRequested();
    }

    public ShipCommandResult Send(IShipCommand command)
    {
        var result = _session.Execute(command);
        if (!result.Accepted)
        {
            SetFeedback($"Rejected: {Humanize(result.Rejection.ToString())}", TacticalUi.Damaged);
            GameAudio.Play("ui.deny");
        }
        else if (result.Warning is not null) { SetFeedback($"Warning: {Humanize(result.Warning.Replace("unsafe_route:", "unsafe route through ", StringComparison.Ordinal))}", TacticalUi.Amber); }
        return result;
    }

    public CommandId NextCommandId(string prefix = "human") => new($"{prefix}.ship.{++_commandSequence}");

    public override void _Process(double delta)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        if (!ReviewDrivesClock) { _session.Advance(TimeSpan.FromSeconds(delta)); }
        AdvanceTerminalClock(delta);
        Synchronize();
        MeasureProcess(started, delta);
    }

    public void FrameBoth() { _playerView.FrameAll(); _enemyView.FrameAll(); }

    private void BuildLayout()
    {
        var views = new HBoxContainer { Name = "Views" };
        views.AddThemeConstantOverride("separation", 0);
        views.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(views);
        _playerView = new ShipBattleView("PlayerView", PlayerModelPath, new Vector2(7, 12.5f), TacticalUi.Shield, 1.3f)
        { SafeInsets = new Vector4(SideColumn, 74, 12, 88) };
        _enemyView = new ShipBattleView("EnemyView", EnemyModelPath, new Vector2(6, 12), new Color("ff9a6b"), 7.9f)
        { SafeInsets = new Vector4(12, 74, SideColumn, 16) };
        _playerView.Frame.SizeFlagsStretchRatio = 1.08f;
        _enemyView.Frame.SizeFlagsStretchRatio = .92f;
        views.AddChild(_playerView.Frame);
        views.AddChild(new ColorRect { Name = "Divider", Color = new Color("1b2a36"), CustomMinimumSize = new Vector2(2, 0), MouseFilter = MouseFilterEnum.Ignore });
        views.AddChild(_enemyView.Frame);
        _playerView.Container.GuiInput += input => ViewInput(_playerView, input);
        _enemyView.Container.GuiInput += input => ViewInput(_enemyView, input);
        _playerView.Container.Resized += () => _playerView.UpdateCamera();
        _enemyView.Container.Resized += () => _enemyView.UpdateCamera();

        var hud = new Control { Name = "Hud", MouseFilter = MouseFilterEnum.Ignore };
        hud.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(hud);
        _pausedFrame = new PausedFrame { Name = "PausedFrame" };
        _pausedFrame.SetAnchorsPreset(LayoutPreset.FullRect);
        hud.AddChild(_pausedFrame);

        // Player status: hull points, shield layers, evasion and oxygen (top left).
        var playerStatus = Panel(hud, "PlayerStatus", TacticalUi.Cyan, new Rect2(10, 8, 340, 58));
        (_playerName, _playerHull, _playerShields, _playerStats) = StatusBlock(playerStatus, hostile: false);

        // Crew column, then doors (left).
        var crew = new VBoxContainer { Name = "Crew", MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(10, 76), Size = new Vector2(176, 180) };
        crew.AddThemeConstantOverride("separation", 6);
        hud.AddChild(crew);
        var number = 0;
        foreach (var member in _session.Observe().Crew)
        {
            var portrait = Portraits.TryGetValue(member.Id, out var path) && ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
            var card = new ShipCrewCard(member.Id, portrait, CrewAccent(member.Id), ++number) { Name = $"Crew_{number}" };
            var id = member.Id;
            card.Pressed += () => { Select(id, Input.IsKeyPressed(Key.Shift)); GameAudio.Play("ui.select"); };
            crew.AddChild(card);
            _cards[id] = card;
            _controls.Add(card);
        }
        var doors = new VBoxContainer { Name = "Doors", MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(10, 262), Size = new Vector2(176, 90) };
        doors.AddThemeConstantOverride("separation", 4);
        hud.AddChild(doors);
        doors.AddChild(TacticalUi.Eyebrow("DOORS  (click a door on the ship)"));
        var doorRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        doorRow.AddThemeConstantOverride("separation", 4);
        doors.AddChild(doorRow);
        OrderButton(doorRow, "OpenAllDoors", "Open all", () => SetAllDoors(true), 86, "ship/door");
        OrderButton(doorRow, "CloseAllDoors", "Close all", () => SetAllDoors(false), 86, "ship/door");
        _airlockButton = OrderButton(doors, "Airlock", "Airlock sealed", ToggleAirlock, 176, "ship/vent");

        // Reactor and system power (bottom left).
        _power = new PowerPanel { Name = "Power" };
        _power.Position = new Vector2(10, 720 - 200);
        _power.Size = new Vector2(188, 192);
        _power.PowerRequested += AdjustPower;
        hud.AddChild(_power);
        _controls.Add(_power);

        // Weapons strip under the player ship.
        var weapons = new HBoxContainer { Name = "Weapons", MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(SideColumn + 8, 720 - 80), Size = new Vector2(440, 72) };
        weapons.AddThemeConstantOverride("separation", 6);
        hud.AddChild(weapons);
        foreach (var weapon in _definition.Player.Weapons)
        {
            var card = new WeaponCard(_weaponCards.Count + 1) { Name = $"Weapon_{weapon.Id}", CustomMinimumSize = new Vector2(176, 72) };
            card.AimRequested += BeginAim;
            card.PowerToggleRequested += ToggleWeaponPower;
            weapons.AddChild(card);
            _weaponCards.Add(card);
            _controls.Add(card);
        }
        _holdButton = OrderButton(weapons, "HoldVolley", "Hold\n(H)", ToggleHold, 62, "ship/hold");
        _holdButton.CustomMinimumSize = new Vector2(62, 72);
        _holdButton.TooltipText = "Hold charged weapons, then release one synchronized volley (H).";

        // Enemy status (top right), telegraphed weapons and repairs (right column).
        var enemyStatus = Panel(hud, "EnemyStatus", TacticalUi.Hostile, new Rect2(1280 - 350, 8, 340, 58));
        (_enemyName, _enemyHull, _enemyShields, _enemyStats) = StatusBlock(enemyStatus, hostile: true);
        var enemyColumn = new VBoxContainer { Name = "EnemyWeapons", MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(1280 - 186, 76), Size = new Vector2(176, 200) };
        enemyColumn.AddThemeConstantOverride("separation", 6);
        hud.AddChild(enemyColumn);
        enemyColumn.AddChild(TacticalUi.Eyebrow("INCOMING", "ffb49c"));
        foreach (var _ in _definition.Enemy.Side.Weapons)
        {
            var row = new EnemyWeaponRow();
            enemyColumn.AddChild(row);
            _enemyRows.Add(row);
        }
        _enemyRepair = TacticalUi.Label("", 11, "ffc2ad");
        _enemyRepair.CustomMinimumSize = new Vector2(176, 30);
        _enemyRepair.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        enemyColumn.AddChild(_enemyRepair);

        // Battle controls (bottom right).
        var controls = new VBoxContainer { Name = "BattleControls", MouseFilter = MouseFilterEnum.Ignore, Position = new Vector2(1280 - 186, 720 - 116), Size = new Vector2(176, 108) };
        controls.AddThemeConstantOverride("separation", 4);
        hud.AddChild(controls);
        _pauseButton = OrderButton(controls, "Pause", "Resume (Space)", TogglePause, 176, "ship/play");
        OrderButton(controls, "FrameBoth", "Frame both (F)", FrameBoth, 176, "ship/frame");
        OrderButton(controls, "RetryBattle", "Retry battle", () => Send(new ShipRestartCommand(NextCommandId())), 176, "ship/retry");

        // Clock, alerts and feedback (top centre, above both ships).
        _clock = TacticalUi.Label("", 15, "e1e8eb");
        _clock.HorizontalAlignment = HorizontalAlignment.Center;
        _clock.Position = new Vector2(1280 / 2 - 160, 8);
        _clock.Size = new Vector2(320, 22);
        _clock.AddThemeFontOverride("font", TacticalUi.BoldFont);
        hud.AddChild(_clock);
        _alerts = TacticalUi.Label("", 12, "ffb36b");
        _alerts.HorizontalAlignment = HorizontalAlignment.Center;
        _alerts.Position = new Vector2(1280 / 2 - 230, 32);
        _alerts.Size = new Vector2(460, 18);
        _alerts.ClipText = true;
        hud.AddChild(_alerts);
        _feedback = TacticalUi.Label("", 12, "afc1c5");
        _feedback.HorizontalAlignment = HorizontalAlignment.Center;
        _feedback.Position = new Vector2(1280 / 2 - 230, 50);
        _feedback.Size = new Vector2(460, 18);
        _feedback.ClipText = true;
        hud.AddChild(_feedback);

        _terminalOverlay = new CenterContainer { Name = "Outcome", Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        _terminalOverlay.SetAnchorsPreset(LayoutPreset.FullRect);
        var terminalPanel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
        terminalPanel.AddThemeStyleboxOverride("panel", TacticalUi.FieldPanel(TacticalUi.Cyan, bottom: true, margin: 22));
        _terminalOverlay.AddChild(terminalPanel);
        var terminal = new VBoxContainer();
        terminal.AddThemeConstantOverride("separation", 10);
        terminalPanel.AddChild(terminal);
        _terminalLabel = TacticalUi.Label("", 28);
        _terminalLabel.AddThemeFontOverride("font", TacticalUi.BoldFont);
        _terminalLabel.HorizontalAlignment = HorizontalAlignment.Center;
        terminal.AddChild(_terminalLabel);
        _terminalDetail = TacticalUi.Label("", 14, "afc1c5");
        _terminalDetail.HorizontalAlignment = HorizontalAlignment.Center;
        terminal.AddChild(_terminalDetail);
        var retry = OrderButton(terminal, "OutcomeRetry", "Retry battle", () => Send(new ShipRestartCommand(NextCommandId())), 200, "ship/retry");
        retry.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        _controls.Remove(retry);
        AddChild(_terminalOverlay);
    }

    private static PanelContainer Panel(Control parent, string name, Color accent, Rect2 rect)
    {
        var panel = new PanelContainer { Name = name, MouseFilter = MouseFilterEnum.Ignore };
        var style = TacticalUi.FieldPanel(accent, margin: 8);
        style.BgColor = new Color("0b151c", .88f);
        panel.AddThemeStyleboxOverride("panel", style);
        panel.Position = rect.Position;
        panel.Size = rect.Size;
        parent.AddChild(panel);
        return panel;
    }

    private static (Label Name, HullBar Hull, ShieldPips Shields, Label Stats) StatusBlock(PanelContainer panel, bool hostile)
    {
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 3);
        panel.AddChild(box);
        var top = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        top.AddThemeConstantOverride("separation", 8);
        box.AddChild(top);
        var name = TacticalUi.Label("", 13, hostile ? "ffb49c" : "e1e8eb");
        name.AddThemeFontOverride("font", TacticalUi.BoldFont);
        name.CustomMinimumSize = new Vector2(118, 0);
        top.AddChild(name);
        var hull = new HullBar { Hostile = hostile, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(190, 13) };
        top.AddChild(hull);
        var bottom = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        bottom.AddThemeConstantOverride("separation", 8);
        box.AddChild(bottom);
        var shields = new ShieldPips { CustomMinimumSize = new Vector2(92, 20) };
        bottom.AddChild(shields);
        var stats = TacticalUi.Label("", 12, "afc1c5");
        stats.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        bottom.AddChild(stats);
        return (name, hull, shields, stats);
    }

    private static Color CrewAccent(string crewId) => crewId switch
    {
        "actor.protagonist" => TacticalUi.Cyan,
        "actor.companion.medic" => new Color("addcfa"),
        _ => TacticalUi.Protector,
    };

    private void BuildPlayerShip()
    {
        var fire = LoadPublication(FireEffectPath);
        var breach = LoadPublication(BreachEffectPath);
        foreach (var room in _definition.Rooms)
        {
            var centre = RoomCentre(room.Id);
            if (fire is not null)
            {
                var view = fire.Instantiate<Node3D>();
                view.Name = $"Fire_{room.Id}";
                view.Position = _playerView.Anchor($"hazard_fire_{room.Id}", centre + new Vector3(.32f, .03f, .28f));
                view.Visible = false;
                _playerView.Overlay.AddChild(view);
                _fireViews[room.Id] = view;
            }
            if (breach is not null)
            {
                var view = breach.Instantiate<Node3D>();
                view.Name = $"Breach_{room.Id}";
                view.Position = _playerView.Anchor($"hazard_breach_{room.Id}", centre + new Vector3(-.32f, .03f, .3f));
                view.Visible = false;
                _playerView.Overlay.AddChild(view);
                _breachViews[room.Id] = view;
            }
            // Oxygen tint: invisible when breathable, FTL pink as the air thins.
            var tint = ShipBattleView.Flat(new Vector3((float)room.Width - .1f, .01f, (float)room.Depth - .1f), new Color(TacticalUi.Oxygen, 0), centre + new Vector3(0, .035f, 0));
            tint.Name = $"Oxygen_{room.Id}";
            _playerView.Overlay.AddChild(tint);
            _roomTints[room.Id] = tint;
        }
        foreach (var door in _definition.Doors)
        {
            var gate = _playerView.Anchor($"gate_{door.Id["door_".Length..]}", new Vector3((float)door.X, ShipBattleView.FloorY, (float)door.Z));
            var alongX = Math.Abs(gate.X) < .3f;
            var marker = ShipBattleView.Flat(door.Exterior ? new Vector3(.12f, .02f, .8f) : alongX ? new Vector3(.8f, .02f, .1f) : new Vector3(.1f, .02f, .8f),
                Colors.Orange, gate + new Vector3(0, .5f, 0));
            marker.Name = $"DoorState_{door.Id}";
            _playerView.Overlay.AddChild(marker);
            _doorViews[door.Id] = marker;
        }
        var stride = (float)_definition.Crew.SpeedMetersPerSecond / AnimationPacing.CrewStrideSpeed;
        foreach (var crew in _session.Observe().Crew)
        {
            if (!CrewModels.TryGetValue(crew.Id, out var path) || LoadPublication(path) is not { } model)
            { throw new InvalidOperationException($"No published crew model for {crew.Id}."); }
            _crewViews[crew.Id] = new ShipCrewPresentation(_playerView.World, crew.Id, model, stride, CrewAccent(crew.Id));
        }
        BuildHazardEffects();
    }

    private void BuildEnemyShip() => BuildEnemyEffects();

    private PackedScene? LoadPublication(string path)
    {
        if (ResourceLoader.Exists(path) && ResourceLoader.Load<PackedScene>(path) is { } scene) { return scene; }
        _missingPublications.Add(path);
        return null;
    }

    private Vector3 RoomCentre(string room)
    {
        var definition = _definition.Rooms.First(item => item.Id == room);
        return _playerView.Anchor($"room_{room}", new Vector3((float)definition.X, ShipBattleView.FloorY, (float)definition.Z));
    }

    private ShipSystemDefinition EnemySystem(string system) => _definition.Enemy.Side.Systems.First(item => item.Id == system);

    private Vector3 EnemySystemPosition(string system)
    {
        var definition = EnemySystem(system);
        return _enemyView.Anchor($"system_{system}", new Vector3((float)definition.X, ShipBattleView.FloorY, (float)definition.Z));
    }

    /// <summary>Enemy room rectangle in world XZ, from content (the published interceptor exposes matching room anchors).</summary>
    private Rect2 EnemyRoomRect(string system)
    {
        var definition = EnemySystem(system);
        var centre = _enemyView.Anchor($"room_{system}", new Vector3((float)definition.X, ShipBattleView.FloorY, (float)definition.Z));
        return new Rect2(centre.X - (float)definition.Width / 2, centre.Z - (float)definition.Depth / 2, (float)definition.Width, (float)definition.Depth);
    }

    private Vector3 PlayerSystemPosition(string system) => _playerView.Anchor($"system_{system}", RoomCentre(system));

    private Vector3 PlayerEffectPosition(string system) => _playerView.Anchor($"effect_{system}", RoomCentre(system) + new Vector3(0, .4f, 0));

    private Button OrderButton(Container parent, string name, string text, Action action, float width, string? icon = null)
    {
        var button = new Button { Name = name, Text = text, FocusMode = FocusModeEnum.None, ClipText = true, CustomMinimumSize = new Vector2(width, 28) };
        TacticalUi.Style(button);
        button.AddThemeFontSizeOverride("font_size", 12);
        if (icon is not null && TacticalUi.Icon(icon) is { } texture) { button.Icon = texture; button.ExpandIcon = false; button.AddThemeConstantOverride("icon_max_width", 16); }
        button.Pressed += () => { GameAudio.Play("ui.click"); action(); };
        parent.AddChild(button);
        _controls.Add(button);
        return button;
    }

    private void SetFeedback(string text, Color color) { _feedback.Text = text; _feedback.Modulate = color; }

    private static string Humanize(string value) => value.Replace('_', ' ');

    /// <summary>Thin cyan frame and tint while the tactical pause holds the battle (FTL-style paused state).</summary>
    private sealed partial class PausedFrame : Control
    {
        public PausedFrame() => MouseFilter = MouseFilterEnum.Ignore;

        public override void _Draw()
        {
            var rect = new Rect2(Vector2.Zero, Size);
            DrawRect(rect, new Color(TacticalUi.Cyan, .035f));
            DrawRect(rect.Grow(-2), new Color(TacticalUi.Cyan, .75f), false, 3);
        }
    }
}
