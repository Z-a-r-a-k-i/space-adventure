using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private void ProcessCombatPresentationEvents(GameObservation observation)
    {
        if (_session is null || observation.StationRoute is not StationRouteObservation route)
        {
            return;
        }

        (long Tick, EntityId Source, float FlightSeconds)? releasedProjectile = null;
        (long Tick, EntityId Source, EntityId Target, Vector3 Position)? incomingContact = null;
        foreach (var gameEvent in _session.EventsSince(_presentationEventSequence))
        {
            if (gameEvent.Tick > _presentationTick) { break; }
            _effectEventTick = gameEvent.Tick;
            if (gameEvent.Type is GameplayEventType.EncounterRestarted or GameplayEventType.EncounterStarted)
            {
                foreach (var effect in _combatPresentationEffects)
                {
                    if (GodotObject.IsInstanceValid(effect.Node)) { effect.Node.QueueFree(); }
                }
                _combatPresentationEffects.Clear();
                _incomingBolts.Clear();
                _audioVariants.Clear();
            }
            switch (gameEvent.Detail)
            {
                case AttackEventDetail attack when gameEvent.Type == GameplayEventType.AttackReleased:
                    if (TryGetCombatantPosition(route, attack.TargetId, out var target))
                    {
                        if (ArmedPresentation(attack.SourceId) is { } armed)
                        {
                            armed.NotifyShot(gameEvent.Tick);
                            var shotgun = attack.SourceId == _definition!.Companion.Id;
                            var destination = attack.Hit ? CombatImpactPosition(target, armed.MuzzlePosition)
                                : armed.MuzzlePosition + armed.MuzzleDirection * (float)_definition.Combat.GetAttack(attack.AttackId).RangeMeters;
                            var color = new Color(shotgun ? "f2c879" : "57ddff");
                            var flight = SpawnProjectile(armed.MuzzlePosition, destination, color,
                                armed.MuzzleDirection, shotgun ? "shotgun" : "carbine");
                            if (shotgun)
                            {
                                var right = armed.MuzzleDirection.Cross(Vector3.Up).Normalized();
                                for (var pellet = -2; pellet <= 2; pellet++)
                                {
                                    if (pellet == 0) { continue; }
                                    var bolt = new CarbineProjectile();
                                    bolt.Configure(armed.MuzzlePosition, destination + right * pellet * .085f
                                        + Vector3.Up * (pellet % 2 == 0 ? .07f : -.07f), color);
                                    AddChild(bolt);
                                    _combatPresentationEffects.Add(new TimedPresentationEffect(bolt, bolt.FlightSeconds, _effectEventTick));
                                }
                            }
                            releasedProjectile = (gameEvent.Tick, attack.SourceId, flight);
                        }
                        else if (!attack.Hit && _enemyViews.TryGetValue(attack.SourceId, out var view) && view.Sentry is { } sentry)
                        {
                            var destination = attack.Hit ? CombatImpactPosition(target, sentry.MuzzlePosition)
                                : sentry.MuzzlePosition + sentry.MuzzleDirection * (float)_definition!.Combat.GetAttack(attack.AttackId).RangeMeters;
                            var flight = SpawnProjectile(sentry.MuzzlePosition, destination,
                                new Color("ff7659"), sentry.MuzzleDirection, "sentry");
                            releasedProjectile = (gameEvent.Tick, attack.SourceId, flight);
                        }
                    }
                    break;
                case AbilityReleasedEventDetail ability when ability.SourceId == route.Protagonist.Id:
                    _vanguardPresentation.NotifyShot(gameEvent.Tick);
                    var isBurst = ability.AbilityId == _definition!.Combat.Burst.Id;
                    var abilityDestination = isBurst
                        ? CombatImpactPosition(ToGodot(ability.TargetPosition), _vanguardPresentation.MuzzlePosition)
                        : ToGodot(ability.TargetPosition) + Vector3.Up * .65f;
                    var abilityFlightSeconds = SpawnProjectile(
                        _vanguardPresentation.MuzzlePosition,
                        abilityDestination,
                        new Color("66f5ff"), _vanguardPresentation.MuzzleDirection,
                        isBurst ? "burst" : "interrupt");
                    releasedProjectile = (gameEvent.Tick, ability.SourceId, abilityFlightSeconds);
                    if (ability.AbilityId == _definition!.Combat.ProtagonistAbility.Id)
                    {
                        SpawnSignature(CombatSignature.Interrupt, abilityDestination,
                            abilityDestination.DirectionTo(_vanguardPresentation.MuzzlePosition), abilityFlightSeconds);
                        SpawnSuppressionPulse(ToGodot(ability.TargetPosition) + new Vector3(0, .06f, 0), abilityFlightSeconds);
                    }
                    break;
                case AbilityReleasedEventDetail ability when ability.AbilityId == _definition!.Combat.Taunt.Id:
                    PresentTaunt(ability, gameEvent.Tick);
                    break;
                case DamageAppliedEventDetail damage:
                    if (TryGetCombatantPosition(route, damage.TargetId, out var impact))
                    {
                        var partyHit = route.Party.Any(actor => actor.Id == damage.TargetId);
                        var color = partyHit
                            ? new Color("ff654f")
                            : new Color("75eeff");
                        var impactDelay = releasedProjectile is { } release && release.Tick == gameEvent.Tick
                            && release.Source == damage.SourceId ? release.FlightSeconds : 0;
                        var signature = damage.AbilityId == _definition!.Combat.ProtagonistAbility.Id ? CombatSignature.Interrupt
                            : damage.AbilityId == _definition.Combat.Burst.Id ? CombatSignature.Burst
                            : damage.SourceId == _definition.Companion.Id ? CombatSignature.Shotgun
                            : damage.SourceId == _definition.Protagonist.Id ? CombatSignature.Carbine
                            : _enemyViews.TryGetValue(damage.SourceId, out var enemy) && enemy.Sentry is not null
                                ? CombatSignature.Sentry : CombatSignature.Melee;
                        var contact =
                            incomingContact is { } arrival && arrival.Tick == gameEvent.Tick
                                && arrival.Source == damage.SourceId && arrival.Target == damage.TargetId
                                ? arrival.Position
                            : ArmedPresentation(damage.SourceId) is { } source
                                ? CombatImpactPosition(impact, source.MuzzlePosition)
                                : impact + new Vector3(0.0f, 1.05f, 0.0f);
                        TryGetCombatantPosition(route, damage.SourceId, out var sourcePosition);
                        SpawnSignature(signature, contact, contact.DirectionTo(sourcePosition + Vector3.Up), impactDelay);
                        PlayCombatCue(signature switch
                        {
                            CombatSignature.Shotgun => "shotgun_hit", CombatSignature.Melee => "melee_hit",
                            CombatSignature.Sentry => "sentry_hit", _ => "carbine_hit",
                        }, contact, impactDelay);
                        SpawnDamageNumber(
                            impact + new Vector3(0.0f, 1.48f, 0.0f),
                            damage.Amount,
                            color,
                            impactDelay);
                    }
                    break;
                case ActionInterruptedEventDetail interrupted:
                    if (TryGetCombatantPosition(route, interrupted.ActorId, out var interruptedPosition))
                    {
                        var interruptDelay = releasedProjectile is { } interruptRelease
                            && interruptRelease.Tick == gameEvent.Tick && interruptRelease.Source == interrupted.SourceId
                                ? interruptRelease.FlightSeconds : 0;
                        SpawnInterruptCue(interruptedPosition, interruptDelay);
                    }
                    break;
                case BarrierEventDetail barrier when gameEvent.Type == GameplayEventType.BarrierDeployed:
                    SpawnSignature(CombatSignature.Barrier,
                        ToGodot(barrier.Position) - Vector3.Up * (float)_definition!.Combat.Barrier.CenterHeightMeters + Vector3.Up * .055f,
                        Vector3.Up, radius: .8f);
                    PlayCombatCue("barrier", ToGodot(barrier.Position));
                    break;
                case ProjectileEventDetail projectile:
                    PresentIncomingProjectile(projectile, gameEvent.Tick, gameEvent.Type);
                    if (gameEvent.Type == GameplayEventType.ProjectileImpacted && projectile.ImpactPosition is { } arrivedAt)
                    { incomingContact = (gameEvent.Tick, projectile.SourceId, projectile.TargetId, ToGodot(arrivedAt)); }
                    break;
            }

            _presentationEventSequence = gameEvent.Sequence;
        }
    }

    private void AdvanceCombatPresentationClock()
    {
        if (_stationAmbience is not null) { _stationAmbience.StreamPaused = _session!.IsPaused; }
        for (var index = _combatPresentationEffects.Count - 1; index >= 0; index--)
        {
            var effect = _combatPresentationEffects[index];
            if (!GodotObject.IsInstanceValid(effect.Node))
            {
                _combatPresentationEffects.RemoveAt(index);
                continue;
            }
            var age = (float)((_presentationTick - effect.BornTick) / GameSession.TicksPerSecond) - effect.DelaySeconds;
            effect.Node.Visible = age >= 0;
            effect.RemainingSeconds = effect.DurationSeconds - age;
            var progress = Math.Clamp(1 - effect.RemainingSeconds / effect.DurationSeconds, 0, 1);
            if (effect.Node is CarbineProjectile projectile) { projectile.Sample(progress); }
            if (effect.Node is CombatContactEffect contact) { contact.Sample(progress); }
            if (effect.Node is GeometryInstance3D geometry) { geometry.Transparency = progress * progress; }
            if (effect.Node is Label3D) { effect.Node.Position = effect.Origin + Vector3.Up * progress * 0.32f; }
            if (effect.Node is AudioStreamPlayer3D audio)
            {
                var paused = _session!.IsPaused && !_reviewDrivesClock;
                audio.StreamPaused = paused;
                if (age >= 0 && age < effect.DurationSeconds && !paused && !audio.HasMeta("started"))
                {
                    audio.SetMeta("started", true);
                    audio.Play(age);
                }
            }
            if (effect.RemainingSeconds > 0.0f)
            {
                continue;
            }

            effect.Node.QueueFree();
            _combatPresentationEffects.RemoveAt(index);
        }
    }

    private static bool TryGetCombatantPosition(
        StationRouteObservation route,
        EntityId entityId,
        out Vector3 position)
    {
        var actor = route.Party.SingleOrDefault(candidate => candidate.Id == entityId);
        if (actor is not null)
        {
            position = ToGodot(actor.Position);
            return true;
        }

        var hostile = route.Hostiles?.SingleOrDefault(candidate => candidate.Id == entityId);
        if (hostile is not null)
        {
            position = ToGodot(hostile.Position);
            return true;
        }

        position = Vector3.Zero;
        return false;
    }

    private static Vector3 CombatImpactPosition(Vector3 target, Vector3 source)
    {
        var torso = target + Vector3.Up * 1.05f;
        // A small presentation offset keeps the hit flash outside the torso mesh.
        return torso + torso.DirectionTo(source) * 0.22f;
    }

    private float SpawnProjectile(Vector3 origin, Vector3 destination, Color color, Vector3 muzzleDirection, string cue = "carbine")
    {
        var node = new CarbineProjectile();
        node.Configure(origin, destination, color);
        AddChild(node);
        _combatPresentationEffects.Add(new TimedPresentationEffect(node, node.FlightSeconds, _effectEventTick));
        SpawnMuzzleSignature(origin, muzzleDirection, cue);
        PlayCombatCue(cue, origin);
        return node.FlightSeconds;
    }

    private void SpawnSignature(CombatSignature signature, Vector3 position, Vector3 direction,
        float delaySeconds = 0, float radius = 1)
    {
        var effect = new CombatContactEffect();
        effect.Configure(signature, position, direction, radius); AddChild(effect);
        _combatPresentationEffects.Add(new TimedPresentationEffect(effect,
            effect.DurationSeconds / AnimationPacing.Rate, _effectEventTick, delaySeconds));
    }

    private void SpawnMuzzleSignature(Vector3 origin, Vector3 direction, string cue)
    {
        SpawnSignature(CombatSignature.Muzzle, origin + direction * .055f, direction);
        if (cue is "shotgun" or "burst")
        { SpawnSignature(cue == "shotgun" ? CombatSignature.Shotgun : CombatSignature.Burst,
            origin + direction * .08f, direction); }
    }

    private void SpawnDamageNumber(Vector3 position, int amount, Color color, float delaySeconds = 0)
    {
        var node = new Label3D
        {
            Position = position,
            Text = $"-{amount}",
            FontSize = 38,
            OutlineSize = 8,
            Modulate = color,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            NoDepthTest = true,
        };
        AddChild(node);
        _combatPresentationEffects.Add(new TimedPresentationEffect(node, 0.70f, _effectEventTick, delaySeconds));
    }

    private void SpawnSuppressionPulse(Vector3 position, float delaySeconds = 0)
    {
        SpawnSignature(CombatSignature.Interrupt, position, Vector3.Up, delaySeconds,
            (float)_definition!.Combat.ProtagonistAbility.RadiusMeters);
    }

    private static StandardMaterial3D CreateCombatEffectMaterial(Color color) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
        AlbedoColor = color,
        EmissionEnabled = true,
        Emission = color,
        EmissionEnergyMultiplier = 1.3f,
    };

}
