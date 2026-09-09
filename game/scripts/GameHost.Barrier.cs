using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private BarrierPresentation _barrierView = null!;
    private BarrierPresentation _barrierPreview = null!;
    private BarrierPresentation _barrierQueued = null!;
    private AbilityId _targetAbilityId;
    private AbilityTargetKind _targetAbilityKind;
    private readonly Dictionary<long, CarbineProjectile> _incomingBolts = [];

    private void CreateBarrierViews()
    {
        var definition = _definition!.Combat.Barrier;
        _barrierView = new BarrierPresentation { Name = "GroundBarrier" };
        _barrierPreview = new BarrierPresentation { Name = "BarrierPreview" };
        _barrierQueued = new BarrierPresentation { Name = "QueuedBarrier" };
        foreach (var view in new[] { _barrierView, _barrierPreview, _barrierQueued })
        { AddChild(view); view.Build((float)definition.WidthMeters, (float)definition.HeightMeters, definition.WindupTicks); }
    }

    private void ShowGroundShield(BarrierPresentation view, Vector3 groundPosition, Vector3 facing, long deployedAt,
        bool preview = false, bool valid = true, bool queued = false)
    {
        var center = ToGodot(_definition!.Combat.Barrier.CenterAt(ToCore(groundPosition)));
        view.ShowShield(center, facing, groundPosition, _presentationTick, deployedAt, preview, valid, queued);
    }

    private void SynchronizeBarrier(StationRouteObservation route)
    {
        var protector = route.Party.FirstOrDefault(actor => actor.Id == _definition!.Companion.Id);
        _barrierView.Visible = route.Encounter?.Barrier is not null;
        if (route.Encounter?.Barrier is { } barrier)
        { _barrierView.ShowShield(ToGodot(barrier.Position), ToGodot(barrier.Facing),
            ToGodot(barrier.Position) - Vector3.Up * (float)_definition!.Combat.Barrier.CenterHeightMeters,
            _presentationTick, barrier.DeployedAtTick - _definition.Combat.Barrier.WindupTicks); }
        var queued = protector?.PendingAction?.AbilityFacing is not null ? protector.PendingAction
            : protector?.CurrentAction is { Phase: PrimaryActionPhase.Windup, AbilityFacing: not null } ? protector.CurrentAction : null;
        _barrierQueued.Visible = queued is not null && !_abilityTargeting;
        if (_barrierQueued.Visible)
        { ShowGroundShield(_barrierQueued, ToGodot(queued!.Destination), ToGodot(queued.AbilityFacing!.Value),
            queued.PhaseStartedTick, queued: protector!.PendingAction is not null); }
        if (route.Encounter?.Phase is not (EncounterPhase.Active or EncounterPhase.Readying))
        { if (_abilityTargeting) { CancelAbilityTargeting(); } ClearIncomingBolts(); }
    }

    private void CancelAbilityTargeting()
    {
        _abilityTargeting = false; _abilityTargetPreview.Visible = false;
        if (_barrierPreview is not null) { _barrierPreview.Visible = false; }
        HideAbilityContext();
    }

    private void BeginAbilityTargeting(int slot = 0)
    {
        CancelSelectionGesture();
        var actor = FocusedActor(_session!.Observe().StationRoute!);
        if ((slot == 0 ? _abilityButton : _secondaryAbilityButton).Disabled || actor.Loadout is null)
        { SetFeedback("This ability is not ready.", TacticalUi.Danger); return; }
        CancelAbilityTargeting(); _abilityOwnerId = actor.Id;
        _targetAbilityId = slot == 0 ? actor.Loadout.ActiveAbilityId : actor.Loadout.SecondaryAbilityId;
        _targetAbilityKind = slot == 0 ? actor.Loadout.ActiveAbilityTargetKind : actor.Loadout.SecondaryAbilityTargetKind;
        if (_targetAbilityKind == AbilityTargetKind.Self)
        { Dispatch(new UseAbilityCommand(NextHumanCommandId("taunt"), actor.Id, _targetAbilityId, new SelfAbilityTarget())); return; }
        _abilityTargeting = true;
        SetFeedback(_targetAbilityKind switch
        {
            AbilityTargetKind.Barrier => "Barrier · click to place. Faces from Protector toward the pointer. Esc cancels.",
            AbilityTargetKind.Entity => "Burst · choose an enemy for three rapid shots. Esc cancels.",
            _ => "Interrupt · click the floor to interrupt enemies in the circle. Esc cancels.",
        }, TacticalUi.Cyan);
    }

    private bool TryPickFloor(Vector2 screen, out Vector3 point)
    {
        var origin = _camera.ProjectRayOrigin(screen);
        var hit = CastRay(origin, origin + _camera.ProjectRayNormal(screen) * 200, FloorCollisionLayer);
        point = hit.Count == 0 ? default : WithGroundHeight(hit["position"].AsVector3());
        return hit.Count > 0;
    }

    private static BarrierAbilityTarget BarrierTargetAt(ActorObservation actor, Vector3 point)
    {
        var facing = point - ToGodot(actor.Position); facing.Y = 0;
        return new BarrierAbilityTarget(ToCore(point), facing.LengthSquared() > .01f
            ? ToCore(facing.Normalized()) : actor.Facing);
    }

    private EntityId? PickSkillEnemy(Vector2 screen)
    {
        var origin = _camera.ProjectRayOrigin(screen);
        var hit = CastRay(origin, origin + _camera.ProjectRayNormal(screen) * 200, HostileCollisionLayer);
        return hit.Count > 0 && hit["collider"].AsGodotObject() is Node node && node.HasMeta("stable_id")
            ? new EntityId(node.GetMeta("stable_id").AsString()) : null;
    }

    private void ConfirmEnemyAbility(EntityId enemy)
    {
        var result = _session!.Execute(new UseAbilityCommand(NextHumanCommandId("burst"), _abilityOwnerId!.Value,
            _targetAbilityId, new EntityAbilityTarget(enemy)));
        if (!result.Accepted) { SetFeedback($"Burst unavailable · {result.RejectionCode}", TacticalUi.Danger); return; }
        CancelAbilityTargeting(); SynchronizePresentation();
        SetFeedback(_session.IsPaused ? "Burst queued · fires on resume." : "Burst fire.", TacticalUi.Cyan);
    }

    private void ConfirmAbilityTarget(Vector2 screenPosition)
    {
        var route = _session!.Observe().StationRoute!;
        var actor = route.Party.FirstOrDefault(candidate => candidate.Id == _abilityOwnerId);
        if (actor?.Loadout is null || actor.Combat?.IsDefeated == true) { CancelAbilityTargeting(); return; }
        if (_targetAbilityKind == AbilityTargetKind.Entity)
        {
            if (PickSkillEnemy(screenPosition) is { } enemy) { ConfirmEnemyAbility(enemy); }
            else { SetFeedback("Choose an enemy.", TacticalUi.Danger); }
            return;
        }
        if (!TryPickFloor(screenPosition, out var point)) { return; }
        AbilityTarget target = _targetAbilityKind == AbilityTargetKind.Barrier
            ? BarrierTargetAt(actor, point) : new PositionAbilityTarget(ToCore(point));
        var acknowledgement = _session.Execute(new UseAbilityCommand(NextHumanCommandId("skill"), actor.Id, _targetAbilityId, target));
        if (!acknowledgement.Accepted)
        {
            var rejection = acknowledgement.RejectionCode!.Value;
            if (_targetAbilityKind == AbilityTargetKind.Barrier) { ShowBarrierRejection(rejection); }
            else { SetFeedback(rejection == CommandRejectionCode.AbilityTargetOutOfRange
                ? "Outside ability range." : "Ability is not ready.", TacticalUi.Danger); }
            return;
        }
        CancelAbilityTargeting(); SynchronizePresentation();
        SetFeedback(_session.IsPaused ? "Ability queued · activates on resume." : "Ability activated.", TacticalUi.Cyan);
    }

    private void UpdateAbilityTargetPreview(GameObservation observation)
    {
        _abilityTargetPreview.Visible = false; _barrierPreview.Visible = false;
        HideAbilityContext();
        if (observation.StationRoute is not { } route) { return; }
        if (!_abilityTargeting) { ShowInspectedAbility(observation, route); return; }
        var actor = route.Party.FirstOrDefault(candidate => candidate.Id == _abilityOwnerId);
        if (actor?.Combat?.IsDefeated != false || route.Encounter?.Phase is not (EncounterPhase.Readying or EncounterPhase.Active))
        { CancelAbilityTargeting(); return; }
        if (route.ActiveDialogue is not null || _controlsOverlay.Visible || _completionOverlay.Visible) { return; }
        var pointer = GetViewport().GetMousePosition();
        if (FieldHudBounds().Any(rect => rect.HasPoint(pointer))) { return; }
        var name = _targetAbilityKind == AbilityTargetKind.Barrier ? "BARRIER" : _targetAbilityKind == AbilityTargetKind.Entity ? "BURST" : "INTERRUPT";
        var title = $"{CrewNumber(route, actor.Id):00} {actor.DisplayName.ToUpperInvariant()} · {name}";
        var timing = AbilityResumeText(observation, route, actor, _targetAbilityKind == AbilityTargetKind.Barrier);
        if (_targetAbilityKind == AbilityTargetKind.Entity)
        {
            var hostile = route.Hostiles?.FirstOrDefault(enemy => enemy.Id == PickSkillEnemy(pointer) && !enemy.Combat.IsDefeated);
            if (hostile is null)
            { ShowAbilityContext(title, "Choose a living enemy.", "Left-click enemy · Esc / RMB cancels", TacticalUi.Amber, atPointer: true); return; }
            var range = _definition!.Combat.Burst.RangeMeters;
            var distance = hostile.Position.DistanceTo(actor.Position);
            var valid = distance <= range;
            ShowAbilityContext(title, valid
                ? $"{hostile.DisplayName}: {_definition.Combat.Burst.ShotCount} shots × {_definition.Combat.Burst.DamagePerShot} damage."
                : $"OUT OF RANGE · {distance:0.0}m / {range:0.#}m", valid ? timing : "Choose a closer enemy · Esc cancels",
                valid ? CrewAccent(route, actor) : TacticalUi.Danger, atPointer: true);
            if (valid) { ShowAffectedTargets(route, [hostile.Id]); }
            return;
        }
        if (!TryPickFloor(pointer, out var point))
        { ShowAbilityContext(title, "NO FLOOR · aim on the station walkway.", "Esc / RMB cancels", TacticalUi.Danger, atPointer: true); return; }
        if (_targetAbilityKind == AbilityTargetKind.Barrier)
        {
            var target = BarrierTargetAt(actor, point);
            var rejection = _session!.CheckBarrierPlacement(actor.Id, target);
            ShowGroundShield(_barrierPreview, point, ToGodot(target.Facing), 0, preview: true, valid: rejection is null);
            ShowAbilityContext(title, rejection is { } reason ? BarrierRejectionText(reason)
                : $"VALID · {_definition!.Combat.Barrier.WidthMeters:0.#}m shield, {_definition.Combat.Barrier.DurationTicks / 30.0:0.#}s. Protects the marked rear side.",
                rejection is null ? timing + " One click fixes position and facing." : "Choose clear floor · Esc cancels",
                rejection is null ? CrewAccent(route, actor) : TacticalUi.Danger, atPointer: true);
            return;
        }
        var ability = _definition!.Combat.ProtagonistAbility;
        var inRange = ToCore(point).DistanceTo(actor.Position) <= ability.RangeMeters;
        var affected = route.Hostiles!.Where(enemy => !enemy.Combat.IsDefeated && enemy.Position.DistanceTo(ToCore(point)) <= ability.RadiusMeters).ToArray();
        var windups = affected.Count(enemy => enemy.CurrentAction?.Phase == PrimaryActionPhase.Windup);
        ShowAbilityContext(title, !inRange ? $"OUT OF RANGE · aim within {ability.RangeMeters:0.#}m."
            : affected.Length == 0 ? $"EMPTY AREA · no enemies in {ability.RadiusMeters:0.#}m radius."
            : $"{affected.Length} {(affected.Length == 1 ? "enemy" : "enemies")} · {ability.Damage} damage each. {windups} active wind-up{(windups == 1 ? "" : "s")} interrupted.",
            inRange ? timing + " Targets are checked at release." : "Choose a closer position · Esc cancels",
            !inRange ? TacticalUi.Danger : affected.Length == 0 ? TacticalUi.Amber : CrewAccent(route, actor), atPointer: true);
        _abilityTargetPreview.Scale = new Vector3((float)ability.RadiusMeters / 2, 1, (float)ability.RadiusMeters / 2);
        _abilityTargetPreview.GlobalPosition = point + Vector3.Up * .035f;
        _abilityTargetPreview.Visible = inRange;
        if (inRange) { ShowAffectedTargets(route, affected.Select(enemy => enemy.Id)); }
    }

    private static string BarrierRejectionText(CommandRejectionCode rejection) => rejection switch
    {
        CommandRejectionCode.AbilityTargetOutOfRange => "OUT OF RANGE · choose a closer position.",
        CommandRejectionCode.InvalidAbilityTarget => "BLOCKED · place the whole barrier on clear floor.",
        _ => "UNAVAILABLE · ability is not ready.",
    };

    private void ShowBarrierRejection(CommandRejectionCode rejection) => SetFeedback(BarrierRejectionText(rejection), TacticalUi.Danger);

    private void PresentIncomingProjectile(ProjectileEventDetail projectile, long tick, GameplayEventType type)
    {
        if (type == GameplayEventType.ProjectileLaunched)
        {
            var sentry = _enemyViews[projectile.SourceId].Sentry!;
            var node = new CarbineProjectile();
            node.Configure(sentry.MuzzlePosition, ToGodot(projectile.Destination), new Color("ff7659"));
            AddChild(node); _incomingBolts.Add(projectile.Id, node);
            _combatPresentationEffects.Add(new TimedPresentationEffect(node, (float)projectile.FlightTicks / GameSession.TicksPerSecond, tick));
            SpawnImpact(sentry.MuzzlePosition, new Color("ffe2ad")); PlayCombatCue("sentry", sentry.MuzzlePosition);
            return;
        }
        if (_incomingBolts.Remove(projectile.Id, out var bolt))
        { _combatPresentationEffects.RemoveAll(effect => effect.Node == bolt); if (GodotObject.IsInstanceValid(bolt)) { bolt.QueueFree(); } }
        if (type != GameplayEventType.ProjectileBlocked || projectile.ImpactPosition is not { } impact) { return; }
        _barrierView.NotifyBlocked(tick);
        SpawnImpact(ToGodot(impact), new Color("8afff0"));
        var label = new Label3D { Text = "BLOCKED", Position = ToGodot(impact) + Vector3.Up * .3f, FontSize = 28,
            OutlineSize = 7, Modulate = new Color("8afff0"), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled };
        AddChild(label); _combatPresentationEffects.Add(new TimedPresentationEffect(label, .5f, tick));
        PlayCombatCue("guard", ToGodot(impact));
    }

    private void ClearIncomingBolts()
    {
        foreach (var bolt in _incomingBolts.Values)
        { _combatPresentationEffects.RemoveAll(effect => effect.Node == bolt); if (GodotObject.IsInstanceValid(bolt)) { bolt.QueueFree(); } }
        _incomingBolts.Clear();
    }
}
