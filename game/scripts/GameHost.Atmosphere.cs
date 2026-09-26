using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

/// <summary>
/// Presentation-only look: a silhouette outline on characters so dark armour reads against the deck
/// (warm for hostiles, cool for crew, fading when an actor falls), a soft screen vignette and the pause frame.
/// Nothing here reads input or changes rules.
/// </summary>
public partial class GameHost
{
    private static readonly Color HostileOutline = new("ff5a3a", .78f);
    private static readonly Color CrewOutline = new("8fd8ff", .28f);
    private const double OutlineFadeTicks = 18;
    private static Shader? _outlineShader;
    private readonly Dictionary<string, ShaderMaterial> _outlineMaterials = new(StringComparer.Ordinal);

    private void CreateCharacterOutlines()
    {
        foreach (var (id, view) in _enemyViews)
        {
            if (view.Root.GetNodeOrNull<Node3D>("Presentation") is { } presentation)
            { _outlineMaterials[id.Value] = ApplyOutline(presentation, HostileOutline, 1.7f, includeWeapon: true); }
        }
        foreach (var (id, view) in _actorViews)
        {
            if (view.GetChildren().OfType<Node3D>().FirstOrDefault(child => child.Name.ToString().EndsWith("Presentation", StringComparison.Ordinal)) is { } presentation)
            { _outlineMaterials[id] = ApplyOutline(presentation, CrewOutline, 1.15f, includeWeapon: false); }
        }
    }

    private static ShaderMaterial ApplyOutline(Node3D presentation, Color color, float widthPixels, bool includeWeapon)
    {
        _outlineShader ??= ResourceLoader.Load<Shader>("res://shaders/actor_outline.gdshader");
        var material = new ShaderMaterial { Shader = _outlineShader };
        material.SetShaderParameter("outline_color", color);
        material.SetShaderParameter("width_pixels", widthPixels);
        // Crew weapons stay unlined: the hostile outline marks a ranged threat, the crew outline only lifts the body.
        var weapon = presentation.GetNodeOrNull("Weapon");
        foreach (var mesh in EnumerateDescendants(presentation).OfType<MeshInstance3D>()
            .Where(mesh => includeWeapon || weapon is null || !weapon.IsAncestorOf(mesh)))
        { mesh.MaterialOverlay = material; }
        return material;
    }

    /// <summary>Full strength while standing; fades over a short presentation window after the defeat tick.</summary>
    private void SetOutlineStrength(string id, CombatantStateObservation? combat)
    {
        if (!_outlineMaterials.TryGetValue(id, out var material)) { return; }
        var strength = 1.0;
        if (combat?.IsDefeated == true)
        {
            strength = combat.DefeatedAtTick is { } tick ? 1 - Math.Clamp((_presentationTick - tick) / OutlineFadeTicks, 0, 1) : 0;
        }
        material.SetShaderParameter("strength", (float)strength);
    }

    private TacticalPauseFrame _pauseFrame = null!;

    /// <summary>Screen treatments under the HUD: the constant vignette and the amber tactical-pause frame.</summary>
    private void CreateVignette()
    {
        var layer = new CanvasLayer { Name = "Vignette", Layer = -1 };
        AddChild(layer);
        var rect = new ColorRect { Name = "Falloff", AnchorRight = 1, AnchorBottom = 1, MouseFilter = Control.MouseFilterEnum.Ignore,
            Material = new ShaderMaterial { Shader = ResourceLoader.Load<Shader>("res://shaders/screen_vignette.gdshader") } };
        layer.AddChild(rect);
        _pauseFrame = new TacticalPauseFrame { Name = "PauseFrame" };
        layer.AddChild(_pauseFrame);
    }
}
