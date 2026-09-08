namespace SpaceAdventure.Core;

public sealed record AttackDefinition(
    AttackId Id,
    double RangeMeters,
    int Damage,
    int WindupTicks,
    int RecoveryTicks,
    double ProjectileSpeedMetersPerSecond = 0);

public sealed record AbilityDefinition(
    AbilityId Id,
    AbilityTargetKind TargetKind,
    double RangeMeters,
    double RadiusMeters,
    int Damage,
    int WindupTicks,
    int RecoveryTicks,
    int CooldownTicks,
    bool InterruptsWindup);

public sealed record HostileDefinition(
    EntityId Id,
    string DisplayName,
    double MovementSpeedMetersPerSecond,
    int MaximumHealth,
    AttackId BasicAttackId,
    HostileBehavior Behavior);

public enum HostileBehavior { Melee, Sentry }

public sealed record BarrierDefinition(
    AbilityId Id,
    double RangeMeters,
    int WindupTicks,
    int RecoveryTicks,
    int DurationTicks,
    int CooldownTicks,
    double WidthMeters,
    double HeightMeters,
    double CenterHeightMeters)
{
    public WorldPosition CenterAt(WorldPosition groundPosition) => new(
        groundPosition.X, groundPosition.Y + CenterHeightMeters, groundPosition.Z);
}

public sealed record BurstDefinition(AbilityId Id, double RangeMeters, int DamagePerShot,
    int ShotCount, int ShotIntervalTicks, int WindupTicks, int RecoveryTicks, int CooldownTicks);

public sealed record TauntDefinition(AbilityId Id, double RadiusMeters, int DurationTicks,
    int WindupTicks, int RecoveryTicks, int CooldownTicks);

public sealed record EncounterDefinition(
    EncounterId Id,
    IReadOnlyList<EntityId> HostileIds,
    int ProtagonistMaximumHealth,
    int ReadyingTicks,
    int SecuringTicks,
    bool RequiresCompanion);

public sealed record StationCombatDefinition(
    IReadOnlyList<AttackDefinition> Attacks,
    AbilityDefinition ProtagonistAbility,
    IReadOnlyList<HostileDefinition> Hostiles,
    IReadOnlyList<EncounterDefinition> Encounters,
    BarrierDefinition Barrier,
    int CompanionMaximumHealth,
    BurstDefinition Burst,
    TauntDefinition Taunt)
{
    public EncounterDefinition SoloEncounter => Encounters.Single(encounter => !encounter.RequiresCompanion);
    public EncounterDefinition PartyEncounter => Encounters.Single(encounter => encounter.RequiresCompanion);
    public HostileDefinition SoloHostile => GetHostile(SoloEncounter.HostileIds.Single());

    public int AbilityCooldownTicks(AbilityId id) => id == ProtagonistAbility.Id ? ProtagonistAbility.CooldownTicks
        : id == Barrier.Id ? Barrier.CooldownTicks : id == Burst.Id ? Burst.CooldownTicks
        : id == Taunt.Id ? Taunt.CooldownTicks
        : throw new ArgumentOutOfRangeException(nameof(id), $"Unknown ability '{id.Value}'.");

    public AttackDefinition GetAttack(AttackId id) =>
        Attacks.Single(attack => attack.Id == id);

    public HostileDefinition GetHostile(EntityId id) => Hostiles.Single(hostile => hostile.Id == id);
}
