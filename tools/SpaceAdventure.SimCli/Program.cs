using System.Diagnostics;
using System.Text.Json;
using SpaceAdventure.Core;

const string BootstrapScenarioId = "bootstrap";
const string StationRouteScenarioId = "station-route";

var scenarioId = args.Length == 0 ? BootstrapScenarioId : args[0];
var output = new JsonLinesOutput();

try
{
    return scenarioId switch
    {
        BootstrapScenarioId => RunBootstrap(output),
        StationRouteScenarioId => RunStationRoute(output),
        _ => ReportUnknownScenario(output, scenarioId),
    };
}
catch (IOException exception)
{
    return ReportFatalError(output, scenarioId, "content_io_failed", exception);
}
catch (UnauthorizedAccessException exception)
{
    return ReportFatalError(output, scenarioId, "content_access_denied", exception);
}
catch (InvalidDataException exception)
{
    return ReportFatalError(output, scenarioId, "content_invalid", exception);
}
catch (InvalidOperationException exception)
{
    return ReportFatalError(output, scenarioId, "scenario_invalid", exception);
}

static int RunBootstrap(JsonLinesOutput output)
{
    var stopwatch = Stopwatch.StartNew();
    var session = new GameSession();
    output.Emit(new
    {
        kind = "run_metadata",
        schema_version = 1,
        scenario_id = BootstrapScenarioId,
        content_revision = "bootstrap-v1",
        seed = 1,
        game_build = typeof(GameSession).Assembly.GetName().Version?.ToString(),
        runtime = Environment.Version.ToString(),
        tick_rate = GameSession.TicksPerSecond,
    });

    var pause = session.Execute(
        new SetPauseCommand(new CommandId("bootstrap.pause"), Paused: true));
    output.Emit(new
    {
        kind = "command_result",
        command_id = pause.CommandId.Value,
        command_type = "set_pause",
        accepted = pause.Accepted,
        observation = ObservationProjection.Project(pause.Observation),
    });

    foreach (var gameEvent in session.EventsSince(0))
    {
        output.Emit(new
        {
            kind = "gameplay_event",
            gameEvent.Sequence,
            gameEvent.Tick,
            Type = JsonLinesOutput.ToJsonName(gameEvent.Type),
            CommandId = gameEvent.CommandId?.Value,
            gameEvent.Paused,
            RejectionCode = gameEvent.RejectionCode is null
                ? null
                : JsonLinesOutput.ToJsonName(gameEvent.RejectionCode.Value),
        });
    }

    var advanced = session.AdvanceTicks(5);
    var pauseObservable = session.Observe().Paused;
    var pauseStoppedTicks = advanced == 0 && session.Tick == 0;
    output.Emit(new { kind = "assertion", name = "pause_is_observable", passed = pauseObservable });
    output.Emit(new
    {
        kind = "assertion",
        name = "pause_stops_gameplay_ticks",
        passed = pauseStoppedTicks,
    });

    stopwatch.Stop();
    var passed = pause.Accepted && pauseObservable && pauseStoppedTicks;
    output.Emit(new
    {
        kind = "final_snapshot",
        passed,
        duration_ms = stopwatch.ElapsedMilliseconds,
        observation = ObservationProjection.Project(session.Observe()),
    });

    return passed ? 0 : 1;
}

