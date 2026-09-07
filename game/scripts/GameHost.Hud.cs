using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class GameHost
{
    private Label _pendingLabel = null!;
    private Label _enemyStatusLabel = null!;
    private ProgressBar _enemyHealth = null!;
    private PanelContainer _enemyPanel = null!;
    private Button _abilityButton = null!;
    private Button _healButton = null!;
    private Button _stopButton = null!;
    private Button _pauseButton = null!;

    private static PanelContainer HudPanel()
    {
        var panel = new PanelContainer { ZIndex = 10 };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.025f, 0.045f, 0.065f, 0.94f),
            BorderColor = new Color("294252"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 12, ContentMarginBottom = 12,
        });
        return panel;
    }

    private static Label HudLabel(string text, int fontSize, string color = "dce6e9")
    {
        var label = new Label { Text = text, Modulate = new Color(color), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", fontSize);
        return label;
    }

    private static Button HudButton(string text, Action action)
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(148, 38), FocusMode = Control.FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", 13);
        button.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color("122532"), BorderColor = new Color("38576a"),
            BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
            CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
        });
        button.Pressed += action;
        return button;
    }

    private void CreateTacticalHud(CanvasLayer canvas)
    {
        var objective = HudPanel();
        objective.OffsetLeft = 20;
        objective.OffsetTop = 20;
        objective.OffsetRight = 370;
        canvas.AddChild(objective);
        var objectiveContent = new VBoxContainer();
        objectiveContent.AddThemeConstantOverride("separation", 5);
        objective.AddChild(objectiveContent);
        objectiveContent.AddChild(HudLabel("FRONTIER STATION  /  01", 11, "86a8b9"));
        _objectiveLabel = HudLabel(string.Empty, 16);
        _objectiveLabel.CustomMinimumSize = new Vector2(320, 38);
        objectiveContent.AddChild(_objectiveLabel);
        _pauseLabel = HudLabel(string.Empty, 12, "f0bb6a");
        objectiveContent.AddChild(_pauseLabel);
        _feedbackLabel = HudLabel(string.Empty, 12, "9bb5c3");
        _feedbackLabel.CustomMinimumSize = new Vector2(320, 30);
        objectiveContent.AddChild(_feedbackLabel);

        _enemyPanel = HudPanel();
        _enemyPanel.AnchorLeft = 1;
        _enemyPanel.AnchorRight = 1;
        _enemyPanel.OffsetLeft = -260;
        _enemyPanel.OffsetRight = -20;
        _enemyPanel.OffsetTop = 20;
        canvas.AddChild(_enemyPanel);
        var enemyContent = new VBoxContainer();
        _enemyPanel.AddChild(enemyContent);
        _enemyStatusLabel = HudLabel("SECURITY ENFORCER", 13, "efa28c");
        _enemyStatusLabel.CustomMinimumSize = new Vector2(210, 36);
        enemyContent.AddChild(_enemyStatusLabel);
        _enemyHealth = new ProgressBar { MaxValue = 100, Value = 100, ShowPercentage = false, CustomMinimumSize = new Vector2(210, 7) };
        _enemyHealth.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = new Color("d76a51") });
        _enemyHealth.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color("38262b") });
        enemyContent.AddChild(_enemyHealth);

        var dock = HudPanel();
        dock.AnchorRight = 1;
        dock.AnchorTop = 1;
        dock.AnchorBottom = 1;
        dock.OffsetLeft = 20;
        dock.OffsetRight = -20;
        dock.OffsetTop = -146;
        dock.OffsetBottom = -20;
        canvas.AddChild(dock);
        var columns = new HBoxContainer();
        columns.AddThemeConstantOverride("separation", 20);
        dock.AddChild(columns);
        _partyList = new VBoxContainer { CustomMinimumSize = new Vector2(184, 0) };
        _partyList.AddThemeConstantOverride("separation", 5);
        columns.AddChild(_partyList);

        var orders = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        orders.AddThemeConstantOverride("separation", 5);
        columns.AddChild(orders);
        _actionLabel = HudLabel(string.Empty, 14);
        _actionLabel.CustomMinimumSize = new Vector2(330, 32);
        orders.AddChild(_actionLabel);
        _pendingLabel = HudLabel(string.Empty, 12, "f0bb6a");
        _pendingLabel.CustomMinimumSize = new Vector2(330, 18);
        orders.AddChild(_pendingLabel);
        _combatLabel = HudLabel(string.Empty, 12, "91adba");
        orders.AddChild(_combatLabel);
        orders.AddChild(HudLabel("WASD pan · Q/E rotate · Wheel zoom · F focus", 11, "79909c"));

        var actions = new GridContainer { Columns = 2 };
        actions.AddThemeConstantOverride("h_separation", 8);
        actions.AddThemeConstantOverride("v_separation", 6);
        columns.AddChild(actions);
        _abilityButton = HudButton("1  Suppressive Fire", BeginAbilityTargeting);
        _abilityButton.TooltipText = "Choose a point within 9 m. Interrupts the Enforcer's windup; waits for any released shot's recovery.";
        actions.AddChild(_abilityButton);
        _healButton = HudButton("2  Field Aid ×1", UseFieldAid);
        _healButton.TooltipText = "Restore 40 health. One charge per attempt. Your explicit attack target resumes afterward.";
        actions.AddChild(_healButton);
        _stopButton = HudButton("X  Stop", StopSelectedActors);
        _stopButton.TooltipText = "Stop the selected crew and clear their attack targets. Released attacks retain their recovery.";
        actions.AddChild(_stopButton);
        _pauseButton = HudButton("SPACE  Pause", () => Dispatch(new SetPauseCommand(NextHumanCommandId("pause"), !_session!.IsPaused)));
        actions.AddChild(_pauseButton);
        _retryButton = HudButton("ENTER  Retry fight", RestartEncounter);
        _retryButton.FocusMode = Control.FocusModeEnum.All;
        _retryButton.Visible = false;
        actions.AddChild(_retryButton);
    }

    private void StopSelectedActors()
    {
        _abilityTargeting = false;
        Dispatch(new StopActorsCommand(NextHumanCommandId("stop"), _selectedActorIds.OrderBy(id => id.Value, StringComparer.Ordinal)));
    }

    private static string ActionSummary(PrimaryActionObservation? action) => action?.Kind switch
    {
        PrimaryActionKind.Attack => $"Carbine · {PhaseSummary(action)}",
        PrimaryActionKind.Ability => $"Suppressive Fire · {PhaseSummary(action)}",
        PrimaryActionKind.Item => $"Field Aid · {PhaseSummary(action)}",
        PrimaryActionKind.Move => "Moving",
        PrimaryActionKind.Interact => "Approaching interaction",
        PrimaryActionKind.Stop => "Stop",
        _ => "Awaiting an order",
    };

    private static string PhaseSummary(PrimaryActionObservation action) => action.Phase switch
    {
        PrimaryActionPhase.Windup => $"preparing {action.PhaseTicksRemaining / 30.0:0.0}s",
        PrimaryActionPhase.Recovery => $"recovering {action.PhaseTicksRemaining / 30.0:0.0}s",
        _ => "approaching target",
    };

    private void UpdateTacticalHud(GameObservation observation, StationRouteObservation route, ActorObservation actor)
    {
        var combat = route.Protagonist.Combat!;
        var phase = route.Encounter!.Phase;
        var active = phase is EncounterPhase.Active or EncounterPhase.Readying;
        var cooldown = combat.Cooldowns.Single().RemainingTicks;
        var charges = combat.Items.Single().Charges;
        _abilityButton.Disabled = !active || cooldown > 0;
        _abilityButton.Text = cooldown > 0 ? $"1  Suppression  {cooldown / 30.0:0.0}s" : "1  Suppressive Fire";
        _healButton.Disabled = !active || charges == 0 || combat.Health == combat.MaximumHealth;
        _healButton.Text = $"2  Field Aid ×{charges}";
        _stopButton.Disabled = phase is EncounterPhase.Defeat or EncounterPhase.Securing || route.ActiveDialogue is not null;
        _stopButton.Visible = phase != EncounterPhase.Defeat;
        _pauseButton.Visible = phase != EncounterPhase.Defeat;
        _pauseButton.Disabled = phase == EncounterPhase.Defeat || route.ActiveDialogue is not null;
        _pauseButton.Text = observation.Paused ? "SPACE  Resume" : "SPACE  Pause";
        _actionLabel.Text = $"{actor.DisplayName}  /  {ActionSummary(actor.CurrentAction)}";
        var waiting = actor.PendingAction?.WaitingReason switch
        {
            ActionWaitingReason.OffensiveRecovery => $"waiting {Math.Max(0, combat.OffensiveRecoveryUntilTick - observation.Tick) / 30.0:0.0}s for recovery",
            ActionWaitingReason.EncounterReadying => "after draw",
            _ => "on resume",
        };
        _pendingLabel.Text = actor.PendingAction is null ? "Right click a target to fire, or the floor to move." : $"Next: {ActionSummary(actor.PendingAction)} · {waiting}";
        _combatLabel.Text = combat.RememberedAttackTargetId is null ? "No attack target selected" : "Attack target: Security Enforcer · resumes after ability or aid";
        _enemyPanel.Visible = phase is EncounterPhase.Active or EncounterPhase.Readying or EncounterPhase.Defeat;
        var enemy = route.Hostiles!.Single();
        _enemyHealth.Value = enemy.Combat.Health;
        var threat = enemy.CurrentAction?.Phase == PrimaryActionPhase.Windup
            ? $"STRIKE IN {enemy.CurrentAction.PhaseTicksRemaining / 30.0:0.0}s"
            : enemy.CurrentAction?.Phase == PrimaryActionPhase.Recovery ? "Recovering" : "Closing in";
        _enemyStatusLabel.Text = $"SECURITY ENFORCER  {enemy.Combat.Health}/100\n{threat}";
        _objectiveLabel.Text = route.Objective.Text;
        if (phase == EncounterPhase.Readying)
        {
            _actionLabel.Text = $"Vanguard / Drawing carbine · {route.Encounter.TransitionTicksRemaining / 30.0:0.0}s";
        }
        if (phase == EncounterPhase.Securing)
        {
            _objectiveLabel.Text = "Threat neutralized";
            _actionLabel.Text = $"Vanguard / Securing carbine · {route.Encounter.TransitionTicksRemaining / 30.0:0.0}s";
            _pendingLabel.Text = "The arena exit opens when your weapon is secured.";
        }
        if (phase == EncounterPhase.Defeat)
        {
            _actionLabel.Text = "Vanguard is down · encounter paused";
            _pendingLabel.Text = "Retry restores health, Field Aid and cooldowns.";
            _combatLabel.Text = "Your completed route progress is preserved.";
        }
        if (route.Party.Count == 2 && phase == EncounterPhase.Victory)
        {
            _objectiveLabel.Text = "Current prototype slice complete";
            _combatLabel.Text = "Protector recruited · the next encounter is in development";
        }
        if (_reviewDrivesClock && _reviewMode == "record") { _pauseLabel.Text = "COMBAT REVIEW"; }
    }
}
