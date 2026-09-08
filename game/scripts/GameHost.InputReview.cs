using Godot;
using SpaceAdventure.Core;
using System.Text.Json;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private readonly List<string> _inputReviewChecks = [];

    private void InputCheck(string name, bool passed)
    {
        if (!passed) { throw new InvalidOperationException($"Graphical input check failed: {name}. Human commands: {_humanCommandSequence}; feedback: {_feedbackLabel.Text}"); }
        _inputReviewChecks.Add(name);
    }

    private async Task InputFrame()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (DisplayServer.GetName() == "headless") { return; }
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
    }

    private async Task InputKey(Key key, bool shift = false)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = true, ShiftPressed = shift });
        await InputFrame();
        Input.ParseInputEvent(new InputEventKey { Keycode = key, PhysicalKeycode = key, Pressed = false });
        await InputFrame();
    }

    private async Task CheckUnavailableStop(string context)
    {
        InputCheck($"Stop is unavailable during {context}", !_stopButton.Visible || _stopButton.Disabled);
        var commandSequence = _humanCommandSequence;
        var eventSequence = _session!.Observe().LatestEventSequence;
        var feedback = _feedbackLabel.Text;
        await InputKey(Key.X);
        InputCheck($"X preserves commands and feedback during {context}", _humanCommandSequence == commandSequence
            && _session.Observe().LatestEventSequence == eventSequence && _feedbackLabel.Text == feedback);
    }

    private async Task InputClick(Vector2 position, MouseButton button, bool alt = false)
    {
        // ParseInputEvent enters at window coordinates; the viewport then applies
        // its stretch transform. Control/world projections above are local.
        var windowPosition = GetViewport().GetFinalTransform() * position;
        Input.ParseInputEvent(new InputEventMouseButton { Position = windowPosition, GlobalPosition = windowPosition, ButtonIndex = button, Pressed = true, ShiftPressed = Input.IsKeyPressed(Key.Shift), AltPressed = alt });
        Input.ParseInputEvent(new InputEventMouseButton { Position = windowPosition, GlobalPosition = windowPosition, ButtonIndex = button, Pressed = false, ShiftPressed = Input.IsKeyPressed(Key.Shift), AltPressed = alt });
        await InputFrame();
    }

    private Task InputWorldClick(Vector3 position, MouseButton button = MouseButton.Right)
    {
        var point = _camera.UnprojectPosition(position);
        InputCheck("projected click inside viewport", !_camera.IsPositionBehind(position)
            && GetViewport().GetVisibleRect().HasPoint(point));
        return InputClick(point, button);
    }

    private Task InputInteraction(string id) => InputWorldClick(_interactionViews[id].GlobalPosition + Vector3.Up * 0.8f);

    private async Task RunGraphicalInputReviewAsync()
    {
        _reviewDrivesClock = true;
        _camera.InputEnabled = true;
        _camera.DistanceMeters = float.Parse(ReviewArgument("review-distance", "14.5"), System.Globalization.CultureInfo.InvariantCulture);
        await InputFrame();
        if (!_session!.IsPaused) { await InputKey(Key.Space); }
        InputCheck("Space pauses gameplay", _session.IsPaused);
        await InputInteraction("interaction.survivor");
        InputCheck("survivor right click creates human order", ReviewState().Protagonist.PendingAction?.CommandId.Value.StartsWith("input.", StringComparison.Ordinal) == true);
        await ReviewUntil(state => state.ActiveDialogue is not null, 300, fast: true);
        await CheckUnavailableStop("dialogue");
        await InputKey(Key.Key1);
        InputCheck("dialogue number key selects route", ReviewState().ActiveDialogue is null);
        await InputInteraction("interaction.service_door.entry");
        await ReviewUntil(state => state.Interactions.Single(item => item.Id.Value == "interaction.service_door.entry").State
            == InteractionState.Completed, 300, fast: true);
        InputCheck("service door right click opens route", true);
        await InputKey(Key.F);
        var destination = new WorldPosition(-10, 0, 2.75);
        await ReviewWaitForPath(destination);
        // Move the camera with real key state so the adjoining arena floor can be clicked.
        var focus = _camera.FocusPoint;
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.W, Keycode = Key.W, Pressed = true });
        for (var frame = 0; frame < 8; frame++) { await InputFrame(); }
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.W, Keycode = Key.W, Pressed = false });
        InputCheck("camera pans during tactical pause", _camera.FocusPoint.DistanceTo(focus) > 0.1f);
        await InputWorldClick(ToGodot(destination));
        InputCheck("floor right click creates move order", ReviewState().Protagonist.PendingAction?.Kind == PrimaryActionKind.Move);
        await ReviewUntil(state => state.Encounter!.Phase == EncounterPhase.Readying, 300, fast: true);
        var pausedTick = _session.Tick;
        focus = _camera.FocusPoint;
        var yaw = _camera.YawRadians;
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Q, Keycode = Key.Q, Pressed = true });
        for (var frame = 0; frame < 5; frame++) { await InputFrame(); }
        Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = Key.Q, Keycode = Key.Q, Pressed = false });
        InputCheck("camera turns while gameplay tick is frozen", _camera.YawRadians != yaw && _session.Tick == pausedTick);
        InputCheck("combat framing does not override camera control", _camera.FocusPoint.IsEqualApprox(focus));
        await InputKey(Key.R);
        await ReviewTicks(54);
        InputCheck("draw reaches armed state", ReviewState().Encounter!.Phase == EncounterPhase.Active);
        var beforeRealtime = _session.Tick;
        _reviewSampleTick = null;
        _reviewDrivesClock = false;
        await InputKey(Key.Space);
        InputCheck("Space resumes real-time gameplay", !_session.IsPaused);
        var realtimeSample = System.Diagnostics.Stopwatch.StartNew();
        while (realtimeSample.Elapsed.TotalSeconds < .6) { await InputFrame(); }
        var realtimeSampleTick = _presentationTick;
        var realtimePose = JsonSerializer.Deserialize<JsonElement>(GetPresentationDiagnosticsJson());
        _reviewDrivesClock = true;
        await InputKey(Key.Space);
        _reviewSampleTick = realtimeSampleTick;
        SynchronizePresentation();
        await InputFrame();
        var soughtPose = JsonSerializer.Deserialize<JsonElement>(GetPresentationDiagnosticsJson());
        static Vector3 Muzzle(JsonElement pose)
        {
            var values = pose.GetProperty("vanguard").GetProperty("muzzle_world");
            return new Vector3(values[0].GetSingle(), values[1].GetSingle(), values[2].GetSingle());
        }
        InputCheck("paused seek agrees with the real-time pose", Muzzle(realtimePose).DistanceTo(Muzzle(soughtPose)) < 0.001f
            && Math.Abs(realtimePose.GetProperty("vanguard").GetProperty("clip_seconds").GetDouble()
                - soughtPose.GetProperty("vanguard").GetProperty("clip_seconds").GetDouble()) < 1.0 / 60);
        _reviewSampleTick = _session.Tick;
        SynchronizePresentation();
        InputCheck("real-time frames advance simulation before pausing", _session.IsPaused && _session.Tick > beforeRealtime + 5);

        if (_reviewSequence == "defeat")
        {
            await ReviewUntil(state => state.Encounter!.Phase == EncounterPhase.Defeat, 1000, fast: true);
            InputCheck("defeat pauses and exposes retry", _session.IsPaused && _retryButton.Visible);
            await CheckUnavailableStop("defeat");
            await CheckCompletedDeathPresentation();
            await ReviewCapture("defeat");
            var button = _retryButton.GetGlobalRect();
            InputCheck("retry button fits viewport", GetViewport().GetVisibleRect().Encloses(button));
            await InputClick(button.GetCenter(), MouseButton.Left);
            InputCheck("retry button creates a new attempt", ReviewState().Encounter!.Attempt == 2
                && ReviewState().Protagonist.Combat!.Health == 100);
            await ReviewTicks(_definition!.Combat.SoloEncounter.ReadyingTicks);
            await ReviewCapture("retry");
            FinishSoloReview();
            return;
        }

        await InputWorldClick(_securityEnforcerView.GlobalPosition + Vector3.Up);
        InputCheck("enemy right click creates attack order", ReviewState().Protagonist.PendingAction?.Kind == PrimaryActionKind.Attack);
        await ReviewTicks(9);
        var instance = ReviewState().Protagonist.CurrentAction!.InstanceId;
        await InputWorldClick(_securityEnforcerView.GlobalPosition + Vector3.Up);
        InputCheck("duplicate target click preserves attack cycle", ReviewState().Protagonist.CurrentAction!.InstanceId == instance);
        var projectileEffect = _combatPresentationEffects.Single(effect => effect.Node is CarbineProjectile);
        var projectile = (CarbineProjectile)projectileEffect.Node;
        InputCheck("shot begins at the actual muzzle", projectile.Progress == 0
            && projectile.Position.DistanceTo(_vanguardPresentation.MuzzlePosition) < 0.001f);
        var projectilePosition = projectile.Position;
        var projectileTick = _session.Tick;
        _reviewDrivesClock = false;
        _reviewSampleTick = null;
        for (var frame = 0; frame < 12; frame++) { await InputFrame(); }
        InputCheck("tactical pause freezes the projectile across rendered frames", _session.IsPaused
            && _session.Tick == projectileTick && projectile.Position.IsEqualApprox(projectilePosition));
        _reviewDrivesClock = true;
        _reviewSampleTick = _session.Tick;
        await ReviewTicks(1);
        InputCheck("projectile advances with the presentation tick", projectile.Progress > 0
            && projectile.Position.DistanceTo(projectilePosition) > 0.01f);
        var delayedImpact = _combatPresentationEffects.Single(effect => effect.BornTick == projectileEffect.BornTick
            && effect.Node is MeshInstance3D && effect.DelaySeconds > 0);
        var delayedNumber = _combatPresentationEffects.Single(effect => effect.BornTick == projectileEffect.BornTick
            && effect.Node is Label3D);
        InputCheck("impact flash waits for projectile arrival", !delayedImpact.Node.Visible);
        InputCheck("damage number waits with the impact flash", !delayedNumber.Node.Visible
            && delayedNumber.DelaySeconds == delayedImpact.DelaySeconds);
        await ReviewTicks((int)Math.Ceiling(projectile.FlightSeconds * GameSession.TicksPerSecond));
        InputCheck("arrival removes the bolt and reveals the impact", !_combatPresentationEffects.Contains(projectileEffect)
            && delayedImpact.Node.Visible);
        InputCheck("arrival reveals the damage number", delayedNumber.Node.Visible);
        // Wait for a strike that can actually be interrupted after our remaining
        // offensive recovery, instead of accepting the final frame of any windup.
        await ReviewUntil(state => state.Hostiles![0].CurrentAction is { Phase: PrimaryActionPhase.Windup } strike
            && strike.PhaseTicksRemaining > _definition!.Combat.ProtagonistAbility.WindupTicks
                + Math.Max(0, state.Protagonist.Combat!.OffensiveRecoveryUntilTick - _session.Tick), 120);
        var sequence = _session.Observe().LatestEventSequence;
        await InputClick(_abilityButton.GetGlobalRect().GetCenter(), MouseButton.Left);
        InputCheck("ability button enters targeting", _abilityTargeting);
        await InputWorldClick(ToGodot(ReviewState().Hostiles![0].Position), MouseButton.Left);
        InputCheck("left click confirms targeted ability", ReviewState().Protagonist.PendingAction?.Kind == PrimaryActionKind.Ability && !_abilityTargeting);
        await ReviewUntil(_ => _session.EventsSince(sequence).Any(item => item.Type == GameplayEventType.ActionInterrupted), 50);
        InputCheck("targeted ability interrupts hostile windup", true);
        await InputKey(Key.X);
        await ReviewTicks(1);
        InputCheck("X stops and clears explicit target", ReviewState().Protagonist.CurrentAction is null
            && ReviewState().Protagonist.Combat!.RememberedAttackTargetId is null);
        await InputWorldClick(_securityEnforcerView.GlobalPosition + Vector3.Up);
        await ReviewUntil(state => state.Encounter!.Phase == EncounterPhase.Securing, 700);
        await CheckUnavailableStop("securing");
        await ReviewUntil(state => state.Encounter!.Phase == EncounterPhase.Victory, 60);
        await ReviewCapture("victory");
        await InputInteraction("interaction.service_door.solo_exit");
        await ReviewUntil(state => state.Objective.Id.Value == "objective.recruit_protector", 300, fast: true);
        await InputKey(Key.F);
        await InputInteraction("interaction.protector");
        await ReviewUntil(state => state.ActiveDialogue is not null, 300, fast: true);
        await InputKey(Key.Enter);
        InputCheck("dialogue Enter recruits Protector", ReviewState().Party.Count == 2);
        InputCheck("slice end does not complete scenario or unlock airlock", ReviewState().Phase == ScenarioPhase.InProgress
            && ReviewState().Interactions.Single(item => item.Id.Value == "interaction.evacuation_airlock").State == InteractionState.Unavailable);
        await InputKey(Key.F);
        await ReviewCapture("slice-complete");
        FinishSoloReview();
    }
}
