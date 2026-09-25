namespace SpaceAdventure.Core;

/// <summary>
/// Pure ship battle rules at fixed 30 Hz. Tick order (tests pin it):
/// 1 crew travel and doorway traversal windows, 2 crew work (treat, extinguish, seal, repair, manning),
/// 3 shields, per-weapon power/charge/release, enemy repair, 4 every impact due this tick (evasion roll,
/// shield layer, hull/system/crew damage, hazards), 5 atmosphere from the previous-tick oxygen snapshot,
/// 6 fire suppression/damage/spread, 7 crew damage and downing, 8 weapon power refresh, 9 terminal outcome
/// (defeat takes precedence). Pause and terminal outcomes stop every step; commands never advance time.
/// Random rolls (evasion, weapon fire/breach chances) come from one seeded stream per attempt.
/// </summary>
public sealed class ShipCombatSession
{
    public const int TicksPerSecond = 30;
    public const int FullOxygen = 100_000;
    private const long TimeUnitsPerTick = TimeSpan.TicksPerSecond;
    private const int MaximumTicksPerFrame = 8;
    private const double DoorwayRadius = 0.6;
    private const int UnpoweredChargeBleed = 3000;

    private readonly ShipBattleDefinition _definition;
    private readonly IReadOnlyList<ShipCrewSeed> _crewSeeds;
    private readonly ulong _seed;
    private readonly List<ShipEvent> _events = [];
    private readonly Dictionary<string, ShipRoomDefinition> _roomDefinitions;
    private SideState _player = null!;
    private SideState _enemy = null!;
    private Dictionary<string, RoomState> _rooms = null!;
    private Dictionary<string, DoorState> _doors = null!;
    private List<CrewState> _crew = null!;
    private List<Shot> _shots = null!;
    private SplitMix64 _random = null!;
    private long _accumulator;
    private long _eventSequence;
    private long _shotSequence;
    private string? _enemyRepairSystem;
    private int _enemyRepairProgress;
    private int _enemyRepairReserve;

    /// <param name="seed">Overrides the authored seed; the same seed, attempt and commands always replay identically.</param>
    public ShipCombatSession(ShipBattleDefinition definition, IReadOnlyList<ShipCrewSeed> crew, ulong? seed = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(crew);
        if (crew.Count == 0 || crew.Count > definition.Crew.StartRooms.Count || crew.Select(seed => seed.Id).Distinct(StringComparer.Ordinal).Count() != crew.Count)
        { throw new ArgumentException("Ship crew must be distinct and fit the authored start rooms.", nameof(crew)); }
        _definition = definition;
        _crewSeeds = crew.ToArray();
        _seed = seed ?? definition.Seed;
        _roomDefinitions = definition.Rooms.ToDictionary(room => room.Id, StringComparer.Ordinal);
        Reset(1);
    }

    public ShipBattleDefinition Definition => _definition;
    public int Attempt { get; private set; }
    public long Tick { get; private set; }

    /// <summary>Fraction of the next fixed tick already accumulated; presentation-only interpolation.</summary>
    public double TickFraction => Paused || Phase != ShipBattlePhase.Active ? 0 : _accumulator / (double)TimeUnitsPerTick;
    public bool Paused { get; private set; }
    public ShipBattlePhase Phase { get; private set; }
    public IReadOnlyList<ShipEvent> Events => _events;

    public IEnumerable<ShipEvent> EventsSince(long sequence) => _events.Where(item => item.Sequence > sequence);

