using Godot;

namespace SpaceAdventure.Game;

internal enum PadButton
{
    A, B, X, Y, LB, RB, LT, RT, LS, RS, L3, R3, View, Menu,
    Dpad, DpadUp, DpadDown, DpadLeft, DpadRight, DpadHorizontal, DpadVertical,
}

/// <summary>
/// Project-authored Xbox-style glyphs (<c>res://ui/icons/pad</c>) and device-aware prompt text. Glyph shapes are
/// white so the caller tints them: face buttons carry their controller colours, the rest the HUD's neutral tone.
/// </summary>
internal static class InputPrompts
{
    public static readonly Color Neutral = new("dce6e9");

    public static string Name(PadButton button) => button switch
    {
        PadButton.DpadUp => "dpad_up",
        PadButton.DpadDown => "dpad_down",
        PadButton.DpadLeft => "dpad_left",
        PadButton.DpadRight => "dpad_right",
        PadButton.DpadHorizontal => "dpad_horizontal",
        PadButton.DpadVertical => "dpad_vertical",
        _ => button.ToString().ToLowerInvariant(),
    };

    public static Color Tint(PadButton button) => button switch
    {
        PadButton.A => new Color("6cc24a"),
        PadButton.B => new Color("e8544e"),
        PadButton.X => new Color("4f9ee8"),
        PadButton.Y => new Color("f2c14e"),
        _ => Neutral,
    };

    public static Texture2D? Glyph(PadButton button) => TacticalUi.Icon($"pad/{Name(button)}");

    /// <summary>Inline glyph for a RichTextLabel with BBCode enabled.</summary>
    public static string Bb(PadButton button, int size = 18) =>
        $"[img width={size} height={size} color=#{Tint(button).ToHtml(false)}]res://ui/icons/pad/{Name(button)}.svg[/img]";

    /// <summary>The keyboard/mouse wording or the controller wording, for the device in use.</summary>
    public static string Pick(string keyboard, string pad) => InputDevice.UsingGamepad ? pad : keyboard;

    public static TextureRect GlyphRect(PadButton button, float size) => new()
    {
        Texture = Glyph(button), Modulate = Tint(button), CustomMinimumSize = new Vector2(size, size),
        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
    };
}
