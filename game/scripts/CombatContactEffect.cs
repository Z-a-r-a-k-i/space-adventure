using Godot;

namespace SpaceAdventure.Game;

public enum CombatSignature { Carbine, Shotgun, Melee, Sentry, Block, Interrupt, Burst, Barrier, Taunt, Muzzle }

/// <summary>Localized, clock-sampled impact shapes. Geometry never supplies a hit result.</summary>
public partial class CombatContactEffect : Node3D
{
    private static readonly Dictionary<CombatSignature, (Mesh Spark, Mesh Ring, Mesh Core)> Resources = [];
    private readonly List<(MeshInstance3D View, Vector3 Direction)> _sparks = [];
    private MeshInstance3D _ring = null!;
    private MeshInstance3D _core = null!;
    private CombatSignature _signature;
    private float _radius;
    private bool _floor;
    private float _progress;
    public float DurationSeconds { get; private set; }

    public void Configure(CombatSignature signature, Vector3 position, Vector3 direction, float radius = 1)
    {
        _signature = signature; _radius = radius; Position = position;
        var floor = signature is CombatSignature.Taunt or CombatSignature.Barrier
            || signature == CombatSignature.Interrupt && direction == Vector3.Up;
        _floor = floor;
        var axis = floor ? Vector3.Up : direction.IsZeroApprox() ? Vector3.Forward : direction.Normalized();
        Basis = new Basis(new Quaternion(Vector3.Up, axis));
        DurationSeconds = signature switch
        {
            CombatSignature.Taunt => .55f, CombatSignature.Barrier => .4f,
            CombatSignature.Interrupt => .3f, CombatSignature.Muzzle => .085f,
            CombatSignature.Melee => .22f, _ => .19f,
        };
        var meshes = GetResources(signature);
        _ring = AddMesh(meshes.Ring); _core = AddMesh(meshes.Core);
        var count = signature switch
        {
            CombatSignature.Shotgun => 7, CombatSignature.Melee => 3, CombatSignature.Block => 6,
            CombatSignature.Interrupt => 4, CombatSignature.Taunt or CombatSignature.Barrier => 0, _ => 4,
        };
        for (var i = 0; i < count; i++)
        {
            var angle = i * Mathf.Tau / count + .25f;
            var ray = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            var spark = AddMesh(meshes.Spark);
            spark.Basis = new Basis(new Quaternion(Vector3.Up, ray));
            _sparks.Add((spark, ray));
        }
        Sample(0);
    }

    public void Sample(float progress)
    {
        var p = Math.Clamp(progress, 0, 1);
        _progress = p;
        var floor = _floor;
        var reach = _signature switch
        {
            CombatSignature.Shotgun => .34f, CombatSignature.Melee => .27f,
            CombatSignature.Interrupt => .42f, CombatSignature.Block => .32f,
            CombatSignature.Muzzle => .12f, _ => .22f,
        };
        _core.Visible = !floor && p < .42f;
        _core.Scale = Vector3.One * Math.Max(.01f, 1 - p * 2);
        _core.Transparency = Math.Clamp(p * 2, 0, 1);
        _ring.Visible = _signature is not CombatSignature.Muzzle;
        var size = floor ? _radius * (.15f + .85f * Mathf.Ease(p, .55f)) : reach * (.35f + p);
        _ring.Scale = new Vector3(size, 1, size);
        _ring.Transparency = p * p;
        foreach (var (view, ray) in _sparks)
        {
            view.Position = ray * reach * (.2f + p);
            view.Scale = new Vector3(1 - p * .75f, 1 + p, 1 - p * .75f);
            view.Transparency = p * p;
        }
    }

    public object GetDiagnostics() => new
    {
        signature = _signature.ToString(), progress = _progress, floor = _floor, radius_m = _radius,
        position = new[] { Position.X, Position.Y, Position.Z },
    };

    private MeshInstance3D AddMesh(Mesh mesh)
    {
        var view = new MeshInstance3D { Mesh = mesh, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddChild(view); return view;
    }

    private static (Mesh Spark, Mesh Ring, Mesh Core) GetResources(CombatSignature signature)
    {
        if (Resources.TryGetValue(signature, out var cached)) { return cached; }
        var tint = signature switch
        {
            CombatSignature.Shotgun => "f5c581", CombatSignature.Melee => "ffd2ab",
            CombatSignature.Sentry => "ff7659", CombatSignature.Block => "bbfff1",
            CombatSignature.Interrupt => "d5b8ff", CombatSignature.Burst => "9efff5",
            CombatSignature.Taunt => "f6bc72", CombatSignature.Barrier => "78dcff", _ => "86e9ff",
        };
        var material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            AlbedoColor = new Color(tint), EmissionEnabled = true, Emission = new Color(tint),
            EmissionEnergyMultiplier = 1.1f,
        };
        var spark = new CylinderMesh
        {
            TopRadius = 0, BottomRadius = signature == CombatSignature.Shotgun ? .025f : .015f,
            Height = signature == CombatSignature.Melee ? .25f : .13f, RadialSegments = 4,
            Rings = 1, Material = material,
        };
        var ring = new TorusMesh
        {
            InnerRadius = .91f, OuterRadius = 1, Rings = signature == CombatSignature.Block ? 6 : 32,
            RingSegments = 4, Material = material,
        };
        var core = new SphereMesh { Radius = .065f, Height = .13f, RadialSegments = 8, Rings = 4, Material = material };
        cached = (spark, ring, core); Resources.Add(signature, cached); return cached;
    }
}
