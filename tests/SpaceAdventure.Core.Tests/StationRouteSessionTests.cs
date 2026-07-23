using SpaceAdventure.Core;
using Xunit;

namespace SpaceAdventure.Core.Tests;

public sealed class StationRouteSessionTests
{
    private static readonly EntityId ProtagonistId = new("actor.protagonist");
    private static readonly EntityId SurvivorId = new("interaction.survivor");
    private static readonly EntityId TerminalId = new("interaction.service_terminal");
    private static readonly EntityId AirlockId = new("interaction.evacuation_airlock");
    private static readonly DialogueResponseId SurvivorResponseId = new("response.survivor_acknowledged");

    [Fact]
    public void AuthoredContentParsesWithTheExpectedStableFlow()
    {
        var definition = LoadDefinition();

        Assert.Equal(StationRouteContent.SupportedSchemaVersion, definition.SchemaVersion);
        Assert.Equal("station-route-v1", definition.ContentRevision);
        Assert.Equal(new ScenarioId("scenario.station_route"), definition.ScenarioId);
        Assert.Equal(ProtagonistId, definition.Protagonist.Id);
        Assert.Equal(new ObjectiveId("objective.speak_to_survivor"), definition.BriefingObjective.Id);
        Assert.Equal(
            new ObjectiveId("objective.reach_evacuation_airlock"),
            definition.DestinationObjective.Id);
        Assert.Equal(3, definition.Interactions.Count);

        var survivor = Assert.Single(
            definition.Interactions,
            interaction => interaction.Id == SurvivorId);
        Assert.Equal(StationInteractionEffect.BeginBriefingDialogue, survivor.Effect);
        Assert.Equal(SurvivorResponseId, survivor.Dialogue!.Response.Id);
    }

    [Fact]
    public void ContentParserRejectsUnsupportedAndUnmappedSchemaData()
    {
        var json = LoadContentJson();
        var unsupported = json.Replace(
            "\"schema_version\": 1",
            "\"schema_version\": 2",
            StringComparison.Ordinal);
        var unmapped = json.Replace(
            "\"schema_version\": 1,",
            "\"schema_version\": 1, \"unexpected\": true,",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => StationRouteContent.ParseJson(unsupported));
        Assert.Throws<InvalidDataException>(() => StationRouteContent.ParseJson(unmapped));
    }

    [Fact]
    public void ContentParserRejectsNullInteractionEntriesAsInvalidData()
    {
        var json = LoadContentJson().Replace(
            "\"interactions\": [",
            "\"interactions\": [null,",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => StationRouteContent.ParseJson(json));
    }

    [Fact]
    public void FactoryRejectsLayoutThatDoesNotMatchEveryStableInteractionId()
    {
        var definition = LoadDefinition();
        var incompleteLayout = new StationRouteLayout(
            new WorldPosition(0, 0, 0),
            CreatePlacements().Where(placement => placement.InteractionId != AirlockId));

        Assert.Throws<InvalidDataException>(
            () => GameSession.CreateStationRoute(
                definition,
                incompleteLayout,
                new DirectPathfinder()));
    }

    [Fact]
    public void MovementChangesPositionOnlyOnTicksAndSnapsToItsAcceptedDestination()
    {
        var session = CreateSession();
        var destination = new WorldPosition(4, 0, 0);

        var acknowledgement = session.Execute(
            new MoveActorCommand(new CommandId("move.direct"), ProtagonistId, destination));

        Assert.True(acknowledgement.Accepted);
        Assert.Equal(new WorldPosition(0, 0, 0), ObserveStation(session).Protagonist.Position);

        session.AdvanceTicks(29);
        var beforeArrival = ObserveStation(session);
        Assert.NotNull(beforeArrival.Protagonist.CurrentAction);
        Assert.InRange(beforeArrival.Protagonist.Position.X, 3.86, 3.87);

        session.AdvanceTicks(1);
        var arrived = ObserveStation(session);
        Assert.Equal(destination, arrived.Protagonist.Position);
        Assert.Null(arrived.Protagonist.CurrentAction);
        Assert.Single(
            session.EventsSince(0),
            gameEvent => gameEvent.Detail is MovementArrivedEventDetail
            {
                CommandId.Value: "move.direct",
            });
    }

