using System.Globalization;
using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

/// <summary>Shared drawing helpers for the ship HUD widgets (points and pips, never percentages for hull or shields).</summary>
internal static class ShipHudDraw
{
    public static readonly Color Panel = new("0b151c", .9f);
    public static readonly Color PanelLine = new("2c3f4b");

    public static void Card(CanvasItem item, Rect2 rect, Color accent, bool emphasised)
    {
        item.DrawRect(rect, Panel);
        item.DrawRect(rect, emphasised ? accent : PanelLine, false, emphasised ? 2 : 1);
        item.DrawRect(new Rect2(rect.Position, new Vector2(3, rect.Size.Y)), accent);
    }

    public static void Text(CanvasItem item, Font font, Vector2 position, string text, int size, Color color, float width = -1,
        HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        item.DrawString(font, position + new Vector2(0, size), text, alignment, width, size, color);
    }

    public static void Icon(CanvasItem item, string name, Rect2 rect, Color color)
    {
        if (TacticalUi.Icon(name) is { } texture) { item.DrawTextureRect(texture, rect, false, color); }
    }

    public static string Seconds(int ticks) => (ticks / (double)ShipCombatSession.TicksPerSecond).ToString("0.0", CultureInfo.InvariantCulture) + "s";

    public static string Human(string id) => id.Replace('_', ' ').ToUpperInvariant();
}

/// <summary>FTL-style hull bar: one segment per hull point, green to amber to red as it empties.</summary>
public sealed partial class HullBar : Control
{
    public int Value { get; set; }
    public int Maximum { get; set; } = 1;
    public bool Hostile { get; set; }

    public HullBar() { MouseFilter = MouseFilterEnum.Ignore; CustomMinimumSize = new Vector2(180, 14); }

    public override void _Draw()
    {
        var fraction = Value / (float)Math.Max(1, Maximum);
        var fill = Hostile ? TacticalUi.Hostile : fraction > .5f ? TacticalUi.Power : fraction > .25f ? TacticalUi.Amber : TacticalUi.Damaged;
        var gap = Maximum > 24 ? 1f : 2f;
        var width = (Size.X - gap * (Maximum - 1)) / Math.Max(1, Maximum);
        for (var index = 0; index < Maximum; index++)
        {
            var rect = new Rect2(index * (width + gap), 0, width, Size.Y);
            DrawRect(rect, index < Value ? fill : new Color("1a262e"));
        }
    }
}

/// <summary>One pip per shield layer the generator can hold, a sliver for the next layer's recharge, and outlines for layers lost to damage.</summary>
public sealed partial class ShieldPips : Control
{
    public int Layers { get; set; }
    public int Capacity { get; set; }
    public int MaximumCapacity { get; set; } = 1;
    public float Recharge { get; set; }
    public int Damaged { get; set; }

    public ShieldPips() { MouseFilter = MouseFilterEnum.Ignore; CustomMinimumSize = new Vector2(120, 22); }

    public override void _Draw()
    {
        var size = Size.Y;
        for (var index = 0; index < MaximumCapacity; index++)
        {
            var centre = new Vector2(size / 2 + index * (size + 6), size / 2);
            var points = Hexagon(centre, size / 2 - 1);
            if (index < Layers)
            {
                DrawColoredPolygon(points, TacticalUi.Shield);
                DrawPolyline([.. points, points[0]], Colors.White with { A = .6f }, 1, true);
            }
            else if (index < Capacity)
            {
                DrawColoredPolygon(points, new Color(TacticalUi.Shield, .12f));
                DrawPolyline([.. points, points[0]], TacticalUi.Shield, 1.5f, true);
                if (index == Layers && Recharge > 0)
                {
                    // Recharge sliver fills the next hexagon from the bottom up.
                    var height = (size - 4) * Recharge;
                    DrawRect(new Rect2(centre.X - size / 4, centre.Y + size / 2 - 2 - height, size / 2, height), new Color(TacticalUi.Shield, .7f));
                }
            }
            else if (index >= MaximumCapacity - Damaged)
            {
                // Generator damage removes layers; unpowered layers are only dim.
                DrawPolyline([.. points, points[0]], new Color(TacticalUi.Damaged, .8f), 1.5f, true);
                DrawLine(points[1], points[4], new Color(TacticalUi.Damaged, .8f), 1.5f, true);
            }
            else
            {
                DrawPolyline([.. points, points[0]], new Color(TacticalUi.Muted, .35f), 1.2f, true);
            }
        }
    }

