using Godot;

namespace SpaceAdventure.Game;

public partial class ActionTile : Button
{
    private Label _caption = null!;
    private Label _state = null!;
    private ProgressBar _meter = null!;
    private TextureRect _icon = null!;
    private string? _iconPath;
    private bool? _targeting;
    private Color _accent = TacticalUi.Cyan;

    public void Build(string key, string caption, string icon)
    {
        CustomMinimumSize = new Vector2(94, 88);
        TacticalUi.Style(this);
        var badge = new PanelContainer { Position = new Vector2(6, 6), MouseFilter = MouseFilterEnum.Ignore };
        badge.AddThemeStyleboxOverride("panel", new StyleBoxEmpty());
        var hotkey = TacticalUi.Label(key, 12, "dcebe9");
        hotkey.CustomMinimumSize = new Vector2(12, 0);
        hotkey.HorizontalAlignment = HorizontalAlignment.Center;
        badge.AddChild(hotkey);
        AddChild(badge);
        _icon = new TextureRect { Position = new Vector2(33, 8), Size = new Vector2(28, 28),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_icon);
        _caption = TacticalUi.Label(caption, 13);
        _caption.HorizontalAlignment = HorizontalAlignment.Center;
        _caption.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        _caption.OffsetTop = 41;
        AddChild(_caption);
        _state = TacticalUi.Label("Ready", 12, "afc1c5");
        _state.HorizontalAlignment = HorizontalAlignment.Center;
        _state.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        _state.OffsetTop = 60;
        AddChild(_state);
        _meter = TacticalUi.Bar(TacticalUi.Cyan, 2);
        _meter.AnchorTop = 1; _meter.AnchorBottom = 1; _meter.AnchorRight = 1;
        _meter.OffsetTop = -3; _meter.OffsetLeft = 4; _meter.OffsetRight = -4; _meter.OffsetBottom = -1;
        AddChild(_meter);
        SetState(caption, icon, "Ready", 1);
    }

    public void SetState(string caption, string icon, string state, double readiness)
    {
        _caption.Text = caption;
        _state.Text = state;
        _state.AddThemeColorOverride("font_color", readiness < 1 || state == "Queued" ? TacticalUi.Amber : TacticalUi.Muted);
        _caption.Modulate = Disabled ? new Color("96a5ad") : Colors.White;
        _meter.Value = readiness * 100;
        var targeting = state is "Place barrier" or "Aim + confirm" or "Pick enemy";
        if (_targeting != targeting)
        {
            AddThemeStyleboxOverride("normal", TacticalUi.Box(targeting ? "28443f" : "101c22", targeting ? "e5bc7d" : "36534f", 8));
            _targeting = targeting;
        }
        if (_iconPath != icon)
        {
            _icon.Texture = ResourceLoader.Load<Texture2D>($"res://ui/icons/{icon}.svg");
            _iconPath = icon;
        }
        _icon.Modulate = Disabled ? TacticalUi.Muted : _accent;
    }

    public void SetAccent(Color accent)
    {
        _accent = accent;
        ((StyleBoxFlat)_meter.GetThemeStylebox("fill")).BgColor = accent;
    }
}
