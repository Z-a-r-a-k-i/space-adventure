using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

/// <summary>
/// Ship input review with injected controller events (<c>review -Encounter ship -Mode input -Sequence gamepad</c>):
/// crew cycling, room hopping, orders, weapon aim and power, the power bar, triggers, doors, zoom, the Menu panel
/// and switching back to the mouse. Injected events are separate evidence from a physical controller.
/// </summary>
public partial class ShipBattleHost
{
    private static void PadEvent(JoyButton button, bool pressed) =>
        Input.ParseInputEvent(new InputEventJoypadButton { Device = 0, ButtonIndex = button, Pressed = pressed });

    private static void PadAxisEvent(JoyAxis axis, float value) =>
        Input.ParseInputEvent(new InputEventJoypadMotion { Device = 0, Axis = axis, AxisValue = value });

    private async Task PadTap(JoyButton button)
    {
        PadEvent(button, true);
        await WaitFrames(2);
        PadEvent(button, false);
        await WaitFrames(2);
    }

    private async Task PadPull(JoyAxis trigger)
    {
        PadAxisEvent(trigger, 1);
        await WaitFrames(2);
        PadAxisEvent(trigger, 0);
        await WaitFrames(2);
    }

    private async Task PadHold(JoyButton button, double seconds)
    {
        PadEvent(button, true);
        var timer = System.Diagnostics.Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < seconds) { await WaitFrames(1); }
        PadEvent(button, false);
        await WaitFrames(2);
    }

    /// <summary>Pushes the left stick from the highlighted room toward <paramref name="target"/> until it gets there.</summary>
    private async Task PadHopTo(string target, bool enemy)
    {
        for (var attempt = 0; attempt < 6 && (enemy ? _padEnemyRoom : _padRoom) != target; attempt++)
        {
            Vector2 Centre(string id) => enemy ? EnemyRoomRect(id).GetCenter() : PlayerRoomCentre(id);
            var direction = (Centre(target) - Centre((enemy ? _padEnemyRoom : _padRoom)!)).Normalized();
            PadAxisEvent(JoyAxis.LeftX, direction.X);
            PadAxisEvent(JoyAxis.LeftY, direction.Y);
            await WaitFrames(2);
            PadAxisEvent(JoyAxis.LeftX, 0);
            PadAxisEvent(JoyAxis.LeftY, 0);
            await WaitFrames(2);
        }
    }

    private async Task RunGamepadReviewAsync()
    {
        await WaitFrames(20);
        Check("keyboard and mouse is the starting device", !InputDevice.UsingGamepad);
        await PadTap(JoyButton.RightShoulder);
        var first = _session.Observe().Crew[0];
        Check("a controller button switches to controller mode", InputDevice.UsingGamepad);
        Check("RB selects the first crew member and highlights their room", Selected.Count == 1 && Selected[0] == first.Id && _padRoom == first.Room);
        Check("battle buttons show controller glyphs", _pauseButton.Icon == InputPrompts.Glyph(PadButton.RT) && _pauseButton.Text == "Resume"
            && _holdButton.Icon == InputPrompts.Glyph(PadButton.LT) && _frameButton.Icon == InputPrompts.Glyph(PadButton.R3));
        await PadHopTo("engines", enemy: false);
        Check("the left stick hops the highlight to the engines", _padRoom == "engines");
        await PadTap(JoyButton.A);
        Check("A sends the selected crew to the highlighted room", _session.Observe().Crew[0].Destination == "engines");
        var files = new List<string> { await Capture("ship-gamepad-room") };

        var weapons = _definition.Player.Weapons;
        await PadTap(JoyButton.X);
        Check("tapping X aims the first weapon at an enemy room", AimingWeapon == weapons[0].Id && _padEnemyRoom is not null);
        await PadHopTo("shields", enemy: true);
        Check("the left stick hops between enemy rooms while aiming", _padEnemyRoom == "shields");
        files.Add(await Capture("ship-gamepad-aim"));
        await PadTap(JoyButton.A);
        Check("A fires the aimed weapon at the highlighted enemy room", _session.Observe().Player.Weapons[0].Target == "shields" && AimingWeapon is null);
        await PadTap(JoyButton.Y);
        Check("tapping Y aims the second weapon", AimingWeapon == weapons[1].Id);
        await PadTap(JoyButton.B);
        Check("B cancels aiming without changing the target", AimingWeapon is null && _session.Observe().Player.Weapons[1].Target is null);
        var armed = _session.Observe().Player.Weapons[0].Armed;
        await PadHold(JoyButton.X, .7);
        Check("holding X switches the first weapon's power instead of aiming", _session.Observe().Player.Weapons[0].Armed != armed && AimingWeapon is null);
        await PadHold(JoyButton.X, .7);
        Check("holding X again restores it", _session.Observe().Player.Weapons[0].Armed == armed);
        PadEvent(JoyButton.X, true);
        await WaitFrames(2);
        await PadTap(JoyButton.Start);
        PadEvent(JoyButton.X, false);
        await WaitFrames(2);
        await PadTap(JoyButton.B);
        var wait = System.Diagnostics.Stopwatch.StartNew();
        while (wait.Elapsed.TotalSeconds < .7) { await WaitFrames(1); }
        Check("a weapon tap interrupted by the Menu panel never turns into a power toggle",
            _session.Observe().Player.Weapons[0].Armed == armed && AimingWeapon is null && !_padPanel.Visible);

        int Power(string system) => _session.Observe().Player.Systems.Single(item => item.Id == system).AllocatedPower;
        await PadTap(JoyButton.DpadRight);
        Check("D-pad right moves the power bar focus to engines", _power.PadFocus == "engines");
        var engines = Power("engines");
        await PadTap(JoyButton.DpadDown);
        Check("D-pad down removes a bar from the focused system", Power("engines") == engines - 1);
        await PadTap(JoyButton.DpadUp);
        Check("D-pad up adds it back", Power("engines") == engines);

        await PadPull(JoyAxis.TriggerLeft);
        Check("LT holds the volley", _session.Observe().Player.HoldFire);
        await PadPull(JoyAxis.TriggerLeft);
        Check("LT releases it", !_session.Observe().Player.HoldFire);
        await PadPull(JoyAxis.TriggerRight);
        Check("RT resumes", !_session.Paused);
        await PadPull(JoyAxis.TriggerRight);
        Check("RT pauses", _session.Paused);

        bool DoorOpen(string id) => _session.Observe().Doors.First(door => door.Id == id).CommandedOpen;
        await PadTap(JoyButton.B);
        Check("B opens the highlighted room's doors", DoorOpen("door_engines"));
        await PadTap(JoyButton.B);
        Check("B closes them again", !DoorOpen("door_engines"));
        await PadTap(JoyButton.Back);
        Check("View selects every crew member", Selected.Count == _session.Observe().Crew.Count);

        PadAxisEvent(JoyAxis.RightY, -1);
        await WaitFrames(12);
        PadAxisEvent(JoyAxis.RightY, 0);
        await WaitFrames(2);
        Check("pushing the right stick up zooms the player view", !_playerView.IsFramedAll && _enemyView.IsFramedAll);
        await PadTap(JoyButton.RightStick);
        Check("pressing the right stick frames both ships", _playerView.IsFramedAll && _enemyView.IsFramedAll);

        await PadTap(JoyButton.Start);
        Check("Menu opens the orders panel with its first button focused", _padPanel.Visible && _padPanelFirst.HasFocus());
        CheckLayout("orders panel");
        await PadTap(JoyButton.DpadDown);
        await PadTap(JoyButton.DpadDown);
        await PadTap(JoyButton.A);
        var airlock = _definition.Doors.First(door => door.Exterior).Id;
        Check("A on the panel's airlock button vents it and closes the panel", DoorOpen(airlock) && !_padPanel.Visible);
        await PadTap(JoyButton.Start);
        Check("the panel offers to seal the venting airlock", _padAirlockButton.Text == "Seal airlock");
        await PadTap(JoyButton.DpadDown);
        await PadTap(JoyButton.DpadDown);
        await PadTap(JoyButton.A);
        Check("A seals it again", !DoorOpen(airlock));
        await PadTap(JoyButton.Start);
        await PadTap(JoyButton.B);
        Check("B closes the panel", !_padPanel.Visible);
        var pausedBefore = _session.Paused;
        await PadTap(JoyButton.Start);
        PadAxisEvent(JoyAxis.TriggerRight, 1);
        await WaitFrames(2);
        await PadTap(JoyButton.B);
        PadAxisEvent(JoyAxis.TriggerRight, .9f);
        await WaitFrames(2);
        PadAxisEvent(JoyAxis.TriggerRight, 0);
        await WaitFrames(2);
        Check("RT held through the panel does not pause once it closes", _session.Paused == pausedBefore && !_padPanel.Visible);

        var centre = GetViewportRect().GetCenter();
        Input.ParseInputEvent(new InputEventMouseMotion { Position = centre, GlobalPosition = centre, Relative = new Vector2(40, 10) });
        await WaitFrames(2);
        Check("moving the mouse switches back to keyboard and mouse prompts", !InputDevice.UsingGamepad
            && _pauseButton.Text == "Resume (Space)" && _power.PadFocus is null && _frameButton.Text == "Frame both (F)");
        Finish("ship_gamepad_review", new { files });
    }
}
