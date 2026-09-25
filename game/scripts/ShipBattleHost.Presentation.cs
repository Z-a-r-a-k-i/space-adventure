using System.Globalization;
using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

/// <summary>Reads observations and events into the HUD, overlays, crew, rooms and effects. Never resolves rules.</summary>
public partial class ShipBattleHost
{
    private int _presentedAttempt = 1;
    private ShipBattleView? _hoverView;
    private Vector3? _hoverPoint;

    /// <summary>Presentation clock: battle tick plus accumulated fraction. Frozen by pause and terminal outcomes.</summary>
    public double PresentationTick => _session.Tick + _session.TickFraction;

    public IReadOnlyList<Control> Controls => _controls;

    private void Synchronize()
    {
        var observation = _session.Observe();
        if (observation.Attempt != _presentedAttempt)
        {
            // Battle-only retry resets every presentation-owned effect and event cursor position.
            _presentedAttempt = observation.Attempt;
            _aimingWeapon = null;
            ResetEffects();
        }
        ConsumeEvents(observation);
        var tick = PresentationTick;
        SynchronizeCrew(observation);
        SynchronizeRooms(observation);
        SynchronizeHud(observation, tick);
        UpdateEffects(observation, tick);
        SynchronizeOverlays(observation, tick);
    }

    private void SynchronizeCrew(ShipBattleObservation observation)
    {
        var fraction = _session.TickFraction;
        foreach (var crew in observation.Crew)
        {
            var view = _crewViews[crew.Id];
            view.Synchronize(crew, observation.Tick, fraction, observation.Paused || observation.Phase != ShipBattlePhase.Active, WorkTarget(observation, crew));
            view.SelectionRing.Visible = _selected.Contains(crew.Id);
        }
    }

    private Vector3? WorkTarget(ShipBattleObservation observation, ShipCrewObservation crew) => crew.CurrentWork switch
    {
        ShipTaskKind.Man or ShipTaskKind.Repair => PlayerSystemPosition(crew.Room),
        ShipTaskKind.Extinguish => _fireViews.TryGetValue(crew.Room, out var fire) ? fire.Position : RoomCentre(crew.Room),
        ShipTaskKind.Seal => _breachViews.TryGetValue(crew.Room, out var breach) ? breach.Position : RoomCentre(crew.Room),
        ShipTaskKind.Treat => observation.Crew.FirstOrDefault(item => item.Id == crew.TreatTarget) is { } target && target.Id != crew.Id
            ? new Vector3((float)target.X, ShipBattleView.FloorY, (float)target.Z) : null,
        _ => null,
    };

    private void SynchronizeRooms(ShipBattleObservation observation)
    {
        foreach (var room in observation.Rooms)
        {
            if (_fireViews.TryGetValue(room.Id, out var fire))
            {
                fire.Visible = room.FireSeverity > 0;
                fire.Scale = Vector3.One * (.8f + room.FireSeverity * .15f);
            }
            if (_breachViews.TryGetValue(room.Id, out var breach))
            {
                breach.Visible = room.BreachSeverity > 0;
                breach.Scale = Vector3.One * (.85f + room.BreachSeverity * .1f);
                breach.Rotation = new Vector3(0, room.BreachSeverity * .7f, 0);
            }
            // FTL-style oxygen: nothing while breathable, a pink wash below 60%, red and strong near suffocation.
            var thin = Math.Clamp((600 - room.OxygenPermille) / 450f, 0, 1);
            ((StandardMaterial3D)_roomTints[room.Id].MaterialOverride).AlbedoColor = new Color(room.OxygenPermille < 200 ? TacticalUi.Damaged : TacticalUi.Oxygen, thin * .42f);
        }
        foreach (var door in observation.Doors)
        {
            ((StandardMaterial3D)_doorViews[door.Id].MaterialOverride).AlbedoColor = door.Exterior
                ? (door.CommandedOpen ? new Color("ff3b3b") : new Color("7a3030"))
                : door.CommandedOpen ? TacticalUi.Power : door.EffectivelyOpen ? TacticalUi.Shield : new Color("c98a3a");
            _playerView.SetDoorOpen(door.Id, door.EffectivelyOpen);
        }
    }

