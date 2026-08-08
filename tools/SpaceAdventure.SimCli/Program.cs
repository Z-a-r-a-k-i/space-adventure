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
    var pathfinder = new StationRouteFixturePathfinder(
        [definition.Protagonist.Id, definition.Companion.Id, definition.Combat.Hostile.Id]);
    var session = GameSession.CreateStationRoute(
        definition,
        layout,
        pathfinder);
    var events = new GameplayEventOutput(output);
    var assertions = new ScenarioAssertions(output);

    var survivor = definition.Interactions.Single(
        interaction => interaction.Effect == StationInteractionEffect.BeginSurvivorDialogue);
    var entryDoor = definition.Interactions.Single(
        interaction => interaction.Effect == StationInteractionEffect.OpenEntryServiceDoor);
    var soloExit = definition.Interactions.Single(
        interaction => interaction.Effect == StationInteractionEffect.OpenSoloExitServiceDoor);
    var terminal = definition.Interactions.Single(
        interaction => interaction.Effect == StationInteractionEffect.RecordObservation);
    var protector = definition.Interactions.Single(
        interaction => interaction.Effect == StationInteractionEffect.BeginRecruitmentDialogue);
    var airlock = definition.Interactions.Single(
        interaction => interaction.Effect == StationInteractionEffect.CompleteScenario);

    output.Emit(new
    {
        kind = "run_metadata",
        schema_version = 3,
        scenario_id = StationRouteScenarioId,
        content_scenario_id = definition.ScenarioId.Value,
        content_revision = definition.ContentRevision,
        content_asset = "content/station-route.json",
        seed = 1,
        game_build = typeof(GameSession).Assembly.GetName().Version?.ToString(),
        runtime = Environment.Version.ToString(),
        tick_rate = GameSession.TicksPerSecond,
        maximum_ticks_per_leg = MaximumTicksPerLeg,
        pathfinder = "deterministic_station_route_fixture_v4",
    });
    events.Flush(session);

    var initial = RequireStationObservation(session.Observe());
    assertions.Check(
        "initial_state_requires_protagonist_kit",
        initial.Phase == ScenarioPhase.AwaitingProtagonistSelection
            && initial.SelectedProtagonistKit is null
            && initial.Party.Count == 1);
    assertions.Check(
        "all_route_gates_are_locked_before_briefing",
        FindInteraction(initial, entryDoor.Id).State == InteractionState.Unavailable
            && FindInteraction(initial, soloExit.Id).State == InteractionState.Unavailable
            && FindInteraction(initial, protector.Id).State == InteractionState.Unavailable
            && FindInteraction(initial, airlock.Id).State == InteractionState.Unavailable);

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

    var positionBeforeLockedMove = afterKitSelection.Protagonist.Position;
    var lockedEntryMove = session.Execute(new MoveActorCommand(
        new CommandId("station-route.verify-entry-navigation-lock"),
        definition.Protagonist.Id,
        new WorldPosition(-10, 0, 0)));
    EmitCommandResult(output, "move_actor", lockedEntryMove);
    events.Flush(session);
    assertions.Check(
        "entry_navigation_is_locked_before_survivor_choice",
        !lockedEntryMove.Accepted
            && lockedEntryMove.RejectionCode == CommandRejectionCode.DestinationUnreachable
            && RequireStationObservation(session.Observe()).Protagonist.Position
                == positionBeforeLockedMove);

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
    pathfinder.EntryDoorUnlocked = true;

    var afterResponse = RequireStationObservation(session.Observe());
    assertions.Check(
        "authored_response_matches_active_dialogue",
        dialogue?.Responses.Any(response => response.Id == responseId) == true
            && responseCommand.Accepted);
    assertions.Check(
        "power_choice_advances_entry_door_objective",
        afterResponse.ActiveDialogue is null
            && afterResponse.RoutePowerMode == RoutePowerMode.ServiceRerouted
            && afterResponse.Objective.Id == definition.EntryDoorObjective.Id
            && FindInteraction(afterResponse, survivor.Id).State == InteractionState.Completed
            && FindInteraction(afterResponse, entryDoor.Id).State == InteractionState.Available
            && FindInteraction(afterResponse, soloExit.Id).State == InteractionState.Unavailable
            && FindInteraction(afterResponse, protector.Id).State == InteractionState.Unavailable
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

    var arenaDestination = layout.Encounter!.TriggerCenter;
    var arenaSequence = session.Observe().LatestEventSequence;
    var arenaMoveCommand = session.Execute(new MoveActorCommand(
        new CommandId("station-route.enter-solo-arena"),
        definition.Protagonist.Id,
        arenaDestination));
    EmitCommandResult(output, "move_actor", arenaMoveCommand);
    events.Flush(session);
    var arenaAdvance = AdvanceUntil(
        session,
        observation => observation.Encounter?.Phase == EncounterPhase.Readying,
        MaximumTicksPerLeg);
    EmitAdvanceResult(output, "enter_solo_arena", MaximumTicksPerLeg, arenaAdvance);
    events.Flush(session);
    var afterArena = RequireStationObservation(session.Observe());
    var arenaEvents = session.EventsSince(arenaSequence);
    var entryDoorOpened = arenaEvents.FirstOrDefault(gameEvent =>
        gameEvent.Detail is InteractionCompletedEventDetail detail
            && detail.InteractionId == entryDoor.Id);
    var encounterStarted = arenaEvents.FirstOrDefault(gameEvent =>
        gameEvent.Type == GameplayEventType.EncounterStarted);
    assertions.Check(
        "unlocked_entry_door_auto_opens_before_encounter",
        arenaMoveCommand.Accepted
            && arenaAdvance.ConditionReached
            && entryDoorOpened is not null
            && encounterStarted is not null
            && entryDoorOpened.Sequence < encounterStarted.Sequence
            && FindInteraction(afterArena, entryDoor.Id).State == InteractionState.Completed
            && afterArena.Objective.Id == definition.CombatObjective.Id
            && afterArena.Encounter?.Phase == EncounterPhase.Readying
            && session.IsPaused
            && afterArena.Party.Count == 1);

    var abilityCommand = session.Execute(new UseAbilityCommand(
        new CommandId("station-route.suppress-enforcer"),
        definition.Protagonist.Id,
        definition.Combat.ProtagonistAbility.Id,
        new PositionAbilityTarget(afterArena.Hostiles!.Single().Position)));
    EmitCommandResult(output, "use_ability", abilityCommand);
    events.Flush(session);
    var resumed = session.Execute(new SetPauseCommand(
        new CommandId("station-route.resume-combat"),
        Paused: false));
    EmitCommandResult(output, "set_pause", resumed);
    events.Flush(session);

    CommandAcknowledgement? attackCommand = null;
    var healCommandAccepted = false;
    var attackResumedAfterHeal = false;
    for (var tick = 0; tick < MaximumTicksPerLeg; tick++)
    {
        var combatObservation = RequireStationObservation(session.Observe());
        if (combatObservation.Encounter!.Phase is EncounterPhase.Securing or EncounterPhase.Victory)
        {
            break;
        }

        if (attackCommand is null
            && combatObservation.Protagonist.CurrentAction is null
            && combatObservation.Protagonist.PendingAction is null
            && combatObservation.Protagonist.Combat!.Cooldowns.Single().RemainingTicks > 0)
        {
            attackCommand = session.Execute(new AssignBasicAttackTargetCommand(
                new CommandId("station-route.attack-enforcer"),
                definition.Protagonist.Id,
                definition.Combat.Hostile.Id));
            EmitCommandResult(output, "assign_basic_attack_target", attackCommand);
            events.Flush(session);
        }

        if (!healCommandAccepted
            && combatObservation.Protagonist.Combat!.Health <= 60
            && combatObservation.Protagonist.Combat.Items.Single().Charges > 0)
        {
            var healCommand = session.Execute(new UseItemCommand(
                new CommandId("station-route.use-field-aid"),
                definition.Protagonist.Id,
                definition.Combat.HealingItem.Id,
                definition.Protagonist.Id));
            EmitCommandResult(output, "use_item", healCommand);
            healCommandAccepted = healCommand.Accepted;
            events.Flush(session);
        }

        if (healCommandAccepted
            && !attackResumedAfterHeal
            && combatObservation.Protagonist.Combat!.Items.Single().Charges == 0
            && combatObservation.Protagonist.CurrentAction is null
            && combatObservation.Hostiles!.Single().Combat.Health > 0)
        {
            var resumedAttack = session.Execute(new AssignBasicAttackTargetCommand(
                new CommandId("station-route.attack-after-field-aid"),
                definition.Protagonist.Id,
                definition.Combat.Hostile.Id));
            EmitCommandResult(output, "assign_basic_attack_target", resumedAttack);
            attackResumedAfterHeal = resumedAttack.Accepted;
            events.Flush(session);
        }

        session.AdvanceTicks(1);
    }

    var securing = RequireStationObservation(session.Observe());
    if (securing.Encounter?.Phase == EncounterPhase.Securing)
    {
        session.AdvanceTicks(definition.Combat.Encounter.SecuringTicks);
    }
    events.Flush(session);

    var afterVictory = RequireStationObservation(session.Observe());
    assertions.Check(
        "combat_is_won_with_attack_ability_and_field_aid",
        attackCommand?.Accepted == true
            && abilityCommand.Accepted
            && resumed.Accepted
            && healCommandAccepted
            && afterVictory.Encounter?.Phase == EncounterPhase.Victory
            && afterVictory.Hostiles!.Single().Combat.Health == 0
            && afterVictory.Objective.Id == definition.SoloExitDoorObjective.Id
            && session.EventsSince(0).Any(gameEvent => gameEvent.Type == GameplayEventType.DamageApplied)
            && session.EventsSince(0).Any(gameEvent => gameEvent.Type == GameplayEventType.HealingApplied)
            && session.EventsSince(0).Any(gameEvent => gameEvent.Type == GameplayEventType.EncounterWon));

    var exitCommand = session.Execute(new InteractCommand(
        new CommandId("station-route.open-solo-exit"),
        definition.Protagonist.Id,
        soloExit.Id));
    EmitCommandResult(output, "interact", exitCommand);
    events.Flush(session);
    var exitAdvance = AdvanceUntil(
        session,
        observation => FindInteraction(observation, soloExit.Id).State == InteractionState.Completed,
        MaximumTicksPerLeg);
    EmitAdvanceResult(output, "open_solo_exit", MaximumTicksPerLeg, exitAdvance);
    events.Flush(session);

    var finalObservation = RequireStationObservation(session.Observe());
    assertions.Check(
        "victory_opens_exit_and_exposes_protector_recruitment",
        exitCommand.Accepted
            && exitAdvance.ConditionReached
            && FindInteraction(finalObservation, soloExit.Id).State == InteractionState.Completed
            && FindInteraction(finalObservation, protector.Id).State == InteractionState.Available
            && FindInteraction(finalObservation, airlock.Id).State == InteractionState.Unavailable);
    assertions.Check(
        "scenario_continues_after_solo_tutorial",
        finalObservation.Phase == ScenarioPhase.InProgress
            && finalObservation.Objective.Id == definition.RecruitmentObjective.Id
            && !session.EventsSince(0).Any(gameEvent => gameEvent.Type == GameplayEventType.ScenarioCompleted));
    assertions.Check(
        "critical_path_remains_within_total_tick_budget",
        session.Tick <= MaximumTicksPerLeg * 5L);

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
            EncounterEventDetail encounter => new
            {
                detail_type = "encounter",
                encounter_id = encounter.EncounterId.Value,
                encounter.Attempt,
            },
            AttackEventDetail attack => new
            {
                detail_type = "attack",
                source_id = attack.SourceId.Value,
                target_id = attack.TargetId.Value,
                attack_id = attack.AttackId.Value,
                attack.Hit,
            },
            AbilityReleasedEventDetail ability => new
            {
                detail_type = "ability_released",
                source_id = ability.SourceId.Value,
                target_position = ObservationProjection.ProjectPosition(ability.TargetPosition),
                ability_id = ability.AbilityId.Value,
                ability.Hit,
            },
            DamageAppliedEventDetail damage => new
            {
                detail_type = "damage_applied",
                source_id = damage.SourceId.Value,
                target_id = damage.TargetId.Value,
                damage.Amount,
                damage.RemainingHealth,
                attack_id = damage.AttackId?.Value,
                ability_id = damage.AbilityId?.Value,
            },
            HealingAppliedEventDetail healing => new
            {
                detail_type = "healing_applied",
                source_id = healing.SourceId.Value,
                target_id = healing.TargetId.Value,
                item_id = healing.ItemId.Value,
                healing.Amount,
                healing.RemainingHealth,
            },
            ActionInterruptedEventDetail interrupted => new
            {
                detail_type = "action_interrupted",
                actor_id = interrupted.ActorId.Value,
                source_id = interrupted.SourceId.Value,
                ability_id = interrupted.AbilityId.Value,
            },
            CombatantDefeatedEventDetail defeated => new
            {
                detail_type = "combatant_defeated",
                combatant_id = defeated.CombatantId.Value,
                source_id = defeated.SourceId.Value,
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
            hostiles = observation.Hostiles?.Select(ProjectHostile).ToArray(),
            encounter = observation.Encounter is null
                ? null
                : new
                {
                    id = observation.Encounter.Id.Value,
                    phase = JsonLinesOutput.ToJsonName(observation.Encounter.Phase),
                    observation.Encounter.Attempt,
                    observation.Encounter.TransitionTicksRemaining,
                    observation.Encounter.TransitionTicksTotal,
                    hostile_id = observation.Encounter.HostileId.Value,
                },
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
            combat = ProjectCombatant(observation.Combat),
        };
    }

    private static object ProjectHostile(HostileObservation observation)
    {
        return new
        {
            id = observation.Id.Value,
            display_name = observation.DisplayName,
            position = ProjectPosition(observation.Position),
            observation.MovementSpeedMetersPerSecond,
            combat = ProjectCombatant(observation.Combat),
            current_action = ProjectAction(observation.CurrentAction),
        };
    }

    private static object? ProjectCombatant(CombatantStateObservation? observation)
    {
        return observation is null
            ? null
            : new
            {
                observation.Health,
                observation.MaximumHealth,
                observation.IsDefeated,
                basic_attack_id = observation.BasicAttackId.Value,
                cooldowns = observation.Cooldowns.Select(cooldown => new
                {
                    ability_id = cooldown.AbilityId.Value,
                    cooldown.RemainingTicks,
                    cooldown.TotalTicks,
                }).ToArray(),
                items = observation.Items.Select(item => new
                {
                    item_id = item.ItemId.Value,
                    item.Charges,
                }).ToArray(),
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
                has_remaining_movement = observation.HasRemainingMovement,
                interaction_target_id = observation.InteractionTargetId?.Value,
                combat_target_id = observation.CombatTargetId?.Value,
                attack_id = observation.AttackId?.Value,
                ability_id = observation.AbilityId?.Value,
                item_id = observation.ItemId?.Value,
                phase = JsonLinesOutput.ToJsonName(observation.Phase),
                observation.PhaseTicksRemaining,
                observation.PhaseTicksTotal,
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
            ["interaction.survivor"] = (new(-8.5, 0, 6.5), new(-9.3, 0, 6.5)),
            ["interaction.service_door.entry"] = (new(-10, 0, 4), new(-10, 0, 4.85)),
            ["interaction.service_door.solo_exit"] = (new(-5, 0, 0), new(-5.85, 0, 0)),
            ["interaction.protector"] = (new(-1.5, 0, 0), new(-2.35, 0, 0)),
            ["interaction.service_terminal"] = (new(-11.5, 0, 6.5), new(-10.65, 0, 6.5)),
            ["interaction.evacuation_airlock"] = (new(12, 0, 8), new(11.15, 0, 8)),
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
            new WorldPosition(-10, 0, 8.5),
            [new StationActorPlacement(definition.Companion.Id, new WorldPosition(-1.5, 0, 0))],
            placements,
            new StationEncounterPlacement(
                definition.Combat.Encounter.Id,
                new WorldPosition(-10, 0, 2.75),
                0.75,
                new WorldPosition(-10, 0, 2.35),
                new WorldPosition(-10, 0, -1.4)));
    }
}

