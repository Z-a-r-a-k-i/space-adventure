namespace SpaceAdventure.Core;

public sealed partial class GameSession
{
    private static void InitializeProtagonistCombatState(StationRouteRuntime station)
    {
        var actor = station.Protagonist;
        actor.MaximumHealth = station.Definition.Combat.Encounter.ProtagonistMaximumHealth;
        actor.Health = actor.MaximumHealth;
        actor.Cooldowns[station.Definition.Combat.ProtagonistAbility.Id] = 0;
        actor.CooldownTotals[station.Definition.Combat.ProtagonistAbility.Id] =
            station.Definition.Combat.ProtagonistAbility.CooldownTicks;
        actor.ItemCharges[station.Definition.Combat.HealingItem.Id] =
            station.Definition.Combat.HealingItem.InitialCharges;
    }

    private CommandAcknowledgement Execute(AssignBasicAttackTargetCommand command)
    {
        if (!TryValidateCombatOrder(command.ActorId, out var station, out var actor, out var rejection))
        {
            return Reject(command.CommandId, rejection);
        }

        var hostile = station.Combat.Hostile;
        if (command.TargetId != hostile.Id)
        {
            return Reject(command.CommandId, CommandRejectionCode.UnknownCombatTarget);
        }

        if (hostile.Health <= 0)
        {
            return Reject(command.CommandId, CommandRejectionCode.CombatantDefeated);
        }

        var attackId = actor.Loadout!.BasicAttackId;
        if (!station.Definition.Combat.Attacks.Any(attack => attack.Id == attackId))
        {
            return Reject(command.CommandId, CommandRejectionCode.UnknownAttack);
        }

        AssignPrimaryAction(
            actor,
            new PrimaryActionRuntime(
                command.CommandId,
                PrimaryActionKind.Attack,
                hostile.Position,
                interactionTargetId: null,
                waypoints: [])
            {
                CombatTargetId = hostile.Id,
                AttackId = attackId,
                Phase = PrimaryActionPhase.Moving,
            });
        return Accept(command.CommandId);
    }

    private CommandAcknowledgement Execute(UseAbilityCommand command)
    {
        ArgumentNullException.ThrowIfNull(command.Target);
        if (!TryValidateCombatOrder(command.ActorId, out var station, out var actor, out var rejection))
        {
            return Reject(command.CommandId, rejection);
        }

        var ability = station.Definition.Combat.ProtagonistAbility;
        if (command.AbilityId != ability.Id || actor.Loadout!.ActiveAbilityId != ability.Id)
        {
            return Reject(command.CommandId, CommandRejectionCode.UnknownAbility);
        }

        if (command.Target.Kind != ability.TargetKind
            || command.Target is not PositionAbilityTarget positionTarget)
        {
            return Reject(command.CommandId, CommandRejectionCode.AbilityTargetKindMismatch);
        }

        if (!positionTarget.Position.IsFinite
            || actor.Position.DistanceTo(positionTarget.Position) > ability.RangeMeters)
        {
            return Reject(command.CommandId, CommandRejectionCode.AbilityTargetOutOfRange);
        }

        if (actor.Cooldowns.GetValueOrDefault(ability.Id) > 0)
        {
            return Reject(command.CommandId, CommandRejectionCode.AbilityOnCooldown);
        }

        AssignPrimaryAction(
            actor,
            new PrimaryActionRuntime(
                command.CommandId,
                PrimaryActionKind.Ability,
                positionTarget.Position,
                interactionTargetId: null,
                waypoints: [])
            {
                AbilityId = ability.Id,
                AbilityTargetPosition = positionTarget.Position,
                Phase = PrimaryActionPhase.Windup,
                PhaseTicksRemaining = ability.WindupTicks,
                PhaseTicksTotal = ability.WindupTicks,
            });
        return Accept(command.CommandId);
    }

