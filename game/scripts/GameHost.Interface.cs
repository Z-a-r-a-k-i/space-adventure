using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private ColorRect _dialogueScrim = null!;
    private ColorRect _controlsScrim = null!;
    private CenterContainer _controlsOverlay = null!;
    private bool _cameraInputBeforeHelp;
    private Control _fieldOrderOverlay = null!;
    private readonly Dictionary<EntityId, FieldOrderView> _fieldOrders = [];
    private Control _worldHealthOverlay = null!;
    private readonly Dictionary<EntityId, WorldHealthView> _worldHealth = [];

    private sealed record WorldHealthView(Control Root, ProgressBar Bar, Label Name, Label Status, StyleBoxFlat Fill);

    private sealed record FieldOrderView(PanelContainer Panel, Label Owner, Label Order, Line2D Leader,
        Label Destination, Line2D DestinationRing)
    {
        public void Hide()
        {
            Panel.Visible = Leader.Visible = Destination.Visible = DestinationRing.Visible = false;
        }
    }

    private void CreateFieldDialogue(CanvasLayer canvas)
    {
        _dialogueScrim = ModalScrim(canvas, 19);
        _dialogueScrim.Color = new Color("030910", .42f);
        _dialogueOverlay = new MarginContainer { Name = "DialogueOverlay", AnchorRight = 1, AnchorBottom = 1,
            MouseFilter = Control.MouseFilterEnum.Stop, Visible = false, ZIndex = 20 };
        _dialogueOverlay.AddThemeConstantOverride("margin_bottom", 42);
        _dialogueOverlay.AddThemeConstantOverride("margin_left", 40);
        _dialogueOverlay.AddThemeConstantOverride("margin_right", 40);
        canvas.AddChild(_dialogueOverlay);
        var panel = HudPanel();
        panel.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        panel.SizeFlagsVertical = Control.SizeFlags.ShrinkEnd;
        panel.AddThemeStyleboxOverride("panel", TacticalUi.FieldPanel(TacticalUi.Cyan, bottom: true, margin: 24));
        _dialogueOverlay.AddChild(panel);
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 28);
        panel.AddChild(columns);
        var speaker = new VBoxContainer { CustomMinimumSize = new Vector2(160, 0) };
        speaker.AddThemeConstantOverride("separation", 12);
        speaker.AddChild(TacticalUi.Eyebrow("COMMS / LOCAL", "a0efd8"));
        _dialogueSpeaker = TacticalUi.Label("", 25);
        speaker.AddChild(_dialogueSpeaker);
        columns.AddChild(speaker);
        var dialogue = new VBoxContainer { CustomMinimumSize = new Vector2(720, 0) };
        dialogue.AddThemeConstantOverride("separation", 14);
        columns.AddChild(dialogue);
        _dialogueLine = HudLabel("", 18, "e1e8eb");
        _dialogueLine.CustomMinimumSize = new Vector2(720, 54);
        dialogue.AddChild(_dialogueLine);
        _dialogueResponses = new VBoxContainer();
        _dialogueResponses.AddThemeConstantOverride("separation", 7);
        dialogue.AddChild(_dialogueResponses);
        dialogue.AddChild(TacticalUi.Eyebrow("1 / 2  Respond     ENTER  First response", "afc1c5"));
    }

    private void CreateFieldOrderOverlay(CanvasLayer canvas)
    {
        _worldHealthOverlay = new Control { Name = "WorldHealth", AnchorRight = 1, AnchorBottom = 1,
            MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 5 };
        canvas.AddChild(_worldHealthOverlay);
        _fieldOrderOverlay = new Control { Name = "FieldOrders", AnchorRight = 1, AnchorBottom = 1,
            MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 6 };
        canvas.AddChild(_fieldOrderOverlay);
    }

    private WorldHealthView WorldHealthFor(EntityId id, bool hostile)
    {
        if (_worldHealth.TryGetValue(id, out var existing)) { return existing; }
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        var name = TacticalUi.Label("", 10, "ee907d");
        var status = TacticalUi.Label("", 11, "e5bc7d");
        foreach (var label in new[] { name, status })
        {
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            label.AddThemeColorOverride("font_shadow_color", new Color("030910"));
            label.AddThemeConstantOverride("shadow_offset_x", 1);
            label.AddThemeConstantOverride("shadow_offset_y", 1);
            root.AddChild(label);
        }
        var bar = TacticalUi.Bar(hostile ? TacticalUi.Danger : TacticalUi.Cyan, 8);
        var background = new StyleBoxFlat { BgColor = new Color("061017"), BorderColor = new Color("061017"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            ContentMarginLeft = 1, ContentMarginRight = 1, ContentMarginTop = 1, ContentMarginBottom = 1,
            ShadowColor = new Color(0, 0, 0, .7f), ShadowSize = 2 };
        bar.AddThemeStyleboxOverride("background", background);
        root.AddChild(bar);
        _worldHealthOverlay.AddChild(root);
        var view = new WorldHealthView(root, bar, name, status, (StyleBoxFlat)bar.GetThemeStylebox("fill"));
        _worldHealth.Add(id, view);
        return view;
    }

    private IEnumerable<Rect2> FieldHudBounds() => new Control[] { _objectivePanel, _pauseButton, _pauseLabel,
        _crewCluster, _actionPanel, _controlsButton, _outcomePanel, _feedbackLabel }
        .Where(control => control.IsVisibleInTree()).Select(control => control.GetGlobalRect().Grow(8));

    private void UpdateWorldHealth(StationRouteObservation route)
    {
        foreach (var view in _worldHealth.Values) { view.Root.Visible = false; }
        if (route.ActiveDialogue is not null || _controlsOverlay.Visible || _completionOverlay.Visible) { return; }
        var occupied = FieldHudBounds().ToList();
        foreach (var actor in route.Party)
        {
            if (actor.Combat is not { } combat) { continue; }
            ShowWorldHealth(actor.Id, _actorViews[actor.Id.Value], combat.Health, combat.MaximumHealth,
                false, "", "", 1.8f, occupied);
        }
        foreach (var hostile in route.Hostiles ?? [])
        {
            var action = hostile.CurrentAction;
            var target = route.Party.FirstOrDefault(actor => actor.Id == action?.CombatTargetId);
            var attack = _enemyViews[hostile.Id].Sentry is null ? "hit" : "fires";
            var status = action?.Phase == PrimaryActionPhase.Windup
                ? $"{target?.DisplayName} · {attack} in {action.PhaseTicksRemaining / 30.0:0.0}s" : "";
            if (hostile.Combat.TauntedBy is not null)
            {
                status = $"TAUNTED {hostile.Combat.TauntRemainingTicks / 30.0:0.0}s"
                    + (action?.Phase == PrimaryActionPhase.Windup ? $" · {attack} {action.PhaseTicksRemaining / 30.0:0.0}s" : "");
            }
            var enemy = _enemyViews[hostile.Id];
            ShowWorldHealth(hostile.Id, enemy.Root, hostile.Combat.Health, hostile.Combat.MaximumHealth,
                true, hostile.DisplayName.Replace("Security ", "", StringComparison.Ordinal), status,
                enemy.Sentry is null ? 1.85f : 2.1f, occupied);
        }
    }

    private void ShowWorldHealth(EntityId id, Node3D actor, int health, int maximum, bool hostile,
        string name, string status, float height, List<Rect2> occupied)
    {
        var view = WorldHealthFor(id, hostile);
        view.Bar.MaxValue = maximum;
        view.Bar.Value = health;
        view.Fill.BgColor = hostile ? TacticalUi.Danger : health <= maximum * .3 ? TacticalUi.Amber : TacticalUi.Cyan;
        view.Name.Text = name;
        view.Name.Visible = hostile;
        view.Status.Text = status;
        view.Status.Visible = status.Length > 0;
        if (health <= 0 || !actor.IsVisibleInTree()) { return; }
        var world = actor.GlobalPosition + Vector3.Up * height;
        if (_camera.IsPositionBehind(world)) { return; }
        var anchor = _camera.UnprojectPosition(world);
        var textWidth = hostile ? Math.Max(
            view.Name.GetThemeFont("font").GetStringSize(name, fontSize: 10).X,
            view.Status.GetThemeFont("font").GetStringSize(status, fontSize: 11).X) + 4 : 0;
        var fullSize = new Vector2(Math.Max(74, textWidth), hostile ? status.Length > 0 ? 44 : 25 : 8);
        var viewport = GetViewport().GetVisibleRect().Grow(-4);
        // Health takes precedence when a nearby crew bar or HUD leaves no room for the full name/status plate.
        foreach (var size in hostile ? new[] { fullSize, new Vector2(74, 8) } : new[] { fullSize })
        {
            view.Root.Size = size;
            view.Name.Visible = hostile && size.Y > 8;
            view.Status.Visible = status.Length > 0 && size.Y > 8;
            view.Name.Position = new Vector2(0, status.Length > 0 ? 18 : 0);
            view.Name.Size = new Vector2(size.X, 14);
            view.Status.Size = new Vector2(size.X, 16);
            view.Bar.Position = new Vector2((size.X - 74) / 2, size.Y - 8);
            view.Bar.Size = new Vector2(74, 8);
            var position = anchor - new Vector2(size.X / 2, size.Y + 6);
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var bounds = new Rect2(position - new Vector2(0, attempt * (size.Y + 4)), size);
                if (!viewport.Encloses(bounds) || occupied.Any(rect => rect.Intersects(bounds))) { continue; }
                view.Root.Position = bounds.Position;
                view.Root.Visible = true;
                occupied.Add(bounds.Grow(3));
                return;
            }
        }
    }

    private FieldOrderView FieldOrderFor(EntityId id)
    {
        if (_fieldOrders.TryGetValue(id, out var existing)) { return existing; }
        var leader = new Line2D { Width = 1.2f, DefaultColor = new Color(TacticalUi.Cyan, .55f), Antialiased = true };
        _fieldOrderOverlay.AddChild(leader);
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(224, 50) };
        panel.AddThemeStyleboxOverride("panel", TacticalUi.FieldPanel(TacticalUi.Cyan, margin: 8));
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 3);
        panel.AddChild(column);
        var owner = TacticalUi.Eyebrow("", "a0efd8");
        var order = TacticalUi.Label("", 13);
        order.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        column.AddChild(owner); column.AddChild(order);
        _fieldOrderOverlay.AddChild(panel);
        var destinationRing = new Line2D { Width = 1.5f, DefaultColor = TacticalUi.Cyan, Antialiased = true };
        destinationRing.Points = Enumerable.Range(0, 25).Select(index => Vector2.FromAngle(index * Mathf.Tau / 24) * 10).ToArray();
        _fieldOrderOverlay.AddChild(destinationRing);
        var destination = TacticalUi.Label("", 11, "a0efd8");
        destination.AddThemeStyleboxOverride("normal", TacticalUi.Box("101c22", "36534f", 3));
        _fieldOrderOverlay.AddChild(destination);
        var result = new FieldOrderView(panel, owner, order, leader, destination, destinationRing);
        result.Hide();
        _fieldOrders.Add(id, result);
        return result;
    }

    private string PendingOrderText(StationRouteObservation route, PrimaryActionObservation action)
    {
        var verb = action.Kind switch
        {
            PrimaryActionKind.Attack => "Attack",
            PrimaryActionKind.Move => "Move to position",
            PrimaryActionKind.Interact => "Interact",
            PrimaryActionKind.Stop => "Stop",
            _ => ShortAction(action).Replace("Deploying barrier", "Barrier", StringComparison.Ordinal)
                .Replace("Burst fire", "Burst", StringComparison.Ordinal),
        };
        var target = AttackTargetName(route, action).Replace("Security ", "", StringComparison.Ordinal);
        var wait = action.WaitingReason switch
        {
            ActionWaitingReason.EncounterReadying => "after draw",
            ActionWaitingReason.OffensiveRecovery => "after recovery",
            _ => "on resume",
        };
        return target.Length > 0 ? $"{verb} → {target}" : $"{verb} · {wait}";
    }

    private void UpdateFieldOrders(GameObservation observation, StationRouteObservation route)
    {
        foreach (var view in _fieldOrders.Values) { view.Hide(); }
        if (!observation.Paused || route.ActiveDialogue is not null || _controlsOverlay.Visible
            || route.Encounter?.Phase is EncounterPhase.Defeat or EncounterPhase.Securing or EncounterPhase.Victory) { return; }
        var viewport = GetViewport().GetVisibleRect().Grow(-12);
        var occupied = FieldHudBounds().ToList();
        occupied.AddRange(_worldHealth.Values.Where(view => view.Root.Visible).Select(view => view.Root.GetGlobalRect().Grow(5)));
        var markerObstacles = new List<Rect2>(occupied);
        var destinations = new List<(FieldOrderView View, Vector2 Point, int Number)>();
        foreach (var actor in route.Party)
        {
            var position = _actorViews[actor.Id.Value].GlobalPosition;
            if (!_camera.IsPositionBehind(position))
            {
                occupied.Add(new Rect2(_camera.UnprojectPosition(position + Vector3.Up * .1f), Vector2.Zero)
                    .Expand(_camera.UnprojectPosition(position + Vector3.Up * 2.2f)).Grow(15));
            }
        }
        foreach (var hostile in route.Hostiles ?? [])
        {
            var position = ToGodot(hostile.Position);
            if (!_camera.IsPositionBehind(position))
            {
                occupied.Add(new Rect2(_camera.UnprojectPosition(position), Vector2.Zero)
                    .Expand(_camera.UnprojectPosition(position + Vector3.Up * 2.4f)).Grow(16));
            }
        }
        for (var index = 0; index < route.Party.Count; index++)
        {
            var actor = route.Party[index];
            if (actor.PendingAction is not { } pending || actor.Combat?.IsDefeated == true) { continue; }
            var world = _actorViews[actor.Id.Value].GlobalPosition + Vector3.Up * 2.1f;
            if (_camera.IsPositionBehind(world)) { continue; }
            var anchor = _camera.UnprojectPosition(world);
            if (!viewport.HasPoint(anchor)) { continue; }
            var view = FieldOrderFor(actor.Id);
            view.Owner.Text = $"{index + 1:00}  {actor.DisplayName.ToUpperInvariant()} / NEXT";
            view.Order.Text = PendingOrderText(route, pending);
            var hasDestination = pending.Kind is PrimaryActionKind.Move or PrimaryActionKind.Interact or PrimaryActionKind.Attack
                || pending.Kind == PrimaryActionKind.Ability && pending.AbilityId != _definition!.Combat.Taunt.Id;
            if (hasDestination)
            {
                var target = route.Hostiles?.FirstOrDefault(hostile => hostile.Id == pending.CombatTargetId);
                var destination = ToGodot(target?.Position ?? pending.Destination) + Vector3.Up * .1f;
                if (!_camera.IsPositionBehind(destination))
                { destinations.Add((view, _camera.UnprojectPosition(destination), index + 1)); }
            }
            var size = view.Panel.GetCombinedMinimumSize().Max(new Vector2(224, 50));
            var offsets = new Vector2[] { new(24, -size.Y - 20), new(-size.X - 24, -size.Y - 20),
                new(24, 22), new(-size.X - 24, 22), new(-size.X - 64, -size.Y / 2), new(64, -size.Y / 2),
                new(-size.X / 2, -size.Y - 100), new(-size.X / 2, 150) };
            Rect2? chosen = null;
            foreach (var offset in offsets)
            {
                var candidate = new Rect2(anchor + offset, size);
                if (viewport.Encloses(candidate) && occupied.All(rect => !rect.Intersects(candidate.Grow(5))))
                { chosen = candidate; break; }
            }
            // The card retains the same pending order if the actor is offscreen or no clear label position remains.
            if (chosen is not { } placement) { continue; }
            view.Panel.Position = placement.Position;
            view.Panel.Size = size;
            view.Panel.Visible = view.Leader.Visible = true;
            var endpoint = new Vector2(Mathf.Clamp(anchor.X, placement.Position.X, placement.End.X),
                Mathf.Clamp(anchor.Y, placement.Position.Y, placement.End.Y));
            view.Leader.Points = [anchor, endpoint];
            occupied.Add(placement.Grow(7));
            markerObstacles.Add(placement.Grow(7));
        }
        while (destinations.Count > 0)
        {
            var first = destinations[0];
            var shared = destinations.Where(item => item.Point.DistanceTo(first.Point) < 24).ToArray();
            destinations.RemoveAll(item => shared.Contains(item));
            var ringBounds = new Rect2(first.Point - new Vector2(12, 12), new Vector2(24, 24));
            if (!viewport.Encloses(ringBounds) || markerObstacles.Any(rect => rect.Intersects(ringBounds))) { continue; }
            var view = first.View;
            view.Destination.Text = string.Join(" / ", shared.Select(item => $"{item.Number:00}"));
            var size = view.Destination.GetCombinedMinimumSize();
            var offsets = new Vector2[] { new(13, -9), new(-size.X - 13, -9), new(13, 15), new(-size.X - 13, 15) };
            foreach (var offset in offsets)
            {
                var bounds = new Rect2(first.Point + offset, size);
                if (!viewport.Encloses(bounds) || markerObstacles.Any(rect => rect.Intersects(bounds.Grow(3)))) { continue; }
                view.Destination.Position = bounds.Position;
                view.Destination.Size = size;
                view.DestinationRing.Position = first.Point;
                view.Destination.Visible = view.DestinationRing.Visible = true;
                markerObstacles.Add(bounds.Grow(3));
                markerObstacles.Add(ringBounds);
                break;
            }
        }
    }

    private static ColorRect ModalScrim(CanvasLayer canvas, int zIndex)
    {
        var scrim = new ColorRect { Color = new Color("030910", .78f), AnchorRight = 1, AnchorBottom = 1,
            MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false, ZIndex = zIndex };
        canvas.AddChild(scrim);
        return scrim;
    }

    private void CreateControlsOverlay(CanvasLayer canvas)
    {
        _controlsScrim = ModalScrim(canvas, 29);
        _controlsOverlay = new CenterContainer { AnchorRight = 1, AnchorBottom = 1,
            MouseFilter = Control.MouseFilterEnum.Stop, Visible = false, ZIndex = 30 };
        canvas.AddChild(_controlsOverlay);
        var panel = HudPanel();
        panel.CustomMinimumSize = new Vector2(680, 0);
        panel.AddThemeStyleboxOverride("panel", TacticalUi.FieldPanel(TacticalUi.Cyan, bottom: true, margin: 28));
        _controlsOverlay.AddChild(panel);
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 14);
        panel.AddChild(content);
        content.AddChild(TacticalUi.Eyebrow("FIELD MANUAL  /  CREW COMMAND", "8bddd9"));
        content.AddChild(TacticalUi.Label("Field operations", 26));
        content.AddChild(HudLabel("Pause with Space to plan. Each crew member keeps one queued order; a new order replaces it.", 14, "bdccd1"));
        content.AddChild(TacticalUi.Rule(TacticalUi.Cyan.Darkened(.5f)));
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 28);
        grid.AddThemeConstantOverride("v_separation", 10);
        content.AddChild(grid);
        foreach (var (key, meaning) in new[]
        {
            ("RIGHT-CLICK", "Move, interact, or assign an attack target"),
            ("CLICK / DRAG", "Select crew · Shift adds to the group"),
            ("TAB / SHIFT + TAB", "Switch the crew member whose abilities you use"),
            ("1 / 2", "Use the focused crew member's abilities"),
            ("X", "Stop selected crew · queued while paused"),
            ("EDGE / WASD / ARROWS", "Pan the camera"),
            ("Q / E · MIDDLE DRAG", "Rotate · drag vertically to tilt"),
            ("WHEEL · PAGE UP / DOWN", "Zoom · adjust pitch"),
            ("F · HOME / R", "Focus selected crew · reset camera orientation"),
        })
        {
            grid.AddChild(TacticalUi.Label(key, 12, "8bddd9"));
            grid.AddChild(TacticalUi.Label(meaning, 13, "cfdbde"));
        }
        content.AddChild(TacticalUi.Rule(new Color("344651")));
        content.AddChild(TacticalUi.Label("Crew fire only at assigned targets. Escape cancels targeting.", 13, "e5bc7d"));
        var close = HudButton("ESC   Return to station", ToggleControls);
        content.AddChild(close);
    }

    private void ToggleControls()
    {
        if (_controlsOverlay.Visible)
        {
            _controlsOverlay.Visible = _controlsScrim.Visible = false;
            _camera.InputEnabled = _cameraInputBeforeHelp;
            return;
        }
        if (_session is null || _dialogueOverlay.Visible) { return; }
        CancelSelectionGesture();
        CancelAbilityTargeting();
        if (!_session.IsPaused) { Dispatch(new SetPauseCommand(NextHumanCommandId("help.pause"), true)); }
        _cameraInputBeforeHelp = _camera.InputEnabled;
        _camera.InputEnabled = false;
        _controlsOverlay.Visible = _controlsScrim.Visible = true;
    }

    private bool HandleControlsInput(InputEvent @event)
    {
        if (_session is null || _controlsOverlay is null) { return false; }
        if (@event is InputEventKey { Pressed: true, Echo: false } key
            && (IsKey(key, Key.F1) || _controlsOverlay.Visible && IsKey(key, Key.Escape)))
        {
            ToggleControls();
            GetViewport().SetInputAsHandled();
            return true;
        }
        else if (_controlsOverlay.Visible && @event is InputEventKey)
        {
            GetViewport().SetInputAsHandled();
            return true;
        }
        return false;
    }
}
