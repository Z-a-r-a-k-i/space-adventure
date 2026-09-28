using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

/// <summary>
/// Controller play in the station. The left stick walks the selected crew member; the rest of the selected squad
/// follows with the same group move a click issues, aimed just behind the walker. While paused the stick drags a
/// planned destination that is queued on release. With no pointer, one enemy, ally or object is highlighted at a
/// time (the D-pad cycles it) and A acts on it. Every action submits the ordinary typed commands.
/// </summary>
public partial class GameHost
{
    private const float PadStickDeadzone = .22f;
    private const float PadWalkLookaheadMeters = 1.3f;
    private const float PadFollowDistanceMeters = 1.5f;
    private const double PadWalkRefreshSeconds = .15;
    private const float PadPlanSpeed = 7f;
    private const float PadAimSpeed = 6f;
    private const float PadInteractRangeMeters = 4.5f;
    private const float PadMaximumSnapMeters = .6f;

    private EntityId? _padHighlight;
    private Vector3? _padAim;
    private Vector3? _padPlan;
    private Vector3? _padPointerWorld;
    private bool _padPlanCancelled;
    private bool _padWalking;
    private Vector3 _padWalkDirection;
    private double _padWalkClock;
    private bool _padRightTriggerDown;
    private bool _padLookAround;
    private double _padFollowSeconds;
    private int _padPromptGeneration = -1;
    private MeshInstance3D _padHighlightRing = null!;
    private StandardMaterial3D _padHighlightMaterial = null!;
    private MeshInstance3D _padPlanMarker = null!;
    private PanelContainer _padPrompt = null!;
    private RichTextLabel _padPromptText = null!;

