using System.Globalization;
using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

/// <summary>
/// Party input review with injected controller events (<c>-Encounter party -Mode input -Sequence gamepad</c>):
/// device switching, stick walking with a following squad, paused move planning, highlight targeting, abilities,
/// camera, manual, dialogue and disconnect handling, all through the ordinary input path and typed commands.
/// Injected events are separate evidence from a physical controller.
/// </summary>
public partial class GameHost
{
    private const int ReviewPad = 0;

    private async Task PadPress(JoyButton button, int heldFrames = 1)
    {
        Input.ParseInputEvent(new InputEventJoypadButton { Device = ReviewPad, ButtonIndex = button, Pressed = true });
        for (var frame = 0; frame < heldFrames; frame++) { await InputFrame(); }
        Input.ParseInputEvent(new InputEventJoypadButton { Device = ReviewPad, ButtonIndex = button, Pressed = false });
        await InputFrame();
    }

    private static void PadAxis(JoyAxis axis, float value) =>
        Input.ParseInputEvent(new InputEventJoypadMotion { Device = ReviewPad, Axis = axis, AxisValue = value });

    private async Task PadTrigger(JoyAxis trigger)
    {
        PadAxis(trigger, 1);
        await InputFrame();
        PadAxis(trigger, 0);
        await InputFrame();
    }

    private async Task PadHoldStick(JoyAxis x, JoyAxis y, Vector2 value, int frames)
    {
        PadAxis(x, value.X);
        PadAxis(y, value.Y);
        for (var frame = 0; frame < frames; frame++) { await InputFrame(); }
        PadAxis(x, 0);
        PadAxis(y, 0);
        await InputFrame();
    }

    /// <summary>Left-stick deflection that walks toward a floor point from the camera's current view.</summary>
    private Vector2 StickToward(Vector3 from, Vector3 to)
    {
        var (forward, right) = _camera.GetPanBasis();
        var direction = WithGroundHeight(to) - WithGroundHeight(from);
        return new Vector2(direction.Dot(right), -direction.Dot(forward)).Normalized();
    }

    private async Task PadMouseMotion(Vector2 relative)
    {
        var point = GetViewport().GetFinalTransform() * (GetViewport().GetVisibleRect().GetCenter() + relative);
        Input.ParseInputEvent(new InputEventMouseMotion { Position = point, GlobalPosition = point, Relative = relative });
        await InputFrame();
    }

