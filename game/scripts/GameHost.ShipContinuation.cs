using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private const string ShipBattleScenePath = "res://scenes/ship_battle.tscn";
    private ShipContinuation? _shipContinuation;
    private bool _shipLoadRequested;

    /// <summary>Review opt-in (--review-continue=ship) that runs the ordinary handoff after the escape route.</summary>
    private bool ShipHandoffReview => ReviewArgument("review-continue", "") is "ship" or "ship-capture";

    // The ordinary game continues into the battle; station review/smoke profiles keep station-only completion
    // unless they explicitly opt into the handoff.
    private bool ShipContinuationEnabled => ShipHandoffReview
        || _developmentArguments.All(argument => argument.StartsWith("--auto-quit-seconds=", StringComparison.Ordinal));

    public ShipContinuationState? ShipContinuationState => _shipContinuation?.State;

    private void AdvanceShipContinuation(GameObservation observation)
    {
        if (!ShipContinuationEnabled || observation.StationRoute is not { Phase: ScenarioPhase.Completed } route) { return; }
        if (_shipContinuation is null)
        {
            _shipContinuation = new ShipContinuation();
            _shipContinuation.CaptureStationResult(route.Party.Select(actor => new ShipCrewSeed(actor.Id.Value,
                actor.Id == _definition!.Medic.Id ? _definition.Medic.DisplayName
                : actor.Id == _definition.Companion.Id ? _definition.Companion.DisplayName : "Vanguard",
                actor.Id == _definition.Medic.Id)).ToArray());
        }
        switch (_shipContinuation.State)
        {
            case Core.ShipContinuationState.Presenting when !_shipLoadRequested:
                _shipLoadRequested = ResourceLoader.LoadThreadedRequest(ShipBattleScenePath) == Error.Ok;
                if (!_shipLoadRequested) { _shipContinuation.MarkLoadFailed("The ship battle could not be requested."); }
                break;
            case Core.ShipContinuationState.Presenting:
                var status = ResourceLoader.LoadThreadedGetStatus(ShipBattleScenePath);
                if (status == ResourceLoader.ThreadLoadStatus.Loaded) { _shipContinuation.MarkResourcesReady(); }
                else if (status is ResourceLoader.ThreadLoadStatus.Failed or ResourceLoader.ThreadLoadStatus.InvalidResource)
                { _shipContinuation.MarkLoadFailed($"Ship battle load failed ({status})."); }
                break;
            case Core.ShipContinuationState.Failed:
                _objectiveLabel.Text = $"{_shipContinuation.Error} Press R to retry.";
                if (Input.IsKeyPressed(Key.R) && _shipContinuation.RetryLoad())
                {
                    // A failed request still owns a loader token; consume it before requesting this path again.
                    if (_shipLoadRequested && ResourceLoader.LoadThreadedGetStatus(ShipBattleScenePath) == ResourceLoader.ThreadLoadStatus.Failed)
                    { _ = ResourceLoader.LoadThreadedGet(ShipBattleScenePath); }
                    _shipLoadRequested = false;
                }
                return;
        }
        if (_departureSeconds >= DepartureDuration) { _shipContinuation.MarkDepartureFinished(); }
        if (!_shipContinuation.TryEnter()) { return; }
        ShipBattleHost? host = null;
        var tree = GetTree();
        try
        {
            host = ((PackedScene)ResourceLoader.LoadThreadedGet(ShipBattleScenePath)).Instantiate<ShipBattleHost>();
            host.CrewSeeds = _shipContinuation.Crew;
            host.EnteredFromStation = true;
            if (ShipHandoffReview)
            {
                host.HandoffReviewMode = ReviewArgument("review-continue", "") == "ship-capture" ? "capture" : "smoke";
                host.ExpectedCrewIds = _shipContinuation.Crew.Select(crew => crew.Id).ToArray();
            }
            tree.Root.AddChild(host);
            if (host.InitializationError is { } error) { throw new InvalidOperationException(error); }
        }
        catch (Exception exception) when (exception is InvalidOperationException or InvalidCastException)
        {
            // Withdraw the consumed entry: discard any partial battle and keep the station retryable.
            if (host is not null) { if (host.IsInsideTree()) { tree.Root.RemoveChild(host); } host.QueueFree(); }
            _shipLoadRequested = false;
            _shipContinuation.MarkEntryFailed(exception.Message);
            GD.Print($"[ship-handoff] entry failed: {exception.Message}");
            return;
        }
        _departureAudio?.Stop();
        tree.CurrentScene = host;
        QueueFree();
    }

    /// <summary>Ordinary departure followed by the real threaded handoff, driven frame by frame for review.</summary>
    private async Task RunShipHandoffAfterBoardingAsync()
    {
        var resourcesReadyFrame = -1;
        for (var frame = 0; frame < 1200; frame++)
        {
            var observation = _session!.Observe();
            AdvanceDeparture(observation, 1.0 / 60);
            AdvanceShipContinuation(observation);
            if (resourcesReadyFrame < 0 && ResourceLoader.LoadThreadedGetStatus(ShipBattleScenePath) == ResourceLoader.ThreadLoadStatus.Loaded)
            { resourcesReadyFrame = frame; }
            if (_shipContinuation?.State == Core.ShipContinuationState.Entered)
            {
                GD.Print($"[ship-handoff] entered frame={frame} departure_seconds={_departureSeconds:0.00} resources_ready_frame={resourcesReadyFrame}");
                InputCheck("Handoff waits for the full departure and title card", _departureSeconds >= DepartureDuration);
                InputCheck("Station completed exactly once before handoff",
                    _session.EventsSince(0).Count(item => item.Type == GameplayEventType.ScenarioCompleted) == 1);
                return;
            }
            if (_shipContinuation?.State == Core.ShipContinuationState.Failed)
            {
                InputCheck($"Handoff entered the battle ({_shipContinuation.Error})", false);
                await FinishSoloReview();
                return;
            }
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        InputCheck("Handoff entered the battle within 20 s of boarding", false);
        await FinishSoloReview();
    }
}
