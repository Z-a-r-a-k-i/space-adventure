using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

/// <summary>
/// Controller play in the ship battle. With no one to walk, the left stick hops a highlight from room to room: A
/// sends the selected crew there (a lone Medic treats an injured ally in it) or, while aiming, fires at the enemy
/// room. X/Y aim the weapons (hold to switch one's power), the D-pad runs the power bar, the triggers pause and hold
/// the volley, and Menu opens a panel for the remaining buttons. Every action sends the ordinary ship commands.
/// </summary>
public partial class ShipBattleHost
{
    private const float PadHopThreshold = .6f;
    private const double PadHoldSeconds = .45;
    private string _padRoom = ShipBattleDefinition.Passage;
    private string? _padEnemyRoom;
    private int _padSystem;
    private double _padHopCooldown;
    private bool _padHopping;
    private readonly Dictionary<JoyButton, ulong> _padPressedMs = [];
    private readonly HashSet<JoyButton> _padHeldFired = [];
    private bool _padRightTriggerDown;
    private bool _padLeftTriggerDown;
    private int _padPromptGeneration = -1;
    private PanelContainer _padPanel = null!;
    private Button _padAirlockButton = null!;
    private Button _padPanelFirst = null!;
    private readonly List<Button> _padPanelButtons = [];

    private void BuildPadPanel()
    {
        _padPanel = new PanelContainer { Name = "PadPanel", Visible = false, MouseFilter = MouseFilterEnum.Stop };
        _padPanel.AddThemeStyleboxOverride("panel", TacticalUi.FieldPanel(TacticalUi.Cyan, bottom: true, margin: 16));
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        _padPanel.AddChild(column);
        column.AddChild(TacticalUi.Eyebrow("ORDERS", "a0efd8"));
        _padPanelFirst = PadPanelButton(column, "Close all doors", "ship/door", () => SetAllDoors(false));
        PadPanelButton(column, "Open all doors", "ship/door", () => SetAllDoors(true));
        _padAirlockButton = PadPanelButton(column, "Vent airlock", "ship/vent", ToggleAirlock);
        PadPanelButton(column, "Frame both ships", "ship/frame", FrameBoth);
        PadPanelButton(column, "Retry battle", "ship/retry", () => Send(new ShipRestartCommand(NextCommandId())));
        var hint = TacticalUi.RichLabel($"{InputPrompts.Bb(PadButton.DpadVertical, 15)} Choose   {InputPrompts.Bb(PadButton.A, 15)} Select   "
            + $"{InputPrompts.Bb(PadButton.B, 15)} Back", 11, "afc1c5");
        column.AddChild(hint);
        AddChild(_padPanel);
    }

