using System.Text.Json;

namespace SpaceAdventure.Core;

public enum ShipPayload
{
    Normal,
    Incendiary,
    Breach,
}

public enum ShipWeaponKind
{
    Laser,
    Missile,
}

/// <summary>
/// One mounted weapon. Lasers are stopped by shield layers and can be evaded; missiles may pierce shields and
/// ignore evasion. Enemy weapons follow authored target/payload sequences; player weapons are aimed by command.
/// </summary>
public sealed record ShipWeaponDefinition(
    string Id, string DisplayName, ShipWeaponKind Kind, int PowerCost, double ChargeSeconds, int Shots, int Damage,
    double TravelSeconds, double ShotSpacingSeconds, bool PiercesShields, bool Evadable, int Ammo,
    int FireChancePercent, int BreachChancePercent, IReadOnlyList<string> TargetSequence, IReadOnlyList<ShipPayload> PayloadSequence)
{
    public bool UnlimitedAmmo => Ammo < 0;
}

public sealed record ShipShieldDefinition(double RegenSecondsPerLayer, double RegenDelaySeconds);

/// <summary>Enemy systems carry their own room rectangle (presentation picking); player systems name a room.</summary>
public sealed record ShipSystemDefinition(string Id, string Room, int MaxPower, int InitialPower, double X, double Z, double Width, double Depth);

public sealed record ShipRoomDefinition(string Id, double X, double Z, double Width, double Depth, double WorkX, double WorkZ);

public sealed record ShipDoorDefinition(string Id, string RoomA, string RoomB, double X, double Z, bool Exterior);

public sealed record ShipCrewTuning(
    IReadOnlyList<string> StartRooms, double SpeedMetersPerSecond, int MaxHealth, int VictoryRestoreHealth,
    double MedicHealPerSecond, double DoorTraversalWindowSeconds, int HitDamagePerPoint);

public sealed record ShipWorkTuning(double ExtinguishSecondsPerSeverity, double SealSecondsPerSeverity, double RepairSecondsPerBar, int SecondWorkerPercent);

public sealed record ShipAtmosphereTuning(
    int DoorExchangePermillePerTick, int VentPermillePerTick, int BreachLeakPermillePerTickPerSeverity,
    double LifeSupportPercentPerSecondPerPower, double CrewConsumptionPercentPerSecond,
    double FireConsumptionPercentPerSecondPerSeverity, double SuffocationThresholdPercent, double SuffocationDamagePerSecond);

public sealed record ShipHazardTuning(
    double FireMinOxygenPercent, double FireCrewDamagePerSecondPerSeverity, double FireSystemDamageSeconds,
    double FireSpreadSeconds, int MaxFireSeverity, int MaxBreachSeverity);

public sealed record ShipRepairDefinition(int ReserveBars, double SecondsPerBar, IReadOnlyList<string> Priority);

public sealed record ShipSideDefinition(
    string DisplayName, int Hull, int Reactor, IReadOnlyList<ShipSystemDefinition> Systems, IReadOnlyList<ShipWeaponDefinition> Weapons,
    ShipShieldDefinition Shield, int EvasionPercentPerEnginePower, int EvasionMaxPercent, int ManningBonusPercent);

public sealed record ShipEnemyDefinition(ShipSideDefinition Side, ShipRepairDefinition Repair);

