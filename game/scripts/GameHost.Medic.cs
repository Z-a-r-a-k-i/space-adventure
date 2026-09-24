using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private static readonly Color MedicAccent = new("addcfa");
    private ArmedHumanoidPresentation _medicPresentation = null!;
    private HumanoidPresentation _medicWaitingPresentation = null!;
    private MeshInstance3D _healingFieldRing = null!;
    private MeshInstance3D _queuedHealingRing = null!;
    private Label3D _healingFieldLabel = null!;

    private static bool IsHealingAbility(AbilityId? id) => id?.Value == "ability.crew.medic.heal";
    private static bool IsHealingField(AbilityId? id) => id?.Value == "ability.crew.medic.healing_field";
    private static string CrewPortrait(EntityId id) => id.Value switch
    {
        "actor.protagonist" => "vanguard", "actor.companion.protector" => "protector", "actor.companion.medic" => "operator",
        _ => throw new InvalidDataException($"No portrait bound for crew '{id}'."),
    };

    private void CacheMedicViews()
    {
        _medicPresentation = GetNode<ArmedHumanoidPresentation>("Actors/Medic/MedicPresentation");
        _medicWaitingPresentation = GetNode<HumanoidPresentation>("Interactions/Medic/MedicPresentation");
        _healingFieldRing = MakeHealingRing("HealingField", new Color("94f4b6", .75f));
        _queuedHealingRing = MakeHealingRing("QueuedHealingField", new Color("addcfa", .5f));
        _healingFieldLabel = new Label3D { Name = "HealingFieldLifetime", FontSize = 32, OutlineSize = 7,
            Modulate = new Color("94f4b6"), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Visible = false };
        AddChild(_healingFieldLabel);
    }

    private MeshInstance3D MakeHealingRing(string name, Color color)
    {
        var view = new MeshInstance3D { Name = name, Visible = false,
            Mesh = new TorusMesh { InnerRadius = .965f, OuterRadius = 1, Rings = 64, RingSegments = 4 },
            MaterialOverride = CreateCombatEffectMaterial(color), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(view);
        return view;
    }

    private void SynchronizeMedic(GameObservation observation, StationRouteObservation route)
    {
        var medic = route.Party.FirstOrDefault(actor => actor.Id == _definition!.Medic.Id);
        _medicPresentation.Synchronize(medic is not null, medic?.CurrentAction?.HasRemainingMovement == true,
            observation.Paused, medic is null ? Vector3.Zero : ActorFacing(route, medic), route.Encounter,
            medic?.CurrentAction, _presentationTick, _presentationDeltaSeconds, medic?.Combat?.IsDefeated == true,
            medic?.Combat?.DefeatedAtTick, bodyFacing: medic is null ? null : SampleActorFacing(medic));
        _medicWaitingPresentation.Synchronize(medic is null, HumanoidPresentationAction.Idle,
            observation.Paused, Vector3.Zero, presentationTick: _presentationTick,
            turnDeltaSeconds: _presentationDeltaSeconds);
    }

    private void SynchronizeHealingField(StationRouteObservation route)
    {
        var field = route.Encounter?.HealingField;
        _healingFieldRing.Visible = _healingFieldLabel.Visible = field is not null;
        if (field is not null)
        {
            _healingFieldRing.Position = ToGodot(field.Position) + Vector3.Up * .055f;
            _healingFieldRing.Scale = new Vector3((float)field.RadiusMeters, 1, (float)field.RadiusMeters);
            _healingFieldLabel.Position = ToGodot(field.Position) + new Vector3(0, .4f, (float)field.RadiusMeters);
            _healingFieldLabel.Text = $"HEALING  {field.RemainingTicks / 30.0:0.0}s";
        }
        var medic = route.Party.FirstOrDefault(actor => actor.Id == _definition!.Medic.Id);
        var pending = IsHealingField(medic?.PendingAction?.AbilityId) ? medic!.PendingAction
            : medic?.CurrentAction is { Phase: PrimaryActionPhase.Windup } action && IsHealingField(action.AbilityId) ? action : null;
        _queuedHealingRing.Visible = pending is not null && !_abilityTargeting;
        if (pending is not null)
        {
            _queuedHealingRing.Position = ToGodot(pending.Destination) + Vector3.Up * .04f;
            var radius = (float)_definition!.Combat.HealingField.RadiusMeters;
            _queuedHealingRing.Scale = new Vector3(radius, 1, radius);
        }
    }

    private bool ShowMedicTargetPreview(GameObservation observation, StationRouteObservation route, ActorObservation actor, Vector2 pointer)
    {
        if (!IsHealingAbility(_targetAbilityId) && !IsHealingField(_targetAbilityId)) { return false; }
        var title = $"{CrewNumber(route, actor.Id):00} MEDIC · {(IsHealingAbility(_targetAbilityId) ? "HEAL" : "HEALING FIELD")}";
        var timing = AbilityResumeText(observation, route, actor, duringRecovery: true);
        if (IsHealingAbility(_targetAbilityId))
        {
            var ally = route.Party.FirstOrDefault(crew => crew.Id == PickCrew(pointer) && crew.Combat?.IsDefeated == false);
            if (ally is null)
            { ShowAbilityContext(title, "Choose a living ally or their portrait. You may heal yourself.", "Left-click ally · Esc cancels", MedicAccent, true); return true; }
            var heal = _definition!.Combat.DirectHeal;
            var healRejection = _session!.CheckDirectHealTarget(actor.Id, ally.Id);
            var healValid = healRejection is null;
            var amount = Math.Min(heal.Healing, ally.Combat!.MaximumHealth - ally.Combat.Health);
            ShowAbilityContext(title, healValid ? $"{ally.DisplayName} · restore {amount} health ({heal.Healing} maximum)."
                : healRejection == CommandRejectionCode.AbilityTargetOutOfRange ? $"OUT OF RANGE · choose an ally within {heal.RangeMeters:0.#}m."
                : "HEAL UNAVAILABLE · choose a living ally when ready.", timing, healValid ? MedicAccent : TacticalUi.Danger, true);
            if (healValid) { ShowAffectedTargets(route, [ally.Id]); }
            return true;
        }
        var field = _definition!.Combat.HealingField;
        if (!TryPickFloor(pointer, out var point))
        { ShowAbilityContext(title, "Aim on the station floor.", "Esc cancels", TacticalUi.Danger, true); return true; }
        var rejection = _session!.CheckHealingFieldPlacement(actor.Id, new PositionAbilityTarget(ToCore(point)));
        var valid = rejection is null;
        var affected = route.Party.Where(crew => crew.Combat?.IsDefeated == false
            && crew.Position.DistanceTo(ToCore(point)) <= field.RadiusMeters).ToArray();
        _abilityTargetPreview.Visible = valid;
        _abilityTargetPreview.Position = point + Vector3.Up * .035f;
        _abilityTargetPreview.Scale = new Vector3((float)field.RadiusMeters / 2, 1, (float)field.RadiusMeters / 2);
        ShowAbilityContext(title, valid ? $"{affected.Length} crew inside now · {field.HealingPerPulse} health each pulse for {field.DurationTicks / 30.0:0.#}s."
            : HealingFieldRejectionText(rejection!.Value), timing + " Crew may enter after deployment.", valid ? MedicAccent : TacticalUi.Danger, true);
        if (valid) { ShowAffectedTargets(route, affected.Select(crew => crew.Id)); }
        return true;
    }

    private string HealingFieldRejectionText(CommandRejectionCode reason) => reason switch
    {
        CommandRejectionCode.AbilityTargetOutOfRange => $"OUT OF RANGE · place within {_definition!.Combat.HealingField.RangeMeters:0.#}m.",
        CommandRejectionCode.InvalidAbilityTarget => "NO WALKABLE FLOOR · place inside the accessible walkway.",
        CommandRejectionCode.AbilityOnCooldown => "Healing Field is recovering. Resume combat to recharge.",
        _ => "Healing Field is unavailable.",
    };

    private void PresentHealing(HealingAppliedEventDetail healing, long tick)
    {
        if (healing.Amount <= 0 || !_actorViews.TryGetValue(healing.TargetId.Value, out var target)) { return; }
        var label = new Label3D { Text = $"+{healing.Amount}", Position = target.GlobalPosition + Vector3.Up * 1.65f,
            FontSize = 35, OutlineSize = 7, Modulate = new Color("94f4b6"), Billboard = BaseMaterial3D.BillboardModeEnum.Enabled };
        AddChild(label);
        _combatPresentationEffects.Add(new TimedPresentationEffect(label, .75f, tick));
        var ring = MakeHealingRing("HealingPulse", new Color("94f4b6", .8f));
        ring.Visible = true; ring.Position = target.GlobalPosition + Vector3.Up * .06f; ring.Scale = new Vector3(.65f, 1, .65f);
        _combatPresentationEffects.Add(new TimedPresentationEffect(ring, .4f, tick));
        if (IsHealingAbility(healing.AbilityId)) { PlayCombatCue("heal", target.GlobalPosition); }
    }
}
