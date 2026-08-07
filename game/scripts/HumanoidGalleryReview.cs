using Godot;

namespace SpaceAdventure.Game;

public partial class HumanoidGalleryReview : Node3D
{
    private const int MaximumPublishedBones = 64;

    private HumanoidPresentation _survivor = null!;
    private HumanoidPresentation _protectorIdle = null!;
    private HumanoidPresentation _protectorWalk = null!;

    public override void _Ready()
    {
        _survivor = GetNode<HumanoidPresentation>("Slots/SurvivorPresentation");
        _protectorIdle = GetNode<HumanoidPresentation>("Slots/ProtectorIdlePresentation");
        _protectorWalk = GetNode<HumanoidPresentation>("Slots/ProtectorWalkPresentation");

        _survivor.Synchronize(
            true,
            HumanoidPresentationAction.Idle,
            false,
            Vector3.Zero);
        _protectorIdle.Synchronize(
            true,
            HumanoidPresentationAction.Idle,
            false,
            Vector3.Zero);
        _protectorWalk.Synchronize(
            true,
            HumanoidPresentationAction.Locomotion,
            false,
            Vector3.Forward);

        if (OS.GetCmdlineUserArgs().Contains(
                "--humanoid-gallery-smoke",
                StringComparer.Ordinal))
        {
            _ = RunSmokeWithFailureHandlingAsync();
        }
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
            GD.PushError($"Humanoid gallery smoke failed: {exception.Message}\n{exception}");
            GetTree().Quit(1);
        }
    }

    private void RunSmoke()
    {
        RequireAnimation(_survivor, HumanoidPresentationAction.Idle);
        RequireAnimation(_survivor, HumanoidPresentationAction.DialogueSpeak);
        RequireAnimation(_survivor, HumanoidPresentationAction.DialogueListen);
        RequireAnimation(_protectorIdle, HumanoidPresentationAction.Idle);
        RequireAnimation(_protectorWalk, HumanoidPresentationAction.Locomotion);

        ValidateSkeleton(_survivor, "Survivor");
        ValidateSkeleton(_protectorIdle, "Protector");
        ValidateGrounding(_survivor, "Survivor");
        ValidateGrounding(_protectorIdle, "Protector");
        var survivorBounds = ValidateVisibleBounds(_survivor, "Survivor");
        var protectorBounds = ValidateVisibleBounds(_protectorIdle, "Protector");
        ValidateMaterialCount(_survivor, "Survivor", 8);
        ValidateMaterialCount(_protectorIdle, "Protector", 2);
        RequireDescendant(_protectorIdle, "socket.weapon.hand_primary");
        RequireDescendant(_protectorIdle, "socket.weapon.holster_primary");

        _survivor.Synchronize(
            true,
            HumanoidPresentationAction.DialogueSpeak,
            false,
            Vector3.Zero);
        if (_survivor.CurrentAction != HumanoidPresentationAction.DialogueSpeak)
        {
            throw new InvalidOperationException("Survivor did not enter dialogue speak presentation.");
        }

        _survivor.Synchronize(
            true,
            HumanoidPresentationAction.DialogueListen,
            true,
            Vector3.Zero);
        _protectorWalk.Synchronize(
            true,
            HumanoidPresentationAction.Locomotion,
            true,
            Vector3.Forward);
        if (!_survivor.PlaybackPaused || !_protectorWalk.PlaybackPaused)
        {
            throw new InvalidOperationException("Tactical pause did not freeze humanoid playback.");
        }

        GD.Print(
            "SPACEADVENTURE_HUMANOID_GALLERY_SMOKE "
            + $"survivor_bones={_survivor.SkeletonBoneCount} "
            + $"protector_bones={_protectorIdle.SkeletonBoneCount} "
            + $"survivor_bounds={survivorBounds} "
            + $"protector_bounds={protectorBounds} "
            + "grounded=true sockets=true materials=true pause=true");
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

    private static void ValidateSkeleton(HumanoidPresentation presentation, string label)
    {
        var boneCount = presentation.SkeletonBoneCount;
        if (boneCount is <= 0 or > MaximumPublishedBones)
        {
            throw new InvalidOperationException(
                $"{label} publishes invalid skeleton size {boneCount}.");
        }
    }

    private static void ValidateGrounding(HumanoidPresentation presentation, string label)
    {
        var minimumY = float.PositiveInfinity;
        foreach (var mesh in Descendants<MeshInstance3D>(presentation))
        {
            var bounds = mesh.GetAabb();
            for (var endpoint = 0; endpoint < 8; endpoint++)
            {
                minimumY = Math.Min(
                    minimumY,
                    (mesh.GlobalTransform * bounds.GetEndpoint(endpoint)).Y);
            }
        }

        var localGround = minimumY - presentation.GlobalPosition.Y;
        if (!float.IsFinite(localGround) || Math.Abs(localGround) > 0.04f)
        {
            throw new InvalidOperationException(
                $"{label} is not grounded at its presentation origin: {localGround:F5} m.");
        }
    }

    private static void ValidateMaterialCount(
        HumanoidPresentation presentation,
        string label,
        int maximum)
    {
        var materialIds = new HashSet<ulong>();
        foreach (var meshInstance in Descendants<MeshInstance3D>(presentation))
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

        if (materialIds.Count is 0 || materialIds.Count > maximum)
        {
            throw new InvalidOperationException(
                $"{label} publishes {materialIds.Count} materials; expected 1-{maximum}.");
        }
    }

    private static string ValidateVisibleBounds(
        HumanoidPresentation presentation,
        string label)
    {
        var minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        var meshCount = 0;
        foreach (var mesh in Descendants<MeshInstance3D>(presentation))
        {
            if (!mesh.IsVisibleInTree())
            {
                throw new InvalidOperationException($"{label} contains a hidden published mesh.");
            }

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
            throw new InvalidOperationException($"{label} has no visible published mesh.");
        }

        return $"{minimum}:{maximum}";
    }

    private static void RequireDescendant(Node root, string expectedName)
    {
        var importedName = expectedName.Replace('.', '_');
        if (!Descendants<Node>(root).Any(node =>
                string.Equals(node.Name.ToString(), expectedName, StringComparison.Ordinal)
                || string.Equals(node.Name.ToString(), importedName, StringComparison.Ordinal)
                || (node.HasMeta("socket_contract")
                    && string.Equals(
                        node.GetMeta("socket_contract").AsString(),
                        expectedName,
                        StringComparison.Ordinal))))
        {
            throw new InvalidOperationException(
                $"{root.Name} is missing required descendant '{expectedName}'.");
        }
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
