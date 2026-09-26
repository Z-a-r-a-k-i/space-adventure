namespace SpaceAdventure.Core;

public sealed record GameObservation(
    long Tick,
    bool Paused,
    long LatestEventSequence,
    StationRouteObservation? StationRoute = null);

public enum ScenarioPhase
{
    AwaitingProtagonistSelection,
    InProgress,
    Completed,
}

public enum ObjectiveStatus
{
    Active,
    Completed,
}

public enum PrimaryActionKind
{
    Move,
    Interact,
    Attack,
    Ability,
    Stop,
}

public enum ActionWaitingReason
{
    TacticalPause,
    EncounterReadying,
    OffensiveRecovery,
}

public enum PrimaryActionPhase
{
    Moving,
    Windup,
    Recovery,
}

public enum EncounterPhase
{
    Dormant,
    Readying,
    Active,
    Securing,
    Victory,
    Defeat,
}

public enum InteractionState
{
    Available,
    Unavailable,
    DialogueActive,
    Completed,
}

public enum AbilityTargetKind
{
    Position,
    Entity,
    Barrier,
    Self,
}

public enum RoutePowerMode
{
    Unset,
    ServiceRerouted,
    ShelterPreserved,
}

public sealed record ProtagonistKitObservation(
    ProtagonistKitId Id,
    string DisplayName,
    string Role,
    string WeaponName,
    AttackId BasicAttackId,
    AbilityId ActiveAbilityId,
    string ActiveAbilityName,
    AbilityTargetKind ActiveAbilityTargetKind,
    AbilityId SecondaryAbilityId,
    string SecondaryAbilityName,
    AbilityTargetKind SecondaryAbilityTargetKind);

public sealed record PartyMemberLoadoutObservation(
    string WeaponName,
    AttackId BasicAttackId,
    AbilityId ActiveAbilityId,
    string ActiveAbilityName,
    AbilityTargetKind ActiveAbilityTargetKind,
    AbilityId SecondaryAbilityId,
    string SecondaryAbilityName,
    AbilityTargetKind SecondaryAbilityTargetKind);

public sealed record PrimaryActionObservation(
    CommandId CommandId,
    PrimaryActionKind Kind,
    WorldPosition Destination,
    bool HasRemainingMovement,
    EntityId? InteractionTargetId,
    EntityId? CombatTargetId = null,
    AttackId? AttackId = null,
    AbilityId? AbilityId = null,
    PrimaryActionPhase Phase = PrimaryActionPhase.Moving,
    int PhaseTicksRemaining = 0,
    int PhaseTicksTotal = 0,
    long InstanceId = 0,
    long PhaseStartedTick = 0,
    ActionWaitingReason? WaitingReason = null,
    bool Interrupted = false,
    WorldPosition? AbilityFacing = null);

public sealed record CooldownObservation(AbilityId AbilityId, int RemainingTicks, int TotalTicks);

public sealed record CombatantStateObservation(
    int Health,
    int MaximumHealth,
    bool IsDefeated,
    AttackId BasicAttackId,
    IReadOnlyList<CooldownObservation> Cooldowns,
    EntityId? RememberedAttackTargetId = null,
    long OffensiveRecoveryUntilTick = 0,
    long? DefeatedAtTick = null,
    EntityId? TauntedBy = null,
    int TauntRemainingTicks = 0);

public sealed record ActorObservation(
    EntityId Id,
    string DisplayName,
    PartyMemberLoadoutObservation? Loadout,
    WorldPosition Position,
    WorldPosition Facing,
    PrimaryActionObservation? CurrentAction,
    PrimaryActionObservation? PendingAction,
    CombatantStateObservation? Combat = null);

public sealed record HostileObservation(
    EntityId Id,
    string DisplayName,
    WorldPosition Position,
    double MovementSpeedMetersPerSecond,
    CombatantStateObservation Combat,
    PrimaryActionObservation? CurrentAction,
    EncounterId EncounterId = default,
    EncounterPhase EncounterPhase = EncounterPhase.Dormant,
    int EncounterAttempt = 0,
    WorldPosition Facing = default);

