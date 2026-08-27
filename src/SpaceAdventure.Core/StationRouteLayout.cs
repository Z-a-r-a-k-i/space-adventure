using System.Collections.ObjectModel;

namespace SpaceAdventure.Core;

public sealed record StationActorPlacement(
    EntityId ActorId,
    WorldPosition Position);

public sealed record StationInteractionPlacement(
    EntityId InteractionId,
    WorldPosition Position,
    WorldPosition ApproachPosition);

public sealed record StationEncounterPlacement(
    EncounterId EncounterId,
    WorldPosition TriggerCenter,
    double TriggerRadiusMeters,
    WorldPosition ProtagonistRestartPosition,
    WorldPosition HostileSpawnPosition);

public sealed class StationRouteLayout
{
    private readonly ReadOnlyDictionary<EntityId, StationActorPlacement> _actorPlacements;
    private readonly ReadOnlyDictionary<EntityId, StationInteractionPlacement> _interactionPlacements;
    private readonly ReadOnlyCollection<StationActorPlacement> _actorList;
    private readonly ReadOnlyCollection<StationInteractionPlacement> _interactionList;

    public StationRouteLayout(
        WorldPosition protagonistStart,
        IEnumerable<StationActorPlacement> actors,
        IEnumerable<StationInteractionPlacement> interactions,
        StationEncounterPlacement? encounter = null)
    {
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(interactions);
        if (!protagonistStart.IsFinite)
        {
            throw new ArgumentOutOfRangeException(
                nameof(protagonistStart),
                "The protagonist start position must be finite.");
        }

        var actorDictionary = new Dictionary<EntityId, StationActorPlacement>();
        foreach (var placement in actors)
        {
            if (!placement.Position.IsFinite)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(actors),
                    $"Actor '{placement.ActorId}' has a non-finite position.");
            }

            if (!actorDictionary.TryAdd(placement.ActorId, placement))
            {
                throw new ArgumentException(
                    $"Actor placement '{placement.ActorId}' is duplicated.",
                    nameof(actors));
            }
        }

        var interactionDictionary = new Dictionary<EntityId, StationInteractionPlacement>();
        foreach (var placement in interactions)
        {
            if (!placement.Position.IsFinite || !placement.ApproachPosition.IsFinite)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(interactions),
                    $"Interaction '{placement.InteractionId}' has a non-finite position.");
            }

            if (!interactionDictionary.TryAdd(placement.InteractionId, placement))
            {
                throw new ArgumentException(
                    $"Interaction placement '{placement.InteractionId}' is duplicated.",
                    nameof(interactions));
            }
        }

        ProtagonistStart = protagonistStart;
        _actorPlacements = new ReadOnlyDictionary<EntityId, StationActorPlacement>(actorDictionary);
        _interactionPlacements = new ReadOnlyDictionary<EntityId, StationInteractionPlacement>(interactionDictionary);
        _actorList = new ReadOnlyCollection<StationActorPlacement>(actorDictionary.Values.ToArray());
        _interactionList = new ReadOnlyCollection<StationInteractionPlacement>(
            interactionDictionary.Values.ToArray());

        if (encounter is not null
            && (!encounter.TriggerCenter.IsFinite
                || !encounter.ProtagonistRestartPosition.IsFinite
                || !encounter.HostileSpawnPosition.IsFinite
                || !double.IsFinite(encounter.TriggerRadiusMeters)
                || encounter.TriggerRadiusMeters <= 0
                || encounter.TriggerRadiusMeters > 20))
        {
            throw new ArgumentOutOfRangeException(
                nameof(encounter),
                "Encounter placement positions and trigger radius must be finite and bounded.");
        }

        Encounter = encounter;
    }

    public WorldPosition ProtagonistStart { get; }

    public IReadOnlyCollection<StationActorPlacement> Actors => _actorList;

    public IReadOnlyCollection<StationInteractionPlacement> Interactions => _interactionList;

    public StationEncounterPlacement? Encounter { get; }

    public bool TryGetActor(EntityId actorId, out StationActorPlacement placement)
    {
        return _actorPlacements.TryGetValue(actorId, out placement!);
    }

    public bool TryGetInteraction(
        EntityId interactionId,
        out StationInteractionPlacement placement)
    {
        return _interactionPlacements.TryGetValue(interactionId, out placement!);
    }
}
