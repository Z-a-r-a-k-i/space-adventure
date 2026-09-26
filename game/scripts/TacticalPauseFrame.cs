using Godot;

namespace SpaceAdventure.Game;

/// <summary>
/// Full-screen tactical-pause treatment: a faint edge glow, a hairline border and corner brackets, faded in
/// on real time so it reads immediately while the simulation is frozen. Never takes mouse input.
/// </summary>
public sealed partial class TacticalPauseFrame : Control
{
    private const float FadeSeconds = .16f;
    private float _alpha;

    public TacticalPauseFrame()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsPreset(LayoutPreset.FullRect);
    }

    public Color Accent { get; set; } = new("ffc45c");

    public bool Shown { get; set; }

    public override void _Process(double delta)
    {
        var target = Shown ? 1f : 0f;
        var next = Mathf.MoveToward(_alpha, target, (float)delta / FadeSeconds);
        if (Mathf.IsEqualApprox(next, _alpha)) { return; }
        _alpha = next;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_alpha <= 0) { return; }
        var rect = new Rect2(Vector2.Zero, Size);
        // Soft inner glow: a few nested translucent borders that fade toward the play area.
        for (var step = 0; step < 6; step++)
        {
            DrawRect(rect.Grow(-step * 3), new Color(Accent, .05f * (6 - step) / 6 * _alpha), false, 3);
        }
        DrawRect(rect.Grow(-6), new Color(Accent, .35f * _alpha), false, 1);
        var arm = Mathf.Min(Size.X, Size.Y) * .06f;
        var inset = 10f;
        var color = new Color(Accent, .9f * _alpha);
        foreach (var (corner, dx, dy) in new[]
        {
            (new Vector2(inset, inset), 1, 1), (new Vector2(Size.X - inset, inset), -1, 1),
            (new Vector2(inset, Size.Y - inset), 1, -1), (new Vector2(Size.X - inset, Size.Y - inset), -1, -1),
        })
        {
            DrawLine(corner, corner + new Vector2(arm * dx, 0), color, 2);
            DrawLine(corner, corner + new Vector2(0, arm * dy), color, 2);
        }
    }
}
