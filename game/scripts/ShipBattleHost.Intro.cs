using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

/// <summary>
/// Arrival from the station: the battle fades in from the departure's black title card, the cutter's view eases
/// out, a hostile-contact warning sounds and the interceptor warps into its frame before the HUD settles in.
/// Real-time presentation only: the battle stays paused at tick 0 throughout, and any key or click skips it.
/// </summary>
public partial class ShipBattleHost
{
    private const float IntroSeconds = 5.4f;
    private const float WarpInSeconds = 2.5f;
    private float _introSeconds;
    private bool _introWarped;
    private ColorRect? _introBlack;
    private VBoxContainer? _introBanner;
    private MeshInstance3D? _introFlash;

    public bool IntroPlaying { get; private set; }

    private void StartIntro()
    {
        IntroPlaying = true;
        _introSeconds = 0;
        _introBlack = new ColorRect { Name = "IntroFade", Color = Colors.Black, MouseFilter = MouseFilterEnum.Ignore };
        _introBlack.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(_introBlack);
        _introBanner = new VBoxContainer { Name = "IntroBanner", MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        _introBanner.AddThemeConstantOverride("separation", 4);
        var title = TacticalUi.Label("HOSTILE CONTACT", 26, "ff7a4d");
        title.AddThemeFontOverride("font", TacticalUi.BoldFont);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        var detail = TacticalUi.Label("Security interceptor on an attack run", 13, "ffc2ad");
        detail.HorizontalAlignment = HorizontalAlignment.Center;
        _introBanner.AddChild(title);
        _introBanner.AddChild(detail);
        AddChild(_introBanner);
        _introFlash = ShipBattleView.Glow(new Color("ffc49a"), 2.2f, 0);
        _introFlash.Visible = false;
        _enemyView.Overlay.AddChild(_introFlash);
        AdvanceIntro(0);
    }

    private void AdvanceIntro(double delta)
    {
        if (!IntroPlaying) { return; }
        _introSeconds += (float)Math.Min(delta, .1);
        var t = _introSeconds;
        _introBlack!.Color = new Color(0, 0, 0, 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp(t / .9f, 0, 1)));
        GetNode<Control>("Hud").Modulate = new Color(1, 1, 1, Mathf.SmoothStep(0, 1, Mathf.Clamp((t - 4.3f) / .8f, 0, 1)));
        // The cutter's frame eases from a close follow shot out to the tactical overview.
        var settle = Mathf.SmoothStep(0, 1, Mathf.Clamp(t / 4.3f, 0, 1));
        _playerView.FocusOn(_playerView.Bounds.GetCenter() + new Vector2(0, (1 - settle) * 2.2f), Mathf.Lerp(.55f, 1, settle));
        // The interceptor is absent until it warps in with a flash and a short shake of its frame.
        var warp = Mathf.Clamp((t - WarpInSeconds) / .35f, 0, 1);
        _enemyView.Ship.Visible = warp > 0;
        _enemyView.Ship.Scale = Vector3.One * Mathf.Lerp(.15f, 1, 1 - (1 - warp) * (1 - warp));
        if (warp <= 0) { _enemyView.Bubble.Visible = false; }
        if (!_introWarped && warp > 0)
        {
            _introWarped = true;
            GameAudio.Play("ship.shield_up");
        }
        var flash = Mathf.Clamp((t - WarpInSeconds) / .45f, 0, 1);
        _introFlash!.Visible = flash is > 0 and < 1;
        var centre = _enemyView.Bounds.GetCenter();
        _introFlash.Position = new Vector3(centre.X, ShipBattleView.FloorY + 2, centre.Y);
        var size = Mathf.Lerp(2f, 7f, flash);
        _introFlash.Scale = new Vector3(size, 1, size);
        _introFlash.Transparency = Mathf.Sqrt(flash);
        _enemyView.SetShake(flash is > 0 and < .5f ? new Vector2(Mathf.Sin(t * 70) * .12f, Mathf.Cos(t * 57) * .1f) : Vector2.Zero);
        // Contact banner over the enemy frame, with the alarm as the warning appears.
        var bannerIn = Mathf.Clamp((t - 1.6f) / .3f, 0, 1) * (1 - Mathf.Clamp((t - 4.2f) / .5f, 0, 1));
        _introBanner!.Modulate = new Color(1, 1, 1, bannerIn);
        var frame = _enemyView.Frame.GetGlobalRect();
        _introBanner.Position = new Vector2(frame.Position.X + frame.Size.X / 2 - _introBanner.Size.X / 2, frame.Position.Y + frame.Size.Y * .2f);
        if (t >= 1.6f && t - (float)Math.Min(delta, .1) < 1.6f) { GameAudio.Play("ship.alarm"); }
        if (t >= IntroSeconds) { EndIntro(); }
    }

    /// <summary>Finishes the arrival immediately in its settled state (end of the timeline or a skip).</summary>
    public void EndIntro()
    {
        if (!IntroPlaying) { return; }
        IntroPlaying = false;
        _introBlack?.QueueFree();
        _introBanner?.QueueFree();
        _introFlash?.QueueFree();
        _introBlack = null; _introBanner = null; _introFlash = null;
        GetNode<Control>("Hud").Modulate = Colors.White;
        _enemyView.Ship.Visible = true;
        _enemyView.Ship.Scale = Vector3.One;
        _enemyView.SetShake(Vector2.Zero);
        FrameBoth();
        Synchronize();
    }

    /// <summary>Any key or click during the arrival skips it and is not passed on to battle controls.</summary>
    private bool HandleIntroInput(InputEvent @event)
    {
        if (!IntroPlaying || @event is not (InputEventKey { Pressed: true, Echo: false } or InputEventMouseButton { Pressed: true })) { return false; }
        EndIntro();
        GetViewport().SetInputAsHandled();
        return true;
    }
}
