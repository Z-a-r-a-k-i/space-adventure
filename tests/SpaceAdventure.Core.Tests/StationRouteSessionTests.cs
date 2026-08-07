using System.Text.Json.Nodes;
using SpaceAdventure.Core;
using Xunit;

namespace SpaceAdventure.Core.Tests;

public sealed class StationRouteSessionTests
{
    private static readonly EntityId ProtagonistId = new("actor.protagonist");
    private static readonly EntityId ProtectorActorId = new("actor.companion.protector");
    private static readonly EntityId SurvivorId = new("interaction.survivor");
    private static readonly EntityId EntryDoorId = new("interaction.service_door.entry");
    private static readonly EntityId SoloExitDoorId = new("interaction.service_door.solo_exit");
    private static readonly EntityId ProtectorInteractionId = new("interaction.protector");
    private static readonly EntityId TerminalId = new("interaction.service_terminal");
    private static readonly EntityId AirlockId = new("interaction.evacuation_airlock");
    private static readonly ProtagonistKitId VanguardKitId = new("kit.protagonist.vanguard");
    private static readonly DialogueResponseId ServicePowerResponseId = new("response.reroute_service_power");

    [Fact]
    public void SchemaThreeContentParsesWithExactDoorEffectsAndStableIdentities()
    {
        var definition = LoadDefinition();

        Assert.Equal(3, definition.SchemaVersion);
        Assert.Equal("station-route-v5", definition.ContentRevision);
        Assert.Equal(new ScenarioId("scenario.station_route"), definition.ScenarioId);
        Assert.Equal(ProtagonistId, definition.Protagonist.Id);
        Assert.Equal(ProtectorActorId, definition.Companion.Id);
        Assert.Equal(2.0, definition.Companion.MovementSpeedMetersPerSecond);
        Assert.Equal(new ObjectiveId("objective.open_entry_service_door"), definition.EntryDoorObjective.Id);
        Assert.Equal(new ObjectiveId("objective.reach_first_combat"), definition.CombatThresholdObjective.Id);
        Assert.Equal(new AttackId("attack.crew.protector.shotgun"), definition.Companion.Loadout!.BasicAttackId);
        Assert.Equal(new AbilityId("ability.crew.protector.guard_ally"), definition.Companion.Loadout.ActiveAbilityId);
        Assert.Equal(AbilityTargetKind.Ally, definition.Companion.Loadout.ActiveAbilityTargetKind);

        var vanguard = Assert.Single(definition.ProtagonistKits);
        Assert.Equal(VanguardKitId, vanguard.Id);
        Assert.Equal(new AttackId("attack.crew.vanguard.carbine"), vanguard.BasicAttackId);
        Assert.Equal(new AbilityId("ability.crew.vanguard.suppressive_fire"), vanguard.ActiveAbilityId);
        Assert.Equal(6, definition.Interactions.Count);
        Assert.Single(definition.Interactions, interaction =>
            interaction.Effect == StationInteractionEffect.OpenEntryServiceDoor);
        Assert.Single(definition.Interactions, interaction =>
            interaction.Effect == StationInteractionEffect.OpenSoloExitServiceDoor);

        var survivor = Assert.Single(definition.Interactions, interaction => interaction.Id == SurvivorId);
        Assert.Equal(StationInteractionEffect.BeginSurvivorDialogue, survivor.Effect);
        Assert.Equal(2, survivor.Dialogue!.Responses.Count);
    }

    [Fact]
    public void ContentParserRejectsUnsupportedAndUnmappedSchemaData()
    {
        var json = LoadContentJson();
        var unsupported = json.Replace(
            "\"schema_version\": 3",
            "\"schema_version\": 99",
            StringComparison.Ordinal);
        var unmapped = json.Replace(
            "\"schema_version\": 3,",
            "\"schema_version\": 3, \"unexpected\": true,",
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => StationRouteContent.ParseJson(unsupported));
        Assert.Throws<InvalidDataException>(() => StationRouteContent.ParseJson(unmapped));
    }

