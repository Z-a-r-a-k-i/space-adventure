using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private PanelContainer _objectivePanel = null!;
    private VBoxContainer _objectiveColumn = null!;
    private Label _objectiveDetail = null!;
    private string _pauseTagMode = "";
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
    private TextureRect _abilityOwnerPortrait = null!;
    private EntityId? _displayedAbilityOwner;
    private Label _sectorLabel = null!;
    private PanelContainer _outcomePanel = null!;
    private Label _outcomeTitle = null!;
    private Label _outcomeDetail = null!;
    private Label _worldControlsHint = null!;

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
        button.Pressed += () => { GameAudio.Play("ui.click"); action(); };
        return button;
    }

    private static ActionTile Tile(string key, string caption, string icon, Action action)
    {
        var tile = new ActionTile();
        tile.Build(key, caption, icon);
        tile.Pressed += () => { GameAudio.Play("ui.click"); action(); };
        return tile;
    }

    private static StyleBoxFlat PillStyle(string background, float alpha, Color border) => new()
    {
        BgColor = new Color(background, alpha), BorderColor = border,
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 17, CornerRadiusTopRight = 17, CornerRadiusBottomLeft = 17, CornerRadiusBottomRight = 17,
        ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 4, ContentMarginBottom = 4,
        ShadowColor = new Color(0, 0, 0, .3f), ShadowSize = 6,
    };

    private void CreateTacticalHud(CanvasLayer canvas)
    {
        // Top-left: compact objective tracker, with short dismissible tips stacked beneath it.
        _objectiveColumn = new VBoxContainer { Name = "ObjectiveColumn", OffsetLeft = 20, OffsetTop = 18, OffsetRight = 344,
            MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 10 };
        _objectiveColumn.AddThemeConstantOverride("separation", 8);
        canvas.AddChild(_objectiveColumn);
        _objectivePanel = new PanelContainer { Name = "ObjectiveTracker" };
        _objectivePanel.AddThemeStyleboxOverride("panel", TacticalUi.TrackerBox(TacticalUi.Cyan));
        _objectiveColumn.AddChild(_objectivePanel);
        var objectiveContent = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        objectiveContent.AddThemeConstantOverride("separation", 3);
        _objectivePanel.AddChild(objectiveContent);
        _sectorLabel = TacticalUi.Eyebrow("◆  ARRIVALS", "a0efd8");
        objectiveContent.AddChild(_sectorLabel);
        _objectiveLabel = HudLabel("", 15, "eef4f5");
        _objectiveLabel.CustomMinimumSize = new Vector2(296, 0);
        objectiveContent.AddChild(_objectiveLabel);
        _objectiveDetail = TacticalUi.Label("", 12, "9fb4ba");
        _objectiveDetail.Visible = false;
        objectiveContent.AddChild(_objectiveDetail);

        // Top-centre: nothing while exploring; a small pause control in live combat; an amber
        // TACTICAL PAUSE tag (click or Space resumes) only while the simulation is frozen.
        _pauseButton = HudButton("", () => Dispatch(new SetPauseCommand(NextHumanCommandId("pause"), !_session!.IsPaused)));
        _pauseButton.Name = "PauseTag";
        _pauseButton.CustomMinimumSize = Vector2.Zero;
        _pauseButton.AnchorLeft = _pauseButton.AnchorRight = .5f;
        _pauseButton.OffsetTop = 16; _pauseButton.OffsetBottom = 50;
        _pauseButton.Icon = TacticalUi.Icon("ship/pause");
        _pauseButton.ExpandIcon = false;
        _pauseButton.AddThemeConstantOverride("h_separation", 10);
        _pauseButton.AddThemeConstantOverride("icon_max_width", 14);
        canvas.AddChild(_pauseButton);
        _pauseLabel = HudLabel("", 11, "afc1c5");
        _pauseLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _pauseLabel.AnchorLeft = _pauseLabel.AnchorRight = .5f;
        _pauseLabel.OffsetLeft = -260; _pauseLabel.OffsetRight = 260; _pauseLabel.OffsetTop = 56;
        _pauseLabel.AddThemeColorOverride("font_shadow_color", new Color("030910"));
        _pauseLabel.AddThemeConstantOverride("shadow_offset_x", 1);
        _pauseLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        canvas.AddChild(_pauseLabel);

        _controlsButton = HudButton("F1   Manual", ToggleControls);
        _controlsButton.CustomMinimumSize = new Vector2(104, 30);
        _controlsButton.AddThemeFontSizeOverride("font_size", 12);
        _controlsButton.AddThemeStyleboxOverride("normal", PillStyle("0a141b", .7f, new Color("36534f")));
        _controlsButton.AddThemeColorOverride("font_color", new Color("afc1c5"));
        _controlsButton.AnchorLeft = _controlsButton.AnchorRight = 1;
        _controlsButton.OffsetLeft = -124; _controlsButton.OffsetRight = -20; _controlsButton.OffsetTop = 18;
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
        var focusHeading = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        focusHeading.AddThemeConstantOverride("separation", 9);
        _abilityOwnerPortrait = new TextureRect { CustomMinimumSize = new Vector2(28, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        focusHeading.AddChild(_abilityOwnerPortrait);
        _abilityOwnerLabel = TacticalUi.Label("01  VANGUARD / ABILITIES", 12, "a0efd8");
        _abilityOwnerLabel.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        focusHeading.AddChild(_abilityOwnerLabel);
        actionColumn.AddChild(focusHeading);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        actionColumn.AddChild(actions);
        _abilityButton = Tile("1", "Interrupt", "suppression", () => BeginAbilityTargeting());
        _secondaryAbilityButton = Tile("2", "Burst", "burst", () => BeginAbilityTargeting(1));
        _stopButton = Tile("X", "Stop", "stop", StopSelectedActors);
        _stopButton.TooltipText = "Stop the selected crew. While paused, this replaces their pending orders and takes effect on resume.";
        actions.AddChild(_abilityButton); actions.AddChild(_secondaryAbilityButton); actions.AddChild(_stopButton);
        _combatLabel = HudLabel("", 12, "afc1c5");
        _combatLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _combatLabel.AutowrapMode = TextServer.AutowrapMode.Off;
        actionColumn.AddChild(_combatLabel);
        // Transient feedback: a slim translucent strip above the bottom edge, not a panel.
        _feedbackLabel = HudLabel("", 13, "dce6e9");
        var feedbackStyle = new StyleBoxFlat { BgColor = new Color("0a141b", .72f), BorderColor = new Color(TacticalUi.Cyan, .7f), BorderWidthBottom = 1,
            ContentMarginLeft = 16, ContentMarginRight = 16, ContentMarginTop = 7, ContentMarginBottom = 7 };
        _feedbackLabel.AddThemeStyleboxOverride("normal", feedbackStyle);
        _feedbackLabel.AddThemeColorOverride("font_shadow_color", new Color("030910"));
        _feedbackLabel.AddThemeConstantOverride("shadow_offset_y", 1);
        _feedbackLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _feedbackLabel.VerticalAlignment = VerticalAlignment.Center;
        _feedbackLabel.AnchorTop = _feedbackLabel.AnchorBottom = 1;
        _feedbackLabel.AnchorRight = 1;
        _feedbackLabel.OffsetLeft = 420; _feedbackLabel.OffsetRight = -460;
        _feedbackLabel.OffsetTop = -92; _feedbackLabel.OffsetBottom = -52;
        canvas.AddChild(_feedbackLabel);
        var help = _worldControlsHint = TacticalUi.Label("RMB  Order    DRAG  Select    TAB  Ability focus    SPACE  Pause", 11, "afc1c5");
        help.Modulate = new Color(1, 1, 1, .6f);
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
        CreateAbilityContext(canvas);
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
                var portrait = CrewPortrait(actor.Id);
                card.Build($"res://ui/portraits/{portrait}.png");
                var id = actor.Id;
                card.Pressed += () => { if (_abilityTargeting && IsHealingAbility(_targetAbilityId)) { ConfirmEntityAbility(id); } else { SelectActor(id, Input.IsKeyPressed(Key.Shift)); } };
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
                AttackTargetName(route, actor.CurrentAction), CrewAccent(route, actor), CrewNumber(route, actor.Id));
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
        action?.CombatTargetId is not null ? route.Party.FirstOrDefault(crew => crew.Id == action.CombatTargetId)?.DisplayName ?? route.VisibleHostiles.FirstOrDefault(hostile => hostile.Id == action.CombatTargetId)?.DisplayName ?? "" : "";

    private static int CrewNumber(StationRouteObservation route, EntityId id) =>
        route.Party.Select((actor, index) => (actor, index)).First(item => item.actor.Id == id).index + 1;

    private static Color CrewAccent(StationRouteObservation route, ActorObservation actor) =>
        actor.Id == route.Protagonist.Id ? TacticalUi.Cyan : actor.Id.Value == "actor.companion.medic" ? MedicAccent : TacticalUi.Protector;

    private string ShortAction(PrimaryActionObservation? action) => action?.Kind switch
    {
        PrimaryActionKind.Attack => action.Phase == PrimaryActionPhase.Moving ? "Closing range" : action.Phase == PrimaryActionPhase.Windup ? "Firing" : "Recovering",
        PrimaryActionKind.Ability => action.AbilityId == _definition!.Combat.Barrier.Id ? "Deploying barrier"
            : action.AbilityId == _definition.Combat.Taunt.Id ? "Taunt"
            : action.AbilityId == _definition.Combat.Burst.Id ? "Burst fire" : IsHealingAbility(action.AbilityId) ? "Healing" : IsHealingField(action.AbilityId) ? "Deploying healing field" : "Interrupt",
        PrimaryActionKind.Move => "Moving",
        PrimaryActionKind.Interact => "Interacting", PrimaryActionKind.Stop => "Stop", _ => "Ready",
    };

    private void UpdateTacticalHud(GameObservation observation, StationRouteObservation route, ActorObservation actor)
    {
        var combat = actor.Combat!;
        var encounter = route.Encounter!;
        var active = encounter.Phase is EncounterPhase.Active or EncounterPhase.Readying;
        var accent = CrewAccent(route, actor);
        _abilityOwnerLabel.Text = $"{CrewNumber(route, actor.Id):00}  {actor.DisplayName.ToUpperInvariant()} / ABILITIES";
        _abilityOwnerLabel.AddThemeColorOverride("font_color", accent);
        _abilityButton.SetAccent(accent);
        _secondaryAbilityButton.SetAccent(accent);
        if (_displayedAbilityOwner != actor.Id)
        {
            _abilityOwnerPortrait.Texture = ResourceLoader.Load<Texture2D>($"res://ui/portraits/{(CrewPortrait(actor.Id))}.png");
            _actionPanel.AddThemeStyleboxOverride("panel", TacticalUi.FieldPanel(accent, bottom: true));
            _displayedAbilityOwner = actor.Id;
        }
        _sectorLabel.Text = "◆  " + CurrentSector(route);
        _outcomePanel.Visible = encounter.Phase == EncounterPhase.Defeat;
        _outcomeTitle.Text = "CREW LOST";
        _outcomeTitle.AddThemeColorOverride("font_color", TacticalUi.Danger);
        _outcomeDetail.Text = "Regroup and try again. Your route progress is safe.";
        var cooldown = combat.Cooldowns.FirstOrDefault(value => value.AbilityId == actor.Loadout?.ActiveAbilityId);
        var secondaryCooldown = combat.Cooldowns.FirstOrDefault(value => value.AbilityId == actor.Loadout?.SecondaryAbilityId);
        var barrierAbility = actor.Loadout?.ActiveAbilityTargetKind == AbilityTargetKind.Barrier;
        var selectedLiving = SelectedLivingActors(route).ToArray();
        var ready = !combat.IsDefeated && active && route.ActiveDialogue is null;
        _abilityButton.Disabled = !ready || cooldown?.RemainingTicks > 0;
        _abilityButton.SetState(actor.Loadout!.ActiveAbilityName, IsHealingAbility(actor.Loadout.ActiveAbilityId) ? "heal" : barrierAbility ? "guard" : "suppression",
            combat.IsDefeated ? "Down" : cooldown?.RemainingTicks > 0 ? $"{cooldown.RemainingTicks / 30.0:0.0}s" : _abilityTargeting && _targetAbilityId == actor.Loadout?.ActiveAbilityId ? barrierAbility ? "Place barrier" : "Aim + confirm" : actor.PendingAction?.AbilityId == actor.Loadout?.ActiveAbilityId ? "Queued" : active ? "Ready" : "In combat",
            cooldown is { TotalTicks: > 0 } ? 1 - (double)cooldown.RemainingTicks / cooldown.TotalTicks : 1);
        _secondaryAbilityButton.Disabled = !ready || secondaryCooldown?.RemainingTicks > 0;
        _secondaryAbilityButton.SetState(actor.Loadout!.SecondaryAbilityName, IsHealingField(actor.Loadout.SecondaryAbilityId) ? "healing_field" : barrierAbility ? "taunt" : "burst",
            combat.IsDefeated ? "Down" : secondaryCooldown?.RemainingTicks > 0 ? $"{secondaryCooldown.RemainingTicks / 30.0:0.0}s"
                : _abilityTargeting && _targetAbilityId == actor.Loadout?.SecondaryAbilityId ? IsHealingField(_targetAbilityId) ? "Place field" : "Pick enemy" : actor.PendingAction?.AbilityId == actor.Loadout?.SecondaryAbilityId ? "Queued" : active ? "Ready" : "In combat",
            secondaryCooldown is { TotalTicks: > 0 } ? 1 - (double)secondaryCooldown.RemainingTicks / secondaryCooldown.TotalTicks : 1);
        _stopButton.Disabled = selectedLiving.Length == 0 || encounter.Phase is EncounterPhase.Defeat or EncounterPhase.Securing || route.ActiveDialogue is not null;
        _abilityButton.Visible = _secondaryAbilityButton.Visible = _stopButton.Visible = encounter.Phase != EncounterPhase.Defeat;
        _stopButton.SetState("Stop", "stop", selectedLiving.Length > 1 ? $"{selectedLiving.Length} crew" : selectedLiving.FirstOrDefault()?.DisplayName ?? "No crew", 1);
        UpdatePauseTag(observation, route, encounter, active);
        _selectionCountLabel.Text = $"{selectedLiving.Length} selected";
        var target = route.VisibleHostiles.FirstOrDefault(hostile => hostile.Id == combat.RememberedAttackTargetId);
        _combatLabel.Text = target is null ? "Awaiting target order" : $"Target · {target.DisplayName.Replace("Security ", "", StringComparison.Ordinal)}";
        if (barrierAbility && encounter.Barrier is { } barrier) { _combatLabel.Text = $"Barrier deployed · {barrier.RemainingTicks / 30.0:0.0}s"; }
        if (_abilityTargeting) { _combatLabel.Text = IsHealingAbility(_targetAbilityId) ? "Choose an ally or portrait · Esc cancels" : _targetAbilityKind == AbilityTargetKind.Entity ? "Choose an enemy · Esc cancels" : "Choose a ground position · Esc cancels"; }
        else if (actor.PendingAction is { } pending) { _combatLabel.Text = $"NEXT · {PendingOrderText(route, pending)}"; }
        if (combat.IsDefeated) { _combatLabel.Text = "Select a living crew member"; }
        _objectiveLabel.Text = encounter.Phase == EncounterPhase.Securing ? "Threats neutralized" : route.Objective.Text;
        UpdateObjectiveDetail(route, encounter);
        _crewCluster.Visible = route.ActiveDialogue is null;
        _actionPanel.Visible = route.ActiveDialogue is null && encounter.Phase != EncounterPhase.Defeat;
        _controlsButton.Visible = route.ActiveDialogue is null;
        if (_reviewDrivesClock && _reviewMode == "record") { _pauseLabel.Text = "COMBAT REVIEW"; _pauseLabel.Visible = true; }
    }

    private void UpdatePauseTag(GameObservation observation, StationRouteObservation route, EncounterObservation encounter, bool active)
    {
        var dialogue = route.ActiveDialogue is not null;
        var mode = dialogue || encounter.Phase == EncounterPhase.Defeat ? "hidden"
            : observation.Paused ? "paused" : active ? "live" : "hidden";
        if (mode != _pauseTagMode)
        {
            _pauseTagMode = mode;
            var paused = mode == "paused";
            _pauseButton.Text = paused ? "TACTICAL PAUSE" : "";
            _pauseButton.TooltipText = paused ? "Resume (Space)" : "Pause (Space)";
            var halfWidth = paused ? 118 : 20;
            _pauseButton.OffsetLeft = -halfWidth; _pauseButton.OffsetRight = halfWidth;
            var accent = new Color("ffc45c");
            _pauseButton.AddThemeStyleboxOverride("normal", paused ? PillStyle("2a2110", .92f, accent) : PillStyle("0a141b", .6f, new Color("36534f")));
            _pauseButton.AddThemeStyleboxOverride("hover", paused ? PillStyle("3a2d12", .95f, accent) : PillStyle("16262c", .8f, TacticalUi.Cyan));
            _pauseButton.AddThemeStyleboxOverride("pressed", PillStyle("3a2d12", .95f, accent));
            _pauseButton.AddThemeColorOverride("font_color", paused ? accent : TacticalUi.Muted);
            _pauseButton.AddThemeColorOverride("font_hover_color", paused ? Colors.White : TacticalUi.Cyan);
            _pauseButton.AddThemeColorOverride("icon_normal_color", paused ? accent : TacticalUi.Muted);
            _pauseButton.AddThemeColorOverride("icon_hover_color", paused ? Colors.White : TacticalUi.Cyan);
            _pauseButton.AddThemeFontSizeOverride("font_size", 13);
        }
        _pauseButton.Visible = mode != "hidden";
        _pauseFrame.Shown = mode == "paused";
        // One line under the tag: who noticed whom when a fight begins, then how to resume.
        var spotter = encounter.SpotterId is { } id ? route.VisibleHostiles.FirstOrDefault(hostile => hostile.Id == id) : null;
        var spotted = route.Party.FirstOrDefault(actor => actor.Id == encounter.SpottedActorId);
        _pauseLabel.Text = encounter.Phase == EncounterPhase.Victory && !observation.Paused ? "AREA SECURED  ·  CREW RECOVERED"
            : mode != "paused" ? ""
            : encounter.Phase == EncounterPhase.Readying && encounter.Attempt == 1 && spotter is not null && spotted is not null
                ? $"CONTACT  ·  {spotter.DisplayName.Replace("Security ", "", StringComparison.Ordinal)} spotted {spotted.DisplayName}  ·  plan, then SPACE"
                : "SPACE  resume  ·  orders take effect when time runs";
        _pauseLabel.Visible = !dialogue && _pauseLabel.Text.Length > 0;
    }

    private void UpdateObjectiveDetail(StationRouteObservation route, EncounterObservation encounter)
    {
        var detail = "";
        if (encounter.Phase is EncounterPhase.Readying or EncounterPhase.Active && route.Hostiles is { } hostiles)
        {
            var remaining = hostiles.Count(hostile => !hostile.Combat.IsDefeated);
            detail = $"Hostiles remaining  {remaining} / {hostiles.Count}";
        }
        else if (route.VisibleHostiles.Count(enemy => enemy.EncounterPhase == EncounterPhase.Dormant && !enemy.Combat.IsDefeated) is > 0 and var sighted)
        {
            detail = sighted == 1 ? "1 hostile sighted  ·  not yet alerted" : $"{sighted} hostiles sighted  ·  not yet alerted";
        }
        _objectiveDetail.Text = detail;
        _objectiveDetail.Visible = detail.Length > 0;
    }
}