/// <summary>Authored ship battle content. Every tunable number lives in the JSON, not in scenes.</summary>
public sealed record ShipBattleDefinition(
    string Id, ulong Seed, ShipSideDefinition Player, IReadOnlyList<ShipRoomDefinition> Rooms, IReadOnlyList<ShipDoorDefinition> Doors,
    ShipCrewTuning Crew, ShipWorkTuning Work, ShipAtmosphereTuning Atmosphere, ShipHazardTuning Hazards, ShipEnemyDefinition Enemy)
{
    public const int SchemaVersion = 2;
    public const string Vacuum = "vacuum";
    public const string Passage = "passage";
    public static readonly IReadOnlyList<string> PlayerSystemIds = ["weapons", "shields", "life_support", "engines"];
    public static readonly IReadOnlyList<string> EnemySystemIds = ["weapons", "shields", "engines"];

    public static ShipBattleDefinition ParseJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (Int(root, "schema_version") != SchemaVersion) { throw new InvalidDataException($"Ship battle schema_version must be {SchemaVersion}."); }
        var rooms = Array(root, "rooms").Select(room => new ShipRoomDefinition(Str(room, "id"), Num(room, "x"), Num(room, "z"),
            Num(room, "width"), Num(room, "depth"), Num(room, "work_x"), Num(room, "work_z"))).ToArray();
        var doors = Array(root, "doors").Select(door => new ShipDoorDefinition(Str(door, "id"), Str(door, "room_a"), Str(door, "room_b"),
            Num(door, "x"), Num(door, "z"), door.GetProperty("exterior").GetBoolean())).ToArray();
        var player = ParseSide(root.GetProperty("player"), enemy: false);
        var crew = root.GetProperty("crew");
        var work = root.GetProperty("work");
        var air = root.GetProperty("atmosphere");
        var hazard = root.GetProperty("hazards");
        var enemyElement = root.GetProperty("enemy");
        var repair = enemyElement.GetProperty("repair");
        var definition = new ShipBattleDefinition(
            Str(root, "id"), root.GetProperty("seed").GetUInt64(), player, rooms, doors,
            new ShipCrewTuning(Array(crew, "start_rooms").Select(item => item.GetString() ?? "").ToArray(), Num(crew, "speed_meters_per_second"),
                Int(crew, "max_health"), Int(crew, "victory_restore_health"), Num(crew, "medic_heal_per_second"), Num(crew, "door_traversal_window_seconds"),
                Int(crew, "hit_damage_per_point")),
            new ShipWorkTuning(Num(work, "extinguish_seconds_per_severity"), Num(work, "seal_seconds_per_severity"), Num(work, "repair_seconds_per_bar"),
                Int(work, "second_worker_percent")),
            new ShipAtmosphereTuning(Int(air, "door_exchange_permille_per_tick"), Int(air, "vent_permille_per_tick"),
                Int(air, "breach_leak_permille_per_tick_per_severity"), Num(air, "life_support_percent_per_second_per_power"),
                Num(air, "crew_consumption_percent_per_second"), Num(air, "fire_consumption_percent_per_second_per_severity"),
                Num(air, "suffocation_threshold_percent"), Num(air, "suffocation_damage_per_second")),
            new ShipHazardTuning(Num(hazard, "fire_min_oxygen_percent"), Num(hazard, "fire_crew_damage_per_second_per_severity"),
                Num(hazard, "fire_system_damage_seconds"), Num(hazard, "fire_spread_seconds"), Int(hazard, "max_fire_severity"), Int(hazard, "max_breach_severity")),
            new ShipEnemyDefinition(ParseSide(enemyElement, enemy: true),
                new ShipRepairDefinition(Int(repair, "reserve_bars"), Num(repair, "seconds_per_bar"), Array(repair, "priority").Select(item => item.GetString() ?? "").ToArray())));
        definition.Validate();
        return definition;
    }

    public void Validate()
    {
        var roomIds = Rooms.Select(room => room.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var required in PlayerSystemIds.Append(Passage))
        { if (!roomIds.Contains(required)) { throw new InvalidDataException($"Ship battle needs room '{required}'."); } }
        if (roomIds.Count != Rooms.Count) { throw new InvalidDataException("Ship room IDs must be unique."); }
        if (Doors.Select(door => door.Id).Distinct(StringComparer.Ordinal).Count() != Doors.Count)
        { throw new InvalidDataException("Ship door IDs must be unique."); }
        foreach (var door in Doors)
        {
            if (!roomIds.Contains(door.RoomA)) { throw new InvalidDataException($"Door '{door.Id}' references unknown room."); }
            if (door.Exterior != (door.RoomB == Vacuum)) { throw new InvalidDataException($"Door '{door.Id}' exterior flag must match vacuum."); }
            if (!door.Exterior && !roomIds.Contains(door.RoomB)) { throw new InvalidDataException($"Door '{door.Id}' references unknown room."); }
        }
        foreach (var room in roomIds.Where(id => id != Passage))
        {
            if (!Doors.Any(door => !door.Exterior && (door.RoomA == room && door.RoomB == Passage || door.RoomB == room && door.RoomA == Passage)))
            { throw new InvalidDataException($"Room '{room}' must connect to the passage."); }
        }
        if (!Player.Systems.Select(system => system.Id).Order(StringComparer.Ordinal).SequenceEqual(PlayerSystemIds.Order(StringComparer.Ordinal)))
        { throw new InvalidDataException("Player ship must define exactly the four player systems."); }
        if (Player.Systems.Any(system => !roomIds.Contains(system.Room) || system.Room == Passage)
            || Player.Systems.Select(system => system.Room).Distinct(StringComparer.Ordinal).Count() != Player.Systems.Count)
        { throw new InvalidDataException("Each player system must occupy a distinct, known, non-passage room."); }
        if (!Enemy.Side.Systems.Select(system => system.Id).Order(StringComparer.Ordinal).SequenceEqual(EnemySystemIds.Order(StringComparer.Ordinal)))
        { throw new InvalidDataException("Enemy ship must define weapons, shields and engines."); }
        if (Player.Systems.Sum(system => system.InitialPower) > Player.Reactor
            || Player.Systems.Any(system => system.InitialPower < 0 || system.InitialPower > system.MaxPower))
        { throw new InvalidDataException("Initial player power exceeds the reactor or a system capacity."); }
        if (Player.Systems.Sum(system => system.MaxPower) <= Player.Reactor)
        { throw new InvalidDataException("The reactor must not be able to maximize every system."); }
        ValidateWeapons(Player, enemy: false);
        ValidateWeapons(Enemy.Side, enemy: true);
        foreach (var side in new[] { Player, Enemy.Side })
        {
            if (side.EvasionPercentPerEnginePower < 0 || side.EvasionMaxPercent is < 0 or > 95)
            { throw new InvalidDataException("Evasion must be non-negative and capped at 95 percent."); }
        }
        if (Enemy.Repair.Priority.Any(system => !EnemySystemIds.Contains(system)))
        { throw new InvalidDataException("Enemy repair priority must name enemy systems."); }
        if (Crew.StartRooms.Any(room => !roomIds.Contains(room))) { throw new InvalidDataException("Crew start room is unknown."); }
        if (Crew.HitDamagePerPoint < 0) { throw new InvalidDataException("Crew hit damage must be non-negative."); }
        // Bounded exchange: the busiest room may never give away more than half its oxygen in one tick.
        var maximumDegree = Rooms.Max(room => Doors.Count(door => door.RoomA == room.Id || door.RoomB == room.Id));
        if (Atmosphere.DoorExchangePermillePerTick * maximumDegree >= 500)
        { throw new InvalidDataException("Door exchange rate is unbounded for the room graph."); }
    }

    private static void ValidateWeapons(ShipSideDefinition side, bool enemy)
    {
        if (side.Weapons.Count == 0) { throw new InvalidDataException($"{side.DisplayName} needs at least one weapon."); }
        if (side.Weapons.Select(weapon => weapon.Id).Distinct(StringComparer.Ordinal).Count() != side.Weapons.Count)
        { throw new InvalidDataException($"{side.DisplayName} weapon IDs must be unique."); }
        var capacity = side.Systems.Single(system => system.Id == "weapons").MaxPower;
        foreach (var weapon in side.Weapons)
        {
            if (weapon.PowerCost < 1 || weapon.PowerCost > capacity || weapon.Shots < 1 || weapon.Damage < 1 || weapon.ChargeSeconds <= 0
                || weapon.TravelSeconds <= 0 || weapon.ShotSpacingSeconds < 0 || weapon.Ammo == 0
                || weapon.FireChancePercent is < 0 or > 100 || weapon.BreachChancePercent is < 0 or > 100)
            { throw new InvalidDataException($"Weapon '{weapon.Id}' has an invalid power, shot, damage, timing, ammo or chance value."); }
            if (enemy)
            {
                if (weapon.TargetSequence.Count == 0 || weapon.TargetSequence.Any(target => !PlayerSystemIds.Contains(target)))
                { throw new InvalidDataException($"Enemy weapon '{weapon.Id}' target sequence must name player systems."); }
                if (weapon.PayloadSequence.Count == 0) { throw new InvalidDataException($"Enemy weapon '{weapon.Id}' payload sequence is empty."); }
            }
            else if (weapon.TargetSequence.Count > 0 || weapon.PayloadSequence.Count > 0)
            { throw new InvalidDataException($"Player weapon '{weapon.Id}' is aimed by command, not by an authored sequence."); }
        }
    }

    private static ShipSideDefinition ParseSide(JsonElement side, bool enemy)
    {
        var systems = Array(side, "systems").Select(system => new ShipSystemDefinition(
            Str(system, "id"), enemy ? Str(system, "id") : Str(system, "room"), Int(system, "max_power"),
            Int(system, enemy ? "power" : "initial_power"), enemy ? Num(system, "x") : 0, enemy ? Num(system, "z") : 0,
            enemy ? Num(system, "width") : 0, enemy ? Num(system, "depth") : 0)).ToArray();
        var shield = side.GetProperty("shield");
        return new ShipSideDefinition(Str(side, "display_name"), Int(side, "hull"), enemy ? systems.Sum(system => system.InitialPower) : Int(side, "reactor"), systems,
            Array(side, "weapons").Select(ParseWeapon).ToArray(),
            new ShipShieldDefinition(Num(shield, "regen_seconds_per_layer"), Num(shield, "regen_delay_seconds")),
            Int(side, "evasion_percent_per_engine_power"), Int(side, "evasion_max_percent"),
            side.TryGetProperty("manning_bonus_percent", out var bonus) ? bonus.GetInt32() : 0);
    }

    private static ShipWeaponDefinition ParseWeapon(JsonElement weapon) => new(
        Str(weapon, "id"), Str(weapon, "display_name"), ParseKind(weapon.GetProperty("kind").GetString()), Int(weapon, "power"),
        Num(weapon, "charge_seconds"), Int(weapon, "shots"), Int(weapon, "damage"), Num(weapon, "travel_seconds"), Num(weapon, "shot_spacing_seconds"),
        weapon.GetProperty("pierces_shields").GetBoolean(), weapon.GetProperty("evadable").GetBoolean(), Int(weapon, "ammo"),
        Int(weapon, "fire_chance_percent"), Int(weapon, "breach_chance_percent"),
        weapon.TryGetProperty("target_sequence", out var targets) ? targets.EnumerateArray().Select(item => item.GetString() ?? "").ToArray() : [],
        weapon.TryGetProperty("payload_sequence", out var payloads) ? payloads.EnumerateArray().Select(item => ParsePayload(item.GetString())).ToArray() : []);

    private static ShipWeaponKind ParseKind(string? value) => value switch
    {
        "laser" => ShipWeaponKind.Laser,
        "missile" => ShipWeaponKind.Missile,
        _ => throw new InvalidDataException($"Unknown ship weapon kind '{value}'."),
    };

    private static ShipPayload ParsePayload(string? value) => value switch
    {
        "normal" => ShipPayload.Normal,
        "incendiary" => ShipPayload.Incendiary,
        "breach" => ShipPayload.Breach,
        _ => throw new InvalidDataException($"Unknown ship payload '{value}'."),
    };

    private static JsonElement.ArrayEnumerator Array(JsonElement element, string name) => element.GetProperty(name).EnumerateArray();

    private static string Str(JsonElement element, string name) =>
        element.GetProperty(name).GetString() is { Length: > 0 } value ? value : throw new InvalidDataException($"'{name}' must be a non-empty string.");

    private static int Int(JsonElement element, string name) => element.GetProperty(name).GetInt32();

    private static double Num(JsonElement element, string name) => element.GetProperty(name).GetDouble();
}