    private async Task RunGamepadPartyReviewAsync()
    {
        _reviewDrivesClock = true;
        _camera.InputEnabled = true;
        var protagonist = _definition!.Protagonist.Id;
        await InputFrame();
        InputCheck("keyboard and mouse is the starting device", !InputDevice.UsingGamepad && !_abilityButton.PadGlyphShown);
        await PadPress(JoyButton.DpadRight);
        InputCheck("any controller button switches to controller mode", InputDevice.UsingGamepad);
        InputCheck("controller mode hides the mouse cursor", DisplayServer.GetName() == "headless" || Input.MouseMode == Input.MouseModeEnum.Hidden);
        InputCheck("controller prompts replace the keyboard hint strip and tile keys", _worldControlsHint.Text.Contains("res://ui/icons/pad/", StringComparison.Ordinal)
            && _abilityButton.PadGlyphShown && _secondaryAbilityButton.PadGlyphShown && _stopButton.PadGlyphShown);
        var commandsBeforeDrift = _humanCommandSequence;
        PadAxis(JoyAxis.LeftX, .15f);
        await PadMouseMotion(new Vector2(2, 1));
        InputCheck("stick drift and pointer jitter below the thresholds keep controller mode and order nothing", InputDevice.UsingGamepad
            && _humanCommandSequence == commandsBeforeDrift);
        PadAxis(JoyAxis.LeftX, 0);
        await PadMouseMotion(new Vector2(30, 0));
        InputCheck("moving the mouse switches back to keyboard and mouse", !InputDevice.UsingGamepad
            && (DisplayServer.GetName() == "headless" || Input.MouseMode == Input.MouseModeEnum.Visible));
        await InputFrame();
        InputCheck("keyboard prompts return unchanged", _worldControlsHint.Text == "RMB  Order    DRAG  Select    TAB  Ability focus    SPACE  Pause"
            && !_abilityButton.PadGlyphShown && _controlsButton.Text == "F1   Manual");
        await PadPress(JoyButton.DpadRight);
        InputCheck("a controller button switches to controller mode again", InputDevice.UsingGamepad);

        // Real time: steer the Vanguard toward the entry door and back to the survivor, as a player would.
        _reviewDrivesClock = false;
        _reviewSampleTick = null;
        if (_session!.IsPaused) { await PadTrigger(JoyAxis.TriggerRight); }
        InputCheck("play runs in real time", !_session.IsPaused);
        var survivor = _interactionViews["interaction.survivor"];
        var door = _interactionViews["interaction.service_door.entry"];
        var start = ToGodot(ReviewState().Protagonist.Position);
        var markerShownWhileWalking = false;
        Vector3 Walker() => ToGodot(ReviewState().Protagonist.Position);
        async Task SteerAsync(Node3D goal, double seconds, float within)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            while (timer.Elapsed.TotalSeconds < seconds && FlatDistance(Walker(), goal.GlobalPosition) > within)
            {
                var stick = StickToward(Walker(), goal.GlobalPosition);
                PadAxis(JoyAxis.LeftX, stick.X);
                PadAxis(JoyAxis.LeftY, stick.Y);
                await InputFrame();
                markerShownWhileWalking |= _destinationMarker.Visible;
            }
        }
        async Task ReleaseStickAsync()
        {
            PadAxis(JoyAxis.LeftX, 0);
            PadAxis(JoyAxis.LeftY, 0);
            for (var frame = 0; frame < 8; frame++) { await InputFrame(); }
        }
        await SteerAsync(door, 1.2, 1.5f);
        var walked = FlatDistance(Walker(), start);
        InputCheck($"the left stick walks the Vanguard where it points ({walked:0.0} m)", walked > 1
            && FlatDistance(Walker(), door.GlobalPosition) < FlatDistance(start, door.GlobalPosition) - .8f);
        InputCheck("stick walking issues ordinary move commands",
            ReviewState().Protagonist.CurrentAction?.CommandId.Value.StartsWith("input.pad-walk.", StringComparison.Ordinal) == true);
        await ReleaseStickAsync();
        InputCheck("releasing the stick stops the walker", ReviewState().Protagonist.CurrentAction?.HasRemainingMovement != true);
        await SteerAsync(survivor, 5, 2.6f);
        InputCheck("the left stick steers the Vanguard back to the survivor", FlatDistance(Walker(), survivor.GlobalPosition) <= 2.6f);
        InputCheck("stick steps are not marked as click destinations", !markerShownWhileWalking);
        await ReleaseStickAsync();
        await PadTrigger(JoyAxis.TriggerRight);
        InputCheck("RT pauses play", _session.IsPaused);
        _reviewDrivesClock = true;
        _reviewSampleTick = _session.Tick;
        await InputFrame();
        InputCheck("walking up to the survivor highlights them with a controller prompt", _padHighlight?.Value == "interaction.survivor"
            && _padPrompt.Visible && _padPromptText.Text.Contains("pad/a", StringComparison.Ordinal) && _padHighlightRing.Visible);
        await PadPress(JoyButton.A);
        InputCheck("A orders the protagonist to talk to the highlighted survivor",
            ReviewState().Protagonist.PendingAction is { Kind: PrimaryActionKind.Interact } interact && interact.InteractionTargetId?.Value == "interaction.survivor");
        await ReviewUntil(state => state.ActiveDialogue is not null, 300, fast: true);
        await InputFrame();
        InputCheck("dialogue shows controller prompts and focuses the first response", _dialogueHint.Text.Contains("pad/a", StringComparison.Ordinal)
            && _dialogueResponses.GetChild<Button>(0).HasFocus());
        await PadPress(JoyButton.DpadDown);
        InputCheck("D-pad down focuses the second response", _dialogueResponses.GetChild<Button>(1).HasFocus());
        PadAxis(JoyAxis.LeftY, -1);
        await InputFrame();
        PadAxis(JoyAxis.LeftY, 0);
        await InputFrame();
        InputCheck("the left stick returns to the first response", _dialogueResponses.GetChild<Button>(0).HasFocus());
        var commands = _humanCommandSequence;
        var paused = _session.IsPaused;
        await PadTrigger(JoyAxis.TriggerRight);
        await PadPress(JoyButton.RightShoulder);
        await PadPress(JoyButton.X);
        InputCheck("dialogue isolates pause, crew switching and abilities from the controller", _session.IsPaused == paused
            && _humanCommandSequence == commands && !_abilityTargeting && ReviewState().ActiveDialogue is not null);
        await PadPress(JoyButton.A);
        InputCheck("A chooses the focused response", ReviewState().ActiveDialogue is null
            && ReviewState().Interactions.Single(item => item.Id.Value == "interaction.survivor").State == InteractionState.Completed);

