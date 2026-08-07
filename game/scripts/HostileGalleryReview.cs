using Godot;

namespace SpaceAdventure.Game;

public partial class HostileGalleryReview : Node3D
{
    private const int MaximumPublishedBones = 64;
    private const float PositionTolerance = 0.005f;

    private HumanoidPresentation _enforcerIdle = null!;
    private HumanoidPresentation _enforcerWalk = null!;
    private Node3D _sentryNeutral = null!;
    private Node3D _sentryAim = null!;
    private Node3D _sentryRecoil = null!;
    private readonly Dictionary<Node, Vector3> _recoilRestPositions = new();

    public override void _Ready()
    {
        _enforcerIdle = GetNode<HumanoidPresentation>("Slots/EnforcerIdlePresentation");
        _enforcerWalk = GetNode<HumanoidPresentation>("Slots/EnforcerWalkPresentation");
        _sentryNeutral = GetNode<Node3D>("Slots/SentryNeutral");
        _sentryAim = GetNode<Node3D>("Slots/SentryAim");
        _sentryRecoil = GetNode<Node3D>("Slots/SentryRecoil");
        CaptureSentryRest(_sentryNeutral);
        CaptureSentryRest(_sentryAim);
        CaptureSentryRest(_sentryRecoil);

        _enforcerIdle.Synchronize(true, HumanoidPresentationAction.Idle, false, Vector3.Zero);
        _enforcerWalk.Synchronize(
            true,
            HumanoidPresentationAction.Locomotion,
            false,
            Vector3.Forward);
        ApplySentryPose(_sentryAim, 30.0f, 12.0f, 0.0f);
        ApplySentryPose(_sentryRecoil, 0.0f, 0.0f, 0.08f);

        if (OS.GetCmdlineUserArgs().Contains(
                "--hostile-gallery-smoke",
                StringComparer.Ordinal))
        {
            _ = RunSmokeWithFailureHandlingAsync();
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key)
        {
            return;
        }

        var cameraPath = key.PhysicalKeycode switch
        {
            Key.Key1 => "ReviewCameras/ReviewCamera7_5m",
            Key.Key2 => "ReviewCameras/ReviewCamera14_5m",
            Key.Key3 => "ReviewCameras/ReviewCamera20m",
            _ => null,
        };
        if (cameraPath is null)
        {
            return;
        }

        GetNode<Camera3D>(cameraPath).Current = true;
        GetViewport().SetInputAsHandled();
    }

