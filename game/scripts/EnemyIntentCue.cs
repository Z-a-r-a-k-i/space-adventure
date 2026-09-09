using Godot;

namespace SpaceAdventure.Game;

/// <summary>Wind-up source, destination, and final-quarter urgency sampled from observed phases.</summary>
public partial class EnemyIntentCue : Node3D
{
    private readonly List<MeshInstance3D> _dashes = [];
    private readonly List<MeshInstance3D> _ticks = [];
    private MeshInstance3D _targetRing = null!;
    private MeshInstance3D _arrow = null!;
    private StandardMaterial3D _material = null!;
    private bool _ranged;
    private float _progress;

    public void Build(bool ranged)
    {
        _ranged = ranged;
        _material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color("ef9a79"), EmissionEnabled = true,
            Emission = new Color("ef9a79"), EmissionEnergyMultiplier = .6f,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        var dashMesh = new BoxMesh { Size = new Vector3(.033f, .015f, 1) };
        for (var i = 0; i < 7; i++) { _dashes.Add(AddMesh(dashMesh)); }
        _targetRing = AddMesh(new TorusMesh
        { InnerRadius = .49f, OuterRadius = .525f, Rings = ranged ? 4 : 32, RingSegments = 4 });
        var arrow = new SurfaceTool(); arrow.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var point in new[] { new Vector3(0, 0, -.2f), new Vector3(-.11f, 0, .1f), new Vector3(.11f, 0, .1f) })
        { arrow.AddVertex(point); }
        _arrow = AddMesh(arrow.Commit());
        var tickMesh = new BoxMesh { Size = new Vector3(.06f, .015f, .15f) };
        for (var i = 0; i < 4; i++) { _ticks.Add(AddMesh(tickMesh)); }
        Visible = false;
    }

    public void Sample(bool visible, Vector3 source, Vector3 target, float progress)
    {
        Visible = visible;
        _progress = progress;
        if (!visible) { return; }
        source.Y += .075f; target.Y += .085f;
        var direction = target - source; direction.Y = 0;
        var distance = direction.Length();
        if (distance < .05f) { Visible = false; return; }
        direction /= distance;
        var yaw = Mathf.Atan2(-direction.X, -direction.Z);
        var imminent = progress >= .75f;
        _material.AlbedoColor = new Color(imminent ? new Color("ffe0bf") : new Color("ef9a79"), imminent ? .85f : .5f);
        _material.EmissionEnergyMultiplier = imminent ? 1.05f : .5f;
        for (var index = 0; index < _dashes.Count; index++)
        {
            var dash = _dashes[index];
            var fraction = (index + .5f) / _dashes.Count;
            dash.GlobalPosition = source.Lerp(target, fraction);
            dash.Rotation = new Vector3(0, yaw, 0);
            dash.Scale = new Vector3(1, 1, distance / _dashes.Count * .48f);
        }
        _arrow.GlobalPosition = target - direction * .58f;
        _arrow.Rotation = new Vector3(0, yaw, 0);
        _arrow.Scale = Vector3.One * (imminent ? 1.3f : 1);
        _targetRing.GlobalPosition = target;
        _targetRing.Scale = Vector3.One * (1.22f - progress * .22f);
        _targetRing.Rotation = new Vector3(0, _ranged ? Mathf.Pi / 4 : 0, 0);
        for (var index = 0; index < _ticks.Count; index++)
        {
            var tick = _ticks[index]; tick.Visible = imminent;
            var angle = index * Mathf.Pi / 2;
            tick.GlobalPosition = target + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * .67f;
            tick.Rotation = new Vector3(0, angle, 0);
        }
    }

    public object GetDiagnostics() => new
    {
        visible = Visible, ranged = _ranged, progress = _progress, imminent = Visible && _progress >= .75f,
        target = new[] { _targetRing.Position.X, _targetRing.Position.Y, _targetRing.Position.Z },
    };

    private MeshInstance3D AddMesh(Mesh mesh)
    {
        var view = new MeshInstance3D
        { Mesh = mesh, MaterialOverride = _material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(view); return view;
    }
}
