namespace SpaceAdventure.Core;

public sealed partial class GameSession
{
    private static void InitializeProtagonistCombatState(StationRouteRuntime station) =>
        InitializeActorCombatState(station, station.Protagonist);

    private static void InitializeActorCombatState(StationRouteRuntime station, ActorRuntime actor)
    {
        var combat = station.Definition.Combat;
        var protagonist = actor.Id == station.Protagonist.Id;
        actor.MaximumHealth = protagonist ? combat.SoloEncounter.ProtagonistMaximumHealth : combat.CompanionMaximumHealth;
        actor.Health = actor.MaximumHealth;
        foreach (var abilityId in new[] { actor.Loadout!.ActiveAbilityId, actor.Loadout.SecondaryAbilityId })
        { actor.Cooldowns[abilityId] = 0; actor.CooldownTotals[abilityId] = combat.AbilityCooldownTicks(abilityId); }
    }

    private CommandAcknowledgement Execute(AssignBasicAttackTargetCommand command)
    {
        if (!TryValidateCombatOrder(command.ActorId, out var station, out var actor, out var rejection))
        {
            return Reject(command.CommandId, rejection);
        }

        if (!station.Combat.Hostiles.TryGetValue(command.TargetId, out var hostile))
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

        if (command.AbilityId == station.Definition.Combat.Barrier.Id)
        {
            return ExecuteBarrier(command, station, actor);
        }
        if (command.AbilityId == station.Definition.Combat.Burst.Id || command.AbilityId == station.Definition.Combat.Taunt.Id)
        { return ExecuteSecondaryAbility(command, station, actor); }
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

    private CommandAcknowledgement Execute(RestartEncounterCommand command)
    {
        if (_stationRoute is null)
        {
            return Reject(command.CommandId, CommandRejectionCode.UnknownCommand);
        }

        var station = _stationRoute;
        var combat = station.Combat;
        if (command.EncounterId != combat.Definition.Id)
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
        if (combat.Phase == EncounterPhase.Victory && !combat.Definition.RequiresCompanion
            && station.CurrentObjective.Id == station.Definition.MainCombatObjective.Id
            && station.PartyCombat is { } party
            && station.Actors.Count == 2
            && station.Actors.Values.All(actor => actor.Position.DistanceTo(party.Placement.TriggerCenter) <= party.Placement.TriggerRadiusMeters))
        {
            station.Combat = combat = party;
        }
        else if (combat.Phase != EncounterPhase.Dormant
            || station.CurrentObjective.Id != station.Definition.CombatThresholdObjective.Id
            || station.Protagonist.Position.DistanceTo(combat.Placement.TriggerCenter) > combat.Placement.TriggerRadiusMeters)
        {
            return;
        }

        combat.Attempt = 1;
        ResetEncounterAttempt(station);
        var commandId = new CommandId($"system.{combat.Definition.Id}.start");
        if (!combat.Definition.RequiresCompanion) { ChangeObjective(station, commandId, station.Definition.CombatObjective); }
        IsPaused = true;
        _accumulatedSeconds = 0;
        Record(GameplayEventType.EncounterStarted, detail: new EncounterEventDetail(combat.Definition.Id, combat.Attempt));
        Record(GameplayEventType.PauseChanged, paused: true);
    }

    private void ResetEncounterAttempt(StationRouteRuntime station)
    {
        var combat = station.Combat;
        combat.Attempt = Math.Max(1, combat.Attempt + (combat.Phase == EncounterPhase.Defeat ? 1 : 0));
        EndBarrier(station, BarrierEndReason.EncounterEnded);
        combat.Projectiles.Clear();
        combat.Phase = EncounterPhase.Readying;
        combat.PhaseStartedTick = Tick;
        combat.TransitionTicksTotal = combat.Definition.ReadyingTicks;
        combat.TransitionTicksRemaining = combat.TransitionTicksTotal;
        foreach (var actor in station.Actors.Values)
        {
            actor.Position = actor.Id == station.Protagonist.Id
                ? combat.Placement.ProtagonistRestartPosition : combat.Placement.CompanionRestartPosition!.Value;
            actor.MaximumHealth = actor.Id == station.Protagonist.Id
                ? combat.Definition.ProtagonistMaximumHealth : station.Definition.Combat.CompanionMaximumHealth;
            actor.Health = actor.MaximumHealth;
            actor.DefeatedAtTick = null;
            actor.HeldFacing = null;
            actor.Facing = DirectionTo(actor.Position, combat.Placement.HostileSpawnPosition) ?? new WorldPosition(0, 0, 1);
            actor.CurrentAction = null;
            actor.PendingAction = null;
            ClearAttackIntent(actor);
            actor.OffensiveRecoveryUntilTick = 0;
            foreach (var abilityId in actor.Cooldowns.Keys.ToArray()) { actor.Cooldowns[abilityId] = 0; }
        }
        foreach (var hostile in combat.Hostiles.Values)
        {
            hostile.TauntedBy = null; hostile.TauntedUntilTick = 0;
            hostile.Position = hostile.SpawnPosition;
            hostile.Health = hostile.MaximumHealth;
            hostile.CurrentAction = null;
            hostile.Waypoints = [];
            hostile.WaypointIndex = 0;
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
                combat.PhaseStartedTick = Tick;
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
            combat.PhaseStartedTick = Tick;
            var systemCommand = new CommandId($"system.{combat.Definition.Id}.victory");
            if (!combat.Definition.RequiresCompanion) { ChangeObjective(station, systemCommand, station.Definition.SoloExitDoorObjective); }
            Record(
                GameplayEventType.EncounterWon,
                detail: new EncounterEventDetail(combat.Definition.Id, combat.Attempt));
        }

        return true;
    }

    private static void AdvanceCombatCooldowns(StationRouteRuntime station)
    {
        foreach (var actor in station.Actors.Values)
        foreach (var abilityId in actor.Cooldowns.Keys.ToArray())
        {
            actor.Cooldowns[abilityId] = Math.Max(0, actor.Cooldowns[abilityId] - 1);
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
        }
    }

    private void AdvancePartyAttack(
        StationRouteRuntime station,
        ActorRuntime actor,
        PrimaryActionRuntime action)
    {
        if (action.CombatTargetId is not EntityId targetId
            || !station.Combat.Hostiles.TryGetValue(targetId, out var hostile)
            || hostile.Health <= 0 || action.AttackId is not AttackId attackId)
        {
            actor.CurrentAction = null;
            ClearAttackIntent(actor);
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
                    ClearAttackIntent(actor);
                    RecordPrimaryActionFailure(actor, action, CommandRejectionCode.DestinationUnreachable);
                }

                return;
            }

            BeginAttackWindup(actor.Id, hostile.Id, attack, action);
            return;
        }

