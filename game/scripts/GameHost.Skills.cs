using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private PanelContainer _abilityContext = null!;
    private Label _abilityContextTitle = null!;
    private Label _abilityContextDetail = null!;
    private Label _abilityContextTiming = null!;
    private int? _inspectedAbilitySlot;
    private readonly Dictionary<EntityId, MeshInstance3D> _affectedTargetRings = [];

    private void CreateAbilityContext(CanvasLayer canvas)
    {
        _abilityContext = new PanelContainer { Name = "AbilityContext", Visible = false, ZIndex = 12,
            MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(288, 0) };
        _abilityContext.AddThemeStyleboxOverride("panel", TacticalUi.FieldPanel(TacticalUi.Cyan, margin: 10));
        canvas.AddChild(_abilityContext);
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 5);
        _abilityContext.AddChild(column);
        _abilityContextTitle = TacticalUi.Label("", 13, "a0efd8");
        _abilityContextDetail = HudLabel("", 13);
        _abilityContextTiming = HudLabel("", 12, "e5bc7d");
        _abilityContextDetail.CustomMinimumSize = _abilityContextTiming.CustomMinimumSize = new Vector2(268, 0);
        column.AddChild(_abilityContextTitle); column.AddChild(_abilityContextDetail); column.AddChild(_abilityContextTiming);
        _abilityButton.MouseEntered += () => _inspectedAbilitySlot = 0;
        _abilityButton.MouseExited += () => _inspectedAbilitySlot = null;
        _secondaryAbilityButton.MouseEntered += () => _inspectedAbilitySlot = 1;
        _secondaryAbilityButton.MouseExited += () => _inspectedAbilitySlot = null;
    }

    private void HideAbilityContext()
    {
        if (_abilityContext is not null) { _abilityContext.Visible = false; }
        foreach (var ring in _affectedTargetRings.Values) { ring.Visible = false; }
    }

    private static string AbilityResumeText(GameObservation observation, StationRouteObservation route, ActorObservation actor,
        bool duringRecovery = false)
    {
        var when = observation.Paused ? "On resume" : "On confirmation";
        if (route.Encounter?.Phase == EncounterPhase.Readying) { when += ", after weapon draw"; }
        else if (!duringRecovery && actor.Combat?.OffensiveRecoveryUntilTick > observation.Tick) { when += ", after recovery"; }
        return when + (actor.PendingAction is null ? "." : ". Replaces this crew member's next order.");
    }

    private void ShowAbilityContext(string title, string detail, string timing, Color accent, bool atPointer)
    {
        _abilityContextTitle.Text = title;
        _abilityContextTitle.AddThemeColorOverride("font_color", accent);
        _abilityContextDetail.Text = detail;
        _abilityContextTiming.Text = timing;
        ((StyleBoxFlat)_abilityContext.GetThemeStylebox("panel")).BorderColor = accent;
        // Fix the width so changing counts cannot make the pointer card jump across its target.
        _abilityContext.Size = new Vector2(288, 0);
        var size = _abilityContext.GetCombinedMinimumSize().Max(new Vector2(288, 0));
        var viewport = GetViewport().GetVisibleRect().Grow(-12);
        var pointer = GetViewport().GetMousePosition();
        var origin = atPointer ? pointer : _actionPanel.GetGlobalRect().Position;
        var candidates = atPointer
            ? new[] { new Vector2(30, 32), new Vector2(-size.X - 30, 32), new Vector2(30, -size.Y - 32), new Vector2(-size.X - 30, -size.Y - 32) }
            : new[] { new Vector2(0, -size.Y - 10) };
        var obstacles = FieldHudBounds().ToList();
        obstacles.AddRange(_worldHealth.Values.Where(view => view.Root.Visible).Select(view => view.Root.GetGlobalRect().Grow(5)));
        var route = _session?.Observe().StationRoute;
        if (atPointer && route is not null)
        {
            foreach (var actor in route.Party) { AddActorObstacle(ToGodot(actor.Position), 2.2f); }
            foreach (var enemy in route.Hostiles?.Where(enemy => !enemy.Combat.IsDefeated) ?? []) { AddActorObstacle(ToGodot(enemy.Position), 2.4f); }
        }
        foreach (var offset in candidates)
        {
            var candidate = new Rect2(origin + offset, size);
            if (viewport.Encloses(candidate) && obstacles.All(rect => !rect.Intersects(candidate)))
            { _abilityContext.Position = candidate.Position; _abilityContext.Visible = true; return; }
        }
        // The fixed ability corner remains a readable fallback in crowded fighting space.
        var fallback = _actionPanel.GetGlobalRect().Position - new Vector2(0, size.Y + 10);
        _abilityContext.Position = new Vector2(Mathf.Clamp(fallback.X, viewport.Position.X, viewport.End.X - size.X),
            Mathf.Clamp(fallback.Y, viewport.Position.Y, viewport.End.Y - size.Y));
        _abilityContext.Visible = true;

        void AddActorObstacle(Vector3 position, float height)
        {
            if (_camera.IsPositionBehind(position)) { return; }
            obstacles.Add(new Rect2(_camera.UnprojectPosition(position), Vector2.Zero)
                .Expand(_camera.UnprojectPosition(position + Vector3.Up * height)).Grow(18));
        }
    }

    private void ShowAffectedTargets(StationRouteObservation route, IEnumerable<EntityId> targetIds)
    {
        foreach (var id in targetIds)
        {
            var hostile = route.Hostiles!.First(enemy => enemy.Id == id);
            if (!_affectedTargetRings.TryGetValue(id, out var ring))
            {
                ring = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = .49f, OuterRadius = .55f,
                    Rings = 32, RingSegments = 4 }, MaterialOverride = CreateCombatEffectMaterial(new Color(TacticalUi.Amber, .8f)),
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                AddChild(ring); _affectedTargetRings.Add(id, ring);
            }
            ring.Position = ToGodot(hostile.Position) + Vector3.Up * .06f;
            ring.Visible = true;
        }
    }

    private void ShowInspectedAbility(GameObservation observation, StationRouteObservation route)
    {
        if (_inspectedAbilitySlot is not { } slot || !_actionPanel.IsVisibleInTree()
            || route.ActiveDialogue is not null || _controlsOverlay.Visible || _completionOverlay.Visible) { return; }
        var actor = FocusedActor(route);
        if (actor.Loadout is null) { return; }
        var id = slot == 0 ? actor.Loadout.ActiveAbilityId : actor.Loadout.SecondaryAbilityId;
        var name = slot == 0 ? actor.Loadout.ActiveAbilityName : actor.Loadout.SecondaryAbilityName;
        var combat = _definition!.Combat;
        var detail = id == combat.Barrier.Id
            ? $"Place within {combat.Barrier.RangeMeters:0.#}m. A {combat.Barrier.WidthMeters:0.#}m shield blocks projectiles for {combat.Barrier.DurationTicks / 30.0:0.#}s. Faces from Protector toward placement and stays there."
            : id == combat.Burst.Id
                ? $"One enemy within {combat.Burst.RangeMeters:0.#}m. {combat.Burst.ShotCount} shots × {combat.Burst.DamagePerShot} damage. Assigned basic fire resumes afterward."
                : id == combat.Taunt.Id
                    ? $"Enemies within {combat.Taunt.RadiusMeters:0.#}m focus Protector for {combat.Taunt.DurationTicks / 30.0:0.#}s. Released shots keep their target."
                    : $"Aim within {combat.ProtagonistAbility.RangeMeters:0.#}m. Enemies in a {combat.ProtagonistAbility.RadiusMeters:0.#}m radius take {combat.ProtagonistAbility.Damage} damage; their wind-ups stop.";
        var cooldown = actor.Combat?.Cooldowns.FirstOrDefault(value => value.AbilityId == id)?.RemainingTicks ?? 0;
        var timing = actor.Combat?.IsDefeated == true ? "Crew member down. Select living crew."
            : route.Encounter?.Phase is not (EncounterPhase.Readying or EncounterPhase.Active) ? "Available during combat."
            : cooldown > 0 ? $"Ready in {cooldown / 30.0:0.0}s of live combat."
            : AbilityResumeText(observation, route, actor, id == combat.Barrier.Id || id == combat.Taunt.Id);
        if (id == combat.Taunt.Id && route.Encounter?.Phase is EncounterPhase.Readying or EncounterPhase.Active)
        {
            var affected = route.Hostiles!.Where(enemy => !enemy.Combat.IsDefeated
                && enemy.Position.DistanceTo(actor.Position) <= combat.Taunt.RadiusMeters).Select(enemy => enemy.Id).ToArray();
            timing = $"{affected.Length} {(affected.Length == 1 ? "enemy" : "enemies")} in reach now. " + timing;
            ShowAffectedTargets(route, affected);
            _abilityTargetPreview.Scale = new Vector3((float)combat.Taunt.RadiusMeters / 2, 1, (float)combat.Taunt.RadiusMeters / 2);
            _abilityTargetPreview.Position = ToGodot(actor.Position) + Vector3.Up * .035f;
            _abilityTargetPreview.Visible = true;
        }
        ShowAbilityContext($"{CrewNumber(route, actor.Id):00} {actor.DisplayName.ToUpperInvariant()} · {name.ToUpperInvariant()}",
            detail, timing, CrewAccent(route, actor), atPointer: false);
    }

    private void PresentTaunt(AbilityReleasedEventDetail ability, long tick)
    {
        var radius = (float)_definition!.Combat.Taunt.RadiusMeters;
        SpawnSignature(CombatSignature.Taunt, ToGodot(ability.TargetPosition) + Vector3.Up * .07f,
            Vector3.Up, radius: radius);
        foreach (var hostile in _session!.Observe().StationRoute!.Hostiles!.Where(enemy => enemy.Combat.TauntedBy == ability.SourceId))
        {
            var label = new Label3D { Text = "TAUNTED", Position = ToGodot(hostile.Position) + Vector3.Up * 2,
                FontSize = 28, OutlineSize = 7, Modulate = new Color("efb263"), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled };
            AddChild(label); _combatPresentationEffects.Add(new TimedPresentationEffect(label, .9f, tick));
        }
        PlayCombatCue("taunt", ToGodot(ability.TargetPosition));
    }
}
