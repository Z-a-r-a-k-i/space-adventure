using System.Text.Json;
using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private double _presentationTick;
    private double? _reviewSampleTick;
    private float _presentationDeltaSeconds;
    private long _effectEventTick;
    private readonly Dictionary<EntityId, MotionSample> _motionSamples = [];
    private int _framedEncounterAttempt;
    private EncounterId? _framedEncounterId;
    private double _defeatPresentationSeconds;
    private AudioStreamPlayer3D? _stationAmbience;
    private readonly Dictionary<string, int> _audioVariants = [];

    private void AdvanceDefeatPresentation(GameObservation observation, double delta)
    {
        if (observation.StationRoute?.Encounter?.Phase == EncounterPhase.Defeat)
        {
            // Rules remain paused. Only the terminal fall and released effects settle.
            var duration = Math.Max(_medicPresentation.DownDurationSeconds, Math.Max(_vanguardPresentation.DownDurationSeconds,
                _protectorPartyPresentation.DownDurationSeconds)) + .5;
            _defeatPresentationSeconds = Math.Min(duration, _defeatPresentationSeconds + Math.Clamp(delta, 0, .25));
        }
        else if (_defeatPresentationSeconds > 0)
        {
            _defeatPresentationSeconds = 0;
            _presentationTick = observation.Tick;
        }
    }

    private void PrepareProjectileResources()
    {
        // Forward+ precompiles hidden instanced geometry. Keep shared resources alive
        // from scene startup instead of constructing materials on the first shot.
        var warmup = new Node3D { Name = "ProjectileResources", Visible = false, ProcessMode = ProcessModeEnum.Disabled };
        AddChild(warmup);
        foreach (var color in new[] { new Color("57ddff"), new Color("66f5ff"), new Color("f2c879"), new Color("ff7659") })
        {
            var projectile = new CarbineProjectile();
            projectile.Configure(Vector3.Zero, Vector3.Forward, color);
            projectile.Sample(0.5f);
            warmup.AddChild(projectile);
        }
        foreach (var signature in Enum.GetValues<CombatSignature>())
        {
            var effect = new CombatContactEffect();
            effect.Configure(signature, Vector3.Zero, Vector3.Forward);
            warmup.AddChild(effect);
        }
        GameAudio.Ensure(this);
        CombatAudio.Warmup();
        var audition = System.Environment.GetEnvironmentVariable("SPACE_ADVENTURE_AUDIO_REVIEW");
        if (!string.IsNullOrWhiteSpace(audition)) { CombatAudio.WriteAudition(audition); }
        if (DisplayServer.GetName() != "headless" && _reviewMode != "capture")
        {
            _stationAmbience = new AudioStreamPlayer3D
            {
                Name = "StationVentilation", Stream = CombatAudio.Get("ambience"), Bus = CombatAudio.Bus("ambience"),
                VolumeDb = CombatAudio.VolumeDb("ambience"), UnitSize = 14, MaxDistance = 60, MaxDb = CombatAudio.VolumeDb("ambience"),
                Position = GetNode<Marker3D>("Markers/PartyEncounterTrigger").GlobalPosition + Vector3.Up * 3,
            };
            AddChild(_stationAmbience);
            _stationAmbience.Play();
        }
    }

    private Vector3 SamplePosition(EntityId id, WorldPosition position, long tick, int attempt)
    {
        var next = ToGodot(position);
        if (!_motionSamples.TryGetValue(id, out var sample) || sample.Attempt != attempt || tick < sample.Tick)
        {
            sample = new MotionSample(next, next, tick, tick, attempt);
        }
        else if (tick > sample.Tick)
        {
            sample = new MotionSample(sample.Current, next, sample.Tick, tick, attempt);
        }
        _motionSamples[id] = sample;
        var fraction = (float)Math.Clamp((_presentationTick - sample.PreviousTick) / Math.Max(1, sample.Tick - sample.PreviousTick), 0, 1);
        return sample.Previous.Lerp(sample.Current, fraction);
    }

    private Vector3 TravelDirection(EntityId id) => _motionSamples.TryGetValue(id, out var sample)
        ? sample.Current - sample.Previous : Vector3.Zero;

    private void FrameCombatEntry(StationRouteObservation route)
    {
        if (route.Encounter is not { Phase: EncounterPhase.Readying } encounter
            || encounter.Attempt == _framedEncounterAttempt && encounter.Id == _framedEncounterId) { return; }
        _framedEncounterAttempt = encounter.Attempt;
        _framedEncounterId = encounter.Id;
        _motionSamples.Clear();
        _facingSamples.Clear();
        var positions = route.Party.Select(actor => ToGodot(actor.Position))
            .Concat(PlayerVisibleHostiles(route).Where(hostile => CanTargetVisibleHostile(route, hostile))
                .Select(hostile => ToGodot(hostile.Position))).ToArray();
        var midpoint = positions.Aggregate(Vector3.Zero, (sum, position) => sum + position) / positions.Length;
        var viewDirection = _camera.ProjectRayNormal(GetViewport().GetVisibleRect().GetCenter());
        var towardCamera = new Vector3(-viewDirection.X, 0, -viewDirection.Z).Normalized();
        midpoint += towardCamera * 1.4f;
        // Frame the crew and currently known threats without revealing other rooms.
        // One explicit frame at entry/retry; later camera input remains under player control.
        if (route.Party.Count == 3) { _camera.DistanceMeters = 20; }
        _camera.FocusOn(midpoint);
    }

    private sealed record MotionSample(Vector3 Previous, Vector3 Current, long PreviousTick, long Tick, int Attempt);

    private double? EnforcerClipSeconds(PrimaryActionObservation? action)
    {
        if (action is null || action.Phase == PrimaryActionPhase.Moving || action.Interrupted) { return null; }
        var progress = (float)Math.Clamp((_presentationTick - action.PhaseStartedTick) / Math.Max(1, action.PhaseTicksTotal), 0, 1);
        const double contactSeconds = 0.3; // Authored Right Hook contact at frame 10.
        if (action.Phase == PrimaryActionPhase.Windup)
        {
            // Show the loaded shoulder early, sustain it while the player can react,
            // then commit through the final quarter to the unchanged contact tick.
            if (progress < .35f) { return .17 * Mathf.SmoothStep(0, 1, progress / .35f); }
            if (progress < .75f) { return .17 + .02 * Mathf.SmoothStep(0, 1, (progress - .35f) / .4f); }
            return .19 + .11 * Mathf.SmoothStep(0, 1, (progress - .75f) / .25f);
        }
        return contactSeconds + progress * (_securityEnforcerPresentation.ClipLength(HumanoidPresentationAction.MeleeStrike) - contactSeconds);
    }

    private void UpdatePresentationTime()
    {
        if (_session is null) { return; }
        var next = _reviewSampleTick ?? (_session.IsPaused
            ? _session.Tick
            : Math.Max(0, _session.Tick - 1 + _session.TickFraction));
        if (_defeatPresentationSeconds > 0) { next = _session.Tick + _defeatPresentationSeconds * GameSession.TicksPerSecond; }
        if (_reviewSampleTick is null) { next = Math.Max(_presentationTick, next); }
        _presentationDeltaSeconds = (float)Math.Clamp((next - _presentationTick) / GameSession.TicksPerSecond, 0, 0.25);
        _presentationTick = next;
    }

    private void SynchronizePresentation()
    {
        if (_session is null) { return; }
        var observation = _session.Observe();
        AdvanceDefeatPresentation(observation, 0);
        UpdatePresentationTime();
        foreach (var item in _session.EventsSince(_presentationEventSequence))
        {
            if (item.Tick > _presentationTick) { break; }
            if (item.Detail is AttackEventDetail attack && item.Type == GameplayEventType.AttackReleased
                && IsPresentationSubjectVisible(observation.StationRoute!, attack.SourceId))
            {
                ArmedPresentation(attack.SourceId)?.NotifyShot(item.Tick);
            }
            if (item.Detail is AbilityReleasedEventDetail ability
                && (ability.AbilityId == _definition!.Combat.ProtagonistAbility.Id
                    || ability.AbilityId == _definition.Combat.Burst.Id))
            {
                ArmedPresentation(ability.SourceId)?.NotifyShot(item.Tick);
            }
        }
        RenderObservation(observation, timeAlreadyUpdated: true);
        ProcessCombatPresentationEvents(observation);
        AdvanceCombatPresentationClock(observation.StationRoute!);
    }

    private void PlayCombatCue(string cue, Vector3 position, float delaySeconds = 0, EntityId? visibilitySubject = null)
    {
        if (_reviewMode == "capture" || DisplayServer.GetName() == "headless") { return; }
        _audioVariants.TryGetValue(cue, out var variant);
        _audioVariants[cue] = (variant + 1) % 4;
        var player = new AudioStreamPlayer3D
        {
            Stream = CombatAudio.Get(cue, variant), Position = position, Bus = CombatAudio.Bus(cue),
            VolumeDb = CombatAudio.VolumeDb(cue), MaxDb = CombatAudio.VolumeDb(cue),
            UnitSize = 10, MaxDistance = 38,
        };
        AddChild(player);
        TrackCombatEffect(new TimedPresentationEffect(player,
            (float)player.Stream.GetLength(), _effectEventTick, delaySeconds), visibilitySubject);
    }

    private void SpawnInterruptCue(Vector3 position, float delaySeconds = 0, EntityId? visibilitySubject = null)
    {
        var label = new Label3D
        {
            Position = position + Vector3.Up * 1.85f, Text = "INTERRUPTED", FontSize = 32, OutlineSize = 8,
            Modulate = new Color("f2c879"), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
        };
        AddChild(label);
        TrackCombatEffect(new TimedPresentationEffect(label, 0.60f, _effectEventTick, delaySeconds), visibilitySubject);
    }

    public string GetPresentationDiagnosticsJson() => JsonSerializer.Serialize(new
    {
        schema_version = 1,
        tick = _session?.Tick,
        presentation_tick = _presentationTick,
        protagonist_position = new[] { _protagonistView.GlobalPosition.X, _protagonistView.GlobalPosition.Y, _protagonistView.GlobalPosition.Z },
        enforcer_position = new[] { _securityEnforcerView.GlobalPosition.X, _securityEnforcerView.GlobalPosition.Y, _securityEnforcerView.GlobalPosition.Z },
        vanguard = _vanguardPresentation.GetDiagnostics(),
        protector = _protectorPartyPresentation.GetDiagnostics(),
        medic = _medicPresentation.GetDiagnostics(),
        ranged_enforcers = _enemyViews.Where(pair => pair.Value.Armed is not null).Select(pair => new { actor_id = pair.Key.Value, pose = pair.Value.Armed!.GetDiagnostics() }),
        departure_seconds = _departureSeconds,
        sentry = _enemyViews.Values.FirstOrDefault(view => view.Sentry is not null)?.Sentry?.GetDiagnostics(),
        selected_actor_ids = _selectedActorIds.Select(id => id.Value),
        focused_actor_id = _focusedActorId?.Value,
        enforcer = _securityEnforcerPresentation.GetDiagnostics(),
        lintels = _camera.ObserveLintels(),
        projectiles = _combatPresentationEffects.Select(effect => effect.Node)
            .OfType<CarbineProjectile>().Select(projectile => projectile.GetDiagnostics()),
        contact_effects = _combatPresentationEffects.Select(effect => effect.Node)
            .OfType<CombatContactEffect>().Select(effect => effect.GetDiagnostics()),
        enemy_intent = _enemyIntentCues.Select(pair => new { actor_id = pair.Key.Value, cue = pair.Value.GetDiagnostics() }),
        effects = _combatPresentationEffects.Select(effect => new
        {
            born_tick = effect.BornTick,
            age_seconds = (_presentationTick - effect.BornTick) / GameSession.TicksPerSecond,
            duration_seconds = effect.DurationSeconds,
            delay_seconds = effect.DelaySeconds,
        }),
    }, CaptureManifestJsonOptions);
}
