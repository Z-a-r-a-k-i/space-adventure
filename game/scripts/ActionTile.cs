using Godot;

namespace SpaceAdventure.Game;

public partial class ActionTile : Button
{
    private Label _caption = null!;
    private Label _state = null!;
    private ProgressBar _meter = null!;
    private TextureRect _icon = null!;
    private string? _iconPath;

    public void Build(string key, string caption, string icon)
    {
        CustomMinimumSize = new Vector2(94, 88);
        TacticalUi.Style(this);
        var hotkey = TacticalUi.Label(key, 11, "8fa7b6");
        hotkey.Position = new Vector2(8, 5);
        AddChild(hotkey);
        _icon = new TextureRect { Position = new Vector2(33, 8), Size = new Vector2(28, 28),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_icon);
        _caption = TacticalUi.Label(caption, 12);
        _caption.HorizontalAlignment = HorizontalAlignment.Center;
        _caption.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        _caption.OffsetTop = 41;
        AddChild(_caption);
        _state = TacticalUi.Label("Ready", 11, "8fa7b6");
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
        _meter.Value = readiness * 100;
        if (_iconPath != icon)
        {
            _icon.Texture = ResourceLoader.Load<Texture2D>($"res://ui/icons/{icon}.svg");
            _iconPath = icon;
        }
        _icon.Modulate = Disabled ? TacticalUi.Muted : TacticalUi.Cyan;
    }
}
