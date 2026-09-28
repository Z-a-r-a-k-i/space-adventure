using Godot;

namespace SpaceAdventure.Game;

/// <summary>
/// Which device the player is using right now, shared by the station and the ship battle. Any controller button,
/// or a stick or trigger pushed past its dead zone, switches to the controller: the mouse cursor hides and prompts
/// show Xbox glyphs. Any key, click or deliberate mouse movement switches back. Presentation state only; both
/// devices submit the same typed commands.
/// </summary>
public static class InputDevice
{
    private const float StickSwitchThreshold = .5f;
    private const float TriggerSwitchThreshold = .35f;
    // Hand tremor and sensor noise stay below this much pointer travel within the window.
    private const float MouseSwitchPixels = 8;
    private const ulong MouseSwitchWindowMs = 200;
    private static Window? _window;
    private static bool _watchingConnections;
    private static bool _disconnected;
    private static float _mouseTravel;
    private static ulong _mouseTravelStartedMs;

    public static bool UsingGamepad { get; private set; }

    /// <summary>The controller that sent the latest controller input.</summary>
    public static int Device { get; private set; }

    /// <summary>Changes on every switch, so hosts can refresh their prompts once per switch.</summary>
    public static int Generation { get; private set; }

    /// <summary>Observes every event the root window receives, before any node can consume it.</summary>
    public static void Attach(Node node)
    {
        var window = node.GetTree().Root;
        if (_window != window)
        {
            if (_window is not null && GodotObject.IsInstanceValid(_window)) { _window.WindowInput -= Observe; }
            _window = window;
            window.WindowInput += Observe;
        }
        if (!_watchingConnections)
        {
            _watchingConnections = true;
            Input.JoyConnectionChanged += OnJoyConnectionChanged;
        }
    }

    /// <summary>True once after the active controller disconnected; the host pauses live play.</summary>
    public static bool ConsumeDisconnect()
    {
        var disconnected = _disconnected;
        _disconnected = false;
        return disconnected;
    }

    public static void Observe(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventJoypadButton { Pressed: true } button:
                Use(true, button.Device);
                break;
            case InputEventJoypadMotion motion when Math.Abs(motion.AxisValue)
                >= (motion.Axis is JoyAxis.TriggerLeft or JoyAxis.TriggerRight ? TriggerSwitchThreshold : StickSwitchThreshold):
                Use(true, motion.Device);
                break;
            case InputEventKey { Pressed: true }:
            case InputEventMouseButton { Pressed: true }:
                Use(false, Device);
                break;
            case InputEventMouseMotion motion when UsingGamepad:
                var now = Time.GetTicksMsec();
                if (now - _mouseTravelStartedMs > MouseSwitchWindowMs) { _mouseTravel = 0; _mouseTravelStartedMs = now; }
                _mouseTravel += motion.Relative.Length();
                if (_mouseTravel >= MouseSwitchPixels) { Use(false, Device); }
                break;
        }
    }

    private static void OnJoyConnectionChanged(long device, bool connected)
    {
        if (connected || device != Device || !UsingGamepad) { return; }
        _disconnected = true;
        Use(false, Device);
    }

    private static void Use(bool gamepad, int device)
    {
        Device = device;
        if (UsingGamepad == gamepad) { return; }
        UsingGamepad = gamepad;
        Generation++;
        _mouseTravel = 0;
        if (DisplayServer.GetName() != "headless")
        { Input.MouseMode = gamepad ? Input.MouseModeEnum.Hidden : Input.MouseModeEnum.Visible; }
    }
}
