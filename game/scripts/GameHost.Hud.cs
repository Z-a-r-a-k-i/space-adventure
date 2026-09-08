using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private Label _pendingLabel = null!;
    private PanelContainer _enemyPanel = null!;
    private VBoxContainer _threatList = null!;
    private readonly Dictionary<EntityId, (Button Button, Label Status, ProgressBar Health)> _threatRows = [];
    private ActionTile _abilityButton = null!;
    private ActionTile _secondaryAbilityButton = null!;
    private ActionTile _stopButton = null!;
    private Button _pauseButton = null!;
    private EntityId? _focusedActorId;
    private EntityId? _abilityOwnerId;

    private static PanelContainer HudPanel()
    {
        var panel = new PanelContainer { ZIndex = 10 };
        panel.AddThemeStyleboxOverride("panel", TacticalUi.Box());
        return panel;
    }

    private static Label HudLabel(string text, int fontSize, string color = "dce6e9")
    {
        var label = TacticalUi.Label(text, fontSize, color);
        label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return label;
    }

    private static Button HudButton(string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(148, 38) };
        TacticalUi.Style(button);
        button.Pressed += action;
        return button;
    }

    private static ActionTile Tile(string key, string caption, string icon, Action action)
    {
        var tile = new ActionTile();
        tile.Build(key, caption, icon);
        tile.Pressed += action;
        return tile;
    }

    private void CreateTacticalHud(CanvasLayer canvas)
    {
        var objective = HudPanel();
        objective.OffsetLeft = 20; objective.OffsetTop = 20; objective.OffsetRight = 350;
        canvas.AddChild(objective);
        var objectiveContent = new VBoxContainer();
        objectiveContent.AddThemeConstantOverride("separation", 7);
        objective.AddChild(objectiveContent);
        objectiveContent.AddChild(HudLabel("FRONTIER STATION     /     AWAKENING", 10, "8fa7b6"));
        _objectiveLabel = HudLabel("", 16);
        _objectiveLabel.CustomMinimumSize = new Vector2(306, 38);
        objectiveContent.AddChild(_objectiveLabel);

        _pauseButton = HudButton("SPACE   Pause", () => Dispatch(new SetPauseCommand(NextHumanCommandId("pause"), !_session!.IsPaused)));
        _pauseButton.AnchorLeft = .5f; _pauseButton.AnchorRight = .5f;
        _pauseButton.OffsetLeft = -105; _pauseButton.OffsetRight = 105; _pauseButton.OffsetTop = 20;
        canvas.AddChild(_pauseButton);
        _pauseLabel = HudLabel("", 11, "e9bd76");
        _pauseLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _pauseLabel.AnchorLeft = .5f; _pauseLabel.AnchorRight = .5f;
        _pauseLabel.OffsetLeft = -150; _pauseLabel.OffsetRight = 150; _pauseLabel.OffsetTop = 63;
        canvas.AddChild(_pauseLabel);

        _enemyPanel = HudPanel();
        _enemyPanel.AnchorLeft = 1; _enemyPanel.AnchorRight = 1;
        _enemyPanel.OffsetLeft = -270; _enemyPanel.OffsetRight = -20; _enemyPanel.OffsetTop = 20;
        canvas.AddChild(_enemyPanel);
        _threatList = new VBoxContainer();
        _threatList.AddThemeConstantOverride("separation", 9);
        _enemyPanel.AddChild(_threatList);
        _threatList.AddChild(HudLabel("HOSTILE CONTACTS", 10, "ee907d"));

        var dock = HudPanel();
        dock.AnchorRight = 1; dock.AnchorTop = 1; dock.AnchorBottom = 1;
        dock.OffsetLeft = 20; dock.OffsetRight = -20; dock.OffsetTop = -150; dock.OffsetBottom = -16;
        canvas.AddChild(dock);
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 18);
        dock.AddChild(columns);
        _partyList = new HBoxContainer();
        _partyList.AddThemeConstantOverride("separation", 8);
        columns.AddChild(_partyList);
        var orders = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(205, 0) };
        orders.AddThemeConstantOverride("separation", 7);
        columns.AddChild(orders);
        _actionLabel = HudLabel("", 15);
        _pendingLabel = HudLabel("", 12, "e9bd76");
        _combatLabel = HudLabel("", 11, "8fa7b6");
        orders.AddChild(_actionLabel); orders.AddChild(_pendingLabel); orders.AddChild(_combatLabel);
        _faceButton = HudButton("T   Face direction", BeginFacingTargeting);
        _faceButton.CustomMinimumSize = new Vector2(0, 26);
        _faceButton.AddThemeFontSizeOverride("font_size", 11);
        _faceButton.TooltipText = "T then click, or Alt + right-click: turn selected crew in place. Move or explicit attack orders release the heading.";
        orders.AddChild(_faceButton);

        var actions = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        actions.AddThemeConstantOverride("separation", 8);
        columns.AddChild(actions);
        _abilityButton = Tile("1", "Interrupt", "suppression", () => BeginAbilityTargeting());
        _secondaryAbilityButton = Tile("2", "Burst", "burst", () => BeginAbilityTargeting(1));
        _stopButton = Tile("X", "Stop", "stop", StopSelectedActors);
        _stopButton.TooltipText = "Cancel orders for selected crew.";
        actions.AddChild(_abilityButton); actions.AddChild(_secondaryAbilityButton); actions.AddChild(_stopButton);
        _retryButton = HudButton("ENTER   Retry fight", RestartEncounter);
        _retryButton.FocusMode = Control.FocusModeEnum.All;
        _retryButton.Visible = false;
        actions.AddChild(_retryButton);

        _feedbackLabel = HudLabel("", 12, "a2bac7");
        _feedbackLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        _feedbackLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _feedbackLabel.AnchorTop = 1; _feedbackLabel.AnchorBottom = 1;
        _feedbackLabel.OffsetLeft = 24; _feedbackLabel.OffsetTop = -177; _feedbackLabel.OffsetRight = 650;
        canvas.AddChild(_feedbackLabel);
        var help = HudLabel("DRAG  Select crew    TAB  Abilities    EDGE / WASD  Pan    Q / E  Rotate    WHEEL  Zoom", 10, "7f9cae");
        help.AnchorLeft = 1; help.AnchorRight = 1; help.AnchorTop = 1; help.AnchorBottom = 1;
        help.OffsetLeft = -570; help.OffsetRight = -24; help.OffsetTop = -173;
        help.HorizontalAlignment = HorizontalAlignment.Right;
        canvas.AddChild(help);
    }

    private ActorObservation FocusedActor(StationRouteObservation route) =>
        route.Party.FirstOrDefault(actor => actor.Id == _focusedActorId)
        ?? route.Party.FirstOrDefault(actor => _selectedActorIds.Contains(actor.Id)) ?? route.Protagonist;

    private IEnumerable<ActorObservation> SelectedLivingActors(StationRouteObservation route) =>
        route.Party.Where(actor => _selectedActorIds.Contains(actor.Id) && actor.Combat?.IsDefeated != true);

    private void RefreshPartyUi(StationRouteObservation route)
    {
        _selectedActorIds.RemoveWhere(id => route.Party.All(actor => actor.Id != id));
        foreach (var actor in route.Party)
        {
            if (!_partyButtons.ContainsKey(actor.Id.Value))
            {
                var card = new CrewCard();
                var portrait = actor.Id == route.Protagonist.Id ? "vanguard" : "protector";
                card.Build($"res://ui/portraits/{portrait}.png");
                var id = actor.Id;
                card.Pressed += () => SelectActor(id, Input.IsKeyPressed(Key.Shift));
                _partyButtons.Add(actor.Id.Value, card);
                _partyList.AddChild(card);
                _selectedActorIds.Add(actor.Id);
            }
        }
        if (_selectedActorIds.Count == 0) { _selectedActorIds.Add(route.Protagonist.Id); }
        if (_focusedActorId is null || !_selectedActorIds.Contains(_focusedActorId.Value))
        { _focusedActorId = route.Party.First(actor => _selectedActorIds.Contains(actor.Id)).Id; }
        foreach (var actor in route.Party)
        {
            var phase = route.Encounter?.Phase;
            var current = phase == EncounterPhase.Readying ? "Drawing weapon" : phase == EncounterPhase.Securing ? "Securing weapon" : ShortAction(actor.CurrentAction);
            if (current == "Ready") { current = actor.FacingHeld ? "Holding direction" : "Awaiting orders"; }
            _partyButtons[actor.Id.Value].Synchronize(actor, _selectedActorIds.Contains(actor.Id), actor.Id == _focusedActorId,
                current, actor.PendingAction is null ? "" : $"Next: {ShortAction(actor.PendingAction)}",
                AttackTargetName(route, actor.CurrentAction), actor.Id == route.Protagonist.Id ? TacticalUi.Cyan : TacticalUi.Amber);
        }
    }

    private void SelectActor(EntityId id, bool add)
    {
        if (!_session!.Observe().StationRoute!.Party.Any(actor => actor.Id == id)) { return; }
        CancelSelectionGesture();
        CancelAbilityTargeting();
        if (!add) { _selectedActorIds.Clear(); }
        if (!add || !_selectedActorIds.Remove(id)) { _selectedActorIds.Add(id); }
        if (_selectedActorIds.Count == 0) { _selectedActorIds.Add(id); }
        _focusedActorId = _selectedActorIds.Contains(id) ? id : _selectedActorIds.First();
        RenderObservation(_session.Observe());
    }

    private void CycleActorSelection(bool backwards)
    {
        var route = _session!.Observe().StationRoute!;
        var grouped = _selectedActorIds.Count > 1;
        var members = route.Party.Where(actor => actor.Combat?.IsDefeated != true
            && (!grouped || _selectedActorIds.Contains(actor.Id))).ToArray();
        if (members.Length == 0 || route.ActiveDialogue is not null) { return; }
        var index = Array.FindIndex(members, actor => actor.Id == _focusedActorId);
        var next = members[(index + (backwards ? members.Length - 1 : 1)) % members.Length].Id;
        if (!grouped) { SelectActor(next, false); return; }
        CancelSelectionGesture();
        CancelAbilityTargeting();
        _focusedActorId = next;
        RenderObservation(_session.Observe());
    }

    private EntityId? PickCrew(Vector2 position)
    {
        var origin = _camera.ProjectRayOrigin(position);
        var hit = CastRay(origin, origin + _camera.ProjectRayNormal(position) * 200, CrewCollisionLayer);
        return hit.Count > 0 && hit["collider"].AsGodotObject() is Node collider && collider.HasMeta("stable_id")
            ? new EntityId(collider.GetMeta("stable_id").AsString()) : null;
    }

    private void StopSelectedActors()
    {
        CancelAbilityTargeting();
        Dispatch(new StopActorsCommand(NextHumanCommandId("stop"), SelectedLivingActors(_session!.Observe().StationRoute!).Select(actor => actor.Id)));
    }

    private static string AttackTargetName(StationRouteObservation route, PrimaryActionObservation? action) =>
        action?.CombatTargetId is not null ? route.Hostiles?.FirstOrDefault(hostile => hostile.Id == action.CombatTargetId)?.DisplayName ?? "" : "";

    private string ShortAction(PrimaryActionObservation? action) => action?.Kind switch
    {
        PrimaryActionKind.Attack => action.Phase == PrimaryActionPhase.Moving ? "Closing range" : action.Phase == PrimaryActionPhase.Windup ? "Firing" : "Recovering",
        PrimaryActionKind.Ability => action.AbilityId == _definition!.Combat.Barrier.Id ? "Deploying barrier"
            : action.AbilityId == _definition.Combat.Taunt.Id ? "Taunt"
            : action.AbilityId == _definition.Combat.Burst.Id ? "Burst fire" : "Interrupt",
        PrimaryActionKind.Move => "Moving",
        PrimaryActionKind.Interact => "Interacting", PrimaryActionKind.Stop => "Stop", PrimaryActionKind.Face => "Turning", _ => "Ready",
    };

    private void UpdateTacticalHud(GameObservation observation, StationRouteObservation route, ActorObservation actor)
    {
        var combat = actor.Combat!;
        var encounter = route.Encounter!;
        var active = encounter.Phase is EncounterPhase.Active or EncounterPhase.Readying;
        var cooldown = combat.Cooldowns.FirstOrDefault(value => value.AbilityId == actor.Loadout?.ActiveAbilityId);
        var secondaryCooldown = combat.Cooldowns.FirstOrDefault(value => value.AbilityId == actor.Loadout?.SecondaryAbilityId);
        var barrierAbility = actor.Loadout?.ActiveAbilityTargetKind == AbilityTargetKind.Barrier;
        var selectedLiving = SelectedLivingActors(route).ToArray();
        var ready = !combat.IsDefeated && active && route.ActiveDialogue is null;
        _abilityButton.Disabled = !ready || cooldown?.RemainingTicks > 0;
        _abilityButton.SetState(barrierAbility ? "Barrier" : "Interrupt", barrierAbility ? "guard" : "suppression",
            combat.IsDefeated ? "Down" : cooldown?.RemainingTicks > 0 ? $"{cooldown.RemainingTicks / 30.0:0.0}s" : _abilityTargeting && _targetAbilityId == actor.Loadout?.ActiveAbilityId ? "Aim + confirm" : active ? "Ready" : "In combat",
            cooldown is { TotalTicks: > 0 } ? 1 - (double)cooldown.RemainingTicks / cooldown.TotalTicks : 1);
        _abilityButton.TooltipText = barrierAbility
            ? $"Barrier · place within {_definition!.Combat.Barrier.RangeMeters:0.#}m, then aim toward incoming fire. Stays at that position and facing for {_definition.Combat.Barrier.DurationTicks / 30.0:0.#}s."
            : "Interrupt · click the floor. Cancels enemy wind-ups inside the circle; deals light damage.";
        _secondaryAbilityButton.Disabled = !ready || secondaryCooldown?.RemainingTicks > 0;
        _secondaryAbilityButton.SetState(barrierAbility ? "Taunt" : "Burst", barrierAbility ? "taunt" : "burst",
            combat.IsDefeated ? "Down" : secondaryCooldown?.RemainingTicks > 0 ? $"{secondaryCooldown.RemainingTicks / 30.0:0.0}s"
                : _abilityTargeting && _targetAbilityId == actor.Loadout?.SecondaryAbilityId ? "Pick enemy" : active ? "Ready" : "In combat",
            secondaryCooldown is { TotalTicks: > 0 } ? 1 - (double)secondaryCooldown.RemainingTicks / secondaryCooldown.TotalTicks : 1);
        _secondaryAbilityButton.TooltipText = barrierAbility
            ? $"Taunt · nearby enemies focus Protector for {_definition!.Combat.Taunt.DurationTicks / 30.0:0.#}s. Radius {_definition.Combat.Taunt.RadiusMeters:0.#}m. Shots already in flight keep their target."
            : $"Burst · choose an enemy within {_definition!.Combat.Burst.RangeMeters:0.#}m. Fires {_definition.Combat.Burst.ShotCount} rapid shots for {_definition.Combat.Burst.DamagePerShot} damage each.";
        _stopButton.Disabled = selectedLiving.Length == 0 || encounter.Phase is EncounterPhase.Defeat or EncounterPhase.Securing || route.ActiveDialogue is not null;
        _abilityButton.Visible = _secondaryAbilityButton.Visible = _stopButton.Visible = encounter.Phase != EncounterPhase.Defeat;
        _faceButton.Disabled = _stopButton.Disabled;
        _faceButton.Visible = encounter.Phase != EncounterPhase.Defeat;
        _faceButton.Text = _facingTargeting ? "T   Click a direction" : actor.FacingHeld ? "T   Change facing" : "T   Face direction";
        _stopButton.SetState("Stop", "stop", selectedLiving.Length > 1 ? "Both crew" : selectedLiving.FirstOrDefault()?.DisplayName ?? "No crew", 1);
        _pauseButton.Disabled = encounter.Phase == EncounterPhase.Defeat || route.ActiveDialogue is not null;
        _pauseButton.Text = encounter.Phase == EncounterPhase.Defeat ? "ENCOUNTER LOST"
            : observation.Paused ? "SPACE   Resume" : "SPACE   Pause";
        _pauseLabel.Text = encounter.Phase == EncounterPhase.Defeat ? "ENTER · RETRY FIGHT"
            : observation.Paused ? "TACTICAL PAUSE · QUEUE ORDERS" : active ? "LIVE COMBAT" : "EXPLORATION";
        _actionLabel.Text = $"{actor.DisplayName}  /  {ShortAction(actor.CurrentAction)}";
        var target = route.Hostiles?.FirstOrDefault(hostile => hostile.Id == combat.RememberedAttackTargetId);
        _combatLabel.Text = target is null ? "Right-click to move or attack" : $"Target: {target.DisplayName}";
        if (actor.FacingHeld && target is null) { _combatLabel.Text = "Facing held · awaiting orders"; }
        var waiting = actor.PendingAction?.WaitingReason switch
        {
            ActionWaitingReason.OffensiveRecovery => "after recovery",
            ActionWaitingReason.EncounterReadying => "after draw", _ => "on resume",
        };
        _pendingLabel.Text = actor.PendingAction is null ? (_selectedActorIds.Count > 1 ? "Group orders · Tab switches abilities" : "No queued order")
            : $"Next: {ShortAction(actor.PendingAction)} · {waiting}";
        if (barrierAbility && encounter.Barrier is { } barrier) { _combatLabel.Text = $"Barrier · {barrier.RemainingTicks / 30.0:0.0}s · stationary"; }
        _objectiveLabel.Text = route.Objective.Text;
        if (encounter.Phase == EncounterPhase.Readying)
        { _actionLabel.Text = $"Drawing weapons · {encounter.TransitionTicksRemaining / 30.0:0.0}s"; }
        if (encounter.Phase == EncounterPhase.Securing)
        { _objectiveLabel.Text = "Threats neutralized"; _actionLabel.Text = "Securing weapons"; _pendingLabel.Text = "Encounter clearing"; }
        if (encounter.Phase == EncounterPhase.Defeat)
        { _actionLabel.Text = "Crew down"; _pendingLabel.Text = "Retry restores the crew."; _combatLabel.Text = "Route progress is preserved."; }
        if (encounter.Id == _definition!.Combat.PartyEncounter.Id && encounter.Phase == EncounterPhase.Victory)
        { _objectiveLabel.Text = "Current prototype slice complete"; _actionLabel.Text = "Arena secured"; _pendingLabel.Text = "Both threats eliminated"; _combatLabel.Text = "Final airlock follows in the next slice."; }
        if (combat.IsDefeated && encounter.Phase != EncounterPhase.Defeat)
        { _actionLabel.Text = $"{actor.DisplayName} is down"; _pendingLabel.Text = "Select the surviving crew member."; }
        UpdateThreatHud(route);
        if (_reviewDrivesClock && _reviewMode == "record") { _pauseLabel.Text = "COMBAT REVIEW"; }
    }

    private void UpdateThreatHud(StationRouteObservation route)
    {
        _enemyPanel.Visible = route.Encounter?.Phase is EncounterPhase.Readying or EncounterPhase.Active or EncounterPhase.Defeat;
        foreach (var (id, row) in _threatRows)
        { row.Button.Visible = route.Hostiles?.Any(hostile => hostile.Id == id) == true; }
        foreach (var hostile in route.Hostiles ?? [])
        {
            if (!_threatRows.TryGetValue(hostile.Id, out var row))
            {
                var targetId = hostile.Id;
                var button = HudButton("", () =>
                {
                    if (_abilityTargeting && _targetAbilityKind == AbilityTargetKind.Entity) { ConfirmEnemyAbility(targetId); return; }
                    CancelAbilityTargeting();
                    AttackWithSelectedCrew(targetId);
                });
                button.CustomMinimumSize = new Vector2(224, 68);
                button.TooltipText = "Assign this target to the selected crew.";
                var column = new VBoxContainer { OffsetLeft = 9, OffsetTop = 7, OffsetRight = 215, OffsetBottom = 61, MouseFilter = Control.MouseFilterEnum.Ignore };
                button.AddChild(column);
                column.AddChild(TacticalUi.Label(hostile.DisplayName.ToUpperInvariant(), 12, "ee907d"));
                var health = TacticalUi.Bar(TacticalUi.Danger, 4);
                var status = TacticalUi.Label("", 11, "b7c5cd");
                column.AddChild(health); column.AddChild(status);
                _threatList.AddChild(button);
                row = (button, status, health);
                _threatRows.Add(hostile.Id, row);
            }
            row.Button.Visible = true;
            row.Button.Disabled = hostile.Combat.IsDefeated || route.Encounter?.Phase == EncounterPhase.Defeat;
            row.Health.MaxValue = hostile.Combat.MaximumHealth;
            row.Health.Value = hostile.Combat.Health;
            row.Button.TooltipText = $"{hostile.DisplayName} · {hostile.Combat.Health}/{hostile.Combat.MaximumHealth} HP\nAssign this target to the selected crew.";
            var target = route.Party.FirstOrDefault(actor => actor.Id == hostile.CurrentAction?.CombatTargetId);
            row.Status.Text = hostile.Combat.IsDefeated ? "NEUTRALIZED" : route.Encounter?.Phase == EncounterPhase.Defeat ? "SECURITY ACTIVE" : hostile.CurrentAction?.Phase == PrimaryActionPhase.Windup
                ? $"{target?.DisplayName} · {(_enemyViews[hostile.Id].Sentry is not null ? "fires" : "hit")} in {hostile.CurrentAction.PhaseTicksRemaining / 30.0:0.0}s"
                : hostile.CurrentAction?.Phase == PrimaryActionPhase.Recovery ? "Recovering" : "Acquiring target";
            if (hostile.Combat.TauntedBy is not null)
            {
                var strike = hostile.CurrentAction?.Phase == PrimaryActionPhase.Windup
                    ? $" · {(_enemyViews[hostile.Id].Sentry is not null ? "fires" : "hit")} {hostile.CurrentAction.PhaseTicksRemaining / 30.0:0.0}s" : " · Protector";
                row.Status.Text = $"TAUNTED {hostile.Combat.TauntRemainingTicks / 30.0:0.0}s{strike}";
            }
        }
    }
}