    private void SynchronizeHud(ShipBattleObservation observation, double tick)
    {
        var player = observation.Player;
        var enemy = observation.Enemy;
        var pulse = (float)(.5 + .5 * Math.Sin(tick / ShipCombatSession.TicksPerSecond * 6));
        _playerName.Text = player.DisplayName.ToUpperInvariant();
        _playerHull.Value = player.Hull; _playerHull.Maximum = player.MaxHull; _playerHull.QueueRedraw();
        PresentShields(_playerShields, player, _definition.Player);
        var oxygen = observation.Rooms.Sum(room => room.OxygenPermille) / Math.Max(1, observation.Rooms.Count) / 10;
        _playerStats.Text = $"HULL {player.Hull}   EVADE {player.EvasionPercent}%   O2 {oxygen}%";
        _enemyName.Text = enemy.DisplayName.ToUpperInvariant();
        _enemyHull.Value = enemy.Hull; _enemyHull.Maximum = enemy.MaxHull; _enemyHull.QueueRedraw();
        PresentShields(_enemyShields, enemy, _definition.Enemy.Side);
        _enemyStats.Text = $"HULL {enemy.Hull}   EVADE {enemy.EvasionPercent}%";
        foreach (var crew in observation.Crew) { _cards[crew.Id].Present(crew, _selected.Contains(crew.Id)); }
        _power.Present(player);
        for (var index = 0; index < _weaponCards.Count; index++)
        {
            var weapon = player.Weapons[index];
            _weaponCards[index].Present(weapon, _aimingWeapon == weapon.Id, player.HoldFire, pulse);
        }
        _holdButton.Text = player.HoldFire ? "HELD\n(H)" : "Hold\n(H)";
        _holdButton.Modulate = player.HoldFire ? TacticalUi.Amber : Colors.White;
        for (var index = 0; index < _enemyRows.Count; index++)
        {
            var intent = observation.Intents[index];
            _enemyRows[index].Present(intent, enemy.Weapons.First(item => item.Id == intent.WeaponId).ChargePermille);
        }
        var repair = observation.EnemyRepair;
        _enemyRepair.Text = repair.System is { } system
            ? $"Repair drone: {Humanize(system).ToUpperInvariant()} {repair.ProgressPermille / 10}%  ({repair.ReserveBars} bars left)"
            : repair.ReserveBars > 0 ? $"Repair drone idle ({repair.ReserveBars} bars left)" : "Repair drone spent";
        var clock = TimeSpan.FromSeconds(observation.Tick / (double)ShipCombatSession.TicksPerSecond).ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        _clock.Text = observation.Phase != ShipBattlePhase.Active ? $"{observation.Phase.ToString().ToUpperInvariant()}  {clock}"
            : observation.Paused ? $"PAUSED  {clock}  (SPACE)" : $"{clock}";
        _clock.Modulate = observation.Paused && observation.Phase == ShipBattlePhase.Active ? TacticalUi.Cyan : Colors.White;
        _pausedFrame.Visible = observation.Paused && observation.Phase == ShipBattlePhase.Active;
        _pauseButton.Text = observation.Paused ? "Resume (Space)" : "Pause (Space)";
        _pauseButton.Icon = TacticalUi.Icon(observation.Paused ? "ship/play" : "ship/pause");
        var airlock = observation.Doors.First(door => door.Exterior);
        _airlockButton.Text = airlock.CommandedOpen ? "Airlock VENTING" : "Airlock sealed";
        _airlockButton.Modulate = airlock.CommandedOpen ? new Color("ff8a7a") : Colors.White;
        var alerts = new List<string>();
        foreach (var room in observation.Rooms)
        {
            if (room.FireSeverity > 0) { alerts.Add($"FIRE {Humanize(room.Id).ToUpperInvariant()}"); }
            if (room.BreachSeverity > 0) { alerts.Add($"BREACH {Humanize(room.Id).ToUpperInvariant()}"); }
            if (room.OxygenPermille < 300) { alerts.Add($"LOW O2 {Humanize(room.Id).ToUpperInvariant()}"); }
        }
        alerts.AddRange(observation.Crew.Where(crew => crew.InDanger && !crew.Downed).Select(crew => $"{crew.DisplayName.ToUpperInvariant()} IN DANGER"));
        if (airlock.CommandedOpen) { alerts.Add("AIRLOCK VENTING"); }
        _alerts.Text = alerts.Count == 0 ? "" : string.Join("  |  ", alerts.Distinct());
        _terminalOverlay.Visible = observation.Phase != ShipBattlePhase.Active && _terminalSeconds > 1.6;
        _terminalLabel.Text = observation.Phase == ShipBattlePhase.Victory ? "INTERCEPTOR DESTROYED" : "THE CUTTER IS LOST";
        _terminalLabel.Modulate = observation.Phase == ShipBattlePhase.Victory ? TacticalUi.Cyan : TacticalUi.Damaged;
        _terminalDetail.Text = observation.Phase == ShipBattlePhase.Victory
            ? $"Hull {player.Hull}/{player.MaxHull} in {clock}. Retry to fight it again."
            : player.Hull <= 0 ? "Hull breached. Retry the battle; the station is not replayed." : "All crew down. Retry the battle; the station is not replayed.";
    }

