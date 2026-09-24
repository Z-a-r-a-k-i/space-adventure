namespace SpaceAdventure.Core;

public sealed partial class GameSession
{
    public CommandRejectionCode? CheckDirectHealTarget(EntityId actorId, EntityId targetId) =>
        !TryValidateCombatOrder(actorId, out var station, out var actor, out var rejection)
            ? rejection : ValidateHealingAbility(station, actor, station.Definition.Combat.DirectHeal.Id, targetId, actor.Position);

    public CommandRejectionCode? CheckHealingFieldPlacement(EntityId actorId, PositionAbilityTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return !TryValidateCombatOrder(actorId, out var station, out var actor, out var rejection)
            ? rejection : ValidateHealingAbility(station, actor, station.Definition.Combat.HealingField.Id, null, target.Position);
    }

    private CommandAcknowledgement ExecuteHealingAbility(UseAbilityCommand command, StationRouteRuntime station, ActorRuntime actor)
    {
        var direct = command.AbilityId == station.Definition.Combat.DirectHeal.Id;
        if (direct ? command.Target is not EntityAbilityTarget : command.Target is not PositionAbilityTarget)
        { return Reject(command.CommandId, CommandRejectionCode.AbilityTargetKindMismatch); }
        var targetId = (command.Target as EntityAbilityTarget)?.EntityId;
        var position = (command.Target as PositionAbilityTarget)?.Position ?? actor.Position;
        var rejection = ValidateHealingAbility(station, actor, command.AbilityId, targetId, position);
        if (rejection is { } reason) { return Reject(command.CommandId, reason); }
        if (targetId is { } id) { position = station.Actors[id].Position; }
        var windup = direct ? station.Definition.Combat.DirectHeal.WindupTicks : station.Definition.Combat.HealingField.WindupTicks;
        AssignPrimaryAction(actor, new PrimaryActionRuntime(command.CommandId, PrimaryActionKind.Ability, position, null, [])
        {
            AbilityId = command.AbilityId, CombatTargetId = targetId, AbilityTargetPosition = position,
            DefensiveAbility = true, Phase = PrimaryActionPhase.Windup,
            PhaseTicksRemaining = windup, PhaseTicksTotal = windup,
        });
        return Accept(command.CommandId);
    }

    private CommandRejectionCode? ValidateHealingAbility(StationRouteRuntime station, ActorRuntime actor,
        AbilityId id, EntityId? targetId, WorldPosition position)
    {
        var combat = station.Definition.Combat;
        var direct = id == combat.DirectHeal.Id;
        if (actor.Id != station.Definition.Medic.Id || actor.Loadout is null
            || (direct ? actor.Loadout.ActiveAbilityId != id : actor.Loadout.SecondaryAbilityId != id))
        { return CommandRejectionCode.UnknownAbility; }
        if (actor.Cooldowns.GetValueOrDefault(id) > 0) { return CommandRejectionCode.AbilityOnCooldown; }
        if (direct)
        {
            if (targetId is not { } target || !station.Actors.TryGetValue(target, out var ally))
            { return CommandRejectionCode.InvalidAbilityTarget; }
            if (ally.Health <= 0) { return CommandRejectionCode.CombatantDefeated; }
            position = ally.Position;
        }
        if (!position.IsFinite || !direct && Math.Abs(position.Y - actor.Position.Y) > .1)
        { return CommandRejectionCode.InvalidAbilityTarget; }
        if (!direct && !TryNormalizePath(_pathfinder!.FindPath(actor.Id, actor.Position, position), actor.Position,
            endpoint => endpoint.DistanceTo(position) <= .1, out _))
        { return CommandRejectionCode.InvalidAbilityTarget; }
        return actor.Position.DistanceTo(position) > (direct ? combat.DirectHeal.RangeMeters : combat.HealingField.RangeMeters)
            ? CommandRejectionCode.AbilityTargetOutOfRange : null;
    }

