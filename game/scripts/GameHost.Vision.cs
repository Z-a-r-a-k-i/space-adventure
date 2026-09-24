using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private List<StationVisionBlocker> CreateVisionBlockers()
    {
        var blockers = _camera.FullWallBounds.Select(wall =>
            VisionBlocker(wall.Id, wall.Bounds)).ToList();
        if (blockers.Count == 0)
            throw new InvalidDataException("Shared crew vision requires the original station wall bounds.");
        // The core treats each blocker as a world-aligned box. An off-axis wall or
        // door would block sight across its whole enclosing box.
        var offAxisWalls = _camera.OffAxisWallIds.ToArray();
        if (offAxisWalls.Length > 0)
            throw new InvalidDataException($"Vision walls must be axis-aligned: {string.Join(", ", offAxisWalls)}.");

        foreach (var (id, view) in _interactionViews)
        {
            var shape = view.GetNodeOrNull<CollisionShape3D>("DoorBlocker/CollisionShape3D");
            if (id == "interaction.evacuation_airlock")
                shape = view.GetNode<CollisionShape3D>("CollisionShape3D");
            if (shape is null) { continue; }
            if (shape.Shape is not BoxShape3D box)
                throw new InvalidDataException($"Vision door '{id}' must have a box blocker.");
            if (!TacticalCameraController.IsAxisAligned(shape.GlobalTransform.Basis))
                throw new InvalidDataException($"Vision door '{id}' must be axis-aligned.");
            var bounds = shape.GlobalTransform * new Aabb(-box.Size / 2, box.Size);
            blockers.Add(VisionBlocker($"vision.door.{id}", bounds, new EntityId(id)));
        }
        return blockers;
    }

    private static StationVisionBlocker VisionBlocker(string id, Aabb bounds, EntityId? doorId = null)
    {
        bounds = bounds.Abs();
        return new StationVisionBlocker(id, ToCore(bounds.Position), ToCore(bounds.End), doorId);
    }
}
