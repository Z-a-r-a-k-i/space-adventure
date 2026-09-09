using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private Label _onboardingHint = null!;
    private long _onboardingEventSequence;
    private bool _counterLearned;
    private bool _cameraInputBeforeDialogue;
    private bool _dialogueInputActive;
    private (EncounterId Id, int Attempt, EncounterPhase Phase)? _lastFlowPhase;

    private void CreateOnboardingHint()
    {
        _onboardingHint = HudLabel("", 13, "cad9dd");
        _onboardingHint.CustomMinimumSize = new Vector2(312, 0);
        _onboardingHint.MouseFilter = Control.MouseFilterEnum.Ignore;
        _objectiveLabel.GetParent().AddChild(_onboardingHint);
    }

    private void UpdateOnboarding(GameObservation observation, StationRouteObservation route)
    {
        foreach (var item in _session!.EventsSince(_onboardingEventSequence))
        {
            _onboardingEventSequence = item.Sequence;
            if (item.Detail is ActionInterruptedEventDetail interrupted
                && interrupted.SourceId == route.Protagonist.Id) { _counterLearned = true; }
        }
        if (route.Encounter is not { } encounter) { return; }
        var flow = (encounter.Id, encounter.Attempt, encounter.Phase);
        if (_lastFlowPhase != flow)
        {
            _lastFlowPhase = flow;
            if (encounter.Phase is EncounterPhase.Readying or EncounterPhase.Securing
                or EncounterPhase.Victory or EncounterPhase.Defeat)
            {
                _feedbackSeconds = 0;
                _feedbackLabel.Visible = false;
                CancelAbilityTargeting();
                CancelSelectionGesture();
            }
        }
        var solo = encounter.Id == _definition!.Combat.SoloEncounter.Id;
        var incoming = route.Hostiles?.Any(enemy => enemy.CurrentAction?.Phase == PrimaryActionPhase.Windup) == true;
        _onboardingHint.Text = encounter.Phase switch
        {
            EncounterPhase.Readying when solo => "FIRST CONTACT\nRight-click the Enforcer to assign fire. Space resumes the weapon draw.",
            EncounterPhase.Active when solo && !_counterLearned && incoming =>
                "COUNTER THE STRIKE\nSpace pauses. Press 1, click the floor beneath the Enforcer, then resume to Interrupt.",
            EncounterPhase.Active when solo && !_counterLearned =>
                "WATCH THE ENFORCER\nIts strike has a wind-up. Pause when the warning appears, then use Interrupt (1).",
            EncounterPhase.Active when solo && _counterLearned =>
                "STRIKE INTERRUPTED\nKeep firing at your assigned target. Move or Stop (X) to break off.",
            EncounterPhase.Readying =>
                "COORDINATE THE CREW\nDrag to select both; right-click to order. Tab changes ability focus. Place Protector's Barrier (1) toward the sentry.",
            EncounterPhase.Active when !solo && observation.Paused =>
                "PLAN TOGETHER\nEach crew member keeps one next order. Protector's Barrier blocks shots; Vanguard's Interrupt cancels a strike.",
            _ => "",
        };
        _onboardingHint.Visible = _onboardingHint.Text.Length > 0 && route.ActiveDialogue is null
            && !_controlsOverlay.Visible && !_abilityTargeting && !_outcomePanel.Visible;
    }

    private void UpdateDialogueInput(bool active)
    {
        if (active == _dialogueInputActive) { return; }
        _dialogueInputActive = active;
        if (active)
        {
            CancelSelectionGesture();
            CancelAbilityTargeting();
            _cameraInputBeforeDialogue = _camera.InputEnabled;
            _camera.InputEnabled = false;
        }
        else { _camera.InputEnabled = _cameraInputBeforeDialogue; }
    }

    private bool HandleDialogueInput(InputEvent @event)
    {
        if (_session?.Observe().StationRoute?.ActiveDialogue is null || @event is not InputEventKey key)
            return false;
        if (key.Pressed && !key.Echo)
        {
            var responses = _dialogueResponses.GetChildren().OfType<Button>().ToArray();
            var focused = Array.FindIndex(responses, response => response.HasFocus());
            if (IsKey(key, Key.Key1) || IsKey(key, Key.Key2))
                ChooseVisibleDialogueResponse(IsKey(key, Key.Key2) ? 1 : 0);
            else if (IsKey(key, Key.Enter) || IsKey(key, Key.KpEnter))
                ChooseVisibleDialogueResponse(Math.Max(0, focused));
            else if (responses.Length > 0 && (IsKey(key, Key.Tab) || IsKey(key, Key.Up) || IsKey(key, Key.Down)))
            {
                var backward = IsKey(key, Key.Up) || IsKey(key, Key.Tab) && key.ShiftPressed;
                responses[(Math.Max(0, focused) + (backward ? responses.Length - 1 : 1)) % responses.Length].GrabFocus();
            }
        }
        GetViewport().SetInputAsHandled();
        return true;
    }
}