    [Fact]
    public void PausedOrdersReplaceOnlyPendingAndInvalidPathDoesNotMutateEitherAction()
    {
        var pathfinder = new ConditionalPathfinder(
            destination => destination.X == 99
                ? SpatialPathResult.Unreachable
                : SpatialPathResult.Reachable([destination]));
        var session = CreateSession(pathfinder);
        var first = session.Execute(
            new MoveActorCommand(
                new CommandId("move.first"),
                ProtagonistId,
                new WorldPosition(3, 0, 0)));
        Assert.True(first.Accepted);
        session.AdvanceTicks(1);
        var positionWhenPaused = ObserveStation(session).Protagonist.Position;
        session.Execute(new SetPauseCommand(new CommandId("pause.on"), Paused: true));

        var pending = session.Execute(
            new MoveActorCommand(
                new CommandId("move.pending"),
                ProtagonistId,
                new WorldPosition(0, 0, 2)));
        Assert.True(pending.Accepted);
        Assert.Equal(
            new CommandId("move.first"),
            ObserveStation(session).Protagonist.CurrentAction!.CommandId);
        Assert.Equal(
            new CommandId("move.pending"),
            ObserveStation(session).Protagonist.PendingAction!.CommandId);

        var invalid = session.Execute(
            new MoveActorCommand(
                new CommandId("move.invalid"),
                ProtagonistId,
                new WorldPosition(99, 0, 0)));
        Assert.False(invalid.Accepted);
        Assert.Equal(CommandRejectionCode.DestinationUnreachable, invalid.RejectionCode);
        Assert.Equal(
            new CommandId("move.first"),
            ObserveStation(session).Protagonist.CurrentAction!.CommandId);
        Assert.Equal(
            new CommandId("move.pending"),
            ObserveStation(session).Protagonist.PendingAction!.CommandId);
        Assert.Equal(positionWhenPaused, ObserveStation(session).Protagonist.Position);

        var replacement = session.Execute(
            new MoveActorCommand(
                new CommandId("move.replacement"),
                ProtagonistId,
                new WorldPosition(0, 0, 3)));
        Assert.True(replacement.Accepted);
        Assert.Equal(0, session.AdvanceTicks(10));
        Assert.Equal(positionWhenPaused, ObserveStation(session).Protagonist.Position);
        Assert.Equal(
            new CommandId("move.first"),
            ObserveStation(session).Protagonist.CurrentAction!.CommandId);

        Assert.Equal(1, session.StepWhilePaused(1));
        var stepped = ObserveStation(session);
        Assert.True(session.Observe().Paused);
        Assert.Equal(new CommandId("move.replacement"), stepped.Protagonist.CurrentAction!.CommandId);
        Assert.Null(stepped.Protagonist.PendingAction);
        Assert.InRange(stepped.Protagonist.Position.Z, 0.13, 0.14);
    }

    [Fact]
    public void SurvivorDialogueRequiresItsTypedResponseBeforeObjectiveAdvances()
    {
        var session = CreateSession();
        var interaction = session.Execute(
            new InteractCommand(new CommandId("interact.survivor"), ProtagonistId, SurvivorId));
        Assert.True(interaction.Accepted);

        AdvanceUntil(
            session,
            observation => observation.ActiveDialogue is not null,
            maximumTicks: 60);
        var dialogueStarted = ObserveStation(session);
        Assert.Equal(
            new ObjectiveId("objective.speak_to_survivor"),
            dialogueStarted.Objective.Id);
        Assert.Equal(SurvivorId, dialogueStarted.ActiveDialogue!.InteractionId);
        Assert.Equal(SurvivorResponseId, dialogueStarted.ActiveDialogue.Response.Id);

        var movementWhileTalking = session.Execute(
            new MoveActorCommand(
                new CommandId("move.during_dialogue"),
                ProtagonistId,
                new WorldPosition(2, 0, 2)));
        Assert.False(movementWhileTalking.Accepted);
        Assert.Equal(CommandRejectionCode.DialogueActive, movementWhileTalking.RejectionCode);

        var wrongResponse = session.Execute(
            new ChooseDialogueResponseCommand(
                new CommandId("dialogue.wrong"),
                ProtagonistId,
                SurvivorId,
                new DialogueResponseId("response.unknown")));
        Assert.False(wrongResponse.Accepted);
        Assert.Equal(CommandRejectionCode.UnknownDialogueResponse, wrongResponse.RejectionCode);
        Assert.NotNull(ObserveStation(session).ActiveDialogue);

        var response = session.Execute(
            new ChooseDialogueResponseCommand(
                new CommandId("dialogue.accept"),
                ProtagonistId,
                SurvivorId,
                SurvivorResponseId));
        Assert.True(response.Accepted);

        var advanced = ObserveStation(session);
        Assert.Null(advanced.ActiveDialogue);
        Assert.Equal(
            new ObjectiveId("objective.reach_evacuation_airlock"),
            advanced.Objective.Id);
        Assert.Equal(InteractionState.Completed, FindInteraction(advanced, SurvivorId).State);
        Assert.Equal(InteractionState.Available, FindInteraction(advanced, AirlockId).State);
    }