    private static Vector2[] Hexagon(Vector2 centre, float radius) =>
        Enumerable.Range(0, 6).Select(index => centre + Vector2.FromAngle(Mathf.Pi / 6 + index * Mathf.Pi / 3) * radius).ToArray();
}

/// <summary>
/// Reactor column plus one pip column per player system. Left click adds a bar, right click removes one (FTL).
/// The widget only raises requests; the host sends the atomic power vector command.
/// </summary>
public sealed partial class PowerPanel : Control
{
    private const float Column = 36;
    private const float Pip = 12;
    private const float IconSize = 30;
    private IReadOnlyList<ShipSystemObservation> _systems = [];
    private int _reactor;

    public PowerPanel()
    {
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(Column * 5 + 8, 190);
    }

    public event Action<string, int>? PowerRequested;

    public string? Hovered { get; private set; }

    public static readonly IReadOnlyList<string> Order = ["shields", "engines", "life_support", "weapons"];

    public void Present(ShipSideObservation player)
    {
        _systems = Order.Select(id => player.Systems.Single(system => system.Id == id)).ToArray();
        _reactor = player.Reactor;
        QueueRedraw();
    }

    public Rect2 ColumnRect(string system)
    {
        var index = Order.ToList().IndexOf(system);
        return new Rect2(Column * (index + 1) + 8, 0, Column - 4, Size.Y);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseMotion motion)
        {
            var hovered = Order.FirstOrDefault(id => ColumnRect(id).HasPoint(motion.Position));
            if (hovered != Hovered) { Hovered = hovered; QueueRedraw(); }
            return;
        }
        if (@event is not InputEventMouseButton { Pressed: true } click || click.ButtonIndex is not (MouseButton.Left or MouseButton.Right)) { return; }
        if (Order.FirstOrDefault(id => ColumnRect(id).HasPoint(click.Position)) is not { } system) { return; }
        PowerRequested?.Invoke(system, click.ButtonIndex == MouseButton.Left ? 1 : -1);
        AcceptEvent();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseExit && Hovered is not null) { Hovered = null; QueueRedraw(); }
    }

    public override void _Draw()
    {
        if (_systems.Count == 0) { return; }
        var font = GetThemeDefaultFont();
        var bottom = Size.Y - IconSize - 4;
        var allocated = _systems.Sum(system => system.AllocatedPower);
        // Reactor: free power as a green stack, allocated as dim.
        for (var index = 0; index < _reactor; index++)
        {
            var rect = new Rect2(4, bottom - (index + 1) * (Pip + 3), Column - 12, Pip);
            DrawRect(rect, index < _reactor - allocated ? TacticalUi.Power : new Color(TacticalUi.PowerOff, .9f));
        }
        ShipHudDraw.Icon(this, "ship/reactor", new Rect2(new Vector2(4 + (Column - 12 - 22) / 2, bottom + 6), new Vector2(22, 22)), TacticalUi.Amber);
        foreach (var system in _systems)
        {
            var column = ColumnRect(system.Id);
            var hover = Hovered == system.Id;
            for (var index = 0; index < system.MaxPower; index++)
            {
                var rect = new Rect2(column.Position.X + 4, bottom - (index + 1) * (Pip + 3), column.Size.X - 8, Pip);
                var broken = index >= system.MaxPower - system.Damage;
                var color = broken ? TacticalUi.Damaged : index < system.EffectivePower ? TacticalUi.Power : new Color(TacticalUi.PowerOff, .95f);
                DrawRect(rect, color);
                if (broken && index < system.AllocatedPower) { DrawRect(rect, Colors.White with { A = .7f }, false, 1); }
            }
            var iconRect = new Rect2(column.Position.X + (column.Size.X - IconSize) / 2, bottom + 4, IconSize, IconSize);
            var tint = system.Damage >= system.MaxPower ? TacticalUi.Damaged : system.Damage > 0 ? TacticalUi.Amber
                : system.EffectivePower > 0 ? TacticalUi.Power : TacticalUi.Muted;
            DrawRect(iconRect, hover ? new Color("1c3440") : new Color("0e1a21"));
            DrawRect(iconRect, hover ? TacticalUi.Cyan : new Color(tint, .6f), false, 1);
            ShipHudDraw.Icon(this, $"ship/{system.Id}", iconRect.Grow(-4), tint);
            if (system.Manned)
            {
                ShipHudDraw.Icon(this, "ship/man", new Rect2(column.Position.X + column.Size.X - 13, bottom - system.MaxPower * (Pip + 3) - 16, 12, 12), TacticalUi.Cyan);
            }
        }
        ShipHudDraw.Text(this, font, new Vector2(0, 0), "POWER", 11, TacticalUi.Muted);
    }
}

