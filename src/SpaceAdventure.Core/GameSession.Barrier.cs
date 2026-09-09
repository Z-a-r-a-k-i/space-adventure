namespace SpaceAdventure.Core;

public sealed partial class GameSession
{
    public CommandRejectionCode? CheckBarrierPlacement(EntityId actorId, BarrierAbilityTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return !TryValidateCombatOrder(actorId, out var station, out var actor, out var rejection)
            ? rejection : ValidateBarrierTarget(station, actor, target.Position, target.Facing);
    }

    private CommandAcknowledgement ExecuteBarrier(UseAbilityCommand command, StationRouteRuntime station, ActorRuntime actor)
    {
        if (actor.Loadout!.ActiveAbilityId != command.AbilityId)
        { return Reject(command.CommandId, CommandRejectionCode.UnknownAbility); }
        if (command.Target is not BarrierAbilityTarget target)
        { return Reject(command.CommandId, CommandRejectionCode.AbilityTargetKindMismatch); }
        var rejection = ValidateBarrierTarget(station, actor, target.Position, target.Facing);
        if (rejection is { } reason) { return Reject(command.CommandId, reason); }
        var definition = station.Definition.Combat.Barrier;
        AssignPrimaryAction(actor, new PrimaryActionRuntime(command.CommandId, PrimaryActionKind.Ability, target.Position, null, [])
        {
            AbilityId = definition.Id, AbilityTargetPosition = target.Position, AbilityFacing = NormalizeFacing(target.Facing),
            Phase = PrimaryActionPhase.Windup, PhaseTicksRemaining = definition.WindupTicks,
            PhaseTicksTotal = definition.WindupTicks, DefensiveAbility = true,
        });
        return Accept(command.CommandId);
    }

    private CommandRejectionCode? ValidateBarrierTarget(StationRouteRuntime station, ActorRuntime actor,
        WorldPosition position, WorldPosition? facing)
    {
        var definition = station.Definition.Combat.Barrier;
        if (actor.Loadout?.ActiveAbilityId != definition.Id) { return CommandRejectionCode.UnknownAbility; }
        if (!position.IsFinite || Math.Abs(position.Y - actor.Position.Y) > .1
            || facing is not { } direction || !IsValidFacing(direction))
        { return CommandRejectionCode.InvalidAbilityTarget; }
        if (actor.Health <= 0) { return CommandRejectionCode.CombatantDefeated; }
        if (actor.Cooldowns.GetValueOrDefault(definition.Id) > 0) { return CommandRejectionCode.AbilityOnCooldown; }
        if (actor.Position.DistanceTo(position) > definition.RangeMeters) { return CommandRejectionCode.AbilityTargetOutOfRange; }
        // The anchor and both ends must fit the navigable floor. Use the same
        // spatial adapter as movement; no Godot physics enters the rules.
        var normal = NormalizeFacing(direction);
        foreach (var offset in new[] { 0, -definition.WidthMeters / 2, definition.WidthMeters / 2 })
        {
            var point = new WorldPosition(position.X - normal.Z * offset, position.Y, position.Z + normal.X * offset);
            if (!TryNormalizePath(_pathfinder!.FindPath(actor.Id, actor.Position, point), actor.Position,
                endpoint => endpoint.DistanceTo(point) <= .1, out _))
            { return CommandRejectionCode.InvalidAbilityTarget; }
        }
        return null;
    }

    private static WorldPosition NormalizeFacing(WorldPosition facing)
    {
        var scale = Math.Max(Math.Abs(facing.X), Math.Abs(facing.Z));
        var x = facing.X / scale; var z = facing.Z / scale;
        var length = Math.Sqrt(x * x + z * z);
        return new WorldPosition(x / length, 0, z / length);
    }

    private void AdvanceBarrierAction(StationRouteRuntime station, ActorRuntime actor, PrimaryActionRuntime action)
    {
        UpdatePhaseRemaining(action);
        if (action.PhaseTicksRemaining > 0) { return; }
        if (action.Phase == PrimaryActionPhase.Windup)
        {
            var rejection = ValidateBarrierTarget(station, actor, action.AbilityTargetPosition, action.AbilityFacing);
            if (rejection is { } reason)
            {
                actor.CurrentAction = null; RecordPrimaryActionFailure(actor, action, reason);
                ResumeRememberedAttack(station, actor); return;
            }
            var definition = station.Definition.Combat.Barrier;
            EndBarrier(station, BarrierEndReason.Replaced);
            var facing = action.AbilityFacing!.Value;
            var position = definition.CenterAt(action.AbilityTargetPosition);
            station.Combat.Barrier = new BarrierRuntime(actor.Id, position, facing, Tick, Tick + definition.DurationTicks);
            actor.Cooldowns[definition.Id] = definition.CooldownTicks;
            Record(GameplayEventType.BarrierDeployed, action.CommandId,
                detail: new BarrierEventDetail(actor.Id, position, facing, definition.Id));
            BeginRecovery(action, definition.RecoveryTicks);
        }
        else { actor.CurrentAction = null; ResumeRememberedAttack(station, actor); }
    }

    private void ValidateBarrierLifetime(StationRouteRuntime station)
    {
        if (station.Combat.Barrier is { } barrier && Tick >= barrier.ExpiresAtTick)
        { EndBarrier(station, BarrierEndReason.Expired); }
    }

    private void EndBarrier(StationRouteRuntime station, BarrierEndReason reason)
    {
        if (station.Combat.Barrier is not { } barrier) { return; }
        station.Combat.Barrier = null;
        Record(GameplayEventType.BarrierEnded, detail: new BarrierEventDetail(barrier.SourceId,
            barrier.Position, barrier.Facing, station.Definition.Combat.Barrier.Id, reason));
    }

    private static bool IntersectBarrier(StationRouteRuntime station, WorldPosition from, WorldPosition to, out WorldPosition hit)
    {
        hit = default;
        if (station.Combat.Barrier is not { } barrier) { return false; }
        var normal = barrier.Facing;
        var center = barrier.Position;
        var startSide = (from.X - center.X) * normal.X + (from.Z - center.Z) * normal.Z;
        var endSide = (to.X - center.X) * normal.X + (to.Z - center.Z) * normal.Z;
        if (startSide < 0 || endSide > 0 || startSide - endSide < .000001) { return false; }
        hit = LerpPosition(from, to, startSide / (startSide - endSide));
        var definition = station.Definition.Combat.Barrier;
        var across = (hit.X - center.X) * -normal.Z + (hit.Z - center.Z) * normal.X;
        var x = Math.Abs(across) / (definition.WidthMeters / 2);
        var y = Math.Abs(hit.Y - center.Y) / (definition.HeightMeters / 2);
        return x <= 1 && y <= 1 && x + y <= 1.7;
    }

    private sealed record BarrierRuntime(EntityId SourceId, WorldPosition Position, WorldPosition Facing,
        long DeployedAtTick, long ExpiresAtTick);
}
