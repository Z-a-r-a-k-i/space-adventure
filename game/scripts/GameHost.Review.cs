using System.Globalization;
using System.Text.Json;
using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private bool _reviewDrivesClock;
    private string _reviewMode = string.Empty;
    private string _reviewSequence = "victory";
    private string _reviewCheckpoint = "all";
    private string _reviewOutput = string.Empty;
    private bool _requestedCheckpointReached;
    private float? _reviewPitch;
    private float? _reviewYaw;
    private readonly List<object> _reviewCheckpoints = [];

    private string ReviewArgument(string name, string fallback) => _developmentArguments
        .FirstOrDefault(argument => argument.StartsWith($"--{name}=", StringComparison.Ordinal))
        ?.Split('=', 2)[1] ?? fallback;

    private async Task RunSoloReviewAsync()
    {
        try
        {
            _reviewMode = ReviewArgument("solo-review", "capture");
            _reviewSequence = ReviewArgument("review-sequence", "victory");
            _reviewCheckpoint = ReviewArgument("review-checkpoint", _reviewMode == "live" ? "armed" : "all");
            if (_reviewMode is not ("capture" or "record" or "live" or "performance" or "input" or "smoke")
                || _reviewSequence is not ("victory" or "defeat"))
            {
                throw new InvalidOperationException("Unknown solo review profile.");
            }

            var distance = float.Parse(ReviewArgument("review-distance", "14.5"), CultureInfo.InvariantCulture);
            if (!float.IsFinite(distance) || distance < 7.5 || distance > 20) { throw new InvalidOperationException("Review camera outside supported range."); }
            if (ReviewArgument("review-pitch", "") is { Length: > 0 } pitchText)
            {
                _reviewPitch = float.Parse(pitchText, CultureInfo.InvariantCulture);
                if (!float.IsFinite(_reviewPitch.Value) || _reviewPitch < .45f || _reviewPitch > 1.15f)
                    throw new InvalidOperationException("Review pitch outside supported range.");
            }
            if (ReviewArgument("review-yaw", "") is { Length: > 0 } yawText)
            {
                _reviewYaw = float.Parse(yawText, CultureInfo.InvariantCulture);
                if (!float.IsFinite(_reviewYaw.Value) || _reviewYaw < -3.14f || _reviewYaw > 3.14f)
                    throw new InvalidOperationException("Review yaw outside supported range.");
            }
            if ((_reviewPitch.HasValue || _reviewYaw.HasValue) && _reviewMode is not ("capture" or "live"))
                throw new InvalidOperationException("Camera angle overrides are for capture/live review only.");
            var repositoryRoot = Path.GetFullPath(ProjectSettings.GlobalizePath("res://.."));
            var size = GetWindow().Size;
            var recoil = ReviewArgument("review-recoil", "restrained");
            _vanguardPresentation.StrongRecoil = recoil == "strong";
            _reviewOutput = Path.Combine(repositoryRoot, "artifacts", IsVisionReview ? "vision-review" : IsEscapeReview ? "escape-review" : IsPartyReview ? "party-review" : "solo-review",
                FormattableString.Invariant($"{_reviewMode}-{_reviewSequence}-{size.X:0}x{size.Y:0}-{distance:0.0}-{recoil}"));
            if (_reviewPitch.HasValue || _reviewYaw.HasValue)
                _reviewOutput += FormattableString.Invariant($"-pitch{_reviewPitch ?? .90f:0.00}-yaw{_reviewYaw ?? .68f:0.00}");
            EnsureSafeCaptureDirectory(repositoryRoot, _reviewOutput);
            File.WriteAllText(Path.Combine(_reviewOutput, "review.json"), JsonSerializer.Serialize(new
            {
                schema_version = 1, passed = false, status = "running", mode = _reviewMode, sequence = _reviewSequence,
            }, CaptureManifestJsonOptions));
            await CheckStationTacticalLayout();
            if (IsVisionReview) { await RunVisionReviewAsync(); return; }
            if (IsEscapeReview) { await RunEscapeReviewAsync(); return; }
            if (IsPartyReview) { await RunPartyReviewAsync(); return; }
            if (_reviewMode == "input") { await RunGraphicalInputReviewAsync(); return; }
            _reviewDrivesClock = true;
            _camera.InputEnabled = false;
            ReviewOrder(new SetPauseCommand(new CommandId("review.pause"), true));
            ReviewOrder(new InteractCommand(new CommandId("review.survivor"), _definition!.Protagonist.Id,
                new EntityId("interaction.survivor")));
            await ReviewUntil(state => state.ActiveDialogue is not null, 300, fast: true);
            if (_reviewCheckpoint == "briefing")
            {
                _camera.DistanceMeters = distance;
                _camera.FocusOn(ToGodot(ReviewState().Protagonist.Position));
                if (await ReviewCapture("briefing")) { return; }
            }
            ReviewOrder(new ChooseDialogueResponseCommand(new CommandId("review.survivor.choice"),
                _definition.Protagonist.Id, new EntityId("interaction.survivor"),
                new DialogueResponseId("response.reroute_service_power")));
            await ReviewWaitForPath(new WorldPosition(-10, 0, 2.75));
            ReviewOrder(new MoveActorCommand(new CommandId("review.enter.arena"), _definition.Protagonist.Id,
                new WorldPosition(-10, 0, 2.75)));
            await ReviewUntil(state => state.Encounter!.Phase == EncounterPhase.Readying, 300, fast: true);
            CheckActiveEncounterPitCommands();
            _camera.DistanceMeters = distance;
            _camera.SnapOcclusionToDesiredState();
            if (await ReviewCapture("ready")) { return; }
            await ReviewTicks(_definition.Combat.SoloEncounter.ReadyingTicks / 2);
            if (await ReviewCapture("draw")) { return; }
            await ReviewTicks(_definition.Combat.SoloEncounter.ReadyingTicks / 2);
            await ReviewTicks(6);
            if (await ReviewCapture("armed")) { return; }
            if (_reviewMode == "performance") { await RunRealtimePerformanceAsync(); return; }

            if (_reviewSequence == "defeat")
            {
                await ReviewUntil(state => state.Hostiles![0].CurrentAction?.Phase == PrimaryActionPhase.Windup, 200);
                await ReviewTicks(_definition.Combat.GetAttack(_definition.Combat.SoloHostile.BasicAttackId).WindupTicks);
                if (await ReviewCapture("contact")) { return; }
                await ReviewUntil(state => state.Encounter!.Phase == EncounterPhase.Defeat, 900, fast: true);
                await CheckCompletedDeathPresentation();
                if (await ReviewCapture("defeat")) { return; }
                ReviewOrder(new RestartEncounterCommand(new CommandId("review.retry"), _definition.Combat.SoloEncounter.Id));
                await ReviewTicks(_definition.Combat.SoloEncounter.ReadyingTicks);
                if (await ReviewCapture("retry")) { return; }
                await FinishSoloReview();
                return;
            }

            var origin = ReviewState().Protagonist.Position;
            ReviewOrder(new MoveActorCommand(new CommandId("review.armed.walk"), _definition.Protagonist.Id,
                new WorldPosition(origin.X + 1.5, origin.Y, origin.Z - 0.5)));
            await ReviewTicks(15);
            if (await ReviewCapture("armed-walk")) { return; }
            ReviewOrder(new StopActorsCommand(new CommandId("review.stop"), [_definition.Protagonist.Id]));
            ReviewAttack("review.fire");
            await ReviewTicks(_definition!.Combat.GetAttack(ReviewState().Protagonist.Combat!.BasicAttackId).WindupTicks);
            if (await ReviewCapture("fire")) { return; }
            await ReviewTicks(3);
            if (await ReviewCapture("recoil")) { return; }
            ReviewOrder(new StopActorsCommand(new CommandId("review.wait.for.hostile"), [_definition.Protagonist.Id]));
            await ReviewUntil(state => state.Hostiles![0].CurrentAction?.Phase == PrimaryActionPhase.Windup, 200);
            var meleeWindup = _definition.Combat.GetAttack(_definition.Combat.SoloHostile.BasicAttackId).WindupTicks;
            await ReviewTicks(meleeWindup / 2);
            if (await ReviewCapture("anticipation")) { return; }
            await ReviewTicks(meleeWindup - meleeWindup / 2);
            if (await ReviewCapture("contact")) { return; }

            ReviewAttack("review.resume.attack");
            await ReviewTicks(_definition!.Combat.GetAttack(ReviewState().Protagonist.Combat!.BasicAttackId).WindupTicks);
            await ReviewUntil(state => state.Hostiles![0].CurrentAction?.Phase == PrimaryActionPhase.Windup, 200);
            var beforeInterrupt = _session!.Observe().LatestEventSequence;
            ReviewOrder(new UseAbilityCommand(new CommandId("review.suppress"), _definition.Protagonist.Id,
                _definition.Combat.ProtagonistAbility.Id,
                new PositionAbilityTarget(ReviewState().Hostiles![0].Position)));
            await ReviewUntil(_ => _session.EventsSince(beforeInterrupt)
                .Any(item => item.Type == GameplayEventType.ActionInterrupted), 50);
            if (await ReviewCapture("interrupt")) { return; }
            await ReviewUntil(state => state.Hostiles![0].Combat.Health <= 20
                && state.Protagonist.CurrentAction?.Phase == PrimaryActionPhase.Recovery, 600);
            await ReviewTicks(3);
            if (await ReviewCapture("late-fire")) { return; }
            await ReviewUntil(state => state.Encounter!.Phase == EncounterPhase.Securing, 600);
            await ReviewTicks(_definition.Combat.SoloEncounter.SecuringTicks / 2);
            if (await ReviewCapture("holster")) { return; }
            await ReviewTicks(_definition.Combat.SoloEncounter.SecuringTicks - _definition.Combat.SoloEncounter.SecuringTicks / 2);
            if (await ReviewCapture("victory")) { return; }
            ReviewOrder(new InteractCommand(new CommandId("review.exit"), _definition.Protagonist.Id,
                new EntityId("interaction.service_door.solo_exit")));
            await ReviewUntil(state => state.Objective.Id.Value == "objective.recruit_protector", 300, fast: true);
            // The exit door just opened; wait until navigation publishes its link before ordering the walk.
            await ReviewWaitForPath(ReviewState().Interactions.Single(item => item.Id.Value == "interaction.protector").ApproachPosition);
            ReviewOrder(new InteractCommand(new CommandId("review.protector"), _definition.Protagonist.Id,
                new EntityId("interaction.protector")));
            await ReviewUntil(state => state.ActiveDialogue is not null, 300, fast: true);
            ReviewOrder(new ChooseDialogueResponseCommand(new CommandId("review.recruit"), _definition.Protagonist.Id,
                new EntityId("interaction.protector"), new DialogueResponseId("response.recruit_protector")));
            _camera.FocusOn(ToGodot(ReviewState().Protagonist.Position));
            if (await ReviewCapture("slice-complete")) { return; }
            await FinishSoloReview();
        }
        catch (Exception exception)
        {
            if (!string.IsNullOrEmpty(_reviewOutput) && Directory.Exists(_reviewOutput))
            {
                File.WriteAllText(Path.Combine(_reviewOutput, "review.json"), JsonSerializer.Serialize(new
                {
                    schema_version = 1, passed = false, status = "failed", error = exception.Message,
                    mode = _reviewMode, sequence = _reviewSequence, checkpoints = _reviewCheckpoints,
                    input_checks = _inputReviewChecks, last_observation = _session?.Observe(),
                }, CaptureManifestJsonOptions));
            }
            GD.PushError($"Solo review failed: {exception}");
            GD.Print(JsonSerializer.Serialize(new { solo_review_passed = false, error = exception.Message }, CaptureLogJsonOptions));
            GetTree().Quit(1);
        }
    }

    private StationRouteObservation ReviewState() => _session!.Observe().StationRoute!;

    private async Task ReviewWaitForPath(WorldPosition destination)
    {
        var pathfinder = new GodotSpatialPathfinder(GetWorld3D().NavigationMap);
        for (var frame = 0; frame < MaximumNavigationInitializationFrames; frame++)
        {
            if (pathfinder.FindPath(_definition!.Protagonist.Id, ReviewState().Protagonist.Position, destination).IsReachable)
            {
                return;
            }
            await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        throw new InvalidOperationException("Navigation did not synchronize the reviewed door state.");
    }

    private void ReviewOrder(IGameCommand command)
    {
        var acknowledgement = _session!.Execute(command);
        if (!acknowledgement.Accepted)
        {
            throw new InvalidOperationException($"Review order {command.CommandId} rejected: {acknowledgement.RejectionCode}.");
        }
        _reviewSampleTick = _session.Tick;
        SynchronizePresentation();
    }

    private void ReviewAttack(string id) => ReviewOrder(new AssignBasicAttackTargetCommand(
        new CommandId(id), _definition!.Protagonist.Id, _definition.Combat.SoloHostile.Id));

    private async Task ReviewTicks(int ticks, bool fast = false)
    {
        for (var index = 0; index < ticks; index++)
        {
            _session!.StepWhilePaused(1);
            if (!fast && _reviewMode == "record")
            {
                // Two 60 fps samples per 30 Hz tick. MovieWriter controls the
                // rendered delta; the movie is not a frame-pacing benchmark.
                _reviewSampleTick = _session!.Tick - 0.5;
                SynchronizePresentation();
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            }
            _reviewSampleTick = _session.Tick;
            SynchronizePresentation();
            AdvanceServiceDoorPresentation(1.0f / GameSession.TicksPerSecond);
            if (!fast && _reviewMode == "record")
            {
                await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            }
        }
    }

    private async Task ReviewUntil(Func<StationRouteObservation, bool> condition, int maximumTicks, bool fast = false)
    {
        for (var index = 0; index <= maximumTicks; index++)
        {
            if (condition(ReviewState())) { return; }
            if (index < maximumTicks) { await ReviewTicks(1, fast); }
        }
        throw new InvalidOperationException($"Review condition was not reached within {maximumTicks} ticks at tick {_session!.Tick}.");
    }

    private async Task<bool> ReviewCapture(string checkpoint)
    {
        if (_reviewMode == "smoke") { return false; }
        _reviewSampleTick = _session!.Tick;
        SynchronizePresentation();
        if (_reviewPitch.HasValue) { _camera.PitchRadians = _reviewPitch.Value; }
        if (_reviewYaw.HasValue) { _camera.YawRadians = _reviewYaw.Value; }
        _camera.SnapOcclusionToDesiredState();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        if (_reviewMode == "input" && checkpoint != "dialogue") { CheckWorldHealth(checkpoint); }
        var diagnostics = JsonSerializer.Deserialize<JsonElement>(GetPresentationDiagnosticsJson());
        var length = diagnostics.GetProperty("vanguard").GetProperty("weapon_length_m").GetDouble();
        if (Math.Abs(length - 0.82) > 0.0164)
        {
            throw new InvalidOperationException($"Weapon length at {checkpoint} is {length:0.0000} m; expected 0.82 m ±2%.");
        }
        var vanguard = diagnostics.GetProperty("vanguard");
        if (ReviewState().Encounter!.Phase == EncounterPhase.Active && !ReviewState().Protagonist.Combat!.IsDefeated
            && (vanguard.GetProperty("primary_grip_error_m").GetDouble() > 0.03
                || vanguard.GetProperty("support_grip_error_m").GetDouble() > 0.03))
        {
            File.WriteAllText(Path.Combine(_reviewOutput, checkpoint + "-failure.json"), diagnostics.GetRawText());
            GetViewport().GetTexture().GetImage().SavePng(Path.Combine(_reviewOutput, checkpoint + ".png"));
            throw new InvalidOperationException($"Palm-to-grip tolerance exceeded at {checkpoint}: primary={vanguard.GetProperty("primary_grip_error_m")}, support={vanguard.GetProperty("support_grip_error_m")}.");
        }
        if (checkpoint == "retry" && diagnostics.GetProperty("effects").GetArrayLength() > 0)
        {
            throw new InvalidOperationException("Retry retained effects from the previous attempt.");
        }
        if (IsPartyReview) { ValidatePartyPresentation(checkpoint, diagnostics); }
        if (IsEscapeReview) { ValidateEscapePresentation(diagnostics); }
        if (!IsPartyReview && checkpoint is "fire" or "recoil" or "late-fire")
        {
            static Vector3 Vector(JsonElement array) => new(array[0].GetSingle(), array[1].GetSingle(), array[2].GetSingle());
            var muzzle = Vector(vanguard.GetProperty("muzzle_world"));
            var towardTarget = (ToGodot(ReviewState().Hostiles![0].Position) + Vector3.Up * 1.15f - muzzle).Normalized();
            var alignment = towardTarget.Dot(Vector(vanguard.GetProperty("muzzle_direction")));
            if (alignment < 0.95f) { throw new InvalidOperationException($"Muzzle points away from target at {checkpoint}: dot={alignment:0.000}."); }
            var bolt = _combatPresentationEffects.Select(effect => effect.Node).OfType<CarbineProjectile>().Single();
            if (checkpoint == "fire" && (bolt.Progress != 0 || bolt.Position.DistanceTo(muzzle) > 0.001f))
            {
                throw new InvalidOperationException("The released projectile did not start at the actual muzzle.");
            }
            if (checkpoint is "recoil" or "late-fire" && (bolt.Progress <= 0 || bolt.Progress >= 1
                || bolt.Position.DistanceTo(bolt.LaunchPosition) < 0.01f
                || bolt.Position.DistanceTo(bolt.TargetPosition) >= bolt.LaunchPosition.DistanceTo(bolt.TargetPosition)))
            {
                throw new InvalidOperationException($"Projectile did not travel toward the target at {checkpoint}.");
            }
        }
        _reviewCheckpoints.Add(new
        {
            checkpoint,
            rendered_frame = Engine.GetFramesDrawn(),
            camera = new { distance = _camera.DistanceMeters, pitch = _camera.PitchRadians, yaw = _camera.YawRadians },
            tick = _session.Tick,
            encounter = ReviewState().Encounter!.Phase.ToString(),
            current_action = ReviewState().Protagonist.CurrentAction,
            diagnostics,
        });
        if (_reviewMode is "capture" or "input" && (_reviewCheckpoint == "all" || _reviewCheckpoint == checkpoint))
        {
            var image = GetViewport().GetTexture().GetImage();
            var result = image.SavePng(Path.Combine(_reviewOutput, checkpoint + ".png"));
            image.Dispose();
            if (result != Error.Ok) { throw new IOException($"Checkpoint capture failed: {result}."); }
        }

        if (_reviewCheckpoint != checkpoint) { return false; }
        _requestedCheckpointReached = true;
        if (_reviewMode == "live")
        {
            _reviewDrivesClock = false;
            _reviewSampleTick = null;
            _camera.InputEnabled = !_dialogueInputActive;
            if (_dialogueInputActive) { _cameraInputBeforeDialogue = true; }
            _autoQuitSeconds = double.Parse(ReviewArgument("auto-quit-seconds", "0"), CultureInfo.InvariantCulture);
            SetFeedback(_dialogueInputActive ? "Choose a response to continue."
                : $"Review ready: {checkpoint}. Space resumes; X stops the selected actor.", new Color("8fe6ff"));
            WriteReviewManifest();
        }
        else
        {
            await FinishSoloReview();
        }
        return true;
    }

    private void WriteReviewManifest(bool passed = true)
    {
        var report = new
        {
            schema_version = 1,
            passed,
            mode = _reviewMode,
            sequence = _reviewSequence,
            requested_checkpoint = _reviewCheckpoint,
            evidence_scope = _reviewMode == "performance"
                ? "Measurement completion; assess frame pacing in performance.json."
                : "Gameplay and presentation checks; owner visual acceptance is separate.",
            timing = "30 Hz simulation; presentation sampled at tick/fraction; record has fixed 60 fps deltas",
            checkpoints = _reviewCheckpoints,
            input_checks = _inputReviewChecks,
        };
        File.WriteAllText(Path.Combine(_reviewOutput, "review.json"), JsonSerializer.Serialize(report, CaptureManifestJsonOptions));
    }

    private async Task FinishSoloReview()
    {
        if (_reviewCheckpoint != "all" && !_requestedCheckpointReached)
        {
            throw new InvalidOperationException("The requested checkpoint was not reached.");
        }
        WriteReviewManifest();
        if (_reviewMode == "record")
        {
            // Drain MovieWriter's final audio mix before freeing looping playback.
            if (_stationAmbience is not null) { _stationAmbience.Stop(); _stationAmbience.Stream = null; }
            foreach (var effect in _combatPresentationEffects)
            {
                if (effect.Node is AudioStreamPlayer3D audio) { audio.Stop(); audio.Stream = null; }
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        GD.Print(JsonSerializer.Serialize(new { solo_review_passed = true, output = _reviewOutput }, CaptureLogJsonOptions));
        GetTree().Quit();
    }
}