    [Fact]
    public void OptionalTerminalIsRepeatSafeAndDoesNotAdvanceTheObjective()
    {
        var session = CreateSession();

        CompleteInteraction(session, TerminalId, "terminal.first");
        var afterFirst = ObserveStation(session);
        Assert.Equal(InteractionState.Completed, FindInteraction(afterFirst, TerminalId).State);
        Assert.True(FindInteraction(afterFirst, TerminalId).CanInteract);
        Assert.Equal(
            new ObjectiveId("objective.speak_to_survivor"),
            afterFirst.Objective.Id);

        CompleteInteraction(session, TerminalId, "terminal.repeat");
        var afterRepeat = ObserveStation(session);
        Assert.Equal(InteractionState.Completed, FindInteraction(afterRepeat, TerminalId).State);
        Assert.True(FindInteraction(afterRepeat, TerminalId).CanInteract);
        Assert.Equal(ScenarioPhase.InProgress, afterRepeat.Phase);
        Assert.Equal(
            new ObjectiveId("objective.speak_to_survivor"),
            afterRepeat.Objective.Id);
    }

    [Fact]
    public void AuthoredCriticalPathUnlocksAndCompletesOnlyThroughTheAirlockInteraction()
    {
        var session = CreateSession();

        var lockedAirlock = session.Execute(
            new InteractCommand(new CommandId("airlock.locked"), ProtagonistId, AirlockId));
        Assert.False(lockedAirlock.Accepted);
        Assert.Equal(CommandRejectionCode.InteractionUnavailable, lockedAirlock.RejectionCode);
        Assert.Null(ObserveStation(session).Protagonist.CurrentAction);

        var survivor = session.Execute(
            new InteractCommand(new CommandId("survivor.approach"), ProtagonistId, SurvivorId));
        Assert.True(survivor.Accepted);
        AdvanceUntil(session, observation => observation.ActiveDialogue is not null, 60);
        var response = session.Execute(
            new ChooseDialogueResponseCommand(
                new CommandId("survivor.response"),
                ProtagonistId,
                SurvivorId,
                SurvivorResponseId));
        Assert.True(response.Accepted);

        CompleteInteraction(session, TerminalId, "terminal.optional");
        CompleteInteraction(session, AirlockId, "airlock.complete");

        var final = ObserveStation(session);
        Assert.Equal(ScenarioPhase.Completed, final.Phase);
        Assert.Equal(ObjectiveStatus.Completed, final.Objective.Status);
        Assert.Equal(InteractionState.Completed, FindInteraction(final, SurvivorId).State);
        Assert.Equal(InteractionState.Completed, FindInteraction(final, TerminalId).State);
        Assert.Equal(InteractionState.Completed, FindInteraction(final, AirlockId).State);
        Assert.Null(final.Protagonist.CurrentAction);
        Assert.Null(final.Protagonist.PendingAction);

        var events = session.EventsSince(0);
        Assert.Single(events, gameEvent => gameEvent.Type == GameplayEventType.DialogueStarted);
        Assert.Single(events, gameEvent => gameEvent.Type == GameplayEventType.DialogueResponseChosen);
        Assert.Single(events, gameEvent => gameEvent.Type == GameplayEventType.ObjectiveChanged
            && gameEvent.Detail is ObjectiveChangedEventDetail { Status: ObjectiveStatus.Active });
        Assert.Single(events, gameEvent => gameEvent.Type == GameplayEventType.ObjectiveChanged
            && gameEvent.Detail is ObjectiveChangedEventDetail { Status: ObjectiveStatus.Completed });
        Assert.Single(events, gameEvent => gameEvent.Type == GameplayEventType.ScenarioCompleted);

        var afterCompletion = session.Execute(
            new MoveActorCommand(
                new CommandId("move.after_completion"),
                ProtagonistId,
                new WorldPosition(0, 0, 0)));
        Assert.False(afterCompletion.Accepted);
        Assert.Equal(CommandRejectionCode.ScenarioCompleted, afterCompletion.RejectionCode);
        Assert.Single(
            session.EventsSince(0),
            gameEvent => gameEvent.Type == GameplayEventType.ScenarioCompleted);
    }