/// <summary>One mounted player weapon: power pips, charge bar, ammo and target. Left click aims, right click toggles power.</summary>
public sealed partial class WeaponCard : Control
{
    private ShipWeaponObservation? _weapon;
    private bool _targeting;
    private bool _holding;
    private bool _hover;
    private float _pulse;

    public WeaponCard(int number)
    {
        Number = number;
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(196, 70);
    }

    public int Number { get; }
    public string? WeaponId => _weapon?.Id;
    public event Action<string>? AimRequested;
    public event Action<string>? PowerToggleRequested;

    public void Present(ShipWeaponObservation weapon, bool targeting, bool holding, float pulse)
    {
        _weapon = weapon;
        _targeting = targeting;
        _holding = holding;
        _pulse = pulse;
        TooltipText = $"{weapon.DisplayName}: left click, then click an enemy room to aim. Right click toggles its power.";
        QueueRedraw();
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_weapon is null || @event is not InputEventMouseButton { Pressed: true } click) { return; }
        if (click.ButtonIndex == MouseButton.Left) { AimRequested?.Invoke(_weapon.Id); AcceptEvent(); }
        else if (click.ButtonIndex == MouseButton.Right) { PowerToggleRequested?.Invoke(_weapon.Id); AcceptEvent(); }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationMouseEnter) { _hover = true; QueueRedraw(); }
        else if (what == NotificationMouseExit) { _hover = false; QueueRedraw(); }
    }

    public override void _Draw()
    {
        if (_weapon is not { } weapon) { return; }
        var font = GetThemeDefaultFont();
        var bold = TacticalUi.BoldFont;
        var rect = new Rect2(Vector2.Zero, Size);
        var kind = weapon.Kind == ShipWeaponKind.Missile ? TacticalUi.Amber : TacticalUi.Cyan;
        ShipHudDraw.Card(this, rect, _targeting ? Colors.White : kind, _targeting || _hover);
        // Hotkey, icon and name.
        DrawRect(new Rect2(8, 7, 16, 16), new Color("1a2c35"));
        ShipHudDraw.Text(this, bold, new Vector2(8, 6), Number.ToString(CultureInfo.InvariantCulture), 12, TacticalUi.Muted, 16, HorizontalAlignment.Center);
        ShipHudDraw.Icon(this, weapon.Kind == ShipWeaponKind.Missile ? "ship/missile" : "ship/laser", new Rect2(29, 5, 20, 20), kind);
        ShipHudDraw.Text(this, bold, new Vector2(53, 5), weapon.DisplayName.ToUpperInvariant(), 13, weapon.Powered ? Colors.White : TacticalUi.Muted, 100);
        // Power cost pips on the right.
        for (var index = 0; index < weapon.PowerCost; index++)
        {
            var pip = new Rect2(Size.X - 12 - index * 11, 9, 8, 12);
            DrawRect(pip, weapon.Powered ? TacticalUi.Power : new Color(TacticalUi.PowerOff, .95f));
            if (!weapon.Armed) { DrawRect(pip, TacticalUi.Muted, false, 1); }
        }
        // Charge bar.
        var bar = new Rect2(8, 30, Size.X - 16, 9);
        var ready = weapon.ChargePermille >= 1000;
        var fill = !weapon.Powered ? TacticalUi.Muted : ready ? (_holding ? TacticalUi.Amber : Colors.White.Lerp(kind, .35f + .35f * _pulse)) : kind;
        DrawRect(bar, new Color("15222a"));
        DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * weapon.ChargePermille / 1000f, bar.Size.Y)), fill);
        for (var tick = 1; tick < 4; tick++) { DrawLine(new Vector2(bar.Position.X + bar.Size.X * tick / 4, bar.Position.Y), new Vector2(bar.Position.X + bar.Size.X * tick / 4, bar.End.Y), new Color(0, 0, 0, .5f)); }
        // Status and target.
        var status = !weapon.Armed ? "OFF" : !weapon.Powered ? "NO POWER" : weapon.Ammo == 0 ? "NO AMMO" : ready ? (_holding ? "HELD" : weapon.Target is null ? "READY" : "FIRING")
            : ShipHudDraw.Seconds(weapon.TicksToFire);
        ShipHudDraw.Text(this, font, new Vector2(8, 44), status, 11, !weapon.Powered || weapon.Ammo == 0 ? TacticalUi.Damaged : ready ? Colors.White : TacticalUi.Muted, 70);
        var targetText = _targeting ? "PICK A ROOM" : weapon.Target is null ? "NO TARGET" : ShipHudDraw.Human(weapon.Target);
        ShipHudDraw.Icon(this, "ship/target", new Rect2(78, 45, 14, 14), weapon.Target is null ? TacticalUi.Muted : TacticalUi.Hostile);
        ShipHudDraw.Text(this, font, new Vector2(95, 44), targetText, 11, weapon.Target is null ? TacticalUi.Muted : TacticalUi.Hostile, 70);
        if (weapon.Ammo >= 0)
        {
            ShipHudDraw.Text(this, bold, new Vector2(Size.X - 40, 44), $"x{weapon.Ammo}", 12, weapon.Ammo == 0 ? TacticalUi.Damaged : TacticalUi.Amber, 32, HorizontalAlignment.Right);
        }
    }
}

