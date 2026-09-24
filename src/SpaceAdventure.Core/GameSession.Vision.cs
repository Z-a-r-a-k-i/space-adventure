namespace SpaceAdventure.Core;

public sealed partial class GameSession
{
    private static bool IsVisibleToCrew(StationRouteRuntime station, HostileRuntime hostile) =>
        station.Actors.Values.Any(actor => actor.Health > 0
            && actor.Position.DistanceTo(hostile.Position) <= station.Definition.Vision.RangeMeters
            && HasClearSight(station, actor.Position, hostile.Position));

    private static bool HasClearSight(StationRouteRuntime station, WorldPosition origin, WorldPosition target)
    {
        var height = station.Definition.Vision.EyeHeightMeters;
        var from = new WorldPosition(origin.X, origin.Y + height, origin.Z);
        var to = new WorldPosition(target.X, target.Y + height, target.Z);
        foreach (var blocker in station.VisionBlockers)
        {
            if (blocker.DoorInteractionId is { } door && station.Interactions[door].Completed) { continue; }
            if (SightIntersectsBounds(from, to, blocker.Minimum, blocker.Maximum)) { return false; }
        }
        return true;
    }

    // Sight from or to a point inside a blocker is always blocked. A hostile placed
    // there could never be targeted, leaving its encounter unwinnable.
    private static void ValidateVisionPlacements(StationRouteDefinition definition, StationRouteLayout layout)
    {
        var eyeHeight = definition.Vision.EyeHeightMeters;
        var positions = layout.Actors.Select(actor => (Name: actor.ActorId.Value, actor.Position))
            .Append(("protagonist start", layout.ProtagonistStart))
            .Concat(layout.Encounters.SelectMany(EncounterPositions));
        foreach (var (name, position) in positions)
        {
            var eye = new WorldPosition(position.X, position.Y + eyeHeight, position.Z);
            if (layout.VisionBlockers.FirstOrDefault(blocker => Contains(blocker, eye)) is { } blocker)
            { throw new InvalidDataException($"'{name}' is placed inside vision blocker '{blocker.Id}'."); }
        }

        static bool Contains(StationVisionBlocker blocker, WorldPosition point) =>
            point.X >= blocker.Minimum.X && point.X <= blocker.Maximum.X
            && point.Y >= blocker.Minimum.Y && point.Y <= blocker.Maximum.Y
            && point.Z >= blocker.Minimum.Z && point.Z <= blocker.Maximum.Z;
    }

    private static IEnumerable<(string Name, WorldPosition Position)> EncounterPositions(StationEncounterPlacement placement)
    {
        var encounter = placement.EncounterId.Value;
        yield return ($"{encounter} protagonist restart", placement.ProtagonistRestartPosition);
        yield return ($"{encounter} hostile spawn", placement.HostileSpawnPosition);
        if (placement.CompanionRestartPosition is { } companion) { yield return ($"{encounter} companion restart", companion); }
        foreach (var actor in placement.AdditionalHostiles ?? []) { yield return (actor.ActorId.Value, actor.Position); }
        foreach (var actor in placement.CrewRestartPositions ?? []) { yield return ($"{encounter} {actor.ActorId.Value} restart", actor.Position); }
        foreach (var hostile in placement.HostilePlacements ?? []) { yield return (hostile.ActorId.Value, hostile.Position); }
    }

    private static bool SightIntersectsBounds(WorldPosition from, WorldPosition to, WorldPosition minimum, WorldPosition maximum)
    {
        var entry = 0.0;
        var exit = 1.0;
        // Inclusive boundaries make tangent rays and a viewer inside a wall blocked.
        return Axis(from.X, to.X - from.X, minimum.X, maximum.X, ref entry, ref exit)
            && Axis(from.Y, to.Y - from.Y, minimum.Y, maximum.Y, ref entry, ref exit)
            && Axis(from.Z, to.Z - from.Z, minimum.Z, maximum.Z, ref entry, ref exit);

        static bool Axis(double start, double delta, double low, double high, ref double entry, ref double exit)
        {
            if (Math.Abs(delta) <= 1e-10) { return start >= low && start <= high; }
            var first = (low - start) / delta;
            var second = (high - start) / delta;
            entry = Math.Max(entry, Math.Min(first, second));
            exit = Math.Min(exit, Math.Max(first, second));
            return entry <= exit;
        }
    }

    private void ClearHiddenOffensiveTargets(StationRouteRuntime station)
    {
        foreach (var actor in station.Actors.Values)
        {
            bool Hidden(EntityId? id) => id is { } target && station.Combat.Hostiles.TryGetValue(target, out var hostile)
                && !IsVisibleToCrew(station, hostile);
            if (Hidden(actor.RememberedAttackTargetId)) { ClearAttackIntent(actor); }
            if (actor.PendingAction is { } pending && IsOffensive(pending) && Hidden(pending.CombatTargetId))
            {
                actor.PendingAction = null;
                RecordPrimaryActionFailure(actor, pending, CommandRejectionCode.CombatTargetNotVisible);
            }
            if (actor.CurrentAction is { } action && IsOffensive(action) && Hidden(action.CombatTargetId))
            {
                // Released shots retain their independent recovery deadline and cooldown.
                actor.CurrentAction = null;
                RecordPrimaryActionFailure(actor, action, CommandRejectionCode.CombatTargetNotVisible);
            }
        }
    }
}