public sealed record EncounterObservation(
    EncounterId Id,
    EncounterPhase Phase,
    int Attempt,
    int TransitionTicksRemaining,
    int TransitionTicksTotal,
    IReadOnlyList<EntityId> HostileIds,
    long PhaseStartedTick = 0,
    BarrierObservation? Barrier = null,
    IReadOnlyList<ProjectileObservation>? Projectiles = null,
    HealingFieldObservation? HealingField = null,
    EntityId? SpotterId = null,
    EntityId? SpottedActorId = null);

public sealed record HealingFieldObservation(EntityId SourceId, WorldPosition Position, double RadiusMeters,
    long DeployedAtTick, int RemainingTicks, int TotalTicks, int PulseIntervalTicks);

public sealed record HealingAppliedEventDetail(EntityId SourceId, EntityId TargetId, AbilityId AbilityId,
    int Amount, int RemainingHealth) : GameplayEventDetail;

public sealed record HealingFieldEventDetail(EntityId SourceId, AbilityId AbilityId, WorldPosition Position,
    double RadiusMeters, int DurationTicks) : GameplayEventDetail;

public sealed record BarrierObservation(EntityId SourceId, WorldPosition Position, WorldPosition Facing,
    int RemainingTicks, int TotalTicks, double WidthMeters, double HeightMeters, long DeployedAtTick);

public enum BarrierEndReason { Expired, EncounterEnded, Replaced }

public sealed record BarrierEventDetail(EntityId SourceId, WorldPosition Position, WorldPosition Facing,
    AbilityId AbilityId, BarrierEndReason? EndReason = null) : GameplayEventDetail;

public sealed record TauntEventDetail(EntityId SourceId, EntityId TargetId, int DurationTicks) : GameplayEventDetail;

public sealed record ProjectileObservation(long Id, EntityId SourceId, EntityId TargetId, AttackId AttackId,
    WorldPosition Origin, WorldPosition Destination, WorldPosition Position, long ReleasedAtTick, int FlightTicks);

public sealed record ProjectileEventDetail(long Id, EntityId SourceId, EntityId TargetId, AttackId AttackId,
    WorldPosition Origin, WorldPosition Destination, int FlightTicks, WorldPosition? ImpactPosition = null,
    bool Blocked = false) : GameplayEventDetail;

public sealed record InteractionObservation(
    EntityId Id,
    StationInteractionKind Kind,
    string Prompt,
    WorldPosition Position,
    WorldPosition ApproachPosition,
    double UseRadiusMeters,
    InteractionState State,
    bool CanInteract,
    string? ResultText);

public sealed record DialogueResponseObservation(DialogueResponseId Id, string Text);

public sealed record DialogueObservation(
    EntityId InteractionId,
    EntityId ActorId,
    string Speaker,
    string Line,
    IReadOnlyList<DialogueResponseObservation> Responses);

public sealed record ObjectiveObservation(
    ObjectiveId Id,
    string Text,
    ObjectiveStatus Status);

public sealed record StationRouteObservation(
    ScenarioId ScenarioId,
    int ContentSchemaVersion,
    string ContentRevision,
    ScenarioPhase Phase,
    ActorObservation Protagonist,
    IReadOnlyList<ActorObservation> Party,
    IReadOnlyList<ProtagonistKitObservation> AvailableProtagonistKits,
    ProtagonistKitObservation? SelectedProtagonistKit,
    RoutePowerMode RoutePowerMode,
    ObjectiveObservation Objective,
    IReadOnlyList<InteractionObservation> Interactions,
    DialogueObservation? ActiveDialogue,
    IReadOnlyList<HostileObservation>? Hostiles = null,
    EncounterObservation? Encounter = null,
    IReadOnlyList<EncounterId>? CompletedEncounterIds = null)
{
    public IReadOnlyList<HostileObservation> VisibleHostiles { get; init; } = [];
}

