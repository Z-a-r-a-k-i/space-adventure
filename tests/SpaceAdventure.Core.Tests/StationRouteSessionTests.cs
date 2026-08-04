using System.Text.Json.Nodes;
using SpaceAdventure.Core;
using Xunit;

namespace SpaceAdventure.Core.Tests;

public sealed class StationRouteSessionTests
{
    private static readonly EntityId ProtagonistId = new("actor.protagonist");
    private static readonly EntityId ProtectorActorId = new("actor.companion.protector");
    private static readonly EntityId SurvivorId = new("interaction.survivor");
    private static readonly EntityId ProtectorInteractionId = new("interaction.protector");
    private static readonly EntityId TerminalId = new("interaction.service_terminal");
    private static readonly EntityId AirlockId = new("interaction.evacuation_airlock");
    private static readonly ProtagonistKitId VanguardKitId = new("kit.protagonist.vanguard");
    private static readonly DialogueResponseId ServicePowerResponseId = new("response.reroute_service_power");
    private static readonly DialogueResponseId ShelterPowerResponseId = new("response.preserve_shelter_power");
    private static readonly DialogueResponseId RecruitResponseId = new("response.recruit_protector");

    [Fact]
    public void AuthoredContentParsesWithStablePartyAndTacticalIdentities()
    {
        var definition = LoadDefinition();

        Assert.Equal(2, definition.SchemaVersion);
        Assert.Equal("station-route-v4", definition.ContentRevision);
        Assert.Equal(new ScenarioId("scenario.station_route"), definition.ScenarioId);
        Assert.Equal(ProtagonistId, definition.Protagonist.Id);
        Assert.Equal(ProtectorActorId, definition.Companion.Id);
        Assert.Equal(new AttackId("attack.crew.protector.shotgun"), definition.Companion.Loadout!.BasicAttackId);
        Assert.Equal(new AbilityId("ability.crew.protector.guard_ally"), definition.Companion.Loadout.ActiveAbilityId);
        Assert.Equal(AbilityTargetKind.Ally, definition.Companion.Loadout.ActiveAbilityTargetKind);
        var vanguard = Assert.Single(definition.ProtagonistKits);
        Assert.Equal(VanguardKitId, vanguard.Id);
        Assert.Equal(new AttackId("attack.crew.vanguard.carbine"), vanguard.BasicAttackId);
        Assert.Equal(
            new AbilityId("ability.crew.vanguard.suppressive_fire"),
            vanguard.ActiveAbilityId);
        Assert.Equal(4, definition.Interactions.Count);

        var survivor = Assert.Single(
            definition.Interactions,
            interaction => interaction.Id == SurvivorId);
        Assert.Equal(StationInteractionEffect.BeginSurvivorDialogue, survivor.Effect);
        Assert.Equal(2, survivor.Dialogue!.Responses.Count);
    }

    [Fact]
    public void ContentParserRejectsUnsupportedAndUnmappedSchemaData()
    {
        var json = LoadContentJson();
        var unsupported = json.Replace(
            "\"schema_version\": 2",
            "\"schema_version\": 99",
            StringComparison.Ordinal);
        var unmapped = json.Replace(
            "\"schema_version\": 2,",
            "\"schema_version\": 2, \"unexpected\": true,",
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
        Assert.Equal(
            2,
            StationRouteContent.ParseJson(WithKits(firstKit, secondKit)).ProtagonistKits.Count);
        Assert.Throws<InvalidDataException>(() => StationRouteContent.ParseJson(WithKits()));
        Assert.Throws<InvalidDataException>(() => StationRouteContent.ParseJson(
            WithKits(firstKit, secondKit, firstKit)));
        Assert.Throws<InvalidDataException>(() => StationRouteContent.ParseJson(
            WithKits((JsonNode?)null)));
    }

    [Fact]
    public void FactoryRequiresEveryInteractionAndTheCompanionPlacement()
    {
        var definition = LoadDefinition();
        var missingInteraction = new StationRouteLayout(
            new WorldPosition(0, 0, 0),
            CreateActorPlacements(),
            CreateInteractionPlacements().Where(placement => placement.InteractionId != AirlockId));
        var missingCompanion = new StationRouteLayout(
            new WorldPosition(0, 0, 0),
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
            new WorldPosition(2, 0, 0)));
        Assert.False(beforeSelection.Accepted);
        Assert.Equal(CommandRejectionCode.ProtagonistKitRequired, beforeSelection.RejectionCode);

        var unknown = session.Execute(new ChooseProtagonistKitCommand(
            new CommandId("kit.unknown"),
            new ProtagonistKitId("kit.protagonist.operator")));
        Assert.False(unknown.Accepted);
        Assert.Equal(CommandRejectionCode.UnknownProtagonistKit, unknown.RejectionCode);

        var selected = SelectVanguard(session);
        Assert.True(selected.Accepted);
        var afterSelection = ObserveStation(session);
        Assert.Equal(ScenarioPhase.InProgress, afterSelection.Phase);
        Assert.Equal(VanguardKitId, afterSelection.SelectedProtagonistKit!.Id);
        Assert.Equal("Vanguard", afterSelection.Protagonist.DisplayName);
        Assert.Equal(
            new AbilityId("ability.crew.vanguard.suppressive_fire"),
            afterSelection.Protagonist.Loadout!.ActiveAbilityId);

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
        var destination = new WorldPosition(4, 0, 0);

        var acknowledgement = session.Execute(
            new MoveActorCommand(new CommandId("move.direct"), ProtagonistId, destination));

        Assert.True(acknowledgement.Accepted);
        Assert.Equal(new WorldPosition(0, 0, 0), ObserveStation(session).Protagonist.Position);
        Assert.True(ObserveStation(session).Protagonist.CurrentAction!.HasRemainingMovement);
        session.AdvanceTicks(59);
        Assert.InRange(ObserveStation(session).Protagonist.Position.X, 3.93, 3.94);
        session.AdvanceTicks(1);

        var arrived = ObserveStation(session);
        Assert.Equal(destination, arrived.Protagonist.Position);
        Assert.Null(arrived.Protagonist.CurrentAction);
        Assert.Single(
            session.EventsSince(0),
            gameEvent => gameEvent.Detail is MovementArrivedEventDetail { CommandId.Value: "move.direct" });
    }

