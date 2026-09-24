using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

/// <summary>
/// FTL-style controls. Crew: click a card or model to select, right-click a room to move (crew then work that
/// room automatically); right-click an injured ally with the Medic selected to treat. Weapons: click a weapon card
/// (or press 1-2), then an enemy room; right-click cancels aiming and clears that weapon's target. Power: click
/// a system to add a bar, right-click to remove. Doors: click a door on the ship. Space pauses, H holds a volley.
/// </summary>
public partial class ShipBattleHost
{
    public override void _Input(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) { return; }
        switch (key.Keycode)
        {
            case Key.Space: TogglePause(); break;
            case Key.Tab: CycleSelection(); break;
            case Key.F: FrameBoth(); break;
            case Key.H: ToggleHold(); break;
            case Key.Escape: CancelAim(clearTarget: false); break;
            case Key.M: AdjustMasterVolume(0, toggleMute: true); break;
            case Key.Minus or Key.KpSubtract: AdjustMasterVolume(-3, toggleMute: false); break;
            case Key.Equal or Key.Plus or Key.KpAdd: AdjustMasterVolume(3, toggleMute: false); break;
            case Key.Key1 or Key.Key2 or Key.Key3 or Key.Key4:
                var weapon = (int)(key.Keycode - Key.Key1);
                if (weapon < _definition.Player.Weapons.Count) { BeginAim(_definition.Player.Weapons[weapon].Id); }
                break;
            case Key.F1 or Key.F2 or Key.F3:
                var index = (int)(key.Keycode - Key.F1);
                var crew = _session.Observe().Crew;
                if (index < crew.Count) { Select(crew[index].Id, key.ShiftPressed); GameAudio.Play("ui.select"); }
                break;
            default: return;
        }
        GetViewport().SetInputAsHandled();
    }

    private void ViewInput(ShipBattleView view, InputEvent input)
    {
        switch (input)
        {
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelUp, Pressed: true }: view.ZoomBy(.9f); return;
            case InputEventMouseButton { ButtonIndex: MouseButton.WheelDown, Pressed: true }: view.ZoomBy(1 / .9f); return;
            case InputEventMouseButton { ButtonIndex: MouseButton.Middle } middle: _panning = middle.Pressed; _panView = view; return;
            case InputEventMouseMotion motion when _panning && _panView == view:
                view.PanBy(new Vector2(-motion.Relative.X, -motion.Relative.Y) * view.MetresPerPixel);
                return;
            case InputEventMouseMotion motion:
                _hoverView = view;
                _hoverPoint = view.FloorPoint(motion.Position);
                return;
            case InputEventMouseButton { Pressed: true } click when click.ButtonIndex is MouseButton.Left or MouseButton.Right:
                if (view.FloorPoint(click.Position) is not { } point) { return; }
                if (view == _enemyView)
                {
                    if (click.ButtonIndex == MouseButton.Left) { ClickEnemy(point); } else { CancelAim(clearTarget: true); }
                    return;
                }
                if (_aimingWeapon is not null) { CancelAim(clearTarget: click.ButtonIndex == MouseButton.Right); return; }
                if (click.ButtonIndex == MouseButton.Left) { ClickPlayer(point, click.ShiftPressed); }
                else { RightClickPlayer(point); }
                return;
        }
    }

    public string? AimingWeapon => _aimingWeapon;

    public void BeginAim(string weaponId)
    {
        _aimingWeapon = _aimingWeapon == weaponId ? null : weaponId;
        if (_aimingWeapon is not null)
        {
            GameAudio.Play("ui.aim");
            SetFeedback($"{_definition.Player.Weapons.First(item => item.Id == weaponId).DisplayName}: click an enemy room (right-click cancels)", TacticalUi.Cyan);
        }
    }

    private void CancelAim(bool clearTarget)
    {
        if (_aimingWeapon is null) { return; }
        if (clearTarget) { Send(new ShipSetWeaponTargetCommand(NextCommandId(), _aimingWeapon, null)); SetFeedback("Target cleared", TacticalUi.Muted); }
        _aimingWeapon = null;
    }

    /// <summary>Aims the weapon being targeted (or, with none chosen, every weapon) at the clicked enemy room.</summary>
    public void ClickEnemy(Vector3 point)
    {
        if (EnemyRoomAt(point) is not { } system) { return; }
        string[] weapons = _aimingWeapon is { } aiming ? [aiming] : _definition.Player.Weapons.Select(item => item.Id).ToArray();
        foreach (var weapon in weapons) { Send(new ShipSetWeaponTargetCommand(NextCommandId(), weapon, system)); }
        GameAudio.Play("ui.target");
        SetFeedback($"{(weapons.Length == 1 ? _definition.Player.Weapons.First(item => item.Id == weapons[0]).DisplayName : "All weapons")} targeting enemy {Humanize(system)}", TacticalUi.Hostile);
        _aimingWeapon = null;
    }

    public string? EnemyRoomAt(Vector3 point)
    {
        var floor = new Vector2(point.X, point.Z);
        return _definition.Enemy.Side.Systems.Select(system => system.Id).FirstOrDefault(id => EnemyRoomRect(id).Grow(.15f).HasPoint(floor));
    }

    public void ClickPlayer(Vector3 point, bool additive)
    {
        var observation = _session.Observe();
        if (CrewAt(observation, point) is { } crew) { Select(crew.Id, additive); GameAudio.Play("ui.select"); return; }
        var door = _definition.Doors.OrderBy(item => new Vector2((float)item.X - point.X, (float)item.Z - point.Z).Length()).First();
        if (new Vector2((float)door.X - point.X, (float)door.Z - point.Z).Length() < .5f) { ToggleDoor(door.Id); return; }
        if (!additive) { _selected.Clear(); }
    }

    /// <summary>Right-click: Medic selected on an injured ally treats them; otherwise selected crew move to the room.</summary>
    public void RightClickPlayer(Vector3 point)
    {
        var observation = _session.Observe();
        if (_selected.Count == 0) { SetFeedback("Select crew first (click a card or a crew member)", TacticalUi.Amber); GameAudio.Play("ui.deny"); return; }
        var patient = CrewAt(observation, point);
        var medic = observation.Crew.FirstOrDefault(item => item.IsMedic && _selected.Contains(item.Id));
        if (patient is not null && medic is not null && patient.Health < patient.MaxHealth && !patient.Downed)
        {
            if (Send(new ShipAssignTaskCommand(NextCommandId(), medic.Id, ShipTaskKind.Treat, patient.Room, patient.Id)).Accepted)
            { SetFeedback($"Medic treating {patient.DisplayName}", TacticalUi.Cyan); GameAudio.Play("ui.order"); }
            return;
        }
        if (RoomAt(point) is not { } room) { return; }
        ForSelected(id => new ShipMoveCrewCommand(NextCommandId(), id, room));
        GameAudio.Play("ui.order");
    }

    private static ShipCrewObservation? CrewAt(ShipBattleObservation observation, Vector3 point) =>
        observation.Crew.Where(item => new Vector2((float)item.X - point.X, (float)item.Z - point.Z).Length() < .45f)
            .OrderBy(item => new Vector2((float)item.X - point.X, (float)item.Z - point.Z).Length()).FirstOrDefault();

    public string? RoomAt(Vector3 point)
    {
        // The aft cross-passage belongs to the passage room.
        if (Math.Abs(point.X) <= 1.9f && point.Z is >= 1.55f and <= 2.15f) { return ShipBattleDefinition.Passage; }
        return _definition.Rooms.FirstOrDefault(room => Math.Abs(point.X - room.X) <= room.Width / 2 && Math.Abs(point.Z - room.Z) <= room.Depth / 2)?.Id;
    }

    public void Select(string crewId, bool additive)
    {
        if (!additive) { _selected.Clear(); }
        if (_selected.Contains(crewId) && additive) { _selected.Remove(crewId); } else if (!_selected.Contains(crewId)) { _selected.Add(crewId); }
    }

    public IReadOnlyList<string> Selected => _selected;

    private void CycleSelection()
    {
        var crew = _session.Observe().Crew.Where(item => !item.Downed).Select(item => item.Id).ToList();
        if (crew.Count == 0) { return; }
        var index = _selected.Count == 0 ? -1 : crew.IndexOf(_selected[^1]);
        Select(crew[(index + 1) % crew.Count], false);
    }

    public void TogglePause() => Send(new ShipSetPauseCommand(NextCommandId(), !_session.Paused));

    public void ToggleHold()
    {
        var hold = !_session.Observe().Player.HoldFire;
        if (Send(new ShipSetHoldFireCommand(NextCommandId(), hold)).Accepted)
        { SetFeedback(hold ? "Holding charged weapons: press H to release a synchronized volley" : "Volley released", hold ? TacticalUi.Amber : TacticalUi.Cyan); }
    }

    public void ToggleWeaponPower(string weaponId)
    {
        var weapon = _session.Observe().Player.Weapons.First(item => item.Id == weaponId);
        if (Send(new ShipSetWeaponPowerCommand(NextCommandId(), weaponId, !weapon.Armed)).Accepted) { GameAudio.Play(weapon.Armed ? "ship.power_down" : "ship.power_up"); }
    }

    public void ToggleDoor(string doorId)
    {
        var door = _session.Observe().Doors.First(item => item.Id == doorId);
        Send(new ShipSetDoorCommand(NextCommandId(), doorId, !door.CommandedOpen));
    }

    private void SetAllDoors(bool open)
    {
        foreach (var door in _definition.Doors.Where(item => !item.Exterior)) { Send(new ShipSetDoorCommand(NextCommandId(), door.Id, open)); }
    }

    private void ToggleAirlock() => ToggleDoor(_definition.Doors.First(item => item.Exterior).Id);

    public void AdjustPower(string system, int delta)
    {
        var allocation = _session.Observe().Player.Systems.ToDictionary(item => item.Id, item => item.AllocatedPower);
        allocation[system] += delta;
        if (Send(new ShipSetPowerCommand(NextCommandId(), allocation)).Accepted) { GameAudio.Play(delta > 0 ? "ship.power_up" : "ship.power_down"); }
    }

    /// <summary>Same session-wide Master bus the station's field manual controls (M mutes, -/+ step 3 dB).</summary>
    private void AdjustMasterVolume(float stepDb, bool toggleMute)
    {
        var bus = AudioServer.GetBusIndex("Master");
        if (bus < 0) { return; }
        if (toggleMute) { AudioServer.SetBusMute(bus, !AudioServer.IsBusMute(bus)); }
        else { AudioServer.SetBusVolumeDb(bus, Math.Clamp(AudioServer.GetBusVolumeDb(bus) + stepDb, -40, 6)); }
        SetFeedback(AudioServer.IsBusMute(bus) ? "Sound muted (M)" : $"Sound {AudioServer.GetBusVolumeDb(bus):+0;-0;0} dB  (M mute, -/+ volume)", TacticalUi.Muted);
    }

    private void ForSelected(Func<string, IShipCommand> command)
    {
        if (_selected.Count == 0) { SetFeedback("Select crew first", TacticalUi.Amber); return; }
        foreach (var id in _selected.ToArray()) { Send(command(id)); }
    }
}
