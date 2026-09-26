using SpaceAdventure.Core;

namespace SpaceAdventure.SimCli;

public enum ShipBattleStrategy
{
    SuppressWeapons,
    OverwhelmDefenses,
    Passive,
}

/// <summary>
/// Scripted player used by CLI scenarios and tests. It issues only ordinary typed commands and
/// reacts once per second, like a competent unpaused player without pause planning.
/// </summary>
public sealed class ShipBattlePilot(ShipCombatSession session, ShipBattleStrategy strategy)
{
    /// <summary>
    /// Unpaused length band for the scripted winning pilots (competent play without pause planning). The first
    /// battle is deliberately short and forgiving; a new player's slower orders stretch it well past the minimum.
    /// </summary>
    public const int MinimumWinSeconds = 35;
    public const int MaximumWinSeconds = 150;

    private int _commands;

    public static IReadOnlyList<ShipCrewSeed> StationCrew { get; } =
    [
        new("actor.protagonist", "Vanguard", false),
        new("actor.companion.protector", "Protector", false),
        new("actor.companion.medic", "Medic", true),
    ];

    public static ShipBattleDefinition LoadDefinition(string baseDirectory) =>
        ShipBattleDefinition.ParseJson(File.ReadAllText(Path.Combine(baseDirectory, "content", "ship-battle.json")));

    private static Dictionary<string, int> Power(int weapons, int shields, int lifeSupport, int engines) =>
        new() { ["weapons"] = weapons, ["shields"] = shields, ["life_support"] = lifeSupport, ["engines"] = engines };

    public ShipCommandResult Send(IShipCommand command) => session.Execute(command);

    public CommandId NextId() => new($"pilot.{++_commands}");

    private IEnumerable<ShipWeaponDefinition> Weapons => session.Definition.Player.Weapons;

    private void Aim(string system)
    {
        foreach (var weapon in Weapons) { Send(new ShipSetWeaponTargetCommand(NextId(), weapon.Id, system)); }
    }

    public void Open()
    {
        if (strategy == ShipBattleStrategy.Passive)
        {
            Send(new ShipSetHoldFireCommand(NextId(), true));
        }
        else
        {
            // Suppress aims every weapon at the enemy guns; overwhelm synchronizes volleys into its shields
            // and trades engine evasion for a third shield layer.
            Aim(strategy == ShipBattleStrategy.SuppressWeapons ? "weapons" : "shields");
            if (strategy == ShipBattleStrategy.OverwhelmDefenses) { Send(new ShipSetPowerCommand(NextId(), Power(3, 3, 1, 1))); }
        }
        Send(new ShipSetPauseCommand(NextId(), false));
    }

    /// <summary>Send one idle crew to each unattended hazard or damaged system.</summary>
    public void React()
    {
        if (strategy == ShipBattleStrategy.Passive) { return; }
        var observation = session.Observe();
        // Suppress first; once enemy weapons are down with no repair reserve left, finish through its shields.
        if (strategy == ShipBattleStrategy.SuppressWeapons && observation.Player.Weapons.Any(weapon => weapon.Target == "weapons")
            && observation.EnemyRepair.ReserveBars == 0 && observation.Enemy.Systems.Single(system => system.Id == "weapons").EffectivePower == 0)
        {
            Aim("shields");
        }
        if (strategy == ShipBattleStrategy.OverwhelmDefenses)
        {
            // Hold until every loaded weapon is charged, then release one synchronized volley.
            var hold = observation.Player.Weapons.Any(weapon => weapon.Powered && weapon.Ammo != 0 && weapon.ChargePermille < 1000);
            if (hold != observation.Player.HoldFire) { Send(new ShipSetHoldFireCommand(NextId(), hold)); }
        }
        if (observation.Rooms.Any(room => room.OxygenPermille < 450)
            && observation.Player.Systems.Single(system => system.Id == "life_support").AllocatedPower < 2)
        {
            Send(new ShipSetPowerCommand(NextId(), Power(3, 2, 2, 1)));
        }
        // A repair or manning order in a room that starts burning or leaking goes back to Auto (hazards first).
        foreach (var crew in observation.Crew.Where(crew => !crew.Downed && crew.ExplicitTask is ShipTaskKind.Repair or ShipTaskKind.Man && !crew.Moving))
        {
            var room = observation.Rooms.Single(item => item.Id == crew.Room);
            if (room.FireSeverity + room.BreachSeverity > 0) { Send(new ShipSetCrewModeCommand(NextId(), crew.Id, ShipCrewMode.Auto)); }
        }
        observation = session.Observe();
        var busy = observation.Crew.Where(crew => crew.Downed || crew.ExplicitTask != ShipTaskKind.None).Select(crew => crew.Id).ToHashSet();
        foreach (var room in observation.Rooms.OrderByDescending(room => room.FireSeverity + room.BreachSeverity))
        {
            var system = observation.Player.Systems.FirstOrDefault(item => item.Room == room.Id);
            var task = room.FireSeverity > 0 ? ShipTaskKind.Extinguish : room.BreachSeverity > 0 ? ShipTaskKind.Seal
                : system is { Damage: > 0 } ? ShipTaskKind.Repair : ShipTaskKind.None;
            if (task == ShipTaskKind.None) { continue; }
            if (observation.Crew.Any(crew => !crew.Downed && (crew.Room == room.Id && !crew.Moving || crew.TaskRoom == room.Id))) { continue; }
            var helper = observation.Crew.Where(crew => !busy.Contains(crew.Id) && !crew.Moving)
                .OrderBy(crew => crew.Room == "engines" ? 0 : 1).FirstOrDefault();
            if (helper is null) { return; }
            busy.Add(helper.Id);
            Send(new ShipAssignTaskCommand(NextId(), helper.Id, task, room.Id));
        }
        var hurt = observation.Crew.Where(crew => !crew.Downed && crew.Health < crew.MaxHealth / 2).OrderBy(crew => crew.Health).FirstOrDefault();
        var medic = observation.Crew.FirstOrDefault(crew => crew.IsMedic && !crew.Downed && crew.ExplicitTask == ShipTaskKind.None);
        if (hurt is not null && medic is not null && !hurt.InDanger)
        {
            Send(new ShipAssignTaskCommand(NextId(), medic.Id, ShipTaskKind.Treat, hurt.Room, hurt.Id));
        }
    }

    public ShipBattlePhase RunToEnd(int maximumTicks)
    {
        Open();
        while (session.Phase == ShipBattlePhase.Active && session.Tick < maximumTicks)
        {
            session.AdvanceTicks(ShipCombatSession.TicksPerSecond);
            React();
        }
        return session.Phase;
    }
}