    private CommandAcknowledgement Execute(UseItemCommand command)
    {
        if (!TryValidateCombatOrder(command.ActorId, out var station, out var actor, out var rejection))
        {
            return Reject(command.CommandId, rejection);
        }

        var item = station.Definition.Combat.HealingItem;
        if (command.ItemId != item.Id)
        {
            return Reject(command.CommandId, CommandRejectionCode.UnknownItem);
        }

        if (command.TargetActorId != actor.Id)
        {
            return Reject(command.CommandId, CommandRejectionCode.InvalidItemTarget);
        }

        if (actor.ItemCharges.GetValueOrDefault(item.Id) <= 0)
        {
            return Reject(command.CommandId, CommandRejectionCode.ItemUnavailable);
        }

        if (actor.Health >= actor.MaximumHealth)
        {
            return Reject(command.CommandId, CommandRejectionCode.NoHealingRequired);
        }

        AssignPrimaryAction(
            actor,
            new PrimaryActionRuntime(
                command.CommandId,
                PrimaryActionKind.Item,
                actor.Position,
                interactionTargetId: null,
                waypoints: [])
            {
                CombatTargetId = actor.Id,
                ItemId = item.Id,
                Phase = PrimaryActionPhase.Windup,
                PhaseTicksRemaining = item.WindupTicks,
                PhaseTicksTotal = item.WindupTicks,
            });
        return Accept(command.CommandId);
    }

    private CommandAcknowledgement Execute(RestartEncounterCommand command)
    {
        if (_stationRoute is null)
        {
            return Reject(command.CommandId, CommandRejectionCode.UnknownCommand);
        }

        var station = _stationRoute;
        var combat = station.Combat;
        if (command.EncounterId != station.Definition.Combat.Encounter.Id)
        {
            return Reject(command.CommandId, CommandRejectionCode.UnknownEncounter);
        }

        if (combat.Phase != EncounterPhase.Defeat)
        {
            return Reject(command.CommandId, CommandRejectionCode.InvalidEncounterState);
        }

        ResetEncounterAttempt(station);
        if (!IsPaused)
        {
            IsPaused = true;
            _accumulatedSeconds = 0;
            Record(GameplayEventType.PauseChanged, command.CommandId, paused: true);
        }

        Record(
            GameplayEventType.EncounterRestarted,
            command.CommandId,
            detail: new EncounterEventDetail(combat.Definition.Id, combat.Attempt));
        return Accept(command.CommandId);
    }

    private bool TryValidateCombatOrder(
        EntityId actorId,
        out StationRouteRuntime station,
        out ActorRuntime actor,
        out CommandRejectionCode rejection)
    {
        if (!TryValidatePrimaryOrder(actorId, out station, out actor, out rejection))
        {
            return false;
        }

        if (station.Combat.Phase is not (EncounterPhase.Readying or EncounterPhase.Active))
        {
            rejection = CommandRejectionCode.CombatInactive;
            return false;
        }

        if (actor.Health <= 0)
        {
            rejection = CommandRejectionCode.CombatantDefeated;
            return false;
        }

        return true;
    }

    private void TryStartEncounter(StationRouteRuntime station)
    {
        var combat = station.Combat;
        if (station.CurrentObjective.Id != station.Definition.CombatThresholdObjective.Id
            || station.Protagonist.Position.DistanceTo(combat.Placement.TriggerCenter)
                > combat.Placement.TriggerRadiusMeters)
        {
            return;
        }

        combat.Attempt = 1;
        ResetEncounterAttempt(station);
        var systemCommand = new CommandId("system.encounter.solo_tutorial.start");
        ChangeObjective(station, systemCommand, station.Definition.CombatObjective);
        IsPaused = true;
        _accumulatedSeconds = 0;
        Record(
            GameplayEventType.EncounterStarted,
            detail: new EncounterEventDetail(combat.Definition.Id, combat.Attempt));
        Record(GameplayEventType.PauseChanged, paused: true);
    }

