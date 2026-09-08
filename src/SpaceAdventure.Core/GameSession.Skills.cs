namespace SpaceAdventure.Core;

public sealed partial class GameSession
{
    private CommandAcknowledgement ExecuteSecondaryAbility(UseAbilityCommand command, StationRouteRuntime station, ActorRuntime actor)
    {
        var combat = station.Definition.Combat;
        if (actor.Loadout!.SecondaryAbilityId != command.AbilityId)
        { return Reject(command.CommandId, CommandRejectionCode.UnknownAbility); }
        var burst = command.AbilityId == combat.Burst.Id;
        if ((burst && command.Target is not EntityAbilityTarget) || (!burst && command.Target is not SelfAbilityTarget))
        { return Reject(command.CommandId, CommandRejectionCode.AbilityTargetKindMismatch); }
        var targetId = (command.Target as EntityAbilityTarget)?.EntityId;
        var rejection = ValidateSecondaryAbility(station, actor, command.AbilityId, targetId);
        if (rejection is { } reason) { return Reject(command.CommandId, reason); }
        var ticks = burst ? combat.Burst.WindupTicks : combat.Taunt.WindupTicks;
        AssignPrimaryAction(actor, new PrimaryActionRuntime(command.CommandId, PrimaryActionKind.Ability,
            targetId is { } id ? station.Combat.Hostiles[id].Position : actor.Position, null, [])
        {
            AbilityId = command.AbilityId, CombatTargetId = targetId,
            Phase = PrimaryActionPhase.Windup, PhaseTicksRemaining = ticks, PhaseTicksTotal = ticks,
            DefensiveAbility = !burst,
        });
        return Accept(command.CommandId);
    }

    private static CommandRejectionCode? ValidateSecondaryAbility(StationRouteRuntime station, ActorRuntime actor,
        AbilityId id, EntityId? targetId, bool released = false)
    {
        if (actor.Loadout?.SecondaryAbilityId != id) { return CommandRejectionCode.UnknownAbility; }
        if (!released && actor.Cooldowns.GetValueOrDefault(id) > 0) { return CommandRejectionCode.AbilityOnCooldown; }
        if (id != station.Definition.Combat.Burst.Id) { return null; }
        if (targetId is not { } target || !station.Combat.Hostiles.TryGetValue(target, out var hostile))
        { return CommandRejectionCode.UnknownCombatTarget; }
        if (hostile.Health <= 0) { return CommandRejectionCode.CombatantDefeated; }
        return actor.Position.DistanceTo(hostile.Position) > station.Definition.Combat.Burst.RangeMeters
            ? CommandRejectionCode.AbilityTargetOutOfRange : null;
    }

    private void AdvanceSecondaryAbility(StationRouteRuntime station, ActorRuntime actor, PrimaryActionRuntime action)
    {
        UpdatePhaseRemaining(action);
        if (action.PhaseTicksRemaining > 0) { return; }
        if (action.Phase == PrimaryActionPhase.Recovery)
        { actor.CurrentAction = null; ResumeRememberedAttack(station, actor); return; }
        var combat = station.Definition.Combat;
        var rejection = ValidateSecondaryAbility(station, actor, action.AbilityId!.Value, action.CombatTargetId, action.ShotsReleased > 0);
        if (rejection is { } reason)
        {
            RecordPrimaryActionFailure(actor, action, reason);
            if (action.ShotsReleased > 0) { BeginRecovery(action, combat.Burst.RecoveryTicks); }
            else { actor.CurrentAction = null; ResumeRememberedAttack(station, actor); }
            return;
        }
        if (action.AbilityId == combat.Taunt.Id)
        {
            actor.Cooldowns[combat.Taunt.Id] = combat.Taunt.CooldownTicks;
            var targets = station.Combat.Hostiles.Values.Where(hostile => hostile.Health > 0
                && hostile.Position.DistanceTo(actor.Position) <= combat.Taunt.RadiusMeters).ToArray();
            Record(GameplayEventType.AbilityReleased, action.CommandId,
                detail: new AbilityReleasedEventDetail(actor.Id, actor.Position, combat.Taunt.Id, targets.Length > 0));
            foreach (var hostile in targets)
            {
                hostile.TauntedBy = actor.Id; hostile.TauntedUntilTick = Tick + combat.Taunt.DurationTicks;
                // Redirect only unreleased intent. Released shots and recovery remain intact.
                if (hostile.CurrentAction is { Phase: not PrimaryActionPhase.Recovery }) { hostile.CurrentAction = null; }
                hostile.Waypoints = []; hostile.WaypointIndex = 0;
                Record(GameplayEventType.TauntApplied, action.CommandId,
                    detail: new TauntEventDetail(actor.Id, hostile.Id, combat.Taunt.DurationTicks));
            }
            BeginRecovery(action, combat.Taunt.RecoveryTicks);
            return;
        }
        var burst = combat.Burst;
        if (action.ShotsReleased == 0) { actor.Cooldowns[burst.Id] = burst.CooldownTicks; }
        var target = station.Combat.Hostiles[action.CombatTargetId!.Value];
        Record(GameplayEventType.AbilityReleased, action.CommandId,
            detail: new AbilityReleasedEventDetail(actor.Id, target.Position, burst.Id, true));
        action.ShotsReleased++;
        actor.OffensiveRecoveryUntilTick = Tick + burst.RecoveryTicks;
        DamageHostile(station, target, actor.Id, burst.DamagePerShot, null, burst.Id);
        if (station.Combat.Phase != EncounterPhase.Active) { return; }
        if (action.ShotsReleased >= burst.ShotCount || target.Health <= 0) { BeginRecovery(action, burst.RecoveryTicks); }
        else
        {
            action.PhaseStartedTick = Tick;
            action.PhaseTicksRemaining = action.PhaseTicksTotal = burst.ShotIntervalTicks;
        }
    }

    private void ValidateTaunts(StationRouteRuntime station)
    {
        foreach (var hostile in station.Combat.Hostiles.Values)
        {
            if (hostile.TauntedBy is not { } id) { continue; }
            if (hostile.Health > 0 && station.Actors[id].Health > 0 && Tick < hostile.TauntedUntilTick) { continue; }
            hostile.TauntedBy = null; hostile.TauntedUntilTick = 0;
        }
    }
}
