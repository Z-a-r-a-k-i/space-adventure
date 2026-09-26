namespace SpaceAdventure.Core;

/// <summary>Crew sight range and eye height, and the shorter range at which a hostile notices a crew member it can see.</summary>
public sealed record StationVisionDefinition(double RangeMeters, double EyeHeightMeters, double HostileDetectionMeters);

public sealed class StationVisionBlocker
{
    public StationVisionBlocker(string id, WorldPosition minimum, WorldPosition maximum, EntityId? doorInteractionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!minimum.IsFinite || !maximum.IsFinite || minimum.X >= maximum.X
            || minimum.Y >= maximum.Y || minimum.Z >= maximum.Z)
        { throw new ArgumentException("Vision blocker bounds must be finite with positive volume.", nameof(maximum)); }
        if (doorInteractionId is { } door && string.IsNullOrWhiteSpace(door.Value))
        { throw new ArgumentException("Vision door interaction ID must be valid.", nameof(doorInteractionId)); }
        Id = id; Minimum = minimum; Maximum = maximum; DoorInteractionId = doorInteractionId;
    }

    public string Id { get; }
    public WorldPosition Minimum { get; }
    public WorldPosition Maximum { get; }
    public EntityId? DoorInteractionId { get; }
}