    private static void ResetEncounterAttempt(StationRouteRuntime station)
    {
        var combat = station.Combat;
        var restarting = combat.Phase == EncounterPhase.Defeat;
        combat.Attempt = Math.Max(1, combat.Attempt + (restarting ? 1 : 0));
        combat.Phase = EncounterPhase.Readying;
        combat.TransitionTicksTotal = combat.Definition.ReadyingTicks;
        combat.TransitionTicksRemaining = combat.TransitionTicksTotal;
        station.Protagonist.Position = combat.Placement.ProtagonistRestartPosition;
        station.Protagonist.Health = station.Protagonist.MaximumHealth;
        station.Protagonist.CurrentAction = null;
        station.Protagonist.PendingAction = null;
        foreach (var abilityId in station.Protagonist.Cooldowns.Keys.ToArray())
        {
            station.Protagonist.Cooldowns[abilityId] = 0;
        }

        station.Protagonist.ItemCharges[station.Definition.Combat.HealingItem.Id] =
            station.Definition.Combat.HealingItem.InitialCharges;
        combat.Hostile.Position = combat.Placement.HostileSpawnPosition;
        combat.Hostile.Health = combat.Hostile.MaximumHealth;
        combat.Hostile.CurrentAction = null;
        combat.Hostile.Waypoints = [];
        combat.Hostile.WaypointIndex = 0;
        if (restarting)
        {
            station.CurrentObjective = station.Definition.CombatObjective;
        }
    }

    private bool AdvanceEncounterTransition(StationRouteRuntime station)
    {
        var combat = station.Combat;
        if (combat.Phase == EncounterPhase.Readying)
        {
            combat.TransitionTicksRemaining = Math.Max(0, combat.TransitionTicksRemaining - 1);
            if (combat.TransitionTicksRemaining == 0)
            {
                combat.Phase = EncounterPhase.Active;
            }

            return true;
        }

        if (combat.Phase != EncounterPhase.Securing)
        {
            return false;
        }

        combat.TransitionTicksRemaining = Math.Max(0, combat.TransitionTicksRemaining - 1);
        if (combat.TransitionTicksRemaining == 0)
        {
            combat.Phase = EncounterPhase.Victory;
            var systemCommand = new CommandId("system.encounter.solo_tutorial.victory");
            ChangeObjective(station, systemCommand, station.Definition.SoloExitDoorObjective);
            Record(
                GameplayEventType.EncounterWon,
                detail: new EncounterEventDetail(combat.Definition.Id, combat.Attempt));
        }

        return true;
    }

    private static void AdvanceCombatCooldowns(StationRouteRuntime station)
    {
        foreach (var abilityId in station.Protagonist.Cooldowns.Keys.ToArray())
        {
            station.Protagonist.Cooldowns[abilityId] =
                Math.Max(0, station.Protagonist.Cooldowns[abilityId] - 1);
        }
    }

    private void AdvancePartyCombatAction(
        StationRouteRuntime station,
        ActorRuntime actor,
        PrimaryActionRuntime action)
    {
        if (station.Combat.Phase != EncounterPhase.Active || actor.Health <= 0)
        {
            return;
        }

        switch (action.Kind)
        {
            case PrimaryActionKind.Attack:
                AdvancePartyAttack(station, actor, action);
                break;
            case PrimaryActionKind.Ability:
                AdvancePartyAbility(station, actor, action);
                break;
            case PrimaryActionKind.Item:
                AdvancePartyItem(station, actor, action);
                break;
        }
    }

    private void AdvancePartyAttack(
        StationRouteRuntime station,
        ActorRuntime actor,
        PrimaryActionRuntime action)
    {
        var hostile = station.Combat.Hostile;
        if (action.CombatTargetId != hostile.Id || hostile.Health <= 0 || action.AttackId is not AttackId attackId)
        {
            actor.CurrentAction = null;
            return;
        }

        var attack = station.Definition.Combat.GetAttack(attackId);
        if (action.Phase == PrimaryActionPhase.Moving)
        {
            if (actor.Position.DistanceTo(hostile.Position) > attack.RangeMeters)
            {
                if (!AdvanceActorToward(actor, action, hostile.Position))
                {
                    actor.CurrentAction = null;
                    RecordPrimaryActionFailure(actor, action, CommandRejectionCode.DestinationUnreachable);
                }

                return;
            }

            BeginAttackWindup(actor.Id, hostile.Id, attack, action);
            return;
        }

        if (action.Phase == PrimaryActionPhase.Windup)
        {
            action.PhaseTicksRemaining--;
            if (action.PhaseTicksRemaining > 0)
            {
                return;
            }

            var hit = actor.Position.DistanceTo(hostile.Position) <= attack.RangeMeters;
            Record(
                GameplayEventType.AttackReleased,
                action.CommandId,
                detail: new AttackEventDetail(actor.Id, hostile.Id, attack.Id, hit));
            if (hit)
            {
                DamageHostile(station, actor.Id, attack.Damage, attack.Id, abilityId: null);
            }

            if (station.Combat.Phase == EncounterPhase.Securing)
            {
                return;
            }

            BeginRecovery(action, attack.RecoveryTicks);
            return;
        }

        action.PhaseTicksRemaining--;
        if (action.PhaseTicksRemaining <= 0)
        {
            action.Phase = PrimaryActionPhase.Moving;
            action.PhaseTicksRemaining = 0;
            action.PhaseTicksTotal = 0;
        }
    }