/// <summary>Telegraphed enemy weapon: charge, next target room, payload and countdown.</summary>
public sealed partial class EnemyWeaponRow : Control
{
    private ShipEnemyIntent? _intent;
    private int _charge;

    public EnemyWeaponRow() { MouseFilter = MouseFilterEnum.Ignore; CustomMinimumSize = new Vector2(176, 52); }

    public void Present(ShipEnemyIntent intent, int chargePermille)
    {
        _intent = intent;
        _charge = chargePermille;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_intent is not { } intent) { return; }
        var font = GetThemeDefaultFont();
        var bold = TacticalUi.BoldFont;
        ShipHudDraw.Card(this, new Rect2(Vector2.Zero, Size), TacticalUi.Hostile, false);
        ShipHudDraw.Icon(this, intent.Kind == ShipWeaponKind.Missile ? "ship/missile" : "ship/laser", new Rect2(8, 5, 18, 18), TacticalUi.Hostile);
        ShipHudDraw.Text(this, bold, new Vector2(30, 4), intent.DisplayName.ToUpperInvariant(), 12, intent.Powered ? Colors.White : TacticalUi.Muted, 100);
        if (intent.Ammo >= 0) { ShipHudDraw.Text(this, bold, new Vector2(Size.X - 38, 4), $"x{intent.Ammo}", 11, TacticalUi.Amber, 30, HorizontalAlignment.Right); }
        var bar = new Rect2(8, 25, Size.X - 16, 6);
        DrawRect(bar, new Color("24161a"));
        DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * _charge / 1000f, bar.Size.Y)), intent.Powered ? TacticalUi.Hostile : TacticalUi.Muted);
        var payload = intent.Payload switch { ShipPayload.Incendiary => " FIRE", ShipPayload.Breach => " BREACH", _ => "" };
        var line = !intent.Powered ? "OFFLINE" : intent.TicksToFire < 0 ? "NO AMMO" : $"{ShipHudDraw.Human(intent.TargetSystem)}{payload}  {ShipHudDraw.Seconds(intent.TicksToFire)}";
        ShipHudDraw.Text(this, font, new Vector2(8, 34), line, 11, intent.Powered ? new Color("ffc2ad") : TacticalUi.Muted, Size.X - 16);
    }
}