static int RunStationRoute(JsonLinesOutput output)
{
    const int MaximumTicksPerLeg = 900;

    var stopwatch = Stopwatch.StartNew();
    var contentPath = Path.Combine(
        AppContext.BaseDirectory,
        "content",
        "station-route.json");
    var definition = StationRouteContent.ParseJson(File.ReadAllText(contentPath));
    var layout = StationRouteFixture.CreateLayout(definition);
    var session = GameSession.CreateStationRoute(
        definition,
        layout,
        new StationRouteFixturePathfinder([definition.Protagonist.Id, definition.Companion.Id]));
    var events = new GameplayEventOutput(output);
    var assertions = new ScenarioAssertions(output);

    var survivor = definition.Interactions.Single(
        interaction => interaction.Effect == StationInteractionEffect.BeginSurvivorDialogue);
    var protector = definition.Interactions.Single(
        interaction => interaction.Effect == StationInteractionEffect.BeginRecruitmentDialogue);
    var terminal = definition.Interactions.Single(
        interaction => interaction.Effect == StationInteractionEffect.RecordObservation);
    var airlock = definition.Interactions.Single(
        interaction => interaction.Effect == StationInteractionEffect.CompleteScenario);

    output.Emit(new
    {
        kind = "run_metadata",
        schema_version = 2,
        scenario_id = StationRouteScenarioId,
        content_scenario_id = definition.ScenarioId.Value,
        content_revision = definition.ContentRevision,
        content_asset = "content/station-route.json",
        seed = 1,
        game_build = typeof(GameSession).Assembly.GetName().Version?.ToString(),
        runtime = Environment.Version.ToString(),
        tick_rate = GameSession.TicksPerSecond,
        maximum_ticks_per_leg = MaximumTicksPerLeg,
        pathfinder = "deterministic_station_route_fixture_v2",
    });
    events.Flush(session);

    var initial = RequireStationObservation(session.Observe());
    assertions.Check(
        "initial_state_requires_protagonist_kit",
        initial.Phase == ScenarioPhase.AwaitingProtagonistSelection
            && initial.SelectedProtagonistKit is null
            && initial.Party.Count == 1);
    assertions.Check(
        "airlock_is_gated_before_briefing",
        FindInteraction(initial, airlock.Id).State == InteractionState.Unavailable);

    var kit = definition.ProtagonistKits.Single(candidate =>
        candidate.Id == new ProtagonistKitId("kit.protagonist.vanguard"));
    var kitCommand = session.Execute(new ChooseProtagonistKitCommand(
        new CommandId("station-route.choose-vanguard"),
        kit.Id));
    EmitCommandResult(output, "choose_protagonist_kit", kitCommand);
    events.Flush(session);
    var afterKitSelection = RequireStationObservation(session.Observe());
    assertions.Check(
        "vanguard_kit_is_selected",
        kitCommand.Accepted
            && afterKitSelection.SelectedProtagonistKit?.Id == kit.Id
            && afterKitSelection.Protagonist.Loadout?.BasicAttackId == kit.BasicAttackId
            && afterKitSelection.Protagonist.Loadout?.ActiveAbilityId == kit.ActiveAbilityId);

    var survivorCommand = session.Execute(new InteractCommand(
        new CommandId("station-route.interact-survivor"),
        definition.Protagonist.Id,
        survivor.Id));
    EmitCommandResult(output, "interact", survivorCommand);
    events.Flush(session);

    var survivorAdvance = AdvanceUntil(
        session,
        observation => observation.ActiveDialogue?.InteractionId == survivor.Id,
        MaximumTicksPerLeg);
    EmitAdvanceResult(output, "approach_survivor", MaximumTicksPerLeg, survivorAdvance);
    events.Flush(session);
    assertions.Check(
        "survivor_dialogue_started_within_budget",
        survivorCommand.Accepted && survivorAdvance.ConditionReached);

    var dialogue = RequireStationObservation(session.Observe()).ActiveDialogue;
    var responseId = survivor.Dialogue!.Responses.Single(response =>
        response.Effect == StationDialogueResponseEffect.RerouteServicePower).Id;
    var responseCommand = session.Execute(new ChooseDialogueResponseCommand(
        new CommandId("station-route.answer-survivor"),
        definition.Protagonist.Id,
        survivor.Id,
        responseId));
    EmitCommandResult(output, "choose_dialogue_response", responseCommand);
    events.Flush(session);

    var afterResponse = RequireStationObservation(session.Observe());
    assertions.Check(
        "authored_response_matches_active_dialogue",
        dialogue?.Responses.Any(response => response.Id == responseId) == true
            && responseCommand.Accepted);
    assertions.Check(
        "power_choice_advances_recruitment_objective",
        afterResponse.ActiveDialogue is null
            && afterResponse.RoutePowerMode == RoutePowerMode.ServiceRerouted
            && afterResponse.Objective.Id == definition.RecruitmentObjective.Id
            && FindInteraction(afterResponse, survivor.Id).State == InteractionState.Completed
            && FindInteraction(afterResponse, protector.Id).State == InteractionState.Available
            && FindInteraction(afterResponse, airlock.Id).State == InteractionState.Unavailable);

    var terminalCommand = session.Execute(new InteractCommand(
        new CommandId("station-route.inspect-terminal"),
        definition.Protagonist.Id,
        terminal.Id));
    EmitCommandResult(output, "interact", terminalCommand);
    events.Flush(session);

    var terminalAdvance = AdvanceUntil(
        session,
        observation => FindInteraction(observation, terminal.Id).State == InteractionState.Completed,
        MaximumTicksPerLeg);
    EmitAdvanceResult(output, "approach_terminal", MaximumTicksPerLeg, terminalAdvance);
    events.Flush(session);
    var afterTerminal = RequireStationObservation(session.Observe());
    assertions.Check(
        "optional_terminal_completed_within_budget",
        terminalCommand.Accepted
            && terminalAdvance.ConditionReached
            && FindInteraction(afterTerminal, terminal.Id).ResultText == terminal.ResultText
            && afterTerminal.Phase == ScenarioPhase.InProgress);

    var protectorCommand = session.Execute(new InteractCommand(
        new CommandId("station-route.interact-protector"),
        definition.Protagonist.Id,
        protector.Id));
    EmitCommandResult(output, "interact", protectorCommand);
    events.Flush(session);
    var protectorAdvance = AdvanceUntil(
        session,
        observation => observation.ActiveDialogue?.InteractionId == protector.Id,
        MaximumTicksPerLeg);
    EmitAdvanceResult(output, "approach_protector", MaximumTicksPerLeg, protectorAdvance);
    events.Flush(session);

    var recruitResponseId = protector.Dialogue!.Responses.Single().Id;
    var recruitCommand = session.Execute(new ChooseDialogueResponseCommand(
        new CommandId("station-route.recruit-protector"),
        definition.Protagonist.Id,
        protector.Id,
        recruitResponseId));
    EmitCommandResult(output, "choose_dialogue_response", recruitCommand);
    events.Flush(session);
    var afterRecruitment = RequireStationObservation(session.Observe());
    assertions.Check(
        "protector_joins_and_unlocks_airlock",
        protectorCommand.Accepted
            && protectorAdvance.ConditionReached
            && recruitCommand.Accepted
            && afterRecruitment.Party.Any(actor => actor.Id == definition.Companion.Id)
            && afterRecruitment.Objective.Id == definition.DestinationObjective.Id
            && FindInteraction(afterRecruitment, airlock.Id).State == InteractionState.Available);

    var partyMoveCommand = session.Execute(new MovePartyCommand(
        new CommandId("station-route.move-party"),
        [definition.Companion.Id, definition.Protagonist.Id],
        new WorldPosition(7, 0, 0)));
    EmitCommandResult(output, "move_party", partyMoveCommand);
    events.Flush(session);
    var partyAdvance = AdvanceUntil(
        session,
        observation => observation.Party.All(actor =>
            actor.CurrentAction is null && actor.PendingAction is null),
        MaximumTicksPerLeg);
    EmitAdvanceResult(output, "move_party", MaximumTicksPerLeg, partyAdvance);
    events.Flush(session);
    var formedParty = RequireStationObservation(session.Observe()).Party;
    var formedProtagonist = formedParty.Single(actor => actor.Id == definition.Protagonist.Id);
    var formedCompanion = formedParty.Single(actor => actor.Id == definition.Companion.Id);
    assertions.Check(
        "party_moves_in_stable_formation",
        partyMoveCommand.Accepted
            && partyAdvance.ConditionReached
            && formedParty.Count == 2
            && formedProtagonist.Position == new WorldPosition(
                6.826074728690739,
                0,
                -0.5217758139277826)
            && formedCompanion.Position == new WorldPosition(
                7.173925271309261,
                0,
                0.5217758139277826));

    var airlockCommand = session.Execute(new InteractCommand(
        new CommandId("station-route.enter-airlock"),
        definition.Protagonist.Id,
        airlock.Id));
    EmitCommandResult(output, "interact", airlockCommand);
    events.Flush(session);

    var airlockAdvance = AdvanceUntil(
        session,
        observation => observation.Phase == ScenarioPhase.Completed,
        MaximumTicksPerLeg);
    EmitAdvanceResult(output, "approach_airlock", MaximumTicksPerLeg, airlockAdvance);
    events.Flush(session);

    var finalObservation = RequireStationObservation(session.Observe());
    assertions.Check(
        "airlock_completes_scenario_within_budget",
        airlockCommand.Accepted
            && airlockAdvance.ConditionReached
            && finalObservation.Phase == ScenarioPhase.Completed
            && finalObservation.Objective.Status == ObjectiveStatus.Completed
            && FindInteraction(finalObservation, airlock.Id).State == InteractionState.Completed);
    assertions.Check(
        "critical_path_emits_completion_event",
        session.EventsSince(0).Any(gameEvent => gameEvent.Type == GameplayEventType.ScenarioCompleted));
    assertions.Check(
        "critical_path_has_no_command_rejections",
        session.EventsSince(0).All(gameEvent => gameEvent.Type != GameplayEventType.CommandRejected));
    assertions.Check(
        "critical_path_remains_within_total_tick_budget",
        session.Tick <= MaximumTicksPerLeg * 6L);

    stopwatch.Stop();
    output.Emit(new
    {
        kind = "final_snapshot",
        passed = assertions.Passed,
        duration_ms = stopwatch.ElapsedMilliseconds,
        total_ticks = session.Tick,
        latest_event_sequence = session.Observe().LatestEventSequence,
        observation = ObservationProjection.Project(session.Observe()),
    });

    return assertions.Passed ? 0 : 1;
}

