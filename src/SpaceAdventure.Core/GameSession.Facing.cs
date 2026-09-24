namespace SpaceAdventure.Core;

public sealed partial class GameSession
{
    private const double TurnRadiansPerTick = 4 * Math.PI / TicksPerSecond;

    private static bool IsValidFacing(WorldPosition facing) => facing.IsFinite && Math.Abs(facing.Y) <= .001
        && Math.Max(Math.Abs(facing.X), Math.Abs(facing.Z)) >= .01;

    private static WorldPosition? DirectionTo(WorldPosition from, WorldPosition to)
    {
        var delta = new WorldPosition(to.X - from.X, 0, to.Z - from.Z);
        return IsValidFacing(delta) ? NormalizeFacing(delta) : null;
    }

    private static double FacingAngle(WorldPosition facing) => Math.Atan2(facing.X, facing.Z);

    private static double FacingAngleDelta(WorldPosition from, WorldPosition to) =>
        Math.IEEERemainder(FacingAngle(to) - FacingAngle(from), 2 * Math.PI);

    private static WorldPosition InterpolateFacing(WorldPosition from, WorldPosition to, double fraction)
    {
        var angle = FacingAngle(from) + FacingAngleDelta(from, to) * fraction;
        return new WorldPosition(Math.Sin(angle), 0, Math.Cos(angle));
    }

    private static WorldPosition DesiredFacing(StationRouteRuntime station, ActorRuntime actor)
    {
        var action = actor.CurrentAction;
        if (action is not null && action.WaypointIndex < action.Waypoints.Count)
        { return DirectionTo(actor.Position, action.Waypoints[action.WaypointIndex]) ?? actor.Facing; }
        if (action?.CombatTargetId is { } targetId && station.Combat.Hostiles.TryGetValue(targetId, out var target)
            && target.Health > 0 && IsVisibleToCrew(station, target))
        { return DirectionTo(actor.Position, target.Position) ?? actor.Facing; }
        if (action?.AbilityId == station.Definition.Combat.DirectHeal.Id && action.CombatTargetId is { } allyId
            && station.Actors.TryGetValue(allyId, out var ally))
        { return DirectionTo(actor.Position, ally.Position) ?? actor.Facing; }
        if (action?.AbilityId == station.Definition.Combat.ProtagonistAbility.Id
            || action?.AbilityId == station.Definition.Combat.HealingField.Id)
        { return DirectionTo(actor.Position, action.AbilityTargetPosition) ?? actor.Facing; }
        if (station.Combat.Phase is EncounterPhase.Readying or EncounterPhase.Active or EncounterPhase.Securing)
        {
            var nearest = station.Combat.Hostiles.Values.Where(hostile => hostile.Health > 0 && IsVisibleToCrew(station, hostile))
                .OrderBy(hostile => hostile.Position.DistanceTo(actor.Position))
                .ThenBy(hostile => hostile.Id.Value, StringComparer.Ordinal).FirstOrDefault();
            if (nearest is not null) { return DirectionTo(actor.Position, nearest.Position) ?? actor.Facing; }
        }
        return actor.Facing;
    }

    private static void AdvanceActorFacing(StationRouteRuntime station, ActorRuntime actor)
    {
        if (actor.MaximumHealth > 0 && actor.Health <= 0) { return; }
        var target = DesiredFacing(station, actor);
        var angle = Math.Abs(FacingAngleDelta(actor.Facing, target));
        actor.Facing = angle <= TurnRadiansPerTick ? target : InterpolateFacing(actor.Facing, target, TurnRadiansPerTick / angle);
    }
}
