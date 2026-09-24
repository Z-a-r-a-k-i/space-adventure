namespace SpaceAdventure.Core;

public enum ShipContinuationState
{
    AwaitingStation,
    Presenting,
    Entered,
    Failed,
}

/// <summary>
/// Station-to-battle handoff. Captures the station result once, then enters the battle exactly once
/// when the departure presentation has finished AND battle resources are ready. Loading never advances
/// battle simulation; a load failure stays retryable.
/// </summary>
public sealed class ShipContinuation
{
    private bool _departureFinished;
    private bool _resourcesReady;

    public ShipContinuationState State { get; private set; } = ShipContinuationState.AwaitingStation;
    public IReadOnlyList<ShipCrewSeed> Crew { get; private set; } = [];
    public string? Error { get; private set; }
    public int LoadAttempts { get; private set; }

    public bool CaptureStationResult(IReadOnlyList<ShipCrewSeed> crew)
    {
        ArgumentNullException.ThrowIfNull(crew);
        if (State != ShipContinuationState.AwaitingStation) { return false; }
        Crew = crew.ToArray();
        State = ShipContinuationState.Presenting;
        LoadAttempts = 1;
        return true;
    }

    public void MarkDepartureFinished() { if (State != ShipContinuationState.AwaitingStation) { _departureFinished = true; } }

    public void MarkResourcesReady() { if (State == ShipContinuationState.Presenting) { _resourcesReady = true; } }

    public void MarkLoadFailed(string error)
    {
        if (State != ShipContinuationState.Presenting) { return; }
        Error = error;
        _resourcesReady = false;
        State = ShipContinuationState.Failed;
    }

    /// <summary>
    /// The host could not instantiate or start the battle after <see cref="TryEnter"/>. The entry is
    /// withdrawn (the host must discard any partial scene) and becomes a retryable failure; the
    /// departure stays finished and resources must be requested again.
    /// </summary>
    public void MarkEntryFailed(string error)
    {
        if (State != ShipContinuationState.Entered) { return; }
        Error = error;
        _resourcesReady = false;
        State = ShipContinuationState.Failed;
    }

    public bool RetryLoad()
    {
        if (State != ShipContinuationState.Failed) { return false; }
        Error = null;
        LoadAttempts++;
        State = ShipContinuationState.Presenting;
        return true;
    }

    /// <summary>Returns true exactly once: the frame on which the host must enter the battle.</summary>
    public bool TryEnter()
    {
        if (State != ShipContinuationState.Presenting || !_departureFinished || !_resourcesReady) { return false; }
        State = ShipContinuationState.Entered;
        return true;
    }
}
