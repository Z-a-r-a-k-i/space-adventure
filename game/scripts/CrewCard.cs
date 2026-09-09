using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class CrewCard : Button
{
    private Label _name = null!;
    private Label _vitals = null!;
    private Label _status = null!;
    private Label _focusBadge = null!;
    private ProgressBar _health = null!;
    private TextureRect _portrait = null!;
    private StyleBoxFlat _healthFill = null!;
    private bool? _selected;
    private bool? _focused;

    public void Build(string portraitPath)
    {
        CustomMinimumSize = new Vector2(284, 78);
        TacticalUi.Style(this);
        _portrait = new TextureRect
        {
            OffsetLeft = 9, OffsetTop = 9, OffsetRight = 51, OffsetBottom = 69,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore, Texture = ResourceLoader.Load<Texture2D>(portraitPath),
        };
        AddChild(_portrait);
        _name = TacticalUi.Label("", 14);
        _name.Position = new Vector2(61, 8);
        AddChild(_name);
        _focusBadge = TacticalUi.Label("", 10, "a0efd8");
        _focusBadge.HorizontalAlignment = HorizontalAlignment.Right;
        _focusBadge.Position = new Vector2(198, 10);
        _focusBadge.Size = new Vector2(75, 14);
        AddChild(_focusBadge);
        _health = TacticalUi.Bar(TacticalUi.Cyan, 4);
        _health.Position = new Vector2(61, 32);
        _health.Size = new Vector2(127, 4);
        _healthFill = (StyleBoxFlat)_health.GetThemeStylebox("fill");
        AddChild(_health);
        _vitals = TacticalUi.Label("", 11);
        _vitals.HorizontalAlignment = HorizontalAlignment.Right;
        _vitals.Position = new Vector2(195, 25);
        _vitals.Size = new Vector2(78, 18);
        AddChild(_vitals);
        _status = TacticalUi.Label("", 11, "afc1c5");
        _status.Position = new Vector2(61, 49);
        _status.Size = new Vector2(212, 19);
        _status.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        AddChild(_status);
    }

    public void Synchronize(ActorObservation actor, bool selected, bool focused, string current, string pending, string target, Color color)
    {
        if (selected != _selected || focused != _focused)
        {
            var style = TacticalUi.FieldPanel(selected ? color : new Color("48615f"), margin: 0);
            style.BgColor = new Color(focused ? "183538" : "101c22", .98f);
            style.BorderWidthLeft = selected ? 3 : 1;
            AddThemeStyleboxOverride("normal", style);
            _selected = selected;
            _focused = focused;
        }
        var down = actor.Combat?.IsDefeated == true;
        _name.Text = actor.DisplayName;
        _focusBadge.Text = down ? "DOWN" : focused ? "FOCUS" : selected ? "SELECTED" : "";
        _focusBadge.Modulate = down ? TacticalUi.Danger : focused ? color : TacticalUi.Muted;
        _vitals.Text = actor.Combat is { } combat ? $"{combat.Health}/{combat.MaximumHealth} HP" : "";
        _health.MaxValue = actor.Combat?.MaximumHealth ?? 100;
        _health.Value = actor.Combat?.Health ?? 100;
        _healthFill.BgColor = _health.Value <= _health.MaxValue * .3 ? TacticalUi.Danger : color;
        _portrait.Modulate = down ? new Color("657677") : Colors.White;
        _status.Text = down ? "Crew member down" : pending.Length > 0 ? $"Next · {pending}" : target.Length > 0 ? $"{current} → {target}" : current;
        _status.AddThemeColorOverride("font_color", down ? TacticalUi.Danger : pending.Length > 0 ? TacticalUi.Amber : TacticalUi.Muted);
        var cooldowns = string.Join(" · ", actor.Combat?.Cooldowns.Select(value =>
            $"{(value.AbilityId == actor.Loadout?.ActiveAbilityId ? actor.Loadout?.ActiveAbilityName : actor.Loadout?.SecondaryAbilityName)} {value.RemainingTicks / 30.0:0.0}s") ?? []);
        TooltipText = $"{actor.DisplayName} · {actor.Loadout?.WeaponName}\n{current}{(target.Length > 0 ? $" → {target}" : "")}"
            + (pending.Length > 0 ? $"\nNext: {pending}" : "") + (cooldowns.Length > 0 ? $"\n{cooldowns}" : "")
            + "\nClick to select · Shift-click to group · Tab changes ability focus";
    }
}
