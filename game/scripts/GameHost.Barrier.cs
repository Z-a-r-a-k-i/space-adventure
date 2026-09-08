using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private BarrierPresentation _barrierView = null!;
    private BarrierPresentation _barrierPreview = null!;
    private BarrierPresentation _barrierQueued = null!;
    private Vector3 _barrierFacing = Vector3.Forward;
    private Vector3? _barrierGroundPosition;
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
        bool preview = false, bool valid = true, bool queued = false, bool aiming = false)
    {
        var center = ToGodot(_definition!.Combat.Barrier.CenterAt(ToCore(groundPosition)));
        view.ShowShield(center, facing, groundPosition, _presentationTick, deployedAt, preview, valid, queued, aiming);
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
        _facingTargeting = false;
        _barrierGroundPosition = null;
        _abilityTargeting = false; _abilityTargetPreview.Visible = false;
        if (_barrierPreview is not null) { _barrierPreview.Visible = false; }
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
            AbilityTargetKind.Barrier => "Barrier · click a ground position, then aim its facing. Esc cancels.",
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
            else { SetFeedback("Choose an enemy or its threat card.", TacticalUi.Danger); }
            return;
        }
        if (!TryPickFloor(screenPosition, out var point)) { return; }
        AbilityTarget target;
        if (_targetAbilityKind == AbilityTargetKind.Barrier)
        {
            if (_barrierGroundPosition is null)
            {
                var suggested = point - ToGodot(actor.Position); suggested.Y = 0;
                if (suggested.LengthSquared() > .01f) { _barrierFacing = suggested.Normalized(); }
                var rejection = _session.CheckBarrierPlacement(actor.Id, new BarrierAbilityTarget(ToCore(point), ToCore(_barrierFacing)));
                if (rejection is not null) { ShowBarrierRejection(rejection.Value); return; }
                _barrierGroundPosition = point;
                SetFeedback("Barrier · face incoming fire, then click to deploy. Esc cancels.", TacticalUi.Cyan);
                return;
            }
            var facing = point - _barrierGroundPosition.Value; facing.Y = 0;
            if (facing.LengthSquared() < .01f) { SetFeedback("Aim away from the barrier's base.", TacticalUi.Danger); return; }
            target = new BarrierAbilityTarget(ToCore(_barrierGroundPosition.Value), ToCore(facing.Normalized()));
        }
        else { target = new PositionAbilityTarget(ToCore(point)); }
        var acknowledgement = _session.Execute(new UseAbilityCommand(NextHumanCommandId("skill"), actor.Id, _targetAbilityId, target));
        if (!acknowledgement.Accepted)
        { ShowBarrierRejection(acknowledgement.RejectionCode!.Value); return; }
        CancelAbilityTargeting(); SynchronizePresentation();
        SetFeedback(_session.IsPaused ? "Ability queued · deploys on resume." : "Ability activated.", TacticalUi.Cyan);
    }

    private void UpdateAbilityTargetPreview(GameObservation observation)
    {
        _abilityTargetPreview.Visible = false; _barrierPreview.Visible = false;
        if (!_abilityTargeting || observation.StationRoute is not { } route) { return; }
        var actor = route.Party.FirstOrDefault(candidate => candidate.Id == _abilityOwnerId);
        if (actor?.Combat?.IsDefeated != false || route.Encounter?.Phase is not (EncounterPhase.Readying or EncounterPhase.Active))
        { CancelAbilityTargeting(); return; }
        if (!TryPickFloor(GetViewport().GetMousePosition(), out var point)) { return; }
        if (_targetAbilityKind == AbilityTargetKind.Barrier)
        {
            var ground = _barrierGroundPosition ?? point;
            var facing = point - (_barrierGroundPosition ?? ToGodot(actor.Position)); facing.Y = 0;
            if (facing.LengthSquared() > .01f) { _barrierFacing = facing.Normalized(); }
            var rejection = _session!.CheckBarrierPlacement(actor.Id, new BarrierAbilityTarget(ToCore(ground), ToCore(_barrierFacing)));
            ShowGroundShield(_barrierPreview, ground, _barrierFacing, 0, preview: true, valid: rejection is null, aiming: _barrierGroundPosition is not null);
            return;
        }
        _abilityTargetPreview.Scale = Vector3.One;
        if (_targetAbilityKind == AbilityTargetKind.Entity)
        {
            var hostile = route.Hostiles?.FirstOrDefault(enemy => enemy.Id == PickSkillEnemy(GetViewport().GetMousePosition()));
            if (hostile is null || hostile.Position.DistanceTo(actor.Position) > _definition!.Combat.Burst.RangeMeters) { return; }
            point = ToGodot(hostile.Position); _abilityTargetPreview.Scale = new Vector3(.3f, 1, .3f);
        }
        _abilityTargetPreview.GlobalPosition = point + Vector3.Up * .035f;
        _abilityTargetPreview.Visible = ToCore(point).DistanceTo(actor.Position) <= _definition!.Combat.ProtagonistAbility.RangeMeters;
    }

    private void ShowBarrierRejection(CommandRejectionCode rejection) => SetFeedback(rejection switch
    {
        CommandRejectionCode.AbilityTargetOutOfRange => "Outside deployment range.",
        CommandRejectionCode.InvalidAbilityTarget => "Place the whole barrier on clear floor.",
        _ => "Ability is not ready.",
    }, TacticalUi.Danger);

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