    private static void PresentShields(ShieldPips pips, ShipSideObservation side, ShipSideDefinition definition)
    {
        pips.Layers = side.ShieldLayers;
        pips.Capacity = side.ShieldCapacity;
        pips.MaximumCapacity = definition.Systems.Single(system => system.Id == "shields").MaxPower;
        pips.Recharge = side.ShieldRechargePermille / 1000f;
        pips.Damaged = side.Systems.Single(system => system.Id == "shields").Damage;
        pips.QueueRedraw();
    }

    private void SynchronizeOverlays(ShipBattleObservation observation, double tick)
    {
        var spin = (float)(tick / ShipCombatSession.TicksPerSecond * 1.3);
        var player = new List<Action<ShipOverlay>>();
        // Hover preview: where a right-click would send the selected crew.
        if (_hoverView == _playerView && _hoverPoint is { } hover && _selected.Count > 0 && _aimingWeapon is null && RoomAt(hover) is { } hoverRoom)
        {
            var room = _definition.Rooms.First(item => item.Id == hoverRoom);
            var rect = _playerView.ScreenRect(new Vector2((float)room.X, (float)room.Z), new Vector2((float)room.Width, (float)room.Depth));
            player.Add(overlay => overlay.Frame(rect, new Color(TacticalUi.Cyan, .7f), 2, new Color(TacticalUi.Cyan, .06f)));
        }
        foreach (var system in observation.Player.Systems)
        {
            var centre = _playerView.ToScreen(PlayerSystemPosition(system.Id));
            var room = observation.Rooms.Single(item => item.Id == system.Room);
            var state = system.Damage >= system.MaxPower ? TacticalUi.Damaged : system.Damage > 0 ? TacticalUi.Amber
                : system.EffectivePower > 0 ? TacticalUi.Power : TacticalUi.Muted;
            var badge = centre + new Vector2(0, -18);
            player.Add(overlay => overlay.Badge($"ship/{system.Id}", badge, 13, state, new Color("071016", .85f)));
            player.Add(overlay => overlay.Pips(badge + new Vector2(0, 19), system.MaxPower, system.EffectivePower, system.Damage, TacticalUi.Power));
            if (system.RepairProgressPermille > 0) { player.Add(overlay => overlay.Ring(badge, 17, TacticalUi.Amber, system.RepairProgressPermille / 1000f)); }
            if (system.Manned) { player.Add(overlay => overlay.Icon("ship/man", badge + new Vector2(18, -10), 11, TacticalUi.Cyan)); }
            if (room.FireSeverity > 0) { player.Add(overlay => overlay.Icon("ship/fire", badge + new Vector2(-19, -10), 13, new Color("ff9b4a"))); }
            if (room.BreachSeverity > 0) { player.Add(overlay => overlay.Icon("ship/breach", badge + new Vector2(-19, 8), 13, new Color("d7e3ea"))); }
        }
        foreach (var intent in observation.Intents.Where(item => item.Powered && item.TicksToFire >= 0))
        {
            var centre = _playerView.ToScreen(RoomCentre(intent.TargetSystem)) + new Vector2(intent.Kind == ShipWeaponKind.Missile ? 16 : -16, 12);
            var urgent = intent.TicksToFire < 3 * ShipCombatSession.TicksPerSecond;
            var color = new Color(TacticalUi.Hostile, urgent ? .95f : .55f);
            player.Add(overlay => overlay.Reticle(centre, 14, color, -spin));
            player.Add(overlay => overlay.Text(centre + new Vector2(0, 22), ShipHudDraw.Seconds(intent.TicksToFire), 11, color));
            if (intent.Payload != ShipPayload.Normal) { player.Add(overlay => overlay.Icon(intent.Payload == ShipPayload.Incendiary ? "ship/fire" : "ship/breach", centre, 12, color)); }
        }
        foreach (var crew in observation.Crew)
        {
            var head = _playerView.ToScreen(_crewViews[crew.Id].Root.Position + new Vector3(0, 0, -.42f));
            var selected = _selected.Contains(crew.Id);
            if (crew.Downed || !selected && crew.Health >= crew.MaxHealth) { continue; }
            var fraction = crew.Health / (float)crew.MaxHealth;
            player.Add(overlay => overlay.Bar(new Rect2(head + new Vector2(-14, -4), new Vector2(28, 4)), fraction,
                fraction > .5f ? TacticalUi.Power : fraction > .25f ? TacticalUi.Amber : TacticalUi.Damaged));
        }
        player.AddRange(FloatingTexts(_playerView, tick));
        _playerView.Overlay2D.Present(player);

        var enemy = new List<Action<ShipOverlay>>();
        var hoverEnemy = _hoverView == _enemyView && _hoverPoint is { } enemyPoint ? EnemyRoomAt(enemyPoint) : null;
        foreach (var system in observation.Enemy.Systems)
        {
            var rect = EnemyRoomRect(system.Id);
            var screen = _enemyView.ScreenRect(rect.GetCenter(), rect.Size);
            var aiming = _aimingWeapon is not null;
            if (aiming || hoverEnemy == system.Id)
            {
                var strong = hoverEnemy == system.Id;
                enemy.Add(overlay => overlay.Frame(screen, new Color(TacticalUi.Hostile, strong ? .95f : .45f), strong ? 2.5f : 1.5f,
                    new Color(TacticalUi.Hostile, strong ? .12f : .04f)));
            }
            var centre = _enemyView.ToScreen(EnemySystemPosition(system.Id)) + new Vector2(0, -16);
            var state = system.Damage >= system.MaxPower ? TacticalUi.Damaged : system.Damage > 0 ? TacticalUi.Amber : new Color("ffc2ad");
            enemy.Add(overlay => overlay.Badge($"ship/{system.Id}", centre, 13, state, new Color("160a08", .85f)));
            enemy.Add(overlay => overlay.Pips(centre + new Vector2(0, 19), system.MaxPower, system.EffectivePower, system.Damage, TacticalUi.Hostile));
            if (observation.EnemyRepair.System == system.Id)
            {
                var progress = observation.EnemyRepair.ProgressPermille / 1000f;
                enemy.Add(overlay => overlay.Ring(centre, 17, TacticalUi.Amber, progress));
                enemy.Add(overlay => overlay.Icon("ship/repair", centre + new Vector2(20, -12), 12, TacticalUi.Amber));
            }
        }
        var aimedRooms = observation.Player.Weapons.Select((weapon, index) => (weapon, index)).Where(item => item.weapon.Target is not null)
            .GroupBy(item => item.weapon.Target!);
        foreach (var group in aimedRooms)
        {
            var rect = EnemyRoomRect(group.Key);
            var centre = _enemyView.ToScreen(new Vector3(rect.GetCenter().X, ShipBattleView.FloorY, rect.GetCenter().Y)) + new Vector2(0, 18);
            var numbers = string.Join(" ", group.Select(item => (item.index + 1).ToString(CultureInfo.InvariantCulture)));
            var live = group.Any(item => item.weapon.Powered);
            var color = new Color(TacticalUi.Cyan, live ? .95f : .45f);
            enemy.Add(overlay => overlay.Reticle(centre, 16, color, spin));
            enemy.Add(overlay => overlay.Text(centre + new Vector2(0, 26), numbers, 12, color, bold: true));
        }
        enemy.AddRange(FloatingTexts(_enemyView, tick));
        _enemyView.Overlay2D.Present(enemy);
    }
}
