using Godot;

namespace SpaceAdventure.Game;

internal static class TacticalUi
{
    private static Theme? _tooltipTheme;
    public static readonly Color Ink = new("101c22");
    public static readonly Color Cyan = new("a0efd8");
    public static readonly Color Amber = new("e5bc7d");
    public static readonly Color Muted = new("afc1c5");
    public static readonly Color Danger = new("ee907d");
    public static readonly Color Protector = new("e5bc7d");

    // Ship battle tokens extend the same kit: friendly mint-cyan, hostile orange, shield blue.
    public static readonly Color Hostile = new("ff7a4d");
    public static readonly Color Shield = new("63c9ff");
    public static readonly Color Power = new("8ee6b4");
    public static readonly Color PowerOff = new("24343d");
    public static readonly Color Damaged = new("ff5c4d");
    public static readonly Color Oxygen = new("ff6f9c");
    public static readonly Color Line = new("344651");

    private static readonly Dictionary<string, Texture2D?> Icons = new(StringComparer.Ordinal);
    private static FontFile? _boldFont;

    /// <summary>Bold face of the project font for headings and numbers; the regular face is the project default.</summary>
    public static Font BoldFont => _boldFont ??= ResourceLoader.Load<FontFile>("res://ui/fonts/chakra_petch_bold.ttf");

    /// <summary>White line icon from <c>res://ui/icons</c>, tinted by the caller; null when the file is missing.</summary>
    public static Texture2D? Icon(string name)
    {
        if (Icons.TryGetValue(name, out var cached)) { return cached; }
        var path = $"res://ui/icons/{name}.svg";
        var texture = ResourceLoader.Exists(path) ? ResourceLoader.Load<Texture2D>(path) : null;
        Icons[name] = texture;
        return texture;
    }

    public static StyleBoxFlat Box(string background = "09131d", string border = "344651", int margin = 12) => new()
    {
        BgColor = new Color(background, .96f), BorderColor = new Color(border),
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3, CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3,
        ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = margin, ContentMarginBottom = margin,
        ShadowColor = new Color(0, 0, 0, .36f), ShadowSize = 8, ShadowOffset = new Vector2(0, 4),
    };

    public static Label Label(string text, int size = 13, string color = "e1e8eb")
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color(color));
        return label;
    }

    public static Label Eyebrow(string text, string color = "8fa7b6") => Label(text, 11, color);

    public static ColorRect Rule(Color color) => new()
    {
        Color = color, CustomMinimumSize = new Vector2(0, 2), MouseFilter = Control.MouseFilterEnum.Ignore,
    };

    public static StyleBoxFlat FieldPanel(Color accent, bool bottom = false, int margin = 12)
    {
        var box = Box("101c22", accent.ToHtml(false), margin);
        box.BgColor = new Color("101c22", .97f);
        box.BorderWidthTop = box.BorderWidthRight = 0;
        box.BorderWidthLeft = bottom ? 0 : 2;
        box.BorderWidthBottom = bottom ? 2 : 0;
        box.CornerRadiusTopLeft = box.CornerRadiusTopRight = box.CornerRadiusBottomLeft = box.CornerRadiusBottomRight = 0;
        box.ShadowSize = 3;
        box.ShadowOffset = new Vector2(0, 2);
        return box;
    }

    public static void Style(Button button)
    {
        if (_tooltipTheme is null)
        {
            _tooltipTheme = new Theme();
            _tooltipTheme.SetStylebox("panel", "TooltipPanel", Box("132430", "486474", 8));
            _tooltipTheme.SetFontSize("font_size", "TooltipLabel", 14);
            _tooltipTheme.SetColor("font_color", "TooltipLabel", new Color("e1e8eb"));
        }
        button.Theme = _tooltipTheme;
        button.FocusMode = Control.FocusModeEnum.None;
        button.AddThemeStyleboxOverride("normal", Box("101c22", "36534f", 8));
        button.AddThemeStyleboxOverride("hover", Box("203c3c", "a0efd8", 8));
        button.AddThemeStyleboxOverride("pressed", Box("284640", "c5f5e6", 8));
        button.AddThemeStyleboxOverride("disabled", Box("101c26", "253442"));
        var focus = Box("203c49", "8bddd9", 0);
        focus.BgColor = Colors.Transparent;
        focus.BorderWidthLeft = focus.BorderWidthRight = focus.BorderWidthTop = focus.BorderWidthBottom = 2;
        button.AddThemeStyleboxOverride("focus", focus);
        button.AddThemeFontSizeOverride("font_size", 14);
        button.AddThemeColorOverride("font_color", new Color("e1e8eb"));
        button.AddThemeColorOverride("font_disabled_color", Muted);
        button.AddThemeColorOverride("font_hover_color", Colors.White);
        button.AddThemeColorOverride("font_pressed_color", Cyan);
    }

    public static ProgressBar Bar(Color color, float height = 4)
    {
        var bar = new ProgressBar { ShowPercentage = false, CustomMinimumSize = new Vector2(0, height),
            MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = color });
        bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color("243342") });
        return bar;
    }
}
