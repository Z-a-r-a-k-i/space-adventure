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

    private void PrepareProjectileResources()
    {
        // Forward+ precompiles hidden instanced geometry. Keep shared resources alive
        // from scene startup instead of constructing materials on the first shot.
        var warmup = new Node3D { Name = "ProjectileResources", Visible = false, ProcessMode = ProcessModeEnum.Disabled };
        AddChild(warmup);
        foreach (var color in new[] { new Color("57ddff"), new Color("66f5ff") })
        {
            var projectile = new CarbineProjectile();
            projectile.Configure(Vector3.Zero, Vector3.Forward, color);
            projectile.Sample(0.5f);
            warmup.AddChild(projectile);
        }
        foreach (var cue in new[] { "carbine", "impact", "aid", "interrupt" }) { _ = CombatAudio.Get(cue); }
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
            || encounter.Attempt == _framedEncounterAttempt) { return; }
        _framedEncounterAttempt = encounter.Attempt;
        var midpoint = ToGodot(route.Protagonist.Position).Lerp(ToGodot(route.Hostiles!.Single().Position), 0.25f);
        // One explicit frame at entry/retry. Subsequent player camera input is never overridden.
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
            return progress < 0.75f
                ? 0.1 * Mathf.SmoothStep(0, 1, progress / 0.75f)
                : 0.1 + 0.2 * Mathf.SmoothStep(0, 1, (progress - 0.75f) / 0.25f);
        }
        return contactSeconds + progress * (_securityEnforcerPresentation.ClipLength(HumanoidPresentationAction.MeleeStrike) - contactSeconds);
    }

    private void UpdatePresentationTime()
    {
        if (_session is null) { return; }
        var next = _reviewSampleTick ?? (_session.IsPaused
            ? _session.Tick
            : Math.Max(0, _session.Tick - 1 + _session.TickFraction));
        if (_reviewSampleTick is null) { next = Math.Max(_presentationTick, next); }
        _presentationDeltaSeconds = (float)Math.Clamp((next - _presentationTick) / GameSession.TicksPerSecond, 0, 0.25);
        _presentationTick = next;
    }

    private void SynchronizePresentation()
    {
        if (_session is null) { return; }
        var observation = _session.Observe();
        UpdatePresentationTime();
        foreach (var item in _session.EventsSince(_presentationEventSequence))
        {
            if (item.Tick > _presentationTick) { break; }
            if (item.Detail is AttackEventDetail attack && item.Type == GameplayEventType.AttackReleased
                && attack.SourceId == _definition!.Protagonist.Id || item.Detail is AbilityReleasedEventDetail)
            {
                _vanguardPresentation.NotifyShot(item.Tick);
            }
        }
        RenderObservation(observation, timeAlreadyUpdated: true);
        ProcessCombatPresentationEvents(observation);
        AdvanceCombatPresentationClock();
    }

    private void PlayCombatCue(string cue, Vector3 position)
    {
        if (_reviewMode == "capture" || DisplayServer.GetName() == "headless") { return; }
        var player = new AudioStreamPlayer3D
        {
            Stream = CombatAudio.Get(cue), Position = position, VolumeDb = -7,
            UnitSize = 10, MaxDistance = 35,
        };
        AddChild(player);
        player.Finished += player.QueueFree;
        player.Play();
        _combatPresentationEffects.Add(new TimedPresentationEffect(player, 0.32f, _effectEventTick));
    }

    private void SpawnInterruptCue(Vector3 position)
    {
        var label = new Label3D
        {
            Position = position + Vector3.Up * 1.85f, Text = "INTERRUPTED", FontSize = 32, OutlineSize = 8,
            Modulate = new Color("f2c879"), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, NoDepthTest = true,
        };
        AddChild(label);
        _combatPresentationEffects.Add(new TimedPresentationEffect(label, 0.60f, _effectEventTick));
        PlayCombatCue("interrupt", position);
    }

    public string GetPresentationDiagnosticsJson() => JsonSerializer.Serialize(new
    {
        schema_version = 1,
        tick = _session?.Tick,
        presentation_tick = _presentationTick,
        protagonist_position = new[] { _protagonistView.GlobalPosition.X, _protagonistView.GlobalPosition.Y, _protagonistView.GlobalPosition.Z },
        enforcer_position = new[] { _securityEnforcerView.GlobalPosition.X, _securityEnforcerView.GlobalPosition.Y, _securityEnforcerView.GlobalPosition.Z },
        vanguard = _vanguardPresentation.GetDiagnostics(),
        enforcer = _securityEnforcerPresentation.GetDiagnostics(),
        lintels = _camera.ObserveLintels(),
        projectiles = _combatPresentationEffects.Select(effect => effect.Node)
            .OfType<CarbineProjectile>().Select(projectile => projectile.GetDiagnostics()),
        effects = _combatPresentationEffects.Select(effect => new
        {
            born_tick = effect.BornTick,
            age_seconds = (_presentationTick - effect.BornTick) / GameSession.TicksPerSecond,
            duration_seconds = effect.DurationSeconds,
            delay_seconds = effect.DelaySeconds,
        }),
    }, CaptureManifestJsonOptions);
}
