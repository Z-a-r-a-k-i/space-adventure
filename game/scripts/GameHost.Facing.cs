using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private bool _facingTargeting;
    private Button _faceButton = null!;
    private readonly Dictionary<EntityId, FacingSample> _facingSamples = [];
    private readonly Dictionary<EntityId, (MeshInstance3D Current, MeshInstance3D Order)> _facingArrows = [];

    private void BeginFacingTargeting()
    {
        CancelSelectionGesture();
        if (_faceButton.Disabled) { return; }
        CancelAbilityTargeting();
        _facingTargeting = true;
        SetFeedback("Face direction · click to turn selected crew. Esc cancels.", TacticalUi.Cyan);
    }

    private bool TryFacingDirection(Vector2 screen, StationRouteObservation route, out Vector3 direction)
    {
        direction = Vector3.Zero;
        var actors = SelectedLivingActors(route).ToArray();
        if (actors.Length == 0 || !TryPickFloor(screen, out var point)) { return false; }
        var center = actors.Aggregate(Vector3.Zero, (sum, actor) => sum + ToGodot(actor.Position)) / actors.Length;
        direction = point - center; direction.Y = 0;
        if (direction.LengthSquared() < .04f) { return false; }
        direction = direction.Normalized();
        return true;
    }

    private void ConfirmFacing(Vector2 screen)
    {
        var route = _session!.Observe().StationRoute!;
        if (_faceButton.Disabled || !TryFacingDirection(screen, route, out var direction)) { return; }
        var result = _session.Execute(new FaceActorsCommand(NextHumanCommandId("face"),
            SelectedLivingActors(route).Select(actor => actor.Id), ToCore(direction)));
        if (!result.Accepted) { SetFeedback($"Cannot turn · {result.RejectionCode}", TacticalUi.Danger); return; }
        CancelAbilityTargeting(); SynchronizePresentation();
        SetFeedback(_session.IsPaused ? "Facing queued · turns on resume." : "Holding direction.", TacticalUi.Cyan);
    }

    private Vector3 SampleActorFacing(ActorObservation actor)
    {
        var tick = _session!.Tick;
        var next = Mathf.Atan2((float)actor.Facing.X, (float)actor.Facing.Z);
        if (!_facingSamples.TryGetValue(actor.Id, out var sample) || tick < sample.Tick)
        { sample = new FacingSample(next, next, tick, tick); }
        else if (tick > sample.Tick)
        { sample = new FacingSample(sample.Current, next, sample.Tick, tick); }
        _facingSamples[actor.Id] = sample;
        var fraction = (float)Math.Clamp((_presentationTick - sample.PreviousTick) / Math.Max(1, sample.Tick - sample.PreviousTick), 0, 1);
        var angle = Mathf.LerpAngle(sample.Previous, sample.Current, fraction);
        return new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
    }

    private MeshInstance3D CreateFacingArrow(Color tint)
    {
        var mesh = new SurfaceTool(); mesh.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var point in new[] { new Vector3(0, 0, 1.22f), new Vector3(-.28f, 0, .88f), new Vector3(0, 0, 1.01f),
            new Vector3(0, 0, 1.22f), new Vector3(0, 0, 1.01f), new Vector3(.28f, 0, .88f) })
        { mesh.AddVertex(point); }
        var material = CreateCombatEffectMaterial(tint);
        material.NoDepthTest = true; material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
        var view = new MeshInstance3D { Mesh = mesh.Commit(), MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(view); return view;
    }

    private void SynchronizeFacing(StationRouteObservation route)
    {
        if (_faceButton.Disabled) { _facingTargeting = false; }
        var previewDirection = Vector3.Zero;
        var preview = _facingTargeting && TryFacingDirection(GetViewport().GetMousePosition(), route, out previewDirection);
        foreach (var actor in route.Party)
        {
            if (!_facingArrows.TryGetValue(actor.Id, out var arrows))
            {
                arrows = (CreateFacingArrow(actor.Id == route.Protagonist.Id ? TacticalUi.Cyan : TacticalUi.Amber),
                    CreateFacingArrow(TacticalUi.Amber));
                arrows.Order.Scale = new Vector3(1.35f, 1, 1.35f);
                _facingArrows.Add(actor.Id, arrows);
            }
            arrows.Current.Visible = _selectedActorIds.Contains(actor.Id) && actor.Combat?.IsDefeated != true;
            var facing = SampleActorFacing(actor);
            arrows.Current.GlobalPosition = _actorViews[actor.Id.Value].GlobalPosition + Vector3.Up * .08f;
            arrows.Current.Rotation = new Vector3(0, Mathf.Atan2(facing.X, facing.Z), 0);
            var pending = actor.PendingAction?.Facing;
            arrows.Order.Visible = arrows.Current.Visible && (preview || pending is not null);
            var desired = preview ? previewDirection : pending is { } queued ? ToGodot(queued) : facing;
            arrows.Order.GlobalPosition = arrows.Current.GlobalPosition;
            arrows.Order.Rotation = new Vector3(0, Mathf.Atan2(desired.X, desired.Z), 0);
        }
    }

    private sealed record FacingSample(float Previous, float Current, long PreviousTick, long Tick);
}