    [Theory]
    [InlineData("attack.crew.protector.shotgun", "attack.crew.vanguard.carbine")]
    [InlineData("ability.crew.protector.guard_ally", "ability.crew.vanguard.suppressive_fire")]
    public void ContentParserRejectsCompanionLoadoutIdentifiersUsedByAProtagonistKit(
        string companionIdentifier,
        string protagonistIdentifier)
    {
        var invalid = LoadContentJson().Replace(
            companionIdentifier,
            protagonistIdentifier,
            StringComparison.Ordinal);

        Assert.Throws<InvalidDataException>(() => StationRouteContent.ParseJson(invalid));
    }

    [Fact]
    public void ContentParserReportsTheInvalidDialogueResponseIndex()
    {
        var invalid = LoadContentJson().Replace(
            "\"id\": \"response.reroute_service_power\"",
            "\"id\": \"\"",
            StringComparison.Ordinal);

        var exception = Assert.Throws<InvalidDataException>(
            () => StationRouteContent.ParseJson(invalid));

        Assert.Contains("responses[0].id", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ContentParserAcceptsOneOrTwoKitsAndRejectsInvalidKitCounts()
    {
        var sourceJson = LoadContentJson();
        var sourceRoot = JsonNode.Parse(sourceJson)!.AsObject();
        var firstKit = sourceRoot["protagonist_kits"]!.AsArray()[0]!.DeepClone();
        var secondKit = firstKit.DeepClone().AsObject();
        secondKit["id"] = "kit.protagonist.test-secondary";
        secondKit["basic_attack_id"] = "attack.crew.test-secondary";
        secondKit["active_ability_id"] = "ability.crew.test-secondary";
        var thirdKit = secondKit.DeepClone().AsObject();
        thirdKit["id"] = "kit.protagonist.test-tertiary";
        thirdKit["basic_attack_id"] = "attack.crew.test-tertiary";
        thirdKit["active_ability_id"] = "ability.crew.test-tertiary";

        string WithKits(params JsonNode?[] kitEntries)
        {
            var root = JsonNode.Parse(sourceJson)!.AsObject();
            var kits = new JsonArray();
            foreach (var entry in kitEntries)
            {
                kits.Add(entry?.DeepClone());
            }
            root["protagonist_kits"] = kits;
            return root.ToJsonString();
        }

        Assert.Single(StationRouteContent.ParseJson(WithKits(firstKit)).ProtagonistKits);
        Assert.Equal(2, StationRouteContent.ParseJson(WithKits(firstKit, secondKit)).ProtagonistKits.Count);
        Assert.Throws<InvalidDataException>(() => StationRouteContent.ParseJson(WithKits()));
        Assert.Throws<InvalidDataException>(() => StationRouteContent.ParseJson(
            WithKits(firstKit, secondKit, thirdKit)));
        Assert.Throws<InvalidDataException>(() => StationRouteContent.ParseJson(
            WithKits(firstKit, firstKit)));
        Assert.Throws<InvalidDataException>(() => StationRouteContent.ParseJson(
            WithKits((JsonNode?)null)));
    }

    [Fact]
    public void FactoryRequiresEveryInteractionAndTheCompanionPlacement()
    {
        var definition = LoadDefinition();
        var missingInteraction = new StationRouteLayout(
            new WorldPosition(-10, 0, 8.5),
            CreateActorPlacements(),
            CreateInteractionPlacements().Where(placement => placement.InteractionId != AirlockId));
        var missingCompanion = new StationRouteLayout(
            new WorldPosition(-10, 0, 8.5),
            [],
            CreateInteractionPlacements());

        Assert.Throws<InvalidDataException>(() => GameSession.CreateStationRoute(
            definition,
            missingInteraction,
            new DirectPathfinder()));
        Assert.Throws<InvalidDataException>(() => GameSession.CreateStationRoute(
            definition,
            missingCompanion,
            new DirectPathfinder()));
    }

    [Fact]
    public void CoreRequiresExplicitKitSelectionAndAllowsItOnlyOnce()
    {
        var session = CreateSession();
        var initial = ObserveStation(session);

        Assert.Equal(ScenarioPhase.AwaitingProtagonistSelection, initial.Phase);
        Assert.Null(initial.SelectedProtagonistKit);
        Assert.Single(initial.Party);
        Assert.Null(initial.Protagonist.Loadout);

        var beforeSelection = session.Execute(new MoveActorCommand(
            new CommandId("move.before-kit"),
            ProtagonistId,
            new WorldPosition(-10, 0, 7)));
        Assert.False(beforeSelection.Accepted);
        Assert.Equal(CommandRejectionCode.ProtagonistKitRequired, beforeSelection.RejectionCode);

        var selected = SelectVanguard(session);
        Assert.True(selected.Accepted);
        var afterSelection = ObserveStation(session);
        Assert.Equal(ScenarioPhase.InProgress, afterSelection.Phase);
        Assert.Equal(VanguardKitId, afterSelection.SelectedProtagonistKit!.Id);
        Assert.Equal("Vanguard", afterSelection.Protagonist.DisplayName);

        var replacement = session.Execute(new ChooseProtagonistKitCommand(
            new CommandId("kit.replace"),
            VanguardKitId));
        Assert.False(replacement.Accepted);
        Assert.Equal(CommandRejectionCode.ProtagonistKitAlreadySelected, replacement.RejectionCode);
    }

    [Fact]
    public void MovementChangesPositionOnlyOnTicksAndSnapsToAcceptedDestination()
    {
        var session = CreateSession();
        _ = SelectVanguard(session);
        var destination = new WorldPosition(-10, 0, 4.5);

        var acknowledgement = session.Execute(
            new MoveActorCommand(new CommandId("move.direct"), ProtagonistId, destination));

        Assert.True(acknowledgement.Accepted);
        Assert.Equal(new WorldPosition(-10, 0, 8.5), ObserveStation(session).Protagonist.Position);
        Assert.True(ObserveStation(session).Protagonist.CurrentAction!.HasRemainingMovement);
        session.AdvanceTicks(59);
        Assert.InRange(ObserveStation(session).Protagonist.Position.Z, 4.56, 4.57);
        session.AdvanceTicks(1);

        var arrived = ObserveStation(session);
        Assert.Equal(destination, arrived.Protagonist.Position);
        Assert.Null(arrived.Protagonist.CurrentAction);
    }

    [Fact]
    public void PausedOrdersReplaceOnlyPendingAndInvalidPathMutatesNothing()
    {
        var pathfinder = new ConditionalPathfinder(
            (_, destination) => destination.X == 99
                ? SpatialPathResult.Unreachable
                : SpatialPathResult.Reachable([destination]));
        var session = CreateSession(pathfinder);
        _ = SelectVanguard(session);
        Assert.True(session.Execute(new MoveActorCommand(
            new CommandId("move.first"),
            ProtagonistId,
            new WorldPosition(-10, 0, 7))).Accepted);
        session.AdvanceTicks(1);
        var positionWhenPaused = ObserveStation(session).Protagonist.Position;
        _ = session.Execute(new SetPauseCommand(new CommandId("pause.on"), Paused: true));
        Assert.True(session.Execute(new MoveActorCommand(
            new CommandId("move.pending"),
            ProtagonistId,
            new WorldPosition(-9, 0, 7))).Accepted);

        var invalid = session.Execute(new MoveActorCommand(
            new CommandId("move.invalid"),
            ProtagonistId,
            new WorldPosition(99, 0, 0)));
        Assert.False(invalid.Accepted);
        Assert.Equal(CommandRejectionCode.DestinationUnreachable, invalid.RejectionCode);
        Assert.Equal(new CommandId("move.first"), ObserveStation(session).Protagonist.CurrentAction!.CommandId);
        Assert.Equal(new CommandId("move.pending"), ObserveStation(session).Protagonist.PendingAction!.CommandId);
        Assert.Equal(positionWhenPaused, ObserveStation(session).Protagonist.Position);

        Assert.True(session.Execute(new MoveActorCommand(
            new CommandId("move.replacement"),
            ProtagonistId,
            new WorldPosition(-11, 0, 7))).Accepted);
        Assert.Equal(1, session.StepWhilePaused(1));
        var stepped = ObserveStation(session);
        Assert.Equal(new CommandId("move.replacement"), stepped.Protagonist.CurrentAction!.CommandId);
        Assert.Null(stepped.Protagonist.PendingAction);
    }

    [Theory]
    [InlineData("response.reroute_service_power", RoutePowerMode.ServiceRerouted)]
    [InlineData("response.preserve_shelter_power", RoutePowerMode.ShelterPreserved)]
    public void BothSurvivorChoicesUnlockEntryDoorAndChangeTerminalResult(
        string responseId,
        RoutePowerMode expectedPowerMode)
    {
        var session = CreateSession();
        _ = SelectVanguard(session);
        StartDialogue(session, SurvivorId, "survivor.start");

        var response = session.Execute(new ChooseDialogueResponseCommand(
            new CommandId("survivor.choice"),
            ProtagonistId,
            SurvivorId,
            new DialogueResponseId(responseId)));
        Assert.True(response.Accepted);

        var afterChoice = ObserveStation(session);
        Assert.Equal(expectedPowerMode, afterChoice.RoutePowerMode);
        Assert.Equal(new ObjectiveId("objective.open_entry_service_door"), afterChoice.Objective.Id);
        Assert.Equal(InteractionState.Available, FindInteraction(afterChoice, EntryDoorId).State);
        Assert.Equal(InteractionState.Available, FindInteraction(afterChoice, TerminalId).State);
        Assert.Equal(InteractionState.Unavailable, FindInteraction(afterChoice, SoloExitDoorId).State);
        Assert.Equal(InteractionState.Unavailable, FindInteraction(afterChoice, ProtectorInteractionId).State);
        Assert.Equal(InteractionState.Unavailable, FindInteraction(afterChoice, AirlockId).State);

        CompleteInteraction(session, TerminalId, "terminal.inspect");
        var terminalResult = FindInteraction(ObserveStation(session), TerminalId).ResultText;
        Assert.NotNull(terminalResult);
        Assert.Contains(
            expectedPowerMode == RoutePowerMode.ServiceRerouted
                ? "full service-corridor power"
                : "stable shelter seals",
            terminalResult,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EntryDoorIsUnavailableBeforeDialogueAndCompletesAtomicallyAfterward()
    {
        var session = CreateSession();
        _ = SelectVanguard(session);

        var locked = session.Execute(new InteractCommand(
            new CommandId("entry.locked"),
            ProtagonistId,
            EntryDoorId));
        Assert.False(locked.Accepted);
        Assert.Equal(CommandRejectionCode.InteractionUnavailable, locked.RejectionCode);
        Assert.Equal(InteractionState.Unavailable, FindInteraction(ObserveStation(session), EntryDoorId).State);

        ChooseServicePower(session);
        var sequenceBeforeDoor = session.Observe().LatestEventSequence;
        CompleteInteraction(session, EntryDoorId, "entry.open");

        var afterDoor = ObserveStation(session);
        Assert.Equal(InteractionState.Completed, FindInteraction(afterDoor, EntryDoorId).State);
        Assert.Equal(new ObjectiveId("objective.reach_first_combat"), afterDoor.Objective.Id);
        Assert.Equal(ScenarioPhase.InProgress, afterDoor.Phase);
        var doorEvents = session.EventsSince(sequenceBeforeDoor);
        Assert.Contains(doorEvents, gameEvent =>
            gameEvent.Detail is InteractionCompletedEventDetail detail
                && detail.InteractionId == EntryDoorId
                && detail.Effect == StationInteractionEffect.OpenEntryServiceDoor);
        Assert.Contains(doorEvents, gameEvent =>
            gameEvent.Detail is ObjectiveChangedEventDetail detail
                && detail.CurrentObjectiveId == new ObjectiveId("objective.reach_first_combat"));
        Assert.DoesNotContain(doorEvents, gameEvent => gameEvent.Type == GameplayEventType.ScenarioCompleted);
    }

    [Fact]
    public void MovingThroughAnUnlockedServiceDoorOpensItBeforeArrival()
    {
        var session = CreateSession();
        _ = SelectVanguard(session);
        ChooseServicePower(session);

        var arenaDestination = new WorldPosition(-10, 0, 0);
        var sequenceBeforeMove = session.Observe().LatestEventSequence;
        var move = session.Execute(new MoveActorCommand(
            new CommandId("entry.auto-open"),
            ProtagonistId,
            arenaDestination));
        Assert.True(move.Accepted);

        AdvanceUntil(
            session,
            observation => FindInteraction(observation, EntryDoorId).State
                == InteractionState.Completed,
            300);

        var afterOpen = ObserveStation(session);
        Assert.NotNull(afterOpen.Protagonist.CurrentAction);
        Assert.NotEqual(arenaDestination, afterOpen.Protagonist.Position);
        Assert.Equal(new ObjectiveId("objective.reach_first_combat"), afterOpen.Objective.Id);
        var openingEvents = session.EventsSince(sequenceBeforeMove);
        Assert.Contains(openingEvents, gameEvent =>
            gameEvent.Detail is InteractionCompletedEventDetail detail
                && detail.InteractionId == EntryDoorId
                && detail.Effect == StationInteractionEffect.OpenEntryServiceDoor);
        Assert.Contains(openingEvents, gameEvent =>
            gameEvent.Detail is ObjectiveChangedEventDetail detail
                && detail.CurrentObjectiveId == new ObjectiveId("objective.reach_first_combat"));
        Assert.DoesNotContain(
            openingEvents,
            gameEvent => gameEvent.Type == GameplayEventType.MovementArrived);

        AdvanceUntil(
            session,
            observation => observation.Protagonist.Position == arenaDestination
                && observation.Protagonist.CurrentAction is null,
            600);
    }

    [Fact]
    public void CombatThresholdLeavesSoloExitProtectorAndAirlockUnavailable()
    {
        var session = CreateSession();
        _ = SelectVanguard(session);
        ChooseServicePower(session);
        CompleteInteraction(session, EntryDoorId, "entry.open");

        var atThreshold = ObserveStation(session);
        Assert.Equal(new ObjectiveId("objective.reach_first_combat"), atThreshold.Objective.Id);
        Assert.Single(atThreshold.Party);
        Assert.Equal(InteractionState.Unavailable, FindInteraction(atThreshold, SoloExitDoorId).State);
        Assert.Equal(InteractionState.Unavailable, FindInteraction(atThreshold, ProtectorInteractionId).State);
        Assert.Equal(InteractionState.Unavailable, FindInteraction(atThreshold, AirlockId).State);

        foreach (var interactionId in new[] { SoloExitDoorId, ProtectorInteractionId, AirlockId })
        {
            var rejected = session.Execute(new InteractCommand(
                new CommandId($"locked.{interactionId.Value}"),
                ProtagonistId,
                interactionId));
            Assert.False(rejected.Accepted);
            Assert.Equal(CommandRejectionCode.InteractionUnavailable, rejected.RejectionCode);
        }
        Assert.Equal(ScenarioPhase.InProgress, ObserveStation(session).Phase);
    }

    private static CommandAcknowledgement SelectVanguard(GameSession session)
    {
        return session.Execute(new ChooseProtagonistKitCommand(
            new CommandId("kit.select-vanguard"),
            VanguardKitId));
    }

    private static void ChooseServicePower(GameSession session)
    {
        StartDialogue(session, SurvivorId, "survivor.start");
        Assert.True(session.Execute(new ChooseDialogueResponseCommand(
            new CommandId("survivor.choose-power"),
            ProtagonistId,
            SurvivorId,
            ServicePowerResponseId)).Accepted);
    }

    private static void StartDialogue(GameSession session, EntityId interactionId, string commandId)
    {
        Assert.True(session.Execute(new InteractCommand(
            new CommandId(commandId),
            ProtagonistId,
            interactionId)).Accepted);
        AdvanceUntil(session, observation => observation.ActiveDialogue?.InteractionId == interactionId, 300);
    }

    private static void CompleteInteraction(GameSession session, EntityId interactionId, string commandId)
    {
        var beforeSequence = session.Observe().LatestEventSequence;
        Assert.True(session.Execute(new InteractCommand(
            new CommandId(commandId),
            ProtagonistId,
            interactionId)).Accepted);
        AdvanceUntil(
            session,
            _ => session.EventsSince(beforeSequence).Any(gameEvent =>
                gameEvent.Detail is InteractionCompletedEventDetail detail
                    && detail.InteractionId == interactionId),
            600);
    }

    private static GameSession CreateSession(ISpatialPathfinder? pathfinder = null)
    {
        // Keep this minimal layout in the core-test assembly: production core must
        // not depend on the SimCli development fixture merely to share coordinates.
        return GameSession.CreateStationRoute(
            LoadDefinition(),
            new StationRouteLayout(
                new WorldPosition(-10, 0, 8.5),
                CreateActorPlacements(),
                CreateInteractionPlacements()),
            pathfinder ?? new DirectPathfinder());
    }

    private static StationActorPlacement[] CreateActorPlacements()
    {
        return [new StationActorPlacement(ProtectorActorId, new WorldPosition(-1.5, 0, 0))];
    }

    private static StationInteractionPlacement[] CreateInteractionPlacements()
    {
        return
        [
            new(SurvivorId, new WorldPosition(-8.5, 0, 6.5), new WorldPosition(-9.3, 0, 6.5)),
            new(EntryDoorId, new WorldPosition(-10, 0, 4), new WorldPosition(-10, 0, 4.85)),
            new(SoloExitDoorId, new WorldPosition(-5, 0, 0), new WorldPosition(-5.85, 0, 0)),
            new(ProtectorInteractionId, new WorldPosition(-1.5, 0, 0), new WorldPosition(-2.35, 0, 0)),
            new(TerminalId, new WorldPosition(-11.5, 0, 6.5), new WorldPosition(-10.65, 0, 6.5)),
            new(AirlockId, new WorldPosition(12, 0, 8), new WorldPosition(11.15, 0, 8)),
        ];
    }

    private static StationRouteDefinition LoadDefinition()
    {
        return StationRouteContent.ParseJson(LoadContentJson());
    }

    private static string LoadContentJson()
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "content", "station-route.json"));
    }

    private static StationRouteObservation ObserveStation(GameSession session)
    {
        return Assert.IsType<StationRouteObservation>(session.Observe().StationRoute);
    }

    private static InteractionObservation FindInteraction(
        StationRouteObservation observation,
        EntityId interactionId)
    {
        return Assert.Single(observation.Interactions, interaction => interaction.Id == interactionId);
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
        public SpatialPathResult FindPath(EntityId actorId, WorldPosition origin, WorldPosition destination)
        {
            _ = actorId;
            _ = origin;
            return SpatialPathResult.Reachable([destination]);
        }
    }

    private sealed class ConditionalPathfinder(
        Func<EntityId, WorldPosition, SpatialPathResult> findPath) : ISpatialPathfinder
    {
        public SpatialPathResult FindPath(EntityId actorId, WorldPosition origin, WorldPosition destination)
        {
            _ = origin;
            return findPath(actorId, destination);
        }
    }
}
