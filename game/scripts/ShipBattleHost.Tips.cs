using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

/// <summary>
/// Step-by-step tips for the first battle, each shown once under the INCOMING column when its situation first
/// arises (aiming, resuming, incoming fire, fire, breach, damage, injuries, lowered shields, power). A tip ages
/// only while time runs, so it can be read during a pause, and the player can dismiss it. Presentation only.
/// </summary>
public partial class ShipBattleHost
{
    private const double TipSeconds = 10;
    private const int PowerTipTick = 25 * ShipCombatSession.TicksPerSecond;
    private PanelContainer _tipCard = null!;
    private Label _tipTitle = null!;
    private RichTextLabel _tipBody = null!;
    private string _tipKey = "";
    private int _tipPromptGeneration;
    private double _tipAge;
    private ulong _tipClockMs;
    private readonly HashSet<string> _retiredTips = new(StringComparer.Ordinal);

    private void BuildTips(Container column)
    {
        _tipCard = new PanelContainer { Name = "Tip", Visible = false, MouseFilter = MouseFilterEnum.Stop };
        _tipCard.AddThemeStyleboxOverride("panel", TacticalUi.TrackerBox(TacticalUi.Amber, horizontal: 10, vertical: 7));
        column.AddChild(_tipCard);
        var content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        content.AddThemeConstantOverride("separation", 3);
        _tipCard.AddChild(content);
        var header = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        content.AddChild(header);
        _tipTitle = TacticalUi.Label("", 11, "f0d9b0");
        _tipTitle.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _tipTitle.AddThemeFontOverride("font", TacticalUi.BoldFont);
        header.AddChild(_tipTitle);
        var close = new Button { Text = "✕", Flat = true, FocusMode = FocusModeEnum.None, TooltipText = "Dismiss tip" };
        close.AddThemeFontSizeOverride("font_size", 10);
        close.AddThemeColorOverride("font_color", TacticalUi.Muted);
        close.Pressed += () => { _retiredTips.Add(_tipKey); _tipCard.Visible = false; };
        header.AddChild(close);
        _tipBody = TacticalUi.RichLabel("", 11, "c3d0d4", wrap: true);
        _tipBody.CustomMinimumSize = new Vector2(150, 0);
        content.AddChild(_tipBody);
    }

    /// <summary>Controller wording for tips that name keys or clicks; a tip keeps its mouse text as its identity.</summary>
    private static string? PadTipBody(string title)
    {
        static string G(PadButton button) => InputPrompts.Bb(button, 15);
        return title switch
        {
            "AIM YOUR WEAPONS" => $"Press {G(PadButton.X)} or {G(PadButton.Y)}, pick an enemy room with {G(PadButton.LS)} and fire with {G(PadButton.A)}. Hit their Weapons first to silence them.",
            "LET TIME RUN" => $"Press {G(PadButton.RT)} to resume. Pause whenever you need to give new orders.",
            "FIRE ABOARD" => $"Crew fight fires in their own room. Pick crew with {G(PadButton.LB)}{G(PadButton.RB)}, highlight the burning room with {G(PadButton.LS)} and press {G(PadButton.A)}.",
            "INJURED CREW" => $"The Medic heals her room. Select her alone, highlight the injured crew member's room and press {G(PadButton.A)} to treat them.",
            "SHIELDS DOWN" => $"Every shot lands now. Hold ({G(PadButton.LT)}) readies all weapons to fire together next time.",
            "POWER" => $"{G(PadButton.DpadHorizontal)} picks a system, {G(PadButton.DpadVertical)} adds or removes a bar. The reactor cannot run everything.",
            _ => null,
        };
    }

    private void UpdateTips(ShipBattleObservation observation)
    {
        var now = Time.GetTicksMsec();
        var elapsed = _tipClockMs == 0 ? 0 : (now - _tipClockMs) / 1000.0;
        _tipClockMs = now;
        if (observation.Phase != ShipBattlePhase.Active) { _tipCard.Visible = false; return; }
        var tip = NextTip(observation);
        if (tip != _tipKey)
        {
            if (_tipKey.Length > 0) { _retiredTips.Add(_tipKey); }
            _tipKey = tip;
            _tipAge = 0;
            _tipPromptGeneration = InputDevice.Generation - 1;
        }
        if (_tipPromptGeneration != InputDevice.Generation)
        {
            // Switching device rewrites the same tip in the other device's words without restarting it.
            _tipPromptGeneration = InputDevice.Generation;
            var lines = tip.Split('\n', 2);
            _tipTitle.Text = lines[0];
            _tipBody.Text = InputDevice.UsingGamepad && PadTipBody(lines[0]) is { } pad ? pad : lines.Length > 1 ? lines[1] : "";
        }
        if (tip.Length == 0 || _retiredTips.Contains(tip)) { _tipCard.Visible = false; return; }
        if (!observation.Paused) { _tipAge += Math.Min(elapsed, .25); }
        if (_tipAge > TipSeconds) { _retiredTips.Add(tip); _tipCard.Visible = false; return; }
        _tipCard.Visible = true;
        _tipCard.Modulate = new Color(1, 1, 1, (float)Math.Clamp((TipSeconds - _tipAge) / .6, 0, 1));
    }

    /// <summary>The most pressing tip not yet shown, or empty.</summary>
    private string NextTip(ShipBattleObservation observation)
    {
        var player = observation.Player;
        var candidates = new List<string>();
        if (player.Weapons.All(weapon => weapon.Target is null))
        { candidates.Add("AIM YOUR WEAPONS\nClick a weapon card, then an enemy room. Hit their Weapons first to silence them."); }
        else if (observation.Paused && observation.Tick == 0)
        { candidates.Add("LET TIME RUN\nPress Space to resume. Pause whenever you need to give new orders."); }
        if (observation.Rooms.Any(room => room.FireSeverity > 0))
        { candidates.Add("FIRE ABOARD\nCrew fight fires in their own room. Select a crew card and right-click the burning room to send help."); }
        if (observation.Rooms.Any(room => room.BreachSeverity > 0))
        { candidates.Add("HULL BREACH\nAir leaks out until it is sealed. Crew seal it on their own; closed doors keep the other rooms breathable."); }
        if (observation.Crew.Any(crew => !crew.Downed && crew.Health < crew.MaxHealth * .6))
        { candidates.Add("INJURED CREW\nThe Medic heals her room. Select her and right-click an injured crew member to treat them."); }
        if (player.Systems.Any(system => system.Damage > 0))
        { candidates.Add("SYSTEM DAMAGED\nBroken bars stop working until repaired. Crew repair their own room automatically."); }
        if (observation.Intents.Any(intent => intent.TicksToFire is > 0 and < 3 * ShipCombatSession.TicksPerSecond))
        { candidates.Add("INCOMING FIRE\nThe right column shows what each enemy weapon will hit and when. Shields stop lasers; missiles ignore them."); }
        if (observation.Enemy.ShieldLayers == 0 && observation.Tick > 0)
        { candidates.Add("SHIELDS DOWN\nEvery shot lands now. Hold (H) readies all weapons to fire together next time."); }
        if (observation.Tick >= PowerTipTick)
        { candidates.Add("POWER\nLeft-click a system's power column to add a bar, right-click to remove one. The reactor cannot run everything."); }
        return candidates.FirstOrDefault(candidate => !_retiredTips.Contains(candidate)) ?? "";
    }
}
