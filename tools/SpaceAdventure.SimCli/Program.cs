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
        "station-party" => RunStationRoute(output, party: true),
        "station-escape" => RunStationRoute(output, party: true, complete: true),
        "station-escape-defeat" => RunStationRoute(output, party: true, complete: true, extensionDefeat: true),
        "station-party-defeat" => RunStationRoute(output, party: true, defeat: true),
        _ when SpaceAdventure.SimCli.ShipScenarios.Handles(scenarioId) => SpaceAdventure.SimCli.ShipScenarios.Run(scenarioId, output),
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

static int RunStationRoute(JsonLinesOutput output, bool party = false, bool defeat = false, bool complete = false, bool extensionDefeat = false)
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
        new[] { definition.Protagonist.Id, definition.Companion.Id, definition.Medic.Id }.Concat(definition.Combat.Hostiles.Select(hostile => hostile.Id)));
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
        schema_version = 11,
        scenario_id = complete ? (extensionDefeat ? "station-escape-defeat" : "station-escape") : party ? (defeat ? "station-party-defeat" : "station-party") : StationRouteScenarioId,
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
            && combatObservation.Protagonist.Combat!.Cooldowns.Single(value => value.AbilityId == definition.Combat.ProtagonistAbility.Id).RemainingTicks > 0)
        {
            attackCommand = session.Execute(new AssignBasicAttackTargetCommand(
                new CommandId("station-route.attack-enforcer"),
                definition.Protagonist.Id,
                definition.Combat.SoloHostile.Id));
            EmitCommandResult(output, "assign_basic_attack_target", attackCommand);
            events.Flush(session);
        }

        session.AdvanceTicks(1);
    }

    var securing = RequireStationObservation(session.Observe());
    if (securing.Encounter?.Phase == EncounterPhase.Securing)
    {
        session.AdvanceTicks(definition.Combat.SoloEncounter.SecuringTicks);
    }
    events.Flush(session);

    var afterVictory = RequireStationObservation(session.Observe());
    assertions.Check(
        "combat_is_won_with_attack_and_ability",
        attackCommand?.Accepted == true
            && abilityCommand.Accepted
            && resumed.Accepted
            && afterVictory.Encounter?.Phase == EncounterPhase.Victory
            && afterVictory.Hostiles!.Single().Combat.Health == 0
            && afterVictory.Objective.Id == definition.SoloExitDoorObjective.Id
            && session.EventsSince(0).Any(gameEvent => gameEvent.Type == GameplayEventType.DamageApplied)
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

    if (party)
    {
        pathfinder.SoloExitUnlocked = true;
        RunPartyContinuation(session, definition, events, assertions, output, defeat);
        if (complete) { RunStationCompletion(session, definition, events, assertions, output, extensionDefeat); }
    }

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

static void RunPartyContinuation(GameSession session, StationRouteDefinition definition, GameplayEventOutput events,
    ScenarioAssertions assertions, JsonLinesOutput output, bool defeat)
{
    var protagonist = definition.Protagonist.Id;
    var protector = definition.Companion.Id;
    void Order(IGameCommand command)
    {
        var result = session.Execute(command);
        EmitCommandResult(output, command.GetType().Name, result);
        if (!result.Accepted) { throw new InvalidOperationException($"Party scenario order {command.CommandId}: {result.RejectionCode}"); }
    }
    void Until(Func<StationRouteObservation, bool> condition, int ticks)
    {
        if (!AdvanceUntil(session, condition, ticks).ConditionReached)
        { throw new InvalidOperationException($"Party scenario exceeded {ticks} ticks at {session.Tick}."); }
    }
    StationRouteObservation State() => RequireStationObservation(session.Observe());
    Order(new InteractCommand(new CommandId("party.recruit"), protagonist, new EntityId("interaction.protector")));
    Until(state => state.ActiveDialogue is not null, 600);
    Order(new ChooseDialogueResponseCommand(new CommandId("party.join"), protagonist, new EntityId("interaction.protector"),
        new DialogueResponseId("response.recruit_protector")));
    Order(new SetPauseCommand(new CommandId("party.travel.resume"), false));
    Order(new MovePartyCommand(new CommandId("party.enter"), [protagonist, protector], new WorldPosition(0, 0, 5)));
    Until(state => state.Encounter!.Id == definition.Combat.PartyEncounter.Id, 600);
    assertions.Check("party_entry_pauses_and_resets_both_kits", session.IsPaused && State().Party.Count == 2
        && State().Party.All(actor => actor.Combat!.Health == actor.Combat.MaximumHealth));
    if (!defeat)
    {
        Order(new AssignBasicAttackTargetCommand(new CommandId("party.vanguard.target"), protagonist, new EntityId("actor.enemy.gun_sentry.main")));
        Order(new AssignBasicAttackTargetCommand(new CommandId("party.protector.target"), protector, new EntityId("actor.enemy.security_enforcer.main")));
    }
    Order(new SetPauseCommand(new CommandId("party.resume"), false));
    Until(state => state.Encounter!.Phase == EncounterPhase.Active, 30);
    if (defeat)
    {
        Until(state => state.Party.Any(actor => actor.Combat!.IsDefeated), 900);
        assertions.Check("one_down_does_not_end_party_fight", State().Encounter!.Phase == EncounterPhase.Active);
        Until(state => state.Encounter!.Phase == EncounterPhase.Defeat, 1200);
        Order(new RestartEncounterCommand(new CommandId("party.retry"), definition.Combat.PartyEncounter.Id));
        assertions.Check("party_retry_preserves_route_and_restores_all_combatants", session.IsPaused && State().Encounter!.Attempt == 2
            && State().Party.Count == 2 && State().Party.All(actor => actor.Combat!.Health == actor.Combat.MaximumHealth)
            && State().Hostiles!.All(hostile => hostile.Combat.Health == hostile.Combat.MaximumHealth)
            && State().RoutePowerMode != RoutePowerMode.Unset);
    }
    else
    {
        session.AdvanceTicks(definition.Combat.GetAttack(definition.Companion.Loadout!.BasicAttackId).WindupTicks);
        Order(new UseAbilityCommand(new CommandId("party.barrier"), protector, definition.Combat.Barrier.Id,
            new BarrierAbilityTarget(new WorldPosition(.55, 0, 5.3), new WorldPosition(0, 0, 1))));
        session.AdvanceTicks(definition.Combat.Barrier.WindupTicks);
        assertions.Check("barrier_observes_fixed_position_and_facing", State().Encounter!.Barrier is { } barrier
            && barrier.SourceId == protector && barrier.Position == definition.Combat.Barrier.CenterAt(new WorldPosition(.55, 0, 5.3)));
        Order(new UseAbilityCommand(new CommandId("party.taunt"), protector, definition.Combat.Taunt.Id, new SelfAbilityTarget()));
        session.AdvanceTicks(definition.Combat.Taunt.WindupTicks);
        assertions.Check("taunt_redirects_nearby_hostiles", State().Hostiles!.All(enemy => enemy.Combat.TauntedBy == protector));
        Order(new UseAbilityCommand(new CommandId("party.burst"), protagonist, definition.Combat.Burst.Id,
            new EntityAbilityTarget(new EntityId("actor.enemy.gun_sentry.main"))));
        for (var tick = 0; tick < 1500 && State().Encounter!.Phase == EncounterPhase.Active; tick++)
        {
            var state = State();
            foreach (var actor in state.Party.Where(actor => !actor.Combat!.IsDefeated))
            {
                if (actor.Combat!.RememberedAttackTargetId is null && actor.PendingAction is null)
                {
                    var target = state.VisibleHostiles.FirstOrDefault(hostile => hostile.EncounterId == state.Encounter!.Id && !hostile.Combat.IsDefeated);
                    if (target is not null) { Order(new AssignBasicAttackTargetCommand(new CommandId($"party.retarget.{actor.Id}.{session.Tick}"), actor.Id, target.Id)); }
                }
            }
            session.AdvanceTicks(1);
        }
        Until(state => state.Encounter!.Phase == EncounterPhase.Victory, 60);
        assertions.Check("party_victory_opens_medic_recruitment", State().Objective.Id == definition.MedicRecruitmentObjective.Id
            && State().Hostiles!.All(hostile => hostile.Combat.IsDefeated) && State().Phase == ScenarioPhase.InProgress
            && FindInteraction(State(), new EntityId("interaction.evacuation_airlock")).State == InteractionState.Unavailable);
    }
    events.Flush(session);
}

static void RunStationCompletion(GameSession session, StationRouteDefinition definition, GameplayEventOutput events,
    ScenarioAssertions assertions, JsonLinesOutput output, bool forceDefeat)
{
    var protagonist = definition.Protagonist.Id;
    var protector = definition.Companion.Id;
    var medic = definition.Medic.Id;
    var combat = definition.Combat;
    StationRouteObservation State() => RequireStationObservation(session.Observe());
    void Order(IGameCommand command)
    {
        var result = session.Execute(command);
        if (!result.Accepted) { throw new InvalidOperationException($"Completion order {command.CommandId}: {result.RejectionCode}"); }
    }
    void Until(Func<StationRouteObservation, bool> done, int ticks)
    {
        for (var index = 0; index < ticks && !done(State()); index++) { session.AdvanceTicks(1); }
        if (!done(State())) { throw new InvalidOperationException($"Completion route timed out at {session.Tick}: {State().Encounter!.Id} {State().Encounter!.Phase}."); }
    }
    var medicInteraction = new EntityId("interaction.medic");
    Order(new InteractCommand(new CommandId("complete.recruit"), protagonist, medicInteraction));
    Until(state => state.ActiveDialogue is not null, 600);
    Order(new ChooseDialogueResponseCommand(new CommandId("complete.join"), protagonist, medicInteraction, new DialogueResponseId("response.recruit_medic")));
    assertions.Check("medic_joins_with_two_independent_healing_skills", State().Party.Count == 3
        && State().Party.Single(actor => actor.Id == medic).Combat!.Cooldowns.Count == 2);
    foreach (var placement in StationRouteFixture.CreateExtensionEncounters(definition))
    {
        Order(new MovePartyCommand(new CommandId($"complete.enter.{placement.EncounterId}"), [protagonist, protector, medic], placement.TriggerCenter));
        Order(new SetPauseCommand(new CommandId("complete.travel.resume"), false));
        Until(state => state.Encounter!.Id == placement.EncounterId, 1000);
        assertions.Check($"{placement.EncounterId}.ready", session.IsPaused && State().Encounter!.Phase == EncounterPhase.Readying);
        Order(new SetPauseCommand(new CommandId("complete.combat.resume"), false));
        Until(state => state.Encounter!.Phase == EncounterPhase.Active, 30);
        if (forceDefeat && placement.EncounterId == combat.Encounters[2].Id)
        {
            Order(new UseAbilityCommand(new CommandId("complete.defeat.field"), medic, combat.HealingField.Id,
                new PositionAbilityTarget(State().Party.Single(actor => actor.Id == medic).Position)));
            Until(state => state.Encounter!.Phase == EncounterPhase.Defeat, 1800);
            Order(new RestartEncounterCommand(new CommandId("complete.retry"), placement.EncounterId));
            assertions.Check("extension_retry_preserves_two_victories_and_three_crew", State().CompletedEncounterIds!.Count == 2
                && State().Party.Count == 3 && State().Encounter!.HealingField is null && State().Encounter!.Projectiles!.Count == 0
                && State().Party.All(actor => actor.Combat!.Health == actor.Combat.MaximumHealth));
            // Retry restarts this encounter from its authored placements without carried-over orders.
            assertions.Check("extension_retry_restores_placements_and_clears_orders",
                State().Party.All(actor => actor.CurrentAction is null && actor.PendingAction is null
                    && actor.Combat!.RememberedAttackTargetId is null
                    && actor.Position == placement.CrewRestartPositions!.Single(item => item.ActorId == actor.Id).Position)
                && State().Hostiles!.All(hostile => hostile.CurrentAction is null && hostile.Combat.Health == hostile.Combat.MaximumHealth
                    && hostile.Position == placement.HostilePlacements!.Single(item => item.ActorId == hostile.Id).Position));
            Order(new SetPauseCommand(new CommandId("complete.retry.resume"), false));
            Until(state => state.Encounter!.Phase == EncounterPhase.Active, 30);
        }
        for (var tick = 0; tick < 2000 && State().Encounter!.Phase == EncounterPhase.Active; tick++)
        {
            var state = State();
            foreach (var actor in state.Party.Where(member => !member.Combat!.IsDefeated))
            {
                var target = state.VisibleHostiles.Where(enemy => enemy.EncounterId == state.Encounter!.Id && !enemy.Combat.IsDefeated)
                    .OrderBy(enemy => enemy.Combat.Health).ThenBy(enemy => enemy.Id.Value, StringComparer.Ordinal).FirstOrDefault();
                if (target is null) { continue; }
                if (actor.Combat!.RememberedAttackTargetId is null)
                { Order(new AssignBasicAttackTargetCommand(new CommandId($"complete.attack.{actor.Id}.{session.Tick}"), actor.Id, target.Id)); }
                if (actor.CurrentAction?.Kind == PrimaryActionKind.Ability || actor.PendingAction?.Kind == PrimaryActionKind.Ability) { continue; }
                bool Ready(AbilityId id) => actor.Combat.Cooldowns.Single(cd => cd.AbilityId == id).RemainingTicks == 0;
                void Skill(AbilityId id, AbilityTarget aim) => Order(new UseAbilityCommand(new CommandId($"complete.skill.{id}.{session.Tick}"), actor.Id, id, aim));
                if (actor.Id == protagonist)
                {
                    if (Ready(combat.Burst.Id) && actor.Position.DistanceTo(target.Position) <= combat.Burst.RangeMeters)
                    { Skill(combat.Burst.Id, new EntityAbilityTarget(target.Id)); }
                    else if (Ready(combat.ProtagonistAbility.Id) && actor.Position.DistanceTo(target.Position) <= combat.ProtagonistAbility.RangeMeters)
                    { Skill(combat.ProtagonistAbility.Id, new PositionAbilityTarget(target.Position)); }
                }
                else if (actor.Id == protector)
                {
                    if (Ready(combat.Taunt.Id)) { Skill(combat.Taunt.Id, new SelfAbilityTarget()); }
                    else if (Ready(combat.Barrier.Id)) { Skill(combat.Barrier.Id,
                        new BarrierAbilityTarget(new WorldPosition(actor.Position.X + .8, 0, actor.Position.Z), new WorldPosition(1, 0, 0))); }
                }
                else
                {
                    var injured = state.Party.Where(member => !member.Combat!.IsDefeated && member.Position.DistanceTo(actor.Position) <= combat.DirectHeal.RangeMeters)
                        .OrderBy(member => (double)member.Combat!.Health / member.Combat.MaximumHealth).First();
                    if (Ready(combat.DirectHeal.Id) && injured.Combat!.MaximumHealth - injured.Combat.Health >= 20)
                    { Skill(combat.DirectHeal.Id, new EntityAbilityTarget(injured.Id)); }
                    else if (Ready(combat.HealingField.Id))
                    {
                        var center = state.Party.Single(member => member.Id == protector).Position;
                        if (center.DistanceTo(actor.Position) <= combat.HealingField.RangeMeters) { Skill(combat.HealingField.Id, new PositionAbilityTarget(center)); }
                    }
                }
            }
            session.AdvanceTicks(1);
        }
        Until(state => state.Encounter!.Phase == EncounterPhase.Victory, 60);
        assertions.Check($"{placement.EncounterId}.victory_recovers_crew", State().Party.All(actor => actor.Combat!.Health == actor.Combat.MaximumHealth
            && !actor.Combat.IsDefeated && actor.Combat.Cooldowns.All(cd => cd.RemainingTicks == 0)));
        events.Flush(session);
    }
    var airlock = new EntityId("interaction.evacuation_airlock");
    Order(new InteractCommand(new CommandId("complete.airlock"), protagonist, airlock));
    Until(state => FindInteraction(state, airlock).State == InteractionState.Completed, 600);
    var board = new EntityId("interaction.escape_cutter.board");
    Order(new InteractCommand(new CommandId("complete.board"), protagonist, board));
    Until(state => state.Phase == ScenarioPhase.Completed, 1000);
    events.Flush(session);
    assertions.Check("six_victories_and_all_crew_boarded_complete_station", State().CompletedEncounterIds!.Count == 6
        && State().Party.All(actor => actor.Position.DistanceTo(FindInteraction(State(), board).Position) <= FindInteraction(State(), board).UseRadiusMeters)
        && State().Phase == ScenarioPhase.Completed);
    output.Emit(new { kind = "station_completion", completed_encounters = State().CompletedEncounterIds!.Select(id => id.Value), party_count = State().Party.Count });
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
            TauntEventDetail taunt => new { detail_type = "taunt", source_id = taunt.SourceId.Value, target_id = taunt.TargetId.Value, taunt.DurationTicks },
            BarrierEventDetail barrier => new
            {
                detail_type = "barrier", source_id = barrier.SourceId.Value,
                position = ObservationProjection.ProjectPosition(barrier.Position),
                facing = ObservationProjection.ProjectPosition(barrier.Facing),
                ability_id = barrier.AbilityId.Value, end_reason = barrier.EndReason?.ToString(),
            },
            ProjectileEventDetail projectile => new
            {
                detail_type = "projectile", projectile.Id, source_id = projectile.SourceId.Value, target_id = projectile.TargetId.Value,
                attack_id = projectile.AttackId.Value,
                origin = ObservationProjection.ProjectPosition(projectile.Origin),
                destination = ObservationProjection.ProjectPosition(projectile.Destination),
                impact_position = projectile.ImpactPosition is { } impact ? ObservationProjection.ProjectPosition(impact) : null,
                projectile.FlightTicks, projectile.Blocked,
            },
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
                target_id = ability.TargetId?.Value,
                target_position = ObservationProjection.ProjectPosition(ability.TargetPosition),
                ability_id = ability.AbilityId.Value,
                ability.Hit,
            },
            HealingAppliedEventDetail healing => new
            {
                detail_type = "healing_applied", source_id = healing.SourceId.Value, target_id = healing.TargetId.Value,
                ability_id = healing.AbilityId.Value, healing.Amount, healing.RemainingHealth,
            },
            HealingFieldEventDetail field => new
            {
                detail_type = "healing_field", source_id = field.SourceId.Value, ability_id = field.AbilityId.Value,
                position = ObservationProjection.ProjectPosition(field.Position), field.RadiusMeters, field.DurationTicks,
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
            completed_encounter_ids = observation.CompletedEncounterIds?.Select(id => id.Value).ToArray(),
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
                    secondary_ability_id = kit.SecondaryAbilityId.Value,
                    kit.SecondaryAbilityName,
                    secondary_ability_target_kind = JsonLinesOutput.ToJsonName(kit.SecondaryAbilityTargetKind),
            }).ToArray(),
            selected_protagonist_kit_id = observation.SelectedProtagonistKit?.Id.Value,
            route_power_mode = JsonLinesOutput.ToJsonName(observation.RoutePowerMode),
            objective = ProjectObjective(observation.Objective),
            interactions = observation.Interactions.Select(ProjectInteraction).ToArray(),
            active_dialogue = observation.ActiveDialogue is null
                ? null
                : ProjectDialogue(observation.ActiveDialogue),
            hostiles = observation.Hostiles?.Select(ProjectHostile).ToArray(),
            visible_hostiles = observation.VisibleHostiles.Select(ProjectHostile).ToArray(),
            encounter = observation.Encounter is null
                ? null
                : new
                {
                    id = observation.Encounter.Id.Value,
                    phase = JsonLinesOutput.ToJsonName(observation.Encounter.Phase),
                    observation.Encounter.Attempt,
                    observation.Encounter.TransitionTicksRemaining,
                    observation.Encounter.TransitionTicksTotal,
                    observation.Encounter.PhaseStartedTick,
                    hostile_ids = observation.Encounter.HostileIds.Select(id => id.Value),
                    barrier = observation.Encounter.Barrier is { } barrier ? new
                    {
                        source_id = barrier.SourceId.Value, position = ProjectPosition(barrier.Position), facing = ProjectPosition(barrier.Facing),
                        remaining_ticks = barrier.RemainingTicks, total_ticks = barrier.TotalTicks,
                        width_meters = barrier.WidthMeters, height_meters = barrier.HeightMeters, deployed_at_tick = barrier.DeployedAtTick,
                    } : null,
                    healing_field = observation.Encounter.HealingField is { } field ? new
                    {
                        source_id = field.SourceId.Value, position = ProjectPosition(field.Position), field.RadiusMeters,
                        field.DeployedAtTick, field.RemainingTicks, field.TotalTicks, field.PulseIntervalTicks,
                    } : null,
                    projectiles = observation.Encounter.Projectiles?.Select(projectile => new
                    {
                        projectile.Id, source_id = projectile.SourceId.Value, target_id = projectile.TargetId.Value,
                        attack_id = projectile.AttackId.Value,
                        origin = ProjectPosition(projectile.Origin), destination = ProjectPosition(projectile.Destination),
                        position = ProjectPosition(projectile.Position), projectile.ReleasedAtTick, projectile.FlightTicks,
                    }).ToArray(),
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
                    secondary_ability_id = observation.Loadout.SecondaryAbilityId.Value,
                    observation.Loadout.SecondaryAbilityName,
                    secondary_ability_target_kind = JsonLinesOutput.ToJsonName(observation.Loadout.SecondaryAbilityTargetKind),
                },
            position = ProjectPosition(observation.Position),
            current_action = ProjectAction(observation.CurrentAction),
            pending_action = ProjectAction(observation.PendingAction),
            facing = ProjectPosition(observation.Facing),
            combat = ProjectCombatant(observation.Combat),
        };
    }

    private static object ProjectHostile(HostileObservation observation)
    {
        return new
        {
            id = observation.Id.Value,
            display_name = observation.DisplayName,
            encounter_id = observation.EncounterId.Value,
            encounter_phase = JsonLinesOutput.ToJsonName(observation.EncounterPhase),
            observation.EncounterAttempt,
            facing = ProjectPosition(observation.Facing),
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
                remembered_attack_target_id = observation.RememberedAttackTargetId?.Value,
                observation.OffensiveRecoveryUntilTick, observation.DefeatedAtTick,
                taunted_by = observation.TauntedBy?.Value, observation.TauntRemainingTicks,
                basic_attack_id = observation.BasicAttackId.Value,
                cooldowns = observation.Cooldowns.Select(cooldown => new
                {
                    ability_id = cooldown.AbilityId.Value,
                    cooldown.RemainingTicks,
                    cooldown.TotalTicks,
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
                ability_facing = observation.AbilityFacing is { } facing ? ProjectPosition(facing) : null,
                attack_id = observation.AttackId?.Value,
                ability_id = observation.AbilityId?.Value,
                phase = JsonLinesOutput.ToJsonName(observation.Phase),
                observation.PhaseTicksRemaining,
                observation.PhaseTicksTotal,
                observation.InstanceId,
                observation.PhaseStartedTick,
                observation.Interrupted,
                waiting_reason = observation.WaitingReason is null ? null : JsonLinesOutput.ToJsonName(observation.WaitingReason.Value),
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
    private static readonly int[] ExtensionCenters = [18, 32, 46, 62];

    public static StationEncounterPlacement[] CreateExtensionEncounters(StationRouteDefinition definition) => definition.Combat.Encounters.Skip(2)
        .Select((encounter, index) =>
        {
            double center = ExtensionCenters[index];
            var crew = encounter.RequiredCrewIds!.Select((id, crewIndex) => new StationActorPlacement(id, new WorldPosition(center - 4, 0, 7 + crewIndex))).ToArray();
            var hostiles = encounter.HostileIds.Select((id, enemyIndex) => new StationHostilePlacement(id,
                new WorldPosition(center + 1 + enemyIndex / 3 * 2, 0, 5.5 + enemyIndex % 3 * 2.5), new WorldPosition(-1, 0, 0))).ToArray();
            return new StationEncounterPlacement(encounter.Id, new WorldPosition(center - 4, 0, 8), 3,
                crew[0].Position, hostiles[0].Position, CrewRestartPositions: crew, HostilePlacements: hostiles);
        }).ToArray();

    private static readonly Dictionary<string, (WorldPosition Position, WorldPosition Approach)>
        InteractionPlacements = new Dictionary<string, (WorldPosition, WorldPosition)>(StringComparer.Ordinal)
        {
            ["interaction.survivor"] = (new(-8.5, 0, 6.5), new(-9.3, 0, 6.5)),
            ["interaction.service_door.entry"] = (new(-10, 0, 4), new(-10, 0, 4.85)),
            ["interaction.service_door.solo_exit"] = (new(-5, 0, 0), new(-5.85, 0, 0)),
            ["interaction.protector"] = (new(-1.5, 0, 0), new(-2.35, 0, 0)),
            ["interaction.service_terminal"] = (new(-11.5, 0, 6.5), new(-10.65, 0, 6.5)),
            ["interaction.evacuation_airlock"] = (new(73, 0, 8), new(72.15, 0, 8)),
            ["interaction.medic"] = (new(9, 0, 8), new(8.15, 0, 8)),
            ["interaction.escape_cutter.board"] = (new(76.5, 0, 8), new(76.5, 0, 8)),
            ["interaction.service_door.service"] = (new(12, 0, 8), new(11.15, 0, 8)),
            ["interaction.service_door.security"] = (new(25, 0, 8), new(24.15, 0, 8)),
            ["interaction.service_door.dock"] = (new(39, 0, 8), new(38.15, 0, 8)),
            ["interaction.service_door.launch"] = (new(53, 0, 8), new(52.15, 0, 8)),
        };

    public static StationRouteLayout CreateLayout(StationRouteDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var partyPositions = new Dictionary<EntityId, WorldPosition>
        {
            [new EntityId("actor.enemy.security_enforcer.main")] = new(-2, 0, 8),
            [new EntityId("actor.enemy.gun_sentry.main")] = new(2.5, 0, 10.5),
        };
        var partyHostiles = definition.Combat.PartyEncounter.HostileIds
            .Select(id => partyPositions.TryGetValue(id, out var position)
                ? new StationActorPlacement(id, position)
                : throw new InvalidDataException($"The deterministic station-route fixture has no hostile placement for '{id}'.")).ToArray();
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
            [new StationActorPlacement(definition.Companion.Id, new WorldPosition(-1.5, 0, 0)),
                new StationActorPlacement(definition.Medic.Id, new WorldPosition(9, 0, 8))],
            placements,
            new StationEncounterPlacement(
                definition.Combat.SoloEncounter.Id,
                new WorldPosition(-10, 0, 2.75),
                0.75,
                new WorldPosition(-10, 0, 2.35),
                new WorldPosition(-10, 0, -1.4)),
            new StationEncounterPlacement(definition.Combat.PartyEncounter.Id, new WorldPosition(0, 0, 5), 2,
                new WorldPosition(-.55, 0, 4.5), partyHostiles[0].Position, new WorldPosition(.55, 0, 4.5),
                partyHostiles.Skip(1).ToArray(), new WorldPosition(0, 0, -1)), CreateExtensionEncounters(definition));
    }
}

internal sealed class StationRouteFixturePathfinder(IEnumerable<EntityId> actorIds) : ISpatialPathfinder
{
    public bool SoloExitUnlocked { get; set; }
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

        if ((originRegion == FixtureRegion.SoloArena && destinationRegion == FixtureRegion.FutureRoute)
            || (originRegion == FixtureRegion.FutureRoute && destinationRegion == FixtureRegion.SoloArena))
        {
            return SoloExitUnlocked ? SpatialPathResult.Reachable([new WorldPosition(-5, 0, 0), destination])
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

        if (position.X >= -5.7 && position.X <= 86 && position.Z >= -3 && position.Z <= 13)
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