static StationRouteObservation RequireStationObservation(GameObservation observation)
{
    return observation.StationRoute
        ?? throw new InvalidOperationException("The station-route observation is unavailable.");
}

static InteractionObservation FindInteraction(
    StationRouteObservation observation,
    EntityId interactionId)
{
    return observation.Interactions.Single(interaction => interaction.Id == interactionId);
}

static AdvanceResult AdvanceUntil(
    GameSession session,
    Func<StationRouteObservation, bool> condition,
    int maximumTickAttempts)
{
    ArgumentNullException.ThrowIfNull(session);
    ArgumentNullException.ThrowIfNull(condition);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumTickAttempts);

    if (condition(RequireStationObservation(session.Observe())))
    {
        return new AdvanceResult(true, 0, 0, session.Observe());
    }

    var ticksAdvanced = 0;
    for (var attempt = 1; attempt <= maximumTickAttempts; attempt++)
    {
        ticksAdvanced += session.AdvanceTicks(1);
        var observation = session.Observe();
        if (condition(RequireStationObservation(observation)))
        {
            return new AdvanceResult(true, attempt, ticksAdvanced, observation);
        }
    }

    return new AdvanceResult(false, maximumTickAttempts, ticksAdvanced, session.Observe());
}

static void EmitAdvanceResult(
    JsonLinesOutput output,
    string label,
    int maximumTickAttempts,
    AdvanceResult result)
{
    output.Emit(new
    {
        kind = "advance_result",
        label,
        condition_reached = result.ConditionReached,
        attempts = result.Attempts,
        ticks_advanced = result.TicksAdvanced,
        maximum_tick_attempts = maximumTickAttempts,
        observation = ObservationProjection.Project(result.Observation),
    });
}

