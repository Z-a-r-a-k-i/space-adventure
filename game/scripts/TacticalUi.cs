using Godot;

namespace SpaceAdventure.Game;

internal static class TacticalUi
{
    private static Theme? _tooltipTheme;
    public static readonly Color Ink = new("0b1722");
    public static readonly Color Cyan = new("72deeb");
    public static readonly Color Amber = new("e9bd76");
    public static readonly Color Muted = new("8fa7b6");
    public static readonly Color Danger = new("ee907d");

    public static StyleBoxFlat Box(string background = "0b1722", string border = "2d4353", int margin = 12) => new()
    {
        BgColor = new Color(background, .96f), BorderColor = new Color(border),
        BorderWidthLeft = 1, BorderWidthRight = 1, BorderWidthTop = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5,
        ContentMarginLeft = margin, ContentMarginRight = margin, ContentMarginTop = margin, ContentMarginBottom = margin,
        ShadowColor = new Color(0, 0, 0, .25f), ShadowSize = 5, ShadowOffset = new Vector2(0, 3),
    };

    public static Label Label(string text, int size = 13, string color = "e1e8eb")
    {
        var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", new Color(color));
        return label;
    }

    public static void Style(Button button)
    {
        if (_tooltipTheme is null)
        {
            _tooltipTheme = new Theme();
            _tooltipTheme.SetStylebox("panel", "TooltipPanel", Box("132430", "486474", 8));
            _tooltipTheme.SetFontSize("font_size", "TooltipLabel", 12);
            _tooltipTheme.SetColor("font_color", "TooltipLabel", new Color("e1e8eb"));
        }
        button.Theme = _tooltipTheme;
        button.FocusMode = Control.FocusModeEnum.None;
        button.AddThemeStyleboxOverride("normal", Box("132430"));
        button.AddThemeStyleboxOverride("hover", Box("203c49", "72deeb"));
        button.AddThemeStyleboxOverride("pressed", Box("274957", "c5f5fa"));
        button.AddThemeStyleboxOverride("disabled", Box("101c26", "253442"));
        button.AddThemeStyleboxOverride("focus", Box("203c49", "72deeb"));
        button.AddThemeFontSizeOverride("font_size", 14);
        button.AddThemeColorOverride("font_color", new Color("e1e8eb"));
        button.AddThemeColorOverride("font_disabled_color", Muted);
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
