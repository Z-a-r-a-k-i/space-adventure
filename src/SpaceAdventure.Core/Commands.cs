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

public sealed record MoveActorCommand(
    CommandId CommandId,
    EntityId ActorId,
    WorldPosition Destination) : IGameCommand;

public sealed record InteractCommand(
    CommandId CommandId,
    EntityId ActorId,
    EntityId TargetId) : IGameCommand;

public sealed record ChooseDialogueResponseCommand(
    CommandId CommandId,
    EntityId ActorId,
    EntityId InteractionId,
    DialogueResponseId ResponseId) : IGameCommand;

public enum CommandRejectionCode
{
    UnknownCommand,
    ScenarioCompleted,
    UnknownActor,
    ActorNotControllable,
    InvalidDestination,
    DestinationUnreachable,
    UnknownInteraction,
    InteractionUnavailable,
    DialogueActive,
    NoActiveDialogue,
    DialogueMismatch,
    UnknownDialogueResponse,
}

public sealed record CommandAcknowledgement(
    CommandId CommandId,
    bool Accepted,
    CommandRejectionCode? RejectionCode,
    GameObservation Observation);
