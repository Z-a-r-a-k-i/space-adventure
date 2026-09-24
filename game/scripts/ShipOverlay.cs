using Godot;

namespace SpaceAdventure.Game;

/// <summary>
/// Screen-space layer over one ship view: in-room system badges, power pips, target and incoming reticles, crew
/// health bars and floating combat text. The host rebuilds a display list every frame from observations; the
/// overlay only draws it and owns no state.
/// </summary>
public sealed partial class ShipOverlay : Control
{
    private readonly List<Action<ShipOverlay>> _commands = [];

    public ShipOverlay() => MouseFilter = MouseFilterEnum.Ignore;

    /// <summary>Frame-local rectangles the latest display list occupies (review checks keep badges clear of each other).</summary>
    public List<Rect2> Badges { get; } = [];

    public void Present(IEnumerable<Action<ShipOverlay>> commands)
    {
        _commands.Clear();
        _commands.AddRange(commands);
        Badges.Clear();
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var command in _commands) { command(this); }
    }

    public void Icon(string name, Vector2 centre, float size, Color color)
    {
        if (TacticalUi.Icon(name) is not { } texture) { return; }
        DrawTextureRect(texture, new Rect2(centre - Vector2.One * size / 2, Vector2.One * size), false, color);
    }

    /// <summary>Round badge with an icon: the in-room system marker.</summary>
    public void Badge(string icon, Vector2 centre, float radius, Color ring, Color fill)
    {
        DrawCircle(centre, radius, fill);
        DrawArc(centre, radius, 0, Mathf.Tau, 32, ring, 2, true);
        Icon(icon, centre, radius * 1.25f, ring);
        Badges.Add(new Rect2(centre - Vector2.One * radius, Vector2.One * radius * 2));
    }

    /// <summary>Horizontal power pips: filled usable, hollow allocated-but-damaged in red, empty unpowered.</summary>
    public void Pips(Vector2 centre, int total, int usable, int damaged, Color on, float width = 7, float height = 4)
    {
        var gap = 2f;
        var span = total * width + (total - 1) * gap;
        for (var index = 0; index < total; index++)
        {
            var rect = new Rect2(centre.X - span / 2 + index * (width + gap), centre.Y - height / 2, width, height);
            var broken = index >= total - damaged;
            DrawRect(rect, broken ? TacticalUi.Damaged : index < usable ? on : new Color(TacticalUi.PowerOff, .95f));
            if (broken) { DrawRect(rect, new Color(1, 1, 1, .5f), false, 1); }
        }
    }

    public void Bar(Rect2 rect, float fraction, Color fill, Color? back = null)
    {
        DrawRect(rect, back ?? new Color(0, 0, 0, .65f));
        DrawRect(new Rect2(rect.Position, new Vector2(rect.Size.X * Math.Clamp(fraction, 0, 1), rect.Size.Y)), fill);
    }

    /// <summary>Crosshair reticle; <paramref name="spin"/> animates from the presentation clock.</summary>
    public void Reticle(Vector2 centre, float radius, Color color, float spin, float width = 2)
    {
        for (var quarter = 0; quarter < 4; quarter++)
        {
            var start = spin + quarter * Mathf.Pi / 2 + .25f;
            DrawArc(centre, radius, start, start + Mathf.Pi / 2 - .5f, 10, color, width, true);
        }
        for (var quarter = 0; quarter < 4; quarter++)
        {
            var direction = Vector2.FromAngle(spin + quarter * Mathf.Pi / 2);
            DrawLine(centre + direction * (radius - 5), centre + direction * (radius + 5), color, width, true);
        }
    }

    public void Frame(Rect2 rect, Color color, float width = 2, Color? fill = null)
    {
        if (fill is { } inside) { DrawRect(rect, inside); }
        DrawRect(rect, color, false, width);
    }

    public void Text(Vector2 position, string text, int size, Color color, bool centred = true, bool bold = false)
    {
        var font = bold ? TacticalUi.BoldFont : GetThemeDefaultFont();
        var measured = font.GetStringSize(text, HorizontalAlignment.Left, -1, size);
        var origin = centred ? position - new Vector2(measured.X / 2, -size * .35f) : position;
        DrawStringOutline(font, origin, text, HorizontalAlignment.Left, -1, size, 4, new Color(0, 0, 0, .85f));
        DrawString(font, origin, text, HorizontalAlignment.Left, -1, size, color);
    }

    public void Ring(Vector2 centre, float radius, Color color, float fraction, float width = 3)
    {
        DrawArc(centre, radius, -Mathf.Pi / 2, -Mathf.Pi / 2 + Mathf.Tau * Math.Clamp(fraction, 0, 1), 32, color, width, true);
    }
}
