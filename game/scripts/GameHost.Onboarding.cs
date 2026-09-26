using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private const double TipSeconds = 9;
    private PanelContainer _tipCard = null!;
    private Label _tipTitle = null!;
    private Label _tipBody = null!;
    private string _tipKey = "";
    private double _tipAgeSeconds;
    private ulong _tipClockMs;
    private ulong _tipShownMs;
    private readonly HashSet<string> _retiredTips = new(StringComparer.Ordinal);
    private long _onboardingEventSequence;
    private bool _counterLearned;
    private bool _cameraInputBeforeDialogue;
    private bool _dialogueInputActive;
    private (EncounterId Id, int Attempt, EncounterPhase Phase)? _lastFlowPhase;

    /// <summary>
    /// Short contextual tips shown once each under the objective tracker. A tip ages only while time runs
    /// (players read while paused), is replaced when the situation changes, and can be dismissed.
    /// </summary>
    private void CreateOnboardingHint()
    {
        _tipCard = new PanelContainer { Name = "TipCard", Visible = false };
        _tipCard.AddThemeStyleboxOverride("panel", TacticalUi.TrackerBox(TacticalUi.Amber));
        _objectiveColumn.AddChild(_tipCard);
        var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 3);
        _tipCard.AddChild(column);
        var header = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        header.AddThemeConstantOverride("separation", 8);
        column.AddChild(header);
        header.AddChild(TacticalUi.Label("TIP", 10, "e5bc7d"));
        _tipTitle = TacticalUi.Label("", 11, "f0d9b0");
        _tipTitle.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        header.AddChild(_tipTitle);
        var close = new Button { Text = "✕", Flat = true, FocusMode = Control.FocusModeEnum.None, TooltipText = "Dismiss tip" };
        close.AddThemeFontSizeOverride("font_size", 11);
        close.AddThemeColorOverride("font_color", TacticalUi.Muted);
        close.AddThemeColorOverride("font_hover_color", Colors.White);
        close.Pressed += () => { _retiredTips.Add(_tipKey); _tipCard.Visible = false; };
        header.AddChild(close);
        _tipBody = HudLabel("", 12, "c3d0d4");
        _tipBody.CustomMinimumSize = new Vector2(296, 0);
        column.AddChild(_tipBody);
    }

    private void ShowTip(string tip, bool paused, bool blocked)
    {
        var now = Time.GetTicksMsec();
        var elapsed = _tipClockMs == 0 ? 0 : (now - _tipClockMs) / 1000.0;
        _tipClockMs = now;
        // Dialogue, the manual or aiming only hide the card; they never use up the tip.
        if (blocked) { _tipCard.Visible = false; return; }
        if (tip != _tipKey)
        {
            if (_tipKey.Length > 0) { _retiredTips.Add(_tipKey); }
            _tipKey = tip;
            _tipAgeSeconds = 0;
            _tipShownMs = now;
            var lines = tip.Split('\n', 2);
            _tipTitle.Text = lines[0];
            _tipBody.Text = lines.Length > 1 ? lines[1] : "";
        }
        if (tip.Length == 0 || _retiredTips.Contains(tip)) { _tipCard.Visible = false; return; }
        if (!paused) { _tipAgeSeconds += Math.Min(elapsed, .25); }
        if (_tipAgeSeconds > TipSeconds) { _retiredTips.Add(tip); _tipCard.Visible = false; return; }
        _tipCard.Visible = true;
        var fadeIn = _reviewMode == "capture" ? 1 : Math.Clamp((now - _tipShownMs) / 250.0, 0, 1);
        var fadeOut = Math.Clamp((TipSeconds - _tipAgeSeconds) / .6, 0, 1);
        _tipCard.Modulate = new Color(1, 1, 1, (float)Math.Min(fadeIn, fadeOut));
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
        var incoming = route.VisibleHostiles.Any(enemy => enemy.EncounterId == route.Encounter?.Id
            && enemy.CurrentAction?.Phase == PrimaryActionPhase.Windup);
        var tip = encounter.Phase switch
        {
            EncounterPhase.Readying when solo => "FIRST CONTACT\nRight-click the Enforcer to assign fire, then press Space to resume.",
            EncounterPhase.Active when solo && !_counterLearned && incoming =>
                "COUNTER THE STRIKE\nPause, press 1 and click the floor beneath the Enforcer, then resume to Interrupt.",
            EncounterPhase.Active when solo && !_counterLearned =>
                "WATCH THE ENFORCER\nIts strike has a wind-up. When the warning appears, pause and Interrupt (1).",
            EncounterPhase.Active when solo && _counterLearned =>
                "STRIKE INTERRUPTED\nKeep firing at your target. Move or Stop (X) to break off.",
            EncounterPhase.Readying when route.Party.Count == 3 =>
                "THREE CREW\nTab focuses the Medic. Heal (1) targets an ally or portrait; Healing Field (2) heals everyone inside.",
            EncounterPhase.Active when route.Party.Count == 3 && observation.Paused =>
                "HOLD THE LINE\nKeep the crew inside the healing field. Barrier stops rifle shots; Taunt protects the Medic.",
            EncounterPhase.Readying =>
                "COORDINATE THE CREW\nDrag to select both, right-click to order. Tab changes ability focus.",
            EncounterPhase.Active when !solo && observation.Paused =>
                "PLAN TOGETHER\nEach crew member keeps one next order. Barrier blocks shots; Interrupt cancels a strike.",
            _ when route.VisibleHostiles.Any(enemy => enemy.EncounterPhase == EncounterPhase.Dormant && !enemy.Combat.IsDefeated) =>
                "ENEMIES AHEAD\nYou see them before they see you. The fight starts the moment one of them spots the crew.",
            _ => "",
        };
        var blocked = route.ActiveDialogue is not null || _controlsOverlay.Visible || _abilityTargeting || _outcomePanel.Visible
            || route.Phase == ScenarioPhase.Completed;
        ShowTip(tip, observation.Paused, blocked);
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