        if (action.Phase == PrimaryActionPhase.Windup)
        {
            UpdatePhaseRemaining(action);
            if (action.PhaseTicksRemaining > 0)
            {
                return;
            }

            var hit = actor.Position.DistanceTo(hostile.Position) <= attack.RangeMeters;
            actor.OffensiveRecoveryUntilTick = Tick + attack.RecoveryTicks;
            Record(
                GameplayEventType.AttackReleased,
                action.CommandId,
                detail: new AttackEventDetail(actor.Id, hostile.Id, attack.Id, hit));
            if (hit)
            {
                DamageHostile(station, hostile, actor.Id, attack.Damage, attack.Id, abilityId: null);
            }

            if (station.Combat.Phase == EncounterPhase.Securing)
            {
                return;
            }

            BeginRecovery(action, attack.RecoveryTicks);
            return;
        }

        UpdatePhaseRemaining(action);
        if (action.PhaseTicksRemaining <= 0)
        {
            action.Phase = PrimaryActionPhase.Moving;
            action.PhaseTicksRemaining = 0;
            action.PhaseTicksTotal = 0;
            AdvancePartyAttack(station, actor, action);
        }
    }

    private void AdvancePartyAbility(
        StationRouteRuntime station,
        ActorRuntime actor,
        PrimaryActionRuntime action)
    {
        if (action.AbilityId == station.Definition.Combat.Barrier.Id)
        {
            AdvanceBarrierAction(station, actor, action);
            return;
        }
        if (action.AbilityId == station.Definition.Combat.Burst.Id || action.AbilityId == station.Definition.Combat.Taunt.Id)
        { AdvanceSecondaryAbility(station, actor, action); return; }
        var ability = station.Definition.Combat.ProtagonistAbility;
        if (action.AbilityId != ability.Id)
        {
            actor.CurrentAction = null;
            return;
        }

        if (action.Phase == PrimaryActionPhase.Windup)
        {
            UpdatePhaseRemaining(action);
            if (action.PhaseTicksRemaining > 0)
            {
                return;
            }

            var rejection = ValidatePendingAction(station, actor, action);
            if (rejection is { } reason)
            { actor.CurrentAction = null; RecordPrimaryActionFailure(actor, action, reason); ResumeRememberedAttack(station, actor); return; }
            actor.Cooldowns[ability.Id] = ability.CooldownTicks;
            actor.OffensiveRecoveryUntilTick = Tick + ability.RecoveryTicks;
            var hits = station.Combat.Hostiles.Values.Where(hostile => hostile.Health > 0
                && hostile.Position.DistanceTo(action.AbilityTargetPosition) <= ability.RadiusMeters).ToArray();
            var hit = hits.Length > 0;
            Record(
                GameplayEventType.AbilityReleased,
                action.CommandId,
                detail: new AbilityReleasedEventDetail(
                    actor.Id,
                    action.AbilityTargetPosition,
                    ability.Id,
                    hit));
            foreach (var hostile in hits)
            {
                DamageHostile(station, hostile, actor.Id, ability.Damage, attackId: null, ability.Id);
                if (ability.InterruptsWindup && hostile.Health > 0
                    && station.Combat.Phase == EncounterPhase.Active
                    && hostile.CurrentAction?.Phase == PrimaryActionPhase.Windup)
                {
                    hostile.CurrentAction.Phase = PrimaryActionPhase.Recovery;
                    hostile.CurrentAction.PhaseTicksRemaining =
                        station.Definition.Combat.GetAttack(hostile.BasicAttackId).RecoveryTicks;
                    hostile.CurrentAction.PhaseTicksTotal = hostile.CurrentAction.PhaseTicksRemaining;
                    hostile.CurrentAction.PhaseStartedTick = Tick;
                    hostile.CurrentAction.Interrupted = true;
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

        UpdatePhaseRemaining(action);
        if (action.PhaseTicksRemaining <= 0)
        {
            actor.CurrentAction = null;
            ResumeRememberedAttack(station, actor);
        }
    }

    private void AdvanceHostileCombat(StationRouteRuntime station)
    {
        foreach (var hostile in station.Combat.Hostiles.Values.OrderBy(hostile => hostile.Id.Value, StringComparer.Ordinal))
        {
            if (station.Combat.Phase != EncounterPhase.Active) { break; }
            AdvanceHostile(station, hostile);
        }
    }

    private void AdvanceHostile(StationRouteRuntime station, HostileRuntime hostile)
    {
        if (hostile.Health <= 0) { return; }
        var attack = station.Definition.Combat.GetAttack(hostile.BasicAttackId);
        hostile.CurrentAction ??= new HostileAttackRuntime { Phase = PrimaryActionPhase.Moving, PhaseStartedTick = Tick };
        var action = hostile.CurrentAction;
        if (action.Phase == PrimaryActionPhase.Moving)
        {
            var candidates = station.Actors.Values.Where(actor => actor.Health > 0);
            // Sentries pressure the back line while melee closes on the nearest crew;
            // Taunt overrides either preference until it expires.
            var target = hostile.TauntedBy is { } taunter ? station.Actors[taunter] : hostile.Behavior == HostileBehavior.Sentry
                ? candidates.Where(actor => CanSentryHit(hostile, actor.Position, attack.RangeMeters))
                    .OrderByDescending(actor => actor.Position.DistanceTo(hostile.Position)).ThenBy(actor => actor.PartyOrder).FirstOrDefault()
                : candidates.OrderBy(actor => actor.Position.DistanceTo(hostile.Position)).ThenBy(actor => actor.PartyOrder).FirstOrDefault();
            if (target is null) { action.TargetId = null; return; }
            action.TargetId = target.Id;
            if (hostile.Behavior == HostileBehavior.Sentry && !CanSentryHit(hostile, target.Position, attack.RangeMeters)) { return; }
            if (hostile.Position.DistanceTo(target.Position) > attack.RangeMeters)
            {
                AdvanceHostileToward(hostile, target.Position);
                return;
            }
            action.Phase = PrimaryActionPhase.Windup;
            action.PhaseTicksRemaining = action.PhaseTicksTotal = attack.WindupTicks;
            action.PhaseStartedTick = Tick;
            action.InstanceId = ++_actionSequence;
            action.Interrupted = false;
            Record(GameplayEventType.AttackWindupStarted, detail: new AttackEventDetail(hostile.Id, target.Id, attack.Id, false));
            return;
        }

        action.PhaseTicksRemaining = (int)Math.Max(0, action.PhaseStartedTick + action.PhaseTicksTotal - Tick);
        if (action.PhaseTicksRemaining > 0) { return; }
        if (action.Phase == PrimaryActionPhase.Windup && action.TargetId is EntityId targetId)
        {
            var target = station.Actors[targetId];
            var hit = target.Health > 0 && (hostile.Behavior == HostileBehavior.Sentry
                ? CanSentryHit(hostile, target.Position, attack.RangeMeters)
                : hostile.Position.DistanceTo(target.Position) <= attack.RangeMeters);
            Record(GameplayEventType.AttackReleased, detail: new AttackEventDetail(hostile.Id, target.Id, attack.Id, hit));
            if (hit)
            {
                if (hostile.Behavior == HostileBehavior.Sentry) { LaunchSentryProjectile(station, hostile, target, attack); }
                else { DamagePartyActor(station, target, hostile.Id, attack.Damage, attack.Id); }
            }
            if (station.Combat.Phase != EncounterPhase.Active) { return; }
            action.Phase = PrimaryActionPhase.Recovery;
            action.PhaseTicksRemaining = action.PhaseTicksTotal = attack.RecoveryTicks;
            action.PhaseStartedTick = Tick;
        }
        else
        {
            action.Phase = PrimaryActionPhase.Moving;
            action.PhaseTicksRemaining = action.PhaseTicksTotal = 0;
            AdvanceHostile(station, hostile);
        }
    }

    private static bool CanSentryHit(HostileRuntime hostile, WorldPosition target, double range)
    {
        if (hostile.Position.DistanceTo(target) > range) { return false; }
        var x = target.X - hostile.Position.X;
        var z = target.Z - hostile.Position.Z;
        var distance = Math.Sqrt(x * x + z * z);
        return distance > 0.01 && (x * hostile.Forward.X + z * hostile.Forward.Z) / distance >= 0.5;
    }

    private void BeginAttackWindup(
        EntityId sourceId,
        EntityId targetId,
        AttackDefinition attack,
        PrimaryActionRuntime action,
        long? startedTick = null)
    {
        action.Phase = PrimaryActionPhase.Windup;
        action.PhaseTicksRemaining = attack.WindupTicks;
        action.PhaseTicksTotal = attack.WindupTicks;
        action.PhaseStartedTick = startedTick ?? Tick;
        action.InstanceId = ++_actionSequence;
        action.Waypoints = [];
        action.WaypointIndex = 0;
        Record(
            GameplayEventType.AttackWindupStarted,
            action.CommandId,
            detail: new AttackEventDetail(sourceId, targetId, attack.Id, Hit: false));
    }

    private void BeginRecovery(PrimaryActionRuntime action, int ticks)
    {
        action.Phase = PrimaryActionPhase.Recovery;
        action.PhaseTicksRemaining = ticks;
        action.PhaseTicksTotal = ticks;
        action.PhaseStartedTick = Tick;
    }

    private void UpdatePhaseRemaining(PrimaryActionRuntime action) =>
        action.PhaseTicksRemaining = (int)Math.Max(0, action.PhaseStartedTick + action.PhaseTicksTotal - Tick);

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
        HostileRuntime hostile,
        EntityId sourceId,
        int amount,
        AttackId? attackId,
        AbilityId? abilityId)
    {
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
            hostile.TauntedBy = null; hostile.TauntedUntilTick = 0;
            Record(
                GameplayEventType.CombatantDefeated,
                detail: new CombatantDefeatedEventDetail(hostile.Id, sourceId));
            hostile.CurrentAction = null;
            foreach (var actor in station.Actors.Values.Where(actor => actor.RememberedAttackTargetId == hostile.Id))
            {
                ClearAttackIntent(actor);
                if (actor.CurrentAction?.Kind == PrimaryActionKind.Attack) { actor.CurrentAction = null; }
            }
            if (station.Combat.Hostiles.Values.All(enemy => enemy.Health <= 0)) { BeginSecuring(station); }
        }
    }

    private void DamagePartyActor(StationRouteRuntime station, ActorRuntime actor, EntityId sourceId, int amount, AttackId attackId)
    {
        ApplyActorDamage(actor, sourceId, amount, attackId);
        ValidateBarrierLifetime(station); ValidateTaunts(station);
        if (station.Actors.Values.All(member => member.Health <= 0))
        {
            station.Combat.Phase = EncounterPhase.Defeat;
            station.Combat.PhaseStartedTick = Tick;
            EndBarrier(station, BarrierEndReason.EncounterEnded);
            station.Combat.Projectiles.Clear();
            foreach (var hostile in station.Combat.Hostiles.Values) { hostile.CurrentAction = null; }
            Record(GameplayEventType.EncounterDefeated, detail: new EncounterEventDetail(station.Combat.Definition.Id, station.Combat.Attempt));
            if (!IsPaused)
            {
                IsPaused = true;
                _accumulatedSeconds = 0;
                Record(GameplayEventType.PauseChanged, paused: true);
            }
        }
    }

    private void ApplyActorDamage(ActorRuntime actor, EntityId sourceId, int amount, AttackId attackId)
    {
        if (actor.Health <= 0 || amount <= 0) { return; }
        actor.Health = Math.Max(0, actor.Health - amount);
        Record(GameplayEventType.DamageApplied,
            detail: new DamageAppliedEventDetail(sourceId, actor.Id, amount, actor.Health, attackId, null));
        if (actor.Health == 0)
        {
            actor.DefeatedAtTick = Tick;
            actor.CurrentAction = actor.PendingAction = null;
            ClearAttackIntent(actor);
            Record(GameplayEventType.CombatantDefeated, detail: new CombatantDefeatedEventDetail(actor.Id, sourceId));
        }
    }

    private void BeginSecuring(StationRouteRuntime station)
    {
        EndBarrier(station, BarrierEndReason.EncounterEnded);
        station.Combat.Projectiles.Clear();
        station.Combat.Phase = EncounterPhase.Securing;
        station.Combat.PhaseStartedTick = Tick;
        station.Combat.TransitionTicksTotal = station.Combat.Definition.SecuringTicks;
        station.Combat.TransitionTicksRemaining = station.Combat.TransitionTicksTotal;
        foreach (var hostile in station.Combat.Hostiles.Values) { hostile.CurrentAction = null; }
        foreach (var actor in station.Actors.Values)
        {
            actor.CurrentAction = actor.PendingAction = null;
            ClearAttackIntent(actor);
            actor.OffensiveRecoveryUntilTick = 0;
        }
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

    private HostileObservation ObserveHostile(StationRouteRuntime station, HostileRuntime hostile)
    {
        var action = hostile.CurrentAction;
        var targetPosition = action?.TargetId is EntityId targetId ? station.Actors[targetId].Position : hostile.Position;
        PrimaryActionObservation? observedAction = action is null
            ? null
            : new PrimaryActionObservation(
                new CommandId($"system.{hostile.Id}.attack"),
                PrimaryActionKind.Attack,
                targetPosition,
                action.Phase == PrimaryActionPhase.Moving,
                InteractionTargetId: null,
                CombatTargetId: action.TargetId,
                AttackId: hostile.BasicAttackId,
                Phase: action.Phase,
                PhaseTicksRemaining: action.PhaseTicksRemaining,
                PhaseTicksTotal: action.PhaseTicksTotal,
                InstanceId: action.InstanceId,
                PhaseStartedTick: action.PhaseStartedTick,
                Interrupted: action.Interrupted);
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
                TauntedBy: hostile.TauntedBy,
                TauntRemainingTicks: (int)Math.Max(0, hostile.TauntedUntilTick - Tick)),
            observedAction);
    }

    private sealed class CombatEncounterRuntime
    {
        public CombatEncounterRuntime(StationCombatDefinition combat, EncounterDefinition definition, StationEncounterPlacement placement)
        {
            Definition = definition;
            Placement = placement;
            Hostiles = definition.HostileIds.ToDictionary(id => id, id => new HostileRuntime(combat.GetHostile(id),
                id == definition.HostileIds[0] ? placement.HostileSpawnPosition
                    : placement.AdditionalHostiles!.Single(actor => actor.ActorId == id).Position,
                placement.SentryForward ?? new WorldPosition(0, 0, -1)));
        }
        public EncounterDefinition Definition { get; }
        public StationEncounterPlacement Placement { get; }
        public Dictionary<EntityId, HostileRuntime> Hostiles { get; }
        public BarrierRuntime? Barrier { get; set; }
        public List<ProjectileRuntime> Projectiles { get; } = [];
        public EncounterPhase Phase { get; set; } = EncounterPhase.Dormant;
        public int Attempt { get; set; }
        public int TransitionTicksRemaining { get; set; }
        public int TransitionTicksTotal { get; set; }
        public long PhaseStartedTick { get; set; }
    }

    private sealed class HostileRuntime(HostileDefinition definition, WorldPosition position, WorldPosition forward)
    {
        public EntityId Id { get; } = definition.Id;
        public string DisplayName { get; } = definition.DisplayName;
        public double MovementSpeedMetersPerSecond { get; } = definition.MovementSpeedMetersPerSecond;
        public int MaximumHealth { get; } = definition.MaximumHealth;
        public int Health { get; set; } = definition.MaximumHealth;
        public AttackId BasicAttackId { get; } = definition.BasicAttackId;
        public HostileBehavior Behavior { get; } = definition.Behavior;
        public WorldPosition Position { get; set; } = position;
        public WorldPosition SpawnPosition { get; } = position;
        public WorldPosition Forward { get; } = forward;
        public EntityId? TauntedBy { get; set; }
        public long TauntedUntilTick { get; set; }
        public HostileAttackRuntime? CurrentAction { get; set; }
        public IReadOnlyList<WorldPosition> Waypoints { get; set; } = [];
        public int WaypointIndex { get; set; }
    }

    private sealed class HostileAttackRuntime
    {
        public long InstanceId { get; set; }
        public long PhaseStartedTick { get; set; }
        public bool Interrupted { get; set; }
        public EntityId? TargetId { get; set; }
        public PrimaryActionPhase Phase { get; set; }
        public int PhaseTicksRemaining { get; set; }
        public int PhaseTicksTotal { get; set; }
    }
}
