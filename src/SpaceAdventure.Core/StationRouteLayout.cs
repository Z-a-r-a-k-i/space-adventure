using System.Collections.ObjectModel;

namespace SpaceAdventure.Core;

public sealed record StationActorPlacement(
    EntityId ActorId,
    WorldPosition Position);

public sealed record StationInteractionPlacement(
    EntityId InteractionId,
    WorldPosition Position,
    WorldPosition ApproachPosition);

public sealed record StationHostilePlacement(EntityId ActorId, WorldPosition Position, WorldPosition Forward);

public sealed record StationEncounterPlacement(
    EncounterId EncounterId,
    WorldPosition TriggerCenter,
    double TriggerRadiusMeters,
    WorldPosition ProtagonistRestartPosition,
    WorldPosition HostileSpawnPosition,
    WorldPosition? CompanionRestartPosition = null,
    IReadOnlyList<StationActorPlacement>? AdditionalHostiles = null,
    WorldPosition? SentryForward = null,
    IReadOnlyList<StationActorPlacement>? CrewRestartPositions = null,
    IReadOnlyList<StationHostilePlacement>? HostilePlacements = null);

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
        StationEncounterPlacement? encounter = null,
        StationEncounterPlacement? partyEncounter = null,
        IEnumerable<StationEncounterPlacement>? additionalEncounters = null,
        IEnumerable<StationVisionBlocker>? visionBlockers = null)
    {
        ArgumentNullException.ThrowIfNull(actors);
        ArgumentNullException.ThrowIfNull(interactions);
        var blockers = (visionBlockers ?? []).ToArray();
        if (blockers.Any(blocker => blocker is null)
            || blockers.Select(blocker => blocker.Id).Distinct(StringComparer.Ordinal).Count() != blockers.Length)
        { throw new ArgumentException("Vision blockers must have unique IDs and cannot be null.", nameof(visionBlockers)); }
        VisionBlockers = Array.AsReadOnly(blockers);
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
        if (partyEncounter is not null && partyEncounter.HostilePlacements is null &&
            (!partyEncounter.TriggerCenter.IsFinite || !partyEncounter.ProtagonistRestartPosition.IsFinite
             || !partyEncounter.HostileSpawnPosition.IsFinite
             || partyEncounter.CompanionRestartPosition is not { IsFinite: true }
             || !double.IsFinite(partyEncounter.TriggerRadiusMeters)
             || partyEncounter.TriggerRadiusMeters <= 0 || partyEncounter.TriggerRadiusMeters > 20
             || partyEncounter.AdditionalHostiles is null
             || partyEncounter.AdditionalHostiles.Any(actor => !actor.Position.IsFinite)
             || partyEncounter.AdditionalHostiles.Select(actor => actor.ActorId).Distinct().Count() != partyEncounter.AdditionalHostiles.Count
             || partyEncounter.SentryForward is not { IsFinite: true } forward
             || forward.Y != 0
             || Math.Abs(forward.X * forward.X + forward.Z * forward.Z - 1) > 0.001))
        {
            throw new ArgumentOutOfRangeException(nameof(partyEncounter), "Party encounter placements and facing must be finite and valid.");
        }
        PartyEncounter = partyEncounter;
        var placements = new[] { encounter, partyEncounter }.OfType<StationEncounterPlacement>()
            .Concat(additionalEncounters ?? []).ToArray();
        if (placements.Select(item => item.EncounterId).Distinct().Count() != placements.Length)
        { throw new ArgumentException("Encounter placement IDs must be unique.", nameof(additionalEncounters)); }
        foreach (var item in placements)
        {
            if (!item.TriggerCenter.IsFinite || !double.IsFinite(item.TriggerRadiusMeters)
                || item.TriggerRadiusMeters is <= 0 or > 20
                || item.CrewRestartPositions is { } crew && (crew.Any(actor => !actor.Position.IsFinite)
                    || crew.Select(actor => actor.ActorId).Distinct().Count() != crew.Count)
                || item.HostilePlacements is { } hostiles && (hostiles.Any(hostile => !hostile.Position.IsFinite
                    || !hostile.Forward.IsFinite || Math.Abs(hostile.Forward.Y) > .001
                    || Math.Abs(hostile.Forward.X * hostile.Forward.X + hostile.Forward.Z * hostile.Forward.Z - 1) > .001)
                    || hostiles.Select(hostile => hostile.ActorId).Distinct().Count() != hostiles.Count))
            { throw new ArgumentException("Encounter positions and facing must be finite, unique and valid.", nameof(additionalEncounters)); }
        }
        Encounters = Array.AsReadOnly(placements);
    }

    public WorldPosition ProtagonistStart { get; }

    public IReadOnlyCollection<StationActorPlacement> Actors => _actorList;

    public IReadOnlyCollection<StationInteractionPlacement> Interactions => _interactionList;

    public StationEncounterPlacement? Encounter { get; }

    public StationEncounterPlacement? PartyEncounter { get; }

    public IReadOnlyList<StationEncounterPlacement> Encounters { get; }

    public IReadOnlyList<StationVisionBlocker> VisionBlockers { get; }

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