    private void AdvancePartyAbility(
        StationRouteRuntime station,
        ActorRuntime actor,
        PrimaryActionRuntime action)
    {
        var ability = station.Definition.Combat.ProtagonistAbility;
        if (action.AbilityId != ability.Id)
        {
            actor.CurrentAction = null;
            return;
        }

        if (action.Phase == PrimaryActionPhase.Windup)
        {
            action.PhaseTicksRemaining--;
            if (action.PhaseTicksRemaining > 0)
            {
                return;
            }

            actor.Cooldowns[ability.Id] = ability.CooldownTicks;
            var hostile = station.Combat.Hostile;
            var hit = hostile.Health > 0
                && hostile.Position.DistanceTo(action.AbilityTargetPosition) <= ability.RadiusMeters;
            Record(
                GameplayEventType.AbilityReleased,
                action.CommandId,
                detail: new AbilityReleasedEventDetail(
                    actor.Id,
                    action.AbilityTargetPosition,
                    ability.Id,
                    hit));
            if (hit)
            {
                DamageHostile(station, actor.Id, ability.Damage, attackId: null, ability.Id);
                if (ability.InterruptsWindup
                    && station.Combat.Phase == EncounterPhase.Active
                    && hostile.CurrentAction?.Phase == PrimaryActionPhase.Windup)
                {
                    hostile.CurrentAction.Phase = PrimaryActionPhase.Recovery;
                    hostile.CurrentAction.PhaseTicksRemaining =
                        station.Definition.Combat.GetAttack(hostile.BasicAttackId).RecoveryTicks;
                    hostile.CurrentAction.PhaseTicksTotal = hostile.CurrentAction.PhaseTicksRemaining;
                    Record(
                        GameplayEventType.ActionInterrupted,
                        action.CommandId,
                        detail: new ActionInterruptedEventDetail(hostile.Id, actor.Id, ability.Id));
                }
            }

            if (station.Combat.Phase == EncounterPhase.Securing)
            {
                return;
            }

            BeginRecovery(action, ability.RecoveryTicks);
            return;
        }

        action.PhaseTicksRemaining--;
        if (action.PhaseTicksRemaining <= 0)
        {
            actor.CurrentAction = null;
        }
    }

    private void AdvancePartyItem(
        StationRouteRuntime station,
        ActorRuntime actor,
        PrimaryActionRuntime action)
    {
        var item = station.Definition.Combat.HealingItem;
        if (action.ItemId != item.Id)
        {
            actor.CurrentAction = null;
            return;
        }

        if (action.Phase == PrimaryActionPhase.Windup)
        {
            action.PhaseTicksRemaining--;
            if (action.PhaseTicksRemaining > 0)
            {
                return;
            }

            var previousHealth = actor.Health;
            actor.Health = Math.Min(actor.MaximumHealth, actor.Health + item.Healing);
            actor.ItemCharges[item.Id]--;
            Record(
                GameplayEventType.HealingApplied,
                action.CommandId,
                detail: new HealingAppliedEventDetail(
                    actor.Id,
                    actor.Id,
                    item.Id,
                    actor.Health - previousHealth,
                    actor.Health));
            BeginRecovery(action, item.RecoveryTicks);
            return;
        }

        action.PhaseTicksRemaining--;
        if (action.PhaseTicksRemaining <= 0)
        {
            actor.CurrentAction = null;
        }
    }

