namespace SpaceAdventure.Core;

public sealed record GameObservation(
    long Tick,
    bool Paused,
    long LatestEventSequence,
    StationRouteObservation? StationRoute = null);

public enum ScenarioPhase
{
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
}

public enum InteractionState
{
    Available,
    Unavailable,
    DialogueActive,
    Completed,
}

public sealed record PrimaryActionObservation(
    CommandId CommandId,
    PrimaryActionKind Kind,
    WorldPosition Destination,
    EntityId? InteractionTargetId);

public sealed record ActorObservation(
    EntityId Id,
    string DisplayName,
    WorldPosition Position,
    PrimaryActionObservation? CurrentAction,
    PrimaryActionObservation? PendingAction);

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
    string Speaker,
    string Line,
    DialogueResponseObservation Response);

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
    ObjectiveObservation Objective,
    IReadOnlyList<InteractionObservation> Interactions,
    DialogueObservation? ActiveDialogue);

public enum GameplayEventType
{
    SessionStarted,
    PauseChanged,
    CommandAccepted,
    CommandRejected,
    PrimaryActionAssigned,
    MovementArrived,
    PrimaryActionFailed,
    DialogueStarted,
    DialogueResponseChosen,
    InteractionCompleted,
    ObjectiveChanged,
    ScenarioCompleted,
}

public abstract record GameplayEventDetail;

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

public sealed record GameplayEvent(
    long Sequence,
    long Tick,
    GameplayEventType Type,
    CommandId? CommandId = null,
    bool? Paused = null,
    CommandRejectionCode? RejectionCode = null,
    GameplayEventDetail? Detail = null);
