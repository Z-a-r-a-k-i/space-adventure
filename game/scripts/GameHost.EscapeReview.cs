using Godot;
using System.Text.Json;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private bool IsEscapeReview => ReviewArgument("review-encounter", "solo") == "escape";

    private void ValidateEscapePresentation(JsonElement diagnostics)
    {
        var route = ReviewState();
        if (route.Encounter?.Phase != EncounterPhase.Active) { return; }
        var medic = route.Party.FirstOrDefault(actor => actor.Id == _definition!.Medic.Id);
        if (medic?.Combat?.IsDefeated == false)
        { ValidateWeapon(diagnostics.GetProperty("medic"), false, medic.Id.Value); }
        foreach (var entry in diagnostics.GetProperty("ranged_enforcers").EnumerateArray())
        {
            var id = entry.GetProperty("actor_id").GetString()!;
            if (route.Hostiles!.Any(enemy => enemy.Id.Value == id && !enemy.Combat.IsDefeated))
            { ValidateWeapon(entry.GetProperty("pose"), true, id); }
        }
        static void ValidateWeapon(JsonElement pose, bool twoHanded, string id)
        {
            if (pose.GetProperty("primary_grip_error_m").GetDouble() > .03
                || twoHanded && pose.GetProperty("support_grip_error_m").GetDouble() > .03)
            { throw new InvalidOperationException($"Armed grip fit exceeded 3 cm for {id}."); }
        }
    }

    private async Task EscapeInteract(string id, bool dialogue = false)
    {
        var target = ReviewState().Interactions.Single(item => item.Id.Value == id);
        await ReviewWaitForPath(target.ApproachPosition);
        ReviewOrder(new InteractCommand(NextHumanCommandId("escape.interact"), _definition!.Protagonist.Id, target.Id));
        await ReviewUntil(state => dialogue ? state.ActiveDialogue?.InteractionId == target.Id
            : state.Interactions.Single(item => item.Id == target.Id).State == InteractionState.Completed, 1200, fast: true);
    }

    private async Task RunEscapeReviewAsync()
    {
        _reviewDrivesClock = true; _camera.InputEnabled = false;
        ReviewOrder(new SetPauseCommand(NextHumanCommandId("escape.pause"), true));
        var protagonist = _definition!.Protagonist.Id;
        await EscapeInteract("interaction.survivor", dialogue: true);
        ReviewOrder(new ChooseDialogueResponseCommand(NextHumanCommandId("escape.choice"), protagonist,
            new EntityId("interaction.survivor"), new DialogueResponseId("response.reroute_service_power")));
        await EscapeInteract("interaction.service_terminal");
        await ReviewWaitForPath(new WorldPosition(-10, 0, 2.75));
        ReviewOrder(new MoveActorCommand(NextHumanCommandId("escape.solo"), protagonist, new WorldPosition(-10, 0, 2.75)));
        await ReviewUntil(state => state.Encounter!.Phase == EncounterPhase.Readying, 600, fast: true);
        await FightEscapeEncounter();
        await EscapeInteract("interaction.service_door.solo_exit");
        await EscapeInteract("interaction.protector", dialogue: true);
        ReviewOrder(new ChooseDialogueResponseCommand(NextHumanCommandId("escape.recruit"), protagonist,
            new EntityId("interaction.protector"), new DialogueResponseId("response.recruit_protector")));
        await ReviewWaitForPath(new WorldPosition(0, 0, 5));
        ReviewOrder(new MovePartyCommand(NextHumanCommandId("escape.party"), ReviewState().Party.Select(actor => actor.Id), new WorldPosition(0, 0, 5)));
        await ReviewUntil(state => state.Encounter!.Id == _definition.Combat.PartyEncounter.Id, 900, fast: true);
        await FightEscapeEncounter();
        await EscapeInteract("interaction.medic", dialogue: true);
        ReviewOrder(new ChooseDialogueResponseCommand(NextHumanCommandId("escape.recruit.medic"), protagonist,
            new EntityId("interaction.medic"), new DialogueResponseId("response.recruit_medic")));
        InputCheck("Medic joins once with two independent skills", ReviewState().Party.Count == 3 && _partyButtons.Count == 3);
        foreach (var placement in CreateEscapePlacements(_definition))
        {
            var area = placement.EncounterId.Value.Split('.').Last();
            await EscapeInteract($"interaction.service_door.{area}");
            await ReviewWaitForPath(placement.TriggerCenter);
            ReviewOrder(new MovePartyCommand(NextHumanCommandId("escape.travel"), ReviewState().Party.Select(actor => actor.Id), placement.TriggerCenter));
            await ReviewUntil(state => state.Encounter!.Id == placement.EncounterId, 1200, fast: true);
            _camera.DistanceMeters = float.Parse(ReviewArgument("review-distance", "14.5"), System.Globalization.CultureInfo.InvariantCulture);
            InputCheck($"{area} starts paused with all crew recovered", _session!.IsPaused
                && ReviewState().Party.All(actor => actor.Combat!.Health == actor.Combat.MaximumHealth));
            if (await ReviewCapture(area)) { return; }
            if (_reviewMode == "performance" && area == "launch")
            {
                await ReviewTicks(_definition.Combat.Encounters[^1].ReadyingTicks);
                await RunRealtimePerformanceAsync(); return;
            }
            if (area == "service" && _reviewCheckpoint is "armed" or "ready")
            {
                if (_reviewCheckpoint == "armed") { await ReviewTicks(_definition.Combat.Encounters[2].ReadyingTicks); }
                if (await ReviewCapture(_reviewCheckpoint)) { return; }
            }
            if (_reviewSequence == "defeat" && area is "security" or "launch")
            {
                var completed = ReviewState().CompletedEncounterIds!.Count;
                await ReviewUntil(state => state.Encounter!.Phase == EncounterPhase.Defeat, 2400, fast: true);
                CheckVisionPresentation();
                if (await ReviewCapture("defeat")) { return; }
                ReviewOrder(new RestartEncounterCommand(NextHumanCommandId("escape.retry"), placement.EncounterId));
                CheckVisionPresentation();
                InputCheck($"{area} retry preserves earlier encounters", ReviewState().CompletedEncounterIds!.Count == completed
                    && ReviewState().Party.Count == 3 && ReviewState().Encounter!.HealingField is null);
                if (await ReviewCapture("retry")) { return; }
            }
            if (_reviewMode == "input" && area == "service") { await CheckMedicInput(); }
            if (await FightEscapeEncounter(captureHealing: area == "service")) { return; }
        }
        InputCheck("All six fights complete before airlock", ReviewState().CompletedEncounterIds!.Count == 6);
        await EscapeInteract("interaction.evacuation_airlock");
        await ReviewWaitForPath(ReviewState().Interactions.Single(item => item.Id.Value == "interaction.escape_cutter.board").ApproachPosition);
        _camera.FocusOn(new Vector3(79, 0, 8)); _camera.DistanceMeters = 20;
        if (await ReviewCapture("boarding")) { return; }
        await EscapeInteract("interaction.escape_cutter.board");
        InputCheck("Boarding completes exactly once with all crew", ReviewState().Phase == ScenarioPhase.Completed
            && _session!.EventsSince(0).Count(item => item.Type == GameplayEventType.ScenarioCompleted) == 1);
        if (_reviewMode == "input")
        {
            var commands = _humanCommandSequence;
            await InputKey(Key.F1); await InputKey(Key.Key1); await InputKey(Key.Space);
            InputCheck("Departure blocks gameplay and manual shortcuts", !_controlsOverlay.Visible
                && !_abilityTargeting && commands == _humanCommandSequence && !_camera.InputEnabled);
        }
        if (ShipHandoffReview) { await RunShipHandoffAfterBoardingAsync(); return; }
        for (var frame = 0; frame < 480; frame++)
        {
            AdvanceDeparture(_session!.Observe(), 1.0 / 60);
            if (_reviewMode != "smoke") { await InputFrame(); }
            if (frame == 310 && await ReviewCapture("departure")) { return; }
        }
        AdvanceDeparture(_session!.Observe(), .1);
        InputCheck("Departure ends with the completion summary", _completionOverlay.Visible);
        if (await ReviewCapture("complete")) { return; }
        await FinishSoloReview();
    }

    private async Task<bool> FightEscapeEncounter(bool captureHealing = false)
    {
        CheckActiveEncounterPitCommands();
        _session!.Execute(new SetPauseCommand(NextHumanCommandId("escape.fight.resume"), false));
        var capturedHeal = false; var capturedField = false;
        for (var tick = 0; tick < 2400 && ReviewState().Encounter!.Phase is EncounterPhase.Readying or EncounterPhase.Active or EncounterPhase.Securing; tick++)
        {
            var route = ReviewState();
            if (route.Encounter!.Phase == EncounterPhase.Active)
            foreach (var actor in route.Party.Where(crew => !crew.Combat!.IsDefeated))
            {
                var target = route.VisibleHostiles.Where(enemy => enemy.EncounterId == route.Encounter!.Id && !enemy.Combat.IsDefeated).OrderBy(enemy => enemy.Combat.Health).ThenBy(enemy => enemy.Id.Value, StringComparer.Ordinal).FirstOrDefault();
                if (target is null) { continue; }
                if (actor.Combat!.RememberedAttackTargetId is null)
                { _session.Execute(new AssignBasicAttackTargetCommand(NextHumanCommandId("escape.attack"), actor.Id, target.Id)); }
                if (actor.CurrentAction?.Kind == PrimaryActionKind.Ability || actor.PendingAction?.Kind == PrimaryActionKind.Ability) { continue; }
                bool Ready(AbilityId id) => actor.Combat.Cooldowns.Any(cd => cd.AbilityId == id && cd.RemainingTicks == 0);
                var kit = _definition!.Combat;
                AbilityId? ability = null; AbilityTarget? aim = null;
                if (actor.Id == _definition.Protagonist.Id)
                {
                    if (Ready(kit.Burst.Id) && actor.Position.DistanceTo(target.Position) <= kit.Burst.RangeMeters)
                    { ability = kit.Burst.Id; aim = new EntityAbilityTarget(target.Id); }
                    else if (Ready(kit.ProtagonistAbility.Id) && actor.Position.DistanceTo(target.Position) <= kit.ProtagonistAbility.RangeMeters)
                    { ability = kit.ProtagonistAbility.Id; aim = new PositionAbilityTarget(target.Position); }
                }
                else if (actor.Id == _definition.Companion.Id)
                {
                    if (Ready(kit.Taunt.Id)) { ability = kit.Taunt.Id; aim = new SelfAbilityTarget(); }
                    else if (Ready(kit.Barrier.Id))
                    {
                        var direction = route.Encounter.Id == kit.PartyEncounter.Id ? new WorldPosition(0, 0, 1) : new WorldPosition(1, 0, 0);
                        var position = new WorldPosition(actor.Position.X + direction.X * .8, 0, actor.Position.Z + direction.Z * .8);
                        var barrier = new BarrierAbilityTarget(position, direction);
                        if (_session.CheckBarrierPlacement(actor.Id, barrier) is null) { ability = kit.Barrier.Id; aim = barrier; }
                    }
                }
                else
                {
                    var ally = route.Party.Where(crew => !crew.Combat!.IsDefeated && crew.Position.DistanceTo(actor.Position) <= kit.DirectHeal.RangeMeters)
                        .OrderBy(crew => (double)crew.Combat!.Health / crew.Combat.MaximumHealth).First();
                    if (Ready(kit.DirectHeal.Id) && ally.Combat!.MaximumHealth - ally.Combat.Health >= 20)
                    { ability = kit.DirectHeal.Id; aim = new EntityAbilityTarget(ally.Id); }
                    else if (Ready(kit.HealingField.Id) && actor.Position.DistanceTo(route.Party.Single(crew => crew.Id == _definition.Companion.Id).Position) <= kit.HealingField.RangeMeters)
                    { ability = kit.HealingField.Id; aim = new PositionAbilityTarget(route.Party.Single(crew => crew.Id == _definition.Companion.Id).Position); }
                }
                if (ability is { } id && aim is not null) { _session.Execute(new UseAbilityCommand(NextHumanCommandId("escape.skill"), actor.Id, id, aim)); }
            }
            _session.AdvanceTicks(1); _reviewSampleTick = _session.Tick;
            SynchronizePresentation(); AdvanceServiceDoorPresentation(1.0f / 30);
            if (_reviewMode == "record") { await InputFrame(); await InputFrame(); }
            string? checkpoint = captureHealing && !capturedHeal && _session.EventsSince(0).Any(item => item.Detail is HealingAppliedEventDetail healing && IsHealingAbility(healing.AbilityId)) ? "heal"
                : captureHealing && !capturedField && ReviewState().Encounter!.HealingField is not null ? "field" : null;
            if (checkpoint is not null)
            {
                if (checkpoint == "heal") { capturedHeal = true; } else { capturedField = true; }
                _session.Execute(new SetPauseCommand(NextHumanCommandId("escape.capture.pause"), true));
                if (await ReviewCapture(checkpoint)) { return true; }
                _session.Execute(new SetPauseCommand(NextHumanCommandId("escape.capture.resume"), false));
            }
        }
        InputCheck("Coordinated crew wins authored encounter", ReviewState().Encounter!.Phase == EncounterPhase.Victory);
        if (captureHealing)
        { InputCheck("Medic releases direct healing and a persistent field", capturedHeal && capturedField); }
        _session.Execute(new SetPauseCommand(NextHumanCommandId("escape.travel.pause"), true));
        return false;
    }

    private async Task CheckMedicInput()
    {
        await InputFrame();
        await InputKey(Key.Tab); await InputKey(Key.Tab);
        InputCheck("Tab focuses the third crew member within the group", _focusedActorId == _definition!.Medic.Id && _selectedActorIds.Count == 3);
        await InputClick(_partyButtons[_definition!.Medic.Id.Value].GetGlobalRect().GetCenter(), MouseButton.Left);
        InputCheck("Medic portrait focuses healing kit", _focusedActorId == _definition.Medic.Id);
        await InputKey(Key.Key1);
        await InputClick(_partyButtons[_definition.Protagonist.Id.Value].GetGlobalRect().GetCenter(), MouseButton.Left);
        InputCheck("Ally portrait queues Heal without changing focus", _focusedActorId == _definition.Medic.Id
            && ReviewState().Party.Single(actor => actor.Id == _definition.Medic.Id).PendingAction?.CombatTargetId == _definition.Protagonist.Id);
        await InputKey(Key.Key2);
        await InputWorldClick(ToGodot(ReviewState().Party.Single(actor => actor.Id == _definition.Medic.Id).Position), MouseButton.Left);
        InputCheck("Ground field replaces pending Heal", IsHealingField(ReviewState().Party.Single(actor => actor.Id == _definition.Medic.Id).PendingAction?.AbilityId));
        CheckHudBounds("three crew medic controls");
    }
}
