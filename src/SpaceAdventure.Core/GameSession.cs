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
    private const double PartySpacingMeters = 1.1;

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
            ChooseProtagonistKitCommand chooseKit => Execute(chooseKit),
            MoveActorCommand moveActor => Execute(moveActor),
            MovePartyCommand moveParty => Execute(moveParty),
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
                PromotePendingActions();
            }

            Record(GameplayEventType.PauseChanged, command.CommandId, paused: IsPaused);
        }

        return Accept(command.CommandId);
    }

    private CommandAcknowledgement Execute(ChooseProtagonistKitCommand command)
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

        if (station.SelectedProtagonistKit is not null)
        {
            return Reject(command.CommandId, CommandRejectionCode.ProtagonistKitAlreadySelected);
        }

        var kit = station.Definition.ProtagonistKits.SingleOrDefault(candidate => candidate.Id == command.KitId);
        if (kit is null)
        {
            return Reject(command.CommandId, CommandRejectionCode.UnknownProtagonistKit);
        }

        station.SelectedProtagonistKit = kit;
        station.Protagonist.DisplayName = kit.DisplayName;
        station.Protagonist.Loadout = new PartyMemberLoadoutDefinition(
            kit.WeaponName,
            kit.BasicAttackId,
            kit.ActiveAbilityId,
            kit.ActiveAbilityName,
            kit.ActiveAbilityTargetKind);
        station.Phase = ScenarioPhase.InProgress;
        Record(
            GameplayEventType.ProtagonistKitSelected,
            command.CommandId,
            detail: new ProtagonistKitSelectedEventDetail(command.CommandId, kit.Id));
        return Accept(command.CommandId);
    }

    private CommandAcknowledgement Execute(MoveActorCommand command)
    {
        if (!TryValidatePrimaryOrder(command.ActorId, out var station, out var actor, out var rejection))
        {
            return Reject(command.CommandId, rejection);
        }

        if (!command.Destination.IsFinite)
        {
            return Reject(command.CommandId, CommandRejectionCode.InvalidDestination);
        }

        if (!TryCreateMoveAction(command.CommandId, actor, command.Destination, out var action))
        {
            return Reject(command.CommandId, CommandRejectionCode.DestinationUnreachable);
        }

        AssignPrimaryAction(actor, action);
        return Accept(command.CommandId);
    }

    private CommandAcknowledgement Execute(MovePartyCommand command)
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

        if (station.SelectedProtagonistKit is null)
        {
            return Reject(command.CommandId, CommandRejectionCode.ProtagonistKitRequired);
        }

        if (station.ActiveDialogue is not null)
        {
            return Reject(command.CommandId, CommandRejectionCode.DialogueActive);
        }

        if (command.ActorIds.Count == 0)
        {
            return Reject(command.CommandId, CommandRejectionCode.EmptyPartySelection);
        }

        if (command.ActorIds.Distinct().Count() != command.ActorIds.Count)
        {
            return Reject(command.CommandId, CommandRejectionCode.DuplicateActor);
        }

        if (!command.Destination.IsFinite)
        {
            return Reject(command.CommandId, CommandRejectionCode.InvalidDestination);
        }

        var actors = new List<ActorRuntime>(command.ActorIds.Count);
        foreach (var actorId in command.ActorIds)
        {
            var actorRejection = ValidateActor(station, actorId, out var actor);
            if (actorRejection is not null)
            {
                return Reject(command.CommandId, actorRejection.Value);
            }

            actors.Add(actor!);
        }

        actors.Sort((left, right) => left.PartyOrder.CompareTo(right.PartyOrder));
        var destinations = GetPartyDestinations(actors, command.Destination);
        var actions = new List<(ActorRuntime Actor, PrimaryActionRuntime Action)>(actors.Count);
        for (var index = 0; index < actors.Count; index++)
        {
            if (!TryCreateMoveAction(command.CommandId, actors[index], destinations[index], out var action)
                && !TryCreateMoveAction(command.CommandId, actors[index], command.Destination, out action))
            {
                return Reject(command.CommandId, CommandRejectionCode.DestinationUnreachable);
            }

            actions.Add((actors[index], action));
        }

        foreach (var assignment in actions)
        {
            AssignPrimaryAction(assignment.Actor, assignment.Action);
        }

        return Accept(command.CommandId);
    }

    private CommandAcknowledgement Execute(InteractCommand command)
    {
        if (!TryValidatePrimaryOrder(command.ActorId, out var station, out var actor, out var rejection))
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
        if (!IsWithinUseRadius(actor.Position, target))
        {
            var pathResult = _pathfinder!.FindPath(
                command.ActorId,
                actor.Position,
                target.Placement.ApproachPosition);
            if (!TryNormalizePath(
                    pathResult,
                    actor.Position,
                    destination => destination.DistanceTo(target.Placement.ApproachPosition)
                            <= MoveEndpointToleranceMeters
                        && IsWithinUseRadius(destination, target),
                    out waypoints))
            {
                return Reject(command.CommandId, CommandRejectionCode.DestinationUnreachable);
            }
        }

        AssignPrimaryAction(
            actor,
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

        var actorRejection = ValidateActor(station, command.ActorId, out _);
        if (actorRejection is not null)
        {
            return Reject(command.CommandId, actorRejection.Value);
        }

        if (station.ActiveDialogue is not { } activeDialogue)
        {
            return Reject(command.CommandId, CommandRejectionCode.NoActiveDialogue);
        }

        if (activeDialogue.InteractionId != command.InteractionId
            || activeDialogue.ActorId != command.ActorId)
        {
            return Reject(command.CommandId, CommandRejectionCode.DialogueMismatch);
        }

        var interaction = station.Interactions[activeDialogue.InteractionId];
        var dialogue = interaction.Definition.Dialogue!;
        var response = dialogue.Responses.SingleOrDefault(candidate => candidate.Id == command.ResponseId);
        if (response is null)
        {
            return Reject(command.CommandId, CommandRejectionCode.UnknownDialogueResponse);
        }

        station.ActiveDialogue = null;
        interaction.Completed = true;
        Record(
            GameplayEventType.DialogueResponseChosen,
            command.CommandId,
            detail: new DialogueResponseChosenEventDetail(
                command.CommandId,
                command.ActorId,
                command.InteractionId,
                command.ResponseId));
        RecordInteractionCompleted(command.CommandId, command.ActorId, interaction);

        switch (response.Effect)
        {
            case StationDialogueResponseEffect.RerouteServicePower:
                ApplyRouteConsequence(station, command.CommandId, RoutePowerMode.ServiceRerouted);
                ChangeObjective(station, command.CommandId, station.Definition.EntryDoorObjective);
                break;

            case StationDialogueResponseEffect.PreserveShelterPower:
                ApplyRouteConsequence(station, command.CommandId, RoutePowerMode.ShelterPreserved);
                ChangeObjective(station, command.CommandId, station.Definition.EntryDoorObjective);
                break;

            case StationDialogueResponseEffect.RecruitProtector:
                RecruitCompanion(station, command.CommandId);
                ChangeObjective(station, command.CommandId, station.Definition.DestinationObjective);
                break;

            default:
                throw new InvalidOperationException($"Unsupported dialogue response effect '{response.Effect}'.");
        }

        return Accept(command.CommandId);
    }

    private bool TryValidatePrimaryOrder(
        EntityId actorId,
        out StationRouteRuntime station,
        out ActorRuntime actor,
        out CommandRejectionCode rejection)
    {
        if (_stationRoute is null)
        {
            station = null!;
            actor = null!;
            rejection = CommandRejectionCode.UnknownCommand;
            return false;
        }

        station = _stationRoute;
        if (station.Phase == ScenarioPhase.Completed)
        {
            actor = null!;
            rejection = CommandRejectionCode.ScenarioCompleted;
            return false;
        }

        if (station.SelectedProtagonistKit is null)
        {
            actor = null!;
            rejection = CommandRejectionCode.ProtagonistKitRequired;
            return false;
        }

        var actorRejection = ValidateActor(station, actorId, out var validatedActor);
        if (actorRejection is not null)
        {
            actor = null!;
            rejection = actorRejection.Value;
            return false;
        }

        if (station.ActiveDialogue is not null)
        {
            actor = null!;
            rejection = CommandRejectionCode.DialogueActive;
            return false;
        }

        actor = validatedActor!;
        rejection = default;
        return true;
    }

    private static CommandRejectionCode? ValidateActor(
        StationRouteRuntime station,
        EntityId actorId,
        out ActorRuntime? actor)
    {
        if (station.Actors.TryGetValue(actorId, out actor))
        {
            return null;
        }

        return actorId == station.Definition.Companion.Id
            || station.Interactions.ContainsKey(actorId)
                ? CommandRejectionCode.ActorNotControllable
                : CommandRejectionCode.UnknownActor;
    }

    private bool TryCreateMoveAction(
        CommandId commandId,
        ActorRuntime actor,
        WorldPosition destination,
        out PrimaryActionRuntime action)
    {
        var pathResult = _pathfinder!.FindPath(actor.Id, actor.Position, destination);
        if (!TryNormalizePath(
                pathResult,
                actor.Position,
                endpoint => endpoint.DistanceTo(destination) <= MoveEndpointToleranceMeters,
                out var waypoints))
        {
            action = null!;
            return false;
        }

        action = new PrimaryActionRuntime(
            commandId,
            PrimaryActionKind.Move,
            destination,
            interactionTargetId: null,
            waypoints);
        return true;
    }

    private static WorldPosition[] GetPartyDestinations(
        IReadOnlyList<ActorRuntime> actors,
        WorldPosition destination)
    {
        if (actors.Count == 1)
        {
            return [destination];
        }

        var centerX = actors.Average(actor => actor.Position.X);
        var centerZ = actors.Average(actor => actor.Position.Z);
        var directionX = destination.X - centerX;
        var directionZ = destination.Z - centerZ;
        var directionLength = Math.Sqrt((directionX * directionX) + (directionZ * directionZ));
        if (directionLength <= PositionToleranceMeters)
        {
            directionX = 1;
            directionZ = 0;
        }
        else
        {
            directionX /= directionLength;
            directionZ /= directionLength;
        }

        var rightX = -directionZ;
        var rightZ = directionX;
        var firstOffset = -((actors.Count - 1) * PartySpacingMeters) / 2.0;
        return actors.Select((_, index) =>
        {
            var offset = firstOffset + (index * PartySpacingMeters);
            return new WorldPosition(
                destination.X + (rightX * offset),
                destination.Y,
                destination.Z + (rightZ * offset));
        }).ToArray();
    }

    private void AssignPrimaryAction(ActorRuntime actor, PrimaryActionRuntime action)
    {
        CommandId? replacedCommandId;
        var pending = IsPaused;
        if (pending)
        {
            replacedCommandId = actor.PendingAction?.CommandId;
            actor.PendingAction = action;
        }
        else
        {
            replacedCommandId = actor.CurrentAction?.CommandId;
            actor.CurrentAction = action;
            actor.PendingAction = null;
        }

        Record(
            GameplayEventType.PrimaryActionAssigned,
            action.CommandId,
            detail: new PrimaryActionAssignedEventDetail(
                action.CommandId,
                actor.Id,
                action.Kind,
                action.Destination,
                action.InteractionTargetId,
                pending,
                replacedCommandId));
    }

    private void PromotePendingActions()
    {
        if (_stationRoute is null)
        {
            return;
        }

        foreach (var actor in _stationRoute.Actors.Values.OrderBy(actor => actor.PartyOrder))
        {
            if (actor.PendingAction is not PrimaryActionRuntime pending)
            {
                continue;
            }

            actor.CurrentAction = pending;
            actor.PendingAction = null;
        }
    }

    private void AdvanceOneTick()
    {
        Tick++;
        if (_stationRoute is null)
        {
            return;
        }

        PromotePendingActions();
        foreach (var actor in _stationRoute.Actors.Values.OrderBy(actor => actor.PartyOrder).ToArray())
        {
            if (actor.CurrentAction is PrimaryActionRuntime action)
            {
                AdvanceAction(_stationRoute, actor, action);
            }
        }
    }

    private void AdvanceAction(
        StationRouteRuntime station,
        ActorRuntime actor,
        PrimaryActionRuntime action)
    {
        var remainingDistance = actor.MovementSpeedMetersPerSecond / TicksPerSecond;
        while (remainingDistance > 0 && action.WaypointIndex < action.Waypoints.Count)
        {
            var waypoint = action.Waypoints[action.WaypointIndex];
            var distance = actor.Position.DistanceTo(waypoint);
            if (distance <= PositionToleranceMeters)
            {
                actor.Position = waypoint;
                action.WaypointIndex++;
                continue;
            }

            if (distance <= remainingDistance + PositionToleranceMeters)
            {
                actor.Position = waypoint;
                action.WaypointIndex++;
                remainingDistance = Math.Max(0, remainingDistance - distance);
                continue;
            }

            var scale = remainingDistance / distance;
            actor.Position = new WorldPosition(
                actor.Position.X + ((waypoint.X - actor.Position.X) * scale),
                actor.Position.Y + ((waypoint.Y - actor.Position.Y) * scale),
                actor.Position.Z + ((waypoint.Z - actor.Position.Z) * scale));
            remainingDistance = 0;
        }

        TryAutoOpenServiceDoors(station, actor, action);

        if (action.WaypointIndex < action.Waypoints.Count)
        {
            return;
        }

        actor.CurrentAction = null;
        Record(
            GameplayEventType.MovementArrived,
            action.CommandId,
            detail: new MovementArrivedEventDetail(action.CommandId, actor.Id, actor.Position));

        if (action.Kind == PrimaryActionKind.Interact)
        {
            ResolveInteractionAction(station, actor, action);
        }
    }

    private void ResolveInteractionAction(
        StationRouteRuntime station,
        ActorRuntime actor,
        PrimaryActionRuntime action)
    {
        if (action.InteractionTargetId is not EntityId targetId
            || !station.Interactions.TryGetValue(targetId, out var interaction)
            || !IsInteractionAvailable(station, interaction)
            || !IsWithinUseRadius(actor.Position, interaction))
        {
            Record(
                GameplayEventType.PrimaryActionFailed,
                action.CommandId,
                rejectionCode: CommandRejectionCode.InteractionUnavailable,
                detail: new PrimaryActionFailedEventDetail(
                    action.CommandId,
                    actor.Id,
                    CommandRejectionCode.InteractionUnavailable));
            return;
        }

        switch (interaction.Definition.Effect)
        {
            case StationInteractionEffect.BeginSurvivorDialogue:
            case StationInteractionEffect.BeginRecruitmentDialogue:
                station.ActiveDialogue = new ActiveDialogueRuntime(
                    interaction.Definition.Id,
                    actor.Id);
                Record(
                    GameplayEventType.DialogueStarted,
                    action.CommandId,
                    detail: new DialogueStartedEventDetail(
                        action.CommandId,
                        actor.Id,
                        interaction.Definition.Id));
                break;

            case StationInteractionEffect.RecordObservation:
                interaction.Completed = true;
                RecordInteractionCompleted(action.CommandId, actor.Id, interaction);
                break;

            case StationInteractionEffect.OpenEntryServiceDoor:
            case StationInteractionEffect.OpenSoloExitServiceDoor:
                // The solo-exit branch is staged for Phase 4; availability keeps it and
                // the following recruitment progression unreachable in this slice.
                CompleteServiceDoorInteraction(station, actor, interaction, action.CommandId);
                break;

            case StationInteractionEffect.CompleteScenario:
                CompleteScenario(station, action.CommandId, actor.Id, interaction);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported station interaction effect '{interaction.Definition.Effect}'.");
        }
    }

    private void TryAutoOpenServiceDoors(
        StationRouteRuntime station,
        ActorRuntime actor,
        PrimaryActionRuntime action)
    {
        foreach (var interaction in station.ServiceDoorInteractions)
        {
            if (action.Kind == PrimaryActionKind.Interact
                && action.InteractionTargetId == interaction.Definition.Id)
            {
                continue;
            }

            if (IsInteractionAvailable(station, interaction)
                && IsWithinUseRadius(actor.Position, interaction))
            {
                CompleteServiceDoorInteraction(station, actor, interaction, action.CommandId);
            }
        }
    }

    private void CompleteServiceDoorInteraction(
        StationRouteRuntime station,
        ActorRuntime actor,
        InteractionRuntime interaction,
        CommandId commandId)
    {
        interaction.Completed = true;
        RecordInteractionCompleted(commandId, actor.Id, interaction);

        var nextObjective = interaction.Definition.Effect switch
        {
            StationInteractionEffect.OpenEntryServiceDoor =>
                station.Definition.CombatThresholdObjective,
            StationInteractionEffect.OpenSoloExitServiceDoor =>
                station.Definition.RecruitmentObjective,
            _ => throw new InvalidOperationException(
                $"Interaction '{interaction.Definition.Id}' is not a service door."),
        };
        ChangeObjective(station, commandId, nextObjective);
    }

    private void ApplyRouteConsequence(
        StationRouteRuntime station,
        CommandId commandId,
        RoutePowerMode routePowerMode)
    {
        station.RoutePowerMode = routePowerMode;
        Record(
            GameplayEventType.RouteConsequenceSelected,
            commandId,
            detail: new RouteConsequenceSelectedEventDetail(commandId, routePowerMode));
    }

    private void RecruitCompanion(StationRouteRuntime station, CommandId commandId)
    {
        if (station.Actors.ContainsKey(station.Definition.Companion.Id))
        {
            return;
        }

        var companion = new ActorRuntime(
            station.Definition.Companion,
            station.CompanionPlacement.Position,
            partyOrder: 1);
        station.Actors.Add(companion.Id, companion);
        Record(
            GameplayEventType.PartyMemberRecruited,
            commandId,
            detail: new PartyMemberRecruitedEventDetail(commandId, companion.Id));
    }

    private void ChangeObjective(
        StationRouteRuntime station,
        CommandId commandId,
        StationObjectiveDefinition objective)
    {
        var previousObjective = station.CurrentObjective;
        station.CurrentObjective = objective;
        Record(
            GameplayEventType.ObjectiveChanged,
            commandId,
            detail: new ObjectiveChangedEventDetail(
                commandId,
                previousObjective.Id,
                objective.Id,
                ObjectiveStatus.Active));
    }

    private void CompleteScenario(
        StationRouteRuntime station,
        CommandId commandId,
        EntityId actorId,
        InteractionRuntime interaction)
    {
        interaction.Completed = true;
        RecordInteractionCompleted(commandId, actorId, interaction);
        station.Phase = ScenarioPhase.Completed;
        foreach (var actor in station.Actors.Values)
        {
            actor.CurrentAction = null;
            actor.PendingAction = null;
        }

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
        CommandId commandId,
        EntityId actorId,
        InteractionRuntime interaction)
    {
        Record(
            GameplayEventType.InteractionCompleted,
            commandId,
            detail: new InteractionCompletedEventDetail(
                commandId,
                actorId,
                interaction.Definition.Id,
                interaction.Definition.Effect));
    }

    private static bool IsInteractionAvailable(
        StationRouteRuntime station,
        InteractionRuntime interaction)
    {
        if (station.Phase != ScenarioPhase.InProgress)
        {
            return false;
        }

        return interaction.Definition.Effect switch
        {
            StationInteractionEffect.BeginSurvivorDialogue =>
                !interaction.Completed
                && station.CurrentObjective.Id == station.Definition.BriefingObjective.Id,
            StationInteractionEffect.BeginRecruitmentDialogue =>
                !interaction.Completed
                && station.CurrentObjective.Id == station.Definition.RecruitmentObjective.Id,
            StationInteractionEffect.RecordObservation =>
                !interaction.Completed && station.RoutePowerMode != RoutePowerMode.Unset,
            StationInteractionEffect.OpenEntryServiceDoor =>
                !interaction.Completed
                && station.CurrentObjective.Id == station.Definition.EntryDoorObjective.Id,
            StationInteractionEffect.OpenSoloExitServiceDoor => false,
            StationInteractionEffect.CompleteScenario =>
                !interaction.Completed
                && station.CurrentObjective.Id == station.Definition.DestinationObjective.Id
                && station.Actors.ContainsKey(station.Definition.Companion.Id),
            _ => false,
        };
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
        var party = station.Actors.Values
            .OrderBy(actor => actor.PartyOrder)
            .Select(ObserveActor)
            .ToArray();
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
                interaction.Completed ? GetResultText(station, interaction) : null))
            .ToArray();

        DialogueObservation? activeDialogue = null;
        if (station.ActiveDialogue is { } activeDialogueRuntime)
        {
            var dialogue = station.Interactions[activeDialogueRuntime.InteractionId].Definition.Dialogue!;
            activeDialogue = new DialogueObservation(
                activeDialogueRuntime.InteractionId,
                activeDialogueRuntime.ActorId,
                dialogue.Speaker,
                dialogue.Line,
                dialogue.Responses
                    .Select(response => new DialogueResponseObservation(response.Id, response.Text))
                    .ToArray());
        }

        var availableKits = station.Definition.ProtagonistKits.Select(ObserveKit).ToArray();
        return new StationRouteObservation(
            station.Definition.ScenarioId,
            station.Definition.SchemaVersion,
            station.Definition.ContentRevision,
            station.Phase,
            ObserveActor(station.Protagonist),
            party,
            availableKits,
            station.SelectedProtagonistKit is null ? null : ObserveKit(station.SelectedProtagonistKit),
            station.RoutePowerMode,
            new ObjectiveObservation(
                station.CurrentObjective.Id,
                station.CurrentObjective.Text,
                objectiveStatus),
            interactions,
            activeDialogue);
    }

    private static ActorObservation ObserveActor(ActorRuntime actor)
    {
        return new ActorObservation(
            actor.Id,
            actor.DisplayName,
            actor.Loadout is null
                ? null
                : new PartyMemberLoadoutObservation(
                    actor.Loadout.WeaponName,
                    actor.Loadout.BasicAttackId,
                    actor.Loadout.ActiveAbilityId,
                    actor.Loadout.ActiveAbilityName,
                    actor.Loadout.ActiveAbilityTargetKind),
            actor.Position,
            ObserveAction(actor.CurrentAction),
            ObserveAction(actor.PendingAction));
    }

    private static ProtagonistKitObservation ObserveKit(ProtagonistKitDefinition kit)
    {
        return new ProtagonistKitObservation(
            kit.Id,
            kit.DisplayName,
            kit.Role,
            kit.WeaponName,
            kit.BasicAttackId,
            kit.ActiveAbilityId,
            kit.ActiveAbilityName,
            kit.ActiveAbilityTargetKind);
    }

    private static PrimaryActionObservation? ObserveAction(PrimaryActionRuntime? action)
    {
        return action is null
            ? null
            : new PrimaryActionObservation(
                action.CommandId,
                action.Kind,
                action.Destination,
                action.WaypointIndex < action.Waypoints.Count,
                action.InteractionTargetId);
    }

    private static InteractionState GetInteractionState(
        StationRouteRuntime station,
        InteractionRuntime interaction)
    {
        if (station.ActiveDialogue?.InteractionId == interaction.Definition.Id)
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

    private static string? GetResultText(
        StationRouteRuntime station,
        InteractionRuntime interaction)
    {
        if (interaction.Definition.Effect == StationInteractionEffect.RecordObservation
            && station.RoutePowerMode == RoutePowerMode.ShelterPreserved)
        {
            return interaction.Definition.PreservedResultText;
        }

        return interaction.Definition.ResultText;
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
        var expectedInteractionIds = definition.Interactions.Select(interaction => interaction.Id).ToHashSet();
        var actualInteractionIds = layout.Interactions.Select(interaction => interaction.InteractionId).ToHashSet();
        if (!expectedInteractionIds.SetEquals(actualInteractionIds))
        {
            throw new InvalidDataException(
                "Station route layout interaction IDs must exactly match the content definition.");
        }

        var actorIds = layout.Actors.Select(actor => actor.ActorId).ToArray();
        if (actorIds.Length != 1 || actorIds[0] != definition.Companion.Id)
        {
            throw new InvalidDataException(
                "Station route layout must define exactly the companion actor placement.");
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

    private sealed record ActiveDialogueRuntime(EntityId InteractionId, EntityId ActorId);

    private sealed class StationRouteRuntime
    {
        public StationRouteRuntime(StationRouteDefinition definition, StationRouteLayout layout)
        {
            Definition = definition;
            CurrentObjective = definition.BriefingObjective;
            Phase = ScenarioPhase.AwaitingProtagonistSelection;
            Protagonist = new ActorRuntime(definition.Protagonist, layout.ProtagonistStart, partyOrder: 0);
            Actors = new Dictionary<EntityId, ActorRuntime> { [Protagonist.Id] = Protagonist };
            _ = layout.TryGetActor(definition.Companion.Id, out var companionPlacement);
            CompanionPlacement = companionPlacement;
            Interactions = definition.Interactions.ToDictionary(
                interaction => interaction.Id,
                interaction =>
                {
                    _ = layout.TryGetInteraction(interaction.Id, out var placement);
                    return new InteractionRuntime(interaction, placement);
                });
            ServiceDoorInteractions = Interactions.Values
                .Where(interaction => interaction.Definition.Effect is
                    StationInteractionEffect.OpenEntryServiceDoor
                    or StationInteractionEffect.OpenSoloExitServiceDoor)
                .OrderBy(
                    interaction => interaction.Definition.Id.Value,
                    StringComparer.Ordinal)
                .ToArray();
        }

        public StationRouteDefinition Definition { get; }

        public ActorRuntime Protagonist { get; }

        public Dictionary<EntityId, ActorRuntime> Actors { get; }

        public StationActorPlacement CompanionPlacement { get; }

        public Dictionary<EntityId, InteractionRuntime> Interactions { get; }

        public IReadOnlyList<InteractionRuntime> ServiceDoorInteractions { get; }

        public StationObjectiveDefinition CurrentObjective { get; set; }

        public ScenarioPhase Phase { get; set; }

        public ProtagonistKitDefinition? SelectedProtagonistKit { get; set; }

        public RoutePowerMode RoutePowerMode { get; set; }

        public ActiveDialogueRuntime? ActiveDialogue { get; set; }
    }

    private sealed class ActorRuntime(
        StationActorDefinition definition,
        WorldPosition position,
        int partyOrder)
    {
        public EntityId Id { get; } = definition.Id;

        public string DisplayName { get; set; } = definition.DisplayName;

        public double MovementSpeedMetersPerSecond { get; } = definition.MovementSpeedMetersPerSecond;

        public PartyMemberLoadoutDefinition? Loadout { get; set; } = definition.Loadout;

        public int PartyOrder { get; } = partyOrder;

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