    [Fact]
    public void CriticalPathCompletesWithoutUsingTheOptionalServiceTerminal()
    {
        var session = CreateSession();

        var survivor = session.Execute(
            new InteractCommand(new CommandId("survivor.required"), ProtagonistId, SurvivorId));
        Assert.True(survivor.Accepted);
        AdvanceUntil(session, observation => observation.ActiveDialogue is not null, 60);

        var response = session.Execute(
            new ChooseDialogueResponseCommand(
                new CommandId("survivor.required.response"),
                ProtagonistId,
                SurvivorId,
                SurvivorResponseId));
        Assert.True(response.Accepted);

        var beforeAirlock = FindInteraction(ObserveStation(session), TerminalId);
        Assert.Equal(InteractionState.Available, beforeAirlock.State);
        Assert.True(beforeAirlock.CanInteract);
        Assert.Null(beforeAirlock.ResultText);

        CompleteInteraction(session, AirlockId, "airlock.without_terminal");

        var final = ObserveStation(session);
        Assert.Equal(ScenarioPhase.Completed, final.Phase);
        Assert.Equal(ObjectiveStatus.Completed, final.Objective.Status);

        var terminal = FindInteraction(final, TerminalId);
        Assert.NotEqual(InteractionState.Completed, terminal.State);
        Assert.Null(terminal.ResultText);
        Assert.DoesNotContain(
            session.EventsSince(0),
            gameEvent => gameEvent.Detail is InteractionCompletedEventDetail detail
                && detail.InteractionId == TerminalId);
    }

    private static GameSession CreateSession(ISpatialPathfinder? pathfinder = null)
    {
        return GameSession.CreateStationRoute(
            LoadDefinition(),
            new StationRouteLayout(new WorldPosition(0, 0, 0), CreatePlacements()),
            pathfinder ?? new DirectPathfinder());
    }

    private static StationInteractionPlacement[] CreatePlacements()
    {
        return
        [
            new StationInteractionPlacement(
                SurvivorId,
                new WorldPosition(4, 0, 0),
                new WorldPosition(3, 0, 0)),
            new StationInteractionPlacement(
                TerminalId,
                new WorldPosition(6, 0, 2),
                new WorldPosition(5, 0, 2)),
            new StationInteractionPlacement(
                AirlockId,
                new WorldPosition(10, 0, 0),
                new WorldPosition(9, 0, 0)),
        ];
    }

    private static StationRouteDefinition LoadDefinition()
    {
        return StationRouteContent.ParseJson(LoadContentJson());
    }

    private static string LoadContentJson()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "content", "station-route.json");
        return File.ReadAllText(path);
    }

    private static StationRouteObservation ObserveStation(GameSession session)
    {
        return Assert.IsType<StationRouteObservation>(session.Observe().StationRoute);
    }

    private static InteractionObservation FindInteraction(
        StationRouteObservation observation,
        EntityId interactionId)
    {
        return Assert.Single(
            observation.Interactions,
            interaction => interaction.Id == interactionId);
    }

    private static void CompleteInteraction(
        GameSession session,
        EntityId interactionId,
        string commandId)
    {
        var beforeSequence = session.Observe().LatestEventSequence;
        var acknowledgement = session.Execute(
            new InteractCommand(new CommandId(commandId), ProtagonistId, interactionId));
        Assert.True(acknowledgement.Accepted);
        AdvanceUntil(
            session,
            _ => session.EventsSince(beforeSequence).Any(
                gameEvent => gameEvent.Detail is InteractionCompletedEventDetail detail
                    && detail.InteractionId == interactionId),
            maximumTicks: 120);
    }

    private static void AdvanceUntil(
        GameSession session,
        Func<StationRouteObservation, bool> condition,
        int maximumTicks)
    {
        for (var index = 0; index < maximumTicks; index++)
        {
            if (condition(ObserveStation(session)))
            {
                return;
            }

            session.AdvanceTicks(1);
        }

        Assert.Fail($"Condition was not reached within {maximumTicks} ticks.");
    }

    private sealed class DirectPathfinder : ISpatialPathfinder
    {
        public SpatialPathResult FindPath(
            EntityId actorId,
            WorldPosition origin,
            WorldPosition destination)
        {
            _ = actorId;
            _ = origin;
            return SpatialPathResult.Reachable([destination]);
        }
    }

    private sealed class ConditionalPathfinder(
        Func<WorldPosition, SpatialPathResult> findPath) : ISpatialPathfinder
    {
        public SpatialPathResult FindPath(
            EntityId actorId,
            WorldPosition origin,
            WorldPosition destination)
        {
            _ = actorId;
            _ = origin;
            return findPath(destination);
        }
    }
}