static void EmitCommandResult(
    JsonLinesOutput output,
    string commandType,
    CommandAcknowledgement acknowledgement)
{
    output.Emit(new
    {
        kind = "command_result",
        command_id = acknowledgement.CommandId.Value,
        command_type = commandType,
        accepted = acknowledgement.Accepted,
        rejection_code = acknowledgement.RejectionCode is null
            ? null
            : JsonLinesOutput.ToJsonName(acknowledgement.RejectionCode.Value),
        observation = ObservationProjection.Project(acknowledgement.Observation),
    });
}

static int ReportUnknownScenario(JsonLinesOutput output, string scenarioId)
{
    output.Emit(new { kind = "error", code = "unknown_scenario", scenario_id = scenarioId });
    return 2;
}

static int ReportFatalError(
    JsonLinesOutput output,
    string scenarioId,
    string code,
    Exception exception)
{
    output.Emit(new
    {
        kind = "error",
        code,
        scenario_id = scenarioId,
        exception_type = exception.GetType().Name,
        message = exception.Message,
    });
    return 1;
}

internal sealed record AdvanceResult(
    bool ConditionReached,
    int Attempts,
    int TicksAdvanced,
    GameObservation Observation);

internal sealed class ScenarioAssertions(JsonLinesOutput output)
{
    public bool Passed { get; private set; } = true;

