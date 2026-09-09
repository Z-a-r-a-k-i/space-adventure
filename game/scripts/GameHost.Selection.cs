using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private const float SelectionDragThreshold = 6;
    private Panel _selectionBox = null!;
    private Vector2? _selectionStart;
    private EntityId? _selectionClickActor;
    private bool _selectionAdditive;
    private bool _selectionDragging;

    private void CreateSelectionBox(CanvasLayer canvas)
    {
        _selectionBox = new Panel { Name = "CrewSelectionBox", MouseFilter = Control.MouseFilterEnum.Ignore,
            Visible = false, ZIndex = 5 };
        _selectionBox.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(TacticalUi.Cyan, .10f), BorderColor = TacticalUi.Cyan,
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        });
        canvas.AddChild(_selectionBox);
        GetWindow().FocusExited += CancelSelectionGesture;
        GetWindow().MouseExited += CancelSelectionGesture;
    }

    private void BeginSelectionGesture(InputEventMouseButton click)
    {
        var route = _session!.Observe().StationRoute!;
        if (route.ActiveDialogue is not null || route.Party.All(actor => actor.Combat?.IsDefeated == true)) { return; }
        _selectionStart = click.Position;
        _selectionClickActor = PickCrew(click.Position);
        _selectionAdditive = click.ShiftPressed;
        _selectionDragging = false;
        _camera.EdgePanBlocked = true;
    }

    // Receive the end of a world drag before GUI controls can swallow its release.
    public override void _Input(InputEvent @event)
    {
        if (HandleControlsInput(@event)) { return; }
        if (_selectionStart is null) { return; }
        switch (@event)
        {
            case InputEventMouseMotion motion:
                UpdateSelectionGesture(motion.Position);
                break;
            case InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: false } release:
                FinishSelectionGesture(release.Position);
                break;
            case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }:
            case InputEventKey { Pressed: true, Keycode: Key.Escape }:
                CancelSelectionGesture();
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    private void UpdateSelectionGesture(Vector2 pointer)
    {
        if (_selectionStart is not { } start) { return; }
        _selectionDragging |= pointer.DistanceTo(start) >= SelectionDragThreshold;
        if (!_selectionDragging) { return; }
        var rectangle = new Rect2(start, pointer - start).Abs().Intersection(GetViewport().GetVisibleRect());
        _selectionBox.Position = rectangle.Position;
        _selectionBox.Size = rectangle.Size;
        _selectionBox.Visible = true;
    }

    private void FinishSelectionGesture(Vector2 pointer)
    {
        UpdateSelectionGesture(pointer);
        var clicked = _selectionClickActor;
        var additive = _selectionAdditive;
        var dragging = _selectionDragging;
        var rectangle = _selectionBox.GetGlobalRect();
        CancelSelectionGesture();
        if (!dragging)
        {
            if (clicked is { } id) { SelectActor(id, additive); }
            return;
        }
        var route = _session!.Observe().StationRoute!;
        var members = route.Party.Where(actor => actor.Combat?.IsDefeated != true
            && CrewIntersectsSelection(actor, rectangle)).ToArray();
        if (members.Length == 0) { SetFeedback("No crew in selection.", TacticalUi.Muted); return; }
        CancelAbilityTargeting();
        if (!additive) { _selectedActorIds.Clear(); }
        foreach (var member in members) { _selectedActorIds.Add(member.Id); }
        if (_focusedActorId is null || !_selectedActorIds.Contains(_focusedActorId.Value)
            || route.Party.Any(actor => actor.Id == _focusedActorId && actor.Combat?.IsDefeated == true))
        { _focusedActorId = members[0].Id; }
        RenderObservation(_session.Observe());
        var count = SelectedLivingActors(route).Count();
        SetFeedback(count > 1 ? $"{count} crew selected · right-click to order the group · Tab switches abilities."
            : $"{members[0].DisplayName} selected.", TacticalUi.Cyan);
    }

    private bool CrewIntersectsSelection(ActorObservation actor, Rect2 rectangle)
    {
        var view = _actorViews[actor.Id.Value];
        var center = view.GlobalPosition + Vector3.Up;
        if (!view.IsVisibleInTree() || _camera.IsPositionBehind(center)) { return false; }
        var feet = _camera.UnprojectPosition(view.GlobalPosition + Vector3.Up * .1f);
        var head = _camera.UnprojectPosition(view.GlobalPosition + Vector3.Up * 1.8f);
        var bounds = new Rect2(feet, Vector2.Zero).Expand(head).Grow(8);
        return rectangle.Intersects(bounds, includeBorders: true);
    }

    private void CancelSelectionGesture()
    {
        _selectionStart = null;
        _selectionClickActor = null;
        _selectionDragging = false;
        if (_selectionBox is not null) { _selectionBox.Visible = false; }
        if (_camera is not null) { _camera.EdgePanBlocked = false; }
    }
}
