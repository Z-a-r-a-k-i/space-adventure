namespace SpaceAdventure.Core;

public sealed partial class GameSession
{
    private CommandAcknowledgement Execute(StopActorsCommand command)
    {
        if (command.ActorIds.Count == 0)
        {
            return Reject(command.CommandId, CommandRejectionCode.EmptyPartySelection);
        }

        if (command.ActorIds.Distinct().Count() != command.ActorIds.Count)
        {
            return Reject(command.CommandId, CommandRejectionCode.DuplicateActor);
        }

        var actors = new List<ActorRuntime>();
        foreach (var actorId in command.ActorIds)
        {
            if (!TryValidatePrimaryOrder(actorId, out _, out var actor, out var rejection))
            {
                return Reject(command.CommandId, rejection);
            }

            actors.Add(actor);
        }

        foreach (var actor in actors)
        {
            AssignPrimaryAction(actor, new PrimaryActionRuntime(
                command.CommandId, PrimaryActionKind.Stop, actor.Position, null, []));
        }

        return Accept(command.CommandId);
    }

    private static bool IsOffensive(PrimaryActionRuntime action) =>
        action.Kind is PrimaryActionKind.Attack or PrimaryActionKind.Ability;

    private static bool SameAttack(PrimaryActionRuntime? current, PrimaryActionRuntime next) =>
        current?.Kind == PrimaryActionKind.Attack
        && next.Kind == PrimaryActionKind.Attack
        && current.CombatTargetId == next.CombatTargetId;

    private ActionWaitingReason? GetWaitingReason(ActorRuntime actor, PrimaryActionRuntime action)
    {
        if (IsPaused)
        {
            return ActionWaitingReason.TacticalPause;
        }

        if (_stationRoute?.Combat.Phase == EncounterPhase.Readying)
        {
            return ActionWaitingReason.EncounterReadying;
        }

        return IsOffensive(action) && Tick < actor.OffensiveRecoveryUntilTick
            ? ActionWaitingReason.OffensiveRecovery
            : null;
    }

    private void AssignPrimaryAction(ActorRuntime actor, PrimaryActionRuntime action)
    {
        if (action.Kind == PrimaryActionKind.Attack)
        {
            actor.RememberedAttackTargetId = action.CombatTargetId;
            actor.RememberedAttackCommandId = action.CommandId;
        }
        else if (action.Kind is PrimaryActionKind.Move or PrimaryActionKind.Interact or PrimaryActionKind.Stop)
        {
            ClearAttackIntent(actor);
        }

        // A repeated click still supersedes a different pending order. It never
        // restarts the attack cycle that is already running against this target.
        if (SameAttack(actor.PendingAction, action)
            || (SameAttack(actor.CurrentAction, action) && (!IsPaused || actor.PendingAction is null)))
        {
            if (!IsPaused)
            {
                actor.PendingAction = null;
            }

            return;
        }

        var pending = GetWaitingReason(actor, action) is not null;
        var replacedCommandId = pending ? actor.PendingAction?.CommandId : actor.CurrentAction?.CommandId;
        action.InstanceId = ++_actionSequence;
        if (pending)
        {
            actor.PendingAction = action;
            if (!IsPaused && actor.CurrentAction?.Phase != PrimaryActionPhase.Recovery)
            {
                actor.CurrentAction = null;
            }
        }
        else
        {
            actor.PendingAction = null;
            StartPrimaryAction(actor, action, Tick);
        }

        Record(GameplayEventType.PrimaryActionAssigned, action.CommandId,
            detail: new PrimaryActionAssignedEventDetail(
                action.CommandId, actor.Id, action.Kind, action.Destination,
                action.InteractionTargetId, pending, replacedCommandId));
    }