    private void AdvanceHostileCombat(StationRouteRuntime station)
    {
        var hostile = station.Combat.Hostile;
        var target = station.Protagonist;
        if (hostile.Health <= 0 || target.Health <= 0)
        {
            return;
        }

        var attack = station.Definition.Combat.GetAttack(hostile.BasicAttackId);
        hostile.CurrentAction ??= new HostileAttackRuntime
        {
            Phase = PrimaryActionPhase.Moving,
        };
        var action = hostile.CurrentAction;
        if (action.Phase == PrimaryActionPhase.Moving)
        {
            if (hostile.Position.DistanceTo(target.Position) > attack.RangeMeters)
            {
                AdvanceHostileToward(hostile, target.Position);
                return;
            }

            action.Phase = PrimaryActionPhase.Windup;
            action.PhaseTicksRemaining = attack.WindupTicks;
            action.PhaseTicksTotal = attack.WindupTicks;
            Record(
                GameplayEventType.AttackWindupStarted,
                detail: new AttackEventDetail(hostile.Id, target.Id, attack.Id, Hit: false));
            return;
        }

        if (action.Phase == PrimaryActionPhase.Windup)
        {
            action.PhaseTicksRemaining--;
            if (action.PhaseTicksRemaining > 0)
            {
                return;
            }

            var hit = hostile.Position.DistanceTo(target.Position) <= attack.RangeMeters;
            Record(
                GameplayEventType.AttackReleased,
                detail: new AttackEventDetail(hostile.Id, target.Id, attack.Id, hit));
            if (hit)
            {
                DamageProtagonist(station, hostile.Id, attack.Damage, attack.Id);
            }

            if (station.Combat.Phase == EncounterPhase.Defeat)
            {
                return;
            }

            action.Phase = PrimaryActionPhase.Recovery;
            action.PhaseTicksRemaining = attack.RecoveryTicks;
            action.PhaseTicksTotal = attack.RecoveryTicks;
            return;
        }

        action.PhaseTicksRemaining--;
        if (action.PhaseTicksRemaining <= 0)
        {
            action.Phase = PrimaryActionPhase.Moving;
            action.PhaseTicksRemaining = 0;
            action.PhaseTicksTotal = 0;
        }
    }

    private void BeginAttackWindup(
        EntityId sourceId,
        EntityId targetId,
        AttackDefinition attack,
        PrimaryActionRuntime action)
    {
        action.Phase = PrimaryActionPhase.Windup;
        action.PhaseTicksRemaining = attack.WindupTicks;
        action.PhaseTicksTotal = attack.WindupTicks;
        Record(
            GameplayEventType.AttackWindupStarted,
            action.CommandId,
            detail: new AttackEventDetail(sourceId, targetId, attack.Id, Hit: false));
    }

    private static void BeginRecovery(PrimaryActionRuntime action, int ticks)
    {
        action.Phase = PrimaryActionPhase.Recovery;
        action.PhaseTicksRemaining = ticks;
        action.PhaseTicksTotal = ticks;
    }

    private bool AdvanceActorToward(
        ActorRuntime actor,
        PrimaryActionRuntime action,
        WorldPosition destination)
    {
        if (action.WaypointIndex >= action.Waypoints.Count
            || action.Waypoints.Count == 0
            || action.Waypoints[^1].DistanceTo(destination) > 0.5)
        {
            var result = _pathfinder!.FindPath(actor.Id, actor.Position, destination);
            if (!TryNormalizePath(
                    result,
                    actor.Position,
                    endpoint => endpoint.DistanceTo(destination) <= MoveEndpointToleranceMeters,
                    out var waypoints))
            {
                return false;
            }

            action.Waypoints = waypoints;
            action.WaypointIndex = 0;
        }

        var waypointIndex = action.WaypointIndex;
        AdvancePosition(actor, action.Waypoints, ref waypointIndex, actor.MovementSpeedMetersPerSecond);
        action.WaypointIndex = waypointIndex;
        return true;
    }

