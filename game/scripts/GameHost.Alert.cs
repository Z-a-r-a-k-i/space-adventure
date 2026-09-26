using Godot;
using SpaceAdventure.Core;

namespace SpaceAdventure.Game;

/// <summary>
/// Seamless encounter presentation: hostiles fade in when crew sight reveals them instead of popping, and the
/// hostile that noticed the crew gets a short pulse and sight line to the crew member it saw. Both run on real
/// time so they still read while the game auto-pauses; neither affects rules or picking.
/// </summary>
public partial class GameHost
{
    private const float RevealFadeSeconds = .35f;
    private const float SpottedCueSeconds = 1.6f;
    private static readonly Color SpottedColor = new("ff5a3a");
    private readonly Dictionary<EntityId, (ulong StartedMs, GeometryInstance3D[] Meshes)> _reveals = [];
    private Node3D? _spottedCue;
    private MeshInstance3D? _spottedRing;
    private MeshInstance3D? _spottedLine;
    private StandardMaterial3D? _spottedMaterial;
    private ulong _spottedCueStartedMs;

    // Captures sample exact poses; a half-faded enemy there would only be noise.
    private bool RevealFades => _reviewMode != "capture";

    private void BeginHostileReveal(EntityId id, EnemyView view)
    {
        if (!RevealFades) { return; }
        var meshes = view.Root.GetNodeOrNull<Node3D>("Presentation") is { } presentation
            ? EnumerateDescendants(presentation).OfType<GeometryInstance3D>().ToArray() : [];
        _reveals[id] = (Time.GetTicksMsec(), meshes);
        foreach (var mesh in meshes) { mesh.Transparency = 1; }
    }

    private void EndHostileReveal(EntityId id)
    {
        if (!_reveals.Remove(id, out var reveal)) { return; }
        foreach (var mesh in reveal.Meshes) { mesh.Transparency = 0; }
    }

    /// <summary>0 while a newly revealed hostile is still invisible, rising to 1 once the fade completes.</summary>
    private float RevealAlpha(EntityId id)
    {
        if (!_reveals.TryGetValue(id, out var reveal)) { return 1; }
        var alpha = Math.Clamp((Time.GetTicksMsec() - reveal.StartedMs) / 1000f / RevealFadeSeconds, 0, 1);
        foreach (var mesh in reveal.Meshes) { mesh.Transparency = 1 - alpha; }
        if (alpha >= 1) { _reveals.Remove(id); }
        return alpha;
    }

    private void ShowSpottedCue(Vector3 spotter, Vector3? spotted)
    {
        if (_spottedCue is null)
        {
            _spottedMaterial = new StandardMaterial3D
            {
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = SpottedColor,
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
                NoDepthTest = true, RenderPriority = 2,
            };
            _spottedCue = new Node3D { Name = "SpottedCue" };
            _spottedRing = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = .9f, OuterRadius = 1, Rings = 48, RingSegments = 4 },
                MaterialOverride = _spottedMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _spottedLine = new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = .025f, BottomRadius = .025f, Height = 1, RadialSegments = 6 },
                MaterialOverride = _spottedMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
            _spottedCue.AddChild(_spottedRing);
            _spottedCue.AddChild(_spottedLine);
            AddChild(_spottedCue);
        }
        _spottedCueStartedMs = Time.GetTicksMsec();
        _spottedCue.Visible = true;
        _spottedRing!.GlobalPosition = spotter + Vector3.Up * .06f;
        _spottedLine!.Visible = spotted is not null;
        if (spotted is { } target)
        {
            var from = spotter + Vector3.Up * 1.45f;
            var to = target + Vector3.Up * 1.45f;
            var direction = to - from;
            if (direction.LengthSquared() < .01f) { _spottedLine.Visible = false; }
            else
            {
                _spottedLine.GlobalTransform = new Transform3D(new Basis(new Quaternion(Vector3.Up, direction.Normalized()))
                    * Basis.FromScale(new Vector3(1, direction.Length(), 1)), (from + to) / 2);
            }
        }
        GameAudio.Play("ui.target", volumeDb: 2);
    }

    private void AdvanceSpottedCue(StationRouteObservation route)
    {
        if (_spottedCue is not { Visible: true }) { return; }
        // The cue marks the moment of contact only; it never outlives the opening pause.
        if (route.Encounter?.Phase != EncounterPhase.Readying) { _spottedCue.Visible = false; return; }
        var progress = Math.Clamp((Time.GetTicksMsec() - _spottedCueStartedMs) / 1000f / SpottedCueSeconds, 0, 1);
        var size = .45f + progress * .9f;
        _spottedRing!.Scale = new Vector3(size, .05f, size);
        _spottedMaterial!.AlbedoColor = new Color(SpottedColor, (1 - progress) * (1 - progress) * .9f);
        if (progress >= 1) { _spottedCue.Visible = false; }
    }
}
