using Godot;

namespace SpaceAdventure.Game;

/// <summary>A release-event visual sampled by the presentation clock; never resolves a hit.</summary>
public partial class CarbineProjectile : Node3D
{
    private static readonly Dictionary<Color, ProjectileMeshes> MeshesByColor = [];
    private MeshInstance3D _trail = null!;

    public Vector3 LaunchPosition { get; private set; }
    public Vector3 TargetPosition { get; private set; }
    public float FlightSeconds { get; private set; }
    public float Progress { get; private set; }

    public void Configure(Vector3 origin, Vector3 destination, Color color)
    {
        LaunchPosition = origin;
        TargetPosition = destination;
        // Keep a few readable frames even at melee distance without changing combat timing.
        FlightSeconds = Math.Clamp(origin.DistanceTo(destination) / 28.0f, 0.14f, 0.24f) / AnimationPacing.Rate;
        var direction = origin.DirectionTo(destination);
        Transform = new Transform3D(new Basis(new Quaternion(Vector3.Up,
            direction.IsZeroApprox() ? Vector3.Forward : direction)), origin);
        var meshes = GetMeshes(color);
        AddChild(new MeshInstance3D
        {
            Mesh = meshes.Core,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        AddChild(new MeshInstance3D
        {
            Mesh = meshes.Halo,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
        _trail = new MeshInstance3D
        {
            Mesh = meshes.Trail,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        AddChild(_trail);
        Sample(0);
    }

    private static ProjectileMeshes GetMeshes(Color color)
    {
        if (MeshesByColor.TryGetValue(color, out var meshes)) { return meshes; }
        meshes = new ProjectileMeshes(
            new CapsuleMesh
            {
                Radius = 0.018f, Height = 0.10f, RadialSegments = 8, Rings = 2,
                Material = Material(new Color("eaffff"), 1.8f),
            },
            new CapsuleMesh
            {
                Radius = 0.042f, Height = 0.17f, RadialSegments = 8, Rings = 2,
                Material = Material(color, 2.2f, 0.32f),
            },
            new CylinderMesh
            {
                TopRadius = 0.032f, BottomRadius = 0.002f, Height = 1,
                RadialSegments = 8, Rings = 1,
                Material = Material(color, 2.0f, 0.65f),
            });
        MeshesByColor.Add(color, meshes);
        return meshes;
    }

    private sealed record ProjectileMeshes(Mesh Core, Mesh Halo, Mesh Trail);

    public void Sample(float progress)
    {
        Progress = Math.Clamp(progress, 0, 1);
        Position = LaunchPosition.Lerp(TargetPosition, Progress);
        var length = Math.Min(0.38f, LaunchPosition.DistanceTo(Position));
        // Grow the trail behind the bolt, never back through the gun on its first frame.
        _trail.Visible = length > 0.001f;
        _trail.Position = Vector3.Down * length * 0.5f;
        _trail.Scale = new Vector3(1, Math.Max(0.001f, length), 1);
    }

    public object GetDiagnostics() => new
    {
        origin = new[] { LaunchPosition.X, LaunchPosition.Y, LaunchPosition.Z },
        position = new[] { Position.X, Position.Y, Position.Z },
        destination = new[] { TargetPosition.X, TargetPosition.Y, TargetPosition.Z },
        flight_seconds = FlightSeconds,
        progress = Progress,
    };

    private static StandardMaterial3D Material(Color color, float energy, float alpha = 1) => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        AlbedoColor = new Color(color, alpha),
        EmissionEnabled = true,
        Emission = color,
        EmissionEnergyMultiplier = energy,
        Transparency = alpha < 1 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
        BlendMode = alpha < 1 ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
    };
}