    private async Task RunSmokeWithFailureHandlingAsync()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            RunSmoke();
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PushError($"Hostile gallery smoke failed: {exception.Message}\n{exception}");
            GetTree().Quit(1);
        }
    }

    private void RunSmoke()
    {
        RequireAnimation(_enforcerIdle, HumanoidPresentationAction.Idle);
        RequireAnimation(_enforcerWalk, HumanoidPresentationAction.Locomotion);
        ValidateSkeleton(_enforcerIdle);
        ValidateHumanoidPublication(_enforcerIdle);
        ValidateMaterialCount(_enforcerIdle, "Security Enforcer", 3, 3);
        RequireDescendant(_enforcerIdle, "socket.attack.contact.primary");

        _enforcerIdle.Synchronize(true, HumanoidPresentationAction.Idle, true, Vector3.Zero);
        _enforcerWalk.Synchronize(
            true,
            HumanoidPresentationAction.Locomotion,
            true,
            Vector3.Forward);
        if (!_enforcerIdle.PlaybackPaused || !_enforcerWalk.PlaybackPaused)
        {
            throw new InvalidOperationException("Tactical pause did not freeze hostile playback.");
        }

        var neutralMetrics = ValidateSentry(_sentryNeutral);
        ValidateSentry(_sentryAim, validateRestBounds: false);
        ValidateSentry(_sentryRecoil, validateRestBounds: false);

        ValidateAimPose(_sentryAim, 30.0f, 12.0f);
        ValidateRecoilPose(_sentryRecoil, 0.08f);
        ResetSentryPose(_sentryAim);
        ResetSentryPose(_sentryRecoil);
        ValidateAimPose(_sentryAim, 0.0f, 0.0f);
        ValidateRecoilPose(_sentryRecoil, 0.0f);

        GD.Print(
            "SPACEADVENTURE_HOSTILE_GALLERY_SMOKE "
            + $"enforcer_bones={_enforcerIdle.SkeletonBoneCount} "
            + $"sentry_bounds={neutralMetrics} "
            + "grounded=true materials=true sockets=true hierarchy=true "
            + "aim_limits=true recoil=true reset=true pause=true");
    }

    private static void RequireAnimation(
        HumanoidPresentation presentation,
        HumanoidPresentationAction action)
    {
        if (!presentation.HasConfiguredAnimation(action))
        {
            throw new InvalidOperationException(
                $"{presentation.Name} does not expose required action {action}.");
        }
    }

    private static void ValidateSkeleton(HumanoidPresentation presentation)
    {
        var boneCount = presentation.SkeletonBoneCount;
        if (boneCount is <= 0 or > MaximumPublishedBones)
        {
            throw new InvalidOperationException(
                $"Security Enforcer publishes invalid skeleton size {boneCount}.");
        }
    }

    private static void ValidateHumanoidPublication(HumanoidPresentation presentation)
    {
        var candidates = Descendants<Node>(presentation)
            .Where(node => TryGetImportedMetadata(node, "publication_height_meters", out _))
            .ToList();
        if (candidates.Count != 1)
        {
            var nodes = string.Join(
                "; ",
                Descendants<Node>(presentation).Select(node =>
                    $"{node.Name}[{string.Join(",", node.GetMetaList())}]"));
            throw new InvalidOperationException(
                "Security Enforcer must publish exactly one rig with validated publication "
                + $"metrics; found {candidates.Count}. Nodes: {nodes}");
        }

        var rig = candidates[0];
        TryGetImportedMetadata(rig, "publication_height_meters", out var heightMetadata);
        if (!TryGetImportedMetadata(rig, "publication_ground_y_meters", out var groundMetadata))
        {
            throw new InvalidOperationException(
                "Security Enforcer rig is missing its ground publication metric.");
        }
        var height = (float)heightMetadata.AsDouble();
        var ground = (float)groundMetadata.AsDouble();
        ValidateStringMetadata(rig, "forward_axis", "-Z");
        ValidateStringMetadata(rig, "up_axis", "+Y");
        if (height is < 1.862f or > 1.938f || Math.Abs(ground) > PositionTolerance)
        {
            throw new InvalidOperationException(
                $"Security Enforcer publication metrics are invalid: height={height}, ground={ground}.");
        }

        if (!Descendants<MeshInstance3D>(presentation).Any(mesh => mesh.IsVisibleInTree()))
        {
            throw new InvalidOperationException("Security Enforcer has no visible published mesh.");
        }
    }

    private static string ValidateSentry(Node3D root, bool validateRestBounds = true)
    {
        if (Descendants<Skeleton3D>(root).Any())
        {
            throw new InvalidOperationException("Gun Sentry must not contain a skeleton.");
        }

        if (Descendants<AnimationPlayer>(root).Any())
        {
            throw new InvalidOperationException("Gun Sentry must not contain authored animation.");
        }

        var baseNode = RequireDescendant(root, "Base");
        var aim = RequireDescendant(root, "Aim_Pivot");
        var housing = RequireDescendant(root, "Gun_Housing");
        var sensor = RequireDescendant(root, "Threat_Sensor");
        var recoil = RequireDescendant(root, "Recoil");
        var barrel = RequireDescendant(root, "Barrel");
        var muzzle = RequireDescendant(root, "socket.attack.muzzle.primary");

        RequireParent(aim, baseNode);
        RequireParent(housing, aim);
        RequireParent(sensor, aim);
        RequireParent(recoil, aim);
        RequireParent(barrel, recoil);
        RequireParent(muzzle, recoil);
        ValidateFloatMetadata(aim, "yaw_min_degrees", -60.0f);
        ValidateFloatMetadata(aim, "yaw_max_degrees", 60.0f);
        ValidateFloatMetadata(aim, "pitch_min_degrees", -15.0f);
        ValidateFloatMetadata(aim, "pitch_max_degrees", 25.0f);
        ValidateFloatMetadata(recoil, "maximum_travel_metres", 0.08f);
        ValidateStringMetadata(recoil, "translation_axis_local", "+Z");
        ValidateStringMetadata(muzzle, "socket_contract", "socket.attack.muzzle.primary");
        ValidateStringMetadata(muzzle, "forward_axis", "-Z");
        ValidateStringMetadata(muzzle, "up_axis", "+Y");

        if (muzzle is not Node3D muzzle3D)
        {
            throw new InvalidOperationException("Gun Sentry muzzle socket must be a Node3D.");
        }

        var forward = muzzle3D.Transform.Basis * Vector3.Forward;
        if (forward.Normalized().Dot(Vector3.Forward) < 0.995f
            || (muzzle3D.Transform.Basis * Vector3.Up).Normalized().Dot(Vector3.Up) < 0.995f)
        {
            throw new InvalidOperationException("Gun Sentry muzzle axes do not publish -Z forward/+Y up.");
        }

        ValidateMaterialCount(root, "Gun Sentry", 3, 3);
        return validateRestBounds
            ? ValidateBounds(root, "Gun Sentry", 2.12f, 2.18f, 1.01f)
            : "pose-validated";
    }

    private static string ValidateBounds(
        Node root,
        string label,
        float minimumHeight,
        float maximumHeight,
        float maximumFootprint = float.PositiveInfinity)
    {
        var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var meshCount = 0;
        foreach (var mesh in Descendants<MeshInstance3D>(root))
        {
            meshCount++;
            var bounds = mesh.GetAabb();
            for (var endpoint = 0; endpoint < 8; endpoint++)
            {
                var point = mesh.GlobalTransform * bounds.GetEndpoint(endpoint);
                minimum = new Vector3(
                    Math.Min(minimum.X, point.X),
                    Math.Min(minimum.Y, point.Y),
                    Math.Min(minimum.Z, point.Z));
                maximum = new Vector3(
                    Math.Max(maximum.X, point.X),
                    Math.Max(maximum.Y, point.Y),
                    Math.Max(maximum.Z, point.Z));
            }
        }

        if (meshCount == 0)
        {
            throw new InvalidOperationException($"{label} has no published mesh.");
        }

        var rootPosition = root is Node3D node3D ? node3D.GlobalPosition : Vector3.Zero;
        var localMinimumY = minimum.Y - rootPosition.Y;
        var size = maximum - minimum;
        if (Math.Abs(localMinimumY) > 0.04f
            || size.Y < minimumHeight
            || size.Y > maximumHeight
            || size.X > maximumFootprint
            || size.Z > maximumFootprint)
        {
            throw new InvalidOperationException(
                $"{label} bounds are invalid: minimum={minimum}, maximum={maximum}, size={size}.");
        }

        return $"{minimum}:{maximum}";
    }

    private static void ValidateMaterialCount(
        Node root,
        string label,
        int minimum,
        int maximum)
    {
        var materialIds = new HashSet<ulong>();
        foreach (var meshInstance in Descendants<MeshInstance3D>(root))
        {
            if (meshInstance.Mesh is not Mesh mesh)
            {
                continue;
            }

            for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                var material = mesh.SurfaceGetMaterial(surface);
                if (material is not null)
                {
                    materialIds.Add(material.GetInstanceId());
                }
            }
        }

        if (materialIds.Count < minimum || materialIds.Count > maximum)
        {
            throw new InvalidOperationException(
                $"{label} publishes {materialIds.Count} materials; expected {minimum}-{maximum}.");
        }
    }

    private void CaptureSentryRest(Node root)
    {
        var recoil = RequireNode3DDescendant(root, "Recoil");
        _recoilRestPositions[recoil] = recoil.Position;
    }

    private void ApplySentryPose(Node root, float yaw, float pitch, float recoil)
    {
        var aim = RequireNode3DDescendant(root, "Aim_Pivot");
        var recoilNode = RequireNode3DDescendant(root, "Recoil");
        aim.RotationDegrees = new Vector3(pitch, yaw, 0.0f);
        recoilNode.Position = _recoilRestPositions[recoilNode] + Vector3.Back * recoil;
    }

    private void ResetSentryPose(Node root)
    {
        var aim = RequireNode3DDescendant(root, "Aim_Pivot");
        var recoil = RequireNode3DDescendant(root, "Recoil");
        aim.RotationDegrees = Vector3.Zero;
        recoil.Position = _recoilRestPositions[recoil];
    }

    private static void ValidateAimPose(Node root, float yaw, float pitch)
    {
        var actual = RequireNode3DDescendant(root, "Aim_Pivot").RotationDegrees;
        if (Math.Abs(actual.Y - yaw) > 0.01f || Math.Abs(actual.X - pitch) > 0.01f)
        {
            throw new InvalidOperationException(
                $"Gun Sentry aim pose is {actual}; expected pitch {pitch}, yaw {yaw}.");
        }
    }

    private void ValidateRecoilPose(Node root, float expected)
    {
        var recoil = RequireNode3DDescendant(root, "Recoil");
        var actual = recoil.Position.Z - _recoilRestPositions[recoil].Z;
        if (Math.Abs(actual - expected) > PositionTolerance)
        {
            throw new InvalidOperationException(
                $"Gun Sentry recoil is {actual:F4} m; expected {expected:F4} m.");
        }
    }

    private static void RequireParent(Node child, Node parent)
    {
        if (child.GetParent() != parent)
        {
            throw new InvalidOperationException(
                $"{child.Name} must be a direct child of {parent.Name}.");
        }
    }

    private static void ValidateFloatMetadata(Node node, string key, float expected)
    {
        if (!TryGetImportedMetadata(node, key, out var value)
            || Math.Abs(value.AsSingle() - expected) > 0.001f)
        {
            throw new InvalidOperationException(
                $"{node.Name} metadata '{key}' must equal {expected}.");
        }
    }

    private static void ValidateStringMetadata(Node node, string key, string expected)
    {
        if (!TryGetImportedMetadata(node, key, out var value)
            || !string.Equals(value.AsString(), expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{node.Name} metadata '{key}' must equal '{expected}'.");
        }
    }

    private static Node RequireDescendant(Node root, string expectedName)
    {
        var importedName = expectedName.Replace('.', '_');
        return Descendants<Node>(root).FirstOrDefault(node =>
                string.Equals(node.Name.ToString(), expectedName, StringComparison.Ordinal)
                || string.Equals(node.Name.ToString(), importedName, StringComparison.Ordinal)
                || (TryGetImportedMetadata(node, "socket_contract", out var socketContract)
                    && string.Equals(
                        socketContract.AsString(),
                        expectedName,
                        StringComparison.Ordinal)))
            ?? throw new InvalidOperationException(
                $"{root.Name} is missing required descendant '{expectedName}'.");
    }

    private static Node3D RequireNode3DDescendant(Node root, string expectedName)
    {
        var node = RequireDescendant(root, expectedName);
        return node as Node3D
            ?? throw new InvalidOperationException(
                $"{root.Name} descendant '{expectedName}' must be a Node3D.");
    }

    private static bool TryGetImportedMetadata(Node node, string key, out Variant value)
    {
        if (node.HasMeta(key))
        {
            value = node.GetMeta(key);
            return true;
        }

        if (node.HasMeta("extras"))
        {
            var extras = node.GetMeta("extras");
            if (extras.VariantType == Variant.Type.Dictionary
                && extras.AsGodotDictionary().TryGetValue(key, out value))
            {
                return true;
            }
        }

        value = default;
        return false;
    }

    private static IEnumerable<T> Descendants<T>(Node root)
        where T : Node
    {
        foreach (var child in root.GetChildren())
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