public enum GameplayEventType
{
    TauntApplied,
    BarrierDeployed,
    BarrierEnded,
    ProjectileLaunched,
    ProjectileBlocked,
    ProjectileImpacted,
    SessionStarted,
    PauseChanged,
    CommandAccepted,
    CommandRejected,
    ProtagonistKitSelected,
    PrimaryActionAssigned,
    MovementArrived,
    PrimaryActionFailed,
    DialogueStarted,
    DialogueResponseChosen,
    RouteConsequenceSelected,
    PartyMemberRecruited,
    InteractionCompleted,
    ObjectiveChanged,
    ScenarioCompleted,
    EncounterStarted,
    EncounterRestarted,
    EncounterWon,
    EncounterDefeated,
    AttackWindupStarted,
    AttackReleased,
    AbilityReleased,
    DamageApplied,
    HealingApplied,
    HealingFieldDeployed,
    ActionInterrupted,
    CombatantDefeated,
}

public abstract record GameplayEventDetail;

public sealed record ProtagonistKitSelectedEventDetail(
    CommandId CommandId,
    ProtagonistKitId KitId) : GameplayEventDetail;

public sealed record PrimaryActionAssignedEventDetail(
    CommandId CommandId,
    EntityId ActorId,
    PrimaryActionKind Kind,
    WorldPosition Destination,
    EntityId? InteractionTargetId,
    bool Pending,
    CommandId? ReplacedCommandId) : GameplayEventDetail;

public sealed record MovementArrivedEventDetail(
    CommandId CommandId,
    EntityId ActorId,
    WorldPosition Position) : GameplayEventDetail;

public sealed record PrimaryActionFailedEventDetail(
    CommandId CommandId,
    EntityId ActorId,
    CommandRejectionCode Reason) : GameplayEventDetail;

public sealed record DialogueStartedEventDetail(
    CommandId CommandId,
    EntityId ActorId,
    EntityId InteractionId) : GameplayEventDetail;

public sealed record DialogueResponseChosenEventDetail(
    CommandId CommandId,
    EntityId ActorId,
    EntityId InteractionId,
    DialogueResponseId ResponseId) : GameplayEventDetail;

public sealed record RouteConsequenceSelectedEventDetail(
    CommandId CommandId,
    RoutePowerMode RoutePowerMode) : GameplayEventDetail;

public sealed record PartyMemberRecruitedEventDetail(
    CommandId CommandId,
    EntityId ActorId) : GameplayEventDetail;

public sealed record InteractionCompletedEventDetail(
    CommandId CommandId,
    EntityId ActorId,
    EntityId InteractionId,
    StationInteractionEffect Effect) : GameplayEventDetail;

public sealed record ObjectiveChangedEventDetail(
    CommandId CommandId,
    ObjectiveId PreviousObjectiveId,
    ObjectiveId CurrentObjectiveId,
    ObjectiveStatus Status) : GameplayEventDetail;

public sealed record ScenarioCompletedEventDetail(
    CommandId CommandId,
    ScenarioId ScenarioId) : GameplayEventDetail;

/// <summary>Encounter lifecycle event; a start also names the hostile that noticed the crew and whom it saw.</summary>
public sealed record EncounterEventDetail(
    EncounterId EncounterId,
    int Attempt,
    EntityId? SpotterId = null,
    EntityId? SpottedActorId = null) : GameplayEventDetail;

public sealed record AttackEventDetail(
    EntityId SourceId,
    EntityId TargetId,
    AttackId AttackId,
    bool Hit) : GameplayEventDetail;

public sealed record AbilityReleasedEventDetail(
    EntityId SourceId,
    WorldPosition TargetPosition,
    AbilityId AbilityId,
    bool Hit,
    EntityId? TargetId = null) : GameplayEventDetail;

public sealed record DamageAppliedEventDetail(
    EntityId SourceId,
    EntityId TargetId,
    int Amount,
    int RemainingHealth,
    AttackId? AttackId,
    AbilityId? AbilityId) : GameplayEventDetail;

public sealed record ActionInterruptedEventDetail(
    EntityId ActorId,
    EntityId SourceId,
    AbilityId AbilityId) : GameplayEventDetail;

public sealed record CombatantDefeatedEventDetail(
    EntityId CombatantId,
    EntityId SourceId) : GameplayEventDetail;

public sealed record GameplayEvent(
    long Sequence,
    long Tick,
    GameplayEventType Type,
    CommandId? CommandId = null,
    bool? Paused = null,
    CommandRejectionCode? RejectionCode = null,
    GameplayEventDetail? Detail = null);