    /// <summary>Integer frame accumulation within the 0.25-second frame clamp and bounded catch-up budget.</summary>
    public int Advance(TimeSpan elapsed)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        if (Paused || Phase != ShipBattlePhase.Active) { return 0; }
        _accumulator += Math.Min(elapsed.Ticks, TimeSpan.TicksPerSecond / 4) * TicksPerSecond;
        var advanced = 0;
        while (_accumulator >= TimeUnitsPerTick && advanced < MaximumTicksPerFrame && !Paused && Phase == ShipBattlePhase.Active)
        {
            _accumulator -= TimeUnitsPerTick;
            AdvanceOneTick();
            advanced++;
        }
        if (advanced == MaximumTicksPerFrame) { _accumulator = Math.Min(_accumulator, TimeUnitsPerTick); }
        return advanced;
    }

    public int AdvanceTicks(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        var advanced = 0;
        while (advanced < count && !Paused && Phase == ShipBattlePhase.Active) { AdvanceOneTick(); advanced++; }
        return advanced;
    }

    public ShipCommandResult Execute(IShipCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var (rejection, warning) = Validate(command);
        if (rejection != ShipRejection.None)
        {
            Emit("command_rejected", command.CommandId.Value, rejection.ToString());
            return new ShipCommandResult(command.CommandId, false, rejection, null);
        }
        Apply(command);
        if (warning is not null) { Emit("warning", command.CommandId.Value, warning); }
        return new ShipCommandResult(command.CommandId, true, ShipRejection.None, warning);
    }

    private (ShipRejection, string?) Validate(IShipCommand command)
    {
        if (command is ShipSetPauseCommand or ShipRestartCommand) { return (ShipRejection.None, null); }
        if (Phase != ShipBattlePhase.Active) { return (ShipRejection.BattleEnded, null); }
        switch (command)
        {
            case ShipMoveCrewCommand move:
            {
                var crew = FindCrew(move.CrewId);
                if (crew is null) { return (ShipRejection.UnknownCrew, null); }
                if (crew.Downed) { return (ShipRejection.CrewDowned, null); }
                if (!_rooms.ContainsKey(move.RoomId)) { return (ShipRejection.UnknownRoom, null); }
                return (ShipRejection.None, RouteWarning(crew, move.RoomId));
            }
            case ShipAssignTaskCommand assign:
            {
                var crew = FindCrew(assign.CrewId);
                if (crew is null) { return (ShipRejection.UnknownCrew, null); }
                if (crew.Downed) { return (ShipRejection.CrewDowned, null); }
                if (!_rooms.TryGetValue(assign.RoomId, out var taskRoom)) { return (ShipRejection.UnknownRoom, null); }
                if (assign.Task == ShipTaskKind.None) { return (ShipRejection.UnknownSystem, null); }
                if (assign.Task is ShipTaskKind.Repair or ShipTaskKind.Man && SystemInRoom(assign.RoomId) is null)
                { return (ShipRejection.RoomHasNoSystem, null); }
                if (assign.Task == ShipTaskKind.Extinguish && taskRoom.Fire == 0
                    || assign.Task == ShipTaskKind.Seal && taskRoom.Breach == 0
                    || assign.Task == ShipTaskKind.Repair && SystemInRoom(assign.RoomId)!.Damage == 0)
                { return (ShipRejection.TaskHasNoWork, null); }
                if (assign.Task == ShipTaskKind.Treat)
                {
                    if (!crew.IsMedic) { return (ShipRejection.NotMedic, null); }
                    var target = assign.TargetCrewId is null ? null : FindCrew(assign.TargetCrewId);
                    if (target is null || target.Downed || target.Room != assign.RoomId) { return (ShipRejection.InvalidTreatTarget, null); }
                    if (target.Health == _definition.Crew.MaxHealth * 1000) { return (ShipRejection.TaskHasNoWork, null); }
                }
                return (ShipRejection.None, RouteWarning(crew, assign.RoomId));
            }
            case ShipSetCrewModeCommand mode:
            {
                var crew = FindCrew(mode.CrewId);
                return crew is null ? (ShipRejection.UnknownCrew, null) : crew.Downed ? (ShipRejection.CrewDowned, null) : (ShipRejection.None, null);
            }
            case ShipSetPowerCommand power:
            {
                if (power.Allocation is null || power.Allocation.Count != _player.Systems.Count
                    || _player.Systems.Keys.Any(id => !power.Allocation.ContainsKey(id)))
                { return (ShipRejection.PowerVectorIncomplete, null); }
                if (power.Allocation.Any(pair => pair.Value < 0 || pair.Value > _player.Systems[pair.Key].MaxPower))
                { return (ShipRejection.PowerExceedsCapacity, null); }
                return power.Allocation.Values.Sum() > _player.Reactor ? (ShipRejection.PowerExceedsReactor, null) : (ShipRejection.None, null);
            }
            case ShipSetWeaponPowerCommand arm:
                return FindWeapon(_player, arm.WeaponId) is null ? (ShipRejection.UnknownWeapon, null) : (ShipRejection.None, null);
            case ShipSetWeaponTargetCommand target:
                if (FindWeapon(_player, target.WeaponId) is null) { return (ShipRejection.UnknownWeapon, null); }
                return target.TargetSystemId is null || _enemy.Systems.ContainsKey(target.TargetSystemId)
                    ? (ShipRejection.None, null) : (ShipRejection.UnknownSystem, null);
            case ShipSetHoldFireCommand:
                return (ShipRejection.None, null);
            case ShipSetDoorCommand door:
                return _doors.ContainsKey(door.DoorId) ? (ShipRejection.None, null) : (ShipRejection.UnknownDoor, null);
            default:
                throw new ArgumentException($"Unsupported ship command {command.GetType().Name}.", nameof(command));
        }
    }

    private void Apply(IShipCommand command)
    {
        switch (command)
        {
            case ShipSetPauseCommand pause:
                Paused = pause.Paused;
                Emit(pause.Paused ? "paused" : "resumed", command.CommandId.Value, "");
                break;
            case ShipRestartCommand:
                Reset(Attempt + 1);
                Emit("restarted", command.CommandId.Value, "");
                break;
            case ShipMoveCrewCommand move:
            {
                var crew = FindCrew(move.CrewId)!;
                ClearTask(crew);
                PlanPath(crew, move.RoomId);
                Emit("crew_ordered", crew.Id, move.RoomId);
                break;
            }
            case ShipAssignTaskCommand assign:
            {
                var crew = FindCrew(assign.CrewId)!;
                crew.Task = assign.Task;
                crew.TaskRoom = assign.RoomId;
                crew.TreatTarget = assign.Task == ShipTaskKind.Treat ? assign.TargetCrewId : null;
                PlanPath(crew, assign.RoomId);
                Emit("task_assigned", crew.Id, $"{assign.Task}:{assign.RoomId}");
                break;
            }
            case ShipSetCrewModeCommand mode:
            {
                var crew = FindCrew(mode.CrewId)!;
                ClearTask(crew);
                crew.Mode = mode.Mode;
                Emit("crew_mode", crew.Id, mode.Mode.ToString());
                break;
            }
            case ShipSetPowerCommand power:
                foreach (var pair in power.Allocation) { _player.Systems[pair.Key].Power = pair.Value; }
                Emit("power_set", "player", string.Join(',', _player.Systems.Values.Select(system => system.Power)));
                RefreshWeaponPower(_player, "player", emit: true);
                break;
            case ShipSetWeaponPowerCommand arm:
            {
                var weapon = FindWeapon(_player, arm.WeaponId)!;
                weapon.Armed = arm.Armed;
                Emit(arm.Armed ? "weapon_armed" : "weapon_disarmed", "player", weapon.Definition.Id);
                RefreshWeaponPower(_player, "player", emit: true);
                break;
            }
            case ShipSetWeaponTargetCommand target:
                FindWeapon(_player, target.WeaponId)!.Target = target.TargetSystemId;
                Emit("target_set", target.WeaponId, target.TargetSystemId ?? "none");
                break;
            case ShipSetHoldFireCommand hold:
                _player.HoldFire = hold.Hold;
                Emit("hold_fire", "player", hold.Hold ? "hold" : "fire");
                break;
            case ShipSetDoorCommand door:
                _doors[door.DoorId].CommandedOpen = door.Open;
                Emit(_doors[door.DoorId].Definition.Exterior ? (door.Open ? "vent_opened" : "vent_closed") : "door_set", door.DoorId, door.Open ? "open" : "closed");
                break;
        }
    }

    private void Reset(int attempt)
    {
        Attempt = attempt;
        Tick = 0;
        Paused = true;
        Phase = ShipBattlePhase.Active;
        _accumulator = 0;
        _random = new SplitMix64(_seed ^ (ulong)attempt * 0xD1B54A32D192ED03UL);
        _player = new SideState(_definition.Player);
        _enemy = new SideState(_definition.Enemy.Side);
        RefreshWeaponPower(_player, "player", emit: false);
        RefreshWeaponPower(_enemy, "enemy", emit: false);
        _rooms = _definition.Rooms.ToDictionary(room => room.Id, room => new RoomState(), StringComparer.Ordinal);
        _doors = _definition.Doors.ToDictionary(door => door.Id, door => new DoorState(door), StringComparer.Ordinal);
        _crew = _crewSeeds.Select((seed, index) =>
        {
            var room = _roomDefinitions[_definition.Crew.StartRooms[index]];
            var (x, z) = WorkPosition(room, index);
            return new CrewState(seed, index, room.Id, x, z, _definition.Crew.MaxHealth * 1000);
        }).ToList();
        _shots = [];
        _enemyRepairSystem = null;
        _enemyRepairProgress = 0;
        _enemyRepairReserve = _definition.Enemy.Repair.ReserveBars;
    }

    private void AdvanceOneTick()
    {
        Tick++;
        foreach (var crew in _crew) { crew.PreviousX = crew.X; crew.PreviousZ = crew.Z; }
        MoveCrew();
        ResolveWork();
        AdvanceShips();
        ResolveImpacts();
        AdvanceAtmosphere();
        AdvanceFire();
        DamageCrew();
        foreach (var door in _doors.Values) { door.TraversalTicks = Math.Max(0, door.TraversalTicks - 1); }
        RefreshWeaponPower(_player, "player", emit: true);
        RefreshWeaponPower(_enemy, "enemy", emit: true);
        ResolveTerminal();
    }

    private void MoveCrew()
    {
        var step = _definition.Crew.SpeedMetersPerSecond / TicksPerSecond;
        var window = Ticks(_definition.Crew.DoorTraversalWindowSeconds);
        foreach (var crew in _crew.Where(item => !item.Downed && item.Path.Count > 0))
        {
            var remaining = step;
            while (remaining > 0 && crew.Path.Count > 0)
            {
                var next = crew.Path[0];
                if (next.DoorId is not null && Distance(crew.X, crew.Z, next.X, next.Z) <= DoorwayRadius)
                {
                    var door = _doors[next.DoorId];
                    // A closed internal door opens for one fixed, bounded traversal window; it exchanges gas and passes fire normally.
                    if (!door.CommandedOpen && door.TraversalTicks == 0)
                    {
                        door.TraversalTicks = window;
                        Emit("door_traversal", door.Definition.Id, crew.Id);
                    }
                }
                var distance = Distance(crew.X, crew.Z, next.X, next.Z);
                if (distance <= remaining)
                {
                    crew.X = next.X; crew.Z = next.Z; remaining -= distance;
                    crew.Room = next.RoomAfter;
                    crew.Path.RemoveAt(0);
                    if (crew.Path.Count == 0) { Emit("crew_arrived", crew.Id, crew.Room); }
                }
                else
                {
                    crew.X += (next.X - crew.X) / distance * remaining;
                    crew.Z += (next.Z - crew.Z) / distance * remaining;
                    remaining = 0;
                }
            }
        }
    }

    private void ResolveWork()
    {
        foreach (var system in _player.Systems.Values) { system.Manned = false; }
        var previousWork = _crew.Select(crew => crew.CurrentWork).ToArray();
        var groups = new SortedDictionary<string, List<CrewState>>(StringComparer.Ordinal);
        foreach (var crew in _crew)
        {
            crew.CurrentWork = ShipTaskKind.None;
            crew.CurrentTreatTarget = null;
            if (crew.Downed) { continue; }
            if (crew.Task != ShipTaskKind.None && !TaskStillValid(crew))
            {
                Emit("task_finished", crew.Id, crew.Task.ToString());
                if (crew.Task is ShipTaskKind.Extinguish or ShipTaskKind.Seal or ShipTaskKind.Repair) { crew.Mode = ShipCrewMode.Auto; }
                ClearTask(crew);
            }
            if (crew.Path.Count > 0) { continue; }
            var (work, patient) = crew.Task != ShipTaskKind.None
                ? (crew.Room == crew.TaskRoom ? crew.Task : ShipTaskKind.None, crew.TreatTarget)
                : crew.Mode == ShipCrewMode.Auto ? AutoWork(crew) : (ShipTaskKind.None, null);
            if (work == ShipTaskKind.None) { continue; }
            crew.CurrentWork = work;
            crew.CurrentTreatTarget = work == ShipTaskKind.Treat ? patient : null;
            var key = $"{work}|{crew.Room}|{crew.CurrentTreatTarget}";
            if (!groups.TryGetValue(key, out var workers)) { groups[key] = workers = []; }
            workers.Add(crew);
        }

        foreach (var workers in groups.Values)
        {
            var room = workers[0].Room;
            var state = _rooms[room];
            var rate = 1000 + (workers.Count >= 2 ? _definition.Work.SecondWorkerPercent * 10 : 0);
            switch (workers[0].CurrentWork)
            {
                case ShipTaskKind.Extinguish:
                    state.FireWork += rate;
                    var fireCost = Ticks(_definition.Work.ExtinguishSecondsPerSeverity) * 1000;
                    if (state.FireWork >= fireCost)
                    {
                        state.FireWork = 0; state.Fire--;
                        if (state.Fire == 0) { state.FireSpread = 0; state.FireSystem = 0; }
                        Emit(state.Fire == 0 ? "fire_extinguished" : "fire_reduced", room, state.Fire.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    break;
                case ShipTaskKind.Seal:
                    state.SealWork += rate;
                    var sealCost = Ticks(_definition.Work.SealSecondsPerSeverity) * 1000;
                    if (state.SealWork >= sealCost)
                    {
                        // Sealing stops the leak; it never refunds hull.
                        state.SealWork = 0; state.Breach--;
                        Emit(state.Breach == 0 ? "breach_sealed" : "breach_reduced", room, state.Breach.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    break;
                case ShipTaskKind.Repair:
                    var system = SystemInRoom(room)!;
                    system.RepairWork += rate;
                    if (system.RepairWork >= Ticks(_definition.Work.RepairSecondsPerBar) * 1000)
                    {
                        system.RepairWork = 0; system.Damage--;
                        Emit("system_repaired", system.Definition.Id, system.Damage.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    break;
                case ShipTaskKind.Man:
                    var manned = SystemInRoom(room)!;
                    // Nonstacking bonus that requires an operational, hazard-free station.
                    manned.Manned = manned.Damage < manned.MaxPower && state.Fire == 0 && state.Breach == 0;
                    break;
                case ShipTaskKind.Treat:
                    var target = FindCrew(workers[0].CurrentTreatTarget!)!;
                    var max = _definition.Crew.MaxHealth * 1000;
                    target.Health = Math.Min(max, target.Health + PerTick(_definition.Crew.MedicHealPerSecond * 1000));
                    break;
            }
        }
        for (var index = 0; index < _crew.Count; index++)
        {
            if (_crew[index].CurrentWork != previousWork[index]) { _crew[index].WorkSinceTick = Tick; }
        }
    }

    private bool TaskStillValid(CrewState crew)
    {
        var room = _rooms[crew.TaskRoom!];
        return crew.Task switch
        {
            ShipTaskKind.Extinguish => room.Fire > 0,
            ShipTaskKind.Seal => room.Breach > 0,
            ShipTaskKind.Repair => SystemInRoom(crew.TaskRoom!)!.Damage > 0,
            ShipTaskKind.Man => true,
            ShipTaskKind.Treat => FindCrew(crew.TreatTarget!) is { Downed: false } target
                && target.Health < _definition.Crew.MaxHealth * 1000
                && (crew.Path.Count > 0 || crew.Room != crew.TaskRoom || (target.Room == crew.Room && target.Path.Count == 0)),
            _ => false,
        };
    }

    /// <summary>Current-room automation: fire, breach, (Medic) the most injured settled ally, repair, man.</summary>
    private (ShipTaskKind Work, string? Patient) AutoWork(CrewState crew)
    {
        var state = _rooms[crew.Room];
        if (state.Fire > 0) { return (ShipTaskKind.Extinguish, null); }
        if (state.Breach > 0) { return (ShipTaskKind.Seal, null); }
        if (crew.IsMedic)
        {
            var max = _definition.Crew.MaxHealth * 1000;
            var patient = _crew.Where(other => !other.Downed && other.Room == crew.Room && other.Path.Count == 0 && other.Health < max)
                .OrderBy(other => other.Health).ThenBy(other => other.Slot).FirstOrDefault();
            if (patient is not null) { return (ShipTaskKind.Treat, patient.Id); }
        }
        var system = SystemInRoom(crew.Room);
        if (system is null) { return (ShipTaskKind.None, null); }
        return (system.Damage > 0 ? ShipTaskKind.Repair : ShipTaskKind.Man, null);
    }

    private void AdvanceShips()
    {
        AdvanceShields(_player, "player");
        AdvanceShields(_enemy, "enemy");
        AdvanceWeapons(_player, "player");
        AdvanceWeapons(_enemy, "enemy");
        AdvanceEnemyRepair();
    }

    private void AdvanceShields(SideState side, string name)
    {
        var shields = side.Systems["shields"];
        var capacity = shields.Effective;
        if (side.ShieldLayers > capacity) { side.ShieldLayers = capacity; side.ShieldRegen = 0; }
        if (side.ShieldLayers >= capacity || Tick - side.LastHitTick < Ticks(side.Definition.Shield.RegenDelaySeconds)) { return; }
        side.ShieldRegen += Output(side, shields);
        if (side.ShieldRegen >= ShieldRegenRequired(side))
        {
            side.ShieldRegen = 0; side.ShieldLayers++;
            Emit("shield_restored", name, side.ShieldLayers.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    /// <summary>Weapons draw weapons-system power in mounting order: armed weapons that fit are powered, the rest are not.</summary>
    private void RefreshWeaponPower(SideState side, string name, bool emit)
    {
        var remaining = side.Systems["weapons"].Effective;
        foreach (var weapon in side.Weapons)
        {
            var powered = weapon.Armed && weapon.Definition.PowerCost <= remaining;
            if (powered) { remaining -= weapon.Definition.PowerCost; }
            if (powered == weapon.Powered) { continue; }
            weapon.Powered = powered;
            if (emit) { Emit(powered ? "weapon_powered" : "weapon_unpowered", name, weapon.Definition.Id); }
        }
    }

    private void AdvanceWeapons(SideState side, string name)
    {
        RefreshWeaponPower(side, name, emit: true);
        var output = Output(side, side.Systems["weapons"]);
        foreach (var weapon in side.Weapons)
        {
            var definition = weapon.Definition;
            // Unpowered weapons bleed charge; power switching never creates it.
            if (!weapon.Powered) { weapon.Charge = Math.Max(0, weapon.Charge - UnpoweredChargeBleed); continue; }
            var required = ChargeRequired(definition);
            weapon.Charge = Math.Min(required, weapon.Charge + output);
            var target = WeaponTarget(side, weapon);
            if (weapon.Charge < required || target is null || side.HoldFire || weapon.Ammo == 0) { continue; }
            weapon.Charge = 0;
            var payload = side == _enemy ? definition.PayloadSequence[weapon.Volleys % definition.PayloadSequence.Count] : ShipPayload.Normal;
            for (var index = 0; index < definition.Shots; index++)
            {
                var launch = Tick + index * SpacingTicks(definition.ShotSpacingSeconds);
                _shots.Add(new Shot(++_shotSequence, name, definition, target, payload, launch, launch + Ticks(definition.TravelSeconds)));
            }
            if (!definition.UnlimitedAmmo) { weapon.Ammo--; }
            weapon.Volleys++;
            Emit("weapon_fired", name, $"{definition.Id}:{target}:{payload}");
        }
    }

    private string? WeaponTarget(SideState side, WeaponState weapon) =>
        side == _enemy ? weapon.Definition.TargetSequence[weapon.Volleys % weapon.Definition.TargetSequence.Count] : weapon.Target;

    private void AdvanceEnemyRepair()
    {
        if (_enemyRepairReserve == 0) { _enemyRepairSystem = null; return; }
        if (_enemyRepairSystem is null || _enemy.Systems[_enemyRepairSystem].Damage == 0)
        {
            _enemyRepairSystem = _definition.Enemy.Repair.Priority.FirstOrDefault(id => _enemy.Systems[id].Damage > 0);
            _enemyRepairProgress = 0;
            if (_enemyRepairSystem is null) { return; }
            Emit("enemy_repair_started", _enemyRepairSystem, "");
        }
        _enemyRepairProgress += 1000;
        if (_enemyRepairProgress < Ticks(_definition.Enemy.Repair.SecondsPerBar) * 1000) { return; }
        _enemy.Systems[_enemyRepairSystem].Damage--;
        _enemyRepairReserve--;
        _enemyRepairProgress = 0;
        Emit("enemy_repaired", _enemyRepairSystem, _enemyRepairReserve.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private void ResolveImpacts()
    {
        // Every impact due this tick resolves, in launch order, before the terminal check.
        foreach (var shot in _shots.Where(item => item.ImpactTick <= Tick).ToArray())
        {
            _shots.Remove(shot);
            var defender = shot.Source == "player" ? _enemy : _player;
            var defenderName = shot.Source == "player" ? "enemy" : "player";
            var detail = $"{shot.TargetSystem}:{shot.Weapon.Id}:{shot.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            if (shot.Weapon.Evadable && _random.Next(100) < Evasion(defender))
            {
                Emit("shot_missed", defenderName, detail);
                continue;
            }
            defender.LastHitTick = Tick;
            if (!shot.Weapon.PiercesShields && defender.ShieldLayers > 0)
            {
                defender.ShieldLayers--; defender.ShieldRegen = 0;
                Emit("shield_absorbed", defenderName, detail);
                continue;
            }
            defender.Hull = Math.Max(0, defender.Hull - shot.Weapon.Damage);
            var system = defender.Systems[shot.TargetSystem];
            system.Damage = Math.Min(system.MaxPower, system.Damage + shot.Weapon.Damage);
            Emit("hull_hit", defenderName, $"{detail}:{shot.Weapon.Damage.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            if (defender != _player) { continue; }
            var roomId = system.Definition.Room;
            var injury = _definition.Crew.HitDamagePerPoint * shot.Weapon.Damage * 1000;
            foreach (var crew in _crew.Where(item => !item.Downed && item.Room == roomId && injury > 0))
            {
                crew.Health = Math.Max(0, crew.Health - injury);
                Emit("crew_hit", crew.Id, (injury / 1000).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            var room = _rooms[roomId];
            var ignite = shot.Payload == ShipPayload.Incendiary || Roll(shot.Weapon.FireChancePercent);
            var tear = shot.Payload == ShipPayload.Breach || Roll(shot.Weapon.BreachChancePercent);
            if (ignite && room.Fire < _definition.Hazards.MaxFireSeverity && room.Oxygen >= FireMinimumOxygen)
            { room.Fire++; Emit("fire_started", roomId, room.Fire.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
            if (tear && room.Breach < _definition.Hazards.MaxBreachSeverity)
            { room.Breach++; Emit("breach_opened", roomId, room.Breach.ToString(System.Globalization.CultureInfo.InvariantCulture)); }
        }
    }

    private bool Roll(int percent) => percent > 0 && _random.Next(100) < percent;

    private void AdvanceAtmosphere()
    {
        var air = _definition.Atmosphere;
        var previous = _rooms.ToDictionary(pair => pair.Key, pair => pair.Value.Oxygen, StringComparer.Ordinal);
        var delta = previous.Keys.ToDictionary(key => key, _ => 0L, StringComparer.Ordinal);
        foreach (var door in _doors.Values.Where(item => item.EffectivelyOpen))
        {
            var from = door.Definition.RoomA;
            if (door.Definition.Exterior) { delta[from] -= previous[from] * (long)air.VentPermillePerTick / 1000; continue; }
            var to = door.Definition.RoomB;
            var flow = (previous[from] - previous[to]) * (long)air.DoorExchangePermillePerTick / 1000;
            delta[from] -= flow;
            delta[to] += flow;
        }
        var lifeSupport = _player.Systems["life_support"];
        var replenish = lifeSupport.Effective == 0 ? 0
            : PerTick(air.LifeSupportPercentPerSecondPerPower * FullOxygen / 100.0 * lifeSupport.Effective * (100 + (lifeSupport.Manned ? _player.Definition.ManningBonusPercent : 0)) / 100.0);
        foreach (var (id, room) in _rooms)
        {
            delta[id] -= previous[id] * (long)air.BreachLeakPermillePerTickPerSeverity * room.Breach / 1000;
            delta[id] += replenish;
            delta[id] -= PerTick(air.FireConsumptionPercentPerSecondPerSeverity * FullOxygen / 100.0) * (long)room.Fire;
            delta[id] -= PerTick(air.CrewConsumptionPercentPerSecond * FullOxygen / 100.0) * (long)_crew.Count(crew => !crew.Downed && crew.Room == id);
        }
        foreach (var (id, room) in _rooms) { room.Oxygen = (int)Math.Clamp(previous[id] + delta[id], 0, FullOxygen); }
    }

    private void AdvanceFire()
    {
        var hazards = _definition.Hazards;
        var spreading = new List<string>();
        foreach (var (id, room) in _rooms.Where(pair => pair.Value.Fire > 0))
        {
            if (room.Oxygen < FireMinimumOxygen)
            {
                room.Fire = 0; room.FireWork = 0; room.FireSpread = 0; room.FireSystem = 0;
                Emit("fire_suffocated", id, "");
                continue;
            }
            room.FireSystem += room.Fire;
            if (room.FireSystem >= Ticks(hazards.FireSystemDamageSeconds) && SystemInRoom(id) is { } system)
            {
                room.FireSystem = 0;
                if (system.Damage < system.MaxPower) { system.Damage++; Emit("fire_damaged_system", system.Definition.Id, ""); }
            }
            if (++room.FireSpread >= Ticks(hazards.FireSpreadSeconds)) { room.FireSpread = 0; spreading.Add(id); }
        }
        foreach (var source in spreading)
        {
            foreach (var door in _doors.Values.Where(item => item.EffectivelyOpen && !item.Definition.Exterior))
            {
                var other = door.Definition.RoomA == source ? door.Definition.RoomB : door.Definition.RoomB == source ? door.Definition.RoomA : null;
                if (other is null || _rooms[other].Fire > 0 || _rooms[other].Oxygen < FireMinimumOxygen) { continue; }
                _rooms[other].Fire = 1;
                Emit("fire_spread", other, source);
            }
        }
    }

    private void DamageCrew()
    {
        var air = _definition.Atmosphere;
        foreach (var crew in _crew.Where(item => !item.Downed))
        {
            var room = _rooms[crew.Room];
            var damage = PerTick(_definition.Hazards.FireCrewDamagePerSecondPerSeverity * 1000) * room.Fire;
            if (room.Oxygen < air.SuffocationThresholdPercent * FullOxygen / 100) { damage += PerTick(air.SuffocationDamagePerSecond * 1000); }
            crew.Health = Math.Max(0, crew.Health - damage);
            // Weapon hits resolved earlier this tick may also have emptied health.
            if (crew.Health > 0) { continue; }
            crew.Downed = true;
            crew.Path.Clear();
            ClearTask(crew);
            Emit("crew_downed", crew.Id, crew.Room);
        }
    }

    private void ResolveTerminal()
    {
        var defeat = _player.Hull <= 0 || _crew.All(crew => crew.Downed);
        if (!defeat && _enemy.Hull > 0) { return; }
        Phase = defeat ? ShipBattlePhase.Defeat : ShipBattlePhase.Victory;
        _shots.Clear();
        if (Phase == ShipBattlePhase.Victory)
        {
            foreach (var crew in _crew.Where(item => item.Downed))
            { crew.Downed = false; crew.Health = _definition.Crew.VictoryRestoreHealth * 1000; Emit("crew_recovered", crew.Id, ""); }
        }
        Emit(defeat ? "defeat" : "victory", _player.Hull <= 0 ? "hull" : defeat ? "crew" : "enemy", "");
    }

    private void PlanPath(CrewState crew, string destination)
    {
        var keep = RetainedDoorway(crew);
        crew.Path.Clear();
        var room = crew.Room;
        if (keep is { } doorway) { crew.Path.Add(doorway); room = doorway.RoomAfter; } // finish the current doorway crossing
        crew.Destination = destination;
        if (room != destination)
        {
            if (room != ShipBattleDefinition.Passage) { AddDoor(crew, room, ShipBattleDefinition.Passage); }
            if (destination != ShipBattleDefinition.Passage) { AddDoor(crew, ShipBattleDefinition.Passage, destination); }
        }
        var (x, z) = WorkPosition(_roomDefinitions[destination], crew.Slot);
        crew.Path.Add(new Waypoint(x, z, null, destination));
    }

    private void AddDoor(CrewState crew, string from, string to)
    {
        var door = _doors.Values.First(item => !item.Definition.Exterior
            && (item.Definition.RoomA == from && item.Definition.RoomB == to || item.Definition.RoomA == to && item.Definition.RoomB == from));
        crew.Path.Add(new Waypoint(door.Definition.X, door.Definition.Z, door.Definition.Id, to));
    }

    private string? RouteWarning(CrewState crew, string destination)
    {
        var start = RetainedDoorway(crew)?.RoomAfter ?? crew.Room;
        var rooms = (start == destination ? new[] { crew.Room, destination }
            : new[] { crew.Room, start, ShipBattleDefinition.Passage, destination }).Distinct(StringComparer.Ordinal);
        var unsafeRooms = rooms.Where(id => IsDangerous(_rooms[id])).ToArray();
        return unsafeRooms.Length == 0 ? null : $"unsafe_route:{string.Join('+', unsafeRooms)}";
    }

    private static Waypoint? RetainedDoorway(CrewState crew) =>
        crew.Path.Count > 0 && crew.Path[0].DoorId is not null
        && Distance(crew.X, crew.Z, crew.Path[0].X, crew.Path[0].Z) <= DoorwayRadius ? crew.Path[0] : null;

    private bool IsDangerous(RoomState room) => room.Fire > 0 || room.Breach > 0
        || room.Oxygen < _definition.Atmosphere.SuffocationThresholdPercent * FullOxygen / 100;

    private static (double X, double Z) WorkPosition(ShipRoomDefinition room, int slot)
    {
        var offsets = new[] { (0.0, 0.0), (-0.32, 0.28), (0.32, 0.28) };
        var (dx, dz) = offsets[slot % offsets.Length];
        var halfWidth = Math.Max(0, room.Width / 2 - 0.3);
        var halfDepth = Math.Max(0, room.Depth / 2 - 0.3);
        return (Math.Clamp(room.WorkX + dx, room.X - halfWidth, room.X + halfWidth), Math.Clamp(room.WorkZ + dz, room.Z - halfDepth, room.Z + halfDepth));
    }

    private static void ClearTask(CrewState crew) { crew.Task = ShipTaskKind.None; crew.TaskRoom = null; crew.TreatTarget = null; }

    private CrewState? FindCrew(string id) => _crew.FirstOrDefault(crew => crew.Id == id);

    private static WeaponState? FindWeapon(SideState side, string id) => side.Weapons.FirstOrDefault(weapon => weapon.Definition.Id == id);

    private SystemState? SystemInRoom(string room) => _player.Systems.Values.FirstOrDefault(system => system.Definition.Room == room);

    private int FireMinimumOxygen => (int)(_definition.Hazards.FireMinOxygenPercent * FullOxygen / 100);

    private static int Output(SideState side, SystemState system) =>
        system.Effective == 0 ? 0 : 1000 * (100 + (system.Manned ? side.Definition.ManningBonusPercent : 0)) / 100;

    private static int ChargeRequired(ShipWeaponDefinition weapon) => Ticks(weapon.ChargeSeconds) * 1000;

    private static int ShieldRegenRequired(SideState side) => Ticks(side.Definition.Shield.RegenSecondsPerLayer) * 1000;

    /// <summary>Chance, in whole percent, that one evadable shot misses: powered engines, boosted by a manned station.</summary>
    private static int Evasion(SideState side)
    {
        var engines = side.Systems["engines"];
        if (engines.Effective == 0) { return 0; }
        var bonus = engines.Manned ? side.Definition.ManningBonusPercent : 0;
        return Math.Min(side.Definition.EvasionMaxPercent, engines.Effective * side.Definition.EvasionPercentPerEnginePower * (100 + bonus) / 100);
    }

    private static int Ticks(double seconds) => Math.Max(1, (int)Math.Round(seconds * TicksPerSecond, MidpointRounding.AwayFromZero));

    private static int SpacingTicks(double seconds) => (int)Math.Round(seconds * TicksPerSecond, MidpointRounding.AwayFromZero);

    private static int PerTick(double perSecond) => (int)Math.Round(perSecond / TicksPerSecond, MidpointRounding.AwayFromZero);

    private static double Distance(double ax, double az, double bx, double bz) => Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));

    private void Emit(string kind, string subject, string detail) =>
        _events.Add(new ShipEvent(++_eventSequence, Attempt, Tick, kind, subject, detail));

    public ShipBattleObservation Observe() => new(
        Attempt, Phase, Paused, Tick, ObserveSide(_player, player: true), ObserveSide(_enemy, player: false),
        _enemy.Weapons.Select(weapon => new ShipEnemyIntent(weapon.Definition.Id, weapon.Definition.DisplayName, weapon.Definition.Kind,
            WeaponTarget(_enemy, weapon)!, weapon.Definition.PayloadSequence[weapon.Volleys % weapon.Definition.PayloadSequence.Count],
            weapon.Ammo == 0 ? -1 : TicksToFire(_enemy, weapon), weapon.Definition.Shots, weapon.Definition.Damage, weapon.Powered, weapon.Ammo)).ToArray(),
        new ShipEnemyRepairObservation(_enemyRepairSystem, _enemyRepairSystem is null ? 0 : _enemyRepairProgress / Math.Max(1, Ticks(_definition.Enemy.Repair.SecondsPerBar)), _enemyRepairReserve),
        _definition.Rooms.Select(room => new ShipRoomObservation(room.Id, _rooms[room.Id].Oxygen / 100, _rooms[room.Id].Fire, _rooms[room.Id].Breach,
            _rooms[room.Id].FireWork / Math.Max(1, Ticks(_definition.Work.ExtinguishSecondsPerSeverity)),
            _rooms[room.Id].SealWork / Math.Max(1, Ticks(_definition.Work.SealSecondsPerSeverity)))).ToArray(),
        _doors.Values.Select(door => new ShipDoorObservation(door.Definition.Id, door.Definition.RoomA, door.Definition.RoomB, door.Definition.Exterior,
            door.CommandedOpen, door.EffectivelyOpen, door.TraversalTicks)).ToArray(),
        _crew.Select(crew => new ShipCrewObservation(crew.Id, crew.Seed.DisplayName, crew.IsMedic, crew.Room, crew.X, crew.Z,
            (crew.Health + 999) / 1000, _definition.Crew.MaxHealth, crew.Downed, crew.Path.Count > 0, crew.Mode, crew.Task, crew.TaskRoom,
            crew.CurrentTreatTarget ?? crew.TreatTarget, crew.CurrentWork, crew.Path.Count > 0 ? crew.Destination : null, IsDangerous(_rooms[crew.Room]),
            crew.PreviousX, crew.PreviousZ, crew.WorkSinceTick)).ToArray(),
        _shots.Select(shot => new ShipShotObservation(shot.Id, shot.Source, shot.Weapon.Id, shot.Weapon.Kind, shot.TargetSystem, shot.Payload,
            shot.Weapon.Damage, shot.LaunchTick, shot.ImpactTick)).ToArray());

    private ShipSideObservation ObserveSide(SideState side, bool player) => new(
        side.Definition.DisplayName, side.Hull, side.Definition.Hull, side.Reactor, side.ShieldLayers, side.Systems["shields"].Effective,
        side.ShieldLayers >= side.Systems["shields"].Effective ? 0 : (int)((long)side.ShieldRegen * 1000 / ShieldRegenRequired(side)), Evasion(side),
        side.Systems.Values.Select(system => new ShipSystemObservation(system.Definition.Id, system.Definition.Room, system.MaxPower, system.Power,
            system.Damage, system.Effective, system.Manned, player ? system.RepairWork / Math.Max(1, Ticks(_definition.Work.RepairSecondsPerBar)) : 0)).ToArray(),
        side.Weapons.Select(weapon => new ShipWeaponObservation(weapon.Definition.Id, weapon.Definition.DisplayName, weapon.Definition.Kind,
            weapon.Definition.PowerCost, weapon.Armed, weapon.Powered, (int)((long)weapon.Charge * 1000 / ChargeRequired(weapon.Definition)),
            WeaponTarget(side, weapon), weapon.Definition.Shots, weapon.Definition.Damage, weapon.Definition.PiercesShields, weapon.Definition.Evadable,
            weapon.Ammo, TicksToFire(side, weapon))).ToArray(),
        side.HoldFire);

    private static int TicksToFire(SideState side, WeaponState weapon)
    {
        var rate = weapon.Powered ? Output(side, side.Systems["weapons"]) : 0;
        return rate <= 0 ? -1 : (ChargeRequired(weapon.Definition) - weapon.Charge + rate - 1) / rate;
    }

    private readonly record struct Waypoint(double X, double Z, string? DoorId, string RoomAfter);

    private sealed record Shot(long Id, string Source, ShipWeaponDefinition Weapon, string TargetSystem, ShipPayload Payload, long LaunchTick, long ImpactTick);

    /// <summary>SplitMix64: a small, fully specified generator so rolls never depend on runtime library versions.</summary>
    private sealed class SplitMix64(ulong seed)
    {
        private ulong _state = seed;

        public int Next(int maximumExclusive)
        {
            var z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return (int)((z ^ (z >> 31)) % (ulong)maximumExclusive);
        }
    }

    private sealed class SideState
    {
        public SideState(ShipSideDefinition definition)
        {
            Definition = definition;
            Hull = definition.Hull;
            Reactor = definition.Reactor;
            Systems = definition.Systems.ToDictionary(system => system.Id, system => new SystemState(system), StringComparer.Ordinal);
            Weapons = definition.Weapons.Select(weapon => new WeaponState(weapon)).ToList();
            ShieldLayers = Systems["shields"].Effective;
            LastHitTick = -1_000_000;
        }

        public ShipSideDefinition Definition { get; }
        public int Hull { get; set; }
        public int Reactor { get; }
        public Dictionary<string, SystemState> Systems { get; }
        public List<WeaponState> Weapons { get; }
        public int ShieldLayers { get; set; }
        public int ShieldRegen { get; set; }
        public long LastHitTick { get; set; }
        public bool HoldFire { get; set; }
    }

    private sealed class WeaponState(ShipWeaponDefinition definition)
    {
        public ShipWeaponDefinition Definition { get; } = definition;
        public bool Armed { get; set; } = true;
        public bool Powered { get; set; }
        public int Charge { get; set; }
        public string? Target { get; set; }
        public int Ammo { get; set; } = definition.Ammo;
        public int Volleys { get; set; }
    }

    private sealed class SystemState(ShipSystemDefinition definition)
    {
        public ShipSystemDefinition Definition { get; } = definition;
        public int MaxPower => Definition.MaxPower;
        public int Power { get; set; } = definition.InitialPower;
        public int Damage { get; set; }
        public int RepairWork { get; set; }
        public bool Manned { get; set; }

        // Damage caps usable power; allocated-but-unusable power stays visibly allocated.
        public int Effective => Math.Min(Power, MaxPower - Damage);
    }

    private sealed class RoomState
    {
        public int Oxygen { get; set; } = FullOxygen;
        public int Fire { get; set; }
        public int Breach { get; set; }
        public int FireWork { get; set; }
        public int SealWork { get; set; }
        public int FireSpread { get; set; }
        public int FireSystem { get; set; }
    }

    private sealed class DoorState(ShipDoorDefinition definition)
    {
        public ShipDoorDefinition Definition { get; } = definition;
        public bool CommandedOpen { get; set; }
        public int TraversalTicks { get; set; }
        public bool EffectivelyOpen => CommandedOpen || TraversalTicks > 0;
    }

    private sealed class CrewState(ShipCrewSeed seed, int slot, string room, double x, double z, int health)
    {
        public ShipCrewSeed Seed { get; } = seed;
        public string Id => Seed.Id;
        public bool IsMedic => Seed.IsMedic;
        public int Slot { get; } = slot;
        public string Room { get; set; } = room;
        public double X { get; set; } = x;
        public double Z { get; set; } = z;
        public double PreviousX { get; set; } = x;
        public double PreviousZ { get; set; } = z;
        public int Health { get; set; } = health;
        public bool Downed { get; set; }
        public ShipCrewMode Mode { get; set; }
        public ShipTaskKind Task { get; set; }
        public string? TaskRoom { get; set; }
        public string? TreatTarget { get; set; }
        public string? CurrentTreatTarget { get; set; }
        public ShipTaskKind CurrentWork { get; set; }
        public long WorkSinceTick { get; set; }
        public string? Destination { get; set; }
        public List<Waypoint> Path { get; } = [];
    }
}