    private Button PadPanelButton(Container parent, string text, string icon, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(220, 32), Alignment = HorizontalAlignment.Left };
        TacticalUi.Style(button);
        // Styled buttons ignore focus for the mouse HUD; this panel is navigated with the D-pad.
        button.FocusMode = FocusModeEnum.All;
        button.AddThemeFontSizeOverride("font_size", 13);
        if (TacticalUi.Icon(icon) is { } texture) { button.Icon = texture; button.ExpandIcon = false; button.AddThemeConstantOverride("icon_max_width", 16); }
        button.Pressed += () => { GameAudio.Play("ui.click"); ClosePadPanel(); action(); };
        parent.AddChild(button);
        _padPanelButtons.Add(button);
        return button;
    }

    private void OpenPadPanel()
    {
        CancelAim(clearTarget: false);
        // The panel swallows button releases, so a weapon tap in progress must not later count as a hold.
        _padPressedMs.Clear();
        _padHeldFired.Clear();
        var airlock = _session.Observe().Doors.First(door => door.Exterior);
        _padAirlockButton.Text = airlock.CommandedOpen ? "Seal airlock" : "Vent airlock";
        _padPanel.Visible = true;
        _padPanel.Size = _padPanel.GetCombinedMinimumSize();
        _padPanel.Position = (GetViewportRect().Size - _padPanel.Size) / 2;
        _padPanelFirst.GrabFocus();
    }

    private void ClosePadPanel()
    {
        _padPanel.Visible = false;
        GetViewport().GuiReleaseFocus();
    }

    private bool HandlePadInput(InputEvent @event) => @event switch
    {
        InputEventJoypadButton button => HandlePadButton(button),
        InputEventJoypadMotion { Axis: JoyAxis.TriggerLeft or JoyAxis.TriggerRight } trigger => HandlePadTrigger(trigger),
        _ => false,
    };

    private bool HandlePadButton(InputEventJoypadButton button)
    {
        if (_padPanel.Visible)
        {
            // The D-pad walks the panel's buttons and A presses the focused one; B or Menu closes it.
            if (button.Pressed)
            {
                var index = Math.Max(0, _padPanelButtons.FindIndex(item => item.HasFocus()));
                switch (button.ButtonIndex)
                {
                    case JoyButton.B or JoyButton.Start: ClosePadPanel(); break;
                    case JoyButton.DpadUp or JoyButton.DpadDown:
                        var step = button.ButtonIndex == JoyButton.DpadUp ? _padPanelButtons.Count - 1 : 1;
                        _padPanelButtons[(index + step) % _padPanelButtons.Count].GrabFocus();
                        break;
                    case JoyButton.A: _padPanelButtons[index].EmitSignal(BaseButton.SignalName.Pressed); break;
                }
            }
            GetViewport().SetInputAsHandled();
            return true;
        }
        if (_terminalOverlay.Visible)
        {
            if (button.Pressed && button.ButtonIndex == JoyButton.A) { Send(new ShipRestartCommand(NextCommandId())); }
            GetViewport().SetInputAsHandled();
            return true;
        }
        if (button.ButtonIndex is JoyButton.X or JoyButton.Y)
        {
            // Tap aims the weapon; holding it switches the weapon's power instead (see ProcessPad).
            if (button.Pressed) { _padPressedMs[button.ButtonIndex] = Time.GetTicksMsec(); _padHeldFired.Remove(button.ButtonIndex); }
            else if (_padPressedMs.Remove(button.ButtonIndex) && !_padHeldFired.Remove(button.ButtonIndex))
            { PadAim(button.ButtonIndex == JoyButton.X ? 0 : 1); }
            GetViewport().SetInputAsHandled();
            return true;
        }
        if (!button.Pressed) { return false; }
        switch (button.ButtonIndex)
        {
            case JoyButton.A: PadConfirm(); break;
            case JoyButton.B: PadBack(); break;
            case JoyButton.LeftShoulder or JoyButton.RightShoulder: PadCycleCrew(button.ButtonIndex == JoyButton.LeftShoulder ? -1 : 1); break;
            case JoyButton.Back:
                _selected.Clear();
                _selected.AddRange(_session.Observe().Crew.Where(crew => !crew.Downed).Select(crew => crew.Id));
                GameAudio.Play("ui.select");
                break;
            case JoyButton.Start: OpenPadPanel(); break;
            case JoyButton.RightStick: FrameBoth(); break;
            case JoyButton.DpadLeft or JoyButton.DpadRight:
                _padSystem = (_padSystem + (button.ButtonIndex == JoyButton.DpadLeft ? PowerPanel.Order.Count - 1 : 1)) % PowerPanel.Order.Count;
                GameAudio.Play("ui.select");
                break;
            case JoyButton.DpadUp or JoyButton.DpadDown: AdjustPower(PowerPanel.Order[_padSystem], button.ButtonIndex == JoyButton.DpadUp ? 1 : -1); break;
            default: return false;
        }
        GetViewport().SetInputAsHandled();
        return true;
    }

    /// <summary>RT pauses and LT holds the volley, once per pull of the analogue trigger.</summary>
    private bool HandlePadTrigger(InputEventJoypadMotion trigger)
    {
        var right = trigger.Axis == JoyAxis.TriggerRight;
        if (_padPanel.Visible || IntroPlaying)
        {
            // Latch a pull made while the panel or intro is up, so it cannot fire once they close.
            if (right) { _padRightTriggerDown = trigger.AxisValue >= .25f; } else { _padLeftTriggerDown = trigger.AxisValue >= .25f; }
            return false;
        }
        if (right ? _padRightTriggerDown : _padLeftTriggerDown)
        {
            if (trigger.AxisValue < .25f) { if (right) { _padRightTriggerDown = false; } else { _padLeftTriggerDown = false; } }
            return true;
        }
        if (trigger.AxisValue < .55f) { return false; }
        if (right) { _padRightTriggerDown = true; TogglePause(); } else { _padLeftTriggerDown = true; ToggleHold(); }
        GetViewport().SetInputAsHandled();
        return true;
    }

    private void ProcessPad(double delta)
    {
        if (_padPromptGeneration != InputDevice.Generation) { ApplyPadPrompts(); }
        if (InputDevice.ConsumeDisconnect() && !_session.Paused && _session.Phase == ShipBattlePhase.Active)
        {
            TogglePause();
            SetFeedback("Controller disconnected · paused", TacticalUi.Amber);
        }
        // Trigger releases can be swallowed by the panel or the intro; read the axes so the next pull always counts.
        if (Input.GetJoyAxis(InputDevice.Device, JoyAxis.TriggerRight) < .25f) { _padRightTriggerDown = false; }
        if (Input.GetJoyAxis(InputDevice.Device, JoyAxis.TriggerLeft) < .25f) { _padLeftTriggerDown = false; }
        if (!InputDevice.UsingGamepad || IntroPlaying || _padPanel.Visible) { return; }
        // Aiming may have started with the mouse before the controller took over.
        if (_aimingWeapon is { } aiming && _padEnemyRoom is null)
        {
            _padEnemyRoom = _session.Observe().Player.Weapons.First(item => item.Id == aiming).Target ?? _definition.Enemy.Side.Systems[0].Id;
        }
        var now = Time.GetTicksMsec();
        foreach (var (button, pressedMs) in _padPressedMs)
        {
            if (_padHeldFired.Contains(button) || now - pressedMs < PadHoldSeconds * 1000) { continue; }
            _padHeldFired.Add(button);
            var index = button == JoyButton.X ? 0 : 1;
            if (index < _definition.Player.Weapons.Count) { ToggleWeaponPower(_definition.Player.Weapons[index].Id); }
        }
        var stick = new Vector2(Input.GetJoyAxis(InputDevice.Device, JoyAxis.LeftX), Input.GetJoyAxis(InputDevice.Device, JoyAxis.LeftY));
        if (stick.Length() < PadHopThreshold) { _padHopping = false; _padHopCooldown = 0; }
        else
        {
            _padHopCooldown -= delta;
            if (_padHopCooldown <= 0)
            {
                HopPad(stick.Normalized());
                // Holding the stick repeats, a little slower on the first repeat, like menu navigation.
                _padHopCooldown = _padHopping ? .2 : .38;
                _padHopping = true;
            }
        }
        var zoom = Input.GetJoyAxis(InputDevice.Device, JoyAxis.RightY);
        if (Math.Abs(zoom) > .2f) { (_aimingWeapon is null ? _playerView : _enemyView).ZoomBy(1 + zoom * 1.6f * (float)delta); }
    }

    private Vector2 PlayerRoomCentre(string room) { var centre = RoomCentre(room); return new Vector2(centre.X, centre.Z); }

    /// <summary>Moves the highlight to the nearest room lying in the pushed direction (bow up: screen right is +X, down is +Z).</summary>
    private void HopPad(Vector2 direction)
    {
        if (_aimingWeapon is not null)
        {
            var rooms = _definition.Enemy.Side.Systems.Select(system => system.Id).ToList();
            var from = _padEnemyRoom ?? rooms[0];
            if (BestHop(rooms, from, id => EnemyRoomRect(id).GetCenter(), direction) is not { } enemy) { return; }
            _padEnemyRoom = enemy;
            KeepHighlightFramed(_enemyView, EnemyRoomRect(enemy).GetCenter());
        }
        else
        {
            if (BestHop(_definition.Rooms.Select(room => room.Id).ToList(), _padRoom, PlayerRoomCentre, direction) is not { } room) { return; }
            _padRoom = room;
            KeepHighlightFramed(_playerView, PlayerRoomCentre(room));
        }
        GameAudio.Play("ui.select");
    }

    private static void KeepHighlightFramed(ShipBattleView view, Vector2 centre)
    {
        if (view.Zoom < .999f) { view.FocusOn(centre, view.Zoom); }
    }

    private static string? BestHop(IReadOnlyList<string> ids, string current, Func<string, Vector2> centre, Vector2 direction)
    {
        var from = centre(current);
        string? best = null;
        var bestScore = float.MaxValue;
        foreach (var id in ids.Where(id => id != current))
        {
            var offset = centre(id) - from;
            var distance = offset.Length();
            if (distance < .01f) { continue; }
            var alignment = offset.Dot(direction) / distance;
            if (alignment < .45f) { continue; }
            var score = distance * (2 - alignment);
            if (score < bestScore) { bestScore = score; best = id; }
        }
        return best;
    }

    private void PadAim(int index)
    {
        if (index >= _definition.Player.Weapons.Count) { return; }
        var weapon = _definition.Player.Weapons[index].Id;
        BeginAim(weapon);
        if (_aimingWeapon is null) { return; }
        _padEnemyRoom = _session.Observe().Player.Weapons.First(item => item.Id == weapon).Target
            ?? _padEnemyRoom ?? _definition.Enemy.Side.Systems[0].Id;
    }

    /// <summary>The injured ally a lone selected Medic would treat in the highlighted room, if any.</summary>
    private ShipCrewObservation? PadPatient(ShipBattleObservation observation)
    {
        if (_selected.Count != 1 || observation.Crew.FirstOrDefault(crew => crew.Id == _selected[0]) is not { IsMedic: true } medic) { return null; }
        return observation.Crew.Where(crew => crew.Id != medic.Id && crew.Room == _padRoom && !crew.Downed && crew.Health < crew.MaxHealth)
            .OrderBy(crew => crew.Health / (double)Math.Max(1, crew.MaxHealth)).FirstOrDefault();
    }

    private void PadConfirm()
    {
        if (_aimingWeapon is not null)
        {
            if (_padEnemyRoom is { } target) { ClickEnemy(EnemySystemPosition(target)); }
            return;
        }
        var observation = _session.Observe();
        if (_selected.Count == 0) { SetFeedback("Select crew first (LB / RB, View for everyone)", TacticalUi.Amber); GameAudio.Play("ui.deny"); return; }
        if (PadPatient(observation) is { } patient)
        {
            if (Send(new ShipAssignTaskCommand(NextCommandId(), _selected[0], ShipTaskKind.Treat, patient.Room, patient.Id)).Accepted)
            { SetFeedback($"Medic treating {patient.DisplayName}", TacticalUi.Cyan); GameAudio.Play("ui.order"); }
            return;
        }
        ForSelected(id => new ShipMoveCrewCommand(NextCommandId(), id, _padRoom));
        GameAudio.Play("ui.order");
    }

    private void PadBack()
    {
        if (_aimingWeapon is not null) { CancelAim(clearTarget: false); SetFeedback("Aiming cancelled", TacticalUi.Muted); return; }
        // B works the highlighted room's interior doors; the airlock stays on the Menu panel.
        var doors = _definition.Doors.Where(door => !door.Exterior && (door.RoomA == _padRoom || door.RoomB == _padRoom)).Select(door => door.Id).ToArray();
        if (doors.Length == 0) { return; }
        var observation = _session.Observe();
        var open = !doors.Any(id => observation.Doors.First(door => door.Id == id).CommandedOpen);
        foreach (var id in doors) { Send(new ShipSetDoorCommand(NextCommandId(), id, open)); }
        SetFeedback($"{Humanize(_padRoom).ToUpperInvariant()} doors {(open ? "open" : "closed")}", TacticalUi.Cyan);
    }

    private void PadCycleCrew(int step)
    {
        var crew = _session.Observe().Crew.Where(item => !item.Downed).ToList();
        if (crew.Count == 0) { return; }
        var index = _selected.Count == 0 ? -1 : crew.FindIndex(item => item.Id == _selected[^1]);
        var next = crew[index < 0 ? (step > 0 ? 0 : crew.Count - 1) : (index + step + crew.Count) % crew.Count];
        Select(next.Id, false);
        // The highlight starts from where that crew member stands.
        _padRoom = next.Room;
        GameAudio.Play("ui.select");
    }

    /// <summary>Rewrites the static prompts after a device switch.</summary>
    private void ApplyPadPrompts()
    {
        _padPromptGeneration = InputDevice.Generation;
        var pad = InputDevice.UsingGamepad;
        if (!pad) { ClosePadPanel(); }
        _doorsHint.Text = pad ? "DOORS  (B on a room · Menu for all)" : "DOORS  (click a door on the ship)";
        _frameButton.Text = pad ? "Frame both" : "Frame both (F)";
        SetPadIcon(_frameButton, pad ? PadButton.R3 : null, "ship/frame");
        // Glyphs carry letters, so they get a little more room than the line icons they replace.
        foreach (var button in new[] { _frameButton, _pauseButton, _holdButton }) { button.AddThemeConstantOverride("icon_max_width", pad ? 20 : 16); }
        // The opening hint names the device's controls until the player's first order replaces it.
        var opening = new[] { "Paused. Space resumes. Click a weapon, then an enemy room.", "Paused. RT resumes. X or Y aims a weapon at an enemy room." };
        if (opening.Contains(_feedback.Text)) { SetFeedback(opening[pad ? 1 : 0], TacticalUi.Muted); }
        foreach (var card in _weaponCards) { card.QueueRedraw(); }
        foreach (var card in _cards.Values) { card.QueueRedraw(); }
    }

    private static void SetPadIcon(Button button, PadButton? glyph, string icon)
    {
        button.Icon = glyph is { } shown ? InputPrompts.Glyph(shown) : TacticalUi.Icon(icon);
        var tint = glyph is { } tinted ? InputPrompts.Tint(tinted) : Colors.White;
        foreach (var state in new[] { "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_focus_color", "icon_disabled_color" })
        { button.AddThemeColorOverride(state, tint); }
    }

    /// <summary>Highlight frames and button prompts drawn over the ship views while the controller is in use.</summary>
    private void PadOverlays(ShipBattleObservation observation, List<Action<ShipOverlay>> player, List<Action<ShipOverlay>> enemy)
    {
        if (!InputDevice.UsingGamepad || IntroPlaying || observation.Phase != ShipBattlePhase.Active) { return; }
        if (_aimingWeapon is not null && _padEnemyRoom is { } target)
        {
            var rect = EnemyRoomRect(target);
            var screen = _enemyView.ScreenRect(rect.GetCenter(), rect.Size);
            enemy.Add(overlay => overlay.Frame(screen, TacticalUi.Hostile, 3, new Color(TacticalUi.Hostile, .14f)));
            enemy.Add(overlay => PadPrompt(overlay, new Vector2(screen.GetCenter().X, screen.Position.Y - 16), PadButton.A, "Fire"));
            return;
        }
        var room = _definition.Rooms.First(item => item.Id == _padRoom);
        var area = _playerView.ScreenRect(new Vector2((float)room.X, (float)room.Z), new Vector2((float)room.Width, (float)room.Depth));
        player.Add(overlay => overlay.Frame(area, TacticalUi.Cyan, 3, new Color(TacticalUi.Cyan, .08f)));
        var patient = PadPatient(observation);
        var label = _selected.Count == 0 ? "Select crew (LB / RB)" : patient is not null ? $"Treat {patient.DisplayName}" : "Send crew here";
        player.Add(overlay => PadPrompt(overlay, new Vector2(area.GetCenter().X, area.Position.Y - 14), _selected.Count == 0 ? PadButton.RB : PadButton.A, label));
    }

    private static void PadPrompt(ShipOverlay overlay, Vector2 centre, PadButton button, string text)
    {
        var width = TacticalUi.BoldFont.GetStringSize(text, HorizontalAlignment.Left, -1, 12).X;
        var left = centre.X - (width + 22) / 2;
        overlay.Icon($"pad/{InputPrompts.Name(button)}", new Vector2(left + 8, centre.Y), 16, InputPrompts.Tint(button));
        overlay.Text(new Vector2(left + 20, centre.Y + 4), text, 12, Colors.White, centred: false, bold: true);
    }
}
