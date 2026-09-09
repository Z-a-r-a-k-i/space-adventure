using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private const uint CrewCollisionLayer = 16;
    private readonly Dictionary<EntityId, EnemyView> _enemyViews = [];
    private readonly Dictionary<EntityId, (MeshInstance3D Line, MeshInstance3D Ring)> _attackLinks = [];
    private readonly Dictionary<EntityId, EnemyIntentCue> _enemyIntentCues = [];

    private void CachePartyCombatViews()
    {
        // Every crew/hostile view exists before the session starts. Recruitment and
        // encounter changes only toggle visibility; ValidateCombatViews checks coverage.
        foreach (var root in GetNode<Node3D>("Hostiles").GetChildren().OfType<Node3D>())
        {
            _enemyViews.Add(new EntityId(GetStableId(root)), new EnemyView(root,
                root.GetNode<CollisionObject3D>("TargetBody"), root.GetNode<MeshInstance3D>("ThreatRing"),
                root.GetNodeOrNull<HumanoidPresentation>("Presentation"),
                root.GetNodeOrNull<SentryPresentation>("Presentation")));
            var cue = new EnemyIntentCue(); AddChild(cue);
            cue.Build(root.GetNodeOrNull<SentryPresentation>("Presentation") is not null);
            _enemyIntentCues.Add(new EntityId(GetStableId(root)), cue);
        }
        foreach (var actor in _actorViews.Values)
        {
            var target = new StaticBody3D { Name = "CrewTarget", CollisionLayer = 0, CollisionMask = 0 };
            target.SetMeta("stable_id", GetStableId(actor));
            target.AddChild(new CollisionShape3D
            {
                Position = new Vector3(0, .95f, 0),
                Shape = new CapsuleShape3D { Radius = .42f, Height = 1.9f },
            });
            actor.AddChild(target);
        }
        _protectorPartyPresentation.StrongRecoil = true;
    }

    private void ValidateCombatViews(StationRouteDefinition definition)
    {
        foreach (var id in new[] { definition.Protagonist.Id, definition.Companion.Id })
        {
            if (!_actorViews.ContainsKey(id.Value))
            { throw new InvalidDataException($"The station scene has no crew view for '{id}'."); }
        }
        foreach (var hostile in definition.Combat.Hostiles)
        {
            if (!_enemyViews.ContainsKey(hostile.Id))
            { throw new InvalidDataException($"The station scene has no hostile view for '{hostile.Id}'."); }
        }
    }

    private void AttackWithSelectedCrew(EntityId targetId)
    {
        var actors = SelectedLivingActors(_session!.Observe().StationRoute!).ToArray();
        if (actors.Length == 0)
        { SetFeedback("Select a living crew member first.", TacticalUi.Danger); return; }
        foreach (var actor in actors)
        { Dispatch(new AssignBasicAttackTargetCommand(NextHumanCommandId("attack"), actor.Id, targetId)); }
    }

    private StationEncounterPlacement CreatePartyPlacement(StationRouteDefinition definition)
    {
        var trigger = GetNode<Marker3D>("Markers/PartyEncounterTrigger");
        var enforcer = GetNode<Marker3D>("Markers/MainEnforcerSpawn");
        var sentry = GetNode<Marker3D>("Markers/SentrySpawn");
        ValidateStableId(trigger, definition.Combat.PartyEncounter.Id.Value);
        var markers = new[] { enforcer, sentry }.ToDictionary(marker => new EntityId(GetStableId(marker)));
        if (!markers.Keys.ToHashSet().SetEquals(definition.Combat.PartyEncounter.HostileIds))
        { throw new InvalidDataException("Party hostile markers do not match content."); }
        var hostiles = definition.Combat.PartyEncounter.HostileIds
            .Select(id => new StationActorPlacement(id, ToCore(markers[id].GlobalPosition))).ToArray();
        return new StationEncounterPlacement(definition.Combat.PartyEncounter.Id,
            ToCore(trigger.GlobalPosition), trigger.GetMeta("trigger_radius_meters").AsDouble(),
            ToCore(GetNode<Marker3D>("Markers/PartyVanguardRestart").GlobalPosition),
            hostiles[0].Position,
            ToCore(GetNode<Marker3D>("Markers/PartyProtectorRestart").GlobalPosition),
            hostiles.Skip(1).ToArray(),
            ToCore(-sentry.GlobalBasis.Z.Normalized()));
    }

    private Vector3 ActorFacing(StationRouteObservation route, ActorObservation actor)
    {
        if (actor.CurrentAction?.HasRemainingMovement == true) { return TravelDirection(actor.Id); }
        if (route.Encounter?.Phase is not (EncounterPhase.Readying or EncounterPhase.Active or EncounterPhase.Securing))
        { return Vector3.Zero; }
        if (actor.CurrentAction is { Kind: PrimaryActionKind.Ability } ability
            && ability.AbilityId == _definition!.Combat.ProtagonistAbility.Id)
        { return ToGodot(ability.Destination) - ToGodot(actor.Position); }
        var targetId = actor.CurrentAction?.CombatTargetId ?? actor.Combat?.RememberedAttackTargetId;
        var target = route.Hostiles?.FirstOrDefault(hostile => hostile.Id == targetId)
            ?? route.Hostiles?.Where(hostile => !hostile.Combat.IsDefeated)
                .OrderBy(hostile => hostile.Position.DistanceTo(actor.Position)).FirstOrDefault();
        return target is null ? Vector3.Zero : ToGodot(target.Position) - ToGodot(actor.Position);
    }

    private void SynchronizeCombatPresentation(GameObservation observation, StationRouteObservation route)
    {
        foreach (var (id, view) in _enemyViews)
        {
            var hostile = route.Hostiles?.FirstOrDefault(candidate => candidate.Id == id);
            var active = hostile is not null && route.Encounter?.Phase != EncounterPhase.Dormant;
            view.Root.Visible = active;
            view.Target.CollisionLayer = active && !hostile!.Combat.IsDefeated ? HostileCollisionLayer : 0;
            _enemyIntentCues[id].Visible = false;
            if (!active) { continue; }
            view.Root.GlobalPosition = SamplePosition(id, hostile!.Position, observation.Tick, route.Encounter!.Attempt);
            var action = hostile.CurrentAction;
            var target = route.Party.FirstOrDefault(actor => actor.Id == action?.CombatTargetId)
                ?? route.Party.Where(actor => actor.Combat?.IsDefeated == false)
                    .OrderBy(actor => actor.Position.DistanceTo(hostile.Position)).FirstOrDefault();
            var targetPosition = target is null ? view.Root.GlobalPosition + Vector3.Forward : ToGodot(target.Position);
            var direction = action?.HasRemainingMovement == true ? TravelDirection(id) : targetPosition - view.Root.GlobalPosition;
            var pose = hostile.Combat.IsDefeated ? HumanoidPresentationAction.Down
                : action is { Kind: PrimaryActionKind.Attack, Phase: PrimaryActionPhase.Windup or PrimaryActionPhase.Recovery, Interrupted: false }
                    ? HumanoidPresentationAction.MeleeStrike
                    : action?.HasRemainingMovement == true ? HumanoidPresentationAction.Locomotion : HumanoidPresentationAction.Idle;
            view.Humanoid?.Synchronize(true, pose, observation.Paused, direction,
                presentationTick: _presentationTick, clipSeconds: EnforcerClipSeconds(action),
                cycle: action?.InstanceId ?? route.Encounter.Attempt, turnDeltaSeconds: _presentationDeltaSeconds,
                snapToPose: route.Encounter.Phase == EncounterPhase.Readying);
            if (!hostile.Combat.IsDefeated && pose == HumanoidPresentationAction.MeleeStrike)
            { view.Humanoid?.FaceDirection(direction, _presentationDeltaSeconds); }
            view.Sentry?.Synchronize(hostile, targetPosition + Vector3.Up * 1.1f, _presentationTick, _presentationDeltaSeconds);
            view.Threat.Visible = false;
            var winding = !hostile.Combat.IsDefeated && target?.Combat?.IsDefeated == false
                && action is { Phase: PrimaryActionPhase.Windup, Interrupted: false }
                && route.Encounter.Phase == EncounterPhase.Active;
            _enemyIntentCues[id].Sample(winding, view.Root.GlobalPosition,
                target is null ? targetPosition : _actorViews[target.Id.Value].GlobalPosition,
                action is null ? 0 : (float)Math.Clamp((_presentationTick - action.PhaseStartedTick) / Math.Max(1, action.PhaseTicksTotal), 0, 1));
            if (view.Root.GetNodeOrNull<Label3D>("Label") is Label3D label) { label.Visible = false; }
        }
        SynchronizeBarrier(route);
        SynchronizeAttackLinks(route);
    }

    private void SynchronizeAttackLinks(StationRouteObservation route)
    {
        foreach (var actor in route.Party)
        {
            if (!_attackLinks.TryGetValue(actor.Id, out var link))
            {
                var color = actor.Id == route.Protagonist.Id ? new Color("65d7ef") : new Color("efc47d");
                var material = CreateCombatEffectMaterial(new Color(color, .5f));
                material.NoDepthTest = true;
                material.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
                var line = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = .018f, BottomRadius = .018f,
                    Height = 1, RadialSegments = 5 }, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                var radius = actor.Id == route.Protagonist.Id ? .62f : .76f;
                var ring = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = radius, OuterRadius = radius + .035f,
                    Rings = 32, RingSegments = 6 }, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                AddChild(line); AddChild(ring); link = (line, ring); _attackLinks.Add(actor.Id, link);
            }
            var targetId = actor.CurrentAction?.CombatTargetId ?? actor.Combat?.RememberedAttackTargetId;
            var target = route.Hostiles?.FirstOrDefault(hostile => hostile.Id == targetId && !hostile.Combat.IsDefeated);
            link.Line.Visible = link.Ring.Visible = target is not null && actor.Combat?.IsDefeated != true
                && (actor.CurrentAction?.Kind == PrimaryActionKind.Attack || actor.CurrentAction?.AbilityId == _definition!.Combat.Burst.Id)
                && route.Encounter?.Phase == EncounterPhase.Active;
            if (!link.Line.Visible) { continue; }
            var from = _actorViews[actor.Id.Value].GlobalPosition + Vector3.Up * .12f;
            var to = _enemyViews[target!.Id].Root.GlobalPosition + Vector3.Up * .12f;
            var direction = to - from;
            if (direction.LengthSquared() < .01f) { link.Line.Visible = false; continue; }
            link.Line.Transform = new Transform3D(new Basis(new Quaternion(Vector3.Up, direction.Normalized()))
                * Basis.FromScale(new Vector3(1, direction.Length(), 1)), (from + to) / 2);
            link.Ring.GlobalPosition = to;
        }
    }

    private ArmedHumanoidPresentation? ArmedPresentation(EntityId id) => id == _definition!.Protagonist.Id
        ? _vanguardPresentation : id == _definition.Companion.Id ? _protectorPartyPresentation : null;

    private sealed record EnemyView(Node3D Root, CollisionObject3D Target, MeshInstance3D Threat,
        HumanoidPresentation? Humanoid, SentryPresentation? Sentry);
}