/// <summary>Compact crew card: portrait, health bar and current activity. Clicking selects (Shift adds).</summary>
public sealed partial class ShipCrewCard : Button
{
    private readonly Texture2D? _portrait;
    private ShipCrewObservation? _crew;
    private bool _selected;

    public ShipCrewCard(string crewId, Texture2D? portrait, Color accent, int number)
    {
        CrewId = crewId;
        _portrait = portrait;
        Accent = accent;
        Number = number;
        FocusMode = FocusModeEnum.None;
        CustomMinimumSize = new Vector2(176, 56);
        Flat = true;
    }

    public string CrewId { get; }
    public Color Accent { get; }
    public int Number { get; }

    public void Present(ShipCrewObservation crew, bool selected)
    {
        _crew = crew;
        _selected = selected;
        TooltipText = $"{crew.DisplayName} (F{Number}): click to select, Shift+click to add. Right-click a room to move.";
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (_crew is not { } crew) { return; }
        var font = GetThemeDefaultFont();
        var bold = TacticalUi.BoldFont;
        var accent = crew.Downed ? TacticalUi.Damaged : Accent;
        ShipHudDraw.Card(this, new Rect2(Vector2.Zero, Size), accent, _selected);
        if (_selected) { DrawRect(new Rect2(Vector2.Zero, Size), new Color(accent, .08f)); }
        if (_portrait is not null)
        {
            DrawTextureRect(_portrait, new Rect2(7, 6, 44, 44), false, crew.Downed ? new Color(1, .4f, .4f, .6f) : Colors.White);
        }
        ShipHudDraw.Text(this, bold, new Vector2(57, 4), crew.DisplayName.ToUpperInvariant(), 13, Colors.White, 90);
        ShipHudDraw.Text(this, bold, new Vector2(Size.X - 28, 5), $"F{Number}", 10, TacticalUi.Muted, 22, HorizontalAlignment.Right);
        var fraction = crew.Health / (float)Math.Max(1, crew.MaxHealth);
        var bar = new Rect2(57, 23, Size.X - 66, 6);
        DrawRect(bar, new Color("1a262e"));
        DrawRect(new Rect2(bar.Position, new Vector2(bar.Size.X * fraction, bar.Size.Y)),
            fraction > .5f ? TacticalUi.Power : fraction > .25f ? TacticalUi.Amber : TacticalUi.Damaged);
        // The icon names the activity; the text names the room (or destination) so it never truncates.
        var (icon, text) = crew.Downed ? ("ship/down", "DOWN")
            : crew.Moving ? ("ship/door", $"> {ShipHudDraw.Human(crew.Destination ?? "")}")
            : crew.CurrentWork switch
            {
                ShipTaskKind.Man => ("ship/man", ShipHudDraw.Human(crew.Room)),
                ShipTaskKind.Repair => ("ship/repair", ShipHudDraw.Human(crew.Room)),
                ShipTaskKind.Extinguish => ("ship/fire", ShipHudDraw.Human(crew.Room)),
                ShipTaskKind.Seal => ("ship/breach", ShipHudDraw.Human(crew.Room)),
                ShipTaskKind.Treat => ("ship/medic", ShipHudDraw.Human(crew.Room)),
                _ => ("ship/pause", ShipHudDraw.Human(crew.Room)),
            };
        ShipHudDraw.Icon(this, icon, new Rect2(57, 33, 14, 14), crew.InDanger ? TacticalUi.Amber : TacticalUi.Muted);
        ShipHudDraw.Text(this, font, new Vector2(75, 32), text, 11, crew.InDanger ? TacticalUi.Amber : TacticalUi.Muted, Size.X - 82);
    }
}