    public void Check(string name, bool passed)
    {
        Passed &= passed;
        output.Emit(new { kind = "assertion", name, passed });
    }
}

internal sealed class GameplayEventOutput(JsonLinesOutput output)
{
    private long _latestSequence;

    public void Flush(GameSession session)
    {
        foreach (var gameEvent in session.EventsSince(_latestSequence))
        {
            output.Emit(new
            {
                kind = "gameplay_event",
                sequence = gameEvent.Sequence,
                tick = gameEvent.Tick,
                type = JsonLinesOutput.ToJsonName(gameEvent.Type),
                command_id = gameEvent.CommandId?.Value,
                paused = gameEvent.Paused,
                rejection_code = gameEvent.RejectionCode is null
                    ? null
                    : JsonLinesOutput.ToJsonName(gameEvent.RejectionCode.Value),
                detail = ProjectDetail(gameEvent.Detail),
            });
            _latestSequence = gameEvent.Sequence;
        }
    }

    private static object? ProjectDetail(GameplayEventDetail? detail)
    {
        return detail switch
        {
            null => null,
            ProtagonistKitSelectedEventDetail selected => new
            {
                detail_type = "protagonist_kit_selected",
                command_id = selected.CommandId.Value,
                kit_id = selected.KitId.Value,
            },
            PrimaryActionAssignedEventDetail assigned => new
            {
                detail_type = "primary_action_assigned",
                command_id = assigned.CommandId.Value,
                actor_id = assigned.ActorId.Value,
                action_kind = JsonLinesOutput.ToJsonName(assigned.Kind),
                destination = ObservationProjection.ProjectPosition(assigned.Destination),
                interaction_target_id = assigned.InteractionTargetId?.Value,
                assigned.Pending,
                replaced_command_id = assigned.ReplacedCommandId?.Value,
            },
            MovementArrivedEventDetail arrived => new
            {
                detail_type = "movement_arrived",
                command_id = arrived.CommandId.Value,
                actor_id = arrived.ActorId.Value,
                position = ObservationProjection.ProjectPosition(arrived.Position),
            },
            PrimaryActionFailedEventDetail failed => new
            {
                detail_type = "primary_action_failed",
                command_id = failed.CommandId.Value,
                actor_id = failed.ActorId.Value,
                reason = JsonLinesOutput.ToJsonName(failed.Reason),
            },
            DialogueStartedEventDetail started => new
            {
                detail_type = "dialogue_started",
                command_id = started.CommandId.Value,
                actor_id = started.ActorId.Value,
                interaction_id = started.InteractionId.Value,
            },
            DialogueResponseChosenEventDetail chosen => new
            {
                detail_type = "dialogue_response_chosen",
                command_id = chosen.CommandId.Value,
                actor_id = chosen.ActorId.Value,
                interaction_id = chosen.InteractionId.Value,
                response_id = chosen.ResponseId.Value,
            },
            RouteConsequenceSelectedEventDetail consequence => new
            {
                detail_type = "route_consequence_selected",
                command_id = consequence.CommandId.Value,
                route_power_mode = JsonLinesOutput.ToJsonName(consequence.RoutePowerMode),
            },
            PartyMemberRecruitedEventDetail recruited => new
            {
                detail_type = "party_member_recruited",
                command_id = recruited.CommandId.Value,
                actor_id = recruited.ActorId.Value,
            },
            InteractionCompletedEventDetail completed => new
            {
                detail_type = "interaction_completed",
                command_id = completed.CommandId.Value,
                actor_id = completed.ActorId.Value,
                interaction_id = completed.InteractionId.Value,
                effect = JsonLinesOutput.ToJsonName(completed.Effect),
            },
            ObjectiveChangedEventDetail objective => new
            {
                detail_type = "objective_changed",
                command_id = objective.CommandId.Value,
                previous_objective_id = objective.PreviousObjectiveId.Value,
                current_objective_id = objective.CurrentObjectiveId.Value,
                status = JsonLinesOutput.ToJsonName(objective.Status),
            },
            ScenarioCompletedEventDetail completed => new
            {
                detail_type = "scenario_completed",
                command_id = completed.CommandId.Value,
                scenario_id = completed.ScenarioId.Value,
            },
            _ => new { detail_type = detail.GetType().Name },
        };
    }
}

