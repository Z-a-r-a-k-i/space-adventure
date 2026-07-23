using System.Collections.ObjectModel;

namespace SpaceAdventure.Core;

public readonly record struct WorldPosition(double X, double Y, double Z)
{
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);

    public double DistanceTo(WorldPosition other)
    {
        var deltaX = other.X - X;
        var deltaY = other.Y - Y;
        var deltaZ = other.Z - Z;
        return Math.Sqrt((deltaX * deltaX) + (deltaY * deltaY) + (deltaZ * deltaZ));
    }
}

public interface ISpatialPathfinder
{
    SpatialPathResult FindPath(
        EntityId actorId,
        WorldPosition origin,
        WorldPosition destination);
}

public sealed class SpatialPathResult
{
    private static readonly SpatialPathResult UnreachableResult = new(false, []);

    private SpatialPathResult(bool isReachable, IEnumerable<WorldPosition> waypoints)
    {
        IsReachable = isReachable;
        Waypoints = new ReadOnlyCollection<WorldPosition>(waypoints.ToArray());
    }

    public bool IsReachable { get; }

    public IReadOnlyList<WorldPosition> Waypoints { get; }

    public static SpatialPathResult Unreachable => UnreachableResult;

    public static SpatialPathResult Reachable(IEnumerable<WorldPosition> waypoints)
    {
        ArgumentNullException.ThrowIfNull(waypoints);
        return new SpatialPathResult(true, waypoints);
    }
}
