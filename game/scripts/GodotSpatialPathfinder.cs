using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public sealed class GodotSpatialPathfinder(Rid navigationMap) : ISpatialPathfinder
{
    private const float MaximumStartSnapDistance = 0.8f;
    private const float MaximumDestinationSnapDistance = 0.9f;
    private const float MaximumEndpointError = 0.35f;
    // Half-width and vertical allowance of the walkable corridor along an enabled door link.
    private const float LinkCorridorHalfWidth = 0.6f;
    private const float LinkCorridorHeightTolerance = 0.5f;
    private const float OffMeshTolerance = 0.05f;
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
        Vector3? destinationOnLink = null;

        // Anyone stopped inside a doorway stands on a door link, between two floors: a fight can start
        // there, or the player can press Stop mid-passage. Such points join the path through the link's
        // nearer endpoint instead of being unreachable; pits and walls are still rejected.
        // The path must end exactly on such a point, or the rules treat a crew member in a doorway as unreachable.
        if (requestedOrigin.DistanceTo(snappedOrigin) > OffMeshTolerance && NearestEnabledLinkEndpoint(requestedOrigin) is { } linkOrigin)
        { snappedOrigin = linkOrigin; }
        else if (requestedOrigin.DistanceTo(snappedOrigin) > MaximumStartSnapDistance) { return SpatialPathResult.Unreachable; }
        if (requestedDestination.DistanceTo(snappedDestination) > OffMeshTolerance
            && NearestEnabledLinkEndpoint(requestedDestination) is { } linkDestination)
        {
            snappedDestination = linkDestination;
            destinationOnLink = requestedDestination;
        }
        else if (requestedDestination.DistanceTo(snappedDestination) > MaximumDestinationSnapDistance) { return SpatialPathResult.Unreachable; }

        if (snappedOrigin.DistanceTo(snappedDestination) <= 0.01f)
        {
            return SpatialPathResult.Reachable(destinationOnLink is { } near
                ? [FromGodot(snappedDestination), FromGodot(near)] : [FromGodot(snappedDestination)]);
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

        if (destinationOnLink is { } onLink) { waypoints.Add(FromGodot(onLink)); }
        return waypoints.Count == 0
            ? SpatialPathResult.Unreachable
            : SpatialPathResult.Reachable(waypoints);
    }

    private Vector3? NearestEnabledLinkEndpoint(Vector3 point)
    {
        foreach (var link in NavigationServer3D.MapGetLinks(_navigationMap))
        {
            if (!NavigationServer3D.LinkGetEnabled(link)) { continue; }
            var start = NavigationServer3D.LinkGetStartPosition(link);
            var end = NavigationServer3D.LinkGetEndPosition(link);
            var along = Geometry3D.GetClosestPointToSegment(point, start, end);
            if (new Vector2(along.X - point.X, along.Z - point.Z).Length() > LinkCorridorHalfWidth
                || Math.Abs(along.Y - point.Y) > LinkCorridorHeightTolerance) { continue; }
            return point.DistanceTo(start) <= point.DistanceTo(end) ? start : end;
        }
        return null;
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
