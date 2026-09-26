using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private ColorRect _dialogueScrim = null!;
    private ColorRect _controlsScrim = null!;
    private CenterContainer _controlsOverlay = null!;
    private bool _cameraInputBeforeHelp;
    private HSlider _masterVolume = null!;
    private Label _masterVolumeLabel = null!;
    private Button _muteButton = null!;
    private ScrollContainer _manualScroll = null!;
    private Button _manualCloseButton = null!;
    private Control _fieldOrderOverlay = null!;
    private readonly Dictionary<EntityId, FieldOrderView> _fieldOrders = [];
    private Control _worldHealthOverlay = null!;
    private readonly Dictionary<EntityId, WorldHealthView> _worldHealth = [];

    private sealed record WorldHealthView(Control Root, ProgressBar Bar, Label Name, Label Status, StyleBoxFlat Fill, Line2D Leader);

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
        dialogue.AddChild(TacticalUi.Eyebrow("1 / 2  Respond     TAB / ARROWS  Focus     ENTER  Confirm", "afc1c5"));
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
        var name = TacticalUi.Label("", 12, "ee907d");
        var status = TacticalUi.Label("", 12, "e5bc7d");
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
        var leader = new Line2D { Width = 1.2f, DefaultColor = new Color(TacticalUi.Muted, .75f), Antialiased = true,
            Visible = false };
        _worldHealthOverlay.AddChild(leader);
        _worldHealthOverlay.AddChild(root);
        var view = new WorldHealthView(root, bar, name, status, (StyleBoxFlat)bar.GetThemeStylebox("fill"), leader);
        _worldHealth.Add(id, view);
        return view;
    }

    private IEnumerable<Rect2> FieldHudBounds() => new Control[] { _objectivePanel, _tipCard, _pauseButton, _pauseLabel,
        _crewCluster, _actionPanel, _controlsButton, _outcomePanel, _feedbackLabel }
        .Where(control => control.IsVisibleInTree()).Select(control => control.GetGlobalRect().Grow(8));

    private void UpdateWorldHealth(StationRouteObservation route)
    {
        foreach (var view in _worldHealth.Values) { view.Root.Visible = view.Leader.Visible = false; }
        if (route.Phase == ScenarioPhase.Completed || route.ActiveDialogue is not null || _controlsOverlay.Visible || _completionOverlay.Visible) { return; }
        var occupied = FieldHudBounds().ToList();
        var hostiles = route.VisibleHostiles;
        foreach (var hostile in hostiles.Where(enemy => enemy.CurrentAction?.Phase == PrimaryActionPhase.Windup))
        { ShowHostile(hostile); }
        foreach (var actor in route.Party.OrderByDescending(actor => actor.Combat is { } combat && combat.Health <= combat.MaximumHealth * .3)
            .ThenByDescending(actor => actor.Id == _focusedActorId))
        {
            if (actor.Combat is not { } combat) { continue; }
            var name = $"{CrewNumber(route, actor.Id):00} {actor.DisplayName}" + (actor.Id == _focusedActorId ? " ▸" : "");
            ShowWorldHealth(actor.Id, _actorViews[actor.Id.Value], combat.Health, combat.MaximumHealth,
                false, name, combat.Health <= combat.MaximumHealth * .3 ? "LOW HEALTH" : "", 1.8f, occupied,
                CrewAccent(route, actor));
        }
        foreach (var hostile in hostiles.Where(enemy => enemy.CurrentAction?.Phase != PrimaryActionPhase.Windup))
        { ShowHostile(hostile); }

        void ShowHostile(HostileObservation hostile)
        {
            var action = hostile.CurrentAction;
            var target = route.Party.FirstOrDefault(actor => actor.Id == action?.CombatTargetId);
            var attack = _enemyViews[hostile.Id].Sentry is not null || _enemyViews[hostile.Id].Armed is not null ? "SHOT" : "STRIKE";
            var status = action?.Phase == PrimaryActionPhase.Windup
                ? $"{attack} → {target?.DisplayName} · {action.PhaseTicksRemaining / 30.0:0.0}s" : "";
            if (hostile.Combat.TauntedBy is not null)
            {
                status = $"TAUNTED {hostile.Combat.TauntRemainingTicks / 30.0:0.0}s"
                    + (action?.Phase == PrimaryActionPhase.Windup ? $" · {attack} {action.PhaseTicksRemaining / 30.0:0.0}s" : "");
            }
            var enemy = _enemyViews[hostile.Id];
            ShowWorldHealth(hostile.Id, enemy.Root, hostile.Combat.Health, hostile.Combat.MaximumHealth,
                true, hostile.DisplayName.Replace("Security ", "", StringComparison.Ordinal), status,
                enemy.Sentry is null ? 1.85f : 2.1f, occupied);
            // The bar fades in with the model that sight has just revealed.
            var reveal = RevealAlpha(hostile.Id);
            _worldHealth[hostile.Id].Root.Modulate = _worldHealth[hostile.Id].Leader.Modulate = new Color(1, 1, 1, reveal);
        }
    }

    private void ShowWorldHealth(EntityId id, Node3D actor, int health, int maximum, bool hostile,
        string name, string status, float height, List<Rect2> occupied, Color? crewAccent = null)
    {
        var view = WorldHealthFor(id, hostile);
        view.Bar.MaxValue = maximum;
        view.Bar.Value = health;
        var accent = hostile ? TacticalUi.Danger : crewAccent ?? TacticalUi.Cyan;
        view.Fill.BgColor = health <= maximum * .3 ? TacticalUi.Amber : accent;
        view.Name.AddThemeColorOverride("font_color", accent);
        view.Leader.DefaultColor = new Color(accent, .7f);
        view.Name.Text = name;
        view.Status.Text = status;
        view.Status.Visible = status.Length > 0;
        if (health <= 0 || !actor.IsVisibleInTree()) { return; }
        var world = actor.GlobalPosition + Vector3.Up * height;
        if (_camera.IsPositionBehind(world)) { return; }
        var anchor = _camera.UnprojectPosition(world);
        var textWidth = Math.Max(view.Name.GetThemeFont("font").GetStringSize(name, fontSize: 12).X,
            view.Status.GetThemeFont("font").GetStringSize(status, fontSize: 12).X) + 8;
        var fullSize = new Vector2(Math.Clamp(textWidth, 84, 250), status.Length > 0 ? 44 : 27);
        var viewport = GetViewport().GetVisibleRect().Grow(-4);
        // Preserve an imminent threat or low-health warning before dropping to health alone.
        var sizes = status.Length > 0 ? new[] { fullSize, new Vector2(fullSize.X, 27), new Vector2(84, 8) }
            : new[] { fullSize, new Vector2(84, 8) };
        foreach (var size in sizes)
        {
            view.Root.Size = size;
            view.Name.Visible = size.Y > 8 && (status.Length == 0 || size.Y == fullSize.Y);
            view.Status.Visible = status.Length > 0 && size.Y > 8;
            view.Name.Position = new Vector2(0, status.Length > 0 ? 17 : 0);
            view.Name.Size = new Vector2(size.X, 17);
            view.Status.Size = new Vector2(size.X, 17);
            view.Bar.Position = new Vector2((size.X - 84) / 2, size.Y - 8);
            view.Bar.Size = new Vector2(84, 8);
            var position = anchor - new Vector2(size.X / 2, size.Y + 6);
            var offsets = new Vector2[] { Vector2.Zero, new(0, -size.Y - 7), new(-size.X - 12, -10),
                new(size.X + 12, -10), new(0, -2 * (size.Y + 7)), new(-size.X / 2 - 8, -size.Y - 20), new(size.X / 2 + 8, -size.Y - 20) };
            foreach (var offset in offsets)
            {
                var bounds = new Rect2(position + offset, size);
                if (!viewport.Encloses(bounds) || occupied.Any(rect => rect.Intersects(bounds))) { continue; }
                view.Root.Position = bounds.Position;
                view.Root.Visible = true;
                view.Leader.Visible = offset.LengthSquared() > 1;
                view.Leader.Points = [anchor, new Vector2(Mathf.Clamp(anchor.X, bounds.Position.X, bounds.End.X), bounds.End.Y + 2)];
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
        var target = (route.Party.FirstOrDefault(crew => crew.Id == action.CombatTargetId)?.DisplayName
            ?? FindVisibleHostile(route, action.CombatTargetId)?.DisplayName ?? "")
            .Replace("Security ", "", StringComparison.Ordinal);
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
        foreach (var hostile in route.VisibleHostiles)
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
            var accent = CrewAccent(route, actor);
            view.Owner.AddThemeColorOverride("font_color", accent);
            ((StyleBoxFlat)view.Panel.GetThemeStylebox("panel")).BorderColor = accent;
            view.Leader.DefaultColor = new Color(accent, .65f);
            view.DestinationRing.DefaultColor = accent;
            view.Destination.AddThemeColorOverride("font_color", accent);
            view.Order.Text = PendingOrderText(route, pending);
            var hasDestination = pending.Kind is PrimaryActionKind.Move or PrimaryActionKind.Interact or PrimaryActionKind.Attack
                || pending.Kind == PrimaryActionKind.Ability && pending.AbilityId != _definition!.Combat.Taunt.Id;
            var hiddenHostileTarget = pending.CombatTargetId is { } targetId && _enemyViews.ContainsKey(targetId)
                && FindVisibleHostile(route, targetId) is null;
            if (hasDestination && !hiddenHostileTarget)
            {
                var target = route.VisibleHostiles.FirstOrDefault(hostile => hostile.Id == pending.CombatTargetId);
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
        _manualScroll = new ScrollContainer { CustomMinimumSize = new Vector2(680, 560),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        panel.AddChild(_manualScroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 8);
        _manualScroll.AddChild(content);
        content.AddChild(TacticalUi.Eyebrow("FIELD MANUAL  /  CREW COMMAND      ↑ / ↓ SCROLL", "8bddd9"));
        content.AddChild(TacticalUi.Label("Field operations", 26));
        content.AddChild(HudLabel("Pause with Space to plan. Each crew member keeps one queued order; a new order replaces it.", 14, "bdccd1"));
        content.AddChild(TacticalUi.Rule(TacticalUi.Cyan.Darkened(.5f)));
        var grid = new GridContainer { Columns = 2 };
        grid.AddThemeConstantOverride("h_separation", 28);
        grid.AddThemeConstantOverride("v_separation", 8);
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
        content.AddChild(TacticalUi.Label("Interrupt stops wind-ups. Barrier blocks shots; Taunt draws nearby threats.", 13));
        content.AddChild(TacticalUi.Label("Medic: Heal (1) targets a living ally or portrait. Healing Field (2) restores crew inside its circle.", 13));
        content.AddChild(TacticalUi.Label("Each victory restores the crew. Defeat retries this fight; prior route progress stays cleared.", 13));
        content.AddChild(TacticalUi.Label("Numbers identify crew. The portrait marked 1 / 2 owns the ability keys.", 13));
        var audio = new HBoxContainer();
        audio.AddThemeConstantOverride("separation", 14);
        content.AddChild(audio);
        _masterVolumeLabel = TacticalUi.Label("Sound 100%", 13);
        _masterVolumeLabel.CustomMinimumSize = new Vector2(100, 0);
        _masterVolumeLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        audio.AddChild(_masterVolumeLabel);
        _masterVolume = new HSlider { MinValue = 0, MaxValue = 100, Step = 5, Value = 100,
            CustomMinimumSize = new Vector2(220, 30), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.All };
        audio.AddChild(_masterVolume);
        _masterVolume.ValueChanged += _ => UpdateMasterVolume();
        _muteButton = HudButton("M   Mute", ToggleMasterMute);
        _muteButton.CustomMinimumSize = new Vector2(120, 32);
        _muteButton.FocusMode = Control.FocusModeEnum.All;
        audio.AddChild(_muteButton);
        content.AddChild(TacticalUi.Label("− / +  Volume    M  Mute    TAB  Focus · Applies to this session", 12, "afc1c5"));
        _manualCloseButton = HudButton("ESC   Return to station", ToggleControls);
        _manualCloseButton.FocusMode = Control.FocusModeEnum.All;
        content.AddChild(_manualCloseButton);
        content.AddChild(TacticalUi.Label("Play stays paused after closing. Press Space when ready.", 12, "afc1c5"));
    }

    private void UpdateMasterVolume()
    {
        var bus = AudioServer.GetBusIndex("Master");
        if (bus < 0) { return; }
        AudioServer.SetBusVolumeDb(bus, _masterVolume.Value <= 0 ? -80 : Mathf.LinearToDb((float)_masterVolume.Value / 100));
        var muted = AudioServer.IsBusMute(bus);
        _masterVolumeLabel.Text = muted ? "Sound muted" : $"Sound {_masterVolume.Value:0}%";
        _muteButton.Text = muted ? "M   Unmute" : "M   Mute";
    }

    private void ToggleMasterMute()
    {
        var bus = AudioServer.GetBusIndex("Master");
        if (bus < 0) { return; }
        AudioServer.SetBusMute(bus, !AudioServer.IsBusMute(bus));
        UpdateMasterVolume();
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
        _manualScroll.ScrollVertical = 0;
        _controlsOverlay.Visible = _controlsScrim.Visible = true;
        _manualCloseButton.GrabFocus();
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
        else if (_controlsOverlay.Visible && @event is InputEventKey audioKey)
        {
            if (audioKey.Pressed)
            {
                if (IsKey(audioKey, Key.Tab) && !audioKey.Echo)
                {
                    var controls = new Control[] { _masterVolume, _muteButton, _manualCloseButton };
                    var index = Array.FindIndex(controls, control => control.HasFocus());
                    var next = controls[(index + (audioKey.ShiftPressed ? controls.Length - 1 : 1)) % controls.Length];
                    next.GrabFocus();
                    _manualScroll.EnsureControlVisible(next);
                }
                else if ((IsKey(audioKey, Key.Enter) || IsKey(audioKey, Key.KpEnter)) && !audioKey.Echo)
                { if (_muteButton.HasFocus()) { ToggleMasterMute(); } else if (!_masterVolume.HasFocus()) { ToggleControls(); } }
                else if (IsKey(audioKey, Key.M) && !audioKey.Echo) { ToggleMasterMute(); }
                else if (IsKey(audioKey, Key.Minus) || IsKey(audioKey, Key.KpSubtract)) { _masterVolume.Value -= 5; }
                else if (IsKey(audioKey, Key.Equal) || IsKey(audioKey, Key.Plus) || IsKey(audioKey, Key.KpAdd)) { _masterVolume.Value += 5; }
                else if (IsKey(audioKey, Key.Left) && _masterVolume.HasFocus()) { _masterVolume.Value -= 5; }
                else if (IsKey(audioKey, Key.Right) && _masterVolume.HasFocus()) { _masterVolume.Value += 5; }
                else if (IsKey(audioKey, Key.Up)) { _manualScroll.ScrollVertical -= 32; }
                else if (IsKey(audioKey, Key.Down)) { _manualScroll.ScrollVertical += 32; }
                else if (IsKey(audioKey, Key.Pageup)) { _manualScroll.ScrollVertical -= 200; }
                else if (IsKey(audioKey, Key.Pagedown)) { _manualScroll.ScrollVertical += 200; }
            }
            GetViewport().SetInputAsHandled();
            return true;
        }
        return false;
    }
}