        await ReviewWaitForPath(new WorldPosition(-10, 0, 2.75));
        ReviewOrder(new MoveActorCommand(new CommandId("pad.setup.enter.solo"), protagonist, new WorldPosition(-10, 0, 2.75)));
        await ReviewUntil(state => state.Encounter!.Phase == EncounterPhase.Readying, 300, fast: true);
        await InputFrame();
        InputCheck("the first fight highlights its Enforcer and explains the controller buttons", _padHighlight == _definition.Combat.SoloHostile.Id
            && _tipCard.Visible && _tipBody.Text.Contains("pad/a", StringComparison.Ordinal) && _pauseLabel.Text.Contains("pad/rt", StringComparison.Ordinal));
        await PadPress(JoyButton.A);
        InputCheck("A assigns fire on the highlighted Enforcer", ReviewState().Protagonist.PendingAction is { Kind: PrimaryActionKind.Attack } attack
            && attack.CombatTargetId == _definition.Combat.SoloHostile.Id);
        await ReviewUntil(state => state.Encounter!.Phase == EncounterPhase.Victory, 1500, fast: true);
        ReviewOrder(new InteractCommand(new CommandId("pad.setup.exit"), protagonist, new EntityId("interaction.service_door.solo_exit")));
        await ReviewUntil(state => state.Objective.Id.Value == "objective.recruit_protector", 300, fast: true);
        await ReviewWaitForPath(ReviewState().Interactions.Single(item => item.Id.Value == "interaction.protector").ApproachPosition);
        ReviewOrder(new InteractCommand(new CommandId("pad.setup.protector"), protagonist, new EntityId("interaction.protector")));
        await ReviewUntil(state => state.ActiveDialogue is not null, 300, fast: true);
        ReviewOrder(new ChooseDialogueResponseCommand(new CommandId("pad.setup.recruit"), protagonist,
            new EntityId("interaction.protector"), new DialogueResponseId("response.recruit_protector")));
        await ReviewWaitForPath(new WorldPosition(0, 0, 5));
        ReviewOrder(new MovePartyCommand(new CommandId("pad.setup.enter.main"), [protagonist, _definition.Companion.Id], new WorldPosition(0, 0, 5)));
        await ReviewUntil(state => state.Encounter!.Id == _definition.Combat.PartyEncounter.Id, 600, fast: true);
        await ReviewTicks(_definition.Combat.PartyEncounter.ReadyingTicks);
        await AdvancePartyIntoArena();
        _camera.FocusOn(ToGodot(ReviewState().Party[0].Position));
        await InputFrame();
        await CheckGamepadCombatInput();
        await FinishSoloReview();
    }

    private async Task CheckGamepadCombatInput()
    {
        var protagonist = _definition!.Protagonist.Id;
        var protector = _definition.Companion.Id;
        var route = ReviewState();
        InputCheck("both recruited crew start selected as a squad led by the Vanguard", _focusedActorId == protagonist
            && SelectedLivingActors(route).Count() == 2);
        await PadPress(JoyButton.RightShoulder);
        InputCheck("RB passes the lead to the next crew member and keeps the squad", _focusedActorId == protector && _selectedActorIds.Count == 2);
        await PadPress(JoyButton.LeftShoulder);
        InputCheck("LB passes the lead back", _focusedActorId == protagonist && _selectedActorIds.Count == 2);
        await PadPress(JoyButton.DpadUp);
        InputCheck("D-pad up leaves the others holding position (solo)", _selectedActorIds.SetEquals([protagonist]));
        await PadPress(JoyButton.DpadUp);
        InputCheck("D-pad up brings the squad back", _selectedActorIds.Count == 2 && _focusedActorId == protagonist);

        var enemies = PadTargetableEnemies(ReviewState());
        InputCheck("a fight highlights a targetable enemy", _padHighlight is { } first && enemies.Contains(first) && _padPrompt.Visible);
        var highlighted = _padHighlight!.Value;
        await PadPress(JoyButton.DpadRight);
        InputCheck("D-pad right moves the highlight to another enemy", _padHighlight is { } next && next != highlighted && enemies.Contains(next));
        await PadPress(JoyButton.DpadLeft);
        InputCheck("D-pad left moves it back", _padHighlight == highlighted);
        await PadPress(JoyButton.A);
        InputCheck("A sends the whole squad at the highlighted enemy", ReviewState().Party.All(actor =>
            actor.PendingAction is { Kind: PrimaryActionKind.Attack } attack && attack.CombatTargetId == highlighted));
        await ReviewCapture("gamepad-target");

        var leader = ReviewState().Protagonist;
        // Push toward open deck so walls and the arena trench cannot deflect the planned point.
        var push = new[] { new Vector2(1, 0), new Vector2(0, 1), new Vector2(-1, 0), new Vector2(0, -1) }
            .First(stick => PadGroundTarget(ToGodot(leader.Position), PadWorldDirection(stick).Normalized(), 2f) is not null);
        var pushed = PadWorldDirection(push).Normalized();
        PadAxis(JoyAxis.LeftX, push.X);
        PadAxis(JoyAxis.LeftY, push.Y);
        for (var frame = 0; frame < 12; frame++) { await InputFrame(); }
        InputCheck("while paused the left stick drags a planned destination", _padPlan is not null && _padPlanMarker.Visible);
        PadAxis(JoyAxis.LeftX, 0);
        PadAxis(JoyAxis.LeftY, 0);
        await InputFrame();
        var planned = ReviewState();
        var offset = ToGodot(planned.Protagonist.PendingAction?.Destination ?? leader.Position) - ToGodot(leader.Position);
        InputCheck("releasing the stick queues the walker's move toward the push", planned.Protagonist.PendingAction is { Kind: PrimaryActionKind.Move } move
            && move.CommandId.Value.StartsWith("input.pad-plan.", StringComparison.Ordinal) && offset.Length() > .5f && offset.Normalized().Dot(pushed) > .6f);
        InputCheck("the squad queues a move to follow", planned.Party[1].PendingAction is { Kind: PrimaryActionKind.Move } follow
            && follow.CommandId.Value.StartsWith("input.pad-plan-follow.", StringComparison.Ordinal));
        CheckFieldOrderLabels("controller planned move");
        var pending = ReviewState().Party.Select(actor => actor.PendingAction).ToArray();
        PadAxis(JoyAxis.LeftX, -1);
        for (var frame = 0; frame < 8; frame++) { await InputFrame(); }
        await PadPress(JoyButton.B);
        PadAxis(JoyAxis.LeftX, 0);
        await InputFrame();
        InputCheck("B drops a planned move before it is queued", ReviewState().Party.Select(actor => actor.PendingAction).SequenceEqual(pending)
            && !_padPlanMarker.Visible);

        await PadPress(JoyButton.X);
        InputCheck("X aims the Vanguard's Interrupt with a placement circle", _abilityTargeting && _abilityOwnerId == protagonist && _padAim is not null);
        var aim = _padAim!.Value;
        // Pull the circle back toward the Vanguard: a push outward could already sit on the range limit.
        await PadHoldStick(JoyAxis.LeftX, JoyAxis.LeftY, StickToward(aim, ToGodot(ReviewState().Protagonist.Position)), 6);
        InputCheck("the left stick moves the circle instead of walking", _padAim is { } moved && moved.DistanceTo(aim) > .2f
            && ReviewState().Protagonist.PendingAction == pending[0]);
        InputCheck("the circle keeps within the ability range", _padAim is { } inRange
            && inRange.DistanceTo(WithGroundHeight(ToGodot(ReviewState().Protagonist.Position))) <= _definition.Combat.ProtagonistAbility.RangeMeters);
        await ReviewCapture("gamepad-aim");
        var placed = _padAim!.Value;
        await PadPress(JoyButton.A);
        var interrupt = ReviewState().Protagonist.PendingAction;
        InputCheck("A queues Interrupt on the circle", interrupt is { Kind: PrimaryActionKind.Ability } queued
            && queued.AbilityId == _definition.Combat.ProtagonistAbility.Id && ToGodot(queued.Destination).DistanceTo(placed) < .05f && !_abilityTargeting);
        await PadPress(JoyButton.X);
        await PadPress(JoyButton.B);
        InputCheck("B cancels aiming and keeps the queued order", !_abilityTargeting && ReviewState().Protagonist.PendingAction == interrupt);

        await PadPress(JoyButton.Y);
        InputCheck("Y aims Burst at the highlighted enemy", _abilityTargeting && _targetAbilityKind == AbilityTargetKind.Entity);
        for (var attempt = 0; attempt < enemies.Count && _session!.CheckBurstTarget(protagonist, _padHighlight!.Value) is not null; attempt++)
        { await PadPress(JoyButton.DpadRight); }
        var burstTarget = _padHighlight!.Value;
        await PadPress(JoyButton.A);
        InputCheck("A fires Burst at the highlighted enemy", ReviewState().Protagonist.PendingAction is { Kind: PrimaryActionKind.Ability } burst
            && burst.AbilityId == _definition.Combat.Burst.Id && burst.CombatTargetId == burstTarget);

        await PadPress(JoyButton.RightShoulder);
        await PadPress(JoyButton.X);
        InputCheck("the Protector's X aims Barrier", _abilityTargeting && _abilityOwnerId == protector && _targetAbilityKind == AbilityTargetKind.Barrier && _padAim is not null);
        var barrierPoint = _padAim!.Value;
        await PadPress(JoyButton.A);
        var guard = ReviewState().Party[1];
        var facing = (barrierPoint - WithGroundHeight(ToGodot(guard.Position))).Normalized();
        InputCheck("A places Barrier facing from the Protector toward the circle", guard.PendingAction?.AbilityFacing is { } shield
            && ToGodot(shield).Dot(facing) > .99f && !_abilityTargeting);
        await PadPress(JoyButton.Y);
        InputCheck("the Protector's Y taunts at once", ReviewState().Party[1].PendingAction?.AbilityId == _definition.Combat.Taunt.Id && !_abilityTargeting);
        await PadPress(JoyButton.DpadDown);
        InputCheck("D-pad down stops the whole squad", ReviewState().Party.All(actor => actor.PendingAction?.Kind == PrimaryActionKind.Stop));
        PadAxis(JoyAxis.LeftX, push.X);
        PadAxis(JoyAxis.LeftY, push.Y);
        for (var frame = 0; frame < 6; frame++) { await InputFrame(); }
        await PadPress(JoyButton.DpadDown);
        PadAxis(JoyAxis.LeftX, 0);
        PadAxis(JoyAxis.LeftY, 0);
        await InputFrame();
        InputCheck("Stop during a planned drag drops the plan instead of queuing it", _padPlan is null
            && ReviewState().Party.All(actor => actor.PendingAction?.Kind == PrimaryActionKind.Stop));
        await PadPress(JoyButton.LeftShoulder);

        var yaw = _camera.YawRadians;
        var distance = _camera.DistanceMeters;
        await PadHoldStick(JoyAxis.RightX, JoyAxis.RightY, new Vector2(1, 0), 8);
        await PadHoldStick(JoyAxis.RightX, JoyAxis.RightY, new Vector2(0, 1), 8);
        InputCheck("the right stick rotates and zooms the camera", !Mathf.IsEqualApprox(_camera.YawRadians, yaw)
            && _camera.DistanceMeters > distance + .1f);
        await PadPress(JoyButton.RightStick);
        InputCheck("pressing the right stick resets the view", Mathf.IsEqualApprox(_camera.DistanceMeters, 14.5f) && Mathf.IsEqualApprox(_camera.PitchRadians, .9f));
        var focus = _camera.FocusPoint;
        var stopOrders = ReviewState().Party.Select(actor => actor.PendingAction).ToArray();
        PadAxis(JoyAxis.TriggerLeft, 1);
        await PadHoldStick(JoyAxis.LeftX, JoyAxis.LeftY, new Vector2(1, 0), 12);
        PadAxis(JoyAxis.TriggerLeft, 0);
        await InputFrame();
        InputCheck("holding LT pans the camera with the left stick without planning a move", _camera.FocusPoint.DistanceTo(focus) > .5f
            && ReviewState().Party.Select(actor => actor.PendingAction).SequenceEqual(stopOrders) && _padPlan is null);

        await PadPress(JoyButton.Start);
        InputCheck("Menu opens the field manual on its controller page", _controlsOverlay.Visible && _manualPad.Visible && !_manualKeys.Visible
            && _session!.IsPaused && _manualCloseButton.HasFocus());
        await PadPress(JoyButton.DpadDown);
        InputCheck("the D-pad walks the manual's controls", _masterVolume.HasFocus());
        var volume = _masterVolume.Value;
        await PadPress(JoyButton.DpadLeft);
        InputCheck("D-pad left lowers the focused volume", _masterVolume.Value == Math.Max(0, volume - 5));
        await PadPress(JoyButton.DpadRight);
        var manualCommands = _humanCommandSequence;
        await PadTrigger(JoyAxis.TriggerRight);
        await PadPress(JoyButton.A);
        InputCheck("the manual blocks controller gameplay input", _controlsOverlay.Visible && _session!.IsPaused && _humanCommandSequence == manualCommands);
        await PadPress(JoyButton.B);
        InputCheck("B closes the manual and restores camera input", !_controlsOverlay.Visible && _camera.InputEnabled);

        // Real time: the walker leads and the rest of the squad follows.
        var before = ReviewState().Party.Select(actor => ToGodot(actor.Position)).ToArray();
        _reviewDrivesClock = false;
        _reviewSampleTick = null;
        await PadTrigger(JoyAxis.TriggerRight);
        InputCheck("RT resumes the fight", !_session!.IsPaused);
        var open = new[] { new Vector2(0, 1), new Vector2(-1, 0), new Vector2(1, 0), new Vector2(0, -1) }
            .First(stick => PadGroundTarget(before[0], PadWorldDirection(stick).Normalized(), 2.5f) is not null);
        PadAxis(JoyAxis.LeftX, open.X);
        PadAxis(JoyAxis.LeftY, open.Y);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < .9) { await InputFrame(); }
        // Pause mid-walk with the stick still held: it must not become a planned move until pushed again.
        await PadTrigger(JoyAxis.TriggerRight);
        _reviewDrivesClock = true;
        _reviewSampleTick = _session.Tick;
        for (var frame = 0; frame < 6; frame++) { await InputFrame(); }
        InputCheck("a pause mid-walk does not turn the held stick into a planned move", _session.IsPaused && _padPlan is null);
        PadAxis(JoyAxis.LeftX, 0);
        PadAxis(JoyAxis.LeftY, 0);
        await InputFrame();
        InputCheck("releasing that stick queues nothing", ReviewState().Party.All(actor =>
            actor.PendingAction?.CommandId.Value.StartsWith("input.pad-plan", StringComparison.Ordinal) != true));
        var after = ReviewState().Party.Select(actor => ToGodot(actor.Position)).ToArray();
        var followed = ReviewState().Party[1].CurrentAction?.CommandId.Value ?? ReviewState().Party[1].PendingAction?.CommandId.Value ?? "";
        InputCheck(string.Create(CultureInfo.InvariantCulture, $"the walker leads in real time ({FlatDistance(after[0], before[0]):0.0} m)"),
            _session.IsPaused && FlatDistance(after[0], before[0]) > .5f);
        InputCheck(string.Create(CultureInfo.InvariantCulture, $"the squad follows with the group move ({FlatDistance(after[1], before[1]):0.0} m, gap {FlatDistance(after[0], after[1]):0.0} m)"),
            FlatDistance(after[1], before[1]) > .3f && FlatDistance(after[0], after[1]) < 4 && followed.StartsWith("input.pad-follow.", StringComparison.Ordinal));

        await PadMouseMotion(new Vector2(40, 10));
        InputCheck("the mouse takes over and restores keyboard prompts", !InputDevice.UsingGamepad && !_abilityButton.PadGlyphShown
            && !_padPrompt.Visible && !_padHighlightRing.Visible);
        await PadPress(JoyButton.DpadRight);
        _reviewDrivesClock = false;
        _reviewSampleTick = null;
        await PadTrigger(JoyAxis.TriggerRight);
        InputCheck("the controller resumes play before it disconnects", InputDevice.UsingGamepad && !_session.IsPaused);
        Input.Singleton.EmitSignal(Input.SignalName.JoyConnectionChanged, ReviewPad, false);
        await InputFrame();
        await InputFrame();
        _reviewDrivesClock = true;
        _reviewSampleTick = _session.Tick;
        InputCheck("losing the active controller pauses play and returns to the mouse", _session.IsPaused && !InputDevice.UsingGamepad);
    }
}
