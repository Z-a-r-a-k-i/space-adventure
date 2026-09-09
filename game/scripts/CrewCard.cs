using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

public partial class CrewCard : Button
{
    private Label _name = null!;
    private Label _vitals = null!;
    private Label _action = null!;
    private Label _pending = null!;
    private Label _target = null!;
    private ProgressBar _health = null!;
    private TextureRect _portrait = null!;
    private bool? _selected;
    private bool? _focused;

    public void Build(string portraitPath)
    {
        CustomMinimumSize = new Vector2(226, 110);
        TacticalUi.Style(this);
        _portrait = new TextureRect
        {
            OffsetLeft = 6, OffsetTop = 6, OffsetRight = 82, OffsetBottom = 104,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = MouseFilterEnum.Ignore,
            Texture = ResourceLoader.Load<Texture2D>(portraitPath),
        };
        AddChild(_portrait);
        var column = new VBoxContainer { OffsetLeft = 92, OffsetTop = 9, OffsetRight = 215, OffsetBottom = 103,
            MouseFilter = MouseFilterEnum.Ignore };
        column.AddThemeConstantOverride("separation", 1);
        AddChild(column);
        _name = TacticalUi.Label("", 14);
        _vitals = TacticalUi.Label("", 12, "a4c4c9");
        _health = TacticalUi.Bar(TacticalUi.Cyan, 5);
        _action = TacticalUi.Label("", 12);
        _pending = TacticalUi.Label("", 11, "e9bd76");
        _target = TacticalUi.Label("", 11, "a4c4c9");
        foreach (var label in new[] { _name, _vitals, _action, _pending, _target })
        { label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis; }
        column.AddChild(_name);
        column.AddChild(_vitals);
        column.AddChild(_health);
        column.AddChild(_action);
        column.AddChild(_target);
        column.AddChild(_pending);
    }

    public void Synchronize(ActorObservation actor, bool selected, bool focused, string current, string pending, string target, Color color)
    {
        if (selected != _selected || focused != _focused)
        {
            var style = TacticalUi.Box(selected ? "172e3a" : "0e1c27", selected ? "72deeb" : "2d4353", 0);
            style.BorderWidthLeft = focused ? 3 : 1;
            AddThemeStyleboxOverride("normal", style);
            _selected = selected;
            _focused = focused;
        }
        _name.Text = actor.DisplayName.ToUpperInvariant();
        _name.Modulate = color;
        _vitals.Text = actor.Combat is { } combat ? $"{combat.Health} / {combat.MaximumHealth}  HP" : "";
        _health.MaxValue = actor.Combat?.MaximumHealth ?? 100;
        _health.Value = actor.Combat?.Health ?? 100;
        _portrait.Modulate = actor.Combat?.IsDefeated == true ? new Color("6d7d88") : Colors.White;
        _action.Text = actor.Combat?.IsDefeated == true ? "DOWN" : current;
        _pending.Text = pending;
        _pending.Visible = pending.Length > 0;
        _target.Text = target.Length > 0 ? $"→ {target.Replace("Security ", "", StringComparison.Ordinal)}" : "";
        _target.Visible = _target.Text.Length > 0;
        TooltipText = $"{actor.Loadout?.WeaponName}\n{current}{(target.Length > 0 ? $" → {target}" : "")}\n{pending}\nClick to select · Shift-click to group";
    }
}