    private void AdvanceHostileToward(HostileRuntime hostile, WorldPosition destination)
    {
        if (hostile.WaypointIndex >= hostile.Waypoints.Count
            || hostile.Waypoints.Count == 0
            || hostile.Waypoints[^1].DistanceTo(destination) > 0.5)
        {
            var result = _pathfinder!.FindPath(hostile.Id, hostile.Position, destination);
            if (!TryNormalizePath(
                    result,
                    hostile.Position,
                    endpoint => endpoint.DistanceTo(destination) <= MoveEndpointToleranceMeters,
                    out var waypoints))
            {
                return;
            }

            hostile.Waypoints = waypoints;
            hostile.WaypointIndex = 0;
        }

        var waypointIndex = hostile.WaypointIndex;
        AdvancePosition(hostile, hostile.Waypoints, ref waypointIndex);
        hostile.WaypointIndex = waypointIndex;
    }

    private static void AdvancePosition(
        ActorRuntime actor,
        IReadOnlyList<WorldPosition> waypoints,
        ref int waypointIndex,
        double speedMetersPerSecond)
    {
        var position = actor.Position;
        AdvancePositionValue(ref position, waypoints, ref waypointIndex, speedMetersPerSecond);
        actor.Position = position;
    }

    private static void AdvancePosition(
        HostileRuntime hostile,
        IReadOnlyList<WorldPosition> waypoints,
        ref int waypointIndex)
    {
        var position = hostile.Position;
        AdvancePositionValue(
            ref position,
            waypoints,
            ref waypointIndex,
            hostile.MovementSpeedMetersPerSecond);
        hostile.Position = position;
    }

    private static void AdvancePositionValue(
        ref WorldPosition position,
        IReadOnlyList<WorldPosition> waypoints,
        ref int waypointIndex,
        double speedMetersPerSecond)
    {
        var remainingDistance = speedMetersPerSecond / TicksPerSecond;
        while (remainingDistance > 0 && waypointIndex < waypoints.Count)
        {
            var waypoint = waypoints[waypointIndex];
            var distance = position.DistanceTo(waypoint);
            if (distance <= PositionToleranceMeters)
            {
                position = waypoint;
                waypointIndex++;
                continue;
            }

            if (distance <= remainingDistance + PositionToleranceMeters)
            {
                position = waypoint;
                waypointIndex++;
                remainingDistance = Math.Max(0, remainingDistance - distance);
                continue;
            }

            var scale = remainingDistance / distance;
            position = new WorldPosition(
                position.X + ((waypoint.X - position.X) * scale),
                position.Y + ((waypoint.Y - position.Y) * scale),
                position.Z + ((waypoint.Z - position.Z) * scale));
            remainingDistance = 0;
        }
    }

    private void DamageHostile(
        StationRouteRuntime station,
        EntityId sourceId,
        int amount,
        AttackId? attackId,
        AbilityId? abilityId)
    {
        var hostile = station.Combat.Hostile;
        hostile.Health = Math.Max(0, hostile.Health - amount);
        Record(
            GameplayEventType.DamageApplied,
            detail: new DamageAppliedEventDetail(
                sourceId,
                hostile.Id,
                amount,
                hostile.Health,
                attackId,
                abilityId));
        if (hostile.Health == 0)
        {
            Record(
                GameplayEventType.CombatantDefeated,
                detail: new CombatantDefeatedEventDetail(hostile.Id, sourceId));
            BeginSecuring(station);
        }
    }

    private void DamageProtagonist(
        StationRouteRuntime station,
        EntityId sourceId,
        int amount,
        AttackId attackId)
    {
        var actor = station.Protagonist;
        actor.Health = Math.Max(0, actor.Health - amount);
        Record(
            GameplayEventType.DamageApplied,
            detail: new DamageAppliedEventDetail(
                sourceId,
                actor.Id,
                amount,
                actor.Health,
                attackId,
                AbilityId: null));
        if (actor.Health == 0)
        {
            actor.CurrentAction = null;
            actor.PendingAction = null;
            station.Combat.Phase = EncounterPhase.Defeat;
            station.Combat.Hostile.CurrentAction = null;
            Record(
                GameplayEventType.CombatantDefeated,
                detail: new CombatantDefeatedEventDetail(actor.Id, sourceId));
            Record(
                GameplayEventType.EncounterDefeated,
                detail: new EncounterEventDetail(
                    station.Combat.Definition.Id,
                    station.Combat.Attempt));
            if (!IsPaused)
            {
                IsPaused = true;
                _accumulatedSeconds = 0;
                Record(GameplayEventType.PauseChanged, paused: true);
            }
        }
    }

