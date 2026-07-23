namespace SpaceAdventure.Core;

public sealed class GameSession
{
    public const int TicksPerSecond = 30;
    public const int MaximumRetainedEvents = 1024;
    public const int MaximumDirectTickAdvance = 3000;

    private const double SecondsPerTick = 1.0 / TicksPerSecond;
    private const double MaximumFrameSeconds = 0.25;
    private const int MaximumFrameTicks = 8;
    private const int MaximumPathWaypoints = 512;
    private const double PositionToleranceMeters = 0.0001;
    private const double MoveEndpointToleranceMeters = 0.25;

    private readonly List<GameplayEvent> _events = [];
    private readonly ISpatialPathfinder? _pathfinder;
    private readonly StationRouteRuntime? _stationRoute;
    private double _accumulatedSeconds;
    private long _eventSequence;

    public GameSession()
    {
        Record(GameplayEventType.SessionStarted);
    }

    private GameSession(
        StationRouteDefinition definition,
        StationRouteLayout layout,
        ISpatialPathfinder pathfinder)
    {
        ValidateLayout(definition, layout);
        _pathfinder = pathfinder;
        _stationRoute = new StationRouteRuntime(definition, layout);
        Record(GameplayEventType.SessionStarted);
    }

    public long Tick { get; private set; }

    public bool IsPaused { get; private set; }

    public long OldestRetainedEventSequence => _events.Count == 0
        ? _eventSequence + 1
        : _events[0].Sequence;