internal sealed class JsonLinesOutput
{
    private readonly JsonSerializerOptions _options = CreateOptions();

    public void Emit(object value)
    {
        Console.WriteLine(JsonSerializer.Serialize(value, _options));
    }

    public static string ToJsonName<TEnum>(TEnum value)
        where TEnum : struct, Enum
    {
        return JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };
        return options;
    }
}

internal static class ObservationProjection
{
    public static object Project(GameObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);

        return new
        {
            tick = observation.Tick,
            paused = observation.Paused,
            latest_event_sequence = observation.LatestEventSequence,
            station_route = observation.StationRoute is null
                ? null
                : ProjectStationRoute(observation.StationRoute),
        };
    }

    public static object ProjectPosition(WorldPosition position)
    {
        return new
        {
            x = position.X,
            y = position.Y,
            z = position.Z,
        };
    }

    private static object ProjectStationRoute(StationRouteObservation observation)
    {
        return new
        {
            scenario_id = observation.ScenarioId.Value,
            content_schema_version = observation.ContentSchemaVersion,
            content_revision = observation.ContentRevision,
            phase = JsonLinesOutput.ToJsonName(observation.Phase),
            protagonist = ProjectActor(observation.Protagonist),
            party = observation.Party.Select(ProjectActor).ToArray(),
            available_protagonist_kits = observation.AvailableProtagonistKits.Select(kit => new
            {
                id = kit.Id.Value,
                kit.DisplayName,
                kit.Role,
                kit.WeaponName,
                basic_attack_id = kit.BasicAttackId.Value,
                active_ability_id = kit.ActiveAbilityId.Value,
                kit.ActiveAbilityName,
                active_ability_target_kind = JsonLinesOutput.ToJsonName(kit.ActiveAbilityTargetKind),
            }).ToArray(),
            selected_protagonist_kit_id = observation.SelectedProtagonistKit?.Id.Value,
            route_power_mode = JsonLinesOutput.ToJsonName(observation.RoutePowerMode),
            objective = ProjectObjective(observation.Objective),
            interactions = observation.Interactions.Select(ProjectInteraction).ToArray(),
            active_dialogue = observation.ActiveDialogue is null
                ? null
                : ProjectDialogue(observation.ActiveDialogue),
        };
    }

    private static object ProjectActor(ActorObservation observation)
    {
        return new
        {
            id = observation.Id.Value,
            display_name = observation.DisplayName,
            loadout = observation.Loadout is null
                ? null
                : new
                {
                    observation.Loadout.WeaponName,
                    basic_attack_id = observation.Loadout.BasicAttackId.Value,
                    active_ability_id = observation.Loadout.ActiveAbilityId.Value,
                    observation.Loadout.ActiveAbilityName,
                    active_ability_target_kind = JsonLinesOutput.ToJsonName(
                        observation.Loadout.ActiveAbilityTargetKind),
                },
            position = ProjectPosition(observation.Position),
            current_action = ProjectAction(observation.CurrentAction),
            pending_action = ProjectAction(observation.PendingAction),
        };
    }

    private static object? ProjectAction(PrimaryActionObservation? observation)
    {
        return observation is null
            ? null
            : new
            {
                command_id = observation.CommandId.Value,
                kind = JsonLinesOutput.ToJsonName(observation.Kind),
                destination = ProjectPosition(observation.Destination),
                interaction_target_id = observation.InteractionTargetId?.Value,
            };
    }

    private static object ProjectObjective(ObjectiveObservation observation)
    {
        return new
        {
            id = observation.Id.Value,
            text = observation.Text,
            status = JsonLinesOutput.ToJsonName(observation.Status),
        };
    }

    private static object ProjectInteraction(InteractionObservation observation)
    {
        return new
        {
            id = observation.Id.Value,
            kind = JsonLinesOutput.ToJsonName(observation.Kind),
            prompt = observation.Prompt,
            position = ProjectPosition(observation.Position),
            approach_position = ProjectPosition(observation.ApproachPosition),
            use_radius_meters = observation.UseRadiusMeters,
            state = JsonLinesOutput.ToJsonName(observation.State),
            can_interact = observation.CanInteract,
            result_text = observation.ResultText,
        };
    }

    private static object ProjectDialogue(DialogueObservation observation)
    {
        return new
        {
            interaction_id = observation.InteractionId.Value,
            actor_id = observation.ActorId.Value,
            speaker = observation.Speaker,
            line = observation.Line,
            responses = observation.Responses.Select(response => new
            {
                id = response.Id.Value,
                response.Text,
            }).ToArray(),
        };
    }
}

