namespace SpaceAdventure.Core;

public sealed record AttackDefinition(
    AttackId Id,
    double RangeMeters,
    int Damage,
    int WindupTicks,
    int RecoveryTicks);

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

public sealed record ItemDefinition(
    ItemId Id,
    int InitialCharges,
    int Healing,
    int WindupTicks,
    int RecoveryTicks);

public sealed record HostileDefinition(
    EntityId Id,
    string DisplayName,
    double MovementSpeedMetersPerSecond,
    int MaximumHealth,
    AttackId BasicAttackId);

public sealed record EncounterDefinition(
    EncounterId Id,
    EntityId HostileId,
    int ProtagonistMaximumHealth,
    int ReadyingTicks,
    int SecuringTicks,
    ItemId HealingItemId);

public sealed record StationCombatDefinition(
    IReadOnlyList<AttackDefinition> Attacks,
    AbilityDefinition ProtagonistAbility,
    ItemDefinition HealingItem,
    HostileDefinition Hostile,
    EncounterDefinition Encounter)
{
    public AttackDefinition GetAttack(AttackId id) =>
        Attacks.Single(attack => attack.Id == id);
}