internal sealed class StationRouteFixturePathfinder(IEnumerable<EntityId> actorIds) : ISpatialPathfinder
{
    private const double CoordinateTolerance = 0.0001;
    private static readonly WorldPosition EntryDoor = new(-10, 0, 4);
    private readonly HashSet<EntityId> _actorIds = actorIds.ToHashSet();

    public bool EntryDoorUnlocked { get; set; }

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

        if (originRegion == destinationRegion)
        {
            return SpatialPathResult.Reachable([destination]);
        }

        if ((originRegion == FixtureRegion.StartRoom && destinationRegion == FixtureRegion.SoloArena)
            || (originRegion == FixtureRegion.SoloArena && destinationRegion == FixtureRegion.StartRoom))
        {
            return EntryDoorUnlocked
                ? SpatialPathResult.Reachable([EntryDoor, destination])
                : SpatialPathResult.Unreachable;
        }

        return SpatialPathResult.Unreachable;
    }

    private static bool IsGroundLevel(WorldPosition position)
    {
        return Math.Abs(position.Y) <= CoordinateTolerance;
    }

    private static FixtureRegion GetRegion(WorldPosition position)
    {
        // Evaluation order intentionally gives shared boundaries to the earlier
        // room: StartRoom before SoloArena, then SoloArena before FutureRoute.
        if (position.X >= -13 && position.X <= -7 && position.Z >= 4 && position.Z <= 10)
        {
            return FixtureRegion.StartRoom;
        }

        if (position.X >= -15 && position.X <= -5 && position.Z >= -4 && position.Z <= 4)
        {
            return FixtureRegion.SoloArena;
        }

        if (position.X >= -5.7 && position.X <= 12 && position.Z >= -3 && position.Z <= 13)
        {
            return FixtureRegion.FutureRoute;
        }

        return FixtureRegion.None;
    }

    private enum FixtureRegion
    {
        None = 0,
        StartRoom = 1,
        SoloArena = 2,
        FutureRoute = 3,
    }
}
