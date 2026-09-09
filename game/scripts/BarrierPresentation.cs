using Godot;

namespace SpaceAdventure.Game;

/// <summary>Stationary ground barrier and placement preview. The core owns interception.</summary>
public partial class BarrierPresentation : Node3D
{
    private readonly StandardMaterial3D _surface = Material(.12f);
    private readonly StandardMaterial3D _edge = Material(.85f);
    private readonly StandardMaterial3D _grid = Material(.25f);
    private Node3D _shield = null!;
    private Node3D _guide = null!;
    private Label3D _label = null!;
    private double _hitTick = -100;
    private int _deploymentTicks;

    private static StandardMaterial3D Material(float alpha) => new()
    {
        AlbedoColor = new Color(.2f, .85f, 1, alpha), Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        EmissionEnabled = true, Emission = new Color("65e8ff"), EmissionEnergyMultiplier = .5f,
    };

    public void Build(float width, float height, int deploymentTicks)
    {
        _deploymentTicks = deploymentTicks;
        _shield = new Node3D(); AddChild(_shield);
        Vector2[] outline = [new(-.7f, -1), new(.7f, -1), new(1, -.7f), new(1, .7f),
            new(.7f, 1), new(-.7f, 1), new(-1, .7f), new(-1, -.7f)];
        var fill = new SurfaceTool(); fill.Begin(Mesh.PrimitiveType.Triangles);
        var edges = new SurfaceTool(); edges.Begin(Mesh.PrimitiveType.Triangles);
        for (var i = 0; i < outline.Length; i++)
        {
            var a = outline[i] * new Vector2(width / 2, height / 2);
            var b = outline[(i + 1) % outline.Length] * new Vector2(width / 2, height / 2);
            fill.AddVertex(Vector3.Zero); fill.AddVertex(new Vector3(a.X, a.Y, 0)); fill.AddVertex(new Vector3(b.X, b.Y, 0));
            AddLine(edges, a, b, .012f);
        }
        AddMesh(_shield, fill.Commit(), _surface); AddMesh(_shield, edges.Commit(), _edge);
        var grid = new SurfaceTool(); grid.Begin(Mesh.PrimitiveType.Triangles);
        const float radius = .24f;
        var columns = Mathf.CeilToInt(width / (3 * radius)) + 1;
        var rows = Mathf.CeilToInt(height / (2 * radius * Mathf.Sqrt(3))) + 1;
        for (var column = -columns; column <= columns; column++)
        for (var row = -rows; row <= rows; row++)
        {
            var center = new Vector2(column * radius * 1.5f, (row + (column % 2 == 0 ? 0 : .5f)) * radius * Mathf.Sqrt(3));
            for (var side = 0; side < 3; side++)
            {
                var a = center + Vector2.FromAngle(side * Mathf.Pi / 3) * radius;
                var b = center + Vector2.FromAngle((side + 1) * Mathf.Pi / 3) * radius;
                if (ClipLine(ref a, ref b, width, height)) { AddLine(grid, a, b, .012f); }
            }
        }
        AddMesh(_shield, grid.Commit(), _grid);
        var baseLine = new SurfaceTool(); baseLine.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var vertex in new[] { new Vector3(-width / 2, -height / 2 + .025f, -.055f), new Vector3(width / 2, -height / 2 + .025f, -.055f),
            new Vector3(width / 2, -height / 2 + .025f, .055f), new Vector3(-width / 2, -height / 2 + .025f, -.055f),
            new Vector3(width / 2, -height / 2 + .025f, .055f), new Vector3(-width / 2, -height / 2 + .025f, .055f) })
        { baseLine.AddVertex(vertex); }
        AddMesh(this, baseLine.Commit(), _edge);
        _guide = new Node3D(); AddChild(_guide);
        var arrow = new SurfaceTool(); arrow.Begin(Mesh.PrimitiveType.Triangles);
        foreach (var vertex in new[] { new Vector3(0, -height / 2 + .04f, -1), new Vector3(-.18f, -height / 2 + .04f, -.65f), new Vector3(.18f, -height / 2 + .04f, -.65f) })
        { arrow.AddVertex(vertex); }
        AddMesh(_guide, arrow.Commit(), _edge);
        var protectedSide = AddMesh(_guide, new PlaneMesh { Size = new Vector2(width, 1.2f) }, _surface);
        protectedSide.Position = new Vector3(0, -height / 2 + .03f, .65f);
        _label = new Label3D { Position = new Vector3(0, height / 2 + .3f, 0), FontSize = 24, OutlineSize = 7,
            PixelSize = .004f, Billboard = BaseMaterial3D.BillboardModeEnum.Enabled };
        _guide.AddChild(_label); Visible = false;
    }

    public void ShowShield(Vector3 center, Vector3 facing, Vector3 ground, double tick, double deployedTick,
        bool preview = false, bool valid = true, bool queued = false)
    {
        Visible = true; GlobalPosition = center;
        Rotation = new Vector3(0, Mathf.Atan2(-facing.X, -facing.Z), 0);
        var tint = !valid ? new Color("ff625f") : queued ? new Color("efc47d") : new Color("65e8ff");
        foreach (var material in new[] { _surface, _edge, _grid })
        {
            material.AlbedoColor = new Color(tint, material.AlbedoColor.A); material.Emission = tint;
            material.EmissionEnergyMultiplier = tick - _hitTick is >= 0 and < 6 ? 2 : .5f;
        }
        _guide.Visible = preview || queued;
        _label.Text = !valid ? "INVALID PLACEMENT" : queued ? "BARRIER QUEUED" : "CLICK TO PLACE";
        _label.Modulate = tint;
        var progress = preview || queued ? 1 : (float)Math.Clamp((tick - deployedTick) / _deploymentTicks, .04, 1);
        var expanded = Mathf.SmoothStep(0, 1, progress);
        _shield.Scale = new Vector3(expanded, expanded, 1);
        _shield.GlobalPosition = ground.Lerp(center, expanded);
    }

    public void NotifyBlocked(long tick) => _hitTick = tick;

    private static MeshInstance3D AddMesh(Node3D parent, Mesh mesh, Material material)
    {
        var view = new MeshInstance3D { Mesh = mesh, MaterialOverride = material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        parent.AddChild(view); return view;
    }

    private static void AddLine(SurfaceTool mesh, Vector2 a, Vector2 b, float width)
    {
        var normal = (b - a).Normalized().Orthogonal() * width / 2;
        foreach (var p in new[] { a - normal, b - normal, b + normal, a - normal, b + normal, a + normal })
        { mesh.AddVertex(new Vector3(p.X, p.Y, -.002f)); }
    }

    private static bool ClipLine(ref Vector2 a, ref Vector2 b, float width, float height)
    {
        var scale = new Vector2(width / 2, height / 2); a /= scale; b /= scale;
        foreach (var (normal, limit) in new (Vector2, float)[]
        { (Vector2.Right, 1), (Vector2.Left, 1), (Vector2.Up, 1), (Vector2.Down, 1),
            (new(1, 1), 1.7f), (new(1, -1), 1.7f), (new(-1, 1), 1.7f), (new(-1, -1), 1.7f) })
        {
            var da = a.Dot(normal) - limit; var db = b.Dot(normal) - limit;
            if (da > 0 && db > 0) { return false; }
            if (da > 0) { a = a.Lerp(b, da / (da - db)); }
            else if (db > 0) { b = a.Lerp(b, da / (da - db)); }
        }
        a *= scale; b *= scale; return a.DistanceSquaredTo(b) > .00001f;
    }
}