    private void StartPrimaryAction(ActorRuntime actor, PrimaryActionRuntime action, long startedTick)
    {
        if (SameAttack(actor.CurrentAction, action))
        {
            return;
        }

        actor.CurrentAction = action.Kind == PrimaryActionKind.Stop ? null : action;
        action.PhaseStartedTick = startedTick;
        if (_stationRoute is not { } station)
        {
            return;
        }

        if (action.Kind == PrimaryActionKind.Attack
            && action.AttackId is AttackId attackId
            && actor.Position.DistanceTo(station.Combat.Hostile.Position)
                <= station.Definition.Combat.GetAttack(attackId).RangeMeters)
        {
            BeginAttackWindup(actor.Id, station.Combat.Hostile.Id,
                station.Definition.Combat.GetAttack(attackId), action, startedTick);
        }
    }

    private void PromotePendingActions(bool advancingTick = false)
    {
        if (_stationRoute is not { } station || station.Combat.Phase == EncounterPhase.Readying)
        {
            return;
        }

        foreach (var actor in station.Actors.Values.OrderBy(actor => actor.PartyOrder))
        {
            if (actor.PendingAction is not { } pending
                || (IsOffensive(pending) && Tick < actor.OffensiveRecoveryUntilTick))
            {
                continue;
            }

            // Exact paused stepping consumes the same pending order as resume.
            // Validate again before replacing anything or spending a resource.
            var rejection = ValidatePendingAction(station, actor, pending);
            actor.PendingAction = null;
            if (rejection is { } reason)
            {
                RecordPrimaryActionFailure(actor, pending, reason);
                continue;
            }

            var boundaryTick = advancingTick ? Tick - 1 : Tick;
            var startTick = IsOffensive(pending)
                ? Math.Max(boundaryTick, actor.OffensiveRecoveryUntilTick)
                : boundaryTick;
            StartPrimaryAction(actor, pending, startTick);
        }
    }

    private static CommandRejectionCode? ValidatePendingAction(
        StationRouteRuntime station, ActorRuntime actor, PrimaryActionRuntime action)
    {
        if (action.Kind is PrimaryActionKind.Attack or PrimaryActionKind.Ability or PrimaryActionKind.Item)
        {
            if (station.Combat.Phase != EncounterPhase.Active)
            {
                return CommandRejectionCode.CombatInactive;
            }

            if (actor.Health <= 0 || station.Combat.Hostile.Health <= 0)
            {
                return CommandRejectionCode.CombatantDefeated;
            }
        }

        if (action.Kind == PrimaryActionKind.Ability)
        {
            var ability = station.Definition.Combat.ProtagonistAbility;
            if (actor.Cooldowns.GetValueOrDefault(ability.Id) > 0)
            {
                return CommandRejectionCode.AbilityOnCooldown;
            }

            if (actor.Position.DistanceTo(action.AbilityTargetPosition) > ability.RangeMeters)
            {
                return CommandRejectionCode.AbilityTargetOutOfRange;
            }
        }

        if (action.Kind == PrimaryActionKind.Item)
        {
            if (actor.ItemCharges.GetValueOrDefault(station.Definition.Combat.HealingItem.Id) <= 0)
            {
                return CommandRejectionCode.ItemUnavailable;
            }

            if (actor.Health >= actor.MaximumHealth)
            {
                return CommandRejectionCode.NoHealingRequired;
            }
        }

        return null;
    }

    private void ResumeRememberedAttack(StationRouteRuntime station, ActorRuntime actor)
    {
        if (station.Combat.Phase != EncounterPhase.Active || actor.Health <= 0
            || actor.PendingAction is not null || Tick < actor.OffensiveRecoveryUntilTick
            || actor.RememberedAttackTargetId != station.Combat.Hostile.Id
            || station.Combat.Hostile.Health <= 0
            || actor.RememberedAttackCommandId is not CommandId commandId)
        {
            return;
        }

        var action = new PrimaryActionRuntime(
            commandId, PrimaryActionKind.Attack, station.Combat.Hostile.Position, null, [])
        {
            CombatTargetId = station.Combat.Hostile.Id,
            AttackId = actor.Loadout!.BasicAttackId,
            InstanceId = ++_actionSequence,
        };
        StartPrimaryAction(actor, action, Tick);
    }

    private static void ClearAttackIntent(ActorRuntime actor)
    {
        actor.RememberedAttackTargetId = null;
        actor.RememberedAttackCommandId = null;
    }
}
