using Godot;
using SpaceAdventure.Core;
using System.Text.Json;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private bool IsPartyReview => ReviewArgument("review-encounter", "solo") == "party";

    private async Task RunPartyReviewAsync()
    {
        _reviewDrivesClock = true;
        _camera.InputEnabled = false;
        var protagonist = _definition!.Protagonist.Id;
        var protector = _definition.Companion.Id;
        if (_reviewMode == "smoke") { CheckPartyHostilePlacementOrder(); }
        ReviewOrder(new SetPauseCommand(new CommandId("party.setup.pause"), true));
        ReviewOrder(new InteractCommand(new CommandId("party.setup.survivor"), protagonist, new EntityId("interaction.survivor")));
        await ReviewUntil(state => state.ActiveDialogue is not null, 300, fast: true);
        ReviewOrder(new ChooseDialogueResponseCommand(new CommandId("party.setup.choice"), protagonist,
            new EntityId("interaction.survivor"), new DialogueResponseId("response.reroute_service_power")));
        await ReviewWaitForPath(new WorldPosition(-10, 0, 2.75));
        ReviewOrder(new MoveActorCommand(new CommandId("party.setup.enter.solo"), protagonist, new WorldPosition(-10, 0, 2.75)));
        await ReviewUntil(state => state.Encounter!.Phase == EncounterPhase.Readying, 300, fast: true);
        ReviewAttack("party.setup.solo.attack");
        await ReviewUntil(state => state.Encounter!.Phase == EncounterPhase.Victory, 1200, fast: true);
        ReviewOrder(new InteractCommand(new CommandId("party.setup.exit"), protagonist, new EntityId("interaction.service_door.solo_exit")));
        await ReviewUntil(state => state.Objective.Id.Value == "objective.recruit_protector", 300, fast: true);
        await ReviewWaitForPath(ReviewState().Interactions.Single(item => item.Id.Value == "interaction.protector").ApproachPosition);
        ReviewOrder(new InteractCommand(new CommandId("party.setup.protector"), protagonist, new EntityId("interaction.protector")));
        await ReviewUntil(state => state.ActiveDialogue is not null, 300, fast: true);
        ReviewOrder(new ChooseDialogueResponseCommand(new CommandId("party.setup.recruit"), protagonist,
            new EntityId("interaction.protector"), new DialogueResponseId("response.recruit_protector")));
        await ReviewWaitForPath(new WorldPosition(0, 0, 5));
        ReviewOrder(new MovePartyCommand(new CommandId("party.setup.enter.main"), [protagonist, protector], new WorldPosition(0, 0, 5)));
        await ReviewUntil(state => state.Encounter!.Id == _definition.Combat.PartyEncounter.Id, 600, fast: true);
        _camera.DistanceMeters = float.Parse(ReviewArgument("review-distance", "14.5"), System.Globalization.CultureInfo.InvariantCulture);
        if (_camera.DistanceMeters < 10) { _camera.FocusOn(ToGodot(ReviewState().Party[1].Position)); }
        await InputFrame();
        InputCheck("party entry resets and pauses both crew", _session!.IsPaused && ReviewState().Party.Count == 2
            && ReviewState().Party.All(actor => actor.Combat!.Health == actor.Combat.MaximumHealth));
        if (_reviewMode == "smoke") { CheckPartyAbilityEnvelope(); }
        if (await ReviewCapture("ready")) { return; }
        await ReviewTicks(_definition.Combat.PartyEncounter.ReadyingTicks / 2);
        if (await ReviewCapture("draw")) { return; }
        await ReviewTicks(_definition.Combat.PartyEncounter.ReadyingTicks
            - _definition.Combat.PartyEncounter.ReadyingTicks / 2);
        CheckSentryMountAndCloseTargets();
        if (_reviewMode == "input") { await CheckWorldHealthInput(); }
        if (await ReviewCapture("armed")) { return; }
        if (_reviewMode == "performance") { await RunRealtimePerformanceAsync(); return; }
        if (_reviewSequence == "defeat")
        {
            await ReviewUntil(state => state.Party.Any(actor => actor.Combat!.IsDefeated), 900, fast: true);
            if (_reviewMode == "input")
            {
                await InputFrame();
                await InputFrame();
                CheckHudBounds("group with one crew member down");
            }
            InputCheck("one crew member down keeps encounter active", ReviewState().Encounter!.Phase == EncounterPhase.Active);
            var fallen = ReviewState().Party.Single(member => member.Combat!.IsDefeated);
            var fallenPresentation = ArmedPresentation(fallen.Id)!;
            if (_reviewMode == "input")
            {
                await InputClick(_partyButtons[fallen.Id.Value].GetGlobalRect().GetCenter(), MouseButton.Left);
                CheckHudBounds("one crew member down");
                var commands = _humanCommandSequence;
                var hostile = ReviewState().Hostiles!.First(enemy => !enemy.Combat.IsDefeated);
                await InputWorldClick(ToGodot(hostile.Position) + Vector3.Up);
                InputCheck("a downed selection explains rejected world attack orders", _humanCommandSequence == commands
                    && _feedbackLabel.Text == "Select a living crew member first.");
                await InputFrame();
                var pausedTick = _session.Tick;
                var pose = JsonSerializer.SerializeToElement(fallenPresentation.GetDiagnostics());
                _reviewSampleTick = null; _reviewDrivesClock = false;
                for (var frame = 0; frame < 8; frame++) { await InputFrame(); }
                var after = JsonSerializer.SerializeToElement(fallenPresentation.GetDiagnostics());
                static Vector3 BonePosition(JsonElement bone)
                { var p = bone.GetProperty("position"); return new Vector3(p[0].GetSingle(), p[1].GetSingle(), p[2].GetSingle()); }
                var motion = pose.GetProperty("bones").EnumerateArray().Zip(after.GetProperty("bones").EnumerateArray(),
                    (before, next) => BonePosition(before).DistanceTo(BonePosition(next))).Max();
                InputCheck("ordinary pause freezes a fallen crew member while the survivor fights", _session.Tick == pausedTick
                    && pose.GetProperty("clip_seconds").GetDouble() == after.GetProperty("clip_seconds").GetDouble() && motion < .001f);
                _reviewDrivesClock = true; _reviewSampleTick = _session.Tick;
            }
            if (_reviewMode == "record")
            { await ReviewTicks((int)Math.Ceiling(fallenPresentation.DownDurationSeconds * GameSession.TicksPerSecond) + 2); }

            await ReviewUntil(state => state.Encounter!.Phase == EncounterPhase.Defeat, 1200, fast: true);
            await InputFrame();
            await CheckUnavailableStop("party defeat");
            if (_reviewMode == "input") { CheckHudBounds("party defeat"); CheckWorldHealth("party defeat"); }
            await CheckCompletedDeathPresentation();
            if (await ReviewCapture("defeat")) { return; }
            if (_reviewMode == "input") { await InputClick(_retryButton.GetGlobalRect().GetCenter(), MouseButton.Left); }
            else { ReviewOrder(new RestartEncounterCommand(new CommandId("party.retry"), _definition.Combat.PartyEncounter.Id)); }
            InputCheck("retry restores both members and hostiles", ReviewState().Encounter!.Attempt == 2
                && ReviewState().Party.All(actor => actor.Combat!.Health == actor.Combat.MaximumHealth)
                && ReviewState().Hostiles!.All(hostile => hostile.Combat.Health == hostile.Combat.MaximumHealth));
            if (await ReviewCapture("retry")) { return; }
            if (_reviewMode == "input") { CheckWorldHealth("party retry"); }
            FinishSoloReview();
            return;
        }

        if (_reviewMode == "input") { await CheckPartySelectionAndOrders(); }
        else
        {
            ReviewOrder(new AssignBasicAttackTargetCommand(new CommandId("party.attack.vanguard"), protagonist, new EntityId("actor.enemy.gun_sentry.main")));
            ReviewOrder(new AssignBasicAttackTargetCommand(new CommandId("party.attack.protector"), protector, new EntityId("actor.enemy.security_enforcer.main")));
        }
        await ReviewTicks(_definition.Combat.GetAttack(_definition.Companion.Loadout!.BasicAttackId).WindupTicks);
        await InputFrame();
        InputCheck("shotgun emits five visible pellets", _combatPresentationEffects.Select(effect => effect.Node)
            .OfType<CarbineProjectile>().Count(bolt => bolt.LaunchPosition.DistanceTo(_protectorPartyPresentation.MuzzlePosition) < .02f) == 5);
        if (_reviewMode == "input")
        {
            var bolts = _combatPresentationEffects.Select(effect => effect.Node).OfType<CarbineProjectile>().ToArray();
            var positions = bolts.Select(bolt => bolt.Position).ToArray();
            var pausedTick = _session.Tick;
            _reviewDrivesClock = false;
            _reviewSampleTick = null;
            for (var frame = 0; frame < 12; frame++) { await InputFrame(); }
            InputCheck("pause freezes every pellet and combat tick", _session.Tick == pausedTick
                && bolts.Select((bolt, index) => bolt.Position.IsEqualApprox(positions[index])).All(frozen => frozen));
            _reviewDrivesClock = true;
            _reviewSampleTick = _session.Tick;
        }
        if (await ReviewCapture("fire")) { return; }
        await ReviewTicks(3);
        if (await ReviewCapture("recoil")) { return; }

        var barrierPosition = ToGodot(ReviewState().Party[1].Position) + Vector3.Back * .8f;
        if (_reviewMode == "input")
        {
            await InputClick(_partyButtons[protector.Value].GetGlobalRect().GetCenter(), MouseButton.Left);
            await InputKey(Key.Key1);
            InputCheck("Protector enters barrier placement", _abilityTargeting && _abilityOwnerId == protector);
            var previousOrder = ReviewState().Party[1].PendingAction;
            await InputKey(Key.Escape);
            InputCheck("cancelling placement keeps the previous order and cooldown", !_abilityTargeting
                && ReviewState().Party[1].PendingAction == previousOrder
                && ReviewState().Party[1].Combat!.Cooldowns.Single(cd => cd.AbilityId == _definition.Combat.Barrier.Id).RemainingTicks == 0);
            await InputKey(Key.Key1);
            var owner = ReviewState().Party[1];
            var distantFloor = ToGodot(owner.Position) + Vector3.Back * (float)(_definition.Combat.Barrier.RangeMeters + 1);
            await InputWorldClick(distantFloor, MouseButton.Left);
            InputCheck("invalid first click keeps placement active without changing the order or cooldown", _abilityTargeting
                && _feedbackLabel.Text == "Outside deployment range." && ReviewState().Party[1].PendingAction == previousOrder
                && ReviewState().Party[1].Combat!.Cooldowns.Single(cd => cd.AbilityId == _definition.Combat.Barrier.Id).RemainingTicks == 0);
            await InputWorldClick(ToGodot(owner.Position), MouseButton.Left);
            InputCheck("one click at Protector's feet uses his current facing", !_abilityTargeting
                && ReviewState().Party[1].PendingAction?.AbilityFacing is { } atFeetFacing
                && atFeetFacing.DistanceTo(owner.Facing) < .001);
            await InputKey(Key.Key1);
            await InputWorldClick(barrierPosition, MouseButton.Left);
            var placed = ReviewState().Party[1].PendingAction;
            InputCheck("first click queues the fixed barrier facing from Protector toward placement", !_abilityTargeting
                && placed?.AbilityFacing is { Z: > .999 } && placed.Destination.DistanceTo(ToCore(barrierPosition)) < .01
                && _barrierQueued.Visible && !_barrierPreview.Visible);
            await InputPointerMotion(_camera.UnprojectPosition(barrierPosition + Vector3.Right * 2));
            InputCheck("pointer movement after placement cannot move or turn the queued barrier", ReviewState().Party[1].PendingAction == placed
                && _barrierQueued.GlobalPosition.DistanceTo(ToGodot(_definition.Combat.Barrier.CenterAt(placed!.Destination))) < .01
                && -_barrierQueued.GlobalBasis.Z.Normalized().Dot(ToGodot(placed.AbilityFacing!.Value)) > .999);
        }
        else { ReviewOrder(new UseAbilityCommand(new CommandId("party.barrier"), protector, _definition.Combat.Barrier.Id,
            new BarrierAbilityTarget(ToCore(barrierPosition), new WorldPosition(0, 0, 1)))); }
        if (await ReviewCapture("barrier-queued")) { return; }
        await ReviewTicks(_definition.Combat.Barrier.WindupTicks);
        InputCheck("barrier deploys without erasing attack intent", ReviewState().Encounter!.Barrier is not null
            && ReviewState().Party[1].Combat!.RememberedAttackTargetId is not null);
        await ReviewTicks(5);
        if (await ReviewCapture("barrier")) { return; }
        if (await CheckPartySkills()) { return; }
        await ReviewUntil(_ => _session!.EventsSince(0).Any(item => item.Type == GameplayEventType.ProjectileBlocked), 120);
        InputCheck("barrier produces a visible block and removes its flying bolt", _incomingBolts.Count == 0
            && _combatPresentationEffects.Any(effect => effect.Node is Label3D { Text: "BLOCKED" }));
        if (await ReviewCapture("barrier-block")) { return; }
        await CheckBarrierAfterMovement();
        for (var tick = 0; tick < 1200 && ReviewState().Encounter!.Phase == EncounterPhase.Active; tick++)
        {
            var route = ReviewState();
            foreach (var actor in route.Party.Where(actor => actor.Combat?.IsDefeated == false))
            {
                if (actor.Combat!.RememberedAttackTargetId is null && actor.PendingAction is null)
                {
                    var target = route.Hostiles!.Where(hostile => !hostile.Combat.IsDefeated).OrderBy(hostile => hostile.Position.DistanceTo(actor.Position)).FirstOrDefault();
                    if (target is not null) { ReviewOrder(new AssignBasicAttackTargetCommand(new CommandId($"party.retarget.{actor.Id}.{_session.Tick}"), actor.Id, target.Id)); }
                }
            }
            await ReviewTicks(1, fast: _reviewMode != "record");
        }
        InputCheck("party fight reaches securing", ReviewState().Encounter!.Phase == EncounterPhase.Securing);
        await ReviewTicks(_definition.Combat.PartyEncounter.SecuringTicks / 2);
        if (await ReviewCapture("holster")) { return; }
        await ReviewTicks(_definition.Combat.PartyEncounter.SecuringTicks - _definition.Combat.PartyEncounter.SecuringTicks / 2);
        InputCheck("main victory ends this slice while airlock remains deferred", ReviewState().Encounter!.Phase == EncounterPhase.Victory
            && ReviewState().Objective.Status == ObjectiveStatus.Completed && ReviewState().Phase == ScenarioPhase.InProgress
            && ReviewState().Interactions.Single(item => item.Id.Value == "interaction.evacuation_airlock").State == InteractionState.Unavailable);
        if (await ReviewCapture("victory")) { return; }
        if (await ReviewCapture("slice-complete")) { return; }
        FinishSoloReview();
    }

    private async Task CheckPartySelectionAndOrders()
    {
        _camera.InputEnabled = true;
        var protagonist = _definition!.Protagonist.Id;
        var protector = _definition.Companion.Id;
        await InputClick(_partyButtons[protagonist.Value].GetGlobalRect().GetCenter(), MouseButton.Left);
        InputCheck("portrait selects one crew member", _selectedActorIds.SetEquals([protagonist]));
        await InputKey(Key.Tab);
        InputCheck("Tab cycles focused crew", _focusedActorId == protector && _selectedActorIds.SetEquals([protector]));
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, PhysicalKeycode = Key.Shift, Pressed = true, ShiftPressed = true });
        await InputFrame();
        await InputWorldClick(_protagonistView.GlobalPosition + Vector3.Up, MouseButton.Left);
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.Shift, PhysicalKeycode = Key.Shift, Pressed = false });
        InputCheck("Shift-click adds a crew member", _selectedActorIds.SetEquals([protagonist, protector]));
        await CheckEdgeCameraInput();
        await CheckDragSelectionAndAbilityFocus();
        var commandsBefore = _humanCommandSequence;
        var tickBefore = _session!.Tick;
        var pendingBefore = ReviewState().Party.Select(actor => actor.PendingAction).ToArray();
        await InputKey(Key.T);
        await InputKey(Key.H);
        await InputKey(Key.Key3);
        InputCheck("removed facing, auto-fire and aid shortcuts do not change paused orders", _humanCommandSequence == commandsBefore
            && _session.Tick == tickBefore && pendingBefore.SequenceEqual(ReviewState().Party.Select(actor => actor.PendingAction)));
        await InputWorldClick(new Vector3(0, 0, 6));
        InputCheck("group floor click queues both moves", ReviewState().Party.All(actor => actor.PendingAction?.Kind == PrimaryActionKind.Move));
        CheckFieldOrderLabels("group move");
        await InputWorldClick(_enemyViews[new EntityId("actor.enemy.security_enforcer.main")].Root.GlobalPosition + Vector3.Up);
        InputCheck("group target click queues both attacks", ReviewState().Party.All(actor => actor.PendingAction?.Kind == PrimaryActionKind.Attack));
        CheckFieldOrderLabels("attack replaces move");
        InputCheck("a shared attack destination uses one crew badge", _fieldOrders.Values.Count(view => view.Destination.Visible) == 1
            && _fieldOrders.Values.Single(view => view.Destination.Visible).Destination.Text == "01 / 02");
        await ReviewCapture("field-orders");
        await InputKey(Key.X);
        InputCheck("group Stop replaces both pending orders", ReviewState().Party.All(actor => actor.PendingAction?.Kind == PrimaryActionKind.Stop));
        CheckFieldOrderLabels("Stop replaces attack");
        await InputWorldClick(_protagonistView.GlobalPosition + Vector3.Up, MouseButton.Left);
        InputCheck("world click selects the actor", _focusedActorId == protagonist);
        await InputWorldClick(_enemyViews[new EntityId("actor.enemy.gun_sentry.main")].Root.GlobalPosition + Vector3.Up);
        await InputKey(Key.Tab);
        await InputWorldClick(_enemyViews[new EntityId("actor.enemy.security_enforcer.main")].Root.GlobalPosition + Vector3.Up);
        InputCheck("each selected member receives its own target", ReviewState().Protagonist.PendingAction?.CombatTargetId?.Value == "actor.enemy.gun_sentry.main"
            && ReviewState().Party[1].PendingAction?.CombatTargetId?.Value == "actor.enemy.security_enforcer.main");
        foreach (var control in _partyButtons.Values.Cast<Control>().Concat([_abilityButton, _secondaryAbilityButton, _stopButton, _pauseButton]))
        { InputCheck("party HUD control fits viewport", GetViewport().GetVisibleRect().Encloses(control.GetGlobalRect())); }
        CheckFieldOrderLabels("independent targets");
        await CheckFieldHudWorldInput();
        await CheckControlsOverlayInput();
    }

    private void CheckPartyHostilePlacementOrder()
    {
        var content = System.Text.Json.Nodes.JsonNode.Parse(Godot.FileAccess.GetFileAsString("res://content/station-route.json"))!;
        var encounter = content["combat"]!["encounters"]!.AsArray().Single(item => item!["requires_companion"]!.GetValue<bool>())!;
        var ids = encounter["hostile_ids"]!.AsArray().Select(item => item!.GetValue<string>()).Reverse().ToArray();
        encounter["hostile_ids"] = JsonSerializer.SerializeToNode(ids);
        var definition = StationRouteContent.ParseJson(content.ToJsonString());
        var layout = CreateLayout(definition);
        _ = GameSession.CreateStationRoute(definition, layout, new GodotSpatialPathfinder(GetWorld3D().NavigationMap));
        foreach (var id in definition.Combat.PartyEncounter.HostileIds)
        {
            var marker = GetNode<Node3D>("Markers").GetChildren().OfType<Marker3D>().Single(node => GetStableId(node) == id.Value);
            var position = id == definition.Combat.PartyEncounter.HostileIds[0] ? layout.PartyEncounter!.HostileSpawnPosition
                : layout.PartyEncounter!.AdditionalHostiles!.Single(actor => actor.ActorId == id).Position;
            InputCheck($"reordered party content starts and keeps {id} at its authored marker", position == ToCore(marker.GlobalPosition));
        }
    }

    private void CheckPartyAbilityEnvelope()
    {
        string Command(object payload) => JsonSerializer.Serialize(new { schema_version = 9,
            command_id = "party.adapter.barrier", type = "use_ability", payload });
        var actor = _definition!.Companion.Id.Value;
        var ability = _definition.Combat.Barrier.Id.Value;
        InputCheck("adapter accepts ground position and shield facing", IsAccepted(_automationBridge!.SubmitCommandJson(Command(new
        { actor_id = actor, ability_id = ability, target_position = new { x = .55, y = 0, z = 5.3 }, target_facing = new { x = 0, y = 0, z = 1 } })))
            && ReviewState().Party[1].PendingAction?.AbilityFacing is { Z: 1 });
        var pending = ReviewState().Party[1].PendingAction;
        foreach (var payload in new object[]
        {
            new { actor_id = actor, ability_id = ability },
            new { actor_id = actor, ability_id = ability, target_position = new { x = 0, y = 0, z = 6.4 } },
            new { actor_id = actor, ability_id = ability, target_actor_id = actor, target_position = new { x = 0, y = 0, z = 6.4 }, target_facing = new { x = 0, y = 0, z = 1 } },
            new { actor_id = actor, ability_id = ability, target_facing = new { x = 0, y = 0, z = 0 } },
        })
        {
            InputCheck("incomplete or ambiguous barrier targets preserve pending order",
                !IsAccepted(_automationBridge.SubmitCommandJson(Command(payload))) && ReviewState().Party[1].PendingAction == pending);
        }
        foreach (var removed in new[] { "set_auto_attack", "use_item", "face_actors" })
        {
            var json = JsonSerializer.Serialize(new { schema_version = 9, command_id = $"party.adapter.removed.{removed}",
                type = removed, payload = new { actor_id = actor } });
            using var result = JsonDocument.Parse(_automationBridge.SubmitCommandJson(json));
            InputCheck("removed commands reject without replacing orders", !result.RootElement.GetProperty("accepted").GetBoolean()
                && result.RootElement.GetProperty("error").GetString() == "unknown_command"
                && ReviewState().Party[1].PendingAction == pending);
        }
        ReviewOrder(new StopActorsCommand(new CommandId("party.adapter.clear"), [_definition.Companion.Id]));
    }

    private void CheckSentryMountAndCloseTargets()
    {
        var hostile = ReviewState().Hostiles!.Single(candidate => _enemyViews[candidate.Id].Sentry is not null);
        var sentry = _enemyViews[hostile.Id].Sentry!;
        var root = _enemyViews[hostile.Id].Root;
        var diagnostics = JsonSerializer.SerializeToElement(sentry.GetDiagnostics());
        var mount = diagnostics.GetProperty("mount_world");
        InputCheck("sentry mount remains at gun height and muzzle clears it", Math.Abs(mount[1].GetSingle() - root.GlobalPosition.Y - 1.62f) < .02f
            && sentry.MuzzlePosition.DistanceTo(new Vector3(mount[0].GetSingle(), mount[1].GetSingle(), mount[2].GetSingle())) is > .5f and < .8f);
        var tick = _session!.Tick;
        foreach (var target in new[] { new Vector3(-4, .3f, -1), new Vector3(4, .3f, -1), new Vector3(0, .1f, -.05f), new Vector3(0, .1f, 1) })
        {
            var previous = JsonSerializer.SerializeToElement(sentry.GetDiagnostics()).GetProperty("aim_degrees")[1].GetSingle();
            sentry.Synchronize(hostile, root.GlobalPosition + target, tick, 1f / 60);
            var aim = JsonSerializer.SerializeToElement(sentry.GetDiagnostics()).GetProperty("aim_degrees");
            InputCheck("close or crossing targets keep sentry aim finite and rate limited", float.IsFinite(aim[0].GetSingle())
                && Math.Abs(aim[0].GetSingle()) <= 25.01f && Math.Abs(aim[1].GetSingle()) <= 60.01f
                && Math.Abs(aim[1].GetSingle() - previous) <= SentryPresentation.TurnDegreesPerSecond / 60 + .01f);
        }
        sentry.Synchronize(hostile, ToGodot(ReviewState().Protagonist.Position) + Vector3.Up * 1.1f, tick, .25f);
    }

    private async Task CheckCompletedDeathPresentation()
    {
        var tick = _session!.Tick;
        var actor = ReviewState().Party.MaxBy(member => member.Combat!.DefeatedAtTick)!;
        var presentation = ArmedPresentation(actor.Id)!;
        double ClipSeconds() => JsonSerializer.SerializeToElement(presentation.GetDiagnostics()).GetProperty("clip_seconds").GetDouble();
        InputCheck("last crew death starts before the final pose", ClipSeconds() < presentation.DownClipLengthSeconds - .1);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (_defeatPresentationSeconds < presentation.DownDurationSeconds && timer.Elapsed.TotalSeconds < 10)
        { await InputFrame(); }
        InputCheck("death animation finishes while combat remains paused", _session.IsPaused && _session.Tick == tick
            && Math.Abs(ClipSeconds() - presentation.DownClipLengthSeconds) < .04);
        foreach (var member in ReviewState().Party.Where(member => member.Combat!.IsDefeated))
        {
            var pose = JsonSerializer.SerializeToElement(ArmedPresentation(member.Id)!.GetDiagnostics()).GetProperty("death_pose");
            InputCheck("fallen crew lie on the floor with settled boots", pose.GetProperty("head_height_m").GetDouble() < .55
                && pose.GetProperty("hips_height_m").GetDouble() < .5 && pose.GetProperty("lowest_foot_height_m").GetDouble() >= 0
                && pose.GetProperty("highest_foot_height_m").GetDouble() < .3);
        }
    }

    private void ValidatePartyPresentation(string checkpoint, JsonElement diagnostics)
    {
        var protector = diagnostics.GetProperty("protector");
        if (Math.Abs(protector.GetProperty("weapon_length_m").GetDouble() - .84) > .017)
        { throw new InvalidOperationException("Shotgun lost metric scale."); }
        if (ReviewState().Encounter!.Phase == EncounterPhase.Active && !ReviewState().Party[1].Combat!.IsDefeated
            && (protector.GetProperty("primary_grip_error_m").GetDouble() > .03 || protector.GetProperty("support_grip_error_m").GetDouble() > .03))
        {
            File.WriteAllText(Path.Combine(_reviewOutput, checkpoint + "-failure.json"), diagnostics.GetRawText());
            throw new InvalidOperationException($"Protector grip fit failed at {checkpoint}.");
        }
        if (checkpoint is "fire" or "recoil")
        {
            foreach (var actor in ReviewState().Party)
            {
                var presentation = ArmedPresentation(actor.Id)!;
                var target = ReviewState().Hostiles!.Single(hostile => hostile.Id == actor.Combat!.RememberedAttackTargetId);
                var alignment = presentation.MuzzleDirection.Dot((ToGodot(target.Position) + Vector3.Up * 1.15f - presentation.MuzzlePosition).Normalized());
                if (alignment < .95f) { throw new InvalidOperationException($"{actor.DisplayName} muzzle alignment failed at {checkpoint}: {alignment}."); }
                var line = _attackLinks[actor.Id].Line;
                var start = line.GlobalTransform * new Vector3(0, -.5f, 0);
                var end = line.GlobalTransform * new Vector3(0, .5f, 0);
                if (start.DistanceTo(_actorViews[actor.Id.Value].GlobalPosition + Vector3.Up * .12f) > .01f
                    || end.DistanceTo(_enemyViews[target.Id].Root.GlobalPosition + Vector3.Up * .12f) > .01f)
                { throw new InvalidOperationException("Attack indicator does not connect its crew member to the current target."); }
            }
        }
    }
}