    private static void BeginSecuring(StationRouteRuntime station)
    {
        station.Combat.Phase = EncounterPhase.Securing;
        station.Combat.TransitionTicksTotal = station.Combat.Definition.SecuringTicks;
        station.Combat.TransitionTicksRemaining = station.Combat.TransitionTicksTotal;
        station.Combat.Hostile.CurrentAction = null;
        station.Protagonist.CurrentAction = null;
        station.Protagonist.PendingAction = null;
    }

    private void RecordPrimaryActionFailure(
        ActorRuntime actor,
        PrimaryActionRuntime action,
        CommandRejectionCode reason)
    {
        Record(
            GameplayEventType.PrimaryActionFailed,
            action.CommandId,
            rejectionCode: reason,
            detail: new PrimaryActionFailedEventDetail(action.CommandId, actor.Id, reason));
    }

    private static HostileObservation ObserveHostile(StationRouteRuntime station)
    {
        var hostile = station.Combat.Hostile;
        var action = hostile.CurrentAction;
        PrimaryActionObservation? observedAction = action is null
            ? null
            : new PrimaryActionObservation(
                new CommandId("system.enemy.security_enforcer.attack"),
                PrimaryActionKind.Attack,
                station.Protagonist.Position,
                action.Phase == PrimaryActionPhase.Moving,
                InteractionTargetId: null,
                CombatTargetId: station.Protagonist.Id,
                AttackId: hostile.BasicAttackId,
                Phase: action.Phase,
                PhaseTicksRemaining: action.PhaseTicksRemaining,
                PhaseTicksTotal: action.PhaseTicksTotal);
        return new HostileObservation(
            hostile.Id,
            hostile.DisplayName,
            hostile.Position,
            hostile.MovementSpeedMetersPerSecond,
            new CombatantStateObservation(
                hostile.Health,
                hostile.MaximumHealth,
                hostile.Health <= 0,
                hostile.BasicAttackId,
                Cooldowns: [],
                Items: []),
            observedAction);
    }

    private sealed class CombatEncounterRuntime(
        StationCombatDefinition combat,
        StationEncounterPlacement placement)
    {
        public EncounterDefinition Definition { get; } = combat.Encounter;

        public StationEncounterPlacement Placement { get; } = placement;

        public HostileRuntime Hostile { get; } = new(combat.Hostile, placement.HostileSpawnPosition);

        public EncounterPhase Phase { get; set; } = EncounterPhase.Dormant;

        public int Attempt { get; set; }

        public int TransitionTicksRemaining { get; set; }

        public int TransitionTicksTotal { get; set; }
    }

    private sealed class HostileRuntime(HostileDefinition definition, WorldPosition position)
    {
        public EntityId Id { get; } = definition.Id;

        public string DisplayName { get; } = definition.DisplayName;

        public double MovementSpeedMetersPerSecond { get; } = definition.MovementSpeedMetersPerSecond;

        public int MaximumHealth { get; } = definition.MaximumHealth;

        public int Health { get; set; } = definition.MaximumHealth;

        public AttackId BasicAttackId { get; } = definition.BasicAttackId;

        public WorldPosition Position { get; set; } = position;

        public HostileAttackRuntime? CurrentAction { get; set; }

        public IReadOnlyList<WorldPosition> Waypoints { get; set; } = [];

        public int WaypointIndex { get; set; }
    }

    private sealed class HostileAttackRuntime
    {
        public PrimaryActionPhase Phase { get; set; }

        public int PhaseTicksRemaining { get; set; }

        public int PhaseTicksTotal { get; set; }
    }
}