internal static class StationRouteFixture
{
    private static readonly Dictionary<string, (WorldPosition Position, WorldPosition Approach)>
        InteractionPlacements = new Dictionary<string, (WorldPosition, WorldPosition)>(StringComparer.Ordinal)
        {
            ["interaction.survivor"] = (new(1, 0, 1), new(0, 0, 1)),
            ["interaction.protector"] = (new(5.5, 0, 0), new(4.5, 0, 0)),
            ["interaction.service_terminal"] = (new(5.5, 0, 1.7), new(5.5, 0, 1)),
            ["interaction.evacuation_airlock"] = (new(9, 0, 0), new(8, 0, 0)),
        };

    public static StationRouteLayout CreateLayout(StationRouteDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var placements = definition.Interactions.Select(interaction =>
        {
            if (!InteractionPlacements.TryGetValue(interaction.Id.Value, out var placement))
            {
                throw new InvalidDataException(
                    $"The deterministic station-route fixture has no placement for '{interaction.Id}'.");
            }

            return new StationInteractionPlacement(
                interaction.Id,
                placement.Position,
                placement.Approach);
        });
        return new StationRouteLayout(
            new WorldPosition(0, 0, 5.5),
            [new StationActorPlacement(definition.Companion.Id, new WorldPosition(5.5, 0, 0))],
            placements);
    }
}

internal sealed class StationRouteFixturePathfinder(IEnumerable<EntityId> actorIds) : ISpatialPathfinder
{
    private const double CoordinateTolerance = 0.0001;
    private static readonly WorldPosition Junction = new(1.5, 0, 1);
    private readonly HashSet<EntityId> _actorIds = actorIds.ToHashSet();

    public SpatialPathResult FindPath(
        EntityId actorId,
        WorldPosition origin,
        WorldPosition destination)
    {
        if (!_actorIds.Contains(actorId)
            || !origin.IsFinite
            || !destination.IsFinite
            || !IsGroundLevel(origin)
            || !IsGroundLevel(destination))
        {
            return SpatialPathResult.Unreachable;
        }

        var originRegion = GetRegion(origin);
        var destinationRegion = GetRegion(destination);
        if (originRegion == FixtureRegion.None || destinationRegion == FixtureRegion.None)
        {
            return SpatialPathResult.Unreachable;
        }

        if ((originRegion & destinationRegion) != 0)
        {
            return SpatialPathResult.Reachable([destination]);
        }

        return SpatialPathResult.Reachable([Junction, destination]);
    }

    private static bool IsGroundLevel(WorldPosition position)
    {
        return Math.Abs(position.Y) <= CoordinateTolerance;
    }

    private static FixtureRegion GetRegion(WorldPosition position)
    {
        var region = FixtureRegion.None;
        if (position.X >= -2 && position.X <= 2 && position.Z >= -2 && position.Z <= 7)
        {
            region |= FixtureRegion.VerticalCorridor;
        }

        if (position.X >= 2 && position.X <= 9 && position.Z >= -2 && position.Z <= 2)
        {
            region |= FixtureRegion.EastCorridor;
        }

        return region;
    }

    [Flags]
    private enum FixtureRegion
    {
        None = 0,
        VerticalCorridor = 1,
        EastCorridor = 2,
    }
}