    public static GameSession CreateStationRoute(
        StationRouteDefinition definition,
        StationRouteLayout layout,
        ISpatialPathfinder pathfinder)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(pathfinder);
        return new GameSession(definition, layout, pathfinder);
    }

    public CommandAcknowledgement Execute(IGameCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        return command switch
        {
            SetPauseCommand setPause => Execute(setPause),
            MoveActorCommand moveActor => Execute(moveActor),
            InteractCommand interact => Execute(interact),
            ChooseDialogueResponseCommand chooseResponse => Execute(chooseResponse),
            _ => Reject(command.CommandId, CommandRejectionCode.UnknownCommand),
        };
    }

    public int Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Elapsed time cannot be negative.");
        }

        if (IsPaused)
        {
            return 0;
        }

        _accumulatedSeconds += Math.Min(elapsed.TotalSeconds, MaximumFrameSeconds);
        var advanced = 0;

        while (_accumulatedSeconds >= SecondsPerTick && advanced < MaximumFrameTicks)
        {
            AdvanceOneTick();
            _accumulatedSeconds -= SecondsPerTick;
            advanced++;
        }

        if (advanced == MaximumFrameTicks)
        {
            _accumulatedSeconds = Math.Min(_accumulatedSeconds, SecondsPerTick);
        }

        return advanced;
    }

    public int AdvanceTicks(int count)
    {
        ValidateTickCount(count);

        if (IsPaused)
        {
            return 0;
        }

        for (var index = 0; index < count; index++)
        {
            AdvanceOneTick();
        }

        return count;
    }

    public int StepWhilePaused(int count)
    {
        ValidateTickCount(count);

        if (!IsPaused)
        {
            throw new InvalidOperationException("Exact stepping is available only while the session is paused.");
        }

        for (var index = 0; index < count; index++)
        {
            AdvanceOneTick();
        }

        return count;
    }

    public GameObservation Observe()
    {
        return new GameObservation(
            Tick,
            IsPaused,
            _eventSequence,
            _stationRoute is null ? null : ObserveStationRoute(_stationRoute));
    }

    public IReadOnlyList<GameplayEvent> EventsSince(long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);

        return _events.Where(gameEvent => gameEvent.Sequence > sequence).ToArray();
    }

    public bool WasEventHistoryTruncatedAfter(long sequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sequence);

        return sequence < OldestRetainedEventSequence - 1;
    }

    private CommandAcknowledgement Execute(SetPauseCommand command)
    {
        if (command.Paused != IsPaused)
        {
            IsPaused = command.Paused;
            _accumulatedSeconds = 0;
            if (!IsPaused)
            {
                PromotePendingAction();
            }

            Record(GameplayEventType.PauseChanged, command.CommandId, paused: IsPaused);
        }

        return Accept(command.CommandId);
    }

    private CommandAcknowledgement Execute(MoveActorCommand command)
    {
        if (!TryValidatePrimaryOrder(command.ActorId, out var station, out var rejection))
        {
            return Reject(command.CommandId, rejection);
        }

        if (!command.Destination.IsFinite)
        {
            return Reject(command.CommandId, CommandRejectionCode.InvalidDestination);
        }

        var pathResult = _pathfinder!.FindPath(
            command.ActorId,
            station.Actor.Position,
            command.Destination);
        if (!TryNormalizePath(
                pathResult,
                station.Actor.Position,
                destination => destination.DistanceTo(command.Destination) <= MoveEndpointToleranceMeters,
                out var waypoints))
        {
            return Reject(command.CommandId, CommandRejectionCode.DestinationUnreachable);
        }

        AssignPrimaryAction(
            station,
            new PrimaryActionRuntime(
                command.CommandId,
                PrimaryActionKind.Move,
                command.Destination,
                interactionTargetId: null,
                waypoints));
        return Accept(command.CommandId);
    }

    private CommandAcknowledgement Execute(InteractCommand command)
    {
        if (!TryValidatePrimaryOrder(command.ActorId, out var station, out var rejection))
        {
            return Reject(command.CommandId, rejection);
        }

        if (!station.Interactions.TryGetValue(command.TargetId, out var target))
        {
            return Reject(command.CommandId, CommandRejectionCode.UnknownInteraction);
        }

        if (!IsInteractionAvailable(station, target))
        {
            return Reject(command.CommandId, CommandRejectionCode.InteractionUnavailable);
        }

        IReadOnlyList<WorldPosition> waypoints = [];
        if (!IsWithinUseRadius(station.Actor.Position, target))
        {
            var pathResult = _pathfinder!.FindPath(
                command.ActorId,
                station.Actor.Position,
                target.Placement.ApproachPosition);
            if (!TryNormalizePath(
                    pathResult,
                    station.Actor.Position,
                    destination => destination.DistanceTo(target.Placement.ApproachPosition)
                            <= MoveEndpointToleranceMeters
                        && IsWithinUseRadius(destination, target),
                    out waypoints))
            {
                return Reject(command.CommandId, CommandRejectionCode.DestinationUnreachable);
            }
        }

        AssignPrimaryAction(
            station,
            new PrimaryActionRuntime(
                command.CommandId,
                PrimaryActionKind.Interact,
                target.Placement.ApproachPosition,
                command.TargetId,
                waypoints));
        return Accept(command.CommandId);
    }

    private CommandAcknowledgement Execute(ChooseDialogueResponseCommand command)
    {
        if (_stationRoute is null)
        {
            return Reject(command.CommandId, CommandRejectionCode.UnknownCommand);
        }

        var station = _stationRoute;
        if (station.Phase == ScenarioPhase.Completed)
        {
            return Reject(command.CommandId, CommandRejectionCode.ScenarioCompleted);
        }

        var actorRejection = ValidateActor(station, command.ActorId);
        if (actorRejection is not null)
        {
            return Reject(command.CommandId, actorRejection.Value);
        }

        if (station.ActiveDialogueInteractionId is not EntityId activeInteractionId)
        {
            return Reject(command.CommandId, CommandRejectionCode.NoActiveDialogue);
        }

        if (activeInteractionId != command.InteractionId)
        {
            return Reject(command.CommandId, CommandRejectionCode.DialogueMismatch);
        }

        var interaction = station.Interactions[activeInteractionId];
        var dialogue = interaction.Definition.Dialogue!;
        if (dialogue.Response.Id != command.ResponseId)
        {
            return Reject(command.CommandId, CommandRejectionCode.UnknownDialogueResponse);
        }

        station.ActiveDialogueInteractionId = null;
        interaction.Completed = true;
        Record(
            GameplayEventType.DialogueResponseChosen,
            command.CommandId,
            detail: new DialogueResponseChosenEventDetail(
                command.CommandId,
                command.ActorId,
                command.InteractionId,
                command.ResponseId));
        RecordInteractionCompleted(station, command.CommandId, interaction);

        var previousObjective = station.Definition.BriefingObjective;
        station.CurrentObjective = station.Definition.DestinationObjective;
        Record(
            GameplayEventType.ObjectiveChanged,
            command.CommandId,
            detail: new ObjectiveChangedEventDetail(
                command.CommandId,
                previousObjective.Id,
                station.CurrentObjective.Id,
                ObjectiveStatus.Active));

        return Accept(command.CommandId);
    }

    private bool TryValidatePrimaryOrder(
        EntityId actorId,
        out StationRouteRuntime station,
        out CommandRejectionCode rejection)
    {
        if (_stationRoute is null)
        {
            station = null!;
            rejection = CommandRejectionCode.UnknownCommand;
            return false;
        }

        station = _stationRoute;
        if (station.Phase == ScenarioPhase.Completed)
        {
            rejection = CommandRejectionCode.ScenarioCompleted;
            return false;
        }

        var actorRejection = ValidateActor(station, actorId);
        if (actorRejection is not null)
        {
            rejection = actorRejection.Value;
            return false;
        }

        if (station.ActiveDialogueInteractionId is not null)
        {
            rejection = CommandRejectionCode.DialogueActive;
            return false;
        }

        rejection = default;
        return true;
    }

    private static CommandRejectionCode? ValidateActor(
        StationRouteRuntime station,
        EntityId actorId)
    {
        if (actorId == station.Actor.Definition.Id)
        {
            return null;
        }

        return station.Interactions.ContainsKey(actorId)
            ? CommandRejectionCode.ActorNotControllable
            : CommandRejectionCode.UnknownActor;
    }

    private void AssignPrimaryAction(StationRouteRuntime station, PrimaryActionRuntime action)
    {
        CommandId? replacedCommandId;
        var pending = IsPaused;
        if (pending)
        {
            replacedCommandId = station.Actor.PendingAction?.CommandId;
            station.Actor.PendingAction = action;
        }
        else
        {
            replacedCommandId = station.Actor.CurrentAction?.CommandId;
            station.Actor.CurrentAction = action;
            station.Actor.PendingAction = null;
        }

        Record(
            GameplayEventType.PrimaryActionAssigned,
            action.CommandId,
            detail: new PrimaryActionAssignedEventDetail(
                action.CommandId,
                station.Actor.Definition.Id,
                action.Kind,
                action.Destination,
                action.InteractionTargetId,
                pending,
                replacedCommandId));
    }

    private void PromotePendingAction()
    {
        if (_stationRoute?.Actor.PendingAction is not PrimaryActionRuntime pending)
        {
            return;
        }

        _stationRoute.Actor.CurrentAction = pending;
        _stationRoute.Actor.PendingAction = null;
    }

    private void AdvanceOneTick()
    {
        Tick++;
        if (_stationRoute is null)
        {
            return;
        }

        PromotePendingAction();
        if (_stationRoute.Actor.CurrentAction is PrimaryActionRuntime action)
        {
            AdvanceAction(_stationRoute, action);
        }
    }

    private void AdvanceAction(StationRouteRuntime station, PrimaryActionRuntime action)
    {
        var remainingDistance = station.Actor.Definition.MovementSpeedMetersPerSecond / TicksPerSecond;
        while (remainingDistance > 0 && action.WaypointIndex < action.Waypoints.Count)
        {
            var waypoint = action.Waypoints[action.WaypointIndex];
            var distance = station.Actor.Position.DistanceTo(waypoint);
            if (distance <= PositionToleranceMeters)
            {
                station.Actor.Position = waypoint;
                action.WaypointIndex++;
                continue;
            }

            if (distance <= remainingDistance + PositionToleranceMeters)
            {
                station.Actor.Position = waypoint;
                action.WaypointIndex++;
                remainingDistance = Math.Max(0, remainingDistance - distance);
                continue;
            }

            var scale = remainingDistance / distance;
            station.Actor.Position = new WorldPosition(
                station.Actor.Position.X + ((waypoint.X - station.Actor.Position.X) * scale),
                station.Actor.Position.Y + ((waypoint.Y - station.Actor.Position.Y) * scale),
                station.Actor.Position.Z + ((waypoint.Z - station.Actor.Position.Z) * scale));
            remainingDistance = 0;
        }

        if (action.WaypointIndex < action.Waypoints.Count)
        {
            return;
        }

        station.Actor.CurrentAction = null;
        Record(
            GameplayEventType.MovementArrived,
            action.CommandId,
            detail: new MovementArrivedEventDetail(
                action.CommandId,
                station.Actor.Definition.Id,
                station.Actor.Position));

        if (action.Kind == PrimaryActionKind.Interact)
        {
            ResolveInteractionAction(station, action);
        }
    }

    private void ResolveInteractionAction(StationRouteRuntime station, PrimaryActionRuntime action)
    {
        if (action.InteractionTargetId is not EntityId targetId
            || !station.Interactions.TryGetValue(targetId, out var interaction)
            || !IsInteractionAvailable(station, interaction)
            || !IsWithinUseRadius(station.Actor.Position, interaction))
        {
            Record(
                GameplayEventType.PrimaryActionFailed,
                action.CommandId,
                rejectionCode: CommandRejectionCode.InteractionUnavailable,
                detail: new PrimaryActionFailedEventDetail(
                    action.CommandId,
                    station.Actor.Definition.Id,
                    CommandRejectionCode.InteractionUnavailable));
            return;
        }

        switch (interaction.Definition.Effect)
        {
            case StationInteractionEffect.BeginBriefingDialogue:
                station.ActiveDialogueInteractionId = interaction.Definition.Id;
                Record(
                    GameplayEventType.DialogueStarted,
                    action.CommandId,
                    detail: new DialogueStartedEventDetail(
                        action.CommandId,
                        station.Actor.Definition.Id,
                        interaction.Definition.Id));
                break;

            case StationInteractionEffect.RecordObservation:
                interaction.Completed = true;
                RecordInteractionCompleted(station, action.CommandId, interaction);
                break;

            case StationInteractionEffect.CompleteScenario:
                CompleteScenario(station, action.CommandId, interaction);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported station interaction effect '{interaction.Definition.Effect}'.");
        }
    }

    private void CompleteScenario(
        StationRouteRuntime station,
        CommandId commandId,
        InteractionRuntime interaction)
    {
        interaction.Completed = true;
        RecordInteractionCompleted(station, commandId, interaction);

        station.Phase = ScenarioPhase.Completed;
        station.Actor.CurrentAction = null;
        station.Actor.PendingAction = null;
        Record(
            GameplayEventType.ObjectiveChanged,
            commandId,
            detail: new ObjectiveChangedEventDetail(
                commandId,
                station.CurrentObjective.Id,
                station.CurrentObjective.Id,
                ObjectiveStatus.Completed));
        Record(
            GameplayEventType.ScenarioCompleted,
            commandId,
            detail: new ScenarioCompletedEventDetail(commandId, station.Definition.ScenarioId));
    }

    private void RecordInteractionCompleted(
        StationRouteRuntime station,
        CommandId commandId,
        InteractionRuntime interaction)
    {
        Record(
            GameplayEventType.InteractionCompleted,
            commandId,
            detail: new InteractionCompletedEventDetail(
                commandId,
                station.Actor.Definition.Id,
                interaction.Definition.Id,
                interaction.Definition.Effect));
    }

    private static bool IsInteractionAvailable(
        StationRouteRuntime station,
        InteractionRuntime interaction)
    {
        if (station.Phase == ScenarioPhase.Completed)
        {
            return false;
        }

        if (interaction.Completed)
        {
            return interaction.Definition.Effect == StationInteractionEffect.RecordObservation;
        }

        return interaction.Definition.Effect != StationInteractionEffect.CompleteScenario
            || station.CurrentObjective.Id == station.Definition.DestinationObjective.Id;
    }

    private static bool IsWithinUseRadius(
        WorldPosition actorPosition,
        InteractionRuntime interaction)
    {
        return actorPosition.DistanceTo(interaction.Placement.Position)
            <= interaction.Definition.UseRadiusMeters;
    }

    private static bool TryNormalizePath(
        SpatialPathResult pathResult,
        WorldPosition origin,
        Func<WorldPosition, bool> validEndpoint,
        out IReadOnlyList<WorldPosition> waypoints)
    {
        ArgumentNullException.ThrowIfNull(pathResult);
        ArgumentNullException.ThrowIfNull(validEndpoint);

        waypoints = [];
        if (!pathResult.IsReachable || pathResult.Waypoints.Count > MaximumPathWaypoints)
        {
            return false;
        }

        var normalized = pathResult.Waypoints
            .Where((waypoint, index) => index != 0 || waypoint.DistanceTo(origin) > PositionToleranceMeters)
            .ToArray();
        if (normalized.Any(waypoint => !waypoint.IsFinite))
        {
            return false;
        }

        var endpoint = normalized.Length == 0 ? origin : normalized[^1];
        if (!validEndpoint(endpoint))
        {
            return false;
        }

        waypoints = normalized;
        return true;
    }

    private static StationRouteObservation ObserveStationRoute(StationRouteRuntime station)
    {
        var objectiveStatus = station.Phase == ScenarioPhase.Completed
            ? ObjectiveStatus.Completed
            : ObjectiveStatus.Active;
        var interactions = station.Interactions.Values
            .OrderBy(interaction => interaction.Definition.Id.Value, StringComparer.Ordinal)
            .Select(interaction => new InteractionObservation(
                interaction.Definition.Id,
                interaction.Definition.Kind,
                interaction.Definition.Prompt,
                interaction.Placement.Position,
                interaction.Placement.ApproachPosition,
                interaction.Definition.UseRadiusMeters,
                GetInteractionState(station, interaction),
                IsInteractionAvailable(station, interaction),
                interaction.Completed ? interaction.Definition.ResultText : null))
            .ToArray();

        DialogueObservation? activeDialogue = null;
        if (station.ActiveDialogueInteractionId is EntityId dialogueInteractionId)
        {
            var dialogue = station.Interactions[dialogueInteractionId].Definition.Dialogue!;
            activeDialogue = new DialogueObservation(
                dialogueInteractionId,
                dialogue.Speaker,
                dialogue.Line,
                new DialogueResponseObservation(dialogue.Response.Id, dialogue.Response.Text));
        }

        return new StationRouteObservation(
            station.Definition.ScenarioId,
            station.Definition.SchemaVersion,
            station.Definition.ContentRevision,
            station.Phase,
            new ActorObservation(
                station.Actor.Definition.Id,
                station.Actor.Definition.DisplayName,
                station.Actor.Position,
                ObserveAction(station.Actor.CurrentAction),
                ObserveAction(station.Actor.PendingAction)),
            new ObjectiveObservation(
                station.CurrentObjective.Id,
                station.CurrentObjective.Text,
                objectiveStatus),
            interactions,
            activeDialogue);
    }

    private static PrimaryActionObservation? ObserveAction(PrimaryActionRuntime? action)
    {
        return action is null
            ? null
            : new PrimaryActionObservation(
                action.CommandId,
                action.Kind,
                action.Destination,
                action.InteractionTargetId);
    }

    private static InteractionState GetInteractionState(
        StationRouteRuntime station,
        InteractionRuntime interaction)
    {
        if (station.ActiveDialogueInteractionId == interaction.Definition.Id)
        {
            return InteractionState.DialogueActive;
        }

        if (interaction.Completed)
        {
            return InteractionState.Completed;
        }

        return IsInteractionAvailable(station, interaction)
            ? InteractionState.Available
            : InteractionState.Unavailable;
    }

    private CommandAcknowledgement Accept(CommandId commandId)
    {
        Record(GameplayEventType.CommandAccepted, commandId);
        return new CommandAcknowledgement(commandId, true, null, Observe());
    }

    private CommandAcknowledgement Reject(CommandId commandId, CommandRejectionCode rejectionCode)
    {
        Record(GameplayEventType.CommandRejected, commandId, rejectionCode: rejectionCode);
        return new CommandAcknowledgement(commandId, false, rejectionCode, Observe());
    }

    private void Record(
        GameplayEventType type,
        CommandId? commandId = null,
        bool? paused = null,
        CommandRejectionCode? rejectionCode = null,
        GameplayEventDetail? detail = null)
    {
        _eventSequence++;
        _events.Add(new GameplayEvent(
            _eventSequence,
            Tick,
            type,
            commandId,
            paused,
            rejectionCode,
            detail));
        if (_events.Count > MaximumRetainedEvents)
        {
            _events.RemoveAt(0);
        }
    }

    private static void ValidateLayout(
        StationRouteDefinition definition,
        StationRouteLayout layout)
    {
        var expectedIds = definition.Interactions.Select(interaction => interaction.Id).ToHashSet();
        var actualIds = layout.Interactions.Select(interaction => interaction.InteractionId).ToHashSet();
        if (!expectedIds.SetEquals(actualIds))
        {
            throw new InvalidDataException(
                "Station route layout interaction IDs must exactly match the content definition.");
        }

        foreach (var interaction in definition.Interactions)
        {
            _ = layout.TryGetInteraction(interaction.Id, out var placement);
            if (placement.ApproachPosition.DistanceTo(placement.Position) > interaction.UseRadiusMeters)
            {
                throw new InvalidDataException(
                    $"Interaction '{interaction.Id}' approach position is outside its use radius.");
            }
        }
    }

    private static void ValidateTickCount(int count)
    {
        if (count < 0 || count > MaximumDirectTickAdvance)
        {
            throw new ArgumentOutOfRangeException(
                nameof(count),
                $"Tick count must be between 0 and {MaximumDirectTickAdvance}.");
        }
    }

    private sealed class StationRouteRuntime
    {
        public StationRouteRuntime(StationRouteDefinition definition, StationRouteLayout layout)
        {
            Definition = definition;
            CurrentObjective = definition.BriefingObjective;
            Actor = new ActorRuntime(definition.Protagonist, layout.ProtagonistStart);
            Interactions = definition.Interactions.ToDictionary(
                interaction => interaction.Id,
                interaction =>
                {
                    _ = layout.TryGetInteraction(interaction.Id, out var placement);
                    return new InteractionRuntime(interaction, placement);
                });
        }

        public StationRouteDefinition Definition { get; }

        public ActorRuntime Actor { get; }

        public Dictionary<EntityId, InteractionRuntime> Interactions { get; }

        public StationObjectiveDefinition CurrentObjective { get; set; }

        public ScenarioPhase Phase { get; set; }

        public EntityId? ActiveDialogueInteractionId { get; set; }
    }

    private sealed class ActorRuntime(
        StationActorDefinition definition,
        WorldPosition position)
    {
        public StationActorDefinition Definition { get; } = definition;

        public WorldPosition Position { get; set; } = position;

        public PrimaryActionRuntime? CurrentAction { get; set; }

        public PrimaryActionRuntime? PendingAction { get; set; }
    }

    private sealed class InteractionRuntime(
        StationInteractionDefinition definition,
        StationInteractionPlacement placement)
    {
        public StationInteractionDefinition Definition { get; } = definition;

        public StationInteractionPlacement Placement { get; } = placement;

        public bool Completed { get; set; }
    }

    private sealed class PrimaryActionRuntime(
        CommandId commandId,
        PrimaryActionKind kind,
        WorldPosition destination,
        EntityId? interactionTargetId,
        IReadOnlyList<WorldPosition> waypoints)
    {
        public CommandId CommandId { get; } = commandId;

        public PrimaryActionKind Kind { get; } = kind;

        public WorldPosition Destination { get; } = destination;

        public EntityId? InteractionTargetId { get; } = interactionTargetId;

        public IReadOnlyList<WorldPosition> Waypoints { get; } = waypoints;

        public int WaypointIndex { get; set; }
    }
}
