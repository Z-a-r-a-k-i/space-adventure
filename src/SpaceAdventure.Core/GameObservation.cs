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
    Item,
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
    Ally,
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
    AbilityTargetKind ActiveAbilityTargetKind);

public sealed record PartyMemberLoadoutObservation(
    string WeaponName,
    AttackId BasicAttackId,
    AbilityId ActiveAbilityId,
    string ActiveAbilityName,
    AbilityTargetKind ActiveAbilityTargetKind);

public sealed record PrimaryActionObservation(
    CommandId CommandId,
    PrimaryActionKind Kind,
    WorldPosition Destination,
    bool HasRemainingMovement,
    EntityId? InteractionTargetId,
    EntityId? CombatTargetId = null,
    AttackId? AttackId = null,
    AbilityId? AbilityId = null,
    ItemId? ItemId = null,
    PrimaryActionPhase Phase = PrimaryActionPhase.Moving,
    int PhaseTicksRemaining = 0,
    int PhaseTicksTotal = 0);

public sealed record CooldownObservation(AbilityId AbilityId, int RemainingTicks, int TotalTicks);

public sealed record ItemChargeObservation(ItemId ItemId, int Charges);

public sealed record CombatantStateObservation(
    int Health,
    int MaximumHealth,
    bool IsDefeated,
    AttackId BasicAttackId,
    IReadOnlyList<CooldownObservation> Cooldowns,
    IReadOnlyList<ItemChargeObservation> Items);

public sealed record ActorObservation(
    EntityId Id,
    string DisplayName,
    PartyMemberLoadoutObservation? Loadout,
    WorldPosition Position,
    PrimaryActionObservation? CurrentAction,
    PrimaryActionObservation? PendingAction,
    CombatantStateObservation? Combat = null);

public sealed record HostileObservation(
    EntityId Id,
    string DisplayName,
    WorldPosition Position,
    double MovementSpeedMetersPerSecond,
    CombatantStateObservation Combat,
    PrimaryActionObservation? CurrentAction);

public sealed record EncounterObservation(
    EncounterId Id,
    EncounterPhase Phase,
    int Attempt,
    int TransitionTicksRemaining,
    int TransitionTicksTotal,
    EntityId HostileId);

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
    EncounterObservation? Encounter = null);

public enum GameplayEventType
{
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

public sealed record EncounterEventDetail(
    EncounterId EncounterId,
    int Attempt) : GameplayEventDetail;

public sealed record AttackEventDetail(
    EntityId SourceId,
    EntityId TargetId,
    AttackId AttackId,
    bool Hit) : GameplayEventDetail;

public sealed record AbilityReleasedEventDetail(
    EntityId SourceId,
    WorldPosition TargetPosition,
    AbilityId AbilityId,
    bool Hit) : GameplayEventDetail;

public sealed record DamageAppliedEventDetail(
    EntityId SourceId,
    EntityId TargetId,
    int Amount,
    int RemainingHealth,
    AttackId? AttackId,
    AbilityId? AbilityId) : GameplayEventDetail;

public sealed record HealingAppliedEventDetail(
    EntityId SourceId,
    EntityId TargetId,
    ItemId ItemId,
    int Amount,
    int RemainingHealth) : GameplayEventDetail;

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
