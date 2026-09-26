using Godot;

namespace SpaceAdventure.Game;

/// <summary>
/// One strict overhead ship viewport (bow up, no yaw) showing a contracted production publication over a
/// starfield, with an FTL-style shield bubble. There is no greybox route: a missing publication is reported
/// through <see cref="LoadError"/>. Anchors and bounds are measured from accumulated local transforms, so they
/// are valid before the model enters the scene tree. HUD panels overlap the frame, so the camera frames the ship
/// inside <see cref="SafeInsets"/> rather than the whole viewport.
/// </summary>
public sealed class ShipBattleView
{
    public const float FloorY = 1.12f;
    public const float EffectMargin = .9f;
    private const float MinimumZoomFraction = .3f;
    private const float BubbleHeight = 2.9f;
    private const float StarParallax = .004f;
    private static Shader? _shieldShader, _starShader, _glowShader, _skyShader;
    private readonly Dictionary<string, Node3D> _doorLeaves = new(StringComparer.Ordinal);
    private Vector2 _shake;

    public ShipBattleView(string name, string publishedPath, Vector2 contractMaximum, Color shieldColor)
    {
        ContractMaximum = contractMaximum;
        Frame = new Control { Name = name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            ClipContents = true, MouseFilter = Control.MouseFilterEnum.Pass };
        Container = new SubViewportContainer { Name = "Viewport", Stretch = true, MouseFilter = Control.MouseFilterEnum.Stop };
        Container.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Frame.AddChild(Container);
        Overlay2D = new ShipOverlay { Name = "Overlay" };
        Overlay2D.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        Frame.AddChild(Overlay2D);
        Viewport = new SubViewport { OwnWorld3D = true, HandleInputLocally = false, Msaa3D = Godot.Viewport.Msaa.Msaa4X };
        Container.AddChild(Viewport);
        World = new Node3D { Name = "World" };
        Viewport.AddChild(World);
        _skyShader ??= ResourceLoader.Load<Shader>("res://shaders/ship_space_sky.gdshader");
        var environment = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color, BackgroundColor = new Color("02050a"),
            // The sky is never drawn; it only feeds ambient and reflections so metal hulls catch light.
            Sky = new Sky { SkyMaterial = new ShaderMaterial { Shader = _skyShader }, RadianceSize = Sky.RadianceSizeEnum.Size128 },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky, AmbientLightColor = new Color("5d7593"), AmbientLightEnergy = .6f,
            AmbientLightSkyContribution = .7f,
            ReflectedLightSource = Godot.Environment.ReflectionSource.Sky,
            TonemapMode = Godot.Environment.ToneMapper.Agx, TonemapExposure = 1f,
            GlowEnabled = true, GlowIntensity = .6f, GlowStrength = 1f, GlowBloom = 0, GlowHdrThreshold = 1f,
            GlowBlendMode = Godot.Environment.GlowBlendModeEnum.Additive,
            SsaoEnabled = true, SsaoRadius = .6f, SsaoIntensity = 1.6f,
        };
        // Tight glow on emissive strips and effects only; wide levels would fog the decks.
        for (var level = 0; level < 7; level++) { environment.SetGlowLevel(level, level is >= 1 and <= 3 ? 1 : 0); }
        World.AddChild(new WorldEnvironment { Environment = environment });
        // Warm key from the upper left, cool fill from the right; shadows give the roof-off rooms depth.
        World.AddChild(new DirectionalLight3D { Name = "Key", RotationDegrees = new Vector3(-58, 38, 0), LightEnergy = 1.1f,
            LightColor = new Color("ffe3c4"), ShadowEnabled = true, ShadowBlur = 1.5f });
        World.AddChild(new DirectionalLight3D { Name = "Fill", RotationDegrees = new Vector3(-70, -140, 0), LightEnergy = .35f, LightColor = new Color("8fb8ff") });
        Camera = new Camera3D { Projection = Camera3D.ProjectionType.Orthogonal, Rotation = new Vector3(-Mathf.Pi / 2, 0, 0), Far = 80 };
        World.AddChild(Camera);
        _starShader ??= ResourceLoader.Load<Shader>("res://shaders/ship_starfield.gdshader");
        // Both frames share one sky; each frame only shifts it by its own screen offset.
        Stars = new ShaderMaterial { Shader = _starShader };
        World.AddChild(new MeshInstance3D { Name = "Starfield", Mesh = new PlaneMesh { Size = new Vector2(160, 160) }, Position = new Vector3(0, -12, 0),
            MaterialOverride = Stars, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        Ship = new Node3D { Name = "Ship" };
        World.AddChild(Ship);
        Overlay = new Node3D { Name = "Overlay3D" };
        World.AddChild(Overlay);
        _shieldShader ??= ResourceLoader.Load<Shader>("res://shaders/ship_shield.gdshader");
        var shieldMaterial = new ShaderMaterial { Shader = _shieldShader, RenderPriority = 4 };
        shieldMaterial.SetShaderParameter("shield_color", shieldColor);
        Bubble = new MeshInstance3D { Name = "ShieldBubble", Mesh = new PlaneMesh { Size = new Vector2(2, 2) }, MaterialOverride = shieldMaterial,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        Overlay.AddChild(Bubble);
        if (!ResourceLoader.Exists(publishedPath) || ResourceLoader.Load<PackedScene>(publishedPath) is not { } scene)
        {
            LoadError = $"Missing production publication {publishedPath}";
            return;
        }
        var model = scene.Instantiate<Node3D>();
        model.Name = "PublishedModel";
        Ship.AddChild(model);
        Aabb? total = null;
        Walk(model, Transform3D.Identity, ref total);
        if (total is not { } aabb || aabb.Size.X < .5f)
        {
            LoadError = $"Publication {publishedPath} has no measurable geometry";
            return;
        }
        Bounds = new Rect2(aabb.Position.X, aabb.Position.Z, aabb.Size.X, aabb.Size.Z);
        var centre = Bounds.GetCenter();
        BubbleRadii = new Vector2(Bounds.Size.X / 2 + .55f, Bounds.Size.Y / 2 + .45f);
        Bubble.Position = new Vector3(centre.X, BubbleHeight, centre.Y);
        Bubble.Scale = new Vector3(BubbleRadii.X, 1, BubbleRadii.Y);
    }

    public Control Frame { get; }
    public SubViewportContainer Container { get; }
    public ShipOverlay Overlay2D { get; }
    public SubViewport Viewport { get; }
    public Node3D World { get; }
    public Node3D Ship { get; }
    public Node3D Overlay { get; }
    public Camera3D Camera { get; }
    public MeshInstance3D Bubble { get; }
    private ShaderMaterial Stars { get; }
    public string? LoadError { get; }
    public Vector2 ContractMaximum { get; }
    public Rect2 Bounds { get; private set; } = new(-3, -6, 6, 12);
    public Vector2 BubbleRadii { get; private set; } = new(3.5f, 6.5f);
    public bool BoundsWithinContract => Bounds.Size.X <= ContractMaximum.X + .05f && Bounds.Size.Y <= ContractMaximum.Y + .05f;
    public Dictionary<string, Vector3> Anchors { get; } = new(StringComparer.Ordinal);
    public Vector2 Pan { get; private set; }
    public float Zoom { get; private set; } = 1;
    public bool IsFramedAll => Mathf.IsEqualApprox(Zoom, 1) && Pan == Vector2.Zero;

    /// <summary>Frame-local pixels (left, top, right, bottom) covered by HUD panels; the ship is framed inside the rest.</summary>
    public Vector4 SafeInsets { get; set; }

    /// <summary>World extent that must stay visible at maximum zoom-out: hull, shield bubble and effect margin.</summary>
    public Rect2 FramedExtent
    {
        get
        {
            var centre = Bounds.GetCenter();
            var half = new Vector2(Math.Max(Bounds.Size.X / 2, BubbleRadii.X), Math.Max(Bounds.Size.Y / 2, BubbleRadii.Y)) + Vector2.One * EffectMargin / 2;
            return new Rect2(centre - half, half * 2);
        }
    }

    public Rect2 SafeRect(Vector2 frameSize) =>
        new(SafeInsets.X, SafeInsets.Y, Math.Max(40, frameSize.X - SafeInsets.X - SafeInsets.Z), Math.Max(40, frameSize.Y - SafeInsets.Y - SafeInsets.W));

    /// <summary>Metres per frame pixel that fits <see cref="FramedExtent"/> inside the safe rectangle.</summary>
    public float FitMetresPerPixel(Vector2 frameSize)
    {
        var safe = SafeRect(frameSize);
        var extent = FramedExtent.Size;
        return Math.Max(extent.Y / safe.Size.Y, extent.X / safe.Size.X);
    }

    public bool FitsAtMaximumZoomOut(Vector2 frameSize)
    {
        var safe = SafeRect(frameSize);
        var a = ToScreen(new Vector3(FramedExtent.Position.X, FloorY, FramedExtent.Position.Y));
        var b = ToScreen(new Vector3(FramedExtent.End.X, FloorY, FramedExtent.End.Y));
        var rect = new Rect2(a.Min(b), (a - b).Abs());
        return safe.Grow(1.5f).Encloses(rect);
    }

    public void FrameAll() { Zoom = 1; Pan = Vector2.Zero; UpdateCamera(); }

    public void ZoomBy(float factor) { Zoom = Math.Clamp(Zoom * factor, MinimumZoomFraction, 1); ClampPan(); UpdateCamera(); }

    public void FocusOn(Vector2 point, float zoom) { Zoom = Math.Clamp(zoom, MinimumZoomFraction, 1); Pan = point - Bounds.GetCenter(); ClampPan(); UpdateCamera(); }

    public void PanBy(Vector2 metres) { Pan += metres; ClampPan(); UpdateCamera(); }

    /// <summary>Presentation-only camera offset in metres (hull-hit shake), applied on top of pan and zoom.</summary>
    public void SetShake(Vector2 metres)
    {
        if (_shake == metres) { return; }
        _shake = metres;
        UpdateCamera();
    }

    public void UpdateCamera()
    {
        var frame = Container.Size.Max(Vector2.One);
        var fit = FitMetresPerPixel(frame);
        var metresPerPixel = fit * Zoom;
        Camera.Size = metresPerPixel * frame.Y;
        // Put the framed centre (plus pan) at the centre of the safe rectangle, not of the whole frame.
        var safe = SafeRect(frame);
        var offset = (safe.GetCenter() - frame / 2) * metresPerPixel;
        var centre = FramedExtent.GetCenter() + Pan - offset + _shake;
        Camera.Position = new Vector3(centre.X, 30, centre.Y);
        // Place this frame in the shared sky (in this SubViewport's pixels) and drift it slightly with the camera.
        var pixelScale = Viewport.Size.Y / Math.Max(1, frame.Y);
        Stars.SetShaderParameter("sky_offset", Frame.GlobalPosition * pixelScale);
        Stars.SetShaderParameter("parallax", (Pan + _shake) * StarParallax);
    }

    public float MetresPerPixel => Camera.Size / Math.Max(1, Container.Size.Y);

    /// <summary>World point to frame-local pixels (independent of SubViewport stretch).</summary>
    public Vector2 ToScreen(Vector3 world) => Camera.UnprojectPosition(world) * (Container.Size / ((Vector2)Viewport.Size).Max(Vector2.One));

    public Rect2 ScreenSilhouette()
    {
        var a = ToScreen(new Vector3(Bounds.Position.X, FloorY, Bounds.Position.Y));
        var b = ToScreen(new Vector3(Bounds.End.X, FloorY, Bounds.End.Y));
        return new Rect2(a.Min(b), (a - b).Abs());
    }

    /// <summary>Frame-local rectangle of a world XZ rectangle at floor height.</summary>
    public Rect2 ScreenRect(Vector2 centre, Vector2 size)
    {
        var a = ToScreen(new Vector3(centre.X - size.X / 2, FloorY, centre.Y - size.Y / 2));
        var b = ToScreen(new Vector3(centre.X + size.X / 2, FloorY, centre.Y + size.Y / 2));
        return new Rect2(a.Min(b), (a - b).Abs());
    }

    public Vector3? FloorPoint(Vector2 containerPosition)
    {
        var local = containerPosition * (((Vector2)Viewport.Size) / Container.Size.Max(Vector2.One));
        var origin = Camera.ProjectRayOrigin(local);
        var normal = Camera.ProjectRayNormal(local);
        if (Mathf.IsZeroApprox(normal.Y)) { return null; }
        return origin + normal * ((FloorY - origin.Y) / normal.Y);
    }

    /// <summary>World XZ where a frame-local pixel lands on the floor plane; frame edges map outside the hull.</summary>
    public Vector2 FloorAt(Vector2 framePosition) => FloorPoint(framePosition) is { } point ? new Vector2(point.X, point.Z) : Bounds.GetCenter();

    public void SetDoorOpen(string doorId, bool open)
    {
        if (_doorLeaves.TryGetValue(doorId, out var leaf)) { leaf.Visible = !open; }
    }

    public bool HasDoorLeaf(string doorId) => _doorLeaves.ContainsKey(doorId);

    public Vector3 Anchor(string name, Vector3 fallback) => Anchors.TryGetValue(name, out var anchor) ? anchor : fallback;

    /// <summary>Drives the bubble from observed layers and presentation-clock effect ages.</summary>
    public void SetShield(int layers, float collapse, float regrow, IReadOnlyList<Vector4> hits)
    {
        var material = (ShaderMaterial)Bubble.MaterialOverride;
        material.SetShaderParameter("layers", (float)layers);
        material.SetShaderParameter("collapse", collapse);
        material.SetShaderParameter("regrow", regrow);
        for (var index = 0; index < 4; index++)
        {
            material.SetShaderParameter($"hit{index}", index < hits.Count ? hits[index] : new Vector4(0, 0, -1, 0));
        }
        Bubble.Visible = layers > 0 || collapse > 0 || hits.Count > 0;
    }

    /// <summary>Bubble-plane coordinates (unit ellipse) of a world XZ point.</summary>
    public Vector2 BubbleLocal(Vector2 world) => (world - new Vector2(Bubble.Position.X, Bubble.Position.Z)) / BubbleRadii;

    /// <summary>Where a straight path from <paramref name="from"/> towards <paramref name="to"/> first meets the bubble ellipse.</summary>
    public Vector2 BubbleEntry(Vector2 from, Vector2 to)
    {
        var centre = new Vector2(Bubble.Position.X, Bubble.Position.Z);
        var a = (from - centre) / BubbleRadii;
        var d = (to - from) / BubbleRadii;
        var qa = d.Dot(d);
        var qb = 2 * a.Dot(d);
        var qc = a.Dot(a) - 1;
        var discriminant = qb * qb - 4 * qa * qc;
        if (qa < 1e-6f || discriminant < 0) { return to; }
        var t = (-qb - Mathf.Sqrt(discriminant)) / (2 * qa);
        return from + (to - from) * Math.Clamp(t, 0, 1);
    }

    private void ClampPan()
    {
        var limit = Bounds.Size / 2 * (1 - Zoom);
        Pan = new Vector2(Math.Clamp(Pan.X, -limit.X, limit.X), Math.Clamp(Pan.Y, -limit.Y, limit.Y));
    }

    private void Walk(Node node, Transform3D parent, ref Aabb? total)
    {
        foreach (var child in node.GetChildren())
        {
            var transform = child is Node3D spatial ? parent * spatial.Transform : parent;
            if (child is Node3D named)
            {
                var id = named.Name.ToString();
                Anchors.TryAdd(id, transform.Origin);
                if (id.StartsWith("door_", StringComparison.Ordinal)) { _doorLeaves.TryAdd(id, named); }
            }
            if (child is GeometryInstance3D geometry)
            {
                var box = transform * geometry.GetAabb();
                total = total is { } existing ? existing.Merge(box) : box;
            }
            Walk(child, transform, ref total);
        }
    }

    internal static IEnumerable<Node> Descendants(Node root)
    {
        foreach (var child in root.GetChildren())
        {
            yield return child;
            foreach (var nested in Descendants(child)) { yield return nested; }
        }
    }

    public static MeshInstance3D Flat(Vector3 size, Color color, Vector3 position) => new()
    {
        Mesh = new BoxMesh { Size = size }, Position = position, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        MaterialOverride = new StandardMaterial3D { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = color.A < 1 ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled },
    };

    public static MeshInstance3D Ring(float radius, Color color) => new()
    {
        Mesh = new TorusMesh { InnerRadius = radius * .86f, OuterRadius = radius, Rings = 48, RingSegments = 6 },
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Scale = new Vector3(1, .05f, 1),
        MaterialOverride = new StandardMaterial3D { AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, NoDepthTest = true, RenderPriority = 3 },
    };

    /// <summary>Additive glow quad (projectiles, flashes, sparks) using the shared glow shader.</summary>
    public static MeshInstance3D Glow(Color tint, float energy, float streak)
    {
        _glowShader ??= ResourceLoader.Load<Shader>("res://shaders/ship_glow_sprite.gdshader");
        var material = new ShaderMaterial { Shader = _glowShader, RenderPriority = 5 };
        material.SetShaderParameter("tint", tint);
        material.SetShaderParameter("energy", energy);
        material.SetShaderParameter("streak", streak);
        return new MeshInstance3D { Mesh = new PlaneMesh { Size = Vector2.One }, MaterialOverride = material, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    }
}
