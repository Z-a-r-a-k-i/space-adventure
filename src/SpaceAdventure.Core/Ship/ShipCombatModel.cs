namespace SpaceAdventure.Core;

public enum ShipBattlePhase
{
    Active,
    Victory,
    Defeat,
}

public enum ShipCrewMode
{
    Auto,
    Hold,
}

public enum ShipTaskKind
{
    None,
    Extinguish,
    Seal,
    Repair,
    Man,
    Treat,
}

public enum ShipRejection
{
    None,
    BattleEnded,
    UnknownCrew,
    UnknownRoom,
    UnknownDoor,
    UnknownSystem,
    UnknownWeapon,
    CrewDowned,
    NotMedic,
    InvalidTreatTarget,
    RoomHasNoSystem,
    PowerVectorIncomplete,
    PowerExceedsCapacity,
    PowerExceedsReactor,
    TaskHasNoWork,
}

public sealed record ShipCrewSeed(string Id, string DisplayName, bool IsMedic);

public interface IShipCommand
{
    CommandId CommandId { get; }
}

public sealed record ShipSetPauseCommand(CommandId CommandId, bool Paused) : IShipCommand;

/// <summary>Battle-only retry. Never repeats the station.</summary>
public sealed record ShipRestartCommand(CommandId CommandId) : IShipCommand;

public sealed record ShipMoveCrewCommand(CommandId CommandId, string CrewId, string RoomId) : IShipCommand;

public sealed record ShipAssignTaskCommand(CommandId CommandId, string CrewId, ShipTaskKind Task, string RoomId, string? TargetCrewId = null) : IShipCommand;

/// <summary>Auto cancels an explicit task and resumes current-room automation; Hold cancels it and suppresses all automatic work.</summary>
public sealed record ShipSetCrewModeCommand(CommandId CommandId, string CrewId, ShipCrewMode Mode) : IShipCommand;

/// <summary>Atomic whole-ship power vector; every player system must be present.</summary>
public sealed record ShipSetPowerCommand(CommandId CommandId, IReadOnlyDictionary<string, int> Allocation) : IShipCommand;

/// <summary>Arms or disarms one weapon. Armed weapons draw weapons-system power in mounting order while it lasts.</summary>
public sealed record ShipSetWeaponPowerCommand(CommandId CommandId, string WeaponId, bool Armed) : IShipCommand;

/// <summary>Aims one weapon at an enemy system; null clears the target. Targets persist between volleys.</summary>
public sealed record ShipSetWeaponTargetCommand(CommandId CommandId, string WeaponId, string? TargetSystemId) : IShipCommand;

/// <summary>Holding keeps charged weapons from firing so a synchronized volley can be released together.</summary>
public sealed record ShipSetHoldFireCommand(CommandId CommandId, bool Hold) : IShipCommand;

public sealed record ShipSetDoorCommand(CommandId CommandId, string DoorId, bool Open) : IShipCommand;

public sealed record ShipCommandResult(CommandId CommandId, bool Accepted, ShipRejection Rejection, string? Warning);

public sealed record ShipEvent(long Sequence, int Attempt, long Tick, string Kind, string Subject, string Detail);

public sealed record ShipSystemObservation(
    string Id, string Room, int MaxPower, int AllocatedPower, int Damage, int EffectivePower, bool Manned, int RepairProgressPermille);

public sealed record ShipWeaponObservation(
    string Id, string DisplayName, ShipWeaponKind Kind, int PowerCost, bool Armed, bool Powered, int ChargePermille, string? Target,
    int Shots, int Damage, bool PiercesShields, bool Evadable, int Ammo, int TicksToFire);

public sealed record ShipRoomObservation(
    string Id, int OxygenPermille, int FireSeverity, int BreachSeverity, int FireWorkPermille, int SealWorkPermille);

public sealed record ShipDoorObservation(string Id, string RoomA, string RoomB, bool Exterior, bool CommandedOpen, bool EffectivelyOpen, int TraversalTicksRemaining);

public sealed record ShipCrewObservation(
    string Id, string DisplayName, bool IsMedic, string Room, double X, double Z, int Health, int MaxHealth, bool Downed, bool Moving,
    ShipCrewMode Mode, ShipTaskKind ExplicitTask, string? TaskRoom, string? TreatTarget, ShipTaskKind CurrentWork, string? Destination, bool InDanger,
    double PreviousX, double PreviousZ, long WorkSinceTick);

/// <summary>A projectile in flight. It leaves the source at <c>LaunchTick</c> and resolves at <c>ImpactTick</c>.</summary>
public sealed record ShipShotObservation(
    long Id, string Source, string WeaponId, ShipWeaponKind Kind, string TargetSystem, ShipPayload Payload, int Damage, long LaunchTick, long ImpactTick);

public sealed record ShipSideObservation(
    string DisplayName, int Hull, int MaxHull, int Reactor, int ShieldLayers, int ShieldCapacity, int ShieldRechargePermille, int EvasionPercent,
    IReadOnlyList<ShipSystemObservation> Systems, IReadOnlyList<ShipWeaponObservation> Weapons, bool HoldFire);

/// <summary>Telegraphed next volley of one enemy weapon; <c>TicksToFire</c> is -1 while it cannot fire.</summary>
public sealed record ShipEnemyIntent(
    string WeaponId, string DisplayName, ShipWeaponKind Kind, string TargetSystem, ShipPayload Payload, int TicksToFire, int Shots, int Damage, bool Powered, int Ammo);

public sealed record ShipEnemyRepairObservation(string? System, int ProgressPermille, int ReserveBars);

public sealed record ShipBattleObservation(
    int Attempt, ShipBattlePhase Phase, bool Paused, long Tick, ShipSideObservation Player, ShipSideObservation Enemy,
    IReadOnlyList<ShipEnemyIntent> Intents, ShipEnemyRepairObservation EnemyRepair, IReadOnlyList<ShipRoomObservation> Rooms,
    IReadOnlyList<ShipDoorObservation> Doors, IReadOnlyList<ShipCrewObservation> Crew, IReadOnlyList<ShipShotObservation> Shots);