    [Fact]
    public void InteractionWithinUseRadiusDoesNotReportRemainingMovement()
    {
        var session = GameSession.CreateStationRoute(
            LoadDefinition(),
            new StationRouteLayout(
                new WorldPosition(4, 0, 0),
                CreateActorPlacements(),
                CreateInteractionPlacements()),
            new DirectPathfinder());
        _ = SelectVanguard(session);

        var acknowledgement = session.Execute(new InteractCommand(
            new CommandId("survivor.interact-in-range"),
            ProtagonistId,
            SurvivorId));

        Assert.True(acknowledgement.Accepted);
        var action = Assert.IsType<PrimaryActionObservation>(
            ObserveStation(session).Protagonist.CurrentAction);
        Assert.False(action.HasRemainingMovement);
        Assert.Equal(new WorldPosition(3, 0, 0), action.Destination);
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
            new WorldPosition(3, 0, 0))).Accepted);
        session.AdvanceTicks(1);
        var positionWhenPaused = ObserveStation(session).Protagonist.Position;
        _ = session.Execute(new SetPauseCommand(new CommandId("pause.on"), Paused: true));
        Assert.True(session.Execute(new MoveActorCommand(
            new CommandId("move.pending"),
            ProtagonistId,
            new WorldPosition(0, 0, 2))).Accepted);

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
            new WorldPosition(0, 0, 3))).Accepted);
        Assert.Equal(1, session.StepWhilePaused(1));
        var stepped = ObserveStation(session);
        Assert.Equal(new CommandId("move.replacement"), stepped.Protagonist.CurrentAction!.CommandId);
        Assert.Null(stepped.Protagonist.PendingAction);
    }

    [Theory]
    [InlineData("response.reroute_service_power", RoutePowerMode.ServiceRerouted)]
    [InlineData("response.preserve_shelter_power", RoutePowerMode.ShelterPreserved)]
    public void SurvivorChoiceRecordsAConsequenceAndChangesTerminalResult(
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
        Assert.Equal(new ObjectiveId("objective.recruit_protector"), afterChoice.Objective.Id);
        Assert.Equal(InteractionState.Available, FindInteraction(afterChoice, ProtectorInteractionId).State);
        Assert.Equal(InteractionState.Available, FindInteraction(afterChoice, TerminalId).State);
        Assert.Equal(InteractionState.Unavailable, FindInteraction(afterChoice, AirlockId).State);

        CompleteInteraction(session, TerminalId, "terminal.inspect");
        var terminalResult = FindInteraction(ObserveStation(session), TerminalId).ResultText;
        Assert.NotNull(terminalResult);
        Assert.Contains(
            expectedPowerMode == RoutePowerMode.ServiceRerouted ? "full service-corridor power" : "stable shelter seals",
            terminalResult,
            StringComparison.Ordinal);
        Assert.Single(
            session.EventsSince(0),
            gameEvent => gameEvent.Detail is RouteConsequenceSelectedEventDetail detail
                && detail.RoutePowerMode == expectedPowerMode);
        var repeated = session.Execute(new InteractCommand(
            new CommandId("terminal.inspect-again"),
            ProtagonistId,
            TerminalId));
        Assert.False(repeated.Accepted);
        Assert.Equal(CommandRejectionCode.InteractionUnavailable, repeated.RejectionCode);
        Assert.Single(
            session.EventsSince(0),
            gameEvent => gameEvent.Detail is InteractionCompletedEventDetail detail
                && detail.InteractionId == TerminalId);
    }

    [Fact]
    public void RecruitmentAddsProtectorWithFixedLoadoutAndUnlocksAirlock()
    {
        var session = CreateSession();
        ReachRecruitment(session);
        StartDialogue(session, ProtectorInteractionId, "protector.start");

        var response = session.Execute(new ChooseDialogueResponseCommand(
            new CommandId("protector.recruit"),
            ProtagonistId,
            ProtectorInteractionId,
            RecruitResponseId));
        Assert.True(response.Accepted);

        var observation = ObserveStation(session);
        Assert.Equal(2, observation.Party.Count);
        var protector = Assert.Single(observation.Party, actor => actor.Id == ProtectorActorId);
        Assert.Equal("Protector", protector.DisplayName);
        Assert.Equal(new AbilityId("ability.crew.protector.guard_ally"), protector.Loadout!.ActiveAbilityId);
        Assert.Equal(new ObjectiveId("objective.reach_evacuation_airlock"), observation.Objective.Id);
        Assert.True(FindInteraction(observation, AirlockId).CanInteract);
        Assert.Single(
            session.EventsSince(0),
            gameEvent => gameEvent.Detail is PartyMemberRecruitedEventDetail { ActorId: var id }
                && id == ProtectorActorId);
    }

    [Fact]
    public void PartyMoveIsAtomicAndUsesStableSpacedFormation()
    {
        var pathfinder = new TogglePartyPathfinder();
        var session = CreateSession(pathfinder);
        RecruitProtector(session);
        pathfinder.RejectProtector = true;

        var rejected = session.Execute(new MovePartyCommand(
            new CommandId("party.rejected"),
            [ProtagonistId, ProtectorActorId],
            new WorldPosition(8, 0, 2)));
        Assert.False(rejected.Accepted);
        Assert.Equal(CommandRejectionCode.DestinationUnreachable, rejected.RejectionCode);
        Assert.All(ObserveStation(session).Party, actor => Assert.Null(actor.CurrentAction));
        Assert.DoesNotContain(
            session.EventsSince(0),
            gameEvent => gameEvent.Detail is PrimaryActionAssignedEventDetail { CommandId.Value: "party.rejected" });

        pathfinder.RejectProtector = false;
        var accepted = session.Execute(new MovePartyCommand(
            new CommandId("party.accepted"),
            [ProtectorActorId, ProtagonistId],
            new WorldPosition(8, 0, 2)));
        Assert.True(accepted.Accepted);
        AdvanceUntil(session, observation => observation.Party.All(actor => actor.CurrentAction is null), 300);

        var party = ObserveStation(session).Party;
        Assert.Equal(2, party.Count);
        Assert.InRange(party[0].Position.DistanceTo(party[1].Position), 1.0999, 1.1001);
        Assert.InRange((party[0].Position.X + party[1].Position.X) / 2.0, 7.9999, 8.0001);
        Assert.InRange((party[0].Position.Z + party[1].Position.Z) / 2.0, 1.9999, 2.0001);
    }

    [Fact]
    public void PartyMoveRejectsEmptyAndDuplicateActorLists()
    {
        var session = CreateSession();
        _ = SelectVanguard(session);

        var empty = session.Execute(new MovePartyCommand(
            new CommandId("party.empty"),
            [],
            new WorldPosition(2, 0, 0)));
        var duplicate = session.Execute(new MovePartyCommand(
            new CommandId("party.duplicate"),
            [ProtagonistId, ProtagonistId],
            new WorldPosition(2, 0, 0)));

        Assert.False(empty.Accepted);
        Assert.Equal(CommandRejectionCode.EmptyPartySelection, empty.RejectionCode);
        Assert.False(duplicate.Accepted);
        Assert.Equal(CommandRejectionCode.DuplicateActor, duplicate.RejectionCode);
        Assert.Null(ObserveStation(session).Protagonist.CurrentAction);
    }

    [Fact]
    public void PartyMoveFallsBackToRawDestinationWhenAFormationSlotIsBlocked()
    {
        var destination = new WorldPosition(8, 0, 2);
        var session = CreateSession(new FormationFallbackPathfinder(destination));
        RecruitProtector(session);

        var acknowledgement = session.Execute(new MovePartyCommand(
            new CommandId("party.fallback"),
            [ProtagonistId, ProtectorActorId],
            destination));

        Assert.True(acknowledgement.Accepted);
        var party = ObserveStation(session).Party;
        Assert.NotEqual(destination, Assert.Single(party, actor => actor.Id == ProtagonistId).CurrentAction!.Destination);
        Assert.Equal(destination, Assert.Single(party, actor => actor.Id == ProtectorActorId).CurrentAction!.Destination);
    }

    [Fact]
    public void AirlockCompletesOnlyAfterChoiceAndRecruitment()
    {
        var session = CreateSession();
        _ = SelectVanguard(session);

        var locked = session.Execute(new InteractCommand(
            new CommandId("airlock.locked"),
            ProtagonistId,
            AirlockId));
        Assert.False(locked.Accepted);
        Assert.Equal(CommandRejectionCode.InteractionUnavailable, locked.RejectionCode);

        RecruitProtector(session);
        CompleteInteraction(session, AirlockId, "airlock.complete");
        var final = ObserveStation(session);
        Assert.Equal(ScenarioPhase.Completed, final.Phase);
        Assert.Equal(ObjectiveStatus.Completed, final.Objective.Status);
        Assert.All(final.Party, actor =>
        {
            Assert.Null(actor.CurrentAction);
            Assert.Null(actor.PendingAction);
        });
        Assert.Single(session.EventsSince(0), gameEvent => gameEvent.Type == GameplayEventType.ScenarioCompleted);

        var afterCompletion = session.Execute(new MovePartyCommand(
            new CommandId("party.after-complete"),
            [ProtagonistId, ProtectorActorId],
            new WorldPosition(0, 0, 0)));
        Assert.False(afterCompletion.Accepted);
        Assert.Equal(CommandRejectionCode.ScenarioCompleted, afterCompletion.RejectionCode);
    }

    private static CommandAcknowledgement SelectVanguard(GameSession session)
    {
        return session.Execute(new ChooseProtagonistKitCommand(
            new CommandId("kit.select-vanguard"),
            VanguardKitId));
    }

    private static void ReachRecruitment(GameSession session)
    {
        _ = SelectVanguard(session);
        StartDialogue(session, SurvivorId, "survivor.start");
        Assert.True(session.Execute(new ChooseDialogueResponseCommand(
            new CommandId("survivor.choose-power"),
            ProtagonistId,
            SurvivorId,
            ServicePowerResponseId)).Accepted);
    }

    private static void RecruitProtector(GameSession session)
    {
        ReachRecruitment(session);
        StartDialogue(session, ProtectorInteractionId, "protector.start");
        Assert.True(session.Execute(new ChooseDialogueResponseCommand(
            new CommandId("protector.recruit"),
            ProtagonistId,
            ProtectorInteractionId,
            RecruitResponseId)).Accepted);
    }

    private static void StartDialogue(GameSession session, EntityId interactionId, string commandId)
    {
        Assert.True(session.Execute(new InteractCommand(
            new CommandId(commandId),
            ProtagonistId,
            interactionId)).Accepted);
        AdvanceUntil(
            session,
            observation => observation.ActiveDialogue?.InteractionId == interactionId,
            180);
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
            300);
    }

    private static GameSession CreateSession(ISpatialPathfinder? pathfinder = null)
    {
        return GameSession.CreateStationRoute(
            LoadDefinition(),
            new StationRouteLayout(
                new WorldPosition(0, 0, 0),
                CreateActorPlacements(),
                CreateInteractionPlacements()),
            pathfinder ?? new DirectPathfinder());
    }

    private static StationActorPlacement[] CreateActorPlacements()
    {
        return [new StationActorPlacement(ProtectorActorId, new WorldPosition(6, 0, 0))];
    }

    private static StationInteractionPlacement[] CreateInteractionPlacements()
    {
        return
        [
            new(SurvivorId, new WorldPosition(4, 0, 0), new WorldPosition(3, 0, 0)),
            new(ProtectorInteractionId, new WorldPosition(6, 0, 0), new WorldPosition(5, 0, 0)),
            new(TerminalId, new WorldPosition(6, 0, 2), new WorldPosition(5, 0, 2)),
            new(AirlockId, new WorldPosition(10, 0, 0), new WorldPosition(9, 0, 0)),
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

    private sealed class TogglePartyPathfinder : ISpatialPathfinder
    {
        public bool RejectProtector { get; set; }

        public SpatialPathResult FindPath(EntityId actorId, WorldPosition origin, WorldPosition destination)
        {
            _ = origin;
            return RejectProtector && actorId == ProtectorActorId
                ? SpatialPathResult.Unreachable
                : SpatialPathResult.Reachable([destination]);
        }
    }

    private sealed class FormationFallbackPathfinder(WorldPosition rawDestination) : ISpatialPathfinder
    {
        public SpatialPathResult FindPath(EntityId actorId, WorldPosition origin, WorldPosition destination)
        {
            _ = origin;
            return actorId == ProtectorActorId && destination != rawDestination
                ? SpatialPathResult.Unreachable
                : SpatialPathResult.Reachable([destination]);
        }
    }
}
