using System.Collections.ObjectModel;

namespace SpaceAdventure.Core;

public sealed record StationInteractionPlacement(
    EntityId InteractionId,
    WorldPosition Position,
    WorldPosition ApproachPosition);

public sealed class StationRouteLayout
{
    private readonly ReadOnlyDictionary<EntityId, StationInteractionPlacement> _placements;
    private readonly ReadOnlyCollection<StationInteractionPlacement> _interactionList;

    public StationRouteLayout(
        WorldPosition protagonistStart,
        IEnumerable<StationInteractionPlacement> interactions)
    {
        ArgumentNullException.ThrowIfNull(interactions);
        if (!protagonistStart.IsFinite)
        {
            throw new ArgumentOutOfRangeException(
                nameof(protagonistStart),
                "The protagonist start position must be finite.");
        }

        var placementDictionary = new Dictionary<EntityId, StationInteractionPlacement>();
        foreach (var placement in interactions)
        {
            if (!placement.Position.IsFinite || !placement.ApproachPosition.IsFinite)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(interactions),
                    $"Interaction '{placement.InteractionId}' has a non-finite position.");
            }

            if (!placementDictionary.TryAdd(placement.InteractionId, placement))
            {
                throw new ArgumentException(
                    $"Interaction placement '{placement.InteractionId}' is duplicated.",
                    nameof(interactions));
            }
        }

        ProtagonistStart = protagonistStart;
        _placements = new ReadOnlyDictionary<EntityId, StationInteractionPlacement>(placementDictionary);
        _interactionList = new ReadOnlyCollection<StationInteractionPlacement>(
            placementDictionary.Values.ToArray());
    }

    public WorldPosition ProtagonistStart { get; }

    public IReadOnlyCollection<StationInteractionPlacement> Interactions => _interactionList;

    public bool TryGetInteraction(
        EntityId interactionId,
        out StationInteractionPlacement placement)
    {
        return _placements.TryGetValue(interactionId, out placement!);
    }
}
