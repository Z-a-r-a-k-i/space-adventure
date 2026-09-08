using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private async Task InputPointerMotion(Vector2 position, MouseButtonMask buttons = 0)
    {
        var window = GetViewport().GetFinalTransform() * position;
        Input.ParseInputEvent(new InputEventMouseMotion { Position = window, GlobalPosition = window, ButtonMask = buttons });
        await InputFrame();
    }

    private async Task InputDragStart(Vector2 start, Vector2 end, bool additive = false)
    {
        await InputPointerMotion(start);
        var window = GetViewport().GetFinalTransform() * start;
        Input.ParseInputEvent(new InputEventMouseButton { Position = window, GlobalPosition = window,
            ButtonIndex = MouseButton.Left, Pressed = true, ShiftPressed = additive });
        await InputPointerMotion(end, MouseButtonMask.Left);
    }

    private async Task InputDragEnd(Vector2 end)
    {
        var window = GetViewport().GetFinalTransform() * end;
        Input.ParseInputEvent(new InputEventMouseButton { Position = window, GlobalPosition = window,
            ButtonIndex = MouseButton.Left, Pressed = false });
        await InputFrame();
    }

    private async Task CheckEdgeCameraInput()
    {
        var focus = _camera.FocusPoint;
        var tick = _session!.Tick;
        var size = GetViewport().GetVisibleRect().Size;
        var (forward, right) = _camera.GetPanBasis();
        foreach (var (point, direction) in new (Vector2, Vector3)[]
        { (new(size.X / 2, 2), forward), (new(size.X / 2, size.Y - 2), -forward),
            (new(2, size.Y / 2), -right), (new(size.X - 2, size.Y / 2), right) })
        {
            var before = _camera.FocusPoint;
            await InputPointerMotion(point);
            for (var frame = 0; frame < 6; frame++) { await InputFrame(); }
            InputCheck($"window edge {point} pans while paused (focus={GetWindow().HasFocus()}, mouse={GetViewport().GetMousePosition()}, edge={_camera.EdgePanDirection}, delta={_camera.FocusPoint - before}, blocked={_camera.EdgePanBlocked}, hover={GetViewport().GuiGetHoveredControl()?.Name})", _session.Tick == tick
                && (_camera.FocusPoint - before).Dot(direction) > .15f);
        }
        foreach (var point in new[] { size / 2, new Vector2(-10, size.Y / 2), new Vector2(21, 40) })
        {
            await InputPointerMotion(point);
            var before = _camera.FocusPoint;
            for (var frame = 0; frame < 4; frame++) { await InputFrame(); }
            InputCheck("interior, outside-window, and HUD positions stop edge panning", _camera.EdgePanDirection.IsZeroApprox()
                && _camera.FocusPoint.DistanceTo(before) < .001f);
        }
        await InputPointerMotion(new Vector2(2, size.Y / 2));
        GetWindow().EmitSignal(Window.SignalName.FocusExited);
        var stopped = _camera.FocusPoint;
        for (var frame = 0; frame < 4; frame++) { await InputFrame(); }
        InputCheck("focus-exit clears ongoing edge scrolling", _camera.EdgePanDirection.IsZeroApprox()
            && _camera.FocusPoint.DistanceTo(stopped) < .001f);
        await InputPointerMotion(size / 2);
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.F, Pressed = true });
        await InputFrame();
        Input.ParseInputEvent(new InputEventKey { Keycode = Key.F, Pressed = false });
        await InputFrame();
        InputCheck("focus shortcut accepts remote key events without a physical scan code",
            _camera.FocusPoint.DistanceTo(new Vector3(_camera.FollowTarget.X, 0, _camera.FollowTarget.Z)) < .001f);
        _camera.FocusPoint = focus;
        await InputFrame();
    }

    private async Task CheckDragSelectionAndAbilityFocus()
    {
        var route = ReviewState();
        var vanguard = route.Party[0].Id;
        var protector = route.Party[1].Id;
        var first = _camera.UnprojectPosition(_actorViews[vanguard.Value].GlobalPosition + Vector3.Up);
        var second = _camera.UnprojectPosition(_actorViews[protector.Value].GlobalPosition + Vector3.Up);
        var bounds = new Rect2(first, Vector2.Zero).Expand(second).Grow(24);
        await InputClick(_partyButtons[vanguard.Value].GetGlobalRect().GetCenter(), MouseButton.Left);
        var commands = _humanCommandSequence;
        await InputDragStart(bounds.End, bounds.Position);
        InputCheck("reverse drag shows a box and preserves selection until release", _selectionBox.Visible
            && _selectedActorIds.SetEquals([vanguard]) && _humanCommandSequence == commands);
        await InputDragEnd(bounds.Position);
        InputCheck("lasso selects both living crew without issuing an order", !_selectionBox.Visible
            && _selectedActorIds.SetEquals([vanguard, protector]) && _humanCommandSequence == commands);
        await InputKey(Key.Tab);
        InputCheck("Tab changes ability focus and preserves the group", _focusedActorId == protector
            && _selectedActorIds.SetEquals([vanguard, protector]));
        await InputKey(Key.Key1);
        InputCheck("focused Protector owns Barrier targeting", _abilityTargeting && _abilityOwnerId == protector);
        await InputKey(Key.Tab);
        InputCheck("cycling ability focus cancels the old preview without losing the group", !_abilityTargeting
            && _focusedActorId == vanguard && _selectedActorIds.SetEquals([vanguard, protector]));
        var protectorOrder = ReviewState().Party[1].PendingAction;
        await InputKey(Key.Key2);
        await InputClick(_threatRows[new EntityId("actor.enemy.gun_sentry.main")].Button.GetGlobalRect().GetCenter(), MouseButton.Left);
        InputCheck("a grouped ability goes only to the focused hero", ReviewState().Protagonist.PendingAction?.AbilityId == _definition!.Combat.Burst.Id
            && ReviewState().Party[1].PendingAction == protectorOrder);
        await InputKey(Key.Tab, shift: true);
        InputCheck("Shift Tab cycles the group's ability focus backwards", _focusedActorId == protector
            && _selectedActorIds.SetEquals([vanguard, protector]));

        var small = new Vector2(5, 5);
        await InputDragStart(first - small, first + small);
        await InputDragEnd(first + small);
        InputCheck("a new lasso replaces the group with its enclosed hero", _selectedActorIds.SetEquals([vanguard]));
        await InputDragStart(second - small, second + small, additive: true);
        await InputDragEnd(second + small);
        InputCheck("Shift drag adds enclosed crew to the selection", _selectedActorIds.SetEquals([vanguard, protector]));

        var edge = new Vector2(2, GetViewport().GetVisibleRect().Size.Y / 2);
        var camera = _camera.FocusPoint;
        await InputDragStart(bounds.Position, edge);
        for (var frame = 0; frame < 6; frame++) { await InputFrame(); }
        InputCheck("a lasso reaching the border holds the camera still", _selectionBox.Visible
            && _camera.EdgePanDirection.IsZeroApprox() && _camera.FocusPoint.DistanceTo(camera) < .001f);
        await InputKey(Key.Escape);
        await InputDragEnd(edge);
        InputCheck("Escape cancels a lasso without changing the group", !_selectionBox.Visible
            && _selectionStart is null && _selectedActorIds.SetEquals([vanguard, protector]));
        await InputDragStart(_partyButtons[vanguard.Value].GetGlobalRect().GetCenter(), first);
        InputCheck("dragging from a portrait cannot start a world selection", _selectionStart is null && !_selectionBox.Visible);
        await InputDragEnd(first);
        await InputPointerMotion(GetViewport().GetVisibleRect().Size / 2);
    }
}
