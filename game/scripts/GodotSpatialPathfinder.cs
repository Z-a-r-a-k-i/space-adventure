using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public sealed class GodotSpatialPathfinder(Rid navigationMap) : ISpatialPathfinder
{
    private const float MaximumStartSnapDistance = 0.8f;
    private const float MaximumDestinationSnapDistance = 0.9f;
    private const float MaximumEndpointError = 0.35f;
    private const int MaximumWaypointCount = 128;

    private readonly Rid _navigationMap = navigationMap;

    public SpatialPathResult FindPath(
        EntityId actorId,
        WorldPosition origin,
        WorldPosition destination)
    {
        _ = actorId;

        if (!CanConvertToGodot(origin) || !CanConvertToGodot(destination)
            || NavigationServer3D.MapGetIterationId(_navigationMap) == 0)
        {
            return SpatialPathResult.Unreachable;
        }

        var requestedOrigin = ToGodot(origin);
        var requestedDestination = ToGodot(destination);
        var snappedOrigin = NavigationServer3D.MapGetClosestPoint(_navigationMap, requestedOrigin);
        var snappedDestination = NavigationServer3D.MapGetClosestPoint(_navigationMap, requestedDestination);

        if (requestedOrigin.DistanceTo(snappedOrigin) > MaximumStartSnapDistance
            || requestedDestination.DistanceTo(snappedDestination) > MaximumDestinationSnapDistance)
        {
            return SpatialPathResult.Unreachable;
        }

        if (snappedOrigin.DistanceTo(snappedDestination) <= 0.01f)
        {
            return SpatialPathResult.Reachable([FromGodot(snappedDestination)]);
        }

        var path = NavigationServer3D.MapGetPath(
            _navigationMap,
            snappedOrigin,
            snappedDestination,
            optimize: true,
            navigationLayers: 1);

        if (path.Length == 0
            || path.Length > MaximumWaypointCount
            || path[^1].DistanceTo(snappedDestination) > MaximumEndpointError)
        {
            return SpatialPathResult.Unreachable;
        }

        var waypoints = new List<WorldPosition>(path.Length);
        foreach (var point in path)
        {
            var waypoint = FromGodot(point);
            if (!waypoint.IsFinite)
            {
                return SpatialPathResult.Unreachable;
            }

            if (waypoints.Count == 0 || waypoints[^1].DistanceTo(waypoint) > 0.001)
            {
                waypoints.Add(waypoint);
            }
        }

        return waypoints.Count == 0
            ? SpatialPathResult.Unreachable
            : SpatialPathResult.Reachable(waypoints);
    }

    private static Vector3 ToGodot(WorldPosition position)
    {
        return new Vector3((float)position.X, (float)position.Y, (float)position.Z);
    }

    private static bool CanConvertToGodot(WorldPosition position)
    {
        return position.IsFinite
            && Math.Abs(position.X) <= float.MaxValue
            && Math.Abs(position.Y) <= float.MaxValue
            && Math.Abs(position.Z) <= float.MaxValue;
    }

    private static WorldPosition FromGodot(Vector3 position)
    {
        return new WorldPosition(position.X, position.Y, position.Z);
    }
}