    private void AdvanceHealingAbility(StationRouteRuntime station, ActorRuntime actor, PrimaryActionRuntime action)
    {
        UpdatePhaseRemaining(action);
        if (action.PhaseTicksRemaining > 0) { return; }
        if (action.Phase == PrimaryActionPhase.Recovery)
        { actor.CurrentAction = null; ResumeRememberedAttack(station, actor); return; }
        var id = action.AbilityId!.Value;
        var rejection = ValidateHealingAbility(station, actor, id, action.CombatTargetId, action.AbilityTargetPosition);
        if (rejection is { } reason)
        { actor.CurrentAction = null; RecordPrimaryActionFailure(actor, action, reason); ResumeRememberedAttack(station, actor); return; }
        var combat = station.Definition.Combat;
        actor.Cooldowns[id] = combat.AbilityCooldownTicks(id);
        var direct = id == combat.DirectHeal.Id;
        var position = direct ? station.Actors[action.CombatTargetId!.Value].Position : action.AbilityTargetPosition;
        Record(GameplayEventType.AbilityReleased, action.CommandId,
            detail: new AbilityReleasedEventDetail(actor.Id, position, id, true));
        if (direct)
        { HealActor(actor.Id, station.Actors[action.CombatTargetId!.Value], id, combat.DirectHeal.Healing); }
        else
        {
            var field = combat.HealingField;
            station.Combat.HealingField = new HealingFieldRuntime(actor.Id, position, Tick,
                Tick + field.DurationTicks, Tick + field.PulseIntervalTicks);
            Record(GameplayEventType.HealingFieldDeployed, action.CommandId,
                detail: new HealingFieldEventDetail(actor.Id, id, position, field.RadiusMeters, field.DurationTicks));
        }
        BeginRecovery(action, direct ? combat.DirectHeal.RecoveryTicks : combat.HealingField.RecoveryTicks);
    }

    private void HealActor(EntityId sourceId, ActorRuntime target, AbilityId abilityId, int amount)
    {
        if (target.Health <= 0) { return; }
        var applied = Math.Min(amount, target.MaximumHealth - target.Health);
        if (applied <= 0) { return; }
        target.Health += applied;
        Record(GameplayEventType.HealingApplied,
            detail: new HealingAppliedEventDetail(sourceId, target.Id, abilityId, applied, target.Health));
    }

    private void AdvanceHealingField(StationRouteRuntime station)
    {
        if (station.Combat.HealingField is not { } field) { return; }
        if (station.Combat.Phase != EncounterPhase.Active || Tick > field.ExpiresAtTick)
        { station.Combat.HealingField = null; return; }
        if (Tick >= field.NextPulseTick)
        {
            var definition = station.Definition.Combat.HealingField;
            foreach (var actor in station.Actors.Values.OrderBy(actor => actor.PartyOrder))
            {
                if (actor.Position.DistanceTo(field.Position) <= definition.RadiusMeters)
                { HealActor(field.SourceId, actor, definition.Id, definition.HealingPerPulse); }
            }
            field.NextPulseTick += definition.PulseIntervalTicks;
        }
        if (Tick >= field.ExpiresAtTick) { station.Combat.HealingField = null; }
    }

    private HealingFieldObservation? ObserveHealingField(StationRouteRuntime station) => station.Combat.HealingField is { } field
        ? new HealingFieldObservation(field.SourceId, field.Position, station.Definition.Combat.HealingField.RadiusMeters,
            field.DeployedAtTick, (int)Math.Max(0, field.ExpiresAtTick - Tick), station.Definition.Combat.HealingField.DurationTicks,
            station.Definition.Combat.HealingField.PulseIntervalTicks) : null;

    private sealed class HealingFieldRuntime(EntityId sourceId, WorldPosition position, long deployedAtTick, long expiresAtTick, long nextPulseTick)
    {
        public EntityId SourceId { get; } = sourceId;
        public WorldPosition Position { get; } = position;
        public long DeployedAtTick { get; } = deployedAtTick;
        public long ExpiresAtTick { get; } = expiresAtTick;
        public long NextPulseTick { get; set; } = nextPulseTick;
    }
}