    private void CreateGamepadPresentation(CanvasLayer canvas)
    {
        _padHighlightMaterial = CreateCombatEffectMaterial(new Color(TacticalUi.Danger, .9f));
        _padHighlightMaterial.NoDepthTest = true;
        _padHighlightRing = new MeshInstance3D { Name = "PadHighlight", Visible = false, MaterialOverride = _padHighlightMaterial,
            Mesh = new TorusMesh { InnerRadius = .56f, OuterRadius = .64f, Rings = 40, RingSegments = 4 },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_padHighlightRing);
        var planMaterial = CreateCombatEffectMaterial(new Color(TacticalUi.Cyan, .85f));
        planMaterial.NoDepthTest = true;
        _padPlanMarker = new MeshInstance3D { Name = "PadPlannedMove", Visible = false, MaterialOverride = planMaterial,
            Mesh = new TorusMesh { InnerRadius = .3f, OuterRadius = .38f, Rings = 32, RingSegments = 4 },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(_padPlanMarker);
        _padPrompt = new PanelContainer { Name = "PadPrompt", Visible = false, ZIndex = 11, MouseFilter = Control.MouseFilterEnum.Ignore };
        _padPrompt.AddThemeStyleboxOverride("panel", TacticalUi.TrackerBox(TacticalUi.Cyan, horizontal: 9, vertical: 4));
        _padPromptText = TacticalUi.RichLabel("", 13, "e1e8eb");
        _padPrompt.AddChild(_padPromptText);
        canvas.AddChild(_padPrompt);
    }

    private static string WorldControlsHintText()
    {
        if (!InputDevice.UsingGamepad) { return "RMB  Order    DRAG  Select    TAB  Ability focus    SPACE  Pause"; }
        static string G(PadButton button) => InputPrompts.Bb(button, 16);
        return $"{G(PadButton.LS)} Walk    {G(PadButton.A)} Act    {G(PadButton.LB)}{G(PadButton.RB)} Crew    "
            + $"{G(PadButton.DpadUp)} Squad    {G(PadButton.RT)} Pause    {G(PadButton.Menu)} Manual";
    }

    /// <summary>Rewrites static prompts after a device switch; dynamic lines pick their wording as they update.</summary>
    private void ApplyInputPrompts()
    {
        _padPromptGeneration = InputDevice.Generation;
        var pad = InputDevice.UsingGamepad;
        _worldControlsHint.Text = WorldControlsHintText();
        _dialogueHint.Text = DialogueHintText();
        _abilityButton.ShowPadGlyph(pad ? PadButton.X : null);
        _secondaryAbilityButton.ShowPadGlyph(pad ? PadButton.Y : null);
        _stopButton.ShowPadGlyph(pad ? PadButton.DpadDown : null);
        SetPromptButton(_controlsButton, "F1   Manual", "Manual", PadButton.Menu);
        SetPromptButton(_retryButton, "ENTER   Retry fight", "Retry fight", PadButton.A);
        ApplyManualPrompts();
    }

    private static void SetPromptButton(Button button, string keyboard, string pad, PadButton glyph)
    {
        var usingPad = InputDevice.UsingGamepad;
        button.Text = usingPad ? pad : keyboard;
        button.Icon = usingPad ? InputPrompts.Glyph(glyph) : null;
        button.ExpandIcon = false;
        button.AddThemeConstantOverride("icon_max_width", 18);
        foreach (var state in new[] { "icon_normal_color", "icon_hover_color", "icon_pressed_color", "icon_focus_color" })
        { button.AddThemeColorOverride(state, InputPrompts.Tint(glyph)); }
    }

    private static Vector2 PadStick(JoyAxis x, JoyAxis y)
    {
        var raw = new Vector2(Input.GetJoyAxis(InputDevice.Device, x), Input.GetJoyAxis(InputDevice.Device, y));
        var length = raw.Length();
        if (length < PadStickDeadzone) { return Vector2.Zero; }
        // Rescale past the dead zone so speed starts at zero and reaches full at the rim.
        return raw / length * Mathf.Clamp((length - PadStickDeadzone) / (1 - PadStickDeadzone), 0, 1);
    }

    /// <summary>Camera-relative floor direction: pushing up walks away from the camera.</summary>
    private Vector3 PadWorldDirection(Vector2 stick)
    {
        var (forward, right) = _camera.GetPanBasis();
        return right * stick.X - forward * stick.Y;
    }

    private void ProcessGamepad(double delta)
    {
        // Consume the latch every frame, so a disconnect while the route is still loading cannot pause its first frame.
        var disconnected = InputDevice.ConsumeDisconnect();
        if (_session is null || _visualCaptureRequested) { return; }
        if (_padPromptGeneration != InputDevice.Generation) { ApplyInputPrompts(); }
        var seconds = (float)delta;
        if (disconnected && !_session.IsPaused && !_session.IsStationRouteCompleted)
        {
            Dispatch(new SetPauseCommand(NextHumanCommandId("pad.disconnect"), true));
            SetFeedback("Controller disconnected · tactical pause engaged.", TacticalUi.Amber);
        }
        if (!InputDevice.UsingGamepad) { ClearPadState(); return; }
        var observation = _session.Observe();
        if (observation.StationRoute is not { } route) { return; }
        if (_controlsOverlay.Visible)
        {
            var scroll = PadStick(JoyAxis.RightX, JoyAxis.RightY).Y;
            if (scroll != 0) { _manualScroll.ScrollVertical += (int)(scroll * 900 * seconds); }
            ClearPadState();
            return;
        }
        if (route.Phase == ScenarioPhase.Completed || route.ActiveDialogue is not null || _departureStarted) { ClearPadState(); return; }
        var leader = FocusedActor(route);
        if (_abilityTargeting && _padAim is null && _targetAbilityKind is AbilityTargetKind.Position or AbilityTargetKind.Barrier)
        { BeginPadAim(route); }
        UpdatePadHighlight(route, leader);
        var stick = PadStick(JoyAxis.LeftX, JoyAxis.LeftY);
        var lookAround = Input.GetJoyAxis(InputDevice.Device, JoyAxis.TriggerLeft) > .4f;
        // A trigger release swallowed by the manual or dialogue must not leave the next RT pull ignored.
        if (Input.GetJoyAxis(InputDevice.Device, JoyAxis.TriggerRight) < .25f) { _padRightTriggerDown = false; }
        if (_padLookAround && !lookAround) { _padFollowSeconds = 1.2; }
        _padLookAround = lookAround;
        if (lookAround)
        {
            if (_padWalking) { StopPadWalk(route); }
            if (stick != Vector2.Zero) { _camera.PanBy(PadWorldDirection(stick) * (5.5f + _camera.DistanceMeters * .2f) * seconds); }
        }
        else if (_abilityTargeting && _padAim is { } aim) { MovePadAim(route, aim, stick, seconds); }
        else if (_abilityTargeting || leader.Combat?.IsDefeated == true) { if (_padWalking) { StopPadWalk(route); } }
        else if (observation.Paused) { PlanPadMove(route, leader, stick, seconds); }
        else { WalkPad(route, leader, stick, delta); }
        FollowPadSubject(leader, seconds);
        UpdatePadPresentation(route);
    }

    private void ClearPadState()
    {
        _padWalking = false;
        _padPlan = null;
        _padAim = null;
        _padPointerWorld = null;
        _padLookAround = false;
        if (_padPrompt is null) { return; }
        _padPrompt.Visible = _padHighlightRing.Visible = _padPlanMarker.Visible = false;
    }

    private void FollowPadSubject(ActorObservation leader, float seconds)
    {
        _padFollowSeconds = Math.Max(0, _padFollowSeconds - seconds);
        if (_padLookAround || _camera.IsGliding) { return; }
        var subject = _padAim ?? _padPlan;
        if (subject is null && (_padWalking || _padFollowSeconds > 0)) { subject = _actorViews[leader.Id.Value].GlobalPosition; }
        if (subject is { } point) { _camera.KeepInView(point, seconds); }
    }

    private ActorObservation[] PadFollowers(StationRouteObservation route, ActorObservation leader) =>
        SelectedLivingActors(route).Where(actor => actor.Id != leader.Id).ToArray();

    private void WalkPad(StationRouteObservation route, ActorObservation leader, Vector2 stick, double delta)
    {
        if (_padPlan is not null) { _padPlan = null; }
        if (stick == Vector2.Zero) { if (_padWalking) { StopPadWalk(route); } return; }
        var direction = PadWorldDirection(stick).Normalized();
        _padWalkClock -= delta;
        // Re-aim a few times a second, or at once when the stick turns noticeably.
        if (_padWalking && _padWalkClock > 0 && direction.Dot(_padWalkDirection) > .94f) { return; }
        _padWalkClock = PadWalkRefreshSeconds;
        _padWalkDirection = direction;
        _padWalking = true;
        var origin = WithGroundHeight(ToGodot(leader.Position));
        if (PadGroundTarget(origin, direction, PadWalkLookaheadMeters) is not { } target) { return; }
        if (!_session!.Execute(new MoveActorCommand(NextHumanCommandId("pad-walk"), leader.Id, ToCore(target))).Accepted) { return; }
        var followers = PadFollowers(route, leader);
        if (followers.Length == 0) { return; }
        // The others take the ordinary group-move formation a step behind the walker.
        var follow = PadGroundTarget(origin, -direction, PadFollowDistanceMeters) ?? origin;
        _session.Execute(new MovePartyCommand(NextHumanCommandId("pad-follow"), followers.Select(actor => actor.Id), ToCore(follow)));
    }

    private void StopPadWalk(StationRouteObservation route)
    {
        _padWalking = false;
        var leader = FocusedActor(route);
        if (leader.Combat?.IsDefeated == true || route.ActiveDialogue is not null || _session is null) { return; }
        static bool FromStick(PrimaryActionObservation? action) => action?.CommandId.Value.StartsWith("input.pad-walk.", StringComparison.Ordinal) == true;
        if (FromStick(leader.CurrentAction) || FromStick(leader.PendingAction))
        { _session.Execute(new StopActorsCommand(NextHumanCommandId("pad-halt"), [leader.Id])); }
        // Only followers still on a follow step settle; an order given since (A on an enemy) must survive.
        static bool Following(ActorObservation actor) => actor.PendingAction is { } pending
            ? pending.CommandId.Value.StartsWith("input.pad-follow.", StringComparison.Ordinal)
            : actor.CurrentAction?.CommandId.Value.StartsWith("input.pad-follow.", StringComparison.Ordinal) == true;
        var followers = PadFollowers(route, leader).Where(Following).ToArray();
        if (followers.Length == 0) { return; }
        // Followers settle just behind where the walker stopped.
        var origin = WithGroundHeight(ToGodot(leader.Position));
        var follow = PadGroundTarget(origin, -_padWalkDirection, PadFollowDistanceMeters) ?? origin;
        _session.Execute(new MovePartyCommand(NextHumanCommandId("pad-follow"), followers.Select(actor => actor.Id), ToCore(follow)));
    }

    /// <summary>
    /// A walkable point a short step from <paramref name="origin"/>, or null when a wall or pit is in the way.
    /// Navigation may snap a point just past a thin wall onto the far floor; that path detours, so it is refused.
    /// </summary>
    private Vector3? PadGroundTarget(Vector3 origin, Vector3 direction, float distance)
    {
        var map = GetWorld3D().NavigationMap;
        var start = NavigationServer3D.MapGetClosestPoint(map, origin);
        foreach (var reach in new[] { distance, distance * .5f })
        {
            var wanted = WithGroundHeight(origin + direction * reach);
            var closest = NavigationServer3D.MapGetClosestPoint(map, wanted);
            if (new Vector2(closest.X - wanted.X, closest.Z - wanted.Z).Length() > PadMaximumSnapMeters) { continue; }
            var path = NavigationServer3D.MapGetPath(map, start, closest, true);
            var length = 0f;
            for (var index = 1; index < path.Length; index++) { length += path[index - 1].DistanceTo(path[index]); }
            if (path.Length == 0 || length > reach * 2.2f + .3f) { continue; }
            return WithGroundHeight(closest);
        }
        return null;
    }

    private void PlanPadMove(StationRouteObservation route, ActorObservation leader, Vector2 stick, float seconds)
    {
        // A pause that lands mid-walk (a fight starting, or RT) must not turn the still-held stick into a plan:
        // planning starts with a fresh push.
        if (_padWalking) { _padWalking = false; _padPlanCancelled = stick != Vector2.Zero; }
        if (stick == Vector2.Zero)
        {
            if (_padPlan is { } plan && !_padPlanCancelled) { CommitPadPlan(route, leader, plan); }
            _padPlan = null;
            _padPlanCancelled = false;
            return;
        }
        if (_padPlanCancelled) { return; }
        var start = _padPlan ?? (leader.PendingAction is { Kind: PrimaryActionKind.Move } pending
            ? ToGodot(pending.Destination) : ToGodot(leader.Position));
        var next = WithGroundHeight(start + PadWorldDirection(stick) * PadPlanSpeed * seconds);
        // Small steps projected back onto the walkable floor slide along walls and pit edges.
        _padPlan = WithGroundHeight(NavigationServer3D.MapGetClosestPoint(GetWorld3D().NavigationMap, next));
    }

    private void CommitPadPlan(StationRouteObservation route, ActorObservation leader, Vector3 plan)
    {
        var offset = plan - WithGroundHeight(ToGodot(leader.Position));
        if (offset.Length() < .3f) { return; }
        var result = _session!.Execute(new MoveActorCommand(NextHumanCommandId("pad-plan"), leader.Id, ToCore(plan)));
        if (!result.Accepted) { SetFeedback($"ORDER REJECTED — {result.RejectionCode}", new Color("ff8b8b")); return; }
        var followers = PadFollowers(route, leader);
        if (followers.Length > 0)
        {
            var follow = PadGroundTarget(plan, -offset.Normalized(), PadFollowDistanceMeters) ?? plan;
            _session.Execute(new MovePartyCommand(NextHumanCommandId("pad-plan-follow"), followers.Select(actor => actor.Id), ToCore(follow)));
        }
        SetFeedback(followers.Length > 0 ? "Squad move queued · activates on resume." : "Move queued · activates on resume.", TacticalUi.Cyan);
        RenderObservation(_session.Observe());
    }

    private double PadAbilityRange() => _targetAbilityKind == AbilityTargetKind.Barrier ? _definition!.Combat.Barrier.RangeMeters
        : IsHealingField(_targetAbilityId) ? _definition!.Combat.HealingField.RangeMeters : _definition!.Combat.ProtagonistAbility.RangeMeters;

    private Vector3 ClampToAbilityRange(ActorObservation owner, Vector3 point)
    {
        var origin = WithGroundHeight(ToGodot(owner.Position));
        var offset = WithGroundHeight(point) - origin;
        var range = (float)PadAbilityRange() - .05f;
        return offset.Length() <= range ? WithGroundHeight(point) : origin + offset.Normalized() * range;
    }

    /// <summary>Starts the placement circle where the ability is most likely wanted.</summary>
    private void BeginPadAim(StationRouteObservation route)
    {
        _padAim = null;
        if (!_abilityTargeting || _targetAbilityKind is not (AbilityTargetKind.Position or AbilityTargetKind.Barrier)) { return; }
        if (route.Party.FirstOrDefault(actor => actor.Id == _abilityOwnerId) is not { } owner) { return; }
        var origin = WithGroundHeight(ToGodot(owner.Position));
        var enemy = FindVisibleHostile(route, _padHighlight) is { } highlighted && CanTargetVisibleHostile(route, highlighted) ? highlighted
            : PlayerVisibleHostiles(route).Where(hostile => CanTargetVisibleHostile(route, hostile))
                .OrderBy(hostile => hostile.Position.DistanceTo(owner.Position)).FirstOrDefault();
        var facing = ToGodot(owner.Facing);
        var toward = enemy is null ? facing : WithGroundHeight(ToGodot(enemy.Position)) - origin;
        toward = toward.LengthSquared() > .01f ? toward.Normalized() : Vector3.Forward;
        var start = IsHealingField(_targetAbilityId)
            ? route.Party.Where(actor => actor.Combat?.IsDefeated == false).Select(actor => ToGodot(actor.Position))
                .Aggregate(Vector3.Zero, (sum, position) => sum + position) / Math.Max(1, route.Party.Count(actor => actor.Combat?.IsDefeated == false))
            : _targetAbilityKind == AbilityTargetKind.Barrier || enemy is null ? origin + toward * 2f : ToGodot(enemy.Position);
        _padAim = ClampToAbilityRange(owner, start);
    }

    private void MovePadAim(StationRouteObservation route, Vector3 aim, Vector2 stick, float seconds)
    {
        if (_padWalking) { StopPadWalk(route); }
        if (stick == Vector2.Zero || route.Party.FirstOrDefault(actor => actor.Id == _abilityOwnerId) is not { } owner) { return; }
        _padAim = ClampToAbilityRange(owner, aim + PadWorldDirection(stick) * PadAimSpeed * seconds);
    }

    private static List<EntityId> PadTargetableEnemies(StationRouteObservation route) => PlayerVisibleHostiles(route)
        .Where(hostile => CanTargetVisibleHostile(route, hostile)).Select(hostile => hostile.Id).ToList();

    private static List<EntityId> PadLivingCrew(StationRouteObservation route) => route.Party
        .Where(actor => actor.Combat?.IsDefeated != true).Select(actor => actor.Id).ToList();

    /// <summary>What the D-pad can highlight now: heal targets, enemies in a fight, otherwise nearby objects.</summary>
    private List<EntityId> PadCandidates(StationRouteObservation route, ActorObservation leader)
    {
        if (_abilityTargeting && (IsHealingAbility(_targetAbilityId) || IsHealingField(_targetAbilityId))) { return PadLivingCrew(route); }
        if (route.Encounter?.Phase is EncounterPhase.Readying or EncounterPhase.Active) { return PadTargetableEnemies(route); }
        var origin = ToGodot(leader.Position);
        return route.Interactions.Where(interaction => interaction.State != InteractionState.Completed
                && _interactionViews.TryGetValue(interaction.Id.Value, out var view) && view.IsVisibleInTree()
                && view is CollisionObject3D { CollisionLayer: not 0u }
                && FlatDistance(view.GlobalPosition, origin) <= PadInteractRangeMeters)
            .OrderBy(interaction => FlatDistance(_interactionViews[interaction.Id.Value].GlobalPosition, origin))
            .Select(interaction => interaction.Id).ToList();
    }

    private static float FlatDistance(Vector3 a, Vector3 b) => new Vector2(a.X - b.X, a.Z - b.Z).Length();

    private void UpdatePadHighlight(StationRouteObservation route, ActorObservation leader)
    {
        var candidates = PadCandidates(route, leader);
        if (_padHighlight is { } current && candidates.Contains(current)) { return; }
        _padHighlight = null;
        if (candidates.Count == 0) { return; }
        if (_abilityTargeting && IsHealingAbility(_targetAbilityId))
        {
            // Heal starts on whoever needs it most.
            _padHighlight = route.Party.Where(actor => candidates.Contains(actor.Id))
                .OrderBy(actor => actor.Combat is { } combat ? combat.Health / (double)Math.Max(1, combat.MaximumHealth) : 1)
                .ThenBy(actor => actor.Position.DistanceTo(leader.Position)).First().Id;
            return;
        }
        var remembered = leader.Combat?.RememberedAttackTargetId;
        _padHighlight = remembered is { } target && candidates.Contains(target) ? target
            : candidates.OrderBy(id => PadEntityPosition(route, id) is { } position ? FlatDistance(position, ToGodot(leader.Position)) : float.MaxValue).First();
    }

    private Vector3? PadEntityPosition(StationRouteObservation route, EntityId id)
    {
        if (route.Party.Any(actor => actor.Id == id)) { return _actorViews[id.Value].GlobalPosition; }
        if (_enemyViews.TryGetValue(id, out var enemy)) { return enemy.Root.GlobalPosition; }
        return _interactionViews.TryGetValue(id.Value, out var view) ? view.GlobalPosition : null;
    }

    private void CyclePadHighlight(StationRouteObservation route, int step)
    {
        var leader = FocusedActor(route);
        var candidates = PadCandidates(route, leader);
        if (candidates.Count == 0)
        {
            SetFeedback(route.Encounter?.Phase is EncounterPhase.Readying or EncounterPhase.Active
                ? "No enemy in sight." : "Nothing to use nearby · walk up to a door or person.", TacticalUi.Muted);
            return;
        }
        // Left and right follow the screen, so the highlight moves the way the player looks.
        var ordered = candidates.OrderBy(id => PadEntityPosition(route, id) is { } position && !_camera.IsPositionBehind(position)
            ? _camera.UnprojectPosition(position).X : float.MaxValue).ToList();
        var index = _padHighlight is { } current ? ordered.IndexOf(current) : -1;
        _padHighlight = index < 0 ? ordered[step > 0 ? 0 : ordered.Count - 1] : ordered[(index + step + ordered.Count) % ordered.Count];
        if (PadEntityPosition(route, _padHighlight.Value) is not { } position) { return; }
        if (_padAim is not null && route.Party.FirstOrDefault(actor => actor.Id == _abilityOwnerId) is { } owner)
        {
            // Ground abilities snap their circle: Barrier turns toward the new target, the others centre on it.
            var origin = WithGroundHeight(ToGodot(owner.Position));
            var toward = WithGroundHeight(position) - origin;
            _padAim = ClampToAbilityRange(owner, _targetAbilityKind == AbilityTargetKind.Barrier && toward.LengthSquared() > .01f
                ? origin + toward.Normalized() * 2f : position);
        }
        if (!_camera.IsOnScreen(position + Vector3.Up, 90)) { _camera.GlideTo(position, .45f); }
        GameAudio.Play("ui.select");
    }

    private void TogglePadSquad(StationRouteObservation route)
    {
        var focused = FocusedActor(route);
        if (focused.Combat?.IsDefeated == true) { return; }
        var living = route.Party.Where(actor => actor.Combat?.IsDefeated != true).ToArray();
        CancelSelectionGesture();
        if (SelectedLivingActors(route).Count() > 1)
        {
            _selectedActorIds.Clear();
            _selectedActorIds.Add(focused.Id);
            SetFeedback($"{focused.DisplayName} moves alone · the others hold position.", TacticalUi.Cyan);
        }
        else if (living.Length > 1)
        {
            foreach (var actor in living) { _selectedActorIds.Add(actor.Id); }
            SetFeedback($"Squad follows {focused.DisplayName}.", TacticalUi.Cyan);
        }
        else { SetFeedback("No other crew member to follow yet.", TacticalUi.Muted); return; }
        _focusedActorId = focused.Id;
        RenderObservation(_session!.Observe());
    }

    private bool HandleGamepadButton(InputEventJoypadButton button)
    {
        if (!button.Pressed || _session?.Observe().StationRoute is not { } route) { return false; }
        switch (button.ButtonIndex)
        {
            case JoyButton.A: PadConfirm(route); break;
            case JoyButton.B: PadCancel(); break;
            case JoyButton.X or JoyButton.Y:
                _padPlanCancelled = _padPlan is not null;
                _padPlan = null;
                BeginAbilityTargeting(button.ButtonIndex == JoyButton.X ? 0 : 1);
                if (_abilityTargeting) { BeginPadAim(route); }
                break;
            case JoyButton.LeftShoulder or JoyButton.RightShoulder:
                _padPlanCancelled = _padPlan is not null;
                _padPlan = null;
                CycleActorSelection(button.ButtonIndex == JoyButton.LeftShoulder);
                _padFollowSeconds = 1.2;
                break;
            case JoyButton.DpadLeft or JoyButton.DpadRight: CyclePadHighlight(route, button.ButtonIndex == JoyButton.DpadLeft ? -1 : 1); break;
            case JoyButton.DpadUp: TogglePadSquad(route); break;
            case JoyButton.DpadDown:
                if (!_stopButton.Visible || _stopButton.Disabled) { break; }
                // Stop also drops a move still being dragged, so releasing the stick cannot replace the Stop.
                _padPlanCancelled = _padPlan is not null;
                _padPlan = null;
                StopSelectedActors();
                break;
            default: return false;
        }
        GetViewport().SetInputAsHandled();
        return true;
    }

    /// <summary>RT toggles tactical pause once per pull; a trigger reports a stream of analogue values.</summary>
    private bool HandleGamepadTrigger(InputEventJoypadMotion motion)
    {
        if (motion.Axis != JoyAxis.TriggerRight) { return false; }
        if (_padRightTriggerDown) { _padRightTriggerDown = motion.AxisValue > .25f; return true; }
        if (motion.AxisValue < .55f) { return false; }
        _padRightTriggerDown = true;
        Dispatch(new SetPauseCommand(NextHumanCommandId("pause"), !_session!.IsPaused));
        GetViewport().SetInputAsHandled();
        return true;
    }

    private void PadConfirm(StationRouteObservation route)
    {
        if (_retryButton.Visible) { RestartEncounter(); return; }
        if (_abilityTargeting) { ConfirmAbilityTarget(PointerPosition); return; }
        if (_padPlan is not null) { return; }
        var fighting = route.Encounter?.Phase is EncounterPhase.Readying or EncounterPhase.Active;
        if (_padHighlight is not { } id)
        {
            SetFeedback(fighting ? "No enemy in sight." : "Nothing to use nearby · walk up to a door or person.", TacticalUi.Muted);
            return;
        }
        if (FindVisibleHostile(route, id) is { } hostile && CanTargetVisibleHostile(route, hostile)) { AttackWithSelectedCrew(id); return; }
        if (route.Interactions.Any(interaction => interaction.Id == id)) { InteractWith(route, id); }
    }

    private void PadCancel()
    {
        if (_abilityTargeting)
        {
            CancelAbilityTargeting();
            SetFeedback("Targeting cancelled.", new Color("9eb6ce"));
            return;
        }
        if (_padPlan is null) { return; }
        _padPlan = null;
        _padPlanCancelled = true;
        SetFeedback("Planned move cancelled.", TacticalUi.Muted);
    }

    /// <summary>The pad's stand-in for the mouse: the placement circle or the highlighted target on screen.</summary>
    private Vector2? PadPointer()
    {
        if (_padPointerWorld is not { } world || _camera.IsPositionBehind(world)) { return null; }
        return _camera.UnprojectPosition(world);
    }

    private EntityId? AimedEnemy(Vector2 pointer, StationRouteObservation route)
    {
        if (!InputDevice.UsingGamepad) { return PickSkillEnemy(pointer, route); }
        return FindVisibleHostile(route, _padHighlight) is { } hostile && CanTargetVisibleHostile(route, hostile) ? hostile.Id : null;
    }

    private EntityId? AimedCrew(Vector2 pointer, StationRouteObservation route)
    {
        if (!InputDevice.UsingGamepad) { return PickCrew(pointer); }
        return route.Party.Any(actor => actor.Id == _padHighlight) ? _padHighlight : null;
    }

    private bool TryAimedFloor(Vector2 pointer, out Vector3 point)
    {
        if (!InputDevice.UsingGamepad || _padAim is not { } aim) { return TryPickFloor(pointer, out point); }
        point = aim;
        return CastRay(aim + Vector3.Up * .5f, aim - Vector3.Up * .05f, FloorCollisionLayer).Count > 0;
    }

    private void UpdatePadPresentation(StationRouteObservation route)
    {
        var highlight = _padHighlight is { } id ? PadEntityPosition(route, id) : null;
        _padPointerWorld = _padAim ?? (highlight is { } target ? target + Vector3.Up : null);
        _padPlanMarker.Visible = _padPlan is not null;
        if (_padPlan is { } plan) { _padPlanMarker.GlobalPosition = plan + Vector3.Up * .05f; }
        // While aiming, the ability preview marks its own targets.
        _padHighlightRing.Visible = highlight is not null && !_abilityTargeting && _padPlan is null;
        string? prompt = null;
        Vector3? anchor = null;
        static string G(PadButton button) => InputPrompts.Bb(button, 18);
        if (_padPlan is { } planned)
        {
            prompt = $"Release {G(PadButton.LS)} to queue the move   {G(PadButton.B)} Cancel";
            anchor = planned;
        }
        else if (!_abilityTargeting && _padHighlight is { } highlighted && highlight is { } position)
        {
            var interaction = route.Interactions.FirstOrDefault(item => item.Id == highlighted);
            var hostile = FindVisibleHostile(route, highlighted);
            var color = interaction is null ? TacticalUi.Danger : interaction.State == InteractionState.Unavailable ? TacticalUi.Muted : TacticalUi.Amber;
            _padHighlightMaterial.AlbedoColor = new Color(color, .9f);
            var radius = interaction is null ? 1f : 1.3f;
            var pulse = 1 + .06f * Mathf.Sin((float)Time.GetTicksMsec() / 180f);
            _padHighlightRing.Scale = new Vector3(radius * pulse, 1, radius * pulse);
            _padHighlightRing.GlobalPosition = WithGroundHeight(position) + Vector3.Up * .06f;
            prompt = interaction is not null
                ? interaction.State == InteractionState.Unavailable ? $"{G(PadButton.A)} {interaction.Prompt} · locked" : $"{G(PadButton.A)} {interaction.Prompt}"
                : $"{G(PadButton.A)} Attack · {hostile?.DisplayName.Replace("Security ", "", StringComparison.Ordinal)}";
            anchor = position;
        }
        _padPrompt.Visible = prompt is not null && anchor is { } ground && !_camera.IsPositionBehind(ground);
        if (!_padPrompt.Visible) { return; }
        if (_padPromptText.Text != prompt) { _padPromptText.Text = prompt!; }
        var size = _padPrompt.GetCombinedMinimumSize();
        // Below the feet: health bars and order labels already sit above heads.
        var screen = _camera.UnprojectPosition(WithGroundHeight(anchor!.Value)) + new Vector2(-size.X / 2, 16);
        var view = GetViewport().GetVisibleRect().Grow(-10);
        _padPrompt.Position = new Vector2(Mathf.Clamp(screen.X, view.Position.X, view.End.X - size.X),
            Mathf.Clamp(screen.Y, view.Position.Y, view.End.Y - size.Y));
        _padPrompt.Size = size;
    }

    private void InteractWith(StationRouteObservation route, EntityId interactionId) =>
        Dispatch(new InteractCommand(NextHumanCommandId("interact"), route.Protagonist.Id, interactionId));
}
