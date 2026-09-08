namespace SpaceAdventure.Core;

public readonly record struct CommandId
{
    public CommandId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public interface IGameCommand
{
    CommandId CommandId { get; }
}

public sealed record SetPauseCommand(CommandId CommandId, bool Paused) : IGameCommand;

public sealed record ChooseProtagonistKitCommand(
    CommandId CommandId,
    ProtagonistKitId KitId) : IGameCommand;

public sealed record MoveActorCommand(
    CommandId CommandId,
    EntityId ActorId,
    WorldPosition Destination) : IGameCommand;

public sealed record StopActorsCommand : IGameCommand
{
    public StopActorsCommand(CommandId commandId, IEnumerable<EntityId> actorIds)
    {
        ArgumentNullException.ThrowIfNull(actorIds);
        CommandId = commandId;
        ActorIds = actorIds.ToArray();
    }

    public CommandId CommandId { get; }

    public IReadOnlyList<EntityId> ActorIds { get; }
}

public sealed record MovePartyCommand : IGameCommand
{
    public MovePartyCommand(
        CommandId commandId,
        IEnumerable<EntityId> actorIds,
        WorldPosition destination)
    {
        ArgumentNullException.ThrowIfNull(actorIds);
        CommandId = commandId;
        ActorIds = actorIds.ToArray();
        Destination = destination;
    }

    public CommandId CommandId { get; }

    public IReadOnlyList<EntityId> ActorIds { get; }

    public WorldPosition Destination { get; }
}

public sealed record InteractCommand(
    CommandId CommandId,
    EntityId ActorId,
    EntityId TargetId) : IGameCommand;

public sealed record FaceActorsCommand : IGameCommand
{
    public FaceActorsCommand(CommandId commandId, IEnumerable<EntityId> actorIds, WorldPosition facing)
    {
        ArgumentNullException.ThrowIfNull(actorIds);
        CommandId = commandId;
        ActorIds = actorIds.ToArray();
        Facing = facing;
    }

    public CommandId CommandId { get; }
    public IReadOnlyList<EntityId> ActorIds { get; }
    public WorldPosition Facing { get; }
}

public sealed record ChooseDialogueResponseCommand(
    CommandId CommandId,
    EntityId ActorId,
    EntityId InteractionId,
    DialogueResponseId ResponseId) : IGameCommand;

public abstract record AbilityTarget(AbilityTargetKind Kind);

public sealed record PositionAbilityTarget(WorldPosition Position)
    : AbilityTarget(AbilityTargetKind.Position);

public sealed record EntityAbilityTarget(EntityId EntityId)
    : AbilityTarget(AbilityTargetKind.Entity);

public sealed record SelfAbilityTarget() : AbilityTarget(AbilityTargetKind.Self);

public sealed record BarrierAbilityTarget(WorldPosition Position, WorldPosition Facing)
    : AbilityTarget(AbilityTargetKind.Barrier);

public sealed record AssignBasicAttackTargetCommand(
    CommandId CommandId,
    EntityId ActorId,
    EntityId TargetId) : IGameCommand;

public sealed record UseAbilityCommand(
    CommandId CommandId,
    EntityId ActorId,
    AbilityId AbilityId,
    AbilityTarget Target) : IGameCommand;

public sealed record RestartEncounterCommand(
    CommandId CommandId,
    EncounterId EncounterId) : IGameCommand;

public enum CommandRejectionCode
{
    UnknownCommand,
    ScenarioCompleted,
    ProtagonistKitRequired,
    UnknownProtagonistKit,
    ProtagonistKitAlreadySelected,
    UnknownActor,
    ActorNotControllable,
    EmptyPartySelection,
    DuplicateActor,
    InvalidDestination,
    InvalidFacing,
    DestinationUnreachable,
    UnknownInteraction,
    InteractionUnavailable,
    DialogueActive,
    NoActiveDialogue,
    DialogueMismatch,
    UnknownDialogueResponse,
    CombatInactive,
    EncounterTransitionActive,
    UnknownEncounter,
    InvalidEncounterState,
    UnknownCombatTarget,
    CombatantDefeated,
    UnknownAttack,
    UnknownAbility,
    AbilityTargetKindMismatch,
    AbilityTargetOutOfRange,
    AbilityOnCooldown,
    InvalidAbilityTarget,
}

public sealed record CommandAcknowledgement(
    CommandId CommandId,
    bool Accepted,
    CommandRejectionCode? RejectionCode,
    GameObservation Observation);
