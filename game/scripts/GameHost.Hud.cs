using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private PanelContainer _objectivePanel = null!;
    private VBoxContainer _crewCluster = null!;
    private PanelContainer _actionPanel = null!;
    private Label _selectionCountLabel = null!;
    private Button _controlsButton = null!;
    private ActionTile _abilityButton = null!;
    private ActionTile _secondaryAbilityButton = null!;
    private ActionTile _stopButton = null!;
    private Button _pauseButton = null!;
    private EntityId? _focusedActorId;
    private EntityId? _abilityOwnerId;
    private Label _abilityOwnerLabel = null!;
    private Label _sectorLabel = null!;
    private PanelContainer _outcomePanel = null!;
    private Label _outcomeTitle = null!;
    private Label _outcomeDetail = null!;

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
        _objectivePanel = HudPanel();
        _objectivePanel.AddThemeStyleboxOverride("panel", TacticalUi.FieldPanel(TacticalUi.Cyan));
        _objectivePanel.OffsetLeft = 20; _objectivePanel.OffsetTop = 20; _objectivePanel.OffsetRight = 360;
        canvas.AddChild(_objectivePanel);
        var objectiveContent = new VBoxContainer();
        objectiveContent.AddThemeConstantOverride("separation", 7);
        _objectivePanel.AddChild(objectiveContent);
        _sectorLabel = TacticalUi.Eyebrow("FRONTIER STATION / ARRIVALS", "a0efd8");
        objectiveContent.AddChild(_sectorLabel);
        _objectiveLabel = HudLabel("", 18);
        _objectiveLabel.CustomMinimumSize = new Vector2(312, 42);
        objectiveContent.AddChild(_objectiveLabel);

        _pauseButton = HudButton("SPACE   Pause", () => Dispatch(new SetPauseCommand(NextHumanCommandId("pause"), !_session!.IsPaused)));
        _pauseButton.AddThemeStyleboxOverride("normal", TacticalUi.FieldPanel(TacticalUi.Amber, bottom: true));
        _pauseButton.AnchorLeft = _pauseButton.AnchorRight = .5f;
        _pauseButton.OffsetLeft = -110; _pauseButton.OffsetRight = 110; _pauseButton.OffsetTop = 20;
        canvas.AddChild(_pauseButton);
        _pauseLabel = HudLabel("", 11, "afc1c5");
        _pauseLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _pauseLabel.AnchorLeft = _pauseLabel.AnchorRight = .5f;
        _pauseLabel.OffsetLeft = -180; _pauseLabel.OffsetRight = 180; _pauseLabel.OffsetTop = 64;
        canvas.AddChild(_pauseLabel);

        _controlsButton = HudButton("F1   Field manual", ToggleControls);
        _controlsButton.CustomMinimumSize = new Vector2(138, 34);
        _controlsButton.AddThemeFontSizeOverride("font_size", 12);
        _controlsButton.AnchorLeft = _controlsButton.AnchorRight = 1;
        _controlsButton.OffsetLeft = -158; _controlsButton.OffsetRight = -20; _controlsButton.OffsetTop = 20;
        canvas.AddChild(_controlsButton);

        _crewCluster = new VBoxContainer { AnchorTop = 1, AnchorBottom = 1, OffsetLeft = 20,
            OffsetRight = 304, OffsetTop = -42, OffsetBottom = -42, GrowVertical = Control.GrowDirection.Begin,
            MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 10 };
        _crewCluster.AddThemeConstantOverride("separation", 7);
        canvas.AddChild(_crewCluster);
        var crewHeading = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        crewHeading.AddChild(TacticalUi.Eyebrow("CREW"));
        _selectionCountLabel = TacticalUi.Label("1 selected", 11, "a0efd8");
        _selectionCountLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        _selectionCountLabel.HorizontalAlignment = HorizontalAlignment.Right;
        crewHeading.AddChild(_selectionCountLabel);
        _crewCluster.AddChild(crewHeading);
        _partyList = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        _partyList.AddThemeConstantOverride("separation", 6);
        _crewCluster.AddChild(_partyList);

        _actionPanel = HudPanel();
        _actionPanel.AddThemeStyleboxOverride("panel", TacticalUi.FieldPanel(TacticalUi.Cyan, bottom: true));
        _actionPanel.AnchorLeft = _actionPanel.AnchorRight = _actionPanel.AnchorTop = _actionPanel.AnchorBottom = 1;
        _actionPanel.OffsetLeft = -342; _actionPanel.OffsetRight = -20;
        _actionPanel.OffsetTop = _actionPanel.OffsetBottom = -42;
        _actionPanel.GrowVertical = Control.GrowDirection.Begin;
        canvas.AddChild(_actionPanel);
        var actionColumn = new VBoxContainer();
        actionColumn.AddThemeConstantOverride("separation", 7);
        _actionPanel.AddChild(actionColumn);
        _abilityOwnerLabel = TacticalUi.Eyebrow("VANGUARD / ABILITY FOCUS", "a0efd8");
        actionColumn.AddChild(_abilityOwnerLabel);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        actionColumn.AddChild(actions);
        _abilityButton = Tile("1", "Interrupt", "suppression", () => BeginAbilityTargeting());
        _secondaryAbilityButton = Tile("2", "Burst", "burst", () => BeginAbilityTargeting(1));
        _stopButton = Tile("X", "Stop", "stop", StopSelectedActors);
        _stopButton.TooltipText = "Stop the selected crew. While paused, this replaces their pending orders and takes effect on resume.";
        actions.AddChild(_abilityButton); actions.AddChild(_secondaryAbilityButton); actions.AddChild(_stopButton);
        _combatLabel = HudLabel("", 11, "afc1c5");
        _combatLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _combatLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        actionColumn.AddChild(_combatLabel);
        _feedbackLabel = HudLabel("", 13, "dce6e9");
        _feedbackLabel.AddThemeStyleboxOverride("normal", TacticalUi.FieldPanel(TacticalUi.Cyan, margin: 10));
        _feedbackLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _feedbackLabel.AnchorTop = _feedbackLabel.AnchorBottom = 1;
        _feedbackLabel.AnchorRight = 1;
        _feedbackLabel.OffsetLeft = 330; _feedbackLabel.OffsetRight = -370;
        _feedbackLabel.OffsetTop = -99; _feedbackLabel.OffsetBottom = -43;
        canvas.AddChild(_feedbackLabel);
        var help = TacticalUi.Label("RMB  Order    DRAG  Select    TAB  Ability focus", 11, "afc1c5");
        help.AnchorTop = help.AnchorBottom = 1;
        help.OffsetLeft = 20; help.OffsetTop = -27;
        canvas.AddChild(help);

        _outcomePanel = HudPanel();
        _outcomePanel.AddThemeStyleboxOverride("panel", TacticalUi.FieldPanel(TacticalUi.Cyan, bottom: true, margin: 20));
        _outcomePanel.MouseFilter = Control.MouseFilterEnum.Ignore;
        _outcomePanel.AnchorLeft = _outcomePanel.AnchorRight = .5f;
        _outcomePanel.OffsetLeft = -240; _outcomePanel.OffsetRight = 240; _outcomePanel.OffsetTop = 116;
        _outcomePanel.Visible = false;
        canvas.AddChild(_outcomePanel);
        var outcome = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        outcome.AddThemeConstantOverride("separation", 9);
        _outcomePanel.AddChild(outcome);
        _outcomeTitle = HudLabel("", 24, "a0efd8");
        _outcomeDetail = HudLabel("", 14, "afc1c5");
        _outcomeTitle.HorizontalAlignment = _outcomeDetail.HorizontalAlignment = HorizontalAlignment.Center;
        outcome.AddChild(_outcomeTitle); outcome.AddChild(_outcomeDetail);
        _retryButton = HudButton("ENTER   Retry fight", RestartEncounter);
        _retryButton.FocusMode = Control.FocusModeEnum.All;
        _retryButton.Visible = false;
        outcome.AddChild(_retryButton);
        CreateFieldOrderOverlay(canvas);
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
            if (current == "Ready") { current = "Awaiting orders"; }
            _partyButtons[actor.Id.Value].Synchronize(actor, _selectedActorIds.Contains(actor.Id), actor.Id == _focusedActorId,
                current, actor.PendingAction is null ? "" : PendingOrderText(route, actor.PendingAction),
                AttackTargetName(route, actor.CurrentAction), TacticalUi.Cyan);
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
        PrimaryActionKind.Interact => "Interacting", PrimaryActionKind.Stop => "Stop", _ => "Ready",
    };

    private void UpdateTacticalHud(GameObservation observation, StationRouteObservation route, ActorObservation actor)
    {
        var combat = actor.Combat!;
        var encounter = route.Encounter!;
        var active = encounter.Phase is EncounterPhase.Active or EncounterPhase.Readying;
        _abilityOwnerLabel.Text = $"{actor.DisplayName.ToUpperInvariant()} / ABILITY FOCUS";
        _abilityOwnerLabel.AddThemeColorOverride("font_color", TacticalUi.Cyan);
        _sectorLabel.Text = "FRONTIER STATION  /  " + (encounter.Id == _definition!.Combat.PartyEncounter.Id ? "TRANSIT HALL"
            : route.Party.Count > 1 ? "CREW ACCESS" : encounter.Phase == EncounterPhase.Dormant ? "ARRIVALS" : "SECURITY");
        _outcomePanel.Visible = encounter.Phase == EncounterPhase.Defeat || encounter.Phase == EncounterPhase.Victory
            && (encounter.Id == _definition.Combat.PartyEncounter.Id || route.Objective.Id == _definition.SoloExitDoorObjective.Id);
        _outcomeTitle.Text = encounter.Phase == EncounterPhase.Defeat ? "CREW LOST" : "AREA SECURED";
        _outcomeTitle.AddThemeColorOverride("font_color", encounter.Phase == EncounterPhase.Defeat ? TacticalUi.Danger : TacticalUi.Cyan);
        _outcomeDetail.Text = encounter.Phase == EncounterPhase.Defeat ? "Regroup and try again. Your route progress is safe."
            : encounter.Id == _definition.Combat.PartyEncounter.Id ? "End of the playable chapter. Thanks for playing."
            : "The service exit is unlocked. Find the other survivor.";
        var cooldown = combat.Cooldowns.FirstOrDefault(value => value.AbilityId == actor.Loadout?.ActiveAbilityId);
        var secondaryCooldown = combat.Cooldowns.FirstOrDefault(value => value.AbilityId == actor.Loadout?.SecondaryAbilityId);
        var barrierAbility = actor.Loadout?.ActiveAbilityTargetKind == AbilityTargetKind.Barrier;
        var selectedLiving = SelectedLivingActors(route).ToArray();
        var ready = !combat.IsDefeated && active && route.ActiveDialogue is null;
        _abilityButton.Disabled = !ready || cooldown?.RemainingTicks > 0;
        _abilityButton.SetState(barrierAbility ? "Barrier" : "Interrupt", barrierAbility ? "guard" : "suppression",
            combat.IsDefeated ? "Down" : cooldown?.RemainingTicks > 0 ? $"{cooldown.RemainingTicks / 30.0:0.0}s" : _abilityTargeting && _targetAbilityId == actor.Loadout?.ActiveAbilityId ? barrierAbility ? "Place barrier" : "Aim + confirm" : active ? "Ready" : "In combat",
            cooldown is { TotalTicks: > 0 } ? 1 - (double)cooldown.RemainingTicks / cooldown.TotalTicks : 1);
        _abilityButton.TooltipText = barrierAbility
            ? $"Barrier · click to place within {_definition!.Combat.Barrier.RangeMeters:0.#}m. Faces from Protector toward the placement point; uses his current facing at his feet. Stays fixed for {_definition.Combat.Barrier.DurationTicks / 30.0:0.#}s."
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
        _stopButton.SetState("Stop", "stop", selectedLiving.Length > 1 ? "Both crew" : selectedLiving.FirstOrDefault()?.DisplayName ?? "No crew", 1);
        _pauseButton.Disabled = encounter.Phase == EncounterPhase.Defeat || route.ActiveDialogue is not null;
        _pauseButton.Text = encounter.Phase == EncounterPhase.Defeat ? "ENCOUNTER LOST"
            : observation.Paused ? "SPACE   Resume" : "SPACE   Pause";
        _pauseLabel.Text = encounter.Phase == EncounterPhase.Defeat ? "ENTER · RETRY FIGHT"
            : encounter.Phase == EncounterPhase.Victory && encounter.Id == _definition.Combat.PartyEncounter.Id ? "ENCOUNTER COMPLETE"
            : observation.Paused ? "TACTICAL PAUSE · PLAN YOUR ORDERS" : active ? "LIVE COMBAT" : "EXPLORATION";
        _selectionCountLabel.Text = $"{selectedLiving.Length} selected";
        var target = route.Hostiles?.FirstOrDefault(hostile => hostile.Id == combat.RememberedAttackTargetId);
        _combatLabel.Text = target is null ? "Awaiting target order" : $"Target · {target.DisplayName.Replace("Security ", "", StringComparison.Ordinal)}";
        if (barrierAbility && encounter.Barrier is { } barrier) { _combatLabel.Text = $"Barrier deployed · {barrier.RemainingTicks / 30.0:0.0}s"; }
        if (_abilityTargeting) { _combatLabel.Text = _targetAbilityKind == AbilityTargetKind.Entity ? "Choose an enemy · Esc cancels" : "Choose a ground position · Esc cancels"; }
        if (combat.IsDefeated) { _combatLabel.Text = "Select a living crew member"; }
        _objectiveLabel.Text = encounter.Phase == EncounterPhase.Securing ? "Threats neutralized"
            : encounter.Id == _definition.Combat.PartyEncounter.Id && encounter.Phase == EncounterPhase.Victory ? "Transit hall secured" : route.Objective.Text;
        _crewCluster.Visible = route.ActiveDialogue is null;
        _actionPanel.Visible = route.ActiveDialogue is null && encounter.Phase != EncounterPhase.Defeat;
        _pauseButton.Visible = _pauseLabel.Visible = route.ActiveDialogue is null;
        _controlsButton.Visible = route.ActiveDialogue is null;
        if (_reviewDrivesClock && _reviewMode == "record") { _pauseLabel.Text = "COMBAT REVIEW"; }
    }
}
